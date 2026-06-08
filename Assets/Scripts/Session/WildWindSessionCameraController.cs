using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class WildWindSessionCameraController : MonoBehaviour
{
    private const float HardMinCameraDistanceMeters = 5f;
    private const float HardMaxCameraDistanceMeters = 2000f;
    private const float CalibratedWheelScrollUnitsAcrossZoomRange = 0.383f;
    private const float CameraWheelEffectivenessMultiplier = 20f;
    private const float FlightGunToStandardWheelUnits = 1.25f;
    private const float FlightStandardToHighWheelUnits = 2.25f;
    private const float FlightHighToScopeWheelUnits = 2f;
    private const float FlightGunViewHeightMeters = 1.2f;
    private const float FlightShipStandardHeightMeters = 18f;
    private const float FlightShipStandardBackOffsetMeters = 24f;
    private const float FlightShipHighLengthMultiplier = 2f;
    private const float FlightCameraWideFieldOfView = 55f;
    private const float FlightScopeReferenceRangeMeters = 20000f;
    private const float FlightScopeScreenWidthMetersAtReferenceRange = 1000f;
    private const float FlightScopeMinimumWheelEffectiveness = 0.2f;

    [SerializeField, HideInInspector] private Transform focus;
    [SerializeField, HideInInspector] private Camera sessionCamera;
    [SerializeField, HideInInspector] private SessionAtmosphereTuner sessionAtmosphereTuner;
    [SerializeField, HideInInspector] private WildWindControlSettings controlSettings;
    [SerializeField, HideInInspector] private WildWindGameplaySession gameplaySession;

    [SerializeField, HideInInspector, Range(50f, 4000f)] private float cruiseSpeedMetersPerSecond = 650f;
    [SerializeField, HideInInspector, Range(50f, 3000f)] private float verticalSpeedMetersPerSecond = 420f;
    [SerializeField, HideInInspector, Range(2f, 20f)] private float sprintMultiplier = 6f;

    [SerializeField, HideInInspector] private Vector3 cameraOffset = new Vector3(-760f, 240f, -820f);
    [SerializeField, HideInInspector] private Vector3 cameraLookOffset = new Vector3(120f, 20f, 120f);
    [SerializeField, HideInInspector, Range(0.01f, 1f)] private float followSharpness = 0.22f;
    [SerializeField, HideInInspector, Range(0.01f, 1f)] private float orbitSensitivity = 0.18f;
    [SerializeField, HideInInspector, Range(0.1f, 1f)] private float closeOrbitSensitivityScale = 0.35f;
    [SerializeField, HideInInspector, Range(1f, 4f)] private float farOrbitSensitivityScale = 2.2f;
    [SerializeField, HideInInspector, Range(0.05f, 2f)] private float scrollUnitsAcrossZoomRange = CalibratedWheelScrollUnitsAcrossZoomRange;
    [SerializeField, HideInInspector, Range(5f, 500f)] private float minCameraDistanceMeters = HardMinCameraDistanceMeters;
    [SerializeField, HideInInspector, Range(500f, 2000f)] private float maxCameraDistanceMeters = HardMaxCameraDistanceMeters;
    [SerializeField, HideInInspector, Range(5f, 2000f)] private float takeoffCameraDistanceMeters = 620f;
    [SerializeField, HideInInspector, Range(0.05f, 2f)] private float takeoffCameraBlendSeconds = 0.65f;
    [SerializeField, HideInInspector, Range(0.02f, 1.5f)] private float flightCameraPivotSmoothSeconds = 0.22f;
    [SerializeField, HideInInspector, Range(0.04f, 2f)] private float flightCameraVerticalPivotSmoothSeconds = 0.62f;
    [SerializeField, HideInInspector, Range(0.04f, 2f)] private float flightCameraHeightSmoothSeconds = 0.48f;
    [SerializeField, HideInInspector, Range(0.02f, 1f)] private float flightCameraRotationSmoothSeconds = 0.14f;
    [SerializeField, HideInInspector, Range(0.02f, 1.5f)] private float flightCameraOrbitHeadingSmoothSeconds = 0.38f;
    [SerializeField, HideInInspector] private bool rigidFlightCamera = true;
    [SerializeField, HideInInspector, Range(0f, 45f)] private float tailAutoAlignWindowDegrees = 15f;
    [SerializeField, HideInInspector, Range(0f, 1f)] private float tailAutoAlignDelaySeconds = 0.12f;
    [SerializeField, HideInInspector, Range(0.02f, 1.5f)] private float tailAutoAlignSmoothSeconds = 0.24f;
    [SerializeField, HideInInspector, Range(-20f, 80f)] private float minCameraPitchDegrees = 6f;
    [SerializeField, HideInInspector, Range(0f, 85f)] private float maxCameraPitchDegrees = 58f;

    [SerializeField, HideInInspector] private bool warshipsFlightMouseLook = true;
    [SerializeField, HideInInspector] private bool altReleasesCursor = true;
    [SerializeField, HideInInspector, Range(50f, 1800f)] private float flightAimLookAheadMeters = 650f;
    [SerializeField, HideInInspector, Range(-50f, 180f)] private float flightAimVerticalOffsetMeters = 28f;

    private Vector3 uiInput;
    private float uiInputUntilTime;
    private bool cameraOrbitInitialized;
    private float cameraOrbitYawDegrees;
    private float cameraOrbitPitchDegrees;
    private float cameraOrbitDistanceMeters;
    private Vector3 lastCameraOrbitPivot;
    private bool cameraWasOrbitingPlayerShip;
    private bool takeoffCameraBlendActive;
    private float takeoffCameraBlendStartedAt;
    private Vector3 takeoffCameraBlendStartPosition;
    private Quaternion takeoffCameraBlendStartRotation;
    private bool hasSmoothedCameraOrbitPivot;
    private Vector3 smoothedCameraOrbitPivot;
    private float smoothedCameraOrbitPivotYVelocity;
    private bool hasSmoothedCameraOrbitHeading;
    private float smoothedCameraOrbitHeadingDegrees;
    private float smoothedCameraOrbitHeadingVelocity;
    private float cameraHeightVelocity;
    private bool hasCameraOrbitManualDrag;
    private float lastCameraOrbitControlTime = -999f;
    private float cameraOrbitTailAlignVelocity;
    private bool ownsGameplayCursor;
    private CursorLockMode storedCursorLockState;
    private bool storedCursorVisible;
    private Vector3 lastCameraAimTarget;
    private float flightCameraWheelRouteUnits;
    private float flightCameraWheelRoute01;
    private bool ownsFlightCameraFieldOfView;
    private float storedFlightCameraFieldOfView;

    public static bool GameplayCursorLockedForMouseLook { get; private set; }
    public static bool GameplayCursorReleasedForUi { get; private set; }
    public static float GameplayMouseLookSensitivityScale { get; private set; } = 1f;

    public Transform CurrentMovementTarget => GetMovementTarget();
    public float CameraOrbitDistanceMeters => cameraOrbitDistanceMeters;
    public float CameraOrbitYawDegrees => cameraOrbitYawDegrees;
    public float CameraOrbitPitchDegrees => cameraOrbitPitchDegrees;
    public float TakeoffCameraDistanceMeters => takeoffCameraDistanceMeters;
    public float MinimumCameraDistanceMeters => GetMinimumCameraDistanceMeters();
    public float MaximumCameraDistanceMeters => GetMaximumCameraDistanceMeters();
    public Vector3 LastCameraOrbitPivot => lastCameraOrbitPivot;
    public Vector3 LastCameraAimTarget => lastCameraAimTarget;
    public float FlightCameraWheelRoute01 => flightCameraWheelRoute01;

    private void Awake()
    {
        ApplyWheelCalibrationMigration();
        ResolveReferences();
        ApplyCamera(true);
    }

    private void OnValidate()
    {
        ApplyWheelCalibrationMigration();
    }

    private void OnDisable()
    {
        RestoreGameplayCursorStateIfOwned();
        RestoreFlightCameraFieldOfView(true);
        GameplayMouseLookSensitivityScale = 1f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && ownsGameplayCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Update()
    {
        ResolveReferences();
        if (focus == null)
        {
            return;
        }

        Transform movementTarget = GetMovementTarget();
        if (movementTarget == null)
        {
            return;
        }

        Vector3 input = IsPlayerShipFlightTarget(movementTarget) ? Vector3.zero : ReadMovementInput();
        if (input.sqrMagnitude > 0.0001f)
        {
            if (!IsFlightPhysicsControlActive(movementTarget))
            {
                float speed = GetCruiseSpeed();
                if (IsSprintPressed())
                {
                    speed *= GetSprintMultiplier();
                }

                movementTarget.position += input * speed * Time.deltaTime;
                RotateTargetTowardInput(movementTarget, input);
            }
        }

        if (focus != null && movementTarget != focus)
        {
            focus.position = movementTarget.position;
        }

        HandleCameraTargetTransition(movementTarget);
        ApplyGameplayCursorState(movementTarget);
        UpdateGameplayMouseLookSensitivityScale(movementTarget);
        UpdateCameraOrbitInput(movementTarget);
    }

    private void LateUpdate()
    {
        ResolveReferences();
        Transform movementTarget = GetMovementTarget();
        if (movementTarget == null)
        {
            return;
        }

        if (focus != null && movementTarget != focus)
        {
            focus.position = movementTarget.position;
        }

        ApplyCamera(false, movementTarget);
        sessionAtmosphereTuner?.ApplyNow();
    }

    public void Configure(
        Transform newFocus,
        Camera newCamera,
        SessionAtmosphereTuner newSessionAtmosphereTuner,
        WildWindControlSettings newControlSettings)
    {
        focus = newFocus;
        sessionCamera = newCamera;
        sessionAtmosphereTuner = newSessionAtmosphereTuner;
        controlSettings = newControlSettings;
        ApplyCamera(true);
    }

    public void SetUiInput(Vector3 input)
    {
        uiInput = input.sqrMagnitude > 1f ? input.normalized : input;
        uiInputUntilTime = Time.unscaledTime + 0.25f;
    }

    public void ClearUiInput()
    {
        uiInput = Vector3.zero;
        uiInputUntilTime = 0f;
    }

    public void ApplyCameraForTests(bool snap)
    {
        ApplyCamera(snap, GetMovementTarget());
    }

    public void FocusOnPlayerShipAfterUndocking()
    {
        ResolveReferences();
        Transform target = GetMovementTarget();
        if (target == null)
        {
            return;
        }

        InitializeCameraOrbit(GetEffectiveCameraOffset());
        cameraOrbitDistanceMeters = Mathf.Clamp(
            takeoffCameraDistanceMeters,
            GetMinimumCameraDistanceMeters(),
            GetMaximumCameraDistanceMeters());
        flightCameraWheelRouteUnits = 0f;
        flightCameraWheelRoute01 = 0f;
        cameraWasOrbitingPlayerShip = IsPlayerShipFlightTarget(target);
        BeginTakeoffCameraBlend();
        ApplyCamera(false, target);
    }

    public void AdjustCameraOrbitForTests(float yawDeltaDegrees, float pitchDeltaDegrees, float zoomSteps)
    {
        InitializeCameraOrbit(GetEffectiveCameraOffset());
        cameraOrbitYawDegrees = NormalizeDegrees(cameraOrbitYawDegrees + yawDeltaDegrees);
        cameraOrbitPitchDegrees = Mathf.Clamp(cameraOrbitPitchDegrees + pitchDeltaDegrees, minCameraPitchDegrees, maxCameraPitchDegrees);
        Transform target = GetMovementTarget();
        ApplyCameraWheelZoomForTarget(target, zoomSteps);
        ApplyCamera(false, target);
    }

    public bool EvaluateGameplayCursorStateForTests(bool altPressed, out CursorLockMode lockState, out bool cursorVisible)
    {
        Transform target = GetMovementTarget();
        bool shouldControl = ShouldControlGameplayCursor(target);
        bool releaseCursor = shouldControl && altReleasesCursor && altPressed;
        ResolveGameplayCursorState(shouldControl, releaseCursor, out lockState, out cursorVisible);
        return shouldControl;
    }

    private Vector3 ReadMovementInput()
    {
        Vector3 input;
        if (controlSettings != null)
        {
            input = controlSettings.ReadSessionCameraInput();
        }
        else
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;

            if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.A)) x -= 1f;
            if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.D)) x += 1f;
            if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.S)) z -= 1f;
            if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.W)) z += 1f;
            if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.Q)) y -= GetVerticalInputRatio();
            if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.E)) y += GetVerticalInputRatio();

            input = new Vector3(x, y, z);
        }

        if (Time.unscaledTime <= uiInputUntilTime)
        {
            Vector3 effectiveUiInput = uiInput;
            effectiveUiInput.y *= GetVerticalInputRatio();
            input += effectiveUiInput;
        }
        else
        {
            uiInput = Vector3.zero;
        }

        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private bool IsSprintPressed()
    {
        if (controlSettings != null)
        {
            return controlSettings.IsSprintPressed();
        }

        return WildWindControlSettings.IsKeyPressed(WildWindInputKey.LeftShift) ||
            WildWindControlSettings.IsKeyPressed(WildWindInputKey.RightShift);
    }

    private float GetCruiseSpeed()
    {
        return controlSettings != null ? controlSettings.SessionCruiseSpeedMetersPerSecond : cruiseSpeedMetersPerSecond;
    }

    private float GetSprintMultiplier()
    {
        return controlSettings != null ? controlSettings.SessionSprintMultiplier : sprintMultiplier;
    }

    private float GetVerticalInputRatio()
    {
        return verticalSpeedMetersPerSecond / Mathf.Max(1f, GetCruiseSpeed());
    }

    private void ResolveReferences()
    {
        if (focus == null)
        {
            GameObject focusObject = GameObject.Find("Player Bubble Focus");
            focus = focusObject != null ? focusObject.transform : null;
        }

        if (sessionCamera == null)
        {
            sessionCamera = Camera.main;
        }

        if (sessionAtmosphereTuner == null)
        {
            sessionAtmosphereTuner = FindFirstObjectByType<SessionAtmosphereTuner>();
        }

        if (controlSettings == null)
        {
            WildWindSettingsRoot settings = FindFirstObjectByType<WildWindSettingsRoot>();
            controlSettings = settings != null ? settings.Controls : FindFirstObjectByType<WildWindControlSettings>();
        }

        if (gameplaySession == null)
        {
            gameplaySession = FindFirstObjectByType<WildWindGameplaySession>();
        }
    }

    private void UpdateCameraOrbitInput(Transform movementTarget)
    {
        InitializeCameraOrbit(GetEffectiveCameraOffset());

        Vector2 dragDelta = ReadCameraOrbitDragDelta(out bool orbitControlActive);
        bool hasDragDelta = dragDelta.sqrMagnitude > 0.0001f;
        if (orbitControlActive)
        {
            lastCameraOrbitControlTime = Time.unscaledTime;
            cameraOrbitTailAlignVelocity = 0f;
        }

        if (hasDragDelta)
        {
            float effectiveOrbitSensitivity = GetOrbitSensitivityForTarget(movementTarget, cameraOrbitDistanceMeters);
            cameraOrbitYawDegrees = NormalizeDegrees(cameraOrbitYawDegrees + dragDelta.x * effectiveOrbitSensitivity);
            cameraOrbitPitchDegrees = Mathf.Clamp(
                cameraOrbitPitchDegrees + dragDelta.y * effectiveOrbitSensitivity,
                minCameraPitchDegrees,
                maxCameraPitchDegrees);
            hasCameraOrbitManualDrag = true;
        }

        float scroll = ReadCameraZoomScroll();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            ApplyCameraWheelZoomForTarget(movementTarget, scroll);
            UpdateGameplayMouseLookSensitivityScale(movementTarget);
        }

        ApplyTailAutoAlignmentAfterOrbitInput(orbitControlActive);
    }

    private Vector3 GetEffectiveCameraOffset()
    {
        return controlSettings != null ? controlSettings.SessionCameraOffset : cameraOffset;
    }

    private Vector3 GetEffectiveCameraLookOffset()
    {
        return controlSettings != null ? controlSettings.SessionCameraLookOffset : cameraLookOffset;
    }

    private void InitializeCameraOrbit(Vector3 effectiveCameraOffset)
    {
        if (cameraOrbitInitialized)
        {
            return;
        }

        Vector3 offset = effectiveCameraOffset.sqrMagnitude > 1f ? effectiveCameraOffset : cameraOffset;
        cameraOrbitDistanceMeters = Mathf.Clamp(offset.magnitude, GetMinimumCameraDistanceMeters(), GetMaximumCameraDistanceMeters());
        if (cameraOrbitDistanceMeters <= 0.001f)
        {
            cameraOrbitDistanceMeters = Mathf.Clamp(900f, GetMinimumCameraDistanceMeters(), GetMaximumCameraDistanceMeters());
            offset = new Vector3(-760f, 240f, -820f);
        }

        cameraOrbitYawDegrees = NormalizeDegrees(Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg);
        cameraOrbitPitchDegrees = Mathf.Clamp(
            Mathf.Asin(Mathf.Clamp(offset.y / cameraOrbitDistanceMeters, -1f, 1f)) * Mathf.Rad2Deg,
            minCameraPitchDegrees,
            maxCameraPitchDegrees);
        cameraOrbitInitialized = true;
    }

    private Vector2 ReadCameraOrbitDragDelta(out bool orbitControlActive)
    {
        orbitControlActive = false;
        if (ShouldUseLockedFlightMouseLook())
        {
            orbitControlActive = true;
#if ENABLE_INPUT_SYSTEM
            Mouse lockedMouse = Mouse.current;
            if (lockedMouse != null)
            {
                return lockedMouse.delta.ReadValue();
            }
#endif

            return Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        Mouse dragMouse = Mouse.current;
        if (dragMouse != null && dragMouse.rightButton.isPressed)
        {
            orbitControlActive = true;
            return dragMouse.delta.ReadValue();
        }
#endif

        return Vector2.zero;
    }

    private void ApplyTailAutoAlignmentAfterOrbitInput(bool orbitControlActive)
    {
        if (orbitControlActive)
        {
            return;
        }

        Transform target = GetMovementTarget();
        bool isPlayerShipFlightTarget = IsPlayerShipFlightTarget(target);
        if (!isPlayerShipFlightTarget)
        {
            hasCameraOrbitManualDrag = false;
            cameraOrbitTailAlignVelocity = 0f;
            return;
        }

        if (!hasCameraOrbitManualDrag)
        {
            cameraOrbitTailAlignVelocity = 0f;
            return;
        }

        float alignWindow = Mathf.Max(0f, tailAutoAlignWindowDegrees);
        if (alignWindow <= 0.001f ||
            Time.unscaledTime - lastCameraOrbitControlTime < Mathf.Max(0f, tailAutoAlignDelaySeconds))
        {
            return;
        }

        float tailDelta = Mathf.DeltaAngle(cameraOrbitYawDegrees, 0f);
        if (Mathf.Abs(tailDelta) > alignWindow)
        {
            cameraOrbitTailAlignVelocity = 0f;
            return;
        }

        cameraOrbitYawDegrees = NormalizeDegrees(Mathf.SmoothDampAngle(
            cameraOrbitYawDegrees,
            0f,
            ref cameraOrbitTailAlignVelocity,
            Mathf.Max(0.001f, tailAutoAlignSmoothSeconds),
            float.PositiveInfinity,
            GetCameraDeltaTime()));

        if (Mathf.Abs(Mathf.DeltaAngle(cameraOrbitYawDegrees, 0f)) <= 0.05f &&
            Mathf.Abs(cameraOrbitTailAlignVelocity) <= 0.05f)
        {
            cameraOrbitYawDegrees = 0f;
            cameraOrbitTailAlignVelocity = 0f;
        }
    }

    private float ReadCameraZoomScroll()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            return mouse.scroll.ReadValue().y / 120f;
        }
#endif

        return 0f;
    }

    private void ApplyCamera(bool snap)
    {
        ApplyCamera(snap, GetMovementTarget());
    }

    private void ApplyCamera(bool snap, Transform target)
    {
        if (sessionCamera == null || target == null)
        {
            return;
        }

        InitializeCameraOrbit(GetEffectiveCameraOffset());
        float effectiveFollowSharpness = controlSettings != null ? controlSettings.SessionCameraFollowSharpness : followSharpness;
        Quaternion orbitFrame = GetCameraOrbitFrame(target, snap);

        Vector3 rawOrbitPivot = GetCameraOrbitPivot(target);
        lastCameraOrbitPivot = GetSmoothedCameraOrbitPivot(target, rawOrbitPivot, snap);
        lastCameraAimTarget = GetCameraAimTarget(target, lastCameraOrbitPivot, orbitFrame);
        Vector3 targetPosition = IsPlayerShipFlightTarget(target)
            ? GetFlightWheelCameraPosition(target, lastCameraOrbitPivot, lastCameraAimTarget, orbitFrame)
            : GetOrbitCameraPosition(lastCameraOrbitPivot, orbitFrame);
        Quaternion targetRotation = GetCameraLookRotation(targetPosition, lastCameraAimTarget);

        if (!snap && TryApplyTakeoffCameraBlend(targetPosition, targetRotation))
        {
            return;
        }

        Vector3 nextPosition = snap
            ? targetPosition
            : GetSmoothedCameraPosition(target, targetPosition, effectiveFollowSharpness);
        sessionCamera.transform.position = nextPosition;
        Quaternion lookRotation = GetCameraLookRotation(nextPosition, lastCameraAimTarget);
        sessionCamera.transform.rotation = snap
            ? lookRotation
            : GetSmoothedCameraRotation(target, lookRotation);
        ApplyFlightCameraFieldOfView(target, snap);
    }

    private Transform GetMovementTarget()
    {
        if (gameplaySession != null &&
            gameplaySession.CurrentMode == GameSessionMode.Flight &&
            gameplaySession.PlayerShipRoot != null)
        {
            return gameplaySession.PlayerShipRoot;
        }

        return focus;
    }

    private bool IsFlightPhysicsControlActive(Transform movementTarget)
    {
        return gameplaySession != null &&
            gameplaySession.CurrentMode == GameSessionMode.Flight &&
            movementTarget != null &&
            movementTarget.GetComponent<ShipPhysics>() != null &&
            FindFirstObjectByType<WildWindFlightControlBridge>() != null;
    }

    private Vector3 GetCameraOrbitPivot(Transform target)
    {
        if (IsPlayerShipFlightTarget(target))
        {
            return target.position;
        }

        return target.position + GetEffectiveCameraLookOffset();
    }

    private Vector3 GetCameraAimTarget(Transform target, Vector3 orbitPivot, Quaternion orbitFrame)
    {
        if (!IsPlayerShipFlightTarget(target))
        {
            return orbitPivot;
        }

        if (TryGetManualGunCameraAimTarget(target, out Vector3 manualGunAimTarget))
        {
            return manualGunAimTarget;
        }

        Vector3 aimForward = orbitFrame * (Quaternion.Euler(0f, cameraOrbitYawDegrees, 0f) * Vector3.forward);
        aimForward = Vector3.ProjectOnPlane(aimForward, Vector3.up);
        if (aimForward.sqrMagnitude <= 0.001f)
        {
            aimForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        }

        if (aimForward.sqrMagnitude <= 0.001f)
        {
            aimForward = Vector3.forward;
        }

        float lookAheadMeters = GetFlightAimLookAheadMeters();
        return orbitPivot +
            aimForward.normalized * lookAheadMeters +
            Vector3.up * flightAimVerticalOffsetMeters;
    }

    private Vector3 GetOrbitCameraPosition(Vector3 orbitPivot, Quaternion orbitFrame)
    {
        Vector3 localOrbitOffset = Quaternion.Euler(cameraOrbitPitchDegrees, cameraOrbitYawDegrees, 0f) *
            (Vector3.back * cameraOrbitDistanceMeters);
        return orbitPivot + orbitFrame * localOrbitOffset;
    }

    private Vector3 GetFlightWheelCameraPosition(Transform target, Vector3 orbitPivot, Vector3 aimTarget, Quaternion orbitFrame)
    {
        Vector3 shipForward = ResolveFlightShipFlatForward(target);
        Vector3 gunAnchor = TryGetManualGunCameraAnchor(target, out Vector3 manualGunAnchor)
            ? manualGunAnchor
            : orbitPivot + shipForward * 12f;

        Vector3 gunViewPosition = gunAnchor + Vector3.up * FlightGunViewHeightMeters;
        Vector3 shipStandardPosition = orbitPivot +
            Vector3.up * FlightShipStandardHeightMeters -
            shipForward * FlightShipStandardBackOffsetMeters;
        Vector3 shipHighPosition = orbitPivot +
            Vector3.up * ResolveFlightShipHighHeightMeters(target) -
            shipForward * FlightShipStandardBackOffsetMeters;

        float route = Mathf.Clamp(flightCameraWheelRouteUnits, 0f, GetFlightCameraWheelTotalUnits());
        float gunEnd = GetFlightGunToStandardWheelUnits();
        float highEnd = gunEnd + GetFlightStandardToHighWheelUnits();
        if (route <= gunEnd)
        {
            float t = Mathf.SmoothStep(0f, 1f, route / gunEnd);
            return Vector3.Lerp(gunViewPosition, shipStandardPosition, t);
        }

        if (route <= highEnd)
        {
            float t = Mathf.SmoothStep(0f, 1f, (route - gunEnd) / (highEnd - gunEnd));
            return Vector3.Lerp(shipStandardPosition, shipHighPosition, t);
        }

        return shipHighPosition;
    }

    private float ResolveFlightShipHighHeightMeters(Transform target)
    {
        float shipLength = ResolveFlightShipLengthMeters(target);
        float highHeight = shipLength * FlightShipHighLengthMultiplier;
        return Mathf.Max(FlightShipStandardHeightMeters, highHeight);
    }

    private static float ResolveFlightShipLengthMeters(Transform target)
    {
        if (target == null)
        {
            return 20f;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return 20f;
        }

        Vector3 size = bounds.size;
        float length = Mathf.Max(size.x, size.z);
        return Mathf.Clamp(length, 8f, 160f);
    }

    private Vector3 ResolveFlightCameraFlatForward(Transform target, Quaternion orbitFrame)
    {
        Vector3 flatForward = orbitFrame * (Quaternion.Euler(0f, cameraOrbitYawDegrees, 0f) * Vector3.forward);
        flatForward = Vector3.ProjectOnPlane(flatForward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.001f && target != null)
        {
            flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        }

        if (flatForward.sqrMagnitude <= 0.001f)
        {
            flatForward = Vector3.forward;
        }

        return flatForward.normalized;
    }

    private static Vector3 ResolveFlightShipFlatForward(Transform target)
    {
        Vector3 flatForward = target != null
            ? Vector3.ProjectOnPlane(target.forward, Vector3.up)
            : Vector3.zero;
        if (flatForward.sqrMagnitude <= 0.001f)
        {
            flatForward = Vector3.forward;
        }

        return flatForward.normalized;
    }

    private static bool TryGetManualGunCameraAimTarget(Transform target, out Vector3 aimTarget)
    {
        aimTarget = Vector3.zero;
        if (target == null)
        {
            return false;
        }

        ShipPhysics ship = target.GetComponent<ShipPhysics>();
        return ship != null && ship.TryGetManualGunCameraAimTarget(out aimTarget);
    }

    private static bool TryGetManualGunCameraAnchor(Transform target, out Vector3 anchor)
    {
        anchor = Vector3.zero;
        if (target == null)
        {
            return false;
        }

        ShipPhysics ship = target.GetComponent<ShipPhysics>();
        return ship != null && ship.TryGetManualGunCameraAnchor(out anchor);
    }

    private float GetFlightAimLookAheadMeters()
    {
        float scaledLookAhead = Mathf.Max(flightAimLookAheadMeters, cameraOrbitDistanceMeters * 0.7f);
        return Mathf.Clamp(scaledLookAhead, 50f, 1800f);
    }

    private Vector3 GetSmoothedCameraOrbitPivot(Transform target, Vector3 rawPivot, bool snap)
    {
        bool smoothFlightPivot = IsPlayerShipFlightTarget(target);
        if (UseRigidFlightCamera(target))
        {
            smoothedCameraOrbitPivot = rawPivot;
            smoothedCameraOrbitPivotYVelocity = 0f;
            cameraHeightVelocity = 0f;
            hasSmoothedCameraOrbitPivot = true;
            return rawPivot;
        }

        float resetDistance = Mathf.Max(80f, cameraOrbitDistanceMeters * 0.4f);
        if (!smoothFlightPivot || snap || !hasSmoothedCameraOrbitPivot ||
            (rawPivot - smoothedCameraOrbitPivot).sqrMagnitude > resetDistance * resetDistance)
        {
            smoothedCameraOrbitPivot = rawPivot;
            smoothedCameraOrbitPivotYVelocity = 0f;
            cameraHeightVelocity = 0f;
            hasSmoothedCameraOrbitPivot = smoothFlightPivot;
            return rawPivot;
        }

        float dt = GetCameraDeltaTime();
        float smoothSeconds = Mathf.Max(0.001f, flightCameraPivotSmoothSeconds);
        float alpha = 1f - Mathf.Exp(-dt / smoothSeconds);
        Vector3 nextPivot = Vector3.Lerp(smoothedCameraOrbitPivot, rawPivot, alpha);
        nextPivot.y = Mathf.SmoothDamp(
            smoothedCameraOrbitPivot.y,
            rawPivot.y,
            ref smoothedCameraOrbitPivotYVelocity,
            Mathf.Max(0.001f, flightCameraVerticalPivotSmoothSeconds),
            float.PositiveInfinity,
            dt);
        smoothedCameraOrbitPivot = nextPivot;
        return smoothedCameraOrbitPivot;
    }

    private Vector3 GetSmoothedCameraPosition(Transform target, Vector3 targetPosition, float effectiveFollowSharpness)
    {
        if (UseRigidFlightCamera(target))
        {
            cameraHeightVelocity = 0f;
            return targetPosition;
        }

        Vector3 currentPosition = sessionCamera.transform.position;
        Vector3 nextPosition = Vector3.Lerp(currentPosition, targetPosition, effectiveFollowSharpness);
        if (!IsPlayerShipFlightTarget(target))
        {
            cameraHeightVelocity = 0f;
            return nextPosition;
        }

        nextPosition.y = Mathf.SmoothDamp(
            currentPosition.y,
            targetPosition.y,
            ref cameraHeightVelocity,
            Mathf.Max(0.001f, flightCameraHeightSmoothSeconds),
            float.PositiveInfinity,
            GetCameraDeltaTime());
        return nextPosition;
    }

    private Quaternion GetSmoothedCameraRotation(Transform target, Quaternion targetRotation)
    {
        if (UseRigidFlightCamera(target))
        {
            return targetRotation;
        }

        if (!IsPlayerShipFlightTarget(target))
        {
            return targetRotation;
        }

        float smoothSeconds = Mathf.Max(0.001f, flightCameraRotationSmoothSeconds);
        float alpha = 1f - Mathf.Exp(-GetCameraDeltaTime() / smoothSeconds);
        return Quaternion.Slerp(sessionCamera.transform.rotation, targetRotation, alpha);
    }

    private static float GetCameraDeltaTime()
    {
        return Mathf.Max(0.0001f, Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime));
    }

    private void BeginTakeoffCameraBlend()
    {
        if (sessionCamera == null)
        {
            return;
        }

        takeoffCameraBlendActive = true;
        takeoffCameraBlendStartedAt = Time.unscaledTime;
        takeoffCameraBlendStartPosition = sessionCamera.transform.position;
        takeoffCameraBlendStartRotation = sessionCamera.transform.rotation;
    }

    private bool TryApplyTakeoffCameraBlend(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (!takeoffCameraBlendActive)
        {
            return false;
        }

        float duration = Mathf.Max(0.01f, takeoffCameraBlendSeconds);
        float ratio = Mathf.Clamp01((Time.unscaledTime - takeoffCameraBlendStartedAt) / duration);
        float easedRatio = Mathf.SmoothStep(0f, 1f, ratio);

        sessionCamera.transform.position = Vector3.Lerp(takeoffCameraBlendStartPosition, targetPosition, easedRatio);
        sessionCamera.transform.rotation = Quaternion.Slerp(takeoffCameraBlendStartRotation, targetRotation, easedRatio);

        if (ratio >= 1f)
        {
            takeoffCameraBlendActive = false;
        }

        return true;
    }

    private static Quaternion GetCameraLookRotation(Vector3 cameraPosition, Vector3 pivot)
    {
        Vector3 direction = pivot - cameraPosition;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : Quaternion.identity;
    }

    private Quaternion GetCameraOrbitFrame(Transform target, bool snap)
    {
        if (!IsPlayerShipFlightTarget(target))
        {
            hasSmoothedCameraOrbitHeading = false;
            smoothedCameraOrbitHeadingVelocity = 0f;
            return Quaternion.identity;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.001f)
        {
            return Quaternion.Euler(0f, smoothedCameraOrbitHeadingDegrees, 0f);
        }

        float rawHeading = NormalizeDegrees(Mathf.Atan2(flatForward.x, flatForward.z) * Mathf.Rad2Deg);
        if (UseRigidFlightCamera(target))
        {
            smoothedCameraOrbitHeadingDegrees = rawHeading;
            smoothedCameraOrbitHeadingVelocity = 0f;
            hasSmoothedCameraOrbitHeading = true;
        }
        else if (snap || !hasSmoothedCameraOrbitHeading)
        {
            smoothedCameraOrbitHeadingDegrees = rawHeading;
            smoothedCameraOrbitHeadingVelocity = 0f;
            hasSmoothedCameraOrbitHeading = true;
        }
        else
        {
            smoothedCameraOrbitHeadingDegrees = Mathf.SmoothDampAngle(
                smoothedCameraOrbitHeadingDegrees,
                rawHeading,
                ref smoothedCameraOrbitHeadingVelocity,
                Mathf.Max(0.001f, flightCameraOrbitHeadingSmoothSeconds),
                float.PositiveInfinity,
                GetCameraDeltaTime());
        }

        return Quaternion.Euler(0f, smoothedCameraOrbitHeadingDegrees, 0f);
    }

    private bool UseRigidFlightCamera(Transform target)
    {
        return rigidFlightCamera && IsPlayerShipFlightTarget(target);
    }

    private void HandleCameraTargetTransition(Transform movementTarget)
    {
        bool orbitingPlayerShip = IsPlayerShipFlightTarget(movementTarget);
        if (orbitingPlayerShip && !cameraWasOrbitingPlayerShip)
        {
            FocusOnPlayerShipAfterUndocking();
        }

        cameraWasOrbitingPlayerShip = orbitingPlayerShip;
    }

    private bool IsPlayerShipFlightTarget(Transform target)
    {
        return gameplaySession != null &&
            gameplaySession.CurrentMode == GameSessionMode.Flight &&
            target != null &&
            gameplaySession.PlayerShipRoot == target;
    }

    private bool ShouldControlGameplayCursor(Transform target)
    {
        return warshipsFlightMouseLook && IsPlayerShipFlightTarget(target);
    }

    private bool ShouldUseLockedFlightMouseLook()
    {
        Transform target = GetMovementTarget();
        return ShouldControlGameplayCursor(target) &&
            !(altReleasesCursor && WildWindControlSettings.IsGameplayCursorReleasePressed());
    }

    private void ApplyGameplayCursorState(Transform target)
    {
        bool shouldControl = ShouldControlGameplayCursor(target);
        bool releaseCursor = shouldControl &&
            altReleasesCursor &&
            WildWindControlSettings.IsGameplayCursorReleasePressed();

        if (!shouldControl)
        {
            RestoreGameplayCursorStateIfOwned();
            GameplayCursorLockedForMouseLook = false;
            GameplayCursorReleasedForUi = false;
            GameplayMouseLookSensitivityScale = 1f;
            return;
        }

        if (!ownsGameplayCursor)
        {
            storedCursorLockState = Cursor.lockState;
            storedCursorVisible = Cursor.visible;
            ownsGameplayCursor = true;
        }

        ResolveGameplayCursorState(shouldControl, releaseCursor, out CursorLockMode lockState, out bool cursorVisible);
        Cursor.lockState = lockState;
        Cursor.visible = cursorVisible;
        GameplayCursorLockedForMouseLook = lockState == CursorLockMode.Locked;
        GameplayCursorReleasedForUi = releaseCursor;
    }

    private void UpdateGameplayMouseLookSensitivityScale(Transform target)
    {
        GameplayMouseLookSensitivityScale = IsPlayerShipFlightTarget(target)
            ? GetFlightMouseLookSensitivityScale()
            : 1f;
    }

    private float GetFlightMouseLookSensitivityScale()
    {
        float scopeRatio = GetFlightCameraScopeRatio();
        if (scopeRatio <= 0.001f)
        {
            return 1f;
        }

        float currentVerticalFovDegrees = Mathf.Lerp(
            FlightCameraWideFieldOfView,
            GetFlightScopeVerticalFieldOfView(),
            scopeRatio);
        return GetFieldOfViewSensitivityScale(FlightCameraWideFieldOfView, currentVerticalFovDegrees);
    }

    private static float GetFieldOfViewSensitivityScale(float referenceVerticalFovDegrees, float currentVerticalFovDegrees)
    {
        float referenceTangent = Mathf.Tan(Mathf.Clamp(referenceVerticalFovDegrees, 0.5f, 179f) * 0.5f * Mathf.Deg2Rad);
        float currentTangent = Mathf.Tan(Mathf.Clamp(currentVerticalFovDegrees, 0.5f, 179f) * 0.5f * Mathf.Deg2Rad);
        return referenceTangent > 0.0001f
            ? Mathf.Clamp(currentTangent / referenceTangent, 0.001f, 1f)
            : 1f;
    }

    private void RestoreGameplayCursorStateIfOwned()
    {
        if (!ownsGameplayCursor)
        {
            return;
        }

        Cursor.lockState = storedCursorLockState;
        Cursor.visible = storedCursorVisible;
        ownsGameplayCursor = false;
        GameplayCursorLockedForMouseLook = false;
        GameplayCursorReleasedForUi = false;
        GameplayMouseLookSensitivityScale = 1f;
    }

    private void ApplyCameraWheelZoomForTarget(Transform target, float scrollDelta)
    {
        if (IsPlayerShipFlightTarget(target))
        {
            ApplyFlightCameraWheelRoute(scrollDelta * CameraWheelEffectivenessMultiplier);
            return;
        }

        ApplyProgressiveZoom(scrollDelta * CameraWheelEffectivenessMultiplier);
    }

    private void ApplyFlightCameraWheelRoute(float scrollDelta)
    {
        if (Mathf.Abs(scrollDelta) <= 0.001f)
        {
            return;
        }

        flightCameraWheelRouteUnits = ApplyFlightCameraWheelRouteDelta(flightCameraWheelRouteUnits, scrollDelta);
        flightCameraWheelRoute01 = GetFlightCameraWheelRoute01();
    }

    private float ApplyFlightCameraWheelRouteDelta(float currentRouteUnits, float scrollDelta)
    {
        float total = GetFlightCameraWheelTotalUnits();
        float scopeStart = GetFlightScopeStartWheelUnits();
        float clampedRoute = Mathf.Clamp(currentRouteUnits, 0f, total);
        if (scrollDelta > 0f)
        {
            return ApplyPositiveFlightCameraWheelRouteDelta(clampedRoute, scrollDelta, scopeStart, total);
        }

        return ApplyNegativeFlightCameraWheelRouteDelta(clampedRoute, scrollDelta, scopeStart, total);
    }

    private float ApplyPositiveFlightCameraWheelRouteDelta(float currentRouteUnits, float scrollDelta, float scopeStart, float total)
    {
        if (currentRouteUnits < scopeStart)
        {
            float deltaToScopeStart = scopeStart - currentRouteUnits;
            if (scrollDelta <= deltaToScopeStart)
            {
                return Mathf.Clamp(currentRouteUnits + scrollDelta, 0f, total);
            }

            currentRouteUnits = scopeStart;
            scrollDelta -= deltaToScopeStart;
        }

        if (currentRouteUnits >= total)
        {
            return total;
        }

        float rawDeltaToMaxScope = GetRawFlightScopeWheelDelta(currentRouteUnits, total, scopeStart, total);
        return scrollDelta >= rawDeltaToMaxScope
            ? total
            : ApplyFlightScopeWheelDampedDelta(currentRouteUnits, scrollDelta, scopeStart, total);
    }

    private float ApplyNegativeFlightCameraWheelRouteDelta(float currentRouteUnits, float scrollDelta, float scopeStart, float total)
    {
        if (currentRouteUnits > scopeStart)
        {
            float rawDeltaToScopeStart = GetRawFlightScopeWheelDelta(currentRouteUnits, scopeStart, scopeStart, total);
            if (scrollDelta >= rawDeltaToScopeStart)
            {
                return ApplyFlightScopeWheelDampedDelta(currentRouteUnits, scrollDelta, scopeStart, total);
            }

            currentRouteUnits = scopeStart;
            scrollDelta -= rawDeltaToScopeStart;
        }

        return Mathf.Clamp(currentRouteUnits + scrollDelta, 0f, total);
    }

    private float ApplyFlightScopeWheelDampedDelta(float currentRouteUnits, float scrollDelta, float scopeStart, float total)
    {
        float scopeLength = Mathf.Max(0.001f, total - scopeStart);
        float minimumEffectiveness = Mathf.Clamp(FlightScopeMinimumWheelEffectiveness, 0.01f, 1f);
        float reduction = 1f - minimumEffectiveness;
        float currentRatio = Mathf.InverseLerp(scopeStart, total, currentRouteUnits);
        if (reduction <= 0.0001f)
        {
            return Mathf.Clamp(currentRouteUnits + scrollDelta, scopeStart, total);
        }

        float remainingEffectiveness = 1f - reduction * currentRatio;
        float nextRatio = (1f - remainingEffectiveness * Mathf.Exp(-reduction * scrollDelta / scopeLength)) / reduction;
        return Mathf.Lerp(scopeStart, total, Mathf.Clamp01(nextRatio));
    }

    private float GetRawFlightScopeWheelDelta(float fromRouteUnits, float toRouteUnits, float scopeStart, float total)
    {
        float scopeLength = Mathf.Max(0.001f, total - scopeStart);
        float minimumEffectiveness = Mathf.Clamp(FlightScopeMinimumWheelEffectiveness, 0.01f, 1f);
        float reduction = 1f - minimumEffectiveness;
        float fromRatio = Mathf.InverseLerp(scopeStart, total, fromRouteUnits);
        float toRatio = Mathf.InverseLerp(scopeStart, total, toRouteUnits);
        if (reduction <= 0.0001f)
        {
            return (toRatio - fromRatio) * scopeLength;
        }

        float fromEffectiveness = Mathf.Max(0.001f, 1f - reduction * fromRatio);
        float toEffectiveness = Mathf.Max(0.001f, 1f - reduction * toRatio);
        return scopeLength / reduction * Mathf.Log(fromEffectiveness / toEffectiveness);
    }

    private void ApplyFlightCameraFieldOfView(Transform target, bool snap)
    {
        if (sessionCamera == null)
        {
            return;
        }

        if (!IsPlayerShipFlightTarget(target))
        {
            RestoreFlightCameraFieldOfView(snap);
            return;
        }

        if (!ownsFlightCameraFieldOfView)
        {
            storedFlightCameraFieldOfView = sessionCamera.fieldOfView;
            ownsFlightCameraFieldOfView = true;
        }

        float focusRatio = GetFlightCameraScopeRatio();
        float desiredFieldOfView = Mathf.Lerp(
            FlightCameraWideFieldOfView,
            GetFlightScopeVerticalFieldOfView(),
            focusRatio);
        sessionCamera.fieldOfView = snap
            ? desiredFieldOfView
            : Mathf.Lerp(sessionCamera.fieldOfView, desiredFieldOfView, 1f - Mathf.Exp(-GetCameraDeltaTime() / 0.08f));
    }

    private float GetFlightScopeVerticalFieldOfView()
    {
        float referenceRange = Mathf.Max(1f, FlightScopeReferenceRangeMeters);
        float screenWidth = Mathf.Clamp(
            FlightScopeScreenWidthMetersAtReferenceRange,
            1f,
            referenceRange * 2f);
        float aspect = sessionCamera != null
            ? Mathf.Max(0.01f, sessionCamera.aspect)
            : 16f / 9f;

        float horizontalRadians = 2f * Mathf.Atan((screenWidth * 0.5f) / referenceRange);
        float verticalRadians = 2f * Mathf.Atan(Mathf.Tan(horizontalRadians * 0.5f) / aspect);
        return Mathf.Clamp(verticalRadians * Mathf.Rad2Deg, 0.5f, FlightCameraWideFieldOfView);
    }

    private void RestoreFlightCameraFieldOfView(bool snap)
    {
        if (!ownsFlightCameraFieldOfView || sessionCamera == null)
        {
            return;
        }

        if (snap)
        {
            sessionCamera.fieldOfView = storedFlightCameraFieldOfView;
            ownsFlightCameraFieldOfView = false;
            return;
        }

        sessionCamera.fieldOfView = Mathf.Lerp(
            sessionCamera.fieldOfView,
            storedFlightCameraFieldOfView,
            1f - Mathf.Exp(-GetCameraDeltaTime() / 0.12f));
        if (Mathf.Abs(sessionCamera.fieldOfView - storedFlightCameraFieldOfView) <= 0.05f)
        {
            sessionCamera.fieldOfView = storedFlightCameraFieldOfView;
            ownsFlightCameraFieldOfView = false;
        }
    }

    private float GetFlightCameraWheelRoute01()
    {
        float total = GetFlightCameraWheelTotalUnits();
        return total > 0.001f ? Mathf.Clamp01(flightCameraWheelRouteUnits / total) : 0f;
    }

    private float GetFlightCameraScopeRatio()
    {
        float scopeStart = GetFlightScopeStartWheelUnits();
        float total = GetFlightCameraWheelTotalUnits();
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(scopeStart, total, flightCameraWheelRouteUnits));
    }

    private float GetFlightScopeStartWheelUnits()
    {
        return GetFlightGunToStandardWheelUnits() + GetFlightStandardToHighWheelUnits();
    }

    private float GetFlightCameraWheelTotalUnits()
    {
        return GetFlightGunToStandardWheelUnits() +
            GetFlightStandardToHighWheelUnits() +
            GetFlightHighToScopeWheelUnits();
    }

    private float GetFlightGunToStandardWheelUnits()
    {
        return FlightGunToStandardWheelUnits;
    }

    private float GetFlightStandardToHighWheelUnits()
    {
        return FlightStandardToHighWheelUnits;
    }

    private float GetFlightHighToScopeWheelUnits()
    {
        return FlightHighToScopeWheelUnits;
    }

    private static void ResolveGameplayCursorState(
        bool shouldControl,
        bool releaseCursor,
        out CursorLockMode lockState,
        out bool cursorVisible)
    {
        if (!shouldControl || releaseCursor)
        {
            lockState = CursorLockMode.None;
            cursorVisible = true;
            return;
        }

        lockState = CursorLockMode.Locked;
        cursorVisible = false;
    }

    private float GetMinimumCameraDistanceMeters()
    {
        return Mathf.Clamp(minCameraDistanceMeters, HardMinCameraDistanceMeters, HardMaxCameraDistanceMeters);
    }

    private float GetMaximumCameraDistanceMeters()
    {
        return Mathf.Clamp(maxCameraDistanceMeters, GetMinimumCameraDistanceMeters(), HardMaxCameraDistanceMeters);
    }

    private float GetOrbitSensitivityForTarget(Transform target, float distanceMeters)
    {
        float sensitivity = GetOrbitSensitivityForDistance(distanceMeters);
        return IsPlayerShipFlightTarget(target)
            ? sensitivity * GetFlightMouseLookSensitivityScale()
            : sensitivity;
    }

    private float GetOrbitSensitivityForDistance(float distanceMeters)
    {
        float minDistance = GetMinimumCameraDistanceMeters();
        float maxDistance = GetMaximumCameraDistanceMeters();
        if (maxDistance <= minDistance + 0.001f)
        {
            return orbitSensitivity * closeOrbitSensitivityScale;
        }

        float distanceRatio = Mathf.InverseLerp(minDistance, maxDistance, Mathf.Clamp(distanceMeters, minDistance, maxDistance));
        float smoothedRatio = Mathf.SmoothStep(0f, 1f, distanceRatio);
        return orbitSensitivity * Mathf.Lerp(closeOrbitSensitivityScale, farOrbitSensitivityScale, smoothedRatio);
    }

    private void ApplyProgressiveZoom(float scrollDelta)
    {
        float minDistance = GetMinimumCameraDistanceMeters();
        float maxDistance = GetMaximumCameraDistanceMeters();
        if (maxDistance <= minDistance + 0.001f)
        {
            cameraOrbitDistanceMeters = minDistance;
            return;
        }

        float logMin = Mathf.Log(minDistance);
        float logMax = Mathf.Log(maxDistance);
        float currentDistance = Mathf.Clamp(cameraOrbitDistanceMeters, minDistance, maxDistance);
        float currentRatio = Mathf.InverseLerp(logMin, logMax, Mathf.Log(currentDistance));
        float nextRatio = Mathf.Clamp01(currentRatio - scrollDelta / Mathf.Max(0.001f, scrollUnitsAcrossZoomRange));
        cameraOrbitDistanceMeters = Mathf.Exp(Mathf.Lerp(logMin, logMax, nextRatio));
    }

    private float GetCurrentZoomMetersPerScrollUnit()
    {
        float minDistance = GetMinimumCameraDistanceMeters();
        float maxDistance = GetMaximumCameraDistanceMeters();
        if (maxDistance <= minDistance + 0.001f)
        {
            return 0f;
        }

        float currentDistance = Mathf.Clamp(cameraOrbitDistanceMeters, minDistance, maxDistance);
        return currentDistance * Mathf.Log(maxDistance / minDistance) / Mathf.Max(0.001f, scrollUnitsAcrossZoomRange);
    }

    private void ApplyWheelCalibrationMigration()
    {
        if (scrollUnitsAcrossZoomRange < 0.05f)
        {
            scrollUnitsAcrossZoomRange = CalibratedWheelScrollUnitsAcrossZoomRange;
        }
    }

    private static float NormalizeDegrees(float value)
    {
        value %= 360f;
        return value < 0f ? value + 360f : value;
    }

    private static void RotateTargetTowardInput(Transform target, Vector3 input)
    {
        Vector3 horizontal = new Vector3(input.x, 0f, input.z);
        if (target == null || horizontal.sqrMagnitude <= 0.001f)
        {
            return;
        }

        target.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
    }
}
