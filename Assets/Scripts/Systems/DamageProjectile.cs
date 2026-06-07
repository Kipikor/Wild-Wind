using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DamageProjectile : MonoBehaviour
{
    private static readonly List<DamageProjectile> ActiveProjectiles = new List<DamageProjectile>();
    public static int MaxActiveProjectiles = 320;

    [Header("Снаряд")]
    [InspectorName("Пресет снаряда")]
    public DamageShellPreset shell = new DamageShellPreset();
    [InspectorName("Источник")]
    [Tooltip("Название оружия или объекта, выпустившего снаряд.")]
    public string sourceName = "Тестовая пушка";
    [InspectorName("Время жизни, сек")]
    public float maxLifetimeSeconds = 8f;
    [InspectorName("Максимальная дальность, м")]
    public float maxTravelDistanceMeters = 1500f;
    [InspectorName("Начальная скорость, м/с")]
    public float launchSpeedMS = 300f;
    [InspectorName("Множитель гравитации")]
    public float gravityScale = 1f;
    [InspectorName("Корабельный источник")]
    public ShipPhysics sourceShip;
    [InspectorName("Уничтожать при любом столкновении")]
    public bool destroyOnAnyCollision = true;
    [Header("Физический толчок")]
    [InspectorName("Двигать kinematic цель")]
    [Tooltip("Используется тестовым стендом: если цель закреплена, фугас считает урон, но не двигает Rigidbody.")]
    public bool moveKinematicTargets;
    [InspectorName("Масштаб импульса фугаса")]
    public float highExplosiveImpulseScale = 25f;
    [InspectorName("Макс. Δv от фугаса, м/с")]
    public float highExplosiveMaxDeltaVelocityMS = 8f;

    private Rigidbody body;
    private Collider ownCollider;
    private Vector3 lastVelocity;
    private Vector3 previousPosition;
    private Vector3 launchPosition;
    private float spawnedAt;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
        launchPosition = transform.position;
        previousPosition = transform.position;
        spawnedAt = Time.time;
        RegisterActiveProjectile();
    }

    private void OnDestroy()
    {
        ActiveProjectiles.Remove(this);
    }

    public void Initialize(
        DamageShellPreset preset,
        string source,
        ShipPhysics ship,
        Vector3 initialVelocity,
        float rangeMeters,
        float projectileLifetimeSeconds,
        float projectileGravityScale)
    {
        shell = CopyPreset(preset);
        sourceName = source ?? "";
        sourceShip = ship;
        launchPosition = transform.position;
        previousPosition = transform.position;
        spawnedAt = Time.time;
        maxTravelDistanceMeters = Mathf.Max(1f, rangeMeters);
        maxLifetimeSeconds = Mathf.Max(0.05f, projectileLifetimeSeconds);
        gravityScale = Mathf.Max(0f, projectileGravityScale);
        launchSpeedMS = Mathf.Max(0.001f, initialVelocity.magnitude);
        lastVelocity = initialVelocity;
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (body != null)
        {
            body.linearVelocity = initialVelocity;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void FixedUpdate()
    {
        if (!hasHit)
        {
            SweepForArmorHit();
        }

        ApplyBallisticForces();

        if (body != null && body.linearVelocity.sqrMagnitude > 0.001f)
        {
            lastVelocity = body.linearVelocity;
        }

        if ((transform.position - launchPosition).magnitude >= Mathf.Max(1f, maxTravelDistanceMeters))
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time - spawnedAt > maxLifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        previousPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        ContactPoint contact = collision.GetContact(0);
        bool armorHit = TryApplyHit(collision.collider, contact.point, contact.normal);

        if (destroyOnAnyCollision || armorHit)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || other == null || other == ownCollider) return;

        Vector3 velocity = lastVelocity.sqrMagnitude > 0.001f
            ? lastVelocity
            : (body != null ? body.linearVelocity : transform.forward);
        Vector3 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        if ((hitPoint - transform.position).sqrMagnitude < 0.0001f)
        {
            hitPoint = transform.position;
        }

        Vector3 hitNormal = -direction;
        if (TryApplyHit(other, hitPoint, hitNormal))
        {
            Destroy(gameObject);
            return;
        }

        if (destroyOnAnyCollision && (sourceShip == null || !other.transform.IsChildOf(sourceShip.transform)))
        {
            Destroy(gameObject);
        }
    }

    private void SweepForArmorHit()
    {
        Vector3 currentPosition = transform.position;
        Vector3 delta = currentPosition - previousPosition;
        float distance = delta.magnitude;
        if (distance <= 0.001f) return;

        RaycastHit[] hits = Physics.RaycastAll(
            previousPosition,
            delta / distance,
            distance + Mathf.Max(0.01f, transform.lossyScale.x * 0.5f),
            ~0,
            QueryTriggerInteraction.Collide);

        RaycastHit bestHit = default;
        bool found = false;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider == ownCollider) continue;
            if (sourceShip != null && hitCollider.transform.IsChildOf(sourceShip.transform)) continue;
            if (hitCollider.GetComponentInParent<PaintedArmorBody>() == null
                && hitCollider.GetComponentInParent<MeshArmorBody>() == null
                && hitCollider.GetComponentInParent<ArmorZone>() == null
                && hitCollider.GetComponentInParent<Leviathan>() == null)
            {
                continue;
            }

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                bestHit = hits[i];
                found = true;
            }
        }

        if (!found) return;

        if (TryApplyHit(bestHit.collider, bestHit.point, bestHit.normal, bestHit.triangleIndex))
        {
            Destroy(gameObject);
        }
    }

    private bool TryApplyHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal, int triangleIndex = -1)
    {
        if (hitCollider == null || hitCollider == ownCollider) return false;
        if (sourceShip != null && hitCollider.transform.IsChildOf(sourceShip.transform)) return false;

        Vector3 velocity = lastVelocity.sqrMagnitude > 0.001f
            ? lastVelocity
            : (body != null ? body.linearVelocity : Vector3.zero);
        Vector3 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;
        float distanceMeters = Mathf.Max(0f, (hitPoint - launchPosition).magnitude);
        float retainedSpeedMS = Mathf.Max(0.001f, launchSpeedMS)
            * BallisticFireControl.CalculateRangeVelocityRetention(
                distanceMeters,
                Mathf.Max(1f, maxTravelDistanceMeters),
                shell.velocityRetentionAtMaxRange);
        float penetrationMultiplier = BallisticFireControl.CalculatePenetrationMultiplier(
            distanceMeters,
            Mathf.Max(1f, maxTravelDistanceMeters),
            shell.penetrationAtMaxRangeMultiplier,
            Mathf.Min(velocity.magnitude, retainedSpeedMS),
            Mathf.Max(0.001f, launchSpeedMS));

        DamageHitContext context = new DamageHitContext
        {
            shellType = shell.shellType,
            shellName = shell.displayNameRu,
            sourceName = sourceName,
            caliberMm = shell.caliberMm,
            damagePoints = shell.damagePoints,
            hullDamageOnPenetration = shell.hullDamageOnPenetration > 0.001f ? shell.hullDamageOnPenetration : shell.damagePoints,
            penetrationMm = RollPenetration(shell) * penetrationMultiplier,
            explosiveRadiusMeters = shell.explosiveRadiusMeters,
            normalizationDegrees = shell.normalizationDegrees,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            incomingDirection = direction,
            velocity = velocity
        };

        Leviathan leviathan = hitCollider.GetComponentInParent<Leviathan>();
        if (leviathan != null)
        {
            float damage = Mathf.Max(shell.damagePoints, shell.hullDamageOnPenetration);
            if (shell.shellType == DamageShellType.ArmorPiercing)
            {
                damage *= Mathf.Lerp(0.55f, 1f, penetrationMultiplier);
            }

            leviathan.ApplyDamage(damage);
            Leviathan.AddAlarmNear(
                hitPoint,
                sourceShip != null ? Mathf.Max(0f, sourceShip.weaponShotAlarmRadiusMeters) : 0f,
                leviathan.alarmShotNearAdd,
                "попадание снаряда рядом",
                leviathan,
                leviathan.alarmShotAtSelfAdd,
                sourceShip);
            hasHit = true;
            return true;
        }

        PaintedArmorBody paintedArmor = hitCollider.GetComponentInParent<PaintedArmorBody>();
        if (paintedArmor != null)
        {
            DamageHitResult result = paintedArmor.ReceiveHit(context);
            ApplyHighExplosiveImpulse(hitCollider, hitPoint, hitNormal, direction, result);
            hasHit = true;
            return true;
        }

        MeshArmorBody meshArmor = hitCollider.GetComponentInParent<MeshArmorBody>();
        if (meshArmor != null)
        {
            DamageHitResult result = meshArmor.ReceiveHit(context, triangleIndex);
            ApplyHighExplosiveImpulse(hitCollider, hitPoint, hitNormal, direction, result);
            hasHit = true;
            return true;
        }

        ArmorZone zone = hitCollider.GetComponentInParent<ArmorZone>();
        DamageableShip ship = hitCollider.GetComponentInParent<DamageableShip>();
        if (zone == null && ship != null)
        {
            zone = ship.GetDefaultZone();
        }

        if (zone == null) return false;

        DamageHitResult zoneResult = zone.ReceiveHit(context);
        ApplyHighExplosiveImpulse(hitCollider, hitPoint, hitNormal, direction, zoneResult);
        hasHit = true;
        return true;
    }

    private void ApplyHighExplosiveImpulse(
        Collider hitCollider,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 projectileDirection,
        DamageHitResult result)
    {
        if (shell.shellType != DamageShellType.HighExplosive) return;

        Rigidbody targetBody = ResolveTargetRigidbody(hitCollider);
        if (targetBody == null) return;
        if (targetBody.isKinematic)
        {
            if (!moveKinematicTargets) return;

            targetBody.isKinematic = false;
        }

        targetBody.useGravity = false;

        Vector3 pushDirection = targetBody.worldCenterOfMass - hitPoint;
        if (pushDirection.sqrMagnitude < 0.001f)
        {
            pushDirection = projectileDirection.sqrMagnitude > 0.001f ? projectileDirection : -hitNormal;
        }

        pushDirection.Normalize();
        float penetrationMultiplier = result.outcome == DamageHitOutcome.Penetration ? 3f : 1f;
        float impulseNs = Mathf.Max(0f, shell.damagePoints) * Mathf.Max(0f, highExplosiveImpulseScale) * penetrationMultiplier;
        float maxImpulse = Mathf.Max(0f, highExplosiveMaxDeltaVelocityMS) * Mathf.Max(1f, targetBody.mass);
        impulseNs = Mathf.Min(impulseNs, maxImpulse);
        if (impulseNs <= 0.001f) return;

        targetBody.AddForce(pushDirection * impulseNs, ForceMode.Impulse);
    }

    private static Rigidbody ResolveTargetRigidbody(Collider hitCollider)
    {
        if (hitCollider == null) return null;
        if (hitCollider.attachedRigidbody != null) return hitCollider.attachedRigidbody;

        Rigidbody body = hitCollider.GetComponentInParent<Rigidbody>();
        if (body != null) return body;

        MeshArmorBody meshArmor = hitCollider.GetComponentInParent<MeshArmorBody>();
        if (meshArmor != null && meshArmor.owner != null)
        {
            body = meshArmor.owner.GetComponentInParent<Rigidbody>();
            if (body != null) return body;
        }

        ArmorZone zone = hitCollider.GetComponentInParent<ArmorZone>();
        if (zone != null && zone.Owner != null)
        {
            body = zone.Owner.GetComponentInParent<Rigidbody>();
            if (body != null) return body;
        }

        DamageableShip ship = hitCollider.GetComponentInParent<DamageableShip>();
        return ship != null ? ship.GetComponentInParent<Rigidbody>() : null;
    }

    private static float RollPenetration(DamageShellPreset preset)
    {
        float spread = Mathf.Clamp01(preset.penetrationRollSpread);
        if (spread <= 0.001f) return preset.penetrationMm;

        return preset.penetrationMm * Random.Range(1f - spread, 1f + spread);
    }

    private void ApplyBallisticForces()
    {
        if (body == null || hasHit) return;

        float deltaSeconds = Mathf.Max(0f, Time.fixedDeltaTime);
        if (deltaSeconds <= 0f) return;

        Vector3 gravity = gravityScale > 0f ? Physics.gravity * gravityScale : Vector3.zero;
        float dragPerMeter = BallisticFireControl.CalculateDragPerMeter(
            Mathf.Max(1f, maxTravelDistanceMeters),
            shell != null ? shell.velocityRetentionAtMaxRange : 1f);
        body.linearVelocity = BallisticFireControl.IntegrateVelocity(
            body.linearVelocity,
            gravity,
            deltaSeconds,
            dragPerMeter);
    }

    private void RegisterActiveProjectile()
    {
        ActiveProjectiles.RemoveAll(projectile => projectile == null);
        ActiveProjectiles.Add(this);
        int maxCount = Mathf.Max(32, MaxActiveProjectiles);
        while (ActiveProjectiles.Count > maxCount)
        {
            DamageProjectile oldest = ActiveProjectiles[0];
            ActiveProjectiles.RemoveAt(0);
            if (oldest != null && oldest != this)
            {
                Destroy(oldest.gameObject);
            }
        }
    }

    private static DamageShellPreset CopyPreset(DamageShellPreset preset)
    {
        if (preset == null) return new DamageShellPreset();

        return new DamageShellPreset
        {
            displayNameRu = preset.displayNameRu,
            shellType = preset.shellType,
            caliberMm = preset.caliberMm,
            damagePoints = preset.damagePoints,
            hullDamageOnPenetration = preset.hullDamageOnPenetration,
            penetrationMm = preset.penetrationMm,
            penetrationAtMaxRangeMultiplier = preset.penetrationAtMaxRangeMultiplier,
            velocityRetentionAtMaxRange = preset.velocityRetentionAtMaxRange,
            explosiveRadiusMeters = preset.explosiveRadiusMeters,
            normalizationDegrees = preset.normalizationDegrees,
            penetrationRollSpread = preset.penetrationRollSpread,
            projectileColor = preset.projectileColor
        };
    }
}
