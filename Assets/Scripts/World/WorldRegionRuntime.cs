using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum WorldAltitudeBand
{
    DeadlyStorm,
    ViolentStorm,
    CalmStorm,
    Habitation,
    ThinAir,
    Ice,
    BeyondClaudiumLift
}

public sealed class WorldRegionRuntime : MonoBehaviour
{
    public const float DefaultWorldSizeMeters = 100000f;
    public const float DefaultChunkSizeMeters = 10000f;
    public const int DefaultChunkCountPerAxis = 10;

    [Header("Размер мира")]
    [SerializeField, InspectorName("Ширина региона, м")] private float worldSizeMeters = DefaultWorldSizeMeters;
    [SerializeField, InspectorName("Размер чанка, м")] private float chunkSizeMeters = DefaultChunkSizeMeters;
    [SerializeField, InspectorName("Сид региона")] private int regionSeed = 170517;

    [Header("Активный пузырь")]
    [SerializeField, InspectorName("Центр пузыря игрока")] private Transform focus;
    [SerializeField, Range(1000f, 15000f), InspectorName("Радиус активного пузыря, м")] private float activeBubbleRadiusMeters = 5000f;
    [SerializeField, Range(500f, 5000f), InspectorName("Радиус детальной материализации, м")] private float detailedBubbleRadiusMeters = 1600f;
    [SerializeField, InspectorName("Генерировать при запуске")] private bool generateOnAwake = true;

    [Header("Отладка")]
    [SerializeField, InspectorName("Рисовать гизмо мира")] private bool drawWorldGizmos = true;
    [SerializeField, InspectorName("Рисовать все чанки")] private bool drawAllChunks = true;
    [SerializeField, InspectorName("Рисовать дальние сущности")] private bool drawDistantRecords = true;

    [Header("Данные региона")]
    [SerializeField, InspectorName("Чанки")] private List<WorldChunkRecord> chunks = new List<WorldChunkRecord>();
    [SerializeField, InspectorName("Острова")] private List<WorldIslandRecord> islands = new List<WorldIslandRecord>();
    [SerializeField, InspectorName("Облачные поля")] private List<WorldCloudFieldRecord> cloudFields = new List<WorldCloudFieldRecord>();
    [SerializeField, InspectorName("Ресурсные поля")] private List<WorldResourceFieldRecord> resourceFields = new List<WorldResourceFieldRecord>();
    [SerializeField, InspectorName("Зоны левиафанов")] private List<WorldLeviathanRegionRecord> leviathanRegions = new List<WorldLeviathanRegionRecord>();
    [SerializeField, InspectorName("Поля айсбергов")] private List<WorldIcebergFieldRecord> icebergFields = new List<WorldIcebergFieldRecord>();

    public float WorldSizeMeters => worldSizeMeters;
    public float ChunkSizeMeters => chunkSizeMeters;
    public float ActiveBubbleRadiusMeters => activeBubbleRadiusMeters;
    public Transform Focus => focus;
    public IReadOnlyList<WorldChunkRecord> Chunks => chunks;
    public IReadOnlyList<WorldIslandRecord> Islands => islands;
    public IReadOnlyList<WorldCloudFieldRecord> CloudFields => cloudFields;
    public IReadOnlyList<WorldResourceFieldRecord> ResourceFields => resourceFields;
    public IReadOnlyList<WorldLeviathanRegionRecord> LeviathanRegions => leviathanRegions;
    public IReadOnlyList<WorldIcebergFieldRecord> IcebergFields => icebergFields;

    private int ChunkCountPerAxis => Mathf.Max(1, Mathf.RoundToInt(worldSizeMeters / Mathf.Max(1f, chunkSizeMeters)));

    private void Awake()
    {
        if (generateOnAwake && chunks.Count == 0)
        {
            GenerateStarterRegion();
        }
    }

    private void OnValidate()
    {
        worldSizeMeters = Mathf.Max(DefaultChunkSizeMeters, worldSizeMeters);
        chunkSizeMeters = Mathf.Clamp(chunkSizeMeters, 1000f, worldSizeMeters);
        activeBubbleRadiusMeters = Mathf.Max(1000f, activeBubbleRadiusMeters);
        detailedBubbleRadiusMeters = Mathf.Clamp(detailedBubbleRadiusMeters, 250f, activeBubbleRadiusMeters);
    }

    public void ConfigureFinalRegion(Transform newFocus)
    {
        focus = newFocus;
        worldSizeMeters = DefaultWorldSizeMeters;
        chunkSizeMeters = DefaultChunkSizeMeters;
        activeBubbleRadiusMeters = 5000f;
        detailedBubbleRadiusMeters = 1600f;
        regionSeed = 170517;
        GenerateStarterRegion();
    }

    [ContextMenu("Сгенерировать стартовый регион")]
    public void GenerateStarterRegion()
    {
        chunks.Clear();
        islands.Clear();
        cloudFields.Clear();
        resourceFields.Clear();
        leviathanRegions.Clear();
        icebergFields.Clear();

        GenerateChunks();
        GenerateIslands();
        GenerateCloudFields();
        GenerateResourceFields();
        GenerateLeviathanRegions();
        GenerateIcebergFields();
    }

    [ContextMenu("Вывести сводку мира")]
    public void LogWorldSummary()
    {
        Debug.Log(GetWorldSummaryRu(), this);
    }

    public string GetWorldSummaryRu()
    {
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("[WorldRegion] Регион: " + FormatKm(worldSizeMeters) + " x " + FormatKm(worldSizeMeters));
        builder.AppendLine("[WorldRegion] Чанк: " + FormatKm(chunkSizeMeters) + " x " + FormatKm(chunkSizeMeters) + ", всего: " + chunks.Count);
        builder.AppendLine("[WorldRegion] Острова: " + islands.Count + ", облачные поля: " + cloudFields.Count + ", ресурсные поля: " + resourceFields.Count);
        builder.AppendLine("[WorldRegion] Зоны левиафанов: " + leviathanRegions.Count + ", поля айсбергов: " + icebergFields.Count);
        builder.AppendLine("[WorldRegion] Активный пузырь: " + activeBubbleRadiusMeters.ToString("0") + " м, детальный пузырь: " + detailedBubbleRadiusMeters.ToString("0") + " м");

        if (focus != null)
        {
            WorldChunkRecord chunk = GetChunkAt(focus.position);
            builder.AppendLine("[WorldRegion] Фокус в чанке: " + (chunk != null ? chunk.id : "вне региона"));
            builder.AppendLine("[WorldRegion] Активных чанков рядом: " + CountActiveChunks(focus.position, activeBubbleRadiusMeters));
        }

        return builder.ToString();
    }

    public WorldAltitudeBand EvaluateAltitudeBand(float altitudeMeters)
    {
        if (altitudeMeters <= 0f) return WorldAltitudeBand.DeadlyStorm;
        if (altitudeMeters < 1000f) return WorldAltitudeBand.ViolentStorm;
        if (altitudeMeters < 2000f) return WorldAltitudeBand.CalmStorm;
        if (altitudeMeters < 10000f) return WorldAltitudeBand.Habitation;
        if (altitudeMeters < 40000f) return WorldAltitudeBand.ThinAir;
        if (altitudeMeters < 100000f) return WorldAltitudeBand.Ice;
        return WorldAltitudeBand.BeyondClaudiumLift;
    }

    public WorldChunkRecord GetChunkAt(Vector3 worldPositionMeters)
    {
        float half = worldSizeMeters * 0.5f;
        int count = ChunkCountPerAxis;
        int x = Mathf.FloorToInt((worldPositionMeters.x + half) / chunkSizeMeters);
        int z = Mathf.FloorToInt((worldPositionMeters.z + half) / chunkSizeMeters);
        if (x < 0 || z < 0 || x >= count || z >= count)
        {
            return null;
        }

        string id = BuildChunkId(x, z);
        for (int i = 0; i < chunks.Count; i++)
        {
            if (chunks[i].id == id)
            {
                return chunks[i];
            }
        }

        return null;
    }

    public int CountActiveChunks(Vector3 focusPositionMeters, float radiusMeters)
    {
        float radiusWithChunk = radiusMeters + chunkSizeMeters * 0.7071f;
        float sqr = radiusWithChunk * radiusWithChunk;
        int count = 0;
        for (int i = 0; i < chunks.Count; i++)
        {
            Vector3 center = new Vector3(chunks[i].centerX, focusPositionMeters.y, chunks[i].centerZ);
            if ((center - focusPositionMeters).sqrMagnitude <= sqr)
            {
                count++;
            }
        }

        return count;
    }

    private void GenerateChunks()
    {
        int count = ChunkCountPerAxis;
        float half = worldSizeMeters * 0.5f;

        for (int z = 0; z < count; z++)
        {
            for (int x = 0; x < count; x++)
            {
                float minX = -half + x * chunkSizeMeters;
                float minZ = -half + z * chunkSizeMeters;
                float centerX = minX + chunkSizeMeters * 0.5f;
                float centerZ = minZ + chunkSizeMeters * 0.5f;
                float weather = Mathf.PerlinNoise((x + regionSeed * 0.001f) * 0.31f, (z - regionSeed * 0.001f) * 0.31f);
                float resources = Mathf.PerlinNoise((x + 91f) * 0.43f, (z + 37f) * 0.43f);
                float danger = Mathf.PerlinNoise((x - 12f) * 0.57f, (z + 73f) * 0.57f);

                chunks.Add(new WorldChunkRecord
                {
                    id = BuildChunkId(x, z),
                    chunkX = x,
                    chunkZ = z,
                    minX = minX,
                    minZ = minZ,
                    maxX = minX + chunkSizeMeters,
                    maxZ = minZ + chunkSizeMeters,
                    centerX = centerX,
                    centerZ = centerZ,
                    dominantBand = ChooseDominantBand(weather, resources, danger),
                    weatherSeed = regionSeed + x * 73856093 ^ z * 19349663,
                    cloudDensity01 = Mathf.Clamp01(weather * 0.82f + resources * 0.18f),
                    resourceScore01 = resources,
                    dangerScore01 = danger,
                    discovered = x >= 4 && x <= 5 && z >= 4 && z <= 5,
                    hasSettlement = false,
                    notesRu = "Фоновая ячейка мира. Детальные объекты появляются только рядом с игроком."
                });
            }
        }
    }

    private void GenerateIslands()
    {
        CreateIsland("capital", "Гринхейвен", "Столица и торговый узел", new Vector3(0f, 2500f, 0f), 640f, true);

        string[] names =
        {
            "Соляной причал", "Медная марь", "Тихий док", "Лесная банка", "Пороховой пост",
            "Восточная верфь", "Сухая гавань", "Башня аптекарей", "Янтарный рынок", "Серый маяк",
            "Платформа угольщиков", "Грозовой склад", "Певчая пристань", "Северная мельница"
        };

        string[] roles =
        {
            "Еда и вода", "Руда и металл", "Доставка", "Древесина", "Порох",
            "Корабельные работы", "Топливо", "Медицина", "Торговля", "Навигация",
            "Уголь", "Опасные товары", "Пассажиры", "Механизмы"
        };

        System.Random random = new System.Random(regionSeed + 11);
        for (int i = 0; i < names.Length; i++)
        {
            Vector3 position = PickSpacedHorizontalPosition(random, 8500f, 45500f, 4200f);
            position.y = NextFloat(random, 2300f, 8800f);
            CreateIsland("island_" + (i + 1).ToString("00"), names[i], roles[i], position, NextFloat(random, 260f, 760f), true);
        }
    }

    private void GenerateCloudFields()
    {
        string[] resources =
        {
            "water_vapor", "claudium_trace", "food_spores", "sulfur_haze", "cloth_fiber",
            "cold_mist", "storm_charge", "crystal_dust"
        };

        System.Random random = new System.Random(regionSeed + 29);
        for (int i = 0; i < 34; i++)
        {
            bool highLayer = i >= 26;
            Vector3 center = PickSpacedHorizontalPosition(random, 2500f, 48000f, 0f);
            center.y = highLayer ? NextFloat(random, 11000f, 36000f) : NextFloat(random, 2200f, 9600f);
            float radius = highLayer ? NextFloat(random, 650f, 2100f) : NextFloat(random, 1200f, 5200f);
            float density = highLayer ? NextFloat(random, 0.12f, 0.38f) : NextFloat(random, 0.35f, 0.95f);
            string resource = resources[i % resources.Length];

            cloudFields.Add(new WorldCloudFieldRecord
            {
                id = "cloud_field_" + i.ToString("00"),
                displayNameRu = highLayer ? "Редкое высотное облако " + (i - 25) : "Облачное поле " + (i + 1),
                centerMeters = center,
                radiusMeters = radius,
                thicknessMeters = highLayer ? NextFloat(random, 180f, 620f) : NextFloat(random, 420f, 1900f),
                density01 = density,
                resourceId = resource,
                resourceKgEstimate = Mathf.RoundToInt(radius * density * (highLayer ? 18f : 46f)),
                chunkId = GetChunkIdAt(center),
                simulationOnlyWhenFar = true
            });
        }
    }

    private void GenerateResourceFields()
    {
        string[] ores = { "windshale", "dawnspar", "bluebrass", "stormbone", "claudreef", "mirrorbasalt", "crownstone" };
        System.Random random = new System.Random(regionSeed + 43);
        for (int i = 0; i < 11; i++)
        {
            bool highValue = i >= 8;
            Vector3 center = PickSpacedHorizontalPosition(random, 6000f, 47000f, 0f);
            center.y = highValue ? NextFloat(random, 12000f, 32000f) : NextFloat(random, 2600f, 9200f);

            resourceFields.Add(new WorldResourceFieldRecord
            {
                id = "ore_field_" + i.ToString("00"),
                displayNameRu = highValue ? "Высотная рудная глыба " + (i - 7) : "Рудное поле " + (i + 1),
                centerMeters = center,
                radiusMeters = highValue ? NextFloat(random, 800f, 1900f) : NextFloat(random, 1200f, 3200f),
                altitudeBand = EvaluateAltitudeBand(center.y),
                resourceId = ores[i % ores.Length],
                resourceKgEstimate = Mathf.RoundToInt(NextFloat(random, highValue ? 180000f : 45000f, highValue ? 620000f : 180000f)),
                chunkId = GetChunkIdAt(center),
                materializesAsNodes = true
            });
        }
    }

    private void GenerateLeviathanRegions()
    {
        System.Random random = new System.Random(regionSeed + 61);
        for (int i = 0; i < 5; i++)
        {
            bool high = i >= 3;
            Vector3 center = PickSpacedHorizontalPosition(random, 12000f, 47000f, 0f);
            center.y = high ? NextFloat(random, 13000f, 36000f) : NextFloat(random, 2800f, 9400f);

            leviathanRegions.Add(new WorldLeviathanRegionRecord
            {
                id = "leviathan_region_" + i.ToString("00"),
                displayNameRu = high ? "Высотный охотничий коридор" : "Пастбище левиафанов",
                centerMeters = center,
                radiusMeters = high ? NextFloat(random, 5000f, 9500f) : NextFloat(random, 6500f, 14000f),
                altitudeMinMeters = high ? 10000f : 2000f,
                altitudeMaxMeters = high ? 40000f : 10000f,
                rarity01 = high ? NextFloat(random, 0.02f, 0.12f) : NextFloat(random, 0.18f, 0.42f),
                chunkId = GetChunkIdAt(center)
            });
        }
    }

    private void GenerateIcebergFields()
    {
        System.Random random = new System.Random(regionSeed + 79);
        for (int i = 0; i < 4; i++)
        {
            Vector3 center = PickSpacedHorizontalPosition(random, 18000f, 48000f, 0f);
            center.y = NextFloat(random, 43000f, 92000f);

            icebergFields.Add(new WorldIcebergFieldRecord
            {
                id = "iceberg_field_" + i.ToString("00"),
                displayNameRu = "Редкое поле айсбергов " + (i + 1),
                centerMeters = center,
                radiusMeters = NextFloat(random, 3500f, 9000f),
                altitudeMeters = center.y,
                icebergCountEstimate = Mathf.RoundToInt(NextFloat(random, 3f, 12f)),
                sublimateKgEstimate = Mathf.RoundToInt(NextFloat(random, 80f, 420f)),
                chunkId = GetChunkIdAt(center)
            });
        }
    }

    private void CreateIsland(string id, string displayNameRu, string roleRu, Vector3 position, float radius, bool permanent)
    {
        WorldIslandRecord island = new WorldIslandRecord
        {
            id = id,
            displayNameRu = displayNameRu,
            roleRu = roleRu,
            positionMeters = position,
            radiusMeters = radius,
            chunkId = GetChunkIdAt(position),
            permanentEntity = permanent,
            materializesAsSceneObject = true
        };

        islands.Add(island);

        WorldChunkRecord chunk = GetChunkAt(position);
        if (chunk != null)
        {
            chunk.hasSettlement = true;
        }
    }

    private Vector3 PickSpacedHorizontalPosition(System.Random random, float minDistanceFromCenter, float maxDistanceFromCenter, float minDistanceFromIslands)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            float angle = NextFloat(random, 0f, Mathf.PI * 2f);
            float distance = NextFloat(random, minDistanceFromCenter, maxDistanceFromCenter);
            Vector3 candidate = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            if (minDistanceFromIslands <= 0f || IsFarEnoughFromIslands(candidate, minDistanceFromIslands))
            {
                return candidate;
            }
        }

        return new Vector3(NextFloat(random, -maxDistanceFromCenter, maxDistanceFromCenter), 0f, NextFloat(random, -maxDistanceFromCenter, maxDistanceFromCenter));
    }

    private bool IsFarEnoughFromIslands(Vector3 candidate, float minDistance)
    {
        float sqr = minDistance * minDistance;
        for (int i = 0; i < islands.Count; i++)
        {
            Vector2 a = new Vector2(candidate.x, candidate.z);
            Vector2 b = new Vector2(islands[i].positionMeters.x, islands[i].positionMeters.z);
            if ((a - b).sqrMagnitude < sqr)
            {
                return false;
            }
        }

        return true;
    }

    private WorldAltitudeBand ChooseDominantBand(float weather, float resources, float danger)
    {
        if (danger > 0.82f) return WorldAltitudeBand.CalmStorm;
        if (resources > 0.80f) return WorldAltitudeBand.ThinAir;
        if (weather > 0.38f) return WorldAltitudeBand.Habitation;
        return WorldAltitudeBand.Habitation;
    }

    private string GetChunkIdAt(Vector3 worldPositionMeters)
    {
        WorldChunkRecord chunk = GetChunkAt(worldPositionMeters);
        return chunk != null ? chunk.id : "out_of_region";
    }

    private string BuildChunkId(int x, int z)
    {
        return "chunk_" + x.ToString("00") + "_" + z.ToString("00");
    }

    private static float NextFloat(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static string FormatKm(float meters)
    {
        return (meters / 1000f).ToString("0.#") + " км";
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawWorldGizmos)
        {
            return;
        }

        Vector3 focusPosition = focus != null ? focus.position : transform.position;
        DrawChunkGizmos(focusPosition);
        DrawBubbleGizmos(focusPosition);

        if (drawDistantRecords)
        {
            DrawRecordGizmos();
        }
    }

    private void DrawChunkGizmos(Vector3 focusPosition)
    {
        float gizmoY = focusPosition.y;
        for (int i = 0; i < chunks.Count; i++)
        {
            WorldChunkRecord chunk = chunks[i];
            Vector3 center = new Vector3(chunk.centerX, gizmoY, chunk.centerZ);
            float distance = Vector2.Distance(new Vector2(focusPosition.x, focusPosition.z), new Vector2(chunk.centerX, chunk.centerZ));
            bool active = distance <= activeBubbleRadiusMeters + chunkSizeMeters * 0.7071f;
            if (!drawAllChunks && !active)
            {
                continue;
            }

            Gizmos.color = active ? new Color(1f, 0.72f, 0.22f, 0.55f) : new Color(0.25f, 0.45f, 0.70f, 0.18f);
            Gizmos.DrawWireCube(center, new Vector3(chunkSizeMeters, 8f, chunkSizeMeters));
        }
    }

    private void DrawBubbleGizmos(Vector3 focusPosition)
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.34f);
        Gizmos.DrawWireSphere(focusPosition, activeBubbleRadiusMeters);
        Gizmos.color = new Color(1f, 1f, 1f, 0.42f);
        Gizmos.DrawWireSphere(focusPosition, detailedBubbleRadiusMeters);
    }

    private void DrawRecordGizmos()
    {
        for (int i = 0; i < islands.Count; i++)
        {
            Gizmos.color = islands[i].permanentEntity ? new Color(1f, 0.78f, 0.28f, 0.9f) : new Color(1f, 0.78f, 0.28f, 0.45f);
            Gizmos.DrawWireSphere(islands[i].positionMeters, Mathf.Max(80f, islands[i].radiusMeters));
        }

        for (int i = 0; i < cloudFields.Count; i++)
        {
            Gizmos.color = new Color(0.92f, 0.95f, 1f, Mathf.Lerp(0.12f, 0.42f, cloudFields[i].density01));
            Gizmos.DrawWireSphere(cloudFields[i].centerMeters, cloudFields[i].radiusMeters);
        }

        for (int i = 0; i < resourceFields.Count; i++)
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.5f);
            Gizmos.DrawWireCube(resourceFields[i].centerMeters, Vector3.one * resourceFields[i].radiusMeters);
        }

        for (int i = 0; i < leviathanRegions.Count; i++)
        {
            Gizmos.color = new Color(0.75f, 0.18f, 1f, 0.42f);
            Gizmos.DrawWireSphere(leviathanRegions[i].centerMeters, leviathanRegions[i].radiusMeters);
        }

        for (int i = 0; i < icebergFields.Count; i++)
        {
            Gizmos.color = new Color(0.68f, 0.92f, 1f, 0.55f);
            Gizmos.DrawWireSphere(icebergFields[i].centerMeters, icebergFields[i].radiusMeters);
        }
    }

    [Serializable]
    public sealed class WorldChunkRecord
    {
        public string id;
        public int chunkX;
        public int chunkZ;
        public float minX;
        public float minZ;
        public float maxX;
        public float maxZ;
        public float centerX;
        public float centerZ;
        public WorldAltitudeBand dominantBand;
        public int weatherSeed;
        [Range(0f, 1f)] public float cloudDensity01;
        [Range(0f, 1f)] public float resourceScore01;
        [Range(0f, 1f)] public float dangerScore01;
        public bool discovered;
        public bool hasSettlement;
        public string notesRu;
    }

    [Serializable]
    public sealed class WorldIslandRecord
    {
        public string id;
        public string displayNameRu;
        public string roleRu;
        public Vector3 positionMeters;
        public float radiusMeters;
        public string chunkId;
        public bool permanentEntity;
        public bool materializesAsSceneObject;
    }

    [Serializable]
    public sealed class WorldCloudFieldRecord
    {
        public string id;
        public string displayNameRu;
        public Vector3 centerMeters;
        public float radiusMeters;
        public float thicknessMeters;
        [Range(0f, 1f)] public float density01;
        public string resourceId;
        public int resourceKgEstimate;
        public string chunkId;
        public bool simulationOnlyWhenFar;
    }

    [Serializable]
    public sealed class WorldResourceFieldRecord
    {
        public string id;
        public string displayNameRu;
        public Vector3 centerMeters;
        public float radiusMeters;
        public WorldAltitudeBand altitudeBand;
        public string resourceId;
        public int resourceKgEstimate;
        public string chunkId;
        public bool materializesAsNodes;
    }

    [Serializable]
    public sealed class WorldLeviathanRegionRecord
    {
        public string id;
        public string displayNameRu;
        public Vector3 centerMeters;
        public float radiusMeters;
        public float altitudeMinMeters;
        public float altitudeMaxMeters;
        [Range(0f, 1f)] public float rarity01;
        public string chunkId;
    }

    [Serializable]
    public sealed class WorldIcebergFieldRecord
    {
        public string id;
        public string displayNameRu;
        public Vector3 centerMeters;
        public float radiusMeters;
        public float altitudeMeters;
        public int icebergCountEstimate;
        public int sublimateKgEstimate;
        public string chunkId;
    }
}
