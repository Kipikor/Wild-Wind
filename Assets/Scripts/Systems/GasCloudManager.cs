using UnityEngine;

public class GasCloudManager : MonoBehaviour
{
    [InspectorName("Мета-игра")]
    public MetaGameState metaGameState;
    [InspectorName("Создавать облака из конфигов")]
    public bool spawnConfigCloudsOnPlay = true;
    [InspectorName("Минимальный видимый радиус")]
    public float minVisualRadiusMeters = 3f;

    private Transform cloudRoot;

    public void SpawnConfiguredClouds(bool forceRebuild = false)
    {
        if (!Application.isPlaying || !spawnConfigCloudsOnPlay) return;

        MetaGameState meta = ResolveMeta();
        if (meta == null || meta.progress == null || meta.WorldConfig == null || !meta.WorldConfig.isLoaded) return;

        if (cloudRoot != null && !forceRebuild)
        {
            return;
        }

        if (cloudRoot != null)
        {
            Destroy(cloudRoot.gameObject);
            cloudRoot = null;
        }

        GameObject rootObject = new GameObject("Газовые облака из конфигов");
        cloudRoot = rootObject.transform;

        for (int i = 0; i < meta.WorldConfig.gasClouds.Count; i++)
        {
            GasCloudConfig cloudConfig = meta.WorldConfig.gasClouds[i];
            if (cloudConfig == null || string.IsNullOrWhiteSpace(cloudConfig.id)) continue;

            GasCloudTypeConfig cloudType = meta.WorldConfig.GetGasCloudType(cloudConfig.cloudTypeId);
            if (cloudType == null) continue;

            GasCloudState state = meta.progress.GetGasCloudState(cloudConfig.id, true);
            if (state != null && !state.initialized)
            {
                state.initialized = true;
                state.remainingVolumeLiters = Mathf.Max(0f, cloudConfig.initialVolumeLiters);
            }

            if (state != null && state.remainingVolumeLiters <= 0f) continue;

            GameObject cloudObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cloudObject.name = string.IsNullOrWhiteSpace(cloudConfig.localNameRu) ? cloudConfig.id : cloudConfig.localNameRu;
            cloudObject.transform.SetParent(cloudRoot, false);
            cloudObject.transform.position = cloudConfig.position;

            Collider collider = cloudObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            GasCloud cloud = cloudObject.AddComponent<GasCloud>();
            cloud.Initialize(cloudConfig, cloudType, state, this);
        }
    }

    public float GetRemainingLiters(string cloudId)
    {
        MetaGameState meta = ResolveMeta();
        if (meta == null || meta.progress == null || meta.WorldConfig == null) return 0f;

        GasCloudConfig config = meta.WorldConfig.GetGasCloud(cloudId);
        if (config == null) return 0f;

        GasCloudState state = meta.progress.GetGasCloudState(cloudId, true);
        if (state == null) return Mathf.Max(0f, config.initialVolumeLiters);
        if (!state.initialized)
        {
            state.initialized = true;
            state.remainingVolumeLiters = Mathf.Max(0f, config.initialVolumeLiters);
        }

        return Mathf.Max(0f, state.remainingVolumeLiters);
    }

    public float HarvestLiters(string cloudId, float sampledCubicMeters)
    {
        if (sampledCubicMeters <= 0f) return 0f;

        GasCloud liveCloud = GasCloud.FindById(cloudId);
        if (liveCloud != null)
        {
            return liveCloud.HarvestLiters(sampledCubicMeters);
        }

        MetaGameState meta = ResolveMeta();
        if (meta == null || meta.progress == null || meta.WorldConfig == null) return 0f;

        GasCloudConfig config = meta.WorldConfig.GetGasCloud(cloudId);
        if (config == null) return 0f;

        GasCloudTypeConfig type = meta.WorldConfig.GetGasCloudType(config.cloudTypeId);
        if (type == null) return 0f;

        GasCloudState state = meta.progress.GetGasCloudState(cloudId, true);
        if (state == null) return 0f;
        if (!state.initialized)
        {
            state.initialized = true;
            state.remainingVolumeLiters = Mathf.Max(0f, config.initialVolumeLiters);
        }

        float requestedLiters = sampledCubicMeters * Mathf.Max(0.0001f, type.condensateLitersPerCubicMeter);
        float harvestedLiters = Mathf.Min(requestedLiters, Mathf.Max(0f, state.remainingVolumeLiters));
        state.remainingVolumeLiters = Mathf.Max(0f, state.remainingVolumeLiters - harvestedLiters);
        return harvestedLiters;
    }

    public void NotifyCloudChanged(GasCloud cloud)
    {
    }

    private MetaGameState ResolveMeta()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return metaGameState;
    }
}
