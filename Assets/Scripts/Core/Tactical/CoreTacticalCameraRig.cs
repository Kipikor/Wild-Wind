using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class CoreTacticalCameraRig : MonoBehaviour
{
    private const float DragActivationPixels = 5f;

    public CoreTacticalFleetController fleet;
    public Camera targetCamera;
    public float distanceMeters = 420f;
    public float minDistanceMeters = 90f;
    public float maxDistanceMeters = 18000f;
    public float wheelZoomFractionPerNotch = 0.12f;
    public float yawDegrees = -138f;
    public float elevationDegrees = 58f;
    // Keep the camera slightly above the command plane; true 0 degrees makes plane raycasts unstable
    // and reads like an under-ground view when the player tries to recover back upward.
    public float minElevationDegrees = 8f;
    public float maxElevationDegrees = 80f;
    public float minHeightAboveCommandPlaneMeters = 60f;
    public float orbitDegreesPerPixel = 0.18f;
    public float keyboardPanScreensPerSecond = 0.72f;
    public float zoomSmoothTimeSeconds = 0.16f;

    private Vector3 focusPoint;
    private bool focusInitialized;
    private bool zoomTargetsInitialized;
    private float targetDistanceMeters;
    private float zoomDistanceVelocity;
    private bool smoothZoomActive;
    private Vector2 smoothZoomScreenAnchor;
    private Vector3 smoothZoomWorldAnchor;
    private bool rightPanCandidate;
    private bool rightPanActive;
    private bool middleTiltCandidate;
    private bool middleTiltActive;
    private Vector2 rightPressScreenPosition;
    private Vector2 middlePressScreenPosition;
    private Vector2 middleLastScreenPosition;
    private Vector3 rightPanWorldAnchor;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
    }

    private void Start()
    {
        InitializeFocusIfNeeded();
        InitializeZoomTargetsIfNeeded();
        if (fleet != null && targetCamera != null)
        {
            fleet.SetInputCamera(targetCamera);
        }

        ApplyCameraTransform();
    }

    private void Update()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                return;
            }
        }

        InitializeFocusIfNeeded();
        InitializeZoomTargetsIfNeeded();
        fleet?.SetInputCamera(targetCamera);
        focusPoint.y = GetCommandPlaneAltitude();
        ReadMouseInput();
    }

    private void LateUpdate()
    {
        UpdateSmoothZoom(Time.unscaledDeltaTime);
        ApplyCameraTransform();
    }

    public void ApplyCurrentTransformForInput()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        InitializeFocusIfNeeded();
        InitializeZoomTargetsIfNeeded();
        StopSmoothZoomForInput();
        focusPoint.y = GetCommandPlaneAltitude();
        ApplyCameraTransform();
    }

    private void InitializeFocusIfNeeded()
    {
        if (focusInitialized)
        {
            return;
        }

        focusPoint = fleet != null ? fleet.GetFleetCenter() : Vector3.zero;
        focusPoint.y = GetCommandPlaneAltitude();
        focusInitialized = true;
    }

    private void InitializeZoomTargetsIfNeeded()
    {
        if (zoomTargetsInitialized)
        {
            return;
        }

        targetDistanceMeters = Mathf.Clamp(distanceMeters, minDistanceMeters, maxDistanceMeters);
        distanceMeters = targetDistanceMeters;
        zoomDistanceVelocity = 0f;
        smoothZoomActive = false;
        zoomTargetsInitialized = true;
    }

    private void ApplyCameraTransform()
    {
        if (targetCamera == null)
        {
            return;
        }

        InitializeFocusIfNeeded();
        InitializeZoomTargetsIfNeeded();
        focusPoint.y = GetCommandPlaneAltitude();
        float safeDistance = Mathf.Clamp(distanceMeters, minDistanceMeters, maxDistanceMeters);
        distanceMeters = safeDistance;
        targetDistanceMeters = Mathf.Clamp(targetDistanceMeters, minDistanceMeters, maxDistanceMeters);
        float safeElevation = Mathf.Clamp(elevationDegrees, minElevationDegrees, maxElevationDegrees);
        elevationDegrees = safeElevation;

        CalculateCameraPose(focusPoint, safeDistance, out Vector3 cameraPosition, out Quaternion cameraRotation);
        targetCamera.transform.position = cameraPosition;
        targetCamera.transform.rotation = cameraRotation;
        targetCamera.ResetWorldToCameraMatrix();
        targetCamera.ResetProjectionMatrix();
    }

    private void StopSmoothZoomForInput()
    {
        targetDistanceMeters = Mathf.Clamp(distanceMeters, minDistanceMeters, maxDistanceMeters);
        zoomDistanceVelocity = 0f;
        smoothZoomActive = false;
    }

    private void CalculateCameraPose(Vector3 focus, float distance, out Vector3 cameraPosition, out Quaternion cameraRotation)
    {
        focus.y = GetCommandPlaneAltitude();
        float safeDistance = Mathf.Clamp(distance, minDistanceMeters, maxDistanceMeters);
        float safeElevation = Mathf.Clamp(elevationDegrees, minElevationDegrees, maxElevationDegrees);
        float yawRadians = yawDegrees * Mathf.Deg2Rad;
        float elevationRadians = safeElevation * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(elevationRadians) * safeDistance;
        Vector3 offset = new Vector3(
            Mathf.Sin(yawRadians) * horizontalDistance,
            Mathf.Sin(elevationRadians) * safeDistance,
            Mathf.Cos(yawRadians) * horizontalDistance);
        cameraPosition = focus + offset;
        float minimumCameraY = focus.y + Mathf.Max(1f, minHeightAboveCommandPlaneMeters);
        if (cameraPosition.y < minimumCameraY)
        {
            cameraPosition.y = minimumCameraY;
        }

        Vector3 lookDirection = focus - cameraPosition;
        cameraRotation = lookDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(lookDirection.normalized, GetCameraUp(lookDirection.normalized, yawRadians))
            : Quaternion.identity;
    }

    private float GetCommandPlaneAltitude()
    {
        return fleet != null ? fleet.CommandPlaneAltitudeMeters : 0f;
    }

    private void ReadMouseInput()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (!TryReadInputScreenPosition(mouse, out Vector2 mousePosition))
        {
            return;
        }

        float scrollNotches = NormalizeScrollNotches(mouse.scroll.ReadValue().y);
        if (Mathf.Abs(scrollNotches) > 0.001f)
        {
            ApplyWheelZoom(scrollNotches, mousePosition);
        }

        HandleRightPan(mouse, mousePosition);
        HandleMiddleOrbit(mouse, mousePosition);
        HandleKeyboardPan();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void ApplyWheelZoom(float scrollNotches, Vector2 mousePosition)
    {
        if (Mathf.Abs(scrollNotches) <= 0.001f)
        {
            return;
        }

        InitializeFocusIfNeeded();
        InitializeZoomTargetsIfNeeded();
        bool hasZoomAnchor = TryProjectScreenPointToCommandPlane(
            mousePosition,
            focusPoint,
            distanceMeters,
            out Vector3 anchorBeforeZoom);

        float zoomFactorPerNotch = Mathf.Clamp(1f - wheelZoomFractionPerNotch, 0.5f, 0.98f);
        float zoomFactor = scrollNotches > 0f
            ? Mathf.Pow(zoomFactorPerNotch, scrollNotches)
            : Mathf.Pow(1f / zoomFactorPerNotch, -scrollNotches);
        float previousTargetDistance = targetDistanceMeters;
        targetDistanceMeters = Mathf.Clamp(targetDistanceMeters * zoomFactor, minDistanceMeters, maxDistanceMeters);
        if (Mathf.Approximately(previousTargetDistance, targetDistanceMeters))
        {
            return;
        }

        if (hasZoomAnchor)
        {
            smoothZoomScreenAnchor = mousePosition;
            smoothZoomWorldAnchor = anchorBeforeZoom;
            smoothZoomActive = true;
        }
    }

    private void UpdateSmoothZoom(float deltaSeconds)
    {
        InitializeFocusIfNeeded();
        InitializeZoomTargetsIfNeeded();
        targetDistanceMeters = Mathf.Clamp(targetDistanceMeters, minDistanceMeters, maxDistanceMeters);
        float safeDeltaSeconds = Mathf.Max(0f, deltaSeconds);
        if (safeDeltaSeconds <= 0.0001f)
        {
            return;
        }

        float smoothTime = Mathf.Max(0.01f, zoomSmoothTimeSeconds);
        distanceMeters = Mathf.SmoothDamp(
            distanceMeters,
            targetDistanceMeters,
            ref zoomDistanceVelocity,
            smoothTime,
            Mathf.Infinity,
            safeDeltaSeconds);
        distanceMeters = Mathf.Clamp(distanceMeters, minDistanceMeters, maxDistanceMeters);

        if (smoothZoomActive)
        {
            KeepSmoothZoomAnchorUnderMouse();
            if (Mathf.Abs(distanceMeters - targetDistanceMeters) <= 0.02f
                && Mathf.Abs(zoomDistanceVelocity) <= 0.02f)
            {
                distanceMeters = targetDistanceMeters;
                zoomDistanceVelocity = 0f;
                KeepSmoothZoomAnchorUnderMouse();
                smoothZoomActive = false;
            }
        }
    }

    private void KeepSmoothZoomAnchorUnderMouse()
    {
        if (!smoothZoomActive
            || !TryProjectScreenPointToCommandPlane(
                smoothZoomScreenAnchor,
                focusPoint,
                distanceMeters,
                out Vector3 anchorAfterZoom))
        {
            return;
        }

        Vector3 correction = smoothZoomWorldAnchor - anchorAfterZoom;
        correction.y = 0f;
        if (IsFiniteVector3(correction))
        {
            focusPoint += correction;
        }
    }

    private void CancelSmoothZoomAnchor()
    {
        StopSmoothZoomForInput();
    }

    private static float NormalizeScrollNotches(float rawScroll)
    {
        float notches = Mathf.Abs(rawScroll) >= 10f ? rawScroll / 120f : rawScroll;
        return Mathf.Clamp(notches, -6f, 6f);
    }

    private bool TryReadInputScreenPosition(Mouse mouse, out Vector2 position)
    {
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            return IsScreenPositionInsideGameView(position);
        }

        position = default;
        return false;
    }

    private static bool IsScreenPositionInsideGameView(Vector2 position)
    {
        if (!IsFiniteFloat(position.x) || !IsFiniteFloat(position.y))
        {
            return false;
        }

        const float tolerancePixels = 8f;
        return position.x >= -tolerancePixels
            && position.x <= Screen.width + tolerancePixels
            && position.y >= -tolerancePixels
            && position.y <= Screen.height + tolerancePixels;
    }

    private static bool IsFiniteVector3(Vector3 value)
    {
        return IsFiniteFloat(value.x)
            && IsFiniteFloat(value.y)
            && IsFiniteFloat(value.z);
    }

    private static bool IsFiniteFloat(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void HandleRightPan(Mouse mouse, Vector2 mousePosition)
    {
        if (mouse.rightButton.wasPressedThisFrame)
        {
            rightPanCandidate = !IsShiftPressed()
                && !HasSelectedFleetShips()
                && !HasFleetCommandDraft()
                && TryProjectMouseToCommandPlane(mousePosition, out rightPanWorldAnchor);
            rightPanActive = false;
            rightPressScreenPosition = mousePosition;
        }

        if (mouse.rightButton.isPressed && rightPanCandidate)
        {
            float distanceFromPress = Vector2.Distance(mousePosition, rightPressScreenPosition);
            if (!rightPanActive && distanceFromPress >= DragActivationPixels)
            {
                rightPanActive = true;
            }

            if (rightPanActive && TryProjectMouseToCommandPlane(mousePosition, out Vector3 currentWorldPoint))
            {
                Vector3 delta = rightPanWorldAnchor - currentWorldPoint;
                delta.y = 0f;
                focusPoint += delta;
                CancelSmoothZoomAnchor();
            }
        }

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            rightPanCandidate = false;
            rightPanActive = false;
        }
    }

    private void HandleMiddleOrbit(Mouse mouse, Vector2 mousePosition)
    {
        if (mouse.middleButton.wasPressedThisFrame)
        {
            middleTiltCandidate = true;
            middleTiltActive = false;
            middlePressScreenPosition = mousePosition;
            middleLastScreenPosition = mousePosition;
        }

        if (mouse.middleButton.isPressed && middleTiltCandidate)
        {
            float distanceFromPress = Vector2.Distance(mousePosition, middlePressScreenPosition);
            if (!middleTiltActive && distanceFromPress >= DragActivationPixels)
            {
                middleTiltActive = true;
            }

            if (middleTiltActive)
            {
                Vector2 delta = mousePosition - middleLastScreenPosition;
                yawDegrees += delta.x * orbitDegreesPerPixel;
                elevationDegrees = Mathf.Clamp(
                    elevationDegrees - delta.y * orbitDegreesPerPixel,
                    minElevationDegrees,
                    maxElevationDegrees);
                CancelSmoothZoomAnchor();
            }

            middleLastScreenPosition = mousePosition;
        }

        if (mouse.middleButton.wasReleasedThisFrame)
        {
            middleTiltCandidate = false;
            middleTiltActive = false;
        }
    }

    private void HandleKeyboardPan()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || targetCamera == null)
        {
            return;
        }

        if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
        {
            return;
        }

        Vector2 axis = Vector2.zero;
        if (keyboard.dKey.isPressed) axis.x += 1f;
        if (keyboard.aKey.isPressed) axis.x -= 1f;
        if (keyboard.wKey.isPressed) axis.y += 1f;
        if (keyboard.sKey.isPressed) axis.y -= 1f;
        if (axis.sqrMagnitude <= 0.001f)
        {
            return;
        }

        axis = Vector2.ClampMagnitude(axis, 1f);
        Vector3 flatForward = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        Vector3 flatRight = Vector3.ProjectOnPlane(targetCamera.transform.right, Vector3.up);
        if (flatRight.sqrMagnitude <= 0.0001f)
        {
            flatRight = Vector3.right;
        }

        flatForward.Normalize();
        flatRight.Normalize();
        float viewHeightAtFocus = 2f
            * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
            * Mathf.Clamp(distanceMeters, minDistanceMeters, maxDistanceMeters);
        float viewWidthAtFocus = viewHeightAtFocus * Mathf.Max(0.1f, targetCamera.aspect);
        Vector3 worldDelta = flatRight * (axis.x * viewWidthAtFocus)
            + flatForward * (axis.y * viewHeightAtFocus);
        focusPoint += worldDelta * keyboardPanScreensPerSecond * Time.unscaledDeltaTime;
        CancelSmoothZoomAnchor();
    }

    private bool TryProjectMouseToCommandPlane(Vector2 mousePosition, out Vector3 point)
    {
        return CoreTacticalFleetController.TryProjectCameraScreenPointToCommandPlane(
            targetCamera,
            mousePosition,
            GetCommandPlaneAltitude(),
            out point);
    }

    private bool TryProjectScreenPointToCommandPlane(
        Vector2 screenPosition,
        Vector3 focus,
        float distance,
        out Vector3 point)
    {
        point = Vector3.zero;
        if (targetCamera == null
            || !IsFiniteFloat(screenPosition.x)
            || !IsFiniteFloat(screenPosition.y))
        {
            return false;
        }

        Rect pixelRect = targetCamera.pixelRect;
        if (pixelRect.width > 0.01f && pixelRect.height > 0.01f)
        {
            bool outsideCamera =
                screenPosition.x < pixelRect.xMin - 8f ||
                screenPosition.x > pixelRect.xMax + 8f ||
                screenPosition.y < pixelRect.yMin - 8f ||
                screenPosition.y > pixelRect.yMax + 8f;
            if (outsideCamera)
            {
                return false;
            }
        }

        CalculateCameraPose(focus, distance, out Vector3 cameraPosition, out Quaternion cameraRotation);
        float rectWidth = pixelRect.width > 0.01f ? pixelRect.width : Screen.width;
        float rectHeight = pixelRect.height > 0.01f ? pixelRect.height : Screen.height;
        float normalizedX = ((screenPosition.x - pixelRect.xMin) / Mathf.Max(1f, rectWidth)) * 2f - 1f;
        float normalizedY = ((screenPosition.y - pixelRect.yMin) / Mathf.Max(1f, rectHeight)) * 2f - 1f;
        float tangent = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = Mathf.Max(0.01f, targetCamera.aspect);
        Vector3 localDirection = new Vector3(normalizedX * aspect * tangent, normalizedY * tangent, 1f).normalized;
        Vector3 worldDirection = cameraRotation * localDirection;
        if (Mathf.Abs(worldDirection.y) <= 0.00001f)
        {
            return false;
        }

        float enter = (GetCommandPlaneAltitude() - cameraPosition.y) / worldDirection.y;
        if (enter < 0f || !IsFiniteFloat(enter))
        {
            return false;
        }

        point = cameraPosition + worldDirection * enter;
        point.y = GetCommandPlaneAltitude();
        return IsFiniteVector3(point);
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private bool HasSelectedFleetShips()
    {
        return fleet != null && fleet.HasSelectedShipsForInput;
    }

    private bool HasFleetCommandDraft()
    {
        return fleet != null && fleet.IsCommandDraftActiveForInput;
    }
#endif

    private static Vector3 GetCameraUp(Vector3 lookDirection, float yawRadians)
    {
        // The playable tactical camera is clamped below true vertical, so world-up remains stable.
        // Keep the singularity fallback very close to 90 degrees; switching earlier rolls the view
        // around the top-down limit and reads as a left/right mirror flip.
        if (Mathf.Abs(Vector3.Dot(lookDirection, Vector3.up)) < 0.9995f)
        {
            return Vector3.up;
        }

        Vector3 yawForward = new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
        Vector3 cameraUp = Vector3.ProjectOnPlane(yawForward, lookDirection);
        if (cameraUp.sqrMagnitude <= 0.0001f)
        {
            cameraUp = Vector3.ProjectOnPlane(Vector3.forward, lookDirection);
        }

        if (cameraUp.sqrMagnitude <= 0.0001f)
        {
            cameraUp = Vector3.right;
        }

        return cameraUp.normalized;
    }
}
