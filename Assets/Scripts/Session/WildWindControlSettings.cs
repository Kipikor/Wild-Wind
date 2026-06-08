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
    RightCtrl,
    LeftAlt,
    RightAlt
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
    [Header("Session Camera Movement")]
    [SerializeField, Range(50f, 4000f), InspectorName("Cruise Speed, m/s")] private float sessionCruiseSpeedMetersPerSecond = 650f;
    [SerializeField, Range(50f, 3000f), InspectorName("Vertical Speed, m/s")] private float sessionVerticalSpeedMetersPerSecond = 420f;
    [SerializeField, Range(2f, 20f), InspectorName("Sprint Multiplier")] private float sessionSprintMultiplier = 6f;

    [Header("Session Camera Keys")]
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

    [Header("Session Camera Follow")]
    [SerializeField, InspectorName("Camera Offset")] private Vector3 sessionCameraOffset = new Vector3(-760f, 240f, -820f);
    [SerializeField, InspectorName("Camera Look Offset")] private Vector3 sessionCameraLookOffset = new Vector3(120f, 20f, 120f);
    [SerializeField, Range(0.01f, 1f), InspectorName("Follow Sharpness")] private float sessionCameraFollowSharpness = 0.16f;

    public float SessionCruiseSpeedMetersPerSecond => sessionCruiseSpeedMetersPerSecond;
    public float SessionVerticalSpeedMetersPerSecond => sessionVerticalSpeedMetersPerSecond;
    public float SessionSprintMultiplier => sessionSprintMultiplier;
    public Vector3 SessionCameraOffset => sessionCameraOffset;
    public Vector3 SessionCameraLookOffset => sessionCameraLookOffset;
    public float SessionCameraFollowSharpness => sessionCameraFollowSharpness;
    public Vector3 ReadSessionCameraInput()
    {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float verticalRatio = sessionVerticalSpeedMetersPerSecond / Mathf.Max(1f, sessionCruiseSpeedMetersPerSecond);

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

        return false;
    }

    public static bool IsGameplayCursorReleasePressed()
    {
        return IsKeyPressed(WildWindInputKey.LeftAlt) || IsKeyPressed(WildWindInputKey.RightAlt);
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
            WildWindInputKey.LeftAlt => keyboard.leftAltKey.isPressed,
            WildWindInputKey.RightAlt => keyboard.rightAltKey.isPressed,
            _ => false
        };
    }
#endif

}
