using System;
using System.Collections.Generic;
using UnityEngine;

public static class SurveySystem
{
    public const string PaperItemId = "paper";
    public const string RockInfoItemId = "rock_info";
    public const string CloudInfoItemId = "cloud_info";
    public const string LeviathanInfoItemId = "leviathan_info";

    public static int ObserveWorldFromPoint(
        WorldConfigDatabase config,
        PlayerProgress progress,
        Vector3 observerPosition,
        float radiusMeters,
        float factsAtHalfRadiusPerSecond,
        float deltaSeconds,
        long utcTicks)
    {
        if (config == null || progress == null || deltaSeconds <= 0f || radiusMeters <= 0f) return 0;

        int changed = 0;
        changed += ObserveGasCloudsFromPoint(config, progress, observerPosition, radiusMeters, factsAtHalfRadiusPerSecond, deltaSeconds, utcTicks);
        changed += ObserveMiningRocksFromPoint(config, progress, observerPosition, radiusMeters, factsAtHalfRadiusPerSecond, deltaSeconds, utcTicks);
        changed += ObserveLeviathansFromPoint(progress, observerPosition, radiusMeters, factsAtHalfRadiusPerSecond, deltaSeconds, utcTicks);
        return changed;
    }

    public static int ObserveAndExtractWorldFromPoint(
        WorldConfigDatabase config,
        PlayerProgress progress,
        Vector3 observerPosition,
        float radiusMeters,
        float factsAtHalfRadiusPerSecond,
        float deltaSeconds,
        long utcTicks,
        List<ResourceStack> cargo,
        float rockInfoEfficiency,
        float cloudInfoEfficiency,
        float leviathanInfoEfficiency)
    {
        if (config == null || progress == null || deltaSeconds <= 0f || radiusMeters <= 0f) return 0;

        int changed = 0;
        changed += ObserveGasCloudsFromPoint(config, progress, observerPosition, radiusMeters, factsAtHalfRadiusPerSecond, deltaSeconds, utcTicks, cargo, cloudInfoEfficiency);
        changed += ObserveMiningRocksFromPoint(config, progress, observerPosition, radiusMeters, factsAtHalfRadiusPerSecond, deltaSeconds, utcTicks, cargo, rockInfoEfficiency);
        changed += ObserveLeviathansFromPoint(progress, observerPosition, radiusMeters, factsAtHalfRadiusPerSecond, deltaSeconds, utcTicks, cargo, leviathanInfoEfficiency);
        return changed;
    }

    public static bool IsFullySurveyedForWork(PlayerProgress progress, ScoutedObjectKind kind, string objectId)
    {
        return progress != null && progress.HasObjectFacts(kind, objectId);
    }

    public static float GetFactsRequired(ScoutedObjectKind kind)
    {
        return kind switch
        {
            ScoutedObjectKind.GasCloud => 45f,
            ScoutedObjectKind.MiningRock => 60f,
            ScoutedObjectKind.Leviathan => 80f,
            _ => 10f
        };
    }

    public static float CalculateGasCloudInformationPotentialKg(float initialVolumeLiters)
    {
        return Mathf.Clamp(Mathf.Max(2f, Mathf.Max(0f, initialVolumeLiters) * 0.025f), 2f, 450f);
    }

    public static float CalculateMiningRockInformationPotentialKg(float rockOreKg)
    {
        return Mathf.Clamp(Mathf.Max(3f, Mathf.Max(0f, rockOreKg) * 0.02f), 3f, 180f);
    }

    public static float CalculateLeviathanInformationPotentialKg(float massKg)
    {
        return Mathf.Clamp(Mathf.Max(4f, Mathf.Max(0f, massKg) * 0.003f), 4f, 240f);
    }

    public static SurveyObjectRuntimeInfo BuildObjectRuntimeInfo(
        WorldConfigDatabase config,
        PlayerProgress progress,
        ScoutedObjectKind kind,
        string objectId,
        string fallbackDisplayName,
        Vector3 fallbackPosition,
        float fallbackInformationPotentialKg)
    {
        ScoutedObjectState state = progress != null
            ? progress.GetScoutedObjectState(kind, objectId, false)
            : null;

        string infoItemId = GetInfoItemId(kind);
        float factsRequired = state != null ? Mathf.Max(1f, state.factsRequired) : GetFactsRequired(kind);
        float factsProgress = state != null ? Mathf.Clamp(state.factsProgress, 0f, factsRequired) : 0f;
        float potentialKg = Mathf.Max(0f, fallbackInformationPotentialKg);
        if (state != null)
        {
            potentialKg = Mathf.Max(potentialKg, state.informationPotentialKg);
        }

        float extractedKg = state != null ? Mathf.Clamp(state.informationExtractedKg, 0f, potentialKg) : 0f;
        bool coordinatesKnown = state != null && state.coordinatesKnown;
        bool factsComplete = state != null && state.factsComplete;
        string displayName = state != null && !string.IsNullOrWhiteSpace(state.displayName)
            ? state.displayName
            : fallbackDisplayName;

        return new SurveyObjectRuntimeInfo
        {
            objectId = objectId ?? "",
            displayName = displayName ?? "",
            kind = kind,
            known = coordinatesKnown,
            coordinatesKnown = coordinatesKnown,
            factsComplete = factsComplete,
            lastKnownPosition = coordinatesKnown && state != null ? state.lastKnownPosition : fallbackPosition,
            factsRequired = factsRequired,
            factsProgress = factsProgress,
            factsRemaining = Mathf.Max(0f, factsRequired - factsProgress),
            factsProgressPercent = factsRequired > 0f ? Mathf.Clamp01(factsProgress / factsRequired) * 100f : 100f,
            informationItemId = infoItemId,
            informationItemNameRu = config != null ? config.GetItemNameRu(infoItemId) : infoItemId,
            informationPotentialKg = potentialKg,
            informationExtractedKg = extractedKg,
            informationRemainingKg = Mathf.Max(0f, potentialKg - extractedKg),
            informationProgressPercent = potentialKg > 0f ? Mathf.Clamp01(extractedKg / potentialKg) * 100f : 100f,
            summaryRu = state != null ? state.summaryRu : ""
        };
    }

    public static int ObserveGasCloudsFromPoint(
        WorldConfigDatabase config,
        PlayerProgress progress,
        Vector3 observerPosition,
        float radiusMeters,
        float factsAtHalfRadiusPerSecond,
        float deltaSeconds,
        long utcTicks,
        List<ResourceStack> cargo = null,
        float infoEfficiency = 0f)
    {
        if (config == null || progress == null || config.gasClouds == null) return 0;

        int changed = 0;
        for (int i = 0; i < config.gasClouds.Count; i++)
        {
            GasCloudConfig cloud = config.gasClouds[i];
            if (cloud == null || string.IsNullOrWhiteSpace(cloud.id)) continue;

            GasCloudTypeConfig type = config.GetGasCloudType(cloud.cloudTypeId);
            if (type == null) continue;

            float remainingLiters = GetCloudRemainingLiters(progress, cloud);
            if (remainingLiters <= 0.001f) continue;

            float cloudRadius = GasCloud.CalculateRadiusMeters(remainingLiters, type.condensateLitersPerCubicMeter);
            float distanceToSurface = Mathf.Max(0f, Vector3.Distance(observerPosition, cloud.position) - cloudRadius);
            if (distanceToSurface > radiusMeters) continue;

            float rate = CalculateGatherRate(radiusMeters, factsAtHalfRadiusPerSecond, distanceToSurface);
            if (rate <= 0f) continue;

            ScoutedObjectState state = progress.GetScoutedObjectState(ScoutedObjectKind.GasCloud, cloud.id, true);
            float potentialInfoKg = CalculateGasCloudInformationPotentialKg(cloud.initialVolumeLiters);
            bool factsChanged = UpdateFacts(
                state,
                cloud.position,
                string.IsNullOrWhiteSpace(cloud.localNameRu) ? cloud.id : cloud.localNameRu,
                GetFactsRequired(ScoutedObjectKind.GasCloud),
                potentialInfoKg,
                rate,
                deltaSeconds,
                utcTicks,
                () => FillGasCloudFacts(state, cloud, type, remainingLiters));

            int extracted = ExtractInformation(state, cargo, ScoutedObjectKind.GasCloud, infoEfficiency, rate, deltaSeconds, utcTicks);
            if (factsChanged || extracted > 0) changed++;
        }

        return changed;
    }

    public static int ObserveMiningRocksFromPoint(
        WorldConfigDatabase config,
        PlayerProgress progress,
        Vector3 observerPosition,
        float radiusMeters,
        float factsAtHalfRadiusPerSecond,
        float deltaSeconds,
        long utcTicks,
        List<ResourceStack> cargo = null,
        float infoEfficiency = 0f)
    {
        if (config == null || progress == null || progress.miningRocks == null) return 0;

        int changed = 0;
        for (int i = 0; i < progress.miningRocks.Count; i++)
        {
            MiningRockState rock = progress.miningRocks[i];
            if (rock == null || string.IsNullOrWhiteSpace(rock.rockId)) continue;
            if (!MiningWorldSimulator.IsRockActive(config, rock, utcTicks)) continue;

            MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
            OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
            if (zone == null || oreType == null) continue;

            Vector3 position = MiningWorldSimulator.CalculateRockPosition(zone, rock, utcTicks);
            float distanceToSurface = Mathf.Max(0f, Vector3.Distance(observerPosition, position) - Mathf.Max(1f, zone.rockRadiusMeters));
            if (distanceToSurface > radiusMeters) continue;

            float rate = CalculateGatherRate(radiusMeters, factsAtHalfRadiusPerSecond, distanceToSurface);
            if (rate <= 0f) continue;

            ScoutedObjectState state = progress.GetScoutedObjectState(ScoutedObjectKind.MiningRock, rock.rockId, true);
            float potentialInfoKg = CalculateMiningRockInformationPotentialKg(zone.rockOreKg);
            bool factsChanged = UpdateFacts(
                state,
                position,
                string.IsNullOrWhiteSpace(oreType.localNameRu) ? rock.rockId : oreType.localNameRu,
                GetFactsRequired(ScoutedObjectKind.MiningRock),
                potentialInfoKg,
                rate,
                deltaSeconds,
                utcTicks,
                () => FillMiningRockFacts(state, config, zone, oreType, rock, position, utcTicks));

            int extracted = ExtractInformation(state, cargo, ScoutedObjectKind.MiningRock, infoEfficiency, rate, deltaSeconds, utcTicks);
            if (factsChanged || extracted > 0) changed++;
        }

        return changed;
    }

    public static int ObserveLeviathansFromPoint(
        PlayerProgress progress,
        Vector3 observerPosition,
        float radiusMeters,
        float factsAtHalfRadiusPerSecond,
        float deltaSeconds,
        long utcTicks,
        List<ResourceStack> cargo = null,
        float infoEfficiency = 0f)
    {
        if (progress == null) return 0;

        List<Leviathan> leviathans = TempLeviathanBuffer;
        Leviathan.GetActiveLeviathans(leviathans, false);

        int changed = 0;
        for (int i = 0; i < leviathans.Count; i++)
        {
            Leviathan leviathan = leviathans[i];
            if (leviathan == null || string.IsNullOrWhiteSpace(leviathan.leviathanId)) continue;

            float distanceToSurface = Mathf.Max(0f, Vector3.Distance(observerPosition, leviathan.transform.position) - Mathf.Max(1f, leviathan.bodyLengthMeters * 0.5f));
            if (distanceToSurface > radiusMeters) continue;

            float rate = CalculateGatherRate(radiusMeters, factsAtHalfRadiusPerSecond, distanceToSurface);
            if (rate <= 0f) continue;

            ScoutedObjectState state = progress.GetScoutedObjectState(ScoutedObjectKind.Leviathan, leviathan.leviathanId, true);
            float potentialInfoKg = CalculateLeviathanInformationPotentialKg(leviathan.massKg);
            bool factsChanged = UpdateFacts(
                state,
                leviathan.transform.position,
                string.IsNullOrWhiteSpace(leviathan.displayName) ? leviathan.leviathanId : leviathan.displayName,
                GetFactsRequired(ScoutedObjectKind.Leviathan),
                potentialInfoKg,
                rate,
                deltaSeconds,
                utcTicks,
                () => FillLeviathanFacts(state, leviathan));

            int extracted = ExtractInformation(state, cargo, ScoutedObjectKind.Leviathan, infoEfficiency, rate, deltaSeconds, utcTicks);
            if (factsChanged || extracted > 0) changed++;
        }

        return changed;
    }

    public static float CalculateGatherRate(float radiusMeters, float factsAtHalfRadiusPerSecond, float distanceToSurface)
    {
        if (radiusMeters <= 0f || factsAtHalfRadiusPerSecond <= 0f) return 0f;

        float t = Mathf.Clamp01(distanceToSurface / radiusMeters);
        float distanceFactor = t <= 0.5f
            ? Mathf.Lerp(1.35f, 1f, t / 0.5f)
            : Mathf.Lerp(1f, 0.02f, Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.5f));
        return Mathf.Max(0f, factsAtHalfRadiusPerSecond * distanceFactor);
    }

    public static string GetInfoItemId(ScoutedObjectKind kind)
    {
        return kind switch
        {
            ScoutedObjectKind.GasCloud => CloudInfoItemId,
            ScoutedObjectKind.MiningRock => RockInfoItemId,
            ScoutedObjectKind.Leviathan => LeviathanInfoItemId,
            _ => RockInfoItemId
        };
    }

    private static bool UpdateFacts(
        ScoutedObjectState state,
        Vector3 position,
        string displayName,
        float factsRequired,
        float informationPotentialKg,
        float rate,
        float deltaSeconds,
        long utcTicks,
        Action fillFacts)
    {
        if (state == null) return false;

        bool changed = !state.coordinatesKnown;
        state.coordinatesKnown = true;
        state.lastKnownPosition = position;
        state.displayName = displayName ?? state.objectId;
        state.factsRequired = Mathf.Max(1f, factsRequired);
        state.informationPotentialKg = Mathf.Max(state.informationPotentialKg, informationPotentialKg);

        float before = state.factsProgress;
        if (!state.factsComplete)
        {
            state.factsProgress = Mathf.Clamp(state.factsProgress + rate * deltaSeconds, 0f, state.factsRequired);
            changed |= !Mathf.Approximately(before, state.factsProgress);
        }

        if (state.factsProgress >= state.factsRequired - 0.001f)
        {
            state.factsComplete = true;
            state.factsProgress = state.factsRequired;
            fillFacts?.Invoke();
        }

        if (state.factsComplete)
        {
            state.factsUpdatedUtcTicks = utcTicks;
            fillFacts?.Invoke();
        }

        return changed;
    }

    private static int ExtractInformation(
        ScoutedObjectState state,
        List<ResourceStack> cargo,
        ScoutedObjectKind kind,
        float efficiency,
        float gatherRate,
        float deltaSeconds,
        long utcTicks)
    {
        if (state == null || !state.factsComplete || cargo == null || efficiency <= 0f) return 0;
        if (GetStackAmount(cargo, PaperItemId) <= 0) return 0;

        float allowedKg = state.informationPotentialKg * Mathf.Clamp01(efficiency);
        float remainingAllowed = allowedKg - state.informationExtractedKg;
        if (remainingAllowed < 1f) return 0;

        state.informationBufferKg += Mathf.Min(remainingAllowed, gatherRate * 0.35f * deltaSeconds);
        int produced = 0;
        string infoItemId = GetInfoItemId(kind);

        while (state.informationBufferKg >= 1f
            && state.informationExtractedKg + 1f <= allowedKg + 0.001f
            && TrySpendStack(cargo, PaperItemId, 1))
        {
            AddStack(cargo, infoItemId, 1);
            state.informationBufferKg -= 1f;
            state.informationExtractedKg += 1f;
            state.informationUpdatedUtcTicks = utcTicks;
            produced++;
        }

        state.informationBufferKg = Mathf.Clamp(state.informationBufferKg, 0f, 0.999f);
        return produced;
    }

    private static void FillGasCloudFacts(ScoutedObjectState state, GasCloudConfig cloud, GasCloudTypeConfig type, float remainingLiters)
    {
        state.typeId = cloud.cloudTypeId;
        state.zoneId = "";
        state.resourceId = type.condensateItemId;
        state.summaryRu = "Облако: " + (string.IsNullOrWhiteSpace(type.localNameRu) ? type.id : type.localNameRu)
            + ". Координаты " + FormatVector(cloud.position)
            + ". Конденсат " + type.condensateItemId
            + ", плотность " + type.condensateLitersPerCubicMeter.ToString("F3") + " л/м3"
            + ", осталось " + remainingLiters.ToString("F0") + " кг сырого концентрата. Состав: " + FormatCloudComposition(type) + ".";
    }

    private static void FillMiningRockFacts(
        ScoutedObjectState state,
        WorldConfigDatabase config,
        MiningZoneConfig zone,
        OreTypeConfig oreType,
        MiningRockState rock,
        Vector3 position,
        long utcTicks)
    {
        state.typeId = rock.oreTypeId;
        state.zoneId = rock.zoneId;
        state.resourceId = oreType.oreItemId;
        float secondsLeft = GetRockSecondsLeft(zone, rock, utcTicks);
        state.summaryRu = "Глыба: " + (string.IsNullOrWhiteSpace(oreType.localNameRu) ? oreType.id : oreType.localNameRu)
            + ". Координаты " + FormatVector(position)
            + ". Руда " + oreType.oreItemId
            + ", осталось " + rock.remainingOreKg.ToString("F0") + " кг"
            + ", естественное осыпание " + oreType.naturalShedKgPerMinute.ToString("F1") + " кг/мин"
            + ", до разрушения около " + FormatSeconds(secondsLeft)
            + ". Состав: " + FormatOreComposition(oreType) + ".";
    }

    private static void FillLeviathanFacts(ScoutedObjectState state, Leviathan leviathan)
    {
        state.typeId = leviathan.typeId;
        state.zoneId = leviathan.zoneId;
        state.resourceId = leviathan.carcassItemId;
        float health01 = leviathan.maxHealth > 0f ? Mathf.Clamp01(leviathan.health / leviathan.maxHealth) : 0f;
        float hungerKg = Mathf.Max(0f, leviathan.maxSatietyKg - leviathan.satietyKg);
        state.summaryRu = "Левиафан: " + leviathan.displayName
            + ". Координаты " + FormatVector(leviathan.transform.position)
            + ". Тип " + leviathan.typeId
            + ", зона " + leviathan.zoneId
            + ", масса " + leviathan.massKg.ToString("F0") + " кг"
            + ", здоровье " + (health01 * 100f).ToString("F0") + "%"
            + ", голод " + hungerKg.ToString("F0") + "/" + leviathan.maxSatietyKg.ToString("F0") + " кг.";
    }

    private static float GetCloudRemainingLiters(PlayerProgress progress, GasCloudConfig cloud)
    {
        GasCloudState state = progress.GetGasCloudState(cloud.id, false);
        if (state == null || !state.initialized) return Mathf.Max(0f, cloud.initialVolumeLiters);
        return Mathf.Max(0f, state.remainingVolumeLiters);
    }

    private static float GetRockSecondsLeft(MiningZoneConfig zone, MiningRockState rock, long utcTicks)
    {
        if (zone == null || rock == null || rock.spawnedUtcTicks <= 0) return 0f;

        float ageSeconds = Mathf.Max(0f, (float)new TimeSpan(utcTicks - rock.spawnedUtcTicks).TotalSeconds);
        return Mathf.Max(0f, zone.LifetimeSeconds - ageSeconds);
    }

    private static string FormatCloudComposition(GasCloudTypeConfig type)
    {
        if (type == null || type.composition == null || type.composition.Count == 0) return "неизвестно";

        string text = "";
        for (int i = 0; i < type.composition.Count; i++)
        {
            GasCloudCompositionConfig entry = type.composition[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId)) continue;
            if (text.Length > 0) text += ", ";
            text += entry.itemId + " " + (entry.share * 100f).ToString("F0") + "%";
        }

        return text.Length > 0 ? text : "неизвестно";
    }

    private static string FormatOreComposition(OreTypeConfig oreType)
    {
        if (oreType == null || oreType.composition == null || oreType.composition.Count == 0) return "неизвестно";

        string text = "";
        for (int i = 0; i < oreType.composition.Count; i++)
        {
            OreMineralCompositionConfig entry = oreType.composition[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.mineralItemId)) continue;
            if (text.Length > 0) text += ", ";
            text += entry.mineralItemId + " " + (entry.share * 100f).ToString("F0") + "%";
        }

        return text.Length > 0 ? text : "неизвестно";
    }

    private static string FormatVector(Vector3 value)
    {
        return value.x.ToString("F0") + ", " + value.y.ToString("F0") + ", " + value.z.ToString("F0");
    }

    private static string FormatSeconds(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        if (seconds >= 3600f) return (seconds / 3600f).ToString("F1") + " ч";
        if (seconds >= 60f) return (seconds / 60f).ToString("F1") + " мин";
        return seconds.ToString("F0") + " сек";
    }

    private static int GetStackAmount(List<ResourceStack> cargo, string itemId)
    {
        if (cargo == null || string.IsNullOrWhiteSpace(itemId)) return 0;
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack != null && stack.resourceId == itemId) return Mathf.Max(0, stack.amount);
        }

        return 0;
    }

    private static bool TrySpendStack(List<ResourceStack> cargo, string itemId, int amount)
    {
        if (amount <= 0) return true;
        if (cargo == null || string.IsNullOrWhiteSpace(itemId)) return false;

        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || stack.resourceId != itemId) continue;
            if (stack.amount < amount) return false;

            stack.amount -= amount;
            if (stack.amount <= 0)
            {
                cargo.RemoveAt(i);
            }

            return true;
        }

        return false;
    }

    private static void AddStack(List<ResourceStack> cargo, string itemId, int amount)
    {
        if (cargo == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;

        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || stack.resourceId != itemId) continue;
            stack.amount += amount;
            return;
        }

        cargo.Add(new ResourceStack { resourceId = itemId, amount = amount });
    }

    private static readonly List<Leviathan> TempLeviathanBuffer = new List<Leviathan>();
}

public struct SurveyObjectRuntimeInfo
{
    public ScoutedObjectKind kind;
    public string objectId;
    public string displayName;
    public bool known;
    public bool coordinatesKnown;
    public bool factsComplete;
    public Vector3 lastKnownPosition;
    public float factsRequired;
    public float factsProgress;
    public float factsRemaining;
    public float factsProgressPercent;
    public string informationItemId;
    public string informationItemNameRu;
    public float informationPotentialKg;
    public float informationExtractedKg;
    public float informationRemainingKg;
    public float informationProgressPercent;
    public string summaryRu;
}

[Serializable]
public class SurveyObjectInspectorState
{
    [InspectorName("Известен игроку")]
    [Tooltip("Становится да, как только любой наблюдатель хотя бы увидел объект и запомнил его координаты.")]
    public bool known;

    [InspectorName("Координаты известны")]
    [Tooltip("Координаты открываются сразу при первом попадании объекта в радиус наблюдения.")]
    public bool coordinatesKnown;

    [InspectorName("Сведения собраны полностью")]
    [Tooltip("Полные сведения нужны промысловым автопилотам, чтобы они могли выбирать этот объект как цель.")]
    public bool factsComplete;

    [InspectorName("Сведения собрано")]
    [Tooltip("Текущий прогресс сведений: сколько набрано из требуемого объема и процент.")]
    public string factsProgressRu = "0/0 (0%)";

    [InspectorName("Сведения собрано, %")]
    [Tooltip("Процент заполнения сведений об объекте.")]
    public float factsProgressPercent;

    [InspectorName("Сведений осталось")]
    [Tooltip("Сколько единиц наблюдения еще нужно, чтобы открыть характеристики объекта.")]
    public float factsRemaining;

    [InspectorName("Какой ресурс информации")]
    [Tooltip("Какой предмет научной информации получается, когда наблюдательное оборудование перерабатывает бумагу около этого объекта.")]
    public string informationItemRu = "";

    [InspectorName("Информации всего, кг")]
    [Tooltip("Полный потенциальный объем научной информации, спрятанный в объекте.")]
    public float informationPotentialKg;

    [InspectorName("Информации собрано, кг")]
    [Tooltip("Сколько кг научной информации уже снято с объекта и превращено в груз корабля.")]
    public float informationExtractedKg;

    [InspectorName("Информации собрано, %")]
    [Tooltip("Процент снятой научной информации от полного потенциала объекта.")]
    public float informationProgressPercent;

    [InspectorName("Информации осталось, кг")]
    [Tooltip("Сколько кг научной информации еще можно снять с объекта подходящим наблюдательным оборудованием.")]
    public float informationRemainingKg;

    [InspectorName("Последние известные координаты")]
    [Tooltip("Если объект известен игроку, это координаты из разведданных. Если нет - текущая позиция объекта для отладки.")]
    public Vector3 lastKnownPosition;

    [TextArea(2, 5)]
    [InspectorName("Открытая сводка")]
    [Tooltip("Читаемое описание появляется после полного сбора сведений и затем обновляется повторным наблюдением.")]
    public string summaryRu = "";

    public void Apply(SurveyObjectRuntimeInfo info)
    {
        known = info.known;
        coordinatesKnown = info.coordinatesKnown;
        factsComplete = info.factsComplete;
        factsProgressPercent = info.factsProgressPercent;
        factsRemaining = info.factsRemaining;
        factsProgressRu = $"{info.factsProgress:F1}/{info.factsRequired:F1} ({info.factsProgressPercent:F0}%)";
        informationItemRu = string.IsNullOrWhiteSpace(info.informationItemNameRu)
            ? info.informationItemId
            : $"{info.informationItemNameRu} ({info.informationItemId})";
        informationPotentialKg = info.informationPotentialKg;
        informationExtractedKg = info.informationExtractedKg;
        informationProgressPercent = info.informationProgressPercent;
        informationRemainingKg = info.informationRemainingKg;
        lastKnownPosition = info.lastKnownPosition;
        summaryRu = string.IsNullOrWhiteSpace(info.summaryRu)
            ? (info.known ? "Координаты известны, характеристики еще собираются." : "Объект еще не известен игроку.")
            : info.summaryRu;
    }
}
