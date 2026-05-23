using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public sealed class WildWindStartScreen : MonoBehaviour
{
    public const string BackgroundResourcePath = "UI/WildWindStartBackground";
    private const string CanvasName = "Start Screen Canvas";
    private const string EventSystemName = "Start Screen EventSystem";
    private const string MenuCameraName = "Menu Camera";
    private const string MainMenuName = "Main Menu";
    private const string ContinuePanelName = "Continue Panel";
    private const string SettingsPanelName = "Settings Panel";
    private const string SaveSlotListName = "Save Slot List";
    private const string StatusTextName = "Status Text";

    private static readonly string[] requiredLocalizationKeys =
    {
        "app.title",
        "app.subtitle",
        "menu.new_world",
        "menu.continue",
        "menu.settings",
        "menu.exit",
        "menu.back",
        "menu.start_hint",
        "continue.title",
        "continue.empty",
        "settings.title",
        "settings.language",
        "settings.language_ru",
        "settings.language_en",
        "status.new_world",
        "status.loading_save",
        "status.missing_scene",
        "error.new_world_save_failed",
        "error.no_save_selected",
        "game.menu.title",
        "game.menu.resume",
        "game.menu.save_exit",
        "game.menu.exit_without_save",
        "game.menu.status_saved",
        "game.menu.status_save_failed"
    };

    [Header("Scene flow")]
    public string gameplaySceneName = "WildWindWorldScene";

    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public float panelWidth = 520f;

    private readonly List<Text> localizedTexts = new List<Text>();
    private Canvas canvas;
    private RectTransform menuPanel;
    private RectTransform continuePanel;
    private RectTransform settingsPanel;
    private RectTransform saveSlotListRoot;
    private Text statusText;
    private static Font cachedDefaultFont;

    public static IReadOnlyList<string> RequiredLocalizationKeys => requiredLocalizationKeys;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            BuildRuntime();
        }
    }

    private void OnEnable()
    {
        WildWindLocalization.LanguageChanged += RefreshTexts;
        if (!Application.isPlaying)
        {
            BuildEditableIfMissing();
        }
    }

    private void OnDisable()
    {
        WildWindLocalization.LanguageChanged -= RefreshTexts;
    }

    [ContextMenu("Rebuild editable start screen")]
    public void RebuildEditableScreen()
    {
        ClearGeneratedChildren();
        BuildFresh();
        MarkSceneDirtyIfEditing();
    }

    private void BuildRuntime()
    {
        if (!TryBindExistingScreen())
        {
            ClearGeneratedChildren();
            BuildFresh();
            return;
        }

        EnsureMenuCamera();
        EnsureEventSystem();
        PopulateSaveSlots(saveSlotListRoot);
        BindStaticButtons();
        ShowMenu();
        RefreshTexts();
    }

    private void BuildEditableIfMissing()
    {
        if (FindChildByName(transform, CanvasName) != null)
        {
            if (TryBindExistingScreen())
            {
                RefreshTexts();
                return;
            }

            ClearGeneratedChildren();
        }

        BuildFresh();
        MarkSceneDirtyIfEditing();
    }

    private void BuildFresh()
    {
        localizedTexts.Clear();
        EnsureMenuCamera();
        EnsureEventSystem();
        BuildCanvas();
        BuildBackground();
        BuildMenuPanel();
        BuildContinuePanel();
        BuildSettingsPanel();
        ShowMenu();
        RefreshTexts();
    }

    private bool TryBindExistingScreen()
    {
        localizedTexts.Clear();

        Transform canvasTransform = FindChildByName(transform, CanvasName);
        canvas = canvasTransform != null ? canvasTransform.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            return false;
        }

        menuPanel = GetRect(canvas.transform, MainMenuName);
        continuePanel = GetRect(canvas.transform, ContinuePanelName);
        settingsPanel = GetRect(canvas.transform, SettingsPanelName);
        saveSlotListRoot = GetRect(canvas.transform, SaveSlotListName);
        statusText = GetText(canvas.transform, StatusTextName);

        if (menuPanel == null || continuePanel == null || settingsPanel == null || saveSlotListRoot == null || statusText == null)
        {
            return false;
        }

        WildWindLocalizedText[] localized = canvas.GetComponentsInChildren<WildWindLocalizedText>(true);
        for (int i = 0; i < localized.Length; i++)
        {
            Text text = localized[i].GetComponent<Text>();
            if (text != null)
            {
                localizedTexts.Add(text);
            }
        }

        BindStaticButtons();
        return true;
    }

    private void BindStaticButtons()
    {
        BindButton(menuPanel, "Button menu.new_world", StartNewWorld);
        BindButton(menuPanel, "Button menu.continue", ShowContinue);
        BindButton(menuPanel, "Button menu.settings", ShowSettings);
        BindButton(menuPanel, "Button menu.exit", ExitGame);
        BindButton(continuePanel, "Button menu.back", ShowMenu);
        BindButton(settingsPanel, "Button settings.language_ru", () => WildWindLocalization.SetLanguage(WildWindLanguage.Ru));
        BindButton(settingsPanel, "Button settings.language_en", () => WildWindLocalization.SetLanguage(WildWindLanguage.En));
        BindButton(settingsPanel, "Button menu.back", ShowMenu);
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildBackground()
    {
        Texture2D texture = Resources.Load<Texture2D>(BackgroundResourcePath);
        GameObject backgroundObject = CreateRect("Background", canvas.transform, StretchFull());
        Image image = backgroundObject.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;

        if (texture != null)
        {
            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            image.preserveAspect = false;
        }
        else
        {
            image.color = new Color(0.06f, 0.08f, 0.11f, 1f);
        }

        GameObject shadeObject = CreateRect("Left Shade", canvas.transform, StretchFull());
        Image shade = shadeObject.AddComponent<Image>();
        shade.raycastTarget = false;
        shade.color = new Color(0.02f, 0.025f, 0.035f, 0.42f);
    }

    private void BuildMenuPanel()
    {
        menuPanel = CreatePanel(MainMenuName, new Vector2(panelWidth, 760f), new Vector2(86f, 0f), new Vector2(0f, 0.5f));

        Text title = CreateText(menuPanel, "app.title", 76, new Vector2(0f, -16f), new Vector2(panelWidth - 72f, 92f), TextAnchor.MiddleLeft, new Color(0.88f, 0.79f, 0.61f, 1f));
        title.fontStyle = FontStyle.Bold;
        CreateText(menuPanel, "app.subtitle", 23, new Vector2(0f, -88f), new Vector2(panelWidth - 72f, 44f), TextAnchor.MiddleLeft, new Color(0.74f, 0.69f, 0.58f, 0.95f));

        float y = -190f;
        CreateMenuButton(menuPanel, "menu.new_world", new Vector2(0f, y), StartNewWorld);
        CreateMenuButton(menuPanel, "menu.continue", new Vector2(0f, y - 86f), ShowContinue);
        CreateMenuButton(menuPanel, "menu.settings", new Vector2(0f, y - 172f), ShowSettings);
        CreateMenuButton(menuPanel, "menu.exit", new Vector2(0f, y - 258f), ExitGame);

        statusText = CreateText(menuPanel, "", 20, new Vector2(0f, -558f), new Vector2(panelWidth - 72f, 54f), TextAnchor.MiddleLeft, new Color(0.86f, 0.75f, 0.55f, 1f), StatusTextName);
        statusText.text = "";
    }

    private void BuildContinuePanel()
    {
        continuePanel = CreatePanel(ContinuePanelName, new Vector2(panelWidth, 760f), new Vector2(86f, 0f), new Vector2(0f, 0.5f));
        CreateText(continuePanel, "continue.title", 38, new Vector2(0f, -22f), new Vector2(panelWidth - 72f, 70f), TextAnchor.MiddleLeft, new Color(0.88f, 0.79f, 0.61f, 1f));

        saveSlotListRoot = CreateRect(SaveSlotListName, continuePanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(0f, -118f),
            sizeDelta = new Vector2(panelWidth - 72f, 440f)
        }).GetComponent<RectTransform>();

        PopulateSaveSlots(saveSlotListRoot);
        CreateMenuButton(continuePanel, "menu.back", new Vector2(0f, -608f), ShowMenu);
        continuePanel.gameObject.SetActive(false);
    }

    private void BuildSettingsPanel()
    {
        settingsPanel = CreatePanel(SettingsPanelName, new Vector2(panelWidth, 760f), new Vector2(86f, 0f), new Vector2(0f, 0.5f));
        CreateText(settingsPanel, "settings.title", 38, new Vector2(0f, -22f), new Vector2(panelWidth - 72f, 70f), TextAnchor.MiddleLeft, new Color(0.88f, 0.79f, 0.61f, 1f));
        CreateText(settingsPanel, "settings.language", 24, new Vector2(0f, -126f), new Vector2(panelWidth - 72f, 44f), TextAnchor.MiddleLeft, new Color(0.74f, 0.69f, 0.58f, 0.95f));

        CreateMenuButton(settingsPanel, "settings.language_ru", new Vector2(0f, -202f), () => WildWindLocalization.SetLanguage(WildWindLanguage.Ru));
        CreateMenuButton(settingsPanel, "settings.language_en", new Vector2(0f, -288f), () => WildWindLocalization.SetLanguage(WildWindLanguage.En));
        CreateMenuButton(settingsPanel, "menu.back", new Vector2(0f, -608f), ShowMenu);
        settingsPanel.gameObject.SetActive(false);
    }

    private RectTransform CreatePanel(string name, Vector2 size, Vector2 anchoredPosition, Vector2 anchor)
    {
        GameObject panelObject = CreateRect(name, canvas.transform, new RectTransformSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });

        Image panel = panelObject.AddComponent<Image>();
        panel.color = new Color(0.025f, 0.027f, 0.032f, 0.70f);
        return panelObject.GetComponent<RectTransform>();
    }

    private Button CreateMenuButton(RectTransform parent, string key, Vector2 anchoredPosition, UnityAction action)
    {
        string buttonName = string.IsNullOrWhiteSpace(key) ? "Save Slot Button" : "Button " + key;
        GameObject buttonObject = CreateRect(buttonName, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = new Vector2(panelWidth - 72f, 64f)
        });

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.075f, 0.065f, 0.88f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.075f, 0.065f, 0.88f);
        colors.highlightedColor = new Color(0.23f, 0.18f, 0.10f, 0.95f);
        colors.pressedColor = new Color(0.45f, 0.31f, 0.13f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        Text label = CreateText(buttonObject.GetComponent<RectTransform>(), key, 28, new Vector2(32f, 0f), new Vector2(panelWidth - 136f, 64f), TextAnchor.MiddleLeft, new Color(0.91f, 0.82f, 0.64f, 1f));
        label.raycastTarget = false;
        return button;
    }

    private Text CreateText(RectTransform parent, string key, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color, string objectName = "")
    {
        string resolvedName = string.IsNullOrWhiteSpace(objectName) ? "Text " + key : objectName;
        GameObject textObject = CreateRect(resolvedName, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });

        Text text = textObject.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        if (!string.IsNullOrWhiteSpace(key))
        {
            WildWindLocalizedText localized = textObject.AddComponent<WildWindLocalizedText>();
            localized.key = key;
            localizedTexts.Add(text);
        }

        return text;
    }

    private void PopulateSaveSlots(RectTransform listRoot)
    {
        if (listRoot == null)
        {
            return;
        }

        ClearChildren(listRoot);
        List<WildWindSaveSlotInfo> slots = WildWindSaveSlots.GetExistingSlots();
        if (slots.Count == 0)
        {
            CreateText(listRoot, "continue.empty", 23, Vector2.zero, new Vector2(panelWidth - 72f, 64f), TextAnchor.MiddleLeft, new Color(0.74f, 0.69f, 0.58f, 0.95f));
            return;
        }

        int count = Mathf.Min(slots.Count, 5);
        for (int i = 0; i < count; i++)
        {
            WildWindSaveSlotInfo slot = slots[i];
            Button button = CreateMenuButton(listRoot, "", new Vector2(0f, -i * 78f), () => ContinueFromSlot(slot.fileName));
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = slot.DisplayName;
            }
        }
    }

    private void ShowMenu()
    {
        menuPanel.gameObject.SetActive(true);
        continuePanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        ClearStatus();
    }

    private void ShowContinue()
    {
        menuPanel.gameObject.SetActive(false);
        continuePanel.gameObject.SetActive(true);
        settingsPanel.gameObject.SetActive(false);
    }

    private void ShowSettings()
    {
        menuPanel.gameObject.SetActive(false);
        continuePanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(true);
    }

    private void StartNewWorld()
    {
        SetStatus("status.new_world");
        if (!WildWindSessionFlow.TryCreateNewWorldAndEnter(gameplaySceneName, out _, out _, out string error))
        {
            Debug.LogError("[WildWindStartScreen] New world save failed: " + error, this);
            SetStatus("error.new_world_save_failed");
        }
    }

    private void ContinueFromSlot(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            SetStatus("error.no_save_selected");
            return;
        }

        SetStatus("status.loading_save");
        if (!WildWindSessionFlow.TryContinueWorldAndEnter(fileName, gameplaySceneName, out string error))
        {
            Debug.LogError("[WildWindStartScreen] Continue save failed: " + error, this);
            SetStatus(string.IsNullOrWhiteSpace(gameplaySceneName) ? "status.missing_scene" : "error.no_save_selected");
        }
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetStatus(string key)
    {
        if (statusText != null)
        {
            statusText.text = WildWindLocalization.Get(key);
        }
    }

    private void ClearStatus()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    private void RefreshTexts()
    {
        for (int i = 0; i < localizedTexts.Count; i++)
        {
            WildWindLocalizedText localized = localizedTexts[i] != null ? localizedTexts[i].GetComponent<WildWindLocalizedText>() : null;
            if (localized != null)
            {
                localized.Refresh();
            }
        }
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            ConfigureEventSystemInput(existing.gameObject);
            return;
        }

        GameObject eventSystem = new GameObject(EventSystemName, typeof(EventSystem));
        ConfigureEventSystemInput(eventSystem);
        eventSystem.transform.SetParent(FindFirstObjectByType<WildWindStartScreen>()?.transform, false);
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

    private void EnsureMenuCamera()
    {
        if (FindFirstObjectByType<Camera>() != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject(MenuCameraName, typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
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

    private static RectTransform GetRect(Transform parent, string childName)
    {
        Transform child = FindChildByName(parent, childName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private static Text GetText(Transform parent, string childName)
    {
        Transform child = FindChildByName(parent, childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private static void BindButton(RectTransform parent, string buttonName, UnityAction action)
    {
        if (parent == null)
        {
            return;
        }

        Transform child = FindChildByName(parent, buttonName);
        Button button = child != null ? child.GetComponent<Button>() : null;
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            Transform match = FindChildByName(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(transform.GetChild(i).gameObject);
        }

        canvas = null;
        menuPanel = null;
        continuePanel = null;
        settingsPanel = null;
        saveSlotListRoot = null;
        statusText = null;
        localizedTexts.Clear();
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(parent.GetChild(i).gameObject);
        }
    }

    private static void DestroyGeneratedObject(Object target)
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

    private void MarkSceneDirtyIfEditing()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            return;
        }

        EditorUtility.SetDirty(this);
        if (gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
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
