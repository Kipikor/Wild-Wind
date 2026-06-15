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
    public List<string> unlockedKnowledgeIds = new List<string>();
    public string activeResearchTechnologyId = "";
    public List<TechnologyResearchProgress> technologyResearchProgress = new List<TechnologyResearchProgress>();
    public List<KnowledgeSpPackageState> knowledgeSpPackages = new List<KnowledgeSpPackageState>();
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public SortieSessionState activeSortie = new SortieSessionState();
    public BaseExtractionIndustryState baseIndustry = new BaseExtractionIndustryState();
    public CourierServiceState courierService = new CourierServiceState();
    public List<QuestState> questStates = new List<QuestState>();
    public List<QuestMetricState> questMetrics = new List<QuestMetricState>();
    public List<BaseIslandExpansionRegionState> baseIslandExpansionRegions = new List<BaseIslandExpansionRegionState>();
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
    public bool receivedStartingProcessingSamples;
    public bool receivedStartingExpansionCurrency;
    public bool receivedStartingCourierSupplies;
    public float shipWeaponSpendBufferKg;
    public ShipConsumableTankState shipFuelTank = new ShipConsumableTankState { resourceId = "charcoal" };
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
        unlockedKnowledgeIds ??= new List<string>();
        technologyResearchProgress ??= new List<TechnologyResearchProgress>();
        knowledgeSpPackages ??= new List<KnowledgeSpPackageState>();
        installedModules ??= new List<InstalledModuleState>();
        activeSortie ??= new SortieSessionState();
        baseIndustry ??= new BaseExtractionIndustryState();
        courierService ??= new CourierServiceState();
        questStates ??= new List<QuestState>();
        questMetrics ??= new List<QuestMetricState>();
        baseIslandExpansionRegions ??= new List<BaseIslandExpansionRegionState>();
        shipFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        inventory ??= new List<ResourceStack>();
        shipCargo ??= new List<ResourceStack>();
        portStorages ??= new List<PortStorageState>();
        activeSortie.Normalize();
        baseIndustry.Normalize();
        courierService.Normalize();
        shipFuelTank.Normalize();
        shipClaudiumTank.Normalize();

        for (int i = baseIslandExpansionRegions.Count - 1; i >= 0; i--)
        {
            BaseIslandExpansionRegionState region = baseIslandExpansionRegions[i];
            if (region == null)
            {
                baseIslandExpansionRegions.RemoveAt(i);
                continue;
            }

            region.Normalize();
            if (string.IsNullOrWhiteSpace(region.regionId))
            {
                baseIslandExpansionRegions.RemoveAt(i);
            }
        }

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

        for (int i = knowledgeSpPackages.Count - 1; i >= 0; i--)
        {
            KnowledgeSpPackageState package = knowledgeSpPackages[i];
            if (package == null || string.IsNullOrWhiteSpace(package.packageId) || package.amountSp <= 0)
            {
                knowledgeSpPackages.RemoveAt(i);
                continue;
            }

            package.Normalize();
        }

        for (int i = questStates.Count - 1; i >= 0; i--)
        {
            QuestState quest = questStates[i];
            if (quest == null || string.IsNullOrWhiteSpace(quest.questId))
            {
                questStates.RemoveAt(i);
                continue;
            }

            quest.Normalize();
        }

        for (int i = questMetrics.Count - 1; i >= 0; i--)
        {
            QuestMetricState metric = questMetrics[i];
            if (metric == null || string.IsNullOrWhiteSpace(metric.metricType))
            {
                questMetrics.RemoveAt(i);
                continue;
            }

            metric.Normalize();
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
        shipFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        shipFuelTank.amountKg = 0f;
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

    public bool IsKnowledgeUnlocked(string technologyId)
    {
        return !string.IsNullOrWhiteSpace(technologyId) && unlockedKnowledgeIds.Contains(technologyId);
    }

    public bool UnlockKnowledge(string technologyId)
    {
        if (string.IsNullOrWhiteSpace(technologyId)) return false;
        unlockedKnowledgeIds ??= new List<string>();
        if (unlockedKnowledgeIds.Contains(technologyId)) return false;

        unlockedKnowledgeIds.Add(technologyId);
        return true;
    }

    public KnowledgeSpPackageState GetKnowledgeSpPackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId) || knowledgeSpPackages == null) return null;

        for (int i = 0; i < knowledgeSpPackages.Count; i++)
        {
            KnowledgeSpPackageState package = knowledgeSpPackages[i];
            if (package != null && package.packageId == packageId)
            {
                return package;
            }
        }

        return null;
    }

    public void AddKnowledgeSpPackage(string packageId, string scopeKind, string scopeId, int amountSp)
    {
        if (string.IsNullOrWhiteSpace(packageId) || amountSp <= 0) return;

        knowledgeSpPackages ??= new List<KnowledgeSpPackageState>();
        KnowledgeSpPackageState package = GetKnowledgeSpPackage(packageId);
        if (package == null)
        {
            package = new KnowledgeSpPackageState { packageId = packageId };
            knowledgeSpPackages.Add(package);
        }

        package.scopeKind = scopeKind ?? "";
        package.scopeId = scopeId ?? "";
        package.amountSp += amountSp;
        package.Normalize();
    }

    public bool RemoveKnowledgeSpPackage(string packageId)
    {
        KnowledgeSpPackageState package = GetKnowledgeSpPackage(packageId);
        if (package == null) return false;

        knowledgeSpPackages.Remove(package);
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
        shipFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        return shipFuelTank.amountKg + shipClaudiumTank.amountKg;
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

    public BaseIslandExpansionRegionState GetBaseIslandExpansionRegionState(string regionId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return null;

        string normalizedId = regionId.Trim();
        baseIslandExpansionRegions ??= new List<BaseIslandExpansionRegionState>();
        for (int i = 0; i < baseIslandExpansionRegions.Count; i++)
        {
            BaseIslandExpansionRegionState state = baseIslandExpansionRegions[i];
            if (state != null && state.regionId == normalizedId)
            {
                return state;
            }
        }

        if (!createIfMissing) return null;

        BaseIslandExpansionRegionState newState = new BaseIslandExpansionRegionState { regionId = normalizedId };
        baseIslandExpansionRegions.Add(newState);
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

    public QuestState GetQuestState(string questId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(questId)) return null;
        questStates ??= new List<QuestState>();

        for (int i = 0; i < questStates.Count; i++)
        {
            QuestState quest = questStates[i];
            if (quest != null && quest.questId == questId)
            {
                return quest;
            }
        }

        if (!createIfMissing) return null;

        QuestState newQuest = new QuestState { questId = questId };
        questStates.Add(newQuest);
        return newQuest;
    }

    public QuestMetricState GetQuestMetric(string metricType, string targetId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(metricType)) return null;
        metricType = NormalizeQuestMetricPart(metricType);
        targetId = NormalizeQuestMetricPart(targetId);
        questMetrics ??= new List<QuestMetricState>();

        for (int i = 0; i < questMetrics.Count; i++)
        {
            QuestMetricState metric = questMetrics[i];
            if (metric != null && metric.metricType == metricType && metric.targetId == targetId)
            {
                return metric;
            }
        }

        if (!createIfMissing) return null;

        QuestMetricState newMetric = new QuestMetricState
        {
            metricType = metricType,
            targetId = targetId
        };
        questMetrics.Add(newMetric);
        return newMetric;
    }

    public int AddQuestMetric(string metricType, string targetId, int amount)
    {
        if (amount <= 0) return GetQuestMetricValue(metricType, targetId);
        QuestMetricState metric = GetQuestMetric(metricType, targetId, true);
        metric.value = Mathf.Max(0, metric.value + amount);
        return metric.value;
    }

    public int SetQuestMetricAtLeast(string metricType, string targetId, int value)
    {
        QuestMetricState metric = GetQuestMetric(metricType, targetId, true);
        metric.value = Mathf.Max(metric.value, value);
        return metric.value;
    }

    public int GetQuestMetricValue(string metricType, string targetId)
    {
        QuestMetricState metric = GetQuestMetric(metricType, targetId, false);
        return metric != null ? Mathf.Max(0, metric.value) : 0;
    }

    public static string NormalizeQuestMetricPart(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "any" : value.Trim().ToLowerInvariant();
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
public class CourierServiceState
{
    public List<CourierOrderSlotState> slots = new List<CourierOrderSlotState>();

    public void Normalize()
    {
        slots ??= new List<CourierOrderSlotState>();

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            CourierOrderSlotState slot = slots[i];
            if (slot == null)
            {
                slots.RemoveAt(i);
                continue;
            }

            slot.Normalize();
        }
    }

    public CourierOrderSlotState GetSlot(int slotIndex, bool createIfMissing)
    {
        slots ??= new List<CourierOrderSlotState>();
        for (int i = 0; i < slots.Count; i++)
        {
            CourierOrderSlotState slot = slots[i];
            if (slot != null && slot.slotIndex == slotIndex)
            {
                return slot;
            }
        }

        if (!createIfMissing)
        {
            return null;
        }

        CourierOrderSlotState newSlot = new CourierOrderSlotState { slotIndex = Mathf.Max(0, slotIndex) };
        slots.Add(newSlot);
        return newSlot;
    }
}

[Serializable]
public class CourierOrderSlotState
{
    public int slotIndex;
    public int generation;
    public string orderId = "";
    public string clientName = "";
    public List<CascadeItemAmount> inputs = new List<CascadeItemAmount>();
    public int freightReward;
    public int designExperienceReward;
    public long cooldownCompleteUtcTicks;

    public bool HasActiveOrder => cooldownCompleteUtcTicks <= 0L
        && !string.IsNullOrWhiteSpace(orderId)
        && inputs != null
        && inputs.Count > 0;

    public bool IsCoolingDownAt(long utcTicks)
    {
        return cooldownCompleteUtcTicks > utcTicks;
    }

    public void Normalize()
    {
        slotIndex = Mathf.Max(0, slotIndex);
        generation = Mathf.Max(0, generation);
        orderId = string.IsNullOrWhiteSpace(orderId) ? "" : orderId.Trim();
        clientName = string.IsNullOrWhiteSpace(clientName) ? "" : clientName.Trim();
        inputs ??= new List<CascadeItemAmount>();
        freightReward = Mathf.Max(0, freightReward);
        designExperienceReward = Mathf.Max(0, designExperienceReward);
        cooldownCompleteUtcTicks = Math.Max(0L, cooldownCompleteUtcTicks);

        for (int i = inputs.Count - 1; i >= 0; i--)
        {
            CascadeItemAmount input = inputs[i];
            if (input == null)
            {
                inputs.RemoveAt(i);
                continue;
            }

            input.Normalize();
            if (string.IsNullOrWhiteSpace(input.itemId) || input.amount <= 0)
            {
                inputs.RemoveAt(i);
            }
        }

        if (cooldownCompleteUtcTicks > 0L)
        {
            orderId = "";
            clientName = "";
            inputs.Clear();
            freightReward = 0;
            designExperienceReward = 0;
        }
    }

    public void ClearOrder()
    {
        orderId = "";
        clientName = "";
        inputs ??= new List<CascadeItemAmount>();
        inputs.Clear();
        freightReward = 0;
        designExperienceReward = 0;
    }
}

[Serializable]
public class BaseIslandExpansionRegionState
{
    public string regionId = "";
    public BaseIslandExpansionRegionStatus status = BaseIslandExpansionRegionStatus.Debris;
    public long clearingCompleteUtcTicks;

    public void Normalize()
    {
        regionId = string.IsNullOrWhiteSpace(regionId) ? "" : regionId.Trim();
        if (!Enum.IsDefined(typeof(BaseIslandExpansionRegionStatus), status))
        {
            status = BaseIslandExpansionRegionStatus.Debris;
        }

        if (status != BaseIslandExpansionRegionStatus.Clearing)
        {
            clearingCompleteUtcTicks = 0;
        }
    }
}

public enum BaseIslandExpansionRegionStatus
{
    Fog = 0,
    Debris = 1,
    Clearing = 2,
    Open = 3
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
    public float currentLevelSpProgress;
    public bool currentLevelRequirementsPaid;
    public long activeCycleStartUtcTicks;
    public long activeCycleEndUtcTicks;

    public bool HasActiveCycle => activeCycleEndUtcTicks > 0;

    public void Normalize()
    {
        technologyId ??= "";
        completedCycles = Mathf.Max(0, completedCycles);
        currentLevelSpProgress = Mathf.Max(0f, currentLevelSpProgress);
        if (activeCycleEndUtcTicks < 0) activeCycleEndUtcTicks = 0;
        if (activeCycleStartUtcTicks < 0) activeCycleStartUtcTicks = 0;
        activeCycleStartUtcTicks = 0;
        activeCycleEndUtcTicks = 0;
    }
}

[Serializable]
public class KnowledgeSpPackageState
{
    public string packageId = "";
    public string scopeKind = "universal";
    public string scopeId = "";
    public int amountSp;

    public void Normalize()
    {
        packageId ??= "";
        scopeKind = string.IsNullOrWhiteSpace(scopeKind) ? "universal" : scopeKind.Trim().ToLowerInvariant();
        scopeId ??= "";
        amountSp = Mathf.Max(0, amountSp);
    }
}

[Serializable]
public class QuestState
{
    public string questId = "";
    public bool accepted;
    public bool completed;
    public bool claimed;
    public int currentAmount;
    public int targetAmount = 1;
    public int baselineAmount;
    public long acceptedUtcTicks;
    public long completedUtcTicks;
    public long claimedUtcTicks;

    public void Normalize()
    {
        questId ??= "";
        currentAmount = Mathf.Max(0, currentAmount);
        targetAmount = Mathf.Max(1, targetAmount);
        baselineAmount = Mathf.Max(0, baselineAmount);
        acceptedUtcTicks = Math.Max(0L, acceptedUtcTicks);
        completedUtcTicks = Math.Max(0L, completedUtcTicks);
        claimedUtcTicks = Math.Max(0L, claimedUtcTicks);
        if (claimed)
        {
            completed = true;
            accepted = true;
        }
        else if (completed)
        {
            accepted = true;
        }
    }
}

[Serializable]
public class QuestMetricState
{
    public string metricType = "";
    public string targetId = "any";
    public int value;

    public void Normalize()
    {
        metricType = PlayerProgress.NormalizeQuestMetricPart(metricType);
        targetId = PlayerProgress.NormalizeQuestMetricPart(targetId);
        value = Mathf.Max(0, value);
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
