using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmorZone : MonoBehaviour
{
    [Header("Бронезона")]
    public string zoneId = "front";
    public string displayNameRu = "Лобовая броня";
    [Min(0f)] public float armorMm = 40f;
    [Range(0f, 89f)] public float ricochetAngleDeg = 70f;
    [Tooltip("Калибр в три толщины брони отключает авторикошет, как игровой overmatch.")]
    public float overmatchCaliberMultiplier = 3f;

    [Header("Урон")]
    [Range(0f, 3f)] public float structureDamageMultiplier = 1f;
    [Range(0f, 3f)] public float moduleDamageMultiplier = 0.65f;
    [Range(0f, 1f)] public float highExplosiveSurfaceDamageMultiplier = 0.35f;
    [Range(0f, 3f)] public float ramDamageMultiplier = 1f;
    public List<string> protectedModuleIds = new List<string>();

    [Header("Таран")]
    public bool receiveRamDamage = true;
    public float ramMinRelativeSpeedMS = 4f;
    public float ramDamagePerKJ = 0.08f;
    public float fallbackOtherMassKg = 1000f;

    [Header("Отладка")]
    public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.25f);

    public DamageableShip Owner
    {
        get
        {
            return GetComponentInParent<DamageableShip>();
        }
    }

    public DamageHitResult ReceiveHit(DamageHitContext context)
    {
        DamageableShip owner = Owner;
        if (owner == null)
        {
            return new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                zoneId = zoneId,
                message = "Попадание в бронезону без DamageableShip."
            };
        }

        if (context.hitNormal.sqrMagnitude < 0.001f)
        {
            context.hitNormal = EstimateSurfaceNormal(context.hitPoint);
        }

        return owner.ApplyHit(this, context);
    }

    public Vector3 EstimateSurfaceNormal(Vector3 hitPoint)
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
        {
            Vector3 fromCenter = hitPoint - transform.position;
            return fromCenter.sqrMagnitude > 0.001f ? fromCenter.normalized : transform.forward;
        }

        Vector3 localPoint = transform.InverseTransformPoint(hitPoint) - box.center;
        Vector3 half = Vector3.Max(box.size * 0.5f, Vector3.one * 0.001f);
        Vector3 normalized = new Vector3(localPoint.x / half.x, localPoint.y / half.y, localPoint.z / half.z);
        Vector3 abs = new Vector3(Mathf.Abs(normalized.x), Mathf.Abs(normalized.y), Mathf.Abs(normalized.z));

        Vector3 localNormal;
        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            localNormal = new Vector3(Mathf.Sign(normalized.x), 0f, 0f);
        }
        else if (abs.y >= abs.x && abs.y >= abs.z)
        {
            localNormal = new Vector3(0f, Mathf.Sign(normalized.y), 0f);
        }
        else
        {
            localNormal = new Vector3(0f, 0f, Mathf.Sign(normalized.z));
        }

        return transform.TransformDirection(localNormal).normalized;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!receiveRamDamage) return;
        if (collision.collider.GetComponentInParent<DamageProjectile>() != null) return;
        if (collision.relativeVelocity.magnitude < ramMinRelativeSpeedMS) return;

        DamageableShip owner = Owner;
        if (owner == null) return;

        float otherMass = fallbackOtherMassKg;
        if (collision.rigidbody != null)
        {
            otherMass = Mathf.Max(1f, collision.rigidbody.mass);
        }

        float ownMass = owner.EstimatedMassKg;
        float reducedMass = ownMass > 0f ? ownMass * otherMass / Mathf.Max(1f, ownMass + otherMass) : otherMass;
        float speed = collision.relativeVelocity.magnitude;
        float energyKJ = 0.5f * reducedMass * speed * speed / 1000f;

        ContactPoint contact = collision.GetContact(0);
        Vector3 incoming = collision.relativeVelocity.sqrMagnitude > 0.001f
            ? collision.relativeVelocity.normalized
            : -contact.normal;

        DamageHitContext context = new DamageHitContext
        {
            shellType = DamageShellType.Impact,
            shellName = "Таран",
            sourceName = collision.collider.name,
            damagePoints = 0f,
            penetrationMm = 0f,
            impactEnergyKJ = energyKJ,
            impactDamagePerKJ = ramDamagePerKJ,
            hitPoint = contact.point,
            hitNormal = contact.normal,
            incomingDirection = incoming,
            velocity = collision.relativeVelocity
        };

        owner.ApplyHit(this, context);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else
        {
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        Gizmos.matrix = previous;
    }
}

