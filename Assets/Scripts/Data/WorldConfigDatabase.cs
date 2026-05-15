using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class WorldConfigDatabase
{
    private readonly Dictionary<string, ItemConfig> itemsById = new Dictionary<string, ItemConfig>();
    private readonly Dictionary<string, IslandConfig> islandsById = new Dictionary<string, IslandConfig>();
    private readonly Dictionary<string, IslandProductionConfig> productionsById = new Dictionary<string, IslandProductionConfig>();
    private readonly Dictionary<string, GasCloudTypeConfig> gasCloudTypesById = new Dictionary<string, GasCloudTypeConfig>();
    private readonly Dictionary<string, GasCloudConfig> gasCloudsById = new Dictionary<string, GasCloudConfig>();
    private readonly Dictionary<string, OreTypeConfig> oreTypesById = new Dictionary<string, OreTypeConfig>();
    private readonly Dictionary<string, MiningZoneConfig> miningZonesById = new Dictionary<string, MiningZoneConfig>();
    private readonly Dictionary<string, TechnologyConfig> technologiesById = new Dictionary<string, TechnologyConfig>();

    public List<ItemConfig> items = new List<ItemConfig>();
    public List<IslandConfig> islands = new List<IslandConfig>();
    public List<IslandProductionConfig> productions = new List<IslandProductionConfig>();
    public List<GasCloudTypeConfig> gasCloudTypes = new List<GasCloudTypeConfig>();
    public List<GasCloudConfig> gasClouds = new List<GasCloudConfig>();
    public List<OreTypeConfig> oreTypes = new List<OreTypeConfig>();
    public List<MiningZoneConfig> miningZones = new List<MiningZoneConfig>();
    public List<TechnologyConfig> technologies = new List<TechnologyConfig>();

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
            LoadProductions(Path.Combine(folder, "Island_production.csv"));
            LoadGasCloudTypes(Path.Combine(folder, "Gas_cloud_type.csv"));
            LoadOreTypes(Path.Combine(folder, "Ore_type.csv"));
            LoadIslands(Path.Combine(folder, "Island.csv"));
            LoadGasClouds(Path.Combine(folder, "Gas_cloud.csv"));
            LoadMiningZones(Path.Combine(folder, "Mining_zone.csv"));
            LoadTechnologies(Path.Combine(folder, "Technology.csv"));
            isLoaded = true;
            lastError = "";
        }
        catch (Exception exception)
        {
            Clear();
            lastError = "Ошибка загрузки конфигов мира: " + exception.Message;
            Debug.LogWarning(lastError);
        }
    }

    public ItemConfig GetItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        itemsById.TryGetValue(itemId, out ItemConfig item);
        return item;
    }

    public IslandConfig GetIsland(string islandId)
    {
        if (string.IsNullOrWhiteSpace(islandId)) return null;
        islandsById.TryGetValue(islandId, out IslandConfig island);
        return island;
    }

    public IslandProductionConfig GetProduction(string productionId)
    {
        if (string.IsNullOrWhiteSpace(productionId)) return null;
        productionsById.TryGetValue(productionId, out IslandProductionConfig production);
        return production;
    }

    public TechnologyConfig GetTechnology(string technologyId)
    {
        if (string.IsNullOrWhiteSpace(technologyId)) return null;
        technologiesById.TryGetValue(technologyId, out TechnologyConfig technology);
        return technology;
    }

    public GasCloudTypeConfig GetGasCloudType(string cloudTypeId)
    {
        if (string.IsNullOrWhiteSpace(cloudTypeId)) return null;
        gasCloudTypesById.TryGetValue(cloudTypeId, out GasCloudTypeConfig cloudType);
        return cloudType;
    }

    public GasCloudConfig GetGasCloud(string cloudId)
    {
        if (string.IsNullOrWhiteSpace(cloudId)) return null;
        gasCloudsById.TryGetValue(cloudId, out GasCloudConfig cloud);
        return cloud;
    }

    public OreTypeConfig GetOreType(string oreTypeId)
    {
        if (string.IsNullOrWhiteSpace(oreTypeId)) return null;
        oreTypesById.TryGetValue(oreTypeId, out OreTypeConfig oreType);
        return oreType;
    }

    public MiningZoneConfig GetMiningZone(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId)) return null;
        miningZonesById.TryGetValue(zoneId, out MiningZoneConfig zone);
        return zone;
    }

    public string GetItemNameRu(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        if (item == null) return itemId ?? "";
        return string.IsNullOrWhiteSpace(item.localNameRu) ? item.id : item.localNameRu;
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
        islands.Clear();
        productions.Clear();
        gasCloudTypes.Clear();
        gasClouds.Clear();
        oreTypes.Clear();
        miningZones.Clear();
        technologies.Clear();
        itemsById.Clear();
        islandsById.Clear();
        productionsById.Clear();
        gasCloudTypesById.Clear();
        gasCloudsById.Clear();
        oreTypesById.Clear();
        miningZonesById.Clear();
        technologiesById.Clear();
        isLoaded = false;
        lastError = "";
    }

    private void LoadItems(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            ItemConfig item = new ItemConfig
            {
                id = Get(row, "id_item"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                energyKwhPerKg = Mathf.Max(0f, ParseFloat(Get(row, "energy_kwh_per_kg")))
            };

            if (string.IsNullOrWhiteSpace(item.id)) continue;
            items.Add(item);
            itemsById[item.id] = item;
        }
    }

    private void LoadIslands(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandConfig island = new IslandConfig
            {
                id = Get(row, "id_island"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                position = new Vector3(
                    ParseFloat(Get(row, "position_x")),
                    ParseFloat(Get(row, "position_y")),
                    ParseFloat(Get(row, "position_z"))),
                productionId = Get(row, "Island_production"),
                dockingRadius = ParseFloat(Get(row, "docking_radius")),
                timeForOneItemLoadSeconds = Mathf.Max(0.01f, ParseFloat(Get(row, "time_for_one_item_load"), 1f))
            };

            if (string.IsNullOrWhiteSpace(island.id)) continue;
            islands.Add(island);
            islandsById[island.id] = island;
        }
    }

    private void LoadProductions(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandProductionConfig production = new IslandProductionConfig
            {
                id = Get(row, "Island_production_id"),
                productionItemId = Get(row, "production_item"),
                productionCountBasePerMinute = ParseFloat(Get(row, "production_count_base"))
            };

            List<string> itemIds = SplitInlineList(Get(row, "consumption_id_item"));
            List<string> counts = SplitInlineList(Get(row, "consumption_item_count"));
            List<string> boosts = SplitInlineList(Get(row, "satisfied_consumption_boost_base_production"));
            int count = Mathf.Min(itemIds.Count, Mathf.Min(counts.Count, boosts.Count));

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemIds[i])) continue;
                production.consumptions.Add(new IslandConsumptionConfig
                {
                    itemId = itemIds[i],
                    countPerMinute = ParseFloat(counts[i]),
                    satisfiedProductionMultiplier = ParseFloat(boosts[i], 1f)
                });
            }

            if (string.IsNullOrWhiteSpace(production.id)) continue;
            productions.Add(production);
            productionsById[production.id] = production;
        }
    }

    private void LoadGasCloudTypes(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            GasCloudTypeConfig cloudType = new GasCloudTypeConfig
            {
                id = Get(row, "id_cloud_type"),
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
                cloudType.composition.Add(new GasCloudCompositionConfig
                {
                    itemId = itemIds[i],
                    share = Mathf.Max(0f, ParseFloat(shares[i]))
                });
            }

            if (string.IsNullOrWhiteSpace(cloudType.id)) continue;
            gasCloudTypes.Add(cloudType);
            gasCloudTypesById[cloudType.id] = cloudType;
        }
    }

    private void LoadGasClouds(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            GasCloudConfig cloud = new GasCloudConfig
            {
                id = Get(row, "id_cloud"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                cloudTypeId = Get(row, "cloud_type_id"),
                position = new Vector3(
                    ParseFloat(Get(row, "position_x")),
                    ParseFloat(Get(row, "position_y")),
                    ParseFloat(Get(row, "position_z"))),
                initialVolumeLiters = Mathf.Max(0f, ParseFloat(Get(row, "initial_volume_l")))
            };

            if (string.IsNullOrWhiteSpace(cloud.id)) continue;
            gasClouds.Add(cloud);
            gasCloudsById[cloud.id] = cloud;
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

    private void LoadMiningZones(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            MiningZoneConfig zone = new MiningZoneConfig
            {
                id = Get(row, "id_mining_zone"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                center = new Vector3(
                    ParseFloat(Get(row, "position_x")),
                    0f,
                    ParseFloat(Get(row, "position_z"))),
                radiusMeters = Mathf.Max(1f, ParseFloat(Get(row, "radius_m"), 300f)),
                maxActiveRocks = Mathf.Max(0, ParseInt(Get(row, "max_active_rocks"), 3)),
                spawnIntervalSeconds = Mathf.Max(1f, ParseFloat(Get(row, "spawn_interval_seconds"), 240f)),
                initialRockCount = Mathf.Max(0, ParseInt(Get(row, "initial_rocks"), 1)),
                initialAgeFraction = Mathf.Clamp01(ParseFloat(Get(row, "initial_age_fraction"), 0.45f)),
                stormY = ParseFloat(Get(row, "storm_y"), -100f),
                stormSafetyClearanceY = Mathf.Max(0f, ParseFloat(Get(row, "storm_safety_clearance_y"), 45f)),
                spawnY = ParseFloat(Get(row, "spawn_y"), -100f),
                apexY = ParseFloat(Get(row, "apex_y"), 170f),
                descentSpeedMS = Mathf.Max(0.1f, ParseFloat(Get(row, "descent_speed_ms"), 0.35f)),
                ascentDurationSeconds = Mathf.Max(1f, ParseFloat(Get(row, "ascent_duration_seconds"), 90f)),
                rockRadiusMeters = Mathf.Max(2f, ParseFloat(Get(row, "rock_radius_m"), 24f)),
                rockOreKg = Mathf.Max(1f, ParseFloat(Get(row, "rock_ore_kg"), 160f))
            };

            zone.oreTypeIds.AddRange(SplitInlineList(Get(row, "ore_type_id")));

            if (string.IsNullOrWhiteSpace(zone.id)) continue;
            miningZones.Add(zone);
            miningZonesById[zone.id] = zone;
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
}

public class ItemConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public float energyKwhPerKg;
}

public class IslandConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public Vector3 position;
    public string productionId = "";
    public float dockingRadius;
    public float timeForOneItemLoadSeconds = 1f;
}

public class GasCloudTypeConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string condensateItemId = "";
    public float condensateLitersPerCubicMeter = 0.005f;
    public Color color = new Color(0.75f, 0.85f, 1f, 0.35f);
    public List<GasCloudCompositionConfig> composition = new List<GasCloudCompositionConfig>();
}

public class GasCloudCompositionConfig
{
    public string itemId = "";
    public float share;
}

public class GasCloudConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string cloudTypeId = "";
    public Vector3 position;
    public float initialVolumeLiters;
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

public class MiningZoneConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public Vector3 center;
    public float radiusMeters = 300f;
    public List<string> oreTypeIds = new List<string>();
    public int maxActiveRocks = 3;
    public float spawnIntervalSeconds = 240f;
    public int initialRockCount = 1;
    public float initialAgeFraction = 0.45f;
    public float stormY = -100f;
    public float stormSafetyClearanceY = 45f;
    public float spawnY = -100f;
    public float apexY = 170f;
    public float descentSpeedMS = 0.35f;
    public float ascentDurationSeconds = 90f;
    public float rockRadiusMeters = 24f;
    public float rockOreKg = 160f;

    public float DescentDurationSeconds
    {
        get
        {
            return Mathf.Max(1f, (apexY - stormY) / Mathf.Max(0.1f, descentSpeedMS));
        }
    }

    public float LifetimeSeconds => ascentDurationSeconds + DescentDurationSeconds;
}

public class IslandProductionConfig
{
    public string id = "";
    public string productionItemId = "";
    public float productionCountBasePerMinute;
    public List<IslandConsumptionConfig> consumptions = new List<IslandConsumptionConfig>();
}

public class IslandConsumptionConfig
{
    public string itemId = "";
    public float countPerMinute;
    public float satisfiedProductionMultiplier = 1f;
}

public class TechnologyConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
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

public static class IslandProductionSimulator
{
    private const double MaxStepSeconds = 60.0;
    private const int MaxSteps = 100000;

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || !config.isLoaded || progress == null || toUtcTicks <= fromUtcTicks) return 0;

        double remainingSeconds = new TimeSpan(toUtcTicks - fromUtcTicks).TotalSeconds;
        if (remainingSeconds <= 0.0) return 0;

        int changedUnits = 0;
        int steps = 0;
        while (remainingSeconds > 0.0001 && steps < MaxSteps)
        {
            float stepSeconds = (float)Math.Min(MaxStepSeconds, remainingSeconds);
            changedUnits += AdvanceStep(config, progress, stepSeconds);
            remainingSeconds -= stepSeconds;
            steps++;
        }

        return changedUnits;
    }

    public static void EnsureIslandStates(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || !config.isLoaded || progress == null) return;

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState state = progress.GetIslandProductionState(island.id, true);
            IslandProductionConfig production = config.GetProduction(island.productionId);
            EnsureConsumptionStates(state, production);
        }
    }

    private static int AdvanceStep(WorldConfigDatabase config, PlayerProgress progress, float stepSeconds)
    {
        float minutes = stepSeconds / 60f;
        int changedUnits = 0;

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null) continue;

            IslandProductionConfig production = config.GetProduction(island.productionId);
            if (production == null) continue;

            IslandProductionState state = progress.GetIslandProductionState(island.id, true);
            EnsureConsumptionStates(state, production);

            changedUnits += AdvanceConsumption(state, production, minutes);
            changedUnits += AdvanceProduction(config, state, production, minutes);
        }

        return changedUnits;
    }

    private static int AdvanceConsumption(IslandProductionState state, IslandProductionConfig production, float minutes)
    {
        int consumedTotal = 0;
        for (int i = 0; i < production.consumptions.Count; i++)
        {
            IslandConsumptionConfig consumption = production.consumptions[i];
            if (consumption == null || string.IsNullOrWhiteSpace(consumption.itemId) || consumption.countPerMinute <= 0f) continue;

            IslandConsumptionState consumptionState = state.GetConsumptionState(consumption.itemId, true);
            int available = state.GetResourceAmount(consumption.itemId);
            if (!consumptionState.isSatisfied)
            {
                consumptionState.consumptionProgress = 0f;
                if (available <= 0)
                {
                    continue;
                }

                state.TrySpendResource(consumption.itemId, 1);
                consumedTotal++;
                consumptionState.isSatisfied = true;
                continue;
            }

            consumptionState.consumptionProgress += consumption.countPerMinute * minutes;
            int dueCycles = Mathf.FloorToInt(consumptionState.consumptionProgress);
            if (dueCycles <= 0) continue;

            available = state.GetResourceAmount(consumption.itemId);
            if (available <= 0)
            {
                consumptionState.consumptionProgress = 0f;
                consumptionState.isSatisfied = false;
                continue;
            }

            int consumed = Mathf.Min(dueCycles, available);
            if (consumed > 0)
            {
                state.TrySpendResource(consumption.itemId, consumed);
                consumptionState.consumptionProgress -= consumed;
                consumedTotal += consumed;
                consumptionState.isSatisfied = true;
            }

            if (consumed < dueCycles)
            {
                consumptionState.consumptionProgress = 0f;
                consumptionState.isSatisfied = false;
            }
        }

        return consumedTotal;
    }

    private static int AdvanceProduction(WorldConfigDatabase config, IslandProductionState state, IslandProductionConfig production, float minutes)
    {
        if (string.IsNullOrWhiteSpace(production.productionItemId) || production.productionCountBasePerMinute <= 0f) return 0;

        float multiplier = CalculateProductionMultiplier(state, production);
        state.productionProgress += production.productionCountBasePerMinute * multiplier * minutes;

        int completedUnits = Mathf.FloorToInt(state.productionProgress);
        if (completedUnits <= 0) return 0;

        int added = state.AddResource(production.productionItemId, completedUnits);
        state.productionProgress -= added;
        return added;
    }

    private static float CalculateProductionMultiplier(IslandProductionState state, IslandProductionConfig production)
    {
        float multiplier = 1f;
        for (int i = 0; i < production.consumptions.Count; i++)
        {
            IslandConsumptionConfig consumption = production.consumptions[i];
            if (consumption == null) continue;

            IslandConsumptionState consumptionState = state.GetConsumptionState(consumption.itemId, false);
            if (consumptionState != null && consumptionState.isSatisfied)
            {
                multiplier *= Mathf.Max(0f, consumption.satisfiedProductionMultiplier);
            }
        }

        return multiplier;
    }

    private static void EnsureConsumptionStates(IslandProductionState state, IslandProductionConfig production)
    {
        if (state == null || production == null) return;

        for (int i = 0; i < production.consumptions.Count; i++)
        {
            IslandConsumptionConfig consumption = production.consumptions[i];
            if (consumption == null || string.IsNullOrWhiteSpace(consumption.itemId)) continue;
            state.GetConsumptionState(consumption.itemId, true);
        }
    }
}
