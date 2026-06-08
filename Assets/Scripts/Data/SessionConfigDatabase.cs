using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public partial class SessionConfigDatabase
{
    private readonly Dictionary<string, ItemConfig> itemsById = new Dictionary<string, ItemConfig>();
    private readonly Dictionary<string, PortConfig> portsById = new Dictionary<string, PortConfig>();
    private readonly Dictionary<string, GasCondensateTypeConfig> gasCondensateTypesById = new Dictionary<string, GasCondensateTypeConfig>();
    private readonly Dictionary<string, OreTypeConfig> oreTypesById = new Dictionary<string, OreTypeConfig>();
    private readonly Dictionary<string, LeviathanTypeConfig> leviathanTypesById = new Dictionary<string, LeviathanTypeConfig>();
    private readonly Dictionary<string, TechnologyConfig> technologiesById = new Dictionary<string, TechnologyConfig>();
    private readonly Dictionary<string, SpecialModuleConfig> specialModulesById = new Dictionary<string, SpecialModuleConfig>();

    public List<ItemConfig> items = new List<ItemConfig>();
    public List<PortConfig> ports = new List<PortConfig>();
    public List<GasCondensateTypeConfig> gasCondensateTypes = new List<GasCondensateTypeConfig>();
    public List<OreTypeConfig> oreTypes = new List<OreTypeConfig>();
    public List<LeviathanTypeConfig> leviathanTypes = new List<LeviathanTypeConfig>();
    public List<TechnologyConfig> technologies = new List<TechnologyConfig>();
    public List<SpecialModuleConfig> specialModules = new List<SpecialModuleConfig>();

    public bool isLoaded;
    public string lastError = "";

    public void LoadFromAssetsConfigFolder(string relativeFolder)
    {
        string folder = Path.Combine(Application.dataPath, string.IsNullOrWhiteSpace(relativeFolder) ? "Data/Config" : relativeFolder);
        LoadFromFolder(folder);
    }

    public void LoadFromFolder(string folder)
    {
            Clear();

        try
        {
            LoadItems(Path.Combine(folder, "Item.csv"));
            LoadGasCondensateTypes(Path.Combine(folder, "Gas_condensate_type.csv"));
            LoadOreTypes(Path.Combine(folder, "Ore_type.csv"));
            LoadPorts(Path.Combine(folder, "Port.csv"));
            LoadLeviathanTypes(Path.Combine(folder, "Leviathan_type.csv"));
            LoadTechnologies(Path.Combine(folder, "Technology.csv"));
            LoadHulls(Path.Combine(folder, "Hull.csv"));
            LoadEngines(Path.Combine(folder, "Engine.csv"));
            LoadPropellers(Path.Combine(folder, "Propeller.csv"));
            LoadClaudiumLoops(Path.Combine(folder, "Claudium_loop.csv"));
            LoadSpecialModules(Path.Combine(folder, "Special_module.csv"));
            LoadShipTree(Path.Combine(folder, "Ship_tree.csv"));
            isLoaded = true;
            lastError = "";
        }
        catch (Exception exception)
        {
            Clear();
            lastError = "Session config load error: " + exception.Message;
            Debug.LogWarning(lastError);
        }
    }

    public ItemConfig GetItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        itemsById.TryGetValue(itemId, out ItemConfig item);
        return item;
    }

    public PortConfig GetPort(string portId)
    {
        if (string.IsNullOrWhiteSpace(portId)) return null;
        portsById.TryGetValue(portId, out PortConfig port);
        return port;
    }

    public TechnologyConfig GetTechnology(string technologyId)
    {
        if (string.IsNullOrWhiteSpace(technologyId)) return null;
        technologiesById.TryGetValue(technologyId, out TechnologyConfig technology);
        return technology;
    }

    public GasCondensateTypeConfig GetGasCondensateType(string condensateTypeId)
    {
        if (string.IsNullOrWhiteSpace(condensateTypeId)) return null;
        gasCondensateTypesById.TryGetValue(condensateTypeId, out GasCondensateTypeConfig condensateType);
        return condensateType;
    }


    public OreTypeConfig GetOreType(string oreTypeId)
    {
        if (string.IsNullOrWhiteSpace(oreTypeId)) return null;
        oreTypesById.TryGetValue(oreTypeId, out OreTypeConfig oreType);
        return oreType;
    }


    public LeviathanTypeConfig GetLeviathanType(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return null;
        leviathanTypesById.TryGetValue(typeId, out LeviathanTypeConfig type);
        return type;
    }


    public SpecialModuleConfig GetSpecialModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return null;
        specialModulesById.TryGetValue(moduleId, out SpecialModuleConfig module);
        return module;
    }

    public string GetItemNameRu(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        if (item == null) return itemId ?? "";
        return string.IsNullOrWhiteSpace(item.localNameRu) ? item.id : item.localNameRu;
    }

    public CargoUnitKind GetItemUnitKind(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        if (item != null) return item.cargoUnitKind;
        return CargoUnitKind.Piece;
    }

    public CargoStorageKind GetItemStorageKind(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        if (item != null) return item.cargoStorageKind;
        return CargoStorageKind.Van;
    }

    public float GetItemStorageAmount(string itemId, int amount)
    {
        return GetItemTransportMassKg(itemId, amount);
    }

    public float GetItemTransportMassKg(string itemId, int amount)
    {
        return GetItemFullMassKg(itemId, amount);
    }

    public float GetItemFullMassKg(string itemId, int amount)
    {
        if (amount <= 0) return 0f;

        ItemConfig item = GetItem(itemId);
        if (item != null)
        {
            return Mathf.Max(0, amount) * Mathf.Max(0f, item.massKgPerUnit);
        }

        return Mathf.Max(0, amount);
    }

    public string GetTechnologyNameRu(string technologyId)
    {
        TechnologyConfig technology = GetTechnology(technologyId);
        if (technology == null) return technologyId ?? "";
        return string.IsNullOrWhiteSpace(technology.localNameRu) ? technology.id : technology.localNameRu;
    }

    private void Clear()
    {
        items.Clear();
        ports.Clear();
        gasCondensateTypes.Clear();
        oreTypes.Clear();
        leviathanTypes.Clear();
        technologies.Clear();
        specialModules.Clear();
        ClearShipPartConfigs();
        itemsById.Clear();
        portsById.Clear();
        gasCondensateTypesById.Clear();
        oreTypesById.Clear();
        leviathanTypesById.Clear();
        technologiesById.Clear();
        specialModulesById.Clear();
        isLoaded = false;
        lastError = "";
    }

    private void LoadItems(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            string itemId = Get(row, "id_item");
            CargoUnitKind parsedUnitKind = ParseCargoUnitKind(Get(row, "cargo_unit_kind"), itemId);
            CargoStorageKind parsedStorageKind = ParseCargoStorageKind(Get(row, "cargo_storage_kind"), itemId);
            ItemConfig item = new ItemConfig
            {
                id = itemId,
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                energyKwhPerKg = Mathf.Max(0f, ParseFloat(Get(row, "energy_kwh_per_kg"))),
                cargoUnitKind = NormalizeCargoUnitKind(itemId, parsedUnitKind, parsedStorageKind),
                cargoStorageKind = NormalizeCargoStorageKind(itemId, parsedUnitKind, parsedStorageKind),
                massKgPerUnit = Mathf.Max(0f, ParseFloat(Get(row, "mass_kg_per_unit"), 1f))
            };

            if (string.IsNullOrWhiteSpace(item.id)) continue;
            items.Add(item);
            itemsById[item.id] = item;
        }
    }

    private void LoadPorts(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            PortConfig port = new PortConfig
            {
                id = Get(row, "id_port"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                position = new Vector3(
                    ParseFloat(Get(row, "position_x")),
                    ParseFloat(Get(row, "position_y")),
                    ParseFloat(Get(row, "position_z"))),
                dockingRadius = ParseFloat(Get(row, "docking_radius")),
            };

            if (string.IsNullOrWhiteSpace(port.id)) continue;
            ports.Add(port);
            portsById[port.id] = port;
        }
    }

    private void LoadGasCondensateTypes(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            GasCondensateTypeConfig condensateType = new GasCondensateTypeConfig
            {
                id = Get(row, "id_gas_condensate_type"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                condensateItemId = Get(row, "condensate_item"),
                condensateLitersPerCubicMeter = Mathf.Max(0.0001f, ParseFloat(Get(row, "condensate_l_per_m3"), 0.005f)),
                color = ParseColor(Get(row, "color_hex"), new Color(0.75f, 0.85f, 1f, 0.35f))
            };

            List<string> itemIds = SplitInlineList(Get(row, "composition_id_item"));
            List<string> shares = SplitInlineList(Get(row, "composition_share"));
            int count = Mathf.Min(itemIds.Count, shares.Count);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemIds[i])) continue;
                condensateType.composition.Add(new GasCondensateCompositionConfig
                {
                    itemId = itemIds[i],
                    share = Mathf.Max(0f, ParseFloat(shares[i]))
                });
            }

            if (string.IsNullOrWhiteSpace(condensateType.id)) continue;
            gasCondensateTypes.Add(condensateType);
            gasCondensateTypesById[condensateType.id] = condensateType;
        }
    }

    private void LoadOreTypes(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            OreTypeConfig oreType = new OreTypeConfig
            {
                id = Get(row, "id_ore_type"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                oreItemId = Get(row, "ore_item"),
                baseValue = Mathf.Max(0f, ParseFloat(Get(row, "base_value"))),
                naturalShedKgPerMinute = Mathf.Max(0f, ParseFloat(Get(row, "natural_shed_intensity"), ParseFloat(Get(row, "natural_shed_kg_per_min"), 0.5f))),
                shotShedKg = Mathf.Max(0, ParseInt(Get(row, "shot_shed_kg"), 6)),
                fragmentFallSpeedMS = Mathf.Max(0.1f, ParseFloat(Get(row, "fragment_fall_speed_ms"), 4f)),
                color = ParseColor(Get(row, "color_hex"), new Color(0.55f, 0.5f, 0.45f, 1f))
            };

            List<string> mineralIds = SplitInlineList(Get(row, "composition_id_item"));
            List<string> shares = SplitInlineList(Get(row, "composition_share"));
            int count = Mathf.Min(mineralIds.Count, shares.Count);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(mineralIds[i])) continue;
                oreType.composition.Add(new OreMineralCompositionConfig
                {
                    mineralItemId = mineralIds[i],
                    share = Mathf.Max(0f, ParseFloat(shares[i]))
                });
            }

            if (string.IsNullOrWhiteSpace(oreType.id)) continue;
            oreTypes.Add(oreType);
            oreTypesById[oreType.id] = oreType;
        }
    }

    private void LoadLeviathanTypes(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            LeviathanTypeConfig type = new LeviathanTypeConfig
            {
                id = Get(row, "id_leviathan_type"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                carcassItemId = Get(row, "carcass_item"),
                bodyLengthMeters = Mathf.Max(2f, ParseFloat(Get(row, "body_length_m"), 28f)),
                bodyRadiusMeters = Mathf.Max(0.5f, ParseFloat(Get(row, "body_radius_m"), 5f)),
                massKg = Mathf.Max(1f, ParseFloat(Get(row, "mass_kg"), 5000f)),
                maxHealth = Mathf.Max(1f, ParseFloat(Get(row, "max_health"), 300f)),
                claudiumLiftKg = Mathf.Max(0f, ParseFloat(Get(row, "claudium_lift_kg"), 5500f)),
                forwardThrustKgf = Mathf.Max(0f, ParseFloat(Get(row, "forward_thrust_kgf"), ParseFloat(Get(row, "swim_force_n"), 9000f) / 9.81f)),
                omniThrustKgf = Mathf.Max(0f, ParseFloat(Get(row, "omni_thrust_kgf"), ParseFloat(Get(row, "swim_force_n"), 9000f) * 0.72f / 9.81f)),
                cruiseSpeedMS = Mathf.Max(0.1f, ParseFloat(Get(row, "cruise_speed_ms"), ParseFloat(Get(row, "max_speed_ms"), 9f))),
                wanderRadiusMeters = Mathf.Max(1f, ParseFloat(Get(row, "wander_radius_m"), 220f)),
                turnTorque = Mathf.Max(0f, ParseFloat(Get(row, "turn_torque"), 1200f)),
                headArmorMm = Mathf.Max(0f, ParseFloat(Get(row, "head_armor_mm"), 28f)),
                bodyArmorMm = Mathf.Max(0f, ParseFloat(Get(row, "body_armor_mm"), 5f)),
                ramDamageMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "ram_damage_multiplier"), 1f)),
                carcassMassFraction = Mathf.Clamp01(ParseFloat(Get(row, "carcass_mass_fraction"), 0.55f)),
                color = ParseColor(Get(row, "color_hex"), new Color(0.35f, 0.55f, 0.7f, 1f))
            };

            if (string.IsNullOrWhiteSpace(type.id)) continue;
            leviathanTypes.Add(type);
            leviathanTypesById[type.id] = type;
        }
    }

    private void LoadTechnologies(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            TechnologyConfig technology = new TechnologyConfig
            {
                id = Get(row, "id_technology"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                rank = Mathf.Max(0, ParseInt(Get(row, "rank"), 0)),
                branch = Get(row, "branch"),
                unlockSummaryRu = Get(row, "unlock_summary_ru"),
                cycleTimeSeconds = Mathf.Max(0, ParseInt(Get(row, "cycle_time_seconds"), 1)),
                requiredCycles = Mathf.Max(1, ParseInt(Get(row, "required_cycles"), 1))
            };

            technology.prerequisiteTechnologyIds.AddRange(SplitInlineList(Get(row, "required_technology")));

            List<string> itemIds = SplitInlineList(Get(row, "cycle_cost_item"));
            List<string> amounts = SplitInlineList(Get(row, "cycle_cost_amount"));
            int count = Mathf.Min(itemIds.Count, amounts.Count);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemIds[i])) continue;

                int amount = Mathf.Max(0, ParseInt(amounts[i]));
                if (amount <= 0) continue;

                technology.cycleCost.Add(new TechnologyCostConfig
                {
                    itemId = itemIds[i],
                    amount = amount
                });
            }

            if (string.IsNullOrWhiteSpace(technology.id)) continue;
            technologies.Add(technology);
            technologiesById[technology.id] = technology;
        }
    }

    private void LoadSpecialModules(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            SpecialModuleConfig module = new SpecialModuleConfig
            {
                id = Get(row, "id_special_module"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                descriptionRu = Get(row, "description_ru"),
                completedTechId = Get(row, "complited_tech"),
                baseMassKg = Mathf.Max(0f, ParseFloat(Get(row, "base_mass_kg"))),
                miningImpactHoldCapacityKg = Mathf.Max(0f, ParseFloat(Get(row, "mining_impact_hold_capacity_kg"))),
                miningImpactDamageTakenMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "mining_impact_damage_taken_multiplier"))),
                cargoVanCapacityKg = Mathf.Max(0f, ParseFloat(Get(row, "cargo_van_capacity_kg"), ParseFloat(Get(row, "cargo_van_capacity_units")))),
                bulkHoldCapacityKg = Mathf.Max(0f, ParseFloat(Get(row, "bulk_hold_capacity_kg"), ParseFloat(Get(row, "bulk_hold_capacity_l")))),
                liquidTankCapacityKg = Mathf.Max(0f, ParseFloat(Get(row, "liquid_tank_capacity_kg"), ParseFloat(Get(row, "liquid_tank_capacity_l")))),
                gasCylinderCapacityKg = Mathf.Max(0f, ParseFloat(Get(row, "gas_cylinder_capacity_kg"), ParseFloat(Get(row, "gas_cylinder_capacity_l"))))
            };

            module.compatibleSlotTypeIds.AddRange(SplitInlineList(Get(row, "compatible_slot_type")));

            if (string.IsNullOrWhiteSpace(module.id)) continue;
            specialModules.Add(module);
            specialModulesById[module.id] = module;
        }
    }

    private static IEnumerable<Dictionary<string, string>> ReadCsv(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Не найден CSV: " + path, path);
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0) yield break;

        List<string> headers = ParseCsvLine(lines[0]);
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            List<string> values = ParseCsvLine(lines[i]);
            Dictionary<string, string> row = new Dictionary<string, string>();
            for (int column = 0; column < headers.Count; column++)
            {
                string header = headers[column].Trim('\uFEFF');
                string value = column < values.Count ? values[column] : "";
                row[header] = value;
            }

            yield return row;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> values = new List<string>();
        StringBuilder builder = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];
            if (current == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (current == ',' && !inQuotes)
            {
                values.Add(builder.ToString().Trim());
                builder.Length = 0;
                continue;
            }

            builder.Append(current);
        }

        values.Add(builder.ToString().Trim());
        return values;
    }

    private static List<string> SplitInlineList(string value)
    {
        List<string> result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        string[] parts = value.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            result.Add(parts[i].Trim());
        }

        return result;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        return row != null && row.TryGetValue(key, out string value) ? value : "";
    }

    private static int ParseInt(string value, int fallback = 0)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
    }

    private static float ParseFloat(string value, float fallback = 0f)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : fallback;
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (!value.StartsWith("#", StringComparison.Ordinal))
        {
            value = "#" + value;
        }

        if (!ColorUtility.TryParseHtmlString(value, out Color color)) return fallback;
        color.a = fallback.a;
        return color;
    }

    private static CargoUnitKind ParseCargoUnitKind(string value, string itemId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CargoUnitKind.Piece;
        }

        return Enum.TryParse(value, true, out CargoUnitKind parsed) ? parsed : CargoUnitKind.Piece;
    }

    private static CargoUnitKind NormalizeCargoUnitKind(string itemId, CargoUnitKind parsedUnitKind, CargoStorageKind parsedStorageKind)
    {
        return CargoUnitKind.Piece;
    }

    private static CargoStorageKind ParseCargoStorageKind(string value, string itemId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CargoStorageKind.Van;
        }

        string normalized = value.Trim().Replace("-", "").Replace("_", "");
        if (string.Equals(normalized, "bulk", StringComparison.OrdinalIgnoreCase))
        {
            return CargoStorageKind.BulkHold;
        }

        if (string.Equals(normalized, "liquid", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "tank", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "cistern", StringComparison.OrdinalIgnoreCase))
        {
            return CargoStorageKind.LiquidTank;
        }

        if (string.Equals(normalized, "gas", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "cylinder", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "bottle", StringComparison.OrdinalIgnoreCase))
        {
            return CargoStorageKind.GasCylinder;
        }

        return Enum.TryParse(value, true, out CargoStorageKind parsed) ? parsed : CargoStorageKind.Van;
    }

    private static CargoStorageKind NormalizeCargoStorageKind(string itemId, CargoUnitKind parsedUnitKind, CargoStorageKind parsedStorageKind)
    {
        return parsedStorageKind;
    }

    private static bool ParseBool01(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim();
        if (float.TryParse(normalized.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number))
        {
            return number > 0.5f;
        }

        return normalized.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("да", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("y", StringComparison.OrdinalIgnoreCase);
    }

}

public class ItemConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public float energyKwhPerKg;
    public CargoUnitKind cargoUnitKind = CargoUnitKind.Piece;
    public CargoStorageKind cargoStorageKind = CargoStorageKind.Van;
    public float massKgPerUnit = 1f;
}

public class PortConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public Vector3 position;
    public float dockingRadius;
}

public class GasCondensateTypeConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string condensateItemId = "";
    public float condensateLitersPerCubicMeter = 0.005f;
    public Color color = new Color(0.75f, 0.85f, 1f, 0.35f);
    public List<GasCondensateCompositionConfig> composition = new List<GasCondensateCompositionConfig>();
}

public class GasCondensateCompositionConfig
{
    public string itemId = "";
    public float share;
}

public class OreTypeConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string oreItemId = "";
    public float baseValue;
    public float naturalShedKgPerMinute = 2f;
    public int shotShedKg = 6;
    public float fragmentFallSpeedMS = 4f;
    public Color color = new Color(0.55f, 0.5f, 0.45f, 1f);
    public List<OreMineralCompositionConfig> composition = new List<OreMineralCompositionConfig>();
}

public class OreMineralCompositionConfig
{
    public string mineralItemId = "";
    public float share;
}

public class LeviathanTypeConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string carcassItemId = "";
    public float bodyLengthMeters = 28f;
    public float bodyRadiusMeters = 5f;
    public float massKg = 5000f;
    public float maxHealth = 300f;
    public float claudiumLiftKg = 5500f;
    public float forwardThrustKgf = 920f;
    public float omniThrustKgf = 660f;
    public float cruiseSpeedMS = 9f;
    public float wanderRadiusMeters = 220f;
    public float turnTorque = 1200f;
    public float headArmorMm = 28f;
    public float bodyArmorMm = 5f;
    public float ramDamageMultiplier = 1f;
    public float carcassMassFraction = 0.55f;
    public Color color = new Color(0.35f, 0.55f, 0.7f, 1f);

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
    public int CarcassMassKg => Mathf.Max(1, Mathf.RoundToInt(massKg * Mathf.Clamp01(carcassMassFraction)));
}

public class TechnologyConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public int rank;
    public string branch = "";
    public string unlockSummaryRu = "";
    public int cycleTimeSeconds = 1;
    public int requiredCycles = 1;
    public List<string> prerequisiteTechnologyIds = new List<string>();
    public List<TechnologyCostConfig> cycleCost = new List<TechnologyCostConfig>();
}

public class TechnologyCostConfig
{
    public string itemId = "";
    public int amount;
}

public class SpecialModuleConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string descriptionRu = "";
    public string completedTechId = "";
    public float baseMassKg;
    public List<string> compatibleSlotTypeIds = new List<string>();
    public float miningImpactHoldCapacityKg;
    public float miningImpactDamageTakenMultiplier;
    public float cargoVanCapacityKg;
    public float bulkHoldCapacityKg;
    public float liquidTankCapacityKg;
    public float gasCylinderCapacityKg;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}
