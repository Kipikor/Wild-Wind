using System.Collections.Generic;
using UnityEngine;

public static class CargoStoragePlanner
{
    public static Dictionary<string, int> ToCargoMap(List<ResourceStack> cargo)
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        if (cargo == null) return map;

        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;

            map[stack.resourceId] = map.TryGetValue(stack.resourceId, out int current)
                ? current + stack.amount
                : stack.amount;
        }

        return map;
    }

    public static List<CargoCompartmentDefinition> BuildStatCompartments(
        ShipStatBlock stats,
        List<CargoCompartmentDefinition> baseCompartments = null)
    {
        List<CargoCompartmentDefinition> compartments = new List<CargoCompartmentDefinition>();
        if (baseCompartments != null)
        {
            for (int i = 0; i < baseCompartments.Count; i++)
            {
                CargoCompartmentDefinition compartment = baseCompartments[i];
                if (compartment == null || compartment.capacity <= 0f) continue;

                compartments.Add(compartment.CloneNormalized());
            }
        }

        AddStatCompartment(compartments, stats, ShipStatId.CargoVanCapacityKg, CargoStorageKind.Van, "Van");
        AddStatCompartment(compartments, stats, ShipStatId.BulkHoldCapacityKg, CargoStorageKind.BulkHold, "Bulk hold");
        AddStatCompartment(compartments, stats, ShipStatId.LiquidTankCapacityKg, CargoStorageKind.LiquidTank, "Liquid tank");
        AddStatCompartment(compartments, stats, ShipStatId.GasCylinderCapacityKg, CargoStorageKind.GasCylinder, "Gas cylinders");

        return compartments;
    }

    public static float GetCargoMassKg(SessionConfigDatabase config, List<ResourceStack> cargo)
    {
        if (cargo == null) return 0f;

        float total = 0f;
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;

            total += config != null ? config.GetItemTransportMassKg(stack.resourceId, stack.amount) : stack.amount;
        }

        return total;
    }

    public static float GetCargoMassKg(SessionConfigDatabase config, Dictionary<string, int> cargo)
    {
        if (cargo == null) return 0f;

        float total = 0f;
        foreach (KeyValuePair<string, int> pair in cargo)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) continue;

            total += config != null ? config.GetItemTransportMassKg(pair.Key, pair.Value) : pair.Value;
        }

        return total;
    }

    public static bool TryValidateCargoStorage(
        SessionConfigDatabase config,
        List<CargoCompartmentDefinition> compartments,
        List<ResourceStack> cargo,
        out string error)
    {
        return TryValidateCargoStorage(config, compartments, ToCargoMap(cargo), out error);
    }

    public static bool TryValidateCargoStorage(
        SessionConfigDatabase config,
        List<CargoCompartmentDefinition> compartments,
        Dictionary<string, int> cargo,
        out string error)
    {
        error = "";
        if (cargo == null || cargo.Count == 0) return true;
        if (compartments == null || compartments.Count == 0) return true;

        float cargoMassKg = 0f;

        foreach (KeyValuePair<string, int> pair in cargo)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) continue;
            cargoMassKg += config != null
                ? config.GetItemTransportMassKg(pair.Key, pair.Value)
                : Mathf.Max(0, pair.Value);
        }

        float cargoCapacityKg = GetGeneralCargoCapacityKg(compartments);
        if (cargoMassKg > cargoCapacityKg + 0.001f)
        {
            error = $"Cargo holds overloaded by mass: {cargoMassKg:F0}/{cargoCapacityKg:F0} kg.";
            return false;
        }

        return true;
    }

    private static void AddStatCompartment(
        List<CargoCompartmentDefinition> compartments,
        ShipStatBlock stats,
        ShipStatId stat,
        CargoStorageKind kind,
        string displayName)
    {
        float capacity = stats != null ? stats.Get(stat, 0f) : 0f;
        if (capacity <= 0f) return;

        compartments.Add(new CargoCompartmentDefinition
        {
            displayName = displayName,
            storageKind = kind,
            capacity = capacity
        });
    }

    private static float GetGeneralCargoCapacityKg(List<CargoCompartmentDefinition> compartments)
    {
        float capacity = 0f;
        if (compartments == null) return capacity;

        for (int i = 0; i < compartments.Count; i++)
        {
            CargoCompartmentDefinition compartment = compartments[i];
            if (compartment == null || compartment.capacity <= 0f) continue;

            capacity += Mathf.Max(0f, compartment.capacity);
        }

        return capacity;
    }
}
