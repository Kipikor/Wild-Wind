using System;
using UnityEngine;

public enum DamageMuzzleAxis
{
    LocalForward,
    LocalUp,
    LocalRight,
    NegativeLocalForward,
    NegativeLocalUp,
    NegativeLocalRight
}

public class DamageTestBench : MonoBehaviour
{
    [Header("Цель")]
    public DamageableShip target;
    public Transform muzzle;
    public DamageMuzzleAxis muzzleAxis = DamageMuzzleAxis.LocalUp;
    public float maxRangeMeters = 400f;

    [Header("Стрельба")]
    public bool spawnPhysicalProjectilesInPlayMode = true;
    public float muzzleVelocityMS = 160f;
    public float projectileMassKg = 8f;
    public float projectileRadiusMeters = 0.25f;
    public float obliqueShotSideOffsetMeters = 170f;
    public float obliqueShotForwardOffsetMeters = 120f;
    public DamageShellPreset armorPiercingShell = new DamageShellPreset
    {
        displayNameRu = "ББ 76 мм",
        shellType = DamageShellType.ArmorPiercing,
        caliberMm = 76f,
        damagePoints = 120f,
        hullDamageOnPenetration = 120f,
        armorPlateDamage = 25f,
        moduleDamage = 80f,
        penetrationMm = 80f,
        normalizationDegrees = 5f,
        penetrationRollSpread = 0.1f,
        projectileColor = Color.red
    };
    public DamageShellPreset highExplosiveShell = new DamageShellPreset
    {
        displayNameRu = "Фугас 90 мм",
        shellType = DamageShellType.HighExplosive,
        caliberMm = 90f,
        damagePoints = 150f,
        hullDamageOnPenetration = 85f,
        armorPlateDamage = 55f,
        moduleDamage = 35f,
        penetrationMm = 28f,
        explosiveRadiusMeters = 6f,
        normalizationDegrees = 0f,
        penetrationRollSpread = 0.05f,
        projectileColor = new Color(1f, 0.75f, 0.1f)
    };

    [Header("Таран")]
    public string ramZoneId = "front";
    public float rammerMassKg = 2200f;
    public float ramTargetMassKg = 1800f;
    public float ramRelativeSpeedMS = 12f;
    public float ramDamagePerKJ = 0.08f;

    [Header("Отладка")]
    public bool debugLogging = true;
    public bool drawAimRayInScene = true;
    public bool drawAimRayOnlyWhenSelected = false;
    public float aimRayHitMarkerRadius = 1.25f;
    public Color aimRayArmorHitColor = new Color(0.2f, 1f, 0.25f, 1f);
    public Color aimRayOtherHitColor = new Color(1f, 0.85f, 0.1f, 1f);
    public Color aimRayMissColor = new Color(1f, 0.15f, 0.1f, 1f);
    public Color aimRayObliqueColor = new Color(0.2f, 0.7f, 1f, 0.65f);
    public bool drawObliqueShotRays = true;
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

            DamageableModuleHitbox moduleHitbox = hit.collider.GetComponentInParent<DamageableModuleHitbox>();
            if (moduleHitbox != null)
            {
                if (!moduleHitbox.BlocksProjectile) continue;

                DamageHitResult moduleResult = moduleHitbox.ReceiveDirectHit(context);
                lastMessage = moduleResult.message;
                return moduleResult;
            }

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
            message = "[Урон] Промах: на линии огня не найден бронелист или модуль."
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

        ArmorZone zone = target.FindZoneById(ramZoneId);
        if (zone == null)
        {
            zone = target.GetDefaultZone();
        }

        if (zone == null)
        {
            Log("У цели нет бронезон для тарана.");
            return;
        }

        float reducedMass = rammerMassKg * ramTargetMassKg / Mathf.Max(1f, rammerMassKg + ramTargetMassKg);
        float energyKJ = 0.5f * reducedMass * ramRelativeSpeedMS * ramRelativeSpeedMS / 1000f;
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
            impactDamagePerKJ = ramDamagePerKJ,
            hitPoint = zone.transform.position,
            hitNormal = -incoming,
            incomingDirection = incoming,
            velocity = incoming * ramRelativeSpeedMS
        };

        DamageHitResult result = zone.ReceiveHit(context);
        lastMessage = result.message;
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
            armorPlateDamage = preset.armorPlateDamage,
            moduleDamage = preset.moduleDamage,
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

        Rigidbody projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.mass = Mathf.Max(0.01f, projectileMassKg);
        projectileBody.useGravity = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectileBody.linearVelocity = direction * Mathf.Max(1f, muzzleVelocityMS);

        DamageProjectile projectile = projectileObject.AddComponent<DamageProjectile>();
        projectile.shell = CopyPreset(preset);
        projectile.sourceName = name;
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

            DamageableModuleHitbox moduleHitbox = hit.collider.GetComponentInParent<DamageableModuleHitbox>();
            if (moduleHitbox != null && !moduleHitbox.BlocksProjectile) continue;

            end = hit.point;
            ArmorZone zone = hit.collider.GetComponentInParent<ArmorZone>();
            PaintedArmorBody paintedArmor = hit.collider.GetComponentInParent<PaintedArmorBody>();
            MeshArmorBody meshArmor = hit.collider.GetComponentInParent<MeshArmorBody>();
            rayColor = mainRay
                ? (zone != null || paintedArmor != null || meshArmor != null || moduleHitbox != null ? aimRayArmorHitColor : aimRayOtherHitColor)
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
            armorPlateDamage = preset.armorPlateDamage,
            moduleDamage = preset.moduleDamage,
            penetrationMm = preset.penetrationMm,
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
