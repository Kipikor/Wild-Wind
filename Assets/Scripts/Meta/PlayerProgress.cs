using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameSessionMode
{
    [InspectorName("Стыковка")]
    Docked,
    [InspectorName("Вылет")]
    Flight
}

public enum DockingLocationKind
{
    [InspectorName("Остров")]
    Island,
    [InspectorName("Корабль")]
    Ship
}

public enum TimedProcessKind
{
    [InspectorName("Пассивная добыча")]
    IdleMining,
    [InspectorName("Производство")]
    Crafting,
    [InspectorName("Миссия")]
    Mission,
    [InspectorName("Обновление магазина")]
    StoreRefresh
}

[Serializable]
public class PlayerProgress
{
    public int money;
    public string selectedHullId = "";
    public List<string> researchedNodeIds = new List<string>();
    public List<string> purchasedNodeIds = new List<string>();
    public string activeResearchTechnologyId = "";
    public List<TechnologyResearchProgress> technologyResearchProgress = new List<TechnologyResearchProgress>();
    public List<ShipExperienceWallet> shipExperience = new List<ShipExperienceWallet>();
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public List<LogisticsShipState> logisticsShips = new List<LogisticsShipState>();
    public List<GasHarvesterShipState> gasHarvesterShips = new List<GasHarvesterShipState>();
    public List<MiningShipState> miningShips = new List<MiningShipState>();
    public List<ScoutShipState> scoutShips = new List<ScoutShipState>();

    public GameSessionMode currentMode = GameSessionMode.Docked;
    public DockingLocationKind currentDockKind = DockingLocationKind.Island;
    public string currentDockId = "capital";
    public bool hasCurrentDockPosition;
    public Vector3 currentDockPosition;
    public bool hasCurrentFlightPose;
    public Vector3 currentFlightPosition;
    public Quaternion currentFlightRotation = Quaternion.identity;
    public string activeFlightMissionId = "";
    public long lastSavedUtcTicks;
    public long lastProcessUtcTicks;
    public long nextShopRefreshUtcTicks;
    public int shopSeed;
    public bool receivedStartingInventory;
    public bool receivedStartingPaper;
    public float shipWeaponSpendBufferKg;

    public List<ResourceStack> inventory = new List<ResourceStack>();
    public List<ResourceStack> shipCargo = new List<ResourceStack>();
    public List<ResourceStack> shipImpactCargo = new List<ResourceStack>();
    public List<IslandProductionState> islandProductions = new List<IslandProductionState>();
    public List<GasCloudState> gasClouds = new List<GasCloudState>();
    public List<MiningRockState> miningRocks = new List<MiningRockState>();
    public List<MiningZoneState> miningZones = new List<MiningZoneState>();
    public List<ScoutedObjectState> scoutedObjects = new List<ScoutedObjectState>();
    public CargoTransferState cargoTransfer = new CargoTransferState();
    public List<TimedProcessState> activeProcesses = new List<TimedProcessState>();
    public List<string> acceptedMissionIds = new List<string>();
    public List<string> completedMissionIds = new List<string>();

    public void Normalize()
    {
        selectedHullId ??= "";
        activeResearchTechnologyId ??= "";
        currentDockId ??= "";
        activeFlightMissionId ??= "";
        if (currentFlightRotation.x == 0f
            && currentFlightRotation.y == 0f
            && currentFlightRotation.z == 0f
            && currentFlightRotation.w == 0f)
        {
            currentFlightRotation = Quaternion.identity;
        }

        researchedNodeIds ??= new List<string>();
        purchasedNodeIds ??= new List<string>();
        technologyResearchProgress ??= new List<TechnologyResearchProgress>();
        shipExperience ??= new List<ShipExperienceWallet>();
        installedModules ??= new List<InstalledModuleState>();
        logisticsShips ??= new List<LogisticsShipState>();
        gasHarvesterShips ??= new List<GasHarvesterShipState>();
        miningShips ??= new List<MiningShipState>();
        scoutShips ??= new List<ScoutShipState>();
        inventory ??= new List<ResourceStack>();
        shipCargo ??= new List<ResourceStack>();
        shipImpactCargo ??= new List<ResourceStack>();
        islandProductions ??= new List<IslandProductionState>();
        gasClouds ??= new List<GasCloudState>();
        miningRocks ??= new List<MiningRockState>();
        miningZones ??= new List<MiningZoneState>();
        scoutedObjects ??= new List<ScoutedObjectState>();
        cargoTransfer ??= new CargoTransferState();
        activeProcesses ??= new List<TimedProcessState>();
        acceptedMissionIds ??= new List<string>();
        completedMissionIds ??= new List<string>();
        cargoTransfer.Normalize();

        for (int i = shipCargo.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = shipCargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId))
            {
                shipCargo.RemoveAt(i);
                continue;
            }

            stack.amount = Mathf.Max(0, stack.amount);
        }

        shipWeaponSpendBufferKg = Mathf.Clamp(shipWeaponSpendBufferKg, 0f, 0.999f);
        if (GetShipCargoAmount("weapon") <= 0)
        {
            shipWeaponSpendBufferKg = 0f;
        }

        for (int i = shipImpactCargo.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = shipImpactCargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId))
            {
                shipImpactCargo.RemoveAt(i);
                continue;
            }

            stack.amount = Mathf.Max(0, stack.amount);
        }

        MigrateDeprecatedImpactCargoToShipCargo();

        for (int i = installedModules.Count - 1; i >= 0; i--)
        {
            if (installedModules[i] == null)
            {
                installedModules.RemoveAt(i);
            }
            else
            {
                installedModules[i].Normalize();
                if (string.IsNullOrWhiteSpace(installedModules[i].slotId))
                {
                    installedModules.RemoveAt(i);
                }
            }
        }

        for (int i = technologyResearchProgress.Count - 1; i >= 0; i--)
        {
            TechnologyResearchProgress state = technologyResearchProgress[i];
            if (state == null || string.IsNullOrWhiteSpace(state.technologyId))
            {
                technologyResearchProgress.RemoveAt(i);
                continue;
            }

            state.Normalize();
        }

        for (int i = logisticsShips.Count - 1; i >= 0; i--)
        {
            LogisticsShipState ship = logisticsShips[i];
            if (ship == null || string.IsNullOrWhiteSpace(ship.shipId))
            {
                logisticsShips.RemoveAt(i);
                continue;
            }

            ship.Normalize();
        }

        for (int i = gasHarvesterShips.Count - 1; i >= 0; i--)
        {
            GasHarvesterShipState ship = gasHarvesterShips[i];
            if (ship == null || string.IsNullOrWhiteSpace(ship.shipId))
            {
                gasHarvesterShips.RemoveAt(i);
                continue;
            }

            ship.Normalize();
        }

        for (int i = miningShips.Count - 1; i >= 0; i--)
        {
            MiningShipState ship = miningShips[i];
            if (ship == null || string.IsNullOrWhiteSpace(ship.shipId))
            {
                miningShips.RemoveAt(i);
                continue;
            }

            ship.Normalize();
        }

        for (int i = scoutShips.Count - 1; i >= 0; i--)
        {
            ScoutShipState ship = scoutShips[i];
            if (ship == null || string.IsNullOrWhiteSpace(ship.shipId))
            {
                scoutShips.RemoveAt(i);
                continue;
            }

            ship.Normalize();
        }

        for (int i = activeProcesses.Count - 1; i >= 0; i--)
        {
            if (activeProcesses[i] == null)
            {
                activeProcesses.RemoveAt(i);
            }
            else
            {
                activeProcesses[i].Normalize();
            }
        }

        for (int i = islandProductions.Count - 1; i >= 0; i--)
        {
            IslandProductionState island = islandProductions[i];
            if (island == null)
            {
                islandProductions.RemoveAt(i);
                continue;
            }

            island.Normalize();
            if (string.IsNullOrWhiteSpace(island.islandId))
            {
                islandProductions.RemoveAt(i);
            }
        }

        for (int i = gasClouds.Count - 1; i >= 0; i--)
        {
            GasCloudState cloud = gasClouds[i];
            if (cloud == null || string.IsNullOrWhiteSpace(cloud.cloudId))
            {
                gasClouds.RemoveAt(i);
                continue;
            }

            cloud.Normalize();
        }

        for (int i = miningRocks.Count - 1; i >= 0; i--)
        {
            MiningRockState rock = miningRocks[i];
            if (rock == null || string.IsNullOrWhiteSpace(rock.rockId))
            {
                miningRocks.RemoveAt(i);
                continue;
            }

            rock.Normalize();
        }

        for (int i = miningZones.Count - 1; i >= 0; i--)
        {
            MiningZoneState zone = miningZones[i];
            if (zone == null || string.IsNullOrWhiteSpace(zone.zoneId))
            {
                miningZones.RemoveAt(i);
                continue;
            }

            zone.Normalize();
        }

        for (int i = scoutedObjects.Count - 1; i >= 0; i--)
        {
            ScoutedObjectState scouted = scoutedObjects[i];
            if (scouted == null || string.IsNullOrWhiteSpace(scouted.objectId))
            {
                scoutedObjects.RemoveAt(i);
                continue;
            }

            scouted.Normalize();
        }
    }

    public void EnsureStarterHull(string hullId)
    {
        if (string.IsNullOrWhiteSpace(hullId)) return;

        if (string.IsNullOrWhiteSpace(selectedHullId))
        {
            selectedHullId = hullId;
        }
    }

    public bool SelectHull(string hullId)
    {
        if (string.IsNullOrWhiteSpace(hullId)) return false;
        if (selectedHullId == hullId) return true;

        selectedHullId = hullId;
        ClearInstalledModules();
        return true;
    }

    public string GetInstalledModule(string slotId)
    {
        InstalledModuleState module = GetInstalledModuleState(slotId, false);
        return module != null ? module.moduleId : "";
    }

    public void InstallModule(string slotId, string moduleId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) return;

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            InstalledModuleState existing = GetInstalledModuleState(slotId, false);
            if (existing != null)
            {
                installedModules.Remove(existing);
            }

            return;
        }

        InstalledModuleState state = GetInstalledModuleState(slotId, true);
        state.moduleId = moduleId;
    }

    public void ClearInstalledModules()
    {
        installedModules ??= new List<InstalledModuleState>();
        installedModules.Clear();
    }

    public void ReplaceShipAssembly(string hullId)
    {
        selectedHullId = hullId ?? "";
        ClearInstalledModules();
    }

    public void ClearShipCargo()
    {
        shipCargo ??= new List<ResourceStack>();
        shipCargo.Clear();
        ClearShipImpactCargo();
    }

    public void StopCargoTransfer()
    {
        cargoTransfer ??= new CargoTransferState();
        cargoTransfer.operations ??= new List<CargoTransferOperation>();
        cargoTransfer.active = false;
        cargoTransfer.currentOperationIndex = 0;
        cargoTransfer.operations.Clear();
    }

    public bool IsNodeResearched(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) && researchedNodeIds.Contains(nodeId);
    }

    public bool IsNodePurchased(string nodeId)
    {
        return !string.IsNullOrWhiteSpace(nodeId) && purchasedNodeIds.Contains(nodeId);
    }

    public bool ResearchNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return false;
        if (researchedNodeIds.Contains(nodeId)) return false;

        researchedNodeIds.Add(nodeId);
        return true;
    }

    public bool IsTechnologyCompleted(string technologyId)
    {
        return IsNodeResearched(technologyId);
    }

    public bool CompleteTechnology(string technologyId)
    {
        return ResearchNode(technologyId);
    }

    public TechnologyResearchProgress GetTechnologyProgress(string technologyId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(technologyId)) return null;
        technologyResearchProgress ??= new List<TechnologyResearchProgress>();

        for (int i = 0; i < technologyResearchProgress.Count; i++)
        {
            TechnologyResearchProgress state = technologyResearchProgress[i];
            if (state != null && state.technologyId == technologyId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        TechnologyResearchProgress newState = new TechnologyResearchProgress { technologyId = technologyId };
        technologyResearchProgress.Add(newState);
        return newState;
    }

    public LogisticsShipState GetLogisticsShipState(string shipId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;
        logisticsShips ??= new List<LogisticsShipState>();

        for (int i = 0; i < logisticsShips.Count; i++)
        {
            LogisticsShipState state = logisticsShips[i];
            if (state != null && state.shipId == shipId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        LogisticsShipState newState = new LogisticsShipState { shipId = shipId };
        logisticsShips.Add(newState);
        return newState;
    }

    public GasHarvesterShipState GetGasHarvesterShipState(string shipId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;
        gasHarvesterShips ??= new List<GasHarvesterShipState>();

        for (int i = 0; i < gasHarvesterShips.Count; i++)
        {
            GasHarvesterShipState state = gasHarvesterShips[i];
            if (state != null && state.shipId == shipId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        GasHarvesterShipState newState = new GasHarvesterShipState { shipId = shipId };
        gasHarvesterShips.Add(newState);
        return newState;
    }

    public MiningShipState GetMiningShipState(string shipId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;
        miningShips ??= new List<MiningShipState>();

        for (int i = 0; i < miningShips.Count; i++)
        {
            MiningShipState state = miningShips[i];
            if (state != null && state.shipId == shipId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        MiningShipState newState = new MiningShipState { shipId = shipId };
        miningShips.Add(newState);
        return newState;
    }

    public ScoutShipState GetScoutShipState(string shipId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;
        scoutShips ??= new List<ScoutShipState>();

        for (int i = 0; i < scoutShips.Count; i++)
        {
            ScoutShipState state = scoutShips[i];
            if (state != null && state.shipId == shipId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        ScoutShipState newState = new ScoutShipState { shipId = shipId };
        scoutShips.Add(newState);
        return newState;
    }

    public ScoutedObjectState GetScoutedObjectState(ScoutedObjectKind kind, string objectId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return null;
        scoutedObjects ??= new List<ScoutedObjectState>();

        for (int i = 0; i < scoutedObjects.Count; i++)
        {
            ScoutedObjectState state = scoutedObjects[i];
            if (state != null && state.kind == kind && state.objectId == objectId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        ScoutedObjectState newState = new ScoutedObjectState
        {
            kind = kind,
            objectId = objectId
        };
        scoutedObjects.Add(newState);
        return newState;
    }

    public bool HasObjectCoordinates(ScoutedObjectKind kind, string objectId)
    {
        ScoutedObjectState state = GetScoutedObjectState(kind, objectId, false);
        return state != null && state.coordinatesKnown;
    }

    public bool HasObjectFacts(ScoutedObjectKind kind, string objectId)
    {
        ScoutedObjectState state = GetScoutedObjectState(kind, objectId, false);
        return state != null && state.factsComplete;
    }

    public bool HasFreshObjectFacts(ScoutedObjectKind kind, string objectId, long currentTicks, float maxAgeSeconds = 0f)
    {
        ScoutedObjectState state = GetScoutedObjectState(kind, objectId, false);
        if (state == null || !state.factsComplete) return false;
        if (maxAgeSeconds <= 0f || currentTicks <= 0 || state.factsUpdatedUtcTicks <= 0) return true;

        long maxAgeTicks = TimeSpan.FromSeconds(maxAgeSeconds).Ticks;
        return currentTicks - state.factsUpdatedUtcTicks <= maxAgeTicks;
    }

    public bool PurchaseNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return false;
        if (purchasedNodeIds.Contains(nodeId)) return false;

        purchasedNodeIds.Add(nodeId);
        return true;
    }

    public int GetShipExperience(string shipId)
    {
        ShipExperienceWallet wallet = GetShipExperienceWallet(shipId, false);
        return wallet != null ? wallet.experience : 0;
    }

    public void AddShipExperience(string shipId, int amount)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return;

        ShipExperienceWallet wallet = GetShipExperienceWallet(shipId, true);
        wallet.experience += Mathf.Max(0, amount);
    }

    public bool TrySpendShipExperience(string shipId, int amount)
    {
        if (amount <= 0) return true;

        ShipExperienceWallet wallet = GetShipExperienceWallet(shipId, false);
        if (wallet == null || wallet.experience < amount) return false;

        wallet.experience -= amount;
        return true;
    }

    public int GetResourceAmount(string resourceId)
    {
        ResourceStack stack = GetResourceStack(resourceId, false);
        return stack != null ? stack.amount : 0;
    }

    public void AddResource(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;

        ResourceStack stack = GetResourceStack(resourceId, true);
        stack.amount += amount;
    }

    public void SetResourceAmount(string resourceId, int amount)
    {
        inventory ??= new List<ResourceStack>();
        SetStackAmount(inventory, resourceId, amount);
    }

    public bool TrySpendResource(string resourceId, int amount)
    {
        if (amount <= 0) return true;

        ResourceStack stack = GetResourceStack(resourceId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        return true;
    }

    public int GetShipCargoAmount(string resourceId)
    {
        ResourceStack stack = GetShipCargoStack(resourceId, false);
        return stack != null ? stack.amount : 0;
    }

    public void AddShipCargo(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;

        ResourceStack stack = GetShipCargoStack(resourceId, true);
        stack.amount += amount;
    }

    public void SetShipCargoAmount(string resourceId, int amount)
    {
        shipCargo ??= new List<ResourceStack>();
        SetStackAmount(shipCargo, resourceId, amount);
    }

    public bool TrySpendShipCargo(string resourceId, int amount)
    {
        if (amount <= 0) return true;

        ResourceStack stack = GetShipCargoStack(resourceId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        return true;
    }

    public int GetShipCargoMassKg()
    {
        return GetShipInternalCargoMassKg();
    }

    public float GetShipCargoMassKg(WorldConfigDatabase config)
    {
        return GetShipInternalCargoMassKg(config);
    }

    public int GetShipInternalCargoMassKg()
    {
        int total = 0;
        shipCargo ??= new List<ResourceStack>();
        for (int i = 0; i < shipCargo.Count; i++)
        {
            ResourceStack stack = shipCargo[i];
            if (stack == null) continue;
            total += Mathf.Max(0, stack.amount);
        }

        return total;
    }

    public float GetShipInternalCargoMassKg(WorldConfigDatabase config)
    {
        if (config == null) return GetShipInternalCargoMassKg();

        float total = 0f;
        shipCargo ??= new List<ResourceStack>();
        for (int i = 0; i < shipCargo.Count; i++)
        {
            ResourceStack stack = shipCargo[i];
            if (stack == null) continue;
            total += config.GetItemTransportMassKg(stack.resourceId, stack.amount);
        }

        return total;
    }

    public int GetShipImpactCargoAmount(string resourceId)
    {
        ResourceStack stack = GetShipImpactCargoStack(resourceId, false);
        return stack != null ? stack.amount : 0;
    }

    public void AddShipImpactCargo(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;

        ResourceStack stack = GetShipImpactCargoStack(resourceId, true);
        stack.amount += amount;
    }

    public void SetShipImpactCargoAmount(string resourceId, int amount)
    {
        shipImpactCargo ??= new List<ResourceStack>();
        SetStackAmount(shipImpactCargo, resourceId, amount);
    }

    public bool TrySpendShipImpactCargo(string resourceId, int amount)
    {
        if (amount <= 0) return true;

        ResourceStack stack = GetShipImpactCargoStack(resourceId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        if (stack.amount <= 0)
        {
            shipImpactCargo.Remove(stack);
        }

        return true;
    }

    public int GetShipImpactCargoMassKg()
    {
        int total = 0;
        shipImpactCargo ??= new List<ResourceStack>();
        for (int i = 0; i < shipImpactCargo.Count; i++)
        {
            ResourceStack stack = shipImpactCargo[i];
            if (stack == null) continue;
            total += Mathf.Max(0, stack.amount);
        }

        return total;
    }

    public void ClearShipImpactCargo()
    {
        shipImpactCargo ??= new List<ResourceStack>();
        shipImpactCargo.Clear();
    }

    private void MigrateDeprecatedImpactCargoToShipCargo()
    {
        if (shipImpactCargo == null || shipImpactCargo.Count == 0) return;

        for (int i = 0; i < shipImpactCargo.Count; i++)
        {
            ResourceStack stack = shipImpactCargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            AddShipCargo(stack.resourceId, stack.amount);
        }

        shipImpactCargo.Clear();
    }

    public IslandProductionState GetIslandProductionState(string islandId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(islandId)) return null;
        islandProductions ??= new List<IslandProductionState>();

        for (int i = 0; i < islandProductions.Count; i++)
        {
            IslandProductionState state = islandProductions[i];
            if (state != null && state.islandId == islandId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        IslandProductionState newState = new IslandProductionState { islandId = islandId };
        islandProductions.Add(newState);
        return newState;
    }

    public GasCloudState GetGasCloudState(string cloudId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(cloudId)) return null;
        gasClouds ??= new List<GasCloudState>();

        for (int i = 0; i < gasClouds.Count; i++)
        {
            GasCloudState state = gasClouds[i];
            if (state != null && state.cloudId == cloudId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        GasCloudState newState = new GasCloudState { cloudId = cloudId };
        gasClouds.Add(newState);
        return newState;
    }

    public MiningRockState GetMiningRockState(string rockId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(rockId)) return null;
        miningRocks ??= new List<MiningRockState>();

        for (int i = 0; i < miningRocks.Count; i++)
        {
            MiningRockState state = miningRocks[i];
            if (state != null && state.rockId == rockId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        MiningRockState newState = new MiningRockState { rockId = rockId };
        miningRocks.Add(newState);
        return newState;
    }

    public MiningZoneState GetMiningZoneState(string zoneId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(zoneId)) return null;
        miningZones ??= new List<MiningZoneState>();

        for (int i = 0; i < miningZones.Count; i++)
        {
            MiningZoneState state = miningZones[i];
            if (state != null && state.zoneId == zoneId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        MiningZoneState newState = new MiningZoneState { zoneId = zoneId };
        miningZones.Add(newState);
        return newState;
    }

    public bool HasActiveProcess(string processId)
    {
        return GetActiveProcess(processId) != null;
    }

    public TimedProcessState GetActiveProcess(string processId)
    {
        if (string.IsNullOrWhiteSpace(processId)) return null;

        for (int i = 0; i < activeProcesses.Count; i++)
        {
            TimedProcessState process = activeProcesses[i];
            if (process != null && process.processId == processId)
            {
                return process;
            }
        }

        return null;
    }

    public bool AddActiveProcess(TimedProcessState process)
    {
        if (process == null || string.IsNullOrWhiteSpace(process.processId)) return false;
        if (HasActiveProcess(process.processId)) return false;

        process.Normalize();
        activeProcesses.Add(process);
        return true;
    }

    public bool RemoveActiveProcess(TimedProcessState process)
    {
        if (process == null) return false;
        return activeProcesses.Remove(process);
    }

    public bool AcceptMission(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId)) return false;
        if (acceptedMissionIds.Contains(missionId)) return false;

        acceptedMissionIds.Add(missionId);
        return true;
    }

    public bool CompleteMission(string missionId)
    {
        if (string.IsNullOrWhiteSpace(missionId)) return false;
        if (!completedMissionIds.Contains(missionId))
        {
            completedMissionIds.Add(missionId);
        }

        return true;
    }

    public bool IsMissionCompleted(string missionId)
    {
        return !string.IsNullOrWhiteSpace(missionId) && completedMissionIds.Contains(missionId);
    }

    public void SetDocked(string dockId, DockingLocationKind dockKind)
    {
        currentMode = GameSessionMode.Docked;
        currentDockId = string.IsNullOrWhiteSpace(dockId) ? "unknown_dock" : dockId;
        currentDockKind = dockKind;
        activeFlightMissionId = "";
        hasCurrentFlightPose = false;
    }

    public void SetDocked(string dockId, DockingLocationKind dockKind, Vector3 dockPosition)
    {
        SetDocked(dockId, dockKind);
        currentDockPosition = dockPosition;
        hasCurrentDockPosition = true;
    }

    public void SetFlight(string missionId)
    {
        currentMode = GameSessionMode.Flight;
        activeFlightMissionId = missionId ?? "";
    }

    public void SetFlightPose(Vector3 position, Quaternion rotation)
    {
        currentFlightPosition = position;
        currentFlightRotation = rotation;
        hasCurrentFlightPose = true;
    }

    public PlayerProgress Clone()
    {
        string json = JsonUtility.ToJson(this);
        PlayerProgress clone = JsonUtility.FromJson<PlayerProgress>(json);
        if (clone == null) clone = new PlayerProgress();
        clone.Normalize();
        return clone;
    }

    public void CopyFrom(PlayerProgress source)
    {
        if (source == null) return;

        string json = JsonUtility.ToJson(source);
        JsonUtility.FromJsonOverwrite(json, this);
        Normalize();
    }

    private ShipExperienceWallet GetShipExperienceWallet(string shipId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;

        for (int i = 0; i < shipExperience.Count; i++)
        {
            ShipExperienceWallet wallet = shipExperience[i];
            if (wallet != null && wallet.shipId == shipId)
            {
                return wallet;
            }
        }

        if (!createIfMissing) return null;

        ShipExperienceWallet newWallet = new ShipExperienceWallet { shipId = shipId };
        shipExperience.Add(newWallet);
        return newWallet;
    }

    private InstalledModuleState GetInstalledModuleState(string slotId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(slotId)) return null;
        installedModules ??= new List<InstalledModuleState>();

        for (int i = 0; i < installedModules.Count; i++)
        {
            InstalledModuleState state = installedModules[i];
            if (state != null && state.slotId == slotId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        InstalledModuleState newState = new InstalledModuleState { slotId = slotId };
        installedModules.Add(newState);
        return newState;
    }

    private ResourceStack GetResourceStack(string resourceId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return null;

        for (int i = 0; i < inventory.Count; i++)
        {
            ResourceStack stack = inventory[i];
            if (stack != null && stack.resourceId == resourceId)
            {
                return stack;
            }
        }

        if (!createIfMissing) return null;

        ResourceStack newStack = new ResourceStack { resourceId = resourceId };
        inventory.Add(newStack);
        return newStack;
    }

    private ResourceStack GetShipCargoStack(string resourceId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return null;
        shipCargo ??= new List<ResourceStack>();

        for (int i = 0; i < shipCargo.Count; i++)
        {
            ResourceStack stack = shipCargo[i];
            if (stack != null && stack.resourceId == resourceId)
            {
                return stack;
            }
        }

        if (!createIfMissing) return null;

        ResourceStack newStack = new ResourceStack { resourceId = resourceId };
        shipCargo.Add(newStack);
        return newStack;
    }

    private ResourceStack GetShipImpactCargoStack(string resourceId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return null;
        shipImpactCargo ??= new List<ResourceStack>();

        for (int i = 0; i < shipImpactCargo.Count; i++)
        {
            ResourceStack stack = shipImpactCargo[i];
            if (stack != null && stack.resourceId == resourceId)
            {
                return stack;
            }
        }

        if (!createIfMissing) return null;

        ResourceStack newStack = new ResourceStack { resourceId = resourceId };
        shipImpactCargo.Add(newStack);
        return newStack;
    }

    private static void SetStackAmount(List<ResourceStack> list, string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return;
        if (list == null) return;

        ResourceStack existing = null;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = list[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId))
            {
                list.RemoveAt(i);
                continue;
            }

            if (stack.resourceId == resourceId)
            {
                existing = stack;
            }
        }

        if (amount <= 0)
        {
            if (existing != null)
            {
                list.Remove(existing);
            }

            return;
        }

        if (existing == null)
        {
            existing = new ResourceStack { resourceId = resourceId };
            list.Add(existing);
        }

        existing.amount = amount;
    }
}

public enum ScoutedObjectKind
{
    [InspectorName("Облако")]
    GasCloud,
    [InspectorName("Глыба")]
    MiningRock,
    [InspectorName("Левиафан")]
    Leviathan
}

[Serializable]
public class ScoutedObjectState
{
    public ScoutedObjectKind kind;
    public string objectId = "";
    public string displayName = "";
    public bool coordinatesKnown;
    public Vector3 lastKnownPosition;
    public float factsProgress;
    public float factsRequired = 10f;
    public bool factsComplete;
    public long factsUpdatedUtcTicks;
    public float informationPotentialKg;
    public float informationExtractedKg;
    public float informationBufferKg;
    public float informationPaperSpendBufferKg;
    public long informationUpdatedUtcTicks;
    public string typeId = "";
    public string zoneId = "";
    public string resourceId = "";
    public string summaryRu = "";

    public void Normalize()
    {
        objectId ??= "";
        displayName ??= "";
        factsRequired = Mathf.Max(1f, factsRequired);
        factsProgress = Mathf.Clamp(factsProgress, 0f, factsRequired);
        informationPotentialKg = Mathf.Max(0f, informationPotentialKg);
        informationExtractedKg = Mathf.Clamp(informationExtractedKg, 0f, Mathf.Max(informationExtractedKg, informationPotentialKg));
        informationBufferKg = Mathf.Clamp(informationBufferKg, 0f, 0.999f);
        informationPaperSpendBufferKg = Mathf.Clamp(informationPaperSpendBufferKg, 0f, 999f);
        if (factsUpdatedUtcTicks < 0) factsUpdatedUtcTicks = 0;
        if (informationUpdatedUtcTicks < 0) informationUpdatedUtcTicks = 0;
        typeId ??= "";
        zoneId ??= "";
        resourceId ??= "";
        summaryRu ??= "";
        if (factsProgress >= factsRequired - 0.001f)
        {
            factsComplete = true;
        }
    }
}

public enum ScoutShipStatus
{
    Idle,
    Traveling,
    Observing,
    ReturningHome,
    Unloading,
    WaitingForPaper,
    Fleeing,
    Error
}

[Serializable]
public class ScoutShipState
{
    public string shipId = "";
    public string displayName = "";
    public string homeIslandId = "capital";
    public ScoutShipStatus status = ScoutShipStatus.Idle;
    public ScoutedObjectKind targetKind = ScoutedObjectKind.GasCloud;
    public string targetObjectId = "";
    public Vector3 lastKnownPosition;
    public Vector3 targetPosition;
    public long nextEventUtcTicks;
    public List<ResourceStack> cargo = new List<ResourceStack>();
    public int completedSurveyRuns;
    public string lastError = "";

    public void Normalize()
    {
        shipId ??= "";
        displayName ??= "";
        homeIslandId ??= "";
        targetObjectId ??= "";
        cargo ??= new List<ResourceStack>();
        completedSurveyRuns = Mathf.Max(0, completedSurveyRuns);
        lastError ??= "";
        if (nextEventUtcTicks < 0) nextEventUtcTicks = 0;

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

[Serializable]
public class GasCloudState
{
    public string cloudId = "";
    public bool initialized;
    public float remainingVolumeLiters;

    public void Normalize()
    {
        cloudId ??= "";
        remainingVolumeLiters = Mathf.Max(0f, remainingVolumeLiters);
    }
}

public enum GasHarvesterShipStatus
{
    Idle,
    FlyingToCloud,
    Harvesting,
    Returning,
    Unloading,
    WaitingForResources,
    Error
}

[Serializable]
public class GasHarvesterShipState
{
    public string shipId = "";
    public string displayName = "";
    public string homeIslandId = "capital";
    public GasHarvesterShipStatus status = GasHarvesterShipStatus.Idle;
    public string targetCloudId = "";
    public long nextEventUtcTicks;
    public long flightStartedUtcTicks;
    public long flightArrivesUtcTicks;
    public Vector3 lastKnownPosition;
    public List<ResourceStack> cargo = new List<ResourceStack>();
    public float harvestBufferKg;
    public float pendingFuelConsumptionKg;
    public float pendingClaudiumConsumptionKg;
    public int completedTrips;
    public string lastError = "";

    public void Normalize()
    {
        shipId ??= "";
        displayName ??= "";
        homeIslandId ??= "";
        targetCloudId ??= "";
        cargo ??= new List<ResourceStack>();
        harvestBufferKg = Mathf.Max(0f, harvestBufferKg);
        pendingFuelConsumptionKg = Mathf.Max(0f, pendingFuelConsumptionKg);
        pendingClaudiumConsumptionKg = Mathf.Max(0f, pendingClaudiumConsumptionKg);
        completedTrips = Mathf.Max(0, completedTrips);
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

[Serializable]
public class MiningZoneState
{
    public string zoneId = "";
    public long lastSpawnUtcTicks;
    public int spawnCounter;
    public bool initialRocksSpawned;

    public void Normalize()
    {
        zoneId ??= "";
        if (lastSpawnUtcTicks < 0) lastSpawnUtcTicks = 0;
        spawnCounter = Mathf.Max(0, spawnCounter);
    }
}

[Serializable]
public class MiningRockState
{
    public string rockId = "";
    public string zoneId = "";
    public string oreTypeId = "";
    public long spawnedUtcTicks;
    public Vector3 spawnPosition;
    public float remainingOreKg;
    public float shedBufferKg;
    public long nextNaturalShedUtcTicks;
    public long lastNaturalShedUtcTicks;
    public int lastNaturalShedAmountKg;
    public string lastNaturalShedOreItemId = "";
    public int shedEventCounter;
    public bool impactedIsland;

    public void Normalize()
    {
        rockId ??= "";
        zoneId ??= "";
        oreTypeId ??= "";
        if (spawnedUtcTicks < 0) spawnedUtcTicks = 0;
        remainingOreKg = Mathf.Max(0f, remainingOreKg);
        shedBufferKg = Mathf.Max(0f, shedBufferKg);
        if (nextNaturalShedUtcTicks < 0) nextNaturalShedUtcTicks = 0;
        if (lastNaturalShedUtcTicks < 0) lastNaturalShedUtcTicks = 0;
        lastNaturalShedAmountKg = Mathf.Max(0, lastNaturalShedAmountKg);
        lastNaturalShedOreItemId ??= "";
        shedEventCounter = Mathf.Max(0, shedEventCounter);
    }
}

public enum MiningShipStatus
{
    Idle,
    FlyingToRock,
    Mining,
    Returning,
    Unloading,
    WaitingForResources,
    Error
}

[Serializable]
public class MiningShipState
{
    public string shipId = "";
    public string displayName = "";
    public string homeIslandId = "capital";
    public MiningShipStatus status = MiningShipStatus.Idle;
    public string targetRockId = "";
    public string targetOreTypeId = "";
    public long nextEventUtcTicks;
    public long flightStartedUtcTicks;
    public long flightArrivesUtcTicks;
    public long miningStartedUtcTicks;
    public long lastObservedShedUtcTicks;
    public Vector3 lastKnownPosition;
    public Vector3 miningPosition;
    public List<ResourceStack> cargo = new List<ResourceStack>();
    public float miningBufferKg;
    public float pendingFuelConsumptionKg;
    public float pendingClaudiumConsumptionKg;
    public int completedTrips;
    public string lastError = "";

    public void Normalize()
    {
        shipId ??= "";
        displayName ??= "";
        homeIslandId ??= "";
        targetRockId ??= "";
        targetOreTypeId ??= "";
        cargo ??= new List<ResourceStack>();
        miningBufferKg = Mathf.Max(0f, miningBufferKg);
        pendingFuelConsumptionKg = Mathf.Max(0f, pendingFuelConsumptionKg);
        pendingClaudiumConsumptionKg = Mathf.Max(0f, pendingClaudiumConsumptionKg);
        if (miningStartedUtcTicks < 0) miningStartedUtcTicks = 0;
        if (lastObservedShedUtcTicks < 0) lastObservedShedUtcTicks = 0;
        completedTrips = Mathf.Max(0, completedTrips);
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

[Serializable]
public class IslandProductionState
{
    public string islandId = "";
    public List<ResourceStack> storage = new List<ResourceStack>();
    public float productionProgress;
    public List<IslandConsumptionState> consumptions = new List<IslandConsumptionState>();
    public List<IslandIndustryState> industries = new List<IslandIndustryState>();
    public List<IslandSocietyNeedState> societyNeeds = new List<IslandSocietyNeedState>();
    public IslandDevelopmentState development = new IslandDevelopmentState();
    public float passengersToCapitalProgress;
    public float passengersFromCapitalProgress;

    public void Normalize()
    {
        islandId ??= "";
        storage ??= new List<ResourceStack>();
        consumptions ??= new List<IslandConsumptionState>();
        industries ??= new List<IslandIndustryState>();
        societyNeeds ??= new List<IslandSocietyNeedState>();
        development ??= new IslandDevelopmentState();
        development.Normalize();
        productionProgress = Mathf.Max(0f, productionProgress);
        passengersToCapitalProgress = Mathf.Max(0f, passengersToCapitalProgress);
        passengersFromCapitalProgress = Mathf.Max(0f, passengersFromCapitalProgress);

        for (int i = storage.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = storage[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId))
            {
                storage.RemoveAt(i);
                continue;
            }

            stack.amount = Mathf.Max(0, stack.amount);
        }

        for (int i = consumptions.Count - 1; i >= 0; i--)
        {
            IslandConsumptionState consumption = consumptions[i];
            if (consumption == null || string.IsNullOrWhiteSpace(consumption.itemId))
            {
                consumptions.RemoveAt(i);
                continue;
            }

            consumption.Normalize();
        }

        for (int i = industries.Count - 1; i >= 0; i--)
        {
            IslandIndustryState industry = industries[i];
            if (industry == null || string.IsNullOrWhiteSpace(industry.industryId))
            {
                industries.RemoveAt(i);
                continue;
            }

            industry.Normalize();
        }

        for (int i = societyNeeds.Count - 1; i >= 0; i--)
        {
            IslandSocietyNeedState need = societyNeeds[i];
            if (need == null || string.IsNullOrWhiteSpace(need.needId))
            {
                societyNeeds.RemoveAt(i);
                continue;
            }

            need.Normalize();
        }
    }

    public int GetResourceAmount(string resourceId)
    {
        ResourceStack stack = GetResourceStack(resourceId, false);
        return stack != null ? stack.amount : 0;
    }

    public int AddResource(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return 0;

        ResourceStack stack = GetResourceStack(resourceId, true);
        stack.amount += amount;
        return amount;
    }

    public void SetResourceAmount(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return;
        storage ??= new List<ResourceStack>();

        ResourceStack existing = null;
        for (int i = storage.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = storage[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId))
            {
                storage.RemoveAt(i);
                continue;
            }

            if (stack.resourceId == resourceId)
            {
                existing = stack;
            }
        }

        if (amount <= 0)
        {
            if (existing != null)
            {
                storage.Remove(existing);
            }

            return;
        }

        if (existing == null)
        {
            existing = new ResourceStack { resourceId = resourceId };
            storage.Add(existing);
        }

        existing.amount = amount;
    }

    public int AddResource(string resourceId, int amount, int capacity)
    {
        return AddResource(resourceId, amount);
    }

    public bool TrySpendResource(string resourceId, int amount)
    {
        if (amount <= 0) return true;

        ResourceStack stack = GetResourceStack(resourceId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        return true;
    }

    public IslandConsumptionState GetConsumptionState(string itemId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        consumptions ??= new List<IslandConsumptionState>();

        for (int i = 0; i < consumptions.Count; i++)
        {
            IslandConsumptionState state = consumptions[i];
            if (state != null && state.itemId == itemId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        IslandConsumptionState newState = new IslandConsumptionState { itemId = itemId };
        consumptions.Add(newState);
        return newState;
    }

    public IslandIndustryState GetIndustryState(string industryId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(industryId)) return null;
        industries ??= new List<IslandIndustryState>();

        for (int i = 0; i < industries.Count; i++)
        {
            IslandIndustryState state = industries[i];
            if (state != null && state.industryId == industryId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        IslandIndustryState newState = new IslandIndustryState { industryId = industryId };
        industries.Add(newState);
        return newState;
    }

    public IslandSocietyNeedState GetSocietyNeedState(string needId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(needId)) return null;
        societyNeeds ??= new List<IslandSocietyNeedState>();

        for (int i = 0; i < societyNeeds.Count; i++)
        {
            IslandSocietyNeedState state = societyNeeds[i];
            if (state != null && state.needId == needId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        IslandSocietyNeedState newState = new IslandSocietyNeedState { needId = needId };
        societyNeeds.Add(newState);
        return newState;
    }

    public IslandBuildingState GetBuildingState(string buildingId, bool createIfMissing)
    {
        development ??= new IslandDevelopmentState();
        return development.GetBuildingState(buildingId, createIfMissing);
    }

    private ResourceStack GetResourceStack(string resourceId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) return null;
        storage ??= new List<ResourceStack>();

        for (int i = 0; i < storage.Count; i++)
        {
            ResourceStack stack = storage[i];
            if (stack != null && stack.resourceId == resourceId)
            {
                return stack;
            }
        }

        if (!createIfMissing) return null;

        ResourceStack newStack = new ResourceStack { resourceId = resourceId };
        storage.Add(newStack);
        return newStack;
    }
}

[Serializable]
public class IslandConsumptionState
{
    public string itemId = "";
    public float consumptionProgress;
    public bool isSatisfied;

    public void Normalize()
    {
        itemId ??= "";
        consumptionProgress = Mathf.Max(0f, consumptionProgress);
    }
}

[Serializable]
public class IslandSocietyNeedState
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

[Serializable]
public enum IslandConstructionProjectKind
{
    Build,
    Upgrade,
    Modernization
}

[Serializable]
public class IslandDevelopmentState
{
    public string archetypeId = "";
    public int completedStage;
    public bool socialNeedsUnlocked;
    public long constructionRecoveryUntilUtcTicks;
    public IslandConstructionProjectState constructionProject = new IslandConstructionProjectState();
    public List<IslandBuildingState> buildings = new List<IslandBuildingState>();

    public void Normalize()
    {
        archetypeId ??= "";
        completedStage = Mathf.Max(0, completedStage);
        if (constructionRecoveryUntilUtcTicks < 0) constructionRecoveryUntilUtcTicks = 0;
        constructionProject ??= new IslandConstructionProjectState();
        constructionProject.Normalize();
        buildings ??= new List<IslandBuildingState>();

        for (int i = buildings.Count - 1; i >= 0; i--)
        {
            IslandBuildingState building = buildings[i];
            if (building == null || string.IsNullOrWhiteSpace(building.buildingId))
            {
                buildings.RemoveAt(i);
                continue;
            }

            building.Normalize();
        }
    }

    public IslandBuildingState GetBuildingState(string buildingId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(buildingId)) return null;
        buildings ??= new List<IslandBuildingState>();

        for (int i = 0; i < buildings.Count; i++)
        {
            IslandBuildingState state = buildings[i];
            if (state != null && state.buildingId == buildingId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        IslandBuildingState newState = new IslandBuildingState { buildingId = buildingId };
        buildings.Add(newState);
        return newState;
    }

    public bool HasAnyBuiltBuildings()
    {
        if (buildings == null) return false;

        for (int i = 0; i < buildings.Count; i++)
        {
            IslandBuildingState building = buildings[i];
            if (building != null && building.built)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsBuildingBuilt(string buildingId)
    {
        IslandBuildingState building = GetBuildingState(buildingId, false);
        return building != null && building.built;
    }
}

[Serializable]
public class IslandConstructionProjectState
{
    public bool active;
    public IslandConstructionProjectKind kind = IslandConstructionProjectKind.Build;
    public string buildingId = "";
    public int targetLevel;
    public int activeStepIndex;
    public long cycleStartedUtcTicks;
    public long nextCompletionUtcTicks;
    public string lastMessage = "";

    public void Normalize()
    {
        buildingId ??= "";
        targetLevel = Mathf.Max(0, targetLevel);
        activeStepIndex = Mathf.Max(0, activeStepIndex);
        if (cycleStartedUtcTicks < 0) cycleStartedUtcTicks = 0;
        if (nextCompletionUtcTicks < 0) nextCompletionUtcTicks = 0;
        lastMessage ??= "";

        if (!active)
        {
            activeStepIndex = 0;
            cycleStartedUtcTicks = 0;
            nextCompletionUtcTicks = 0;
        }
    }
}

[Serializable]
public class IslandBuildingState
{
    public string buildingId = "";
    public bool built;
    public int level;
    public int modernizationLevel;

    public void Normalize()
    {
        buildingId ??= "";
        level = Mathf.Max(0, level);
        modernizationLevel = Mathf.Clamp(modernizationLevel, 0, 5);

        if (built && level <= 0)
        {
            level = 1;
        }

        if (!built)
        {
            level = 0;
            modernizationLevel = 0;
        }
    }
}

[Serializable]
public class IslandIndustryState
{
    public string industryId = "";
    public string recipeId = "";
    public IslandIndustryKind kind = IslandIndustryKind.Generation;
    public bool active;
    public int activeStepIndex;
    public long cycleStartedUtcTicks;
    public long nextCompletionUtcTicks;
    public float productionProgress;
    public string activeInputResourceId = "";
    public float reactionSpeedMultiplier = 1f;
    public float activeReactionSuccessChance = 1f;
    public float conversionMultiplier = 1f;
    public int completedCycles;
    public int failedCycles;
    public string lastMessage = "";
    public List<ProductionOutputBufferState> outputBuffers = new List<ProductionOutputBufferState>();

    public void Normalize()
    {
        industryId ??= "";
        recipeId ??= "";
        activeStepIndex = Mathf.Max(0, activeStepIndex);
        if (cycleStartedUtcTicks < 0) cycleStartedUtcTicks = 0;
        if (nextCompletionUtcTicks < 0) nextCompletionUtcTicks = 0;
        productionProgress = Mathf.Max(0f, productionProgress);
        activeInputResourceId ??= "";
        reactionSpeedMultiplier = Mathf.Clamp(reactionSpeedMultiplier <= 0f ? 1f : reactionSpeedMultiplier, 1f, 50f);
        activeReactionSuccessChance = Mathf.Clamp01(activeReactionSuccessChance);
        conversionMultiplier = Mathf.Max(1f, conversionMultiplier <= 0f ? 1f : conversionMultiplier);
        completedCycles = Mathf.Max(0, completedCycles);
        failedCycles = Mathf.Max(0, failedCycles);
        lastMessage ??= "";
        outputBuffers ??= new List<ProductionOutputBufferState>();

        for (int i = outputBuffers.Count - 1; i >= 0; i--)
        {
            ProductionOutputBufferState buffer = outputBuffers[i];
            if (buffer == null || string.IsNullOrWhiteSpace(buffer.itemId))
            {
                outputBuffers.RemoveAt(i);
                continue;
            }

            buffer.Normalize();
        }
    }

    public ProductionOutputBufferState GetOutputBuffer(string itemId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        outputBuffers ??= new List<ProductionOutputBufferState>();

        for (int i = 0; i < outputBuffers.Count; i++)
        {
            ProductionOutputBufferState buffer = outputBuffers[i];
            if (buffer != null && buffer.itemId == itemId)
            {
                return buffer;
            }
        }

        if (!createIfMissing) return null;

        ProductionOutputBufferState newBuffer = new ProductionOutputBufferState { itemId = itemId };
        outputBuffers.Add(newBuffer);
        return newBuffer;
    }
}

[Serializable]
public class ProductionOutputBufferState
{
    public string itemId = "";
    public float amount;

    public void Normalize()
    {
        itemId ??= "";
        amount = Mathf.Max(0f, amount);
    }
}

[Serializable]
public class CargoTransferState
{
    public bool active;
    public string islandId = "";
    public long startedUtcTicks;
    public long nextOperationUtcTicks;
    public float secondsPerItem = 1f;
    public int currentOperationIndex;
    public List<CargoTransferOperation> operations = new List<CargoTransferOperation>();

    public bool HasWork => active && operations != null && currentOperationIndex >= 0 && currentOperationIndex < operations.Count;

    public void Normalize()
    {
        islandId ??= "";
        secondsPerItem = Mathf.Max(0.01f, secondsPerItem);
        operations ??= new List<CargoTransferOperation>();
        currentOperationIndex = Mathf.Max(0, currentOperationIndex);

        for (int i = operations.Count - 1; i >= 0; i--)
        {
            CargoTransferOperation operation = operations[i];
            if (operation == null)
            {
                operations.RemoveAt(i);
                continue;
            }

            operation.Normalize();
            if (string.IsNullOrWhiteSpace(operation.itemId) || operation.remainingAmount <= 0)
            {
                operations.RemoveAt(i);
            }
        }

        if (currentOperationIndex >= operations.Count)
        {
            active = false;
            currentOperationIndex = 0;
        }
    }

    public int GetRemainingUnits()
    {
        if (operations == null) return 0;

        int total = 0;
        for (int i = currentOperationIndex; i < operations.Count; i++)
        {
            CargoTransferOperation operation = operations[i];
            if (operation == null) continue;
            total += Mathf.Max(0, operation.remainingAmount);
        }

        return total;
    }
}

[Serializable]
public class CargoTransferOperation
{
    public string itemId = "";
    public bool loadToShip;
    public int remainingAmount;

    public void Normalize()
    {
        itemId ??= "";
        remainingAmount = Mathf.Max(0, remainingAmount);
    }
}

[Serializable]
public class InstalledModuleState
{
    public string slotId = "";
    public string moduleId = "";

    public void Normalize()
    {
        slotId ??= "";
        moduleId ??= "";
    }
}

[Serializable]
public class ShipExperienceWallet
{
    public string shipId = "";
    public int experience;
}

[Serializable]
public class TechnologyResearchProgress
{
    public string technologyId = "";
    public int completedCycles;
    public long activeCycleStartUtcTicks;
    public long activeCycleEndUtcTicks;

    public bool HasActiveCycle => activeCycleEndUtcTicks > 0;

    public void Normalize()
    {
        technologyId ??= "";
        completedCycles = Mathf.Max(0, completedCycles);
        if (activeCycleEndUtcTicks < 0) activeCycleEndUtcTicks = 0;
        if (activeCycleStartUtcTicks < 0) activeCycleStartUtcTicks = 0;
        if (activeCycleEndUtcTicks == 0) activeCycleStartUtcTicks = 0;
    }
}

[Serializable]
public class ResourceStack
{
    public string resourceId = "";
    public int amount;
}

[Serializable]
public class TimedProcessState
{
    public string processId = "";
    public string displayName = "";
    public TimedProcessKind kind = TimedProcessKind.Crafting;
    public long startedUtcTicks;
    public long nextCompletionUtcTicks;
    public int durationSeconds = 60;
    public bool repeat;
    public int remainingCycles = 1;

    public string inputResourceId = "";
    public int inputAmount;
    public string outputResourceId = "";
    public int outputAmount;

    public string missionId = "";
    public int rewardMoney;
    public int rewardExperience;
    public string experienceShipId = "";

    public void Normalize()
    {
        processId ??= "";
        displayName ??= "";
        inputResourceId ??= "";
        outputResourceId ??= "";
        missionId ??= "";
        experienceShipId ??= "";
        durationSeconds = Mathf.Max(1, durationSeconds);
    }
}
