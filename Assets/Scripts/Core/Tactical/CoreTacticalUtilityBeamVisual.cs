using System.Collections.Generic;
using UnityEngine;

public enum CoreTacticalUtilityBeamPalette
{
    Magnet,
    Salvage,
    Drill,
    Repair,
    Scanner
}

public sealed class CoreTacticalUtilityBeamVisual : MonoBehaviour
{
    private const float DefaultVisibleSeconds = 0.12f;
    private const float MinimumBeamLengthSqr = 0.01f;

    private LineRenderer haloLine;
    private LineRenderer coreLine;
    private Material haloMaterial;
    private Material coreMaterial;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Color startColor;
    private Color middleColor;
    private Color endColor;
    private float visibleUntil;
    private float coreWidthMeters = 0.85f;
    private float haloWidthMeters = 3.4f;
    private float rippleMeters = 1.4f;
    private float phase;

    public static CoreTacticalUtilityBeamVisual GetOrCreate(Transform parent, string objectName, CoreTacticalUtilityBeamPalette palette)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        CoreTacticalUtilityBeamVisual visual = child.GetComponent<CoreTacticalUtilityBeamVisual>();
        if (visual == null)
        {
            visual = child.gameObject.AddComponent<CoreTacticalUtilityBeamVisual>();
        }

        visual.ConfigurePalette(palette);
        return visual;
    }

    public static void ResolvePalette(CoreTacticalUtilityBeamPalette palette, out Color start, out Color middle, out Color end)
    {
        switch (palette)
        {
            case CoreTacticalUtilityBeamPalette.Magnet:
                start = new Color(0.18f, 1f, 0.84f, 0.92f);
                middle = new Color(0.24f, 0.76f, 1f, 0.96f);
                end = new Color(0.42f, 1f, 0.32f, 0.64f);
                break;
            case CoreTacticalUtilityBeamPalette.Salvage:
                start = new Color(1f, 0.48f, 0.18f, 0.92f);
                middle = new Color(1f, 0.88f, 0.38f, 0.98f);
                end = new Color(0.88f, 0.36f, 1f, 0.62f);
                break;
            case CoreTacticalUtilityBeamPalette.Drill:
                start = new Color(1f, 0.76f, 0.16f, 0.94f);
                middle = new Color(1f, 0.96f, 0.48f, 1f);
                end = new Color(1f, 0.38f, 0.12f, 0.64f);
                break;
            case CoreTacticalUtilityBeamPalette.Scanner:
                start = new Color(0.58f, 0.36f, 1f, 0.92f);
                middle = new Color(0.22f, 0.92f, 1f, 0.98f);
                end = new Color(0.14f, 0.42f, 1f, 0.62f);
                break;
            default:
                start = new Color(0.24f, 1f, 0.48f, 0.92f);
                middle = new Color(0.92f, 1f, 0.62f, 1f);
                end = new Color(0.22f, 0.90f, 1f, 0.64f);
                break;
        }
    }

    public void ConfigurePalette(CoreTacticalUtilityBeamPalette palette)
    {
        ResolvePalette(palette, out startColor, out middleColor, out endColor);
        switch (palette)
        {
            case CoreTacticalUtilityBeamPalette.Magnet:
                coreWidthMeters = 0.95f;
                haloWidthMeters = 4.2f;
                rippleMeters = 2.2f;
                break;
            case CoreTacticalUtilityBeamPalette.Salvage:
                coreWidthMeters = 0.55f;
                haloWidthMeters = 2.2f;
                rippleMeters = 0.65f;
                break;
            case CoreTacticalUtilityBeamPalette.Drill:
                coreWidthMeters = 0.72f;
                haloWidthMeters = 2.9f;
                rippleMeters = 1.05f;
                break;
            case CoreTacticalUtilityBeamPalette.Scanner:
                coreWidthMeters = 0.82f;
                haloWidthMeters = 3.6f;
                rippleMeters = 1.8f;
                break;
            default:
                coreWidthMeters = 1.05f;
                haloWidthMeters = 4.6f;
                rippleMeters = 1.55f;
                break;
        }

        EnsureLines();
    }

    public void Show(Vector3 start, Vector3 end, float visibleSeconds = DefaultVisibleSeconds)
    {
        if (!IsFinite(start) || !IsFinite(end) || (end - start).sqrMagnitude <= MinimumBeamLengthSqr)
        {
            HideNow();
            return;
        }

        EnsureLines();
        startPosition = start;
        endPosition = end;
        visibleUntil = Time.time + Mathf.Max(0.02f, visibleSeconds);
        if (haloLine != null) haloLine.gameObject.SetActive(true);
        if (coreLine != null) coreLine.gameObject.SetActive(true);
        ApplyBeamShape();
    }

    public void Hide()
    {
        HideNow();
    }

    private void Awake()
    {
        phase = Random.value * Mathf.PI * 2f;
        EnsureLines();
        HideNow();
    }

    private void LateUpdate()
    {
        if (Time.time > visibleUntil)
        {
            HideNow();
            return;
        }

        ApplyBeamShape();
    }

    private void OnDisable()
    {
        HideNow();
    }

    private void OnDestroy()
    {
        DestroyMaterial(haloMaterial);
        DestroyMaterial(coreMaterial);
    }

    private void EnsureLines()
    {
        if (haloLine == null)
        {
            haloLine = CreateLine("Halo");
            haloMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(endColor);
            haloLine.sharedMaterial = haloMaterial;
        }

        if (coreLine == null)
        {
            coreLine = CreateLine("Core");
            coreMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(middleColor);
            coreLine.sharedMaterial = coreMaterial;
        }
    }

    private LineRenderer CreateLine(string suffix)
    {
        GameObject lineObject = new GameObject(gameObject.name + " " + suffix);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 4;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.gameObject.SetActive(false);
        return line;
    }

    private void ApplyBeamShape()
    {
        if (haloLine == null || coreLine == null)
        {
            return;
        }

        Vector3 direction = endPosition - startPosition;
        float length = direction.magnitude;
        if (length <= 0.001f)
        {
            HideNow();
            return;
        }

        Vector3 forward = direction / length;
        Vector3 side = Vector3.Cross(forward, Vector3.up);
        if (side.sqrMagnitude <= 0.001f)
        {
            side = Vector3.Cross(forward, Vector3.right);
        }

        side.Normalize();
        Vector3 lift = Vector3.Cross(side, forward).normalized;
        float pulse = 0.78f + Mathf.Sin(Time.time * 12.5f + phase) * 0.22f;
        float wave = Mathf.Sin(Time.time * 9.0f + phase) * rippleMeters * Mathf.Clamp01(length / 60f);
        Vector3 firstBend = startPosition + direction * 0.34f + lift * wave + side * (wave * 0.32f);
        Vector3 secondBend = startPosition + direction * 0.68f - lift * (wave * 0.64f) - side * (wave * 0.18f);

        ApplyLine(haloLine, haloWidthMeters * (0.88f + pulse * 0.25f), true, firstBend, secondBend, pulse);
        ApplyLine(coreLine, coreWidthMeters * (0.92f + pulse * 0.18f), false, firstBend, secondBend, pulse);
    }

    private void ApplyLine(LineRenderer line, float width, bool halo, Vector3 firstBend, Vector3 secondBend, float pulse)
    {
        line.positionCount = 4;
        line.SetPosition(0, startPosition);
        line.SetPosition(1, firstBend);
        line.SetPosition(2, secondBend);
        line.SetPosition(3, endPosition);
        line.startWidth = width;
        line.endWidth = halo ? width * 0.25f : width * 0.42f;
        line.colorGradient = BuildGradient(halo ? 0.42f + pulse * 0.18f : 0.78f + pulse * 0.20f);
    }

    private Gradient BuildGradient(float alphaScale)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(middleColor, 0.52f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(startColor.a * alphaScale), 0f),
                new GradientAlphaKey(Mathf.Clamp01(middleColor.a * alphaScale), 0.52f),
                new GradientAlphaKey(Mathf.Clamp01(endColor.a * alphaScale), 1f)
            });
        return gradient;
    }

    private void HideNow()
    {
        if (haloLine != null) haloLine.gameObject.SetActive(false);
        if (coreLine != null) coreLine.gameObject.SetActive(false);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void DestroyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }
}

public sealed class CoreTacticalSiphonIntakeVisual : MonoBehaviour
{
    private const int RingCount = 4;
    private const int SegmentCount = 48;
    private const float DefaultVisibleSeconds = 0.24f;
    private const float MinimumLengthSqr = 0.01f;

    private readonly LineRenderer[] rings = new LineRenderer[RingCount];

    private Material ringMaterial;
    private Vector3 intakeOrigin;
    private Vector3 coneTarget;
    private float visibleUntil;
    private float phase;

    public static CoreTacticalSiphonIntakeVisual GetOrCreate(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName);
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        CoreTacticalSiphonIntakeVisual visual = child.GetComponent<CoreTacticalSiphonIntakeVisual>();
        if (visual == null)
        {
            visual = child.gameObject.AddComponent<CoreTacticalSiphonIntakeVisual>();
        }

        visual.EnsureRings();
        return visual;
    }

    public void Show(Vector3 origin, Vector3 target, float visibleSeconds = DefaultVisibleSeconds)
    {
        if (!IsFinite(origin) || !IsFinite(target) || (target - origin).sqrMagnitude <= MinimumLengthSqr)
        {
            HideNow();
            return;
        }

        EnsureRings();
        intakeOrigin = origin;
        coneTarget = target;
        visibleUntil = Time.time + Mathf.Max(0.02f, visibleSeconds);
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] != null)
            {
                rings[i].gameObject.SetActive(true);
            }
        }

        ApplyConeRings();
    }

    public void Hide()
    {
        HideNow();
    }

    private void Awake()
    {
        phase = Random.value;
        EnsureRings();
        HideNow();
    }

    private void LateUpdate()
    {
        if (Time.time > visibleUntil)
        {
            HideNow();
            return;
        }

        ApplyConeRings();
    }

    private void OnDisable()
    {
        HideNow();
    }

    private void OnDestroy()
    {
        if (ringMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(ringMaterial);
        }
        else
        {
            DestroyImmediate(ringMaterial);
        }
    }

    private void EnsureRings()
    {
        if (ringMaterial == null)
        {
            ringMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(new Color(0.36f, 1f, 0.88f, 0.88f));
        }

        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] != null)
            {
                continue;
            }

            GameObject ringObject = new GameObject("Siphon Intake Ring " + (i + 1));
            ringObject.transform.SetParent(transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.positionCount = SegmentCount + 1;
            ring.numCapVertices = 2;
            ring.numCornerVertices = 4;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = ringMaterial;
            ring.gameObject.SetActive(false);
            rings[i] = ring;
        }
    }

    private void ApplyConeRings()
    {
        Vector3 direction = coneTarget - intakeOrigin;
        float targetDistance = direction.magnitude;
        if (targetDistance <= 0.001f)
        {
            HideNow();
            return;
        }

        Vector3 forward = direction / targetDistance;
        Vector3 side = Vector3.Cross(forward, Vector3.up);
        if (side.sqrMagnitude <= 0.001f)
        {
            side = Vector3.Cross(forward, Vector3.right);
        }

        side.Normalize();
        Vector3 lift = Vector3.Cross(side, forward).normalized;
        float coneLength = Mathf.Clamp(targetDistance, 18f, 120f);
        float outerRadius = Mathf.Clamp(coneLength * 0.34f, 5.5f, 46f);
        float innerRadius = Mathf.Clamp(coneLength * 0.018f, 0.35f, 1.45f);
        float time = Time.time;

        for (int i = 0; i < rings.Length; i++)
        {
            LineRenderer ring = rings[i];
            if (ring == null)
            {
                continue;
            }

            float travel01 = Mathf.Repeat(time * 0.62f + phase + i / (float)RingCount, 1f);
            float easedTravel = 1f - Mathf.Pow(1f - travel01, 1.45f);
            float axisDistance = Mathf.Lerp(coneLength, 0.75f, easedTravel);
            float radius = Mathf.Lerp(outerRadius, innerRadius, easedTravel);
            float alpha = Mathf.Sin(travel01 * Mathf.PI);
            alpha *= Mathf.Lerp(0.44f, 0.95f, easedTravel);
            float width = Mathf.Clamp(radius * 0.075f, 0.34f, 1.85f);
            Vector3 center = intakeOrigin + forward * axisDistance;
            float swirl = time * (1.15f + i * 0.08f) + phase * Mathf.PI * 2f + travel01 * Mathf.PI * 2f;

            ring.startWidth = width;
            ring.endWidth = width;
            ring.colorGradient = BuildRingGradient(Mathf.Clamp01(alpha));

            for (int segment = 0; segment <= SegmentCount; segment++)
            {
                float angle = (segment / (float)SegmentCount) * Mathf.PI * 2f + swirl;
                float wobble = 1f + Mathf.Sin(angle * 3f + time * 2.2f + i) * 0.035f;
                Vector3 point = center
                    + side * (Mathf.Cos(angle) * radius * wobble)
                    + lift * (Mathf.Sin(angle) * radius * wobble);
                ring.SetPosition(segment, point);
            }
        }
    }

    private static Gradient BuildRingGradient(float alpha)
    {
        Color outer = new Color(0.34f, 1f, 0.86f, alpha * 0.66f);
        Color crest = new Color(0.78f, 1f, 0.96f, alpha);
        Color tail = new Color(0.18f, 0.72f, 1f, alpha * 0.46f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(outer, 0f),
                new GradientColorKey(crest, 0.52f),
                new GradientColorKey(tail, 1f)
            },
            new[]
            {
                new GradientAlphaKey(outer.a, 0f),
                new GradientAlphaKey(crest.a, 0.52f),
                new GradientAlphaKey(tail.a, 1f)
            });
        return gradient;
    }

    private void HideNow()
    {
        for (int i = 0; i < rings.Length; i++)
        {
            if (rings[i] != null)
            {
                rings[i].gameObject.SetActive(false);
            }
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public sealed class CoreTacticalAuxiliaryBeamEmitter : MonoBehaviour
{
    private sealed class AuxiliaryBeamChannel
    {
        public readonly int sideSign;
        public CoreTacticalUtilityBeamVisual beam;
        public float cycleTimer;
        public float cooldownRemaining;
        public int completedCycles;

        public AuxiliaryBeamChannel(int sideSign)
        {
            this.sideSign = sideSign < 0 ? -1 : 1;
        }
    }

    private readonly AuxiliaryBeamChannel[] channels =
    {
        new AuxiliaryBeamChannel(-1),
        new AuxiliaryBeamChannel(1)
    };

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor fallbackTarget;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public CoreTacticalUtilityBeamPalette palette = CoreTacticalUtilityBeamPalette.Repair;
    public float rangeMeters = 800f;
    public float sideArcDegrees = 175f;
    public float cycleSeconds = 1f;
    public float cooldownSeconds;
    public float repairHpPerCycle;

    public void Configure(
        CoreTacticalShipMotor newOwner,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam newTargetTeam,
        CoreTacticalUtilityBeamPalette newPalette,
        float newRangeMeters,
        float newCycleSeconds = 0f,
        float newCooldownSeconds = 0f,
        float newRepairHpPerCycle = 0f)
    {
        owner = newOwner != null ? newOwner : GetComponent<CoreTacticalShipMotor>();
        fallbackTarget = target;
        targetTeam = newTargetTeam;
        palette = newPalette;
        rangeMeters = Mathf.Max(1f, newRangeMeters);
        cycleSeconds = Mathf.Max(0.05f, newCycleSeconds > 0f ? newCycleSeconds : cycleSeconds);
        cooldownSeconds = Mathf.Max(0f, newCooldownSeconds);
        repairHpPerCycle = Mathf.Max(0f, newRepairHpPerCycle);
        EnsureBeams();
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        EnsureBeams();
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        if (owner == null)
        {
            return;
        }

        EnsureBeams();
        float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
        List<CoreTacticalShipMotor> reservedTargets = null;
        for (int i = 0; i < channels.Length; i++)
        {
            AuxiliaryBeamChannel channel = channels[i];
            if (channel == null || channel.beam == null)
            {
                continue;
            }

            if (channel.cooldownRemaining > 0f)
            {
                channel.cooldownRemaining = Mathf.Max(0f, channel.cooldownRemaining - deltaSeconds);
            }

            if (!TryResolveBeamEnd(channel.sideSign, reservedTargets, out Vector3 beamEnd, out CoreTacticalShipMotor target))
            {
                continue;
            }

            if (target != null)
            {
                reservedTargets ??= new List<CoreTacticalShipMotor>();
                reservedTargets.Add(target);
            }

            Vector3 start = GetBeamOrigin(channel.sideSign, beamEnd);
            channel.beam.Show(start, beamEnd, 0.14f);
            ApplyChannelEffect(channel, target, deltaSeconds);
        }
    }

    private void EnsureBeams()
    {
        if (owner == null)
        {
            return;
        }

        for (int i = 0; i < channels.Length; i++)
        {
            AuxiliaryBeamChannel channel = channels[i];
            if (channel == null)
            {
                continue;
            }

            string sideName = channel.sideSign < 0 ? "Left" : "Right";
            channel.beam ??= CoreTacticalUtilityBeamVisual.GetOrCreate(owner.transform, "Core Tactical " + palette + " Beam " + sideName, palette);
        }
    }

    private bool TryResolveBeamEnd(
        int sideSign,
        List<CoreTacticalShipMotor> reservedTargets,
        out Vector3 end,
        out CoreTacticalShipMotor selectedTarget)
    {
        end = Vector3.zero;
        selectedTarget = null;
        if (palette == CoreTacticalUtilityBeamPalette.Repair)
        {
            CoreTacticalShipMotor ally = FindDamagedFriendly(sideSign, reservedTargets);
            if (ally != null)
            {
                selectedTarget = ally;
                end = ally.transform.position + ally.transform.up * Mathf.Max(4f, ally.hullSizeMeters.y * 0.45f);
                return true;
            }

            return false;
        }

        CoreTacticalShipMotor target = ResolvePriorityTarget();
        if (target == null)
        {
            return false;
        }

        if (!IsInsideSideArc(sideSign, target.transform.position))
        {
            return false;
        }

        selectedTarget = target;
        end = target.transform.position + target.transform.up * Mathf.Max(4f, target.hullSizeMeters.y * 0.45f);
        return true;
    }

    private Vector3 GetBeamOrigin(int sideSign, Vector3 beamEnd)
    {
        Vector3 fallback = GetFallbackBeamOrigin(sideSign);
        CoreTacticalShipVisualWeaponBinding binding = owner != null ? owner.GetComponent<CoreTacticalShipVisualWeaponBinding>() : null;
        if (binding == null)
        {
            return fallback;
        }

        string utilityKind = palette == CoreTacticalUtilityBeamPalette.Scanner ? "scanner_hacker" : "repair";
        if (!binding.TryBindUtilityModule(utilityKind, sideSign, out CoreTacticalVisualWeaponHandle handle))
        {
            return fallback;
        }

        Vector3 direction = beamEnd - fallback;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = owner.transform.forward;
        }

        CoreTacticalShipVisualWeaponBinding.ApplyYawToward(handle, owner.transform, direction, sideSign < 0 ? -90f : 90f);
        return CoreTacticalShipVisualWeaponBinding.GetMuzzlePosition(handle, fallback, direction);
    }

    private void ApplyChannelEffect(AuxiliaryBeamChannel channel, CoreTacticalShipMotor target, float deltaSeconds)
    {
        if (channel == null || target == null || deltaSeconds <= 0f)
        {
            return;
        }

        if (channel.cooldownRemaining > 0f)
        {
            return;
        }

        if (palette == CoreTacticalUtilityBeamPalette.Repair)
        {
            TickRepairChannel(channel, target, deltaSeconds);
            return;
        }

        if (palette == CoreTacticalUtilityBeamPalette.Scanner)
        {
            TickScannerChannel(channel, deltaSeconds);
        }
    }

    private void TickRepairChannel(AuxiliaryBeamChannel channel, CoreTacticalShipMotor target, float deltaSeconds)
    {
        CoreTacticalPrototypeHealth health = target != null ? target.GetComponent<CoreTacticalPrototypeHealth>() : null;
        if (health == null || health.currentHealth >= health.maxHealth - 0.01f || repairHpPerCycle <= 0f)
        {
            channel.cycleTimer = 0f;
            return;
        }

        channel.cycleTimer += deltaSeconds;
        if (channel.cycleTimer < Mathf.Max(0.05f, cycleSeconds))
        {
            return;
        }

        channel.cycleTimer = 0f;
        channel.cooldownRemaining = Mathf.Max(0f, cooldownSeconds);
        health.currentHealth = Mathf.Min(Mathf.Max(1f, health.maxHealth), health.currentHealth + repairHpPerCycle);
        channel.completedCycles++;
    }

    private void TickScannerChannel(AuxiliaryBeamChannel channel, float deltaSeconds)
    {
        channel.cycleTimer += deltaSeconds;
        if (channel.cycleTimer < Mathf.Max(0.05f, cycleSeconds))
        {
            return;
        }

        channel.cycleTimer = 0f;
        channel.cooldownRemaining = Mathf.Max(0f, cooldownSeconds);
        channel.completedCycles++;
    }

    private Vector3 GetFallbackBeamOrigin(int sideSign)
    {
        if (owner == null)
        {
            return transform.position;
        }

        Vector3 center = GetBeamOriginCenter();
        Vector3 side = owner.transform.right * Mathf.Max(3f, owner.hullSizeMeters.x * 0.32f);
        Vector3 vertical = owner.transform.up * Mathf.Max(1.5f, owner.hullSizeMeters.y * 0.08f);
        return center + side * (sideSign < 0 ? -1f : 1f) + vertical;
    }

    private CoreTacticalShipMotor ResolvePriorityTarget()
    {
        CoreTacticalPriorityTargetControl priority = owner != null ? owner.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        if (priority != null && priority.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor priorityTarget))
        {
            return priorityTarget;
        }

        if (CoreTacticalPriorityTargetControl.IsValidPriorityTarget(fallbackTarget, targetTeam))
        {
            return fallbackTarget;
        }

        return null;
    }

    private CoreTacticalShipMotor FindDamagedFriendly(int sideSign, List<CoreTacticalShipMotor> reservedTargets)
    {
        if (owner == null)
        {
            return null;
        }

        CoreTacticalCombatant ownerCombatant = owner.GetComponent<CoreTacticalCombatant>();
        CoreTacticalCombatTeam friendlyTeam = ownerCombatant != null ? ownerCombatant.team : CoreTacticalCombatTeam.Friendly;
        CoreTacticalShipMotor[] ships = FindObjectsByType<CoreTacticalShipMotor>(FindObjectsSortMode.None);
        CoreTacticalShipMotor best = null;
        float bestDistanceSqr = float.PositiveInfinity;
        Vector3 origin = owner.transform.position;
        for (int i = 0; i < ships.Length; i++)
        {
            CoreTacticalShipMotor candidate = ships[i];
            if (candidate == null)
            {
                continue;
            }

            CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
            CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
            if (combatant == null
                || combatant.team != friendlyTeam
                || !combatant.IsAlive
                || health == null
                || health.currentHealth >= health.maxHealth - 0.01f)
            {
                continue;
            }

            if (reservedTargets != null && reservedTargets.Contains(candidate))
            {
                continue;
            }

            Vector3 toCandidate = candidate.transform.position - origin;
            if (!IsInsideSideArc(sideSign, candidate.transform.position))
            {
                continue;
            }

            float distanceSqr = toCandidate.sqrMagnitude;
            if (distanceSqr > rangeMeters * rangeMeters || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            best = candidate;
            bestDistanceSqr = distanceSqr;
        }

        return best;
    }

    private bool IsInsideSideArc(int sideSign, Vector3 targetPosition)
    {
        if (owner == null)
        {
            return true;
        }

        Vector3 toTarget = targetPosition - owner.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector3 sideDirection = GetVisualSideDirection(sideSign);
        sideDirection.y = 0f;
        if (sideDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float halfArc = Mathf.Clamp(sideArcDegrees, 1f, 360f) * 0.5f;
        return Vector3.Angle(sideDirection.normalized, toTarget.normalized) <= halfArc;
    }

    private Vector3 GetVisualSideDirection(int sideSign)
    {
        if (owner == null)
        {
            return sideSign < 0 ? Vector3.left : Vector3.right;
        }

        // Imported Korshun equipment uses left/right mount names opposite to
        // the tactical motor's local +X, so helper beams must use visual side arcs.
        return owner.transform.right * (sideSign < 0 ? 1f : -1f);
    }

    private Vector3 GetBeamOriginCenter()
    {
        return owner.transform.position + owner.transform.up * Mathf.Max(5f, owner.hullSizeMeters.y * 0.52f);
    }
}
