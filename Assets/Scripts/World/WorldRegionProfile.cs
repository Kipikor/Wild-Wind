using UnityEngine;

[CreateAssetMenu(fileName = "WorldRegionProfile", menuName = "Wild Wind/World/Region Profile")]
public sealed class WorldRegionProfile : ScriptableObject
{
    [Header("Размер и сид")]
    [SerializeField, InspectorName("Сид региона")] private int seed = 170517;
    [SerializeField, InspectorName("Ширина региона, м")] private float worldSizeMeters = WorldRegionRuntime.DefaultWorldSizeMeters;
    [SerializeField, InspectorName("Размер чанка, м")] private float chunkSizeMeters = WorldRegionRuntime.DefaultChunkSizeMeters;

    [Header("Пузырь игрока")]
    [SerializeField, InspectorName("Активный радиус, м")] private float activeBubbleRadiusMeters = 5000f;
    [SerializeField, InspectorName("Детальный радиус, м")] private float detailedBubbleRadiusMeters = 1600f;

    [Header("Плотность записей мира")]
    [SerializeField, InspectorName("Острова, шт")] private int islandCount = 15;
    [SerializeField, InspectorName("Облачные поля, шт")] private int cloudFieldCount = 34;
    [SerializeField, InspectorName("Высотные облака, шт")] private int highCloudFieldCount = 8;
    [SerializeField, InspectorName("Рудные поля, шт")] private int resourceFieldCount = 11;
    [SerializeField, InspectorName("Высотные рудные поля, шт")] private int highResourceFieldCount = 3;
    [SerializeField, InspectorName("Зоны левиафанов, шт")] private int leviathanRegionCount = 5;
    [SerializeField, InspectorName("Высотные зоны левиафанов, шт")] private int highLeviathanRegionCount = 2;
    [SerializeField, InspectorName("Поля айсбергов, шт")] private int icebergFieldCount = 4;

    public int Seed => seed;
    public float WorldSizeMeters => worldSizeMeters;
    public float ChunkSizeMeters => chunkSizeMeters;
    public float ActiveBubbleRadiusMeters => activeBubbleRadiusMeters;
    public float DetailedBubbleRadiusMeters => detailedBubbleRadiusMeters;
    public int IslandCount => islandCount;
    public int CloudFieldCount => cloudFieldCount;
    public int HighCloudFieldCount => highCloudFieldCount;
    public int ResourceFieldCount => resourceFieldCount;
    public int HighResourceFieldCount => highResourceFieldCount;
    public int LeviathanRegionCount => leviathanRegionCount;
    public int HighLeviathanRegionCount => highLeviathanRegionCount;
    public int IcebergFieldCount => icebergFieldCount;
    public int ChunkCountPerAxis => Mathf.Max(1, Mathf.RoundToInt(worldSizeMeters / Mathf.Max(1f, chunkSizeMeters)));

    private void OnValidate()
    {
        worldSizeMeters = Mathf.Max(WorldRegionRuntime.DefaultChunkSizeMeters, worldSizeMeters);
        chunkSizeMeters = Mathf.Clamp(chunkSizeMeters, 1000f, worldSizeMeters);
        activeBubbleRadiusMeters = Mathf.Clamp(activeBubbleRadiusMeters, 1000f, 15000f);
        detailedBubbleRadiusMeters = Mathf.Clamp(detailedBubbleRadiusMeters, 250f, activeBubbleRadiusMeters);

        islandCount = Mathf.Clamp(islandCount, 1, 200);
        cloudFieldCount = Mathf.Clamp(cloudFieldCount, 0, 1000);
        highCloudFieldCount = Mathf.Clamp(highCloudFieldCount, 0, cloudFieldCount);
        resourceFieldCount = Mathf.Clamp(resourceFieldCount, 0, 500);
        highResourceFieldCount = Mathf.Clamp(highResourceFieldCount, 0, resourceFieldCount);
        leviathanRegionCount = Mathf.Clamp(leviathanRegionCount, 0, 200);
        highLeviathanRegionCount = Mathf.Clamp(highLeviathanRegionCount, 0, leviathanRegionCount);
        icebergFieldCount = Mathf.Clamp(icebergFieldCount, 0, 200);
    }
}
