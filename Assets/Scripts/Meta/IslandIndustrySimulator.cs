using System;
using System.Collections.Generic;
using UnityEngine;

public static class IslandIndustrySimulator
{
    private const double MaxStepSeconds = 30.0;
    private const int MaxCyclesPerIndustryAdvance = 512;

    private static readonly List<ProductionItemAmountConfig> CompositionScratch = new List<ProductionItemAmountConfig>();

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || !config.isLoaded || progress == null || toUtcTicks <= fromUtcTicks) return 0;

        EnsureIslandStates(config, progress);

        int changed = 0;
        long currentTicks = fromUtcTicks;
        int guard = 0;
        while (currentTicks < toUtcTicks && guard < 100000)
        {
            guard++;
            long nextTicks = currentTicks + TimeSpan.FromSeconds(MaxStepSeconds).Ticks;
            if (nextTicks > toUtcTicks)
            {
                nextTicks = toUtcTicks;
            }

            changed += AdvanceStep(config, progress, currentTicks, nextTicks);
            currentTicks = nextTicks;
        }

        return changed;
    }

    public static void EnsureIslandStates(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || !config.isLoaded || progress == null) return;

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null || string.IsNullOrWhiteSpace(industry.id) || string.IsNullOrWhiteSpace(industry.islandId)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(industry.islandId, true);
            IslandIndustryState state = islandState.GetIndustryState(industry.id, true);
            IndustryRecipeConfig recipe = config.GetIndustryRecipe(industry.recipeId);
            state.kind = industry.kind;
            state.recipeId = recipe != null ? recipe.id : industry.recipeId;
            state.Normalize();
        }
    }

    public static bool CancelCycle(PlayerProgress progress, string islandId, string industryId, out string message)
    {
        message = "";
        if (progress == null || string.IsNullOrWhiteSpace(islandId) || string.IsNullOrWhiteSpace(industryId))
        {
            message = "Нет острова или производства.";
            return false;
        }

        IslandProductionState island = progress.GetIslandProductionState(islandId, false);
        IslandIndustryState state = island != null ? island.GetIndustryState(industryId, false) : null;
        if (state == null || !state.active)
        {
            message = "Активного цикла нет.";
            return false;
        }

        state.active = false;
        state.cycleStartedUtcTicks = 0;
        state.nextCompletionUtcTicks = 0;
        state.activeInputResourceId = "";
        state.lastMessage = state.kind == IslandIndustryKind.Reaction
            ? "Реакция прервана: партия потеряна."
            : "Цикл прерван: уже загруженные ресурсы потеряны.";
        message = state.lastMessage;
        return true;
    }

    public static bool SetReactionSpeed(PlayerProgress progress, string islandId, string industryId, float speedMultiplier, out string message)
    {
        message = "";
        IslandProductionState island = progress != null ? progress.GetIslandProductionState(islandId, false) : null;
        IslandIndustryState state = island != null ? island.GetIndustryState(industryId, false) : null;
        if (state == null)
        {
            message = "Производство не найдено.";
            return false;
        }

        state.reactionSpeedMultiplier = Mathf.Clamp(speedMultiplier, 1f, 50f);
        state.lastMessage = "Скорость реакции: x" + state.reactionSpeedMultiplier.ToString("0.#") + ".";
        message = state.lastMessage;
        return true;
    }

    private static int AdvanceStep(WorldConfigDatabase config, PlayerProgress progress, long fromTicks, long toTicks)
    {
        int changed = 0;
        float deltaMinutes = Mathf.Max(0f, (float)new TimeSpan(toTicks - fromTicks).TotalMinutes);

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null || string.IsNullOrWhiteSpace(industry.id)) continue;

            IndustryRecipeConfig recipe = config.GetIndustryRecipe(industry.recipeId);
            if (recipe == null)
            {
                continue;
            }

            IslandProductionState islandState = progress.GetIslandProductionState(industry.islandId, true);
            IslandIndustryState state = islandState.GetIndustryState(industry.id, true);
            state.kind = industry.kind;
            state.recipeId = recipe.id;

            if (industry.kind == IslandIndustryKind.Generation)
            {
                changed += AdvanceGeneration(islandState, state, recipe, deltaMinutes);
                continue;
            }

            if (industry.kind == IslandIndustryKind.Conversion && !state.active)
            {
                DecayConversion(state, recipe, deltaMinutes);
            }

            changed += AdvanceCycledIndustry(config, islandState, state, industry, recipe, fromTicks, toTicks);
        }

        return changed;
    }

    private static int AdvanceGeneration(IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, float minutes)
    {
        if (islandState == null || state == null || recipe == null || recipe.generationCountBasePerMinute <= 0f) return 0;

        state.productionProgress += recipe.generationCountBasePerMinute * Mathf.Max(0f, minutes);
        int completed = Mathf.FloorToInt(state.productionProgress);
        if (completed <= 0) return 0;

        int added = 0;
        for (int i = 0; i < recipe.outputs.Count; i++)
        {
            ProductionItemAmountConfig output = recipe.outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0f) continue;
            added += islandState.AddResource(output.itemId, Mathf.Max(1, Mathf.FloorToInt(output.amount * completed)));
        }

        state.productionProgress -= completed;
        if (added > 0)
        {
            state.completedCycles += completed;
            state.lastMessage = "Генерация выгрузила на склад: " + added + " ед.";
        }

        return added;
    }

    private static int AdvanceCycledIndustry(
        WorldConfigDatabase config,
        IslandProductionState islandState,
        IslandIndustryState state,
        IslandIndustryConfig industry,
        IndustryRecipeConfig recipe,
        long fromTicks,
        long toTicks)
    {
        int changed = 0;
        long cursor = fromTicks;
        int guard = 0;

        if (!state.active)
        {
            TryStartCycle(config, islandState, state, industry, recipe, cursor);
        }

        while (state.active && state.nextCompletionUtcTicks <= toTicks && guard < MaxCyclesPerIndustryAdvance)
        {
            guard++;
            cursor = Math.Max(cursor, state.nextCompletionUtcTicks);
            changed += CompleteCycle(config, islandState, state, industry, recipe, cursor);
            state.active = false;
            state.cycleStartedUtcTicks = 0;
            state.nextCompletionUtcTicks = 0;

            TryStartCycle(config, islandState, state, industry, recipe, cursor);
        }

        return changed;
    }

    private static bool TryStartCycle(
        WorldConfigDatabase config,
        IslandProductionState islandState,
        IslandIndustryState state,
        IslandIndustryConfig industry,
        IndustryRecipeConfig recipe,
        long startTicks)
    {
        if (state == null || islandState == null || industry == null || recipe == null || state.active) return false;

        switch (industry.kind)
        {
            case IslandIndustryKind.Processing:
                return TryStartProcessing(config, islandState, state, recipe, startTicks);
            case IslandIndustryKind.Manufacturing:
                return TryStartManufacturing(config, islandState, state, recipe, startTicks);
            case IslandIndustryKind.Reaction:
                return TryStartReaction(config, islandState, state, recipe, startTicks);
            case IslandIndustryKind.Conversion:
                return TryStartConversion(config, islandState, state, recipe, startTicks);
            case IslandIndustryKind.Assembly:
                return TryStartAssemblyStep(islandState, state, recipe, startTicks);
            default:
                return false;
        }
    }

    private static bool TryStartProcessing(WorldConfigDatabase config, IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, long startTicks)
    {
        string inputResourceId = FindProcessingInputResource(config, islandState, recipe);
        if (string.IsNullOrWhiteSpace(inputResourceId))
        {
            state.lastMessage = "Ждет руду или конденсат для переработки.";
            return false;
        }

        if (!TrySpendFuelEnergy(config, islandState, recipe, out string fuelMessage))
        {
            state.lastMessage = fuelMessage;
            return false;
        }

        if (!islandState.TrySpendResource(inputResourceId, 1))
        {
            state.lastMessage = "Не удалось забрать сырье со склада.";
            return false;
        }

        state.activeInputResourceId = inputResourceId;
        StartTimedCycle(state, recipe.durationSeconds, startTicks);
        state.lastMessage = "Переработка начата: " + inputResourceId + ".";
        return true;
    }

    private static bool TryStartManufacturing(WorldConfigDatabase config, IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, long startTicks)
    {
        if (!HasItems(islandState, recipe.inputs, 1f))
        {
            state.lastMessage = "Ждет ресурсы по рецепту.";
            return false;
        }

        if (!TrySpendFuelEnergy(config, islandState, recipe, out string fuelMessage))
        {
            state.lastMessage = fuelMessage;
            return false;
        }

        TrySpendItems(islandState, recipe.inputs);

        StartTimedCycle(state, recipe.durationSeconds, startTicks);
        state.lastMessage = "Производство по рецепту начато.";
        return true;
    }

    private static bool TryStartReaction(WorldConfigDatabase config, IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, long startTicks)
    {
        if (!HasItems(islandState, recipe.inputs, 1f))
        {
            state.lastMessage = "Ждет реагенты.";
            return false;
        }

        if (!TrySpendFuelEnergy(config, islandState, recipe, out string fuelMessage))
        {
            state.lastMessage = fuelMessage;
            return false;
        }

        TrySpendItems(islandState, recipe.inputs);

        float catalystBonus = SpendAvailableCatalysts(islandState, recipe);
        float speed = Mathf.Clamp(state.reactionSpeedMultiplier <= 0f ? 1f : state.reactionSpeedMultiplier, 1f, 50f);
        float successChance = Mathf.Clamp01(recipe.reactionBaseSuccessChance - recipe.reactionRiskPerSpeed * (speed - 1f) + catalystBonus);
        state.activeReactionSuccessChance = successChance;

        StartTimedCycle(state, Mathf.Max(1f, recipe.durationSeconds / speed), startTicks);
        state.lastMessage = $"Реакция начата: x{speed:0.#}, шанс {successChance * 100f:0}%";
        return true;
    }

    private static bool TryStartConversion(WorldConfigDatabase config, IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, long startTicks)
    {
        float multiplier = Mathf.Max(1f, state.conversionMultiplier);
        if (!HasItems(islandState, recipe.inputs, multiplier))
        {
            state.lastMessage = "Маховик ждет входные ресурсы; темп падает.";
            return false;
        }

        if (!TrySpendFuelEnergy(config, islandState, recipe, out string fuelMessage))
        {
            state.lastMessage = fuelMessage;
            return false;
        }

        TrySpendScaledItems(islandState, recipe.inputs, multiplier);

        StartTimedCycle(state, recipe.durationSeconds, startTicks);
        state.lastMessage = "Маховик держит темп x" + state.conversionMultiplier.ToString("0.##") + ".";
        return true;
    }

    private static bool TryStartAssemblyStep(IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, long startTicks)
    {
        if (recipe.assemblySteps == null || recipe.assemblySteps.Count == 0)
        {
            state.lastMessage = "У сборки нет этапов в конфиге.";
            return false;
        }

        state.activeStepIndex = Mathf.Clamp(state.activeStepIndex, 0, recipe.assemblySteps.Count - 1);
        AssemblyStepConfig step = recipe.assemblySteps[state.activeStepIndex];
        if (!TrySpendItems(islandState, step.inputs))
        {
            state.lastMessage = "Сборка ждет ресурсы для этапа: " + step.DisplayNameRu + ".";
            return false;
        }

        StartTimedCycle(state, step.durationSeconds, startTicks);
        state.lastMessage = "Сборка: " + step.DisplayNameRu + ".";
        return true;
    }

    private static int CompleteCycle(
        WorldConfigDatabase config,
        IslandProductionState islandState,
        IslandIndustryState state,
        IslandIndustryConfig industry,
        IndustryRecipeConfig recipe,
        long completedTicks)
    {
        switch (industry.kind)
        {
            case IslandIndustryKind.Processing:
                return CompleteProcessing(config, islandState, state, recipe);
            case IslandIndustryKind.Manufacturing:
                return CompleteManufacturing(islandState, state, recipe);
            case IslandIndustryKind.Reaction:
                return CompleteReaction(islandState, state, recipe, completedTicks);
            case IslandIndustryKind.Conversion:
                return CompleteConversion(islandState, state, recipe);
            case IslandIndustryKind.Assembly:
                return CompleteAssemblyStep(islandState, state, recipe);
            default:
                return 0;
        }
    }

    private static int CompleteProcessing(WorldConfigDatabase config, IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe)
    {
        BuildProcessingOutputs(config, recipe, state.activeInputResourceId, CompositionScratch);
        int added = AddFractionalOutputs(islandState, state, CompositionScratch, 1f);
        state.completedCycles++;
        state.activeInputResourceId = "";
        state.lastMessage = added > 0
            ? "Переработка выгрузила минералы: " + added + " ед."
            : "Переработка добавила фракции минералов в накопитель.";
        return Mathf.Max(1, added);
    }

    private static int CompleteManufacturing(IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe)
    {
        int added = AddOutputs(islandState, recipe.outputs, 1f);
        state.completedCycles++;
        state.lastMessage = "Рецепт завершен, выгружено: " + added + " ед.";
        return Mathf.Max(1, added);
    }

    private static int CompleteReaction(IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe, long completedTicks)
    {
        bool success = Roll01(state.industryId, completedTicks, state.completedCycles + state.failedCycles) <= state.activeReactionSuccessChance;
        if (!success)
        {
            state.failedCycles++;
            state.lastMessage = "Реакция сорвалась: партия потеряна.";
            return 1;
        }

        int added = AddOutputs(islandState, recipe.outputs, 1f);
        state.completedCycles++;
        state.lastMessage = "Реакция успешна, выгружено: " + added + " ед.";
        return Mathf.Max(1, added);
    }

    private static int CompleteConversion(IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe)
    {
        float multiplier = Mathf.Max(1f, state.conversionMultiplier);
        int added = AddOutputs(islandState, recipe.outputs, multiplier);
        state.conversionMultiplier = Mathf.Min(Mathf.Max(1f, recipe.conversionMaxMultiplier), multiplier + Mathf.Max(0f, recipe.conversionGrowthPerCycle));
        state.completedCycles++;
        state.lastMessage = "Маховик выдал " + added + " ед., следующий темп x" + state.conversionMultiplier.ToString("0.##") + ".";
        return Mathf.Max(1, added);
    }

    private static int CompleteAssemblyStep(IslandProductionState islandState, IslandIndustryState state, IndustryRecipeConfig recipe)
    {
        if (recipe.assemblySteps == null || recipe.assemblySteps.Count == 0) return 0;

        state.activeStepIndex = Mathf.Clamp(state.activeStepIndex, 0, recipe.assemblySteps.Count - 1);
        AssemblyStepConfig step = recipe.assemblySteps[state.activeStepIndex];
        int added = AddOutputs(islandState, step.outputs, 1f);
        state.activeStepIndex++;

        if (state.activeStepIndex >= recipe.assemblySteps.Count)
        {
            state.activeStepIndex = 0;
            state.completedCycles++;
            state.lastMessage = "Сборка завершена.";
        }
        else
        {
            state.lastMessage = "Этап завершен: " + step.DisplayNameRu + ". Ждет следующий комплект.";
        }

        return Mathf.Max(1, added);
    }

    private static void StartTimedCycle(IslandIndustryState state, float durationSeconds, long startTicks)
    {
        long durationTicks = TimeSpan.FromSeconds(Mathf.Max(1f, durationSeconds)).Ticks;
        state.active = true;
        state.cycleStartedUtcTicks = startTicks;
        state.nextCompletionUtcTicks = startTicks + durationTicks;
    }

    private static bool TrySpendItems(IslandProductionState islandState, List<ProductionItemAmountConfig> items)
    {
        if (items == null || items.Count == 0) return true;
        if (!HasItems(islandState, items, 1f)) return false;

        for (int i = 0; i < items.Count; i++)
        {
            ProductionItemAmountConfig item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0f) continue;
            islandState.TrySpendResource(item.itemId, Mathf.Max(1, Mathf.CeilToInt(item.amount)));
        }

        return true;
    }

    private static bool TrySpendScaledItems(IslandProductionState islandState, List<ProductionItemAmountConfig> items, float multiplier)
    {
        if (items == null || items.Count == 0) return true;
        if (!HasItems(islandState, items, multiplier)) return false;

        for (int i = 0; i < items.Count; i++)
        {
            ProductionItemAmountConfig item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0f) continue;
            islandState.TrySpendResource(item.itemId, Mathf.Max(1, Mathf.CeilToInt(item.amount * multiplier)));
        }

        return true;
    }

    private static bool HasItems(IslandProductionState islandState, List<ProductionItemAmountConfig> items, float multiplier)
    {
        if (islandState == null) return false;
        if (items == null) return true;

        for (int i = 0; i < items.Count; i++)
        {
            ProductionItemAmountConfig item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0f) continue;
            int needed = Mathf.Max(1, Mathf.CeilToInt(item.amount * Mathf.Max(1f, multiplier)));
            if (islandState.GetResourceAmount(item.itemId) < needed) return false;
        }

        return true;
    }

    private static int AddOutputs(IslandProductionState islandState, List<ProductionItemAmountConfig> outputs, float multiplier)
    {
        if (islandState == null || outputs == null) return 0;

        int added = 0;
        for (int i = 0; i < outputs.Count; i++)
        {
            ProductionItemAmountConfig output = outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0f) continue;
            int amount = Mathf.Max(1, Mathf.FloorToInt(output.amount * Mathf.Max(1f, multiplier)));
            added += islandState.AddResource(output.itemId, amount);
        }

        return added;
    }

    private static int AddFractionalOutputs(IslandProductionState islandState, IslandIndustryState state, List<ProductionItemAmountConfig> outputs, float multiplier)
    {
        if (islandState == null || state == null || outputs == null) return 0;

        int added = 0;
        for (int i = 0; i < outputs.Count; i++)
        {
            ProductionItemAmountConfig output = outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0f) continue;

            ProductionOutputBufferState buffer = state.GetOutputBuffer(output.itemId, true);
            buffer.amount += output.amount * Mathf.Max(0f, multiplier);
            int whole = Mathf.FloorToInt(buffer.amount);
            if (whole <= 0) continue;

            added += islandState.AddResource(output.itemId, whole);
            buffer.amount -= whole;
        }

        return added;
    }

    private static bool TrySpendFuelEnergy(WorldConfigDatabase config, IslandProductionState islandState, IndustryRecipeConfig recipe, out string message)
    {
        message = "";
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.fuelItemId) || recipe.energyCostKwh <= 0f) return true;

        ItemConfig fuel = config != null ? config.GetItem(recipe.fuelItemId) : null;
        float energyPerKg = fuel != null ? fuel.energyKwhPerKg : 0f;
        if (energyPerKg <= 0f)
        {
            message = "Топливо не имеет энергоемкости в Item.csv: " + recipe.fuelItemId + ".";
            return false;
        }

        int fuelKg = Mathf.Max(1, Mathf.CeilToInt(recipe.energyCostKwh / energyPerKg));
        if (!islandState.TrySpendResource(recipe.fuelItemId, fuelKg))
        {
            message = "Ждет топливо: " + recipe.fuelItemId + " x" + fuelKg + ".";
            return false;
        }

        return true;
    }

    private static float SpendAvailableCatalysts(IslandProductionState islandState, IndustryRecipeConfig recipe)
    {
        if (islandState == null || recipe == null || recipe.catalysts == null) return 0f;

        float bonus = 0f;
        for (int i = 0; i < recipe.catalysts.Count; i++)
        {
            ProductionCatalystConfig catalyst = recipe.catalysts[i];
            if (catalyst == null || string.IsNullOrWhiteSpace(catalyst.itemId) || catalyst.amount <= 0) continue;
            if (islandState.GetResourceAmount(catalyst.itemId) < catalyst.amount) continue;
            if (!islandState.TrySpendResource(catalyst.itemId, catalyst.amount)) continue;

            bonus += Mathf.Max(0f, catalyst.successBonus);
        }

        return bonus;
    }

    private static string FindProcessingInputResource(WorldConfigDatabase config, IslandProductionState islandState, IndustryRecipeConfig recipe)
    {
        if (config == null || islandState == null || recipe == null) return "";

        string source = recipe.processingSource ?? "";
        if (source.Equals("ore", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.oreTypes.Count; i++)
            {
                OreTypeConfig ore = config.oreTypes[i];
                if (ore != null && !string.IsNullOrWhiteSpace(ore.oreItemId) && islandState.GetResourceAmount(ore.oreItemId) > 0)
                {
                    return ore.oreItemId;
                }
            }
        }

        if (source.Equals("gas", StringComparison.OrdinalIgnoreCase) || source.Equals("condensate", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.gasCloudTypes.Count; i++)
            {
                GasCloudTypeConfig gas = config.gasCloudTypes[i];
                if (gas != null && !string.IsNullOrWhiteSpace(gas.condensateItemId) && islandState.GetResourceAmount(gas.condensateItemId) > 0)
                {
                    return gas.condensateItemId;
                }
            }
        }

        if (recipe.inputs != null)
        {
            for (int i = 0; i < recipe.inputs.Count; i++)
            {
                ProductionItemAmountConfig input = recipe.inputs[i];
                if (input != null && !string.IsNullOrWhiteSpace(input.itemId) && islandState.GetResourceAmount(input.itemId) > 0)
                {
                    return input.itemId;
                }
            }
        }

        return "";
    }

    private static void BuildProcessingOutputs(WorldConfigDatabase config, IndustryRecipeConfig recipe, string inputResourceId, List<ProductionItemAmountConfig> results)
    {
        results.Clear();
        if (config == null || recipe == null || string.IsNullOrWhiteSpace(inputResourceId)) return;

        string source = recipe.processingSource ?? "";
        if (source.Equals("ore", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.oreTypes.Count; i++)
            {
                OreTypeConfig ore = config.oreTypes[i];
                if (ore == null || ore.oreItemId != inputResourceId) continue;
                for (int j = 0; j < ore.composition.Count; j++)
                {
                    OreMineralCompositionConfig composition = ore.composition[j];
                    if (composition == null || string.IsNullOrWhiteSpace(composition.mineralItemId)) continue;
                    results.Add(new ProductionItemAmountConfig
                    {
                        itemId = composition.mineralItemId,
                        amount = Mathf.Max(0f, composition.share)
                    });
                }

                return;
            }
        }

        if (source.Equals("gas", StringComparison.OrdinalIgnoreCase) || source.Equals("condensate", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.gasCloudTypes.Count; i++)
            {
                GasCloudTypeConfig gas = config.gasCloudTypes[i];
                if (gas == null || gas.condensateItemId != inputResourceId) continue;
                for (int j = 0; j < gas.composition.Count; j++)
                {
                    GasCloudCompositionConfig composition = gas.composition[j];
                    if (composition == null || string.IsNullOrWhiteSpace(composition.itemId)) continue;
                    results.Add(new ProductionItemAmountConfig
                    {
                        itemId = composition.itemId,
                        amount = Mathf.Max(0f, composition.share)
                    });
                }

                return;
            }
        }

        results.AddRange(recipe.outputs);
    }

    private static void DecayConversion(IslandIndustryState state, IndustryRecipeConfig recipe, float minutes)
    {
        if (state == null || recipe == null || state.conversionMultiplier <= 1f || minutes <= 0f) return;

        state.conversionMultiplier = Mathf.Max(1f, state.conversionMultiplier - recipe.conversionDecayPerMinute * minutes);
    }

    private static float Roll01(string salt, long ticks, int cycle)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string text = (salt ?? "") + ":" + ticks.ToString() + ":" + cycle.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (hash % 10000u) / 9999f;
        }
    }
}
