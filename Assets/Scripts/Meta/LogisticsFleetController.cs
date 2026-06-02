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
                routeId = "starter_food_aerolite_capital",
                displayName = "Еда, аэролит, столица",
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
                        islandId = "Island2",
                        unload = new List<LogisticsCargoOrder> { new LogisticsCargoOrder { itemId = "food", amount = 20 } },
                        load = new List<LogisticsCargoOrder> { new LogisticsCargoOrder { itemId = "aerolite", amount = 12 } },
                        targetFuelKg = 30,
                        targetClaudiumKg = 15
                    },
                    new LogisticsRouteStop
                    {
                        islandId = "capital",
                        unload = new List<LogisticsCargoOrder>
                        {
                            new LogisticsCargoOrder { itemId = "aerolite", amount = 12 }
                        },
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
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return;

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

        if (SessionExtractionCoreRuntime.IsCoreMode(progress))
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

        LogisticsShipMetrics metrics;
        if (!TryBuildMetrics(definition, progress, catalog, techTree, config, state.GetPayloadMassKg(config), out metrics, out string metricsError))
        {
            SetError(state, metricsError);
            return 0;
        }

        int changed = 0;
        float deltaMinutes = Mathf.Max(0f, (float)new TimeSpan(toUtcTicks - fromUtcTicks).TotalMinutes);
        IslandProductionState capitalStorage = progress.GetIslandProductionState("capital", true);
        changed += ShipboardNeedsSimulator.AdvanceLogisticsShip(config, definition, state, metrics, capitalStorage, deltaMinutes);

        LogisticsRouteDefinition route = GetRoute(definition.routeId);
        if (route == null || route.stops == null || route.stops.Count == 0)
        {
            SetWaiting(state, "Маршрут не найден или пуст.");
            return changed;
        }

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
            movedUnits += MoveStorageToTank(islandStorage, state.engineFuelTank, metrics.engineFuelId, stop.targetFuelKg, metrics.fuelTankCapacityKg);
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition, stop.claudiumResourceId);
        if (stop.refillClaudium && stop.targetClaudiumKg > 0)
        {
            movedUnits += MoveStorageToTank(islandStorage, state.claudiumTank, claudiumResourceId, stop.targetClaudiumKg, metrics.claudiumTankCapacityKg);
        }

        movedUnits += ApplyAutomaticPassengerExchange(definition, state, stop, shipCargo, islandStorage);

        if (!CargoStoragePlanner.TryValidateCargoStorage(config, metrics, shipCargo, out error))
        {
            return false;
        }

        float payloadMassKg = CargoStoragePlanner.GetCargoMassKg(config, shipCargo) + state.engineFuelTank.amountKg + state.claudiumTank.amountKg;
        if (payloadMassKg > metrics.maxCargoKg + 0.001f)
        {
            error = $"После операций полезная нагрузка {payloadMassKg:F0} кг больше грузоподъемности {metrics.maxCargoKg:F0} кг.";
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

        Dictionary<string, int> currentCargo = ToMap(state.cargo);
        float currentCargoMassKg = CargoStoragePlanner.GetCargoMassKg(config, currentCargo) + state.engineFuelTank.amountKg + state.claudiumTank.amountKg;
        float dockedShipFullMassKg = CargoStoragePlanner.GetDockedShipFullMassKg(config, currentCargo);
        LogisticsLegEstimate estimate = EstimateLeg(metrics, fromIsland.position, toIsland.position, currentCargoMassKg, dockedShipFullMassKg);
        if (!estimate.canFly)
        {
            error = estimate.reason;
            return false;
        }

        IslandProductionState storage = progress.GetIslandProductionState(fromIsland.id, true);
        if (!TryTopUpTankForLeg(state.engineFuelTank, storage, metrics.engineFuelId, estimate.requiredFuelKg, metrics.fuelTankCapacityKg))
        {
            error = $"Не хватает топлива {metrics.engineFuelId}: нужно {estimate.requiredFuelKg} кг.";
            return false;
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition, currentStop != null ? currentStop.claudiumResourceId : "");
        if (!TryTopUpTankForLeg(state.claudiumTank, storage, claudiumResourceId, estimate.requiredClaudiumKg, metrics.claudiumTankCapacityKg))
        {
            error = $"Не хватает клавдия: нужно {estimate.requiredClaudiumKg} кг.";
            return false;
        }

        Dictionary<string, int> toppedCargo = ToMap(state.cargo);
        if (!CargoStoragePlanner.TryValidateCargoStorage(config, metrics, toppedCargo, out error))
        {
            return false;
        }

        float toppedPayloadMassKg = CargoStoragePlanner.GetCargoMassKg(config, toppedCargo) + state.engineFuelTank.amountKg + state.claudiumTank.amountKg;
        if (toppedPayloadMassKg > metrics.maxCargoKg + 0.001f)
        {
            error = $"После дозаправки полезная нагрузка {toppedPayloadMassKg:F0} кг больше грузоподъемности {metrics.maxCargoKg:F0} кг.";
            return false;
        }

        if (!state.engineFuelTank.TrySpend(metrics.engineFuelId, estimate.requiredFuelKg))
        {
            error = $"Не удалось списать топливо {metrics.engineFuelId}.";
            return false;
        }

        if (!state.claudiumTank.TrySpend(claudiumResourceId, estimate.requiredClaudiumKg))
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
        metrics.engineFuelId = string.IsNullOrWhiteSpace(stats.EngineFuelId) ? "charcoal" : stats.EngineFuelId;
        metrics.propellerMaxSpeedMS = stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f);
        metrics.maxAutoVerticalSpeedMS = stats.Get(ShipStatId.MaxAutoVerticalSpeed, 1f);
        metrics.maxStructuralVerticalSpeedMS = stats.Get(ShipStatId.MaxStructuralVerticalSpeed, 1f);
        metrics.claudiumConsumptionPerTonSecond = stats.Get(ShipStatId.ClaudiumConsumptionPerTonSecond, 0f);
        metrics.claudiumMaxLiftKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
        metrics.claudiumLiftEfficiency = stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f);
        metrics.hullLimitKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, metrics.emptyMassKg);
        metrics.engineLiftKg = metrics.enginePowerKw * metrics.claudiumLiftEfficiency;
        metrics.allowedTakeoffMassKg = Mathf.Min(metrics.engineLiftKg, Mathf.Min(metrics.claudiumMaxLiftKg, metrics.hullLimitKg));
        metrics.maxCargoKg = Mathf.Max(0f, metrics.allowedTakeoffMassKg - metrics.emptyMassKg);
        metrics.fuelTankCapacityKg = ShipConsumableTankMath.CalculateFuelTankCapacityKg(metrics.maxCargoKg);
        metrics.claudiumTankCapacityKg = ShipConsumableTankMath.CalculateClaudiumTankCapacityKg(metrics.maxCargoKg);
        metrics.cruiseSpeedMS = Mathf.Max(0f, metrics.propellerMaxSpeedMS * Mathf.Clamp(cruiseSpeedFactor, 0.1f, 1f));
        metrics.cruisePowerKw = Mathf.Max(0f, metrics.enginePowerKw * Mathf.Clamp(cruisePowerLever, 0.05f, 1.2f));
        metrics.needWorkforceRecoveryPerHour = stats.Get(ShipStatId.NeedWorkforceRecoveryPerHour, 0f);
        metrics.needHealthRecoveryPerHour = stats.Get(ShipStatId.NeedHealthRecoveryPerHour, 0f);
        metrics.needSafetyRecoveryPerHour = stats.Get(ShipStatId.NeedSafetyRecoveryPerHour, 0f);
        metrics.needComfortRecoveryPerHour = stats.Get(ShipStatId.NeedComfortRecoveryPerHour, 0f);
        metrics.needCreativityRecoveryPerHour = stats.Get(ShipStatId.NeedCreativityRecoveryPerHour, 0f);
        metrics.needRepairRecoveryPerHour = stats.Get(ShipStatId.NeedRepairRecoveryPerHour, 0f);
        metrics.needCapitalConnectionRecoveryPerHour = stats.Get(ShipStatId.NeedCapitalConnectionRecoveryPerHour, 0f);
        metrics.cargoCompartments = CargoStoragePlanner.BuildStatCompartments(stats, definition.cargoCompartments);

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
            error = $"Грузовик перегружен по полезной массе: {currentCargoKg:F0}/{metrics.maxCargoKg:F0} кг.";
            return false;
        }

        return true;
    }

    private LogisticsLegEstimate EstimateLeg(LogisticsShipMetrics metrics, Vector3 from, Vector3 to, float cargoKg, float dockedShipFullMassKg = 0f)
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
        claudiumKg += CargoStoragePlanner.GetDockSupportClaudiumKg(metrics, dockedShipFullMassKg, estimate.durationSeconds);
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

    private static bool TryTopUpTankForLeg(ShipConsumableTankState tank, IslandProductionState storage, string resourceId, int requiredAmount, float capacityKg)
    {
        if (requiredAmount <= 0) return true;
        if (tank == null || storage == null || string.IsNullOrWhiteSpace(resourceId)) return false;
        if (requiredAmount > capacityKg + 0.001f) return false;
        if (tank.GetAmount(resourceId) >= requiredAmount) return true;

        int moved = MoveStorageToTank(storage, tank, resourceId, requiredAmount, capacityKg);
        return moved >= 0 && tank.GetAmount(resourceId) >= requiredAmount;
    }

    private static int MoveStorageToTank(Dictionary<string, int> islandStorage, ShipConsumableTankState tank, string resourceId, int targetAmount, float capacityKg)
    {
        if (islandStorage == null || tank == null || string.IsNullOrWhiteSpace(resourceId) || targetAmount <= 0) return 0;

        float target = Mathf.Min(targetAmount, capacityKg);
        int needed = Mathf.FloorToInt(Mathf.Max(0f, target - tank.GetAmount(resourceId)) + 0.0001f);
        if (needed <= 0) return 0;

        int moved = Mathf.Min(needed, GetAmount(islandStorage, resourceId));
        if (moved <= 0) return 0;

        SetAmount(islandStorage, resourceId, GetAmount(islandStorage, resourceId) - moved);
        return Mathf.RoundToInt(tank.Add(resourceId, moved, capacityKg));
    }

    private static int MoveStorageToTank(IslandProductionState storage, ShipConsumableTankState tank, string resourceId, int targetAmount, float capacityKg)
    {
        if (storage == null || tank == null || string.IsNullOrWhiteSpace(resourceId) || targetAmount <= 0) return 0;

        float target = Mathf.Min(targetAmount, capacityKg);
        int needed = Mathf.FloorToInt(Mathf.Max(0f, target - tank.GetAmount(resourceId)) + 0.0001f);
        if (needed <= 0) return 0;

        int moved = Mathf.Min(needed, storage.GetResourceAmount(resourceId));
        if (moved <= 0 || !storage.TrySpendResource(resourceId, moved)) return 0;

        return Mathf.RoundToInt(tank.Add(resourceId, moved, capacityKg));
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
                error = $"На складе не хватает {order.itemId}: нужно {order.amount} ед.";
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

    private static int ApplyAutomaticPassengerExchange(
        LogisticsShipDefinition definition,
        LogisticsShipState state,
        LogisticsRouteStop stop,
        Dictionary<string, int> shipCargo,
        Dictionary<string, int> islandStorage)
    {
        if (state == null || stop == null || shipCargo == null || islandStorage == null) return 0;

        int moved = 0;
        if (!string.Equals(stop.islandId, "capital", StringComparison.OrdinalIgnoreCase))
        {
            return moved;
        }

        int passengersToCapital = GetAmount(shipCargo, PassengerCargoIds.ToCapitalItemId);
        if (passengersToCapital > 0)
        {
            SetAmount(shipCargo, PassengerCargoIds.ToCapitalItemId, 0);
            moved += passengersToCapital;
        }

        if (ShipboardNeedsSimulator.UsesCapitalConnection(definition))
        {
            string passengerItemId = PassengerCargoIds.ToShipItemId(state.shipId);
            int targetPassengers = ShipboardNeedsSimulator.CalculateCapitalPassengerTargetAmount(definition);
            moved += MoveUpToTarget(islandStorage, shipCargo, passengerItemId, targetPassengers);
        }

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

public static class CargoStoragePlanner
{
    public static Dictionary<string, int> ToCargoMap(List<ResourceStack> cargo)
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        if (cargo == null) return map;

        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            map[stack.resourceId] = map.TryGetValue(stack.resourceId, out int current) ? current + stack.amount : stack.amount;
        }

        return map;
    }

    public static List<CargoCompartmentDefinition> BuildStatCompartments(ShipStatBlock stats, List<CargoCompartmentDefinition> baseCompartments = null)
    {
        List<CargoCompartmentDefinition> compartments = new List<CargoCompartmentDefinition>();
        if (baseCompartments != null)
        {
            for (int i = 0; i < baseCompartments.Count; i++)
            {
                CargoCompartmentDefinition compartment = baseCompartments[i];
                if (compartment == null || compartment.capacity <= 0f) continue;
                compartments.Add(compartment.CloneNormalized());
            }
        }

        AddStatCompartment(compartments, stats, ShipStatId.CargoVanCapacityKg, CargoStorageKind.Van, "Фургон");
        AddStatCompartment(compartments, stats, ShipStatId.PassengerSeatCapacity, CargoStorageKind.Cabin, "Салон");
        AddStatCompartment(compartments, stats, ShipStatId.BulkHoldCapacityKg, CargoStorageKind.Van, "Грузовой кузов");
        AddStatCompartment(compartments, stats, ShipStatId.LiquidTankCapacityKg, CargoStorageKind.Van, "Грузовая цистерна");
        AddStatCompartment(compartments, stats, ShipStatId.GasCylinderCapacityKg, CargoStorageKind.Van, "Грузовые баллоны");

        float dockSlots = stats != null ? stats.Get(ShipStatId.ShipDockSlots, 0f) : 0f;
        if (dockSlots > 0f)
        {
            ShipSizeClass dockClass = (ShipSizeClass)Mathf.RoundToInt(stats.Get(ShipStatId.ShipDockMaxClass, (float)ShipSizeClass.Cruiser));
            compartments.Add(new CargoCompartmentDefinition
            {
                displayName = "Док",
                storageKind = CargoStorageKind.ShipDock,
                capacity = dockSlots,
                maxDockedShipClass = dockClass,
                dockedShipMassFactor = Mathf.Clamp(stats.Get(ShipStatId.DockedShipMassFactor, 0.1f), 0.01f, 1f),
                dockSupportClaudiumPerTonHour = Mathf.Max(0f, stats.Get(ShipStatId.DockSupportClaudiumPerTonHour, 0.02f))
            });
        }

        return compartments;
    }

    private static void AddStatCompartment(
        List<CargoCompartmentDefinition> compartments,
        ShipStatBlock stats,
        ShipStatId stat,
        CargoStorageKind kind,
        string displayName)
    {
        float capacity = stats != null ? stats.Get(stat, 0f) : 0f;
        if (capacity <= 0f) return;

        CargoCompartmentDefinition compartment = new CargoCompartmentDefinition
        {
            displayName = displayName,
            storageKind = kind,
            capacity = capacity
        };

        compartments.Add(compartment);
    }

    public static float GetCargoMassKg(WorldConfigDatabase config, List<ResourceStack> cargo)
    {
        if (cargo == null) return 0f;

        float total = 0f;
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            total += config != null ? config.GetItemTransportMassKg(stack.resourceId, stack.amount) : stack.amount;
        }

        return total;
    }

    public static float GetCargoMassKg(WorldConfigDatabase config, Dictionary<string, int> cargo)
    {
        if (cargo == null) return 0f;

        float total = 0f;
        foreach (KeyValuePair<string, int> pair in cargo)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) continue;
            total += config != null ? config.GetItemTransportMassKg(pair.Key, pair.Value) : pair.Value;
        }

        return total;
    }

    public static float GetDockedShipFullMassKg(WorldConfigDatabase config, Dictionary<string, int> cargo)
    {
        if (config == null || cargo == null) return 0f;

        float total = 0f;
        foreach (KeyValuePair<string, int> pair in cargo)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) continue;
            if (config.GetItemStorageKind(pair.Key) != CargoStorageKind.ShipDock) continue;
            total += config.GetItemFullMassKg(pair.Key, pair.Value);
        }

        return total;
    }

    public static float GetDockSupportClaudiumKg(LogisticsShipMetrics metrics, float dockedShipFullMassKg, float durationSeconds)
    {
        if (dockedShipFullMassKg <= 0f || durationSeconds <= 0f) return 0f;

        float supportPerTonHour = 0f;
        if (metrics.cargoCompartments != null)
        {
            for (int i = 0; i < metrics.cargoCompartments.Count; i++)
            {
                CargoCompartmentDefinition compartment = metrics.cargoCompartments[i];
                if (compartment == null || compartment.storageKind != CargoStorageKind.ShipDock) continue;
                supportPerTonHour = Mathf.Max(supportPerTonHour, compartment.dockSupportClaudiumPerTonHour);
            }
        }

        return Mathf.Max(0f, dockedShipFullMassKg / 1000f) * supportPerTonHour * Mathf.Max(0f, durationSeconds / 3600f);
    }

    public static bool TryValidateCargoStorage(
        WorldConfigDatabase config,
        List<CargoCompartmentDefinition> compartments,
        List<ResourceStack> cargo,
        out string error)
    {
        return TryValidateCargoStorage(config, compartments, ToCargoMap(cargo), out error);
    }

    public static bool TryValidateCargoStorage(
        WorldConfigDatabase config,
        List<CargoCompartmentDefinition> compartments,
        Dictionary<string, int> cargo,
        out string error)
    {
        LogisticsShipMetrics metrics = new LogisticsShipMetrics { cargoCompartments = compartments };
        return TryValidateCargoStorage(config, metrics, cargo, out error);
    }

    public static bool TryValidateCargoStorage(
        WorldConfigDatabase config,
        LogisticsShipMetrics metrics,
        Dictionary<string, int> cargo,
        out string error)
    {
        error = "";
        if (cargo == null || cargo.Count == 0) return true;
        if (metrics.cargoCompartments == null || metrics.cargoCompartments.Count == 0) return true;

        float generalCargoMassKg = 0f;
        float cabinDemand = 0f;
        Dictionary<string, int> dockDemands = new Dictionary<string, int>();

        foreach (KeyValuePair<string, int> pair in cargo)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) continue;

            CargoStorageKind storageKind = config != null ? config.GetItemStorageKind(pair.Key) : CargoStorageKind.Van;
            switch (storageKind)
            {
                case CargoStorageKind.Cabin:
                    cabinDemand += Mathf.Max(0, pair.Value);
                    break;
                case CargoStorageKind.ShipDock:
                    dockDemands[pair.Key] = Mathf.Max(0, pair.Value);
                    break;
                default:
                    generalCargoMassKg += config != null ? config.GetItemTransportMassKg(pair.Key, pair.Value) : Mathf.Max(0, pair.Value);
                    break;
            }
        }

        float generalCargoCapacityKg = GetGeneralCargoCapacityKg(metrics);
        if (generalCargoMassKg > generalCargoCapacityKg + 0.001f)
        {
            error = $"Грузовые отсеки перегружены по массе: {generalCargoMassKg:F0}/{generalCargoCapacityKg:F0} кг.";
            return false;
        }

        if (cabinDemand > GetMixedCapacity(metrics, CargoStorageKind.Cabin) + 0.001f)
        {
            error = $"Салон перегружен: {cabinDemand:F0}/{GetMixedCapacity(metrics, CargoStorageKind.Cabin):F0} мест.";
            return false;
        }

        if (!TryFitDockSlots(config, metrics, dockDemands, out error)) return false;

        return true;
    }

    private static float GetMixedCapacity(LogisticsShipMetrics metrics, CargoStorageKind kind)
    {
        float capacity = 0f;
        if (metrics.cargoCompartments == null) return capacity;

        for (int i = 0; i < metrics.cargoCompartments.Count; i++)
        {
            CargoCompartmentDefinition compartment = metrics.cargoCompartments[i];
            if (compartment == null || compartment.storageKind != kind) continue;
            capacity += Mathf.Max(0f, compartment.capacity);
        }

        return capacity;
    }

    private static float GetGeneralCargoCapacityKg(LogisticsShipMetrics metrics)
    {
        float capacity = 0f;
        if (metrics.cargoCompartments == null) return capacity;

        for (int i = 0; i < metrics.cargoCompartments.Count; i++)
        {
            CargoCompartmentDefinition compartment = metrics.cargoCompartments[i];
            if (compartment == null || compartment.capacity <= 0f) continue;
            if (compartment.storageKind == CargoStorageKind.Cabin || compartment.storageKind == CargoStorageKind.ShipDock) continue;
            capacity += Mathf.Max(0f, compartment.capacity);
        }

        return capacity;
    }

    private static bool TryFitDockSlots(
        WorldConfigDatabase config,
        LogisticsShipMetrics metrics,
        Dictionary<string, int> dockDemands,
        out string error)
    {
        error = "";
        if (dockDemands == null || dockDemands.Count == 0) return true;

        List<ShipSizeClass> slots = new List<ShipSizeClass>();
        if (metrics.cargoCompartments != null)
        {
            for (int i = 0; i < metrics.cargoCompartments.Count; i++)
            {
                CargoCompartmentDefinition compartment = metrics.cargoCompartments[i];
                if (compartment == null || compartment.storageKind != CargoStorageKind.ShipDock || compartment.capacity <= 0f) continue;

                int count = Mathf.FloorToInt(compartment.capacity + 0.001f);
                ShipSizeClass maxClass = compartment.maxDockedShipClass == ShipSizeClass.None ? ShipSizeClass.Cruiser : compartment.maxDockedShipClass;
                for (int slot = 0; slot < count; slot++)
                {
                    slots.Add(maxClass);
                }
            }
        }

        slots.Sort((a, b) => ((int)a).CompareTo((int)b));
        foreach (KeyValuePair<string, int> pair in dockDemands)
        {
            ShipSizeClass shipClass = config != null ? config.GetItemShipSizeClass(pair.Key) : ShipSizeClass.None;
            if (shipClass == ShipSizeClass.None) shipClass = ShipSizeClass.Boat;

            for (int unit = 0; unit < pair.Value; unit++)
            {
                int slotIndex = FindDockSlot(slots, shipClass);
                if (slotIndex < 0)
                {
                    error = $"Док: нет свободного слота класса {shipClass} для {pair.Key}.";
                    return false;
                }

                slots.RemoveAt(slotIndex);
            }
        }

        return true;
    }

    private static int FindDockSlot(List<ShipSizeClass> slots, ShipSizeClass shipClass)
    {
        if (slots == null) return -1;

        for (int i = 0; i < slots.Count; i++)
        {
            if ((int)slots[i] >= (int)shipClass)
            {
                return i;
            }
        }

        return -1;
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
    public int workforceNeedLoad;
    public int healthNeedLoad;
    public int safetyNeedLoad;
    public int comfortNeedLoad;
    public int creativityNeedLoad;
    public bool repairNeedEnabled;
    public bool capitalConnectionEnabled;
    public int crewCapacity = 1;
    public int passengerCapacity;
    public int repairNeedLoad = 1;
    public int capitalConnectionNeedLoad = 1;
    public List<CargoCompartmentDefinition> cargoCompartments = new List<CargoCompartmentDefinition>();
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
    public ShipConsumableTankState engineFuelTank = new ShipConsumableTankState();
    public ShipConsumableTankState claudiumTank = new ShipConsumableTankState { resourceId = "claudium" };
    public List<ShipboardNeedState> societyNeeds = new List<ShipboardNeedState>();
    public float passengersToCapitalProgress;
    public float passengersFromCapitalProgress;
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
        engineFuelTank ??= new ShipConsumableTankState();
        claudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        societyNeeds ??= new List<ShipboardNeedState>();
        passengersToCapitalProgress = Mathf.Max(0f, passengersToCapitalProgress);
        passengersFromCapitalProgress = Mathf.Max(0f, passengersFromCapitalProgress);
        lastError ??= "";
        engineFuelTank.Normalize();
        claudiumTank.Normalize();

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

        for (int i = societyNeeds.Count - 1; i >= 0; i--)
        {
            ShipboardNeedState need = societyNeeds[i];
            if (need == null || string.IsNullOrWhiteSpace(need.needId))
            {
                societyNeeds.RemoveAt(i);
                continue;
            }

            need.Normalize();
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

    public float GetCargoMassKg(WorldConfigDatabase config)
    {
        return CargoStoragePlanner.GetCargoMassKg(config, cargo);
    }

    public float GetPayloadMassKg(WorldConfigDatabase config)
    {
        return GetCargoMassKg(config) + engineFuelTank.amountKg + claudiumTank.amountKg;
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

    public ShipboardNeedState GetSocietyNeedState(string needId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(needId)) return null;
        societyNeeds ??= new List<ShipboardNeedState>();

        for (int i = 0; i < societyNeeds.Count; i++)
        {
            ShipboardNeedState state = societyNeeds[i];
            if (state != null && state.needId == needId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        ShipboardNeedState newState = new ShipboardNeedState { needId = needId };
        societyNeeds.Add(newState);
        return newState;
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

[Serializable]
public class ShipboardNeedState
{
    public string needId = "";
    public float currentValue;
    public float recoveryCapacityProgress;
    public float recoveryItemProgress;
    public bool initialized;

    public void Normalize()
    {
        needId ??= "";
        currentValue = Mathf.Max(0f, currentValue);
        recoveryCapacityProgress = Mathf.Max(0f, recoveryCapacityProgress);
        recoveryItemProgress = Mathf.Max(0f, recoveryItemProgress);
    }
}

public static class ShipboardNeedsSimulator
{
    public static int AdvanceLogisticsShip(
        WorldConfigDatabase config,
        LogisticsShipDefinition definition,
        LogisticsShipState state,
        LogisticsShipMetrics metrics,
        IslandProductionState capitalStorage,
        float deltaMinutes)
    {
        if (config == null || !config.isLoaded || definition == null || state == null || deltaMinutes <= 0f) return 0;

        state.Normalize();
        int changed = 0;
        float deltaHours = Mathf.Max(0f, deltaMinutes / 60f);

        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null || string.IsNullOrWhiteSpace(need.id)) continue;

            int load = GetNeedLoad(definition, need.kind);
            float serviceRecoveryPerHour = GetModuleRecoveryPerHour(metrics, need.kind);
            bool activeNeed = load > 0 || serviceRecoveryPerHour > 0f;
            if (need.kind == IslandNeedKind.CapitalConnection)
            {
                activeNeed = UsesCapitalConnection(definition);
            }

            if (!activeNeed) continue;

            string recoveryItemId = ResolveShipRecoveryItemId(need, state.shipId);
            changed += AdvanceNeed(state, need, load, serviceRecoveryPerHour, deltaHours, recoveryItemId);
        }

        if (UsesCapitalConnection(definition))
        {
            float passengerRate = CalculatePassengerRatePerHour(definition);
            changed += GeneratePassengerCargo(state, PassengerCargoIds.ToCapitalItemId, ref state.passengersToCapitalProgress, passengerRate * deltaHours);

            if (capitalStorage != null)
            {
                changed += GeneratePassengerCargo(capitalStorage, PassengerCargoIds.ToShipItemId(state.shipId), ref state.passengersFromCapitalProgress, passengerRate * 0.8f * deltaHours);
            }
        }

        return changed;
    }

    public static bool UsesCapitalConnection(LogisticsShipDefinition definition)
    {
        return definition != null && (definition.capitalConnectionEnabled || definition.passengerCapacity > 0 || definition.crewCapacity >= 40);
    }

    public static int CalculateCapitalPassengerTargetAmount(LogisticsShipDefinition definition)
    {
        if (definition == null) return 1;

        int people = Mathf.Max(1, definition.crewCapacity + definition.passengerCapacity);
        return Mathf.Clamp(Mathf.CeilToInt(people / 20f), 1, 20);
    }

    private static int AdvanceNeed(LogisticsShipState state, IslandSocialNeedConfig need, int load, float moduleRecoveryPerHour, float deltaHours, string recoveryItemId)
    {
        if (state == null || need == null || string.IsNullOrWhiteSpace(recoveryItemId)) return 0;

        ShipboardNeedState needState = state.GetSocietyNeedState(need.id, true);
        InitializeNeedState(needState, need);

        float decay = (need.baseDecayPerHour + Mathf.Max(0, load) * need.loadDecayPerHour) * deltaHours;
        if (decay > 0f)
        {
            needState.currentValue = Mathf.Max(0f, needState.currentValue - decay);
        }

        float deficit = Mathf.Max(0f, need.maxValue - needState.currentValue);
        if (deficit <= 0.001f) return 0;

        float recoveryPerHour = IslandSocietySimulator.GetBaseNeedRecoveryPointsPerHour(need) + Mathf.Max(0f, moduleRecoveryPerHour);
        needState.recoveryCapacityProgress += Mathf.Max(0f, recoveryPerHour) * Mathf.Max(0f, deltaHours);
        if (needState.recoveryCapacityProgress <= 0.001f && needState.recoveryItemProgress <= 0.001f) return 0;

        int spentItems = 0;
        int guard = 0;
        while (deficit > 0.001f && needState.recoveryCapacityProgress > 0.001f && guard < 1000)
        {
            guard++;
            if (needState.recoveryItemProgress <= 0.001f)
            {
                if (!state.TrySpendCargo(recoveryItemId, 1)) break;

                needState.recoveryItemProgress += Mathf.Max(1f, need.restorePerItem);
                spentItems++;
            }

            float restored = Mathf.Min(deficit, needState.recoveryCapacityProgress, needState.recoveryItemProgress);
            if (restored <= 0.001f) break;

            needState.currentValue = Mathf.Min(need.maxValue, needState.currentValue + restored);
            needState.recoveryCapacityProgress = Mathf.Max(0f, needState.recoveryCapacityProgress - restored);
            needState.recoveryItemProgress = Mathf.Max(0f, needState.recoveryItemProgress - restored);
            deficit = Mathf.Max(0f, need.maxValue - needState.currentValue);
        }

        return spentItems;
    }

    private static int GetNeedLoad(LogisticsShipDefinition definition, IslandNeedKind kind)
    {
        if (definition == null) return 0;

        switch (kind)
        {
            case IslandNeedKind.Workforce:
                return Mathf.Max(0, definition.workforceNeedLoad);
            case IslandNeedKind.Health:
                return Mathf.Max(0, definition.healthNeedLoad);
            case IslandNeedKind.Safety:
                return Mathf.Max(0, definition.safetyNeedLoad);
            case IslandNeedKind.Comfort:
                return Mathf.Max(0, definition.comfortNeedLoad);
            case IslandNeedKind.Creativity:
                return Mathf.Max(0, definition.creativityNeedLoad);
            case IslandNeedKind.Repair:
                return definition.repairNeedEnabled ? Mathf.Max(0, definition.repairNeedLoad) : 0;
            case IslandNeedKind.CapitalConnection:
                return UsesCapitalConnection(definition) ? Mathf.Max(0, definition.capitalConnectionNeedLoad) : 0;
            default:
                return 0;
        }
    }

    private static float GetModuleRecoveryPerHour(LogisticsShipMetrics metrics, IslandNeedKind kind)
    {
        switch (kind)
        {
            case IslandNeedKind.Workforce:
                return metrics.needWorkforceRecoveryPerHour;
            case IslandNeedKind.Health:
                return metrics.needHealthRecoveryPerHour;
            case IslandNeedKind.Safety:
                return metrics.needSafetyRecoveryPerHour;
            case IslandNeedKind.Comfort:
                return metrics.needComfortRecoveryPerHour;
            case IslandNeedKind.Creativity:
                return metrics.needCreativityRecoveryPerHour;
            case IslandNeedKind.Repair:
                return metrics.needRepairRecoveryPerHour;
            case IslandNeedKind.CapitalConnection:
                return metrics.needCapitalConnectionRecoveryPerHour;
            default:
                return 0f;
        }
    }

    private static string ResolveShipRecoveryItemId(IslandSocialNeedConfig need, string shipId)
    {
        if (need == null) return "";
        if (need.kind == IslandNeedKind.CapitalConnection)
        {
            return PassengerCargoIds.ToShipItemId(shipId);
        }

        return need.recoveryItemId ?? "";
    }

    private static void InitializeNeedState(ShipboardNeedState state, IslandSocialNeedConfig need)
    {
        if (state == null || need == null || state.initialized) return;

        state.currentValue = need.maxValue;
        state.initialized = true;
    }

    private static IslandSocialNeedConfig FindNeed(WorldConfigDatabase config, IslandNeedKind kind)
    {
        if (config == null) return null;
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need != null && need.kind == kind)
            {
                return need;
            }
        }

        return null;
    }

    private static float CalculatePassengerRatePerHour(LogisticsShipDefinition definition)
    {
        if (definition == null) return 0f;

        int people = Mathf.Max(0, definition.crewCapacity) + Mathf.Max(0, definition.passengerCapacity);
        return Mathf.Clamp(0.15f + people * 0.015f, 0.25f, 24f);
    }

    private static int GeneratePassengerCargo(LogisticsShipState state, string itemId, ref float passengerProgress, float passengerCount)
    {
        if (state == null || string.IsNullOrWhiteSpace(itemId) || passengerCount <= 0f) return 0;

        passengerProgress = Mathf.Max(0f, passengerProgress + passengerCount);
        int wholePassengers = Mathf.FloorToInt(passengerProgress);
        if (wholePassengers <= 0) return 0;

        passengerProgress -= wholePassengers;
        state.AddCargo(itemId, wholePassengers);
        return wholePassengers;
    }

    private static int GeneratePassengerCargo(IslandProductionState storage, string itemId, ref float passengerProgress, float passengerCount)
    {
        if (storage == null || string.IsNullOrWhiteSpace(itemId) || passengerCount <= 0f) return 0;

        passengerProgress = Mathf.Max(0f, passengerProgress + passengerCount);
        int wholePassengers = Mathf.FloorToInt(passengerProgress);
        if (wholePassengers <= 0) return 0;

        passengerProgress -= wholePassengers;
        storage.AddResource(itemId, wholePassengers);
        return wholePassengers;
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
    public float fuelTankCapacityKg;
    public float claudiumTankCapacityKg;
    public float cruiseSpeedMS;
    public float cruisePowerKw;
    public float needWorkforceRecoveryPerHour;
    public float needHealthRecoveryPerHour;
    public float needSafetyRecoveryPerHour;
    public float needComfortRecoveryPerHour;
    public float needCreativityRecoveryPerHour;
    public float needRepairRecoveryPerHour;
    public float needCapitalConnectionRecoveryPerHour;
    public List<CargoCompartmentDefinition> cargoCompartments;
}

public struct LogisticsLegEstimate
{
    public bool canFly;
    public float durationSeconds;
    public int requiredFuelKg;
    public int requiredClaudiumKg;
    public string reason;
}
