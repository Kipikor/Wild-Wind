using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SortieResourceCacheController : MonoBehaviour
{
    private const string ControllerObjectName = "Sortie Resource Cache Controller";
    private const string CacheRootName = "Sortie Resource Caches";

    [InspectorName("Meta State")]
    public MetaGameState metaGameState;
    [InspectorName("Cache Count")]
    public int cacheCount = 3;
    [InspectorName("Cache Ring Radius, m")]
    public float cacheRingRadiusMeters = 120f;
    [InspectorName("Cache Height Above Entry, m")]
    public float cacheHeightAboveEntryMeters = 70f;

    private readonly List<ResourceCacheNode> caches = new List<ResourceCacheNode>();
    private Transform cacheRoot;
    private string activeSortieKey = "";

    public int ActiveCacheCount => caches.Count;
    public string ActiveResourceItemId { get; private set; } = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSortieResourceCacheBootstrap()
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
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        if (FindFirstObjectByType<SortieResourceCacheController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        controllerObject.AddComponent<SortieResourceCacheController>();
    }

    private void Update()
    {
        RefreshNow();
    }

    public void RefreshNow()
    {
        MetaGameState meta = ResolveMeta();
        if (meta == null || meta.CurrentMode != GameSessionMode.Flight || !meta.HasActiveSortie)
        {
            ClearCaches();
            return;
        }

        SortieSessionState sortie = meta.ActiveSortie;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;
        if (zone == null)
        {
            ClearCaches();
            return;
        }

        zone.Normalize();
        if (zone.sortieId == SessionExtractionConstants.QuickAdaptiveManualSortieId && zone.HasPayloadRewards)
        {
            ClearCaches();
            return;
        }

        if (zone.sortieId == SessionExtractionConstants.DefaultSafeOreSortieId && !zone.HasPayloadRewards)
        {
            ClearCaches();
            return;
        }

        if (string.IsNullOrWhiteSpace(zone.starterResourceItemId) && !zone.HasPayloadRewards)
        {
            ClearCaches();
            return;
        }

        string key = sortie.startedUtcTicks + ":" + zone.sortieId;
        if (activeSortieKey != key)
        {
            SpawnCaches(zone, key);
        }
    }

    public bool TrySpawnImmediateFragmentForActiveSortie(out string itemId)
    {
        itemId = "";
        if (caches.Count == 0)
        {
            RefreshNow();
        }

        for (int i = 0; i < caches.Count; i++)
        {
            ResourceCacheNode cache = caches[i];
            if (cache != null && cache.TrySpawnImmediateFragment(out itemId))
            {
                return true;
            }
        }

        return false;
    }

    private MetaGameState ResolveMeta()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return metaGameState;
    }

    private void SpawnCaches(SortieZoneDefinition zone, string key)
    {
        ClearCaches();
        if (zone == null) return;

        activeSortieKey = key ?? "";
        ActiveResourceItemId = zone.HasPayloadRewards && zone.payloadRewards.Count > 0
            ? zone.payloadRewards[0].itemId
            : zone.starterResourceItemId ?? "";
        int seed = activeSortieKey.GetHashCode();

        GameObject rootObject = new GameObject(CacheRootName);
        cacheRoot = rootObject.transform;

        int payloadCount = zone.HasPayloadRewards ? zone.payloadRewards.Count : 0;
        int count = payloadCount > 0 ? payloadCount : Mathf.Max(1, cacheCount);
        float radius = Mathf.Clamp(cacheRingRadiusMeters, 10f, Mathf.Max(10f, zone.radiusMeters * 0.40f));
        float baseAngle = Mathf.Abs(seed % 360) * Mathf.Deg2Rad;
        float y = zone.entryPosition.y + Mathf.Max(20f, cacheHeightAboveEntryMeters);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + i * Mathf.PI * 2f / count;
            Vector3 position = new Vector3(
                zone.centerPosition.x + Mathf.Cos(angle) * radius,
                y + Mathf.Sin(i * 1.41f) * 10f,
                zone.centerPosition.z + Mathf.Sin(angle) * radius);

            SortiePayloadRewardLine payload = payloadCount > 0 ? zone.payloadRewards[i] : null;
            string itemId = payload != null ? payload.itemId : zone.starterResourceItemId;
            Color color = payload != null ? payload.color : zone.starterResourceColor;

            GameObject cacheObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cacheObject.name = itemId + " sortie cache " + (i + 1);
            cacheObject.transform.SetParent(cacheRoot, false);
            cacheObject.transform.position = position;
            cacheObject.transform.localScale = new Vector3(10f, 7f, 10f);

            Collider collider = cacheObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = cacheObject.GetComponent<Renderer>();
            MiningFragment.ApplyRendererColor(renderer, color);

            ResourceCacheNode cache = cacheObject.AddComponent<ResourceCacheNode>();
            cache.Initialize(zone, payload);
            caches.Add(cache);
        }
    }

    private void ClearCaches()
    {
        activeSortieKey = "";
        ActiveResourceItemId = "";
        caches.Clear();

        if (cacheRoot != null)
        {
            Destroy(cacheRoot.gameObject);
            cacheRoot = null;
        }
    }

    private sealed class ResourceCacheNode : MonoBehaviour
    {
        private SortieZoneDefinition zone;
        private string itemId = "";
        private Color color = Color.white;
        private int chunkMin;
        private int chunkMax;
        private float shedIntervalSeconds;
        private int remainingPayloadAmount = -1;
        private float nextShedTime;

        public void Initialize(SortieZoneDefinition definition, SortiePayloadRewardLine payload)
        {
            zone = definition != null ? definition.Clone() : null;
            if (zone != null)
            {
                zone.Normalize();
                if (payload != null)
                {
                    payload.Normalize();
                    itemId = payload.itemId;
                    color = payload.color;
                    remainingPayloadAmount = payload.amount;
                    chunkMax = CalculatePayloadChunkMax(payload.amount);
                    chunkMin = Mathf.Max(1, Mathf.Min(chunkMax, Mathf.FloorToInt(chunkMax * 0.45f)));
                }
                else
                {
                    itemId = zone.starterResourceItemId;
                    color = zone.starterResourceColor;
                    remainingPayloadAmount = -1;
                    chunkMin = zone.starterResourceChunkMin;
                    chunkMax = zone.starterResourceChunkMax;
                }

                shedIntervalSeconds = zone.starterResourceShedIntervalSeconds;
                nextShedTime = Time.time + Random.Range(0.2f, shedIntervalSeconds);
            }
        }

        private void Update()
        {
            if (zone == null || string.IsNullOrWhiteSpace(itemId)) return;
            if (Time.time < nextShedTime) return;

            nextShedTime = Time.time + shedIntervalSeconds;
            SpawnFragment(out _);
        }

        public bool TrySpawnImmediateFragment(out string itemId)
        {
            itemId = "";
            return SpawnFragment(out itemId);
        }

        private bool SpawnFragment(out string itemId)
        {
            itemId = "";
            if (zone == null || string.IsNullOrWhiteSpace(this.itemId)) return false;
            if (!MiningFragment.CanSpawnMore) return false;
            if (remainingPayloadAmount == 0) return false;

            int amount = Random.Range(chunkMin, chunkMax + 1);
            if (remainingPayloadAmount > 0)
            {
                amount = Mathf.Min(amount, remainingPayloadAmount);
            }

            if (amount <= 0) return false;
            if (remainingPayloadAmount > 0)
            {
                remainingPayloadAmount = Mathf.Max(0, remainingPayloadAmount - amount);
            }

            GameObject fragmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fragmentObject.name = this.itemId + "_sortie_fragment";
            fragmentObject.transform.position = transform.position
                + Vector3.down * 6f
                + Random.insideUnitSphere * 3f;
            fragmentObject.transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1.0f, Mathf.Clamp01(amount / 5f));

            Collider collider = fragmentObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MiningFragment fragment = fragmentObject.AddComponent<MiningFragment>();
            fragment.Initialize(this.itemId, amount, 3.2f, zone.stormFloorY, color);
            itemId = this.itemId;
            return true;
        }

        private static int CalculatePayloadChunkMax(int amount)
        {
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, amount) / 12f), 1, 30);
        }
    }
}
