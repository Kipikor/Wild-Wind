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
    public string selectedShipId = "";
    public List<string> unlockedShipIds = new List<string>();
    public List<string> researchedNodeIds = new List<string>();
    public List<string> purchasedNodeIds = new List<string>();
    public List<ShipExperienceWallet> shipExperience = new List<ShipExperienceWallet>();

    public GameSessionMode currentMode = GameSessionMode.Docked;
    public DockingLocationKind currentDockKind = DockingLocationKind.Island;
    public string currentDockId = "starter_island";
    public bool hasCurrentDockPosition;
    public Vector3 currentDockPosition;
    public string activeFlightMissionId = "";
    public long lastSavedUtcTicks;
    public long lastProcessUtcTicks;
    public long nextShopRefreshUtcTicks;
    public int shopSeed;
    public bool receivedStartingInventory;

    public List<ResourceStack> inventory = new List<ResourceStack>();
    public List<TimedProcessState> activeProcesses = new List<TimedProcessState>();
    public List<string> acceptedMissionIds = new List<string>();
    public List<string> completedMissionIds = new List<string>();

    public void Normalize()
    {
        selectedShipId ??= "";
        currentDockId ??= "";
        activeFlightMissionId ??= "";
        unlockedShipIds ??= new List<string>();
        researchedNodeIds ??= new List<string>();
        purchasedNodeIds ??= new List<string>();
        shipExperience ??= new List<ShipExperienceWallet>();
        inventory ??= new List<ResourceStack>();
        activeProcesses ??= new List<TimedProcessState>();
        acceptedMissionIds ??= new List<string>();
        completedMissionIds ??= new List<string>();

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
    }

    public bool IsShipUnlocked(string shipId)
    {
        return !string.IsNullOrWhiteSpace(shipId) && unlockedShipIds.Contains(shipId);
    }

    public void EnsureStarterShip(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return;

        UnlockShip(shipId);

        if (string.IsNullOrWhiteSpace(selectedShipId))
        {
            selectedShipId = shipId;
        }
    }

    public bool UnlockShip(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return false;
        if (unlockedShipIds.Contains(shipId)) return false;

        unlockedShipIds.Add(shipId);
        return true;
    }

    public bool SelectShip(string shipId)
    {
        if (!IsShipUnlocked(shipId)) return false;

        selectedShipId = shipId;
        return true;
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

    public bool TrySpendResource(string resourceId, int amount)
    {
        if (amount <= 0) return true;

        ResourceStack stack = GetResourceStack(resourceId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        return true;
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
}

[Serializable]
public class ShipExperienceWallet
{
    public string shipId = "";
    public int experience;
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
