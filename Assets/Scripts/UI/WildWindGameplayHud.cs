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
        "game.hud.mission_title",
        "game.hud.route",
        "game.hud.capital_stock",
        "game.hud.destination_stock",
        "game.hud.ship_cargo",
        "game.hud.distance",
        "game.hud.altitude",
        "game.hud.target_altitude",
        "game.hud.speed",
        "game.hud.load_food",
        "game.hud.takeoff",
        "game.hud.dock",
        "game.hud.unload_food",
        "game.hud.menu",
        "game.hud.completed",
        "game.hud.flight_controls",
        "game.hud.forward",
        "game.hud.backward",
        "game.hud.left",
        "game.hud.right",
        "game.hud.ascend",
        "game.hud.descend",
        "game.hud.axis_altitude",
        "game.hud.axis_heading",
        "game.hud.axis_speed",
        "game.hud.mode_manual",
        "game.hud.mode_assist",
        "game.hud.mode_autopilot",
        "game.hud.target_altitude_up",
        "game.hud.target_altitude_down",
        "game.hud.target_altitude_set",
        "game.hud.compass_target",
        "game.hud.target_heading_left",
        "game.hud.target_heading_right",
        "game.hud.target_speed_up",
        "game.hud.target_speed_down",
        "game.hud.autopilot_target_task",
        "game.hud.autopilot_target_here",
        "game.hud.autopilot_target_manual",
        "game.hud.autopilot_target_move",
        "game.hud.autopilot_target_delete",
        "game.hud.autopilot_auto_dock",
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

    [SerializeField, InspectorName("Reference Resolution")] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private Canvas canvas;
    private RectTransform root;
    private RectTransform dockedPanel;
    private RectTransform flightPanel;
    private RectTransform flightControlsPanel;
    private RectTransform compassPanel;
    private RectTransform enginePowerBar;
    private RectTransform engineLiftPowerFill;
    private RectTransform engineModulePowerFill;
    private RectTransform engineThrustPowerFill;
    private RectTransform engineAfterburnerMarker;
    private Text[] compassTickTexts;
    private RectTransform[] compassTickLines;
    private RectTransform compassTargetLine;
    private Text compassTargetText;
    private Text modeText;
    private Text missionText;
    private Text routeText;
    private Text capitalStockText;
    private Text destinationStockText;
    private Text shipCargoText;
    private Text distanceText;
    private Text altitudeText;
    private Text targetAltitudeText;
    private Text speedText;
    private Text enginePowerText;
    private Text statusText;
    private Button loadFoodButton;
    private Button takeoffButton;
    private Button dockButton;
    private Button unloadFoodButton;
    private Button menuButton;
    private Text loadFoodText;
    private Text takeoffText;
    private Text dockText;
    private Text unloadFoodText;
    private Text menuText;
    private Text flightMenuText;
    private Text forwardText;
    private Text backwardText;
    private Text leftText;
    private Text rightText;
    private Text ascendText;
    private Text descendText;
    private Text flightControlSummaryText;
    private Text assistAltitudeText;
    private Text assistHeadingText;
    private Text assistSpeedText;
    private Text targetAltitudeUpText;
    private Text targetAltitudeDownText;
    private Text targetAltitudeSetText;
    private Text targetAltitudeQuickDownText;
    private Text targetAltitudeQuickUpText;
    private Text targetHeadingLeftText;
    private Text targetHeadingRightText;
    private Text targetSpeedUpText;
    private Text targetSpeedDownText;
    private Text autopilotTargetTaskText;
    private Text autopilotTargetHereText;
    private Text autopilotTargetManualText;
    private Text autopilotTargetMoveText;
    private Text autopilotTargetDeleteText;
    private Text autopilotAutoDockText;
    private Text afterburnerText;
    private InputField targetAltitudeInput;
    private InputField targetXInput;
    private InputField targetYInput;
    private InputField targetZInput;
    private MetaGameState meta;
    private WildWindGameplaySession session;
    private WorldDebugTravelController travelController;
    private WildWindFlightControlBridge flightControls;
    private Vector3 heldFlightInput;
    private float nextRefreshTime;
    private float lastCompassHeadingDegrees;
    private string statusMessage = "";
    private static Font cachedDefaultFont;

    public bool IsReady => canvas != null && root != null && ResolveMeta() != null && ResolveSession() != null;
    public bool IsDockedPanelVisible => dockedPanel != null && dockedPanel.gameObject.activeSelf;
    public bool IsFlightPanelVisible => flightPanel != null && flightPanel.gameObject.activeSelf;
    public bool IsFlightControlsVisible => flightControlsPanel != null && flightControlsPanel.gameObject.activeSelf;
    public bool IsFlightCompassVisible => compassPanel != null && compassPanel.gameObject.activeSelf;
    public float LastCompassHeadingDegrees => lastCompassHeadingDegrees;

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
        if (Object.FindFirstObjectByType<WorldRegionRuntime>() == null)
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
        ResolveFlightControls()?.ClearDirectInput();
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

    public bool TryLoadStarterFood()
    {
        MetaGameState currentMeta = ResolveMeta();
        bool result = WildWindStarterDelivery.TryLoadFood(currentMeta, out statusMessage);
        RefreshState(true);
        return result;
    }

    public bool TryTakeOff()
    {
        WildWindGameplaySession currentSession = ResolveSession();
        if (currentSession == null)
        {
            statusMessage = "Сессия мира не найдена.";
            RefreshState(true);
            return false;
        }

        bool result = currentSession.TryBeginFreeFlight(out statusMessage);
        if (result)
        {
            ResolveTravelController()?.FocusOnPlayerShipAfterUndocking();
        }

        RefreshState(true);
        return result;
    }

    public bool TryDockNearest()
    {
        WildWindGameplaySession currentSession = ResolveSession();
        if (currentSession == null)
        {
            statusMessage = "Сессия мира не найдена.";
            RefreshState(true);
            return false;
        }

        bool result = currentSession.TryDockAtNearestAvailableDock(out statusMessage);
        RefreshState(true);
        return result;
    }

    public bool TryUnloadStarterFood()
    {
        MetaGameState currentMeta = ResolveMeta();
        bool result = WildWindStarterDelivery.TryUnloadFood(currentMeta, out statusMessage);
        RefreshState(true);
        return result;
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

        RectTransform topBand = CreatePanel("Mission Strip", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(1f, 1f),
            pivot = new Vector2(0.5f, 1f),
            anchoredPosition = new Vector2(0f, -24f),
            sizeDelta = new Vector2(-80f, 92f)
        }, new Color(0.025f, 0.026f, 0.028f, 0.82f));

        modeText = CreateText(topBand, "", 28, new Vector2(24f, -12f), new Vector2(240f, 34f), TextAnchor.MiddleLeft, new Color(0.95f, 0.81f, 0.52f, 1f));
        missionText = CreateText(topBand, "", 24, new Vector2(280f, -14f), new Vector2(760f, 32f), TextAnchor.MiddleLeft, new Color(0.91f, 0.88f, 0.78f, 1f));
        routeText = CreateText(topBand, "", 20, new Vector2(280f, -50f), new Vector2(760f, 28f), TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 0.82f, 1f));
        distanceText = CreateText(topBand, "", 22, new Vector2(1480f, -30f), new Vector2(300f, 34f), TextAnchor.MiddleRight, new Color(0.86f, 0.80f, 0.64f, 1f));

        BuildCompass();

        dockedPanel = CreatePanel("Docked Panel", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(0f, 0f),
            pivot = new Vector2(0f, 0f),
            anchoredPosition = new Vector2(42f, 42f),
            sizeDelta = new Vector2(520f, 250f)
        }, new Color(0.026f, 0.024f, 0.022f, 0.84f));

        capitalStockText = CreateText(dockedPanel, "", 21, new Vector2(24f, -22f), new Vector2(460f, 30f), TextAnchor.MiddleLeft, new Color(0.88f, 0.83f, 0.68f, 1f));
        destinationStockText = CreateText(dockedPanel, "", 21, new Vector2(24f, -58f), new Vector2(460f, 30f), TextAnchor.MiddleLeft, new Color(0.70f, 0.78f, 0.82f, 1f));
        shipCargoText = CreateText(dockedPanel, "", 21, new Vector2(24f, -94f), new Vector2(460f, 30f), TextAnchor.MiddleLeft, new Color(0.91f, 0.88f, 0.78f, 1f));
        loadFoodText = CreateButton(dockedPanel, "Load Food", new Vector2(24f, -144f), new Vector2(220f, 54f), TryLoadStarterFood, out loadFoodButton);
        takeoffText = CreateButton(dockedPanel, "Takeoff", new Vector2(264f, -144f), new Vector2(220f, 54f), TryTakeOff, out takeoffButton);
        unloadFoodText = CreateButton(dockedPanel, "Unload Food", new Vector2(24f, -206f), new Vector2(220f, 54f), TryUnloadStarterFood, out unloadFoodButton);
        menuText = CreateButton(dockedPanel, "Menu", new Vector2(264f, -206f), new Vector2(220f, 54f), () => { OpenMenu(); return true; }, out menuButton);

        flightPanel = CreatePanel("Flight Panel", root, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 0f),
            anchorMax = new Vector2(1f, 0f),
            pivot = new Vector2(0.5f, 0f),
            anchoredPosition = new Vector2(0f, 34f),
            sizeDelta = new Vector2(-84f, 306f)
        }, new Color(0.018f, 0.022f, 0.026f, 0.78f));

        altitudeText = CreateText(flightPanel, "", 22, new Vector2(26f, -20f), new Vector2(190f, 32f), TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.92f, 1f));
        targetAltitudeText = CreateText(flightPanel, "", 18, new Vector2(224f, -20f), new Vector2(158f, 32f), TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.92f, 1f));
        targetAltitudeInput = CreateInputField(flightPanel, "Target Altitude", new Vector2(390f, -18f), new Vector2(82f, 28f), "m");
        targetAltitudeSetText = CreateButton(flightPanel, "Set Target Altitude", new Vector2(482f, -18f), new Vector2(82f, 28f), SetTargetAltitudeFromInput, out _);
        targetAltitudeQuickDownText = CreateButton(flightPanel, "Target Altitude Quick Down", new Vector2(574f, -18f), new Vector2(52f, 28f), () => NudgeTargetAltitudeBy(-50f), out _);
        targetAltitudeQuickUpText = CreateButton(flightPanel, "Target Altitude Quick Up", new Vector2(632f, -18f), new Vector2(52f, 28f), () => NudgeTargetAltitudeBy(50f), out _);
        speedText = CreateText(flightPanel, "", 22, new Vector2(26f, -58f), new Vector2(300f, 32f), TextAnchor.MiddleLeft, new Color(0.82f, 0.90f, 0.92f, 1f));
        enginePowerText = CreateText(flightPanel, "", 17, new Vector2(1668f, -60f), new Vector2(170f, 24f), TextAnchor.MiddleCenter, new Color(0.92f, 0.90f, 0.82f, 1f));
        BuildEnginePowerBar(flightPanel);
        dockText = CreateButton(flightPanel, "Dock", new Vector2(1280f, -22f), new Vector2(220f, 54f), TryDockNearest, out dockButton);
        flightMenuText = CreateButton(flightPanel, "Menu Flight", new Vector2(1520f, -22f), new Vector2(180f, 54f), () => { OpenMenu(); return true; }, out _);
        afterburnerText = CreateButton(flightPanel, "Afterburner", new Vector2(1520f, -86f), new Vector2(180f, 36f), ToggleAfterburner, out _);

        flightControlsPanel = CreatePanel("Flight Controls", flightPanel, new RectTransformSpec
        {
            anchorMin = new Vector2(0f, 1f),
            anchorMax = new Vector2(0f, 1f),
            pivot = new Vector2(0f, 1f),
            anchoredPosition = new Vector2(24f, -86f),
            sizeDelta = new Vector2(1420f, 202f)
        }, new Color(0.030f, 0.035f, 0.040f, 0.62f));

        flightControlSummaryText = CreateText(flightControlsPanel, "", 18, new Vector2(18f, -8f), new Vector2(1370f, 24f), TextAnchor.MiddleLeft, new Color(0.74f, 0.84f, 0.86f, 1f));

        forwardText = CreateButton(flightControlsPanel, "Forward", new Vector2(154f, -40f), new Vector2(136f, 36f), () => { ResolveFlightControls()?.NudgeManualThrust(1f); RefreshState(true); return true; }, out _);
        backwardText = CreateButton(flightControlsPanel, "Backward", new Vector2(154f, -84f), new Vector2(136f, 36f), () => { ResolveFlightControls()?.NudgeManualThrust(-1f); RefreshState(true); return true; }, out _);
        leftText = CreateButton(flightControlsPanel, "Left", new Vector2(18f, -84f), new Vector2(124f, 36f), () => { ResolveFlightControls()?.NudgeManualTurn(-1f); RefreshState(true); return true; }, out _);
        rightText = CreateButton(flightControlsPanel, "Right", new Vector2(302f, -84f), new Vector2(124f, 36f), () => { ResolveFlightControls()?.NudgeManualTurn(1f); RefreshState(true); return true; }, out _);
        ascendText = CreateButton(flightControlsPanel, "Ascend", new Vector2(18f, -40f), new Vector2(124f, 36f), () => { ResolveFlightControls()?.NudgeTargetAltitude(50f); RefreshState(true); return true; }, out _);
        descendText = CreateButton(flightControlsPanel, "Descend", new Vector2(302f, -40f), new Vector2(124f, 36f), () => { ResolveFlightControls()?.NudgeTargetAltitude(-50f); RefreshState(true); return true; }, out _);

        assistAltitudeText = CreateButton(flightControlsPanel, "Altitude Mode", new Vector2(452f, -40f), new Vector2(170f, 36f), () => { ResolveFlightControls()?.CycleAltitudeMode(); RefreshState(true); return true; }, out _);
        assistHeadingText = CreateButton(flightControlsPanel, "Heading Mode", new Vector2(452f, -84f), new Vector2(170f, 36f), () => { ResolveFlightControls()?.CycleHeadingMode(); RefreshState(true); return true; }, out _);
        assistSpeedText = CreateButton(flightControlsPanel, "Speed Mode", new Vector2(452f, -128f), new Vector2(170f, 36f), () => { ResolveFlightControls()?.CycleSpeedMode(); RefreshState(true); return true; }, out _);
        targetAltitudeUpText = CreateButton(flightControlsPanel, "Target Altitude Up", new Vector2(638f, -40f), new Vector2(88f, 36f), () => { ResolveFlightControls()?.NudgeTargetAltitude(50f); RefreshState(true); return true; }, out _);
        targetAltitudeDownText = CreateButton(flightControlsPanel, "Target Altitude Down", new Vector2(734f, -40f), new Vector2(88f, 36f), () => { ResolveFlightControls()?.NudgeTargetAltitude(-50f); RefreshState(true); return true; }, out _);
        targetHeadingLeftText = CreateButton(flightControlsPanel, "Target Heading Left", new Vector2(638f, -84f), new Vector2(88f, 36f), () => { ResolveFlightControls()?.NudgeTargetHeading(-10f); RefreshState(true); return true; }, out _);
        targetHeadingRightText = CreateButton(flightControlsPanel, "Target Heading Right", new Vector2(734f, -84f), new Vector2(88f, 36f), () => { ResolveFlightControls()?.NudgeTargetHeading(10f); RefreshState(true); return true; }, out _);
        targetSpeedUpText = CreateButton(flightControlsPanel, "Target Speed Up", new Vector2(638f, -128f), new Vector2(88f, 36f), () => { ResolveFlightControls()?.NudgeTargetSpeed(2f); RefreshState(true); return true; }, out _);
        targetSpeedDownText = CreateButton(flightControlsPanel, "Target Speed Down", new Vector2(734f, -128f), new Vector2(88f, 36f), () => { ResolveFlightControls()?.NudgeTargetSpeed(-2f); RefreshState(true); return true; }, out _);

        autopilotTargetTaskText = CreateButton(flightControlsPanel, "Autopilot Target Task", new Vector2(852f, -40f), new Vector2(162f, 36f), SetAutopilotTargetFromTask, out _);
        autopilotTargetHereText = CreateButton(flightControlsPanel, "Autopilot Target Here", new Vector2(1026f, -40f), new Vector2(162f, 36f), SetAutopilotTargetHere, out _);
        autopilotTargetMoveText = CreateButton(flightControlsPanel, "Autopilot Target Move", new Vector2(852f, -84f), new Vector2(162f, 36f), MoveAutopilotTarget, out _);
        autopilotTargetDeleteText = CreateButton(flightControlsPanel, "Autopilot Target Delete", new Vector2(1026f, -84f), new Vector2(162f, 36f), ClearAutopilotTarget, out _);
        autopilotAutoDockText = CreateButton(flightControlsPanel, "Autopilot Auto Dock", new Vector2(852f, -128f), new Vector2(162f, 36f), () => { ResolveFlightControls()?.ToggleAutoDockOnArrival(); RefreshState(true); return true; }, out _);

        targetXInput = CreateInputField(flightControlsPanel, "Target X", new Vector2(18f, -164f), new Vector2(86f, 30f), "X");
        targetYInput = CreateInputField(flightControlsPanel, "Target Y", new Vector2(112f, -164f), new Vector2(86f, 30f), "Y");
        targetZInput = CreateInputField(flightControlsPanel, "Target Z", new Vector2(206f, -164f), new Vector2(86f, 30f), "Z");
        autopilotTargetManualText = CreateButton(flightControlsPanel, "Autopilot Target Manual", new Vector2(302f, -164f), new Vector2(124f, 30f), SetManualAutopilotTarget, out _);

        statusText = CreateText(root, "", 20, new Vector2(42f, -126f), new Vector2(760f, 32f), TextAnchor.MiddleLeft, new Color(0.95f, 0.75f, 0.42f, 1f));
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
        compassTargetText.color = new Color(0.44f, 0.95f, 1f, 0.96f);
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
        engineThrustPowerFill = CreatePowerFill("Thrust Power", new Color(0.14f, 0.78f, 0.28f, 0.96f));

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
        SetText(loadFoodText, WildWindLocalization.Get("game.hud.load_food"));
        SetText(takeoffText, WildWindLocalization.Get("game.hud.takeoff"));
        SetText(dockText, WildWindLocalization.Get("game.hud.dock"));
        SetText(unloadFoodText, WildWindLocalization.Get("game.hud.unload_food"));
        SetText(menuText, WildWindLocalization.Get("game.hud.menu"));
        SetText(flightMenuText, WildWindLocalization.Get("game.hud.menu"));
        SetText(forwardText, WildWindLocalization.Get("game.hud.forward"));
        SetText(backwardText, WildWindLocalization.Get("game.hud.backward"));
        SetText(leftText, WildWindLocalization.Get("game.hud.left"));
        SetText(rightText, WildWindLocalization.Get("game.hud.right"));
        SetText(ascendText, WildWindLocalization.Get("game.hud.target_altitude_up"));
        SetText(descendText, WildWindLocalization.Get("game.hud.target_altitude_down"));
        SetText(targetAltitudeSetText, WildWindLocalization.Get("game.hud.target_altitude_set"));
        SetText(targetAltitudeQuickDownText, "-50");
        SetText(targetAltitudeQuickUpText, "+50");
        SetText(targetAltitudeUpText, WildWindLocalization.Get("game.hud.target_altitude_up"));
        SetText(targetAltitudeDownText, WildWindLocalization.Get("game.hud.target_altitude_down"));
        SetText(targetHeadingLeftText, WildWindLocalization.Get("game.hud.target_heading_left"));
        SetText(targetHeadingRightText, WildWindLocalization.Get("game.hud.target_heading_right"));
        SetText(targetSpeedUpText, WildWindLocalization.Get("game.hud.target_speed_up"));
        SetText(targetSpeedDownText, WildWindLocalization.Get("game.hud.target_speed_down"));
        SetText(autopilotTargetTaskText, WildWindLocalization.Get("game.hud.autopilot_target_task"));
        SetText(autopilotTargetHereText, WildWindLocalization.Get("game.hud.autopilot_target_here"));
        SetText(autopilotTargetManualText, WildWindLocalization.Get("game.hud.autopilot_target_manual"));
        SetText(autopilotTargetMoveText, WildWindLocalization.Get("game.hud.autopilot_target_move"));
        SetText(autopilotTargetDeleteText, WildWindLocalization.Get("game.hud.autopilot_target_delete"));
        RefreshAfterburnerText();
        RefreshState(true);
    }

    private void RefreshState(bool force)
    {
        MetaGameState currentMeta = ResolveMeta();
        WildWindGameplaySession currentSession = ResolveSession();
        if (currentMeta == null || currentMeta.progress == null)
        {
            return;
        }

        currentMeta.EnsureProgressInitialized();
        PlayerProgress progress = currentMeta.progress;
        bool docked = currentMeta.CurrentMode == GameSessionMode.Docked;
        bool completed = WildWindStarterDelivery.IsCompleted(progress);
        bool hasActiveDelivery = WildWindStarterDelivery.HasActiveDelivery(progress);
        int sourceStock = WildWindStarterDelivery.GetSourceStock(progress);
        int destinationStock = WildWindStarterDelivery.GetDestinationStock(progress);
        int shipCargo = WildWindStarterDelivery.GetShipCargo(progress);
        int deliveryAmount = WildWindStarterDelivery.GetActiveDeliveryAmount(progress);
        float distance = Vector3.Distance(GetPlayerPosition(currentSession), GetDestinationPosition(currentMeta));

        dockedPanel.gameObject.SetActive(docked);
        flightPanel.gameObject.SetActive(!docked);
        flightControlsPanel.gameObject.SetActive(!docked);
        compassPanel.gameObject.SetActive(!docked);
        SetText(modeText, WildWindLocalization.Get(docked ? "game.hud.city" : "game.hud.flight"));
        SetText(missionText, completed
            ? WildWindLocalization.Get("game.hud.completed")
            : WildWindStarterDelivery.GetMissionTitle(progress));
        SetText(routeText, WildWindStarterDelivery.GetRouteText(progress));
        SetText(capitalStockText, WildWindStarterDelivery.GetSourceStockLabel(progress) + ": " + sourceStock);
        SetText(destinationStockText, WildWindStarterDelivery.GetDestinationStockLabel(progress) + ": " + destinationStock + (deliveryAmount > 0 ? " / " + deliveryAmount : ""));
        SetText(shipCargoText, WildWindStarterDelivery.GetShipCargoLabel(progress) + ": " + shipCargo);
        SetText(loadFoodText, WildWindStarterDelivery.GetLoadButtonText(progress));
        SetText(unloadFoodText, WildWindStarterDelivery.GetUnloadButtonText(progress));
        WildWindFlightControlBridge controls = ResolveFlightControls();
        float currentAltitude = GetPlayerPosition(currentSession).y;
        float targetAltitude = controls != null ? controls.TargetAltitude : currentAltitude;
        SetText(distanceText, WildWindLocalization.Get("game.hud.distance") + ": " + FormatDistance(distance));
        SetText(altitudeText, WildWindLocalization.Get("game.hud.altitude") + ": " + currentAltitude.ToString("0") + " м");
        SetText(targetAltitudeText, WildWindLocalization.Get("game.hud.target_altitude") + ": " + targetAltitude.ToString("0") + " м");
        SetText(speedText, WildWindLocalization.Get("game.hud.speed") + ": " + GetShipSpeed(currentMeta).ToString("0.0") + " м/с");
        SetText(statusText, statusMessage);
        RefreshEnginePowerBar();

        if (controls != null)
        {
            SetText(flightControlSummaryText, WildWindLocalization.Get("game.hud.flight_controls") + ": " + controls.GetControlSummary());
            SetAxisModeText(assistAltitudeText, "game.hud.axis_altitude", controls.AltitudeMode);
            SetAxisModeText(assistHeadingText, "game.hud.axis_heading", controls.HeadingMode);
            SetAxisModeText(assistSpeedText, "game.hud.axis_speed", controls.SpeedMode);
            SetToggleText(autopilotAutoDockText, "game.hud.autopilot_auto_dock", controls.AutoDockOnArrival);
            RefreshAfterburnerText();
            SyncTargetFields(controls);
            SyncTargetAltitudeField(controls);
        }

        string sourceDockId = WildWindStarterDelivery.GetActiveSourceDockId(progress);
        string destinationDockId = WildWindStarterDelivery.GetActiveDestinationDockId(progress);
        bool atSource = docked && progress.currentDockId == sourceDockId;
        bool atDestination = docked && progress.currentDockId == destinationDockId;
        SetInteractable(loadFoodButton, hasActiveDelivery && atSource && !completed && shipCargo < deliveryAmount && sourceStock > 0);
        SetInteractable(takeoffButton, docked);
        SetInteractable(dockButton, !docked);
        SetInteractable(unloadFoodButton, hasActiveDelivery && atDestination && !completed && shipCargo > 0);
        SetInteractable(menuButton, true);
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

        float maxPowerKw = Mathf.Max(0.001f, ship.enginePowerKwAt100 * EngineAfterburnerPowerMultiplier);
        float liftPowerKw = Mathf.Clamp(ship.claudiumPowerDrawKw, 0f, maxPowerKw);
        float modulePowerKw = Mathf.Clamp(GetShipModulePowerKw(ship), 0f, maxPowerKw - liftPowerKw);
        float thrustPowerKw = Mathf.Clamp(ship.propellerInputPowerKw, 0f, maxPowerKw - liftPowerKw - modulePowerKw);
        float liftHeight = EnginePowerBarHeight * liftPowerKw / maxPowerKw;
        float moduleHeight = EnginePowerBarHeight * modulePowerKw / maxPowerKw;
        float thrustHeight = EnginePowerBarHeight * thrustPowerKw / maxPowerKw;

        SetPowerFill(engineLiftPowerFill, 0f, liftHeight);
        SetPowerFill(engineModulePowerFill, liftHeight, moduleHeight);
        SetPowerFill(engineThrustPowerFill, liftHeight + moduleHeight, thrustHeight);

        if (engineAfterburnerMarker != null)
        {
            engineAfterburnerMarker.anchoredPosition = new Vector2(
                0f,
                EnginePowerBarHeight / EngineAfterburnerPowerMultiplier);
        }

        float usedPowerKw = liftPowerKw + modulePowerKw + thrustPowerKw;
        SetText(enginePowerText, "PWR " + usedPowerKw.ToString("0") + " / " + maxPowerKw.ToString("0") + " kW");
    }

    private static float GetShipModulePowerKw(ShipPhysics ship)
    {
        if (ship == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, ship.gasHarvesterPowerDrawActualKw);
    }

    private bool ToggleAfterburner()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls != null)
        {
            controls.ToggleAfterburner();
            statusMessage = controls.StatusMessage;
            RefreshAfterburnerText();
            RefreshState(true);
            return true;
        }

        ShipPhysics ship = GetHudShip();
        if (ship == null)
        {
            return false;
        }

        ship.engineAfterburnerEnabled = !ship.engineAfterburnerEnabled;
        if (!ship.engineAfterburnerEnabled)
        {
            ship.enginePowerLever = Mathf.Min(ship.enginePowerLever, ship.EnginePowerLeverLimit);
        }

        RefreshAfterburnerText();
        RefreshState(true);
        return true;
    }

    private void RefreshAfterburnerText()
    {
        ShipPhysics ship = GetHudShip();
        bool enabled = ship != null && ship.engineAfterburnerEnabled;
        string state = WildWindLocalization.Get(enabled ? "game.hud.on" : "game.hud.off");
        SetText(afterburnerText, "Форсаж: " + state);
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
        heldFlightInput = input.sqrMagnitude > 1f ? input.normalized : input;
        if (heldFlightInput.sqrMagnitude <= 0.0001f)
        {
            ResolveFlightControls()?.ClearDirectInput();
            ResolveTravelController()?.ClearUiInput();
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
                controls.SetDirectInput(heldFlightInput.z, heldFlightInput.y, heldFlightInput.x);
            }
            return;
        }

        if (heldFlightInput.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        WorldDebugTravelController controller = ResolveTravelController();
        if (controller != null)
        {
            controller.SetUiInput(heldFlightInput);
        }
    }

    private bool SetAutopilotTargetFromTask()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        bool result = controls != null && controls.SetTargetFromActiveTask();
        statusMessage = controls != null ? controls.StatusMessage : "Flight controls not found.";
        RefreshState(true);
        return result;
    }

    private bool SetAutopilotTargetHere()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls == null)
        {
            statusMessage = "Flight controls not found.";
            return false;
        }

        controls.SetTargetAtShipPosition();
        statusMessage = controls.StatusMessage;
        RefreshState(true);
        return true;
    }

    private bool MoveAutopilotTarget()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls == null)
        {
            statusMessage = "Flight controls not found.";
            return false;
        }

        controls.MoveTargetForward();
        statusMessage = controls.StatusMessage;
        RefreshState(true);
        return true;
    }

    private bool ClearAutopilotTarget()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls == null)
        {
            statusMessage = "Flight controls not found.";
            return false;
        }

        controls.ClearAutopilotTarget();
        statusMessage = controls.StatusMessage;
        RefreshState(true);
        return true;
    }

    private bool SetManualAutopilotTarget()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls == null)
        {
            statusMessage = "Flight controls not found.";
            return false;
        }

        if (!TryReadCoordinate(targetXInput, out float x) ||
            !TryReadCoordinate(targetYInput, out float y) ||
            !TryReadCoordinate(targetZInput, out float z))
        {
            statusMessage = "Target coordinates are invalid.";
            RefreshState(true);
            return false;
        }

        controls.SetManualTargetCoordinates(x, y, z);
        statusMessage = controls.StatusMessage;
        RefreshState(true);
        return true;
    }

    private bool SetTargetAltitudeFromInput()
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls == null)
        {
            statusMessage = "Flight controls not found.";
            return false;
        }

        if (!TryReadCoordinate(targetAltitudeInput, out float altitude))
        {
            statusMessage = "Target altitude is invalid.";
            RefreshState(true);
            return false;
        }

        controls.SetTargetAltitude(altitude);
        statusMessage = controls.StatusMessage;
        RefreshState(true);
        return true;
    }

    private bool NudgeTargetAltitudeBy(float deltaMeters)
    {
        WildWindFlightControlBridge controls = ResolveFlightControls();
        if (controls == null)
        {
            statusMessage = "Flight controls not found.";
            return false;
        }

        controls.NudgeTargetAltitude(deltaMeters);
        statusMessage = controls.StatusMessage;
        RefreshState(true);
        return true;
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
        if (!TryGetCompassTargetPosition(currentMeta, controls, out Vector3 targetPosition))
        {
            SetCompassTargetMarkerVisible(false);
            return;
        }

        Vector3 from = GetPlayerPosition(currentSession);
        Vector3 toTarget = targetPosition - from;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.01f)
        {
            SetCompassTargetMarkerVisible(false);
            return;
        }

        float targetHeading = NormalizeHeading(Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg);
        float delta = Mathf.DeltaAngle(viewHeading, targetHeading);
        float rawX = delta * CompassPixelsPerDegree;
        float edgePadding = 28f;
        float x = Mathf.Clamp(rawX, -Mathf.Max(0f, halfWidth - edgePadding), Mathf.Max(0f, halfWidth - edgePadding));
        bool clamped = !Mathf.Approximately(rawX, x);

        SetCompassTargetMarkerVisible(true);
        if (compassTargetLine != null)
        {
            compassTargetLine.anchoredPosition = new Vector2(x, -12f);
        }

        if (compassTargetText != null)
        {
            string targetLabel = WildWindLocalization.Get("game.hud.compass_target");
            if (clamped)
            {
                targetLabel = delta < 0f ? "< " + targetLabel : targetLabel + " >";
            }

            compassTargetText.rectTransform.anchoredPosition = new Vector2(x, -2f);
            compassTargetText.text = targetLabel;
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

    private bool TryGetCompassTargetPosition(MetaGameState currentMeta, WildWindFlightControlBridge controls, out Vector3 targetPosition)
    {
        if (controls != null && controls.HasAutopilotTarget)
        {
            targetPosition = controls.AutopilotTarget;
            return true;
        }

        if (currentMeta != null && currentMeta.progress != null && WildWindStarterDelivery.HasActiveDelivery(currentMeta.progress))
        {
            targetPosition = GetDestinationPosition(currentMeta);
            return true;
        }

        targetPosition = Vector3.zero;
        return false;
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

    private WorldDebugTravelController ResolveTravelController()
    {
        if (travelController == null)
        {
            travelController = Object.FindFirstObjectByType<WorldDebugTravelController>();
        }

        return travelController;
    }

    private WildWindFlightControlBridge ResolveFlightControls()
    {
        if (flightControls == null)
        {
            flightControls = Object.FindFirstObjectByType<WildWindFlightControlBridge>();
        }

        return flightControls;
    }

    private Vector3 GetDestinationPosition(MetaGameState currentMeta)
    {
        PlayerProgress progress = currentMeta != null ? currentMeta.progress : null;
        string destinationDockId = WildWindStarterDelivery.GetActiveDestinationDockId(progress);
        if (string.IsNullOrWhiteSpace(destinationDockId))
        {
            return GetPlayerPosition(ResolveSession());
        }

        DockingPort[] docks = Object.FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        for (int i = 0; i < docks.Length; i++)
        {
            DockingPort dock = docks[i];
            if (dock != null && dock.dockId == destinationDockId)
            {
                return dock.DockPosition;
            }
        }

        IslandConfig island = currentMeta != null && currentMeta.WorldConfig != null
            ? currentMeta.WorldConfig.GetIsland(destinationDockId)
            : null;
        return island != null ? island.position : new Vector3(2300f, 2550f, 1200f);
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

    private void SetToggleText(Text text, string key, bool enabled)
    {
        string state = WildWindLocalization.Get(enabled ? "game.hud.on" : "game.hud.off");
        SetText(text, WildWindLocalization.Get(key) + ": " + state);
    }

    private void SetAxisModeText(Text text, string key, WildWindAxisControlMode mode)
    {
        SetText(text, WildWindLocalization.Get(key) + ": " + GetAxisModeText(mode));
    }

    private static string GetAxisModeText(WildWindAxisControlMode mode)
    {
        switch (mode)
        {
            case WildWindAxisControlMode.Assist:
                return WildWindLocalization.Get("game.hud.mode_assist");
            case WildWindAxisControlMode.Autopilot:
                return WildWindLocalization.Get("game.hud.mode_autopilot");
            default:
                return WildWindLocalization.Get("game.hud.mode_manual");
        }
    }

    private void SyncTargetFields(WildWindFlightControlBridge controls)
    {
        if (controls == null || !controls.HasAutopilotTarget)
        {
            return;
        }

        if (IsInputFocused(targetXInput) || IsInputFocused(targetYInput) || IsInputFocused(targetZInput))
        {
            return;
        }

        Vector3 target = controls.AutopilotTarget;
        SetInputText(targetXInput, target.x.ToString("0", CultureInfo.InvariantCulture));
        SetInputText(targetYInput, target.y.ToString("0", CultureInfo.InvariantCulture));
        SetInputText(targetZInput, target.z.ToString("0", CultureInfo.InvariantCulture));
    }

    private void SyncTargetAltitudeField(WildWindFlightControlBridge controls)
    {
        if (controls == null || IsInputFocused(targetAltitudeInput))
        {
            return;
        }

        SetInputText(targetAltitudeInput, controls.TargetAltitude.ToString("0", CultureInfo.InvariantCulture));
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
        StandaloneInputModule legacyModule = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            Destroy(legacyModule);
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
