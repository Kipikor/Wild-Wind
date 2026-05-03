using UnityEngine;

public class ShipEngine : MonoBehaviour
{
    [Header("Настройки двигателя")]
    public string engineName = "Стандартный двигатель";
    public float maxPower = 1000f;       // Максимальная мощность
    public float responsiveness = 0.5f;   // Приемистость (насколько быстро набирает обороты)
    [Range(0, 1.2f)] public float startingRPM = 0.5f; 

    [Header("Настройки CSU (Constant Speed Unit)")]
    public float targetRPM = 0.8f;   // Обороты, которые хочет пилот
    public float currentLoad = 0f;  // Текущая нагрузка на валу (0..1)
    public bool isClaudium = false; // Клавдиевые системы не задыхаются от нагрузки

    [Header("Текущее состояние")]
    public float currentRPM = 0f;
    [HideInInspector] public float throttle = 0f; // Сохраняем для совместимости

    void Start()
    {
        currentRPM = startingRPM;
        targetRPM = startingRPM;
    }

    void FixedUpdate()
    {
        // 1. Считаем крутящий момент мотора
        // Если обороты падают ниже цели, мотор сопротивляется в 5 раз сильнее
        float error = targetRPM - currentRPM;
        float dynamicResponsiveness = error > 0 ? responsiveness * 5f : responsiveness;
        float torque = error * dynamicResponsiveness;

        // 2. Считаем сопротивление нагрузки
        // Обычный двигатель теряет обороты от нагрузки. Клавдиевый - в 4 раза меньше.
        float loadResistance = isClaudium ? (currentLoad * 0.05f) : (currentLoad * 0.2f);

        // 3. Изменяем обороты (инерция)
        currentRPM += (torque - loadResistance) * Time.fixedDeltaTime;
        
        // Обороты не могут быть отрицательными
        currentRPM = Mathf.Max(currentRPM, 0f);
        
        // Для совместимости с другими скриптами
        throttle = currentRPM;
    }

    public float GetPowerOutput()
    {
        // Мощность = Макс_Мощь * Текущие_Обороты
        return maxPower * currentRPM;
    }
}
