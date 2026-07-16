using System;
using System.Collections.Generic;
using UnityEngine;

public static class SessionExtractionConstants
{
    public const float DefaultSortieRadiusMeters = 1500f;
    public const float DefaultSortieEntryAltitudeMeters = 2500f;
    public const float DefaultSafeSortieDistanceToBaseKm = 30f;
    public const float DefaultSortiePocketOriginMeters = 250000f;
    public const float DefaultSortiePocketMinimumDockSeparationMeters = 100000f;
    public const float DefaultSortiePocketSpacingMeters = 12000f;
    public const string HighSlotTypeId = "high";
    public const string MidSlotTypeId = "mid";
    public const string LowSlotTypeId = "low";
    public const string RigSlotTypeId = "rig";

    public const string DefaultSafeOreSortieId = "safe_ore_boulders";
    public const string DefaultSafeOreSortieName = "Safe Ore Boulders";
    public const string DefaultSafeGasSortieId = "safe_gas_condensate";
    public const string DefaultSafeGasSortieName = "Safe Gas Condensate";
    public const string DefaultSafeAutomatonSortieId = "safe_automaton_wrecks";
    public const string DefaultSafeAutomatonSortieName = "Safe Automaton Wrecks";
    public const string DefaultSafeLeviathanSortieId = "safe_leviathan_remains";
    public const string DefaultSafeLeviathanSortieName = "Safe Leviathan Remains";
    public const string DefaultSafeSurveySortieId = "safe_survey_ruins";
    public const string DefaultSafeSurveySortieName = "Safe Survey Ruins";
    public const string CoreTacticalIntroCombatSortieId = "core_tactical_intro_combat";
    public const string CoreTacticalIntroCombatSortieName = "Core Tactical: First Contact";
    public const string QuickAdaptiveManualSortieId = "quick_adaptive_manual";
    public const string StarterAirframeKitItemId = "airframe_kit";
    public const string StarterAirframeOrderId = "starter_airframe_kit";
    public const string StarterModuleKitItemId = "module_kit";
    public const string StarterModuleKitOrderId = "starter_module_kit";
    public const string StarterMunitionBundleItemId = "munition_bundle";
    public const string StarterMunitionBundleOrderId = "starter_munition_bundle";
    public const string StarterWeaponCargoItemId = "weapon";
    public const int StarterWeaponUnitsPerMunitionBundle = 8;
    public const int StarterWeaponLoadoutTargetUnits = 24;
    public const string StarterCargoRackModuleId = "starter_cargo_rack";
    public const string StarterGasExtractorModuleId = "starter_gas_extractor";
    public const string StarterMiningHoldModuleId = "starter_mining_hold";
    public const string StarterObservationPostModuleId = "starter_observation_post";
    public const string StarterLeviathanSalvageModuleId = "starter_leviathan_salvage_rig";
    public const string StarterLowSlotId = "low_01";
    public const string StarterHighSlotId = "high_01";
    public const string StarterSecondHighSlotId = "high_02";
    public const string StarterThirdHighSlotId = "high_03";
    public const string StarterMidSlotId = "mid_01";
    public const string StarterAutomatonPartItemId = "automaton_relay";
    public const string AutomatonCoreItemId = "automaton_core";
    public const string ClaudiumItemId = "claudium";
    public const string LeviathanMeatItemId = "leviathan_meat";
    public const string LeviathanFatItemId = "leviathan_fat";
    public const string LeviathanHideItemId = "leviathan_hide";
    public const string LeviathanIchorItemId = "leviathan_ichor";
    public const string LeviathanSinewItemId = "leviathan_sinew";
    public const string BoneGritItemId = "bone_grit";
    public const string AcidItemId = "acid";
    public const string RockInfoItemId = "rock_info";
    public const string FundamentalExperienceItemId = "fundamental_experience";
    public const string DesignExperienceItemId = "design_experience";
    public const string MechanismsItemId = "mechanisms";
    public const string ToolsItemId = "tools";
}

public enum ShipFittingSlotBand
{
    High,
    Mid,
    Low,
    Rig
}

public enum BaseProcessingBranch
{
    Ore,
    Gas,
    AutomatonDismantling,
    LeviathanProcessing,
    CyberneticDeciphering
}

public enum CascadeProductionType
{
    Construction,
    Metallurgy,
    Mechanical,
    Instrumentation,
    ChemicalReactor,
    Automaton,
    Electrical,
    Assembly
}

public static class SessionExtractionIndustry
{
    public static readonly BaseProcessingBranch[] ProcessingBranches =
    {
        BaseProcessingBranch.Ore,
        BaseProcessingBranch.Gas,
        BaseProcessingBranch.AutomatonDismantling,
        BaseProcessingBranch.LeviathanProcessing,
        BaseProcessingBranch.CyberneticDeciphering
    };

    public static readonly CascadeProductionType[] CascadeProductionTypes =
    {
        CascadeProductionType.Construction,
        CascadeProductionType.Metallurgy,
        CascadeProductionType.Mechanical,
        CascadeProductionType.Instrumentation,
        CascadeProductionType.ChemicalReactor,
        CascadeProductionType.Automaton,
        CascadeProductionType.Electrical,
        CascadeProductionType.Assembly
    };

    public static string GetProcessingDisplayName(BaseProcessingBranch branch)
    {
        return branch switch
        {
            BaseProcessingBranch.Ore => "Ore",
            BaseProcessingBranch.Gas => "Gas",
            BaseProcessingBranch.AutomatonDismantling => "Automatons",
            BaseProcessingBranch.LeviathanProcessing => "Leviathans",
            BaseProcessingBranch.CyberneticDeciphering => "Cybernetic deciphering",
            _ => branch.ToString()
        };
    }

    public static string GetProductionDisplayName(CascadeProductionType type)
    {
        return type switch
        {
            CascadeProductionType.Construction => "Construction",
            CascadeProductionType.Metallurgy => "Metallurgy",
            CascadeProductionType.Mechanical => "Mechanical",
            CascadeProductionType.Instrumentation => "Instrumentation",
            CascadeProductionType.ChemicalReactor => "Chemical reactor",
            CascadeProductionType.Automaton => "Automaton",
            CascadeProductionType.Electrical => "Electrical",
            CascadeProductionType.Assembly => "Assembly",
            _ => type.ToString()
        };
    }

    public static float GetDefaultProcessingCapacity(BaseProcessingBranch branch)
    {
        return branch switch
        {
            BaseProcessingBranch.Ore => 25f,
            BaseProcessingBranch.Gas => 20f,
            BaseProcessingBranch.AutomatonDismantling => 8f,
            BaseProcessingBranch.LeviathanProcessing => 12f,
            BaseProcessingBranch.CyberneticDeciphering => 6f,
            _ => 10f
        };
    }

    public static float GetDefaultProductionCapacity(CascadeProductionType type)
    {
        return type switch
        {
            CascadeProductionType.Construction => 10f,
            CascadeProductionType.Metallurgy => 10f,
            CascadeProductionType.Mechanical => 8f,
            CascadeProductionType.Instrumentation => 6f,
            CascadeProductionType.ChemicalReactor => 8f,
            CascadeProductionType.Automaton => 4f,
            CascadeProductionType.Electrical => 7f,
            CascadeProductionType.Assembly => 6f,
            _ => 6f
        };
    }

    public static CascadeProductionOrderDefinition CreateStarterAirframeOrder()
    {
        CascadeProductionOrderDefinition order = new CascadeProductionOrderDefinition
        {
            orderId = SessionExtractionConstants.StarterAirframeOrderId,
            displayName = "Starter airframe kit"
        };

        order.inputs.Add(new CascadeItemAmount { itemId = "iron", amount = 12 });
        order.inputs.Add(new CascadeItemAmount { itemId = "calcite", amount = 4 });
        order.inputs.Add(new CascadeItemAmount { itemId = "charcoal", amount = 2 });
        order.outputs.Add(new CascadeItemAmount { itemId = SessionExtractionConstants.StarterAirframeKitItemId, amount = 1 });

        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Construction, loadUnits = 4f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Metallurgy, loadUnits = 2f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Mechanical, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Instrumentation, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.ChemicalReactor, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Automaton, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Electrical, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Assembly, loadUnits = 3f });
        order.Normalize();
        return order;
    }

    public static List<CascadeProductionOrderDefinition> CreateStarterCascadeOrders()
    {
        return new List<CascadeProductionOrderDefinition>
        {
            CreateStarterAirframeOrder(),
            CreateStarterModuleKitOrder(),
            CreateStarterMunitionBundleOrder()
        };
    }

    public static CascadeProductionOrderDefinition CreateStarterModuleKitOrder()
    {
        CascadeProductionOrderDefinition order = new CascadeProductionOrderDefinition
        {
            orderId = SessionExtractionConstants.StarterModuleKitOrderId,
            displayName = "Starter module kit"
        };

        order.inputs.Add(new CascadeItemAmount { itemId = "iron", amount = 4 });
        order.inputs.Add(new CascadeItemAmount { itemId = "calcite", amount = 1 });
        order.inputs.Add(new CascadeItemAmount { itemId = "charcoal", amount = 2 });
        order.outputs.Add(new CascadeItemAmount { itemId = SessionExtractionConstants.StarterModuleKitItemId, amount = 1 });

        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Metallurgy, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Mechanical, loadUnits = 3f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Instrumentation, loadUnits = 2f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.ChemicalReactor, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Electrical, loadUnits = 2f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Assembly, loadUnits = 2f });
        order.Normalize();
        return order;
    }

    public static CascadeProductionOrderDefinition CreateStarterMunitionBundleOrder()
    {
        CascadeProductionOrderDefinition order = new CascadeProductionOrderDefinition
        {
            orderId = SessionExtractionConstants.StarterMunitionBundleOrderId,
            displayName = "Starter munition bundle"
        };

        order.inputs.Add(new CascadeItemAmount { itemId = SessionExtractionConstants.BoneGritItemId, amount = 2 });
        order.inputs.Add(new CascadeItemAmount { itemId = "iron", amount = 2 });
        order.inputs.Add(new CascadeItemAmount { itemId = "charcoal", amount = 2 });
        order.outputs.Add(new CascadeItemAmount { itemId = SessionExtractionConstants.StarterMunitionBundleItemId, amount = 4 });

        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Metallurgy, loadUnits = 1.5f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Mechanical, loadUnits = 1f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.ChemicalReactor, loadUnits = 2f });
        order.loads.Add(new CascadeProductionLoad { type = CascadeProductionType.Assembly, loadUnits = 1f });
        order.Normalize();
        return order;
    }

}

[Serializable]
public class BaseProcessingLineState
{
    public BaseProcessingBranch branch;
    public int level = 1;
    public float capacityUnitsPerMinute = 10f;
    public float totalProcessedUnits;

    public void Normalize()
    {
        level = Mathf.Max(1, level);
        if (capacityUnitsPerMinute <= 0f)
        {
            capacityUnitsPerMinute = SessionExtractionIndustry.GetDefaultProcessingCapacity(branch);
        }

        capacityUnitsPerMinute = Mathf.Max(0.1f, capacityUnitsPerMinute);
        totalProcessedUnits = Mathf.Max(0f, totalProcessedUnits);
    }
}

[Serializable]
public class BaseProcessingFacilityState
{
    public string facilityId = "";
    public BaseProcessingBranch branch;
    public int level = 1;
    public int cycleInputUnits = 15;
    public int bunkerCapacityUnits = 1000;
    public float efficiency = 0.12f;
    public float cycleDurationSeconds = 10f;
    public float cycleElapsedSeconds;
    public float processingUnitsPerMinute = 10f;
    public float processingTickElapsedSeconds;
    public float processingUnitAccumulator;
    public float totalProcessedUnits;
    public List<ResourceStack> bunker = new List<ResourceStack>();
    public List<BaseProcessingOutputBufferState> outputBuffers = new List<BaseProcessingOutputBufferState>();

    public void Normalize()
    {
        facilityId = string.IsNullOrWhiteSpace(facilityId) ? branch.ToString() : facilityId.Trim();
        level = Mathf.Max(1, level);
        ConfigureForLevel(level);
        cycleElapsedSeconds = Mathf.Max(0f, cycleElapsedSeconds);
        processingTickElapsedSeconds = Mathf.Max(0f, processingTickElapsedSeconds);
        processingUnitAccumulator = Mathf.Max(0f, processingUnitAccumulator);
        processingUnitsPerMinute = Mathf.Max(0.1f, processingUnitsPerMinute);
        totalProcessedUnits = Mathf.Max(0f, totalProcessedUnits);
        bunker ??= new List<ResourceStack>();
        outputBuffers ??= new List<BaseProcessingOutputBufferState>();

        for (int i = bunker.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = bunker[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
            {
                bunker.RemoveAt(i);
                continue;
            }

            stack.amount = Mathf.Max(0, stack.amount);
        }

        for (int i = outputBuffers.Count - 1; i >= 0; i--)
        {
            BaseProcessingOutputBufferState buffer = outputBuffers[i];
            if (buffer == null || string.IsNullOrWhiteSpace(buffer.itemId))
            {
                outputBuffers.RemoveAt(i);
                continue;
            }

            buffer.Normalize();
        }
    }

    public void ConfigureForLevel(int sourceLevel)
    {
        level = Mathf.Max(1, sourceLevel);
        cycleInputUnits = 1;
        bunkerCapacityUnits = Mathf.Max(cycleInputUnits, 500 + level * 100);
        efficiency = Mathf.Clamp01(0.07f + level * 0.01f);
        cycleDurationSeconds = 1f;
        if (processingUnitsPerMinute <= 0f)
        {
            processingUnitsPerMinute = SessionExtractionIndustry.GetDefaultProcessingCapacity(branch);
        }
    }

    public int BunkerLoadUnits
    {
        get
        {
            int total = 0;
            bunker ??= new List<ResourceStack>();
            for (int i = 0; i < bunker.Count; i++)
            {
                ResourceStack stack = bunker[i];
                if (stack != null)
                {
                    total += Mathf.Max(0, stack.amount);
                }
            }

            return total;
        }
    }

    public int BunkerFreeUnits => Mathf.Max(0, bunkerCapacityUnits - BunkerLoadUnits);

    public int GetBunkerAmount(string itemId)
    {
        ResourceStack stack = GetBunkerStack(itemId, false);
        return stack != null ? stack.amount : 0;
    }

    public int AddBunker(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return 0;
        int moved = Mathf.Min(amount, BunkerFreeUnits);
        if (moved <= 0) return 0;

        ResourceStack stack = GetBunkerStack(itemId, true);
        stack.amount += moved;
        return moved;
    }

    public bool TrySpendBunker(string itemId, int amount)
    {
        if (amount <= 0) return true;
        ResourceStack stack = GetBunkerStack(itemId, false);
        if (stack == null || stack.amount < amount) return false;

        stack.amount -= amount;
        if (stack.amount <= 0)
        {
            bunker.Remove(stack);
        }

        return true;
    }

    public int RemoveBunker(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return 0;
        ResourceStack stack = GetBunkerStack(itemId, false);
        if (stack == null || stack.amount <= 0) return 0;

        int removed = Mathf.Min(amount, stack.amount);
        stack.amount -= removed;
        if (stack.amount <= 0)
        {
            bunker.Remove(stack);
        }

        return removed;
    }

    public BaseProcessingOutputBufferState GetOutputBuffer(string itemId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        outputBuffers ??= new List<BaseProcessingOutputBufferState>();
        for (int i = 0; i < outputBuffers.Count; i++)
        {
            BaseProcessingOutputBufferState buffer = outputBuffers[i];
            if (buffer != null && buffer.itemId == itemId)
            {
                buffer.Normalize();
                return buffer;
            }
        }

        if (!createIfMissing) return null;

        BaseProcessingOutputBufferState created = new BaseProcessingOutputBufferState { itemId = itemId };
        outputBuffers.Add(created);
        return created;
    }

    private ResourceStack GetBunkerStack(string itemId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        bunker ??= new List<ResourceStack>();
        for (int i = 0; i < bunker.Count; i++)
        {
            ResourceStack stack = bunker[i];
            if (stack != null && stack.resourceId == itemId)
            {
                return stack;
            }
        }

        if (!createIfMissing) return null;

        ResourceStack created = new ResourceStack { resourceId = itemId };
        bunker.Add(created);
        return created;
    }
}

[Serializable]
public class BaseProcessingOutputBufferState
{
    public string itemId = "";
    public int readyAmount;
    public float fractionalAmount;

    public void Normalize()
    {
        itemId = string.IsNullOrWhiteSpace(itemId) ? "" : itemId.Trim();
        readyAmount = Mathf.Max(0, readyAmount);
        fractionalAmount = Mathf.Clamp(fractionalAmount, 0f, 0.9999f);
    }
}

[Serializable]
public class CascadeProductionLineState
{
    public CascadeProductionType type;
    public int level = 1;
    public float capacityUnitsPerMinute = 6f;
    public float totalLoadApplied;

    public void Normalize()
    {
        level = Mathf.Max(1, level);
        if (capacityUnitsPerMinute <= 0f)
        {
            capacityUnitsPerMinute = SessionExtractionIndustry.GetDefaultProductionCapacity(type);
        }

        capacityUnitsPerMinute = Mathf.Max(0.1f, capacityUnitsPerMinute);
        totalLoadApplied = Mathf.Max(0f, totalLoadApplied);
    }
}

[Serializable]
public class CascadeItemAmount
{
    public string itemId = "";
    public int amount;

    public void Normalize()
    {
        itemId = string.IsNullOrWhiteSpace(itemId) ? "" : itemId.Trim();
        amount = Mathf.Max(0, amount);
    }
}

[Serializable]
public class CascadeProductionLoad
{
    public CascadeProductionType type;
    public float loadUnits;

    public void Normalize()
    {
        loadUnits = Mathf.Max(0f, loadUnits);
    }
}

[Serializable]
public class CascadeProductionOrderDefinition
{
    public string orderId = "";
    public string displayName = "";
    public List<CascadeItemAmount> inputs = new List<CascadeItemAmount>();
    public List<CascadeItemAmount> outputs = new List<CascadeItemAmount>();
    public List<CascadeProductionLoad> loads = new List<CascadeProductionLoad>();

    public void Normalize()
    {
        orderId = string.IsNullOrWhiteSpace(orderId) ? "cascade_order" : orderId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? orderId : displayName.Trim();
        inputs ??= new List<CascadeItemAmount>();
        outputs ??= new List<CascadeItemAmount>();
        loads ??= new List<CascadeProductionLoad>();

        for (int i = inputs.Count - 1; i >= 0; i--)
        {
            CascadeItemAmount item = inputs[i];
            if (item == null)
            {
                inputs.RemoveAt(i);
                continue;
            }

            item.Normalize();
            if (string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0)
            {
                inputs.RemoveAt(i);
            }
        }

        for (int i = outputs.Count - 1; i >= 0; i--)
        {
            CascadeItemAmount item = outputs[i];
            if (item == null)
            {
                outputs.RemoveAt(i);
                continue;
            }

            item.Normalize();
            if (string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0)
            {
                outputs.RemoveAt(i);
            }
        }

        for (int i = loads.Count - 1; i >= 0; i--)
        {
            CascadeProductionLoad load = loads[i];
            if (load == null)
            {
                loads.RemoveAt(i);
                continue;
            }

            load.Normalize();
            if (load.loadUnits <= 0f)
            {
                loads.RemoveAt(i);
            }
        }
    }
}

public class CascadeResourceGap
{
    public string itemId = "";
    public int required;
    public int available;
    public int missing;
}

public class CascadeProductionEstimate
{
    public bool canRun;
    public string blockedReason = "";
    public CascadeProductionType bottleneck;
    public float bottleneckMinutes;
    public float totalLoadUnits;
    public List<CascadeResourceGap> missingInputs = new List<CascadeResourceGap>();
}

[Serializable]
public class CascadeProductionQueueItemState
{
    public string queueId = "";
    public string orderId = "";
    public string displayName = "";
    public int quantity = 1;
    public long startedUtcTicks;
    public long completeUtcTicks;
    public List<CascadeItemAmount> inputs = new List<CascadeItemAmount>();
    public List<CascadeItemAmount> outputs = new List<CascadeItemAmount>();
    public List<CascadeProductionLoad> loads = new List<CascadeProductionLoad>();

    public void Normalize()
    {
        queueId = string.IsNullOrWhiteSpace(queueId) ? "" : queueId.Trim();
        orderId = string.IsNullOrWhiteSpace(orderId) ? "cascade_order" : orderId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? orderId : displayName.Trim();
        quantity = Mathf.Max(1, quantity);
        startedUtcTicks = Math.Max(0L, startedUtcTicks);
        completeUtcTicks = Math.Max(0L, completeUtcTicks);
        inputs ??= new List<CascadeItemAmount>();
        outputs ??= new List<CascadeItemAmount>();
        loads ??= new List<CascadeProductionLoad>();

        NormalizeItems(inputs);
        NormalizeItems(outputs);
        NormalizeLoads(loads);
    }

    public bool IsCompleteAt(long utcTicks)
    {
        return completeUtcTicks > 0L && utcTicks >= completeUtcTicks;
    }

    private static void NormalizeItems(List<CascadeItemAmount> items)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            CascadeItemAmount item = items[i];
            if (item == null)
            {
                items.RemoveAt(i);
                continue;
            }

            item.Normalize();
            if (string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0)
            {
                items.RemoveAt(i);
            }
        }
    }

    private static void NormalizeLoads(List<CascadeProductionLoad> loads)
    {
        for (int i = loads.Count - 1; i >= 0; i--)
        {
            CascadeProductionLoad load = loads[i];
            if (load == null)
            {
                loads.RemoveAt(i);
                continue;
            }

            load.Normalize();
            if (load.loadUnits <= 0f)
            {
                loads.RemoveAt(i);
            }
        }
    }
}

[Serializable]
public class BaseExtractionIndustryState
{
    public List<BaseProcessingLineState> processing = new List<BaseProcessingLineState>();
    public List<BaseProcessingFacilityState> processingFacilities = new List<BaseProcessingFacilityState>();
    public List<CascadeProductionLineState> cascadeProduction = new List<CascadeProductionLineState>();
    public List<CascadeProductionQueueItemState> cascadeQueue = new List<CascadeProductionQueueItemState>();

    public void Normalize()
    {
        processing ??= new List<BaseProcessingLineState>();
        processingFacilities ??= new List<BaseProcessingFacilityState>();
        cascadeProduction ??= new List<CascadeProductionLineState>();
        cascadeQueue ??= new List<CascadeProductionQueueItemState>();

        EnsureProcessingBranches();
        EnsureCascadeProductionTypes();

        for (int i = processing.Count - 1; i >= 0; i--)
        {
            BaseProcessingLineState line = processing[i];
            if (line == null)
            {
                processing.RemoveAt(i);
                continue;
            }

            line.Normalize();
        }

        for (int i = processingFacilities.Count - 1; i >= 0; i--)
        {
            BaseProcessingFacilityState facility = processingFacilities[i];
            if (facility == null)
            {
                processingFacilities.RemoveAt(i);
                continue;
            }

            facility.Normalize();
            if (string.IsNullOrWhiteSpace(facility.facilityId))
            {
                processingFacilities.RemoveAt(i);
            }
        }

        for (int i = cascadeProduction.Count - 1; i >= 0; i--)
        {
            CascadeProductionLineState line = cascadeProduction[i];
            if (line == null)
            {
                cascadeProduction.RemoveAt(i);
                continue;
            }

            line.Normalize();
        }

        for (int i = cascadeQueue.Count - 1; i >= 0; i--)
        {
            CascadeProductionQueueItemState item = cascadeQueue[i];
            if (item == null)
            {
                cascadeQueue.RemoveAt(i);
                continue;
            }

            item.Normalize();
            if (string.IsNullOrWhiteSpace(item.orderId)
                || item.outputs.Count == 0
                || item.loads.Count == 0
                || item.completeUtcTicks <= 0L)
            {
                cascadeQueue.RemoveAt(i);
            }
        }
    }

    public int ActiveCascadeQueueCount => cascadeQueue != null ? cascadeQueue.Count : 0;

    public BaseProcessingLineState GetProcessing(BaseProcessingBranch branch)
    {
        processing ??= new List<BaseProcessingLineState>();
        for (int i = 0; i < processing.Count; i++)
        {
            BaseProcessingLineState line = processing[i];
            if (line != null && line.branch == branch)
            {
                line.Normalize();
                return line;
            }
        }

        BaseProcessingLineState created = new BaseProcessingLineState
        {
            branch = branch,
            capacityUnitsPerMinute = SessionExtractionIndustry.GetDefaultProcessingCapacity(branch)
        };
        created.Normalize();
        processing.Add(created);
        return created;
    }

    public BaseProcessingFacilityState GetProcessingFacility(string facilityId, BaseProcessingBranch branch, int level)
    {
        processingFacilities ??= new List<BaseProcessingFacilityState>();
        string normalizedId = string.IsNullOrWhiteSpace(facilityId) ? branch.ToString() : facilityId.Trim();
        for (int i = 0; i < processingFacilities.Count; i++)
        {
            BaseProcessingFacilityState facility = processingFacilities[i];
            if (facility != null && facility.facilityId == normalizedId)
            {
                facility.branch = branch;
                facility.ConfigureForLevel(level);
                facility.Normalize();
                return facility;
            }
        }

        BaseProcessingFacilityState created = new BaseProcessingFacilityState
        {
            facilityId = normalizedId,
            branch = branch
        };
        created.ConfigureForLevel(level);
        created.Normalize();
        processingFacilities.Add(created);
        return created;
    }

    public CascadeProductionLineState GetProduction(CascadeProductionType type)
    {
        cascadeProduction ??= new List<CascadeProductionLineState>();
        for (int i = 0; i < cascadeProduction.Count; i++)
        {
            CascadeProductionLineState line = cascadeProduction[i];
            if (line != null && line.type == type)
            {
                line.Normalize();
                return line;
            }
        }

        CascadeProductionLineState created = new CascadeProductionLineState
        {
            type = type,
            capacityUnitsPerMinute = SessionExtractionIndustry.GetDefaultProductionCapacity(type)
        };
        created.Normalize();
        cascadeProduction.Add(created);
        return created;
    }

    private void EnsureProcessingBranches()
    {
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            GetProcessing(SessionExtractionIndustry.ProcessingBranches[i]);
        }
    }

    private void EnsureCascadeProductionTypes()
    {
        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            GetProduction(SessionExtractionIndustry.CascadeProductionTypes[i]);
        }
    }
}

public static class SessionExtractionFitting
{
    public static string GetSlotTypeId(ShipFittingSlotBand band)
    {
        return band switch
        {
            ShipFittingSlotBand.High => SessionExtractionConstants.HighSlotTypeId,
            ShipFittingSlotBand.Mid => SessionExtractionConstants.MidSlotTypeId,
            ShipFittingSlotBand.Low => SessionExtractionConstants.LowSlotTypeId,
            ShipFittingSlotBand.Rig => SessionExtractionConstants.RigSlotTypeId,
            _ => SessionExtractionConstants.LowSlotTypeId
        };
    }

    public static ShipFittingSlotBand ClassifySpecialModule(SpecialModuleConfig module)
    {
        if (module == null) return ShipFittingSlotBand.Low;

        string id = (module.id ?? "").ToLowerInvariant();
        string name = ((module.localNameEn ?? "") + " " + (module.localNameRu ?? "") + " " + (module.descriptionRu ?? "")).ToLowerInvariant();

        if (id.Contains("gas_extractor")
            || module.miningImpactHoldCapacityKg > 0f
            || id.Contains("leviathan_salvage")
            || id.Contains("weapon"))
        {
            return ShipFittingSlotBand.High;
        }

        if (id.Contains("observation")
            || id.Contains("radar")
            || id.Contains("scanner")
            || id.Contains("sensor")
            || id.Contains("autopilot")
            || name.Contains("observation"))
        {
            return ShipFittingSlotBand.Mid;
        }

        if (id.Contains("rig") || name.Contains(" rig"))
        {
            return ShipFittingSlotBand.Rig;
        }

        return ShipFittingSlotBand.Low;
    }
}

[Serializable]
public class SortiePayloadRewardLine
{
    public string itemId = "";
    public string displayNameRu = "";
    public int amount;
    public Color color = new Color(0.8f, 0.72f, 0.52f, 1f);

    public void Normalize()
    {
        itemId = string.IsNullOrWhiteSpace(itemId) ? "" : itemId.Trim();
        displayNameRu = string.IsNullOrWhiteSpace(displayNameRu) ? itemId : displayNameRu.Trim();
        amount = Mathf.Max(0, amount);
    }
}

[Serializable]
public class SortieZoneDefinition
{
    public string sortieId = SessionExtractionConstants.DefaultSafeOreSortieId;
    public string displayName = SessionExtractionConstants.DefaultSafeOreSortieName;
    public BaseProcessingBranch primaryBranch = BaseProcessingBranch.Ore;
    public string sourceShipId = "";
    public string sourceShipDisplayNameRu = "";
    public string missionProfile = "";
    public string missionArchetype = "";
    public string primaryActivity = "";
    public string primaryActivityRu = "";
    public string missionSeed = "";
    public List<SortiePayloadRewardLine> payloadRewards = new List<SortiePayloadRewardLine>();
    public int completionFreightAward;
    public int completionDesignExperienceAward;
    public string starterResourceItemId = "";
    public int starterResourceChunkMin = 1;
    public int starterResourceChunkMax = 4;
    public float starterResourceShedIntervalSeconds = 2.5f;
    public Color starterResourceColor = new Color(0.8f, 0.72f, 0.52f, 1f);
    public Vector3 centerPosition;
    public Vector3 entryPosition;
    public float radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters;
    public float stormFloorY = 0f;
    public float extractionBoundaryToleranceMeters = 150f;
    public float distanceToBaseKm = SessionExtractionConstants.DefaultSafeSortieDistanceToBaseKm;
    public float returnCruiseSpeedMS = 35f;
    public float returnPowerLever = 0.7f;
    public float extractionRunupRequiredSeconds = 5f;
    public float extractionRunupSpeedRatio = 0.9f;

    public void Normalize()
    {
        sortieId = string.IsNullOrWhiteSpace(sortieId)
            ? SessionExtractionConstants.DefaultSafeOreSortieId
            : sortieId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? sortieId : displayName.Trim();
        if (radiusMeters <= 0f)
        {
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters;
        }

        radiusMeters = Mathf.Max(100f, radiusMeters);
        extractionBoundaryToleranceMeters = Mathf.Clamp(extractionBoundaryToleranceMeters, 1f, radiusMeters);
        distanceToBaseKm = Mathf.Max(0.1f, distanceToBaseKm);
        returnCruiseSpeedMS = Mathf.Max(0.1f, returnCruiseSpeedMS);
        returnPowerLever = Mathf.Clamp(returnPowerLever, 0.05f, 1.2f);
        extractionRunupRequiredSeconds = Mathf.Max(0f, extractionRunupRequiredSeconds);
        extractionRunupSpeedRatio = Mathf.Clamp01(extractionRunupSpeedRatio);
        sourceShipId = string.IsNullOrWhiteSpace(sourceShipId) ? "" : sourceShipId.Trim();
        sourceShipDisplayNameRu = string.IsNullOrWhiteSpace(sourceShipDisplayNameRu) ? "" : sourceShipDisplayNameRu.Trim();
        missionProfile = string.IsNullOrWhiteSpace(missionProfile) ? "" : missionProfile.Trim();
        missionArchetype = string.IsNullOrWhiteSpace(missionArchetype) ? "" : missionArchetype.Trim();
        primaryActivity = string.IsNullOrWhiteSpace(primaryActivity) ? "" : primaryActivity.Trim();
        primaryActivityRu = string.IsNullOrWhiteSpace(primaryActivityRu) ? "" : primaryActivityRu.Trim();
        missionSeed = string.IsNullOrWhiteSpace(missionSeed) ? "" : missionSeed.Trim();
        completionFreightAward = Mathf.Max(0, completionFreightAward);
        completionDesignExperienceAward = Mathf.Max(0, completionDesignExperienceAward);
        payloadRewards ??= new List<SortiePayloadRewardLine>();
        for (int i = payloadRewards.Count - 1; i >= 0; i--)
        {
            SortiePayloadRewardLine reward = payloadRewards[i];
            if (reward == null)
            {
                payloadRewards.RemoveAt(i);
                continue;
            }

            reward.Normalize();
            if (string.IsNullOrWhiteSpace(reward.itemId) || reward.amount <= 0)
            {
                payloadRewards.RemoveAt(i);
            }
        }

        starterResourceItemId ??= "";
        if (payloadRewards.Count > 0 && string.IsNullOrWhiteSpace(starterResourceItemId))
        {
            starterResourceItemId = payloadRewards[0].itemId;
            starterResourceColor = payloadRewards[0].color;
        }

        starterResourceChunkMin = Mathf.Max(1, starterResourceChunkMin);
        starterResourceChunkMax = Mathf.Max(starterResourceChunkMin, starterResourceChunkMax);
        starterResourceShedIntervalSeconds = Mathf.Max(0.25f, starterResourceShedIntervalSeconds);
        if (entryPosition == Vector3.zero)
        {
            entryPosition = centerPosition + new Vector3(
                0f,
                stormFloorY + SessionExtractionConstants.DefaultSortieEntryAltitudeMeters,
                0f);
        }
    }

    public SortieZoneDefinition Clone()
    {
        string json = JsonUtility.ToJson(this);
        SortieZoneDefinition clone = JsonUtility.FromJson<SortieZoneDefinition>(json);
        if (clone == null) clone = new SortieZoneDefinition();
        clone.Normalize();
        return clone;
    }

    public bool HasPayloadRewards => payloadRewards != null && payloadRewards.Count > 0;

    public bool AcceptsResource(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        Normalize();
        if (HasPayloadRewards)
        {
            for (int i = 0; i < payloadRewards.Count; i++)
            {
                SortiePayloadRewardLine reward = payloadRewards[i];
                if (reward != null && reward.itemId == itemId)
                {
                    return true;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(starterResourceItemId) && starterResourceItemId == itemId;
    }

    public string GetAcceptedResourceSummary()
    {
        Normalize();
        if (!HasPayloadRewards)
        {
            return starterResourceItemId;
        }

        List<string> itemIds = new List<string>();
        for (int i = 0; i < payloadRewards.Count; i++)
        {
            SortiePayloadRewardLine reward = payloadRewards[i];
            if (reward != null && !string.IsNullOrWhiteSpace(reward.itemId) && !itemIds.Contains(reward.itemId))
            {
                itemIds.Add(reward.itemId);
            }
        }

        return string.Join(", ", itemIds);
    }
}

[Serializable]
public class SortieSessionState
{
    public bool active;
    public SortieZoneDefinition zone = new SortieZoneDefinition();
    public long startedUtcTicks;
    public string launchedFromDockId = "capital";
    public Vector3 launchPosition;
    public Vector3 lastKnownPosition;
    public float extractionRunupSeconds;

    public void Normalize()
    {
        zone ??= new SortieZoneDefinition();
        zone.Normalize();
        launchedFromDockId = string.IsNullOrWhiteSpace(launchedFromDockId) ? "capital" : launchedFromDockId.Trim();
        extractionRunupSeconds = Mathf.Clamp(extractionRunupSeconds, 0f, Mathf.Max(0f, zone.extractionRunupRequiredSeconds));
        if (!active)
        {
            startedUtcTicks = 0L;
            extractionRunupSeconds = 0f;
        }
    }

    public void Begin(SortieZoneDefinition definition, long utcTicks, string dockId, Vector3 dockPosition)
    {
        active = true;
        zone = definition != null ? definition.Clone() : new SortieZoneDefinition();
        startedUtcTicks = Math.Max(0L, utcTicks);
        launchedFromDockId = string.IsNullOrWhiteSpace(dockId) ? "capital" : dockId.Trim();
        launchPosition = dockPosition;
        lastKnownPosition = zone.entryPosition;
        extractionRunupSeconds = 0f;
        Normalize();
    }

    public void Clear()
    {
        active = false;
        startedUtcTicks = 0L;
        lastKnownPosition = Vector3.zero;
        extractionRunupSeconds = 0f;
        Normalize();
    }

    public void RememberPosition(Vector3 position)
    {
        lastKnownPosition = position;
        if (!CanMaintainExtractionRunupAt(position))
        {
            ResetExtractionRunup();
        }
    }

    public void AddExtractionRunup(float deltaSeconds)
    {
        Normalize();
        extractionRunupSeconds = Mathf.Clamp(
            extractionRunupSeconds + Mathf.Max(0f, deltaSeconds),
            0f,
            Mathf.Max(0f, zone.extractionRunupRequiredSeconds));
    }

    public void ResetExtractionRunup()
    {
        extractionRunupSeconds = 0f;
    }

    private bool CanMaintainExtractionRunupAt(Vector3 position)
    {
        if (!active || zone == null)
        {
            return false;
        }

        zone.Normalize();
        Vector2 center = new Vector2(zone.centerPosition.x, zone.centerPosition.z);
        Vector2 point = new Vector2(position.x, position.z);
        float distance = Vector2.Distance(center, point);
        float distanceToBoundary = zone.radiusMeters - distance;
        bool outsideCylinder = distance >= zone.radiusMeters;
        return outsideCylinder;
    }
}

public struct SortieReturnProfile
{
    public float emptyMassKg;
    public float cargoMassKg;
    public float cruiseSpeedMS;
    public float coalBurnKgPerSecond;
    public float claudiumBurnKgPerSecond;
    public float currentCoalKg;
    public float currentClaudiumKg;
    public string coalResourceId;
    public string claudiumResourceId;

    public float TotalMassKg => Mathf.Max(0f, emptyMassKg + cargoMassKg);
}

public struct SortieReturnEstimate
{
    public bool hasActiveSortie;
    public bool isInsideCylinder;
    public bool isAboveStorm;
    public bool isNearBoundary;
    public bool hasEnoughCoal;
    public bool hasEnoughClaudium;
    public bool hasExtractionRunup;
    public bool canExtract;
    public float horizontalDistanceFromCenterMeters;
    public float distanceToBoundaryMeters;
    public float distanceToBaseKm;
    public float returnTimeSeconds;
    public float requiredExtractionRunupSeconds;
    public float extractionRunupSeconds;
    public float missingExtractionRunupSeconds;
    public float extractionRunupSpeedRatio;
    public float requiredCoalKg;
    public float requiredClaudiumKg;
    public float currentCoalKg;
    public float currentClaudiumKg;
    public float missingCoalKg;
    public float missingClaudiumKg;
    public string status;
}

public static class SortieExtractionCalculator
{
    public static SortieReturnEstimate Calculate(SortieSessionState session, Vector3 shipPosition, SortieReturnProfile profile)
    {
        SortieReturnEstimate estimate = new SortieReturnEstimate
        {
            hasActiveSortie = session != null && session.active,
            status = "No active sortie."
        };

        if (session == null || !session.active)
        {
            return estimate;
        }

        session.Normalize();
        SortieZoneDefinition zone = session.zone;
        Vector2 center = new Vector2(zone.centerPosition.x, zone.centerPosition.z);
        Vector2 position = new Vector2(shipPosition.x, shipPosition.z);
        float horizontalDistance = Vector2.Distance(center, position);
        float distanceToBoundary = zone.radiusMeters - horizontalDistance;

        estimate.horizontalDistanceFromCenterMeters = horizontalDistance;
        estimate.distanceToBoundaryMeters = distanceToBoundary;
        estimate.distanceToBaseKm = zone.distanceToBaseKm;
        estimate.isInsideCylinder = horizontalDistance < zone.radiusMeters;
        estimate.isAboveStorm = shipPosition.y > zone.stormFloorY;
        estimate.isNearBoundary = distanceToBoundary <= zone.extractionBoundaryToleranceMeters;
        estimate.requiredExtractionRunupSeconds = Mathf.Max(0f, zone.extractionRunupRequiredSeconds);
        estimate.extractionRunupSpeedRatio = Mathf.Clamp01(zone.extractionRunupSpeedRatio);
        estimate.extractionRunupSeconds = Mathf.Clamp(session.extractionRunupSeconds, 0f, estimate.requiredExtractionRunupSeconds);
        estimate.missingExtractionRunupSeconds = Mathf.Max(0f, estimate.requiredExtractionRunupSeconds - estimate.extractionRunupSeconds);
        estimate.hasExtractionRunup = estimate.missingExtractionRunupSeconds <= 0.001f;

        float speed = Mathf.Max(0.1f, profile.cruiseSpeedMS > 0f ? profile.cruiseSpeedMS : zone.returnCruiseSpeedMS);
        float returnDistanceMeters = zone.distanceToBaseKm * 1000f;
        estimate.returnTimeSeconds = Mathf.Max(1f, returnDistanceMeters / speed);

        estimate.requiredCoalKg = 0f;
        estimate.requiredClaudiumKg = 0f;
        estimate.currentCoalKg = Mathf.Max(0f, profile.currentCoalKg);
        estimate.currentClaudiumKg = Mathf.Max(0f, profile.currentClaudiumKg);
        estimate.missingCoalKg = 0f;
        estimate.missingClaudiumKg = 0f;
        estimate.hasEnoughCoal = true;
        estimate.hasEnoughClaudium = true;
        estimate.canExtract = !estimate.isInsideCylinder
            && estimate.hasExtractionRunup;

        if (estimate.isInsideCylinder)
        {
            estimate.status = "Extraction blocked: leave the mission circle.";
        }
        else if (!estimate.hasExtractionRunup)
        {
            estimate.status = $"Extraction blocked: stay outside the mission circle for {estimate.missingExtractionRunupSeconds:F1} s.";
        }
        else
        {
            estimate.status = "Extraction possible.";
        }

        return estimate;
    }
}
