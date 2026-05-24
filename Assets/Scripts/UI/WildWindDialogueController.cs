using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class WildWindDialogueController : MonoBehaviour
{
    private const string DialogueHomeId = "dialogue_intro_home";
    private const string DialogueAeroliteId = "dialogue_intro_aerolite";
    private const string DialogueCapitalId = "dialogue_intro_capital";
    private const string ControllerName = "Wild Wind Dialogue Controller";
    private const string CanvasName = "Dialogue Canvas";
    private const string EventSystemName = "Dialogue EventSystem";

    private Canvas canvas;
    private RectTransform root;
    private RectTransform panel;
    private RawImage portraitImage;
    private Text speakerText;
    private Text bodyText;
    private Text nextText;
    private Button nextButton;
    private MetaGameState meta;
    private DialogueSequence currentSequence;
    private int currentLineIndex;
    private float nextProbeTime;
    private static Font cachedDefaultFont;
    private static readonly Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();

    private static readonly DialogueSequence HomeSequence = new DialogueSequence
    {
        id = DialogueHomeId,
        requiredDockId = WildWindStarterDelivery.SourceDockId,
        lines = new[]
        {
            new DialogueLine("Отец", "intro_father", "Пионер старый, но до аэролитового острова дотянет. Главное не гони его выше, чем он сам хочет."),
            new DialogueLine("Отец", "intro_father", "Еду уже сложили у причала. Отвезёшь ящики, покажешь людям, что твой автоматин хотя бы курс держать умеет."),
            new DialogueLine("Отец", "intro_father", "Дома тебя не понимают не потому, что ты неправ. Просто фермеры верят рукам больше, чем железкам."),
            new DialogueLine("Отец", "intro_father", "Лети спокойно. А если эта штука сама доведёт корабль до дока, я первый перестану ворчать.")
        }
    };

    private static readonly DialogueSequence AeroliteSequence = new DialogueSequence
    {
        id = DialogueAeroliteId,
        requiredDockId = WildWindStarterDelivery.DestinationDockId,
        lines = new[]
        {
            new DialogueLine("Мира", "intro_aerolite_friend", "Ты всё-таки довёл отцовский Пионер сюда. И автоматин не развалил курс? Уже лучше, чем половина капитанов."),
            new DialogueLine("Мира", "intro_aerolite_friend", "У меня есть заказ в столицу: двенадцать ящиков аэролита. Небольшой груз, зато там бывают люди с настоящими мастерскими."),
            new DialogueLine("Мира", "intro_aerolite_friend", "Найди в столице Рена. Он давно на пенсии, но если кто-то поймёт идею автоматонов, то он."),
            new DialogueLine("Мира", "intro_aerolite_friend", "Загружай аэролит здесь и держи курс на столицу. Дальше тебе нужен не спор, а работающий маршрут.")
        }
    };

    private static readonly DialogueSequence CapitalSequence = new DialogueSequence
    {
        id = DialogueCapitalId,
        requiredDockId = "capital",
        lines = new[]
        {
            new DialogueLine("Рен", "intro_retired_engineer", "Автоматон, который не спорит с ветром, а договаривается с ним? Забавно. Совет инженеров такое не подпишет."),
            new DialogueLine("Рен", "intro_retired_engineer", "Значит, пойдём старым путём: сначала покажем пользу, потом попросим разрешение у тех, кто уже опоздал."),
            new DialogueLine("Рен", "intro_retired_engineer", "Нам нужен треугольник поставок: ферма, аэролитовый остров и столица. Три стареньких Пионера, один понятный поток товаров."),
            new DialogueLine("Рен", "intro_retired_engineer", "Когда автоматоны научатся возить груз без твоих рук на штурвале, мы дадим им руду, облака, разведку и охоту."),
            new DialogueLine("Рен", "intro_retired_engineer", "А пока запомни: твоя первая фабрика будет не на земле. Она будет в маршрутах между островами.")
        }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallDialogueBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureDialogueForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureDialogueForGameplayScene();
    }

    private static void EnsureDialogueForGameplayScene()
    {
        if (Object.FindFirstObjectByType<WorldRegionRuntime>() == null)
        {
            return;
        }

        if (Object.FindFirstObjectByType<WildWindDialogueController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(ControllerName);
        controllerObject.AddComponent<WildWindDialogueController>();
    }

    private void Awake()
    {
        ResolveMeta();
        BuildUi();
        HideDialogue();
    }

    private void Update()
    {
        if (currentSequence != null || Time.unscaledTime < nextProbeTime)
        {
            return;
        }

        nextProbeTime = Time.unscaledTime + 0.35f;
        TryOpenPendingDialogue();
    }

    private void TryOpenPendingDialogue()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return;
        }

        currentMeta.EnsureProgressInitialized();
        PlayerProgress progress = currentMeta.progress;
        DialogueSequence sequence = GetPendingSequence(progress);
        if (sequence == null)
        {
            return;
        }

        currentSequence = sequence;
        currentLineIndex = 0;
        panel.gameObject.SetActive(true);
        RefreshLine();
    }

    private DialogueSequence GetPendingSequence(PlayerProgress progress)
    {
        if (progress == null || progress.currentMode != GameSessionMode.Docked)
        {
            return null;
        }

        if (CanShowSequence(progress, HomeSequence))
        {
            return HomeSequence;
        }

        if (progress.IsMissionCompleted(WildWindStarterDelivery.FirstMissionId) && CanShowSequence(progress, AeroliteSequence))
        {
            return AeroliteSequence;
        }

        if (progress.IsMissionCompleted(WildWindStarterDelivery.SecondMissionId) && CanShowSequence(progress, CapitalSequence))
        {
            return CapitalSequence;
        }

        return null;
    }

    private static bool CanShowSequence(PlayerProgress progress, DialogueSequence sequence)
    {
        return sequence != null &&
            progress != null &&
            progress.currentDockId == sequence.requiredDockId &&
            !progress.IsMissionCompleted(sequence.id);
    }

    private void Advance()
    {
        if (currentSequence == null)
        {
            HideDialogue();
            return;
        }

        currentLineIndex++;
        if (currentLineIndex >= currentSequence.lines.Length)
        {
            FinishSequence();
            return;
        }

        RefreshLine();
    }

    private void FinishSequence()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta != null)
        {
            currentMeta.EnsureProgressInitialized();
            currentMeta.progress?.CompleteMission(currentSequence.id);
        }

        HideDialogue();
    }

    private void RefreshLine()
    {
        if (currentSequence == null || currentSequence.lines == null || currentSequence.lines.Length == 0)
        {
            HideDialogue();
            return;
        }

        DialogueLine line = currentSequence.lines[Mathf.Clamp(currentLineIndex, 0, currentSequence.lines.Length - 1)];
        SetText(speakerText, line.speaker);
        SetText(bodyText, line.text);
        SetText(nextText, "Дальше");

        Texture2D portrait = LoadPortrait(line.portraitResource);
        portraitImage.texture = portrait;
        portraitImage.enabled = portrait != null;
        nextButton.Select();
    }

    private void HideDialogue()
    {
        currentSequence = null;
        currentLineIndex = 0;
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 920;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateRect("Dialogue Root", canvas.transform, StretchFull()).GetComponent<RectTransform>();
        panel = CreatePanel("Dialogue Panel", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 36f),
            sizeDelta = new Vector2(-140f, 250f)
        }, new Color(0.018f, 0.016f, 0.014f, 0.94f));

        GameObject portraitObject = CreateRect("Portrait", panel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(28f, 0f),
            sizeDelta = new Vector2(190f, 190f)
        });
        portraitImage = portraitObject.AddComponent<RawImage>();
        portraitImage.color = Color.white;

        speakerText = CreateText(panel, "", 28, new Vector2(250f, -28f), new Vector2(840f, 42f), TextAnchor.MiddleLeft, new Color(0.95f, 0.80f, 0.50f, 1f));
        bodyText = CreateText(panel, "", 24, new Vector2(250f, -78f), new Vector2(1260f, 100f), TextAnchor.UpperLeft, new Color(0.90f, 0.88f, 0.78f, 1f));
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Truncate;
        nextText = CreateButton(panel, "Next", new Vector2(1510f, -166f), new Vector2(230f, 54f), Advance, out nextButton);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventObject = new GameObject(EventSystemName);
        eventObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventObject.AddComponent<InputSystemUIInputModule>();
#else
        eventObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private MetaGameState ResolveMeta()
    {
        if (meta == null)
        {
            meta = Object.FindFirstObjectByType<MetaGameState>();
        }

        return meta;
    }

    private static RectTransform CreatePanel(string name, RectTransform parent, RectTransformSpec spec, Color color)
    {
        GameObject panelObject = CreateRect(name, parent, spec);
        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return panelObject.GetComponent<RectTransform>();
    }

    private static Text CreateText(RectTransform parent, string text, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor, Color color)
    {
        GameObject textObject = CreateRect("Text", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });

        Text label = textObject.AddComponent<Text>();
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        label.text = text ?? "";
        return label;
    }

    private static Text CreateButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, out Button button)
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
        image.color = new Color(0.12f, 0.095f, 0.055f, 0.96f);

        button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.095f, 0.055f, 0.96f);
        colors.highlightedColor = new Color(0.28f, 0.20f, 0.09f, 0.98f);
        colors.pressedColor = new Color(0.50f, 0.34f, 0.12f, 1f);
        colors.disabledColor = new Color(0.07f, 0.07f, 0.07f, 0.55f);
        button.colors = colors;
        button.onClick.AddListener(action);

        Text label = CreateText(buttonObject.GetComponent<RectTransform>(), "", 22, Vector2.zero, size, TextAnchor.MiddleCenter, new Color(0.94f, 0.84f, 0.62f, 1f));
        return label;
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

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cachedDefaultFont;
    }

    private static Texture2D LoadPortrait(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        if (portraitCache.TryGetValue(resourceName, out Texture2D cached))
        {
            return cached;
        }

        Texture2D portrait = Resources.Load<Texture2D>("Portraits/" + resourceName);
        if (portrait == null)
        {
            portrait = LoadPortraitFromProjectFile(resourceName);
        }

        portraitCache[resourceName] = portrait;
        return portrait;
    }

    private static Texture2D LoadPortraitFromProjectFile(string resourceName)
    {
        string path = Path.Combine(Application.dataPath, "Resources", "Portraits", resourceName + ".png");
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }

            return null;
        }

        texture.name = resourceName;
        return texture;
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? "";
        }
    }

    private struct RectTransformSpec
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }

    private sealed class DialogueSequence
    {
        public string id;
        public string requiredDockId;
        public DialogueLine[] lines;
    }

    private readonly struct DialogueLine
    {
        public readonly string speaker;
        public readonly string portraitResource;
        public readonly string text;

        public DialogueLine(string speaker, string portraitResource, string text)
        {
            this.speaker = speaker;
            this.portraitResource = portraitResource;
            this.text = text;
        }
    }
}
