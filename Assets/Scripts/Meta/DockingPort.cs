using UnityEngine;

public class DockingPort : MonoBehaviour
{
    public MetaGameState metaGameState;
    public ShipPhysics targetShip;
    public string dockId = "starter_island";
    public string displayName = "Starter Island";
    public DockingLocationKind kind = DockingLocationKind.Island;
    public float dockingRadius = 20f;
    public bool canEndSession = true;
    public bool autoDockWhenInRange = true;
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
