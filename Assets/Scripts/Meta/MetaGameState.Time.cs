using System;
using UnityEngine;

public partial class MetaGameState
{
    [Header("Ускорение времени")]
    [InspectorName("Множитель времени")]
    [Range(1f, 64f)]
    public float gameTimeScale = 1f;
    [InspectorName("Ускорять физику Unity")]
    [Tooltip("Если включено, полет игрока тоже ускоряется через Time.timeScale, но не выше безопасного лимита ниже.")]
    public bool accelerateUnityTimeScale = true;
    [InspectorName("Лимит физики Unity")]
    [Range(1f, 8f)]
    public float maxUnityTimeScale = 4f;
    [InspectorName("Максимальный шаг мета-времени, сек")]
    [Tooltip("Большие ускоренные промежутки нарезаются на шаги, чтобы производство, исследования, погрузка и логистика не прыгали одним грубым куском.")]
    public float maxAcceleratedProcessStepSeconds = 15f;
    [InspectorName("Offline-прогресс при загрузке")]
    [Tooltip("Если включено, при запуске игры симуляция догоняет время, прошедшее с последнего сохранения.")]
    public bool processOfflineProgressOnLoad = true;
    [InspectorName("Лимит offline-догонки, часов")]
    [Tooltip("Защита от огромных скачков системных часов. Для теста 100 часов оставьте значение выше 100.")]
    public float maxOfflineCatchUpHours = 240f;

    private DateTime lastProcessRealtimeUtc;
    private float originalFixedDeltaTime = -1f;
    private float originalMaximumDeltaTime = -1f;

    public DateTime CurrentProcessUtcNow => GetProcessUtcNow();

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

    private bool TryAdvanceOfflineProgressFromLastSave(DateTime realNowUtc, out int changedEvents)
    {
        changedEvents = 0;
        if (!processOfflineProgressOnLoad || progress == null || progress.lastSavedUtcTicks <= 0) return false;

        long elapsedTicks = realNowUtc.Ticks - progress.lastSavedUtcTicks;
        if (elapsedTicks <= TimeSpan.FromSeconds(1).Ticks) return false;

        double elapsedHours = new TimeSpan(elapsedTicks).TotalHours;
        double cappedHours = Math.Min(elapsedHours, Math.Max(0.01f, maxOfflineCatchUpHours));
        if (cappedHours <= 0d) return false;

        changedEvents = FastForwardSimulation(TimeSpan.FromHours(cappedHours));
        string capText = elapsedHours > cappedHours + 0.001d ? $" (ограничено с {elapsedHours:0.#} ч)" : "";
        lastSaveMessage = $"Offline-прогресс: прошло {cappedHours:0.#} ч{capText}, событий {changedEvents}.";
        return true;
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
        lastSaveMessage = $"Перемотка: {duration.TotalHours:0.#} ч, событий {changedEvents}.";
        return changedEvents;
    }

    private void DrawTimeScaleUi()
    {
        GUILayout.Space(6f);
        GUILayout.Label($"Время: x{gameTimeScale:0.#}   физика: x{(accelerateUnityTimeScale ? Mathf.Min(gameTimeScale, maxUnityTimeScale) : 1f):0.#}");
        GUILayout.BeginHorizontal();
        DrawTimeScaleButton(1f);
        DrawTimeScaleButton(2f);
        DrawTimeScaleButton(4f);
        DrawTimeScaleButton(8f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        DrawTimeScaleButton(16f);
        DrawTimeScaleButton(32f);
        DrawTimeScaleButton(64f);
        GUILayout.EndHorizontal();
        GUILayout.Label("Перемотка");
        GUILayout.BeginHorizontal();
        DrawFastForwardButton(1f, "+1ч");
        DrawFastForwardButton(8f, "+8ч");
        DrawFastForwardButton(24f, "+24ч");
        DrawFastForwardButton(100f, "+100ч");
        GUILayout.EndHorizontal();
    }

    private void DrawTimeScaleButton(float scale)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && !Mathf.Approximately(gameTimeScale, scale);
        if (GUILayout.Button("x" + scale.ToString("0")))
        {
            gameTimeScale = scale;
            ResetProcessRealtimeClock();
            ApplyUnityTimeScale();
        }

        GUI.enabled = wasEnabled;
    }

    private void DrawFastForwardButton(float hours, string label)
    {
        if (GUILayout.Button(label))
        {
            FastForwardSimulationHours(hours);
        }
    }
}
