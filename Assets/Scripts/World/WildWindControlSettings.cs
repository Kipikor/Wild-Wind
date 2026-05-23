using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum WildWindInputKey
{
    None,
    W,
    A,
    S,
    D,
    Q,
    E,
    LeftShift,
    RightShift,
    Space,
    X,
    LeftCtrl,
    RightCtrl
}

public struct WildWindFlightInputState
{
    public float thrust;
    public float lateral;
    public float lift;
    public float turn;

    public bool HasAnyInput =>
        Mathf.Abs(thrust) > 0.001f ||
        Mathf.Abs(lateral) > 0.001f ||
        Mathf.Abs(lift) > 0.001f ||
        Mathf.Abs(turn) > 0.001f;
}

public sealed class WildWindControlSettings : MonoBehaviour
{
    [Header("Debug Travel Movement")]
    [SerializeField, Range(50f, 4000f), InspectorName("Cruise Speed, m/s")] private float debugCruiseSpeedMetersPerSecond = 650f;
    [SerializeField, Range(50f, 3000f), InspectorName("Vertical Speed, m/s")] private float debugVerticalSpeedMetersPerSecond = 420f;
    [SerializeField, Range(2f, 20f), InspectorName("Sprint Multiplier")] private float debugSprintMultiplier = 6f;

    [Header("Debug Travel Keys")]
    [SerializeField, InspectorName("Forward")] private WildWindInputKey forwardKey = WildWindInputKey.W;
    [SerializeField, InspectorName("Backward")] private WildWindInputKey backwardKey = WildWindInputKey.S;
    [SerializeField, InspectorName("Left")] private WildWindInputKey leftKey = WildWindInputKey.A;
    [SerializeField, InspectorName("Right")] private WildWindInputKey rightKey = WildWindInputKey.D;
    [SerializeField, InspectorName("Up")] private WildWindInputKey upKey = WildWindInputKey.E;
    [SerializeField, InspectorName("Down")] private WildWindInputKey downKey = WildWindInputKey.Q;
    [SerializeField, InspectorName("Sprint")] private WildWindInputKey sprintKey = WildWindInputKey.LeftShift;
    [SerializeField, InspectorName("Sprint Alt")] private WildWindInputKey sprintAltKey = WildWindInputKey.RightShift;

    [Header("Flight Keys")]
    [SerializeField, InspectorName("Thrust Forward")] private WildWindInputKey flightThrustForwardKey = WildWindInputKey.W;
    [SerializeField, InspectorName("Thrust Backward")] private WildWindInputKey flightThrustBackwardKey = WildWindInputKey.S;
    [SerializeField, InspectorName("Slide Left")] private WildWindInputKey flightSlideLeftKey = WildWindInputKey.A;
    [SerializeField, InspectorName("Slide Right")] private WildWindInputKey flightSlideRightKey = WildWindInputKey.D;
    [SerializeField, InspectorName("Turn Left")] private WildWindInputKey flightTurnLeftKey = WildWindInputKey.Q;
    [SerializeField, InspectorName("Turn Right")] private WildWindInputKey flightTurnRightKey = WildWindInputKey.E;
    [SerializeField, InspectorName("Target Altitude Up")] private WildWindInputKey flightAscendKey = WildWindInputKey.LeftShift;
    [SerializeField, InspectorName("Target Altitude Up Alt")] private WildWindInputKey flightAscendAltKey = WildWindInputKey.RightShift;
    [SerializeField, InspectorName("Target Altitude Down")] private WildWindInputKey flightDescendKey = WildWindInputKey.LeftCtrl;
    [SerializeField, InspectorName("Target Altitude Down Alt")] private WildWindInputKey flightDescendAltKey = WildWindInputKey.RightCtrl;

    [Header("Flight Assist Rates")]
    [SerializeField, Range(1f, 80f), InspectorName("Target Speed Change, m/s per sec")] private float flightTargetSpeedChangeMetersPerSecond = 8f;
    [SerializeField, Range(5f, 400f), InspectorName("Target Altitude Change, m/s")] private float flightTargetAltitudeChangeMetersPerSecond = 70f;
    [SerializeField, Range(5f, 180f), InspectorName("Target Heading Change, deg/s")] private float flightTargetHeadingChangeDegreesPerSecond = 45f;
    [SerializeField, Range(0f, 80f), InspectorName("Max Reverse Target Speed, m/s")] private float flightMaxReverseTargetSpeedMetersPerSecond = 25f;

    [Header("Debug Travel Camera")]
    [SerializeField, InspectorName("Camera Offset")] private Vector3 debugCameraOffset = new Vector3(-760f, 240f, -820f);
    [SerializeField, InspectorName("Camera Look Offset")] private Vector3 debugCameraLookOffset = new Vector3(120f, 20f, 120f);
    [SerializeField, Range(0.01f, 1f), InspectorName("Follow Sharpness")] private float debugCameraFollowSharpness = 0.16f;

    public float DebugCruiseSpeedMetersPerSecond => debugCruiseSpeedMetersPerSecond;
    public float DebugVerticalSpeedMetersPerSecond => debugVerticalSpeedMetersPerSecond;
    public float DebugSprintMultiplier => debugSprintMultiplier;
    public Vector3 DebugCameraOffset => debugCameraOffset;
    public Vector3 DebugCameraLookOffset => debugCameraLookOffset;
    public float DebugCameraFollowSharpness => debugCameraFollowSharpness;
    public float FlightTargetSpeedChangeMetersPerSecond => flightTargetSpeedChangeMetersPerSecond;
    public float FlightTargetAltitudeChangeMetersPerSecond => flightTargetAltitudeChangeMetersPerSecond;
    public float FlightTargetHeadingChangeDegreesPerSecond => flightTargetHeadingChangeDegreesPerSecond;
    public float FlightMaxReverseTargetSpeedMetersPerSecond => flightMaxReverseTargetSpeedMetersPerSecond;

    public Vector3 ReadDebugTravelInput()
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float verticalRatio = debugVerticalSpeedMetersPerSecond / Mathf.Max(1f, debugCruiseSpeedMetersPerSecond);

        if (IsKeyPressed(leftKey)) x -= 1f;
        if (IsKeyPressed(rightKey)) x += 1f;
        if (IsKeyPressed(backwardKey)) z -= 1f;
        if (IsKeyPressed(forwardKey)) z += 1f;
        if (IsKeyPressed(downKey)) y -= verticalRatio;
        if (IsKeyPressed(upKey)) y += verticalRatio;

        Vector3 input = new Vector3(x, y, z);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    public bool IsSprintPressed()
    {
        return IsKeyPressed(sprintKey) || IsKeyPressed(sprintAltKey);
    }

    public WildWindFlightInputState ReadFlightInput()
    {
        return new WildWindFlightInputState
        {
            thrust = ReadAxis(flightThrustForwardKey, flightThrustBackwardKey),
            lateral = ReadAxis(flightSlideRightKey, flightSlideLeftKey),
            lift = ReadAxisPair(flightAscendKey, flightAscendAltKey, flightDescendKey, flightDescendAltKey),
            turn = ReadAxis(flightTurnRightKey, flightTurnLeftKey)
        };
    }

    public static WildWindFlightInputState ReadDefaultFlightInput()
    {
        return new WildWindFlightInputState
        {
            thrust = ReadAxis(WildWindInputKey.W, WildWindInputKey.S),
            lateral = ReadAxis(WildWindInputKey.D, WildWindInputKey.A),
            lift = ReadAxisPair(WildWindInputKey.LeftShift, WildWindInputKey.RightShift, WildWindInputKey.LeftCtrl, WildWindInputKey.RightCtrl),
            turn = ReadAxis(WildWindInputKey.E, WildWindInputKey.Q)
        };
    }

    private static float ReadAxis(WildWindInputKey positiveKey, WildWindInputKey negativeKey)
    {
        float value = 0f;
        if (IsKeyPressed(positiveKey)) value += 1f;
        if (IsKeyPressed(negativeKey)) value -= 1f;
        return Mathf.Clamp(value, -1f, 1f);
    }

    private static float ReadAxisPair(
        WildWindInputKey positiveKey,
        WildWindInputKey positiveAltKey,
        WildWindInputKey negativeKey,
        WildWindInputKey negativeAltKey)
    {
        float value = 0f;
        if (IsKeyPressed(positiveKey) || IsKeyPressed(positiveAltKey)) value += 1f;
        if (IsKeyPressed(negativeKey) || IsKeyPressed(negativeAltKey)) value -= 1f;
        return Mathf.Clamp(value, -1f, 1f);
    }

    public static bool IsKeyPressed(WildWindInputKey key)
    {
        if (key == WildWindInputKey.None)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (TryReadInputSystemKey(key))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (TryReadLegacyInputKey(key))
        {
            return true;
        }
#endif

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryReadInputSystemKey(WildWindInputKey key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return key switch
        {
            WildWindInputKey.W => keyboard.wKey.isPressed,
            WildWindInputKey.A => keyboard.aKey.isPressed,
            WildWindInputKey.S => keyboard.sKey.isPressed,
            WildWindInputKey.D => keyboard.dKey.isPressed,
            WildWindInputKey.Q => keyboard.qKey.isPressed,
            WildWindInputKey.E => keyboard.eKey.isPressed,
            WildWindInputKey.LeftShift => keyboard.leftShiftKey.isPressed,
            WildWindInputKey.RightShift => keyboard.rightShiftKey.isPressed,
            WildWindInputKey.Space => keyboard.spaceKey.isPressed,
            WildWindInputKey.X => keyboard.xKey.isPressed,
            WildWindInputKey.LeftCtrl => keyboard.leftCtrlKey.isPressed,
            WildWindInputKey.RightCtrl => keyboard.rightCtrlKey.isPressed,
            _ => false
        };
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private static bool TryReadLegacyInputKey(WildWindInputKey key)
    {
        return key switch
        {
            WildWindInputKey.W => Input.GetKey(KeyCode.W),
            WildWindInputKey.A => Input.GetKey(KeyCode.A),
            WildWindInputKey.S => Input.GetKey(KeyCode.S),
            WildWindInputKey.D => Input.GetKey(KeyCode.D),
            WildWindInputKey.Q => Input.GetKey(KeyCode.Q),
            WildWindInputKey.E => Input.GetKey(KeyCode.E),
            WildWindInputKey.LeftShift => Input.GetKey(KeyCode.LeftShift),
            WildWindInputKey.RightShift => Input.GetKey(KeyCode.RightShift),
            WildWindInputKey.Space => Input.GetKey(KeyCode.Space),
            WildWindInputKey.X => Input.GetKey(KeyCode.X),
            WildWindInputKey.LeftCtrl => Input.GetKey(KeyCode.LeftControl),
            WildWindInputKey.RightCtrl => Input.GetKey(KeyCode.RightControl),
            _ => false
        };
    }
#endif
}
