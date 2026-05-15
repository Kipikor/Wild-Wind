using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DamageProjectile : MonoBehaviour
{
    [Header("Снаряд")]
    public DamageShellPreset shell = new DamageShellPreset();
    public string sourceName = "Тестовая пушка";
    public float maxLifetimeSeconds = 8f;
    public bool destroyOnAnyCollision = true;

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
            DamageableModuleHitbox moduleHitbox = hitCollider.GetComponentInParent<DamageableModuleHitbox>();
            if (moduleHitbox != null)
            {
                if (!moduleHitbox.BlocksProjectile) continue;
            }
            else if (hitCollider.GetComponentInParent<PaintedArmorBody>() == null
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
            moduleDamage = shell.moduleDamage,
            penetrationMm = RollPenetration(shell),
            explosiveRadiusMeters = shell.explosiveRadiusMeters,
            normalizationDegrees = shell.normalizationDegrees,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            incomingDirection = direction,
            velocity = velocity
        };

        DamageableModuleHitbox moduleHitbox = hitCollider.GetComponentInParent<DamageableModuleHitbox>();
        if (moduleHitbox != null)
        {
            if (!moduleHitbox.BlocksProjectile) return false;

            moduleHitbox.ReceiveDirectHit(context);
            hasHit = true;
            return true;
        }

        PaintedArmorBody paintedArmor = hitCollider.GetComponentInParent<PaintedArmorBody>();
        if (paintedArmor != null)
        {
            paintedArmor.ReceiveHit(context);
            hasHit = true;
            return true;
        }

        MeshArmorBody meshArmor = hitCollider.GetComponentInParent<MeshArmorBody>();
        if (meshArmor != null)
        {
            meshArmor.ReceiveHit(context, triangleIndex);
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

        zone.ReceiveHit(context);
        hasHit = true;
        return true;
    }

    private static float RollPenetration(DamageShellPreset preset)
    {
        float spread = Mathf.Clamp01(preset.penetrationRollSpread);
        if (spread <= 0.001f) return preset.penetrationMm;

        return preset.penetrationMm * Random.Range(1f - spread, 1f + spread);
    }
}
