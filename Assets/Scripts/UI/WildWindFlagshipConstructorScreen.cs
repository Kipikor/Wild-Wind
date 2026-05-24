using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[ExecuteAlways]
public sealed class WildWindFlagshipConstructorScreen : MonoBehaviour
{
    private const string CanvasName = "Flagship Constructor Canvas";
    private const string EventSystemName = "Flagship Constructor EventSystem";
    private const string PreviewCameraName = "Flagship Constructor Camera";
    private const int DeckCount = 4;
    private const int CellsPerDeck = 12;
    private const float CellWidth = 86f;
    private const float CellHeight = 82f;
    private const float CellGap = 6f;

    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Preview State")]
    [Range(3, 7)] public int flagshipRank = 3;
    public string flagshipName = "Флагман R3 Горизонт";

    private readonly Dictionary<string, RoomDefinition> definitionsById = new Dictionary<string, RoomDefinition>();
    private readonly List<RoomDefinition> catalogDefinitions = new List<RoomDefinition>();
    private readonly List<RoomInstance> rooms = new List<RoomInstance>();
    private readonly List<CellView> cells = new List<CellView>();
    private readonly List<GameObject> roomVisuals = new List<GameObject>();
    private readonly List<GameObject> inspectorObjects = new List<GameObject>();

    private RectTransform root;
    private RectTransform gridRoot;
    private RectTransform roomLayer;
    private RectTransform inspectorRoot;
    private RectTransform catalogRoot;
    private RectTransform ghostRoot;
    private Text statusText;
    private string selectedRoomId = "";
    private string draggedDefinitionId = "";
    private string draggedRoomId = "";
    private RectTransform dragGhost;
    private Vector2Int? hoveredCell;
    private static Font cachedDefaultFont;

    private static readonly Color BackgroundColor = new Color(0.018f, 0.024f, 0.030f, 1f);
    private static readonly Color TopBarColor = new Color(0.030f, 0.036f, 0.042f, 0.96f);
    private static readonly Color PanelColor = new Color(0.044f, 0.052f, 0.060f, 0.94f);
    private static readonly Color PanelSoftColor = new Color(0.070f, 0.076f, 0.078f, 0.90f);
    private static readonly Color ButtonColor = new Color(0.095f, 0.088f, 0.074f, 0.92f);
    private static readonly Color ButtonHoverColor = new Color(0.190f, 0.152f, 0.092f, 0.98f);
    private static readonly Color ButtonPressedColor = new Color(0.360f, 0.240f, 0.105f, 1f);
    private static readonly Color CellColor = new Color(0.040f, 0.047f, 0.052f, 0.90f);
    private static readonly Color CellAlternateColor = new Color(0.048f, 0.055f, 0.060f, 0.90f);
    private static readonly Color ValidCellColor = new Color(0.22f, 0.54f, 0.30f, 0.70f);
    private static readonly Color ReplaceCellColor = new Color(0.88f, 0.58f, 0.18f, 0.78f);
    private static readonly Color InvalidCellColor = new Color(0.70f, 0.16f, 0.14f, 0.76f);
    private static readonly Color AccentColor = new Color(0.92f, 0.67f, 0.34f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.84f, 0.70f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.68f, 0.67f, 1f);
    private static readonly Color DimTextColor = new Color(0.44f, 0.47f, 0.47f, 1f);
    private static readonly Color SystemRoomColor = new Color(0.36f, 0.38f, 0.40f, 1f);

    public void BeginDrag(string definitionId, PointerEventData eventData)
    {
        if (!definitionsById.TryGetValue(definitionId ?? "", out RoomDefinition definition))
        {
            return;
        }

        draggedDefinitionId = definition.id;
        draggedRoomId = "";
        selectedRoomId = "";
        RefreshInspector();
        CreateDragGhost(definition);
        MoveDrag(eventData);
        SetStatus("Тащи отсек на свободную цепочку ячеек. Поверх комнаты того же размера будет замена.");
    }

    public void BeginRoomDrag(string roomId, PointerEventData eventData)
    {
        RoomInstance room = FindRoom(roomId);
        if (room == null || !definitionsById.TryGetValue(room.definitionId, out RoomDefinition definition))
        {
            return;
        }

        selectedRoomId = room.roomId;
        if (room.systemLocked)
        {
            RefreshRoomVisuals();
            RefreshInspector();
            SetStatus("Системный отсек можно выбрать, но нельзя перетащить.");
            return;
        }

        draggedRoomId = room.roomId;
        draggedDefinitionId = room.definitionId;
        RefreshRoomVisuals();
        RefreshInspector();
        CreateDragGhost(definition);
        MoveDrag(eventData);
        SetStatus("Отсек взят за середину. Поднеси к палубе: форма примагнитится к ближайшим ячейкам.");
    }

    public void MoveDrag(PointerEventData eventData)
    {
        if (dragGhost == null || eventData == null)
        {
            return;
        }

        RoomDefinition definition = GetDraggedDefinition();
        hoveredCell = definition != null ? FindDragTargetCell(eventData.position, definition.widthCells) : null;
        if (hoveredCell.HasValue && definition != null)
        {
            MoveGhostToCell(hoveredCell.Value, definition);
        }
        else if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, eventData.position, null, out Vector2 localPoint))
        {
            dragGhost.anchoredPosition = localPoint;
        }

        RefreshPlacementPreview();
    }

    public void EndDrag(PointerEventData eventData)
    {
        MoveDrag(eventData);
        if (!string.IsNullOrWhiteSpace(draggedDefinitionId) && hoveredCell.HasValue)
        {
            if (string.IsNullOrWhiteSpace(draggedRoomId))
            {
                TryPlaceDraggedDefinition(hoveredCell.Value);
            }
            else
            {
                TryMoveDraggedRoom(hoveredCell.Value);
            }
        }

        draggedDefinitionId = "";
        draggedRoomId = "";
        hoveredCell = null;
        DestroyGeneratedObject(dragGhost != null ? dragGhost.gameObject : null);
        dragGhost = null;
        ResetCellHighlights();
        RefreshRoomVisuals();
    }

    public void SelectCatalogDefinition(string definitionId)
    {
        if (!definitionsById.TryGetValue(definitionId ?? "", out RoomDefinition definition))
        {
            return;
        }

        SetStatus(definition.displayName + ": ширина " + definition.widthCells.ToString(CultureInfo.InvariantCulture) + " клетки. Перетащи карточку на палубу.");
    }

    public void SelectRoom(string roomId)
    {
        selectedRoomId = roomId ?? "";
        RefreshRoomVisuals();
        RefreshInspector();
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            RebuildScreen();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying && transform.Find(CanvasName) == null)
        {
            RebuildEditableScreen();
        }
    }

    [ContextMenu("Rebuild flagship constructor")]
    public void RebuildEditableScreen()
    {
        RebuildScreen();
        MarkSceneDirtyIfEditing();
    }

    public void RebuildScreen()
    {
        ClearGeneratedChildren();
        BuildDefinitions();
        BuildInitialRooms();
        EnsurePreviewCamera();
        EnsureEventSystem(transform);
        BuildCanvas();
        BuildBackground();
        BuildTopBar();
        BuildCatalog();
        BuildShipPanel();
        BuildInspectorPanel();
        BuildStatusBar();
        RefreshGridCells();
        RefreshRoomVisuals();
        RefreshInspector();
    }

    private void BuildDefinitions()
    {
        definitionsById.Clear();
        catalogDefinitions.Clear();

        RegisterDefinition(new RoomDefinition("bridge", "Мостик", 2, "Системный центр управления.", SystemRoomColor, false, "СИСТ"));
        RegisterDefinition(new RoomDefinition("engine", "Двигатели", 2, "Системный машинный отсек.", SystemRoomColor, false, "ДВГ"));
        RegisterDefinition(new RoomDefinition("hangar", "Ангар", 3, "Предустановленный отсек вылета малых машин.", SystemRoomColor, false, "АНГ"));

        RegisterDefinition(new RoomDefinition("storage_small", "Склад", 1, "Компактный запас воды, еды и аварийных материалов.", new Color(0.47f, 0.56f, 0.47f, 1f), true, "СКЛ"));
        RegisterDefinition(new RoomDefinition("infirmary", "Медпункт", 1, "Снижает риск тяжёлых последствий от усталости экипажа.", new Color(0.54f, 0.62f, 0.67f, 1f), true, "МЕД"));
        RegisterDefinition(new RoomDefinition("canteen", "Столовая", 2, "Восстанавливает силы экипажа во время экспедиции.", new Color(0.70f, 0.48f, 0.28f, 1f), true, "ЕДА"));
        RegisterDefinition(new RoomDefinition("bunks", "Каюты", 2, "Удерживают усталость и дают запас автономности.", new Color(0.44f, 0.50f, 0.62f, 1f), true, "СОН"));
        RegisterDefinition(new RoomDefinition("repair", "Ремонтная", 2, "Запускает авторемонт поломок, тратя потребности экипажа.", new Color(0.62f, 0.52f, 0.34f, 1f), true, "РЕМ"));
        RegisterDefinition(new RoomDefinition("power", "Энергоблок", 2, "Питает активные отсеки и повышает цену аварий.", new Color(0.65f, 0.58f, 0.32f, 1f), true, "ЭН"));
        RegisterDefinition(new RoomDefinition("workshop", "Мастерская", 3, "Большой отсек для тяжёлых ремонтов и производства в пути.", new Color(0.58f, 0.45f, 0.36f, 1f), true, "ЦЕХ"));
        RegisterDefinition(new RoomDefinition("canteen_large", "Большая столовая", 3, "Сильная регенерация сил, но дорогая поломка и содержание.", new Color(0.76f, 0.52f, 0.30f, 1f), true, "ЕДА+"));
    }

    private void RegisterDefinition(RoomDefinition definition)
    {
        definitionsById[definition.id] = definition;
        if (definition.showInCatalog)
        {
            catalogDefinitions.Add(definition);
        }
    }

    private void BuildInitialRooms()
    {
        rooms.Clear();
        AddRoom("bridge", "bridge_0", 0, 0, 100, true);
        AddRoom("hangar", "hangar_0", 1, 4, 100, true);
        AddRoom("engine", "engine_0", 3, 10, 100, true);
        AddRoom("storage_small", "storage_0", 2, 0, 50, false);
        AddRoom("repair", "repair_0", 2, 2, 100, false);
        AddRoom("canteen", "canteen_0", 0, 5, 100, false);
        selectedRoomId = "canteen_0";
    }

    private void AddRoom(string definitionId, string roomId, int deckIndex, int startCell, int modePercent, bool systemLocked)
    {
        if (!definitionsById.TryGetValue(definitionId, out RoomDefinition definition))
        {
            return;
        }

        rooms.Add(new RoomInstance
        {
            roomId = roomId,
            definitionId = definitionId,
            deckIndex = deckIndex,
            startCell = startCell,
            widthCells = definition.widthCells,
            modePercent = modePercent,
            systemLocked = systemLocked
        });
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas screenCanvas = canvasObject.GetComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateRect("Flagship Constructor Root", screenCanvas.transform, StretchFull()).GetComponent<RectTransform>();
        ghostRoot = CreateRect("Drag Ghost Root", root, StretchFull()).GetComponent<RectTransform>();
        ghostRoot.SetAsLastSibling();
    }

    private void BuildBackground()
    {
        Image background = CreateImage("Background", root, StretchFull(), BackgroundColor);
        background.raycastTarget = false;

        Image cloudBand = CreateImage("Storm Band", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 180f)
        }, new Color(0.10f, 0.09f, 0.075f, 0.36f));
        cloudBand.raycastTarget = false;
    }

    private void BuildTopBar()
    {
        RectTransform topBar = CreateImage("Top Bar", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 88f)
        }, TopBarColor).GetComponent<RectTransform>();

        Text title = CreateText(topBar, "Конструктор отсеков флагмана", 32, new Vector2(46f, -12f), new Vector2(660f, 44f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;
        CreateText(topBar, flagshipName + " / R" + flagshipRank.ToString(CultureInfo.InvariantCulture), 18, new Vector2(48f, -54f), new Vector2(620f, 26f), TextAnchor.MiddleLeft, MutedTextColor);

        CreateTopMetric(topBar, "Силы", "100", new Vector2(-706f, -18f));
        CreateTopMetric(topBar, "Еда", "72", new Vector2(-540f, -18f));
        CreateTopMetric(topBar, "Вода", "68", new Vector2(-374f, -18f));
        CreateTopMetric(topBar, "Ремонт", "42/мин", new Vector2(-208f, -18f));
    }

    private void CreateTopMetric(RectTransform parent, string label, string value, Vector2 position)
    {
        RectTransform metric = CreateImage("Metric " + label, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(1f, 1f),
            anchoredPosition = position,
            sizeDelta = new Vector2(144f, 52f)
        }, new Color(0.048f, 0.055f, 0.060f, 0.84f)).GetComponent<RectTransform>();

        CreateText(metric, label, 13, new Vector2(12f, -4f), new Vector2(118f, 20f), TextAnchor.MiddleLeft, MutedTextColor);
        Text valueText = CreateText(metric, value, 19, new Vector2(12f, -25f), new Vector2(118f, 24f), TextAnchor.MiddleLeft, TextColor);
        valueText.fontStyle = FontStyle.Bold;
    }

    private void BuildCatalog()
    {
        RectTransform panel = CreatePanel("Catalog Panel", new Vector2(360f, 860f), new Vector2(42f, -112f), new Vector2(0f, 1f));
        Text title = CreateText(panel, "Отсеки", 28, new Vector2(24f, -20f), new Vector2(280f, 38f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;
        CreateText(panel, "Тащи карточку на свободные клетки или на комнату того же размера.", 15, new Vector2(26f, -58f), new Vector2(300f, 54f), TextAnchor.UpperLeft, MutedTextColor);

        catalogRoot = CreateRect("Catalog Content", panel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(20f, -132f),
            sizeDelta = new Vector2(320f, 680f)
        }).GetComponent<RectTransform>();

        for (int i = 0; i < catalogDefinitions.Count; i++)
        {
            CreateCatalogCard(catalogDefinitions[i], i);
        }
    }

    private void CreateCatalogCard(RoomDefinition definition, int index)
    {
        RectTransform card = CreateImage("Catalog " + definition.id, catalogRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(0f, -index * 76f),
            sizeDelta = new Vector2(320f, 64f)
        }, ButtonColor).GetComponent<RectTransform>();

        Button button = card.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        button.colors = colors;

        WildWindFlagshipCatalogDragSource dragSource = card.gameObject.AddComponent<WildWindFlagshipCatalogDragSource>();
        dragSource.owner = this;
        dragSource.definitionId = definition.id;

        Text icon = CreateText(card, definition.shortLabel, 17, new Vector2(12f, -11f), new Vector2(56f, 40f), TextAnchor.MiddleCenter, TextColor);
        icon.fontStyle = FontStyle.Bold;
        CreateText(card, definition.displayName, 18, new Vector2(78f, -8f), new Vector2(168f, 24f), TextAnchor.MiddleLeft, TextColor);
        CreateText(card, definition.widthCells.ToString(CultureInfo.InvariantCulture) + " клетки", 14, new Vector2(78f, -33f), new Vector2(128f, 20f), TextAnchor.MiddleLeft, MutedTextColor);
        CreateText(card, "≡".PadRight(definition.widthCells, '≡'), 20, new Vector2(238f, -18f), new Vector2(60f, 28f), TextAnchor.MiddleRight, AccentColor);
    }

    private void BuildShipPanel()
    {
        RectTransform panel = CreatePanel("Ship Panel", new Vector2(1110f, 860f), new Vector2(424f, -112f), new Vector2(0f, 1f));
        Text title = CreateText(panel, "Палубы универсальные", 28, new Vector2(28f, -20f), new Vector2(520f, 38f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;
        CreateText(panel, "Серые комнаты предустановлены. Обычные можно заменить перетаскиванием.", 16, new Vector2(30f, -58f), new Vector2(760f, 26f), TextAnchor.MiddleLeft, MutedTextColor);

        RectTransform hull = CreateImage("Hull Silhouette", panel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(26f, -118f),
            sizeDelta = new Vector2(1058f, 524f)
        }, new Color(0.025f, 0.030f, 0.034f, 0.96f)).GetComponent<RectTransform>();

        CreateImage("Hull Nose", hull, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 0.5f),
            anchorMax = new Vector2(1f, 0.5f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(-2f, 0f),
            sizeDelta = new Vector2(54f, 240f)
        }, new Color(0.025f, 0.030f, 0.034f, 0.96f));

        gridRoot = CreateRect("Deck Grid", hull, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(20f, -54f),
            sizeDelta = new Vector2(CellsPerDeck * CellWidth + (CellsPerDeck - 1) * CellGap, DeckCount * CellHeight + (DeckCount - 1) * CellGap)
        }).GetComponent<RectTransform>();

        roomLayer = CreateRect("Room Layer", gridRoot, StretchFull()).GetComponent<RectTransform>();

        for (int deck = 0; deck < DeckCount; deck++)
        {
            CreateText(hull, "Палуба " + (deck + 1).ToString(CultureInfo.InvariantCulture), 14, new Vector2(22f, -52f - deck * (CellHeight + CellGap)), new Vector2(90f, 22f), TextAnchor.MiddleLeft, DimTextColor);
            for (int cell = 0; cell < CellsPerDeck; cell++)
            {
                RectTransform cellRect = CreateImage("Cell " + deck + "-" + cell, gridRoot, new RectTransformSpec
                {
                    anchorMin = new Vector2(0f, 1f),
                    anchorMax = new Vector2(0f, 1f),
                    pivot = new Vector2(0f, 1f),
                    anchoredPosition = CellPosition(deck, cell),
                    sizeDelta = new Vector2(CellWidth, CellHeight)
                }, ((deck + cell) % 2 == 0) ? CellColor : CellAlternateColor).GetComponent<RectTransform>();

                cells.Add(new CellView
                {
                    deckIndex = deck,
                    cellIndex = cell,
                    rect = cellRect,
                    image = cellRect.GetComponent<Image>(),
                    baseColor = ((deck + cell) % 2 == 0) ? CellColor : CellAlternateColor
                });
            }
        }

        roomLayer.SetAsLastSibling();
        CreateLegend(panel);
    }

    private void CreateLegend(RectTransform panel)
    {
        RectTransform legend = CreateImage("Legend", panel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(28f, -678f),
            sizeDelta = new Vector2(1048f, 66f)
        }, PanelSoftColor).GetComponent<RectTransform>();

        CreateLegendItem(legend, "Можно ставить", ValidCellColor, new Vector2(20f, -18f));
        CreateLegendItem(legend, "Замена", ReplaceCellColor, new Vector2(230f, -18f));
        CreateLegendItem(legend, "Нельзя", InvalidCellColor, new Vector2(402f, -18f));
        CreateText(legend, "UX-правило сейчас простое: размер 1-3 клетки, палубы без типов, системные отсеки не сносятся.", 16, new Vector2(584f, -16f), new Vector2(430f, 32f), TextAnchor.MiddleLeft, MutedTextColor);
    }

    private void CreateLegendItem(RectTransform parent, string label, Color color, Vector2 position)
    {
        CreateImage("Legend Swatch", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = position,
            sizeDelta = new Vector2(24f, 24f)
        }, color);
        CreateText(parent, label, 15, new Vector2(position.x + 34f, position.y + 1f), new Vector2(150f, 22f), TextAnchor.MiddleLeft, MutedTextColor);
    }

    private void BuildInspectorPanel()
    {
        inspectorRoot = CreatePanel("Inspector Panel", new Vector2(342f, 860f), new Vector2(-42f, -112f), new Vector2(1f, 1f));
    }

    private void BuildStatusBar()
    {
        RectTransform status = CreateImage("Status Bar", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 52f)
        }, TopBarColor).GetComponent<RectTransform>();

        statusText = CreateText(status, "Выбери или перетащи отсек.", 17, new Vector2(46f, 0f), new Vector2(1100f, 52f), TextAnchor.MiddleLeft, AccentColor);
    }

    private void RefreshGridCells()
    {
        ResetCellHighlights();
    }

    private void RefreshRoomVisuals()
    {
        for (int i = 0; i < roomVisuals.Count; i++)
        {
            DestroyGeneratedObject(roomVisuals[i]);
        }

        roomVisuals.Clear();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomInstance room = rooms[i];
            if (!definitionsById.TryGetValue(room.definitionId, out RoomDefinition definition))
            {
                continue;
            }

            bool selected = room.roomId == selectedRoomId;
            bool dragged = room.roomId == draggedRoomId;
            Color roomColor = selected ? Brighten(definition.color, 0.18f) : definition.color;
            if (dragged)
            {
                roomColor = new Color(roomColor.r, roomColor.g, roomColor.b, 0.30f);
            }

            RectTransform rect = CreateImage("Room " + room.roomId, roomLayer, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = CellPosition(room.deckIndex, room.startCell),
                sizeDelta = new Vector2(room.widthCells * CellWidth + (room.widthCells - 1) * CellGap, CellHeight)
            }, roomColor).GetComponent<RectTransform>();
            roomVisuals.Add(rect.gameObject);

            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = roomColor;
            colors.highlightedColor = Brighten(definition.color, 0.26f);
            colors.pressedColor = Darken(definition.color, 0.16f);
            button.colors = colors;

            WildWindFlagshipRoomHandle handle = rect.gameObject.AddComponent<WildWindFlagshipRoomHandle>();
            handle.owner = this;
            handle.roomId = room.roomId;

            CreateText(rect, definition.shortLabel, 24, new Vector2(10f, -10f), new Vector2(68f, 34f), TextAnchor.MiddleCenter, TextColor).fontStyle = FontStyle.Bold;
            CreateText(rect, definition.displayName, 18, new Vector2(84f, -10f), new Vector2(rect.sizeDelta.x - 96f, 26f), TextAnchor.MiddleLeft, TextColor);
            CreateText(rect, room.systemLocked ? "Системный" : "Режим " + FormatMode(room.modePercent), 14, new Vector2(86f, -42f), new Vector2(rect.sizeDelta.x - 98f, 22f), TextAnchor.MiddleLeft, room.systemLocked ? DimTextColor : MutedTextColor);

            RectTransform modeBar = CreateImage("Mode Bar", rect, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(0f, 0f),
                anchoredPosition = new Vector2(8f, 8f),
                sizeDelta = new Vector2(-16f, 5f)
            }, new Color(0f, 0f, 0f, 0.34f)).GetComponent<RectTransform>();

            float fillWidth = Mathf.Max(0.05f, Mathf.Clamp01(room.modePercent / 100f)) * (modeBar.sizeDelta.x <= 0f ? 120f : modeBar.sizeDelta.x);
            CreateImage("Mode Fill", modeBar, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 0f),
                anchoredPosition = Vector2.zero,
                sizeDelta = new Vector2(fillWidth, 0f)
            }, room.systemLocked ? DimTextColor : AccentColor);
        }
    }

    private void RefreshInspector()
    {
        if (inspectorRoot == null)
        {
            return;
        }

        for (int i = 0; i < inspectorObjects.Count; i++)
        {
            DestroyGeneratedObject(inspectorObjects[i]);
        }

        inspectorObjects.Clear();
        Text title = AddInspectorText("Инспектор", 27, new Vector2(24f, -20f), new Vector2(280f, 38f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;

        RoomInstance room = FindRoom(selectedRoomId);
        if (room == null || !definitionsById.TryGetValue(room.definitionId, out RoomDefinition definition))
        {
            AddInspectorText("Комната не выбрана.", 17, new Vector2(26f, -76f), new Vector2(280f, 48f), TextAnchor.UpperLeft, MutedTextColor);
            AddInspectorText("Перетащи карточку из каталога или щёлкни по отсеку на палубе.", 15, new Vector2(26f, -130f), new Vector2(280f, 84f), TextAnchor.UpperLeft, DimTextColor);
            return;
        }

        AddInspectorText(definition.displayName, 24, new Vector2(26f, -78f), new Vector2(280f, 34f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        AddInspectorText(definition.description, 15, new Vector2(26f, -118f), new Vector2(280f, 70f), TextAnchor.UpperLeft, MutedTextColor);

        AddInspectorMetric("Размер", room.widthCells.ToString(CultureInfo.InvariantCulture) + " клетки", -210f);
        AddInspectorMetric("Палуба", (room.deckIndex + 1).ToString(CultureInfo.InvariantCulture), -296f);
        AddInspectorMetric("Позиция", (room.startCell + 1).ToString(CultureInfo.InvariantCulture), -382f);

        AddInspectorText("Режим работы", 18, new Vector2(26f, -480f), new Vector2(280f, 28f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        int[] modes = { 0, 25, 50, 100 };
        for (int i = 0; i < modes.Length; i++)
        {
            int mode = modes[i];
            Button modeButton = AddInspectorButton(FormatMode(mode), new Vector2(26f + i * 74f, -522f), new Vector2(64f, 42f), () => SetRoomMode(room.roomId, mode));
            modeButton.interactable = !room.systemLocked;
        }

        AddInspectorText(room.systemLocked ? "Предустановленный системный отсек нельзя снести или заменить." : "Обычный отсек можно освободить или заменить отсеком того же размера.", 15, new Vector2(26f, -590f), new Vector2(280f, 76f), TextAnchor.UpperLeft, room.systemLocked ? DimTextColor : MutedTextColor);

        Button clearButton = AddInspectorButton("Освободить", new Vector2(26f, -704f), new Vector2(136f, 48f), () => RemoveSelectedRoom());
        clearButton.interactable = !room.systemLocked;
        AddInspectorButton("Сбросить макет", new Vector2(176f, -704f), new Vector2(136f, 48f), () =>
        {
            BuildInitialRooms();
            RefreshRoomVisuals();
            RefreshInspector();
            SetStatus("Макет вернулся к стартовому состоянию.");
        });
    }

    private Text AddInspectorText(string value, int fontSize, Vector2 position, Vector2 size, TextAnchor alignment, Color color)
    {
        Text text = CreateText(inspectorRoot, value, fontSize, position, size, alignment, color);
        inspectorObjects.Add(text.gameObject);
        return text;
    }

    private void AddInspectorMetric(string label, string value, float y)
    {
        RectTransform metric = CreateImage("Inspector Metric " + label, inspectorRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(24f, y),
            sizeDelta = new Vector2(292f, 64f)
        }, PanelSoftColor).GetComponent<RectTransform>();
        inspectorObjects.Add(metric.gameObject);

        CreateText(metric, label, 13, new Vector2(14f, -6f), new Vector2(250f, 20f), TextAnchor.MiddleLeft, MutedTextColor);
        CreateText(metric, value, 21, new Vector2(14f, -30f), new Vector2(250f, 28f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
    }

    private Button AddInspectorButton(string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton("Inspector Button " + label, inspectorRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = position,
            sizeDelta = size
        }, ButtonColor, action);
        inspectorObjects.Add(button.gameObject);

        Text text = CreateText(button.transform as RectTransform, label, 16, Vector2.zero, size, TextAnchor.MiddleCenter, TextColor, new Vector2(0.5f, 0.5f));
        text.raycastTarget = false;
        return button;
    }

    private void SetRoomMode(string roomId, int mode)
    {
        RoomInstance room = FindRoom(roomId);
        if (room == null || room.systemLocked)
        {
            return;
        }

        room.modePercent = Mathf.Clamp(mode, 0, 100);
        RefreshRoomVisuals();
        RefreshInspector();
        SetStatus(GetRoomDisplayName(room) + ": режим " + FormatMode(room.modePercent) + ".");
    }

    private void RemoveSelectedRoom()
    {
        RoomInstance room = FindRoom(selectedRoomId);
        if (room == null || room.systemLocked)
        {
            return;
        }

        string removedName = GetRoomDisplayName(room);
        rooms.Remove(room);
        selectedRoomId = "";
        RefreshRoomVisuals();
        RefreshInspector();
        SetStatus("Отсек освобождён: " + removedName + ".");
    }

    private void TryPlaceDraggedDefinition(Vector2Int cell)
    {
        if (!definitionsById.TryGetValue(draggedDefinitionId, out RoomDefinition definition))
        {
            return;
        }

        PlacementPreview preview = EvaluatePlacement(definition, cell.x, cell.y);
        if (!preview.valid)
        {
            SetStatus(preview.message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(preview.replacedRoomId))
        {
            RoomInstance replaced = FindRoom(preview.replacedRoomId);
            if (replaced != null)
            {
                rooms.Remove(replaced);
            }
        }

        string roomId = definition.id + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        RoomInstance room = new RoomInstance
        {
            roomId = roomId,
            definitionId = definition.id,
            deckIndex = cell.x,
            startCell = cell.y,
            widthCells = definition.widthCells,
            modePercent = 100,
            systemLocked = false
        };
        rooms.Add(room);
        selectedRoomId = roomId;
        RefreshRoomVisuals();
        RefreshInspector();
        SetStatus(string.IsNullOrWhiteSpace(preview.replacedRoomId) ? "Отсек установлен: " + definition.displayName + "." : "Отсек заменён: " + definition.displayName + ".");
    }

    private void TryMoveDraggedRoom(Vector2Int cell)
    {
        RoomInstance room = FindRoom(draggedRoomId);
        if (room == null || room.systemLocked || !definitionsById.TryGetValue(room.definitionId, out RoomDefinition definition))
        {
            return;
        }

        PlacementPreview preview = EvaluatePlacement(definition, cell.x, cell.y, room.roomId);
        if (!preview.valid)
        {
            SetStatus(preview.message);
            return;
        }

        if (!string.IsNullOrWhiteSpace(preview.replacedRoomId))
        {
            RoomInstance replaced = FindRoom(preview.replacedRoomId);
            if (replaced != null)
            {
                rooms.Remove(replaced);
            }
        }

        room.deckIndex = cell.x;
        room.startCell = cell.y;
        room.widthCells = definition.widthCells;
        selectedRoomId = room.roomId;
        RefreshRoomVisuals();
        RefreshInspector();
        SetStatus(string.IsNullOrWhiteSpace(preview.replacedRoomId) ? "Отсек перенесён: " + definition.displayName + "." : "Отсек перенесён с заменой: " + definition.displayName + ".");
    }

    private void RefreshPlacementPreview()
    {
        ResetCellHighlights();
        RoomDefinition definition = GetDraggedDefinition();
        if (definition == null || !hoveredCell.HasValue)
        {
            return;
        }

        Vector2Int cell = hoveredCell.Value;
        PlacementPreview preview = EvaluatePlacement(definition, cell.x, cell.y, draggedRoomId);
        Color highlight = !preview.valid ? InvalidCellColor : (string.IsNullOrWhiteSpace(preview.replacedRoomId) ? ValidCellColor : ReplaceCellColor);
        for (int i = 0; i < definition.widthCells; i++)
        {
            CellView view = GetCell(cell.x, cell.y + i);
            if (view != null && view.image != null)
            {
                view.image.color = highlight;
            }
        }

        Image ghostImage = dragGhost != null ? dragGhost.GetComponent<Image>() : null;
        if (ghostImage != null)
        {
            Color ghostColor = definition.color;
            if (!preview.valid)
            {
                ghostColor = InvalidCellColor;
            }
            else if (!string.IsNullOrWhiteSpace(preview.replacedRoomId))
            {
                ghostColor = ReplaceCellColor;
            }

            ghostImage.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.72f);
        }
    }

    private PlacementPreview EvaluatePlacement(RoomDefinition definition, int deckIndex, int startCell, string ignoredRoomId = "")
    {
        PlacementPreview preview = new PlacementPreview
        {
            valid = false,
            message = "Нельзя поставить отсек."
        };

        if (definition == null)
        {
            return preview;
        }

        if (deckIndex < 0 || deckIndex >= DeckCount || startCell < 0 || startCell + definition.widthCells > CellsPerDeck)
        {
            preview.message = "Не хватает места на этой палубе.";
            return preview;
        }

        RoomInstance overlappedRoom = null;
        for (int offset = 0; offset < definition.widthCells; offset++)
        {
            RoomInstance occupied = FindRoomAt(deckIndex, startCell + offset);
            if (occupied == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(ignoredRoomId) && occupied.roomId == ignoredRoomId)
            {
                continue;
            }

            if (occupied.systemLocked)
            {
                preview.message = "Системные отсеки нельзя заменить.";
                return preview;
            }

            if (overlappedRoom == null)
            {
                overlappedRoom = occupied;
            }
            else if (overlappedRoom != occupied)
            {
                preview.message = "Замена должна покрывать одну комнату, не несколько.";
                return preview;
            }
        }

        if (overlappedRoom != null)
        {
            bool exactSameFootprint = overlappedRoom.deckIndex == deckIndex &&
                overlappedRoom.startCell == startCell &&
                overlappedRoom.widthCells == definition.widthCells;
            if (!exactSameFootprint)
            {
                preview.message = "Для замены нужен отсек того же размера и в той же рамке.";
                return preview;
            }

            preview.valid = true;
            preview.replacedRoomId = overlappedRoom.roomId;
            preview.message = "Будет замена: " + GetRoomDisplayName(overlappedRoom) + " -> " + definition.displayName + ".";
            return preview;
        }

        preview.valid = true;
        preview.message = "Можно установить: " + definition.displayName + ".";
        return preview;
    }

    private Vector2Int? FindDragTargetCell(Vector2 screenPoint, int widthCells)
    {
        if (gridRoot == null)
        {
            return null;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRoot, screenPoint, null, out Vector2 localPoint))
        {
            return null;
        }

        float gridWidth = CellsPerDeck * CellWidth + (CellsPerDeck - 1) * CellGap;
        float gridHeight = DeckCount * CellHeight + (DeckCount - 1) * CellGap;
        float margin = Mathf.Max(CellWidth, CellHeight) * 0.55f;
        if (localPoint.x < -margin || localPoint.x > gridWidth + margin || localPoint.y > margin || localPoint.y < -gridHeight - margin)
        {
            return null;
        }

        int deckIndex = Mathf.RoundToInt((-localPoint.y - CellHeight * 0.5f) / (CellHeight + CellGap));
        int maxStartCell = Mathf.Max(0, CellsPerDeck - Mathf.Clamp(widthCells, 1, 3));
        float roomWidth = widthCells * CellWidth + (widthCells - 1) * CellGap;
        int startCell = Mathf.RoundToInt((localPoint.x - roomWidth * 0.5f) / (CellWidth + CellGap));
        deckIndex = Mathf.Clamp(deckIndex, 0, DeckCount - 1);
        startCell = Mathf.Clamp(startCell, 0, maxStartCell);
        return new Vector2Int(deckIndex, startCell);
    }

    private void MoveGhostToCell(Vector2Int cell, RoomDefinition definition)
    {
        if (dragGhost == null || gridRoot == null || root == null || definition == null)
        {
            return;
        }

        float roomWidth = definition.widthCells * CellWidth + (definition.widthCells - 1) * CellGap;
        Vector2 gridLocalCenter = CellPosition(cell.x, cell.y) + new Vector2(roomWidth * 0.5f, -CellHeight * 0.5f);
        Vector3 worldPosition = gridRoot.TransformPoint(gridLocalCenter);
        dragGhost.anchoredPosition = root.InverseTransformPoint(worldPosition);
    }

    private RoomDefinition GetDraggedDefinition()
    {
        if (string.IsNullOrWhiteSpace(draggedDefinitionId))
        {
            return null;
        }

        definitionsById.TryGetValue(draggedDefinitionId, out RoomDefinition definition);
        return definition;
    }

    private RoomInstance FindRoomAt(int deckIndex, int cellIndex)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomInstance room = rooms[i];
            if (room.deckIndex == deckIndex && cellIndex >= room.startCell && cellIndex < room.startCell + room.widthCells)
            {
                return room;
            }
        }

        return null;
    }

    private RoomInstance FindRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return null;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].roomId == roomId)
            {
                return rooms[i];
            }
        }

        return null;
    }

    private CellView GetCell(int deckIndex, int cellIndex)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            CellView view = cells[i];
            if (view.deckIndex == deckIndex && view.cellIndex == cellIndex)
            {
                return view;
            }
        }

        return null;
    }

    private void ResetCellHighlights()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].image != null)
            {
                cells[i].image.color = cells[i].baseColor;
            }
        }
    }

    private void CreateDragGhost(RoomDefinition definition)
    {
        DestroyGeneratedObject(dragGhost != null ? dragGhost.gameObject : null);
        dragGhost = CreateImage("Drag Ghost " + definition.id, ghostRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(definition.widthCells * CellWidth + (definition.widthCells - 1) * CellGap, CellHeight)
        }, new Color(definition.color.r, definition.color.g, definition.color.b, 0.72f)).GetComponent<RectTransform>();
        CanvasGroup group = dragGhost.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        CreateText(dragGhost, definition.displayName, 20, Vector2.zero, dragGhost.sizeDelta, TextAnchor.MiddleCenter, TextColor, new Vector2(0.5f, 0.5f)).fontStyle = FontStyle.Bold;
    }

    private string GetRoomDisplayName(RoomInstance room)
    {
        if (room == null || !definitionsById.TryGetValue(room.definitionId, out RoomDefinition definition))
        {
            return "Отсек";
        }

        return definition.displayName;
    }

    private static string FormatMode(int modePercent)
    {
        return modePercent <= 0 ? "выкл." : modePercent.ToString(CultureInfo.InvariantCulture) + "%";
    }

    private Vector2 CellPosition(int deckIndex, int cellIndex)
    {
        return new Vector2(cellIndex * (CellWidth + CellGap), -deckIndex * (CellHeight + CellGap));
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? "";
        }
    }

    private RectTransform CreatePanel(string name, Vector2 size, Vector2 position, Vector2 anchor)
    {
        return CreateImage(name, root, new RectTransformSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = anchor,
            anchoredPosition = position,
            sizeDelta = size
        }, PanelColor).GetComponent<RectTransform>();
    }

    private Button CreateButton(string name, RectTransform parent, RectTransformSpec spec, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateRect(name, parent, spec);
        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = ButtonHoverColor;
        colors.disabledColor = new Color(0.060f, 0.060f, 0.056f, 0.72f);
        button.colors = colors;
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        return button;
    }

    private Image CreateImage(string name, Transform parent, RectTransformSpec spec, Color color)
    {
        GameObject imageObject = CreateRect(name, parent, spec);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(RectTransform parent, string textValue, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color)
    {
        return CreateText(parent, textValue, fontSize, anchoredPosition, size, alignment, color, new Vector2(0f, 1f));
    }

    private Text CreateText(RectTransform parent, string textValue, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color, Vector2 anchor)
    {
        GameObject textObject = CreateRect("Text", parent, new RectTransformSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = anchor,
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });

        Text text = textObject.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = textValue ?? "";
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static RectTransformSpec StretchFull()
    {
        return new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        };
    }

    private static GameObject CreateRect(string name, Transform parent, RectTransformSpec spec)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMax;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.sizeDelta;
        return gameObject;
    }

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont != null)
        {
            return cachedDefaultFont;
        }

        cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedDefaultFont;
    }

    private static Color Brighten(Color color, float amount)
    {
        return new Color(Mathf.Clamp01(color.r + amount), Mathf.Clamp01(color.g + amount), Mathf.Clamp01(color.b + amount), color.a);
    }

    private static Color Darken(Color color, float amount)
    {
        return new Color(Mathf.Clamp01(color.r - amount), Mathf.Clamp01(color.g - amount), Mathf.Clamp01(color.b - amount), color.a);
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(transform.GetChild(i).gameObject);
        }

        root = null;
        gridRoot = null;
        roomLayer = null;
        inspectorRoot = null;
        catalogRoot = null;
        ghostRoot = null;
        statusText = null;
        dragGhost = null;
        cells.Clear();
        roomVisuals.Clear();
        inspectorObjects.Clear();
    }

    private static void DestroyGeneratedObject(UnityEngine.Object target)
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

    private void EnsurePreviewCamera()
    {
        if (FindFirstObjectByType<Camera>() != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject(PreviewCameraName, typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BackgroundColor;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
    }

    public static void EnsureEventSystem(Transform parent)
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            ConfigureEventSystemInput(existing.gameObject);
            return;
        }

        GameObject eventSystem = new GameObject(EventSystemName, typeof(EventSystem));
        if (parent != null)
        {
            eventSystem.transform.SetParent(parent, false);
        }

        ConfigureEventSystemInput(eventSystem);
    }

    private static void ConfigureEventSystemInput(GameObject eventSystemObject)
    {
        if (eventSystemObject == null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            DestroyGeneratedObject(legacyModule);
        }

        if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
#else
        if (eventSystemObject.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
#endif
    }

    private static void MarkSceneDirtyIfEditing()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
#endif
    }

    private sealed class RoomDefinition
    {
        public readonly string id;
        public readonly string displayName;
        public readonly int widthCells;
        public readonly string description;
        public readonly Color color;
        public readonly bool showInCatalog;
        public readonly string shortLabel;

        public RoomDefinition(string id, string displayName, int widthCells, string description, Color color, bool showInCatalog, string shortLabel)
        {
            this.id = id;
            this.displayName = displayName;
            this.widthCells = Mathf.Clamp(widthCells, 1, 3);
            this.description = description;
            this.color = color;
            this.showInCatalog = showInCatalog;
            this.shortLabel = shortLabel;
        }
    }

    private sealed class RoomInstance
    {
        public string roomId = "";
        public string definitionId = "";
        public int deckIndex;
        public int startCell;
        public int widthCells;
        public int modePercent = 100;
        public bool systemLocked;
    }

    private sealed class CellView
    {
        public int deckIndex;
        public int cellIndex;
        public RectTransform rect;
        public Image image;
        public Color baseColor;
    }

    private struct PlacementPreview
    {
        public bool valid;
        public string replacedRoomId;
        public string message;
    }

    private struct RectTransformSpec
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }
}
