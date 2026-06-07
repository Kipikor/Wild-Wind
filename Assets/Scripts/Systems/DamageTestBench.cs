using System;
using UnityEngine;

public enum DamageMuzzleAxis
{
    [InspectorName("Локально вперёд")]
    LocalForward,
    [InspectorName("Локально вверх")]
    LocalUp,
    [InspectorName("Локально вправо")]
    LocalRight,
    [InspectorName("Локально назад")]
    NegativeLocalForward,
    [InspectorName("Локально вниз")]
    NegativeLocalUp,
    [InspectorName("Локально влево")]
    NegativeLocalRight
}

public class DamageTestBench : MonoBehaviour
{
    [Header("Цель")]
    [InspectorName("Цель")]
    [Tooltip("Корабль или бронекорпус, по которому стреляет тестовый стенд.")]
    public DamageableShip target;
    [InspectorName("Ствол")]
    [Tooltip("Точка, из которой выходит снаряд и откуда рисуется прицельный луч.")]
    public Transform muzzle;
    [InspectorName("Ось ствола")]
    [Tooltip("Какая локальная ось объекта ствола считается направлением выстрела.")]
    public DamageMuzzleAxis muzzleAxis = DamageMuzzleAxis.LocalUp;
    [InspectorName("Дальность стрельбы, м")]
    [Tooltip("Максимальная длина тестового луча и дальность raycast-выстрела.")]
    public float maxRangeMeters = 400f;

    [Header("Стрельба")]
    [InspectorName("Создавать физические снаряды в Play Mode")]
    [Tooltip("Если включено, в Play Mode выстрел создаёт настоящий Rigidbody-снаряд. В редакторе кнопки используют быстрый raycast.")]
    public bool spawnPhysicalProjectilesInPlayMode = true;
    [InspectorName("Начальная скорость снаряда, м/с")]
    public float muzzleVelocityMS = 160f;
    [InspectorName("Масса снаряда, кг")]
    public float projectileMassKg = 8f;
    [InspectorName("Радиус снаряда, м")]
    public float projectileRadiusMeters = 0.25f;
    [InspectorName("Боковое смещение косого выстрела, м")]
    [Tooltip("Насколько вбок от цели ставится виртуальная пушка для кнопок косого ББ-выстрела.")]
    public float obliqueShotSideOffsetMeters = 170f;
    [InspectorName("Продольное смещение косого выстрела, м")]
    [Tooltip("Насколько назад от цели ставится виртуальная пушка для кнопок косого ББ-выстрела.")]
    public float obliqueShotForwardOffsetMeters = 120f;
    [InspectorName("Бронебойный снаряд")]
    public DamageShellPreset armorPiercingShell = new DamageShellPreset
    {
        displayNameRu = "ББ 76 мм",
        shellType = DamageShellType.ArmorPiercing,
        caliberMm = 76f,
        damagePoints = 120f,
        hullDamageOnPenetration = 120f,
        penetrationMm = 80f,
        normalizationDegrees = 5f,
        penetrationRollSpread = 0.1f,
        projectileColor = Color.red
    };
    [InspectorName("Фугасный снаряд")]
    public DamageShellPreset highExplosiveShell = new DamageShellPreset
    {
        displayNameRu = "Фугас 90 мм",
        shellType = DamageShellType.HighExplosive,
        caliberMm = 90f,
        damagePoints = 150f,
        hullDamageOnPenetration = 85f,
        penetrationMm = 28f,
        explosiveRadiusMeters = 6f,
        normalizationDegrees = 0f,
        penetrationRollSpread = 0.05f,
        projectileColor = new Color(1f, 0.75f, 0.1f)
    };
    [Header("Фугасный толчок")]
    [InspectorName("Масштаб импульса фугаса")]
    [Tooltip("Импульс фугаса = урон снаряда * этот масштаб. При пробитии брони импульс утраивается.")]
    [Min(0f)]
    public float highExplosiveImpulseScale = 25f;
    [InspectorName("Макс. Δv от фугаса, м/с")]
    [Min(0f)]
    public float highExplosiveMaxTargetDeltaVelocityMS = 8f;

    [Header("Таран")]
    [InspectorName("Бронезона тарана")]
    public string ramZoneId = "front";
    [InspectorName("Масса таранящего объекта, кг")]
    [Min(1f)]
    public float rammerMassKg = 2200f;
    [InspectorName("Масса цели, кг")]
    [Min(1f)]
    public float ramTargetMassKg = 1800f;
    [InspectorName("Скорость удара, м/с")]
    [Min(0f)]
    public float ramRelativeSpeedMS = 12f;
    [InspectorName("Минимальная скорость урона, м/с")]
    [Min(0f)]
    public float ramMinDamageSpeedMS = 4f;
    [InspectorName("Масштаб урона тарана")]
    [Tooltip("Урон считается как sqrt(энергия удара в кДж) * этот масштаб.")]
    [Min(0f)]
    public float ramDamageScale = 10f;
    [InspectorName("Модификатор урона таранящего")]
    [Min(0f)]
    public float rammerDamageMultiplier = 1f;
    [InspectorName("Упругость толчка")]
    [Range(0f, 1f)]
    public float ramPushElasticity = 0.45f;
    [InspectorName("Макс. скорость толчка цели")]
    [Min(0f)]
    public float ramMaxTargetDeltaVelocityMS = 16f;
    [InspectorName("Цель закреплена для теста")]
    [Tooltip("Если включено, урон считается, но физический толчок не применяется. Удобно для повторных тестов.")]
    public bool ramKeepTargetAnchored = true;

    [Header("Отладка")]
    [InspectorName("Писать логи")]
    public bool debugLogging = true;
    [InspectorName("Показывать прицельный луч в сцене")]
    public bool drawAimRayInScene = true;
    [InspectorName("Показывать луч только при выборе")]
    public bool drawAimRayOnlyWhenSelected = false;
    [InspectorName("Радиус маркера попадания")]
    public float aimRayHitMarkerRadius = 1.25f;
    [InspectorName("Цвет попадания в броню")]
    public Color aimRayArmorHitColor = new Color(0.2f, 1f, 0.25f, 1f);
    [InspectorName("Цвет попадания в другой объект")]
    public Color aimRayOtherHitColor = new Color(1f, 0.85f, 0.1f, 1f);
    [InspectorName("Цвет промаха")]
    public Color aimRayMissColor = new Color(1f, 0.15f, 0.1f, 1f);
    [InspectorName("Цвет косого луча")]
    public Color aimRayObliqueColor = new Color(0.2f, 0.7f, 1f, 0.65f);
    [InspectorName("Показывать косые лучи")]
    public bool drawObliqueShotRays = true;
    [InspectorName("Последнее сообщение")]
    [TextArea(2, 5)]
    public string lastMessage = "";

    private void Reset()
    {
        target = FindFirstObjectByType<DamageableShip>();
        if (muzzle == null)
        {
            muzzle = transform;
        }
    }

    public void FireArmorPiercing()
    {
        FireShell(armorPiercingShell);
    }

    public void FireHighExplosive()
    {
        FireShell(highExplosiveShell);
    }

    public void FireArmorPiercingObliqueLeft()
    {
        FireShellFromTargetOffset(armorPiercingShell, new Vector3(-obliqueShotSideOffsetMeters, 0f, -obliqueShotForwardOffsetMeters));
    }

    public void FireArmorPiercingObliqueRight()
    {
        FireShellFromTargetOffset(armorPiercingShell, new Vector3(obliqueShotSideOffsetMeters, 0f, -obliqueShotForwardOffsetMeters));
    }

    public void FireShell(DamageShellPreset preset)
    {
        if (preset == null)
        {
            Log("Нет пресета снаряда.");
            return;
        }

        if (target == null)
        {
            target = FindFirstObjectByType<DamageableShip>();
        }

        if (target == null)
        {
            Log("Нет цели DamageableShip.");
            return;
        }

        Transform actualMuzzle = muzzle != null ? muzzle : transform;
        Vector3 origin = actualMuzzle.position;
        Vector3 direction = GetMuzzleForward(actualMuzzle, muzzleAxis);

        if (Application.isPlaying && spawnPhysicalProjectilesInPlayMode)
        {
            PrepareTargetForProjectilePhysics(preset);
            SpawnProjectile(preset, origin, direction);
            Log("Выстрел: " + preset.displayNameRu + " по " + target.displayNameRu + ".");
            return;
        }

        FireRaycast(preset, origin, direction);
    }

    public void FireShellFromTargetOffset(DamageShellPreset preset, Vector3 localTargetOffset)
    {
        if (preset == null)
        {
            Log("Нет пресета снаряда.");
            return;
        }

        if (target == null)
        {
            target = FindFirstObjectByType<DamageableShip>();
        }

        if (target == null)
        {
            Log("Нет цели DamageableShip.");
            return;
        }

        Vector3 aimPoint = target.GetAimPoint();
        Vector3 origin = aimPoint + target.transform.TransformDirection(localTargetOffset);
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = target.transform.forward;
        }
        direction.Normalize();

        if (Application.isPlaying && spawnPhysicalProjectilesInPlayMode)
        {
            PrepareTargetForProjectilePhysics(preset);
            SpawnProjectile(preset, origin, direction);
            Log("Косой выстрел: " + preset.displayNameRu + " по " + target.displayNameRu + ".");
            return;
        }

        FireRaycast(preset, origin, direction);
    }

    public DamageHitResult FireRaycast(DamageShellPreset preset, Vector3 origin, Vector3 direction)
    {
        Physics.SyncTransforms();
        float range = Mathf.Max(1f, maxRangeMeters);
        Vector3 shotDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        Ray ray = new Ray(origin, shotDirection);
        RaycastHit[] hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Collide);
        if (hits.Length == 0)
        {
            DamageHitResult miss = new DamageHitResult
            {
                outcome = DamageHitOutcome.Miss,
                message = "[Урон] Промах: луч не задел цель."
            };
            Log(miss.message);
            return miss;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null) continue;

            DamageHitContext context = CreateHitContext(preset, hit.point, hit.normal, shotDirection);

            PaintedArmorBody paintedArmor = hit.collider.GetComponentInParent<PaintedArmorBody>();
            if (paintedArmor != null)
            {
                DamageHitResult paintedResult = paintedArmor.ReceiveHit(context, hit);
                lastMessage = paintedResult.message;
                return paintedResult;
            }

            MeshArmorBody meshArmor = hit.collider.GetComponentInParent<MeshArmorBody>();
            if (meshArmor != null)
            {
                DamageHitResult meshResult = meshArmor.ReceiveHit(context, hit);
                lastMessage = meshResult.message;
                return meshResult;
            }

            ArmorZone zone = hit.collider.GetComponentInParent<ArmorZone>();
            DamageableShip ship = hit.collider.GetComponentInParent<DamageableShip>();
            if (zone == null && ship != null)
            {
                zone = ship.GetDefaultZone();
            }

            if (zone == null) continue;

            DamageHitResult result = zone.ReceiveHit(context);
            lastMessage = result.message;
            return result;
        }

        DamageHitResult noDamage = new DamageHitResult
        {
            outcome = DamageHitOutcome.Miss,
            message = "[Урон] Промах: на линии огня не найден бронелист."
        };
        Log(noDamage.message);
        return noDamage;
    }

    public void SimulateRam()
    {
        if (target == null)
        {
            target = FindFirstObjectByType<DamageableShip>();
        }

        if (target == null)
        {
            Log("Нет цели для тарана.");
            return;
        }

        float speed = Mathf.Max(0f, ramRelativeSpeedMS);
        if (speed < ramMinDamageSpeedMS)
        {
            Log($"Таран слишком медленный: {speed:0.0} м/с меньше порога {ramMinDamageSpeedMS:0.0} м/с. Урон не считается.");
            return;
        }

        ApplyTargetTestMass();
        float reducedMass = rammerMassKg * ramTargetMassKg / Mathf.Max(1f, rammerMassKg + ramTargetMassKg);
        float energyKJ = 0.5f * reducedMass * speed * speed / 1000f;
        Vector3 incoming = (target.GetAimPoint() - (muzzle != null ? muzzle.position : transform.position)).normalized;
        if (incoming.sqrMagnitude < 0.001f)
        {
            incoming = target.transform.forward;
        }

        DamageHitContext context = new DamageHitContext
        {
            shellType = DamageShellType.Impact,
            shellName = "Тестовый таран",
            sourceName = name,
            impactEnergyKJ = energyKJ,
            impactDamagePerKJ = ramDamageScale,
            impactSpeedMS = speed,
            impactSourceMassKg = rammerMassKg,
            impactTargetMassKg = ramTargetMassKg,
            impactSourceDamageMultiplier = rammerDamageMultiplier,
            hitPoint = target.GetAimPoint(),
            hitNormal = -incoming,
            incomingDirection = incoming,
            velocity = incoming * speed
        };

        DamageHitResult result = ApplyRamHit(context, incoming);
        string pushMessage = ApplyRamPushToTarget(incoming, reducedMass, speed);
        lastMessage = string.IsNullOrWhiteSpace(pushMessage)
            ? result.message
            : result.message + " " + pushMessage;
    }

    private DamageHitResult ApplyRamHit(DamageHitContext context, Vector3 incoming)
    {
        ArmorZone zone = target.FindZoneById(ramZoneId);
        if (zone == null)
        {
            zone = target.GetDefaultZone();
        }

        if (zone != null)
        {
            context.hitPoint = zone.transform.position;
            context.hitNormal = -incoming;
            return zone.ReceiveHit(context);
        }

        MeshArmorBody meshArmor = FindTargetMeshArmor();
        if (meshArmor != null)
        {
            context.hitPoint = meshArmor.transform.position;
            context.hitNormal = -incoming;
            return meshArmor.ReceiveHit(context, -1);
        }

        DamageHitResult miss = new DamageHitResult
        {
            outcome = DamageHitOutcome.Miss,
            message = "[Урон] У цели нет ArmorZone или MeshArmorBody для тарана."
        };
        Log(miss.message);
        return miss;
    }

    private MeshArmorBody FindTargetMeshArmor()
    {
        if (target == null) return null;

        MeshArmorBody meshArmor = target.GetComponentInChildren<MeshArmorBody>(true);
        if (meshArmor != null) return meshArmor;

        MeshArmorBody[] armorBodies = FindObjectsByType<MeshArmorBody>(FindObjectsSortMode.None);
        MeshArmorBody fallback = null;
        for (int i = 0; i < armorBodies.Length; i++)
        {
            MeshArmorBody armor = armorBodies[i];
            if (armor == null) continue;

            if (armor.owner == target)
            {
                return armor;
            }

            DamageableShip parentOwner = armor.GetComponentInParent<DamageableShip>();
            if (parentOwner == target)
            {
                return armor;
            }

            if (fallback == null && armor.owner == null && armorBodies.Length == 1)
            {
                fallback = armor;
            }
        }

        return fallback;
    }

    public void ResetTargetDamage()
    {
        if (target == null)
        {
            target = FindFirstObjectByType<DamageableShip>();
        }

        if (target == null)
        {
            Log("Нет цели для сброса повреждений.");
            return;
        }

        target.ResetDamageState();
        lastMessage = target.lastDamageMessage;
    }

    private void ApplyTargetTestMass()
    {
        if (target == null) return;

        Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
        if (targetBody != null)
        {
            targetBody.mass = Mathf.Max(1f, ramTargetMassKg);
            targetBody.useGravity = false;
            if (ramKeepTargetAnchored)
            {
                if (!targetBody.isKinematic)
                {
                    targetBody.linearVelocity = Vector3.zero;
                    targetBody.angularVelocity = Vector3.zero;
                }

                targetBody.isKinematic = true;
            }
            else
            {
                targetBody.isKinematic = false;
            }
        }
    }

    private string ApplyRamPushToTarget(Vector3 incoming, float reducedMass, float speed)
    {
        float impulseNs = Mathf.Max(0f, reducedMass * speed * ramPushElasticity);
        float rawDeltaVelocity = impulseNs / Mathf.Max(1f, ramTargetMassKg);
        float deltaVelocity = Mathf.Min(rawDeltaVelocity, Mathf.Max(0f, ramMaxTargetDeltaVelocityMS));

        if (ramKeepTargetAnchored)
        {
            return $"Толчок рассчитан: импульс {impulseNs:0} Н·с, Δv {deltaVelocity:0.00} м/с, но цель закреплена.";
        }

        Rigidbody targetBody = target != null ? target.GetComponentInParent<Rigidbody>() : null;
        if (targetBody != null)
        {
            targetBody.isKinematic = false;
            targetBody.useGravity = false;
            if (Application.isPlaying)
            {
                targetBody.AddForce(incoming.normalized * Mathf.Min(impulseNs, deltaVelocity * ramTargetMassKg), ForceMode.Impulse);
                return $"Толчок применён: импульс {impulseNs:0} Н·с, Δv до {deltaVelocity:0.00} м/с.";
            }
        }

        if (target != null)
        {
            target.transform.position += incoming.normalized * Mathf.Min(5f, deltaVelocity * 0.25f);
            return $"Тестовый сдвиг цели применён в редакторе: Δv {deltaVelocity:0.00} м/с.";
        }

        return "";
    }

    private DamageHitContext CreateHitContext(DamageShellPreset preset, Vector3 hitPoint, Vector3 hitNormal, Vector3 direction)
    {
        Vector3 shotDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        return new DamageHitContext
        {
            shellType = preset.shellType,
            shellName = preset.displayNameRu,
            sourceName = name,
            caliberMm = preset.caliberMm,
            damagePoints = preset.damagePoints,
            hullDamageOnPenetration = preset.hullDamageOnPenetration > 0.001f ? preset.hullDamageOnPenetration : preset.damagePoints,
            penetrationMm = RollPenetration(preset),
            explosiveRadiusMeters = preset.explosiveRadiusMeters,
            normalizationDegrees = preset.normalizationDegrees,
            hitPoint = hitPoint,
            hitNormal = hitNormal,
            incomingDirection = shotDirection,
            velocity = shotDirection * muzzleVelocityMS
        };
    }

    private void SpawnProjectile(DamageShellPreset preset, Vector3 origin, Vector3 direction)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Damage Projectile " + preset.displayNameRu;
        projectileObject.transform.position = origin;
        projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, projectileRadiusMeters * 2f);

        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = preset.projectileColor;
        }

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        Rigidbody projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.mass = Mathf.Max(0.01f, projectileMassKg);
        projectileBody.useGravity = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Vector3 initialVelocity = direction * Mathf.Max(1f, muzzleVelocityMS);
        projectileBody.linearVelocity = initialVelocity;

        DamageProjectile projectile = projectileObject.AddComponent<DamageProjectile>();
        float lifetime = Mathf.Max(0.2f, maxRangeMeters / Mathf.Max(1f, muzzleVelocityMS) * 3f);
        projectile.Initialize(CopyPreset(preset), name, null, initialVelocity, Mathf.Max(1f, maxRangeMeters), lifetime, 1f);
        projectile.moveKinematicTargets = !ramKeepTargetAnchored;
        projectile.highExplosiveImpulseScale = highExplosiveImpulseScale;
        projectile.highExplosiveMaxDeltaVelocityMS = highExplosiveMaxTargetDeltaVelocityMS;
    }

    private void PrepareTargetForProjectilePhysics(DamageShellPreset preset)
    {
        if (target == null) return;

        Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
        if (targetBody == null) return;

        targetBody.mass = Mathf.Max(1f, ramTargetMassKg);
        targetBody.useGravity = false;

        if (ramKeepTargetAnchored)
        {
            if (!targetBody.isKinematic)
            {
                targetBody.linearVelocity = Vector3.zero;
                targetBody.angularVelocity = Vector3.zero;
            }

            targetBody.isKinematic = true;
            return;
        }

        if (preset != null && preset.shellType == DamageShellType.HighExplosive)
        {
            targetBody.isKinematic = false;
        }
    }

    private bool TryGetCurrentAimRay(out Vector3 origin, out Vector3 direction)
    {
        Transform actualMuzzle = muzzle != null ? muzzle : transform;
        origin = actualMuzzle.position;
        direction = GetMuzzleForward(actualMuzzle, muzzleAxis);
        return true;
    }

    private static Vector3 GetMuzzleForward(Transform actualMuzzle, DamageMuzzleAxis axis)
    {
        Vector3 direction = Vector3.forward;
        if (actualMuzzle != null)
        {
            switch (axis)
            {
                case DamageMuzzleAxis.LocalUp:
                    direction = actualMuzzle.up;
                    break;
                case DamageMuzzleAxis.LocalRight:
                    direction = actualMuzzle.right;
                    break;
                case DamageMuzzleAxis.NegativeLocalForward:
                    direction = -actualMuzzle.forward;
                    break;
                case DamageMuzzleAxis.NegativeLocalUp:
                    direction = -actualMuzzle.up;
                    break;
                case DamageMuzzleAxis.NegativeLocalRight:
                    direction = -actualMuzzle.right;
                    break;
                default:
                    direction = actualMuzzle.forward;
                    break;
            }
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = Vector3.forward;
        }

        direction.Normalize();
        return direction;
    }

    private void OnDrawGizmos()
    {
        if (!drawAimRayInScene || drawAimRayOnlyWhenSelected) return;

        DrawAimGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAimRayInScene) return;

        DrawAimGizmos();
    }

    private void DrawAimGizmos()
    {
        if (TryGetCurrentAimRay(out Vector3 origin, out Vector3 direction))
        {
            DrawSingleRay(origin, direction, true);
        }

        if (!drawObliqueShotRays || target == null) return;

        Vector3 aimPoint = target.GetAimPoint();
        Vector3 leftOrigin = aimPoint + target.transform.TransformDirection(new Vector3(-obliqueShotSideOffsetMeters, 0f, -obliqueShotForwardOffsetMeters));
        Vector3 rightOrigin = aimPoint + target.transform.TransformDirection(new Vector3(obliqueShotSideOffsetMeters, 0f, -obliqueShotForwardOffsetMeters));
        DrawSingleRay(leftOrigin, (aimPoint - leftOrigin).normalized, false);
        DrawSingleRay(rightOrigin, (aimPoint - rightOrigin).normalized, false);
    }

    private void DrawSingleRay(Vector3 origin, Vector3 direction, bool mainRay)
    {
        if (direction.sqrMagnitude < 0.001f) return;

        float length = Mathf.Max(1f, maxRangeMeters);
        Color rayColor = mainRay ? aimRayMissColor : aimRayObliqueColor;
        Vector3 end = origin + direction.normalized * length;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, length, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null) continue;

            end = hit.point;
            ArmorZone zone = hit.collider.GetComponentInParent<ArmorZone>();
            PaintedArmorBody paintedArmor = hit.collider.GetComponentInParent<PaintedArmorBody>();
            MeshArmorBody meshArmor = hit.collider.GetComponentInParent<MeshArmorBody>();
            rayColor = mainRay
                ? (zone != null || paintedArmor != null || meshArmor != null ? aimRayArmorHitColor : aimRayOtherHitColor)
                : aimRayObliqueColor;

            Gizmos.color = rayColor;
            Gizmos.DrawWireSphere(hit.point, Mathf.Max(0.05f, aimRayHitMarkerRadius));
            break;
        }

        Gizmos.color = rayColor;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(origin, Mathf.Max(0.05f, aimRayHitMarkerRadius * 0.45f));
    }

    private static DamageShellPreset CopyPreset(DamageShellPreset preset)
    {
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

    private static float RollPenetration(DamageShellPreset preset)
    {
        float spread = Mathf.Clamp01(preset.penetrationRollSpread);
        if (spread <= 0.001f) return preset.penetrationMm;

        return preset.penetrationMm * UnityEngine.Random.Range(1f - spread, 1f + spread);
    }

    private void Log(string message)
    {
        lastMessage = message;
        if (debugLogging)
        {
            Debug.Log("[Обстрел] " + message, this);
        }
    }
}
