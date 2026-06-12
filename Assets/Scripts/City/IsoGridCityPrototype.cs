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

    [Header("Grid")]
    public int gridWidth = 18;
    public int gridHeight = 18;
    public float cellSize = 1f;

    [Header("Buildings")]
    public int selectedSize = 2;
    public float buildingHeightPerCell = 0.45f;

    [Header("Floating Island")]
    public float islandPaddingCells = 2f;
    public float islandDropDepth = 4.8f;
    public float islandBottomScale = 0.16f;

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

    private readonly Dictionary<int, PlacedBuilding> buildings = new Dictionary<int, PlacedBuilding>();
    private IsoGridCityCell[,] cells;
    private int[,] occupancy;
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
    private GameObject ghostObject;
    private GameObject hoverOutlineObject;
    private int hoverOutlineBuildingId = EmptyCell;
    private Text modeText;
    private Text countText;
    private Vector3 cameraPivot;
    private bool pointerCameraControlActive;
    private int pressedBuildingId = EmptyCell;
    private Vector2 leftPressScreenPosition;
    private bool dragMoveActive;

    private Material tileMaterial;
    private Material occupiedMaterial;
    private Material validMaterial;
    private Material invalidMaterial;
    private Material ghostValidMaterial;
    private Material ghostInvalidMaterial;
    private Material hoverOutlineMaterial;
    private Material islandTopMaterial;
    private Material islandSideMaterial;
    private Material islandBottomMaterial;
    private Material[] buildingMaterials;

    private bool IsMoving => movingBuilding != null;
    private int ActiveSize => IsMoving ? movingBuilding.size : Mathf.Clamp(selectedSize, 2, 4);
    private const float DragStartPixels = 8f;
    private const float OutlinePointEpsilonPixels = 0.75f;
    private const float OutlineRasterPaddingPixels = 6f;
    private const float OutlineRasterMinStepPixels = 1.5f;
    private const int OutlineRasterMaxCells = 180;
    private const float OutlineSimplifyTolerancePixels = 2.25f;

    private void Awake()
    {
        BuildPrototype();
    }

    private void Update()
    {
        ReadKeyboardShortcuts();
        HandleCameraInput();
        UpdateHover();
        RefreshTiles();
        RefreshGhost();
        HandlePointerInput();
        RefreshHoverOutline();
        RefreshHud();
    }

    private void BuildPrototype()
    {
        gridWidth = Mathf.Max(4, gridWidth);
        gridHeight = Mathf.Max(4, gridHeight);
        selectedSize = ClampBuildingSize(selectedSize);

        CreateMaterials();
        EnsureCameraAndLight();
        BuildGrid();
        BuildHud();
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
        sceneCamera = Camera.main;
        if (sceneCamera == null)
        {
            GameObject cameraObject = new GameObject("Iso Grid City Camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        cameraPivot = IslandOrbitCenterWorld();
        cameraMinPitchDegrees = Mathf.Clamp(cameraMinPitchDegrees, -35f, 60f);
        cameraMaxPitchDegrees = Mathf.Clamp(cameraMaxPitchDegrees, cameraMinPitchDegrees + 5f, 88f);
        cameraPitchDegrees = Mathf.Clamp(cameraPitchDegrees, cameraMinPitchDegrees, cameraMaxPitchDegrees);
        sceneCamera.orthographic = true;
        RefreshCameraZoomLimits();
        sceneCamera.orthographicSize = Mathf.Clamp(
            Mathf.Max(gridWidth, gridHeight) * 0.58f,
            cameraMinOrthographicSize,
            cameraMaxOrthographicSize);
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = 260f;
        sceneCamera.backgroundColor = new Color(0.12f, 0.15f, 0.17f, 1f);
        ApplyCameraTransform();

        if (FindFirstObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Iso Grid City Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        }
    }

    private void BuildGrid()
    {
        islandRoot = new GameObject("Floating Island").transform;
        islandRoot.SetParent(transform, false);
        tileRoot = new GameObject("Grid Tiles").transform;
        tileRoot.SetParent(transform, false);
        buildingRoot = new GameObject("Buildings").transform;
        buildingRoot.SetParent(transform, false);

        BuildFloatingIsland();

        cells = new IsoGridCityCell[gridWidth, gridHeight];
        occupancy = new int[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Cell " + x + " " + y;
                tile.transform.SetParent(tileRoot, false);
                tile.transform.position = CellCenterWorld(x, y);
                tile.transform.localScale = new Vector3(cellSize * 0.96f, TileHeight, cellSize * 0.96f);
                tile.GetComponent<Renderer>().sharedMaterial = tileMaterial;

                IsoGridCityCell cell = tile.AddComponent<IsoGridCityCell>();
                cell.x = x;
                cell.y = y;
                cells[x, y] = cell;
            }
        }
    }

    private void BuildFloatingIsland()
    {
        if (islandRoot == null) return;

        float padding = Mathf.Max(0.5f, islandPaddingCells) * cellSize;
        float halfX = gridWidth * cellSize * 0.5f + padding;
        float halfZ = gridHeight * cellSize * 0.5f + padding;
        float dropDepth = Mathf.Max(2.2f, islandDropDepth);
        float bottomScale = Mathf.Clamp(islandBottomScale, 0.06f, 0.50f);
        Vector3 center = GridCenterWorld();
        float topY = -TileHeight - 0.035f;
        float shoulderY = -dropDepth * 0.26f;
        float lowerY = -dropDepth * 0.72f;
        float bottomY = -dropDepth;

        Vector2[] outline = new[]
        {
            new Vector2(-0.90f, -0.48f),
            new Vector2(-0.58f, -0.96f),
            new Vector2(0.06f, -0.90f),
            new Vector2(0.74f, -0.98f),
            new Vector2(0.98f, -0.44f),
            new Vector2(0.90f, 0.12f),
            new Vector2(1.00f, 0.68f),
            new Vector2(0.36f, 0.98f),
            new Vector2(-0.24f, 0.90f),
            new Vector2(-0.82f, 1.00f),
            new Vector2(-1.00f, 0.42f),
            new Vector2(-0.96f, -0.18f)
        };

        Vector3[] topRing = new Vector3[outline.Length];
        Vector3[] shoulderRing = new Vector3[outline.Length];
        Vector3[] lowerRing = new Vector3[outline.Length];
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 point = outline[i];
            topRing[i] = new Vector3(center.x + point.x * halfX, topY, center.z + point.y * halfZ);

            float shoulderScale = 0.76f + (i % 2 == 0 ? 0.04f : -0.03f);
            float lowerScale = Mathf.Lerp(0.36f, bottomScale, i % 3 == 0 ? 0.25f : 0.0f);
            shoulderRing[i] = new Vector3(
                center.x + point.x * halfX * shoulderScale,
                shoulderY + ((i % 3) - 1) * 0.10f,
                center.z + point.y * halfZ * shoulderScale);
            lowerRing[i] = new Vector3(
                center.x + point.x * halfX * lowerScale,
                lowerY + (i % 2 == 0 ? 0.12f : -0.08f),
                center.z + point.y * halfZ * lowerScale);
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

            AddMeshQuad(vertices, sideTriangles, topRing[i], topRing[next], shoulderRing[next], shoulderRing[i]);
            AddMeshQuad(vertices, sideTriangles, shoulderRing[i], shoulderRing[next], lowerRing[next], lowerRing[i]);
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
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Iso Grid City HUD");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();

        RectTransform panel = CreateUiRect("Toolbar", canvas.transform, new Vector2(18f, -18f), new Vector2(560f, 96f));
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
            sceneCamera.orthographicSize = Mathf.Clamp(
                sceneCamera.orthographicSize * zoomFactor,
                cameraMinOrthographicSize,
                cameraMaxOrthographicSize);
        }

        ApplyCameraTransform();
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

        float speed = cameraMoveSpeed * Mathf.Max(0.5f, sceneCamera.orthographicSize / 10f) * Time.deltaTime;
        cameraPivot += (right * input.x + forward * input.y) * speed;
    }

    private void PanCameraPivot(Vector2 pointerDelta)
    {
        int screenHeight = Mathf.Max(1, Screen.height);
        float unitsPerPixel = sceneCamera.orthographicSize * 2f / screenHeight * cameraPanSpeed;
        Vector3 right = Vector3.ProjectOnPlane(sceneCamera.transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        cameraPivot -= right * pointerDelta.x * unitsPerPixel;
        cameraPivot -= forward * pointerDelta.y * unitsPerPixel;
    }

    private void ApplyCameraTransform()
    {
        if (sceneCamera == null) return;

        Quaternion rotation = Quaternion.Euler(cameraPitchDegrees, cameraYawDegrees, 0f);
        sceneCamera.transform.rotation = rotation;
        sceneCamera.transform.position = cameraPivot - rotation * Vector3.forward * cameraDistance;
    }

    private void SelectSize(int size)
    {
        selectedSize = ClampBuildingSize(size);
        removeMode = false;
        freeMouseMode = false;
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
        HideGhost();
    }

    private void ToggleRemoveMode()
    {
        CancelMove();
        removeMode = !removeMode;
        freeMouseMode = false;
        DestroyHoverOutline();
    }

    private void UpdateHover()
    {
        hoverX = -1;
        hoverY = -1;
        hoverBuildingId = EmptyCell;
        hoverValid = false;

        RaycastHit hit;
        if (!TryRaycastPointer(out hit)) return;

        IsoGridCityCell cell = hit.collider.GetComponent<IsoGridCityCell>();
        if (cell == null)
        {
            IsoGridCityBuildingHandle handle = hit.collider.GetComponent<IsoGridCityBuildingHandle>();
            if (handle != null && buildings.TryGetValue(handle.buildingId, out PlacedBuilding building))
            {
                hoverBuildingId = handle.buildingId;
                hoverX = building.originX;
                hoverY = building.originY;
                hoverValid = IsMoving && CanPlaceAt(hoverX, hoverY, ActiveSize);
            }

            return;
        }

        hoverX = cell.x;
        hoverY = cell.y;
        hoverValid = CanPlaceAt(hoverX, hoverY, ActiveSize);
    }

    private bool TryRaycastPointer(out RaycastHit hit)
    {
        hit = default;
        if (sceneCamera == null) return false;

        Vector2 pointerPosition;
        if (!TryGetPointerPosition(out pointerPosition)) return false;

        Ray ray = sceneCamera.ScreenPointToRay(pointerPosition);
        return Physics.Raycast(ray, out hit, 300f);
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

        if (pointerCameraControlActive)
        {
            ClearPressedBuilding();
            return;
        }

        if (leftPressed && IsPointerOverUi())
        {
            ClearPressedBuilding();
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

        if (!leftReleased) return;

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

        if (pressedBuildingId != EmptyCell)
        {
            if (!freeMouseMode)
            {
                BeginMove(pressedBuildingId);
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

        IsoGridCityCell cell = hit.collider.GetComponent<IsoGridCityCell>();
        if (cell != null && !freeMouseMode)
        {
            PlaceActiveBuilding(cell.x, cell.y);
        }
    }

    private void ClearPressedBuilding()
    {
        pressedBuildingId = EmptyCell;
        leftPressScreenPosition = Vector2.zero;
    }

    private bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void PlaceActiveBuilding(int originX, int originY)
    {
        int size = ActiveSize;
        if (!CanPlaceAt(originX, originY, size)) return;

        int id = IsMoving ? movingBuilding.id : nextBuildingId++;
        Color color = IsMoving ? movingBuilding.color : BuildingColorForId(id);
        BuildingVisualVariant variant = IsMoving ? movingBuilding.variant : BuildingVariantForId(id, size);
        Occupy(originX, originY, size, id);
        CreateBuildingObject(id, size, originX, originY, color, variant);

        movingBuilding = null;
        dragMoveActive = false;
        ClearPressedBuilding();
        removeMode = false;
    }

    private void BeginMove(int buildingId)
    {
        if (!buildings.TryGetValue(buildingId, out PlacedBuilding building)) return;

        DestroyHoverOutline();
        movingBuilding = new PlacedBuilding
        {
            id = building.id,
            size = building.size,
            originX = building.originX,
            originY = building.originY,
            color = building.color,
            variant = building.variant
        };
        Free(building);
        Destroy(building.gameObject);
        buildings.Remove(buildingId);
        removeMode = false;
        EnsureGhost();
    }

    private void CancelMove()
    {
        dragMoveActive = false;
        ClearPressedBuilding();
        if (!IsMoving) return;

        if (CanPlaceAt(movingBuilding.originX, movingBuilding.originY, movingBuilding.size))
        {
            Occupy(movingBuilding.originX, movingBuilding.originY, movingBuilding.size, movingBuilding.id);
            CreateBuildingObject(
                movingBuilding.id,
                movingBuilding.size,
                movingBuilding.originX,
                movingBuilding.originY,
                movingBuilding.color,
                movingBuilding.variant);
        }

        movingBuilding = null;
    }

    private void RemoveBuilding(int buildingId)
    {
        if (!buildings.TryGetValue(buildingId, out PlacedBuilding building)) return;

        Free(building);
        Destroy(building.gameObject);
        buildings.Remove(buildingId);
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
        dragMoveActive = false;
        ClearPressedBuilding();
        DestroyHoverOutline();
        removeMode = false;
    }

    private bool CanPlaceAt(int originX, int originY, int size)
    {
        if (originX < 0 || originY < 0 || originX + size > gridWidth || originY + size > gridHeight)
        {
            return false;
        }

        for (int x = originX; x < originX + size; x++)
        {
            for (int y = originY; y < originY + size; y++)
            {
                if (occupancy[x, y] != EmptyCell)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void Occupy(int originX, int originY, int size, int buildingId)
    {
        for (int x = originX; x < originX + size; x++)
        {
            for (int y = originY; y < originY + size; y++)
            {
                occupancy[x, y] = buildingId;
            }
        }
    }

    private void Free(PlacedBuilding building)
    {
        if (building == null) return;

        for (int x = building.originX; x < building.originX + building.size; x++)
        {
            for (int y = building.originY; y < building.originY + building.size; y++)
            {
                if (x >= 0 && y >= 0 && x < gridWidth && y < gridHeight && occupancy[x, y] == building.id)
                {
                    occupancy[x, y] = EmptyCell;
                }
            }
        }
    }

    private void CreateBuildingObject(int id, int size, int originX, int originY, Color color, BuildingVisualVariant variant)
    {
        float baseHeight = Mathf.Max(0.5f, size * buildingHeightPerCell);
        float footprint = BuildingFootprint(size);
        GameObject building = new GameObject("Building " + id + " " + size + "x" + size + " " + variant);
        building.transform.SetParent(buildingRoot, false);
        building.transform.position = FootprintCenterWorld(originX, originY, size);

        Material bodyMaterial = CreateMaterial(color);
        Material accentMaterial = CreateMaterial(ScaleColor(color, 1.12f));
        Material roofMaterial = CreateMaterial(Color.Lerp(color, Color.black, 0.22f));
        float visualTop = BuildBuildingVisual(building.transform, size, baseHeight, variant, bodyMaterial, accentMaterial, roofMaterial, id);

        BoxCollider collider = building.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, visualTop * 0.5f, 0f);
        collider.size = new Vector3(footprint, visualTop, footprint);

        IsoGridCityBuildingHandle handle = building.AddComponent<IsoGridCityBuildingHandle>();
        handle.buildingId = id;

        buildings[id] = new PlacedBuilding
        {
            id = id,
            size = size,
            originX = originX,
            originY = originY,
            color = color,
            variant = variant,
            gameObject = building
        };
    }

    private float BuildBuildingVisual(
        Transform root,
        int size,
        float baseHeight,
        BuildingVisualVariant variant,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        int id)
    {
        float footprint = BuildingFootprint(size);
        bool rotated = id % 2 == 0;

        switch (variant)
        {
            case BuildingVisualVariant.SteppedKeep:
                return BuildSteppedKeep(root, footprint, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.HallAndTower:
                return BuildHallAndTower(root, footprint, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.TwinTowers:
                return BuildTwinTowers(root, footprint, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.CourtyardBlock:
                return BuildCourtyardBlock(root, footprint, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            case BuildingVisualVariant.IndustrialStack:
                return BuildIndustrialStack(root, footprint, baseHeight, bodyMaterial, accentMaterial, roofMaterial, rotated);
            default:
                return BuildCompactBlock(root, footprint, baseHeight, bodyMaterial, roofMaterial);
        }
    }

    private float BuildCompactBlock(Transform root, float footprint, float baseHeight, Material bodyMaterial, Material roofMaterial)
    {
        float bodyHeight = baseHeight * 1.05f;
        float roofHeight = Mathf.Max(0.08f, baseHeight * 0.10f);
        float top = AddBuildingPiece(root, "Main Block", Vector3.zero, new Vector3(footprint, bodyHeight, footprint), bodyMaterial);
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
        float footprint,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float lowerHeight = baseHeight * 0.42f;
        float middleHeight = baseHeight * 0.55f;
        float upperHeight = baseHeight * 0.50f;
        float top = AddBuildingPiece(root, "Lower Tier", Vector3.zero, new Vector3(footprint, lowerHeight, footprint), bodyMaterial);
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
        float footprint,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float hallHeight = baseHeight * 0.70f;
        float annexHeight = baseHeight * 0.48f;
        float towerHeight = baseHeight * 1.50f;
        float top = AddBuildingPiece(
            root,
            "Long Hall",
            RotateFootprint(Vector3.zero, rotated),
            RotateFootprint(new Vector3(footprint * 0.92f, hallHeight, footprint * 0.42f), rotated),
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
        float footprint,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float plinthHeight = baseHeight * 0.28f;
        float firstTowerHeight = baseHeight * 1.22f;
        float secondTowerHeight = baseHeight * 1.42f;
        float top = AddBuildingPiece(
            root,
            "Shared Plinth",
            Vector3.zero,
            new Vector3(footprint * 0.94f, plinthHeight, footprint * 0.74f),
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
        float footprint,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
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
        float footprint,
        float baseHeight,
        Material bodyMaterial,
        Material accentMaterial,
        Material roofMaterial,
        bool rotated)
    {
        float shedHeight = baseHeight * 0.58f;
        float sideHeight = baseHeight * 0.42f;
        float stackHeight = baseHeight * 1.65f;
        float top = AddBuildingPiece(
            root,
            "Wide Shed",
            RotateFootprint(new Vector3(-footprint * 0.08f, 0f, 0f), rotated),
            RotateFootprint(new Vector3(footprint * 0.74f, shedHeight, footprint * 0.78f), rotated),
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

    private BuildingVisualVariant BuildingVariantForId(int id, int size)
    {
        return (BuildingVisualVariant)((id * 37 + size * 11) % BuildingVisualVariantCount);
    }

    private float BuildingFootprint(int size)
    {
        return size * cellSize * 0.88f;
    }

    private Vector3 FootprintCenterWorld(int originX, int originY, int size)
    {
        float half = (size - 1) * 0.5f * cellSize;
        return new Vector3(originX * cellSize + half, 0f, originY * cellSize + half);
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

    private void RefreshHoverOutline()
    {
        if (!freeMouseMode || removeMode || IsMoving || hoverBuildingId == EmptyCell || IsPointerOverUi())
        {
            DestroyHoverOutline();
            return;
        }

        if (!buildings.TryGetValue(hoverBuildingId, out PlacedBuilding building) || building.gameObject == null)
        {
            DestroyHoverOutline();
            return;
        }

        if (hoverOutlineObject == null || hoverOutlineBuildingId != hoverBuildingId)
        {
            DestroyHoverOutline();
            hoverOutlineBuildingId = hoverBuildingId;
            hoverOutlineObject = CreateHoverOutline(building);
        }

        if (!UpdateHoverOutlineShape(building))
        {
            ClearHoverOutlineShape();
            DestroyHoverOutline();
        }
    }

    private GameObject CreateHoverOutline(PlacedBuilding building)
    {
        GameObject outline = new GameObject("Hovered Building Outline " + building.id);
        outline.transform.SetParent(transform, false);
        LineRenderer line = outline.AddComponent<LineRenderer>();
        line.sharedMaterial = hoverOutlineMaterial;
        line.loop = true;
        line.useWorldSpace = true;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return outline;
    }

    private bool UpdateHoverOutlineShape(PlacedBuilding building)
    {
        if (hoverOutlineObject == null || sceneCamera == null) return false;

        LineRenderer line = hoverOutlineObject.GetComponent<LineRenderer>();
        if (line == null) return false;

        List<List<Vector2>> polygons = new List<List<Vector2>>();
        foreach (Transform source in building.gameObject.transform)
        {
            MeshFilter sourceMesh = source.GetComponent<MeshFilter>();
            if (sourceMesh == null) continue;

            List<Vector2> hull = ProjectBoxHullToScreen(source);
            if (hull.Count >= 3)
            {
                polygons.Add(hull);
            }
        }

        List<Vector2> outlineScreenPoints = new List<Vector2>();
        if (!TryBuildRasterOutline(polygons, outlineScreenPoints) || outlineScreenPoints.Count < 3)
        {
            return false;
        }

        float outlineDepth = OutlineDepthForBuilding(building);
        line.positionCount = outlineScreenPoints.Count;
        for (int i = 0; i < outlineScreenPoints.Count; i++)
        {
            Vector2 point = outlineScreenPoints[i];
            line.SetPosition(i, sceneCamera.ScreenToWorldPoint(new Vector3(point.x, point.y, outlineDepth)));
        }

        float pixelsToWorld = sceneCamera.orthographic
            ? sceneCamera.orthographicSize * 2f / Mathf.Max(1f, Screen.height)
            : Vector3.Distance(sceneCamera.transform.position, building.gameObject.transform.position) * 0.002f;
        line.widthMultiplier = Mathf.Max(0.01f, pixelsToWorld * 4.25f);
        hoverOutlineObject.SetActive(true);
        return true;
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

    private float OutlineDepthForBuilding(PlacedBuilding building)
    {
        return sceneCamera.nearClipPlane + 0.05f;
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
        hoverOutlineBuildingId = EmptyCell;
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

    private void RefreshTiles()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Material material = occupancy[x, y] == EmptyCell ? tileMaterial : occupiedMaterial;
                cells[x, y].GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        bool showPlacementFootprint = !removeMode && hoverX >= 0 && hoverY >= 0 && (!freeMouseMode || IsMoving);
        if (showPlacementFootprint)
        {
            ApplyFootprintHighlight(hoverX, hoverY, ActiveSize, hoverValid ? validMaterial : invalidMaterial);
        }
        else if (removeMode && hoverX >= 0 && hoverY >= 0)
        {
            int id = occupancy[Mathf.Clamp(hoverX, 0, gridWidth - 1), Mathf.Clamp(hoverY, 0, gridHeight - 1)];
            if (id != EmptyCell && buildings.TryGetValue(id, out PlacedBuilding building))
            {
                ApplyFootprintHighlight(building.originX, building.originY, building.size, invalidMaterial);
            }
        }
    }

    private void ApplyFootprintHighlight(int originX, int originY, int size, Material material)
    {
        for (int x = originX; x < originX + size; x++)
        {
            for (int y = originY; y < originY + size; y++)
            {
                if (x >= 0 && y >= 0 && x < gridWidth && y < gridHeight)
                {
                    cells[x, y].GetComponent<Renderer>().sharedMaterial = material;
                }
            }
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
        int size = ActiveSize;
        float height = Mathf.Max(0.5f, size * buildingHeightPerCell);
        ghostObject.SetActive(true);
        ghostObject.transform.position = BuildingCenterWorld(hoverX, hoverY, size, height);
        ghostObject.transform.localScale = new Vector3(size * cellSize * 0.88f, height, size * cellSize * 0.88f);
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
                ? "Перенос " + ActiveSize + "x" + ActiveSize
                : freeMouseMode
                ? "Свободная мышь"
                : "Строим " + ActiveSize + "x" + ActiveSize;
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

    private Vector3 BuildingCenterWorld(int originX, int originY, int size, float height)
    {
        float half = (size - 1) * 0.5f * cellSize;
        return new Vector3(originX * cellSize + half, TileHeight + height * 0.5f, originY * cellSize + half);
    }

    private Vector3 GridCenterWorld()
    {
        return new Vector3((gridWidth - 1) * cellSize * 0.5f, 0f, (gridHeight - 1) * cellSize * 0.5f);
    }

    private Vector3 IslandOrbitCenterWorld()
    {
        return GridCenterWorld() + Vector3.down * Mathf.Max(2.2f, islandDropDepth) * 0.34f;
    }

    private Color BuildingColorForId(int id)
    {
        return buildingMaterials[(id - 1) % buildingMaterials.Length].color;
    }

    private static int ClampBuildingSize(int size)
    {
        if (size <= 2) return 2;
        if (size == 3) return 3;
        return 4;
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
        public int size;
        public int originX;
        public int originY;
        public Color color;
        public BuildingVisualVariant variant;
        public GameObject gameObject;
    }
}

public sealed class IsoGridCityCell : MonoBehaviour
{
    public int x;
    public int y;
}

public sealed class IsoGridCityBuildingHandle : MonoBehaviour
{
    public int buildingId;
}
