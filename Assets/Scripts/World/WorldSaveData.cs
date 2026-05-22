using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorldManifestData
{
    public int schemaVersion = 1;
    public int seed;
    public float worldSizeMeters;
    public float chunkSizeMeters;
    public string generatedAtUtc = "";
    public string manifestName = "";

    public List<WorldRegionRuntime.WorldChunkRecord> chunks = new List<WorldRegionRuntime.WorldChunkRecord>();
    public List<WorldRegionRuntime.WorldIslandRecord> islands = new List<WorldRegionRuntime.WorldIslandRecord>();
    public List<WorldRegionRuntime.WorldCloudFieldRecord> cloudFields = new List<WorldRegionRuntime.WorldCloudFieldRecord>();
    public List<WorldRegionRuntime.WorldResourceFieldRecord> resourceFields = new List<WorldRegionRuntime.WorldResourceFieldRecord>();
    public List<WorldRegionRuntime.WorldLeviathanRegionRecord> leviathanRegions = new List<WorldRegionRuntime.WorldLeviathanRegionRecord>();
    public List<WorldRegionRuntime.WorldIcebergFieldRecord> icebergFields = new List<WorldRegionRuntime.WorldIcebergFieldRecord>();

    public bool IsUsable => schemaVersion > 0 && worldSizeMeters > 0f && chunkSizeMeters > 0f && chunks != null && chunks.Count > 0;

    public static WorldManifestData FromRuntime(WorldRegionRuntime runtime, string name)
    {
        WorldManifestData data = new WorldManifestData();
        if (runtime == null)
        {
            return data;
        }

        if (runtime.Chunks.Count == 0)
        {
            runtime.GenerateStarterRegion();
        }

        data.schemaVersion = 1;
        data.seed = runtime.RegionSeed;
        data.worldSizeMeters = runtime.WorldSizeMeters;
        data.chunkSizeMeters = runtime.ChunkSizeMeters;
        data.generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        data.manifestName = string.IsNullOrWhiteSpace(name) ? "runtime_world" : name;
        data.chunks = WorldRegionManifest.CloneChunks(runtime.Chunks);
        data.islands = WorldRegionManifest.CloneIslands(runtime.Islands);
        data.cloudFields = WorldRegionManifest.CloneCloudFields(runtime.CloudFields);
        data.resourceFields = WorldRegionManifest.CloneResourceFields(runtime.ResourceFields);
        data.leviathanRegions = WorldRegionManifest.CloneLeviathanRegions(runtime.LeviathanRegions);
        data.icebergFields = WorldRegionManifest.CloneIcebergFields(runtime.IcebergFields);
        return data;
    }

    public static WorldManifestData FromManifest(WorldRegionManifest manifest)
    {
        WorldManifestData data = new WorldManifestData();
        if (manifest == null || !manifest.IsUsable)
        {
            return data;
        }

        data.schemaVersion = manifest.SchemaVersion;
        data.seed = manifest.Seed;
        data.worldSizeMeters = manifest.WorldSizeMeters;
        data.chunkSizeMeters = manifest.ChunkSizeMeters;
        data.generatedAtUtc = manifest.GeneratedAtUtc;
        data.manifestName = manifest.name;
        data.chunks = WorldRegionManifest.CloneChunks(manifest.Chunks);
        data.islands = WorldRegionManifest.CloneIslands(manifest.Islands);
        data.cloudFields = WorldRegionManifest.CloneCloudFields(manifest.CloudFields);
        data.resourceFields = WorldRegionManifest.CloneResourceFields(manifest.ResourceFields);
        data.leviathanRegions = WorldRegionManifest.CloneLeviathanRegions(manifest.LeviathanRegions);
        data.icebergFields = WorldRegionManifest.CloneIcebergFields(manifest.IcebergFields);
        return data;
    }
}

[Serializable]
public sealed class WorldRuntimeSaveData
{
    public int version = 1;
    public int manifestSeed;
    public string manifestName = "";
    public long savedUtcTicks;
    public Vector3 lastKnownPlayerPosition;
    public List<WorldRuntimeState.ChunkRuntimeState> chunks = new List<WorldRuntimeState.ChunkRuntimeState>();
    public List<WorldRuntimeState.EntityRuntimeState> entities = new List<WorldRuntimeState.EntityRuntimeState>();

    public bool IsUsable => version > 0;

    public static WorldRuntimeSaveData CreateInitial(WorldManifestData manifest)
    {
        return new WorldRuntimeSaveData
        {
            version = 1,
            manifestSeed = manifest != null ? manifest.seed : 0,
            manifestName = manifest != null ? manifest.manifestName : "",
            savedUtcTicks = DateTime.UtcNow.Ticks,
            lastKnownPlayerPosition = Vector3.zero,
            chunks = new List<WorldRuntimeState.ChunkRuntimeState>(),
            entities = new List<WorldRuntimeState.EntityRuntimeState>()
        };
    }
}
