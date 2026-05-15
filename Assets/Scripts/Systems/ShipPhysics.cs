using UnityEngine;
using UnityEngine.Serialization;

public struct RouteEtaInfo
{
    public bool hasTarget;
    public bool routeEnabled;
    public bool canEstimate;
    public int waypointIndex;
    public int waypointCount;
    public Vector3 targetPosition;
    public float horizontalDistance;
    public float horizontalRemaining;
    public float verticalError;
    public float verticalRemaining;
    public float horizontalSpeed;
    public float horizontalClosingSpeed;
    public float verticalSpeed;
    public float verticalClosingSpeed;
    public float etaSeconds;
    public string status;
}

[RequireComponent(typeof(Rigidbody))]
public class ShipPhysics : MonoBehaviour
{
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
    public string engineFuelId = "wood";
    [Tooltip("Доля энергии топлива, которая превращается в полезную мощность двигателя.")]
    public float engineFuelEfficiency = 0.32f;
    [HideInInspector] public float engineFuelEnergyKwhPerKg = 4f;
    [HideInInspector] public float engineFuelStockKg = 0f;
    
    [Header("Параметры винта")]
    [Tooltip("Скорость, после которой винт больше не может разгонять корабль, м/с.")]
    public float propellerMaxSpeedMS = 30f;
    [Tooltip("Доля мощности двигателя, которая превращается в полезную тягу винта.")]
    public float propellerEfficiency = 0.8f;
    [Tooltip("Максимальная статическая тяга винта, кгс.")]
    public float propellerMaxThrustKgf = 220f;
    
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
    [HideInInspector] public float claudiumCurrentLiftN;
    [HideInInspector] public float claudiumPowerDrawWatts;
    [HideInInspector] public float claudiumPowerDrawKw;
    [HideInInspector] public float claudiumRequestedLiftKg;

    [Header("Харвестеринг")]
    [Tooltip("Включает установленный газовый харвестер. Модуль работает циклами и кладет целые кг концентрата в общий груз корабля.")]
    public bool gasHarvesterEnabled;
    [Tooltip("Сколько кубометров облака всасывается и конденсируется в секунду.")]
    public float gasHarvesterVolumeM3PerSecond;
    [Tooltip("Сколько мощности харвестер забирает после клавдиевого контура и до винта.")]
    public float gasHarvesterPowerDrawKw;
    [Tooltip("Радиус забора. Добыча возможна, если этот радиус пересекается с облаком.")]
    public float gasHarvesterRadiusMeters;
    [Tooltip("Длительность одного цикла добычи. Контакт с облаком проверяется в начале и в конце цикла.")]
    public float gasHarvesterCycleSeconds = 5f;
    [HideInInspector] public float gasHarvesterPowerDrawActualKw;
    [HideInInspector] public float gasHarvesterCycleProgressSeconds;
    [HideInInspector] public float gasHarvesterBufferKg;
    [HideInInspector] public string gasHarvesterActiveCloudId = "";
    [HideInInspector] public string gasHarvesterLastMessage = "";

    [Header("Майнинг")]
    [Tooltip("Противоударный кузов ловит падающие куски. Пойманная руда складывается в общий груз корабля.")]
    public float miningImpactHoldCapacityKg;
    [Tooltip("Радиус сбора падающих кусков вокруг корабля.")]
    public float miningCatchRadiusMeters = 10f;
    [Tooltip("Дальность временной кнопки выстрела по глыбе.")]
    public float miningManualShotRangeMeters = 180f;
    [HideInInspector] public string miningLastMessage = "";
    
    [Header("Гироскопический поворот")]
    public float gyroTurnTorque = 12000f; // Максимальный внутренний момент поворота корпуса, Н*м
    public float gyroTurnDamping = 0.8f; // Демпфирование, которое гасит лишнюю угловую скорость
    [HideInInspector] public float currentGyroTurnTorque = 0f;

    [Header("Аэродинамика")]
    public float airDensity = 1.225f; // Плотность воздуха (1.225 на уровне моря)
    public float dragCoefficient = 1.1f; // Коэффициент формы Cd (1.1 для контейнера)
    public float frontalArea = 6.3f; // Лобовая площадь (кв.м)
    public float sideResistance = 2.0f; // Сопротивление боковому сносу (эффект киля)
    public float verticalAreaFactor = 4.0f; // Во сколько раз площадь "пуза" больше лобовой площади
    
    [Header("Лимиты скорости подъема")]
    public float maxStructuralVerticalSpeed = 5.0f; // Предел прочности (конструкционный)
    public float maxAutoVerticalSpeed = 1.0f;        // Лимит автопилота
    
    [Header("Окружающая среда")]
    public Vector3 windVelocity = Vector3.zero; // Глобальный вектор ветра (м/с)

    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * frontalArea;

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
    
    [Header("Путевая машина")]
    public bool routeEnabled = false;
    public System.Collections.Generic.List<Vector3> waypoints = new System.Collections.Generic.List<Vector3>();
    public float waypointRadius = 10f; // Радиус засчитывания точки
    public float routeArrivalSpeedMS = 1f; // Точка засчитывается только если горизонтальная скорость ниже этого порога
    public float routeBrakeAccelerationMS2 = 2.5f; // Расчетное торможение для раннего сброса скорости перед точкой
    [HideInInspector] public int currentWaypointIndex = 0;

    [Header("Удержание позиции")]
    public bool positionHold = false;
    public Vector3 targetHoldPosition = Vector3.zero;
    public float positionHoldRadius = 5f;
    public float positionHoldMaxSpeedMS = 8f;
    public float positionHoldStiffness = 0.35f;
    public float positionHoldDamping = 0.9f;
    
    [Header("Настройки тяги винта")]
    public float propellerPitch = 0f;    // Текущее задание тяги (-1..1)
    public float speedStiffness = 0.8f;  // Насколько активно круиз меняет тягу
    public float speedDamping = 0.3f;    // Демпфирование тяги

    [Header("Текущее управление (для чтения/записи из интерфейса)")]
    [HideInInspector] public float thrustInput; // -1 полный реверс, 0 нет тяги, 1 полный ход вперед
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float targetTrimMass = 1000f; // Масса для триммирования (кг)
    [HideInInspector] public float liftInput;   // -1 вниз, 1 вверх (Точная доводка +-10%)
    
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
    [HideInInspector] public float propellerInputPowerKw;
    [HideInInspector] public float propellerUsefulPowerKw;
    [HideInInspector] public float propellerCalculatedEfficiency;
    [HideInInspector] public float propellerThrustKgf;
    private bool wasAltitudeHold = false;
    private float altIntegral = 0f; // Память автопилота (I-терм)
    private bool routeWasEnabled = false;
    private bool routePreviousAltitudeHold = false;
    private bool routePreviousCruiseControl = false;
    private bool routePreviousHeadingHold = false;
    private bool routePreviousPositionHold = false;
    private bool positionHoldWasEnabled = false;
    private string gasHarvesterCycleCloudId = "";
    private MetaGameState cachedMetaGameState;
    
    // Единая ручка управления мощностью (Обороты для CSU / Газ для Manual)
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = GetTotalMassKg();
        rb.useGravity = true;
        
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
        }
    }

    public float GetTotalMassKg()
    {
        return Mathf.Max(1f, baseMass + Mathf.Max(0f, cargoMassKg));
    }

    public bool TryCollectMiningFragment(string oreItemId, int amountKg, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(oreItemId) || amountKg <= 0)
        {
            return true;
        }

        if (miningImpactHoldCapacityKg <= 0f)
        {
            reason = "На корабле нет противоударного кузова.";
            miningLastMessage = reason;
            return false;
        }

        MetaGameState meta = ResolveMetaGameState();
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
            StopRouteForFullMiningHold();
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

    private void StopRouteForFullMiningHold()
    {
        if (!routeEnabled) return;

        routeEnabled = false;
        routeWasEnabled = false;
        targetSpeedMS = 0f;
        thrustInput = 0f;
        turnInput = 0f;

        positionHold = true;
        positionHoldWasEnabled = true;
        targetHoldPosition = transform.position;

        altitudeHold = true;
        if (rb != null)
        {
            targetAltitude = rb.position.y;
        }

        cruiseControl = true;
        headingHold = true;
        miningLastMessage += " Маршрут остановлен, удерживаю позицию.";
    }

    public RouteEtaInfo GetCurrentRouteEta()
    {
        RouteEtaInfo info = new RouteEtaInfo
        {
            routeEnabled = routeEnabled,
            etaSeconds = float.PositiveInfinity,
            status = routeEnabled ? "Путевая машина активна." : "Путевая машина выключена, оценка по текущей скорости."
        };

        if (waypoints == null || waypoints.Count == 0)
        {
            info.status = "Точки маршрута не заданы.";
            return info;
        }

        info.waypointCount = waypoints.Count;
        if (currentWaypointIndex >= waypoints.Count)
        {
            info.status = "Маршрут завершен.";
            return info;
        }

        int targetIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
        Rigidbody body = rb != null ? rb : GetComponent<Rigidbody>();
        if (body == null)
        {
            info.status = "Нет Rigidbody для расчета ETA.";
            return info;
        }

        Vector3 target = waypoints[targetIndex];
        Vector3 toTarget = target - transform.position;
        Vector3 horizontalToTarget = FlattenHorizontal(toTarget);
        Vector3 horizontalVelocity = FlattenHorizontal(body.linearVelocity);
        float horizontalDistance = horizontalToTarget.magnitude;
        float verticalError = toTarget.y;
        float verticalTolerance = Mathf.Max(1f, waypointRadius * 0.5f);

        info.hasTarget = true;
        info.waypointIndex = targetIndex;
        info.targetPosition = target;
        info.horizontalDistance = horizontalDistance;
        info.horizontalRemaining = Mathf.Max(0f, horizontalDistance - Mathf.Max(0.1f, waypointRadius));
        info.verticalError = verticalError;
        info.verticalRemaining = Mathf.Max(0f, Mathf.Abs(verticalError) - verticalTolerance);
        info.horizontalSpeed = horizontalVelocity.magnitude;
        info.verticalSpeed = body.linearVelocity.y;

        if (horizontalDistance > 0.001f)
        {
            info.horizontalClosingSpeed = Vector3.Dot(horizontalVelocity, horizontalToTarget / horizontalDistance);
        }

        if (info.verticalRemaining > 0f)
        {
            info.verticalClosingSpeed = body.linearVelocity.y * Mathf.Sign(verticalError);
        }

        float horizontalEta = 0f;
        if (info.horizontalRemaining > 0f)
        {
            horizontalEta = info.horizontalClosingSpeed > 0.05f
                ? info.horizontalRemaining / info.horizontalClosingSpeed
                : float.PositiveInfinity;
        }

        float verticalEta = 0f;
        if (info.verticalRemaining > 0f)
        {
            verticalEta = info.verticalClosingSpeed > 0.05f
                ? info.verticalRemaining / info.verticalClosingSpeed
                : float.PositiveInfinity;
        }

        info.etaSeconds = Mathf.Max(horizontalEta, verticalEta);
        info.canEstimate = !float.IsInfinity(info.etaSeconds) && !float.IsNaN(info.etaSeconds);

        if (info.horizontalRemaining <= 0f && info.verticalRemaining <= 0f)
        {
            info.canEstimate = true;
            info.etaSeconds = 0f;
            info.status = "Корабль уже в зоне текущей точки; осталось погасить скорость для засчитывания.";
        }
        else if (!info.canEstimate)
        {
            info.status = "Нет устойчивой оценки: текущая скорость не ведет к точке по одной из осей.";
        }

        return info;
    }

    void Start()
    {
        if (autoStabilizeAtStart)
        {
            PerformAutoStabilization();
        }
        else
        {
            UpdateEngineThrottles();
        }
    }

    private void PerformAutoStabilization()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        targetTrimMass = rb != null ? rb.mass : baseMass;
        if (altitudeHold && rb != null)
        {
            targetAltitude = rb.position.y;
        }

        currentGasLift = 0f;
        activeLiftForce = claudiumCurrentLiftN;
        UpdateEngineThrottles();
    }

    public void StabilizeForFlightStart(bool holdCurrentAltitude)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (holdCurrentAltitude && rb != null)
        {
            altitudeHold = true;
            targetAltitude = rb.position.y;
        }

        thrustInput = 0f;
        turnInput = 0f;
        liftInput = 0f;
        PerformAutoStabilization();
    }

    void FixedUpdate()
    {
        UpdateRouteModeState();
        UpdateWaypointNavigation(); // Мастер-автопилот
        UpdateRouteModeState();
        UpdatePositionHold();       // Удержание координат, если маршрут не активен
        UpdateCruiseControl();      // Круиз-контроль (скорость)
        UpdateEngineThrottles();
        UpdateHeadingAutopilot(); // Автопилот курса
        UpdateClaudium(); // Магия Клавдия
        UpdateGasHarvester();
        
        // --- АЭРОДИНАМИКА (с учетом ветра) ---
        Vector3 airVelocity = rb.linearVelocity - windVelocity;
        float airspeed = airVelocity.magnitude;
        
        float aeroMultiplier = 1.0f;
        float currentDrag = CurrentAeroDrag * aeroMultiplier;
        
        if (airspeed > 0.01f)
        {
            Vector3 dragForce = -airVelocity.normalized * (airspeed * airspeed) * currentDrag;
            rb.AddForce(dragForce, ForceMode.Force);
        }

        // --- ПОДЪЕМНАЯ СИЛА ---
        float totalLift = claudiumCurrentLiftN;
        activeLiftForce = totalLift;
        currentGasLift = 0f;
        rb.AddForce(Vector3.up * totalLift, ForceMode.Force);

        ApplyPropellerThrust();

        ApplyGyroTurn();

        // 4. Подавление бокового сноса (Киль сопротивляется воздуху)
        Vector3 localAirVel = transform.InverseTransformDirection(airVelocity);
        Vector3 sideAirVelocity = transform.right * localAirVel.x;
        rb.AddForce(-sideAirVelocity * rb.mass * sideResistance, ForceMode.Force);
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
        rb.AddTorque(transform.up * finalTorque, ForceMode.Force);
    }

    private void ApplyPropellerThrust()
    {
        float residualPowerKw = Mathf.Max(0f, engineGeneratedPowerKw - claudiumPowerDrawKw - gasHarvesterPowerDrawActualKw);
        float thrustDirection = Mathf.Sign(thrustInput);
        float propellerEngagement = Mathf.Clamp01(Mathf.Abs(thrustInput));

        propellerInputPowerKw = residualPowerKw * propellerEngagement;
        propellerCalculatedEfficiency = Mathf.Clamp01(propellerEfficiency);
        propellerUsefulPowerKw = propellerInputPowerKw * propellerCalculatedEfficiency;
        if (propellerMaxSpeedMS <= 0f
            || propellerMaxThrustKgf <= 0f
            || propellerUsefulPowerKw <= 0f
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
        Vector3 horizontalAirVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - windVelocity, Vector3.up);
        float signedAirspeedWithThrust = Vector3.Dot(horizontalAirVelocity, thrustAxis) * thrustDirection;
        float speedFactor = signedAirspeedWithThrust >= propellerMaxSpeedMS ? 0f : 1f;
        float maxThrustKgf = propellerMaxThrustKgf * propellerEngagement * speedFactor;
        float powerLimitedThrustN = propellerUsefulPowerKw * 1000f / Mathf.Max(1f, Mathf.Abs(signedAirspeedWithThrust));
        float powerLimitedThrustKgf = powerLimitedThrustN / 9.81f;
        float thrustKgf = Mathf.Min(maxThrustKgf, powerLimitedThrustKgf);

        propellerThrustKgf = thrustKgf * thrustDirection;
        rb.AddForce(thrustAxis * (propellerThrustKgf * 9.81f), ForceMode.Force);
    }

    private void UpdateRouteModeState()
    {
        if (routeEnabled && !routeWasEnabled)
        {
            if (waypoints != null && waypoints.Count > 0)
            {
                if (currentWaypointIndex < 0 || currentWaypointIndex >= waypoints.Count)
                {
                    currentWaypointIndex = 0;
                }
            }

            routePreviousAltitudeHold = altitudeHold;
            routePreviousCruiseControl = cruiseControl;
            routePreviousHeadingHold = headingHold;
            routePreviousPositionHold = positionHold;
            positionHold = false;
            routeWasEnabled = true;
        }
        else if (!routeEnabled && routeWasEnabled)
        {
            altitudeHold = routePreviousAltitudeHold;
            cruiseControl = routePreviousCruiseControl;
            headingHold = routePreviousHeadingHold;
            positionHold = routePreviousPositionHold;
            positionHoldWasEnabled = false;
            targetSpeedMS = 0f;
            turnInput = 0f;
            routeWasEnabled = false;
        }
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
        if (cruiseControl)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            }

            Vector3 horizontalVelocity = rb.linearVelocity;
            horizontalVelocity.y = 0f;
            float currentSpeed = Vector3.Dot(horizontalVelocity, forward.normalized);
            float speedError = targetSpeedMS - currentSpeed;
            
            // Жесткость (P-терм)
            float desiredOutput = speedError * speedStiffness;
            desiredOutput = Mathf.Clamp(desiredOutput, -1f, 1f);
            
            float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, speedDamping) * 6f * Time.fixedDeltaTime);
            thrustInput = Mathf.Lerp(thrustInput, desiredOutput, response);
            enginePowerLever = Mathf.Lerp(enginePowerLever, Mathf.Clamp01(Mathf.Abs(desiredOutput)), response);
        }
    }

    private void UpdateWaypointNavigation()
    {
        if (!routeEnabled || waypoints == null || waypoints.Count == 0) return;

        if (currentWaypointIndex >= waypoints.Count)
        {
            CompleteRouteWithPositionHold(waypoints[waypoints.Count - 1]);
            return;
        }

        // Путевая машина берет на себя все системы.
        altitudeHold = true;
        cruiseControl = true;
        headingHold = true;

        Vector3 currentTarget = waypoints[currentWaypointIndex];
        float verticalError = currentTarget.y - transform.position.y;
        Vector3 horizontalError = FlattenHorizontal(currentTarget - transform.position);
        Vector3 horizontalVelocity = FlattenHorizontal(rb.linearVelocity);
        float horizDist = horizontalError.magnitude;
        float horizontalSpeed = horizontalVelocity.magnitude;
        float verticalTolerance = Mathf.Max(1f, waypointRadius * 0.5f);
        float arrivalSpeed = Mathf.Max(0.25f, routeArrivalSpeedMS);
        float arrivalVerticalSpeed = Mathf.Max(0.5f, maxAutoVerticalSpeed);

        bool horizontalStable = horizDist <= waypointRadius && horizontalSpeed <= arrivalSpeed;
        bool verticalStable = Mathf.Abs(verticalError) <= verticalTolerance && Mathf.Abs(rb.linearVelocity.y) <= arrivalVerticalSpeed;
        bool reached = horizontalStable && verticalStable;

        if (reached)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                CompleteRouteWithPositionHold(currentTarget);
                return;
            }
            currentTarget = waypoints[currentWaypointIndex];
            verticalError = currentTarget.y - transform.position.y;
            horizontalError = FlattenHorizontal(currentTarget - transform.position);
            horizDist = horizontalError.magnitude;
        }

        // 1. Задаем высоту.
        targetAltitude = currentTarget.y;

        if (horizDist <= waypointRadius)
        {
            ApplyPositionHoldHorizontalControl(currentTarget, waypointRadius, Mathf.Min(propellerMaxSpeedMS, positionHoldMaxSpeedMS));
            return;
        }

        ApplyRouteHorizontalControl(currentTarget);
    }

    private void CompleteRouteWithPositionHold(Vector3 holdPosition)
    {
        routeEnabled = false;
        routeWasEnabled = false;
        currentWaypointIndex = waypoints != null ? waypoints.Count : currentWaypointIndex;

        altitudeHold = true;
        cruiseControl = true;
        headingHold = true;
        positionHold = true;
        positionHoldWasEnabled = true;

        targetHoldPosition = holdPosition;
        targetAltitude = holdPosition.y;
        targetSpeedMS = 0f;
        thrustInput = 0f;
        turnInput = 0f;
    }

    private void UpdatePositionHold()
    {
        if (routeEnabled) return;

        if (!positionHold)
        {
            positionHoldWasEnabled = false;
            return;
        }

        if (!positionHoldWasEnabled)
        {
            targetHoldPosition = transform.position;
            positionHoldWasEnabled = true;
        }

        cruiseControl = true;
        headingHold = true;
        ApplyPositionHoldHorizontalControl(targetHoldPosition, positionHoldRadius, positionHoldMaxSpeedMS);
    }

    private void ApplyRouteHorizontalControl(Vector3 target)
    {
        Vector3 horizontalError = FlattenHorizontal(target - transform.position);
        float distance = horizontalError.magnitude;
        if (distance <= 0.1f)
        {
            ApplyPositionHoldHorizontalControl(target, waypointRadius, positionHoldMaxSpeedMS);
            return;
        }

        Vector3 directionToTarget = horizontalError / distance;
        float maxSpeed = Mathf.Max(0f, propellerMaxSpeedMS);
        float stopDistance = Mathf.Max(0f, distance - waypointRadius);
        float brakeAcceleration = EstimateRouteBrakeAcceleration();
        float desiredSpeed = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * brakeAcceleration * stopDistance));

        Vector3 desiredGroundVelocity = directionToTarget * desiredSpeed;
        Vector3 requiredAirVelocity = desiredGroundVelocity - FlattenHorizontal(windVelocity);
        Vector3 headingVector = requiredAirVelocity.sqrMagnitude > 0.04f ? requiredAirVelocity : directionToTarget;

        targetHeading = HeadingFromVector(headingVector);

        float headingError = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading));
        desiredSpeed *= CalculateRouteHeadingSpeedFactor(headingError);
        targetSpeedMS = Mathf.Clamp(desiredSpeed, 0f, maxSpeed);
    }

    private float EstimateRouteBrakeAcceleration()
    {
        float configuredAcceleration = Mathf.Max(0.25f, routeBrakeAccelerationMS2);
        if (rb == null || propellerMaxThrustKgf <= 0f || propellerEfficiency <= 0f)
        {
            return configuredAcceleration;
        }

        float mass = Mathf.Max(1f, rb.mass);
        float staticThrustN = propellerMaxThrustKgf * 9.81f;
        float staticAcceleration = staticThrustN / mass;

        float liftPowerKw = altitudeHold ? CalculateClaudiumPowerKwForLift(mass) : 0f;
        float modulePowerKw = gasHarvesterEnabled ? Mathf.Max(0f, gasHarvesterPowerDrawKw) : 0f;
        float residualPowerKw = Mathf.Max(0f, enginePowerKwAt100 - liftPowerKw - modulePowerKw);
        float usefulPowerW = residualPowerKw * Mathf.Clamp01(propellerEfficiency) * 1000f;
        float horizontalAirspeed = FlattenHorizontal(rb.linearVelocity - windVelocity).magnitude;
        float powerLimitedThrustN = usefulPowerW > 0f ? usefulPowerW / Mathf.Max(1f, horizontalAirspeed) : 0f;
        float estimatedThrustN = Mathf.Min(staticThrustN, powerLimitedThrustN);
        float estimatedAcceleration = estimatedThrustN / mass;

        // РљРѕСЌС„С„РёС†РёРµРЅС‚ Р·Р°РїР°СЃР° РЅСѓР¶РµРЅ, РїРѕС‚РѕРјСѓ С‡С‚Рѕ РєРѕСЂР°Р±Р»СЊ РµС‰Рµ РґРѕРІРѕСЂР°С‡РёРІР°РµС‚ РЅРѕСЃ РґР»СЏ СЂРµРІРµСЂСЃР°.
        float safeAcceleration = Mathf.Max(0.25f, Mathf.Min(staticAcceleration, estimatedAcceleration) * 0.6f);
        return Mathf.Min(configuredAcceleration, safeAcceleration);
    }

    private void ApplyPositionHoldHorizontalControl(Vector3 target, float radius, float maxSpeed)
    {
        Vector3 horizontalError = FlattenHorizontal(target - transform.position);
        Vector3 horizontalVelocity = FlattenHorizontal(rb.linearVelocity);
        float distance = horizontalError.magnitude;
        float speed = horizontalVelocity.magnitude;
        float holdRadius = Mathf.Max(0.1f, radius);
        float maxHoldSpeed = Mathf.Max(0f, maxSpeed);
        float brakeAcceleration = EstimateRouteBrakeAcceleration();
        float stoppingDistance = speed * speed / Mathf.Max(0.5f, 2f * brakeAcceleration);
        float remainingToHold = Mathf.Max(0f, distance - holdRadius);

        Vector3 headingVector = Vector3.zero;
        float desiredSpeed = 0f;

        if (speed > 0.15f && (distance <= holdRadius || stoppingDistance >= remainingToHold))
        {
            headingVector = -horizontalVelocity;
        }
        else
        {
            Vector3 correctionVelocity = horizontalError * Mathf.Max(0f, positionHoldStiffness)
                - horizontalVelocity * Mathf.Max(0f, positionHoldDamping);
            float approachSpeedLimit = Mathf.Sqrt(2f * brakeAcceleration * remainingToHold);
            correctionVelocity = Vector3.ClampMagnitude(correctionVelocity, Mathf.Min(maxHoldSpeed, approachSpeedLimit));
            headingVector = correctionVelocity;
            desiredSpeed = correctionVelocity.magnitude;
        }

        if (headingVector.sqrMagnitude < 0.04f)
        {
            if (horizontalVelocity.sqrMagnitude > 0.04f)
            {
                headingVector = -horizontalVelocity;
            }
            else
            {
                Vector3 windHorizontal = FlattenHorizontal(windVelocity);
                if (windHorizontal.sqrMagnitude > 0.04f)
                {
                    headingVector = -windHorizontal;
                }
            }
        }

        if (headingVector.sqrMagnitude > 0.04f)
        {
            targetHeading = HeadingFromVector(headingVector);
        }

        if (distance <= holdRadius && speed < 0.35f)
        {
            desiredSpeed = 0f;
        }

        float headingError = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading));
        desiredSpeed *= CalculateRouteHeadingSpeedFactor(headingError);
        targetSpeedMS = Mathf.Clamp(desiredSpeed, 0f, Mathf.Max(0f, maxSpeed));
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

    private float CalculateRouteHeadingSpeedFactor(float headingErrorDeg)
    {
        float normalized = 1f - Mathf.Clamp01(Mathf.Abs(headingErrorDeg) / 45f);
        return Mathf.SmoothStep(0f, 1f, normalized);
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

        float smoothing = claudiumLiftSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-claudiumLiftSmoothing * dt);
        claudiumCurrentLiftN = Mathf.Lerp(claudiumCurrentLiftN, targetLiftN, smoothing);
        if (claudiumCurrentLiftN < 0.001f)
        {
            claudiumCurrentLiftN = 0f;
        }

        claudiumRequestedLiftKg = requestedLiftKg;
        claudiumPowerDrawKw = claudiumLiftEfficiency > 0f ? CalculateClaudiumPowerKwForLift(claudiumCurrentLiftN / 9.81f) : 0f;
        claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;

        float supportedTons = Mathf.Max(0f, claudiumCurrentLiftN / 9.81f) / 1000f;
        float consumption = claudiumConsumptionPerTonSecond * supportedTons * dt;
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
        if (!gasHarvesterEnabled || gasHarvesterVolumeM3PerSecond <= 0f || gasHarvesterPowerDrawKw <= 0f)
        {
            return 0f;
        }

        if (claudiumPowerKw + gasHarvesterPowerDrawKw > enginePowerKwAt100 + 0.001f)
        {
            return 0f;
        }

        return gasHarvesterPowerDrawKw;
    }

    private float CalculateClaudiumPowerKwForLift(float liftKg)
    {
        if (claudiumLiftEfficiency <= 0f) return 0f;
        return Mathf.Max(0f, liftKg) / claudiumLiftEfficiency;
    }

    private void UpdateEnginePowerOutput(float minimumPowerKw)
    {
        float maxLever = 1.2f;
        engineMinimumPowerLever = enginePowerKwAt100 > 0f ? Mathf.Clamp(minimumPowerKw / enginePowerKwAt100, 0f, maxLever) : 0f;
        enginePowerLever = Mathf.Clamp(enginePowerLever, engineMinimumPowerLever, maxLever);

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
                targetAltitude = rb.position.y;
                wasAltitudeHold = true;
                altIntegral = 0f;
            }

            targetAltitude += liftInput * 8.0f * Time.fixedDeltaTime;

            float currentAcceleration = (claudiumCurrentLiftN / Mathf.Max(mass, 1f)) - 9.81f;
            float lookAheadTime = 1.2f;
            float predictedHeight = rb.position.y + rb.linearVelocity.y * lookAheadTime + 0.5f * currentAcceleration * lookAheadTime * lookAheadTime;
            float altitudeError = targetAltitude - predictedHeight;

            if (Mathf.Abs(altitudeError) > altDriftTolerance)
            {
                altIntegral += (targetAltitude - rb.position.y) * Time.fixedDeltaTime * 0.05f;
                altIntegral = Mathf.Clamp(altIntegral, -0.25f, 0.25f);
            }

            float targetVerticalSpeed = 0f;
            if (Mathf.Abs(altitudeError) > 0.001f)
            {
                targetVerticalSpeed = Mathf.Sqrt(2f * 0.25f * Mathf.Abs(altitudeError)) * Mathf.Sign(altitudeError);
                targetVerticalSpeed = Mathf.Clamp(targetVerticalSpeed, -maxAutoVerticalSpeed, maxAutoVerticalSpeed);
            }

            float velocityError = targetVerticalSpeed - rb.linearVelocity.y;
            float desiredAcceleration = velocityError * altDamping - currentAcceleration * 1.5f + altIntegral + liftInput * 0.1f;
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
        return Mathf.Max(0f, trimMass * (1f + liftInput * 0.1f));
    }

    void UpdateClaudium()
    {
        UpdateSimplifiedClaudium();
    }

    private void UpdateGasHarvester()
    {
        gasHarvesterPowerDrawActualKw = 0f;

        if (!gasHarvesterEnabled)
        {
            ResetGasHarvesterCycle();
            return;
        }

        if (gasHarvesterVolumeM3PerSecond <= 0f || gasHarvesterRadiusMeters <= 0f || gasHarvesterPowerDrawKw <= 0f)
        {
            gasHarvesterLastMessage = "Харвестер не установлен или не имеет рабочих характеристик.";
            ResetGasHarvesterCycle();
            return;
        }

        if (claudiumPowerDrawKw + gasHarvesterPowerDrawKw > enginePowerKwAt100 + 0.001f)
        {
            gasHarvesterLastMessage = "Харвестер выключен: после клавдиевого контура не хватает мощности до лимита 100%.";
            ResetGasHarvesterCycle();
            return;
        }

        if (engineGeneratedPowerKw + 0.001f < claudiumPowerDrawKw + gasHarvesterPowerDrawKw || !engineHasFuel)
        {
            gasHarvesterLastMessage = "Харвестер ждет мощность или топливо.";
            ResetGasHarvesterCycle();
            return;
        }

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            gasHarvesterLastMessage = "Харвестер ждет MetaGameState.";
            ResetGasHarvesterCycle();
            return;
        }

        if (meta.GetRemainingShipCargoCapacityKg() < 1f)
        {
            gasHarvesterEnabled = false;
            gasHarvesterLastMessage = "Харвестер выключен: не осталось грузоподъемности.";
            ResetGasHarvesterCycle();
            return;
        }

        if (string.IsNullOrWhiteSpace(gasHarvesterCycleCloudId))
        {
            GasCloud startCloud = GasCloud.FindRandomOverlapping(transform.position, gasHarvesterRadiusMeters);
            if (startCloud == null)
            {
                gasHarvesterLastMessage = "Нет облака в радиусе харвестера.";
                ResetGasHarvesterCycle();
                return;
            }

            gasHarvesterCycleCloudId = startCloud.cloudId;
            gasHarvesterActiveCloudId = startCloud.cloudId;
            gasHarvesterCycleProgressSeconds = 0f;
            gasHarvesterLastMessage = "Цикл начат: " + startCloud.displayName;
        }

        gasHarvesterPowerDrawActualKw = gasHarvesterPowerDrawKw;
        gasHarvesterCycleProgressSeconds += Time.fixedDeltaTime;
        float cycleSeconds = Mathf.Max(0.1f, gasHarvesterCycleSeconds);
        if (gasHarvesterCycleProgressSeconds < cycleSeconds)
        {
            return;
        }

        GasCloud endCloud = GasCloud.FindById(gasHarvesterCycleCloudId);
        if (endCloud == null || !endCloud.IntersectsHarvestRadius(transform.position, gasHarvesterRadiusMeters))
        {
            gasHarvesterLastMessage = "Цикл сорван: в конце цикла нет контакта с выбранным облаком.";
            ResetGasHarvesterCycle();
            return;
        }

        float sampledCubicMeters = gasHarvesterVolumeM3PerSecond * cycleSeconds;
        float possibleLiters = Mathf.Min(sampledCubicMeters * Mathf.Max(0.0001f, endCloud.condensateLitersPerCubicMeter), endCloud.remainingVolumeLiters);
        int possibleWholeKg = Mathf.FloorToInt(gasHarvesterBufferKg + possibleLiters + 0.0001f);
        if (possibleWholeKg > 0 && meta.GetRemainingShipCargoCapacityKg() < possibleWholeKg)
        {
            gasHarvesterEnabled = false;
            gasHarvesterLastMessage = "Харвестер выключен: не хватит места для результата цикла.";
            ResetGasHarvesterCycle();
            return;
        }

        float harvestedLiters = endCloud.HarvestLiters(sampledCubicMeters);
        gasHarvesterBufferKg += harvestedLiters;
        int wholeKg = Mathf.FloorToInt(gasHarvesterBufferKg + 0.0001f);
        if (wholeKg > 0)
        {
            if (meta.TryAddShipCargoFromRuntime(endCloud.condensateItemId, wholeKg, out string cargoError))
            {
                gasHarvesterBufferKg -= wholeKg;
                gasHarvesterLastMessage = $"Добыто {wholeKg} кг: {endCloud.condensateItemId}. Остаток облака {endCloud.remainingVolumeLiters:F1} кг.";
            }
            else
            {
                gasHarvesterEnabled = false;
                gasHarvesterLastMessage = cargoError;
            }
        }
        else
        {
            gasHarvesterLastMessage = $"Буфер {gasHarvesterBufferKg:F2} кг. Остаток облака {endCloud.remainingVolumeLiters:F1} кг.";
        }

        ResetGasHarvesterCycle();
    }

    private void ResetGasHarvesterCycle()
    {
        gasHarvesterCycleCloudId = "";
        gasHarvesterActiveCloudId = "";
        gasHarvesterCycleProgressSeconds = 0f;
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
        enginePowerLever = Mathf.Clamp(enginePowerLever, 0f, 1.2f);
        propellerPitch = Mathf.Clamp(thrustInput, -1f, 1f);
    }

    private void OnDrawGizmos()
    {
        // Визуализация ветра
        if (windVelocity.sqrMagnitude > 0.1f)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.7f); // Пурпурный
            Vector3 startPos = transform.position + Vector3.up * 10f; // Чуть выше корабля
            Gizmos.DrawLine(startPos, startPos + windVelocity);
            Gizmos.DrawWireSphere(startPos + windVelocity, 1f); // Наконечник
        }

        if (positionHold)
        {
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.6f);
            Gizmos.DrawWireSphere(targetHoldPosition, Mathf.Max(0.1f, positionHoldRadius));
        }

        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Полупрозрачный голубой
        for (int i = 0; i < waypoints.Count; i++)
        {
            // Рисуем сферу (радиус достижения точки)
            Gizmos.DrawWireSphere(waypoints[i], waypointRadius);
            
            // Соединяем точки линией
            if (i > 0)
            {
                Gizmos.DrawLine(waypoints[i - 1], waypoints[i]);
            }
            else
            {
                // Линия от текущей позиции корабля к первой точке
                Gizmos.DrawLine(transform.position, waypoints[i]);
            }
        }

        // Подсвечиваем текущую цель желтым, если маршрут активен
        if (Application.isPlaying && routeEnabled && currentWaypointIndex < waypoints.Count)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(waypoints[currentWaypointIndex], waypointRadius);
            Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex]);
        }
    }
}
