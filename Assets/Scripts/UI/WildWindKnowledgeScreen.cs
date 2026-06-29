using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class WildWindKnowledgeScreen : MonoBehaviour
{
    private const string CanvasName = "Knowledge Canvas";
    private const string EventSystemName = "Knowledge EventSystem";
    private const int DesignedTreeColumnCount = 10;
    private const float LiveResearchRefreshIntervalSeconds = 0.2f;

    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public int sortingOrder = 6500;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform rubricTabRoot;
    private RectTransform rubricViewport;
    private ScrollRect rubricScrollRect;
    private RectTransform treeViewport;
    private RectTransform treeContent;
    private RectTransform inspectorPanel;
    private ScrollRect treeScrollRect;
    private Text titleText;
    private Text subtitleText;
    private Text resourceText;
    private Text contentText;
    private Text actionTitleText;
    private Text selectedTitleText;
    private Text selectedStatusText;
    private Text selectedEffectText;
    private Text selectedCostText;
    private Text selectedRequirementText;
    private Text statusText;
    private Text primaryActionText;
    private Text secondaryActionText;
    private MetaGameState runtimeMeta;
    private string selectedCategoryId = "";
    private string selectedTechnologyId = "";
    private string lastMessage = "Архивы готовы.";
    private Func<bool> closeAction;
    private int visibleRubricTabCount;
    private int visibleTechnologyNodeCount;
    private int visibleConnectionCount;
    private int visibleTreeColumnCount;
    private float nextLiveResearchRefreshRealtime;
    private string lastLiveSelectedTechnologyId = "";
    private int lastLiveSelectedTechnologyLevel = -1;
    private readonly Dictionary<string, RectTransform> technologyProgressFills = new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);
    private static Font cachedDefaultFont;
    private static Sprite cachedRoundedSprite;

    private static readonly Color BackgroundColor = new Color(0.01f, 0.02f, 0.035f, 0.46f);
    private static readonly Color TopBarColor = new Color(0.94f, 0.97f, 1.00f, 0.82f);
    private static readonly Color PanelColor = new Color(0.95f, 0.98f, 1.00f, 0.88f);
    private static readonly Color PanelDarkColor = new Color(0.82f, 0.93f, 0.99f, 0.72f);
    private static readonly Color ButtonColor = new Color(0.92f, 0.96f, 1.00f, 0.96f);
    private static readonly Color ButtonHoverColor = new Color(0.98f, 1.00f, 1.00f, 1f);
    private static readonly Color ButtonPressedColor = new Color(0.62f, 0.80f, 0.92f, 1f);
    private static readonly Color SelectedColor = new Color(0.06f, 0.58f, 0.70f, 0.96f);
    private static readonly Color AvailableColor = new Color(0.98f, 0.96f, 0.90f, 0.98f);
    private static readonly Color LockedColor = new Color(0.70f, 0.74f, 0.78f, 0.78f);
    private static readonly Color CompletedColor = new Color(0.55f, 0.78f, 0.70f, 0.96f);
    private static readonly Color ActiveColor = new Color(1.00f, 0.88f, 0.48f, 0.98f);
    private static readonly Color ConnectionColor = new Color(0.36f, 0.58f, 0.74f, 0.78f);
    private static readonly Color LockedConnectionColor = new Color(0.50f, 0.56f, 0.62f, 0.48f);
    private static readonly Color TextColor = new Color(0.18f, 0.23f, 0.31f, 1f);
    private static readonly Color MutedTextColor = new Color(0.39f, 0.46f, 0.56f, 1f);
    private static readonly Color AccentColor = new Color(0.06f, 0.58f, 0.68f, 1f);
    private static readonly Color CyanTextColor = new Color(0.03f, 0.34f, 0.50f, 1f);
    private static readonly Color BadTextColor = new Color(0.72f, 0.23f, 0.22f, 1f);
    private static readonly Color BorderColor = new Color(0.57f, 0.66f, 0.76f, 0.72f);
    private static readonly Color GlowColor = new Color(0.38f, 0.95f, 1.00f, 0.72f);

    public string ContentForTests => BuildContent();
    public string Report => BuildReport();
    public string SelectedCategoryIdForTests => EnsureSelectedCategory();
    public string SelectedTechnologyIdForTests => EnsureSelectedTechnology();
    public string SelectedTechnologyDetailsForTests => BuildSelectedTechnologyDetails();
    public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
    public int SortingOrderForTests => canvas != null ? canvas.sortingOrder : -1;
    public bool IsReadyForTests => canvas != null
        && root != null
        && runtimeMeta != null
        && rubricTabRoot != null
        && treeViewport != null
        && treeContent != null
        && inspectorPanel != null
        && contentText != null
        && statusText != null;
    public bool HasVisibleTextForTests => HasRenderableText(titleText)
        && HasRenderableText(subtitleText)
        && HasRenderableText(contentText)
        && HasRenderableText(actionTitleText)
        && HasRenderableText(selectedTitleText)
        && HasRenderableText(primaryActionText)
        && HasRenderableText(statusText);
    public bool HasRubricTabsForTests => rubricTabRoot != null && visibleRubricTabCount >= 3;
    public bool HasTreeViewForTests => treeScrollRect != null
        && treeViewport != null
        && treeContent != null
        && visibleTechnologyNodeCount >= 1
        && visibleTreeColumnCount >= DesignedTreeColumnCount;
    public bool HasInspectorPanelForTests => inspectorPanel != null
        && HasRenderableText(selectedTitleText)
        && HasRenderableText(selectedEffectText)
        && HasRenderableText(selectedCostText);
    public int VisibleRubricTabCountForTests => visibleRubricTabCount;
    public int VisibleTechnologyNodeCountForTests => visibleTechnologyNodeCount;
    public int VisibleTreeConnectionCountForTests => visibleConnectionCount;
    public int VisibleTreeColumnCountForTests => visibleTreeColumnCount;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            RebuildScreen();
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying && transform.Find(CanvasName) == null)
        {
            RebuildScreen();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || canvas == null || !canvas.gameObject.activeInHierarchy)
        {
            return;
        }

        if (Time.unscaledTime < nextLiveResearchRefreshRealtime)
        {
            return;
        }

        nextLiveResearchRefreshRealtime = Time.unscaledTime + LiveResearchRefreshIntervalSeconds;
        RefreshLiveResearchProgress();
    }

    public void BindRuntime(MetaGameState meta)
    {
        runtimeMeta = meta;
        if (runtimeMeta != null)
        {
            runtimeMeta.EnsureProgressInitialized();
            EnsureSelectedCategory();
            EnsureSelectedTechnology();
        }

        RefreshScreen();
    }

    public void SetCloseAction(Func<bool> action)
    {
        closeAction = action;
    }

    public void SetVisible(bool visible)
    {
        if (canvas == null)
        {
            RebuildScreen();
        }

        if (canvas != null)
        {
            canvas.gameObject.SetActive(visible);
        }
    }

    public void RebuildScreen()
    {
        ClearGeneratedChildren();
        EnsureEventSystem(transform);
        BuildCanvas();
        BuildBackground();
        BuildTopBar();
        BuildLayout();
        RefreshScreen();
        Canvas.ForceUpdateCanvases();
    }

    public bool RunPrimaryActionForTests()
    {
        return RunPrimaryAction();
    }

    public bool RunSecondaryActionForTests()
    {
        return RunSecondaryAction();
    }

    public bool SelectFirstVisibleTechnologyForTests()
    {
        TechnologyConfig technology = GetFirstTechnologyInCategory(EnsureSelectedCategory());
        if (technology == null)
        {
            return false;
        }

        SelectTechnology(technology.id);
        return selectedTechnologyId == technology.id;
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateRect("Knowledge Root", canvas.transform, StretchFull()).GetComponent<RectTransform>();
    }

    private void BuildBackground()
    {
        Image background = CreateImage("Background", root, StretchFull(), BackgroundColor);
        background.raycastTarget = true;
        Button fadeButton = background.gameObject.AddComponent<Button>();
        fadeButton.transition = Selectable.Transition.None;
        fadeButton.onClick.AddListener(() => CloseScreen());

        CreateImage("Top Sky Wash", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 180f)
        }, new Color(0f, 0f, 0f, 0f)).raycastTarget = false;

        CreateImage("Bottom Soft Haze", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 180f)
        }, new Color(0f, 0f, 0f, 0f)).raycastTarget = false;
    }

    private void BuildTopBar()
    {
        RectTransform topBar = CreateImage("Top Bar", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 108f)
        }, new Color(1f, 1f, 1f, 0f)).GetComponent<RectTransform>();

        titleText = CreateText(topBar, "АРХИВЫ", 34, new Vector2(44f, -12f), new Vector2(380f, 44f), TextAnchor.MiddleLeft, TextColor);
        titleText.fontStyle = FontStyle.Bold;
        subtitleText = CreateText(topBar, "Знания, SP архивов, книги и уровни 1-5", 17, new Vector2(46f, -56f), new Vector2(720f, 26f), TextAnchor.MiddleLeft, MutedTextColor);
        resourceText = CreateText(topBar, "", 16, new Vector2(-44f, -16f), new Vector2(900f, 56f), TextAnchor.MiddleRight, AccentColor, new Vector2(1f, 1f));
        titleText.color = new Color(1f, 1f, 1f, 0f);
        subtitleText.color = new Color(1f, 1f, 1f, 0f);
        resourceText.color = new Color(1f, 1f, 1f, 0f);

        RectTransform playerCapsule = CreateCapsule(topBar, "Player Capsule", new Vector2(28f, -18f), new Vector2(390f, 76f), new Vector2(0f, 1f), TopBarColor);
        AddSoftShadow(playerCapsule.gameObject, 0.12f, new Vector2(0f, -3f));
        CreateIconMedallion(playerCapsule, "АР", new Vector2(18f, -10f), 56f, new Color(0.72f, 0.84f, 0.94f, 1f));
        CreateText(playerCapsule, "Капитан Ветров", 20, new Vector2(90f, -11f), new Vector2(220f, 28f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        CreateProgressLine(playerCapsule, new Vector2(92f, -52f), new Vector2(190f, 10f), 0.62f);
        CreateText(playerCapsule, "27", 20, new Vector2(306f, -22f), new Vector2(54f, 38f), TextAnchor.MiddleCenter, AccentColor).fontStyle = FontStyle.Bold;

        CreateResourcePill(topBar, "FR", "1.24M", new Vector2(560f, -22f));
        CreateResourcePill(topBar, "XP", "853K", new Vector2(772f, -22f));
        CreateResourcePill(topBar, "MT", "412K", new Vector2(984f, -22f));
        CreateResourcePill(topBar, "SP", "18.7K", new Vector2(1196f, -22f));
        CreateResourcePill(topBar, "SO", "2 450", new Vector2(1408f, -22f));

        CreateTopIconButton(topBar, "КН", new Vector2(-300f, -20f));
        CreateTopIconButton(topBar, "★", new Vector2(-218f, -20f));
        CreateTopIconButton(topBar, "✉", new Vector2(-136f, -20f));
        CreateTopIconButton(topBar, "⚙", new Vector2(-54f, -20f));
        SetRaycastTargetRecursive(topBar, false);
    }

    private void BuildLayout()
    {
        RectTransform mainWindow = CreatePanel("Knowledge Main Window", new Vector2(1776f, 760f), new Vector2(72f, -104f), new Vector2(0f, 1f));
        AddSoftShadow(mainWindow.gameObject, 0.20f, new Vector2(0f, -7f));
        AddBorder(mainWindow, BorderColor, new Vector2(1772f, 756f));

        CreateIconMedallion(mainWindow, "АР", new Vector2(26f, -24f), 74f, new Color(0.80f, 0.90f, 0.98f, 1f));
        titleText = CreateText(mainWindow, "Архивы и знания", 36, new Vector2(120f, -24f), new Vector2(520f, 48f), TextAnchor.MiddleLeft, TextColor);
        titleText.fontStyle = FontStyle.Bold;
        subtitleText = CreateText(mainWindow, "Постоянные исследования базы", 19, new Vector2(122f, -72f), new Vector2(620f, 28f), TextAnchor.MiddleLeft, MutedTextColor);
        CreateBlueprintLines(mainWindow, new Vector2(590f, -22f), new Vector2(390f, 74f));
        RectTransform archiveSummaryPill = CreateCapsule(mainWindow, "Archive Summary", new Vector2(650f, -56f), new Vector2(560f, 44f), new Vector2(0f, 1f), new Color(0.88f, 0.95f, 1f, 0.78f));
        AddBorder(archiveSummaryPill, new Color(0.52f, 0.72f, 0.86f, 0.72f), new Vector2(556f, 40f));
        resourceText = CreateText(archiveSummaryPill, "", 18, new Vector2(14f, -5f), new Vector2(532f, 32f), TextAnchor.MiddleCenter, AccentColor);
        resourceText.fontStyle = FontStyle.Bold;

        RectTransform knowledgePill = CreateCapsule(mainWindow, "Knowledge Points", new Vector2(1120f, -20f), new Vector2(250f, 74f), new Vector2(0f, 1f), new Color(0.97f, 0.99f, 1f, 0.58f));
        CreateIconMedallion(knowledgePill, "SP", new Vector2(12f, -10f), 52f, new Color(0.94f, 0.96f, 0.98f, 1f));
        CreateText(knowledgePill, "Очки знаний", 16, new Vector2(78f, -8f), new Vector2(130f, 24f), TextAnchor.MiddleLeft, MutedTextColor);
        CreateText(knowledgePill, "4 230", 34, new Vector2(78f, -34f), new Vector2(130f, 36f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        knowledgePill.gameObject.SetActive(false);

        RectTransform archivistPill = CreateCapsule(mainWindow, "Archivist Level", new Vector2(1390f, -20f), new Vector2(250f, 74f), new Vector2(0f, 1f), new Color(0.97f, 0.99f, 1f, 0.58f));
        CreateIconMedallion(archivistPill, "12", new Vector2(12f, -10f), 52f, new Color(0.94f, 0.96f, 0.98f, 1f));
        CreateText(archivistPill, "Архивариус", 17, new Vector2(78f, -11f), new Vector2(132f, 24f), TextAnchor.MiddleLeft, MutedTextColor);
        CreateText(archivistPill, "ур. 12", 24, new Vector2(78f, -38f), new Vector2(132f, 30f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        CreateTopIconButton(mainWindow, "?", new Vector2(-76f, -20f));

        RectTransform rubricsPanel = CreatePanel("Knowledge Rubrics", new Vector2(270f, 560f), new Vector2(96f, -178f), new Vector2(0f, 1f));
        Text rubricsTitle = CreateText(rubricsPanel, "РУБРИКИ", 21, new Vector2(24f, -22f), new Vector2(230f, 32f), TextAnchor.MiddleLeft, TextColor);
        rubricsTitle.fontStyle = FontStyle.Bold;
        SetText(rubricsTitle, "Разделы архива");
        rubricViewport = CreateImage("Rubric Viewport", rubricsPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(16f, -70f),
            sizeDelta = new Vector2(238f, 466f)
        }, new Color(1f, 1f, 1f, 0f)).GetComponent<RectTransform>();
        Mask rubricMask = rubricViewport.gameObject.AddComponent<Mask>();
        rubricMask.showMaskGraphic = false;
        rubricTabRoot = CreateRect("Rubric Tabs", rubricViewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(238f, 466f)
        }).GetComponent<RectTransform>();
        rubricScrollRect = rubricsPanel.gameObject.AddComponent<ScrollRect>();
        rubricScrollRect.content = rubricTabRoot;
        rubricScrollRect.viewport = rubricViewport;
        rubricScrollRect.horizontal = false;
        rubricScrollRect.vertical = true;
        rubricScrollRect.movementType = ScrollRect.MovementType.Clamped;
        rubricScrollRect.scrollSensitivity = 32f;

        RectTransform logButton = CreateCapsule(rubricsPanel, "Discovery Log", new Vector2(16f, -602f), new Vector2(238f, 56f), new Vector2(0f, 1f), new Color(0.89f, 0.94f, 0.99f, 0.96f));
        CreateText(logButton, "Журнал открытий", 18, new Vector2(60f, -12f), new Vector2(162f, 28f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        CreateIconMedallion(logButton, "Ж", new Vector2(12f, -8f), 40f, new Color(0.78f, 0.89f, 0.98f, 1f));
        logButton.gameObject.SetActive(false);

        RectTransform treePanel = CreatePanel("Knowledge Tree", new Vector2(1014f, 560f), new Vector2(384f, -178f), new Vector2(0f, 1f));
        Text treeTitle = CreateText(treePanel, "ДЕРЕВО ЗНАНИЙ", 22, new Vector2(24f, -18f), new Vector2(360f, 34f), TextAnchor.MiddleLeft, TextColor);
        treeTitle.fontStyle = FontStyle.Bold;
        treeTitle.fontSize = 27;
        treeTitle.rectTransform.anchoredPosition = new Vector2(38f, -18f);
        treeTitle.rectTransform.sizeDelta = new Vector2(420f, 40f);
        SetText(treeTitle, "Разведка и сканирование");
        contentText = CreateText(treePanel, "", 14, new Vector2(476f, -22f), new Vector2(480f, 34f), TextAnchor.MiddleRight, MutedTextColor);
        contentText.verticalOverflow = VerticalWrapMode.Overflow;

        treeViewport = CreateImage("Knowledge Tree Viewport", treePanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(22f, -72f),
            sizeDelta = new Vector2(970f, 430f)
        }, PanelDarkColor).GetComponent<RectTransform>();
        Mask viewportMask = treeViewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        treeContent = CreateRect("Knowledge Tree Content", treeViewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(DesignedTreeColumnCount * 190f + 180f, 520f)
        }).GetComponent<RectTransform>();

        treeScrollRect = treePanel.gameObject.AddComponent<ScrollRect>();
        treeScrollRect.content = treeContent;
        treeScrollRect.viewport = treeViewport;
        treeScrollRect.horizontal = true;
        treeScrollRect.vertical = true;
        treeScrollRect.movementType = ScrollRect.MovementType.Clamped;
        treeScrollRect.scrollSensitivity = 36f;

        inspectorPanel = CreatePanel("Knowledge Inspector", new Vector2(402f, 560f), new Vector2(-96f, -178f), new Vector2(1f, 1f));
        actionTitleText = CreateText(inspectorPanel, "ЗНАНИЕ", 22, new Vector2(28f, -24f), new Vector2(420f, 34f), TextAnchor.MiddleLeft, TextColor);
        actionTitleText.fontStyle = FontStyle.Bold;
        SetText(actionTitleText, "Индексация архивов");
        actionTitleText.alignment = TextAnchor.MiddleCenter;
        actionTitleText.rectTransform.sizeDelta = new Vector2(340f, 36f);
        CreateIconMedallion(inspectorPanel, "КН", new Vector2(140f, -60f), 112f, new Color(0.86f, 0.94f, 1f, 1f));
        selectedTitleText = CreateText(inspectorPanel, "", 21, new Vector2(28f, -182f), new Vector2(340f, 34f), TextAnchor.MiddleCenter, CyanTextColor);
        selectedTitleText.fontStyle = FontStyle.Bold;
        selectedTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedStatusText = CreateText(inspectorPanel, "", 14, new Vector2(28f, -220f), new Vector2(340f, 58f), TextAnchor.UpperLeft, MutedTextColor);
        selectedStatusText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedEffectText = CreateText(inspectorPanel, "", 15, new Vector2(28f, -290f), new Vector2(340f, 78f), TextAnchor.UpperLeft, TextColor);
        selectedEffectText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedCostText = CreateText(inspectorPanel, "", 15, new Vector2(28f, -380f), new Vector2(340f, 58f), TextAnchor.UpperLeft, TextColor);
        selectedCostText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedRequirementText = CreateText(inspectorPanel, "", 14, new Vector2(28f, -446f), new Vector2(340f, 38f), TextAnchor.UpperLeft, MutedTextColor);
        selectedRequirementText.verticalOverflow = VerticalWrapMode.Overflow;
        primaryActionText = CreateButton(inspectorPanel, "Направить Архивы", new Vector2(28f, -498f), new Vector2(340f, 48f), RunPrimaryAction);
        secondaryActionText = CreateButton(inspectorPanel, "Следующая рубрика", new Vector2(28f, -708f), new Vector2(204f, 48f), RunSecondaryAction);
        CreateButton(inspectorPanel, "Закрыть", new Vector2(244f, -708f), new Vector2(204f, 48f), CloseScreen);
        SetText(primaryActionText, "Исследовать");
        RectTransform primaryButtonRect = primaryActionText.rectTransform.parent.GetComponent<RectTransform>();
        primaryButtonRect.anchoredPosition = new Vector2(28f, -498f);
        primaryButtonRect.sizeDelta = new Vector2(340f, 48f);
        SetButtonPalette(primaryButtonRect, new Color(0.30f, 0.66f, 0.30f, 1f), new Color(0.42f, 0.78f, 0.40f, 1f), new Color(0.20f, 0.48f, 0.22f, 1f));
        primaryActionText.color = Color.white;
        primaryActionText.fontStyle = FontStyle.Bold;
        SetText(secondaryActionText, "Следующая рубрика");
        RectTransform secondaryButtonRect = secondaryActionText.rectTransform.parent.GetComponent<RectTransform>();
        secondaryButtonRect.anchoredPosition = new Vector2(28f, -660f);
        secondaryButtonRect.sizeDelta = new Vector2(164f, 36f);
        secondaryButtonRect.gameObject.SetActive(false);
        if (inspectorPanel.childCount > 0)
        {
            inspectorPanel.GetChild(inspectorPanel.childCount - 1).gameObject.SetActive(false);
        }
        CreateButton(inspectorPanel, "Закрыть", new Vector2(204f, -660f), new Vector2(164f, 36f), CloseScreen);
        inspectorPanel.GetChild(inspectorPanel.childCount - 1).gameObject.SetActive(false);
        statusText = CreateText(inspectorPanel, "", 13, new Vector2(28f, -548f), new Vector2(340f, 20f), TextAnchor.UpperLeft, AccentColor);
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private bool RunPrimaryAction()
    {
        if (runtimeMeta == null)
        {
            lastMessage = "MetaGameState недоступен.";
            RefreshDynamicState();
            return false;
        }

        TechnologyConfig selected = GetSelectedTechnology();
        if (selected != null && !runtimeMeta.IsTechnologyCompleted(selected.id))
        {
            return TryStartResearch(selected);
        }

        List<TechnologyConfig> technologies = GetTechnologiesInSelectedCategory();
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || runtimeMeta.IsTechnologyCompleted(technology.id)) continue;

            selectedTechnologyId = technology.id;
            return TryStartResearch(technology);
        }

        lastMessage = "В этой рубрике все знания завершены или недоступны.";
        RefreshDynamicState();
        return true;
    }

    private bool TryStartResearch(TechnologyConfig technology)
    {
        if (technology == null || runtimeMeta == null)
        {
            lastMessage = "Знание не выбрано.";
            RefreshDynamicState();
            return false;
        }

        int archiveCount = GetArchiveCountForUi();
        int slotIndex = runtimeMeta.GetActiveResearchSlotIndex(technology.id, archiveCount);
        if (slotIndex >= 0)
        {
            lastMessage = "Это знание уже исследует Архив " + (slotIndex + 1).ToString() + ".";
            RefreshDynamicState();
            return true;
        }

        if (slotIndex < 0)
        {
            slotIndex = runtimeMeta.GetFirstAvailableResearchSlotIndex(archiveCount);
        }

        if (!runtimeMeta.CanSelectResearchTechnologyInSlot(technology, slotIndex, archiveCount, out string reason))
        {
            lastMessage = reason;
            RefreshDynamicState();
            return true;
        }

        if (runtimeMeta.TrySelectResearchTechnologyInSlot(technology.id, slotIndex, archiveCount))
        {
            lastMessage = "Архив " + (slotIndex + 1).ToString() + " направлен в знание: " + runtimeMeta.GetTechnologyDisplayName(technology) + ".";
            nextLiveResearchRefreshRealtime = 0f;
            RefreshLiveResearchProgress();
            return true;
        }

        lastMessage = string.IsNullOrWhiteSpace(runtimeMeta.LastAccountMessage)
            ? "Не удалось направить Архив в знание: " + runtimeMeta.GetTechnologyDisplayName(technology) + "."
            : runtimeMeta.LastAccountMessage;
        RefreshDynamicState();
        return true;
    }

    private bool RunSecondaryAction()
    {
        if (runtimeMeta == null)
        {
            lastMessage = "MetaGameState недоступен.";
            RefreshDynamicState();
            return false;
        }

        string nextCategory = runtimeMeta.GetNextTechnologyCategoryId(selectedCategoryId);
        if (string.IsNullOrWhiteSpace(nextCategory))
        {
            lastMessage = "Рубрики знаний не загружены.";
            RefreshDynamicState();
            return false;
        }

        selectedCategoryId = nextCategory;
        selectedTechnologyId = "";
        EnsureSelectedTechnology();
        lastMessage = "Рубрика знаний: " + GetCategoryDisplayName(selectedCategoryId) + ".";
        RefreshScreen();
        return true;
    }

    private bool RunPackageAction()
    {
        if (runtimeMeta == null)
        {
            lastMessage = "MetaGameState недоступен.";
            RefreshDynamicState();
            return false;
        }

        TechnologyConfig selected = GetSelectedTechnology();
        if (selected == null)
        {
            lastMessage = "Сначала выбери знание.";
            RefreshDynamicState();
            return false;
        }

        if (runtimeMeta.TryApplyBestKnowledgeSpPackage(selected.id, out int appliedSp, out int burnedSp, out string reason))
        {
            lastMessage = "SP-пакет вложен: +" + appliedSp.ToString() + " SP"
                + (burnedSp > 0 ? ", перелив сгорел " + burnedSp.ToString() + " SP." : ".");
            RefreshDynamicState();
            return true;
        }

        lastMessage = string.IsNullOrWhiteSpace(reason) ? "Нет подходящего SP-пакета." : reason;
        RefreshDynamicState();
        return true;
    }

    private bool CloseScreen()
    {
        if (closeAction != null)
        {
            return closeAction();
        }

        SetVisible(false);
        return true;
    }

    private void RefreshScreen()
    {
        EnsureSelectedCategory();
        EnsureSelectedTechnology();
        SetText(resourceText, BuildArchiveHeaderResourceLine());
        SetText(contentText, BuildTreeHeaderText());
        SetText(primaryActionText, "Исследовать");
        SetText(secondaryActionText, "Следующая рубрика");
        SetText(statusText, BuildStatusText());
        RebuildRubricTabs();
        RebuildTechnologyTree();
        RefreshInspector();
        Canvas.ForceUpdateCanvases();
    }

    private void RefreshLiveResearchProgress()
    {
        if (runtimeMeta == null)
        {
            return;
        }

        runtimeMeta.AdvanceRealTimeProcesses(DateTime.UtcNow);

        TechnologyConfig selected = GetSelectedTechnology();
        int selectedLevel = selected != null ? runtimeMeta.GetTechnologyCompletedLevel(selected) : -1;
        string selectedId = selected != null ? selected.id : "";
        bool selectedLevelChanged = !string.Equals(selectedId, lastLiveSelectedTechnologyId, StringComparison.OrdinalIgnoreCase)
            || selectedLevel != lastLiveSelectedTechnologyLevel;

        lastLiveSelectedTechnologyId = selectedId;
        lastLiveSelectedTechnologyLevel = selectedLevel;

        if (selectedLevelChanged && selected != null && selectedLevel > 0)
        {
            RefreshScreen();
            return;
        }

        SetText(resourceText, BuildArchiveHeaderResourceLine());
        RefreshInspector();
        UpdateVisibleTechnologyProgressBars();
        SetText(statusText, BuildStatusText());
    }

    private void RefreshDynamicState()
    {
        EnsureSelectedCategory();
        EnsureSelectedTechnology();
        SetText(resourceText, BuildArchiveHeaderResourceLine());
        RefreshInspector();
        UpdateVisibleTechnologyProgressBars();
        SetText(statusText, BuildStatusText());
        Canvas.ForceUpdateCanvases();
    }

    private void RebuildRubricTabs()
    {
        visibleRubricTabCount = 0;
        if (rubricTabRoot == null || runtimeMeta == null)
        {
            return;
        }

        ClearChildren(rubricTabRoot);
        IReadOnlyList<string> categories = runtimeMeta.GetTechnologyCategoryIdsForUi();
        if (categories == null || categories.Count == 0)
        {
            rubricTabRoot.sizeDelta = new Vector2(238f, rubricViewport != null ? rubricViewport.rect.height : 466f);
            CreateText(rubricTabRoot, "Нет рубрик", 16, Vector2.zero, new Vector2(220f, 28f), TextAnchor.MiddleLeft, MutedTextColor);
            return;
        }

        rubricTabRoot.anchoredPosition = Vector2.zero;

        for (int i = 0; i < categories.Count; i++)
        {
            string categoryId = categories[i];
            if (string.IsNullOrWhiteSpace(categoryId)) continue;

            int count = CountTechnologiesInCategory(categoryId);
            bool selected = string.Equals(categoryId, selectedCategoryId, StringComparison.OrdinalIgnoreCase);
            RectTransform tab = CreateImage("Rubric " + categoryId, rubricTabRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(0f, -visibleRubricTabCount * 56f),
                sizeDelta = new Vector2(238f, 50f)
            }, selected ? SelectedColor : ButtonColor).GetComponent<RectTransform>();
            AddSoftShadow(tab.gameObject, selected ? 0.16f : 0.08f, new Vector2(0f, -2f));
            AddBorder(tab, selected ? GlowColor : BorderColor, new Vector2(234f, 46f));

            string capturedCategoryId = categoryId;
            Button button = tab.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? SelectedColor : ButtonColor;
            colors.highlightedColor = selected ? SelectedColor : ButtonHoverColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = SelectedColor;
            button.colors = colors;
            button.onClick.AddListener(() => SelectCategory(capturedCategoryId));

            CreateIconMedallion(tab, GetCategoryIconText(categoryId), new Vector2(10f, -7f), 40f, selected ? new Color(0.12f, 0.70f, 0.82f, 1f) : new Color(0.78f, 0.88f, 0.96f, 1f));
            Text label = CreateText(tab, GetCategoryDisplayName(categoryId), 15, new Vector2(58f, -4f), new Vector2(132f, 26f), TextAnchor.MiddleLeft, selected ? Color.white : TextColor);
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            CreateText(tab, count.ToString(), 14, new Vector2(-14f, -8f), new Vector2(38f, 24f), TextAnchor.MiddleRight, selected ? Color.white : MutedTextColor, new Vector2(1f, 1f));
            CreateProgressLine(tab, new Vector2(58f, -38f), new Vector2(132f, 6f), GetCategoryCompletion01(categoryId));
            visibleRubricTabCount++;
        }

        float viewportHeight = rubricViewport != null ? rubricViewport.rect.height : 466f;
        rubricTabRoot.sizeDelta = new Vector2(238f, Mathf.Max(viewportHeight, visibleRubricTabCount * 56f + 4f));
    }

    private void RebuildTechnologyTree()
    {
        visibleTechnologyNodeCount = 0;
        visibleConnectionCount = 0;
        visibleTreeColumnCount = DesignedTreeColumnCount;
        technologyProgressFills.Clear();
        if (treeContent == null || runtimeMeta == null)
        {
            return;
        }

        ClearChildren(treeContent);
        List<TechnologyConfig> technologies = GetTechnologiesInSelectedCategory();
        if (technologies.Count == 0)
        {
            CreateText(treeContent, "В этой рубрике пока нет знаний.", 18, new Vector2(40f, -80f), new Vector2(520f, 40f), TextAnchor.MiddleLeft, MutedTextColor);
            return;
        }

        int maxColumn = 0;
        int maxRow = 0;
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null) continue;
            maxColumn = Mathf.Max(maxColumn, technology.treeColumn);
            maxRow = Mathf.Max(maxRow, technology.treeRow);
        }

        visibleTreeColumnCount = Mathf.Max(DesignedTreeColumnCount, maxColumn + 1);
        int rowCount = Mathf.Max(3, maxRow + 1);
        float contentWidth = visibleTreeColumnCount * 190f + 160f;
        float contentHeight = Mathf.Max(520f, rowCount * 150f + 190f);
        treeContent.sizeDelta = new Vector2(contentWidth, contentHeight);

        for (int column = 0; column < visibleTreeColumnCount; column++)
        {
            string label = ToRoman(column + 1) + " УР.";
            CreateText(treeContent, label, 14, new Vector2(50f + column * 190f, -22f), new Vector2(140f, 24f), TextAnchor.MiddleCenter, MutedTextColor);
            CreateImage("Column Guide " + column, treeContent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = new Vector2(120f + column * 190f, -56f),
                sizeDelta = new Vector2(1f, contentHeight - 92f)
            }, new Color(0.42f, 0.58f, 0.70f, 0.24f)).raycastTarget = false;
        }

        Dictionary<string, Vector2> centersById = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) continue;
            centersById[technology.id] = GetNodeCenter(technology);
        }

        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || technology.prerequisiteTechnologyIds == null) continue;
            for (int prereqIndex = 0; prereqIndex < technology.prerequisiteTechnologyIds.Count; prereqIndex++)
            {
                string prerequisiteId = technology.prerequisiteTechnologyIds[prereqIndex];
                if (!centersById.TryGetValue(prerequisiteId, out Vector2 from) ||
                    !centersById.TryGetValue(technology.id, out Vector2 to))
                {
                    continue;
                }

                bool unlocked = runtimeMeta.IsTechnologyCompleted(prerequisiteId) || runtimeMeta.IsKnowledgeAvailableForResearch(technology);
                DrawConnection(from, to, unlocked ? ConnectionColor : LockedConnectionColor);
                visibleConnectionCount++;
            }
        }

        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null) continue;
            CreateTechnologyNode(technology);
            visibleTechnologyNodeCount++;
        }
    }

    private void RefreshInspector()
    {
        TechnologyConfig selected = GetSelectedTechnology();
        if (selected == null)
        {
            SetText(selectedTitleText, "Знание не выбрано");
            SetText(selectedStatusText, "Выбери узел в дереве.");
            SetText(selectedEffectText, "Эффект: -");
            SetText(selectedCostText, "Стоимость: -");
            SetText(selectedRequirementText, "Требования: -");
            return;
        }

        SetText(selectedTitleText, runtimeMeta.GetTechnologyDisplayName(selected));
        SetText(selectedStatusText, BuildSelectedStatus(selected));
        SetText(selectedEffectText, BuildSelectedEffectText(selected));
        SetText(selectedCostText, BuildSelectedCostText(selected));
        SetText(selectedCostText, BuildArchiveSelectedCostText(selected));
        SetText(selectedRequirementText, BuildSelectedRequirementText(selected));
    }

    private void SelectCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return;
        }

        selectedCategoryId = categoryId;
        selectedTechnologyId = "";
        EnsureSelectedTechnology();
        lastMessage = "Рубрика знаний: " + GetCategoryDisplayName(categoryId) + ".";
        RefreshScreen();
    }

    private void SelectTechnology(string technologyId)
    {
        selectedTechnologyId = technologyId ?? "";
        TechnologyConfig technology = GetSelectedTechnology();
        lastMessage = technology != null
            ? "Выбрано знание: " + runtimeMeta.GetTechnologyDisplayName(technology) + "."
            : "Знание не найдено.";
        RefreshDynamicState();
    }

    private void CreateTechnologyNode(TechnologyConfig technology)
    {
        Vector2 center = GetNodeCenter(technology);
        bool selected = string.Equals(technology.id, selectedTechnologyId, StringComparison.OrdinalIgnoreCase);
        bool completed = runtimeMeta.IsTechnologyCompleted(technology.id);
        bool active = runtimeMeta.IsTechnologyActivelyResearched(technology.id);
        bool available = runtimeMeta.IsKnowledgeAvailableForResearch(technology) && ArePrerequisitesCompletedForUi(technology);
        Color nodeColor = selected
            ? SelectedColor
            : completed
            ? CompletedColor
            : active
            ? ActiveColor
            : available
            ? AvailableColor
            : LockedColor;

        RectTransform node = CreateImage("Knowledge Node " + technology.id, treeContent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = center,
            sizeDelta = new Vector2(166f, 122f)
        }, nodeColor).GetComponent<RectTransform>();
        AddSoftShadow(node.gameObject, selected ? 0.20f : 0.10f, new Vector2(0f, -3f));
        AddBorder(node, selected ? GlowColor : BorderColor, selected ? new Vector2(174f, 130f) : new Vector2(162f, 118f));

        Button button = node.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = nodeColor;
        colors.highlightedColor = selected ? SelectedColor : ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = SelectedColor;
        button.colors = colors;
        string capturedId = technology.id;
        button.onClick.AddListener(() => SelectTechnology(capturedId));

        CreateIconMedallion(node, string.IsNullOrWhiteSpace(technology.iconText) ? "KN" : technology.iconText, new Vector2(10f, -10f), 42f, selected ? new Color(0.12f, 0.72f, 0.84f, 1f) : new Color(0.82f, 0.90f, 0.96f, 1f));
        Text icon = CreateText(node, "", 1, Vector2.zero, new Vector2(1f, 1f), TextAnchor.MiddleLeft, AccentColor);
        icon.fontStyle = FontStyle.Bold;
        Text name = CreateText(node, runtimeMeta.GetTechnologyDisplayName(technology), 14, new Vector2(58f, -12f), new Vector2(92f, 48f), TextAnchor.UpperLeft, selected ? Color.white : TextColor);
        name.verticalOverflow = VerticalWrapMode.Truncate;
        int level = runtimeMeta.GetTechnologyCompletedLevel(technology);
        int maxLevel = GetTechnologyMaxLevel(technology);
        Text levelText = CreateText(node, level + "/" + maxLevel, 14, new Vector2(-12f, -72f), new Vector2(52f, 22f), TextAnchor.MiddleRight, completed ? new Color(0.16f, 0.55f, 0.36f, 1f) : MutedTextColor, new Vector2(1f, 1f));
        levelText.fontStyle = FontStyle.Bold;
        technologyProgressFills[technology.id] = CreateProgressLine(node, new Vector2(14f, -92f), new Vector2(136f, 7f), GetTechnologyProgress01(technology));

        if (!available && !completed)
        {
            CreateText(node, "замок", 12, new Vector2(12f, -70f), new Vector2(140f, 18f), TextAnchor.MiddleLeft, new Color(0.72f, 0.72f, 0.72f, 1f));
        }
        else if (active)
        {
            CreateText(node, "в работе", 12, new Vector2(12f, -70f), new Vector2(140f, 18f), TextAnchor.MiddleLeft, AccentColor);
        }
        else if (completed)
        {
            CreateText(node, "изучено", 12, new Vector2(12f, -70f), new Vector2(140f, 18f), TextAnchor.MiddleLeft, new Color(0.58f, 1f, 0.78f, 1f));
        }
    }

    private void DrawConnection(Vector2 from, Vector2 to, Color color)
    {
        float startX = from.x + 83f;
        float endX = to.x - 83f;
        float midX = Mathf.Lerp(startX, endX, 0.5f);
        DrawLine(new Vector2(startX, from.y), new Vector2(midX, from.y), color);
        DrawLine(new Vector2(midX, from.y), new Vector2(midX, to.y), color);
        DrawLine(new Vector2(midX, to.y), new Vector2(endX, to.y), color);
    }

    private void DrawLine(Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length <= 0.5f)
        {
            return;
        }

        RectTransform line = CreateImage("Knowledge Connection", treeContent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = (from + to) * 0.5f,
            sizeDelta = new Vector2(length, 4f)
        }, color).GetComponent<RectTransform>();
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        line.SetAsFirstSibling();
    }

    private RectTransform CreateProgressLine(RectTransform parent, Vector2 anchoredPosition, Vector2 size, float progress01)
    {
        RectTransform background = CreateImage("Progress Background", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        }, new Color(0.0f, 0.0f, 0.0f, 0.42f)).GetComponent<RectTransform>();

        RectTransform fill = CreateImage("Progress Fill", background, StretchFull(), new Color(0.18f, 0.85f, 0.88f, 0.92f)).GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        return fill;
    }

    private string BuildContent()
    {
        if (runtimeMeta == null)
        {
            return "Архивы недоступны: MetaGameState не найден.";
        }

        EnsureSelectedCategory();
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("ЗНАНИЯ");
        builder.AppendLine("Рубрики слева, дерево знаний в центре, выбранное знание справа.");
        builder.AppendLine("Архивы льют SP в выбранное знание. Книги/права открывают доступ, ресурсы оплачивают уровень, SP заполняет прогресс.");
        builder.AppendLine();
        builder.Append(runtimeMeta.GetTechnologyBoardOverviewText(selectedCategoryId, 12));
        builder.AppendLine();
        builder.AppendLine("Выбрано: " + BuildSelectedTechnologyDetails());
        return builder.ToString();
    }

    private string BuildReport()
    {
        return "Knowledge screen source="
            + (runtimeMeta != null ? "runtime" : "missing")
            + " category="
            + (string.IsNullOrWhiteSpace(selectedCategoryId) ? "-" : selectedCategoryId)
            + " selected="
            + (string.IsNullOrWhiteSpace(selectedTechnologyId) ? "-" : selectedTechnologyId)
            + " treeNodes="
            + visibleTechnologyNodeCount
            + " last="
            + lastMessage;
    }

    private string BuildStatusText()
    {
        return string.IsNullOrWhiteSpace(lastMessage) ? "Архивы готовы." : lastMessage;
    }

    private string BuildArchiveHeaderResourceLine()
    {
        if (runtimeMeta == null)
        {
            return "SP: -";
        }

        EnsureSelectedCategory();
        int archiveCount = GetArchiveCountForUi();
        IReadOnlyList<string> slots = runtimeMeta.GetActiveResearchTechnologyIdsForUi(archiveCount);
        int activeSlots = 0;
        if (slots != null)
        {
            for (int i = 0; i < Mathf.Min(archiveCount, slots.Count); i++)
            {
                if (!string.IsNullOrWhiteSpace(slots[i])) activeSlots++;
            }
        }

        float spPerMinute = runtimeMeta.GetArchiveKnowledgeSpPerMinute(selectedCategoryId);
        return "Архивы " + archiveCount.ToString()
            + "     Занято " + activeSlots.ToString() + "/" + archiveCount.ToString()
            + "     " + spPerMinute.ToString("0.##") + " очк./мин";
    }

    private string BuildHeaderResourceLine()
    {
        if (runtimeMeta == null)
        {
            return "SP: -";
        }

        EnsureSelectedCategory();
        return "Архивы "
            + runtimeMeta.GetArchiveKnowledgeSpPerMinute(selectedCategoryId).ToString("0.##")
            + " SP/мин";
    }

    private string BuildTreeHeaderText()
    {
        if (runtimeMeta == null)
        {
            return "Конфигурация знаний не загружена.";
        }

        string categoryId = EnsureSelectedCategory();
        return GetCategoryDisplayName(categoryId)
            + " | узлов: "
            + CountTechnologiesInCategory(categoryId)
            + " | дерево рассчитано под "
            + DesignedTreeColumnCount
            + " колонок, прокрутка по горизонтали.";
    }

    private string BuildSelectedTechnologyDetails()
    {
        TechnologyConfig selected = GetSelectedTechnology();
        if (runtimeMeta == null || selected == null)
        {
            return "нет выбранного знания";
        }

        return runtimeMeta.GetTechnologyDisplayName(selected)
            + " | "
            + BuildSelectedStatus(selected)
            + " | "
            + BuildSelectedEffectText(selected).Replace("\n", " ");
    }

    private string BuildSelectedStatus(TechnologyConfig technology)
    {
        if (technology == null || runtimeMeta == null)
        {
            return "";
        }

        int level = runtimeMeta.GetTechnologyCompletedLevel(technology);
        int maxLevel = GetTechnologyMaxLevel(technology);
        int nextLevel = Mathf.Clamp(level + 1, 1, maxLevel);
        int levelSpCost = runtimeMeta.GetTechnologyLevelSpCost(technology, nextLevel);
        float spProgress = runtimeMeta.GetTechnologyCurrentLevelSpProgress(technology);
        int archiveCount = GetArchiveCountForUi();
        int activeSlot = runtimeMeta.GetActiveResearchSlotIndex(technology.id, archiveCount);
        string etaText = runtimeMeta.GetTechnologyResearchEtaText(technology, archiveCount);
        string activeText = activeSlot >= 0
            ? "Активно: Архивы льют SP сюда."
            : "Можно выбрать узел и направить Архивы.";

        if (runtimeMeta.IsTechnologyCompleted(technology.id))
        {
            return "Статус: изучено. Уровни " + level + "/" + maxLevel + ".";
        }

        return "Статус: " + runtimeMeta.GetTechnologyStatusText(technology)
            + "\nУровень " + level + "/" + maxLevel
            + ", следующий L" + nextLevel
            + ": " + FormatSpAmount(spProgress) + "/" + levelSpCost + " SP. "
            + "ETA: " + etaText + ". "
            + activeText;
    }

    private string BuildSelectedEffectText(TechnologyConfig technology)
    {
        if (technology == null)
        {
            return "Эффект: -";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Эффект");
        if (!string.IsNullOrWhiteSpace(technology.unlockSummaryRu))
        {
            builder.AppendLine(technology.unlockSummaryRu);
        }

        if (technology.modifierGrants == null || technology.modifierGrants.Count == 0)
        {
            builder.AppendLine("Модификатор: нет.");
            return builder.ToString().TrimEnd();
        }

        for (int i = 0; i < technology.modifierGrants.Count; i++)
        {
            TechnologyModifierGrantConfig grant = technology.modifierGrants[i];
            if (grant == null || string.IsNullOrWhiteSpace(grant.modifierId)) continue;
            ModifierDefinitionConfig modifier = runtimeMeta.SessionConfig != null
                ? runtimeMeta.SessionConfig.GetModifierDefinition(grant.modifierId)
                : null;
            string valueKind = modifier != null ? modifier.valueKind : "percent";
            string modifierName = runtimeMeta.SessionConfig != null
                ? runtimeMeta.SessionConfig.GetModifierNameRu(grant.modifierId)
                : grant.modifierId;
            builder.Append("• ");
            builder.Append(modifierName);
            builder.Append(": ");
            builder.Append(FormatModifierValue(grant.valuePerLevel, valueKind));
            builder.Append("/ур.");
            if (!string.IsNullOrWhiteSpace(grant.targetId))
            {
                builder.Append(" -> ");
                builder.Append(grant.targetId);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private string BuildArchiveSelectedCostText(TechnologyConfig technology)
    {
        if (technology == null || runtimeMeta == null)
        {
            return "Стоимость: -";
        }

        int level = runtimeMeta.GetTechnologyCompletedLevel(technology);
        int maxLevel = GetTechnologyMaxLevel(technology);
        int nextLevel = Mathf.Clamp(level + 1, 1, maxLevel);
        int levelSpCost = runtimeMeta.GetTechnologyLevelSpCost(technology, nextLevel);
        float spProgress = runtimeMeta.GetTechnologyCurrentLevelSpProgress(technology);

        return "Стоимость: " + levelSpCost.ToString() + " очков знаний"
            + "\nНакоплено: " + FormatSpAmount(spProgress) + "/" + levelSpCost.ToString() + " SP"
            + "\nРесурсы уровня: " + runtimeMeta.FormatTechnologyLevelCost(technology)
            + "\nВремя: " + runtimeMeta.GetTechnologyResearchEtaText(technology, GetArchiveCountForUi())
            + "\n" + runtimeMeta.GetKnowledgeSpPackageSummaryForUi(technology);
    }

    private string BuildSelectedCostText(TechnologyConfig technology)
    {
        if (technology == null || runtimeMeta == null)
        {
            return "Стоимость: -";
        }

        int level = runtimeMeta.GetTechnologyCompletedLevel(technology);
        int maxLevel = GetTechnologyMaxLevel(technology);
        int nextLevel = Mathf.Clamp(level + 1, 1, maxLevel);
        int levelSpCost = runtimeMeta.GetTechnologyLevelSpCost(technology, nextLevel);
        return "Стоимость уровня"
            + "\nSP L" + nextLevel + ": " + levelSpCost
            + "\nРесурсы: " + runtimeMeta.FormatTechnologyLevelCost(technology);
    }

    private string BuildSelectedRequirementText(TechnologyConfig technology)
    {
        if (technology == null)
        {
            return "Требования: -";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("Требования");
        if (!string.IsNullOrWhiteSpace(technology.lockSummaryRu))
        {
            builder.Append("\n").Append(technology.lockSummaryRu);
        }

        if (technology.prerequisiteTechnologyIds != null && technology.prerequisiteTechnologyIds.Count > 0)
        {
            builder.Append("\nПредыдущие: ");
            for (int i = 0; i < technology.prerequisiteTechnologyIds.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(GetTechnologyName(technology.prerequisiteTechnologyIds[i]));
            }
        }
        else
        {
            builder.Append("\nПредыдущие: нет.");
        }

        if (!runtimeMeta.IsKnowledgeAvailableForResearch(technology))
        {
            builder.Append("\nНужна книга/право.");
        }

        return builder.ToString();
    }

    private string EnsureSelectedCategory()
    {
        if (runtimeMeta == null)
        {
            selectedCategoryId = "";
            return selectedCategoryId;
        }

        IReadOnlyList<string> categories = runtimeMeta.GetTechnologyCategoryIdsForUi();
        if (categories == null || categories.Count == 0)
        {
            selectedCategoryId = "";
            selectedTechnologyId = "";
            return selectedCategoryId;
        }

        for (int i = 0; i < categories.Count; i++)
        {
            if (string.Equals(categories[i], selectedCategoryId, StringComparison.OrdinalIgnoreCase))
            {
                return selectedCategoryId;
            }
        }

        if (string.IsNullOrWhiteSpace(selectedCategoryId))
        {
            selectedCategoryId = "";
            for (int i = 0; i < categories.Count; i++)
            {
                if (string.Equals(categories[i], "base", StringComparison.OrdinalIgnoreCase))
                {
                    selectedCategoryId = categories[i];
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(selectedCategoryId))
            {
                selectedCategoryId = runtimeMeta.GetFirstTechnologyCategoryId();
            }

            if (string.IsNullOrWhiteSpace(selectedCategoryId))
            {
                selectedCategoryId = categories[0];
            }
        }
        else
        {
            selectedCategoryId = categories[0];
        }

        selectedTechnologyId = "";
        return selectedCategoryId;
    }

    private string EnsureSelectedTechnology()
    {
        if (runtimeMeta == null)
        {
            selectedTechnologyId = "";
            return selectedTechnologyId;
        }

        EnsureSelectedCategory();
        TechnologyConfig current = GetSelectedTechnology();
        if (current != null)
        {
            return selectedTechnologyId;
        }

        TechnologyConfig first = GetFirstTechnologyInCategory(selectedCategoryId);
        selectedTechnologyId = first != null ? first.id : "";
        return selectedTechnologyId;
    }

    private TechnologyConfig GetSelectedTechnology()
    {
        if (runtimeMeta == null || string.IsNullOrWhiteSpace(selectedTechnologyId))
        {
            return null;
        }

        IReadOnlyList<TechnologyConfig> technologies = runtimeMeta.GetTechnologyConfigs();
        if (technologies == null)
        {
            return null;
        }

        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology != null && string.Equals(technology.id, selectedTechnologyId, StringComparison.OrdinalIgnoreCase))
            {
                return technology;
            }
        }

        return null;
    }

    private TechnologyConfig GetFirstTechnologyInCategory(string categoryId)
    {
        List<TechnologyConfig> technologies = GetTechnologiesInCategory(categoryId);
        return technologies.Count > 0 ? technologies[0] : null;
    }

    private List<TechnologyConfig> GetTechnologiesInSelectedCategory()
    {
        return GetTechnologiesInCategory(EnsureSelectedCategory());
    }

    private List<TechnologyConfig> GetTechnologiesInCategory(string categoryId)
    {
        List<TechnologyConfig> result = new List<TechnologyConfig>();
        if (runtimeMeta == null || string.IsNullOrWhiteSpace(categoryId))
        {
            return result;
        }

        IReadOnlyList<TechnologyConfig> technologies = runtimeMeta.GetTechnologyConfigs();
        if (technologies == null)
        {
            return result;
        }

        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology != null && string.Equals(technology.categoryId, categoryId, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(technology);
            }
        }

        result.Sort(CompareTechnologyTreePosition);
        return result;
    }

    private int CountTechnologiesInCategory(string categoryId)
    {
        return GetTechnologiesInCategory(categoryId).Count;
    }

    private string GetCategoryDisplayName(string categoryId)
    {
        List<TechnologyConfig> technologies = GetTechnologiesInCategory(categoryId);
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology != null && !string.IsNullOrWhiteSpace(technology.CategoryDisplayNameRu))
            {
                return technology.CategoryDisplayNameRu;
            }
        }

        return string.IsNullOrWhiteSpace(categoryId) ? "-" : categoryId;
    }

    private float GetCategoryCompletion01(string categoryId)
    {
        List<TechnologyConfig> technologies = GetTechnologiesInCategory(categoryId);
        if (technologies.Count == 0 || runtimeMeta == null)
        {
            return 0f;
        }

        float completed = 0f;
        float total = 0f;
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null) continue;
            int max = GetTechnologyMaxLevel(technology);
            completed += runtimeMeta.GetTechnologyCompletedLevel(technology);
            total += max;
        }

        return total <= 0f ? 0f : completed / total;
    }

    private float GetTechnologyProgress01(TechnologyConfig technology)
    {
        if (technology == null || runtimeMeta == null)
        {
            return 0f;
        }

        int maxLevel = GetTechnologyMaxLevel(technology);
        int completed = runtimeMeta.GetTechnologyCompletedLevel(technology);
        if (completed >= maxLevel)
        {
            return 1f;
        }

        int nextLevel = Mathf.Clamp(completed + 1, 1, maxLevel);
        int levelCost = runtimeMeta.GetTechnologyLevelSpCost(technology, nextLevel);
        float levelProgress = levelCost <= 0 ? 0f : runtimeMeta.GetTechnologyCurrentLevelSpProgress(technology) / levelCost;
        return (completed + Mathf.Clamp01(levelProgress)) / Mathf.Max(1, maxLevel);
    }

    private void UpdateVisibleTechnologyProgressBars()
    {
        if (technologyProgressFills.Count == 0 || runtimeMeta == null)
        {
            return;
        }

        List<TechnologyConfig> technologies = GetTechnologiesInSelectedCategory();
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id))
            {
                continue;
            }

            if (technologyProgressFills.TryGetValue(technology.id, out RectTransform fill) && fill != null)
            {
                fill.anchorMax = new Vector2(GetTechnologyProgress01(technology), 1f);
            }
        }
    }

    private bool ArePrerequisitesCompletedForUi(TechnologyConfig technology)
    {
        if (runtimeMeta == null || technology == null || technology.prerequisiteTechnologyIds == null)
        {
            return true;
        }

        for (int i = 0; i < technology.prerequisiteTechnologyIds.Count; i++)
        {
            string prerequisiteId = technology.prerequisiteTechnologyIds[i];
            if (!string.IsNullOrWhiteSpace(prerequisiteId) && !runtimeMeta.IsTechnologyCompleted(prerequisiteId))
            {
                return false;
            }
        }

        return true;
    }

    private string GetTechnologyName(string technologyId)
    {
        if (runtimeMeta == null || runtimeMeta.SessionConfig == null)
        {
            return technologyId ?? "";
        }

        return runtimeMeta.SessionConfig.GetTechnologyNameRu(technologyId);
    }

    private int GetArchiveCountForUi()
    {
        WildWindBaseIslandView island = FindFirstObjectByType<WildWindBaseIslandView>();
        int count = island != null ? island.GetPlacedBuildingCountForTests("archive") : 1;
        return runtimeMeta != null ? runtimeMeta.GetArchiveResearchSlotCount(count) : Mathf.Max(1, count);
    }

    private Vector2 GetNodeCenter(TechnologyConfig technology)
    {
        int column = Mathf.Max(0, technology != null ? technology.treeColumn : 0);
        int row = Mathf.Max(0, technology != null ? technology.treeRow : 0);
        return new Vector2(120f + column * 190f, -130f - row * 150f);
    }

    private static int CompareTechnologyTreePosition(TechnologyConfig left, TechnologyConfig right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int columnComparison = left.treeColumn.CompareTo(right.treeColumn);
        if (columnComparison != 0) return columnComparison;
        int rowComparison = left.treeRow.CompareTo(right.treeRow);
        return rowComparison != 0 ? rowComparison : string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetTechnologyMaxLevel(TechnologyConfig technology)
    {
        return Mathf.Max(1, technology != null ? technology.requiredCycles : 1);
    }

    private static string FormatModifierValue(float value, string valueKind)
    {
        if (valueKind == "unlock")
        {
            return value > 0.5f ? "открыто" : "закрыто";
        }

        if (valueKind == "percent" || valueKind == "multiplier")
        {
            return (value >= 0f ? "+" : "") + (value * 100f).ToString("0.#") + "%";
        }

        return (value >= 0f ? "+" : "") + value.ToString("0.##");
    }

    private static string FormatSpAmount(float value)
    {
        value = Mathf.Max(0f, value);
        if (value < 10f)
        {
            return value.ToString("0.00");
        }

        if (value < 100f)
        {
            return value.ToString("0.0");
        }

        return value.ToString("0");
    }

    private static string ToRoman(int value)
    {
        return value switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            8 => "VIII",
            9 => "IX",
            10 => "X",
            _ => value.ToString()
        };
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(transform.GetChild(i).gameObject);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            DestroyGeneratedObject(child);
        }
    }

    private static void DestroyGeneratedObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private RectTransform CreateCapsule(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color)
    {
        return CreateImage(name, parent, new RectTransformSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = new Vector2(anchor.x, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        }, color).GetComponent<RectTransform>();
    }

    private void CreateResourcePill(RectTransform parent, string iconText, string amount, Vector2 anchoredPosition)
    {
        RectTransform pill = CreateCapsule(parent, "Resource Pill " + iconText, anchoredPosition, new Vector2(180f, 54f), new Vector2(0f, 1f), TopBarColor);
        AddSoftShadow(pill.gameObject, 0.12f, new Vector2(0f, -2f));
        CreateIconMedallion(pill, iconText, new Vector2(10f, -8f), 38f, new Color(0.82f, 0.90f, 0.98f, 1f));
        CreateText(pill, amount, 22, new Vector2(58f, -11f), new Vector2(82f, 30f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        CreateText(pill, "+", 26, new Vector2(-36f, -8f), new Vector2(28f, 32f), TextAnchor.MiddleCenter, MutedTextColor, new Vector2(1f, 1f)).fontStyle = FontStyle.Bold;
    }

    private void CreateTopIconButton(RectTransform parent, string iconText, Vector2 anchoredPosition)
    {
        RectTransform button = CreateCapsule(parent, "Top Icon " + iconText, anchoredPosition, new Vector2(62f, 54f), new Vector2(1f, 1f), TopBarColor);
        AddSoftShadow(button.gameObject, 0.12f, new Vector2(0f, -2f));
        CreateText(button, iconText, 22, Vector2.zero, button.sizeDelta, TextAnchor.MiddleCenter, MutedTextColor).fontStyle = FontStyle.Bold;
    }

    private void CreateIconMedallion(RectTransform parent, string label, Vector2 anchoredPosition, float size, Color color)
    {
        RectTransform medallion = CreateImage("Icon Medallion " + label, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = new Vector2(size, size)
        }, color).GetComponent<RectTransform>();
        AddBorder(medallion, new Color(0.50f, 0.62f, 0.74f, 0.60f), new Vector2(size - 4f, size - 4f));
        Text icon = CreateText(medallion, label, Mathf.Clamp(Mathf.RoundToInt(size * 0.32f), 12, 24), Vector2.zero, medallion.sizeDelta, TextAnchor.MiddleCenter, TextColor);
        icon.fontStyle = FontStyle.Bold;
    }

    private void CreateBlueprintLines(RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        for (int i = 0; i < 5; i++)
        {
            float y = -8f - i * (size.y / 5f);
            CreateImage("Blueprint H " + i, parent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = anchoredPosition + new Vector2(0f, y),
                sizeDelta = new Vector2(size.x, 1f)
            }, new Color(0.45f, 0.60f, 0.72f, 0.12f)).raycastTarget = false;
        }

        for (int i = 0; i < 6; i++)
        {
            float x = i * (size.x / 6f);
            CreateImage("Blueprint V " + i, parent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = anchoredPosition + new Vector2(x, 0f),
                sizeDelta = new Vector2(1f, size.y)
            }, new Color(0.45f, 0.60f, 0.72f, 0.10f)).raycastTarget = false;
        }
    }

    private static void AddSoftShadow(GameObject target, float alpha, Vector2 distance)
    {
        if (target == null)
        {
            return;
        }

        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.18f, 0.26f, 0.34f, alpha);
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static void SetRaycastTargetRecursive(RectTransform parent, bool value)
    {
        if (parent == null)
        {
            return;
        }

        Graphic[] graphics = parent.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = value;
            }
        }
    }

    private static void SetButtonPalette(RectTransform buttonRect, Color normal, Color highlighted, Color pressed)
    {
        if (buttonRect == null)
        {
            return;
        }

        Image image = buttonRect.GetComponent<Image>();
        if (image != null)
        {
            image.color = normal;
        }

        Button button = buttonRect.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.selectedColor = highlighted;
        button.colors = colors;
    }

    private static void AddBorder(RectTransform parent, Color color, Vector2 size)
    {
        if (parent == null)
        {
            return;
        }

        Image border = CreateImage("Soft Border", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = size
        }, new Color(1f, 1f, 1f, 0f));
        Outline outline = border.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;
        border.raycastTarget = false;
        border.transform.SetAsFirstSibling();
    }

    private static string GetCategoryIconText(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            return "АР";
        }

        string normalized = categoryId.Trim().ToLowerInvariant();
        if (normalized.Contains("base")) return "БЗ";
        if (normalized.Contains("research") || normalized.Contains("scan")) return "РЗ";
        if (normalized.Contains("ore") || normalized.Contains("mining")) return "РД";
        if (normalized.Contains("gas")) return "ГЗ";
        if (normalized.Contains("drone") || normalized.Contains("salvage")) return "ДР";
        if (normalized.Contains("leviathan") || normalized.Contains("hunt")) return "ЛВ";
        if (normalized.Contains("trade")) return "ТР";
        if (normalized.Contains("cascade") || normalized.Contains("prod")) return "ЦХ";
        return normalized.Length >= 2 ? normalized.Substring(0, 2).ToUpperInvariant() : normalized.ToUpperInvariant();
    }

    private RectTransform CreatePanel(string name, Vector2 size, Vector2 anchoredPosition, Vector2 anchor)
    {
        return CreateImage(name, root, new RectTransformSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = new Vector2(anchor.x, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        }, PanelColor).GetComponent<RectTransform>();
    }

    private Text CreateButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Func<bool> action)
    {
        GameObject buttonObject = CreateRect("Button " + name, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });

        Image image = buttonObject.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = ButtonHoverColor;
        button.colors = colors;
        button.onClick.AddListener(() => action?.Invoke());

        Text label = CreateText(buttonObject.GetComponent<RectTransform>(), name, size.y > 42f ? 19 : 15, Vector2.zero, size, TextAnchor.MiddleCenter, TextColor);
        label.raycastTarget = false;
        return label;
    }

    private static Text CreateText(RectTransform parent, string value, int size, Vector2 anchoredPosition, Vector2 rectSize, TextAnchor alignment, Color color, Vector2? anchor = null)
    {
        Vector2 anchorValue = anchor ?? new Vector2(0f, 1f);
        GameObject textObject = CreateRect("Text", parent, new RectTransformSpec
        {
            anchorMin = anchorValue,
            anchorMax = anchorValue,
            pivot = new Vector2(anchorValue.x, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = rectSize
        });

        Text text = textObject.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        SetText(text, value);
        return text;
    }

    private static Image CreateImage(string name, RectTransform parent, RectTransformSpec spec, Color color)
    {
        GameObject obj = CreateRect(name, parent, spec);
        Image image = obj.AddComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent, RectTransformSpec spec)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMax;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.sizeDelta;
        return obj;
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

    private static void EnsureEventSystem(Transform parent)
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(EventSystemName);
        eventSystemObject.transform.SetParent(parent, false);
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedDefaultFont;
    }

    private static Sprite GetRoundedSprite()
    {
        if (cachedRoundedSprite != null)
        {
            return cachedRoundedSprite;
        }

        const int size = 32;
        const int radius = 12;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "WW Rounded Ui Sprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool left = x < radius;
                bool right = x >= size - radius;
                bool bottom = y < radius;
                bool top = y >= size - radius;
                bool corner = (left || right) && (bottom || top);
                if (!corner)
                {
                    texture.SetPixel(x, y, solid);
                    continue;
                }

                float cx = left ? radius : size - radius - 1;
                float cy = bottom ? radius : size - radius - 1;
                float dx = x - cx;
                float dy = y - cy;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? solid : clear);
            }
        }

        texture.Apply(false, true);
        cachedRoundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        cachedRoundedSprite.name = "WW Rounded Ui Sprite";
        cachedRoundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return cachedRoundedSprite;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null && text.text != value)
        {
            text.text = value ?? "";
        }
    }

    private static bool HasRenderableText(Text text)
    {
        if (text == null || text.font == null || string.IsNullOrWhiteSpace(text.text) || !text.gameObject.activeInHierarchy)
        {
            return false;
        }

        RectTransform rect = text.GetComponent<RectTransform>();
        return rect != null && rect.rect.width > 8f && rect.rect.height > 8f;
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
