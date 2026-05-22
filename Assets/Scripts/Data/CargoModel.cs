using System;
using UnityEngine;

[Serializable]
public enum CargoUnitKind
{
    Piece,
    Passenger,
    VolumeLiter,
    Ship
}

[Serializable]
public enum CargoStorageKind
{
    Van,
    Cabin,
    BulkHold,
    LiquidTank,
    GasCylinder,
    RefrigeratedHold,
    ShipDock
}

[Serializable]
public enum ShipSizeClass
{
    None = 0,
    Boat = 1,
    Corvette = 2,
    Frigate = 3,
    Destroyer = 4,
    Cruiser = 5,
    Battleship = 6,
    Capital = 7
}

[Serializable]
public class CargoCompartmentDefinition
{
    [InspectorName("Название")]
    public string displayName = "";

    [InspectorName("Тип отсека")]
    public CargoStorageKind storageKind = CargoStorageKind.Van;

    [InspectorName("Вместимость")]
    [Tooltip("Фургон и салон считают штуки/места, кузов/цистерна/баллон/холодильник считают литры, док считает слоты.")]
    public float capacity = 0f;

    [InspectorName("Макс. класс корабля для дока")]
    public ShipSizeClass maxDockedShipClass = ShipSizeClass.None;

    [InspectorName("Масса корабля в доке")]
    [Range(0.01f, 1f)] public float dockedShipMassFactor = 0.1f;

    [InspectorName("Клавдий дока, кг/т/ч")]
    public float dockSupportClaudiumPerTonHour = 0.02f;

    public CargoCompartmentDefinition CloneNormalized()
    {
        return new CargoCompartmentDefinition
        {
            displayName = displayName ?? "",
            storageKind = storageKind,
            capacity = Mathf.Max(0f, capacity),
            maxDockedShipClass = maxDockedShipClass,
            dockedShipMassFactor = Mathf.Clamp(dockedShipMassFactor <= 0f ? 0.1f : dockedShipMassFactor, 0.01f, 1f),
            dockSupportClaudiumPerTonHour = Mathf.Max(0f, dockSupportClaudiumPerTonHour)
        };
    }
}
