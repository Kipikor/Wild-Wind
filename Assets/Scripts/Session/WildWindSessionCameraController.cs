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
    [SerializeField, HideInInspector] private bool rigidFlightCamera = false;
    [SerializeField, HideInInspector, Range(-85f, 80f)] private float minCameraPitchDegrees = -80f;
    [SerializeField, HideInInspector, Range(0f, 85f)] private float maxCameraPitchDegrees = 80f;

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
    private float cameraHeightVelocity;
    private bool ownsGameplayCursor;
    private CursorLockMode storedCursorLockState;
    private bool storedCursorVisible;
    private Vector3 lastCameraAimTarget;

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
        ResolveGameplayCursorState(shouldControl, out lockState, out cursorVisible);
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

        Vector2 dragDelta = ReadCameraOrbitDragDelta();
        bool hasDragDelta = dragDelta.sqrMagnitude > 0.0001f;

        if (hasDragDelta)
        {
            float effectiveOrbitSensitivity = GetOrbitSensitivityForTarget(movementTarget, cameraOrbitDistanceMeters);
            cameraOrbitYawDegrees = NormalizeDegrees(cameraOrbitYawDegrees + dragDelta.x * effectiveOrbitSensitivity);
            cameraOrbitPitchDegrees = Mathf.Clamp(
                cameraOrbitPitchDegrees + dragDelta.y * effectiveOrbitSensitivity,
                minCameraPitchDegrees,
                maxCameraPitchDegrees);
        }

        float scroll = ReadCameraZoomScroll();
        if (Mathf.Abs(scroll) > 0.001f)
        {
            ApplyCameraWheelZoomForTarget(movementTarget, scroll);
            UpdateGameplayMouseLookSensitivityScale(movementTarget);
        }
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

    private Vector2 ReadCameraOrbitDragDelta()
    {
        if (WildWindGameplayHud.IsHudPointerCaptureActiveForCamera)
        {
            return Vector2.zero;
        }

#if ENABLE_INPUT_SYSTEM
        Mouse dragMouse = Mouse.current;
        if (dragMouse != null && dragMouse.rightButton.isPressed)
        {
            return dragMouse.delta.ReadValue();
        }
#endif

        return Vector2.zero;
    }

    private float ReadCameraZoomScroll()
    {
        if (WildWindGameplayHud.IsHudPointerCaptureActiveForCamera)
        {
            return 0f;
        }

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
        Vector3 targetPosition = GetOrbitCameraPosition(lastCameraOrbitPivot, orbitFrame);
        Vector3 targetAimTarget = GetCameraAimTarget(target, lastCameraOrbitPivot, targetPosition, orbitFrame);
        lastCameraAimTarget = targetAimTarget;
        Quaternion targetRotation = GetCameraLookRotation(targetPosition, targetAimTarget);

        if (!snap && TryApplyTakeoffCameraBlend(targetPosition, targetRotation))
        {
            return;
        }

        Vector3 nextPosition = snap
            ? targetPosition
            : GetSmoothedCameraPosition(target, targetPosition, effectiveFollowSharpness);
        sessionCamera.transform.position = nextPosition;
        lastCameraAimTarget = GetCameraAimTarget(target, lastCameraOrbitPivot, nextPosition, orbitFrame);
        Quaternion lookRotation = GetCameraLookRotation(nextPosition, lastCameraAimTarget);
        sessionCamera.transform.rotation = snap
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

    private Vector3 GetCameraAimTarget(Transform target, Vector3 orbitPivot, Vector3 cameraPosition, Quaternion orbitFrame)
    {
        return orbitPivot;
    }

    private Vector3 GetOrbitCameraPosition(Vector3 orbitPivot, Quaternion orbitFrame)
    {
        Vector3 localOrbitOffset = Quaternion.Euler(cameraOrbitPitchDegrees, cameraOrbitYawDegrees, 0f) *
            (Vector3.back * cameraOrbitDistanceMeters);
        return orbitPivot + orbitFrame * localOrbitOffset;
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
        bool flightTarget = IsPlayerShipFlightTarget(target);
        float alpha = flightTarget
            ? 1f - Mathf.Exp(-GetCameraDeltaTime() / Mathf.Max(0.001f, flightCameraPivotSmoothSeconds))
            : Mathf.Clamp01(effectiveFollowSharpness);
        Vector3 nextPosition = Vector3.Lerp(currentPosition, targetPosition, alpha);
        if (!flightTarget)
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
        float dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Max(0.0001f, dt);
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
        return Quaternion.identity;
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
        return IsPlayerShipFlightTarget(target);
    }

    private void ApplyGameplayCursorState(Transform target)
    {
        bool shouldControl = ShouldControlGameplayCursor(target);

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

        ResolveGameplayCursorState(shouldControl, out CursorLockMode lockState, out bool cursorVisible);
        Cursor.lockState = lockState;
        Cursor.visible = cursorVisible;
        GameplayCursorLockedForMouseLook = false;
        GameplayCursorReleasedForUi = shouldControl;
    }

    private void UpdateGameplayMouseLookSensitivityScale(Transform target)
    {
        GameplayMouseLookSensitivityScale = IsPlayerShipFlightTarget(target)
            ? GetFlightMouseLookSensitivityScale()
            : 1f;
    }

    private float GetFlightMouseLookSensitivityScale()
    {
        return 1f;
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
        ApplyProgressiveZoom(scrollDelta * CameraWheelEffectivenessMultiplier);
    }

    private static void ResolveGameplayCursorState(
        bool shouldControl,
        out CursorLockMode lockState,
        out bool cursorVisible)
    {
        lockState = CursorLockMode.None;
        cursorVisible = true;
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

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
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
