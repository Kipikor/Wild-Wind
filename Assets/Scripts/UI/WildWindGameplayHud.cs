using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public sealed class WildWindGameplayHud : MonoBehaviour
{
    public static readonly string[] RequiredLocalizationKeys =
    {
        "game.hud.city",
        "game.hud.flight",
        "game.hud.session_objective",
        "game.hud.session_path",
        "game.hud.distance",
        "game.hud.altitude",
        "game.hud.speed",
        "game.hud.dock",
        "game.hud.menu",
        "game.hud.completed",
        "game.hud.compass_target",
        "game.hud.on",
        "game.hud.off"
    };

    private const string HudObjectName = "Wild Wind Gameplay HUD";
    private const string CanvasName = "Gameplay HUD Canvas";
    private const string EventSystemName = "Gameplay HUD EventSystem";
    private const int CompassTickCount = 25;
    private const int CompassTickCenterIndex = CompassTickCount / 2;
    private const float CompassTickStepDegrees = 15f;
    private const float CompassPixelsPerDegree = 3.35f;
    private const float EnginePowerBarHeight = 880f;
    private const float EngineGearSelectorX = 1738f;
    private const float EngineGearSelectorBottomY = 16f;
    private const float EngineGearSelectorMarkWidth = 54f;
    private const float EngineGearSelectorMarkHeight = 24f;
    private const float RadarMaxRangeMeters = 10000f;
    private const string WindowLocationId = "location";
    private const string WindowReturnId = "return";
    private const string WindowShipId = "ship";
    private const string WindowTasksId = "tasks";
    private const string WindowCargoId = "cargo";
    private const string WindowRadarId = "radar";
    private const string WindowModulesId = "modules";
    private const string WindowResourcesId = "resources";
    private const string WindowEncyclopediaId = "encyclopedia";
    private const string WindowAchievementsId = "achievements";
    private const string WindowMailId = "mail";
    private const string WindowSettingsId = "settings";
    private const string WindowOfferId = "offer";
    private const string WindowSideEventsId = "side_events";
    private const string WindowFactionsId = "factions";
    private const string WindowDevelopmentId = "development";
    private const string WindowMetaAllTasksId = "meta_all_tasks";
    private const string MainHudPrefabResourcePath = "UI/HUD/MainScreen/WildWindMainScreenHud";
    private const string MainHudIconResourceFolderPath = "UI/HUD/MainScreen/Icons/";
    private const string HudWindowFrameResourcePath = "UI/HUD/WildWindHudWindowFrame";
    private const string HudWindowPrefabFolderResourcePath = "UI/HUD/Windows/";
    private enum MetaScreenMode
    {
        Port,
        Dock
    }

    private const string MetaProfileTitlePlaceholder = "Капитан Ветров";
    private const string MetaProfileInitialsPlaceholder = "КВ";
    private const int MetaProfileMasteryCurrentPlaceholder = 26;
    private const int MetaProfileMasteryNextPlaceholder = 27;
    private const float MetaProfileMasteryProgressPlaceholder = 0.64f;
    private const int MetaResourceCounterCount = 5;
    private const float MetaResourceCounterWidth = 198f;
    private const float MetaResourceCounterHeight = 70f;
    private const float MetaResourceCounterGap = 0f;
    private const int MetaTopRightButtonCount = 4;
    private const float MetaTopRightButtonSize = 60f;
    private const float MetaTopRightButtonGap = 22f;
    private const int MetaLeftSideButtonCount = 5;
    private const float MetaLeftSideButtonSize = 110f;
    private const float MetaLeftSideButtonGap = 21f;
    private const float MetaDockButtonSize = 156f;
    private const int MetaQuestMaxCount = 3;
    private const int MetaProjectMaxCount = 5;
    private const float MetaProjectCardWidth = 252f;
    private const float MetaProjectCardHeight = 124f;
    private const float MetaProjectCardGap = 17f;
    private const int DevelopmentTreeTierCount = 10;
    private const float DevelopmentFactionListWidth = 250f;
    private const float DevelopmentTreePanelWidth = 1220f;
    private const float DevelopmentTreeViewportWidth = 1220f;
    private const float DevelopmentTreeViewportHeight = 858f;
    private const float DevelopmentTreeContentPaddingX = 72f;
    private const float DevelopmentTreeContentPaddingY = 48f;
    private const float DevelopmentDetailsWidth = 340f;
    private const float DevelopmentTileWidth = 150f;
    private const float DevelopmentTileHeight = 80f;
    private const float DevelopmentRankStep = 118f;
    private const float DevelopmentBranchStep = 176f;
    private static readonly Color EngineForwardThrustColor = new Color(0.14f, 0.78f, 0.28f, 0.96f);
    private static readonly Color EngineReverseThrustColor = new Color(0.96f, 0.72f, 0.12f, 0.96f);
    private static readonly int[] EngineThrottleGearNotches = { 5, 4, 3, 2, 1, 0, -1, -2, -3 };
    private static readonly Color CompassDefaultTargetColor = new Color(0.44f, 0.95f, 1f, 0.96f);
    private static readonly Color CompassExitTargetColor = new Color(1f, 0.78f, 0.22f, 0.98f);
    private static readonly Color PortGlass = new Color(0.90f, 0.95f, 1.00f, 0.78f);
    private static readonly Color PortGlassStrong = new Color(0.94f, 0.97f, 1.00f, 0.92f);
    private static readonly Color PortGlassMuted = new Color(0.82f, 0.89f, 0.96f, 0.74f);
    private static readonly Color PortStroke = new Color(0.47f, 0.56f, 0.68f, 0.62f);
    private static readonly Color PortText = new Color(0.18f, 0.22f, 0.31f, 1f);
    private static readonly Color PortTextSoft = new Color(0.33f, 0.39f, 0.50f, 0.96f);
    private static readonly Color PortNavy = new Color(0.15f, 0.22f, 0.40f, 1f);
    private static readonly Color PortGold = new Color(0.94f, 0.70f, 0.28f, 1f);
    private static readonly Color PortTrack = new Color(0.61f, 0.67f, 0.75f, 0.78f);
    private static readonly MetaResourceCounterSpec[] MetaResourceCounters =
    {
        new MetaResourceCounterSpec("freight", 1240000, "F", new Color(0.78f, 0.76f, 0.68f, 1f)),
        new MetaResourceCounterSpec("design_experience", 853000, "XP", new Color(0.36f, 0.72f, 0.32f, 1f)),
        new MetaResourceCounterSpec("iron", 412000, "Fe", new Color(0.66f, 0.46f, 0.28f, 1f)),
        new MetaResourceCounterSpec("water", 18700, "W", new Color(0.28f, 0.62f, 0.92f, 1f)),
        new MetaResourceCounterSpec("solid", 2450, "S", new Color(0.94f, 0.72f, 0.26f, 1f))
    };
    private static readonly MetaTopRightButtonSpec[] MetaTopRightButtons =
    {
        new MetaTopRightButtonSpec(WindowEncyclopediaId, "ЭНЦИКЛОПЕДИЯ", "\u25A4"),
        new MetaTopRightButtonSpec(WindowAchievementsId, "ДОСТИЖЕНИЯ", "\u2605"),
        new MetaTopRightButtonSpec(WindowMailId, "ПОЧТА", "\u2709"),
        new MetaTopRightButtonSpec(WindowSettingsId, "НАСТРОЙКИ", "\u2699")
    };
    private static readonly MetaSideButtonSpec[] MetaLeftSideButtons =
    {
        new MetaSideButtonSpec(WindowSideEventsId, "События", "\u25A3", "MetaEvents"),
        new MetaSideButtonSpec(WindowFactionsId, "Торговцы", "\u2696", "MetaMerchants"),
        new MetaSideButtonSpec(WindowCargoId, "Инвентарь", "\u25A4", "MetaInventory"),
        new MetaSideButtonSpec(WindowEncyclopediaId, "Знания", "\u25A5", "MetaKnowledge"),
        new MetaSideButtonSpec(WindowOfferId, "Магазин", "\u25C6", "MetaShop")
    };
    private static readonly MetaQuestSpec[] MetaQuestSpecs =
    {
        new MetaQuestSpec("\u25C9", "Улучшите переработку", "до уровня 6", 4, 6),
        new MetaQuestSpec("\u2697", "Проведите исследование", "карты ветров", 0, 1),
        new MetaQuestSpec("\u2691", "Отправьте корабли", "в разведку", 2, 3)
    };
    private static readonly MetaProjectSpec[] MetaProjectSpecs =
    {
        new MetaProjectSpec("Рецепт: Сталь", "24%", "\u2699", "1д 23ч 54м", 0.24f),
        new MetaProjectSpec("Рецепт: Сталь", "24%", "\u2699", "1д 23ч 54м", 0.24f),
        new MetaProjectSpec("Рецепт: Сталь", "24%", "\u2699", "1д 23ч 54м", 0.24f),
        new MetaProjectSpec("Рецепт: Сталь", "24%", "\u2699", "1д 23ч 54м", 0.24f),
        new MetaProjectSpec("Рецепт: Сталь", "24%", "\u2699", "1д 23ч 54м", 0.24f)
    };
    private static readonly string[] MainHudRequiredIconResourceNames =
    {
        "MetaEvents",
        "MetaMerchants",
        "MetaInventory",
        "MetaKnowledge",
        "MetaShop",
        "MetaProjects",
        "MetaDock"
    };

    [SerializeField, InspectorName("Reference Resolution")] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(1, 3), InspectorName("Meta Quest Preview Count")] private int metaQuestPreviewCount = 3;
    [SerializeField, Range(1, 5), InspectorName("Meta Project Queue Preview Count")] private int metaProjectQueuePreviewCount = 5;

    private Canvas canvas;
    private RectTransform root;
    private RectTransform flightPanel;
    private RectTransform sortiePanel;
    private RectTransform windowDockPanel;
    private RectTransform compassPanel;
    private RectTransform enginePowerBar;
    private RectTransform engineLiftPowerFill;
    private RectTransform engineModulePowerFill;
    private RectTransform engineThrustPowerFill;
    private RectTransform[] engineThrottleGearMarks;
    private Text[] engineThrottleGearTexts;
    private Text[] compassTickTexts;
    private RectTransform[] compassTickLines;
    private RectTransform compassTargetLine;
    private Text compassTargetText;
    private Text modeText;
    private Text objectiveText;
    private Text sessionPathText;
    private Text distanceText;
    private Text altitudeText;
    private Text speedText;
    private Text sortieTitleText;
    private Text sortieZoneText;
    private Text sortieNavigationText;
    private Text sortieExtractionText;
    private Text sortieReservesText;
    private Text sortieCargoText;
    private Text sortieShipText;
    private Text sortieStatusDetailText;
    private Text enginePowerText;
    private Text statusText;
    private Button dockButton;
    private Text dockText;
    private Text flightMenuText;
    private Text claudiumSlipstreamText;
    private Button resourcesButton;
    private Text resourcesText;
    private RectTransform playerProfilePanel;
    private Text playerProfileTitleText;
    private Text playerProfileMasteryCurrentText;
    private Text playerProfileMasteryNextText;
    private RectTransform playerProfileMasteryFill;
    private RectTransform metaResourceCounterStrip;
    private Text[] metaResourceCounterAmountTexts;
    private Button[] metaResourceCounterPlusButtons;
    private Image[] metaResourceCounterIconImages;
    private RectTransform metaTopRightButtonStrip;
    private Button[] metaTopRightButtons;
    private Text[] metaTopRightButtonIconTexts;
    private RectTransform metaLeftSideButtonStrip;
    private Button[] metaLeftSideButtons;
    private Text[] metaLeftSideButtonIconTexts;
    private Button metaDockButton;
    private Text metaDockButtonText;
    private RectTransform metaBlueprintButtonRoot;
    private RectTransform metaDockScreenLayer;
    private Button metaDockScreenPortButton;
    private Text metaDockScreenPortButtonText;
    private RectTransform metaDockShipSlotRoot;
    private RectTransform metaDockSelectedShipRoot;
    private RectTransform metaDockMissionRoot;
    private RectTransform metaDockRewardRoot;
    private RectTransform metaDockResultModalRoot;
    private Text metaDockHeaderText;
    private Text metaDockSelectedShipText;
    private Text metaDockSelectedStatsText;
    private Text metaDockMissionHeaderText;
    private Text metaDockMissionDetailText;
    private Text metaDockRewardText;
    private Text metaDockResultTitleText;
    private Text metaDockResultSubtitleText;
    private Text metaDockResultBodyText;
    private Text metaDockResultCloseText;
    private Text metaDockManualSortieText;
    private Text metaDockCoreCombatText;
    private Text metaDockQuickBattleText;
    private Text metaDockRunMissionText;
    private Text metaDockSellText;
    private Text[] metaDockSlotTexts;
    private Text[] metaDockMissionTexts;
    private Button[] metaDockSlotButtons;
    private Button[] metaDockMissionButtons;
    private Button metaDockManualSortieButton;
    private Button metaDockCoreCombatButton;
    private Button metaDockQuickBattleButton;
    private Button metaDockRunMissionButton;
    private Button metaDockSellButton;
    private Button metaDockResultCloseButton;
    private RectTransform metaQuestPanel;
    private RectTransform[] metaQuestRows;
    private Text[] metaQuestIconTexts;
    private Text[] metaQuestLineOneTexts;
    private Text[] metaQuestLineTwoTexts;
    private Text[] metaQuestCounterTexts;
    private RectTransform[] metaQuestProgressFills;
    private Button metaAllTasksButton;
    private Text metaAllTasksButtonText;
    private RectTransform metaProjectPanel;
    private RectTransform metaProjectToolButtonStrip;
    private Button metaProjectHomeButton;
    private Button metaProjectGridButton;
    private Button metaProjectCameraButton;
    private Button settingsResetProgressButton;
    private Text metaProjectHomeButtonText;
    private Text metaProjectGridButtonText;
    private Text metaProjectCameraButtonText;
    private Text settingsResetProgressText;
    private RectTransform[] metaProjectCards;
    private RectTransform[] metaProjectProgressFills;
    private Text[] metaProjectTitleTexts;
    private Text[] metaProjectAmountTexts;
    private Text[] metaProjectIconTexts;
    private Text[] metaProjectTimerTexts;
    private HudWindow locationWindow;
    private HudWindow returnWindow;
    private HudWindow shipWindow;
    private HudWindow tasksWindow;
    private HudWindow cargoWindow;
    private HudWindow radarWindow;
    private HudWindow modulesWindow;
    private HudWindow resourcesWindow;
    private HudWindow encyclopediaWindow;
    private HudWindow achievementsWindow;
    private HudWindow mailWindow;
    private HudWindow settingsWindow;
    private HudWindow offerWindow;
    private HudWindow sideEventsWindow;
    private HudWindow factionsWindow;
    private HudWindow developmentWindow;
    private Text developmentWindowText;
    private RectTransform developmentSupplierTabRoot;
    private RectTransform developmentTreeScrollRoot;
    private RectTransform developmentTreeViewport;
    private RectTransform developmentTreeRoot;
    private ScrollRect developmentTreeScrollRect;
    private RectTransform developmentDetailsRoot;
    private readonly Dictionary<string, DevelopmentShipTileView> developmentShipTilesById = new Dictionary<string, DevelopmentShipTileView>(StringComparer.OrdinalIgnoreCase);
    private static bool hudPointerCaptureActiveForCamera;
    private string selectedDevelopmentSupplierId = "";
    private string selectedDevelopmentShipId = "";
    private string hoveredDevelopmentShipId = "";
    private string selectedMetaDockMissionOfferId = "";
    private string developmentDetailsShipId = "";
    private string developmentWindowLayoutKey = "";
    private int developmentWindowSupplierCount;
    private int developmentWindowTileCount;
    private int developmentWindowConnectionCount;
    private HudWindow metaAllTasksWindow;
    private Text locationWindowText;
    private Text returnWindowText;
    private Text shipWindowText;
    private Text tasksWindowText;
    private Text cargoHeaderText;
    private Text[] cargoSlotTexts;
    private Text radarBoulderFilterText;
    private Text radarDebrisFilterText;
    private Text radarWindowText;
    private Text modulesWindowText;
    private Text resourceCatalogHeaderText;
    private RectTransform resourceCatalogContent;
    private WildWindKnowledgeScreen knowledgeScreen;
    private GameObject knowledgeScreenObject;
    private RadarTacticalSessionOverlay radarSessionOverlay;
    private readonly List<ResourceCatalogRow> resourceCatalogRows = new List<ResourceCatalogRow>();
    private readonly Dictionary<string, HudWindow> hudWindows = new Dictionary<string, HudWindow>();
    private readonly List<RadarEntry> radarEntries = new List<RadarEntry>();
    private readonly List<MiningFragment> radarFragments = new List<MiningFragment>();
    private MetaGameState meta;
    private WildWindGameplaySession session;
    private WildWindSessionCameraController sessionCameraController;
    private WildWindFlightControlBridge flightControls;
    private Vector3 heldFlightInput;
    private float nextRefreshTime;
    private float lastCompassHeadingDegrees;
    private string statusMessage = "";
    private int resourceCatalogBuildItemCount = -1;
    private int resourceCatalogBuildCategoryCount = -1;
    private bool radarShowBoulders = true;
    private bool radarShowDebris = true;
    private bool baseBuildingFocusActive;
    private MetaScreenMode metaScreenMode = MetaScreenMode.Port;
    private static Font cachedDefaultFont;
    private static Sprite cachedCircleSprite;
    private static Sprite cachedRoundedPanelSprite;
    private static Sprite cachedRoundedSmallSprite;
    private static readonly Dictionary<string, Sprite> cachedMainHudIconSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    public bool IsReady => canvas != null && root != null && ResolveMeta() != null && ResolveSession() != null;
    public bool IsFlightPanelVisible => flightPanel != null && flightPanel.gameObject.activeSelf;
    public bool IsFlightCompassVisible => compassPanel != null && compassPanel.gameObject.activeSelf;
    public float LastCompassHeadingDegrees => lastCompassHeadingDegrees;
    public string KnowledgeScreenReportForTests => knowledgeScreen != null ? knowledgeScreen.Report : "";
    public string KnowledgeScreenContentForTests => knowledgeScreen != null ? knowledgeScreen.ContentForTests : "";
    public string KnowledgeScreenSelectedCategoryForTests => knowledgeScreen != null ? knowledgeScreen.SelectedCategoryIdForTests : "";
    public bool IsKnowledgeScreenReadyForTests => knowledgeScreen != null && knowledgeScreen.IsReadyForTests;
    public bool IsKnowledgeScreenVisibleForTests => knowledgeScreen != null && knowledgeScreen.IsVisible;
    public bool KnowledgeScreenHasVisibleTextForTests => knowledgeScreen != null && knowledgeScreen.HasVisibleTextForTests;
    public int KnowledgeScreenSortingOrderForTests => knowledgeScreen != null ? knowledgeScreen.SortingOrderForTests : -1;
    public bool KnowledgeScreenHasRubricTabsForTests => knowledgeScreen != null && knowledgeScreen.HasRubricTabsForTests;
    public bool KnowledgeScreenHasTreeViewForTests => knowledgeScreen != null && knowledgeScreen.HasTreeViewForTests;
    public bool KnowledgeScreenHasInspectorPanelForTests => knowledgeScreen != null && knowledgeScreen.HasInspectorPanelForTests;
    public int KnowledgeScreenVisibleTechnologyNodeCountForTests => knowledgeScreen != null ? knowledgeScreen.VisibleTechnologyNodeCountForTests : 0;
    public int KnowledgeScreenVisibleTreeConnectionCountForTests => knowledgeScreen != null ? knowledgeScreen.VisibleTreeConnectionCountForTests : 0;
    public int KnowledgeScreenVisibleTreeColumnCountForTests => knowledgeScreen != null ? knowledgeScreen.VisibleTreeColumnCountForTests : 0;
    public bool IsResourceCatalogReadyForTests => resourcesWindow != null
        && resourceCatalogContent != null
        && resourceCatalogRows.Count > 0
        && CountResourceRowsWithIcons() == resourceCatalogRows.Count;
    public bool IsResourceCatalogWindowOpenForTests => resourcesWindow != null && resourcesWindow.IsOpen;
    public int ResourceCatalogWindowItemCountForTests => resourceCatalogRows.Count;
    public bool IsMetaPlayerProfileReadyForTests => playerProfilePanel != null
        && playerProfileTitleText != null
        && playerProfileMasteryCurrentText != null
        && playerProfileMasteryNextText != null
        && playerProfileMasteryFill != null;
    public bool IsMetaPlayerProfileVisibleForTests => playerProfilePanel != null && playerProfilePanel.gameObject.activeInHierarchy;
    public string MetaPlayerProfileTitleForTests => playerProfileTitleText != null ? playerProfileTitleText.text : "";
    public float MetaPlayerProfileMasteryFillForTests => playerProfileMasteryFill != null ? Mathf.Clamp01(playerProfileMasteryFill.anchorMax.x) : 0f;
    public bool IsMetaResourceCounterStripReadyForTests => metaResourceCounterStrip != null
        && metaResourceCounterAmountTexts != null
        && metaResourceCounterAmountTexts.Length == MetaResourceCounterCount
        && metaResourceCounterPlusButtons != null
        && metaResourceCounterPlusButtons.Length == MetaResourceCounterCount
        && metaResourceCounterIconImages != null
        && metaResourceCounterIconImages.Length == MetaResourceCounterCount
        && AreMetaResourceCounterControlsReadyForTests();
    public bool IsMetaResourceCounterStripVisibleForTests => metaResourceCounterStrip != null && metaResourceCounterStrip.gameObject.activeInHierarchy;
    public int MetaResourceCounterCountForTests => metaResourceCounterAmountTexts != null ? metaResourceCounterAmountTexts.Length : 0;
    public string GetMetaResourceCounterAmountForTests(int index)
    {
        return metaResourceCounterAmountTexts != null
            && index >= 0
            && index < metaResourceCounterAmountTexts.Length
            && metaResourceCounterAmountTexts[index] != null
            ? metaResourceCounterAmountTexts[index].text
            : "";
    }

    public static string FormatMetaResourceAmountForTests(int amount)
    {
        return FormatMetaResourceAmount(amount);
    }
    public bool IsMetaTopRightButtonsReadyForTests => metaTopRightButtonStrip != null
        && metaTopRightButtons != null
        && metaTopRightButtons.Length == MetaTopRightButtonCount
        && metaTopRightButtonIconTexts != null
        && metaTopRightButtonIconTexts.Length == MetaTopRightButtonCount
        && encyclopediaWindow != null
        && achievementsWindow != null
        && mailWindow != null
        && settingsWindow != null
        && AreMetaTopRightButtonControlsReadyForTests()
        && AreMetaTopRightWindowsReadyForTests();
    public bool IsMetaTopRightButtonsVisibleForTests => metaTopRightButtonStrip != null && metaTopRightButtonStrip.gameObject.activeInHierarchy;
    public int MetaTopRightButtonCountForTests => metaTopRightButtons != null ? metaTopRightButtons.Length : 0;
    public bool IsSettingsResetProgressButtonReadyForTests => settingsResetProgressButton != null
        && settingsResetProgressText != null
        && settingsResetProgressButton.transform.parent == settingsWindow.content
        && settingsResetProgressText.text == "Сбросить прогресс";
    public bool PressSettingsResetProgressForTests()
    {
        return HandleSettingsResetProgressButton();
    }
    public bool OpenMetaTopRightWindowForTests(int index)
    {
        if (!TryGetMetaTopRightWindow(index, out HudWindow window))
        {
            return false;
        }

        window.SetOpen(true);
        RefreshState(true);
        return window.IsOpen;
    }

    public bool IsMetaTopRightWindowOpenForTests(int index)
    {
        return TryGetMetaTopRightWindow(index, out HudWindow window) && window.IsOpen;
    }
    public bool PressMetaTopRightButtonForTests(int index)
    {
        if (index < 0 || metaTopRightButtons == null || index >= metaTopRightButtons.Length || metaTopRightButtons[index] == null || !metaTopRightButtons[index].interactable)
        {
            return false;
        }

        metaTopRightButtons[index].onClick.Invoke();
        return IsMetaTopRightWindowOpenForTests(index);
    }
    public bool AreOpenHudWindowsModalForTests => AreOpenHudWindowsModal();
    public bool IsMetaLeftSideButtonsReadyForTests => metaLeftSideButtonStrip != null
        && metaLeftSideButtons != null
        && metaLeftSideButtons.Length == MetaLeftSideButtonCount
        && metaLeftSideButtonIconTexts != null
        && metaLeftSideButtonIconTexts.Length == MetaLeftSideButtonCount
        && offerWindow != null
        && sideEventsWindow != null
        && factionsWindow != null
        && developmentWindow != null
        && AreMetaLeftSideButtonControlsReadyForTests()
        && AreMetaLeftSideWindowsReadyForTests();
    public bool IsMetaLeftSideButtonsVisibleForTests => metaLeftSideButtonStrip != null && metaLeftSideButtonStrip.gameObject.activeInHierarchy;
    public int MetaLeftSideButtonCountForTests => metaLeftSideButtons != null ? metaLeftSideButtons.Length : 0;
    public bool PressMetaLeftSideButtonForTests(int index)
    {
        if (index < 0 || metaLeftSideButtons == null || index >= metaLeftSideButtons.Length || metaLeftSideButtons[index] == null || !metaLeftSideButtons[index].interactable)
        {
            return false;
        }

        metaLeftSideButtons[index].onClick.Invoke();
        return IsMetaLeftSideWindowOpenForTests(index);
    }
    public bool AreMainHudReferenceIconsReadyForTests => AreMainHudIconSpritesReadyForTests();
    public bool IsMetaDockButtonReadyForTests => metaDockButton != null
        && metaDockButtonText != null
        && metaDockButton.targetGraphic != null
        && metaDockButton.targetGraphic.GetComponent<Image>() != null
        && metaDockButton.targetGraphic.GetComponent<Image>().sprite != null;
    public bool IsMetaDockButtonVisibleForTests => metaDockButton != null && metaDockButton.gameObject.activeInHierarchy;
    public string MetaDockButtonLabelForTests => metaDockButtonText != null ? metaDockButtonText.text : "";
    public bool IsMetaSideRailOnRightForTests => IsRightAnchoredForTests(metaLeftSideButtonStrip)
        && metaDockButton != null
        && IsBottomRightAnchoredForTests(metaDockButton.GetComponent<RectTransform>());
    public bool IsMetaDockScreenReadyForTests => metaDockScreenLayer != null
        && metaDockScreenPortButton != null
        && metaDockScreenPortButtonText != null
        && metaDockScreenPortButton.targetGraphic != null
        && IsMetaDockScreenPortButtonAtDockButtonSpotForTests;
    public bool IsMetaDockGameplayReadyForTests => IsMetaDockScreenReadyForTests
        && metaDockShipSlotRoot != null
        && metaDockSelectedShipRoot != null
        && metaDockMissionRoot != null
        && metaDockRewardRoot != null
        && metaDockHeaderText != null
        && metaDockSelectedShipText != null
        && metaDockSelectedStatsText != null
        && metaDockMissionHeaderText != null
        && metaDockMissionDetailText != null
        && metaDockRewardText != null
        && metaDockResultModalRoot != null
        && metaDockResultTitleText != null
        && metaDockResultSubtitleText != null
        && metaDockResultBodyText != null
        && metaDockResultCloseButton != null
        && metaDockQuickBattleButton != null
        && metaDockRunMissionButton != null
        && metaDockManualSortieButton != null
        && metaDockCoreCombatButton != null
        && metaDockSellButton != null
        && metaDockMissionTexts != null
        && metaDockMissionTexts.Length == 15
        && metaDockMissionButtons != null
        && metaDockMissionButtons.Length == 15
        && metaDockSlotTexts != null
        && metaDockSlotTexts.Length == MetaGameState.DevelopmentDockSlotCount
        && metaDockSlotButtons != null
        && metaDockSlotButtons.Length == MetaGameState.DevelopmentDockSlotCount;
    public bool IsMetaDockScreenVisibleForTests => metaDockScreenLayer != null && metaDockScreenLayer.gameObject.activeInHierarchy;
    public string MetaDockRewardTextForTests => metaDockRewardText != null ? metaDockRewardText.text : "";
    public bool IsMetaDockResultWindowVisibleForTests => metaDockResultModalRoot != null && metaDockResultModalRoot.gameObject.activeInHierarchy;
    public string MetaDockResultWindowTextForTests => metaDockResultBodyText != null ? metaDockResultBodyText.text : "";
    public int MetaDockMissionOfferCountForTests => metaDockMissionTexts != null ? metaDockMissionTexts.Length : 0;
    public string MetaDockSelectedShipTextForTests => metaDockSelectedShipText != null ? metaDockSelectedShipText.text : "";
    public bool CloseAllHudWindowsForTests()
    {
        CloseAllHudWindows();
        RefreshState(true);
        return !AreOpenHudWindowsModalForTests;
    }
    public bool IsMetaDockScreenActiveForRuntime => metaScreenMode == MetaScreenMode.Dock
        && metaDockScreenLayer != null
        && metaDockScreenLayer.gameObject.activeInHierarchy;
    public bool IsPortHudHiddenForDockScreenForTests => IsMetaDockScreenVisibleForTests
        && !IsMetaResourceCounterStripVisibleForTests
        && !IsMetaTopRightButtonsVisibleForTests
        && !IsMetaLeftSideButtonsVisibleForTests
        && !IsMetaDockButtonVisibleForTests
        && !IsMetaQuestPanelVisibleForTests
        && !IsMetaProjectPanelVisibleForTests;
    public bool IsBaseBuildingFocusHidingPortHudForTests => baseBuildingFocusActive
        && !IsMetaResourceCounterStripVisibleForTests
        && !IsMetaTopRightButtonsVisibleForTests
        && !IsMetaLeftSideButtonsVisibleForTests
        && !IsMetaDockButtonVisibleForTests
        && !IsMetaQuestPanelVisibleForTests
        && !IsMetaProjectPanelVisibleForTests;
    public bool IsPortHudVisibleForTests => !baseBuildingFocusActive
        && metaScreenMode == MetaScreenMode.Port
        && IsMetaResourceCounterStripVisibleForTests
        && IsMetaTopRightButtonsVisibleForTests
        && IsMetaLeftSideButtonsVisibleForTests
        && IsMetaDockButtonVisibleForTests
        && IsMetaQuestPanelVisibleForTests
        && IsMetaProjectPanelVisibleForTests;
    public string MetaDockScreenPortButtonLabelForTests => metaDockScreenPortButtonText != null ? metaDockScreenPortButtonText.text : "";
    public bool IsMetaDockScreenPortButtonAtDockButtonSpotForTests
    {
        get
        {
            RectTransform dockRect = metaDockButton != null ? metaDockButton.GetComponent<RectTransform>() : null;
            RectTransform portRect = metaDockScreenPortButton != null ? metaDockScreenPortButton.GetComponent<RectTransform>() : null;
            return dockRect != null
                && portRect != null
                && IsBottomRightAnchoredForTests(portRect)
                && Vector2.Distance(dockRect.anchoredPosition, portRect.anchoredPosition) <= 0.01f
                && Vector2.Distance(dockRect.sizeDelta, portRect.sizeDelta) <= 0.01f;
        }
    }
    public bool IsMetaQuestPanelReadyForTests => metaQuestPanel != null
        && metaQuestRows != null
        && metaQuestRows.Length == MetaQuestMaxCount
        && metaQuestIconTexts != null
        && metaQuestIconTexts.Length == MetaQuestMaxCount
        && metaQuestLineOneTexts != null
        && metaQuestLineOneTexts.Length == MetaQuestMaxCount
        && metaQuestLineTwoTexts != null
        && metaQuestLineTwoTexts.Length == MetaQuestMaxCount
        && metaQuestCounterTexts != null
        && metaQuestCounterTexts.Length == MetaQuestMaxCount
        && metaQuestProgressFills != null
        && metaQuestProgressFills.Length == MetaQuestMaxCount
        && metaAllTasksButton != null
        && metaAllTasksButtonText != null
        && metaAllTasksWindow != null
        && metaAllTasksWindow.content != null
        && metaAllTasksWindow.content.childCount == 0
        && AreMetaQuestRowsReadyForTests();
    public bool IsMetaQuestPanelVisibleForTests => metaQuestPanel != null && metaQuestPanel.gameObject.activeInHierarchy;
    public int MetaQuestVisibleCountForTests => GetMetaQuestVisibleCount();
    public int MetaQuestMaxCountForTests => MetaQuestMaxCount;
    public string MetaAllTasksButtonLabelForTests => metaAllTasksButtonText != null ? metaAllTasksButtonText.text : "";
    public bool IsMetaQuestPanelPinnedLeftForTests => metaQuestPanel != null
        && Mathf.Approximately(metaQuestPanel.anchorMin.x, 0f)
        && Mathf.Approximately(metaQuestPanel.anchorMax.x, 0f)
        && Mathf.Approximately(metaQuestPanel.pivot.x, 0f)
        && metaQuestPanel.anchoredPosition.x <= 50f;
    public bool IsMetaProjectPanelReadyForTests => metaProjectPanel != null
        && metaProjectToolButtonStrip != null
        && metaProjectHomeButton != null
        && metaProjectGridButton != null
        && metaProjectCameraButton != null
        && metaProjectHomeButtonText != null
        && metaProjectGridButtonText != null
        && metaProjectCameraButtonText != null
        && metaProjectCards != null
        && metaProjectCards.Length == MetaProjectMaxCount
        && metaProjectProgressFills != null
        && metaProjectProgressFills.Length == MetaProjectMaxCount
        && metaProjectTitleTexts != null
        && metaProjectTitleTexts.Length == MetaProjectMaxCount
        && metaProjectAmountTexts != null
        && metaProjectAmountTexts.Length == MetaProjectMaxCount
        && metaProjectIconTexts != null
        && metaProjectIconTexts.Length == MetaProjectMaxCount
        && metaProjectTimerTexts != null
        && metaProjectTimerTexts.Length == MetaProjectMaxCount
        && AreMetaProjectCardsReadyForTests();
    public bool IsMetaProjectPanelVisibleForTests => metaProjectPanel != null && metaProjectPanel.gameObject.activeInHierarchy;
    public bool IsMetaProjectPanelBottomLeftForTests => metaQuestPanel != null
        && metaProjectToolButtonStrip != null
        && metaProjectPanel != null
        && IsBottomLeftAnchoredForTests(metaProjectToolButtonStrip)
        && IsBottomLeftAnchoredForTests(metaProjectPanel)
        && metaProjectToolButtonStrip.anchoredPosition.x <= 24f
            && metaProjectPanel.anchoredPosition.x >= 80f
            && metaProjectPanel.anchoredPosition.x <= 110f
            && metaProjectPanel.anchoredPosition.y <= 40f
        && metaProjectToolButtonStrip.anchoredPosition.y >= metaProjectPanel.anchoredPosition.y + metaProjectPanel.sizeDelta.y + 20f
        && metaProjectHomeButton != null
        && metaProjectGridButton != null
        && metaProjectCameraButton != null
        && metaProjectHomeButton.transform.parent == metaProjectToolButtonStrip
        && metaProjectGridButton.transform.parent == metaProjectToolButtonStrip
        && metaProjectCameraButton.transform.parent == metaProjectToolButtonStrip
        && metaProjectHomeButton.transform.parent != metaProjectPanel
        && metaProjectGridButton.transform.parent != metaProjectPanel
        && metaProjectCameraButton.transform.parent != metaProjectPanel;
    public int MetaProjectVisibleCountForTests => GetMetaProjectVisibleCount();
    public int MetaProjectMaxCountForTests => MetaProjectMaxCount;
    public string MetaProjectHomeButtonLabelForTests => metaProjectHomeButtonText != null ? metaProjectHomeButtonText.text : "";
    public string MetaProjectGridButtonLabelForTests => metaProjectGridButtonText != null ? metaProjectGridButtonText.text : "";
    public string MetaProjectCameraButtonLabelForTests => metaProjectCameraButtonText != null ? metaProjectCameraButtonText.text : "";
    public bool PressMetaProjectCameraButtonForTests()
    {
        return HandleMetaCameraButton();
    }

    public string GetMetaProjectTimerForTests(int index)
    {
        return metaProjectTimerTexts != null
            && index >= 0
            && index < metaProjectTimerTexts.Length
            && metaProjectTimerTexts[index] != null
            ? metaProjectTimerTexts[index].text
            : "";
    }

    public bool OpenMetaLeftSideWindowForTests(int index)
    {
        if (!TryGetMetaLeftSideWindow(index, out HudWindow window))
        {
            return false;
        }

        window.SetOpen(true);
        RefreshState(true);
        return window.IsOpen;
    }

    public bool IsMetaLeftSideWindowOpenForTests(int index)
    {
        return TryGetMetaLeftSideWindow(index, out HudWindow window) && window.IsOpen;
    }
    public bool IsDevelopmentWindowCatalogReadyForTests => developmentWindow != null
        && developmentWindowText != null
        && developmentSupplierTabRoot != null
        && developmentTreeRoot != null
        && !string.IsNullOrWhiteSpace(developmentWindowText.text)
        && developmentWindowText.text.Contains("Корабли развития")
        && developmentWindowSupplierCount >= 6
        && developmentWindowTileCount >= 15
        && developmentWindowConnectionCount >= 14;
    public string DevelopmentWindowTextForTests => CollectWindowTextForTests(developmentWindow);
    public int DevelopmentWindowSupplierCountForTests => developmentWindowSupplierCount;
    public int DevelopmentWindowTileCountForTests => developmentWindowTileCount;
    public int DevelopmentWindowTierColumnCountForTests => DevelopmentTreeTierCount;
    public int DevelopmentWindowConnectionCountForTests => developmentWindowConnectionCount;

    private static string CollectWindowTextForTests(HudWindow window)
    {
        if (window == null || window.root == null)
        {
            return "";
        }

        Text[] texts = window.root.GetComponentsInChildren<Text>(true);
        StringBuilder builder = new StringBuilder(4096);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null || string.IsNullOrWhiteSpace(texts[i].text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(texts[i].text);
        }

        return builder.ToString();
    }

    public bool OpenMetaAllTasksWindowForTests()
    {
        if (metaAllTasksWindow == null)
        {
            return false;
        }

        metaAllTasksWindow.SetOpen(true);
        RefreshState(true);
        return metaAllTasksWindow.IsOpen;
    }

    public bool IsMetaAllTasksWindowOpenForTests => metaAllTasksWindow != null && metaAllTasksWindow.IsOpen;
    public bool IsMetaAllTasksWindowModalForTests => metaAllTasksWindow != null && metaAllTasksWindow.IsModalForTests;

    public bool CloseMetaAllTasksWindowByBackdropForTests()
    {
        if (metaAllTasksWindow == null)
        {
            return false;
        }

        bool closed = metaAllTasksWindow.CloseByBackdropForTests();
        RefreshState(true);
        return closed;
    }

    public string GetMetaQuestCounterForTests(int index)
    {
        return metaQuestCounterTexts != null
            && index >= 0
            && index < metaQuestCounterTexts.Length
            && metaQuestCounterTexts[index] != null
            ? metaQuestCounterTexts[index].text
            : "";
    }

    private bool AreMetaResourceCounterControlsReadyForTests()
    {
        for (int i = 0; i < MetaResourceCounterCount; i++)
        {
            if (metaResourceCounterAmountTexts[i] == null
                || metaResourceCounterPlusButtons[i] == null
                || metaResourceCounterIconImages[i] == null
                || string.IsNullOrWhiteSpace(metaResourceCounterAmountTexts[i].text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRightAnchoredForTests(RectTransform rect)
    {
        return rect != null
            && Mathf.Approximately(rect.anchorMin.x, 1f)
            && Mathf.Approximately(rect.anchorMax.x, 1f)
            && Mathf.Approximately(rect.pivot.x, 1f)
            && rect.anchoredPosition.x < 0f;
    }

    private static bool IsBottomRightAnchoredForTests(RectTransform rect)
    {
        return rect != null
            && Mathf.Approximately(rect.anchorMin.x, 1f)
            && Mathf.Approximately(rect.anchorMax.x, 1f)
            && Mathf.Approximately(rect.anchorMin.y, 0f)
            && Mathf.Approximately(rect.anchorMax.y, 0f)
            && Mathf.Approximately(rect.pivot.x, 1f)
            && Mathf.Approximately(rect.pivot.y, 0f)
            && rect.anchoredPosition.x < 0f
            && rect.anchoredPosition.y > 0f;
    }

    private static bool IsBottomLeftAnchoredForTests(RectTransform rect)
    {
        return rect != null
            && Mathf.Approximately(rect.anchorMin.x, 0f)
            && Mathf.Approximately(rect.anchorMax.x, 0f)
            && Mathf.Approximately(rect.anchorMin.y, 0f)
            && Mathf.Approximately(rect.anchorMax.y, 0f)
            && Mathf.Approximately(rect.pivot.x, 0f)
            && Mathf.Approximately(rect.pivot.y, 0f)
            && rect.anchoredPosition.x >= 0f
            && rect.anchoredPosition.y > 0f;
    }

    private bool AreMetaTopRightButtonControlsReadyForTests()
    {
        for (int i = 0; i < MetaTopRightButtonCount; i++)
        {
            if (metaTopRightButtons[i] == null
                || metaTopRightButtonIconTexts[i] == null
                || string.IsNullOrWhiteSpace(metaTopRightButtonIconTexts[i].text))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreMetaLeftSideButtonControlsReadyForTests()
    {
        for (int i = 0; i < MetaLeftSideButtonCount; i++)
        {
            if (metaLeftSideButtons[i] == null
                || metaLeftSideButtonIconTexts[i] == null
                || string.IsNullOrWhiteSpace(metaLeftSideButtonIconTexts[i].text))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreMainHudIconSpritesReadyForTests()
    {
        for (int i = 0; i < MainHudRequiredIconResourceNames.Length; i++)
        {
            if (GetMainHudIconSprite(MainHudRequiredIconResourceNames[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreMetaQuestRowsReadyForTests()
    {
        int count = GetMetaQuestVisibleCount();
        if (count < 1 || count > MetaQuestMaxCount)
        {
            return false;
        }

        for (int i = 0; i < MetaQuestMaxCount; i++)
        {
            bool shouldBeVisible = i < count;
            if (metaQuestRows[i] == null
                || metaQuestRows[i].gameObject.activeSelf != shouldBeVisible
                || metaQuestIconTexts[i] == null
                || metaQuestLineOneTexts[i] == null
                || metaQuestLineTwoTexts[i] == null
                || metaQuestCounterTexts[i] == null
                || metaQuestProgressFills[i] == null)
            {
                return false;
            }

            if (shouldBeVisible
                && (string.IsNullOrWhiteSpace(metaQuestIconTexts[i].text)
                    || string.IsNullOrWhiteSpace(metaQuestLineOneTexts[i].text)
                    || string.IsNullOrWhiteSpace(metaQuestLineTwoTexts[i].text)
                    || string.IsNullOrWhiteSpace(metaQuestCounterTexts[i].text)))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreMetaProjectCardsReadyForTests()
    {
        int count = GetMetaProjectVisibleCount();
        if (count < 1 || count > MetaProjectMaxCount)
        {
            return false;
        }

        for (int i = 0; i < MetaProjectMaxCount; i++)
        {
            bool shouldBeVisible = i < count;
            if (metaProjectCards[i] == null
                || metaProjectCards[i].gameObject.activeSelf != shouldBeVisible
                || metaProjectProgressFills[i] == null
                || metaProjectTitleTexts[i] == null
                || metaProjectAmountTexts[i] == null
                || metaProjectIconTexts[i] == null
                || metaProjectTimerTexts[i] == null)
            {
                return false;
            }

            if (shouldBeVisible)
            {
                RectTransform fill = metaProjectProgressFills[i];
                if (string.IsNullOrWhiteSpace(metaProjectTitleTexts[i].text)
                    || string.IsNullOrWhiteSpace(metaProjectAmountTexts[i].text)
                    || string.IsNullOrWhiteSpace(metaProjectIconTexts[i].text)
                    || string.IsNullOrWhiteSpace(metaProjectTimerTexts[i].text)
                    || !Mathf.Approximately(fill.anchorMin.x, 0f)
                    || !Mathf.Approximately(fill.anchorMin.y, 0f)
                    || !Mathf.Approximately(fill.anchorMax.y, 1f)
                    || fill.anchorMax.x < 0f
                    || fill.anchorMax.x > 1f)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool AreMetaTopRightWindowsReadyForTests()
    {
        for (int i = 0; i < MetaTopRightButtonCount; i++)
        {
            if (!TryGetMetaTopRightWindow(i, out HudWindow window)
                || window.content == null)
            {
                return false;
            }

            bool settingsWindowSlot = i == 3;
            if (settingsWindowSlot)
            {
                if (settingsResetProgressButton == null
                    || settingsResetProgressText == null
                    || settingsResetProgressButton.transform.parent != window.content)
                {
                    return false;
                }

                continue;
            }

            if (window.content.childCount != 0)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreMetaLeftSideWindowsReadyForTests()
    {
        for (int i = 0; i < MetaLeftSideButtonCount; i++)
        {
            if (!TryGetMetaLeftSideWindow(i, out HudWindow window)
                || window.content == null)
            {
                return false;
            }

            string windowId = MetaLeftSideButtons[i].windowId;
            if (windowId == WindowDevelopmentId)
            {
                if (developmentWindowText == null || developmentWindowText.transform.parent != window.content)
                {
                    return false;
                }

                continue;
            }

            if (windowId == WindowCargoId)
            {
                if (cargoHeaderText == null || cargoHeaderText.transform.parent != window.content)
                {
                    return false;
                }

                continue;
            }

            if (window.content.childCount != 0)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreOpenHudWindowsModal()
    {
        bool hasOpenWindow = false;
        foreach (KeyValuePair<string, HudWindow> pair in hudWindows)
        {
            HudWindow window = pair.Value;
            if (window == null || !window.IsOpen)
            {
                continue;
            }

            hasOpenWindow = true;
            if (!window.IsModalForTests)
            {
                return false;
            }
        }

        return hasOpenWindow;
    }

    private bool TryGetMetaTopRightWindow(int index, out HudWindow window)
    {
        window = null;
        if (index < 0 || index >= MetaTopRightButtons.Length)
        {
            return false;
        }

        return hudWindows.TryGetValue(MetaTopRightButtons[index].windowId, out window) && window != null;
    }

    private bool TryGetMetaLeftSideWindow(int index, out HudWindow window)
    {
        window = null;
        if (index < 0 || index >= MetaLeftSideButtons.Length)
        {
            return false;
        }

        return hudWindows.TryGetValue(MetaLeftSideButtons[index].windowId, out window) && window != null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallGameplayHudBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureHudForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureHudForGameplayScene();
    }

    private static void EnsureHudForGameplayScene()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<WildWindGameplayHud>() != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(MainHudPrefabResourcePath);
        GameObject hudObject = prefab != null
            ? UnityEngine.Object.Instantiate(prefab)
            : new GameObject(HudObjectName);
        hudObject.name = HudObjectName;
        if (hudObject.GetComponent<WildWindGameplayHud>() == null)
        {
            hudObject.AddComponent<WildWindGameplayHud>();
        }
    }

    private void Awake()
    {
        ResolveMeta();
        ResolveSession();
        BuildUi();
        RefreshTexts();
        RefreshState(true);
    }

    private void OnEnable()
    {
        WildWindLocalization.LanguageChanged += RefreshTexts;
    }

    private void OnDisable()
    {
        hudPointerCaptureActiveForCamera = false;
        WildWindLocalization.LanguageChanged -= RefreshTexts;
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null || currentMeta.CurrentMode != GameSessionMode.Flight || !currentMeta.HasActiveSortie)
        {
            ResolveFlightControls()?.ClearDirectInput();
        }
    }

    private void Update()
    {
        PushHeldFlightInput();
        RefreshCompass();
        RefreshEnginePowerBar();

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + 0.2f;
        RefreshState(false);
    }

    public bool TryDockNearest()
    {
        WildWindGameplaySession currentSession = ResolveSession();
        MetaGameState currentMeta = ResolveMeta();
        if (currentSession == null)
        {
            statusMessage = "Сессия мира не найдена.";
            RefreshState(true);
            return false;
        }

        if (currentMeta != null && currentMeta.HasActiveSortie)
        {
            bool extracted = currentMeta.TryExtractActiveSortie(out statusMessage);
            currentSession.RefreshFromMetaProgress();
            RefreshState(true);
            return extracted;
        }

        bool result = currentSession.TryDockAtNearestAvailableDock(out statusMessage);
        RefreshState(true);
        return result;
    }

    public void RefreshCompassForTests()
    {
        RefreshCompass();
    }

    public void OpenMenu()
    {
        WildWindGameplayMenu menu = UnityEngine.Object.FindFirstObjectByType<WildWindGameplayMenu>();
        if (menu != null)
        {
            menu.SetOpen(true);
        }
    }

    public bool OpenKnowledgeScreenForTests()
    {
        return OpenKnowledgeScreen();
    }

    public bool RunKnowledgePrimaryActionForTests()
    {
        return knowledgeScreen != null && knowledgeScreen.RunPrimaryActionForTests();
    }

    public bool RunKnowledgeSecondaryActionForTests()
    {
        return knowledgeScreen != null && knowledgeScreen.RunSecondaryActionForTests();
    }

    public bool CloseKnowledgeScreenForTests()
    {
        if (knowledgeScreen == null)
        {
            return true;
        }

        knowledgeScreen.SetVisible(false);
        RefreshState(true);
        return !knowledgeScreen.IsVisible;
    }

    public bool OpenMetaDockScreenForTests()
    {
        return OpenMetaDockScreen();
    }

    public bool PressMetaDockShipSlotForTests(int slotIndex)
    {
        return HandleMetaDockSlotSelected(slotIndex);
    }

    public bool OpenMetaDockScreenForRuntime()
    {
        return OpenMetaDockScreen();
    }

    public bool OpenKnowledgeScreenForRuntime()
    {
        return OpenKnowledgeScreen();
    }

    public bool ReturnFromMetaDockScreenToPortForTests()
    {
        return ReturnFromMetaDockScreenToPort();
    }

    public bool BuySelectedDevelopmentShipForTests()
    {
        return HandleDevelopmentBuyShip(selectedDevelopmentShipId);
    }

    public bool RunQuickDockSortieForTests()
    {
        return HandleMetaDockQuickBattle();
    }

    public bool RunCoreCombatDockSortieForTests()
    {
        return HandleMetaDockCoreCombatSortie();
    }

    public bool CloseMetaDockResultWindowForTests()
    {
        HideMetaDockSortieResultWindow();
        return metaDockResultModalRoot == null || !metaDockResultModalRoot.gameObject.activeSelf;
    }

    public bool SelectDockMissionForTests(int offerIndex)
    {
        return HandleMetaDockMissionSelected(offerIndex);
    }

    public bool RunSelectedOrdinaryDockMissionForTests()
    {
        return HandleMetaDockRunSelectedMission();
    }

    public bool RunFirstOrdinaryDockMissionForTests()
    {
        return HandleMetaDockMissionSelected(0) && HandleMetaDockRunSelectedMission();
    }

    public bool SellSelectedDockShipForTests()
    {
        return HandleMetaDockSellShip();
    }

    public bool OpenDevelopmentWindowForTests()
    {
        return OpenDevelopmentWindow();
    }

    public bool IsDevelopmentWindowOpenForTests => IsHudWindowOpen(WindowDevelopmentId);
    public bool IsDevelopmentWindowVisibleForTests => developmentWindow != null
        && developmentWindow.IsOpen
        && developmentWindow.root != null
        && developmentWindow.root.gameObject.activeInHierarchy;

    public bool DragDevelopmentTreeForTests(PointerEventData.InputButton button, Vector2 delta)
    {
        if (developmentTreeViewport == null)
        {
            return false;
        }

        DevelopmentTreeMousePanHandler handler = developmentTreeViewport.GetComponent<DevelopmentTreeMousePanHandler>();
        if (handler == null)
        {
            return false;
        }

        float beforeX = developmentTreeScrollRect != null ? developmentTreeScrollRect.horizontalNormalizedPosition : 0f;
        float beforeY = developmentTreeScrollRect != null ? developmentTreeScrollRect.verticalNormalizedPosition : 0f;
        EventSystem eventSystem = EventSystem.current;
        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            button = button,
            delta = delta
        };

        handler.OnPointerDown(eventData);
        bool capturedDuringDrag = IsHudPointerCaptureActiveForCamera;
        handler.OnDrag(eventData);
        handler.OnPointerUp(eventData);

        float afterX = developmentTreeScrollRect != null ? developmentTreeScrollRect.horizontalNormalizedPosition : beforeX;
        float afterY = developmentTreeScrollRect != null ? developmentTreeScrollRect.verticalNormalizedPosition : beforeY;
        return capturedDuringDrag
            && !IsHudPointerCaptureActiveForCamera
            && (Mathf.Abs(afterX - beforeX) > 0.0001f || Mathf.Abs(afterY - beforeY) > 0.0001f);
    }

    public bool OpenResourceCatalogWindowForTests()
    {
        return OpenResourceCatalogWindow();
    }

    public void SetBaseBuildingFocusActive(bool active)
    {
        if (baseBuildingFocusActive == active)
        {
            return;
        }

        baseBuildingFocusActive = active;
        RefreshState(true);
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 850;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateRect("Gameplay HUD Root", canvas.transform, StretchFull()).GetComponent<RectTransform>();

        RectTransform topBand = CreateRect("Session Status Strip", root, StretchFull()).GetComponent<RectTransform>();

        BuildPlayerProfile(topBand);
        BuildMetaResourceCounters(topBand);
        BuildMetaTopRightButtons(topBand);
        BuildMetaLeftSideButtons(root);
        BuildMetaQuestPanel(root);
        BuildMetaProjectPanel(root);
        BuildMetaDockButton(root);
        modeText = CreateText(topBand, "", 28, new Vector2(24f, -12f), new Vector2(240f, 34f), TextAnchor.MiddleLeft, new Color(0.95f, 0.81f, 0.52f, 1f));
        objectiveText = CreateText(topBand, "", 24, new Vector2(280f, -14f), new Vector2(760f, 32f), TextAnchor.MiddleLeft, new Color(0.91f, 0.88f, 0.78f, 1f));
        sessionPathText = CreateText(topBand, "", 20, new Vector2(280f, -50f), new Vector2(760f, 28f), TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 0.82f, 1f));
        resourcesText = CreateButton(topBand, "Resources", new Vector2(1210f, -29f), new Vector2(154f, 34f), ToggleResourcesWindow, out resourcesButton);
        distanceText = CreateText(topBand, "", 22, new Vector2(1480f, -30f), new Vector2(300f, 34f), TextAnchor.MiddleRight, new Color(0.86f, 0.80f, 0.64f, 1f));
        modeText.raycastTarget = false;
        objectiveText.raycastTarget = false;
        sessionPathText.raycastTarget = false;
        distanceText.raycastTarget = false;

        BuildCompass();

        flightPanel = CreatePanel("Flight Panel", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 34f),
            sizeDelta = new Vector2(-84f, 306f)
        }, new Color(0.018f, 0.022f, 0.026f, 0.78f));

        altitudeText = CreateText(flightPanel, "", 22, new Vector2(26f, -20f), new Vector2(190f, 32f), TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.92f, 1f));
        speedText = CreateText(flightPanel, "", 22, new Vector2(26f, -58f), new Vector2(300f, 32f), TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.92f, 1f));
        enginePowerText = CreateText(flightPanel, "", 17, new Vector2(1668f, -60f), new Vector2(170f, 24f), TextAnchor.MiddleCenter, new Color(0.92f, 0.90f, 0.82f, 1f));
        BuildEnginePowerBar(flightPanel);
        dockText = CreateButton(flightPanel, "Dock", new Vector2(1280f, -22f), new Vector2(220f, 54f), TryDockNearest, out dockButton);
        flightMenuText = CreateButton(flightPanel, "Menu Flight", new Vector2(1520f, -22f), new Vector2(180f, 54f), () => { OpenMenu(); return true; }, out _);
        claudiumSlipstreamText = CreateButton(flightPanel, "Claudium Slipstream", new Vector2(1520f, -86f), new Vector2(180f, 36f), ToggleClaudiumSlipstream, out _);

        sortiePanel = CreatePanel("Sortie Overview", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(42f, -166f),
            sizeDelta = new Vector2(700f, 258f)
        }, new Color(0.016f, 0.019f, 0.022f, 0.76f));

        sortieTitleText = CreateText(sortiePanel, "", 21, new Vector2(20f, -12f), new Vector2(660f, 28f), TextAnchor.MiddleLeft, new Color(0.95f, 0.81f, 0.52f, 1f));
        sortieTitleText.fontStyle = FontStyle.Bold;
        sortieZoneText = CreateText(sortiePanel, "", 16, new Vector2(20f, -44f), new Vector2(660f, 24f), TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.92f, 1f));
        sortieNavigationText = CreateText(sortiePanel, "", 16, new Vector2(20f, -72f), new Vector2(660f, 24f), TextAnchor.MiddleLeft, new Color(0.72f, 0.84f, 0.88f, 1f));
        sortieExtractionText = CreateText(sortiePanel, "", 16, new Vector2(20f, -100f), new Vector2(660f, 24f), TextAnchor.MiddleLeft, new Color(0.92f, 0.82f, 0.58f, 1f));
        sortieReservesText = CreateText(sortiePanel, "", 16, new Vector2(20f, -128f), new Vector2(660f, 24f), TextAnchor.MiddleLeft, new Color(0.90f, 0.88f, 0.76f, 1f));
        sortieCargoText = CreateText(sortiePanel, "", 16, new Vector2(20f, -156f), new Vector2(660f, 24f), TextAnchor.MiddleLeft, new Color(0.74f, 0.86f, 0.74f, 1f));
        sortieShipText = CreateText(sortiePanel, "", 16, new Vector2(20f, -184f), new Vector2(660f, 24f), TextAnchor.MiddleLeft, new Color(0.80f, 0.84f, 0.90f, 1f));
        sortieStatusDetailText = CreateText(sortiePanel, "", 15, new Vector2(20f, -214f), new Vector2(660f, 32f), TextAnchor.UpperLeft, new Color(0.95f, 0.75f, 0.42f, 1f));

        statusText = CreateText(root, "", 20, new Vector2(42f, -126f), new Vector2(760f, 32f), TextAnchor.MiddleLeft, new Color(0.95f, 0.75f, 0.42f, 1f));
        if (sortiePanel != null)
        {
            sortiePanel.gameObject.SetActive(false);
        }

        BuildHudWindows();
        BuildMetaDockScreen(root);
    }

    private void BuildHudWindows()
    {
        windowDockPanel = CreatePanel("Window Dock", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(24f, 0f),
            sizeDelta = new Vector2(116f, 498f)
        }, new Color(0.012f, 0.014f, 0.017f, 0.74f));

        CreateWindowDockButton(WindowLocationId, "Где я", 14f);
        CreateWindowDockButton(WindowReturnId, "Возврат", -28f);
        CreateWindowDockButton(WindowShipId, "Корабль", -70f);
        CreateWindowDockButton(WindowTasksId, "Задачи", -112f);
        CreateWindowDockButton(WindowCargoId, "Трюм", -154f);
        CreateWindowDockButton(WindowRadarId, "Радар", -196f);
        CreateWindowDockButton(WindowModulesId, "Модули", -238f);
        CreateWindowDockButton(WindowResourcesId, "Ресурсы", -280f);

        locationWindow = CreateHudWindow(WindowLocationId, "ГДЕ Я", new Vector2(154f, -136f), new Vector2(430f, 178f), false);
        locationWindowText = CreateText(locationWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(390f, 124f), TextAnchor.UpperLeft, new Color(0.82f, 0.90f, 0.92f, 1f));

        returnWindow = CreateHudWindow(WindowReturnId, "ВОЗВРАТ", new Vector2(1450f, -136f), new Vector2(420f, 188f), false);
        returnWindowText = CreateText(returnWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(386f, 134f), TextAnchor.UpperLeft, new Color(0.90f, 0.86f, 0.70f, 1f));

        shipWindow = CreateHudWindow(WindowShipId, "КОРАБЛЬ", new Vector2(154f, -326f), new Vector2(430f, 198f), false);
        shipWindowText = CreateText(shipWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(390f, 144f), TextAnchor.UpperLeft, new Color(0.84f, 0.90f, 0.86f, 1f));

        tasksWindow = CreateHudWindow(WindowTasksId, "ЗАДАНИЯ", new Vector2(154f, -538f), new Vector2(430f, 168f), false);
        tasksWindowText = CreateText(tasksWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(390f, 114f), TextAnchor.UpperLeft, new Color(0.92f, 0.84f, 0.60f, 1f));

        radarWindow = CreateHudWindow(WindowRadarId, "РАДАР", new Vector2(1390f, -338f), new Vector2(480f, 312f), false);
        radarBoulderFilterText = CreateButton(radarWindow.content, "Radar Boulders", new Vector2(12f, -10f), new Vector2(96f, 28f), ToggleRadarBoulders, out _);
        radarDebrisFilterText = CreateButton(radarWindow.content, "Radar Debris", new Vector2(116f, -10f), new Vector2(104f, 28f), ToggleRadarDebris, out _);
        radarWindowText = CreateText(radarWindow.content, "", 14, new Vector2(12f, -48f), new Vector2(430f, 212f), TextAnchor.UpperLeft, new Color(0.80f, 0.88f, 0.90f, 1f));

        modulesWindow = CreateHudWindow(WindowModulesId, "МОДУЛИ", new Vector2(620f, -740f), new Vector2(570f, 178f), false);
        modulesWindowText = CreateText(modulesWindow.content, "", 15, new Vector2(12f, -12f), new Vector2(532f, 126f), TextAnchor.UpperLeft, new Color(0.88f, 0.84f, 0.66f, 1f));

        resourcesWindow = CreateHudWindow(WindowResourcesId, "РЕСУРСЫ", new Vector2(620f, -126f), new Vector2(680f, 620f), false);
        resourceCatalogHeaderText = CreateText(resourcesWindow.content, "", 15, new Vector2(12f, -8f), new Vector2(620f, 24f), TextAnchor.MiddleLeft, new Color(0.92f, 0.86f, 0.66f, 1f));
        BuildResourceCatalogScroll(resourcesWindow.content);

        encyclopediaWindow = CreateMetaTopRightWindow(0);
        achievementsWindow = CreateMetaTopRightWindow(1);
        mailWindow = CreateMetaTopRightWindow(2);
        settingsWindow = CreateMetaTopRightWindow(3);
        BuildSettingsWindowContent(settingsWindow.content);
        offerWindow = CreateHudWindow(WindowOfferId, "МАГАЗИН", new Vector2(1180f, -150f), new Vector2(520f, 360f), false);
        sideEventsWindow = CreateHudWindow(WindowSideEventsId, "СОБЫТИЯ", new Vector2(1180f, -150f), new Vector2(520f, 360f), false);
        factionsWindow = CreateHudWindow(WindowFactionsId, "ТОРГОВЦЫ", new Vector2(1180f, -150f), new Vector2(520f, 360f), false);
        developmentWindow = CreateHudWindow(WindowDevelopmentId, "РАЗВИТИЕ", new Vector2(0f, 0f), new Vector2(1880f, 980f), false);
        BuildDevelopmentWindowContent(developmentWindow.content);
        metaAllTasksWindow = CreateHudWindow(WindowMetaAllTasksId, "ЗАДАЧИ", new Vector2(480f, -150f), new Vector2(520f, 360f), false);

        cargoWindow = CreateHudWindow(WindowCargoId, "ТРЮМ", new Vector2(790f, -336f), new Vector2(520f, 342f), false);
        cargoHeaderText = CreateText(cargoWindow.content, "", 16, new Vector2(12f, -10f), new Vector2(470f, 26f), TextAnchor.MiddleLeft, new Color(0.84f, 0.90f, 0.86f, 1f));
        BuildCargoGrid(cargoWindow.content);

    }

    private void BuildPlayerProfile(RectTransform parent)
    {
        playerProfilePanel = CreateGlassPanel("Meta Player Profile", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(20f, -20f),
            sizeDelta = new Vector2(400f, 110f)
        }, PortGlassStrong);

        RectTransform avatar = CreatePanel("Profile Icon", playerProfilePanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(0f, 0f),
            sizeDelta = new Vector2(110f, 110f)
        }, new Color(0.82f, 0.89f, 0.96f, 1f));
        Image avatarImage = avatar.GetComponent<Image>();
        if (avatarImage != null)
        {
            avatarImage.sprite = GetCircleSprite();
            avatarImage.type = Image.Type.Simple;
        }
        AddSoftShadow(avatar, new Color(0.18f, 0.26f, 0.36f, 0.22f), new Vector2(0f, -3f));

        Text avatarText = CreateText(avatar, MetaProfileInitialsPlaceholder, 22, Vector2.zero, new Vector2(110f, 110f), TextAnchor.MiddleCenter, PortText);
        avatarText.fontStyle = FontStyle.Bold;

        playerProfileTitleText = CreateText(playerProfilePanel, "", 20, new Vector2(120f, -22f), new Vector2(250f, 30f), TextAnchor.MiddleLeft, PortText);
        playerProfileTitleText.fontStyle = FontStyle.Bold;

        playerProfileMasteryCurrentText = CreateText(playerProfilePanel, "", 15, new Vector2(122f, -64f), new Vector2(38f, 24f), TextAnchor.MiddleLeft, PortText);
        playerProfileMasteryNextText = CreateText(playerProfilePanel, "", 15, new Vector2(350f, -64f), new Vector2(30f, 24f), TextAnchor.MiddleRight, PortTextSoft);

        RectTransform masteryBar = CreatePanel("Profile Mastery Bar", playerProfilePanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(168f, -70f),
            sizeDelta = new Vector2(174f, 16f)
        }, PortTrack);
        ApplyRoundedSprite(masteryBar.GetComponent<Image>(), GetRoundedSmallSprite());

        playerProfileMasteryFill = CreatePanel("Profile Mastery Fill", masteryBar, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        }, new Color(0.93f, 0.74f, 0.36f, 1f));
        ApplyRoundedSprite(playerProfileMasteryFill.GetComponent<Image>(), GetRoundedSmallSprite());

        RefreshPlayerProfile(true);
    }

    private void BuildMetaResourceCounters(RectTransform parent)
    {
        float totalWidth = MetaResourceCounterCount * MetaResourceCounterWidth
            + (MetaResourceCounterCount - 1) * MetaResourceCounterGap;
        metaResourceCounterStrip = CreateGlassPanel("Meta Resource Counters", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 1f),
            anchorMax = new Vector2(0.5f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(110f, -20f),
            sizeDelta = new Vector2(totalWidth, MetaResourceCounterHeight)
        }, PortGlassStrong);

        metaResourceCounterAmountTexts = new Text[MetaResourceCounterCount];
        metaResourceCounterPlusButtons = new Button[MetaResourceCounterCount];
        metaResourceCounterIconImages = new Image[MetaResourceCounterCount];

        for (int i = 0; i < MetaResourceCounterCount; i++)
        {
            MetaResourceCounterSpec spec = MetaResourceCounters[i];
            RectTransform counter = CreatePanel("Meta Resource Counter " + spec.itemId, metaResourceCounterStrip, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(i * (MetaResourceCounterWidth + MetaResourceCounterGap), 0f),
                sizeDelta = new Vector2(MetaResourceCounterWidth, MetaResourceCounterHeight)
            }, new Color(1f, 1f, 1f, 0f));

            if (i > 0)
            {
                CreatePanel("Resource Counter Divider", counter, new RectTransformSpec
                {
                    anchorMin = new Vector2(0f, 0.5f),
                    anchorMax = new Vector2(0f, 0.5f),
                    pivot = new Vector2(0.5f, 0.5f),
                    anchoredPosition = new Vector2(0f, 0f),
                    sizeDelta = new Vector2(1f, 48f)
                }, new Color(0.55f, 0.63f, 0.74f, 0.32f));
            }

            RectTransform iconRoot = CreatePanel("Icon " + spec.itemId, counter, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(18f, -12f),
                sizeDelta = new Vector2(46f, 46f)
            }, spec.fallbackColor);
            ApplyRoundedSprite(iconRoot.GetComponent<Image>(), GetRoundedSmallSprite());
            AddSoftShadow(iconRoot, new Color(0.18f, 0.24f, 0.34f, 0.18f), new Vector2(0f, -2f));

            Image iconImage = iconRoot.GetComponent<Image>();
            Sprite sprite = WildWindResourceIconCatalog.LoadSprite(spec.itemId);
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }

            Text fallbackIconText = CreateText(iconRoot, sprite == null ? spec.fallbackText : "", 12, Vector2.zero, new Vector2(46f, 46f), TextAnchor.MiddleCenter, new Color(0.08f, 0.08f, 0.07f, 1f));
            fallbackIconText.fontStyle = FontStyle.Bold;
            fallbackIconText.raycastTarget = false;

            Text amountText = CreateText(counter, "", 24, new Vector2(76f, -13f), new Vector2(78f, 42f), TextAnchor.MiddleLeft, PortText);
            amountText.fontStyle = FontStyle.Bold;
            metaResourceCounterAmountTexts[i] = amountText;
            metaResourceCounterIconImages[i] = iconImage;

            Text plusText = CreateGlassButton(counter, "Meta Resource Plus " + spec.itemId, new Vector2(154f, -16f), new Vector2(34f, 34f), HandleMetaResourcePlus, out Button plusButton, "+");
            plusText.fontSize = 24;
            plusText.fontStyle = FontStyle.Bold;
            metaResourceCounterPlusButtons[i] = plusButton;
        }

        RefreshMetaResourceCounters(true);
    }

    private void BuildMetaTopRightButtons(RectTransform parent)
    {
        float totalWidth = MetaTopRightButtonCount * MetaTopRightButtonSize
            + (MetaTopRightButtonCount - 1) * MetaTopRightButtonGap;
        metaTopRightButtonStrip = CreateRect("Meta Top Right Buttons", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(1f, 1f),
            anchoredPosition = new Vector2(-22f, -20f),
            sizeDelta = new Vector2(totalWidth, MetaTopRightButtonSize)
        }).GetComponent<RectTransform>();

        metaTopRightButtons = new Button[MetaTopRightButtonCount];
        metaTopRightButtonIconTexts = new Text[MetaTopRightButtonCount];
        for (int i = 0; i < MetaTopRightButtonCount; i++)
        {
            MetaTopRightButtonSpec spec = MetaTopRightButtons[i];
            GameObject buttonObject = CreateRect("Meta Top Button " + spec.windowId, metaTopRightButtonStrip, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(i * (MetaTopRightButtonSize + MetaTopRightButtonGap), 0f),
                sizeDelta = new Vector2(MetaTopRightButtonSize, MetaTopRightButtonSize)
            });

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);
            ApplyRoundedSprite(image, GetRoundedSmallSprite());

            Button button = buttonObject.AddComponent<Button>();
            ConfigureGlassButton(button, image);
            string windowId = spec.windowId;
            button.onClick.AddListener(() => ToggleHudWindow(windowId));

            Text iconText = CreateText(buttonObject.GetComponent<RectTransform>(), spec.iconText, 40, Vector2.zero, new Vector2(MetaTopRightButtonSize, MetaTopRightButtonSize), TextAnchor.MiddleCenter, new Color(0.53f, 0.59f, 0.70f, 1f));
            iconText.fontStyle = FontStyle.Bold;
            iconText.raycastTarget = false;

            metaTopRightButtons[i] = button;
            metaTopRightButtonIconTexts[i] = iconText;
        }

        RefreshMetaTopRightButtons(true);
    }

    private void BuildMetaLeftSideButtons(RectTransform parent)
    {
        float totalHeight = MetaLeftSideButtonCount * MetaLeftSideButtonSize
            + (MetaLeftSideButtonCount - 1) * MetaLeftSideButtonGap;
        metaLeftSideButtonStrip = CreateRect("Meta Left Side Buttons", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(1f, 1f),
            anchoredPosition = new Vector2(-20f, -123f),
            sizeDelta = new Vector2(MetaLeftSideButtonSize, totalHeight)
        }).GetComponent<RectTransform>();

        metaLeftSideButtons = new Button[MetaLeftSideButtonCount];
        metaLeftSideButtonIconTexts = new Text[MetaLeftSideButtonCount];
        for (int i = 0; i < MetaLeftSideButtonCount; i++)
        {
            MetaSideButtonSpec spec = MetaLeftSideButtons[i];
            GameObject buttonObject = CreateRect("Meta Side Button " + spec.windowId, metaLeftSideButtonStrip, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(0f, -i * (MetaLeftSideButtonSize + MetaLeftSideButtonGap)),
                sizeDelta = new Vector2(MetaLeftSideButtonSize, MetaLeftSideButtonSize)
            });

            Image image = buttonObject.AddComponent<Image>();
            image.color = PortGlassStrong;
            ApplyRoundedSprite(image, GetRoundedSmallSprite());
            AddSoftShadow(buttonObject.GetComponent<RectTransform>(), new Color(0.18f, 0.26f, 0.38f, 0.16f), new Vector2(0f, -3f));

            Button button = buttonObject.AddComponent<Button>();
            ConfigureGlassButton(button, image);
            string windowId = spec.windowId;
            button.onClick.AddListener(() => ToggleHudWindow(windowId));

            Text iconText = CreateText(buttonObject.GetComponent<RectTransform>(), spec.iconText, 38, new Vector2(0f, -8f), new Vector2(MetaLeftSideButtonSize, 54f), TextAnchor.MiddleCenter, new Color(0.51f, 0.57f, 0.68f, 1f));
            iconText.fontStyle = FontStyle.Bold;
            iconText.raycastTarget = false;
            Image iconImage = CreateMainHudIconImage(
                buttonObject.GetComponent<RectTransform>(),
                "Meta Side Icon " + spec.windowId,
                spec.iconResourceName,
                new RectTransformSpec
                {
                    anchorMin = new Vector2(0.5f, 1f),
                    anchorMax = new Vector2(0.5f, 1f),
                    pivot = new Vector2(0.5f, 1f),
                    anchoredPosition = new Vector2(0f, -8f),
                    sizeDelta = new Vector2(78f, 64f)
                },
                Color.white);
            SetFallbackTextVisibleWhenIconMissing(iconText, iconImage);

            Text labelText = CreateText(buttonObject.GetComponent<RectTransform>(), spec.title, 18, new Vector2(6f, -72f), new Vector2(MetaLeftSideButtonSize - 12f, 28f), TextAnchor.MiddleCenter, PortText);
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;

            metaLeftSideButtons[i] = button;
            metaLeftSideButtonIconTexts[i] = iconText;
        }

        RefreshMetaLeftSideButtons(true);
    }

    private void BuildMetaDockButton(RectTransform parent)
    {
        GameObject buttonObject = CreateRect("Meta Dock Button", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(1f, 0f),
            anchoredPosition = new Vector2(-20f, 24f),
            sizeDelta = new Vector2(MetaDockButtonSize, MetaDockButtonSize)
        });

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = new Color(0.72f, 0.80f, 0.92f, 0.78f);
        AddSoftShadow(buttonObject.GetComponent<RectTransform>(), new Color(0.12f, 0.20f, 0.34f, 0.28f), new Vector2(0f, -6f));

        metaDockButton = buttonObject.AddComponent<Button>();
        metaDockButton.targetGraphic = image;
        ColorBlock colors = metaDockButton.colors;
        colors.normalColor = new Color(0.72f, 0.80f, 0.92f, 0.78f);
        colors.highlightedColor = new Color(0.84f, 0.90f, 1.00f, 0.95f);
        colors.pressedColor = new Color(0.60f, 0.70f, 0.86f, 0.98f);
        colors.disabledColor = new Color(0.45f, 0.50f, 0.58f, 0.45f);
        metaDockButton.colors = colors;
        metaDockButton.onClick.AddListener(() => HandleMetaDockButton());

        Text wheelText = CreateText(buttonObject.GetComponent<RectTransform>(), "\u2638", 86, new Vector2(0f, -8f), new Vector2(MetaDockButtonSize, 96f), TextAnchor.MiddleCenter, new Color(0.70f, 0.50f, 0.27f, 0.95f));
        wheelText.fontStyle = FontStyle.Bold;
        wheelText.raycastTarget = false;
        Image dockIconImage = CreateMainHudIconImage(
            buttonObject.GetComponent<RectTransform>(),
            "Meta Dock Icon",
            "MetaDock",
            new RectTransformSpec
            {
                anchorMin = new Vector2(0.5f, 0.5f),
                anchorMax = new Vector2(0.5f, 0.5f),
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = new Vector2(0f, 8f),
                sizeDelta = new Vector2(128f, 128f)
            },
            Color.white);
        SetFallbackTextVisibleWhenIconMissing(wheelText, dockIconImage);

        RectTransform labelPill = CreatePanel("Meta Dock Label", buttonObject.GetComponent<RectTransform>(), new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0f),
            anchorMax = new Vector2(0.5f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, -2f),
            sizeDelta = new Vector2(136f, 28f)
        }, PortGlassStrong);
        Image dockLabelImage = labelPill.GetComponent<Image>();
        ApplyRoundedSprite(dockLabelImage, GetRoundedSmallSprite());
        SetGraphicRaycastTarget(dockLabelImage, false);
        metaDockButtonText = CreateText(labelPill, "В док", 21, Vector2.zero, new Vector2(136f, 28f), TextAnchor.MiddleCenter, PortText);
        metaDockButtonText.fontStyle = FontStyle.Bold;
        metaDockButtonText.raycastTarget = false;

        RefreshMetaDockButton(true);
    }

    private void BuildMetaDockScreen(RectTransform parent)
    {
        metaDockScreenLayer = CreatePanel("Meta Dock Screen", parent, StretchFull(), new Color(0.014f, 0.020f, 0.026f, 1f));

        RectTransform topBand = CreatePanel("Dock Screen Top Band", metaDockScreenLayer, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 92f)
        }, new Color(0.026f, 0.032f, 0.036f, 1f));

        Text title = CreateText(topBand, "ДОК", 34, new Vector2(42f, -18f), new Vector2(360f, 48f), TextAnchor.MiddleLeft, new Color(0.95f, 0.84f, 0.62f, 1f));
        title.fontStyle = FontStyle.Bold;
        title.raycastTarget = false;

        metaDockHeaderText = CreateText(metaDockScreenLayer, "Купи корабль в развитии, поставь его в слот и нажми быстрый вылет.", 18, new Vector2(42f, -104f), new Vector2(1120f, 32f), TextAnchor.MiddleLeft, new Color(0.78f, 0.88f, 0.86f, 0.94f));
        metaDockManualSortieText = CreateButton(metaDockScreenLayer, "Dock Manual Quick Sortie", new Vector2(1180f, -92f), new Vector2(312f, 44f), HandleMetaDockManualSortie, out metaDockManualSortieButton);
        SetText(metaDockManualSortieText, "РУЧНОЙ ВЫЛЕТ");
        metaDockManualSortieText.fontSize = 19;
        metaDockManualSortieText.fontStyle = FontStyle.Bold;

        metaDockCoreCombatText = CreateButton(metaDockScreenLayer, "Dock Core Tactical Combat Sortie", new Vector2(1510f, -92f), new Vector2(312f, 44f), HandleMetaDockCoreCombatSortie, out metaDockCoreCombatButton);
        SetText(metaDockCoreCombatText, "CORE COMBAT");
        metaDockCoreCombatText.fontSize = 19;
        metaDockCoreCombatText.fontStyle = FontStyle.Bold;

        metaDockSelectedShipRoot = CreatePanel("Dock Selected Ship Panel", metaDockScreenLayer, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(42f, -154f),
            sizeDelta = new Vector2(650f, 540f)
        }, new Color(0.020f, 0.070f, 0.078f, 0.88f));

        metaDockSelectedShipText = CreateText(metaDockSelectedShipRoot, "", 24, new Vector2(24f, -22f), new Vector2(590f, 40f), TextAnchor.MiddleLeft, new Color(0.95f, 0.86f, 0.64f, 1f));
        metaDockSelectedShipText.fontStyle = FontStyle.Bold;
        metaDockSelectedStatsText = CreateText(metaDockSelectedShipRoot, "", 17, new Vector2(24f, -76f), new Vector2(596f, 300f), TextAnchor.UpperLeft, new Color(0.84f, 0.90f, 0.86f, 0.96f));

        metaDockQuickBattleText = CreateButton(metaDockSelectedShipRoot, "Dock Enter Core Combat", new Vector2(24f, -402f), new Vector2(284f, 56f), HandleMetaDockCoreCombatSortie, out metaDockQuickBattleButton);
        SetText(metaDockQuickBattleText, "В БОЙ");
        metaDockQuickBattleText.fontSize = 28;
        metaDockQuickBattleText.fontStyle = FontStyle.Bold;

        metaDockSellText = CreateButton(metaDockSelectedShipRoot, "Dock Sell Ship", new Vector2(324f, -402f), new Vector2(284f, 56f), HandleMetaDockSellShip, out metaDockSellButton);
        SetText(metaDockSellText, "ПРОДАТЬ");
        metaDockSellText.fontSize = 22;
        metaDockSellText.fontStyle = FontStyle.Bold;

        metaDockMissionRoot = CreatePanel("Dock Mission Panel", metaDockScreenLayer, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(720f, -154f),
            sizeDelta = new Vector2(552f, 540f)
        }, new Color(0.025f, 0.052f, 0.066f, 0.90f));

        metaDockMissionHeaderText = CreateText(metaDockMissionRoot, "ОБЫЧНЫЕ МИССИИ", 20, new Vector2(20f, -18f), new Vector2(330f, 30f), TextAnchor.MiddleLeft, new Color(0.95f, 0.84f, 0.62f, 1f));
        metaDockMissionHeaderText.fontStyle = FontStyle.Bold;
        metaDockMissionDetailText = CreateText(metaDockMissionRoot, "", 14, new Vector2(260f, -16f), new Vector2(270f, 46f), TextAnchor.UpperRight, new Color(0.80f, 0.88f, 0.86f, 0.95f));
        metaDockMissionTexts = new Text[15];
        metaDockMissionButtons = new Button[15];
        for (int i = 0; i < metaDockMissionTexts.Length; i++)
        {
            int offerIndex = i;
            float x = i < 8 ? 20f : 284f;
            float y = -64f - (i % 8) * 44f;
            Text missionText = CreateButton(metaDockMissionRoot, "Dock Mission Offer " + i, new Vector2(x, y), new Vector2(248f, 38f), () => HandleMetaDockMissionSelected(offerIndex), out Button missionButton);
            missionText.fontSize = 13;
            missionText.alignment = TextAnchor.MiddleLeft;
            metaDockMissionTexts[i] = missionText;
            metaDockMissionButtons[i] = missionButton;
        }

        metaDockRunMissionText = CreateButton(metaDockMissionRoot, "Dock Run Ordinary Mission", new Vector2(20f, -468f), new Vector2(512f, 46f), HandleMetaDockRunSelectedMission, out metaDockRunMissionButton);
        SetText(metaDockRunMissionText, "ВЫЛЕТ ПО ВЫБРАННОЙ МИССИИ");
        metaDockRunMissionText.fontSize = 19;
        metaDockRunMissionText.fontStyle = FontStyle.Bold;

        metaDockRewardRoot = CreatePanel("Dock Reward Panel", metaDockScreenLayer, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(1f, 1f),
            anchoredPosition = new Vector2(-42f, -154f),
            sizeDelta = new Vector2(570f, 540f)
        }, new Color(0.030f, 0.038f, 0.050f, 0.90f));

        Text rewardTitle = CreateText(metaDockRewardRoot, "ПОСЛЕДНИЙ ВЫЛЕТ", 20, new Vector2(22f, -20f), new Vector2(520f, 34f), TextAnchor.MiddleLeft, new Color(0.95f, 0.84f, 0.62f, 1f));
        rewardTitle.fontStyle = FontStyle.Bold;
        metaDockRewardText = CreateText(metaDockRewardRoot, "", 16, new Vector2(22f, -70f), new Vector2(522f, 430f), TextAnchor.UpperLeft, new Color(0.82f, 0.90f, 0.86f, 0.96f));

        metaDockShipSlotRoot = CreatePanel("Dock Ship Slot Strip", metaDockScreenLayer, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = new Vector2(42f, 42f),
            sizeDelta = new Vector2(960f, 150f)
        }, new Color(0.018f, 0.058f, 0.064f, 0.90f));

        Text slotTitle = CreateText(metaDockShipSlotRoot, "СЛОТЫ ДОКА", 18, new Vector2(20f, -12f), new Vector2(260f, 26f), TextAnchor.MiddleLeft, new Color(0.95f, 0.84f, 0.62f, 1f));
        slotTitle.fontStyle = FontStyle.Bold;
        metaDockSlotTexts = new Text[MetaGameState.DevelopmentDockSlotCount];
        metaDockSlotButtons = new Button[MetaGameState.DevelopmentDockSlotCount];
        for (int i = 0; i < MetaGameState.DevelopmentDockSlotCount; i++)
        {
            int slotIndex = i;
            Text slotText = CreateButton(metaDockShipSlotRoot, "Dock Ship Slot " + i, new Vector2(20f + i * 226f, -52f), new Vector2(206f, 72f), () => HandleMetaDockSlotSelected(slotIndex), out Button slotButton);
            slotText.fontSize = 15;
            slotText.alignment = TextAnchor.MiddleCenter;
            metaDockSlotTexts[i] = slotText;
            metaDockSlotButtons[i] = slotButton;
        }

        GameObject portButtonObject = CreateRect("Meta Dock Screen Port Button", metaDockScreenLayer, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(1f, 0f),
            anchoredPosition = new Vector2(-20f, 24f),
            sizeDelta = new Vector2(MetaDockButtonSize, MetaDockButtonSize)
        });

        Image image = portButtonObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.color = new Color(0.18f, 0.44f, 0.50f, 0.96f);

        metaDockScreenPortButton = portButtonObject.AddComponent<Button>();
        metaDockScreenPortButton.targetGraphic = image;
        ColorBlock colors = metaDockScreenPortButton.colors;
        colors.normalColor = new Color(0.18f, 0.44f, 0.50f, 0.96f);
        colors.highlightedColor = new Color(0.24f, 0.58f, 0.66f, 1f);
        colors.pressedColor = new Color(0.30f, 0.68f, 0.76f, 1f);
        colors.disabledColor = new Color(0.10f, 0.09f, 0.08f, 0.45f);
        metaDockScreenPortButton.colors = colors;
        metaDockScreenPortButton.onClick.AddListener(() => ReturnFromMetaDockScreenToPort());

        metaDockScreenPortButtonText = CreateText(portButtonObject.GetComponent<RectTransform>(), "ПОРТ", 28, Vector2.zero, new Vector2(MetaDockButtonSize, MetaDockButtonSize), TextAnchor.MiddleCenter, new Color(1f, 0.90f, 0.70f, 1f));
        metaDockScreenPortButtonText.fontStyle = FontStyle.Bold;
        metaDockScreenPortButtonText.raycastTarget = false;

        BuildMetaDockResultWindow();
        RefreshMetaDockScreen(false);
    }

    private void BuildMetaDockResultWindow()
    {
        if (metaDockScreenLayer == null)
        {
            return;
        }

        metaDockResultModalRoot = CreatePanel("Dock Sortie Result Modal", metaDockScreenLayer, StretchFull(), new Color(0.002f, 0.004f, 0.006f, 0.72f));
        metaDockResultModalRoot.SetAsLastSibling();

        RectTransform panel = CreatePanel("Dock Sortie Result Window", metaDockResultModalRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 0.5f),
            anchorMax = new Vector2(0.5f, 0.5f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = new Vector2(0f, 8f),
            sizeDelta = new Vector2(860f, 620f)
        }, new Color(0.020f, 0.040f, 0.050f, 0.98f));

        CreatePanel("Dock Sortie Result Top Accent", panel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 5f)
        }, new Color(0.80f, 0.54f, 0.20f, 1f));

        metaDockResultTitleText = CreateText(panel, "", 30, new Vector2(30f, -24f), new Vector2(660f, 42f), TextAnchor.MiddleLeft, new Color(0.96f, 0.86f, 0.62f, 1f));
        metaDockResultTitleText.fontStyle = FontStyle.Bold;
        metaDockResultSubtitleText = CreateText(panel, "", 16, new Vector2(32f, -70f), new Vector2(660f, 28f), TextAnchor.MiddleLeft, new Color(0.78f, 0.88f, 0.86f, 0.96f));

        metaDockResultCloseText = CreateButton(panel, "Dock Sortie Result Close", new Vector2(760f, -22f), new Vector2(68f, 44f), HideMetaDockSortieResultWindow, out metaDockResultCloseButton);
        SetText(metaDockResultCloseText, "OK");
        metaDockResultCloseText.fontSize = 20;
        metaDockResultCloseText.fontStyle = FontStyle.Bold;

        CreatePanel("Dock Sortie Result Divider", panel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -112f),
            sizeDelta = new Vector2(-60f, 2f)
        }, new Color(0.24f, 0.34f, 0.36f, 0.92f));

        metaDockResultBodyText = CreateText(panel, "", 18, new Vector2(34f, -134f), new Vector2(792f, 430f), TextAnchor.UpperLeft, new Color(0.86f, 0.92f, 0.88f, 0.98f));
        metaDockResultBodyText.lineSpacing = 1.08f;

        Text spentSortieText = CreateText(panel, "Списан 1 вылет корабля.", 16, new Vector2(34f, -576f), new Vector2(500f, 28f), TextAnchor.MiddleLeft, new Color(0.72f, 0.78f, 0.74f, 0.94f));
        spentSortieText.fontStyle = FontStyle.Italic;

        metaDockResultModalRoot.gameObject.SetActive(false);
    }

    private void BuildMetaQuestPanel(RectTransform parent)
    {
        metaQuestPanel = CreateGlassPanel("Meta Quest Panel", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(20f, -163f),
            sizeDelta = new Vector2(344f, 368f)
        }, PortGlass);

        Text header = CreateText(metaQuestPanel, "Задачи", 30, new Vector2(22f, -18f), new Vector2(220f, 40f), TextAnchor.MiddleLeft, PortText);
        header.fontStyle = FontStyle.Normal;
        header.raycastTarget = false;

        RectTransform arrow = CreatePanel("Meta Quest Arrow", metaQuestPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(1f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(1f, 1f),
            anchoredPosition = new Vector2(-14f, -12f),
            sizeDelta = new Vector2(48f, 48f)
        }, PortNavy);
        Image arrowImage = arrow.GetComponent<Image>();
        if (arrowImage != null)
        {
            arrowImage.sprite = GetCircleSprite();
            arrowImage.type = Image.Type.Simple;
        }
        Text arrowText = CreateText(arrow, ">", 34, Vector2.zero, new Vector2(48f, 48f), TextAnchor.MiddleCenter, Color.white);
        arrowText.fontStyle = FontStyle.Bold;
        arrowText.raycastTarget = false;

        CreatePanel("Meta Quest Header Line", metaQuestPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -68f),
            sizeDelta = new Vector2(-44f, 2f)
        }, new Color(0.48f, 0.56f, 0.68f, 0.45f));

        metaQuestRows = new RectTransform[MetaQuestMaxCount];
        metaQuestIconTexts = new Text[MetaQuestMaxCount];
        metaQuestLineOneTexts = new Text[MetaQuestMaxCount];
        metaQuestLineTwoTexts = new Text[MetaQuestMaxCount];
        metaQuestCounterTexts = new Text[MetaQuestMaxCount];
        metaQuestProgressFills = new RectTransform[MetaQuestMaxCount];

        for (int i = 0; i < MetaQuestMaxCount; i++)
        {
            float y = -82f - i * 76f;
            RectTransform row = CreatePanel("Meta Quest Row " + i, metaQuestPanel, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = new Vector2(0f, y),
                sizeDelta = new Vector2(-40f, 70f)
            }, new Color(1f, 1f, 1f, 0f));

            RectTransform icon = CreatePanel("Quest Icon", row, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(0f, -4f),
                sizeDelta = new Vector2(48f, 48f)
            }, PortGlassMuted);
            ApplyRoundedSprite(icon.GetComponent<Image>(), GetRoundedSmallSprite());

            metaQuestIconTexts[i] = CreateText(icon, "", 22, Vector2.zero, new Vector2(48f, 48f), TextAnchor.MiddleCenter, PortText);
            metaQuestIconTexts[i].fontStyle = FontStyle.Bold;
            metaQuestIconTexts[i].raycastTarget = false;
            metaQuestLineOneTexts[i] = CreateText(row, "", 15, new Vector2(60f, -1f), new Vector2(236f, 22f), TextAnchor.MiddleLeft, PortText);
            metaQuestLineTwoTexts[i] = CreateText(row, "", 15, new Vector2(60f, -23f), new Vector2(236f, 22f), TextAnchor.MiddleLeft, PortText);

            RectTransform bar = CreatePanel("Quest Progress Bar", row, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(60f, -54f),
                sizeDelta = new Vector2(154f, 10f)
            }, PortTrack);
            ApplyRoundedSprite(bar.GetComponent<Image>(), GetRoundedSmallSprite());

            metaQuestProgressFills[i] = CreatePanel("Quest Progress Fill", bar, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = Vector2.zero
            }, PortNavy);
            ApplyRoundedSprite(metaQuestProgressFills[i].GetComponent<Image>(), GetRoundedSmallSprite());

            metaQuestCounterTexts[i] = CreateText(row, "", 16, new Vector2(224f, -46f), new Vector2(76f, 24f), TextAnchor.MiddleRight, PortTextSoft);
            metaQuestRows[i] = row;
        }

        metaAllTasksButtonText = CreateGlassButton(metaQuestPanel, "Meta All Tasks", new Vector2(22f, -310f), new Vector2(300f, 44f), OpenMetaAllTasksWindow, out metaAllTasksButton, "Все задачи");
        metaAllTasksButtonText.fontSize = 18;
        RefreshMetaQuestPanel(true);
    }

    private void BuildMetaProjectPanel(RectTransform parent)
    {
        metaProjectToolButtonStrip = CreateRect("Meta Project Tool Buttons", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = new Vector2(20f, 238f),
            sizeDelta = new Vector2(252f, 74f)
        }).GetComponent<RectTransform>();

        metaProjectHomeButtonText = CreateRoundGlassButton(metaProjectToolButtonStrip, "Meta Project Home", new Vector2(0f, 0f), 74f, HandleMetaProjectToolButton, out metaProjectHomeButton, "\u2708");
        metaProjectGridButtonText = CreateRoundGlassButton(metaProjectToolButtonStrip, "Meta Project Grid", new Vector2(90f, 0f), 74f, HandleMetaBuildingCatalogButton, out metaProjectGridButton, "\u25A6");
        metaProjectCameraButtonText = CreateRoundGlassButton(metaProjectToolButtonStrip, "Meta Project Camera", new Vector2(180f, 0f), 74f, HandleMetaCameraButton, out metaProjectCameraButton, "\u2699");
        SetText(metaProjectHomeButtonText, "\u2708");
        SetText(metaProjectGridButtonText, "\u2693");
        SetText(metaProjectCameraButtonText, "\u25A3");

        Text blueprintText = CreateRoundGlassButton(parent, "Meta Blueprint Development", new Vector2(32f, 28f), 156f, OpenDevelopmentWindow, out _, "\u2692");
        metaBlueprintButtonRoot = blueprintText != null && blueprintText.transform.parent != null
            ? blueprintText.transform.parent.GetComponent<RectTransform>()
            : null;
        blueprintText.fontSize = 68;
        blueprintText.color = new Color(0.64f, 0.55f, 0.40f, 0.95f);
        if (metaBlueprintButtonRoot != null)
        {
            Image projectsIconImage = CreateMainHudIconImage(
                metaBlueprintButtonRoot,
                "Meta Projects Icon",
                "MetaProjects",
                new RectTransformSpec
                {
                    anchorMin = new Vector2(0.5f, 0.5f),
                    anchorMax = new Vector2(0.5f, 0.5f),
                    pivot = new Vector2(0.5f, 0.5f),
                    anchoredPosition = new Vector2(0f, 10f),
                    sizeDelta = new Vector2(126f, 126f)
                },
                Color.white);
            SetFallbackTextVisibleWhenIconMissing(blueprintText, projectsIconImage);

            RectTransform labelPill = CreatePanel("Meta Blueprint Label", metaBlueprintButtonRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0.5f, 0f),
                anchorMax = new Vector2(0.5f, 0f),
                pivot = new Vector2(0.5f, 0f),
                anchoredPosition = new Vector2(0f, -2f),
                sizeDelta = new Vector2(136f, 28f)
            }, PortGlassStrong);
            Image blueprintLabelImage = labelPill.GetComponent<Image>();
            ApplyRoundedSprite(blueprintLabelImage, GetRoundedSmallSprite());
            SetGraphicRaycastTarget(blueprintLabelImage, false);
            Text labelText = CreateText(labelPill, "Проекты", 21, Vector2.zero, new Vector2(136f, 28f), TextAnchor.MiddleCenter, PortText);
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;
        }

        metaProjectPanel = CreateGlassPanel("Meta Project Queue Panel", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = new Vector2(86f, 26f),
            sizeDelta = new Vector2(1458f, 148f)
        }, PortGlass);

        metaProjectCards = new RectTransform[MetaProjectMaxCount];
        metaProjectProgressFills = new RectTransform[MetaProjectMaxCount];
        metaProjectTitleTexts = new Text[MetaProjectMaxCount];
        metaProjectAmountTexts = new Text[MetaProjectMaxCount];
        metaProjectIconTexts = new Text[MetaProjectMaxCount];
        metaProjectTimerTexts = new Text[MetaProjectMaxCount];

        for (int i = 0; i < MetaProjectMaxCount; i++)
        {
            RectTransform card = CreateGlassPanel("Meta Project Card " + i, metaProjectPanel, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(122f + i * (MetaProjectCardWidth + 16f), -10f),
                sizeDelta = new Vector2(MetaProjectCardWidth, MetaProjectCardHeight)
            }, PortGlassStrong, false);

            RectTransform iconDisc = CreatePanel("Meta Project Icon Disc " + i, card, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(14f, -18f),
                sizeDelta = new Vector2(58f, 58f)
            }, PortGlassMuted);
            Image iconDiscImage = iconDisc.GetComponent<Image>();
            if (iconDiscImage != null)
            {
                iconDiscImage.sprite = GetCircleSprite();
                iconDiscImage.type = Image.Type.Simple;
            }

            RectTransform track = CreatePanel("Meta Project Progress Track " + i, card, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(76f, -54f),
                sizeDelta = new Vector2(128f, 11f)
            }, PortTrack);
            ApplyRoundedSprite(track.GetComponent<Image>(), GetRoundedSmallSprite());

            metaProjectProgressFills[i] = CreatePanel("Meta Project Progress Fill", track, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = Vector2.zero
            }, PortNavy);
            ApplyRoundedSprite(metaProjectProgressFills[i].GetComponent<Image>(), GetRoundedSmallSprite());

            metaProjectTitleTexts[i] = CreateText(card, "", 18, new Vector2(76f, -20f), new Vector2(158f, 26f), TextAnchor.MiddleLeft, PortText);
            metaProjectTitleTexts[i].fontStyle = FontStyle.Bold;
            metaProjectAmountTexts[i] = CreateText(card, "", 17, new Vector2(204f, -48f), new Vector2(38f, 24f), TextAnchor.MiddleRight, PortTextSoft);
            metaProjectAmountTexts[i].fontStyle = FontStyle.Bold;
            metaProjectIconTexts[i] = CreateText(iconDisc, "", 30, Vector2.zero, new Vector2(58f, 58f), TextAnchor.MiddleCenter, PortText);
            metaProjectIconTexts[i].fontStyle = FontStyle.Bold;
            CreatePanel("Meta Project Card Divider " + i, card, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = new Vector2(0f, -86f),
                sizeDelta = new Vector2(-24f, 1f)
            }, new Color(0.42f, 0.48f, 0.56f, 0.22f));
            metaProjectTimerTexts[i] = CreateText(card, "", 17, new Vector2(34f, -94f), new Vector2(184f, 24f), TextAnchor.MiddleCenter, PortTextSoft);

            metaProjectCards[i] = card;
        }

        RefreshMetaProjectPanel(true);
        if (metaBlueprintButtonRoot != null)
        {
            metaBlueprintButtonRoot.SetAsLastSibling();
        }

        if (metaProjectToolButtonStrip != null)
        {
            metaProjectToolButtonStrip.SetAsLastSibling();
        }
    }

    private void RefreshPlayerProfile(bool visible)
    {
        if (playerProfilePanel == null)
        {
            return;
        }

        playerProfilePanel.gameObject.SetActive(visible);
        SetText(playerProfileTitleText, MetaProfileTitlePlaceholder);
        SetText(playerProfileMasteryCurrentText, MetaProfileMasteryCurrentPlaceholder.ToString(CultureInfo.InvariantCulture));
        SetText(playerProfileMasteryNextText, MetaProfileMasteryNextPlaceholder.ToString(CultureInfo.InvariantCulture));

        if (playerProfileMasteryFill == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(MetaProfileMasteryProgressPlaceholder);
        playerProfileMasteryFill.anchorMin = new Vector2(0f, 0f);
        playerProfileMasteryFill.anchorMax = new Vector2(progress, 1f);
        playerProfileMasteryFill.offsetMin = Vector2.zero;
        playerProfileMasteryFill.offsetMax = Vector2.zero;
    }

    private void RefreshMetaResourceCounters(bool visible)
    {
        if (metaResourceCounterStrip == null)
        {
            return;
        }

        metaResourceCounterStrip.gameObject.SetActive(visible);
        if (metaResourceCounterAmountTexts == null)
        {
            return;
        }

        for (int i = 0; i < MetaResourceCounterCount && i < metaResourceCounterAmountTexts.Length; i++)
        {
            SetText(metaResourceCounterAmountTexts[i], FormatMetaResourceAmount(MetaResourceCounters[i].amount));
            if (metaResourceCounterPlusButtons != null && i < metaResourceCounterPlusButtons.Length)
            {
                SetInteractable(metaResourceCounterPlusButtons[i], true);
            }
        }
    }

    private bool HandleMetaResourcePlus()
    {
        return true;
    }

    private void RefreshMetaTopRightButtons(bool visible)
    {
        if (metaTopRightButtonStrip == null)
        {
            return;
        }

        metaTopRightButtonStrip.gameObject.SetActive(visible);
        if (metaTopRightButtons == null)
        {
            return;
        }

        for (int i = 0; i < metaTopRightButtons.Length; i++)
        {
            SetInteractable(metaTopRightButtons[i], true);
        }
    }

    private void RefreshMetaLeftSideButtons(bool visible)
    {
        if (metaLeftSideButtonStrip == null)
        {
            return;
        }

        metaLeftSideButtonStrip.gameObject.SetActive(visible);
        if (metaLeftSideButtons == null)
        {
            return;
        }

        for (int i = 0; i < metaLeftSideButtons.Length; i++)
        {
            SetInteractable(metaLeftSideButtons[i], true);
        }
    }

    private void RefreshMetaDockButton(bool visible)
    {
        if (metaDockButton == null)
        {
            return;
        }

        metaDockButton.gameObject.SetActive(visible);
        SetInteractable(metaDockButton, true);
    }

    private void HandleMetaDockButton()
    {
        OpenMetaDockScreen();
    }

    private void RefreshMetaDockScreen(bool visible)
    {
        if (metaDockScreenLayer == null)
        {
            return;
        }

        metaDockScreenLayer.gameObject.SetActive(visible);
        if (visible)
        {
            metaDockScreenLayer.SetAsLastSibling();
        }
        else
        {
            HideMetaDockSortieResultWindow();
        }

        SetInteractable(metaDockScreenPortButton, visible);
        RefreshMetaDockGameplayPanel(visible);
    }

    private void ShowMetaDockSortieResultWindow(string reportText, ShipTreeEntryConfig ship, DockedDevelopmentShipState slot)
    {
        if (metaDockResultModalRoot == null)
        {
            return;
        }

        string title = ship != null
            ? "РЕЗУЛЬТАТ: " + ShortenButtonLabel(ship.DisplayNameRu, 34)
            : "РЕЗУЛЬТАТ ВЫЛЕТА";
        string subtitle = ship != null
            ? FormatRomanTier(Mathf.Clamp(ship.treeTier, 1, DevelopmentTreeTierCount)) + " ранг"
            : "";
        if (slot != null)
        {
            subtitle = string.IsNullOrWhiteSpace(subtitle)
                ? slot.sortiesRemaining + "/" + MetaGameState.DevelopmentDockShipMaxSorties + " вылетов"
                : subtitle + " | " + slot.sortiesRemaining + "/" + MetaGameState.DevelopmentDockShipMaxSorties + " вылетов";
        }

        SetText(metaDockResultTitleText, title);
        SetText(metaDockResultSubtitleText, subtitle);
        SetText(metaDockResultBodyText, string.IsNullOrWhiteSpace(reportText) ? "Вылет завершён." : reportText);
        metaDockResultModalRoot.gameObject.SetActive(true);
        metaDockResultModalRoot.SetAsLastSibling();
    }

    private bool HideMetaDockSortieResultWindow()
    {
        if (metaDockResultModalRoot != null)
        {
            metaDockResultModalRoot.gameObject.SetActive(false);
        }

        return true;
    }

    private void RefreshMetaDockGameplayPanel(bool visible)
    {
        if (metaDockShipSlotRoot == null || metaDockSelectedShipRoot == null || metaDockMissionRoot == null || metaDockRewardRoot == null)
        {
            return;
        }

        metaDockShipSlotRoot.gameObject.SetActive(visible);
        metaDockSelectedShipRoot.gameObject.SetActive(visible);
        metaDockMissionRoot.gameObject.SetActive(visible);
        metaDockRewardRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        MetaGameState currentMeta = ResolveMeta();
        IReadOnlyList<DockedDevelopmentShipState> slots = currentMeta != null ? currentMeta.GetDevelopmentDockShipSlots() : null;
        DockedDevelopmentShipState selectedSlot = currentMeta != null ? currentMeta.GetSelectedDevelopmentDockShipSlot() : null;
        SessionConfigDatabase config = currentMeta != null ? currentMeta.SessionConfig : null;
        bool canLaunchSelectedDockShip = currentMeta != null
            && currentMeta.IsDockedAtCapital()
            && selectedSlot != null
            && selectedSlot.HasShip
            && selectedSlot.sortiesRemaining > 0;
        SetInteractable(metaDockManualSortieButton, canLaunchSelectedDockShip);
        SetInteractable(metaDockCoreCombatButton, canLaunchSelectedDockShip);

        SetText(metaDockHeaderText, "Быстрые миссии подстраиваются под корабль: высокий рейтинг привозит более дорогой конкретный ресурс.");
        if (metaDockSlotTexts != null)
        {
            for (int i = 0; i < metaDockSlotTexts.Length; i++)
            {
                DockedDevelopmentShipState slot = slots != null && i < slots.Count ? slots[i] : null;
                bool selected = selectedSlot != null && slot != null && selectedSlot.slotIndex == slot.slotIndex;
                ShipTreeEntryConfig ship = slot != null && slot.HasShip && config != null ? config.GetShipTreeEntry(slot.shipId) : null;
                string slotText = "Слот " + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n";
                slotText += ship != null
                    ? ShortenButtonLabel(ship.DisplayNameRu, 20) + "\n" + slot.sortiesRemaining + "/" + MetaGameState.DevelopmentDockShipMaxSorties + " вылетов"
                    : "Пустой док";
                SetText(metaDockSlotTexts[i], slotText);

                if (metaDockSlotButtons != null && i < metaDockSlotButtons.Length && metaDockSlotButtons[i] != null)
                {
                    Image image = metaDockSlotButtons[i].GetComponent<Image>();
                    if (image != null)
                    {
                        image.color = selected
                            ? new Color(0.26f, 0.20f, 0.09f, 0.98f)
                            : new Color(0.08f, 0.075f, 0.055f, 0.94f);
                    }
                }
            }
        }

        if (selectedSlot == null || !selectedSlot.HasShip || config == null)
        {
            SetText(metaDockSelectedShipText, "Слот пуст");
            SetText(metaDockSelectedStatsText, "Купи корабль в окне развития. Он встанет сюда, после чего можно нажимать быстрый вылет и смотреть конкретный вывоз ресурсов.");
            RefreshMetaDockMissionPanel(null, null, null);
            SetText(metaDockRewardText, string.IsNullOrWhiteSpace(currentMeta != null ? currentMeta.LastQuickSortieReport : "") ? "Пока вылетов не было." : currentMeta.LastQuickSortieReport);
            SetInteractable(metaDockQuickBattleButton, false);
            SetInteractable(metaDockRunMissionButton, false);
            SetInteractable(metaDockManualSortieButton, false);
            SetInteractable(metaDockCoreCombatButton, false);
            SetInteractable(metaDockSellButton, false);
            return;
        }

        ShipTreeEntryConfig selectedShip = config.GetShipTreeEntry(selectedSlot.shipId);
        if (selectedShip == null)
        {
            SetText(metaDockSelectedShipText, selectedSlot.shipId);
            SetText(metaDockSelectedStatsText, "Корабль не найден в Ship_tree.csv.");
            RefreshMetaDockMissionPanel(null, null, null);
            SetInteractable(metaDockQuickBattleButton, false);
            SetInteractable(metaDockRunMissionButton, false);
            SetInteractable(metaDockManualSortieButton, false);
            SetInteractable(metaDockCoreCombatButton, false);
            SetInteractable(metaDockSellButton, true);
            return;
        }

        SetText(metaDockSelectedShipText, FormatRomanTier(Mathf.Clamp(selectedShip.treeTier, 1, DevelopmentTreeTierCount)) + " " + selectedShip.DisplayNameRu);
        SetText(metaDockSelectedStatsText, BuildMetaDockShipStatsText(selectedShip, selectedSlot));
        RefreshMetaDockMissionPanel(currentMeta, selectedSlot, selectedShip);
        string reward = !string.IsNullOrWhiteSpace(selectedSlot.lastRewardSummary)
            ? selectedSlot.lastRewardSummary
            : currentMeta.LastQuickSortieReport;
        SetText(metaDockRewardText, string.IsNullOrWhiteSpace(reward) ? "Пока этот корабль не летал." : reward);
        SetInteractable(metaDockQuickBattleButton, canLaunchSelectedDockShip);
        SetInteractable(metaDockRunMissionButton, selectedSlot.sortiesRemaining > 0 && !string.IsNullOrWhiteSpace(selectedMetaDockMissionOfferId));
        SetInteractable(metaDockSellButton, true);
    }

    private void RefreshMetaDockMissionPanel(MetaGameState currentMeta, DockedDevelopmentShipState selectedSlot, ShipTreeEntryConfig selectedShip)
    {
        if (metaDockMissionTexts == null || metaDockMissionButtons == null)
        {
            return;
        }

        List<SortieMissionOffer> offers = currentMeta != null && selectedSlot != null && selectedSlot.HasShip
            ? currentMeta.GetDevelopmentDockOrdinaryMissionOffers(selectedSlot.slotIndex)
            : null;
        if (offers == null || offers.Count == 0)
        {
            selectedMetaDockMissionOfferId = "";
            SetText(metaDockMissionDetailText, "Нет корабля");
            for (int i = 0; i < metaDockMissionTexts.Length; i++)
            {
                SetText(metaDockMissionTexts[i], "");
                SetInteractable(metaDockMissionButtons[i], false);
            }

            SetInteractable(metaDockRunMissionButton, false);
            return;
        }

        bool selectedStillExists = false;
        for (int i = 0; i < offers.Count; i++)
        {
            if (string.Equals(offers[i].offerId, selectedMetaDockMissionOfferId, StringComparison.OrdinalIgnoreCase))
            {
                selectedStillExists = true;
                break;
            }
        }

        if (!selectedStillExists)
        {
            selectedMetaDockMissionOfferId = offers[0].offerId;
        }

        SortieMissionOffer selectedOffer = null;
        for (int i = 0; i < metaDockMissionTexts.Length; i++)
        {
            SortieMissionOffer offer = i < offers.Count ? offers[i] : null;
            bool hasOffer = offer != null;
            bool selected = hasOffer && string.Equals(offer.offerId, selectedMetaDockMissionOfferId, StringComparison.OrdinalIgnoreCase);
            SetInteractable(metaDockMissionButtons[i], hasOffer);
            SetText(metaDockMissionTexts[i], hasOffer
                ? ShortenButtonLabel(offer.titleRu, 28) + "\n" + offer.BuildRequirementSummary(selectedShip)
                : "");
            if (metaDockMissionButtons[i] != null)
            {
                Image image = metaDockMissionButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = selected
                        ? new Color(0.23f, 0.16f, 0.06f, 0.98f)
                        : new Color(0.065f, 0.074f, 0.080f, 0.94f);
                }
            }

            if (selected)
            {
                selectedOffer = offer;
            }
        }

        if (selectedOffer != null)
        {
            SetText(metaDockMissionDetailText,
                "Шанс выхода " + Mathf.RoundToInt(selectedOffer.estimatedSuccessFactor * 100f).ToString(CultureInfo.InvariantCulture) + "%\n"
                + selectedOffer.primaryActivityRu);
        }
        else
        {
            SetText(metaDockMissionDetailText, "Выбери миссию");
        }

        SetInteractable(metaDockRunMissionButton, selectedSlot != null && selectedSlot.sortiesRemaining > 0 && selectedOffer != null);
    }

    private bool HandleMetaDockMissionSelected(int offerIndex)
    {
        MetaGameState currentMeta = ResolveMeta();
        DockedDevelopmentShipState slot = currentMeta != null ? currentMeta.GetSelectedDevelopmentDockShipSlot() : null;
        List<SortieMissionOffer> offers = currentMeta != null && slot != null && slot.HasShip
            ? currentMeta.GetDevelopmentDockOrdinaryMissionOffers(slot.slotIndex)
            : null;
        if (offers == null || offerIndex < 0 || offerIndex >= offers.Count || offers[offerIndex] == null)
        {
            return false;
        }

        selectedMetaDockMissionOfferId = offers[offerIndex].offerId;
        RefreshState(true);
        return true;
    }

    private bool HandleMetaDockRunSelectedMission()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return false;
        }

        DockedDevelopmentShipState slot = currentMeta.GetSelectedDevelopmentDockShipSlot();
        bool success = currentMeta.TryRunDevelopmentDockOrdinaryMission(
            slot != null ? slot.slotIndex : 0,
            selectedMetaDockMissionOfferId,
            out statusMessage);
        RefreshState(true);
        if (success)
        {
            DockedDevelopmentShipState updatedSlot = currentMeta.GetSelectedDevelopmentDockShipSlot();
            SessionConfigDatabase config = currentMeta.SessionConfig;
            ShipTreeEntryConfig ship = updatedSlot != null && updatedSlot.HasShip && config != null
                ? config.GetShipTreeEntry(updatedSlot.shipId)
                : null;
            ShowMetaDockSortieResultWindow(statusMessage, ship, updatedSlot);
        }

        return success;
    }

    private bool HandleMetaDockSlotSelected(int slotIndex)
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return false;
        }

        bool selected = currentMeta.SelectDevelopmentDockSlot(slotIndex);
        DockedDevelopmentShipState selectedSlot = currentMeta.GetSelectedDevelopmentDockShipSlot();
        if (selected && (selectedSlot == null || !selectedSlot.HasShip))
        {
            return OpenDevelopmentWindowFromDock();
        }

        RefreshState(true);
        return selected;
    }

    private bool HandleMetaDockQuickBattle()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return false;
        }

        DockedDevelopmentShipState slot = currentMeta.GetSelectedDevelopmentDockShipSlot();
        bool success = currentMeta.TryRunQuickDevelopmentSortie(slot != null ? slot.slotIndex : 0, out statusMessage);
        RefreshState(true);
        if (success)
        {
            DockedDevelopmentShipState updatedSlot = currentMeta.GetSelectedDevelopmentDockShipSlot();
            SessionConfigDatabase config = currentMeta.SessionConfig;
            ShipTreeEntryConfig ship = updatedSlot != null && updatedSlot.HasShip && config != null
                ? config.GetShipTreeEntry(updatedSlot.shipId)
                : null;
            ShowMetaDockSortieResultWindow(statusMessage, ship, updatedSlot);
        }

        return success;
    }

    private bool HandleMetaDockManualSortie()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return false;
        }

        bool success = currentMeta.BeginQuickAdaptiveManualSessionSortie();
        statusMessage = currentMeta.LastAccountMessage;
        if (success)
        {
            metaScreenMode = MetaScreenMode.Port;
            HideMetaDockSortieResultWindow();
        }

        RefreshState(true);
        return success;
    }

    private bool HandleMetaDockCoreCombatSortie()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return false;
        }

        bool success = currentMeta.BeginCoreTacticalIntroCombatSortie();
        statusMessage = currentMeta.LastAccountMessage;
        if (success)
        {
            metaScreenMode = MetaScreenMode.Port;
            HideMetaDockSortieResultWindow();
        }

        RefreshState(true);
        return success;
    }

    private bool HandleMetaDockSellShip()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            return false;
        }

        DockedDevelopmentShipState slot = currentMeta.GetSelectedDevelopmentDockShipSlot();
        bool success = currentMeta.TrySellDevelopmentDockShip(slot != null ? slot.slotIndex : 0, out statusMessage);
        if (success)
        {
            HideMetaDockSortieResultWindow();
        }

        RefreshState(true);
        return success;
    }

    private static string BuildMetaDockShipStatsText(ShipTreeEntryConfig ship, DockedDevelopmentShipState slot)
    {
        if (ship == null)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(GetDevelopmentRoleText(ship));
        if (slot != null)
        {
            builder.Append("Вылеты: ");
            builder.Append(slot.sortiesRemaining.ToString(CultureInfo.InvariantCulture));
            builder.Append("/");
            builder.AppendLine(MetaGameState.DevelopmentDockShipMaxSorties.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("Груз: ");
        builder.AppendLine(FormatCargoCapacityTons(ship.cargoCapacityTons));
        builder.AppendLine();
        builder.AppendLine("Основные");
        builder.AppendLine("Живучесть " + GetDevelopmentDefenseRating(ship).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("Мобильность " + GetDevelopmentMobilityRating(ship).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("Маскировка " + GetDevelopmentStealthRating(ship).ToString(CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("Активные");
        AppendMetaDockRating(builder, "Вооружение", GetDevelopmentWarfareRating(ship));
        AppendMetaDockRating(builder, "Руда", ship.miningRating);
        AppendMetaDockRating(builder, "Облака", ship.harvestingRating);
        AppendMetaDockRating(builder, "Охота", ship.huntingRating);
        AppendMetaDockRating(builder, "Взлом", ship.hackingRating);
        AppendMetaDockRating(builder, "Сальваж", ship.salvageRating);
        AppendMetaDockRating(builder, "Сканирование", ship.surveyRating);
        AppendMetaDockRating(builder, "Ремонт союзников", ship.repairRating);
        return builder.ToString().TrimEnd();
    }

    private static void AppendMetaDockRating(StringBuilder builder, string label, int value)
    {
        if (builder == null || value <= 0)
        {
            return;
        }

        builder.Append(label);
        builder.Append(" ");
        builder.AppendLine(Mathf.Clamp(value, 1, 100).ToString(CultureInfo.InvariantCulture));
    }

    private bool OpenMetaDockScreen()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null || currentMeta.CurrentMode != GameSessionMode.Docked)
        {
            return false;
        }

        CloseAllHudWindows();
        if (knowledgeScreen != null)
        {
            knowledgeScreen.SetVisible(false);
        }

        metaScreenMode = MetaScreenMode.Dock;
        statusMessage = "Dock screen opened.";
        RefreshState(true);
        return metaDockScreenLayer != null && metaDockScreenLayer.gameObject.activeInHierarchy;
    }

    private bool ReturnFromMetaDockScreenToPort()
    {
        metaScreenMode = MetaScreenMode.Port;
        statusMessage = "Port screen opened.";
        RefreshState(true);
        return metaDockScreenLayer == null || !metaDockScreenLayer.gameObject.activeInHierarchy;
    }

    private void CloseAllHudWindows()
    {
        foreach (KeyValuePair<string, HudWindow> pair in hudWindows)
        {
            if (pair.Value != null)
            {
                pair.Value.SetOpen(false);
            }
        }
    }

    private void RefreshMetaQuestPanel(bool visible)
    {
        if (metaQuestPanel == null)
        {
            return;
        }

        metaQuestPanel.gameObject.SetActive(visible);
        if (metaQuestRows == null)
        {
            return;
        }

        int count = GetMetaQuestVisibleCount();
        for (int i = 0; i < MetaQuestMaxCount; i++)
        {
            bool rowVisible = visible && i < count;
            if (metaQuestRows[i] != null)
            {
                metaQuestRows[i].gameObject.SetActive(rowVisible);
            }

            if (!rowVisible)
            {
                continue;
            }

            RefreshMetaQuestRow(i, MetaQuestSpecs[i]);
        }

        SetInteractable(metaAllTasksButton, true);
    }

    private void RefreshMetaQuestRow(int index, MetaQuestSpec quest)
    {
        SetText(metaQuestIconTexts[index], quest.iconText);
        SetText(metaQuestLineOneTexts[index], quest.lineOne);
        SetText(metaQuestLineTwoTexts[index], quest.lineTwo);
        SetText(metaQuestCounterTexts[index], quest.current.ToString(CultureInfo.InvariantCulture) + "/" + quest.target.ToString(CultureInfo.InvariantCulture));

        if (metaQuestProgressFills[index] == null)
        {
            return;
        }

        float progress = quest.target > 0 ? Mathf.Clamp01(quest.current / (float)quest.target) : 0f;
        metaQuestProgressFills[index].anchorMin = new Vector2(0f, 0f);
        metaQuestProgressFills[index].anchorMax = new Vector2(progress, 1f);
        metaQuestProgressFills[index].offsetMin = Vector2.zero;
        metaQuestProgressFills[index].offsetMax = Vector2.zero;
    }

    private int GetMetaQuestVisibleCount()
    {
        return Mathf.Clamp(metaQuestPreviewCount, 1, Mathf.Min(MetaQuestMaxCount, MetaQuestSpecs.Length));
    }

    private bool OpenMetaAllTasksWindow()
    {
        if (metaAllTasksWindow == null)
        {
            return false;
        }

        metaAllTasksWindow.SetOpen(true);
        RefreshState(true);
        return true;
    }

    private void RefreshMetaProjectPanel(bool visible)
    {
        if (metaProjectPanel == null)
        {
            return;
        }

        metaProjectPanel.gameObject.SetActive(visible);
        if (metaProjectToolButtonStrip != null)
        {
            metaProjectToolButtonStrip.gameObject.SetActive(visible);
        }

        if (metaProjectCards == null)
        {
            return;
        }

        int count = GetMetaProjectVisibleCount();
        for (int i = 0; i < MetaProjectMaxCount; i++)
        {
            bool cardVisible = visible && i < count;
            if (metaProjectCards[i] != null)
            {
                metaProjectCards[i].gameObject.SetActive(cardVisible);
            }

            if (!cardVisible)
            {
                continue;
            }

            RefreshMetaProjectCard(i, MetaProjectSpecs[i]);
        }

        SetInteractable(metaProjectHomeButton, true);
        SetInteractable(metaProjectGridButton, true);
        SetInteractable(metaProjectCameraButton, true);
    }

    private void RefreshMetaProjectCard(int index, MetaProjectSpec project)
    {
        SetText(metaProjectTitleTexts[index], project.title);
        SetText(metaProjectAmountTexts[index], project.amount);
        SetText(metaProjectIconTexts[index], project.iconText);
        SetText(metaProjectTimerTexts[index], project.timer);

        if (metaProjectProgressFills[index] == null)
        {
            return;
        }

        float progress = Mathf.Clamp01(project.progress01);
        metaProjectProgressFills[index].anchorMin = new Vector2(0f, 0f);
        metaProjectProgressFills[index].anchorMax = new Vector2(progress, 1f);
        metaProjectProgressFills[index].offsetMin = Vector2.zero;
        metaProjectProgressFills[index].offsetMax = Vector2.zero;
    }

    private int GetMetaProjectVisibleCount()
    {
        return Mathf.Clamp(metaProjectQueuePreviewCount, 1, Mathf.Min(MetaProjectMaxCount, MetaProjectSpecs.Length));
    }

    private bool HandleMetaProjectToolButton()
    {
        return true;
    }

    private bool HandleMetaBuildingCatalogButton()
    {
        WildWindBaseIslandView island = FindFirstObjectByType<WildWindBaseIslandView>();
        if (island == null)
        {
            island = WildWindBaseIslandView.EnsureForCurrentSessionScene();
        }

        if (island == null)
        {
            return false;
        }

        island.ToggleBuildingCatalogForRuntime();
        return true;
    }

    private bool HandleMetaCameraButton()
    {
        WildWindBaseIslandView island = FindFirstObjectByType<WildWindBaseIslandView>();
        if (island == null)
        {
            island = WildWindBaseIslandView.EnsureForCurrentSessionScene();
        }

        if (island == null)
        {
            return false;
        }

        island.FlyCityCameraHomeForRuntime();
        return true;
    }

    private void CreateWindowDockButton(string windowId, string label, float y)
    {
        Text text = CreateButton(windowDockPanel, "Window " + label, new Vector2(10f, y), new Vector2(96f, 34f), () => ToggleHudWindow(windowId), out _);
        SetText(text, label);
    }

    private HudWindow CreateHudWindow(string id, string title, Vector2 position, Vector2 size, bool openByDefault)
    {
        HudWindow window = HudWindow.Create(this, root, id, title, position, size, openByDefault);
        hudWindows[id] = window;
        return window;
    }

    private HudWindow CreateMetaTopRightWindow(int index)
    {
        MetaTopRightButtonSpec spec = MetaTopRightButtons[index];
        return CreateHudWindow(spec.windowId, spec.title, new Vector2(1180f, -126f), new Vector2(520f, 360f), false);
    }

    private void BuildSettingsWindowContent(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        settingsResetProgressText = CreateButton(
            parent,
            "Settings Reset Progress",
            new Vector2(18f, -18f),
            new Vector2(240f, 44f),
            HandleSettingsResetProgressButton,
            out settingsResetProgressButton);
        SetText(settingsResetProgressText, "Сбросить прогресс");
    }

    private bool HandleSettingsResetProgressButton()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            statusMessage = "Meta progress is unavailable.";
            RefreshState(true);
            return false;
        }

        bool reset = currentMeta.ResetPersistentProgressFromSettings(out statusMessage);
        CloseAllHudWindows();
        RefreshState(true);
        return reset;
    }

    private HudWindow CreateMetaLeftSideWindow(int index)
    {
        MetaSideButtonSpec spec = MetaLeftSideButtons[index];
        return CreateHudWindow(spec.windowId, spec.title, new Vector2(154f, -150f), new Vector2(520f, 360f), false);
    }

    private void BuildDevelopmentWindowContent(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        developmentWindowText = CreateText(
            parent,
            "",
            16,
            new Vector2(16f, -8f),
            new Vector2(720f, 28f),
            TextAnchor.MiddleLeft,
            new Color(0.86f, 0.90f, 0.82f, 1f));
        developmentWindowText.fontStyle = FontStyle.Bold;

        developmentSupplierTabRoot = CreatePanel("Development Faction List", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(16f, -44f),
            sizeDelta = new Vector2(DevelopmentFactionListWidth, 858f)
        }, new Color(0.020f, 0.024f, 0.029f, 0.90f));

        developmentTreeScrollRoot = CreatePanel("Development Ship Tree Scroll", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(284f, -44f),
            sizeDelta = new Vector2(DevelopmentTreeViewportWidth, DevelopmentTreeViewportHeight)
        }, new Color(0.018f, 0.023f, 0.029f, 0.93f));

        developmentTreeScrollRect = developmentTreeScrollRoot.gameObject.AddComponent<ScrollRect>();
        developmentTreeScrollRect.horizontal = true;
        developmentTreeScrollRect.vertical = true;
        developmentTreeScrollRect.inertia = true;
        developmentTreeScrollRect.movementType = ScrollRect.MovementType.Clamped;
        developmentTreeScrollRect.scrollSensitivity = 42f;

        developmentTreeViewport = CreatePanel("Development Ship Tree Viewport", developmentTreeScrollRoot, new RectTransformSpec
        {
            anchorMin = Vector2.zero,
            anchorMax = Vector2.one,
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = Vector2.zero,
            sizeDelta = Vector2.zero
        }, new Color(0f, 0f, 0f, 0f));
        developmentTreeViewport.gameObject.AddComponent<RectMask2D>();
        InstallDevelopmentTreePanHandler(developmentTreeViewport);

        developmentTreeRoot = CreatePanel("Development Ship Tree Content", developmentTreeViewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(DevelopmentTreePanelWidth, DevelopmentTreeViewportHeight)
        }, new Color(0.018f, 0.023f, 0.029f, 0.10f));

        developmentTreeScrollRect.viewport = developmentTreeViewport;
        developmentTreeScrollRect.content = developmentTreeRoot;
        InstallDevelopmentTreePanHandler(developmentTreeRoot);

        developmentDetailsRoot = CreatePanel("Development Ship Details", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(1522f, -44f),
            sizeDelta = new Vector2(DevelopmentDetailsWidth, 858f)
        }, new Color(0.020f, 0.024f, 0.029f, 0.94f));
    }

    private void BuildCargoGrid(RectTransform parent)
    {
        cargoSlotTexts = new Text[16];
        float slot = 74f;
        float gap = 10f;
        for (int i = 0; i < cargoSlotTexts.Length; i++)
        {
            int col = i % 4;
            int row = i / 4;
            RectTransform cell = CreatePanel("Cargo Slot " + i, parent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(16f + col * (slot + gap), -48f - row * (slot + gap)),
                sizeDelta = new Vector2(slot, slot)
            }, new Color(0.040f, 0.045f, 0.050f, 0.88f));

            cargoSlotTexts[i] = CreateText(cell, "", 12, new Vector2(6f, -6f), new Vector2(slot - 12f, slot - 12f), TextAnchor.UpperLeft, new Color(0.86f, 0.88f, 0.78f, 1f));
        }
    }

    private void BuildResourceCatalogScroll(RectTransform parent)
    {
        RectTransform scrollRoot = CreatePanel("Resource Catalog Scroll", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -42f),
            sizeDelta = new Vector2(-24f, 520f)
        }, new Color(0.020f, 0.024f, 0.028f, 0.86f));

        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        RectTransform viewport = CreatePanel("Viewport", scrollRoot, StretchFull(), new Color(0f, 0f, 0f, 0f));
        viewport.gameObject.AddComponent<RectMask2D>();

        resourceCatalogContent = CreateRect("Content", viewport, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(0f, 0f)
        }).GetComponent<RectTransform>();

        scroll.viewport = viewport;
        scroll.content = resourceCatalogContent;
    }

    private void BuildCompass()
    {
        compassPanel = CreatePanel("Heading Compass", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 1f),
            anchorMax = new Vector2(0.5f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -6f),
            sizeDelta = new Vector2(620f, 42f)
        }, new Color(0.015f, 0.014f, 0.012f, 0.36f));
        compassPanel.gameObject.AddComponent<RectMask2D>();

        compassTickTexts = new Text[CompassTickCount];
        compassTickLines = new RectTransform[CompassTickCount];
        for (int i = 0; i < CompassTickCount; i++)
        {
            compassTickTexts[i] = CreateCompassText(compassPanel, "");
            compassTickLines[i] = CreateCompassLine(compassPanel, i == CompassTickCenterIndex ? 28f : 13f);
        }

        RectTransform centerLine = CreateCompassLine(compassPanel, 34f);
        centerLine.anchoredPosition = new Vector2(0f, -4f);
        Image image = centerLine.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.95f, 0.78f, 0.42f, 0.95f);
        }

        compassTargetLine = CreateCompassLine(compassPanel, 30f);
        compassTargetLine.anchoredPosition = new Vector2(0f, -12f);
        Image targetImage = compassTargetLine.GetComponent<Image>();
        if (targetImage != null)
        {
            targetImage.color = new Color(0.30f, 0.86f, 0.96f, 0.95f);
        }

        compassTargetText = CreateCompassText(compassPanel, "");
        compassTargetText.fontSize = 12;
        compassTargetText.fontStyle = FontStyle.Bold;
        compassTargetText.color = CompassDefaultTargetColor;
    }

    private void BuildEnginePowerBar(RectTransform parent)
    {
        GameObject frameObject = CreateRect("Engine Power Bar", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = new Vector2(1770f, 16f),
            sizeDelta = new Vector2(52f, EnginePowerBarHeight)
        });

        Image frameImage = frameObject.AddComponent<Image>();
        frameImage.color = new Color(0.010f, 0.011f, 0.012f, 0.96f);
        enginePowerBar = frameObject.GetComponent<RectTransform>();

        GameObject backgroundObject = CreateRect("Power Remaining", enginePowerBar, StretchFull());
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.94f, 0.94f, 0.88f, 0.92f);

        engineLiftPowerFill = CreatePowerFill("Lift Power", new Color(0.12f, 0.46f, 0.95f, 0.96f));
        engineModulePowerFill = CreatePowerFill("Module Power", new Color(0.58f, 0.24f, 0.86f, 0.96f));
        engineThrustPowerFill = CreatePowerFill("Thrust Power", EngineForwardThrustColor);

        BuildThrottleGearSelector(parent);
    }

    private void BuildThrottleGearSelector(RectTransform parent)
    {
        engineThrottleGearMarks = new RectTransform[EngineThrottleGearNotches.Length];
        engineThrottleGearTexts = new Text[EngineThrottleGearNotches.Length];

        for (int i = 0; i < EngineThrottleGearNotches.Length; i++)
        {
            int notch = EngineThrottleGearNotches[i];
            float y = EngineGearSelectorBottomY
                + EngineGearSelectorMarkHeight * 0.5f
                + (EnginePowerBarHeight - EngineGearSelectorMarkHeight) * GetGearNotchRatio(notch);
            GameObject markObject = CreateRect("Throttle Gear " + FormatThrottleGearLabel(notch), parent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 0f),
                anchorMax = new Vector2(0f, 0f),
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = new Vector2(EngineGearSelectorX, y),
                sizeDelta = new Vector2(EngineGearSelectorMarkWidth, EngineGearSelectorMarkHeight)
            });

            Image image = markObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.052f, 0.058f, 0.86f);

            Button button = markObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.08f, 0.88f, 1f);
            colors.pressedColor = new Color(1.22f, 0.92f, 0.36f, 1f);
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.45f);
            button.colors = colors;
            int capturedNotch = notch;
            button.onClick.AddListener(() => SelectThrottleGear(capturedNotch));

            Text label = CreateText(markObject.GetComponent<RectTransform>(), FormatThrottleGearLabel(notch), 12, Vector2.zero, new Vector2(EngineGearSelectorMarkWidth, EngineGearSelectorMarkHeight), TextAnchor.MiddleCenter, new Color(0.82f, 0.80f, 0.72f, 1f));
            label.raycastTarget = false;

            engineThrottleGearMarks[i] = markObject.GetComponent<RectTransform>();
            engineThrottleGearTexts[i] = label;
        }
    }

    private RectTransform CreatePowerFill(string name, Color color)
    {
        GameObject fillObject = CreateRect(name, enginePowerBar, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(-8f, 0f)
        });

        Image image = fillObject.AddComponent<Image>();
        image.color = color;
        return fillObject.GetComponent<RectTransform>();
    }

    private Text CreateButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, System.Func<bool> action, out Button button)
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
        image.color = new Color(0.12f, 0.095f, 0.055f, 0.94f);

        button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.095f, 0.055f, 0.94f);
        colors.highlightedColor = new Color(0.28f, 0.20f, 0.09f, 0.98f);
        colors.pressedColor = new Color(0.50f, 0.34f, 0.12f, 1f);
        colors.disabledColor = new Color(0.07f, 0.07f, 0.07f, 0.55f);
        button.colors = colors;
        button.onClick.AddListener(() => action?.Invoke());

        int labelFontSize = size.y <= 38f ? (size.x <= 96f ? 14 : 15) : 21;
        Text label = CreateText(buttonObject.GetComponent<RectTransform>(), "", labelFontSize, new Vector2(0f, 0f), size, TextAnchor.MiddleCenter, new Color(0.94f, 0.84f, 0.62f, 1f));
        label.raycastTarget = false;
        return label;
    }

    private Text CreateHoldButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector3 input)
    {
        Button button;
        Text label = CreateButton(parent, name, anchoredPosition, size, () => true, out button);

        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, () => SetHeldFlightInput(input));
        AddTrigger(trigger, EventTriggerType.PointerUp, () => SetHeldFlightInput(Vector3.zero));
        AddTrigger(trigger, EventTriggerType.PointerExit, () => SetHeldFlightInput(Vector3.zero));
        return label;
    }

    private InputField CreateInputField(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, string placeholder)
    {
        GameObject fieldObject = CreateRect("Input " + name, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = anchoredPosition,
            sizeDelta = size
        });

        Image image = fieldObject.AddComponent<Image>();
        image.color = new Color(0.05f, 0.055f, 0.060f, 0.95f);

        InputField input = fieldObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.contentType = InputField.ContentType.Standard;
        input.textComponent = CreateText(fieldObject.GetComponent<RectTransform>(), "", 15, new Vector2(8f, -3f), new Vector2(size.x - 12f, size.y - 4f), TextAnchor.MiddleLeft, new Color(0.90f, 0.88f, 0.78f, 1f));
        input.placeholder = CreateText(fieldObject.GetComponent<RectTransform>(), placeholder, 15, new Vector2(8f, -3f), new Vector2(size.x - 12f, size.y - 4f), TextAnchor.MiddleLeft, new Color(0.45f, 0.48f, 0.50f, 1f));
        return input;
    }

    private Text CreateCompassText(RectTransform parent, string initialText)
    {
        GameObject textObject = CreateRect("Compass Tick Text", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 1f),
            anchorMax = new Vector2(0.5f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -4f),
            sizeDelta = new Vector2(70f, 18f)
        });

        Text text = textObject.AddComponent<Text>();
        text.font = GetDefaultFont();
        text.fontSize = 13;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.90f, 0.88f, 0.80f, 0.82f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        text.text = initialText ?? "";
        return text;
    }

    private RectTransform CreateCompassLine(RectTransform parent, float height)
    {
        GameObject lineObject = CreateRect("Compass Tick Line", parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0.5f, 1f),
            anchorMax = new Vector2(0.5f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -23f),
            sizeDelta = new Vector2(1.5f, height)
        });

        Image image = lineObject.AddComponent<Image>();
        image.color = new Color(0.88f, 0.82f, 0.66f, 0.48f);
        return lineObject.GetComponent<RectTransform>();
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action?.Invoke());
        trigger.triggers.Add(entry);
    }

    private RectTransform CreatePanel(string name, Transform parent, RectTransformSpec spec, Color color)
    {
        GameObject panelObject = CreateRect(name, parent, spec);
        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        return panelObject.GetComponent<RectTransform>();
    }

    private RectTransform CreateDecorativePanel(string name, Transform parent, RectTransformSpec spec, Color color)
    {
        RectTransform panel = CreatePanel(name, parent, spec, color);
        Image image = panel.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = false;
        }

        return panel;
    }

    private RectTransform CreateGlassPanel(string name, Transform parent, RectTransformSpec spec, Color color, bool shadow = true)
    {
        RectTransform panel = CreatePanel(name, parent, spec, color);
        Image image = panel.GetComponent<Image>();
        ApplyRoundedSprite(image, GetRoundedPanelSprite());
        if (shadow)
        {
            AddSoftShadow(panel, new Color(0.20f, 0.30f, 0.45f, 0.18f), new Vector2(0f, -4f));
        }

        return panel;
    }

    private Text CreateGlassButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        System.Func<bool> action,
        out Button button,
        string label = "")
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
        image.color = PortGlassStrong;
        ApplyRoundedSprite(image, GetRoundedSmallSprite());
        AddSoftShadow(buttonObject.GetComponent<RectTransform>(), new Color(0.18f, 0.26f, 0.38f, 0.16f), new Vector2(0f, -3f));

        button = buttonObject.AddComponent<Button>();
        ConfigureGlassButton(button, image);
        button.onClick.AddListener(() => action?.Invoke());

        Text text = CreateText(buttonObject.GetComponent<RectTransform>(), label, size.y >= 70f ? 20 : 17, Vector2.zero, size, TextAnchor.MiddleCenter, PortText);
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        return text;
    }

    private Text CreateRoundGlassButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        float size,
        System.Func<bool> action,
        out Button button,
        string label = "")
    {
        GameObject buttonObject = CreateRect("Button " + name, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = anchoredPosition,
            sizeDelta = new Vector2(size, size)
        });

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = GetCircleSprite();
        image.type = Image.Type.Simple;
        image.color = PortGlassStrong;
        AddSoftShadow(buttonObject.GetComponent<RectTransform>(), new Color(0.18f, 0.26f, 0.38f, 0.18f), new Vector2(0f, -3f));

        button = buttonObject.AddComponent<Button>();
        ConfigureGlassButton(button, image);
        button.onClick.AddListener(() => action?.Invoke());

        Text text = CreateText(buttonObject.GetComponent<RectTransform>(), label, 32, Vector2.zero, new Vector2(size, size), TextAnchor.MiddleCenter, new Color(0.51f, 0.57f, 0.68f, 1f));
        text.fontStyle = FontStyle.Bold;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateMainHudIconImage(
        RectTransform parent,
        string name,
        string iconResourceName,
        RectTransformSpec spec,
        Color color)
    {
        Sprite sprite = GetMainHudIconSprite(iconResourceName);
        if (sprite == null)
        {
            return null;
        }

        GameObject iconObject = CreateRect(name, parent, spec);
        Image image = iconObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void SetFallbackTextVisibleWhenIconMissing(Text fallbackText, Image iconImage)
    {
        if (fallbackText == null || iconImage == null)
        {
            return;
        }

        fallbackText.gameObject.SetActive(false);
    }

    private static void SetGraphicRaycastTarget(Graphic graphic, bool raycastTarget)
    {
        if (graphic != null)
        {
            graphic.raycastTarget = raycastTarget;
        }
    }

    private static Sprite GetMainHudIconSprite(string iconResourceName)
    {
        if (string.IsNullOrWhiteSpace(iconResourceName))
        {
            return null;
        }

        if (cachedMainHudIconSprites.TryGetValue(iconResourceName, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

        Texture2D texture = Resources.Load<Texture2D>(MainHudIconResourceFolderPath + iconResourceName);
        if (texture == null)
        {
            cachedMainHudIconSprites[iconResourceName] = null;
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(texture.width, texture.height),
            0,
            SpriteMeshType.FullRect);
        sprite.name = iconResourceName;
        cachedMainHudIconSprites[iconResourceName] = sprite;
        return sprite;
    }

    private static void ConfigureGlassButton(Button button, Graphic targetGraphic)
    {
        if (button == null)
        {
            return;
        }

        button.targetGraphic = targetGraphic;
        ColorBlock colors = button.colors;
        colors.normalColor = PortGlassStrong;
        colors.highlightedColor = new Color(0.98f, 1.00f, 1.00f, 0.98f);
        colors.pressedColor = new Color(0.78f, 0.88f, 0.98f, 0.98f);
        colors.selectedColor = new Color(0.90f, 0.96f, 1.00f, 1f);
        colors.disabledColor = new Color(0.72f, 0.76f, 0.82f, 0.48f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    private static void ApplyRoundedSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
    }

    private static void AddSoftShadow(RectTransform target, Color color, Vector2 distance)
    {
        if (target == null || target.GetComponent<Shadow>() != null)
        {
            return;
        }

        Shadow shadow = target.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private Text CreateText(RectTransform parent, string initialText, int fontSize, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateRect("Text", parent, new RectTransformSpec
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
        text.alignByGeometry = true;
        text.raycastTarget = false;
        text.text = initialText ?? "";
        return text;
    }

    private void RefreshTexts()
    {
        SetText(dockText, WildWindLocalization.Get("game.hud.dock"));
        SetText(flightMenuText, WildWindLocalization.Get("game.hud.menu"));
        SetText(resourcesText, "Ресурсы");
        RefreshClaudiumSlipstreamText();
        RefreshState(true);
    }

    private void RefreshState(bool force)
    {
        MetaGameState currentMeta = ResolveMeta();
        WildWindGameplaySession currentSession = ResolveSession();
        if (currentMeta == null || currentMeta.progress == null)
        {
            baseBuildingFocusActive = false;
            if (sortiePanel != null)
            {
                sortiePanel.gameObject.SetActive(false);
            }

            SetHudWindowLayerVisible(false);
            SetRadarSessionOverlayVisible(false);
            RefreshPlayerProfile(false);
            RefreshMetaResourceCounters(false);
            RefreshMetaTopRightButtons(false);
            RefreshMetaLeftSideButtons(false);
            RefreshMetaDockButton(false);
            RefreshMetaDockScreen(false);
            RefreshMetaQuestPanel(false);
            RefreshMetaProjectPanel(false);
            return;
        }

        currentMeta.EnsureProgressInitialized();
        PlayerProgress progress = currentMeta.progress;
        bool docked = currentMeta.CurrentMode == GameSessionMode.Docked;
        if (!docked)
        {
            metaScreenMode = MetaScreenMode.Port;
            baseBuildingFocusActive = false;
        }

        bool portScreenActive = docked && metaScreenMode == MetaScreenMode.Port && !baseBuildingFocusActive;
        bool dockScreenActive = docked && metaScreenMode == MetaScreenMode.Dock;
        float distance = 0f;
        SortieReturnEstimate sortieEstimate = currentMeta.HasActiveSortie
            ? currentMeta.GetActiveSortieReturnEstimate()
            : default;
        string selectedSortieName = currentMeta.GetSelectedSessionSortieDisplayName();
        string selectedSortieRequirement = currentMeta.GetSelectedSessionSortieRequirementText();

        flightPanel.gameObject.SetActive(!docked);
        compassPanel.gameObject.SetActive(!docked);
        RefreshPlayerProfile(portScreenActive);
        RefreshMetaResourceCounters(portScreenActive);
        RefreshMetaTopRightButtons(portScreenActive);
        RefreshMetaLeftSideButtons(portScreenActive);
        RefreshMetaDockButton(portScreenActive);
        RefreshMetaDockScreen(dockScreenActive);
        RefreshMetaQuestPanel(portScreenActive);
        RefreshMetaProjectPanel(portScreenActive);
        SetButtonVisible(resourcesText, !docked);
        SetHudWindowLayerVisible(!docked);
        if (docked)
        {
            SetRadarSessionOverlayVisible(false);
        }

        SetText(modeText, docked ? "" : WildWindLocalization.Get("game.hud.flight"));
        SetText(objectiveText, docked ? "" : "Session extraction");
        SetText(sessionPathText, docked
                ? ""
                : currentMeta.HasActiveSortie
                ? currentMeta.ActiveSortie.zone.displayName + " -> manual extraction"
                : "Base -> " + selectedSortieName + " (" + selectedSortieRequirement + ")");
        SetText(dockText, "Extract home");
        WildWindFlightControlBridge controls = ResolveFlightControls();
        float currentAltitude = GetPlayerPosition(currentSession).y;
        if (sortiePanel != null)
        {
            sortiePanel.gameObject.SetActive(false);
        }

        RefreshHudWindows(currentMeta, currentSession, progress, controls, sortieEstimate, !docked);
        RefreshResourceCatalogWindow(currentMeta.SessionConfig);
        RefreshDevelopmentWindow(currentMeta);
        SetText(distanceText, docked
            ? ""
            : currentMeta.HasActiveSortie
            ? FormatBoundaryDistance(sortieEstimate.distanceToBoundaryMeters)
                + " | Slip " + sortieEstimate.extractionRunupSeconds.ToString("0.0")
                + "/" + sortieEstimate.requiredExtractionRunupSeconds.ToString("0.0")
                + "s"
            : WildWindLocalization.Get("game.hud.distance") + ": " + FormatDistance(distance));
        SetText(altitudeText, WildWindLocalization.Get("game.hud.altitude") + ": " + currentAltitude.ToString("0") + " м");
        SetText(speedText, WildWindLocalization.Get("game.hud.speed") + ": " + GetShipSpeed(currentMeta).ToString("0.0") + " м/с");
        string runupStatus = !docked && currentMeta.HasActiveSortie
            ? currentMeta.ActiveSortieExtractionRunupStatus
            : "";
        string visibleStatus = statusMessage;
        if (!string.IsNullOrWhiteSpace(runupStatus))
        {
            visibleStatus = runupStatus;
        }
        else if (!docked && currentMeta.HasActiveSortie && string.IsNullOrWhiteSpace(visibleStatus))
        {
            visibleStatus = sortieEstimate.status;
        }

        SetText(statusText, visibleStatus);
        RefreshEnginePowerBar();

        if (controls != null)
        {
            RefreshClaudiumSlipstreamText();
        }

        SetInteractable(dockButton, !docked && currentMeta.HasActiveSortie && sortieEstimate.canExtract);
        SetInteractable(resourcesButton, currentMeta.SessionConfig != null && currentMeta.SessionConfig.items.Count > 0);
    }

    private void SetHudWindowLayerVisible(bool visible)
    {
        if (windowDockPanel != null)
        {
            windowDockPanel.gameObject.SetActive(visible);
        }

        foreach (KeyValuePair<string, HudWindow> pair in hudWindows)
        {
            bool keepDockedResourceVisible = pair.Key == WindowResourcesId && pair.Value.IsOpen;
            bool keepDockedMetaTopWindowVisible = IsMetaTopRightWindowId(pair.Key) && pair.Value.IsOpen;
            bool keepDockedMetaLeftWindowVisible = IsMetaLeftSideWindowId(pair.Key) && pair.Value.IsOpen;
            bool keepDockedAllTasksVisible = pair.Key == WindowMetaAllTasksId && pair.Value.IsOpen;
            bool keepDockedDevelopmentVisible = pair.Key == WindowDevelopmentId && pair.Value.IsOpen;
            pair.Value.SetFlightVisible(visible
                || keepDockedResourceVisible
                || keepDockedMetaTopWindowVisible
                || keepDockedMetaLeftWindowVisible
                || keepDockedAllTasksVisible
                || keepDockedDevelopmentVisible);
        }
    }

    private static bool IsMetaTopRightWindowId(string windowId)
    {
        for (int i = 0; i < MetaTopRightButtons.Length; i++)
        {
            if (MetaTopRightButtons[i].windowId == windowId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMetaLeftSideWindowId(string windowId)
    {
        for (int i = 0; i < MetaLeftSideButtons.Length; i++)
        {
            if (MetaLeftSideButtons[i].windowId == windowId)
            {
                return true;
            }
        }

        return false;
    }

    private RadarTacticalSessionOverlay EnsureRadarSessionOverlay()
    {
        if (radarSessionOverlay != null)
        {
            return radarSessionOverlay;
        }

        GameObject overlayObject = new GameObject("Radar Tactical Session Overlay");
        overlayObject.transform.SetParent(transform, false);
        radarSessionOverlay = overlayObject.AddComponent<RadarTacticalSessionOverlay>();
        return radarSessionOverlay;
    }

    private void SetRadarSessionOverlayVisible(bool visible)
    {
        if (radarSessionOverlay != null)
        {
            radarSessionOverlay.SetVisible(visible);
        }
    }

    private bool IsHudWindowOpen(string windowId)
    {
        return hudWindows.TryGetValue(windowId, out HudWindow window) && window.IsOpen;
    }

    private bool ToggleHudWindow(string windowId)
    {
        if (!hudWindows.TryGetValue(windowId, out HudWindow window))
        {
            return false;
        }

        window.ToggleOpen();
        RefreshState(true);
        return true;
    }

    private bool ToggleResourcesWindow()
    {
        return ToggleHudWindow(WindowResourcesId);
    }

    private bool OpenDevelopmentWindow()
    {
        if (!hudWindows.TryGetValue(WindowDevelopmentId, out HudWindow window) || window == null)
        {
            return false;
        }

        metaScreenMode = MetaScreenMode.Port;
        window.SetOpen(true);
        RefreshState(true);
        return window.IsOpen;
    }

    private bool OpenDevelopmentWindowFromDock()
    {
        bool opened = OpenDevelopmentWindow();
        if (opened)
        {
            statusMessage = "Выбери корабль и нажми «Купить в док».";
            RefreshState(true);
        }

        return opened;
    }

    private bool OpenResourceCatalogWindow()
    {
        if (!hudWindows.TryGetValue(WindowResourcesId, out HudWindow window))
        {
            return false;
        }

        window.SetOpen(true);
        RefreshState(true);
        return true;
    }

    private bool OpenKnowledgeScreen()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null || currentMeta.CurrentMode != GameSessionMode.Docked)
        {
            return false;
        }

        CloseAllHudWindows();
        metaScreenMode = MetaScreenMode.Port;
        WildWindKnowledgeScreen screen = EnsureKnowledgeScreen();
        screen.BindRuntime(currentMeta);
        screen.SetVisible(true);
        statusMessage = "Archive knowledge screen opened.";
        RefreshState(true);
        return screen.IsVisible;
    }

    private WildWindKnowledgeScreen EnsureKnowledgeScreen()
    {
        if (knowledgeScreen != null)
        {
            knowledgeScreen.BindRuntime(ResolveMeta());
            return knowledgeScreen;
        }

        knowledgeScreenObject = new GameObject("Wild Wind Knowledge Screen");
        knowledgeScreenObject.transform.SetParent(transform, false);
        knowledgeScreen = knowledgeScreenObject.AddComponent<WildWindKnowledgeScreen>();
        knowledgeScreen.BindRuntime(ResolveMeta());
        knowledgeScreen.SetCloseAction(() =>
        {
            knowledgeScreen.SetVisible(false);
            statusMessage = "Archive knowledge screen closed.";
            RefreshState(true);
            return true;
        });
        knowledgeScreen.SetVisible(false);
        return knowledgeScreen;
    }

    private void RefreshResourceCatalogWindow(SessionConfigDatabase config)
    {
        if (resourceCatalogContent == null)
        {
            return;
        }

        int itemCount = config != null && config.items != null ? config.items.Count : 0;
        int categoryCount = config != null && config.resourceCategories != null ? config.resourceCategories.Count : 0;
        if (itemCount == resourceCatalogBuildItemCount && categoryCount == resourceCatalogBuildCategoryCount)
        {
            return;
        }

        resourceCatalogBuildItemCount = itemCount;
        resourceCatalogBuildCategoryCount = categoryCount;
        resourceCatalogRows.Clear();

        for (int i = resourceCatalogContent.childCount - 1; i >= 0; i--)
        {
            Destroy(resourceCatalogContent.GetChild(i).gameObject);
        }

        if (config == null || config.items == null || config.items.Count == 0)
        {
            SetText(resourceCatalogHeaderText, "Resources: config unavailable");
            resourceCatalogContent.sizeDelta = new Vector2(0f, 0f);
            return;
        }

        SetText(resourceCatalogHeaderText, "Resources: " + config.items.Count + " items | icons " + CountExistingResourceIconAssets(config));

        HashSet<string> placed = new HashSet<string>();
        float y = -8f;
        if (config.resourceCategories != null && config.resourceCategories.Count > 0)
        {
            for (int i = 0; i < config.resourceCategories.Count; i++)
            {
                ResourceCategoryConfig category = config.resourceCategories[i];
                if (category == null || category.itemIds == null || category.itemIds.Count == 0) continue;
                y = AddResourceCategoryHeader(category.DisplayNameRu, y);
                for (int itemIndex = 0; itemIndex < category.itemIds.Count; itemIndex++)
                {
                    string itemId = category.itemIds[itemIndex];
                    ItemConfig item = config.GetItem(itemId);
                    if (item == null || placed.Contains(item.id)) continue;
                    y = AddResourceCatalogRow(item, category.id, y);
                    placed.Add(item.id);
                }
            }
        }

        bool hasUncategorized = false;
        for (int i = 0; i < config.items.Count; i++)
        {
            ItemConfig item = config.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id) || placed.Contains(item.id)) continue;
            if (!hasUncategorized)
            {
                y = AddResourceCategoryHeader("Uncategorized", y);
                hasUncategorized = true;
            }

            y = AddResourceCatalogRow(item, "uncategorized", y);
            placed.Add(item.id);
        }

        resourceCatalogContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, -y + 8f));
    }

    private void RefreshDevelopmentWindow(MetaGameState currentMeta)
    {
        if (developmentWindowText == null || developmentSupplierTabRoot == null || developmentTreeRoot == null || developmentDetailsRoot == null)
        {
            return;
        }

        IReadOnlyList<ShipTreeEntryConfig> entries = currentMeta != null ? currentMeta.GetShipTreeEntryConfigs() : null;
        List<DevelopmentSupplierView> suppliers = BuildDevelopmentSupplierViews(entries);
        developmentWindowSupplierCount = suppliers.Count;

        if (suppliers.Count == 0)
        {
            developmentWindowTileCount = 0;
            developmentWindowConnectionCount = 0;
            developmentWindowLayoutKey = "";
            developmentDetailsShipId = "";
            developmentShipTilesById.Clear();
            ClearChildren(developmentSupplierTabRoot);
            ClearChildren(developmentTreeRoot);
            ClearChildren(developmentDetailsRoot);
            SetText(developmentWindowText, "Корабельный каталог не загружен.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedDevelopmentSupplierId) || FindDevelopmentSupplier(suppliers, selectedDevelopmentSupplierId) == null)
        {
            selectedDevelopmentSupplierId = suppliers[0].factionId;
            developmentWindowLayoutKey = "";
        }

        DevelopmentSupplierView selectedSupplier = FindDevelopmentSupplier(suppliers, selectedDevelopmentSupplierId);
        EnsureDevelopmentSelectedShip(selectedSupplier);

        string layoutKey = BuildDevelopmentWindowLayoutKey(suppliers, selectedDevelopmentSupplierId);
        bool rebuilt = false;
        if (layoutKey != developmentWindowLayoutKey)
        {
            developmentWindowLayoutKey = layoutKey;
            RebuildDevelopmentWindowLayout(suppliers);
            rebuilt = true;
        }

        if (!rebuilt)
        {
            if (!string.Equals(developmentDetailsShipId, selectedDevelopmentShipId, StringComparison.OrdinalIgnoreCase))
            {
                BuildDevelopmentShipDetails(selectedSupplier);
            }

            RefreshDevelopmentTileVisuals();
        }

        int developmentShipCount = CountDevelopmentSupplierShips(suppliers);
        SetText(developmentWindowText, "Корабли развития: "
            + developmentShipCount
            + " | Фракции: "
            + developmentWindowSupplierCount
            + " | Ранги I-X");
    }

    private List<DevelopmentSupplierView> BuildDevelopmentSupplierViews(IReadOnlyList<ShipTreeEntryConfig> entries)
    {
        List<DevelopmentSupplierView> suppliers = new List<DevelopmentSupplierView>();
        Dictionary<string, DevelopmentSupplierView> byFaction = new Dictionary<string, DevelopmentSupplierView>(StringComparer.OrdinalIgnoreCase);
        if (entries == null)
        {
            return suppliers;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ShipTreeEntryConfig entry = entries[i];
            if (entry == null || !entry.IsDevelopmentRosterShip || string.IsNullOrWhiteSpace(entry.factionId))
            {
                continue;
            }

            if (!byFaction.TryGetValue(entry.factionId, out DevelopmentSupplierView supplier))
            {
                supplier = new DevelopmentSupplierView
                {
                    factionId = entry.factionId,
                    displayName = GetDevelopmentFactionDisplayName(entry.factionId, entry.FactionDisplayNameRu),
                    color = entry.visualColor
                };
                byFaction[entry.factionId] = supplier;
                suppliers.Add(supplier);
            }

            if (IsDevelopmentStarterEntry(entry))
            {
                supplier.starterShip = entry;
                continue;
            }

            supplier.ships.Add(entry);
        }

        suppliers.Sort(CompareDevelopmentSuppliers);
        return suppliers;
    }

    private void RebuildDevelopmentWindowLayout(List<DevelopmentSupplierView> suppliers)
    {
        ClearChildren(developmentSupplierTabRoot);
        ClearChildren(developmentTreeRoot);
        ClearChildren(developmentDetailsRoot);
        developmentShipTilesById.Clear();
        hoveredDevelopmentShipId = "";
        developmentDetailsShipId = "";
        developmentWindowTileCount = 0;
        developmentWindowConnectionCount = 0;

        DevelopmentSupplierView selectedSupplier = FindDevelopmentSupplier(suppliers, selectedDevelopmentSupplierId);
        if (selectedSupplier == null && suppliers.Count > 0)
        {
            selectedSupplier = suppliers[0];
            selectedDevelopmentSupplierId = selectedSupplier.factionId;
        }

        EnsureDevelopmentSelectedShip(selectedSupplier);
        BuildDevelopmentSupplierTabs(suppliers);
        if (selectedSupplier == null)
        {
            return;
        }

        BuildDevelopmentTreeGrid(selectedSupplier);
        BuildDevelopmentShipDetails(selectedSupplier);
        RefreshDevelopmentTileVisuals();

        if (developmentTreeScrollRect != null)
        {
            developmentTreeScrollRect.horizontalNormalizedPosition = 0f;
            developmentTreeScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void BuildDevelopmentSupplierTabs(List<DevelopmentSupplierView> suppliers)
    {
        Text title = CreateText(developmentSupplierTabRoot, "ФРАКЦИИ", 16, new Vector2(14f, -12f), new Vector2(210f, 26f), TextAnchor.MiddleLeft, new Color(0.88f, 0.82f, 0.64f, 1f));
        title.fontStyle = FontStyle.Bold;

        const float tabWidth = 222f;
        const float tabHeight = 42f;
        const float tabGap = 7f;
        for (int i = 0; i < suppliers.Count; i++)
        {
            DevelopmentSupplierView supplier = suppliers[i];
            bool selected = supplier.factionId == selectedDevelopmentSupplierId;
            string factionId = supplier.factionId;
            Text label = CreateButton(
                developmentSupplierTabRoot,
                "Development Supplier " + factionId,
                new Vector2(14f, -48f - i * (tabHeight + tabGap)),
                new Vector2(tabWidth, tabHeight),
                () => SelectDevelopmentSupplier(factionId),
                out Button button);
            SetText(label, ShortenButtonLabel(supplier.displayName, 22));
            label.fontSize = 15;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(16f, label.rectTransform.offsetMin.y);
            TintDevelopmentSupplierButton(button, selected, supplier.color);
        }
    }

    private void BuildDevelopmentTreeGrid(DevelopmentSupplierView supplier)
    {
        const float rankLabelX = 18f;
        const float rankOneY = -104f;
        const float branchHeaderY = -48f;

        List<DevelopmentBranchView> branches = BuildDevelopmentBranchViews(supplier);
        float branchAreaWidth = branches.Count > 0
            ? (branches.Count - 1) * DevelopmentBranchStep + DevelopmentTileWidth
            : DevelopmentTileWidth;
        float contentWidth = Mathf.Max(
            DevelopmentTreeViewportWidth,
            branchAreaWidth + DevelopmentTreeContentPaddingX * 2f + 48f);
        float contentHeight = Mathf.Max(
            DevelopmentTreeViewportHeight,
            DevelopmentTreeContentPaddingY * 2f + DevelopmentTileHeight + (DevelopmentTreeTierCount - 1) * DevelopmentRankStep + 96f);

        developmentTreeRoot.sizeDelta = new Vector2(contentWidth, contentHeight);

        float gridX = Mathf.Max(DevelopmentTreeContentPaddingX, (contentWidth - branchAreaWidth) * 0.5f);
        float railX = DevelopmentTreeContentPaddingX * 0.55f;
        float railWidth = Mathf.Max(DevelopmentTileWidth, contentWidth - railX - DevelopmentTreeContentPaddingX * 0.65f);

        for (int rank = 1; rank <= DevelopmentTreeTierCount; rank++)
        {
            float y = GetDevelopmentRankY(rankOneY, rank);
            Text rankText = CreateText(developmentTreeRoot, FormatRomanTier(rank), 16, new Vector2(rankLabelX, y - 2f), new Vector2(38f, 24f), TextAnchor.MiddleCenter, new Color(0.76f, 0.84f, 0.88f, 0.82f));
            rankText.fontStyle = FontStyle.Bold;
            CreateDecorativePanel("Development Rank Rail " + rank, developmentTreeRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(railX, y - DevelopmentTileHeight * 0.5f),
                sizeDelta = new Vector2(railWidth, 1.5f)
            }, new Color(0.32f, 0.44f, 0.50f, rank == 1 ? 0.22f : 0.11f));
        }

        for (int i = 0; i < branches.Count; i++)
        {
            DevelopmentBranchView branch = branches[i];
            float x = gridX + i * DevelopmentBranchStep;
            Text branchText = CreateText(developmentTreeRoot, ShortenButtonLabel(branch.displayName, 17), 14, new Vector2(x, branchHeaderY), new Vector2(DevelopmentTileWidth, 30f), TextAnchor.MiddleCenter, new Color(0.90f, 0.78f, 0.56f, 0.86f));
            branchText.fontStyle = FontStyle.Bold;
        }

        Dictionary<string, Vector2> centersByShip = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        float starterX = gridX + Mathf.Max(0f, (branches.Count - 1) * DevelopmentBranchStep) * 0.5f;
        Vector2 starterTopLeft = new Vector2(starterX, rankOneY);
        string starterShipId = supplier.starterShip != null ? supplier.starterShip.shipId : "pioneer";
        centersByShip[starterShipId] = GetDevelopmentTileCenter(starterTopLeft);
        centersByShip["pioneer"] = GetDevelopmentTileCenter(starterTopLeft);

        for (int i = 0; i < branches.Count; i++)
        {
            DevelopmentBranchView branch = branches[i];
            for (int j = 0; j < branch.ships.Count; j++)
            {
                ShipTreeEntryConfig ship = branch.ships[j];
                int rank = Mathf.Clamp(ship.treeTier, 1, DevelopmentTreeTierCount);
                Vector2 topLeft = new Vector2(gridX + i * DevelopmentBranchStep, GetDevelopmentRankY(rankOneY, rank));
                centersByShip[ship.shipId] = GetDevelopmentTileCenter(topLeft);
            }
        }

        for (int i = 0; i < branches.Count; i++)
        {
            DevelopmentBranchView branch = branches[i];
            for (int j = 0; j < branch.ships.Count; j++)
            {
                ShipTreeEntryConfig ship = branch.ships[j];
                if (ship.parentShipIds == null || ship.parentShipIds.Count == 0)
                {
                    continue;
                }

                for (int parentIndex = 0; parentIndex < ship.parentShipIds.Count; parentIndex++)
                {
                    string parentId = ship.parentShipIds[parentIndex];
                    if (!centersByShip.TryGetValue(parentId, out Vector2 parentCenter) || !centersByShip.TryGetValue(ship.shipId, out Vector2 childCenter))
                    {
                        continue;
                    }

                    DrawDevelopmentConnection(parentCenter, childCenter, supplier.color);
                    developmentWindowConnectionCount++;
                }
            }
        }

        CreateDevelopmentStarterTile(starterTopLeft, supplier);
        developmentWindowTileCount++;

        for (int i = 0; i < branches.Count; i++)
        {
            DevelopmentBranchView branch = branches[i];
            for (int j = 0; j < branch.ships.Count; j++)
            {
                ShipTreeEntryConfig ship = branch.ships[j];
                int rank = Mathf.Clamp(ship.treeTier, 1, DevelopmentTreeTierCount);
                Vector2 topLeft = new Vector2(gridX + i * DevelopmentBranchStep, GetDevelopmentRankY(rankOneY, rank));
                CreateDevelopmentShipTile(topLeft, ship);
                developmentWindowTileCount++;
            }
        }
    }

    private List<DevelopmentBranchView> BuildDevelopmentBranchViews(DevelopmentSupplierView supplier)
    {
        List<DevelopmentBranchView> branches = new List<DevelopmentBranchView>();
        Dictionary<string, DevelopmentBranchView> byBranch = new Dictionary<string, DevelopmentBranchView>(StringComparer.OrdinalIgnoreCase);
        if (supplier == null)
        {
            return branches;
        }

        for (int i = 0; i < supplier.ships.Count; i++)
        {
            ShipTreeEntryConfig ship = supplier.ships[i];
            if (ship == null)
            {
                continue;
            }

            string branchId = string.IsNullOrWhiteSpace(ship.branchId) ? ship.shipClassId + "_line" : ship.branchId;
            if (!byBranch.TryGetValue(branchId, out DevelopmentBranchView branch))
            {
                branch = new DevelopmentBranchView
                {
                    branchId = branchId,
                    displayName = string.IsNullOrWhiteSpace(ship.BranchDisplayNameRu) ? ship.ClassDisplayNameRu : ship.BranchDisplayNameRu,
                    treeRow = Mathf.Max(0, ship.treeRow)
                };
                byBranch[branchId] = branch;
                branches.Add(branch);
            }

            branch.treeRow = Mathf.Min(branch.treeRow, Mathf.Max(0, ship.treeRow));
            branch.ships.Add(ship);
        }

        branches.Sort(CompareDevelopmentBranches);
        for (int i = 0; i < branches.Count; i++)
        {
            branches[i].ships.Sort(CompareDevelopmentShips);
        }

        return branches;
    }

    private static int CompareDevelopmentBranches(DevelopmentBranchView left, DevelopmentBranchView right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int rowComparison = left.treeRow.CompareTo(right.treeRow);
        return rowComparison != 0 ? rowComparison : string.Compare(left.branchId, right.branchId, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDevelopmentShips(ShipTreeEntryConfig left, ShipTreeEntryConfig right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int tierComparison = left.treeTier.CompareTo(right.treeTier);
        return tierComparison != 0 ? tierComparison : string.Compare(left.shipId, right.shipId, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDevelopmentSuppliers(DevelopmentSupplierView left, DevelopmentSupplierView right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        int orderComparison = GetDevelopmentFactionOrder(left.factionId).CompareTo(GetDevelopmentFactionOrder(right.factionId));
        return orderComparison != 0 ? orderComparison : string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
    }

    private void CreateDevelopmentStarterTile(Vector2 topLeft, DevelopmentSupplierView supplier)
    {
        ShipTreeEntryConfig starter = supplier != null ? supplier.starterShip : null;
        bool selected = starter != null && string.Equals(starter.shipId, selectedDevelopmentShipId, StringComparison.OrdinalIgnoreCase);
        Color normalColor = new Color(0.085f, 0.105f, 0.120f, 0.98f);
        Color selectedColor = new Color(0.145f, 0.160f, 0.172f, 1f);
        RectTransform tile = CreatePanel("Development Starter Tile", developmentTreeRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = topLeft,
            sizeDelta = new Vector2(DevelopmentTileWidth, DevelopmentTileHeight)
        }, selected ? selectedColor : normalColor);

        Image tileImage = tile.GetComponent<Image>();
        string starterId = starter != null ? starter.shipId : "pioneer";
        InstallDevelopmentTreePanHandler(tile);

        if (starter != null)
        {
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tileImage;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectDevelopmentShip(starterId));
            AddDevelopmentTilePointerEvents(tile, starterId);
        }

        Outline outline = tile.gameObject.AddComponent<Outline>();
        outline.effectColor = selected ? Color.white : new Color(0.92f, 0.74f, 0.36f, 0.65f);
        outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        RegisterDevelopmentTile(starterId, tileImage, outline, normalColor, selectedColor, new Color(0.92f, 0.74f, 0.36f, 0.65f));

        CreateDecorativePanel("Starter Empty Image", tile, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(12f, -28f),
            sizeDelta = new Vector2(126f, 32f)
        }, new Color(0.10f, 0.13f, 0.15f, 0.92f));

        Text rank = CreateText(tile, "I", 15, new Vector2(12f, -4f), new Vector2(42f, 22f), TextAnchor.MiddleLeft, new Color(0.92f, 0.92f, 0.86f, 1f));
        rank.fontStyle = FontStyle.Bold;

        string starterName = starter != null ? starter.DisplayNameRu : "ПИОНЕР";
        Text label = CreateText(tile, ShortenButtonLabel(starterName, 18).ToUpperInvariant(), 14, new Vector2(12f, -54f), new Vector2(126f, 22f), TextAnchor.MiddleRight, supplier != null ? supplier.color : new Color(0.96f, 0.90f, 0.72f, 1f));
        label.fontStyle = FontStyle.Bold;
    }

    private void CreateDevelopmentShipTile(Vector2 topLeft, ShipTreeEntryConfig ship)
    {
        Color baseColor = ship != null ? ship.visualColor : Color.white;
        bool selected = ship != null && string.Equals(ship.shipId, selectedDevelopmentShipId, StringComparison.OrdinalIgnoreCase);
        Color normalColor = new Color(0.065f, 0.078f, 0.092f, 0.98f);
        Color selectedColor = new Color(0.145f, 0.160f, 0.172f, 1f);
        RectTransform tile = CreatePanel("Development Ship Tile " + (ship != null ? ship.shipId : "empty"), developmentTreeRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = topLeft,
            sizeDelta = new Vector2(DevelopmentTileWidth, DevelopmentTileHeight)
        }, selected ? selectedColor : normalColor);

        Image tileImage = tile.GetComponent<Image>();
        InstallDevelopmentTreePanHandler(tile);

        if (ship != null)
        {
            Button button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tileImage;
            button.transition = Selectable.Transition.None;
            string shipId = ship.shipId;
            button.onClick.AddListener(() => SelectDevelopmentShip(shipId));
            AddDevelopmentTilePointerEvents(tile, shipId);
        }

        Outline outline = tile.gameObject.AddComponent<Outline>();
        outline.effectColor = selected ? Color.white : new Color(baseColor.r, baseColor.g, baseColor.b, ship != null && ship.HasRuntimeHull ? 0.82f : 0.38f);
        outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        if (ship != null)
        {
            RegisterDevelopmentTile(ship.shipId, tileImage, outline, normalColor, selectedColor, new Color(baseColor.r, baseColor.g, baseColor.b, ship.HasRuntimeHull ? 0.82f : 0.38f));
        }

        CreateDecorativePanel("Ship Empty Image", tile, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(12f, -28f),
            sizeDelta = new Vector2(126f, 32f)
        }, new Color(baseColor.r * 0.25f, baseColor.g * 0.25f, baseColor.b * 0.25f, 0.92f));

        string tierText = ship != null ? FormatRomanTier(Mathf.Clamp(ship.treeTier, 1, DevelopmentTreeTierCount)) : "";
        Text tier = CreateText(tile, tierText, 15, new Vector2(12f, -4f), new Vector2(42f, 22f), TextAnchor.MiddleLeft, new Color(0.92f, 0.92f, 0.86f, 1f));
        tier.fontStyle = FontStyle.Bold;

        string name = ship != null ? ShortenButtonLabel(ship.DisplayNameRu, 18).ToUpperInvariant() : "";
        Text nameText = CreateText(tile, name, 14, new Vector2(12f, -54f), new Vector2(126f, 22f), TextAnchor.MiddleRight, new Color(0.91f, 0.94f, 0.90f, 1f));
        nameText.fontStyle = FontStyle.Bold;
    }

    public static bool IsHudPointerCaptureActiveForCamera => hudPointerCaptureActiveForCamera;

    private void InstallDevelopmentTreePanHandler(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        DevelopmentTreeMousePanHandler handler = target.gameObject.GetComponent<DevelopmentTreeMousePanHandler>();
        if (handler == null)
        {
            handler = target.gameObject.AddComponent<DevelopmentTreeMousePanHandler>();
        }

        handler.owner = this;
    }

    private void SetDevelopmentTreePointerCapture(bool active)
    {
        hudPointerCaptureActiveForCamera = active;
    }

    private void PanDevelopmentTreeScroll(Vector2 pointerDelta)
    {
        if (developmentTreeScrollRect == null || developmentTreeRoot == null || developmentTreeViewport == null)
        {
            return;
        }

        Rect viewportRect = developmentTreeViewport.rect;
        Rect contentRect = developmentTreeRoot.rect;
        float horizontalRange = contentRect.width - viewportRect.width;
        if (horizontalRange > 1f)
        {
            developmentTreeScrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                developmentTreeScrollRect.horizontalNormalizedPosition - pointerDelta.x / horizontalRange);
        }

        float verticalRange = contentRect.height - viewportRect.height;
        if (verticalRange > 1f)
        {
            developmentTreeScrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                developmentTreeScrollRect.verticalNormalizedPosition - pointerDelta.y / verticalRange);
        }
    }

    private void RegisterDevelopmentTile(string shipId, Image image, Outline outline, Color normalColor, Color selectedColor, Color normalOutlineColor)
    {
        if (string.IsNullOrWhiteSpace(shipId) || image == null || outline == null)
        {
            return;
        }

        developmentShipTilesById[shipId] = new DevelopmentShipTileView
        {
            shipId = shipId,
            image = image,
            outline = outline,
            normalColor = normalColor,
            selectedColor = selectedColor,
            normalOutlineColor = normalOutlineColor
        };
    }

    private void AddDevelopmentTilePointerEvents(RectTransform tile, string shipId)
    {
        if (tile == null || string.IsNullOrWhiteSpace(shipId))
        {
            return;
        }

        EventTrigger trigger = tile.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = tile.gameObject.AddComponent<EventTrigger>();
        }

        AddTrigger(trigger, EventTriggerType.PointerEnter, () => SetDevelopmentHoveredShip(shipId));
        AddTrigger(trigger, EventTriggerType.PointerExit, () => SetDevelopmentHoveredShip(""));
    }

    private void SetDevelopmentHoveredShip(string shipId)
    {
        string normalized = shipId ?? "";
        if (string.Equals(hoveredDevelopmentShipId, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        hoveredDevelopmentShipId = normalized;
        RefreshDevelopmentTileVisuals();
    }

    private void RefreshDevelopmentTileVisuals()
    {
        foreach (KeyValuePair<string, DevelopmentShipTileView> pair in developmentShipTilesById)
        {
            DevelopmentShipTileView tile = pair.Value;
            if (tile == null || tile.image == null || tile.outline == null)
            {
                continue;
            }

            bool selected = string.Equals(tile.shipId, selectedDevelopmentShipId, StringComparison.OrdinalIgnoreCase);
            bool hovered = string.Equals(tile.shipId, hoveredDevelopmentShipId, StringComparison.OrdinalIgnoreCase);
            if (selected)
            {
                tile.image.color = tile.selectedColor;
                tile.outline.effectColor = Color.white;
                tile.outline.effectDistance = new Vector2(2.5f, -2.5f);
                continue;
            }

            if (hovered)
            {
                Color hoverColor = new Color(
                    Mathf.Clamp01(tile.normalColor.r + tile.normalOutlineColor.r * 0.28f + 0.04f),
                    Mathf.Clamp01(tile.normalColor.g + tile.normalOutlineColor.g * 0.28f + 0.04f),
                    Mathf.Clamp01(tile.normalColor.b + tile.normalOutlineColor.b * 0.28f + 0.04f),
                    1f);
                tile.image.color = hoverColor;
                tile.outline.effectColor = new Color(1f, 0.86f, 0.45f, 0.98f);
                tile.outline.effectDistance = new Vector2(2f, -2f);
                continue;
            }

            tile.image.color = tile.normalColor;
            tile.outline.effectColor = tile.normalOutlineColor;
            tile.outline.effectDistance = new Vector2(1f, -1f);
        }
    }

    private void DrawDevelopmentConnection(Vector2 parentCenter, Vector2 childCenter, Color supplierColor)
    {
        float midY = Mathf.Lerp(parentCenter.y, childCenter.y, 0.5f);
        Color color = new Color(supplierColor.r, supplierColor.g, supplierColor.b, 0.54f);
        DrawDevelopmentLine(parentCenter, new Vector2(parentCenter.x, midY), color);
        DrawDevelopmentLine(new Vector2(parentCenter.x, midY), new Vector2(childCenter.x, midY), color);
        DrawDevelopmentLine(new Vector2(childCenter.x, midY), childCenter, color);
    }

    private void DrawDevelopmentLine(Vector2 start, Vector2 end, Color color)
    {
        const float thickness = 3f;
        if (Mathf.Abs(start.x - end.x) >= Mathf.Abs(start.y - end.y))
        {
            float minX = Mathf.Min(start.x, end.x);
            float width = Mathf.Max(thickness, Mathf.Abs(start.x - end.x));
            CreateDecorativePanel("Development Connection H", developmentTreeRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(minX, start.y + thickness * 0.5f),
                sizeDelta = new Vector2(width, thickness)
            }, color);
            return;
        }

        float topY = Mathf.Max(start.y, end.y);
        float height = Mathf.Max(thickness, Mathf.Abs(start.y - end.y));
        CreateDecorativePanel("Development Connection V", developmentTreeRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(start.x - thickness * 0.5f, topY),
            sizeDelta = new Vector2(thickness, height)
        }, color);
    }

    private static Vector2 GetDevelopmentTileCenter(Vector2 topLeft)
    {
        return new Vector2(topLeft.x + DevelopmentTileWidth * 0.5f, topLeft.y - DevelopmentTileHeight * 0.5f);
    }

    private static float GetDevelopmentRankY(float rankOneY, int rank)
    {
        return rankOneY - (Mathf.Clamp(rank, 1, DevelopmentTreeTierCount) - 1) * DevelopmentRankStep;
    }

    private bool SelectDevelopmentSupplier(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return false;
        }

        selectedDevelopmentSupplierId = factionId;
        selectedDevelopmentShipId = "";
        hoveredDevelopmentShipId = "";
        developmentWindowLayoutKey = "";
        RefreshState(true);
        return true;
    }

    private bool SelectDevelopmentShip(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
        {
            return false;
        }

        selectedDevelopmentShipId = shipId;
        hoveredDevelopmentShipId = "";
        RefreshDevelopmentTileVisuals();
        RefreshState(true);
        return true;
    }

    private bool HandleDevelopmentBuyShip(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId))
        {
            statusMessage = "Корабль не выбран.";
            RefreshState(true);
            return false;
        }

        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            statusMessage = "Мета-состояние не найдено.";
            RefreshState(true);
            return false;
        }

        bool success = currentMeta.TryBuyDevelopmentShipToDock(shipId, out statusMessage);
        if (success)
        {
            if (hudWindows.TryGetValue(WindowDevelopmentId, out HudWindow development) && development != null)
            {
                development.SetOpen(false);
            }

            metaScreenMode = MetaScreenMode.Dock;
        }

        RefreshState(true);
        return success;
    }

    private DevelopmentSupplierView FindDevelopmentSupplier(List<DevelopmentSupplierView> suppliers, string factionId)
    {
        if (suppliers == null || string.IsNullOrWhiteSpace(factionId))
        {
            return null;
        }

        for (int i = 0; i < suppliers.Count; i++)
        {
            if (suppliers[i] != null && string.Equals(suppliers[i].factionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                return suppliers[i];
            }
        }

        return null;
    }

    private void EnsureDevelopmentSelectedShip(DevelopmentSupplierView supplier)
    {
        if (supplier == null)
        {
            selectedDevelopmentShipId = "";
            return;
        }

        if (FindDevelopmentShip(supplier, selectedDevelopmentShipId) != null)
        {
            return;
        }

        ShipTreeEntryConfig firstShip = FindFirstDevelopmentShip(supplier);
        selectedDevelopmentShipId = firstShip != null ? firstShip.shipId : "";
    }

    private ShipTreeEntryConfig FindDevelopmentShip(DevelopmentSupplierView supplier, string shipId)
    {
        if (supplier == null || string.IsNullOrWhiteSpace(shipId))
        {
            return null;
        }

        if (supplier.starterShip != null && string.Equals(supplier.starterShip.shipId, shipId, StringComparison.OrdinalIgnoreCase))
        {
            return supplier.starterShip;
        }

        for (int i = 0; i < supplier.ships.Count; i++)
        {
            ShipTreeEntryConfig ship = supplier.ships[i];
            if (ship != null && string.Equals(ship.shipId, shipId, StringComparison.OrdinalIgnoreCase))
            {
                return ship;
            }
        }

        return null;
    }

    private ShipTreeEntryConfig FindFirstDevelopmentShip(DevelopmentSupplierView supplier)
    {
        if (supplier != null && supplier.starterShip != null)
        {
            return supplier.starterShip;
        }

        if (supplier == null || supplier.ships.Count == 0)
        {
            return null;
        }

        ShipTreeEntryConfig best = null;
        for (int i = 0; i < supplier.ships.Count; i++)
        {
            ShipTreeEntryConfig ship = supplier.ships[i];
            if (ship == null)
            {
                continue;
            }

            if (best == null || CompareDevelopmentShips(ship, best) < 0)
            {
                best = ship;
            }
        }

        return best;
    }

    private void BuildDevelopmentShipDetails(DevelopmentSupplierView supplier)
    {
        if (developmentDetailsRoot == null)
        {
            return;
        }

        ClearChildren(developmentDetailsRoot);
        ShipTreeEntryConfig ship = FindDevelopmentShip(supplier, selectedDevelopmentShipId) ?? FindFirstDevelopmentShip(supplier);
        developmentDetailsShipId = ship != null ? ship.shipId : "";
        if (ship == null)
        {
            CreateText(developmentDetailsRoot, "Корабль не выбран.", 16, new Vector2(18f, -18f), new Vector2(300f, 34f), TextAnchor.MiddleLeft, new Color(0.86f, 0.84f, 0.76f, 1f));
            return;
        }

        Color color = ship.visualColor;
        CreatePanel("Development Details Accent", developmentDetailsRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(0f, 0f),
            sizeDelta = new Vector2(DevelopmentDetailsWidth, 4f)
        }, new Color(color.r, color.g, color.b, 0.85f));

        Text title = CreateText(developmentDetailsRoot,
            FormatRomanTier(Mathf.Clamp(ship.treeTier, 1, DevelopmentTreeTierCount)) + " " + ShortenButtonLabel(ship.DisplayNameRu, 22).ToUpperInvariant(),
            22,
            new Vector2(18f, -18f),
            new Vector2(300f, 32f),
            TextAnchor.MiddleLeft,
            new Color(0.95f, 0.90f, 0.78f, 1f));
        title.fontStyle = FontStyle.Bold;

        CreateText(developmentDetailsRoot,
            GetDevelopmentRoleText(ship),
            15,
            new Vector2(20f, -54f),
            new Vector2(294f, 24f),
            TextAnchor.MiddleLeft,
            new Color(0.70f, 0.78f, 0.78f, 0.95f));

        float y = -96f;
        y = AddDevelopmentStatRow(developmentDetailsRoot, "Живучесть", GetDevelopmentDefenseRating(ship), y, color);
        y = AddDevelopmentStatRow(developmentDetailsRoot, "Мобильность", GetDevelopmentMobilityRating(ship), y, color);
        y = AddDevelopmentStatRow(developmentDetailsRoot, "Маскировка", GetDevelopmentStealthRating(ship), y, color);
        y = AddDevelopmentCargoCapacityRow(developmentDetailsRoot, ship, y);

        y -= 12f;
        CreatePanel("Development Details Divider", developmentDetailsRoot, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(18f, y),
            sizeDelta = new Vector2(298f, 1.5f)
        }, new Color(0.26f, 0.34f, 0.36f, 0.76f));
        y -= 24f;

        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Вооружение", GetDevelopmentWarfareRating(ship), y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Добыча руды", ship.miningRating, y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Сбор облаков", ship.harvestingRating, y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Охота", ship.huntingRating, y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Взлом", ship.hackingRating, y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Сальваж", ship.salvageRating, y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Сканирование", ship.surveyRating, y, color);
        y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Ремонт", ship.repairRating, y, color);

        if (!HasExplicitActivityRatings(ship))
        {
            y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Груз", ship.cargo, y, color);
            y = AddDevelopmentActivityStatRow(developmentDetailsRoot, "Утилитарность", ship.utility, y, color);
        }

        y = Mathf.Min(y - 18f, -520f);
        CreateText(developmentDetailsRoot, "Исследование:", 16, new Vector2(20f, y), new Vector2(150f, 24f), TextAnchor.MiddleLeft, new Color(0.82f, 0.86f, 0.82f, 1f));
        CreateText(developmentDetailsRoot, FormatMetaResourceAmount(GetDevelopmentResearchCost(ship)) + " опыта", 16, new Vector2(164f, y), new Vector2(150f, 24f), TextAnchor.MiddleRight, new Color(0.95f, 0.90f, 0.74f, 1f));
        y -= 32f;
        CreateText(developmentDetailsRoot, "Покупка:", 16, new Vector2(20f, y), new Vector2(150f, 24f), TextAnchor.MiddleLeft, new Color(0.82f, 0.86f, 0.82f, 1f));
        CreateText(developmentDetailsRoot, FormatMetaResourceAmount(ship.costAmount) + " фрахтов", 16, new Vector2(164f, y), new Vector2(150f, 24f), TextAnchor.MiddleRight, new Color(0.95f, 0.90f, 0.74f, 1f));

        y -= 42f;
        Text buyText = CreateButton(developmentDetailsRoot, "Development Buy Ship To Dock", new Vector2(20f, y), new Vector2(296f, 38f), () => HandleDevelopmentBuyShip(ship.shipId), out _);
        SetText(buyText, "КУПИТЬ В ДОК");
        buyText.fontStyle = FontStyle.Bold;

        string lore = ship.LoreDisplayRu;
        if (!string.IsNullOrWhiteSpace(lore))
        {
            y -= 42f;
            CreatePanel("Development Lore Divider", developmentDetailsRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(18f, y + 10f),
                sizeDelta = new Vector2(298f, 1.5f)
            }, new Color(0.26f, 0.34f, 0.36f, 0.76f));

            Text loreText = CreateText(developmentDetailsRoot, lore, 14, new Vector2(20f, y), new Vector2(296f, 220f), TextAnchor.UpperLeft, new Color(0.78f, 0.82f, 0.76f, 0.96f));
            loreText.lineSpacing = 1.05f;
        }
    }

    private float AddDevelopmentActivityStatRow(RectTransform parent, string label, int value, float y, Color color)
    {
        if (value <= 0)
        {
            return y;
        }

        return AddDevelopmentStatRow(parent, label, value, y, color);
    }

    private float AddDevelopmentCargoCapacityRow(RectTransform parent, ShipTreeEntryConfig ship, float y)
    {
        if (ship == null || ship.cargoCapacityTons <= 0f)
        {
            return y;
        }

        CreateText(parent, "Грузоподъёмность", 14, new Vector2(20f, y), new Vector2(176f, 22f), TextAnchor.MiddleLeft, new Color(0.88f, 0.88f, 0.82f, 1f));
        Text valueText = CreateText(parent, FormatCargoCapacityTons(ship.cargoCapacityTons), 16, new Vector2(204f, y), new Vector2(112f, 22f), TextAnchor.MiddleRight, new Color(0.94f, 0.90f, 0.78f, 1f));
        valueText.fontStyle = FontStyle.Bold;
        return y - 30f;
    }

    private float AddDevelopmentStatRow(RectTransform parent, string label, int value, float y, Color color)
    {
        int clamped = Mathf.Clamp(value, 0, 100);
        CreateText(parent, label, 15, new Vector2(20f, y), new Vector2(122f, 22f), TextAnchor.MiddleLeft, new Color(0.88f, 0.88f, 0.82f, 1f));
        RectTransform bar = CreatePanel("Development Stat Bar " + label, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(142f, y - 7f),
            sizeDelta = new Vector2(122f, 8f)
        }, new Color(0.060f, 0.076f, 0.082f, 0.96f));

        CreatePanel("Development Stat Fill " + label, bar, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = Vector2.zero,
            sizeDelta = new Vector2(122f * (clamped / 100f), 8f)
        }, new Color(Mathf.Clamp01(color.r * 0.70f + 0.10f), Mathf.Clamp01(color.g * 0.70f + 0.18f), Mathf.Clamp01(color.b * 0.70f + 0.22f), 0.96f));

        Text valueText = CreateText(parent, clamped.ToString(CultureInfo.InvariantCulture), 16, new Vector2(270f, y), new Vector2(46f, 22f), TextAnchor.MiddleRight, new Color(0.94f, 0.90f, 0.78f, 1f));
        valueText.fontStyle = FontStyle.Bold;
        return y - 30f;
    }

    private static string GetDevelopmentFactionDisplayName(string factionId, string fallback)
    {
        switch ((factionId ?? "").Trim().ToLowerInvariant())
        {
            case "capital":
            case "imperial":
            case "empire":
                return "Империя";
            case "wind_houses":
            case "windhouse":
                return "Ветровые Дома";
            case "mist_synod":
            case "gas_synod":
                return "Туманный Синод";
            case "stone_vault":
                return "Каменный Свод";
            case "factory_ark":
            case "clockwork_ark":
                return "Заводные Ковчеги";
            case "devourers":
            case "devourer":
                return "Пожиратели";
            default:
                return string.IsNullOrWhiteSpace(fallback) ? factionId : fallback;
        }
    }

    private static int GetDevelopmentFactionOrder(string factionId)
    {
        switch ((factionId ?? "").Trim().ToLowerInvariant())
        {
            case "capital":
            case "imperial":
            case "empire":
                return 0;
            case "wind_houses":
            case "windhouse":
                return 1;
            case "mist_synod":
            case "gas_synod":
                return 2;
            case "stone_vault":
                return 3;
            case "factory_ark":
            case "clockwork_ark":
                return 4;
            case "devourers":
            case "devourer":
                return 5;
            default:
                return 100;
        }
    }

    private static string GetDevelopmentRoleText(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return "";
        }

        string shipClass = GetDevelopmentClassName(ship.shipClassId, ship.ClassDisplayNameRu);
        string role = GetDevelopmentRoleName(ship.roleId, ship.roleNameRu);
        return string.IsNullOrWhiteSpace(role) ? shipClass : shipClass + " · " + role;
    }

    private static string GetDevelopmentClassName(string classId, string fallback)
    {
        switch ((classId ?? "").Trim().ToLowerInvariant())
        {
            case "prototype":
                return "Стартовый корабль";
            case "destroyer":
            case "frigate":
                return "Фрегат";
            case "cruiser":
                return "Крейсер";
            case "battleship":
                return "Линкор";
            default:
                return string.IsNullOrWhiteSpace(fallback) ? classId : fallback;
        }
    }

    private static string GetDevelopmentRoleName(string roleId, string fallback)
    {
        switch ((roleId ?? "").Trim().ToLowerInvariant())
        {
            case "starter_universal": return "стартовый";
            case "screen": return "экран";
            case "raider": return "рейдер";
            case "escort": return "эскорт";
            case "interceptor": return "перехват";
            case "patrol": return "патруль";
            case "scout": return "разведка";
            case "logistics": return "логистика";
            case "cargo": return "грузовой";
            case "artillery": return "артиллерия";
            case "assault": return "штурм";
            case "industrial": return "промышленный";
            case "expedition": return "экспедиционный";
            case "line": return "линейный";
            case "platform": return "платформа";
            case "starter": return "стартовый";
            case "combat": return "боевой";
            case "recon": return "разведка";
            case "light_recon": return "легкая разведка";
            case "light_research": return "исследователь";
            case "light_interceptor": return "легкий перехват";
            case "torpedo_recon": return "торпедная разведка";
            case "repair": return "ремонт";
            case "harpoon": return "гарпунный";
            case "brawler": return "ближний бой";
            case "ore_hauler": return "рудный грузовой";
            case "bomb": return "бомбовый";
            case "ripper": return "разрыватель";
            case "leviathan_hunter": return "охота на левиафанов";
            case "leviathan_processor": return "разделка левиафанов";
            case "ore_tug": return "рудный буксир";
            case "single_gun_line": return "линейная пушка";
            case "armored_gun": return "бронированный артиллерист";
            case "magnet_hauler": return "магнитный сборщик";
            case "refinery": return "рудная переработка";
            case "heavy_gun": return "тяжелая артиллерия";
            case "ore_foundry": return "рудный завод";
            case "siphon_scout": return "газовый сборщик";
            case "dome_recon": return "купольная разведка";
            case "gunline": return "пушечная линия";
            case "toxic_cloud": return "ядовитый туман";
            case "chemical": return "химический";
            case "heavy_egg": return "тяжелое яйцо";
            case "gas_combine": return "газовый комбинат";
            case "x_ray_scout": return "X-Ray разведка";
            case "automaton_tender": return "тендер автоматонов";
            case "modular_survey": return "модульное исследование";
            case "combat_ark": return "боевой ковчег";
            case "mechanized_citadel": return "механизированная цитадель";
            case "automaton_factory": return "фабрика автоматонов";
            case "bomb_artillery": return "бомбовая артиллерия";
            case "long_range_heavy": return "дальнобойный тяжелый";
            case "heavy": return "тяжелый";
            default:
                return string.IsNullOrWhiteSpace(fallback) ? "" : fallback;
        }
    }

    private static int GetDevelopmentDefenseRating(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return 0;
        }

        if (ship.defenseRating >= 0)
        {
            return Mathf.Clamp(ship.defenseRating, 0, 100);
        }

        return Mathf.Clamp(Mathf.RoundToInt((ship.armor + ship.durability) * 0.5f), 0, 100);
    }

    private static int GetDevelopmentMobilityRating(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return 0;
        }

        if (ship.mobilityRating >= 0)
        {
            return Mathf.Clamp(ship.mobilityRating, 0, 100);
        }

        return Mathf.Clamp(Mathf.RoundToInt((ship.speed + ship.maneuverability) * 0.5f), 0, 100);
    }

    private static int GetDevelopmentStealthRating(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return 0;
        }

        if (ship.stealthRating >= 0)
        {
            return Mathf.Clamp(ship.stealthRating, 0, 100);
        }

        return Mathf.Clamp(Mathf.RoundToInt((ship.speed + ship.maneuverability + ship.utility) / 3f), 0, 100);
    }

    private static int GetDevelopmentWarfareRating(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return 0;
        }

        return Mathf.Clamp(ship.warfareRating >= 0 ? ship.warfareRating : ship.firepower, 0, 100);
    }

    private static string FormatCargoCapacityTons(float tons)
    {
        if (tons >= 100f)
        {
            return tons.ToString("0", CultureInfo.InvariantCulture) + " т";
        }

        return tons.ToString(tons >= 10f ? "0.#" : "0.##", CultureInfo.InvariantCulture) + " т";
    }

    private static bool HasExplicitActivityRatings(ShipTreeEntryConfig ship)
    {
        return ship != null
            && (ship.warfareRating >= 0
                || ship.miningRating >= 0
                || ship.harvestingRating >= 0
                || ship.huntingRating >= 0
                || ship.hackingRating >= 0
                || ship.salvageRating >= 0
                || ship.surveyRating >= 0
                || ship.repairRating >= 0);
    }

    private static int GetDevelopmentResearchCost(ShipTreeEntryConfig ship)
    {
        if (ship == null)
        {
            return 0;
        }

        if (ship.researchCostAmount > 0)
        {
            return ship.researchCostAmount;
        }

        return Mathf.Max(0, Mathf.RoundToInt(ship.costAmount * 1.25f));
    }

    private static bool IsDevelopmentStarterEntry(ShipTreeEntryConfig entry)
    {
        return entry != null
            && entry.IsDevelopmentRosterShip
            && (entry.treeTier <= 1 || string.Equals(entry.branchId, "starter", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDevelopmentWindowLayoutKey(List<DevelopmentSupplierView> suppliers, string selectedSupplierId)
    {
        int supplierCount = suppliers != null ? suppliers.Count : 0;
        int shipCount = 0;
        if (suppliers != null)
        {
            for (int i = 0; i < suppliers.Count; i++)
            {
                if (suppliers[i] == null)
                {
                    continue;
                }

                shipCount += suppliers[i].ships.Count + (suppliers[i].starterShip != null ? 1 : 0);
            }
        }

        return (selectedSupplierId ?? "") + "|" + supplierCount.ToString(CultureInfo.InvariantCulture) + "|" + shipCount.ToString(CultureInfo.InvariantCulture);
    }

    private static int CountDevelopmentSupplierShips(List<DevelopmentSupplierView> suppliers)
    {
        int count = 0;
        if (suppliers == null)
        {
            return count;
        }

        for (int i = 0; i < suppliers.Count; i++)
        {
            if (suppliers[i] == null)
            {
                continue;
            }

            count += suppliers[i].ships.Count + (suppliers[i].starterShip != null ? 1 : 0);
        }

        return count;
    }

    private static string FormatRomanTier(int tier)
    {
        switch (Mathf.Clamp(tier, 1, 10))
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            default: return "X";
        }
    }

    private static void TintDevelopmentSupplierButton(Button button, bool selected, Color supplierColor)
    {
        if (button == null)
        {
            return;
        }

        Color normal = selected
            ? new Color(Mathf.Clamp01(supplierColor.r * 0.55f + 0.18f), Mathf.Clamp01(supplierColor.g * 0.55f + 0.14f), Mathf.Clamp01(supplierColor.b * 0.55f + 0.10f), 0.98f)
            : new Color(0.10f, 0.095f, 0.085f, 0.94f);
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = selected
            ? new Color(Mathf.Clamp01(normal.r + 0.10f), Mathf.Clamp01(normal.g + 0.10f), Mathf.Clamp01(normal.b + 0.10f), 1f)
            : new Color(0.20f, 0.16f, 0.10f, 0.98f);
        colors.pressedColor = new Color(0.46f, 0.32f, 0.14f, 1f);
        button.colors = colors;
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private float AddResourceCategoryHeader(string title, float y)
    {
        Text header = CreateText(resourceCatalogContent, string.IsNullOrWhiteSpace(title) ? "Resources" : title, 14, new Vector2(12f, y), new Vector2(600f, 24f), TextAnchor.MiddleLeft, new Color(0.96f, 0.78f, 0.42f, 1f));
        header.fontStyle = FontStyle.Bold;
        return y - 30f;
    }

    private float AddResourceCatalogRow(ItemConfig item, string categoryId, float y)
    {
        RectTransform row = CreatePanel("Resource " + item.id, resourceCatalogContent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, y),
            sizeDelta = new Vector2(-18f, 54f)
        }, GetResourceRowColor(resourceCatalogRows.Count));

        Image icon = CreateResourceIcon(row, item.id);
        string displayName = string.IsNullOrWhiteSpace(item.localNameRu) ? item.id : item.localNameRu;
        Text name = CreateText(row, displayName, 14, new Vector2(58f, -5f), new Vector2(420f, 22f), TextAnchor.MiddleLeft, new Color(0.88f, 0.91f, 0.84f, 1f));
        name.fontStyle = FontStyle.Bold;
        CreateText(row,
            item.id + " | " + categoryId + " | " + FormatResourceUnitMass(item.massKgPerUnit),
            11,
            new Vector2(58f, -29f),
            new Vector2(520f, 18f),
            TextAnchor.MiddleLeft,
            new Color(0.58f, 0.66f, 0.68f, 1f));

        resourceCatalogRows.Add(new ResourceCatalogRow
        {
            itemId = item.id,
            icon = icon
        });
        return y - 60f;
    }

    private Image CreateResourceIcon(RectTransform parent, string itemId)
    {
        GameObject iconObject = CreateRect("Icon " + itemId, parent, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(12f, -7f),
            sizeDelta = new Vector2(40f, 40f)
        });

        Image image = iconObject.AddComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.sprite = WildWindResourceIconCatalog.LoadSprite(itemId);
        if (image.sprite == null)
        {
            image.color = new Color(0.18f, 0.20f, 0.22f, 1f);
        }

        return image;
    }

    private int CountExistingResourceIconAssets(SessionConfigDatabase config)
    {
        if (config == null || config.items == null) return 0;

        int count = 0;
        for (int i = 0; i < config.items.Count; i++)
        {
            ItemConfig item = config.items[i];
            if (item != null && WildWindResourceIconCatalog.HasIconTexture(item.id))
            {
                count++;
            }
        }

        return count;
    }

    private int CountResourceRowsWithIcons()
    {
        int count = 0;
        for (int i = 0; i < resourceCatalogRows.Count; i++)
        {
            ResourceCatalogRow row = resourceCatalogRows[i];
            if (row != null && row.icon != null && row.icon.sprite != null)
            {
                count++;
            }
        }

        return count;
    }

    private void RefreshHudWindows(
        MetaGameState currentMeta,
        WildWindGameplaySession currentSession,
        PlayerProgress progress,
        WildWindFlightControlBridge controls,
        SortieReturnEstimate sortieEstimate,
        bool inFlight)
    {
        if (!inFlight || currentMeta == null || progress == null)
        {
            return;
        }

        ShipPhysics ship = GetHudShip();
        SessionConfigDatabase config = currentMeta.SessionConfig;
        Vector3 position = GetPlayerPosition(currentSession);
        SortieSessionState sortie = currentMeta.HasActiveSortie ? currentMeta.ActiveSortie : null;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;
        float stormFloorY = zone != null ? zone.stormFloorY : 0f;
        float altitudeAboveStorm = position.y - stormFloorY;
        float speedMS = GetShipSpeed(currentMeta);
        float cargoMassKg = progress.GetShipCargoMassKg(config);
        float payloadMassKg = progress.GetShipPayloadMassKg(config);
        float freePayloadKg = currentMeta.GetRemainingShipCargoCapacityKg();
        float maxPayloadKg = Mathf.Max(payloadMassKg + freePayloadKg, payloadMassKg, 1f);
        float payloadPercent = Mathf.Clamp01(payloadMassKg / maxPayloadKg) * 100f;
        DamageableShip damage = ship != null ? ship.GetComponentInParent<DamageableShip>() : null;
        float structurePercent = damage != null ? damage.StructureRatio * 100f : 100f;
        string resourceId = zone != null ? zone.starterResourceItemId : "";
        int oreTaskCurrent = GetAnyOreCargoKg(progress);
        int oreTaskTarget = 200;

        SetText(locationWindowText,
            "Коорд. от базы: X " + FormatSignedMeters(position.x)
            + "  Y " + FormatSignedMeters(position.y)
            + "  Z " + FormatSignedMeters(position.z)
            + "\nВысота над бурей: " + altitudeAboveStorm.ToString("0") + " м"
            + "\nДо края зоны: " + (sortieEstimate.hasActiveSortie ? FormatBoundaryDistance(sortieEstimate.distanceToBoundaryMeters) : "-")
            + "\nЗона: " + (zone != null ? zone.displayName : "-"));

        SetText(returnWindowText,
            "Return: no coal/claudium cost"
            + "\nTo edge: " + (sortieEstimate.hasActiveSortie ? FormatBoundaryDistance(sortieEstimate.distanceToBoundaryMeters) : "-")
            + "\nTo base: " + sortieEstimate.distanceToBaseKm.ToString("0") + " km | ETA " + FormatHudDuration(sortieEstimate.returnTimeSeconds)
            + "\nExit: " + (sortieEstimate.canExtract ? "ready" : FormatExtractionBlocker(sortieEstimate))
            + "\nSlip: " + sortieEstimate.extractionRunupSeconds.ToString("0.0") + "/" + sortieEstimate.requiredExtractionRunupSeconds.ToString("0.0") + " s");
        if (returnWindowText != null)
        {
            returnWindowText.color = new Color(0.90f, 0.86f, 0.70f, 1f);
        }

        SetText(shipWindowText,
            "Прочность: " + structurePercent.ToString("0") + "%  " + MakeAsciiBar(structurePercent / 100f, 18)
            + "\nПолезная нагрузка: " + payloadMassKg.ToString("0") + " / " + maxPayloadKg.ToString("0") + " кг  " + payloadPercent.ToString("0") + "%"
            + "\n" + MakeAsciiBar(payloadPercent / 100f, 22)
            + "\nСвободно: " + freePayloadKg.ToString("0") + " кг"
            + "\nГруз в трюме: " + cargoMassKg.ToString("0") + " кг");

        SetText(tasksWindowText,
            FormatTaskLine("Набрать любую руду", oreTaskCurrent, oreTaskTarget)
            + "\n" + FormatTaskLine("Вернуться домой", sortieEstimate.canExtract ? 1 : 0, 1)
            + "\n" + FormatTaskLine("Покинуть бурю", altitudeAboveStorm > 0f ? 1 : 0, 1));

        SetText(cargoHeaderText, payloadMassKg.ToString("0") + " / " + maxPayloadKg.ToString("0") + " кг  " + payloadPercent.ToString("0") + "%  " + MakeAsciiBar(payloadPercent / 100f, 18));
        RefreshCargoSlots(progress, config);

        RefreshRadarWindow(position);
        RefreshModulesWindow(ship, controls, resourceId, progress);
    }

    private void RefreshCargoSlots(PlayerProgress progress, SessionConfigDatabase config)
    {
        if (cargoSlotTexts == null) return;

        for (int i = 0; i < cargoSlotTexts.Length; i++)
        {
            SetText(cargoSlotTexts[i], "");
        }

        if (progress == null || progress.shipCargo == null) return;

        int slot = 0;
        for (int i = 0; i < progress.shipCargo.Count && slot < cargoSlotTexts.Length; i++)
        {
            ResourceStack stack = progress.shipCargo[i];
            if (stack == null || stack.amount <= 0) continue;
            SetText(cargoSlotTexts[slot], ShortenButtonLabel(GetItemDisplayName(config, stack.resourceId), 18) + "\n" + stack.amount + " кг");
            slot++;
        }
    }

    private void RefreshRadarWindow(Vector3 playerPosition)
    {
        SetText(radarBoulderFilterText, (radarShowBoulders ? "[x] " : "[ ] ") + "Глыбы");
        SetText(radarDebrisFilterText, (radarShowDebris ? "[x] " : "[ ] ") + "Обломки");

        radarEntries.Clear();
        if (radarShowBoulders)
        {
            GameObject rootBoulders = GameObject.Find("Safe Ore Sortie Boulders");
            if (rootBoulders != null)
            {
                for (int i = 0; i < rootBoulders.transform.childCount; i++)
                {
                    Transform child = rootBoulders.transform.GetChild(i);
                    AddRadarEntry(child.name, "глыба", RadarEntryKind.Boulder, child, playerPosition);
                }
            }
        }

        if (radarShowDebris)
        {
            MiningFragment.GetActiveFragments(radarFragments);
            for (int i = 0; i < radarFragments.Count; i++)
            {
                MiningFragment fragment = radarFragments[i];
                if (fragment == null) continue;
                AddRadarEntry(fragment.oreItemId + " " + fragment.amountKg + "кг", "обломок", RadarEntryKind.Debris, fragment.transform, playerPosition);
            }
        }

        radarEntries.Sort((a, b) => a.distanceMeters.CompareTo(b.distanceMeters));
        RadarTacticalSessionOverlay overlay = EnsureRadarSessionOverlay();
        if (overlay != null)
        {
            ShipPhysics ship = GetHudShip();
            Transform shipTransform = ship != null ? ship.transform : null;
            overlay.SetEntries(shipTransform, playerPosition, shipTransform != null ? shipTransform.forward : Vector3.forward, radarEntries, IsHudWindowOpen(WindowRadarId));
        }

        string text = "Название                         Тип        Дистанция";
        int rows = Mathf.Min(9, radarEntries.Count);
        for (int i = 0; i < rows; i++)
        {
            RadarEntry entry = radarEntries[i];
            text += "\n" + ShortenButtonLabel(entry.name, 28).PadRight(29)
                + ShortenButtonLabel(entry.kind, 10).PadRight(11)
                + FormatBoundaryMagnitude(entry.distanceMeters);
        }

        if (rows == 0)
        {
            text += "\n-";
        }

        SetText(radarWindowText, text);
    }

    private void AddRadarEntry(string name, string kind, RadarEntryKind entryKind, Transform target, Vector3 playerPosition)
    {
        Vector3 position = target != null ? target.position : playerPosition;
        Vector3 delta = position - playerPosition;
        Vector2 mapOffsetMeters = CalculateRadarMapOffset(delta);
        radarEntries.Add(new RadarEntry
        {
            name = string.IsNullOrWhiteSpace(name) ? "-" : name,
            kind = string.IsNullOrWhiteSpace(kind) ? "-" : kind,
            entryKind = entryKind,
            target = target,
            position = position,
            mapOffsetMeters = mapOffsetMeters,
            distanceMeters = delta.magnitude,
            horizontalDistanceMeters = mapOffsetMeters.magnitude,
            verticalDeltaMeters = delta.y
        });
    }

    private Vector2 CalculateRadarMapOffset(Vector3 worldDelta)
    {
        ShipPhysics ship = GetHudShip();
        if (ship == null)
        {
            return new Vector2(worldDelta.x, worldDelta.z);
        }

        Vector3 forward = Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return new Vector2(Vector3.Dot(worldDelta, right), Vector3.Dot(worldDelta, forward));
    }

    private void RefreshModulesWindow(ShipPhysics ship, WildWindFlightControlBridge controls, string resourceId, PlayerProgress progress)
    {
        string activeBur = "CRUSHER: " + (ship != null ? "ready " + MakeAsciiBar(0.63f, 10) : "-");
        string activeWeapon = "WEAPONS: " + (progress != null ? progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId) + " units" : "-");
        string passive = "BUILT-IN: guns | crusher | sensors | hold";
        SetText(modulesWindowText, activeBur + "\n" + activeWeapon + "\n" + passive);
    }

    private void RefreshSortiePanel(
        MetaGameState currentMeta,
        WildWindGameplaySession currentSession,
        PlayerProgress progress,
        SortieReturnEstimate sortieEstimate,
        bool inFlight)
    {
        if (sortiePanel == null)
        {
            return;
        }

        bool visible = inFlight
            && currentMeta != null
            && progress != null
            && currentMeta.HasActiveSortie;
        sortiePanel.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        SortieSessionState sortie = currentMeta.ActiveSortie;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;
        SessionConfigDatabase config = currentMeta.SessionConfig;
        string zoneName = zone != null && !string.IsNullOrWhiteSpace(zone.displayName)
            ? zone.displayName
            : "Unknown sortie";
        string resourceId = zone != null ? zone.starterResourceItemId : "";
        string resourceName = ShortenButtonLabel(GetItemDisplayName(config, resourceId), 28);
        int resourceAmount = !string.IsNullOrWhiteSpace(resourceId)
            ? progress.GetShipCargoAmount(resourceId)
            : 0;
        float cargoMassKg = progress.GetShipCargoMassKg(config);
        float freeCargoKg = currentMeta.GetRemainingShipCargoCapacityKg();
        ShipPhysics ship = GetHudShip();
        float speedMS = GetShipSpeed(currentMeta);
        float altitudeMeters = GetPlayerPosition(currentSession).y;
        string slipText = FormatSlipstreamState(ship);
        string engineModeText = FormatEngineMode(ship);
        string statusDetail = !string.IsNullOrWhiteSpace(currentMeta.ActiveSortieExtractionRunupStatus)
            ? currentMeta.ActiveSortieExtractionRunupStatus
            : sortieEstimate.status;

        SetText(sortieTitleText, "SORTIE | " + zoneName);
        SetText(sortieZoneText, "Zone: radius "
            + (zone != null ? FormatBoundaryMagnitude(zone.radiusMeters) : "-")
            + " | storm floor "
            + (zone != null ? zone.stormFloorY.ToString("0") + " m" : "-")
            + " | base "
            + sortieEstimate.distanceToBaseKm.ToString("0")
            + " km");
        SetText(sortieNavigationText, "Nav: "
            + FormatSortieBoundaryState(sortieEstimate)
            + " | return ETA "
            + FormatHudDuration(sortieEstimate.returnTimeSeconds)
            + " | marker BASE SLIP");
        SetText(sortieExtractionText, "Exit: "
            + (sortieEstimate.canExtract ? "READY" : FormatExtractionBlocker(sortieEstimate))
            + " | slip "
            + sortieEstimate.extractionRunupSeconds.ToString("0.0")
            + "/"
            + sortieEstimate.requiredExtractionRunupSeconds.ToString("0.0")
            + "s");
        SetText(sortieReservesText, "Autonomy: built-in systems | no coal/claudium cost");
        SetText(sortieCargoText, "Cargo: "
            + resourceName
            + " "
            + resourceAmount
            + " | hold "
            + FormatKg(cargoMassKg)
            + " | free "
            + FormatKg(freeCargoKg));
        SetText(sortieShipText, "Ship: alt "
            + FormatBoundaryMagnitude(Mathf.Max(0f, altitudeMeters))
            + " | speed "
            + speedMS.ToString("0.0")
            + " m/s | "
            + slipText
            + " | "
            + engineModeText);
        SetText(sortieStatusDetailText, ShortenButtonLabel(statusDetail, 92));

        Color readyColor = new Color(0.54f, 0.92f, 0.58f, 1f);
        Color warningColor = new Color(0.95f, 0.75f, 0.42f, 1f);
        if (sortieExtractionText != null)
        {
            sortieExtractionText.color = sortieEstimate.canExtract ? readyColor : warningColor;
        }

        if (sortieReservesText != null)
        {
            sortieReservesText.color = new Color(0.84f, 0.90f, 0.76f, 1f);
        }

        if (sortieStatusDetailText != null)
        {
            sortieStatusDetailText.color = sortieEstimate.canExtract ? readyColor : warningColor;
        }
    }

    private void RefreshEnginePowerBar()
    {
        if (enginePowerBar == null || engineLiftPowerFill == null || engineModulePowerFill == null || engineThrustPowerFill == null)
        {
            return;
        }

        ShipPhysics ship = GetHudShip();
        if (ship == null || ship.HullThrustCapacityKgf <= 0f)
        {
            SetPowerFill(engineLiftPowerFill, 0f, 0f);
            SetPowerFill(engineModulePowerFill, 0f, 0f);
            SetPowerFill(engineThrustPowerFill, 0f, 0f);
            SetText(enginePowerText, "THR 0 / 0 kgf");
            return;
        }

        float maxThrustKgf = Mathf.Max(0.001f, ship.HullThrustCapacityKgf);
        float liftRatio = ship.claudiumMaxLiftKg > 0f
            ? Mathf.Clamp01((ship.claudiumCurrentLiftN / 9.81f) / ship.claudiumMaxLiftKg)
            : 0f;
        float thrustKgf = Mathf.Abs(ship.hullForwardThrustKgfCurrent);
        float thrustRatio = Mathf.Clamp01(thrustKgf / maxThrustKgf);
        float liftHeight = EnginePowerBarHeight * liftRatio;
        float moduleHeight = 0f;
        float thrustHeight = EnginePowerBarHeight * thrustRatio;

        SetPowerFill(engineLiftPowerFill, 0f, liftHeight);
        SetPowerFill(engineModulePowerFill, liftHeight, moduleHeight);
        SetPowerFillColor(engineThrustPowerFill,
            ship.hullThrustOutput < -0.001f
                ? EngineReverseThrustColor
                : EngineForwardThrustColor);
        SetPowerFill(engineThrustPowerFill, liftHeight + moduleHeight, thrustHeight);
        RefreshThrottleGearSelector();

        SetText(enginePowerText, "THR " + thrustKgf.ToString("0") + " / " + maxThrustKgf.ToString("0") + " kgf");
    }

    private void RefreshThrottleGearSelector()
    {
        if (engineThrottleGearMarks == null || engineThrottleGearTexts == null)
        {
            return;
        }

        WildWindFlightControlBridge controls = ResolveFlightControls();
        int selectedNotch = controls != null ? controls.ManualThrustNotch : 0;
        for (int i = 0; i < engineThrottleGearMarks.Length && i < engineThrottleGearTexts.Length; i++)
        {
            RectTransform mark = engineThrottleGearMarks[i];
            Text label = engineThrottleGearTexts[i];
            if (mark == null || label == null)
            {
                continue;
            }

            int notch = EngineThrottleGearNotches[i];
            bool selected = notch == selectedNotch;
            Image image = mark.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(0.96f, 0.72f, 0.20f, 0.98f)
                    : new Color(0.05f, 0.052f, 0.058f, 0.86f);
            }

            label.color = selected
                ? new Color(0.06f, 0.045f, 0.02f, 1f)
                : new Color(0.82f, 0.80f, 0.72f, 1f);
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private bool SelectThrottleGear(int notch)
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls != null)
        {
            controls.SetManualThrustNotchForHud(notch);
            statusMessage = controls.StatusMessage;
            RefreshState(true);
            return true;
        }

        ShipPhysics ship = GetHudShip();
        if (ship == null)
        {
            return false;
        }

        ship.thrustInput = GetThrottleForGearNotch(notch);
        statusMessage = "Manual thrust notch selected.";
        RefreshState(true);
        return true;
    }

    private static float GetGearNotchRatio(int notch)
    {
        return (GetThrottleForGearNotch(notch) + 1f) * 0.5f;
    }

    private static string FormatThrottleGearLabel(int notch)
    {
        if (notch > 0) return notch.ToString(CultureInfo.InvariantCulture);
        if (notch == 0) return "Stop";
        return "R" + Mathf.Abs(notch).ToString(CultureInfo.InvariantCulture);
    }

    private static float GetThrottleForGearNotch(int notch)
    {
        if (notch > 0)
        {
            return Mathf.Clamp01(notch * 0.2f);
        }

        return notch switch
        {
            -1 => -0.25f,
            -2 => -0.5f,
            -3 => -1f,
            _ => 0f
        };
    }

    private static float GetShipModulePowerKw(ShipPhysics ship)
    {
        if (ship == null)
        {
            return 0f;
        }

        return 0f;
    }

    private bool ToggleClaudiumSlipstream()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls != null)
        {
            controls.ToggleClaudiumSlipstream();
            statusMessage = controls.StatusMessage;
            RefreshClaudiumSlipstreamText();
            RefreshState(true);
            return true;
        }

        ShipPhysics ship = GetHudShip();
        if (ship == null)
        {
            return false;
        }

        if (!ship.TrySetClaudiumSlipstreamEnabled(!ship.claudiumSlipstreamEnabled, out statusMessage))
        {
            RefreshClaudiumSlipstreamText();
            RefreshState(true);
            return false;
        }

        RefreshClaudiumSlipstreamText();
        RefreshState(true);
        return true;
    }

    private bool ToggleRadarBoulders()
    {
        radarShowBoulders = !radarShowBoulders;
        RefreshState(true);
        return true;
    }

    private bool ToggleRadarDebris()
    {
        radarShowDebris = !radarShowDebris;
        RefreshState(true);
        return true;
    }

    private void RefreshClaudiumSlipstreamText()
    {
        ShipPhysics ship = GetHudShip();
        bool enabled = ship != null && ship.claudiumSlipstreamEnabled;
        string state = WildWindLocalization.Get(enabled ? "game.hud.on" : "game.hud.off");
        string charge = ship != null && ship.ClaudiumSlipstreamCharge01 > 0.001f
            ? " " + (ship.ClaudiumSlipstreamCharge01 * 100f).ToString("0") + "%"
            : "";
        SetText(claudiumSlipstreamText, "Скольжение: " + state + charge);
    }

    private static void SetPowerFill(RectTransform fill, float bottomOffset, float height)
    {
        if (fill == null)
        {
            return;
        }

        fill.anchoredPosition = new Vector2(0f, Mathf.Max(0f, bottomOffset));
        fill.sizeDelta = new Vector2(fill.sizeDelta.x, Mathf.Max(0f, height));
    }

    private static void SetPowerFillColor(RectTransform fill, Color color)
    {
        Image image = fill != null ? fill.GetComponent<Image>() : null;
        if (image != null)
        {
            image.color = color;
        }
    }

    private ShipPhysics GetHudShip()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls != null && controls.ControlledShip != null)
        {
            return controls.ControlledShip;
        }

        MetaGameState currentMeta = ResolveMeta();
        return currentMeta != null && currentMeta.shipLoader != null
            ? currentMeta.shipLoader.targetShip
            : null;
    }

    private void SetHeldFlightInput(Vector3 input)
    {
        Vector3 previousInput = heldFlightInput;
        heldFlightInput = input.sqrMagnitude > 1f ? input.normalized : input;
        if (heldFlightInput.sqrMagnitude <= 0.0001f)
        {
            WildWindFlightControlBridge controls = ResolveFlightControls();
            if (controls != null && IsPureLiftInput(previousInput))
            {
                controls.ClearMomentaryLiftInput();
            }
            else
            {
                controls?.ClearDirectInput();
            }

            ResolveSessionCameraController()?.ClearUiInput();
            return;
        }

        PushHeldFlightInput();
    }

    private void PushHeldFlightInput()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls != null)
        {
            if (heldFlightInput.sqrMagnitude > 0.0001f)
            {
                if (IsPureLiftInput(heldFlightInput))
                {
                    controls.SetMomentaryLiftInput(heldFlightInput.y);
                }
                else
                {
                    controls.SetDirectInput(heldFlightInput.z, heldFlightInput.y, heldFlightInput.x);
                }
            }
            return;
        }

        if (heldFlightInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        WildWindSessionCameraController controller = ResolveSessionCameraController();
        if (controller != null)
        {
            controller.SetUiInput(heldFlightInput);
        }
    }

    private static bool IsPureLiftInput(Vector3 input)
    {
        return Mathf.Abs(input.y) > 0.001f
            && Mathf.Abs(input.x) <= 0.001f
            && Mathf.Abs(input.z) <= 0.001f;
    }

    private void RefreshCompass()
    {
        if (compassPanel == null || compassTickTexts == null || compassTickLines == null)
        {
            return;
        }

        MetaGameState currentMeta = ResolveMeta();
        WildWindGameplaySession currentSession = ResolveSession();
        WildWindFlightControlBridge controls = ResolveFlightControls();
        bool visible = currentMeta != null && currentMeta.CurrentMode == GameSessionMode.Flight;
        compassPanel.gameObject.SetActive(visible);
        if (!visible)
        {
            SetCompassTargetMarkerVisible(false);
            return;
        }

        float heading = NormalizeHeading(GetViewHeading(currentSession));
        lastCompassHeadingDegrees = heading;
        float nearestTick = Mathf.Round(heading / CompassTickStepDegrees) * CompassTickStepDegrees;
        float halfWidth = compassPanel.rect.width * 0.5f;
        for (int i = 0; i < compassTickTexts.Length; i++)
        {
            float tickHeading = nearestTick + (i - CompassTickCenterIndex) * CompassTickStepDegrees;
            float delta = Mathf.DeltaAngle(heading, NormalizeHeading(tickHeading));
            float x = delta * CompassPixelsPerDegree;
            float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Abs(x) - halfWidth * 0.62f) / (halfWidth * 0.30f));
            bool cardinal = IsCardinalHeading(tickHeading);

            Text text = compassTickTexts[i];
            text.rectTransform.anchoredPosition = new Vector2(x, -4f);
            text.text = FormatCompassTick(tickHeading);
            text.fontStyle = cardinal ? FontStyle.Bold : FontStyle.Normal;
            text.fontSize = cardinal ? 18 : 13;
            text.color = new Color(0.92f, 0.90f, 0.82f, cardinal ? Mathf.Max(alpha, 0.25f) : alpha * 0.82f);

            RectTransform line = compassTickLines[i];
            line.anchoredPosition = new Vector2(x, cardinal ? -18f : -24f);
            line.sizeDelta = new Vector2(cardinal ? 2f : 1.2f, cardinal ? 22f : 12f);
            Image image = line.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.88f, 0.82f, 0.66f, alpha * (cardinal ? 0.70f : 0.42f));
            }
        }

        UpdateCompassTargetMarker(currentMeta, currentSession, controls, heading, halfWidth);
    }

    private void UpdateCompassTargetMarker(
        MetaGameState currentMeta,
        WildWindGameplaySession currentSession,
        WildWindFlightControlBridge controls,
        float viewHeading,
        float halfWidth)
    {
        if (!TryGetCompassTargetHeading(
            currentMeta,
            currentSession,
            controls,
            out float targetHeading,
            out string targetLabel,
            out Color markerColor))
        {
            SetCompassTargetMarkerVisible(false);
            return;
        }

        float delta = Mathf.DeltaAngle(viewHeading, targetHeading);
        float rawX = delta * CompassPixelsPerDegree;
        float edgePadding = 28f;
        float x = Mathf.Clamp(rawX, -Mathf.Max(0f, halfWidth - edgePadding), Mathf.Max(0f, halfWidth - edgePadding));
        bool clamped = !Mathf.Approximately(rawX, x);

        SetCompassTargetMarkerVisible(true);
        if (compassTargetLine != null)
        {
            compassTargetLine.anchoredPosition = new Vector2(x, -12f);
            Image image = compassTargetLine.GetComponent<Image>();
            if (image != null)
            {
                image.color = markerColor;
            }
        }

        if (compassTargetText != null)
        {
            if (clamped)
            {
                targetLabel = delta < 0f ? "< " + targetLabel : targetLabel + " >";
            }

            compassTargetText.rectTransform.anchoredPosition = new Vector2(x, -2f);
            compassTargetText.text = targetLabel;
            compassTargetText.color = markerColor;
        }
    }

    private void SetCompassTargetMarkerVisible(bool visible)
    {
        if (compassTargetLine != null)
        {
            compassTargetLine.gameObject.SetActive(visible);
        }

        if (compassTargetText != null)
        {
            compassTargetText.gameObject.SetActive(visible);
        }
    }

    private bool TryGetCompassTargetHeading(
        MetaGameState currentMeta,
        WildWindGameplaySession currentSession,
        WildWindFlightControlBridge controls,
        out float targetHeading,
        out string targetLabel,
        out Color markerColor)
    {
        if (TryGetActiveSortieExitDirection(currentMeta, currentSession, out Vector3 exitDirection))
        {
            targetHeading = NormalizeHeading(Mathf.Atan2(exitDirection.x, exitDirection.z) * Mathf.Rad2Deg);
            SortieReturnEstimate estimate = currentMeta.GetActiveSortieReturnEstimate();
            targetLabel = "BASE SLIP " + estimate.requiredExtractionRunupSeconds.ToString("0") + "s";
            markerColor = CompassExitTargetColor;
            return true;
        }

        targetHeading = 0f;
        targetLabel = "";
        markerColor = CompassDefaultTargetColor;
        return false;
    }

    private static bool TryGetActiveSortieExitDirection(
        MetaGameState currentMeta,
        WildWindGameplaySession currentSession,
        out Vector3 direction)
    {
        direction = Vector3.zero;
        SortieSessionState sortie = currentMeta != null ? currentMeta.ActiveSortie : null;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;
        if (currentMeta == null || currentMeta.CurrentMode != GameSessionMode.Flight || zone == null || !sortie.active)
        {
            return false;
        }

        zone.Normalize();
        Vector3 shipPosition = GetPlayerPosition(currentSession);
        Vector3 radial = new Vector3(
            shipPosition.x - zone.centerPosition.x,
            0f,
            shipPosition.z - zone.centerPosition.z);
        if (radial.sqrMagnitude > 0.25f)
        {
            direction = radial.normalized;
            return true;
        }

        Vector3 fallback = currentSession != null && currentSession.PlayerShipRoot != null
            ? Vector3.ProjectOnPlane(currentSession.PlayerShipRoot.forward, Vector3.up)
            : Vector3.zero;
        direction = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        return true;
    }

    private MetaGameState ResolveMeta()
    {
        if (meta == null)
        {
            meta = UnityEngine.Object.FindFirstObjectByType<MetaGameState>();
        }

        return meta;
    }

    private WildWindGameplaySession ResolveSession()
    {
        if (session == null)
        {
            session = UnityEngine.Object.FindFirstObjectByType<WildWindGameplaySession>();
        }

        return session;
    }

    private WildWindSessionCameraController ResolveSessionCameraController()
    {
        if (sessionCameraController == null)
        {
            sessionCameraController = UnityEngine.Object.FindFirstObjectByType<WildWindSessionCameraController>();
        }

        return sessionCameraController;
    }

    private WildWindFlightControlBridge ResolveFlightControls()
    {
        if (flightControls == null)
        {
            flightControls = UnityEngine.Object.FindFirstObjectByType<WildWindFlightControlBridge>();
        }

        return flightControls;
    }

    private static Vector3 GetPlayerPosition(WildWindGameplaySession currentSession)
    {
        return currentSession != null ? currentSession.PlayerPosition : Vector3.zero;
    }

    private static float GetShipSpeed(MetaGameState currentMeta)
    {
        if (currentMeta == null || currentMeta.shipLoader == null || currentMeta.shipLoader.targetShip == null)
        {
            return 0f;
        }

        Rigidbody body = currentMeta.shipLoader.targetShip.GetComponent<Rigidbody>();
        return body != null ? body.linearVelocity.magnitude : 0f;
    }

    private static float GetPlayerHeading(WildWindGameplaySession currentSession)
    {
        if (currentSession == null || currentSession.PlayerShipRoot == null)
        {
            return 0f;
        }

        return NormalizeHeading(currentSession.PlayerShipRoot.eulerAngles.y);
    }

    private static float GetViewHeading(WildWindGameplaySession currentSession)
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            return NormalizeHeading(camera.transform.eulerAngles.y);
        }

        return GetPlayerHeading(currentSession);
    }

    private static string FormatCompassTick(float heading)
    {
        int rounded = Mathf.RoundToInt(NormalizeHeading(heading));
        if (rounded == 0 || rounded == 360) return "N";
        if (rounded == 90) return "E";
        if (rounded == 180) return "S";
        if (rounded == 270) return "W";
        return rounded.ToString("000");
    }

    private static bool IsCardinalHeading(float heading)
    {
        int rounded = Mathf.RoundToInt(NormalizeHeading(heading));
        return rounded == 0 || rounded == 90 || rounded == 180 || rounded == 270 || rounded == 360;
    }

    private static float NormalizeHeading(float heading)
    {
        heading %= 360f;
        return heading < 0f ? heading + 360f : heading;
    }

    private static string FormatDistance(float meters)
    {
        return meters >= 1000f ? (meters / 1000f).ToString("0.0") + " км" : meters.ToString("0") + " м";
    }

    private static string FormatSignedMeters(float meters)
    {
        return (meters >= 0f ? "+" : "") + meters.ToString("0") + " м";
    }

    private static string FormatReserveTime(float stockKg, float kgPerSecond)
    {
        if (kgPerSecond <= 0.0001f)
        {
            return stockKg > 0.001f ? "inf" : "0s";
        }

        return FormatHudDuration(stockKg / kgPerSecond);
    }

    private static string MakeAsciiBar(float ratio, int width)
    {
        width = Mathf.Clamp(width, 4, 40);
        int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ratio) * width), 0, width);
        return "[" + new string('#', filled) + new string('-', width - filled) + "]";
    }

    private static string FormatTaskLine(string label, int current, int target)
    {
        target = Mathf.Max(1, target);
        float ratio = Mathf.Clamp01((float)Mathf.Max(0, current) / target);
        string state = current >= target ? "готово" : "идет";
        return label + ": " + current + "/" + target + " кг " + MakeAsciiBar(ratio, 14) + " " + state;
    }

    private static int GetAnyOreCargoKg(PlayerProgress progress)
    {
        if (progress == null) return 0;

        return progress.GetShipCargoAmount("windshale_ore")
            + progress.GetShipCargoAmount("dawnspar_ore")
            + progress.GetShipCargoAmount("bluebrass_ore");
    }

    private static string FormatBoundaryDistance(float meters)
    {
        if (meters < -0.05f)
        {
            return "Outside: " + FormatBoundaryMagnitude(-meters);
        }

        if (meters <= 0.05f)
        {
            return "Boundary: edge";
        }

        return "Boundary: " + FormatBoundaryMagnitude(meters);
    }

    private static string FormatBoundaryMagnitude(float meters)
    {
        if (meters >= 1000f)
        {
            return (meters / 1000f).ToString("0.0") + " km";
        }

        return meters < 10f ? meters.ToString("0.0") + " m" : meters.ToString("0") + " m";
    }

    private static string FormatSortieBoundaryState(SortieReturnEstimate estimate)
    {
        if (!estimate.hasActiveSortie)
        {
            return "-";
        }

        if (estimate.distanceToBoundaryMeters < -0.05f)
        {
            return "outside " + FormatBoundaryMagnitude(-estimate.distanceToBoundaryMeters);
        }

        if (estimate.distanceToBoundaryMeters <= 0.05f)
        {
            return "at edge";
        }

        return "inside, " + FormatBoundaryMagnitude(estimate.distanceToBoundaryMeters) + " to edge";
    }

    private static string FormatExtractionBlocker(SortieReturnEstimate estimate)
    {
        if (!estimate.hasActiveSortie) return "no sortie";
        if (!estimate.isAboveStorm) return "storm layer";
        if (estimate.isInsideCylinder) return "leave cylinder";
        if (!estimate.hasExtractionRunup) return "hold slip " + estimate.missingExtractionRunupSeconds.ToString("0.0") + "s";
        return "blocked";
    }

    private static string FormatReserve(float currentKg, float requiredKg)
    {
        return currentKg.ToString("0.#") + "/" + requiredKg.ToString("0.#") + " kg";
    }

    private static string FormatKg(float kg)
    {
        if (float.IsNaN(kg) || float.IsInfinity(kg) || kg < 0f)
        {
            return "-";
        }

        if (kg >= 1000f)
        {
            return (kg / 1000f).ToString("0.0") + " t";
        }

        return kg >= 100f ? kg.ToString("0") + " kg" : kg.ToString("0.#") + " kg";
    }

    private static string FormatResourceUnitMass(float massKg)
    {
        return massKg <= 0.0001f ? "no cargo mass" : massKg.ToString("0.###", CultureInfo.InvariantCulture) + " kg/unit";
    }

    private static string FormatMetaResourceAmount(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        if (amount >= 1000000)
        {
            float value = amount / 1000000f;
            string format = value < 10f ? "0.##" : value < 100f ? "0.#" : "0";
            return TrimCompactNumber(value.ToString(format, CultureInfo.InvariantCulture)) + "M";
        }

        if (amount >= 10000)
        {
            float value = amount / 1000f;
            string format = value < 100f ? "0.#" : "0";
            return TrimCompactNumber(value.ToString(format, CultureInfo.InvariantCulture)) + "K";
        }

        return amount.ToString(CultureInfo.InvariantCulture);
    }

    private static string TrimCompactNumber(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains("."))
        {
            return value ?? "";
        }

        while (value.EndsWith("0", System.StringComparison.Ordinal))
        {
            value = value.Substring(0, value.Length - 1);
        }

        return value.EndsWith(".", System.StringComparison.Ordinal)
            ? value.Substring(0, value.Length - 1)
            : value;
    }

    private static Color GetResourceRowColor(int index)
    {
        return (index & 1) == 0
            ? new Color(0.034f, 0.040f, 0.045f, 0.92f)
            : new Color(0.026f, 0.031f, 0.036f, 0.92f);
    }

    private static string FormatHudDuration(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
        {
            return "-";
        }

        int totalSeconds = Mathf.CeilToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int restSeconds = totalSeconds % 60;
        if (hours > 0)
        {
            return hours + "h " + minutes.ToString("00") + "m";
        }

        if (minutes > 0)
        {
            return minutes + "m " + restSeconds.ToString("00") + "s";
        }

        return restSeconds + "s";
    }

    private static string GetItemDisplayName(SessionConfigDatabase config, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return "resource";
        }

        string displayName = config != null ? config.GetItemNameRu(itemId) : itemId;
        return string.IsNullOrWhiteSpace(displayName) ? itemId : displayName;
    }

    private static string FormatSlipstreamState(ShipPhysics ship)
    {
        if (ship == null)
        {
            return "slip -";
        }

        string state = ship.claudiumSlipstreamEnabled ? "on" : "off";
        return "slip " + state + " " + (ship.ClaudiumSlipstreamCharge01 * 100f).ToString("0") + "%";
    }

    private static string FormatEngineMode(ShipPhysics ship)
    {
        if (ship == null)
        {
            return "hull thrust -";
        }

        return "hull thrust " + (ship.thrustInput * 100f).ToString("0") + "%";
    }

    private static string ShortenButtonLabel(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        if (maxLength <= 3 || value.Length <= maxLength) return value;
        return value.Substring(0, maxLength - 1) + ".";
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? "";
        }
    }

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void SetButtonVisible(Text buttonLabel, bool visible)
    {
        if (buttonLabel != null && buttonLabel.transform.parent != null)
        {
            buttonLabel.transform.parent.gameObject.SetActive(visible);
        }
    }

    private static void MakeInvisibleHitboxLayer(RectTransform layer)
    {
        if (layer == null)
        {
            return;
        }

        CanvasGroup group = layer.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = layer.gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        group.ignoreParentGroups = false;
    }

    private static bool TryReadCoordinate(InputField input, out float value)
    {
        value = 0f;
        if (input == null)
        {
            return false;
        }

        string text = (input.text ?? "").Trim().Replace(',', '.');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsInputFocused(InputField input)
    {
        return input != null && input.isFocused;
    }

    private static void SetInputText(InputField input, string value)
    {
        if (input == null || input.text == value)
        {
            return;
        }

        input.text = value;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
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

    private static Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null)
        {
            return cachedCircleSprite;
        }

        const int size = 64;
        const float radius = size * 0.48f;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Meta Dock Circle Sprite",
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, true);
        cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        cachedCircleSprite.name = "Meta Dock Circle Sprite";
        return cachedCircleSprite;
    }

    private static Sprite GetRoundedPanelSprite()
    {
        if (cachedRoundedPanelSprite == null)
        {
            cachedRoundedPanelSprite = CreateRoundedSprite("Port Rounded Panel Sprite", 96, 22f, 28f);
        }

        return cachedRoundedPanelSprite;
    }

    private static Sprite GetRoundedSmallSprite()
    {
        if (cachedRoundedSmallSprite == null)
        {
            cachedRoundedSmallSprite = CreateRoundedSprite("Port Rounded Small Sprite", 80, 14f, 22f);
        }

        return cachedRoundedSmallSprite;
    }

    private static Sprite CreateRoundedSprite(string spriteName, int size, float radius, float border)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            hideFlags = HideFlags.HideAndDontSave
        };

        Vector2 min = new Vector2(radius, radius);
        Vector2 max = new Vector2(size - 1 - radius, size - 1 - radius);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x, min.x, max.x);
                float cy = Mathf.Clamp(y, min.y, max.y);
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        sprite.name = spriteName;
        return sprite;
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

    private static void ApplyRectTransform(RectTransform rect, RectTransformSpec spec)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMax;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.sizeDelta;
    }

    private struct MetaResourceCounterSpec
    {
        public readonly string itemId;
        public readonly int amount;
        public readonly string fallbackText;
        public readonly Color fallbackColor;

        public MetaResourceCounterSpec(string itemId, int amount, string fallbackText, Color fallbackColor)
        {
            this.itemId = itemId;
            this.amount = amount;
            this.fallbackText = fallbackText;
            this.fallbackColor = fallbackColor;
        }
    }

    private struct MetaTopRightButtonSpec
    {
        public readonly string windowId;
        public readonly string title;
        public readonly string iconText;

        public MetaTopRightButtonSpec(string windowId, string title, string iconText)
        {
            this.windowId = windowId;
            this.title = title;
            this.iconText = iconText;
        }
    }

    private struct MetaSideButtonSpec
    {
        public readonly string windowId;
        public readonly string title;
        public readonly string iconText;
        public readonly string iconResourceName;

        public MetaSideButtonSpec(string windowId, string title, string iconText, string iconResourceName)
        {
            this.windowId = windowId;
            this.title = title;
            this.iconText = iconText;
            this.iconResourceName = iconResourceName;
        }
    }

    private struct MetaQuestSpec
    {
        public readonly string iconText;
        public readonly string lineOne;
        public readonly string lineTwo;
        public readonly int current;
        public readonly int target;

        public MetaQuestSpec(string iconText, string lineOne, string lineTwo, int current, int target)
        {
            this.iconText = iconText;
            this.lineOne = lineOne;
            this.lineTwo = lineTwo;
            this.current = current;
            this.target = target;
        }
    }

    private struct MetaProjectSpec
    {
        public readonly string title;
        public readonly string amount;
        public readonly string iconText;
        public readonly string timer;
        public readonly float progress01;

        public MetaProjectSpec(string title, string amount, string iconText, string timer, float progress01)
        {
            this.title = title;
            this.amount = amount;
            this.iconText = iconText;
            this.timer = timer;
            this.progress01 = progress01;
        }
    }

    private sealed class ResourceCatalogRow
    {
        public string itemId;
        public Image icon;
    }

    private sealed class HudWindow
    {
        private const float HeaderHeight = 34f;
        private bool flightVisible = true;

        private RectTransform modalLayer;
        private Button fadeButton;
        private Button closeButton;
        public RectTransform root;
        public RectTransform content;
        public bool IsOpen { get; private set; }
        public bool IsModalForTests => modalLayer != null
            && fadeButton != null
            && closeButton != null
            && root != null
            && content != null
            && fadeButton.transform.parent == modalLayer
            && root.transform.parent == modalLayer
            && fadeButton.transform.GetSiblingIndex() < root.transform.GetSiblingIndex()
            && IsCenteredModalRoot(root)
            && !ContainsHeaderButtonLabel("-");

        public static HudWindow Create(
            WildWindGameplayHud owner,
            RectTransform parent,
            string id,
            string title,
            Vector2 position,
            Vector2 size,
            bool openByDefault)
        {
            if (TryCreateFromPrefab(owner, parent, id, title, size, openByDefault, out HudWindow prefabWindow))
            {
                return prefabWindow;
            }

            HudWindow window = new HudWindow
            {
                IsOpen = openByDefault
            };

            window.modalLayer = CreateRect("HUD Window Modal Layer " + id, parent, new RectTransformSpec
            {
                anchorMin = Vector2.zero,
                anchorMax = Vector2.one,
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = Vector2.zero
            }).GetComponent<RectTransform>();

            RectTransform fade = owner.CreatePanel("Modal Fade", window.modalLayer, new RectTransformSpec
            {
                anchorMin = Vector2.zero,
                anchorMax = Vector2.one,
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = Vector2.zero
            }, new Color(0f, 0f, 0f, 0.58f));
            Image fadeImage = fade.GetComponent<Image>();
            window.fadeButton = fade.gameObject.AddComponent<Button>();
            window.fadeButton.targetGraphic = fadeImage;
            window.fadeButton.transition = Selectable.Transition.None;
            window.fadeButton.onClick.AddListener(() => window.SetOpen(false));

            window.root = owner.CreatePanel("HUD Window " + id, window.modalLayer, new RectTransformSpec
            {
                anchorMin = new Vector2(0.5f, 0.5f),
                anchorMax = new Vector2(0.5f, 0.5f),
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = size
            }, new Color(0.014f, 0.017f, 0.021f, 0.86f));

            RectTransform header = owner.CreatePanel("Header", window.root, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = Vector2.zero,
                sizeDelta = new Vector2(0f, HeaderHeight)
            }, new Color(0.055f, 0.047f, 0.032f, 0.94f));

            Text titleText = owner.CreateText(header, title, 16, new Vector2(10f, -3f), new Vector2(size.x - 54f, 28f), TextAnchor.MiddleLeft, new Color(0.95f, 0.82f, 0.55f, 1f));
            titleText.fontStyle = FontStyle.Bold;
            titleText.raycastTarget = false;

            window.closeButton = CreateHeaderButton(owner, header, "x", new Vector2(-10f, -4f), () => window.SetOpen(false));

            GameObject contentObject = CreateRect("Content", window.root, new RectTransformSpec
            {
                anchorMin = Vector2.zero,
                anchorMax = Vector2.one,
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = Vector2.zero
            });
            window.content = contentObject.GetComponent<RectTransform>();
            window.content.offsetMin = new Vector2(10f, 10f);
            window.content.offsetMax = new Vector2(-10f, -HeaderHeight - 8f);

            window.RefreshVisible();
            return window;
        }

        private static bool TryCreateFromPrefab(
            WildWindGameplayHud owner,
            RectTransform parent,
            string id,
            string title,
            Vector2 size,
            bool openByDefault,
            out HudWindow window)
        {
            window = null;
            if (owner == null || parent == null)
            {
                return false;
            }

            GameObject prefab = Resources.Load<GameObject>(HudWindowPrefabFolderResourcePath + id);
            if (prefab == null)
            {
                prefab = Resources.Load<GameObject>(HudWindowFrameResourcePath);
            }

            if (prefab == null)
            {
                return false;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.name = "HUD Window Modal Layer " + id;

            WildWindHudWindowFrame frame = instance.GetComponent<WildWindHudWindowFrame>();
            if (frame == null)
            {
                frame = instance.GetComponentInChildren<WildWindHudWindowFrame>(true);
            }

            if (frame == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            frame.ResolveReferences();
            RectTransform modalLayer = frame.ModalLayer;
            RectTransform fadeLayer = frame.FadeLayer;
            RectTransform windowRoot = frame.WindowRoot;
            RectTransform headerRoot = frame.HeaderRoot;
            RectTransform titleRoot = frame.TitleRoot;
            RectTransform closeButtonRoot = frame.CloseButtonRoot;
            RectTransform contentRoot = frame.ContentRoot;
            if (modalLayer == null || fadeLayer == null || windowRoot == null || headerRoot == null || titleRoot == null || closeButtonRoot == null || contentRoot == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            window = new HudWindow
            {
                IsOpen = openByDefault,
                modalLayer = modalLayer,
                root = windowRoot,
                content = contentRoot
            };
            HudWindow createdWindow = window;

            ApplyRectTransform(window.modalLayer, StretchFull());
            ApplyRectTransform(fadeLayer, StretchFull());
            fadeLayer.SetAsFirstSibling();
            window.root.SetAsLastSibling();

            Image fadeImage = EnsureImage(fadeLayer, new Color(0f, 0f, 0f, 0.58f), true);
            window.fadeButton = EnsureButton(fadeLayer, fadeImage);
            window.fadeButton.transition = Selectable.Transition.None;
            window.fadeButton.onClick.RemoveAllListeners();
            window.fadeButton.onClick.AddListener(() => createdWindow.SetOpen(false));

            window.root.name = "HUD Window " + id;
            ApplyRectTransform(window.root, new RectTransformSpec
            {
                anchorMin = new Vector2(0.5f, 0.5f),
                anchorMax = new Vector2(0.5f, 0.5f),
                pivot = new Vector2(0.5f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = size
            });
            EnsureImage(window.root, new Color(0.014f, 0.017f, 0.021f, 0.86f), false);

            ApplyRectTransform(headerRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(0.5f, 1f),
                anchoredPosition = Vector2.zero,
                sizeDelta = new Vector2(0f, HeaderHeight)
            });
            EnsureImage(headerRoot, new Color(0.055f, 0.047f, 0.032f, 0.94f), false);

            ApplyRectTransform(titleRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = new Vector2(10f, -3f),
                sizeDelta = new Vector2(-54f, 28f)
            });
            Text titleText = EnsureText(owner, titleRoot, title, 16, TextAnchor.MiddleLeft, new Color(0.95f, 0.82f, 0.55f, 1f));
            titleText.fontStyle = FontStyle.Bold;
            titleText.raycastTarget = false;

            ApplyRectTransform(closeButtonRoot, new RectTransformSpec
            {
                anchorMin = new Vector2(1f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(1f, 1f),
                anchoredPosition = new Vector2(-10f, -4f),
                sizeDelta = new Vector2(24f, 24f)
            });
            Image closeImage = EnsureImage(closeButtonRoot, new Color(0.12f, 0.095f, 0.055f, 0.94f), true);
            window.closeButton = EnsureButton(closeButtonRoot, closeImage);
            window.closeButton.onClick.RemoveAllListeners();
            window.closeButton.onClick.AddListener(() => createdWindow.SetOpen(false));
            EnsureChildLabel(owner, closeButtonRoot, "x", 14, TextAnchor.MiddleCenter, new Color(0.94f, 0.84f, 0.62f, 1f));

            ApplyRectTransform(window.content, StretchFull());
            window.content.offsetMin = new Vector2(10f, 10f);
            window.content.offsetMax = new Vector2(-10f, -HeaderHeight - 8f);

            window.RefreshVisible();
            return true;
        }

        private static Image EnsureImage(RectTransform rect, Color color, bool raycastTarget = true)
        {
            Image image = rect.GetComponent<Image>();
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Button EnsureButton(RectTransform rect, Image targetGraphic)
        {
            Button button = rect.GetComponent<Button>();
            if (button == null)
            {
                button = rect.gameObject.AddComponent<Button>();
            }

            button.targetGraphic = targetGraphic;
            return button;
        }

        private static Text EnsureText(WildWindGameplayHud owner, RectTransform rect, string value, int fontSize, TextAnchor alignment, Color color)
        {
            Text text = rect.GetComponent<Text>();
            if (text == null)
            {
                text = rect.gameObject.AddComponent<Text>();
            }

            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignByGeometry = true;
            text.raycastTarget = false;
            text.text = value ?? "";
            return text;
        }

        private static Text EnsureChildLabel(WildWindGameplayHud owner, RectTransform parent, string value, int fontSize, TextAnchor alignment, Color color)
        {
            Text text = parent.GetComponentInChildren<Text>(true);
            if (text == null)
            {
                text = owner.CreateText(parent, value, fontSize, Vector2.zero, parent.sizeDelta, alignment, color);
            }

            RectTransform textRect = text.GetComponent<RectTransform>();
            if (textRect != null)
            {
                ApplyRectTransform(textRect, StretchFull());
            }

            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.alignByGeometry = true;
            text.raycastTarget = false;
            text.text = value ?? "";
            return text;
        }

        public void SetFlightVisible(bool visible)
        {
            flightVisible = visible;
            RefreshVisible();
        }

        public void ToggleOpen()
        {
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (open && modalLayer != null)
            {
                root.anchoredPosition = Vector2.zero;
                if (fadeButton != null)
                {
                    fadeButton.transform.SetAsFirstSibling();
                }

                if (root != null)
                {
                    root.SetAsLastSibling();
                }

                modalLayer.SetAsLastSibling();
            }

            RefreshVisible();
        }

        public bool CloseByBackdropForTests()
        {
            if (fadeButton == null)
            {
                return false;
            }

            SetOpen(false);
            return !IsOpen;
        }

        private void RefreshVisible()
        {
            if (modalLayer != null)
            {
                modalLayer.gameObject.SetActive(flightVisible && IsOpen);
            }
        }

        private static Button CreateHeaderButton(WildWindGameplayHud owner, RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateRect("Header Button " + label, parent, new RectTransformSpec
            {
                anchorMin = new Vector2(1f, 1f),
                anchorMax = new Vector2(1f, 1f),
                pivot = new Vector2(1f, 1f),
                anchoredPosition = anchoredPosition,
                sizeDelta = new Vector2(24f, 24f)
            });

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.095f, 0.055f, 0.94f);
            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);
            Text text = owner.CreateText(buttonObject.GetComponent<RectTransform>(), label, 14, Vector2.zero, new Vector2(24f, 24f), TextAnchor.MiddleCenter, new Color(0.94f, 0.84f, 0.62f, 1f));
            text.raycastTarget = false;
            return button;
        }

        private static bool IsCenteredModalRoot(RectTransform rect)
        {
            return Mathf.Approximately(rect.anchorMin.x, 0.5f)
                && Mathf.Approximately(rect.anchorMax.x, 0.5f)
                && Mathf.Approximately(rect.anchorMin.y, 0.5f)
                && Mathf.Approximately(rect.anchorMax.y, 0.5f)
                && Mathf.Approximately(rect.pivot.x, 0.5f)
                && Mathf.Approximately(rect.pivot.y, 0.5f)
                && rect.anchoredPosition.sqrMagnitude <= 0.01f;
        }

        private bool ContainsHeaderButtonLabel(string label)
        {
            if (root == null)
            {
                return false;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].text == label)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [DefaultExecutionOrder(10000)]
    private sealed class RadarTacticalSessionOverlay : MonoBehaviour
    {
        private static readonly float[] RingMeters = { 100f, 250f, 500f, 1000f, 2000f, 5000f, 10000f };
        private const int RingSegments = 160;
        private const float PlaneHeightOffsetMeters = 0.8f;
        private const float RingWidthMeters = 1.8f;
        private const float StemWidthMeters = 1.2f;
        private const float MarkerSizeMeters = 16f;
        private readonly List<RadarEntry> entries = new List<RadarEntry>();
        private readonly List<EntryVisual> entryVisuals = new List<EntryVisual>();
        private LineRenderer[] ringLines;
        private LineRenderer forwardLine;
        private Material lineMaterial;
        private Transform shipTransform;
        private Vector3 fallbackShipPosition;
        private Vector3 fallbackShipForward = Vector3.forward;
        private bool overlayVisible;

        public void SetEntries(Transform newShipTransform, Vector3 shipPosition, Vector3 shipForward, List<RadarEntry> sourceEntries, bool visible)
        {
            shipTransform = newShipTransform;
            fallbackShipPosition = shipPosition;
            fallbackShipForward = shipForward.sqrMagnitude > 0.0001f ? shipForward : Vector3.forward;
            entries.Clear();
            if (sourceEntries != null)
            {
                for (int i = 0; i < sourceEntries.Count; i++)
                {
                    entries.Add(sourceEntries[i]);
                }
            }

            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            RebuildGeometry();
        }

        public void SetVisible(bool visible)
        {
            overlayVisible = visible;
            if (!visible)
            {
                if (ringLines != null)
                {
                    for (int i = 0; i < ringLines.Length; i++)
                    {
                        ringLines[i].enabled = false;
                    }
                }

                if (forwardLine != null)
                {
                    forwardLine.enabled = false;
                }

                for (int i = 0; i < entryVisuals.Count; i++)
                {
                    entryVisuals[i].SetVisible(false);
                }

                return;
            }

            EnsureVisuals();
            for (int i = 0; i < ringLines.Length; i++)
            {
                ringLines[i].enabled = true;
            }

            if (forwardLine != null)
            {
                forwardLine.enabled = true;
            }
        }

        private void LateUpdate()
        {
            if (overlayVisible)
            {
                RebuildGeometry();
            }
        }

        private void RebuildGeometry()
        {
            EnsureVisuals();
            Vector3 shipPosition = shipTransform != null ? shipTransform.position : fallbackShipPosition;
            Vector3 shipForward = shipTransform != null ? shipTransform.forward : fallbackShipForward;
            float planeY = shipPosition.y + PlaneHeightOffsetMeters;
            for (int i = 0; i < ringLines.Length; i++)
            {
                DrawRing(ringLines[i], shipPosition, RingMeters[i], planeY, i >= RingMeters.Length - 2);
            }

            DrawForwardLine(shipPosition, shipForward, planeY);

            int visibleEntryCount = 0;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    RadarEntry entry = entries[i];
                    Vector3 entryPosition = GetCurrentEntryPosition(entry);
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(entryPosition.x, entryPosition.z),
                        new Vector2(shipPosition.x, shipPosition.z));
                    if (horizontalDistance > RadarMaxRangeMeters)
                    {
                        continue;
                    }

                    EntryVisual visual = EnsureEntryVisual(visibleEntryCount);
                    DrawEntryVisual(visual, entry, entryPosition, planeY);
                    visibleEntryCount++;
                }
            }

            for (int i = visibleEntryCount; i < entryVisuals.Count; i++)
            {
                entryVisuals[i].SetVisible(false);
            }
        }

        private static Vector3 GetCurrentEntryPosition(RadarEntry entry)
        {
            return entry.target != null ? entry.target.position : entry.position;
        }

        private void EnsureVisuals()
        {
            if (lineMaterial == null)
            {
                lineMaterial = CreateLineMaterial();
            }

            if (ringLines == null)
            {
                ringLines = new LineRenderer[RingMeters.Length];
                for (int i = 0; i < ringLines.Length; i++)
                {
                    ringLines[i] = CreateLine("Radar Ring " + FormatBoundaryMagnitude(RingMeters[i]), RingWidthMeters);
                }
            }

            if (forwardLine == null)
            {
                forwardLine = CreateLine("Radar Forward Line", 2.2f);
            }
        }

        private void DrawRing(LineRenderer line, Vector3 shipPosition, float radius, float planeY, bool major)
        {
            line.positionCount = RingSegments + 1;
            line.widthMultiplier = major ? RingWidthMeters * 1.35f : RingWidthMeters;
            Color color = major
                ? new Color(0.42f, 0.88f, 1f, 0.48f)
                : new Color(0.35f, 0.82f, 0.96f, 0.32f);
            line.startColor = color;
            line.endColor = color;

            for (int i = 0; i <= RingSegments; i++)
            {
                float angle = i / (float)RingSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(
                    shipPosition.x + Mathf.Cos(angle) * radius,
                    planeY,
                    shipPosition.z + Mathf.Sin(angle) * radius));
            }
        }

        private void DrawForwardLine(Vector3 shipPosition, Vector3 shipForward, float planeY)
        {
            Vector3 forward = Vector3.ProjectOnPlane(shipForward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            forwardLine.positionCount = 2;
            forwardLine.startColor = new Color(0.46f, 0.95f, 1f, 0.55f);
            forwardLine.endColor = new Color(0.46f, 0.95f, 1f, 0.08f);
            forwardLine.SetPosition(0, new Vector3(shipPosition.x, planeY + 0.05f, shipPosition.z));
            forwardLine.SetPosition(1, new Vector3(shipPosition.x, planeY + 0.05f, shipPosition.z) + forward * RadarMaxRangeMeters);
        }

        private void DrawEntryVisual(EntryVisual visual, RadarEntry entry, Vector3 entryPosition, float planeY)
        {
            Color color = GetEntryColor(entry.entryKind);
            Vector3 projected = new Vector3(entryPosition.x, planeY + 0.2f, entryPosition.z);
            Vector3 actual = entryPosition;
            if (Mathf.Abs(actual.y - projected.y) < 1f)
            {
                actual.y = projected.y + 1f;
            }

            visual.SetVisible(true);
            visual.stem.positionCount = 2;
            visual.stem.widthMultiplier = StemWidthMeters;
            visual.stem.startColor = new Color(color.r, color.g, color.b, 0.20f);
            visual.stem.endColor = new Color(color.r, color.g, color.b, 0.82f);
            visual.stem.SetPosition(0, projected);
            visual.stem.SetPosition(1, actual);

            float markerSize = MarkerSizeMeters;
            visual.marker.positionCount = 5;
            visual.marker.widthMultiplier = Mathf.Max(1.4f, StemWidthMeters);
            visual.marker.startColor = color;
            visual.marker.endColor = color;
            visual.marker.SetPosition(0, projected + Vector3.forward * markerSize);
            visual.marker.SetPosition(1, projected + Vector3.right * markerSize);
            visual.marker.SetPosition(2, projected + Vector3.back * markerSize);
            visual.marker.SetPosition(3, projected + Vector3.left * markerSize);
            visual.marker.SetPosition(4, projected + Vector3.forward * markerSize);
        }

        private EntryVisual EnsureEntryVisual(int index)
        {
            while (entryVisuals.Count <= index)
            {
                EntryVisual visual = new EntryVisual
                {
                    stem = CreateLine("Radar Object Stem " + entryVisuals.Count, StemWidthMeters),
                    marker = CreateLine("Radar Object Marker " + entryVisuals.Count, StemWidthMeters)
                };
                entryVisuals.Add(visual);
            }

            return entryVisuals[index];
        }

        private LineRenderer CreateLine(string lineName, float widthMeters)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.loop = false;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = widthMeters;
            line.material = lineMaterial;
            line.enabled = false;
            return line;
        }

        private static Material CreateLineMaterial()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
            if (shader != null && shader.name == "Hidden/Internal-Colored")
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            return material;
        }

        private static Color GetEntryColor(RadarEntryKind kind)
        {
            switch (kind)
            {
                case RadarEntryKind.Boulder:
                    return new Color(0.76f, 0.66f, 0.48f, 0.95f);
                case RadarEntryKind.Debris:
                    return new Color(0.96f, 0.78f, 0.28f, 0.96f);
                default:
                    return Color.white;
            }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }

        private sealed class EntryVisual
        {
            public LineRenderer stem;
            public LineRenderer marker;

            public void SetVisible(bool visible)
            {
                if (stem != null)
                {
                    stem.enabled = visible;
                }

                if (marker != null)
                {
                    marker.enabled = visible;
                }
            }
        }
    }

    private enum RadarEntryKind
    {
        Boulder,
        Debris
    }

    private struct RadarEntry
    {
        public string name;
        public string kind;
        public RadarEntryKind entryKind;
        public Transform target;
        public Vector3 position;
        public Vector2 mapOffsetMeters;
        public float distanceMeters;
        public float horizontalDistanceMeters;
        public float verticalDeltaMeters;
    }

    private sealed class DevelopmentSupplierView
    {
        public string factionId = "";
        public string displayName = "";
        public Color color = Color.white;
        public ShipTreeEntryConfig starterShip;
        public readonly List<ShipTreeEntryConfig> ships = new List<ShipTreeEntryConfig>();
    }

    private sealed class DevelopmentBranchView
    {
        public string branchId = "";
        public string displayName = "";
        public int treeRow;
        public readonly List<ShipTreeEntryConfig> ships = new List<ShipTreeEntryConfig>();
    }

    private sealed class DevelopmentShipTileView
    {
        public string shipId = "";
        public Image image;
        public Outline outline;
        public Color normalColor;
        public Color selectedColor;
        public Color normalOutlineColor;
    }

    private sealed class DevelopmentTreeMousePanHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IEndDragHandler
    {
        public WildWindGameplayHud owner;
        private bool dragging;
        private PointerEventData.InputButton activeButton;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            activeButton = eventData.button;
            dragging = true;
            owner?.SetDevelopmentTreePointerCapture(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || eventData == null)
            {
                return;
            }

            owner?.PanDevelopmentTreeScroll(eventData.delta);
            eventData.Use();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != activeButton)
            {
                return;
            }

            Release();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Release();
            eventData?.Use();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            owner?.SetDevelopmentTreePointerCapture(false);
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
}
