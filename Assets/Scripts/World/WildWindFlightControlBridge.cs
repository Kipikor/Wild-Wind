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
    private const int MinManualThrustNotch = -1;
    private const int MaxManualThrustNotch = 5;
    private const float ManualThrustNotchStep = 0.2f;
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
    private WildWindAxisControlMode altitudeMode = WildWindAxisControlMode.Assist;
    private WildWindAxisControlMode headingMode;
    private WildWindAxisControlMode speedMode;
    private bool hasAutopilotTarget;
    private Vector3 autopilotTarget;
    private string autopilotTargetDockId = "";
    private DockingLocationKind autopilotTargetDockKind = DockingLocationKind.Island;
    private bool autoDockOnArrival;
    private bool autoDockAttempted;
    private string statusMessage = "";

    public bool IsReady => ResolveShip() != null;
    public ShipPhysics ControlledShip => ResolveShip();
    public WildWindAxisControlMode AltitudeMode => altitudeMode;
    public WildWindAxisControlMode HeadingMode => headingMode;
    public WildWindAxisControlMode SpeedMode => speedMode;
    public bool AltitudeAssistEnabled => altitudeMode == WildWindAxisControlMode.Assist;
    public bool HeadingAssistEnabled => headingMode == WildWindAxisControlMode.Assist;
    public bool SpeedAssistEnabled => speedMode == WildWindAxisControlMode.Assist;
    public bool AutopilotAltitudeEnabled => altitudeMode == WildWindAxisControlMode.Autopilot;
    public bool AutopilotHeadingEnabled => headingMode == WildWindAxisControlMode.Autopilot;
    public bool AutopilotSpeedEnabled => speedMode == WildWindAxisControlMode.Autopilot;
    public bool AutoDockOnArrival => autoDockOnArrival;
    public bool HasAutopilotTarget => hasAutopilotTarget;
    public bool IsFullAutopilot => AutopilotAltitudeEnabled && AutopilotHeadingEnabled && AutopilotSpeedEnabled;
    public bool IsConnectedToGameplayShip => IsConnectedToSessionShip();
    public bool AfterburnerEnabled => ResolveShip() != null && ship.engineAfterburnerEnabled;
    public Vector3 AutopilotTarget => autopilotTarget;
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

        if (Object.FindFirstObjectByType<WildWindFlightControlBridge>() != null)
        {
            return;
        }

        GameObject bridgeObject = new GameObject(ObjectName);
        bridgeObject.AddComponent<WildWindFlightControlBridge>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        ApplyKeyboardFlightInput(Time.unscaledDeltaTime);
        TryAutoDockOnArrival();
    }

    private void FixedUpdate()
    {
        ApplyControlState();
    }

    public void ApplyForTests()
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
        manualLift = 0f;
        manualTurn = Mathf.Clamp(turn, -1f, 1f);
        StopKeyboardThrustRepeat();
        keyboardLateralActive = false;
        keyboardLiftActive = false;
        keyboardTurnActive = false;
        statusMessage = "Direct controls updated.";
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
        NudgeTargetAltitude(Mathf.Sign(direction) * TargetAltitudeButtonStepMeters);
    }

    public void NudgeManualTurn(float direction)
    {
        manualTurn = Mathf.Clamp(manualTurn + Mathf.Sign(direction) * ManualLeverStep, -1f, 1f);
        headingMode = WildWindAxisControlMode.Manual;
        statusMessage = "Manual turn lever changed.";
    }

    public void SetAltitudeAssist(bool enabled)
    {
        altitudeMode = WildWindAxisControlMode.Assist;
        if (ResolveShip() != null)
        {
            EnsureTargetAltitudeInitialized();
        }
    }

    public void SetHeadingAssist(bool enabled)
    {
        headingMode = enabled ? WildWindAxisControlMode.Assist : WildWindAxisControlMode.Manual;
        if (enabled && ResolveShip() != null)
        {
            ship.targetHeading = NormalizeHeading(ship.transform.eulerAngles.y);
        }
    }

    public void SetSpeedAssist(bool enabled)
    {
        speedMode = enabled ? WildWindAxisControlMode.Autopilot : WildWindAxisControlMode.Manual;
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
        altitudeMode = enabled ? WildWindAxisControlMode.Autopilot : WildWindAxisControlMode.Assist;
        if (!enabled)
        {
            EnsureTargetAltitudeInitialized();
        }
    }

    public void SetAutopilotHeading(bool enabled)
    {
        headingMode = enabled ? WildWindAxisControlMode.Autopilot : WildWindAxisControlMode.Manual;
    }

    public void SetAutopilotSpeed(bool enabled)
    {
        speedMode = enabled ? WildWindAxisControlMode.Autopilot : WildWindAxisControlMode.Manual;
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
        altitudeMode = altitudeMode == WildWindAxisControlMode.Autopilot
            ? WildWindAxisControlMode.Assist
            : WildWindAxisControlMode.Autopilot;
        if (altitudeMode == WildWindAxisControlMode.Assist)
        {
            EnsureTargetAltitudeInitialized();
        }
    }

    public void CycleHeadingMode()
    {
        headingMode = NextMode(headingMode);
        if (headingMode == WildWindAxisControlMode.Assist && ResolveShip() != null)
        {
            ship.targetHeading = NormalizeHeading(ship.transform.eulerAngles.y);
        }
    }

    public void CycleSpeedMode()
    {
        speedMode = speedMode == WildWindAxisControlMode.Autopilot
            ? WildWindAxisControlMode.Manual
            : WildWindAxisControlMode.Autopilot;
    }

    public void ToggleAutoDockOnArrival()
    {
        autoDockOnArrival = !autoDockOnArrival;
        autoDockAttempted = false;
    }

    public void ToggleAfterburner()
    {
        if (ResolveShip() == null)
        {
            statusMessage = "Flight controls have no ship.";
            return;
        }

        ship.engineAfterburnerEnabled = !ship.engineAfterburnerEnabled;
        if (!ship.engineAfterburnerEnabled)
        {
            ship.enginePowerLever = Mathf.Min(ship.enginePowerLever, ship.EnginePowerLeverLimit);
        }

        statusMessage = ship.engineAfterburnerEnabled
            ? "Afterburner enabled."
            : "Afterburner disabled.";
    }

    public void NudgeTargetAltitude(float deltaMeters)
    {
        if (ResolveShip() == null) return;
        EnsureTargetAltitudeInitialized();
        SetTargetAltitude(ship.targetAltitude + deltaMeters);
    }

    public void SetTargetAltitude(float altitudeMeters)
    {
        if (ResolveShip() == null) return;
        ship.targetAltitude = Mathf.Max(0f, altitudeMeters);
        altitudeMode = WildWindAxisControlMode.Assist;
        statusMessage = "Target altitude assigned.";
    }

    public void NudgeTargetHeading(float deltaDegrees)
    {
        if (ResolveShip() == null) return;
        ship.targetHeading = NormalizeHeading(ship.targetHeading + deltaDegrees);
        if (headingMode == WildWindAxisControlMode.Manual)
        {
            headingMode = WildWindAxisControlMode.Assist;
        }
    }

    public void NudgeTargetSpeed(float deltaMS)
    {
        targetSpeedMS = ClampTargetSpeed(targetSpeedMS + deltaMS);
        if (speedMode != WildWindAxisControlMode.Autopilot)
        {
            speedMode = WildWindAxisControlMode.Autopilot;
        }
    }

    public bool SetTargetFromActiveTask()
    {
        MissionController activeMission = FindFirstObjectByType<MissionController>();
        if (activeMission != null && activeMission.IsActive)
        {
            SetAutopilotTarget(activeMission.GetDestinationPosition(), "", DockingLocationKind.Island);
            statusMessage = "Autopilot target copied from active mission.";
            return true;
        }

        ResolveReferences();
        PlayerProgress progress = meta != null ? meta.progress : null;
        string destinationDockId = WildWindStarterDelivery.GetActiveDestinationDockId(progress);
        if (string.IsNullOrWhiteSpace(destinationDockId))
        {
            statusMessage = "No active task target found.";
            return false;
        }

        DockingPort dock = FindDock(destinationDockId);
        if (dock != null)
        {
            SetAutopilotTarget(dock.DockPosition, dock.dockId, dock.kind);
            statusMessage = "Autopilot target copied from intro delivery.";
            return true;
        }

        IslandConfig island = meta != null && meta.WorldConfig != null
            ? meta.WorldConfig.GetIsland(destinationDockId)
            : null;
        if (island == null)
        {
            statusMessage = "No active task target found.";
            return false;
        }

        SetAutopilotTarget(island.position, destinationDockId, DockingLocationKind.Island);
        statusMessage = "Autopilot target copied from intro delivery config.";
        return true;
    }

    public void SetTargetAtShipPosition()
    {
        ShipPhysics currentShip = ResolveShip();
        if (currentShip == null)
        {
            statusMessage = "No ship for target placement.";
            return;
        }

        SetAutopilotTarget(currentShip.transform.position, "", DockingLocationKind.Island);
        statusMessage = "Autopilot target moved to ship position.";
    }

    public void SetManualTargetCoordinates(float x, float y, float z)
    {
        SetAutopilotTarget(new Vector3(x, y, z), "", DockingLocationKind.Island);
        statusMessage = "Manual autopilot target assigned.";
    }

    public void MoveTarget(Vector3 delta)
    {
        if (!hasAutopilotTarget)
        {
            SetTargetAtShipPosition();
        }

        autopilotTarget += delta;
        targetDock = null;
        autopilotTargetDockId = "";
        autoDockAttempted = false;
        statusMessage = "Autopilot target moved.";
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
        autoDockAttempted = false;
        statusMessage = "Autopilot target cleared.";
    }

    public void ClearAllControlModes()
    {
        altitudeMode = WildWindAxisControlMode.Assist;
        headingMode = WildWindAxisControlMode.Manual;
        speedMode = WildWindAxisControlMode.Manual;
        autoDockOnArrival = false;
        ClearDirectInput();
        ClearKeyboardInputFlags();
        EnsureTargetAltitudeInitialized();
    }

    public string GetControlSummary()
    {
        string targetText = hasAutopilotTarget
            ? autopilotTarget.x.ToString("0") + ", " + autopilotTarget.y.ToString("0") + ", " + autopilotTarget.z.ToString("0")
            : "no target";
        string autopilot = IsFullAutopilot ? "full" : HasAnySemiAutopilot() ? "semi" : "off";
        string connection = IsConnectedToGameplayShip ? "ship ok" : "ship ?";
        return "T " + manualThrust.ToString("0.0") +
            " (" + manualThrustNotch + ")" +
            " / S " + manualLateral.ToString("0.0") +
            " / R " + manualTurn.ToString("0.0") +
            " | Alt " + TargetAltitude.ToString("0") +
            " | " + altitudeMode + "/" + headingMode + "/" + speedMode +
            " | AP " + autopilot +
            " | " + connection +
            " | " + targetText;
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
        if (IsFullAutopilot)
        {
            ClearKeyboardManualAxis(ref keyboardLateralActive, ref manualLateral);
            return;
        }

        ApplyManualKeyboardAxis(axis, ref keyboardLateralActive, ref manualLateral);
    }

    private void ApplyAltitudeAxisInput(float axis, float deltaSeconds)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);
        if (altitudeMode == WildWindAxisControlMode.Autopilot)
        {
            ClearKeyboardManualAxis(ref keyboardLiftActive, ref manualLift);
            return;
        }

        ClearKeyboardManualAxis(ref keyboardLiftActive, ref manualLift);
        if (Mathf.Abs(axis) > 0.001f && ResolveShip() != null)
        {
            altitudeMode = WildWindAxisControlMode.Assist;
            EnsureTargetAltitudeInitialized();
            float rate = ResolveControlSettings() != null
                ? controlSettings.FlightTargetAltitudeChangeMetersPerSecond
                : 70f;
            ship.targetAltitude = Mathf.Max(0f, ship.targetAltitude + axis * rate * deltaSeconds);
            statusMessage = "Target altitude changed.";
        }
    }

    private void ApplyHeadingAxisInput(float axis, float deltaSeconds)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);
        if (headingMode == WildWindAxisControlMode.Autopilot)
        {
            ClearKeyboardManualAxis(ref keyboardTurnActive, ref manualTurn);
            return;
        }

        if (headingMode == WildWindAxisControlMode.Assist)
        {
            ClearKeyboardManualAxis(ref keyboardTurnActive, ref manualTurn);
            if (Mathf.Abs(axis) > 0.001f && ResolveShip() != null)
            {
                float rate = ResolveControlSettings() != null
                    ? controlSettings.FlightTargetHeadingChangeDegreesPerSecond
                    : 45f;
                ship.targetHeading = NormalizeHeading(ship.targetHeading + axis * rate * deltaSeconds);
                statusMessage = "Target heading changed.";
            }

            return;
        }

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
            }

            return;
        }

        ship.routeEnabled = false;
        ship.positionHold = false;

        Vector3 target = autopilotTarget;
        bool targetReady = hasAutopilotTarget;
        Vector3 toTarget = targetReady ? target - ship.transform.position : Vector3.zero;
        Vector3 flatToTarget = Flatten(toTarget);
        float distance = flatToTarget.magnitude;
        float desiredHeading = targetReady && distance > 0.1f
            ? HeadingFromVector(flatToTarget)
            : NormalizeHeading(ship.transform.eulerAngles.y);

        bool useAltitudeHold = true;
        bool useHeadingHold = HeadingAssistEnabled || (AutopilotHeadingEnabled && targetReady);
        bool useSpeedHold = SpeedAssistEnabled || (AutopilotSpeedEnabled && targetReady);

        if (altitudeMode == WildWindAxisControlMode.Manual)
        {
            altitudeMode = WildWindAxisControlMode.Assist;
        }

        EnsureTargetAltitudeInitialized();
        ship.altitudeHold = useAltitudeHold;
        ship.headingHold = useHeadingHold;
        ship.cruiseControl = useSpeedHold;

        if (AutopilotAltitudeEnabled && targetReady)
        {
            ship.targetAltitude = Mathf.Max(0f, target.y);
        }

        if (AutopilotHeadingEnabled && targetReady)
        {
            ship.targetHeading = desiredHeading;
        }

        if (AutopilotSpeedEnabled && targetReady)
        {
            float headingError = Mathf.Abs(Mathf.DeltaAngle(ship.transform.eulerAngles.y, desiredHeading));
            float facingFactor = Mathf.Clamp01(1f - Mathf.InverseLerp(10f, 75f, headingError));
            float brakingSpeed = CalculateArrivalSpeed(distance);
            ship.targetSpeedMS = Mathf.Min(Mathf.Max(0f, targetSpeedMS), brakingSpeed) * facingFactor;
        }
        else if (SpeedAssistEnabled)
        {
            ship.targetSpeedMS = ClampTargetSpeed(targetSpeedMS);
        }

        ship.sideInput = IsFullAutopilot ? 0f : manualLateral;
        ship.liftInput = 0f;

        if (!useHeadingHold)
        {
            ship.turnInput = manualTurn;
        }

        if (!useSpeedHold)
        {
            ship.thrustInput = manualThrust;
            ship.enginePowerLever = ship.CalculateEnginePowerLeverForPropellerEngagement(Mathf.Abs(manualThrust));
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
        SetManualThrustNotch(Mathf.RoundToInt(Mathf.Clamp(value, -1f, 1f) / ManualThrustNotchStep));
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
        manualThrust = manualThrustNotch * ManualThrustNotchStep;
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
        autopilotTarget = target;
        autopilotTargetDockId = dockId ?? "";
        autopilotTargetDockKind = dockKind;
        targetDock = !string.IsNullOrWhiteSpace(autopilotTargetDockId) ? FindDock(autopilotTargetDockId) : null;
        hasAutopilotTarget = true;
        autoDockAttempted = false;
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
        return AutopilotAltitudeEnabled || AutopilotHeadingEnabled || AutopilotSpeedEnabled;
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
