using System.Collections.Generic;

public sealed class R1ShipDesignDefinition
{
    public string shipId = "";
    public string displayNameRu = "";
    public string requiredTechId = "";
    public string hullId = "";
    public string engineId = "";
    public string propellerId = "";
    public string claudiumLoopId = "";
    public string specialModuleId = "";
    public List<string> hullUpgradeIds = new List<string>();
    public List<string> engineUpgradeIds = new List<string>();
    public List<string> propellerUpgradeIds = new List<string>();
    public List<string> claudiumLoopUpgradeIds = new List<string>();
    public List<string> specialModuleUpgradeIds = new List<string>();
    public float expectedServiceMassKg;
    public float expectedMaxTakeoffMassKg;
    public float expectedEnginePowerKw;
    public float expectedStructureHp;
    public float expectedClaudiumLiftEfficiency = 28f;

    public List<string> GetAllowedHullIds()
    {
        return BuildAllowedPartIds(hullId, hullUpgradeIds);
    }

    public List<string> GetAllowedEngineIds()
    {
        return BuildAllowedPartIds(engineId, engineUpgradeIds);
    }

    public List<string> GetAllowedPropellerIds()
    {
        return BuildAllowedPartIds(propellerId, propellerUpgradeIds);
    }

    public List<string> GetAllowedClaudiumLoopIds()
    {
        return BuildAllowedPartIds(claudiumLoopId, claudiumLoopUpgradeIds);
    }

    public List<string> GetAllowedSpecialModuleIds()
    {
        return BuildAllowedPartIds(specialModuleId, specialModuleUpgradeIds);
    }

    private static List<string> BuildAllowedPartIds(string baseId, List<string> upgradeIds)
    {
        List<string> ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseId))
        {
            ids.Add(baseId);
        }

        if (upgradeIds != null)
        {
            for (int i = 0; i < upgradeIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(upgradeIds[i]) && !ids.Contains(upgradeIds[i]))
                {
                    ids.Add(upgradeIds[i]);
                }
            }
        }

        return ids;
    }
}

public static class R1ShipDesignCatalog
{
    public const string EngineSlotId = "engine_main";
    public const string PropellerSlotId = "propeller_main";
    public const string ClaudiumLoopSlotId = "claudium_loop";
    public const string RoleModuleSlotId = "role_module";

    private static readonly List<R1ShipDesignDefinition> designs = new List<R1ShipDesignDefinition>
    {
        new R1ShipDesignDefinition
        {
            shipId = "water_strider",
            displayNameRu = "Водомерка",
            requiredTechId = "bearing_skin",
            hullId = "water_strider_hull",
            engineId = "water_strider_engine",
            propellerId = "water_strider_propeller",
            claudiumLoopId = "water_strider_claudium_loop",
            specialModuleId = "water_strider_harvester",
            hullUpgradeIds = new List<string> { "water_strider_hull_mk2" },
            engineUpgradeIds = new List<string> { "water_strider_engine_mk2" },
            claudiumLoopUpgradeIds = new List<string> { "water_strider_claudium_loop_mk2" },
            specialModuleUpgradeIds = new List<string> { "water_strider_harvester_mk2" },
            expectedServiceMassKg = 2500f,
            expectedMaxTakeoffMassKg = 3500f,
            expectedEnginePowerKw = 220f,
            expectedStructureHp = 940f
        },
        new R1ShipDesignDefinition
        {
            shipId = "bulat",
            displayNameRu = "Булат",
            requiredTechId = "ore_collector",
            hullId = "bulat_hull",
            engineId = "bulat_engine",
            propellerId = "bulat_propeller",
            claudiumLoopId = "bulat_claudium_loop",
            specialModuleId = "bulat_impact_hold",
            hullUpgradeIds = new List<string> { "bulat_hull_mk2" },
            engineUpgradeIds = new List<string> { "bulat_engine_mk2" },
            claudiumLoopUpgradeIds = new List<string> { "bulat_claudium_loop_mk2" },
            specialModuleUpgradeIds = new List<string> { "bulat_impact_hold_mk2" },
            expectedServiceMassKg = 3000f,
            expectedMaxTakeoffMassKg = 6000f,
            expectedEnginePowerKw = 240f,
            expectedStructureHp = 1250f
        },
        new R1ShipDesignDefinition
        {
            shipId = "hornet",
            displayNameRu = "Шершень",
            requiredTechId = "continuous_observations",
            hullId = "hornet_hull",
            engineId = "hornet_engine",
            propellerId = "hornet_propeller",
            claudiumLoopId = "hornet_claudium_loop",
            specialModuleId = "hornet_observation_suite",
            engineUpgradeIds = new List<string> { "hornet_engine_mk2" },
            specialModuleUpgradeIds = new List<string> { "hornet_observation_suite_mk2" },
            expectedServiceMassKg = 3200f,
            expectedMaxTakeoffMassKg = 3800f,
            expectedEnginePowerKw = 280f,
            expectedStructureHp = 1350f
        },
        new R1ShipDesignDefinition
        {
            shipId = "jaeger",
            displayNameRu = "Егерь",
            requiredTechId = "hunter_hull",
            hullId = "jaeger_hull",
            engineId = "jaeger_engine",
            propellerId = "jaeger_propeller",
            claudiumLoopId = "jaeger_claudium_loop",
            specialModuleId = "jaeger_harpoon_rig",
            hullUpgradeIds = new List<string> { "jaeger_hull_mk2" },
            engineUpgradeIds = new List<string> { "jaeger_engine_mk2" },
            claudiumLoopUpgradeIds = new List<string> { "jaeger_claudium_loop_mk2" },
            specialModuleUpgradeIds = new List<string> { "jaeger_harpoon_rig_mk2" },
            expectedServiceMassKg = 4700f,
            expectedMaxTakeoffMassKg = 5200f,
            expectedEnginePowerKw = 330f,
            expectedStructureHp = 1600f
        },
        new R1ShipDesignDefinition
        {
            shipId = "opora",
            displayNameRu = "Опора",
            requiredTechId = "support_platform",
            hullId = "opora_hull",
            engineId = "opora_engine",
            propellerId = "opora_propeller",
            claudiumLoopId = "opora_claudium_loop",
            specialModuleId = "opora_crane_platform",
            hullUpgradeIds = new List<string> { "opora_hull_mk2" },
            propellerUpgradeIds = new List<string> { "opora_propeller_mk2" },
            claudiumLoopUpgradeIds = new List<string> { "opora_claudium_loop_mk2" },
            specialModuleUpgradeIds = new List<string> { "opora_crane_platform_mk2" },
            expectedServiceMassKg = 3600f,
            expectedMaxTakeoffMassKg = 10000f,
            expectedEnginePowerKw = 400f,
            expectedStructureHp = 1400f,
            expectedClaudiumLiftEfficiency = 45f
        },
        new R1ShipDesignDefinition
        {
            shipId = "fuel_tender",
            displayNameRu = "Топливщик",
            requiredTechId = "support_platform",
            hullId = "fuel_tender_hull",
            engineId = "fuel_tender_engine",
            propellerId = "fuel_tender_propeller",
            claudiumLoopId = "fuel_tender_claudium_loop",
            specialModuleId = "fuel_tender_tanks",
            expectedServiceMassKg = 3600f,
            expectedMaxTakeoffMassKg = 10000f,
            expectedEnginePowerKw = 400f,
            expectedStructureHp = 700f,
            expectedClaudiumLiftEfficiency = 50f
        },
        new R1ShipDesignDefinition
        {
            shipId = "parovoz",
            displayNameRu = "Паровоз",
            requiredTechId = "passenger_routes",
            hullId = "parovoz_hull",
            engineId = "parovoz_engine",
            propellerId = "parovoz_propeller",
            claudiumLoopId = "parovoz_claudium_loop",
            specialModuleId = "parovoz_passenger_cabin",
            hullUpgradeIds = new List<string> { "parovoz_hull_mk2" },
            engineUpgradeIds = new List<string> { "parovoz_engine_mk2" },
            propellerUpgradeIds = new List<string> { "parovoz_propeller_mk2" },
            claudiumLoopUpgradeIds = new List<string> { "parovoz_claudium_loop_mk2" },
            specialModuleUpgradeIds = new List<string> { "parovoz_passenger_cabin_mk2" },
            expectedServiceMassKg = 4300f,
            expectedMaxTakeoffMassKg = 5900f,
            expectedEnginePowerKw = 360f,
            expectedStructureHp = 1100f
        }
    };

    public static IReadOnlyList<R1ShipDesignDefinition> All => designs;

    public static bool TryGetByHullId(string hullId, out R1ShipDesignDefinition design)
    {
        design = null;
        if (string.IsNullOrWhiteSpace(hullId)) return false;

        for (int i = 0; i < designs.Count; i++)
        {
            R1ShipDesignDefinition designCandidate = designs[i];
            if (designCandidate != null && designCandidate.GetAllowedHullIds().Contains(hullId))
            {
                design = designCandidate;
                return true;
            }
        }

        return false;
    }

    public static PlayerProgress CreateUnlockedProgress(R1ShipDesignDefinition design)
    {
        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        if (design == null) return progress;

        progress.selectedHullId = design.hullId;
        progress.CompleteTechnology(design.requiredTechId);
        progress.PurchaseNode(design.requiredTechId);
        progress.InstallModule(EngineSlotId, design.engineId);
        progress.InstallModule(PropellerSlotId, design.propellerId);
        progress.InstallModule(ClaudiumLoopSlotId, design.claudiumLoopId);
        progress.InstallModule(RoleModuleSlotId, design.specialModuleId);
        return progress;
    }
}
