using UnityEngine;

public class DockingPort : MonoBehaviour
{
    [Header("Связи")]
    [InspectorName("Состояние меты")]
    public MetaGameState metaGameState;
    [InspectorName("Корабль игрока")]
    public ShipPhysics targetShip;

    [Header("Стыковка")]
    [InspectorName("Идентификатор дока")]
    [Tooltip("Технический идентификатор точки стыковки. Используется в сохранениях.")]
    public string dockId = "starter_island";
    [InspectorName("Название")]
    public string displayName = "Стартовый остров";
    [InspectorName("Тип")]
    public DockingLocationKind kind = DockingLocationKind.Island;
    [InspectorName("Радиус стыковки")]
    [Tooltip("Если корабль в полете входит в этот радиус, точка может завершить вылет.")]
    public float dockingRadius = 20f;
    [InspectorName("Можно завершить сессию")]
    public bool canEndSession = true;
    [InspectorName("Автостыковка в радиусе")]
    [Tooltip("Если включено, корабль автоматически перейдет в режим стыковки при входе в радиус.")]
    public bool autoDockWhenInRange = true;
    [InspectorName("Точка привязки")]
    public Transform snapPoint;

    public Vector3 DockPosition => snapPoint != null ? snapPoint.position : transform.position;

    private void Reset()
    {
        metaGameState = FindFirstObjectByType<MetaGameState>();
        targetShip = FindFirstObjectByType<ShipPhysics>();
    }

    private void Awake()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        if (targetShip == null)
        {
            targetShip = FindFirstObjectByType<ShipPhysics>();
        }
    }

    private void Update()
    {
        if (!autoDockWhenInRange || !canEndSession || metaGameState == null || targetShip == null) return;
        if (metaGameState.CurrentMode != GameSessionMode.Flight) return;
        if (!Contains(targetShip.transform.position)) return;

        targetShip.transform.position = DockPosition;
        metaGameState.DockAt(dockId, kind);
    }

    public bool Contains(Vector3 position)
    {
        return Vector3.Distance(position, DockPosition) <= Mathf.Max(0.1f, dockingRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == DockingLocationKind.Island ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(DockPosition, Mathf.Max(0.1f, dockingRadius));
    }
}
