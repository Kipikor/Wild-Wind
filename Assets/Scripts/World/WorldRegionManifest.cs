using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldRegionManifest", menuName = "Wild Wind/World/Region Manifest")]
public sealed class WorldRegionManifest : ScriptableObject
{
    [SerializeField, InspectorName("Версия схемы")] private int schemaVersion = 1;
    [SerializeField, InspectorName("Сид")] private int seed;
    [SerializeField, InspectorName("Ширина региона, м")] private float worldSizeMeters;
    [SerializeField, InspectorName("Размер чанка, м")] private float chunkSizeMeters;
    [SerializeField, InspectorName("Создано UTC")] private string generatedAtUtc;

    [SerializeField, InspectorName("Чанки")] private List<WorldRegionRuntime.WorldChunkRecord> chunks = new List<WorldRegionRuntime.WorldChunkRecord>();
    [SerializeField, InspectorName("Острова")] private List<WorldRegionRuntime.WorldIslandRecord> islands = new List<WorldRegionRuntime.WorldIslandRecord>();
    [SerializeField, InspectorName("Облачные поля")] private List<WorldRegionRuntime.WorldCloudFieldRecord> cloudFields = new List<WorldRegionRuntime.WorldCloudFieldRecord>();
    [SerializeField, InspectorName("Ресурсные поля")] private List<WorldRegionRuntime.WorldResourceFieldRecord> resourceFields = new List<WorldRegionRuntime.WorldResourceFieldRecord>();
    [SerializeField, InspectorName("Зоны левиафанов")] private List<WorldRegionRuntime.WorldLeviathanRegionRecord> leviathanRegions = new List<WorldRegionRuntime.WorldLeviathanRegionRecord>();
    [SerializeField, InspectorName("Поля айсбергов")] private List<WorldRegionRuntime.WorldIcebergFieldRecord> icebergFields = new List<WorldRegionRuntime.WorldIcebergFieldRecord>();

    public int SchemaVersion => schemaVersion;
    public int Seed => seed;
    public float WorldSizeMeters => worldSizeMeters;
    public float ChunkSizeMeters => chunkSizeMeters;
    public string GeneratedAtUtc => generatedAtUtc;
    public IReadOnlyList<WorldRegionRuntime.WorldChunkRecord> Chunks => chunks;
    public IReadOnlyList<WorldRegionRuntime.WorldIslandRecord> Islands => islands;
    public IReadOnlyList<WorldRegionRuntime.WorldCloudFieldRecord> CloudFields => cloudFields;
    public IReadOnlyList<WorldRegionRuntime.WorldResourceFieldRecord> ResourceFields => resourceFields;
    public IReadOnlyList<WorldRegionRuntime.WorldLeviathanRegionRecord> LeviathanRegions => leviathanRegions;
    public IReadOnlyList<WorldRegionRuntime.WorldIcebergFieldRecord> IcebergFields => icebergFields;
    public bool IsUsable => schemaVersion > 0 && worldSizeMeters > 0f && chunkSizeMeters > 0f && chunks != null && chunks.Count > 0;

    public void CaptureFromRuntime(WorldRegionProfile profile, WorldRegionRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        schemaVersion = 1;
        seed = profile != null ? profile.Seed : 170517;
        worldSizeMeters = runtime.WorldSizeMeters;
        chunkSizeMeters = runtime.ChunkSizeMeters;
        generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        chunks = CloneChunks(runtime.Chunks);
        islands = CloneIslands(runtime.Islands);
        cloudFields = CloneCloudFields(runtime.CloudFields);
        resourceFields = CloneResourceFields(runtime.ResourceFields);
        leviathanRegions = CloneLeviathanRegions(runtime.LeviathanRegions);
        icebergFields = CloneIcebergFields(runtime.IcebergFields);
    }

    public static List<WorldRegionRuntime.WorldChunkRecord> CloneChunks(IReadOnlyList<WorldRegionRuntime.WorldChunkRecord> source)
    {
        List<WorldRegionRuntime.WorldChunkRecord> result = new List<WorldRegionRuntime.WorldChunkRecord>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            WorldRegionRuntime.WorldChunkRecord item = source[i];
            if (item == null) continue;
            result.Add(new WorldRegionRuntime.WorldChunkRecord
            {
                id = item.id,
                chunkX = item.chunkX,
                chunkZ = item.chunkZ,
                minX = item.minX,
                minZ = item.minZ,
                maxX = item.maxX,
                maxZ = item.maxZ,
                centerX = item.centerX,
                centerZ = item.centerZ,
                dominantBand = item.dominantBand,
                weatherSeed = item.weatherSeed,
                cloudDensity01 = item.cloudDensity01,
                resourceScore01 = item.resourceScore01,
                dangerScore01 = item.dangerScore01,
                discovered = item.discovered,
                hasSettlement = item.hasSettlement,
                notesRu = item.notesRu
            });
        }

        return result;
    }

    public static List<WorldRegionRuntime.WorldIslandRecord> CloneIslands(IReadOnlyList<WorldRegionRuntime.WorldIslandRecord> source)
    {
        List<WorldRegionRuntime.WorldIslandRecord> result = new List<WorldRegionRuntime.WorldIslandRecord>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            WorldRegionRuntime.WorldIslandRecord item = source[i];
            if (item == null) continue;
            result.Add(new WorldRegionRuntime.WorldIslandRecord
            {
                id = item.id,
                displayNameRu = item.displayNameRu,
                roleRu = item.roleRu,
                positionMeters = item.positionMeters,
                radiusMeters = item.radiusMeters,
                chunkId = item.chunkId,
                permanentEntity = item.permanentEntity,
                materializesAsSceneObject = item.materializesAsSceneObject
            });
        }

        return result;
    }

    public static List<WorldRegionRuntime.WorldCloudFieldRecord> CloneCloudFields(IReadOnlyList<WorldRegionRuntime.WorldCloudFieldRecord> source)
    {
        List<WorldRegionRuntime.WorldCloudFieldRecord> result = new List<WorldRegionRuntime.WorldCloudFieldRecord>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            WorldRegionRuntime.WorldCloudFieldRecord item = source[i];
            if (item == null) continue;
            result.Add(new WorldRegionRuntime.WorldCloudFieldRecord
            {
                id = item.id,
                displayNameRu = item.displayNameRu,
                centerMeters = item.centerMeters,
                radiusMeters = item.radiusMeters,
                thicknessMeters = item.thicknessMeters,
                density01 = item.density01,
                resourceId = item.resourceId,
                resourceKgEstimate = item.resourceKgEstimate,
                chunkId = item.chunkId,
                simulationOnlyWhenFar = item.simulationOnlyWhenFar
            });
        }

        return result;
    }

    public static List<WorldRegionRuntime.WorldResourceFieldRecord> CloneResourceFields(IReadOnlyList<WorldRegionRuntime.WorldResourceFieldRecord> source)
    {
        List<WorldRegionRuntime.WorldResourceFieldRecord> result = new List<WorldRegionRuntime.WorldResourceFieldRecord>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            WorldRegionRuntime.WorldResourceFieldRecord item = source[i];
            if (item == null) continue;
            result.Add(new WorldRegionRuntime.WorldResourceFieldRecord
            {
                id = item.id,
                displayNameRu = item.displayNameRu,
                centerMeters = item.centerMeters,
                radiusMeters = item.radiusMeters,
                altitudeBand = item.altitudeBand,
                resourceId = item.resourceId,
                resourceKgEstimate = item.resourceKgEstimate,
                chunkId = item.chunkId,
                materializesAsNodes = item.materializesAsNodes
            });
        }

        return result;
    }

    public static List<WorldRegionRuntime.WorldLeviathanRegionRecord> CloneLeviathanRegions(IReadOnlyList<WorldRegionRuntime.WorldLeviathanRegionRecord> source)
    {
        List<WorldRegionRuntime.WorldLeviathanRegionRecord> result = new List<WorldRegionRuntime.WorldLeviathanRegionRecord>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            WorldRegionRuntime.WorldLeviathanRegionRecord item = source[i];
            if (item == null) continue;
            result.Add(new WorldRegionRuntime.WorldLeviathanRegionRecord
            {
                id = item.id,
                displayNameRu = item.displayNameRu,
                centerMeters = item.centerMeters,
                radiusMeters = item.radiusMeters,
                altitudeMinMeters = item.altitudeMinMeters,
                altitudeMaxMeters = item.altitudeMaxMeters,
                rarity01 = item.rarity01,
                chunkId = item.chunkId
            });
        }

        return result;
    }

    public static List<WorldRegionRuntime.WorldIcebergFieldRecord> CloneIcebergFields(IReadOnlyList<WorldRegionRuntime.WorldIcebergFieldRecord> source)
    {
        List<WorldRegionRuntime.WorldIcebergFieldRecord> result = new List<WorldRegionRuntime.WorldIcebergFieldRecord>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            WorldRegionRuntime.WorldIcebergFieldRecord item = source[i];
            if (item == null) continue;
            result.Add(new WorldRegionRuntime.WorldIcebergFieldRecord
            {
                id = item.id,
                displayNameRu = item.displayNameRu,
                centerMeters = item.centerMeters,
                radiusMeters = item.radiusMeters,
                altitudeMeters = item.altitudeMeters,
                icebergCountEstimate = item.icebergCountEstimate,
                sublimateKgEstimate = item.sublimateKgEstimate,
                chunkId = item.chunkId
            });
        }

        return result;
    }
}
