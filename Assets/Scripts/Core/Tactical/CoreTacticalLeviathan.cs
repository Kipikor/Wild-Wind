using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoreTacticalLeviathanController : MonoBehaviour
{
    public const float BiteAllowedTargetLengthRatio = 0.35f;
    public const float LethalBiteTargetLengthRatio = 0.20f;
    public const float TooSmallLeviathanToTargetRatio = 0.30f;
    public const float EdibleBodyLengthRatio = 1f / 3f;
    public const float DefaultAttackCommitSeconds = 20f;

    public CoreTacticalShipMotor ship;
    public CoreTacticalPrototypeHealth health;
    public float lengthMeters = 60f;
    public float aggressionRadiusMeters = 1350f;
    public float closeAggressionRadiusMeters = 420f;
    public float aggressionPerSecondNear = 0.095f;
    public float aggressionPerSecondClose = 0.064f;
    public float aggressionDecayPerSecond = 1f / 30f;
    public float artilleryReportAggression = 0.02f;
    public float directDamageAggression = 0.315f;
    public float nearExplosionAggression = 0.06f;
    public float stalkAggressionThreshold01 = 0.12f;
    public float attackCommitSeconds = DefaultAttackCommitSeconds;
    public float attackCooldownSeconds = 3.6f;
    public float threatRetreatSeconds = 5.5f;
    public float ramMinRelativeSpeedMS = 26f;
    public float ramDamageScale = 7.5f;
    public float biteDamageMultiplier = 5.5f;
    public float lethalBiteDamageMultiplier = 80f;
    public float passiveRegenerationPercentPerSecond = 0.006f;
    public float feedingHealthPercentPerTon = 0.035f;
    public float lowHealthFeedingThreshold01 = 0.58f;
    public float feedingSearchRadiusMeters = 1050f;
    public float feedingContactRadiusMeters = 42f;
    public float idleFeedingIntervalSeconds = 13f;
    public float idleWanderIntervalSeconds = 7.5f;
    public float idleWanderRadiusMeters = 650f;
    public float cloudAvoidSeconds = 4.5f;
    public float cloudAvoidDistanceMeters = 900f;
    public float fleeDistanceMeters = 1250f;

    private CoreTacticalShipMotor currentTarget;
    private float aggression01;
    private float attackRemainingSeconds;
    private float retreatUntilTime;
    private float cloudAvoidUntilTime;
    private Vector3 cloudAvoidDirection = Vector3.forward;
    private Vector3 wanderAnchorPosition;
    private float nextDecisionTime;
    private float nextIdleFeedingTime;
    private float nextIdleWanderTime;
    private float lastObservedHealth;
    private bool hasWanderAnchor;

    public float Aggression01 => Mathf.Clamp01(aggression01);
    public bool IsAttackCommitted => attackRemainingSeconds > 0f && IsLiveTarget(currentTarget);
    public CoreTacticalShipMotor CurrentTarget => currentTarget;
    public bool IsAutomaticWeaponTarget => Aggression01 > 0.001f || IsAttackCommitted;

    private void Awake()
    {
        ResolveLinks();
    }

    private void Start()
    {
        ResolveLinks();
        DisableShipDeathBurst();
        SyncLengthFromShip();
        SetWanderAnchor(transform.position);
        lastObservedHealth = health != null ? health.currentHealth : 0f;
        nextIdleFeedingTime = Time.time + Random.Range(0f, Mathf.Max(0.1f, idleFeedingIntervalSeconds));
        nextIdleWanderTime = Time.time + Random.Range(0f, Mathf.Max(0.1f, idleWanderIntervalSeconds));
    }

    private void Update()
    {
        Tick(Mathf.Max(0f, Time.deltaTime));
    }

    public void Configure(float newLengthMeters)
    {
        ResolveLinks();
        DisableShipDeathBurst();
        lengthMeters = Mathf.Max(10f, newLengthMeters);
        SyncLengthFromShip();
        SetWanderAnchor(transform.position);
    }

    public void TickForTests(float deltaSeconds)
    {
        Tick(Mathf.Max(0f, deltaSeconds));
    }

    public void AddAggressionForTests(float amount, CoreTacticalShipMotor target = null)
    {
        AddAggression(amount, target);
    }

    public void NotifyDirectDamageForTests(CoreTacticalShipMotor suspectedAttacker)
    {
        AddDirectDamageAggression(suspectedAttacker);
    }

    public void ForceIdleWanderForTests()
    {
        nextIdleWanderTime = Time.time;
    }

    public float ApplyAttackDamageForTests(CoreTacticalShipMotor target, float relativeSpeedMS)
    {
        return ApplyAttackDamage(target, relativeSpeedMS, true);
    }

    public float ApplyContactAttackDamageForTests(CoreTacticalShipMotor target, float relativeSpeedMS)
    {
        return ApplyAttackDamage(target, relativeSpeedMS, false);
    }

    public bool TryEatOreFragmentForTests(CoreTacticalOreFragment fragment)
    {
        return TryEatFragment(fragment);
    }

    public bool TryEatOreBoulderForTests(CoreTacticalOreBoulder boulder)
    {
        return TryEatBoulder(boulder);
    }

    public bool TryEatAutomatonWreckForTests(CoreTacticalAutomatonWreck wreck)
    {
        return TryEatWreck(wreck);
    }

    public bool ShouldRetreatFromTargetForTests(CoreTacticalShipMotor target)
    {
        return IsTargetTooLargeForAggression(target);
    }

    public bool ShouldAvoidCloudForTests(CoreTacticalGasCloud cloud)
    {
        return cloud != null && ship != null && cloud.OverlapsCollectionSphere(ship.transform.position, ResolveContactRadius());
    }

    public float CalculateProximityAggressionRateForTests(float distanceMeters)
    {
        return CalculateProximityAggressionRate(Mathf.Max(0f, distanceMeters));
    }

    public float CalculateNetProximityAggressionRateForTests(float distanceMeters)
    {
        return Mathf.Max(0f, CalculateProximityAggressionRate(Mathf.Max(0f, distanceMeters)) - Mathf.Max(0f, aggressionDecayPerSecond));
    }

    public static bool CanBiteTarget(float leviathanLengthMeters, float targetLengthMeters)
    {
        float leviathanLength = Mathf.Max(0.001f, leviathanLengthMeters);
        float targetLength = Mathf.Max(0.001f, targetLengthMeters);
        return targetLength / leviathanLength <= BiteAllowedTargetLengthRatio;
    }

    public static bool IsLethalBiteTarget(float leviathanLengthMeters, float targetLengthMeters)
    {
        float leviathanLength = Mathf.Max(0.001f, leviathanLengthMeters);
        float targetLength = Mathf.Max(0.001f, targetLengthMeters);
        return targetLength / leviathanLength <= LethalBiteTargetLengthRatio;
    }

    public static bool IsLeviathanTooSmallForTarget(float leviathanLengthMeters, float targetLengthMeters)
    {
        float leviathanLength = Mathf.Max(0.001f, leviathanLengthMeters);
        float targetLength = Mathf.Max(0.001f, targetLengthMeters);
        return leviathanLength / targetLength < TooSmallLeviathanToTargetRatio;
    }

    public static bool CanEatBodyByLength(float leviathanLengthMeters, float bodyLengthMeters)
    {
        float leviathanLength = Mathf.Max(0.001f, leviathanLengthMeters);
        float bodyLength = Mathf.Max(0.001f, bodyLengthMeters);
        return bodyLength / leviathanLength <= EdibleBodyLengthRatio;
    }

    public static float CalculateRamDamage(float leviathanMassKg, float targetMassKg, float relativeSpeedMS, float damageScale)
    {
        float sourceMass = Mathf.Max(1f, leviathanMassKg);
        float targetMass = Mathf.Max(1f, targetMassKg);
        float reducedMassKg = sourceMass * targetMass / Mathf.Max(1f, sourceMass + targetMass);
        float speed = Mathf.Max(0f, relativeSpeedMS);
        float energyKJ = 0.5f * reducedMassKg * speed * speed / 1000f;
        return Mathf.Sqrt(Mathf.Max(0f, energyKJ)) * Mathf.Max(0f, damageScale);
    }

    public static float CalculateBiteDamage(float leviathanMassKg, float targetMassKg, float relativeSpeedMS, float targetMaxHealth, bool lethalBite, float ramScale, float biteMultiplier, float lethalMultiplier)
    {
        float ram = CalculateRamDamage(leviathanMassKg, targetMassKg, relativeSpeedMS, ramScale);
        if (lethalBite)
        {
            return Mathf.Max(ram * Mathf.Max(1f, lethalMultiplier), Mathf.Max(1f, targetMaxHealth) * 2.25f);
        }

        return ram * Mathf.Max(1f, biteMultiplier);
    }

    public static int NotifyArtilleryReport(Vector3 reportPosition, float reportRangeMeters, CoreTacticalShipMotor shooter)
    {
        CoreTacticalLeviathanController[] leviathans = FindObjectsByType<CoreTacticalLeviathanController>(FindObjectsSortMode.None);
        int affected = 0;
        float range = Mathf.Max(1f, reportRangeMeters);
        for (int i = 0; i < leviathans.Length; i++)
        {
            CoreTacticalLeviathanController leviathan = leviathans[i];
            if (leviathan == null || !leviathan.IsAliveSelf())
            {
                continue;
            }

            float distance = FlatDistance(leviathan.transform.position, reportPosition);
            if (distance > range)
            {
                continue;
            }

            if (leviathan.IsTargetTooLargeForAggression(shooter))
            {
                leviathan.currentTarget = shooter;
                leviathan.retreatUntilTime = Mathf.Max(
                    leviathan.retreatUntilTime,
                    Time.time + Mathf.Max(0.1f, leviathan.threatRetreatSeconds));
                leviathan.CommandRetreatFrom(shooter);
                affected++;
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(distance / range);
            leviathan.AddAggression(leviathan.artilleryReportAggression * Mathf.Lerp(0.35f, 1f, falloff), shooter);
            affected++;
        }

        return affected;
    }

    public static int NotifyProjectileImpact(Vector3 impactPosition, float radiusMeters, float aggressionAmount)
    {
        CoreTacticalLeviathanController[] leviathans = FindObjectsByType<CoreTacticalLeviathanController>(FindObjectsSortMode.None);
        int affected = 0;
        float radius = Mathf.Max(1f, radiusMeters);
        for (int i = 0; i < leviathans.Length; i++)
        {
            CoreTacticalLeviathanController leviathan = leviathans[i];
            if (leviathan == null || !leviathan.IsAliveSelf())
            {
                continue;
            }

            float distance = FlatDistance(leviathan.transform.position, impactPosition);
            if (distance > radius)
            {
                continue;
            }

            float falloff = 1f - Mathf.Clamp01(distance / radius);
            leviathan.AddAggression(Mathf.Max(0f, aggressionAmount) * Mathf.Lerp(0.35f, 1f, falloff), null);
            affected++;
        }

        return affected;
    }

    private void Tick(float deltaSeconds)
    {
        ResolveLinks();
        if (ship == null || !IsAliveSelf())
        {
            return;
        }

        SyncLengthFromShip();
        ObserveDamageAggression();
        Regenerate(deltaSeconds);
        if (TryAvoidCloud(deltaSeconds))
        {
            return;
        }

        if (Time.time < retreatUntilTime)
        {
            CommandRetreatFrom(currentTarget);
            return;
        }

        DecayPreAttackAggression(deltaSeconds);
        UpdateProximityAggression(deltaSeconds);
        UpdateAttack(deltaSeconds);
        if (IsAttackCommitted)
        {
            return;
        }

        if (TryFeed(deltaSeconds))
        {
            return;
        }

        TryIdleWander(deltaSeconds);
    }

    private void ResolveLinks()
    {
        ship ??= GetComponent<CoreTacticalShipMotor>();
        health ??= GetComponent<CoreTacticalPrototypeHealth>();
    }

    private void DisableShipDeathBurst()
    {
        if (health != null)
        {
            health.createDeathExplosionVisual = false;
        }
    }

    private void SetWanderAnchor(Vector3 position)
    {
        wanderAnchorPosition = position;
        hasWanderAnchor = true;
    }

    private void EnsureWanderAnchor()
    {
        if (!hasWanderAnchor)
        {
            SetWanderAnchor(ship != null ? ship.transform.position : transform.position);
        }
    }

    private void SyncLengthFromShip()
    {
        if (ship != null)
        {
            lengthMeters = Mathf.Max(10f, ship.hullSizeMeters.z);
        }
        else
        {
            lengthMeters = Mathf.Max(10f, lengthMeters);
        }
    }

    private bool IsAliveSelf()
    {
        return health == null || health.currentHealth > 0f;
    }

    private void ObserveDamageAggression()
    {
        if (health == null)
        {
            return;
        }

        if (lastObservedHealth <= 0f)
        {
            lastObservedHealth = health.currentHealth;
            return;
        }

        if (health.currentHealth + 0.001f < lastObservedHealth)
        {
            AddDirectDamageAggression(FindNearestValidPrey(out _));
        }

        lastObservedHealth = health.currentHealth;
    }

    private void AddDirectDamageAggression(CoreTacticalShipMotor suspectedAttacker)
    {
        if (suspectedAttacker != null && IsTargetTooLargeForAggression(suspectedAttacker))
        {
            currentTarget = suspectedAttacker;
            retreatUntilTime = Mathf.Max(retreatUntilTime, Time.time + Mathf.Max(0.1f, threatRetreatSeconds));
            CommandRetreatFrom(suspectedAttacker);
            return;
        }

        AddAggression(directDamageAggression, suspectedAttacker);
    }

    private void Regenerate(float deltaSeconds)
    {
        if (health == null || health.currentHealth <= 0f || health.currentHealth >= health.maxHealth)
        {
            return;
        }

        health.currentHealth = Mathf.Min(
            health.maxHealth,
            health.currentHealth + health.maxHealth * Mathf.Max(0f, passiveRegenerationPercentPerSecond) * deltaSeconds);
    }

    private bool TryAvoidCloud(float deltaSeconds)
    {
        if (Time.time < cloudAvoidUntilTime)
        {
            CommandMoveAway(cloudAvoidDirection, Mathf.Max(fleeDistanceMeters, cloudAvoidDistanceMeters));
            return true;
        }

        CoreTacticalGasCloud[] clouds = FindObjectsByType<CoreTacticalGasCloud>(FindObjectsSortMode.None);
        for (int i = 0; i < clouds.Length; i++)
        {
            CoreTacticalGasCloud cloud = clouds[i];
            if (cloud == null || !cloud.OverlapsCollectionSphere(ship.transform.position, ResolveContactRadius()))
            {
                continue;
            }

            Vector3 away = ship.transform.position - cloud.transform.position;
            away.y = 0f;
            cloudAvoidDirection = Flatten(away, ship.transform.forward);
            cloudAvoidUntilTime = Time.time + Mathf.Max(0.1f, cloudAvoidSeconds);
            CommandMoveAway(cloudAvoidDirection, Mathf.Max(fleeDistanceMeters, cloudAvoidDistanceMeters));
            return true;
        }

        return false;
    }

    private struct FeedingTarget
    {
        public CoreTacticalOreFragment fragment;
        public CoreTacticalOreBoulder boulder;
        public CoreTacticalAutomatonWreck wreck;
        public Vector3 position;
        public float radiusMeters;
        public float sqrDistance;

        public bool IsValid => fragment != null || boulder != null || wreck != null;
    }

    private bool TryFeed(float deltaSeconds)
    {
        bool lowHealth = health != null && health.maxHealth > 0f && health.currentHealth / health.maxHealth <= Mathf.Clamp01(lowHealthFeedingThreshold01);
        bool idleFeed = Time.time >= nextIdleFeedingTime;
        if (!lowHealth && !idleFeed)
        {
            return false;
        }

        FeedingTarget feedingTarget = FindNearestFeedingTarget(feedingSearchRadiusMeters);
        if (!feedingTarget.IsValid)
        {
            if (idleFeed)
            {
                nextIdleFeedingTime = Time.time + Mathf.Max(0.1f, idleFeedingIntervalSeconds);
            }

            return false;
        }

        float contact = Mathf.Max(feedingContactRadiusMeters, ResolveContactRadius() * 0.65f + feedingTarget.radiusMeters);
        if (FlatDistance(ship.transform.position, feedingTarget.position) <= contact)
        {
            if (TryEatFeedingTarget(feedingTarget))
            {
                nextIdleFeedingTime = Time.time + Mathf.Max(0.1f, idleFeedingIntervalSeconds);
                return true;
            }
        }

        Vector3 toFood = feedingTarget.position - ship.transform.position;
        toFood.y = 0f;
        Vector3 forward = Flatten(toFood, ship.transform.forward);
        Vector3 commandPosition = feedingTarget.position + forward * Mathf.Max(60f, lengthMeters * 0.7f + feedingTarget.radiusMeters);
        commandPosition.y = ship.transform.position.y;
        ship.SetCommand(commandPosition, forward);
        return true;
    }

    private FeedingTarget FindNearestFeedingTarget(float radiusMeters)
    {
        FeedingTarget best = new FeedingTarget
        {
            sqrDistance = Mathf.Max(1f, radiusMeters) * Mathf.Max(1f, radiusMeters)
        };

        CoreTacticalOreFragment[] fragments = FindObjectsByType<CoreTacticalOreFragment>(FindObjectsSortMode.None);
        for (int i = 0; i < fragments.Length; i++)
        {
            CoreTacticalOreFragment fragment = fragments[i];
            if (fragment == null || fragment.rawMassKg <= 0f)
            {
                continue;
            }

            ConsiderFeedingTarget(
                ref best,
                fragment.transform.position,
                Mathf.Max(0.5f, fragment.transform.localScale.x * 0.5f),
                fragment,
                null,
                null);
        }

        CoreTacticalOreBoulder[] boulders = FindObjectsByType<CoreTacticalOreBoulder>(FindObjectsSortMode.None);
        for (int i = 0; i < boulders.Length; i++)
        {
            CoreTacticalOreBoulder boulder = boulders[i];
            if (boulder == null
                || boulder.CurrentHealth <= 0f
                || boulder.PhysicalMassKg <= 0f
                || !CanEatBodyByLength(lengthMeters, boulder.EffectiveDiameterMeters))
            {
                continue;
            }

            ConsiderFeedingTarget(
                ref best,
                boulder.transform.position,
                boulder.EffectiveDiameterMeters * 0.5f,
                null,
                boulder,
                null);
        }

        CoreTacticalAutomatonWreck[] wrecks = FindObjectsByType<CoreTacticalAutomatonWreck>(FindObjectsSortMode.None);
        for (int i = 0; i < wrecks.Length; i++)
        {
            CoreTacticalAutomatonWreck wreck = wrecks[i];
            if (wreck == null
                || wreck.currentHealth <= 0f
                || !CanEatBodyByLength(lengthMeters, wreck.ApproximateLengthMeters))
            {
                continue;
            }

            ConsiderFeedingTarget(
                ref best,
                wreck.transform.position,
                wreck.VisualRadiusMeters,
                null,
                null,
                wreck);
        }

        return best;
    }

    private void ConsiderFeedingTarget(
        ref FeedingTarget best,
        Vector3 position,
        float radiusMeters,
        CoreTacticalOreFragment fragment,
        CoreTacticalOreBoulder boulder,
        CoreTacticalAutomatonWreck wreck)
    {
        if (ship == null)
        {
            return;
        }

        float sqr = FlatSqrDistance(ship.transform.position, position);
        if (sqr >= best.sqrDistance)
        {
            return;
        }

        best.fragment = fragment;
        best.boulder = boulder;
        best.wreck = wreck;
        best.position = position;
        best.radiusMeters = Mathf.Max(0.5f, radiusMeters);
        best.sqrDistance = sqr;
    }

    private bool TryEatFeedingTarget(FeedingTarget feedingTarget)
    {
        if (feedingTarget.fragment != null)
        {
            return TryEatFragment(feedingTarget.fragment);
        }

        if (feedingTarget.boulder != null)
        {
            return TryEatBoulder(feedingTarget.boulder);
        }

        return feedingTarget.wreck != null && TryEatWreck(feedingTarget.wreck);
    }

    private CoreTacticalOreFragment FindNearestOreFragment(float radiusMeters)
    {
        CoreTacticalOreFragment[] fragments = FindObjectsByType<CoreTacticalOreFragment>(FindObjectsSortMode.None);
        CoreTacticalOreFragment best = null;
        float bestSqr = Mathf.Max(1f, radiusMeters) * Mathf.Max(1f, radiusMeters);
        for (int i = 0; i < fragments.Length; i++)
        {
            CoreTacticalOreFragment fragment = fragments[i];
            if (fragment == null || fragment.rawMassKg <= 0f)
            {
                continue;
            }

            float sqr = FlatSqrDistance(ship.transform.position, fragment.transform.position);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = fragment;
            }
        }

        return best;
    }

    private bool TryEatFragment(CoreTacticalOreFragment fragment)
    {
        if (fragment == null || fragment.rawMassKg <= 0f)
        {
            return false;
        }

        HealFromConsumedMass(fragment.rawMassKg);
        fragment.ConsumeByLeviathan();
        return true;
    }

    private bool TryEatBoulder(CoreTacticalOreBoulder boulder)
    {
        if (boulder == null
            || boulder.CurrentHealth <= 0f
            || boulder.PhysicalMassKg <= 0f
            || !CanEatBodyByLength(lengthMeters, boulder.EffectiveDiameterMeters))
        {
            return false;
        }

        HealFromConsumedMass(boulder.PhysicalMassKg);
        boulder.ConsumeByLeviathan();
        return true;
    }

    private bool TryEatWreck(CoreTacticalAutomatonWreck wreck)
    {
        if (wreck == null
            || wreck.currentHealth <= 0f
            || !CanEatBodyByLength(lengthMeters, wreck.ApproximateLengthMeters))
        {
            return false;
        }

        HealFromConsumedMass(wreck.massKg);
        wreck.ConsumeByLeviathan();
        return true;
    }

    private void HealFromConsumedMass(float consumedKg)
    {
        if (health == null || health.currentHealth <= 0f)
        {
            return;
        }

        float heal01 = Mathf.Max(0f, consumedKg) / 1000f * Mathf.Max(0f, feedingHealthPercentPerTon);
        health.currentHealth = Mathf.Min(health.maxHealth, health.currentHealth + health.maxHealth * heal01);
        lastObservedHealth = health.currentHealth;
    }

    private void UpdateProximityAggression(float deltaSeconds)
    {
        CoreTacticalShipMotor target = FindNearestValidPrey(out float distance);
        if (target == null)
        {
            return;
        }

        if (IsTargetTooLargeForAggression(target))
        {
            if (distance <= aggressionRadiusMeters)
            {
                CommandRetreatFrom(target);
            }

            return;
        }

        if (distance > aggressionRadiusMeters)
        {
            return;
        }

        float build = CalculateProximityAggressionRate(distance);
        AddAggression(build * deltaSeconds, target);
        if (currentTarget == target && (aggression01 >= Mathf.Clamp01(stalkAggressionThreshold01) || distance <= closeAggressionRadiusMeters))
        {
            CommandStalkTarget(target, distance);
        }
    }

    private void DecayPreAttackAggression(float deltaSeconds)
    {
        if (attackRemainingSeconds > 0f || aggression01 >= 1f || aggression01 <= 0f)
        {
            return;
        }

        aggression01 = Mathf.MoveTowards(aggression01, 0f, Mathf.Max(0f, aggressionDecayPerSecond) * Mathf.Max(0f, deltaSeconds));
    }

    private float CalculateProximityAggressionRate(float distanceMeters)
    {
        float outer01 = 1f - Mathf.Clamp01(distanceMeters / Mathf.Max(1f, aggressionRadiusMeters));
        float close01 = 1f - Mathf.Clamp01(distanceMeters / Mathf.Max(1f, closeAggressionRadiusMeters));
        return aggressionPerSecondNear * outer01 * outer01
            + aggressionPerSecondClose * close01 * close01;
    }

    private void UpdateAttack(float deltaSeconds)
    {
        if (attackRemainingSeconds <= 0f && aggression01 >= 1f)
        {
            CoreTacticalShipMotor attackTarget = IsLiveTarget(currentTarget) && !IsTargetTooLargeForAggression(currentTarget)
                ? currentTarget
                : FindNearestValidPrey(out _);
            if (attackTarget != null && !IsTargetTooLargeForAggression(attackTarget))
            {
                currentTarget = attackTarget;
                attackRemainingSeconds = Mathf.Max(0.1f, attackCommitSeconds);
            }
            else
            {
                aggression01 = 0f;
            }
        }

        if (attackRemainingSeconds <= 0f)
        {
            return;
        }

        if (!IsLiveTarget(currentTarget) || IsTargetTooLargeForAggression(currentTarget))
        {
            attackRemainingSeconds = 0f;
            aggression01 = 0f;
            currentTarget = null;
            return;
        }

        attackRemainingSeconds -= deltaSeconds;
        if (attackRemainingSeconds <= 0f)
        {
            aggression01 = 0f;
            currentTarget = null;
            return;
        }

        CommandAttackPass(currentTarget);
        float contactRange = ResolveAttackContactRange(currentTarget);
        if (FlatDistance(ship.transform.position, currentTarget.transform.position) <= contactRange)
        {
            float speed = ResolveRelativeFlatSpeed(currentTarget);
            if (ApplyAttackDamage(currentTarget, speed, false) > 0f)
            {
                aggression01 = 0f;
                attackRemainingSeconds = 0f;
                retreatUntilTime = Time.time + Mathf.Max(0.1f, attackCooldownSeconds);
            }
        }
    }

    private void CommandAttackPass(CoreTacticalShipMotor target)
    {
        if (target == null || ship == null || Time.time < nextDecisionTime)
        {
            return;
        }

        nextDecisionTime = Time.time + 0.24f;
        Vector3 toTarget = target.transform.position - ship.transform.position;
        toTarget.y = 0f;
        Vector3 forward = Flatten(toTarget, ship.transform.forward);
        Vector3 commandPosition = target.transform.position + forward * Mathf.Max(180f, lengthMeters * 1.5f);
        commandPosition.y = ship.transform.position.y;
        ship.SetCommand(commandPosition, forward);
    }

    private void CommandStalkTarget(CoreTacticalShipMotor target, float distanceMeters)
    {
        if (target == null || ship == null || Time.time < nextDecisionTime)
        {
            return;
        }

        nextDecisionTime = Time.time + 0.35f;
        Vector3 toTarget = target.transform.position - ship.transform.position;
        toTarget.y = 0f;
        Vector3 forward = Flatten(toTarget, ship.transform.forward);
        float standoff = Mathf.Max(ResolveAttackContactRange(target) * 1.45f, lengthMeters * 1.15f);
        Vector3 commandPosition = distanceMeters < standoff * 0.75f
            ? target.transform.position + forward * standoff
            : target.transform.position - forward * standoff;
        commandPosition.y = ship.transform.position.y;
        ship.SetCommand(commandPosition, forward);
    }

    private void CommandRetreatFrom(CoreTacticalShipMotor target)
    {
        if (target == null || ship == null)
        {
            return;
        }

        Vector3 away = ship.transform.position - target.transform.position;
        away.y = 0f;
        CommandMoveAway(away, fleeDistanceMeters);
    }

    private void CommandMoveAway(Vector3 direction, float distanceMeters)
    {
        if (ship == null || Time.time < nextDecisionTime)
        {
            return;
        }

        nextDecisionTime = Time.time + 0.25f;
        Vector3 forward = Flatten(direction, ship.transform.forward);
        Vector3 commandPosition = ship.transform.position + forward * Mathf.Max(50f, distanceMeters);
        commandPosition.y = ship.transform.position.y;
        ship.SetCommand(commandPosition, forward);
    }

    private bool TryIdleWander(float deltaSeconds)
    {
        if (ship == null)
        {
            return false;
        }

        EnsureWanderAnchor();
        float arrivalDistance = FlatDistance(ship.transform.position, ship.TargetPosition);
        if (Time.time < nextIdleWanderTime && arrivalDistance > Mathf.Max(20f, ResolveContactRadius() * 0.35f))
        {
            return false;
        }

        float interval = Mathf.Max(0.1f, idleWanderIntervalSeconds);
        float radius = Mathf.Max(50f, idleWanderRadiusMeters);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(radius * 0.35f, radius);
        Vector3 target = wanderAnchorPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        target.y = ship.transform.position.y;
        Vector3 forward = Flatten(target - ship.transform.position, ship.transform.forward);
        ship.SetCommand(target, forward);
        nextIdleWanderTime = Time.time + interval * Random.Range(0.65f, 1.25f);
        return true;
    }

    private float ApplyAttackDamage(CoreTacticalShipMotor target, float relativeSpeedMS, bool ignoreMinimumSpeed)
    {
        if (target == null || ship == null)
        {
            return 0f;
        }

        float speed = Mathf.Max(0f, relativeSpeedMS);
        if (!ignoreMinimumSpeed && speed < Mathf.Max(0f, ramMinRelativeSpeedMS))
        {
            return 0f;
        }

        CoreTacticalPrototypeHealth targetHealth = target.GetComponent<CoreTacticalPrototypeHealth>();
        if (targetHealth == null || targetHealth.currentHealth <= 0f)
        {
            return 0f;
        }

        float targetLength = ResolveTargetLength(target);
        bool bite = CanBiteTarget(lengthMeters, targetLength);
        bool lethalBite = bite && IsLethalBiteTarget(lengthMeters, targetLength);
        float targetMass = target.Body != null ? target.Body.mass : Mathf.Max(1f, target.massKg);
        float leviathanMass = ship.Body != null ? ship.Body.mass : Mathf.Max(1f, ship.massKg);
        float damage = bite
            ? CalculateBiteDamage(
                leviathanMass,
                targetMass,
                speed,
                targetHealth.maxHealth,
                lethalBite,
                ramDamageScale,
                biteDamageMultiplier,
                lethalBiteDamageMultiplier)
            : CalculateRamDamage(leviathanMass, targetMass, speed, ramDamageScale);
        if (damage <= 0f)
        {
            return 0f;
        }

        targetHealth.ApplyDamage(CoreTacticalDamageRequest.Kinetic(damage, bite ? "leviathan bite" : "leviathan ram"));
        return damage;
    }

    private void AddAggression(float amount, CoreTacticalShipMotor target)
    {
        if (amount <= 0f)
        {
            return;
        }

        CoreTacticalShipMotor aggressionTarget = target;
        if (aggressionTarget == null)
        {
            aggressionTarget = IsLiveTarget(currentTarget) && !IsTargetTooLargeForAggression(currentTarget)
                ? currentTarget
                : FindNearestValidPrey(out _);
        }

        if (aggressionTarget == null || IsTargetTooLargeForAggression(aggressionTarget))
        {
            return;
        }

        currentTarget = aggressionTarget;
        aggression01 = Mathf.Clamp01(aggression01 + amount);
    }

    private CoreTacticalShipMotor FindNearestValidPrey(out float distanceMeters)
    {
        distanceMeters = float.PositiveInfinity;
        if (ship == null)
        {
            return null;
        }

        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        CoreTacticalShipMotor best = null;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            CoreTacticalShipMotor candidate = combatant != null ? combatant.ship : null;
            if (!IsLiveTarget(candidate) || candidate == ship || candidate.GetComponent<CoreTacticalOreBoulder>() != null || candidate.GetComponent<CoreTacticalLeviathanController>() != null)
            {
                continue;
            }

            float sqr = FlatSqrDistance(ship.transform.position, candidate.transform.position);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }

        distanceMeters = best != null ? Mathf.Sqrt(bestSqr) : float.PositiveInfinity;
        return best;
    }

    private bool IsTargetTooLargeForAggression(CoreTacticalShipMotor target)
    {
        return target != null && IsLeviathanTooSmallForTarget(lengthMeters, ResolveTargetLength(target));
    }

    private static bool IsLiveTarget(CoreTacticalShipMotor target)
    {
        if (target == null)
        {
            return false;
        }

        CoreTacticalPrototypeHealth targetHealth = target.GetComponent<CoreTacticalPrototypeHealth>();
        return targetHealth == null || targetHealth.currentHealth > 0f;
    }

    private float ResolveAttackContactRange(CoreTacticalShipMotor target)
    {
        return Mathf.Max(8f, (lengthMeters + ResolveTargetLength(target)) * 0.28f);
    }

    private float ResolveContactRadius()
    {
        if (ship == null)
        {
            return Mathf.Max(8f, lengthMeters * 0.25f);
        }

        Vector3 size = ship.hullSizeMeters;
        return Mathf.Max(8f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.32f);
    }

    private static float ResolveTargetLength(CoreTacticalShipMotor target)
    {
        return target != null ? Mathf.Max(1f, target.hullSizeMeters.z) : 1f;
    }

    private float ResolveRelativeFlatSpeed(CoreTacticalShipMotor target)
    {
        Vector3 ownVelocity = ship != null && ship.Body != null ? ship.Body.linearVelocity : Vector3.zero;
        Vector3 targetVelocity = target != null && target.Body != null ? target.Body.linearVelocity : Vector3.zero;
        ownVelocity.y = 0f;
        targetVelocity.y = 0f;
        return (ownVelocity - targetVelocity).magnitude;
    }

    private static float FlatDistance(Vector3 first, Vector3 second)
    {
        return Mathf.Sqrt(FlatSqrDistance(first, second));
    }

    private static float FlatSqrDistance(Vector3 first, Vector3 second)
    {
        Vector3 delta = first - second;
        delta.y = 0f;
        return delta.sqrMagnitude;
    }

    private static Vector3 Flatten(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        fallback.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }
}
