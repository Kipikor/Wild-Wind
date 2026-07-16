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
    private readonly Dictionary<string, ResourceCategoryConfig> resourceCategoriesById = new Dictionary<string, ResourceCategoryConfig>();
    private readonly Dictionary<string, ModifierDefinitionConfig> modifierDefinitionsById = new Dictionary<string, ModifierDefinitionConfig>();
    private readonly Dictionary<string, QuestDefinitionConfig> questDefinitionsById = new Dictionary<string, QuestDefinitionConfig>();

    public List<ItemConfig> items = new List<ItemConfig>();
    public List<PortConfig> ports = new List<PortConfig>();
    public List<GasCondensateTypeConfig> gasCondensateTypes = new List<GasCondensateTypeConfig>();
    public List<OreTypeConfig> oreTypes = new List<OreTypeConfig>();
    public List<LeviathanTypeConfig> leviathanTypes = new List<LeviathanTypeConfig>();
    public List<TechnologyConfig> technologies = new List<TechnologyConfig>();
    public List<SpecialModuleConfig> specialModules = new List<SpecialModuleConfig>();
    public List<ResourceCategoryConfig> resourceCategories = new List<ResourceCategoryConfig>();
    public List<ModifierDefinitionConfig> modifierDefinitions = new List<ModifierDefinitionConfig>();
    public List<QuestDefinitionConfig> questDefinitions = new List<QuestDefinitionConfig>();
    public List<QuickSortieRewardSourceConfig> quickSortieRewardSources = new List<QuickSortieRewardSourceConfig>();
    public CoreTacticalBalanceConfig coreTacticalBalance = new CoreTacticalBalanceConfig();

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
            LoadResourceCategories(Path.Combine(folder, "Resource_category.csv"));
            LoadModifierDefinitions(Path.Combine(folder, "Modifier_catalog.csv"));
            LoadGasCondensateTypes(Path.Combine(folder, "Gas_condensate_type.csv"));
            LoadOreTypes(Path.Combine(folder, "Ore_type.csv"));
            LoadPorts(Path.Combine(folder, "Port.csv"));
            LoadLeviathanTypes(Path.Combine(folder, "Leviathan_type.csv"));
            LoadQuickSortieRewardSources(Path.Combine(folder, "Quick_sortie_reward_source.csv"));
            LoadTechnologies(Path.Combine(folder, "Technology.csv"));
            LoadQuestDefinitions(Path.Combine(folder, "Quest.csv"));
            LoadHulls(Path.Combine(folder, "Hull.csv"));
            LoadClaudiumLoops(Path.Combine(folder, "Claudium_loop.csv"));
            LoadSpecialModules(Path.Combine(folder, "Special_module.csv"));
            LoadShipCatalog(Path.Combine(folder, "Ship_catalog.csv"));
            LoadCoreTacticalBalance(Path.Combine(folder, "Core_tactical_balance.csv"));
            LoadKorshunComponentConfigs(folder);
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

    public ResourceCategoryConfig GetResourceCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return null;
        resourceCategoriesById.TryGetValue(categoryId, out ResourceCategoryConfig category);
        return category;
    }

    public ModifierDefinitionConfig GetModifierDefinition(string modifierId)
    {
        if (string.IsNullOrWhiteSpace(modifierId)) return null;
        modifierDefinitionsById.TryGetValue(modifierId, out ModifierDefinitionConfig modifier);
        return modifier;
    }

    public QuestDefinitionConfig GetQuestDefinition(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId)) return null;
        questDefinitionsById.TryGetValue(questId, out QuestDefinitionConfig quest);
        return quest;
    }

    public string GetItemNameRu(string itemId)
    {
        ItemConfig item = GetItem(itemId);
        if (item == null) return itemId ?? "";
        return string.IsNullOrWhiteSpace(item.localNameRu) ? item.id : item.localNameRu;
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

    public string GetModifierNameRu(string modifierId)
    {
        ModifierDefinitionConfig modifier = GetModifierDefinition(modifierId);
        if (modifier == null) return modifierId ?? "";
        return string.IsNullOrWhiteSpace(modifier.localNameRu) ? modifier.id : modifier.localNameRu;
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
        resourceCategories.Clear();
        modifierDefinitions.Clear();
        questDefinitions.Clear();
        ClearQuickSortieConfigs();
        ClearShipPartConfigs();
        ClearCoreTacticalBalance();
        ClearKorshunComponentConfigs();
        itemsById.Clear();
        portsById.Clear();
        gasCondensateTypesById.Clear();
        oreTypesById.Clear();
        leviathanTypesById.Clear();
        technologiesById.Clear();
        specialModulesById.Clear();
        resourceCategoriesById.Clear();
        modifierDefinitionsById.Clear();
        questDefinitionsById.Clear();
        isLoaded = false;
        lastError = "";
    }

    private void ClearQuickSortieConfigs()
    {
        quickSortieRewardSources.Clear();
    }

    private void ClearCoreTacticalBalance()
    {
        coreTacticalBalance = new CoreTacticalBalanceConfig();
    }

    private void LoadCoreTacticalBalance(string path)
    {
        ClearCoreTacticalBalance();
        if (!File.Exists(path))
        {
            return;
        }

        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            string id = NormalizeBalanceKey(Get(row, "id"));
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            float value = ParseFloat(Get(row, "value"));
            switch (id)
            {
                case "explosive_radius_reference_mass_kg":
                    coreTacticalBalance.explosiveRadiusReferenceMassKg = Mathf.Max(0.001f, value);
                    break;
                case "explosive_radius_reference_m":
                    coreTacticalBalance.explosiveRadiusReferenceMeters = Mathf.Max(0.001f, value);
                    break;
                case "explosive_radius_mass_exponent":
                    coreTacticalBalance.explosiveRadiusMassExponent = Mathf.Clamp(value, 0.1f, 1f);
                    break;
            }
        }
    }

    private void LoadQuickSortieRewardSources(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            QuickSortieRewardSourceConfig source = new QuickSortieRewardSourceConfig
            {
                activityId = Get(row, "activity_id"),
                minRating = Mathf.Clamp(ParseInt(Get(row, "min_rating"), 1), 1, 100),
                sourceKind = Get(row, "source_kind"),
                sourceId = Get(row, "source_id"),
                weight = Mathf.Max(0.01f, ParseFloat(Get(row, "weight"), 1f)),
                notesRu = Get(row, "notes_ru")
            };

            source.Normalize();
            if (string.IsNullOrWhiteSpace(source.activityId)
                || string.IsNullOrWhiteSpace(source.sourceKind)
                || string.IsNullOrWhiteSpace(source.sourceId))
            {
                continue;
            }

            quickSortieRewardSources.Add(source);
        }

        quickSortieRewardSources.Sort(CompareQuickSortieRewardSources);
    }

    private static int CompareQuickSortieRewardSources(QuickSortieRewardSourceConfig left, QuickSortieRewardSourceConfig right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int activity = string.Compare(left.activityId, right.activityId, StringComparison.OrdinalIgnoreCase);
        if (activity != 0) return activity;
        int minRating = left.minRating.CompareTo(right.minRating);
        return minRating != 0 ? minRating : string.Compare(left.sourceId, right.sourceId, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadItems(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            string itemId = Get(row, "id_item");
            ItemConfig item = new ItemConfig
            {
                id = itemId,
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                massKgPerUnit = Mathf.Max(0f, ParseFloat(Get(row, "mass_kg_per_unit"), 1f))
            };

            if (string.IsNullOrWhiteSpace(item.id)) continue;
            items.Add(item);
            itemsById[item.id] = item;
        }
    }

    private void LoadResourceCategories(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            ResourceCategoryConfig category = new ResourceCategoryConfig
            {
                id = Get(row, "category_id"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                sourceConfig = Get(row, "source_config"),
                notesRu = Get(row, "notes_ru")
            };

            category.itemIds.AddRange(SplitInlineList(Get(row, "item_ids"), '|'));

            if (string.IsNullOrWhiteSpace(category.id)) continue;
            resourceCategories.Add(category);
            resourceCategoriesById[category.id] = category;
        }
    }

    private void LoadModifierDefinitions(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            ModifierDefinitionConfig modifier = new ModifierDefinitionConfig
            {
                id = Get(row, "modifier_id"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                categoryId = Get(row, "category_id"),
                valueKind = Get(row, "value_kind"),
                stackingRule = Get(row, "stacking_rule"),
                defaultOperation = Get(row, "default_operation"),
                descriptionRu = Get(row, "description_ru")
            };

            if (string.IsNullOrWhiteSpace(modifier.id)) continue;
            modifierDefinitions.Add(modifier);
            modifierDefinitionsById[modifier.id] = modifier;
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

            List<string> itemIds = SplitInlineList(Get(row, "composition_id_item"));
            List<string> shares = SplitInlineList(Get(row, "composition_share"));
            int count = Mathf.Min(itemIds.Count, shares.Count);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(itemIds[i])) continue;
                type.composition.Add(new LeviathanButcheryCompositionConfig
                {
                    itemId = itemIds[i],
                    share = Mathf.Max(0f, ParseFloat(shares[i]))
                });
            }

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
                categoryId = Get(row, "category_id"),
                categoryNameRu = Get(row, "category_name_ru"),
                treeColumn = Mathf.Max(0, ParseInt(Get(row, "tree_column"), 0)),
                treeRow = Mathf.Max(0, ParseInt(Get(row, "tree_row"), 0)),
                iconText = Get(row, "icon_text"),
                lockSummaryRu = Get(row, "lock_summary_ru"),
                unlockSummaryRu = Get(row, "unlock_summary_ru"),
                cycleTimeSeconds = Mathf.Max(0, ParseInt(Get(row, "cycle_time_seconds"), 1)),
                requiredCycles = Mathf.Max(1, ParseInt(Get(row, "required_cycles"), 1))
            };

            if (string.IsNullOrWhiteSpace(technology.categoryId))
            {
                technology.categoryId = string.IsNullOrWhiteSpace(technology.branch) ? "general" : technology.branch;
            }

            if (string.IsNullOrWhiteSpace(technology.categoryNameRu))
            {
                technology.categoryNameRu = technology.categoryId;
            }

            technology.prerequisiteTechnologyIds.AddRange(SplitInlineList(Get(row, "required_technology")));
            List<string> spCosts = SplitInlineList(Get(row, "sp_cost_by_level"));
            for (int i = 0; i < spCosts.Count; i++)
            {
                int cost = Mathf.Max(0, ParseInt(spCosts[i]));
                if (cost > 0)
                {
                    technology.spCostByLevel.Add(cost);
                }
            }

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

            List<string> modifierIds = SplitInlineList(Get(row, "modifier_id"));
            List<string> operations = SplitInlineList(Get(row, "modifier_operation"));
            List<string> values = SplitInlineList(Get(row, "modifier_value_per_level"));
            List<string> targets = SplitInlineList(Get(row, "modifier_target_id"));
            for (int i = 0; i < modifierIds.Count; i++)
            {
                string modifierId = modifierIds[i];
                if (string.IsNullOrWhiteSpace(modifierId)) continue;

                TechnologyModifierGrantConfig grant = new TechnologyModifierGrantConfig
                {
                    modifierId = modifierId,
                    operation = i < operations.Count ? operations[i] : "",
                    valuePerLevel = i < values.Count ? ParseFloat(values[i]) : 0f,
                    targetId = i < targets.Count ? targets[i] : ""
                };

                if (string.IsNullOrWhiteSpace(grant.operation))
                {
                    ModifierDefinitionConfig definition = GetModifierDefinition(grant.modifierId);
                    grant.operation = definition != null ? definition.defaultOperation : "add_percent";
                }

                technology.modifierGrants.Add(grant);
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

    private void LoadQuestDefinitions(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            QuestDefinitionConfig quest = new QuestDefinitionConfig
            {
                id = Get(row, "quest_id"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                factionId = Get(row, "faction_id"),
                questKind = Get(row, "quest_kind"),
                categoryId = Get(row, "category_id"),
                descriptionRu = Get(row, "description_ru"),
                objectiveType = Get(row, "objective_type"),
                targetId = Get(row, "target_id"),
                targetAmount = Mathf.Max(1, ParseInt(Get(row, "target_amount"), 1)),
                activationMode = Get(row, "activation_mode"),
                claimMode = Get(row, "claim_mode"),
                requiredQuestId = Get(row, "required_quest_id"),
                rewardItemId = Get(row, "reward_item_id"),
                rewardAmount = Mathf.Max(0, ParseInt(Get(row, "reward_amount"), 0)),
                rewardItem2Id = Get(row, "reward_item2_id"),
                rewardItem2Amount = Mathf.Max(0, ParseInt(Get(row, "reward_item2_amount"), 0)),
                rewardFreightAmount = Mathf.Max(0, ParseInt(Get(row, "reward_freight_amount"), 0)),
                rewardMasteryAmount = Mathf.Max(0, ParseInt(Get(row, "reward_mastery_amount"), 0)),
                rewardReputationFactionId = Get(row, "reward_reputation_faction_id"),
                rewardReputationAmount = Mathf.Max(0, ParseInt(Get(row, "reward_reputation_amount"), 0)),
                sortOrder = ParseInt(Get(row, "sort_order"), 0)
            };

            if (string.IsNullOrWhiteSpace(quest.id)) continue;
            quest.factionId = string.IsNullOrWhiteSpace(quest.factionId) ? "" : quest.factionId.Trim().ToLowerInvariant();
            quest.questKind = string.IsNullOrWhiteSpace(quest.questKind) ? "" : quest.questKind.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(quest.categoryId)) quest.categoryId = "main";
            if (string.IsNullOrWhiteSpace(quest.objectiveType)) quest.objectiveType = "event";
            if (string.IsNullOrWhiteSpace(quest.targetId)) quest.targetId = "any";
            if (string.IsNullOrWhiteSpace(quest.activationMode)) quest.activationMode = QuestDefinitionConfig.ActivationRetroactive;
            if (string.IsNullOrWhiteSpace(quest.claimMode)) quest.claimMode = QuestDefinitionConfig.ClaimManual;
            quest.rewardReputationFactionId = string.IsNullOrWhiteSpace(quest.rewardReputationFactionId)
                ? quest.factionId
                : quest.rewardReputationFactionId.Trim().ToLowerInvariant();
            questDefinitions.Add(quest);
            questDefinitionsById[quest.id] = quest;
        }

        questDefinitions.Sort((left, right) =>
        {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int order = left.sortOrder.CompareTo(right.sortOrder);
            return order != 0 ? order : string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
        });
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

    private static List<string> SplitInlineList(string value, char separator = ',')
    {
        List<string> result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        string[] parts = value.Split(separator);
        for (int i = 0; i < parts.Length; i++)
        {
            string item = parts[i].Trim();
            if (!string.IsNullOrWhiteSpace(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        return row != null && row.TryGetValue(key, out string value) ? value : "";
    }

    private static string NormalizeBalanceKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
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

public class CoreTacticalBalanceConfig
{
    public const float DefaultExplosiveRadiusReferenceMassKg = 50f;
    public const float DefaultExplosiveRadiusReferenceMeters = 20f;
    public const float DefaultExplosiveRadiusMassExponent = 0.5f;

    public float explosiveRadiusReferenceMassKg = DefaultExplosiveRadiusReferenceMassKg;
    public float explosiveRadiusReferenceMeters = DefaultExplosiveRadiusReferenceMeters;
    public float explosiveRadiusMassExponent = DefaultExplosiveRadiusMassExponent;
}

public class ItemConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public float massKgPerUnit = 1f;
}

public class ResourceCategoryConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string sourceConfig = "";
    public string notesRu = "";
    public List<string> itemIds = new List<string>();

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
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
    public List<LeviathanButcheryCompositionConfig> composition = new List<LeviathanButcheryCompositionConfig>();

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
    public int CarcassMassKg => Mathf.Max(1, Mathf.RoundToInt(massKg * Mathf.Clamp01(carcassMassFraction)));
}

public class LeviathanButcheryCompositionConfig
{
    public string itemId = "";
    public float share;
}

public class TechnologyConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public int rank;
    public string branch = "";
    public string categoryId = "";
    public string categoryNameRu = "";
    public int treeColumn;
    public int treeRow;
    public string iconText = "";
    public string lockSummaryRu = "";
    public string unlockSummaryRu = "";
    public int cycleTimeSeconds = 1;
    public int requiredCycles = 1;
    public List<int> spCostByLevel = new List<int>();
    public List<string> prerequisiteTechnologyIds = new List<string>();
    public List<TechnologyCostConfig> cycleCost = new List<TechnologyCostConfig>();
    public List<TechnologyModifierGrantConfig> modifierGrants = new List<TechnologyModifierGrantConfig>();

    public string CategoryDisplayNameRu => string.IsNullOrWhiteSpace(categoryNameRu) ? categoryId : categoryNameRu;
}

public class TechnologyCostConfig
{
    public string itemId = "";
    public int amount;
}

public class TechnologyModifierGrantConfig
{
    public string modifierId = "";
    public string operation = "";
    public float valuePerLevel;
    public string targetId = "";
}

public class ModifierDefinitionConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string categoryId = "";
    public string valueKind = "";
    public string stackingRule = "";
    public string defaultOperation = "";
    public string descriptionRu = "";
}

public class QuestDefinitionConfig
{
    public const string ActivationRetroactive = "retroactive";
    public const string ActivationFromAccept = "from_accept";
    public const string ClaimAuto = "auto";
    public const string ClaimManual = "manual";

    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string factionId = "";
    public string questKind = "";
    public string categoryId = "";
    public string descriptionRu = "";
    public string objectiveType = "";
    public string targetId = "";
    public int targetAmount = 1;
    public string activationMode = ActivationRetroactive;
    public string claimMode = ClaimManual;
    public string requiredQuestId = "";
    public string rewardItemId = "";
    public int rewardAmount;
    public string rewardItem2Id = "";
    public int rewardItem2Amount;
    public int rewardFreightAmount;
    public int rewardMasteryAmount;
    public string rewardReputationFactionId = "";
    public int rewardReputationAmount;
    public int sortOrder;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
    public bool IsRetroactive => !string.Equals(activationMode, ActivationFromAccept, StringComparison.OrdinalIgnoreCase);
    public bool IsAutoClaim => string.Equals(claimMode, ClaimAuto, StringComparison.OrdinalIgnoreCase);
}

public class QuickSortieRewardSourceConfig
{
    public string activityId = "";
    public int minRating = 1;
    public string sourceKind = "";
    public string sourceId = "";
    public float weight = 1f;
    public string notesRu = "";

    public void Normalize()
    {
        activityId = string.IsNullOrWhiteSpace(activityId) ? "" : activityId.Trim().ToLowerInvariant();
        sourceKind = string.IsNullOrWhiteSpace(sourceKind) ? "" : sourceKind.Trim().ToLowerInvariant();
        sourceId = string.IsNullOrWhiteSpace(sourceId) ? "" : sourceId.Trim();
        minRating = Mathf.Clamp(minRating, 1, 100);
        weight = Mathf.Max(0.01f, weight);
        notesRu ??= "";
    }
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
