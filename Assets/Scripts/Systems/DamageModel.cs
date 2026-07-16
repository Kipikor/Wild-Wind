using System;
using UnityEngine;

public enum DamageShellType
{
    [InspectorName("Бронебойный")]
    ArmorPiercing,
    [InspectorName("Фугасный")]
    HighExplosive,
    [InspectorName("Удар/таран")]
    Impact
}

public static class DamageResistanceUtility
{
    public static CoreTacticalResistanceSet DefaultShipResistances => new CoreTacticalResistanceSet(48f, 18f, 18f, 28f);
    public static CoreTacticalResistanceSet DefaultOreResistances => new CoreTacticalResistanceSet(95f, 35f, 15f, 65f);
    public static CoreTacticalResistanceSet DefaultAutomatonResistances => new CoreTacticalResistanceSet(34f, 22f, 16f, 24f);

    public static CoreTacticalResistanceSet FromLegacyArmorHint(float armorMm)
    {
        float kinetic = Mathf.Clamp(Mathf.Max(0f, armorMm) * 1.65f, 0f, 95f);
        return new CoreTacticalResistanceSet(kinetic, 18f, 18f, 28f);
    }
}

public enum DamageHitOutcome
{
    [InspectorName("Пробитие")]
    Penetration,
    [InspectorName("Непробитие")]
    NoPenetration,
    [InspectorName("Рикошет")]
    Ricochet,
    [InspectorName("Фугасный взрыв")]
    ExplosiveSplash,
    [InspectorName("Ударный урон")]
    ImpactDamage,
    [InspectorName("Промах")]
    Miss
}

public enum ShipGunFireMode
{
    [InspectorName("Manual")]
    Manual,
    [InspectorName("Automatic")]
    Automatic
}

[Serializable]
public class DamageShellPreset
{
    [InspectorName("Название")]
    public string displayNameRu = "Бронебойный";
    [InspectorName("Тип")]
    public DamageShellType shellType = DamageShellType.ArmorPiercing;
    [InspectorName("Калибр, мм")]
    public float caliberMm = 76f;
    [InspectorName("Старый общий урон")]
    [Tooltip("Оставлено для совместимости. Новая модель использует три отдельных урона ниже.")]
    public float damagePoints = 100f;
    [InspectorName("Damage type")]
    public CoreTacticalDamageType damageType = CoreTacticalDamageType.Kinetic;
    [InspectorName("Resistance ignore, %")]
    [Range(0f, 100f)] public float resistanceIgnorePercent = 0f;
    [InspectorName("Урон корпусу при пробитии")]
    [Tooltip("Сколько прочности корпуса снимает снаряд, если бронелист пробит.")]
    public float hullDamageOnPenetration = 100f;
    [InspectorName("Damage roll spread")]
    [Range(0f, 0.5f)] public float damageRollSpread = 0.25f;
    [InspectorName("Пробитие, мм")]
    public float penetrationMm = 70f;
    [InspectorName("AP arming armor, mm")]
    public float armingArmorMm = 0f;
    [InspectorName("Множитель пробития на максимальной дальности")]
    [Tooltip("Бронепробитие бронебойного снаряда на пределе дальности. Между стволом и пределом интерполируется по дистанции/скорости.")]
    [Range(0.05f, 1f)] public float penetrationAtMaxRangeMultiplier = 0.55f;
    [InspectorName("Сохранение скорости на максимальной дальности")]
    [Tooltip("Упрощенное сопротивление воздуха: какую долю начальной скорости снаряд сохраняет на пределе дальности.")]
    [Range(0.05f, 1f)] public float velocityRetentionAtMaxRange = 0.65f;
    [InspectorName("Радиус фугаса, м")]
    public float explosiveRadiusMeters = 4f;
    [InspectorName("Нормализация, град")]
    public float normalizationDegrees = 4f;
    [InspectorName("Разброс пробития")]
    [Range(0f, 0.5f)] public float penetrationRollSpread = 0.25f;
    [InspectorName("Цвет снаряда")]
    public Color projectileColor = Color.red;
}

public struct BallisticAimSolution
{
    public bool valid;
    public bool inRange;
    public bool lockedRange;
    public bool hasDirectHitTarget;
    public Vector3 origin;
    public Vector3 shipCenter;
    public Vector3 targetPoint;
    public Vector3 targetVelocity;
    public Vector3 predictedTargetPoint;
    public Vector3 launchVelocity;
    public Vector3 launchDirection;
    public float muzzleVelocityMS;
    public float maxRangeMeters;
    public float distanceFromShipCenter;
    public float directDistanceFromMuzzle;
    public float travelTimeSeconds;
    public float velocityRetentionAtMaxRange;
    public float dragPerMeter;
    public string status;
}

public static class BallisticFireControl
{
    private const float MinSolveTime = 0.03f;
    private const float DragEpsilon = 0.0000001f;
    private const int SolverIntegrationMaxSteps = 96;
    private const int PublicIntegrationMaxSteps = 512;

    public static BallisticAimSolution BuildSolution(
        Vector3 shipCenter,
        Vector3 origin,
        Vector3 targetPoint,
        Vector3 targetVelocity,
        float muzzleVelocityMS,
        float maxRangeMeters,
        float gravityScale,
        bool lockedRange,
        bool hasDirectHitTarget,
        float velocityRetentionAtMaxRange = 1f)
    {
        float safeRange = Mathf.Max(1f, maxRangeMeters);
        Vector3 fromCenter = targetPoint - shipCenter;
        float centerDistance = fromCenter.magnitude;
        bool inRange = centerDistance <= safeRange + 0.001f;
        if (centerDistance > safeRange && centerDistance > 0.001f)
        {
            targetPoint = shipCenter + fromCenter / centerDistance * safeRange;
            centerDistance = safeRange;
            inRange = false;
        }

        Vector3 gravity = Physics.gravity * Mathf.Max(0f, gravityScale);
        float speed = Mathf.Max(1f, muzzleVelocityMS);
        float safeRetention = Mathf.Clamp(velocityRetentionAtMaxRange, 0.05f, 1f);
        float dragPerMeter = CalculateDragPerMeter(safeRange, safeRetention);
        bool solved = TrySolveLaunchVelocity(
            origin,
            targetPoint,
            targetVelocity,
            speed,
            gravity,
            safeRange,
            out Vector3 launchVelocity,
            out float travelTime,
            out Vector3 predictedTargetPoint,
            safeRetention);

        if (!solved)
        {
            Vector3 fallback = targetPoint - origin;
            if (fallback.sqrMagnitude < 0.001f)
            {
                fallback = Vector3.forward;
            }

            launchVelocity = fallback.normalized * speed;
            travelTime = fallback.magnitude / speed;
            predictedTargetPoint = targetPoint;
        }

        Vector3 direction = launchVelocity.sqrMagnitude > 0.001f ? launchVelocity.normalized : Vector3.forward;
        return new BallisticAimSolution
        {
            valid = solved,
            inRange = inRange,
            lockedRange = lockedRange,
            hasDirectHitTarget = hasDirectHitTarget,
            origin = origin,
            shipCenter = shipCenter,
            targetPoint = targetPoint,
            targetVelocity = targetVelocity,
            predictedTargetPoint = predictedTargetPoint,
            launchVelocity = launchVelocity,
            launchDirection = direction,
            muzzleVelocityMS = speed,
            maxRangeMeters = safeRange,
            distanceFromShipCenter = centerDistance,
            directDistanceFromMuzzle = Vector3.Distance(origin, targetPoint),
            travelTimeSeconds = Mathf.Max(0f, travelTime),
            velocityRetentionAtMaxRange = safeRetention,
            dragPerMeter = dragPerMeter,
            status = solved
                ? (dragPerMeter > DragEpsilon ? "Ballistic drag solution" : "Ballistic solution")
                : "Direct fallback"
        };
    }

    public static bool TrySolveLaunchVelocity(
        Vector3 origin,
        Vector3 targetPoint,
        Vector3 targetVelocity,
        float muzzleVelocityMS,
        Vector3 gravity,
        float maxRangeMeters,
        out Vector3 launchVelocity,
        out float travelTimeSeconds,
        out Vector3 predictedTargetPoint,
        float velocityRetentionAtMaxRange = 1f)
    {
        launchVelocity = Vector3.zero;
        travelTimeSeconds = 0f;
        predictedTargetPoint = targetPoint;

        float speed = Mathf.Max(0.001f, muzzleVelocityMS);
        Vector3 delta = targetPoint - origin;
        float directDistance = delta.magnitude;
        if (directDistance <= 0.001f)
        {
            launchVelocity = Vector3.forward * speed;
            travelTimeSeconds = MinSolveTime;
            predictedTargetPoint = targetPoint;
            return true;
        }

        float dragPerMeter = CalculateDragPerMeter(maxRangeMeters, velocityRetentionAtMaxRange);
        if (dragPerMeter > DragEpsilon)
        {
            return TrySolveLaunchVelocityWithDrag(
                origin,
                targetPoint,
                targetVelocity,
                speed,
                gravity,
                Mathf.Max(1f, maxRangeMeters),
                dragPerMeter,
                directDistance,
                out launchVelocity,
                out travelTimeSeconds,
                out predictedTargetPoint);
        }

        return TrySolveLaunchVelocityNoDrag(
            origin,
            targetPoint,
            targetVelocity,
            speed,
            gravity,
            maxRangeMeters,
            directDistance,
            delta,
            out launchVelocity,
            out travelTimeSeconds,
            out predictedTargetPoint);
    }

    public static Vector3 EvaluatePosition(
        Vector3 origin,
        Vector3 launchVelocity,
        Vector3 gravity,
        float timeSeconds,
        float dragPerMeter = 0f)
    {
        return EvaluatePositionInternal(origin, launchVelocity, gravity, timeSeconds, dragPerMeter, PublicIntegrationMaxSteps);
    }

    public static Vector3 EvaluateVelocity(
        Vector3 launchVelocity,
        Vector3 gravity,
        float timeSeconds,
        float dragPerMeter = 0f)
    {
        float t = Mathf.Max(0f, timeSeconds);
        if (t <= 0f)
        {
            return launchVelocity;
        }

        if (dragPerMeter <= DragEpsilon)
        {
            return launchVelocity + gravity * t;
        }

        int steps = CalculateIntegrationSteps(t, PublicIntegrationMaxSteps);
        float dt = t / steps;
        Vector3 velocity = launchVelocity;
        for (int i = 0; i < steps; i++)
        {
            velocity = IntegrateVelocity(velocity, gravity, dt, dragPerMeter);
        }

        return velocity;
    }

    public static Vector3 IntegrateVelocity(
        Vector3 currentVelocity,
        Vector3 gravity,
        float deltaSeconds,
        float dragPerMeter = 0f)
    {
        float dt = Mathf.Max(0f, deltaSeconds);
        if (dt <= 0f)
        {
            return currentVelocity;
        }

        Vector3 velocity = currentVelocity + gravity * dt;
        if (dragPerMeter <= DragEpsilon)
        {
            return velocity;
        }

        float speed = velocity.magnitude;
        if (speed <= 0.0001f)
        {
            return velocity;
        }

        return velocity / (1f + Mathf.Max(0f, dragPerMeter) * speed * dt);
    }

    public static Vector2 CalculateSpreadRadii(float horizontalAtMaxRange, float verticalAtMaxRange, float distanceMeters, float maxRangeMeters)
    {
        float ratio = maxRangeMeters > 0.001f ? Mathf.Clamp01(Mathf.Max(0f, distanceMeters) / maxRangeMeters) : 0f;
        return new Vector2(Mathf.Max(0f, horizontalAtMaxRange) * ratio, Mathf.Max(0f, verticalAtMaxRange) * ratio);
    }

    public static float CalculateRangeVelocityRetention(float distanceMeters, float maxRangeMeters, float velocityRetentionAtMaxRange)
    {
        float ratio = maxRangeMeters > 0.001f ? Mathf.Clamp01(Mathf.Max(0f, distanceMeters) / maxRangeMeters) : 0f;
        return Mathf.Pow(Mathf.Clamp(velocityRetentionAtMaxRange, 0.05f, 1f), ratio);
    }

    public static float CalculateDragPerMeter(float maxRangeMeters, float velocityRetentionAtMaxRange)
    {
        float safeRetention = Mathf.Clamp(velocityRetentionAtMaxRange, 0.05f, 1f);
        if (safeRetention >= 0.9999f)
        {
            return 0f;
        }

        return -Mathf.Log(safeRetention) / Mathf.Max(1f, maxRangeMeters);
    }

    public static float CalculatePenetrationMultiplier(float distanceMeters, float maxRangeMeters, float penetrationAtMaxRangeMultiplier, float currentSpeedMS, float launchSpeedMS)
    {
        float rangeRatio = maxRangeMeters > 0.001f ? Mathf.Clamp01(Mathf.Max(0f, distanceMeters) / maxRangeMeters) : 0f;
        float rangeMultiplier = Mathf.Lerp(1f, Mathf.Clamp(penetrationAtMaxRangeMultiplier, 0.05f, 1f), rangeRatio);
        float speedMultiplier = launchSpeedMS > 0.001f ? Mathf.Clamp01(Mathf.Max(0f, currentSpeedMS) / launchSpeedMS) : 1f;
        return Mathf.Clamp(rangeMultiplier * Mathf.Lerp(1f, speedMultiplier, 0.65f), 0.02f, 1f);
    }

    private static bool TrySolveLaunchVelocityNoDrag(
        Vector3 origin,
        Vector3 targetPoint,
        Vector3 targetVelocity,
        float muzzleVelocityMS,
        Vector3 gravity,
        float maxRangeMeters,
        float directDistance,
        Vector3 delta,
        out Vector3 launchVelocity,
        out float travelTimeSeconds,
        out Vector3 predictedTargetPoint)
    {
        launchVelocity = Vector3.zero;
        travelTimeSeconds = 0f;
        predictedTargetPoint = targetPoint;

        float speed = Mathf.Max(0.001f, muzzleVelocityMS);
        if (gravity.sqrMagnitude <= 0.000001f &&
            targetVelocity.sqrMagnitude <= 0.000001f)
        {
            launchVelocity = delta.normalized * speed;
            travelTimeSeconds = directDistance / speed;
            return true;
        }

        float range = Mathf.Max(directDistance, Mathf.Max(1f, maxRangeMeters));
        float maxTime = Mathf.Clamp(range / speed * 4f + 2f, 0.2f, 90f);
        float previousTime = MinSolveTime;
        float previousValue = RequiredSpeedMinusMuzzle(origin, targetPoint, targetVelocity, gravity, previousTime, speed);
        float bestTime = previousTime;
        float bestAbs = Mathf.Abs(previousValue);

        const int scanSteps = 128;
        for (int i = 1; i <= scanSteps; i++)
        {
            float t = Mathf.Lerp(MinSolveTime, maxTime, i / (float)scanSteps);
            float value = RequiredSpeedMinusMuzzle(origin, targetPoint, targetVelocity, gravity, t, speed);
            float abs = Mathf.Abs(value);
            if (abs < bestAbs)
            {
                bestAbs = abs;
                bestTime = t;
            }

            if ((previousValue >= 0f && value <= 0f) || Mathf.Abs(value) <= 0.01f)
            {
                float low = previousTime;
                float high = t;
                for (int j = 0; j < 18; j++)
                {
                    float mid = (low + high) * 0.5f;
                    float midValue = RequiredSpeedMinusMuzzle(origin, targetPoint, targetVelocity, gravity, mid, speed);
                    if (midValue > 0f)
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                travelTimeSeconds = (low + high) * 0.5f;
                predictedTargetPoint = targetPoint + targetVelocity * travelTimeSeconds;
                Vector3 requiredVelocity = RequiredLaunchVelocity(origin, predictedTargetPoint, gravity, travelTimeSeconds);
                launchVelocity = requiredVelocity.sqrMagnitude > 0.001f ? requiredVelocity.normalized * speed : delta.normalized * speed;
                return true;
            }

            previousTime = t;
            previousValue = value;
        }

        if (bestAbs <= Mathf.Max(0.5f, speed * 0.025f))
        {
            travelTimeSeconds = bestTime;
            predictedTargetPoint = targetPoint + targetVelocity * travelTimeSeconds;
            Vector3 requiredVelocity = RequiredLaunchVelocity(origin, predictedTargetPoint, gravity, travelTimeSeconds);
            launchVelocity = requiredVelocity.sqrMagnitude > 0.001f ? requiredVelocity.normalized * speed : delta.normalized * speed;
            return true;
        }

        return false;
    }

    private static bool TrySolveLaunchVelocityWithDrag(
        Vector3 origin,
        Vector3 targetPoint,
        Vector3 targetVelocity,
        float muzzleVelocityMS,
        Vector3 gravity,
        float maxRangeMeters,
        float dragPerMeter,
        float directDistance,
        out Vector3 launchVelocity,
        out float travelTimeSeconds,
        out Vector3 predictedTargetPoint)
    {
        launchVelocity = Vector3.zero;
        travelTimeSeconds = 0f;
        predictedTargetPoint = targetPoint;

        float speed = Mathf.Max(0.001f, muzzleVelocityMS);
        float estimatedDirectTime = EstimateDragFlightTime(directDistance, speed, dragPerMeter);
        float rangeTime = EstimateDragFlightTime(Mathf.Max(directDistance, maxRangeMeters), speed, dragPerMeter);
        float targetMotionAllowance = targetVelocity.magnitude / speed;
        float maxTime = Mathf.Clamp(Mathf.Max(estimatedDirectTime, rangeTime) * (3.2f + targetMotionAllowance) + 2f, 0.2f, 90f);

        bool found = false;
        float bestMiss = float.PositiveInfinity;
        float bestTime = estimatedDirectTime > MinSolveTime ? estimatedDirectTime : MinSolveTime;
        Vector3 bestVelocity = Vector3.zero;
        Vector3 bestPredicted = targetPoint;

        float low = MinSolveTime;
        float high = maxTime;
        const int scanSteps = 48;
        for (int pass = 0; pass < 3; pass++)
        {
            float span = Mathf.Max(0.001f, high - low);
            float passBestTime = bestTime;
            for (int i = 0; i <= scanSteps; i++)
            {
                float t = low + span * (i / (float)scanSteps);
                Vector3 predicted = targetPoint + targetVelocity * t;
                if (!TryRefineLaunchVelocityForFixedTime(
                    origin,
                    predicted,
                    speed,
                    gravity,
                    dragPerMeter,
                    t,
                    out Vector3 candidateVelocity,
                    out Vector3 impact,
                    out float miss))
                {
                    continue;
                }

                if (miss < bestMiss)
                {
                    found = true;
                    bestMiss = miss;
                    passBestTime = t;
                    bestTime = t;
                    bestVelocity = candidateVelocity;
                    bestPredicted = predicted;
                }

                float acceptableMiss = Mathf.Max(1.5f, directDistance * 0.004f);
                if (miss <= acceptableMiss && t <= bestTime + 0.0001f)
                {
                    found = true;
                    bestMiss = miss;
                    bestTime = t;
                    bestVelocity = candidateVelocity;
                    bestPredicted = predicted;
                    launchVelocity = bestVelocity;
                    travelTimeSeconds = bestTime;
                    predictedTargetPoint = bestPredicted;
                    return true;
                }
            }

            float pad = span / scanSteps * 2.5f;
            low = Mathf.Max(MinSolveTime, passBestTime - pad);
            high = Mathf.Min(maxTime, passBestTime + pad);
        }

        float fallbackMiss = Mathf.Max(3f, directDistance * 0.008f);
        if (found && bestMiss <= fallbackMiss)
        {
            launchVelocity = bestVelocity;
            travelTimeSeconds = bestTime;
            predictedTargetPoint = bestPredicted;
            return true;
        }

        return false;
    }

    private static bool TryRefineLaunchVelocityForFixedTime(
        Vector3 origin,
        Vector3 targetPoint,
        float muzzleVelocityMS,
        Vector3 gravity,
        float dragPerMeter,
        float timeSeconds,
        out Vector3 launchVelocity,
        out Vector3 impact,
        out float missMeters)
    {
        launchVelocity = Vector3.zero;
        impact = origin;
        missMeters = float.PositiveInfinity;

        float t = Mathf.Max(MinSolveTime, timeSeconds);
        float speed = Mathf.Max(0.001f, muzzleVelocityMS);
        Vector3 direct = targetPoint - origin;
        Vector3 required = RequiredLaunchVelocity(origin, targetPoint, gravity, t);
        Vector3 direction = required.sqrMagnitude > 0.0001f
            ? required.normalized
            : (direct.sqrMagnitude > 0.0001f ? direct.normalized : Vector3.forward);
        if (!IsFinite(direction))
        {
            return false;
        }

        Vector3 bestDirection = direction;
        Vector3 bestImpact = EvaluatePositionInternal(origin, direction * speed, gravity, t, dragPerMeter, SolverIntegrationMaxSteps);
        float bestMiss = Vector3.Distance(bestImpact, targetPoint);
        const int iterations = 7;
        const float perturbRadians = 0.004f;
        for (int i = 0; i < iterations; i++)
        {
            Vector3 currentVelocity = direction * speed;
            Vector3 currentImpact = EvaluatePositionInternal(origin, currentVelocity, gravity, t, dragPerMeter, SolverIntegrationMaxSteps);
            if (!IsFinite(currentImpact))
            {
                break;
            }

            float currentMiss = Vector3.Distance(currentImpact, targetPoint);
            if (currentMiss < bestMiss)
            {
                bestMiss = currentMiss;
                bestImpact = currentImpact;
                bestDirection = direction;
            }

            if (currentMiss <= 0.05f)
            {
                break;
            }

            Vector3 error = targetPoint - currentImpact;
            BuildDirectionBasis(direction, out Vector3 axisA, out Vector3 axisB);
            Vector3 impactA = EvaluatePositionInternal(
                origin,
                (direction + axisA * perturbRadians).normalized * speed,
                gravity,
                t,
                dragPerMeter,
                SolverIntegrationMaxSteps);
            Vector3 impactB = EvaluatePositionInternal(
                origin,
                (direction + axisB * perturbRadians).normalized * speed,
                gravity,
                t,
                dragPerMeter,
                SolverIntegrationMaxSteps);
            Vector3 columnA = (impactA - currentImpact) / perturbRadians;
            Vector3 columnB = (impactB - currentImpact) / perturbRadians;
            float aa = Vector3.Dot(columnA, columnA) + 0.0001f;
            float ab = Vector3.Dot(columnA, columnB);
            float bb = Vector3.Dot(columnB, columnB) + 0.0001f;
            float ea = Vector3.Dot(columnA, error);
            float eb = Vector3.Dot(columnB, error);
            float det = aa * bb - ab * ab;
            if (Mathf.Abs(det) <= 0.000001f)
            {
                break;
            }

            float deltaA = (ea * bb - eb * ab) / det;
            float deltaB = (aa * eb - ab * ea) / det;
            if (!IsFinite(deltaA) || !IsFinite(deltaB))
            {
                break;
            }

            deltaA = Mathf.Clamp(deltaA, -0.25f, 0.25f);
            deltaB = Mathf.Clamp(deltaB, -0.25f, 0.25f);
            Vector3 nextDirection = (direction + axisA * deltaA + axisB * deltaB).normalized;
            if (!IsFinite(nextDirection) || nextDirection.sqrMagnitude <= 0.0001f)
            {
                break;
            }

            Vector3 nextImpact = EvaluatePositionInternal(origin, nextDirection * speed, gravity, t, dragPerMeter, SolverIntegrationMaxSteps);
            float nextMiss = Vector3.Distance(nextImpact, targetPoint);
            if (nextMiss > currentMiss && (Mathf.Abs(deltaA) > 0.0001f || Mathf.Abs(deltaB) > 0.0001f))
            {
                nextDirection = (direction + axisA * (deltaA * 0.5f) + axisB * (deltaB * 0.5f)).normalized;
                nextImpact = EvaluatePositionInternal(origin, nextDirection * speed, gravity, t, dragPerMeter, SolverIntegrationMaxSteps);
                nextMiss = Vector3.Distance(nextImpact, targetPoint);
            }

            direction = nextDirection;
            if (nextMiss < bestMiss)
            {
                bestMiss = nextMiss;
                bestImpact = nextImpact;
                bestDirection = nextDirection;
            }
        }

        launchVelocity = bestDirection * speed;
        impact = bestImpact;
        missMeters = bestMiss;
        return IsFinite(launchVelocity) && IsFinite(impact);
    }

    private static Vector3 EvaluatePositionInternal(
        Vector3 origin,
        Vector3 launchVelocity,
        Vector3 gravity,
        float timeSeconds,
        float dragPerMeter,
        int maxSteps)
    {
        float t = Mathf.Max(0f, timeSeconds);
        if (t <= 0f)
        {
            return origin;
        }

        if (dragPerMeter <= DragEpsilon)
        {
            return origin + launchVelocity * t + 0.5f * gravity * t * t;
        }

        int steps = CalculateIntegrationSteps(t, maxSteps);
        float dt = t / steps;
        Vector3 position = origin;
        Vector3 velocity = launchVelocity;
        for (int i = 0; i < steps; i++)
        {
            velocity = IntegrateVelocity(velocity, gravity, dt, dragPerMeter);
            position += velocity * dt;
        }

        return position;
    }

    private static float RequiredSpeedMinusMuzzle(
        Vector3 origin,
        Vector3 targetPoint,
        Vector3 targetVelocity,
        Vector3 gravity,
        float timeSeconds,
        float muzzleVelocityMS)
    {
        float t = Mathf.Max(MinSolveTime, timeSeconds);
        Vector3 predicted = targetPoint + targetVelocity * t;
        Vector3 required = RequiredLaunchVelocity(origin, predicted, gravity, t);
        return required.magnitude - muzzleVelocityMS;
    }

    private static Vector3 RequiredLaunchVelocity(Vector3 origin, Vector3 predictedTargetPoint, Vector3 gravity, float timeSeconds)
    {
        float t = Mathf.Max(MinSolveTime, timeSeconds);
        return (predictedTargetPoint - origin - 0.5f * gravity * t * t) / t;
    }

    private static float EstimateDragFlightTime(float distanceMeters, float muzzleVelocityMS, float dragPerMeter)
    {
        float distance = Mathf.Max(0f, distanceMeters);
        float speed = Mathf.Max(0.001f, muzzleVelocityMS);
        float drag = Mathf.Max(0f, dragPerMeter);
        if (drag <= DragEpsilon)
        {
            return distance / speed;
        }

        float exponent = Mathf.Clamp(drag * distance, 0f, 8f);
        return (Mathf.Exp(exponent) - 1f) / (drag * speed);
    }

    private static int CalculateIntegrationSteps(float timeSeconds, int maxSteps)
    {
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(0f, timeSeconds) / 0.035f), 1, Mathf.Max(1, maxSteps));
    }

    private static void BuildDirectionBasis(Vector3 direction, out Vector3 axisA, out Vector3 axisB)
    {
        Vector3 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.95f ? Vector3.up : Vector3.forward;
        axisA = Vector3.Cross(reference, forward);
        if (axisA.sqrMagnitude <= 0.0001f)
        {
            axisA = Vector3.right;
        }

        axisA.Normalize();
        axisB = Vector3.Cross(forward, axisA);
        if (axisB.sqrMagnitude <= 0.0001f)
        {
            axisB = Vector3.up;
        }

        axisB.Normalize();
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

public struct DamageHitContext
{
    public DamageShellType shellType;
    public CoreTacticalDamageType damageType;
    public string shellName;
    public string sourceName;
    public float caliberMm;
    public float damagePoints;
    public float hullDamageOnPenetration;
    public float resistanceIgnorePercent;
    public float armingArmorMm;
    public float penetrationMm;
    public float explosiveRadiusMeters;
    public float normalizationDegrees;
    public float impactEnergyKJ;
    public float impactDamagePerKJ;
    public float impactSpeedMS;
    public float impactSourceMassKg;
    public float impactTargetMassKg;
    public float impactSourceDamageMultiplier;
    public float damageRollSpread;
    public float internalTravelDistance;
    public bool deferResultLogging;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public Vector3 incomingDirection;
    public Vector3 velocity;
}

public struct DamageHitResult
{
    public DamageHitOutcome outcome;
    public string message;
    public string zoneId;
    public CoreTacticalDamageType damageType;
    public float resistancePercent;
    public float resistanceIgnorePercent;
    public float effectiveResistancePercent;
    public float armorMm;
    public float effectiveArmorMm;
    public float impactAngleDeg;
    public float penetrationMm;
    public float structureDamage;
    public float remainingStructureHp;
}

public struct ArmorSurface
{
    public string zoneId;
    public string displayNameRu;
    public CoreTacticalResistanceSet resistances;
    public float armorMm;
    public float ricochetAngleDeg;
    public float overmatchCaliberMultiplier;
    public float structureDamageMultiplier;
    public float highExplosiveSurfaceDamageMultiplier;
    public float ramDamageMultiplier;

    public float GetResistancePercent(CoreTacticalDamageType damageType)
    {
        return resistances.Get(damageType);
    }

    public static ArmorSurface FromZone(ArmorZone zone)
    {
        return new ArmorSurface
        {
            zoneId = zone != null ? zone.zoneId : "",
            displayNameRu = zone != null ? zone.displayNameRu : "",
            resistances = zone != null ? zone.Resistances : DamageResistanceUtility.DefaultShipResistances,
            armorMm = zone != null ? zone.armorMm : 0f,
            ricochetAngleDeg = zone != null ? zone.ricochetAngleDeg : 70f,
            overmatchCaliberMultiplier = zone != null ? zone.overmatchCaliberMultiplier : 3f,
            structureDamageMultiplier = zone != null ? zone.structureDamageMultiplier : 1f,
            highExplosiveSurfaceDamageMultiplier = zone != null ? zone.highExplosiveSurfaceDamageMultiplier : 0.35f,
            ramDamageMultiplier = zone != null ? zone.ramDamageMultiplier : 1f
        };
    }
}
