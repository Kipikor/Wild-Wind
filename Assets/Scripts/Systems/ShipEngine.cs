using UnityEngine;

public class ShipEngine : MonoBehaviour
{
    [Header("Настройки двигателя")]
    public string engineName = "Стандартный паровой";
    public float maxPower = 1000f;       // Максимальная мощность (л.с. или у.е.)
    public float responsiveness = 0.5f;   // Насколько быстро движок набирает обороты
    [Range(0, 1)] public float startingRPM = 0.5f; // Обороты при старте игры
    
    [Header("Текущее состояние")]
    [Range(0, 1)] public float throttle; // Подача топлива (от игрока)
    public float currentRPM;             // Текущие обороты (0..1)
    
    // Итоговая мощность, которую выдает движок в данный момент
    public float GetPowerOutput()
    {
        return currentRPM * maxPower;
    }

    void Start()
    {
        // Устанавливаем обороты и газ на стартовое значение
        currentRPM = startingRPM;
        throttle = startingRPM;
    }

    void Update()
    {
        // Плавный набор и сброс оборотов (инерция двигателя)
        currentRPM = Mathf.Lerp(currentRPM, throttle, Time.deltaTime * responsiveness);
    }
}
