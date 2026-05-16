using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DamageableModuleHitbox : MonoBehaviour
{
    [Header("Модуль")]
    [InspectorName("Корабль-владелец")]
    [Tooltip("Корабль, которому принадлежит этот модуль.")]
    public DamageableShip owner;
    [InspectorName("ID модуля")]
    [Tooltip("Технический id модуля. Сейчас используется только для отладочной геометрии, боевой урон отключен.")]
    public string moduleId = "module";
    [InspectorName("Название")]
    [Tooltip("Название модуля для логов и инспектора.")]
    public string displayNameRu = "Модуль";
    [InspectorName("Максимальная прочность")]
    [Tooltip("Устаревшее поле для старых тестовых сцен. Боевой урон по модулям отключен.")]
    public float defaultMaxHp = 100f;
    [InspectorName("Внешний модуль")]
    [Tooltip("Устаревшее поле. Модульные hitbox сейчас не ловят боевые попадания.")]
    public bool externalModule;
    [InspectorName("Прозрачен после уничтожения")]
    [Tooltip("Устаревшее поле. Снаряды проходят сквозь модульные hitbox, потому что урон по модулям отключен.")]
    public bool transparentWhenDestroyed = true;

    [Header("Отладка")]
    [InspectorName("Цвет")]
    public Color debugColor = new Color(0.3f, 0.8f, 1f, 0.25f);

    private Collider cachedCollider;

    public bool IsDestroyed
    {
        get
        {
            ShipDamageModuleState state = ResolveState(false);
            return state != null && state.IsDestroyed;
        }
    }

    public bool BlocksProjectile => false;

    private void Reset()
    {
        owner = GetComponentInParent<DamageableShip>();
        EnsureCollider();
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<DamageableShip>();
        }

        EnsureCollider();
        ResolveState(true);
    }

    private void OnValidate()
    {
        defaultMaxHp = Mathf.Max(1f, defaultMaxHp);
        EnsureCollider();
    }

    public DamageHitResult ReceiveDirectHit(DamageHitContext context)
    {
        return new DamageHitResult
        {
            outcome = DamageHitOutcome.Miss,
            message = "Попадание в модуль проигнорировано: урон по модулям отключен."
        };
    }

    public float ApplyModuleDamage(float amount)
    {
        return 0f;
    }

    public ShipDamageModuleState ResolveState(bool create)
    {
        DamageableShip target = owner != null ? owner : GetComponentInParent<DamageableShip>();
        if (target == null) return null;

        if (create)
        {
            return target.GetOrCreateModule(moduleId, displayNameRu, defaultMaxHp, 1f);
        }

        return target.GetModule(moduleId);
    }

    private void EnsureCollider()
    {
        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider>();
        }

        if (cachedCollider != null)
        {
            cachedCollider.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = debugColor;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(debugColor.r, debugColor.g, debugColor.b, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else
        {
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        Gizmos.matrix = previous;
    }
}
