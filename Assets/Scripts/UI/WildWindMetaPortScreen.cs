using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class WildWindMetaPortScreen : MonoBehaviour
{
    private const string CanvasName = "Meta Port Canvas";
    private const string EventSystemName = "Meta Port EventSystem";

    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public int sortingOrder = 900;

    private Canvas canvas;
    private RectTransform root;
    private Text resourceText;
    private Text shipText;
    private Text contentText;
    private Text statusText;
    private Text primaryActionText;
    private Text secondaryActionText;
    private Text[] tabTexts;
    private WildWindMetaPortUiState state;
    private Func<bool> closeAction;
    private static Font cachedDefaultFont;

    private static readonly Color BackgroundColor = new Color(0.018f, 0.024f, 0.030f, 1f);
    private static readonly Color TopBarColor = new Color(0.030f, 0.036f, 0.042f, 0.96f);
    private static readonly Color PanelColor = new Color(0.044f, 0.052f, 0.060f, 0.94f);
    private static readonly Color SoftPanelColor = new Color(0.070f, 0.076f, 0.078f, 0.90f);
    private static readonly Color ButtonColor = new Color(0.095f, 0.088f, 0.074f, 0.92f);
    private static readonly Color ButtonHoverColor = new Color(0.190f, 0.152f, 0.092f, 0.98f);
    private static readonly Color ButtonPressedColor = new Color(0.360f, 0.240f, 0.105f, 1f);
    private static readonly Color AccentColor = new Color(0.92f, 0.67f, 0.34f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.84f, 0.70f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.68f, 0.67f, 1f);

    public string Report => EnsureState().BuildReport();
    public string ContentForTests => EnsureState().BuildTabContent();
    public string SelectedTabName => WildWindMetaPortUiState.GetTabDisplayName(EnsureState().selectedTab);
    public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
    public bool IsReadyForTests => canvas != null
        && root != null
        && EnsureState().IsReady
        && tabTexts != null
        && tabTexts.Length == WildWindMetaPortUiState.Tabs.Count;

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

    public void RebuildScreen()
    {
        ClearGeneratedChildren();
        EnsureEventSystem(transform);
        BuildCanvas();
        BuildBackground();
        BuildTopBar();
        BuildLayout();
        RefreshScreen();
    }

    public void BindState(WildWindMetaPortUiState externalState)
    {
        state = externalState ?? WildWindMetaPortUiState.CreateFromRuntime(FindFirstObjectByType<MetaGameState>());
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

    public bool SelectTabForTests(WildWindMetaPortTab tab)
    {
        return SelectTab(tab);
    }

    public bool RunPrimaryActionForTests()
    {
        return RunPrimaryAction();
    }

    public bool RunSecondaryActionForTests()
    {
        return RunSecondaryAction();
    }

    public bool SelectNextShipForTests()
    {
        return SelectNextShip();
    }

    public bool SelectPreviousShipForTests()
    {
        return SelectPreviousShip();
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

        root = CreateRect("Meta Port Root", canvas.transform, StretchFull()).GetComponent<RectTransform>();
    }

    private void BuildBackground()
    {
        Image background = CreateImage("Background", root, StretchFull(), BackgroundColor);
        background.raycastTarget = false;
        Image deckShade = CreateImage("Deck Shade", root, StretchFull(), new Color(0.12f, 0.09f, 0.05f, 0.18f));
        deckShade.raycastTarget = false;
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

        Text title = CreateText(topBar, "WILD WIND PORT", 34, new Vector2(44f, -12f), new Vector2(440f, 44f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;
        CreateText(topBar, "Session port: ships, fitting, sorties, production and progression", 17, new Vector2(46f, -56f), new Vector2(620f, 26f), TextAnchor.MiddleLeft, MutedTextColor);
        resourceText = CreateText(topBar, "", 16, new Vector2(-44f, -16f), new Vector2(1180f, 56f), TextAnchor.MiddleRight, AccentColor, new Vector2(1f, 1f));
    }

    private void BuildLayout()
    {
        RectTransform navPanel = CreatePanel("Navigation Panel", new Vector2(320f, 846f), new Vector2(42f, -118f), new Vector2(0f, 1f));
        CreateText(navPanel, "SECTIONS", 22, new Vector2(24f, -18f), new Vector2(260f, 34f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;

        tabTexts = new Text[WildWindMetaPortUiState.Tabs.Count];
        for (int i = 0; i < WildWindMetaPortUiState.Tabs.Count; i++)
        {
            WildWindMetaPortTab tab = WildWindMetaPortUiState.Tabs[i];
            tabTexts[i] = CreateButton(
                navPanel,
                "Tab " + tab,
                new Vector2(24f, -58f - i * 28f),
                new Vector2(270f, 24f),
                () => SelectTab(tab));
        }

        RectTransform shipPanel = CreatePanel("Ship Panel", new Vector2(1490f, 166f), new Vector2(388f, -118f), new Vector2(0f, 1f));
        CreateButton(shipPanel, "<", new Vector2(24f, -26f), new Vector2(58f, 48f), SelectPreviousShip);
        CreateButton(shipPanel, ">", new Vector2(94f, -26f), new Vector2(58f, 48f), SelectNextShip);
        shipText = CreateText(shipPanel, "", 16, new Vector2(178f, -20f), new Vector2(1260f, 116f), TextAnchor.UpperLeft, TextColor);

        RectTransform contentPanel = CreatePanel("Content Panel", new Vector2(1040f, 648f), new Vector2(388f, -302f), new Vector2(0f, 1f));
        contentText = CreateText(contentPanel, "", 17, new Vector2(30f, -28f), new Vector2(970f, 560f), TextAnchor.UpperLeft, TextColor);

        RectTransform actionPanel = CreatePanel("Action Panel", new Vector2(426f, 648f), new Vector2(-42f, -302f), new Vector2(1f, 1f));
        CreateText(actionPanel, "ACTIONS", 22, new Vector2(28f, -24f), new Vector2(360f, 34f), TextAnchor.MiddleLeft, TextColor).fontStyle = FontStyle.Bold;
        primaryActionText = CreateButton(actionPanel, "Primary", new Vector2(28f, -88f), new Vector2(360f, 54f), RunPrimaryAction);
        secondaryActionText = CreateButton(actionPanel, "Secondary", new Vector2(28f, -154f), new Vector2(360f, 54f), RunSecondaryAction);
        CreateButton(actionPanel, "Close Port", new Vector2(28f, -220f), new Vector2(360f, 48f), CloseScreen);
        statusText = CreateText(actionPanel, "", 16, new Vector2(28f, -292f), new Vector2(360f, 274f), TextAnchor.UpperLeft, AccentColor);
    }

    private bool SelectTab(WildWindMetaPortTab tab)
    {
        EnsureState().SelectTab(tab);
        RefreshScreen();
        return true;
    }

    private bool RunPrimaryAction()
    {
        WildWindMetaPortActionResult result = EnsureState().RunPrimaryAction();
        RefreshScreen();
        return result.success;
    }

    private bool RunSecondaryAction()
    {
        WildWindMetaPortActionResult result = EnsureState().RunSecondaryAction();
        RefreshScreen();
        return result.success;
    }

    private bool SelectNextShip()
    {
        bool changed = EnsureState().SelectNextShip();
        RefreshScreen();
        return changed;
    }

    private bool SelectPreviousShip()
    {
        bool changed = EnsureState().SelectPreviousShip();
        RefreshScreen();
        return changed;
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
        WildWindMetaPortUiState current = EnsureState();
        SetText(resourceText, current.BuildResourceStrip());
        SetText(shipText, current.BuildShipCarousel() + "\n\n" + current.BuildSelectedShipSummary());
        SetText(contentText, current.BuildTabContent());
        SetText(primaryActionText, current.GetPrimaryActionLabel());
        SetText(secondaryActionText, current.GetSecondaryActionLabel());
        SetText(statusText, current.BuildReport());

        if (tabTexts == null)
        {
            return;
        }

        for (int i = 0; i < tabTexts.Length && i < WildWindMetaPortUiState.Tabs.Count; i++)
        {
            WildWindMetaPortTab tab = WildWindMetaPortUiState.Tabs[i];
            string prefix = tab == current.selectedTab ? "[x] " : "[ ] ";
            SetText(tabTexts[i], prefix + WildWindMetaPortUiState.GetTabDisplayName(tab));
        }
    }

    private WildWindMetaPortUiState EnsureState()
    {
        state ??= WildWindMetaPortUiState.CreateFromRuntime(FindFirstObjectByType<MetaGameState>());
        return state;
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(transform.GetChild(i).gameObject);
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
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
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

    private Text CreateButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, System.Func<bool> action)
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
        colors.disabledColor = SoftPanelColor;
        button.colors = colors;
        button.onClick.AddListener(() => action?.Invoke());

        Text label = CreateText(buttonObject.GetComponent<RectTransform>(), "", size.y > 42f ? 20 : 16, Vector2.zero, size, TextAnchor.MiddleCenter, TextColor);
        label.raycastTarget = false;
        SetText(label, name);
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
        text.text = value ?? "";
        return text;
    }

    private static Image CreateImage(string name, RectTransform parent, RectTransformSpec spec, Color color)
    {
        GameObject imageObject = CreateRect(name, parent, spec);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static GameObject CreateRect(string name, Transform parent, RectTransformSpec spec)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMax;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.sizeDelta;
        return gameObject;
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

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? "";
        }
    }

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedDefaultFont;
    }

    private static void EnsureEventSystem(Transform parent)
    {
        EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            ConfigureEventSystemInput(existing.gameObject);
            return;
        }

        GameObject eventSystem = new GameObject(EventSystemName, typeof(EventSystem));
        eventSystem.transform.SetParent(parent, false);
        ConfigureEventSystemInput(eventSystem);
    }

    private static void ConfigureEventSystemInput(GameObject eventSystem)
    {
#if ENABLE_INPUT_SYSTEM
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.AddComponent<StandaloneInputModule>();
        }
#endif
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
