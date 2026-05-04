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
    
    [Header("Параметры винта")]
    public float propellerDiameter = 4.5f; // Диаметр винта в метрах
    public float propellerEfficiency = 0.8f; // КПД винта (0.7 - 0.85)
    public float propellerMaxPitchMeters = 3.0f; // На сколько метров вкручивается винт за 1 оборот при 100% шаге
    
    [Header("Эффективность систем")]
    public float liftEfficiency = 20f;   // Сколько силы дает 1 единица мощности контура
    public float turnTorque = 500f;      // Сила поворота
    
    [Header("Аэродинамика")]
    public float airDensity = 1.225f; // Плотность воздуха (1.225 на уровне моря)
    public float dragCoefficient = 1.1f; // Коэффициент формы Cd (1.1 для контейнера)
    public float frontalArea = 6.3f; // Лобовая площадь (кв.м)
    public float sideResistance = 2.0f; // Сопротивление боковому сносу (эффект киля)
    public float verticalAreaFactor = 4.0f; // Во сколько раз площадь "пуза" больше лобовой площади
    
    [Header("Лимиты скорости подъема")]
    public float maxStructuralVerticalSpeed = 5.0f; // Предел прочности (конструкционный)
    public float maxAutoVerticalSpeed = 1.0f;        // Лимит автопилота
    
    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * frontalArea;

    [Header("Автопилот и Системы")]
    public bool hasCSU = false;       // Есть ли автомат шага винта (Governor)
    public bool autoStabilizeAtStart = true; // Новая галочка
    public bool altitudeHold = false;
    public float targetAltitude = 0f;
    public float altStiffness = 0.2f; // P (Было 0.5 - слишком резко)
    public float altDamping = 1.2f;   // D
    public float altDriftTolerance = 0.15f; // Допуск дрейфа (м)

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
    
    // ==========================================
    // МОДУЛИ (Дочерние объекты)
    // ==========================================
    [Header("Установленные модули")]
    public ShipBalloon balloonModule;      // Ссылка на шар
    public ShipClaudiumLoop claudiumLoop;  // Ссылка на контур
    
    [HideInInspector] public float currentGasLift; 
    [HideInInspector] public float activeLiftForce; 
    
    [HideInInspector] public float targetMainEngineRPM = 0.8f; 
    private bool wasAltitudeHold = false;
    private float altIntegral = 0f; // Память автопилота (I-терм)
    
    // Единая ручка управления мощностью (Обороты для CSU / Газ для Manual)
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
        
        // 1. Устанавливаем триммер по текущей массе
        targetTrimMass = rb.mass;
        
        // 2. Рассчитываем, сколько силы нам не хватает (Масса - Баллон)
        float balloonLiftN = (balloonModule != null && balloonModule.gameObject.activeSelf) ? balloonModule.GetStaticLiftNewtons() : 0f;
        float neededLiftN = (rb.mass * 9.81f) - balloonLiftN;
        
        if (neededLiftN > 0 && claudiumLoop != null && liftEngine != null)
        {
            // 3. Какая скорость потока v нужна для этой силы?
            // Сила = АктивКлавдий * v * Длина * 17
            float массаВКонтуре = claudiumLoop.объемСистемыЛ * claudiumLoop.плотностьРаствора;
            float активныйКлавдийКг = массаВКонтуре * (claudiumLoop.концентрацияКлавдия / 100f);
            float kLift = активныйКлавдийКг * claudiumLoop.длинаКонтураМ * 17.0f;
            
            float targetV = neededLiftN / Mathf.Max(kLift, 1f);
            claudiumLoop.скоростьПотока_мс = targetV;
            
            // 4. Какие обороты двигателя нужны, чтобы поддерживать эту скорость (F_friction = F_pump)
            // Friction_Power = Friction_Force * v
            float трениеОСпейсН = 2.6f * активныйКлавдийКг * (targetV * targetV) * claudiumLoop.длинаКонтураМ;
            float трениеГидроН = 0.5f * массаВКонтуре * (targetV * targetV) * (1f / claudiumLoop.длинаКонтураМ);
            float totalFrictionN = трениеОСпейсН + трениеГидроН;
            
            float requiredPowerWatts = (totalFrictionN * targetV) / claudiumLoop.кпдКонтура;
            
            // P_engine = maxPower * 735.5 * RPM^3 => RPM = (P / P_max)^(1/3)
            float maxPowerWatts = liftEngine.maxPower * 735.5f;
            float targetRPM = Mathf.Pow(requiredPowerWatts / Mathf.Max(maxPowerWatts, 1f), 1f/3f);
            
            liftEngine.startingRPM = targetRPM;
            liftEngine.currentRPM = targetRPM;
            liftEngine.targetRPM = targetRPM;
        }
        else if (liftEngine != null)
        {
            liftEngine.startingRPM = 0.1f;
            liftEngine.currentRPM = 0.1f;
            liftEngine.targetRPM = 0.1f;
        }

        UpdateEngineThrottles();
    }

    void FixedUpdate()
    {
        UpdateEngineThrottles();
        UpdateClaudium(); // Магия Клавдия
        
        // --- АЭРОДИНАМИКА ---
        float aeroMultiplier = (balloonModule != null && balloonModule.gameObject.activeSelf) ? 5.0f : 1.0f;
        float currentDrag = CurrentAeroDrag * aeroMultiplier;
        
        float speed = rb.linearVelocity.magnitude;
        Vector3 dragForce = -rb.linearVelocity * speed * currentDrag;
        rb.AddForce(dragForce, ForceMode.Force);

        // --- ПОДЪЕМНАЯ СИЛА ---
        float totalLift = 0f;
        if (balloonModule != null && balloonModule.gameObject.activeSelf) 
            totalLift += balloonModule.GetStaticLiftNewtons();
        
        // НОВАЯ ЛОГИКА: Сила от Клавдия теперь берется напрямую из контура
        if (claudiumLoop != null && claudiumLoop.gameObject.activeSelf)
        {
            totalLift += claudiumLoop.создаваемаяСилаН;
        }
        
        rb.AddForce(Vector3.up * totalLift, ForceMode.Force);

        // 2. Тяга маршевого винта (ВРШ) - ИМПУЛЬСНАЯ ТЕОРИЯ
        if (thrustEngine != null)
        {
            float pWatts = thrustEngine.maxPower * 735.5f * thrustEngine.currentRPM;
            float discArea = Mathf.PI * Mathf.Pow(propellerDiameter * 0.5f, 2);
            
            float rpm = thrustEngine.currentRPM;
            // Нормализуем нагрузку винта (0..1), чтобы автомат шага (CSU) работал корректно. 
            // При шаге 1.0 и RPM 1.0 винт должен потреблять 100% мощности.
            float normalizedLoad = Mathf.Abs(propellerPitch) * (rpm * rpm);

            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            
            float propRevPerSec = (thrustEngine.currentRPM * thrustEngine.maxRPM) / 60f;
            float screwSpeed = propRevPerSec * (propellerPitch * propellerMaxPitchMeters);
            
            float speedUnloading = 0f;
            if (Mathf.Abs(screwSpeed) > 0.1f) {
                speedUnloading = Mathf.Clamp01(forwardSpeed / screwSpeed);
            }
            
            // Нагрузка падает при наборе скорости (винт разгружается)
            thrustEngine.currentLoad = normalizedLoad * (1f - speedUnloading * 0.5f) + 0.05f;

            // --- УМНЫЙ АВТОМАТ ШАГА (CSU / Governor) ---
            if (hasCSU)
            {
                // Регулятор теперь следит за НАГРУЗКОЙ
                // Задача: держать двигатель на 100% мощности (Load = 1.0)
                float loadError = thrustEngine.currentLoad - 1.0f;
                float governorSpeed = 0.5f * Time.fixedDeltaTime * 15f; 
                
                if (loadError > 0.02f) // Перегруз - облегчаем винт
                    propellerPitch -= governorSpeed;
                else if (loadError < -0.01f) // Есть запас мощи - увеличиваем шаг
                    propellerPitch += governorSpeed;

                // Ограничиваем шаг направлением ввода игрока
                if (thrustInput >= 0)
                    propellerPitch = Mathf.Clamp(propellerPitch, 0f, thrustInput);
                else
                    propellerPitch = Mathf.Clamp(propellerPitch, thrustInput, 0f);
            }
            else
            {
                propellerPitch = thrustInput;
            }

            float pWattsNominal = thrustEngine.maxPower * 735.5f;
            float maxStaticT = Mathf.Pow(2f * airDensity * discArea * (pWattsNominal * pWattsNominal), 1f/3f) * propellerEfficiency;

            float currentStaticT = maxStaticT * (thrustEngine.currentRPM * thrustEngine.currentRPM);
            
            // Доступная мощность маршевого двигателя (для сохранения энергии P = F * v)
            float availableThrustPower = pWattsNominal * propellerEfficiency * thrustEngine.currentRPM;

            float thrustFactor = 0f;
            if (Mathf.Abs(screwSpeed) > 0.01f) {
                thrustFactor = (screwSpeed > 0) ? (1f - forwardSpeed / screwSpeed) : (-1f + forwardSpeed / screwSpeed);
            }
            
            // Рассчитываем идеальную тягу из ограничения по мощности
            float powerLimitedThrust = currentStaticT;
            if (Mathf.Abs(forwardSpeed) > 0.5f)
            {
                powerLimitedThrust = availableThrustPower / Mathf.Abs(forwardSpeed);
            }

            // Итоговая тяга - минимум из статической и мощностной. 
            // Умножаем на thrustFactor для учета вырождения винта (шаг).
            float thrustForce = Mathf.Min(currentStaticT, powerLimitedThrust) * Mathf.Clamp(thrustFactor, -1.2f, 1.2f);
            
            rb.AddForce(transform.forward * thrustForce, ForceMode.Force);
        }

        // 3. Угловой момент для разворота (Аэродинамический руль)
        float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float propWash = 0f;
        if (thrustEngine != null)
        {
            float propRevPerSec = (thrustEngine.currentRPM * thrustEngine.maxRPM) / 60f;
            propWash = Mathf.Abs(propRevPerSec * propellerPitch * propellerMaxPitchMeters);
        }
        float effAirspeed = Mathf.Abs(fwdSpeed) + propWash * 0.5f;

        // 1. АКТИВНАЯ СИЛА (Рули)
        float activeTorque = turnInput * turnTorque * effAirspeed * 0.15f;
        
        // 2. ФИЗИЧЕСКОЕ ДЕМПФИРОВАНИЕ КОРПУСА (Связано с sideResistance)
        float rotationResistance = sideResistance * rb.mass * 0.15f; 
        float dampingTorque = -rb.angularVelocity.y * rotationResistance * (effAirspeed + 1.0f);
        
        float finalTorque = (activeTorque + dampingTorque) * 9.81f;
        
        // Ограничиваем момент для стабильности
        finalTorque = Mathf.Clamp(finalTorque, -turnTorque * 50f, turnTorque * 50f);
        rb.AddTorque(transform.up * finalTorque, ForceMode.Force);

        // 4. Подавление бокового сноса
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        Vector3 sideVelocity = transform.right * localVel.x;
        rb.AddForce(-sideVelocity * rb.mass * sideResistance, ForceMode.Force);
    }

    void UpdateClaudium()
    {
        // 1. Статика от баллона (для телеметрии)
        bool isBalloonActive = (balloonModule != null && balloonModule.gameObject.activeSelf);
        currentGasLift = isBalloonActive ? balloonModule.GetStaticLiftNewtons() : 0f;
        
        // 2. Работа контура
        bool isLoopActive = (claudiumLoop != null && claudiumLoop.gameObject.activeSelf);
        if (isLoopActive && liftEngine != null)
        {
            // Теперь мощность передается не линейно, а КУБИЧЕСКИ (как в реальных насосах)
            // P = P_max * (RPM/RPM_max)^3
            float rpmFactor = liftEngine.currentRPM;
            float engineOutputWatts = liftEngine.maxPower * 735.5f * (rpmFactor * rpmFactor * rpmFactor);
            
            // Передаем тепло от двигателя в контур
            claudiumLoop.теплоОтДвигателяВт = liftEngine.wasteHeatWatts;
            
            // Рассчитываем физику внутри контура (силы, трение, расход)
            claudiumLoop.UpdatePhysics(engineOutputWatts, Time.fixedDeltaTime);
            
            // Нагрузка на двигатель определяется тем, сколько мощности "съедает" трение в контуре
            float frictionPowerHP = claudiumLoop.GetRequiredPowerWatts() / 735.5f;
            liftEngine.currentLoad = frictionPowerHP / liftEngine.maxPower;
            
            activeLiftForce = claudiumLoop.создаваемаяСилаН;
        }
        else if (liftEngine != null)
        {
            liftEngine.currentLoad = 0f;
            activeLiftForce = 0f;
            
            // Если двигатель выключен, контур все равно должен обновлять физику (для инерции затухания)
            if (isLoopActive) claudiumLoop.UpdatePhysics(0, Time.fixedDeltaTime);
        }
    }

    private void UpdateEngineThrottles()
    {
        bool isLoopActive = (claudiumLoop != null && claudiumLoop.gameObject.activeSelf);

        // 1. Управление подъемной системой (Иерархическое)
        if (liftEngine != null)
        {
            float maxLiftCapacity = liftEngine.maxPower * liftEfficiency;
            float baseTargetRPM = 0f;
            if (maxLiftCapacity > 0)
            {
                // Триммирование только в плюс (никакой прижимной силы)
                float requiredActiveLiftKg = targetTrimMass - (currentGasLift / 9.81f);
                float clampedRequiredLift = Mathf.Clamp(requiredActiveLiftKg, 0, maxLiftCapacity * 0.9f);
                
                baseTargetRPM = clampedRequiredLift / maxLiftCapacity;
            }

            float inputMod = 0f;
            bool forceVenting = false;

            if (altitudeHold)
            {
                if (!wasAltitudeHold)
                {
                    targetAltitude = rb.position.y;
                    wasAltitudeHold = true;
                    altIntegral = 0f; // Сбрасываем память при включении
                }

                // 1. Плавное движение цели
                targetAltitude += liftInput * 8.0f * Time.fixedDeltaTime; 
                
                // 1.1 ПРОГНОЗ: Где мы будем через 1.2 сек?
                // Учитываем текущую скорость и ускорение, чтобы "увидеть будущее"
                float currentA = (activeLiftForce + currentGasLift) / rb.mass - 9.81f;
                float lookAheadTime = 1.2f; // Секунд прогноза
                float predictedHeight = rb.position.y + rb.linearVelocity.y * lookAheadTime + 0.5f * currentA * (lookAheadTime * lookAheadTime);
                
                float altError = targetAltitude - predictedHeight;
                float currentVVel = rb.linearVelocity.y;
                
                // 1.2 Интегратор (I-терм): Накапливаем ошибку, чтобы идеально выровнять вес
                if (Mathf.Abs(altError) > altDriftTolerance)
                {
                    altIntegral += (targetAltitude - rb.position.y) * Time.fixedDeltaTime * 0.05f;
                    altIntegral = Mathf.Clamp(altIntegral, -0.2f, 0.2f);
                }

                // 2. АЛГОРИТМ БЕЗ ПРОМАХА (На прогнозируемую высоту)
                float maxDecel = 0.25f; // Еще более осторожное торможение
                float targetVVel = 0f;
                if (Mathf.Abs(altError) > 0)
                {
                    targetVVel = Mathf.Sqrt(2f * maxDecel * Mathf.Abs(altError)) * Mathf.Sign(altError);
                    targetVVel = Mathf.Clamp(targetVVel, -maxAutoVerticalSpeed, maxAutoVerticalSpeed);
                }
                
                // 4. ИТОГОВЫЙ КОНТРОЛЬ
                float velocityError = targetVVel - currentVVel;
                float accelerationDamping = currentA * 2.0f; // Усилил гашение рывков
                
                inputMod = (velocityError * altDamping) - accelerationDamping + altIntegral + (liftInput * 0.1f);
                
                // Сброс газа при перелете
                if (velocityError < -0.2f && liftEngine.currentRPM < 0.2f) forceVenting = true;

                inputMod = Mathf.Clamp(inputMod, -0.6f, 0.6f);
            }
            else
            {
                wasAltitudeHold = false;
                altIntegral = 0f;
                inputMod = liftInput * 0.4f;
                if (liftInput < -0.9f) forceVenting = true;
            }

            // --- ГЛОБАЛЬНЫЙ КОНСТРУКЦИОННЫЙ ОГРАНИЧИТЕЛЬ ---
            // Если скорость уже выше предела, режем любой положительный ввод
            float vVel = rb.linearVelocity.y;
            float finalTargetRPM = baseTargetRPM + inputMod;
            
            if (vVel > maxStructuralVerticalSpeed * 0.9f)
            {
                // Плавное снижение тяги при приближении к физическому пределу
                float speedFactor = Mathf.InverseLerp(maxStructuralVerticalSpeed, maxStructuralVerticalSpeed * 0.9f, vVel);
                finalTargetRPM *= speedFactor;
            }

            // Устанавливаем обороты двигателя (минимум 10% в самом двигателе)
            liftEngine.targetRPM = Mathf.Clamp01(finalTargetRPM);

            if (isLoopActive)
            {
                // ЛОГИКА МУФТЫ СЦЕПЛЕНИЯ
                float requestedRPM = baseTargetRPM + inputMod;
                float clutchEngagement = 1.0f;
                
                // Если мы хотим меньше тяги, чем дает холостой ход (0.1), проскальзываем муфтой
                if (requestedRPM < 0.1f)
                {
                    clutchEngagement = Mathf.Clamp01(requestedRPM / 0.1f);
                }
                
                float rpmFactor = liftEngine.currentRPM;
                float enginePowerWatts = liftEngine.maxPower * 735.5f * (rpmFactor * rpmFactor * rpmFactor);
                
                // Насос получает мощность через муфту (проскальзывание)
                float pumpPowerWatts = enginePowerWatts * clutchEngagement;
                
                claudiumLoop.UpdatePhysics(pumpPowerWatts, Time.fixedDeltaTime);
                claudiumLoop.теплоОтДвигателяВт = liftEngine.wasteHeatWatts;
            }

            if (balloonModule != null)
            {
                // ТЕПЕРЬ УЧИТЫВАЕМ ИНЕРЦИЮ ЖИДКОСТИ:
                // Не сбрасываем газ, пока жидкость в трубах еще крутится и дает подъемную силу.
                bool flowStopped = (claudiumLoop == null) || (claudiumLoop.скоростьПотока_мс < 0.5f);
                
                // УМНЫЙ СБРОС: Газ сбрасываем ТОЛЬКО если:
                // 1. Двигатель на холостых.
                // 2. Жидкость в трубах ПОЧТИ ОСТАНОВИЛАСЬ.
                // 3. Баллон все еще перекачан выше веса корабля.
                bool balloonTooStrong = currentGasLift > (targetTrimMass * 9.81f * 1.02f);
                
                bool shouldVent = forceVenting && (liftEngine.currentRPM < 0.15f) && flowStopped && balloonTooStrong;
                balloonModule.valveOpen = shouldVent;
            }
        }

        // 2. Управление маршевым двигателем
        if (thrustEngine != null)
        {
            if (hasCSU)
            {
                // УМНЫЙ РЕЖИМ (CSU): Игрок задает целевые обороты
                thrustEngine.targetRPM = targetMainEngineRPM;
                
                // Автомат шага (CSU) сам управляет propellerPitch в FixedUpdate, 
                // опираясь на нагрузку (currentLoad). Здесь больше не нужно жестко его перезаписывать.
            }
            else
            {
                // РУЧНОЙ РЕЖИМ: Газ и Шаг раздельно (Газом управляет та же ручка, что и оборотами в CSU)
                thrustEngine.targetRPM = targetMainEngineRPM; 
                propellerPitch = thrustInput;             // Ручка шага напрямую
            }
        }
    }
}
