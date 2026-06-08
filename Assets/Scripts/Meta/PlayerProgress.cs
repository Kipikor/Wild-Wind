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

[Serializable]
public class PlayerProgress
{
    public string selectedHullId = "";
    public List<string> completedTechnologyIds = new List<string>();
    public string activeResearchTechnologyId = "";
    public List<TechnologyResearchProgress> technologyResearchProgress = new List<TechnologyResearchProgress>();
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public SortieSessionState activeSortie = new SortieSessionState();
    public BaseExtractionIndustryState baseIndustry = new BaseExtractionIndustryState();
    public string selectedSortieId = SessionExtractionConstants.DefaultSafeOreSortieId;

    public GameSessionMode currentMode = GameSessionMode.Docked;
    public string currentDockId = "capital";
    public bool hasCurrentDockPosition;
    public Vector3 currentDockPosition;
    public bool hasCurrentFlightPose;
    public Vector3 currentFlightPosition;
    public Quaternion currentFlightRotation = Quaternion.identity;
    public long lastProcessUtcTicks;
    public bool receivedStartingInventory;
    public float shipWeaponSpendBufferKg;
    public ShipConsumableTankState shipEngineFuelTank = new ShipConsumableTankState { resourceId = "charcoal" };
    public ShipConsumableTankState shipClaudiumTank = new ShipConsumableTankState { resourceId = "claudium" };

    public List<ResourceStack> inventory = new List<ResourceStack>();
    public List<ResourceStack> shipCargo = new List<ResourceStack>();
    public List<PortStorageState> portStorages = new List<PortStorageState>();

    public void Normalize()
    {
        selectedHullId ??= "";
        activeResearchTechnologyId ??= "";
        currentDockId ??= "";
        selectedSortieId = string.IsNullOrWhiteSpace(selectedSortieId)
            ? SessionExtractionConstants.DefaultSafeOreSortieId
            : selectedSortieId.Trim();
        if (currentFlightRotation.x == 0f
            && currentFlightRotation.y == 0f
            && currentFlightRotation.z == 0f
            && currentFlightRotation.w == 0f)
        {
            currentFlightRotation = Quaternion.identity;
        }

        completedTechnologyIds ??= new List<string>();
        technologyResearchProgress ??= new List<TechnologyResearchProgress>();
        installedModules ??= new List<InstalledModuleState>();
        activeSortie ??= new SortieSessionState();
        baseIndustry ??= new BaseExtractionIndustryState();
        shipEngineFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        inventory ??= new List<ResourceStack>();
        shipCargo ??= new List<ResourceStack>();
        portStorages ??= new List<PortStorageState>();
        activeSortie.Normalize();
        baseIndustry.Normalize();
        shipEngineFuelTank.Normalize();
        shipClaudiumTank.Normalize();

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

        for (int i = portStorages.Count - 1; i >= 0; i--)
        {
            PortStorageState port = portStorages[i];
            if (port == null)
            {
                portStorages.RemoveAt(i);
                continue;
            }

            port.Normalize();
            if (string.IsNullOrWhiteSpace(port.portId))
            {
                portStorages.RemoveAt(i);
            }
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
    }

    public void ClearShipConsumableTanks()
    {
        shipEngineFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        shipEngineFuelTank.amountKg = 0f;
        shipClaudiumTank.amountKg = 0f;
    }

    public bool IsTechnologyCompleted(string technologyId)
    {
        return !string.IsNullOrWhiteSpace(technologyId) && completedTechnologyIds.Contains(technologyId);
    }

    public bool CompleteTechnology(string technologyId)
    {
        if (string.IsNullOrWhiteSpace(technologyId)) return false;
        if (completedTechnologyIds.Contains(technologyId)) return false;

        completedTechnologyIds.Add(technologyId);
        return true;
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

    public float GetShipCargoMassKg(SessionConfigDatabase config)
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

    public float GetShipInternalCargoMassKg(SessionConfigDatabase config)
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

    public float GetShipConsumableTankMassKg()
    {
        shipEngineFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        return shipEngineFuelTank.amountKg + shipClaudiumTank.amountKg;
    }

    public float GetShipPayloadMassKg(SessionConfigDatabase config)
    {
        return GetShipInternalCargoMassKg(config) + GetShipConsumableTankMassKg();
    }

    public PortStorageState GetPortStorageState(string portId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(portId)) return null;
        portStorages ??= new List<PortStorageState>();

        for (int i = 0; i < portStorages.Count; i++)
        {
            PortStorageState state = portStorages[i];
            if (state != null && state.portId == portId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        PortStorageState newState = new PortStorageState { portId = portId };
        portStorages.Add(newState);
        return newState;
    }


    public void SetDocked(string dockId)
    {
        currentMode = GameSessionMode.Docked;
        currentDockId = string.IsNullOrWhiteSpace(dockId) ? "unknown_dock" : dockId;
        hasCurrentFlightPose = false;
        activeSortie?.Clear();
    }

    public void SetDocked(string dockId, Vector3 dockPosition)
    {
        SetDocked(dockId);
        currentDockPosition = dockPosition;
        hasCurrentDockPosition = true;
    }

    public void SetFlight()
    {
        currentMode = GameSessionMode.Flight;
    }

    public void SetFlightPose(Vector3 position, Quaternion rotation)
    {
        currentFlightPosition = position;
        currentFlightRotation = rotation;
        hasCurrentFlightPose = true;
        RememberSortiePosition(position);
    }

    public bool HasActiveSortie => activeSortie != null && activeSortie.active;

    public void BeginSortie(SortieZoneDefinition definition, long utcTicks, string dockId, Vector3 dockPosition)
    {
        activeSortie ??= new SortieSessionState();
        activeSortie.Begin(definition, utcTicks, dockId, dockPosition);
    }

    public void ClearActiveSortie()
    {
        activeSortie ??= new SortieSessionState();
        activeSortie.Clear();
    }

    public void RememberSortiePosition(Vector3 position)
    {
        if (activeSortie == null || !activeSortie.active) return;

        activeSortie.RememberPosition(position);
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

[Serializable]
public class PortStorageState
{
    public string portId = "";
    public List<ResourceStack> storage = new List<ResourceStack>();

    public void Normalize()
    {
        portId ??= "";
        storage ??= new List<ResourceStack>();

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
public class ShipConsumableTankState
{
    public string resourceId = "";
    public float amountKg;

    public void Normalize()
    {
        resourceId ??= "";
        amountKg = Mathf.Max(0f, amountKg);
    }

    public float GetAmount(string requestedResourceId)
    {
        if (string.IsNullOrWhiteSpace(requestedResourceId)) return 0f;
        return resourceId == requestedResourceId ? Mathf.Max(0f, amountKg) : 0f;
    }

    public void SetResource(string requestedResourceId)
    {
        requestedResourceId ??= "";
        if (resourceId == requestedResourceId) return;

        resourceId = requestedResourceId;
        amountKg = 0f;
    }

    public float Add(string requestedResourceId, float amount, float capacityKg)
    {
        if (string.IsNullOrWhiteSpace(requestedResourceId) || amount <= 0f) return 0f;

        SetResource(requestedResourceId);
        float freeKg = Mathf.Max(0f, capacityKg - amountKg);
        float added = Mathf.Min(freeKg, amount);
        amountKg += added;
        return added;
    }

    public bool TrySpend(string requestedResourceId, float amount)
    {
        if (amount <= 0f) return true;
        if (string.IsNullOrWhiteSpace(requestedResourceId) || resourceId != requestedResourceId) return false;
        if (amountKg + 0.0001f < amount) return false;

        amountKg = Mathf.Max(0f, amountKg - amount);
        return true;
    }
}
