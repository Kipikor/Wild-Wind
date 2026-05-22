using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldSimulationTick : MonoBehaviour
{
    [Header("References")]
    [SerializeField, InspectorName("World Runtime")] private WorldRegionRuntime world;
    [SerializeField, InspectorName("World Entity Index")] private WorldEntityIndex index;
    [SerializeField, InspectorName("World Runtime State")] private WorldRuntimeState runtimeState;
    [SerializeField, InspectorName("Bubble Streamer")] private WorldBubbleStreamer streamer;
    [SerializeField, InspectorName("Focus")] private Transform focus;

    [Header("Tick")]
    [SerializeField, InspectorName("Run In Play Mode")] private bool runInPlayMode = true;
    [SerializeField, Min(0.1f), InspectorName("Real Tick Interval, s")] private float realTickIntervalSeconds = 1f;
    [SerializeField, Min(0.1f), InspectorName("Simulated Seconds Per Tick")] private float simulatedSecondsPerTick = 15f;
    [SerializeField, InspectorName("Refresh Bubble Each Tick")] private bool refreshBubbleEachTick = true;
    [SerializeField, InspectorName("Simulate Far Resources")] private bool simulateFarResources = true;

    [Header("Far Simulation, kg/min")]
    [SerializeField, Min(0f), InspectorName("Cloud Passive Harvest")] private float cloudPassiveHarvestKgPerMinute = 0.75f;
    [SerializeField, Min(0f), InspectorName("Ore Passive Harvest")] private float resourcePassiveHarvestKgPerMinute = 0.35f;
    [SerializeField, Min(0f), InspectorName("Iceberg Passive Drift")] private float icebergPassiveDriftKgPerMinute = 0.04f;

    [Header("Environment")]
    [SerializeField, InspectorName("Habitation Technical Visibility, m")] private float habitationTechnicalVisibilityMeters = 6000f;
    [SerializeField, InspectorName("Upper Technical Visibility, m")] private float upperTechnicalVisibilityMeters = 14000f;

    [Header("Debug")]
    [SerializeField, InspectorName("Log Ticks")] private bool logTicks = false;

    [SerializeField, HideInInspector] private long tickCount;
    [SerializeField, HideInInspector] private float totalSimulatedSeconds;
    [SerializeField, HideInInspector] private WorldAltitudeBand currentAltitudeBand = WorldAltitudeBand.Habitation;
    [SerializeField, HideInInspector] private float currentVisibilityMeters;
    [SerializeField, HideInInspector] private float currentWindMetersPerSecond;
    [SerializeField, HideInInspector] private float currentStormDamagePerMinute;
    [SerializeField, HideInInspector] private float currentClaudiumLift01 = 1f;
    [SerializeField, HideInInspector] private int lastTouchedEntities;
    [SerializeField, HideInInspector] private float lastExtractedKg;
    [SerializeField, HideInInspector] private string lastSummary = "";

    private float realTickAccumulator;

    public long TickCount => tickCount;
    public float TotalSimulatedSeconds => totalSimulatedSeconds;
    public WorldAltitudeBand CurrentAltitudeBand => currentAltitudeBand;
    public float CurrentVisibilityMeters => currentVisibilityMeters;
    public float CurrentWindMetersPerSecond => currentWindMetersPerSecond;
    public float CurrentStormDamagePerMinute => currentStormDamagePerMinute;
    public float CurrentClaudiumLift01 => currentClaudiumLift01;
    public int LastTouchedEntities => lastTouchedEntities;
    public float LastExtractedKg => lastExtractedKg;
    public string LastSummary => lastSummary;
    public float SimulatedSecondsPerTick => simulatedSecondsPerTick;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!runInPlayMode || !Application.isPlaying)
        {
            return;
        }

        realTickAccumulator += Time.deltaTime;
        if (realTickAccumulator < realTickIntervalSeconds)
        {
            return;
        }

        realTickAccumulator = 0f;
        TickOnce(simulatedSecondsPerTick);
    }

    public void Configure(
        WorldRegionRuntime newWorld,
        WorldEntityIndex newIndex,
        WorldRuntimeState newRuntimeState,
        Transform newFocus,
        WorldBubbleStreamer newStreamer)
    {
        world = newWorld;
        index = newIndex;
        runtimeState = newRuntimeState;
        focus = newFocus;
        streamer = newStreamer;
        Initialize();
    }

    [ContextMenu("Initialize Simulation Tick")]
    public void Initialize()
    {
        ResolveReferences();
        if (runtimeState != null)
        {
            runtimeState.Configure(world, index, focus);
        }

        if (focus != null)
        {
            ApplyEnvironmentSample(EvaluateEnvironment(focus.position.y));
        }
    }

    [ContextMenu("Tick Once")]
    public TickResult TickOnce()
    {
        return TickOnce(simulatedSecondsPerTick);
    }

    public TickResult TickOnce(float simulatedSeconds)
    {
        ResolveReferences();
        simulatedSeconds = Mathf.Max(0.01f, simulatedSeconds);
        tickCount++;
        totalSimulatedSeconds += simulatedSeconds;

        if (runtimeState != null)
        {
            runtimeState.InitializeFromWorld(true);
        }

        Vector3 focusPosition = focus != null ? focus.position : Vector3.zero;
        EnvironmentSample environment = EvaluateEnvironment(focusPosition.y);
        ApplyEnvironmentSample(environment);

        if (refreshBubbleEachTick && runtimeState != null && world != null)
        {
            runtimeState.RefreshActiveBubble(focusPosition, world.ActiveBubbleRadiusMeters);
        }

        if (refreshBubbleEachTick && streamer != null)
        {
            streamer.RefreshNow();
        }

        int touched = 0;
        float extracted = 0f;
        if (simulateFarResources && runtimeState != null)
        {
            SimulateFarResourceTick(simulatedSeconds / 60f, ref touched, ref extracted);
        }

        lastTouchedEntities = touched;
        lastExtractedKg = extracted;
        lastSummary = "tick=" + tickCount +
            ", sim=" + simulatedSeconds.ToString("0.#") + "s" +
            ", band=" + currentAltitudeBand +
            ", visibility=" + currentVisibilityMeters.ToString("0") + "m" +
            ", far touched=" + touched +
            ", extracted=" + extracted.ToString("0.##") + "kg";

        if (logTicks)
        {
            Debug.Log("[WorldSimulationTick] " + lastSummary, this);
        }

        return new TickResult(tickCount, simulatedSeconds, environment, touched, extracted);
    }

    public TickResult TickMinutes(float minutes)
    {
        return TickOnce(Mathf.Max(0.01f, minutes) * 60f);
    }

    public EnvironmentSample EvaluateEnvironment(float altitudeMeters)
    {
        WorldAltitudeBand band = world != null ? world.EvaluateAltitudeBand(altitudeMeters) : EvaluateFallbackAltitudeBand(altitudeMeters);
        float visibility;
        float wind;
        float damage;
        float lift;

        switch (band)
        {
            case WorldAltitudeBand.DeadlyStorm:
                visibility = 35f;
                wind = 50f;
                damage = 10000f;
                lift = 1f;
                break;
            case WorldAltitudeBand.ViolentStorm:
                visibility = 100f;
                wind = 34f;
                damage = 18f;
                lift = 1f;
                break;
            case WorldAltitudeBand.CalmStorm:
                visibility = 1000f;
                wind = 12f;
                damage = 2f;
                lift = 1f;
                break;
            case WorldAltitudeBand.Habitation:
                visibility = Mathf.Max(1000f, habitationTechnicalVisibilityMeters);
                wind = 6f;
                damage = 0f;
                lift = 1f;
                break;
            case WorldAltitudeBand.ThinAir:
                visibility = Mathf.Max(habitationTechnicalVisibilityMeters, upperTechnicalVisibilityMeters);
                wind = 22f;
                damage = 0f;
                lift = Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(10000f, 40000f, altitudeMeters));
                break;
            case WorldAltitudeBand.Ice:
                visibility = Mathf.Max(habitationTechnicalVisibilityMeters, upperTechnicalVisibilityMeters);
                wind = 0f;
                damage = 4f;
                lift = Mathf.Lerp(0.25f, 0f, Mathf.InverseLerp(40000f, 100000f, altitudeMeters));
                break;
            default:
                visibility = Mathf.Max(habitationTechnicalVisibilityMeters, upperTechnicalVisibilityMeters);
                wind = 0f;
                damage = 50f;
                lift = 0f;
                break;
        }

        return new EnvironmentSample(band, altitudeMeters, visibility, wind, damage, Mathf.Clamp01(lift));
    }

    private void SimulateFarResourceTick(float simulatedMinutes, ref int touched, ref float extracted)
    {
        for (int i = 0; i < runtimeState.Entities.Count; i++)
        {
            WorldRuntimeState.EntityRuntimeState entity = runtimeState.Entities[i];
            if (entity == null || entity.activeInBubble || entity.remainingAmount <= 0f)
            {
                continue;
            }

            float amount = GetPassiveAmountPerMinute(entity.kind) * simulatedMinutes * GetEntityActivityFactor(entity.id);
            if (amount <= 0f)
            {
                continue;
            }

            if (runtimeState.TryExtract(entity.kind, entity.id, amount, out float entityExtracted))
            {
                touched++;
                extracted += entityExtracted;
            }
        }
    }

    private float GetPassiveAmountPerMinute(WorldEntityKind kind)
    {
        switch (kind)
        {
            case WorldEntityKind.CloudField:
                return cloudPassiveHarvestKgPerMinute;
            case WorldEntityKind.ResourceField:
                return resourcePassiveHarvestKgPerMinute;
            case WorldEntityKind.IcebergField:
                return icebergPassiveDriftKgPerMinute;
            default:
                return 0f;
        }
    }

    private static float GetEntityActivityFactor(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 1f;
        }

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < id.Length; i++)
            {
                hash = hash * 31 + id[i];
            }

            return 0.7f + Mathf.Abs(hash % 61) / 100f;
        }
    }

    private void ApplyEnvironmentSample(EnvironmentSample sample)
    {
        currentAltitudeBand = sample.band;
        currentVisibilityMeters = sample.visibilityMeters;
        currentWindMetersPerSecond = sample.windMetersPerSecond;
        currentStormDamagePerMinute = sample.stormDamagePerMinute;
        currentClaudiumLift01 = sample.claudiumLift01;
    }

    private void ResolveReferences()
    {
        if (world == null)
        {
            world = FindFirstObjectByType<WorldRegionRuntime>();
        }

        if (index == null)
        {
            index = FindFirstObjectByType<WorldEntityIndex>();
        }

        if (runtimeState == null)
        {
            runtimeState = FindFirstObjectByType<WorldRuntimeState>();
        }

        if (streamer == null)
        {
            streamer = FindFirstObjectByType<WorldBubbleStreamer>();
        }

        if (focus == null && world != null)
        {
            focus = world.Focus;
        }
    }

    private static WorldAltitudeBand EvaluateFallbackAltitudeBand(float altitudeMeters)
    {
        if (altitudeMeters <= 0f) return WorldAltitudeBand.DeadlyStorm;
        if (altitudeMeters < 1000f) return WorldAltitudeBand.ViolentStorm;
        if (altitudeMeters < 2000f) return WorldAltitudeBand.CalmStorm;
        if (altitudeMeters < 10000f) return WorldAltitudeBand.Habitation;
        if (altitudeMeters < 40000f) return WorldAltitudeBand.ThinAir;
        if (altitudeMeters < 100000f) return WorldAltitudeBand.Ice;
        return WorldAltitudeBand.BeyondClaudiumLift;
    }

    [Serializable]
    public readonly struct EnvironmentSample
    {
        public readonly WorldAltitudeBand band;
        public readonly float altitudeMeters;
        public readonly float visibilityMeters;
        public readonly float windMetersPerSecond;
        public readonly float stormDamagePerMinute;
        public readonly float claudiumLift01;

        public EnvironmentSample(
            WorldAltitudeBand band,
            float altitudeMeters,
            float visibilityMeters,
            float windMetersPerSecond,
            float stormDamagePerMinute,
            float claudiumLift01)
        {
            this.band = band;
            this.altitudeMeters = altitudeMeters;
            this.visibilityMeters = visibilityMeters;
            this.windMetersPerSecond = windMetersPerSecond;
            this.stormDamagePerMinute = stormDamagePerMinute;
            this.claudiumLift01 = claudiumLift01;
        }
    }

    public readonly struct TickResult
    {
        public readonly long tick;
        public readonly float simulatedSeconds;
        public readonly EnvironmentSample environment;
        public readonly int touchedEntities;
        public readonly float extractedKg;

        public TickResult(long tick, float simulatedSeconds, EnvironmentSample environment, int touchedEntities, float extractedKg)
        {
            this.tick = tick;
            this.simulatedSeconds = simulatedSeconds;
            this.environment = environment;
            this.touchedEntities = touchedEntities;
            this.extractedKg = extractedKg;
        }
    }
}
