using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class WorldDebugTravelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField, InspectorName("Focus")] private Transform focus;
    [SerializeField, InspectorName("World Camera")] private Camera worldCamera;
    [SerializeField, InspectorName("Visual Tuner")] private VisualPlayModeTuner visualTuner;
    [SerializeField, InspectorName("Bubble Streamer")] private WorldBubbleStreamer bubbleStreamer;

    [Header("Movement")]
    [SerializeField, Range(50f, 4000f), InspectorName("Cruise Speed, m/s")] private float cruiseSpeedMetersPerSecond = 650f;
    [SerializeField, Range(50f, 3000f), InspectorName("Vertical Speed, m/s")] private float verticalSpeedMetersPerSecond = 420f;
    [SerializeField, Range(2f, 20f), InspectorName("Sprint Multiplier")] private float sprintMultiplier = 6f;

    [Header("Camera")]
    [SerializeField, InspectorName("Camera Offset")] private Vector3 cameraOffset = new Vector3(-760f, 240f, -820f);
    [SerializeField, InspectorName("Camera Look Offset")] private Vector3 cameraLookOffset = new Vector3(120f, 20f, 120f);
    [SerializeField, Range(0.01f, 1f), InspectorName("Follow Sharpness")] private float followSharpness = 0.16f;

    private void Awake()
    {
        ResolveReferences();
        ApplyCamera(true);
    }

    private void Update()
    {
        ResolveReferences();
        if (focus == null)
        {
            return;
        }

        Vector3 input = ReadMovementInput();
        if (input.sqrMagnitude > 0.0001f)
        {
            float speed = cruiseSpeedMetersPerSecond;
            if (IsKeyPressed(DebugTravelKey.Sprint))
            {
                speed *= sprintMultiplier;
            }

            focus.position += input * speed * Time.deltaTime;
            bubbleStreamer?.RefreshNow();
        }

        ApplyCamera(false);
        visualTuner?.ApplyNow();
    }

    public void Configure(Transform newFocus, Camera newCamera, VisualPlayModeTuner newVisualTuner, WorldBubbleStreamer newStreamer)
    {
        focus = newFocus;
        worldCamera = newCamera;
        visualTuner = newVisualTuner;
        bubbleStreamer = newStreamer;
        ApplyCamera(true);
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
        float x = 0f;
        float y = 0f;
        float z = 0f;

        if (IsKeyPressed(DebugTravelKey.Left)) x -= 1f;
        if (IsKeyPressed(DebugTravelKey.Right)) x += 1f;
        if (IsKeyPressed(DebugTravelKey.Backward)) z -= 1f;
        if (IsKeyPressed(DebugTravelKey.Forward)) z += 1f;
        if (IsKeyPressed(DebugTravelKey.Down)) y -= verticalSpeedMetersPerSecond / Mathf.Max(1f, cruiseSpeedMetersPerSecond);
        if (IsKeyPressed(DebugTravelKey.Up)) y += verticalSpeedMetersPerSecond / Mathf.Max(1f, cruiseSpeedMetersPerSecond);

        Vector3 input = new Vector3(x, y, z);
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private static bool IsKeyPressed(DebugTravelKey key)
    {
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
    private static bool TryReadInputSystemKey(DebugTravelKey key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        return key switch
        {
            DebugTravelKey.Forward => keyboard.wKey.isPressed,
            DebugTravelKey.Backward => keyboard.sKey.isPressed,
            DebugTravelKey.Left => keyboard.aKey.isPressed,
            DebugTravelKey.Right => keyboard.dKey.isPressed,
            DebugTravelKey.Down => keyboard.qKey.isPressed,
            DebugTravelKey.Up => keyboard.eKey.isPressed,
            DebugTravelKey.Sprint => keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed,
            _ => false
        };
    }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
    private static bool TryReadLegacyInputKey(DebugTravelKey key)
    {
        return key switch
        {
            DebugTravelKey.Forward => Input.GetKey(KeyCode.W),
            DebugTravelKey.Backward => Input.GetKey(KeyCode.S),
            DebugTravelKey.Left => Input.GetKey(KeyCode.A),
            DebugTravelKey.Right => Input.GetKey(KeyCode.D),
            DebugTravelKey.Down => Input.GetKey(KeyCode.Q),
            DebugTravelKey.Up => Input.GetKey(KeyCode.E),
            DebugTravelKey.Sprint => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            _ => false
        };
    }
#endif

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
    }

    private void ApplyCamera(bool snap)
    {
        if (worldCamera == null || focus == null)
        {
            return;
        }

        Vector3 targetPosition = focus.position + cameraOffset;
        worldCamera.transform.position = snap
            ? targetPosition
            : Vector3.Lerp(worldCamera.transform.position, targetPosition, followSharpness);
        worldCamera.transform.LookAt(focus.position + cameraLookOffset);
    }

    private enum DebugTravelKey
    {
        Forward,
        Backward,
        Left,
        Right,
        Down,
        Up,
        Sprint
    }
}
