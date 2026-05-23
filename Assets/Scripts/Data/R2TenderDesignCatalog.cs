using System.Collections.Generic;

public static class R2TenderDesignCatalog
{
    private static readonly List<R1ShipDesignDefinition> designs = new List<R1ShipDesignDefinition>
    {
        new R1ShipDesignDefinition
        {
            shipId = "liquid_tanker",
            displayNameRu = "Жидковоз",
            requiredTechId = "liquid_tender_tanks",
            hullId = "liquid_tanker_hull",
            engineId = "liquid_tanker_engine",
            propellerId = "liquid_tanker_propeller",
            claudiumLoopId = "liquid_tanker_claudium_loop",
            specialModuleId = "liquid_tanker_transverse_tanks",
            expectedServiceMassKg = 4000f,
            expectedMaxTakeoffMassKg = 12000f,
            expectedEnginePowerKw = 360f,
            expectedStructureHp = 900f,
            expectedClaudiumLiftEfficiency = 45f
        },
        new R1ShipDesignDefinition
        {
            shipId = "gletcher",
            displayNameRu = "Глетчер",
            requiredTechId = "bulk_gas_tender",
            hullId = "gletcher_hull",
            engineId = "gletcher_engine",
            propellerId = "gletcher_propeller",
            claudiumLoopId = "gletcher_claudium_loop",
            specialModuleId = "gletcher_bulk_gasholders",
            expectedServiceMassKg = 5500f,
            expectedMaxTakeoffMassKg = 9000f,
            expectedEnginePowerKw = 300f,
            expectedStructureHp = 750f,
            expectedClaudiumLiftEfficiency = 45f
        },
        new R1ShipDesignDefinition
        {
            shipId = "vakhta",
            displayNameRu = "Вахта",
            requiredTechId = "workforce_health_tender",
            hullId = "vakhta_hull",
            engineId = "vakhta_engine",
            propellerId = "vakhta_propeller",
            claudiumLoopId = "vakhta_claudium_loop",
            specialModuleId = "vakhta_workforce_health_block",
            expectedServiceMassKg = 6000f,
            expectedMaxTakeoffMassKg = 11000f,
            expectedEnginePowerKw = 330f,
            expectedStructureHp = 900f,
            expectedClaudiumLiftEfficiency = 45f
        },
        new R1ShipDesignDefinition
        {
            shipId = "boxvan_tender",
            displayNameRu = "Фургонщик",
            requiredTechId = "boxed_goods_tender",
            hullId = "boxvan_tender_hull",
            engineId = "boxvan_tender_engine",
            propellerId = "boxvan_tender_propeller",
            claudiumLoopId = "boxvan_tender_claudium_loop",
            specialModuleId = "boxvan_tender_sections",
            expectedServiceMassKg = 4300f,
            expectedMaxTakeoffMassKg = 10000f,
            expectedEnginePowerKw = 320f,
            expectedStructureHp = 850f,
            expectedClaudiumLiftEfficiency = 45f
        },
        new R1ShipDesignDefinition
        {
            shipId = "stapel",
            displayNameRu = "Стапель",
            requiredTechId = "field_flying_dock",
            hullId = "stapel_hull",
            engineId = "stapel_engine",
            propellerId = "stapel_propeller",
            claudiumLoopId = "stapel_claudium_loop",
            specialModuleId = "stapel_flying_dock_bay",
            expectedServiceMassKg = 8000f,
            expectedMaxTakeoffMassKg = 16000f,
            expectedEnginePowerKw = 360f,
            expectedStructureHp = 1800f,
            expectedClaudiumLiftEfficiency = 55f
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
}
