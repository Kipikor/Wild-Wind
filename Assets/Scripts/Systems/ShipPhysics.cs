using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(CoreTacticalShipMotor))]
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
    public float minElevationDegrees = -80f;
    [InspectorName("Максимальный угол ствола, град")]
    public float maxElevationDegrees = 80f;
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
    private const float MainGunRoundsPerMinute = 30f;
    private const float MainGunMinElevationDegrees = -80f;
    private const float MainGunMaxElevationDegrees = 80f;
    private const float SecondaryGunMinElevationDegrees = -80f;
    private const float SecondaryGunMaxElevationDegrees = 80f;
    private const float ManualGunAimTargetSmoothSeconds = 0.055f;
    private const float ManualGunSphereAimSensitivityDegreesPerPixel = 0.18f;
    private const float ManualGunSphereAimMinPitchDegrees = -89f;
    private const float ManualGunSphereAimMaxPitchDegrees = 89f;
    private const float GunProjectileTrailBrightnessMultiplier = 6f;
    private const float GunProjectileTrailEmissionMultiplier = 2.5f;
    private const float GunMuzzleFlashLifetimeSeconds = 0.08f;
    public const float ClaudiumSlipstreamActivationMinSpeedMS = 15f;
    public const float ClaudiumSlipstreamActivationSeconds = 20f;
    private const float ClaudiumSlipstreamFullDragMultiplier = 1f;

    private Rigidbody rb;
    private CoreTacticalShipMotor strategicMotor;

    [Header("Параметры корабля")]
    public float baseMass = 1000f;
    [Tooltip("Максимальная взлетная масса, которую допускает корпус, в килограммах.")]
    public float hullMaxTakeoffMassKg = 2000f;
    [HideInInspector] public float cargoMassKg;

    [Header("Hull Thrust And Fuel")]
    [Tooltip("Forward thrust stat used to derive strategic acceleration, in kilogram-force.")]
    public float hullForwardThrustKgf = 1200f;
    [Tooltip("Fuel resource burned by the hull.")]
    public string fuelResourceId = "charcoal";
    [Tooltip("Fuel consumed per minute while the ship is active.")]
    public float fuelConsumptionKgPerMinute = 1.2f;
    [Tooltip("Runtime fuel burn in kg/s after active multipliers.")]
    [HideInInspector] public float fuelConsumptionKgPerSecond;
    [Tooltip("Whether the hull has fuel available for movement.")]
    [HideInInspector] public bool hasFuel = true;
    [HideInInspector] public float fuelStockKg = 0f;

    [Header("Корпусная скорость")]
    [Tooltip("Reference clean strategic cruise speed of the hull, m/s.")]
    public float hullCruiseReferenceSpeedMS = 30f;

    [Header("Ход и скольжение")]
    [Tooltip("Clean maximum strategic speed of the ship, m/s.")]
    public float baseMaxSpeedMS = 30f;
    [Tooltip("Текущий множитель хода от нагрузки. Активные устройства должны снижать его вместо публичного расхода мощности.")]
    public float loadSpeedMultiplier = 1f;
    [Tooltip("Текущий множитель хода от повреждений.")]
    public float damageSpeedMultiplier = 1f;
    [Tooltip("С какой доли текущего хода стратегическая тяга начинает плавно слабеть.")]
    [Range(0f, 1f)] public float nearMaxThrustFadeStartRatio = 0.72f;
    [Tooltip("На какой доле текущего хода стратегическая тяга перестает разгонять корабль дальше.")]
    [Range(0.01f, 1.5f)] public float nearMaxThrustFadeEndRatio = 1.0f;
    [Tooltip("Во сколько раз скольжение поднимает доступный максимальный ход.")]
    public float slipstreamMaxSpeedMultiplier = 5f;
    [Tooltip("Доля чистого хода, которую нужно набрать для включения скольжения.")]
    [Range(0f, 1.5f)] public float slipstreamActivationSpeedRatio = 0.8f;
    [Tooltip("Сколько секунд максимум хода разгоняется от обычного до полного скольжения.")]
    public float slipstreamMaxRampSeconds = ClaudiumSlipstreamActivationSeconds;
    [Tooltip("Multiplier applied to fuel consumption at full slipstream charge.")]
    public float slipstreamFuelConsumptionMultiplier = 2f;
    [HideInInspector] public float currentMaxSpeedMS;

    [Header("Клавдиевый контур")]
    [Tooltip("Ресурс клавдия в грузовом списке корабля.")]
    public string claudiumResourceId = "claudium";
    [HideInInspector] public float claudiumStock = 0f;
    [Tooltip("Максимальная масса в килограммах, которую контур способен поддерживать как стратегический лимит корпуса.")]
    public float claudiumMaxLiftKg = 1000f;
    [Tooltip("Claudium slipstream request. Can be enabled above the relative clean-speed threshold, then ramps max ход.")]
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
    public bool showGunAimHud = false;
    [Tooltip("Временно отключено: ручной главный калибр вернется, когда камера/прицел будут готовы.")]
    public bool manualMainBatteryEnabled = false;
    [Tooltip("Индекс ручной группы в списке shipGunGroups.")]
    public int manualGunGroupIndex;
    [Tooltip("Орудийные группы корабля: главный калибр, ПМК и будущие группы.")]
    public List<ShipGunGroup> shipGunGroups = new List<ShipGunGroup>
    {
        new ShipGunGroup
        {
            groupId = "main",
            displayNameRu = "Главный калибр",
            fireMode = ShipGunFireMode.Automatic,
            localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
            barrelsPerSalvo = 1,
            roundsPerMinute = MainGunRoundsPerMinute,
            yawSpeedDegPerSecond = 45f,
            elevationUpSpeedDegPerSecond = 22f,
            elevationDownSpeedDegPerSecond = 30f,
            minElevationDegrees = MainGunMinElevationDegrees,
            maxElevationDegrees = MainGunMaxElevationDegrees,
            fireAlignmentToleranceDegrees = 1.5f,
            maxRangeMeters = 2500f,
            muzzleVelocityMS = 360f,
            projectileMassKg = 12f,
            projectileRadiusMeters = 0.10f,
            horizontalSpreadAtMaxRangeMeters = 24f,
            verticalSpreadAtMaxRangeMeters = 9f,
            automaticTargetsDamageableShips = true,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 76 мм",
                shellType = DamageShellType.ArmorPiercing,
                damageType = CoreTacticalDamageType.Kinetic,
                resistanceIgnorePercent = 24f,
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
            minElevationDegrees = SecondaryGunMinElevationDegrees,
            maxElevationDegrees = SecondaryGunMaxElevationDegrees,
            fireAlignmentToleranceDegrees = 3f,
            maxRangeMeters = 1500f,
            muzzleVelocityMS = 420f,
            projectileMassKg = 0.12f,
            projectileRadiusMeters = 0.035f,
            horizontalSpreadAtMaxRangeMeters = 35f,
            verticalSpreadAtMaxRangeMeters = 16f,
            automaticTargetsDamageableShips = true,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 20 мм",
                shellType = DamageShellType.ArmorPiercing,
                damageType = CoreTacticalDamageType.Kinetic,
                resistanceIgnorePercent = 4f,
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

    [Header("Strategic Movement")]
    [Tooltip("Vertical speed used by CoreTacticalShipMotor, m/s.")]
    public float strategicVerticalSpeedMS = 8f;
    [Tooltip("Vertical acceleration used by CoreTacticalShipMotor, m/s2.")]
    public float strategicVerticalAccelerationMS2 = 8f;
    [Tooltip("Yaw rate used by CoreTacticalShipMotor, deg/s.")]
    public float strategicYawRateDegPerSecond = 24f;
    [Tooltip("Yaw acceleration used by CoreTacticalShipMotor, deg/s2.")]
    public float strategicYawAccelerationDegPerSecond2 = 84f;

    public float ClaudiumSlipstreamCharge01 => Mathf.Clamp01(claudiumSlipstreamCharge01);
    public float CurrentFuelConsumptionMultiplier => Mathf.Lerp(1f, Mathf.Max(1f, slipstreamFuelConsumptionMultiplier), ClaudiumSlipstreamCharge01);
    public bool ClaudiumSlipstreamActive => claudiumSlipstreamEnabled;
    public float HorizontalSpeedMS => GetHorizontalSpeedMS();
    public float CleanBaseMaxSpeedMS => ResolveCleanBaseMaxSpeedMS();
    public float SlipstreamActivationSpeedMS => Mathf.Max(0f, CleanBaseMaxSpeedMS * Mathf.Max(0f, slipstreamActivationSpeedRatio));
    public float CurrentMaxSpeedMS => CalculateCurrentMaxSpeedMS();
    public bool CanActivateClaudiumSlipstream => HorizontalSpeedMS >= SlipstreamActivationSpeedMS;
    public float HullThrustCapacityKgf => Mathf.Max(0f, hullForwardThrustKgf);

    public bool TrySetClaudiumSlipstreamEnabled(bool enabled, out string reason)
    {
        reason = "";
        if (enabled && !CanActivateClaudiumSlipstream)
        {
            claudiumSlipstreamEnabled = false;
            reason = "Claudium slipstream needs speed above "
                + SlipstreamActivationSpeedMS.ToString("0")
                + " m/s.";
            return false;
        }

        claudiumSlipstreamEnabled = enabled;
        reason = claudiumSlipstreamEnabled
            ? "Claudium slipstream enabled."
            : "Claudium slipstream disabled.";
        return true;
    }

    public float EstimateFullSlipstreamCruiseSpeedMS()
    {
        float fullSlipstreamMaxSpeedMS = CalculateMaxSpeedMS(1f);
        return Mathf.Max(1f, fullSlipstreamMaxSpeedMS);
    }

    [Header("Strategic Input State")]
    [HideInInspector] private float strategicForwardInput;
    [HideInInspector] private float strategicLateralInput;
    [HideInInspector] private float strategicVerticalInput;
    [HideInInspector] private float strategicTurnInput;
    [HideInInspector] private bool strategicStopCommand;
    [HideInInspector] private bool strategicAltitudeHold;
    [HideInInspector] private bool strategicHeadingHold;
    [HideInInspector] private bool strategicSpeedHold;
    [HideInInspector] private float strategicTargetAltitude;
    [HideInInspector] private float strategicTargetHeading;
    [HideInInspector] private float strategicTargetSpeedMS;

    public float StrategicForwardInput => strategicForwardInput;
    public float StrategicLateralInput => strategicLateralInput;
    public float StrategicVerticalInput => strategicVerticalInput;
    public float StrategicTurnInput => strategicTurnInput;
    public bool StrategicStopCommand => strategicStopCommand;
    public float CurrentStrategicThrustOrderKgf => Mathf.Abs(strategicForwardInput) * HullThrustCapacityKgf;
    public float CurrentStrategicLiftOrderKg => Mathf.Abs(strategicVerticalInput) * Mathf.Max(0f, claudiumMaxLiftKg);
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

    public void SetStrategicManualInput(float forward, float lateral, float vertical, float turn, bool stop)
    {
        SetStrategicInputState(
            forward,
            lateral,
            vertical,
            turn,
            stop,
            false,
            strategicTargetAltitude,
            false,
            strategicTargetHeading,
            false,
            0f);
    }

    public void SetStrategicInputState(
        float forward,
        float lateral,
        float vertical,
        float turn,
        bool stop,
        bool altitudeHold,
        float targetAltitude,
        bool headingHold,
        float targetHeading,
        bool speedHold,
        float targetSpeedMS)
    {
        strategicForwardInput = Mathf.Clamp(forward, -1f, 1f);
        strategicLateralInput = Mathf.Clamp(lateral, -1f, 1f);
        strategicVerticalInput = Mathf.Clamp(vertical, -1f, 1f);
        strategicTurnInput = Mathf.Clamp(turn, -1f, 1f);
        strategicStopCommand = stop;
        strategicAltitudeHold = altitudeHold;
        strategicTargetAltitude = Mathf.Max(0f, targetAltitude);
        strategicHeadingHold = headingHold;
        strategicTargetHeading = NormalizeHeadingDegrees(targetHeading);
        strategicSpeedHold = speedHold;
        strategicTargetSpeedMS = targetSpeedMS;
    }

    public void ClearStrategicInput()
    {
        strategicForwardInput = 0f;
        strategicLateralInput = 0f;
        strategicVerticalInput = 0f;
        strategicTurnInput = 0f;
        strategicStopCommand = false;
        strategicAltitudeHold = false;
        strategicHeadingHold = false;
        strategicSpeedHold = false;
        strategicTargetSpeedMS = 0f;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        EnsureStrategicMotor();

        ConfigureStrategicRigidbody();
    }

    public void RefreshRuntimeShipSettings()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            ConfigureStrategicRigidbody();
        }

        SyncStrategicMotorSettings();
    }

    private CoreTacticalShipMotor EnsureStrategicMotor()
    {
        if (strategicMotor == null)
        {
            strategicMotor = GetComponent<CoreTacticalShipMotor>();
        }

        if (strategicMotor == null)
        {
            strategicMotor = gameObject.AddComponent<CoreTacticalShipMotor>();
        }

        return strategicMotor;
    }

    private void ConfigureStrategicRigidbody()
    {
        if (rb == null) return;

        rb.mass = GetTotalMassKg();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = false;
        rb.linearDamping = Mathf.Max(rb.linearDamping, 0.22f);
        rb.angularDamping = Mathf.Max(rb.angularDamping, 0.9f);
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ConfigureYawOnlyRigidbody();
        EnforceYawOnlyRotation(true);
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
        ClearStrategicInput();
        strategicStopCommand = true;
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
            damageType = CoreTacticalDamageType.Kinetic,
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
            resistances = new CoreTacticalResistanceSet(60f, 10f, 10f, 20f),
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
        RefreshRuntimeShipSettings();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        EnsureGunGroups();
        if (IsManualMainBatteryEnabled())
        {
            UpdateManualGunAim();
            UpdateGunInput();
        }
        else
        {
            ClearManualGunRuntimeState();
        }

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
        if (!Application.isPlaying || !showGunAimHud || !IsManualMainBatteryEnabled()) return;
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
        group = group ?? ResolveDefaultGunGroupForBallistics();
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
        if (!IsManualMainBatteryEnabled())
        {
            reason = "Ручной главный калибр временно отключен. Работает ПМК.";
            weaponLastMessage = reason;
            return false;
        }

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
        int reloadingGroups = 0;
        float longestReloadSeconds = 0f;
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
                    if (IsGunGroupReloading(manualGroup, out float reloadSeconds))
                    {
                        reloadingGroups++;
                        longestReloadSeconds = Mathf.Max(longestReloadSeconds, reloadSeconds);
                    }

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

        if (reloadingGroups > 1)
        {
            reason = "Главный калибр перезаряжается: "
                + longestReloadSeconds.ToString("0.0")
                + " с. ("
                + reloadingGroups.ToString()
                + "/"
                + Mathf.Max(1, attemptedGroups).ToString()
                + " башн.)";
        }
        else
        {
            reason = string.IsNullOrWhiteSpace(lastFailure) ? "No manual turret is ready to fire." : lastFailure;
        }

        weaponLastMessage = reason;
        return false;
    }

    public bool TryGetManualGunCameraAimTarget(out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;
        if (!IsManualMainBatteryEnabled())
        {
            return false;
        }

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
        if (!IsManualMainBatteryEnabled())
        {
            return false;
        }

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
        group ??= ResolveDefaultGunGroupForBallistics();
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
        if (!IsManualMainBatteryEnabled())
        {
            ClearManualGunRuntimeState();
            return;
        }

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
        if (!IsManualMainBatteryEnabled())
        {
            return;
        }

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

        if (!TryBuildManualGunAimRay(camera, mousePosition, out Ray ray))
        {
            if (smoothTarget) ResetManualGunTargetSmoothing();
            return false;
        }

        Vector3 center = transform.position;
        float maxRange = Mathf.Max(1f, group.maxRangeMeters);
        float freeAimRange = useLockedRange
            ? Mathf.Clamp(manualGunLockedRangeMeters, 1f, maxRange)
            : maxRange;
        Vector3 targetPoint;
        Vector3 targetVelocity = Vector3.zero;
        bool hasHitTarget = false;

        if (IsLockedGameplayAim())
        {
            targetPoint = center + ray.direction.normalized * freeAimRange;
        }
        else if (TryRaycastCursorTarget(ray, maxRange, out RaycastHit hit))
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

    private ShipGunGroup ResolveDefaultGunGroupForBallistics()
    {
        EnsureGunGroups();
        return GetManualGunGroup()
            ?? FindGunGroupById("main")
            ?? FindFirstGunGroup(ShipGunFireMode.Automatic);
    }

    private bool IsManualMainBatteryEnabled()
    {
        return manualMainBatteryEnabled;
    }

    private void ClearManualGunRuntimeState()
    {
        weaponAimFlightTimeSeconds = 0f;
        weaponAimDistanceMeters = 0f;
        weaponAimStatus = "";
        lastManualGunAim = default;
        manualGunRangeLocked = false;
        hasManualGunSphereAim = false;
        ResetManualGunTargetSmoothing();
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
            float remainingSeconds = GetGunReloadRemainingSeconds(group);
            reason = ResolveGunGroupDisplayName(group) + ": перезарядка "
                + remainingSeconds.ToString("0.0") + " с.";
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
        aim = AlignAimWithCurrentBarrel(group, aim);
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

    private BallisticAimSolution AlignAimWithCurrentBarrel(ShipGunGroup group, BallisticAimSolution aim)
    {
        Vector3 origin = ResolveGunMuzzlePosition(group);
        Vector3 barrelForward = ResolveGunMuzzleForward(group, aim.launchDirection);
        if (barrelForward.sqrMagnitude > 0.001f)
        {
            barrelForward.Normalize();
            aim.origin = origin;
            aim.launchDirection = barrelForward;
            aim.launchVelocity = barrelForward * Mathf.Max(1f, group != null ? group.muzzleVelocityMS : 1f);
            aim.directDistanceFromMuzzle = Vector3.Distance(origin, aim.targetPoint);
        }

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
            ApplyGunProjectileTrailMaterialColor(renderer.material, BuildGunProjectileTrailColor(projectileColor));
        }

        SpawnGunMuzzleFlash(spawnPosition, projectileColor, group);

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
            trail.startWidth = Mathf.Max(0.08f, group.projectileRadiusMeters * 4.5f);
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

    private void SpawnGunMuzzleFlash(Vector3 position, Color projectileColor, ShipGunGroup group)
    {
        if (!Application.isPlaying || !IsFinite(position))
        {
            return;
        }

        GameObject flashObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flashObject.name = (group != null ? group.displayNameRu : "Gun") + " Muzzle Flash";
        flashObject.transform.position = position;
        float caliberScale = group != null ? Mathf.Max(0.35f, group.projectileRadiusMeters * 8f) : 0.35f;
        flashObject.transform.localScale = Vector3.one * caliberScale;

        Collider flashCollider = flashObject.GetComponent<Collider>();
        if (flashCollider != null)
        {
            flashCollider.enabled = false;
        }

        Renderer flashRenderer = flashObject.GetComponent<Renderer>();
        if (flashRenderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = shader != null ? new Material(shader) : flashRenderer.material;
            Color flashColor = BuildGunProjectileTrailColor(projectileColor);
            flashColor = Color.Lerp(flashColor, Color.white, 0.35f);
            ApplyGunProjectileTrailMaterialColor(material, flashColor);
            flashRenderer.material = material;
        }

        Destroy(flashObject, GunMuzzleFlashLifetimeSeconds);
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

    private static string ResolveGunGroupDisplayName(ShipGunGroup group)
    {
        if (group == null || string.IsNullOrWhiteSpace(group.displayNameRu))
        {
            return "Орудийная группа";
        }

        return group.displayNameRu;
    }

    private static bool IsGunGroupReloading(ShipGunGroup group, out float remainingSeconds)
    {
        remainingSeconds = GetGunReloadRemainingSeconds(group);
        return remainingSeconds > 0.001f;
    }

    private static float GetGunReloadRemainingSeconds(ShipGunGroup group)
    {
        if (group == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, group.nextShotTime - Time.time);
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

    public bool BindGunGroupsFromVisual(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return false;
        }

        EnsureGunGroups();
        if (shipGunGroups == null || shipGunGroups.Count == 0)
        {
            return false;
        }

        bool boundAny = false;
        if (IsMainBatteryEnabled())
        {
            ShipGunGroup main = EnsureMainGunGroup(
                "main",
                "Главный калибр",
                FindGunGroupById("main") ?? FindFirstGunGroup(ShipGunFireMode.Manual));
            NormalizeMainGunGroup(main);
            if (TryBindVisualMainGunMount(main, visualRoot, "Fwd_76mmTwinTurret"))
            {
                main.barrelsPerSalvo = Mathf.Max(main.barrelsPerSalvo, 2);
                boundAny = true;
            }

            if (HasVisualMainGunMount(visualRoot, "Aft_76mmTwinTurret"))
            {
                ShipGunGroup aftMain = EnsureMainGunGroup("main_aft", "Главный калибр: кормовая башня", main);
                if (TryBindVisualMainGunMount(aftMain, visualRoot, "Aft_76mmTwinTurret"))
                {
                    aftMain.barrelsPerSalvo = Mathf.Max(aftMain.barrelsPerSalvo, 2);
                    boundAny = true;
                }
            }
        }

        boundAny |= BindVisualSecondaryGunMounts(visualRoot);

        return boundAny;
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
            name.StartsWith("GunMuzzle", System.StringComparison.OrdinalIgnoreCase) ||
            name.IndexOf("Muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsNamedPitchPivot(Transform candidate)
    {
        if (candidate == null) return false;
        string name = candidate.name;
        return string.Equals(name, "Barrel", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "BarrelPitch", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "\u0421\u0442\u0432\u043e\u043b", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "РЎС‚РІРѕР»", System.StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("GunPitch", System.StringComparison.OrdinalIgnoreCase) ||
            name.IndexOf("PitchPivot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Barrel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            (name.IndexOf("MG_", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                name.IndexOf("_ROOT", System.StringComparison.OrdinalIgnoreCase) >= 0);
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
        yaw ??= FindDeepChildContaining(transform, "YawPivot");

        Transform pitch = FindDeepChild(transform, "GunPitch_" + suffix)
            ?? FindDeepChild(transform, "GunPitch")
            ?? FindDeepChild(transform, "BarrelPitch")
            ?? FindDeepChild(transform, "Barrel")
            ?? FindDeepChild(transform, "\u0421\u0442\u0432\u043e\u043b")
            ?? FindDeepChild(transform, "Ствол");
        pitch ??= FindDeepChildContaining(yaw != null ? yaw : transform, "PitchPivot")
            ?? FindDeepChildContaining(yaw != null ? yaw : transform, "Barrel");

        Transform muzzle = pitch != null
            ? FindDeepChild(pitch, "GunMuzzle_" + suffix)
                ?? FindDeepChild(pitch, "GunMuzzle")
                ?? FindDeepChild(pitch, "Muzzle")
            : null;
        muzzle ??= FindDeepChild(transform, "GunMuzzle_" + suffix)
            ?? FindDeepChild(transform, "GunMuzzle")
            ?? FindDeepChild(transform, "Muzzle");

        muzzle ??= FindDeepChildContaining(pitch != null ? pitch : transform, "Muzzle");

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

    private ShipGunGroup FindGunGroupById(string groupId)
    {
        if (shipGunGroups == null || string.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group != null && string.Equals(group.groupId, groupId, System.StringComparison.OrdinalIgnoreCase))
            {
                return group;
            }
        }

        return null;
    }

    private ShipGunGroup FindFirstGunGroup(ShipGunFireMode fireMode)
    {
        if (shipGunGroups == null)
        {
            return null;
        }

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group != null && group.fireMode == fireMode)
            {
                return group;
            }
        }

        return null;
    }

    private ShipGunGroup EnsureMainGunGroup(string groupId, string displayNameRu, ShipGunGroup template)
    {
        if (shipGunGroups == null)
        {
            shipGunGroups = new List<ShipGunGroup>();
        }

        ShipGunGroup group = FindGunGroupById(groupId);
        if (group == null)
        {
            group = CreateMainGunGroupFromTemplate(groupId, displayNameRu, template);
            shipGunGroups.Add(group);
        }

        NormalizeMainGunGroup(group);
        if (!string.IsNullOrWhiteSpace(displayNameRu))
        {
            group.displayNameRu = displayNameRu;
        }

        return group;
    }

    private static ShipGunGroup CreateMainGunGroupFromTemplate(string groupId, string displayNameRu, ShipGunGroup template)
    {
        template ??= CreateDefaultMainGunGroup();
        return new ShipGunGroup
        {
            groupId = groupId,
            displayNameRu = string.IsNullOrWhiteSpace(displayNameRu) ? groupId : displayNameRu,
            enabled = true,
            fireMode = ShipGunFireMode.Automatic,
            shell = template.shell,
            localMuzzleOffset = template.localMuzzleOffset,
            barrelsPerSalvo = Mathf.Max(1, template.barrelsPerSalvo),
            roundsPerMinute = MainGunRoundsPerMinute,
            yawSpeedDegPerSecond = template.yawSpeedDegPerSecond,
            useYawLimits = template.useYawLimits,
            minYawDegrees = template.minYawDegrees,
            maxYawDegrees = template.maxYawDegrees,
            elevationUpSpeedDegPerSecond = template.elevationUpSpeedDegPerSecond,
            elevationDownSpeedDegPerSecond = template.elevationDownSpeedDegPerSecond,
            minElevationDegrees = template.minElevationDegrees,
            maxElevationDegrees = template.maxElevationDegrees,
            fireAlignmentToleranceDegrees = template.fireAlignmentToleranceDegrees,
            maxRangeMeters = template.maxRangeMeters,
            muzzleVelocityMS = template.muzzleVelocityMS,
            projectileMassKg = template.projectileMassKg,
            projectileRadiusMeters = template.projectileRadiusMeters,
            horizontalSpreadAtMaxRangeMeters = template.horizontalSpreadAtMaxRangeMeters,
            verticalSpreadAtMaxRangeMeters = template.verticalSpreadAtMaxRangeMeters,
            gravityScale = template.gravityScale,
            projectileTrailSeconds = template.projectileTrailSeconds,
            automaticTargetsDamageableShips = template.automaticTargetsDamageableShips
        };
    }

    private void NormalizeMainGunGroup(ShipGunGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.enabled = true;
        bool manual = IsManualMainBatteryEnabled();
        group.fireMode = manual ? ShipGunFireMode.Manual : ShipGunFireMode.Automatic;
        group.roundsPerMinute = MainGunRoundsPerMinute;
        group.useYawLimits = false;
        group.minElevationDegrees = MainGunMinElevationDegrees;
        group.maxElevationDegrees = MainGunMaxElevationDegrees;
        group.automaticTargetsDamageableShips = !manual;
    }

    private bool IsMainBatteryEnabled()
    {
        return true;
    }

    private static bool IsMainGunGroupId(string groupId)
    {
        return !string.IsNullOrWhiteSpace(groupId) &&
            (string.Equals(groupId, "main", System.StringComparison.OrdinalIgnoreCase) ||
             groupId.StartsWith("main_", System.StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSecondaryGunGroupId(string groupId)
    {
        return !string.IsNullOrWhiteSpace(groupId) &&
            (string.Equals(groupId, "secondary", System.StringComparison.OrdinalIgnoreCase) ||
             groupId.StartsWith("secondary_", System.StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasVisualMainGunMount(Transform visualRoot, string mountNamePart)
    {
        return FindVisualMainGunMuzzle(visualRoot, mountNamePart) != null;
    }

    private static bool TryBindVisualMainGunMount(ShipGunGroup group, Transform visualRoot, string mountNamePart)
    {
        if (group == null || visualRoot == null)
        {
            return false;
        }

        Transform muzzle = FindVisualMainGunMuzzle(visualRoot, mountNamePart);
        if (muzzle == null)
        {
            return false;
        }

        Transform pitch = FindClosestAncestorContaining(muzzle, visualRoot, "PitchPivot")
            ?? FindDeepChildContaining(visualRoot, mountNamePart, "PitchPivot");
        Transform yaw = FindClosestAncestorContaining(muzzle, visualRoot, "YawPivot")
            ?? FindDeepChildContaining(visualRoot, mountNamePart, "YawPivot");

        group.yawPivot = yaw != null ? yaw : muzzle;
        group.pitchPivot = pitch != null ? pitch : muzzle;
        group.muzzle = muzzle;
        return true;
    }

    private static bool TryBindFirstVisualSecondaryGunMount(ShipGunGroup group, Transform visualRoot)
    {
        if (group == null || visualRoot == null)
        {
            return false;
        }

        Transform muzzle = FindDeepChildContaining(visualRoot, "MG_", "Muzzle");
        if (muzzle == null)
        {
            return false;
        }

        Transform pitch = FindClosestAncestorContaining(muzzle, visualRoot, "_ROOT")
            ?? muzzle.parent;

        group.yawPivot = pitch != null ? pitch : muzzle;
        group.pitchPivot = pitch != null ? pitch : muzzle;
        group.muzzle = muzzle;
        return true;
    }

    private bool BindVisualSecondaryGunMounts(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return false;
        }

        List<Transform> muzzles = new List<Transform>();
        CollectDeepChildrenContaining(visualRoot, muzzles, "MG_", "Muzzle");
        if (muzzles.Count == 0)
        {
            ShipGunGroup secondary = FindGunGroupById("secondary") ?? FindFirstGunGroup(ShipGunFireMode.Automatic);
            return TryBindFirstVisualSecondaryGunMount(secondary, visualRoot);
        }

        ShipGunGroup template = FindGunGroupById("secondary") ?? FindFirstGunGroup(ShipGunFireMode.Automatic);
        bool boundAny = false;
        for (int i = 0; i < muzzles.Count; i++)
        {
            string groupId = i == 0 ? "secondary" : "secondary_" + (i + 1).ToString("00");
            string displayName = i == 0 ? "ПМК" : "ПМК " + (i + 1).ToString();
            ShipGunGroup group = EnsureAutomaticSecondaryGunGroup(groupId, displayName, template);
            if (TryBindVisualSecondaryMuzzle(group, muzzles[i]))
            {
                boundAny = true;
            }
        }

        return boundAny;
    }

    private ShipGunGroup EnsureAutomaticSecondaryGunGroup(string groupId, string displayNameRu, ShipGunGroup template)
    {
        if (shipGunGroups == null)
        {
            shipGunGroups = new List<ShipGunGroup>();
        }

        ShipGunGroup group = FindGunGroupById(groupId);
        if (group == null)
        {
            group = CreateAutomaticSecondaryGunGroupFromTemplate(groupId, displayNameRu, template);
            shipGunGroups.Add(group);
        }

        NormalizeSecondaryGunGroup(group);
        if (!string.IsNullOrWhiteSpace(displayNameRu))
        {
            group.displayNameRu = displayNameRu;
        }

        return group;
    }

    private static ShipGunGroup CreateAutomaticSecondaryGunGroupFromTemplate(string groupId, string displayNameRu, ShipGunGroup template)
    {
        template ??= CreateDefaultSecondaryGunGroup();
        return new ShipGunGroup
        {
            groupId = groupId,
            displayNameRu = string.IsNullOrWhiteSpace(displayNameRu) ? groupId : displayNameRu,
            enabled = true,
            fireMode = ShipGunFireMode.Automatic,
            shell = template.shell,
            localMuzzleOffset = template.localMuzzleOffset,
            barrelsPerSalvo = Mathf.Max(1, template.barrelsPerSalvo),
            roundsPerMinute = template.roundsPerMinute,
            yawSpeedDegPerSecond = template.yawSpeedDegPerSecond,
            useYawLimits = template.useYawLimits,
            minYawDegrees = template.minYawDegrees,
            maxYawDegrees = template.maxYawDegrees,
            elevationUpSpeedDegPerSecond = template.elevationUpSpeedDegPerSecond,
            elevationDownSpeedDegPerSecond = template.elevationDownSpeedDegPerSecond,
            minElevationDegrees = template.minElevationDegrees,
            maxElevationDegrees = template.maxElevationDegrees,
            fireAlignmentToleranceDegrees = template.fireAlignmentToleranceDegrees,
            maxRangeMeters = template.maxRangeMeters,
            muzzleVelocityMS = template.muzzleVelocityMS,
            projectileMassKg = template.projectileMassKg,
            projectileRadiusMeters = template.projectileRadiusMeters,
            horizontalSpreadAtMaxRangeMeters = template.horizontalSpreadAtMaxRangeMeters,
            verticalSpreadAtMaxRangeMeters = template.verticalSpreadAtMaxRangeMeters,
            gravityScale = template.gravityScale,
            projectileTrailSeconds = template.projectileTrailSeconds,
            automaticTargetsDamageableShips = true
        };
    }

    private static void NormalizeSecondaryGunGroup(ShipGunGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.enabled = true;
        group.fireMode = ShipGunFireMode.Automatic;
        group.useYawLimits = false;
        group.minElevationDegrees = SecondaryGunMinElevationDegrees;
        group.maxElevationDegrees = SecondaryGunMaxElevationDegrees;
        group.automaticTargetsDamageableShips = true;
    }

    private static bool TryBindVisualSecondaryMuzzle(ShipGunGroup group, Transform muzzle)
    {
        if (group == null || muzzle == null)
        {
            return false;
        }

        Transform pitch = FindClosestAncestorContaining(muzzle, null, "_ROOT")
            ?? muzzle.parent;

        group.yawPivot = pitch != null ? pitch : muzzle;
        group.pitchPivot = pitch != null ? pitch : muzzle;
        group.muzzle = muzzle;
        return true;
    }

    private static Transform FindVisualMainGunMuzzle(Transform visualRoot, string mountNamePart)
    {
        if (visualRoot == null || string.IsNullOrWhiteSpace(mountNamePart))
        {
            return null;
        }

        return FindDeepChildContaining(visualRoot, mountNamePart, "Muzzle_1")
            ?? FindDeepChildContaining(visualRoot, mountNamePart, "Muzzle");
    }

    private static Transform FindDeepChildContaining(Transform root, params string[] requiredNameParts)
    {
        if (root == null)
        {
            return null;
        }

        if (NameContainsAll(root.name, requiredNameParts))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChildContaining(root.GetChild(i), requiredNameParts);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void CollectDeepChildrenContaining(Transform root, List<Transform> results, params string[] requiredNameParts)
    {
        if (root == null || results == null)
        {
            return;
        }

        if (NameContainsAll(root.name, requiredNameParts))
        {
            results.Add(root);
        }

        for (int i = 0; i < root.childCount; i++)
        {
            CollectDeepChildrenContaining(root.GetChild(i), results, requiredNameParts);
        }
    }

    private static Transform FindClosestAncestorContaining(Transform child, Transform stopRoot, params string[] requiredNameParts)
    {
        Transform current = child;
        while (current != null)
        {
            if (NameContainsAll(current.name, requiredNameParts))
            {
                return current;
            }

            if (current == stopRoot)
            {
                break;
            }

            current = current.parent;
        }

        return null;
    }

    private static bool NameContainsAll(string name, params string[] requiredNameParts)
    {
        if (string.IsNullOrWhiteSpace(name) || requiredNameParts == null || requiredNameParts.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < requiredNameParts.Length; i++)
        {
            string part = requiredNameParts[i];
            if (!string.IsNullOrWhiteSpace(part) &&
                name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
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

            if (IsMainGunGroupId(group.groupId))
            {
                NormalizeMainGunGroup(group);
            }

            if (IsSecondaryGunGroupId(group.groupId))
            {
                NormalizeSecondaryGunGroup(group);
                hasSecondaryGroup = true;
            }
        }

        if (IsManualMainBatteryEnabled())
        {
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
                if (IsMainGunGroupId(manualGroup.groupId))
                {
                    NormalizeMainGunGroup(manualGroup);
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
        }
        else
        {
            int mainIndex = FindMainGunGroupIndex();
            if (mainIndex < 0 && !HasGunGroupId("main"))
            {
                shipGunGroups.Insert(0, CreateDefaultMainGunGroup());
                mainIndex = 0;
            }

            manualGunGroupIndex = Mathf.Clamp(mainIndex >= 0 ? mainIndex : 0, 0, Mathf.Max(0, shipGunGroups.Count - 1));
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

    private int FindMainGunGroupIndex()
    {
        if (shipGunGroups == null) return -1;

        for (int i = 0; i < shipGunGroups.Count; i++)
        {
            ShipGunGroup group = shipGunGroups[i];
            if (group != null && IsMainGunGroupId(group.groupId))
            {
                return i;
            }
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

    private bool TryBuildManualGunAimRay(Camera camera, Vector3 mousePosition, out Ray ray)
    {
        ray = default;
        if (camera == null)
        {
            return false;
        }

        if (IsLockedGameplayAim())
        {
            Vector3 direction = ResolveLockedCameraGunAimDirection(camera);
            if (!IsFinite(direction) || direction.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            ray = new Ray(camera.transform.position, direction.normalized);
            return true;
        }

        Rect pixelRect = camera.pixelRect;
        if (pixelRect.width <= 1f || pixelRect.height <= 1f)
        {
            return false;
        }

        float x = Mathf.Clamp(mousePosition.x, pixelRect.xMin + 0.5f, pixelRect.xMax - 0.5f);
        float y = Mathf.Clamp(mousePosition.y, pixelRect.yMin + 0.5f, pixelRect.yMax - 0.5f);
        ray = camera.ScreenPointToRay(new Vector3(x, y, 0f));
        return IsFinite(ray.origin) && IsFinite(ray.direction) && ray.direction.sqrMagnitude > 0.001f;
    }

    private Vector3 ResolveLockedCameraGunAimDirection(Camera camera)
    {
        Vector3 direction = camera != null ? camera.transform.forward : Vector3.zero;
        if (!IsFinite(direction) || direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }

    private static bool IsLockedGameplayAim()
    {
        return WildWindSessionCameraController.GameplayCursorLockedForMouseLook &&
            !WildWindControlSettings.IsGameplayCursorReleasePressed();
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
            fireMode = ShipGunFireMode.Automatic,
            localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
            barrelsPerSalvo = 1,
            roundsPerMinute = MainGunRoundsPerMinute,
            yawSpeedDegPerSecond = 45f,
            elevationUpSpeedDegPerSecond = 22f,
            elevationDownSpeedDegPerSecond = 30f,
            minElevationDegrees = MainGunMinElevationDegrees,
            maxElevationDegrees = MainGunMaxElevationDegrees,
            fireAlignmentToleranceDegrees = 1.5f,
            maxRangeMeters = 2500f,
            muzzleVelocityMS = 360f,
            projectileMassKg = 12f,
            projectileRadiusMeters = 0.10f,
            horizontalSpreadAtMaxRangeMeters = 24f,
            verticalSpreadAtMaxRangeMeters = 9f,
            automaticTargetsDamageableShips = true,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 76 мм",
                shellType = DamageShellType.ArmorPiercing,
                damageType = CoreTacticalDamageType.Kinetic,
                resistanceIgnorePercent = 24f,
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
            minElevationDegrees = SecondaryGunMinElevationDegrees,
            maxElevationDegrees = SecondaryGunMaxElevationDegrees,
            fireAlignmentToleranceDegrees = 3f,
            maxRangeMeters = 1500f,
            muzzleVelocityMS = 420f,
            projectileMassKg = 0.12f,
            projectileRadiusMeters = 0.035f,
            horizontalSpreadAtMaxRangeMeters = 35f,
            verticalSpreadAtMaxRangeMeters = 16f,
            automaticTargetsDamageableShips = true,
            shell = new DamageShellPreset
            {
                displayNameRu = "ББ 20 мм",
                shellType = DamageShellType.ArmorPiercing,
                damageType = CoreTacticalDamageType.Kinetic,
                resistanceIgnorePercent = 4f,
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

    public void StabilizeForFlightStart(bool holdCurrentAltitude)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        ClearStrategicInput();
        if (holdCurrentAltitude && rb != null)
        {
            strategicAltitudeHold = true;
            strategicTargetAltitude = rb.position.y;
        }

        SyncStrategicMotorSettings();
    }

    void FixedUpdate()
    {
        UpdateStrategicPhysicsFrame();
    }

    private void UpdateStrategicPhysicsFrame()
    {
        EnsureStrategicMotor();
        EnforceYawOnlyRotation(false);
        UpdateClaudiumSlipstream(Time.fixedDeltaTime);
        UpdateFuelConsumption(Time.fixedDeltaTime);
        SyncStrategicMotorSettings();
        ApplyStrategicControlBridge();
        EnforceYawOnlyRotation(false);
    }

    private void SyncStrategicMotorSettings()
    {
        CoreTacticalShipMotor motor = EnsureStrategicMotor();
        if (motor == null)
        {
            return;
        }

        float totalMass = GetTotalMassKg();
        float cleanSpeed = CleanBaseMaxSpeedMS;
        float currentMaxSpeed = CurrentMaxSpeedMS;
        float accelerationFromHull = totalMass > 0.001f
            ? Mathf.Max(0.25f, HullThrustCapacityKgf * 9.81f / totalMass)
            : 1f;

        motor.massKg = totalMass;
        motor.maxForwardSpeedMS = Mathf.Max(1f, currentMaxSpeed);
        motor.maxReverseSpeedMS = Mathf.Max(1f, cleanSpeed * 0.20f);
        motor.maxLateralSpeedMS = Mathf.Max(0.5f, cleanSpeed * 0.12f);
        motor.forwardAccelerationMS2 = Mathf.Max(0.25f, accelerationFromHull);
        motor.lateralAccelerationMS2 = Mathf.Max(0.25f, motor.forwardAccelerationMS2 * 0.45f);
        motor.brakingAccelerationMS2 = Mathf.Max(1f, motor.forwardAccelerationMS2 * 1.4f);
        motor.ascentSpeedMS = Mathf.Max(0.5f, strategicVerticalSpeedMS);
        motor.descentSpeedMS = Mathf.Max(0.5f, strategicVerticalSpeedMS);
        motor.verticalAccelerationMS2 = Mathf.Max(0.5f, strategicVerticalAccelerationMS2);
        motor.maxYawRateDegPerSecond = ResolveStrategicYawRateDeg();
        motor.yawAccelerationDegPerSecond2 = Mathf.Max(5f, strategicYawAccelerationDegPerSecond2);

        if (rb != null)
        {
            rb.mass = totalMass;
            rb.useGravity = false;
        }
    }

    private float ResolveStrategicYawRateDeg()
    {
        return strategicYawRateDegPerSecond > 0.001f ? strategicYawRateDegPerSecond : 32f;
    }

    private void ApplyStrategicControlBridge()
    {
        CoreTacticalShipMotor motor = EnsureStrategicMotor();
        if (motor == null || rb == null || !HasStrategicInput())
        {
            return;
        }

        Vector3 position = rb.position;
        Vector3 currentForward = FlattenHorizontal(transform.forward);
        if (currentForward.sqrMagnitude < 0.001f)
        {
            currentForward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        }

        currentForward.Normalize();
        Vector3 currentRight = FlattenHorizontal(transform.right);
        if (currentRight.sqrMagnitude < 0.001f)
        {
            currentRight = Vector3.Cross(Vector3.up, currentForward);
        }

        currentRight.Normalize();

        Vector3 desiredForward = ResolveStrategicDesiredForward(currentForward);
        float speedRatio = ResolveStrategicSpeedRatio();
        bool stopCommand = strategicStopCommand
            && Mathf.Abs(speedRatio) <= 0.001f
            && Mathf.Abs(strategicLateralInput) <= 0.001f
            && Mathf.Abs(strategicVerticalInput) <= 0.001f;

        float horizonSeconds = Mathf.Clamp(4f + Mathf.Abs(speedRatio) * 5f, 4f, 9f);
        Vector3 targetPosition = position;
        if (!stopCommand)
        {
            targetPosition += currentForward * (speedRatio * motor.maxForwardSpeedMS * horizonSeconds);
            targetPosition += currentRight * (Mathf.Clamp(strategicLateralInput, -1f, 1f) * motor.maxLateralSpeedMS * horizonSeconds);
        }

        if (strategicAltitudeHold)
        {
            targetPosition.y = Mathf.Max(0f, strategicTargetAltitude);
        }
        else
        {
            targetPosition.y += Mathf.Clamp(strategicVerticalInput, -1f, 1f) * motor.ascentSpeedMS * horizonSeconds;
        }

        bool holdFacing = Mathf.Abs(strategicLateralInput) > 0.001f || Mathf.Abs(strategicTurnInput) > 0.001f || strategicHeadingHold;
        motor.SetCommand(targetPosition, desiredForward, holdFacing);
    }

    private bool HasStrategicInput()
    {
        return strategicSpeedHold
            || strategicHeadingHold
            || strategicAltitudeHold
            || strategicStopCommand
            || Mathf.Abs(strategicForwardInput) > 0.001f
            || Mathf.Abs(strategicLateralInput) > 0.001f
            || Mathf.Abs(strategicVerticalInput) > 0.001f
            || Mathf.Abs(strategicTurnInput) > 0.001f;
    }

    private Vector3 ResolveStrategicDesiredForward(Vector3 fallbackForward)
    {
        if (strategicHeadingHold)
        {
            return Quaternion.Euler(0f, strategicTargetHeading, 0f) * Vector3.forward;
        }

        float turn = Mathf.Clamp(strategicTurnInput, -1f, 1f);
        if (Mathf.Abs(turn) > 0.001f)
        {
            float turnDegrees = turn * ResolveStrategicYawRateDeg() * 2.0f;
            return Quaternion.AngleAxis(turnDegrees, Vector3.up) * fallbackForward;
        }

        return fallbackForward;
    }

    private float ResolveStrategicSpeedRatio()
    {
        if (strategicSpeedHold)
        {
            return Mathf.Clamp(
                strategicTargetSpeedMS / Mathf.Max(1f, CurrentMaxSpeedMS),
                -1f,
                1f);
        }

        return Mathf.Clamp(strategicForwardInput, -1f, 1f);
    }

    private void UpdateClaudiumSlipstream(float deltaSeconds)
    {
        if (claudiumSlipstreamEnabled && !CanActivateClaudiumSlipstream)
        {
            claudiumSlipstreamEnabled = false;
        }

        float target = claudiumSlipstreamEnabled ? 1f : 0f;
        float step = slipstreamMaxRampSeconds > 0f
            ? Mathf.Max(0f, deltaSeconds) / slipstreamMaxRampSeconds
            : 1f;
        claudiumSlipstreamCharge01 = Mathf.MoveTowards(
            Mathf.Clamp01(claudiumSlipstreamCharge01),
            target,
            step);
        currentMaxSpeedMS = CalculateCurrentMaxSpeedMS();
    }

    private float ResolveCleanBaseMaxSpeedMS()
    {
        float configuredSpeed = baseMaxSpeedMS > 0f ? baseMaxSpeedMS : hullCruiseReferenceSpeedMS;
        return Mathf.Max(1f, configuredSpeed);
    }

    private float CalculateCurrentMaxSpeedMS()
    {
        return CalculateMaxSpeedMS(ClaudiumSlipstreamCharge01);
    }

    private float CalculateMaxSpeedMS(float slipstreamCharge01)
    {
        float cleanSpeed = ResolveCleanBaseMaxSpeedMS();
        float loadMultiplier = Mathf.Max(0f, loadSpeedMultiplier);
        float damageMultiplier = Mathf.Max(0f, damageSpeedMultiplier);
        float slipMultiplier = Mathf.Lerp(
            1f,
            Mathf.Max(1f, slipstreamMaxSpeedMultiplier),
            Mathf.Clamp01(slipstreamCharge01));
        return Mathf.Max(1f, cleanSpeed * loadMultiplier * damageMultiplier * slipMultiplier);
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

    private static float NormalizeHeadingDegrees(float value)
    {
        value %= 360f;
        if (value < 0f)
        {
            value += 360f;
        }

        return value;
    }

    private void UpdateFuelConsumption(float deltaSeconds)
    {
        hasFuel = fuelStockKg > 0f || fuelConsumptionKgPerMinute <= 0f;
        fuelConsumptionKgPerSecond = Mathf.Max(0f, fuelConsumptionKgPerMinute)
            * CurrentFuelConsumptionMultiplier
            / 60f;
        float requestedFuel = fuelConsumptionKgPerSecond * Mathf.Max(deltaSeconds, 0.0001f);
        if (requestedFuel <= 0f)
        {
            return;
        }

        if (fuelStockKg >= requestedFuel)
        {
            fuelStockKg -= requestedFuel;
            return;
        }

        float availableFraction = requestedFuel > 0f ? Mathf.Clamp01(fuelStockKg / requestedFuel) : 0f;
        fuelStockKg = 0f;
        fuelConsumptionKgPerSecond *= availableFraction;
        hasFuel = false;
    }

    private MetaGameState ResolveMetaGameState()
    {
        if (cachedMetaGameState == null)
        {
            cachedMetaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return cachedMetaGameState;
    }

}
