using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SortieBoundaryController : MonoBehaviour
{
    private const string BoundaryObjectName = "Sortie Boundary Controller";
    private const int MinimumRingSegments = 24;

    [InspectorName("Meta State")]
    public MetaGameState metaGameState;
    [InspectorName("Target Ship")]
    public ShipPhysics targetShip;
    [InspectorName("Legacy Clamp To Cylinder")]
    public bool clampToCylinder = false;
    [InspectorName("Boundary Padding, m")]
    public float boundaryPaddingMeters = 2f;
    [InspectorName("Draw Runtime Ring")]
    public bool drawRuntimeRing = true;
    [InspectorName("Ring Segments")]
    public int ringSegments = 96;
    [InspectorName("Ring Width, m")]
    public float ringWidthMeters = 18f;
    [InspectorName("Ring Height Offset, m")]
    public float ringHeightOffsetMeters = 0f;

    private LineRenderer ringRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSortieBoundaryBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureBoundaryForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureBoundaryForGameplayScene();
    }

    private static void EnsureBoundaryForGameplayScene()
    {
        if (FindFirstObjectByType<WorldRegionRuntime>() == null)
        {
            return;
        }

        if (FindFirstObjectByType<SortieBoundaryController>() != null)
        {
            return;
        }

        GameObject boundaryObject = new GameObject(BoundaryObjectName);
        boundaryObject.AddComponent<SortieBoundaryController>();
    }

    private void Update()
    {
        MetaGameState meta = ResolveMeta();
        if (meta == null || meta.CurrentMode != GameSessionMode.Flight || !meta.HasActiveSortie)
        {
            SetRingVisible(false);
            return;
        }

        SortieSessionState sortie = meta.ActiveSortie;
        if (sortie == null || !sortie.active || sortie.zone == null)
        {
            SetRingVisible(false);
            return;
        }

        ShipPhysics ship = ResolveShip();
        if (ship == null)
        {
            SetRingVisible(false);
            return;
        }

        sortie.zone.Normalize();
        if (clampToCylinder)
        {
            ClampShipInsideCylinder(ship, sortie.zone);
        }

        meta.RememberActiveSortiePosition(ship.transform.position);
        UpdateRing(ship.transform.position, sortie.zone);
    }

    private MetaGameState ResolveMeta()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return metaGameState;
    }

    private ShipPhysics ResolveShip()
    {
        MetaGameState meta = ResolveMeta();
        ShipPhysics loaderShip = meta != null && meta.shipLoader != null
            ? meta.shipLoader.targetShip
            : null;
        if (IsUsableShip(loaderShip))
        {
            targetShip = loaderShip;
            return targetShip;
        }

        WildWindGameplaySession session = FindFirstObjectByType<WildWindGameplaySession>();
        if (session != null && session.PlayerShipRoot != null)
        {
            ShipPhysics sessionShip = session.PlayerShipRoot.GetComponent<ShipPhysics>();
            if (sessionShip == null)
            {
                sessionShip = session.PlayerShipRoot.GetComponentInChildren<ShipPhysics>();
            }

            if (IsUsableShip(sessionShip))
            {
                targetShip = sessionShip;
                return targetShip;
            }
        }

        if (!IsUsableShip(targetShip))
        {
            targetShip = FindFirstObjectByType<ShipPhysics>();
        }

        return targetShip;
    }

    private static bool IsUsableShip(ShipPhysics ship)
    {
        return ship != null && ship.gameObject.activeInHierarchy;
    }

    private void ClampShipInsideCylinder(ShipPhysics ship, SortieZoneDefinition zone)
    {
        Vector3 position = ship.transform.position;
        Vector3 clampedPosition = ClampPositionToCylinder(position, zone, boundaryPaddingMeters);
        if ((clampedPosition - position).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 center = zone.centerPosition;
        Vector2 delta = new Vector2(position.x - center.x, position.z - center.z);
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.001f ? delta / distance : Vector2.right;

        Rigidbody body = ship.GetComponent<Rigidbody>();
        if (body != null)
        {
            Vector3 radial = new Vector3(direction.x, 0f, direction.y);
            float outwardVelocity = Vector3.Dot(body.linearVelocity, radial);
            if (outwardVelocity > 0f)
            {
                body.linearVelocity -= radial * outwardVelocity;
            }

            body.position = clampedPosition;
        }

        ship.transform.position = clampedPosition;
        Physics.SyncTransforms();
    }

    public static Vector3 ClampPositionToCylinder(Vector3 position, SortieZoneDefinition zone, float paddingMeters)
    {
        if (zone == null)
        {
            return position;
        }

        zone.Normalize();
        Vector3 center = zone.centerPosition;
        Vector2 delta = new Vector2(position.x - center.x, position.z - center.z);
        float distance = delta.magnitude;
        float allowedRadius = Mathf.Max(1f, zone.radiusMeters - Mathf.Max(0f, paddingMeters));
        if (distance <= allowedRadius)
        {
            return position;
        }

        Vector2 direction = distance > 0.001f ? delta / distance : Vector2.right;
        return new Vector3(
            center.x + direction.x * allowedRadius,
            position.y,
            center.z + direction.y * allowedRadius);
    }

    private void UpdateRing(Vector3 shipPosition, SortieZoneDefinition zone)
    {
        if (!drawRuntimeRing || zone == null)
        {
            SetRingVisible(false);
            return;
        }

        LineRenderer line = EnsureRingRenderer();
        if (line == null)
        {
            return;
        }

        int segments = Mathf.Max(MinimumRingSegments, ringSegments);
        line.positionCount = segments + 1;
        line.widthMultiplier = Mathf.Max(0.1f, ringWidthMeters);
        line.enabled = true;

        float y = shipPosition.y + ringHeightOffsetMeters;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = t * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                zone.centerPosition.x + Mathf.Cos(angle) * zone.radiusMeters,
                y,
                zone.centerPosition.z + Mathf.Sin(angle) * zone.radiusMeters);
            line.SetPosition(i, point);
        }
    }

    private LineRenderer EnsureRingRenderer()
    {
        if (ringRenderer != null)
        {
            return ringRenderer;
        }

        GameObject ringObject = new GameObject("Sortie Boundary Ring");
        ringObject.transform.SetParent(transform, false);
        ringRenderer = ringObject.AddComponent<LineRenderer>();
        ringRenderer.loop = false;
        ringRenderer.useWorldSpace = true;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;
        ringRenderer.material = new Material(Shader.Find("Sprites/Default"));
        ringRenderer.startColor = new Color(0.35f, 0.9f, 1f, 0.75f);
        ringRenderer.endColor = new Color(0.35f, 0.9f, 1f, 0.75f);
        return ringRenderer;
    }

    private void SetRingVisible(bool visible)
    {
        if (ringRenderer != null)
        {
            ringRenderer.enabled = visible;
        }
    }

    private void OnDrawGizmosSelected()
    {
        MetaGameState meta = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        SortieSessionState sortie = meta != null ? meta.ActiveSortie : null;
        if (sortie == null || !sortie.active || sortie.zone == null) return;

        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.5f);
        DrawGizmoCircle(sortie.zone.centerPosition, sortie.zone.radiusMeters, transform.position.y);
    }

    private static void DrawGizmoCircle(Vector3 center, float radius, float y)
    {
        int segments = 96;
        Vector3 previous = new Vector3(center.x + radius, y, center.z);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
