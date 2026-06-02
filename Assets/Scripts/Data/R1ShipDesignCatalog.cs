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

    private static readonly List<R1ShipDesignDefinition> designs = new List<R1ShipDesignDefinition>();

    public static IReadOnlyList<R1ShipDesignDefinition> All => designs;

    public static bool TryGetByHullId(string hullId, out R1ShipDesignDefinition design)
    {
        design = null;
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
