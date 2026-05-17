using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum WorldEntityKind
{
    None = 0,
    Island = 1 << 0,
    CloudField = 1 << 1,
    ResourceField = 1 << 2,
    LeviathanRegion = 1 << 3,
    IcebergField = 1 << 4,
    All = Island | CloudField | ResourceField | LeviathanRegion | IcebergField
}

public readonly struct WorldEntityQueryResult
{
    public readonly WorldEntityKind kind;
    public readonly string id;
    public readonly string displayNameRu;
    public readonly string chunkId;
    public readonly Vector3 positionMeters;
    public readonly float radiusMeters;
    public readonly object record;
    public readonly float sqrDistance;

    public WorldEntityQueryResult(
        WorldEntityKind kind,
        string id,
        string displayNameRu,
        string chunkId,
        Vector3 positionMeters,
        float radiusMeters,
        object record,
        float sqrDistance)
    {
        this.kind = kind;
        this.id = id;
        this.displayNameRu = displayNameRu;
        this.chunkId = chunkId;
        this.positionMeters = positionMeters;
        this.radiusMeters = radiusMeters;
        this.record = record;
        this.sqrDistance = sqrDistance;
    }

    public WorldEntityQueryResult WithDistance(float newSqrDistance)
    {
        return new WorldEntityQueryResult(kind, id, displayNameRu, chunkId, positionMeters, radiusMeters, record, newSqrDistance);
    }
}

public sealed class WorldEntityIndex : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField, InspectorName("Мир")] private WorldRegionRuntime world;
    [SerializeField, InspectorName("Перестраивать при старте")] private bool rebuildOnAwake = true;

    private readonly Dictionary<string, WorldRegionRuntime.WorldChunkRecord> chunksById = new Dictionary<string, WorldRegionRuntime.WorldChunkRecord>();
    private readonly Dictionary<string, List<WorldEntityQueryResult>> recordsByChunk = new Dictionary<string, List<WorldEntityQueryResult>>();
    private readonly Dictionary<string, WorldEntityQueryResult> recordsByKey = new Dictionary<string, WorldEntityQueryResult>();
    private readonly List<WorldEntityQueryResult> scratch = new List<WorldEntityQueryResult>(128);

    public WorldRegionRuntime World => world;
    public bool IsBuilt => world != null && chunksById.Count > 0;
    public int ChunkCount => chunksById.Count;
    public int IndexedRecordCount { get; private set; }

    public void Configure(WorldRegionRuntime newWorld)
    {
        world = newWorld;
        Rebuild();
    }

    private void Awake()
    {
        ResolveReferences();
        if (rebuildOnAwake)
        {
            Rebuild();
        }
    }

    [ContextMenu("Перестроить индекс мира")]
    public void Rebuild()
    {
        ResolveReferences();
        ClearIndex();
        if (world == null)
        {
            return;
        }

        if (world.Chunks.Count == 0)
        {
            world.GenerateStarterRegion();
        }

        for (int i = 0; i < world.Chunks.Count; i++)
        {
            WorldRegionRuntime.WorldChunkRecord chunk = world.Chunks[i];
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.id))
            {
                continue;
            }

            chunksById[chunk.id] = chunk;
            EnsureChunkBucket(chunk.id);
        }

        AddIslands();
        AddCloudFields();
        AddResourceFields();
        AddLeviathanRegions();
        AddIcebergFields();
    }

    public void EnsureBuilt(WorldRegionRuntime expectedWorld = null)
    {
        if (expectedWorld != null && world != expectedWorld)
        {
            Configure(expectedWorld);
            return;
        }

        if (!IsBuilt)
        {
            Rebuild();
        }
    }

    public bool TryGetChunk(string chunkId, out WorldRegionRuntime.WorldChunkRecord chunk)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(chunkId))
        {
            chunk = null;
            return false;
        }

        return chunksById.TryGetValue(chunkId, out chunk);
    }

    public bool TryGet(WorldEntityKind kind, string id, out WorldEntityQueryResult result)
    {
        EnsureBuilt();
        if (string.IsNullOrWhiteSpace(id))
        {
            result = default;
            return false;
        }

        return recordsByKey.TryGetValue(BuildRecordKey(kind, id), out result);
    }

    public bool TryGetIsland(string id, out WorldRegionRuntime.WorldIslandRecord record)
    {
        bool found = TryGet(WorldEntityKind.Island, id, out WorldEntityQueryResult result);
        record = found ? result.record as WorldRegionRuntime.WorldIslandRecord : null;
        return record != null;
    }

    public bool TryGetCloudField(string id, out WorldRegionRuntime.WorldCloudFieldRecord record)
    {
        bool found = TryGet(WorldEntityKind.CloudField, id, out WorldEntityQueryResult result);
        record = found ? result.record as WorldRegionRuntime.WorldCloudFieldRecord : null;
        return record != null;
    }

    public bool TryGetResourceField(string id, out WorldRegionRuntime.WorldResourceFieldRecord record)
    {
        bool found = TryGet(WorldEntityKind.ResourceField, id, out WorldEntityQueryResult result);
        record = found ? result.record as WorldRegionRuntime.WorldResourceFieldRecord : null;
        return record != null;
    }

    public int CollectInChunk(string chunkId, WorldEntityKind mask, List<WorldEntityQueryResult> results)
    {
        EnsureBuilt();
        if (results == null)
        {
            return 0;
        }

        results.Clear();
        if (string.IsNullOrWhiteSpace(chunkId) || !recordsByChunk.TryGetValue(chunkId, out List<WorldEntityQueryResult> bucket))
        {
            return 0;
        }

        for (int i = 0; i < bucket.Count; i++)
        {
            WorldEntityQueryResult record = bucket[i];
            if ((record.kind & mask) != 0)
            {
                results.Add(record);
            }
        }

        return results.Count;
    }

    public int CollectNearby(Vector3 positionMeters, float radiusMeters, WorldEntityKind mask, List<WorldEntityQueryResult> results, int maxResults = 0, bool includeAltitude = false)
    {
        EnsureBuilt();
        if (results == null)
        {
            return 0;
        }

        results.Clear();
        if (world == null || radiusMeters <= 0f)
        {
            return 0;
        }

        scratch.Clear();
        float chunkRadius = radiusMeters + world.ChunkSizeMeters * 0.7071f;
        float chunkSqrRadius = chunkRadius * chunkRadius;
        float sqrRadius = radiusMeters * radiusMeters;

        foreach (WorldRegionRuntime.WorldChunkRecord chunk in chunksById.Values)
        {
            Vector3 chunkCenter = new Vector3(chunk.centerX, positionMeters.y, chunk.centerZ);
            if ((chunkCenter - positionMeters).sqrMagnitude > chunkSqrRadius)
            {
                continue;
            }

            if (!recordsByChunk.TryGetValue(chunk.id, out List<WorldEntityQueryResult> bucket))
            {
                continue;
            }

            for (int i = 0; i < bucket.Count; i++)
            {
                WorldEntityQueryResult candidate = bucket[i];
                if ((candidate.kind & mask) == 0)
                {
                    continue;
                }

                float sqrDistance = includeAltitude
                    ? (candidate.positionMeters - positionMeters).sqrMagnitude
                    : HorizontalSqrDistance(candidate.positionMeters, positionMeters);
                if (sqrDistance <= sqrRadius)
                {
                    scratch.Add(candidate.WithDistance(sqrDistance));
                }
            }
        }

        scratch.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));
        int count = maxResults > 0 ? Mathf.Min(maxResults, scratch.Count) : scratch.Count;
        for (int i = 0; i < count; i++)
        {
            results.Add(scratch[i]);
        }

        return results.Count;
    }

    public int CollectByAltitudeBand(WorldAltitudeBand band, WorldEntityKind mask, List<WorldEntityQueryResult> results, int maxResults = 0)
    {
        EnsureBuilt();
        if (results == null)
        {
            return 0;
        }

        results.Clear();
        if (world == null)
        {
            return 0;
        }

        foreach (WorldEntityQueryResult record in recordsByKey.Values)
        {
            if ((record.kind & mask) != 0 && world.EvaluateAltitudeBand(record.positionMeters.y) == band)
            {
                results.Add(record);
                if (maxResults > 0 && results.Count >= maxResults)
                {
                    break;
                }
            }
        }

        return results.Count;
    }

    private void ResolveReferences()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<WorldRegionRuntime>();
        }
    }

    private void ClearIndex()
    {
        chunksById.Clear();
        recordsByChunk.Clear();
        recordsByKey.Clear();
        scratch.Clear();
        IndexedRecordCount = 0;
    }

    private void AddIslands()
    {
        for (int i = 0; i < world.Islands.Count; i++)
        {
            WorldRegionRuntime.WorldIslandRecord record = world.Islands[i];
            if (record == null) continue;
            AddRecord(WorldEntityKind.Island, record.id, record.displayNameRu, record.chunkId, record.positionMeters, record.radiusMeters, record);
        }
    }

    private void AddCloudFields()
    {
        for (int i = 0; i < world.CloudFields.Count; i++)
        {
            WorldRegionRuntime.WorldCloudFieldRecord record = world.CloudFields[i];
            if (record == null) continue;
            AddRecord(WorldEntityKind.CloudField, record.id, record.displayNameRu, record.chunkId, record.centerMeters, record.radiusMeters, record);
        }
    }

    private void AddResourceFields()
    {
        for (int i = 0; i < world.ResourceFields.Count; i++)
        {
            WorldRegionRuntime.WorldResourceFieldRecord record = world.ResourceFields[i];
            if (record == null) continue;
            AddRecord(WorldEntityKind.ResourceField, record.id, record.displayNameRu, record.chunkId, record.centerMeters, record.radiusMeters, record);
        }
    }

    private void AddLeviathanRegions()
    {
        for (int i = 0; i < world.LeviathanRegions.Count; i++)
        {
            WorldRegionRuntime.WorldLeviathanRegionRecord record = world.LeviathanRegions[i];
            if (record == null) continue;
            AddRecord(WorldEntityKind.LeviathanRegion, record.id, record.displayNameRu, record.chunkId, record.centerMeters, record.radiusMeters, record);
        }
    }

    private void AddIcebergFields()
    {
        for (int i = 0; i < world.IcebergFields.Count; i++)
        {
            WorldRegionRuntime.WorldIcebergFieldRecord record = world.IcebergFields[i];
            if (record == null) continue;
            AddRecord(WorldEntityKind.IcebergField, record.id, record.displayNameRu, record.chunkId, record.centerMeters, record.radiusMeters, record);
        }
    }

    private void AddRecord(WorldEntityKind kind, string id, string displayNameRu, string chunkId, Vector3 positionMeters, float radiusMeters, object sourceRecord)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        string key = BuildRecordKey(kind, id);
        WorldEntityQueryResult result = new WorldEntityQueryResult(kind, id, displayNameRu, chunkId, positionMeters, radiusMeters, sourceRecord, 0f);
        recordsByKey[key] = result;
        EnsureChunkBucket(chunkId).Add(result);
        IndexedRecordCount++;
    }

    private List<WorldEntityQueryResult> EnsureChunkBucket(string chunkId)
    {
        string safeChunkId = string.IsNullOrWhiteSpace(chunkId) ? "out_of_region" : chunkId;
        if (!recordsByChunk.TryGetValue(safeChunkId, out List<WorldEntityQueryResult> bucket))
        {
            bucket = new List<WorldEntityQueryResult>();
            recordsByChunk[safeChunkId] = bucket;
        }

        return bucket;
    }

    private static string BuildRecordKey(WorldEntityKind kind, string id)
    {
        return kind + ":" + id;
    }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
