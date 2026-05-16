using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class DamageableModuleHitbox : MonoBehaviour
{
    [Header("Отладочная метка")]
    [InspectorName("Корабль-владелец")]
    [Tooltip("Оставлено только для старых тестовых сцен и отладочной геометрии. Боевого урона по модулям больше нет.")]
    public DamageableShip owner;
    [InspectorName("ID метки")]
    public string moduleId = "module";
    [InspectorName("Название")]
    public string displayNameRu = "Метка";
    [InspectorName("Внешняя метка")]
    public bool externalModule;

    [Header("Отладка")]
    [InspectorName("Цвет")]
    public Color debugColor = new Color(0.3f, 0.8f, 1f, 0.25f);

    private Collider cachedCollider;

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
    }

    private void OnValidate()
    {
        EnsureCollider();
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
