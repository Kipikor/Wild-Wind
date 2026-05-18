using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class ProductionAutoTestRunner : MonoBehaviour
{
    private const string LogPrefix = "[ProductionAutoTest] ";
    private const string DefaultConfigFolder = "Data/Config";

    [Header("Автопроверка производств")]
    [InspectorName("Мета-состояние")]
    public MetaGameState metaGameState;
    [InspectorName("Запускать при старте Play Mode")]
    public bool runOnStart = true;
    [InspectorName("Минут симуляции")]
    public float simulatedMinutes = 12f;
    [InspectorName("Логировать успешные проверки")]
    public bool logSuccessfulChecks;
    [InspectorName("Тихий режим")]
    public bool quietMode = true;
    [InspectorName("Отключать живой тик на время теста")]
    public bool disableLiveProcessTick = true;

    private bool hasRun;

    private IEnumerator Start()
    {
        if (!runOnStart)
        {
            yield break;
        }

        yield return null;
        RunSelfTest();
    }

    [ContextMenu("Запустить автопроверку производств")]
    public void RunSelfTest()
    {
        if (hasRun)
        {
            Debug.Log(LogPrefix + "Автопроверка уже запускалась для этого объекта.", this);
            return;
        }

        hasRun = true;
        ProductionAutoTestReport report = new ProductionAutoTestReport(this, logSuccessfulChecks && !quietMode);
        MetaGameState meta = ResolveMetaGameState(report);
        bool previousLiveTick = meta != null && meta.processRealTimeWhilePlaying;

        try
        {
            if (meta != null && disableLiveProcessTick)
            {
                meta.processRealTimeWhilePlaying = false;
            }

            WorldConfigDatabase config = ResolveWorldConfig(meta, report);
            if (config == null || !config.isLoaded)
            {
                report.Fail("Конфиги мира не загружены.");
                report.Finish(0, simulatedMinutes);
                return;
            }

            ValidateConfig(config, report);
            TestReactionControls(config, report);

            PlayerProgress progress = CreateStockedProgress(config);
            Dictionary<string, Dictionary<string, int>> before = SnapshotStorages(progress);
            long startTicks = DateTime.UtcNow.Ticks;
            long finishTicks = startTicks + TimeSpan.FromMinutes(Mathf.Max(1f, simulatedMinutes)).Ticks;
            int changedEvents = IslandIndustrySimulator.Advance(config, progress, startTicks, finishTicks);

            report.Check(changedEvents > 0, "Симуляция дала события производства: " + changedEvents + ".");
            ValidateRuntimeResults(config, progress, before, report);
            report.Finish(changedEvents, simulatedMinutes);
        }
        catch (Exception exception)
        {
            report.Fail("Исключение во время автопроверки: " + exception.GetType().Name + " - " + exception.Message);
            Debug.LogException(exception, this);
            report.Finish(0, simulatedMinutes);
        }
        finally
        {
            if (meta != null && disableLiveProcessTick)
            {
                meta.processRealTimeWhilePlaying = previousLiveTick;
            }
        }
    }

    private MetaGameState ResolveMetaGameState(ProductionAutoTestReport report)
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        report.Check(metaGameState != null, metaGameState != null
            ? "MetaGameState найден."
            : "MetaGameState не найден. Будет использована прямая загрузка CSV.");
        return metaGameState;
    }

    private static WorldConfigDatabase ResolveWorldConfig(MetaGameState meta, ProductionAutoTestReport report)
    {
        if (meta != null)
        {
            meta.EnsureProgressInitialized();
            WorldConfigDatabase metaConfig = meta.WorldConfig;
            if (metaConfig != null && metaConfig.isLoaded)
            {
                report.Pass("Конфиги взяты из MetaGameState.");
                return metaConfig;
            }
        }

        WorldConfigDatabase config = new WorldConfigDatabase();
        string folder = meta != null && !string.IsNullOrWhiteSpace(meta.worldConfigFolder)
            ? meta.worldConfigFolder
            : DefaultConfigFolder;
        config.LoadFromAssetsConfigFolder(folder);
        report.Check(config.isLoaded, config.isLoaded
            ? "CSV-конфиги загружены из Assets/" + folder + "."
            : "CSV-конфиги не загрузились: " + config.lastError);
        return config;
    }

    private static void ValidateConfig(WorldConfigDatabase config, ProductionAutoTestReport report)
    {
        report.Check(config.items.Count > 0, "Item.csv содержит предметы: " + config.items.Count + ".");
        report.Check(config.islandIndustries.Count > 0, "Production_industry.csv содержит линии: " + config.islandIndustries.Count + ".");
        report.Check(config.industryRecipes.Count > 0, "Production_recipe.csv содержит рецепты: " + config.industryRecipes.Count + ".");

        for (int i = 0; i < Enum.GetValues(typeof(IslandIndustryKind)).Length; i++)
        {
            IslandIndustryKind kind = (IslandIndustryKind)i;
            report.Check(HasIndustryKind(config, kind), "Есть линия типа " + kind + ".");
        }

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null)
            {
                report.Fail("В списке производств есть пустая строка.");
                continue;
            }

            IndustryRecipeConfig recipe = config.GetIndustryRecipe(industry.recipeId);
            report.Check(recipe != null, "Линия " + industry.id + " нашла рецепт " + industry.recipeId + ".");
            if (recipe == null)
            {
                continue;
            }

            report.Check(recipe.kind == industry.kind, "Тип линии " + industry.id + " совпадает с типом рецепта.");
            if (industry.kind == IslandIndustryKind.Assembly)
            {
                report.Check(recipe.assemblySteps.Count > 0, "У сборки " + industry.id + " есть этапы: " + recipe.assemblySteps.Count + ".");
            }

            if (industry.kind == IslandIndustryKind.Reaction)
            {
                report.Check(recipe.catalysts.Count > 0, "У реакции " + industry.id + " есть катализаторы.");
            }

            if (industry.kind == IslandIndustryKind.Processing)
            {
                report.Check(!string.IsNullOrWhiteSpace(recipe.fuelItemId) && recipe.energyCostKwh > 0f,
                    "Переработка " + industry.id + " требует топливо и энергию.");
            }
        }
    }

    private static bool HasIndustryKind(WorldConfigDatabase config, IslandIndustryKind kind)
    {
        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry != null && industry.kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static void TestReactionControls(WorldConfigDatabase config, ProductionAutoTestReport report)
    {
        IslandIndustryConfig reaction = FindIndustry(config, IslandIndustryKind.Reaction);
        if (reaction == null)
        {
            report.Fail("Не найдена линия реакции для проверки скорости и прерывания.");
            return;
        }

        PlayerProgress progress = CreateStockedProgress(config);
        IslandProductionState storage = progress.GetIslandProductionState(reaction.islandId, true);
        IslandIndustryState state = storage.GetIndustryState(reaction.id, true);

        IslandIndustrySimulator.SetReactionSpeed(progress, reaction.islandId, reaction.id, 50f, out string speedMessage);
        report.Check(Mathf.Approximately(state.reactionSpeedMultiplier, 50f), "Ползунок реакции ставится на x50. " + speedMessage);
        IslandIndustrySimulator.SetReactionSpeed(progress, reaction.islandId, reaction.id, 1f, out _);

        long startTicks = DateTime.UtcNow.Ticks;
        IslandIndustrySimulator.Advance(config, progress, startTicks, startTicks + TimeSpan.FromSeconds(1).Ticks);
        bool cancelResult = IslandIndustrySimulator.CancelCycle(progress, reaction.islandId, reaction.id, out string cancelMessage);
        report.Check(cancelResult && !state.active, "Активную реакцию можно прервать с потерей партии. " + cancelMessage);
    }

    private static IslandIndustryConfig FindIndustry(WorldConfigDatabase config, IslandIndustryKind kind)
    {
        if (config == null) return null;
        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry != null && industry.kind == kind)
            {
                return industry;
            }
        }

        return null;
    }

    private static PlayerProgress CreateStockedProgress(WorldConfigDatabase config)
    {
        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        IslandIndustrySimulator.EnsureIslandStates(config, progress);

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null) continue;

            IndustryRecipeConfig recipe = config.GetIndustryRecipe(industry.recipeId);
            IslandProductionState storage = progress.GetIslandProductionState(industry.islandId, true);
            StockIndustry(storage, config, industry, recipe);

            if (industry.kind == IslandIndustryKind.Reaction)
            {
                IslandIndustrySimulator.SetReactionSpeed(progress, industry.islandId, industry.id, 1f, out _);
            }
        }

        return progress;
    }

    private static void StockIndustry(IslandProductionState storage, WorldConfigDatabase config, IslandIndustryConfig industry, IndustryRecipeConfig recipe)
    {
        if (storage == null || industry == null) return;

        StockCommonResources(storage);
        if (recipe == null) return;

        StockAmounts(storage, recipe.inputs, 300);
        if (!string.IsNullOrWhiteSpace(recipe.fuelItemId))
        {
            SetAtLeast(storage, recipe.fuelItemId, 600);
        }

        for (int i = 0; i < recipe.catalysts.Count; i++)
        {
            ProductionCatalystConfig catalyst = recipe.catalysts[i];
            if (catalyst == null || string.IsNullOrWhiteSpace(catalyst.itemId)) continue;
            SetAtLeast(storage, catalyst.itemId, Mathf.Max(50, catalyst.amount * 20));
        }

        for (int i = 0; i < recipe.assemblySteps.Count; i++)
        {
            AssemblyStepConfig step = recipe.assemblySteps[i];
            if (step == null) continue;
            StockAmounts(storage, step.inputs, 300);
        }

        if (industry.kind == IslandIndustryKind.Processing)
        {
            StockProcessingInputs(storage, config, recipe);
        }
    }

    private static void StockCommonResources(IslandProductionState storage)
    {
        SetAtLeast(storage, "food", 300);
        SetAtLeast(storage, "water", 300);
        SetAtLeast(storage, "aerolite", 300);
        SetAtLeast(storage, "wood", 300);
        SetAtLeast(storage, "metal", 300);
        SetAtLeast(storage, "mechanisms", 300);
        SetAtLeast(storage, "tools", 300);
        SetAtLeast(storage, "medicines", 300);
        SetAtLeast(storage, "weapon", 300);
        SetAtLeast(storage, "paper", 300);
        SetAtLeast(storage, "cloth", 300);
        SetAtLeast(storage, "charcoal", 900);
        SetAtLeast(storage, "sulfur", 300);
        SetAtLeast(storage, "alcohol", 120);
        SetAtLeast(storage, "claudium", 300);
        SetAtLeast(storage, "claudite", 300);
    }

    private static void StockAmounts(IslandProductionState storage, List<ProductionItemAmountConfig> amounts, int minimum)
    {
        if (storage == null || amounts == null) return;
        for (int i = 0; i < amounts.Count; i++)
        {
            ProductionItemAmountConfig amount = amounts[i];
            if (amount == null || string.IsNullOrWhiteSpace(amount.itemId)) continue;
            SetAtLeast(storage, amount.itemId, Mathf.Max(minimum, Mathf.CeilToInt(amount.amount * 40f)));
        }
    }

    private static void StockProcessingInputs(IslandProductionState storage, WorldConfigDatabase config, IndustryRecipeConfig recipe)
    {
        if (storage == null || config == null || recipe == null) return;

        string source = recipe.processingSource ?? "";
        if (source.Equals("ore", StringComparison.OrdinalIgnoreCase))
        {
            int stocked = 0;
            for (int i = 0; i < config.oreTypes.Count && stocked < 3; i++)
            {
                OreTypeConfig ore = config.oreTypes[i];
                if (ore == null || string.IsNullOrWhiteSpace(ore.oreItemId)) continue;
                SetAtLeast(storage, ore.oreItemId, 24);
                stocked++;
            }
        }

        if (source.Equals("gas", StringComparison.OrdinalIgnoreCase) || source.Equals("condensate", StringComparison.OrdinalIgnoreCase))
        {
            int stocked = 0;
            for (int i = 0; i < config.gasCloudTypes.Count && stocked < 3; i++)
            {
                GasCloudTypeConfig gas = config.gasCloudTypes[i];
                if (gas == null || string.IsNullOrWhiteSpace(gas.condensateItemId)) continue;
                SetAtLeast(storage, gas.condensateItemId, 24);
                stocked++;
            }
        }
    }

    private static void SetAtLeast(IslandProductionState storage, string itemId, int amount)
    {
        if (storage == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;
        if (storage.GetResourceAmount(itemId) < amount)
        {
            storage.SetResourceAmount(itemId, amount);
        }
    }

    private static Dictionary<string, Dictionary<string, int>> SnapshotStorages(PlayerProgress progress)
    {
        Dictionary<string, Dictionary<string, int>> result = new Dictionary<string, Dictionary<string, int>>();
        if (progress == null || progress.islandProductions == null) return result;

        for (int i = 0; i < progress.islandProductions.Count; i++)
        {
            IslandProductionState island = progress.islandProductions[i];
            if (island == null || string.IsNullOrWhiteSpace(island.islandId)) continue;

            Dictionary<string, int> resources = new Dictionary<string, int>();
            if (island.storage != null)
            {
                for (int j = 0; j < island.storage.Count; j++)
                {
                    ResourceStack stack = island.storage[j];
                    if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId)) continue;
                    resources[stack.resourceId] = stack.amount;
                }
            }

            result[island.islandId] = resources;
        }

        return result;
    }

    private static void ValidateRuntimeResults(
        WorldConfigDatabase config,
        PlayerProgress progress,
        Dictionary<string, Dictionary<string, int>> before,
        ProductionAutoTestReport report)
    {
        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null) continue;

            IndustryRecipeConfig recipe = config.GetIndustryRecipe(industry.recipeId);
            IslandProductionState storage = progress.GetIslandProductionState(industry.islandId, false);
            IslandIndustryState state = storage != null ? storage.GetIndustryState(industry.id, false) : null;

            report.Check(state != null, "Есть runtime-состояние линии " + industry.id + ".");
            if (state == null)
            {
                continue;
            }

            report.Check(state.completedCycles > 0, "Линия " + industry.id + " завершила циклы: " + state.completedCycles + ".");
            if (industry.kind == IslandIndustryKind.Reaction)
            {
                report.Check(state.failedCycles == 0, "Реакция " + industry.id + " на безопасной скорости не сорвала партию.");
            }

            List<string> outputIds = BuildExpectedOutputIds(config, industry, recipe);
            if (outputIds.Count > 0)
            {
                report.Check(HasAnyOutputGain(storage, state, before, industry.islandId, outputIds),
                    "Линия " + industry.id + " выгрузила или накопила ожидаемый продукт.");
            }

            if (industry.kind == IslandIndustryKind.Conversion)
            {
                report.Check(state.conversionMultiplier > 1f, "Маховик " + industry.id + " разогнался до x" + state.conversionMultiplier.ToString("0.##") + ".");
            }

            if (industry.kind == IslandIndustryKind.Assembly)
            {
                report.Check(state.activeStepIndex == 0 || state.completedCycles > 0, "Сборка " + industry.id + " прошла этапы без зависания.");
            }
        }
    }

    private static List<string> BuildExpectedOutputIds(WorldConfigDatabase config, IslandIndustryConfig industry, IndustryRecipeConfig recipe)
    {
        List<string> outputIds = new List<string>();
        if (industry == null || recipe == null) return outputIds;

        if (industry.kind == IslandIndustryKind.Processing)
        {
            AddProcessingOutputIds(config, recipe, outputIds);
        }
        else if (industry.kind == IslandIndustryKind.Assembly)
        {
            for (int i = 0; i < recipe.assemblySteps.Count; i++)
            {
                AssemblyStepConfig step = recipe.assemblySteps[i];
                if (step == null) continue;
                AddOutputIds(step.outputs, outputIds);
            }
        }
        else
        {
            AddOutputIds(recipe.outputs, outputIds);
        }

        return outputIds;
    }

    private static void AddProcessingOutputIds(WorldConfigDatabase config, IndustryRecipeConfig recipe, List<string> outputIds)
    {
        if (config == null || recipe == null || outputIds == null) return;

        string source = recipe.processingSource ?? "";
        if (source.Equals("ore", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.oreTypes.Count; i++)
            {
                OreTypeConfig ore = config.oreTypes[i];
                if (ore == null || ore.composition == null) continue;
                for (int j = 0; j < ore.composition.Count; j++)
                {
                    OreMineralCompositionConfig composition = ore.composition[j];
                    AddOutputId(composition != null ? composition.mineralItemId : "", outputIds);
                }
            }
        }

        if (source.Equals("gas", StringComparison.OrdinalIgnoreCase) || source.Equals("condensate", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.gasCloudTypes.Count; i++)
            {
                GasCloudTypeConfig gas = config.gasCloudTypes[i];
                if (gas == null || gas.composition == null) continue;
                for (int j = 0; j < gas.composition.Count; j++)
                {
                    GasCloudCompositionConfig composition = gas.composition[j];
                    AddOutputId(composition != null ? composition.itemId : "", outputIds);
                }
            }
        }

        AddOutputIds(recipe.outputs, outputIds);
    }

    private static void AddOutputIds(List<ProductionItemAmountConfig> outputs, List<string> outputIds)
    {
        if (outputs == null || outputIds == null) return;
        for (int i = 0; i < outputs.Count; i++)
        {
            ProductionItemAmountConfig output = outputs[i];
            AddOutputId(output != null ? output.itemId : "", outputIds);
        }
    }

    private static void AddOutputId(string itemId, List<string> outputIds)
    {
        if (string.IsNullOrWhiteSpace(itemId) || outputIds == null || outputIds.Contains(itemId)) return;
        outputIds.Add(itemId);
    }

    private static bool HasAnyOutputGain(
        IslandProductionState storage,
        IslandIndustryState state,
        Dictionary<string, Dictionary<string, int>> before,
        string islandId,
        List<string> outputIds)
    {
        if (outputIds == null || outputIds.Count == 0) return true;

        for (int i = 0; i < outputIds.Count; i++)
        {
            string itemId = outputIds[i];
            int previous = GetSnapshotAmount(before, islandId, itemId);
            int current = storage != null ? storage.GetResourceAmount(itemId) : 0;
            if (current > previous)
            {
                return true;
            }

            ProductionOutputBufferState buffer = state != null ? state.GetOutputBuffer(itemId, false) : null;
            if (buffer != null && buffer.amount > 0.001f)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetSnapshotAmount(Dictionary<string, Dictionary<string, int>> snapshot, string islandId, string itemId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(islandId) || string.IsNullOrWhiteSpace(itemId)) return 0;
        if (!snapshot.TryGetValue(islandId, out Dictionary<string, int> resources) || resources == null) return 0;
        return resources.TryGetValue(itemId, out int amount) ? amount : 0;
    }

    private sealed class ProductionAutoTestReport
    {
        private readonly UnityEngine.Object context;
        private readonly bool logPasses;
        private readonly StringBuilder failures = new StringBuilder();

        public int Checks { get; private set; }
        public int FailureCount { get; private set; }

        public ProductionAutoTestReport(UnityEngine.Object context, bool logPasses)
        {
            this.context = context;
            this.logPasses = logPasses;
        }

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Pass(message);
            }
            else
            {
                Fail(message);
            }
        }

        public void Pass(string message)
        {
            Checks++;
            if (logPasses)
            {
                Debug.Log(LogPrefix + "OK: " + message, context);
            }
        }

        public void Fail(string message)
        {
            Checks++;
            FailureCount++;
            failures.AppendLine("- " + message);
            Debug.LogError(LogPrefix + "FAIL: " + message, context);
        }

        public void Finish(int changedEvents, float minutes)
        {
            if (FailureCount == 0)
            {
                Debug.Log(LogPrefix + "OK: автопроверка производств пройдена. Проверок: " + Checks
                    + ", событий: " + changedEvents
                    + ", симуляция: " + minutes.ToString("0.#") + " мин.", context);
                return;
            }

            Debug.LogError(LogPrefix + "НЕ ОК: автопроверка нашла проблем: " + FailureCount
                + " из " + Checks + ". Событий: " + changedEvents
                + ".\n" + failures, context);
        }
    }
}
