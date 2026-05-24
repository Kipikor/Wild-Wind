using System.Collections.Generic;
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
    [SerializeField, InspectorName("Dock Area Segments")] private int dockAreaSegments = 128;
    [SerializeField, InspectorName("Dock Area Line Width")] private float dockAreaLineWidth = 12f;
    [SerializeField, InspectorName("Dock Area Height Offset")] private float dockAreaHeightOffset = 22f;
    [SerializeField, InspectorName("Dock Area Beacon Height")] private float dockAreaBeaconHeight = 130f;
    [SerializeField, InspectorName("Dock Area Beacon Width")] private float dockAreaBeaconWidth = 12f;
    [SerializeField, InspectorName("Dock Area Color")] private Color dockAreaColor = new Color(0.12f, 0.92f, 1f, 0.95f);
    [SerializeField, InspectorName("Dock Area In Range Color")] private Color dockAreaInRangeColor = new Color(0.30f, 1f, 0.44f, 1f);

    public Vector3 DockPosition => snapPoint != null ? snapPoint.position : transform.position;

    private bool leftRadiusSinceFlightStart;
    private GameSessionMode lastObservedMode;
    private LineRenderer dockAreaRenderer;
    private Material dockAreaMaterial;
    private readonly List<Transform> dockAreaBeacons = new List<Transform>(4);
    private Material dockAreaBeaconMaterial;

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
        DestroyRuntimeMaterial(dockAreaMaterial);
        DestroyRuntimeMaterial(dockAreaBeaconMaterial);
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
            SetDockAreaBeaconsVisible(false);
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
        UpdateDockAreaBeacons(center, radius, color);
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
        EnsureDockAreaBeacons(visual.transform);
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

    private void EnsureDockAreaBeacons(Transform parent)
    {
        while (dockAreaBeacons.Count < 4)
        {
            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Dock Area Beacon " + dockAreaBeacons.Count;
            beacon.transform.SetParent(parent, false);

            Collider collider = beacon.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MeshRenderer renderer = beacon.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = GetDockAreaBeaconMaterial();
            }

            dockAreaBeacons.Add(beacon.transform);
        }
    }

    private void UpdateDockAreaBeacons(Vector3 center, float radius, Color color)
    {
        EnsureDockAreaBeacons(dockAreaRenderer.transform);
        Material material = GetDockAreaBeaconMaterial();
        SetMaterialColor(material, color);

        float height = Mathf.Max(12f, dockAreaBeaconHeight);
        float width = Mathf.Max(1f, dockAreaBeaconWidth);
        for (int i = 0; i < dockAreaBeacons.Count; i++)
        {
            float angle = i * Mathf.PI * 0.5f;
            Transform beacon = dockAreaBeacons[i];
            if (beacon == null)
            {
                continue;
            }

            Vector3 edge = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            beacon.gameObject.SetActive(dockAreaRenderer.enabled);
            beacon.position = edge + Vector3.up * (height * 0.5f);
            beacon.rotation = Quaternion.identity;
            beacon.localScale = new Vector3(width, height * 0.5f, width);
        }
    }

    private void SetDockAreaBeaconsVisible(bool visible)
    {
        for (int i = 0; i < dockAreaBeacons.Count; i++)
        {
            Transform beacon = dockAreaBeacons[i];
            if (beacon != null)
            {
                beacon.gameObject.SetActive(visible);
            }
        }
    }

    private Material GetDockAreaBeaconMaterial()
    {
        if (dockAreaBeaconMaterial != null)
        {
            return dockAreaBeaconMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        dockAreaBeaconMaterial = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };
        SetMaterialTransparent(dockAreaBeaconMaterial);
        return dockAreaBeaconMaterial;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static void SetMaterialTransparent(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == DockingLocationKind.Island ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(DockPosition, Mathf.Max(0.1f, dockingRadius));
    }
}
