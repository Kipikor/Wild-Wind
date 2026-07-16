using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CoreTacticalOreBoulderDefinition
{
    private const float DefaultOreDensityKgPerCubicMeter = 3200f;
    private const float DefaultHealthPerDiameterMeter = 10000f;

    public string oreTypeId = "windshale";
    public string oreItemId = "windshale_ore";
    public string displayName = "Ore boulder";
    public Color color = new Color(0.55f, 0.50f, 0.45f, 1f);
    public float massKg = 200000f;
    public float densityKgPerCubicMeter = DefaultOreDensityKgPerCubicMeter;
    public Vector3 sizeMeters = new Vector3(70f, 48f, 62f);
    public float maxHealth = 8000f;
    public float healthPerDiameterMeter = DefaultHealthPerDiameterMeter;
    public float fragmentMassPerIntegrityPointKg = 1f;
    [Range(0f, 100f)] public float kineticResistancePercent = 95f;
    [Range(0f, 100f)] public float thermalResistancePercent = 35f;
    [Range(0f, 100f)] public float chemicalResistancePercent = 15f;
    [Range(0f, 100f)] public float explosiveResistancePercent = 65f;
    public float usefulOreConcentration01 = 0.05f;
    public float naturalIntegrityLossPerSecond = 4.5f;
    public float verticalDriftMS = 0.04f;
    public float chunkMinKg = 15f;
    public float chunkMaxKg = 140f;
    public float weaponRetention01 = 0.50f;
    public float fragmentFallSpeedMS = 3.8f;

    public CoreTacticalResistanceSet Resistances => new CoreTacticalResistanceSet(
        kineticResistancePercent,
        thermalResistancePercent,
        chemicalResistancePercent,
        explosiveResistancePercent);

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(oreTypeId))
        {
            oreTypeId = "ore";
        }

        if (string.IsNullOrWhiteSpace(oreItemId))
        {
            oreItemId = oreTypeId + "_ore";
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = oreTypeId + " boulder";
        }

        sizeMeters = new Vector3(
            Mathf.Max(2f, sizeMeters.x),
            Mathf.Max(2f, sizeMeters.y),
            Mathf.Max(2f, sizeMeters.z));
        densityKgPerCubicMeter = Mathf.Max(1f, densityKgPerCubicMeter);
        float ellipsoidVolume = 4f * Mathf.PI
            * (sizeMeters.x * 0.5f)
            * (sizeMeters.y * 0.5f)
            * (sizeMeters.z * 0.5f)
            / 3f;
        massKg = Mathf.Max(1f, ellipsoidVolume * densityKgPerCubicMeter);
        healthPerDiameterMeter = Mathf.Max(1f, healthPerDiameterMeter);
        float effectiveDiameter = Mathf.Max(1f, (sizeMeters.x + sizeMeters.y + sizeMeters.z) / 3f);
        maxHealth = Mathf.Max(1f, effectiveDiameter * healthPerDiameterMeter);
        fragmentMassPerIntegrityPointKg = Mathf.Max(0.01f, fragmentMassPerIntegrityPointKg);
        maxHealth = Mathf.Max(1f, maxHealth);
        kineticResistancePercent = Mathf.Clamp(kineticResistancePercent, 0f, 100f);
        thermalResistancePercent = Mathf.Clamp(thermalResistancePercent, 0f, 100f);
        chemicalResistancePercent = Mathf.Clamp(chemicalResistancePercent, 0f, 100f);
        explosiveResistancePercent = Mathf.Clamp(explosiveResistancePercent, 0f, 100f);
        usefulOreConcentration01 = Mathf.Clamp01(usefulOreConcentration01);
        naturalIntegrityLossPerSecond = Mathf.Max(0f, naturalIntegrityLossPerSecond);
        chunkMinKg = Mathf.Max(1f, chunkMinKg);
        chunkMaxKg = Mathf.Max(chunkMinKg, chunkMaxKg);
        weaponRetention01 = Mathf.Clamp01(weaponRetention01);
        fragmentFallSpeedMS = Mathf.Max(0.1f, fragmentFallSpeedMS);
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalOreBoulder : MonoBehaviour
{
    private const int MaxFragmentsPerTick = 8;

    public CoreTacticalOreBoulderDefinition definition;
    public float currentMassKg;
    public float stormFloorY = -700f;

    private CoreTacticalPrototypeHealth health;
    private float initialMassKg = 1f;
    private float naturalChunkAccumulatorKg;
    private float weaponChunkAccumulatorKg;
    private float nextNaturalChunkKg;
    private float nextWeaponChunkKg;
    private bool initialized;

    public string OreItemId => definition != null ? definition.oreItemId : "";
    public string OreDisplayName => definition != null ? definition.displayName : "Ore boulder";
    public float CurrentHealth => health != null ? Mathf.Max(0f, health.currentHealth) : 0f;
    public float MaxHealth => health != null ? Mathf.Max(1f, health.maxHealth) : definition != null ? Mathf.Max(1f, definition.maxHealth) : 1f;
    public float PhysicalMassKg => currentMassKg;
    public float EffectiveDiameterMeters => definition != null ? Mathf.Max(1f, (definition.sizeMeters.x + definition.sizeMeters.y + definition.sizeMeters.z) / 3f) : 1f;
    public CoreTacticalResistanceSet Resistances => definition != null
        ? new CoreTacticalResistanceSet(
            definition.kineticResistancePercent,
            definition.thermalResistancePercent,
            definition.chemicalResistancePercent,
            definition.explosiveResistancePercent)
        : CoreTacticalDamageProfile.ResolveClassBaselineResistances("ore_boulder");
    public float NaturalIntegrityLossPerSecond => definition != null ? definition.naturalIntegrityLossPerSecond : 0f;
    public float NaturalFragmentMassPerSecond => definition != null ? definition.naturalIntegrityLossPerSecond * definition.fragmentMassPerIntegrityPointKg : 0f;
    public float VerticalDriftMS => definition != null ? definition.verticalDriftMS : 0f;
    public float CurrentHealth01 => health != null && health.maxHealth > 0f ? Mathf.Clamp01(health.currentHealth / health.maxHealth) : 0f;
    public float CurrentMass01 => CurrentHealth01;

    public void Initialize(CoreTacticalOreBoulderDefinition boulderDefinition, CoreTacticalPrototypeHealth boulderHealth, float floorY)
    {
        definition = boulderDefinition ?? new CoreTacticalOreBoulderDefinition();
        definition.Normalize();
        health = boulderHealth != null ? boulderHealth : GetComponent<CoreTacticalPrototypeHealth>();
        initialMassKg = Mathf.Max(1f, definition.massKg);
        currentMassKg = initialMassKg;
        stormFloorY = floorY;
        nextNaturalChunkKg = RollNextChunkMass();
        nextWeaponChunkKg = RollNextChunkMass();
        initialized = true;
        ApplyVisualScale();
    }

    private void Awake()
    {
        health = GetComponent<CoreTacticalPrototypeHealth>();
    }

    private void Update()
    {
        if (!initialized || definition == null || health == null || currentMassKg <= 0f || health.currentHealth <= 0f)
        {
            return;
        }

        float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (Mathf.Abs(definition.verticalDriftMS) > 0.0001f)
        {
            transform.position += Vector3.up * (definition.verticalDriftMS * deltaSeconds);
        }

        float naturalDamage = definition.naturalIntegrityLossPerSecond * deltaSeconds;
        if (naturalDamage > 0f)
        {
            float applied = ApplyInternalIntegrityLoss(naturalDamage, "natural ore shedding");
            RegisterMassLossFromDamage(applied, true);
        }

        TrySpawnAccumulatedFragments(true);
        TrySpawnAccumulatedFragments(false);
        ApplyVisualScale();
    }

    public void NotifyAppliedDamage(CoreTacticalDamageRequest request, float appliedHullDamage)
    {
        if (!initialized || appliedHullDamage <= 0f)
        {
            return;
        }

        bool natural = request.source != null
            && !string.IsNullOrWhiteSpace(request.source)
            && request.source.IndexOf("natural", StringComparison.OrdinalIgnoreCase) >= 0;
        RegisterMassLossFromDamage(appliedHullDamage, natural);
    }

    public float DrillExtract(float damagePerTick, float drillLossReduction01, out string oreItemId)
    {
        oreItemId = OreItemId;
        if (!initialized || definition == null || health == null || currentMassKg <= 0f || health.currentHealth <= 0f)
        {
            return 0f;
        }

        float applied = ApplyTypedIntegrityLoss(CoreTacticalDamageRequest.Thermal(damagePerTick, "drill laser", 10f));
        if (applied <= 0f)
        {
            return 0f;
        }

        float lostMassKg = ConvertDamageToMass(applied);
        lostMassKg = Mathf.Min(lostMassKg, currentMassKg);
        currentMassKg = Mathf.Max(0f, currentMassKg - lostMassKg);
        float retention = Mathf.Clamp01(definition.weaponRetention01 + Mathf.Clamp01(drillLossReduction01));
        float cleanKg = lostMassKg * definition.usefulOreConcentration01 * retention;
        ApplyVisualScale();
        return Mathf.Max(0f, cleanKg);
    }

    public float EstimateDrillCleanOutputKg(float damagePerTick, float drillLossReduction01)
    {
        if (!initialized || definition == null || health == null || currentMassKg <= 0f || health.currentHealth <= 0f || damagePerTick <= 0f)
        {
            return 0f;
        }

        float applied = Mathf.Min(Mathf.Max(0f, damagePerTick), Mathf.Max(0f, health.currentHealth));
        float lostMassKg = Mathf.Min(ConvertDamageToMass(applied), currentMassKg);
        float retention = Mathf.Clamp01(definition.weaponRetention01 + Mathf.Clamp01(drillLossReduction01));
        return Mathf.Max(0f, lostMassKg * definition.usefulOreConcentration01 * retention);
    }

    public void ConsumeByLeviathan()
    {
        currentMassKg = 0f;
        if (health != null)
        {
            health.currentHealth = 0f;
        }

        Destroy(gameObject);
    }

    private float ApplyInternalIntegrityLoss(float amount, string source)
    {
        if (health == null || amount <= 0f)
        {
            return 0f;
        }

        float before = health.currentHealth;
        health.ApplyResolvedDamage(amount, source);
        return Mathf.Max(0f, before - health.currentHealth);
    }

    private float ApplyTypedIntegrityLoss(CoreTacticalDamageRequest request)
    {
        if (health == null || request.damage <= 0f)
        {
            return 0f;
        }

        CoreTacticalDamageProfile profile = GetComponent<CoreTacticalDamageProfile>();
        float appliedDamage = request.damage;
        if (profile != null)
        {
            appliedDamage = profile.ResolveDamage(request).hullDamage;
        }

        float before = health.currentHealth;
        health.ApplyResolvedDamage(appliedDamage, request.source);
        return Mathf.Max(0f, before - health.currentHealth);
    }

    private void RegisterMassLossFromDamage(float appliedDamage, bool natural)
    {
        if (definition == null || appliedDamage <= 0f || currentMassKg <= 0f)
        {
            return;
        }

        float lostMassKg = Mathf.Min(ConvertDamageToMass(appliedDamage), currentMassKg);
        if (lostMassKg <= 0f)
        {
            return;
        }

        currentMassKg = Mathf.Max(0f, currentMassKg - lostMassKg);
        if (natural)
        {
            naturalChunkAccumulatorKg += lostMassKg;
        }
        else
        {
            weaponChunkAccumulatorKg += lostMassKg;
        }
    }

    private float ConvertDamageToMass(float appliedDamage)
    {
        float kgPerPoint = definition != null ? Mathf.Max(0.01f, definition.fragmentMassPerIntegrityPointKg) : 1f;
        return Mathf.Max(0f, appliedDamage) * kgPerPoint;
    }

    private void TrySpawnAccumulatedFragments(bool natural)
    {
        float accumulator = natural ? naturalChunkAccumulatorKg : weaponChunkAccumulatorKg;
        float nextChunk = natural ? nextNaturalChunkKg : nextWeaponChunkKg;
        int spawned = 0;
        while (accumulator >= nextChunk && spawned < MaxFragmentsPerTick)
        {
            float chunkMassKg = Mathf.Min(accumulator, nextChunk);
            float retention = natural ? 1f : definition.weaponRetention01;
            SpawnFragment(chunkMassKg, retention);
            accumulator -= chunkMassKg;
            nextChunk = RollNextChunkMass();
            spawned++;
        }

        if (natural)
        {
            naturalChunkAccumulatorKg = accumulator;
            nextNaturalChunkKg = nextChunk;
        }
        else
        {
            weaponChunkAccumulatorKg = accumulator;
            nextWeaponChunkKg = nextChunk;
        }
    }

    private float RollNextChunkMass()
    {
        if (definition == null)
        {
            return 20f;
        }

        return UnityEngine.Random.Range(definition.chunkMinKg, definition.chunkMaxKg);
    }

    private void SpawnFragment(float massKg, float retention01)
    {
        if (definition == null || massKg <= 0f)
        {
            return;
        }

        Vector3 offset = UnityEngine.Random.insideUnitSphere;
        offset.y = -Mathf.Abs(offset.y);
        offset = Vector3.Scale(offset.normalized, definition.sizeMeters * 0.22f);
        GameObject fragmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fragmentObject.name = "Core Tactical Ore Fragment - " + definition.oreItemId;
        fragmentObject.transform.position = transform.position + offset;
        float visualSize = Mathf.Clamp(Mathf.Pow(Mathf.Max(1f, massKg) / 35f, 1f / 3f), 0.7f, 3.2f);
        fragmentObject.transform.localScale = Vector3.one * visualSize;

        Collider collider = fragmentObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = fragmentObject.GetComponent<Renderer>();
        ApplyRendererColor(renderer, definition.color);

        CoreTacticalOreFragment fragment = fragmentObject.AddComponent<CoreTacticalOreFragment>();
        fragment.Initialize(
            definition.oreTypeId,
            definition.oreItemId,
            definition.displayName,
            massKg,
            definition.usefulOreConcentration01 * Mathf.Clamp01(retention01),
            definition.fragmentFallSpeedMS,
            stormFloorY,
            definition.color);
    }

    private void ApplyVisualScale()
    {
        if (definition == null)
        {
            return;
        }

        float scale01 = Mathf.Clamp(Mathf.Pow(CurrentHealth01, 1f / 3f), 0.04f, 1f);
        transform.localScale = definition.sizeMeters * scale01;
    }

    public static void ApplyRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        renderer.sharedMaterial = material;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalOreFragment : MonoBehaviour
{
    public string oreTypeId = "";
    public string oreItemId = "";
    public string displayName = "";
    public float rawMassKg;
    public float usefulOreKg;
    public float fallSpeedMS = 3.5f;
    public float stormFloorY = -700f;
    public float fragmentLifetimeSeconds = 20f;
    public bool captured;

    private Color oreColor = Color.gray;
    private float lifeSeconds;
    private Vector3 initialScale = Vector3.one;

    public void Initialize(
        string typeId,
        string itemId,
        string oreDisplayName,
        float rawKg,
        float usefulConcentration01,
        float fallSpeed,
        float floorY,
        Color color)
    {
        oreTypeId = typeId ?? "";
        oreItemId = itemId ?? "";
        displayName = string.IsNullOrWhiteSpace(oreDisplayName) ? oreItemId : oreDisplayName;
        rawMassKg = Mathf.Max(0f, rawKg);
        usefulOreKg = rawMassKg * Mathf.Clamp01(usefulConcentration01);
        fallSpeedMS = Mathf.Max(0.1f, fallSpeed);
        stormFloorY = floorY;
        oreColor = color;
        fragmentLifetimeSeconds = Mathf.Max(1f, fragmentLifetimeSeconds);
        lifeSeconds = 0f;
        initialScale = transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
    }

    private void Update()
    {
        if (captured)
        {
            return;
        }

        AdvanceLifetime(deltaSeconds: Mathf.Max(0f, Time.deltaTime));
    }

    public void AdvanceLifetimeForTests(float deltaSeconds)
    {
        if (captured)
        {
            return;
        }

        AdvanceLifetime(Mathf.Max(0f, deltaSeconds));
    }

    public float LifetimeSecondsForTests => lifeSeconds;

    private void AdvanceLifetime(float deltaSeconds)
    {
        transform.position += Vector3.down * (fallSpeedMS * deltaSeconds);
        lifeSeconds += deltaSeconds;
        float lifetime01 = Mathf.Clamp01(lifeSeconds / Mathf.Max(1f, fragmentLifetimeSeconds));
        float dustScale = Mathf.Lerp(1f, 0.04f, Mathf.SmoothStep(0f, 1f, lifetime01));
        transform.localScale = initialScale * dustScale;
        if (transform.position.y <= stormFloorY || lifeSeconds >= fragmentLifetimeSeconds)
        {
            Destroy(gameObject);
        }
    }

    public bool PullToward(Vector3 target, float maxDistance, bool allowCaptured = false)
    {
        if (captured && !allowCaptured)
        {
            return false;
        }

        transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(0f, maxDistance));
        return Vector3.Distance(transform.position, target) <= 2.4f;
    }

    public void ConsumeByLeviathan()
    {
        rawMassKg = 0f;
        usefulOreKg = 0f;
        captured = true;
        Destroy(gameObject);
    }
}

public static class CoreTacticalOreTargetingRules
{
    public static bool IsOreBoulder(CoreTacticalShipMotor candidate)
    {
        return candidate != null && candidate.GetComponent<CoreTacticalOreBoulder>() != null;
    }

    public static bool IsLeviathan(CoreTacticalShipMotor candidate)
    {
        return candidate != null && candidate.GetComponent<CoreTacticalLeviathanController>() != null;
    }

    public static bool IsAutomaticCombatTarget(CoreTacticalCombatant combatant, CoreTacticalCombatTeam targetTeam)
    {
        if (combatant == null || combatant.team != targetTeam || !combatant.IsAlive || combatant.ship == null)
        {
            return false;
        }

        if (IsOreBoulder(combatant.ship))
        {
            return false;
        }

        CoreTacticalLeviathanController leviathan = combatant.ship.GetComponent<CoreTacticalLeviathanController>();
        return leviathan == null || leviathan.IsAutomaticWeaponTarget;
    }

    public static bool IsValidExplicitTarget(CoreTacticalShipMotor candidate, CoreTacticalCombatTeam targetTeam)
    {
        if (candidate == null)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        if (health != null && health.currentHealth <= 0f)
        {
            return false;
        }

        if (IsOreBoulder(candidate))
        {
            return true;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        return combatant != null && combatant.team == targetTeam && combatant.IsAlive;
    }
}

[Serializable]
public sealed class CoreTacticalGasCloudDefinition
{
    public string condensateTypeId = "";
    public string condensateItemId = "";
    public string displayName = "Cloud";
    public Color color = new Color(0.72f, 0.86f, 1f, 0.24f);
    public float rawVolumeLiters = 8000f;
    public float usefulVolumeLiters = 1200f;
    public float chemicalDamagePerMinute;
    public Vector3 driftVelocityMS;
    public bool harvestable = true;
    public Vector3[] lobeOffsets = Array.Empty<Vector3>();
    public Vector3[] lobeSizes = Array.Empty<Vector3>();

    public float UsefulConcentration01 => rawVolumeLiters > 0.001f
        ? Mathf.Clamp01(usefulVolumeLiters / rawVolumeLiters)
        : 0f;

    public void Normalize()
    {
        condensateTypeId = string.IsNullOrWhiteSpace(condensateTypeId) ? "" : condensateTypeId.Trim();
        condensateItemId = string.IsNullOrWhiteSpace(condensateItemId) ? "" : condensateItemId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? "Cloud" : displayName.Trim();
        rawVolumeLiters = Mathf.Max(0f, rawVolumeLiters);
        usefulVolumeLiters = Mathf.Clamp(usefulVolumeLiters, 0f, rawVolumeLiters);
        chemicalDamagePerMinute = Mathf.Max(0f, chemicalDamagePerMinute);
        Vector3 size = lobeSizes != null && lobeSizes.Length > 0
            ? lobeSizes[0]
            : new Vector3(520f, 180f, 360f);
        lobeOffsets = new[] { Vector3.zero };
        lobeSizes = new[]
        {
            new Vector3(
                Mathf.Max(8f, Mathf.Abs(size.x)),
                Mathf.Max(8f, Mathf.Abs(size.y)),
                Mathf.Max(8f, Mathf.Abs(size.z)))
        };
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalGasCloud : MonoBehaviour
{
    private readonly List<Transform> lobeTransforms = new List<Transform>(4);
    private CoreTacticalGasCloudDefinition definition;
    private float initialRawVolumeLiters = 1f;
    private bool initialized;

    public string CondensateItemId => definition != null ? definition.condensateItemId : "";
    public string DisplayName => definition != null ? definition.displayName : "Cloud";
    public Color CloudColor => definition != null ? definition.color : new Color(0.72f, 0.86f, 1f, 0.24f);
    public float RawVolumeLiters => definition != null ? Mathf.Max(0f, definition.rawVolumeLiters) : 0f;
    public float UsefulVolumeLiters => definition != null ? Mathf.Max(0f, definition.usefulVolumeLiters) : 0f;
    public float UsefulConcentration01 => definition != null ? definition.UsefulConcentration01 : 0f;
    public float ChemicalDamagePerMinute => definition != null ? Mathf.Max(0f, definition.chemicalDamagePerMinute) : 0f;
    public Vector3 DriftVelocityMS => definition != null ? definition.driftVelocityMS : Vector3.zero;
    public bool CanHarvest => definition != null && definition.harvestable && !string.IsNullOrWhiteSpace(definition.condensateItemId) && RawVolumeLiters > 0.001f;
    public float Volume01 => Mathf.Clamp01(RawVolumeLiters / Mathf.Max(1f, initialRawVolumeLiters));
    public int LobeRendererCountForTests => CountLobeRenderersForTests();
    public bool UsesPlainTransparentCloudMaterialForTests => CheckPlainTransparentCloudMaterialForTests();

    public void Initialize(CoreTacticalGasCloudDefinition cloudDefinition)
    {
        definition = cloudDefinition ?? new CoreTacticalGasCloudDefinition();
        definition.Normalize();
        initialRawVolumeLiters = Mathf.Max(1f, definition.rawVolumeLiters);
        initialized = true;
        BuildLobes();
        ApplyVisualScale();
    }

    private void Update()
    {
        if (!initialized || definition == null)
        {
            return;
        }

        float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
        if (deltaSeconds <= 0f)
        {
            return;
        }

        transform.position += definition.driftVelocityMS * deltaSeconds;
        ApplyChemicalContactDamage(deltaSeconds);
        ApplyVisualScale();
    }

    public float ExtractRawCondensate(float requestedLiters, out float usefulLiters)
    {
        usefulLiters = 0f;
        if (!CanHarvest || requestedLiters <= 0f)
        {
            return 0f;
        }

        float extractedRaw = Mathf.Min(Mathf.Max(0f, requestedLiters), definition.rawVolumeLiters);
        float concentration = definition.UsefulConcentration01;
        usefulLiters = extractedRaw * concentration;
        definition.rawVolumeLiters = Mathf.Max(0f, definition.rawVolumeLiters - extractedRaw);
        definition.usefulVolumeLiters = Mathf.Max(0f, definition.usefulVolumeLiters - usefulLiters);
        ApplyVisualScale();
        if (definition.rawVolumeLiters <= 0.001f)
        {
            Destroy(gameObject);
        }

        return extractedRaw;
    }

    public bool OverlapsCollectionSphere(Vector3 sphereCenter, float sphereRadiusMeters)
    {
        if (!initialized || definition == null || definition.lobeOffsets == null || definition.lobeSizes == null)
        {
            return false;
        }

        float shrinkScale = GetShrinkScale();
        for (int i = 0; i < definition.lobeSizes.Length; i++)
        {
            Vector3 center = transform.TransformPoint(definition.lobeOffsets[i] * shrinkScale);
            Vector3 radius = definition.lobeSizes[i] * (0.5f * shrinkScale);
            float normalizedDistance = CalculateNormalizedSphereEllipsoidDistance(sphereCenter, sphereRadiusMeters, center, radius);
            if (normalizedDistance <= 1f)
            {
                return true;
            }
        }

        return false;
    }

    public void ApplyChemicalContactDamageForTests(float deltaSeconds)
    {
        ApplyChemicalContactDamage(Mathf.Max(0f, deltaSeconds));
    }

    private static float CalculateNormalizedSphereEllipsoidDistance(Vector3 sphereCenter, float sphereRadiusMeters, Vector3 ellipsoidCenter, Vector3 ellipsoidRadius)
    {
        float x = Mathf.Abs(sphereCenter.x - ellipsoidCenter.x) / Mathf.Max(1f, ellipsoidRadius.x + sphereRadiusMeters);
        float y = Mathf.Abs(sphereCenter.y - ellipsoidCenter.y) / Mathf.Max(1f, ellipsoidRadius.y + sphereRadiusMeters);
        float z = Mathf.Abs(sphereCenter.z - ellipsoidCenter.z) / Mathf.Max(1f, ellipsoidRadius.z + sphereRadiusMeters);
        return Mathf.Sqrt(x * x + y * y + z * z);
    }

    private void ApplyChemicalContactDamage(float deltaSeconds)
    {
        if (definition == null || definition.chemicalDamagePerMinute <= 0f)
        {
            return;
        }

        float damage = definition.chemicalDamagePerMinute / 60f * deltaSeconds;
        if (damage <= 0f)
        {
            return;
        }

        CoreTacticalShipMotor[] ships = FindObjectsByType<CoreTacticalShipMotor>(FindObjectsSortMode.None);
        for (int i = 0; i < ships.Length; i++)
        {
            CoreTacticalShipMotor ship = ships[i];
            if (ship == null || !OverlapsCollectionSphere(ship.transform.position, ResolveShipContactRadius(ship)))
            {
                continue;
            }

            CoreTacticalPrototypeHealth health = ship.GetComponent<CoreTacticalPrototypeHealth>();
            if (health == null || health.currentHealth <= 0f)
            {
                continue;
            }

            CoreTacticalDamageRequest request = CoreTacticalDamageRequest.Chemical(damage, "cloud chemical damage");
            request.damageSpread = 0f;
            health.ApplyDamage(request);
        }
    }

    private static float ResolveShipContactRadius(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return 1f;
        }

        Vector3 size = ship.hullSizeMeters;
        return Mathf.Max(1f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f);
    }

    private void BuildLobes()
    {
        for (int i = lobeTransforms.Count - 1; i >= 0; i--)
        {
            if (lobeTransforms[i] != null)
            {
                Destroy(lobeTransforms[i].gameObject);
            }
        }
        lobeTransforms.Clear();

        if (definition == null) return;

        Material material = CoreTacticalWeaponVisualMaterialUtility.CreateTransparentMaterial(definition.color, 0.12f);
        material.name = "Core Tactical Gas Cloud " + ColorUtility.ToHtmlStringRGBA(definition.color);
        for (int i = 0; i < definition.lobeSizes.Length; i++)
        {
            GameObject lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lobe.name = "Cloud Lobe " + (i + 1);
            lobe.transform.SetParent(transform, false);
            lobe.transform.localPosition = definition.lobeOffsets[i];
            lobe.transform.localScale = definition.lobeSizes[i];

            Collider collider = lobe.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = lobe.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            lobeTransforms.Add(lobe.transform);
        }
    }

    private int CountLobeRenderersForTests()
    {
        int count = 0;
        for (int i = 0; i < lobeTransforms.Count; i++)
        {
            Transform lobe = lobeTransforms[i];
            if (lobe != null && lobe.GetComponent<Renderer>() != null)
            {
                count++;
            }
        }

        return count;
    }

    private bool CheckPlainTransparentCloudMaterialForTests()
    {
        Material sharedCloudMaterial = null;
        int rendererCount = 0;
        for (int i = 0; i < lobeTransforms.Count; i++)
        {
            Transform lobe = lobeTransforms[i];
            Renderer renderer = lobe != null ? lobe.GetComponent<Renderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            rendererCount++;
            Material material = renderer.sharedMaterial;
            if (sharedCloudMaterial == null)
            {
                sharedCloudMaterial = material;
            }
            else if (!ReferenceEquals(sharedCloudMaterial, material))
            {
                return false;
            }

            if (renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off || renderer.receiveShadows)
            {
                return false;
            }
        }

        if (rendererCount <= 0 || sharedCloudMaterial == null)
        {
            return false;
        }

        return (!sharedCloudMaterial.HasProperty("_ZWrite") || sharedCloudMaterial.GetInt("_ZWrite") == 0)
            && sharedCloudMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void ApplyVisualScale()
    {
        if (definition == null || lobeTransforms.Count == 0)
        {
            return;
        }

        float scale = GetShrinkScale();
        for (int i = 0; i < lobeTransforms.Count && i < definition.lobeSizes.Length; i++)
        {
            Transform lobe = lobeTransforms[i];
            if (lobe == null) continue;
            lobe.localPosition = definition.lobeOffsets[i] * scale;
            lobe.localScale = definition.lobeSizes[i] * scale;
        }
    }

    private float GetShrinkScale()
    {
        return Mathf.Clamp(Mathf.Pow(Volume01, 1f / 3f), 0.04f, 1f);
    }
}

public enum CoreTacticalMiningModule
{
    Magnet,
    Drill,
    Crusher,
    Siphon,
    CloudConcentrator
}

public struct CoreTacticalInventoryRow
{
    public string itemId;
    public string displayName;
    public float amountKg;
}

[DisallowMultipleComponent]
public sealed class CoreTacticalAutomatonWreck : MonoBehaviour
{
    private static readonly RaycastHit[] WreckHits = new RaycastHit[24];
    private static readonly Collider[] ExplosionHits = new Collider[64];

    private readonly List<CoreTacticalInventoryRow> salvageManifest = new List<CoreTacticalInventoryRow>();

    public string displayName = "Automaton wreck";
    public float massKg = 100f;
    public float maxHealth = 80f;
    public float currentHealth = 80f;
    public float accessDifficultyPercent = 15f;
    public float fallSpeedMS = 12f;
    public float capturedFallSpeedMultiplier = 0.12f;
    public float spawnDamageGraceSeconds = 0.25f;
    public float despawnAfterSeconds = 60f;
    public bool createDespawnExplosionVisual = true;
    public bool captured;
    public string captorName = "";

    private float accessBuildupPercent;
    private float visualRadiusMeters = 4f;
    private float spawnedAtTime;
    private float uncapturedLifetimeSeconds;
    private bool expiredByLifetime;

    public int RemainingSalvageCount => salvageManifest.Count;
    public bool HasSalvage => salvageManifest.Count > 0;
    public bool IsDestroyed => currentHealth <= 0.001f || this == null;
    public float AccessBuildupPercent => Mathf.Max(0f, accessBuildupPercent);
    public float VisualRadiusMeters => Mathf.Max(0.5f, visualRadiusMeters);
    public float ApproximateLengthMeters => Mathf.Max(1f, VisualRadiusMeters / 0.38f);
    public float UncapturedLifetimeSecondsForTests => Mathf.Max(0f, uncapturedLifetimeSeconds);
    public bool ExpiredByLifetimeForTests => expiredByLifetime;

    public void Initialize(
        string wreckName,
        float wreckMassKg,
        float wreckHealth,
        float accessDifficulty,
        IReadOnlyList<CoreTacticalInventoryRow> manifest,
        float visualRadius)
    {
        displayName = string.IsNullOrWhiteSpace(wreckName) ? "Automaton wreck" : wreckName.Trim();
        massKg = Mathf.Max(1f, wreckMassKg);
        maxHealth = Mathf.Max(1f, wreckHealth);
        currentHealth = maxHealth;
        accessDifficultyPercent = accessDifficulty;
        visualRadiusMeters = Mathf.Max(0.5f, visualRadius);
        spawnedAtTime = Time.time;
        uncapturedLifetimeSeconds = 0f;
        expiredByLifetime = false;
        salvageManifest.Clear();

        if (manifest != null)
        {
            for (int i = 0; i < manifest.Count; i++)
            {
                CoreTacticalInventoryRow row = manifest[i];
                if (row.amountKg <= 0.001f)
                {
                    continue;
                }

                salvageManifest.Add(new CoreTacticalInventoryRow
                {
                    itemId = row.itemId,
                    displayName = string.IsNullOrWhiteSpace(row.displayName) ? "Automaton salvage" : row.displayName,
                    amountKg = row.amountKg
                });
            }
        }

        if (salvageManifest.Count == 0)
        {
            AddManifestRow(salvageManifest, "automaton_relay", "Automaton relay", 35f);
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.mass = massKg;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
    }

    private void Update()
    {
        TickLifetimeAndFall(Mathf.Max(0f, Time.deltaTime));
    }

    private void TickLifetimeAndFall(float deltaSeconds)
    {
        if (currentHealth <= 0.001f)
        {
            return;
        }

        if (!captured && despawnAfterSeconds > 0.001f)
        {
            uncapturedLifetimeSeconds += Mathf.Max(0f, deltaSeconds);
            if (uncapturedLifetimeSeconds >= despawnAfterSeconds)
            {
                ExpireByLifetime();
                return;
            }
        }

        float speed = Mathf.Max(0f, fallSpeedMS) * (captured ? Mathf.Clamp01(capturedFallSpeedMultiplier) : 1f);
        if (speed <= 0.001f)
        {
            return;
        }

        transform.position += Vector3.down * speed * Mathf.Max(0f, deltaSeconds);
    }

    public void SetCaptured(bool value, string captor = "", float fallSpeedMultiplier = 0.12f)
    {
        bool stateChanged = captured != value;
        captured = value;
        captorName = value ? (captor ?? "") : "";
        capturedFallSpeedMultiplier = value ? Mathf.Clamp01(fallSpeedMultiplier) : 1f;
        if (value || stateChanged)
        {
            uncapturedLifetimeSeconds = 0f;
        }
    }

    public bool PullToward(Vector3 targetPosition, float maxDistanceThisFrame, bool snapAtTarget)
    {
        Vector3 position = transform.position;
        Vector3 delta = targetPosition - position;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
        {
            if (snapAtTarget)
            {
                transform.position = targetPosition;
            }

            return true;
        }

        float step = Mathf.Max(0f, maxDistanceThisFrame);
        if (step >= distance)
        {
            transform.position = snapAtTarget ? targetPosition : position + delta;
            return true;
        }

        transform.position = position + delta / distance * step;
        return false;
    }

    public bool TryExtractSalvage(
        float accessRatingPercent,
        float buildupOnFailurePercent,
        out CoreTacticalInventoryRow extractedRow,
        out bool emptied,
        out float chancePercent)
    {
        extractedRow = default;
        emptied = salvageManifest.Count == 0;
        chancePercent = 0f;

        if (salvageManifest.Count == 0)
        {
            accessBuildupPercent = 0f;
            return false;
        }

        chancePercent = Mathf.Clamp(accessRatingPercent - accessDifficultyPercent + accessBuildupPercent, 0f, 100f);
        if (UnityEngine.Random.value * 100f > chancePercent)
        {
            accessBuildupPercent = Mathf.Max(0f, accessBuildupPercent + Mathf.Max(0f, buildupOnFailurePercent));
            emptied = false;
            return false;
        }

        int index = UnityEngine.Random.Range(0, salvageManifest.Count);
        extractedRow = salvageManifest[index];
        salvageManifest.RemoveAt(index);
        accessBuildupPercent = 0f;
        emptied = salvageManifest.Count == 0;
        return true;
    }

    public void ApplyProjectileDamage(float amount, string source, Vector3 hitPosition)
    {
        ApplyDamage(amount, source);
    }

    public void ApplyDamage(float amount, string source = "")
    {
        if (currentHealth <= 0.001f)
        {
            return;
        }

        if (spawnDamageGraceSeconds > 0f && Time.time - spawnedAtTime < spawnDamageGraceSeconds)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - Mathf.Max(0f, amount));
        if (currentHealth <= 0.001f)
        {
            Destroy(gameObject);
        }
    }

    private void ExpireByLifetime()
    {
        if (currentHealth <= 0.001f)
        {
            return;
        }

        currentHealth = 0f;
        expiredByLifetime = true;
        captured = false;
        captorName = "";
        if (createDespawnExplosionVisual)
        {
            CoreTacticalFlakBurstVisual.Create(transform.position, Mathf.Max(12f, VisualRadiusMeters * 2.4f));
        }

        Destroy(gameObject);
    }

    public void ConsumeByLeviathan()
    {
        currentHealth = 0f;
        captured = true;
        captorName = "Leviathan";
        Destroy(gameObject);
    }

    public void DisableSpawnDamageGraceForTests()
    {
        spawnDamageGraceSeconds = 0f;
    }

    public void AdvanceLifetimeForTests(float deltaSeconds)
    {
        TickLifetimeAndFall(Mathf.Max(0f, deltaSeconds));
    }

    public static bool TrySphereCastWreck(
        Vector3 from,
        Vector3 to,
        float radiusMeters,
        out CoreTacticalAutomatonWreck wreck,
        out Vector3 hitPosition)
    {
        wreck = null;
        hitPosition = to;
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int count = Physics.SphereCastNonAlloc(
            from,
            Mathf.Max(0.05f, radiusMeters),
            delta / distance,
            WreckHits,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < count && i < WreckHits.Length; i++)
        {
            Collider collider = WreckHits[i].collider;
            if (collider == null)
            {
                continue;
            }

            CoreTacticalAutomatonWreck candidate = collider.GetComponentInParent<CoreTacticalAutomatonWreck>();
            if (candidate == null || candidate.currentHealth <= 0.001f)
            {
                continue;
            }

            if (WreckHits[i].distance >= bestDistance)
            {
                continue;
            }

            wreck = candidate;
            hitPosition = WreckHits[i].point.sqrMagnitude > 0.001f ? WreckHits[i].point : from + delta.normalized * WreckHits[i].distance;
            bestDistance = WreckHits[i].distance;
        }

        return wreck != null;
    }

    public static int ApplyExplosionDamageToWrecks(Vector3 center, float radiusMeters, float damageAmount, string source)
    {
        float radius = Mathf.Max(0.1f, radiusMeters);
        int hitCount = Physics.OverlapSphereNonAlloc(center, radius, ExplosionHits, ~0, QueryTriggerInteraction.Ignore);
        int damaged = 0;
        HashSet<CoreTacticalAutomatonWreck> unique = new HashSet<CoreTacticalAutomatonWreck>();
        for (int i = 0; i < hitCount && i < ExplosionHits.Length; i++)
        {
            Collider collider = ExplosionHits[i];
            if (collider == null)
            {
                continue;
            }

            CoreTacticalAutomatonWreck wreck = collider.GetComponentInParent<CoreTacticalAutomatonWreck>();
            if (wreck == null || wreck.currentHealth <= 0.001f || !unique.Add(wreck))
            {
                continue;
            }

            float distance = Vector3.Distance(center, wreck.transform.position);
            float falloff01 = Mathf.Clamp01(1f - distance / radius);
            float scaledDamage = Mathf.Max(1f, damageAmount * Mathf.Lerp(0.35f, 1f, falloff01));
            wreck.ApplyDamage(scaledDamage, source);
            damaged++;
        }

        return damaged;
    }

    public static List<CoreTacticalInventoryRow> BuildDefaultManifest(float sizeMeters)
    {
        List<CoreTacticalInventoryRow> rows = new List<CoreTacticalInventoryRow>();
        float size = Mathf.Max(1f, sizeMeters);
        AddManifestRow(rows, "automaton_relay", "Automaton relay", Mathf.Max(35f, size * 8f));
        AddManifestRow(rows, "automaton_coil", "Automaton coil", Mathf.Max(45f, size * 11f));

        if (size >= 7.5f)
        {
            AddManifestRow(rows, "automaton_mainspring", "Automaton mainspring", Mathf.Max(65f, size * 14f));
            AddManifestRow(rows, "automaton_servo_joint", "Automaton servo joint", Mathf.Max(90f, size * 20f));
        }

        if (size >= 15f)
        {
            AddManifestRow(rows, "automaton_gyroscope", "Automaton gyroscope", Mathf.Max(120f, size * 24f));
            AddManifestRow(rows, "automaton_logic_drum", "Automaton logic drum", Mathf.Max(160f, size * 30f));
            AddManifestRow(rows, "automaton_command_cylinder", "Automaton command cylinder", Mathf.Max(220f, size * 35f));
        }

        return rows;
    }

    private static void AddManifestRow(List<CoreTacticalInventoryRow> rows, string itemId, string name, float kg)
    {
        rows.Add(new CoreTacticalInventoryRow
        {
            itemId = itemId,
            displayName = name,
            amountKg = Mathf.Max(0.1f, kg)
        });
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalAutomatonWreckSpawner : MonoBehaviour
{
    private List<CoreTacticalInventoryRow> manifest = new List<CoreTacticalInventoryRow>();

    public string wreckDisplayName = "Automaton wreck";
    public float wreckMassKg = 100f;
    public float wreckHealth = 80f;
    public float accessDifficultyPercent = 15f;
    public float visualRadiusMeters = 4f;

    public void Configure(
        string displayName,
        float massKg,
        float health,
        float accessDifficulty,
        float visualRadius,
        IReadOnlyList<CoreTacticalInventoryRow> salvageManifest)
    {
        wreckDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Automaton wreck" : displayName.Trim();
        wreckMassKg = Mathf.Max(1f, massKg);
        wreckHealth = Mathf.Max(1f, health);
        accessDifficultyPercent = accessDifficulty;
        visualRadiusMeters = Mathf.Max(0.5f, visualRadius);
        manifest = new List<CoreTacticalInventoryRow>();
        if (salvageManifest != null)
        {
            for (int i = 0; i < salvageManifest.Count; i++)
            {
                CoreTacticalInventoryRow row = salvageManifest[i];
                if (row.amountKg > 0.001f)
                {
                    manifest.Add(row);
                }
            }
        }
    }

    public CoreTacticalAutomatonWreck SpawnWreck()
    {
        GameObject wreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wreckObject.name = wreckDisplayName;
        wreckObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
        wreckObject.transform.localScale = new Vector3(visualRadiusMeters * 1.5f, visualRadiusMeters * 0.75f, visualRadiusMeters * 2f);

        Renderer renderer = wreckObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(
                new Color(0.30f, 0.28f, 0.25f, 1f),
                0.12f);
        }

        Rigidbody body = wreckObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = wreckObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.mass = Mathf.Max(1f, wreckMassKg);

        if (wreckObject.GetComponent<CoreTacticalObstacle>() == null)
        {
            wreckObject.AddComponent<CoreTacticalObstacle>();
        }

        CoreTacticalAutomatonWreck wreck = wreckObject.AddComponent<CoreTacticalAutomatonWreck>();
        wreck.Initialize(
            wreckDisplayName,
            wreckMassKg,
            wreckHealth,
            accessDifficultyPercent,
            manifest,
            visualRadiusMeters);
        return wreck;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalMiningRig : MonoBehaviour
{
    private const float DefaultBatteryCapacity = 120f;
    private const float DefaultGeneratorEnergyPerSecond = 10f;

    private sealed class DirtyOreStack
    {
        public string oreItemId = "";
        public string displayName = "";
        public float rawKg;
        public float usefulKg;
    }

    private sealed class RawCloudCondensateStack
    {
        public string condensateItemId = "";
        public string displayName = "";
        public float rawLiters;
        public float usefulLiters;

        public float UsefulConcentration01 => rawLiters > 0.001f
            ? Mathf.Clamp01(usefulLiters / rawLiters)
            : 0f;

        public float RemoveWater(float requestedLiters)
        {
            float waterLiters = Mathf.Max(0f, rawLiters - usefulLiters);
            float removed = Mathf.Min(waterLiters, Mathf.Max(0f, requestedLiters));
            rawLiters = Mathf.Max(usefulLiters, rawLiters - removed);
            return removed;
        }
    }

    private sealed class MagnetDeliveryChannel
    {
        public readonly int sideSign;
        public readonly string beamObjectName;
        public CoreTacticalOreFragment activeFragment;
        public CoreTacticalAutomatonWreck activeWreck;
        public float salvageCycleTimer;
        public CoreTacticalUtilityBeamVisual beamVisual;

        public MagnetDeliveryChannel(int sideSign, string beamObjectName)
        {
            this.sideSign = sideSign < 0 ? -1 : 1;
            this.beamObjectName = beamObjectName ?? "Core Tactical Mining Magnet Beam";
        }
    }

    private readonly List<DirtyOreStack> dirtyOre = new List<DirtyOreStack>();
    private readonly List<RawCloudCondensateStack> rawCloudCondensate = new List<RawCloudCondensateStack>();
    private readonly List<CoreTacticalInventoryRow> startingInventoryRows = new List<CoreTacticalInventoryRow>();
    private readonly Dictionary<string, float> cleanOreKgByItem = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> cleanOreNameByItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> automatonSalvageKgByItem = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> automatonSalvageUnitsByItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> automatonSalvageNameByItem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> pendingRuntimeDepositKgByItem = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly MagnetDeliveryChannel[] magnetDeliveryChannels =
    {
        new MagnetDeliveryChannel(-1, "Core Tactical Mining Magnet Beam Left"),
        new MagnetDeliveryChannel(1, "Core Tactical Mining Magnet Beam Right")
    };

    public CoreTacticalShipMotor ownerShip;
    public MetaGameState metaGameState;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;

    [Header("Installed Modules")]
    public bool magnetInstalled;
    public bool drillInstalled;
    public bool crusherInstalled;
    public bool siphonInstalled;
    public bool cloudConcentratorInstalled;

    [Header("Inventory")]
    public float inventoryCapacityKg = 40000f;
    public float startingPayloadKg;
    public string lastInventoryMessage = "";

    [Header("Energy")]
    public float batteryCapacity = DefaultBatteryCapacity;
    public float batteryCurrent = DefaultBatteryCapacity;
    public float generatorEnergyPerSecond = DefaultGeneratorEnergyPerSecond;

    [Header("Magnet")]
    public bool magnetEnabled;
    public float magnetRangeMeters = 500f;
    public float magnetPullSpeedMS = 90f;
    public float magnetMaxChunkMassKg = 160f;
    public float magnetEnergyPerSecondPerFragment = 3.5f;
    public float magnetSideArcDegrees = 175f;
    public bool magnetBrownout;
    public bool magnetCargoFull;

    [Header("Automaton Salvage")]
    public float salvageWreckRangeMeters = 1000f;
    public float salvageWreckPullSpeedMS = 120f;
    public float salvageWreckMaxHeldMassKg;
    public float salvageWreckTowForceKg = 200f;
    public float salvageWreckHoldForceMultiplier = 10f;
    public float salvageWreckCycleSeconds = 5f;
    public float salvageAccessRatingPercent = 22f;
    public float salvageAccessBuildupOnFailurePercent = 6f;
    public float salvageWreckEnergyPerSecond = 3.5f;
    public float salvageHoldDistanceExtraMeters = 50f;
    public bool salvageMagnetInstalled;
    public string activeSalvageTargetName = "";

    [Header("Drill")]
    public bool drillEnabled;
    public float drillRangeMeters = 460f;
    public float drillDamagePerSecond = 14f;
    public float drillEnergyPerSecond = 16f;
    public float drillLossReduction01 = 0.40f;
    public bool drillBrownout;
    public bool drillCargoFull;
    public string activeDrillTargetName = "";

    [Header("Crusher")]
    public bool crusherEnabled;
    public float crusherCycleSeconds = 5f;
    public float crusherRawKgPerCycle = 20f;
    public float crusherEnergyPerCycle = 18f;
    public bool crusherBrownout;

    [Header("Cloud Siphon")]
    public bool siphonEnabled;
    public int siphonChannelCount = 1;
    public float siphonLitersPerSecond = 24f;
    public float siphonEnergyPerSecond = 9f;
    public bool siphonBrownout;
    public bool siphonCargoFull;
    public string activeSiphonTargetName = "";

    [Header("Cloud Concentrator")]
    public bool cloudConcentratorEnabled;
    public float cloudConcentratorCycleSeconds = 5f;
    public float cloudConcentratorWaterLitersPerCycle = 20f;
    public float cloudConcentratorEnergyPerCycle = 12f;
    public bool cloudConcentratorBrownout;

    private float crusherTimer;
    private float cloudConcentratorTimer;
    private int lastDepositedWholeKg;
    private float totalOreCollectedKg;
    private float totalCloudCondensateCollectedLiters;
    private CoreTacticalUtilityBeamVisual drillBeamVisual;
    private CoreTacticalSiphonIntakeVisual siphonIntakeVisual;

    public float Battery01 => Mathf.Clamp01(batteryCurrent / Mathf.Max(1f, batteryCapacity));
    public float DirtyOreKg
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < dirtyOre.Count; i++)
            {
                total += dirtyOre[i].rawKg;
            }

            return total;
        }
    }

    public float CleanOreKg
    {
        get
        {
            float total = 0f;
            foreach (KeyValuePair<string, float> pair in cleanOreKgByItem)
            {
                total += pair.Value;
            }

            return total;
        }
    }

    public float RawCloudCondensateLiters
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < rawCloudCondensate.Count; i++)
            {
                total += rawCloudCondensate[i].rawLiters;
            }

            return total;
        }
    }

    public int LastDepositedWholeKg => lastDepositedWholeKg;
    public float TotalOreCollectedKg => Mathf.Max(0f, totalOreCollectedKg);
    public float TotalCloudCondensateCollectedLiters => Mathf.Max(0f, totalCloudCondensateCollectedLiters);
    public float AutomatonSalvageKg
    {
        get
        {
            float total = 0f;
            foreach (KeyValuePair<string, float> pair in automatonSalvageKgByItem)
            {
                total += Mathf.Max(0f, pair.Value);
            }

            return total;
        }
    }

    public float InventoryCapacityKg => Mathf.Max(1f, inventoryCapacityKg);
    public float InventoryUsedKg => Mathf.Max(0f, startingPayloadKg + DirtyOreKg + CleanOreKg + RawCloudCondensateLiters + AutomatonSalvageKg);
    public float InventoryFreeKg => Mathf.Max(0f, InventoryCapacityKg - InventoryUsedKg);
    public string LastInventoryMessage => lastInventoryMessage;
    public bool HasAnyInstalledMiningModule => magnetInstalled || drillInstalled || crusherInstalled || siphonInstalled || cloudConcentratorInstalled;

    public void Initialize(CoreTacticalShipMotor owner, MetaGameState meta, CoreTacticalCombatTeam enemyTeam, float capacityKg = 0f, float initialPayloadKg = 0f)
    {
        ownerShip = owner != null ? owner : GetComponent<CoreTacticalShipMotor>();
        metaGameState = meta;
        targetTeam = enemyTeam;
        inventoryCapacityKg = Mathf.Max(1f, capacityKg > 0f ? capacityKg : inventoryCapacityKg);
        startingPayloadKg = Mathf.Clamp(Mathf.Max(0f, initialPayloadKg), 0f, inventoryCapacityKg);
        batteryCapacity = Mathf.Max(1f, batteryCapacity);
        batteryCurrent = batteryCapacity;
    }

    private void Awake()
    {
        ownerShip ??= GetComponent<CoreTacticalShipMotor>();
        batteryCapacity = Mathf.Max(1f, batteryCapacity);
        batteryCurrent = Mathf.Clamp(batteryCurrent, 0f, batteryCapacity);
    }

    public void SetInstalledModules(bool hasMagnet, bool hasDrill, bool hasCrusher, bool hasSiphon = false, bool hasCloudConcentrator = false)
    {
        bool wasMagnetInstalled = magnetInstalled;
        bool wasDrillInstalled = drillInstalled;
        bool wasCrusherInstalled = crusherInstalled;
        bool wasSiphonInstalled = siphonInstalled;
        bool wasCloudConcentratorInstalled = cloudConcentratorInstalled;
        bool wasMagnetEnabled = magnetEnabled;
        bool wasDrillEnabled = drillEnabled;
        bool wasCrusherEnabled = crusherEnabled;
        bool wasSiphonEnabled = siphonEnabled;
        bool wasCloudConcentratorEnabled = cloudConcentratorEnabled;

        magnetInstalled = hasMagnet;
        drillInstalled = hasDrill;
        crusherInstalled = hasCrusher;
        siphonInstalled = hasSiphon;
        cloudConcentratorInstalled = hasCloudConcentrator;

        magnetEnabled = magnetInstalled && (!wasMagnetInstalled || wasMagnetEnabled);
        drillEnabled = drillInstalled && (!wasDrillInstalled || wasDrillEnabled);
        crusherEnabled = crusherInstalled && (!wasCrusherInstalled || wasCrusherEnabled);
        siphonEnabled = siphonInstalled && (!wasSiphonInstalled || wasSiphonEnabled);
        cloudConcentratorEnabled = cloudConcentratorInstalled && (!wasCloudConcentratorInstalled || wasCloudConcentratorEnabled);

        if (!magnetInstalled)
        {
            magnetBrownout = false;
            magnetCargoFull = false;
            salvageMagnetInstalled = false;
            activeSalvageTargetName = "";
            for (int i = 0; i < magnetDeliveryChannels.Length; i++)
            {
                if (magnetDeliveryChannels[i] != null)
                {
                    if (magnetDeliveryChannels[i].activeWreck != null)
                    {
                        magnetDeliveryChannels[i].activeWreck.SetCaptured(false);
                    }

                    magnetDeliveryChannels[i].activeFragment = null;
                    magnetDeliveryChannels[i].activeWreck = null;
                    magnetDeliveryChannels[i].salvageCycleTimer = 0f;
                    magnetDeliveryChannels[i].beamVisual?.Hide();
                }
            }
        }

        if (!drillInstalled)
        {
            drillBrownout = false;
            drillCargoFull = false;
            activeDrillTargetName = "";
        }

        if (!crusherInstalled)
        {
            crusherBrownout = false;
            crusherTimer = 0f;
        }

        if (!siphonInstalled)
        {
            siphonBrownout = false;
            siphonCargoFull = false;
            activeSiphonTargetName = "";
            siphonIntakeVisual?.Hide();
        }
        else if (!siphonEnabled)
        {
            activeSiphonTargetName = "";
            siphonIntakeVisual?.Hide();
        }

        if (!cloudConcentratorInstalled)
        {
            cloudConcentratorBrownout = false;
            cloudConcentratorTimer = 0f;
        }
    }

    private void Update()
    {
        ownerShip ??= GetComponent<CoreTacticalShipMotor>();
        float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
        if (deltaSeconds <= 0f || ownerShip == null)
        {
            return;
        }

        batteryCurrent = Mathf.Min(batteryCapacity, batteryCurrent + Mathf.Max(0f, generatorEnergyPerSecond) * deltaSeconds);
        magnetBrownout = false;
        drillBrownout = false;
        crusherBrownout = false;
        siphonBrownout = false;
        cloudConcentratorBrownout = false;
        activeDrillTargetName = "";
        activeSiphonTargetName = "";
        activeSalvageTargetName = "";

        UpdateMagnet(deltaSeconds);
        UpdateDrill(deltaSeconds);
        UpdateCrusher(deltaSeconds);
        UpdateSiphon(deltaSeconds);
        UpdateCloudConcentrator(deltaSeconds);
    }

    public void ToggleModule(CoreTacticalMiningModule module)
    {
        if (!IsModuleInstalled(module))
        {
            return;
        }

        switch (module)
        {
            case CoreTacticalMiningModule.Magnet:
                magnetEnabled = !magnetEnabled;
                magnetCargoFull = false;
                break;
            case CoreTacticalMiningModule.Drill:
                drillEnabled = !drillEnabled;
                drillCargoFull = false;
                break;
            case CoreTacticalMiningModule.Crusher:
                crusherEnabled = !crusherEnabled;
                break;
            case CoreTacticalMiningModule.Siphon:
                siphonEnabled = !siphonEnabled;
                siphonCargoFull = false;
                break;
            case CoreTacticalMiningModule.CloudConcentrator:
                cloudConcentratorEnabled = !cloudConcentratorEnabled;
                break;
        }
    }

    public string GetModuleStatus(CoreTacticalMiningModule module)
    {
        if (!IsModuleInstalled(module))
        {
            return "N/A";
        }

        switch (module)
        {
            case CoreTacticalMiningModule.Magnet:
                return !magnetEnabled ? magnetCargoFull ? "FULL" : "OFF" : magnetBrownout ? "POWER" : "ON";
            case CoreTacticalMiningModule.Drill:
                return !drillEnabled ? drillCargoFull ? "FULL" : "OFF" : drillBrownout ? "POWER" : "ON";
            case CoreTacticalMiningModule.Crusher:
                return !crusherEnabled ? "OFF" : crusherBrownout ? "POWER" : "ON";
            case CoreTacticalMiningModule.Siphon:
                return !siphonEnabled ? siphonCargoFull ? "FULL" : "OFF" : siphonBrownout ? "POWER" : "ON";
            case CoreTacticalMiningModule.CloudConcentrator:
                return !cloudConcentratorEnabled ? "OFF" : cloudConcentratorBrownout ? "POWER" : "ON";
            default:
                return "";
        }
    }

    public bool IsModuleInstalled(CoreTacticalMiningModule module)
    {
        switch (module)
        {
            case CoreTacticalMiningModule.Magnet:
                return magnetInstalled;
            case CoreTacticalMiningModule.Drill:
                return drillInstalled;
            case CoreTacticalMiningModule.Crusher:
                return crusherInstalled;
            case CoreTacticalMiningModule.Siphon:
                return siphonInstalled;
            case CoreTacticalMiningModule.CloudConcentrator:
                return cloudConcentratorInstalled;
            default:
                return false;
        }
    }

    public bool IsModuleEnabled(CoreTacticalMiningModule module)
    {
        if (!IsModuleInstalled(module))
        {
            return false;
        }

        switch (module)
        {
            case CoreTacticalMiningModule.Magnet:
                return magnetEnabled;
            case CoreTacticalMiningModule.Drill:
                return drillEnabled;
            case CoreTacticalMiningModule.Crusher:
                return crusherEnabled;
            case CoreTacticalMiningModule.Siphon:
                return siphonEnabled;
            case CoreTacticalMiningModule.CloudConcentrator:
                return cloudConcentratorEnabled;
            default:
                return false;
        }
    }

    public bool IsModuleBrownout(CoreTacticalMiningModule module)
    {
        if (!IsModuleInstalled(module))
        {
            return false;
        }

        switch (module)
        {
            case CoreTacticalMiningModule.Magnet:
                return magnetBrownout;
            case CoreTacticalMiningModule.Drill:
                return drillBrownout;
            case CoreTacticalMiningModule.Crusher:
                return crusherBrownout;
            case CoreTacticalMiningModule.Siphon:
                return siphonBrownout;
            case CoreTacticalMiningModule.CloudConcentrator:
                return cloudConcentratorBrownout;
            default:
                return false;
        }
    }

    public float GetModuleCooldownRemainingSeconds(CoreTacticalMiningModule module)
    {
        switch (module)
        {
            case CoreTacticalMiningModule.Crusher:
                if (!crusherInstalled || !crusherEnabled || crusherBrownout || DirtyOreKg <= 0.001f)
                {
                    return 0f;
                }

                return Mathf.Max(0f, Mathf.Max(0.1f, crusherCycleSeconds) - crusherTimer);
            case CoreTacticalMiningModule.CloudConcentrator:
                if (!cloudConcentratorInstalled
                    || !cloudConcentratorEnabled
                    || cloudConcentratorBrownout
                    || RawCloudCondensateLiters <= 0.001f
                    || !HasConcentratorWaterToRemove())
                {
                    return 0f;
                }

                return Mathf.Max(0f, Mathf.Max(0.1f, cloudConcentratorCycleSeconds) - cloudConcentratorTimer);
            default:
                return 0f;
        }
    }

    public float GetModuleCooldown01(CoreTacticalMiningModule module)
    {
        float cycleSeconds;
        switch (module)
        {
            case CoreTacticalMiningModule.Crusher:
                cycleSeconds = Mathf.Max(0.1f, crusherCycleSeconds);
                break;
            case CoreTacticalMiningModule.CloudConcentrator:
                cycleSeconds = Mathf.Max(0.1f, cloudConcentratorCycleSeconds);
                break;
            default:
                return 0f;
        }

        float remainingSeconds = GetModuleCooldownRemainingSeconds(module);
        return remainingSeconds > 0.05f ? Mathf.Clamp01(remainingSeconds / cycleSeconds) : 0f;
    }

    public string BuildCargoLine()
    {
        return "Cargo: " + InventoryUsedKg.ToString("0") + " / " + InventoryCapacityKg.ToString("0") + " kg";
    }

    public string BuildEnergyLine()
    {
        return "Energy: " + batteryCurrent.ToString("0") + " / " + batteryCapacity.ToString("0") + " | Gen " + generatorEnergyPerSecond.ToString("0") + "/s";
    }

    public string BuildInstalledModuleStatusLine()
    {
        List<string> parts = new List<string>();
        if (magnetInstalled)
        {
            parts.Add("M " + GetModuleStatus(CoreTacticalMiningModule.Magnet));
        }

        if (drillInstalled)
        {
            parts.Add("L " + GetModuleStatus(CoreTacticalMiningModule.Drill));
        }

        if (crusherInstalled)
        {
            parts.Add("C " + GetModuleStatus(CoreTacticalMiningModule.Crusher));
        }

        if (siphonInstalled)
        {
            parts.Add("S " + GetModuleStatus(CoreTacticalMiningModule.Siphon));
        }

        if (cloudConcentratorInstalled)
        {
            parts.Add("K " + GetModuleStatus(CoreTacticalMiningModule.CloudConcentrator));
        }

        return parts.Count > 0 ? "Mining: " + string.Join(" | ", parts) : "Mining: no equipment";
    }

    public void BuildInventoryRows(List<CoreTacticalInventoryRow> rows)
    {
        if (rows == null)
        {
            return;
        }

        rows.Clear();
        if (startingInventoryRows.Count > 0)
        {
            for (int i = 0; i < startingInventoryRows.Count; i++)
            {
                CoreTacticalInventoryRow row = startingInventoryRows[i];
                if (row.amountKg > 0.001f)
                {
                    rows.Add(row);
                }
            }
        }
        else if (startingPayloadKg > 0.001f)
        {
            rows.Add(new CoreTacticalInventoryRow
            {
                displayName = "Existing payload",
                amountKg = startingPayloadKg
            });
        }

        for (int i = 0; i < dirtyOre.Count; i++)
        {
            DirtyOreStack stack = dirtyOre[i];
            if (stack == null || stack.rawKg <= 0.001f)
            {
                continue;
            }

            rows.Add(new CoreTacticalInventoryRow
            {
                displayName = "Raw " + (string.IsNullOrWhiteSpace(stack.displayName) ? stack.oreItemId : stack.displayName),
                amountKg = stack.rawKg
            });
        }

        for (int i = 0; i < rawCloudCondensate.Count; i++)
        {
            RawCloudCondensateStack stack = rawCloudCondensate[i];
            if (stack == null || stack.rawLiters <= 0.001f)
            {
                continue;
            }

            rows.Add(new CoreTacticalInventoryRow
            {
                displayName = "Raw cloud " + (string.IsNullOrWhiteSpace(stack.displayName) ? stack.condensateItemId : stack.displayName)
                    + " (" + (stack.UsefulConcentration01 * 100f).ToString("0.#") + "%)",
                amountKg = stack.rawLiters
            });
        }

        foreach (KeyValuePair<string, float> pair in cleanOreKgByItem)
        {
            if (pair.Value <= 0.001f)
            {
                continue;
            }

            cleanOreNameByItem.TryGetValue(pair.Key, out string displayName);
            rows.Add(new CoreTacticalInventoryRow
            {
                displayName = "Clean " + (string.IsNullOrWhiteSpace(displayName) ? pair.Key : displayName),
                amountKg = pair.Value
            });
        }

        foreach (KeyValuePair<string, float> pair in automatonSalvageKgByItem)
        {
            if (pair.Value <= 0.001f)
            {
                continue;
            }

            automatonSalvageNameByItem.TryGetValue(pair.Key, out string displayName);
            rows.Add(new CoreTacticalInventoryRow
            {
                itemId = pair.Key,
                displayName = string.IsNullOrWhiteSpace(displayName) ? pair.Key : displayName,
                amountKg = pair.Value
            });
        }
    }

    public bool TryFlushDirtyOreToRuntimeCargo(out string message)
    {
        message = "";
        if (dirtyOre.Count == 0 && rawCloudCondensate.Count == 0 && automatonSalvageUnitsByItem.Count == 0)
        {
            return true;
        }

        if (metaGameState == null)
        {
            message = "Cannot unload runtime cargo without active meta state.";
            return false;
        }

        for (int i = dirtyOre.Count - 1; i >= 0; i--)
        {
            DirtyOreStack stack = dirtyOre[i];
            if (stack == null || stack.rawKg <= 0.001f)
            {
                dirtyOre.RemoveAt(i);
                continue;
            }

            if (!metaGameState.TryAddShipLowGradeOreFromRuntime(
                    stack.oreItemId,
                    stack.rawKg,
                    stack.usefulKg,
                    stack.displayName,
                    out string reason))
            {
                message = string.IsNullOrWhiteSpace(reason) ? "Low-grade concentrate unload blocked." : reason;
                return false;
            }

            dirtyOre.RemoveAt(i);
        }

        for (int i = rawCloudCondensate.Count - 1; i >= 0; i--)
        {
            RawCloudCondensateStack stack = rawCloudCondensate[i];
            if (stack == null || stack.rawLiters <= 0.001f)
            {
                rawCloudCondensate.RemoveAt(i);
                continue;
            }

            if (!metaGameState.TryAddShipRawCloudCondensateFromRuntime(
                    stack.condensateItemId,
                    stack.rawLiters,
                    stack.usefulLiters,
                    stack.displayName,
                    out string reason))
            {
                message = string.IsNullOrWhiteSpace(reason) ? "Raw cloud condensate unload blocked." : reason;
                return false;
            }

            rawCloudCondensate.RemoveAt(i);
        }

        List<string> emptiedSalvageItems = new List<string>();
        foreach (KeyValuePair<string, int> pair in automatonSalvageUnitsByItem)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
            {
                emptiedSalvageItems.Add(pair.Key);
                continue;
            }

            if (!metaGameState.TryAddShipCargoFromRuntime(pair.Key, pair.Value, out string reason))
            {
                message = string.IsNullOrWhiteSpace(reason) ? "Automaton salvage unload blocked." : reason;
                return false;
            }

            emptiedSalvageItems.Add(pair.Key);
        }

        for (int i = 0; i < emptiedSalvageItems.Count; i++)
        {
            string itemId = emptiedSalvageItems[i];
            automatonSalvageUnitsByItem.Remove(itemId);
            automatonSalvageKgByItem.Remove(itemId);
            automatonSalvageNameByItem.Remove(itemId);
        }

        lastInventoryMessage = "";
        message = "Low-grade concentrate, raw cloud condensate, and automaton salvage unloaded to ship cargo.";
        return true;
    }

    public void SetStartingInventoryRows(List<CoreTacticalInventoryRow> rows)
    {
        startingInventoryRows.Clear();
        if (rows == null)
        {
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            CoreTacticalInventoryRow row = rows[i];
            if (row.amountKg > 0.001f)
            {
                startingInventoryRows.Add(row);
            }
        }
    }

    private void UpdateMagnet(float deltaSeconds)
    {
        if (!magnetInstalled)
        {
            return;
        }

        for (int i = 0; i < magnetDeliveryChannels.Length; i++)
        {
            MagnetDeliveryChannel channel = magnetDeliveryChannels[i];
            if (channel == null)
            {
                continue;
            }

            if (channel.activeWreck != null)
            {
                ContinueWreckSalvage(channel, deltaSeconds);
            }

            if (channel.activeFragment != null)
            {
                ContinuePaidMagnetDelivery(channel, deltaSeconds);
            }
        }

        if (!magnetEnabled)
        {
            return;
        }

        CoreTacticalAutomatonWreck[] wrecks = salvageMagnetInstalled
            ? FindObjectsByType<CoreTacticalAutomatonWreck>(FindObjectsSortMode.None)
            : null;
        CoreTacticalOreFragment[] fragments = FindObjectsByType<CoreTacticalOreFragment>(FindObjectsSortMode.None);

        for (int channelIndex = 0; channelIndex < magnetDeliveryChannels.Length; channelIndex++)
        {
            MagnetDeliveryChannel channel = magnetDeliveryChannels[channelIndex];
            if (channel == null || channel.activeFragment != null || channel.activeWreck != null)
            {
                continue;
            }

            if (TryStartWreckSalvage(channel, wrecks, deltaSeconds))
            {
                continue;
            }

            TryStartMagnetDelivery(channel, fragments, deltaSeconds);
        }
    }

    private bool TryStartMagnetDelivery(MagnetDeliveryChannel channel, CoreTacticalOreFragment[] fragments, float deltaSeconds)
    {
        if (channel == null || fragments == null || fragments.Length == 0)
        {
            return false;
        }

        CoreTacticalOreFragment bestFragment = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < fragments.Length; i++)
        {
            CoreTacticalOreFragment fragment = fragments[i];
            if (fragment == null || fragment.captured || fragment.rawMassKg <= 0f || fragment.rawMassKg > magnetMaxChunkMassKg)
            {
                continue;
            }

            if (!IsInsideMagnetSideArc(channel.sideSign, fragment.transform.position))
            {
                continue;
            }

            Vector3 catchPoint = GetMagnetCatchPoint(channel.sideSign, fragment.transform.position);
            float distance = Vector3.Distance(fragment.transform.position, catchPoint);
            if (distance > magnetRangeMeters || distance >= bestDistance)
            {
                continue;
            }

            bestFragment = fragment;
            bestDistance = distance;
        }

        if (bestFragment == null)
        {
            return false;
        }

        if (!CanAcceptCargoKg(bestFragment.rawMassKg))
        {
            SetCargoFull(CoreTacticalMiningModule.Magnet, bestFragment.rawMassKg);
            return false;
        }

        float energyCost = CalculateMagnetDeliveryEnergyCost(bestDistance);
        if (!TrySpendEnergy(energyCost))
        {
            magnetBrownout = true;
            lastInventoryMessage = "Magnet waiting: need " + energyCost.ToString("0.0") + " energy for delivery.";
            return false;
        }

        bestFragment.captured = true;
        channel.activeFragment = bestFragment;
        ContinuePaidMagnetDelivery(channel, deltaSeconds);
        return channel.activeFragment != null;
    }

    private bool TryStartWreckSalvage(MagnetDeliveryChannel channel, CoreTacticalAutomatonWreck[] wrecks, float deltaSeconds)
    {
        if (channel == null || wrecks == null || wrecks.Length == 0)
        {
            return false;
        }

        CoreTacticalAutomatonWreck bestWreck = null;
        float bestDistance = float.PositiveInfinity;
        float range = GetSalvageWreckRangeMeters();
        for (int i = 0; i < wrecks.Length; i++)
        {
            CoreTacticalAutomatonWreck wreck = wrecks[i];
            if (wreck == null
                || wreck.captured
                || !wreck.HasSalvage
                || wreck.currentHealth <= 0.001f
                || (salvageWreckMaxHeldMassKg > 0f && wreck.massKg > Mathf.Max(1f, salvageWreckMaxHeldMassKg)))
            {
                continue;
            }

            if (!IsInsideMagnetSideArc(channel.sideSign, wreck.transform.position))
            {
                continue;
            }

            Vector3 beamOrigin = GetSalvageBeamOrigin(channel.sideSign, wreck.transform.position);
            float distance = Vector3.Distance(wreck.transform.position, beamOrigin);
            if (distance > range || distance >= bestDistance)
            {
                continue;
            }

            bestWreck = wreck;
            bestDistance = distance;
        }

        if (bestWreck == null)
        {
            return false;
        }

        float energyCost = Mathf.Max(0f, salvageWreckEnergyPerSecond) * Mathf.Max(0.05f, deltaSeconds);
        if (!TrySpendEnergy(energyCost))
        {
            magnetBrownout = true;
            lastInventoryMessage = "Salvage beam waiting: no energy.";
            return false;
        }

        GetSalvageBeamOrigin(channel.sideSign, bestWreck.transform.position, true);
        bestWreck.SetCaptured(true, ownerShip != null ? ownerShip.name : "", CalculateSalvageFallSpeedMultiplier(bestWreck));
        channel.activeWreck = bestWreck;
        channel.salvageCycleTimer = 0f;
        ContinueWreckSalvage(channel, deltaSeconds);
        return channel.activeWreck != null;
    }

    private void ContinueWreckSalvage(MagnetDeliveryChannel channel, float deltaSeconds)
    {
        if (channel == null)
        {
            return;
        }

        CoreTacticalAutomatonWreck wreck = channel.activeWreck;
        if (wreck == null || wreck.currentHealth <= 0.001f)
        {
            channel.activeWreck = null;
            channel.salvageCycleTimer = 0f;
            channel.beamVisual?.Hide();
            return;
        }

        wreck.SetCaptured(true, ownerShip != null ? ownerShip.name : "", CalculateSalvageFallSpeedMultiplier(wreck));
        Vector3 beamOrigin = GetSalvageBeamOrigin(channel.sideSign, wreck.transform.position);
        float beamLength = Vector3.Distance(beamOrigin, wreck.transform.position);
        if (beamLength > GetSalvageWreckRangeMeters())
        {
            ReleaseWreckSalvage(channel, wreck, "Salvage beam snapped: wreck left beam range.");
            return;
        }

        float energyCost = Mathf.Max(0f, salvageWreckEnergyPerSecond) * Mathf.Max(0f, deltaSeconds);
        if (!TrySpendEnergy(energyCost))
        {
            magnetBrownout = true;
            ReleaseWreckSalvage(channel, wreck, "Salvage beam snapped: no energy.");
            return;
        }

        activeSalvageTargetName = wreck.displayName;
        ShowUtilityBeam(ref channel.beamVisual, CoreTacticalUtilityBeamPalette.Salvage, channel.beamObjectName, beamOrigin, wreck.transform.position);

        Vector3 holdPoint = GetSalvageHoldPoint(channel.sideSign, wreck);
        wreck.PullToward(holdPoint, CalculateSalvageTowSpeedMS(wreck) * Mathf.Max(0f, deltaSeconds), false);

        channel.salvageCycleTimer += Mathf.Max(0f, deltaSeconds);
        float cycleSeconds = Mathf.Max(0.1f, salvageWreckCycleSeconds);
        if (channel.salvageCycleTimer < cycleSeconds)
        {
            return;
        }

        channel.salvageCycleTimer = 0f;
        if (!wreck.HasSalvage)
        {
            channel.activeWreck = null;
            Destroy(wreck.gameObject);
            lastInventoryMessage = "Automaton wreck stripped clean.";
            return;
        }

        if (wreck.TryExtractSalvage(
                salvageAccessRatingPercent,
                salvageAccessBuildupOnFailurePercent,
                out CoreTacticalInventoryRow extractedRow,
                out bool emptied,
                out float chancePercent))
        {
            if (!AddAutomatonSalvage(extractedRow))
            {
                ReleaseWreckSalvage(channel, wreck, "Cargo full: automaton salvage left in wreck.");
                return;
            }

            lastInventoryMessage = "Salvaged " + FormatInventoryRowName(extractedRow) + " (" + chancePercent.ToString("0.#") + "% access).";
        }
        else
        {
            lastInventoryMessage = "Salvage access failed: +" + Mathf.Max(0f, salvageAccessBuildupOnFailurePercent).ToString("0.#") + "% buildup.";
        }

        if (emptied || !wreck.HasSalvage)
        {
            channel.activeWreck = null;
            channel.salvageCycleTimer = 0f;
            channel.beamVisual?.Hide();
            Destroy(wreck.gameObject);
        }
    }

    private void ContinuePaidMagnetDelivery(MagnetDeliveryChannel channel, float deltaSeconds)
    {
        if (channel == null)
        {
            return;
        }

        CoreTacticalOreFragment fragment = channel.activeFragment;
        if (fragment == null)
        {
            channel.activeFragment = null;
            return;
        }

        Vector3 catchPoint = GetMagnetCatchPoint(channel.sideSign, fragment.transform.position, true);
        if (!IsMagnetDeliveryInRange(fragment, catchPoint))
        {
            ReleaseMagnetDelivery(channel, fragment, "Magnet released: fragment left beam range.");
            return;
        }

        ShowUtilityBeam(ref channel.beamVisual, CoreTacticalUtilityBeamPalette.Magnet, channel.beamObjectName, catchPoint, fragment.transform.position);
        if (!fragment.PullToward(catchPoint, magnetPullSpeedMS * deltaSeconds, true))
        {
            return;
        }

        if (AddDirtyOre(fragment))
        {
            channel.activeFragment = null;
            Destroy(fragment.gameObject);
            return;
        }

        fragment.captured = false;
        channel.activeFragment = null;
    }

    private bool IsMagnetDeliveryInRange(CoreTacticalOreFragment fragment, Vector3 catchPoint)
    {
        if (fragment == null)
        {
            return false;
        }

        float beamLengthMeters = Vector3.Distance(fragment.transform.position, catchPoint);
        return beamLengthMeters <= Mathf.Max(1f, magnetRangeMeters);
    }

    private void ReleaseMagnetDelivery(MagnetDeliveryChannel channel, CoreTacticalOreFragment fragment, string message)
    {
        if (fragment != null)
        {
            fragment.captured = false;
        }

        if (channel != null)
        {
            channel.activeFragment = null;
            channel.beamVisual?.Hide();
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            lastInventoryMessage = message;
        }
    }

    private void ReleaseWreckSalvage(MagnetDeliveryChannel channel, CoreTacticalAutomatonWreck wreck, string message)
    {
        if (wreck != null)
        {
            wreck.SetCaptured(false);
        }

        if (channel != null)
        {
            channel.activeWreck = null;
            channel.salvageCycleTimer = 0f;
            channel.beamVisual?.Hide();
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            lastInventoryMessage = message;
        }
    }

    private float GetSalvageWreckRangeMeters()
    {
        float configuredRange = Mathf.Max(1f, salvageWreckRangeMeters);
        float moduleRange = Mathf.Max(1f, magnetRangeMeters);
        return Mathf.Min(configuredRange, moduleRange);
    }

    private Vector3 GetSalvageBeamOrigin(int sideSign, Vector3 targetPosition, bool rotateBinding = false)
    {
        return GetUtilityModuleCatchPoint("salvage_magnet", sideSign, targetPosition, rotateBinding);
    }

    private Vector3 GetSalvageHoldPoint(int sideSign, CoreTacticalAutomatonWreck wreck)
    {
        if (ownerShip == null)
        {
            return transform.position;
        }

        Vector3 hullSize = ownerShip.hullSizeMeters;
        float aftOffset = Mathf.Max(15f, hullSize.z * 0.5f + Mathf.Max(0f, salvageHoldDistanceExtraMeters));
        float sideOffset = Mathf.Max(8f, hullSize.x * 0.55f + (wreck != null ? wreck.VisualRadiusMeters : 4f));
        float upOffset = Mathf.Max(4f, hullSize.y * 0.25f);
        Vector3 sideDirection = GetVisualSideDirection(sideSign);
        if (sideDirection.sqrMagnitude <= 0.0001f)
        {
            sideDirection = ownerShip.transform.right * (sideSign < 0 ? -1f : 1f);
        }

        return ownerShip.transform.position
            - ownerShip.transform.forward * aftOffset
            + sideDirection.normalized * sideOffset
            + ownerShip.transform.up * upOffset;
    }

    private float CalculateSalvageTowSpeedMS(CoreTacticalAutomatonWreck wreck)
    {
        float forceKg = Mathf.Max(0f, salvageWreckTowForceKg);
        if (wreck == null || forceKg <= 0f)
        {
            return 0f;
        }

        float massKg = Mathf.Max(1f, wreck.massKg);
        float maxSpeed = Mathf.Max(0f, salvageWreckPullSpeedMS);
        return Mathf.Min(maxSpeed, maxSpeed * forceKg / massKg);
    }

    private float CalculateSalvageFallSpeedMultiplier(CoreTacticalAutomatonWreck wreck)
    {
        if (wreck == null)
        {
            return 1f;
        }

        float massKg = Mathf.Max(1f, wreck.massKg);
        float holdRatio = Mathf.Clamp01(Mathf.Max(0f, salvageWreckTowForceKg) * Mathf.Max(1f, salvageWreckHoldForceMultiplier) / massKg);
        return Mathf.Min(0.1f, Mathf.Lerp(1f, 0.02f, holdRatio));
    }

    private bool AddAutomatonSalvage(CoreTacticalInventoryRow row)
    {
        float amountKg = Mathf.Max(0f, row.amountKg);
        if (amountKg <= 0.001f)
        {
            return true;
        }

        if (!CanAcceptCargoKg(amountKg))
        {
            SetCargoFull(CoreTacticalMiningModule.Magnet, amountKg);
            return false;
        }

        string itemId = string.IsNullOrWhiteSpace(row.itemId) ? FormatInventoryRowName(row) : row.itemId.Trim();
        string displayName = FormatInventoryRowName(row);
        automatonSalvageKgByItem[itemId] = automatonSalvageKgByItem.TryGetValue(itemId, out float currentKg)
            ? currentKg + amountKg
            : amountKg;
        automatonSalvageUnitsByItem[itemId] = automatonSalvageUnitsByItem.TryGetValue(itemId, out int currentUnits)
            ? currentUnits + 1
            : 1;
        automatonSalvageNameByItem[itemId] = displayName;
        return true;
    }

    private static string FormatInventoryRowName(CoreTacticalInventoryRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.displayName))
        {
            return row.displayName.Trim();
        }

        return string.IsNullOrWhiteSpace(row.itemId) ? "Automaton salvage" : row.itemId.Trim();
    }

    private float CalculateMagnetDeliveryEnergyCost(float distanceMeters)
    {
        float deliverySeconds = Mathf.Max(0.05f, Mathf.Max(0f, distanceMeters) / Mathf.Max(1f, magnetPullSpeedMS));
        return Mathf.Max(0f, magnetEnergyPerSecondPerFragment) * deliverySeconds;
    }

    private void UpdateDrill(float deltaSeconds)
    {
        if (!drillInstalled || !drillEnabled)
        {
            return;
        }

        CoreTacticalOreBoulder boulder = ResolveDrillTarget();
        if (boulder == null)
        {
            return;
        }

        float distance = Vector3.Distance(ownerShip.transform.position, boulder.transform.position);
        if (distance > drillRangeMeters)
        {
            return;
        }

        float estimatedCleanKg = boulder.EstimateDrillCleanOutputKg(drillDamagePerSecond * deltaSeconds, drillLossReduction01);
        if (estimatedCleanKg > 0.001f && !CanAcceptCargoKg(estimatedCleanKg))
        {
            SetCargoFull(CoreTacticalMiningModule.Drill, estimatedCleanKg);
            return;
        }

        float energyCost = drillEnergyPerSecond * deltaSeconds;
        if (!TrySpendEnergy(energyCost))
        {
            drillBrownout = true;
            drillEnabled = false;
            lastInventoryMessage = "Drill stopped: no energy.";
            return;
        }

        activeDrillTargetName = boulder.OreDisplayName;
        ShowUtilityBeam(ref drillBeamVisual, CoreTacticalUtilityBeamPalette.Drill, "Core Tactical Mining Drill Beam", GetUtilityBeamOrigin(), boulder.transform.position);
        float cleanKg = boulder.DrillExtract(drillDamagePerSecond * deltaSeconds, drillLossReduction01, out string oreItemId);
        AddCleanOre(oreItemId, cleanKg, boulder.OreDisplayName);
    }

    private void ShowUtilityBeam(
        ref CoreTacticalUtilityBeamVisual visual,
        CoreTacticalUtilityBeamPalette palette,
        string objectName,
        Vector3 start,
        Vector3 end)
    {
        if (ownerShip == null)
        {
            return;
        }

        visual ??= CoreTacticalUtilityBeamVisual.GetOrCreate(ownerShip.transform, objectName, palette);
        visual?.Show(start, end);
    }

    private Vector3 GetUtilityBeamOrigin()
    {
        return ownerShip != null
            ? ownerShip.transform.position + ownerShip.transform.up * Mathf.Max(4f, ownerShip.hullSizeMeters.y * 0.48f)
            : transform.position;
    }

    private Vector3 GetUtilityBeamOrigin(int sideSign)
    {
        if (ownerShip == null)
        {
            return transform.position;
        }

        float sideOffset = Mathf.Max(3f, ownerShip.hullSizeMeters.x * 0.35f);
        return GetUtilityBeamOrigin() + ownerShip.transform.right * (sideSign < 0 ? -sideOffset : sideOffset);
    }

    private Vector3 GetMagnetCatchPoint(int sideSign, Vector3 targetPosition, bool rotateVisual = false)
    {
        return GetUtilityModuleCatchPoint("magnet", sideSign, targetPosition, rotateVisual);
    }

    private Vector3 GetUtilityModuleCatchPoint(string utilityKind, int sideSign, Vector3 targetPosition, bool rotateVisual = false)
    {
        if (ownerShip == null)
        {
            return transform.position;
        }

        CoreTacticalShipVisualWeaponBinding binding = ownerShip.GetComponent<CoreTacticalShipVisualWeaponBinding>();
        if (binding == null)
        {
            return GetUtilityBeamOrigin(sideSign);
        }

        if (!binding.TryBindUtilityModule(utilityKind, sideSign, out CoreTacticalVisualWeaponHandle handle))
        {
            return GetUtilityBeamOrigin(sideSign);
        }

        Vector3 fallback = GetUtilityBeamOrigin(sideSign);
        Vector3 direction = targetPosition - fallback;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = ownerShip.transform.forward;
        }

        if (rotateVisual)
        {
            CoreTacticalShipVisualWeaponBinding.ApplyYawToward(handle, ownerShip.transform, direction, sideSign < 0 ? -90f : 90f);
        }

        return CoreTacticalShipVisualWeaponBinding.GetMuzzlePosition(handle, fallback, direction);
    }

    private bool IsInsideMagnetSideArc(int sideSign, Vector3 targetPosition)
    {
        if (ownerShip == null)
        {
            return true;
        }

        Vector3 toTarget = targetPosition - ownerShip.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector3 sideDirection = GetVisualSideDirection(sideSign);
        sideDirection.y = 0f;
        if (sideDirection.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float halfArc = Mathf.Clamp(magnetSideArcDegrees, 1f, 360f) * 0.5f;
        return Vector3.Angle(sideDirection.normalized, toTarget.normalized) <= halfArc;
    }

    private Vector3 GetVisualSideDirection(int sideSign)
    {
        if (ownerShip == null)
        {
            return sideSign < 0 ? Vector3.left : Vector3.right;
        }

        // Authored Korshun left/right equipment names are mirrored relative to
        // the tactical motor's local +X, so side arcs must use the visual side.
        return ownerShip.transform.right * (sideSign < 0 ? 1f : -1f);
    }

    private CoreTacticalOreBoulder ResolveDrillTarget()
    {
        CoreTacticalPriorityTargetControl priority = ownerShip != null ? ownerShip.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        if (priority != null && priority.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor target) && target != null)
        {
            CoreTacticalOreBoulder boulder = target.GetComponent<CoreTacticalOreBoulder>();
            if (boulder != null)
            {
                return boulder;
            }
        }

        return null;
    }

    private void UpdateCrusher(float deltaSeconds)
    {
        if (!crusherInstalled || !crusherEnabled || DirtyOreKg <= 0.001f)
        {
            crusherTimer = 0f;
            return;
        }

        crusherTimer += deltaSeconds;
        if (crusherTimer < Mathf.Max(0.1f, crusherCycleSeconds))
        {
            return;
        }

        if (!TrySpendEnergy(crusherEnergyPerCycle))
        {
            crusherBrownout = true;
            crusherEnabled = false;
            lastInventoryMessage = "Crusher stopped: no energy.";
            return;
        }

        crusherTimer = 0f;
        ProcessCrusherCycle();
    }

    private void UpdateSiphon(float deltaSeconds)
    {
        if (!siphonInstalled || !siphonEnabled)
        {
            siphonIntakeVisual?.Hide();
            return;
        }

        CoreTacticalGasCloud[] clouds = FindObjectsByType<CoreTacticalGasCloud>(FindObjectsSortMode.None);
        if (clouds == null || clouds.Length == 0)
        {
            siphonIntakeVisual?.Hide();
            return;
        }

        int channels = Mathf.Max(1, siphonChannelCount);
        bool anyIntakeShown = false;
        for (int channel = 0; channel < channels; channel++)
        {
            if (!TryRunSiphonChannel(clouds, deltaSeconds))
            {
                continue;
            }

            anyIntakeShown = true;
        }

        if (!anyIntakeShown)
        {
            siphonIntakeVisual?.Hide();
        }
    }

    private bool TryRunSiphonChannel(CoreTacticalGasCloud[] clouds, float deltaSeconds)
    {
        CoreTacticalGasCloud cloud = ResolveBestSiphonCloud(clouds);
        if (cloud == null)
        {
            return false;
        }

        float requestedLiters = Mathf.Max(0f, siphonLitersPerSecond) * deltaSeconds;
        if (requestedLiters <= 0.001f)
        {
            return false;
        }

        if (InventoryFreeKg <= 0.001f)
        {
            SetCargoFull(CoreTacticalMiningModule.Siphon, requestedLiters);
            return false;
        }

        requestedLiters = Mathf.Min(requestedLiters, InventoryFreeKg);
        float energyCost = Mathf.Max(0f, siphonEnergyPerSecond) * deltaSeconds;
        if (!TrySpendEnergy(energyCost))
        {
            siphonBrownout = true;
            siphonEnabled = false;
            lastInventoryMessage = "Siphon stopped: no energy.";
            return false;
        }

        float extractedRaw = cloud.ExtractRawCondensate(requestedLiters, out float usefulLiters);
        if (extractedRaw <= 0.001f)
        {
            return false;
        }

        AddRawCloudCondensate(cloud.CondensateItemId, extractedRaw, usefulLiters, cloud.DisplayName);
        activeSiphonTargetName = cloud.DisplayName;
        ShowSiphonIntake(GetSiphonIntakeOrigin(cloud.transform.position), cloud.transform.position);
        return true;
    }

    private void ShowSiphonIntake(Vector3 origin, Vector3 targetPosition)
    {
        if (ownerShip == null)
        {
            return;
        }

        siphonIntakeVisual ??= CoreTacticalSiphonIntakeVisual.GetOrCreate(ownerShip.transform, "Core Tactical Cloud Siphon Intake");
        siphonIntakeVisual?.Show(origin, targetPosition);
    }

    private Vector3 GetSiphonIntakeOrigin(Vector3 targetPosition)
    {
        if (ownerShip == null)
        {
            return transform.position;
        }

        CoreTacticalShipVisualWeaponBinding binding = ownerShip.GetComponent<CoreTacticalShipVisualWeaponBinding>();
        if (binding == null)
        {
            return GetUtilityBeamOrigin();
        }

        int sideSign = Vector3.Dot(targetPosition - ownerShip.transform.position, ownerShip.transform.right) >= 0f ? -1 : 1;
        Vector3 fallback = GetUtilityBeamOrigin(sideSign);
        if (!binding.TryBindUtilityModule("siphon", sideSign, out CoreTacticalVisualWeaponHandle handle))
        {
            return fallback;
        }

        Vector3 direction = targetPosition - fallback;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = ownerShip.transform.forward;
        }

        CoreTacticalShipVisualWeaponBinding.ApplyYawToward(handle, ownerShip.transform, direction, sideSign < 0 ? -90f : 90f);
        return CoreTacticalShipVisualWeaponBinding.GetMuzzlePosition(handle, fallback, direction);
    }

    private CoreTacticalGasCloud ResolveBestSiphonCloud(CoreTacticalGasCloud[] clouds)
    {
        if (clouds == null || ownerShip == null)
        {
            return null;
        }

        float radius = GetCloudCollectionRadiusMeters();
        CoreTacticalGasCloud best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < clouds.Length; i++)
        {
            CoreTacticalGasCloud cloud = clouds[i];
            if (cloud == null || !cloud.CanHarvest)
            {
                continue;
            }

            if (!cloud.OverlapsCollectionSphere(ownerShip.transform.position, radius))
            {
                continue;
            }

            float distance = Vector3.Distance(ownerShip.transform.position, cloud.transform.position);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = cloud;
            bestDistance = distance;
        }

        return best;
    }

    public float GetCloudCollectionRadiusMeters()
    {
        if (ownerShip == null)
        {
            return 1f;
        }

        Vector3 size = ownerShip.hullSizeMeters;
        return Mathf.Max(1f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.5f);
    }

    public bool AddRawCloudCondensateForTests(string condensateItemId, float rawLiters, float usefulLiters, string displayName = "")
    {
        return AddRawCloudCondensate(condensateItemId, rawLiters, usefulLiters, displayName);
    }

    public void UpdateSiphonForTests(float deltaSeconds)
    {
        UpdateSiphon(Mathf.Max(0f, deltaSeconds));
    }

    public float ProcessCloudConcentratorCycleForTests()
    {
        return ProcessCloudConcentratorCycle();
    }

    public void SetModuleCycleTimerForTests(CoreTacticalMiningModule module, float elapsedSeconds)
    {
        switch (module)
        {
            case CoreTacticalMiningModule.Crusher:
                crusherTimer = Mathf.Clamp(elapsedSeconds, 0f, Mathf.Max(0.1f, crusherCycleSeconds));
                break;
            case CoreTacticalMiningModule.CloudConcentrator:
                cloudConcentratorTimer = Mathf.Clamp(elapsedSeconds, 0f, Mathf.Max(0.1f, cloudConcentratorCycleSeconds));
                break;
        }
    }

    public float GetRawCloudCondensateRawLitersForTests(string condensateItemId)
    {
        RawCloudCondensateStack stack = FindRawCloudCondensateStack(condensateItemId);
        return stack != null ? stack.rawLiters : 0f;
    }

    public float GetRawCloudCondensateConcentrationForTests(string condensateItemId)
    {
        RawCloudCondensateStack stack = FindRawCloudCondensateStack(condensateItemId);
        return stack != null ? stack.UsefulConcentration01 : 0f;
    }

    private bool AddRawCloudCondensate(string condensateItemId, float rawLiters, float usefulLiters, string displayName)
    {
        if (string.IsNullOrWhiteSpace(condensateItemId) || rawLiters <= 0f)
        {
            return false;
        }

        if (!CanAcceptCargoKg(rawLiters))
        {
            SetCargoFull(CoreTacticalMiningModule.Siphon, rawLiters);
            return false;
        }

        RawCloudCondensateStack stack = FindRawCloudCondensateStack(condensateItemId);
        if (stack == null)
        {
            stack = new RawCloudCondensateStack
            {
                condensateItemId = condensateItemId.Trim(),
                displayName = string.IsNullOrWhiteSpace(displayName) ? condensateItemId.Trim() : displayName.Trim()
            };
            rawCloudCondensate.Add(stack);
        }

        stack.rawLiters += Mathf.Max(0f, rawLiters);
        stack.usefulLiters = Mathf.Clamp(stack.usefulLiters + Mathf.Max(0f, usefulLiters), 0f, stack.rawLiters);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            stack.displayName = displayName.Trim();
        }

        totalCloudCondensateCollectedLiters += Mathf.Max(0f, rawLiters);
        lastInventoryMessage = "";
        return true;
    }

    private RawCloudCondensateStack FindRawCloudCondensateStack(string condensateItemId)
    {
        if (string.IsNullOrWhiteSpace(condensateItemId))
        {
            return null;
        }

        for (int i = 0; i < rawCloudCondensate.Count; i++)
        {
            RawCloudCondensateStack stack = rawCloudCondensate[i];
            if (stack != null && string.Equals(stack.condensateItemId, condensateItemId, StringComparison.OrdinalIgnoreCase))
            {
                return stack;
            }
        }

        return null;
    }

    private void UpdateCloudConcentrator(float deltaSeconds)
    {
        if (!cloudConcentratorInstalled || !cloudConcentratorEnabled || RawCloudCondensateLiters <= 0.001f)
        {
            cloudConcentratorTimer = 0f;
            return;
        }

        cloudConcentratorTimer += deltaSeconds;
        if (cloudConcentratorTimer < Mathf.Max(0.1f, cloudConcentratorCycleSeconds))
        {
            return;
        }

        if (!HasConcentratorWaterToRemove())
        {
            cloudConcentratorTimer = 0f;
            return;
        }

        if (!TrySpendEnergy(cloudConcentratorEnergyPerCycle))
        {
            cloudConcentratorBrownout = true;
            cloudConcentratorEnabled = false;
            lastInventoryMessage = "Cloud concentrator stopped: no energy.";
            return;
        }

        cloudConcentratorTimer = 0f;
        float removed = ProcessCloudConcentratorCycle();
        if (removed > 0.001f)
        {
            lastInventoryMessage = "Cloud concentrator vented " + removed.ToString("0.#") + " l water.";
        }
    }

    private bool HasConcentratorWaterToRemove()
    {
        for (int i = 0; i < rawCloudCondensate.Count; i++)
        {
            RawCloudCondensateStack stack = rawCloudCondensate[i];
            if (stack != null && stack.rawLiters - stack.usefulLiters > 0.001f)
            {
                return true;
            }
        }

        return false;
    }

    private float ProcessCloudConcentratorCycle()
    {
        float remainingCapacity = Mathf.Max(0f, cloudConcentratorWaterLitersPerCycle);
        float removedTotal = 0f;
        for (int i = rawCloudCondensate.Count - 1; i >= 0 && remainingCapacity > 0.001f; i--)
        {
            RawCloudCondensateStack stack = rawCloudCondensate[i];
            if (stack == null || stack.rawLiters <= 0.001f)
            {
                rawCloudCondensate.RemoveAt(i);
                continue;
            }

            float concentration = stack.UsefulConcentration01;
            float requestedWater = Mathf.Min(remainingCapacity, cloudConcentratorWaterLitersPerCycle * (1f - concentration));
            float removed = stack.RemoveWater(requestedWater);
            removedTotal += removed;
            remainingCapacity -= removed;
            if (stack.rawLiters <= 0.001f)
            {
                rawCloudCondensate.RemoveAt(i);
            }
        }

        return removedTotal;
    }

    private void ProcessCrusherCycle()
    {
        float remainingToProcessKg = Mathf.Max(0f, crusherRawKgPerCycle);
        for (int i = dirtyOre.Count - 1; i >= 0 && remainingToProcessKg > 0.001f; i--)
        {
            DirtyOreStack stack = dirtyOre[i];
            if (stack == null || stack.rawKg <= 0f)
            {
                dirtyOre.RemoveAt(i);
                continue;
            }

            float processedKg = Mathf.Min(stack.rawKg, remainingToProcessKg);
            float usefulRatio = stack.rawKg > 0f ? Mathf.Clamp01(stack.usefulKg / stack.rawKg) : 0f;
            AddCleanOre(stack.oreItemId, processedKg * usefulRatio, stack.displayName, false);
            stack.rawKg -= processedKg;
            stack.usefulKg = Mathf.Max(0f, stack.usefulKg - processedKg * usefulRatio);
            remainingToProcessKg -= processedKg;
            if (stack.rawKg <= 0.001f)
            {
                dirtyOre.RemoveAt(i);
            }
        }
    }

    public bool AddDirtyOreForTests(string oreItemId, float rawKg, float usefulKg, string displayName = "")
    {
        if (string.IsNullOrWhiteSpace(oreItemId) || rawKg <= 0f)
        {
            return false;
        }

        if (!CanAcceptCargoKg(rawKg))
        {
            SetCargoFull(CoreTacticalMiningModule.Magnet, rawKg);
            return false;
        }

        DirtyOreStack stack = null;
        for (int i = 0; i < dirtyOre.Count; i++)
        {
            if (string.Equals(dirtyOre[i].oreItemId, oreItemId, StringComparison.OrdinalIgnoreCase))
            {
                stack = dirtyOre[i];
                break;
            }
        }

        if (stack == null)
        {
            stack = new DirtyOreStack
            {
                oreItemId = oreItemId.Trim(),
                displayName = string.IsNullOrWhiteSpace(displayName) ? oreItemId.Trim() : displayName.Trim()
            };
            dirtyOre.Add(stack);
        }

        stack.rawKg += Mathf.Max(0f, rawKg);
        stack.usefulKg = Mathf.Clamp(stack.usefulKg + Mathf.Max(0f, usefulKg), 0f, stack.rawKg);
        totalOreCollectedKg += Mathf.Max(0f, rawKg);
        lastInventoryMessage = "";
        return true;
    }

    public void UpdateMagnetForTests(float deltaSeconds)
    {
        UpdateMagnet(Mathf.Max(0f, deltaSeconds));
    }

    public bool AddAutomatonSalvageForTests(string itemId, string displayName, float amountKg)
    {
        return AddAutomatonSalvage(new CoreTacticalInventoryRow
        {
            itemId = itemId,
            displayName = displayName,
            amountKg = amountKg
        });
    }

    public float GetAutomatonSalvageKgForTests(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0f;
        }

        return automatonSalvageKgByItem.TryGetValue(itemId, out float amountKg) ? amountKg : 0f;
    }

    public int GetAutomatonSalvageUnitsForTests(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return automatonSalvageUnitsByItem.TryGetValue(itemId, out int units) ? units : 0;
    }

    private bool AddDirtyOre(CoreTacticalOreFragment fragment)
    {
        if (fragment == null || string.IsNullOrWhiteSpace(fragment.oreItemId) || fragment.rawMassKg <= 0f)
        {
            return false;
        }

        if (!CanAcceptCargoKg(fragment.rawMassKg))
        {
            SetCargoFull(CoreTacticalMiningModule.Magnet, fragment.rawMassKg);
            return false;
        }

        DirtyOreStack stack = null;
        for (int i = 0; i < dirtyOre.Count; i++)
        {
            if (string.Equals(dirtyOre[i].oreItemId, fragment.oreItemId, StringComparison.OrdinalIgnoreCase))
            {
                stack = dirtyOre[i];
                break;
            }
        }

        if (stack == null)
        {
            stack = new DirtyOreStack
            {
                oreItemId = fragment.oreItemId,
                displayName = fragment.displayName
            };
            dirtyOre.Add(stack);
        }

        stack.rawKg += Mathf.Max(0f, fragment.rawMassKg);
        stack.usefulKg += Mathf.Max(0f, fragment.usefulOreKg);
        totalOreCollectedKg += Mathf.Max(0f, fragment.rawMassKg);
        lastInventoryMessage = "";
        return true;
    }

    private void AddCleanOre(string oreItemId, float amountKg, string displayName = "", bool countMissionProgress = true)
    {
        if (string.IsNullOrWhiteSpace(oreItemId) || amountKg <= 0f)
        {
            return;
        }

        if (!cleanOreKgByItem.ContainsKey(oreItemId))
        {
            cleanOreKgByItem[oreItemId] = 0f;
        }

        cleanOreKgByItem[oreItemId] += amountKg;
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            cleanOreNameByItem[oreItemId] = displayName;
        }

        if (countMissionProgress)
        {
            totalOreCollectedKg += Mathf.Max(0f, amountKg);
        }

        lastInventoryMessage = "";
        DepositWholeKgToRuntimeCargo(oreItemId, amountKg);
    }

    private void DepositWholeKgToRuntimeCargo(string oreItemId, float amountKg)
    {
        if (metaGameState == null || amountKg <= 0f)
        {
            return;
        }

        pendingRuntimeDepositKgByItem.TryGetValue(oreItemId, out float pending);
        pending += amountKg;
        int wholeKg = Mathf.FloorToInt(pending);
        if (wholeKg <= 0)
        {
            pendingRuntimeDepositKgByItem[oreItemId] = pending;
            return;
        }

        if (metaGameState.TryAddShipCargoFromRuntime(oreItemId, wholeKg, out string reason))
        {
            pending -= wholeKg;
            lastDepositedWholeKg = wholeKg;
        }
        else
        {
            lastInventoryMessage = string.IsNullOrWhiteSpace(reason) ? "Runtime cargo deposit blocked." : reason;
        }

        pendingRuntimeDepositKgByItem[oreItemId] = pending;
    }

    private bool CanAcceptCargoKg(float amountKg)
    {
        return amountKg <= 0f || InventoryFreeKg + 0.001f >= amountKg;
    }

    private void SetCargoFull(CoreTacticalMiningModule module, float requiredKg)
    {
        lastInventoryMessage = "No cargo space: need " + Mathf.Max(0f, requiredKg).ToString("0.0") + " kg, free " + InventoryFreeKg.ToString("0.0") + " kg.";
        if (module == CoreTacticalMiningModule.Magnet)
        {
            magnetEnabled = false;
            magnetCargoFull = true;
        }
        else if (module == CoreTacticalMiningModule.Drill)
        {
            drillEnabled = false;
            drillCargoFull = true;
        }
        else if (module == CoreTacticalMiningModule.Siphon)
        {
            siphonEnabled = false;
            siphonCargoFull = true;
        }
    }

    private bool TrySpendEnergy(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
        {
            return true;
        }

        if (batteryCurrent + 0.0001f < amount)
        {
            return false;
        }

        batteryCurrent = Mathf.Max(0f, batteryCurrent - amount);
        return true;
    }
}
