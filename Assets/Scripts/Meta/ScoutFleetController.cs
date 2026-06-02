using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ScoutFleetController : MonoBehaviour
{
    [Header("Связи")]
    [InspectorName("Мета-игра")]
    [Tooltip("MetaGameState, из которого разведчики берут конфиги мира и сохраняемый прогресс.")]
    public MetaGameState metaGameState;

    [Header("Разведчики")]
    [InspectorName("Корабли разведки")]
    [Tooltip("Автономные разведчики. Они ищут неизвестные облака, глыбы и левиафанов, собирают сведения и превращают бумагу в научную информацию.")]
    public List<ScoutShipDefinition> ships = new List<ScoutShipDefinition>();

    [Header("Симуляция")]
    [InspectorName("Шаг симуляции, сек")]
    [Tooltip("Чем меньше шаг, тем точнее движение разведчиков в перемотке. 5 секунд обычно достаточно.")]
    public float simulationStepSeconds = 5f;
    [InspectorName("Пассивный радиус обычных кораблей, м")]
    [Tooltip("Логистические, газовые и майнинговые корабли тоже замечают объекты рядом с собой, но без научной информации.")]
    public float passiveFleetObservationRadiusMeters = 100f;
    [InspectorName("Скорость пассивных сведений")]
    [Tooltip("Скорость сбора сведений обычным кораблем на половине радиуса наблюдения.")]
    public float passiveFleetFactsAtHalfRadiusPerSecond = 0.7f;
    [InspectorName("Логировать события")]
    [Tooltip("Пишет в консоль ключевые события разведчиков: выбор цели, отход от левиафана, выгрузку информации.")]
    public bool debugLogging = true;

    private readonly List<Leviathan> leviathanBuffer = new List<Leviathan>();

    public void CreateExampleSetupIfEmpty()
    {
        ships ??= new List<ScoutShipDefinition>();
        if (ships.Count > 0) return;

        ships.Add(new ScoutShipDefinition
        {
            shipId = "scout_01",
            displayName = "Разведчик 01",
            homeIslandId = "capital",
            cruiseSpeedMS = 22f,
            searchRadiusMeters = 2600f,
            observationRadiusMeters = 650f,
            factsAtHalfRadiusPerSecond = 2.2f,
            rockInfoEfficiency = 0.85f,
            cloudInfoEfficiency = 0.75f,
            leviathanInfoEfficiency = 0.55f,
            paperToInfoEfficiency = 1f,
            paperCargoTargetKg = 60,
            maxInfoCargoKg = 90,
            safeLeviathanDistanceMeters = 260f
        });
    }

    public void EnsureRuntimeShips(PlayerProgress progress)
    {
        if (progress == null) return;
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return;

        ships ??= new List<ScoutShipDefinition>();
        if (ships.Count == 0)
        {
            CreateExampleSetupIfEmpty();
        }

        for (int i = 0; i < ships.Count; i++)
        {
            ScoutShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            ScoutShipState state = progress.GetScoutShipState(definition.shipId, true);
            InitializeStateFromDefinition(state, definition);
        }
    }

    public int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || !config.isLoaded || progress == null || toUtcTicks <= fromUtcTicks) return 0;
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return 0;

        EnsureRuntimeShips(progress);
        int changed = AdvancePassiveFleetObservation(config, progress, fromUtcTicks, toUtcTicks);

        float stepSeconds = Mathf.Clamp(simulationStepSeconds, 1f, 60f);
        long stepTicks = TimeSpan.FromSeconds(stepSeconds).Ticks;
        long cursor = fromUtcTicks;
        int guard = 0;
        int maxSteps = Mathf.Max(1, Mathf.CeilToInt((float)((double)(toUtcTicks - fromUtcTicks) / Math.Max(1L, stepTicks))) + 4);

        while (cursor < toUtcTicks && guard++ < maxSteps)
        {
            long next = Math.Min(toUtcTicks, cursor + stepTicks);
            float deltaSeconds = Mathf.Max(0.01f, (float)new TimeSpan(next - cursor).TotalSeconds);

            for (int i = 0; i < ships.Count; i++)
            {
                ScoutShipDefinition definition = ships[i];
                if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

                ScoutShipState state = progress.GetScoutShipState(definition.shipId, true);
                changed += AdvanceShip(definition, state, config, progress, deltaSeconds, next);
            }

            cursor = next;
        }

        return changed;
    }

    private int AdvanceShip(
        ScoutShipDefinition definition,
        ScoutShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        float deltaSeconds,
        long utcTicks)
    {
        if (definition == null || state == null) return 0;

        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null)
        {
            state.status = ScoutShipStatus.Error;
            state.lastError = "Нет домашнего острова для разведчика.";
            return 0;
        }

        if (state.lastKnownPosition == Vector3.zero)
        {
            state.lastKnownPosition = home.position;
        }

        int changed = 0;
        if (TryFleeFromLeviathan(definition, state, deltaSeconds))
        {
            return 1;
        }

        if (ShouldReturnHome(definition, state))
        {
            changed += MoveHomeOrUnload(definition, state, home, progress, deltaSeconds);
            return changed;
        }

        SurveyTarget target = ResolveTarget(state, config, progress, utcTicks);
        if (!target.valid || TargetIsDoneForScout(definition, progress, target))
        {
            target = FindBestTarget(definition, state, config, progress, home.position, utcTicks);
            if (target.valid)
            {
                state.targetKind = target.kind;
                state.targetObjectId = target.objectId;
                state.targetPosition = target.position;
                state.lastError = "Новая цель разведки: " + target.displayName + ".";
                LogEvent(state, state.lastError);
                changed++;
            }
        }

        if (target.valid && state.GetCargoAmount(SurveySystem.PaperItemId) <= 0)
        {
            ScoutedObjectState known = progress.GetScoutedObjectState(target.kind, target.objectId, false);
            if (known != null && known.factsComplete)
            {
                state.targetObjectId = "";
                changed += MoveHomeOrUnload(definition, state, home, progress, deltaSeconds);
                if (state.GetCargoAmount(SurveySystem.PaperItemId) <= 0)
                {
                    state.status = ScoutShipStatus.WaitingForPaper;
                    state.lastError = "Нужна бумага, чтобы снять научную информацию с уже изученных целей.";
                }

                return changed;
            }
        }

        if (!target.valid)
        {
            state.status = ScoutShipStatus.Idle;
            state.lastError = "В радиусе поиска нет неизвестных или недоисследованных объектов.";
            SurveySystem.ObserveAndExtractWorldFromPoint(
                config,
                progress,
                state.lastKnownPosition,
                Mathf.Max(1f, definition.observationRadiusMeters),
                Mathf.Max(0.01f, definition.factsAtHalfRadiusPerSecond),
                deltaSeconds,
                utcTicks,
                state.cargo,
                definition.rockInfoEfficiency,
                definition.cloudInfoEfficiency,
                definition.leviathanInfoEfficiency,
                definition.paperToInfoEfficiency);
            return changed;
        }

        float observeRadius = Mathf.Max(1f, definition.observationRadiusMeters);
        float distanceToTarget = Vector3.Distance(state.lastKnownPosition, target.position);
        if (distanceToTarget > observeRadius * 0.55f)
        {
            float step = Mathf.Max(1f, definition.cruiseSpeedMS) * deltaSeconds;
            state.lastKnownPosition = Vector3.MoveTowards(state.lastKnownPosition, target.position, step);
            state.status = ScoutShipStatus.Traveling;
            state.lastError = "Летит к цели разведки: " + target.displayName + ".";
        }
        else
        {
            state.status = ScoutShipStatus.Observing;
            state.lastError = "Изучает цель: " + target.displayName + ".";
        }

        changed += SurveySystem.ObserveAndExtractWorldFromPoint(
            config,
            progress,
            state.lastKnownPosition,
            observeRadius,
            Mathf.Max(0.01f, definition.factsAtHalfRadiusPerSecond),
            deltaSeconds,
            utcTicks,
            state.cargo,
            definition.rockInfoEfficiency,
            definition.cloudInfoEfficiency,
            definition.leviathanInfoEfficiency,
            definition.paperToInfoEfficiency);

        if (TargetIsDoneForScout(definition, progress, target))
        {
            state.targetObjectId = "";
            state.lastError = "Цель полностью изучена: " + target.displayName + ".";
            changed++;
        }

        return changed;
    }

    private int MoveHomeOrUnload(
        ScoutShipDefinition definition,
        ScoutShipState state,
        IslandConfig home,
        PlayerProgress progress,
        float deltaSeconds)
    {
        float distance = Vector3.Distance(state.lastKnownPosition, home.position);
        if (distance > Mathf.Max(10f, definition.observationRadiusMeters * 0.15f))
        {
            state.status = ScoutShipStatus.ReturningHome;
            state.lastKnownPosition = Vector3.MoveTowards(state.lastKnownPosition, home.position, Mathf.Max(1f, definition.cruiseSpeedMS) * deltaSeconds);
            state.lastError = "Возвращается домой для выгрузки информации или бумаги.";
            return 1;
        }

        IslandProductionState storage = progress.GetIslandProductionState(home.id, true);
        int unloaded = UnloadInfoCargo(state, storage);
        int loadedPaper = LoadPaperCargo(definition, state, storage);
        if (unloaded > 0 || loadedPaper > 0)
        {
            state.status = ScoutShipStatus.Unloading;
            state.lastError = $"Домашний остров: выгружено информации {unloaded} кг, загружено бумаги {loadedPaper} кг.";
            LogEvent(state, state.lastError);
            return unloaded + loadedPaper;
        }

        state.status = state.GetCargoAmount(SurveySystem.PaperItemId) > 0 ? ScoutShipStatus.Idle : ScoutShipStatus.WaitingForPaper;
        return 0;
    }

    private bool TryFleeFromLeviathan(ScoutShipDefinition definition, ScoutShipState state, float deltaSeconds)
    {
        float safeDistance = Mathf.Max(0f, definition.safeLeviathanDistanceMeters);
        if (safeDistance <= 0f) return false;

        Leviathan.GetActiveLeviathans(leviathanBuffer, true);
        Leviathan nearest = null;
        float bestSqr = safeDistance * safeDistance;
        for (int i = 0; i < leviathanBuffer.Count; i++)
        {
            Leviathan leviathan = leviathanBuffer[i];
            if (leviathan == null) continue;
            float sqr = (leviathan.transform.position - state.lastKnownPosition).sqrMagnitude;
            if (sqr > bestSqr) continue;
            bestSqr = sqr;
            nearest = leviathan;
        }

        if (nearest == null) return false;

        Vector3 away = state.lastKnownPosition - nearest.transform.position;
        if (away.sqrMagnitude < 0.01f) away = Vector3.right;
        float fleeSpeed = Mathf.Max(1f, definition.cruiseSpeedMS) * 1.45f;
        state.lastKnownPosition += away.normalized * fleeSpeed * deltaSeconds;
        state.status = ScoutShipStatus.Fleeing;
        state.lastError = "Уходит от агрессивного левиафана: " + nearest.displayName + ".";
        return true;
    }

    private int AdvancePassiveFleetObservation(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        float deltaSeconds = Mathf.Max(0.01f, (float)new TimeSpan(toUtcTicks - fromUtcTicks).TotalSeconds);
        float radius = Mathf.Max(0f, passiveFleetObservationRadiusMeters);
        float speed = Mathf.Max(0.01f, passiveFleetFactsAtHalfRadiusPerSecond);
        if (radius <= 0f) return 0;

        int changed = 0;
        if (progress.logisticsShips != null)
        {
            for (int i = 0; i < progress.logisticsShips.Count; i++)
            {
                LogisticsShipState ship = progress.logisticsShips[i];
                if (ship == null) continue;
                changed += SurveySystem.ObserveWorldFromPoint(config, progress, ship.lastKnownPosition, radius, speed, deltaSeconds, toUtcTicks);
            }
        }

        if (progress.gasHarvesterShips != null)
        {
            for (int i = 0; i < progress.gasHarvesterShips.Count; i++)
            {
                GasHarvesterShipState ship = progress.gasHarvesterShips[i];
                if (ship == null) continue;
                changed += SurveySystem.ObserveWorldFromPoint(config, progress, ship.lastKnownPosition, radius, speed, deltaSeconds, toUtcTicks);
            }
        }

        if (progress.miningShips != null)
        {
            for (int i = 0; i < progress.miningShips.Count; i++)
            {
                MiningShipState ship = progress.miningShips[i];
                if (ship == null) continue;
                changed += SurveySystem.ObserveWorldFromPoint(config, progress, ship.lastKnownPosition, radius, speed, deltaSeconds, toUtcTicks);
            }
        }

        return changed;
    }

    private SurveyTarget FindBestTarget(
        ScoutShipDefinition definition,
        ScoutShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        Vector3 homePosition,
        long utcTicks)
    {
        SurveyTarget best = default;
        float bestScore = float.NegativeInfinity;
        float searchRadius = Mathf.Max(0f, definition.searchRadiusMeters);

        for (int i = 0; i < config.gasClouds.Count; i++)
        {
            GasCloudConfig cloud = config.gasClouds[i];
            if (cloud == null || string.IsNullOrWhiteSpace(cloud.id)) continue;

            ScoutedObjectState known = progress.GetScoutedObjectState(ScoutedObjectKind.GasCloud, cloud.id, false);
            if (!NeedsScoutAttention(definition.cloudInfoEfficiency, known)) continue;

            float distance = Vector3.Distance(homePosition, cloud.position);
            if (distance > searchRadius) continue;

            float score = ScoreTarget(known, distance, definition.cloudInfoEfficiency);
            if (score > bestScore)
            {
                bestScore = score;
                best = new SurveyTarget(ScoutedObjectKind.GasCloud, cloud.id, string.IsNullOrWhiteSpace(cloud.localNameRu) ? cloud.id : cloud.localNameRu, cloud.position);
            }
        }

        if (progress.miningRocks != null)
        {
            for (int i = 0; i < progress.miningRocks.Count; i++)
            {
                MiningRockState rock = progress.miningRocks[i];
                if (rock == null || !MiningWorldSimulator.IsRockActive(config, rock, utcTicks)) continue;

                MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
                OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
                if (zone == null || oreType == null) continue;

                ScoutedObjectState known = progress.GetScoutedObjectState(ScoutedObjectKind.MiningRock, rock.rockId, false);
                if (!NeedsScoutAttention(definition.rockInfoEfficiency, known)) continue;

                Vector3 position = MiningWorldSimulator.CalculateRockPosition(zone, rock, utcTicks);
                float distance = Vector3.Distance(homePosition, position);
                if (distance > searchRadius) continue;

                float score = ScoreTarget(known, distance, definition.rockInfoEfficiency) + oreType.baseValue * 0.5f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new SurveyTarget(ScoutedObjectKind.MiningRock, rock.rockId, string.IsNullOrWhiteSpace(oreType.localNameRu) ? rock.rockId : oreType.localNameRu, position);
                }
            }
        }

        Leviathan.GetActiveLeviathans(leviathanBuffer, false);
        for (int i = 0; i < leviathanBuffer.Count; i++)
        {
            Leviathan leviathan = leviathanBuffer[i];
            if (leviathan == null || string.IsNullOrWhiteSpace(leviathan.leviathanId)) continue;

            ScoutedObjectState known = progress.GetScoutedObjectState(ScoutedObjectKind.Leviathan, leviathan.leviathanId, false);
            if (!NeedsScoutAttention(definition.leviathanInfoEfficiency, known)) continue;

            float distance = Vector3.Distance(homePosition, leviathan.transform.position);
            if (distance > searchRadius) continue;

            float score = ScoreTarget(known, distance, definition.leviathanInfoEfficiency) + leviathan.massKg * 0.001f;
            if (score > bestScore)
            {
                bestScore = score;
                best = new SurveyTarget(ScoutedObjectKind.Leviathan, leviathan.leviathanId, leviathan.displayName, leviathan.transform.position);
            }
        }

        return best;
    }

    private SurveyTarget ResolveTarget(ScoutShipState state, WorldConfigDatabase config, PlayerProgress progress, long utcTicks)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.targetObjectId)) return default;

        if (state.targetKind == ScoutedObjectKind.GasCloud)
        {
            GasCloudConfig cloud = config.GetGasCloud(state.targetObjectId);
            if (cloud == null) return default;
            return new SurveyTarget(state.targetKind, cloud.id, string.IsNullOrWhiteSpace(cloud.localNameRu) ? cloud.id : cloud.localNameRu, cloud.position);
        }

        if (state.targetKind == ScoutedObjectKind.MiningRock)
        {
            MiningRockState rock = progress.GetMiningRockState(state.targetObjectId, false);
            if (rock == null || !MiningWorldSimulator.IsRockActive(config, rock, utcTicks)) return default;
            MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
            OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
            if (zone == null || oreType == null) return default;
            Vector3 position = MiningWorldSimulator.CalculateRockPosition(zone, rock, utcTicks);
            return new SurveyTarget(state.targetKind, rock.rockId, string.IsNullOrWhiteSpace(oreType.localNameRu) ? rock.rockId : oreType.localNameRu, position);
        }

        Leviathan leviathan = Leviathan.FindById(state.targetObjectId);
        if (leviathan == null) return default;
        return new SurveyTarget(state.targetKind, leviathan.leviathanId, leviathan.displayName, leviathan.transform.position);
    }

    private static bool NeedsScoutAttention(float efficiency, ScoutedObjectState known)
    {
        if (known == null) return true;
        if (!known.factsComplete) return true;
        if (efficiency <= 0f) return false;

        float allowedInfo = known.informationPotentialKg * Mathf.Clamp01(efficiency);
        return known.informationExtractedKg + 0.999f < allowedInfo;
    }

    private static bool TargetIsDoneForScout(ScoutShipDefinition definition, PlayerProgress progress, SurveyTarget target)
    {
        if (!target.valid) return true;

        ScoutedObjectState known = progress.GetScoutedObjectState(target.kind, target.objectId, false);
        if (known == null || !known.factsComplete) return false;

        float efficiency = target.kind switch
        {
            ScoutedObjectKind.GasCloud => definition.cloudInfoEfficiency,
            ScoutedObjectKind.MiningRock => definition.rockInfoEfficiency,
            ScoutedObjectKind.Leviathan => definition.leviathanInfoEfficiency,
            _ => 0f
        };

        if (efficiency <= 0f) return true;
        float allowedInfo = known.informationPotentialKg * Mathf.Clamp01(efficiency);
        return known.informationExtractedKg + 0.999f >= allowedInfo;
    }

    private static bool ShouldReturnHome(ScoutShipDefinition definition, ScoutShipState state)
    {
        if (CountInfoCargo(state) >= Mathf.Max(1, definition.maxInfoCargoKg)) return true;
        return state.GetCargoAmount(SurveySystem.PaperItemId) <= 0 && CountInfoCargo(state) > 0;
    }

    private static float ScoreTarget(ScoutedObjectState known, float distance, float efficiency)
    {
        if (known == null) return 10000f - distance * 0.1f;
        if (!known.factsComplete) return 7000f + (known.factsRequired - known.factsProgress) * 100f - distance * 0.1f;

        float allowedInfo = known.informationPotentialKg * Mathf.Clamp01(efficiency);
        float remainingInfo = Mathf.Max(0f, allowedInfo - known.informationExtractedKg);
        return remainingInfo * 50f - distance * 0.1f;
    }

    private static int CountInfoCargo(ScoutShipState state)
    {
        if (state == null) return 0;
        return state.GetCargoAmount(SurveySystem.RockInfoItemId)
            + state.GetCargoAmount(SurveySystem.CloudInfoItemId)
            + state.GetCargoAmount(SurveySystem.LeviathanInfoItemId);
    }

    private static int UnloadInfoCargo(ScoutShipState state, IslandProductionState storage)
    {
        if (state == null || storage == null || state.cargo == null) return 0;

        int moved = 0;
        for (int i = state.cargo.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = state.cargo[i];
            if (stack == null || stack.amount <= 0) continue;
            if (stack.resourceId != SurveySystem.RockInfoItemId
                && stack.resourceId != SurveySystem.CloudInfoItemId
                && stack.resourceId != SurveySystem.LeviathanInfoItemId)
            {
                continue;
            }

            storage.AddResource(stack.resourceId, stack.amount);
            moved += stack.amount;
            state.cargo.RemoveAt(i);
        }

        if (moved > 0)
        {
            state.completedSurveyRuns++;
        }

        return moved;
    }

    private static int LoadPaperCargo(ScoutShipDefinition definition, ScoutShipState state, IslandProductionState storage)
    {
        if (definition == null || state == null || storage == null) return 0;

        int wanted = Mathf.Max(0, definition.paperCargoTargetKg - state.GetCargoAmount(SurveySystem.PaperItemId));
        if (wanted <= 0) return 0;

        int moved = Mathf.Min(wanted, storage.GetResourceAmount(SurveySystem.PaperItemId));
        if (moved <= 0) return 0;

        if (!storage.TrySpendResource(SurveySystem.PaperItemId, moved)) return 0;
        state.AddCargo(SurveySystem.PaperItemId, moved);
        return moved;
    }

    private static void InitializeStateFromDefinition(ScoutShipState state, ScoutShipDefinition definition)
    {
        if (state == null || definition == null) return;

        state.displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.shipId : definition.displayName;
        if (string.IsNullOrWhiteSpace(state.homeIslandId))
        {
            state.homeIslandId = string.IsNullOrWhiteSpace(definition.homeIslandId) ? "capital" : definition.homeIslandId;
        }
    }

    private void LogEvent(ScoutShipState state, string message)
    {
        if (!debugLogging || state == null) return;
        Debug.Log("[Разведка] " + state.shipId + ": " + message, this);
    }

    private struct SurveyTarget
    {
        public bool valid;
        public ScoutedObjectKind kind;
        public string objectId;
        public string displayName;
        public Vector3 position;

        public SurveyTarget(ScoutedObjectKind kind, string objectId, string displayName, Vector3 position)
        {
            valid = !string.IsNullOrWhiteSpace(objectId);
            this.kind = kind;
            this.objectId = objectId;
            this.displayName = displayName ?? objectId;
            this.position = position;
        }
    }
}

[Serializable]
public class ScoutShipDefinition
{
    [InspectorName("Включен")]
    [Tooltip("Если выключено, этот разведчик не тикает в мета-симуляции.")]
    public bool enabled = true;
    [InspectorName("id корабля")]
    [Tooltip("Уникальный id разведчика в сохранении.")]
    public string shipId = "scout_01";
    [InspectorName("Название")]
    [Tooltip("Имя в инспекторе и логах.")]
    public string displayName = "Разведчик 01";
    [InspectorName("Домашний остров")]
    [Tooltip("Отсюда разведчик стартует, сюда выгружает информацию и здесь берет бумагу.")]
    public string homeIslandId = "capital";
    [InspectorName("Крейсерская скорость, м/с")]
    [Tooltip("Скорость мета-перелета разведчика между точками.")]
    public float cruiseSpeedMS = 22f;
    [InspectorName("Радиус поиска от дома, м")]
    [Tooltip("Разведчик ищет цели только в этом радиусе от домашнего острова.")]
    public float searchRadiusMeters = 2600f;
    [InspectorName("Радиус наблюдения, м")]
    [Tooltip("На таком расстоянии приборы видят объект. У края сведения собираются медленнее.")]
    public float observationRadiusMeters = 650f;
    [InspectorName("Сведения на 50% радиуса, ед/с")]
    [Tooltip("Средняя скорость сбора сведений. Ближе к объекту быстрее, на границе медленнее.")]
    public float factsAtHalfRadiusPerSecond = 2.2f;
    [InspectorName("Информация по глыбам")]
    [Tooltip("Какую долю потенциальной информации о глыбах этот прибор способен снять.")]
    [Range(0f, 1f)] public float rockInfoEfficiency = 0.85f;
    [InspectorName("Информация по облакам")]
    [Tooltip("Какую долю потенциальной информации об облаках этот прибор способен снять.")]
    [Range(0f, 1f)] public float cloudInfoEfficiency = 0.75f;
    [InspectorName("Информация по левиафанам")]
    [Tooltip("Какую долю потенциальной информации о левиафанах этот прибор способен снять.")]
    [Range(0f, 1f)] public float leviathanInfoEfficiency = 0.55f;
    [InspectorName("КПД бумаги в информацию")]
    [Tooltip("Сколько научной информации дает 1 кг бумаги. 0.2 = 5 кг бумаги на 1 кг информации.")]
    [Range(0.01f, 1f)] public float paperToInfoEfficiency = 1f;
    [InspectorName("Бумага в рейс, кг")]
    [Tooltip("Сколько бумаги разведчик пытается взять перед выходом. 1 кг бумаги превращается в 1 кг информации.")]
    public int paperCargoTargetKg = 60;
    [InspectorName("Возврат при информации, кг")]
    [Tooltip("Когда в трюме накопилось столько информации, разведчик возвращается домой и выгружает ее.")]
    public int maxInfoCargoKg = 90;
    [InspectorName("Безопасная дистанция до левиафана, м")]
    [Tooltip("Если живой левиафан подходит ближе, разведчик уходит от него и не продолжает наблюдение в упор.")]
    public float safeLeviathanDistanceMeters = 260f;
}
