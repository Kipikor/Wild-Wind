using UnityEngine;

[DisallowMultipleComponent]
public class ArmorZone : MonoBehaviour
{
    [Header("Бронезона")]
    [InspectorName("ID зоны")]
    public string zoneId = "front";
    [InspectorName("Название")]
    public string displayNameRu = "Лобовая броня";
    [InspectorName("Толщина брони, мм")]
    [Min(0f)] public float armorMm = 40f;
    [Header("Damage resistances")]
    [Range(0f, 100f)] public float kineticResistancePercent = 48f;
    [Range(0f, 100f)] public float thermalResistancePercent = 18f;
    [Range(0f, 100f)] public float chemicalResistancePercent = 18f;
    [Range(0f, 100f)] public float explosiveResistancePercent = 28f;
    [InspectorName("Угол рикошета, град")]
    [Range(0f, 89f)] public float ricochetAngleDeg = 70f;
    [InspectorName("Множитель overmatch")]
    [Tooltip("Калибр в три толщины брони отключает авторикошет, как игровой overmatch.")]
    public float overmatchCaliberMultiplier = 3f;

    [Header("Урон")]
    [InspectorName("Множитель урона корпусу")]
    [Range(0f, 3f)] public float structureDamageMultiplier = 1f;
    [InspectorName("Доля поверхностного урона фугаса")]
    [Range(0f, 1f)] public float highExplosiveSurfaceDamageMultiplier = 0.35f;
    [InspectorName("Множитель урона тарана")]
    [Range(0f, 3f)] public float ramDamageMultiplier = 1f;

    [Header("Таран")]
    [InspectorName("Получать урон от тарана")]
    public bool receiveRamDamage = true;
    [InspectorName("Минимальная скорость тарана, м/с")]
    public float ramMinRelativeSpeedMS = 4f;
    [InspectorName("Масштаб урона тарана")]
    [Tooltip("Урон считается как sqrt(энергия удара в кДж) * этот масштаб.")]
    public float ramDamageScale = 10f;
    [InspectorName("Запасная масса другого объекта, кг")]
    public float fallbackOtherMassKg = 1000f;

    [Header("Отладка")]
    [InspectorName("Цвет gizmo")]
    public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.25f);

    public DamageableShip Owner
    {
        get
        {
            return GetComponentInParent<DamageableShip>();
        }
    }

    public CoreTacticalResistanceSet Resistances => new CoreTacticalResistanceSet(
        kineticResistancePercent,
        thermalResistancePercent,
        chemicalResistancePercent,
        explosiveResistancePercent);

    public void SetResistances(CoreTacticalResistanceSet resistances)
    {
        resistances.Normalize();
        kineticResistancePercent = resistances.kineticPercent;
        thermalResistancePercent = resistances.thermalPercent;
        chemicalResistancePercent = resistances.chemicalPercent;
        explosiveResistancePercent = resistances.explosivePercent;
    }

    private void OnValidate()
    {
        armorMm = Mathf.Max(0f, armorMm);
        kineticResistancePercent = Mathf.Clamp(kineticResistancePercent, 0f, 100f);
        thermalResistancePercent = Mathf.Clamp(thermalResistancePercent, 0f, 100f);
        chemicalResistancePercent = Mathf.Clamp(chemicalResistancePercent, 0f, 100f);
        explosiveResistancePercent = Mathf.Clamp(explosiveResistancePercent, 0f, 100f);
        ricochetAngleDeg = Mathf.Clamp(ricochetAngleDeg, 0f, 89f);
        overmatchCaliberMultiplier = Mathf.Max(0f, overmatchCaliberMultiplier);
        structureDamageMultiplier = Mathf.Clamp(structureDamageMultiplier, 0f, 3f);
        highExplosiveSurfaceDamageMultiplier = Mathf.Clamp01(highExplosiveSurfaceDamageMultiplier);
        ramDamageMultiplier = Mathf.Clamp(ramDamageMultiplier, 0f, 3f);
        ramMinRelativeSpeedMS = Mathf.Max(0f, ramMinRelativeSpeedMS);
        ramDamageScale = Mathf.Max(0f, ramDamageScale);
        fallbackOtherMassKg = Mathf.Max(1f, fallbackOtherMassKg);
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
        DamageableShip owner = Owner;
        if (owner == null) return;

        float otherMass = fallbackOtherMassKg;
        if (collision.rigidbody != null)
        {
            otherMass = Mathf.Max(1f, collision.rigidbody.mass);
        }

        float ownMass = owner.EstimatedMassKg;
        float reducedMass = ownMass > 0f ? ownMass * otherMass / Mathf.Max(1f, ownMass + otherMass) : otherMass;
        ContactPoint contact = collision.GetContact(0);
        float normalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
        float speed = normalSpeed > 0.001f ? normalSpeed : collision.relativeVelocity.magnitude;
        if (speed < ramMinRelativeSpeedMS) return;

        float energyKJ = 0.5f * reducedMass * speed * speed / 1000f;

        Vector3 incoming = collision.relativeVelocity.sqrMagnitude > 0.001f
            ? collision.relativeVelocity.normalized
            : -contact.normal;
        DamageableShip otherShip = collision.collider.GetComponentInParent<DamageableShip>();

        DamageHitContext context = new DamageHitContext
        {
            shellType = DamageShellType.Impact,
            damageType = CoreTacticalDamageType.Kinetic,
            shellName = "Таран",
            sourceName = collision.collider.name,
            damagePoints = 0f,
            penetrationMm = 0f,
            impactEnergyKJ = energyKJ,
            impactDamagePerKJ = ramDamageScale,
            impactSpeedMS = speed,
            impactSourceMassKg = otherMass,
            impactTargetMassKg = ownMass,
            impactSourceDamageMultiplier = otherShip != null ? otherShip.ramDamageDealtMultiplier : 1f,
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
