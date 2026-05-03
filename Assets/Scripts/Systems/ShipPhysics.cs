using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipPhysics : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Параметры корабля")]
    public float baseMass = 1000f; // Стартовая масса 1000кг
    
    // Силы теперь указываются в килограмм-силах (кгс)
    public float thrustForce = 1500f; 
    public float turnTorque = 500f;
    public float liftForce = 2000f;
    
    [Header("Модель Аэродинамики")]
    public float airDensity = 1.225f; // Плотность воздуха (1.225 на уровне моря)
    public float dragCoefficient = 1.1f; // Коэффициент формы Cd (1.1 для контейнера)
    public float frontalArea = 6.3f; // Лобовая площадь (кв.м)
    
    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * frontalArea;

    [Header("Текущее управление (для чтения/записи из UI)")]
    [HideInInspector] public float thrustInput; // -1 назад, 1 вперед
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float liftInput;   // -1 вниз, 1 вверх

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = baseMass;
        rb.useGravity = true;
        
        // В базе всё по нулям, будем управлять этим в FixedUpdate
        rb.angularDamping = 2f; 
        rb.linearDamping = 0f; 
    }

    void FixedUpdate()
    {
        // Перевод из килограмм-силы (кгс) в Ньютоны для Unity (1 кгс ≈ 9.81 Н)
        float g = Mathf.Abs(Physics.gravity.y);
        float speed = rb.linearVelocity.magnitude;

        // Микро-стабилизация: включаем линейный демпфер только на малых скоростях,
        // чтобы корабль мог полностью остановиться и не дрейфовал вечно.
        rb.linearDamping = (speed < 3.0f) ? 0.5f : 0f;

        // 1. Создание подъемной силы (ось Y)
        if (liftInput != 0)
        {
            rb.AddForce(transform.up * (liftInput * liftForce * g), ForceMode.Force);
        }

        // 2. Тяга корабля (ось Z / forward)
        if (thrustInput != 0)
        {
            rb.AddForce(transform.forward * (thrustInput * thrustForce * g), ForceMode.Force);
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
    }
}
