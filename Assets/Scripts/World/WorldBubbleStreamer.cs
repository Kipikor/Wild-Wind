using System.Collections.Generic;
using UnityEngine;

public sealed class WorldBubbleStreamer : MonoBehaviour
{
    [Header("References")]
    [SerializeField, InspectorName("World Runtime")] private WorldRegionRuntime world;
    [SerializeField, InspectorName("World Entity Index")] private WorldEntityIndex worldIndex;
    [SerializeField, InspectorName("World Runtime State")] private WorldRuntimeState runtimeState;
    [SerializeField, InspectorName("Focus")] private Transform focus;
    [SerializeField, InspectorName("Materialized Root")] private Transform materializedRoot;

    [Header("Streaming")]
    [SerializeField, InspectorName("Refresh In Play Mode")] private bool refreshInPlayMode = true;
    [SerializeField, Range(50f, 2000f), InspectorName("Refresh Step, m")] private float refreshStepMeters = 350f;
    [SerializeField, Range(1f, 3f), InspectorName("Active Radius Multiplier")] private float activeRadiusMultiplier = 1.0f;
    [SerializeField, Range(1, 32), InspectorName("Max Islands")] private int maxIslandProxies = 10;
    [SerializeField, Range(1, 48), InspectorName("Max Cloud Fields")] private int maxCloudProxies = 18;
    [SerializeField, Range(1, 32), InspectorName("Max Resource Fields")] private int maxResourceProxies = 12;
    [SerializeField, Range(0, 8), InspectorName("Max Leviathan Regions")] private int maxLeviathanProxies = 3;
    [SerializeField, Range(0, 8), InspectorName("Max Iceberg Fields")] private int maxIcebergProxies = 2;

    [Header("Debug")]
    [SerializeField, InspectorName("Log Refreshes")] private bool logRefreshes = false;

    private readonly Dictionary<string, GameObject> activeProxies = new Dictionary<string, GameObject>();
    private readonly HashSet<string> wantedProxyKeys = new HashSet<string>();
    private readonly List<string> staleProxyKeys = new List<string>();
    private readonly List<Candidate> candidates = new List<Candidate>(64);

    private Vector3 lastFocusPosition;
    private bool hasRefreshed;

    private Material islandMaterial;
    private Material cloudMaterial;
    private Material resourceMaterial;
    private Material leviathanMaterial;
    private Material icebergMaterial;

    public int ActiveProxyCount => activeProxies.Count;
    public Transform MaterializedRoot => materializedRoot;
    public WorldEntityIndex Index => worldIndex;
    public WorldRuntimeState RuntimeState => runtimeState;

    public void Configure(WorldRegionRuntime newWorld, Transform newFocus)
    {
        Configure(newWorld, newFocus, null, null);
    }

    public void Configure(WorldRegionRuntime newWorld, Transform newFocus, WorldEntityIndex newIndex)
    {
        Configure(newWorld, newFocus, newIndex, null);
    }

    public void Configure(WorldRegionRuntime newWorld, Transform newFocus, WorldEntityIndex newIndex, WorldRuntimeState newRuntimeState)
    {
        world = newWorld;
        focus = newFocus;
        worldIndex = newIndex;
        runtimeState = newRuntimeState;
        if (runtimeState != null)
        {
            runtimeState.Configure(world, worldIndex, focus);
        }

        EnsureRoot();
        RefreshNow();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureRoot();
    }

    private void Start()
    {
        RefreshNow();
    }

    private void Update()
    {
        if (!refreshInPlayMode || !Application.isPlaying || focus == null)
        {
            return;
        }

        if (!hasRefreshed || Vector3.Distance(focus.position, lastFocusPosition) >= refreshStepMeters)
        {
            RefreshNow();
        }
    }

    [ContextMenu("Refresh Bubble Now")]
    public void RefreshNow()
    {
        ResolveReferences();
        EnsureRoot();
        if (world == null || focus == null || materializedRoot == null)
        {
            return;
        }

        if (worldIndex != null)
        {
            worldIndex.EnsureBuilt(world);
        }

        wantedProxyKeys.Clear();
        Vector3 focusPosition = focus.position;
        float activeRadius = Mathf.Max(500f, world.ActiveBubbleRadiusMeters * activeRadiusMultiplier);
        if (runtimeState != null)
        {
            runtimeState.RefreshActiveBubble(focusPosition, activeRadius);
        }

        RefreshIslandProxies(focusPosition, activeRadius);
        RefreshCloudProxies(focusPosition, activeRadius);
        RefreshResourceProxies(focusPosition, activeRadius);
        RefreshLeviathanProxies(focusPosition, activeRadius);
        RefreshIcebergProxies(focusPosition, activeRadius);
        RemoveStaleProxies();

        lastFocusPosition = focusPosition;
        hasRefreshed = true;

        if (logRefreshes)
        {
            Debug.Log("[WorldBubbleStreamer] Active proxies: " + activeProxies.Count + ", focus=" + focusPosition, this);
        }
    }

    [ContextMenu("Clear Materialized Bubble")]
    public void ClearMaterializedBubble()
    {
        foreach (GameObject proxy in activeProxies.Values)
        {
            DestroyProxy(proxy);
        }

        activeProxies.Clear();

        if (materializedRoot != null)
        {
            for (int i = materializedRoot.childCount - 1; i >= 0; i--)
            {
                DestroyProxy(materializedRoot.GetChild(i).gameObject);
            }
        }
    }

    private void ResolveReferences()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<WorldRegionRuntime>();
        }

        if (worldIndex == null)
        {
            worldIndex = FindFirstObjectByType<WorldEntityIndex>();
        }

        if (runtimeState == null)
        {
            runtimeState = FindFirstObjectByType<WorldRuntimeState>();
        }

        if (focus == null && world != null)
        {
            focus = world.Focus;
        }
    }

    private void EnsureRoot()
    {
        if (materializedRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("World Streamed Bubble");
        if (existing != null)
        {
            materializedRoot = existing;
            return;
        }

        GameObject root = new GameObject("World Streamed Bubble");
        root.transform.SetParent(transform, false);
        materializedRoot = root.transform;
    }

    private void RefreshIslandProxies(Vector3 focusPosition, float activeRadius)
    {
        BuildCandidates(world.Islands, focusPosition, activeRadius, maxIslandProxies, record => record.positionMeters, record => "island:" + record.id);
        for (int i = 0; i < candidates.Count; i++)
        {
            WorldRegionRuntime.WorldIslandRecord record = (WorldRegionRuntime.WorldIslandRecord)candidates[i].record;
            string key = candidates[i].key;
            wantedProxyKeys.Add(key);
            GameObject proxy = EnsureProxy(key, "Island - " + record.displayNameRu);
            if (proxy.transform.childCount == 0)
            {
                BuildIslandProxy(proxy.transform, record);
            }

            proxy.transform.position = record.positionMeters;
            proxy.transform.localScale = Vector3.one;
        }
    }

    private void RefreshCloudProxies(Vector3 focusPosition, float activeRadius)
    {
        BuildCandidates(world.CloudFields, focusPosition, activeRadius, maxCloudProxies, record => record.centerMeters, record => "cloud:" + record.id);
        for (int i = 0; i < candidates.Count; i++)
        {
            WorldRegionRuntime.WorldCloudFieldRecord record = (WorldRegionRuntime.WorldCloudFieldRecord)candidates[i].record;
            string key = candidates[i].key;
            wantedProxyKeys.Add(key);
            GameObject proxy = EnsureProxy(key, "Cloud Field - " + record.displayNameRu);
            if (proxy.transform.childCount == 0)
            {
                BuildSphereProxy(proxy.transform, "Cloud Volume", cloudMaterial ??= CreateMaterial("Runtime Cloud Proxy", new Color(0.92f, 0.95f, 1f, 0.33f), true));
            }

            proxy.transform.position = record.centerMeters;
            proxy.transform.localScale = Vector3.one;
            SetChildScale(proxy.transform, "Cloud Volume", new Vector3(record.radiusMeters * 2f, Mathf.Max(90f, record.thicknessMeters), record.radiusMeters * 2f));
        }
    }

    private void RefreshResourceProxies(Vector3 focusPosition, float activeRadius)
    {
        BuildCandidates(world.ResourceFields, focusPosition, activeRadius, maxResourceProxies, record => record.centerMeters, record => "resource:" + record.id);
        for (int i = 0; i < candidates.Count; i++)
        {
            WorldRegionRuntime.WorldResourceFieldRecord record = (WorldRegionRuntime.WorldResourceFieldRecord)candidates[i].record;
            string key = candidates[i].key;
            wantedProxyKeys.Add(key);
            GameObject proxy = EnsureProxy(key, "Resource Field - " + record.displayNameRu);
            if (proxy.transform.childCount == 0)
            {
                BuildCubeProxy(proxy.transform, "Resource Marker", resourceMaterial ??= CreateMaterial("Runtime Resource Proxy", new Color(0.16f, 0.78f, 1f, 0.48f), true));
            }

            float size = Mathf.Max(160f, record.radiusMeters * 0.45f);
            proxy.transform.position = record.centerMeters;
            proxy.transform.localScale = Vector3.one;
            SetChildScale(proxy.transform, "Resource Marker", Vector3.one * size);
        }
    }

    private void RefreshLeviathanProxies(Vector3 focusPosition, float activeRadius)
    {
        BuildCandidates(world.LeviathanRegions, focusPosition, activeRadius * 2f, maxLeviathanProxies, record => record.centerMeters, record => "leviathan:" + record.id);
        for (int i = 0; i < candidates.Count; i++)
        {
            WorldRegionRuntime.WorldLeviathanRegionRecord record = (WorldRegionRuntime.WorldLeviathanRegionRecord)candidates[i].record;
            string key = candidates[i].key;
            wantedProxyKeys.Add(key);
            GameObject proxy = EnsureProxy(key, "Leviathan Region - " + record.displayNameRu);
            if (proxy.transform.childCount == 0)
            {
                BuildSphereProxy(proxy.transform, "Leviathan Territory", leviathanMaterial ??= CreateMaterial("Runtime Leviathan Proxy", new Color(0.72f, 0.18f, 1f, 0.26f), true));
            }

            proxy.transform.position = record.centerMeters;
            proxy.transform.localScale = Vector3.one;
            SetChildScale(proxy.transform, "Leviathan Territory", Vector3.one * Mathf.Max(360f, record.radiusMeters * 0.22f));
        }
    }

    private void RefreshIcebergProxies(Vector3 focusPosition, float activeRadius)
    {
        BuildCandidates(world.IcebergFields, focusPosition, activeRadius * 4f, maxIcebergProxies, record => record.centerMeters, record => "iceberg:" + record.id);
        for (int i = 0; i < candidates.Count; i++)
        {
            WorldRegionRuntime.WorldIcebergFieldRecord record = (WorldRegionRuntime.WorldIcebergFieldRecord)candidates[i].record;
            string key = candidates[i].key;
            wantedProxyKeys.Add(key);
            GameObject proxy = EnsureProxy(key, "Iceberg Field - " + record.displayNameRu);
            if (proxy.transform.childCount == 0)
            {
                BuildCubeProxy(proxy.transform, "Iceberg Marker", icebergMaterial ??= CreateMaterial("Runtime Iceberg Proxy", new Color(0.65f, 0.9f, 1f, 0.42f), true));
            }

            proxy.transform.position = record.centerMeters;
            proxy.transform.localScale = Vector3.one;
            SetChildScale(proxy.transform, "Iceberg Marker", Vector3.one * Mathf.Max(280f, record.radiusMeters * 0.18f));
        }
    }

    private delegate Vector3 PositionSelector<T>(T record);
    private delegate string KeySelector<T>(T record);

    private void BuildCandidates<T>(IReadOnlyList<T> records, Vector3 focusPosition, float radius, int maxCount, PositionSelector<T> positionSelector, KeySelector<T> keySelector)
    {
        candidates.Clear();
        float sqrRadius = radius * radius;
        for (int i = 0; i < records.Count; i++)
        {
            T record = records[i];
            Vector3 position = positionSelector(record);
            float sqrDistance = HorizontalSqrDistance(position, focusPosition);
            if (sqrDistance > sqrRadius)
            {
                continue;
            }

            candidates.Add(new Candidate
            {
                record = record,
                key = keySelector(record),
                sqrDistance = sqrDistance
            });
        }

        candidates.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));
        if (candidates.Count > maxCount)
        {
            candidates.RemoveRange(maxCount, candidates.Count - maxCount);
        }
    }

    private GameObject EnsureProxy(string key, string displayName)
    {
        if (activeProxies.TryGetValue(key, out GameObject proxy) && proxy != null)
        {
            return proxy;
        }

        proxy = new GameObject(displayName);
        proxy.transform.SetParent(materializedRoot, false);
        activeProxies[key] = proxy;
        return proxy;
    }

    private void RemoveStaleProxies()
    {
        staleProxyKeys.Clear();
        foreach (string key in activeProxies.Keys)
        {
            if (!wantedProxyKeys.Contains(key))
            {
                staleProxyKeys.Add(key);
            }
        }

        for (int i = 0; i < staleProxyKeys.Count; i++)
        {
            string key = staleProxyKeys[i];
            if (activeProxies.TryGetValue(key, out GameObject proxy))
            {
                DestroyProxy(proxy);
            }

            activeProxies.Remove(key);
        }
    }

    private void BuildIslandProxy(Transform root, WorldRegionRuntime.WorldIslandRecord record)
    {
        islandMaterial ??= CreateMaterial("Runtime Island Proxy", new Color(0.24f, 0.28f, 0.30f, 1f), false);
        Material warmMaterial = CreateMaterial("Runtime Island Warm Lights", new Color(1f, 0.72f, 0.32f, 1f), false);
        float radius = Mathf.Max(90f, record.radiusMeters);

        CreatePrimitive("Rock", PrimitiveType.Cube, root, new Vector3(0f, -radius * 0.16f, 0f), new Vector3(radius * 1.45f, radius * 0.22f, radius * 1.02f), islandMaterial);
        CreatePrimitive("Platform", PrimitiveType.Cube, root, new Vector3(0f, 0f, 0f), new Vector3(radius * 1.18f, radius * 0.04f, radius * 0.78f), islandMaterial);
        CreatePrimitive("Beacon", PrimitiveType.Cube, root, new Vector3(-radius * 0.18f, radius * 0.13f, radius * 0.02f), new Vector3(radius * 0.18f, radius * 0.22f, radius * 0.16f), islandMaterial);
        CreatePrimitive("Warm Light", PrimitiveType.Sphere, root, new Vector3(-radius * 0.18f, radius * 0.28f, -radius * 0.14f), Vector3.one * radius * 0.06f, warmMaterial);
    }

    private void BuildSphereProxy(Transform root, string name, Material material)
    {
        CreatePrimitive(name, PrimitiveType.Sphere, root, Vector3.zero, Vector3.one, material);
    }

    private void BuildCubeProxy(Transform root, string name, Material material)
    {
        CreatePrimitive(name, PrimitiveType.Cube, root, Vector3.zero, Vector3.one, material);
    }

    private static void SetChildScale(Transform root, string childName, Vector3 scale)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            child.localScale = scale;
        }
    }

    private GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;

        MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyProxy(collider);
        }

        return primitive;
    }

    private Material CreateMaterial(string name, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader) { name = name, hideFlags = HideFlags.DontSave };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        if (transparent)
        {
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
        }

        return material;
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static void DestroyProxy(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private struct Candidate
    {
        public object record;
        public string key;
        public float sqrDistance;
    }
}
