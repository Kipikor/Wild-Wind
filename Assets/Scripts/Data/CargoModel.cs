using System;
using UnityEngine;

[Serializable]
public enum CargoUnitKind
{
    Piece
}

[Serializable]
public enum CargoStorageKind
{
    Van,
    BulkHold,
    LiquidTank,
    GasCylinder
}

[Serializable]
public class CargoCompartmentDefinition
{
    [InspectorName("Название")]
    public string displayName = "";

    [InspectorName("Тип отсека")]
    public CargoStorageKind storageKind = CargoStorageKind.Van;

    [InspectorName("Вместимость")]
    [Tooltip("Cargo compartment capacity in kilograms.")]
    public float capacity = 0f;

    public CargoCompartmentDefinition CloneNormalized()
    {
        CargoCompartmentDefinition clone = new CargoCompartmentDefinition
        {
            displayName = displayName ?? "",
            storageKind = storageKind,
            capacity = Mathf.Max(0f, capacity)
        };

        return clone;
    }
}

public static class ShipConsumableTankMath
{
    public static float CalculateFuelTankCapacityKg(float usefulPayloadKg)
    {
        if (usefulPayloadKg <= 0f) return 0f;
        return Mathf.Clamp(usefulPayloadKg * 0.12f, 50f, 2000f);
    }

    public static float CalculateClaudiumTankCapacityKg(float usefulPayloadKg)
    {
        if (usefulPayloadKg <= 0f) return 0f;
        return Mathf.Clamp(usefulPayloadKg * 0.06f, 25f, 1000f);
    }
}
