using System;
using System.Collections.Generic;
using UnityEngine;

public class GasHarvesterFleetController : MonoBehaviour
{
    [InspectorName("Meta game")]
    public MetaGameState metaGameState;

    [InspectorName("Simulation enabled")]
    public bool simulationEnabled = true;

    [InspectorName("Fuel and claudium reserve")]
    [Tooltip("1.2 means the ship keeps a 20% return reserve.")]
    public float reserveMultiplier = 1.2f;

    [InspectorName("Cruise speed share")]
    [Range(0.1f, 1f)] public float cruiseSpeedFactor = 0.75f;

    [InspectorName("Cruise power lever")]
    [Range(0.05f, 1.2f)] public float cruisePowerLever = 0.7f;

    [InspectorName("Default claudium resource")]
    public string defaultClaudiumResourceId = "claudium";

    [InspectorName("Unload seconds per kg")]
    public float unloadSecondsPerKg = 0.5f;

    [Header("Debug")]
    public bool debugLogging;
    public bool debugStockAllIslandFuelAndClaudium = true;
    public int debugMinIslandWoodKg = 500;
    public int debugMinIslandCharcoalKg = 500;
    public int debugMinIslandClaudiumKg = 250;

    [InspectorName("Gas harvesters")]
    public List<GasHarvesterShipDefinition> ships = new List<GasHarvesterShipDefinition>();

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
        ships ??= new List<GasHarvesterShipDefinition>();
        if (ships.Count > 0) return;

        ships.Add(new GasHarvesterShipDefinition
        {
            shipId = "gas_harvester_01",
            displayName = "Gas Harvester 01",
            homeIslandId = "capital",
            searchRadiusMeters = 1200f,
            targetCargoKg = 35,
            hullId = "starter_hull",
            autoInstallRequiredModules = true,
            installedModules = new List<InstalledModuleState>
            {
                new InstalledModuleState { slotId = "utility_01", moduleId = "starter_gas_harvester" }
            }
        });
    }

    public void EnsureRuntimeShips(PlayerProgress progress)
    {
        if (progress == null) return;
        progress.Normalize();
        if (ships == null) return;

        for (int i = 0; i < ships.Count; i++)
        {
            GasHarvesterShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            GasHarvesterShipState state = progress.GetGasHarvesterShipState(definition.shipId, false);
            if (state == null)
            {
                state = progress.GetGasHarvesterShipState(definition.shipId, true);
                InitializeStateFromDefinition(state, definition);
                LogEvent(state, "created at " + state.homeIslandId);
            }

            state.displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.shipId : definition.displayName;
            state.homeIslandId = string.IsNullOrWhiteSpace(definition.homeIslandId) ? "capital" : definition.homeIslandId;
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
        if (debugStockAllIslandFuelAndClaudium)
        {
            changed += DebugStockAllIslandFuelAndClaudium(config, progress);
        }

        if (ships == null) return changed;

        for (int i = 0; i < ships.Count; i++)
        {
            GasHarvesterShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            GasHarvesterShipState state = progress.GetGasHarvesterShipState(definition.shipId, true);
            changed += AdvanceShip(definition, state, config, progress, catalog, techTree, fromUtcTicks, toUtcTicks);
        }

        return changed;
    }

    public int DebugStockAllIslandFuelAndClaudium(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || !config.isLoaded || progress == null) return 0;

        int changed = 0;
        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState storage = progress.GetIslandProductionState(island.id, true);
            changed += SetStorageAtLeast(storage, "wood", debugMinIslandWoodKg);
            changed += SetStorageAtLeast(storage, "charcoal", debugMinIslandCharcoalKg);
            changed += SetStorageAtLeast(storage, ResolveClaudiumResourceId(null), debugMinIslandClaudiumKg);
        }

        return changed;
    }

    private int AdvanceShip(
        GasHarvesterShipDefinition definition,
        GasHarvesterShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        long fromUtcTicks,
        long toUtcTicks)
    {
        if (state == null) return 0;

        GasHarvesterShipMetrics metrics;
        if (!TryBuildMetrics(definition, progress, catalog, techTree, config, state.GetCargoMassKg(), out metrics, out string metricsError))
        {
            SetError(state, metricsError);
            return 0;
        }

        EnsureShipHasHome(state, definition, config);

        int changed = 0;
        long cursorTicks = fromUtcTicks;
        int guard = 0;

        while (cursorTicks <= toUtcTicks && guard < 50000)
        {
            guard++;
            state.Normalize();

            if (state.status == GasHarvesterShipStatus.FlyingToCloud)
            {
                if (state.flightArrivesUtcTicks <= 0)
                {
                    SetError(state, "Gas harvester flight has no arrival time.");
                    break;
                }

                if (toUtcTicks < state.flightArrivesUtcTicks)
                {
                    UpdateFlightPositionToCloud(state, config, toUtcTicks);
                    break;
                }

                cursorTicks = Math.Max(cursorTicks, state.flightArrivesUtcTicks);
                CompleteOutboundFlight(state, config, cursorTicks, metrics);
                changed++;
                continue;
            }

            if (state.status == GasHarvesterShipStatus.Harvesting)
            {
                if (state.nextEventUtcTicks <= 0)
                {
                    state.nextEventUtcTicks = cursorTicks + TimeSpan.FromSeconds(Mathf.Max(0.1f, metrics.harvesterCycleSeconds)).Ticks;
                }

                if (toUtcTicks < state.nextEventUtcTicks)
                {
                    break;
                }

                cursorTicks = Math.Max(cursorTicks, state.nextEventUtcTicks);
                if (!TryCompleteHarvestCycle(definition, state, config, progress, metrics, cursorTicks))
                {
                    break;
                }

                changed++;
                continue;
            }

            if (state.status == GasHarvesterShipStatus.Returning)
            {
                if (state.flightArrivesUtcTicks <= 0)
                {
                    SetError(state, "Gas harvester return flight has no arrival time.");
                    break;
                }

                if (toUtcTicks < state.flightArrivesUtcTicks)
                {
                    UpdateReturnFlightPosition(state, config, toUtcTicks);
                    break;
                }

                cursorTicks = Math.Max(cursorTicks, state.flightArrivesUtcTicks);
                CompleteReturnFlight(state, config, progress, cursorTicks);
                changed++;
                continue;
            }

            if (state.status == GasHarvesterShipStatus.Unloading)
            {
                if (state.nextEventUtcTicks <= 0 || toUtcTicks < state.nextEventUtcTicks)
                {
                    break;
                }

                cursorTicks = Math.Max(cursorTicks, state.nextEventUtcTicks);
                state.status = GasHarvesterShipStatus.Idle;
                state.nextEventUtcTicks = 0;
                state.completedTrips++;
                state.lastError = "";
                LogEvent(state, "unload finished at " + state.homeIslandId + ", trips=" + state.completedTrips);
                changed++;
                continue;
            }

            if (state.status == GasHarvesterShipStatus.Error)
            {
                break;
            }

            if (!TryStartHarvestTrip(definition, state, config, progress, metrics, cursorTicks, out string startError))
            {
                SetWaiting(state, startError);
                break;
            }

            changed++;
        }

        if (guard >= 50000)
        {
            SetWaiting(state, "Gas harvester time step stopped by safety guard.");
        }

        return changed;
    }

    private bool TryStartHarvestTrip(
        GasHarvesterShipDefinition definition,
        GasHarvesterShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        GasHarvesterShipMetrics metrics,
        long currentTicks,
        out string error)
    {
        error = "";

        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null)
        {
            error = "Home island not found: " + state.homeIslandId;
            return false;
        }

        GasCloudConfig targetCloud = FindTargetCloud(definition, state, config, progress, home);
        if (targetCloud == null)
        {
            error = "No gas cloud with remaining concentrate inside search radius.";
            return false;
        }

        float freeCargoKg = metrics.maxCargoKg - state.GetCargoMassKg();
        if (freeCargoKg < 1f)
        {
            error = "No free cargo space for concentrate.";
            return false;
        }

        GasCloudTypeConfig targetCloudType = config.GetGasCloudType(targetCloud.cloudTypeId);
        if (targetCloudType == null)
        {
            error = "Target cloud type not found: " + targetCloud.cloudTypeId;
            return false;
        }

        float cloudRemainingKg = GetCloudRemainingLiters(progress, config, targetCloud.id);
        float plannedHarvestKg = Mathf.Min(Mathf.Max(1f, definition.targetCargoKg), Mathf.Min(freeCargoKg, cloudRemainingKg));
        if (plannedHarvestKg < 1f)
        {
            error = "Target cloud does not have enough whole kilograms to harvest.";
            return false;
        }

        GasHarvesterLegEstimate outbound = EstimateLeg(metrics, home.position, targetCloud.position, state.GetCargoMassKg());
        GasHarvesterLegEstimate ret = EstimateLeg(metrics, targetCloud.position, home.position, state.GetCargoMassKg() + plannedHarvestKg);
        if (!TryEstimateHarvestWorkBudget(metrics, targetCloudType, plannedHarvestKg, state.GetCargoMassKg(), out GasHarvesterWorkEstimate work, out string workError))
        {
            error = workError;
            return false;
        }

        if (!outbound.canFly)
        {
            error = outbound.reason;
            return false;
        }

        if (!ret.canFly)
        {
            error = ret.reason;
            return false;
        }

        IslandProductionState storage = progress.GetIslandProductionState(home.id, true);
        int requiredFuelKg = outbound.requiredFuelKg + ret.requiredFuelKg + work.requiredFuelKg;
        int requiredClaudiumKg = outbound.requiredClaudiumKg + ret.requiredClaudiumKg + work.requiredClaudiumKg;

        if (!TryTopUpForLeg(state, storage, metrics.engineFuelId, requiredFuelKg))
        {
            error = $"Not enough fuel {metrics.engineFuelId}: need {requiredFuelKg} kg.";
            return false;
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition);
        if (!TryTopUpForLeg(state, storage, claudiumResourceId, requiredClaudiumKg))
        {
            error = $"Not enough claudium: need {requiredClaudiumKg} kg.";
            return false;
        }

        if (state.GetCargoMassKg() > metrics.maxCargoKg + 0.001f)
        {
            error = $"Cargo after reserve loading is {state.GetCargoMassKg():F0}/{metrics.maxCargoKg:F0} kg.";
            return false;
        }

        if (!state.TrySpendCargo(metrics.engineFuelId, outbound.requiredFuelKg))
        {
            error = "Could not spend outbound fuel.";
            return false;
        }

        if (!state.TrySpendCargo(claudiumResourceId, outbound.requiredClaudiumKg))
        {
            error = "Could not spend outbound claudium.";
            return false;
        }

        state.status = GasHarvesterShipStatus.FlyingToCloud;
        state.targetCloudId = targetCloud.id;
        state.flightStartedUtcTicks = currentTicks;
        state.flightArrivesUtcTicks = currentTicks + TimeSpan.FromSeconds(outbound.durationSeconds).Ticks;
        state.nextEventUtcTicks = state.flightArrivesUtcTicks;
        state.lastKnownPosition = home.position;
        state.lastError = "Flying to gas cloud: " + targetCloud.id;
        UpdateFlightPositionToCloud(state, config, currentTicks);
        LogEvent(state, $"flight started {home.id}->{targetCloud.id}, eta={outbound.durationSeconds:F1}s, return fuel={ret.requiredFuelKg}, return claudium={ret.requiredClaudiumKg}, work fuel={work.requiredFuelKg}, work claudium={work.requiredClaudiumKg}, cycles~{work.estimatedCycles}, cargo={FormatCargo(state.cargo)}");
        return true;
    }

    private void CompleteOutboundFlight(GasHarvesterShipState state, WorldConfigDatabase config, long currentTicks, GasHarvesterShipMetrics metrics)
    {
        GasCloudConfig cloud = config.GetGasCloud(state.targetCloudId);
        if (cloud != null)
        {
            state.lastKnownPosition = cloud.position;
        }

        state.status = GasHarvesterShipStatus.Harvesting;
        state.flightStartedUtcTicks = 0;
        state.flightArrivesUtcTicks = 0;
        state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(Mathf.Max(0.1f, metrics.harvesterCycleSeconds)).Ticks;
        state.lastError = "Harvesting cloud: " + state.targetCloudId;
        LogEvent(state, "arrived at cloud " + state.targetCloudId);
    }

    private bool TryCompleteHarvestCycle(
        GasHarvesterShipDefinition definition,
        GasHarvesterShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        GasHarvesterShipMetrics metrics,
        long currentTicks)
    {
        GasCloudConfig cloud = config.GetGasCloud(state.targetCloudId);
        if (cloud == null)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Target cloud disappeared.");
        }

        GasCloudTypeConfig cloudType = config.GetGasCloudType(cloud.cloudTypeId);
        if (cloudType == null || string.IsNullOrWhiteSpace(cloudType.condensateItemId))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Target cloud has no condensate item.");
        }

        float remainingLiters = GetCloudRemainingLiters(progress, config, cloud.id);
        if (remainingLiters <= 0.001f)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Cloud depleted.");
        }

        if (GetHarvestPayloadKg(state, metrics, definition) >= Mathf.Max(1f, definition.targetCargoKg))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Target cargo reached.");
        }

        float cycleSeconds = Mathf.Max(0.1f, metrics.harvesterCycleSeconds);
        float sampledM3 = metrics.harvesterVolumeM3PerSecond * cycleSeconds;
        float possibleLiters = sampledM3 * Mathf.Max(0.0001f, cloudType.condensateLitersPerCubicMeter);
        int freeWholeKg = Mathf.FloorToInt(metrics.maxCargoKg - state.GetCargoMassKg() + 0.0001f);
        int possibleWholeKg = Mathf.FloorToInt(state.harvestBufferKg + possibleLiters + 0.0001f);
        if (freeWholeKg <= 0 || possibleWholeKg > freeWholeKg)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Cargo full.");
        }

        if (!TryEstimateHarvestCycle(metrics, state.GetCargoMassKg(), cycleSeconds, out float fuelKg, out float claudiumKg, out string powerError))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, powerError);
        }

        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null)
        {
            SetError(state, "Cannot reserve return: home island not found.");
            return false;
        }

        GasHarvesterLegEstimate ret = EstimateLeg(metrics, cloud.position, home.position, state.GetCargoMassKg() + possibleWholeKg);
        if (!ret.canFly)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, ret.reason);
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition);
        float fuelAfterCycle = state.GetCargoAmount(metrics.engineFuelId) - state.pendingFuelConsumptionKg - fuelKg;
        float claudiumAfterCycle = state.GetCargoAmount(claudiumResourceId) - state.pendingClaudiumConsumptionKg - claudiumKg;
        if (fuelAfterCycle + 0.001f < ret.requiredFuelKg || claudiumAfterCycle + 0.001f < ret.requiredClaudiumKg)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Return reserve reached.");
        }

        AccumulateConsumableSpend(state, metrics.engineFuelId, fuelKg, ref state.pendingFuelConsumptionKg);
        AccumulateConsumableSpend(state, claudiumResourceId, claudiumKg, ref state.pendingClaudiumConsumptionKg);

        float harvestedLiters = HarvestCloudLiters(progress, config, cloud.id, sampledM3);
        if (harvestedLiters <= 0.001f)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Cloud depleted.");
        }

        state.harvestBufferKg += harvestedLiters;
        int wholeKg = Mathf.FloorToInt(state.harvestBufferKg + 0.0001f);
        if (wholeKg > 0)
        {
            state.AddCargo(cloudType.condensateItemId, wholeKg);
            state.harvestBufferKg -= wholeKg;
        }

        float newRemainingLiters = GetCloudRemainingLiters(progress, config, cloud.id);
        state.lastKnownPosition = cloud.position;
        state.lastError = $"Harvested {wholeKg} kg {cloudType.condensateItemId}, cloud left {newRemainingLiters:F1} kg.";
        if (wholeKg > 0 || newRemainingLiters <= 0.001f)
        {
            LogEvent(state, $"cycle at {cloud.id}: +{wholeKg}kg, buffer={state.harvestBufferKg:F2}, left={newRemainingLiters:F1}, cargo={FormatCargo(state.cargo)}");
        }

        if (newRemainingLiters <= 0.001f || state.GetCargoMassKg() >= metrics.maxCargoKg - 0.001f || GetHarvestPayloadKg(state, metrics, definition) >= Mathf.Max(1f, definition.targetCargoKg))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Harvest run finished.");
        }

        state.status = GasHarvesterShipStatus.Harvesting;
        state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(cycleSeconds).Ticks;
        return true;
    }

    private bool TryBeginReturn(
        GasHarvesterShipDefinition definition,
        GasHarvesterShipState state,
        WorldConfigDatabase config,
        GasHarvesterShipMetrics metrics,
        long currentTicks,
        string reason)
    {
        GasCloudConfig cloud = config.GetGasCloud(state.targetCloudId);
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (cloud == null || home == null)
        {
            SetError(state, "Cannot return: cloud or home island not found.");
            return false;
        }

        GasHarvesterLegEstimate ret = EstimateLeg(metrics, cloud.position, home.position, state.GetCargoMassKg());
        if (!ret.canFly)
        {
            SetError(state, ret.reason);
            return false;
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition);
        if (!state.TrySpendCargo(metrics.engineFuelId, ret.requiredFuelKg))
        {
            SetError(state, "Not enough reserved fuel to return.");
            return false;
        }

        if (!state.TrySpendCargo(claudiumResourceId, ret.requiredClaudiumKg))
        {
            SetError(state, "Not enough reserved claudium to return.");
            return false;
        }

        state.status = GasHarvesterShipStatus.Returning;
        state.flightStartedUtcTicks = currentTicks;
        state.flightArrivesUtcTicks = currentTicks + TimeSpan.FromSeconds(ret.durationSeconds).Ticks;
        state.nextEventUtcTicks = state.flightArrivesUtcTicks;
        state.lastKnownPosition = cloud.position;
        state.lastError = string.IsNullOrWhiteSpace(reason) ? "Returning home." : reason;
        UpdateReturnFlightPosition(state, config, currentTicks);
        LogEvent(state, $"return started {cloud.id}->{home.id}, eta={ret.durationSeconds:F1}s, fuel={ret.requiredFuelKg}, claudium={ret.requiredClaudiumKg}, reason={reason}, cargo={FormatCargo(state.cargo)}");
        return true;
    }

    private void CompleteReturnFlight(GasHarvesterShipState state, WorldConfigDatabase config, PlayerProgress progress, long currentTicks)
    {
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home != null)
        {
            state.lastKnownPosition = home.position;
        }

        IslandProductionState storage = progress.GetIslandProductionState(state.homeIslandId, true);
        int moved = UnloadHarvestPayload(state, storage);

        state.targetCloudId = "";
        state.flightStartedUtcTicks = 0;
        state.flightArrivesUtcTicks = 0;
        state.nextEventUtcTicks = 0;
        state.lastError = "";

        if (moved > 0)
        {
            state.status = GasHarvesterShipStatus.Unloading;
            float seconds = Mathf.Max(0.01f, unloadSecondsPerKg) * moved;
            state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(seconds).Ticks;
            LogEvent(state, $"arrived home, unloading {moved}kg for {seconds:F1}s, cargo={FormatCargo(state.cargo)}");
        }
        else
        {
            state.status = GasHarvesterShipStatus.Idle;
            state.completedTrips++;
            LogEvent(state, "arrived home with no payload");
        }
    }

    private GasCloudConfig FindTargetCloud(
        GasHarvesterShipDefinition definition,
        GasHarvesterShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        IslandConfig home)
    {
        GasCloudConfig best = null;
        float bestScore = float.MinValue;
        float searchRadius = Mathf.Max(0f, definition.searchRadiusMeters);

        for (int i = 0; i < config.gasClouds.Count; i++)
        {
            GasCloudConfig cloud = config.gasClouds[i];
            if (cloud == null || string.IsNullOrWhiteSpace(cloud.id)) continue;

            float remaining = GetCloudRemainingLiters(progress, config, cloud.id);
            if (remaining <= 0.001f) continue;

            GasCloudTypeConfig type = config.GetGasCloudType(cloud.cloudTypeId);
            if (type == null) continue;

            float cloudRadius = GasCloud.CalculateRadiusMeters(remaining, type.condensateLitersPerCubicMeter);
            float distance = Vector3.Distance(home.position, cloud.position);
            if (distance > searchRadius + cloudRadius) continue;

            float score = type.condensateLitersPerCubicMeter * 100000f + Mathf.Min(remaining, Mathf.Max(1f, definition.targetCargoKg)) - distance * 0.01f;
            if (score > bestScore)
            {
                bestScore = score;
                best = cloud;
            }
        }

        return best;
    }

    private bool TryBuildMetrics(
        GasHarvesterShipDefinition definition,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        WorldConfigDatabase config,
        float currentCargoKg,
        out GasHarvesterShipMetrics metrics,
        out string error)
    {
        metrics = new GasHarvesterShipMetrics();
        error = "";

        if (definition == null)
        {
            error = "No gas harvester definition.";
            return false;
        }

        if (catalog == null)
        {
            error = "No ship catalog.";
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
            error = result != null ? result.message : "Gas harvester assembly is invalid.";
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
        metrics.harvesterVolumeM3PerSecond = stats.Get(ShipStatId.GasHarvesterVolumeM3PerSecond, 0f);
        metrics.harvesterPowerDrawKw = stats.Get(ShipStatId.GasHarvesterPowerDrawKw, 0f);
        metrics.harvesterRadiusMeters = stats.Get(ShipStatId.GasHarvesterRadiusMeters, 0f);
        metrics.harvesterCycleSeconds = Mathf.Max(0.1f, stats.Get(ShipStatId.GasHarvesterCycleSeconds, 5f));
        metrics.engineLiftKg = metrics.enginePowerKw * metrics.claudiumLiftEfficiency;
        metrics.allowedTakeoffMassKg = Mathf.Min(metrics.engineLiftKg, Mathf.Min(metrics.claudiumMaxLiftKg, metrics.hullLimitKg));
        metrics.maxCargoKg = Mathf.Max(0f, metrics.allowedTakeoffMassKg - metrics.emptyMassKg);
        metrics.cruiseSpeedMS = Mathf.Max(0f, metrics.propellerMaxSpeedMS * Mathf.Clamp(cruiseSpeedFactor, 0.1f, 1f));
        metrics.cruisePowerKw = Mathf.Max(0f, metrics.enginePowerKw * Mathf.Clamp(cruisePowerLever, 0.05f, 1.2f));

        ItemConfig fuel = config != null ? config.GetItem(metrics.engineFuelId) : null;
        metrics.fuelEnergyKwhPerKg = fuel != null ? Mathf.Max(0f, fuel.energyKwhPerKg) : 0f;

        if (metrics.enginePowerKw <= 0f)
        {
            error = "Gas harvester ship has no engine power.";
            return false;
        }

        if (metrics.cruiseSpeedMS <= 0f)
        {
            error = "Gas harvester ship has no cruise speed.";
            return false;
        }

        if (metrics.harvesterVolumeM3PerSecond <= 0f || metrics.harvesterPowerDrawKw <= 0f || metrics.harvesterRadiusMeters <= 0f)
        {
            error = "Gas harvester module is not installed or has no working stats.";
            return false;
        }

        if (metrics.allowedTakeoffMassKg <= metrics.emptyMassKg)
        {
            error = "Gas harvester cannot take off: empty mass is above lift limit.";
            return false;
        }

        if (currentCargoKg > metrics.maxCargoKg + 0.001f)
        {
            error = $"Gas harvester overloaded: {currentCargoKg:F0}/{metrics.maxCargoKg:F0} kg.";
            return false;
        }

        return true;
    }

    private GasHarvesterLegEstimate EstimateLeg(GasHarvesterShipMetrics metrics, Vector3 from, Vector3 to, float cargoKg)
    {
        GasHarvesterLegEstimate estimate = new GasHarvesterLegEstimate();
        float totalMassKg = metrics.emptyMassKg + Mathf.Max(0f, cargoKg);
        if (totalMassKg > metrics.allowedTakeoffMassKg + 0.001f)
        {
            estimate.reason = $"Takeoff mass {totalMassKg:F0} kg is above limit {metrics.allowedTakeoffMassKg:F0} kg.";
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

    private bool TryEstimateHarvestCycle(
        GasHarvesterShipMetrics metrics,
        float cargoKg,
        float cycleSeconds,
        out float fuelKg,
        out float claudiumKg,
        out string error)
    {
        fuelKg = 0f;
        claudiumKg = 0f;
        error = "";

        float totalMassKg = metrics.emptyMassKg + Mathf.Max(0f, cargoKg);
        float claudiumPowerKw = metrics.claudiumLiftEfficiency > 0f ? totalMassKg / metrics.claudiumLiftEfficiency : float.MaxValue;
        if (claudiumPowerKw + metrics.harvesterPowerDrawKw > metrics.enginePowerKw + 0.001f)
        {
            error = "Harvester cannot run: claudium lift plus module power exceeds 100% engine load.";
            return false;
        }

        float totalPowerKw = claudiumPowerKw + metrics.harvesterPowerDrawKw;
        if (metrics.engineFuelEfficiency > 0f && metrics.fuelEnergyKwhPerKg > 0f)
        {
            fuelKg = totalPowerKw / metrics.engineFuelEfficiency / metrics.fuelEnergyKwhPerKg * (cycleSeconds / 3600f);
        }

        claudiumKg = metrics.claudiumConsumptionPerTonSecond * Mathf.Max(0f, totalMassKg / 1000f) * cycleSeconds;
        return true;
    }

    private bool TryEstimateHarvestWorkBudget(
        GasHarvesterShipMetrics metrics,
        GasCloudTypeConfig cloudType,
        float plannedHarvestKg,
        float currentCargoKg,
        out GasHarvesterWorkEstimate estimate,
        out string error)
    {
        estimate = new GasHarvesterWorkEstimate();
        error = "";

        if (cloudType == null)
        {
            error = "Target cloud type not found.";
            return false;
        }

        float cycleSeconds = Mathf.Max(0.1f, metrics.harvesterCycleSeconds);
        float kgPerCycle = metrics.harvesterVolumeM3PerSecond * cycleSeconds * Mathf.Max(0.0001f, cloudType.condensateLitersPerCubicMeter);
        if (kgPerCycle <= 0f)
        {
            error = "Harvester cannot collect this cloud type.";
            return false;
        }

        int cycles = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1f, plannedHarvestKg) / kgPerCycle));
        float estimatedCargoKg = currentCargoKg + Mathf.Max(0f, plannedHarvestKg);
        if (!TryEstimateHarvestCycle(metrics, estimatedCargoKg, cycleSeconds, out float fuelPerCycleKg, out float claudiumPerCycleKg, out error))
        {
            return false;
        }

        float reserve = Mathf.Max(1f, reserveMultiplier);
        estimate.estimatedCycles = cycles;
        estimate.requiredFuelKg = Mathf.CeilToInt(fuelPerCycleKg * cycles * reserve);
        estimate.requiredClaudiumKg = Mathf.CeilToInt(claudiumPerCycleKg * cycles * reserve);
        return true;
    }

    private void EnsureShipHasHome(GasHarvesterShipState state, GasHarvesterShipDefinition definition, WorldConfigDatabase config)
    {
        if (string.IsNullOrWhiteSpace(state.homeIslandId))
        {
            state.homeIslandId = string.IsNullOrWhiteSpace(definition.homeIslandId) ? "capital" : definition.homeIslandId;
        }

        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home != null && state.status == GasHarvesterShipStatus.Idle)
        {
            state.lastKnownPosition = home.position;
        }
    }

    private static void InitializeStateFromDefinition(GasHarvesterShipState state, GasHarvesterShipDefinition definition)
    {
        state.displayName = string.IsNullOrWhiteSpace(definition.displayName) ? definition.shipId : definition.displayName;
        state.homeIslandId = string.IsNullOrWhiteSpace(definition.homeIslandId) ? "capital" : definition.homeIslandId;
        state.status = GasHarvesterShipStatus.Idle;
        state.targetCloudId = "";
        state.cargo = CloneResourceStacks(definition.startingCargo);
        state.harvestBufferKg = 0f;
        state.pendingFuelConsumptionKg = 0f;
        state.pendingClaudiumConsumptionKg = 0f;
        state.lastError = "Ready to harvest gas.";
    }

    private float GetCloudRemainingLiters(PlayerProgress progress, WorldConfigDatabase config, string cloudId)
    {
        GasCloudManager manager = ResolveCloudManager();
        if (manager != null)
        {
            return manager.GetRemainingLiters(cloudId);
        }

        GasCloudConfig cloud = config.GetGasCloud(cloudId);
        if (cloud == null || progress == null) return 0f;

        GasCloudState state = progress.GetGasCloudState(cloudId, true);
        if (state == null) return Mathf.Max(0f, cloud.initialVolumeLiters);
        if (!state.initialized)
        {
            state.initialized = true;
            state.remainingVolumeLiters = Mathf.Max(0f, cloud.initialVolumeLiters);
        }

        return Mathf.Max(0f, state.remainingVolumeLiters);
    }

    private float HarvestCloudLiters(PlayerProgress progress, WorldConfigDatabase config, string cloudId, float sampledCubicMeters)
    {
        GasCloudManager manager = ResolveCloudManager();
        if (manager != null)
        {
            return manager.HarvestLiters(cloudId, sampledCubicMeters);
        }

        GasCloudConfig cloud = config.GetGasCloud(cloudId);
        if (cloud == null || progress == null || sampledCubicMeters <= 0f) return 0f;

        GasCloudTypeConfig type = config.GetGasCloudType(cloud.cloudTypeId);
        if (type == null) return 0f;

        GasCloudState state = progress.GetGasCloudState(cloudId, true);
        if (state == null) return 0f;
        if (!state.initialized)
        {
            state.initialized = true;
            state.remainingVolumeLiters = Mathf.Max(0f, cloud.initialVolumeLiters);
        }

        float requestedLiters = sampledCubicMeters * Mathf.Max(0.0001f, type.condensateLitersPerCubicMeter);
        float harvestedLiters = Mathf.Min(requestedLiters, Mathf.Max(0f, state.remainingVolumeLiters));
        state.remainingVolumeLiters = Mathf.Max(0f, state.remainingVolumeLiters - harvestedLiters);
        return harvestedLiters;
    }

    private GasCloudManager ResolveCloudManager()
    {
        MetaGameState meta = Meta;
        if (meta != null && meta.gasCloudManager != null)
        {
            return meta.gasCloudManager;
        }

        return FindFirstObjectByType<GasCloudManager>();
    }

    private int GetHarvestPayloadKg(GasHarvesterShipState state, GasHarvesterShipMetrics metrics, GasHarvesterShipDefinition definition)
    {
        int payload = 0;
        string claudiumResourceId = ResolveClaudiumResourceId(definition);
        if (state.cargo == null) return payload;

        for (int i = 0; i < state.cargo.Count; i++)
        {
            ResourceStack stack = state.cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId)) continue;
            if (stack.resourceId == metrics.engineFuelId || stack.resourceId == claudiumResourceId) continue;
            payload += Mathf.Max(0, stack.amount);
        }

        return payload;
    }

    private static void AccumulateConsumableSpend(GasHarvesterShipState state, string itemId, float amountKg, ref float buffer)
    {
        if (state == null || string.IsNullOrWhiteSpace(itemId) || amountKg <= 0f) return;

        buffer += amountKg;
        int wholeKg = Mathf.FloorToInt(buffer + 0.0001f);
        if (wholeKg <= 0) return;

        state.TrySpendCargo(itemId, wholeKg);
        buffer = Mathf.Max(0f, buffer - wholeKg);
    }

    private void UpdateFlightPositionToCloud(GasHarvesterShipState state, WorldConfigDatabase config, long utcTicks)
    {
        IslandConfig home = config.GetIsland(state.homeIslandId);
        GasCloudConfig cloud = config.GetGasCloud(state.targetCloudId);
        if (home == null || cloud == null) return;

        float t = 0f;
        if (state.flightArrivesUtcTicks > state.flightStartedUtcTicks)
        {
            t = Mathf.Clamp01((utcTicks - state.flightStartedUtcTicks) / (float)(state.flightArrivesUtcTicks - state.flightStartedUtcTicks));
        }

        state.lastKnownPosition = Vector3.Lerp(home.position, cloud.position, t);
    }

    private void UpdateReturnFlightPosition(GasHarvesterShipState state, WorldConfigDatabase config, long utcTicks)
    {
        GasCloudConfig cloud = config.GetGasCloud(state.targetCloudId);
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (cloud == null || home == null) return;

        float t = 0f;
        if (state.flightArrivesUtcTicks > state.flightStartedUtcTicks)
        {
            t = Mathf.Clamp01((utcTicks - state.flightStartedUtcTicks) / (float)(state.flightArrivesUtcTicks - state.flightStartedUtcTicks));
        }

        state.lastKnownPosition = Vector3.Lerp(cloud.position, home.position, t);
    }

    private static bool TryTopUpForLeg(GasHarvesterShipState state, IslandProductionState storage, string resourceId, int requiredAmount)
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

    private static int SetStorageAtLeast(IslandProductionState storage, string resourceId, int minAmount)
    {
        if (storage == null || string.IsNullOrWhiteSpace(resourceId) || minAmount <= 0) return 0;

        int current = storage.GetResourceAmount(resourceId);
        if (current >= minAmount) return 0;

        storage.SetResourceAmount(resourceId, minAmount);
        return minAmount - current;
    }

    private int UnloadHarvestPayload(GasHarvesterShipState state, IslandProductionState storage)
    {
        if (state == null || storage == null || state.cargo == null) return 0;

        int moved = 0;
        for (int i = state.cargo.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = state.cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
            {
                state.cargo.RemoveAt(i);
                continue;
            }

            if (IsPropulsionResource(stack.resourceId))
            {
                continue;
            }

            moved += stack.amount;
            storage.AddResource(stack.resourceId, stack.amount);
            state.cargo.RemoveAt(i);
        }

        return moved;
    }

    private bool IsPropulsionResource(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (itemId == defaultClaudiumResourceId) return true;
        return itemId == "wood" || itemId == "coal";
    }

    private string ResolveClaudiumResourceId(GasHarvesterShipDefinition definition)
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.claudiumResourceId)) return definition.claudiumResourceId;
        return string.IsNullOrWhiteSpace(defaultClaudiumResourceId) ? "claudium" : defaultClaudiumResourceId;
    }

    private void SetWaiting(GasHarvesterShipState state, string message)
    {
        string text = string.IsNullOrWhiteSpace(message) ? "Waiting for resources." : message;
        bool changed = state.status != GasHarvesterShipStatus.WaitingForResources || state.lastError != text;
        state.status = GasHarvesterShipStatus.WaitingForResources;
        state.nextEventUtcTicks = 0;
        state.lastError = text;
        if (changed)
        {
            LogEvent(state, "waiting: " + text);
        }
    }

    private void SetError(GasHarvesterShipState state, string message)
    {
        string text = string.IsNullOrWhiteSpace(message) ? "Gas harvester error." : message;
        bool changed = state.status != GasHarvesterShipStatus.Error || state.lastError != text;
        state.status = GasHarvesterShipStatus.Error;
        state.nextEventUtcTicks = 0;
        state.lastError = text;
        if (changed)
        {
            LogEvent(state, "error: " + text);
        }
    }

    private void LogEvent(GasHarvesterShipState state, string message)
    {
        if (!debugLogging) return;

        string shipId = state != null && !string.IsNullOrWhiteSpace(state.shipId) ? state.shipId : "unknown";
        Debug.Log("[GasHarvesting] " + shipId + ": " + message, this);
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
public class GasHarvesterShipDefinition
{
    public bool enabled = true;
    public string shipId = "gas_harvester_01";
    public string displayName = "Gas Harvester";
    public string homeIslandId = "capital";
    public float searchRadiusMeters = 1200f;
    public int targetCargoKg = 40;
    public string hullId = "starter_hull";
    public string claudiumResourceId = "claudium";
    public bool autoInstallRequiredModules = true;
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public List<ResourceStack> startingCargo = new List<ResourceStack>();
}

public struct GasHarvesterShipMetrics
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
    public float harvesterVolumeM3PerSecond;
    public float harvesterPowerDrawKw;
    public float harvesterRadiusMeters;
    public float harvesterCycleSeconds;
}

public struct GasHarvesterLegEstimate
{
    public bool canFly;
    public float durationSeconds;
    public int requiredFuelKg;
    public int requiredClaudiumKg;
    public string reason;
}

public struct GasHarvesterWorkEstimate
{
    public int estimatedCycles;
    public int requiredFuelKg;
    public int requiredClaudiumKg;
}
