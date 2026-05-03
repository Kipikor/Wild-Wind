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
    public float altStiffness = 0.5f; // Насколько жестко держим высоту (P)
    public float altDamping = 1.5f;   // Насколько сильно гасим раскачку (D)

    [Header("Текущее управление (для чтения/записи из UI)")]
    [HideInInspector] public float thrustInput; // -1 назад, 1 вперед
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float targetTrimMass = 1000f; // Масса для триммирования (кг)
    [HideInInspector] public float liftInput;   // -1 вниз, 1 вверх (Точная доводка +-10%)

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

        // 2. Тяга маршевого винта
        if (thrustEngine != null)
        {
            float thrustDir = Mathf.Sign(thrustInput);
            if (thrustInput == 0) thrustDir = 0;

            rb.AddForce(transform.forward * (thrustDir * thrustEngine.GetPowerOutput() * thrustEfficiency * g), ForceMode.Force);
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

    private void UpdateEngineThrottles()
    {
        // 1. Управление двигателем подъема
        if (liftEngine != null)
        {
            float maxLiftCapacity = liftEngine.maxPower * liftEfficiency;
            float baseThrottle = 0f;
            if (maxLiftCapacity > 0)
            {
                float clampedTrimMass = Mathf.Clamp(targetTrimMass, 0, maxLiftCapacity * 0.9f);
                baseThrottle = clampedTrimMass / maxLiftCapacity;
            }

            float inputMod = 0f;

            if (altitudeHold)
            {
                // Если только что включили - запоминаем текущую высоту
                if (!wasAltitudeHold)
                {
                    targetAltitude = rb.position.y;
                    wasAltitudeHold = true;
                }

                // Рычаг подъема теперь меняет целевую высоту (5 метров в секунду)
                targetAltitude += liftInput * 5.0f * Time.fixedDeltaTime;

                // PID: Ошибка высоты + гашение вертикальной скорости
                float altError = targetAltitude - rb.position.y;
                float vVel = rb.linearVelocity.y;
                
                inputMod = (altError * altStiffness) - (vVel * altDamping);
                // Ограничиваем влияние автопилота (+-20% мощности), чтобы не шел вразнос
                inputMod = Mathf.Clamp(inputMod, -0.2f, 0.2f);
            }
            else
            {
                wasAltitudeHold = false;
                inputMod = liftInput * 0.1f;

                // ГУВЕРНЁР (работает только в ручном режиме)
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

            float targetLiftThrottle = Mathf.Clamp01(baseThrottle + inputMod);

            targetLiftThrottle = Mathf.Clamp01(targetLiftThrottle);

            if (liftEngine == thrustEngine)
            {
                liftEngine.throttle = Mathf.Max(targetLiftThrottle, Mathf.Abs(thrustInput));
            }
            else
            {
                liftEngine.throttle = targetLiftThrottle;
            }
        }

        // 2. Управление двигателем тяги
        if (thrustEngine != null && thrustEngine != liftEngine)
        {
            thrustEngine.throttle = Mathf.Abs(thrustInput);
        }
    }
}
