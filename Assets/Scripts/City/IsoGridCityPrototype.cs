using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class IsoGridCityPrototype : MonoBehaviour
{
    private const int EmptyCell = 0;
    private const float TileHeight = 0.06f;
    private const int BuildingVisualVariantCount = 6;
    private const float SessionCameraMinFarClipPlane = 8000f;
    private const float TileVisualInset = 0.03f;
    private const float BaseTileVisualY = 0.004f;
    private const float HighlightTileVisualY = 0.018f;
    private const int CityRaycastLayer = 30;
    private const int CityRaycastLayerMask = 1 << CityRaycastLayer;
    private const float OccupiedExternalDockVisualWidthMultiplier = 1.36f;
    private const float OccupiedExternalDockVisualLengthMultiplier = 1.68f;
    private const float ExternalDockShipBerthHeight = 0.58f;

    [Header("Grid")]
    public int gridWidth = 40;
    public int gridHeight = 40;
    public float cellSize = 1f;

    [Header("Buildings")]
    public int selectedSize = 2;
    public int selectedWidth = 2;
    public int selectedHeight = 2;
    public float buildingHeightPerCell = 0.45f;
    [SerializeField, InspectorName("Show Prototype Toolbar")]
    private bool showPrototypeToolbar = true;

    [Header("Floating Island")]
    public float islandPaddingCells = 1.5f;
    public float islandDropDepth = 22f;
    public float islandBottomScale = 0.18f;

    [Header("Camera")]
    public float cameraYawDegrees = 45f;
    public float cameraPitchDegrees = 58f;
    public float cameraDistance = 32f;
    public float cameraOrbitSensitivity = 0.18f;
    public float cameraPanSpeed = 1f;
    public float cameraZoomWheelRotationsFullRange = 1f;
    public float cameraWheelStepsPerRotation = 24f;
    public float cameraMinVisible3x3DiagonalBuildings = 3f;
    public float cameraMaxVisible3x3DiagonalBuildings = 15f;
    public float cameraMoveSpeed = 12f;
    public float cameraMinPitchDegrees = -18f;
    public float cameraMaxPitchDegrees = 80f;
    public float cameraMinOrthographicSize = 4f;
    public float cameraMaxOrthographicSize = 22f;
    public float cameraFieldOfViewDegrees = 35f;

    private readonly Dictionary<int, PlacedBuilding> buildings = new Dictionary<int, PlacedBuilding>();
    private readonly Dictionary<string, GameObject> expansionMarkers = new Dictionary<string, GameObject>();
    private readonly List<BaseIslandExternalDockSlotView> externalDockSlots = new List<BaseIslandExternalDockSlotView>();
    private readonly Dictionary<string, ExternalDockPlacement> externalDockPlacements = new Dictionary<string, ExternalDockPlacement>();
    private readonly List<GameObject> externalDockObjects = new List<GameObject>();
    private readonly RaycastHit[] pointerRaycastHits = new RaycastHit[96];
    private IsoGridCityCell[,] cells;
    private int[,] occupancy;
    private CityCellAccess[,] cellAccess;
    private string[,] cellRegionIds;
    private int nextBuildingId = 1;
    private bool removeMode;
    private bool freeMouseMode;
    private PlacedBuilding movingBuilding;
    private int hoverX = -1;
    private int hoverY = -1;
    private int hoverBuildingId = EmptyCell;
    private bool hoverValid;
    private Camera sceneCamera;
    private Transform tileRoot;
    private Transform islandRoot;
    private Transform buildingRoot;
    private Transform expansionMarkerRoot;
    private Transform externalDockRoot;
    private MeshFilter openTileMeshFilter;
    private MeshFilter occupiedTileMeshFilter;
    private MeshFilter fogTileMeshFilter;
    private MeshFilter debrisTileMeshFilter;
    private MeshFilter clearingTileMeshFilter;
    private MeshFilter validHighlightTileMeshFilter;
    private MeshFilter invalidHighlightTileMeshFilter;
    private GameObject ghostObject;
    private GameObject hoverOutlineObject;
    private string hoverOutlineTargetId = "";
    private string hoverOutlineCachedTargetId = "";
    private int hoverOutlineCachedScreenWidth = -1;
    private int hoverOutlineCachedScreenHeight = -1;
    private Vector3 hoverOutlineCachedCameraPosition;
    private Quaternion hoverOutlineCachedCameraRotation;
    private float hoverOutlineCachedOrthographicSize = -1f;
    private Vector3 hoverOutlineCachedTargetPosition;
    private bool tilesDirty = true;
    private int cachedTileHoverX = int.MinValue;
    private int cachedTileHoverY = int.MinValue;
    private int cachedTileHoverBuildingId = int.MinValue;
    private bool cachedTileHoverValid;
    private bool cachedTileRemoveMode;
    private bool cachedTileFreeMouseMode;
    private bool cachedTileIsMoving;
    private int cachedTileActiveWidth = -1;
    private int cachedTileActiveHeight = -1;
    private int cachedTileBuildingCount = -1;
    private Text modeText;
    private Text countText;
    private Canvas hudCanvas;
    private Vector3 cameraPivot;
    private bool pointerCameraControlActive;
    private int pressedBuildingId = EmptyCell;
    private Vector2 leftPressScreenPosition;
    private bool dragMoveActive;
    private string expansionRegionSignature = "";
    private string externalDockSlotSignature = "";
    private string pressedExternalDockKey = "";
    private Vector2 leftDockPressScreenPosition;
    private string movingExternalDockKey = "";
    private string hoverExternalDockSlotId = "";
    private string hoverExpansionRegionId = "";
    private bool showExternalDockTargets;
    private bool externalDockDragActive;
    private bool hasPreservedCameraState;
    private Vector3 preservedCameraPivot;
    private float preservedCameraYawDegrees;
    private float preservedCameraPitchDegrees;
    private float preservedCameraOrthographicSize;
    private bool cameraHomeBlendActive;
    private float cameraHomeBlendStartedAt;
    private float cameraHomeBlendDuration;
    private Vector3 cameraHomeBlendStartPivot;
    private Vector3 cameraHomeBlendTargetPivot;
    private float cameraHomeBlendStartYawDegrees;
    private float cameraHomeBlendTargetYawDegrees;
    private float cameraHomeBlendStartPitchDegrees;
    private float cameraHomeBlendTargetPitchDegrees;
    private float cameraHomeBlendStartOrthographicSize;
    private float cameraHomeBlendTargetOrthographicSize;
    private bool cameraTransformBlendActive;
    private bool cameraTransformBlendActivatesPendingPortDockOrbit;
    private Vector3 cameraTransformBlendStartPosition;
    private Vector3 cameraTransformBlendTargetPosition;
    private Quaternion cameraTransformBlendStartRotation = Quaternion.identity;
    private Quaternion cameraTransformBlendTargetRotation = Quaternion.identity;
    private float cameraTransformBlendStartOrthographicSize;
    private float cameraTransformBlendTargetOrthographicSize;
    private Vector3 cameraTransformBlendTargetPivot;
    private float cameraTransformBlendTargetYawDegrees;
    private float cameraTransformBlendTargetPitchDegrees;
    private bool portDockOrbitActive;
    private bool pendingPortDockOrbitActive;
    private bool portDockWorldInteractionSuppressed;
    private Vector3 portDockOrbitFocus;
    private Vector3 portDockOrbitForward = Vector3.forward;
    private Vector3 portDockOrbitRight = Vector3.right;
    private float portDockOrbitBaseMajorRadius;
    private float portDockOrbitBaseMinorRadius;
    private float portDockOrbitBaseZoomSize;
    private float portDockOrbitBaseCameraDistance;
    private float portDockOrbitAngleDegrees;
    private float portDockOrbitPitchDegrees;
    private Vector3 pendingPortDockOrbitFocus;
    private Vector3 pendingPortDockOrbitForward = Vector3.forward;
    private Vector3 pendingPortDockOrbitRight = Vector3.right;
    private float pendingPortDockOrbitBaseMajorRadius;
    private float pendingPortDockOrbitBaseMinorRadius;
    private float pendingPortDockOrbitBaseZoomSize;
    private float pendingPortDockOrbitBaseCameraDistance;
    private float pendingPortDockOrbitAngleDegrees;
    private float pendingPortDockOrbitPitchDegrees;
    private Vector3 portDockOrbitBlendStartFocus;
    private Vector3 portDockOrbitBlendStartForward = Vector3.forward;
    private Vector3 portDockOrbitBlendStartRight = Vector3.right;
    private float portDockOrbitBlendStartBaseMajorRadius;
    private float portDockOrbitBlendStartBaseMinorRadius;
    private float portDockOrbitBlendStartBaseZoomSize;
    private float portDockOrbitBlendStartBaseCameraDistance;
    private float portDockOrbitBlendStartAngleDegrees;
    private float portDockOrbitBlendStartPitchDegrees;

    private Material tileMaterial;
    private Material occupiedMaterial;
    private Material validMaterial;
    private Material invalidMaterial;
    private Material ghostValidMaterial;
    private Material ghostInvalidMaterial;
    private Material hoverOutlineMaterial;
    private Material fogMaterial;
    private Material debrisMaterial;
    private Material clearingMaterial;
    private Material expansionMarkerMaterial;
    private Material externalDockEmptyMaterial;
    private Material externalDockDeckMaterial;
    private Material externalDockAccentMaterial;
    private Material islandTopMaterial;
    private Material islandSideMaterial;
    private Material islandBottomMaterial;
    private Material[] buildingMaterials;

    private bool IsMoving => movingBuilding != null;
    private int ActiveWidth => IsMoving ? movingBuilding.width : ClampBuildingDimension(selectedWidth);
    private int ActiveHeight => IsMoving ? movingBuilding.height : ClampBuildingDimension(selectedHeight);
    private const float DragStartPixels = 8f;
    private const float OutlinePointEpsilonPixels = 0.75f;
    private const float OutlineRasterPaddingPixels = 6f;
    private const float OutlineRasterMinStepPixels = 1.5f;
    private const int OutlineRasterMaxCells = 180;
    private const float OutlineSimplifyTolerancePixels = 2.25f;
    private const float OutlineOverlayPlaneDistance = 2f;
    private const float CameraHomeBlendSeconds = 0.55f;
    private const float PortDockOrbitAngleSensitivity = 0.16f;
    private const float PortDockOrbitKeyboardDegreesPerSecond = 48f;
    private const float PortDockOrbitMinPitchDegrees = 18f;
    private const float PortDockOrbitMaxPitchDegrees = 66f;
    private const float PortDockOrbitMinZoomScale = 0.55f;
    private const float PortDockOrbitMaxZoomScale = 1.85f;

    public event Action<string> BuildingClicked;
    public event Action<string> ExpansionRegionClicked;
    public event Action<string> ExternalDockClicked;
    public string HoveredBuildingKeyForRuntime { get; private set; } = "";
    public bool IsMovingBuildingForRuntime => IsMoving;

    public bool IsReadyForTests => sceneCamera != null
        && islandRoot != null
        && tileRoot != null
        && buildingRoot != null
        && expansionMarkerRoot != null
        && externalDockRoot != null
        && cells != null
        && occupancy != null
        && cellAccess != null;
    public int BuildingCountForTests => buildings.Count;
    public int GridWidthForTests => gridWidth;
    public int GridHeightForTests => gridHeight;
    public int ExpansionMarkerCountForTests => expansionMarkers.Count;
    public int ExternalDockSlotCountForTests => externalDockSlots.Count;
    public int ExternalDockPlacedCountForTests => externalDockPlacements.Count;
    public bool IsPortDockWorldInteractionSuppressedForTests => portDockWorldInteractionSuppressed;
    public int TileColliderCountForTests => tileRoot != null
        ? tileRoot.GetComponentsInChildren<Collider>(true).Length
        : 0;
    public int TileBatchRendererCountForTests => tileRoot != null
        ? tileRoot.GetComponentsInChildren<MeshRenderer>(true).Length
        : 0;
    public int EnabledCityRendererCountForTests => CountEnabledRenderers(transform);
    public int EnabledBuildingRendererCountForTests => buildingRoot != null ? CountEnabledRenderers(buildingRoot) : 0;
    public int EnabledTileRendererCountForTests => tileRoot != null ? CountEnabledRenderers(tileRoot) : 0;
    public int CameraVisibleCityRendererCountForTests => CountCameraVisibleRenderers(transform);
    public Vector3 IslandWorldSizeForTests => TryGetWorldRendererBounds(islandRoot != null ? islandRoot.gameObject : null, out Bounds islandBounds)
        ? islandBounds.size
        : Vector3.zero;
    public float IslandGridOverhangCellsForTests
    {
        get
        {
            if (!TryGetWorldRendererBounds(islandRoot != null ? islandRoot.gameObject : null, out Bounds islandBounds))
            {
                return 0f;
            }

            float gridMinX = -cellSize * 0.5f;
            float gridMaxX = (gridWidth - 1) * cellSize + cellSize * 0.5f;
            float gridMinZ = -cellSize * 0.5f;
            float gridMaxZ = (gridHeight - 1) * cellSize + cellSize * 0.5f;
            float minOverhang = Mathf.Min(
                gridMinX - islandBounds.min.x,
                islandBounds.max.x - gridMaxX,
                gridMinZ - islandBounds.min.z,
                islandBounds.max.z - gridMaxZ);
            return minOverhang / Mathf.Max(0.01f, cellSize);
        }
    }
    public float IslandGridMaxOverhangCellsForTests
    {
        get
        {
            if (!TryGetWorldRendererBounds(islandRoot != null ? islandRoot.gameObject : null, out Bounds islandBounds))
            {
                return 0f;
            }

            float gridMinX = -cellSize * 0.5f;
            float gridMaxX = (gridWidth - 1) * cellSize + cellSize * 0.5f;
            float gridMinZ = -cellSize * 0.5f;
            float gridMaxZ = (gridHeight - 1) * cellSize + cellSize * 0.5f;
            float maxOverhang = Mathf.Max(
                gridMinX - islandBounds.min.x,
                islandBounds.max.x - gridMaxX,
                gridMinZ - islandBounds.min.z,
                islandBounds.max.z - gridMaxZ);
            return maxOverhang / Mathf.Max(0.01f, cellSize);
        }
    }
    public bool IslandFootprintCoversGridForTests
    {
        get
        {
            if (!TryGetWorldRendererBounds(islandRoot != null ? islandRoot.gameObject : null, out Bounds islandBounds))
            {
                return false;
            }

            float gridMinX = -cellSize * 0.5f;
            float gridMaxX = (gridWidth - 1) * cellSize + cellSize * 0.5f;
            float gridMinZ = -cellSize * 0.5f;
            float gridMaxZ = (gridHeight - 1) * cellSize + cellSize * 0.5f;
            return islandBounds.min.x <= gridMinX
                && islandBounds.max.x >= gridMaxX
                && islandBounds.min.z <= gridMinZ
                && islandBounds.max.z >= gridMaxZ;
        }
    }
    public int CellDataCountForTests => cells != null ? cells.Length : 0;
    public int OpenCellCountForTests => CountCellsWithAccess(CityCellAccess.Open);
    public int FogCellCountForTests => CountCellsWithAccess(CityCellAccess.Fog);
    public int DebrisCellCountForTests => CountCellsWithAccess(CityCellAccess.Debris);
    public bool IsPrototypeToolbarVisibleForTests => hudCanvas != null && hudCanvas.gameObject.activeSelf;
    public bool IsHoverOutlineVisibleForTests
    {
        get
        {
            if (hoverOutlineObject == null || !hoverOutlineObject.activeSelf)
            {
                return false;
            }

            LineRenderer line = hoverOutlineObject.GetComponent<LineRenderer>();
            return line != null && line.positionCount >= 3;
        }
    }
    public bool HasCityCameraForTests => sceneCamera != null
        && !sceneCamera.orthographic
        && sceneCamera.farClipPlane >= SessionCameraMinFarClipPlane * 0.99f
        && sceneCamera.fieldOfView >= 20f
        && cameraPitchDegrees >= cameraMinPitchDegrees
        && cameraPitchDegrees <= cameraMaxPitchDegrees;
    public bool IsCameraHomeBlendActiveForTests => cameraHomeBlendActive;
    public bool IsCameraTransformBlendActiveForTests => cameraTransformBlendActive;
    public bool IsCameraTransformBlendTargetingPortDockForTests => cameraTransformBlendActive
        && cameraTransformBlendActivatesPendingPortDockOrbit
        && pendingPortDockOrbitActive;
    public bool IsCameraTransformBlendTargetingPreservedStateForTests => cameraTransformBlendActive
        && !cameraTransformBlendActivatesPendingPortDockOrbit
        && hasPreservedCameraState
        && Vector3.Distance(cameraTransformBlendTargetPivot, preservedCameraPivot) <= 0.01f
        && Mathf.Abs(Mathf.DeltaAngle(cameraTransformBlendTargetYawDegrees, preservedCameraYawDegrees)) <= 0.01f
        && Mathf.Abs(cameraTransformBlendTargetPitchDegrees - Mathf.Clamp(preservedCameraPitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees)) <= 0.01f
        && Mathf.Abs(cameraTransformBlendTargetOrthographicSize - Mathf.Clamp(preservedCameraOrthographicSize, cameraMinOrthographicSize, cameraMaxOrthographicSize)) <= 0.01f;
    public bool IsPortDockCameraOrbitActiveForTests => portDockOrbitActive;
    public bool IsPortDockCameraOrbitLookingAtFocusForTests
    {
        get
        {
            if (!portDockOrbitActive || sceneCamera == null)
            {
                return false;
            }

            Vector3 toFocus = portDockOrbitFocus - sceneCamera.transform.position;
            return toFocus.sqrMagnitude > 0.0001f
                && Vector3.Angle(sceneCamera.transform.forward, toFocus.normalized) <= 0.5f;
        }
    }
    public bool IsCameraAtDefaultViewForTests => sceneCamera != null
        && Vector3.Distance(cameraPivot, IslandOrbitCenterWorld()) <= 0.01f
        && Mathf.Abs(Mathf.DeltaAngle(cameraYawDegrees, 45f)) <= 0.01f
        && Mathf.Abs(cameraPitchDegrees - Mathf.Clamp(58f, cameraMinPitchDegrees, cameraMaxPitchDegrees)) <= 0.01f
        && Mathf.Abs(GetCameraZoomSize() - GetDefaultCameraOrthographicSize()) <= 0.01f;
    public int CityRaycastLayerForTests => CityRaycastLayer;
    public int InteractiveColliderLayerMismatchCountForTests => CountInteractiveColliderLayerMismatches();
    public string CameraSignatureForTests => sceneCamera != null
        ? cameraPivot.x.ToString("0.###") + "|"
            + cameraPivot.y.ToString("0.###") + "|"
            + cameraPivot.z.ToString("0.###") + "|"
            + cameraYawDegrees.ToString("0.###") + "|"
            + cameraPitchDegrees.ToString("0.###") + "|"
            + GetCameraZoomSize().ToString("0.###")
        : "";

    private void Awake()
    {
        BuildPrototype();
    }

    private void Update()
    {
        if (!EnsureRuntimeGridReady())
        {
            return;
        }

        ReadKeyboardShortcuts();
        if (!ApplyCameraHomeBlend())
        {
            HandleCameraInput();
        }
        UpdateHover();
        RefreshTilesIfNeeded();
        RefreshGhost();
        HandlePointerInput();
        RefreshHoverOutline();
        RefreshExpansionMarkerFacing();
        RefreshHud();
    }

    public void SnapCameraToCityViewForRuntime()
    {
        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return;
        }

        cameraHomeBlendActive = false;
        cameraTransformBlendActive = false;
        ClearPortDockCameraOrbit();
        ApplyDefaultCameraPose();
    }

    public void PreserveCameraStateBeforePortDockForRuntime()
    {
        EnsureSceneCamera();
        if (sceneCamera == null || portDockOrbitActive || pendingPortDockOrbitActive || cameraTransformBlendActive)
        {
            return;
        }

        PreserveCameraStateForRuntime();
    }

    public void PreserveCameraStateForRuntime()
    {
        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return;
        }

        preservedCameraPivot = cameraPivot;
        preservedCameraYawDegrees = cameraYawDegrees;
        preservedCameraPitchDegrees = cameraPitchDegrees;
        preservedCameraOrthographicSize = GetCameraZoomSize();
        hasPreservedCameraState = true;
    }

    public bool RestorePreservedCameraStateForRuntime()
    {
        if (!hasPreservedCameraState)
        {
            return false;
        }

        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return false;
        }

        cameraHomeBlendActive = false;
        cameraTransformBlendActive = false;
        ClearPortDockCameraOrbit();
        cameraPivot = preservedCameraPivot;
        cameraYawDegrees = preservedCameraYawDegrees;
        cameraPitchDegrees = Mathf.Clamp(preservedCameraPitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        ConfigureSceneCameraForCity();
        SetCameraZoomSize(preservedCameraOrthographicSize);
        ApplyCameraTransform();
        InvalidateHoverOutlineCache();
        return true;
    }

    public bool FlyCameraToPreservedCameraStateForRuntime()
    {
        if (!hasPreservedCameraState)
        {
            return false;
        }

        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return false;
        }

        ConfigureSceneCameraForCity();
        float targetPitch = Mathf.Clamp(preservedCameraPitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        float targetZoom = Mathf.Clamp(preservedCameraOrthographicSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        CalculateCityCameraPose(
            preservedCameraPivot,
            preservedCameraYawDegrees,
            targetPitch,
            targetZoom,
            out Vector3 targetPosition,
            out Quaternion targetRotation);
        StartCameraTransformBlend(
            targetPosition,
            targetRotation,
            targetZoom,
            preservedCameraPivot,
            preservedCameraYawDegrees,
            targetPitch,
            false);
        ClearPortDockCameraOrbit();
        InvalidateHoverOutlineCache();
        return true;
    }

    public void FlyCameraToDefaultViewForRuntime()
    {
        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return;
        }

        ConfigureSceneCameraForCity();
        cameraTransformBlendActive = false;
        ClearPortDockCameraOrbit();
        cameraHomeBlendStartPivot = cameraPivot;
        cameraHomeBlendTargetPivot = IslandOrbitCenterWorld();
        cameraHomeBlendStartYawDegrees = cameraYawDegrees;
        cameraHomeBlendTargetYawDegrees = 45f;
        cameraHomeBlendStartPitchDegrees = cameraPitchDegrees;
        cameraHomeBlendTargetPitchDegrees = Mathf.Clamp(58f, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        cameraHomeBlendStartOrthographicSize = GetCameraZoomSize();
        cameraHomeBlendTargetOrthographicSize = GetDefaultCameraOrthographicSize();
        cameraHomeBlendStartedAt = Time.unscaledTime;
        cameraHomeBlendDuration = CameraHomeBlendSeconds;
        cameraHomeBlendActive = true;
        InvalidateHoverOutlineCache();
    }

    public bool FlyCameraToWorldFocusForRuntime(Vector3 targetPivot, float yawDegrees, float pitchDegrees, float zoomSize, bool snap)
    {
        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return false;
        }

        ConfigureSceneCameraForCity();
        cameraTransformBlendActive = false;
        ClearPortDockCameraOrbit();
        float clampedPitch = Mathf.Clamp(pitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        float clampedZoom = Mathf.Clamp(zoomSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        if (snap)
        {
            cameraHomeBlendActive = false;
            cameraPivot = targetPivot;
            cameraYawDegrees = yawDegrees;
            cameraPitchDegrees = clampedPitch;
            SetCameraZoomSize(clampedZoom);
            ApplyCameraTransform();
            InvalidateHoverOutlineCache();
            return true;
        }

        cameraHomeBlendStartPivot = cameraPivot;
        cameraHomeBlendTargetPivot = targetPivot;
        cameraHomeBlendStartYawDegrees = cameraYawDegrees;
        cameraHomeBlendTargetYawDegrees = yawDegrees;
        cameraHomeBlendStartPitchDegrees = cameraPitchDegrees;
        cameraHomeBlendTargetPitchDegrees = clampedPitch;
        cameraHomeBlendStartOrthographicSize = GetCameraZoomSize();
        cameraHomeBlendTargetOrthographicSize = clampedZoom;
        cameraHomeBlendStartedAt = Time.unscaledTime;
        cameraHomeBlendDuration = CameraHomeBlendSeconds;
        cameraHomeBlendActive = true;
        InvalidateHoverOutlineCache();
        return true;
    }

    public bool FlyCameraToExternalDockForRuntime(string dockKey, float zoomSize, bool snap)
    {
        return FlyCameraToExternalDockForRuntime(dockKey, zoomSize, Vector3.zero, snap);
    }

    public bool FlyCameraToExternalDockForRuntime(string dockKey, float zoomSize, Vector3 shipSize, bool snap)
    {
        if (!TryGetExternalDockBerthPoseForRuntime(
                dockKey,
                out Vector3 berthPosition,
                out Quaternion berthRotation,
                out float berthLength,
                out float berthWidth,
                out BaseIslandExternalDockOrientation orientation))
        {
            return false;
        }

        Vector3 pivot = berthPosition;
        pivot.y = Mathf.Max(pivot.y + 0.35f, 0.9f);
        Vector3 safeShipSize = shipSize.sqrMagnitude > 0.0001f
            ? new Vector3(
                Mathf.Max(0.01f, shipSize.x),
                Mathf.Max(0.01f, shipSize.y),
                Mathf.Max(0.01f, shipSize.z))
            : new Vector3(Mathf.Max(1f, berthWidth), 1f, Mathf.Max(1f, berthLength));

        Vector3 forward = Vector3.ProjectOnPlane(berthRotation * Vector3.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = GetExternalDockOutwardDirection(orientation);
        }

        Vector3 right = Vector3.ProjectOnPlane(berthRotation * Vector3.right, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        ConfigureSceneCameraForCity();
        float clampedZoom = Mathf.Clamp(zoomSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        float majorRadius = Mathf.Max(safeShipSize.z * 0.72f, clampedZoom * 0.72f, berthLength * 0.55f);
        float minorRadius = Mathf.Max(safeShipSize.x * 2.15f, clampedZoom * 0.56f, berthWidth * 0.85f);
        float initialYaw = GetExternalDockCameraYawDegrees(orientation);
        float targetYaw = portDockOrbitActive && sceneCamera != null
            ? sceneCamera.transform.eulerAngles.y
            : initialYaw;
        float angleDegrees = CalculatePortDockOrbitAngleForCameraYaw(targetYaw, forward, right, majorRadius, minorRadius);
        float pitchDegrees = Mathf.Clamp(42f, PortDockOrbitMinPitchDegrees, PortDockOrbitMaxPitchDegrees);

        if (snap)
        {
            cameraHomeBlendActive = false;
            cameraTransformBlendActive = false;
            SetCameraZoomSize(clampedZoom);
            ActivatePortDockCameraOrbit(
                pivot,
                forward,
                right,
                majorRadius,
                minorRadius,
                clampedZoom,
                Mathf.Max(0.001f, cameraDistance),
                angleDegrees,
                pitchDegrees);
            return true;
        }

        if (portDockOrbitActive)
        {
            CapturePortDockOrbitBlendStart();
            SetPendingPortDockCameraOrbit(
                pivot,
                forward,
                right,
                majorRadius,
                minorRadius,
                clampedZoom,
                EquivalentOrthographicSizeToCameraDistance(clampedZoom),
                angleDegrees,
                pitchDegrees);
            cameraHomeBlendStartedAt = Time.unscaledTime;
            cameraHomeBlendDuration = CameraHomeBlendSeconds;
            cameraHomeBlendActive = true;
            InvalidateHoverOutlineCache();
            return true;
        }

        ClearPortDockCameraOrbit();
        float targetCameraDistance = EquivalentOrthographicSizeToCameraDistance(clampedZoom);
        SetPendingPortDockCameraOrbit(
            pivot,
            forward,
            right,
            majorRadius,
            minorRadius,
            clampedZoom,
            targetCameraDistance,
            angleDegrees,
            pitchDegrees);
        CalculatePortDockOrbitCameraPose(
            pivot,
            forward,
            right,
            majorRadius,
            minorRadius,
            targetCameraDistance,
            targetCameraDistance,
            angleDegrees,
            pitchDegrees,
            out Vector3 targetPosition,
            out Quaternion targetRotation);
        StartCameraTransformBlend(
            targetPosition,
            targetRotation,
            clampedZoom,
            pivot,
            initialYaw,
            pitchDegrees,
            true);
        InvalidateHoverOutlineCache();
        return true;
    }

    public bool OffsetCameraForTests(float pivotX, float pivotZ, float yawDelta, float pitchDelta, float orthographicSizeDelta)
    {
        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return false;
        }

        cameraHomeBlendActive = false;
        cameraTransformBlendActive = false;
        ClearPortDockCameraOrbit();
        cameraPivot += new Vector3(pivotX, 0f, pivotZ);
        cameraYawDegrees += yawDelta;
        cameraPitchDegrees = Mathf.Clamp(cameraPitchDegrees + pitchDelta, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        ConfigureSceneCameraForCity();
        SetCameraZoomSize(GetCameraZoomSize() + orthographicSizeDelta);
        ApplyCameraTransform();
        InvalidateHoverOutlineCache();
        return true;
    }

    public bool OffsetPortDockCameraOrbitForTests(float angleDeltaDegrees)
    {
        if (!portDockOrbitActive || sceneCamera == null)
        {
            return false;
        }

        Vector3 before = sceneCamera.transform.position;
        portDockOrbitAngleDegrees += angleDeltaDegrees;
        ApplyCameraTransform();
        return Vector3.Distance(before, sceneCamera.transform.position) > 0.01f
            && IsPortDockCameraOrbitLookingAtFocusForTests;
    }

    public void SelectFreeMouseModeForRuntime()
    {
        SelectFreeMouseMode();
    }

    public void SetPrototypeToolbarVisibleForRuntime(bool visible)
    {
        showPrototypeToolbar = visible;
        if (visible)
        {
            if (hudCanvas == null)
            {
                BuildHud();
            }
            else
            {
                hudCanvas.gameObject.SetActive(true);
            }
        }
        else
        {
            HidePrototypeToolbar();
        }
    }

    public void SetPortDockWorldInteractionSuppressedForRuntime(bool suppressed)
    {
        if (portDockWorldInteractionSuppressed == suppressed)
        {
            return;
        }

        portDockWorldInteractionSuppressed = suppressed;
        if (!suppressed)
        {
            return;
        }

        ClearPressedBuilding();
        ClearPressedExternalDock();
        CancelMove();
        externalDockDragActive = false;
        movingExternalDockKey = "";
        showExternalDockTargets = false;
        hoverX = -1;
        hoverY = -1;
        hoverBuildingId = EmptyCell;
        hoverExternalDockSlotId = "";
        hoverExpansionRegionId = "";
        hoverValid = false;
        HoveredBuildingKeyForRuntime = "";
        DestroyHoverOutline();
    }

    public void ApplyExpansionRegions(IReadOnlyList<BaseIslandExpansionRegionView> regions)
    {
        if (!EnsureRuntimeGridReady())
        {
            return;
        }

        string signature = BuildExpansionRegionSignature(regions);
        if (signature == expansionRegionSignature)
        {
            return;
        }

        expansionRegionSignature = signature;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                cellAccess[x, y] = CityCellAccess.Void;
                cellRegionIds[x, y] = "";
            }
        }

        if (regions != null)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                BaseIslandExpansionRegionView region = regions[i];
                CityCellAccess access = ToCellAccess(region.state);
                for (int x = region.originX; x < region.originX + region.width; x++)
                {
                    for (int y = region.originY; y < region.originY + region.height; y++)
                    {
                        if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                        {
                            continue;
                        }

                        cellAccess[x, y] = access;
                        cellRegionIds[x, y] = region.regionId ?? "";
                    }
                }
            }
        }

        RefreshExpansionMarkers(regions);
        InvalidateHoverOutlineCache();
        MarkTilesDirty();
    }

    public void ApplyExternalDockSlots(IReadOnlyList<BaseIslandExternalDockSlotView> slots)
    {
        if (!EnsureRuntimeGridReady())
        {
            return;
        }

        string signature = BuildExternalDockSlotSignature(slots);
        if (signature == externalDockSlotSignature)
        {
            return;
        }

        externalDockSlotSignature = signature;
        externalDockSlots.Clear();
        HashSet<string> validSlotIds = new HashSet<string>();
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                BaseIslandExternalDockSlotView slot = slots[i];
                if (string.IsNullOrWhiteSpace(slot.dockId))
                {
                    continue;
                }

                externalDockSlots.Add(slot);
                validSlotIds.Add(slot.dockId);
            }
        }

        RemoveInvalidExternalDockPlacements(validSlotIds);
        RefreshExternalDockVisuals();
    }

    private void RefreshExternalDockVisuals()
    {
        if (externalDockRoot == null)
        {
            return;
        }

        for (int i = 0; i < externalDockObjects.Count; i++)
        {
            if (externalDockObjects[i] != null)
            {
                Destroy(externalDockObjects[i]);
            }
        }

        externalDockObjects.Clear();
        for (int i = 0; i < externalDockSlots.Count; i++)
        {
            externalDockObjects.Add(CreateExternalDockSlot(externalDockSlots[i]));
        }
    }

    private void RemoveInvalidExternalDockPlacements(HashSet<string> validSlotIds)
    {
        if (externalDockPlacements.Count == 0)
        {
            return;
        }

        List<string> removedKeys = null;
        foreach (KeyValuePair<string, ExternalDockPlacement> pair in externalDockPlacements)
        {
            if (pair.Value == null || !validSlotIds.Contains(pair.Value.slotId))
            {
                if (removedKeys == null)
                {
                    removedKeys = new List<string>();
                }

                removedKeys.Add(pair.Key);
            }
        }

        if (removedKeys == null)
        {
            return;
        }

        for (int i = 0; i < removedKeys.Count; i++)
        {
            externalDockPlacements.Remove(removedKeys[i]);
        }
    }

    private static string BuildExpansionRegionSignature(IReadOnlyList<BaseIslandExpansionRegionView> regions)
    {
        if (regions == null || regions.Count == 0)
        {
            return "";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(regions.Count * 32);
        for (int i = 0; i < regions.Count; i++)
        {
            BaseIslandExpansionRegionView region = regions[i];
            builder.Append(region.regionId).Append(':')
                .Append(region.originX).Append(',')
                .Append(region.originY).Append(',')
                .Append(region.width).Append('x')
                .Append(region.height).Append(':')
                .Append((int)region.state).Append(':')
                .Append(Mathf.CeilToInt(region.remainingSeconds)).Append('|');
        }

        return builder.ToString();
    }

    private static string BuildExternalDockSlotSignature(IReadOnlyList<BaseIslandExternalDockSlotView> slots)
    {
        if (slots == null || slots.Count == 0)
        {
            return "";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(slots.Count * 28);
        for (int i = 0; i < slots.Count; i++)
        {
            BaseIslandExternalDockSlotView slot = slots[i];
            builder.Append(slot.dockId).Append(':')
                .Append(slot.centerX.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(slot.centerY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(':')
                .Append(slot.contactWidth).Append('x')
                .Append(slot.protrusionLength).Append(':')
                .Append((int)slot.orientation).Append('|');
        }

        return builder.ToString();
    }


    public bool TryPlacePrefabBuildingForRuntime(
        string buildingKey,
        string prefabResourcePath,
        int size,
        int originX,
        int originY,
        Color color)
    {
        return TryPlacePrefabBuildingForRuntime(buildingKey, prefabResourcePath, size, size, originX, originY, color, "");
    }

    public bool TryPlacePrefabBuildingForRuntime(
        string buildingKey,
        string prefabResourcePath,
        int width,
        int height,
        int originX,
        int originY,
        Color color,
        string visualKind = "")
    {
        if (string.IsNullOrWhiteSpace(buildingKey) || string.IsNullOrWhiteSpace(prefabResourcePath))
        {
            if (string.IsNullOrWhiteSpace(buildingKey))
            {
                return false;
            }
        }

        if (!EnsureRuntimeGridReady())
        {
            return false;
        }

        if (HasBuildingKeyForTests(buildingKey))
        {
            return true;
        }

        width = ClampBuildingDimension(width);
        height = ClampBuildingDimension(height);
        if (!CanPlaceAt(originX, originY, width, height))
        {
            return false;
        }

        int id = nextBuildingId++;
        Occupy(originX, originY, width, height, id);
        CreateBuildingObject(
            id,
            width,
            height,
            originX,
            originY,
            color,
            BuildingVariantForId(id, width, height, visualKind),
            buildingKey.Trim(),
            prefabResourcePath != null ? prefabResourcePath.Trim() : "",
            visualKind != null ? visualKind.Trim() : "");
        return true;
    }

    public bool TryPlacePrefabBuildingAtFirstFreeForRuntime(
        string buildingKey,
        string prefabResourcePath,
        int width,
        int height,
        Color color,
        string visualKind,
        out int originX,
        out int originY)
    {
        originX = -1;
        originY = -1;
        width = ClampBuildingDimension(width);
        height = ClampBuildingDimension(height);
        for (int y = 0; y <= gridHeight - height; y++)
        {
            for (int x = 0; x <= gridWidth - width; x++)
            {
                if (!CanPlaceAt(x, y, width, height))
                {
                    continue;
                }

                bool placed = TryPlacePrefabBuildingForRuntime(
                    buildingKey,
                    prefabResourcePath,
                    width,
                    height,
                    x,
                    y,
                    color,
                    visualKind);
                if (placed)
                {
                    originX = x;
                    originY = y;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryPlaceExternalDockAtFirstFreeForRuntime(
        string dockKey,
        Color color,
        string visualKind,
        out string slotId)
    {
        slotId = "";
        if (string.IsNullOrWhiteSpace(dockKey) || externalDockSlots.Count == 0 || HasExternalDockKeyForTests(dockKey))
        {
            return false;
        }

        for (int i = 0; i < externalDockSlots.Count; i++)
        {
            string candidateSlotId = externalDockSlots[i].dockId;
            if (string.IsNullOrWhiteSpace(candidateSlotId) || TryGetExternalDockInSlot(candidateSlotId, out _))
            {
                continue;
            }

            string normalizedKey = dockKey.Trim();
            externalDockPlacements[normalizedKey] = new ExternalDockPlacement
            {
                dockKey = normalizedKey,
                definitionId = GetDefinitionIdFromInstanceKey(normalizedKey),
                slotId = candidateSlotId,
                color = color,
                visualKind = visualKind ?? ""
            };
            slotId = candidateSlotId;
            RefreshExternalDockVisuals();
            return true;
        }

        return false;
    }

    public int CountExternalDockInstancesForRuntime(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return 0;
        }

        string normalizedId = definitionId.Trim();
        int count = 0;
        foreach (ExternalDockPlacement placement in externalDockPlacements.Values)
        {
            if (placement != null && placement.definitionId == normalizedId)
            {
                count++;
            }
        }

        return count;
    }

    public bool HasExternalDockKeyForTests(string dockKey)
    {
        return !string.IsNullOrWhiteSpace(dockKey) && externalDockPlacements.ContainsKey(dockKey.Trim());
    }

    public int CountExactExternalDockKeyForTests(string dockKey)
    {
        return HasExternalDockKeyForTests(dockKey) ? 1 : 0;
    }

    public bool TryMoveExternalDockToFirstFreeSlotForTests(string dockKey)
    {
        if (string.IsNullOrWhiteSpace(dockKey) || !externalDockPlacements.TryGetValue(dockKey.Trim(), out ExternalDockPlacement placement))
        {
            return false;
        }

        for (int i = 0; i < externalDockSlots.Count; i++)
        {
            string candidateSlotId = externalDockSlots[i].dockId;
            if (candidateSlotId == placement.slotId || TryGetExternalDockInSlot(candidateSlotId, out _))
            {
                continue;
            }

            return MoveExternalDockToSlot(placement.dockKey, candidateSlotId, true);
        }

        return false;
    }

    public bool TryMoveExternalDockForTests(string dockKey, string targetSlotId)
    {
        return MoveExternalDockToSlot(dockKey, targetSlotId, true);
    }

    public bool ShowExternalDockHoverForTests(string dockKey)
    {
        if (string.IsNullOrWhiteSpace(dockKey)
            || !externalDockPlacements.TryGetValue(dockKey.Trim(), out ExternalDockPlacement placement))
        {
            return false;
        }

        freeMouseMode = true;
        removeMode = false;
        hoverBuildingId = EmptyCell;
        hoverExternalDockSlotId = placement.slotId;
        RefreshHoveredBuildingKey();
        RefreshHoverOutline();
        return IsHoverOutlineVisibleForTests;
    }

    public bool HasBuildingKeyForTests(string buildingKey)
    {
        if (string.IsNullOrWhiteSpace(buildingKey))
        {
            return false;
        }

        foreach (PlacedBuilding building in buildings.Values)
        {
            if (building != null && building.buildingKey == buildingKey)
            {
                return true;
            }
        }

        return false;
    }

    public int CountBuildingInstancesForRuntime(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return 0;
        }

        string normalizedId = definitionId.Trim();
        string instancePrefix = normalizedId + "__";
        int count = 0;
        foreach (PlacedBuilding building in buildings.Values)
        {
            if (building == null || string.IsNullOrWhiteSpace(building.buildingKey))
            {
                continue;
            }

            if (string.Equals(building.buildingKey, normalizedId, StringComparison.Ordinal)
                || building.buildingKey.StartsWith(instancePrefix, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    public int PruneInvalidBuildingsForRuntime()
    {
        List<int> invalidIds = null;
        foreach (KeyValuePair<int, PlacedBuilding> pair in buildings)
        {
            PlacedBuilding building = pair.Value;
            if (building == null
                || building.gameObject == null
                || CountEnabledRenderers(building.gameObject.transform) <= 0)
            {
                if (invalidIds == null)
                {
                    invalidIds = new List<int>();
                }

                invalidIds.Add(pair.Key);
            }
        }

        if (invalidIds == null)
        {
            return 0;
        }

        int removed = 0;
        for (int i = 0; i < invalidIds.Count; i++)
        {
            int id = invalidIds[i];
            if (id == EmptyCell)
            {
                continue;
            }

            RemoveBuilding(id);
            removed++;
        }

        return removed;
    }

    public int CountExactBuildingKeyForTests(string buildingKey)
    {
        if (string.IsNullOrWhiteSpace(buildingKey))
        {
            return 0;
        }

        int count = 0;
        foreach (PlacedBuilding building in buildings.Values)
        {
            if (building != null && string.Equals(building.buildingKey, buildingKey, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    public bool DeactivateBuildingVisualForTests(string buildingKey)
    {
        if (!TryGetBuildingByKey(buildingKey, out PlacedBuilding building) || building.gameObject == null)
        {
            return false;
        }

        building.gameObject.SetActive(false);
        return CountEnabledRenderers(building.gameObject.transform) <= 0;
    }

    private bool MoveExternalDockToSlot(string dockKey, string targetSlotId, bool allowSwap)
    {
        string normalizedDockKey = string.IsNullOrWhiteSpace(dockKey) ? "" : dockKey.Trim();
        string normalizedTargetSlotId = string.IsNullOrWhiteSpace(targetSlotId) ? "" : targetSlotId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDockKey)
            || string.IsNullOrWhiteSpace(normalizedTargetSlotId)
            || !externalDockPlacements.TryGetValue(normalizedDockKey, out ExternalDockPlacement placement)
            || !HasExternalDockSlot(normalizedTargetSlotId))
        {
            return false;
        }

        string sourceSlotId = placement.slotId;
        if (sourceSlotId == normalizedTargetSlotId)
        {
            return true;
        }

        ExternalDockPlacement targetPlacement = TryGetExternalDockInSlot(normalizedTargetSlotId, out ExternalDockPlacement found)
            ? found
            : null;
        if (targetPlacement != null && targetPlacement.dockKey != normalizedDockKey)
        {
            if (!allowSwap)
            {
                return false;
            }

            targetPlacement.slotId = sourceSlotId;
        }

        placement.slotId = normalizedTargetSlotId;
        RefreshExternalDockVisuals();
        return true;
    }

    private bool HasExternalDockSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return false;
        }

        string normalizedSlotId = slotId.Trim();
        for (int i = 0; i < externalDockSlots.Count; i++)
        {
            if (externalDockSlots[i].dockId == normalizedSlotId)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetExternalDockInSlot(string slotId, out ExternalDockPlacement placement)
    {
        placement = null;
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return false;
        }

        string normalizedSlotId = slotId.Trim();
        foreach (ExternalDockPlacement candidate in externalDockPlacements.Values)
        {
            if (candidate != null && candidate.slotId == normalizedSlotId)
            {
                placement = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryGetExternalDockObject(string slotId, out GameObject dockObject)
    {
        dockObject = null;
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return false;
        }

        string normalizedSlotId = slotId.Trim();
        for (int i = 0; i < externalDockObjects.Count; i++)
        {
            GameObject candidate = externalDockObjects[i];
            if (candidate == null)
            {
                continue;
            }

            IsoGridCityExternalDockHandle handle = candidate.GetComponent<IsoGridCityExternalDockHandle>();
            if (handle != null && handle.slotId == normalizedSlotId)
            {
                dockObject = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetExternalDockBerthPoseForRuntime(
        string dockKey,
        out Vector3 berthPosition,
        out Quaternion berthRotation,
        out float berthLength,
        out float berthWidth,
        out BaseIslandExternalDockOrientation orientation)
    {
        berthPosition = Vector3.zero;
        berthRotation = Quaternion.identity;
        berthLength = 0f;
        berthWidth = 0f;
        orientation = BaseIslandExternalDockOrientation.South;
        if (string.IsNullOrWhiteSpace(dockKey))
        {
            return false;
        }

        string normalizedDockKey = dockKey.Trim();
        if (!externalDockPlacements.TryGetValue(normalizedDockKey, out ExternalDockPlacement placement)
            || placement == null
            || !TryGetExternalDockSlot(placement.slotId, out BaseIslandExternalDockSlotView slot))
        {
            return false;
        }

        bool northSouth = slot.orientation == BaseIslandExternalDockOrientation.North
            || slot.orientation == BaseIslandExternalDockOrientation.South;
        float contactWidth = Mathf.Max(1, slot.contactWidth) * cellSize * 0.92f;
        float protrusionLength = Mathf.Max(1, slot.protrusionLength) * cellSize * 0.92f;
        float visualWidth = GetExternalDockVisualContactWidth(contactWidth, true);
        float visualLength = GetExternalDockVisualProtrusionLength(protrusionLength, true);
        Vector3 outward = GetExternalDockOutwardDirection(slot.orientation);
        berthPosition = new Vector3(slot.centerX * cellSize, ExternalDockShipBerthHeight, slot.centerY * cellSize)
            + outward * (visualLength * 0.08f);
        berthRotation = Quaternion.Euler(0f, GetExternalDockShipYawDegrees(slot.orientation), 0f);
        berthLength = visualLength * 0.90f;
        berthWidth = visualWidth * (northSouth ? 0.78f : 0.78f);
        orientation = slot.orientation;
        return true;
    }

    private bool TryGetExternalDockSlot(string slotId, out BaseIslandExternalDockSlotView slot)
    {
        slot = default;
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return false;
        }

        string normalizedSlotId = slotId.Trim();
        for (int i = 0; i < externalDockSlots.Count; i++)
        {
            if (externalDockSlots[i].dockId == normalizedSlotId)
            {
                slot = externalDockSlots[i];
                return true;
            }
        }

        return false;
    }

    private static string GetDefinitionIdFromInstanceKey(string instanceKey)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
        {
            return "";
        }

        string normalizedKey = instanceKey.Trim();
        int marker = normalizedKey.IndexOf("__", StringComparison.Ordinal);
        return marker >= 0 ? normalizedKey.Substring(0, marker) : normalizedKey;
    }

    public bool BeginMoveBuildingForTests(string buildingKey)
    {
        if (!TryGetBuildingByKey(buildingKey, out PlacedBuilding building))
        {
            return false;
        }

        BeginMove(building.id);
        return IsMoving && movingBuilding != null && movingBuilding.buildingKey == buildingKey;
    }

    public bool PlaceMovingBuildingForTests(int originX, int originY)
    {
        if (!IsMoving || !CanPlaceAt(originX, originY, ActiveWidth, ActiveHeight))
        {
            return false;
        }

        PlaceActiveBuilding(originX, originY);
        return !IsMoving;
    }

    public void CancelMoveForTests()
    {
        CancelMove();
    }

    public bool BuildingUsesPrefabForTests(string buildingKey)
    {
        return TryGetBuildingByKey(buildingKey, out PlacedBuilding building)
            && building.loadedFromPrefab;
    }

    public bool BuildingHasColliderForTests(string buildingKey)
    {
        return TryGetBuildingByKey(buildingKey, out PlacedBuilding building)
            && building.gameObject != null
            && building.gameObject.GetComponent<Collider>() != null;
    }

    public bool BuildingHasRendererForTests(string buildingKey)
    {
        return TryGetBuildingByKey(buildingKey, out PlacedBuilding building)
            && building.gameObject != null
            && building.gameObject.GetComponentsInChildren<Renderer>(true).Length > 0;
    }

    public bool BuildingHasEnabledRendererForTests(string buildingKey)
    {
        return TryGetBuildingByKey(buildingKey, out PlacedBuilding building)
            && building.gameObject != null
            && CountEnabledRenderers(building.gameObject.transform) > 0;
    }

    public bool BuildingHasCameraVisibleRendererForTests(string buildingKey)
    {
        return TryGetBuildingByKey(buildingKey, out PlacedBuilding building)
            && building.gameObject != null
            && CountCameraVisibleRenderers(building.gameObject.transform) > 0;
    }

    public bool TryGetBuildingFootprintForTests(string buildingKey, out int width, out int height)
    {
        if (TryGetBuildingByKey(buildingKey, out PlacedBuilding building))
        {
            width = building.width;
            height = building.height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    public bool TryGetBuildingScreenAnchorForRuntime(string buildingKey, out Vector2 screenPosition)
    {
        screenPosition = default;
        if (sceneCamera == null || !TryGetBuildingByKey(buildingKey, out PlacedBuilding building) || building.gameObject == null)
        {
            return false;
        }

        if (!TryGetWorldRendererBounds(building.gameObject, out Bounds bounds))
        {
            Collider collider = building.gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return false;
            }

            bounds = collider.bounds;
        }

        Vector3 world = new Vector3(bounds.center.x, bounds.max.y + 0.55f, bounds.center.z);
        Vector3 screen = sceneCamera.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
            return false;
        }

        screenPosition = new Vector2(screen.x, screen.y);
        return true;
    }

    public bool TryGetBuildingWorldAnchorForRuntime(string buildingKey, out Vector3 worldPosition)
    {
        worldPosition = default;
        if (!TryGetBuildingByKey(buildingKey, out PlacedBuilding building) || building.gameObject == null)
        {
            return false;
        }

        if (!TryGetWorldRendererBounds(building.gameObject, out Bounds bounds))
        {
            Collider collider = building.gameObject.GetComponent<Collider>();
            if (collider == null)
            {
                return false;
            }

            bounds = collider.bounds;
        }

        worldPosition = new Vector3(bounds.center.x, bounds.max.y + 0.55f, bounds.center.z);
        return true;
    }

    private void BuildPrototype()
    {
        gridWidth = Mathf.Max(4, gridWidth);
        gridHeight = Mathf.Max(4, gridHeight);
        selectedSize = ClampBuildingDimension(selectedSize);
        selectedWidth = ClampBuildingDimension(selectedWidth <= 0 ? selectedSize : selectedWidth);
        selectedHeight = ClampBuildingDimension(selectedHeight <= 0 ? selectedSize : selectedHeight);

        CreateMaterials();
        EnsureCameraAndLight();
        BuildGrid();
        BuildHud();
    }

    private bool EnsureRuntimeGridReady()
    {
        if (HasRuntimeGrid())
        {
            return true;
        }

        DestroyGeneratedRuntimeRoot(islandRoot, "Floating Island");
        DestroyGeneratedRuntimeRoot(tileRoot, "Grid Tiles");
        DestroyGeneratedRuntimeRoot(buildingRoot, "Buildings");
        DestroyGeneratedRuntimeRoot(expansionMarkerRoot, "Expansion Markers");
        DestroyGeneratedRuntimeRoot(externalDockRoot, "External Dock Slots");

        islandRoot = null;
        tileRoot = null;
        buildingRoot = null;
        expansionMarkerRoot = null;
        externalDockRoot = null;
        cells = null;
        occupancy = null;
        cellAccess = null;
        cellRegionIds = null;
        movingBuilding = null;
        dragMoveActive = false;
        externalDockDragActive = false;
        movingExternalDockKey = "";
        hoverExternalDockSlotId = "";
        hoverExpansionRegionId = "";
        pressedBuildingId = EmptyCell;
        pressedExternalDockKey = "";
        nextBuildingId = 1;
        expansionRegionSignature = "";
        externalDockSlotSignature = "";
        buildings.Clear();
        expansionMarkers.Clear();
        externalDockSlots.Clear();
        externalDockPlacements.Clear();
        externalDockObjects.Clear();
        DestroyHoverOutline();
        HideGhost();

        BuildPrototype();
        return HasRuntimeGrid();
    }

    private bool HasRuntimeGrid()
    {
        return gridWidth > 0
            && gridHeight > 0
            && cells != null
            && occupancy != null
            && cellAccess != null
            && cellRegionIds != null
            && cells.GetLength(0) == gridWidth
            && cells.GetLength(1) == gridHeight
            && occupancy.GetLength(0) == gridWidth
            && occupancy.GetLength(1) == gridHeight
            && cellAccess.GetLength(0) == gridWidth
            && cellAccess.GetLength(1) == gridHeight
            && cellRegionIds.GetLength(0) == gridWidth
            && cellRegionIds.GetLength(1) == gridHeight;
    }

    private void DestroyGeneratedRuntimeRoot(Transform cachedRoot, string rootName)
    {
        Transform root = cachedRoot != null ? cachedRoot : transform.Find(rootName);
        if (root == null)
        {
            return;
        }

        DestroyRuntimeObject(root.gameObject);
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void CreateMaterials()
    {
        tileMaterial = CreateMaterial(new Color(0.22f, 0.28f, 0.30f, 1f));
        occupiedMaterial = CreateMaterial(new Color(0.34f, 0.38f, 0.40f, 1f));
        validMaterial = CreateMaterial(new Color(0.24f, 0.70f, 0.45f, 1f));
        invalidMaterial = CreateMaterial(new Color(0.86f, 0.25f, 0.22f, 1f));
        ghostValidMaterial = CreateMaterial(new Color(0.20f, 0.86f, 0.50f, 0.50f));
        ghostInvalidMaterial = CreateMaterial(new Color(0.95f, 0.18f, 0.16f, 0.50f));
        hoverOutlineMaterial = CreateOutlineMaterial(new Color(1f, 0.83f, 0.22f, 1f));
        fogMaterial = CreateMaterial(new Color(0.54f, 0.61f, 0.64f, 0.96f));
        debrisMaterial = CreateMaterial(new Color(0.36f, 0.34f, 0.29f, 1f));
        clearingMaterial = CreateMaterial(new Color(0.54f, 0.46f, 0.24f, 1f));
        expansionMarkerMaterial = CreateMaterial(new Color(0.66f, 0.54f, 0.32f, 1f));
        externalDockEmptyMaterial = CreateMaterial(new Color(0.18f, 0.16f, 0.12f, 0.72f));
        externalDockDeckMaterial = CreateMaterial(new Color(0.38f, 0.30f, 0.20f, 1f));
        externalDockAccentMaterial = CreateMaterial(new Color(0.74f, 0.58f, 0.32f, 1f));
        islandTopMaterial = CreateMaterial(new Color(0.20f, 0.34f, 0.27f, 1f));
        islandSideMaterial = CreateMaterial(new Color(0.30f, 0.25f, 0.20f, 1f));
        islandBottomMaterial = CreateMaterial(new Color(0.18f, 0.16f, 0.15f, 1f));
        buildingMaterials = new[]
        {
            CreateMaterial(new Color(0.42f, 0.56f, 0.82f, 1f)),
            CreateMaterial(new Color(0.78f, 0.58f, 0.30f, 1f)),
            CreateMaterial(new Color(0.56f, 0.72f, 0.50f, 1f)),
            CreateMaterial(new Color(0.70f, 0.46f, 0.64f, 1f))
        };
    }

    private void EnsureCameraAndLight()
    {
        EnsureSceneCamera();
        cameraPivot = IslandOrbitCenterWorld();
        cameraMinPitchDegrees = Mathf.Clamp(cameraMinPitchDegrees, -35f, 60f);
        cameraMaxPitchDegrees = Mathf.Clamp(cameraMaxPitchDegrees, cameraMinPitchDegrees + 5f, 88f);
        cameraPitchDegrees = Mathf.Clamp(cameraPitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        ApplyDefaultCameraPose();

        if (FindFirstObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Iso Grid City Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }
    }

    private void EnsureSceneCamera()
    {
        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (sceneCamera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Iso Grid City Camera");
        sceneCamera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
    }

    private void ApplyDefaultCameraPose()
    {
        cameraPivot = IslandOrbitCenterWorld();
        cameraYawDegrees = 45f;
        cameraPitchDegrees = Mathf.Clamp(58f, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        ConfigureSceneCameraForCity();
        SetCameraZoomSize(GetDefaultCameraOrthographicSize());
        ApplyCameraTransform();
        InvalidateHoverOutlineCache();
    }

    private void ConfigureSceneCameraForCity()
    {
        if (sceneCamera == null)
        {
            return;
        }

        sceneCamera.orthographic = false;
        sceneCamera.fieldOfView = Mathf.Clamp(cameraFieldOfViewDegrees, 20f, 70f);
        RefreshCameraZoomLimits();
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = Mathf.Max(sceneCamera.farClipPlane, SessionCameraMinFarClipPlane);
        sceneCamera.backgroundColor = new Color(0.12f, 0.15f, 0.17f, 1f);
    }

    private float GetCameraZoomSize()
    {
        if (sceneCamera == null)
        {
            return CameraDistanceToEquivalentOrthographicSize(cameraDistance);
        }

        if (sceneCamera.orthographic)
        {
            return sceneCamera.orthographicSize;
        }

        return CameraDistanceToEquivalentOrthographicSize(cameraDistance);
    }

    private void SetCameraZoomSize(float zoomSize)
    {
        RefreshCameraZoomLimits();
        float clampedZoomSize = Mathf.Clamp(zoomSize, cameraMinOrthographicSize, cameraMaxOrthographicSize);
        if (sceneCamera != null && sceneCamera.orthographic)
        {
            sceneCamera.orthographicSize = clampedZoomSize;
            return;
        }

        cameraDistance = EquivalentOrthographicSizeToCameraDistance(clampedZoomSize);
    }

    private float EquivalentOrthographicSizeToCameraDistance(float zoomSize)
    {
        float halfFovRadians = Mathf.Clamp(cameraFieldOfViewDegrees, 20f, 70f) * Mathf.Deg2Rad * 0.5f;
        return Mathf.Max(0.1f, zoomSize / Mathf.Tan(halfFovRadians));
    }

    private float CameraDistanceToEquivalentOrthographicSize(float distance)
    {
        float halfFovRadians = Mathf.Clamp(cameraFieldOfViewDegrees, 20f, 70f) * Mathf.Deg2Rad * 0.5f;
        return Mathf.Max(0.01f, Mathf.Max(0.1f, distance) * Mathf.Tan(halfFovRadians));
    }

    private float GetDefaultCameraOrthographicSize()
    {
        if (sceneCamera == null)
        {
            return cameraMaxOrthographicSize;
        }

        RefreshCameraZoomLimits();
        return Mathf.Clamp(
            Mathf.Max(gridWidth, gridHeight) * 0.58f,
            cameraMinOrthographicSize,
            cameraMaxOrthographicSize);
    }

    private void BuildGrid()
    {
        islandRoot = new GameObject("Floating Island").transform;
        islandRoot.SetParent(transform, false);
        tileRoot = new GameObject("Grid Tiles").transform;
        tileRoot.SetParent(transform, false);
        buildingRoot = new GameObject("Buildings").transform;
        buildingRoot.SetParent(transform, false);
        expansionMarkerRoot = new GameObject("Expansion Markers").transform;
        expansionMarkerRoot.SetParent(transform, false);
        externalDockRoot = new GameObject("External Dock Slots").transform;
        externalDockRoot.SetParent(transform, false);

        BuildFloatingIsland();

        cells = new IsoGridCityCell[gridWidth, gridHeight];
        occupancy = new int[gridWidth, gridHeight];
        cellAccess = new CityCellAccess[gridWidth, gridHeight];
        cellRegionIds = new string[gridWidth, gridHeight];
        MarkTilesDirty();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                cellAccess[x, y] = CityCellAccess.Open;
                cellRegionIds[x, y] = "";

                cells[x, y] = new IsoGridCityCell
                {
                    x = x,
                    y = y
                };
            }
        }

        GameObject groundColliderObject = new GameObject("Grid Ground Collider");
        SetCityRaycastLayer(groundColliderObject);
        groundColliderObject.transform.SetParent(tileRoot, false);
        groundColliderObject.transform.position = GridCenterWorld() + Vector3.down * (TileHeight * 0.5f);
        BoxCollider groundCollider = groundColliderObject.AddComponent<BoxCollider>();
        groundCollider.size = new Vector3(gridWidth * cellSize, TileHeight, gridHeight * cellSize);
        groundColliderObject.AddComponent<IsoGridCityGroundHandle>();
    }

    private void BuildFloatingIsland()
    {
        if (islandRoot == null) return;

        float padding = Mathf.Max(0.75f, islandPaddingCells) * cellSize;
        float dropDepth = Mathf.Max(18f, islandDropDepth);
        float bottomScale = Mathf.Clamp(islandBottomScale, 0.12f, 0.34f);
        Vector3 center = GridCenterWorld();
        float topY = -TileHeight - 0.045f;
        float rimY = -dropDepth * 0.035f;
        float shoulderY = -dropDepth * 0.18f;
        float midY = -dropDepth * 0.52f;
        float lowerY = -dropDepth * 0.83f;
        float bottomY = -dropDepth;
        float gridMinX = -cellSize * 0.5f;
        float gridMaxX = (gridWidth - 1) * cellSize + cellSize * 0.5f;
        float gridMinZ = -cellSize * 0.5f;
        float gridMaxZ = (gridHeight - 1) * cellSize + cellSize * 0.5f;
        float midX = (gridMinX + gridMaxX) * 0.5f;
        float midZ = (gridMinZ + gridMaxZ) * 0.5f;

        Vector2[] outline = new[]
        {
            new Vector2(gridMinX - padding * 0.90f, gridMinZ - padding * 0.65f),
            new Vector2(gridMinX + cellSize * 7f, gridMinZ - padding * 1.06f),
            new Vector2(midX - cellSize * 3f, gridMinZ - padding * 0.82f),
            new Vector2(gridMaxX - cellSize * 7f, gridMinZ - padding * 1.12f),
            new Vector2(gridMaxX + padding * 0.92f, gridMinZ - padding * 0.70f),
            new Vector2(gridMaxX + padding * 1.10f, gridMinZ + cellSize * 8f),
            new Vector2(gridMaxX + padding * 0.82f, midZ + cellSize * 1.5f),
            new Vector2(gridMaxX + padding * 1.02f, gridMaxZ - cellSize * 7f),
            new Vector2(gridMaxX + padding * 0.72f, gridMaxZ + padding * 0.95f),
            new Vector2(gridMaxX - cellSize * 8f, gridMaxZ + padding * 1.08f),
            new Vector2(midX + cellSize * 2f, gridMaxZ + padding * 0.82f),
            new Vector2(gridMinX + cellSize * 6f, gridMaxZ + padding * 1.02f),
            new Vector2(gridMinX - padding * 0.82f, gridMaxZ + padding * 0.76f),
            new Vector2(gridMinX - padding * 1.08f, gridMaxZ - cellSize * 7f),
            new Vector2(gridMinX - padding * 0.78f, midZ - cellSize * 1f),
            new Vector2(gridMinX - padding * 1.00f, gridMinZ + cellSize * 7f)
        };

        Vector3[] topRing = new Vector3[outline.Length];
        Vector3[] rimRing = new Vector3[outline.Length];
        Vector3[] shoulderRing = new Vector3[outline.Length];
        Vector3[] midRing = new Vector3[outline.Length];
        Vector3[] lowerRing = new Vector3[outline.Length];
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 point = outline[i];
            topRing[i] = new Vector3(point.x, topY, point.y);

            Vector2 fromCenter = point - new Vector2(center.x, center.z);
            float rimScale = 0.99f + (i % 2 == 0 ? 0.012f : -0.018f);
            float shoulderScale = 0.92f + (i % 2 == 0 ? 0.045f : -0.035f);
            float midScale = 0.58f + (i % 3 == 0 ? 0.06f : -0.035f);
            float lowerScale = Mathf.Lerp(0.30f, bottomScale, i % 3 == 0 ? 0.35f : 0.0f);
            rimRing[i] = new Vector3(
                center.x + fromCenter.x * rimScale,
                rimY + ((i % 3) - 1) * 0.12f,
                center.z + fromCenter.y * rimScale);
            shoulderRing[i] = new Vector3(
                center.x + fromCenter.x * shoulderScale,
                shoulderY + ((i % 4) - 1.5f) * 0.22f,
                center.z + fromCenter.y * shoulderScale);
            midRing[i] = new Vector3(
                center.x + fromCenter.x * midScale,
                midY + (i % 2 == 0 ? 0.30f : -0.24f),
                center.z + fromCenter.y * midScale);
            lowerRing[i] = new Vector3(
                center.x + fromCenter.x * lowerScale,
                lowerY + (i % 2 == 0 ? 0.34f : -0.26f),
                center.z + fromCenter.y * lowerScale);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<int> topTriangles = new List<int>();
        List<int> sideTriangles = new List<int>();
        List<int> bottomTriangles = new List<int>();

        int topCenterIndex = vertices.Count;
        vertices.Add(new Vector3(center.x, topY, center.z));
        int[] topIndices = new int[topRing.Length];
        for (int i = 0; i < topRing.Length; i++)
        {
            topIndices[i] = vertices.Count;
            vertices.Add(topRing[i]);
        }

        for (int i = 0; i < topRing.Length; i++)
        {
            int next = (i + 1) % topRing.Length;
            topTriangles.Add(topCenterIndex);
            topTriangles.Add(topIndices[next]);
            topTriangles.Add(topIndices[i]);

            AddMeshQuad(vertices, sideTriangles, topRing[i], topRing[next], rimRing[next], rimRing[i]);
            AddMeshQuad(vertices, sideTriangles, rimRing[i], rimRing[next], shoulderRing[next], shoulderRing[i]);
            AddMeshQuad(vertices, sideTriangles, shoulderRing[i], shoulderRing[next], midRing[next], midRing[i]);
            AddMeshQuad(vertices, sideTriangles, midRing[i], midRing[next], lowerRing[next], lowerRing[i]);
            AddMeshTriangle(
                vertices,
                bottomTriangles,
                lowerRing[i],
                lowerRing[next],
                new Vector3(center.x, bottomY, center.z));
        }

        Mesh mesh = new Mesh();
        mesh.name = "Iso Grid Floating Island Mesh";
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(topTriangles, 0);
        mesh.SetTriangles(sideTriangles, 1);
        mesh.SetTriangles(bottomTriangles, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject island = new GameObject("Faceted Rock");
        island.transform.SetParent(islandRoot, false);
        MeshFilter meshFilter = island.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        MeshRenderer meshRenderer = island.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = new[] { islandTopMaterial, islandSideMaterial, islandBottomMaterial };
    }

    private static void AddMeshQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private static void AddMeshTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private void BuildHud()
    {
        if (!showPrototypeToolbar)
        {
            HidePrototypeToolbar();
            return;
        }

        hudCanvas = hudCanvas != null ? hudCanvas : GetComponentInChildren<Canvas>(true);
        if (hudCanvas == null)
        {
            GameObject canvasObject = new GameObject("Iso Grid City HUD");
            canvasObject.transform.SetParent(transform, false);
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 825;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        hudCanvas.gameObject.SetActive(true);
        EnsureEventSystem();

        RectTransform panel = CreateUiRect("Toolbar", hudCanvas.transform, new Vector2(18f, -18f), new Vector2(560f, 96f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.10f, 0.11f, 0.86f);

        CreateButton(panel, "Мышь", new Vector2(12f, -12f), SelectFreeMouseMode, 76f);
        CreateButton(panel, "2x2", new Vector2(92f, -12f), () => SelectSize(2));
        CreateButton(panel, "3x3", new Vector2(172f, -12f), () => SelectSize(3));
        CreateButton(panel, "4x4", new Vector2(252f, -12f), () => SelectSize(4));
        CreateButton(panel, "Убрать", new Vector2(332f, -12f), ToggleRemoveMode, 92f);
        CreateButton(panel, "Очистить", new Vector2(428f, -12f), ClearBuildings, 84f);

        modeText = CreateText(panel, "", 17, new Vector2(12f, -58f), new Vector2(340f, 24f), TextAnchor.MiddleLeft);
        countText = CreateText(panel, "", 17, new Vector2(404f, -58f), new Vector2(108f, 24f), TextAnchor.MiddleRight);
    }

    private void HidePrototypeToolbar()
    {
        hudCanvas = hudCanvas != null ? hudCanvas : GetComponentInChildren<Canvas>(true);
        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(false);
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject eventSystemObject = new GameObject("Iso Grid City EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private RectTransform CreateUiRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private Button CreateButton(RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action, float width = 68f)
    {
        RectTransform rect = CreateUiRect(label + " Button", parent, anchoredPosition, new Vector2(width, 40f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.27f, 1f);

        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.26f, 0.34f, 0.38f, 1f);
        colors.pressedColor = new Color(0.12f, 0.16f, 0.18f, 1f);
        button.colors = colors;

        CreateText(rect, label, 18, Vector2.zero, rect.sizeDelta, TextAnchor.MiddleCenter);
        return button;
    }

    private Text CreateText(Transform parent, string value, int size, Vector2 anchoredPosition, Vector2 rectSize, TextAnchor anchor)
    {
        RectTransform rect = CreateUiRect("Text", parent, anchoredPosition, rectSize);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = size;
        text.alignment = anchor;
        text.color = new Color(0.88f, 0.92f, 0.90f, 1f);
        text.text = value;
        return text;
    }

    private void ReadKeyboardShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame) SelectSize(2);
        if (keyboard.digit2Key.wasPressedThisFrame) SelectSize(3);
        if (keyboard.digit3Key.wasPressedThisFrame) SelectSize(4);
        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) SelectFreeMouseMode();
        if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) ToggleRemoveMode();
        if (keyboard.escapeKey.wasPressedThisFrame) CancelMove();
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSize(2);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSize(3);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSize(4);
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Space)) SelectFreeMouseMode();
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) ToggleRemoveMode();
        if (Input.GetKeyDown(KeyCode.Escape)) CancelMove();
#endif
    }

    private void HandleCameraInput()
    {
        pointerCameraControlActive = false;
        if (sceneCamera == null) return;

        bool rightHeld;
        bool middleHeld;
        Vector2 pointerDelta;
        float scrollY;
        ReadCameraPointerInput(out rightHeld, out middleHeld, out pointerDelta, out scrollY);

        if (portDockOrbitActive)
        {
            pointerCameraControlActive = HandlePortDockCameraInput(rightHeld, middleHeld, pointerDelta, scrollY);
            ApplyCameraTransform();
            return;
        }

        if ((rightHeld || middleHeld || Mathf.Abs(scrollY) > 0.001f) &&
            (WildWindGameplayHud.IsHudPointerCaptureActiveForCamera || IsPointerOverUi()))
        {
            pointerCameraControlActive = true;
            ApplyCameraTransform();
            return;
        }

        if (rightHeld)
        {
            pointerCameraControlActive = true;
            cameraYawDegrees += pointerDelta.x * cameraOrbitSensitivity;
            cameraPitchDegrees = Mathf.Clamp(
                cameraPitchDegrees - pointerDelta.y * cameraOrbitSensitivity,
                cameraMinPitchDegrees,
                cameraMaxPitchDegrees);

            Vector2 moveInput = ReadCameraMoveInput();
            if (moveInput.sqrMagnitude > 0.001f)
            {
                MoveCameraPivot(moveInput);
            }
        }

        if (middleHeld)
        {
            pointerCameraControlActive = true;
            PanCameraPivot(pointerDelta);
        }

        float scrollSteps = NormalizeScrollSteps(scrollY);
        if (Mathf.Abs(scrollSteps) > 0.001f)
        {
            pointerCameraControlActive = true;
            RefreshCameraZoomLimits();
            float zoomFactor = Mathf.Exp(-scrollSteps * CameraZoomStepLogScale());
            SetCameraZoomSize(GetCameraZoomSize() * zoomFactor);
        }

        ApplyCameraTransform();
    }

    private bool HandlePortDockCameraInput(bool rightHeld, bool middleHeld, Vector2 pointerDelta, float scrollY)
    {
        bool handled = false;
        if (rightHeld || middleHeld)
        {
            handled = true;
            portDockOrbitAngleDegrees -= pointerDelta.x * PortDockOrbitAngleSensitivity;
            portDockOrbitPitchDegrees = Mathf.Clamp(
                portDockOrbitPitchDegrees - pointerDelta.y * PortDockOrbitAngleSensitivity,
                PortDockOrbitMinPitchDegrees,
                PortDockOrbitMaxPitchDegrees);
        }

        Vector2 moveInput = ReadCameraMoveInput();
        if (Mathf.Abs(moveInput.x) > 0.001f)
        {
            handled = true;
            portDockOrbitAngleDegrees += moveInput.x * PortDockOrbitKeyboardDegreesPerSecond * Time.deltaTime;
        }

        float scrollSteps = NormalizeScrollSteps(scrollY);
        if (Mathf.Abs(scrollSteps) > 0.001f)
        {
            handled = true;
            RefreshCameraZoomLimits();
            float zoomFactor = Mathf.Exp(-scrollSteps * CameraZoomStepLogScale());
            float baseZoom = Mathf.Max(0.01f, portDockOrbitBaseZoomSize);
            float targetZoom = Mathf.Clamp(
                GetCameraZoomSize() * zoomFactor,
                baseZoom * PortDockOrbitMinZoomScale,
                baseZoom * PortDockOrbitMaxZoomScale);
            SetCameraZoomSize(targetZoom);
        }

        return handled;
    }

    private bool ApplyCameraHomeBlend()
    {
        if (!cameraHomeBlendActive)
        {
            return false;
        }

        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            cameraHomeBlendActive = false;
            cameraTransformBlendActive = false;
            return false;
        }

        if (cameraTransformBlendActive)
        {
            return ApplyCameraTransformBlend();
        }

        if (portDockOrbitActive && pendingPortDockOrbitActive)
        {
            return ApplyPortDockOrbitBlend();
        }

        float duration = Mathf.Max(0.01f, cameraHomeBlendDuration);
        float ratio = Mathf.Clamp01((Time.unscaledTime - cameraHomeBlendStartedAt) / duration);
        float easedRatio = ratio * ratio * (3f - 2f * ratio);

        cameraPivot = Vector3.Lerp(cameraHomeBlendStartPivot, cameraHomeBlendTargetPivot, easedRatio);
        cameraYawDegrees = Mathf.LerpAngle(cameraHomeBlendStartYawDegrees, cameraHomeBlendTargetYawDegrees, easedRatio);
        cameraPitchDegrees = Mathf.Lerp(cameraHomeBlendStartPitchDegrees, cameraHomeBlendTargetPitchDegrees, easedRatio);
        ConfigureSceneCameraForCity();
        SetCameraZoomSize(Mathf.Lerp(
            cameraHomeBlendStartOrthographicSize,
            Mathf.Clamp(cameraHomeBlendTargetOrthographicSize, cameraMinOrthographicSize, cameraMaxOrthographicSize),
            easedRatio));
        ApplyCameraTransform();

        if (ratio >= 1f)
        {
            cameraPivot = cameraHomeBlendTargetPivot;
            cameraYawDegrees = cameraHomeBlendTargetYawDegrees;
            cameraPitchDegrees = cameraHomeBlendTargetPitchDegrees;
            SetCameraZoomSize(cameraHomeBlendTargetOrthographicSize);
            ApplyCameraTransform();
            cameraHomeBlendActive = false;
            if (pendingPortDockOrbitActive)
            {
                ActivatePendingPortDockCameraOrbit();
            }
        }

        InvalidateHoverOutlineCache();
        return true;
    }

    private bool ApplyCameraTransformBlend()
    {
        if (sceneCamera == null)
        {
            cameraHomeBlendActive = false;
            cameraTransformBlendActive = false;
            return false;
        }

        float duration = Mathf.Max(0.01f, cameraHomeBlendDuration);
        float ratio = Mathf.Clamp01((Time.unscaledTime - cameraHomeBlendStartedAt) / duration);
        float easedRatio = ratio * ratio * (3f - 2f * ratio);

        Vector3 position = Vector3.Lerp(cameraTransformBlendStartPosition, cameraTransformBlendTargetPosition, easedRatio);
        Quaternion rotation = Quaternion.Slerp(cameraTransformBlendStartRotation, cameraTransformBlendTargetRotation, easedRatio);
        SetCameraZoomSize(Mathf.Lerp(
            cameraTransformBlendStartOrthographicSize,
            cameraTransformBlendTargetOrthographicSize,
            easedRatio));
        sceneCamera.transform.SetPositionAndRotation(position, rotation);

        if (ratio >= 1f)
        {
            SetCameraZoomSize(cameraTransformBlendTargetOrthographicSize);
            cameraHomeBlendActive = false;
            cameraTransformBlendActive = false;
            if (cameraTransformBlendActivatesPendingPortDockOrbit)
            {
                ActivatePendingPortDockCameraOrbit();
            }
            else
            {
                cameraPivot = cameraTransformBlendTargetPivot;
                cameraYawDegrees = cameraTransformBlendTargetYawDegrees;
                cameraPitchDegrees = Mathf.Clamp(
                    cameraTransformBlendTargetPitchDegrees,
                    cameraMinPitchDegrees,
                    cameraMaxPitchDegrees);
                ApplyCameraTransform();
            }
        }

        InvalidateHoverOutlineCache();
        return true;
    }

    private bool ApplyPortDockOrbitBlend()
    {
        float duration = Mathf.Max(0.01f, cameraHomeBlendDuration);
        float ratio = Mathf.Clamp01((Time.unscaledTime - cameraHomeBlendStartedAt) / duration);
        float easedRatio = ratio * ratio * (3f - 2f * ratio);

        portDockOrbitFocus = Vector3.Lerp(portDockOrbitBlendStartFocus, pendingPortDockOrbitFocus, easedRatio);
        portDockOrbitForward = SanitizeHorizontalAxis(
            Vector3.Slerp(portDockOrbitBlendStartForward, pendingPortDockOrbitForward, easedRatio),
            pendingPortDockOrbitForward);
        portDockOrbitRight = SanitizeHorizontalAxis(
            Vector3.Slerp(portDockOrbitBlendStartRight, pendingPortDockOrbitRight, easedRatio),
            pendingPortDockOrbitRight);
        portDockOrbitBaseMajorRadius = Mathf.Lerp(
            portDockOrbitBlendStartBaseMajorRadius,
            pendingPortDockOrbitBaseMajorRadius,
            easedRatio);
        portDockOrbitBaseMinorRadius = Mathf.Lerp(
            portDockOrbitBlendStartBaseMinorRadius,
            pendingPortDockOrbitBaseMinorRadius,
            easedRatio);
        portDockOrbitBaseZoomSize = Mathf.Lerp(
            portDockOrbitBlendStartBaseZoomSize,
            pendingPortDockOrbitBaseZoomSize,
            easedRatio);
        portDockOrbitBaseCameraDistance = Mathf.Lerp(
            portDockOrbitBlendStartBaseCameraDistance,
            pendingPortDockOrbitBaseCameraDistance,
            easedRatio);
        portDockOrbitAngleDegrees = Mathf.LerpAngle(
            portDockOrbitBlendStartAngleDegrees,
            pendingPortDockOrbitAngleDegrees,
            easedRatio);
        portDockOrbitPitchDegrees = Mathf.Lerp(
            portDockOrbitBlendStartPitchDegrees,
            pendingPortDockOrbitPitchDegrees,
            easedRatio);
        SetCameraZoomSize(Mathf.Lerp(
            portDockOrbitBlendStartBaseZoomSize,
            pendingPortDockOrbitBaseZoomSize,
            easedRatio));
        ApplyCameraTransform();

        if (ratio >= 1f)
        {
            cameraHomeBlendActive = false;
            ActivatePendingPortDockCameraOrbit();
        }

        InvalidateHoverOutlineCache();
        return true;
    }

    private void StartCameraTransformBlend(
        Vector3 targetPosition,
        Quaternion targetRotation,
        float targetOrthographicSize,
        Vector3 targetPivot,
        float targetYawDegrees,
        float targetPitchDegrees,
        bool activatesPendingPortDockOrbit)
    {
        EnsureSceneCamera();
        if (sceneCamera == null)
        {
            return;
        }

        cameraTransformBlendStartPosition = sceneCamera.transform.position;
        cameraTransformBlendTargetPosition = targetPosition;
        cameraTransformBlendStartRotation = sceneCamera.transform.rotation;
        cameraTransformBlendTargetRotation = targetRotation;
        cameraTransformBlendStartOrthographicSize = GetCameraZoomSize();
        cameraTransformBlendTargetOrthographicSize = Mathf.Clamp(
            targetOrthographicSize,
            cameraMinOrthographicSize,
            cameraMaxOrthographicSize);
        cameraTransformBlendTargetPivot = targetPivot;
        cameraTransformBlendTargetYawDegrees = targetYawDegrees;
        cameraTransformBlendTargetPitchDegrees = targetPitchDegrees;
        cameraTransformBlendActivatesPendingPortDockOrbit = activatesPendingPortDockOrbit;
        cameraHomeBlendStartedAt = Time.unscaledTime;
        cameraHomeBlendDuration = CameraHomeBlendSeconds;
        cameraTransformBlendActive = true;
        cameraHomeBlendActive = true;
    }

    private void ReadCameraPointerInput(out bool rightHeld, out bool middleHeld, out Vector2 pointerDelta, out float scrollY)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        rightHeld = mouse != null && mouse.rightButton.isPressed;
        middleHeld = mouse != null && mouse.middleButton.isPressed;
        pointerDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        scrollY = mouse != null ? mouse.scroll.ReadValue().y : 0f;
#else
        rightHeld = Input.GetMouseButton(1);
        middleHeld = Input.GetMouseButton(2);
        pointerDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        scrollY = Input.mouseScrollDelta.y * 120f;
#endif
    }

    private static float NormalizeScrollSteps(float rawScrollY)
    {
        return Mathf.Abs(rawScrollY) >= 10f ? rawScrollY / 120f : rawScrollY;
    }

    private void RefreshCameraZoomLimits()
    {
        float aspect = sceneCamera != null && sceneCamera.aspect > 0.01f
            ? sceneCamera.aspect
            : 16f / 9f;
        float minBuildings = Mathf.Max(1f, cameraMinVisible3x3DiagonalBuildings);
        float maxBuildings = Mathf.Max(minBuildings + 1f, cameraMaxVisible3x3DiagonalBuildings);
        float buildingDiagonal = 3f * cellSize * Mathf.Sqrt(2f);

        cameraMinOrthographicSize = ScreenDiagonalToOrthographicSize(buildingDiagonal * minBuildings, aspect);
        cameraMaxOrthographicSize = ScreenDiagonalToOrthographicSize(buildingDiagonal * maxBuildings, aspect);
    }

    private float CameraZoomStepLogScale()
    {
        float rotations = Mathf.Max(0.25f, cameraZoomWheelRotationsFullRange);
        float stepsPerRotation = Mathf.Max(1f, cameraWheelStepsPerRotation);
        float totalSteps = rotations * stepsPerRotation;
        float rangeRatio = Mathf.Max(1.01f, cameraMaxOrthographicSize / Mathf.Max(0.01f, cameraMinOrthographicSize));
        return Mathf.Log(rangeRatio) / totalSteps;
    }

    private static float ScreenDiagonalToOrthographicSize(float worldDiagonal, float aspect)
    {
        return worldDiagonal / (2f * Mathf.Sqrt(aspect * aspect + 1f));
    }

    private Vector2 ReadCameraMoveInput()
    {
        Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return input;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
#else
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
#endif
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private void MoveCameraPivot(Vector2 input)
    {
        Vector3 right = Vector3.ProjectOnPlane(sceneCamera.transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        float speed = cameraMoveSpeed * Mathf.Max(0.5f, GetCameraZoomSize() / 10f) * Time.deltaTime;
        cameraPivot += (right * input.x + forward * input.y) * speed;
    }

    private void PanCameraPivot(Vector2 pointerDelta)
    {
        int screenHeight = Mathf.Max(1, Screen.height);
        float unitsPerPixel = GetCameraZoomSize() * 2f / screenHeight * cameraPanSpeed;
        Vector3 right = Vector3.ProjectOnPlane(sceneCamera.transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        cameraPivot -= right * pointerDelta.x * unitsPerPixel;
        cameraPivot -= forward * pointerDelta.y * unitsPerPixel;
    }

    private void ApplyCameraTransform()
    {
        if (sceneCamera == null) return;
        if (portDockOrbitActive)
        {
            ApplyPortDockOrbitCameraTransform();
            return;
        }

        Quaternion rotation = Quaternion.Euler(cameraPitchDegrees, cameraYawDegrees, 0f);
        sceneCamera.transform.rotation = rotation;
        sceneCamera.transform.position = cameraPivot - rotation * Vector3.forward * cameraDistance;
    }

    private void ApplyPortDockOrbitCameraTransform()
    {
        CalculatePortDockOrbitCameraPose(
            portDockOrbitFocus,
            portDockOrbitForward,
            portDockOrbitRight,
            portDockOrbitBaseMajorRadius,
            portDockOrbitBaseMinorRadius,
            portDockOrbitBaseCameraDistance,
            cameraDistance,
            portDockOrbitAngleDegrees,
            portDockOrbitPitchDegrees,
            out Vector3 cameraPosition,
            out Quaternion rotation);
        sceneCamera.transform.SetPositionAndRotation(cameraPosition, rotation);
        cameraPivot = portDockOrbitFocus;
        cameraYawDegrees = rotation.eulerAngles.y;
        cameraPitchDegrees = Mathf.Clamp(portDockOrbitPitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
    }

    private void CalculateCityCameraPose(
        Vector3 pivot,
        float yawDegrees,
        float pitchDegrees,
        float zoomSize,
        out Vector3 cameraPosition,
        out Quaternion rotation)
    {
        float clampedPitch = Mathf.Clamp(pitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        rotation = Quaternion.Euler(clampedPitch, yawDegrees, 0f);
        cameraPosition = pivot - rotation * Vector3.forward * EquivalentOrthographicSizeToCameraDistance(zoomSize);
    }

    private void CalculatePortDockOrbitCameraPose(
        Vector3 focus,
        Vector3 forward,
        Vector3 right,
        float baseMajorRadius,
        float baseMinorRadius,
        float baseCameraDistance,
        float poseCameraDistance,
        float angleDegrees,
        float pitchDegrees,
        out Vector3 cameraPosition,
        out Quaternion rotation)
    {
        float zoomScale = Mathf.Clamp(
            poseCameraDistance / Mathf.Max(0.001f, baseCameraDistance),
            PortDockOrbitMinZoomScale,
            PortDockOrbitMaxZoomScale);
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        Vector3 flatOffset =
            SanitizeHorizontalAxis(right, Vector3.right) * (Mathf.Cos(angleRadians) * Mathf.Max(0.1f, baseMinorRadius) * zoomScale)
            + SanitizeHorizontalAxis(forward, Vector3.forward) * (Mathf.Sin(angleRadians) * Mathf.Max(0.1f, baseMajorRadius) * zoomScale);
        if (flatOffset.sqrMagnitude < 0.0001f)
        {
            flatOffset = -Vector3.forward;
        }

        float horizontalDistance = Mathf.Max(0.01f, flatOffset.magnitude);
        float height = Mathf.Tan(Mathf.Clamp(
            pitchDegrees,
            PortDockOrbitMinPitchDegrees,
            PortDockOrbitMaxPitchDegrees) * Mathf.Deg2Rad) * horizontalDistance;
        cameraPosition = focus + flatOffset + Vector3.up * height;
        Vector3 lookDirection = focus - cameraPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = Vector3.forward;
        }

        rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private void ActivatePortDockCameraOrbit(
        Vector3 focus,
        Vector3 forward,
        Vector3 right,
        float majorRadius,
        float minorRadius,
        float baseZoomSize,
        float baseCameraDistance,
        float angleDegrees,
        float pitchDegrees)
    {
        portDockOrbitFocus = focus;
        portDockOrbitForward = SanitizeHorizontalAxis(forward, Vector3.forward);
        portDockOrbitRight = SanitizeHorizontalAxis(right, Vector3.right);
        portDockOrbitBaseMajorRadius = Mathf.Max(0.1f, majorRadius);
        portDockOrbitBaseMinorRadius = Mathf.Max(0.1f, minorRadius);
        portDockOrbitBaseZoomSize = Mathf.Max(0.01f, baseZoomSize);
        portDockOrbitBaseCameraDistance = Mathf.Max(0.001f, baseCameraDistance);
        portDockOrbitAngleDegrees = angleDegrees;
        portDockOrbitPitchDegrees = Mathf.Clamp(pitchDegrees, PortDockOrbitMinPitchDegrees, PortDockOrbitMaxPitchDegrees);
        pendingPortDockOrbitActive = false;
        portDockOrbitActive = true;
        ApplyCameraTransform();
        InvalidateHoverOutlineCache();
    }

    private void CapturePortDockOrbitBlendStart()
    {
        portDockOrbitBlendStartFocus = portDockOrbitFocus;
        portDockOrbitBlendStartForward = SanitizeHorizontalAxis(portDockOrbitForward, Vector3.forward);
        portDockOrbitBlendStartRight = SanitizeHorizontalAxis(portDockOrbitRight, Vector3.right);
        portDockOrbitBlendStartBaseMajorRadius = Mathf.Max(0.1f, portDockOrbitBaseMajorRadius);
        portDockOrbitBlendStartBaseMinorRadius = Mathf.Max(0.1f, portDockOrbitBaseMinorRadius);
        portDockOrbitBlendStartBaseZoomSize = Mathf.Max(0.01f, GetCameraZoomSize());
        portDockOrbitBlendStartBaseCameraDistance = Mathf.Max(0.001f, portDockOrbitBaseCameraDistance);
        portDockOrbitBlendStartAngleDegrees = portDockOrbitAngleDegrees;
        portDockOrbitBlendStartPitchDegrees = Mathf.Clamp(
            portDockOrbitPitchDegrees,
            PortDockOrbitMinPitchDegrees,
            PortDockOrbitMaxPitchDegrees);
    }

    private void SetPendingPortDockCameraOrbit(
        Vector3 focus,
        Vector3 forward,
        Vector3 right,
        float majorRadius,
        float minorRadius,
        float baseZoomSize,
        float baseCameraDistance,
        float angleDegrees,
        float pitchDegrees)
    {
        pendingPortDockOrbitFocus = focus;
        pendingPortDockOrbitForward = SanitizeHorizontalAxis(forward, Vector3.forward);
        pendingPortDockOrbitRight = SanitizeHorizontalAxis(right, Vector3.right);
        pendingPortDockOrbitBaseMajorRadius = Mathf.Max(0.1f, majorRadius);
        pendingPortDockOrbitBaseMinorRadius = Mathf.Max(0.1f, minorRadius);
        pendingPortDockOrbitBaseZoomSize = Mathf.Max(0.01f, baseZoomSize);
        pendingPortDockOrbitBaseCameraDistance = Mathf.Max(0.001f, baseCameraDistance);
        pendingPortDockOrbitAngleDegrees = angleDegrees;
        pendingPortDockOrbitPitchDegrees = Mathf.Clamp(pitchDegrees, PortDockOrbitMinPitchDegrees, PortDockOrbitMaxPitchDegrees);
        pendingPortDockOrbitActive = true;
    }

    private void ActivatePendingPortDockCameraOrbit()
    {
        if (!pendingPortDockOrbitActive)
        {
            return;
        }

        ActivatePortDockCameraOrbit(
            pendingPortDockOrbitFocus,
            pendingPortDockOrbitForward,
            pendingPortDockOrbitRight,
            pendingPortDockOrbitBaseMajorRadius,
            pendingPortDockOrbitBaseMinorRadius,
            pendingPortDockOrbitBaseZoomSize,
            pendingPortDockOrbitBaseCameraDistance,
            pendingPortDockOrbitAngleDegrees,
            pendingPortDockOrbitPitchDegrees);
    }

    private void ClearPortDockCameraOrbit()
    {
        portDockOrbitActive = false;
        pendingPortDockOrbitActive = false;
    }

    private static float CalculatePortDockOrbitAngleForCameraYaw(
        float cameraYawDegrees,
        Vector3 forward,
        Vector3 right,
        float majorRadius,
        float minorRadius)
    {
        Vector3 desiredCameraOffset = -(Quaternion.Euler(0f, cameraYawDegrees, 0f) * Vector3.forward);
        desiredCameraOffset = Vector3.ProjectOnPlane(desiredCameraOffset, Vector3.up).normalized;
        if (desiredCameraOffset.sqrMagnitude < 0.0001f)
        {
            desiredCameraOffset = -Vector3.forward;
        }

        float localX = Vector3.Dot(desiredCameraOffset, SanitizeHorizontalAxis(right, Vector3.right));
        float localZ = Vector3.Dot(desiredCameraOffset, SanitizeHorizontalAxis(forward, Vector3.forward));
        return Mathf.Atan2(
            localZ / Mathf.Max(0.001f, majorRadius),
            localX / Mathf.Max(0.001f, minorRadius)) * Mathf.Rad2Deg;
    }

    private static Vector3 SanitizeHorizontalAxis(Vector3 axis, Vector3 fallback)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(axis, Vector3.up);
        if (horizontal.sqrMagnitude < 0.0001f)
        {
            horizontal = Vector3.ProjectOnPlane(fallback, Vector3.up);
        }

        return horizontal.sqrMagnitude < 0.0001f ? Vector3.forward : horizontal.normalized;
    }

    private void SelectSize(int size)
    {
        selectedSize = ClampBuildingDimension(size);
        selectedWidth = selectedSize;
        selectedHeight = selectedSize;
        removeMode = false;
        freeMouseMode = false;
        MarkTilesDirty();
        DestroyHoverOutline();
        if (!IsMoving)
        {
            EnsureGhost();
        }
    }

    private void SelectFreeMouseMode()
    {
        CancelMove();
        removeMode = false;
        freeMouseMode = true;
        MarkTilesDirty();
        HideGhost();
    }

    private void ToggleRemoveMode()
    {
        CancelMove();
        removeMode = !removeMode;
        freeMouseMode = false;
        MarkTilesDirty();
        DestroyHoverOutline();
    }

    private void UpdateHover()
    {
        hoverX = -1;
        hoverY = -1;
        hoverBuildingId = EmptyCell;
        hoverExternalDockSlotId = "";
        hoverExpansionRegionId = "";
        hoverValid = false;

        if (portDockWorldInteractionSuppressed)
        {
            RefreshHoveredBuildingKey();
            return;
        }

        RaycastHit hit;
        if (!TryRaycastPointer(out hit))
        {
            RefreshHoveredBuildingKey();
            return;
        }

        if (hit.collider.GetComponent<IsoGridCityGroundHandle>() == null)
        {
            IsoGridCityExternalDockHandle dockHandle = hit.collider.GetComponent<IsoGridCityExternalDockHandle>();
            if (dockHandle != null)
            {
                hoverExternalDockSlotId = dockHandle.occupied ? dockHandle.slotId : "";
                RefreshHoveredBuildingKey();
                return;
            }

            IsoGridCityExpansionHandle expansionHandle = hit.collider.GetComponent<IsoGridCityExpansionHandle>();
            if (expansionHandle != null)
            {
                hoverExpansionRegionId = expansionHandle.regionId;
                RefreshHoveredBuildingKey();
                return;
            }

            IsoGridCityBuildingHandle handle = hit.collider.GetComponent<IsoGridCityBuildingHandle>();
            if (handle != null && buildings.TryGetValue(handle.buildingId, out PlacedBuilding building))
            {
                hoverBuildingId = handle.buildingId;
                hoverX = building.originX;
                hoverY = building.originY;
                hoverValid = IsMoving && CanPlaceAt(hoverX, hoverY, ActiveWidth, ActiveHeight);
            }

            RefreshHoveredBuildingKey();
            return;
        }

        if (!TryWorldPointToCell(hit.point, out hoverX, out hoverY))
        {
            hoverX = -1;
            hoverY = -1;
            RefreshHoveredBuildingKey();
            return;
        }

        hoverValid = CanPlaceAt(hoverX, hoverY, ActiveWidth, ActiveHeight);
        RefreshHoveredBuildingKey();
    }

    private void RefreshHoveredBuildingKey()
    {
        if (hoverBuildingId != EmptyCell && buildings.TryGetValue(hoverBuildingId, out PlacedBuilding building))
        {
            HoveredBuildingKeyForRuntime = building.buildingKey;
            return;
        }

        HoveredBuildingKeyForRuntime = !string.IsNullOrWhiteSpace(hoverExternalDockSlotId)
            && TryGetExternalDockInSlot(hoverExternalDockSlotId, out ExternalDockPlacement placement)
            ? placement.dockKey
            : "";
    }

    private bool TryRaycastPointer(out RaycastHit hit)
    {
        hit = default;
        if (sceneCamera == null) return false;

        Vector2 pointerPosition;
        if (!TryGetPointerPosition(out pointerPosition)) return false;

        Ray ray = sceneCamera.ScreenPointToRay(pointerPosition);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            pointerRaycastHits,
            Mathf.Max(300f, sceneCamera.farClipPlane),
            CityRaycastLayerMask,
            QueryTriggerInteraction.Ignore);
        if (hitCount <= 0)
        {
            return false;
        }

        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = pointerRaycastHits[i].collider;
            if (collider == null || !IsCityPointerTarget(collider))
            {
                continue;
            }

            float distance = pointerRaycastHits[i].distance;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            return false;
        }

        hit = pointerRaycastHits[bestIndex];
        return true;
    }

    private static bool IsCityPointerTarget(Collider collider)
    {
        return collider.GetComponent<IsoGridCityGroundHandle>() != null
            || collider.GetComponent<IsoGridCityBuildingHandle>() != null
            || collider.GetComponent<IsoGridCityExternalDockHandle>() != null
            || collider.GetComponent<IsoGridCityExpansionHandle>() != null;
    }

    private static void SetCityRaycastLayer(GameObject target)
    {
        if (target != null)
        {
            target.layer = CityRaycastLayer;
        }
    }

    private int CountInteractiveColliderLayerMismatches()
    {
        int count = 0;
        CountInteractiveColliderLayerMismatches(tileRoot, ref count);
        CountInteractiveColliderLayerMismatches(buildingRoot, ref count);
        CountInteractiveColliderLayerMismatches(externalDockRoot, ref count);
        CountInteractiveColliderLayerMismatches(expansionMarkerRoot, ref count);
        return count;
    }

    private static void CountInteractiveColliderLayerMismatches(Transform root, ref int count)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && IsCityPointerTarget(collider) && collider.gameObject.layer != CityRaycastLayer)
            {
                count++;
            }
        }
    }

    private bool TryGetPointerPosition(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            position = default;
            return false;
        }

        position = mouse.position.ReadValue();
        return true;
#else
        position = Input.mousePosition;
        return true;
#endif
    }

    private void HandlePointerInput()
    {
        bool leftPressed;
        bool leftHeld;
        bool leftReleased;
        ReadPlacementPointerInput(out leftPressed, out leftHeld, out leftReleased);

        if (portDockWorldInteractionSuppressed)
        {
            ClearPressedBuilding();
            ClearPressedExternalDock();
            return;
        }

        if (pointerCameraControlActive)
        {
            ClearPressedBuilding();
            ClearPressedExternalDock();
            return;
        }

        if (leftPressed && IsPointerOverUi())
        {
            ClearPressedBuilding();
            ClearPressedExternalDock();
            return;
        }

        if (leftPressed)
        {
            HandleLeftPress();
            return;
        }

        if (leftHeld && pressedBuildingId != EmptyCell && !IsMoving && !IsPointerOverUi())
        {
            Vector2 pointerPosition;
            if (TryGetPointerPosition(out pointerPosition) &&
                (pointerPosition - leftPressScreenPosition).sqrMagnitude >= DragStartPixels * DragStartPixels)
            {
                BeginMove(pressedBuildingId);
                dragMoveActive = IsMoving;
                pressedBuildingId = EmptyCell;
            }
        }

        if (leftHeld && !string.IsNullOrWhiteSpace(pressedExternalDockKey) && !IsMoving && !IsPointerOverUi())
        {
            Vector2 pointerPosition;
            if (TryGetPointerPosition(out pointerPosition) &&
                (pointerPosition - leftDockPressScreenPosition).sqrMagnitude >= DragStartPixels * DragStartPixels)
            {
                movingExternalDockKey = pressedExternalDockKey;
                externalDockDragActive = true;
                showExternalDockTargets = true;
                RefreshExternalDockVisuals();
                ClearPressedExternalDock();
            }
        }

        if (!leftReleased) return;

        if (externalDockDragActive)
        {
            TryDropMovingExternalDockUnderPointer();
            externalDockDragActive = false;
            movingExternalDockKey = "";
            showExternalDockTargets = false;
            RefreshExternalDockVisuals();
            ClearPressedExternalDock();
            return;
        }

        if (dragMoveActive)
        {
            if (hoverX >= 0 && hoverY >= 0 && hoverValid)
            {
                PlaceActiveBuilding(hoverX, hoverY);
            }
            else
            {
                CancelMove();
            }

            dragMoveActive = false;
            ClearPressedBuilding();
            return;
        }

        if (!string.IsNullOrWhiteSpace(pressedExternalDockKey))
        {
            ExternalDockClicked?.Invoke(pressedExternalDockKey);
            ClearPressedExternalDock();
            return;
        }

        if (pressedBuildingId != EmptyCell)
        {
            if (!freeMouseMode)
            {
                BeginMove(pressedBuildingId);
            }
            else
            {
                NotifyBuildingClicked(pressedBuildingId);
            }

            ClearPressedBuilding();
        }
    }

    private void ReadPlacementPointerInput(out bool leftPressed, out bool leftHeld, out bool leftReleased)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        leftPressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        leftHeld = mouse != null && mouse.leftButton.isPressed;
        leftReleased = mouse != null && mouse.leftButton.wasReleasedThisFrame;
#else
        leftPressed = Input.GetMouseButtonDown(0);
        leftHeld = Input.GetMouseButton(0);
        leftReleased = Input.GetMouseButtonUp(0);
#endif
    }

    private void HandleLeftPress()
    {
        if (!TryGetPointerPosition(out leftPressScreenPosition)) return;

        RaycastHit hit;
        if (!TryRaycastPointer(out hit)) return;

        IsoGridCityExternalDockHandle dockHandle = hit.collider.GetComponent<IsoGridCityExternalDockHandle>();
        if (dockHandle != null)
        {
            if (!string.IsNullOrWhiteSpace(dockHandle.dockKey))
            {
                pressedExternalDockKey = dockHandle.dockKey;
                leftDockPressScreenPosition = leftPressScreenPosition;
            }

            ClearPressedBuilding();
            return;
        }

        IsoGridCityExpansionHandle expansionHandle = hit.collider.GetComponent<IsoGridCityExpansionHandle>();
        if (expansionHandle != null)
        {
            ExpansionRegionClicked?.Invoke(expansionHandle.regionId);
            ClearPressedBuilding();
            return;
        }

        IsoGridCityBuildingHandle handle = hit.collider.GetComponent<IsoGridCityBuildingHandle>();
        if (handle != null)
        {
            if (removeMode)
            {
                RemoveBuilding(handle.buildingId);
                ClearPressedBuilding();
            }
            else
            {
                if (!IsMoving)
                {
                    pressedBuildingId = handle.buildingId;
                }
            }

            return;
        }

        if (hit.collider.GetComponent<IsoGridCityGroundHandle>() != null
            && !freeMouseMode
            && TryWorldPointToCell(hit.point, out int cellX, out int cellY))
        {
            PlaceActiveBuilding(cellX, cellY);
        }
    }

    private void ClearPressedBuilding()
    {
        pressedBuildingId = EmptyCell;
        leftPressScreenPosition = Vector2.zero;
    }

    private void ClearPressedExternalDock()
    {
        pressedExternalDockKey = "";
        leftDockPressScreenPosition = Vector2.zero;
    }

    private bool TryDropMovingExternalDockUnderPointer()
    {
        if (string.IsNullOrWhiteSpace(movingExternalDockKey))
        {
            return false;
        }

        RaycastHit hit;
        if (!TryRaycastPointer(out hit))
        {
            return false;
        }

        IsoGridCityExternalDockHandle dockHandle = hit.collider.GetComponent<IsoGridCityExternalDockHandle>();
        return dockHandle != null && MoveExternalDockToSlot(movingExternalDockKey, dockHandle.slotId, true);
    }

    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void PlaceActiveBuilding(int originX, int originY)
    {
        int width = ActiveWidth;
        int height = ActiveHeight;
        if (!CanPlaceAt(originX, originY, width, height)) return;

        int id = IsMoving ? movingBuilding.id : nextBuildingId++;
        Color color = IsMoving ? movingBuilding.color : BuildingColorForId(id);
        BuildingVisualVariant variant = IsMoving ? movingBuilding.variant : BuildingVariantForId(id, width, height, "");
        string buildingKey = IsMoving ? movingBuilding.buildingKey : "";
        string prefabResourcePath = IsMoving ? movingBuilding.prefabResourcePath : "";
        string visualKind = IsMoving ? movingBuilding.visualKind : "";
        Occupy(originX, originY, width, height, id);
        CreateBuildingObject(id, width, height, originX, originY, color, variant, buildingKey, prefabResourcePath, visualKind);

        movingBuilding = null;
        dragMoveActive = false;
        ClearPressedBuilding();
        removeMode = false;
        MarkTilesDirty();
    }

    private void BeginMove(int buildingId)
    {
        if (!buildings.TryGetValue(buildingId, out PlacedBuilding building)) return;

        DestroyHoverOutline();
        movingBuilding = new PlacedBuilding
        {
            id = building.id,
            width = building.width,
            height = building.height,
            originX = building.originX,
            originY = building.originY,
            color = building.color,
            variant = building.variant,
            buildingKey = building.buildingKey,
            prefabResourcePath = building.prefabResourcePath,
            visualKind = building.visualKind,
            loadedFromPrefab = building.loadedFromPrefab
        };
        Free(building);
        Destroy(building.gameObject);
        buildings.Remove(buildingId);
        removeMode = false;
        MarkTilesDirty();
        EnsureGhost();
    }

    private void CancelMove()
    {
        dragMoveActive = false;
        ClearPressedBuilding();
        if (!IsMoving) return;

        if (CanPlaceAt(movingBuilding.originX, movingBuilding.originY, movingBuilding.width, movingBuilding.height))
        {
            Occupy(movingBuilding.originX, movingBuilding.originY, movingBuilding.width, movingBuilding.height, movingBuilding.id);
            CreateBuildingObject(
                movingBuilding.id,
                movingBuilding.width,
                movingBuilding.height,
                movingBuilding.originX,
                movingBuilding.originY,
                movingBuilding.color,
                movingBuilding.variant,
                movingBuilding.buildingKey,
                movingBuilding.prefabResourcePath,
                movingBuilding.visualKind);
        }

        movingBuilding = null;
        MarkTilesDirty();
    }

    private void RemoveBuilding(int buildingId)
    {
        if (!buildings.TryGetValue(buildingId, out PlacedBuilding building)) return;

        Free(building);
        Destroy(building.gameObject);
        buildings.Remove(buildingId);
        MarkTilesDirty();
    }

    private void ClearBuildings()
    {
        foreach (PlacedBuilding building in buildings.Values)
        {
            if (building != null && building.gameObject != null)
            {
                Destroy(building.gameObject);
            }
        }

        buildings.Clear();
        occupancy = new int[gridWidth, gridHeight];
        movingBuilding = null;
        MarkTilesDirty();
        dragMoveActive = false;
        ClearPressedBuilding();
        DestroyHoverOutline();
        removeMode = false;
    }

    private bool CanPlaceAt(int originX, int originY, int width, int height)
    {
        if (originX < 0 || originY < 0 || originX + width > gridWidth || originY + height > gridHeight)
        {
            return false;
        }

        for (int x = originX; x < originX + width; x++)
        {
            for (int y = originY; y < originY + height; y++)
            {
                if (occupancy[x, y] != EmptyCell)
                {
                    return false;
                }

                if (cellAccess != null && cellAccess[x, y] != CityCellAccess.Open)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void Occupy(int originX, int originY, int width, int height, int buildingId)
    {
        for (int x = originX; x < originX + width; x++)
        {
            for (int y = originY; y < originY + height; y++)
            {
                occupancy[x, y] = buildingId;
            }
        }

        MarkTilesDirty();
    }

    private void Free(PlacedBuilding building)
    {
        if (building == null) return;

        for (int x = building.originX; x < building.originX + building.width; x++)
        {
            for (int y = building.originY; y < building.originY + building.height; y++)
            {
                if (x >= 0 && y >= 0 && x < gridWidth && y < gridHeight && occupancy[x, y] == building.id)
                {
                    occupancy[x, y] = EmptyCell;
                }
            }
        }

        MarkTilesDirty();
    }

    private void CreateBuildingObject(
        int id,
        int width,
        int height,
        int originX,
        int originY,
        Color color,
        BuildingVisualVariant variant,
        string buildingKey = "",
        string prefabResourcePath = "",
        string visualKind = "")
    {
        string resolvedBuildingKey = string.IsNullOrWhiteSpace(buildingKey) ? "building_" + id : buildingKey.Trim();
        RemoveBuildingsWithKeyExcept(resolvedBuildingKey, id);

        float baseHeight = Mathf.Max(0.5f, Mathf.Max(width, height) * buildingHeightPerCell);
        float footprintX = BuildingFootprint(width);
        float footprintZ = BuildingFootprint(height);
        GameObject building = new GameObject("Building " + id + " " + width + "x" + height + " " + variant);
        SetCityRaycastLayer(building);
        building.transform.SetParent(buildingRoot, false);
        building.transform.position = FootprintCenterWorld(originX, originY, width, height);

        Material bodyMaterial = CreateMaterial(color);
        Material accentMaterial = CreateMaterial(ScaleColor(color, 1.12f));
        Material roofMaterial = CreateMaterial(Color.Lerp(color, Color.black, 0.22f));
        bool loadedFromPrefab = TryBuildPrefabBuildingVisual(
            building.transform,
            prefabResourcePath,
            footprintX,
            footprintZ,
            color,
            id,
            out float visualTop);
        if (!loadedFromPrefab)
        {
            visualTop = BuildBuildingVisual(building.transform, width, height, baseHeight, variant, bodyMaterial, accentMaterial, roofMaterial, id, visualKind);
        }

        BoxCollider collider = building.AddComponent<BoxCollider>();
        if (TryGetLocalRendererBounds(building, out Bounds localBounds))
        {
            collider.center = localBounds.center;
            collider.size = new Vector3(
                Mathf.Max(0.1f, localBounds.size.x),
                Mathf.Max(0.1f, localBounds.size.y),
                Mathf.Max(0.1f, localBounds.size.z));
        }
        else
        {
            collider.center = new Vector3(0f, visualTop * 0.5f, 0f);
            collider.size = new Vector3(footprintX, visualTop, footprintZ);
        }
        DisableChildColliders(building, collider);

        IsoGridCityBuildingHandle handle = building.AddComponent<IsoGridCityBuildingHandle>();
        handle.buildingId = id;

        buildings[id] = new PlacedBuilding
        {
            id = id,
            width = width,
            height = height,
            originX = originX,
            originY = originY,
            color = color,
            variant = variant,
            buildingKey = resolvedBuildingKey,
            prefabResourcePath = prefabResourcePath ?? "",
            visualKind = visualKind ?? "",
            loadedFromPrefab = loadedFromPrefab,
            gameObject = building
        };
        MarkTilesDirty();
    }

    private void RemoveBuildingsWithKeyExcept(string buildingKey, int exceptId)
    {
        if (string.IsNullOrWhiteSpace(buildingKey))
        {
            return;
        }

        List<int> duplicates = null;
        foreach (PlacedBuilding building in buildings.Values)
        {
            if (building == null
                || building.id == exceptId
                || !string.Equals(building.buildingKey, buildingKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (duplicates == null)
            {
                duplicates = new List<int>();
            }

            duplicates.Add(building.id);
        }

        if (duplicates == null)
        {
            return;
        }

        for (int i = 0; i < duplicates.Count; i++)
        {
            RemoveBuilding(duplicates[i]);
        }
    }

    private float BuildBuildingVisual(
        Transform root,
        int width,
        int height,
        float baseHeight,
        BuildingVisualVariant variant,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        int id,
        string visualKind)
    {
        float footprintX = BuildingFootprint(width);
        float footprintZ = BuildingFootprint(height);
        bool rotated = id % 2 == 0;

        if (!string.IsNullOrWhiteSpace(visualKind))
        {
            return BuildCatalogBuildingVisual(root, footprintX, footprintZ, baseHeight, visualKind, bodyMaterial, accentMaterial, roofMaterial, rotated);
        }

        switch (variant)
        {
            case BuildingVisualVariant.SteppedKeep:
                return BuildSteppedKeep(root, footprintX, footprintZ, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.HallAndTower:
                return BuildHallAndTower(root, footprintX, footprintZ, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.TwinTowers:
                return BuildTwinTowers(root, footprintX, footprintZ, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.CourtyardBlock:
                return BuildCourtyardBlock(root, footprintX, footprintZ, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.IndustrialStack:
                return BuildIndustrialStack(root, footprintX, footprintZ, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            default:
                return BuildCompactBlock(root, footprintX, footprintZ, baseHeight, bodyMaterial, roofMaterial);
        }
    }

    private bool TryBuildPrefabBuildingVisual(
        Transform root,
        string prefabResourcePath,
        float footprintX,
        float footprintZ,
        Color color,
        int id,
        out float visualTop)
    {
        visualTop = 0f;
        if (root == null || string.IsNullOrWhiteSpace(prefabResourcePath))
        {
            return false;
        }

        GameObject prefab = Resources.Load<GameObject>(prefabResourcePath);
        if (prefab == null)
        {
            return false;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = "Prefab Visual - " + prefab.name;
        instance.transform.SetParent(root, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        ApplyPrefabFallbackMaterials(instance, color, id);

        if (!TryGetLocalRendererBounds(instance, out Bounds localBounds))
        {
            visualTop = Mathf.Max(0.5f, buildingHeightPerCell);
            return true;
        }

        float scaleX = localBounds.size.x > 0.0001f ? footprintX / localBounds.size.x : 1f;
        float scaleZ = localBounds.size.z > 0.0001f ? footprintZ / localBounds.size.z : 1f;
        float scale = Mathf.Min(scaleX, scaleZ);
        scale = Mathf.Clamp(scale, 0.0001f, 1000f);

        instance.transform.localScale = Vector3.one * scale;
        instance.transform.localPosition = new Vector3(
            -localBounds.center.x * scale,
            TileHeight - localBounds.min.y * scale,
            -localBounds.center.z * scale);
        visualTop = TileHeight + localBounds.size.y * scale;
        return true;
    }

    private void ApplyPrefabFallbackMaterials(GameObject prefabInstance, Color color, int id)
    {
        if (prefabInstance == null)
        {
            return;
        }

        Material bodyMaterial = CreateMaterial(color);
        Material accentMaterial = CreateMaterial(ScaleColor(color, 1.12f + (id % 3) * 0.04f));
        MeshRenderer[] renderers = prefabInstance.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || !NeedsFallbackMaterial(renderer.sharedMaterial))
            {
                continue;
            }

            renderer.sharedMaterial = i == 0 ? bodyMaterial : accentMaterial;
        }
    }

    private static bool NeedsFallbackMaterial(Material material)
    {
        return material == null
            || material.shader == null
            || material.shader.name.Contains("Error");
    }

    private static bool TryGetLocalRendererBounds(GameObject root, out Bounds localBounds)
    {
        localBounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 localMin = root.transform.InverseTransformPoint(worldBounds.min);
            Vector3 localMax = root.transform.InverseTransformPoint(worldBounds.max);
            Bounds rendererBounds = new Bounds((localMin + localMax) * 0.5f, Abs(localMax - localMin));
            if (!hasBounds)
            {
                localBounds = rendererBounds;
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(rendererBounds);
            }
        }

        return hasBounds;
    }

    private static bool TryGetWorldRendererBounds(GameObject root, out Bounds worldBounds)
    {
        worldBounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                worldBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private int CountCameraVisibleRenderers(Transform root)
    {
        if (root == null || sceneCamera == null)
        {
            return 0;
        }

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(sceneCamera);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountEnabledRenderers(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    private static void DisableChildColliders(GameObject root, Collider rootCollider)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider != rootCollider)
            {
                collider.enabled = false;
            }
        }
    }

    private void NotifyBuildingClicked(int buildingId)
    {
        if (!buildings.TryGetValue(buildingId, out PlacedBuilding building) || building == null)
        {
            return;
        }

        BuildingClicked?.Invoke(building.buildingKey);
    }

    private bool TryGetBuildingByKey(string buildingKey, out PlacedBuilding building)
    {
        if (!string.IsNullOrWhiteSpace(buildingKey))
        {
            foreach (PlacedBuilding candidate in buildings.Values)
            {
                if (candidate != null && candidate.buildingKey == buildingKey)
                {
                    building = candidate;
                    return true;
                }
            }
        }

        building = null;
        return false;
    }

    private float BuildCompactBlock(Transform root, float footprintX, float footprintZ, float baseHeight, Material bodyMaterial, Material roofMaterial)
    {
        float footprint = Mathf.Min(footprintX, footprintZ);
        float bodyHeight = baseHeight * 1.05f;
        float roofHeight = Mathf.Max(0.08f, baseHeight * 0.10f);
        float top = AddBuildingPiece(root, "Main Block", Vector3.zero, new Vector3(footprintX, bodyHeight, footprintZ), bodyMaterial);
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Roof Cap",
            new Vector3(0f, top - TileHeight, 0f),
            new Vector3(footprint * 0.72f, roofHeight, footprint * 0.72f),
            roofMaterial));
        return top;
    }

    private float BuildSteppedKeep(
        Transform root,
        float footprintX,
        float footprintZ,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float footprint = Mathf.Min(footprintX, footprintZ);
        float lowerHeight = baseHeight * 0.42f;
        float middleHeight = baseHeight * 0.55f;
        float upperHeight = baseHeight * 0.50f;
        float top = AddBuildingPiece(root, "Lower Tier", Vector3.zero, new Vector3(footprintX, lowerHeight, footprintZ), bodyMaterial);
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Middle Tier",
            RotateFootprint(new Vector3(footprint * 0.08f, top - TileHeight, -footprint * 0.04f), rotated),
            RotateFootprint(new Vector3(footprint * 0.66f, middleHeight, footprint * 0.66f), rotated),
            accentMaterial));
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Upper Tier",
            RotateFootprint(new Vector3(-footprint * 0.06f, top - TileHeight, footprint * 0.05f), rotated),
            RotateFootprint(new Vector3(footprint * 0.40f, upperHeight, footprint * 0.40f), rotated),
            roofMaterial));
        return top;
    }

    private float BuildHallAndTower(
        Transform root,
        float footprintX,
        float footprintZ,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float footprint = Mathf.Min(footprintX, footprintZ);
        float hallHeight = baseHeight * 0.70f;
        float annexHeight = baseHeight * 0.48f;
        float towerHeight = baseHeight * 1.50f;
        float top = AddBuildingPiece(
            root,
            "Long Hall",
            RotateFootprint(Vector3.zero, rotated),
            RotateFootprint(new Vector3(footprintX * 0.92f, hallHeight, footprintZ * 0.42f), rotated),
            bodyMaterial);
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Side Annex",
            RotateFootprint(new Vector3(footprint * 0.20f, 0f, footprint * 0.26f), rotated),
            RotateFootprint(new Vector3(footprint * 0.46f, annexHeight, footprint * 0.34f), rotated),
            accentMaterial));
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Corner Tower",
            RotateFootprint(new Vector3(-footprint * 0.28f, 0f, footprint * 0.22f), rotated),
            RotateFootprint(new Vector3(footprint * 0.30f, towerHeight, footprint * 0.30f), rotated),
            roofMaterial));
        return top;
    }

    private float BuildTwinTowers(
        Transform root,
        float footprintX,
        float footprintZ,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float footprint = Mathf.Min(footprintX, footprintZ);
        float plinthHeight = baseHeight * 0.28f;
        float firstTowerHeight = baseHeight * 1.22f;
        float secondTowerHeight = baseHeight * 1.42f;
        float top = AddBuildingPiece(
            root,
            "Shared Plinth",
            Vector3.zero,
            new Vector3(footprintX * 0.94f, plinthHeight, footprintZ * 0.74f),
            bodyMaterial);
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Tower A",
            RotateFootprint(new Vector3(-footprint * 0.22f, 0f, 0f), rotated),
            RotateFootprint(new Vector3(footprint * 0.28f, firstTowerHeight, footprint * 0.42f), rotated),
            accentMaterial));
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Tower B",
            RotateFootprint(new Vector3(footprint * 0.22f, 0f, 0f), rotated),
            RotateFootprint(new Vector3(footprint * 0.28f, secondTowerHeight, footprint * 0.42f), rotated),
            roofMaterial));
        return top;
    }

    private float BuildCourtyardBlock(
        Transform root,
        float footprintX,
        float footprintZ,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float footprint = Mathf.Min(footprintX, footprintZ);
        float wingHeight = baseHeight * 0.86f;
        float backHeight = baseHeight * 0.72f;
        float towerHeight = baseHeight * 1.18f;
        float bar = footprint * 0.24f;
        float top = AddBuildingPiece(
            root,
            "Left Wing",
            RotateFootprint(new Vector3(-footprint * 0.32f, 0f, 0f), rotated),
            RotateFootprint(new Vector3(bar, wingHeight, footprint * 0.86f), rotated),
            bodyMaterial);
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Right Wing",
            RotateFootprint(new Vector3(footprint * 0.32f, 0f, 0f), rotated),
            RotateFootprint(new Vector3(bar, wingHeight, footprint * 0.86f), rotated),
            bodyMaterial));
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Back Wing",
            RotateFootprint(new Vector3(0f, 0f, footprint * 0.31f), rotated),
            RotateFootprint(new Vector3(footprint * 0.88f, backHeight, bar), rotated),
            accentMaterial));
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Gate Tower",
            RotateFootprint(new Vector3(-footprint * 0.32f, 0f, -footprint * 0.32f), rotated),
            RotateFootprint(new Vector3(bar, towerHeight, bar), rotated),
            roofMaterial));
        return top;
    }

    private float BuildIndustrialStack(
        Transform root,
        float footprintX,
        float footprintZ,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float footprint = Mathf.Min(footprintX, footprintZ);
        float shedHeight = baseHeight * 0.58f;
        float sideHeight = baseHeight * 0.42f;
        float stackHeight = baseHeight * 1.65f;
        float top = AddBuildingPiece(
            root,
            "Wide Shed",
            RotateFootprint(new Vector3(-footprint * 0.08f, 0f, 0f), rotated),
            RotateFootprint(new Vector3(footprintX * 0.74f, shedHeight, footprintZ * 0.78f), rotated),
            bodyMaterial);
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Service Block",
            RotateFootprint(new Vector3(footprint * 0.29f, 0f, -footprint * 0.23f), rotated),
            RotateFootprint(new Vector3(footprint * 0.28f, sideHeight, footprint * 0.30f), rotated),
            accentMaterial));
        top = Mathf.Max(top, AddBuildingPiece(
            root,
            "Stack",
            RotateFootprint(new Vector3(footprint * 0.28f, 0f, footprint * 0.22f), rotated),
            RotateFootprint(new Vector3(footprint * 0.18f, stackHeight, footprint * 0.18f), rotated),
            roofMaterial));
        return top;
    }

    private float BuildCatalogBuildingVisual(
        Transform root,
        float x,
        float z,
        float baseHeight,
        string visualKind,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        string kind = (visualKind ?? "").Trim().ToLowerInvariant();
        float s = Mathf.Min(x, z);
        float top = 0f;

        switch (kind)
        {
            case "hq":
                top = AddBuildingPiece(root, "HQ Base", Vector3.zero, new Vector3(x, baseHeight * 0.70f, z), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "HQ Keep", new Vector3(0f, top - TileHeight, 0f), new Vector3(x * 0.58f, baseHeight * 0.72f, z * 0.58f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "HQ Cap", new Vector3(0f, top - TileHeight, 0f), new Vector3(s * 0.30f, baseHeight * 0.42f, s * 0.30f), roofMaterial));
            case "refinery":
                top = AddBuildingPiece(root, "Ore Hall", new Vector3(-x * 0.10f, 0f, 0f), new Vector3(x * 0.78f, baseHeight * 0.56f, z * 0.86f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Crusher", new Vector3(x * 0.28f, 0f, -z * 0.24f), new Vector3(x * 0.28f, baseHeight * 0.84f, z * 0.26f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Ore Stack", new Vector3(x * 0.30f, 0f, z * 0.25f), new Vector3(s * 0.16f, baseHeight * 1.42f, s * 0.16f), roofMaterial));
            case "separator":
                top = AddBuildingPiece(root, "Separator Deck", Vector3.zero, new Vector3(x, baseHeight * 0.42f, z * 0.86f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Gas Drum A", new Vector3(-x * 0.24f, 0f, 0f), new Vector3(s * 0.24f, baseHeight * 1.10f, s * 0.24f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Gas Drum B", new Vector3(x * 0.24f, 0f, 0f), new Vector3(s * 0.24f, baseHeight * 0.92f, s * 0.24f), roofMaterial));
            case "workshop":
                top = AddBuildingPiece(root, "Workshop Hall", Vector3.zero, new Vector3(x * 0.86f, baseHeight * 0.62f, z * 0.74f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Tool Wing", new Vector3(-x * 0.26f, 0f, z * 0.24f), new Vector3(x * 0.34f, baseHeight * 0.42f, z * 0.28f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Crane Block", new Vector3(x * 0.24f, 0f, -z * 0.22f), new Vector3(x * 0.18f, baseHeight * 1.16f, z * 0.22f), roofMaterial));
            case "scrapyard":
                top = AddBuildingPiece(root, "Scrap Slab", Vector3.zero, new Vector3(x, baseHeight * 0.26f, z), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Sorting Shed", new Vector3(-x * 0.22f, 0f, 0f), new Vector3(x * 0.42f, baseHeight * 0.72f, z * 0.62f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Lift Post", new Vector3(x * 0.30f, 0f, z * 0.22f), new Vector3(s * 0.14f, baseHeight * 1.22f, s * 0.14f), roofMaterial));
            case "butchery":
                top = AddBuildingPiece(root, "Cold Hall", Vector3.zero, new Vector3(x * 0.86f, baseHeight * 0.58f, z * 0.86f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Hanging Bay", new Vector3(-x * 0.25f, 0f, -z * 0.12f), new Vector3(x * 0.28f, baseHeight * 1.04f, z * 0.40f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Processing Tank", new Vector3(x * 0.24f, 0f, z * 0.20f), new Vector3(s * 0.24f, baseHeight * 0.86f, s * 0.24f), roofMaterial));
            case "laboratory":
                top = AddBuildingPiece(root, "Lab Block", Vector3.zero, new Vector3(x * 0.72f, baseHeight * 0.82f, z * 0.72f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Signal Mast", new Vector3(-x * 0.25f, 0f, z * 0.22f), new Vector3(s * 0.12f, baseHeight * 1.62f, s * 0.12f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Data Vault", new Vector3(x * 0.24f, 0f, -z * 0.22f), new Vector3(s * 0.28f, baseHeight * 0.62f, s * 0.28f), roofMaterial));
            case "archive":
                top = AddBuildingPiece(root, "Archive Hall", Vector3.zero, new Vector3(x * 0.86f, baseHeight * 0.62f, z), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Stacks", new Vector3(-x * 0.22f, 0f, 0f), new Vector3(x * 0.24f, baseHeight * 1.12f, z * 0.72f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Reading Tower", new Vector3(x * 0.28f, 0f, z * 0.22f), new Vector3(s * 0.24f, baseHeight * 1.42f, s * 0.24f), roofMaterial));
            case "trader":
                top = AddBuildingPiece(root, "Pavilion Floor", Vector3.zero, new Vector3(x, baseHeight * 0.28f, z), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Market Hall", new Vector3(0f, 0f, -z * 0.10f), new Vector3(x * 0.74f, baseHeight * 0.72f, z * 0.58f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Awning Sign", new Vector3(0f, top - TileHeight, z * 0.28f), new Vector3(x * 0.62f, baseHeight * 0.18f, z * 0.16f), roofMaterial));
            case "pve_dock":
                top = AddBuildingPiece(root, "Dock Deck", Vector3.zero, new Vector3(x, baseHeight * 0.30f, z), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Dock Gantry", new Vector3(0f, 0f, -z * 0.22f), new Vector3(x * 0.82f, baseHeight * 1.10f, z * 0.14f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Service House", new Vector3(-x * 0.28f, 0f, z * 0.24f), new Vector3(x * 0.28f, baseHeight * 0.64f, z * 0.28f), roofMaterial));
            case "relic":
                top = AddBuildingPiece(root, "Relic Plinth", Vector3.zero, new Vector3(x * 0.78f, baseHeight * 0.28f, z * 0.78f), bodyMaterial);
                return Mathf.Max(top, AddBuildingPiece(root, "Relic Core", Vector3.zero, new Vector3(s * 0.32f, baseHeight * 1.26f, s * 0.32f), accentMaterial));
            case "capital":
                top = AddBuildingPiece(root, "Capital Base", Vector3.zero, new Vector3(x * 0.90f, baseHeight * 0.64f, z * 0.90f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Capital Wing", new Vector3(-x * 0.22f, 0f, z * 0.20f), new Vector3(x * 0.34f, baseHeight * 0.42f, z * 0.36f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Flag Tower", new Vector3(x * 0.28f, 0f, -z * 0.20f), new Vector3(s * 0.18f, baseHeight * 1.34f, s * 0.18f), roofMaterial));
            case "pub":
                top = AddBuildingPiece(root, "Pub Hall", Vector3.zero, new Vector3(x * 0.86f, baseHeight * 0.72f, z * 0.82f), bodyMaterial);
                return Mathf.Max(top, AddBuildingPiece(root, "Pub Sign", new Vector3(x * 0.26f, 0f, z * 0.28f), new Vector3(s * 0.18f, baseHeight * 0.92f, s * 0.16f), accentMaterial));
            case "monument":
                top = AddBuildingPiece(root, "Monument Base", Vector3.zero, new Vector3(x * 0.80f, baseHeight * 0.24f, z * 0.80f), bodyMaterial);
                return Mathf.Max(top, AddBuildingPiece(root, "Monument Pillar", Vector3.zero, new Vector3(s * 0.22f, baseHeight * 1.28f, s * 0.22f), roofMaterial));
            case "beacon":
                top = AddBuildingPiece(root, "Beacon House", Vector3.zero, new Vector3(x * 0.54f, baseHeight * 0.56f, z * 0.54f), bodyMaterial);
                return Mathf.Max(top, AddBuildingPiece(root, "Beacon Tower", new Vector3(0f, 0f, z * 0.14f), new Vector3(s * 0.22f, baseHeight * 1.72f, s * 0.22f), accentMaterial));
            case "mechanism":
                top = AddBuildingPiece(root, "Mechanism Base", Vector3.zero, new Vector3(x * 0.86f, baseHeight * 0.32f, z * 0.86f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Gear Block", new Vector3(-x * 0.18f, 0f, 0f), new Vector3(s * 0.30f, baseHeight * 0.84f, s * 0.30f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Counterweight", new Vector3(x * 0.24f, 0f, z * 0.20f), new Vector3(s * 0.22f, baseHeight * 0.62f, s * 0.22f), roofMaterial));
            case "construction":
                top = AddBuildingPiece(root, "Frame Yard", Vector3.zero, new Vector3(x, baseHeight * 0.32f, z * 0.82f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Beam Stack", new Vector3(-x * 0.24f, 0f, z * 0.22f), new Vector3(x * 0.36f, baseHeight * 0.64f, z * 0.24f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Crane Upright", new Vector3(x * 0.28f, 0f, -z * 0.22f), new Vector3(s * 0.14f, baseHeight * 1.42f, s * 0.14f), roofMaterial));
            case "metallurgy":
                top = AddBuildingPiece(root, "Foundry", Vector3.zero, new Vector3(x * 0.86f, baseHeight * 0.76f, z * 0.86f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Casting Pit", new Vector3(-x * 0.22f, 0f, z * 0.22f), new Vector3(s * 0.28f, baseHeight * 0.44f, s * 0.28f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Furnace Stack", new Vector3(x * 0.25f, 0f, -z * 0.20f), new Vector3(s * 0.20f, baseHeight * 1.36f, s * 0.20f), roofMaterial));
            case "mechanical":
                top = AddBuildingPiece(root, "Machine Hall", Vector3.zero, new Vector3(x * 0.92f, baseHeight * 0.64f, z * 0.78f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Pump Row", new Vector3(-x * 0.24f, 0f, z * 0.18f), new Vector3(x * 0.24f, baseHeight * 0.76f, z * 0.30f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Winch Block", new Vector3(x * 0.26f, 0f, -z * 0.20f), new Vector3(x * 0.24f, baseHeight * 0.94f, z * 0.28f), roofMaterial));
            case "instrumentation":
                top = AddBuildingPiece(root, "Instrument Lab", Vector3.zero, new Vector3(x * 0.76f, baseHeight * 0.68f, z * 0.76f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Sensor Mast", new Vector3(-x * 0.25f, 0f, z * 0.22f), new Vector3(s * 0.12f, baseHeight * 1.58f, s * 0.12f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Optics Room", new Vector3(x * 0.22f, 0f, -z * 0.20f), new Vector3(s * 0.30f, baseHeight * 0.58f, s * 0.30f), roofMaterial));
            case "reactor":
                top = AddBuildingPiece(root, "Reactor Hall", Vector3.zero, new Vector3(x * 0.84f, baseHeight * 0.72f, z * 0.78f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Reaction Vessel", new Vector3(-x * 0.22f, 0f, 0f), new Vector3(s * 0.30f, baseHeight * 1.18f, s * 0.30f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Pipe Stack", new Vector3(x * 0.28f, 0f, z * 0.20f), new Vector3(s * 0.16f, baseHeight * 1.36f, s * 0.16f), roofMaterial));
            case "automaton":
                top = AddBuildingPiece(root, "Automaton Hall", Vector3.zero, new Vector3(x * 0.82f, baseHeight * 0.68f, z * 0.88f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Logic Tower", new Vector3(-x * 0.24f, 0f, z * 0.20f), new Vector3(s * 0.24f, baseHeight * 1.24f, s * 0.24f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Servo Bay", new Vector3(x * 0.24f, 0f, -z * 0.22f), new Vector3(x * 0.30f, baseHeight * 0.54f, z * 0.28f), roofMaterial));
            case "electrical":
                top = AddBuildingPiece(root, "Electrical Hall", Vector3.zero, new Vector3(x * 0.80f, baseHeight * 0.60f, z * 0.86f), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Battery Bank", new Vector3(-x * 0.24f, 0f, -z * 0.18f), new Vector3(x * 0.26f, baseHeight * 0.76f, z * 0.34f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Coil Tower", new Vector3(x * 0.25f, 0f, z * 0.22f), new Vector3(s * 0.20f, baseHeight * 1.42f, s * 0.20f), roofMaterial));
            case "assembly":
                top = AddBuildingPiece(root, "Assembly Floor", Vector3.zero, new Vector3(x, baseHeight * 0.42f, z), bodyMaterial);
                top = Mathf.Max(top, AddBuildingPiece(root, "Assembly Bay", new Vector3(-x * 0.12f, 0f, 0f), new Vector3(x * 0.66f, baseHeight * 0.94f, z * 0.62f), accentMaterial));
                return Mathf.Max(top, AddBuildingPiece(root, "Overhead Crane", new Vector3(x * 0.22f, 0f, z * 0.24f), new Vector3(x * 0.38f, baseHeight * 0.24f, z * 0.18f), roofMaterial));
            default:
                return BuildCompactBlock(root, x, z, baseHeight, bodyMaterial, roofMaterial);
        }
    }

    private float AddBuildingPiece(Transform root, string name, Vector3 localCenter, Vector3 localScale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(root, false);
        piece.transform.localPosition = new Vector3(localCenter.x, TileHeight + localScale.y * 0.5f + localCenter.y, localCenter.z);
        piece.transform.localScale = localScale;
        piece.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = piece.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        return piece.transform.localPosition.y + localScale.y * 0.5f;
    }

    private BuildingVisualVariant BuildingVariantForId(int id, int width, int height, string visualKind)
    {
        int kindHash = string.IsNullOrWhiteSpace(visualKind) ? 0 : Mathf.Abs(visualKind.GetHashCode());
        return (BuildingVisualVariant)((id * 37 + width * 11 + height * 17 + kindHash) % BuildingVisualVariantCount);
    }

    private float BuildingFootprint(int cells)
    {
        return cells * cellSize * 0.88f;
    }

    private Vector3 FootprintCenterWorld(int originX, int originY, int width, int height)
    {
        float halfX = (width - 1) * 0.5f * cellSize;
        float halfZ = (height - 1) * 0.5f * cellSize;
        return new Vector3(originX * cellSize + halfX, 0f, originY * cellSize + halfZ);
    }

    private static Vector3 RotateFootprint(Vector3 value, bool rotated)
    {
        return rotated ? new Vector3(value.z, value.y, value.x) : value;
    }

    private static Color ScaleColor(Color color, float multiplier)
    {
        return new Color(
            Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier),
            color.a);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void RefreshHoverOutline()
    {
        if (pointerCameraControlActive
            || cameraHomeBlendActive
            || portDockWorldInteractionSuppressed
            || !freeMouseMode
            || removeMode
            || IsMoving
            || IsPointerOverUi())
        {
            DestroyHoverOutline();
            return;
        }

        GameObject targetObject = null;
        string targetId = "";
        if (hoverBuildingId != EmptyCell && buildings.TryGetValue(hoverBuildingId, out PlacedBuilding building))
        {
            targetObject = building.gameObject;
            targetId = "building:" + hoverBuildingId;
        }
        else if (!string.IsNullOrWhiteSpace(hoverExternalDockSlotId)
            && TryGetExternalDockObject(hoverExternalDockSlotId, out targetObject))
        {
            targetId = "dock:" + hoverExternalDockSlotId;
        }

        if (targetObject == null)
        {
            DestroyHoverOutline();
            return;
        }

        if (hoverOutlineObject == null || hoverOutlineTargetId != targetId)
        {
            DestroyHoverOutline();
            hoverOutlineTargetId = targetId;
            hoverOutlineObject = CreateHoverOutline(targetId);
        }

        if (!ShouldRebuildHoverOutline(targetId, targetObject))
        {
            SetHoverOutlineOverlayPose(hoverOutlineObject.transform);
            SetHoverOutlineRendererEnabled(true);
            hoverOutlineObject.SetActive(true);
            return;
        }

        if (!UpdateHoverOutlineShape(targetId, targetObject))
        {
            if (HasHoverOutlineShape())
            {
                SetHoverOutlineOverlayPose(hoverOutlineObject.transform);
                SetHoverOutlineRendererEnabled(true);
                hoverOutlineObject.SetActive(true);
                return;
            }

            DestroyHoverOutline();
        }
    }

    private GameObject CreateHoverOutline(string targetId)
    {
        GameObject outline = new GameObject("Hovered Outline " + targetId);
        outline.transform.SetParent(transform, false);
        SetHoverOutlineOverlayPose(outline.transform);
        LineRenderer line = outline.AddComponent<LineRenderer>();
        line.sharedMaterial = hoverOutlineMaterial;
        line.loop = true;
        line.useWorldSpace = false;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return outline;
    }

    private bool UpdateHoverOutlineShape(string targetId, GameObject targetObject)
    {
        if (hoverOutlineObject == null || sceneCamera == null || targetObject == null) return false;

        LineRenderer line = hoverOutlineObject.GetComponent<LineRenderer>();
        if (line == null) return false;
        if (hoverOutlineObject.transform.parent != transform)
        {
            hoverOutlineObject.transform.SetParent(transform, true);
        }

        SetHoverOutlineOverlayPose(hoverOutlineObject.transform);
        line.enabled = true;
        line.useWorldSpace = false;

        List<List<Vector2>> polygons = new List<List<Vector2>>();
        MeshFilter[] sourceMeshes = targetObject.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < sourceMeshes.Length; i++)
        {
            MeshFilter sourceMesh = sourceMeshes[i];
            if (sourceMesh == null) continue;

            List<Vector2> hull = ProjectBoxHullToScreen(sourceMesh.transform);
            if (hull.Count >= 3)
            {
                polygons.Add(hull);
            }
        }

        List<Vector2> outlineScreenPoints = new List<Vector2>();
        if ((!TryBuildRasterOutline(polygons, outlineScreenPoints) || outlineScreenPoints.Count < 3)
            && (!TryBuildUnionOutline(polygons, outlineScreenPoints) || outlineScreenPoints.Count < 3))
        {
            return false;
        }

        float outlineDepth = GetHoverOutlineOverlayDepth();
        line.positionCount = outlineScreenPoints.Count;
        for (int i = 0; i < outlineScreenPoints.Count; i++)
        {
            Vector2 point = outlineScreenPoints[i];
            line.SetPosition(i, ScreenPointToCameraOverlayLocal(point, outlineDepth));
        }

        float unitsPerPixel = CameraOverlayUnitsPerPixel(outlineDepth);
        line.widthMultiplier = Mathf.Max(0.001f, unitsPerPixel * 4.25f);
        hoverOutlineObject.SetActive(true);
        CacheHoverOutlineState(targetId, targetObject);
        return true;
    }

    private float GetHoverOutlineOverlayDepth()
    {
        if (sceneCamera == null)
        {
            return OutlineOverlayPlaneDistance;
        }

        return Mathf.Max(sceneCamera.nearClipPlane + 0.5f, OutlineOverlayPlaneDistance);
    }

    private Vector3 ScreenPointToCameraOverlayLocal(Vector2 screenPoint, float depth)
    {
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);
        float x01 = screenPoint.x / screenWidth - 0.5f;
        float y01 = screenPoint.y / screenHeight - 0.5f;
        float halfHeight = CameraOverlayHalfHeight(depth);
        float halfWidth = halfHeight * (sceneCamera != null ? Mathf.Max(0.01f, sceneCamera.aspect) : screenWidth / screenHeight);
        return new Vector3(x01 * 2f * halfWidth, y01 * 2f * halfHeight, depth);
    }

    private float CameraOverlayUnitsPerPixel(float depth)
    {
        return CameraOverlayHalfHeight(depth) * 2f / Mathf.Max(1f, Screen.height);
    }

    private float CameraOverlayHalfHeight(float depth)
    {
        if (sceneCamera == null)
        {
            return Mathf.Max(0.01f, depth);
        }

        if (sceneCamera.orthographic)
        {
            return Mathf.Max(0.01f, sceneCamera.orthographicSize);
        }

        float halfFovRadians = Mathf.Clamp(sceneCamera.fieldOfView, 1f, 179f) * Mathf.Deg2Rad * 0.5f;
        return Mathf.Max(0.01f, Mathf.Tan(halfFovRadians) * Mathf.Max(0.01f, depth));
    }

    private void SetHoverOutlineOverlayPose(Transform outlineTransform)
    {
        if (outlineTransform == null)
        {
            return;
        }

        if (sceneCamera == null)
        {
            outlineTransform.localPosition = Vector3.zero;
            outlineTransform.localRotation = Quaternion.identity;
            outlineTransform.localScale = Vector3.one;
            return;
        }

        outlineTransform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
        outlineTransform.localScale = Vector3.one;
    }

    private void SetHoverOutlineRendererEnabled(bool enabled)
    {
        if (hoverOutlineObject == null)
        {
            return;
        }

        LineRenderer line = hoverOutlineObject.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.enabled = enabled;
        }
    }

    private bool ShouldRebuildHoverOutline(string targetId, GameObject targetObject)
    {
        if (sceneCamera == null || targetObject == null) return true;
        if (hoverOutlineCachedTargetId != targetId) return true;
        if (hoverOutlineCachedScreenWidth != Screen.width || hoverOutlineCachedScreenHeight != Screen.height) return true;
        if (Mathf.Abs(hoverOutlineCachedOrthographicSize - GetCameraZoomSize()) > 0.0001f) return true;
        if ((hoverOutlineCachedCameraPosition - sceneCamera.transform.position).sqrMagnitude > 0.000001f) return true;
        if (Quaternion.Angle(hoverOutlineCachedCameraRotation, sceneCamera.transform.rotation) > 0.01f) return true;
        if ((hoverOutlineCachedTargetPosition - targetObject.transform.position).sqrMagnitude > 0.000001f) return true;

        return false;
    }

    private void CacheHoverOutlineState(string targetId, GameObject targetObject)
    {
        hoverOutlineCachedTargetId = targetId;
        hoverOutlineCachedScreenWidth = Screen.width;
        hoverOutlineCachedScreenHeight = Screen.height;
        hoverOutlineCachedCameraPosition = sceneCamera.transform.position;
        hoverOutlineCachedCameraRotation = sceneCamera.transform.rotation;
        hoverOutlineCachedOrthographicSize = GetCameraZoomSize();
        hoverOutlineCachedTargetPosition = targetObject != null ? targetObject.transform.position : Vector3.zero;
    }

    private void InvalidateHoverOutlineCache()
    {
        hoverOutlineCachedTargetId = "";
        hoverOutlineCachedScreenWidth = -1;
        hoverOutlineCachedScreenHeight = -1;
        hoverOutlineCachedOrthographicSize = -1f;
    }

    private static bool TryBuildRasterOutline(List<List<Vector2>> polygons, List<Vector2> outlinePoints)
    {
        outlinePoints.Clear();
        if (polygons.Count == 0) return false;

        if (!TryGetPolygonBounds(polygons, out Vector2 min, out Vector2 max)) return false;

        Vector2 size = max - min;
        float maxExtent = Mathf.Max(size.x, size.y);
        if (maxExtent <= 1f) return false;

        float step = Mathf.Max(OutlineRasterMinStepPixels, maxExtent / OutlineRasterMaxCells);
        Vector2 origin = min - Vector2.one * OutlineRasterPaddingPixels;
        int width = Mathf.CeilToInt((size.x + OutlineRasterPaddingPixels * 2f) / step);
        int height = Mathf.CeilToInt((size.y + OutlineRasterPaddingPixels * 2f) / step);
        if (width < 2 || height < 2) return false;

        bool[,] filled = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 sample = origin + new Vector2((x + 0.5f) * step, (y + 0.5f) * step);
                filled[x, y] = IsPointInsideAnyPolygon(sample, polygons);
            }
        }

        List<RasterOutlineEdge> edges = new List<RasterOutlineEdge>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!filled[x, y]) continue;

                if (!IsFilled(filled, width, height, x, y - 1))
                {
                    edges.Add(new RasterOutlineEdge(new Vector2Int(x, y), new Vector2Int(x + 1, y)));
                }

                if (!IsFilled(filled, width, height, x + 1, y))
                {
                    edges.Add(new RasterOutlineEdge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1)));
                }

                if (!IsFilled(filled, width, height, x, y + 1))
                {
                    edges.Add(new RasterOutlineEdge(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1)));
                }

                if (!IsFilled(filled, width, height, x - 1, y))
                {
                    edges.Add(new RasterOutlineEdge(new Vector2Int(x, y + 1), new Vector2Int(x, y)));
                }
            }
        }

        if (!TryTraceLargestRasterLoop(edges, out List<Vector2Int> loop) || loop.Count < 3)
        {
            return false;
        }

        for (int i = 0; i < loop.Count; i++)
        {
            outlinePoints.Add(origin + new Vector2(loop[i].x * step, loop[i].y * step));
        }

        SimplifyClosedOutline(outlinePoints, OutlineSimplifyTolerancePixels);
        return outlinePoints.Count >= 3;
    }

    private static bool TryGetPolygonBounds(List<List<Vector2>> polygons, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;

        for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            List<Vector2> polygon = polygons[polygonIndex];
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 point = polygon[i];
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                found = true;
            }
        }

        return found;
    }

    private static bool IsFilled(bool[,] filled, int width, int height, int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height && filled[x, y];
    }

    private static bool TryTraceLargestRasterLoop(List<RasterOutlineEdge> edges, out List<Vector2Int> bestLoop)
    {
        bestLoop = new List<Vector2Int>();
        if (edges.Count == 0) return false;

        Dictionary<Vector2Int, List<int>> outgoing = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            if (!outgoing.TryGetValue(edges[i].start, out List<int> indices))
            {
                indices = new List<int>();
                outgoing.Add(edges[i].start, indices);
            }

            indices.Add(i);
        }

        bool[] used = new bool[edges.Count];
        float bestArea = 0f;
        for (int i = 0; i < edges.Count; i++)
        {
            if (used[i]) continue;

            List<Vector2Int> loop = new List<Vector2Int>();
            RasterOutlineEdge startEdge = edges[i];
            Vector2Int start = startEdge.start;
            Vector2Int previous = startEdge.start;
            Vector2Int current = startEdge.end;
            used[i] = true;
            loop.Add(start);
            loop.Add(current);

            for (int guard = 0; guard < edges.Count + 8; guard++)
            {
                if (current == start)
                {
                    loop.RemoveAt(loop.Count - 1);
                    break;
                }

                int nextIndex = FindNextRasterEdge(edges, outgoing, used, current, previous);
                if (nextIndex < 0) break;

                used[nextIndex] = true;
                previous = current;
                current = edges[nextIndex].end;
                loop.Add(current);
            }

            if (loop.Count < 3 || current != start) continue;

            float area = Mathf.Abs(PolygonArea(loop));
            if (area > bestArea)
            {
                bestArea = area;
                bestLoop = loop;
            }
        }

        return bestLoop.Count >= 3;
    }

    private static int FindNextRasterEdge(
        List<RasterOutlineEdge> edges,
        Dictionary<Vector2Int, List<int>> outgoing,
        bool[] used,
        Vector2Int current,
        Vector2Int previous)
    {
        if (!outgoing.TryGetValue(current, out List<int> candidates)) return -1;

        int bestIndex = -1;
        float bestScore = float.NegativeInfinity;
        Vector2 incoming = new Vector2(current.x - previous.x, current.y - previous.y).normalized;
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidateIndex = candidates[i];
            if (used[candidateIndex]) continue;

            Vector2Int next = edges[candidateIndex].end;
            Vector2 outgoingDirection = new Vector2(next.x - current.x, next.y - current.y).normalized;
            float score = Vector2.Dot(incoming, outgoingDirection);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = candidateIndex;
            }
        }

        return bestIndex;
    }

    private static void SimplifyClosedOutline(List<Vector2> points, float tolerance)
    {
        if (points.Count <= 3) return;

        float toleranceSquared = tolerance * tolerance;
        FindFarthestPointPair(points, out int firstIndex, out int secondIndex);
        if (firstIndex == secondIndex) return;

        if (firstIndex > secondIndex)
        {
            int swap = firstIndex;
            firstIndex = secondIndex;
            secondIndex = swap;
        }

        List<Vector2> firstArc = new List<Vector2>();
        for (int i = firstIndex; i <= secondIndex; i++)
        {
            firstArc.Add(points[i]);
        }

        List<Vector2> secondArc = new List<Vector2>();
        for (int i = secondIndex; i < points.Count; i++)
        {
            secondArc.Add(points[i]);
        }

        for (int i = 0; i <= firstIndex; i++)
        {
            secondArc.Add(points[i]);
        }

        List<Vector2> simplifiedFirst = SimplifyOpenOutline(firstArc, toleranceSquared);
        List<Vector2> simplifiedSecond = SimplifyOpenOutline(secondArc, toleranceSquared);

        points.Clear();
        points.AddRange(simplifiedFirst);
        for (int i = 1; i < simplifiedSecond.Count - 1; i++)
        {
            points.Add(simplifiedSecond[i]);
        }
    }

    private static void FindFarthestPointPair(List<Vector2> points, out int firstIndex, out int secondIndex)
    {
        firstIndex = 0;
        secondIndex = 0;
        float bestDistance = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float distance = (points[i] - points[j]).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    firstIndex = i;
                    secondIndex = j;
                }
            }
        }
    }

    private static List<Vector2> SimplifyOpenOutline(List<Vector2> points, float toleranceSquared)
    {
        if (points.Count <= 2) return new List<Vector2>(points);

        bool[] keep = new bool[points.Count];
        keep[0] = true;
        keep[points.Count - 1] = true;
        MarkSimplifiedOutlinePoints(points, 0, points.Count - 1, toleranceSquared, keep);

        List<Vector2> simplified = new List<Vector2>();
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
            {
                simplified.Add(points[i]);
            }
        }

        return simplified;
    }

    private static void MarkSimplifiedOutlinePoints(
        List<Vector2> points,
        int startIndex,
        int endIndex,
        float toleranceSquared,
        bool[] keep)
    {
        float bestDistance = 0f;
        int bestIndex = -1;
        Vector2 start = points[startIndex];
        Vector2 end = points[endIndex];
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            float distance = DistancePointToSegmentSquared(points[i], start, end);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        if (bestIndex < 0 || bestDistance <= toleranceSquared) return;

        keep[bestIndex] = true;
        MarkSimplifiedOutlinePoints(points, startIndex, bestIndex, toleranceSquared, keep);
        MarkSimplifiedOutlinePoints(points, bestIndex, endIndex, toleranceSquared, keep);
    }

    private static float DistancePointToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0.0001f) return (point - a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        Vector2 projected = a + ab * t;
        return (point - projected).sqrMagnitude;
    }

    private List<Vector2> ProjectBoxHullToScreen(Transform boxTransform)
    {
        List<Vector2> points = new List<Vector2>(8);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 world = boxTransform.TransformPoint(new Vector3(x * 0.5f, y * 0.5f, z * 0.5f));
                    Vector3 screen = sceneCamera.WorldToScreenPoint(world);
                    if (screen.z > 0f)
                    {
                        points.Add(new Vector2(screen.x, screen.y));
                    }
                }
            }
        }

        return ConvexHull(points);
    }

    private bool TryBuildUnionOutline(List<List<Vector2>> polygons, List<Vector2> outlinePoints)
    {
        outlinePoints.Clear();
        if (polygons.Count == 0) return false;
        if (polygons.Count == 1)
        {
            outlinePoints.AddRange(polygons[0]);
            return outlinePoints.Count >= 3;
        }

        List<OutlineSegment> boundarySegments = new List<OutlineSegment>();
        for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            List<Vector2> polygon = polygons[polygonIndex];
            for (int edgeIndex = 0; edgeIndex < polygon.Count; edgeIndex++)
            {
                Vector2 a = polygon[edgeIndex];
                Vector2 b = polygon[(edgeIndex + 1) % polygon.Count];
                List<float> cuts = new List<float> { 0f, 1f };
                CollectEdgeCuts(polygons, polygonIndex, a, b, cuts);
                cuts.Sort();

                for (int cutIndex = 0; cutIndex < cuts.Count - 1; cutIndex++)
                {
                    float from = cuts[cutIndex];
                    float to = cuts[cutIndex + 1];
                    if (to - from <= 0.0001f) continue;

                    Vector2 midpoint = Vector2.Lerp(a, b, (from + to) * 0.5f);
                    if (IsPointStrictlyInsideAnyPolygon(midpoint, polygons, polygonIndex)) continue;

                    AddUniqueSegment(
                        boundarySegments,
                        new OutlineSegment(Vector2.Lerp(a, b, from), Vector2.Lerp(a, b, to)));
                }
            }
        }

        return TryTraceLongestLoop(boundarySegments, outlinePoints);
    }

    private void CollectEdgeCuts(List<List<Vector2>> polygons, int ownerPolygonIndex, Vector2 a, Vector2 b, List<float> cuts)
    {
        for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            if (polygonIndex == ownerPolygonIndex) continue;

            List<Vector2> polygon = polygons[polygonIndex];
            for (int edgeIndex = 0; edgeIndex < polygon.Count; edgeIndex++)
            {
                Vector2 c = polygon[edgeIndex];
                Vector2 d = polygon[(edgeIndex + 1) % polygon.Count];
                if (TrySegmentIntersectionParameter(a, b, c, d, out float t))
                {
                    AddCut(cuts, t);
                }

                if (TryGetPointParameterOnSegment(a, b, c, out t))
                {
                    AddCut(cuts, t);
                }

                if (TryGetPointParameterOnSegment(a, b, d, out t))
                {
                    AddCut(cuts, t);
                }
            }
        }
    }

    private static void AddCut(List<float> cuts, float value)
    {
        if (value <= 0.0001f || value >= 0.9999f) return;
        for (int i = 0; i < cuts.Count; i++)
        {
            if (Mathf.Abs(cuts[i] - value) <= 0.0001f) return;
        }

        cuts.Add(value);
    }

    private static void AddUniqueSegment(List<OutlineSegment> segments, OutlineSegment segment)
    {
        if ((segment.a - segment.b).sqrMagnitude <= 0.01f) return;
        for (int i = 0; i < segments.Count; i++)
        {
            if (AreSameSegment(segments[i], segment)) return;
        }

        segments.Add(segment);
    }

    private static bool TryTraceLongestLoop(List<OutlineSegment> segments, List<Vector2> outlinePoints)
    {
        outlinePoints.Clear();
        List<OutlineSegment> unused = new List<OutlineSegment>(segments);
        List<Vector2> bestLoop = new List<Vector2>();
        float bestArea = 0f;

        while (unused.Count > 0)
        {
            OutlineSegment startSegment = unused[0];
            unused.RemoveAt(0);

            List<Vector2> loop = new List<Vector2> { startSegment.a, startSegment.b };
            Vector2 previous = startSegment.a;
            Vector2 current = startSegment.b;

            for (int guard = 0; guard < 512; guard++)
            {
                if (PointsClose(current, loop[0]))
                {
                    loop.RemoveAt(loop.Count - 1);
                    break;
                }

                int nextIndex = FindConnectedSegment(unused, current, previous, out Vector2 nextPoint);
                if (nextIndex < 0) break;

                unused.RemoveAt(nextIndex);
                previous = current;
                current = nextPoint;
                loop.Add(current);
            }

            if (loop.Count < 3 || !PointsClose(loop[0], current)) continue;

            float area = Mathf.Abs(PolygonArea(loop));
            if (area > bestArea)
            {
                bestArea = area;
                bestLoop = loop;
            }
        }

        if (bestLoop.Count < 3) return false;
        outlinePoints.AddRange(bestLoop);
        return true;
    }

    private static int FindConnectedSegment(List<OutlineSegment> segments, Vector2 current, Vector2 previous, out Vector2 nextPoint)
    {
        int bestIndex = -1;
        nextPoint = default;
        float bestScore = float.NegativeInfinity;
        Vector2 incoming = (current - previous).normalized;

        for (int i = 0; i < segments.Count; i++)
        {
            bool fromA = PointsClose(segments[i].a, current);
            bool fromB = PointsClose(segments[i].b, current);
            if (!fromA && !fromB) continue;

            Vector2 candidate = fromA ? segments[i].b : segments[i].a;
            if (PointsClose(candidate, previous)) continue;

            Vector2 outgoing = (candidate - current).normalized;
            float score = Vector2.Dot(incoming, outgoing);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
                nextPoint = candidate;
            }
        }

        return bestIndex;
    }

    private static List<Vector2> ConvexHull(List<Vector2> points)
    {
        List<Vector2> sorted = new List<Vector2>(points);
        sorted.Sort((left, right) =>
        {
            int xCompare = left.x.CompareTo(right.x);
            return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
        });

        List<Vector2> unique = new List<Vector2>();
        for (int i = 0; i < sorted.Count; i++)
        {
            if (unique.Count == 0 || !PointsClose(unique[unique.Count - 1], sorted[i]))
            {
                unique.Add(sorted[i]);
            }
        }

        if (unique.Count <= 2) return unique;

        List<Vector2> lower = new List<Vector2>();
        for (int i = 0; i < unique.Count; i++)
        {
            while (lower.Count >= 2 &&
                Cross(lower[lower.Count - 1] - lower[lower.Count - 2], unique[i] - lower[lower.Count - 1]) <= 0f)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(unique[i]);
        }

        List<Vector2> upper = new List<Vector2>();
        for (int i = unique.Count - 1; i >= 0; i--)
        {
            while (upper.Count >= 2 &&
                Cross(upper[upper.Count - 1] - upper[upper.Count - 2], unique[i] - upper[upper.Count - 1]) <= 0f)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(unique[i]);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static bool IsPointStrictlyInsideAnyPolygon(Vector2 point, List<List<Vector2>> polygons, int ignoredPolygonIndex)
    {
        for (int i = 0; i < polygons.Count; i++)
        {
            if (i == ignoredPolygonIndex) continue;
            if (IsPointStrictlyInsidePolygon(point, polygons[i])) return true;
        }

        return false;
    }

    private static bool IsPointInsideAnyPolygon(Vector2 point, List<List<Vector2>> polygons)
    {
        for (int i = 0; i < polygons.Count; i++)
        {
            if (IsPointInsideOrOnPolygon(point, polygons[i])) return true;
        }

        return false;
    }

    private static bool IsPointInsideOrOnPolygon(Vector2 point, List<Vector2> polygon)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            if (PointOnSegment(polygon[i], polygon[(i + 1) % polygon.Count], point)) return true;
        }

        return IsPointStrictlyInsidePolygon(point, polygon);
    }

    private static bool IsPointStrictlyInsidePolygon(Vector2 point, List<Vector2> polygon)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            if (PointOnSegment(polygon[i], polygon[(i + 1) % polygon.Count], point)) return false;
        }

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            bool crosses = (polygon[i].y > point.y) != (polygon[j].y > point.y);
            if (crosses)
            {
                float x = (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                    (polygon[j].y - polygon[i].y) + polygon[i].x;
                if (point.x < x)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    private static bool TrySegmentIntersectionParameter(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out float t)
    {
        t = 0f;
        Vector2 ab = b - a;
        Vector2 cd = d - c;
        float denominator = Cross(ab, cd);
        if (Mathf.Abs(denominator) <= 0.0001f) return false;

        t = Cross(c - a, cd) / denominator;
        float u = Cross(c - a, ab) / denominator;
        return t > 0.0001f && t < 0.9999f && u > 0.0001f && u < 0.9999f;
    }

    private static bool TryGetPointParameterOnSegment(Vector2 a, Vector2 b, Vector2 point, out float t)
    {
        t = 0f;
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0.0001f) return false;

        t = Vector2.Dot(point - a, ab) / lengthSquared;
        return t > 0.0001f && t < 0.9999f && PointOnSegment(a, b, point);
    }

    private static bool PointOnSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        Vector2 segment = b - a;
        float cross = Mathf.Abs(Cross(b - a, point - a));
        if (cross > OutlinePointEpsilonPixels * Mathf.Max(1f, segment.magnitude)) return false;

        float dot = Vector2.Dot(point - a, point - b);
        return dot <= OutlinePointEpsilonPixels * OutlinePointEpsilonPixels;
    }

    private static bool AreSameSegment(OutlineSegment left, OutlineSegment right)
    {
        return (PointsClose(left.a, right.a) && PointsClose(left.b, right.b)) ||
            (PointsClose(left.a, right.b) && PointsClose(left.b, right.a));
    }

    private static bool PointsClose(Vector2 left, Vector2 right)
    {
        return (left - right).sqrMagnitude <= OutlinePointEpsilonPixels * OutlinePointEpsilonPixels;
    }

    private static float PolygonArea(List<Vector2> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }

    private static float PolygonArea(List<Vector2Int> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2Int a = polygon[i];
            Vector2Int b = polygon[(i + 1) % polygon.Count];
            area += a.x * b.y - b.x * a.y;
        }

        return area * 0.5f;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private void DestroyHoverOutline()
    {
        hoverOutlineTargetId = "";
        InvalidateHoverOutlineCache();
        if (hoverOutlineObject == null) return;

        ClearHoverOutlineShape();
        Destroy(hoverOutlineObject);
        hoverOutlineObject = null;
    }

    private void ClearHoverOutlineShape()
    {
        if (hoverOutlineObject == null) return;

        LineRenderer line = hoverOutlineObject.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.positionCount = 0;
        }
    }

    private bool HasHoverOutlineShape()
    {
        if (hoverOutlineObject == null) return false;

        LineRenderer line = hoverOutlineObject.GetComponent<LineRenderer>();
        return line != null && line.positionCount >= 3;
    }

    private void RefreshTilesIfNeeded()
    {
        bool refreshBaseTiles = tilesDirty;
        bool refreshHighlightTiles = ShouldRefreshTileHighlight();
        if (!refreshBaseTiles && !refreshHighlightTiles)
        {
            return;
        }

        if (refreshBaseTiles)
        {
            RefreshTileBaseVisuals();
        }

        if (refreshHighlightTiles)
        {
            RefreshTileHighlightVisuals();
        }

        CacheTileRefreshState();
        tilesDirty = false;
    }

    private bool ShouldRefreshTileHighlight()
    {
        bool isMoving = IsMoving;
        return hoverX != cachedTileHoverX
            || hoverY != cachedTileHoverY
            || hoverBuildingId != cachedTileHoverBuildingId
            || hoverValid != cachedTileHoverValid
            || removeMode != cachedTileRemoveMode
            || freeMouseMode != cachedTileFreeMouseMode
            || isMoving != cachedTileIsMoving
            || ActiveWidth != cachedTileActiveWidth
            || ActiveHeight != cachedTileActiveHeight
            || buildings.Count != cachedTileBuildingCount;
    }

    private void CacheTileRefreshState()
    {
        cachedTileHoverX = hoverX;
        cachedTileHoverY = hoverY;
        cachedTileHoverBuildingId = hoverBuildingId;
        cachedTileHoverValid = hoverValid;
        cachedTileRemoveMode = removeMode;
        cachedTileFreeMouseMode = freeMouseMode;
        cachedTileIsMoving = IsMoving;
        cachedTileActiveWidth = ActiveWidth;
        cachedTileActiveHeight = ActiveHeight;
        cachedTileBuildingCount = buildings.Count;
    }

    private void MarkTilesDirty()
    {
        tilesDirty = true;
    }

    private void RefreshTileBaseVisuals()
    {
        List<Vector3> openVertices = new List<Vector3>(gridWidth * gridHeight);
        List<int> openTriangles = new List<int>(gridWidth * gridHeight);
        List<Vector3> occupiedVertices = new List<Vector3>();
        List<int> occupiedTriangles = new List<int>();
        List<Vector3> fogVertices = new List<Vector3>();
        List<int> fogTriangles = new List<int>();
        List<Vector3> debrisVertices = new List<Vector3>();
        List<int> debrisTriangles = new List<int>();
        List<Vector3> clearingVertices = new List<Vector3>();
        List<int> clearingTriangles = new List<int>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                CityCellAccess access = cellAccess != null ? cellAccess[x, y] : CityCellAccess.Open;
                bool visible = access != CityCellAccess.Void;
                if (!visible) continue;

                switch (access)
                {
                    case CityCellAccess.Fog:
                        AddTileQuad(x, y, BaseTileVisualY, TileVisualInset, fogVertices, fogTriangles);
                        break;
                    case CityCellAccess.Debris:
                        AddTileQuad(x, y, BaseTileVisualY, TileVisualInset, debrisVertices, debrisTriangles);
                        break;
                    case CityCellAccess.Clearing:
                        AddTileQuad(x, y, BaseTileVisualY, TileVisualInset, clearingVertices, clearingTriangles);
                        break;
                    default:
                        if (occupancy[x, y] == EmptyCell)
                        {
                            AddTileQuad(x, y, BaseTileVisualY, TileVisualInset, openVertices, openTriangles);
                        }
                        else
                        {
                            AddTileQuad(x, y, BaseTileVisualY, TileVisualInset, occupiedVertices, occupiedTriangles);
                        }
                        break;
                }
            }
        }

        AssignTileMesh(ref openTileMeshFilter, "Open Tile Batch", tileMaterial, openVertices, openTriangles);
        AssignTileMesh(ref occupiedTileMeshFilter, "Occupied Tile Batch", occupiedMaterial, occupiedVertices, occupiedTriangles);
        AssignTileMesh(ref fogTileMeshFilter, "Fog Tile Batch", fogMaterial != null ? fogMaterial : tileMaterial, fogVertices, fogTriangles);
        AssignTileMesh(ref debrisTileMeshFilter, "Debris Tile Batch", debrisMaterial != null ? debrisMaterial : tileMaterial, debrisVertices, debrisTriangles);
        AssignTileMesh(ref clearingTileMeshFilter, "Clearing Tile Batch", clearingMaterial != null ? clearingMaterial : debrisMaterial, clearingVertices, clearingTriangles);
    }

    private void RefreshTileHighlightVisuals()
    {
        List<Vector3> validVertices = new List<Vector3>(32);
        List<int> validTriangles = new List<int>(48);
        List<Vector3> invalidVertices = new List<Vector3>(32);
        List<int> invalidTriangles = new List<int>(48);

        bool showPlacementFootprint = !removeMode && hoverX >= 0 && hoverY >= 0 && (!freeMouseMode || IsMoving);
        if (showPlacementFootprint)
        {
            AddFootprintQuads(
                hoverX,
                hoverY,
                ActiveWidth,
                ActiveHeight,
                hoverValid ? validVertices : invalidVertices,
                hoverValid ? validTriangles : invalidTriangles);
        }
        else if (removeMode && hoverX >= 0 && hoverY >= 0)
        {
            int id = occupancy[Mathf.Clamp(hoverX, 0, gridWidth - 1), Mathf.Clamp(hoverY, 0, gridHeight - 1)];
            if (id != EmptyCell && buildings.TryGetValue(id, out PlacedBuilding building))
            {
                AddFootprintQuads(building.originX, building.originY, building.width, building.height, invalidVertices, invalidTriangles);
            }
        }

        AssignTileMesh(ref validHighlightTileMeshFilter, "Valid Tile Highlight Batch", validMaterial, validVertices, validTriangles);
        AssignTileMesh(ref invalidHighlightTileMeshFilter, "Invalid Tile Highlight Batch", invalidMaterial, invalidVertices, invalidTriangles);
    }

    private void AddFootprintQuads(int originX, int originY, int width, int height, List<Vector3> vertices, List<int> triangles)
    {
        for (int x = originX; x < originX + width; x++)
        {
            for (int y = originY; y < originY + height; y++)
            {
                if (x >= 0 && y >= 0 && x < gridWidth && y < gridHeight)
                {
                    if (IsCellVisible(x, y))
                    {
                        AddTileQuad(x, y, HighlightTileVisualY, 0f, vertices, triangles);
                    }
                }
            }
        }
    }

    private void AddTileQuad(int x, int y, float yPosition, float inset, List<Vector3> vertices, List<int> triangles)
    {
        float half = Mathf.Max(0.01f, cellSize * 0.5f - inset);
        Vector3 center = new Vector3(x * cellSize, yPosition, y * cellSize);
        int start = vertices.Count;
        vertices.Add(center + new Vector3(-half, 0f, -half));
        vertices.Add(center + new Vector3(half, 0f, -half));
        vertices.Add(center + new Vector3(-half, 0f, half));
        vertices.Add(center + new Vector3(half, 0f, half));
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private void AssignTileMesh(ref MeshFilter meshFilter, string name, Material material, List<Vector3> vertices, List<int> triangles)
    {
        if (meshFilter == null)
        {
            GameObject batch = new GameObject(name);
            batch.transform.SetParent(tileRoot, false);
            meshFilter = batch.AddComponent<MeshFilter>();
            MeshRenderer renderer = batch.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = material != null ? material : tileMaterial;
            meshRenderer.enabled = vertices != null && vertices.Count > 0;
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = name + " Mesh";
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }

        mesh.Clear();
        if (vertices == null || vertices.Count == 0)
        {
            return;
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private GameObject CreateExternalDockSlot(BaseIslandExternalDockSlotView slot)
    {
        bool occupied = TryGetExternalDockInSlot(slot.dockId, out ExternalDockPlacement placement);
        GameObject dock = new GameObject((occupied ? "External Dock " : "External Dock Slot ") + slot.dockId);
        SetCityRaycastLayer(dock);
        dock.transform.SetParent(externalDockRoot, false);
        dock.transform.position = new Vector3(slot.centerX * cellSize, 0.03f, slot.centerY * cellSize);

        bool northSouth = slot.orientation == BaseIslandExternalDockOrientation.North
            || slot.orientation == BaseIslandExternalDockOrientation.South;
        float contactWidth = Mathf.Max(1, slot.contactWidth) * cellSize * 0.92f;
        float protrusionLength = Mathf.Max(1, slot.protrusionLength) * cellSize * 0.92f;
        float visualContactWidth = GetExternalDockVisualContactWidth(contactWidth, occupied);
        float visualProtrusionLength = GetExternalDockVisualProtrusionLength(protrusionLength, occupied);
        Vector3 deckScale = northSouth
            ? new Vector3(visualContactWidth, 0.14f, visualProtrusionLength)
            : new Vector3(visualProtrusionLength, 0.14f, visualContactWidth);

        if (!occupied && !showExternalDockTargets)
        {
            return dock;
        }

        BoxCollider collider = dock.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.18f, 0f);
        collider.size = new Vector3(deckScale.x, 0.46f, deckScale.z);
        IsoGridCityExternalDockHandle handle = dock.AddComponent<IsoGridCityExternalDockHandle>();
        handle.slotId = slot.dockId;
        handle.dockKey = occupied && placement != null ? placement.dockKey : "";
        handle.occupied = occupied;

        float edgeOffset = visualContactWidth * 0.30f;
        float railThickness = Mathf.Max(0.08f, cellSize * (occupied ? 0.12f : 0.075f));
        float railHeight = occupied ? 0.22f : 0.09f;
        float railLength = visualProtrusionLength * 0.88f;
        float tieThickness = Mathf.Max(0.07f, cellSize * 0.10f);
        float innerOffset = GetExternalDockInnerOffset(slot.orientation, visualProtrusionLength);
        float outerOffset = -innerOffset;
        Color dockColor = occupied && placement != null
            ? placement.color
            : new Color(0.38f, 0.32f, 0.22f, 0.65f);
        Material accentMaterial = occupied
            ? CreateMaterial(ScaleColor(dockColor, 1.22f))
            : externalDockAccentMaterial;
        Material railMaterial = occupied
            ? CreateMaterial(Color.Lerp(dockColor, Color.black, 0.18f))
            : externalDockEmptyMaterial;
        if (northSouth)
        {
            CreateExternalDockBox(
                dock.transform,
                "Left Guide Beam",
                new Vector3(-edgeOffset, railHeight * 0.5f, 0f),
                new Vector3(railThickness, railHeight, railLength),
                accentMaterial);
            CreateExternalDockBox(
                dock.transform,
                "Right Guide Beam",
                new Vector3(edgeOffset, railHeight * 0.5f, 0f),
                new Vector3(railThickness, railHeight, railLength),
                accentMaterial);
            CreateExternalDockBox(
                dock.transform,
                "Inner Tie",
                new Vector3(0f, railHeight * 0.55f, innerOffset),
                new Vector3(visualContactWidth * 0.72f, tieThickness, tieThickness),
                railMaterial);
            CreateExternalDockBox(
                dock.transform,
                "Outer Tie",
                new Vector3(0f, railHeight * 0.55f, outerOffset),
                new Vector3(visualContactWidth * 0.72f, tieThickness, tieThickness),
                railMaterial);

            CreateExternalDockBox(
                dock.transform,
                occupied ? "Dock Anchorage" : "Dock Slot Brace",
                new Vector3(0f, railHeight * 0.8f, innerOffset * 0.82f),
                new Vector3(visualContactWidth * 0.34f, railHeight * 1.35f, visualProtrusionLength * 0.08f),
                accentMaterial);

        }
        else
        {
            CreateExternalDockBox(
                dock.transform,
                "Left Guide Beam",
                new Vector3(0f, railHeight * 0.5f, -edgeOffset),
                new Vector3(railLength, railHeight, railThickness),
                accentMaterial);
            CreateExternalDockBox(
                dock.transform,
                "Right Guide Beam",
                new Vector3(0f, railHeight * 0.5f, edgeOffset),
                new Vector3(railLength, railHeight, railThickness),
                accentMaterial);
            CreateExternalDockBox(
                dock.transform,
                "Inner Tie",
                new Vector3(innerOffset, railHeight * 0.55f, 0f),
                new Vector3(tieThickness, tieThickness, visualContactWidth * 0.72f),
                railMaterial);
            CreateExternalDockBox(
                dock.transform,
                "Outer Tie",
                new Vector3(outerOffset, railHeight * 0.55f, 0f),
                new Vector3(tieThickness, tieThickness, visualContactWidth * 0.72f),
                railMaterial);

            CreateExternalDockBox(
                dock.transform,
                occupied ? "Dock Anchorage" : "Dock Slot Brace",
                new Vector3(innerOffset * 0.82f, railHeight * 0.8f, 0f),
                new Vector3(visualProtrusionLength * 0.08f, railHeight * 1.35f, visualContactWidth * 0.34f),
                accentMaterial);

        }

        return dock;
    }

    private static float GetExternalDockInnerOffset(BaseIslandExternalDockOrientation orientation, float protrusionLength)
    {
        float offset = protrusionLength * 0.36f;
        switch (orientation)
        {
            case BaseIslandExternalDockOrientation.North:
            case BaseIslandExternalDockOrientation.East:
                return -offset;
            default:
                return offset;
        }
    }

    private static float GetExternalDockVisualContactWidth(float contactWidth, bool occupied)
    {
        return occupied ? contactWidth * OccupiedExternalDockVisualWidthMultiplier : contactWidth;
    }

    private static float GetExternalDockVisualProtrusionLength(float protrusionLength, bool occupied)
    {
        return occupied ? protrusionLength * OccupiedExternalDockVisualLengthMultiplier : protrusionLength;
    }

    private static Vector3 GetExternalDockOutwardDirection(BaseIslandExternalDockOrientation orientation)
    {
        switch (orientation)
        {
            case BaseIslandExternalDockOrientation.North:
                return Vector3.forward;
            case BaseIslandExternalDockOrientation.West:
                return Vector3.left;
            case BaseIslandExternalDockOrientation.East:
                return Vector3.right;
            default:
                return Vector3.back;
        }
    }

    private static float GetExternalDockShipYawDegrees(BaseIslandExternalDockOrientation orientation)
    {
        switch (orientation)
        {
            case BaseIslandExternalDockOrientation.North:
                return 0f;
            case BaseIslandExternalDockOrientation.West:
                return -90f;
            case BaseIslandExternalDockOrientation.East:
                return 90f;
            default:
                return 180f;
        }
    }

    private static float GetExternalDockCameraYawDegrees(BaseIslandExternalDockOrientation orientation)
    {
        switch (orientation)
        {
            case BaseIslandExternalDockOrientation.North:
                return 180f;
            case BaseIslandExternalDockOrientation.West:
                return 90f;
            case BaseIslandExternalDockOrientation.East:
                return -90f;
            default:
                return 0f;
        }
    }

    private GameObject CreateExternalDockBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;

        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material != null ? material : tileMaterial;
        }

        Collider collider = box.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        return box;
    }

    private void RefreshExpansionMarkers(IReadOnlyList<BaseIslandExpansionRegionView> regions)
    {
        foreach (GameObject marker in expansionMarkers.Values)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        expansionMarkers.Clear();
        if (regions == null || expansionMarkerRoot == null)
        {
            return;
        }

        for (int i = 0; i < regions.Count; i++)
        {
            BaseIslandExpansionRegionView region = regions[i];
            if (region.state == BaseIslandExpansionVisualState.Open
                || string.IsNullOrWhiteSpace(region.regionId))
            {
                continue;
            }

            GameObject marker = CreateExpansionMarker(region);
            expansionMarkers[region.regionId] = marker;
        }
    }

    private GameObject CreateExpansionMarker(BaseIslandExpansionRegionView region)
    {
        GameObject marker = new GameObject("Expansion Marker " + region.regionId);
        SetCityRaycastLayer(marker);
        marker.transform.SetParent(expansionMarkerRoot, false);
        marker.transform.position = RegionCenterWorld(region);

        BoxCollider collider = marker.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.35f, 0f);
        collider.size = new Vector3(region.width * cellSize, 1.1f, region.height * cellSize);
        IsoGridCityExpansionHandle handle = marker.AddComponent<IsoGridCityExpansionHandle>();
        handle.regionId = region.regionId;

        if (region.state != BaseIslandExpansionVisualState.Fog)
        {
            CreateExpansionActionButton(marker.transform, GetExpansionMarkerMaterial(region.state));
        }

        GameObject textObject = new GameObject("Expansion Label");
        textObject.transform.SetParent(marker.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = GetExpansionMarkerLabel(region);
        if (region.state == BaseIslandExpansionVisualState.Debris)
        {
            text.text = "";
        }

        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.24f;
        text.fontSize = 64;
        text.color = region.state == BaseIslandExpansionVisualState.Fog
            ? new Color(0.92f, 0.94f, 0.94f, 1f)
            : new Color(1f, 0.86f, 0.48f, 1f);
        textObject.SetActive(false);

        return marker;
    }

    private void CreateExpansionActionButton(Transform parent, Material buttonMaterial)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "Expansion Action Button Post";
        post.transform.SetParent(parent, false);
        post.transform.localPosition = new Vector3(0f, 0.48f, 0f);
        post.transform.localScale = new Vector3(0.12f, 0.72f, 0.12f);
        Renderer postRenderer = post.GetComponent<Renderer>();
        if (postRenderer != null)
        {
            postRenderer.sharedMaterial = externalDockAccentMaterial != null ? externalDockAccentMaterial : buttonMaterial;
        }

        Collider postCollider = post.GetComponent<Collider>();
        if (postCollider != null)
        {
            Destroy(postCollider);
        }

        GameObject pivot = new GameObject("Expansion Action Button Pivot");
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = new Vector3(0f, 0.98f, 0f);

        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Expansion Action Button";
        plate.transform.SetParent(pivot.transform, false);
        plate.transform.localPosition = Vector3.zero;
        plate.transform.localScale = new Vector3(0.82f, 0.62f, 0.08f);
        Renderer plateRenderer = plate.GetComponent<Renderer>();
        if (plateRenderer != null)
        {
            plateRenderer.sharedMaterial = buttonMaterial;
        }

        Collider plateCollider = plate.GetComponent<Collider>();
        if (plateCollider != null)
        {
            Destroy(plateCollider);
        }

        Material iconMaterial = externalDockEmptyMaterial != null
            ? externalDockEmptyMaterial
            : CreateMaterial(new Color(0.12f, 0.10f, 0.06f, 1f));
        CreateExpansionActionButtonGlyph(pivot.transform, "Icon Cabin", new Vector3(-0.12f, 0.02f, -0.055f), new Vector3(0.22f, 0.16f, 0.035f), iconMaterial);
        CreateExpansionActionButtonGlyph(pivot.transform, "Icon Blade", new Vector3(0.13f, -0.11f, -0.055f), new Vector3(0.34f, 0.07f, 0.035f), iconMaterial);
        CreateExpansionActionButtonGlyph(pivot.transform, "Icon Arm", new Vector3(0.04f, 0.08f, -0.055f), new Vector3(0.07f, 0.28f, 0.035f), iconMaterial);
    }

    private static void CreateExpansionActionButtonGlyph(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject glyph = GameObject.CreatePrimitive(PrimitiveType.Cube);
        glyph.name = name;
        glyph.transform.SetParent(parent, false);
        glyph.transform.localPosition = localPosition;
        glyph.transform.localScale = localScale;
        Renderer renderer = glyph.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        Collider collider = glyph.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private Vector3 RegionCenterWorld(BaseIslandExpansionRegionView region)
    {
        return new Vector3(
            (region.originX + region.width * 0.5f - 0.5f) * cellSize,
            0f,
            (region.originY + region.height * 0.5f - 0.5f) * cellSize);
    }

    private Material GetExpansionMarkerMaterial(BaseIslandExpansionVisualState state)
    {
        switch (state)
        {
            case BaseIslandExpansionVisualState.Fog:
                return fogMaterial != null ? fogMaterial : expansionMarkerMaterial;
            case BaseIslandExpansionVisualState.Clearing:
                return clearingMaterial != null ? clearingMaterial : expansionMarkerMaterial;
            default:
                return expansionMarkerMaterial != null ? expansionMarkerMaterial : debrisMaterial;
        }
    }

    private static string GetExpansionMarkerLabel(BaseIslandExpansionRegionView region)
    {
        switch (region.state)
        {
            case BaseIslandExpansionVisualState.Fog:
                return "Туман\nДом ур. " + region.requiredAnchorLevel;
            case BaseIslandExpansionVisualState.Clearing:
                return "Расчистка\n" + FormatShortTime(region.remainingSeconds);
            default:
                return "Расчистить\nзавалы";
        }
    }

    private static string FormatShortTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainder = totalSeconds % 60;
        return minutes > 0
            ? minutes + "м " + remainder.ToString("00") + "с"
            : remainder + "с";
    }

    private void RefreshExpansionMarkerFacing()
    {
        if (sceneCamera == null || expansionMarkers.Count == 0)
        {
            return;
        }

        foreach (GameObject marker in expansionMarkers.Values)
        {
            if (marker == null) continue;
            Transform actionButton = marker.transform.Find("Expansion Action Button Pivot");
            if (actionButton != null)
            {
                actionButton.rotation = sceneCamera.transform.rotation;
            }

            TextMesh text = marker.GetComponentInChildren<TextMesh>(true);
            if (text != null)
            {
                IsoGridCityExpansionHandle handle = marker.GetComponent<IsoGridCityExpansionHandle>();
                bool showLabel = handle != null
                    && handle.regionId == hoverExpansionRegionId
                    && !string.IsNullOrWhiteSpace(text.text);
                if (text.gameObject.activeSelf != showLabel)
                {
                    text.gameObject.SetActive(showLabel);
                }

                text.transform.rotation = sceneCamera.transform.rotation;
            }
        }
    }

    private int CountCellsWithAccess(CityCellAccess access)
    {
        if (cellAccess == null)
        {
            return 0;
        }

        int count = 0;
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (cellAccess[x, y] == access)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static CityCellAccess ToCellAccess(BaseIslandExpansionVisualState state)
    {
        switch (state)
        {
            case BaseIslandExpansionVisualState.Fog:
                return CityCellAccess.Fog;
            case BaseIslandExpansionVisualState.Debris:
                return CityCellAccess.Debris;
            case BaseIslandExpansionVisualState.Clearing:
                return CityCellAccess.Clearing;
            default:
                return CityCellAccess.Open;
        }
    }

    private void RefreshGhost()
    {
        if (removeMode || hoverX < 0 || hoverY < 0 || (freeMouseMode && !IsMoving))
        {
            HideGhost();
            return;
        }

        EnsureGhost();
        int width = ActiveWidth;
        int heightCells = ActiveHeight;
        float height = Mathf.Max(0.5f, Mathf.Max(width, heightCells) * buildingHeightPerCell);
        ghostObject.SetActive(true);
        ghostObject.transform.position = BuildingCenterWorld(hoverX, hoverY, width, heightCells, height);
        ghostObject.transform.localScale = new Vector3(width * cellSize * 0.88f, height, heightCells * cellSize * 0.88f);
        ghostObject.GetComponent<Renderer>().sharedMaterial = hoverValid ? ghostValidMaterial : ghostInvalidMaterial;
    }

    private void HideGhost()
    {
        if (ghostObject != null)
        {
            ghostObject.SetActive(false);
        }
    }

    private void EnsureGhost()
    {
        if (ghostObject != null) return;

        ghostObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghostObject.name = "Placement Ghost";
        ghostObject.transform.SetParent(transform, false);
        Collider ghostCollider = ghostObject.GetComponent<Collider>();
        if (ghostCollider != null) ghostCollider.enabled = false;
        ghostObject.SetActive(false);
    }

    private void RefreshHud()
    {
        if (modeText != null)
        {
            string mode = removeMode
                ? "Удаление"
                : IsMoving
                ? "Перенос " + ActiveWidth + "x" + ActiveHeight
                : freeMouseMode
                ? "Свободная мышь"
                : "Строим " + ActiveWidth + "x" + ActiveHeight;
            modeText.text = mode;
        }

        if (countText != null)
        {
            countText.text = buildings.Count + " шт.";
        }
    }

    private Vector3 CellCenterWorld(int x, int y)
    {
        return new Vector3(x * cellSize, -TileHeight * 0.5f, y * cellSize);
    }

    private bool TryWorldPointToCell(Vector3 worldPoint, out int x, out int y)
    {
        x = Mathf.FloorToInt(worldPoint.x / cellSize + 0.5f);
        y = Mathf.FloorToInt(worldPoint.z / cellSize + 0.5f);
        return x >= 0
            && y >= 0
            && x < gridWidth
            && y < gridHeight
            && IsCellVisible(x, y);
    }

    private bool IsCellVisible(int x, int y)
    {
        return cellAccess == null || cellAccess[x, y] != CityCellAccess.Void;
    }

    private Vector3 BuildingCenterWorld(int originX, int originY, int width, int depth, float height)
    {
        float halfX = (width - 1) * 0.5f * cellSize;
        float halfZ = (depth - 1) * 0.5f * cellSize;
        return new Vector3(originX * cellSize + halfX, TileHeight + height * 0.5f, originY * cellSize + halfZ);
    }

    private Vector3 GridCenterWorld()
    {
        return new Vector3((gridWidth - 1) * cellSize * 0.5f, 0f, (gridHeight - 1) * cellSize * 0.5f);
    }

    private Vector3 IslandOrbitCenterWorld()
    {
        return GridCenterWorld() + Vector3.down * Mathf.Max(18f, islandDropDepth) * 0.22f;
    }

    private Color BuildingColorForId(int id)
    {
        return buildingMaterials[(id - 1) % buildingMaterials.Length].color;
    }

    private static int ClampBuildingDimension(int size)
    {
        if (size <= 2) return 2;
        return Mathf.Min(size, 8);
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private static Material CreateOutlineMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        if (material.HasProperty("_ZTest"))
        {
            material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        material.renderQueue = 4000;
        return material;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private enum BuildingVisualVariant
    {
        CompactBlock,
        SteppedKeep,
        HallAndTower,
        TwinTowers,
        CourtyardBlock,
        IndustrialStack
    }

    private readonly struct OutlineSegment
    {
        public readonly Vector2 a;
        public readonly Vector2 b;

        public OutlineSegment(Vector2 a, Vector2 b)
        {
            this.a = a;
            this.b = b;
        }
    }

    private readonly struct RasterOutlineEdge
    {
        public readonly Vector2Int start;
        public readonly Vector2Int end;

        public RasterOutlineEdge(Vector2Int start, Vector2Int end)
        {
            this.start = start;
            this.end = end;
        }
    }

    private sealed class PlacedBuilding
    {
        public int id;
        public int width;
        public int height;
        public int originX;
        public int originY;
        public Color color;
        public BuildingVisualVariant variant;
        public string buildingKey;
        public string prefabResourcePath;
        public string visualKind;
        public bool loadedFromPrefab;
        public GameObject gameObject;
    }

    private sealed class ExternalDockPlacement
    {
        public string dockKey;
        public string definitionId;
        public string slotId;
        public Color color;
        public string visualKind;
    }
}

public sealed class IsoGridCityCell
{
    public int x;
    public int y;
}

public sealed class IsoGridCityGroundHandle : MonoBehaviour
{
}

public enum BaseIslandExpansionVisualState
{
    Open,
    Debris,
    Fog,
    Clearing
}

public struct BaseIslandExpansionRegionView
{
    public string regionId;
    public int originX;
    public int originY;
    public int width;
    public int height;
    public int requiredAnchorLevel;
    public int clearCostFreight;
    public float clearSeconds;
    public float remainingSeconds;
    public BaseIslandExpansionVisualState state;

    public BaseIslandExpansionRegionView(
        string regionId,
        int originX,
        int originY,
        int width,
        int height,
        int requiredAnchorLevel,
        int clearCostFreight,
        float clearSeconds,
        float remainingSeconds,
        BaseIslandExpansionVisualState state)
    {
        this.regionId = regionId ?? "";
        this.originX = originX;
        this.originY = originY;
        this.width = Mathf.Max(1, width);
        this.height = Mathf.Max(1, height);
        this.requiredAnchorLevel = Mathf.Max(1, requiredAnchorLevel);
        this.clearCostFreight = Mathf.Max(0, clearCostFreight);
        this.clearSeconds = Mathf.Max(0f, clearSeconds);
        this.remainingSeconds = Mathf.Max(0f, remainingSeconds);
        this.state = state;
    }
}

public enum BaseIslandExternalDockOrientation
{
    South,
    North,
    West,
    East
}

public struct BaseIslandExternalDockSlotView
{
    public string dockId;
    public float centerX;
    public float centerY;
    public int contactWidth;
    public int protrusionLength;
    public BaseIslandExternalDockOrientation orientation;

    public BaseIslandExternalDockSlotView(
        string dockId,
        float centerX,
        float centerY,
        int contactWidth,
        int protrusionLength,
        BaseIslandExternalDockOrientation orientation)
    {
        this.dockId = dockId ?? "";
        this.centerX = centerX;
        this.centerY = centerY;
        this.contactWidth = Mathf.Max(1, contactWidth);
        this.protrusionLength = Mathf.Max(1, protrusionLength);
        this.orientation = orientation;
    }
}

internal enum CityCellAccess
{
    Void,
    Open,
    Debris,
    Fog,
    Clearing
}

public sealed class IsoGridCityExpansionHandle : MonoBehaviour
{
    public string regionId = "";
}

public sealed class IsoGridCityExternalDockHandle : MonoBehaviour
{
    public string slotId = "";
    public string dockKey = "";
    public bool occupied;
}

public sealed class IsoGridCityBuildingHandle : MonoBehaviour
{
    public int buildingId;
}
