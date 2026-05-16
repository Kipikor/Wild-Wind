using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DamageProjectile : MonoBehaviour
{
    [Header("Снаряд")]
    [InspectorName("Пресет снаряда")]
    public DamageShellPreset shell = new DamageShellPreset();
    [InspectorName("Источник")]
    [Tooltip("Название оружия или объекта, выпустившего снаряд.")]
    public string sourceName = "Тестовая пушка";
    [InspectorName("Время жизни, сек")]
    public float maxLifetimeSeconds = 8f;
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
    private float spawnedAt;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ownCollider = GetComponent<Collider>();
        previousPosition = transform.position;
        spawnedAt = Time.time;
    }

    private void FixedUpdate()
    {
        if (!hasHit)
        {
            SweepForArmorHit();
        }

        if (body != null && body.linearVelocity.sqrMagnitude > 0.001f)
        {
            lastVelocity = body.linearVelocity;
        }

        if (Time.time - spawnedAt > maxLifetimeSeconds)
        {
            Destroy(gameObject);
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
            if (hitCollider.GetComponentInParent<PaintedArmorBody>() == null
                && hitCollider.GetComponentInParent<MeshArmorBody>() == null
                && hitCollider.GetComponentInParent<ArmorZone>() == null)
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

        Vector3 velocity = lastVelocity.sqrMagnitude > 0.001f
            ? lastVelocity
            : (body != null ? body.linearVelocity : Vector3.zero);
        Vector3 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : transform.forward;

        DamageHitContext context = new DamageHitContext
        {
            shellType = shell.shellType,
            shellName = shell.displayNameRu,
            sourceName = sourceName,
            caliberMm = shell.caliberMm,
            damagePoints = shell.damagePoints,
            hullDamageOnPenetration = shell.hullDamageOnPenetration > 0.001f ? shell.hullDamageOnPenetration : shell.damagePoints,
            armorPlateDamage = shell.armorPlateDamage,
            penetrationMm = RollPenetration(shell),
            explosiveRadiusMeters = shell.explosiveRadiusMeters,
            normalizationDegrees = shell.normalizationDegrees,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            incomingDirection = direction,
            velocity = velocity
        };

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
}
