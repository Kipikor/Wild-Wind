using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

public sealed class WorldAutoTestRunner : MonoBehaviour
{
    private const string LogPrefix = "[WorldAutoTest] ";

    [Header("World Auto Test")]
    [InspectorName("World Runtime")]
    public WorldRegionRuntime world;
    [InspectorName("Bubble Streamer")]
    public WorldBubbleStreamer streamer;
    [InspectorName("Focus")]
    public Transform focus;
    [InspectorName("Run On Start")]
    public bool runOnStart = true;
    [InspectorName("Log Successful Checks")]
    public bool logSuccessfulChecks;
    [InspectorName("Quiet Mode")]
    public bool quietMode = true;
    [InspectorName("Refresh Budget, ms")]
    public float refreshBudgetMs = 250f;

    private IEnumerator Start()
    {
        if (!runOnStart)
        {
            yield break;
        }

        yield return null;
        RunSelfTest();
    }

    [ContextMenu("Run World Auto Test")]
    public void RunSelfTest()
    {
        WorldAutoTestReport report = new WorldAutoTestReport(this, logSuccessfulChecks && !quietMode);

        try
        {
            ResolveReferences(report);
            if (world == null)
            {
                report.Fail("WorldRegionRuntime не найден.");
                report.Finish();
                return;
            }

            if (world.Chunks.Count == 0)
            {
                world.GenerateStarterRegion();
            }

            ValidateRegionGrid(report);
            ValidateAltitudeBands(report);
            ValidateWorldRecords(report);
            ValidateBubbleQueries(report);
            ValidateStreamer(report);
            ValidateRefreshBudget(report);
        }
        catch (Exception exception)
        {
            report.Fail("Исключение во время автопроверки мира: " + exception.GetType().Name + " - " + exception.Message);
            UnityEngine.Debug.LogException(exception, this);
        }

        report.Finish();
    }

    private void ResolveReferences(WorldAutoTestReport report)
    {
        if (world == null)
        {
            world = FindFirstObjectByType<WorldRegionRuntime>();
        }

        if (streamer == null)
        {
            streamer = FindFirstObjectByType<WorldBubbleStreamer>();
        }

        if (focus == null && world != null)
        {
            focus = world.Focus;
        }

        report.Check(world != null, world != null ? "WorldRegionRuntime найден." : "WorldRegionRuntime не найден.");
        report.Check(focus != null, focus != null ? "Фокус игрока найден." : "Фокус игрока не найден.");
        report.Check(streamer != null, streamer != null ? "WorldBubbleStreamer найден." : "WorldBubbleStreamer не найден.");
    }

    private void ValidateRegionGrid(WorldAutoTestReport report)
    {
        int expectedChunkCount = WorldRegionRuntime.DefaultChunkCountPerAxis * WorldRegionRuntime.DefaultChunkCountPerAxis;
        report.Check(Mathf.Approximately(world.WorldSizeMeters, WorldRegionRuntime.DefaultWorldSizeMeters),
            "Размер региона 100 км: " + FormatKm(world.WorldSizeMeters) + ".");
        report.Check(Mathf.Approximately(world.ChunkSizeMeters, WorldRegionRuntime.DefaultChunkSizeMeters),
            "Размер чанка 10 км: " + FormatKm(world.ChunkSizeMeters) + ".");
        report.Check(world.Chunks.Count == expectedChunkCount,
            "Сетка содержит 100 чанков: " + world.Chunks.Count + ".");

        HashSet<string> ids = new HashSet<string>();
        float half = world.WorldSizeMeters * 0.5f;
        bool allIdsUnique = true;
        bool allBoundsValid = true;
        bool allCentersResolvable = true;

        for (int i = 0; i < world.Chunks.Count; i++)
        {
            WorldRegionRuntime.WorldChunkRecord chunk = world.Chunks[i];
            allIdsUnique &= ids.Add(chunk.id);
            allBoundsValid &= chunk.minX >= -half &&
                chunk.maxX <= half &&
                chunk.minZ >= -half &&
                chunk.maxZ <= half &&
                Mathf.Approximately(chunk.maxX - chunk.minX, world.ChunkSizeMeters) &&
                Mathf.Approximately(chunk.maxZ - chunk.minZ, world.ChunkSizeMeters);

            WorldRegionRuntime.WorldChunkRecord resolved = world.GetChunkAt(new Vector3(chunk.centerX, 2500f, chunk.centerZ));
            allCentersResolvable &= resolved != null && resolved.id == chunk.id;
        }

        report.Check(allIdsUnique, "У всех чанков уникальные id.");
        report.Check(allBoundsValid, "Границы чанков лежат внутри региона и имеют правильный размер.");
        report.Check(allCentersResolvable, "Центр каждого чанка адресуется обратно в тот же чанк.");

        report.Check(world.GetChunkAt(new Vector3(-half + 1f, 2500f, -half + 1f)) != null, "Юго-западный край региона адресуется.");
        report.Check(world.GetChunkAt(new Vector3(half - 1f, 2500f, half - 1f)) != null, "Северо-восточный край региона адресуется.");
        report.Check(world.GetChunkAt(new Vector3(half + 1f, 2500f, 0f)) == null, "Точка за границей региона не адресуется как чанк.");
    }

    private void ValidateAltitudeBands(WorldAutoTestReport report)
    {
        report.Check(world.EvaluateAltitudeBand(0f) == WorldAltitudeBand.DeadlyStorm, "Высота 0 м = смертельная буря.");
        report.Check(world.EvaluateAltitudeBand(999f) == WorldAltitudeBand.ViolentStorm, "Высота 999 м = яростная буря.");
        report.Check(world.EvaluateAltitudeBand(1000f) == WorldAltitudeBand.CalmStorm, "Высота 1000 м = спокойная буря.");
        report.Check(world.EvaluateAltitudeBand(2000f) == WorldAltitudeBand.Habitation, "Высота 2000 м = зона обитания.");
        report.Check(world.EvaluateAltitudeBand(10000f) == WorldAltitudeBand.ThinAir, "Высота 10000 м = разреженная зона.");
        report.Check(world.EvaluateAltitudeBand(40000f) == WorldAltitudeBand.Ice, "Высота 40000 м = ледяная зона.");
        report.Check(world.EvaluateAltitudeBand(100000f) == WorldAltitudeBand.BeyondClaudiumLift, "Высота 100000 м = выше подъёмной силы клавдия.");
    }

    private void ValidateWorldRecords(WorldAutoTestReport report)
    {
        report.Check(world.Islands.Count == 6, "Учебный регион содержит столицу и пять островов: " + world.Islands.Count + ".");
        report.Check(world.CloudFields.Count == 3, "Учебный регион содержит только стартовые облака: " + world.CloudFields.Count + ".");
        report.Check(world.ResourceFields.Count == 1, "Учебный регион содержит одну учебную глыбу: " + world.ResourceFields.Count + ".");
        report.Check(world.LeviathanRegions.Count == 1, "Учебный регион содержит одну зону малых левиафанов: " + world.LeviathanRegions.Count + ".");
        report.Check(world.IcebergFields.Count == 0, "Учебный регион пока не содержит айсбергов: " + world.IcebergFields.Count + ".");

        ValidateIslandRecords(report);
        ValidateCloudRecords(report);
        ValidateResourceRecords(report);
        ValidateLeviathanRecords(report);
        ValidateIcebergRecords(report);
    }

    private void ValidateIslandRecords(WorldAutoTestReport report)
    {
        bool allValid = true;
        for (int i = 0; i < world.Islands.Count; i++)
        {
            WorldRegionRuntime.WorldIslandRecord island = world.Islands[i];
            allValid &= !string.IsNullOrWhiteSpace(island.id) &&
                island.radiusMeters > 0f &&
                world.EvaluateAltitudeBand(island.positionMeters.y) == WorldAltitudeBand.Habitation &&
                ChunkMatches(island.chunkId, island.positionMeters);
        }

        report.Check(allValid, "Все острова имеют id, радиус, зону обитания и корректный chunkId.");
    }

    private void ValidateCloudRecords(WorldAutoTestReport report)
    {
        bool allValid = true;
        for (int i = 0; i < world.CloudFields.Count; i++)
        {
            WorldRegionRuntime.WorldCloudFieldRecord cloud = world.CloudFields[i];
            WorldAltitudeBand band = world.EvaluateAltitudeBand(cloud.centerMeters.y);
            allValid &= !string.IsNullOrWhiteSpace(cloud.id) &&
                !string.IsNullOrWhiteSpace(cloud.resourceId) &&
                cloud.radiusMeters > 0f &&
                cloud.thicknessMeters > 0f &&
                cloud.density01 >= 0f &&
                cloud.density01 <= 1f &&
                cloud.resourceKgEstimate > 0 &&
                ChunkMatches(cloud.chunkId, cloud.centerMeters) &&
                (band == WorldAltitudeBand.Habitation || band == WorldAltitudeBand.ThinAir);
        }

        report.Check(allValid, "Все облачные поля валидны и лежат в зоне обитания/разреженной зоне.");
    }

    private void ValidateResourceRecords(WorldAutoTestReport report)
    {
        bool allValid = true;
        for (int i = 0; i < world.ResourceFields.Count; i++)
        {
            WorldRegionRuntime.WorldResourceFieldRecord resource = world.ResourceFields[i];
            allValid &= !string.IsNullOrWhiteSpace(resource.id) &&
                !string.IsNullOrWhiteSpace(resource.resourceId) &&
                resource.radiusMeters > 0f &&
                resource.resourceKgEstimate > 0 &&
                resource.altitudeBand == world.EvaluateAltitudeBand(resource.centerMeters.y) &&
                ChunkMatches(resource.chunkId, resource.centerMeters);
        }

        report.Check(allValid, "Все ресурсные поля имеют ресурс, запас, высотный слой и корректный chunkId.");
    }

    private void ValidateLeviathanRecords(WorldAutoTestReport report)
    {
        bool allValid = true;
        for (int i = 0; i < world.LeviathanRegions.Count; i++)
        {
            WorldRegionRuntime.WorldLeviathanRegionRecord region = world.LeviathanRegions[i];
            allValid &= !string.IsNullOrWhiteSpace(region.id) &&
                region.radiusMeters > 0f &&
                region.altitudeMaxMeters > region.altitudeMinMeters &&
                region.rarity01 >= 0f &&
                region.rarity01 <= 1f &&
                ChunkMatches(region.chunkId, region.centerMeters);
        }

        report.Check(allValid, "Все зоны левиафанов имеют радиус, высотный диапазон, редкость и корректный chunkId.");
    }

    private void ValidateIcebergRecords(WorldAutoTestReport report)
    {
        bool allValid = true;
        for (int i = 0; i < world.IcebergFields.Count; i++)
        {
            WorldRegionRuntime.WorldIcebergFieldRecord field = world.IcebergFields[i];
            allValid &= !string.IsNullOrWhiteSpace(field.id) &&
                field.radiusMeters > 0f &&
                field.icebergCountEstimate > 0 &&
                field.sublimateKgEstimate > 0 &&
                world.EvaluateAltitudeBand(field.altitudeMeters) == WorldAltitudeBand.Ice &&
                ChunkMatches(field.chunkId, field.centerMeters);
        }

        report.Check(allValid, "Все поля айсбергов лежат в ледяной зоне и имеют добываемый запас.");
    }

    private void ValidateBubbleQueries(WorldAutoTestReport report)
    {
        Vector3 center = new Vector3(0f, 2500f, 0f);
        Vector3 nearEdge = new Vector3(49000f, 2500f, 49000f);
        Vector3 outside = new Vector3(51000f, 2500f, 0f);

        report.Check(world.GetChunkAt(center) != null, "Фокус на центральной границе чанков адресуется.");
        report.Check(world.CountActiveChunks(center, world.ActiveBubbleRadiusMeters) > 1, "Пузырь на границе захватывает несколько чанков.");
        report.Check(world.GetChunkAt(nearEdge) != null, "Фокус рядом с краем мира адресуется.");
        report.Check(world.CountActiveChunks(nearEdge, world.ActiveBubbleRadiusMeters) > 0, "Пузырь рядом с краем мира имеет активные чанки.");
        report.Check(world.GetChunkAt(outside) == null, "Фокус за пределами мира не получает чанк.");
    }

    private void ValidateStreamer(WorldAutoTestReport report)
    {
        if (streamer == null || focus == null)
        {
            report.Fail("Стример или фокус не найдены, проверка материализации невозможна.");
            return;
        }

        Vector3 original = focus.position;
        try
        {
            focus.position = new Vector3(0f, 2500f, 0f);
            streamer.RefreshNow();
            int centerCount = streamer.ActiveProxyCount;
            report.Check(centerCount > 0, "Стример материализует объекты в центре региона: " + centerCount + ".");
            report.Check(streamer.MaterializedRoot != null, "У стримера есть корень материализации.");
            report.Check(streamer.MaterializedRoot != null && streamer.MaterializedRoot.childCount == streamer.ActiveProxyCount,
                "Количество дочерних объектов пузыря совпадает с активными proxy.");

            focus.position = new Vector3(42000f, 2500f, 42000f);
            streamer.RefreshNow();
            int edgeCount = streamer.ActiveProxyCount;
            report.Check(edgeCount >= 0, "Стример обновляется у края региона без исключений. Proxy: " + edgeCount + ".");
            report.Check(edgeCount <= 45, "Стример не материализует слишком много proxy за один пузырь: " + edgeCount + ".");
        }
        finally
        {
            focus.position = original;
            streamer.RefreshNow();
        }
    }

    private void ValidateRefreshBudget(WorldAutoTestReport report)
    {
        if (streamer == null || focus == null)
        {
            return;
        }

        Vector3 original = focus.position;
        Vector3[] samples =
        {
            new Vector3(0f, 2500f, 0f),
            new Vector3(9000f, 2500f, 0f),
            new Vector3(0f, 2500f, 9000f),
            new Vector3(-18000f, 8000f, 12000f),
            new Vector3(32000f, 12000f, -18000f)
        };

        Stopwatch stopwatch = new Stopwatch();
        long totalMs = 0;

        try
        {
            for (int i = 0; i < samples.Length; i++)
            {
                focus.position = samples[i];
                stopwatch.Restart();
                streamer.RefreshNow();
                stopwatch.Stop();
                totalMs += stopwatch.ElapsedMilliseconds;
            }
        }
        finally
        {
            focus.position = original;
            streamer.RefreshNow();
        }

        float averageMs = totalMs / Mathf.Max(1f, samples.Length);
        report.Check(averageMs <= refreshBudgetMs,
            "Среднее обновление пузыря в бюджете: " + averageMs.ToString("0.0") + " мс / " + refreshBudgetMs.ToString("0.0") + " мс.");
    }

    private bool ChunkMatches(string chunkId, Vector3 position)
    {
        WorldRegionRuntime.WorldChunkRecord chunk = world.GetChunkAt(position);
        return chunk != null && chunk.id == chunkId;
    }

    private static string FormatKm(float meters)
    {
        return (meters / 1000f).ToString("0.#") + " км";
    }

    private sealed class WorldAutoTestReport
    {
        private readonly UnityEngine.Object context;
        private readonly bool logPasses;
        private readonly List<string> failures = new List<string>();
        private int checks;

        public WorldAutoTestReport(UnityEngine.Object context, bool logPasses)
        {
            this.context = context;
            this.logPasses = logPasses;
        }

        public void Check(bool condition, string message)
        {
            checks++;
            if (condition)
            {
                Pass(message);
            }
            else
            {
                Fail(message);
            }
        }

        public void Pass(string message)
        {
            if (logPasses)
            {
                UnityEngine.Debug.Log(LogPrefix + "OK: " + message, context);
            }
        }

        public void Fail(string message)
        {
            failures.Add(message);
            UnityEngine.Debug.LogError(LogPrefix + "FAIL: " + message, context);
        }

        public void Finish()
        {
            if (failures.Count == 0)
            {
                UnityEngine.Debug.Log(LogPrefix + "OK: автопроверка мира пройдена. Проверок: " + checks + ".", context);
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(LogPrefix + "НЕ ОК: автопроверка мира нашла проблем: " + failures.Count + " из " + checks + ".");
            for (int i = 0; i < failures.Count; i++)
            {
                builder.AppendLine("- " + failures[i]);
            }

            UnityEngine.Debug.LogError(builder.ToString(), context);
        }
    }
}
