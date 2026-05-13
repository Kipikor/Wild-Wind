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
    [InspectorName("Тип детали")]
    public ShipPartKind kind = ShipPartKind.Module;
    [InspectorName("Префаб")]
    [Tooltip("Для корпуса это основной префаб корабля с физикой, сокетами и коллайдерами. Для модуля это визуальный префаб, который будет вставлен в подходящий сокет.")]
    public GameObject prefab;
    [InspectorName("Тип топлива двигателя")]
    [Tooltip("Заполняется только у двигателя. Энергоемкость топлива берется из Item.csv.")]
    public string engineFuelId = "";

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
    [Tooltip("Произвольный тип слота. Например: engine_main, propeller, claudium_loop.")]
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
    [InspectorName("Масса корпуса/корабля")]
    BaseMass = 0,
    [InspectorName("Масса триммирования")]
    TargetTrimMass = 1,
    [InspectorName("Максимальная скорость винта")]
    PropellerMaxSpeedMS = 2,
    [InspectorName("КПД винта")]
    PropellerEfficiency = 3,
    [InspectorName("Максимальная тяга винта")]
    PropellerMaxThrustKgf = 4,
    [InspectorName("Мощность двигателя на 100%, кВт")]
    EngineMaxPower = 6,
    [InspectorName("КПД топлива двигателя")]
    EngineFuelEfficiency = 10,
    [InspectorName("Плотность воздуха")]
    AirDensity = 11,
    [InspectorName("Коэффициент сопротивления")]
    DragCoefficient = 12,
    [InspectorName("Лобовая площадь")]
    FrontalArea = 13,
    [InspectorName("Боковое сопротивление")]
    SideResistance = 14,
    [InspectorName("Множитель вертикальной площади")]
    VerticalAreaFactor = 15,
    [InspectorName("Лимит поворота автопилота")]
    MaxAutoTurnRateDeg = 21,
    [InspectorName("Конструкционный лимит поворота")]
    MaxStructuralTurnRateDeg = 22,
    [InspectorName("Конструкционный лимит вертикальной скорости")]
    MaxStructuralVerticalSpeed = 23,
    [InspectorName("Лимит вертикальной скорости автопилота")]
    MaxAutoVerticalSpeed = 24,
    [InspectorName("Жесткость высоты")]
    AltitudeStiffness = 27,
    [InspectorName("Демпфирование высоты")]
    AltitudeDamping = 28,
    [InspectorName("Допуск дрейфа высоты")]
    AltitudeDriftTolerance = 29,
    [InspectorName("Жесткость курса")]
    HeadingStiffness = 30,
    [InspectorName("Демпфирование курса")]
    HeadingDamping = 31,
    [InspectorName("Радиус точки маршрута")]
    WaypointRadius = 32,
    [InspectorName("Жесткость скорости")]
    SpeedStiffness = 34,
    [InspectorName("Демпфирование скорости")]
    SpeedDamping = 35,
    [InspectorName("Расход клавдия на тонну в секунду")]
    ClaudiumConsumptionPerTonSecond = 37,
    [InspectorName("КПД клавдиевого контура")]
    ClaudiumLiftEfficiency = 38,
    [InspectorName("Максимальная подъемная сила контура")]
    ClaudiumMaxLiftKg = 39,
    [InspectorName("Сглаживание подъемной силы")]
    ClaudiumLiftSmoothing = 40,
    [InspectorName("Макс. усилие поворота корпуса (Н*м)")]
    GyroTurnTorque = 43,
    [InspectorName("Демпфирование гироповорота")]
    GyroTurnDamping = 44,
    [InspectorName("Максимальная взлетная масса корпуса, кг")]
    HullMaxTakeoffMassKg = 45
}
