using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipPhysics : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Параметры корабля")]
    public float baseMass = 1000f; // Стартовая масса 1000кг
    
    [Header("Силовые установки")]
    public ShipEngine thrustEngine; // Двигатель для винта
    public ShipEngine liftEngine;   // Двигатель для подъема (контура)
    
    [Header("Эффективность систем")]
    public float thrustEfficiency = 15f; // Сколько силы дает 1 единица мощности винта
    public float liftEfficiency = 20f;   // Сколько силы дает 1 единица мощности контура
    public float turnTorque = 500f;      // Сила поворота (пока оставим как есть)
    
    [Header("Аэродинамика")]
    public float airDensity = 1.225f; // Плотность воздуха (1.225 на уровне моря)
    public float dragCoefficient = 1.1f; // Коэффициент формы Cd (1.1 для контейнера)
    public float frontalArea = 6.3f; // Лобовая площадь (кв.м)
    public float sideResistance = 2.0f; // Сопротивление боковому сносу (эффект киля)
    public float verticalAreaFactor = 4.0f; // Во сколько раз площадь "пуза" больше лобовой площади
    public float maxVerticalSpeed = 5.0f; // Максимальная комфортная скорость подъема (м/с)
    
    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * frontalArea;

    [Header("Автопилот")]
    public bool altitudeHold = false;
    public float targetAltitude = 0f;
    public float altStiffness = 0.5f; // P
    public float altDamping = 1.5f;   // D

    public bool cruiseControl = false;
    public float targetSpeedMS = 0f;
    public float maxCruiseSpeedMS = 20f;  // Лимит для автопилота (м/с)
    public float maxManualSpeedMS = 30f;  // Лимит для ручного режима (м/с)
    
    [Header("Настройки ВРШ (Шаг винта)")]
    public float propellerPitch = 0f;    // Текущий шаг (-1..1)
    public float speedStiffness = 0.8f;  // Насколько активно круиз меняет шаг винта
    public float speedDamping = 0.3f;    // Демпфирование шага

    [Header("Текущее управление (для чтения/записи из UI)")]
    [HideInInspector] public float thrustInput; // -1 назад, 1 вперед
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float targetTrimMass = 1000f; // Масса для триммирования (кг)
    [HideInInspector] public float liftInput;   // -1 вниз, 1 вверх (Точная доводка +-10%)
    
    // Новое: Целевые обороты маршевого двигателя (CSU)
    [HideInInspector] public float targetMainEngineRPM = 0.8f; 

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = baseMass;
        rb.useGravity = true;
        
        rb.angularDamping = 2f; 
        rb.linearDamping = 0f; 
    }

    void Start()
    {
        // Устанавливаем начальный газ сразу при старте, чтобы двигатели успели "прогреться"
        UpdateEngineThrottles();
    }

    void FixedUpdate()
    {
        // Перевод из килограмм-силы (кгс) в Ньютоны для Unity (1 кгс ≈ 9.81 Н)
        float g = Mathf.Abs(Physics.gravity.y);
        
        UpdateEngineThrottles();

        float speed = rb.linearVelocity.magnitude;

        // Проверяем, пытается ли игрок двигаться (есть ли ввод на рычагах)
        bool isTryingToMove = Mathf.Abs(thrustInput) > 0.01f || 
                             Mathf.Abs(liftInput) > 0.01f || 
                             Mathf.Abs(turnInput) > 0.01f;

        // Микро-стабилизация: включаем линейный демпфер только на малых скоростях
        // И ТОЛЬКО если игрок не пытается куда-то плыть.
        rb.linearDamping = (speed < 3.0f && !isTryingToMove) ? 0.5f : 0f;

        // 1. Подъемная сила
        if (liftEngine != null)
        {
            float forceMagnitude = liftEngine.GetPowerOutput() * liftEfficiency * g;
            rb.AddForce(transform.up * forceMagnitude, ForceMode.Force);
        }

        // 2. Тяга маршевого винта (ВРШ)
        if (thrustEngine != null)
        {
            // Нагрузка зависит от реального угла лопастей и оборотов
            thrustEngine.currentLoad = Mathf.Abs(propellerPitch) * thrustEngine.currentRPM;

            // Сила = Шаг * Мощность(от оборотов) * Эффективность
            float thrustForce = propellerPitch * thrustEngine.GetPowerOutput() * thrustEfficiency * g;
            rb.AddForce(transform.forward * thrustForce, ForceMode.Force);
        }

        // 3. Угловой момент для разворота влево/вправо (ось Y / up)
        if (turnInput != 0)
        {
            rb.AddTorque(transform.up * (turnInput * turnTorque * g), ForceMode.Force);
        }

        // 4. Реалистичное аэродинамическое сопротивление (квадратичное)
        // На скоростях > 3 м/с работает только оно.
        Vector3 dragForce = -rb.linearVelocity * rb.linearVelocity.magnitude * CurrentAeroDrag;
        rb.AddForce(dragForce, ForceMode.Force);

        // 5. Подавление бокового сноса (эффект киля)
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 sideVelocity = transform.right * localVel.x;
        rb.AddForce(-sideVelocity * rb.mass * sideResistance, ForceMode.Force);

        // 6. Вертикальное аэродинамическое сопротивление
        // Площадь дна = frontalArea * verticalAreaFactor
        float vVel = rb.linearVelocity.y;
        float vDragForce = -vVel * Mathf.Abs(vVel) * (CurrentAeroDrag * verticalAreaFactor);
        rb.AddForce(Vector3.up * vDragForce, ForceMode.Force);
    }

    private bool wasAltitudeHold = false;
    private float lastForwardSpeed = 0f; 

    private void UpdateEngineThrottles()
    {
        // 1. Управление двигателем подъема
        if (liftEngine != null)
        {
            // Для клавдиевого двигателя targetRPM - это давление/мощность
            float maxLiftCapacity = liftEngine.maxPower * liftEfficiency;
            float baseTargetRPM = 0f;
            if (maxLiftCapacity > 0)
            {
                float clampedTrimMass = Mathf.Clamp(targetTrimMass, 0, maxLiftCapacity * 0.9f);
                baseTargetRPM = clampedTrimMass / maxLiftCapacity;
            }

            float inputMod = 0f;

            if (altitudeHold)
            {
                if (!wasAltitudeHold)
                {
                    targetAltitude = rb.position.y;
                    wasAltitudeHold = true;
                }

                targetAltitude += liftInput * 10.0f * Time.fixedDeltaTime;
                float altError = targetAltitude - rb.position.y;
                float vVel = rb.linearVelocity.y;
                
                inputMod = (altError * altStiffness) - (vVel * altDamping);
                inputMod = Mathf.Clamp(inputMod, -0.2f, 0.2f);
            }
            else
            {
                wasAltitudeHold = false;
                inputMod = liftInput * 0.1f;

                // ГУВЕРНЁР (просто ограничиваем целевые обороты)
                float vVel = rb.linearVelocity.y;
                float speedExcess = Mathf.Abs(vVel) - maxVerticalSpeed;
                if (speedExcess > 0)
                {
                    float severity = speedExcess / maxVerticalSpeed;
                    float correction = severity * 0.5f; 
                    if (vVel > 0) inputMod -= correction;
                    else inputMod += correction;
                }
            }

            liftEngine.targetRPM = Mathf.Clamp01(baseTargetRPM + inputMod);
        }

        // 2. Управление маршевым двигателем (ВРШ)
        if (thrustEngine != null)
        {
            // Мотор всегда стремится к тем оборотам, что выставил пилот
            thrustEngine.targetRPM = targetMainEngineRPM;

            if (cruiseControl)
            {
                // Рычаг тяги меняет целевую скорость
                targetSpeedMS += thrustInput * 5.0f * Time.fixedDeltaTime;
                targetSpeedMS = Mathf.Clamp(targetSpeedMS, -maxCruiseSpeedMS, maxCruiseSpeedMS);

                // PD-регулятор для подбора ШАГА ВИНТА
                float currentForwardSpeedMS = Vector3.Dot(rb.linearVelocity, transform.forward);
                float speedError = targetSpeedMS - currentForwardSpeedMS;
                float acceleration = (currentForwardSpeedMS - lastForwardSpeed) / Time.fixedDeltaTime;
                lastForwardSpeed = currentForwardSpeedMS;

                float idealPitch = (speedError * speedStiffness) - (acceleration * speedDamping);
                propellerPitch = Mathf.Clamp(idealPitch, -1f, 1f);
            }
            else
            {
                lastForwardSpeed = 0f;
                // В ручном режиме рычаг напрямую задает ШАГ лопастей
                propellerPitch = thrustInput;

                // ЛИНЕЙНЫЙ ГУВЕРНЁР (Ограничиваем шаг, если летим слишком быстро)
                float currentForwardSpeedMS = Vector3.Dot(rb.linearVelocity, transform.forward);
                float speedExcess = Mathf.Abs(currentForwardSpeedMS) - maxManualSpeedMS;
                
                if (speedExcess > 0)
                {
                    float correction = speedExcess * 0.5f; 
                    if (currentForwardSpeedMS > 0) propellerPitch = Mathf.Max(0, propellerPitch - correction);
                    else propellerPitch = Mathf.Min(0, propellerPitch + correction);
                }
            }

            // ==========================================
            // ЗАЩИТА ОТ "УДУШЬЯ" (АППАРАТНЫЙ CSU)
            // ==========================================
            // Если мотор не справляется и обороты падают ниже целевых:
            float rpmRatio = thrustEngine.currentRPM / Mathf.Max(thrustEngine.targetRPM, 0.05f);
            if (rpmRatio < 0.95f)
            {
                // Плавно, но жестко уменьшаем максимальный доступный шаг
                // Если обороты упали до 50% от цели - шаг ограничивается нулем!
                float maxSafePitch = Mathf.Lerp(0f, 1f, (rpmRatio - 0.5f) / 0.45f);
                maxSafePitch = Mathf.Clamp01(maxSafePitch);
                
                // Принудительно "схлопываем" лопасти
                propellerPitch = Mathf.Clamp(propellerPitch, -maxSafePitch, maxSafePitch);
            }
        }
    }
}
