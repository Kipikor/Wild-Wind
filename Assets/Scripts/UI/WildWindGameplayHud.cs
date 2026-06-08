using System.Collections.Generic;
using System.Globalization;
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
        "game.hud.base_inputs",
        "game.hud.base_materials",
        "game.hud.ship_supplies",
        "game.hud.distance",
        "game.hud.altitude",
        "game.hud.speed",
        "game.hud.process_base",
        "game.hud.takeoff",
        "game.hud.dock",
        "game.hud.run_base_work",
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
    private const float EngineAfterburnerPowerMultiplier = 1.2f;
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
    private const string WindowMetaPortId = "meta_port";
    private static readonly Color EngineForwardThrustColor = new Color(0.14f, 0.78f, 0.28f, 0.96f);
    private static readonly Color EngineReverseThrustColor = new Color(0.96f, 0.72f, 0.12f, 0.96f);
    private static readonly int[] EngineThrottleGearNotches = { 5, 4, 3, 2, 1, 0, -1, -2, -3 };
    private static readonly Color CompassDefaultTargetColor = new Color(0.44f, 0.95f, 1f, 0.96f);
    private static readonly Color CompassExitTargetColor = new Color(1f, 0.78f, 0.22f, 0.98f);

    [SerializeField, InspectorName("Reference Resolution")] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas canvas;
    private RectTransform root;
    private RectTransform dockedPanel;
    private RectTransform flightPanel;
    private RectTransform sortiePanel;
    private RectTransform windowDockPanel;
    private RectTransform compassPanel;
    private RectTransform enginePowerBar;
    private RectTransform engineLiftPowerFill;
    private RectTransform engineModulePowerFill;
    private RectTransform engineThrustPowerFill;
    private RectTransform engineAfterburnerMarker;
    private RectTransform[] engineThrottleGearMarks;
    private Text[] engineThrottleGearTexts;
    private Text[] compassTickTexts;
    private RectTransform[] compassTickLines;
    private RectTransform compassTargetLine;
    private Text compassTargetText;
    private Text modeText;
    private Text objectiveText;
    private Text sessionPathText;
    private Text baseInputsText;
    private Text baseMaterialsText;
    private Text shipSuppliesText;
    private Text fittingText;
    private Text baseProcessingText;
    private Text baseCascadeText;
    private Text baseCascadeOrderText;
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
    private Button processBaseButton;
    private Button takeoffButton;
    private Button dockButton;
    private Button runBaseWorkButton;
    private Button sortieButton;
    private Button portButton;
    private Button menuButton;
    private Text processBaseText;
    private Text takeoffText;
    private Text dockText;
    private Text runBaseWorkText;
    private Text sortieText;
    private Text portText;
    private Text menuText;
    private Text flightMenuText;
    private Text afterburnerText;
    private Text claudiumSlipstreamText;
    private HudWindow locationWindow;
    private HudWindow returnWindow;
    private HudWindow shipWindow;
    private HudWindow tasksWindow;
    private HudWindow cargoWindow;
    private HudWindow radarWindow;
    private HudWindow modulesWindow;
    private HudWindow metaPortWindow;
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
    private Text metaPortResourceText;
    private Text metaPortShipText;
    private Text metaPortContentText;
    private Text metaPortStatusText;
    private Text metaPortPrimaryActionText;
    private Text metaPortSecondaryActionText;
    private Text[] metaPortTabTexts;
    private WildWindMetaPortScreen fullMetaPortScreen;
    private GameObject fullMetaPortScreenObject;
    private RadarTacticalSessionOverlay radarSessionOverlay;
    private WildWindMetaPortUiState metaPortState;
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
    private bool radarShowBoulders = true;
    private bool radarShowDebris = true;
    private bool openMetaPortOnDockedStart = true;
    private static Font cachedDefaultFont;

    public bool IsReady => canvas != null && root != null && ResolveMeta() != null && ResolveSession() != null;
    public bool IsDockedPanelVisible => dockedPanel != null && dockedPanel.gameObject.activeSelf;
    public bool IsFlightPanelVisible => flightPanel != null && flightPanel.gameObject.activeSelf;
    public bool IsFlightCompassVisible => compassPanel != null && compassPanel.gameObject.activeSelf;
    public float LastCompassHeadingDegrees => lastCompassHeadingDegrees;
    public string MetaPortReport => EnsureMetaPortState().BuildReport();
    public string MetaPortContentForTests => EnsureMetaPortState().BuildTabContent();
    public string MetaPortSelectedTab => WildWindMetaPortUiState.GetTabDisplayName(EnsureMetaPortState().selectedTab);
    public bool IsMetaPortRuntimeBoundForTests => EnsureMetaPortState().IsRuntimeBound;
    public bool IsMetaPortReadyForTests => metaPortWindow != null
        && EnsureMetaPortState().IsReady
        && metaPortTabTexts != null
        && metaPortTabTexts.Length == WildWindMetaPortUiState.Tabs.Count;
    public bool IsMetaPortScreenVisibleForTests => fullMetaPortScreen != null && fullMetaPortScreen.IsVisible;

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

        if (Object.FindFirstObjectByType<WildWindGameplayHud>() != null)
        {
            return;
        }

        GameObject hudObject = new GameObject(HudObjectName);
        hudObject.AddComponent<WildWindGameplayHud>();
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
        OpenMetaPortOnDockedStartIfReady();

        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + 0.2f;
        RefreshState(false);
    }

    public bool TryRunBaseProcessAction()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta != null)
        {
            bool result = currentMeta.TryGetNextProcessableBaseBranch(out _)
                ? currentMeta.TryProcessNextBaseBatch(out statusMessage)
                : currentMeta.CanRefuelBaseShip(out _)
                ? currentMeta.TryRefuelBaseShip(out statusMessage)
                : currentMeta.TryLoadStarterMunitionsAtBase(out statusMessage);
            RefreshState(true);
            return result;
        }

        statusMessage = "Session runtime is unavailable.";
        RefreshState(true);
        return false;
    }

    public bool TryTakeOff()
    {
        WildWindGameplaySession currentSession = ResolveSession();
        MetaGameState currentMeta = ResolveMeta();
        if (currentSession == null)
        {
            statusMessage = "Сессия мира не найдена.";
            RefreshState(true);
            return false;
        }

        if (currentMeta != null)
        {
            string sortieName = currentMeta.GetSelectedSessionSortieDisplayName();
            if (!currentMeta.CanBeginSelectedSessionSortie(out statusMessage))
            {
                RefreshState(true);
                return false;
            }

            bool result = currentMeta.BeginSelectedSessionSortie();
            statusMessage = result ? sortieName + " started." : sortieName + " blocked.";
            currentSession.RefreshFromMetaProgress();
            if (result)
            {
                ResolveSessionCameraController()?.FocusOnPlayerShipAfterUndocking();
            }

            RefreshState(true);
            return result;
        }

        statusMessage = "Session runtime is unavailable.";
        RefreshState(true);
        return false;
    }

    public bool TryCycleSessionSortie()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta == null)
        {
            statusMessage = "Session runtime is unavailable.";
            RefreshState(true);
            return false;
        }

        bool result = currentMeta.SelectNextSessionSortie(out statusMessage);
        RefreshState(true);
        return result;
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

    public bool TryRunBaseWorkAction()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (currentMeta != null)
        {
            bool result = currentMeta.CanInstallNextStarterFittingUpgrade(out _)
                ? currentMeta.TryInstallNextStarterFittingUpgrade(out statusMessage)
                : currentMeta.CanUpgradeNextBaseIndustryLine(out _)
                ? currentMeta.TryUpgradeNextBaseIndustryLine(out statusMessage)
                : currentMeta.TryRunNextBaseCascadeOrder(out statusMessage);
            RefreshState(true);
            return result;
        }

        statusMessage = "Session runtime is unavailable.";
        RefreshState(true);
        return false;
    }

    public void RefreshCompassForTests()
    {
        RefreshCompass();
    }

    public void OpenMenu()
    {
        WildWindGameplayMenu menu = Object.FindFirstObjectByType<WildWindGameplayMenu>();
        if (menu != null)
        {
            menu.SetOpen(true);
        }
    }

    public bool SelectMetaPortTabForTests(WildWindMetaPortTab tab)
    {
        return SelectMetaPortTab(tab);
    }

    public bool RunMetaPortPrimaryActionForTests()
    {
        return RunMetaPortPrimaryAction();
    }

    public bool RunMetaPortSecondaryActionForTests()
    {
        return RunMetaPortSecondaryAction();
    }

    public bool SelectNextMetaPortShipForTests()
    {
        return SelectNextMetaPortShip();
    }

    public bool SelectPreviousMetaPortShipForTests()
    {
        return SelectPreviousMetaPortShip();
    }

    public bool ToggleMetaPortScreenForTests()
    {
        return ToggleMetaPortScreen();
    }

    public bool OpenMetaPortScreenForTests()
    {
        return OpenMetaPortScreen();
    }

    private void BuildUi()
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 850;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateRect("Gameplay HUD Root", canvas.transform, StretchFull()).GetComponent<RectTransform>();

        RectTransform topBand = CreatePanel("Session Status Strip", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -24f),
            sizeDelta = new Vector2(-80f, 92f)
        }, new Color(0.025f, 0.026f, 0.028f, 0.82f));

        modeText = CreateText(topBand, "", 28, new Vector2(24f, -12f), new Vector2(240f, 34f), TextAnchor.MiddleLeft, new Color(0.95f, 0.81f, 0.52f, 1f));
        objectiveText = CreateText(topBand, "", 24, new Vector2(280f, -14f), new Vector2(760f, 32f), TextAnchor.MiddleLeft, new Color(0.91f, 0.88f, 0.78f, 1f));
        sessionPathText = CreateText(topBand, "", 20, new Vector2(280f, -50f), new Vector2(760f, 28f), TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 0.82f, 1f));
        distanceText = CreateText(topBand, "", 22, new Vector2(1480f, -30f), new Vector2(300f, 34f), TextAnchor.MiddleRight, new Color(0.86f, 0.80f, 0.64f, 1f));

        BuildCompass();

        dockedPanel = CreatePanel("Docked Panel", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = new Vector2(42f, 42f),
            sizeDelta = new Vector2(920f, 342f)
        }, new Color(0.026f, 0.024f, 0.022f, 0.84f));

        baseInputsText = CreateText(dockedPanel, "", 21, new Vector2(24f, -22f), new Vector2(460f, 30f), TextAnchor.MiddleLeft, new Color(0.88f, 0.83f, 0.68f, 1f));
        baseMaterialsText = CreateText(dockedPanel, "", 21, new Vector2(24f, -58f), new Vector2(460f, 30f), TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 0.82f, 1f));
        shipSuppliesText = CreateText(dockedPanel, "", 21, new Vector2(24f, -94f), new Vector2(460f, 30f), TextAnchor.MiddleLeft, new Color(0.91f, 0.88f, 0.78f, 1f));
        fittingText = CreateText(dockedPanel, "", 17, new Vector2(24f, -121f), new Vector2(460f, 22f), TextAnchor.MiddleLeft, new Color(0.74f, 0.84f, 0.86f, 1f));
        baseProcessingText = CreateText(dockedPanel, "", 15, new Vector2(512f, -22f), new Vector2(380f, 86f), TextAnchor.UpperLeft, new Color(0.83f, 0.84f, 0.72f, 1f));
        baseCascadeText = CreateText(dockedPanel, "", 15, new Vector2(512f, -116f), new Vector2(380f, 106f), TextAnchor.UpperLeft, new Color(0.72f, 0.84f, 0.86f, 1f));
        baseCascadeOrderText = CreateText(dockedPanel, "", 15, new Vector2(512f, -232f), new Vector2(380f, 84f), TextAnchor.UpperLeft, new Color(0.91f, 0.84f, 0.62f, 1f));
        processBaseText = CreateButton(dockedPanel, "Process", new Vector2(24f, -144f), new Vector2(220f, 54f), TryRunBaseProcessAction, out processBaseButton);
        takeoffText = CreateButton(dockedPanel, "Takeoff", new Vector2(264f, -144f), new Vector2(220f, 54f), TryTakeOff, out takeoffButton);
        runBaseWorkText = CreateButton(dockedPanel, "Base work", new Vector2(24f, -206f), new Vector2(220f, 54f), TryRunBaseWorkAction, out runBaseWorkButton);
        sortieText = CreateButton(dockedPanel, "Next Sortie", new Vector2(264f, -206f), new Vector2(220f, 54f), TryCycleSessionSortie, out sortieButton);
        portText = CreateButton(dockedPanel, "Port", new Vector2(24f, -268f), new Vector2(220f, 42f), ToggleMetaPortScreen, out portButton);
        menuText = CreateButton(dockedPanel, "Menu", new Vector2(264f, -268f), new Vector2(220f, 42f), () => { OpenMenu(); return true; }, out menuButton);

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
        afterburnerText = CreateButton(flightPanel, "Afterburner", new Vector2(1520f, -86f), new Vector2(180f, 36f), ToggleAfterburner, out _);
        claudiumSlipstreamText = CreateButton(flightPanel, "Claudium Slipstream", new Vector2(1520f, -128f), new Vector2(180f, 36f), ToggleClaudiumSlipstream, out _);

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
    }

    private void BuildHudWindows()
    {
        windowDockPanel = CreatePanel("Window Dock", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0.5f),
            anchorMax = new Vector2(0f, 0.5f),
            pivot = new Vector2(0f, 0.5f),
            anchoredPosition = new Vector2(24f, 0f),
            sizeDelta = new Vector2(116f, 456f)
        }, new Color(0.012f, 0.014f, 0.017f, 0.74f));

        CreateWindowDockButton(WindowLocationId, "Где я", 14f);
        CreateWindowDockButton(WindowReturnId, "Возврат", -28f);
        CreateWindowDockButton(WindowShipId, "Корабль", -70f);
        CreateWindowDockButton(WindowTasksId, "Задачи", -112f);
        CreateWindowDockButton(WindowCargoId, "Трюм", -154f);
        CreateWindowDockButton(WindowRadarId, "Радар", -196f);
        CreateWindowDockButton(WindowModulesId, "Модули", -238f);
        CreateWindowDockButton(WindowMetaPortId, "PORT", -280f);

        locationWindow = CreateHudWindow(WindowLocationId, "ГДЕ Я", new Vector2(154f, -136f), new Vector2(430f, 178f), true);
        locationWindowText = CreateText(locationWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(390f, 124f), TextAnchor.UpperLeft, new Color(0.82f, 0.90f, 0.92f, 1f));

        returnWindow = CreateHudWindow(WindowReturnId, "ВОЗВРАТ", new Vector2(1450f, -136f), new Vector2(420f, 188f), true);
        returnWindowText = CreateText(returnWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(386f, 134f), TextAnchor.UpperLeft, new Color(0.90f, 0.86f, 0.70f, 1f));

        shipWindow = CreateHudWindow(WindowShipId, "КОРАБЛЬ", new Vector2(154f, -326f), new Vector2(430f, 198f), true);
        shipWindowText = CreateText(shipWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(390f, 144f), TextAnchor.UpperLeft, new Color(0.84f, 0.90f, 0.86f, 1f));

        tasksWindow = CreateHudWindow(WindowTasksId, "ЗАДАНИЯ", new Vector2(154f, -538f), new Vector2(430f, 168f), true);
        tasksWindowText = CreateText(tasksWindow.content, "", 16, new Vector2(12f, -12f), new Vector2(390f, 114f), TextAnchor.UpperLeft, new Color(0.92f, 0.84f, 0.60f, 1f));

        radarWindow = CreateHudWindow(WindowRadarId, "РАДАР", new Vector2(1390f, -338f), new Vector2(480f, 312f), true);
        radarBoulderFilterText = CreateButton(radarWindow.content, "Radar Boulders", new Vector2(12f, -10f), new Vector2(96f, 28f), ToggleRadarBoulders, out _);
        radarDebrisFilterText = CreateButton(radarWindow.content, "Radar Debris", new Vector2(116f, -10f), new Vector2(104f, 28f), ToggleRadarDebris, out _);
        radarWindowText = CreateText(radarWindow.content, "", 14, new Vector2(12f, -48f), new Vector2(430f, 212f), TextAnchor.UpperLeft, new Color(0.80f, 0.88f, 0.90f, 1f));

        modulesWindow = CreateHudWindow(WindowModulesId, "МОДУЛИ", new Vector2(620f, -740f), new Vector2(570f, 178f), true);
        modulesWindowText = CreateText(modulesWindow.content, "", 15, new Vector2(12f, -12f), new Vector2(532f, 126f), TextAnchor.UpperLeft, new Color(0.88f, 0.84f, 0.66f, 1f));

        metaPortWindow = CreateHudWindow(WindowMetaPortId, "META PORT", new Vector2(210f, -104f), new Vector2(1460f, 760f), false);
        metaPortResourceText = CreateText(metaPortWindow.content, "", 16, new Vector2(12f, -8f), new Vector2(1400f, 28f), TextAnchor.MiddleLeft, new Color(0.96f, 0.84f, 0.54f, 1f));
        CreateButton(metaPortWindow.content, "< Ship", new Vector2(12f, -46f), new Vector2(120f, 30f), SelectPreviousMetaPortShip, out _);
        CreateButton(metaPortWindow.content, "Ship >", new Vector2(140f, -46f), new Vector2(120f, 30f), SelectNextMetaPortShip, out _);
        metaPortShipText = CreateText(metaPortWindow.content, "", 15, new Vector2(284f, -42f), new Vector2(1120f, 74f), TextAnchor.UpperLeft, new Color(0.82f, 0.90f, 0.92f, 1f));

        metaPortTabTexts = new Text[WildWindMetaPortUiState.Tabs.Count];
        int tabColumnCount = 3;
        int rowsPerTabColumn = Mathf.CeilToInt(WildWindMetaPortUiState.Tabs.Count / (float)tabColumnCount);
        for (int i = 0; i < WildWindMetaPortUiState.Tabs.Count; i++)
        {
            int column = i / rowsPerTabColumn;
            int row = i % rowsPerTabColumn;
            WildWindMetaPortTab tab = WildWindMetaPortUiState.Tabs[i];
            metaPortTabTexts[i] = CreateButton(
                metaPortWindow.content,
                "Meta Tab " + tab,
                new Vector2(12f + column * 130f, -126f - row * 31f),
                new Vector2(122f, 26f),
                () => SelectMetaPortTab(tab),
                out _);
        }

        metaPortContentText = CreateText(metaPortWindow.content, "", 15, new Vector2(420f, -126f), new Vector2(990f, 476f), TextAnchor.UpperLeft, new Color(0.86f, 0.89f, 0.82f, 1f));
        metaPortStatusText = CreateText(metaPortWindow.content, "", 14, new Vector2(420f, -612f), new Vector2(990f, 36f), TextAnchor.UpperLeft, new Color(0.95f, 0.76f, 0.42f, 1f));
        metaPortPrimaryActionText = CreateButton(metaPortWindow.content, "Meta Primary", new Vector2(420f, -662f), new Vector2(204f, 36f), RunMetaPortPrimaryAction, out _);
        metaPortSecondaryActionText = CreateButton(metaPortWindow.content, "Meta Secondary", new Vector2(636f, -662f), new Vector2(204f, 36f), RunMetaPortSecondaryAction, out _);

        cargoWindow = CreateHudWindow(WindowCargoId, "ТРЮМ", new Vector2(790f, -336f), new Vector2(520f, 342f), false);
        cargoHeaderText = CreateText(cargoWindow.content, "", 16, new Vector2(12f, -10f), new Vector2(470f, 26f), TextAnchor.MiddleLeft, new Color(0.84f, 0.90f, 0.86f, 1f));
        BuildCargoGrid(cargoWindow.content);

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

        engineAfterburnerMarker = CreateRect("Afterburner Marker", enginePowerBar, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0.5f),
            anchoredPosition = new Vector2(0f, EnginePowerBarHeight / EngineAfterburnerPowerMultiplier),
            sizeDelta = new Vector2(8f, 3f)
        }).GetComponent<RectTransform>();

        Image markerImage = engineAfterburnerMarker.gameObject.AddComponent<Image>();
        markerImage.color = new Color(0.92f, 0.08f, 0.055f, 1f);

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
        text.text = initialText ?? "";
        return text;
    }

    private void RefreshTexts()
    {
        SetText(processBaseText, WildWindLocalization.Get("game.hud.process_base"));
        SetText(takeoffText, WildWindLocalization.Get("game.hud.takeoff"));
        SetText(dockText, WildWindLocalization.Get("game.hud.dock"));
        SetText(runBaseWorkText, WildWindLocalization.Get("game.hud.run_base_work"));
        SetText(sortieText, "Next sortie");
        SetText(portText, "Port");
        SetText(menuText, WildWindLocalization.Get("game.hud.menu"));
        SetText(flightMenuText, WildWindLocalization.Get("game.hud.menu"));
        RefreshAfterburnerText();
        RefreshClaudiumSlipstreamText();
        RefreshState(true);
    }

    private void RefreshState(bool force)
    {
        MetaGameState currentMeta = ResolveMeta();
        WildWindGameplaySession currentSession = ResolveSession();
        if (currentMeta == null || currentMeta.progress == null)
        {
            if (sortiePanel != null)
            {
                sortiePanel.gameObject.SetActive(false);
            }

            SetHudWindowLayerVisible(false);
            SetRadarSessionOverlayVisible(false);
            return;
        }

        currentMeta.EnsureProgressInitialized();
        PlayerProgress progress = currentMeta.progress;
        bool docked = currentMeta.CurrentMode == GameSessionMode.Docked;
        bool dockedAtBase = docked && currentMeta.IsDockedAtCapital();
        float distance = 0f;
        PortStorageState baseStorage = currentMeta.GetCapitalStorageState();
        SortieReturnEstimate sortieEstimate = currentMeta.HasActiveSortie
            ? currentMeta.GetActiveSortieReturnEstimate()
            : default;
        string selectedSortieName = currentMeta.GetSelectedSessionSortieDisplayName();
        string selectedSortieRequirement = currentMeta.GetSelectedSessionSortieRequirementText();
        string selectedSortieBlocker = "";
        bool canBeginSelectedSortie = currentMeta.CanBeginSelectedSessionSortie(out selectedSortieBlocker);

        BaseProcessingBranch nextProcessingBranch = BaseProcessingBranch.Ore;
        bool hasProcessableBaseBatch = currentMeta.TryGetNextProcessableBaseBranch(out nextProcessingBranch);
        bool canRefuelBaseShip = currentMeta.CanRefuelBaseShip(out _);
        bool canLoadStarterMunitions = currentMeta.CanLoadStarterMunitionsAtBase(out _);
        bool canInstallStarterFitting = currentMeta.CanInstallNextStarterFittingUpgrade(out _);
        bool canUpgradeBaseIndustry = !canInstallStarterFitting && currentMeta.CanUpgradeNextBaseIndustryLine(out _);
        CascadeProductionOrderDefinition nextCascadeOrder = null;
        CascadeProductionEstimate nextCascadeEstimate = !canInstallStarterFitting
            ? currentMeta.EstimateNextBaseCascadeOrder(out nextCascadeOrder)
            : null;
        string processButtonText = hasProcessableBaseBatch
            ? "Process " + SessionExtractionIndustry.GetProcessingDisplayName(nextProcessingBranch)
            : canRefuelBaseShip
            ? "Refuel"
            : canLoadStarterMunitions
            ? "Load munitions"
            : "Process batch";
        string cascadeButtonText = nextCascadeEstimate != null && nextCascadeEstimate.canRun
            ? "Run " + ShortenButtonLabel(nextCascadeOrder != null ? nextCascadeOrder.displayName : "cascade", 14)
                + " " + nextCascadeEstimate.bottleneckMinutes.ToString("0.0") + "m"
            : "Run cascade";

        dockedPanel.gameObject.SetActive(docked);
        flightPanel.gameObject.SetActive(!docked);
        compassPanel.gameObject.SetActive(!docked);
        SetHudWindowLayerVisible(!docked);
        if (docked)
        {
            SetRadarSessionOverlayVisible(false);
        }

        SetText(modeText, WildWindLocalization.Get(docked ? "game.hud.city" : "game.hud.flight"));
        SetText(objectiveText, "Session extraction");
        SetText(sessionPathText, currentMeta.HasActiveSortie
                ? currentMeta.ActiveSortie.zone.displayName + " -> manual extraction"
                : "Base -> " + selectedSortieName + " (" + selectedSortieRequirement + ")");
        SetText(baseInputsText, "Inputs: " + GetStoredCoreInputsText(baseStorage));
        SetText(baseMaterialsText, "Materials: ferron " + GetStorageAmount(baseStorage, "ferron")
                + ", silvate " + GetStorageAmount(baseStorage, "silvate")
                + ", kits " + GetStorageAmount(baseStorage, SessionExtractionConstants.StarterAirframeKitItemId)
                + "/" + GetStorageAmount(baseStorage, SessionExtractionConstants.StarterModuleKitItemId)
                + "/" + GetStorageAmount(baseStorage, SessionExtractionConstants.StarterMunitionBundleItemId));
        SetText(shipSuppliesText, "Tanks: " + currentMeta.GetBaseRefuelStatusText()
                + " | weapon " + progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId));
        SetText(fittingText, currentMeta.GetCoreFittingCompactText());
        SetText(baseProcessingText, currentMeta.GetBaseProcessingOverviewText());
        SetText(baseCascadeText, currentMeta.GetBaseCascadeProductionOverviewText());
        SetText(baseCascadeOrderText, currentMeta.GetNextBaseIndustryUpgradeOverviewText() + "\n" + currentMeta.GetNextBaseCascadeOrderOverviewText());
        SetText(processBaseText, processButtonText);
        SetText(takeoffText, "Start " + ShortenButtonLabel(selectedSortieName, 16));
        SetText(dockText, "Extract home");
        SetText(runBaseWorkText, canInstallStarterFitting
                ? currentMeta.GetNextStarterFittingUpgradeActionLabel()
                : canUpgradeBaseIndustry
                ? "Upgrade base"
                : cascadeButtonText);
        SetText(sortieText, "Next sortie");
        SetText(portText, "Port");
        WildWindFlightControlBridge controls = ResolveFlightControls();
        float currentAltitude = GetPlayerPosition(currentSession).y;
        if (sortiePanel != null)
        {
            sortiePanel.gameObject.SetActive(false);
        }

        RefreshHudWindows(currentMeta, currentSession, progress, controls, sortieEstimate, !docked);
        RefreshMetaPortWindow();
        SetText(distanceText, currentMeta.HasActiveSortie
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
        else if (dockedAtBase && !canBeginSelectedSortie && string.IsNullOrWhiteSpace(visibleStatus))
        {
            visibleStatus = selectedSortieBlocker;
        }
        else if (!docked && currentMeta.HasActiveSortie && string.IsNullOrWhiteSpace(visibleStatus))
        {
            visibleStatus = sortieEstimate.status;
        }

        SetText(statusText, visibleStatus);
        RefreshEnginePowerBar();

        if (controls != null)
        {
            RefreshAfterburnerText();
            RefreshClaudiumSlipstreamText();
        }

        SetInteractable(processBaseButton, dockedAtBase && (hasProcessableBaseBatch || canRefuelBaseShip || canLoadStarterMunitions));
        SetInteractable(takeoffButton, docked && dockedAtBase && canBeginSelectedSortie);
        SetInteractable(dockButton, !docked && currentMeta.HasActiveSortie && sortieEstimate.canExtract);
        SetInteractable(runBaseWorkButton, dockedAtBase && (canInstallStarterFitting || canUpgradeBaseIndustry || (nextCascadeEstimate != null && nextCascadeEstimate.canRun)));
        SetInteractable(sortieButton, dockedAtBase);
        SetInteractable(portButton, docked);
        SetInteractable(menuButton, true);
    }

    private void SetHudWindowLayerVisible(bool visible)
    {
        if (windowDockPanel != null)
        {
            windowDockPanel.gameObject.SetActive(visible);
        }

        foreach (KeyValuePair<string, HudWindow> pair in hudWindows)
        {
            bool keepDockedPortVisible = pair.Key == WindowMetaPortId && pair.Value.IsOpen;
            pair.Value.SetFlightVisible(visible || keepDockedPortVisible);
        }
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

    private bool ToggleMetaPortWindow()
    {
        return ToggleHudWindow(WindowMetaPortId);
    }

    private bool ToggleMetaPortScreen()
    {
        WildWindMetaPortScreen screen = EnsureFullMetaPortScreen();
        bool visible = !screen.IsVisible;
        screen.SetVisible(visible);
        statusMessage = visible ? "Port screen opened." : "Port screen closed.";
        RefreshState(true);
        return true;
    }

    private bool OpenMetaPortScreen()
    {
        WildWindMetaPortScreen screen = EnsureFullMetaPortScreen();
        screen.SetVisible(true);
        statusMessage = "Port screen opened.";
        RefreshState(true);
        return true;
    }

    private void OpenMetaPortOnDockedStartIfReady()
    {
        if (!openMetaPortOnDockedStart)
        {
            return;
        }

        MetaGameState currentMeta = ResolveMeta();
        WildWindGameplaySession currentSession = ResolveSession();
        if (currentMeta == null || currentSession == null || !currentMeta.IsDocked)
        {
            return;
        }

        openMetaPortOnDockedStart = false;
        OpenMetaPortScreen();
    }

    private WildWindMetaPortScreen EnsureFullMetaPortScreen()
    {
        if (fullMetaPortScreen != null)
        {
            fullMetaPortScreen.BindState(EnsureMetaPortState());
            return fullMetaPortScreen;
        }

        fullMetaPortScreenObject = new GameObject("Wild Wind Meta Port Screen");
        fullMetaPortScreenObject.transform.SetParent(transform, false);
        fullMetaPortScreen = fullMetaPortScreenObject.AddComponent<WildWindMetaPortScreen>();
        fullMetaPortScreen.BindState(EnsureMetaPortState());
        fullMetaPortScreen.SetCloseAction(() =>
        {
            fullMetaPortScreen.SetVisible(false);
            statusMessage = "Port screen closed.";
            RefreshState(true);
            return true;
        });
        fullMetaPortScreen.SetVisible(false);
        return fullMetaPortScreen;
    }

    private bool SelectMetaPortTab(WildWindMetaPortTab tab)
    {
        EnsureMetaPortState().SelectTab(tab);
        if (fullMetaPortScreen != null)
        {
            fullMetaPortScreen.BindState(EnsureMetaPortState());
        }

        RefreshState(true);
        return true;
    }

    private bool RunMetaPortPrimaryAction()
    {
        WildWindMetaPortActionResult result = EnsureMetaPortState().RunPrimaryAction();
        statusMessage = result.message;
        if (fullMetaPortScreen != null)
        {
            fullMetaPortScreen.BindState(EnsureMetaPortState());
        }

        RefreshState(true);
        return result.success;
    }

    private bool RunMetaPortSecondaryAction()
    {
        WildWindMetaPortActionResult result = EnsureMetaPortState().RunSecondaryAction();
        statusMessage = result.message;
        if (fullMetaPortScreen != null)
        {
            fullMetaPortScreen.BindState(EnsureMetaPortState());
        }

        RefreshState(true);
        return result.success;
    }

    private bool SelectNextMetaPortShip()
    {
        bool changed = EnsureMetaPortState().SelectNextShip();
        if (fullMetaPortScreen != null)
        {
            fullMetaPortScreen.BindState(EnsureMetaPortState());
        }

        RefreshState(true);
        return changed;
    }

    private bool SelectPreviousMetaPortShip()
    {
        bool changed = EnsureMetaPortState().SelectPreviousShip();
        if (fullMetaPortScreen != null)
        {
            fullMetaPortScreen.BindState(EnsureMetaPortState());
        }

        RefreshState(true);
        return changed;
    }

    private void RefreshMetaPortWindow()
    {
        if (metaPortWindow == null)
        {
            return;
        }

        WildWindMetaPortUiState state = EnsureMetaPortState();
        SetText(metaPortResourceText, state.BuildResourceStrip());
        SetText(metaPortShipText, state.BuildShipCarousel() + "\n" + state.BuildSelectedShipSummary());
        SetText(metaPortContentText, state.BuildTabContent());
        SetText(metaPortStatusText, state.BuildReport());
        SetText(metaPortPrimaryActionText, state.GetPrimaryActionLabel());
        SetText(metaPortSecondaryActionText, state.GetSecondaryActionLabel());

        if (metaPortTabTexts == null)
        {
            return;
        }

        for (int i = 0; i < metaPortTabTexts.Length && i < WildWindMetaPortUiState.Tabs.Count; i++)
        {
            WildWindMetaPortTab tab = WildWindMetaPortUiState.Tabs[i];
            string prefix = tab == state.selectedTab ? "[x] " : "[ ] ";
            SetText(metaPortTabTexts[i], prefix + WildWindMetaPortUiState.GetTabDisplayName(tab));
        }
    }

    private WildWindMetaPortUiState EnsureMetaPortState()
    {
        MetaGameState currentMeta = ResolveMeta();
        if (metaPortState == null)
        {
            metaPortState = WildWindMetaPortUiState.CreateFromRuntime(currentMeta);
        }
        else
        {
            metaPortState.BindRuntimeMeta(currentMeta);
        }

        return metaPortState;
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

        float fuelRate = ship != null ? ship.engineFuelConsumptionKgPerSecond : 0f;
        float claudiumRate = ship != null
            ? ship.claudiumConsumptionPerTonSecond * ship.ClaudiumSlipstreamClaudiumMultiplier * Mathf.Max(0f, ship.GetTotalMassKg() / 1000f)
            : 0f;
        bool hasReturnFuel = !sortieEstimate.hasActiveSortie || sortieEstimate.hasEnoughCoal;
        bool hasReturnClaudium = !sortieEstimate.hasActiveSortie || sortieEstimate.hasEnoughClaudium;
        SetText(returnWindowText,
            "Уголь: " + FormatReserveTime(sortieEstimate.currentCoalKg, fuelRate) + " осталось"
            + "\nКлавдий: " + FormatReserveTime(sortieEstimate.currentClaudiumKg, claudiumRate) + " осталось"
            + "\nДо базы нужно: уголь " + sortieEstimate.requiredCoalKg.ToString("0") + " кг"
            + " | клавдий " + sortieEstimate.requiredClaudiumKg.ToString("0") + " кг"
            + "\nВыход: " + (sortieEstimate.canExtract ? "готов" : FormatExtractionBlocker(sortieEstimate))
            + "\nСкольжение: " + sortieEstimate.extractionRunupSeconds.ToString("0.0") + "/" + sortieEstimate.requiredExtractionRunupSeconds.ToString("0.0") + " с");
        if (returnWindowText != null)
        {
            returnWindowText.color = hasReturnFuel && hasReturnClaudium
                ? new Color(0.90f, 0.86f, 0.70f, 1f)
                : new Color(1f, 0.32f, 0.24f, 1f);
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
        string activeBur = "БУР: " + (ship != null ? "готов " + MakeAsciiBar(0.63f, 10) : "-");
        string activeWeapon = "ОРУЖИЕ: " + (progress != null ? progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId) + " ед." : "-");
        string passive = "Пассивные: ГРУЗ | БРОНЯ | ДВИГ | РИГ";
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
        SetText(sortieReservesText, "Reserves: coal "
            + FormatReserve(sortieEstimate.currentCoalKg, sortieEstimate.requiredCoalKg)
            + " | claudium "
            + FormatReserve(sortieEstimate.currentClaudiumKg, sortieEstimate.requiredClaudiumKg));
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
            sortieReservesText.color = sortieEstimate.hasEnoughCoal && sortieEstimate.hasEnoughClaudium
                ? new Color(0.84f, 0.90f, 0.76f, 1f)
                : warningColor;
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
        if (ship == null || ship.enginePowerKwAt100 <= 0f)
        {
            SetPowerFill(engineLiftPowerFill, 0f, 0f);
            SetPowerFill(engineModulePowerFill, 0f, 0f);
            SetPowerFill(engineThrustPowerFill, 0f, 0f);
            SetText(enginePowerText, "PWR 0 / 0 kW");
            return;
        }

        float displayPowerMultiplier = EngineAfterburnerPowerMultiplier;
        float maxPowerKw = Mathf.Max(0.001f, ship.enginePowerKwAt100 * displayPowerMultiplier);
        float liftPowerKw = Mathf.Clamp(ship.claudiumPowerDrawKw, 0f, maxPowerKw);
        float modulePowerKw = Mathf.Clamp(GetShipModulePowerKw(ship), 0f, maxPowerKw - liftPowerKw);
        float thrustPowerKw = Mathf.Clamp(ship.propellerInputPowerKw, 0f, maxPowerKw - liftPowerKw - modulePowerKw);
        float liftHeight = EnginePowerBarHeight * liftPowerKw / maxPowerKw;
        float moduleHeight = EnginePowerBarHeight * modulePowerKw / maxPowerKw;
        float thrustHeight = EnginePowerBarHeight * thrustPowerKw / maxPowerKw;

        SetPowerFill(engineLiftPowerFill, 0f, liftHeight);
        SetPowerFill(engineModulePowerFill, liftHeight, moduleHeight);
        SetPowerFillColor(engineThrustPowerFill,
            ship.propellerPitch < -0.001f
                ? EngineReverseThrustColor
                : EngineForwardThrustColor);
        SetPowerFill(engineThrustPowerFill, liftHeight + moduleHeight, thrustHeight);
        RefreshThrottleGearSelector();

        if (engineAfterburnerMarker != null)
        {
            engineAfterburnerMarker.anchoredPosition = new Vector2(
                0f,
                EnginePowerBarHeight / displayPowerMultiplier);
        }

        float usedPowerKw = liftPowerKw + modulePowerKw + thrustPowerKw;
        SetText(enginePowerText, "PWR " + usedPowerKw.ToString("0") + " / " + maxPowerKw.ToString("0") + " kW");
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

    private static string GetStoredOreText(PortStorageState storage)
    {
        int windshale = GetStorageAmount(storage, "windshale_ore");
        int dawnspar = GetStorageAmount(storage, "dawnspar_ore");
        int bluebrass = GetStorageAmount(storage, "bluebrass_ore");
        int total = windshale + dawnspar + bluebrass;
        if (total <= 0) return "0";

        return "windshale " + windshale + ", dawnspar " + dawnspar + ", bluebrass " + bluebrass;
    }

    private static string GetStoredCoreInputsText(PortStorageState storage)
    {
        int ore = SumStorageAmounts(storage, "windshale_ore", "dawnspar_ore", "bluebrass_ore");
        int gas = SumStorageAmounts(storage, "mist_condensate", "cloud_condensate", "wet_condensate", "storm_condensate");
        int leviathan = SumStorageAmounts(storage, "windcalf_carcass", "windminnow_carcass", "mistwhale_carcass");
        int info = SumStorageAmounts(storage, SessionExtractionConstants.RockInfoItemId);
        int automaton = GetStorageAmount(storage, SessionExtractionConstants.BrokenAutomatonItemId);

        return "ore " + ore
            + ", gas " + gas
            + ", auto " + automaton
            + ", lev " + leviathan
            + ", info " + info;
    }

    private static bool HasStoredOre(PortStorageState storage)
    {
        return GetStorageAmount(storage, "windshale_ore") > 0
            || GetStorageAmount(storage, "dawnspar_ore") > 0
            || GetStorageAmount(storage, "bluebrass_ore") > 0;
    }

    private static int SumStorageAmounts(PortStorageState storage, params string[] itemIds)
    {
        if (itemIds == null) return 0;

        int total = 0;
        for (int i = 0; i < itemIds.Length; i++)
        {
            total += GetStorageAmount(storage, itemIds[i]);
        }

        return total;
    }

    private static int GetStorageAmount(PortStorageState storage, string itemId)
    {
        return storage != null && !string.IsNullOrWhiteSpace(itemId) ? storage.GetResourceAmount(itemId) : 0;
    }

    private static float GetShipModulePowerKw(ShipPhysics ship)
    {
        if (ship == null)
        {
            return 0f;
        }

        return 0f;
    }

    private bool ToggleAfterburner()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls != null)
        {
            controls.ToggleAfterburner();
            statusMessage = controls.StatusMessage;
            RefreshAfterburnerText();
            RefreshClaudiumSlipstreamText();
            RefreshState(true);
            return true;
        }

        ShipPhysics ship = GetHudShip();
        if (ship == null)
        {
            return false;
        }

        ship.engineAfterburnerEnabled = !ship.engineAfterburnerEnabled;
        ship.enginePowerLever = Mathf.Min(ship.enginePowerLever, ship.EnginePowerLeverLimit);

        statusMessage = ship.engineAfterburnerEnabled
            ? "Afterburner 120% enabled."
            : "Afterburner disabled.";
        RefreshAfterburnerText();
        RefreshClaudiumSlipstreamText();
        RefreshState(true);
        return true;
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

    private void RefreshAfterburnerText()
    {
        ShipPhysics ship = GetHudShip();
        bool enabled = ship != null && ship.engineAfterburnerEnabled;
        string state = WildWindLocalization.Get(enabled ? "game.hud.on" : "game.hud.off");
        SetText(afterburnerText, "Форсаж 120%: " + state);
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
            meta = Object.FindFirstObjectByType<MetaGameState>();
        }

        return meta;
    }

    private WildWindGameplaySession ResolveSession()
    {
        if (session == null)
        {
            session = Object.FindFirstObjectByType<WildWindGameplaySession>();
        }

        return session;
    }

    private WildWindSessionCameraController ResolveSessionCameraController()
    {
        if (sessionCameraController == null)
        {
            sessionCameraController = Object.FindFirstObjectByType<WildWindSessionCameraController>();
        }

        return sessionCameraController;
    }

    private WildWindFlightControlBridge ResolveFlightControls()
    {
        if (flightControls == null)
        {
            flightControls = Object.FindFirstObjectByType<WildWindFlightControlBridge>();
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
        if (!estimate.hasEnoughCoal && !estimate.hasEnoughClaudium) return "reserve short";
        if (!estimate.hasEnoughCoal) return "coal short";
        if (!estimate.hasEnoughClaudium) return "claudium short";
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
            return "engine -";
        }

        string mode = ship.engineAfterburnerEnabled
            ? "120%"
            : "normal";
        return "engine " + mode + ", T " + (ship.thrustInput * 100f).ToString("0") + "%";
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

    private sealed class HudWindow
    {
        private const float HeaderHeight = 34f;
        private readonly Vector2 minSize = new Vector2(260f, 96f);
        private bool flightVisible = true;
        private bool minimized;
        private Vector2 expandedSize;

        public RectTransform root;
        public RectTransform content;
        public bool IsOpen { get; private set; }

        public static HudWindow Create(
            WildWindGameplayHud owner,
            RectTransform parent,
            string id,
            string title,
            Vector2 position,
            Vector2 size,
            bool openByDefault)
        {
            HudWindow window = new HudWindow
            {
                IsOpen = openByDefault,
                expandedSize = size
            };

            window.root = owner.CreatePanel("HUD Window " + id, parent, new RectTransformSpec
            {
                anchorMin = new Vector2(0f, 1f),
                anchorMax = new Vector2(0f, 1f),
                pivot = new Vector2(0f, 1f),
                anchoredPosition = position,
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

            HudWindowDragHandle drag = header.gameObject.AddComponent<HudWindowDragHandle>();
            drag.target = window.root;
            drag.canvas = owner.canvas;

            Text titleText = owner.CreateText(header, title, 16, new Vector2(10f, -3f), new Vector2(size.x - 90f, 28f), TextAnchor.MiddleLeft, new Color(0.95f, 0.82f, 0.55f, 1f));
            titleText.fontStyle = FontStyle.Bold;
            titleText.raycastTarget = false;

            CreateHeaderButton(owner, header, "-", new Vector2(-58f, -4f), () => window.ToggleMinimized());
            CreateHeaderButton(owner, header, "x", new Vector2(-30f, -4f), () => window.SetOpen(false));

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

            RectTransform resize = CreateRect("Resize", window.root, new RectTransformSpec
            {
                anchorMin = new Vector2(1f, 0f),
                anchorMax = new Vector2(1f, 0f),
                pivot = new Vector2(1f, 0f),
                anchoredPosition = new Vector2(-2f, 2f),
                sizeDelta = new Vector2(18f, 18f)
            }).GetComponent<RectTransform>();
            Image resizeImage = resize.gameObject.AddComponent<Image>();
            resizeImage.color = new Color(0.72f, 0.58f, 0.28f, 0.60f);
            HudWindowResizeHandle resizeHandle = resize.gameObject.AddComponent<HudWindowResizeHandle>();
            resizeHandle.target = window.root;
            resizeHandle.canvas = owner.canvas;
            resizeHandle.minSize = window.minSize;

            window.RefreshVisible();
            return window;
        }

        public void SetFlightVisible(bool visible)
        {
            flightVisible = visible;
            RefreshVisible();
        }

        public void ToggleOpen()
        {
            IsOpen = !IsOpen;
            RefreshVisible();
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            RefreshVisible();
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;
            if (minimized)
            {
                expandedSize = root.sizeDelta;
                root.sizeDelta = new Vector2(root.sizeDelta.x, HeaderHeight);
            }
            else
            {
                root.sizeDelta = new Vector2(Mathf.Max(minSize.x, expandedSize.x), Mathf.Max(minSize.y, expandedSize.y));
            }

            if (content != null)
            {
                content.gameObject.SetActive(!minimized);
            }
        }

        private void RefreshVisible()
        {
            if (root != null)
            {
                root.gameObject.SetActive(flightVisible && IsOpen);
            }
        }

        private static Text CreateHeaderButton(WildWindGameplayHud owner, RectTransform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
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
            return text;
        }
    }

    private sealed class HudWindowDragHandle : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform target;
        public Canvas canvas;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target != null)
            {
                target.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            target.anchoredPosition += eventData.delta / scale;
        }
    }

    private sealed class HudWindowResizeHandle : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public RectTransform target;
        public Canvas canvas;
        public Vector2 minSize = new Vector2(260f, 96f);

        public void OnPointerDown(PointerEventData eventData)
        {
            if (target != null)
            {
                target.SetAsLastSibling();
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 delta = eventData.delta / scale;
            Vector2 size = target.sizeDelta;
            size.x = Mathf.Max(minSize.x, size.x + delta.x);
            size.y = Mathf.Max(minSize.y, size.y - delta.y);
            target.sizeDelta = size;
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

    private struct RectTransformSpec
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
    }
}
