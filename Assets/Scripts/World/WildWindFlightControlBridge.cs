using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum WildWindAxisControlMode
{
    Manual,
    Assist,
    Autopilot
}

[DefaultExecutionOrder(-200)]
public sealed class WildWindFlightControlBridge : MonoBehaviour
{
    private const string ObjectName = "Wild Wind Flight Control Bridge";
    private const float DefaultTargetSpeedMS = 34f;
    private const float TargetMoveStepMeters = 100f;
    private const float DefaultArrivalRadiusMeters = 24f;
    private const int MinManualThrustNotch = -3;
    private const int MaxManualThrustNotch = 5;
    private const float ForwardManualThrustNotchStep = 0.2f;
    private const float ManualLeverStep = 0.25f;
    private const float ManualThrustHoldDelaySeconds = 0.5f;
    private const float ManualThrustRepeatSeconds = 0.3f;
    private const float MaxForwardTargetSpeedMS = 120f;
    private const float DefaultMaxReverseTargetSpeedMS = 25f;
    private const float TargetAltitudeButtonStepMeters = 50f;

    [SerializeField, InspectorName("Target Speed, m/s")] private float targetSpeedMS = DefaultTargetSpeedMS;
    [SerializeField, InspectorName("Target Move Step, m")] private float targetMoveStepMeters = TargetMoveStepMeters;
    [SerializeField, InspectorName("Arrival Radius, m")] private float arrivalRadiusMeters = DefaultArrivalRadiusMeters;

    private MetaGameState meta;
    private WildWindGameplaySession session;
    private WildWindControlSettings controlSettings;
    private ShipPhysics ship;
    private DockingPort targetDock;

    private float manualThrust;
    private int manualThrustNotch;
    private float manualLateral;
    private float manualLift;
    private float manualTurn;
    private bool keyboardThrustActive;
    private bool keyboardLateralActive;
    private bool keyboardLiftActive;
    private bool keyboardTurnActive;
    private int keyboardThrustDirection;
    private float keyboardThrustHeldSeconds;
    private float keyboardThrustNextRepeatSeconds;
    private WildWindAxisControlMode altitudeMode = WildWindAxisControlMode.Manual;
    private WildWindAxisControlMode headingMode;
    private WildWindAxisControlMode speedMode;
    private bool hasAutopilotTarget;
    private Vector3 autopilotTarget;
    private string autopilotTargetDockId = "";
    private DockingLocationKind autopilotTargetDockKind = DockingLocationKind.Island;
    private bool autoDockOnArrival;
    private bool autoDockAttempted;
    private string syncedSortieEntryKey = "";
    private string statusMessage = "";

    public bool IsReady => ResolveShip() != null;
    public ShipPhysics ControlledShip => ResolveShip();
    public WildWindAxisControlMode AltitudeMode => altitudeMode;
    public WildWindAxisControlMode HeadingMode => headingMode;
    public WildWindAxisControlMode SpeedMode => speedMode;
    public bool AltitudeAssistEnabled => false;
    public bool HeadingAssistEnabled => false;
    public bool SpeedAssistEnabled => false;
    public bool AutopilotAltitudeEnabled => false;
    public bool AutopilotHeadingEnabled => false;
    public bool AutopilotSpeedEnabled => false;
    public bool AutoDockOnArrival => false;
    public bool HasAutopilotTarget => false;
    public bool IsFullAutopilot => false;
    public bool IsConnectedToGameplayShip => IsConnectedToSessionShip();
    public bool AfterburnerEnabled => ResolveShip() != null && ship.engineAfterburnerEnabled;
    public bool CheatAfterburnerEnabled => ResolveShip() != null && ship.engineCheatAfterburnerEnabled;
    public bool ClaudiumSlipstreamEnabled => ResolveShip() != null && ship.claudiumSlipstreamEnabled;
    public float ClaudiumSlipstreamCharge01 => ResolveShip() != null ? ship.ClaudiumSlipstreamCharge01 : 0f;
    public Vector3 AutopilotTarget => Vector3.zero;
    public float ManualThrust => manualThrust;
    public int ManualThrustNotch => manualThrustNotch;
    public float ManualLateral => manualLateral;
    public float ManualLift => manualLift;
    public float ManualTurn => manualTurn;
    public float TargetAltitude => ResolveShip() != null ? ship.targetAltitude : 0f;
    public float TargetHeading => ResolveShip() != null ? ship.targetHeading : 0f;
    public float TargetSpeedMS => targetSpeedMS;
    public string StatusMessage => statusMessage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallFlightControlBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureBridgeForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureBridgeForGameplayScene();
    }

    private static void EnsureBridgeForGameplayScene()
    {
        if (Object.FindFirstObjectByType<WorldRegionRuntime>() == null)
        {
            return;
        }

        EnsureInstance();
    }

    public static WildWindFlightControlBridge EnsureInstance()
    {
        WildWindFlightControlBridge existing = Object.FindFirstObjectByType<WildWindFlightControlBridge>();
        if (existing != null)
        {
            return existing;
        }

        GameObject bridgeObject = new GameObject(ObjectName);
        return bridgeObject.AddComponent<WildWindFlightControlBridge>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        ApplyKeyboardFlightInput(Time.unscaledDeltaTime);
    }

    private void FixedUpdate()
    {
        ApplyControlState();
    }

    public void ApplyForTests()
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        ResolveReferences();
        ApplyControlState();
    }

    public void SetDirectInput(float thrust, float lift, float turn)
    {
        SetDirectInput(thrust, lift, turn, 0f);
    }

    public void SetDirectInput(float thrust, float lift, float turn, float lateral)
    {
        SetManualThrustValue(thrust);
        manualLateral = Mathf.Clamp(lateral, -1f, 1f);
        manualLift = Mathf.Clamp(lift, -1f, 1f);
        manualTurn = Mathf.Clamp(turn, -1f, 1f);
        StopKeyboardThrustRepeat();
        keyboardLateralActive = false;
        keyboardLiftActive = false;
        keyboardTurnActive = false;
        statusMessage = "Direct controls updated.";
    }

    public void SetMomentaryLiftInput(float lift)
    {
        manualLift = Mathf.Clamp(lift, -1f, 1f);
        keyboardLiftActive = Mathf.Abs(manualLift) > 0.001f;
        altitudeMode = WildWindAxisControlMode.Manual;
        statusMessage = "Manual lift input.";
    }

    public void ClearMomentaryLiftInput()
    {
        manualLift = 0f;
        keyboardLiftActive = false;
    }

    public void SetManualThrustNotchForHud(int notch)
    {
        speedMode = WildWindAxisControlMode.Manual;
        SetManualThrustNotch(notch);
        StopKeyboardThrustRepeat();
        statusMessage = "Manual thrust notch selected.";
    }

    public void ClearDirectInput()
    {
        SetDirectInput(0f, 0f, 0f);
    }

    public void NudgeManualThrust(float direction)
    {
        StepManualThrust(Mathf.RoundToInt(Mathf.Sign(direction)));
        speedMode = WildWindAxisControlMode.Manual;
        statusMessage = "Manual thrust lever changed.";
    }

    public void NudgeManualLift(float direction)
    {
        manualLift = Mathf.Clamp(Mathf.Sign(direction), -1f, 1f);
        altitudeMode = WildWindAxisControlMode.Manual;
        statusMessage = "Manual lift input changed.";
    }

    public void NudgeManualTurn(float direction)
    {
        manualTurn = Mathf.Clamp(manualTurn + Mathf.Sign(direction) * ManualLeverStep, -1f, 1f);
        headingMode = WildWindAxisControlMode.Manual;
        statusMessage = "Manual turn lever changed.";
    }

    public void SetAltitudeAssist(bool enabled)
    {
        altitudeMode = WildWindAxisControlMode.Manual;
        if (ResolveShip() != null) ship.altitudeHold = false;
        statusMessage = "Altitude autopilot disabled.";
    }

    public void SetHeadingAssist(bool enabled)
    {
        headingMode = WildWindAxisControlMode.Manual;
        if (ResolveShip() != null) ship.headingHold = false;
        statusMessage = "Heading autopilot disabled.";
    }

    public void SetSpeedAssist(bool enabled)
    {
        speedMode = WildWindAxisControlMode.Manual;
        statusMessage = "Speed autopilot disabled.";
    }

    public void ToggleAltitudeAssist()
    {
        CycleAltitudeMode();
    }

    public void ToggleHeadingAssist()
    {
        SetHeadingAssist(!HeadingAssistEnabled);
    }

    public void ToggleSpeedAssist()
    {
        SetSpeedAssist(!SpeedAssistEnabled);
    }

    public void SetAutopilotAltitude(bool enabled)
    {
        altitudeMode = WildWindAxisControlMode.Manual;
        if (ResolveShip() != null) ship.altitudeHold = false;
        statusMessage = "Autopilot disabled.";
    }

    public void SetAutopilotHeading(bool enabled)
    {
        headingMode = WildWindAxisControlMode.Manual;
        if (ResolveShip() != null) ship.headingHold = false;
        statusMessage = "Autopilot disabled.";
    }

    public void SetAutopilotSpeed(bool enabled)
    {
        speedMode = WildWindAxisControlMode.Manual;
        statusMessage = "Autopilot disabled.";
    }

    public void ToggleAutopilotAltitude()
    {
        SetAutopilotAltitude(!AutopilotAltitudeEnabled);
    }

    public void ToggleAutopilotHeading()
    {
        SetAutopilotHeading(!AutopilotHeadingEnabled);
    }

    public void ToggleAutopilotSpeed()
    {
        SetAutopilotSpeed(!AutopilotSpeedEnabled);
    }

    public void CycleAltitudeMode()
    {
        SetAltitudeAssist(false);
    }

    public void CycleHeadingMode()
    {
        SetHeadingAssist(false);
    }

    public void CycleSpeedMode()
    {
        SetSpeedAssist(false);
    }

    public void ToggleAutoDockOnArrival()
    {
        autoDockOnArrival = false;
        autoDockAttempted = false;
        statusMessage = "Auto-dock disabled.";
    }

    public void ToggleAfterburner()
    {
        if (ResolveShip() == null)
        {
            statusMessage = "Flight controls have no ship.";
            return;
        }

        ship.engineAfterburnerEnabled = !ship.engineAfterburnerEnabled;
        ClampEnginePowerLeverToLimit();

        statusMessage = ship.engineAfterburnerEnabled
            ? "Afterburner 120% enabled."
            : "Afterburner disabled.";
    }

    public void ToggleCheatAfterburner()
    {
        if (ResolveShip() == null)
        {
            statusMessage = "Flight controls have no ship.";
            return;
        }

        ship.engineCheatAfterburnerEnabled = !ship.engineCheatAfterburnerEnabled;
        ClampEnginePowerLeverToLimit();

        statusMessage = ship.engineCheatAfterburnerEnabled
            ? "Cheat afterburner +500% enabled."
            : "Cheat afterburner disabled.";
    }

    private void ClampEnginePowerLeverToLimit()
    {
        if (ship != null)
        {
            ship.enginePowerLever = Mathf.Min(ship.enginePowerLever, ship.EnginePowerLeverLimit);
        }
    }

    public void ToggleClaudiumSlipstream()
    {
        if (ResolveShip() == null)
        {
            statusMessage = "Flight controls have no ship.";
            return;
        }

        if (ship.TrySetClaudiumSlipstreamEnabled(!ship.claudiumSlipstreamEnabled, out string reason))
        {
            statusMessage = reason;
            return;
        }

        statusMessage = reason;
    }

    public void NudgeTargetAltitude(float deltaMeters)
    {
        statusMessage = "Target altitude controls disabled.";
    }

    public void SetTargetAltitude(float altitudeMeters)
    {
        statusMessage = "Target altitude controls disabled.";
    }

    public void NudgeTargetHeading(float deltaDegrees)
    {
        statusMessage = "Heading autopilot disabled.";
    }

    public void NudgeTargetSpeed(float deltaMS)
    {
        statusMessage = "Speed autopilot disabled.";
    }

    public bool SetTargetFromActiveTask()
    {
        ClearAutopilotTarget();
        statusMessage = "Autopilot disabled.";
        return false;
    }

    public void SetTargetAtShipPosition()
    {
        ClearAutopilotTarget();
        statusMessage = "Autopilot disabled.";
    }

    public void SetManualTargetCoordinates(float x, float y, float z)
    {
        ClearAutopilotTarget();
        statusMessage = "Autopilot disabled.";
    }

    public void MoveTarget(Vector3 delta)
    {
        ClearAutopilotTarget();
        statusMessage = "Autopilot disabled.";
    }

    public void MoveTargetForward()
    {
        MoveTarget(new Vector3(targetMoveStepMeters, 0f, 0f));
    }

    public void ClearAutopilotTarget()
    {
        hasAutopilotTarget = false;
        targetDock = null;
        autopilotTargetDockId = "";
        autoDockOnArrival = false;
        autoDockAttempted = false;
        statusMessage = "Autopilot target cleared.";
    }

    public void ClearAllControlModes()
    {
        altitudeMode = WildWindAxisControlMode.Manual;
        headingMode = WildWindAxisControlMode.Manual;
        speedMode = WildWindAxisControlMode.Manual;
        autoDockOnArrival = false;
        ClearDirectInput();
        ClearKeyboardInputFlags();
        ClearAutopilotTarget();
    }

    public void PrimeSortieEntryCruise(Vector3 targetPosition, float speedMS)
    {
        ResolveReferences();
        ShipPhysics currentShip = ResolveShip();
        if (currentShip == null)
        {
            statusMessage = "No ship for sortie entry cruise.";
            return;
        }

        ClearKeyboardInputFlags();
        manualLateral = 0f;
        manualLift = 0f;
        manualTurn = 0f;
        SetManualThrustNotch(MaxManualThrustNotch);

        altitudeMode = WildWindAxisControlMode.Manual;
        headingMode = WildWindAxisControlMode.Manual;
        speedMode = WildWindAxisControlMode.Manual;
        targetSpeedMS = ClampTargetSpeed(Mathf.Max(0f, speedMS));

        hasAutopilotTarget = false;
        targetDock = null;
        autopilotTargetDockId = "";
        autoDockOnArrival = false;
        autoDockAttempted = false;

        currentShip.targetAltitude = Mathf.Max(0f, currentShip.transform.position.y);
        Vector3 toTarget = Flatten(targetPosition - currentShip.transform.position);
        currentShip.targetHeading = toTarget.sqrMagnitude > 0.0001f
            ? HeadingFromVector(toTarget)
            : NormalizeHeading(currentShip.transform.eulerAngles.y);
        currentShip.altitudeHold = false;
        currentShip.headingHold = false;
        currentShip.cruiseControl = false;
        syncedSortieEntryKey = GetActiveSortieEntryKey();
        statusMessage = "Sortie entry cruise primed.";
    }

    public string GetControlSummary()
    {
        string connection = IsConnectedToGameplayShip ? "ship ok" : "ship ?";
        string thrustLabel = manualThrustNotch == 0
            ? "Stop"
            : manualThrust.ToString("0.0") + " (" + manualThrustNotch + ")";
        return "T " + thrustLabel +
            " / S " + manualLateral.ToString("0.0") +
            " / L " + manualLift.ToString("0.0") +
            " / R " + manualTurn.ToString("0.0") +
            " | AP off"
            + " | " + connection;
    }

    public void ApplyFlightInputForTests(WildWindFlightInputState input, float deltaSeconds)
    {
        ResolveReferences();
        ApplyFlightInput(input, Mathf.Max(0f, deltaSeconds));
    }

    private void ApplyKeyboardFlightInput(float deltaSeconds)
    {
        if (!CanReadKeyboardFlightInput())
        {
            ClearKeyboardInputFlags();
            return;
        }

        WildWindControlSettings settings = ResolveControlSettings();
        WildWindFlightInputState input = settings != null
            ? settings.ReadFlightInput()
            : WildWindControlSettings.ReadDefaultFlightInput();
        ApplyFlightInput(input, Mathf.Max(0f, deltaSeconds));
    }

    private void ApplyFlightInput(WildWindFlightInputState input, float deltaSeconds)
    {
        if (meta == null || meta.CurrentMode != GameSessionMode.Flight)
        {
            return;
        }

        ApplySpeedAxisInput(input.thrust, deltaSeconds);
        ApplyLateralAxisInput(input.lateral);
        ApplyAltitudeAxisInput(input.lift, deltaSeconds);
        ApplyHeadingAxisInput(input.turn, deltaSeconds);
    }

    private void ApplySpeedAxisInput(float axis, float deltaSeconds)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);
        if (speedMode == WildWindAxisControlMode.Autopilot)
        {
            StopKeyboardThrustRepeat();
            return;
        }

        if (speedMode == WildWindAxisControlMode.Assist)
        {
            speedMode = WildWindAxisControlMode.Manual;
        }

        ApplyManualThrustStepInput(axis, deltaSeconds);
    }

    private void ApplyLateralAxisInput(float axis)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);

        ApplyManualKeyboardAxis(axis, ref keyboardLateralActive, ref manualLateral);
    }

    private void ApplyAltitudeAxisInput(float axis, float deltaSeconds)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);
        altitudeMode = WildWindAxisControlMode.Manual;
        ApplyManualKeyboardAxis(axis, ref keyboardLiftActive, ref manualLift);
        if (Mathf.Abs(axis) > 0.001f)
        {
            statusMessage = "Manual lift input.";
        }
    }

    private void ApplyHeadingAxisInput(float axis, float deltaSeconds)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);
        headingMode = WildWindAxisControlMode.Manual;
        ApplyManualKeyboardAxis(axis, ref keyboardTurnActive, ref manualTurn);
    }

    private static void ApplyManualKeyboardAxis(float axis, ref bool activeFlag, ref float manualValue)
    {
        if (Mathf.Abs(axis) > 0.001f)
        {
            manualValue = axis;
            activeFlag = true;
            return;
        }

        if (activeFlag)
        {
            manualValue = 0f;
            activeFlag = false;
        }
    }

    private static void ClearKeyboardManualAxis(ref bool activeFlag, ref float manualValue)
    {
        if (!activeFlag)
        {
            return;
        }

        manualValue = 0f;
        activeFlag = false;
    }

    private void ClearKeyboardInputFlags()
    {
        StopKeyboardThrustRepeat();
        ClearKeyboardManualAxis(ref keyboardLateralActive, ref manualLateral);
        ClearKeyboardManualAxis(ref keyboardLiftActive, ref manualLift);
        ClearKeyboardManualAxis(ref keyboardTurnActive, ref manualTurn);
    }

    private void ApplyControlState()
    {
        ResolveReferences();
        if (ship == null || meta == null || meta.CurrentMode != GameSessionMode.Flight)
        {
            if (ship != null)
            {
                ship.thrustInput = 0f;
                ship.sideInput = 0f;
                ship.liftInput = 0f;
                ship.turnInput = 0f;
                ship.neutralStopBrakeEnabled = false;
            }

            return;
        }

        ship.routeEnabled = false;
        ship.positionHold = false;

        bool useHeadingHold = false;
        bool useSpeedHold = false;
        bool useStopGear = !useSpeedHold && Mathf.Approximately(manualThrust, 0f);

        ship.altitudeHold = false;
        ship.headingHold = useHeadingHold;
        ship.cruiseControl = useSpeedHold;
        ship.neutralStopBrakeEnabled = useStopGear;

        if (useStopGear)
        {
            ship.targetSpeedMS = 0f;
            ship.thrustInput = 0f;
        }

        ship.sideInput = manualLateral;
        ship.liftInput = manualLift;

        if (!useHeadingHold)
        {
            ship.turnInput = manualTurn;
        }

        if (!useSpeedHold && !useStopGear)
        {
            ship.thrustInput = manualThrust;
        }
    }

    private void ApplyManualThrustStepInput(float axis, float deltaSeconds)
    {
        int direction = Mathf.Abs(axis) > 0.001f ? (axis > 0f ? 1 : -1) : 0;
        if (direction == 0)
        {
            StopKeyboardThrustRepeat();
            return;
        }

        if (!keyboardThrustActive || keyboardThrustDirection != direction)
        {
            keyboardThrustActive = true;
            keyboardThrustDirection = direction;
            keyboardThrustHeldSeconds = 0f;
            keyboardThrustNextRepeatSeconds = ManualThrustHoldDelaySeconds;
            StepManualThrust(direction);
            statusMessage = "Manual thrust notch changed.";
            return;
        }

        keyboardThrustHeldSeconds += Mathf.Max(0f, deltaSeconds);
        while (keyboardThrustHeldSeconds + 0.0001f >= keyboardThrustNextRepeatSeconds)
        {
            StepManualThrust(direction);
            keyboardThrustNextRepeatSeconds += ManualThrustRepeatSeconds;
            statusMessage = "Manual thrust notch changed.";
        }
    }

    private void SetManualThrustValue(float value)
    {
        SetManualThrustNotch(GetNearestManualThrustNotch(value));
    }

    private void StepManualThrust(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        SetManualThrustNotch(manualThrustNotch + Mathf.Clamp(direction, -1, 1));
    }

    private void SetManualThrustNotch(int notch)
    {
        manualThrustNotch = Mathf.Clamp(notch, MinManualThrustNotch, MaxManualThrustNotch);
        manualThrust = GetManualThrustForNotch(manualThrustNotch);
    }

    private static float GetManualThrustForNotch(int notch)
    {
        if (notch > 0)
        {
            return Mathf.Clamp01(notch * ForwardManualThrustNotchStep);
        }

        return notch switch
        {
            -1 => -0.25f,
            -2 => -0.5f,
            -3 => -1f,
            _ => 0f
        };
    }

    private static int GetNearestManualThrustNotch(float value)
    {
        float clamped = Mathf.Clamp(value, -1f, 1f);
        if (clamped > 0f)
        {
            return Mathf.Clamp(Mathf.RoundToInt(clamped / ForwardManualThrustNotchStep), 0, MaxManualThrustNotch);
        }

        float reverseMagnitude = Mathf.Abs(clamped);
        if (reverseMagnitude < 0.125f) return 0;
        if (reverseMagnitude < 0.375f) return -1;
        if (reverseMagnitude < 0.75f) return -2;
        return -3;
    }

    private void StopKeyboardThrustRepeat()
    {
        keyboardThrustActive = false;
        keyboardThrustDirection = 0;
        keyboardThrustHeldSeconds = 0f;
        keyboardThrustNextRepeatSeconds = 0f;
    }

    private float CalculateArrivalSpeed(float distance)
    {
        float radius = Mathf.Max(1f, GetArrivalRadius());
        float stopDistance = Mathf.Max(0f, distance - radius);
        float desired = Mathf.Sqrt(2f * 2.5f * stopDistance);
        return Mathf.Clamp(desired, 0f, Mathf.Max(0f, targetSpeedMS));
    }

    private void TryAutoDockOnArrival()
    {
        autoDockOnArrival = false;
        autoDockAttempted = false;
        hasAutopilotTarget = false;
        if (!autoDockOnArrival || autoDockAttempted || !hasAutopilotTarget)
        {
            return;
        }

        ResolveReferences();
        if (session == null || meta == null || meta.CurrentMode != GameSessionMode.Flight || ResolveShip() == null)
        {
            return;
        }

        float distance = Vector3.Distance(ship.transform.position, autopilotTarget);
        if (distance > GetArrivalRadius())
        {
            return;
        }

        autoDockAttempted = true;
        string dockId = string.IsNullOrWhiteSpace(autopilotTargetDockId)
            ? (targetDock != null ? targetDock.dockId : "")
            : autopilotTargetDockId;
        DockingLocationKind dockKind = targetDock != null ? targetDock.kind : autopilotTargetDockKind;
        Vector3 dockPosition = targetDock != null ? targetDock.DockPosition : autopilotTarget;

        bool result = string.IsNullOrWhiteSpace(dockId)
            ? session.TryDockAtNearestAvailableDock(out statusMessage)
            : session.TryDockAt(dockId, dockKind, dockPosition, out statusMessage);
        if (!result)
        {
            autoDockAttempted = false;
        }
    }

    private void SetAutopilotTarget(Vector3 target, string dockId, DockingLocationKind dockKind)
    {
        autopilotTarget = Vector3.zero;
        autopilotTargetDockId = "";
        autopilotTargetDockKind = DockingLocationKind.Island;
        targetDock = null;
        hasAutopilotTarget = false;
        autoDockOnArrival = false;
        autoDockAttempted = false;
        statusMessage = "Autopilot disabled.";
    }

    private float GetArrivalRadius()
    {
        if (targetDock != null)
        {
            return Mathf.Max(1f, targetDock.dockingRadius);
        }

        return Mathf.Max(1f, arrivalRadiusMeters);
    }

    private bool HasAnySemiAutopilot()
    {
        return false;
    }

    private void EnsureTargetAltitudeInitialized()
    {
        if (ResolveShip() == null)
        {
            return;
        }

        if (ship.targetAltitude <= 0f)
        {
            ship.targetAltitude = Mathf.Max(0f, ship.transform.position.y);
        }
    }

    private bool CanReadKeyboardFlightInput()
    {
        if (meta == null || meta.CurrentMode != GameSessionMode.Flight || meta.IsSessionPaused)
        {
            return false;
        }

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        InputField selectedInput = selected != null ? selected.GetComponent<InputField>() : null;
        return selectedInput == null || !selectedInput.isFocused;
    }

    private float ClampTargetSpeed(float value)
    {
        float maxReverse = ResolveControlSettings() != null
            ? controlSettings.FlightMaxReverseTargetSpeedMetersPerSecond
            : DefaultMaxReverseTargetSpeedMS;
        return Mathf.Clamp(value, -Mathf.Max(0f, maxReverse), MaxForwardTargetSpeedMS);
    }

    private bool IsConnectedToSessionShip()
    {
        ResolveReferences();
        if (ship == null)
        {
            return false;
        }

        if (session == null || session.PlayerShipRoot == null)
        {
            return true;
        }

        return ship.transform == session.PlayerShipRoot || ship.transform.IsChildOf(session.PlayerShipRoot);
    }

    private static WildWindAxisControlMode NextMode(WildWindAxisControlMode mode)
    {
        switch (mode)
        {
            case WildWindAxisControlMode.Manual:
                return WildWindAxisControlMode.Assist;
            case WildWindAxisControlMode.Assist:
                return WildWindAxisControlMode.Autopilot;
            default:
                return WildWindAxisControlMode.Manual;
        }
    }

    private ShipPhysics ResolveShip()
    {
        ResolveReferences();
        return ship;
    }

    private WildWindControlSettings ResolveControlSettings()
    {
        if (controlSettings == null)
        {
            WildWindSettingsRoot settings = FindFirstObjectByType<WildWindSettingsRoot>();
            controlSettings = settings != null ? settings.Controls : FindFirstObjectByType<WildWindControlSettings>();
        }

        return controlSettings;
    }

    private void ResolveReferences()
    {
        if (meta == null)
        {
            meta = FindFirstObjectByType<MetaGameState>();
        }

        if (session == null)
        {
            session = FindFirstObjectByType<WildWindGameplaySession>();
        }

        ResolveControlSettings();

        if (meta != null && meta.shipLoader != null && meta.shipLoader.targetShip != null &&
            ship != meta.shipLoader.targetShip)
        {
            ship = meta.shipLoader.targetShip;
            SyncManualThrustWithActiveSortieEntry();
            return;
        }

        if (ship != null && !ship.isActiveAndEnabled)
        {
            ship = null;
        }

        if (ship == null && session != null && session.PlayerShipRoot != null)
        {
            ship = session.PlayerShipRoot.GetComponent<ShipPhysics>();
            if (ship == null)
            {
                ship = session.PlayerShipRoot.GetComponentInChildren<ShipPhysics>();
            }
        }

        if (ship == null && meta != null && meta.shipLoader != null)
        {
            ship = meta.shipLoader.targetShip;
        }

        if (ship == null)
        {
            ship = FindFirstObjectByType<ShipPhysics>();
        }

        SyncManualThrustWithActiveSortieEntry();
    }

    private void SyncManualThrustWithActiveSortieEntry()
    {
        string key = GetActiveSortieEntryKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            syncedSortieEntryKey = "";
            return;
        }

        if (key == syncedSortieEntryKey || ship == null)
        {
            return;
        }

        syncedSortieEntryKey = key;
        if (ship.thrustInput < 0.9f && ship.propellerPitch < 0.9f)
        {
            return;
        }

        SetManualThrustNotch(MaxManualThrustNotch);
        speedMode = WildWindAxisControlMode.Manual;
        statusMessage = "Sortie entry full thrust synced.";
    }

    private string GetActiveSortieEntryKey()
    {
        SortieSessionState sortie = meta != null ? meta.ActiveSortie : null;
        if (sortie == null || !sortie.active)
        {
            return "";
        }

        string sortieId = sortie.zone != null ? sortie.zone.sortieId : "";
        return sortie.startedUtcTicks.ToString() + ":" + sortieId;
    }

    private static DockingPort FindDock(string dockId)
    {
        if (string.IsNullOrWhiteSpace(dockId))
        {
            return null;
        }

        DockingPort[] docks = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        for (int i = 0; i < docks.Length; i++)
        {
            DockingPort dock = docks[i];
            if (dock != null && dock.dockId == dockId)
            {
                return dock;
            }
        }

        return null;
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HeadingFromVector(Vector3 vector)
    {
        if (vector.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return NormalizeHeading(Mathf.Atan2(vector.x, vector.z) * Mathf.Rad2Deg);
    }

    private static float NormalizeHeading(float value)
    {
        value %= 360f;
        return value < 0f ? value + 360f : value;
    }
}
