using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class WorldDebugTravelController : MonoBehaviour
{
    private const float HardMinCameraDistanceMeters = 5f;
    private const float HardMaxCameraDistanceMeters = 2000f;
    private const float CalibratedWheelScrollUnitsAcrossZoomRange = 0.383f;
    private const float CalibratedWheelTickUnit = CalibratedWheelScrollUnitsAcrossZoomRange / 6f;

    [Header("References")]
    [SerializeField, InspectorName("Focus")] private Transform focus;
    [SerializeField, InspectorName("World Camera")] private Camera worldCamera;
    [SerializeField, InspectorName("Visual Tuner")] private VisualPlayModeTuner visualTuner;
    [SerializeField, InspectorName("Bubble Streamer")] private WorldBubbleStreamer bubbleStreamer;
    [SerializeField, InspectorName("Control Settings")] private WildWindControlSettings controlSettings;
    [SerializeField, InspectorName("Gameplay Session")] private WildWindGameplaySession gameplaySession;

    [Header("Movement")]
    [SerializeField, Range(50f, 4000f), InspectorName("Cruise Speed, m/s")] private float cruiseSpeedMetersPerSecond = 650f;
    [SerializeField, Range(50f, 3000f), InspectorName("Vertical Speed, m/s")] private float verticalSpeedMetersPerSecond = 420f;
    [SerializeField, Range(2f, 20f), InspectorName("Sprint Multiplier")] private float sprintMultiplier = 6f;

    [Header("Camera")]
    [SerializeField, InspectorName("Camera Offset")] private Vector3 cameraOffset = new Vector3(-760f, 240f, -820f);
    [SerializeField, InspectorName("Camera Look Offset")] private Vector3 cameraLookOffset = new Vector3(120f, 20f, 120f);
    [SerializeField, Range(0.01f, 1f), InspectorName("Follow Sharpness")] private float followSharpness = 0.22f;
    [SerializeField, Range(0.01f, 1f), InspectorName("Orbit Sensitivity")] private float orbitSensitivity = 0.18f;
    [SerializeField, Range(0.1f, 1f), InspectorName("Close Orbit Sensitivity Scale")] private float closeOrbitSensitivityScale = 0.35f;
    [SerializeField, Range(1f, 4f), InspectorName("Far Orbit Sensitivity Scale")] private float farOrbitSensitivityScale = 2.2f;
    [SerializeField, Range(0.05f, 2f), InspectorName("Scroll Units Across Zoom Range")] private float scrollUnitsAcrossZoomRange = CalibratedWheelScrollUnitsAcrossZoomRange;
    [SerializeField, Range(5f, 500f), InspectorName("Min Camera Distance, m")] private float minCameraDistanceMeters = HardMinCameraDistanceMeters;
    [SerializeField, Range(500f, 2000f), InspectorName("Max Camera Distance, m")] private float maxCameraDistanceMeters = HardMaxCameraDistanceMeters;
    [SerializeField, Range(5f, 2000f), InspectorName("Takeoff Camera Distance, m")] private float takeoffCameraDistanceMeters = 620f;
    [SerializeField, Range(0.05f, 2f), InspectorName("Takeoff Camera Blend, s")] private float takeoffCameraBlendSeconds = 0.65f;
    [SerializeField, Range(0.02f, 1.5f), InspectorName("Flight Pivot Smooth, s")] private float flightCameraPivotSmoothSeconds = 0.22f;
    [SerializeField, Range(0.04f, 2f), InspectorName("Flight Vertical Pivot Smooth, s")] private float flightCameraVerticalPivotSmoothSeconds = 0.62f;
    [SerializeField, Range(0.04f, 2f), InspectorName("Flight Camera Height Smooth, s")] private float flightCameraHeightSmoothSeconds = 0.48f;
    [SerializeField, Range(0.02f, 1f), InspectorName("Flight Camera Rotation Smooth, s")] private float flightCameraRotationSmoothSeconds = 0.14f;
    [SerializeField, Range(0.02f, 1.5f), InspectorName("Flight Orbit Heading Smooth, s")] private float flightCameraOrbitHeadingSmoothSeconds = 0.38f;
    [SerializeField, Range(0f, 45f), InspectorName("Tail Auto Align Window, deg")] private float tailAutoAlignWindowDegrees = 15f;
    [SerializeField, Range(0f, 1f), InspectorName("Tail Auto Align Delay, s")] private float tailAutoAlignDelaySeconds = 0.12f;
    [SerializeField, Range(0.02f, 1.5f), InspectorName("Tail Auto Align Smooth, s")] private float tailAutoAlignSmoothSeconds = 0.24f;
    [SerializeField, Range(-20f, 80f), InspectorName("Min Camera Pitch")] private float minCameraPitchDegrees = 6f;
    [SerializeField, Range(0f, 85f), InspectorName("Max Camera Pitch")] private float maxCameraPitchDegrees = 58f;

    [Header("Temporary Wheel Debug")]
    [SerializeField, InspectorName("Show Wheel Tick Counter")] private bool showWheelTickCounter = false;
    [SerializeField, Range(0.001f, 0.5f), InspectorName("Wheel Tick Unit")] private float wheelTickUnit = CalibratedWheelTickUnit;

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
    private float wheelTickAccumulator;
    private float lastWheelScrollDelta;
    private float lastWheelTickTime = -1f;
    private int wheelTickCounter;
    private int lastWheelTickDirection;
    private string lastWheelInputSource = "none";

    public Transform CurrentMovementTarget => GetMovementTarget();
    public float CameraOrbitDistanceMeters => cameraOrbitDistanceMeters;
    public float CameraOrbitYawDegrees => cameraOrbitYawDegrees;
    public float CameraOrbitPitchDegrees => cameraOrbitPitchDegrees;
    public float TakeoffCameraDistanceMeters => takeoffCameraDistanceMeters;
    public float MinimumCameraDistanceMeters => GetMinimumCameraDistanceMeters();
    public float MaximumCameraDistanceMeters => GetMaximumCameraDistanceMeters();
    public Vector3 LastCameraOrbitPivot => lastCameraOrbitPivot;

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
        UpdateCameraOrbitInput();
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
        visualTuner?.ApplyNow();
    }

    private void OnGUI()
    {
        if (!showWheelTickCounter)
        {
            return;
        }

        DrawWheelTickCounter();
    }

    public void Configure(
        Transform newFocus,
        Camera newCamera,
        VisualPlayModeTuner newVisualTuner,
        WorldBubbleStreamer newStreamer,
        WildWindControlSettings newControlSettings)
    {
        focus = newFocus;
        worldCamera = newCamera;
        visualTuner = newVisualTuner;
        bubbleStreamer = newStreamer;
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
        cameraWasOrbitingPlayerShip = IsPlayerShipFlightTarget(target);
        BeginTakeoffCameraBlend();
        ApplyCamera(false, target);
    }

    public void AdjustCameraOrbitForTests(float yawDeltaDegrees, float pitchDeltaDegrees, float zoomSteps)
    {
        InitializeCameraOrbit(GetEffectiveCameraOffset());
        cameraOrbitYawDegrees = NormalizeDegrees(cameraOrbitYawDegrees + yawDeltaDegrees);
        cameraOrbitPitchDegrees = Mathf.Clamp(cameraOrbitPitchDegrees + pitchDeltaDegrees, minCameraPitchDegrees, maxCameraPitchDegrees);
        ApplyProgressiveZoom(zoomSteps);
        ApplyCamera(false, GetMovementTarget());
    }

    [ContextMenu("Move To Capital")]
    public void MoveToCapital()
    {
        if (focus != null)
        {
            focus.position = new Vector3(0f, 2500f, 0f);
            bubbleStreamer?.RefreshNow();
            ApplyCamera(true);
            visualTuner?.ApplyNow();
        }
    }

    [ContextMenu("Move To Habitation Test Altitude")]
    public void MoveToHabitationTestAltitude()
    {
        MoveToAltitude(2500f);
    }

    [ContextMenu("Move To Thin Air Test Altitude")]
    public void MoveToThinAirTestAltitude()
    {
        MoveToAltitude(12000f);
    }

    [ContextMenu("Move To Ice Test Altitude")]
    public void MoveToIceTestAltitude()
    {
        MoveToAltitude(45000f);
    }

    private void MoveToAltitude(float altitude)
    {
        if (focus == null)
        {
            return;
        }

        Vector3 position = focus.position;
        position.y = altitude;
        focus.position = position;
        bubbleStreamer?.RefreshNow();
        ApplyCamera(true);
        visualTuner?.ApplyNow();
    }

    private Vector3 ReadMovementInput()
    {
        Vector3 input;
        if (controlSettings != null)
        {
            input = controlSettings.ReadDebugTravelInput();
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
        return controlSettings != null ? controlSettings.DebugCruiseSpeedMetersPerSecond : cruiseSpeedMetersPerSecond;
    }

    private float GetSprintMultiplier()
    {
        return controlSettings != null ? controlSettings.DebugSprintMultiplier : sprintMultiplier;
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

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (visualTuner == null)
        {
            visualTuner = FindFirstObjectByType<VisualPlayModeTuner>();
        }

        if (bubbleStreamer == null)
        {
            bubbleStreamer = FindFirstObjectByType<WorldBubbleStreamer>();
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

    private void UpdateCameraOrbitInput()
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
            float effectiveOrbitSensitivity = GetOrbitSensitivityForDistance(cameraOrbitDistanceMeters);
            cameraOrbitYawDegrees = NormalizeDegrees(cameraOrbitYawDegrees + dragDelta.x * effectiveOrbitSensitivity);
            cameraOrbitPitchDegrees = Mathf.Clamp(
                cameraOrbitPitchDegrees - dragDelta.y * effectiveOrbitSensitivity,
                minCameraPitchDegrees,
                maxCameraPitchDegrees);
            hasCameraOrbitManualDrag = true;
        }

        float scroll = ReadCameraZoomScroll();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            ApplyProgressiveZoom(scroll);
        }

        ApplyTailAutoAlignmentAfterOrbitInput(orbitControlActive);
    }

    private Vector3 GetEffectiveCameraOffset()
    {
        return controlSettings != null ? controlSettings.DebugCameraOffset : cameraOffset;
    }

    private Vector3 GetEffectiveCameraLookOffset()
    {
        return controlSettings != null ? controlSettings.DebugCameraLookOffset : cameraLookOffset;
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
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            orbitControlActive = true;
            return mouse.delta.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButton(1))
        {
            orbitControlActive = true;
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 18f;
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
            float scroll = mouse.scroll.ReadValue().y / 120f;
            TrackWheelScrollDelta(scroll, "Input System");
            return scroll;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        float legacyScroll = Input.mouseScrollDelta.y;
        TrackWheelScrollDelta(legacyScroll, "Legacy Input");
        return legacyScroll;
#else
        TrackWheelScrollDelta(0f, "none");
        return 0f;
#endif
    }

    private void TrackWheelScrollDelta(float scroll, string inputSource)
    {
        lastWheelInputSource = inputSource;
        lastWheelScrollDelta = scroll;
        if (Mathf.Abs(scroll) <= 0.0001f)
        {
            return;
        }

        float tickUnit = Mathf.Max(0.001f, wheelTickUnit);
        wheelTickAccumulator += scroll;
        while (Mathf.Abs(wheelTickAccumulator) >= tickUnit)
        {
            int tickDirection = wheelTickAccumulator > 0f ? 1 : -1;
            wheelTickCounter += tickDirection;
            lastWheelTickDirection = tickDirection;
            lastWheelTickTime = Time.unscaledTime;
            wheelTickAccumulator -= tickDirection * tickUnit;
        }
    }

    private void ApplyCamera(bool snap)
    {
        ApplyCamera(snap, GetMovementTarget());
    }

    private void ApplyCamera(bool snap, Transform target)
    {
        if (worldCamera == null || target == null)
        {
            return;
        }

        InitializeCameraOrbit(GetEffectiveCameraOffset());
        float effectiveFollowSharpness = controlSettings != null ? controlSettings.DebugCameraFollowSharpness : followSharpness;
        Vector3 localOrbitOffset = Quaternion.Euler(cameraOrbitPitchDegrees, cameraOrbitYawDegrees, 0f) *
            (Vector3.back * cameraOrbitDistanceMeters);
        Vector3 orbitOffset = GetCameraOrbitFrame(target, snap) * localOrbitOffset;

        Vector3 rawOrbitPivot = GetCameraOrbitPivot(target);
        lastCameraOrbitPivot = GetSmoothedCameraOrbitPivot(target, rawOrbitPivot, snap);
        Vector3 targetPosition = lastCameraOrbitPivot + orbitOffset;
        Quaternion targetRotation = GetCameraLookRotation(targetPosition, lastCameraOrbitPivot);

        if (!snap && TryApplyTakeoffCameraBlend(targetPosition, targetRotation))
        {
            return;
        }

        Vector3 nextPosition = snap
            ? targetPosition
            : GetSmoothedCameraPosition(target, targetPosition, effectiveFollowSharpness);
        worldCamera.transform.position = nextPosition;
        Quaternion lookRotation = GetCameraLookRotation(nextPosition, lastCameraOrbitPivot);
        worldCamera.transform.rotation = snap
            ? lookRotation
            : GetSmoothedCameraRotation(target, lookRotation);
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

    private Vector3 GetSmoothedCameraOrbitPivot(Transform target, Vector3 rawPivot, bool snap)
    {
        bool smoothFlightPivot = IsPlayerShipFlightTarget(target);
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
        Vector3 currentPosition = worldCamera.transform.position;
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
        if (!IsPlayerShipFlightTarget(target))
        {
            return targetRotation;
        }

        float smoothSeconds = Mathf.Max(0.001f, flightCameraRotationSmoothSeconds);
        float alpha = 1f - Mathf.Exp(-GetCameraDeltaTime() / smoothSeconds);
        return Quaternion.Slerp(worldCamera.transform.rotation, targetRotation, alpha);
    }

    private static float GetCameraDeltaTime()
    {
        return Mathf.Max(0.0001f, Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime));
    }

    private void BeginTakeoffCameraBlend()
    {
        if (worldCamera == null)
        {
            return;
        }

        takeoffCameraBlendActive = true;
        takeoffCameraBlendStartedAt = Time.unscaledTime;
        takeoffCameraBlendStartPosition = worldCamera.transform.position;
        takeoffCameraBlendStartRotation = worldCamera.transform.rotation;
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

        worldCamera.transform.position = Vector3.Lerp(takeoffCameraBlendStartPosition, targetPosition, easedRatio);
        worldCamera.transform.rotation = Quaternion.Slerp(takeoffCameraBlendStartRotation, targetRotation, easedRatio);

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
        if (snap || !hasSmoothedCameraOrbitHeading)
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

    private float GetMinimumCameraDistanceMeters()
    {
        return Mathf.Clamp(minCameraDistanceMeters, HardMinCameraDistanceMeters, HardMaxCameraDistanceMeters);
    }

    private float GetMaximumCameraDistanceMeters()
    {
        return Mathf.Clamp(maxCameraDistanceMeters, GetMinimumCameraDistanceMeters(), HardMaxCameraDistanceMeters);
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
        if (wheelTickUnit > 0.25f)
        {
            wheelTickUnit = CalibratedWheelTickUnit;
        }

        if (scrollUnitsAcrossZoomRange < 0.05f)
        {
            scrollUnitsAcrossZoomRange = CalibratedWheelScrollUnitsAcrossZoomRange;
        }
    }

    private void DrawWheelTickCounter()
    {
        const float padding = 14f;
        Rect boxRect = new Rect(16f, 16f, 360f, 156f);
        GUI.Box(boxRect, "Mouse Wheel Tick Debug");

        GUILayout.BeginArea(new Rect(
            boxRect.x + padding,
            boxRect.y + 24f,
            boxRect.width - padding * 2f,
            boxRect.height - 34f));

        GUILayout.Label("source: " + lastWheelInputSource);
        GUILayout.Label("raw scroll/frame: " + lastWheelScrollDelta.ToString("0.###"));
        GUILayout.Label("tick accumulator: " + wheelTickAccumulator.ToString("0.###") + " / " + Mathf.Max(0.001f, wheelTickUnit).ToString("0.###"));
        GUILayout.Label("tick counter: " + wheelTickCounter);
        GUILayout.Label("last tick: " + FormatWheelTickDirection() + " at " + FormatWheelTickTime());
        GUILayout.Label("distance: " + cameraOrbitDistanceMeters.ToString("0.#") + " m, zoom/unit now: " + GetCurrentZoomMetersPerScrollUnit().ToString("0.#") + " m");

        GUILayout.EndArea();
    }

    private string FormatWheelTickDirection()
    {
        if (lastWheelTickDirection > 0)
        {
            return "+1";
        }

        if (lastWheelTickDirection < 0)
        {
            return "-1";
        }

        return "none";
    }

    private string FormatWheelTickTime()
    {
        return lastWheelTickTime >= 0f ? lastWheelTickTime.ToString("0.00") + "s" : "never";
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
