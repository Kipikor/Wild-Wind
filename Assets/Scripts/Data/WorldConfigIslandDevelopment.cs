using System;
using System.Collections.Generic;
using UnityEngine;

public partial class WorldConfigDatabase
{
    private void LoadIslandArchetypes(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandArchetypeConfig archetype = new IslandArchetypeConfig
            {
                id = Get(row, "id_archetype"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                heightBand = Get(row, "height_band"),
                baseProductionItemId = Get(row, "base_production_item"),
                startNeedItemId = Get(row, "start_need_item"),
                descriptionRu = Get(row, "description_ru")
            };

            if (string.IsNullOrWhiteSpace(archetype.id)) continue;
            islandArchetypes.Add(archetype);
            islandArchetypesById[archetype.id] = archetype;
        }
    }

    private void LoadIslandArchetypeStages(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandArchetypeStageConfig stage = new IslandArchetypeStageConfig
            {
                id = Get(row, "id_stage"),
                archetypeId = Get(row, "archetype_id"),
                stageIndex = Mathf.Max(0, ParseInt(Get(row, "stage_index"))),
                triggerNeedItemId = Get(row, "trigger_need_item"),
                unlockedProductionItemId = Get(row, "unlocked_production_item"),
                productionMultiplier = Mathf.Max(1f, ParseFloat(Get(row, "production_multiplier"), 1f)),
                opensSocialNeeds = ParseBool(Get(row, "opens_social_needs")),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                descriptionRu = Get(row, "description_ru")
            };

            if (string.IsNullOrWhiteSpace(stage.id)) continue;
            islandArchetypeStages.Add(stage);
            islandArchetypeStagesById[stage.id] = stage;
        }

        islandArchetypeStages.Sort((a, b) =>
        {
            int byArchetype = string.Compare(a.archetypeId, b.archetypeId, StringComparison.Ordinal);
            return byArchetype != 0 ? byArchetype : a.stageIndex.CompareTo(b.stageIndex);
        });
    }

    private void LoadIslandSocialNeeds(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandSocialNeedConfig need = new IslandSocialNeedConfig
            {
                id = Get(row, "id_need"),
                kind = ParseIslandNeedKind(Get(row, "need_kind")),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                recoveryItemId = Get(row, "recovery_item"),
                maxValue = Mathf.Max(1f, ParseFloat(Get(row, "max_value"), 100f)),
                restorePerItem = Mathf.Max(1f, ParseFloat(Get(row, "restore_per_item"), 20f)),
                baseDecayPerHour = Mathf.Max(0f, ParseFloat(Get(row, "base_decay_per_hour"), 0f)),
                loadDecayPerHour = Mathf.Max(0f, ParseFloat(Get(row, "load_decay_per_hour"), 2f)),
                satisfiedThreshold = Mathf.Max(0f, ParseFloat(Get(row, "satisfied_threshold"), 1f)),
                descriptionRu = Get(row, "description_ru")
            };

            if (string.IsNullOrWhiteSpace(need.id)) continue;
            islandSocialNeeds.Add(need);
            islandSocialNeedsById[need.id] = need;
        }
    }

    private void LoadIslandBuildings(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandBuildingConfig building = new IslandBuildingConfig
            {
                id = Get(row, "id_building"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                category = Get(row, "category"),
                industryKind = ParseIndustryKind(Get(row, "production_type")),
                serviceRole = Get(row, "service_role"),
                requiredTechnologyId = Get(row, "required_technology"),
                maxUpgradeLevel = Mathf.Clamp(ParseInt(Get(row, "max_upgrade_level"), 5), 0, 5),
                constructionRecoveryHours = Mathf.Max(0f, ParseFloat(Get(row, "construction_recovery_hours"), 0.5f)),
                workforceLoad = Mathf.Clamp(ParseInt(Get(row, "workforce_load")), 0, 5),
                healthLoad = Mathf.Clamp(ParseInt(Get(row, "health_load")), 0, 5),
                safetyLoad = Mathf.Clamp(ParseInt(Get(row, "safety_load")), 0, 5),
                comfortLoad = Mathf.Clamp(ParseInt(Get(row, "comfort_load")), 0, 5),
                creativityLoad = Mathf.Clamp(ParseInt(Get(row, "creativity_load")), 0, 5),
                descriptionRu = Get(row, "description_ru")
            };

            building.constructionInputs.AddRange(BuildItemAmounts(row, "construction_item", "construction_amount"));

            if (string.IsNullOrWhiteSpace(building.id)) continue;
            islandBuildings.Add(building);
            islandBuildingsById[building.id] = building;
        }
    }

    private static IslandNeedKind ParseIslandNeedKind(string value)
    {
        if (Enum.TryParse(value, true, out IslandNeedKind kind))
        {
            return kind;
        }

        return IslandNeedKind.Workforce;
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}

public enum IslandNeedKind
{
    Workforce,
    Health,
    Safety,
    Comfort,
    Creativity
}

public class IslandArchetypeConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string heightBand = "";
    public string baseProductionItemId = "";
    public string startNeedItemId = "";
    public string descriptionRu = "";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class IslandArchetypeStageConfig
{
    public string id = "";
    public string archetypeId = "";
    public int stageIndex;
    public string triggerNeedItemId = "";
    public string unlockedProductionItemId = "";
    public float productionMultiplier = 1f;
    public bool opensSocialNeeds;
    public string localNameRu = "";
    public string localNameEn = "";
    public string descriptionRu = "";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class IslandSocialNeedConfig
{
    public string id = "";
    public IslandNeedKind kind = IslandNeedKind.Workforce;
    public string localNameRu = "";
    public string localNameEn = "";
    public string recoveryItemId = "";
    public float maxValue = 100f;
    public float restorePerItem = 20f;
    public float baseDecayPerHour;
    public float loadDecayPerHour = 2f;
    public float satisfiedThreshold = 1f;
    public string descriptionRu = "";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class IslandBuildingConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string category = "";
    public IslandIndustryKind industryKind = IslandIndustryKind.Generation;
    public string serviceRole = "";
    public string requiredTechnologyId = "";
    public int maxUpgradeLevel = 5;
    public float constructionRecoveryHours = 0.5f;
    public int workforceLoad;
    public int healthLoad;
    public int safetyLoad;
    public int comfortLoad;
    public int creativityLoad;
    public string descriptionRu = "";
    public List<ProductionItemAmountConfig> constructionInputs = new List<ProductionItemAmountConfig>();

    public bool IsService => !string.IsNullOrWhiteSpace(serviceRole);
    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
    public int TotalNeedLoad => workforceLoad + healthLoad + safetyLoad + comfortLoad + creativityLoad;

    public int GetNeedLoad(IslandNeedKind kind)
    {
        switch (kind)
        {
            case IslandNeedKind.Workforce:
                return workforceLoad;
            case IslandNeedKind.Health:
                return healthLoad;
            case IslandNeedKind.Safety:
                return safetyLoad;
            case IslandNeedKind.Comfort:
                return comfortLoad;
            case IslandNeedKind.Creativity:
                return creativityLoad;
            default:
                return 0;
        }
    }
}
