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

    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public int sortingOrder = 6500;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform rubricTabRoot;
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
    private static Font cachedDefaultFont;

    private static readonly Color BackgroundColor = new Color(0.020f, 0.026f, 0.030f, 1f);
    private static readonly Color TopBarColor = new Color(0.030f, 0.040f, 0.046f, 0.96f);
    private static readonly Color PanelColor = new Color(0.046f, 0.056f, 0.062f, 0.94f);
    private static readonly Color PanelDarkColor = new Color(0.028f, 0.036f, 0.041f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.095f, 0.088f, 0.074f, 0.94f);
    private static readonly Color ButtonHoverColor = new Color(0.190f, 0.152f, 0.092f, 0.98f);
    private static readonly Color ButtonPressedColor = new Color(0.360f, 0.240f, 0.105f, 1f);
    private static readonly Color SelectedColor = new Color(0.055f, 0.360f, 0.380f, 0.96f);
    private static readonly Color AvailableColor = new Color(0.145f, 0.118f, 0.076f, 0.98f);
    private static readonly Color LockedColor = new Color(0.090f, 0.095f, 0.098f, 0.94f);
    private static readonly Color CompletedColor = new Color(0.075f, 0.220f, 0.160f, 0.96f);
    private static readonly Color ActiveColor = new Color(0.200f, 0.145f, 0.065f, 0.98f);
    private static readonly Color ConnectionColor = new Color(0.130f, 0.720f, 0.760f, 0.65f);
    private static readonly Color LockedConnectionColor = new Color(0.240f, 0.260f, 0.270f, 0.72f);
    private static readonly Color TextColor = new Color(0.91f, 0.84f, 0.70f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.72f, 0.72f, 1f);
    private static readonly Color AccentColor = new Color(0.92f, 0.67f, 0.34f, 1f);
    private static readonly Color CyanTextColor = new Color(0.56f, 0.93f, 0.95f, 1f);
    private static readonly Color BadTextColor = new Color(0.95f, 0.47f, 0.36f, 1f);

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
        background.raycastTarget = false;
    }

    private void BuildTopBar()
    {
        RectTransform topBar = CreateImage("Top Bar", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 96f)
        }, TopBarColor).GetComponent<RectTransform>();

        titleText = CreateText(topBar, "АРХИВЫ", 34, new Vector2(44f, -12f), new Vector2(380f, 44f), TextAnchor.MiddleLeft, TextColor);
        titleText.fontStyle = FontStyle.Bold;
        subtitleText = CreateText(topBar, "Знания, SP архивов, книги и уровни 1-5", 17, new Vector2(46f, -56f), new Vector2(720f, 26f), TextAnchor.MiddleLeft, MutedTextColor);
        resourceText = CreateText(topBar, "", 16, new Vector2(-44f, -16f), new Vector2(900f, 56f), TextAnchor.MiddleRight, AccentColor, new Vector2(1f, 1f));
    }

    private void BuildLayout()
    {
        RectTransform rubricsPanel = CreatePanel("Knowledge Rubrics", new Vector2(280f, 820f), new Vector2(34f, -126f), new Vector2(0f, 1f));
        Text rubricsTitle = CreateText(rubricsPanel, "РУБРИКИ", 21, new Vector2(24f, -22f), new Vector2(230f, 32f), TextAnchor.MiddleLeft, TextColor);
        rubricsTitle.fontStyle = FontStyle.Bold;
        rubricTabRoot = CreateRect("Rubric Tabs", rubricsPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(20f, -72f),
            sizeDelta = new Vector2(240f, 640f)
        }).GetComponent<RectTransform>();

        RectTransform treePanel = CreatePanel("Knowledge Tree", new Vector2(1048f, 820f), new Vector2(334f, -126f), new Vector2(0f, 1f));
        Text treeTitle = CreateText(treePanel, "ДЕРЕВО ЗНАНИЙ", 22, new Vector2(24f, -18f), new Vector2(360f, 34f), TextAnchor.MiddleLeft, TextColor);
        treeTitle.fontStyle = FontStyle.Bold;
        contentText = CreateText(treePanel, "", 15, new Vector2(24f, -52f), new Vector2(760f, 42f), TextAnchor.UpperLeft, MutedTextColor);
        contentText.verticalOverflow = VerticalWrapMode.Overflow;

        treeViewport = CreateImage("Knowledge Tree Viewport", treePanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(24f, -112f),
            sizeDelta = new Vector2(1000f, 660f)
        }, PanelDarkColor).GetComponent<RectTransform>();
        Mask viewportMask = treeViewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        treeContent = CreateRect("Knowledge Tree Content", treeViewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(DesignedTreeColumnCount * 220f + 180f, 640f)
        }).GetComponent<RectTransform>();

        treeScrollRect = treePanel.gameObject.AddComponent<ScrollRect>();
        treeScrollRect.content = treeContent;
        treeScrollRect.viewport = treeViewport;
        treeScrollRect.horizontal = true;
        treeScrollRect.vertical = true;
        treeScrollRect.movementType = ScrollRect.MovementType.Clamped;
        treeScrollRect.scrollSensitivity = 36f;

        inspectorPanel = CreatePanel("Knowledge Inspector", new Vector2(480f, 820f), new Vector2(-34f, -126f), new Vector2(1f, 1f));
        actionTitleText = CreateText(inspectorPanel, "ЗНАНИЕ", 22, new Vector2(28f, -24f), new Vector2(420f, 34f), TextAnchor.MiddleLeft, TextColor);
        actionTitleText.fontStyle = FontStyle.Bold;
        selectedTitleText = CreateText(inspectorPanel, "", 23, new Vector2(28f, -70f), new Vector2(420f, 58f), TextAnchor.UpperLeft, CyanTextColor);
        selectedTitleText.fontStyle = FontStyle.Bold;
        selectedTitleText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedStatusText = CreateText(inspectorPanel, "", 15, new Vector2(28f, -132f), new Vector2(420f, 62f), TextAnchor.UpperLeft, MutedTextColor);
        selectedStatusText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedEffectText = CreateText(inspectorPanel, "", 16, new Vector2(28f, -210f), new Vector2(420f, 160f), TextAnchor.UpperLeft, TextColor);
        selectedEffectText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedCostText = CreateText(inspectorPanel, "", 16, new Vector2(28f, -392f), new Vector2(420f, 110f), TextAnchor.UpperLeft, TextColor);
        selectedCostText.verticalOverflow = VerticalWrapMode.Overflow;
        selectedRequirementText = CreateText(inspectorPanel, "", 15, new Vector2(28f, -520f), new Vector2(420f, 96f), TextAnchor.UpperLeft, MutedTextColor);
        selectedRequirementText.verticalOverflow = VerticalWrapMode.Overflow;
        primaryActionText = CreateButton(inspectorPanel, "Направить Архивы", new Vector2(28f, -642f), new Vector2(420f, 54f), RunPrimaryAction);
        secondaryActionText = CreateButton(inspectorPanel, "Следующая рубрика", new Vector2(28f, -708f), new Vector2(204f, 48f), RunSecondaryAction);
        CreateButton(inspectorPanel, "Закрыть", new Vector2(244f, -708f), new Vector2(204f, 48f), CloseScreen);
        statusText = CreateText(inspectorPanel, "", 14, new Vector2(28f, -770f), new Vector2(420f, 42f), TextAnchor.UpperLeft, AccentColor);
        statusText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private bool RunPrimaryAction()
    {
        if (runtimeMeta == null)
        {
            lastMessage = "MetaGameState недоступен.";
            RefreshScreen();
            return false;
        }

        TechnologyConfig selected = GetSelectedTechnology();
        if (selected != null && !runtimeMeta.IsTechnologyCompleted(selected.id))
        {
            if (!runtimeMeta.CanSelectResearchTechnology(selected, out string reason))
            {
                lastMessage = reason;
                RefreshScreen();
                return true;
            }

            if (runtimeMeta.TrySelectResearchTechnology(selected.id))
            {
                lastMessage = "Архивы направлены в знание: " + runtimeMeta.GetTechnologyDisplayName(selected) + ".";
                RefreshScreen();
                return true;
            }

            lastMessage = "Не удалось направить Архивы в знание: " + runtimeMeta.GetTechnologyDisplayName(selected) + ".";
            RefreshScreen();
            return true;
        }

        string firstBlocked = "";
        List<TechnologyConfig> technologies = GetTechnologiesInSelectedCategory();
        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || runtimeMeta.IsTechnologyCompleted(technology.id)) continue;

            if (runtimeMeta.TrySelectResearchTechnology(technology.id))
            {
                selectedTechnologyId = technology.id;
                lastMessage = "Архивы направлены в знание: " + runtimeMeta.GetTechnologyDisplayName(technology) + ".";
                RefreshScreen();
                return true;
            }

            if (string.IsNullOrWhiteSpace(firstBlocked))
            {
                firstBlocked = runtimeMeta.GetTechnologyDisplayName(technology);
            }
        }

        lastMessage = string.IsNullOrWhiteSpace(firstBlocked)
            ? "В этой рубрике все знания завершены или недоступны."
            : "Первое доступное действие заблокировано: " + firstBlocked + ".";
        RefreshScreen();
        return true;
    }

    private bool RunSecondaryAction()
    {
        if (runtimeMeta == null)
        {
            lastMessage = "MetaGameState недоступен.";
            RefreshScreen();
            return false;
        }

        string nextCategory = runtimeMeta.GetNextTechnologyCategoryId(selectedCategoryId);
        if (string.IsNullOrWhiteSpace(nextCategory))
        {
            lastMessage = "Рубрики знаний не загружены.";
            RefreshScreen();
            return false;
        }

        selectedCategoryId = nextCategory;
        selectedTechnologyId = "";
        EnsureSelectedTechnology();
        lastMessage = "Рубрика знаний: " + GetCategoryDisplayName(selectedCategoryId) + ".";
        RefreshScreen();
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
        SetText(resourceText, BuildHeaderResourceLine());
        SetText(contentText, BuildTreeHeaderText());
        SetText(primaryActionText, "Направить Архивы");
        SetText(secondaryActionText, "Следующая рубрика");
        SetText(statusText, BuildReport());
        RebuildRubricTabs();
        RebuildTechnologyTree();
        RefreshInspector();
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
            CreateText(rubricTabRoot, "Нет рубрик", 16, Vector2.zero, new Vector2(220f, 28f), TextAnchor.MiddleLeft, MutedTextColor);
            return;
        }

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
                anchoredPosition = new Vector2(0f, -visibleRubricTabCount * 68f),
                sizeDelta = new Vector2(240f, 56f)
            }, selected ? SelectedColor : ButtonColor).GetComponent<RectTransform>();

            string capturedCategoryId = categoryId;
            Button button = tab.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? SelectedColor : ButtonColor;
            colors.highlightedColor = selected ? SelectedColor : ButtonHoverColor;
            colors.pressedColor = ButtonPressedColor;
            colors.selectedColor = SelectedColor;
            button.colors = colors;
            button.onClick.AddListener(() => SelectCategory(capturedCategoryId));

            Text label = CreateText(tab, GetCategoryDisplayName(categoryId), 17, new Vector2(14f, -5f), new Vector2(174f, 28f), TextAnchor.MiddleLeft, selected ? Color.white : TextColor);
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            CreateText(tab, count.ToString(), 15, new Vector2(-16f, -7f), new Vector2(42f, 24f), TextAnchor.MiddleRight, AccentColor, new Vector2(1f, 1f));
            CreateProgressLine(tab, new Vector2(14f, -42f), new Vector2(176f, 4f), GetCategoryCompletion01(categoryId));
            visibleRubricTabCount++;
        }
    }

    private void RebuildTechnologyTree()
    {
        visibleTechnologyNodeCount = 0;
        visibleConnectionCount = 0;
        visibleTreeColumnCount = DesignedTreeColumnCount;
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
        float contentWidth = visibleTreeColumnCount * 220f + 180f;
        float contentHeight = Mathf.Max(640f, rowCount * 160f + 200f);
        treeContent.sizeDelta = new Vector2(contentWidth, contentHeight);

        for (int column = 0; column < visibleTreeColumnCount; column++)
        {
            string label = ToRoman(column + 1) + " УР.";
            CreateText(treeContent, label, 14, new Vector2(64f + column * 220f, -26f), new Vector2(140f, 24f), TextAnchor.MiddleCenter, MutedTextColor);
            CreateImage("Column Guide " + column, treeContent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = new Vector2(134f + column * 220f, -58f),
                sizeDelta = new Vector2(1f, contentHeight - 92f)
            }, new Color(0.18f, 0.24f, 0.25f, 0.28f)).raycastTarget = false;
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
        RefreshScreen();
    }

    private void CreateTechnologyNode(TechnologyConfig technology)
    {
        Vector2 center = GetNodeCenter(technology);
        bool selected = string.Equals(technology.id, selectedTechnologyId, StringComparison.OrdinalIgnoreCase);
        bool completed = runtimeMeta.IsTechnologyCompleted(technology.id);
        bool active = runtimeMeta.progress != null && runtimeMeta.progress.activeResearchTechnologyId == technology.id;
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
            sizeDelta = new Vector2(164f, 104f)
        }, nodeColor).GetComponent<RectTransform>();

        Button button = node.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = nodeColor;
        colors.highlightedColor = selected ? SelectedColor : ButtonHoverColor;
        colors.pressedColor = ButtonPressedColor;
        colors.selectedColor = SelectedColor;
        button.colors = colors;
        string capturedId = technology.id;
        button.onClick.AddListener(() => SelectTechnology(capturedId));

        Text icon = CreateText(node, string.IsNullOrWhiteSpace(technology.iconText) ? "KN" : technology.iconText, 17, new Vector2(10f, -8f), new Vector2(42f, 26f), TextAnchor.MiddleLeft, AccentColor);
        icon.fontStyle = FontStyle.Bold;
        Text name = CreateText(node, runtimeMeta.GetTechnologyDisplayName(technology), 14, new Vector2(12f, -36f), new Vector2(140f, 38f), TextAnchor.UpperLeft, selected ? Color.white : TextColor);
        name.verticalOverflow = VerticalWrapMode.Truncate;
        int level = runtimeMeta.GetTechnologyCompletedLevel(technology);
        int maxLevel = GetTechnologyMaxLevel(technology);
        Text levelText = CreateText(node, level + "/" + maxLevel, 14, new Vector2(-12f, -9f), new Vector2(52f, 24f), TextAnchor.MiddleRight, completed ? new Color(0.58f, 1f, 0.78f, 1f) : MutedTextColor, new Vector2(1f, 1f));
        levelText.fontStyle = FontStyle.Bold;
        CreateProgressLine(node, new Vector2(12f, -90f), new Vector2(140f, 5f), GetTechnologyProgress01(technology));

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
        float startX = from.x + 82f;
        float endX = to.x - 82f;
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

    private void CreateProgressLine(RectTransform parent, Vector2 anchoredPosition, Vector2 size, float progress01)
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
        string activeText = runtimeMeta.progress != null && runtimeMeta.progress.activeResearchTechnologyId == technology.id
            ? "Активно: Архивы льют SP сюда."
            : "Можно выбрать узел и направить Архивы.";

        if (runtimeMeta.IsTechnologyCompleted(technology.id))
        {
            return "Статус: изучено. Уровни " + level + "/" + maxLevel + ".";
        }

        return "Статус: " + runtimeMeta.GetTechnologyStatusText(technology)
            + "\nУровень " + level + "/" + maxLevel
            + ", следующий L" + nextLevel
            + ": " + Mathf.FloorToInt(spProgress) + "/" + levelSpCost + " SP. "
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

        selectedCategoryId = string.IsNullOrWhiteSpace(selectedCategoryId)
            ? runtimeMeta.GetFirstTechnologyCategoryId()
            : categories[0];
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

    private Vector2 GetNodeCenter(TechnologyConfig technology)
    {
        int column = Mathf.Max(0, technology != null ? technology.treeColumn : 0);
        int row = Mathf.Max(0, technology != null ? technology.treeRow : 0);
        return new Vector2(134f + column * 220f, -142f - row * 154f);
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
