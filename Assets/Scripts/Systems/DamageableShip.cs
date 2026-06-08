using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DamageableShip : MonoBehaviour
{
    [Header("Прочность")]
    [InspectorName("ID корабля")]
    [Tooltip("Технический id цели в логах и будущих сохранениях.")]
    public string shipId = "target_ship";
    [InspectorName("Название")]
    [Tooltip("Название цели для инспектора и сообщений о попаданиях.")]
    public string displayNameRu = "Цель";
    [InspectorName("Максимальная прочность корпуса")]
    public float maxStructureHp = 500f;
    [InspectorName("Текущая прочность корпуса")]
    public float structureHp = 500f;

    [Header("Связь с физикой")]
    [InspectorName("Физика корабля")]
    [Tooltip("Необязательная ссылка на ShipPhysics. Нужна только для оценки массы цели в таранах.")]
    public ShipPhysics shipPhysics;

    [Header("Таран")]
    [InspectorName("Получаемый урон тараном")]
    [Tooltip("Множитель получаемого тараном/ударом урона для корпуса. 1 = как есть, 0.5 = получает вдвое меньше, 2 = вдвое больше.")]
    [Min(0f)] public float ramDamageTakenMultiplier = 1f;
    [InspectorName("Наносимый урон тараном")]
    [Tooltip("Множитель урона, который этот корпус наносит другим при таране.")]
    [Min(0f)] public float ramDamageDealtMultiplier = 1f;

    [Header("Отладка")]
    [InspectorName("Писать логи")]
    public bool debugLogging;
    [InspectorName("Последнее сообщение")]
    [TextArea(2, 5)]
    public string lastDamageMessage = "";
    [InspectorName("Итог последнего попадания")]
    public DamageHitOutcome lastHitOutcome = DamageHitOutcome.Miss;
    [InspectorName("Последняя зона")]
    public string lastHitZoneId = "";
    [InspectorName("Броня последнего попадания, мм")]
    public float lastHitArmorMm;
    [InspectorName("Приведённая броня, мм")]
    public float lastHitEffectiveArmorMm;
    [InspectorName("Угол попадания, град")]
    public float lastHitImpactAngleDeg;
    [InspectorName("Пробитие снаряда, мм")]
    public float lastHitPenetrationMm;
    [InspectorName("Попаданий всего")]
    public int hitCount;
    [InspectorName("Пробитий")]
    public int penetrationCount;
    [InspectorName("Рикошетов")]
    public int ricochetCount;
    [InspectorName("Непробитий")]
    public int noPenetrationCount;
    [InspectorName("Фугасных взрывов")]
    public int explosiveSplashCount;
    [InspectorName("Ударов/таранов")]
    public int impactCount;
    [InspectorName("Последние события")]
    public List<string> recentEvents = new List<string>();
    [InspectorName("Размер истории событий")]
    public int maxRecentEvents = 8;

    public float StructureRatio
    {
        get
        {
            if (maxStructureHp <= 0.001f) return 1f;
            return Mathf.Clamp01(structureHp / maxStructureHp);
        }
    }

    public float EstimatedMassKg
    {
        get
        {
            Rigidbody body = GetComponentInParent<Rigidbody>();
            if (body != null) return Mathf.Max(1f, body.mass);
            if (shipPhysics != null) return shipPhysics.GetTotalMassKg();
            return 1000f;
        }
    }

    private void Reset()
    {
        shipPhysics = GetComponent<ShipPhysics>();
        ResetDamageState();
    }

    private void Awake()
    {
        if (shipPhysics == null)
        {
            shipPhysics = GetComponent<ShipPhysics>();
        }
    }

    private void OnValidate()
    {
        maxStructureHp = Mathf.Max(1f, maxStructureHp);
        structureHp = Mathf.Clamp(structureHp, 0f, maxStructureHp);
        ramDamageTakenMultiplier = Mathf.Max(0f, ramDamageTakenMultiplier);
        ramDamageDealtMultiplier = Mathf.Max(0f, ramDamageDealtMultiplier);
        maxRecentEvents = Mathf.Clamp(maxRecentEvents, 1, 20);
    }

    public void ResetDamageState()
    {
        structureHp = Mathf.Max(1f, maxStructureHp);
        hitCount = 0;
        penetrationCount = 0;
        ricochetCount = 0;
        noPenetrationCount = 0;
        explosiveSplashCount = 0;
        impactCount = 0;
        lastHitOutcome = DamageHitOutcome.Miss;
        lastHitZoneId = "";
        lastHitArmorMm = 0f;
        lastHitEffectiveArmorMm = 0f;
        lastHitImpactAngleDeg = 0f;
        lastHitPenetrationMm = 0f;
        recentEvents.Clear();
        lastDamageMessage = "Повреждения сброшены.";

        PaintedArmorBody[] armorBodies = GetComponentsInChildren<PaintedArmorBody>();
        for (int i = 0; i < armorBodies.Length; i++)
        {
            if (armorBodies[i] != null)
            {
                armorBodies[i].ResetArmorState();
            }
        }

        MeshArmorBody[] meshArmorBodies = GetComponentsInChildren<MeshArmorBody>();
        for (int i = 0; i < meshArmorBodies.Length; i++)
        {
            if (meshArmorBodies[i] != null)
            {
                meshArmorBodies[i].ResetArmorState();
            }
        }

        LogEvent(lastDamageMessage);
    }

    public DamageHitResult ApplyHit(ArmorZone zone, DamageHitContext context)
    {
        if (zone == null)
        {
            DamageHitResult miss = new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                message = "Попадание прошло мимо бронезоны."
            };
            LogEvent(miss.message);
            return miss;
        }

        if (context.hitNormal.sqrMagnitude < 0.001f)
        {
            context.hitNormal = zone.EstimateSurfaceNormal(context.hitPoint);
        }

        return ApplyHit(ArmorSurface.FromZone(zone), context);
    }

    public DamageHitResult ApplyHit(ArmorSurface surface, DamageHitContext context)
    {
        hitCount++;

        Vector3 incoming = context.incomingDirection;
        if (incoming.sqrMagnitude < 0.001f && context.velocity.sqrMagnitude > 0.001f)
        {
            incoming = context.velocity.normalized;
        }

        if (incoming.sqrMagnitude < 0.001f)
        {
            incoming = Vector3.forward;
        }

        incoming.Normalize();

        Vector3 normal = context.hitNormal.sqrMagnitude > 0.001f
            ? context.hitNormal.normalized
            : -incoming;
        if (Vector3.Dot(normal, -incoming) < 0f)
        {
            normal = -normal;
        }

        float rawAngle = Vector3.Angle(-incoming, normal);
        float normalizedAngle = Mathf.Max(0f, rawAngle - Mathf.Max(0f, context.normalizationDegrees));
        float cos = Mathf.Max(0.05f, Mathf.Cos(normalizedAngle * Mathf.Deg2Rad));
        float effectiveArmor = Mathf.Max(0f, surface.armorMm) / cos;

        DamageHitResult result = new DamageHitResult
        {
            zoneId = surface.zoneId,
            armorMm = surface.armorMm,
            effectiveArmorMm = effectiveArmor,
            impactAngleDeg = rawAngle,
            penetrationMm = context.penetrationMm,
            remainingStructureHp = structureHp
        };

        if (context.shellType == DamageShellType.ArmorPiercing)
        {
            ResolveArmorPiercingHit(surface, context, effectiveArmor, rawAngle, ref result);
        }
        else if (context.shellType == DamageShellType.HighExplosive)
        {
            ResolveHighExplosiveHit(surface, context, effectiveArmor, ref result);
        }
        else
        {
            ResolveImpactHit(surface, context, ref result);
        }

        result.remainingStructureHp = structureHp;
        result.message = AppendArmorTelemetry(result.message, context, result);
        if (!context.deferResultLogging)
        {
            FinalizeHitResult(result);
        }

        return result;
    }

    public void FinalizeHitResult(DamageHitResult result)
    {
        lastHitOutcome = result.outcome;
        lastHitZoneId = result.zoneId;
        lastHitArmorMm = result.armorMm;
        lastHitEffectiveArmorMm = result.effectiveArmorMm;
        lastHitImpactAngleDeg = result.impactAngleDeg;
        lastHitPenetrationMm = result.penetrationMm;

        string message = result.message;
        if (structureHp <= 0.001f)
        {
            message += " Корпус разрушен, корабль уничтожен.";
        }

        LogEvent(message);
    }

    public ArmorZone FindZoneById(string zoneId)
    {
        ArmorZone[] zones = GetComponentsInChildren<ArmorZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null && zones[i].zoneId == zoneId)
            {
                return zones[i];
            }
        }

        return null;
    }

    public ArmorZone GetDefaultZone()
    {
        ArmorZone[] zones = GetComponentsInChildren<ArmorZone>();
        return zones.Length > 0 ? zones[0] : null;
    }

    public Vector3 GetAimPoint()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            return transform.position;
        }

        Bounds bounds = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
        {
            bounds.Encapsulate(colliders[i].bounds);
        }

        return bounds.center;
    }

    private void ResolveArmorPiercingHit(
        ArmorSurface surface,
        DamageHitContext context,
        float effectiveArmor,
        float rawAngle,
        ref DamageHitResult result)
    {
        if (surface.armorMm > 0.001f && rawAngle >= surface.ricochetAngleDeg)
        {
            ricochetCount++;
            result.outcome = DamageHitOutcome.Ricochet;
            result.message = $"[Урон] Рикошет: {context.shellName} от {surface.displayNameRu}.";
            return;
        }

        if (context.penetrationMm + 0.001f < effectiveArmor)
        {
            noPenetrationCount++;
            result.outcome = DamageHitOutcome.NoPenetration;
            result.message = $"[Урон] Непробитие: {context.shellName} в {surface.displayNameRu}, пробитие {context.penetrationMm:0} мм против {effectiveArmor:0} мм.";
            return;
        }

        penetrationCount++;
        float hullDamage = ApplyHullDamage(context.hullDamageOnPenetration * Mathf.Max(0f, surface.structureDamageMultiplier));
        result.structureDamage = hullDamage;
        result.outcome = DamageHitOutcome.Penetration;
        result.message = $"[Урон] Пробитие: {context.shellName} пробил {surface.displayNameRu}, корпус -{hullDamage:0.0}.";
    }

    private static string AppendArmorTelemetry(string message, DamageHitContext context, DamageHitResult result)
    {
        if (context.shellType == DamageShellType.Impact)
        {
            return $"{message} Угол {result.impactAngleDeg:0}°, броня {result.armorMm:0}->{result.effectiveArmorMm:0} мм.";
        }

        return $"{message} Угол {result.impactAngleDeg:0}°, броня {result.armorMm:0}->{result.effectiveArmorMm:0} мм, пробитие {result.penetrationMm:0} мм.";
    }

    private void ResolveHighExplosiveHit(
        ArmorSurface surface,
        DamageHitContext context,
        float effectiveArmor,
        ref DamageHitResult result)
    {
        if (context.penetrationMm >= effectiveArmor)
        {
            penetrationCount++;
            float hullDamage = ApplyHullDamage(context.hullDamageOnPenetration * Mathf.Max(0f, surface.structureDamageMultiplier));
            result.structureDamage = hullDamage;
            result.outcome = DamageHitOutcome.Penetration;
            result.message = $"[Урон] Фугас пробил {surface.displayNameRu}: корпус -{hullDamage:0.0}.";
            return;
        }

        explosiveSplashCount++;
        result.outcome = DamageHitOutcome.ExplosiveSplash;
        result.message = $"[Урон] Фугас разорвался на {surface.displayNameRu}: броня удержала.";
    }

    private void ResolveImpactHit(
        ArmorSurface surface,
        DamageHitContext context,
        ref DamageHitResult result)
    {
        impactCount++;
        float sourceMultiplier = context.impactSourceDamageMultiplier > 0.001f
            ? context.impactSourceDamageMultiplier
            : 1f;
        float damageScale = context.impactDamagePerKJ > 0.001f ? context.impactDamagePerKJ : 10f;
        float totalDamage = Mathf.Sqrt(Mathf.Max(0f, context.impactEnergyKJ))
            * damageScale
            * sourceMultiplier
            * Mathf.Max(0f, ramDamageTakenMultiplier)
            * Mathf.Max(0f, surface.ramDamageMultiplier);

        result.structureDamage = ApplyHullDamage(totalDamage);
        result.outcome = DamageHitOutcome.ImpactDamage;
        result.message = $"[Урон] Таран/удар в {surface.displayNameRu}: скорость {context.impactSpeedMS:0.0} м/с, массы {context.impactSourceMassKg:0}/{context.impactTargetMassKg:0} кг, энергия {context.impactEnergyKJ:0.0} кДж, урон {totalDamage:0.0}: корпус -{result.structureDamage:0.0}.";
    }

    private float ApplyHullDamage(float amount)
    {
        float previous = structureHp;
        structureHp = Mathf.Clamp(structureHp - Mathf.Max(0f, amount), 0f, maxStructureHp);
        return previous - structureHp;
    }

    private void LogEvent(string message)
    {
        lastDamageMessage = message;
        if (recentEvents == null)
        {
            recentEvents = new List<string>();
        }

        recentEvents.Insert(0, message);
        while (recentEvents.Count > maxRecentEvents)
        {
            recentEvents.RemoveAt(recentEvents.Count - 1);
        }

        if (debugLogging)
        {
            Debug.Log(message, this);
        }
    }
}
