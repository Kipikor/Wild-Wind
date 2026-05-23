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
    public bool autoDockWhenInRange = false;
    [InspectorName("Ждать выхода из текущего дока")]
    [Tooltip("Если корабль начал вылет из этого дока, автостыковка сработает только после того, как корабль сначала покинет радиус. Это не дает свободному вылету сразу вернуться в стыковку.")]
    public bool requireLeaveBeforeRedocking = true;
    [InspectorName("Точка привязки")]
    public Transform snapPoint;

    [Header("Runtime Visual")]
    [SerializeField, InspectorName("Show Dock Area In Play")] private bool showDockAreaInPlay = true;
    [SerializeField, InspectorName("Dock Area Segments")] private int dockAreaSegments = 96;
    [SerializeField, InspectorName("Dock Area Line Width")] private float dockAreaLineWidth = 4f;
    [SerializeField, InspectorName("Dock Area Height Offset")] private float dockAreaHeightOffset = 8f;
    [SerializeField, InspectorName("Dock Area Color")] private Color dockAreaColor = new Color(0.22f, 0.88f, 1f, 0.82f);
    [SerializeField, InspectorName("Dock Area In Range Color")] private Color dockAreaInRangeColor = new Color(0.34f, 1f, 0.48f, 0.92f);

    public Vector3 DockPosition => snapPoint != null ? snapPoint.position : transform.position;

    private bool leftRadiusSinceFlightStart;
    private GameSessionMode lastObservedMode;
    private LineRenderer dockAreaRenderer;
    private Material dockAreaMaterial;

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

        UpdateDockAreaVisual();
    }

    private void Update()
    {
        UpdateDockAreaVisual();

        // Стыковка теперь только ручная: игрок должен быть в радиусе и нажать кнопку "Стыковка".
        // Поля автостыковки оставлены для старых сцен, но больше не завершают вылет сами.
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        if (targetShip == null)
        {
            targetShip = FindFirstObjectByType<ShipPhysics>();
        }

        if (metaGameState == null || targetShip == null) return;

        GameSessionMode currentMode = metaGameState.CurrentMode;
        if (currentMode != lastObservedMode)
        {
            lastObservedMode = currentMode;
            leftRadiusSinceFlightStart = currentMode == GameSessionMode.Flight && !Contains(targetShip.transform.position);
        }
        else if (currentMode == GameSessionMode.Flight && !Contains(targetShip.transform.position))
        {
            leftRadiusSinceFlightStart = true;
        }
    }

    private bool IsCurrentDock()
    {
        return metaGameState != null
            && metaGameState.progress != null
            && !string.IsNullOrWhiteSpace(dockId)
            && metaGameState.progress.currentDockId == dockId;
    }

    public bool Contains(Vector3 position)
    {
        return Vector3.Distance(position, DockPosition) <= Mathf.Max(0.1f, dockingRadius);
    }

    private void OnDisable()
    {
        if (dockAreaRenderer != null)
        {
            dockAreaRenderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (dockAreaMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(dockAreaMaterial);
        }
        else
        {
            DestroyImmediate(dockAreaMaterial);
        }
    }

    private void UpdateDockAreaVisual()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureDockAreaVisual();
        if (dockAreaRenderer == null)
        {
            return;
        }

        bool visible = showDockAreaInPlay && canEndSession && dockingRadius > 0f;
        dockAreaRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        int segmentCount = Mathf.Clamp(dockAreaSegments, 24, 192);
        if (dockAreaRenderer.positionCount != segmentCount)
        {
            dockAreaRenderer.positionCount = segmentCount;
        }

        float radius = Mathf.Max(0.1f, dockingRadius);
        Vector3 center = DockPosition + Vector3.up * dockAreaHeightOffset;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (float)i / segmentCount * Mathf.PI * 2f;
            dockAreaRenderer.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        float lineWidth = Mathf.Max(0.25f, dockAreaLineWidth);
        dockAreaRenderer.startWidth = lineWidth;
        dockAreaRenderer.endWidth = lineWidth;
        Color color = targetShip != null && Contains(targetShip.transform.position) ? dockAreaInRangeColor : dockAreaColor;
        dockAreaRenderer.startColor = color;
        dockAreaRenderer.endColor = color;
    }

    private void EnsureDockAreaVisual()
    {
        if (!showDockAreaInPlay || dockAreaRenderer != null)
        {
            return;
        }

        GameObject visual = new GameObject("Dock Area Ring");
        visual.transform.SetParent(transform, false);
        dockAreaRenderer = visual.AddComponent<LineRenderer>();
        dockAreaRenderer.hideFlags = HideFlags.DontSave;
        dockAreaRenderer.useWorldSpace = true;
        dockAreaRenderer.loop = true;
        dockAreaRenderer.alignment = LineAlignment.View;
        dockAreaRenderer.textureMode = LineTextureMode.Stretch;
        dockAreaRenderer.numCapVertices = 4;
        dockAreaRenderer.numCornerVertices = 4;
        dockAreaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dockAreaRenderer.receiveShadows = false;
        dockAreaRenderer.sharedMaterial = GetDockAreaMaterial();
    }

    private Material GetDockAreaMaterial()
    {
        if (dockAreaMaterial != null)
        {
            return dockAreaMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        dockAreaMaterial = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };
        return dockAreaMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == DockingLocationKind.Island ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(DockPosition, Mathf.Max(0.1f, dockingRadius));
    }
}
