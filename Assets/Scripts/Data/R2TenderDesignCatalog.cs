using System.Collections.Generic;

public static class R2TenderDesignCatalog
{
    private static readonly List<R1ShipDesignDefinition> designs = new List<R1ShipDesignDefinition>();

    public static IReadOnlyList<R1ShipDesignDefinition> All => designs;

    public static bool TryGetByHullId(string hullId, out R1ShipDesignDefinition design)
    {
        design = null;
        return false;
    }
}
