using UnityEngine;

[DisallowMultipleComponent]
public class MiningRockManager : MonoBehaviour
{
    public MetaGameState metaGameState;
    public bool spawnRocksOnPlay = true;
    public float syncIntervalSeconds = 1f;

    private Transform rockRoot;
    private float nextSyncTime;

    private MetaGameState Meta
    {
        get
        {
            if (metaGameState == null)
            {
                metaGameState = FindFirstObjectByType<MetaGameState>();
            }

            return metaGameState;
        }
    }

    private void Start()
    {
        SpawnConfiguredRocks();
    }

    private void Update()
    {
        MetaGameState meta = Meta;
        if (meta != null && meta.IsSessionExtractionCoreMode)
        {
            ClearConfiguredRocks();
            return;
        }

        if (!spawnRocksOnPlay || Time.unscaledTime < nextSyncTime) return;
        nextSyncTime = Time.unscaledTime + Mathf.Max(0.1f, syncIntervalSeconds);
        SpawnConfiguredRocks();
    }

    public void SpawnConfiguredRocks(bool forceRebuild = false)
    {
        if (!Application.isPlaying || !spawnRocksOnPlay) return;

        MetaGameState meta = Meta;
        if (meta == null || meta.progress == null || meta.WorldConfig == null || !meta.WorldConfig.isLoaded) return;
        if (meta.IsSessionExtractionCoreMode)
        {
            ClearConfiguredRocks();
            return;
        }

        long nowTicks = meta.CurrentProcessUtcNow.Ticks;
        if (forceRebuild)
        {
            meta.progress.miningRocks.Clear();
            for (int i = 0; i < meta.progress.miningZones.Count; i++)
            {
                MiningZoneState zone = meta.progress.miningZones[i];
                if (zone == null) continue;
                zone.initialRocksSpawned = false;
                zone.lastSpawnUtcTicks = 0;
                zone.spawnCounter = 0;
            }
        }

        MiningWorldSimulator.EnsureRuntime(meta.WorldConfig, meta.progress, nowTicks);

        if (rockRoot != null && forceRebuild)
        {
            Destroy(rockRoot.gameObject);
            rockRoot = null;
        }

        if (rockRoot == null)
        {
            GameObject root = new GameObject("Mining Rocks");
            rockRoot = root.transform;
        }

        for (int i = 0; i < meta.progress.miningRocks.Count; i++)
        {
            MiningRockState state = meta.progress.miningRocks[i];
            if (state == null || !MiningWorldSimulator.IsRockActive(meta.WorldConfig, state, nowTicks)) continue;
            if (MiningRock.FindById(state.rockId) != null) continue;

            MiningZoneConfig zone = meta.WorldConfig.GetMiningZone(state.zoneId);
            OreTypeConfig oreType = meta.WorldConfig.GetOreType(state.oreTypeId);
            if (zone == null || oreType == null) continue;

            GameObject rockObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rockObject.name = string.IsNullOrWhiteSpace(oreType.localNameRu) ? state.rockId : oreType.localNameRu;
            rockObject.transform.SetParent(rockRoot, false);

            Collider collider = rockObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MiningRock rock = rockObject.AddComponent<MiningRock>();
            rock.Initialize(meta, state);
        }
    }

    public void ClearConfiguredRocks()
    {
        if (rockRoot != null)
        {
            Destroy(rockRoot.gameObject);
            rockRoot = null;
        }
    }
}
