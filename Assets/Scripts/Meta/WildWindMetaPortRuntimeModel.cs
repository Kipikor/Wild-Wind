using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MetaShipDefinition
{
    public string shipId = "";
    public int tier = 1;
    public int maxSorties = 5;
    public bool starterRecoveryShip;
    public readonly List<string> preinstalledModules = new List<string>();
    public int highSlots;
    public int midSlots;
    public int lowSlots;
    public int rigSlots;
}

public sealed class MetaShipInstance
{
    public string instanceId = Guid.NewGuid().ToString("N");
    public string shipId = "";
    public int tier = 1;
    public int remainingSorties = 5;
    public int maxSorties = 5;
    public readonly List<string> preinstalledModules = new List<string>();
    public readonly List<string> fittedModules = new List<string>();
    public readonly List<string> rigs = new List<string>();
}

public sealed class MetaAccountState
{
    public int portSlots = 5;
    public readonly List<MetaShipInstance> ships = new List<MetaShipInstance>();
    public readonly Dictionary<string, int> storage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, float> processingBuffers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProcessingInputProfile
{
    public string inputId = "";
    public int cycleInputUnits = 10;
    public float efficiency = 0.5f;
    public readonly Dictionary<string, float> composition = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
}

public sealed class MetaOperationResult
{
    public bool success;
    public string message = "";
}

public sealed class WildWindMetaCatalog
{
    public readonly Dictionary<string, MetaShipDefinition> ships = new Dictionary<string, MetaShipDefinition>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, ProcessingInputProfile> processing = new Dictionary<string, ProcessingInputProfile>(StringComparer.OrdinalIgnoreCase);
}

public static class WildWindMetaMechanics
{
    public const int InitialPortSlots = 5;

    public static WildWindMetaCatalog CreateMinimalCatalog()
    {
        WildWindMetaCatalog catalog = new WildWindMetaCatalog();
        MetaShipDefinition pioneer = new MetaShipDefinition
        {
            shipId = "pioneer",
            tier = 1,
            maxSorties = 7,
            starterRecoveryShip = true,
            highSlots = 3,
            midSlots = 1,
            lowSlots = 1,
            rigSlots = 1
        };
        pioneer.preinstalledModules.Add("basic_engine");
        catalog.ships[pioneer.shipId] = pioneer;

        catalog.ships["cruiser203_hull"] = new MetaShipDefinition { shipId = "cruiser203_hull", tier = 2, maxSorties = 8, highSlots = 3, midSlots = 1, lowSlots = 2, rigSlots = 1 };

        ProcessingInputProfile ore = new ProcessingInputProfile { inputId = "ognejar_ore", cycleInputUnits = 10, efficiency = 0.5f };
        ore.composition["charcoal"] = 0.10f;
        ore.composition["ferron"] = 0.20f;
        catalog.processing[ore.inputId] = ore;
        return catalog;
    }

    public static MetaAccountState CreateFreshAccount(WildWindMetaCatalog catalog)
    {
        MetaAccountState account = new MetaAccountState
        {
            portSlots = InitialPortSlots
        };
        EnsureStarterPioneerRecovery(account, catalog);
        return account;
    }

    public static bool EnsureStarterPioneerRecovery(MetaAccountState account, WildWindMetaCatalog catalog)
    {
        if (account == null || catalog == null) return false;
        if (account.ships.Count > 0) return false;
        if (!catalog.ships.TryGetValue("pioneer", out MetaShipDefinition pioneer)) return false;
        account.ships.Add(CreateShipInstance(pioneer));
        return true;
    }

    public static bool ConsumeShipSortie(MetaShipInstance ship)
    {
        if (ship == null || ship.remainingSorties <= 0) return false;
        ship.remainingSorties--;
        return ship.remainingSorties > 0;
    }

    public static MetaOperationResult ProcessOneCycle(MetaAccountState account, ProcessingInputProfile profile)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || profile == null || string.IsNullOrWhiteSpace(profile.inputId))
        {
            result.message = "Bad processing input.";
            return result;
        }

        int available = GetStorage(account, profile.inputId);
        if (available < profile.cycleInputUnits)
        {
            result.message = "Not enough raw input.";
            return result;
        }

        AddStorage(account, profile.inputId, -profile.cycleInputUnits);
        foreach (KeyValuePair<string, float> entry in profile.composition)
        {
            float producedFraction = profile.cycleInputUnits * Mathf.Max(0f, entry.Value) * Mathf.Clamp01(profile.efficiency);
            string bufferKey = "buffer:" + entry.Key;
            account.processingBuffers.TryGetValue(bufferKey, out float buffer);
            buffer += producedFraction;
            int whole = Mathf.FloorToInt(buffer);
            if (whole > 0)
            {
                AddStorage(account, entry.Key, whole);
                buffer -= whole;
            }

            account.processingBuffers[bufferKey] = buffer;
        }

        result.success = true;
        result.message = "Processed one batch.";
        return result;
    }

    public static int GetStorage(MetaAccountState account, string itemId)
    {
        if (account == null || string.IsNullOrWhiteSpace(itemId)) return 0;
        return account.storage.TryGetValue(itemId, out int amount) ? amount : 0;
    }

    public static void AddStorage(MetaAccountState account, string itemId, int amount)
    {
        if (account == null || string.IsNullOrWhiteSpace(itemId) || amount == 0) return;
        account.storage.TryGetValue(itemId, out int current);
        current = Mathf.Max(0, current + amount);
        if (current == 0)
        {
            account.storage.Remove(itemId);
        }
        else
        {
            account.storage[itemId] = current;
        }
    }

    private static MetaShipInstance CreateShipInstance(MetaShipDefinition definition)
    {
        MetaShipInstance instance = new MetaShipInstance
        {
            shipId = definition.shipId,
            tier = definition.tier,
            remainingSorties = definition.maxSorties,
            maxSorties = definition.maxSorties
        };
        instance.preinstalledModules.AddRange(definition.preinstalledModules);
        return instance;
    }
}
