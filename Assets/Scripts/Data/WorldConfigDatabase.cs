using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public partial class WorldConfigDatabase
{
    private readonly Dictionary<string, ItemConfig> itemsById = new Dictionary<string, ItemConfig>();
    private readonly Dictionary<string, IslandConfig> islandsById = new Dictionary<string, IslandConfig>();
    private readonly Dictionary<string, IslandProductionConfig> productionsById = new Dictionary<string, IslandProductionConfig>();
    private readonly Dictionary<string, GasCloudTypeConfig> gasCloudTypesById = new Dictionary<string, GasCloudTypeConfig>();
    private readonly Dictionary<string, GasCloudConfig> gasCloudsById = new Dictionary<string, GasCloudConfig>();
    private readonly Dictionary<string, OreTypeConfig> oreTypesById = new Dictionary<string, OreTypeConfig>();
    private readonly Dictionary<string, MiningZoneConfig> miningZonesById = new Dictionary<string, MiningZoneConfig>();
    private readonly Dictionary<string, LeviathanTypeConfig> leviathanTypesById = new Dictionary<string, LeviathanTypeConfig>();
    private readonly Dictionary<string, LeviathanZoneConfig> leviathanZonesById = new Dictionary<string, LeviathanZoneConfig>();
    private readonly Dictionary<string, TechnologyConfig> technologiesById = new Dictionary<string, TechnologyConfig>();
    private readonly Dictionary<string, SpecialModuleConfig> specialModulesById = new Dictionary<string, SpecialModuleConfig>();
    private readonly Dictionary<string, IslandIndustryConfig> islandIndustriesById = new Dictionary<string, IslandIndustryConfig>();
    private readonly Dictionary<string, IndustryRecipeConfig> industryRecipesById = new Dictionary<string, IndustryRecipeConfig>();
    private readonly Dictionary<string, IslandArchetypeConfig> islandArchetypesById = new Dictionary<string, IslandArchetypeConfig>();
    private readonly Dictionary<string, IslandArchetypeStageConfig> islandArchetypeStagesById = new Dictionary<string, IslandArchetypeStageConfig>();
    private readonly Dictionary<string, IslandSocialNeedConfig> islandSocialNeedsById = new Dictionary<string, IslandSocialNeedConfig>();
    private readonly Dictionary<string, IslandBuildingConfig> islandBuildingsById = new Dictionary<string, IslandBuildingConfig>();
    private readonly Dictionary<string, FlagshipExpeditionDefinition> flagshipExpeditionsById = new Dictionary<string, FlagshipExpeditionDefinition>();

    public List<ItemConfig> items = new List<ItemConfig>();
    public List<IslandConfig> islands = new List<IslandConfig>();
    public List<IslandProductionConfig> productions = new List<IslandProductionConfig>();
    public List<GasCloudTypeConfig> gasCloudTypes = new List<GasCloudTypeConfig>();
    public List<GasCloudConfig> gasClouds = new List<GasCloudConfig>();
    public List<OreTypeConfig> oreTypes = new List<OreTypeConfig>();
    public List<MiningZoneConfig> miningZones = new List<MiningZoneConfig>();
    public List<LeviathanTypeConfig> leviathanTypes = new List<LeviathanTypeConfig>();
    public List<LeviathanZoneConfig> leviathanZones = new List<LeviathanZoneConfig>();
    public List<TechnologyConfig> technologies = new List<TechnologyConfig>();
    public List<SpecialModuleConfig> specialModules = new List<SpecialModuleConfig>();
    public List<IslandIndustryConfig> islandIndustries = new List<IslandIndustryConfig>();
    public List<IndustryRecipeConfig> industryRecipes = new List<IndustryRecipeConfig>();
    public List<IslandArchetypeConfig> islandArchetypes = new List<IslandArchetypeConfig>();
    public List<IslandArchetypeStageConfig> islandArchetypeStages = new List<IslandArchetypeStageConfig>();
    public List<IslandSocialNeedConfig> islandSocialNeeds = new List<IslandSocialNeedConfig>();
    public List<IslandBuildingConfig> islandBuildings = new List<IslandBuildingConfig>();
    public List<FlagshipExpeditionDefinition> flagshipExpeditions = new List<FlagshipExpeditionDefinition>();

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
            LoadLeviathanTypes(Path.Combine(folder, "Leviathan_type.csv"));
            LoadLeviathanZones(Path.Combine(folder, "Leviathan_zone.csv"));
            LoadTechnologies(Path.Combine(folder, "Technology.csv"));
            LoadHulls(Path.Combine(folder, "Hull.csv"));
            LoadEngines(Path.Combine(folder, "Engine.csv"));
            LoadPropellers(Path.Combine(folder, "Propeller.csv"));
            LoadClaudiumLoops(Path.Combine(folder, "Claudium_loop.csv"));
            LoadSpecialModules(Path.Combine(folder, "Special_module.csv"));
            LoadShipTree(Path.Combine(folder, "Ship_tree.csv"));
            LoadIndustryRecipes(Path.Combine(folder, "Production_recipe.csv"));
            LoadAssemblySteps(Path.Combine(folder, "Assembly_step.csv"));
            LoadIslandIndustries(Path.Combine(folder, "Production_industry.csv"));
            LoadIslandArchetypes(Path.Combine(folder, "Island_archetype.csv"));
            LoadIslandArchetypeStages(Path.Combine(folder, "Island_archetype_stage.csv"));
            LoadIslandSocialNeeds(Path.Combine(folder, "Island_social_need.csv"));
            LoadIslandBuildings(Path.Combine(folder, "Island_building.csv"));
            LoadFlagshipExpeditions(Path.Combine(folder, "Expedition.csv"));
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

    public FlagshipExpeditionDefinition GetFlagshipExpedition(string expeditionId)
    {
        if (string.IsNullOrWhiteSpace(expeditionId)) return null;
        flagshipExpeditionsById.TryGetValue(expeditionId, out FlagshipExpeditionDefinition expedition);
        return expedition;
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

    public LeviathanTypeConfig GetLeviathanType(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return null;
        leviathanTypesById.TryGetValue(typeId, out LeviathanTypeConfig type);
        return type;
    }

    public LeviathanZoneConfig GetLeviathanZone(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId)) return null;
        leviathanZonesById.TryGetValue(zoneId, out LeviathanZoneConfig zone);
        return zone;
    }

    public SpecialModuleConfig GetSpecialModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId)) return null;
        specialModulesById.TryGetValue(moduleId, out SpecialModuleConfig module);
        return module;
    }

    public IslandIndustryConfig GetIslandIndustry(string industryId)
    {
        if (string.IsNullOrWhiteSpace(industryId)) return null;
        islandIndustriesById.TryGetValue(industryId, out IslandIndustryConfig industry);
        return industry;
    }

    public IndustryRecipeConfig GetIndustryRecipe(string recipeId)
    {
        if (string.IsNullOrWhiteSpace(recipeId)) return null;
        industryRecipesById.TryGetValue(recipeId, out IndustryRecipeConfig recipe);
        return recipe;
    }

    public IslandArchetypeConfig GetIslandArchetype(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId)) return null;
        islandArchetypesById.TryGetValue(archetypeId, out IslandArchetypeConfig archetype);
        return archetype;
    }

    public IslandArchetypeStageConfig GetIslandArchetypeStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId)) return null;
        islandArchetypeStagesById.TryGetValue(stageId, out IslandArchetypeStageConfig stage);
        return stage;
    }

    public IslandSocialNeedConfig GetIslandSocialNeed(string needId)
    {
        if (string.IsNullOrWhiteSpace(needId)) return null;
        islandSocialNeedsById.TryGetValue(needId, out IslandSocialNeedConfig need);
        return need;
    }

    public IslandBuildingConfig GetIslandBuilding(string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId)) return null;
        islandBuildingsById.TryGetValue(buildingId, out IslandBuildingConfig building);
        return building;
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
        return IsPassengerCargoItemId(itemId) ? CargoUnitKind.Passenger : CargoUnitKind.Piece;
    }

    public CargoStorageKind GetItemStorageKind(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        if (item != null) return item.cargoStorageKind;
        return IsPassengerCargoItemId(itemId) ? CargoStorageKind.Cabin : CargoStorageKind.Van;
    }

    public ShipSizeClass GetItemShipSizeClass(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        return item != null ? item.shipSizeClass : ShipSizeClass.None;
    }

    public float GetItemStorageAmount(string itemId, int amount)
    {
        CargoStorageKind storageKind = GetItemStorageKind(itemId);
        if (storageKind == CargoStorageKind.Cabin || storageKind == CargoStorageKind.ShipDock)
        {
            return Mathf.Max(0, amount);
        }

        return GetItemTransportMassKg(itemId, amount);
    }

    public float GetItemTransportMassKg(string itemId, int amount)
    {
        float fullMassKg = GetItemFullMassKg(itemId, amount);
        ItemConfig item = GetItem(itemId);
        if (item != null && item.cargoStorageKind == CargoStorageKind.ShipDock)
        {
            return fullMassKg * Mathf.Clamp(item.dockedTransportMassFactor <= 0f ? 0.1f : item.dockedTransportMassFactor, 0.01f, 1f);
        }

        return fullMassKg;
    }

    public float GetItemFullMassKg(string itemId, int amount)
    {
        if (amount <= 0) return 0f;

        ItemConfig item = GetItem(itemId);
        if (item != null)
        {
            return Mathf.Max(0, amount) * Mathf.Max(0f, item.massKgPerUnit);
        }

        if (IsPassengerCargoItemId(itemId))
        {
            return Mathf.Max(0, amount) * 100f;
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
        islands.Clear();
        productions.Clear();
        gasCloudTypes.Clear();
        gasClouds.Clear();
        oreTypes.Clear();
        miningZones.Clear();
        leviathanTypes.Clear();
        leviathanZones.Clear();
        technologies.Clear();
        specialModules.Clear();
        ClearShipPartConfigs();
        islandIndustries.Clear();
        industryRecipes.Clear();
        islandArchetypes.Clear();
        islandArchetypeStages.Clear();
        islandSocialNeeds.Clear();
        islandBuildings.Clear();
        flagshipExpeditions.Clear();
        itemsById.Clear();
        islandsById.Clear();
        productionsById.Clear();
        gasCloudTypesById.Clear();
        gasCloudsById.Clear();
        oreTypesById.Clear();
        miningZonesById.Clear();
        leviathanTypesById.Clear();
        leviathanZonesById.Clear();
        technologiesById.Clear();
        specialModulesById.Clear();
        islandIndustriesById.Clear();
        industryRecipesById.Clear();
        islandArchetypesById.Clear();
        islandArchetypeStagesById.Clear();
        islandSocialNeedsById.Clear();
        islandBuildingsById.Clear();
        flagshipExpeditionsById.Clear();
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
                massKgPerUnit = Mathf.Max(0f, ParseFloat(Get(row, "mass_kg_per_unit"), IsPassengerCargoItemId(itemId) ? 100f : 1f)),
                shipSizeClass = ParseShipSizeClass(Get(row, "ship_size_class")),
                dockedTransportMassFactor = Mathf.Clamp(ParseFloat(Get(row, "docked_transport_mass_factor"), 0.1f), 0.01f, 1f)
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
                archetypeId = Get(row, "archetype_id"),
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

    private void LoadLeviathanZones(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            LeviathanZoneConfig zone = new LeviathanZoneConfig
            {
                id = Get(row, "id_leviathan_zone"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                center = new Vector3(
                    ParseFloat(Get(row, "position_x")),
                    ParseFloat(Get(row, "position_y"), 90f),
                    ParseFloat(Get(row, "position_z"))),
                radiusMeters = Mathf.Max(1f, ParseFloat(Get(row, "radius_m"), 600f)),
                initialCount = Mathf.Max(0, ParseInt(Get(row, "initial_count"), 2)),
                maxActive = Mathf.Max(0, ParseInt(Get(row, "max_active"), 4)),
                spawnIntervalSeconds = Mathf.Max(1f, ParseFloat(Get(row, "spawn_interval_seconds"), 900f)),
                minY = ParseFloat(Get(row, "min_y"), 40f),
                maxY = ParseFloat(Get(row, "max_y"), 180f)
            };

            zone.leviathanTypeIds.AddRange(SplitInlineList(Get(row, "leviathan_type_id")));

            if (string.IsNullOrWhiteSpace(zone.id)) continue;
            leviathanZones.Add(zone);
            leviathanZonesById[zone.id] = zone;
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
                gasHarvesterVolumeM3PerSecond = Mathf.Max(0f, ParseFloat(Get(row, "gas_harvester_volume_m3_per_second"))),
                gasHarvesterPowerDrawKw = Mathf.Max(0f, ParseFloat(Get(row, "gas_harvester_power_draw_kw"))),
                gasHarvesterRadiusMeters = Mathf.Max(0f, ParseFloat(Get(row, "gas_harvester_radius_m"))),
                gasHarvesterCycleSeconds = Mathf.Max(0f, ParseFloat(Get(row, "gas_harvester_cycle_seconds"))),
                gasHarvesterWaterOnly = ParseBool01(Get(row, "gas_harvester_water_only")),
                miningImpactHoldCapacityKg = Mathf.Max(0f, ParseFloat(Get(row, "mining_impact_hold_capacity_kg"))),
                miningImpactDamageTakenMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "mining_impact_damage_taken_multiplier"))),
                observationRadiusMeters = Mathf.Max(0f, ParseFloat(Get(row, "observation_radius_m"))),
                observationFactsAtHalfRadiusPerSecond = Mathf.Max(0f, ParseFloat(Get(row, "observation_facts_at_half_radius_per_second"))),
                observationRockInfoEfficiency = Mathf.Clamp01(ParseFloat(Get(row, "observation_rock_info_efficiency"))),
                observationCloudInfoEfficiency = Mathf.Clamp01(ParseFloat(Get(row, "observation_cloud_info_efficiency"))),
                observationLeviathanInfoEfficiency = Mathf.Clamp01(ParseFloat(Get(row, "observation_leviathan_info_efficiency"))),
                surveyPaperToInfoEfficiency = Mathf.Clamp01(ParseFloat(Get(row, "survey_paper_to_info_efficiency"))),
                leviathanAlarmGenerationMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "leviathan_alarm_generation_multiplier"))),
                harpoonWeaponCostPerMinute = Mathf.Max(0f, ParseFloat(Get(row, "harpoon_weapon_cost_per_minute"))),
                harpoonMaxCarcassMassKg = Mathf.Max(0f, ParseFloat(Get(row, "harpoon_max_carcass_mass_kg"))),
                harpoonFlightDamage = Mathf.Max(0f, ParseFloat(Get(row, "harpoon_flight_damage"))),
                harpoonRangeMeters = Mathf.Max(0f, ParseFloat(Get(row, "harpoon_range_m"))),
                needWorkforceRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_workforce_recovery_per_hour"))),
                needHealthRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_health_recovery_per_hour"))),
                needSafetyRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_safety_recovery_per_hour"))),
                needComfortRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_comfort_recovery_per_hour"))),
                needCreativityRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_creativity_recovery_per_hour"))),
                needRepairRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_repair_recovery_per_hour"))),
                needCapitalConnectionRecoveryPerHour = Mathf.Max(0f, ParseFloat(Get(row, "need_capital_connection_recovery_per_hour"))),
                cargoVanCapacityUnits = Mathf.Max(0f, ParseFloat(Get(row, "cargo_van_capacity_units"))),
                passengerSeatCapacity = Mathf.Max(0f, ParseFloat(Get(row, "passenger_seat_capacity"))),
                bulkHoldCapacityLiters = Mathf.Max(0f, ParseFloat(Get(row, "bulk_hold_capacity_l"))),
                liquidTankCapacityLiters = Mathf.Max(0f, ParseFloat(Get(row, "liquid_tank_capacity_l"))),
                gasCylinderCapacityLiters = Mathf.Max(0f, ParseFloat(Get(row, "gas_cylinder_capacity_l"))),
                refrigeratedHoldCapacityLiters = Mathf.Max(0f, ParseFloat(Get(row, "refrigerated_hold_capacity_l"))),
                refrigeratedHoldPowerDrawKw = Mathf.Max(0f, ParseFloat(Get(row, "refrigerated_hold_power_draw_kw"))),
                shipDockSlots = Mathf.Max(0f, ParseFloat(Get(row, "ship_dock_slots"))),
                shipDockMaxClass = ParseShipSizeClass(Get(row, "ship_dock_max_class")),
                dockedShipMassFactor = Mathf.Clamp(ParseFloat(Get(row, "docked_ship_mass_factor"), 0.1f), 0.01f, 1f),
                dockSupportClaudiumPerTonHour = Mathf.Max(0f, ParseFloat(Get(row, "dock_support_claudium_per_ton_hour"), 0.02f))
            };

            module.compatibleSlotTypeIds.AddRange(SplitInlineList(Get(row, "compatible_slot_type")));
            module.allowedCargoItemIds.AddRange(SplitInlineList(Get(row, "allowed_cargo_item_ids")));

            if (string.IsNullOrWhiteSpace(module.id)) continue;
            specialModules.Add(module);
            specialModulesById[module.id] = module;
        }
    }

    private void LoadFlagshipExpeditions(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            FlagshipExpeditionDefinition expedition = new FlagshipExpeditionDefinition
            {
                expeditionId = Get(row, "id_expedition"),
                displayNameRu = Get(row, "local_name_ru"),
                regionId = Get(row, "region_id"),
                sceneName = Get(row, "scene_name"),
                returnDockId = Get(row, "return_dock_id"),
                returnDockKind = ParseDockingLocationKind(Get(row, "return_dock_kind"), DockingLocationKind.Island),
                minimumFlagshipRank = Mathf.Max(0, ParseInt(Get(row, "minimum_flagship_rank"), FlagshipInteriorSimulator.MinimumFlagshipRank)),
                moraleDrainMultiplier = Mathf.Max(0f, ParseFloat(Get(row, "morale_drain_multiplier"), 1f)),
                summaryRu = Get(row, "summary_ru")
            };

            expedition.Normalize();
            if (string.IsNullOrWhiteSpace(expedition.expeditionId)) continue;

            flagshipExpeditions.Add(expedition);
            flagshipExpeditionsById[expedition.expeditionId] = expedition;
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
            return IsPassengerCargoItemId(itemId) ? CargoUnitKind.Passenger : CargoUnitKind.Piece;
        }

        string normalized = value.Trim().Replace("-", "").Replace("_", "");
        if (string.Equals(normalized, "liter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "liters", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "volume", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "volumeliter", StringComparison.OrdinalIgnoreCase))
        {
            return CargoUnitKind.VolumeLiter;
        }

        return Enum.TryParse(value, true, out CargoUnitKind parsed) ? parsed : CargoUnitKind.Piece;
    }

    private static CargoUnitKind NormalizeCargoUnitKind(string itemId, CargoUnitKind parsedUnitKind, CargoStorageKind parsedStorageKind)
    {
        if (IsPassengerCargoItemId(itemId) || parsedUnitKind == CargoUnitKind.Passenger || parsedStorageKind == CargoStorageKind.Cabin)
        {
            return CargoUnitKind.Passenger;
        }

        if (parsedUnitKind == CargoUnitKind.Ship || parsedStorageKind == CargoStorageKind.ShipDock)
        {
            return CargoUnitKind.Ship;
        }

        return CargoUnitKind.Piece;
    }

    private static CargoStorageKind ParseCargoStorageKind(string value, string itemId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return IsPassengerCargoItemId(itemId) ? CargoStorageKind.Cabin : CargoStorageKind.Van;
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

        if (string.Equals(normalized, "refrigerated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "refrigerator", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "fridge", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "cold", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "coldhold", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "refrigeratedhold", StringComparison.OrdinalIgnoreCase))
        {
            return CargoStorageKind.RefrigeratedHold;
        }

        if (string.Equals(normalized, "dock", StringComparison.OrdinalIgnoreCase))
        {
            return CargoStorageKind.ShipDock;
        }

        return Enum.TryParse(value, true, out CargoStorageKind parsed) ? parsed : CargoStorageKind.Van;
    }

    private static CargoStorageKind NormalizeCargoStorageKind(string itemId, CargoUnitKind parsedUnitKind, CargoStorageKind parsedStorageKind)
    {
        if (IsPassengerCargoItemId(itemId) || parsedUnitKind == CargoUnitKind.Passenger || parsedStorageKind == CargoStorageKind.Cabin)
        {
            return CargoStorageKind.Cabin;
        }

        if (parsedUnitKind == CargoUnitKind.Ship || parsedStorageKind == CargoStorageKind.ShipDock)
        {
            return CargoStorageKind.ShipDock;
        }

        return CargoStorageKind.Van;
    }

    private static ShipSizeClass ParseShipSizeClass(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ShipSizeClass.None;
        return Enum.TryParse(value, true, out ShipSizeClass parsed) ? parsed : ShipSizeClass.None;
    }

    private static DockingLocationKind ParseDockingLocationKind(string value, DockingLocationKind fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return Enum.TryParse(value, true, out DockingLocationKind parsed) ? parsed : fallback;
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

    private static bool IsPassengerCargoItemId(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) &&
            itemId.StartsWith("passengers_to_", StringComparison.OrdinalIgnoreCase);
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
    public ShipSizeClass shipSizeClass = ShipSizeClass.None;
    public float dockedTransportMassFactor = 0.1f;
}

public class IslandConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public Vector3 position;
    public string archetypeId = "";
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

public class LeviathanZoneConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public Vector3 center;
    public float radiusMeters = 600f;
    public List<string> leviathanTypeIds = new List<string>();
    public int initialCount = 2;
    public int maxActive = 4;
    public float spawnIntervalSeconds = 900f;
    public float minY = 40f;
    public float maxY = 180f;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
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
    public float gasHarvesterVolumeM3PerSecond;
    public float gasHarvesterPowerDrawKw;
    public float gasHarvesterRadiusMeters;
    public float gasHarvesterCycleSeconds;
    public bool gasHarvesterWaterOnly;
    public float miningImpactHoldCapacityKg;
    public float miningImpactDamageTakenMultiplier;
    public float observationRadiusMeters;
    public float observationFactsAtHalfRadiusPerSecond;
    public float observationRockInfoEfficiency;
    public float observationCloudInfoEfficiency;
    public float observationLeviathanInfoEfficiency;
    public float surveyPaperToInfoEfficiency;
    public float leviathanAlarmGenerationMultiplier;
    public float harpoonWeaponCostPerMinute;
    public float harpoonMaxCarcassMassKg;
    public float harpoonFlightDamage;
    public float harpoonRangeMeters;
    public float needWorkforceRecoveryPerHour;
    public float needHealthRecoveryPerHour;
    public float needSafetyRecoveryPerHour;
    public float needComfortRecoveryPerHour;
    public float needCreativityRecoveryPerHour;
    public float needRepairRecoveryPerHour;
    public float needCapitalConnectionRecoveryPerHour;
    public float cargoVanCapacityUnits;
    public float passengerSeatCapacity;
    public float bulkHoldCapacityLiters;
    public float liquidTankCapacityLiters;
    public float gasCylinderCapacityLiters;
    public float refrigeratedHoldCapacityLiters;
    public float refrigeratedHoldPowerDrawKw;
    public float shipDockSlots;
    public ShipSizeClass shipDockMaxClass = ShipSizeClass.None;
    public float dockedShipMassFactor = 0.1f;
    public float dockSupportClaudiumPerTonHour = 0.02f;
    public List<string> allowedCargoItemIds = new List<string>();

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public static class IslandProductionSimulator
{
    private const double MaxStepSeconds = 60.0;
    private const int MaxSteps = 100000;

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || !config.isLoaded || progress == null || toUtcTicks <= fromUtcTicks) return 0;

        IslandDevelopmentSimulator.EnsureIslandStates(config, progress);

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

        IslandDevelopmentSimulator.EnsureIslandStates(config, progress);

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
        multiplier *= IslandDevelopmentSimulator.GetBaseProductionMultiplier(config, state);
        state.productionProgress += production.productionCountBasePerMinute * multiplier * minutes;

        int completedUnits = Mathf.FloorToInt(state.productionProgress);
        if (completedUnits <= 0) return 0;

        int added = state.AddResource(production.productionItemId, completedUnits);
        int unlockedAdded = IslandDevelopmentSimulator.AddUnlockedStageProduction(config, state, added);
        state.productionProgress -= added;
        return added + unlockedAdded;
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
