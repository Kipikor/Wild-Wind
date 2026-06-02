using System;
using System.Collections.Generic;
using UnityEngine;

public enum ExtractionDamageElement
{
    Physical,
    Fire,
    Electric,
    Ichor,
    Cold,
    Explosion
}

public enum ShellDeliveryMode
{
    ArmorPiercing,
    HighExplosive
}

public enum ExtractionObjectKind
{
    Boulder,
    Cloud,
    Leviathan,
    Drone,
    Relic,
    Hackable,
    Wreck
}

public enum ScanPattern
{
    Passive,
    Omnidirectional,
    Directional
}

public enum MiningDroneKind
{
    Catcher,
    Grabber,
    Burner,
    Tug,
    Disarmer,
    Opener,
    Syringe,
    Scanner,
    Salvager
}

public enum DroneIchorProtection
{
    None,
    Basic,
    Advanced,
    Immune
}

public enum FireAuthorizationMode
{
    Safe,
    DangerousOnly,
    Everything
}

public enum VoucherType
{
    Coins,
    Experience,
    Reputation,
    SkillPoints
}

public enum VoucherRarity
{
    Common,
    Uncommon,
    Rare,
    Epic
}

public enum RecipeTier
{
    T2,
    T3
}

public sealed class OreMechanicsProfile
{
    public string oreItemId = "";
    public float usefulFraction = 0.25f;
    public float heatDestructionC = 500f;
    public bool burnsAsCoal;
    public bool containsClaudium;
    public bool isSublikat;
    public float claudiumFieldRadiusPerMeter = 0.0f;
}

public sealed class ExtractionBoulderState
{
    public string id = "";
    public OreMechanicsProfile ore = new OreMechanicsProfile();
    public float radiusMeters = 12f;
    public float maxHealth = 1000f;
    public float health = 1000f;
    public float crustMaxHealth;
    public float crustHealth;
    public float temperatureC = -100f;
    public bool armoredCrust;
    public bool explosiveGas;
    public bool iceCrust;
    public bool ichor;
    public bool relic;
    public bool relicExtracted;
    public bool relicDestroyed;
    public bool oreDestroyedByHeat;
    public bool safeDoNotTouch;
    public bool canShed = true;
    public float exposure;
    public float exposureRequired = 400f;
    public float lastShedMassKg;
    public int lastShedChunkCount;

    public float Health01 => maxHealth <= 0f ? 0f : Mathf.Clamp01(health / maxHealth);
    public float DensityKgPerM3 => ore != null && ore.isSublikat ? 800f : 2600f;
    public float MassKg => 4f / 3f * Mathf.PI * radiusMeters * radiusMeters * radiusMeters * DensityKgPerM3;
    public float UsefulOreKg => ore == null || oreDestroyedByHeat ? 0f : MassKg * Mathf.Clamp01(ore.usefulFraction);
    public bool CrustActive => armoredCrust && crustHealth > 0.001f;
    public bool IsDestroyed => health <= 0.001f;
    public float ClaudiumFieldRadiusMeters
    {
        get
        {
            if (ore == null || !ore.containsClaudium) return 0f;
            float oreFactor = Mathf.Lerp(1f, 2.8f, Mathf.Clamp01(ore.usefulFraction));
            return radiusMeters * Mathf.Max(0f, ore.claudiumFieldRadiusPerMeter) * oreFactor * Mathf.Sqrt(Mathf.Max(1f, radiusMeters));
        }
    }

    public ExtractionBoulderState Clone()
    {
        ExtractionBoulderState clone = (ExtractionBoulderState)MemberwiseClone();
        clone.ore = ore;
        return clone;
    }

    public void Normalize()
    {
        radiusMeters = Mathf.Max(0.1f, radiusMeters);
        maxHealth = Mathf.Max(1f, maxHealth);
        health = Mathf.Clamp(health <= 0f ? maxHealth : health, 0f, maxHealth);
        crustMaxHealth = Mathf.Max(0f, crustMaxHealth);
        crustHealth = Mathf.Clamp(crustHealth, 0f, crustMaxHealth);
        exposureRequired = Mathf.Max(1f, exposureRequired);
        exposure = Mathf.Max(0f, exposure);
        if (armoredCrust && crustMaxHealth <= 0f)
        {
            crustMaxHealth = maxHealth * 2.5f;
            crustHealth = crustMaxHealth;
        }

        if (ichor && armoredCrust)
        {
            armoredCrust = false;
            crustHealth = 0f;
            crustMaxHealth = 0f;
        }
    }
}

public sealed class ExtractionChunkState
{
    public string oreItemId = "";
    public float massKg = 100f;
    public float usefulFraction = 0.25f;
    public float fallSpeedMS = 20f;
    public bool ichorAcid;
    public bool destroyed;

    public float UsefulOreKg => Mathf.Max(0f, massKg) * Mathf.Clamp01(usefulFraction);
}

public sealed class ExtractionShipProfile
{
    public float hullHealth = 1000f;
    public float cargoCapacityKg = 500f;
    public float cargoUsedKg;
    public bool hasCrusher;
    public bool advancedCrusherCollectsIchor;
    public bool ramShieldActive;
    public bool passiveRamPlating;
    public bool ichorChemsealLowSlot;
    public float activeModulePowerDrawKw;
    public float noise;

    public float FreeCargoKg => Mathf.Max(0f, cargoCapacityKg - cargoUsedKg);
}

public sealed class ScanModuleProfile
{
    public string id = "";
    public ScanPattern pattern;
    public float exposurePerSecond = 10f;
    public float rangeMeters = 1000f;
    public float boulderMultiplier = 1f;
    public float cloudMultiplier = 1f;
    public float relicMultiplier = 1f;
    public float leviathanMultiplier = 1f;
    public float droneMultiplier = 1f;
    public float powerDrawKw;
}

public sealed class ProjectileProfile
{
    public ShellDeliveryMode delivery = ShellDeliveryMode.ArmorPiercing;
    public ExtractionDamageElement element = ExtractionDamageElement.Physical;
    public float damage = 100f;
    public float penetration = 1f;
    public bool triggersExplosiveGas = true;
}

public sealed class GasCloudMechanicsState
{
    public string id = "";
    public float radiusMeters = 80f;
    public float remainingGasUnits = 1000f;
    public float harvestUnitsPerSecondAtCenter = 10f;
    public float temperatureOffsetC;
    public float opacity01;
    public bool ichor;
    public bool hot;
    public bool cold;
    public bool electric;
    public float electricCharge;
    public float electricPulseThreshold = 10f;
    public Vector3 windVelocity = new Vector3(4f, 0f, 0f);
    public Vector3 center;
}

public sealed class EnvironmentRegionProfile
{
    public float ambientTemperatureC = 20f;
    public float fogOpacity01 = 0.1f;
    public float stormTopMeters = 1000f;
    public float worldHeightMeters = 100000f;
}

public sealed class ClaudiumFieldSource
{
    public Vector3 center;
    public float radiusMeters;
    public float strength;
}

public sealed class ScanResult
{
    public float exposureAdded;
    public float exposureTotal;
    public bool passportComplete;
}

public sealed class BoulderDamageResult
{
    public float healthDamage;
    public float crustDamage;
    public bool crustBroken;
    public bool exploded;
    public bool relicDestroyed;
    public bool pushedWithoutDamage;
    public int chunks;
    public float threatAdded;
}

public sealed class ChunkCatchResult
{
    public float rawRamDamage;
    public float finalRamDamage;
    public float ichorDamage;
    public float oreCollectedKg;
    public float ichorCollectedKg;
    public bool cargoOverflow;
    public bool chunkDestroyed;
}

public sealed class ThermalResult
{
    public float temperatureBeforeC;
    public float temperatureAfterC;
    public bool iceMelted;
    public bool explosiveDisarmed;
    public bool ichorBurnedOut;
    public bool oreDestroyed;
    public bool relicDestroyed;
    public bool overheated;
}

public sealed class CloudHarvestResult
{
    public float concentration01;
    public float harvestedUnits;
    public float thermalDamage;
    public float ichorDamage;
    public float electricDamage;
}

public sealed class EnvironmentSample
{
    public float temperatureC;
    public float visibility01;
    public float claudiumFieldStrength;
    public float airDensity;
    public float claudiumLiftFactor;
    public float stormDamagePerSecond;
}

public sealed class DroneTaskResult
{
    public bool success;
    public string task = "";
    public float oreDeliveredKg;
    public float ichorDeliveredKg;
    public float targetDamage;
    public float droneDamageTaken;
    public bool propertyRemoved;
    public bool playerAvoidedRamDamage;
}

public sealed class RelicExtractionResult
{
    public bool success;
    public bool relicDestroyed;
    public string reason = "";
}

public sealed class HackResult
{
    public bool canStartMinigame;
    public bool success;
    public float threatAdded;
    public bool shipDamaged;
}

public sealed class FireControlResult
{
    public bool allowed;
    public float hitChance;
    public string reason = "";
}

public sealed class SalvageResult
{
    public bool salvageAvailable;
    public bool caughtBeforeStorm;
    public bool requiresOnSiteDismantling;
}

public sealed class WildWindMechanicsCatalog
{
    public readonly Dictionary<string, OreMechanicsProfile> ores = new Dictionary<string, OreMechanicsProfile>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, ScanModuleProfile> scanners = new Dictionary<string, ScanModuleProfile>(StringComparer.OrdinalIgnoreCase);
    public readonly List<string> highModules = new List<string>();
    public readonly List<string> midModules = new List<string>();
    public readonly List<string> lowModules = new List<string>();
    public readonly List<string> drones = new List<string>();
}

public static class WildWindExtractionMechanics
{
    public const float RamNoDamageSpeedThresholdMS = 2f;
    public const float BoulderOverheatThresholdC = 180f;
    public const float LeviathanNormalTemperatureC = 100f;
    public const float LeviathanHeatDamageThresholdC = 150f;

    public static WildWindMechanicsCatalog CreateMinimalCatalog()
    {
        WildWindMechanicsCatalog catalog = new WildWindMechanicsCatalog();
        catalog.ores["ferron"] = new OreMechanicsProfile { oreItemId = "ferron", usefulFraction = 0.30f, heatDestructionC = 620f };
        catalog.ores["coal"] = new OreMechanicsProfile { oreItemId = "charcoal", usefulFraction = 0.22f, heatDestructionC = 260f, burnsAsCoal = true };
        catalog.ores["ichorite"] = new OreMechanicsProfile { oreItemId = "ichor", usefulFraction = 0.18f, heatDestructionC = 420f };
        catalog.ores["claudite"] = new OreMechanicsProfile { oreItemId = "claudium", usefulFraction = 0.20f, heatDestructionC = 520f, containsClaudium = true, claudiumFieldRadiusPerMeter = 4f };
        catalog.ores["sublikat"] = new OreMechanicsProfile { oreItemId = "sublikat", usefulFraction = 1f, heatDestructionC = 900f, isSublikat = true };

        catalog.scanners["passive_belly_scanner"] = new ScanModuleProfile { id = "passive_belly_scanner", pattern = ScanPattern.Passive, exposurePerSecond = 3f, rangeMeters = 450f, boulderMultiplier = 0.7f, cloudMultiplier = 0.7f, relicMultiplier = 0.45f };
        catalog.scanners["omni_mid_scanner"] = new ScanModuleProfile { id = "omni_mid_scanner", pattern = ScanPattern.Omnidirectional, exposurePerSecond = 40f, rangeMeters = 1200f, boulderMultiplier = 1.2f, cloudMultiplier = 1f, relicMultiplier = 0.8f, leviathanMultiplier = 0.8f };
        catalog.scanners["directional_high_scanner"] = new ScanModuleProfile { id = "directional_high_scanner", pattern = ScanPattern.Directional, exposurePerSecond = 120f, rangeMeters = 2600f, boulderMultiplier = 3f, cloudMultiplier = 0.25f, relicMultiplier = 1.2f, droneMultiplier = 1.2f, powerDrawKw = 12f };

        catalog.highModules.AddRange(new[]
        {
            "crusher",
            "advanced_ichor_crusher",
            "vibro_ram",
            "flamethrower",
            "harpoon",
            "gas_harvester",
            "directional_high_scanner",
            "cannon"
        });
        catalog.midModules.AddRange(new[] { "active_ram_shield", "crane", "omni_mid_scanner", "hacker", "heater", "cooler" });
        catalog.lowModules.AddRange(new[] { "passive_ram_plating", "thermal_insulation", "ichor_chemseal", "passive_belly_scanner" });
        catalog.drones.AddRange(new[] { "catcher", "grabber", "burner", "tug", "disarmer", "opener", "syringe", "scanner", "salvager" });
        return catalog;
    }

    public static ExtractionBoulderState CreateBoulder(string id, OreMechanicsProfile ore, float radiusMeters, bool armored = false, bool explosive = false, bool ice = false, bool ichor = false, bool relic = false)
    {
        ExtractionBoulderState boulder = new ExtractionBoulderState
        {
            id = id,
            ore = ore,
            radiusMeters = Mathf.Max(0.1f, radiusMeters),
            maxHealth = Mathf.Max(100f, radiusMeters * radiusMeters * 10f),
            temperatureC = ice ? -80f : -20f,
            armoredCrust = armored,
            explosiveGas = explosive,
            iceCrust = ice,
            ichor = ichor,
            relic = relic,
            canShed = !ice && (ore == null || !ore.isSublikat),
            exposureRequired = 250f + Mathf.Pow(Mathf.Max(1f, radiusMeters), 1.35f) * 18f
        };
        boulder.health = boulder.maxHealth;
        if (armored)
        {
            boulder.crustMaxHealth = boulder.maxHealth * 4f;
            boulder.crustHealth = boulder.crustMaxHealth;
        }

        boulder.Normalize();
        return boulder;
    }

    public static ExtractionChunkState ShedChunk(ExtractionBoulderState boulder, float requestedMassKg, float fallSpeedMS)
    {
        if (boulder == null || boulder.ore == null) return null;
        float mass = Mathf.Clamp(requestedMassKg, 1f, Mathf.Max(1f, boulder.MassKg));
        return new ExtractionChunkState
        {
            oreItemId = boulder.ore.oreItemId,
            massKg = mass,
            usefulFraction = boulder.oreDestroyedByHeat ? 0f : boulder.ore.usefulFraction,
            fallSpeedMS = Mathf.Max(0f, fallSpeedMS),
            ichorAcid = boulder.ichor
        };
    }

    public static ScanResult ApplyScanExposure(ScanModuleProfile scanner, ExtractionBoulderState target, float seconds, float distanceMeters, float visibility01, bool targetInsideCone)
    {
        if (scanner == null || target == null)
        {
            return new ScanResult();
        }

        target.Normalize();
        float patternFactor = scanner.pattern == ScanPattern.Directional
            ? (targetInsideCone ? 1f : 0f)
            : scanner.pattern == ScanPattern.Omnidirectional ? 0.65f : 0.25f;
        float distanceFactor = Mathf.Pow(scanner.rangeMeters / Mathf.Max(scanner.rangeMeters, scanner.rangeMeters + Mathf.Max(0f, distanceMeters)), 2f);
        float sizePenalty = 1f / Mathf.Max(1f, Mathf.Pow(target.radiusMeters / 12f, 0.35f));
        float added = Mathf.Max(0f, scanner.exposurePerSecond)
            * Mathf.Max(0f, seconds)
            * Mathf.Max(0f, scanner.boulderMultiplier)
            * Mathf.Clamp01(visibility01)
            * patternFactor
            * distanceFactor
            * sizePenalty;
        target.exposure = Mathf.Min(target.exposureRequired, target.exposure + added);
        return new ScanResult
        {
            exposureAdded = added,
            exposureTotal = target.exposure,
            passportComplete = target.exposure >= target.exposureRequired - 0.001f
        };
    }

    public static BoulderDamageResult ApplyProjectileDamage(ExtractionBoulderState boulder, ProjectileProfile projectile, bool directHit)
    {
        BoulderDamageResult result = new BoulderDamageResult();
        if (boulder == null || projectile == null || boulder.IsDestroyed)
        {
            return result;
        }

        boulder.Normalize();
        if (boulder.relic && directHit && projectile.element != ExtractionDamageElement.Cold)
        {
            boulder.relicDestroyed = true;
            result.relicDestroyed = true;
        }

        if (boulder.explosiveGas && directHit && projectile.triggersExplosiveGas && projectile.element != ExtractionDamageElement.Cold)
        {
            result.exploded = true;
            result.threatAdded = 80f;
            result.chunks = Mathf.Max(8, Mathf.CeilToInt(boulder.radiusMeters * 3f));
            boulder.health = 0f;
            if (boulder.relic)
            {
                boulder.relicDestroyed = true;
                result.relicDestroyed = true;
            }

            return result;
        }

        float damage = Mathf.Max(0f, projectile.damage);
        if (boulder.temperatureC >= BoulderOverheatThresholdC)
        {
            damage *= 2f;
        }

        if (boulder.CrustActive)
        {
            float deliveryMultiplier = projectile.delivery == ShellDeliveryMode.ArmorPiercing ? 1.5f : 0.65f;
            float elementMultiplier = projectile.element switch
            {
                ExtractionDamageElement.Electric => 0.1f,
                ExtractionDamageElement.Ichor => 6f,
                ExtractionDamageElement.Fire => boulder.temperatureC >= BoulderOverheatThresholdC ? 2f : 0.25f,
                ExtractionDamageElement.Explosion => 0.75f,
                _ => 1f
            };
            float crustDamage = damage * deliveryMultiplier * elementMultiplier * Mathf.Max(0.1f, projectile.penetration);
            boulder.crustHealth = Mathf.Max(0f, boulder.crustHealth - crustDamage);
            result.crustDamage = crustDamage;
            result.crustBroken = boulder.crustHealth <= 0.001f;
            if (result.crustBroken)
            {
                boulder.armoredCrust = false;
            }

            return result;
        }

        float healthDamage = damage * GetBoulderHealthElementMultiplier(projectile.element);
        boulder.health = Mathf.Max(0f, boulder.health - healthDamage);
        result.healthDamage = healthDamage;
        result.chunks = EstimateShedChunks(boulder, healthDamage);
        boulder.lastShedChunkCount = result.chunks;
        boulder.lastShedMassKg = Mathf.Min(boulder.MassKg * 0.08f, Mathf.Max(0f, healthDamage * boulder.radiusMeters * 0.1f));
        if (boulder.IsDestroyed && boulder.relic && !boulder.relicExtracted)
        {
            boulder.relicDestroyed = true;
            result.relicDestroyed = true;
        }

        return result;
    }

    public static BoulderDamageResult ApplyRamToBoulder(ExtractionBoulderState boulder, float sourceMassKg, float sourceSpeedMS, bool vibroRamActive)
    {
        BoulderDamageResult result = new BoulderDamageResult();
        if (boulder == null || boulder.IsDestroyed)
        {
            return result;
        }

        boulder.Normalize();
        if (sourceSpeedMS < RamNoDamageSpeedThresholdMS)
        {
            result.pushedWithoutDamage = true;
            return result;
        }

        if (boulder.relic)
        {
            boulder.relicDestroyed = true;
            result.relicDestroyed = true;
        }

        float energyKJ = 0.5f * Mathf.Max(0f, sourceMassKg) * sourceSpeedMS * sourceSpeedMS / 1000f;
        float damage = Mathf.Sqrt(Mathf.Max(0f, energyKJ)) * 12f;
        if (vibroRamActive)
        {
            damage *= 2f;
        }

        if (boulder.CrustActive)
        {
            float crustDamage = damage * (vibroRamActive ? 10f : 1f);
            boulder.crustHealth = Mathf.Max(0f, boulder.crustHealth - crustDamage);
            result.crustDamage = crustDamage;
            result.crustBroken = boulder.crustHealth <= 0.001f;
            if (result.crustBroken)
            {
                boulder.armoredCrust = false;
            }
        }
        else
        {
            boulder.health = Mathf.Max(0f, boulder.health - damage);
            result.healthDamage = damage;
            result.chunks = EstimateShedChunks(boulder, damage);
        }

        result.threatAdded = 12f + sourceSpeedMS;
        return result;
    }

    public static ChunkCatchResult ResolveChunkShipContact(ExtractionChunkState chunk, ExtractionShipProfile ship, bool crusherPlateContact)
    {
        ChunkCatchResult result = new ChunkCatchResult();
        if (chunk == null || ship == null || chunk.destroyed)
        {
            return result;
        }

        float rawDamage = CalculateRamDamage(chunk.massKg, chunk.fallSpeedMS);
        float ramReduction = CombineReductions(
            ship.ramShieldActive ? 0.5f : 0f,
            ship.passiveRamPlating ? 0.2f : 0f);
        float finalRam = rawDamage * (1f - ramReduction);
        float ichorDamage = chunk.ichorAcid ? rawDamage * (1f - (ship.ichorChemsealLowSlot ? 0.2f : 0f)) * 0.75f : 0f;

        result.rawRamDamage = rawDamage;
        result.finalRamDamage = finalRam;
        result.ichorDamage = ichorDamage;
        ship.hullHealth = Mathf.Max(0f, ship.hullHealth - finalRam - ichorDamage);

        if (crusherPlateContact && ship.hasCrusher)
        {
            float ore = Mathf.Floor(chunk.UsefulOreKg);
            float accepted = Mathf.Min(ore, ship.FreeCargoKg);
            result.oreCollectedKg = accepted;
            result.cargoOverflow = accepted + 0.001f < ore;
            ship.cargoUsedKg += accepted;
            if (chunk.ichorAcid && ship.advancedCrusherCollectsIchor)
            {
                result.ichorCollectedKg = Mathf.Floor(chunk.massKg * 0.08f);
            }
        }

        chunk.destroyed = true;
        result.chunkDestroyed = true;
        return result;
    }

    public static bool ShootChunkToDust(ExtractionChunkState chunk)
    {
        if (chunk == null || chunk.destroyed) return false;
        chunk.destroyed = true;
        return true;
    }

    public static ThermalResult ApplyHeat(ExtractionBoulderState boulder, float energyKJ, float ambientTemperatureC, bool fromFlamethrower)
    {
        ThermalResult result = new ThermalResult();
        if (boulder == null)
        {
            return result;
        }

        boulder.Normalize();
        result.temperatureBeforeC = boulder.temperatureC;
        if (fromFlamethrower && boulder.relic && !boulder.relicExtracted)
        {
            boulder.relicDestroyed = true;
            result.relicDestroyed = true;
        }

        float thermalMass = Mathf.Max(1f, boulder.MassKg * 0.75f);
        float deltaC = Mathf.Max(0f, energyKJ) / thermalMass;
        boulder.temperatureC += deltaC;

        if (fromFlamethrower && boulder.explosiveGas)
        {
            boulder.explosiveGas = false;
            result.explosiveDisarmed = true;
        }

        if (fromFlamethrower && boulder.ichor)
        {
            boulder.ichor = false;
            result.ichorBurnedOut = true;
        }

        if (boulder.iceCrust && boulder.temperatureC >= 0f)
        {
            boulder.iceCrust = false;
            boulder.canShed = true;
            result.iceMelted = true;
        }

        if (boulder.ore != null && boulder.temperatureC >= boulder.ore.heatDestructionC)
        {
            boulder.oreDestroyedByHeat = true;
            result.oreDestroyed = true;
        }

        result.overheated = boulder.temperatureC >= BoulderOverheatThresholdC;
        result.temperatureAfterC = boulder.temperatureC;
        return result;
    }

    public static void DriftTemperatureTowardEnvironment(ExtractionBoulderState boulder, float ambientTemperatureC, float seconds)
    {
        if (boulder == null) return;
        float rate = 1f - Mathf.Exp(-Mathf.Max(0f, seconds) / Mathf.Max(10f, boulder.radiusMeters * 4f));
        boulder.temperatureC = Mathf.Lerp(boulder.temperatureC, ambientTemperatureC, rate);
        if (boulder.temperatureC < 0f)
        {
            boulder.iceCrust = true;
            boulder.canShed = false;
        }
    }

    public static CloudHarvestResult HarvestCloud(GasCloudMechanicsState cloud, Vector3 shipPosition, float harvesterRadiusMeters, float seconds)
    {
        CloudHarvestResult result = new CloudHarvestResult();
        if (cloud == null || cloud.remainingGasUnits <= 0f)
        {
            return result;
        }

        float distance = Vector3.Distance(shipPosition, cloud.center);
        float reach = Mathf.Max(0f, harvesterRadiusMeters) + Mathf.Max(0.1f, cloud.radiusMeters);
        if (distance > reach)
        {
            return result;
        }

        float normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(0.1f, cloud.radiusMeters));
        float concentration = Mathf.Pow(1f - normalizedDistance, 2f);
        result.concentration01 = concentration;
        float request = cloud.harvestUnitsPerSecondAtCenter * concentration * Mathf.Max(0f, seconds);
        result.harvestedUnits = Mathf.Min(request, cloud.remainingGasUnits);
        cloud.remainingGasUnits -= result.harvestedUnits;
        result.thermalDamage = cloud.hot ? concentration * seconds * 8f : 0f;
        result.ichorDamage = cloud.ichor ? concentration * seconds * 12f : 0f;
        result.electricDamage = cloud.electric ? concentration * seconds * 10f : 0f;
        return result;
    }

    public static bool BurnCloud(GasCloudMechanicsState cloud, float seconds)
    {
        if (cloud == null || cloud.remainingGasUnits <= 0f) return false;
        cloud.remainingGasUnits = Mathf.Max(0f, cloud.remainingGasUnits - Mathf.Max(0f, seconds) * 90f);
        return true;
    }

    public static bool AccumulateElectricPulse(GasCloudMechanicsState cloud, ExtractionBoulderState boulder, float seconds)
    {
        if (cloud == null || !cloud.electric || boulder == null) return false;
        cloud.electricCharge += Mathf.Max(0f, seconds) * 4f;
        if (cloud.electricCharge < cloud.electricPulseThreshold)
        {
            return false;
        }

        cloud.electricCharge = 0f;
        if (!boulder.explosiveGas)
        {
            return false;
        }

        ProjectileProfile pulse = new ProjectileProfile
        {
            delivery = ShellDeliveryMode.HighExplosive,
            element = ExtractionDamageElement.Electric,
            damage = 1f,
            triggersExplosiveGas = true
        };
        return ApplyProjectileDamage(boulder, pulse, true).exploded;
    }

    public static EnvironmentSample SampleEnvironment(
        EnvironmentRegionProfile region,
        Vector3 position,
        IList<GasCloudMechanicsState> clouds,
        IList<ClaudiumFieldSource> claudiumFields)
    {
        region ??= new EnvironmentRegionProfile();
        EnvironmentSample sample = new EnvironmentSample
        {
            temperatureC = region.ambientTemperatureC,
            visibility01 = Mathf.Clamp01(1f - region.fogOpacity01),
            airDensity = Mathf.Max(0f, 2f * (1f - Mathf.Clamp01(position.y / Mathf.Max(1f, region.worldHeightMeters)))),
            claudiumLiftFactor = Mathf.Max(0f, 1f - Mathf.Clamp01(position.y / Mathf.Max(1f, region.worldHeightMeters)))
        };

        if (clouds != null)
        {
            for (int i = 0; i < clouds.Count; i++)
            {
                GasCloudMechanicsState cloud = clouds[i];
                if (cloud == null || cloud.remainingGasUnits <= 0f) continue;
                float distance = Vector3.Distance(position, cloud.center);
                float concentration = Mathf.Clamp01(1f - distance / Mathf.Max(0.1f, cloud.radiusMeters));
                sample.temperatureC += cloud.temperatureOffsetC * concentration;
                sample.visibility01 *= Mathf.Clamp01(1f - cloud.opacity01 * concentration);
            }
        }

        if (claudiumFields != null)
        {
            for (int i = 0; i < claudiumFields.Count; i++)
            {
                ClaudiumFieldSource field = claudiumFields[i];
                if (field == null || field.radiusMeters <= 0f) continue;
                float distance = Vector3.Distance(position, field.center);
                float factor = Mathf.Clamp01(1f - distance / field.radiusMeters);
                sample.claudiumFieldStrength += Mathf.Max(0f, field.strength) * factor;
            }
        }

        if (position.y < region.stormTopMeters)
        {
            float t = Mathf.Clamp01(position.y / Mathf.Max(1f, region.stormTopMeters));
            sample.stormDamagePerSecond = Mathf.Lerp(1000f, 1f, t);
        }

        return sample;
    }

    public static DroneTaskResult ResolveDroneTask(MiningDroneKind kind, ExtractionBoulderState boulder, ExtractionChunkState chunk, DroneIchorProtection ichorProtection)
    {
        DroneTaskResult result = new DroneTaskResult { task = kind.ToString() };
        float ichorMultiplier = GetDroneIchorDamageMultiplier(ichorProtection);
        switch (kind)
        {
            case MiningDroneKind.Catcher:
                if (chunk != null && !chunk.destroyed)
                {
                    chunk.destroyed = true;
                    result.success = true;
                    result.oreDeliveredKg = Mathf.Floor(chunk.UsefulOreKg);
                    result.playerAvoidedRamDamage = true;
                    result.droneDamageTaken = chunk.ichorAcid ? 20f * ichorMultiplier : 0f;
                }
                break;
            case MiningDroneKind.Grabber:
                if (boulder != null && !boulder.CrustActive && !boulder.IsDestroyed)
                {
                    result.success = true;
                    result.oreDeliveredKg = Mathf.Min(25f, boulder.UsefulOreKg);
                    boulder.health = Mathf.Max(0f, boulder.health - result.oreDeliveredKg);
                    result.droneDamageTaken = boulder.ichor ? 15f * ichorMultiplier : 0f;
                }
                break;
            case MiningDroneKind.Burner:
                if (boulder != null)
                {
                    ThermalResult heat = ApplyHeat(boulder, boulder.MassKg * 0.2f, 20f, true);
                    result.success = true;
                    result.propertyRemoved = heat.iceMelted || heat.explosiveDisarmed || heat.ichorBurnedOut;
                }
                break;
            case MiningDroneKind.Tug:
                result.success = boulder != null;
                result.targetDamage = 0f;
                break;
            case MiningDroneKind.Disarmer:
                if (boulder != null && boulder.explosiveGas)
                {
                    boulder.explosiveGas = false;
                    result.success = true;
                    result.propertyRemoved = true;
                }
                break;
            case MiningDroneKind.Opener:
                if (boulder != null && boulder.CrustActive)
                {
                    float damage = Mathf.Min(boulder.crustHealth, boulder.maxHealth * 0.35f);
                    boulder.crustHealth -= damage;
                    if (boulder.crustHealth <= 0f) boulder.armoredCrust = false;
                    result.success = true;
                    result.targetDamage = damage;
                    result.propertyRemoved = !boulder.CrustActive;
                }
                break;
            case MiningDroneKind.Syringe:
                if (boulder != null && boulder.ichor)
                {
                    result.success = true;
                    result.ichorDeliveredKg = Mathf.Max(1f, boulder.MassKg * 0.01f);
                    result.propertyRemoved = true;
                    boulder.ichor = false;
                    result.droneDamageTaken = 0f;
                }
                else if (boulder != null && boulder.CrustActive)
                {
                    float damage = Mathf.Min(boulder.crustHealth, boulder.maxHealth * 1.5f);
                    boulder.crustHealth -= damage;
                    if (boulder.crustHealth <= 0f) boulder.armoredCrust = false;
                    result.success = true;
                    result.targetDamage = damage;
                }
                break;
            case MiningDroneKind.Scanner:
                result.success = boulder != null;
                break;
            case MiningDroneKind.Salvager:
                result.success = true;
                break;
        }

        return result;
    }

    public static RelicExtractionResult TryExtractRelic(ExtractionBoulderState boulder, bool craneAvailable, bool catcherDroneAvailable)
    {
        RelicExtractionResult result = new RelicExtractionResult();
        if (boulder == null || !boulder.relic)
        {
            result.reason = "No relic.";
            return result;
        }

        if (boulder.relicDestroyed)
        {
            result.relicDestroyed = true;
            result.reason = "Relic already destroyed.";
            return result;
        }

        if (boulder.Health01 > 0.5f)
        {
            result.reason = "Boulder health must be below half.";
            return result;
        }

        if (!craneAvailable && !catcherDroneAvailable)
        {
            result.reason = "Need crane or catcher drone.";
            return result;
        }

        boulder.relicExtracted = true;
        result.success = true;
        result.reason = "Relic extracted carefully.";
        return result;
    }

    public static HackResult ResolveHackAttempt(float exposure, float exposureRequired, bool hasMidHackModule, bool shipAttempt, bool minigameWon)
    {
        HackResult result = new HackResult
        {
            canStartMinigame = shipAttempt && hasMidHackModule && exposure >= exposureRequired
        };
        if (!result.canStartMinigame)
        {
            return result;
        }

        result.success = minigameWon;
        result.threatAdded = minigameWon ? 2f : 35f;
        result.shipDamaged = false;
        return result;
    }

    public static FireControlResult ResolveFireAuthorization(
        FireAuthorizationMode mode,
        ExtractionObjectKind targetKind,
        bool targetDangerous,
        bool targetScanned,
        bool relicRisk,
        float visibility01,
        float scanResolution01,
        bool ammoAvailable,
        bool inSector)
    {
        FireControlResult result = new FireControlResult();
        if (!ammoAvailable)
        {
            result.reason = "No ammo.";
            return result;
        }

        if (!inSector)
        {
            result.reason = "Target outside sector.";
            return result;
        }

        bool resourceTarget = targetKind == ExtractionObjectKind.Boulder || targetKind == ExtractionObjectKind.Cloud || targetKind == ExtractionObjectKind.Relic;
        if (mode == FireAuthorizationMode.Safe && resourceTarget)
        {
            result.reason = "Safe mode blocks resource targets.";
            return result;
        }

        if (mode == FireAuthorizationMode.DangerousOnly && !targetDangerous)
        {
            result.reason = "Only dangerous targets are allowed.";
            return result;
        }

        if (!targetScanned && resourceTarget)
        {
            result.reason = "Unknown resource target.";
            return result;
        }

        if (relicRisk)
        {
            result.reason = "Relic risk.";
            return result;
        }

        result.allowed = true;
        result.hitChance = Mathf.Clamp01(0.25f + Mathf.Clamp01(visibility01) * 0.25f + Mathf.Clamp01(scanResolution01) * 0.5f);
        result.reason = "Allowed.";
        return result;
    }

    public static SalvageResult ResolveDroneWreckSalvage(float wreckMassKg, bool fallsOnIsland, bool catcherDroneAvailable, float shipLiftSpareKg)
    {
        SalvageResult result = new SalvageResult();
        if (catcherDroneAvailable)
        {
            result.salvageAvailable = true;
            result.caughtBeforeStorm = true;
            result.requiresOnSiteDismantling = wreckMassKg > shipLiftSpareKg;
            return result;
        }

        result.salvageAvailable = fallsOnIsland;
        result.requiresOnSiteDismantling = wreckMassKg > shipLiftSpareKg;
        return result;
    }

    public static float CombineReductions(params float[] reductions)
    {
        float remaining = 1f;
        if (reductions != null)
        {
            for (int i = 0; i < reductions.Length; i++)
            {
                remaining *= 1f - Mathf.Clamp01(reductions[i]);
            }
        }

        return Mathf.Clamp01(1f - remaining);
    }

    public static float CalculateRamDamage(float massKg, float speedMS)
    {
        if (speedMS < RamNoDamageSpeedThresholdMS) return 0f;
        float energyKJ = 0.5f * Mathf.Max(0f, massKg) * speedMS * speedMS / 1000f;
        return Mathf.Sqrt(Mathf.Max(0f, energyKJ)) * 10f;
    }

    public static float GetDroneIchorDamageMultiplier(DroneIchorProtection protection)
    {
        return protection switch
        {
            DroneIchorProtection.None => 5f,
            DroneIchorProtection.Basic => 1f,
            DroneIchorProtection.Advanced => 0.5f,
            DroneIchorProtection.Immune => 0f,
            _ => 1f
        };
    }

    private static int EstimateShedChunks(ExtractionBoulderState boulder, float damage)
    {
        if (boulder == null || !boulder.canShed || boulder.iceCrust || boulder.CrustActive) return 0;
        return Mathf.Max(1, Mathf.CeilToInt(damage / Mathf.Max(1f, boulder.maxHealth) * Mathf.Max(3f, boulder.radiusMeters * 0.5f)));
    }

    private static float GetBoulderHealthElementMultiplier(ExtractionDamageElement element)
    {
        return element switch
        {
            ExtractionDamageElement.Fire => 0.8f,
            ExtractionDamageElement.Electric => 0.7f,
            ExtractionDamageElement.Ichor => 1f,
            ExtractionDamageElement.Cold => 0f,
            ExtractionDamageElement.Explosion => 1.25f,
            _ => 1f
        };
    }
}

public sealed class MetaShipDefinition
{
    public string shipId = "";
    public int tier = 1;
    public int silverCost;
    public int constructionXpUnlockCost;
    public int maxSorties = 5;
    public bool fallbackPioneer;
    public readonly List<string> preinstalledModules = new List<string>();
    public int highSlots;
    public int midSlots;
    public int lowSlots;
    public int rigSlots;
}

public sealed class MetaShipInstance
{
    public string instanceId = Guid.NewGuid().ToString("N");
    public string shipId = "";
    public int tier = 1;
    public int remainingSorties = 5;
    public int maxSorties = 5;
    public readonly List<string> preinstalledModules = new List<string>();
    public readonly List<string> fittedModules = new List<string>();
    public readonly List<string> rigs = new List<string>();
}

public sealed class MetaRecipeState
{
    public string recipeId = "";
    public RecipeTier tier = RecipeTier.T2;
    public int level;
    public float yieldMultiplier = 1f;
    public float speedMultiplier = 1f;
}

public sealed class MetaAccountState
{
    public int silver;
    public int gold;
    public int portSlots = 5;
    public int constructionXp;
    public int industrialXp;
    public int militaryXp;
    public int researchXp;
    public int commanderXp;
    public int commanderLevel = 1;
    public int commanderTalentPoints;
    public string activeContractId = "";
    public int weeklyBattlePassPoints;
    public bool weeklyBattlePassPaidTrack;
    public readonly List<MetaShipInstance> ships = new List<MetaShipInstance>();
    public readonly HashSet<string> unlockedShips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> unlockedT1Modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, int> storage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, float> processingBuffers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, MetaRecipeState> recipes = new Dictionary<string, MetaRecipeState>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> activeCommanderTalents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProcessingInputProfile
{
    public string inputId = "";
    public int cycleInputUnits = 10;
    public float efficiency = 0.5f;
    public readonly Dictionary<string, float> composition = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
}

public sealed class ContainerLot
{
    public string itemId = "";
    public int amount = 1;
    public float weight = 1f;
}

public sealed class VoucherState
{
    public VoucherType type;
    public VoucherRarity rarity;
    public float multiplier = 1.1f;
}

public sealed class MetaOperationResult
{
    public bool success;
    public string message = "";
    public int silverDelta;
    public int goldDelta;
    public int constructionXpDelta;
}

public sealed class WildWindMetaCatalog
{
    public readonly Dictionary<string, MetaShipDefinition> ships = new Dictionary<string, MetaShipDefinition>(StringComparer.OrdinalIgnoreCase);
    public readonly List<ContainerLot> dailyContainerLots = new List<ContainerLot>();
    public readonly Dictionary<string, ProcessingInputProfile> processing = new Dictionary<string, ProcessingInputProfile>(StringComparer.OrdinalIgnoreCase);
}

public static class WildWindMetaMechanics
{
    public const int InitialPortSlots = 5;
    public const int PortSlotGoldCost = 200;
    public const int RigSafeRemoveGoldCost = 20;
    public const int WeeklyBattlePassPaidTrackGoldCost = 250;

    public static WildWindMetaCatalog CreateMinimalCatalog()
    {
        WildWindMetaCatalog catalog = new WildWindMetaCatalog();
        MetaShipDefinition pioneer = new MetaShipDefinition
        {
            shipId = "pioneer",
            tier = 1,
            silverCost = 0,
            constructionXpUnlockCost = 0,
            maxSorties = 7,
            fallbackPioneer = true,
            highSlots = 3,
            midSlots = 1,
            lowSlots = 1,
            rigSlots = 1
        };
        pioneer.preinstalledModules.Add("basic_engine");
        catalog.ships[pioneer.shipId] = pioneer;

        catalog.ships["hauler_t1"] = new MetaShipDefinition { shipId = "hauler_t1", tier = 1, silverCost = 1200, constructionXpUnlockCost = 80, maxSorties = 6, highSlots = 2, midSlots = 1, lowSlots = 2, rigSlots = 1 };
        catalog.ships["pioneer_mining_t2"] = new MetaShipDefinition { shipId = "pioneer_mining_t2", tier = 2, silverCost = 0, constructionXpUnlockCost = 0, maxSorties = 8, highSlots = 4, midSlots = 1, lowSlots = 1, rigSlots = 2 };
        catalog.ships["precursor_t3"] = new MetaShipDefinition { shipId = "precursor_t3", tier = 3, silverCost = 0, constructionXpUnlockCost = 0, maxSorties = 10, highSlots = 4, midSlots = 2, lowSlots = 2, rigSlots = 2 };

        ProcessingInputProfile ore = new ProcessingInputProfile { inputId = "ognejar_ore", cycleInputUnits = 10, efficiency = 0.5f };
        ore.composition["charcoal"] = 0.10f;
        ore.composition["ferron"] = 0.20f;
        catalog.processing[ore.inputId] = ore;

        catalog.dailyContainerLots.Add(new ContainerLot { itemId = "charcoal", amount = 10, weight = 4f });
        catalog.dailyContainerLots.Add(new ContainerLot { itemId = "charcoal", amount = 50, weight = 1f });
        catalog.dailyContainerLots.Add(new ContainerLot { itemId = "datacore_industrial", amount = 1, weight = 0.5f });
        catalog.dailyContainerLots.Add(new ContainerLot { itemId = "hauler_t1", amount = 1, weight = 0.15f });
        return catalog;
    }

    public static MetaAccountState CreateFreshAccount(WildWindMetaCatalog catalog)
    {
        MetaAccountState account = new MetaAccountState
        {
            silver = 1000,
            gold = 0,
            portSlots = InitialPortSlots
        };
        account.unlockedShips.Add("pioneer");
        EnsurePioneerFallback(account, catalog);
        return account;
    }

    public static bool EnsurePioneerFallback(MetaAccountState account, WildWindMetaCatalog catalog)
    {
        if (account == null || catalog == null) return false;
        if (account.ships.Count > 0) return false;
        if (!catalog.ships.TryGetValue("pioneer", out MetaShipDefinition pioneer)) return false;
        account.ships.Add(CreateShipInstance(pioneer));
        account.unlockedShips.Add("pioneer");
        return true;
    }

    public static MetaOperationResult BuyPortSlot(MetaAccountState account)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || account.gold < PortSlotGoldCost)
        {
            result.message = "Not enough gold.";
            return result;
        }

        account.gold -= PortSlotGoldCost;
        account.portSlots++;
        result.success = true;
        result.goldDelta = -PortSlotGoldCost;
        result.message = "Port slot bought.";
        return result;
    }

    public static MetaOperationResult BuyT1Ship(MetaAccountState account, WildWindMetaCatalog catalog, string shipId)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || catalog == null || !catalog.ships.TryGetValue(shipId, out MetaShipDefinition definition))
        {
            result.message = "Unknown ship.";
            return result;
        }

        if (definition.tier != 1)
        {
            result.message = "Only T1 ships are direct silver purchases.";
            return result;
        }

        if (!account.unlockedShips.Contains(shipId))
        {
            result.message = "Ship is not unlocked in the tree.";
            return result;
        }

        if (account.ships.Count >= account.portSlots)
        {
            result.message = "No port slot.";
            return result;
        }

        if (account.silver < definition.silverCost)
        {
            result.message = "Not enough silver.";
            return result;
        }

        account.silver -= definition.silverCost;
        account.ships.Add(CreateShipInstance(definition));
        result.success = true;
        result.silverDelta = -definition.silverCost;
        result.message = "Ship bought.";
        return result;
    }

    public static MetaOperationResult SellShip(MetaAccountState account, WildWindMetaCatalog catalog, MetaShipInstance ship)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || catalog == null || ship == null || !account.ships.Contains(ship))
        {
            result.message = "Ship not owned.";
            return result;
        }

        catalog.ships.TryGetValue(ship.shipId, out MetaShipDefinition definition);
        int baseCost = definition != null ? definition.silverCost : 0;
        int silver = ship.shipId == "pioneer" ? 0 : Mathf.FloorToInt(baseCost * 0.5f);
        int xp = Mathf.Max(5, (definition != null ? definition.tier : ship.tier) * 20 + ship.maxSorties);
        account.ships.Remove(ship);
        account.silver += silver;
        account.constructionXp += xp;
        EnsurePioneerFallback(account, catalog);

        result.success = true;
        result.silverDelta = silver;
        result.constructionXpDelta = xp;
        result.message = "Ship sold; fitted rigs went with the hull, preinstalled modules stayed built-in.";
        return result;
    }

    public static MetaOperationResult LoseShipInSortie(MetaAccountState account, WildWindMetaCatalog catalog, MetaShipInstance ship, Dictionary<string, int> sortieLoot)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || catalog == null || ship == null || !account.ships.Contains(ship))
        {
            result.message = "Ship not active.";
            return result;
        }

        account.ships.Remove(ship);
        sortieLoot?.Clear();
        EnsurePioneerFallback(account, catalog);
        result.success = true;
        result.message = "Sortie lost: ship and sortie loot are gone, fallback Pioneer restored.";
        return result;
    }

    public static bool ConsumeShipSortie(MetaShipInstance ship)
    {
        if (ship == null || ship.remainingSorties <= 0) return false;
        ship.remainingSorties--;
        return ship.remainingSorties > 0;
    }

    public static MetaOperationResult ProcessOneCycle(MetaAccountState account, ProcessingInputProfile profile)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || profile == null || string.IsNullOrWhiteSpace(profile.inputId))
        {
            result.message = "Bad processing input.";
            return result;
        }

        int available = GetStorage(account, profile.inputId);
        if (available < profile.cycleInputUnits)
        {
            result.message = "Not enough raw input.";
            return result;
        }

        AddStorage(account, profile.inputId, -profile.cycleInputUnits);
        foreach (KeyValuePair<string, float> entry in profile.composition)
        {
            float producedFraction = profile.cycleInputUnits * Mathf.Max(0f, entry.Value) * Mathf.Clamp01(profile.efficiency);
            string bufferKey = "buffer:" + entry.Key;
            account.processingBuffers.TryGetValue(bufferKey, out float buffer);
            buffer += producedFraction;
            int whole = Mathf.FloorToInt(buffer);
            if (whole > 0)
            {
                AddStorage(account, entry.Key, whole);
                buffer -= whole;
            }

            account.processingBuffers[bufferKey] = buffer;
        }

        result.success = true;
        result.message = "Processed one batch.";
        return result;
    }

    public static bool LearnRecipe(MetaAccountState account, string recipeId, RecipeTier tier)
    {
        if (account == null || string.IsNullOrWhiteSpace(recipeId)) return false;
        if (account.recipes.ContainsKey(recipeId)) return false;
        account.recipes[recipeId] = new MetaRecipeState { recipeId = recipeId, tier = tier, level = 0 };
        return true;
    }

    public static MetaOperationResult UpgradeRecipe(MetaAccountState account, string recipeId)
    {
        MetaOperationResult result = new MetaOperationResult();
        if (account == null || !account.recipes.TryGetValue(recipeId, out MetaRecipeState recipe))
        {
            result.message = "Recipe missing.";
            return result;
        }

        string datacoreId = recipe.tier == RecipeTier.T3 ? "datacore_precursor" : "datacore_industrial";
        int cost = recipe.tier == RecipeTier.T3 ? 3 + recipe.level : 1 + recipe.level;
        if (GetStorage(account, datacoreId) < cost)
        {
            result.message = "Not enough datacores.";
            return result;
        }

        AddStorage(account, datacoreId, -cost);
        recipe.level++;
        recipe.yieldMultiplier += 0.05f;
        recipe.speedMultiplier += 0.08f;
        result.success = true;
        result.message = "Recipe upgraded.";
        return result;
    }

    public static ContainerLot OpenContainer(IList<ContainerLot> lots, int deterministicSeed)
    {
        if (lots == null || lots.Count == 0) return null;
        float total = 0f;
        for (int i = 0; i < lots.Count; i++)
        {
            if (lots[i] != null)
            {
                total += Mathf.Max(0f, lots[i].weight);
            }
        }

        if (total <= 0f) return lots[0];
        float roll = Mathf.Abs(deterministicSeed * 1103515245 + 12345) % 100000 / 100000f * total;
        for (int i = 0; i < lots.Count; i++)
        {
            ContainerLot lot = lots[i];
            if (lot == null) continue;
            roll -= Mathf.Max(0f, lot.weight);
            if (roll <= 0f)
            {
                return lot;
            }
        }

        return lots[lots.Count - 1];
    }

    public static float ApplyVoucherSet(IList<VoucherState> vouchers, VoucherType type, float baseValue)
    {
        float result = baseValue;
        bool used = false;
        if (vouchers != null)
        {
            for (int i = 0; i < vouchers.Count; i++)
            {
                VoucherState voucher = vouchers[i];
                if (voucher == null || voucher.type != type || used) continue;
                result *= Mathf.Max(1f, voucher.multiplier);
                used = true;
            }
        }

        return result;
    }

    public static bool ValidateConsumableLoadout(IList<string> consumables, out string reason)
    {
        reason = "";
        if (consumables == null) return true;
        if (consumables.Count > 5)
        {
            reason = "Only five consumable slots.";
            return false;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < consumables.Count; i++)
        {
            string id = consumables[i] ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!seen.Add(id))
            {
                reason = "Duplicate consumable.";
                return false;
            }
        }

        return true;
    }

    public static bool ActivateBattlePassContract(MetaAccountState account, string contractId)
    {
        if (account == null || string.IsNullOrWhiteSpace(contractId) || !string.IsNullOrWhiteSpace(account.activeContractId))
        {
            return false;
        }

        account.activeContractId = contractId;
        return true;
    }

    public static void AbandonBattlePassContract(MetaAccountState account)
    {
        if (account != null)
        {
            account.activeContractId = "";
        }
    }

    public static bool BuyWeeklyBattlePassPaidTrack(MetaAccountState account)
    {
        if (account == null || account.weeklyBattlePassPaidTrack || account.gold < WeeklyBattlePassPaidTrackGoldCost)
        {
            return false;
        }

        account.gold -= WeeklyBattlePassPaidTrackGoldCost;
        account.weeklyBattlePassPaidTrack = true;
        return true;
    }

    public static void AddRepeatableBattlePassProgress(MetaAccountState account, int xpEarned, int craftedT2Ships)
    {
        if (account == null) return;
        account.weeklyBattlePassPoints += Mathf.Max(0, xpEarned / 1000) * 8;
        account.weeklyBattlePassPoints += Mathf.Max(0, craftedT2Ships) * 27;
    }

    public static bool BuyInternalGoldBundle(MetaAccountState account, int goldCost, Dictionary<string, int> rewards)
    {
        if (account == null || goldCost < 0 || account.gold < goldCost)
        {
            return false;
        }

        account.gold -= goldCost;
        if (rewards != null)
        {
            foreach (KeyValuePair<string, int> reward in rewards)
            {
                AddStorage(account, reward.Key, reward.Value);
            }
        }

        return true;
    }

    public static bool ActivateCommanderTalent(MetaAccountState account, string talentId, int pointCost)
    {
        if (account == null || string.IsNullOrWhiteSpace(talentId) || account.activeCommanderTalents.Contains(talentId))
        {
            return false;
        }

        if (account.commanderTalentPoints < pointCost)
        {
            return false;
        }

        account.commanderTalentPoints -= pointCost;
        account.activeCommanderTalents.Add(talentId);
        return true;
    }

    public static bool DeactivateCommanderTalent(MetaAccountState account, string talentId, int pointCost)
    {
        if (account == null || !account.activeCommanderTalents.Remove(talentId))
        {
            return false;
        }

        account.commanderTalentPoints += Mathf.Max(0, pointCost);
        return true;
    }

    public static int GetStorage(MetaAccountState account, string itemId)
    {
        if (account == null || string.IsNullOrWhiteSpace(itemId)) return 0;
        return account.storage.TryGetValue(itemId, out int amount) ? amount : 0;
    }

    public static void AddStorage(MetaAccountState account, string itemId, int amount)
    {
        if (account == null || string.IsNullOrWhiteSpace(itemId) || amount == 0) return;
        account.storage.TryGetValue(itemId, out int current);
        current = Mathf.Max(0, current + amount);
        if (current == 0)
        {
            account.storage.Remove(itemId);
        }
        else
        {
            account.storage[itemId] = current;
        }
    }

    private static MetaShipInstance CreateShipInstance(MetaShipDefinition definition)
    {
        MetaShipInstance instance = new MetaShipInstance
        {
            shipId = definition.shipId,
            tier = definition.tier,
            remainingSorties = definition.maxSorties,
            maxSorties = definition.maxSorties
        };
        instance.preinstalledModules.AddRange(definition.preinstalledModules);
        return instance;
    }
}

public sealed class WildWindMechanicsLabState
{
    public WildWindMechanicsCatalog extractionCatalog;
    public WildWindMetaCatalog metaCatalog;
    public MetaAccountState account;
    public string lastReport = "";

    public static WildWindMechanicsLabState Create()
    {
        WildWindMechanicsLabState state = new WildWindMechanicsLabState
        {
            extractionCatalog = WildWindExtractionMechanics.CreateMinimalCatalog(),
            metaCatalog = WildWindMetaMechanics.CreateMinimalCatalog()
        };
        state.account = WildWindMetaMechanics.CreateFreshAccount(state.metaCatalog);
        state.lastReport = "Lab ready: mining, cloud, relic, hack and meta scenarios are available.";
        return state;
    }

    public string RunMiningScenario()
    {
        OreMechanicsProfile ore = extractionCatalog.ores["ferron"];
        ExtractionBoulderState boulder = WildWindExtractionMechanics.CreateBoulder("lab_ferron", ore, 16f);
        ScanResult scan = WildWindExtractionMechanics.ApplyScanExposure(extractionCatalog.scanners["directional_high_scanner"], boulder, 14f, 320f, 0.85f, true);
        ProjectileProfile shot = new ProjectileProfile { delivery = ShellDeliveryMode.ArmorPiercing, element = ExtractionDamageElement.Physical, damage = 140f };
        BoulderDamageResult damage = WildWindExtractionMechanics.ApplyProjectileDamage(boulder, shot, true);
        ExtractionChunkState chunk = WildWindExtractionMechanics.ShedChunk(boulder, 180f, 24f);
        ExtractionShipProfile ship = new ExtractionShipProfile { hasCrusher = true, ramShieldActive = true, passiveRamPlating = true, cargoCapacityKg = 1000f };
        ChunkCatchResult catchResult = WildWindExtractionMechanics.ResolveChunkShipContact(chunk, ship, true);
        lastReport = "Mining: scan " + scan.exposureTotal.ToString("0") + "/" + boulder.exposureRequired.ToString("0")
            + ", chunks " + damage.chunks
            + ", ore " + catchResult.oreCollectedKg.ToString("0")
            + " kg, ram " + catchResult.finalRamDamage.ToString("0.0") + ".";
        return lastReport;
    }

    public string RunHazardScenario()
    {
        OreMechanicsProfile coal = extractionCatalog.ores["coal"];
        ExtractionBoulderState boulder = WildWindExtractionMechanics.CreateBoulder("lab_explosive", coal, 20f, explosive: true, ice: true);
        ThermalResult heat = WildWindExtractionMechanics.ApplyHeat(boulder, boulder.MassKg * 100f, 20f, true);
        GasCloudMechanicsState cloud = new GasCloudMechanicsState { id = "lab_electric", center = Vector3.zero, radiusMeters = 100f, electric = true, opacity01 = 0.4f };
        ExtractionBoulderState armed = WildWindExtractionMechanics.CreateBoulder("lab_armed", coal, 12f, explosive: true);
        bool detonated = WildWindExtractionMechanics.AccumulateElectricPulse(cloud, armed, 4f);
        lastReport = "Hazards: ice melted " + heat.iceMelted
            + ", gas disarmed " + heat.explosiveDisarmed
            + ", electric detonation " + detonated + ".";
        return lastReport;
    }

    public string RunRelicScenario()
    {
        OreMechanicsProfile ore = extractionCatalog.ores["ferron"];
        ExtractionBoulderState relicBoulder = WildWindExtractionMechanics.CreateBoulder("lab_relic", ore, 10f, relic: true);
        relicBoulder.health = relicBoulder.maxHealth * 0.45f;
        RelicExtractionResult safe = WildWindExtractionMechanics.TryExtractRelic(relicBoulder, true, false);
        ExtractionBoulderState rough = WildWindExtractionMechanics.CreateBoulder("lab_rough_relic", ore, 10f, relic: true);
        WildWindExtractionMechanics.ApplyRamToBoulder(rough, 8000f, 8f, false);
        lastReport = "Relic: careful crane " + safe.success + ", rough contact destroyed " + rough.relicDestroyed + ".";
        return lastReport;
    }

    public string RunMetaScenario()
    {
        account.gold += 300;
        WildWindMetaMechanics.BuyPortSlot(account);
        account.unlockedShips.Add("hauler_t1");
        account.silver += 2000;
        WildWindMetaMechanics.BuyT1Ship(account, metaCatalog, "hauler_t1");
        MetaShipInstance sold = account.ships.Find(s => s.shipId == "hauler_t1");
        MetaOperationResult sale = WildWindMetaMechanics.SellShip(account, metaCatalog, sold);
        WildWindMetaMechanics.AddStorage(account, "ognejar_ore", 20);
        WildWindMetaMechanics.ProcessOneCycle(account, metaCatalog.processing["ognejar_ore"]);
        WildWindMetaMechanics.ProcessOneCycle(account, metaCatalog.processing["ognejar_ore"]);
        ContainerLot lot = WildWindMetaMechanics.OpenContainer(metaCatalog.dailyContainerLots, 7);
        lastReport = "Meta: slots " + account.portSlots
            + ", sale xp +" + sale.constructionXpDelta
            + ", charcoal " + WildWindMetaMechanics.GetStorage(account, "charcoal")
            + ", container " + (lot != null ? lot.itemId + " x" + lot.amount : "-") + ".";
        return lastReport;
    }
}
