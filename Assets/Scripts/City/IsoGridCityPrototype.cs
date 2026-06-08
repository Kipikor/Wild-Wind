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

    [Header("Grid")]
    public int gridWidth = 18;
    public int gridHeight = 18;
    public float cellSize = 1f;

    [Header("Buildings")]
    public int selectedSize = 2;
    public float buildingHeightPerCell = 0.45f;

    [Header("Camera")]
    public float cameraYawDegrees = 45f;
    public float cameraPitchDegrees = 58f;
    public float cameraDistance = 32f;
    public float cameraOrbitSensitivity = 0.18f;
    public float cameraPanSpeed = 1f;
    public float cameraZoomSensitivity = 0.0015f;
    public float cameraMoveSpeed = 12f;
    public float cameraMinOrthographicSize = 4f;
    public float cameraMaxOrthographicSize = 22f;

    private readonly Dictionary<int, PlacedBuilding> buildings = new Dictionary<int, PlacedBuilding>();
    private IsoGridCityCell[,] cells;
    private int[,] occupancy;
    private int nextBuildingId = 1;
    private bool removeMode;
    private PlacedBuilding movingBuilding;
    private int hoverX = -1;
    private int hoverY = -1;
    private bool hoverValid;
    private Camera sceneCamera;
    private Transform tileRoot;
    private Transform buildingRoot;
    private GameObject ghostObject;
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
    private Material[] buildingMaterials;

    private bool IsMoving => movingBuilding != null;
    private int ActiveSize => IsMoving ? movingBuilding.size : Mathf.Clamp(selectedSize, 2, 4);
    private const float DragStartPixels = 8f;

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

        cameraPivot = GridCenterWorld();
        cameraPitchDegrees = Mathf.Clamp(cameraPitchDegrees, 25f, 80f);
        sceneCamera.orthographic = true;
        sceneCamera.orthographicSize = Mathf.Max(gridWidth, gridHeight) * 0.58f;
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = 200f;
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
        tileRoot = new GameObject("Grid Tiles").transform;
        tileRoot.SetParent(transform, false);
        buildingRoot = new GameObject("Buildings").transform;
        buildingRoot.SetParent(transform, false);

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

        RectTransform panel = CreateUiRect("Toolbar", canvas.transform, new Vector2(18f, -18f), new Vector2(450f, 96f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.10f, 0.11f, 0.86f);

        CreateButton(panel, "2x2", new Vector2(12f, -12f), () => SelectSize(2));
        CreateButton(panel, "3x3", new Vector2(92f, -12f), () => SelectSize(3));
        CreateButton(panel, "4x4", new Vector2(172f, -12f), () => SelectSize(4));
        CreateButton(panel, "Убрать", new Vector2(252f, -12f), () => removeMode = !removeMode, 92f);
        CreateButton(panel, "Очистить", new Vector2(348f, -12f), ClearBuildings, 84f);

        modeText = CreateText(panel, "", 17, new Vector2(12f, -58f), new Vector2(260f, 24f), TextAnchor.MiddleLeft);
        countText = CreateText(panel, "", 17, new Vector2(270f, -58f), new Vector2(132f, 24f), TextAnchor.MiddleRight);
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
        if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame) removeMode = !removeMode;
        if (keyboard.escapeKey.wasPressedThisFrame) CancelMove();
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSize(2);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSize(3);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSize(4);
        if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) removeMode = !removeMode;
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
            cameraPitchDegrees = Mathf.Clamp(cameraPitchDegrees - pointerDelta.y * cameraOrbitSensitivity, 25f, 80f);

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

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            pointerCameraControlActive = true;
            float zoomFactor = 1f - Mathf.Clamp(scrollY * cameraZoomSensitivity, -0.35f, 0.35f);
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
        if (!IsMoving)
        {
            EnsureGhost();
        }
    }

    private void UpdateHover()
    {
        hoverX = -1;
        hoverY = -1;
        hoverValid = false;

        RaycastHit hit;
        if (!TryRaycastPointer(out hit)) return;

        IsoGridCityCell cell = hit.collider.GetComponent<IsoGridCityCell>();
        if (cell == null)
        {
            IsoGridCityBuildingHandle handle = hit.collider.GetComponent<IsoGridCityBuildingHandle>();
            if (handle != null && buildings.TryGetValue(handle.buildingId, out PlacedBuilding building))
            {
                hoverX = building.originX;
                hoverY = building.originY;
                hoverValid = true;
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
            BeginMove(pressedBuildingId);
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
        if (cell != null)
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
        Occupy(originX, originY, size, id);
        CreateBuildingObject(id, size, originX, originY, color);

        movingBuilding = null;
        dragMoveActive = false;
        ClearPressedBuilding();
        removeMode = false;
    }

    private void BeginMove(int buildingId)
    {
        if (!buildings.TryGetValue(buildingId, out PlacedBuilding building)) return;

        movingBuilding = new PlacedBuilding
        {
            id = building.id,
            size = building.size,
            originX = building.originX,
            originY = building.originY,
            color = building.color
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
                movingBuilding.color);
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

    private void CreateBuildingObject(int id, int size, int originX, int originY, Color color)
    {
        float height = Mathf.Max(0.5f, size * buildingHeightPerCell);
        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = "Building " + id + " " + size + "x" + size;
        building.transform.SetParent(buildingRoot, false);
        building.transform.position = BuildingCenterWorld(originX, originY, size, height);
        building.transform.localScale = new Vector3(size * cellSize * 0.88f, height, size * cellSize * 0.88f);
        building.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);

        IsoGridCityBuildingHandle handle = building.AddComponent<IsoGridCityBuildingHandle>();
        handle.buildingId = id;

        buildings[id] = new PlacedBuilding
        {
            id = id,
            size = size,
            originX = originX,
            originY = originY,
            color = color,
            gameObject = building
        };
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

        if (!removeMode && hoverX >= 0 && hoverY >= 0)
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
        if (removeMode || hoverX < 0 || hoverY < 0)
        {
            if (ghostObject != null) ghostObject.SetActive(false);
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

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private sealed class PlacedBuilding
    {
        public int id;
        public int size;
        public int originX;
        public int originY;
        public Color color;
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
