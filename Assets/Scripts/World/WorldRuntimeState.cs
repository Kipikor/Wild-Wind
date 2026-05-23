using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldRuntimeState : MonoBehaviour
{
    private const int CurrentSaveVersion = 1;
    private const string DefaultSaveFileName = "wild_wind_world_runtime.json";

    [Header("References")]
    [SerializeField, InspectorName("World Runtime")] private WorldRegionRuntime world;
    [SerializeField, InspectorName("World Entity Index")] private WorldEntityIndex index;
    [SerializeField, InspectorName("Focus")] private Transform focus;

    [Header("Runtime")]
    [SerializeField, InspectorName("Initialize On Awake")] private bool initializeOnAwake = true;
    [SerializeField, InspectorName("Refresh Bubble On Start")] private bool refreshBubbleOnStart = true;
    [SerializeField, InspectorName("Save File Name")] private string saveFileName = DefaultSaveFileName;

    [Header("State")]
    [SerializeField, InspectorName("Chunks")] private List<ChunkRuntimeState> chunks = new List<ChunkRuntimeState>();
    [SerializeField, InspectorName("Entities")] private List<EntityRuntimeState> entities = new List<EntityRuntimeState>();

    private readonly Dictionary<string, ChunkRuntimeState> chunkStatesById = new Dictionary<string, ChunkRuntimeState>();
    private readonly Dictionary<string, EntityRuntimeState> entityStatesByKey = new Dictionary<string, EntityRuntimeState>();
    private readonly List<WorldEntityQueryResult> nearbyScratch = new List<WorldEntityQueryResult>(128);

    [SerializeField, HideInInspector] private Vector3 lastKnownPlayerPosition;
    [SerializeField, HideInInspector] private int loadedManifestSeed;
    [SerializeField, HideInInspector] private string loadedManifestName;

    public WorldRegionRuntime World => world;
    public WorldEntityIndex Index => index;
    public IReadOnlyList<ChunkRuntimeState> Chunks => chunks;
    public IReadOnlyList<EntityRuntimeState> Entities => entities;
    public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
    public int LoadedManifestSeed => loadedManifestSeed;
    public string LoadedManifestName => loadedManifestName;
    public int ChunkStateCount => chunks.Count;
    public int EntityStateCount => entities.Count;
    public string SavePath => Path.Combine(Application.persistentDataPath, string.IsNullOrWhiteSpace(saveFileName) ? DefaultSaveFileName : saveFileName);

    public int ActiveChunkCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] != null && chunks[i].activeInBubble)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int DiscoveredChunkCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i] != null && chunks[i].discovered)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int ActiveEntityCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i] != null && entities[i].activeInBubble)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private void Awake()
    {
        ResolveReferences();
        if (initializeOnAwake)
        {
            InitializeFromWorld(true);
        }
    }

    private void Start()
    {
        if (refreshBubbleOnStart && focus != null && world != null)
        {
            RefreshActiveBubble(focus.position, world.ActiveBubbleRadiusMeters);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(saveFileName))
        {
            saveFileName = DefaultSaveFileName;
        }
    }

    public void Configure(WorldRegionRuntime newWorld, WorldEntityIndex newIndex, Transform newFocus)
    {
        world = newWorld;
        index = newIndex;
        focus = newFocus;
        InitializeFromWorld(true);
    }

    [ContextMenu("Initialize From World")]
    public void InitializeFromWorld()
    {
        InitializeFromWorld(true);
    }

    public void InitializeFromWorld(bool preserveExisting)
    {
        ResolveReferences();
        if (world == null)
        {
            BuildRuntimeDictionaries();
            return;
        }

        if (world.Chunks.Count == 0)
        {
            world.GenerateStarterRegion();
        }

        if (index != null)
        {
            index.EnsureBuilt(world);
        }

        Dictionary<string, ChunkRuntimeState> previousChunks = preserveExisting ? CopyChunkMap() : null;
        Dictionary<string, EntityRuntimeState> previousEntities = preserveExisting ? CopyEntityMap() : null;

        chunks.Clear();
        entities.Clear();
        chunkStatesById.Clear();
        entityStatesByKey.Clear();

        long now = DateTime.UtcNow.Ticks;
        for (int i = 0; i < world.Chunks.Count; i++)
        {
            WorldRegionRuntime.WorldChunkRecord record = world.Chunks[i];
            if (record == null || string.IsNullOrWhiteSpace(record.id))
            {
                continue;
            }

            ChunkRuntimeState state = null;
            if (previousChunks != null)
            {
                previousChunks.TryGetValue(record.id, out state);
            }

            if (state == null)
            {
                state = new ChunkRuntimeState
                {
                    chunkId = record.id,
                    discovered = record.discovered,
                    firstDiscoveredUtcTicks = record.discovered ? now : 0L
                };
            }

            state.chunkId = record.id;
            state.activeInBubble = false;
            state.Normalize();
            chunks.Add(state);
            chunkStatesById[state.chunkId] = state;
        }

        AddEntityStates(world.Islands, previousEntities);
        AddEntityStates(world.CloudFields, previousEntities);
        AddEntityStates(world.ResourceFields, previousEntities);
        AddEntityStates(world.LeviathanRegions, previousEntities);
        AddEntityStates(world.IcebergFields, previousEntities);

        loadedManifestSeed = CurrentManifestSeed();
        loadedManifestName = CurrentManifestName();
    }

    public void RefreshActiveBubble(Vector3 positionMeters, float activeRadiusMeters, bool discover = true)
    {
        EnsureInitializedForCurrentWorld();
        lastKnownPlayerPosition = positionMeters;

        for (int i = 0; i < chunks.Count; i++)
        {
            if (chunks[i] != null)
            {
                chunks[i].activeInBubble = false;
            }
        }

        for (int i = 0; i < entities.Count; i++)
        {
            if (entities[i] != null)
            {
                entities[i].activeInBubble = false;
            }
        }

        if (world == null)
        {
            return;
        }

        long now = DateTime.UtcNow.Ticks;
        float chunkRadius = Mathf.Max(1f, activeRadiusMeters) + world.ChunkSizeMeters * 0.7071f;
        float chunkSqrRadius = chunkRadius * chunkRadius;
        for (int i = 0; i < world.Chunks.Count; i++)
        {
            WorldRegionRuntime.WorldChunkRecord chunk = world.Chunks[i];
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.id))
            {
                continue;
            }

            Vector3 chunkCenter = new Vector3(chunk.centerX, positionMeters.y, chunk.centerZ);
            if ((chunkCenter - positionMeters).sqrMagnitude > chunkSqrRadius)
            {
                continue;
            }

            if (chunkStatesById.TryGetValue(chunk.id, out ChunkRuntimeState state))
            {
                state.activeInBubble = true;
                state.visitCount++;
                state.lastVisitedUtcTicks = now;
                if (discover && !state.discovered)
                {
                    state.discovered = true;
                    state.firstDiscoveredUtcTicks = now;
                }
            }
        }

        if (index == null)
        {
            return;
        }

        index.EnsureBuilt(world);
        index.CollectNearby(positionMeters, Mathf.Max(1f, activeRadiusMeters), WorldEntityKind.All, nearbyScratch);
        for (int i = 0; i < nearbyScratch.Count; i++)
        {
            WorldEntityQueryResult result = nearbyScratch[i];
            if (!TryGetEntityState(result.kind, result.id, out EntityRuntimeState state))
            {
                continue;
            }

            state.activeInBubble = true;
            state.lastUpdatedUtcTicks = now;
            if (discover && !state.discovered)
            {
                state.discovered = true;
                state.firstDiscoveredUtcTicks = now;
            }
        }
    }

    private void EnsureInitializedForCurrentWorld()
    {
        ResolveReferences();
        if (world == null)
        {
            if ((chunks.Count > 0 && chunkStatesById.Count != chunks.Count) ||
                (entities.Count > 0 && entityStatesByKey.Count != entities.Count))
            {
                BuildRuntimeDictionaries();
            }

            return;
        }

        if (world.Chunks.Count == 0)
        {
            world.GenerateStarterRegion();
        }

        bool manifestMatches =
            loadedManifestSeed == CurrentManifestSeed() &&
            string.Equals(loadedManifestName ?? "", CurrentManifestName() ?? "", StringComparison.Ordinal);
        bool stateShapeMatches =
            chunks.Count == world.Chunks.Count &&
            entities.Count == GetWorldEntityCount();

        if (!manifestMatches || !stateShapeMatches)
        {
            InitializeFromWorld(true);
            return;
        }

        if (chunkStatesById.Count != chunks.Count || entityStatesByKey.Count != entities.Count)
        {
            BuildRuntimeDictionaries();
        }

        if (index != null)
        {
            index.EnsureBuilt(world);
        }
    }

    private int GetWorldEntityCount()
    {
        if (world == null)
        {
            return entities.Count;
        }

        return world.Islands.Count +
            world.CloudFields.Count +
            world.ResourceFields.Count +
            world.LeviathanRegions.Count +
            world.IcebergFields.Count;
    }

    public bool TryGetChunkState(string chunkId, out ChunkRuntimeState state)
    {
        if (chunkStatesById.Count == 0)
        {
            BuildRuntimeDictionaries();
        }

        if (string.IsNullOrWhiteSpace(chunkId))
        {
            state = null;
            return false;
        }

        return chunkStatesById.TryGetValue(chunkId, out state);
    }

    public bool TryGetEntityState(WorldEntityKind kind, string id, out EntityRuntimeState state)
    {
        if (entityStatesByKey.Count == 0)
        {
            BuildRuntimeDictionaries();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            state = null;
            return false;
        }

        return entityStatesByKey.TryGetValue(BuildEntityKey(kind, id), out state);
    }

    public bool MarkEntityDiscovered(WorldEntityKind kind, string id)
    {
        if (!TryGetEntityState(kind, id, out EntityRuntimeState state))
        {
            return false;
        }

        long now = DateTime.UtcNow.Ticks;
        if (!state.discovered)
        {
            state.discovered = true;
            state.firstDiscoveredUtcTicks = now;
        }

        state.lastUpdatedUtcTicks = now;
        return true;
    }

    public bool TryExtract(WorldEntityKind kind, string id, float amount, out float extracted)
    {
        extracted = 0f;
        if (amount <= 0f || !TryGetEntityState(kind, id, out EntityRuntimeState state))
        {
            return false;
        }

        if (state.depleted || state.remainingAmount <= 0f)
        {
            state.depleted = true;
            return false;
        }

        extracted = Mathf.Min(amount, state.remainingAmount);
        state.remainingAmount = Mathf.Max(0f, state.remainingAmount - extracted);
        state.depleted = state.remainingAmount <= 0.001f;
        state.interactionCount++;
        state.lastUpdatedUtcTicks = DateTime.UtcNow.Ticks;
        return extracted > 0f;
    }

    [ContextMenu("Save Runtime State")]
    public bool SaveToDisk()
    {
        return SaveToPath(SavePath);
    }

    [ContextMenu("Load Runtime State")]
    public bool LoadFromDisk()
    {
        return LoadFromPath(SavePath);
    }

    public bool SaveToPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            WorldRuntimeSaveData data = CreateSaveData();

            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, JsonUtility.ToJson(data, true), Encoding.UTF8);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("[WorldRuntimeState] Save failed: " + exception.Message, this);
            return false;
        }
    }

    public bool LoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            WorldRuntimeSaveData data = JsonUtility.FromJson<WorldRuntimeSaveData>(File.ReadAllText(path, Encoding.UTF8));
            return ApplySaveData(data);
        }
        catch (Exception exception)
        {
            Debug.LogError("[WorldRuntimeState] Load failed: " + exception.Message, this);
            return false;
        }
    }

    public WorldRuntimeSaveData CreateSaveData()
    {
        InitializeFromWorld(true);
        return new WorldRuntimeSaveData
        {
            version = CurrentSaveVersion,
            manifestSeed = CurrentManifestSeed(),
            manifestName = CurrentManifestName(),
            savedUtcTicks = DateTime.UtcNow.Ticks,
            lastKnownPlayerPosition = lastKnownPlayerPosition,
            chunks = CloneChunkRuntimeStates(chunks),
            entities = CloneEntityRuntimeStates(entities)
        };
    }

    public bool ApplySaveData(WorldRuntimeSaveData data)
    {
        if (data == null || !data.IsUsable)
        {
            return false;
        }

        chunks = CloneChunkRuntimeStates(data.chunks);
        entities = CloneEntityRuntimeStates(data.entities);
        lastKnownPlayerPosition = data.lastKnownPlayerPosition;
        loadedManifestSeed = data.manifestSeed;
        loadedManifestName = data.manifestName ?? "";
        BuildRuntimeDictionaries();
        InitializeFromWorld(true);
        return true;
    }

    [ContextMenu("Reset Runtime State")]
    public void ResetRuntimeState()
    {
        chunks.Clear();
        entities.Clear();
        chunkStatesById.Clear();
        entityStatesByKey.Clear();
        lastKnownPlayerPosition = focus != null ? focus.position : Vector3.zero;
        loadedManifestSeed = CurrentManifestSeed();
        loadedManifestName = CurrentManifestName();
        InitializeFromWorld(false);
    }

    private void ResolveReferences()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<WorldRegionRuntime>();
        }

        if (index == null)
        {
            index = FindFirstObjectByType<WorldEntityIndex>();
        }

        if (focus == null && world != null)
        {
            focus = world.Focus;
        }
    }

    private Dictionary<string, ChunkRuntimeState> CopyChunkMap()
    {
        BuildRuntimeDictionaries();
        return new Dictionary<string, ChunkRuntimeState>(chunkStatesById);
    }

    private Dictionary<string, EntityRuntimeState> CopyEntityMap()
    {
        BuildRuntimeDictionaries();
        return new Dictionary<string, EntityRuntimeState>(entityStatesByKey);
    }

    private void BuildRuntimeDictionaries()
    {
        chunkStatesById.Clear();
        entityStatesByKey.Clear();

        for (int i = 0; i < chunks.Count; i++)
        {
            ChunkRuntimeState state = chunks[i];
            if (state == null || string.IsNullOrWhiteSpace(state.chunkId))
            {
                continue;
            }

            state.Normalize();
            chunkStatesById[state.chunkId] = state;
        }

        for (int i = 0; i < entities.Count; i++)
        {
            EntityRuntimeState state = entities[i];
            if (state == null || string.IsNullOrWhiteSpace(state.id))
            {
                continue;
            }

            state.Normalize();
            entityStatesByKey[state.Key] = state;
        }
    }

    private void AddEntityStates(IReadOnlyList<WorldRegionRuntime.WorldIslandRecord> records, Dictionary<string, EntityRuntimeState> previous)
    {
        for (int i = 0; i < records.Count; i++)
        {
            WorldRegionRuntime.WorldIslandRecord record = records[i];
            if (record == null) continue;
            AddEntityState(WorldEntityKind.Island, record.id, record.chunkId, 0f, previous, record.permanentEntity && record.id == "capital");
        }
    }

    private void AddEntityStates(IReadOnlyList<WorldRegionRuntime.WorldCloudFieldRecord> records, Dictionary<string, EntityRuntimeState> previous)
    {
        for (int i = 0; i < records.Count; i++)
        {
            WorldRegionRuntime.WorldCloudFieldRecord record = records[i];
            if (record == null) continue;
            AddEntityState(WorldEntityKind.CloudField, record.id, record.chunkId, record.resourceKgEstimate, previous, false);
        }
    }

    private void AddEntityStates(IReadOnlyList<WorldRegionRuntime.WorldResourceFieldRecord> records, Dictionary<string, EntityRuntimeState> previous)
    {
        for (int i = 0; i < records.Count; i++)
        {
            WorldRegionRuntime.WorldResourceFieldRecord record = records[i];
            if (record == null) continue;
            AddEntityState(WorldEntityKind.ResourceField, record.id, record.chunkId, record.resourceKgEstimate, previous, false);
        }
    }

    private void AddEntityStates(IReadOnlyList<WorldRegionRuntime.WorldLeviathanRegionRecord> records, Dictionary<string, EntityRuntimeState> previous)
    {
        for (int i = 0; i < records.Count; i++)
        {
            WorldRegionRuntime.WorldLeviathanRegionRecord record = records[i];
            if (record == null) continue;
            AddEntityState(WorldEntityKind.LeviathanRegion, record.id, record.chunkId, 0f, previous, false);
        }
    }

    private void AddEntityStates(IReadOnlyList<WorldRegionRuntime.WorldIcebergFieldRecord> records, Dictionary<string, EntityRuntimeState> previous)
    {
        for (int i = 0; i < records.Count; i++)
        {
            WorldRegionRuntime.WorldIcebergFieldRecord record = records[i];
            if (record == null) continue;
            AddEntityState(WorldEntityKind.IcebergField, record.id, record.chunkId, record.sublimateKgEstimate, previous, false);
        }
    }

    private void AddEntityState(WorldEntityKind kind, string id, string chunkId, float initialAmount, Dictionary<string, EntityRuntimeState> previous, bool discoveredByDefault)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        string key = BuildEntityKey(kind, id);
        EntityRuntimeState state = null;
        if (previous != null)
        {
            previous.TryGetValue(key, out state);
        }

        long now = DateTime.UtcNow.Ticks;
        if (state == null)
        {
            state = new EntityRuntimeState
            {
                kind = kind,
                id = id,
                discovered = discoveredByDefault,
                firstDiscoveredUtcTicks = discoveredByDefault ? now : 0L
            };
        }

        float oldInitial = state.initialAmount;
        state.kind = kind;
        state.id = id;
        state.chunkId = string.IsNullOrWhiteSpace(chunkId) ? "out_of_region" : chunkId;
        state.activeInBubble = false;
        state.initialAmount = Mathf.Max(0f, initialAmount);
        if (oldInitial <= 0f && state.initialAmount > 0f)
        {
            state.remainingAmount = state.initialAmount;
            state.depleted = false;
        }
        else
        {
            state.remainingAmount = Mathf.Clamp(state.remainingAmount, 0f, state.initialAmount);
            state.depleted = state.initialAmount > 0f && state.remainingAmount <= 0.001f;
        }

        state.Normalize();
        entities.Add(state);
        entityStatesByKey[state.Key] = state;
    }

    private int CurrentManifestSeed()
    {
        if (world != null && world.Manifest != null)
        {
            return world.Manifest.Seed;
        }

        if (world != null && world.Profile != null)
        {
            return world.Profile.Seed;
        }

        if (world != null)
        {
            return world.RegionSeed;
        }

        return 0;
    }

    private string CurrentManifestName()
    {
        if (world != null && world.Manifest != null)
        {
            return world.Manifest.name;
        }

        if (world != null && world.Profile != null)
        {
            return world.Profile.name;
        }

        if (world != null)
        {
            return "runtime_seed_" + world.RegionSeed;
        }

        return "";
    }

    private static List<ChunkRuntimeState> CloneChunkRuntimeStates(IReadOnlyList<ChunkRuntimeState> source)
    {
        List<ChunkRuntimeState> result = new List<ChunkRuntimeState>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            ChunkRuntimeState item = source[i];
            if (item == null) continue;
            result.Add(new ChunkRuntimeState
            {
                chunkId = item.chunkId,
                discovered = item.discovered,
                activeInBubble = item.activeInBubble,
                visitCount = item.visitCount,
                firstDiscoveredUtcTicks = item.firstDiscoveredUtcTicks,
                lastVisitedUtcTicks = item.lastVisitedUtcTicks
            });
        }

        return result;
    }

    private static List<EntityRuntimeState> CloneEntityRuntimeStates(IReadOnlyList<EntityRuntimeState> source)
    {
        List<EntityRuntimeState> result = new List<EntityRuntimeState>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            EntityRuntimeState item = source[i];
            if (item == null) continue;
            result.Add(new EntityRuntimeState
            {
                kind = item.kind,
                id = item.id,
                chunkId = item.chunkId,
                discovered = item.discovered,
                activeInBubble = item.activeInBubble,
                depleted = item.depleted,
                initialAmount = item.initialAmount,
                remainingAmount = item.remainingAmount,
                interactionCount = item.interactionCount,
                firstDiscoveredUtcTicks = item.firstDiscoveredUtcTicks,
                lastUpdatedUtcTicks = item.lastUpdatedUtcTicks
            });
        }

        return result;
    }

    private static string BuildEntityKey(WorldEntityKind kind, string id)
    {
        return kind + ":" + id;
    }

    [Serializable]
    public sealed class ChunkRuntimeState
    {
        public string chunkId;
        public bool discovered;
        public bool activeInBubble;
        public int visitCount;
        public long firstDiscoveredUtcTicks;
        public long lastVisitedUtcTicks;

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(chunkId))
            {
                chunkId = "";
            }

            visitCount = Mathf.Max(0, visitCount);
            if (!discovered)
            {
                firstDiscoveredUtcTicks = 0L;
            }
        }
    }

    [Serializable]
    public sealed class EntityRuntimeState
    {
        public WorldEntityKind kind;
        public string id;
        public string chunkId;
        public bool discovered;
        public bool activeInBubble;
        public bool depleted;
        public float initialAmount;
        public float remainingAmount;
        public int interactionCount;
        public long firstDiscoveredUtcTicks;
        public long lastUpdatedUtcTicks;

        public string Key => BuildEntityKey(kind, id);

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = "";
            }

            if (string.IsNullOrWhiteSpace(chunkId))
            {
                chunkId = "out_of_region";
            }

            initialAmount = Mathf.Max(0f, initialAmount);
            remainingAmount = Mathf.Clamp(remainingAmount, 0f, initialAmount);
            interactionCount = Mathf.Max(0, interactionCount);
            depleted = initialAmount > 0f && remainingAmount <= 0.001f;
            if (!discovered)
            {
                firstDiscoveredUtcTicks = 0L;
            }
        }
    }
}
