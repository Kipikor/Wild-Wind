using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
[System.Serializable]
public class ShipGunGroup
{
    [InspectorName("ID")]
    public string groupId = "main";
    [InspectorName("Название")]
    public string displayNameRu = "Главный калибр";
    [InspectorName("Включена")]
    public bool enabled = true;
    [InspectorName("Режим огня")]
    public ShipGunFireMode fireMode = ShipGunFireMode.Manual;
    [InspectorName("Снаряд")]
    public DamageShellPreset shell = new DamageShellPreset();
    [InspectorName("Ствол")]
    public Transform muzzle;
    [InspectorName("Поворотная часть")]
    public Transform yawPivot;
    [InspectorName("Качающаяся часть")]
    public Transform pitchPivot;
    [InspectorName("Локальное смещение ствола")]
    public Vector3 localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f);
    [InspectorName("Стволов в залпе")]
    [Min(1)] public int barrelsPerSalvo = 1;
    [InspectorName("Выстрелов в минуту")]
    [Min(0f)] public float roundsPerMinute = 60f;
    [InspectorName("Скорость поворота башни, град/с")]
    [Min(0f)] public float yawSpeedDegPerSecond = 45f;
    public bool useYawLimits;
    public float minYawDegrees = -180f;
    public float maxYawDegrees = 180f;
    [InspectorName("Скорость подъёма ствола, град/с")]
    [Min(0f)] public float elevationUpSpeedDegPerSecond = 25f;
    [InspectorName("Скорость опускания ствола, град/с")]
    [Min(0f)] public float elevationDownSpeedDegPerSecond = 35f;
    [InspectorName("Минимальный угол ствола, град")]
    public float minElevationDegrees = -5f;
    [InspectorName("Максимальный угол ствола, град")]
    public float maxElevationDegrees = 55f;
    [InspectorName("Допуск готовности к выстрелу, град")]
    [Min(0f)] public float fireAlignmentToleranceDegrees = 1.5f;
    [InspectorName("Дальность от центра корабля, м")]
    [Min(1f)] public float maxRangeMeters = 1500f;
    [InspectorName("Начальная скорость, м/с")]
    [Min(1f)] public float muzzleVelocityMS = 320f;
    [InspectorName("Масса снаряда, кг")]
    [Min(0.01f)] public float projectileMassKg = 8f;
    [InspectorName("Радиус снаряда, м")]
    [Min(0.01f)] public float projectileRadiusMeters = 0.08f;
    [InspectorName("Горизонтальный разброс на максимальной дальности, м")]
    [Min(0f)] public float horizontalSpreadAtMaxRangeMeters = 18f;
    [InspectorName("Вертикальный разброс на максимальной дальности, м")]
    [Min(0f)] public float verticalSpreadAtMaxRangeMeters = 8f;
    [InspectorName("Множитель гравитации")]
    [Min(0f)] public float gravityScale = 1f;
    [InspectorName("Trail, сек")]
    [Min(0f)] public float projectileTrailSeconds = 1.6f;
    [InspectorName("Автоцели: корабли")]
    public bool automaticTargetsDamageableShips;

    [System.NonSerialized] public float nextShotTime;

    public float SecondsBetweenSalvos
    {
        get
        {
            return roundsPerMinute > 0.001f ? 60f / roundsPerMinute : float.PositiveInfinity;
        }
    }
}

public class ShipPhysics : MonoBehaviour
{
    private const float EngineAfterburnerPowerMultiplier = 1.2f;
    private const float MainGunRoundsPerMinute = 60f;
    private const float ManualGunAimTargetSmoothSeconds = 0.055f;
    private const float ManualGunSphereAimSensitivityDegreesPerPixel = 0.18f;
    private const float ManualGunSphereAimMinPitchDegrees = -89f;
    private const float ManualGunSphereAimMaxPitchDegrees = 89f;
    private const float GunProjectileTrailBrightnessMultiplier = 6f;
    private const float GunProjectileTrailEmissionMultiplier = 2.5f;
    public const float ClaudiumSlipstreamActivationMinSpeedMS = 15f;
    public const float ClaudiumSlipstreamActivationSeconds = 20f;
    private const float ClaudiumSlipstreamFullDragMultiplier = 0.05f;
    private const float ClaudiumSlipstreamFullClaudiumMultiplier = 10f;

    private Rigidbody rb;

    [Header("Параметры корабля")]
    public float baseMass = 1000f; // Стартовая масса 1000кг
    [Tooltip("Максимальная взлетная масса, которую допускает корпус, в килограммах.")]
    public float hullMaxTakeoffMassKg = 2000f;
    [HideInInspector] public float cargoMassKg;

    [Header("Двигатель")]
    [Tooltip("Мощность, которую двигатель выдает на ручке 100%, в киловаттах.")]
    public float enginePowerKwAt100 = 80f;
    [Tooltip("Тип топлива. Энергоемкость берется из Item.csv по этому id.")]
    public string engineFuelId = "charcoal";
    [Tooltip("Доля энергии топлива, которая превращается в полезную мощность двигателя.")]
    public float engineFuelEfficiency = 0.32f;
    [Tooltip("Разрешает двигателю использовать форсажную зону сверх номинальных 100% мощности.")]
    public bool engineAfterburnerEnabled;
    [HideInInspector] public float engineFuelEnergyKwhPerKg = 4f;
    [HideInInspector] public float engineFuelStockKg = 0f;
    [Tooltip("How fast the engine output follows the requested power, as a fraction of the current maximum per second.")]
    public float engineResponseRate01PerSecond = 0.10f;
    [Tooltip("Stop gear applies direct horizontal braking without asking the propeller to oscillate through reverse.")]
    public bool neutralStopBrakeEnabled;
    public float neutralStopBrakeMaxDecelerationMS2 = 8f;
    public float neutralStopBrakeStopTimeSeconds = 0.75f;
    public float neutralStopBrakeDeadzoneMS = 0.05f;

    [Header("Параметры винта")]
    [Tooltip("Справочная расчетная скорость винта, м/с. Физическая тяга не отсекается по этой скорости.")]
    public float propellerMaxSpeedMS = 30f;
    [Tooltip("Доля мощности двигателя, которая превращается в полезную тягу винта.")]
    public float propellerEfficiency = 0.8f;
    [Header("Клавдиевый контур")]
    [Tooltip("Ресурс клавдия в грузовом списке корабля.")]
    public string claudiumResourceId = "claudium";
    [HideInInspector] public float claudiumStock = 0f;
    [Tooltip("Расход клавдия в секунду на одну тонну поддерживаемой массы.")]
    public float claudiumConsumptionPerTonSecond = 0.05f;
    [Tooltip("Сколько килограммов подъема дает один киловатт мощности двигателя.")]
    public float claudiumLiftEfficiency = 10f;
    [Tooltip("Максимальная масса в килограммах, которую контур способен поддерживать.")]
    public float claudiumMaxLiftKg = 1000f;
    [Tooltip("Скорость сглаживания подъемной силы. Чем больше значение, тем быстрее контур выходит на нужную силу.")]
    public float claudiumLiftSmoothing = 2f;
    [Tooltip("How fast the claudium loop changes actual lift, as a fraction of maximum lift per second.")]
    public float claudiumLoopResponseRate01PerSecond = 0.10f;
    [HideInInspector] public float claudiumCurrentLiftN;
    [HideInInspector] public float claudiumPowerDrawWatts;
    [HideInInspector] public float claudiumPowerDrawKw;
    [HideInInspector] public float claudiumRequestedLiftKg;
    [Tooltip("Claudium slipstream request. Can be enabled only above 15 m/s, then ramps over 20 seconds.")]
    public bool claudiumSlipstreamEnabled;
    [HideInInspector] public float claudiumSlipstreamCharge01;


    [Header("Майнинг")]
    [Tooltip("Противоударный кузов ловит падающие куски. Пойманная руда складывается в общий груз корабля.")]
    public float miningImpactHoldCapacityKg;
    [Tooltip("Множитель урона от пойманных падающих рудных глыб. 0.5 = противоударный кузов получает вдвое меньше.")]
    public float miningImpactDamageTakenMultiplier = 1f;
    public float miningImpactMinDamageSpeedMS = 4f;
    public float miningImpactDamageScale = 10f;
    [Tooltip("Радиус сбора падающих кусков вокруг корабля.")]
    public float miningCatchRadiusMeters = 10f;
    [Tooltip("Дальность временной кнопки выстрела по глыбе.")]
    public float miningManualShotRangeMeters = 180f;
    [HideInInspector] public string miningLastMessage = "";

    public string weaponResourceId = "weapon";
    [Tooltip("Сколько килограммов оружия тратит один ручной выстрел.")]
    public float weaponShotCostKg = 0f;
    [Tooltip("Показывать прицел и время полета у курсора для ручной орудийной группы.")]
    public bool showGunAimHud = true;
    [Tooltip("Индекс ручной группы в списке shipGunGroups.")]
    public int manualGunGroupIndex;
    [Tooltip("Орудийные группы корабля: главный калибр, ПМК и будущие группы.")]
    public List<ShipGunGroup> shipGunGroups = new List<ShipGunGroup>
    {
        new ShipGunGroup
        {
            groupId = "main",
            displayNameRu = "Главный калибр",
            fireMode = ShipGunFireMode.Manual,
            localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
            barrelsPerSalvo = 1,
            roundsPerMinute = MainGunRoundsPerMinute,
            yawSpeedDegPerSecond = 45f,
            elevationUpSpeedDegPerSecond = 22f,
            elevationDownSpeedDegPerSecond = 30f,
            minElevationDegrees = -5f,
            maxElevationDegrees = 55f,
            fireAlignmentToleranceDegrees = 1.5f,
            maxRangeMeters = 2500f,
            muzzleVelocityMS = 360f,
            projectileMassKg = 12f,
            projectileRadiusMeters = 0.10f,
            horizontalSpreadAtMaxRangeMeters = 24f,
            verticalSpreadAtMaxRangeMeters = 9f,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 76 мм",
                shellType = DamageShellType.ArmorPiercing,
                caliberMm = 76f,
                damagePoints = 120f,
                hullDamageOnPenetration = 120f,
                penetrationMm = 85f,
                penetrationAtMaxRangeMultiplier = 0.55f,
                velocityRetentionAtMaxRange = 0.62f,
                normalizationDegrees = 5f,
                penetrationRollSpread = 0.08f,
                projectileColor = Color.red
            }
        },
        new ShipGunGroup
        {
            groupId = "secondary",
            displayNameRu = "ПМК",
            fireMode = ShipGunFireMode.Automatic,
            localMuzzleOffset = new Vector3(0f, 1.2f, -7.5f),
            barrelsPerSalvo = 1,
            roundsPerMinute = 200f,
            yawSpeedDegPerSecond = 140f,
            elevationUpSpeedDegPerSecond = 90f,
            elevationDownSpeedDegPerSecond = 110f,
            minElevationDegrees = -10f,
            maxElevationDegrees = 70f,
            fireAlignmentToleranceDegrees = 3f,
            maxRangeMeters = 1500f,
            muzzleVelocityMS = 420f,
            projectileMassKg = 0.12f,
            projectileRadiusMeters = 0.035f,
            horizontalSpreadAtMaxRangeMeters = 35f,
            verticalSpreadAtMaxRangeMeters = 16f,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 20 мм",
                shellType = DamageShellType.ArmorPiercing,
                caliberMm = 20f,
                damagePoints = 18f,
                hullDamageOnPenetration = 18f,
                penetrationMm = 18f,
                penetrationAtMaxRangeMultiplier = 0.45f,
                velocityRetentionAtMaxRange = 0.58f,
                normalizationDegrees = 2f,
                penetrationRollSpread = 0.12f,
                projectileColor = new Color(0.2f, 0.85f, 1f, 1f)
            }
        }
    };
    [HideInInspector] public string weaponLastMessage = "";
    [HideInInspector] public float weaponAimFlightTimeSeconds;
    [HideInInspector] public float weaponAimDistanceMeters;
    [HideInInspector] public string weaponAimStatus = "";

    public float gyroTurnTorque = 12000f; // Максимальный внутренний момент поворота корпуса, Н*м
    public float gyroTurnDamping = 0.8f; // Демпфирование, которое гасит лишнюю угловую скорость
    [HideInInspector] public float currentGyroTurnTorque = 0f;

    [Header("Аэродинамика")]
    public float airDensity = 1.225f; // Плотность воздуха (1.225 на уровне моря)
    public float dragCoefficient = 1.1f; // Коэффициент формы Cd (1.1 для контейнера)
    public float frontalArea = 6.3f; // Лобовая площадь (кв.м)
    public float sideResistance = 2.0f; // Сопротивление боковому сносу (эффект киля)
    public float verticalAreaFactor = 4.0f; // Во сколько раз площадь "пуза" больше лобовой площади

    [Header("Боковая всенаправленная тяга")]
    [Tooltip("Максимальная боковая сила для ручного скольжения A/D, в килограмм-силах.")]
    public float lateralOmniThrustKgf = 260f;

    [Header("Лимиты скорости подъема")]
    public float maxStructuralVerticalSpeed = 10.0f; // Предел прочности (конструкционный)
    public float maxAutoVerticalSpeed = 10.0f;       // Лимит ручного вертикального контура

    [Header("Окружающая среда")]
    public Vector3 windVelocity = Vector3.zero; // Глобальный вектор ветра (м/с)

    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float ClaudiumSlipstreamCharge01 => Mathf.Clamp01(claudiumSlipstreamCharge01);
    public float ClaudiumSlipstreamDragMultiplier => Mathf.Lerp(1f, ClaudiumSlipstreamFullDragMultiplier, ClaudiumSlipstreamCharge01);
    public float ClaudiumSlipstreamClaudiumMultiplier => Mathf.Lerp(1f, ClaudiumSlipstreamFullClaudiumMultiplier, ClaudiumSlipstreamCharge01);
    public bool ClaudiumSlipstreamActive => claudiumSlipstreamEnabled;
    public float HorizontalSpeedMS => GetHorizontalSpeedMS();
    public bool CanActivateClaudiumSlipstream => HorizontalSpeedMS > ClaudiumSlipstreamActivationMinSpeedMS;
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * ClaudiumSlipstreamDragMultiplier * frontalArea;
    public float CurrentWindAerodynamicFactor => Mathf.Max(0f, dragCoefficient * ClaudiumSlipstreamDragMultiplier);
    public Vector3 EffectiveWindVelocity => GetEffectiveWindVelocity();
    public float EnginePowerLeverLimit => engineAfterburnerEnabled
        ? EngineAfterburnerPowerMultiplier
        : 1f;
    public float EnginePowerCapacityKw => Mathf.Max(0f, enginePowerKwAt100) * EnginePowerLeverLimit;

    public bool TrySetClaudiumSlipstreamEnabled(bool enabled, out string reason)
    {
        reason = "";
        if (enabled && !CanActivateClaudiumSlipstream)
        {
            claudiumSlipstreamEnabled = false;
            reason = "Claudium slipstream needs speed above "
                + ClaudiumSlipstreamActivationMinSpeedMS.ToString("0")
                + " m/s.";
            return false;
        }

        claudiumSlipstreamEnabled = enabled;
        reason = claudiumSlipstreamEnabled
            ? "Claudium slipstream enabled."
            : "Claudium slipstream disabled.";
        return true;
    }

    public float CalculateEnginePowerLeverForPropellerEngagement(float propellerEngagement)
    {
        float maxLever = EnginePowerLeverLimit;
        if (enginePowerKwAt100 <= 0f)
        {
            return 0f;
        }

        float supportLever = Mathf.Clamp(CalculateCurrentSupportPowerDrawKw() / enginePowerKwAt100, 0f, maxLever);
        float remainingLever = Mathf.Max(0f, maxLever - supportLever);
        return Mathf.Clamp(supportLever + Mathf.Clamp01(propellerEngagement) * remainingLever, supportLever, maxLever);
    }

    public float EstimateFullSlipstreamCruiseSpeedMS()
    {
        float dragPerSpeedSquared = 0.5f
            * Mathf.Max(0f, airDensity)
            * Mathf.Max(0f, dragCoefficient)
            * ClaudiumSlipstreamFullDragMultiplier
            * Mathf.Max(0f, frontalArea);
        if (dragPerSpeedSquared <= 0.0001f || propellerEfficiency <= 0f)
        {
            return Mathf.Max(1f, propellerMaxSpeedMS > 0f ? propellerMaxSpeedMS : 1f);
        }

        float mass = rb != null ? Mathf.Max(1f, rb.mass) : GetTotalMassKg();
        float liftPowerKw = claudiumLiftEfficiency > 0f
            ? CalculateClaudiumPowerKwForLift(Mathf.Min(mass, Mathf.Max(0f, claudiumMaxLiftKg)))
            : 0f;
        float modulePowerKw = CalculatePoweredModulePowerRequestKw(liftPowerKw);
        float availablePropellerPowerKw = Mathf.Max(0f, EnginePowerCapacityKw - liftPowerKw - modulePowerKw);
        float usefulPowerW = availablePropellerPowerKw * Mathf.Clamp01(propellerEfficiency) * 1000f;
        if (usefulPowerW <= 0.001f)
        {
            return 1f;
        }

        float terminalSpeedMS = Mathf.Pow(usefulPowerW / dragPerSpeedSquared, 1f / 3f);
        float configuredCap = propellerMaxSpeedMS > 0f ? propellerMaxSpeedMS : terminalSpeedMS;
        return Mathf.Max(1f, Mathf.Min(terminalSpeedMS, configuredCap));
    }

    [Header("Автопилот и Системы")]
    public bool autoStabilizeAtStart = true; // Новая галочка
    public bool altitudeHold = false;
    public float targetAltitude = 0f;
    public float altStiffness = 0.2f; // P (Было 0.5 - слишком резко)
    public float altDamping = 1.2f;   // D
    public float altDriftTolerance = 0.15f; // Допуск дрейфа (м)

    public bool cruiseControl = false;
    public float targetSpeedMS = 0f;
    [Header("Автопилот курса")]
    public bool headingHold = false;
    public float targetHeading = 0f;      // Целевой курс (градусы 0-360)
    public float headingStiffness = 0.5f; // P-терм (Жесткость реакции на ошибку курса)
    public float headingDamping = 0.5f;   // D-терм (Демпфирование по угловой скорости)
    public float maxAutoTurnRateDeg = 5.0f; // Лимит угловой скорости для автопилота (°/сек)
    public float maxStructuralTurnRateDeg = 15.0f; // Конструкционный лимит угловой скорости (°/сек)

    [Header("Настройки тяги винта")]
    public float propellerPitch = 0f;    // Текущее задание тяги (-1..1)
    public float speedStiffness = 0.8f;  // Насколько активно круиз меняет тягу
    public float speedDamping = 0.3f;    // Демпфирование тяги

    [Header("Текущее управление (для чтения/записи из интерфейса)")]
    [HideInInspector] public float thrustInput; // -1 полный реверс, 0 нет тяги, 1 полный ход вперед
    [HideInInspector] public float sideInput;   // -1 скольжение влево, 1 скольжение вправо
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float targetTrimMass = 1000f; // Масса для триммирования (кг)
    [HideInInspector] public float liftInput;   // -1 descent, 1 climb; maps to target vertical speed.

    // ==========================================
    // МОДУЛИ (Дочерние объекты)
    // ==========================================
    [Header("Установленные модули")]

    [HideInInspector] public float currentGasLift;
    [HideInInspector] public float activeLiftForce;

    [FormerlySerializedAs("targetMainEngineRPM")]
    [HideInInspector] public float enginePowerLever = 0.88f;
    [HideInInspector] public float engineEfficiencyCurrent;
    [HideInInspector] public float engineGeneratedPowerKw;
    [HideInInspector] public float engineFuelConsumptionKgPerSecond;
    [HideInInspector] public float engineMinimumPowerLever;
    [HideInInspector] public bool engineHasFuel = true;
    [HideInInspector] public float neutralStopBrakeAccelerationMS2;
    [HideInInspector] public float propellerInputPowerKw;
    [HideInInspector] public float propellerUsefulPowerKw;
    [HideInInspector] public float propellerCalculatedEfficiency;
    [HideInInspector] public float propellerThrustKgf;
    private bool wasAltitudeHold = false;
    private float altIntegral = 0f; // Память автопилота (I-терм)
    private bool manualGunRangeLocked;
    private float manualGunLockedRangeMeters;
    private BallisticAimSolution lastManualGunAim;
    private bool hasSmoothedManualGunTargetPoint;
    private Vector3 smoothedManualGunTargetPoint;
    private bool hasManualGunSphereAim;
    private float manualGunSphereYawDegrees;
    private float manualGunSpherePitchDegrees;
    private MetaGameState cachedMetaGameState;
    private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(16);

    private struct GunMountAimState
    {
        public bool readyToFire;
        public bool withinYawLimits;
        public bool withinElevationLimits;
        public float yawErrorDegrees;
        public float elevationErrorDegrees;
        public float desiredYawDegrees;
        public float clampedYawDegrees;
        public float desiredElevationDegrees;
        public float clampedElevationDegrees;
        public string status;
    }

    // Единая ручка управления мощностью (Обороты для CSU / Газ для Manual)
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = GetTotalMassKg();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = true;
        ConfigureYawOnlyRigidbody();
        EnforceYawOnlyRotation(true);

        rb.angularDamping = 2f;
        rb.linearDamping = 0f;
    }

    public void RefreshRuntimeShipSettings()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.mass = GetTotalMassKg();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            ConfigureYawOnlyRigidbody();
            EnforceYawOnlyRotation(true);
        }
    }

    private void ConfigureYawOnlyRigidbody()
    {
        if (rb == null) return;

        RigidbodyConstraints preservedConstraints = rb.constraints & ~RigidbodyConstraints.FreezeRotationY;
        rb.constraints = preservedConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void EnforceYawOnlyRotation(bool immediate)
    {
        if (rb == null) return;

        ConfigureYawOnlyRigidbody();

        Vector3 angularVelocity = rb.angularVelocity;
        if (Mathf.Abs(angularVelocity.x) > 0.0001f || Mathf.Abs(angularVelocity.z) > 0.0001f)
        {
            rb.angularVelocity = new Vector3(0f, angularVelocity.y, 0f);
        }

        Quaternion yawOnlyRotation = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
        if (Quaternion.Angle(rb.rotation, yawOnlyRotation) <= 0.01f) return;

        if (immediate)
        {
            rb.rotation = yawOnlyRotation;
            transform.rotation = yawOnlyRotation;
        }
        else
        {
            rb.MoveRotation(yawOnlyRotation);
        }
    }

    public float GetTotalMassKg()
    {
        return Mathf.Max(1f, baseMass + Mathf.Max(0f, cargoMassKg));
    }

    public bool TryCollectMiningFragment(string oreItemId, int amountKg, out string reason)
    {
        return TryCollectMiningFragment(oreItemId, amountKg, 0f, out reason);
    }

    public bool TryCollectMiningFragment(string oreItemId, int amountKg, float impactSpeedMS, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(oreItemId) || amountKg <= 0)
        {
            return true;
        }

        MetaGameState meta = ResolveMetaGameState();
        bool starterSortieCatch = meta != null && meta.CanCatchStarterSortieFragmentsInCargo;
        if (miningImpactHoldCapacityKg <= 0f && !starterSortieCatch)
        {
            reason = "На корабле нет противоударного кузова.";
            miningLastMessage = reason;
            return false;
        }

        if (!starterSortieCatch)
        {
            ApplyMiningImpactDamage(amountKg, impactSpeedMS);
        }

        if (!starterSortieCatch && amountKg > miningImpactHoldCapacityKg + 0.001f)
        {
            reason = $"Глыба слишком тяжёлая для противоударного кузова: {amountKg}/{miningImpactHoldCapacityKg:0} кг.";
            miningLastMessage = reason;
            return false;
        }

        if (meta == null || meta.progress == null)
        {
            reason = "Майнинг ждет MetaGameState.";
            miningLastMessage = reason;
            return false;
        }

        float freeCargoKg = meta.GetRemainingShipCargoCapacityKg();
        if (freeCargoKg + 0.001f < amountKg)
        {
            reason = $"Не хватает грузоподъемности: нужно {amountKg} кг, свободно {Mathf.FloorToInt(freeCargoKg)} кг.";
            miningLastMessage = reason;
            StopManualMovementForFullMiningHold();
            return false;
        }

        if (!meta.TryAddShipCargoFromRuntime(oreItemId, amountKg, out reason))
        {
            miningLastMessage = reason;
            return false;
        }

        miningLastMessage = "Поймано в груз " + amountKg + " кг: " + oreItemId + ".";
        return true;
    }

    private void StopManualMovementForFullMiningHold()
    {
        targetSpeedMS = 0f;
        thrustInput = 0f;
        sideInput = 0f;
        turnInput = 0f;
        neutralStopBrakeEnabled = true;
        miningLastMessage += " Mining hold is full; movement input cleared.";
    }
    private void ApplyMiningImpactDamage(int amountKg, float impactSpeedMS)
    {
        float speed = Mathf.Max(0f, impactSpeedMS);
        if (amountKg <= 0 || speed < Mathf.Max(0f, miningImpactMinDamageSpeedMS)) return;

        DamageableShip damageableShip = GetComponentInParent<DamageableShip>();
        if (damageableShip == null) return;

        float targetMass = GetTotalMassKg();
        float sourceMass = Mathf.Max(1f, amountKg);
        float reducedMass = sourceMass * targetMass / Mathf.Max(1f, sourceMass + targetMass);
        float energyKJ = 0.5f * reducedMass * speed * speed / 1000f;
        DamageHitContext context = new DamageHitContext
        {
            shellType = DamageShellType.Impact,
            shellName = "Падающая рудная глыба",
            sourceName = "OreFragmentImpact",
            impactEnergyKJ = energyKJ,
            impactDamagePerKJ = Mathf.Max(0f, miningImpactDamageScale),
            impactSpeedMS = speed,
            impactSourceMassKg = sourceMass,
            impactTargetMassKg = targetMass,
            impactSourceDamageMultiplier = Mathf.Max(0f, miningImpactDamageTakenMultiplier),
            hitPoint = transform.position,
            hitNormal = Vector3.up,
            incomingDirection = Vector3.down,
            velocity = Vector3.down * speed
        };

        damageableShip.ApplyHit(new ArmorSurface
        {
            zoneId = "mining_impact_hold",
            displayNameRu = "противоударный кузов",
            armorMm = 0f,
            ricochetAngleDeg = 90f,
            overmatchCaliberMultiplier = 3f,
            structureDamageMultiplier = 1f,
            highExplosiveSurfaceDamageMultiplier = 1f,
            ramDamageMultiplier = 1f
        }, context);
    }

    private bool TrySpendWeaponForShot(out string reason, float costMultiplier = 1f)
    {
        reason = "";
        float costKg = Mathf.Max(0f, weaponShotCostKg) * Mathf.Max(1f, costMultiplier);
        if (costKg <= 0.0001f)
        {
            return true;
        }

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            reason = "Стрельба ждет MetaGameState.";
            weaponLastMessage = reason;
            return false;
        }

        string resourceId = string.IsNullOrWhiteSpace(weaponResourceId) ? "weapon" : weaponResourceId;
        bool spent = meta.TrySpendFractionalShipCargoFromRuntime(resourceId, costKg, ref meta.progress.shipWeaponSpendBufferKg, out reason);
        if (!spent)
        {
            weaponLastMessage = reason;
        }

        return spent;
    }

    void Start()
    {
        EnsureGunGroups();
        if (autoStabilizeAtStart)
        {
            PerformAutoStabilization();
        }
        else
        {
            UpdateEngineThrottles();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        EnsureGunGroups();
        UpdateManualGunAim();
        UpdateGunInput();
        UpdateAutomaticGunGroups();
    }

    private void OnValidate()
    {
        EnsureGunGroups();
        manualGunGroupIndex = Mathf.Max(0, manualGunGroupIndex);
        weaponShotCostKg = Mathf.Max(0f, weaponShotCostKg);
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !showGunAimHud) return;
        ShipGunGroup group = GetManualGunGroup();
        if (group == null || !group.enabled) return;
        if (IsGameplayCursorReleasedForUi()) return;

        BallisticAimSolution aim = lastManualGunAim;
        if (aim.maxRangeMeters <= 0.001f && !TryBuildManualGunAim(group, manualGunRangeLocked, out aim))
        {
            return;
        }

        if (!TryReadMousePosition(out Vector3 mouse))
        {
            return;
        }

        Vector2 aimGuiPosition = ResolveAimGuiPosition(aim, mouse);
        Color ringColor = aim.valid && aim.inRange
            ? new Color(1f, 0.88f, 0.18f, 0.95f)
            : new Color(1f, 0.25f, 0.18f, 0.95f);
        DrawAimRing(aimGuiPosition, 13f, 2f, ringColor);

        Rect rect = new Rect(aimGuiPosition.x + 18f, aimGuiPosition.y + 16f, 280f, 64f);
        string lockText = aim.lockedRange ? " LOCK" : "";
        string rangeText = aim.inRange ? aim.distanceFromShipCenter.ToString("0") + " m" : "MAX " + aim.maxRangeMeters.ToString("0") + " m";
        string message = string.IsNullOrWhiteSpace(weaponLastMessage) ? aim.status : weaponLastMessage;
        GUI.Label(rect, group.displayNameRu + lockText + "\n" + aim.travelTimeSeconds.ToString("0.0") + " s / " + rangeText + "\n" + message);
    }

    public bool BuildGunAimSolutionForPoint(
        ShipGunGroup group,
        Vector3 targetPoint,
        Vector3 targetVelocity,
        bool lockedRange,
        bool hasDirectHitTarget,
        out BallisticAimSolution solution)
    {
        solution = default;
        group = group ?? GetManualGunGroup();
        if (group == null) return false;

        Vector3 center = transform.position;
        EnsureGunMountForGroup(group);
        Vector3 origin = ResolveGunMuzzlePosition(group);
        solution = BallisticFireControl.BuildSolution(
            center,
            origin,
            targetPoint,
            targetVelocity,
            Mathf.Max(1f, group.muzzleVelocityMS),
            Mathf.Max(1f, group.maxRangeMeters),
            Mathf.Max(0f, group.gravityScale),
            lockedRange,
            hasDirectHitTarget,
            group.shell != null ? group.shell.velocityRetentionAtMaxRange : 1f);
        return true;
    }

    public bool TryFireManualGunGroup(out string reason)
    {
        reason = "";
        ShipGunGroup group = GetManualGunGroup();
        if (group == null)
        {
            reason = "Нет ручной орудийной группы.";
            weaponLastMessage = reason;
            return false;
        }

        BallisticAimSolution aim = lastManualGunAim;
        if (aim.maxRangeMeters <= 0.001f && !TryBuildManualGunAim(group, manualGunRangeLocked, out aim))
        {
            reason = "Нет баллистического решения.";
            weaponLastMessage = reason;
            return false;
        }

        bool anyFired = false;
        int attemptedGroups = 0;
        int firedGroups = 0;
        string lastFailure = "";
        if (shipGunGroups != null)
        {
            for (int i = 0; i < shipGunGroups.Count; i++)
            {
                ShipGunGroup manualGroup = shipGunGroups[i];
                if (!IsEnabledManualGunGroup(manualGroup))
                {
                    continue;
                }

                attemptedGroups++;
                BallisticAimSolution groupAim = manualGroup == group && aim.maxRangeMeters > 0.001f
                    ? aim
                    : default;
                if (groupAim.maxRangeMeters <= 0.001f &&
                    !TryBuildManualGunAim(manualGroup, manualGunRangeLocked, out groupAim))
                {
                    lastFailure = "No ballistic solution.";
                    continue;
                }

                if (TryFireGunGroup(manualGroup, groupAim, out string groupReason))
                {
                    anyFired = true;
                    firedGroups++;
                }
                else
                {
                    lastFailure = groupReason;
                }
            }
        }

        if (anyFired)
        {
            reason = "Main battery salvo: " + firedGroups.ToString() + "/" + Mathf.Max(1, attemptedGroups).ToString() + " turrets fired.";
            weaponLastMessage = reason;
            return true;
        }

        reason = string.IsNullOrWhiteSpace(lastFailure) ? "No manual turret is ready to fire." : lastFailure;
        weaponLastMessage = reason;
        return false;
    }

    public bool TryGetManualGunCameraAimTarget(out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;
        ShipGunGroup group = GetManualGunGroup();
        if (group == null || !group.enabled)
        {
            return false;
        }

        BallisticAimSolution aim = lastManualGunAim;
        if ((aim.maxRangeMeters <= 0.001f || !IsFinite(aim.targetPoint)) &&
            !TryBuildManualGunAim(group, manualGunRangeLocked, out aim))
        {
            return false;
        }

        if (!IsFinite(aim.targetPoint))
        {
            return false;
        }

        targetPoint = aim.targetPoint;
        return true;
    }

    public bool TryGetManualGunCameraAnchor(out Vector3 anchor)
    {
        anchor = Vector3.zero;
        ShipGunGroup group = GetManualGunGroup();
        if (group == null || !group.enabled)
        {
            return false;
        }

        EnsureGunMountForGroup(group);
        if (group.yawPivot != null)
        {
            anchor = group.yawPivot.position;
            return IsFinite(anchor);
        }

        if (group.muzzle != null)
        {
            anchor = group.muzzle.position;
            return IsFinite(anchor);
        }

        anchor = ResolveGunMuzzlePosition(group);
        return IsFinite(anchor);
    }

    public bool SetManualGunSphereAimForTests(float yawDegrees, float pitchDegrees, out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;
        ShipGunGroup group = GetManualGunGroup();
        if (group == null || !group.enabled)
        {
            return false;
        }

        manualGunSphereYawDegrees = NormalizeManualGunDegrees(yawDegrees);
        manualGunSpherePitchDegrees = Mathf.Clamp(
            pitchDegrees,
            ManualGunSphereAimMinPitchDegrees,
            ManualGunSphereAimMaxPitchDegrees);
        hasManualGunSphereAim = true;
        ResetManualGunTargetSmoothing();

        float range = Mathf.Max(1f, group.maxRangeMeters);
        targetPoint = transform.position + ResolveManualGunSphereDirection() * range;
        bool built = BuildGunAimSolutionForPoint(group, targetPoint, Vector3.zero, false, false, out lastManualGunAim);
        if (built)
        {
            weaponAimFlightTimeSeconds = lastManualGunAim.travelTimeSeconds;
            weaponAimDistanceMeters = lastManualGunAim.distanceFromShipCenter;
            weaponAimStatus = lastManualGunAim.status;
        }

        return built;
    }

    public bool TryFireGunGroupAtPointForTests(ShipGunGroup group, Vector3 targetPoint, Vector3 targetVelocity, out string reason)
    {
        reason = "";
        group ??= GetManualGunGroup();
        if (group == null)
        {
            reason = "No gun group.";
            return false;
        }

        EnsureGunMountForGroup(group);
        if (!BuildGunAimSolutionForPoint(group, targetPoint, targetVelocity, false, true, out BallisticAimSolution aim))
        {
            reason = "No ballistic solution.";
            return false;
        }

        UpdateGunMountPose(group, aim);
        return TryFireGunGroup(group, aim, out reason);
    }

    private void UpdateManualGunAim()
    {
        ShipGunGroup group = GetManualGunGroup();
        if (group == null || !group.enabled)
        {
            weaponAimFlightTimeSeconds = 0f;
            weaponAimDistanceMeters = 0f;
            weaponAimStatus = "";
            ResetManualGunTargetSmoothing();
            return;
        }

        if (IsGameplayCursorReleasedForUi())
        {
            ResetManualGunTargetSmoothing();
            return;
        }

        if (WasMouseButtonPressedThisFrame(1) && TryBuildManualGunAim(group, false, out BallisticAimSolution initialAim))
        {
            manualGunRangeLocked = true;
            manualGunLockedRangeMeters = Mathf.Clamp(
                initialAim.distanceFromShipCenter,
                1f,
                Mathf.Max(1f, group.maxRangeMeters));
        }
        else if (!IsMouseButtonPressed(1))
        {
            manualGunRangeLocked = false;
        }

        bool primarySolved = TryBuildManualGunAim(group, manualGunRangeLocked, out lastManualGunAim, true);
        if (primarySolved)
        {
            weaponAimFlightTimeSeconds = lastManualGunAim.travelTimeSeconds;
            weaponAimDistanceMeters = lastManualGunAim.distanceFromShipCenter;
            GunMountAimState mountAim = UpdateGunMountPose(group, lastManualGunAim);
            weaponAimStatus = string.IsNullOrWhiteSpace(mountAim.status) ? lastManualGunAim.status : mountAim.status;
            UpdateOtherManualGunMounts(group);
        }
        else
        {
            ResetManualGunTargetSmoothing();
        }
    }

    private void UpdateOtherManualGunMounts(ShipGunGroup primaryGroup)
    {
        if (shipGunGroups == null)
        {
            return;
        }

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (!IsEnabledManualGunGroup(group) || group == primaryGroup)
            {
                continue;
            }

            if (TryBuildManualGunAim(group, manualGunRangeLocked, out BallisticAimSolution groupAim, false))
            {
                UpdateGunMountPose(group, groupAim);
            }
        }
    }

    private void UpdateGunInput()
    {
        if (IsGameplayCursorReleasedForUi()) return;
        if (IsPointerOverBlockingUi()) return;
        if (!WasMouseButtonPressedThisFrame(0)) return;

        TryFireManualGunGroup(out _);
    }

    private void UpdateAutomaticGunGroups()
    {
        if (shipGunGroups == null) return;

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group == null || !group.enabled || group.fireMode != ShipGunFireMode.Automatic) continue;
            if (Time.time + 0.0001f < group.nextShotTime) continue;

            if (!TryBuildAutomaticGunAim(group, out BallisticAimSolution aim)) continue;
            GunMountAimState mountAim = UpdateGunMountPose(group, aim);
            if (!mountAim.readyToFire) continue;
            TryFireGunGroup(group, aim, out _);
        }
    }

    private bool TryBuildManualGunAim(ShipGunGroup group, bool useLockedRange, out BallisticAimSolution solution, bool smoothTarget = false)
    {
        solution = default;
        Camera camera = Camera.main;
        if (camera == null)
        {
            if (smoothTarget) ResetManualGunTargetSmoothing();
            return false;
        }

        if (!TryReadMousePosition(out Vector3 mousePosition))
        {
            if (smoothTarget) ResetManualGunTargetSmoothing();
            return false;
        }

        Ray ray = camera.ScreenPointToRay(mousePosition);
        Vector3 center = transform.position;
        float maxRange = Mathf.Max(1f, group.maxRangeMeters);
        float freeAimRange = useLockedRange
            ? Mathf.Clamp(manualGunLockedRangeMeters, 1f, maxRange)
            : maxRange;
        Vector3 targetPoint;
        Vector3 targetVelocity = Vector3.zero;
        bool hasHitTarget = false;

        if (TryRaycastCursorTarget(ray, maxRange, out RaycastHit hit))
        {
            targetPoint = hit.point;
            targetVelocity = ResolveHitVelocity(hit.collider);
            hasHitTarget = true;
            if (smoothTarget)
            {
                SetManualGunSphereAimFromDirection(targetPoint - center);
            }
        }
        else
        {
            targetPoint = ResolveManualGunSphereAimPoint(ray, center, freeAimRange, smoothTarget);
        }

        if (smoothTarget)
        {
            targetPoint = SmoothManualGunTargetPoint(targetPoint);
        }

        return BuildGunAimSolutionForPoint(group, targetPoint, targetVelocity, useLockedRange, hasHitTarget, out solution);
    }

    private bool TryBuildAutomaticGunAim(ShipGunGroup group, out BallisticAimSolution solution)
    {
        solution = default;
        if (!TryFindAutomaticGunTarget(group, out Vector3 targetPoint, out Vector3 targetVelocity))
        {
            return false;
        }

        return BuildGunAimSolutionForPoint(group, targetPoint, targetVelocity, false, true, out solution);
    }

    private bool TryFireGunGroup(ShipGunGroup group, BallisticAimSolution aim, out string reason)
    {
        reason = "";
        if (group == null || !group.enabled)
        {
            reason = "Орудийная группа выключена.";
            weaponLastMessage = reason;
            return false;
        }

        if (Time.time + 0.0001f < group.nextShotTime)
        {
            reason = "Орудийная группа перезаряжается.";
            weaponLastMessage = reason;
            return false;
        }

        GunMountAimState mountAim = UpdateGunMountPose(group, aim);
        if (!mountAim.readyToFire)
        {
            reason = mountAim.status;
            weaponLastMessage = reason;
            return false;
        }

        int barrels = Mathf.Max(1, group.barrelsPerSalvo);
        if (!TrySpendWeaponForShot(out reason, barrels))
        {
            weaponLastMessage = reason;
            return false;
        }

        aim = RefreshAimOriginFromCurrentMuzzle(group, aim);
        for (int i = 0; i < barrels; i++)
        {
            BallisticAimSolution shotAim = BuildSpreadSolution(group, aim);
            SpawnGunProjectile(group, shotAim, i);
        }

        group.nextShotTime = Time.time + group.SecondsBetweenSalvos;

        reason = $"{group.displayNameRu}: залп {barrels} шт., время полета {aim.travelTimeSeconds:0.0} с, дальность {aim.distanceFromShipCenter:0} м.";
        weaponLastMessage = reason;
        return true;
    }

    private BallisticAimSolution BuildSpreadSolution(ShipGunGroup group, BallisticAimSolution aim)
    {
        Vector3 origin = ResolveGunMuzzlePosition(group);
        Vector3 forward = ResolveGunMuzzleForward(group, aim.launchDirection);
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = transform.forward;
        }

        forward.Normalize();
        Vector2 radii = BallisticFireControl.CalculateSpreadRadii(
            group.horizontalSpreadAtMaxRangeMeters,
            group.verticalSpreadAtMaxRangeMeters,
            aim.distanceFromShipCenter,
            group.maxRangeMeters);
        Vector2 random = Random.insideUnitCircle;
        Vector3 up = transform.up.sqrMagnitude > 0.001f ? transform.up.normalized : Vector3.up;
        Vector3 right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.001f)
        {
            right = transform.right;
        }

        right.Normalize();
        float directDistance = Mathf.Max(1f, Vector3.Distance(origin, aim.predictedTargetPoint));
        float yawDegrees = Mathf.Atan2(random.x * radii.x, directDistance) * Mathf.Rad2Deg;
        float pitchDegrees = Mathf.Atan2(random.y * radii.y, directDistance) * Mathf.Rad2Deg;
        Vector3 spreadDirection = Quaternion.AngleAxis(yawDegrees, up)
            * Quaternion.AngleAxis(-pitchDegrees, right)
            * forward;
        if (spreadDirection.sqrMagnitude <= 0.001f)
        {
            spreadDirection = forward;
        }

        spreadDirection.Normalize();
        aim.origin = origin;
        aim.launchDirection = spreadDirection;
        aim.launchVelocity = spreadDirection * Mathf.Max(1f, group.muzzleVelocityMS);
        aim.directDistanceFromMuzzle = Vector3.Distance(origin, aim.targetPoint);
        return aim;
    }

    private void SpawnGunProjectile(ShipGunGroup group, BallisticAimSolution aim, int barrelIndex)
    {
        Vector3 spawnPosition = ResolveGunMuzzlePosition(group);
        Vector3 launchVelocity = aim.launchVelocity.sqrMagnitude > 0.001f
            ? aim.launchVelocity
            : ResolveGunMuzzleForward(group, aim.launchDirection) * Mathf.Max(1f, group.muzzleVelocityMS);

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = $"{group.displayNameRu} Projectile";
        projectileObject.transform.position = spawnPosition;
        projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.02f, group.projectileRadiusMeters * 2f);

        Color projectileColor = group.shell != null ? group.shell.projectileColor : Color.white;
        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = projectileColor;
        }

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        Rigidbody projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.mass = Mathf.Max(0.01f, group.projectileMassKg);
        projectileBody.useGravity = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectileBody.interpolation = RigidbodyInterpolation.Interpolate;
        projectileBody.linearVelocity = launchVelocity;

        if (group.projectileTrailSeconds > 0.001f)
        {
            TrailRenderer trail = projectileObject.AddComponent<TrailRenderer>();
            trail.time = Mathf.Max(0.05f, group.projectileTrailSeconds);
            trail.startWidth = Mathf.Max(0.01f, group.projectileRadiusMeters * 2.2f);
            trail.endWidth = 0f;
            trail.autodestruct = false;
            Color trailColor = BuildGunProjectileTrailColor(projectileColor);
            Color trailEndColor = trailColor;
            trailEndColor.a = 0f;
            trail.startColor = trailColor;
            trail.endColor = trailEndColor;
            trail.numCapVertices = 3;
            trail.generateLightingData = true;
            Shader trailShader = Shader.Find("Sprites/Default");
            if (trailShader == null) trailShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (trailShader == null) trailShader = Shader.Find("Standard");
            Material trailMaterial = trailShader != null ? new Material(trailShader) : null;
            if (trailMaterial != null)
            {
                ApplyGunProjectileTrailMaterialColor(trailMaterial, trailColor);
                trail.sharedMaterial = trailMaterial;
            }
        }

        DamageProjectile projectile = projectileObject.AddComponent<DamageProjectile>();
        float lifetime = Mathf.Max(0.2f, group.maxRangeMeters / Mathf.Max(1f, group.muzzleVelocityMS) * 3f);
        projectile.Initialize(
            group.shell,
            group.displayNameRu,
            this,
            launchVelocity,
            group.maxRangeMeters,
            lifetime,
            group.gravityScale);
    }

    private static Color BuildGunProjectileTrailColor(Color projectileColor)
    {
        if (!IsFinite(projectileColor.r) ||
            !IsFinite(projectileColor.g) ||
            !IsFinite(projectileColor.b))
        {
            projectileColor = Color.white;
        }

        if (projectileColor.maxColorComponent <= 0.05f)
        {
            projectileColor = Color.white;
        }

        Color trailColor = projectileColor * GunProjectileTrailBrightnessMultiplier;
        trailColor.a = 1f;
        return trailColor;
    }

    private static void ApplyGunProjectileTrailMaterialColor(Material material, Color trailColor)
    {
        if (material == null)
        {
            return;
        }

        material.color = trailColor;
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", trailColor);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", trailColor);
        }

        Color emissionColor = trailColor * GunProjectileTrailEmissionMultiplier;
        emissionColor.a = 1f;
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
        }
    }

    private bool TryFindAutomaticGunTarget(ShipGunGroup group, out Vector3 targetPoint, out Vector3 targetVelocity)
    {
        targetPoint = Vector3.zero;
        targetVelocity = Vector3.zero;
        float maxSqr = Mathf.Max(1f, group.maxRangeMeters) * Mathf.Max(1f, group.maxRangeMeters);
        float bestSqr = maxSqr;
        bool found = false;

        if (group.automaticTargetsDamageableShips)
        {
            DamageableShip[] ships = FindObjectsByType<DamageableShip>(FindObjectsSortMode.None);
            for (int i = 0; i < ships.Length; i++)
            {
                DamageableShip targetShip = ships[i];
                if (targetShip == null || targetShip.GetComponentInParent<ShipPhysics>() == this) continue;

                Vector3 point = targetShip.GetAimPoint();
                float sqr = (point - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                targetPoint = point;
                Rigidbody targetBody = targetShip.GetComponentInParent<Rigidbody>();
                targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
                found = true;
            }
        }

        return found;
    }

    private bool TryRaycastCursorTarget(Ray ray, float maxRangeMeters, out RaycastHit bestHit)
    {
        bestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(ray, 20000f, ~0, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0) return false;

        Vector3 center = transform.position;
        float maxRange = Mathf.Max(1f, maxRangeMeters);
        float maxRangeSqr = maxRange * maxRange;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;
            if (collider.GetComponentInParent<DamageProjectile>() != null) continue;
            if (collider.transform.IsChildOf(transform)) continue;
            if (!IsManualGunRaycastTarget(collider)) continue;
            if ((hits[i].point - center).sqrMagnitude > maxRangeSqr + 0.001f) continue;

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestHit = hits[i];
            }
        }

        return bestDistance < float.PositiveInfinity;
    }

    private static bool IsManualGunRaycastTarget(Collider collider)
    {
        if (collider == null) return false;
        if (collider.GetComponentInParent<DamageableShip>() != null) return true;

        return false;
    }

    private Vector3 ResolveManualGunSphereAimPoint(Ray ray, Vector3 center, float rangeMeters, bool updateFromInput)
    {
        float range = Mathf.Max(1f, rangeMeters);
        if (!hasManualGunSphereAim)
        {
            InitializeManualGunSphereAim(ray, center, range);
        }

        if (WildWindSessionCameraController.GameplayCursorLockedForMouseLook &&
            !WildWindControlSettings.IsGameplayCursorReleasePressed())
        {
            if (updateFromInput && TryReadManualGunAimDelta(out Vector2 aimDelta))
            {
                float sensitivityScale = Mathf.Clamp(
                    WildWindSessionCameraController.GameplayMouseLookSensitivityScale,
                    0.001f,
                    1f);
                manualGunSphereYawDegrees = NormalizeManualGunDegrees(
                    manualGunSphereYawDegrees + aimDelta.x * ManualGunSphereAimSensitivityDegreesPerPixel * sensitivityScale);
                manualGunSpherePitchDegrees = Mathf.Clamp(
                    manualGunSpherePitchDegrees + aimDelta.y * ManualGunSphereAimSensitivityDegreesPerPixel * sensitivityScale,
                    ManualGunSphereAimMinPitchDegrees,
                    ManualGunSphereAimMaxPitchDegrees);
            }

            return center + ResolveManualGunSphereDirection() * range;
        }

        Vector3 point = ResolveCursorRayRangePoint(ray, center, range);
        if (updateFromInput)
        {
            SetManualGunSphereAimFromDirection(point - center);
        }

        return point;
    }

    private void InitializeManualGunSphereAim(Ray ray, Vector3 center, float rangeMeters)
    {
        Vector3 point = ResolveCursorRayRangePoint(ray, center, rangeMeters);
        SetManualGunSphereAimFromDirection(point - center);
    }

    private void SetManualGunSphereAimFromDirection(Vector3 direction)
    {
        if (!IsFinite(direction) || direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;
        }

        direction.Normalize();
        manualGunSphereYawDegrees = NormalizeManualGunDegrees(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
        manualGunSpherePitchDegrees = Mathf.Clamp(
            Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg,
            ManualGunSphereAimMinPitchDegrees,
            ManualGunSphereAimMaxPitchDegrees);
        hasManualGunSphereAim = true;
    }

    private Vector3 ResolveManualGunSphereDirection()
    {
        float yawRadians = manualGunSphereYawDegrees * Mathf.Deg2Rad;
        float pitchRadians = manualGunSpherePitchDegrees * Mathf.Deg2Rad;
        float horizontal = Mathf.Cos(pitchRadians);
        Vector3 direction = new Vector3(
            Mathf.Sin(yawRadians) * horizontal,
            Mathf.Sin(pitchRadians),
            Mathf.Cos(yawRadians) * horizontal);
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }

    private static bool TryReadManualGunAimDelta(out Vector2 delta)
    {
        delta = Vector2.zero;
        if (!WildWindSessionCameraController.GameplayCursorLockedForMouseLook ||
            WildWindControlSettings.IsGameplayCursorReleasePressed())
        {
            return false;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        delta = mouse.delta.ReadValue();
        return delta.sqrMagnitude > 0.0001f;
    }

    private static float NormalizeManualGunDegrees(float value)
    {
        value %= 360f;
        if (value < 0f)
        {
            value += 360f;
        }

        return value;
    }

    private Vector3 SmoothManualGunTargetPoint(Vector3 targetPoint)
    {
        if (!Application.isPlaying || !IsFinite(targetPoint))
        {
            return targetPoint;
        }

        if (!hasSmoothedManualGunTargetPoint || !IsFinite(smoothedManualGunTargetPoint))
        {
            hasSmoothedManualGunTargetPoint = true;
            smoothedManualGunTargetPoint = targetPoint;
            return targetPoint;
        }

        float smooth = Mathf.Max(0.001f, ManualGunAimTargetSmoothSeconds);
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, Time.deltaTime) / smooth);
        smoothedManualGunTargetPoint = Vector3.Lerp(smoothedManualGunTargetPoint, targetPoint, Mathf.Clamp01(t));
        return smoothedManualGunTargetPoint;
    }

    private void ResetManualGunTargetSmoothing()
    {
        hasSmoothedManualGunTargetPoint = false;
        smoothedManualGunTargetPoint = Vector3.zero;
    }

    private Vector3 ResolveHitVelocity(Collider collider)
    {
        if (collider == null) return Vector3.zero;
        Rigidbody body = collider.attachedRigidbody;
        if (body == null)
        {
            body = collider.GetComponentInParent<Rigidbody>();
        }

        return body != null ? body.linearVelocity : Vector3.zero;
    }

    private Vector3 ResolveGunMuzzlePosition(ShipGunGroup group)
    {
        Transform muzzle = EnsureGunMountForGroup(group);
        if (muzzle != null)
        {
            return muzzle.position;
        }

        Vector3 offset = group != null ? group.localMuzzleOffset : Vector3.zero;
        return transform.TransformPoint(offset);
    }

    private Vector3 ResolveGunMuzzleForward(ShipGunGroup group, Vector3 fallbackDirection)
    {
        Transform muzzle = EnsureGunMountForGroup(group);
        if (muzzle != null && muzzle.forward.sqrMagnitude > 0.001f)
        {
            return muzzle.forward.normalized;
        }

        if (group != null && group.pitchPivot != null && group.pitchPivot.forward.sqrMagnitude > 0.001f)
        {
            return group.pitchPivot.forward.normalized;
        }

        if (fallbackDirection.sqrMagnitude > 0.001f)
        {
            return fallbackDirection.normalized;
        }

        return transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;
    }

    private BallisticAimSolution RefreshAimOriginFromCurrentMuzzle(ShipGunGroup group, BallisticAimSolution aim)
    {
        Vector3 origin = ResolveGunMuzzlePosition(group);
        if ((origin - aim.origin).sqrMagnitude <= 0.000001f)
        {
            return aim;
        }

        return BallisticFireControl.BuildSolution(
            aim.shipCenter,
            origin,
            aim.targetPoint,
            aim.targetVelocity,
            Mathf.Max(1f, group.muzzleVelocityMS),
            Mathf.Max(1f, group.maxRangeMeters),
            Mathf.Max(0f, group.gravityScale),
            aim.lockedRange,
            aim.hasDirectHitTarget,
            group.shell != null ? group.shell.velocityRetentionAtMaxRange : 1f);
    }

    private ShipGunGroup GetManualGunGroup()
    {
        EnsureGunGroups();
        if (shipGunGroups == null || shipGunGroups.Count == 0) return null;

        int index = Mathf.Clamp(manualGunGroupIndex, 0, shipGunGroups.Count - 1);
        ShipGunGroup indexed = shipGunGroups[index];
        if (indexed != null && indexed.fireMode == ShipGunFireMode.Manual) return indexed;

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group != null && group.fireMode == ShipGunFireMode.Manual)
            {
                manualGunGroupIndex = i;
                return group;
            }
        }

        return null;
    }

    private static bool IsEnabledManualGunGroup(ShipGunGroup group)
    {
        return group != null && group.enabled && group.fireMode == ShipGunFireMode.Manual;
    }

    private Transform EnsureGunMountForGroup(ShipGunGroup group)
    {
        if (group == null) return null;
        if (ShouldRebindGunMount(group))
        {
            TryBindExistingGunMount(group);
        }

        return group.muzzle;
    }

    private bool ShouldRebindGunMount(ShipGunGroup group)
    {
        if (group == null) return false;
        if (group.muzzle == null || group.yawPivot == null || group.pitchPivot == null) return true;
        if (!group.muzzle.IsChildOf(transform)) return true;
        if (!IsNamedPitchPivot(group.pitchPivot)) return true;
        if (!group.muzzle.IsChildOf(group.pitchPivot)) return true;
        return !IsNamedMuzzle(group.muzzle);
    }

    private static bool IsNamedMuzzle(Transform candidate)
    {
        if (candidate == null) return false;
        string name = candidate.name;
        return string.Equals(name, "Muzzle", System.StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("GunMuzzle", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNamedPitchPivot(Transform candidate)
    {
        if (candidate == null) return false;
        string name = candidate.name;
        return string.Equals(name, "Barrel", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "BarrelPitch", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "\u0421\u0442\u0432\u043e\u043b", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "РЎС‚РІРѕР»", System.StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("GunPitch", System.StringComparison.OrdinalIgnoreCase);
    }

    private bool TryBindExistingGunMount(ShipGunGroup group)
    {
        string suffix = SanitizeName(group.groupId);
        Transform yaw = FindDeepChild(transform, "GunYaw_" + suffix)
            ?? FindDeepChild(transform, "GunYaw")
            ?? FindDeepChild(transform, "TurretYaw")
            ?? FindDeepChild(transform, "Turret")
            ?? FindDeepChild(transform, "\u0422\u0443\u0440\u0435\u043b\u044c")
            ?? FindDeepChild(transform, "Турель");
        Transform pitch = FindDeepChild(transform, "GunPitch_" + suffix)
            ?? FindDeepChild(transform, "GunPitch")
            ?? FindDeepChild(transform, "BarrelPitch")
            ?? FindDeepChild(transform, "Barrel")
            ?? FindDeepChild(transform, "\u0421\u0442\u0432\u043e\u043b")
            ?? FindDeepChild(transform, "Ствол");
        Transform muzzle = pitch != null
            ? FindDeepChild(pitch, "GunMuzzle_" + suffix)
                ?? FindDeepChild(pitch, "GunMuzzle")
                ?? FindDeepChild(pitch, "Muzzle")
            : null;
        muzzle ??= FindDeepChild(transform, "GunMuzzle_" + suffix)
            ?? FindDeepChild(transform, "GunMuzzle")
            ?? FindDeepChild(transform, "Muzzle");

        if (muzzle == null)
        {
            muzzle = pitch;
        }

        if (muzzle == null)
        {
            return false;
        }

        group.yawPivot = yaw != null ? yaw : muzzle;
        group.pitchPivot = pitch != null ? pitch : muzzle;
        group.muzzle = muzzle;
        return true;
    }

    private GunMountAimState UpdateGunMountPose(ShipGunGroup group, BallisticAimSolution aim)
    {
        GunMountAimState state = new GunMountAimState
        {
            readyToFire = false,
            withinYawLimits = true,
            withinElevationLimits = true,
            status = "Орудие ждёт решения наведения."
        };

        Transform muzzleTransform = EnsureGunMountForGroup(group);
        if (aim.launchDirection.sqrMagnitude <= 0.0001f)
        {
            state.status = "Орудие ждёт баллистическое решение.";
            return state;
        }

        Vector3 direction = aim.launchDirection.normalized;
        Vector3 up = transform.up.sqrMagnitude > 0.0001f ? transform.up.normalized : Vector3.up;
        Vector3 localDirection = transform.InverseTransformDirection(direction);
        float desiredYawDegrees = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float clampedYawDegrees = ClampGunYawDegrees(group, desiredYawDegrees, out bool withinYawLimits);
        float desiredElevationDegrees = Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        float minElevation = group != null ? group.minElevationDegrees : -89f;
        float maxElevation = group != null ? group.maxElevationDegrees : 89f;
        if (maxElevation < minElevation)
        {
            float min = maxElevation;
            maxElevation = minElevation;
            minElevation = min;
        }

        float clampedElevationDegrees = Mathf.Clamp(desiredElevationDegrees, minElevation, maxElevation);
        state.desiredYawDegrees = desiredYawDegrees;
        state.clampedYawDegrees = clampedYawDegrees;
        state.withinYawLimits = withinYawLimits;
        state.desiredElevationDegrees = desiredElevationDegrees;
        state.clampedElevationDegrees = clampedElevationDegrees;
        state.withinElevationLimits = Mathf.Abs(Mathf.DeltaAngle(desiredElevationDegrees, clampedElevationDegrees)) <= 0.05f;

        if (muzzleTransform == null && (group == null || group.yawPivot == null || group.pitchPivot == null))
        {
            state.readyToFire = state.withinYawLimits && state.withinElevationLimits;
            state.yawErrorDegrees = 0f;
            state.elevationErrorDegrees = 0f;
            state.status = !state.withinYawLimits
                ? "Target outside turret sector: yaw "
                  + desiredYawDegrees.ToString("0.#")
                  + " deg, limit "
                  + (group != null ? group.minYawDegrees : -180f).ToString("0.#")
                  + ".."
                  + (group != null ? group.maxYawDegrees : 180f).ToString("0.#")
                  + " deg."
                : state.withinElevationLimits
                    ? "Орудие без визуального ствола стреляет из offset."
                    : "Цель вне углов ствола: нужно "
                  + desiredElevationDegrees.ToString("0.#")
                  + "°, предел "
                  + minElevation.ToString("0.#")
                  + ".."
                  + maxElevation.ToString("0.#")
                  + "°.";
            return state;
        }

        float yawRadians = clampedYawDegrees * Mathf.Deg2Rad;
        Vector3 yawLocalDirection = new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
        Vector3 yawWorldDirection = transform.TransformDirection(yawLocalDirection);
        if (yawWorldDirection.sqrMagnitude <= 0.0001f)
        {
            yawWorldDirection = Vector3.ProjectOnPlane(transform.forward, up);
        }

        float elevationRadians = clampedElevationDegrees * Mathf.Deg2Rad;
        Vector3 pitchLocalDirection = new Vector3(0f, Mathf.Sin(elevationRadians), Mathf.Cos(elevationRadians)).normalized;
        Vector3 clampedWorldDirection = transform.TransformDirection(new Vector3(
            Mathf.Sin(yawRadians) * Mathf.Cos(elevationRadians),
            Mathf.Sin(elevationRadians),
            Mathf.Cos(yawRadians) * Mathf.Cos(elevationRadians)).normalized);

        float deltaSeconds = Application.isPlaying && Time.deltaTime > 0.0001f ? Time.deltaTime : 1f / 60f;
        float yawStep = GetGunRotationStep(group != null ? group.yawSpeedDegPerSecond : 0f, deltaSeconds);
        if (group != null && group.yawPivot != null && group.pitchPivot != null)
        {
            if (yawWorldDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(yawWorldDirection.normalized, up);
                group.yawPivot.rotation = Quaternion.RotateTowards(group.yawPivot.rotation, targetYaw, yawStep);
                state.yawErrorDegrees = Quaternion.Angle(group.yawPivot.rotation, targetYaw);
            }

            float currentElevation = CalculateWorldElevationDegrees(group.pitchPivot.forward);
            float elevationSpeed = clampedElevationDegrees >= currentElevation
                ? (group.elevationUpSpeedDegPerSecond)
                : (group.elevationDownSpeedDegPerSecond);
            float elevationStep = GetGunRotationStep(elevationSpeed, deltaSeconds);
            Quaternion targetPitchLocal = Quaternion.LookRotation(pitchLocalDirection, Vector3.up);
            if (group.pitchPivot.parent == group.yawPivot)
            {
                group.pitchPivot.localRotation = Quaternion.RotateTowards(group.pitchPivot.localRotation, targetPitchLocal, elevationStep);
                state.elevationErrorDegrees = Quaternion.Angle(group.pitchPivot.localRotation, targetPitchLocal);
            }
            else
            {
                Quaternion targetPitchWorld = Quaternion.LookRotation(clampedWorldDirection.normalized, up);
                group.pitchPivot.rotation = Quaternion.RotateTowards(group.pitchPivot.rotation, targetPitchWorld, elevationStep);
                state.elevationErrorDegrees = Quaternion.Angle(group.pitchPivot.rotation, targetPitchWorld);
            }
        }
        else
        {
            Quaternion targetMuzzleRotation = Quaternion.LookRotation(clampedWorldDirection.normalized, up);
            muzzleTransform.rotation = Quaternion.RotateTowards(muzzleTransform.rotation, targetMuzzleRotation, yawStep);
            state.yawErrorDegrees = Quaternion.Angle(muzzleTransform.rotation, targetMuzzleRotation);
            state.elevationErrorDegrees = 0f;
        }

        float tolerance = group != null ? Mathf.Max(0.05f, group.fireAlignmentToleranceDegrees) : 1.5f;
        state.readyToFire = state.withinYawLimits
            && state.withinElevationLimits
            && state.yawErrorDegrees <= tolerance
            && state.elevationErrorDegrees <= tolerance;
        if (!state.withinYawLimits)
        {
            state.status = "Target outside turret sector: yaw "
                + desiredYawDegrees.ToString("0.#")
                + " deg, limit "
                + (group != null ? group.minYawDegrees : -180f).ToString("0.#")
                + ".."
                + (group != null ? group.maxYawDegrees : 180f).ToString("0.#")
                + " deg.";
        }
        else if (!state.withinElevationLimits)
        {
            state.status = "Цель вне углов ствола: нужно "
                + desiredElevationDegrees.ToString("0.#")
                + "°, предел "
                + minElevation.ToString("0.#")
                + ".."
                + maxElevation.ToString("0.#")
                + "°.";
        }
        else if (!state.readyToFire)
        {
            state.status = "Орудие доворачивается: yaw "
                + state.yawErrorDegrees.ToString("0.#")
                + "°, elev "
                + state.elevationErrorDegrees.ToString("0.#")
                + "°.";
        }
        else
        {
            state.status = "Орудие наведено.";
        }

        return state;
    }

    private float CalculateWorldElevationDegrees(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f) return 0f;

        Vector3 localDirection = transform.InverseTransformDirection(direction.normalized);
        return Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
    }

    private static float ClampGunYawDegrees(ShipGunGroup group, float desiredYawDegrees, out bool withinLimits)
    {
        float yaw = Mathf.DeltaAngle(0f, desiredYawDegrees);
        if (group == null || !group.useYawLimits)
        {
            withinLimits = true;
            return yaw;
        }

        float minYaw = Mathf.Clamp(group.minYawDegrees, -180f, 180f);
        float maxYaw = Mathf.Clamp(group.maxYawDegrees, -180f, 180f);
        if (maxYaw < minYaw)
        {
            float min = maxYaw;
            maxYaw = minYaw;
            minYaw = min;
        }

        float clampedYaw = Mathf.Clamp(yaw, minYaw, maxYaw);
        withinLimits = Mathf.Abs(Mathf.DeltaAngle(yaw, clampedYaw)) <= 0.05f;
        return clampedYaw;
    }

    private static float GetGunRotationStep(float speedDegPerSecond, float deltaSeconds)
    {
        return speedDegPerSecond <= 0.001f
            ? 36000f
            : Mathf.Max(0f, speedDegPerSecond) * Mathf.Max(0f, deltaSeconds);
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName)) return null;
        if (root.name == childName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "main";
        return raw.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    private void EnsureGunGroups()
    {
        if (shipGunGroups == null)
        {
            shipGunGroups = new List<ShipGunGroup>();
        }

        bool createdDefaultGunGroups = shipGunGroups.Count == 0;
        if (createdDefaultGunGroups)
        {
            shipGunGroups.Add(CreateDefaultMainGunGroup());
            shipGunGroups.Add(CreateDefaultSecondaryGunGroup());
        }

        bool hasSecondaryGroup = false;
        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group == null)
            {
                group = new ShipGunGroup { groupId = "group_" + (i + 1).ToString("00") };
                shipGunGroups[i] = group;
            }

            if (string.IsNullOrWhiteSpace(group.groupId))
            {
                group.groupId = "group_" + (i + 1).ToString("00");
            }

            if (string.IsNullOrWhiteSpace(group.displayNameRu))
            {
                group.displayNameRu = group.groupId;
            }

            group.barrelsPerSalvo = Mathf.Max(1, group.barrelsPerSalvo);
            group.roundsPerMinute = Mathf.Max(0f, group.roundsPerMinute);
            group.yawSpeedDegPerSecond = Mathf.Max(0f, group.yawSpeedDegPerSecond);
            if (group.maxYawDegrees < group.minYawDegrees)
            {
                float min = group.maxYawDegrees;
                group.maxYawDegrees = group.minYawDegrees;
                group.minYawDegrees = min;
            }

            group.elevationUpSpeedDegPerSecond = Mathf.Max(0f, group.elevationUpSpeedDegPerSecond);
            group.elevationDownSpeedDegPerSecond = Mathf.Max(0f, group.elevationDownSpeedDegPerSecond);
            if (group.maxElevationDegrees < group.minElevationDegrees)
            {
                float min = group.maxElevationDegrees;
                group.maxElevationDegrees = group.minElevationDegrees;
                group.minElevationDegrees = min;
            }

            group.fireAlignmentToleranceDegrees = Mathf.Max(0.05f, group.fireAlignmentToleranceDegrees);
            group.maxRangeMeters = Mathf.Max(1f, group.maxRangeMeters);
            group.muzzleVelocityMS = Mathf.Max(1f, group.muzzleVelocityMS);
            group.projectileMassKg = Mathf.Max(0.01f, group.projectileMassKg);
            group.projectileRadiusMeters = Mathf.Max(0.01f, group.projectileRadiusMeters);
            group.horizontalSpreadAtMaxRangeMeters = Mathf.Max(0f, group.horizontalSpreadAtMaxRangeMeters);
            group.verticalSpreadAtMaxRangeMeters = Mathf.Max(0f, group.verticalSpreadAtMaxRangeMeters);
            group.gravityScale = Mathf.Max(0f, group.gravityScale);
            group.projectileTrailSeconds = Mathf.Max(0f, group.projectileTrailSeconds);
            if (group.shell == null)
            {
                group.shell = new DamageShellPreset();
            }

            if (group.fireMode == ShipGunFireMode.Automatic && string.Equals(group.groupId, "secondary", System.StringComparison.OrdinalIgnoreCase))
            {
                hasSecondaryGroup = true;
            }
        }

        int enabledManualIndex = FindManualGunGroupIndex(true);
        if (enabledManualIndex < 0)
        {
            int disabledManualIndex = FindManualGunGroupIndex(false);
            if (disabledManualIndex >= 0)
            {
                shipGunGroups[disabledManualIndex].enabled = true;
                enabledManualIndex = disabledManualIndex;
            }
            else
            {
                shipGunGroups.Insert(0, CreateDefaultMainGunGroup());
                enabledManualIndex = 0;
            }
        }

        manualGunGroupIndex = Mathf.Clamp(enabledManualIndex, 0, Mathf.Max(0, shipGunGroups.Count - 1));
        ShipGunGroup manualGroup = shipGunGroups[manualGunGroupIndex];
        if (manualGroup != null)
        {
            if (string.Equals(manualGroup.groupId, "main", System.StringComparison.OrdinalIgnoreCase))
            {
                manualGroup.roundsPerMinute = MainGunRoundsPerMinute;
            }

            if (Application.isPlaying)
            {
                float latestAllowedShotTime = Time.time + manualGroup.SecondsBetweenSalvos;
                if (manualGroup.nextShotTime > latestAllowedShotTime)
                {
                    manualGroup.nextShotTime = latestAllowedShotTime;
                }
            }
        }

        if (!createdDefaultGunGroups &&
            shipGunGroups.Count == 1 &&
            HasGunGroupId("main") &&
            !hasSecondaryGroup &&
            !HasGunGroupId("secondary"))
        {
            shipGunGroups.Add(CreateDefaultSecondaryGunGroup());
        }
    }

    private int FindManualGunGroupIndex(bool requireEnabled)
    {
        if (shipGunGroups == null) return -1;

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group == null || group.fireMode != ShipGunFireMode.Manual) continue;
            if (requireEnabled && !group.enabled) continue;
            return i;
        }

        return -1;
    }

    private bool HasGunGroupId(string groupId)
    {
        if (shipGunGroups == null || string.IsNullOrWhiteSpace(groupId)) return false;

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group != null && string.Equals(group.groupId, groupId, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerOverBlockingUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !TryReadMousePosition(out Vector3 mousePosition))
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = mousePosition
        };
        uiRaycastResults.Clear();
        eventSystem.RaycastAll(eventData, uiRaycastResults);
        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject target = uiRaycastResults[i].gameObject;
            if (target == null) continue;
            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null) return true;
            if (ExecuteEvents.GetEventHandler<IDragHandler>(target) != null) return true;
            if (ExecuteEvents.GetEventHandler<IScrollHandler>(target) != null) return true;
            if (ExecuteEvents.GetEventHandler<ISubmitHandler>(target) != null) return true;
        }

        return false;
    }

    private static bool TryReadMousePosition(out Vector3 mousePosition)
    {
        if (WildWindSessionCameraController.GameplayCursorLockedForMouseLook &&
            !WildWindControlSettings.IsGameplayCursorReleasePressed())
        {
            mousePosition = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            return Screen.width > 0 && Screen.height > 0;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            mousePosition = Vector3.zero;
            return false;
        }

        Vector2 position = mouse.position.ReadValue();
        mousePosition = new Vector3(position.x, position.y, 0f);
        return true;
    }

    private static bool WasMouseButtonPressedThisFrame(int button)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        return button switch
        {
            0 => mouse.leftButton.wasPressedThisFrame,
            1 => mouse.rightButton.wasPressedThisFrame,
            2 => mouse.middleButton.wasPressedThisFrame,
            _ => false
        };
    }

    private static bool IsMouseButtonPressed(int button)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        return button switch
        {
            0 => mouse.leftButton.isPressed,
            1 => mouse.rightButton.isPressed,
            2 => mouse.middleButton.isPressed,
            _ => false
        };
    }

    private static bool IsGameplayCursorReleasedForUi()
    {
        return WildWindSessionCameraController.GameplayCursorReleasedForUi ||
            WildWindControlSettings.IsGameplayCursorReleasePressed();
    }

    private static Vector3 ResolveCursorRayRangePoint(Ray ray, Vector3 center, float rangeMeters)
    {
        float range = Mathf.Max(1f, rangeMeters);
        Vector3 origin = IsFinite(ray.origin) ? ray.origin : center;
        Vector3 direction = ray.direction.sqrMagnitude > 0.001f ? ray.direction.normalized : Vector3.forward;
        Vector3 originToCenter = origin - center;
        float projectedCenter = Vector3.Dot(originToCenter, direction);
        float outsideRange = originToCenter.sqrMagnitude - range * range;
        float discriminant = projectedCenter * projectedCenter - outsideRange;
        if (discriminant >= 0f)
        {
            float root = Mathf.Sqrt(discriminant);
            float farT = -projectedCenter + root;
            if (farT > 0.001f)
            {
                return origin + direction * farT;
            }

            float nearT = -projectedCenter - root;
            if (nearT > 0.001f)
            {
                return origin + direction * nearT;
            }
        }

        float closestT = Mathf.Max(0f, -projectedCenter);
        Vector3 closestPoint = origin + direction * closestT;
        Vector3 fromCenter = closestPoint - center;
        if (!IsFinite(fromCenter) || fromCenter.sqrMagnitude <= 0.001f)
        {
            fromCenter = direction.sqrMagnitude > 0.001f ? direction : Vector3.forward;
        }

        return center + fromCenter.normalized * range;
    }

    private static Vector2 ResolveAimGuiPosition(BallisticAimSolution aim, Vector3 fallbackMousePosition)
    {
        if (WildWindSessionCameraController.GameplayCursorLockedForMouseLook &&
            !WildWindControlSettings.IsGameplayCursorReleasePressed())
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        Camera camera = Camera.main;
        if (camera != null && IsFinite(aim.targetPoint))
        {
            Vector3 screen = camera.WorldToScreenPoint(aim.targetPoint);
            if (screen.z > 0.001f && IsFinite(screen))
            {
                return new Vector2(screen.x, Screen.height - screen.y);
            }
        }

        if (!IsFinite(fallbackMousePosition))
        {
            fallbackMousePosition = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        float screenWidth = Mathf.Max(0f, Screen.width);
        float screenHeight = Mathf.Max(0f, Screen.height);
        float x = Mathf.Clamp(fallbackMousePosition.x, 0f, screenWidth);
        float y = Mathf.Clamp(screenHeight - fallbackMousePosition.y, 0f, screenHeight);
        return new Vector2(x, y);
    }

    private static void DrawAimRing(Vector2 center, float radius, float thickness, Color color)
    {
        if (radius <= 0.001f || thickness <= 0.001f)
        {
            return;
        }

        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.color = color;

        const int SegmentCount = 48;
        Vector2 previous = center + new Vector2(radius, 0f);
        for (int i = 1; i <= SegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / SegmentCount;
            Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            DrawGuiLine(previous, next, thickness);
            previous = next;
        }

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    private static void DrawGuiLine(Vector2 start, Vector2 end, float thickness)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.001f)
        {
            return;
        }

        Matrix4x4 previousMatrix = GUI.matrix;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
        GUI.matrix = previousMatrix;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static ShipGunGroup CreateDefaultMainGunGroup()
    {
        return new ShipGunGroup
        {
            groupId = "main",
            displayNameRu = "Главный калибр",
            fireMode = ShipGunFireMode.Manual,
            localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
            barrelsPerSalvo = 1,
            roundsPerMinute = MainGunRoundsPerMinute,
            yawSpeedDegPerSecond = 45f,
            elevationUpSpeedDegPerSecond = 22f,
            elevationDownSpeedDegPerSecond = 30f,
            minElevationDegrees = -5f,
            maxElevationDegrees = 55f,
            fireAlignmentToleranceDegrees = 1.5f,
            maxRangeMeters = 2500f,
            muzzleVelocityMS = 360f,
            projectileMassKg = 12f,
            projectileRadiusMeters = 0.10f,
            horizontalSpreadAtMaxRangeMeters = 24f,
            verticalSpreadAtMaxRangeMeters = 9f,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 76 мм",
                shellType = DamageShellType.ArmorPiercing,
                caliberMm = 76f,
                damagePoints = 120f,
                hullDamageOnPenetration = 120f,
                penetrationMm = 85f,
                penetrationAtMaxRangeMultiplier = 0.55f,
                velocityRetentionAtMaxRange = 0.62f,
                normalizationDegrees = 5f,
                penetrationRollSpread = 0.08f,
                projectileColor = Color.red
            }
        };
    }

    private static ShipGunGroup CreateDefaultSecondaryGunGroup()
    {
        return new ShipGunGroup
        {
            groupId = "secondary",
            displayNameRu = "ПМК",
            fireMode = ShipGunFireMode.Automatic,
            localMuzzleOffset = new Vector3(0f, 1.2f, -7.5f),
            barrelsPerSalvo = 1,
            roundsPerMinute = 200f,
            yawSpeedDegPerSecond = 140f,
            elevationUpSpeedDegPerSecond = 90f,
            elevationDownSpeedDegPerSecond = 110f,
            minElevationDegrees = -10f,
            maxElevationDegrees = 70f,
            fireAlignmentToleranceDegrees = 3f,
            maxRangeMeters = 1500f,
            muzzleVelocityMS = 420f,
            projectileMassKg = 0.12f,
            projectileRadiusMeters = 0.035f,
            horizontalSpreadAtMaxRangeMeters = 35f,
            verticalSpreadAtMaxRangeMeters = 16f,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 20 мм",
                shellType = DamageShellType.ArmorPiercing,
                caliberMm = 20f,
                damagePoints = 18f,
                hullDamageOnPenetration = 18f,
                penetrationMm = 18f,
                penetrationAtMaxRangeMultiplier = 0.45f,
                velocityRetentionAtMaxRange = 0.58f,
                normalizationDegrees = 2f,
                penetrationRollSpread = 0.12f,
                projectileColor = new Color(0.2f, 0.85f, 1f, 1f)
            }
        };
    }

    private void PerformAutoStabilization()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        targetTrimMass = rb != null ? rb.mass : baseMass;
        if (rb != null)
        {
            targetAltitude = rb.position.y;
            PrimeClaudiumLiftForHover(rb.mass);
        }

        currentGasLift = 0f;
        activeLiftForce = claudiumCurrentLiftN;
        UpdateEngineThrottles();
    }

    public void StabilizeForFlightStart(bool holdCurrentAltitude)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        altitudeHold = false;
        if (rb != null)
        {
            targetAltitude = rb.position.y;
        }

        thrustInput = 0f;
        propellerPitch = 0f;
        neutralStopBrakeEnabled = false;
        neutralStopBrakeAccelerationMS2 = 0f;
        sideInput = 0f;
        turnInput = 0f;
        liftInput = 0f;
        PerformAutoStabilization();
    }

    void FixedUpdate()
    {
        EnforceYawOnlyRotation(false);
        UpdateCruiseControl();      // Круиз-контроль (скорость)
        UpdateEngineThrottles();
        UpdateHeadingAutopilot(); // Автопилот курса
        UpdateClaudiumSlipstream(Time.fixedDeltaTime);
        UpdateClaudium(); // Магия Клавдия

        // --- АЭРОДИНАМИКА (с учетом ветра) ---
        Vector3 effectiveWindVelocity = GetEffectiveWindVelocity();
        Vector3 airVelocity = rb.linearVelocity - effectiveWindVelocity;
        float airspeed = airVelocity.magnitude;

        float aeroMultiplier = 1.0f;
        float currentDrag = CurrentAeroDrag * aeroMultiplier;

        if (airspeed > 0.01f)
        {
            Vector3 dragForce = -airVelocity.normalized * (airspeed * airspeed) * currentDrag;
            rb.AddForce(dragForce, ForceMode.Force);
        }

        // --- ПОДЪЕМНАЯ СИЛА ---
        ApplyNeutralStopBrake();

        float totalLift = claudiumCurrentLiftN;
        activeLiftForce = totalLift;
        currentGasLift = 0f;
        rb.AddForce(Vector3.up * totalLift, ForceMode.Force);

        ApplyPropellerThrust();
        ApplyLateralOmniThrust();

        ApplyGyroTurn();

        // 4. Подавление бокового сноса (Киль сопротивляется воздуху)
        Vector3 localAirVel = transform.InverseTransformDirection(airVelocity);
        Vector3 sideAirVelocity = transform.right * localAirVel.x;
        rb.AddForce(-sideAirVelocity * rb.mass * sideResistance, ForceMode.Force);
        EnforceYawOnlyRotation(false);
    }

    private void ApplyGyroTurn()
    {
        float maxTorque = Mathf.Max(0f, gyroTurnTorque);
        float currentTurnRateDeg = rb.angularVelocity.y * Mathf.Rad2Deg;
        float activeTorque = Mathf.Clamp(turnInput, -1f, 1f) * maxTorque;
        float dampingTorque = -rb.angularVelocity.y * maxTorque * Mathf.Max(0f, gyroTurnDamping);

        float maxSafeDamping = Mathf.Abs(rb.angularVelocity.y) * rb.inertiaTensor.y / Time.fixedDeltaTime;
        dampingTorque = Mathf.Clamp(dampingTorque, -maxSafeDamping, maxSafeDamping);

        float finalTorque = activeTorque + dampingTorque;
        if (maxStructuralTurnRateDeg > 0f && Mathf.Abs(currentTurnRateDeg) > maxStructuralTurnRateDeg)
        {
            if (Mathf.Sign(finalTorque) == Mathf.Sign(currentTurnRateDeg))
            {
                float overspeed = Mathf.Abs(currentTurnRateDeg) - maxStructuralTurnRateDeg;
                finalTorque *= Mathf.Clamp01(1f - overspeed * 0.2f);
            }
        }

        currentGyroTurnTorque = finalTorque;
        rb.AddTorque(Vector3.up * finalTorque, ForceMode.Force);
    }

    private void UpdateClaudiumSlipstream(float deltaSeconds)
    {
        if (claudiumSlipstreamEnabled && !CanActivateClaudiumSlipstream)
        {
            claudiumSlipstreamEnabled = false;
        }

        float target = claudiumSlipstreamEnabled ? 1f : 0f;
        float step = ClaudiumSlipstreamActivationSeconds > 0f
            ? Mathf.Max(0f, deltaSeconds) / ClaudiumSlipstreamActivationSeconds
            : 1f;
        claudiumSlipstreamCharge01 = Mathf.MoveTowards(
            Mathf.Clamp01(claudiumSlipstreamCharge01),
            target,
            step);
    }

    private float GetHorizontalSpeedMS()
    {
        Rigidbody body = rb != null ? rb : GetComponent<Rigidbody>();
        if (body == null)
        {
            return 0f;
        }

        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    private void ApplyPropellerThrust()
    {
        float thrustDirection = Mathf.Sign(propellerPitch);
        float propellerEngagement = Mathf.Clamp01(Mathf.Abs(propellerPitch));
        float supportPowerKw = CalculateCurrentSupportPowerDrawKw();
        float residualPowerKw = Mathf.Max(0f, engineGeneratedPowerKw - supportPowerKw);

        propellerInputPowerKw = Mathf.Min(residualPowerKw, CalculatePropellerPowerRequestKw(propellerEngagement, supportPowerKw));
        propellerCalculatedEfficiency = Mathf.Clamp01(propellerEfficiency);
        propellerUsefulPowerKw = propellerInputPowerKw * propellerCalculatedEfficiency;
        if (propellerUsefulPowerKw <= 0f
            || Mathf.Approximately(thrustDirection, 0f))
        {
            propellerCalculatedEfficiency = 0f;
            propellerUsefulPowerKw = 0f;
            propellerThrustKgf = 0f;
            return;
        }

        Vector3 thrustAxis = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (thrustAxis.sqrMagnitude < 0.001f)
        {
            thrustAxis = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        }

        thrustAxis.Normalize();
        Vector3 horizontalAirVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - GetEffectiveWindVelocity(), Vector3.up);
        float signedAirspeedWithThrust = Vector3.Dot(horizontalAirVelocity, thrustAxis) * thrustDirection;
        float powerLimitedThrustN = propellerUsefulPowerKw * 1000f / Mathf.Max(1f, Mathf.Abs(signedAirspeedWithThrust));

        propellerThrustKgf = (powerLimitedThrustN / 9.81f) * thrustDirection;
        rb.AddForce(thrustAxis * (propellerThrustKgf * 9.81f), ForceMode.Force);
    }

    private void ApplyNeutralStopBrake()
    {
        neutralStopBrakeAccelerationMS2 = 0f;
        if (!neutralStopBrakeEnabled || rb == null)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float speed = horizontalVelocity.magnitude;
        float deadzone = Mathf.Max(0f, neutralStopBrakeDeadzoneMS);
        if (speed <= deadzone)
        {
            if (speed > 0.0001f)
            {
                rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
            }

            return;
        }

        float deltaSeconds = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float stopTime = Mathf.Max(0.05f, neutralStopBrakeStopTimeSeconds);
        float requestedDeceleration = speed / stopTime;
        float maxDeceleration = Mathf.Max(0f, neutralStopBrakeMaxDecelerationMS2);
        float deceleration = Mathf.Min(maxDeceleration, requestedDeceleration);
        float deltaSpeed = Mathf.Min(speed, deceleration * deltaSeconds);
        if (deltaSpeed <= 0f)
        {
            return;
        }

        Vector3 velocityChange = -horizontalVelocity.normalized * deltaSpeed;
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
        neutralStopBrakeAccelerationMS2 = deltaSpeed / deltaSeconds;
    }

    private float CalculateCurrentSupportPowerDrawKw()
    {
        return Mathf.Clamp(
            Mathf.Max(0f, claudiumPowerDrawKw),
            0f,
            EnginePowerCapacityKw);
    }

    private float CalculatePropellerPowerRequestKw(float propellerEngagement, float supportPowerKw)
    {
        float availablePowerKw = Mathf.Max(0f, EnginePowerCapacityKw - Mathf.Max(0f, supportPowerKw));
        return availablePowerKw * Mathf.Clamp01(propellerEngagement);
    }

    private void ApplyLateralOmniThrust()
    {
        float input = Mathf.Clamp(sideInput, -1f, 1f);
        float forceN = Mathf.Abs(input) * Mathf.Max(0f, lateralOmniThrustKgf) * 9.81f;
        if (forceN <= 0.001f)
        {
            return;
        }

        Vector3 sideAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (sideAxis.sqrMagnitude < 0.001f)
        {
            sideAxis = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.right;
        }

        rb.AddForce(sideAxis.normalized * (forceN * Mathf.Sign(input)), ForceMode.Force);
    }

    private void UpdateHeadingAutopilot()
    {
        if (headingHold)
        {
            float currentHeading = transform.eulerAngles.y;
            float headingError = Mathf.DeltaAngle(currentHeading, targetHeading);

            // P-терм: требуемая угловая скорость (градусов в секунду)
            float targetTurnRate = headingError * headingStiffness;

            // Ограничение скорости поворота автопилотом
            targetTurnRate = Mathf.Clamp(targetTurnRate, -maxAutoTurnRateDeg, maxAutoTurnRateDeg);

            // D-терм: компенсация по текущей угловой скорости
            float currentTurnRate = rb.angularVelocity.y * Mathf.Rad2Deg;
            float rateError = targetTurnRate - currentTurnRate;

            // Вывод на штурвал (turnInput)
            float turnCommand = rateError * headingDamping;
            turnInput = Mathf.Clamp(turnCommand, -1f, 1f);
        }
    }

    private void UpdateCruiseControl()
    {
        if (!cruiseControl)
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            }

            forward.Normalize();
            Vector3 horizontalVelocity = rb.linearVelocity;
            horizontalVelocity.y = 0f;
            float currentSpeed = Vector3.Dot(horizontalVelocity, forward);
            float speedError = targetSpeedMS - currentSpeed;
            bool zeroSpeedTarget = Mathf.Abs(targetSpeedMS) <= 0.001f;

            // Жесткость (P-терм)
            if (zeroSpeedTarget)
            {
                thrustInput = 0f;
                neutralStopBrakeEnabled = true;
                return;
            }

            neutralStopBrakeEnabled = false;
            float desiredOutput = Mathf.Clamp(speedError * speedStiffness, -1f, 1f);

            float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, speedDamping) * 6f * Time.fixedDeltaTime);
            thrustInput = Mathf.Lerp(thrustInput, desiredOutput, response);
    }

    private Vector3 GetEffectiveWindVelocity()
    {
        return windVelocity * CurrentWindAerodynamicFactor;
    }

    private static Vector3 FlattenHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HeadingFromVector(Vector3 value)
    {
        float angle = Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    private void UpdateSimplifiedClaudium()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        currentGasLift = 0f;
        float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float requestedLiftKg = CalculateDesiredClaudiumLiftKg();
        if (claudiumMaxLiftKg > 0f)
        {
            requestedLiftKg = Mathf.Min(requestedLiftKg, claudiumMaxLiftKg);
        }
        else
        {
            requestedLiftKg = 0f;
        }

        bool hasClaudium = claudiumStock > 0f;
        bool canLift = enginePowerKwAt100 > 0f
            && hasClaudium
            && claudiumLiftEfficiency > 0f;

        float requestedPowerKw = canLift ? CalculateClaudiumPowerKwForLift(requestedLiftKg) : 0f;
        float requestedModulePowerKw = CalculatePoweredModulePowerRequestKw(requestedPowerKw);
        UpdateEnginePowerOutput(requestedPowerKw + requestedModulePowerKw);

        float targetLiftN = 0f;
        if (!hasClaudium)
        {
            claudiumCurrentLiftN = 0f;
            claudiumPowerDrawWatts = 0f;
            claudiumPowerDrawKw = 0f;
            claudiumRequestedLiftKg = requestedLiftKg;
            activeLiftForce = 0f;
            return;
        }

        if (canLift)
        {
            float powerForLiftKw = Mathf.Min(requestedPowerKw, engineGeneratedPowerKw);
            targetLiftN = powerForLiftKw * claudiumLiftEfficiency * 9.81f;
        }

        float liftStepN = CalculateResponseStep(claudiumMaxLiftKg * 9.81f, claudiumLoopResponseRate01PerSecond, dt);
        claudiumCurrentLiftN = Mathf.MoveTowards(claudiumCurrentLiftN, targetLiftN, liftStepN);
        if (claudiumCurrentLiftN < 0.001f)
        {
            claudiumCurrentLiftN = 0f;
        }

        claudiumRequestedLiftKg = requestedLiftKg;
        claudiumPowerDrawKw = claudiumLiftEfficiency > 0f ? CalculateClaudiumPowerKwForLift(claudiumCurrentLiftN / 9.81f) : 0f;
        claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;

        float supportedTons = Mathf.Max(0f, claudiumCurrentLiftN / 9.81f) / 1000f;
        float consumption = claudiumConsumptionPerTonSecond * ClaudiumSlipstreamClaudiumMultiplier * supportedTons * dt;
        if (consumption > 0f)
        {
            if (claudiumStock >= consumption)
            {
                claudiumStock -= consumption;
            }
            else
            {
                float availableFraction = Mathf.Clamp01(claudiumStock / consumption);
                claudiumStock = 0f;
                claudiumCurrentLiftN *= availableFraction;
                claudiumPowerDrawKw *= availableFraction;
                claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;
            }
        }

        activeLiftForce = claudiumCurrentLiftN;
    }

    private float CalculatePoweredModulePowerRequestKw(float claudiumPowerKw)
    {
        return 0f;
    }

    private float CalculateClaudiumPowerKwForLift(float liftKg)
    {
        if (claudiumLiftEfficiency <= 0f) return 0f;
        return Mathf.Max(0f, liftKg) / claudiumLiftEfficiency;
    }

    private static float CalculateResponseStep(float maximum, float rate01PerSecond, float deltaSeconds)
    {
        if (rate01PerSecond <= 0f)
        {
            return float.PositiveInfinity;
        }

        return Mathf.Max(0f, maximum) * Mathf.Max(0f, rate01PerSecond) * Mathf.Max(0f, deltaSeconds);
    }

    private void UpdateEnginePowerOutput(float minimumPowerKw)
    {
        float maxLever = EnginePowerLeverLimit;
        engineMinimumPowerLever = enginePowerKwAt100 > 0f ? Mathf.Clamp(minimumPowerKw / enginePowerKwAt100, 0f, maxLever) : 0f;
        float propellerLever = Mathf.Clamp01(Mathf.Abs(propellerPitch)) * Mathf.Max(0f, maxLever - engineMinimumPowerLever);
        float requestedLever = engineMinimumPowerLever + propellerLever;
        enginePowerLever = Mathf.MoveTowards(
            Mathf.Clamp(enginePowerLever, 0f, maxLever),
            Mathf.Clamp(requestedLever, 0f, maxLever),
            CalculateResponseStep(maxLever, engineResponseRate01PerSecond, Time.fixedDeltaTime));

        engineGeneratedPowerKw = Mathf.Max(0f, enginePowerKwAt100) * enginePowerLever;
        engineEfficiencyCurrent = Mathf.Clamp01(engineFuelEfficiency);
        engineHasFuel = engineFuelStockKg > 0f;
        engineFuelConsumptionKgPerSecond = 0f;

        if (engineGeneratedPowerKw <= 0f || engineEfficiencyCurrent <= 0f || engineFuelEnergyKwhPerKg <= 0f)
        {
            engineGeneratedPowerKw = 0f;
            return;
        }

        engineFuelConsumptionKgPerSecond = engineGeneratedPowerKw / engineEfficiencyCurrent / engineFuelEnergyKwhPerKg / 3600f;
        float requestedFuel = engineFuelConsumptionKgPerSecond * Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        if (engineFuelStockKg >= requestedFuel)
        {
            engineFuelStockKg -= requestedFuel;
            return;
        }

        float availableFraction = requestedFuel > 0f ? Mathf.Clamp01(engineFuelStockKg / requestedFuel) : 0f;
        engineFuelStockKg = 0f;
        engineGeneratedPowerKw *= availableFraction;
        engineFuelConsumptionKgPerSecond *= availableFraction;
        engineHasFuel = false;
    }

    private float CalculateDesiredClaudiumLiftKg()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        float mass = rb != null ? rb.mass : baseMass;
        float trimMass = Mathf.Max(1f, targetTrimMass > 0f ? targetTrimMass : mass);

        if (altitudeHold && rb != null)
        {
            if (!wasAltitudeHold)
            {
                if (targetAltitude <= 0f)
                {
                    targetAltitude = rb.position.y;
                }

                wasAltitudeHold = true;
                altIntegral = 0f;
                if (Mathf.Abs(targetAltitude - rb.position.y) <= Mathf.Max(2f, altDriftTolerance * 2f) &&
                    Mathf.Abs(rb.linearVelocity.y) <= 0.5f)
                {
                    PrimeClaudiumLiftForHover(mass);
                }
            }

            targetAltitude = Mathf.Max(0f, targetAltitude);

            float altitudeError = targetAltitude - rb.position.y;
            float driftTolerance = Mathf.Max(0f, altDriftTolerance);
            float absError = Mathf.Abs(altitudeError);
            float correctedError = absError > driftTolerance
                ? altitudeError - Mathf.Sign(altitudeError) * driftTolerance
                : 0f;

            if (Mathf.Abs(correctedError) > 0.001f)
            {
                altIntegral += correctedError * Time.fixedDeltaTime * 0.05f;
                altIntegral = Mathf.Clamp(altIntegral, -0.25f, 0.25f);
            }
            else
            {
                altIntegral = Mathf.MoveTowards(altIntegral, 0f, Time.fixedDeltaTime * 0.25f);
            }

            float targetVerticalSpeed = 0f;
            if (Mathf.Abs(correctedError) > 0.001f)
            {
                targetVerticalSpeed = correctedError * Mathf.Max(0f, altStiffness);
                targetVerticalSpeed = Mathf.Clamp(targetVerticalSpeed, -maxAutoVerticalSpeed, maxAutoVerticalSpeed);
            }

            float velocityError = targetVerticalSpeed - rb.linearVelocity.y;
            float desiredAcceleration = velocityError * Mathf.Max(0f, altDamping) + altIntegral;
            float requestedKg = mass * Mathf.Max(0f, 9.81f + desiredAcceleration) / 9.81f;

            if (rb.linearVelocity.y > maxStructuralVerticalSpeed * 0.9f)
            {
                float speedFactor = Mathf.InverseLerp(maxStructuralVerticalSpeed, maxStructuralVerticalSpeed * 0.9f, rb.linearVelocity.y);
                requestedKg *= speedFactor;
            }

            return requestedKg;
        }

        wasAltitudeHold = false;
        altIntegral = 0f;
        if (rb == null)
        {
            return Mathf.Max(0f, trimMass);
        }

        float verticalLimit = Mathf.Max(0.1f, maxAutoVerticalSpeed);
        if (maxStructuralVerticalSpeed > 0.1f)
        {
            verticalLimit = Mathf.Min(verticalLimit, maxStructuralVerticalSpeed);
        }

        float manualTargetVerticalSpeed = Mathf.Clamp(liftInput, -1f, 1f) * verticalLimit;
        float manualVelocityError = manualTargetVerticalSpeed - rb.linearVelocity.y;
        float manualDesiredAcceleration = manualVelocityError * Mathf.Max(0.1f, altDamping);
        float manualRequestedKg = mass * Mathf.Max(0f, 9.81f + manualDesiredAcceleration) / 9.81f;
        return Mathf.Max(0f, manualRequestedKg);
    }

    private void PrimeClaudiumLiftForHover(float mass)
    {
        if (claudiumMaxLiftKg <= 0f || enginePowerKwAt100 <= 0f || claudiumLiftEfficiency <= 0f || claudiumStock <= 0f)
        {
            return;
        }

        float hoverLiftKg = Mathf.Max(0f, mass);
        float powerLimitedLiftKg = EnginePowerCapacityKw * claudiumLiftEfficiency;
        hoverLiftKg = Mathf.Min(hoverLiftKg, claudiumMaxLiftKg, Mathf.Max(0f, powerLimitedLiftKg));

        claudiumCurrentLiftN = hoverLiftKg * 9.81f;
        claudiumRequestedLiftKg = hoverLiftKg;
        claudiumPowerDrawKw = claudiumLiftEfficiency > 0f ? CalculateClaudiumPowerKwForLift(hoverLiftKg) : 0f;
        claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;
        activeLiftForce = claudiumCurrentLiftN;
    }

    void UpdateClaudium()
    {
        UpdateSimplifiedClaudium();
    }

    private MetaGameState ResolveMetaGameState()
    {
        if (cachedMetaGameState == null)
        {
            cachedMetaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return cachedMetaGameState;
    }

    private void UpdateEngineThrottles()
    {
        enginePowerLever = Mathf.Clamp(enginePowerLever, 0f, EnginePowerLeverLimit);
        propellerPitch = Mathf.MoveTowards(
            Mathf.Clamp(propellerPitch, -1f, 1f),
            Mathf.Clamp(thrustInput, -1f, 1f),
            CalculateResponseStep(1f, engineResponseRate01PerSecond, Time.fixedDeltaTime));
    }

    private void OnDrawGizmos()
    {
        // Визуализация ветра
        Vector3 effectiveWindVelocity = GetEffectiveWindVelocity();
        if (effectiveWindVelocity.sqrMagnitude > 0.1f)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.7f); // Пурпурный
            Vector3 startPos = transform.position + Vector3.up * 10f; // Чуть выше корабля
            Gizmos.DrawLine(startPos, startPos + effectiveWindVelocity);
            Gizmos.DrawWireSphere(startPos + effectiveWindVelocity, 1f); // Наконечник
        }

    }
}
