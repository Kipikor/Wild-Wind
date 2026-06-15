using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed class WildWindGameplayMenu : MonoBehaviour
{
    public static readonly string[] RequiredLocalizationKeys =
    {
        "game.menu.title",
        "game.menu.resume",
        "game.menu.reset_progress_cheat",
        "game.menu.status_reset_progress",
        "game.menu.status_reset_failed"
    };

    private const string MenuObjectName = "Wild Wind Gameplay Menu";
    private const string CanvasName = "Gameplay Menu Canvas";
    private const string EventSystemName = "Gameplay Menu EventSystem";

    [SerializeField, InspectorName("Reference Resolution")] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas canvas;
    private RectTransform overlayRoot;
    private RectTransform panel;
    private Text titleText;
    private Text resumeText;
    private Text resetProgressText;
    private Text statusText;
    private MetaGameState meta;
    private static Font cachedDefaultFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallGameplayMenuBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureMenuForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureMenuForGameplayScene();
    }

    private static void EnsureMenuForGameplayScene()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        if (Object.FindFirstObjectByType<WildWindGameplayMenu>() != null)
        {
            return;
        }

        GameObject menuObject = new GameObject(MenuObjectName);
        menuObject.AddComponent<WildWindGameplayMenu>();
    }

    private void Awake()
    {
        meta = Object.FindFirstObjectByType<MetaGameState>();
        BuildUi();
        SetOpen(false);
    }

    private void OnEnable()
    {
        WildWindLocalization.LanguageChanged += RefreshTexts;
    }

    private void OnDisable()
    {
        WildWindLocalization.LanguageChanged -= RefreshTexts;
        SetPaused(false);
    }

    private void Update()
    {
        if (WasResetProgressCheatPressed())
        {
            ResetProgressCheat();
            return;
        }

        if (WasMenuPressed())
        {
            SetOpen(overlayRoot == null || !overlayRoot.gameObject.activeSelf);
        }
    }

    public void SetOpen(bool open)
    {
        if (overlayRoot != null)
        {
            overlayRoot.gameObject.SetActive(open);
        }

        SetPaused(open);
        if (open)
        {
            ClearStatus();
            RefreshTexts();
        }
    }

    private void Resume()
    {
        SetOpen(false);
    }

    private void ResetProgressCheat()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            SetStatus("game.menu.status_reset_failed");
            return;
        }

        if (!currentMeta.ResetAccountProgressForCheat(out string message))
        {
            Debug.LogWarning("[WildWindGameplayMenu] Progress reset failed: " + message, this);
            SetStatus("game.menu.status_reset_failed");
            return;
        }

        SetStatus("game.menu.status_reset_progress");
        SetOpen(false);
    }

    private void SetPaused(bool paused)
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta != null)
        {
            currentMeta.SetSessionPaused(paused);
            return;
        }

        Time.timeScale = paused ? 0f : 1f;
    }

    private MetaGameState ResolveMeta()
    {
        if (meta == null)
        {
            meta = Object.FindFirstObjectByType<MetaGameState>();
        }

        return meta;
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot = CreateRect("Gameplay Menu Overlay", canvas.transform, StretchFull()).GetComponent<RectTransform>();

        GameObject shadeObject = CreateRect("Gameplay Menu Shade", overlayRoot, StretchFull());
        Image shade = shadeObject.AddComponent<Image>();
        shade.color = new Color(0.01f, 0.012f, 0.018f, 0.62f);

        panel = CreateRect("Gameplay Menu Panel", overlayRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(560f, 340f)
        }).GetComponent<RectTransform>();

        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.025f, 0.027f, 0.032f, 0.88f);

        titleText = CreateText(panel, "game.menu.title", 42, new Vector2(42f, -30f), new Vector2(476f, 66f), TextAnchor.MiddleLeft, new Color(0.88f, 0.79f, 0.61f, 1f));
        resumeText = CreateMenuButton(panel, "game.menu.resume", new Vector2(42f, -118f), Resume);
        resetProgressText = CreateMenuButton(panel, "game.menu.reset_progress_cheat", new Vector2(42f, -198f), ResetProgressCheat);
        statusText = CreateText(panel, "", 20, new Vector2(42f, -276f), new Vector2(476f, 40f), TextAnchor.MiddleLeft, new Color(0.86f, 0.75f, 0.55f, 1f));
        RefreshTexts();
    }

    private Text CreateMenuButton(RectTransform parent, string key, Vector2 anchoredPosition, UnityAction action)
    {
        GameObject buttonObject = CreateRect("Button " + key, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = new Vector2(476f, 58f)
        });

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.075f, 0.065f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.075f, 0.065f, 0.92f);
        colors.highlightedColor = new Color(0.23f, 0.18f, 0.10f, 0.98f);
        colors.pressedColor = new Color(0.45f, 0.31f, 0.13f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        Text label = CreateText(buttonObject.GetComponent<RectTransform>(), key, 25, new Vector2(26f, 0f), new Vector2(424f, 58f), TextAnchor.MiddleLeft, new Color(0.91f, 0.82f, 0.64f, 1f));
        label.raycastTarget = false;
        return label;
    }

    private Text CreateText(RectTransform parent, string key, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateRect("Text " + key, parent, new RectTransformSpec
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
            text.text = WildWindLocalization.Get(key);
        }

        return text;
    }

    private void RefreshTexts()
    {
        SetLocalizedText(titleText, "game.menu.title");
        SetLocalizedText(resumeText, "game.menu.resume");
        SetLocalizedText(resetProgressText, "game.menu.reset_progress_cheat");
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

    private static void SetLocalizedText(Text text, string key)
    {
        if (text != null)
        {
            text.text = WildWindLocalization.Get(key);
        }
    }

    private static bool WasMenuPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

        return false;
    }

    private static bool WasResetProgressCheatPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            bool controlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (controlHeld && shiftHeld && keyboard.backspaceKey.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif

        return false;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            ConfigureEventSystemInput(existing.gameObject);
            return;
        }

        GameObject eventSystem = new GameObject(EventSystemName, typeof(EventSystem));
        ConfigureEventSystemInput(eventSystem);
    }

    private static void ConfigureEventSystemInput(GameObject eventSystemObject)
    {
        if (eventSystemObject == null)
        {
            return;
        }

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule standaloneModule = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (standaloneModule != null)
        {
            Destroy(standaloneModule);
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

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedDefaultFont;
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
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMax;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.sizeDelta;
        return gameObject;
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
