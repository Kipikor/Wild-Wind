using System;
using UnityEngine;

public enum CoreTacticalDamageType
{
    Kinetic,
    Thermal,
    Chemical,
    Explosive
}

public enum CoreTacticalDamageModuleKind
{
    Engine,
    Citadel,
    MainBattery,
    SecondaryBattery,
    MachineGuns,
    Launcher
}

[Serializable]
public struct CoreTacticalResistanceSet
{
    [Range(0f, 100f)] public float kineticPercent;
    [Range(0f, 100f)] public float thermalPercent;
    [Range(0f, 100f)] public float chemicalPercent;
    [Range(0f, 100f)] public float explosivePercent;

    public CoreTacticalResistanceSet(float kinetic, float thermal, float chemical, float explosive)
    {
        kineticPercent = Mathf.Clamp(kinetic, 0f, 100f);
        thermalPercent = Mathf.Clamp(thermal, 0f, 100f);
        chemicalPercent = Mathf.Clamp(chemical, 0f, 100f);
        explosivePercent = Mathf.Clamp(explosive, 0f, 100f);
    }

    public float Get(CoreTacticalDamageType type)
    {
        switch (type)
        {
            case CoreTacticalDamageType.Thermal:
                return Mathf.Clamp(thermalPercent, 0f, 100f);
            case CoreTacticalDamageType.Chemical:
                return Mathf.Clamp(chemicalPercent, 0f, 100f);
            case CoreTacticalDamageType.Explosive:
                return Mathf.Clamp(explosivePercent, 0f, 100f);
            default:
                return Mathf.Clamp(kineticPercent, 0f, 100f);
        }
    }

    public void Normalize()
    {
        kineticPercent = Mathf.Clamp(kineticPercent, 0f, 100f);
        thermalPercent = Mathf.Clamp(thermalPercent, 0f, 100f);
        chemicalPercent = Mathf.Clamp(chemicalPercent, 0f, 100f);
        explosivePercent = Mathf.Clamp(explosivePercent, 0f, 100f);
    }
}

[Serializable]
public struct CoreTacticalDamageRequest
{
    public CoreTacticalDamageType damageType;
    public string source;
    public float damage;
    public float resistanceIgnorePercent;
    public float damageSpread;
    public float fireChancePercent;
    public float explosionRadiusMeters;
    public Vector3 hitPoint;
    public Vector3 incomingDirection;

    public static CoreTacticalDamageRequest Create(
        CoreTacticalDamageType type,
        float damage,
        string source,
        float resistanceIgnorePercent = 0f,
        float fireChancePercent = 0f,
        float explosionRadiusMeters = 0f,
        Vector3 hitPoint = default,
        Vector3 incomingDirection = default)
    {
        return new CoreTacticalDamageRequest
        {
            damageType = type,
            source = source,
            damage = Mathf.Max(0f, damage),
            resistanceIgnorePercent = Mathf.Max(0f, resistanceIgnorePercent),
            damageSpread = 0.25f,
            fireChancePercent = Mathf.Max(0f, fireChancePercent),
            explosionRadiusMeters = Mathf.Max(0f, explosionRadiusMeters),
            hitPoint = hitPoint,
            incomingDirection = incomingDirection
        };
    }

    public static CoreTacticalDamageRequest Kinetic(float damage, string source, float resistanceIgnorePercent = 0f)
    {
        return Create(CoreTacticalDamageType.Kinetic, damage, source, resistanceIgnorePercent);
    }

    public static CoreTacticalDamageRequest Thermal(float damage, string source, float resistanceIgnorePercent = 0f, float fireChancePercent = 0f)
    {
        return Create(CoreTacticalDamageType.Thermal, damage, source, resistanceIgnorePercent, fireChancePercent);
    }

    public static CoreTacticalDamageRequest Chemical(float damage, string source, float resistanceIgnorePercent = 0f)
    {
        return Create(CoreTacticalDamageType.Chemical, damage, source, resistanceIgnorePercent);
    }

    public static CoreTacticalDamageRequest Explosive(
        float damage,
        string source,
        float resistanceIgnorePercent = 0f,
        float fireChancePercent = 0f,
        float explosionRadiusMeters = 0f,
        Vector3 hitPoint = default,
        Vector3 incomingDirection = default)
    {
        return Create(
            CoreTacticalDamageType.Explosive,
            damage,
            source,
            resistanceIgnorePercent,
            fireChancePercent,
            explosionRadiusMeters,
            hitPoint,
            incomingDirection);
    }
}

public struct CoreTacticalDamageResolution
{
    public CoreTacticalDamageType damageType;
    public float rawDamage;
    public float rolledDamage;
    public float resistancePercent;
    public float resistanceIgnorePercent;
    public float effectiveResistancePercent;
    public float hullDamage;
    public bool fireStarted;
    public bool damageControlBlockedMalfunction;
    public bool citadelHit;
    public bool engineBroken;
    public bool moduleBroken;
    public string message;
}

[Serializable]
public class CoreTacticalDamageModuleState
{
    public CoreTacticalDamageModuleKind kind = CoreTacticalDamageModuleKind.Engine;
    public string displayName = "Module";
    public float maxHp = 100f;
    public float currentHp = 100f;
    public float repairSeconds = 10f;
    public bool broken;
    public float repairRemainingSeconds;

    public void Configure(CoreTacticalDamageModuleKind moduleKind, string moduleName, float hp, float repairTime)
    {
        kind = moduleKind;
        displayName = string.IsNullOrWhiteSpace(moduleName) ? moduleKind.ToString() : moduleName;
        maxHp = Mathf.Max(1f, hp);
        repairSeconds = Mathf.Max(0.1f, repairTime);
        ResetRuntimeState();
    }

    public void ResetRuntimeState()
    {
        maxHp = Mathf.Max(1f, maxHp);
        currentHp = maxHp;
        broken = false;
        repairRemainingSeconds = 0f;
    }

    public bool ApplyDamage(float amount)
    {
        currentHp = Mathf.Max(0f, currentHp - Mathf.Max(0f, amount));
        if (currentHp > 0f)
        {
            return false;
        }

        broken = true;
        currentHp = maxHp;
        repairRemainingSeconds = repairSeconds;
        return true;
    }

    public void RepairNow()
    {
        currentHp = maxHp;
        broken = false;
        repairRemainingSeconds = 0f;
    }

    public void TickRepair(float deltaSeconds)
    {
        if (!broken)
        {
            return;
        }

        repairRemainingSeconds = Mathf.Max(0f, repairRemainingSeconds - Mathf.Max(0f, deltaSeconds));
        if (repairRemainingSeconds <= 0f)
        {
            RepairNow();
        }
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalDamageProfile : MonoBehaviour
{
    [Header("Damage resistances")]
    public CoreTacticalResistanceSet resistances = new CoreTacticalResistanceSet(35f, 12f, 12f, 20f);
    [Range(1, 4)] public int fireSectorCount = 2;

    [Header("Fire")]
    public float fireDurationSeconds = 15f;
    public float fireDamagePerSecondMaxHealthFraction = 0.005f;

    [Header("Emergency team")]
    public bool automaticEmergencyTeam = true;
    public float emergencyTeamActivationDelaySeconds = 1f;
    public float emergencyTeamActiveSeconds = 8f;
    public float emergencyTeamCooldownSeconds = 55f;
    public bool emergencyTeamActive;
    public float emergencyTeamReadyTime;

    [Header("Modules")]
    public CoreTacticalDamageModuleState engine = new CoreTacticalDamageModuleState();
    public CoreTacticalDamageModuleState citadel = new CoreTacticalDamageModuleState();
    public CoreTacticalDamageModuleState mainBattery = new CoreTacticalDamageModuleState();
    public CoreTacticalDamageModuleState secondaryBattery = new CoreTacticalDamageModuleState();
    public CoreTacticalDamageModuleState machineGuns = new CoreTacticalDamageModuleState();
    public CoreTacticalDamageModuleState launcher = new CoreTacticalDamageModuleState();

    [Header("Debug")]
    public string lastDamageModelMessage = "";
    public int activeFireCount;
    public int maxObservedFireCount;

    private readonly float[] fireRemainingSeconds = new float[4];
    private CoreTacticalPrototypeHealth health;
    private CoreTacticalShipMotor motor;
    private float emergencyTeamPendingTime = -1f;
    private float emergencyTeamEndsTime;

    public bool IsEmergencyTeamActive => emergencyTeamActive;
    public float EmergencyTeamCooldownRemaining => Mathf.Max(0f, emergencyTeamReadyTime - Time.time);
    public bool IsEngineBroken => engine != null && engine.broken && !emergencyTeamActive;
    public float KineticResistancePercent => resistances.Get(CoreTacticalDamageType.Kinetic);
    public float ThermalResistancePercent => resistances.Get(CoreTacticalDamageType.Thermal);
    public float ChemicalResistancePercent => resistances.Get(CoreTacticalDamageType.Chemical);
    public float ExplosiveResistancePercent => resistances.Get(CoreTacticalDamageType.Explosive);

    private void Awake()
    {
        ResolveRuntimeLinks();
        EnsureModuleDefaults();
    }

    private void OnValidate()
    {
        resistances.Normalize();
        fireSectorCount = Mathf.Clamp(fireSectorCount, 1, 4);
        fireDurationSeconds = Mathf.Max(0.1f, fireDurationSeconds);
        fireDamagePerSecondMaxHealthFraction = Mathf.Max(0f, fireDamagePerSecondMaxHealthFraction);
        emergencyTeamActivationDelaySeconds = Mathf.Max(0f, emergencyTeamActivationDelaySeconds);
        emergencyTeamActiveSeconds = Mathf.Max(0.1f, emergencyTeamActiveSeconds);
        emergencyTeamCooldownSeconds = Mathf.Max(0.1f, emergencyTeamCooldownSeconds);
        EnsureModuleDefaults();
    }

    private void Update()
    {
        ResolveRuntimeLinks();
        float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
        TickEmergencyTeam(deltaSeconds);
        TickFires(deltaSeconds);
        TickModules(deltaSeconds);
        ApplyMotorDamageState();
    }

    public void ConfigureDefense(
        string classId,
        float maxHealth,
        CoreTacticalResistanceSet resistanceSet,
        float citadelHp,
        float engineHp)
    {
        resistances = resistanceSet;
        resistances.Normalize();
        fireSectorCount = ResolveFireSectorCount(classId);

        float safeHealth = Mathf.Max(1f, maxHealth);
        engine.Configure(CoreTacticalDamageModuleKind.Engine, "Engine", engineHp > 0f ? engineHp : safeHealth * 0.18f, 10f);
        citadel.Configure(CoreTacticalDamageModuleKind.Citadel, "Citadel", citadelHp > 0f ? citadelHp : safeHealth * 0.45f, 14f);
        mainBattery.Configure(CoreTacticalDamageModuleKind.MainBattery, "Main battery", safeHealth * 0.30f, 12f);
        secondaryBattery.Configure(CoreTacticalDamageModuleKind.SecondaryBattery, "Secondary battery", safeHealth * 0.16f, 9f);
        machineGuns.Configure(CoreTacticalDamageModuleKind.MachineGuns, "Machine guns", safeHealth * 0.10f, 7f);
        launcher.Configure(CoreTacticalDamageModuleKind.Launcher, "Launcher", safeHealth * 0.14f, 10f);

        ResetRuntimeState();
    }

    public void ResetRuntimeState()
    {
        for (int i = 0; i < fireRemainingSeconds.Length; i++)
        {
            fireRemainingSeconds[i] = 0f;
        }

        activeFireCount = 0;
        maxObservedFireCount = 0;
        emergencyTeamActive = false;
        emergencyTeamReadyTime = 0f;
        emergencyTeamPendingTime = -1f;
        emergencyTeamEndsTime = 0f;
        EnsureModuleDefaults();
        engine.ResetRuntimeState();
        citadel.ResetRuntimeState();
        mainBattery.ResetRuntimeState();
        secondaryBattery.ResetRuntimeState();
        machineGuns.ResetRuntimeState();
        launcher.ResetRuntimeState();
        ApplyMotorDamageState();
    }

    public CoreTacticalDamageResolution ResolveDamage(CoreTacticalDamageRequest request)
    {
        ResolveRuntimeLinks();
        EnsureModuleDefaults();
        resistances.Normalize();

        float rolledDamage = Roll(request.damage, request.damageSpread);
        float resistance = resistances.Get(request.damageType);
        float ignore = Mathf.Max(0f, request.resistanceIgnorePercent);
        float effectiveResistance = Mathf.Max(0f, resistance - ignore);
        float hullDamage = rolledDamage * Mathf.Clamp01(1f - effectiveResistance / 100f);
        CoreTacticalDamageResolution resolution = new CoreTacticalDamageResolution
        {
            damageType = request.damageType,
            rawDamage = request.damage,
            rolledDamage = rolledDamage,
            resistancePercent = resistance,
            resistanceIgnorePercent = ignore,
            effectiveResistancePercent = effectiveResistance,
            hullDamage = hullDamage,
            message = string.IsNullOrWhiteSpace(request.source) ? request.damageType.ToString() : request.source
        };

        if (!emergencyTeamActive)
        {
            if (TryStartFire(request.damageType, request.fireChancePercent, effectiveResistance))
            {
                resolution.fireStarted = true;
                ScheduleEmergencyTeam();
            }

            TryApplyModuleDamage(request, resolution.hullDamage, ref resolution);
        }
        else
        {
            resolution.damageControlBlockedMalfunction = true;
        }

        lastDamageModelMessage = BuildDebugMessage(request, resolution);
        return resolution;
    }

    public float GetResistancePercent(CoreTacticalDamageType type)
    {
        return resistances.Get(type);
    }

    public static CoreTacticalResistanceSet ResolveClassBaselineResistances(string classId)
    {
        string normalized = string.IsNullOrWhiteSpace(classId) ? "" : classId.Trim().ToLowerInvariant();
        if (normalized.Contains("battleship"))
        {
            return new CoreTacticalResistanceSet(90f, 30f, 30f, 42f);
        }

        if (normalized.Contains("cruiser"))
        {
            return new CoreTacticalResistanceSet(72f, 26f, 26f, 36f);
        }

        if (normalized.Contains("leviathan"))
        {
            return new CoreTacticalResistanceSet(40f, 80f, 90f, 50f);
        }

        if (normalized.Contains("automaton"))
        {
            return new CoreTacticalResistanceSet(34f, 22f, 16f, 24f);
        }

        if (normalized.Contains("ore") || normalized.Contains("boulder"))
        {
            return new CoreTacticalResistanceSet(95f, 35f, 15f, 65f);
        }

        return new CoreTacticalResistanceSet(48f, 18f, 18f, 28f);
    }

    private void TryApplyModuleDamage(CoreTacticalDamageRequest request, float hullDamage, ref CoreTacticalDamageResolution resolution)
    {
        if (hullDamage <= 0f)
        {
            return;
        }

        float moduleDamage = hullDamage * ResolveModuleDamageFraction(request.damageType);
        if (moduleDamage <= 0f)
        {
            return;
        }

        CoreTacticalDamageModuleState candidate = request.damageType == CoreTacticalDamageType.Kinetic
            ? ResolveInternalModuleCandidate(request.hitPoint)
            : ResolveExternalModuleCandidate(request.hitPoint);
        if (candidate == null)
        {
            return;
        }

        bool broken = candidate.ApplyDamage(moduleDamage);
        resolution.moduleBroken = broken;
        resolution.engineBroken = broken && candidate.kind == CoreTacticalDamageModuleKind.Engine;
        resolution.citadelHit = candidate.kind == CoreTacticalDamageModuleKind.Citadel && request.damageType == CoreTacticalDamageType.Kinetic;
        if (broken)
        {
            ScheduleEmergencyTeam();
        }
    }

    private float ResolveModuleDamageFraction(CoreTacticalDamageType type)
    {
        switch (type)
        {
            case CoreTacticalDamageType.Kinetic:
                return 0.45f;
            case CoreTacticalDamageType.Explosive:
                return 0.35f;
            case CoreTacticalDamageType.Thermal:
                return 0.18f;
            default:
                return 0.08f;
        }
    }

    private CoreTacticalDamageModuleState ResolveInternalModuleCandidate(Vector3 hitPoint)
    {
        if (motor == null || hitPoint.sqrMagnitude <= 0.0001f)
        {
            return UnityEngine.Random.value < 0.65f ? citadel : engine;
        }

        Vector3 local = motor.transform.InverseTransformPoint(hitPoint);
        float halfLength = Mathf.Max(1f, motor.hullSizeMeters.z * 0.5f);
        float z01 = Mathf.Clamp(local.z / halfLength, -1f, 1f);
        if (z01 < -0.35f)
        {
            return engine;
        }

        if (Mathf.Abs(z01) <= 0.45f)
        {
            return citadel;
        }

        return mainBattery;
    }

    private CoreTacticalDamageModuleState ResolveExternalModuleCandidate(Vector3 hitPoint)
    {
        if (UnityEngine.Random.value > 0.22f)
        {
            return null;
        }

        if (motor == null || hitPoint.sqrMagnitude <= 0.0001f)
        {
            return mainBattery;
        }

        Vector3 local = motor.transform.InverseTransformPoint(hitPoint);
        float halfLength = Mathf.Max(1f, motor.hullSizeMeters.z * 0.5f);
        float z01 = Mathf.Clamp(local.z / halfLength, -1f, 1f);
        if (z01 < -0.55f)
        {
            return launcher;
        }

        if (Mathf.Abs(z01) < 0.25f)
        {
            return secondaryBattery;
        }

        return mainBattery;
    }

    private bool TryStartFire(CoreTacticalDamageType damageType, float baseChancePercent, float effectiveResistancePercent)
    {
        if (baseChancePercent <= 0f || fireSectorCount <= 0 || emergencyTeamActive)
        {
            return false;
        }

        if (damageType != CoreTacticalDamageType.Thermal && damageType != CoreTacticalDamageType.Explosive)
        {
            return false;
        }

        float finalChance = Mathf.Max(0f, baseChancePercent) * Mathf.Clamp01(1f - effectiveResistancePercent / 100f);
        if (UnityEngine.Random.value * 100f > finalChance)
        {
            return false;
        }

        int sectorLimit = Mathf.Clamp(fireSectorCount, 1, fireRemainingSeconds.Length);
        int bestSector = -1;
        for (int i = 0; i < sectorLimit; i++)
        {
            if (fireRemainingSeconds[i] <= 0f)
            {
                bestSector = i;
                break;
            }
        }

        if (bestSector < 0)
        {
            return false;
        }

        fireRemainingSeconds[bestSector] = fireDurationSeconds;
        RecountFires();
        return true;
    }

    private void TickFires(float deltaSeconds)
    {
        int sectorLimit = Mathf.Clamp(fireSectorCount, 1, fireRemainingSeconds.Length);
        bool anyFire = false;
        for (int i = 0; i < sectorLimit; i++)
        {
            if (fireRemainingSeconds[i] <= 0f)
            {
                continue;
            }

            anyFire = true;
            fireRemainingSeconds[i] = Mathf.Max(0f, fireRemainingSeconds[i] - deltaSeconds);
        }

        RecountFires();
        if (!anyFire || activeFireCount <= 0 || emergencyTeamActive || health == null || health.currentHealth <= 0f)
        {
            return;
        }

        float baseDamage = health.maxHealth
            * Mathf.Max(0f, fireDamagePerSecondMaxHealthFraction)
            * activeFireCount
            * deltaSeconds;
        CoreTacticalDamageResolution resolution = ResolveDamage(CoreTacticalDamageRequest.Thermal(baseDamage, "Fire"));
        health.ApplyResolvedDamage(resolution.hullDamage, "Fire");
    }

    private void TickModules(float deltaSeconds)
    {
        if (emergencyTeamActive)
        {
            RepairAllMalfunctions();
            return;
        }

        engine.TickRepair(deltaSeconds);
        citadel.TickRepair(deltaSeconds);
        mainBattery.TickRepair(deltaSeconds);
        secondaryBattery.TickRepair(deltaSeconds);
        machineGuns.TickRepair(deltaSeconds);
        launcher.TickRepair(deltaSeconds);
    }

    private void TickEmergencyTeam(float deltaSeconds)
    {
        if (!automaticEmergencyTeam)
        {
            return;
        }

        float now = Time.time;
        if (emergencyTeamActive)
        {
            if (now >= emergencyTeamEndsTime)
            {
                emergencyTeamActive = false;
                emergencyTeamReadyTime = now + Mathf.Max(0.1f, emergencyTeamCooldownSeconds);
            }

            return;
        }

        if (emergencyTeamPendingTime >= 0f && now >= emergencyTeamPendingTime && now >= emergencyTeamReadyTime)
        {
            emergencyTeamPendingTime = -1f;
            ActivateEmergencyTeam(now);
        }
    }

    private void ScheduleEmergencyTeam()
    {
        if (!automaticEmergencyTeam || emergencyTeamActive || Time.time < emergencyTeamReadyTime)
        {
            return;
        }

        if (emergencyTeamPendingTime < 0f)
        {
            emergencyTeamPendingTime = Time.time + Mathf.Max(0f, emergencyTeamActivationDelaySeconds);
        }
    }

    private void ActivateEmergencyTeam(float now)
    {
        emergencyTeamActive = true;
        emergencyTeamEndsTime = now + Mathf.Max(0.1f, emergencyTeamActiveSeconds);
        ClearFires();
        RepairAllMalfunctions();
    }

    private void ClearFires()
    {
        for (int i = 0; i < fireRemainingSeconds.Length; i++)
        {
            fireRemainingSeconds[i] = 0f;
        }

        RecountFires();
    }

    private void RepairAllMalfunctions()
    {
        engine.RepairNow();
        citadel.RepairNow();
        mainBattery.RepairNow();
        secondaryBattery.RepairNow();
        machineGuns.RepairNow();
        launcher.RepairNow();
    }

    private void RecountFires()
    {
        int sectorLimit = Mathf.Clamp(fireSectorCount, 1, fireRemainingSeconds.Length);
        activeFireCount = 0;
        for (int i = 0; i < sectorLimit; i++)
        {
            if (fireRemainingSeconds[i] > 0f)
            {
                activeFireCount++;
            }
        }

        maxObservedFireCount = Mathf.Max(maxObservedFireCount, activeFireCount);
    }

    private void ApplyMotorDamageState()
    {
        if (motor == null)
        {
            return;
        }

        motor.damageMobilityMultiplier = IsEngineBroken ? 0f : 1f;
    }

    private void ResolveRuntimeLinks()
    {
        if (health == null)
        {
            health = GetComponent<CoreTacticalPrototypeHealth>();
        }

        if (motor == null)
        {
            motor = GetComponent<CoreTacticalShipMotor>();
        }
    }

    private void EnsureModuleDefaults()
    {
        if (engine == null) engine = new CoreTacticalDamageModuleState();
        if (citadel == null) citadel = new CoreTacticalDamageModuleState();
        if (mainBattery == null) mainBattery = new CoreTacticalDamageModuleState();
        if (secondaryBattery == null) secondaryBattery = new CoreTacticalDamageModuleState();
        if (machineGuns == null) machineGuns = new CoreTacticalDamageModuleState();
        if (launcher == null) launcher = new CoreTacticalDamageModuleState();
    }

    private static float Roll(float value, float spread)
    {
        float safeSpread = Mathf.Clamp01(spread);
        if (safeSpread <= 0.001f)
        {
            return Mathf.Max(0f, value);
        }

        return Mathf.Max(0f, value) * UnityEngine.Random.Range(1f - safeSpread, 1f + safeSpread);
    }

    private static int ResolveFireSectorCount(string classId)
    {
        string normalized = string.IsNullOrWhiteSpace(classId) ? "" : classId.Trim().ToLowerInvariant();
        if (normalized.Contains("battleship")) return 4;
        if (normalized.Contains("cruiser")) return 3;
        if (normalized.Contains("frigate")) return 2;
        return 1;
    }

    private static string BuildDebugMessage(CoreTacticalDamageRequest request, CoreTacticalDamageResolution resolution)
    {
        return request.damageType
            + " damage="
            + resolution.hullDamage.ToString("0.##")
            + " resist="
            + resolution.effectiveResistancePercent.ToString("0.#")
            + "%"
            + (resolution.citadelHit ? " citadel" : "")
            + (resolution.engineBroken ? " engine_broken" : "")
            + (resolution.fireStarted ? " fire" : "");
    }
}
