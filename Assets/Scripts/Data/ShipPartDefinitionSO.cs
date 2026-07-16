using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "НоваяДетальКорабля", menuName = "Wild Wind/Корабли/Деталь корабля")]
public class ShipPartDefinitionSO : ScriptableObject
{
    [Header("Основное")]
    [InspectorName("Идентификатор детали")]
    [Tooltip("Технический идентификатор корпуса или модуля. Используется в сохранениях, древе техники и сборке корабля.")]
    public string partId = "part";
    [InspectorName("Название")]
    public string displayName = "Деталь";
    [InspectorName("Описание")]
    [TextArea] public string description = "";
    [InspectorName("Технология доступа")]
    [Tooltip("Если заполнено, деталь можно ставить только после завершения этой технологии. В CSV колонка называется complited_tech.")]
    public string completedTechId = "";
    [InspectorName("Тип детали")]
    public ShipPartKind kind = ShipPartKind.Module;
    [InspectorName("Префаб")]
    [Tooltip("Для корпуса это основной префаб корабля с физикой, сокетами и коллайдерами. Для модуля это визуальный префаб, который будет вставлен в подходящий сокет.")]
    public GameObject prefab;
    [Header("Runtime visual override")]
    [InspectorName("Visual prefab")]
    [Tooltip("Optional imported Blender visual that is mounted on top of the gameplay hull prefab.")]
    public GameObject visualPrefab;
    [InspectorName("Hide fallback renderers")]
    [Tooltip("Hide mesh renderers from the gameplay hull prefab when the imported visual is active.")]
    public bool hidePrefabRenderersWhenVisualPrefabSet = true;
    [InspectorName("Visual local position")]
    public Vector3 visualLocalPosition = Vector3.zero;
    [InspectorName("Visual local euler")]
    public Vector3 visualLocalEulerAngles = Vector3.zero;
    [InspectorName("Visual local scale")]
    public Vector3 visualLocalScale = Vector3.one;
    [InspectorName("Runtime ship catalog id")]
    [Tooltip("Ship_catalog.csv id used when this hull needs quick-mission balance.")]
    public string runtimeShipTreeId = "";
    [InspectorName("Fuel resource")]
    [Tooltip("Resource burned by the hull at its fixed fuel consumption rate.")]
    public string fuelResourceId = "";

    [Header("Слоты корпуса")]
    [InspectorName("Слоты")]
    [Tooltip("Слоты, которые дает корпус. Обязательность задается у каждого слота отдельно и не зависит от типа слота.")]
    public List<ShipSlotDefinition> slots = new List<ShipSlotDefinition>();

    [Header("Совместимость модуля")]
    [InspectorName("Подходит к типам слотов")]
    [Tooltip("Типы слотов, в которые можно поставить этот модуль.")]
    public List<string> compatibleSlotTypeIds = new List<string>();
    [InspectorName("Дополнительные слоты")]
    [Tooltip("Слоты, которые появятся на корабле после установки этого модуля.")]
    public List<ShipSlotDefinition> grantedSlots = new List<ShipSlotDefinition>();

    [Header("Характеристики")]
    [InspectorName("Изменения характеристик")]
    [Tooltip("Модуль может перезаписать характеристику или изменить ее. Две детали не могут одновременно перезаписывать одну характеристику.")]
    public List<ShipStatModifier> statModifiers = new List<ShipStatModifier>();

    public bool IsHull => kind == ShipPartKind.Hull;
    public bool IsModule => kind == ShipPartKind.Module;

    public bool CanFitSlot(ShipSlotDefinition slot)
    {
        if (slot == null || !IsModule) return false;
        if (!slot.AllowsPart(partId)) return false;
        if (compatibleSlotTypeIds == null || compatibleSlotTypeIds.Count == 0) return false;

        for (int i = 0; i < compatibleSlotTypeIds.Count; i++)
        {
            if (compatibleSlotTypeIds[i] == slot.slotTypeId)
            {
                return true;
            }
        }

        return false;
    }
}

public enum ShipPartKind
{
    [InspectorName("Корпус")]
    Hull,
    [InspectorName("Модуль")]
    Module
}

[Serializable]
public class ShipSlotDefinition
{
    [InspectorName("Идентификатор слота")]
    [Tooltip("Уникальный идентификатор слота внутри корпуса или модуля.")]
    public string slotId = "slot";
    [InspectorName("Название")]
    public string displayName = "Слот";
    [InspectorName("Тип слота")]
    [Tooltip("Произвольный тип слота. Например: claudium_loop, high, mid, low, rig.")]
    public string slotTypeId = "module";
    [InspectorName("Обязательный")]
    [Tooltip("Если включено, корабль нельзя собрать без подходящего купленного модуля в этом слоте.")]
    public bool required = true;
    [InspectorName("Разрешенные детали")]
    [Tooltip("Если список пуст, подходит любой модуль совместимого типа. Если заполнен, можно ставить только эти идентификаторы деталей.")]
    public List<string> allowedPartIds = new List<string>();

    public bool AllowsPart(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId)) return false;
        if (allowedPartIds == null || allowedPartIds.Count == 0) return true;

        for (int i = 0; i < allowedPartIds.Count; i++)
        {
            if (allowedPartIds[i] == partId)
            {
                return true;
            }
        }

        return false;
    }

    public ShipSlotDefinition CloneWithId(string newSlotId)
    {
        ShipSlotDefinition clone = new ShipSlotDefinition
        {
            slotId = newSlotId,
            displayName = displayName,
            slotTypeId = slotTypeId,
            required = required,
            allowedPartIds = new List<string>()
        };

        if (allowedPartIds != null)
        {
            clone.allowedPartIds.AddRange(allowedPartIds);
        }

        return clone;
    }
}

[Serializable]
public class ShipStatModifier
{
    [InspectorName("Характеристика")]
    public ShipStatId stat = ShipStatId.BaseMass;
    [InspectorName("Операция")]
    [Tooltip("Перезапись задает точное значение. Изменение прибавляет или вычитает. Множитель умножает уже собранное значение.")]
    public ShipStatOperation operation = ShipStatOperation.Add;
    [InspectorName("Значение")]
    public float value;
}

public enum ShipStatOperation
{
    [InspectorName("Перезаписать")]
    Set,
    [InspectorName("Изменить")]
    Add,
    [InspectorName("Умножить")]
    Multiply
}

public enum ShipStatId
{
    [InspectorName("Base mass")]
    BaseMass = 0,
    [InspectorName("Legacy unused")]
    HullLegacyUnused1 = 1,
    [InspectorName("Hull cruise reference speed, m/s")]
    HullCruiseReferenceSpeedMS = 2,
    [InspectorName("Legacy unused")]
    HullLegacyUnused3 = 3,
    [InspectorName("Hull forward thrust, kgf")]
    HullForwardThrustKgf = 6,
    [InspectorName("Fuel consumption, kg/min")]
    FuelConsumptionKgPerMinute = 7,
    [InspectorName("Legacy unused")]
    HullLegacyUnused9 = 9,
    [InspectorName("Legacy unused")]
    HullLegacyUnused10 = 10,
    [InspectorName("Legacy unused")]
    HullLegacyUnused11 = 11,
    [InspectorName("Legacy unused")]
    HullLegacyUnused12 = 12,
    [InspectorName("Legacy unused")]
    HullLegacyUnused13 = 13,
    [InspectorName("Legacy unused")]
    HullLegacyUnused14 = 14,
    [InspectorName("Legacy unused")]
    HullLegacyUnused15 = 15,
    [InspectorName("Strategic yaw rate, deg/s")]
    StrategicYawRateDegPerSecond = 21,
    [InspectorName("Strategic yaw acceleration, deg/s2")]
    StrategicYawAccelerationDegPerSecond2 = 22,
    [InspectorName("Strategic vertical speed, m/s")]
    StrategicVerticalSpeedMS = 23,
    [InspectorName("Strategic vertical acceleration, m/s2")]
    StrategicVerticalAccelerationMS2 = 24,
    [InspectorName("Legacy unused")]
    HullLegacyUnused27 = 27,
    [InspectorName("Legacy unused")]
    HullLegacyUnused28 = 28,
    [InspectorName("Legacy unused")]
    HullLegacyUnused29 = 29,
    [InspectorName("Legacy unused")]
    HullLegacyUnused30 = 30,
    [InspectorName("Legacy unused")]
    HullLegacyUnused31 = 31,
    [InspectorName("Legacy unused")]
    HullLegacyUnused34 = 34,
    [InspectorName("Legacy unused")]
    HullLegacyUnused35 = 35,
    [InspectorName("Legacy unused")]
    ClaudiumLegacyUnused37 = 37,
    [InspectorName("Legacy unused")]
    ClaudiumLegacyUnused38 = 38,
    [InspectorName("Claudium max lift, kg")]
    ClaudiumMaxLiftKg = 39,
    [InspectorName("Legacy unused")]
    ClaudiumLegacyUnused40 = 40,
    [InspectorName("Legacy unused")]
    HullLegacyUnused43 = 43,
    [InspectorName("Legacy unused")]
    HullLegacyUnused44 = 44,
    [InspectorName("Hull max takeoff mass, kg")]
    HullMaxTakeoffMassKg = 45,
    [InspectorName("Mining impact hold capacity, kg")]
    MiningImpactHoldCapacityKg = 60,
    [InspectorName("Mining impact damage multiplier")]
    MiningImpactDamageTakenMultiplier = 61,
    [InspectorName("Cargo van capacity, kg")]
    CargoVanCapacityKg = 100,
    [InspectorName("Bulk hold capacity, kg")]
    BulkHoldCapacityKg = 102,
    [InspectorName("Liquid tank capacity, kg")]
    LiquidTankCapacityKg = 103,
    [InspectorName("Gas cylinder capacity, kg")]
    GasCylinderCapacityKg = 104,
    [InspectorName("Structure HP")]
    StructureHp = 111
}
