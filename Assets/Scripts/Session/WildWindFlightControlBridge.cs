using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public sealed class WildWindFlightControlBridge : MonoBehaviour
{
    private const string ObjectName = "Wild Wind Flight Control Bridge";
    private const int MinManualThrustNotch = -3;
    private const int MaxManualThrustNotch = 5;
    private const float ForwardManualThrustNotchStep = 0.2f;
    private const float ManualLeverStep = 0.25f;
    private const float ManualThrustHoldDelaySeconds = 0.5f;
    private const float ManualThrustRepeatSeconds = 0.3f;

    private MetaGameState meta;
    private WildWindGameplaySession session;
    private WildWindControlSettings controlSettings;
    private ShipPhysics ship;

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
    private string syncedSortieEntryKey = "";
    private string statusMessage = "";

    public bool IsReady => ResolveShip() != null;
    public ShipPhysics ControlledShip => ResolveShip();
    public bool IsConnectedToGameplayShip => IsConnectedToSessionShip();
    public bool ClaudiumSlipstreamEnabled => ResolveShip() != null && ship.claudiumSlipstreamEnabled;
    public float ClaudiumSlipstreamCharge01 => ResolveShip() != null ? ship.ClaudiumSlipstreamCharge01 : 0f;
    public float ManualThrust => manualThrust;
    public int ManualThrustNotch => manualThrustNotch;
    public float ManualLateral => manualLateral;
    public float ManualLift => manualLift;
    public float ManualTurn => manualTurn;
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
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
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
        statusMessage = "Manual lift input.";
    }

    public void ClearMomentaryLiftInput()
    {
        manualLift = 0f;
        keyboardLiftActive = false;
    }

    public void SetManualThrustNotchForHud(int notch)
    {
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
        statusMessage = "Manual thrust lever changed.";
    }

    public void NudgeManualLift(float direction)
    {
        manualLift = Mathf.Clamp(Mathf.Sign(direction), -1f, 1f);
        statusMessage = "Manual lift input changed.";
    }

    public void NudgeManualTurn(float direction)
    {
        manualTurn = Mathf.Clamp(manualTurn + Mathf.Sign(direction) * ManualLeverStep, -1f, 1f);
        statusMessage = "Manual turn lever changed.";
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

    public void ClearAllControlModes()
    {
        ClearDirectInput();
        ClearKeyboardInputFlags();
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

        Vector3 toTarget = Flatten(targetPosition - currentShip.transform.position);
        float entryHeading = toTarget.sqrMagnitude > 0.0001f
            ? HeadingFromVector(toTarget)
            : NormalizeHeading(currentShip.transform.eulerAngles.y);
        currentShip.SetStrategicInputState(
            manualThrust,
            0f,
            0f,
            0f,
            false,
            false,
            currentShip.transform.position.y,
            false,
            entryHeading,
            false,
            0f);
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
            " | manual"
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
        ApplyManualKeyboardAxis(axis, ref keyboardLiftActive, ref manualLift);
        if (Mathf.Abs(axis) > 0.001f)
        {
            statusMessage = "Manual lift input.";
        }
    }

    private void ApplyHeadingAxisInput(float axis, float deltaSeconds)
    {
        axis = Mathf.Clamp(axis, -1f, 1f);
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
                ship.ClearStrategicInput();
            }

            return;
        }

        bool useHeadingHold = false;
        bool useSpeedHold = false;
        bool useStopGear = !useSpeedHold && Mathf.Approximately(manualThrust, 0f);

        ship.SetStrategicInputState(
            useStopGear ? 0f : manualThrust,
            manualLateral,
            manualLift,
            useHeadingHold ? 0f : manualTurn,
            useStopGear,
            false,
            ship.transform.position.y,
            useHeadingHold,
            ship.transform.eulerAngles.y,
            useSpeedHold,
            0f);
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
        if (ship.StrategicForwardInput < 0.9f)
        {
            return;
        }

        SetManualThrustNotch(MaxManualThrustNotch);
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
