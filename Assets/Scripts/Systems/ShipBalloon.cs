using UnityEngine;

public class ShipBalloon : MonoBehaviour
{
    [Header("Геометрия баллона")]
    public string названиеБаллона = "Стандартный баллон";
    public float диаметрМ = 8.0f;          // Диаметр (м)
    public float длинаМ = 12.0f;           // Общая длина (м)
    
    [Header("Справочно (вычисляется)")]
    public float лобоваяПроекцияМ2 = 0f;   // Площадь сечения
    public float объемМ3 = 0f;             // Итоговый объем
    public const float ПОДЪЕМ_НА_КУБ = 5.0f; // Константа: 5 кг на 1 м3
    
    [Header("Состояние газа")]
    public float текущийГазКг = 0f;        // Масса газа (кг)
    public float процентЗаполнения = 0f;   
    public float утечкаМ3вЧас = 1.0f;      // Потеря объема газа в час
    public bool valveOpen = false;         // Открыт ли клапан сброса
    public float valveFlowRate = 0.5f;     // Скорость сброса (кг/сек)
    
    public float GetStaticLiftNewtons()
    {
        // 1г газа = 1кг подъема. Значит 1кг газа = 1000кг подъема.
        return текущийГазКг * 1000f * 9.81f;
    }

    private void OnValidate()
    {
        CalculateGeometry();
    }

    public void CalculateGeometry()
    {
        // 1. Клэмпим процент перед расчетами
        процентЗаполнения = Mathf.Clamp(процентЗаполнения, 0f, 100f);

        float радиус = диаметрМ * 0.5f;
        
        // Лобовая проекция - площадь круга
        лобоваяПроекцияМ2 = Mathf.PI * (радиус * радиус);
        
        // Объем капсулы = Цилиндр + Сфера
        // Длина цилиндрической части (не может быть меньше 0)
        float цилиндрДлина = Mathf.Max(0, длинаМ - диаметрМ);
        
        float объемЦилиндра = лобоваяПроекцияМ2 * цилиндрДлина;
        float объемСферы = (4f / 3f) * Mathf.PI * Mathf.Pow(радиус, 3);
        
        объемМ3 = объемЦилиндра + объемСферы;

        // ВАЖНО: При изменении геометрии мы сохраняем ПРОЦЕНТ заполнения,
        // поэтому пересчитываем массу газа (текущийГазКг) под новый объем.
        float maxGasKg = (объемМ3 * ПОДЪЕМ_НА_КУБ) / 1000f;
        текущийГазКг = (процентЗаполнения / 100f) * maxGasKg;
    }

    void FixedUpdate()
    {
        // 1. Утечка (уменьшает массу газа)
        if (текущийГазКг > 0)
        {
            float gasDensityKgPerM3 = ПОДЪЕМ_НА_КУБ / 1000f;
            float leakKgPerHour = утечкаМ3вЧас * gasDensityKgPerM3;
            
            float leakStep = (leakKgPerHour / 3600f) * Time.fixedDeltaTime;
            текущийГазКг = Mathf.Max(0, текущийГазКг - leakStep);
        }
        
        // 1.1 Активный сброс (Клапан)
        if (valveOpen && текущийГазКг > 0)
        {
            текущийГазКг = Mathf.Max(0, текущийГазКг - valveFlowRate * Time.fixedDeltaTime);
        }

        // 2. Лимиты (Жесткий ограничитель массы)
        float maxGasKg = (объемМ3 * ПОДЪЕМ_НА_КУБ) / 1000f;
        текущийГазКг = Mathf.Clamp(текущийГазКг, 0, maxGasKg);

        // 3. Обновляем процент заполнения
        if (maxGasKg > 0) 
            процентЗаполнения = (текущийГазКг / maxGasKg) * 100f;
        else 
            процентЗаполнения = 0f;
    }
}
