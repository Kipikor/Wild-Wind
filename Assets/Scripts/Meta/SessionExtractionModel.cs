using System;
using System.Collections.Generic;
using UnityEngine;

public static class SessionExtractionConstants
{
    public const float DefaultSortieRadiusMeters = 1500f;
    public const float LegacyDefaultSortieRadiusMeters = 5000f;
    public const string HighSlotTypeId = "high";
    public const string MidSlotTypeId = "mid";
    public const string LowSlotTypeId = "low";
    public const string RigSlotTypeId = "rig";
    public const string LegacyUtilitySlotTypeId = "utility";

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
    public const string StarterGasHarvesterModuleId = "starter_gas_harvester";
    public const string StarterMiningHoldModuleId = "starter_mining_hold";
    public const string StarterObservationPostModuleId = "starter_observation_post";
    public const string StarterHarpoonModuleId = "starter_harpoon_rig";
    public const string StarterLowSlotId = "low_01";
    public const string StarterHighSlotId = "high_01";
    public const string StarterSecondHighSlotId = "high_02";
    public const string StarterThirdHighSlotId = "high_03";
    public const string StarterMidSlotId = "mid_01";
    public const string LegacyUtilitySlotId = "utility_01";
    public const string BrokenAutomatonItemId = "broken_automaton";
    public const string AutomatonCoreItemId = "automaton_core";
    public const string LeviathanFatItemId = "leviathan_fat";
    public const string LeviathanHideItemId = "leviathan_hide";
    public const string ClaudiumGlandItemId = "claudium_gland";
    public const string MineralShellItemId = "mineral_shell";
    public const string RockInfoItemId = "rock_info";
    public const string CloudInfoItemId = "cloud_info";
    public const string LeviathanInfoItemId = "leviathan_info";
    public const string FundamentalExperienceItemId = "fundamental_experience";
    public const string DesignExperienceItemId = "design_experience";
    public const string MechanismsItemId = "mechanisms";
    public const string ToolsItemId = "tools";
}

public static class SessionExtractionCoreRuntime
{
    public static bool IsCoreMode(PlayerProgress progress)
    {
        return progress != null && progress.sessionExtractionCoreMode;
    }
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

        order.inputs.Add(new CascadeItemAmount { itemId = "ferron", amount = 12 });
        order.inputs.Add(new CascadeItemAmount { itemId = "silvate", amount = 4 });
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

        order.inputs.Add(new CascadeItemAmount { itemId = "ferron", amount = 4 });
        order.inputs.Add(new CascadeItemAmount { itemId = "silvate", amount = 1 });
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

        order.inputs.Add(new CascadeItemAmount { itemId = SessionExtractionConstants.MineralShellItemId, amount = 2 });
        order.inputs.Add(new CascadeItemAmount { itemId = "ferron", amount = 2 });
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
public class BaseExtractionIndustryState
{
    public List<BaseProcessingLineState> processing = new List<BaseProcessingLineState>();
    public List<CascadeProductionLineState> cascadeProduction = new List<CascadeProductionLineState>();

    public void Normalize()
    {
        processing ??= new List<BaseProcessingLineState>();
        cascadeProduction ??= new List<CascadeProductionLineState>();

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
    }

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

        if (module.gasHarvesterVolumeM3PerSecond > 0f
            || module.miningImpactHoldCapacityKg > 0f
            || module.harpoonRangeMeters > 0f
            || module.harpoonFlightDamage > 0f
            || id.Contains("weapon"))
        {
            return ShipFittingSlotBand.High;
        }

        if (module.observationRadiusMeters > 0f
            || id.Contains("radar")
            || id.Contains("scanner")
            || id.Contains("sensor")
            || id.Contains("autopilot")
            || name.Contains("observation"))
        {
            return ShipFittingSlotBand.Mid;
        }

        if ((id.Contains("rig") || name.Contains(" rig")) && module.harpoonRangeMeters <= 0f)
        {
            return ShipFittingSlotBand.Rig;
        }

        return ShipFittingSlotBand.Low;
    }
}

[Serializable]
public class SortieZoneDefinition
{
    public const float DefaultReturnCoalKgPerSecond = 0.0055f;
    public const float DefaultReturnClaudiumKgPerTonSecond = 0.00045f;
    private const float LegacyDefaultReturnCoalKgPerSecond = 0.006f;
    private const float LegacyDefaultReturnClaudiumKgPerTonSecond = 0.001f;

    public string sortieId = SessionExtractionConstants.DefaultSafeOreSortieId;
    public string displayName = SessionExtractionConstants.DefaultSafeOreSortieName;
    public BaseProcessingBranch primaryBranch = BaseProcessingBranch.Ore;
    public ShipFittingSlotBand recommendedSlotBand = ShipFittingSlotBand.High;
    public string requiredFittingSummary = "";
    public List<string> requiredModuleIds = new List<string>();
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
    public float distanceToBaseKm = 220f;
    public float returnCruiseSpeedMS = 35f;
    public float returnPowerLever = 0.7f;
    public float returnReserveMultiplier = 1.15f;
    public float extractionRunupRequiredSeconds = 12f;
    public float extractionRunupSpeedRatio = 0.9f;
    public float fallbackCoalKgPerSecond = DefaultReturnCoalKgPerSecond;
    public float fallbackClaudiumKgPerTonSecond = DefaultReturnClaudiumKgPerTonSecond;

    public void Normalize()
    {
        sortieId = string.IsNullOrWhiteSpace(sortieId)
            ? SessionExtractionConstants.DefaultSafeOreSortieId
            : sortieId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? sortieId : displayName.Trim();
        if (radiusMeters <= 0f
            || Mathf.Abs(radiusMeters - SessionExtractionConstants.LegacyDefaultSortieRadiusMeters) <= 0.1f)
        {
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters;
        }

        radiusMeters = Mathf.Max(100f, radiusMeters);
        extractionBoundaryToleranceMeters = Mathf.Clamp(extractionBoundaryToleranceMeters, 1f, radiusMeters);
        distanceToBaseKm = Mathf.Max(0.1f, distanceToBaseKm);
        returnCruiseSpeedMS = Mathf.Max(0.1f, returnCruiseSpeedMS);
        returnPowerLever = Mathf.Clamp(returnPowerLever, 0.05f, 1.2f);
        returnReserveMultiplier = Mathf.Max(1f, returnReserveMultiplier);
        extractionRunupRequiredSeconds = Mathf.Max(0f, extractionRunupRequiredSeconds);
        extractionRunupSpeedRatio = Mathf.Clamp01(extractionRunupSpeedRatio);
        fallbackCoalKgPerSecond = NormalizeLegacyReturnRate(
            fallbackCoalKgPerSecond,
            LegacyDefaultReturnCoalKgPerSecond,
            DefaultReturnCoalKgPerSecond);
        fallbackClaudiumKgPerTonSecond = NormalizeLegacyReturnRate(
            fallbackClaudiumKgPerTonSecond,
            LegacyDefaultReturnClaudiumKgPerTonSecond,
            DefaultReturnClaudiumKgPerTonSecond);
        starterResourceItemId ??= "";
        starterResourceChunkMin = Mathf.Max(1, starterResourceChunkMin);
        starterResourceChunkMax = Mathf.Max(starterResourceChunkMin, starterResourceChunkMax);
        starterResourceShedIntervalSeconds = Mathf.Max(0.25f, starterResourceShedIntervalSeconds);
        requiredFittingSummary ??= "";
        requiredModuleIds ??= new List<string>();
        for (int i = requiredModuleIds.Count - 1; i >= 0; i--)
        {
            string moduleId = requiredModuleIds[i];
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                requiredModuleIds.RemoveAt(i);
                continue;
            }

            requiredModuleIds[i] = moduleId.Trim();
        }

        if (entryPosition == Vector3.zero)
        {
            entryPosition = centerPosition + new Vector3(0f, Mathf.Max(250f, stormFloorY + 250f), 0f);
        }
    }

    private static float NormalizeLegacyReturnRate(float value, float legacyDefault, float currentDefault)
    {
        if (value <= 0f || Mathf.Abs(value - legacyDefault) <= 0.000001f)
        {
            return currentDefault;
        }

        return Mathf.Max(0f, value);
    }

    public SortieZoneDefinition Clone()
    {
        string json = JsonUtility.ToJson(this);
        SortieZoneDefinition clone = JsonUtility.FromJson<SortieZoneDefinition>(json);
        if (clone == null) clone = new SortieZoneDefinition();
        clone.Normalize();
        return clone;
    }
}

[Serializable]
public class SortieSessionState
{
    public bool active;
    public SortieZoneDefinition zone = new SortieZoneDefinition();
    public long startedUtcTicks;
    public string launchedFromDockId = "capital";
    public DockingLocationKind launchedFromDockKind = DockingLocationKind.Island;
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

    public void Begin(SortieZoneDefinition definition, long utcTicks, string dockId, DockingLocationKind dockKind, Vector3 dockPosition)
    {
        active = true;
        zone = definition != null ? definition.Clone() : new SortieZoneDefinition();
        startedUtcTicks = Math.Max(0L, utcTicks);
        launchedFromDockId = string.IsNullOrWhiteSpace(dockId) ? "capital" : dockId.Trim();
        launchedFromDockKind = dockKind;
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
        bool aboveStorm = position.y > zone.stormFloorY;
        return outsideCylinder && aboveStorm;
    }
}

public struct SortieReturnProfile
{
    public float emptyMassKg;
    public float cargoMassKg;
    public float cruiseSpeedMS;
    public float enginePowerKw;
    public float cruisePowerKw;
    public float engineFuelEfficiency;
    public float fuelEnergyKwhPerKg;
    public float claudiumConsumptionPerTonSecond;
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

        float reserve = Mathf.Max(1f, zone.returnReserveMultiplier);
        float fuelKg = zone.fallbackCoalKgPerSecond * estimate.returnTimeSeconds;
        if (profile.cruisePowerKw > 0f && profile.engineFuelEfficiency > 0f && profile.fuelEnergyKwhPerKg > 0f)
        {
            fuelKg = profile.cruisePowerKw / profile.engineFuelEfficiency / profile.fuelEnergyKwhPerKg * (estimate.returnTimeSeconds / 3600f);
        }

        float claudiumRate = profile.claudiumConsumptionPerTonSecond > 0f
            ? profile.claudiumConsumptionPerTonSecond
            : zone.fallbackClaudiumKgPerTonSecond;
        float claudiumKg = claudiumRate * Mathf.Max(0f, profile.TotalMassKg / 1000f) * estimate.returnTimeSeconds;

        estimate.requiredCoalKg = Mathf.Ceil(fuelKg * reserve);
        estimate.requiredClaudiumKg = Mathf.Ceil(claudiumKg * reserve);
        estimate.currentCoalKg = Mathf.Max(0f, profile.currentCoalKg);
        estimate.currentClaudiumKg = Mathf.Max(0f, profile.currentClaudiumKg);
        estimate.missingCoalKg = Mathf.Max(0f, estimate.requiredCoalKg - estimate.currentCoalKg);
        estimate.missingClaudiumKg = Mathf.Max(0f, estimate.requiredClaudiumKg - estimate.currentClaudiumKg);
        estimate.hasEnoughCoal = estimate.missingCoalKg <= 0.001f;
        estimate.hasEnoughClaudium = estimate.missingClaudiumKg <= 0.001f;
        estimate.canExtract = !estimate.isInsideCylinder
            && estimate.isAboveStorm
            && estimate.hasEnoughCoal
            && estimate.hasEnoughClaudium
            && estimate.hasExtractionRunup;

        if (!estimate.isAboveStorm)
        {
            estimate.status = "Extraction blocked: ship is in the storm layer.";
        }
        else if (estimate.isInsideCylinder)
        {
            estimate.status = "Extraction blocked: leave the sortie cylinder.";
        }
        else if (!estimate.hasEnoughCoal || !estimate.hasEnoughClaudium)
        {
            estimate.status = $"Extraction blocked: missing {estimate.missingCoalKg:F1} kg coal and {estimate.missingClaudiumKg:F1} kg claudium.";
        }
        else if (!estimate.hasExtractionRunup)
        {
            estimate.status = $"Extraction blocked: hold claudium slipstream toward base for {estimate.missingExtractionRunupSeconds:F1} s.";
        }
        else
        {
            estimate.status = "Extraction possible.";
        }

        return estimate;
    }
}
