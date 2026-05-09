using UnityEngine;

public class ShipEngine : MonoBehaviour
{
    [Header("Настройки двигателя")]
    public string engineName = "Стандартный двигатель";
    public float maxPower = 60f; // Теперь это Лошадиные Силы (л.с.)
    public float maxRPM = 2500f; // Максимальные обороты двигателя
    public float responsiveness = 0.5f; // Инерция (0 - вялый, 1 - резкий)
    [Range(0, 1.2f)] public float startingRPM = 0.5f; 

    [Header("Настройки автомата шага винта")]
    public float targetRPM = 0.8f;   // Обороты, которые хочет пилот
    public float currentLoad = 0f;  // Текущая нагрузка на валу (0..1)
    public bool isOverloaded = false;
    [Header("КПД и Тепло")]
    [Range(0.01f, 0.9f)] public float efficiency = 0.15f; // КПД (0.15 для пара)
    public float wasteHeatWatts = 0f;                     // Выделяемое тепло (Вт)
    public bool isClaudium = false; 

    [Header("Текущее состояние")]
    public float currentRPM = 0f;
    [HideInInspector] public float throttle = 0f; 

    void Start()
    {
        currentRPM = startingRPM;
        targetRPM = startingRPM;
    }

    void FixedUpdate()
    {
        // 1. Лимиты и константы
        float idleRPM = 0.1f; // Холостой ход (10%)
        float effectiveTarget = Mathf.Max(targetRPM, idleRPM);
        
        // 2. Рассчитываем доступный крутящий момент (Нормализованный)
        // Поскольку нагрузка (currentLoad) нормализована от 0 до 1, крутящий момент тоже нормализован.
        float maxAvailableTorque = 1.0f; 
        
        // P-регулятор двигателя: чем сильнее просели обороты, тем больше газуем.
        // Увеличили коэффициент с 10 до 100, чтобы мотор жестче держал заданные обороты 
        // и не проседал до 85% под нагрузкой.
        float rpmDiff = effectiveTarget - currentRPM;
        float rawTorque = rpmDiff * responsiveness * 100f;
        float engineTorque = Mathf.Clamp(rawTorque, -maxAvailableTorque, maxAvailableTorque);
        
        // Мотор перегружен, если он хочет выдать больше, чем может
        isOverloaded = (rawTorque > maxAvailableTorque * 0.9f) && (currentRPM < effectiveTarget * 0.95f);
        
        // 3. Сопротивление (Внутреннее трение + Нагрузка)
        float friction = currentRPM * 0.05f; 
        float totalResistance = currentLoad + friction;
        
        // 4. Физика вала
        float inertia = 1f / Mathf.Max(responsiveness, 0.1f);
        float rpmChange = (engineTorque - totalResistance) / (inertia * 2f);
        
        currentRPM += rpmChange * Time.fixedDeltaTime;
        currentRPM = Mathf.Clamp(currentRPM, 0f, 1.2f);
        
        // Обновляем публичные переменные для UI и других систем
        throttle = currentRPM;

        // 5. Расчет бросового тепла (Вт)
        // Если КПД 15%, то 85% энергии идет в тепло.
        float mechanicalPowerWatts = GetPowerOutput() * 735.5f;
        float totalEnergyInputWatts = mechanicalPowerWatts / efficiency;
        wasteHeatWatts = totalEnergyInputWatts - mechanicalPowerWatts;
        
        // Даже на холостом ходу котел греется
        if (currentRPM < 0.15f) wasteHeatWatts += (maxPower * 735.5f * 0.1f); 
    }

    public float GetPowerOutput()
    {
        // Честный расчет текущей отдачи (в л.с.)
        return maxPower * currentRPM;
    }
}
