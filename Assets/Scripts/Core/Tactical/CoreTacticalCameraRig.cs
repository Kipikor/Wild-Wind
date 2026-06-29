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

    private Vector3 focusPoint;
    private bool focusInitialized;
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
        focusPoint.y = GetCommandPlaneAltitude();
        ReadMouseInput();
    }

    private void LateUpdate()
    {
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

    private void ApplyCameraTransform()
    {
        if (targetCamera == null)
        {
            return;
        }

        focusPoint.y = GetCommandPlaneAltitude();
        float safeDistance = Mathf.Clamp(distanceMeters, minDistanceMeters, maxDistanceMeters);
        distanceMeters = safeDistance;
        float safeElevation = Mathf.Clamp(elevationDegrees, minElevationDegrees, maxElevationDegrees);
        elevationDegrees = safeElevation;

        float yawRadians = yawDegrees * Mathf.Deg2Rad;
        float elevationRadians = safeElevation * Mathf.Deg2Rad;
        float horizontalDistance = Mathf.Cos(elevationRadians) * safeDistance;
        Vector3 offset = new Vector3(
            Mathf.Sin(yawRadians) * horizontalDistance,
            Mathf.Sin(elevationRadians) * safeDistance,
            Mathf.Cos(yawRadians) * horizontalDistance);
        Vector3 cameraPosition = focusPoint + offset;
        float minimumCameraY = focusPoint.y + Mathf.Max(1f, minHeightAboveCommandPlaneMeters);
        if (cameraPosition.y < minimumCameraY)
        {
            cameraPosition.y = minimumCameraY;
        }

        Vector3 lookDirection = focusPoint - cameraPosition;
        targetCamera.transform.position = cameraPosition;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            targetCamera.transform.rotation = Quaternion.LookRotation(
                lookDirection.normalized,
                GetCameraUp(lookDirection.normalized, yawRadians));
        }
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

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            ApplyWheelZoom(scroll);
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        HandleRightPan(mouse, mousePosition);
        HandleMiddleOrbit(mouse, mousePosition);
        HandleKeyboardPan();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void ApplyWheelZoom(float scroll)
    {
        float scrollNotches = NormalizeScrollNotches(scroll);
        if (Mathf.Abs(scrollNotches) <= 0.001f)
        {
            return;
        }

        float zoomFactorPerNotch = Mathf.Clamp(1f - wheelZoomFractionPerNotch, 0.5f, 0.98f);
        float zoomFactor = scrollNotches > 0f
            ? Mathf.Pow(zoomFactorPerNotch, scrollNotches)
            : Mathf.Pow(1f / zoomFactorPerNotch, -scrollNotches);
        distanceMeters = Mathf.Clamp(distanceMeters * zoomFactor, minDistanceMeters, maxDistanceMeters);
    }

    private static float NormalizeScrollNotches(float rawScroll)
    {
        // Input System can report wheel deltas either as 120 units per notch or as direct notch counts.
        // Decide from the raw magnitude before scaling, otherwise fast direct-count scrolling is crushed.
        return Mathf.Abs(rawScroll) >= 10f ? rawScroll / 120f : rawScroll;
    }

    private void HandleRightPan(Mouse mouse, Vector2 mousePosition)
    {
        if (mouse.rightButton.wasPressedThisFrame)
        {
            rightPanCandidate = !IsShiftPressed() && TryProjectMouseToCommandPlane(mousePosition, out rightPanWorldAnchor);
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
    }

    private bool TryProjectMouseToCommandPlane(Vector2 mousePosition, out Vector3 point)
    {
        point = Vector3.zero;
        if (targetCamera == null)
        {
            return false;
        }

        Ray ray = targetCamera.ScreenPointToRay(mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, GetCommandPlaneAltitude(), 0f));
        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        point = ray.GetPoint(enter);
        point.y = GetCommandPlaneAltitude();
        return true;
    }

    private static bool IsShiftPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
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
