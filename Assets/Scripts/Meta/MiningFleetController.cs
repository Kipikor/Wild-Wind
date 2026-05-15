using System;
using System.Collections.Generic;
using UnityEngine;

public class MiningFleetController : MonoBehaviour
{
    [InspectorName("Meta game")]
    public MetaGameState metaGameState;
    [InspectorName("Simulation enabled")]
    public bool simulationEnabled = true;
    [InspectorName("Fuel and claudium reserve")]
    public float reserveMultiplier = 1.2f;
    [InspectorName("Cruise speed share")]
    [Range(0.1f, 1f)] public float cruiseSpeedFactor = 0.7f;
    [InspectorName("Cruise power lever")]
    [Range(0.05f, 1.2f)] public float cruisePowerLever = 0.7f;
    [InspectorName("Default claudium resource")]
    public string defaultClaudiumResourceId = "claudium";
    [InspectorName("Интервал проверки осыпи, сек")]
    public float miningCycleSeconds = 10f;
    [HideInInspector]
    public int plannedMiningWorkCycles = 8;
    [InspectorName("Макс. ожидание под глыбой, сек")]
    public float maxMiningWatchSeconds = 300f;
    [InspectorName("Unload seconds per kg")]
    public float unloadSecondsPerKg = 0.5f;
    [InspectorName("Mining position below rock")]
    public float miningOffsetBelowRockMeters = 24f;
    [Header("Debug")]
    public bool debugLogging;
    public bool debugStockAllIslandFuelAndClaudium = true;
    public int debugMinIslandWoodKg = 500;
    public int debugMinIslandCharcoalKg = 500;
    public int debugMinIslandClaudiumKg = 250;
    [InspectorName("Mining ships")]
    public List<MiningShipDefinition> ships = new List<MiningShipDefinition>();

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
        ships ??= new List<MiningShipDefinition>();
        if (ships.Count > 0) return;

        ships.Add(new MiningShipDefinition
        {
            shipId = "miner_01",
            displayName = "Mining Kamaz 01",
            homeIslandId = "capital",
            searchRadiusMeters = 1600f,
            targetOreKg = 20,
            hullId = "starter_hull",
            autoInstallRequiredModules = true,
            installedModules = new List<InstalledModuleState>
            {
                new InstalledModuleState { slotId = "utility_01", moduleId = "starter_mining_hold" }
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
            MiningShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            MiningShipState state = progress.GetMiningShipState(definition.shipId, false);
            if (state == null)
            {
                state = progress.GetMiningShipState(definition.shipId, true);
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

        MiningWorldSimulator.EnsureRuntime(config, progress, toUtcTicks);
        EnsureRuntimeShips(progress);

        int changed = 0;
        if (debugStockAllIslandFuelAndClaudium)
        {
            changed += DebugStockAllIslandFuelAndClaudium(config, progress);
        }

        if (ships == null) return changed;

        for (int i = 0; i < ships.Count; i++)
        {
            MiningShipDefinition definition = ships[i];
            if (definition == null || !definition.enabled || string.IsNullOrWhiteSpace(definition.shipId)) continue;

            MiningShipState state = progress.GetMiningShipState(definition.shipId, true);
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
        MiningShipDefinition definition,
        MiningShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        long fromUtcTicks,
        long toUtcTicks)
    {
        if (state == null) return 0;

        if (!TryBuildMetrics(definition, progress, catalog, techTree, config, state.GetCargoMassKg(), out MiningShipMetrics metrics, out string metricsError))
        {
            SetError(state, metricsError);
            return 0;
        }

        if (state.status == MiningShipStatus.Error
            && !string.IsNullOrWhiteSpace(state.lastError)
            && (state.lastError.IndexOf("overloaded", StringComparison.OrdinalIgnoreCase) >= 0
                || state.lastError.IndexOf("перегруж", StringComparison.OrdinalIgnoreCase) >= 0)
            && state.GetCargoMassKg() <= metrics.maxCargoKg + 0.001f)
        {
            state.status = MiningShipStatus.Idle;
            state.lastError = "Overload cleared after mining setup update.";
            LogEvent(state, "overload cleared, retrying");
        }

        EnsureShipHasHome(state, definition, config);

        int changed = 0;
        long cursorTicks = fromUtcTicks;
        int guard = 0;
        while (cursorTicks < toUtcTicks && guard++ < 10000)
        {
            if (state.status == MiningShipStatus.FlyingToRock)
            {
                if (state.flightArrivesUtcTicks <= 0)
                {
                    SetError(state, "Mining flight has no arrival time.");
                    return changed;
                }

                if (toUtcTicks < state.flightArrivesUtcTicks)
                {
                    UpdateFlightPositionToRock(state, config, toUtcTicks);
                    return changed;
                }

                cursorTicks = Math.Max(cursorTicks, state.flightArrivesUtcTicks);
                CompleteOutboundFlight(state, config, progress, cursorTicks);
                changed++;
                continue;
            }

            if (state.status == MiningShipStatus.Mining)
            {
                if (state.nextEventUtcTicks <= 0)
                {
                    state.nextEventUtcTicks = cursorTicks + TimeSpan.FromSeconds(Mathf.Max(0.1f, miningCycleSeconds)).Ticks;
                }

                if (toUtcTicks < state.nextEventUtcTicks)
                {
                    return changed;
                }

                cursorTicks = Math.Max(cursorTicks, state.nextEventUtcTicks);
                if (!TryCompleteMiningCycle(definition, state, config, progress, metrics, cursorTicks))
                {
                    return changed;
                }

                changed++;
                continue;
            }

            if (state.status == MiningShipStatus.Returning)
            {
                if (state.flightArrivesUtcTicks <= 0)
                {
                    SetError(state, "Mining return flight has no arrival time.");
                    return changed;
                }

                if (toUtcTicks < state.flightArrivesUtcTicks)
                {
                    UpdateReturnPosition(state, config, toUtcTicks);
                    return changed;
                }

                cursorTicks = Math.Max(cursorTicks, state.flightArrivesUtcTicks);
                CompleteReturnFlight(definition, state, config, progress, metrics, cursorTicks);
                changed++;
                continue;
            }

            if (state.status == MiningShipStatus.Unloading)
            {
                if (state.nextEventUtcTicks <= 0 || toUtcTicks < state.nextEventUtcTicks)
                {
                    return changed;
                }

                cursorTicks = Math.Max(cursorTicks, state.nextEventUtcTicks);
                FinishUnload(definition, state, config, progress, metrics);
                changed++;
                continue;
            }

            if (state.status == MiningShipStatus.Error)
            {
                return changed;
            }

            if (!TryStartMiningTrip(definition, state, config, progress, metrics, cursorTicks, out string startError))
            {
                SetWaiting(state, startError);
                return changed;
            }

            changed++;
        }

        if (guard >= 10000)
        {
            SetWaiting(state, "Mining time step stopped by safety guard.");
        }

        return changed;
    }

    private bool TryStartMiningTrip(
        MiningShipDefinition definition,
        MiningShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        MiningShipMetrics metrics,
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

        MiningRockState targetRock = FindBestRock(config, progress, home.position, definition.searchRadiusMeters, currentTicks);
        if (targetRock == null)
        {
            error = "No safe mining rock inside search radius.";
            return false;
        }

        MiningZoneConfig zone = config.GetMiningZone(targetRock.zoneId);
        OreTypeConfig oreType = config.GetOreType(targetRock.oreTypeId);
        if (zone == null || oreType == null || string.IsNullOrWhiteSpace(oreType.oreItemId))
        {
            error = "Target mining rock has no valid ore type.";
            return false;
        }

        Vector3 rockPosition = MiningWorldSimulator.CalculateRockPosition(zone, targetRock, currentTicks);
        Vector3 miningPosition = CalculateMiningPosition(zone, rockPosition);
        if (miningPosition.y < zone.stormY + zone.stormSafetyClearanceY)
        {
            error = "Target rock is too close to storm border.";
            return false;
        }

        float currentPayloadKg = GetMiningPayloadKg(state, metrics, definition);
        float freeImpactKg = Mathf.Max(0f, metrics.impactHoldCapacityKg - currentPayloadKg);
        float freeMassKg = Mathf.Max(0f, metrics.maxCargoKg - state.GetCargoMassKg());
        float plannedHarvestKg = Mathf.Min(Mathf.Max(1f, definition.targetOreKg), Mathf.Min(freeImpactKg, Mathf.Min(freeMassKg, targetRock.remainingOreKg)));
        if (plannedHarvestKg < 1f)
        {
            error = "Mining ship is full or target rock has no whole kilograms.";
            return false;
        }

        MiningLegEstimate outbound = EstimateLeg(metrics, home.position, miningPosition, state.GetCargoMassKg());
        MiningLegEstimate ret = EstimateLeg(metrics, miningPosition, home.position, state.GetCargoMassKg() + plannedHarvestKg);
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

        if (!TryEstimateMiningWorkBudget(metrics, Mathf.Max(miningCycleSeconds, maxMiningWatchSeconds), state.GetCargoMassKg() + plannedHarvestKg, out MiningWorkEstimate work, out string workError))
        {
            error = workError;
            return false;
        }

        string claudiumResourceId = ResolveClaudiumResourceId(definition);
        IslandProductionState storage = progress.GetIslandProductionState(home.id, true);
        int fuelNeed = outbound.requiredFuelKg + ret.requiredFuelKg + work.requiredFuelKg;
        int claudiumNeed = outbound.requiredClaudiumKg + ret.requiredClaudiumKg + work.requiredClaudiumKg;
        int missingFuel = Mathf.Max(0, fuelNeed - state.GetCargoAmount(metrics.engineFuelId));
        int missingClaudium = Mathf.Max(0, claudiumNeed - state.GetCargoAmount(claudiumResourceId));
        int cargoAfterLoadAndOutbound = state.GetCargoMassKg() + missingFuel + missingClaudium - outbound.requiredFuelKg - outbound.requiredClaudiumKg;
        if (cargoAfterLoadAndOutbound > metrics.maxCargoKg)
        {
            error = $"Not enough mass reserve for mining supplies: after launch {cargoAfterLoadAndOutbound} kg, limit {metrics.maxCargoKg:F0} kg.";
            return false;
        }

        if (!EnsureCargoAtLeastFromStorage(state, storage, metrics.engineFuelId, fuelNeed, out error))
        {
            return false;
        }

        if (!EnsureCargoAtLeastFromStorage(state, storage, claudiumResourceId, claudiumNeed, out error))
        {
            return false;
        }

        if (!state.TrySpendCargo(metrics.engineFuelId, outbound.requiredFuelKg))
        {
            error = "Not enough loaded fuel for outbound flight.";
            return false;
        }

        if (!state.TrySpendCargo(claudiumResourceId, outbound.requiredClaudiumKg))
        {
            error = "Not enough loaded claudium for outbound flight.";
            return false;
        }

        state.status = MiningShipStatus.FlyingToRock;
        state.targetRockId = targetRock.rockId;
        state.targetOreTypeId = targetRock.oreTypeId;
        state.miningPosition = miningPosition;
        state.flightStartedUtcTicks = currentTicks;
        state.flightArrivesUtcTicks = currentTicks + TimeSpan.FromSeconds(outbound.durationSeconds).Ticks;
        state.nextEventUtcTicks = state.flightArrivesUtcTicks;
        state.lastKnownPosition = home.position;
        state.lastError = "Flying to mining rock: " + targetRock.rockId;
        LogEvent(state, $"flight started {home.id}->{targetRock.rockId}, eta={outbound.durationSeconds:F1}s, return fuel={ret.requiredFuelKg}, return claudium={ret.requiredClaudiumKg}, work fuel={work.requiredFuelKg}, work claudium={work.requiredClaudiumKg}, cargo={FormatCargo(state.cargo)}");
        return true;
    }

    private void CompleteOutboundFlight(MiningShipState state, WorldConfigDatabase config, PlayerProgress progress, long currentTicks)
    {
        state.status = MiningShipStatus.Mining;
        state.flightArrivesUtcTicks = 0;
        state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(Mathf.Max(0.1f, miningCycleSeconds)).Ticks;
        state.miningStartedUtcTicks = currentTicks;
        state.lastObservedShedUtcTicks = 0;
        MiningRockState rock = FindRock(progress, state.targetRockId);
        if (rock != null)
        {
            rock.shedBufferKg = 0f;
            state.lastObservedShedUtcTicks = rock.lastNaturalShedUtcTicks;
        }

        state.lastKnownPosition = state.miningPosition;
        state.lastError = "Ждет осыпь под глыбой: " + state.targetRockId;
        LogEvent(state, "прибыл под глыбу " + state.targetRockId + " и ждет осыпь");
    }

    private bool TryCompleteMiningCycle(
        MiningShipDefinition definition,
        MiningShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        MiningShipMetrics metrics,
        long currentTicks)
    {
        MiningRockState rock = FindRock(progress, state.targetRockId);
        if (rock == null || !MiningWorldSimulator.IsRockSafeForAutopilot(config, rock, currentTicks))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Rock is gone or too close to storm.");
        }

        OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
        MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
        if (oreType == null || zone == null || string.IsNullOrWhiteSpace(oreType.oreItemId))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Target ore type missing.");
        }

        Vector3 rockPosition = MiningWorldSimulator.CalculateRockPosition(zone, rock, currentTicks);
        state.miningPosition = CalculateMiningPosition(zone, rockPosition);
        if (state.miningPosition.y < zone.stormY + zone.stormSafetyClearanceY)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Storm safety border reached.");
        }

        if (GetMiningPayloadKg(state, metrics, definition) >= Mathf.Max(1f, definition.targetOreKg))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Target ore cargo reached.");
        }

        int freeImpactKg = Mathf.FloorToInt(metrics.impactHoldCapacityKg - GetMiningPayloadKg(state, metrics, definition) + 0.0001f);
        int freeMassKg = Mathf.FloorToInt(metrics.maxCargoKg - state.GetCargoMassKg() + 0.0001f);
        if (freeImpactKg <= 0 || freeMassKg <= 0)
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Impact hold full.");
        }

        float checkSeconds = Mathf.Max(0.1f, miningCycleSeconds);
        if (!TryEstimateMiningCycle(metrics, state.GetCargoMassKg(), checkSeconds, out float fuelKg, out float claudiumKg, out string powerError))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, powerError);
        }

        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null)
        {
            SetError(state, "Cannot reserve return: home island not found.");
            return false;
        }

        MiningLegEstimate ret = EstimateLeg(metrics, state.miningPosition, home.position, state.GetCargoMassKg());
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

        int caughtKg = 0;
        int shedKg = 0;
        if (rock.shedBufferKg >= 1f
            && rock.lastNaturalShedUtcTicks > state.lastObservedShedUtcTicks
            && rock.lastNaturalShedUtcTicks <= currentTicks
            && rock.lastNaturalShedOreItemId == oreType.oreItemId)
        {
            shedKg = Mathf.FloorToInt(rock.shedBufferKg);
            rock.shedBufferKg = 0f;
            state.lastObservedShedUtcTicks = rock.lastNaturalShedUtcTicks;
            int catchableKg = Mathf.FloorToInt(shedKg * Mathf.Clamp01(definition.passiveCatchEfficiency) + 0.0001f);
            caughtKg = Mathf.Min(catchableKg, Mathf.Min(freeImpactKg, freeMassKg));
            if (caughtKg > 0)
            {
                state.AddCargo(oreType.oreItemId, caughtKg);
            }
        }

        double waitSeconds = state.miningStartedUtcTicks > 0 ? new TimeSpan(currentTicks - state.miningStartedUtcTicks).TotalSeconds : 0d;

        state.lastKnownPosition = state.miningPosition;
        state.lastError = caughtKg > 0
            ? $"Пойман кусок {caughtKg}/{shedKg} кг: {oreType.oreItemId}. В глыбе осталось {rock.remainingOreKg:F1} кг."
            : $"Ждет осыпь: {oreType.oreItemId}, в глыбе осталось {rock.remainingOreKg:F1} кг.";
        if (caughtKg > 0 || rock.remainingOreKg <= 0.001f)
        {
            LogEvent(state, $"осыпь у {rock.rockId}: отвалилось={shedKg} кг, поймано={caughtKg} кг, осталось={rock.remainingOreKg:F1}, груз={FormatCargo(state.cargo)}");
        }

        if (rock.remainingOreKg <= 0.001f
            || GetMiningPayloadKg(state, metrics, definition) >= Mathf.Max(1f, definition.targetOreKg)
            || GetMiningPayloadKg(state, metrics, definition) >= metrics.impactHoldCapacityKg - 0.001f
            || waitSeconds >= Mathf.Max(checkSeconds, maxMiningWatchSeconds))
        {
            return TryBeginReturn(definition, state, config, metrics, currentTicks, "Смена под глыбой закончилась.");
        }

        state.status = MiningShipStatus.Mining;
        state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(checkSeconds).Ticks;
        return true;
    }

    private bool TryBeginReturn(
        MiningShipDefinition definition,
        MiningShipState state,
        WorldConfigDatabase config,
        MiningShipMetrics metrics,
        long currentTicks,
        string reason)
    {
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null)
        {
            SetError(state, "Cannot return: home island not found.");
            return false;
        }

        MiningLegEstimate ret = EstimateLeg(metrics, state.lastKnownPosition, home.position, state.GetCargoMassKg());
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

        state.status = MiningShipStatus.Returning;
        state.flightStartedUtcTicks = currentTicks;
        state.flightArrivesUtcTicks = currentTicks + TimeSpan.FromSeconds(ret.durationSeconds).Ticks;
        state.nextEventUtcTicks = state.flightArrivesUtcTicks;
        state.lastError = reason;
        LogEvent(state, $"return started {state.targetRockId}->{home.id}, eta={ret.durationSeconds:F1}s, fuel={ret.requiredFuelKg}, claudium={ret.requiredClaudiumKg}, reason={reason}, cargo={FormatCargo(state.cargo)}");
        return true;
    }

    private void CompleteReturnFlight(
        MiningShipDefinition definition,
        MiningShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        MiningShipMetrics metrics,
        long currentTicks)
    {
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home != null)
        {
            state.lastKnownPosition = home.position;
        }

        state.flightArrivesUtcTicks = 0;
        state.nextEventUtcTicks = 0;
        int payloadKg = GetPayloadKgOnly(state, metrics, definition);
        if (payloadKg > 0)
        {
            state.status = MiningShipStatus.Unloading;
            float seconds = Mathf.Max(0.01f, unloadSecondsPerKg) * payloadKg;
            state.nextEventUtcTicks = currentTicks + TimeSpan.FromSeconds(seconds).Ticks;
            state.lastError = "Unloading ore, " + payloadKg + " kg.";
            LogEvent(state, $"unload started at {state.homeIslandId}: payload={payloadKg}, duration={seconds:F1}s, cargo={FormatCargo(state.cargo)}");
            return;
        }

        state.status = MiningShipStatus.Idle;
        state.targetRockId = "";
        state.targetOreTypeId = "";
        state.completedTrips++;
        state.lastError = "Arrived home with no ore payload.";
        LogEvent(state, "arrived home with no ore payload");
    }

    private void FinishUnload(
        MiningShipDefinition definition,
        MiningShipState state,
        WorldConfigDatabase config,
        PlayerProgress progress,
        MiningShipMetrics metrics)
    {
        IslandProductionState storage = progress.GetIslandProductionState(state.homeIslandId, true);
        string claudiumId = ResolveClaudiumResourceId(definition);
        int moved = 0;
        for (int i = state.cargo.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = state.cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
            {
                state.cargo.RemoveAt(i);
                continue;
            }

            if (stack.resourceId == metrics.engineFuelId || stack.resourceId == claudiumId)
            {
                continue;
            }

            storage.AddResource(stack.resourceId, stack.amount);
            moved += stack.amount;
            state.cargo.RemoveAt(i);
        }

        state.status = MiningShipStatus.Idle;
        state.targetRockId = "";
        state.targetOreTypeId = "";
        state.nextEventUtcTicks = 0;
        state.completedTrips++;
        state.lastError = "Unloaded ore: " + moved + " kg.";
        LogEvent(state, "unload finished at " + state.homeIslandId + ", moved=" + moved + ", cargo=" + FormatCargo(state.cargo));
    }

    private MiningRockState FindBestRock(WorldConfigDatabase config, PlayerProgress progress, Vector3 homePosition, float searchRadius, long currentTicks)
    {
        MiningRockState best = null;
        float bestScore = float.NegativeInfinity;
        if (progress == null || progress.miningRocks == null) return null;

        for (int i = 0; i < progress.miningRocks.Count; i++)
        {
            MiningRockState rock = progress.miningRocks[i];
            if (rock == null || !MiningWorldSimulator.IsRockSafeForAutopilot(config, rock, currentTicks)) continue;

            MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
            OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
            if (zone == null || oreType == null) continue;

            Vector3 position = MiningWorldSimulator.CalculateRockPosition(zone, rock, currentTicks);
            Vector3 miningPosition = CalculateMiningPosition(zone, position);
            if (miningPosition.y < zone.stormY + zone.stormSafetyClearanceY)
            {
                continue;
            }

            float distance = Vector3.Distance(homePosition, position);
            if (distance > searchRadius) continue;

            float score = oreType.baseValue * 10f + Mathf.Min(rock.remainingOreKg, 100f) - distance * 0.01f + position.y * 0.05f;
            if (score > bestScore)
            {
                bestScore = score;
                best = rock;
            }
        }

        return best;
    }

    private static MiningRockState FindRock(PlayerProgress progress, string rockId)
    {
        if (progress == null || progress.miningRocks == null) return null;

        for (int i = 0; i < progress.miningRocks.Count; i++)
        {
            MiningRockState rock = progress.miningRocks[i];
            if (rock != null && rock.rockId == rockId)
            {
                return rock;
            }
        }

        return null;
    }

    private Vector3 CalculateMiningPosition(MiningZoneConfig zone, Vector3 rockPosition)
    {
        float safeY = zone.stormY + zone.stormSafetyClearanceY + 5f;
        float y = Mathf.Max(safeY, rockPosition.y - Mathf.Max(1f, zone.rockRadiusMeters) - Mathf.Max(0f, miningOffsetBelowRockMeters));
        return new Vector3(rockPosition.x, y, rockPosition.z);
    }

    private bool TryBuildMetrics(
        MiningShipDefinition definition,
        PlayerProgress progress,
        ShipCatalogSO catalog,
        TechTreeDefinitionSO techTree,
        WorldConfigDatabase config,
        float currentCargoKg,
        out MiningShipMetrics metrics,
        out string error)
    {
        metrics = new MiningShipMetrics();
        error = "";

        if (definition == null)
        {
            error = "No mining ship definition.";
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
            error = result != null ? result.message : "Mining ship assembly is invalid.";
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
        metrics.impactHoldCapacityKg = stats.Get(ShipStatId.MiningImpactHoldCapacityKg, 0f);
        metrics.engineLiftKg = metrics.enginePowerKw * metrics.claudiumLiftEfficiency;
        metrics.allowedTakeoffMassKg = Mathf.Min(metrics.engineLiftKg, Mathf.Min(metrics.claudiumMaxLiftKg, metrics.hullLimitKg));
        metrics.maxCargoKg = Mathf.Max(0f, metrics.allowedTakeoffMassKg - metrics.emptyMassKg);
        metrics.cruiseSpeedMS = Mathf.Max(0f, metrics.propellerMaxSpeedMS * Mathf.Clamp(cruiseSpeedFactor, 0.1f, 1f));
        metrics.cruisePowerKw = Mathf.Max(0f, metrics.enginePowerKw * Mathf.Clamp(cruisePowerLever, 0.05f, 1.2f));

        ItemConfig fuel = config != null ? config.GetItem(metrics.engineFuelId) : null;
        metrics.fuelEnergyKwhPerKg = fuel != null ? Mathf.Max(0f, fuel.energyKwhPerKg) : 0f;

        if (metrics.enginePowerKw <= 0f)
        {
            error = "Mining ship has no engine power.";
            return false;
        }

        if (metrics.cruiseSpeedMS <= 0f)
        {
            error = "Mining ship has no cruise speed.";
            return false;
        }

        if (metrics.impactHoldCapacityKg <= 0f)
        {
            error = "Mining ship has no impact hold module.";
            return false;
        }

        if (metrics.allowedTakeoffMassKg <= metrics.emptyMassKg)
        {
            error = "Mining ship cannot take off: empty mass is above lift limit.";
            return false;
        }

        if (currentCargoKg > metrics.maxCargoKg + 0.001f)
        {
            error = $"Майнер перегружен: {currentCargoKg:F0}/{metrics.maxCargoKg:F0} кг.";
            return false;
        }

        return true;
    }

    private MiningLegEstimate EstimateLeg(MiningShipMetrics metrics, Vector3 from, Vector3 to, float cargoKg)
    {
        MiningLegEstimate estimate = new MiningLegEstimate();
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

    private bool TryEstimateMiningCycle(MiningShipMetrics metrics, float cargoKg, float cycleSeconds, out float fuelKg, out float claudiumKg, out string error)
    {
        fuelKg = 0f;
        claudiumKg = 0f;
        error = "";

        float totalMassKg = metrics.emptyMassKg + Mathf.Max(0f, cargoKg);
        float claudiumPowerKw = metrics.claudiumLiftEfficiency > 0f ? totalMassKg / metrics.claudiumLiftEfficiency : float.MaxValue;
        if (claudiumPowerKw > metrics.enginePowerKw + 0.001f)
        {
            error = "Mining ship cannot hover: claudium lift exceeds 100% engine load.";
            return false;
        }

        if (metrics.engineFuelEfficiency > 0f && metrics.fuelEnergyKwhPerKg > 0f)
        {
            fuelKg = claudiumPowerKw / metrics.engineFuelEfficiency / metrics.fuelEnergyKwhPerKg * (cycleSeconds / 3600f);
        }

        claudiumKg = metrics.claudiumConsumptionPerTonSecond * Mathf.Max(0f, totalMassKg / 1000f) * cycleSeconds;
        return true;
    }

    private bool TryEstimateMiningWorkBudget(MiningShipMetrics metrics, float plannedWatchSeconds, float estimatedCargoKg, out MiningWorkEstimate estimate, out string error)
    {
        estimate = new MiningWorkEstimate();
        error = "";

        float seconds = Mathf.Max(1f, plannedWatchSeconds);
        if (!TryEstimateMiningCycle(metrics, estimatedCargoKg, seconds, out float fuelKg, out float claudiumKg, out error))
        {
            return false;
        }

        float reserve = Mathf.Max(1f, reserveMultiplier);
        estimate.estimatedCycles = Mathf.CeilToInt(seconds / Mathf.Max(0.1f, miningCycleSeconds));
        estimate.requiredFuelKg = Mathf.CeilToInt(fuelKg * reserve);
        estimate.requiredClaudiumKg = Mathf.CeilToInt(claudiumKg * reserve);
        return true;
    }

    private void EnsureShipHasHome(MiningShipState state, MiningShipDefinition definition, WorldConfigDatabase config)
    {
        if (string.IsNullOrWhiteSpace(state.homeIslandId))
        {
            state.homeIslandId = string.IsNullOrWhiteSpace(definition.homeIslandId) ? "capital" : definition.homeIslandId;
        }

        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home != null && state.status == MiningShipStatus.Idle)
        {
            state.lastKnownPosition = home.position;
        }
    }

    private static void InitializeStateFromDefinition(MiningShipState state, MiningShipDefinition definition)
    {
        if (state == null || definition == null) return;
        state.shipId = definition.shipId ?? "";
        state.displayName = string.IsNullOrWhiteSpace(definition.displayName) ? state.shipId : definition.displayName;
        state.homeIslandId = string.IsNullOrWhiteSpace(definition.homeIslandId) ? "capital" : definition.homeIslandId;
        state.status = MiningShipStatus.Idle;
        state.targetRockId = "";
        state.targetOreTypeId = "";
        state.cargo = CloneResourceStacks(definition.startingCargo);
        state.lastError = "";
        state.Normalize();
    }

    private static bool EnsureCargoAtLeastFromStorage(MiningShipState state, IslandProductionState storage, string resourceId, int targetAmount, out string error)
    {
        error = "";
        if (state == null || storage == null || string.IsNullOrWhiteSpace(resourceId) || targetAmount <= 0) return true;

        int current = state.GetCargoAmount(resourceId);
        int missing = Mathf.Max(0, targetAmount - current);
        if (missing <= 0) return true;

        if (storage.GetResourceAmount(resourceId) < missing)
        {
            error = "Not enough " + resourceId + ": need " + missing + " kg.";
            return false;
        }

        storage.TrySpendResource(resourceId, missing);
        state.AddCargo(resourceId, missing);
        return true;
    }

    private static void AccumulateConsumableSpend(MiningShipState state, string resourceId, float amountKg, ref float pendingKg)
    {
        if (state == null || string.IsNullOrWhiteSpace(resourceId) || amountKg <= 0f) return;

        pendingKg += amountKg;
        int wholeKg = Mathf.FloorToInt(pendingKg + 0.0001f);
        if (wholeKg <= 0) return;

        int cargoAmount = state.GetCargoAmount(resourceId);
        int spend = Mathf.Min(cargoAmount, wholeKg);
        if (spend <= 0) return;

        if (state.TrySpendCargo(resourceId, spend))
        {
            pendingKg -= spend;
        }
    }

    private int GetMiningPayloadKg(MiningShipState state, MiningShipMetrics metrics, MiningShipDefinition definition)
    {
        if (state == null || state.cargo == null) return 0;
        string claudiumId = ResolveClaudiumResourceId(definition);
        int total = 0;
        for (int i = 0; i < state.cargo.Count; i++)
        {
            ResourceStack stack = state.cargo[i];
            if (stack == null || stack.amount <= 0) continue;
            if (stack.resourceId == metrics.engineFuelId || stack.resourceId == claudiumId) continue;
            total += stack.amount;
        }

        return total;
    }

    private int GetPayloadKgOnly(MiningShipState state, MiningShipMetrics metrics, MiningShipDefinition definition)
    {
        if (state == null || state.cargo == null) return 0;
        string claudiumId = ResolveClaudiumResourceId(definition);
        int total = 0;
        for (int i = 0; i < state.cargo.Count; i++)
        {
            ResourceStack stack = state.cargo[i];
            if (stack == null || stack.amount <= 0) continue;
            if (stack.resourceId == metrics.engineFuelId || stack.resourceId == claudiumId) continue;
            total += stack.amount;
        }

        return total;
    }

    private void UpdateFlightPositionToRock(MiningShipState state, WorldConfigDatabase config, long utcTicks)
    {
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null) return;

        float t = 0f;
        if (state.flightArrivesUtcTicks > state.flightStartedUtcTicks)
        {
            t = Mathf.Clamp01((utcTicks - state.flightStartedUtcTicks) / (float)(state.flightArrivesUtcTicks - state.flightStartedUtcTicks));
        }

        state.lastKnownPosition = Vector3.Lerp(home.position, state.miningPosition, t);
    }

    private void UpdateReturnPosition(MiningShipState state, WorldConfigDatabase config, long utcTicks)
    {
        IslandConfig home = config.GetIsland(state.homeIslandId);
        if (home == null) return;

        float t = 0f;
        if (state.flightArrivesUtcTicks > state.flightStartedUtcTicks)
        {
            t = Mathf.Clamp01((utcTicks - state.flightStartedUtcTicks) / (float)(state.flightArrivesUtcTicks - state.flightStartedUtcTicks));
        }

        state.lastKnownPosition = Vector3.Lerp(state.miningPosition, home.position, t);
    }

    private void SetError(MiningShipState state, string error)
    {
        if (state == null) return;
        string text = string.IsNullOrWhiteSpace(error) ? "Unknown mining error." : error;
        bool changed = state.status != MiningShipStatus.Error || state.lastError != text;
        state.status = MiningShipStatus.Error;
        state.lastError = text;
        state.nextEventUtcTicks = 0;
        if (changed)
        {
            LogEvent(state, "error: " + state.lastError);
        }
    }

    private void SetWaiting(MiningShipState state, string message)
    {
        if (state == null) return;
        string text = string.IsNullOrWhiteSpace(message) ? "Waiting." : message;
        bool changed = state.status != MiningShipStatus.WaitingForResources || state.lastError != text;
        state.status = MiningShipStatus.WaitingForResources;
        state.lastError = text;
        state.nextEventUtcTicks = 0;
        if (changed)
        {
            LogEvent(state, "waiting: " + state.lastError);
        }
    }

    private void LogEvent(MiningShipState state, string message)
    {
        if (!debugLogging || state == null) return;
        Debug.Log("[Майнинг] " + state.shipId + ": " + message, this);
    }

    private string ResolveClaudiumResourceId(MiningShipDefinition definition)
    {
        if (definition != null && !string.IsNullOrWhiteSpace(definition.claudiumResourceId))
        {
            return definition.claudiumResourceId;
        }

        return string.IsNullOrWhiteSpace(defaultClaudiumResourceId) ? "claudium" : defaultClaudiumResourceId;
    }

    private static int SetStorageAtLeast(IslandProductionState storage, string resourceId, int minimum)
    {
        if (storage == null || string.IsNullOrWhiteSpace(resourceId) || minimum <= 0) return 0;

        int current = storage.GetResourceAmount(resourceId);
        if (current >= minimum) return 0;

        storage.SetResourceAmount(resourceId, minimum);
        return minimum - current;
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
public class MiningShipDefinition
{
    public bool enabled = true;
    public string shipId = "miner_01";
    public string displayName = "Mining Ship";
    public string homeIslandId = "capital";
    public float searchRadiusMeters = 1600f;
    public int targetOreKg = 20;
    [Range(0f, 1f)] public float passiveCatchEfficiency = 1f;
    public string hullId = "starter_hull";
    public string claudiumResourceId = "claudium";
    public bool autoInstallRequiredModules = true;
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public List<ResourceStack> startingCargo = new List<ResourceStack>();
}

public struct MiningShipMetrics
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
    public float impactHoldCapacityKg;
}

public struct MiningLegEstimate
{
    public bool canFly;
    public float durationSeconds;
    public int requiredFuelKg;
    public int requiredClaudiumKg;
    public string reason;
}

public struct MiningWorkEstimate
{
    public int estimatedCycles;
    public int requiredFuelKg;
    public int requiredClaudiumKg;
}
