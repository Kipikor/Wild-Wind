using System;
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
    [InspectorName("Модули")]
    [Tooltip("Состояние внутренних и внешних модулей, которые могут получать урон.")]
    public List<ShipDamageModuleState> modules = new List<ShipDamageModuleState>();

    [Header("Связь с физикой")]
    [InspectorName("Влиять повреждениями на полёт")]
    [Tooltip("Если включено, повреждения двигателя, винта и контура временно меняют характеристики ShipPhysics.")]
    public bool applyModuleEffectsToShipPhysics = true;
    [InspectorName("Физика корабля")]
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
    public bool debugLogging = true;
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

    private float baseEnginePowerKwAt100;
    private float basePropellerMaxThrustKgf;
    private float basePropellerMaxSpeedMS;
    private float baseClaudiumMaxLiftKg;
    private float baseClaudiumLiftEfficiency;
    private bool baselinesCaptured;

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
        EnsureDefaultModules();
        ResetDamageState();
    }

    private void Awake()
    {
        if (shipPhysics == null)
        {
            shipPhysics = GetComponent<ShipPhysics>();
        }

        EnsureDefaultModules();
        CaptureShipPhysicsBaselines();
    }

    private void OnValidate()
    {
        maxStructureHp = Mathf.Max(1f, maxStructureHp);
        structureHp = Mathf.Clamp(structureHp, 0f, maxStructureHp);
        ramDamageTakenMultiplier = Mathf.Max(0f, ramDamageTakenMultiplier);
        ramDamageDealtMultiplier = Mathf.Max(0f, ramDamageDealtMultiplier);
        maxRecentEvents = Mathf.Clamp(maxRecentEvents, 1, 20);
    }

    public void EnsureDefaultModules()
    {
        if (modules == null)
        {
            modules = new List<ShipDamageModuleState>();
        }

        EnsureModule("hull", "Корпус", 220f, 0.6f);
        EnsureModule("engine", "Двигатель", 120f, 1f);
        EnsureModule("propeller", "Винт", 90f, 1f);
        EnsureModule("claudium_loop", "Клавдиевый контур", 130f, 1f);
        EnsureModule("cargo", "Грузовой отсек", 80f, 0.8f);
    }

    public void ResetDamageState()
    {
        structureHp = Mathf.Max(1f, maxStructureHp);
        EnsureDefaultModules();

        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] != null)
            {
                modules[i].ResetHp();
            }
        }

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

        CaptureShipPhysicsBaselines();
        ApplyModuleEffects();
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
        CaptureShipPhysicsBaselines();

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
            ResolveHighExplosiveHit(surface, context, effectiveArmor, rawAngle, ref result);
        }
        else
        {
            ResolveImpactHit(surface, context, rawAngle, ref result);
        }

        result.remainingStructureHp = structureHp;
        result.message = AppendArmorTelemetry(result.message, context, result);
        if (!context.deferResultLogging)
        {
            FinalizeHitResult(result);
        }

        return result;
    }

    public DamageHitResult ApplyExternalModuleHit(DamageableModuleHitbox moduleHitbox, DamageHitContext context)
    {
        hitCount++;
        CaptureShipPhysicsBaselines();

        string moduleId = moduleHitbox != null ? moduleHitbox.moduleId : "";
        string moduleName = moduleHitbox != null ? moduleHitbox.displayNameRu : "модуль";
        bool externalModule = moduleHitbox != null && moduleHitbox.externalModule;
        string moduleKind = externalModule ? "внешний модуль" : "модуль";
        float moduleDamageMultiplier = externalModule ? 2f : 1f;
        float rawModuleDamage = context.moduleDamage * moduleDamageMultiplier;
        float moduleDamage = moduleHitbox != null ? moduleHitbox.ApplyModuleDamage(rawModuleDamage) : 0f;
        string multiplierText = externalModule ? " x2 за внешний модуль" : "";

        DamageHitResult result = new DamageHitResult
        {
            outcome = DamageHitOutcome.ModuleHit,
            message = $"[Урон] Прямое попадание в {moduleKind} {moduleName}: модуль -{moduleDamage:0.0}{multiplierText}, корпус не задет.",
            zoneId = moduleId,
            moduleId = moduleId,
            moduleNameRu = moduleName,
            structureDamage = 0f,
            moduleDamage = moduleDamage,
            remainingStructureHp = structureHp
        };

        FinalizeHitResult(result);
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
        ApplyModuleEffects();

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

    public ShipDamageModuleState GetModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId) || modules == null) return null;

        for (int i = 0; i < modules.Count; i++)
        {
            ShipDamageModuleState module = modules[i];
            if (module != null && module.moduleId == moduleId)
            {
                return module;
            }
        }

        return null;
    }

    public ShipDamageModuleState GetOrCreateModule(string moduleId, string nameRu, float maxHp, float damageWeight)
    {
        ShipDamageModuleState module = GetModule(moduleId);
        if (module != null) return module;

        module = new ShipDamageModuleState
        {
            moduleId = moduleId,
            displayNameRu = string.IsNullOrWhiteSpace(nameRu) ? moduleId : nameRu,
            maxHp = Mathf.Max(1f, maxHp),
            hp = Mathf.Max(1f, maxHp),
            damageWeight = Mathf.Max(0f, damageWeight)
        };
        modules.Add(module);
        return module;
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
        float hullDamage = ApplyHullDamage(context.hullDamageOnPenetration);
        result.structureDamage = hullDamage;
        TryDamageFirstInternalModule(context, ref result);
        result.outcome = DamageHitOutcome.Penetration;
        result.message = result.moduleDamage > 0.001f
            ? $"[Урон] Пробитие: {context.shellName} пробил {surface.displayNameRu}, корпус -{hullDamage:0.0}, модуль {result.moduleNameRu} -{result.moduleDamage:0.0}."
            : $"[Урон] Пробитие: {context.shellName} пробил {surface.displayNameRu}, корпус -{hullDamage:0.0}, модуль не задет.";
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
        float rawAngle,
        ref DamageHitResult result)
    {
        if (context.penetrationMm >= effectiveArmor)
        {
            penetrationCount++;
            float hullDamage = ApplyHullDamage(context.hullDamageOnPenetration);
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
        float rawAngle,
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

        int damagedModules = ApplyDistributedImpactDamage(totalDamage, ref result);
        result.outcome = DamageHitOutcome.ImpactDamage;
        string moduleText = damagedModules > 0
            ? $"модули -{result.moduleDamage:0.0} по {damagedModules} целым"
            : "живых модулей нет";
        result.message = $"[Урон] Таран/удар в {surface.displayNameRu}: скорость {context.impactSpeedMS:0.0} м/с, массы {context.impactSourceMassKg:0}/{context.impactTargetMassKg:0} кг, энергия {context.impactEnergyKJ:0.0} кДж, урон {totalDamage:0.0}: корпус -{result.structureDamage:0.0}, {moduleText}.";
    }

    private float ApplyHullDamage(float amount)
    {
        float previous = structureHp;
        structureHp = Mathf.Clamp(structureHp - Mathf.Max(0f, amount), 0f, maxStructureHp);
        return previous - structureHp;
    }

    private int ApplyDistributedImpactDamage(float totalDamage, ref DamageHitResult result)
    {
        totalDamage = Mathf.Max(0f, totalDamage);
        List<ShipDamageModuleState> aliveModules = new List<ShipDamageModuleState>();
        if (modules != null)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                ShipDamageModuleState module = modules[i];
                if (module == null || module.IsDestroyed || IsHullModule(module)) continue;
                aliveModules.Add(module);
            }
        }

        float hullDamage = aliveModules.Count > 0 ? totalDamage * 0.5f : totalDamage;
        float modulePool = totalDamage - hullDamage;
        result.structureDamage = ApplyHullDamage(hullDamage);

        if (aliveModules.Count <= 0 || modulePool <= 0.001f)
        {
            result.moduleDamage = 0f;
            return 0;
        }

        float damagePerModule = modulePool / aliveModules.Count;
        float appliedModuleDamage = 0f;
        for (int i = 0; i < aliveModules.Count; i++)
        {
            appliedModuleDamage += ApplyModuleRawDamage(aliveModules[i], damagePerModule);
        }

        result.moduleDamage = appliedModuleDamage;
        result.moduleNameRu = $"{aliveModules.Count} мод.";
        return aliveModules.Count;
    }

    private static bool IsHullModule(ShipDamageModuleState module)
    {
        return module != null && string.Equals(module.moduleId, "hull", StringComparison.OrdinalIgnoreCase);
    }

    private static float ApplyModuleRawDamage(ShipDamageModuleState module, float amount)
    {
        if (module == null) return 0f;

        float previous = module.hp;
        module.hp = Mathf.Max(0f, module.hp - Mathf.Max(0f, amount));
        return Mathf.Max(0f, previous - module.hp);
    }

    private void TryDamageFirstInternalModule(DamageHitContext context, ref DamageHitResult result)
    {
        if (context.moduleDamage <= 0.001f) return;

        Vector3 direction = context.incomingDirection.sqrMagnitude > 0.001f
            ? context.incomingDirection.normalized
            : Vector3.forward;

        DamageableModuleHitbox module = FindFirstModuleOnPath(
            context.hitPoint + direction * 0.05f,
            direction,
            Mathf.Max(0.1f, context.internalTravelDistance),
            false);

        if (module == null) return;

        float applied = module.ApplyModuleDamage(context.moduleDamage);
        result.moduleDamage = applied;
        result.moduleId = module.moduleId;
        result.moduleNameRu = module.displayNameRu;
    }

    private DamageableModuleHitbox FindFirstModuleOnPath(Vector3 origin, Vector3 direction, float distance, bool includeExternal)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Collide);
        DamageableModuleHitbox best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            DamageableModuleHitbox module = hits[i].collider != null ? hits[i].collider.GetComponentInParent<DamageableModuleHitbox>() : null;
            DamageableShip moduleOwner = module != null && module.owner != null
                ? module.owner
                : (module != null ? module.GetComponentInParent<DamageableShip>() : null);
            if (module == null || moduleOwner != this || !module.BlocksProjectile) continue;
            if (!includeExternal && module.externalModule) continue;

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                best = module;
            }
        }

        return best;
    }

    private void CaptureShipPhysicsBaselines()
    {
        if (baselinesCaptured) return;
        if (shipPhysics == null) return;

        baseEnginePowerKwAt100 = shipPhysics.enginePowerKwAt100;
        basePropellerMaxThrustKgf = shipPhysics.propellerMaxThrustKgf;
        basePropellerMaxSpeedMS = shipPhysics.propellerMaxSpeedMS;
        baseClaudiumMaxLiftKg = shipPhysics.claudiumMaxLiftKg;
        baseClaudiumLiftEfficiency = shipPhysics.claudiumLiftEfficiency;
        baselinesCaptured = true;
    }

    private void ApplyModuleEffects()
    {
        if (!applyModuleEffectsToShipPhysics || shipPhysics == null || !baselinesCaptured) return;

        ShipDamageModuleState engine = GetModule("engine");
        ShipDamageModuleState propeller = GetModule("propeller");
        ShipDamageModuleState claudiumLoop = GetModule("claudium_loop");

        float engineFactor = DamageToPerformanceFactor(engine, 0.12f);
        float propellerFactor = DamageToPerformanceFactor(propeller, 0.18f);
        float claudiumFactor = DamageToPerformanceFactor(claudiumLoop, 0.1f);

        shipPhysics.enginePowerKwAt100 = baseEnginePowerKwAt100 * engineFactor;
        shipPhysics.propellerMaxThrustKgf = basePropellerMaxThrustKgf * propellerFactor;
        shipPhysics.propellerMaxSpeedMS = basePropellerMaxSpeedMS * Mathf.Lerp(0.35f, 1f, propellerFactor);
        shipPhysics.claudiumMaxLiftKg = baseClaudiumMaxLiftKg * claudiumFactor;
        shipPhysics.claudiumLiftEfficiency = baseClaudiumLiftEfficiency * Mathf.Lerp(0.45f, 1f, claudiumFactor);
        shipPhysics.RefreshRuntimeShipSettings();
    }

    private static float DamageToPerformanceFactor(ShipDamageModuleState module, float destroyedFloor)
    {
        if (module == null) return 1f;
        return Mathf.Lerp(Mathf.Clamp01(destroyedFloor), 1f, module.HpRatio);
    }

    private void EnsureModule(string moduleId, string nameRu, float maxHp, float damageWeight)
    {
        ShipDamageModuleState module = GetModule(moduleId);
        if (module != null) return;

        modules.Add(new ShipDamageModuleState
        {
            moduleId = moduleId,
            displayNameRu = nameRu,
            maxHp = maxHp,
            hp = maxHp,
            damageWeight = damageWeight
        });
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
