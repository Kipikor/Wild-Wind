using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

[ExecuteAlways]
public sealed class WildWindExpeditionSelectionScreen : MonoBehaviour
{
    private const string CanvasName = "Expedition Selection Canvas";
    private const string EventSystemName = "Expedition Selection EventSystem";
    private const string PreviewCameraName = "Expedition Selection Camera";
    private const string RootName = "Expedition Selection Root";

    [Header("Data")]
    public string configFolder = "Data/Config";
    public bool loadCsvConfigs = true;

    [Header("Preview State")]
    [Range(3, 7)] public int previewFlagshipRank = 3;
    [Range(0f, 100f)] public float previewMoralePercent = 100f;
    public string previewFlagshipName = "Флагман R3 Горизонт";

    [Header("Layout")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private readonly List<FlagshipExpeditionDefinition> expeditions = new List<FlagshipExpeditionDefinition>();
    private Canvas canvas;
    private RectTransform root;
    private RectTransform expeditionListContent;
    private RectTransform detailsRoot;
    private Text statusText;
    private string selectedExpeditionId = "";
    private static Font cachedDefaultFont;

    private static readonly Color BackgroundColor = new Color(0.018f, 0.024f, 0.030f, 1f);
    private static readonly Color TopBarColor = new Color(0.030f, 0.036f, 0.042f, 0.96f);
    private static readonly Color PanelColor = new Color(0.044f, 0.052f, 0.060f, 0.94f);
    private static readonly Color PanelSoftColor = new Color(0.070f, 0.076f, 0.078f, 0.90f);
    private static readonly Color ButtonColor = new Color(0.095f, 0.088f, 0.074f, 0.92f);
    private static readonly Color ButtonHoverColor = new Color(0.190f, 0.152f, 0.092f, 0.98f);
    private static readonly Color ButtonPressedColor = new Color(0.360f, 0.240f, 0.105f, 1f);
    private static readonly Color AccentColor = new Color(0.92f, 0.67f, 0.34f, 1f);
    private static readonly Color GreenColor = new Color(0.48f, 0.78f, 0.56f, 1f);
    private static readonly Color WarningColor = new Color(0.96f, 0.55f, 0.36f, 1f);
    private static readonly Color TextColor = new Color(0.91f, 0.84f, 0.70f, 1f);
    private static readonly Color MutedTextColor = new Color(0.65f, 0.68f, 0.67f, 1f);
    private static readonly Color DimTextColor = new Color(0.44f, 0.47f, 0.47f, 1f);

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

    [ContextMenu("Rebuild expedition selection screen")]
    public void RebuildEditableScreen()
    {
        RebuildScreen();
        MarkSceneDirtyIfEditing();
    }

    public void RebuildScreen()
    {
        ClearGeneratedChildren();
        EnsurePreviewCamera();
        EnsureEventSystem(transform);
        LoadExpeditions();
        BuildCanvas();
        BuildBackground();
        BuildTopBar();
        BuildMainPanels();
        SelectInitialExpedition();
        RefreshExpeditionList();
        RefreshDetails();
    }

    private void LoadExpeditions()
    {
        expeditions.Clear();
        if (loadCsvConfigs)
        {
            WorldConfigDatabase config = new WorldConfigDatabase();
            config.LoadFromAssetsConfigFolder(configFolder);
            if (config.isLoaded)
            {
                for (int i = 0; i < config.flagshipExpeditions.Count; i++)
                {
                    FlagshipExpeditionDefinition expedition = config.flagshipExpeditions[i];
                    if (expedition == null)
                    {
                        continue;
                    }

                    expedition.Normalize();
                    expeditions.Add(expedition);
                }
            }
        }

        if (expeditions.Count == 0)
        {
            expeditions.Add(new FlagshipExpeditionDefinition
            {
                expeditionId = "frontier_trial",
                displayNameRu = "Тестовая окраина",
                regionId = "frontier_trial_region",
                sceneName = "WildWindWorldScene",
                returnDockId = "capital",
                returnDockKind = DockingLocationKind.Island,
                minimumFlagshipRank = 3,
                moraleDrainMultiplier = 1.25f,
                summaryRu = "Первый черновой регион для отдельного выхода флагмана из столицы."
            });
        }
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

        root = CreateRect(RootName, canvas.transform, StretchFull()).GetComponent<RectTransform>();
    }

    private void BuildBackground()
    {
        Image background = CreateImage("Background", root, StretchFull(), BackgroundColor);
        background.raycastTarget = false;

        Image leftShade = CreateImage("Left Shade", root, StretchFull(), new Color(0.02f, 0.018f, 0.014f, 0.28f));
        leftShade.raycastTarget = false;

        Image horizon = CreateImage("Low Horizon", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 170f)
        }, new Color(0.115f, 0.090f, 0.063f, 0.42f));
        horizon.raycastTarget = false;
    }

    private void BuildTopBar()
    {
        RectTransform topBar = CreateImage("Top Bar", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 98f)
        }, TopBarColor).GetComponent<RectTransform>();

        Text title = CreateText(topBar, "Экспедиции флагмана", 34, new Vector2(54f, -12f), new Vector2(620f, 48f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;

        CreateText(topBar, "Столица / выбор региона", 18, new Vector2(56f, -58f), new Vector2(620f, 26f), TextAnchor.MiddleLeft, MutedTextColor);

        string flagship = string.IsNullOrWhiteSpace(previewFlagshipName) ? "Флагман R" + previewFlagshipRank.ToString(CultureInfo.InvariantCulture) : previewFlagshipName;
        CreateText(topBar, flagship, 20, new Vector2(-700f, -16f), new Vector2(420f, 34f), TextAnchor.MiddleRight, TextColor, new Vector2(1f, 1f));
        CreateText(topBar, "Ранг R" + previewFlagshipRank.ToString(CultureInfo.InvariantCulture) + " / мораль " + Mathf.RoundToInt(previewMoralePercent).ToString(CultureInfo.InvariantCulture) + "%", 18, new Vector2(-700f, -52f), new Vector2(420f, 30f), TextAnchor.MiddleRight, MutedTextColor, new Vector2(1f, 1f));
    }

    private void BuildMainPanels()
    {
        RectTransform listPanel = CreatePanel("Expedition List Panel", new Vector2(560f, 850f), new Vector2(54f, -126f), new Vector2(0f, 1f));
        Text listTitle = CreateText(listPanel, "Регионы", 30, new Vector2(28f, -24f), new Vector2(460f, 42f), TextAnchor.MiddleLeft, TextColor);
        listTitle.fontStyle = FontStyle.Bold;
        CreateText(listPanel, "Отдельные сцены/зоны для крупных кораблей.", 17, new Vector2(30f, -64f), new Vector2(480f, 28f), TextAnchor.MiddleLeft, MutedTextColor);

        RectTransform viewport = CreateImage("Expedition List Viewport", listPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(24f, -112f),
            sizeDelta = new Vector2(512f, 650f)
        }, new Color(1f, 1f, 1f, 0.001f)).GetComponent<RectTransform>();
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        expeditionListContent = CreateRect("Expedition List Content", viewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 650f)
        }).GetComponent<RectTransform>();

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = expeditionListContent;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        statusText = CreateText(listPanel, "", 18, new Vector2(28f, -786f), new Vector2(500f, 42f), TextAnchor.MiddleLeft, AccentColor);
        statusText.text = "Первый экран: выбор экспедиционного региона.";

        detailsRoot = CreatePanel("Expedition Details Panel", new Vector2(1240f, 850f), new Vector2(626f, -126f), new Vector2(0f, 1f));
    }

    private void SelectInitialExpedition()
    {
        if (!string.IsNullOrWhiteSpace(selectedExpeditionId) && FindExpedition(selectedExpeditionId) != null)
        {
            return;
        }

        selectedExpeditionId = expeditions.Count > 0 ? expeditions[0].expeditionId : "";
    }

    private void RefreshExpeditionList()
    {
        if (expeditionListContent == null)
        {
            return;
        }

        ClearChildren(expeditionListContent);
        float rowHeight = 142f;
        expeditionListContent.sizeDelta = new Vector2(0f, Mathf.Max(650f, expeditions.Count * rowHeight));

        for (int i = 0; i < expeditions.Count; i++)
        {
            FlagshipExpeditionDefinition expedition = expeditions[i];
            bool selected = expedition.expeditionId == selectedExpeditionId;
            bool locked = IsLocked(expedition);
            Color cardColor = selected ? new Color(0.145f, 0.112f, 0.070f, 0.98f) : (locked ? new Color(0.045f, 0.050f, 0.052f, 0.88f) : ButtonColor);
            Button card = CreateButton("Expedition " + expedition.expeditionId, expeditionListContent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = new Vector2(0f, -i * rowHeight),
                sizeDelta = new Vector2(0f, 126f)
            }, cardColor, () => SelectExpedition(expedition.expeditionId));

            CreateText(card.transform as RectTransform, GetExpeditionDisplayName(expedition), 23, new Vector2(18f, -12f), new Vector2(300f, 32f), TextAnchor.MiddleLeft, locked ? DimTextColor : TextColor);

            string meta = "R" + expedition.minimumFlagshipRank.ToString(CultureInfo.InvariantCulture) +
                "+ / мораль x" + expedition.moraleDrainMultiplier.ToString("0.##", CultureInfo.InvariantCulture) +
                " / " + Clean(expedition.regionId);
            CreateText(card.transform as RectTransform, meta, 16, new Vector2(18f, -48f), new Vector2(380f, 24f), TextAnchor.MiddleLeft, locked ? DimTextColor : MutedTextColor);

            string summary = string.IsNullOrWhiteSpace(expedition.summaryRu) ? "Экспедиционный регион флагмана." : expedition.summaryRu;
            CreateText(card.transform as RectTransform, summary, 15, new Vector2(18f, -78f), new Vector2(440f, 42f), TextAnchor.UpperLeft, locked ? DimTextColor : MutedTextColor);

            string badge = locked ? "Закрыто" : "Готово";
            Color badgeColor = locked ? WarningColor : GreenColor;
            CreateBadge(card.transform as RectTransform, badge, new Vector2(-18f, -18f), new Vector2(92f, 30f), badgeColor);
        }
    }

    private void SelectExpedition(string expeditionId)
    {
        selectedExpeditionId = expeditionId ?? "";
        RefreshExpeditionList();
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (detailsRoot == null)
        {
            return;
        }

        ClearChildren(detailsRoot);
        FlagshipExpeditionDefinition expedition = FindExpedition(selectedExpeditionId);
        if (expedition == null)
        {
            CreateText(detailsRoot, "Экспедиции не найдены", 32, new Vector2(36f, -30f), new Vector2(640f, 64f), TextAnchor.MiddleLeft, TextColor);
            return;
        }

        bool locked = IsLocked(expedition);
        Text title = CreateText(detailsRoot, GetExpeditionDisplayName(expedition), 42, new Vector2(36f, -30f), new Vector2(780f, 60f), TextAnchor.MiddleLeft, TextColor);
        title.fontStyle = FontStyle.Bold;
        CreateText(detailsRoot, Clean(expedition.regionId), 18, new Vector2(40f, -86f), new Vector2(760f, 28f), TextAnchor.MiddleLeft, MutedTextColor);

        string state = locked ? "Требуется флагман R" + expedition.minimumFlagshipRank.ToString(CultureInfo.InvariantCulture) : "Можно отправлять";
        CreateBadge(detailsRoot, state, new Vector2(-40f, -42f), new Vector2(230f, 36f), locked ? WarningColor : GreenColor);

        float metricY = -150f;
        CreateMetric(detailsRoot, "Вход", "R" + expedition.minimumFlagshipRank.ToString(CultureInfo.InvariantCulture) + "+", new Vector2(36f, metricY));
        CreateMetric(detailsRoot, "Мораль", "x" + expedition.moraleDrainMultiplier.ToString("0.##", CultureInfo.InvariantCulture), new Vector2(316f, metricY));
        CreateMetric(detailsRoot, "Сцена", Clean(expedition.sceneName), new Vector2(596f, metricY));
        CreateMetric(detailsRoot, "Возврат", Clean(expedition.returnDockId), new Vector2(876f, metricY));

        BuildRouteStrip(expedition);

        CreateSection(detailsRoot, "Описание региона", string.IsNullOrWhiteSpace(expedition.summaryRu) ? "Экспедиционный регион флагмана." : expedition.summaryRu, new Vector2(36f, -360f), new Vector2(548f, 180f));
        CreateSection(detailsRoot, "Требования", BuildRequirementText(expedition, locked), new Vector2(632f, -360f), new Vector2(548f, 180f));
        CreateSection(detailsRoot, "Последствия", "После старта флагман покидает городскую сцену. Мораль начинает снижаться по таймеру региона, а нормальное восстановление происходит только после возвращения в столицу.", new Vector2(36f, -576f), new Vector2(548f, 166f));
        CreateSection(detailsRoot, "Зачем отдельная сцена", "Перелёт можно свернуть в загрузку, а регион сделать плотным: цели, угрозы, поломки, малые острова и задачи вокруг флагмана.", new Vector2(632f, -576f), new Vector2(548f, 166f));

        CreateButtonWithLabel(detailsRoot, "Назад", new Vector2(36f, -768f), new Vector2(220f, 54f), ButtonColor, () => SetStatus("Возврат на городской экран пока будет следующим макетом."));

        Button launch = CreateButtonWithLabel(detailsRoot, locked ? "Нужен R" + expedition.minimumFlagshipRank.ToString(CultureInfo.InvariantCulture) : "Начать экспедицию", new Vector2(932f, -768f), new Vector2(250f, 54f), locked ? new Color(0.075f, 0.070f, 0.064f, 0.92f) : ButtonColor, () => SetStatus("Выбрана экспедиция: " + GetExpeditionDisplayName(expedition) + ". Подключение к MetaGameState сделаем после утверждения экрана."));
        launch.interactable = !locked;
    }

    private void BuildRouteStrip(FlagshipExpeditionDefinition expedition)
    {
        RectTransform strip = CreateImage("Route Strip", detailsRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(36f, -260f),
            sizeDelta = new Vector2(1146f, 70f)
        }, new Color(0.030f, 0.035f, 0.040f, 0.88f)).GetComponent<RectTransform>();

        CreateRouteNode(strip, "Столица", new Vector2(24f, -15f), new Vector2(250f, 40f), TextColor);
        CreateRouteLine(strip, new Vector2(290f, -34f), new Vector2(220f, 2f));
        CreateRouteNode(strip, "Загрузка", new Vector2(528f, -15f), new Vector2(220f, 40f), AccentColor);
        CreateRouteLine(strip, new Vector2(768f, -34f), new Vector2(190f, 2f));
        CreateRouteNode(strip, Clean(expedition.regionId), new Vector2(976f, -15f), new Vector2(150f, 40f), TextColor);
    }

    private void CreateRouteNode(RectTransform parent, string text, Vector2 position, Vector2 size, Color color)
    {
        Text label = CreateText(parent, text, 18, position, size, TextAnchor.MiddleCenter, color);
        label.fontStyle = FontStyle.Bold;
    }

    private void CreateRouteLine(RectTransform parent, Vector2 position, Vector2 size)
    {
        CreateImage("Route Line", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = position,
            sizeDelta = size
        }, new Color(0.45f, 0.40f, 0.30f, 0.76f));
    }

    private void CreateMetric(RectTransform parent, string label, string value, Vector2 position)
    {
        RectTransform metric = CreateImage("Metric " + label, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = position,
            sizeDelta = new Vector2(250f, 80f)
        }, PanelSoftColor).GetComponent<RectTransform>();

        CreateText(metric, label, 15, new Vector2(18f, -10f), new Vector2(210f, 24f), TextAnchor.MiddleLeft, MutedTextColor);
        Text valueText = CreateText(metric, value, 25, new Vector2(18f, -38f), new Vector2(210f, 32f), TextAnchor.MiddleLeft, TextColor);
        valueText.fontStyle = FontStyle.Bold;
    }

    private void CreateSection(RectTransform parent, string title, string body, Vector2 position, Vector2 size)
    {
        RectTransform section = CreateImage("Section " + title, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = position,
            sizeDelta = size
        }, PanelSoftColor).GetComponent<RectTransform>();

        Text titleText = CreateText(section, title, 20, new Vector2(18f, -14f), new Vector2(size.x - 36f, 28f), TextAnchor.MiddleLeft, TextColor);
        titleText.fontStyle = FontStyle.Bold;
        CreateText(section, body, 17, new Vector2(18f, -52f), new Vector2(size.x - 36f, size.y - 66f), TextAnchor.UpperLeft, MutedTextColor);
    }

    private void CreateBadge(RectTransform parent, string text, Vector2 position, Vector2 size, Color color)
    {
        RectTransform badge = CreateImage("Badge " + text, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(1f, 1f),
            anchoredPosition = position,
            sizeDelta = size
        }, new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.98f)).GetComponent<RectTransform>();

        Text badgeText = CreateText(badge, text, 15, Vector2.zero, size, TextAnchor.MiddleCenter, color, new Vector2(0.5f, 0.5f));
        badgeText.fontStyle = FontStyle.Bold;
    }

    private Button CreateButtonWithLabel(RectTransform parent, string label, Vector2 position, Vector2 size, Color color, UnityAction action)
    {
        Button button = CreateButton("Button " + label, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = position,
            sizeDelta = size
        }, color, action);

        Text text = CreateText(button.transform as RectTransform, label, 20, Vector2.zero, size, TextAnchor.MiddleCenter, TextColor, new Vector2(0.5f, 0.5f));
        text.raycastTarget = false;
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    private Button CreateButton(string name, RectTransform parent, RectTransformSpec spec, Color color, UnityAction action)
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
        colors.colorMultiplier = 1f;
        button.colors = colors;
        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        return button;
    }

    private RectTransform CreatePanel(string name, Vector2 size, Vector2 position, Vector2 anchor)
    {
        return CreateImage(name, root, new RectTransformSpec
        {
            anchorMin = anchor,
            anchorMax = anchor,
            pivot = new Vector2(0f, 1f),
            anchoredPosition = position,
            sizeDelta = size
        }, PanelColor).GetComponent<RectTransform>();
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

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? "";
        }
    }

    private string BuildRequirementText(FlagshipExpeditionDefinition expedition, bool locked)
    {
        string result = "Флагман R" + expedition.minimumFlagshipRank.ToString(CultureInfo.InvariantCulture) + "+, док столицы, свободный слот активной экспедиции.";
        if (locked)
        {
            result += "\n\nСейчас выбран R" + previewFlagshipRank.ToString(CultureInfo.InvariantCulture) + ": регион виден, но старт закрыт.";
        }
        else
        {
            result += "\n\nСейчас выбранный флагман подходит.";
        }

        return result;
    }

    private bool IsLocked(FlagshipExpeditionDefinition expedition)
    {
        return expedition != null && previewFlagshipRank < expedition.minimumFlagshipRank;
    }

    private FlagshipExpeditionDefinition FindExpedition(string expeditionId)
    {
        if (string.IsNullOrWhiteSpace(expeditionId))
        {
            return null;
        }

        for (int i = 0; i < expeditions.Count; i++)
        {
            if (expeditions[i] != null && expeditions[i].expeditionId == expeditionId)
            {
                return expeditions[i];
            }
        }

        return null;
    }

    private static string GetExpeditionDisplayName(FlagshipExpeditionDefinition expedition)
    {
        if (expedition == null)
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(expedition.displayNameRu) ? expedition.expeditionId : expedition.displayNameRu;
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
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

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(transform.GetChild(i).gameObject);
        }

        canvas = null;
        root = null;
        expeditionListContent = null;
        detailsRoot = null;
        statusText = null;
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

    private struct RectTransformSpec
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }
}
