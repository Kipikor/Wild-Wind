using UnityEngine;

public sealed class WorldDebugTravelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField, InspectorName("Focus")] private Transform focus;
    [SerializeField, InspectorName("World Camera")] private Camera worldCamera;
    [SerializeField, InspectorName("Visual Tuner")] private VisualPlayModeTuner visualTuner;
    [SerializeField, InspectorName("Bubble Streamer")] private WorldBubbleStreamer bubbleStreamer;
    [SerializeField, InspectorName("Control Settings")] private WildWindControlSettings controlSettings;

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
            float speed = GetCruiseSpeed();
            if (IsSprintPressed())
            {
                speed *= GetSprintMultiplier();
            }

            focus.position += input * speed * Time.deltaTime;
            bubbleStreamer?.RefreshNow();
        }

        ApplyCamera(false);
        visualTuner?.ApplyNow();
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
        if (controlSettings != null)
        {
            return controlSettings.ReadDebugTravelInput();
        }

        float x = 0f;
        float y = 0f;
        float z = 0f;

        if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.A)) x -= 1f;
        if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.D)) x += 1f;
        if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.S)) z -= 1f;
        if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.W)) z += 1f;
        if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.Q)) y -= verticalSpeedMetersPerSecond / Mathf.Max(1f, cruiseSpeedMetersPerSecond);
        if (WildWindControlSettings.IsKeyPressed(WildWindInputKey.E)) y += verticalSpeedMetersPerSecond / Mathf.Max(1f, cruiseSpeedMetersPerSecond);

        Vector3 input = new Vector3(x, y, z);
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
    }

    private void ApplyCamera(bool snap)
    {
        if (worldCamera == null || focus == null)
        {
            return;
        }

        Vector3 effectiveCameraOffset = controlSettings != null ? controlSettings.DebugCameraOffset : cameraOffset;
        Vector3 effectiveLookOffset = controlSettings != null ? controlSettings.DebugCameraLookOffset : cameraLookOffset;
        float effectiveFollowSharpness = controlSettings != null ? controlSettings.DebugCameraFollowSharpness : followSharpness;

        Vector3 targetPosition = focus.position + effectiveCameraOffset;
        worldCamera.transform.position = snap
            ? targetPosition
            : Vector3.Lerp(worldCamera.transform.position, targetPosition, effectiveFollowSharpness);
        worldCamera.transform.LookAt(focus.position + effectiveLookOffset);
    }
}
