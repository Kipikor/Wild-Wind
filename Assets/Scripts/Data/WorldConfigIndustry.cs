using System;
using System.Collections.Generic;
using UnityEngine;

public partial class WorldConfigDatabase
{
    private void LoadIslandIndustries(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IslandIndustryConfig industry = new IslandIndustryConfig
            {
                id = Get(row, "id_industry"),
                islandId = Get(row, "island_id"),
                kind = ParseIndustryKind(Get(row, "production_type")),
                recipeId = Get(row, "recipe_id"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en")
            };

            if (string.IsNullOrWhiteSpace(industry.id)) continue;
            islandIndustries.Add(industry);
            islandIndustriesById[industry.id] = industry;
        }
    }

    private void LoadIndustryRecipes(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            IndustryRecipeConfig recipe = new IndustryRecipeConfig
            {
                id = Get(row, "id_recipe"),
                kind = ParseIndustryKind(Get(row, "production_type")),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                durationSeconds = Mathf.Max(1f, ParseFloat(Get(row, "duration_seconds"), 60f)),
                generationCountBasePerMinute = Mathf.Max(0f, ParseFloat(Get(row, "generation_count_per_minute"))),
                fuelItemId = Get(row, "fuel_item"),
                energyCostKwh = Mathf.Max(0f, ParseFloat(Get(row, "energy_kwh"))),
                processingSource = Get(row, "processing_source"),
                reactionBaseSuccessChance = Mathf.Clamp01(ParseFloat(Get(row, "reaction_base_success"), 0.95f)),
                reactionRiskPerSpeed = Mathf.Max(0f, ParseFloat(Get(row, "reaction_risk_per_speed"), 0.01f)),
                conversionGrowthPerCycle = Mathf.Max(0f, ParseFloat(Get(row, "conversion_growth_per_cycle"), 0.08f)),
                conversionDecayPerMinute = Mathf.Max(0f, ParseFloat(Get(row, "conversion_decay_per_minute"), 0.25f)),
                conversionMaxMultiplier = Mathf.Max(1f, ParseFloat(Get(row, "conversion_max_multiplier"), 5f))
            };

            recipe.inputs.AddRange(BuildItemAmounts(row, "input_item", "input_amount"));
            recipe.outputs.AddRange(BuildItemAmounts(row, "output_item", "output_amount"));
            recipe.catalysts.AddRange(BuildCatalysts(row));

            if (string.IsNullOrWhiteSpace(recipe.id)) continue;
            industryRecipes.Add(recipe);
            industryRecipesById[recipe.id] = recipe;
        }
    }

    private void LoadAssemblySteps(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            string recipeId = Get(row, "recipe_id");
            IndustryRecipeConfig recipe = GetIndustryRecipe(recipeId);
            if (recipe == null)
            {
                continue;
            }

            AssemblyStepConfig step = new AssemblyStepConfig
            {
                recipeId = recipeId,
                stepIndex = Mathf.Max(0, ParseInt(Get(row, "step_index"))),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                durationSeconds = Mathf.Max(1f, ParseFloat(Get(row, "duration_seconds"), recipe.durationSeconds))
            };

            step.inputs.AddRange(BuildItemAmounts(row, "input_item", "input_amount"));
            step.outputs.AddRange(BuildItemAmounts(row, "output_item", "output_amount"));
            recipe.assemblySteps.Add(step);
        }

        for (int i = 0; i < industryRecipes.Count; i++)
        {
            IndustryRecipeConfig recipe = industryRecipes[i];
            if (recipe == null || recipe.assemblySteps == null) continue;
            recipe.assemblySteps.Sort((a, b) => a.stepIndex.CompareTo(b.stepIndex));
        }
    }

    private static List<ProductionItemAmountConfig> BuildItemAmounts(Dictionary<string, string> row, string itemKey, string amountKey)
    {
        List<ProductionItemAmountConfig> result = new List<ProductionItemAmountConfig>();
        List<string> itemIds = SplitInlineList(Get(row, itemKey));
        List<string> amounts = SplitInlineList(Get(row, amountKey));
        int count = Mathf.Min(itemIds.Count, amounts.Count);

        for (int i = 0; i < count; i++)
        {
            if (string.IsNullOrWhiteSpace(itemIds[i])) continue;
            float amount = Mathf.Max(0f, ParseFloat(amounts[i]));
            if (amount <= 0f) continue;

            result.Add(new ProductionItemAmountConfig
            {
                itemId = itemIds[i],
                amount = amount
            });
        }

        return result;
    }

    private static List<ProductionCatalystConfig> BuildCatalysts(Dictionary<string, string> row)
    {
        List<ProductionCatalystConfig> result = new List<ProductionCatalystConfig>();
        List<string> itemIds = SplitInlineList(Get(row, "catalyst_item"));
        List<string> amounts = SplitInlineList(Get(row, "catalyst_amount"));
        List<string> bonuses = SplitInlineList(Get(row, "catalyst_success_bonus"));
        int count = Mathf.Min(itemIds.Count, Mathf.Min(amounts.Count, bonuses.Count));

        for (int i = 0; i < count; i++)
        {
            if (string.IsNullOrWhiteSpace(itemIds[i])) continue;
            int amount = Mathf.Max(0, ParseInt(amounts[i], 1));
            if (amount <= 0) continue;

            result.Add(new ProductionCatalystConfig
            {
                itemId = itemIds[i],
                amount = amount,
                successBonus = Mathf.Max(0f, ParseFloat(bonuses[i]))
            });
        }

        return result;
    }

    private static IslandIndustryKind ParseIndustryKind(string value)
    {
        if (Enum.TryParse(value, true, out IslandIndustryKind kind))
        {
            return kind;
        }

        return IslandIndustryKind.Generation;
    }
}

public enum IslandIndustryKind
{
    Generation,
    Processing,
    Manufacturing,
    Reaction,
    Conversion,
    Assembly
}

public class IslandIndustryConfig
{
    public string id = "";
    public string islandId = "";
    public IslandIndustryKind kind = IslandIndustryKind.Generation;
    public string recipeId = "";
    public string localNameRu = "";
    public string localNameEn = "";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class IndustryRecipeConfig
{
    public string id = "";
    public IslandIndustryKind kind = IslandIndustryKind.Generation;
    public string localNameRu = "";
    public string localNameEn = "";
    public float durationSeconds = 60f;
    public float generationCountBasePerMinute;
    public List<ProductionItemAmountConfig> inputs = new List<ProductionItemAmountConfig>();
    public List<ProductionItemAmountConfig> outputs = new List<ProductionItemAmountConfig>();
    public string fuelItemId = "";
    public float energyCostKwh;
    public string processingSource = "";
    public float reactionBaseSuccessChance = 0.95f;
    public float reactionRiskPerSpeed = 0.01f;
    public List<ProductionCatalystConfig> catalysts = new List<ProductionCatalystConfig>();
    public float conversionGrowthPerCycle = 0.08f;
    public float conversionDecayPerMinute = 0.25f;
    public float conversionMaxMultiplier = 5f;
    public List<AssemblyStepConfig> assemblySteps = new List<AssemblyStepConfig>();

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class ProductionItemAmountConfig
{
    public string itemId = "";
    public float amount;
}

public class ProductionCatalystConfig
{
    public string itemId = "";
    public int amount = 1;
    public float successBonus;
}

public class AssemblyStepConfig
{
    public string recipeId = "";
    public int stepIndex;
    public string localNameRu = "";
    public string localNameEn = "";
    public float durationSeconds = 60f;
    public List<ProductionItemAmountConfig> inputs = new List<ProductionItemAmountConfig>();
    public List<ProductionItemAmountConfig> outputs = new List<ProductionItemAmountConfig>();

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? "Этап " + (stepIndex + 1) : localNameRu;
}
