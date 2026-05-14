using System;
using System.Collections.Generic;
using UnityEngine;

public class LogisticsFleetController : MonoBehaviour
{
    [InspectorName("Мета-игра")]
    public MetaGameState metaGameState;

    [InspectorName("Симуляция включена")]
    public bool simulationEnabled = true;

    [InspectorName("Запас топлива и клавдия")]
    [Tooltip("Множитель к расчетному расходу на рейс. 1.2 означает 20% резерва перед вылетом.")]
    public float reserveMultiplier = 1.2f;

    [InspectorName("Доля крейсерской скорости")]
    [Tooltip("Виртуальные грузовики идут не быстрее этой доли от максимальной скорости винта.")]
    [Range(0.1f, 1f)] public float cruiseSpeedFactor = 0.8f;

    [InspectorName("Ручка мощности в рейсе")]
    [Tooltip("Доля мощности двигателя, используемая для расчета расхода топлива в виртуальном рейсе.")]
    [Range(0.05f, 1.2f)] public float cruisePowerLever = 0.75f;

    [InspectorName("Секунд на 1 кг по умолчанию")]
    public float fallbackSecondsPerItem = 1f;

    [InspectorName("Ресурс клавдия по умолчанию")]
    public string defaultClaudiumResourceId = "claudium";

    [Header("Debug")]
    [Tooltip("Temporary event log for route setup checks.")]
    public bool debugLogging;

    [InspectorName("Маршруты")]
    public List<LogisticsRouteDefinition> routes = new List<LogisticsRouteDefinition>();

    [InspectorName("Грузовики")]
    public List<LogisticsShipDefinition> ships = new List<LogisticsShipDefinition>();

    public MetaGameState Meta
    {
        get
        {
            if (metaGameState == null)
            {
                metaGameState = FindFirstObjectByType<MetaGameState>();
            }

            return metaGameState;
        }
    }

    private void Reset()
    {
        metaGameState = FindFirstObjectByType<MetaGameState>();
        CreateExampleSetupIfEmpty();
    }

    public void CreateExampleSetupIfEmpty()
    {
        if (routes == null) routes = new List<LogisticsRouteDefinition>();
        if (ships == null) ships = new List<LogisticsShipDefinition>();

        if (routes.Count == 0)
        {
            routes.Add(new LogisticsRouteDefinition
            {
                routeId = "starter_food_wood_tools",
                displayName = "Еда, доски, инструменты",
                loop = true,
                stops = new List<LogisticsRouteStop>
                {
                    new LogisticsRouteStop
                    {
                        islandId = "Island1",
                        load = new List<LogisticsCargoOrder> { new LogisticsCargoOrder { itemId = "food", amount = 20 } },
                        targetFuelKg = 30,
                        targetClaudiumKg = 15
                    },
                    new LogisticsRouteStop
                    {
                        islandId = "Island6",
                        unload = new List<LogisticsCargoOrder> { new LogisticsCargoOrder { itemId = "food", amount = 10 } },
                        load = new List<LogisticsCargoOrder> { new LogisticsCargoOrder { itemId = "wood", amount = 10 } },
                        targetFuelKg = 30,
                        targetClaudiumKg = 15
                    },
                    new LogisticsRouteStop
                    {
                        islandId = "Island5",
                        unload = new List<LogisticsCargoOrder>
                        {
                            new LogisticsCargoOrder { itemId = "food", amount = 10 },
                            new LogisticsCargoOrder { itemId = "wood", amount = 10 }
                        },
                        load = new List<LogisticsCargoOrder> { new LogisticsCargoOrder { itemId = "tools", amount = 20 } },
                        targetFuelKg = 30,
                        targetClaudiumKg = 15
                    }
                }
            });
        }

        if (ships.Count == 0)
        {
            ships.Add(new LogisticsShipDefinition
            {
                shipId = "logi_01",
                displayName = "Грузовичок 01",
                routeId = routes[0].routeId,
                startingIslandId = routes[0].stops.Count > 0 ? routes[0].stops[0].islandId : "capital",
                hullId = "starter_hull",
                autoInstallRequiredModules = true
            });
        }
    }

    public void EnsureRuntimeShips(PlayerProgress progress)
    {
        if (progress == null) return;
        progress.Normalize();

        if (ships == null) return;
        for (int i = 0; i < ships.Count; i++)
        {
            LogisticsShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            LogisticsShipState state = progress.GetLogisticsShipState(definition.shipId, false);
            if (state == null)
            {
                state = progress.GetLogisticsShipState(definition.shipId, true);
                InitializeStateFromDefinition(state, definition);
                LogEvent(state, "created at " + state.currentIslandId);
            }

            state.displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.shipId : definition.displayName;
            state.routeId = definition.routeId ?? "";
            state.Normalize();
        }
    }

    public int Advance(
        WorldConfigDatabase config,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        long fromUtcTicks,
        long toUtcTicks)
    {
        if (!simulationEnabled || config == null || !config.isLoaded || progress == null || toUtcTicks <= fromUtcTicks)
        {
            return 0;
        }

        EnsureRuntimeShips(progress);

        int changed = 0;
        if (ships == null) return changed;

        for (int i = 0; i < ships.Count; i++)
        {
            LogisticsShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            LogisticsShipState state = progress.GetLogisticsShipState(definition.shipId, true);
            changed += AdvanceShip(definition, state, config, progress, catalog, techTree, fromUtcTicks, toUtcTicks);
        }

        return changed;
    }

    public LogisticsRouteDefinition GetRoute(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId) || routes == null) return null;

        for (int i = 0; i < routes.Count; i++)
        {
            LogisticsRouteDefinition route = routes[i];
            if (route != null && route.routeId == routeId)
            {
                return route;
            }
        }

        return null;
    }

    private int AdvanceShip(
        LogisticsShipDefinition definition,
        LogisticsShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        long fromUtcTicks,
        long toUtcTicks)
    {
        if (state == null) return 0;

        LogisticsRouteDefinition route = GetRoute(definition.routeId);
        if (route == null || route.stops == null || route.stops.Count == 0)
        {
            SetWaiting(state, "Маршрут не найден или пуст.");
            return 0;
        }

        LogisticsShipMetrics metrics;
        if (!TryBuildMetrics(definition, progress, catalog, techTree, config, state.GetCargoMassKg(), out metrics, out string metricsError))
        {
            SetError(state, metricsError);
            return 0;
        }

        int changed = 0;
        long cursorTicks = fromUtcTicks;
        int guard = 0;

        EnsureShipHasIsland(state, definition, route, config);

        while (cursorTicks <= toUtcTicks && guard < 10000)
        {
            guard++;
            state.Normalize();

            if (state.status == LogisticsShipStatus.Flying)
            {
                if (state.flightArrivesUtcTicks <= 0)
                {
                    SetError(state, "Рейс в некорректном состоянии: нет времени прибытия.");
                    break;
                }

                if (toUtcTicks < state.flightArrivesUtcTicks)
                {
                    UpdateFlightPosition(state, config, toUtcTicks);
                    break;
                }

                cursorTicks = Math.Max(cursorTicks, state.flightArrivesUtcTicks);
                CompleteFlight(state, route, config);
                changed++;
                continue;
            }

            if (state.status == LogisticsShipStatus.Loading)
            {
                if (state.nextEventUtcTicks <= 0 || toUtcTicks < state.nextEventUtcTicks)
                {
                    break;
                }

                cursorTicks = Math.Max(cursorTicks, state.nextEventUtcTicks);
                state.status = LogisticsShipStatus.Idle;
                state.nextEventUtcTicks = 0;
                state.lastError = "";
                LogEvent(state, "loading finished at " + state.currentIslandId + ", cargo=" + FormatCargo(state.cargo));
                changed++;
                continue;
            }

            if (state.status == LogisticsShipStatus.Finished)
            {
                break;
            }

            bool advanced = TryAdvanceIdleShip(definition, state, route, config, progress, metrics, cursorTicks, out bool scheduled);
            if (advanced)
            {
                changed++;
            }

            if (!advanced)
            {
                break;
            }

            if (scheduled)
            {
                long nextEventTicks = state.nextEventUtcTicks;
                if (nextEventTicks > cursorTicks && nextEventTicks <= toUtcTicks)
                {
                    continue;
                }

                break;
            }
        }

        return changed;
    }

    private bool TryAdvanceIdleShip(
        LogisticsShipDefinition definition,
        LogisticsShipState state,
        LogisticsRouteDefinition route,
        WorldConfigDatabase config,
        PlayerProgress progress,
        LogisticsShipMetrics metrics,
        long currentTicks,
        out bool scheduled)
    {
        scheduled = false;

        if (state.needsStopService)
        {
            if (!TryBeginStopService(definition, state, route, config, progress, metrics, currentTicks, out scheduled, out string stopError))
            {
                SetWaiting(state, stopError);
                return false;
            }

            if (scheduled)
            {
                return true;
            }
        }

        if (!route.loop && state.currentStopIndex >= route.stops.Count - 1)
        {
            state.status = LogisticsShipStatus.Finished;
            state.lastError = "Маршрут завершен.";
            return true;
        }

        if (!TryStartNextFlight(definition, state, route, config, progress, metrics, currentTicks, out string flightError))
        {
            SetWaiting(state, flightError);
            return false;
        }

        scheduled = true;
        return true;
    }

    private bool TryBeginStopService(
        LogisticsShipDefinition definition,
        LogisticsShipState state,
        LogisticsRouteDefinition route,
        WorldConfigDatabase config,
        PlayerProgress progress,
        LogisticsShipMetrics metrics,
        long currentTicks,
        out bool scheduled,
        out string error)
    {
        scheduled = false;
        error = "";

        LogisticsRouteStop stop = GetCurrentStop(state, route);
        if (stop == null)
        {
            error = "Текущая остановка маршрута не найдена.";
            return false;
        }

        IslandProductionState storage = progress.GetIslandProductionState(stop.islandId, true);
        if (storage == null)
        {
            error = "Склад острова не найден: " + stop.islandId;
            return false;
        }

        Dictionary<string, int> shipCargo = ToMap(state.cargo);
        Dictionary<string, int> islandStorage = ToMap(storage.storage);
        int movedUnits = 0;

        ApplyUnloadOrders(stop.unload, shipCargo, islandStorage, ref movedUnits);

        if (!CanApplyLoadOrders(stop.load, islandStorage, out error))
        {
            return false;
        }

        ApplyLoadOrders(stop.load, shipCargo, islandStorage, ref movedUnits);

        if (stop.refuelEngine && !string.IsNullOrWhiteSpace(metrics.engineFuelId) && stop.targetFuelKg > 0)
        {
            movedUnits += MoveUpToTarget(islandStorage, shipCargo, metrics.engineFuelId, stop.targetFuelKg);
        }

        if (stop.refillClaudium && stop.targetClaudiumKg > 0)
        {
            movedUnits += MoveUpToTarget(islandStorage, shipCargo, ResolveClaudiumResourceId(definition, stop.claudiumResourceId), stop.targetClaudiumKg);
        }

        if (GetCargoMassKg(shipCargo) > metrics.maxCargoKg + 0.001f)
        {
            error = $"После операций груз {GetCargoMassKg(shipCargo):F0} кг больше грузоподъемности {metrics.maxCargoKg:F0} кг.";
            return false;
        }

        ApplyMapToStacks(shipCargo, state.cargo);
        ApplyMapToStacks(islandStorage, storage.storage);
        state.needsStopService = false;
        state.status = LogisticsShipStatus.Idle;
        state.lastError = "";

        if (movedUnits > 0)
        {
            float secondsPerItem = GetSecondsPerItem(config, stop.islandId);
            state.status = LogisticsShipStatus.Loading;
            state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(Mathf.Max(0.01f, secondsPerItem) * movedUnits).Ticks;
            scheduled = true;
            LogEvent(state, $"service at {stop.islandId}: moved={movedUnits}, duration={Mathf.Max(0.01f, secondsPerItem) * movedUnits:F1}s, cargo={FormatCargo(state.cargo)}");
        }
        else
        {
            LogEvent(state, "service at " + stop.islandId + ": nothing to move");
        }

        return true;
    }

    private bool TryStartNextFlight(
        LogisticsShipDefinition definition,
        LogisticsShipState state,
        LogisticsRouteDefinition route,
        WorldConfigDatabase config,
        PlayerProgress progress,
        LogisticsShipMetrics metrics,
        long currentTicks,
        out string error)
    {
        error = "";
        int nextStopIndex = GetNextStopIndex(state, route);
        if (nextStopIndex < 0)
        {
            state.status = LogisticsShipStatus.Finished;
            state.lastError = "Маршрут завершен.";
            return true;
        }

        LogisticsRouteStop currentStop = GetCurrentStop(state, route);
        LogisticsRouteStop nextStop = route.stops[nextStopIndex];
        IslandConfig fromIsland = config.GetIsland(currentStop != null ? currentStop.islandId : state.currentIslandId);
        IslandConfig toIsland = config.GetIsland(nextStop.islandId);
        if (fromIsland == null || toIsland == null)
        {
            error = "Остров рейса не найден.";
            return false;
        }

        LogisticsLegEstimate estimate = EstimateLeg(metrics, fromIsland.position, toIsland.position, state.GetCargoMassKg());
        if (!estimate.canFly)
        {
            error = estimate.reason;
            return false;
        }

        IslandProductionState storage = progress.GetIslandProductionState(fromIsland.id, true);
        if (!TryTopUpForLeg(state, storage, metrics.engineFuelId, estimate.requiredFuelKg))
        {
            error = $"Не хватает топлива {metrics.engineFuelId}: нужно {estimate.requiredFuelKg} кг.";
            return false;
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition, currentStop != null ? currentStop.claudiumResourceId : "");
        if (!TryTopUpForLeg(state, storage, claudiumResourceId, estimate.requiredClaudiumKg))
        {
            error = $"Не хватает клавдия: нужно {estimate.requiredClaudiumKg} кг.";
            return false;
        }

        if (state.GetCargoMassKg() > metrics.maxCargoKg + 0.001f)
        {
            error = $"После дозаправки груз {state.GetCargoMassKg():F0} кг больше грузоподъемности {metrics.maxCargoKg:F0} кг.";
            return false;
        }

        if (!state.TrySpendCargo(metrics.engineFuelId, estimate.requiredFuelKg))
        {
            error = $"Не удалось списать топливо {metrics.engineFuelId}.";
            return false;
        }

        if (!state.TrySpendCargo(claudiumResourceId, estimate.requiredClaudiumKg))
        {
            error = "Не удалось списать клавдий.";
            return false;
        }

        state.status = LogisticsShipStatus.Flying;
        state.currentIslandId = fromIsland.id;
        state.targetIslandId = toIsland.id;
        state.targetStopIndex = nextStopIndex;
        state.flightStartedUtcTicks = currentTicks;
        state.flightArrivesUtcTicks = currentTicks + TimeSpan.FromSeconds(estimate.durationSeconds).Ticks;
        state.nextEventUtcTicks = state.flightArrivesUtcTicks;
        state.lastKnownPosition = fromIsland.position;
        state.lastError = $"Рейс: {fromIsland.id} -> {toIsland.id}.";
        UpdateFlightPosition(state, config, currentTicks);
        LogEvent(state, $"flight started {fromIsland.id}->{toIsland.id}, eta={estimate.durationSeconds:F1}s, fuel={estimate.requiredFuelKg}, claudium={estimate.requiredClaudiumKg}, cargo={FormatCargo(state.cargo)}");
        return true;
    }

    private string ResolveClaudiumResourceId(LogisticsShipDefinition definition, string stopResourceId)
    {
        if (!string.IsNullOrWhiteSpace(stopResourceId)) return stopResourceId;
        if (definition != null && !string.IsNullOrWhiteSpace(definition.claudiumResourceId)) return definition.claudiumResourceId;
        return string.IsNullOrWhiteSpace(defaultClaudiumResourceId) ? "claudium" : defaultClaudiumResourceId;
    }

    private bool TryBuildMetrics(
        LogisticsShipDefinition definition,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        WorldConfigDatabase config,
        float currentCargoKg,
        out LogisticsShipMetrics metrics,
        out string error)
    {
        metrics = new LogisticsShipMetrics();
        error = "";

        if (definition == null)
        {
            error = "Нет определения грузовика.";
            return false;
        }

        if (catalog == null)
        {
            error = "Нет каталога кораблей.";
            return false;
        }

        PlayerProgress assemblyProgress = progress != null ? progress.Clone() : new PlayerProgress();
        assemblyProgress.selectedHullId = string.IsNullOrWhiteSpace(definition.hullId) ? catalog.starterHullId : definition.hullId;
        assemblyProgress.installedModules = CloneInstalledModules(definition.installedModules);
        assemblyProgress.Normalize();

        if (definition.autoInstallRequiredModules)
        {
            ShipAssemblyBuilder.AutoInstallRequiredModules(catalog, techTree, assemblyProgress, out _);
        }

        if (!ShipAssemblyBuilder.TryBuild(catalog, techTree, assemblyProgress, out ShipAssemblyResult result))
        {
            error = result != null ? result.message : "Сборка грузовика невалидна.";
            return false;
        }

        ShipStatBlock stats = result.stats;
        metrics.emptyMassKg = stats.Get(ShipStatId.BaseMass, 0f);
        metrics.enginePowerKw = stats.Get(ShipStatId.EngineMaxPower, 0f);
        metrics.engineFuelEfficiency = Mathf.Clamp(stats.Get(ShipStatId.EngineFuelEfficiency, 0.32f), 0.01f, 0.95f);
        metrics.engineFuelId = string.IsNullOrWhiteSpace(stats.EngineFuelId) ? "wood" : stats.EngineFuelId;
        metrics.propellerMaxSpeedMS = stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f);
        metrics.maxAutoVerticalSpeedMS = stats.Get(ShipStatId.MaxAutoVerticalSpeed, 1f);
        metrics.maxStructuralVerticalSpeedMS = stats.Get(ShipStatId.MaxStructuralVerticalSpeed, 1f);
        metrics.claudiumConsumptionPerTonSecond = stats.Get(ShipStatId.ClaudiumConsumptionPerTonSecond, 0f);
        metrics.claudiumMaxLiftKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
        metrics.claudiumLiftEfficiency = stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f);
        metrics.hullLimitKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, metrics.emptyMassKg);
        metrics.engineLiftKg = metrics.enginePowerKw * 0.9f * metrics.claudiumLiftEfficiency;
        metrics.allowedTakeoffMassKg = Mathf.Min(metrics.engineLiftKg, Mathf.Min(metrics.claudiumMaxLiftKg, metrics.hullLimitKg));
        metrics.maxCargoKg = Mathf.Max(0f, metrics.allowedTakeoffMassKg - metrics.emptyMassKg);
        metrics.cruiseSpeedMS = Mathf.Max(0f, metrics.propellerMaxSpeedMS * Mathf.Clamp(cruiseSpeedFactor, 0.1f, 1f));
        metrics.cruisePowerKw = Mathf.Max(0f, metrics.enginePowerKw * Mathf.Clamp(cruisePowerLever, 0.05f, 1.2f));

        ItemConfig fuel = config != null ? config.GetItem(metrics.engineFuelId) : null;
        metrics.fuelEnergyKwhPerKg = fuel != null ? Mathf.Max(0f, fuel.energyKwhPerKg) : 0f;

        if (metrics.enginePowerKw <= 0f)
        {
            error = "У грузовика нет мощности двигателя.";
            return false;
        }

        if (metrics.cruiseSpeedMS <= 0f)
        {
            error = "У грузовика нет крейсерской скорости.";
            return false;
        }

        if (metrics.allowedTakeoffMassKg <= metrics.emptyMassKg)
        {
            error = "Грузовик не может взлететь: сухая масса выше доступной подъемной силы.";
            return false;
        }

        if (currentCargoKg > metrics.maxCargoKg + 0.001f)
        {
            error = $"Грузовик перегружен: {currentCargoKg:F0}/{metrics.maxCargoKg:F0} кг.";
            return false;
        }

        return true;
    }

    private LogisticsLegEstimate EstimateLeg(LogisticsShipMetrics metrics, Vector3 from, Vector3 to, float cargoKg)
    {
        LogisticsLegEstimate estimate = new LogisticsLegEstimate();
        float totalMassKg = metrics.emptyMassKg + Mathf.Max(0f, cargoKg);
        if (totalMassKg > metrics.allowedTakeoffMassKg + 0.001f)
        {
            estimate.reason = $"Взлетная масса {totalMassKg:F0} кг выше лимита {metrics.allowedTakeoffMassKg:F0} кг.";
            return estimate;
        }

        float horizontalDistance = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        float verticalDistance = Mathf.Abs(to.y - from.y);
        float horizontalTime = horizontalDistance / Mathf.Max(0.1f, metrics.cruiseSpeedMS);
        float verticalLimit = Mathf.Min(metrics.maxAutoVerticalSpeedMS, metrics.maxStructuralVerticalSpeedMS);
        float verticalTime = verticalDistance / Mathf.Max(0.1f, verticalLimit);
        estimate.durationSeconds = Mathf.Max(1f, Mathf.Max(horizontalTime, verticalTime));

        float reserve = Mathf.Max(1f, reserveMultiplier);
        float fuelKg = 0f;
        if (metrics.cruisePowerKw > 0f && metrics.engineFuelEfficiency > 0f && metrics.fuelEnergyKwhPerKg > 0f)
        {
            fuelKg = metrics.cruisePowerKw / metrics.engineFuelEfficiency / metrics.fuelEnergyKwhPerKg * (estimate.durationSeconds / 3600f);
        }

        float claudiumKg = metrics.claudiumConsumptionPerTonSecond * Mathf.Max(0f, totalMassKg / 1000f) * estimate.durationSeconds;
        estimate.requiredFuelKg = Mathf.CeilToInt(fuelKg * reserve);
        estimate.requiredClaudiumKg = Mathf.CeilToInt(claudiumKg * reserve);
        estimate.canFly = true;
        estimate.reason = "";
        return estimate;
    }

    private static void InitializeStateFromDefinition(LogisticsShipState state, LogisticsShipDefinition definition)
    {
        state.displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.shipId : definition.displayName;
        state.routeId = definition.routeId ?? "";
        state.currentIslandId = definition.startingIslandId ?? "";
        state.currentStopIndex = 0;
        state.targetStopIndex = -1;
        state.status = LogisticsShipStatus.Idle;
        state.needsStopService = true;
        state.cargo = CloneResourceStacks(definition.startingCargo);
        state.lastError = "Готов к маршруту.";
    }

    private void EnsureShipHasIsland(LogisticsShipState state, LogisticsShipDefinition definition, LogisticsRouteDefinition route, WorldConfigDatabase config)
    {
        if (!string.IsNullOrWhiteSpace(state.currentIslandId)) return;

        if (!string.IsNullOrWhiteSpace(definition.startingIslandId))
        {
            state.currentIslandId = definition.startingIslandId;
        }
        else if (route.stops != null && route.stops.Count > 0)
        {
            state.currentIslandId = route.stops[0].islandId;
        }

        IslandConfig island = config.GetIsland(state.currentIslandId);
        if (island != null)
        {
            state.lastKnownPosition = island.position;
        }
    }

    private static LogisticsRouteStop GetCurrentStop(LogisticsShipState state, LogisticsRouteDefinition route)
    {
        if (state == null || route == null || route.stops == null || route.stops.Count == 0) return null;
        state.currentStopIndex = Mathf.Clamp(state.currentStopIndex, 0, route.stops.Count - 1);
        return route.stops[state.currentStopIndex];
    }

    private static int GetNextStopIndex(LogisticsShipState state, LogisticsRouteDefinition route)
    {
        if (state == null || route == null || route.stops == null || route.stops.Count == 0) return -1;

        if (state.currentStopIndex < route.stops.Count - 1)
        {
            return state.currentStopIndex + 1;
        }

        return route.loop ? 0 : -1;
    }

    private void CompleteFlight(LogisticsShipState state, LogisticsRouteDefinition route, WorldConfigDatabase config)
    {
        state.currentIslandId = state.targetIslandId;
        state.currentStopIndex = Mathf.Clamp(state.targetStopIndex, 0, route.stops.Count - 1);
        state.targetIslandId = "";
        state.targetStopIndex = -1;
        state.flightStartedUtcTicks = 0;
        state.flightArrivesUtcTicks = 0;
        state.nextEventUtcTicks = 0;
        state.needsStopService = true;
        state.status = LogisticsShipStatus.Idle;
        state.lastError = "Прибыл: " + state.currentIslandId;

        IslandConfig island = config.GetIsland(state.currentIslandId);
        if (island != null)
        {
            state.lastKnownPosition = island.position;
        }

        if (route.loop && state.currentStopIndex == 0)
        {
            state.completedRouteLoops++;
        }

        LogEvent(state, "flight completed at " + state.currentIslandId + ", loops=" + state.completedRouteLoops);
    }

    private static void UpdateFlightPosition(LogisticsShipState state, WorldConfigDatabase config, long utcTicks)
    {
        if (state == null || state.status != LogisticsShipStatus.Flying) return;

        IslandConfig fromIsland = config.GetIsland(state.currentIslandId);
        IslandConfig toIsland = config.GetIsland(state.targetIslandId);
        if (fromIsland == null || toIsland == null) return;

        float t = 0f;
        if (state.flightArrivesUtcTicks > state.flightStartedUtcTicks)
        {
            t = Mathf.Clamp01((utcTicks - state.flightStartedUtcTicks) / (float)(state.flightArrivesUtcTicks - state.flightStartedUtcTicks));
        }

        state.lastKnownPosition = Vector3.Lerp(fromIsland.position, toIsland.position, t);
    }

    private static bool TryTopUpForLeg(LogisticsShipState state, IslandProductionState storage, string resourceId, int requiredAmount)
    {
        if (requiredAmount <= 0) return true;
        if (state.GetCargoAmount(resourceId) >= requiredAmount) return true;
        if (storage == null || string.IsNullOrWhiteSpace(resourceId)) return false;

        int needed = requiredAmount - state.GetCargoAmount(resourceId);
        int available = storage.GetResourceAmount(resourceId);
        int moved = Mathf.Min(needed, available);
        if (moved > 0)
        {
            storage.TrySpendResource(resourceId, moved);
            state.AddCargo(resourceId, moved);
        }

        return state.GetCargoAmount(resourceId) >= requiredAmount;
    }

    private static void ApplyUnloadOrders(List<LogisticsCargoOrder> orders, Dictionary<string, int> shipCargo, Dictionary<string, int> islandStorage, ref int movedUnits)
    {
        if (orders == null) return;

        for (int i = 0; i < orders.Count; i++)
        {
            LogisticsCargoOrder order = orders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.itemId) || order.amount <= 0) continue;

            int current = GetAmount(shipCargo, order.itemId);
            int moved = Mathf.Min(current, order.amount);
            if (moved <= 0) continue;

            SetAmount(shipCargo, order.itemId, current - moved);
            SetAmount(islandStorage, order.itemId, GetAmount(islandStorage, order.itemId) + moved);
            movedUnits += moved;
        }
    }

    private static bool CanApplyLoadOrders(List<LogisticsCargoOrder> orders, Dictionary<string, int> islandStorage, out string error)
    {
        error = "";
        if (orders == null) return true;

        for (int i = 0; i < orders.Count; i++)
        {
            LogisticsCargoOrder order = orders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.itemId) || order.amount <= 0) continue;
            if (GetAmount(islandStorage, order.itemId) < order.amount)
            {
                error = $"На складе не хватает {order.itemId}: нужно {order.amount} кг.";
                return false;
            }
        }

        return true;
    }

    private static void ApplyLoadOrders(List<LogisticsCargoOrder> orders, Dictionary<string, int> shipCargo, Dictionary<string, int> islandStorage, ref int movedUnits)
    {
        if (orders == null) return;

        for (int i = 0; i < orders.Count; i++)
        {
            LogisticsCargoOrder order = orders[i];
            if (order == null || string.IsNullOrWhiteSpace(order.itemId) || order.amount <= 0) continue;

            SetAmount(islandStorage, order.itemId, GetAmount(islandStorage, order.itemId) - order.amount);
            SetAmount(shipCargo, order.itemId, GetAmount(shipCargo, order.itemId) + order.amount);
            movedUnits += order.amount;
        }
    }

    private static int MoveUpToTarget(Dictionary<string, int> from, Dictionary<string, int> to, string itemId, int targetAmount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || targetAmount <= 0) return 0;

        int current = GetAmount(to, itemId);
        int needed = Mathf.Max(0, targetAmount - current);
        int moved = Mathf.Min(needed, GetAmount(from, itemId));
        if (moved <= 0) return 0;

        SetAmount(from, itemId, GetAmount(from, itemId) - moved);
        SetAmount(to, itemId, current + moved);
        return moved;
    }

    private float GetSecondsPerItem(WorldConfigDatabase config, string islandId)
    {
        IslandConfig island = config != null ? config.GetIsland(islandId) : null;
        if (island != null && island.timeForOneItemLoadSeconds > 0f)
        {
            return island.timeForOneItemLoadSeconds;
        }

        return Mathf.Max(0.01f, fallbackSecondsPerItem);
    }

    private static Dictionary<string, int> ToMap(List<ResourceStack> stacks)
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        if (stacks == null) return map;

        for (int i = 0; i < stacks.Count; i++)
        {
            ResourceStack stack = stacks[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            SetAmount(map, stack.resourceId, GetAmount(map, stack.resourceId) + stack.amount);
        }

        return map;
    }

    private static void ApplyMapToStacks(Dictionary<string, int> map, List<ResourceStack> stacks)
    {
        if (stacks == null) return;
        stacks.Clear();
        foreach (KeyValuePair<string, int> pair in map)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) continue;
            stacks.Add(new ResourceStack { resourceId = pair.Key, amount = pair.Value });
        }
    }

    private static int GetAmount(Dictionary<string, int> map, string itemId)
    {
        if (map == null || string.IsNullOrWhiteSpace(itemId)) return 0;
        return map.TryGetValue(itemId, out int amount) ? Mathf.Max(0, amount) : 0;
    }

    private static void SetAmount(Dictionary<string, int> map, string itemId, int amount)
    {
        if (map == null || string.IsNullOrWhiteSpace(itemId)) return;
        if (amount <= 0)
        {
            map.Remove(itemId);
        }
        else
        {
            map[itemId] = amount;
        }
    }

    private static float GetCargoMassKg(Dictionary<string, int> cargo)
    {
        if (cargo == null) return 0f;
        int total = 0;
        foreach (KeyValuePair<string, int> pair in cargo)
        {
            total += Mathf.Max(0, pair.Value);
        }

        return total;
    }

    private static List<ResourceStack> CloneResourceStacks(List<ResourceStack> source)
    {
        List<ResourceStack> clone = new List<ResourceStack>();
        if (source == null) return clone;

        for (int i = 0; i < source.Count; i++)
        {
            ResourceStack stack = source[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            clone.Add(new ResourceStack { resourceId = stack.resourceId, amount = stack.amount });
        }

        return clone;
    }

    private static List<InstalledModuleState> CloneInstalledModules(List<InstalledModuleState> source)
    {
        List<InstalledModuleState> clone = new List<InstalledModuleState>();
        if (source == null) return clone;

        for (int i = 0; i < source.Count; i++)
        {
            InstalledModuleState module = source[i];
            if (module == null || string.IsNullOrWhiteSpace(module.slotId) || string.IsNullOrWhiteSpace(module.moduleId)) continue;
            clone.Add(new InstalledModuleState { slotId = module.slotId, moduleId = module.moduleId });
        }

        return clone;
    }

    private void SetWaiting(LogisticsShipState state, string message)
    {
        string text = string.IsNullOrWhiteSpace(message) ? "Ожидает ресурсов." : message;
        bool changed = state.status != LogisticsShipStatus.WaitingForResources || state.lastError != text;
        state.status = LogisticsShipStatus.WaitingForResources;
        state.nextEventUtcTicks = 0;
        state.lastError = text;
        if (changed)
        {
            LogEvent(state, "waiting: " + text);
        }
    }

    private void SetError(LogisticsShipState state, string message)
    {
        string text = string.IsNullOrWhiteSpace(message) ? "Ошибка логистики." : message;
        bool changed = state.status != LogisticsShipStatus.Error || state.lastError != text;
        state.status = LogisticsShipStatus.Error;
        state.nextEventUtcTicks = 0;
        state.lastError = text;
        if (changed)
        {
            LogEvent(state, "error: " + text);
        }
    }

    private void LogEvent(LogisticsShipState state, string message)
    {
        if (!debugLogging) return;

        string shipId = state != null && !string.IsNullOrWhiteSpace(state.shipId) ? state.shipId : "unknown";
        Debug.Log("[Logistics] " + shipId + ": " + message, this);
    }

    private static string FormatCargo(List<ResourceStack> cargo)
    {
        if (cargo == null || cargo.Count == 0) return "empty";

        string text = "";
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            if (text.Length > 0) text += ", ";
            text += stack.resourceId + "=" + stack.amount;
        }

        return text.Length > 0 ? text : "empty";
    }
}

[Serializable]
public class LogisticsRouteDefinition
{
    public string routeId = "route";
    public string displayName = "Маршрут";
    public bool loop = true;
    public List<LogisticsRouteStop> stops = new List<LogisticsRouteStop>();
}

[Serializable]
public class LogisticsRouteStop
{
    public string islandId = "capital";
    public List<LogisticsCargoOrder> unload = new List<LogisticsCargoOrder>();
    public List<LogisticsCargoOrder> load = new List<LogisticsCargoOrder>();
    public bool refuelEngine = true;
    public int targetFuelKg = 30;
    public bool refillClaudium = true;
    public string claudiumResourceId = "claudium";
    public int targetClaudiumKg = 15;
}

[Serializable]
public class LogisticsCargoOrder
{
    public string itemId = "";
    public int amount;
}

[Serializable]
public class LogisticsShipDefinition
{
    public bool enabled = true;
    public string shipId = "logi_01";
    public string displayName = "Грузовичок";
    public string routeId = "route";
    public string startingIslandId = "capital";
    public string hullId = "starter_hull";
    public string claudiumResourceId = "claudium";
    public bool autoInstallRequiredModules = true;
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public List<ResourceStack> startingCargo = new List<ResourceStack>();
}

public enum LogisticsShipStatus
{
    Idle,
    Loading,
    Flying,
    WaitingForResources,
    Finished,
    Error
}

[Serializable]
public class LogisticsShipState
{
    public string shipId = "";
    public string displayName = "";
    public string routeId = "";
    public LogisticsShipStatus status = LogisticsShipStatus.Idle;
    public string currentIslandId = "";
    public string targetIslandId = "";
    public int currentStopIndex;
    public int targetStopIndex = -1;
    public bool needsStopService = true;
    public long nextEventUtcTicks;
    public long flightStartedUtcTicks;
    public long flightArrivesUtcTicks;
    public Vector3 lastKnownPosition;
    public List<ResourceStack> cargo = new List<ResourceStack>();
    public int completedRouteLoops;
    public string lastError = "";

    public void Normalize()
    {
        shipId ??= "";
        displayName ??= "";
        routeId ??= "";
        currentIslandId ??= "";
        targetIslandId ??= "";
        targetStopIndex = Mathf.Max(-1, targetStopIndex);
        currentStopIndex = Mathf.Max(0, currentStopIndex);
        cargo ??= new List<ResourceStack>();
        lastError ??= "";

        for (int i = cargo.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
            {
                cargo.RemoveAt(i);
                continue;
            }

            stack.amount = Mathf.Max(0, stack.amount);
        }
    }

    public int GetCargoAmount(string itemId)
    {
        ResourceStack stack = GetCargoStack(itemId, false);
        return stack != null ? stack.amount : 0;
    }

    public int GetCargoMassKg()
    {
        int total = 0;
        if (cargo == null) return total;
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null) continue;
            total += Mathf.Max(0, stack.amount);
        }

        return total;
    }

    public void AddCargo(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;
        ResourceStack stack = GetCargoStack(itemId, true);
        stack.amount += amount;
    }

    public bool TrySpendCargo(string itemId, int amount)
    {
        if (amount <= 0) return true;
        ResourceStack stack = GetCargoStack(itemId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        if (stack.amount <= 0)
        {
            cargo.Remove(stack);
        }

        return true;
    }

    private ResourceStack GetCargoStack(string itemId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        cargo ??= new List<ResourceStack>();

        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack != null && stack.resourceId == itemId)
            {
                return stack;
            }
        }

        if (!createIfMissing) return null;

        ResourceStack newStack = new ResourceStack { resourceId = itemId };
        cargo.Add(newStack);
        return newStack;
    }
}

public struct LogisticsShipMetrics
{
    public float emptyMassKg;
    public float enginePowerKw;
    public float engineFuelEfficiency;
    public string engineFuelId;
    public float fuelEnergyKwhPerKg;
    public float propellerMaxSpeedMS;
    public float maxAutoVerticalSpeedMS;
    public float maxStructuralVerticalSpeedMS;
    public float claudiumConsumptionPerTonSecond;
    public float claudiumMaxLiftKg;
    public float claudiumLiftEfficiency;
    public float hullLimitKg;
    public float engineLiftKg;
    public float allowedTakeoffMassKg;
    public float maxCargoKg;
    public float cruiseSpeedMS;
    public float cruisePowerKw;
}

public struct LogisticsLegEstimate
{
    public bool canFly;
    public float durationSeconds;
    public int requiredFuelKg;
    public int requiredClaudiumKg;
    public string reason;
}
