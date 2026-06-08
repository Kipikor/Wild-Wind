using System;
using UnityEngine;

public partial class MetaGameState
{
    [Header("Session Time")]
    [InspectorName("Time Scale")]
    [Range(1f, 64f)]
    public float gameTimeScale = 1f;
    [InspectorName("Accelerate Unity Physics")]
    [Tooltip("When enabled, player flight also uses Time.timeScale, clamped by the safety limit.")]
    public bool accelerateUnityTimeScale = true;
    [InspectorName("Unity Physics Limit")]
    [Range(1f, 8f)]
    public float maxUnityTimeScale = 4f;
    [InspectorName("Max Session Time Step, Seconds")]
    [Tooltip("Large accelerated intervals are sliced so session processes advance in stable chunks.")]
    public float maxAcceleratedProcessStepSeconds = 15f;

    private DateTime lastProcessRealtimeUtc;
    private float originalFixedDeltaTime = -1f;
    private float originalMaximumDeltaTime = -1f;
    private bool sessionPaused;

    public DateTime CurrentProcessUtcNow => GetProcessUtcNow();
    public bool IsSessionPaused => sessionPaused;

    public void SetSessionPaused(bool paused)
    {
        if (sessionPaused == paused)
        {
            return;
        }

        sessionPaused = paused;
        if (paused)
        {
            CacheUnityTimeSettings();
            Time.timeScale = 0f;
            return;
        }

        ResetProcessRealtimeClock();
        ApplyUnityTimeScale();
    }

    private void CacheUnityTimeSettings()
    {
        if (originalFixedDeltaTime <= 0f)
        {
            originalFixedDeltaTime = Time.fixedDeltaTime;
        }

        if (originalMaximumDeltaTime <= 0f)
        {
            originalMaximumDeltaTime = Time.maximumDeltaTime;
        }
    }

    private void RestoreUnityTimeSettings()
    {
        if (!Application.isPlaying) return;
        if (originalFixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = originalFixedDeltaTime;
        }

        if (originalMaximumDeltaTime > 0f)
        {
            Time.maximumDeltaTime = originalMaximumDeltaTime;
        }

        Time.timeScale = 1f;
    }

    private void ApplyUnityTimeScale()
    {
        if (!Application.isPlaying) return;
        CacheUnityTimeSettings();

        if (sessionPaused)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = originalFixedDeltaTime > 0f ? originalFixedDeltaTime : Time.fixedDeltaTime;
            Time.maximumDeltaTime = originalMaximumDeltaTime > 0f ? originalMaximumDeltaTime : Time.maximumDeltaTime;
            return;
        }

        float physicsScale = accelerateUnityTimeScale ? Mathf.Clamp(gameTimeScale, 1f, Mathf.Max(1f, maxUnityTimeScale)) : 1f;
        Time.timeScale = physicsScale;
        Time.fixedDeltaTime = originalFixedDeltaTime > 0f ? originalFixedDeltaTime : Time.fixedDeltaTime;
        Time.maximumDeltaTime = originalMaximumDeltaTime > 0f ? Mathf.Max(originalMaximumDeltaTime, Time.fixedDeltaTime * 4f) : Time.maximumDeltaTime;
    }

    private void ResetProcessRealtimeClock()
    {
        lastProcessRealtimeUtc = DateTime.UtcNow;
    }

    private void AdvanceScaledRealTimeProcesses()
    {
        DateTime realNow = DateTime.UtcNow;
        if (lastProcessRealtimeUtc.Ticks <= 0)
        {
            lastProcessRealtimeUtc = realNow;
        }

        double realSeconds = Math.Max(0d, (realNow - lastProcessRealtimeUtc).TotalSeconds);
        lastProcessRealtimeUtc = realNow;
        if (realSeconds <= 0d) return;

        long startTicks = progress != null && progress.lastProcessUtcTicks > 0 ? progress.lastProcessUtcTicks : realNow.Ticks;
        double scaledSeconds = realSeconds * Mathf.Max(1f, gameTimeScale);
        long deltaTicks = TimeSpan.FromSeconds(scaledSeconds).Ticks;
        if (deltaTicks <= 0) return;

        long maxTicks = DateTime.MaxValue.Ticks;
        long targetTicks = startTicks > maxTicks - deltaTicks ? maxTicks : startTicks + deltaTicks;
        AdvanceRealTimeProcessesSliced(new DateTime(targetTicks, DateTimeKind.Utc));
    }

    private DateTime GetProcessUtcNow()
    {
        if (progress != null && progress.lastProcessUtcTicks > 0)
        {
            return new DateTime(progress.lastProcessUtcTicks, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }

    private int AdvanceRealTimeProcessesSliced(DateTime targetUtc)
    {
        if (progress == null) return 0;
        if (progress.lastProcessUtcTicks <= 0)
        {
            return AdvanceRealTimeProcesses(targetUtc);
        }

        long targetTicks = targetUtc.Ticks;
        long currentTicks = progress.lastProcessUtcTicks;
        if (targetTicks <= currentTicks) return 0;

        long stepTicks = TimeSpan.FromSeconds(Mathf.Max(0.25f, maxAcceleratedProcessStepSeconds)).Ticks;
        int total = 0;
        int guard = 0;
        while (currentTicks < targetTicks && guard < 100000)
        {
            guard++;
            long nextTicks = Math.Min(targetTicks, currentTicks + stepTicks);
            total += AdvanceRealTimeProcesses(new DateTime(nextTicks, DateTimeKind.Utc));
            if (progress.lastProcessUtcTicks <= currentTicks)
            {
                break;
            }

            currentTicks = progress.lastProcessUtcTicks;
        }

        return total;
    }

    public int FastForwardSimulationHours(float hours)
    {
        return FastForwardSimulation(TimeSpan.FromHours(Mathf.Max(0f, hours)));
    }

    public int FastForwardSimulation(TimeSpan duration)
    {
        EnsureProgressInitialized();
        if (progress == null || duration <= TimeSpan.Zero) return 0;

        DateTime start = GetProcessUtcNow();
        long durationTicks = duration.Ticks;
        long maxTicks = DateTime.MaxValue.Ticks;
        long targetTicks = start.Ticks > maxTicks - durationTicks ? maxTicks : start.Ticks + durationTicks;
        int changedEvents = AdvanceRealTimeProcessesSliced(new DateTime(targetTicks, DateTimeKind.Utc));
        SyncShipConsumablesWithCargo(false);
        ResetProcessRealtimeClock();
        lastAccountMessage = $"Р СџР ВµРЎР‚Р ВµР СР С•РЎвЂљР С”Р В°: {duration.TotalHours:0.#} РЎвЂЎ, РЎРѓР С•Р В±РЎвЂ№РЎвЂљР С‘Р в„– {changedEvents}.";
        return changedEvents;
    }

}
