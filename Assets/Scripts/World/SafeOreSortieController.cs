using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SafeOreSortieController : MonoBehaviour
{
    private const string ControllerObjectName = "Safe Ore Sortie Controller";
    private const string BoulderRootName = "Safe Ore Sortie Boulders";
    private const string DefaultOreTypeId = "windshale";

    [InspectorName("Meta State")]
    public MetaGameState metaGameState;
    [InspectorName("Ore Type")]
    public string oreTypeId = DefaultOreTypeId;
    [InspectorName("Boulder Count")]
    public int boulderCount = 4;
    [InspectorName("Boulder Ring Radius, m")]
    public float boulderRingRadiusMeters = 140f;
    [InspectorName("Boulder Height Above Entry, m")]
    public float boulderHeightAboveEntryMeters = 95f;
    [InspectorName("Shed Interval, s")]
    public float shedIntervalSeconds = 1.75f;
    [InspectorName("Chunk Min, kg")]
    public int chunkMinKg = 2;
    [InspectorName("Chunk Max, kg")]
    public int chunkMaxKg = 5;
    [InspectorName("Boulder Ore, kg")]
    public int orePerBoulderKg = 450;

    private readonly List<SafeOreBoulder> boulders = new List<SafeOreBoulder>();
    private Transform boulderRoot;
    private string activeSortieId = "";
    private int spawnedSortieSeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSafeOreSortieBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureControllerForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureControllerForGameplayScene();
    }

    private static void EnsureControllerForGameplayScene()
    {
        if (FindFirstObjectByType<WorldRegionRuntime>() == null)
        {
            return;
        }

        if (FindFirstObjectByType<SafeOreSortieController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        controllerObject.AddComponent<SafeOreSortieController>();
    }

    private void Update()
    {
        MetaGameState meta = ResolveMeta();
        if (meta == null || !meta.IsSafeOreSortieActive)
        {
            ClearBoulders();
            return;
        }

        SortieSessionState sortie = meta.ActiveSortie;
        if (sortie == null || sortie.zone == null)
        {
            ClearBoulders();
            return;
        }

        string sortieKey = sortie.startedUtcTicks + ":" + sortie.zone.sortieId;
        if (activeSortieId != sortieKey)
        {
            SpawnBoulders(meta, sortie);
        }
    }

    private MetaGameState ResolveMeta()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return metaGameState;
    }

    private void SpawnBoulders(MetaGameState meta, SortieSessionState sortie)
    {
        ClearBoulders();
        if (meta == null || sortie == null || sortie.zone == null) return;

        WorldConfigDatabase config = meta.WorldConfig;
        OreTypeConfig oreType = ResolveOreType(config);
        if (oreType == null || string.IsNullOrWhiteSpace(oreType.oreItemId)) return;

        activeSortieId = sortie.startedUtcTicks + ":" + sortie.zone.sortieId;
        spawnedSortieSeed = activeSortieId.GetHashCode();

        GameObject rootObject = new GameObject(BoulderRootName);
        boulderRoot = rootObject.transform;

        SortieZoneDefinition zone = sortie.zone;
        zone.Normalize();
        int count = Mathf.Max(1, boulderCount);
        float radius = Mathf.Clamp(boulderRingRadiusMeters, 10f, Mathf.Max(10f, zone.radiusMeters * 0.45f));
        float baseAngle = Mathf.Abs(spawnedSortieSeed % 360) * Mathf.Deg2Rad;
        float boulderY = zone.entryPosition.y + Mathf.Max(20f, boulderHeightAboveEntryMeters);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + i * Mathf.PI * 2f / count;
            Vector3 position = new Vector3(
                zone.centerPosition.x + Mathf.Cos(angle) * radius,
                boulderY + Mathf.Sin(i * 1.73f) * 12f,
                zone.centerPosition.z + Mathf.Sin(angle) * radius);

            GameObject boulderObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boulderObject.name = "Safe Ore Boulder " + (i + 1);
            boulderObject.transform.SetParent(boulderRoot, false);
            boulderObject.transform.position = position;
            boulderObject.transform.localScale = new Vector3(18f, 12f, 16f);

            Collider collider = boulderObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = boulderObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = oreType.color;
            }

            SafeOreBoulder boulder = boulderObject.AddComponent<SafeOreBoulder>();
            boulder.Initialize(oreType, Mathf.Max(1, orePerBoulderKg), Mathf.Max(0.25f, shedIntervalSeconds), Mathf.Max(1, chunkMinKg), Mathf.Max(chunkMinKg, chunkMaxKg), zone.stormFloorY);
            boulders.Add(boulder);
        }
    }

    private OreTypeConfig ResolveOreType(WorldConfigDatabase config)
    {
        if (config == null || !config.isLoaded) return null;

        OreTypeConfig oreType = config.GetOreType(string.IsNullOrWhiteSpace(oreTypeId) ? DefaultOreTypeId : oreTypeId);
        if (oreType != null) return oreType;

        return config.oreTypes != null && config.oreTypes.Count > 0 ? config.oreTypes[0] : null;
    }

    private void ClearBoulders()
    {
        activeSortieId = "";
        boulders.Clear();

        if (boulderRoot != null)
        {
            Destroy(boulderRoot.gameObject);
            boulderRoot = null;
        }
    }

    private sealed class SafeOreBoulder : MonoBehaviour
    {
        private OreTypeConfig oreType;
        private int remainingKg;
        private float shedIntervalSeconds;
        private int chunkMinKg;
        private int chunkMaxKg;
        private float stormFloorY;
        private float nextShedTime;

        public void Initialize(OreTypeConfig type, int oreKg, float intervalSeconds, int minKg, int maxKg, float stormY)
        {
            oreType = type;
            remainingKg = Mathf.Max(0, oreKg);
            shedIntervalSeconds = Mathf.Max(0.1f, intervalSeconds);
            chunkMinKg = Mathf.Max(1, minKg);
            chunkMaxKg = Mathf.Max(chunkMinKg, maxKg);
            stormFloorY = stormY;
            nextShedTime = Time.time + Random.Range(0.15f, shedIntervalSeconds);
        }

        private void Update()
        {
            if (oreType == null || remainingKg <= 0)
            {
                return;
            }

            if (Time.time < nextShedTime)
            {
                return;
            }

            nextShedTime = Time.time + shedIntervalSeconds;
            SpawnFragment();
        }

        private void SpawnFragment()
        {
            int amount = Mathf.Min(remainingKg, Random.Range(chunkMinKg, chunkMaxKg + 1));
            if (amount <= 0) return;

            remainingKg -= amount;
            GameObject fragmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fragmentObject.name = oreType.oreItemId + "_safe_sortie_fragment";
            fragmentObject.transform.position = transform.position
                + Vector3.down * 8f
                + Random.insideUnitSphere * 4f;
            fragmentObject.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.25f, Mathf.Clamp01(amount / 5f));

            Collider collider = fragmentObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MiningFragment fragment = fragmentObject.AddComponent<MiningFragment>();
            fragment.Initialize(oreType.oreItemId, amount, oreType.fragmentFallSpeedMS, stormFloorY, oreType.color);
        }
    }
}
