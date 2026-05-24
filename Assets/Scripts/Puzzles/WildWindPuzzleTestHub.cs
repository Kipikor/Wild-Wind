using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public enum WildWindPuzzleDuration
{
    Quick,
    Medium,
    Long
}

public enum WildWindPuzzleKind
{
    FuseSlot,
    DebrisSweep,
    RelayButtons,
    WireMatch,
    SliderTune,
    GearMount,
    PowerCellService,
    CircuitRotation,
    PressureSequence
}

[Serializable]
public sealed class WildWindPuzzleDefinition
{
    public string id;
    public string titleRu;
    public string subtitleRu;
    public WildWindPuzzleDuration duration;
    public WildWindPuzzleKind kind;
    public Color accent;

    public string DurationNameRu
    {
        get
        {
            switch (duration)
            {
                case WildWindPuzzleDuration.Quick:
                    return "Быстрая";
                case WildWindPuzzleDuration.Medium:
                    return "Средняя";
                case WildWindPuzzleDuration.Long:
                    return "Долгая";
                default:
                    return "Задача";
            }
        }
    }
}

public static class WildWindPuzzleCatalog
{
    public static readonly WildWindPuzzleDefinition[] Definitions =
    {
        new WildWindPuzzleDefinition
        {
            id = "quick_fuse_slot",
            titleRu = "Предохранитель насоса",
            subtitleRu = "Перетащи плавкую вставку в пустой держатель.",
            duration = WildWindPuzzleDuration.Quick,
            kind = WildWindPuzzleKind.FuseSlot,
            accent = new Color(0.20f, 0.88f, 1f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "quick_debris_sweep",
            titleRu = "Снять обломки",
            subtitleRu = "Убери осколки с ремонтной панели.",
            duration = WildWindPuzzleDuration.Quick,
            kind = WildWindPuzzleKind.DebrisSweep,
            accent = new Color(1f, 0.72f, 0.28f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "quick_relay_buttons",
            titleRu = "Запуск реле",
            subtitleRu = "Нажми подсвеченные кнопки в правильном порядке.",
            duration = WildWindPuzzleDuration.Quick,
            kind = WildWindPuzzleKind.RelayButtons,
            accent = new Color(0.55f, 1f, 0.42f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "medium_wire_match",
            titleRu = "Связать жилы",
            subtitleRu = "Соедини цветные жилы с такими же гнездами.",
            duration = WildWindPuzzleDuration.Medium,
            kind = WildWindPuzzleKind.WireMatch,
            accent = new Color(1f, 0.42f, 0.50f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "medium_slider_tune",
            titleRu = "Настроить контуры",
            subtitleRu = "Выставь все ползунки в зеленые зоны.",
            duration = WildWindPuzzleDuration.Medium,
            kind = WildWindPuzzleKind.SliderTune,
            accent = new Color(0.45f, 0.74f, 1f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "medium_gear_mount",
            titleRu = "Поставить шестерни",
            subtitleRu = "Разложи шестерни по подходящим осям.",
            duration = WildWindPuzzleDuration.Medium,
            kind = WildWindPuzzleKind.GearMount,
            accent = new Color(0.92f, 0.72f, 0.36f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "long_power_cell",
            titleRu = "Сервис силовой ячейки",
            subtitleRu = "Открой защелки, вынь старую ячейку, поставь новую и закрой отсек.",
            duration = WildWindPuzzleDuration.Long,
            kind = WildWindPuzzleKind.PowerCellService,
            accent = new Color(0.75f, 0.94f, 1f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "long_circuit_rotation",
            titleRu = "Развернуть цепь",
            subtitleRu = "Поворачивай сегменты, пока все стрелки не совпадут с метками.",
            duration = WildWindPuzzleDuration.Long,
            kind = WildWindPuzzleKind.CircuitRotation,
            accent = new Color(0.72f, 0.54f, 1f, 1f)
        },
        new WildWindPuzzleDefinition
        {
            id = "long_pressure_sequence",
            titleRu = "Стабилизировать давление",
            subtitleRu = "Поверни клапаны и выровняй две шкалы в рабочий диапазон.",
            duration = WildWindPuzzleDuration.Long,
            kind = WildWindPuzzleKind.PressureSequence,
            accent = new Color(0.62f, 1f, 0.74f, 1f)
        }
    };

    public static WildWindPuzzleDefinition GetRandom()
    {
        if (Definitions.Length == 0)
        {
            return null;
        }

        return Definitions[UnityEngine.Random.Range(0, Definitions.Length)];
    }
}

[DisallowMultipleComponent]
public sealed class WildWindPuzzleTestHub : MonoBehaviour
{
    private const string CanvasName = "Puzzle Test Canvas";
    private const string EventSystemName = "Puzzle Test EventSystem";

    [SerializeField, InspectorName("Reference Resolution")] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas canvas;
    private RectTransform root;
    private RectTransform puzzleHost;
    private Text statusText;
    private WildWindPuzzleRunner runner;

    private void Awake()
    {
        BuildUi();
    }

    public void BuildUi()
    {
        WildWindPuzzleUi.ClearChildren(transform);
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = WildWindPuzzleUi.CreateRect("Puzzle Test Root", canvas.transform, WildWindPuzzleUi.StretchFull()).GetComponent<RectTransform>();
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.030f, 0.034f, 1f);

        BuildBackgroundProps(root);
        BuildHeader(root);
        BuildMenu(root);
        BuildPuzzleHost(root);

        runner = gameObject.GetComponent<WildWindPuzzleRunner>();
        if (runner == null)
        {
            runner = gameObject.AddComponent<WildWindPuzzleRunner>();
        }

        runner.Configure(puzzleHost, canvas, statusText);
    }

    private void BuildBackgroundProps(RectTransform parent)
    {
        CreatePipe(parent, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.72f), 16f, new Color(0.12f, 0.17f, 0.18f, 1f));
        CreatePipe(parent, new Vector2(0.18f, 0.10f), new Vector2(0.18f, 0.92f), 12f, new Color(0.09f, 0.14f, 0.16f, 1f));
        CreatePipe(parent, new Vector2(0.72f, 0.08f), new Vector2(0.72f, 0.92f), 10f, new Color(0.13f, 0.12f, 0.10f, 1f));

        CreateProp(parent, "Soft Coil A", new Vector2(0.87f, 0.17f), new Vector2(240f, 240f), new Color(0.05f, 0.08f, 0.09f, 0.94f), "◎");
        CreateProp(parent, "Soft Coil B", new Vector2(0.37f, 0.88f), new Vector2(170f, 170f), new Color(0.10f, 0.07f, 0.045f, 0.86f), "◌");
        CreateProp(parent, "Aero Relay", new Vector2(0.93f, 0.82f), new Vector2(190f, 120f), new Color(0.08f, 0.12f, 0.13f, 0.90f), "II");
    }

    private static void BuildHeader(RectTransform parent)
    {
        RectTransform header = WildWindPuzzleUi.CreateRect("Header", parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 92f)
        }).GetComponent<RectTransform>();
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color(0.035f, 0.042f, 0.045f, 0.94f);

        WildWindPuzzleUi.CreateText(header, "Wild Wind: ремонтные мини-задачи", 34, new Vector2(34f, -18f), new Vector2(780f, 48f), TextAnchor.MiddleLeft, new Color(0.93f, 0.84f, 0.66f, 1f));
        WildWindPuzzleUi.CreateText(header, "3 быстрые / 3 средние / 3 долгие / случайный запуск", 20, new Vector2(36f, -58f), new Vector2(780f, 30f), TextAnchor.MiddleLeft, new Color(0.58f, 0.70f, 0.72f, 1f));
    }

    private void BuildMenu(RectTransform parent)
    {
        RectTransform panel = WildWindPuzzleUi.CreateRect("Puzzle List Panel", parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(28f, -34f),
            sizeDelta = new Vector2(390f, -130f)
        }).GetComponent<RectTransform>();
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.045f, 0.052f, 0.055f, 0.94f);

        WildWindPuzzleUi.CreateText(panel, "Тестовый список", 26, new Vector2(24f, -22f), new Vector2(330f, 38f), TextAnchor.MiddleLeft, new Color(0.88f, 0.78f, 0.58f, 1f));

        float y = -78f;
        for (int i = 0; i < WildWindPuzzleCatalog.Definitions.Length; i++)
        {
            WildWindPuzzleDefinition definition = WildWindPuzzleCatalog.Definitions[i];
            string label = (i + 1).ToString("00") + "  " + definition.DurationNameRu + "  " + definition.titleRu;
            Text buttonText = WildWindPuzzleUi.CreateButton(panel, "Puzzle Button " + definition.id, label, new Vector2(22f, y), new Vector2(346f, 54f), () => runner.StartPuzzle(definition), definition.accent);
            buttonText.fontSize = 17;
            y -= 62f;
        }

        Text randomText = WildWindPuzzleUi.CreateButton(panel, "Puzzle Button Random", "10  Случайная задача", new Vector2(22f, y - 8f), new Vector2(346f, 58f), StartRandomPuzzle, new Color(1f, 0.86f, 0.38f, 1f));
        randomText.fontSize = 18;
    }

    private void BuildPuzzleHost(RectTransform parent)
    {
        RectTransform panel = WildWindPuzzleUi.CreateRect("Puzzle Host Panel", parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = new Vector2(226f, -34f),
            sizeDelta = new Vector2(-500f, -130f)
        }).GetComponent<RectTransform>();
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.018f, 0.021f, 0.023f, 0.72f);

        puzzleHost = WildWindPuzzleUi.CreateRect("Puzzle Host", panel, WildWindPuzzleUi.StretchFull(new Vector2(24f, 72f), new Vector2(24f, 24f))).GetComponent<RectTransform>();
        statusText = WildWindPuzzleUi.CreateText(panel, "Выбери задачу слева.", 22, new Vector2(28f, -26f), new Vector2(720f, 36f), TextAnchor.MiddleLeft, new Color(0.66f, 0.82f, 0.82f, 1f));
    }

    private void StartRandomPuzzle()
    {
        WildWindPuzzleDefinition definition = WildWindPuzzleCatalog.GetRandom();
        if (definition != null)
        {
            runner.StartPuzzle(definition);
        }
    }

    private static void CreatePipe(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float width, Color color)
    {
        RectTransform pipe = WildWindPuzzleUi.CreateRect("Background Pipe", parent, new WildWindRectSpec
        {
            anchorMin = anchorMin,
            anchorMax = anchorMax,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(width, width)
        }).GetComponent<RectTransform>();
        Image image = pipe.gameObject.AddComponent<Image>();
        image.color = color;
    }

    private static void CreateProp(RectTransform parent, string name, Vector2 anchor, Vector2 size, Color color, string glyph)
    {
        RectTransform prop = WildWindPuzzleUi.CreateRect(name, parent, new WildWindRectSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = size
        }).GetComponent<RectTransform>();
        Image image = prop.gameObject.AddComponent<Image>();
        image.color = color;
        WildWindPuzzleUi.CreateText(prop, glyph, 58, Vector2.zero, size, TextAnchor.MiddleCenter, new Color(0.24f, 0.34f, 0.36f, 0.58f));
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(EventSystemName, typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}

public sealed class WildWindPuzzleRunner : MonoBehaviour
{
    private RectTransform host;
    private Canvas canvas;
    private Text statusText;
    private WildWindPuzzleDefinition current;
    private bool completed;
    private readonly List<Button> currentButtons = new List<Button>();

    public void Configure(RectTransform newHost, Canvas newCanvas, Text newStatusText)
    {
        host = newHost;
        canvas = newCanvas;
        statusText = newStatusText;
        ShowWelcome();
    }

    public void StartPuzzle(WildWindPuzzleDefinition definition)
    {
        if (definition == null || host == null)
        {
            return;
        }

        current = definition;
        completed = false;
        currentButtons.Clear();
        WildWindPuzzleUi.ClearChildren(host);
        SetStatus(definition.DurationNameRu + ": " + definition.titleRu);

        RectTransform stage = CreatePuzzleFrame(definition);
        switch (definition.kind)
        {
            case WildWindPuzzleKind.FuseSlot:
                BuildFuseSlot(stage, definition);
                break;
            case WildWindPuzzleKind.DebrisSweep:
                BuildDebrisSweep(stage, definition);
                break;
            case WildWindPuzzleKind.RelayButtons:
                BuildRelayButtons(stage, definition);
                break;
            case WildWindPuzzleKind.WireMatch:
                BuildWireMatch(stage, definition);
                break;
            case WildWindPuzzleKind.SliderTune:
                BuildSliderTune(stage, definition);
                break;
            case WildWindPuzzleKind.GearMount:
                BuildGearMount(stage, definition);
                break;
            case WildWindPuzzleKind.PowerCellService:
                BuildPowerCellService(stage, definition);
                break;
            case WildWindPuzzleKind.CircuitRotation:
                BuildCircuitRotation(stage, definition);
                break;
            case WildWindPuzzleKind.PressureSequence:
                BuildPressureSequence(stage, definition);
                break;
        }
    }

    private void ShowWelcome()
    {
        if (host == null)
        {
            return;
        }

        WildWindPuzzleUi.ClearChildren(host);
        RectTransform card = WildWindPuzzleUi.CreateRect("Welcome Card", host, new WildWindRectSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(760f, 360f)
        }).GetComponent<RectTransform>();
        Image image = card.gameObject.AddComponent<Image>();
        image.color = new Color(0.055f, 0.063f, 0.067f, 0.95f);
        WildWindPuzzleUi.CreateText(card, "Движок ремонтных задач", 38, new Vector2(36f, -34f), new Vector2(690f, 60f), TextAnchor.MiddleLeft, new Color(0.94f, 0.82f, 0.58f, 1f));
        WildWindPuzzleUi.CreateText(card, "Здесь все задачи имеют только один полезный исход: выполнено. Закрыть или бросить задачу можно будет позже без отдельного провала.", 22, new Vector2(40f, -112f), new Vector2(680f, 132f), TextAnchor.UpperLeft, new Color(0.70f, 0.82f, 0.82f, 1f));
        WildWindPuzzleUi.CreateText(card, "Слева 9 задач и десятая кнопка случайного запуска.", 22, new Vector2(40f, -258f), new Vector2(680f, 48f), TextAnchor.MiddleLeft, new Color(0.78f, 0.70f, 0.54f, 1f));
    }

    private RectTransform CreatePuzzleFrame(WildWindPuzzleDefinition definition)
    {
        RectTransform frame = WildWindPuzzleUi.CreateRect("Puzzle Frame " + definition.id, host, WildWindPuzzleUi.StretchFull()).GetComponent<RectTransform>();
        Image image = frame.gameObject.AddComponent<Image>();
        image.color = new Color(0.040f, 0.046f, 0.050f, 0.96f);

        RectTransform accentBar = WildWindPuzzleUi.CreateRect("Accent Bar", frame, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 8f)
        }).GetComponent<RectTransform>();
        Image accent = accentBar.gameObject.AddComponent<Image>();
        accent.color = definition.accent;

        WildWindPuzzleUi.CreateText(frame, definition.titleRu, 34, new Vector2(28f, -24f), new Vector2(700f, 54f), TextAnchor.MiddleLeft, new Color(0.94f, 0.86f, 0.68f, 1f));
        WildWindPuzzleUi.CreateAnchoredText(frame, definition.DurationNameRu, 20, new Vector2(-30f, -30f), new Vector2(160f, 40f), TextAnchor.MiddleCenter, definition.accent, new Vector2(1f, 1f), new Vector2(1f, 1f));
        WildWindPuzzleUi.CreateText(frame, definition.subtitleRu, 20, new Vector2(30f, -76f), new Vector2(860f, 42f), TextAnchor.MiddleLeft, new Color(0.67f, 0.80f, 0.80f, 1f));

        RectTransform stage = WildWindPuzzleUi.CreateRect("Puzzle Stage", frame, WildWindPuzzleUi.StretchFull(new Vector2(30f, 132f), new Vector2(30f, 30f))).GetComponent<RectTransform>();
        Image stageImage = stage.gameObject.AddComponent<Image>();
        stageImage.color = new Color(0.018f, 0.021f, 0.022f, 0.72f);
        return stage;
    }

    private void CompletePuzzle()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        SetStatus("Выполнено: " + (current != null ? current.titleRu : "задача"));
        for (int i = 0; i < currentButtons.Count; i++)
        {
            if (currentButtons[i] != null)
            {
                currentButtons[i].interactable = false;
            }
        }

        RectTransform badge = WildWindPuzzleUi.CreateRect("Completed Badge", host, new WildWindRectSpec
        {
            anchorMin = new Vector2(1f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(1f, 0f),
            anchoredPosition = new Vector2(-38f, 38f),
            sizeDelta = new Vector2(260f, 70f)
        }).GetComponent<RectTransform>();
        Image image = badge.gameObject.AddComponent<Image>();
        image.color = new Color(0.10f, 0.28f, 0.18f, 0.96f);
        WildWindPuzzleUi.CreateText(badge, "Выполнено", 28, Vector2.zero, new Vector2(240f, 58f), TextAnchor.MiddleCenter, new Color(0.67f, 1f, 0.66f, 1f));
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    private void BuildFuseSlot(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Pump Relay Board", definition.accent);
        RectTransform slot = CreateSocket(board, "ПУСТОЙ ДЕРЖАТЕЛЬ", new Vector2(180f, -48f), new Vector2(250f, 170f), new Color(0.08f, 0.12f, 0.13f, 1f));
        CreateSocket(board, "ПИТАНИЕ", new Vector2(-160f, -48f), new Vector2(250f, 170f), new Color(0.07f, 0.09f, 0.10f, 1f));

        RectTransform fuse = CreateToken(board, "ПЛАВКАЯ\nВСТАВКА", new Vector2(-160f, -260f), new Vector2(220f, 82f), definition.accent);
        WildWindPuzzleDrag drag = fuse.gameObject.AddComponent<WildWindPuzzleDrag>();
        drag.Configure(canvas, "fuse");
        drag.Ended += (_, eventData) =>
        {
            if (WildWindPuzzleUi.ContainsPointer(slot, eventData))
            {
                drag.SnapTo(slot);
                CompletePuzzle();
            }
            else
            {
                drag.ReturnHome();
            }
        };
    }

    private void BuildDebrisSweep(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Cracked Panel", definition.accent);
        WildWindPuzzleUi.CreateCenteredText(board, "Щелкай по осколкам, пока панель не освободится.", 22, new Vector2(0f, -270f), new Vector2(760f, 44f), TextAnchor.MiddleCenter, new Color(0.70f, 0.82f, 0.82f, 1f));

        int remaining = 6;
        Vector2[] positions =
        {
            new Vector2(-270f, -60f),
            new Vector2(-128f, 34f),
            new Vector2(42f, -112f),
            new Vector2(198f, 58f),
            new Vector2(302f, -88f),
            new Vector2(-18f, 82f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Text shardText = WildWindPuzzleUi.CreateCenteredButton(board, "Shard " + i, "◆", positions[i], new Vector2(82f, 70f), null, definition.accent);
            Button shardButton = shardText.GetComponentInParent<Button>();
            currentButtons.Add(shardButton);
            shardButton.onClick.RemoveAllListeners();
            shardButton.onClick.AddListener(() =>
            {
                shardButton.gameObject.SetActive(false);
                remaining--;
                if (remaining <= 0)
                {
                    CompletePuzzle();
                }
            });
        }
    }

    private void BuildRelayButtons(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Relay Start Board", definition.accent);
        int[] sequence = { 2, 4, 1, 3 };
        int index = 0;
        Text hint = WildWindPuzzleUi.CreateCenteredText(board, "Порядок: 2 -> 4 -> 1 -> 3", 26, new Vector2(0f, -250f), new Vector2(720f, 52f), TextAnchor.MiddleCenter, definition.accent);

        for (int i = 1; i <= 4; i++)
        {
            int value = i;
            Vector2 position = new Vector2(-255f + (i - 1) * 170f, -56f);
            Text text = WildWindPuzzleUi.CreateCenteredButton(board, "Relay Button " + i, i.ToString(), position, new Vector2(110f, 110f), null, definition.accent);
            text.fontSize = 40;
            Button button = text.GetComponentInParent<Button>();
            currentButtons.Add(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (value == sequence[index])
                {
                    index++;
                    hint.text = index >= sequence.Length ? "Реле запущено." : "Верно. Следующая кнопка: " + sequence[index];
                    if (index >= sequence.Length)
                    {
                        CompletePuzzle();
                    }
                    return;
                }

                index = 0;
                hint.text = "Сброс. Начни снова: " + sequence[0];
            });
        }
    }

    private void BuildWireMatch(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Wire Harness", definition.accent);
        Color[] colors =
        {
            new Color(0.95f, 0.22f, 0.26f, 1f),
            new Color(0.24f, 0.62f, 1f, 1f),
            new Color(1f, 0.84f, 0.24f, 1f),
            new Color(0.38f, 1f, 0.48f, 1f)
        };
        string[] ids = { "R", "B", "Y", "G" };
        int connected = 0;
        Dictionary<string, RectTransform> sockets = new Dictionary<string, RectTransform>();

        for (int i = 0; i < ids.Length; i++)
        {
            float y = 82f - i * 92f;
            Vector2 sourceLineStart = new Vector2(-310f, y);
            CreateSocket(board, ids[i], new Vector2(-310f, y), new Vector2(120f, 58f), colors[i] * 0.72f);
            RectTransform socket = CreateSocket(board, ids[i], new Vector2(310f, -102f + i * 92f), new Vector2(126f, 62f), colors[i] * 0.46f);
            sockets[ids[i]] = socket;

            RectTransform plug = CreateToken(board, ids[i], new Vector2(-170f, y), new Vector2(92f, 54f), colors[i]);
            WildWindPuzzleDrag drag = plug.gameObject.AddComponent<WildWindPuzzleDrag>();
            drag.Configure(canvas, ids[i]);
            drag.Ended += (item, eventData) =>
            {
                RectTransform target = sockets[item.ItemId];
                if (!WildWindPuzzleUi.ContainsPointer(target, eventData))
                {
                    item.ReturnHome();
                    return;
                }

                item.SnapTo(target);
                item.Locked = true;
                CreateWireLine(board, sourceLineStart, target.anchoredPosition, item.GetComponent<Image>().color);
                connected++;
                if (connected >= ids.Length)
                {
                    CompletePuzzle();
                }
            };
        }
    }

    private void BuildSliderTune(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Claudium Loop Tuning", definition.accent);
        float[] centers = { 0.24f, 0.58f, 0.82f };
        float[] initial = { 0.82f, 0.12f, 0.42f };
        Slider[] sliders = new Slider[centers.Length];

        Action check = () =>
        {
            for (int i = 0; i < sliders.Length; i++)
            {
                if (Mathf.Abs(sliders[i].value - centers[i]) > 0.06f)
                {
                    return;
                }
            }

            CompletePuzzle();
        };

        for (int i = 0; i < centers.Length; i++)
        {
            WildWindPuzzleUi.CreateCenteredText(board, "Контур " + (i + 1), 22, new Vector2(-330f, 80f - i * 120f), new Vector2(180f, 40f), TextAnchor.MiddleLeft, new Color(0.70f, 0.82f, 0.82f, 1f));
            sliders[i] = CreateSlider(board, new Vector2(60f, 80f - i * 120f), centers[i], initial[i], definition.accent);
            sliders[i].onValueChanged.AddListener(_ => check());
        }
    }

    private void BuildGearMount(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Reduction Gearbox", definition.accent);
        string[] ids = { "A", "B", "C" };
        int placed = 0;
        Dictionary<string, RectTransform> axles = new Dictionary<string, RectTransform>();

        for (int i = 0; i < ids.Length; i++)
        {
            RectTransform axle = CreateSocket(board, "ОСЬ " + ids[i], new Vector2(140f + i * 170f, -36f), new Vector2(126f, 126f), new Color(0.09f, 0.11f, 0.12f, 1f));
            axles[ids[i]] = axle;

            RectTransform gear = CreateToken(board, "⚙\n" + ids[i], new Vector2(-330f, 88f - i * 126f), new Vector2(120f, 100f), definition.accent);
            WildWindPuzzleDrag drag = gear.gameObject.AddComponent<WildWindPuzzleDrag>();
            drag.Configure(canvas, ids[i]);
            drag.Ended += (item, eventData) =>
            {
                RectTransform target = axles[item.ItemId];
                if (WildWindPuzzleUi.ContainsPointer(target, eventData))
                {
                    item.SnapTo(target);
                    item.Locked = true;
                    placed++;
                    if (placed >= ids.Length)
                    {
                        CompletePuzzle();
                    }
                }
                else
                {
                    item.ReturnHome();
                }
            };
        }
    }

    private void BuildPowerCellService(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Power Cell Service", definition.accent);
        Text instruction = WildWindPuzzleUi.CreateCenteredText(board, "1. Открой три защелки.", 24, new Vector2(0f, -268f), new Vector2(800f, 48f), TextAnchor.MiddleCenter, new Color(0.72f, 0.86f, 0.86f, 1f));
        RectTransform bay = CreateSocket(board, "ОТСЕК", new Vector2(72f, -42f), new Vector2(270f, 180f), new Color(0.08f, 0.11f, 0.12f, 1f));
        RectTransform waste = CreateSocket(board, "СБРОС", new Vector2(372f, -42f), new Vector2(150f, 150f), new Color(0.13f, 0.08f, 0.08f, 1f));

        int latches = 0;
        RectTransform oldCell = CreateToken(board, "СТАРАЯ\nЯЧЕЙКА", new Vector2(72f, -42f), new Vector2(210f, 90f), new Color(0.64f, 0.28f, 0.22f, 1f));
        RectTransform newCell = CreateToken(board, "НОВАЯ\nЯЧЕЙКА", new Vector2(-330f, -188f), new Vector2(210f, 90f), definition.accent);
        oldCell.gameObject.SetActive(false);
        newCell.gameObject.SetActive(false);

        for (int i = 0; i < 3; i++)
        {
            Text latchText = WildWindPuzzleUi.CreateCenteredButton(board, "Latch " + i, "ЗАЩЕЛКА", new Vector2(-330f, 98f - i * 86f), new Vector2(170f, 54f), null, definition.accent);
            Button latch = latchText.GetComponentInParent<Button>();
            currentButtons.Add(latch);
            latch.onClick.RemoveAllListeners();
            latch.onClick.AddListener(() =>
            {
                latch.interactable = false;
                latchText.text = "ОТКРЫТО";
                latches++;
                if (latches >= 3)
                {
                    oldCell.gameObject.SetActive(true);
                    instruction.text = "2. Перетащи старую ячейку в сброс.";
                }
            });
        }

        WildWindPuzzleDrag oldDrag = oldCell.gameObject.AddComponent<WildWindPuzzleDrag>();
        oldDrag.Configure(canvas, "old");
        oldDrag.Ended += (item, eventData) =>
        {
            if (latches < 3 || !WildWindPuzzleUi.ContainsPointer(waste, eventData))
            {
                item.ReturnHome();
                return;
            }

            item.gameObject.SetActive(false);
            newCell.gameObject.SetActive(true);
            instruction.text = "3. Поставь новую ячейку в отсек.";
        };

        WildWindPuzzleDrag newDrag = newCell.gameObject.AddComponent<WildWindPuzzleDrag>();
        newDrag.Configure(canvas, "new");
        newDrag.Ended += (item, eventData) =>
        {
            if (!WildWindPuzzleUi.ContainsPointer(bay, eventData))
            {
                item.ReturnHome();
                return;
            }

            item.SnapTo(bay);
            item.Locked = true;
            instruction.text = "4. Запечатай отсек.";
            Text sealText = WildWindPuzzleUi.CreateCenteredButton(board, "Seal Button", "ЗАПЕЧАТАТЬ", new Vector2(0f, -202f), new Vector2(250f, 62f), CompletePuzzle, definition.accent);
            currentButtons.Add(sealText.GetComponentInParent<Button>());
        };
    }

    private void BuildCircuitRotation(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Circuit Rotation", definition.accent);
        int[] target = { 0, 1, 1, 0, 2, 1, 3, 2, 0 };
        int[] currentState = { 1, 3, 0, 2, 0, 3, 1, 0, 2 };
        Text[] labels = new Text[target.Length];
        string[] arrows = { "↑", "→", "↓", "←" };

        Action check = () =>
        {
            for (int i = 0; i < target.Length; i++)
            {
                if (currentState[i] != target[i])
                {
                    return;
                }
            }

            CompletePuzzle();
        };

        for (int i = 0; i < 9; i++)
        {
            int index = i;
            int x = i % 3;
            int y = i / 3;
            Vector2 position = new Vector2(-150f + x * 150f, 90f - y * 132f);
            Text tileText = WildWindPuzzleUi.CreateCenteredButton(board, "Circuit Tile " + i, arrows[currentState[i]], position, new Vector2(118f, 104f), null, definition.accent);
            tileText.fontSize = 40;
            labels[i] = tileText;
            Button button = tileText.GetComponentInParent<Button>();
            currentButtons.Add(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                currentState[index] = (currentState[index] + 1) % 4;
                labels[index].text = arrows[currentState[index]];
                check();
            });

            WildWindPuzzleUi.CreateCenteredText(board, arrows[target[i]], 16, position + new Vector2(42f, -38f), new Vector2(38f, 26f), TextAnchor.MiddleCenter, new Color(0.54f, 0.68f, 0.68f, 1f));
        }

        WildWindPuzzleUi.CreateCenteredText(board, "Маленькая метка в углу показывает нужное направление.", 20, new Vector2(0f, -278f), new Vector2(760f, 40f), TextAnchor.MiddleCenter, new Color(0.66f, 0.80f, 0.80f, 1f));
    }

    private void BuildPressureSequence(RectTransform stage, WildWindPuzzleDefinition definition)
    {
        RectTransform board = CreateMachinePlate(stage, "Pressure Stabilizer", definition.accent);
        int[] valveState = { 0, 2, 1 };
        int[] valveTarget = { 2, 1, 3 };
        Text[] valveLabels = new Text[3];
        Slider main = CreateSlider(board, new Vector2(60f, -118f), 0.42f, 0.82f, definition.accent);
        Slider bleed = CreateSlider(board, new Vector2(60f, -210f), 0.70f, 0.16f, definition.accent);
        WildWindPuzzleUi.CreateCenteredText(board, "Главная шкала", 20, new Vector2(-325f, -118f), new Vector2(180f, 34f), TextAnchor.MiddleLeft, new Color(0.70f, 0.82f, 0.82f, 1f));
        WildWindPuzzleUi.CreateCenteredText(board, "Сброс", 20, new Vector2(-325f, -210f), new Vector2(180f, 34f), TextAnchor.MiddleLeft, new Color(0.70f, 0.82f, 0.82f, 1f));

        Action check = () =>
        {
            for (int i = 0; i < valveState.Length; i++)
            {
                if (valveState[i] != valveTarget[i])
                {
                    return;
                }
            }

            if (Mathf.Abs(main.value - 0.42f) <= 0.055f && Mathf.Abs(bleed.value - 0.70f) <= 0.055f)
            {
                CompletePuzzle();
            }
        };

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            Vector2 position = new Vector2(-230f + i * 230f, 68f);
            Text text = WildWindPuzzleUi.CreateCenteredButton(board, "Valve " + i, FormatValve(valveState[i]), position, new Vector2(148f, 96f), null, definition.accent);
            valveLabels[i] = text;
            Button button = text.GetComponentInParent<Button>();
            currentButtons.Add(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                valveState[index] = (valveState[index] + 1) % 4;
                valveLabels[index].text = FormatValve(valveState[index]);
                check();
            });
            WildWindPuzzleUi.CreateCenteredText(board, "цель " + FormatValve(valveTarget[i]), 17, position + new Vector2(0f, -74f), new Vector2(148f, 28f), TextAnchor.MiddleCenter, new Color(0.58f, 0.72f, 0.72f, 1f));
        }

        main.onValueChanged.AddListener(_ => check());
        bleed.onValueChanged.AddListener(_ => check());
    }

    private static string FormatValve(int value)
    {
        switch (value)
        {
            case 0:
                return "0°";
            case 1:
                return "90°";
            case 2:
                return "180°";
            default:
                return "270°";
        }
    }

    private static RectTransform CreateMachinePlate(RectTransform stage, string name, Color accent)
    {
        RectTransform board = WildWindPuzzleUi.CreateRect(name, stage, new WildWindRectSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(930f, 590f)
        }).GetComponent<RectTransform>();
        Image image = board.gameObject.AddComponent<Image>();
        image.color = new Color(0.055f, 0.062f, 0.064f, 0.96f);

        RectTransform glow = WildWindPuzzleUi.CreateRect("Panel Accent", board, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 5f)
        }).GetComponent<RectTransform>();
        Image glowImage = glow.gameObject.AddComponent<Image>();
        glowImage.color = accent;
        return board;
    }

    private static RectTransform CreateSocket(RectTransform parent, string label, Vector2 position, Vector2 size, Color color)
    {
        RectTransform socket = WildWindPuzzleUi.CreateRect("Socket " + label, parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = position,
            sizeDelta = size
        }).GetComponent<RectTransform>();
        Image image = socket.gameObject.AddComponent<Image>();
        image.color = color;
        WildWindPuzzleUi.CreateText(socket, label, 18, Vector2.zero, size - new Vector2(12f, 12f), TextAnchor.MiddleCenter, new Color(0.64f, 0.78f, 0.78f, 1f));
        return socket;
    }

    private static RectTransform CreateToken(RectTransform parent, string label, Vector2 position, Vector2 size, Color color)
    {
        RectTransform token = WildWindPuzzleUi.CreateRect("Token " + label, parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = position,
            sizeDelta = size
        }).GetComponent<RectTransform>();
        Image image = token.gameObject.AddComponent<Image>();
        image.color = color;
        WildWindPuzzleUi.CreateText(token, label, 18, Vector2.zero, size - new Vector2(10f, 8f), TextAnchor.MiddleCenter, new Color(0.025f, 0.030f, 0.032f, 1f));
        return token;
    }

    private Slider CreateSlider(RectTransform parent, Vector2 position, float target, float initial, Color accent)
    {
        RectTransform root = WildWindPuzzleUi.CreateRect("Tune Slider", parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = position,
            sizeDelta = new Vector2(540f, 54f)
        }).GetComponent<RectTransform>();

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform background = WildWindPuzzleUi.CreateRect("Background", root, WildWindPuzzleUi.StretchFull(new Vector2(0f, 18f), new Vector2(0f, 18f))).GetComponent<RectTransform>();
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.07f, 0.08f, 0.085f, 1f);

        RectTransform targetZone = WildWindPuzzleUi.CreateRect("Target Zone", root, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = new Vector2(Mathf.Lerp(0f, 540f, target), 0f),
            sizeDelta = new Vector2(62f, 30f)
        }).GetComponent<RectTransform>();
        Image targetImage = targetZone.gameObject.AddComponent<Image>();
        targetImage.color = new Color(0.28f, 1f, 0.38f, 0.70f);

        RectTransform fillArea = WildWindPuzzleUi.CreateRect("Fill Area", root, WildWindPuzzleUi.StretchFull(new Vector2(0f, 20f), new Vector2(0f, 20f))).GetComponent<RectTransform>();
        RectTransform fill = WildWindPuzzleUi.CreateRect("Fill", fillArea, WildWindPuzzleUi.StretchFull()).GetComponent<RectTransform>();
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = accent;

        RectTransform handle = WildWindPuzzleUi.CreateRect("Handle", root, new WildWindRectSpec
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(34f, 50f)
        }).GetComponent<RectTransform>();
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.84f, 0.62f, 1f);

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.value = initial;
        return slider;
    }

    private static void CreateWireLine(RectTransform parent, Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        RectTransform line = WildWindPuzzleUi.CreateRect("Connected Wire", parent, new WildWindRectSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = from + delta * 0.5f,
            sizeDelta = new Vector2(delta.magnitude, 10f)
        }).GetComponent<RectTransform>();
        line.SetAsFirstSibling();
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        Image image = line.gameObject.AddComponent<Image>();
        image.color = color;
    }
}

public sealed class WildWindPuzzleDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Canvas canvas;
    private Vector2 homePosition;

    public event Action<WildWindPuzzleDrag, PointerEventData> Ended;

    public RectTransform RectTransform { get; private set; }
    public string ItemId { get; private set; }
    public bool Locked { get; set; }

    public void Configure(Canvas parentCanvas, string itemId)
    {
        canvas = parentCanvas;
        ItemId = itemId;
        RectTransform = GetComponent<RectTransform>();
        homePosition = RectTransform != null ? RectTransform.anchoredPosition : Vector2.zero;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Locked || RectTransform == null)
        {
            return;
        }

        RectTransform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Locked || RectTransform == null)
        {
            return;
        }

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        RectTransform.anchoredPosition += eventData.delta / Mathf.Max(0.01f, scale);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Locked)
        {
            return;
        }

        Ended?.Invoke(this, eventData);
    }

    public void ReturnHome()
    {
        if (RectTransform != null)
        {
            RectTransform.anchoredPosition = homePosition;
        }
    }

    public void SnapTo(RectTransform target)
    {
        if (RectTransform == null || target == null)
        {
            return;
        }

        RectTransform.position = target.position;
    }
}

public struct WildWindRectSpec
{
    public Vector2 anchorMin;
    public Vector2 anchorMax;
    public Vector2 pivot;
    public Vector2 anchoredPosition;
    public Vector2 sizeDelta;
}

public static class WildWindPuzzleUi
{
    private static Font cachedDefaultFont;

    public static WildWindRectSpec StretchFull()
    {
        return StretchFull(Vector2.zero, Vector2.zero);
    }

    public static WildWindRectSpec StretchFull(Vector2 leftTop, Vector2 rightBottom)
    {
        return new WildWindRectSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = new Vector2((leftTop.x - rightBottom.x) * 0.5f, (rightBottom.y - leftTop.y) * 0.5f),
            sizeDelta = new Vector2(-(leftTop.x + rightBottom.x), -(leftTop.y + rightBottom.y))
        };
    }

    public static GameObject CreateRect(string name, Transform parent, WildWindRectSpec spec)
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

    public static Text CreateText(RectTransform parent, string text, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor, Color color)
    {
        return CreateAnchoredText(parent, text, fontSize, anchoredPosition, size, anchor, color, new Vector2(0f, 1f), new Vector2(0f, 1f));
    }

    public static Text CreateCenteredText(RectTransform parent, string text, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor, Color color)
    {
        return CreateAnchoredText(parent, text, fontSize, anchoredPosition, size, anchor, color, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
    }

    public static Text CreateAnchoredText(RectTransform parent, string text, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor anchor, Color color, Vector2 rectAnchor, Vector2 pivot)
    {
        GameObject textObject = CreateRect("Text", parent, new WildWindRectSpec
        {
            anchorMin = rectAnchor,
            anchorMax = rectAnchor,
            pivot = pivot,
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });
        Text label = textObject.AddComponent<Text>();
        label.font = GetDefaultFont();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        return label;
    }

    public static Text CreateButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, Color accent)
    {
        return CreateAnchoredButton(parent, name, label, anchoredPosition, size, action, accent, new Vector2(0f, 1f), new Vector2(0f, 1f));
    }

    public static Text CreateCenteredButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, Color accent)
    {
        return CreateAnchoredButton(parent, name, label, anchoredPosition, size, action, accent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
    }

    public static Text CreateAnchoredButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action, Color accent, Vector2 rectAnchor, Vector2 pivot)
    {
        GameObject buttonObject = CreateRect(name, parent, new WildWindRectSpec
        {
            anchorMin = rectAnchor,
            anchorMax = rectAnchor,
            pivot = pivot,
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.080f, 0.086f, 0.086f, 0.97f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = Color.Lerp(image.color, accent, 0.30f);
        colors.pressedColor = Color.Lerp(image.color, accent, 0.55f);
        colors.selectedColor = Color.Lerp(image.color, accent, 0.24f);
        colors.disabledColor = new Color(0.06f, 0.065f, 0.065f, 0.60f);
        button.colors = colors;
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        Text text = CreateText(buttonObject.GetComponent<RectTransform>(), label, 18, Vector2.zero, size, TextAnchor.MiddleCenter, new Color(0.91f, 0.88f, 0.76f, 1f));
        text.raycastTarget = false;
        return text;
    }

    public static bool ContainsPointer(RectTransform rect, PointerEventData eventData)
    {
        if (rect == null || eventData == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera);
    }

    public static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static Font GetDefaultFont()
    {
        if (cachedDefaultFont == null)
        {
            cachedDefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedDefaultFont == null)
            {
                cachedDefaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        return cachedDefaultFont;
    }
}
