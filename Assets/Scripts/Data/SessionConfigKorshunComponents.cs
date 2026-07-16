using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class SessionConfigDatabase
{
    private readonly Dictionary<string, KorshunHullPackageConfig> korshunHullPackagesById = new Dictionary<string, KorshunHullPackageConfig>();
    private readonly Dictionary<string, KorshunPowerPlantConfig> korshunPowerPlantsById = new Dictionary<string, KorshunPowerPlantConfig>();
    private readonly Dictionary<string, KorshunWeaponPackageConfig> korshunWeaponPackagesById = new Dictionary<string, KorshunWeaponPackageConfig>();
    private readonly Dictionary<string, KorshunAuxiliaryPackageConfig> korshunAuxiliaryPackagesById = new Dictionary<string, KorshunAuxiliaryPackageConfig>();
    private readonly Dictionary<string, ShipCitadelPackageConfig> shipCitadelPackagesById = new Dictionary<string, ShipCitadelPackageConfig>();

    public List<KorshunHullPackageConfig> korshunHullPackages = new List<KorshunHullPackageConfig>();
    public List<KorshunPowerPlantConfig> korshunPowerPlants = new List<KorshunPowerPlantConfig>();
    public List<KorshunWeaponPackageConfig> korshunWeaponPackages = new List<KorshunWeaponPackageConfig>();
    public List<KorshunAuxiliaryPackageConfig> korshunAuxiliaryPackages = new List<KorshunAuxiliaryPackageConfig>();
    public List<ShipCitadelPackageConfig> shipCitadelPackages = new List<ShipCitadelPackageConfig>();

    public KorshunHullPackageConfig GetKorshunHullPackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return null;
        korshunHullPackagesById.TryGetValue(packageId, out KorshunHullPackageConfig package);
        return package;
    }

    public KorshunPowerPlantConfig GetKorshunPowerPlant(string powerPlantId)
    {
        if (string.IsNullOrWhiteSpace(powerPlantId)) return null;
        korshunPowerPlantsById.TryGetValue(powerPlantId, out KorshunPowerPlantConfig powerPlant);
        return powerPlant;
    }

    public KorshunWeaponPackageConfig GetKorshunWeaponPackage(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId)) return null;
        korshunWeaponPackagesById.TryGetValue(weaponId, out KorshunWeaponPackageConfig weapon);
        return weapon;
    }

    public KorshunAuxiliaryPackageConfig GetKorshunAuxiliaryPackage(string auxiliaryId)
    {
        if (string.IsNullOrWhiteSpace(auxiliaryId)) return null;
        korshunAuxiliaryPackagesById.TryGetValue(auxiliaryId, out KorshunAuxiliaryPackageConfig auxiliary);
        return auxiliary;
    }

    public ShipCitadelPackageConfig GetShipCitadelPackage(string citadelId)
    {
        if (string.IsNullOrWhiteSpace(citadelId)) return null;
        shipCitadelPackagesById.TryGetValue(citadelId, out ShipCitadelPackageConfig citadel);
        return citadel;
    }

    private void ClearKorshunComponentConfigs()
    {
        korshunHullPackages.Clear();
        korshunPowerPlants.Clear();
        korshunWeaponPackages.Clear();
        korshunAuxiliaryPackages.Clear();
        shipCitadelPackages.Clear();
        korshunHullPackagesById.Clear();
        korshunPowerPlantsById.Clear();
        korshunWeaponPackagesById.Clear();
        korshunAuxiliaryPackagesById.Clear();
        shipCitadelPackagesById.Clear();
    }

    private void LoadKorshunComponentConfigs(string folder)
    {
        LoadKorshunHullPackages(Path.Combine(folder, "Korshun_hull_packages.csv"));
        LoadKorshunPowerPlants(Path.Combine(folder, "Korshun_power_plants.csv"));
        LoadKorshunWeaponPackages(Path.Combine(folder, "Korshun_weapon_packages.csv"));
        LoadKorshunAuxiliaryPackages(Path.Combine(folder, "Korshun_auxiliary_packages.csv"));
        LoadKorshunHullPackages(Path.Combine(folder, "Barbet_hull_packages.csv"));
        LoadKorshunPowerPlants(Path.Combine(folder, "Barbet_power_plants.csv"));
        LoadKorshunWeaponPackages(Path.Combine(folder, "Barbet_weapon_packages.csv"));
        LoadKorshunAuxiliaryPackages(Path.Combine(folder, "Barbet_auxiliary_packages.csv"));
        LoadShipCitadelPackages(Path.Combine(folder, "Barbet_citadel_packages.csv"));
    }

    private void LoadKorshunHullPackages(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            KorshunHullPackageConfig package = new KorshunHullPackageConfig
            {
                id = Get(row, "id_package"),
                basePackageId = Get(row, "base_package_id"),
                upgradeLevel = Get(row, "upgrade_level"),
                shipId = Get(row, "ship_id"),
                localNameRu = Get(row, "local_name_ru"),
                roleRu = Get(row, "role_ru"),
                powerMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "power_multiplier"), 1f)),
                upgradeCostMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "upgrade_cost_multiplier"), 1f)),
                lengthM = Mathf.Max(1f, ParseFloat(Get(row, "length_m"), 60f)),
                structureHp = Mathf.Max(1f, ParseFloat(Get(row, "structure_hp"), 16000f)),
                kineticResistancePercent = Mathf.Clamp(ParseFloat(Get(row, "kinetic_resistance_percent"), 48f), 0f, 100f),
                thermalResistancePercent = Mathf.Clamp(ParseFloat(Get(row, "thermal_resistance_percent"), 18f), 0f, 100f),
                chemicalResistancePercent = Mathf.Clamp(ParseFloat(Get(row, "chemical_resistance_percent"), 18f), 0f, 100f),
                explosiveResistancePercent = Mathf.Clamp(ParseFloat(Get(row, "explosive_resistance_percent"), 28f), 0f, 100f),
                cargoCapacityTons = Mathf.Max(0f, ParseFloat(Get(row, "cargo_capacity_tons"), 40f)),
                cruiseSpeedMS = Mathf.Max(0f, ParseFloat(Get(row, "cruise_speed_ms"), 34f)),
                accelerationMS2 = Mathf.Max(0f, ParseFloat(Get(row, "acceleration_ms2"), 4.5f)),
                turnRateDegPerSecond = Mathf.Max(0f, ParseFloat(Get(row, "turn_rate_deg_per_second"), 24f)),
                detectionRangeM = Mathf.Max(0f, ParseFloat(Get(row, "detection_range_m"), 4800f)),
                weaponRangeMultiplier = Mathf.Max(0.01f, ParseFloat(Get(row, "weapon_range_multiplier"), 1f)),
                reloadMultiplier = Mathf.Max(0.01f, ParseFloat(Get(row, "reload_multiplier"), 1f)),
                dispersionMultiplier = Mathf.Max(0.01f, ParseFloat(Get(row, "dispersion_multiplier"), 1f)),
                notesRu = Get(row, "notes_ru")
            };

            if (string.IsNullOrWhiteSpace(package.id)) continue;
            korshunHullPackages.Add(package);
            korshunHullPackagesById[package.id] = package;
        }
    }

    private void LoadKorshunPowerPlants(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            KorshunPowerPlantConfig powerPlant = new KorshunPowerPlantConfig
            {
                id = Get(row, "id_power_plant"),
                basePackageId = Get(row, "base_package_id"),
                upgradeLevel = Get(row, "upgrade_level"),
                shipId = Get(row, "ship_id"),
                localNameRu = Get(row, "local_name_ru"),
                powerMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "power_multiplier"), 1f)),
                upgradeCostMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "upgrade_cost_multiplier"), 1f)),
                speedDeltaMS = ParseFloat(Get(row, "speed_delta_ms")),
                accelerationDeltaMS2 = ParseFloat(Get(row, "acceleration_delta_ms2")),
                turnRateDeltaDegPerSecond = ParseFloat(Get(row, "turn_rate_delta_deg_per_second")),
                batteryCapacity = Mathf.Max(0f, ParseFloat(Get(row, "battery_capacity"))),
                energyGenerationPerSecond = Mathf.Max(0f, ParseFloat(Get(row, "energy_generation_per_second"))),
                moduleHp = Mathf.Max(0f, ParseFloat(Get(row, "module_hp"))),
                allowsElectronicEquipment = ParseBool01(Get(row, "allows_electronic_equipment")),
                notesRu = Get(row, "notes_ru")
            };

            if (string.IsNullOrWhiteSpace(powerPlant.id)) continue;
            korshunPowerPlants.Add(powerPlant);
            korshunPowerPlantsById[powerPlant.id] = powerPlant;
        }
    }

    private void LoadKorshunWeaponPackages(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            KorshunWeaponPackageConfig weapon = new KorshunWeaponPackageConfig
            {
                id = Get(row, "id_weapon"),
                basePackageId = Get(row, "base_package_id"),
                upgradeLevel = Get(row, "upgrade_level"),
                shipId = Get(row, "ship_id"),
                localNameRu = Get(row, "local_name_ru"),
                slotRole = Get(row, "slot_role"),
                powerMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "power_multiplier"), 1f)),
                upgradeCostMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "upgrade_cost_multiplier"), 1f)),
                rangeM = Mathf.Max(0f, ParseFloat(Get(row, "range_m"))),
                damage = Mathf.Max(0f, ParseFloat(Get(row, "damage"))),
                damageType = ParseCoreTacticalDamageType(Get(row, "damage_type"), InferWeaponDamageType(Get(row, "shell_type"), ParseBool01(Get(row, "is_machinegun_aura")))),
                resistanceIgnorePercent = Mathf.Clamp(ParseFloat(Get(row, "resistance_ignore_percent")), 0f, 100f),
                barrelsOrProjectiles = Mathf.Max(0, ParseInt(Get(row, "barrels_or_projectiles"))),
                shotsPerMinute = Mathf.Max(0f, ParseFloat(Get(row, "shots_per_minute"))),
                reloadSeconds = Mathf.Max(0f, ParseFloat(Get(row, "reload_seconds"))),
                projectileSpeedMS = Mathf.Max(0f, ParseFloat(Get(row, "projectile_speed_ms"))),
                splashRadiusM = Mathf.Max(0f, ParseFloat(Get(row, "splash_radius_m"))),
                explosiveKg = Mathf.Max(0f, ParseFloat(Get(row, "explosive_kg"))),
                dispersionMPerKm = Mathf.Max(0f, ParseFloat(Get(row, "dispersion_m_per_km"))),
                shellType = Get(row, "shell_type"),
                isMachinegunAura = ParseBool01(Get(row, "is_machinegun_aura")),
                machinegunDamagePerSecond = Mathf.Max(0f, ParseFloat(Get(row, "machinegun_damage_per_second"))),
                notesRu = Get(row, "notes_ru")
            };

            if (string.IsNullOrWhiteSpace(weapon.id)) continue;
            korshunWeaponPackages.Add(weapon);
            korshunWeaponPackagesById[weapon.id] = weapon;
        }
    }

    private void LoadKorshunAuxiliaryPackages(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            KorshunAuxiliaryPackageConfig auxiliary = new KorshunAuxiliaryPackageConfig
            {
                id = Get(row, "id_auxiliary"),
                basePackageId = Get(row, "base_package_id"),
                upgradeLevel = Get(row, "upgrade_level"),
                shipId = Get(row, "ship_id"),
                localNameRu = Get(row, "local_name_ru"),
                slotRole = Get(row, "slot_role"),
                kind = Get(row, "kind"),
                powerMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "power_multiplier"), 1f)),
                upgradeCostMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "upgrade_cost_multiplier"), 1f)),
                rangeM = Mathf.Max(0f, ParseFloat(Get(row, "range_m"))),
                damage = Mathf.Max(0f, ParseFloat(Get(row, "damage"))),
                projectileSpeedMS = Mathf.Max(0f, ParseFloat(Get(row, "projectile_speed_ms"))),
                projectileHp = Mathf.Max(0f, ParseFloat(Get(row, "projectile_hp"))),
                explosiveKg = Mathf.Max(0f, ParseFloat(Get(row, "explosive_kg"))),
                projectilesPerSalvo = Mathf.Max(0, ParseInt(Get(row, "projectiles_per_salvo"))),
                reloadSeconds = Mathf.Max(0f, ParseFloat(Get(row, "reload_seconds"))),
                energyCost = Mathf.Max(0f, ParseFloat(Get(row, "energy_cost"))),
                cycleSeconds = Mathf.Max(0f, ParseFloat(Get(row, "cycle_seconds"))),
                cooldownSeconds = Mathf.Max(0f, ParseFloat(Get(row, "cooldown_seconds"))),
                repairHpPerCycle = Mathf.Max(0f, ParseFloat(Get(row, "repair_hp_per_cycle"))),
                requiresEnergy = ParseBool01(Get(row, "requires_energy")),
                notesRu = Get(row, "notes_ru")
            };

            if (string.IsNullOrWhiteSpace(auxiliary.id)) continue;
            korshunAuxiliaryPackages.Add(auxiliary);
            korshunAuxiliaryPackagesById[auxiliary.id] = auxiliary;
        }
    }

    private void LoadShipCitadelPackages(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            ShipCitadelPackageConfig citadel = new ShipCitadelPackageConfig
            {
                id = Get(row, "id_citadel"),
                basePackageId = Get(row, "base_package_id"),
                upgradeLevel = Get(row, "upgrade_level"),
                shipId = Get(row, "ship_id"),
                localNameRu = Get(row, "local_name_ru"),
                powerMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "power_multiplier"), 1f)),
                upgradeCostMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "upgrade_cost_multiplier"), 1f)),
                citadelHp = Mathf.Max(0f, ParseFloat(Get(row, "citadel_hp"))),
                kineticResistanceBonusPercent = Mathf.Clamp(ParseFloat(Get(row, "kinetic_resistance_bonus_percent")), 0f, 100f),
                thermalResistanceBonusPercent = Mathf.Clamp(ParseFloat(Get(row, "thermal_resistance_bonus_percent")), 0f, 100f),
                chemicalResistanceBonusPercent = Mathf.Clamp(ParseFloat(Get(row, "chemical_resistance_bonus_percent")), 0f, 100f),
                explosiveResistanceBonusPercent = Mathf.Clamp(ParseFloat(Get(row, "explosive_resistance_bonus_percent")), 0f, 100f),
                speedDeltaMS = ParseFloat(Get(row, "speed_delta_ms")),
                accelerationDeltaMS2 = ParseFloat(Get(row, "acceleration_delta_ms2")),
                turnRateDeltaDegPerSecond = ParseFloat(Get(row, "turn_rate_delta_deg_per_second")),
                reloadRateMultiplier = Mathf.Max(0.01f, ParseFloat(Get(row, "reload_rate_multiplier"), 1f)),
                notesRu = Get(row, "notes_ru")
            };

            if (string.IsNullOrWhiteSpace(citadel.id)) continue;
            shipCitadelPackages.Add(citadel);
            shipCitadelPackagesById[citadel.id] = citadel;
        }
    }

    private static CoreTacticalDamageType InferWeaponDamageType(string shellType, bool isMachinegunAura)
    {
        if (isMachinegunAura)
        {
            return CoreTacticalDamageType.Kinetic;
        }

        string normalized = string.IsNullOrWhiteSpace(shellType) ? "" : shellType.Trim().ToLowerInvariant();
        if (normalized == "he" || normalized == "high_explosive" || normalized == "rocket" || normalized == "torpedo")
        {
            return CoreTacticalDamageType.Explosive;
        }

        if (normalized == "thermal")
        {
            return CoreTacticalDamageType.Thermal;
        }

        if (normalized == "chemical")
        {
            return CoreTacticalDamageType.Chemical;
        }

        return CoreTacticalDamageType.Kinetic;
    }

    private static CoreTacticalDamageType ParseCoreTacticalDamageType(string value, CoreTacticalDamageType fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "kinetic":
            case "kin":
                return CoreTacticalDamageType.Kinetic;
            case "thermal":
            case "heat":
                return CoreTacticalDamageType.Thermal;
            case "chemical":
            case "chem":
                return CoreTacticalDamageType.Chemical;
            case "explosive":
            case "explosion":
            case "blast":
                return CoreTacticalDamageType.Explosive;
            default:
                return fallback;
        }
    }
}

public class KorshunHullPackageConfig
{
    public string id = "";
    public string basePackageId = "";
    public string upgradeLevel = "";
    public string shipId = "";
    public string localNameRu = "";
    public string roleRu = "";
    public float powerMultiplier = 1f;
    public float upgradeCostMultiplier = 1f;
    public float lengthM;
    public float structureHp;
    public float kineticResistancePercent;
    public float thermalResistancePercent;
    public float chemicalResistancePercent;
    public float explosiveResistancePercent;
    public float cargoCapacityTons;
    public float cruiseSpeedMS;
    public float accelerationMS2;
    public float turnRateDegPerSecond;
    public float detectionRangeM;
    public float weaponRangeMultiplier = 1f;
    public float reloadMultiplier = 1f;
    public float dispersionMultiplier = 1f;
    public string notesRu = "";

    public CoreTacticalResistanceSet Resistances => new CoreTacticalResistanceSet(
        kineticResistancePercent,
        thermalResistancePercent,
        chemicalResistancePercent,
        explosiveResistancePercent);

    public bool IsBasePackage => string.IsNullOrWhiteSpace(basePackageId)
        && string.Equals(upgradeLevel, "base", StringComparison.OrdinalIgnoreCase);
    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class KorshunPowerPlantConfig
{
    public string id = "";
    public string basePackageId = "";
    public string upgradeLevel = "";
    public string shipId = "";
    public string localNameRu = "";
    public float powerMultiplier = 1f;
    public float upgradeCostMultiplier = 1f;
    public float speedDeltaMS;
    public float accelerationDeltaMS2;
    public float turnRateDeltaDegPerSecond;
    public float batteryCapacity;
    public float energyGenerationPerSecond;
    public float moduleHp;
    public bool allowsElectronicEquipment;
    public string notesRu = "";

    public bool IsBasePackage => string.IsNullOrWhiteSpace(basePackageId)
        && string.Equals(upgradeLevel, "base", StringComparison.OrdinalIgnoreCase);
    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class KorshunWeaponPackageConfig
{
    public string id = "";
    public string basePackageId = "";
    public string upgradeLevel = "";
    public string shipId = "";
    public string localNameRu = "";
    public string slotRole = "";
    public float powerMultiplier = 1f;
    public float upgradeCostMultiplier = 1f;
    public float rangeM;
    public float damage;
    public CoreTacticalDamageType damageType = CoreTacticalDamageType.Kinetic;
    public float resistanceIgnorePercent;
    public int barrelsOrProjectiles;
    public float shotsPerMinute;
    public float reloadSeconds;
    public float projectileSpeedMS;
    public float splashRadiusM;
    public float explosiveKg;
    public float dispersionMPerKm;
    public string shellType = "";
    public bool isMachinegunAura;
    public float machinegunDamagePerSecond;
    public string notesRu = "";

    public bool IsBasePackage => string.IsNullOrWhiteSpace(basePackageId)
        && string.Equals(upgradeLevel, "base", StringComparison.OrdinalIgnoreCase);
    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class KorshunAuxiliaryPackageConfig
{
    public string id = "";
    public string basePackageId = "";
    public string upgradeLevel = "";
    public string shipId = "";
    public string localNameRu = "";
    public string slotRole = "";
    public string kind = "";
    public float powerMultiplier = 1f;
    public float upgradeCostMultiplier = 1f;
    public float rangeM;
    public float damage;
    public float projectileSpeedMS;
    public float projectileHp;
    public float explosiveKg;
    public int projectilesPerSalvo;
    public float reloadSeconds;
    public float energyCost;
    public float cycleSeconds;
    public float cooldownSeconds;
    public float repairHpPerCycle;
    public bool requiresEnergy;
    public string notesRu = "";

    public bool IsBasePackage => string.IsNullOrWhiteSpace(basePackageId)
        && string.Equals(upgradeLevel, "base", StringComparison.OrdinalIgnoreCase);
    public string SlotRoleOrDefault => string.IsNullOrWhiteSpace(slotRole) ? "auxiliary" : slotRole;
    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class ShipCitadelPackageConfig
{
    public string id = "";
    public string basePackageId = "";
    public string upgradeLevel = "";
    public string shipId = "";
    public string localNameRu = "";
    public float powerMultiplier = 1f;
    public float upgradeCostMultiplier = 1f;
    public float citadelHp;
    public float kineticResistanceBonusPercent;
    public float thermalResistanceBonusPercent;
    public float chemicalResistanceBonusPercent;
    public float explosiveResistanceBonusPercent;
    public float speedDeltaMS;
    public float accelerationDeltaMS2;
    public float turnRateDeltaDegPerSecond;
    public float reloadRateMultiplier = 1f;
    public string notesRu = "";

    public bool IsBasePackage => string.IsNullOrWhiteSpace(basePackageId)
        && string.Equals(upgradeLevel, "base", StringComparison.OrdinalIgnoreCase);
    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
    public CoreTacticalResistanceSet ResistanceBonuses => new CoreTacticalResistanceSet(
        kineticResistanceBonusPercent,
        thermalResistanceBonusPercent,
        chemicalResistanceBonusPercent,
        explosiveResistanceBonusPercent);
}
