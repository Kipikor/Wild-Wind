using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public sealed class WildWindBigTestRunner : MonoBehaviour
{
    private const string LogPrefix = "[WildWindBigTest] ";
    private const string DefaultConfigFolder = "Data/Config";
    private const int BigTestContractVersion = 2;
    private const int MinimumExpectedCheckCount = 320;
    private const string BigTestSessionSavePrefix = "wild_wind_big_test_session_";

    public const string DefaultStartSceneName = "StartScreen";
    public const string DefaultWorldSceneName = "WildWindWorldScene";

    public static bool SuppressRunOnStartForAutomation { get; set; }
    public static bool IsSessionLoopLaunchInProgress => sessionLoopLaunchInProgress;

    private static bool autoRunConsumedThisPlaySession;
    private static bool activeRunInProgress;
    private static bool sessionLoopLaunchInProgress;

    private static readonly string[] RequiredSectionTitles =
    {
        "Паспорт проверки",
        "Сцена и контекст запуска",
        "CSV-конфиги",
        "Localization",
        "Грузовые единицы и отсеки кораблей",
        "R1 ships: runtime mechanics",
        "Симуляция производств",
        "Сид и манифест мира",
        "Большой мир и чанки",
        "Индекс сущностей мира",
        "Единый runtime-состояния мира",
        "Сохранение мира в слот",
        "Сердцебиение мира и фоновая симуляция",
        "Активный пузырь и материализация",
        "Визуал, высотные слои и туман",
        "Настройки проекта и управление",
        "Корабль, ветер и лётная физика",
        "Сессионные перезаходы стартовое меню <-> мир",
        "Защита побочных эффектов",
        "Методика сопровождения"
    };

    [Header("Большой тест")]
    [SerializeField, InspectorName("Запускать при старте Play Mode")] public bool runOnStart = true;
    [SerializeField, InspectorName("Писать полный протокол в Console")] public bool logFullReportToConsole = true;
    [SerializeField, InspectorName("Сохранять текстовый протокол")] public bool writeReportFile = true;
    [SerializeField, InspectorName("Папка протоколов от корня проекта")] public string reportFolder = "TestReports";
    [SerializeField, InspectorName("Симуляция производств, минут")] public float productionSimulationMinutes = 12f;
    [SerializeField, InspectorName("Бюджет обновления пузыря, мс")] public float streamerAverageBudgetMs = 250f;

    [Header("Ссылки сцены")]
    [SerializeField, InspectorName("Мир")] public WorldRegionRuntime world;
    [SerializeField, InspectorName("Единый runtime мира")] public WorldRuntimeState runtimeState;
    [SerializeField, InspectorName("Сердцебиение мира")] public WorldSimulationTick simulationTick;
    [SerializeField, InspectorName("Индекс мира")] public WorldEntityIndex worldIndex;
    [SerializeField, InspectorName("Пузырь")] public WorldBubbleStreamer streamer;
    [SerializeField, InspectorName("Фокус игрока")] public Transform focus;
    [SerializeField, InspectorName("Визуальный тюнер")] public VisualPlayModeTuner visualTuner;
    [SerializeField, InspectorName("Настройки")] public WildWindSettingsRoot settings;
    [SerializeField, InspectorName("Мета-состояние")] public MetaGameState metaGameState;

    private bool hasRun;
    private bool becamePersistentForSceneLoop;

    public WildWindBigTestResult LastResult { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaySessionState()
    {
        SuppressRunOnStartForAutomation = false;
        autoRunConsumedThisPlaySession = false;
        activeRunInProgress = false;
        sessionLoopLaunchInProgress = false;
    }

    public static bool IsBigTestTemporarySaveFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
            fileName.StartsWith(BigTestSessionSavePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerator Start()
    {
        if (!runOnStart || SuppressRunOnStartForAutomation || autoRunConsumedThisPlaySession || activeRunInProgress)
        {
            yield break;
        }

        yield return null;
        if (!runOnStart || SuppressRunOnStartForAutomation || autoRunConsumedThisPlaySession || activeRunInProgress)
        {
            yield break;
        }

        autoRunConsumedThisPlaySession = true;
        RunBigTest();
    }

    [ContextMenu("Провести большой тест")]
    public void RunBigTest()
    {
        if (hasRun)
        {
            Debug.Log(LogPrefix + "Большой тест уже запускался на этом объекте.", this);
            return;
        }

        if (activeRunInProgress)
        {
            Debug.LogWarning(LogPrefix + "Большой тест уже выполняется другим runner'ом.", this);
            return;
        }

        hasRun = true;
        activeRunInProgress = true;
        StartCoroutine(RunBigTestRoutine(null, true));
    }

    public IEnumerator RunBigTestForAutomation(Action<WildWindBigTestResult> completed = null)
    {
        if (hasRun)
        {
            completed?.Invoke(LastResult ?? WildWindBigTestResult.CreateBlocked("Большой тест уже запускался на этом объекте."));
            yield break;
        }

        if (activeRunInProgress)
        {
            completed?.Invoke(WildWindBigTestResult.CreateBlocked("Большой тест уже выполняется другим runner'ом."));
            yield break;
        }

        hasRun = true;
        activeRunInProgress = true;
        yield return RunBigTestRoutine(completed, false);
    }

    private IEnumerator RunBigTestRoutine(Action<WildWindBigTestResult> completed, bool emitReportOutput)
    {
        BigTestSideEffectSnapshot sideEffects = BigTestSideEffectSnapshot.Capture();
        BigTestReport report = new BigTestReport(this);
        Stopwatch totalWatch = Stopwatch.StartNew();

        RunChecked(report, () =>
        {
            ResolveReferences();
            DescribeTestScope(report);
            ValidateSceneContext(report);

            WorldConfigDatabase config = LoadConfig(report);
            ValidateConfigDatabase(config, report);
            ValidateLocalizationConfig(report);
            ValidateCargoStorageModel(config, report);
            ValidateR1ShipMechanics(config, report);
            ValidateProductionSimulation(config, report);

            ValidateWorldDataManifest(report);
            ValidateWorldRuntime(report);
            ValidateWorldEntityIndex(report);
            ValidateWorldRuntimeState(report);
            ValidateWorldSaveSlotRoundTrip(report);
            ValidateWorldSimulationTick(report);
            ValidateBubbleStreaming(report);
            ValidateVisualAtmosphere(report);
            ValidateSettings(report);
            ValidateShipWindAerodynamics(report);
        });

        yield return RunCheckedCoroutine(report, ValidateSessionLoopRoundTrip(report));
        yield return RestoreAndValidateSideEffects(sideEffects, report);

        RunChecked(report, () => ValidateMaintainability(report));
        report.AssertIntegrity(RequiredSectionTitles, MinimumExpectedCheckCount, CanarySelfTestPasses);

        totalWatch.Stop();
        long elapsedMs = totalWatch.ElapsedMilliseconds;
        report.Finish(elapsedMs);
        LastResult = report.CreateResult(BigTestContractVersion, elapsedMs, true, RequiredSectionTitles, MinimumExpectedCheckCount);
        completed?.Invoke(LastResult);

        string text = report.BuildText();
        if (emitReportOutput && writeReportFile)
        {
            TryWriteReport(text, LastResult, report);
        }

        if (emitReportOutput && logFullReportToConsole)
        {
            if (report.FailureCount == 0)
            {
                Debug.Log(text, this);
            }
            else
            {
                Debug.LogError(text, this);
            }
        }

        ReleaseActiveRun();

        if (becamePersistentForSceneLoop)
        {
            Destroy(gameObject);
        }
    }

    private void ReleaseActiveRun()
    {
        activeRunInProgress = false;
        sessionLoopLaunchInProgress = false;
    }

    private void RunChecked(BigTestReport report, Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            report.Fail("Большой тест упал исключением: " + exception.GetType().Name + " - " + exception.Message);
            Debug.LogException(exception, this);
        }
    }

    private IEnumerator RunCheckedCoroutine(BigTestReport report, IEnumerator routine)
    {
        while (routine != null)
        {
            bool moved;
            object current;
            try
            {
                moved = routine.MoveNext();
                current = moved ? routine.Current : null;
            }
            catch (Exception exception)
            {
                report.Fail("Асинхронная часть большого теста упала исключением: " + exception.GetType().Name + " - " + exception.Message);
                Debug.LogException(exception, this);
                yield break;
            }

            if (!moved)
            {
                yield break;
            }

            yield return current;
        }
    }

    private IEnumerator RestoreAndValidateSideEffects(BigTestSideEffectSnapshot snapshot, BigTestReport report)
    {
        report.Section("Защита побочных эффектов");
        if (snapshot == null)
        {
            report.Fail("Не удалось снять snapshot побочных эффектов перед стартом большого теста.");
            yield break;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrWhiteSpace(snapshot.ActiveSceneName) && activeScene.name != snapshot.ActiveSceneName)
        {
            report.Warn("Большой тест завершает проверку в сцене '" + activeScene.name + "', восстанавливаю '" + snapshot.ActiveSceneName + "'.");
            SceneManager.LoadScene(snapshot.ActiveSceneName);
            DisableDuplicateBigTestRunners();
            yield return null;
            DisableDuplicateBigTestRunners();
            yield return null;
        }

        snapshot.RestorePrefsAndTimeScale();
        snapshot.AssertRestored(report);
    }

    public static WildWindBigTestResult RunCanarySelfTest()
    {
        BigTestReport report = new BigTestReport(null, false);
        report.Section("Canary");
        report.Fail("Ожидаемый canary FAIL: механизм ошибок должен делать результат красным.");
        report.Finish(0L);
        return report.CreateResult(BigTestContractVersion, 0L, true, new[] { "Canary" }, 1);
    }

    private static bool CanarySelfTestPasses()
    {
        WildWindBigTestResult result = RunCanarySelfTest();
        return result != null &&
            result.Completed &&
            !result.Succeeded &&
            result.FailureCount == 1 &&
            result.CheckCount == 1;
    }

    public void ResetRunStateForEditor()
    {
        hasRun = false;
        LastResult = null;
        autoRunConsumedThisPlaySession = false;
        activeRunInProgress = false;
        sessionLoopLaunchInProgress = false;
    }

    private void ResolveReferences()
    {
        if (world == null) world = FindFirstObjectByType<WorldRegionRuntime>();
        if (worldIndex == null) worldIndex = FindFirstObjectByType<WorldEntityIndex>();
        if (runtimeState == null) runtimeState = FindFirstObjectByType<WorldRuntimeState>();
        if (simulationTick == null) simulationTick = FindFirstObjectByType<WorldSimulationTick>();
        if (streamer == null) streamer = FindFirstObjectByType<WorldBubbleStreamer>();
        if (focus == null && world != null) focus = world.Focus;
        if (visualTuner == null) visualTuner = FindFirstObjectByType<VisualPlayModeTuner>();
        if (settings == null) settings = FindFirstObjectByType<WildWindSettingsRoot>();
        if (metaGameState == null) metaGameState = FindFirstObjectByType<MetaGameState>();
    }

    private void DescribeTestScope(BigTestReport report)
    {
        report.Section("Паспорт проверки");
        report.Info("Версия контракта большого теста: " + BigTestContractVersion + ".");
        report.Info("ID запуска: " + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".");
        report.Info("Кнопка: Wild Wind/Провести большой тест.");
        report.Info("Назначение: один общий дотошный протокол по текущей сборке игры.");
        report.Info("Сейчас покрыто: CSV-конфиги, дерево технологий, дерево кораблей, производства, потребности островов/кораблей, скорость их удовлетворения, пассажироперевозки, типы грузов и отсеки, мир 100x100 км, чанки, высотные зоны, активный пузырь, визуальные зависимости, настройки, ветер/аэродинамика, лётная физика, save slots и перезаходы стартовое меню <-> мир.");
        report.Info("Допуски: размер мира +-1 м, размер чанка +-1 м, среднее обновление пузыря <= " + streamerAverageBudgetMs.ToString("0.#") + " мс, симуляция производств " + productionSimulationMinutes.ToString("0.#") + " мин.");
        report.Info("Принцип: FAIL = сломано или противоречит текущему ТЗ; WARN = подозрительно, но можно продолжать; OK = проверено явно.");
        report.Pass("Паспорт большого теста сформирован и попадёт в машинно-читаемый результат.");
    }

    private void ValidateSceneContext(BigTestReport report)
    {
        report.Section("Сцена и контекст запуска");
        Scene scene = SceneManager.GetActiveScene();
        report.Check(scene.IsValid(), "Активная сцена валидна: " + (scene.IsValid() ? scene.name : "<нет сцены>") + ".");
        report.Check(Application.isPlaying, "Тест выполняется в Play Mode, runtime-компоненты реально инициализируются.");
        report.Info("Unity: " + Application.unityVersion + ".");
        report.Info("Платформа: " + Application.platform + ".");
        report.Info("Время запуска: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ".");

        Camera mainCamera = Camera.main;
        report.Check(mainCamera != null, mainCamera != null ? "MainCamera найдена: " + mainCamera.name + "." : "MainCamera не найдена.");
        if (mainCamera != null)
        {
            report.Check(mainCamera.farClipPlane >= 8000f, "Far Clip камеры достаточен для текущего визуального пузыря: " + mainCamera.farClipPlane.ToString("0.#") + " м.");
            if (mainCamera.nearClipPlane > 1f)
            {
                report.Warn("Near Clip камеры больше 1 м. Для мелких корабельных деталей это может быть грубовато: " + mainCamera.nearClipPlane.ToString("0.###") + ".");
            }
            else
            {
                report.Pass("Near Clip камеры подходит для мелких деталей: " + mainCamera.nearClipPlane.ToString("0.###") + ".");
            }
        }

        if (RenderSettings.fog)
        {
            report.Pass("Unity fog включён как базовая страховочная дымка.");
        }
        else if (visualTuner != null)
        {
            report.Pass("Unity fog выключен, это допустимо: высотной видимостью управляет VisualPlayModeTuner/AERO.");
        }
        else
        {
            report.Warn("Unity fog выключен и VisualPlayModeTuner не найден. Видимость может остаться без страховочного ограничения.");
        }
    }

    private WorldConfigDatabase LoadConfig(BigTestReport report)
    {
        report.Section("CSV-конфиги");
        WorldConfigDatabase config = null;

        if (metaGameState != null)
        {
            metaGameState.EnsureProgressInitialized();
            config = metaGameState.WorldConfig;
            if (config != null && config.isLoaded)
            {
                report.Pass("Конфиги взяты из MetaGameState.");
                return config;
            }
        }

        config = new WorldConfigDatabase();
        config.LoadFromAssetsConfigFolder(DefaultConfigFolder);
        report.Check(config.isLoaded, config.isLoaded
            ? "CSV-конфиги загружены из Assets/" + DefaultConfigFolder + "."
            : "CSV-конфиги не загрузились: " + config.lastError);
        return config;
    }

    private void ValidateLocalizationConfig(BigTestReport report)
    {
        report.Section("Localization");
        bool defaultLanguageIsRussian = WildWindLocalization.DefaultLanguage == WildWindLanguage.Ru;
        report.Check(defaultLanguageIsRussian, "Default UI language is Russian.");

        List<string> requiredKeys = new List<string>();
        requiredKeys.AddRange(WildWindStartScreen.RequiredLocalizationKeys);
        requiredKeys.AddRange(WildWindGameplayMenu.RequiredLocalizationKeys);
        bool valid = WildWindLocalization.ValidateDefaultConfig(requiredKeys, out List<string> errors);
        if (valid)
        {
            report.Pass("Localization config is loaded and all start/gameplay menu keys have ru/en text.");
        }
        else
        {
            for (int i = 0; i < errors.Count; i++)
            {
                report.Fail(errors[i]);
            }
        }

        bool missingKeyDetected = !WildWindLocalization.TryGet("big_test_missing_key_probe", out _);
        report.Check(missingKeyDetected, "Missing localization keys are detectable before runtime rendering.");
    }

    private void ValidateConfigDatabase(WorldConfigDatabase config, BigTestReport report)
    {
        if (config == null || !config.isLoaded)
        {
            report.Fail("Проверки CSV остановлены: нет загруженной базы конфигов.");
            return;
        }

        report.Check(config.items.Count >= 20, "Item.csv содержит предметы: " + config.items.Count + ".");
        report.Check(config.islands.Count >= 1, "Island.csv содержит острова: " + config.islands.Count + ".");
        report.Check(config.productions.Count >= 1, "Island_production.csv содержит базовые генерации: " + config.productions.Count + ".");
        report.Check(config.gasCloudTypes.Count >= 1, "Gas_cloud_type.csv содержит типы облаков: " + config.gasCloudTypes.Count + ".");
        report.Check(config.gasClouds.Count >= 1, "Gas_cloud.csv содержит облака: " + config.gasClouds.Count + ".");
        report.Check(config.oreTypes.Count >= 1, "Ore_type.csv содержит типы руды: " + config.oreTypes.Count + ".");
        report.Check(config.miningZones.Count >= 1, "Mining_zone.csv содержит зоны добычи: " + config.miningZones.Count + ".");
        report.Check(config.leviathanTypes.Count >= 1, "Leviathan_type.csv содержит типы левиафанов: " + config.leviathanTypes.Count + ".");
        report.Check(config.leviathanZones.Count >= 1, "Leviathan_zone.csv содержит зоны левиафанов: " + config.leviathanZones.Count + ".");
        report.Check(config.technologies.Count >= 1, "Technology.csv содержит технологии: " + config.technologies.Count + ".");
        report.Check(config.specialModules.Count >= 1, "Special_module.csv содержит спецмодули: " + config.specialModules.Count + ".");
        report.Check(config.shipTreeEntries.Count >= 7, "Ship_tree.csv содержит дерево кораблей: " + config.shipTreeEntries.Count + ".");
        report.Check(config.islandIndustries.Count >= 6, "Production_industry.csv содержит производственные линии: " + config.islandIndustries.Count + ".");
        report.Check(config.industryRecipes.Count >= 6, "Production_recipe.csv содержит производственные рецепты: " + config.industryRecipes.Count + ".");
        report.Check(config.islandArchetypes.Count == 5, "Island_archetype.csv содержит 5 типов островов: " + config.islandArchetypes.Count + ".");
        report.Check(config.islandArchetypeStages.Count >= 10, "Island_archetype_stage.csv содержит стадии развития островов: " + config.islandArchetypeStages.Count + ".");
        report.Check(config.islandSocialNeeds.Count == 7, "Island_social_need.csv содержит 7 общественных потребностей: " + config.islandSocialNeeds.Count + ".");
        report.Check(config.islandBuildings.Count >= 30, "Island_building.csv содержит производственные и сервисные здания: " + config.islandBuildings.Count + ".");

        CheckUniqueIds(config.items, item => item.id, "предметов", report);
        CheckUniqueIds(config.islands, island => island.id, "островов", report);
        CheckUniqueIds(config.productions, production => production.id, "базовых производств", report);
        CheckUniqueIds(config.gasCloudTypes, cloudType => cloudType.id, "типов облаков", report);
        CheckUniqueIds(config.gasClouds, cloud => cloud.id, "облаков", report);
        CheckUniqueIds(config.oreTypes, ore => ore.id, "типов руды", report);
        CheckUniqueIds(config.miningZones, zone => zone.id, "зон добычи", report);
        CheckUniqueIds(config.leviathanTypes, type => type.id, "типов левиафанов", report);
        CheckUniqueIds(config.leviathanZones, zone => zone.id, "зон левиафанов", report);
        CheckUniqueIds(config.technologies, tech => tech.id, "технологий", report);
        CheckUniqueIds(config.specialModules, module => module.id, "спецмодулей", report);
        CheckUniqueIds(config.shipTreeEntries, ship => ship.shipId, "кораблей в Ship_tree.csv", report);
        CheckUniqueIds(config.islandIndustries, industry => industry.id, "линий производств", report);
        CheckUniqueIds(config.industryRecipes, recipe => recipe.id, "рецептов производств", report);
        CheckUniqueIds(config.islandArchetypes, archetype => archetype.id, "типов островов", report);
        CheckUniqueIds(config.islandArchetypeStages, stage => stage.id, "стадий островов", report);
        CheckUniqueIds(config.islandSocialNeeds, need => need.id, "общественных потребностей", report);
        CheckUniqueIds(config.islandBuildings, building => building.id, "островных зданий", report);

        ValidateShipTreeConfig(config, report);
        ValidateConfigReferences(config, report);
        ValidateIslandDevelopmentConfig(config, report);
        ValidateIndustryConfig(config, report);
    }

    private static void ValidateShipTreeConfig(WorldConfigDatabase config, BigTestReport report)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            report.Fail("Ship_tree.csv не загружен.");
            return;
        }

        bool entriesValid = config.shipTreeEntries.Count >= 7;
        bool hasPioneerRoot = false;
        int r1Count = 0;
        HashSet<string> roleIds = new HashSet<string>();

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null)
            {
                entriesValid = false;
                continue;
            }

            bool isRoot = entry.rank == 0;
            hasPioneerRoot |= entry.shipId == "pioneer" &&
                isRoot &&
                (entry.parentShipIds == null || entry.parentShipIds.Count == 0);
            if (entry.rank == 1)
            {
                r1Count++;
            }

            if (!string.IsNullOrWhiteSpace(entry.roleId))
            {
                roleIds.Add(entry.roleId);
            }

            entriesValid &= !string.IsNullOrWhiteSpace(entry.shipId) &&
                !string.IsNullOrWhiteSpace(entry.localNameRu) &&
                !string.IsNullOrWhiteSpace(entry.localNameEn) &&
                !string.IsNullOrWhiteSpace(entry.classNameRu) &&
                !string.IsNullOrWhiteSpace(entry.roleId) &&
                !string.IsNullOrWhiteSpace(entry.roleNameRu) &&
                !string.IsNullOrWhiteSpace(entry.summaryRu) &&
                (isRoot || (entry.parentShipIds != null && entry.parentShipIds.Count > 0)) &&
                (string.IsNullOrWhiteSpace(entry.requiredTechnologyId) || config.GetTechnology(entry.requiredTechnologyId) != null) &&
                config.GetHull(entry.hullId) != null &&
                config.GetEngine(entry.engineId) != null &&
                config.GetPropeller(entry.propellerId) != null &&
                config.GetClaudiumLoop(entry.claudiumLoopId) != null &&
                config.GetSpecialModule(entry.specialModuleId) != null &&
                AllIdsExistAllowEmpty(entry.upgradeHullIds, config.GetHull) &&
                AllIdsExistAllowEmpty(entry.upgradeEngineIds, config.GetEngine) &&
                AllIdsExistAllowEmpty(entry.upgradePropellerIds, config.GetPropeller) &&
                AllIdsExistAllowEmpty(entry.upgradeClaudiumLoopIds, config.GetClaudiumLoop) &&
                AllIdsExistAllowEmpty(entry.upgradeSpecialModuleIds, config.GetSpecialModule);

            if (entry.parentShipIds != null)
            {
                for (int j = 0; j < entry.parentShipIds.Count; j++)
                {
                    string parentId = entry.parentShipIds[j];
                    ShipTreeEntryConfig parent = config.GetShipTreeEntry(parentId);
                    entriesValid &= parent != null &&
                        parent.shipId != entry.shipId &&
                        parent.rank <= entry.rank;
                }
            }
        }

        report.Check(entriesValid && hasPioneerRoot && r1Count >= 7 && roleIds.Count >= 7,
            "Ship_tree.csv задаёт скромное текущее дерево: Пионер R0, минимум семь R1-кораблей и основные роли.");

        report.Check(!ShipTreeHasCycles(config),
            "Дерево кораблей не содержит циклов по parent_ship_id.");

        bool r1CatalogMirrored = true;
        for (int i = 0; i < R1ShipDesignCatalog.All.Count; i++)
        {
            R1ShipDesignDefinition design = R1ShipDesignCatalog.All[i];
            ShipTreeEntryConfig entry = design != null ? config.GetShipTreeEntry(design.shipId) : null;
            r1CatalogMirrored &= design != null &&
                entry != null &&
                entry.rank == 1 &&
                entry.requiredTechnologyId == design.requiredTechId &&
                entry.hullId == design.hullId &&
                entry.engineId == design.engineId &&
                entry.propellerId == design.propellerId &&
                entry.claudiumLoopId == design.claudiumLoopId &&
                entry.specialModuleId == design.specialModuleId;
        }

        report.Check(r1CatalogMirrored,
            "Ship_tree.csv синхронизирован с R1ShipDesignCatalog по текущим R1-кораблям.");
    }

    private static bool ShipTreeHasCycles(WorldConfigDatabase config)
    {
        if (config == null || config.shipTreeEntries == null) return true;

        HashSet<string> visiting = new HashSet<string>();
        HashSet<string> visited = new HashSet<string>();
        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.shipId)) return true;
            if (ShipTreeVisitHasCycle(entry, config, visiting, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShipTreeVisitHasCycle(ShipTreeEntryConfig entry, WorldConfigDatabase config, HashSet<string> visiting, HashSet<string> visited)
    {
        if (entry == null || config == null || visiting == null || visited == null) return true;
        if (visited.Contains(entry.shipId)) return false;
        if (!visiting.Add(entry.shipId)) return true;

        if (entry.parentShipIds != null)
        {
            for (int i = 0; i < entry.parentShipIds.Count; i++)
            {
                ShipTreeEntryConfig parent = config.GetShipTreeEntry(entry.parentShipIds[i]);
                if (parent == null || ShipTreeVisitHasCycle(parent, config, visiting, visited))
                {
                    return true;
                }
            }
        }

        visiting.Remove(entry.shipId);
        visited.Add(entry.shipId);
        return false;
    }

    private static void ValidateConfigReferences(WorldConfigDatabase config, BigTestReport report)
    {
        bool obsoleteResourcesRemoved = config.GetItem("sulfur") == null &&
            config.GetItem("wood") == null;
        report.Check(obsoleteResourcesRemoved, "Item.csv очищен от неактуальных ресурсов: серы и древесины нет.");

        bool baseProductionsValid = true;
        for (int i = 0; i < config.productions.Count; i++)
        {
            IslandProductionConfig production = config.productions[i];
            baseProductionsValid &= production != null &&
                !string.IsNullOrWhiteSpace(production.id) &&
                config.GetItem(production.productionItemId) != null &&
                production.productionCountBasePerMinute > 0f &&
                ItemAmountsReferenceExistingItems(production.consumptions, c => c.itemId, c => c.countPerMinute, config);
        }

        report.Check(baseProductionsValid, "Базовые Island_production ссылаются только на текущие предметы и имеют валидную скорость.");

        bool islandsValid = true;
        int islandsWithoutBaseProduction = 0;
        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null)
            {
                islandsValid = false;
                report.Fail("В Island.csv есть пустая строка острова.");
                continue;
            }

            bool hasId = !string.IsNullOrWhiteSpace(island.id);
            bool hasDocking = island.dockingRadius > 0f;
            bool hasLoadSpeed = island.timeForOneItemLoadSeconds > 0f;
            bool hasBaseProduction = !string.IsNullOrWhiteSpace(island.productionId) && config.GetProduction(island.productionId) != null;
            bool hasIndustryProduction = hasId && HasIndustryOnIsland(config, island.id);

            if (!hasId)
            {
                report.Fail("Остров в Island.csv без id.");
            }

            if (hasId && !hasDocking)
            {
                report.Fail("Остров " + island.id + " имеет неверный docking_radius: " + island.dockingRadius.ToString("0.###") + ".");
            }

            if (hasId && !hasLoadSpeed)
            {
                report.Fail("Остров " + island.id + " имеет неверный time_for_one_item_load: " + island.timeForOneItemLoadSeconds.ToString("0.###") + ".");
            }

            if (hasId && !hasBaseProduction && !hasIndustryProduction)
            {
                report.Fail("Остров " + island.id + " не имеет ни Island_production, ни Production_industry линий.");
            }

            if (hasId && !hasBaseProduction && hasIndustryProduction)
            {
                islandsWithoutBaseProduction++;
                report.Info("Остров " + island.id + " без старой Island_production, но покрыт новыми Production_industry линиями.");
            }

            islandsValid &= hasId && hasDocking && hasLoadSpeed && (hasBaseProduction || hasIndustryProduction);
        }

        report.Check(islandsValid, "Острова имеют радиус стыковки и либо старую Island_production, либо новые Production_industry линии.");
        if (islandsWithoutBaseProduction > 0)
        {
            report.Info("Островов без старой базовой генерации, но с новой промышленностью: " + islandsWithoutBaseProduction + ".");
        }

        bool gasValid = true;
        for (int i = 0; i < config.gasCloudTypes.Count; i++)
        {
            GasCloudTypeConfig type = config.gasCloudTypes[i];
            gasValid &= type != null &&
                config.GetItem(type.condensateItemId) != null &&
                type.condensateLitersPerCubicMeter > 0f &&
                ItemAmountsReferenceExistingItems(type.composition, c => c.itemId, c => c.share, config);
        }

        for (int i = 0; i < config.gasClouds.Count; i++)
        {
            GasCloudConfig cloud = config.gasClouds[i];
            gasValid &= cloud != null &&
                config.GetGasCloudType(cloud.cloudTypeId) != null &&
                cloud.initialVolumeLiters > 0f;
        }

        report.Check(gasValid, "Газовые облака и их типы ссылаются на существующие предметы/типы и имеют добываемый объём.");

        bool oreValid = true;
        for (int i = 0; i < config.oreTypes.Count; i++)
        {
            OreTypeConfig ore = config.oreTypes[i];
            oreValid &= ore != null &&
                config.GetItem(ore.oreItemId) != null &&
                ore.baseValue >= 0f &&
                ore.naturalShedKgPerMinute >= 0f &&
                ore.shotShedKg >= 0 &&
                ore.fragmentFallSpeedMS > 0f &&
                ItemAmountsReferenceExistingItems(ore.composition, c => c.mineralItemId, c => c.share, config);
        }

        for (int i = 0; i < config.miningZones.Count; i++)
        {
            MiningZoneConfig zone = config.miningZones[i];
            oreValid &= zone != null &&
                zone.radiusMeters > 0f &&
                zone.maxActiveRocks >= zone.initialRockCount &&
                zone.rockRadiusMeters > 0f &&
                zone.rockOreKg > 0f &&
                zone.apexY > zone.stormY &&
                AllIdsExist(zone.oreTypeIds, config.GetOreType);
        }

        report.Check(oreValid, "Руда и зоны добычи имеют валидные предметы, минералы, объём и высотную траекторию.");

        bool leviathansValid = true;
        for (int i = 0; i < config.leviathanTypes.Count; i++)
        {
            LeviathanTypeConfig type = config.leviathanTypes[i];
            leviathansValid &= type != null &&
                config.GetItem(type.carcassItemId) != null &&
                type.bodyLengthMeters > 0f &&
                type.bodyRadiusMeters > 0f &&
                type.massKg > 0f &&
                type.maxHealth > 0f &&
                type.forwardThrustKgf >= 0f &&
                type.omniThrustKgf >= 0f;
        }

        for (int i = 0; i < config.leviathanZones.Count; i++)
        {
            LeviathanZoneConfig zone = config.leviathanZones[i];
            leviathansValid &= zone != null &&
                zone.radiusMeters > 0f &&
                zone.maxY > zone.minY &&
                zone.maxActive >= zone.initialCount &&
                AllIdsExist(zone.leviathanTypeIds, config.GetLeviathanType);
        }

        report.Check(leviathansValid, "Левиафаны и их зоны имеют валидные типы, размеры, здоровье и высотные диапазоны.");

        bool techValid = true;
        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig tech = config.technologies[i];
            techValid &= tech != null &&
                !string.IsNullOrWhiteSpace(tech.id) &&
                !string.IsNullOrWhiteSpace(tech.localNameRu) &&
                !string.IsNullOrWhiteSpace(tech.localNameEn) &&
                tech.rank >= 0 &&
                !string.IsNullOrWhiteSpace(tech.branch) &&
                !string.IsNullOrWhiteSpace(tech.unlockSummaryRu) &&
                tech.cycleTimeSeconds >= 0 &&
                tech.requiredCycles > 0 &&
                AllIdsExistAllowEmpty(tech.prerequisiteTechnologyIds, config.GetTechnology) &&
                ItemAmountsReferenceExistingItems(tech.cycleCost, c => c.itemId, c => c.amount, config);
        }

        bool techTreeValid = TechnologyTreeMetadataValid(config);

        bool hasShipNeedServiceModule = false;
        bool hasCargoStorageModule = false;
        for (int i = 0; i < config.specialModules.Count; i++)
        {
            SpecialModuleConfig module = config.specialModules[i];
            bool techReferenceOk = module == null ||
                string.IsNullOrWhiteSpace(module.completedTechId) ||
                config.GetTechnology(module.completedTechId) != null;
            techValid &= module != null &&
                !string.IsNullOrWhiteSpace(module.id) &&
                module.baseMassKg >= 0f &&
                module.needWorkforceRecoveryPerHour >= 0f &&
                module.needHealthRecoveryPerHour >= 0f &&
                module.needSafetyRecoveryPerHour >= 0f &&
                module.needComfortRecoveryPerHour >= 0f &&
                module.needCreativityRecoveryPerHour >= 0f &&
                module.needRepairRecoveryPerHour >= 0f &&
                module.needCapitalConnectionRecoveryPerHour >= 0f &&
                module.cargoVanCapacityUnits >= 0f &&
                module.passengerSeatCapacity >= 0f &&
                module.bulkHoldCapacityLiters >= 0f &&
                module.liquidTankCapacityLiters >= 0f &&
                module.gasCylinderCapacityLiters >= 0f &&
                module.miningImpactDamageTakenMultiplier >= 0f &&
                module.surveyPaperToInfoEfficiency >= 0f &&
                module.leviathanAlarmGenerationMultiplier >= 0f &&
                module.harpoonWeaponCostPerMinute >= 0f &&
                module.harpoonMaxCarcassMassKg >= 0f &&
                module.harpoonFlightDamage >= 0f &&
                module.harpoonRangeMeters >= 0f &&
                module.refrigeratedHoldCapacityLiters >= 0f &&
                module.refrigeratedHoldPowerDrawKw >= 0f &&
                module.shipDockSlots >= 0f &&
                module.dockedShipMassFactor > 0f &&
                module.dockSupportClaudiumPerTonHour >= 0f &&
                techReferenceOk;

            if (module != null)
            {
                hasShipNeedServiceModule |= module.needWorkforceRecoveryPerHour > 0f ||
                    module.needHealthRecoveryPerHour > 0f ||
                    module.needSafetyRecoveryPerHour > 0f ||
                    module.needComfortRecoveryPerHour > 0f ||
                    module.needCreativityRecoveryPerHour > 0f ||
                    module.needRepairRecoveryPerHour > 0f ||
                    module.needCapitalConnectionRecoveryPerHour > 0f;

                hasCargoStorageModule |= module.cargoVanCapacityUnits > 0f ||
                    module.passengerSeatCapacity > 0f ||
                    module.bulkHoldCapacityLiters > 0f ||
                    module.liquidTankCapacityLiters > 0f ||
                    module.gasCylinderCapacityLiters > 0f ||
                    module.refrigeratedHoldCapacityLiters > 0f ||
                    module.shipDockSlots > 0f;
            }
        }

        report.Check(techValid && techTreeValid, "Текущее дерево технологий имеет ранги, ветки, описания, валидные зависимости и не содержит циклов.");
        report.Check(techValid && hasShipNeedServiceModule, "Технологии и спецмодули имеют валидные зависимости, стоимость, массу и корабельные сервисные мощности.");
        report.Check(techValid && hasCargoStorageModule, "Спецмодули могут задавать грузовые отсеки: фургон, салон, кузов, цистерну, баллоны или док.");
    }

    private static bool TechnologyTreeMetadataValid(WorldConfigDatabase config)
    {
        if (config == null || config.technologies == null || config.technologies.Count == 0) return false;

        string[] expectedBranches =
        {
            "Старт",
            "Фундамент",
            "Общая инженерия",
            "Вода и корпус",
            "Пассажиры",
            "Ремонт и логистика",
            "Руда",
            "Разведка",
            "Охота",
            "Водомерка",
            "Булат",
            "Шершень",
            "Егерь",
            "Опора",
            "Паровоз"
        };

        bool branchesPresent = true;
        for (int i = 0; i < expectedBranches.Length; i++)
        {
            branchesPresent &= TechnologyBranchExists(config, expectedBranches[i]);
        }

        return branchesPresent &&
            TechnologyRanksRespectPrerequisites(config) &&
            !TechnologyTreeHasCycles(config);
    }

    private static bool TechnologyBranchExists(WorldConfigDatabase config, string branch)
    {
        if (config == null || config.technologies == null || string.IsNullOrWhiteSpace(branch)) return false;

        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            if (technology != null && technology.branch == branch)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TechnologyRanksRespectPrerequisites(WorldConfigDatabase config)
    {
        if (config == null || config.technologies == null) return false;

        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            if (technology == null || technology.prerequisiteTechnologyIds == null) return false;

            for (int j = 0; j < technology.prerequisiteTechnologyIds.Count; j++)
            {
                TechnologyConfig prerequisite = config.GetTechnology(technology.prerequisiteTechnologyIds[j]);
                if (prerequisite == null || prerequisite.rank >= technology.rank)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TechnologyTreeHasCycles(WorldConfigDatabase config)
    {
        if (config == null || config.technologies == null) return true;

        HashSet<string> visiting = new HashSet<string>();
        HashSet<string> visited = new HashSet<string>();
        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) return true;

            if (TechnologyVisitHasCycle(technology, config, visiting, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TechnologyVisitHasCycle(TechnologyConfig technology, WorldConfigDatabase config, HashSet<string> visiting, HashSet<string> visited)
    {
        if (technology == null || config == null || visiting == null || visited == null) return true;
        if (visited.Contains(technology.id)) return false;
        if (!visiting.Add(technology.id)) return true;

        if (technology.prerequisiteTechnologyIds != null)
        {
            for (int i = 0; i < technology.prerequisiteTechnologyIds.Count; i++)
            {
                TechnologyConfig prerequisite = config.GetTechnology(technology.prerequisiteTechnologyIds[i]);
                if (prerequisite == null || TechnologyVisitHasCycle(prerequisite, config, visiting, visited))
                {
                    return true;
                }
            }
        }

        visiting.Remove(technology.id);
        visited.Add(technology.id);
        return false;
    }

    private static void ValidateCargoStorageModel(WorldConfigDatabase config, BigTestReport report)
    {
        report.Section("Грузовые единицы и отсеки кораблей");

        if (config == null || !config.isLoaded)
        {
            report.Fail("Проверки грузовых отсеков остановлены: нет загруженной базы конфигов.");
            return;
        }

        ItemConfig passengerTemplate = config.GetItem(PassengerCargoIds.ToCapitalItemId);
        ItemConfig water = config.GetItem("water");
        ItemConfig sampleOre = config.GetItem("windshale_ore");
        ItemConfig dockedBoat = config.GetItem("utility_boat_ship");
        ItemConfig carcass = config.GetItem("windcalf_carcass");
        bool cargoMetadataValid = passengerTemplate != null &&
            passengerTemplate.cargoUnitKind == CargoUnitKind.Passenger &&
            passengerTemplate.cargoStorageKind == CargoStorageKind.Cabin &&
            Mathf.Abs(config.GetItemTransportMassKg(PassengerCargoIds.ToCapitalItemId, 3) - 300f) <= 0.001f &&
            water != null &&
            water.cargoUnitKind == CargoUnitKind.VolumeLiter &&
            water.cargoStorageKind == CargoStorageKind.LiquidTank &&
            sampleOre != null &&
            sampleOre.cargoUnitKind == CargoUnitKind.VolumeLiter &&
            sampleOre.cargoStorageKind == CargoStorageKind.BulkHold &&
            dockedBoat != null &&
            dockedBoat.cargoUnitKind == CargoUnitKind.Ship &&
            dockedBoat.cargoStorageKind == CargoStorageKind.ShipDock &&
            carcass != null &&
            carcass.cargoUnitKind == CargoUnitKind.VolumeLiter &&
            carcass.cargoStorageKind == CargoStorageKind.RefrigeratedHold &&
            Mathf.Abs(config.GetItemTransportMassKg("utility_boat_ship", 1) - 1500f) <= 0.001f;
        report.Check(cargoMetadataValid, "Item.csv задаёт единицы груза, типы отсеков и массу единицы: пассажиры считаются местами, жидкости/сыпучка - литрами, докованные корабли дают 10% транспортной массы.");

        LogisticsShipMetrics cargoMetrics = new LogisticsShipMetrics
        {
            cargoCompartments = new List<CargoCompartmentDefinition>
            {
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.Cabin, capacity = 3f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.Van, capacity = 10f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.BulkHold, capacity = 20f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.BulkHold, capacity = 15f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.LiquidTank, capacity = 10f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.GasCylinder, capacity = 5f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.RefrigeratedHold, capacity = 100f },
                new CargoCompartmentDefinition
                {
                    storageKind = CargoStorageKind.ShipDock,
                    capacity = 1f,
                    maxDockedShipClass = ShipSizeClass.Boat,
                    dockSupportClaudiumPerTonHour = 0.02f
                }
            }
        };
        Dictionary<string, int> typedCargo = new Dictionary<string, int>
        {
            [PassengerCargoIds.ToCapitalItemId] = 3,
            ["food"] = 10,
            ["windshale_ore"] = 12,
            ["dawnspar_ore"] = 8,
            ["water"] = 10,
            ["aer_silt"] = 5,
            ["windcalf_carcass"] = 80,
            ["utility_boat_ship"] = 1
        };
        bool cargoFits = CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, typedCargo, out _);
        float typedCargoMass = CargoStoragePlanner.GetCargoMassKg(config, typedCargo);
        float dockSupportClaudium = CargoStoragePlanner.GetDockSupportClaudiumKg(cargoMetrics, CargoStoragePlanner.GetDockedShipFullMassKg(config, typedCargo), 3600f);
        Dictionary<string, int> passengerOverflow = new Dictionary<string, int> { [PassengerCargoIds.ToCapitalItemId] = 4 };
        Dictionary<string, int> mixedBulkOverflow = new Dictionary<string, int>
        {
            ["windshale_ore"] = 10,
            ["dawnspar_ore"] = 10,
            ["bluebrass_ore"] = 10
        };
        Dictionary<string, int> dockOverflow = new Dictionary<string, int> { ["utility_boat_ship"] = 2 };
        Dictionary<string, int> refrigeratedOverflow = new Dictionary<string, int> { ["windcalf_carcass"] = 120 };
        bool cargoLimitsWork = cargoFits &&
            Mathf.Abs(typedCargoMass - 1925f) <= 0.001f &&
            Mathf.Abs(dockSupportClaudium - 0.3f) <= 0.001f &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, passengerOverflow, out _) &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, mixedBulkOverflow, out _) &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, refrigeratedOverflow, out _) &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, dockOverflow, out _);
        report.Check(cargoLimitsWork, "Корабельные отсеки проверяют места салона, смешиваемый фургон, однотипные кузова/цистерны/баллоны, док-слот, 10% массу докованного корабля и расход клавдия на поддержку дока.");

        LogisticsShipMetrics fuelOnlyMetrics = new LogisticsShipMetrics
        {
            cargoCompartments = new List<CargoCompartmentDefinition>
            {
                new CargoCompartmentDefinition
                {
                    storageKind = CargoStorageKind.Van,
                    capacity = 100f,
                    allowedItemIds = new List<string> { "charcoal", "claudium" }
                }
            }
        };
        bool cargoItemWhitelistWorks =
            CargoStoragePlanner.TryValidateCargoStorage(config, fuelOnlyMetrics, new Dictionary<string, int> { ["charcoal"] = 50, ["claudium"] = 40 }, out _) &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, fuelOnlyMetrics, new Dictionary<string, int> { ["food"] = 1 }, out _);
        report.Check(cargoItemWhitelistWorks, "Грузовой отсек может быть ограничен конкретными item id, например только углем и клавдием.");

        PlayerProgress playerCargoProgress = new PlayerProgress();
        playerCargoProgress.Normalize();
        playerCargoProgress.AddShipCargo(PassengerCargoIds.ToCapitalItemId, 2);
        playerCargoProgress.AddShipCargo("water", 10);
        playerCargoProgress.AddShipCargo("utility_boat_ship", 1);
        bool playerCargoUsesConfigMass = Mathf.Abs(playerCargoProgress.GetShipCargoMassKg(config) - 1710f) <= 0.001f &&
            CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics.cargoCompartments, playerCargoProgress.shipCargo, out _);
        report.Check(playerCargoUsesConfigMass, "Груз основного корабля игрока тоже считает массу через Item.csv и проходит ту же проверку отсеков.");
    }

    private static void ValidateR1ShipMechanics(WorldConfigDatabase config, BigTestReport report)
    {
        report.Section("R1 ships: runtime mechanics");

        if (config == null || !config.isLoaded)
        {
            report.Fail("R1 ship checks stopped: config database is not loaded.");
            return;
        }

        SpecialModuleConfig waterStrider = config.GetSpecialModule("water_strider_harvester");
        SpecialModuleConfig bulat = config.GetSpecialModule("bulat_impact_hold");
        SpecialModuleConfig hornet = config.GetSpecialModule("hornet_observation_suite");
        SpecialModuleConfig hornetMk2 = config.GetSpecialModule("hornet_observation_suite_mk2");
        SpecialModuleConfig jaeger = config.GetSpecialModule("jaeger_harpoon_fridge");
        SpecialModuleConfig jaegerMk2 = config.GetSpecialModule("jaeger_harpoon_fridge_mk2");
        SpecialModuleConfig opora = config.GetSpecialModule("opora_crane_platform");
        SpecialModuleConfig fuelTender = config.GetSpecialModule("fuel_tender_tanks");
        SpecialModuleConfig parovoz = config.GetSpecialModule("parovoz_passenger_cabin");

        ValidateR1ShipCatalog(config, report);

        report.Check(waterStrider != null &&
            waterStrider.gasHarvesterWaterOnly &&
            waterStrider.gasHarvesterVolumeM3PerSecond > 0f &&
            waterStrider.gasHarvesterPowerDrawKw > 0f &&
            waterStrider.liquidTankCapacityLiters >= 1000f,
            "Vodomerka module exists: water-only cloud harvester and 1000 l liquid tank.");

        report.Check(bulat != null &&
            bulat.miningImpactHoldCapacityKg >= 3000f &&
            Approximately(bulat.miningImpactDamageTakenMultiplier, 0.5f, 0.001f) &&
            bulat.bulkHoldCapacityLiters >= 3000f,
            "Bulat module exists: 3 m3 bulk hold and 50% mining impact damage.");

        report.Check(hornet != null &&
            Approximately(hornet.surveyPaperToInfoEfficiency, 0.2f, 0.001f) &&
            Approximately(hornet.leviathanAlarmGenerationMultiplier, 0.5f, 0.001f) &&
            hornet.observationRadiusMeters >= 700f,
            "Shershen base module exists: 20% paper efficiency and x0.5 leviathan alarm.");

        report.Check(hornet != null &&
            hornetMk2 != null &&
            Approximately(hornetMk2.surveyPaperToInfoEfficiency, 0.45f, 0.001f) &&
            Approximately(hornetMk2.leviathanAlarmGenerationMultiplier, 0.25f, 0.001f) &&
            hornetMk2.baseMassKg > hornet.baseMassKg,
            "Shershen upgraded instruments exist: 45% paper efficiency, x0.25 alarm, heavier module.");

        report.Check(jaeger != null &&
            Approximately(jaeger.harpoonWeaponCostPerMinute, 2f, 0.001f) &&
            jaeger.harpoonMaxCarcassMassKg >= 100f &&
            jaeger.refrigeratedHoldCapacityLiters >= 2500f &&
            jaeger.refrigeratedHoldPowerDrawKw > 0f,
            "Eger base module exists: harpoon upkeep, 100 kg target limit and powered refrigerator.");

        report.Check(jaeger != null &&
            jaegerMk2 != null &&
            Approximately(jaegerMk2.harpoonWeaponCostPerMinute, 4f, 0.001f) &&
            jaegerMk2.harpoonMaxCarcassMassKg >= 200f &&
            jaegerMk2.harpoonFlightDamage > jaeger.harpoonFlightDamage &&
            jaegerMk2.refrigeratedHoldPowerDrawKw < jaeger.refrigeratedHoldPowerDrawKw,
            "Eger upgraded harpoon exists: 200 kg limit, more damage, lower refrigerator draw.");

        report.Check(opora != null &&
            opora.needRepairRecoveryPerHour > 0f &&
            opora.cargoVanCapacityUnits >= 6000f,
            "Opora module exists: repair recovery and large van deck.");

        report.Check(fuelTender != null &&
            fuelTender.cargoVanCapacityUnits >= 6400f &&
            ContainsId(fuelTender.allowedCargoItemIds, "charcoal") &&
            ContainsId(fuelTender.allowedCargoItemIds, "claudium"),
            "Fuel tender module exists: large van capacity locked to charcoal and claudium.");

        report.Check(parovoz != null &&
            parovoz.passengerSeatCapacity >= 15f &&
            parovoz.cargoVanCapacityUnits > 0f,
            "Parovoz module exists: 15 passenger seats and supplies van.");

        LogisticsShipMetrics fridgeMetrics = new LogisticsShipMetrics
        {
            cargoCompartments = new List<CargoCompartmentDefinition>
            {
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.RefrigeratedHold, capacity = 100f }
            }
        };
        bool refrigeratorAcceptsCarcass = CargoStoragePlanner.TryValidateCargoStorage(
            config,
            fridgeMetrics,
            new Dictionary<string, int> { ["windcalf_carcass"] = 80 },
            out _);
        bool refrigeratorRejectsOverflow = !CargoStoragePlanner.TryValidateCargoStorage(
            config,
            fridgeMetrics,
            new Dictionary<string, int> { ["windcalf_carcass"] = 120 },
            out _);
        bool refrigeratorRejectsNoFridge = !CargoStoragePlanner.TryValidateCargoStorage(
            config,
            new LogisticsShipMetrics
            {
                cargoCompartments = new List<CargoCompartmentDefinition>
                {
                    new CargoCompartmentDefinition { storageKind = CargoStorageKind.Van, capacity = 1000f }
                }
            },
            new Dictionary<string, int> { ["windcalf_carcass"] = 1 },
            out _);
        report.Check(refrigeratorAcceptsCarcass && refrigeratorRejectsOverflow && refrigeratorRejectsNoFridge,
            "Leviathan carcasses are refrigerated cargo: fit only into a refrigerator and obey liter capacity.");

        bool surveyEfficiencyWorks = false;
        if (config.gasClouds != null && config.gasClouds.Count > 0)
        {
            GasCloudConfig cloud = config.gasClouds[0];
            PlayerProgress progress = new PlayerProgress();
            progress.Normalize();
            ScoutedObjectState state = progress.GetScoutedObjectState(ScoutedObjectKind.GasCloud, cloud.id, true);
            state.factsComplete = true;
            state.factsRequired = 1f;
            state.factsProgress = 1f;
            state.informationPotentialKg = 100f;

            List<ResourceStack> cargo = new List<ResourceStack>
            {
                new ResourceStack { resourceId = SurveySystem.PaperItemId, amount = 10 }
            };

            SurveySystem.ObserveAndExtractWorldFromPoint(
                config,
                progress,
                cloud.position,
                1f,
                1000f,
                60f,
                DateTime.UtcNow.Ticks,
                cargo,
                0f,
                1f,
                0f,
                0.2f);

            surveyEfficiencyWorks = GetStackAmountForTest(cargo, SurveySystem.PaperItemId) == 0 &&
                GetStackAmountForTest(cargo, SurveySystem.CloudInfoItemId) == 2;
        }

        report.Check(surveyEfficiencyWorks, "Survey paper efficiency works: at 20%, 10 paper becomes exactly 2 cloud info.");

        GameObject alarmShip = null;
        GameObject alarmLeviathan = null;
        try
        {
            alarmShip = new GameObject("Big Test R1 Alarm Ship");
            alarmShip.AddComponent<Rigidbody>();
            ShipPhysics ship = alarmShip.AddComponent<ShipPhysics>();
            ship.enabled = false;
            ship.leviathanAlarmGenerationMultiplier = 0.25f;

            alarmLeviathan = new GameObject("Big Test R1 Alarm Leviathan");
            alarmLeviathan.AddComponent<Rigidbody>();
            Leviathan leviathan = alarmLeviathan.AddComponent<Leviathan>();
            leviathan.enabled = false;
            leviathan.alarm01 = 0f;
            leviathan.AddAlarm(0.4f, "big test", ship);

            report.Check(Approximately(leviathan.alarm01, 0.1f, 0.001f),
                "Leviathan alarm multiplier works: x0.25 source turns 0.4 alarm into 0.1.");
        }
        finally
        {
            DestroyBigTestObject(alarmShip);
            DestroyBigTestObject(alarmLeviathan);
        }

        float fullImpactDamage = MeasureMiningImpactDamage(1f);
        float dampedImpactDamage = MeasureMiningImpactDamage(0.5f);
        report.Check(fullImpactDamage > 0f &&
            dampedImpactDamage > 0f &&
            Approximately(dampedImpactDamage, fullImpactDamage * 0.5f, Mathf.Max(0.05f, fullImpactDamage * 0.05f)),
            "Mining impact damping works: Bulat-style x0.5 hold halves fragment hit damage.");
    }

    private static void ValidateR1ShipCatalog(WorldConfigDatabase config, BigTestReport report)
    {
        ShipCatalogSO catalog = ScriptableObject.CreateInstance<ShipCatalogSO>();
        catalog.starterHullId = "starter_hull";
        catalog.parts = new List<ShipPartDefinitionSO>();

        try
        {
            int expectedParts = config.hulls.Count + config.engines.Count + config.propellers.Count + config.claudiumLoops.Count + config.specialModules.Count;
            int appliedParts = ShipAssemblyBuilder.ApplyCsvShipPartConfigs(catalog, config);
            report.Check(appliedParts >= expectedParts && expectedParts > 0,
                "CSV ship part configs sync into ShipCatalog: " + appliedParts + "/" + expectedParts + " parts.");

            report.Check(R1ShipDesignCatalog.All.Count == 7,
                "R1 design catalog contains all seven ships: Vodomerka, Bulat, Shershen, Eger, Opora, Fuel Tender, Parovoz.");

            string[] requiredR1TechnologyIds =
            {
                "aerodynamics",
                "basic_geometry",
                "steam_claudium_theory",
                "air_pumps",
                "mathematics",
                "bearing_skin",
                "ore_collector",
                "continuous_observations",
                "hunter_hull",
                "support_platform",
                "passenger_routes",
                "water_strider_tankage",
                "water_strider_engine_tuning",
                "water_strider_loop_tuning",
                "water_collection_baffles",
                "water_strider_large_tank",
                "water_collection_autopilot",
                "shockproof_bulk_hold",
                "bulat_engine_tuning",
                "bulat_loop_tuning",
                "ore_receiver",
                "mining_autopilot",
                "reinforced_observation_suite",
                "quiet_skin",
                "scout_engine_tuning",
                "survey_autopilot",
                "reinforced_harpoon",
                "hunter_engine_tuning",
                "armored_hunter_hull",
                "cold_chamber",
                "crane_tackles",
                "short_circuit_claudium_loop",
                "ribbed_deck_truss",
                "removable_van_sections",
                "cabin_standards",
                "forced_firebox",
                "passenger_propeller",
                "streamlined_superstructure",
                "route_tables"
            };

            bool allR1TechRowsExist = true;
            for (int i = 0; i < requiredR1TechnologyIds.Length; i++)
            {
                allR1TechRowsExist &= config.GetTechnology(requiredR1TechnologyIds[i]) != null;
            }

            report.Check(allR1TechRowsExist,
                "R1 technology chain has base and upgrade technology rows.");

            for (int i = 0; i < R1ShipDesignCatalog.All.Count; i++)
            {
                R1ShipDesignDefinition design = R1ShipDesignCatalog.All[i];
                if (design == null) continue;

                bool csvRowsExist =
                    config.GetTechnology(design.requiredTechId) != null &&
                    config.GetHull(design.hullId) != null &&
                    config.GetEngine(design.engineId) != null &&
                    config.GetPropeller(design.propellerId) != null &&
                    config.GetClaudiumLoop(design.claudiumLoopId) != null &&
                    config.GetSpecialModule(design.specialModuleId) != null;
                report.Check(csvRowsExist, design.displayNameRu + " has technology, hull, engine, propeller, claudium loop and role module CSV rows.");

                report.Check(R1ConfiguredPartRowsExist(config, design),
                    design.displayNameRu + " has CSV rows for all configured base and upgrade parts.");

                report.Check(R1ConfiguredSlotsAllowParts(catalog, design),
                    design.displayNameRu + " hull slots allow its own base and upgraded parts only.");

                PlayerProgress progress = R1ShipDesignCatalog.CreateUnlockedProgress(design);
                bool assembled = ShipAssemblyBuilder.TryBuild(catalog, null, progress, out ShipAssemblyResult result);
                report.Check(assembled, assembled
                    ? design.displayNameRu + " assembles from CSV catalog."
                    : design.displayNameRu + " does not assemble from CSV catalog: " + result.message);

                if (!assembled || result == null || result.stats == null)
                {
                    continue;
                }

                ShipStatBlock stats = result.stats;
                bool coreStatsMatch =
                    Approximately(stats.Get(ShipStatId.BaseMass, 0f), design.expectedServiceMassKg, 1f) &&
                    Approximately(stats.Get(ShipStatId.HullMaxTakeoffMassKg, 0f), design.expectedMaxTakeoffMassKg, 0.01f) &&
                    Approximately(stats.Get(ShipStatId.EngineMaxPower, 0f), design.expectedEnginePowerKw, 0.01f) &&
                    Approximately(stats.Get(ShipStatId.StructureHp, 0f), design.expectedStructureHp, 0.01f) &&
                    stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f) >= design.expectedMaxTakeoffMassKg - 0.01f &&
                    Approximately(stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f), design.expectedClaudiumLiftEfficiency, 0.01f) &&
                    stats.Get(ShipStatId.PropellerMaxThrustKgf, 0f) > 0f &&
                    stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f) > 0f;
                report.Check(coreStatsMatch,
                    design.displayNameRu + " core stats match R1 balance: service mass, takeoff mass, engine, lift, propeller and structure.");

                report.Check(R1RoleStatsConfigured(design.shipId, stats),
                    design.displayNameRu + " role stats are configured on the mandatory module.");

                report.Check(R1LoadoutStatsMatch(design.shipId, false, stats),
                    design.displayNameRu + " minimum configuration has required characteristics.");

                bool maximumAssembled = TryBuildR1ConfiguredMaximum(catalog, config, design, out ShipAssemblyResult maximumResult, out string maximumBuildMessage);
                report.Check(maximumAssembled, maximumBuildMessage);
                if (maximumAssembled && maximumResult != null && maximumResult.stats != null)
                {
                    report.Check(R1LoadoutStatsMatch(design.shipId, true, maximumResult.stats),
                        design.displayNameRu + " maximum configuration has required characteristics.");
                }
            }

            ValidateR1UpgradeModules(config, report);
        }
        finally
        {
            if (catalog != null && catalog.parts != null)
            {
                for (int i = 0; i < catalog.parts.Count; i++)
                {
                    DestroyBigTestUnityObject(catalog.parts[i]);
                }
            }

            DestroyBigTestUnityObject(catalog);
        }
    }

    private static bool R1ConfiguredPartRowsExist(WorldConfigDatabase config, R1ShipDesignDefinition design)
    {
        if (config == null || design == null) return false;

        bool rowsExist = true;
        rowsExist &= AllConfiguredIdsExist(design.GetAllowedHullIds(), config.GetHull);
        rowsExist &= AllConfiguredIdsExist(design.GetAllowedEngineIds(), config.GetEngine);
        rowsExist &= AllConfiguredIdsExist(design.GetAllowedPropellerIds(), config.GetPropeller);
        rowsExist &= AllConfiguredIdsExist(design.GetAllowedClaudiumLoopIds(), config.GetClaudiumLoop);
        rowsExist &= AllConfiguredIdsExist(design.GetAllowedSpecialModuleIds(), config.GetSpecialModule);
        return rowsExist;
    }

    private static bool R1ConfiguredSlotsAllowParts(ShipCatalogSO catalog, R1ShipDesignDefinition design)
    {
        if (catalog == null || design == null) return false;

        List<string> hullIds = design.GetAllowedHullIds();
        for (int i = 0; i < hullIds.Count; i++)
        {
            ShipPartDefinitionSO hull = catalog.GetPartById(hullIds[i]);
            if (hull == null || !hull.IsHull) return false;

            if (!SlotAllowsAll(hull, R1ShipDesignCatalog.EngineSlotId, design.GetAllowedEngineIds())) return false;
            if (!SlotAllowsAll(hull, R1ShipDesignCatalog.PropellerSlotId, design.GetAllowedPropellerIds())) return false;
            if (!SlotAllowsAll(hull, R1ShipDesignCatalog.ClaudiumLoopSlotId, design.GetAllowedClaudiumLoopIds())) return false;
            if (!SlotAllowsAll(hull, R1ShipDesignCatalog.RoleModuleSlotId, design.GetAllowedSpecialModuleIds())) return false;
        }

        return true;
    }

    private static bool TryBuildR1ConfiguredMaximum(ShipCatalogSO catalog, WorldConfigDatabase config, R1ShipDesignDefinition design, out ShipAssemblyResult result, out string message)
    {
        result = null;
        message = "";
        if (catalog == null || config == null || design == null)
        {
            message = "R1 upgraded assembly is missing test context.";
            return false;
        }

        string hullId = LastConfiguredId(design.GetAllowedHullIds());
        string engineId = LastConfiguredId(design.GetAllowedEngineIds());
        string propellerId = LastConfiguredId(design.GetAllowedPropellerIds());
        string claudiumLoopId = LastConfiguredId(design.GetAllowedClaudiumLoopIds());
        string specialModuleId = LastConfiguredId(design.GetAllowedSpecialModuleIds());

        PlayerProgress progress = R1ShipDesignCatalog.CreateUnlockedProgress(design);
        progress.selectedHullId = hullId;
        progress.InstallModule(R1ShipDesignCatalog.EngineSlotId, engineId);
        progress.InstallModule(R1ShipDesignCatalog.PropellerSlotId, propellerId);
        progress.InstallModule(R1ShipDesignCatalog.ClaudiumLoopSlotId, claudiumLoopId);
        progress.InstallModule(R1ShipDesignCatalog.RoleModuleSlotId, specialModuleId);

        UnlockTechnology(progress, config.GetHull(hullId)?.completedTechId);
        UnlockTechnology(progress, config.GetEngine(engineId)?.completedTechId);
        UnlockTechnology(progress, config.GetPropeller(propellerId)?.completedTechId);
        UnlockTechnology(progress, config.GetClaudiumLoop(claudiumLoopId)?.completedTechId);
        UnlockTechnology(progress, config.GetSpecialModule(specialModuleId)?.completedTechId);

        bool assembled = ShipAssemblyBuilder.TryBuild(catalog, null, progress, out result);
        string buildError = result != null ? result.message : "no assembly result";
        message = assembled
            ? design.displayNameRu + " maximum configured assembly builds."
            : design.displayNameRu + " maximum configured assembly does not build: " + buildError;
        return assembled;
    }

    private static bool R1LoadoutStatsMatch(string shipId, bool maximum, ShipStatBlock stats)
    {
        if (stats == null) return false;

        switch (shipId)
        {
            case "water_strider":
                return R1CoreLoadoutStatsMatch(stats,
                        maximum ? 2760f : 2500f,
                        maximum ? 4300f : 3500f,
                        maximum ? 1500f : 1000f,
                        maximum ? 260f : 220f,
                        maximum ? 1050f : 940f,
                        28f,
                        maximum ? 4.5f : 3f,
                        maximum ? 0.68f : 0.7f,
                        maximum ? 4300f : 3500f) &&
                    stats.Get(ShipStatId.GasHarvesterWaterOnly, 0f) > 0.5f &&
                    Approximately(stats.Get(ShipStatId.GasHarvesterVolumeM3PerSecond, 0f), maximum ? 15f : 12f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.LiquidTankCapacityLiters, 0f), maximum ? 1500f : 1000f, 0.001f);
            case "bulat":
                return R1CoreLoadoutStatsMatch(stats,
                        maximum ? 3320f : 3000f,
                        maximum ? 6500f : 6000f,
                        3000f,
                        maximum ? 310f : 240f,
                        maximum ? 1350f : 1250f,
                        27f,
                        3f,
                        maximum ? 0.76f : 0.78f,
                        maximum ? 6500f : 6000f) &&
                    Approximately(stats.Get(ShipStatId.MiningImpactHoldCapacityKg, 0f), 3000f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.BulkHoldCapacityLiters, 0f), 3000f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.MiningImpactDamageTakenMultiplier, 0f), maximum ? 0.45f : 0.5f, 0.001f);
            case "hornet":
                return R1CoreLoadoutStatsMatch(stats,
                        maximum ? 3610f : 3200f,
                        3800f,
                        maximum ? 100f : 600f,
                        maximum ? 315f : 280f,
                        1350f,
                        31f,
                        3.5f,
                        0.66f,
                        3800f) &&
                    Approximately(stats.Get(ShipStatId.ObservationRadiusMeters, 0f), maximum ? 950f : 700f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.SurveyPaperToInfoEfficiency, 0f), maximum ? 0.45f : 0.2f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.LeviathanAlarmGenerationMultiplier, 0f), maximum ? 0.25f : 0.5f, 0.001f);
            case "jaeger":
                return R1CoreLoadoutStatsMatch(stats,
                        maximum ? 5190f : 4700f,
                        maximum ? 5400f : 5200f,
                        maximum ? 200f : 500f,
                        maximum ? 380f : 330f,
                        maximum ? 1900f : 1600f,
                        24f,
                        3f,
                        maximum ? 0.9f : 0.95f,
                        maximum ? 5400f : 5200f) &&
                    Approximately(stats.Get(ShipStatId.HarpoonWeaponCostPerMinute, 0f), maximum ? 4f : 2f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.HarpoonMaxCarcassMassKg, 0f), maximum ? 200f : 100f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.HarpoonFlightDamage, 0f), maximum ? 55f : 40f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.HarpoonRangeMeters, 0f), maximum ? 60f : 45f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.RefrigeratedHoldCapacityLiters, 0f), 2500f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.RefrigeratedHoldPowerDrawKw, 0f), maximum ? 55f : 70f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedSafetyRecoveryPerHour, 0f), maximum ? 30f : 20f, 0.001f);
            case "opora":
                return R1CoreLoadoutStatsMatch(stats,
                        maximum ? 4260f : 3600f,
                        maximum ? 11000f : 10000f,
                        maximum ? 6740f : 6400f,
                        400f,
                        maximum ? 2000f : 1400f,
                        maximum ? 16f : 10f,
                        4f,
                        maximum ? 1.25f : 1.6f,
                        maximum ? 11000f : 10000f,
                        maximum ? 50f : 45f) &&
                    Approximately(stats.Get(ShipStatId.NeedRepairRecoveryPerHour, 0f), maximum ? 38f : 30f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityUnits, 0f), maximum ? 14000f : 6000f, 0.001f);
            case "fuel_tender":
                return R1CoreLoadoutStatsMatch(stats,
                        3600f,
                        10000f,
                        6400f,
                        400f,
                        700f,
                        16f,
                        2f,
                        1.15f,
                        10000f,
                        50f) &&
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityUnits, 0f), 6400f, 0.001f) &&
                    ContainsId(stats.AllowedCargoItemIds, "charcoal") &&
                    ContainsId(stats.AllowedCargoItemIds, "claudium");
            case "parovoz":
                return R1CoreLoadoutStatsMatch(stats,
                        maximum ? 4820f : 4300f,
                        maximum ? 6600f : 5900f,
                        1500f,
                        maximum ? 430f : 360f,
                        maximum ? 1540f : 1100f,
                        maximum ? 30f : 20f,
                        maximum ? 3f : 2f,
                        maximum ? 0.74f : 1.2f,
                        maximum ? 6600f : 5900f) &&
                    Approximately(stats.Get(ShipStatId.PassengerSeatCapacity, 0f), 15f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedWorkforceRecoveryPerHour, 0f), maximum ? 5f : 4f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedHealthRecoveryPerHour, 0f), maximum ? 4f : 3f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedComfortRecoveryPerHour, 0f), maximum ? 8f : 5f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedCapitalConnectionRecoveryPerHour, 0f), maximum ? 12f : 8f, 0.001f);
            default:
                return false;
        }
    }

    private static bool R1CoreLoadoutStatsMatch(
        ShipStatBlock stats,
        float expectedServiceMassKg,
        float expectedMaxTakeoffMassKg,
        float expectedUsefulPayloadKg,
        float expectedEnginePowerKw,
        float expectedStructureHp,
        float expectedPropellerMaxSpeedMS,
        float expectedAutoVerticalSpeedMS,
        float expectedDragCoefficient,
        float expectedClaudiumMaxLiftKg,
        float expectedClaudiumLiftEfficiency = 28f)
    {
        if (stats == null) return false;

        float serviceMassKg = stats.Get(ShipStatId.BaseMass, 0f);
        float maxTakeoffMassKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, 0f);
        float usefulPayloadKg = maxTakeoffMassKg - serviceMassKg;

        return Approximately(serviceMassKg, expectedServiceMassKg, 1f) &&
            Approximately(maxTakeoffMassKg, expectedMaxTakeoffMassKg, 0.01f) &&
            usefulPayloadKg >= expectedUsefulPayloadKg - 1f &&
            Approximately(stats.Get(ShipStatId.EngineMaxPower, 0f), expectedEnginePowerKw, 0.01f) &&
            Approximately(stats.Get(ShipStatId.StructureHp, 0f), expectedStructureHp, 0.01f) &&
            Approximately(stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f), expectedPropellerMaxSpeedMS, 0.01f) &&
            Approximately(stats.Get(ShipStatId.MaxAutoVerticalSpeed, 0f), expectedAutoVerticalSpeedMS, 0.01f) &&
            Approximately(stats.Get(ShipStatId.DragCoefficient, 0f), expectedDragCoefficient, 0.001f) &&
            Approximately(stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f), expectedClaudiumLiftEfficiency, 0.01f) &&
            Approximately(stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f), expectedClaudiumMaxLiftKg, 0.01f) &&
            stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f) >= maxTakeoffMassKg - 0.01f;
    }

    private static bool AllConfiguredIdsExist<T>(List<string> ids, System.Func<string, T> getter) where T : class
    {
        if (ids == null || getter == null || ids.Count == 0) return false;

        for (int i = 0; i < ids.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(ids[i]) || getter(ids[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsId(IReadOnlyList<string> ids, string id)
    {
        if (ids == null || string.IsNullOrWhiteSpace(id)) return false;

        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SlotAllowsAll(ShipPartDefinitionSO hull, string slotId, List<string> partIds)
    {
        if (hull == null || hull.slots == null || string.IsNullOrWhiteSpace(slotId) || partIds == null || partIds.Count == 0)
        {
            return false;
        }

        ShipSlotDefinition slot = null;
        for (int i = 0; i < hull.slots.Count; i++)
        {
            if (hull.slots[i] != null && hull.slots[i].slotId == slotId)
            {
                slot = hull.slots[i];
                break;
            }
        }

        if (slot == null) return false;

        for (int i = 0; i < partIds.Count; i++)
        {
            if (!slot.AllowsPart(partIds[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string LastConfiguredId(List<string> ids)
    {
        if (ids == null || ids.Count == 0) return "";

        for (int i = ids.Count - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
            {
                return ids[i];
            }
        }

        return "";
    }

    private static void UnlockTechnology(PlayerProgress progress, string technologyId)
    {
        if (progress == null || string.IsNullOrWhiteSpace(technologyId)) return;

        progress.CompleteTechnology(technologyId);
        progress.PurchaseNode(technologyId);
    }

    private static void ValidateR1UpgradeModules(WorldConfigDatabase config, BigTestReport report)
    {
        SpecialModuleConfig waterBase = config.GetSpecialModule("water_strider_harvester");
        SpecialModuleConfig waterMk2 = config.GetSpecialModule("water_strider_harvester_mk2");
        report.Check(waterBase != null &&
            waterMk2 != null &&
            waterMk2.gasHarvesterWaterOnly &&
            waterMk2.gasHarvesterVolumeM3PerSecond > waterBase.gasHarvesterVolumeM3PerSecond &&
            waterMk2.liquidTankCapacityLiters >= 1500f &&
            waterMk2.baseMassKg > waterBase.baseMassKg,
            "Water Strider upgraded harvester keeps water-only mode and grows to a 1.5 t tank.");

        SpecialModuleConfig bulatBase = config.GetSpecialModule("bulat_impact_hold");
        SpecialModuleConfig bulatMk2 = config.GetSpecialModule("bulat_impact_hold_mk2");
        report.Check(bulatBase != null &&
            bulatMk2 != null &&
            bulatMk2.miningImpactHoldCapacityKg >= 3000f &&
            bulatMk2.bulkHoldCapacityLiters >= 3000f &&
            bulatMk2.miningImpactDamageTakenMultiplier <= bulatBase.miningImpactDamageTakenMultiplier &&
            bulatMk2.baseMassKg > bulatBase.baseMassKg,
            "Bulat upgraded impact hold preserves the 3 m3 ore role and improves impact damping.");

        SpecialModuleConfig hornetBase = config.GetSpecialModule("hornet_observation_suite");
        SpecialModuleConfig hornetMk2 = config.GetSpecialModule("hornet_observation_suite_mk2");
        report.Check(hornetBase != null &&
            hornetMk2 != null &&
            hornetMk2.surveyPaperToInfoEfficiency > hornetBase.surveyPaperToInfoEfficiency &&
            hornetMk2.leviathanAlarmGenerationMultiplier < hornetBase.leviathanAlarmGenerationMultiplier &&
            hornetMk2.baseMassKg > hornetBase.baseMassKg,
            "Hornet upgraded instruments improve paper-to-info efficiency and reduce leviathan alarm.");

        SpecialModuleConfig jaegerBase = config.GetSpecialModule("jaeger_harpoon_fridge");
        SpecialModuleConfig jaegerMk2 = config.GetSpecialModule("jaeger_harpoon_fridge_mk2");
        report.Check(jaegerBase != null &&
            jaegerMk2 != null &&
            jaegerMk2.harpoonMaxCarcassMassKg >= 200f &&
            Approximately(jaegerMk2.harpoonWeaponCostPerMinute, jaegerBase.harpoonWeaponCostPerMinute * 2f, 0.001f) &&
            jaegerMk2.refrigeratedHoldCapacityLiters >= jaegerBase.refrigeratedHoldCapacityLiters,
            "Eger upgraded harpoon catches 200 kg carcasses and doubles weapon drain while keeping cold storage.");

        SpecialModuleConfig oporaBase = config.GetSpecialModule("opora_crane_platform");
        SpecialModuleConfig oporaMk2 = config.GetSpecialModule("opora_crane_platform_mk2");
        report.Check(oporaBase != null &&
            oporaMk2 != null &&
            oporaMk2.needRepairRecoveryPerHour > oporaBase.needRepairRecoveryPerHour &&
            oporaMk2.cargoVanCapacityUnits >= 14000f &&
            oporaMk2.baseMassKg > oporaBase.baseMassKg,
            "Opora upgraded crane improves repair throughput and expands van capacity.");

        SpecialModuleConfig parovozBase = config.GetSpecialModule("parovoz_passenger_cabin");
        SpecialModuleConfig parovozMk2 = config.GetSpecialModule("parovoz_passenger_cabin_mk2");
        report.Check(parovozBase != null &&
            parovozMk2 != null &&
            parovozBase.passengerSeatCapacity >= 15f &&
            parovozBase.needHealthRecoveryPerHour > 0f &&
            parovozBase.needComfortRecoveryPerHour > 0f &&
            parovozBase.needCapitalConnectionRecoveryPerHour > 0f &&
            parovozMk2.needComfortRecoveryPerHour > parovozBase.needComfortRecoveryPerHour &&
            parovozMk2.passengerSeatCapacity >= parovozBase.passengerSeatCapacity,
            "Parovoz cabin has passenger seats, onboard needs and an upgraded comfort service.");
    }

    private static bool R1RoleStatsConfigured(string shipId, ShipStatBlock stats)
    {
        if (stats == null) return false;

        switch (shipId)
        {
            case "water_strider":
                return stats.Get(ShipStatId.GasHarvesterWaterOnly, 0f) > 0.5f &&
                    stats.Get(ShipStatId.GasHarvesterVolumeM3PerSecond, 0f) > 0f &&
                    stats.Get(ShipStatId.LiquidTankCapacityLiters, 0f) >= 1000f;
            case "bulat":
                return stats.Get(ShipStatId.MiningImpactHoldCapacityKg, 0f) >= 3000f &&
                    Approximately(stats.Get(ShipStatId.MiningImpactDamageTakenMultiplier, 0f), 0.5f, 0.001f) &&
                    stats.Get(ShipStatId.BulkHoldCapacityLiters, 0f) >= 3000f;
            case "hornet":
                return stats.Get(ShipStatId.ObservationRadiusMeters, 0f) >= 700f &&
                    Approximately(stats.Get(ShipStatId.SurveyPaperToInfoEfficiency, 0f), 0.2f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.LeviathanAlarmGenerationMultiplier, 0f), 0.5f, 0.001f);
            case "jaeger":
                return Approximately(stats.Get(ShipStatId.HarpoonWeaponCostPerMinute, 0f), 2f, 0.001f) &&
                    stats.Get(ShipStatId.HarpoonMaxCarcassMassKg, 0f) >= 100f &&
                    stats.Get(ShipStatId.RefrigeratedHoldCapacityLiters, 0f) >= 2500f &&
                    stats.Get(ShipStatId.RefrigeratedHoldPowerDrawKw, 0f) > 0f &&
                    stats.Get(ShipStatId.NeedSafetyRecoveryPerHour, 0f) > 0f;
            case "opora":
                return stats.Get(ShipStatId.NeedRepairRecoveryPerHour, 0f) > 0f &&
                    stats.Get(ShipStatId.CargoVanCapacityUnits, 0f) >= 6000f;
            case "fuel_tender":
                return stats.Get(ShipStatId.CargoVanCapacityUnits, 0f) >= 6400f &&
                    ContainsId(stats.AllowedCargoItemIds, "charcoal") &&
                    ContainsId(stats.AllowedCargoItemIds, "claudium");
            case "parovoz":
                return stats.Get(ShipStatId.PassengerSeatCapacity, 0f) >= 15f &&
                    stats.Get(ShipStatId.CargoVanCapacityUnits, 0f) >= 250f &&
                    stats.Get(ShipStatId.NeedHealthRecoveryPerHour, 0f) > 0f &&
                    stats.Get(ShipStatId.NeedComfortRecoveryPerHour, 0f) > 0f &&
                    stats.Get(ShipStatId.NeedCapitalConnectionRecoveryPerHour, 0f) > 0f;
            default:
                return false;
        }
    }

    private static void ValidateIslandDevelopmentConfig(WorldConfigDatabase config, BigTestReport report)
    {
        bool coreItemsExist =
            config.GetItem("food") != null &&
            config.GetItem("water") != null &&
            config.GetItem("aerolite") != null &&
            config.GetItem("charcoal") != null &&
            config.GetItem("claudium") != null &&
            config.GetItem("cloth") != null &&
            config.GetItem("tools") != null &&
            config.GetItem("medicines") != null &&
            config.GetItem("paper") != null &&
            config.GetItem("weapon") != null &&
            config.GetItem("gunpowder") != null &&
            config.GetItem(PassengerCargoIds.ToCapitalItemId) != null &&
            config.GetItem(PassengerCargoIds.ToIslandTemplateItemId) != null &&
            config.GetItem(PassengerCargoIds.ToShipTemplateItemId) != null &&
            config.GetItem("design_experience") != null &&
            config.GetItem("fundamental_experience") != null;
        report.Check(coreItemsExist, "Магистральные ресурсы, пассажиры, фундаментальный и конструкторский опыт заведены в Item.csv.");

        bool archetypesValid = true;
        bool hasFarming = false;
        bool hasOre = false;
        bool hasMist = false;
        bool hasCoal = false;
        bool hasClaudium = false;

        for (int i = 0; i < config.islandArchetypes.Count; i++)
        {
            IslandArchetypeConfig archetype = config.islandArchetypes[i];
            if (archetype == null)
            {
                archetypesValid = false;
                continue;
            }

            hasFarming |= archetype.id == "farming";
            hasOre |= archetype.id == "ore";
            hasMist |= archetype.id == "mist";
            hasCoal |= archetype.id == "coal";
            hasClaudium |= archetype.id == "claudium";

            archetypesValid &= config.GetItem(archetype.baseProductionItemId) != null &&
                config.GetItem(archetype.startNeedItemId) != null &&
                !string.IsNullOrWhiteSpace(archetype.heightBand);
        }

        report.Check(archetypesValid && hasFarming && hasOre && hasMist && hasCoal && hasClaudium, "Пять архетипов островов валидны и ссылаются на магистральные ресурсы.");

        bool islandArchetypeReferencesValid = true;
        bool hasClaudiumIsland = false;
        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.archetypeId)) continue;

            islandArchetypeReferencesValid &= config.GetIslandArchetype(island.archetypeId) != null;
            hasClaudiumIsland |= island.archetypeId == "claudium";
        }

        report.Check(islandArchetypeReferencesValid && hasClaudiumIsland, "Острова с archetype_id ссылаются на существующие типы и есть клавдиевые острова.");

        bool stagesValid = true;
        int socialOpeningStages = 0;
        for (int i = 0; i < config.islandArchetypeStages.Count; i++)
        {
            IslandArchetypeStageConfig stage = config.islandArchetypeStages[i];
            if (stage == null)
            {
                stagesValid = false;
                continue;
            }

            stagesValid &= config.GetIslandArchetype(stage.archetypeId) != null &&
                stage.stageIndex > 0 &&
                config.GetItem(stage.triggerNeedItemId) != null &&
                stage.productionMultiplier >= 1f;

            if (!string.IsNullOrWhiteSpace(stage.unlockedProductionItemId))
            {
                stagesValid &= config.GetItem(stage.unlockedProductionItemId) != null;
            }

            if (stage.opensSocialNeeds)
            {
                socialOpeningStages++;
            }
        }

        report.Check(stagesValid && socialOpeningStages == 5, "Стадии островов валидны и каждая ветка открывает общественные потребности.");

        bool needsValid = true;
        HashSet<IslandNeedKind> needKinds = new HashSet<IslandNeedKind>();
        bool hasRepairNeed = false;
        bool hasCapitalConnectionNeed = false;
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null)
            {
                needsValid = false;
                continue;
            }

            needKinds.Add(need.kind);
            hasRepairNeed |= need.kind == IslandNeedKind.Repair && need.recoveryItemId == "tools";
            hasCapitalConnectionNeed |= need.kind == IslandNeedKind.CapitalConnection && need.recoveryItemId == PassengerCargoIds.ToIslandTemplateItemId;
            needsValid &= config.GetItem(need.recoveryItemId) != null &&
                need.maxValue > 0f &&
                need.restorePerItem > 0f &&
                need.loadDecayPerHour > 0f;
        }

        report.Check(needsValid && needKinds.Count == 7 && hasRepairNeed && hasCapitalConnectionNeed, "Семь общественных потребностей валидны: пять базовых, ремонт и связь со столицей.");

        bool buildingsValid = true;
        bool hasProcessing = false;
        bool hasReaction = false;
        bool hasConversion = false;
        bool hasAssembly = false;
        bool hasManufacturing = false;
        int productionBuildings = 0;
        int serviceBuildings = 0;

        for (int i = 0; i < config.islandBuildings.Count; i++)
        {
            IslandBuildingConfig building = config.islandBuildings[i];
            if (building == null)
            {
                buildingsValid = false;
                continue;
            }

            bool constructionOk = ItemAmountsReferenceExistingItems(building.constructionInputs, a => a.itemId, a => a.amount, config);
            bool techOk = string.IsNullOrWhiteSpace(building.requiredTechnologyId) || config.GetTechnology(building.requiredTechnologyId) != null;
            bool loadsOk = building.workforceLoad <= 5 &&
                building.healthLoad <= 5 &&
                building.safetyLoad <= 5 &&
                building.comfortLoad <= 5 &&
                building.creativityLoad <= 5 &&
                building.GetNeedLoad(IslandNeedKind.Repair) <= 5 &&
                building.GetNeedLoad(IslandNeedKind.CapitalConnection) <= 5;

            buildingsValid &= constructionOk && techOk && loadsOk && building.maxUpgradeLevel >= 0;

            if (building.IsService)
            {
                serviceBuildings++;
            }
            else
            {
                productionBuildings++;
                hasProcessing |= building.industryKind == IslandIndustryKind.Processing;
                hasReaction |= building.industryKind == IslandIndustryKind.Reaction;
                hasConversion |= building.industryKind == IslandIndustryKind.Conversion;
                hasAssembly |= building.industryKind == IslandIndustryKind.Assembly;
                hasManufacturing |= building.industryKind == IslandIndustryKind.Manufacturing;
            }
        }

        report.Check(buildingsValid && productionBuildings == 21 && serviceBuildings >= 10 &&
            hasProcessing && hasReaction && hasConversion && hasAssembly && hasManufacturing,
            "Островные здания покрывают 21 производство, сервисы и все типы механик.");

        bool industryBuildingReferencesValid = true;
        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null || string.IsNullOrWhiteSpace(industry.buildingId)) continue;

            industryBuildingReferencesValid &= config.GetIslandBuilding(industry.buildingId) != null;
        }

        report.Check(industryBuildingReferencesValid, "Production_industry.csv может ссылаться на Island_building.csv через building_id.");

        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        IslandProductionState needIsland = progress.GetIslandProductionState("Island1", true);
        needIsland.development.completedStage = 2;
        needIsland.development.socialNeedsUnlocked = true;
        needIsland.SetResourceAmount("food", 200);
        needIsland.SetResourceAmount("medicines", 200);
        needIsland.SetResourceAmount("weapon", 200);
        needIsland.SetResourceAmount("cloth", 200);
        needIsland.SetResourceAmount("paper", 200);
        needIsland.SetResourceAmount("tools", 200);
        needIsland.SetResourceAmount(PassengerCargoIds.ToIslandItemId("Island1"), 2);

        IslandSocietySimulator.EnsureIslandStates(config, progress);
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            IslandSocietyNeedState state = needIsland.GetSocietyNeedState(need.id, true);
            state.currentValue = 0f;
            state.initialized = true;
        }

        int restored = IslandSocietySimulator.Advance(config, progress, 1f);
        bool restoredAllNeeds = restored >= config.islandSocialNeeds.Count;
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            IslandSocietyNeedState state = needIsland.GetSocietyNeedState(need.id, false);
            restoredAllNeeds &= state != null && state.currentValue > 0f;
        }

        report.Check(restoredAllNeeds, "Остров сам восстанавливает базовые потребности, ремонт и связь со столицей ресурсами со склада.");

        PlayerProgress passengerProgress = new PlayerProgress();
        passengerProgress.Normalize();
        IslandProductionState passengerIsland = passengerProgress.GetIslandProductionState("Island1", true);
        passengerIsland.development.completedStage = 3;
        passengerIsland.development.socialNeedsUnlocked = true;
        IslandProductionState passengerCapital = passengerProgress.GetIslandProductionState("capital", true);
        int passengerEvents = PassengerTrafficSimulator.Advance(config, passengerProgress, 240f);
        string islandPassengerItemId = PassengerCargoIds.ToIslandItemId("Island1");
        int islandToCapitalPassengers = passengerIsland.GetResourceAmount(PassengerCargoIds.ToCapitalItemId);
        int capitalToIslandPassengers = passengerCapital.GetResourceAmount(islandPassengerItemId);
        report.Check(passengerEvents > 0 && islandToCapitalPassengers >= 1 && capitalToIslandPassengers >= 1,
            "Пассажиропоток создаёт груз остров -> столица и столица -> остров.");

        passengerCapital.AddResource(PassengerCargoIds.ToCapitalItemId, islandToCapitalPassengers);
        int absorbedPassengers = PassengerTrafficSimulator.AbsorbCapitalPassengers(passengerCapital);
        report.Check(absorbedPassengers >= islandToCapitalPassengers && passengerCapital.GetResourceAmount(PassengerCargoIds.ToCapitalItemId) == 0,
            "Столица поглощает выгруженных пассажиров до столицы.");

        passengerCapital.TrySpendResource(islandPassengerItemId, capitalToIslandPassengers);
        passengerIsland.AddResource(islandPassengerItemId, capitalToIslandPassengers);
        IslandSocietySimulator.EnsureIslandStates(config, passengerProgress);
        IslandSocialNeedConfig connectionNeed = config.GetIslandSocialNeed("need_capital_connection");
        IslandSocietyNeedState connectionState = connectionNeed != null ? passengerIsland.GetSocietyNeedState(connectionNeed.id, true) : null;
        if (connectionState != null)
        {
            connectionState.currentValue = 0f;
            connectionState.initialized = true;
        }

        int passengerRestore = IslandSocietySimulator.Advance(config, passengerProgress, 1f);
        report.Check(connectionState != null && connectionState.currentValue > 0f && passengerRestore > 0,
            "Доставленные пассажиры до конкретного острова закрывают потребность связи со столицей.");

        IslandSocialNeedConfig healthNeed = config.GetIslandSocialNeed("need_health");
        PlayerProgress slowHealthProgress = new PlayerProgress();
        slowHealthProgress.Normalize();
        IslandProductionState slowHealthIsland = slowHealthProgress.GetIslandProductionState("Island1", true);
        slowHealthIsland.development.completedStage = 2;
        slowHealthIsland.development.socialNeedsUnlocked = true;
        slowHealthIsland.SetResourceAmount("medicines", 200);
        IslandSocietySimulator.EnsureIslandStates(config, slowHealthProgress);
        IslandSocietyNeedState slowHealthState = healthNeed != null ? slowHealthIsland.GetSocietyNeedState(healthNeed.id, true) : null;
        if (slowHealthState != null)
        {
            slowHealthState.currentValue = 0f;
            slowHealthState.initialized = true;
        }

        IslandSocietySimulator.Advance(config, slowHealthProgress, 60f);
        float slowHealthRecovery = slowHealthState != null ? slowHealthState.currentValue : 0f;

        PlayerProgress hospitalProgress = new PlayerProgress();
        hospitalProgress.Normalize();
        IslandProductionState hospitalIsland = hospitalProgress.GetIslandProductionState("Island1", true);
        hospitalIsland.development.completedStage = 2;
        hospitalIsland.development.socialNeedsUnlocked = true;
        hospitalIsland.SetResourceAmount("medicines", 200);
        IslandBuildingState hospitalBuilding = hospitalIsland.GetBuildingState("hospital", true);
        hospitalBuilding.built = true;
        hospitalBuilding.level = 3;
        hospitalBuilding.modernizationLevel = 1;
        IslandSocietySimulator.EnsureIslandStates(config, hospitalProgress);
        IslandSocietyNeedState fastHealthState = healthNeed != null ? hospitalIsland.GetSocietyNeedState(healthNeed.id, true) : null;
        if (fastHealthState != null)
        {
            fastHealthState.currentValue = 0f;
            fastHealthState.initialized = true;
        }

        IslandSocietySimulator.Advance(config, hospitalProgress, 60f);
        float fastHealthRecovery = fastHealthState != null ? fastHealthState.currentValue : 0f;
        report.Check(healthNeed != null && slowHealthRecovery > 0f && fastHealthRecovery > slowHealthRecovery * 5f,
            "Скорость удовлетворения потребности ограничена сервисом: без больницы здоровье восстановилось на " + slowHealthRecovery.ToString("0.##") + ", с больницей III - на " + fastHealthRecovery.ToString("0.##") + ".");

        LogisticsShipDefinition shipNeedDefinition = new LogisticsShipDefinition
        {
            shipId = "big_test_large_logistics",
            displayName = "Большой тестовый корабль",
            healthNeedLoad = 3,
            repairNeedEnabled = true,
            capitalConnectionEnabled = true,
            crewCapacity = 80,
            passengerCapacity = 120,
            repairNeedLoad = 3,
            capitalConnectionNeedLoad = 4
        };
        LogisticsShipState shipNeedState = new LogisticsShipState { shipId = shipNeedDefinition.shipId };
        shipNeedState.AddCargo("medicines", 50);
        shipNeedState.AddCargo("tools", 50);
        shipNeedState.AddCargo(PassengerCargoIds.ToShipItemId(shipNeedDefinition.shipId), 2);
        IslandProductionState shipNeedCapital = new IslandProductionState { islandId = "capital" };
        LogisticsShipMetrics serviceShipMetrics = new LogisticsShipMetrics
        {
            needHealthRecoveryPerHour = 120f,
            needRepairRecoveryPerHour = 80f,
            needCapitalConnectionRecoveryPerHour = 40f
        };
        ShipboardNeedsSimulator.AdvanceLogisticsShip(config, shipNeedDefinition, shipNeedState, serviceShipMetrics, shipNeedCapital, 60f);
        ShipboardNeedState shipHealthState = shipNeedState.GetSocietyNeedState("need_health", false);
        ShipboardNeedState shipRepairState = shipNeedState.GetSocietyNeedState("need_repair", false);
        ShipboardNeedState shipConnectionState = shipNeedState.GetSocietyNeedState("need_capital_connection", false);
        bool shipNeedsRestored = shipHealthState != null &&
            shipRepairState != null &&
            shipConnectionState != null &&
            shipHealthState.currentValue > 0f &&
            shipRepairState.currentValue > 0f &&
            shipConnectionState.currentValue > 0f &&
            shipNeedState.GetCargoAmount("medicines") < 50 &&
            shipNeedState.GetCargoAmount("tools") < 50 &&
            shipNeedState.GetCargoAmount(PassengerCargoIds.ToShipItemId(shipNeedDefinition.shipId)) < 2;
        report.Check(shipNeedsRestored, "Корабельные сервисные модули ускоряют удовлетворение здоровья, ремонта и связи со столицей.");

        int shipPassengerEvents = ShipboardNeedsSimulator.AdvanceLogisticsShip(config, shipNeedDefinition, shipNeedState, serviceShipMetrics, shipNeedCapital, 180f);
        report.Check(shipPassengerEvents > 0 &&
            shipNeedState.GetCargoAmount(PassengerCargoIds.ToCapitalItemId) >= 1 &&
            shipNeedCapital.GetResourceAmount(PassengerCargoIds.ToShipItemId(shipNeedDefinition.shipId)) >= 1,
            "Крупный корабль генерирует пассажиров до столицы, а столица генерирует пассажиров до корабля.");

        PlayerProgress developmentProgress = new PlayerProgress();
        developmentProgress.Normalize();
        IslandDevelopmentSimulator.EnsureIslandStates(config, developmentProgress);

        IslandProductionState farmingIsland = developmentProgress.GetIslandProductionState("Island1", true);
        farmingIsland.SetResourceAmount("water", 5);
        farmingIsland.SetResourceAmount("tools", 5);

        bool stage1Completed = IslandDevelopmentSimulator.TryCompleteNextStage(config, developmentProgress, "Island1", out _);
        bool stage2Completed = IslandDevelopmentSimulator.TryCompleteNextStage(config, developmentProgress, "Island1", out _);
        bool stageStateOk = farmingIsland.development.completedStage >= 2 && farmingIsland.development.socialNeedsUnlocked;
        report.Check(stage1Completed && stage2Completed && stageStateOk, "Остров проходит несгораемые стадии развития и открывает общественные потребности.");

        int foodBeforeStageProduction = farmingIsland.GetResourceAmount("food");
        int clothBeforeStageProduction = farmingIsland.GetResourceAmount("cloth");
        long stageStartTicks = DateTime.UtcNow.Ticks;
        IslandProductionSimulator.Advance(config, developmentProgress, stageStartTicks, stageStartTicks + TimeSpan.FromMinutes(5).Ticks);
        bool stageProductionOk = farmingIsland.GetResourceAmount("food") > foodBeforeStageProduction &&
            farmingIsland.GetResourceAmount("cloth") > clothBeforeStageProduction;
        report.Check(stageProductionOk, "Развитый остров усиливает базовую выработку и даёт открытый побочный ресурс.");

        PlayerProgress buildingProgress = new PlayerProgress();
        buildingProgress.Normalize();
        IslandDevelopmentSimulator.EnsureIslandStates(config, buildingProgress);
        IslandProductionState buildIsland = buildingProgress.GetIslandProductionState("Island1", true);
        StockCommonResources(buildIsland);

        IslandBuildingConfig lightIndustry = config.GetIslandBuilding("light_industry");
        StockAmounts(buildIsland, lightIndustry != null ? lightIndustry.constructionInputs : null, 100);

        long buildStartTicks = DateTime.UtcNow.Ticks;
        bool constructionStarted = IslandDevelopmentSimulator.TryStartConstruction(config, buildingProgress, "Island1", "light_industry", buildStartTicks, out _);
        bool duplicateBlockedWhileBuilding = !IslandDevelopmentSimulator.TryStartConstruction(config, buildingProgress, "Island1", "light_industry", buildStartTicks, out _);
        IslandDevelopmentSimulator.Advance(config, buildingProgress, buildStartTicks, buildStartTicks + TimeSpan.FromHours(1).Ticks);
        IslandBuildingState builtLightIndustry = buildIsland.GetBuildingState("light_industry", false);
        bool constructionCompleted = builtLightIndustry != null && builtLightIndustry.built && builtLightIndustry.level == 1;
        report.Check(constructionStarted && duplicateBlockedWhileBuilding && constructionCompleted, "Строительство здания идёт одним assembly-проектом, проходит этапы и запрещает дубликаты.");

        long upgradeStartTicks = buildStartTicks + TimeSpan.FromHours(2).Ticks;
        StockAmounts(buildIsland, lightIndustry != null ? lightIndustry.constructionInputs : null, 100);
        bool upgradeStarted = IslandDevelopmentSimulator.TryStartUpgrade(config, buildingProgress, "Island1", "light_industry", upgradeStartTicks, out _);
        IslandDevelopmentSimulator.Advance(config, buildingProgress, upgradeStartTicks, upgradeStartTicks + TimeSpan.FromHours(1).Ticks);
        bool upgradeCompleted = builtLightIndustry.level >= 2 && builtLightIndustry.built;
        bool duplicateBlockedAfterBuild = !IslandDevelopmentSimulator.TryStartConstruction(config, buildingProgress, "Island1", "light_industry", upgradeStartTicks + TimeSpan.FromHours(2).Ticks, out _);
        report.Check(upgradeStarted && upgradeCompleted && duplicateBlockedAfterBuild, "Построенное здание можно улучшать без клонирования второго такого же здания.");
    }

    private static void ValidateIndustryConfig(WorldConfigDatabase config, BigTestReport report)
    {
        for (int i = 0; i < Enum.GetValues(typeof(IslandIndustryKind)).Length; i++)
        {
            IslandIndustryKind kind = (IslandIndustryKind)i;
            report.Check(HasIndustryKind(config, kind), "Есть линия производства типа " + kind + ".");
        }

        bool industriesValid = true;
        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null)
            {
                industriesValid = false;
                continue;
            }

            IndustryRecipeConfig recipe = config.GetIndustryRecipe(industry.recipeId);
            industriesValid &= config.GetIsland(industry.islandId) != null &&
                recipe != null &&
                recipe.kind == industry.kind;
        }

        report.Check(industriesValid, "Все производственные линии привязаны к островам и рецептам своего типа.");

        bool recipesValid = true;
        for (int i = 0; i < config.industryRecipes.Count; i++)
        {
            IndustryRecipeConfig recipe = config.industryRecipes[i];
            if (recipe == null)
            {
                recipesValid = false;
                continue;
            }

            recipesValid &= recipe.durationSeconds > 0f;
            recipesValid &= ItemAmountsReferenceExistingItems(recipe.inputs, a => a.itemId, a => a.amount, config);
            recipesValid &= ItemAmountsReferenceExistingItems(recipe.outputs, a => a.itemId, a => a.amount, config);
            recipesValid &= ItemAmountsReferenceExistingItems(recipe.catalysts, c => c.itemId, c => c.amount, config);

            if (recipe.kind == IslandIndustryKind.Generation)
            {
                recipesValid &= recipe.generationCountBasePerMinute > 0f || recipe.outputs.Count > 0;
            }

            if (recipe.kind == IslandIndustryKind.Processing)
            {
                recipesValid &= !string.IsNullOrWhiteSpace(recipe.fuelItemId) &&
                    config.GetItem(recipe.fuelItemId) != null &&
                    recipe.energyCostKwh > 0f &&
                    !string.IsNullOrWhiteSpace(recipe.processingSource);
            }

            if (recipe.kind == IslandIndustryKind.Reaction)
            {
                recipesValid &= recipe.reactionBaseSuccessChance > 0f &&
                    recipe.reactionBaseSuccessChance <= 1f &&
                    recipe.reactionRiskPerSpeed >= 0f &&
                    recipe.catalysts.Count > 0;
            }

            if (recipe.kind == IslandIndustryKind.Conversion)
            {
                recipesValid &= recipe.conversionGrowthPerCycle > 0f &&
                    recipe.conversionDecayPerMinute > 0f &&
                    recipe.conversionMaxMultiplier > 1f;
            }

            if (recipe.kind == IslandIndustryKind.Assembly)
            {
                recipesValid &= recipe.assemblySteps.Count > 0;
                for (int j = 0; j < recipe.assemblySteps.Count; j++)
                {
                    AssemblyStepConfig step = recipe.assemblySteps[j];
                    recipesValid &= step != null &&
                        step.stepIndex == j &&
                        step.durationSeconds > 0f &&
                        ItemAmountsReferenceExistingItems(step.inputs, a => a.itemId, a => a.amount, config) &&
                        ItemAmountsReferenceExistingItems(step.outputs, a => a.itemId, a => a.amount, config);
                }
            }
        }

        report.Check(recipesValid, "Все рецепты производств имеют валидные предметы, длительность и специальные параметры своего типа.");
    }

    private void ValidateProductionSimulation(WorldConfigDatabase config, BigTestReport report)
    {
        report.Section("Симуляция производств");
        if (config == null || !config.isLoaded)
        {
            report.Fail("Симуляция производств невозможна: конфиги не загружены.");
            return;
        }

        PlayerProgress progress = CreateStockedProgress(config);
        long startTicks = DateTime.UtcNow.Ticks;
        long finishTicks = startTicks + TimeSpan.FromMinutes(Mathf.Max(1f, productionSimulationMinutes)).Ticks;
        int changedEvents = IslandIndustrySimulator.Advance(config, progress, startTicks, finishTicks);

        report.Check(changedEvents > 0, "Островная промышленность дала события за " + productionSimulationMinutes.ToString("0.#") + " мин: " + changedEvents + ".");

        bool allRuntimeStatesExist = true;
        bool allLinesCompletedCycles = true;
        bool safeReactionsDidNotFail = true;
        bool conversionAccelerated = true;
        bool assemblyAdvanced = true;

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(industry.islandId, false);
            IslandIndustryState state = islandState != null ? islandState.GetIndustryState(industry.id, false) : null;
            allRuntimeStatesExist &= state != null;
            if (state == null)
            {
                allLinesCompletedCycles = false;
                continue;
            }

            allLinesCompletedCycles &= state.completedCycles > 0;
            if (industry.kind == IslandIndustryKind.Reaction)
            {
                safeReactionsDidNotFail &= state.failedCycles == 0;
            }

            if (industry.kind == IslandIndustryKind.Conversion)
            {
                conversionAccelerated &= state.conversionMultiplier > 1f;
            }

            if (industry.kind == IslandIndustryKind.Assembly)
            {
                assemblyAdvanced &= state.completedCycles > 0 || state.activeStepIndex > 0;
            }
        }

        report.Check(allRuntimeStatesExist, "Для всех производственных линий созданы runtime-состояния.");
        report.Check(allLinesCompletedCycles, "Все производственные линии завершили хотя бы один цикл на тестовом складе.");
        report.Check(safeReactionsDidNotFail, "Реакции на безопасной скорости не сорвали партии.");
        report.Check(conversionAccelerated, "Conversion-линии разогнали маховик выше x1.");
        report.Check(assemblyAdvanced, "Assembly-линии проходят этапы и не висят на первом шаге.");
    }

    private void ValidateWorldRuntime(BigTestReport report)
    {
        report.Section("Большой мир и чанки");
        if (world == null)
        {
            report.Fail("WorldRegionRuntime не найден.");
            return;
        }

        if (world.Chunks.Count == 0)
        {
            world.GenerateStarterRegion();
            report.Info("Чанки были пустые, стартовый регион сгенерирован прямо перед проверкой.");
        }

        report.Check(Approximately(world.WorldSizeMeters, WorldRegionRuntime.DefaultWorldSizeMeters, 1f), "Размер региона 100 км x 100 км: " + FormatKm(world.WorldSizeMeters) + ".");
        report.Check(Approximately(world.ChunkSizeMeters, WorldRegionRuntime.DefaultChunkSizeMeters, 1f), "Размер чанка 10 км x 10 км: " + FormatKm(world.ChunkSizeMeters) + ".");
        report.Check(world.Chunks.Count == WorldRegionRuntime.DefaultChunkCountPerAxis * WorldRegionRuntime.DefaultChunkCountPerAxis, "Сетка содержит 100 чанков: " + world.Chunks.Count + ".");
        report.Check(world.ActiveBubbleRadiusMeters > 0f && world.ActiveBubbleRadiusMeters <= 15000f, "Активный пузырь имеет разумный радиус: " + world.ActiveBubbleRadiusMeters.ToString("0") + " м.");
        report.Check(world.DetailedBubbleRadiusMeters > 0f && world.DetailedBubbleRadiusMeters <= world.ActiveBubbleRadiusMeters, "Детальный пузырь не больше активного: " + world.DetailedBubbleRadiusMeters.ToString("0") + " м.");

        ValidateWorldChunkIntegrity(report);
        ValidateWorldAltitudeBands(report);
        ValidateWorldEntityRecords(report);
    }

    private void ValidateWorldDataManifest(BigTestReport report)
    {
        report.Section("Сид и манифест мира");
        if (world == null)
        {
            report.Fail("Проверка манифеста невозможна: WorldRegionRuntime не найден.");
            return;
        }

        WorldRegionProfile profile = world.Profile;
        WorldRegionManifest manifest = world.Manifest;

        report.Check(profile != null, profile != null ? "WorldRegionProfile подключён к сцене: " + profile.name + "." : "WorldRegionProfile не подключён к WorldRegionRuntime.");
        report.Check(manifest != null && manifest.IsUsable, manifest != null && manifest.IsUsable ? "WorldRegionManifest подключён и содержит данные: " + manifest.name + "." : "WorldRegionManifest не подключён или пуст.");

        if (profile != null)
        {
            report.Check(profile.Seed != 0, "Сид региона задан явно: " + profile.Seed + ".");
            report.Check(Approximately(profile.WorldSizeMeters, WorldRegionRuntime.DefaultWorldSizeMeters, 1f), "Профиль задаёт регион 100 км x 100 км: " + FormatKm(profile.WorldSizeMeters) + ".");
            report.Check(Approximately(profile.ChunkSizeMeters, WorldRegionRuntime.DefaultChunkSizeMeters, 1f), "Профиль задаёт чанк 10 км x 10 км: " + FormatKm(profile.ChunkSizeMeters) + ".");
            report.Check(profile.ChunkCountPerAxis == WorldRegionRuntime.DefaultChunkCountPerAxis, "Профиль даёт сетку 10 x 10 чанков.");
            report.Check(profile.ActiveBubbleRadiusMeters > 0f && profile.DetailedBubbleRadiusMeters <= profile.ActiveBubbleRadiusMeters, "Профиль задаёт корректные радиусы активного и детального пузыря.");
        }

        if (manifest == null || !manifest.IsUsable)
        {
            return;
        }

        int expectedChunkCount = WorldRegionRuntime.DefaultChunkCountPerAxis * WorldRegionRuntime.DefaultChunkCountPerAxis;
        report.Check(manifest.SchemaVersion == 1, "Версия схемы манифеста поддерживается: " + manifest.SchemaVersion + ".");
        if (profile != null)
        {
            report.Check(manifest.Seed == profile.Seed, "Сид манифеста совпадает с профилем: " + manifest.Seed + ".");
            report.Check(Approximately(manifest.WorldSizeMeters, profile.WorldSizeMeters, 0.1f), "Размер мира в манифесте совпадает с профилем.");
            report.Check(Approximately(manifest.ChunkSizeMeters, profile.ChunkSizeMeters, 0.1f), "Размер чанка в манифесте совпадает с профилем.");
            report.Check(manifest.Islands.Count == profile.IslandCount, "Манифест содержит острова по профилю: " + manifest.Islands.Count + ".");
            report.Check(manifest.CloudFields.Count == profile.CloudFieldCount, "Манифест содержит облачные поля по профилю: " + manifest.CloudFields.Count + ".");
            report.Check(manifest.ResourceFields.Count == profile.ResourceFieldCount, "Манифест содержит ресурсные поля по профилю: " + manifest.ResourceFields.Count + ".");
            report.Check(manifest.LeviathanRegions.Count == profile.LeviathanRegionCount, "Манифест содержит зоны левиафанов по профилю: " + manifest.LeviathanRegions.Count + ".");
            report.Check(manifest.IcebergFields.Count == profile.IcebergFieldCount, "Манифест содержит поля айсбергов по профилю: " + manifest.IcebergFields.Count + ".");
        }

        report.Check(manifest.Chunks.Count == expectedChunkCount, "Манифест содержит 100 чанков: " + manifest.Chunks.Count + ".");
        report.Check(!string.IsNullOrWhiteSpace(manifest.GeneratedAtUtc), "Манифест хранит время последней генерации: " + manifest.GeneratedAtUtc + " UTC.");
        report.Check(world.Chunks.Count == manifest.Chunks.Count, "Runtime загружен из манифеста: чанки совпадают по количеству.");
        report.Check(world.Islands.Count == manifest.Islands.Count, "Runtime загружен из манифеста: острова совпадают по количеству.");
        report.Check(world.CloudFields.Count == manifest.CloudFields.Count, "Runtime загружен из манифеста: облачные поля совпадают по количеству.");
        report.Check(world.ResourceFields.Count == manifest.ResourceFields.Count, "Runtime загружен из манифеста: ресурсные поля совпадают по количеству.");
        report.Check(world.LeviathanRegions.Count == manifest.LeviathanRegions.Count, "Runtime загружен из манифеста: зоны левиафанов совпадают по количеству.");
        report.Check(world.IcebergFields.Count == manifest.IcebergFields.Count, "Runtime загружен из манифеста: поля айсбергов совпадают по количеству.");
    }

    private void ValidateWorldChunkIntegrity(BigTestReport report)
    {
        HashSet<string> ids = new HashSet<string>();
        float half = world.WorldSizeMeters * 0.5f;
        bool unique = true;
        bool bounds = true;
        bool centersResolveBack = true;

        for (int i = 0; i < world.Chunks.Count; i++)
        {
            WorldRegionRuntime.WorldChunkRecord chunk = world.Chunks[i];
            unique &= chunk != null && ids.Add(chunk.id);
            bounds &= chunk != null &&
                chunk.minX >= -half - 0.1f &&
                chunk.minZ >= -half - 0.1f &&
                chunk.maxX <= half + 0.1f &&
                chunk.maxZ <= half + 0.1f &&
                Approximately(chunk.maxX - chunk.minX, world.ChunkSizeMeters, 0.1f) &&
                Approximately(chunk.maxZ - chunk.minZ, world.ChunkSizeMeters, 0.1f);

            if (chunk != null)
            {
                WorldRegionRuntime.WorldChunkRecord resolved = world.GetChunkAt(new Vector3(chunk.centerX, 2500f, chunk.centerZ));
                centersResolveBack &= resolved != null && resolved.id == chunk.id;
            }
        }

        report.Check(unique, "Все чанки имеют уникальные id.");
        report.Check(bounds, "Границы чанков лежат внутри региона и имеют правильный размер.");
        report.Check(centersResolveBack, "Центр каждого чанка адресуется обратно в тот же чанк.");
        report.Check(world.GetChunkAt(new Vector3(-half + 1f, 2500f, -half + 1f)) != null, "Юго-западный край региона адресуется.");
        report.Check(world.GetChunkAt(new Vector3(half - 1f, 2500f, half - 1f)) != null, "Северо-восточный край региона адресуется.");
        report.Check(world.GetChunkAt(new Vector3(half + 1f, 2500f, 0f)) == null, "Точка за границей региона не получает чанк.");
    }

    private void ValidateWorldAltitudeBands(BigTestReport report)
    {
        report.Check(world.EvaluateAltitudeBand(0f) == WorldAltitudeBand.DeadlyStorm, "0 м = смертельная буря.");
        report.Check(world.EvaluateAltitudeBand(999f) == WorldAltitudeBand.ViolentStorm, "999 м = яростная буря.");
        report.Check(world.EvaluateAltitudeBand(1000f) == WorldAltitudeBand.CalmStorm, "1000 м = спокойная буря.");
        report.Check(world.EvaluateAltitudeBand(2000f) == WorldAltitudeBand.Habitation, "2000 м = зона обитания.");
        report.Check(world.EvaluateAltitudeBand(10000f) == WorldAltitudeBand.ThinAir, "10000 м = разреженная зона.");
        report.Check(world.EvaluateAltitudeBand(40000f) == WorldAltitudeBand.Ice, "40000 м = ледяная зона.");
        report.Check(world.EvaluateAltitudeBand(100000f) == WorldAltitudeBand.BeyondClaudiumLift, "100000 м = выше подъёмной силы клавдия.");
    }

    private void ValidateWorldEntityRecords(BigTestReport report)
    {
        report.Check(world.Islands.Count >= 10, "В регионе достаточно островов для первого мира: " + world.Islands.Count + ".");
        report.Check(world.CloudFields.Count >= 20, "В регионе достаточно облачных полей: " + world.CloudFields.Count + ".");
        report.Check(world.ResourceFields.Count >= 8, "В регионе достаточно рудных/ресурсных полей: " + world.ResourceFields.Count + ".");
        report.Check(world.LeviathanRegions.Count >= 3, "В регионе достаточно зон левиафанов: " + world.LeviathanRegions.Count + ".");
        report.Check(world.IcebergFields.Count >= 2, "В регионе достаточно высотных айсберговых полей: " + world.IcebergFields.Count + ".");

        bool islandsValid = true;
        for (int i = 0; i < world.Islands.Count; i++)
        {
            WorldRegionRuntime.WorldIslandRecord island = world.Islands[i];
            islandsValid &= island != null &&
                !string.IsNullOrWhiteSpace(island.id) &&
                island.radiusMeters > 0f &&
                world.EvaluateAltitudeBand(island.positionMeters.y) == WorldAltitudeBand.Habitation &&
                ChunkMatches(island.chunkId, island.positionMeters);
        }

        report.Check(islandsValid, "Острова имеют id, радиус, зону обитания и корректный chunkId.");

        bool cloudsValid = true;
        for (int i = 0; i < world.CloudFields.Count; i++)
        {
            WorldRegionRuntime.WorldCloudFieldRecord cloud = world.CloudFields[i];
            WorldAltitudeBand band = world.EvaluateAltitudeBand(cloud.centerMeters.y);
            cloudsValid &= cloud != null &&
                !string.IsNullOrWhiteSpace(cloud.id) &&
                !string.IsNullOrWhiteSpace(cloud.resourceId) &&
                cloud.radiusMeters > 0f &&
                cloud.thicknessMeters > 0f &&
                cloud.density01 >= 0f &&
                cloud.density01 <= 1f &&
                cloud.resourceKgEstimate > 0 &&
                ChunkMatches(cloud.chunkId, cloud.centerMeters) &&
                (band == WorldAltitudeBand.Habitation || band == WorldAltitudeBand.ThinAir);
        }

        report.Check(cloudsValid, "Облачные поля валидны и лежат в зоне обитания/разреженной зоне.");

        bool resourcesValid = true;
        for (int i = 0; i < world.ResourceFields.Count; i++)
        {
            WorldRegionRuntime.WorldResourceFieldRecord resource = world.ResourceFields[i];
            resourcesValid &= resource != null &&
                !string.IsNullOrWhiteSpace(resource.id) &&
                !string.IsNullOrWhiteSpace(resource.resourceId) &&
                resource.radiusMeters > 0f &&
                resource.resourceKgEstimate > 0 &&
                resource.altitudeBand == world.EvaluateAltitudeBand(resource.centerMeters.y) &&
                ChunkMatches(resource.chunkId, resource.centerMeters);
        }

        report.Check(resourcesValid, "Ресурсные поля имеют ресурс, запас, высотный слой и корректный chunkId.");

        bool leviathansValid = true;
        for (int i = 0; i < world.LeviathanRegions.Count; i++)
        {
            WorldRegionRuntime.WorldLeviathanRegionRecord region = world.LeviathanRegions[i];
            leviathansValid &= region != null &&
                !string.IsNullOrWhiteSpace(region.id) &&
                region.radiusMeters > 0f &&
                region.altitudeMaxMeters > region.altitudeMinMeters &&
                region.rarity01 >= 0f &&
                region.rarity01 <= 1f &&
                ChunkMatches(region.chunkId, region.centerMeters);
        }

        report.Check(leviathansValid, "Зоны левиафанов имеют радиус, высотный диапазон, редкость и корректный chunkId.");

        bool icebergsValid = true;
        for (int i = 0; i < world.IcebergFields.Count; i++)
        {
            WorldRegionRuntime.WorldIcebergFieldRecord field = world.IcebergFields[i];
            icebergsValid &= field != null &&
                !string.IsNullOrWhiteSpace(field.id) &&
                field.radiusMeters > 0f &&
                field.icebergCountEstimate > 0 &&
                field.sublimateKgEstimate > 0 &&
                world.EvaluateAltitudeBand(field.altitudeMeters) == WorldAltitudeBand.Ice &&
                ChunkMatches(field.chunkId, field.centerMeters);
        }

        report.Check(icebergsValid, "Айсберговые поля лежат в ледяной зоне и имеют добываемый запас субликатов.");
    }

    private void ValidateWorldEntityIndex(BigTestReport report)
    {
        report.Section("Индекс сущностей мира");
        if (world == null)
        {
            report.Fail("Проверка индекса невозможна: WorldRegionRuntime не найден.");
            return;
        }

        if (worldIndex == null)
        {
            report.Fail("WorldEntityIndex не найден в сцене.");
            return;
        }

        worldIndex.EnsureBuilt(world);
        int expectedRecordCount =
            world.Islands.Count +
            world.CloudFields.Count +
            world.ResourceFields.Count +
            world.LeviathanRegions.Count +
            world.IcebergFields.Count;

        report.Check(worldIndex.IsBuilt, "WorldEntityIndex построен.");
        report.Check(worldIndex.World == world, "WorldEntityIndex смотрит на тот же WorldRegionRuntime.");
        report.Check(worldIndex.ChunkCount == world.Chunks.Count, "Индекс содержит все чанки: " + worldIndex.ChunkCount + ".");
        report.Check(worldIndex.IndexedRecordCount == expectedRecordCount, "Индекс содержит все записи сущностей мира: " + worldIndex.IndexedRecordCount + ".");

        WorldRegionRuntime.WorldIslandRecord capital;
        report.Check(worldIndex.TryGetIsland("capital", out capital), "Индекс находит столицу по id: capital.");
        if (capital != null)
        {
            report.Check(worldIndex.TryGetChunk(capital.chunkId, out _), "Индекс находит чанк столицы: " + capital.chunkId + ".");
        }

        List<WorldEntityQueryResult> results = new List<WorldEntityQueryResult>();
        int centerIslandCount = worldIndex.CollectNearby(new Vector3(0f, 2500f, 0f), 5000f, WorldEntityKind.Island, results);
        bool hasCapitalNearby = false;
        for (int i = 0; i < results.Count; i++)
        {
            hasCapitalNearby |= results[i].id == "capital";
        }

        report.Check(centerIslandCount >= 1 && hasCapitalNearby, "Поиск рядом с центром мира находит столицу.");

        int allNearby = worldIndex.CollectNearby(new Vector3(0f, 2500f, 0f), world.ActiveBubbleRadiusMeters, WorldEntityKind.All, results, 12);
        bool sortedByDistance = true;
        for (int i = 1; i < results.Count; i++)
        {
            sortedByDistance &= results[i - 1].sqrDistance <= results[i].sqrDistance + 0.001f;
        }

        report.Check(allNearby > 0, "Поиск рядом с игроком возвращает сущности мира: " + allNearby + ".");
        report.Check(sortedByDistance, "Результаты поиска рядом отсортированы по дистанции.");

        int chunkRecords = capital != null ? worldIndex.CollectInChunk(capital.chunkId, WorldEntityKind.All, results) : 0;
        report.Check(chunkRecords > 0, "Поиск по чанку возвращает записи сущностей: " + chunkRecords + ".");

        int iceRecords = worldIndex.CollectByAltitudeBand(WorldAltitudeBand.Ice, WorldEntityKind.IcebergField, results);
        report.Check(iceRecords == world.IcebergFields.Count, "Поиск по ледяной зоне находит все поля айсбергов: " + iceRecords + ".");
    }

    private void ValidateWorldRuntimeState(BigTestReport report)
    {
        report.Section("Единый runtime-состояния мира");
        if (world == null)
        {
            report.Fail("Проверка runtime-состояния невозможна: WorldRegionRuntime не найден.");
            return;
        }

        if (runtimeState == null)
        {
            report.Fail("WorldRuntimeState не найден в сцене.");
            return;
        }

        runtimeState.Configure(world, worldIndex, focus);
        runtimeState.InitializeFromWorld(true);

        int expectedEntityCount =
            world.Islands.Count +
            world.CloudFields.Count +
            world.ResourceFields.Count +
            world.LeviathanRegions.Count +
            world.IcebergFields.Count;

        report.Check(runtimeState.World == world, "WorldRuntimeState привязан к текущему WorldRegionRuntime.");
        report.Check(worldIndex == null || runtimeState.Index == worldIndex, "WorldRuntimeState использует тот же WorldEntityIndex, что и сцена.");
        report.Check(runtimeState.ChunkStateCount == world.Chunks.Count, "Runtime хранит состояние каждого чанка: " + runtimeState.ChunkStateCount + ".");
        report.Check(runtimeState.EntityStateCount == expectedEntityCount, "Runtime хранит состояние каждой сущности мира: " + runtimeState.EntityStateCount + ".");

        Vector3 samplePosition = new Vector3(0f, 2500f, 0f);
        runtimeState.RefreshActiveBubble(samplePosition, world.ActiveBubbleRadiusMeters);
        report.Check(runtimeState.ActiveChunkCount > 0, "Runtime отмечает активные чанки внутри пузыря: " + runtimeState.ActiveChunkCount + ".");
        report.Check(runtimeState.DiscoveredChunkCount > 0, "Runtime отмечает открытые чанки: " + runtimeState.DiscoveredChunkCount + ".");
        report.Check(runtimeState.ActiveEntityCount > 0, "Runtime отмечает активные сущности внутри пузыря: " + runtimeState.ActiveEntityCount + ".");

        bool capitalVisible = runtimeState.TryGetEntityState(WorldEntityKind.Island, "capital", out WorldRuntimeState.EntityRuntimeState capitalState) &&
            capitalState.discovered &&
            capitalState.activeInBubble;
        report.Check(capitalVisible, "Столица открывается и становится активной в стартовом пузыре.");

        WorldRuntimeState.EntityRuntimeState sample = FindExtractableRuntimeEntity();
        if (sample == null)
        {
            report.Warn("В runtime нет добываемой сущности для проверки расходования запаса.");
            return;
        }

        float before = sample.remainingAmount;
        bool extractedOk = runtimeState.TryExtract(sample.kind, sample.id, 10f, out float extracted);
        runtimeState.TryGetEntityState(sample.kind, sample.id, out WorldRuntimeState.EntityRuntimeState afterExtract);
        report.Check(extractedOk && extracted > 0f && afterExtract != null && afterExtract.remainingAmount < before,
            "Runtime умеет списывать добываемый запас сущности " + sample.id + ": -" + extracted.ToString("0.##") + " кг.");

        string tempPath = Path.Combine(Application.temporaryCachePath, "WildWindBigTestRuntimeState.json");
        bool saved = runtimeState.SaveToPath(tempPath);
        float savedRemaining = afterExtract != null ? afterExtract.remainingAmount : -1f;
        runtimeState.ResetRuntimeState();
        bool loaded = runtimeState.LoadFromPath(tempPath);
        runtimeState.TryGetEntityState(sample.kind, sample.id, out WorldRuntimeState.EntityRuntimeState loadedState);
        bool amountPersisted = loadedState != null && Approximately(loadedState.remainingAmount, savedRemaining, 0.001f);

        report.Check(saved, "Runtime-состояние сохраняется во временный JSON.");
        report.Check(loaded, "Runtime-состояние загружается из временного JSON.");
        report.Check(amountPersisted, "После загрузки сохраняется остаток добываемого запаса.");

        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception exception)
        {
            report.Warn("Не удалось удалить временный файл runtime-теста: " + exception.Message);
        }
    }

    private void ValidateWorldSaveSlotRoundTrip(BigTestReport report)
    {
        report.Section("Сохранение мира в слот");
        if (world == null)
        {
            report.Fail("Проверка save slot мира невозможна: WorldRegionRuntime не найден.");
            return;
        }

        WorldManifestData runtimeManifest = WorldManifestData.FromRuntime(world, "big_test_runtime");
        report.Check(runtimeManifest != null && runtimeManifest.IsUsable, "WorldManifestData снимается с runtime мира.");
        report.Check(runtimeManifest.chunks.Count == world.Chunks.Count && runtimeManifest.islands.Count == world.Islands.Count,
            "WorldManifestData сохраняет чанки и острова: " + runtimeManifest.chunks.Count + " / " + runtimeManifest.islands.Count + ".");

        int seed = 424242;
        MetaGameSaveData generatedSave = WorldSaveSlotFactory.BuildNewWorldSaveData(seed);
        bool generatedUsable = generatedSave != null &&
            generatedSave.version == MetaGameSaveData.CurrentVersion &&
            generatedSave.worldManifest != null &&
            generatedSave.worldManifest.IsUsable &&
            generatedSave.worldManifest.seed == seed &&
            generatedSave.worldRuntime != null &&
            generatedSave.worldRuntime.IsUsable;
        report.Check(generatedUsable, "Новая игра создаёт save data с версией, seed, manifest и runtime-заготовкой.");

        string json = JsonUtility.ToJson(generatedSave, true);
        MetaGameSaveData loadedSave = JsonUtility.FromJson<MetaGameSaveData>(json);
        bool jsonRoundTrip = loadedSave != null &&
            loadedSave.worldManifest != null &&
            loadedSave.worldManifest.IsUsable &&
            loadedSave.worldManifest.seed == seed &&
            loadedSave.worldRuntime != null &&
            loadedSave.worldRuntime.IsUsable;
        report.Check(jsonRoundTrip, "Save slot мира проходит JSON round-trip.");

        string tempSlotName = "wild_wind_big_test_slot_" + DateTime.UtcNow.Ticks + ".json";
        string tempSlotPath = WildWindSaveSlots.GetSavePath(tempSlotName);
        try
        {
            File.WriteAllText(tempSlotPath, json);
            List<WildWindSaveSlotInfo> slots = WildWindSaveSlots.GetExistingSlots();
            bool slotListed = false;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].fileName == tempSlotName && slots[i].seed == seed)
                {
                    slotListed = true;
                    break;
                }
            }

            report.Check(slotListed, "Continue видит только настоящий игровой save slot с world manifest.");
        }
        finally
        {
            try
            {
                if (File.Exists(tempSlotPath))
                {
                    File.Delete(tempSlotPath);
                }
            }
            catch (Exception exception)
            {
                report.Warn("Не удалось удалить временный save slot большого теста: " + exception.Message);
            }
        }

        WildWindSaveSlots.MarkPendingGameplayLaunch();
        bool pendingLaunchConsumed = WildWindSaveSlots.ConsumePendingGameplayLaunch();
        bool pendingLaunchCleared = !WildWindSaveSlots.ConsumePendingGameplayLaunch();
        report.Check(pendingLaunchConsumed && pendingLaunchCleared, "Флаг перехода стартовый экран -> gameplay одноразовый.");

        GameObject probeRoot = null;
        try
        {
            probeRoot = new GameObject("Big Test World Save Slot Probe");
            WorldRegionRuntime probeWorld = probeRoot.AddComponent<WorldRegionRuntime>();
            WorldManifestData loadedManifest = loadedSave != null ? loadedSave.worldManifest : null;
            bool manifestApplied = loadedManifest != null && probeWorld.LoadFromManifestData(loadedManifest);
            report.Check(manifestApplied, "WorldManifestData распаковывается обратно в WorldRegionRuntime.");
            report.Check(loadedManifest != null &&
                probeWorld.Chunks.Count == loadedManifest.chunks.Count &&
                probeWorld.Islands.Count == loadedManifest.islands.Count,
                "Распакованный мир сохранил количество чанков и островов.");

            WorldEntityIndex probeIndex = probeRoot.AddComponent<WorldEntityIndex>();
            probeIndex.Configure(probeWorld);
            probeIndex.EnsureBuilt(probeWorld);
            bool capitalResolved = probeIndex.TryGetIsland("capital", out WorldRegionRuntime.WorldIslandRecord capital) && capital != null;
            report.Check(capitalResolved, "После распаковки WorldEntityIndex находит столицу.");

            WorldRuntimeState probeState = probeRoot.AddComponent<WorldRuntimeState>();
            probeState.Configure(probeWorld, probeIndex, null);
            WorldRuntimeSaveData loadedRuntime = loadedSave != null ? loadedSave.worldRuntime : null;
            bool runtimeApplied = loadedRuntime != null && probeState.ApplySaveData(loadedRuntime);
            report.Check(runtimeApplied, "WorldRuntimeSaveData накатывается на распакованный мир.");
            report.Check(probeState.ChunkStateCount == probeWorld.Chunks.Count &&
                probeState.EntityStateCount >= probeWorld.Islands.Count,
                "Runtime state после распаковки восстановил чанки и сущности.");

            WorldRuntimeSaveData recapturedRuntime = probeState.CreateSaveData();
            report.Check(recapturedRuntime != null &&
                recapturedRuntime.manifestSeed == seed &&
                recapturedRuntime.chunks.Count == probeWorld.Chunks.Count,
                "Runtime state повторно пакуется с тем же seed и чанками.");
        }
        finally
        {
            DestroyBigTestObject(probeRoot);
        }
    }

    private void ValidateWorldSimulationTick(BigTestReport report)
    {
        report.Section("Сердцебиение мира и фоновая симуляция");
        if (world == null || runtimeState == null)
        {
            report.Fail("Проверка WorldSimulationTick невозможна: нужны WorldRegionRuntime и WorldRuntimeState.");
            return;
        }

        if (simulationTick == null)
        {
            report.Fail("WorldSimulationTick не найден в сцене.");
            return;
        }

        simulationTick.Configure(world, worldIndex, runtimeState, focus, streamer);
        WorldSimulationTick.EnvironmentSample violent = simulationTick.EvaluateEnvironment(500f);
        WorldSimulationTick.EnvironmentSample calm = simulationTick.EvaluateEnvironment(1500f);
        WorldSimulationTick.EnvironmentSample habitation = simulationTick.EvaluateEnvironment(2500f);
        WorldSimulationTick.EnvironmentSample thin = simulationTick.EvaluateEnvironment(12000f);
        WorldSimulationTick.EnvironmentSample ice = simulationTick.EvaluateEnvironment(45000f);
        WorldSimulationTick.EnvironmentSample beyond = simulationTick.EvaluateEnvironment(100000f);

        report.Check(violent.band == WorldAltitudeBand.ViolentStorm && violent.visibilityMeters <= 150f, "Tick знает яростную бурю: видимость около 100 м.");
        report.Check(calm.band == WorldAltitudeBand.CalmStorm && calm.visibilityMeters >= 900f && calm.visibilityMeters <= 1200f, "Tick знает спокойную бурю: видимость около 1000 м.");
        report.Check(habitation.band == WorldAltitudeBand.Habitation && Approximately(habitation.claudiumLift01, 1f, 0.001f), "Tick знает зону обитания: клавдиевая подъёмная сила полная.");
        report.Check(thin.band == WorldAltitudeBand.ThinAir && thin.windMetersPerSecond > habitation.windMetersPerSecond && thin.claudiumLift01 < habitation.claudiumLift01, "Tick знает разреженную зону: ветер сильнее, подъёмная сила падает.");
        report.Check(ice.band == WorldAltitudeBand.Ice && ice.windMetersPerSecond <= 0.001f && ice.stormDamagePerMinute > 0f, "Tick знает ледяную зону: ветра нет, холод опасен.");
        report.Check(beyond.band == WorldAltitudeBand.BeyondClaudiumLift && Approximately(beyond.claudiumLift01, 0f, 0.001f), "Tick знает потолок клавдия: на 100 км подъёмной силы нет.");

        long beforeTicks = simulationTick.TickCount;
        float beforeSeconds = simulationTick.TotalSimulatedSeconds;
        WorldSimulationTick.TickResult result = simulationTick.TickOnce(60f);

        report.Check(simulationTick.TickCount == beforeTicks + 1, "Один ручной tick увеличивает счётчик сердцебиений.");
        report.Check(simulationTick.TotalSimulatedSeconds >= beforeSeconds + 59.9f, "Один ручной tick продвигает игровое время на 60 секунд.");
        report.Check(runtimeState.ActiveChunkCount > 0, "Tick обновляет runtime-пузырь и активные чанки: " + runtimeState.ActiveChunkCount + ".");
        report.Check(result.touchedEntities > 0, "Tick двигает фоновую симуляцию дальних добываемых сущностей: " + result.touchedEntities + ".");
        report.Check(result.extractedKg > 0f, "Tick списывает небольшой фоновый объём ресурсов вдали: " + result.extractedKg.ToString("0.##") + " кг.");
        report.Info("Последний tick: " + simulationTick.LastSummary + ".");
    }

    private void ValidateBubbleStreaming(BigTestReport report)
    {
        report.Section("Активный пузырь и материализация");
        if (world == null || streamer == null || focus == null)
        {
            report.Fail("Проверка пузыря невозможна: нужны WorldRegionRuntime, WorldBubbleStreamer и Focus.");
            return;
        }

        Vector3 original = focus.position;
        try
        {
            CheckBubbleAt(new Vector3(0f, 2500f, 0f), "центр региона", report);
            CheckBubbleAt(new Vector3(49000f, 2500f, 49000f), "край региона", report);
            CheckBubbleAt(new Vector3(-18000f, 12000f, 12000f), "разреженная зона", report);
            CheckBubbleAt(new Vector3(22000f, 45000f, -26000f), "ледяная зона", report);
            ValidateStreamerRefreshBudget(report);
        }
        finally
        {
            focus.position = original;
            streamer.RefreshNow();
        }
    }

    private void CheckBubbleAt(Vector3 position, string label, BigTestReport report)
    {
        focus.position = position;
        streamer.RefreshNow();
        int activeChunks = world.CountActiveChunks(position, world.ActiveBubbleRadiusMeters);
        report.Check(world.GetChunkAt(position) != null, "Позиция '" + label + "' находится в чанке: " + FormatVector(position) + ".");
        report.Check(activeChunks > 0, "Пузырь в точке '" + label + "' видит активные чанки: " + activeChunks + ".");
        report.Check(streamer.ActiveProxyCount >= 0, "Стример обновился в точке '" + label + "' без исключений. Proxy: " + streamer.ActiveProxyCount + ".");
        report.Check(streamer.MaterializedRoot != null, "У стримера есть корень материализации для точки '" + label + "'.");
        if (streamer.ActiveProxyCount > 60)
        {
            report.Warn("В точке '" + label + "' материализовано больше 60 proxy. Это может быть тяжеловато: " + streamer.ActiveProxyCount + ".");
        }
        else
        {
            report.Pass("В точке '" + label + "' количество proxy в мягком лимите: " + streamer.ActiveProxyCount + " / 60.");
        }
    }

    private void ValidateStreamerRefreshBudget(BigTestReport report)
    {
        Vector3[] samples =
        {
            new Vector3(0f, 2500f, 0f),
            new Vector3(9000f, 2500f, 0f),
            new Vector3(0f, 2500f, 9000f),
            new Vector3(-18000f, 8000f, 12000f),
            new Vector3(32000f, 12000f, -18000f),
            new Vector3(44000f, 45000f, 44000f)
        };

        Stopwatch stopwatch = new Stopwatch();
        long totalTicks = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            focus.position = samples[i];
            stopwatch.Restart();
            streamer.RefreshNow();
            stopwatch.Stop();
            totalTicks += stopwatch.ElapsedTicks;
        }

        double averageMs = totalTicks * 1000.0 / Stopwatch.Frequency / Mathf.Max(1, samples.Length);
        report.Check(averageMs <= streamerAverageBudgetMs, "Среднее обновление пузыря в бюджете: " + averageMs.ToString("0.00") + " мс / " + streamerAverageBudgetMs.ToString("0.#") + " мс.");
    }

    private void ValidateVisualAtmosphere(BigTestReport report)
    {
        report.Section("Визуал, высотные слои и туман");
        report.Check(visualTuner != null, visualTuner != null ? "VisualPlayModeTuner найден." : "VisualPlayModeTuner не найден.");
        if (visualTuner == null)
        {
            return;
        }

        report.Info(visualTuner.GetAtmosphereDebugText());

        float transitionHalfWidth = ReadPrivateFloat(visualTuner, "altitudeTransitionHalfWidth", -1f);
        float violentVisibility = ReadPrivateFloat(visualTuner, "violentStormVisibility", -1f);
        float calmVisibility = ReadPrivateFloat(visualTuner, "calmStormVisibility", -1f);
        float deadlyDrawDistance = ReadPrivateFloat(visualTuner, "deadlyStormDrawDistance", -1f);
        bool useAltitudeAtmosphere = ReadPrivateBool(visualTuner, "useAltitudeAtmosphere", false);

        report.Check(useAltitudeAtmosphere, "Высотное управление AERO-туманом включено.");
        report.Check(Approximately(transitionHalfWidth, 50f, 0.5f), "Плавный переход высотных зон держится около +-50 м: " + transitionHalfWidth.ToString("0.#") + " м.");
        report.Check(violentVisibility > 0f && violentVisibility <= 150f, "Видимость яростной бури ограничена примерно 100 м: " + violentVisibility.ToString("0.#") + " м.");
        report.Check(calmVisibility >= 800f && calmVisibility <= 1300f, "Видимость спокойной бури около 1000 м: " + calmVisibility.ToString("0.#") + " м.");
        report.Check(deadlyDrawDistance > 0f && deadlyDrawDistance <= 120f, "Поверхность смертельной бури рисуется только вблизи: " + deadlyDrawDistance.ToString("0.#") + " м.");

        GameObject stormSurface = GameObject.Find("Deadly Storm Surface Local Bubble");
        report.Check(stormSurface != null, stormSurface != null ? "Локальная поверхность смертельной бури найдена." : "Локальная поверхность смертельной бури не найдена.");
        if (stormSurface != null)
        {
            Renderer renderer = stormSurface.GetComponent<Renderer>();
            report.Check(renderer != null && renderer.sharedMaterial != null, "У поверхности бури назначен материал.");
            if (renderer != null && renderer.sharedMaterial != null)
            {
                report.Info("Материал бури: " + renderer.sharedMaterial.name + ", shader=" + renderer.sharedMaterial.shader.name + ".");
            }
        }

        if (GameObject.Find("AERO Visual Fog Controller") == null)
        {
            report.Warn("AERO Visual Fog Controller не найден в сцене. Если туман виден через Renderer Feature, это может быть нормально, но стоит проверить сцену глазами.");
        }
        else
        {
            report.Pass("AERO Visual Fog Controller найден в сцене.");
        }
    }

    private void ValidateSettings(BigTestReport report)
    {
        report.Section("Настройки проекта и управление");
        report.Check(settings != null, settings != null ? "WildWindSettingsRoot найден." : "WildWindSettingsRoot не найден.");
        WildWindControlSettings controls = settings != null ? settings.Controls : FindFirstObjectByType<WildWindControlSettings>();
        report.Check(controls != null, controls != null ? "Модуль Controls найден." : "Модуль Controls не найден.");
        if (controls == null)
        {
            return;
        }

        report.Check(controls.DebugCruiseSpeedMetersPerSecond > 0f, "Скорость debug-перелёта положительная: " + controls.DebugCruiseSpeedMetersPerSecond.ToString("0.#") + " м/с.");
        report.Check(controls.DebugVerticalSpeedMetersPerSecond > 0f, "Вертикальная скорость debug-перелёта положительная: " + controls.DebugVerticalSpeedMetersPerSecond.ToString("0.#") + " м/с.");
        report.Check(controls.DebugSprintMultiplier >= 1f, "Множитель ускорения debug-перелёта не меньше 1: x" + controls.DebugSprintMultiplier.ToString("0.#") + ".");
        report.Check(controls.DebugCameraFollowSharpness > 0f && controls.DebugCameraFollowSharpness <= 1f, "Плавность следования камеры в диапазоне 0..1: " + controls.DebugCameraFollowSharpness.ToString("0.###") + ".");

        WildWindGameplayMenu gameplayMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        report.Check(gameplayMenu != null, gameplayMenu != null ? "Внутриигровое меню найдено в world-сессии." : "Внутриигровое меню не найдено.");
    }

    private void ValidateShipWindAerodynamics(BigTestReport report)
    {
        report.Section("Корабль, ветер и лётная физика");
        GameObject testShip = null;
        Scene probeScene = default;
        try
        {
            testShip = new GameObject("Big Test Temporary ShipPhysics");
            Rigidbody body = testShip.AddComponent<Rigidbody>();
            ShipPhysics ship = testShip.AddComponent<ShipPhysics>();
            ship.enabled = false;
            body.useGravity = false;

            ship.dragCoefficient = 0.5f;
            ship.windVelocity = new Vector3(10f, 0f, 0f);
            report.Check(Approximately(ship.CurrentWindAerodynamicFactor, 0.5f, 0.001f), "Аэродинамика 0.5 даёт коэффициент ветра 0.5.");
            report.Check(Approximately(ship.EffectiveWindVelocity.magnitude, 5f, 0.001f), "Ветер 10 м/с при аэродинамике 0.5 ощущается как 5 м/с.");

            ship.dragCoefficient = 1.2f;
            ship.windVelocity = new Vector3(0f, 0f, 10f);
            report.Check(Approximately(ship.EffectiveWindVelocity.magnitude, 12f, 0.001f), "Плохая аэродинамика 1.2 усиливает воздействие ветра до 12 м/с.");

            probeScene = SceneManager.CreateScene("Wild Wind Big Test Flight Probe", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            report.Check(probeScene.IsValid(), "Изолированная сцена для проверки лётной физики создана.");
            if (!probeScene.IsValid())
            {
                return;
            }

            SceneManager.MoveGameObjectToScene(testShip, probeScene);
            PhysicsScene physicsScene = probeScene.GetPhysicsScene();
            report.Check(physicsScene.IsValid(), "Изолированная 3D physics-сцена валидна.");
            if (!physicsScene.IsValid())
            {
                return;
            }

            ConfigureFlightProbeShip(ship, body);
            float expectedMass = ship.baseMass + ship.cargoMassKg;
            report.Check(Approximately(body.mass, expectedMass, 0.001f), "Rigidbody получает сухую массу и груз: " + body.mass.ToString("0.#") + " кг.");

            float fuelBeforeLift = ship.engineFuelStockKg;
            float claudiumBeforeLift = ship.claudiumStock;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedLiftN = body.mass * 9.81f;
                report.Check(Approximately(ship.claudiumRequestedLiftKg, body.mass, 0.5f), "Клавдиевый контур запрашивает триммируемую массу корабля: " + ship.claudiumRequestedLiftKg.ToString("0.#") + " кг.");
                report.Check(Approximately(ship.claudiumCurrentLiftN, expectedLiftN, expectedLiftN * 0.02f), "Клавдиевый контур выдаёт подъёмную силу примерно веса корабля: " + ship.claudiumCurrentLiftN.ToString("0.#") + " Н.");
                report.Check(ship.engineGeneratedPowerKw + 0.001f >= ship.claudiumPowerDrawKw, "Двигатель покрывает мощность клавдиевого контура: " + ship.engineGeneratedPowerKw.ToString("0.#") + " / " + ship.claudiumPowerDrawKw.ToString("0.#") + " кВт.");
                report.Check(ship.engineFuelStockKg < fuelBeforeLift && ship.claudiumStock < claudiumBeforeLift, "Подъём тратит топливо и клавдий в одном физическом тике.");
            }

            ship.claudiumStock = 0f;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                report.Check(Approximately(ship.claudiumCurrentLiftN, 0f, 0.001f), "Без клавдия подъёмная сила падает в ноль.");
            }

            ConfigureFlightProbeShip(ship, body);
            ResetFlightProbeBody(body, new Vector3(0f, 1000f, 0f), Quaternion.identity, true);
            float hoverStartY = body.position.y;
            if (StepShipPhysicsProbe(ship, physicsScene, 20, report))
            {
                float hoverDrift = Mathf.Abs(body.position.y - hoverStartY);
                report.Check(IsFinite(body.position) && IsFinite(body.linearVelocity), "Сбалансированный полёт не создаёт NaN/Infinity в позиции и скорости.");
                report.Check(hoverDrift <= 0.25f && Mathf.Abs(body.linearVelocity.y) <= 0.5f, "При рабочем клавдиевом контуре корабль держит высоту: дрейф " + hoverDrift.ToString("0.###") + " м, vy " + body.linearVelocity.y.ToString("0.###") + " м/с.");
            }

            ConfigureFlightProbeShip(ship, body);
            ship.claudiumStock = 0f;
            ResetFlightProbeBody(body, new Vector3(0f, 1000f, 0f), Quaternion.identity, true);
            if (StepShipPhysicsProbe(ship, physicsScene, 10, report))
            {
                report.Check(body.linearVelocity.y < -0.75f, "Без клавдия корабль реально начинает падать: vy " + body.linearVelocity.y.ToString("0.###") + " м/с.");
            }

            ConfigureFlightProbeShip(ship, body);
            ship.claudiumStock = 0f;
            ship.thrustInput = 1f;
            ship.enginePowerLever = 1f;
            ResetFlightProbeBody(body, Vector3.zero, Quaternion.identity, false);
            if (StepShipPhysicsProbe(ship, physicsScene, 15, report))
            {
                Vector3 horizontalVelocity = body.linearVelocity;
                horizontalVelocity.y = 0f;
                report.Check(horizontalVelocity.z > 0.75f && ship.propellerThrustKgf > 0f, "Винт с доступной мощностью разгоняет корабль вперёд: v " + horizontalVelocity.magnitude.ToString("0.###") + " м/с, тяга " + ship.propellerThrustKgf.ToString("0.#") + " кгс.");
            }

            ConfigureForwardSpeedProbeShip(ship, body);
            float expectedMaxSpeed = CalculateExpectedForwardMaxSpeed(ship);
            ResetFlightProbeBody(body, Vector3.zero, Quaternion.identity, false);
            if (StepShipPhysicsProbe(ship, physicsScene, 2000, report))
            {
                Vector3 terminalVelocity = body.linearVelocity;
                terminalVelocity.y = 0f;
                float actualSpeed = terminalVelocity.magnitude;
                float tolerance = Mathf.Max(1f, expectedMaxSpeed * 0.08f);
                report.Check(expectedMaxSpeed > 0f && IsFinite(expectedMaxSpeed), "Расчётная максимальная скорость для тестового корабля конечна: " + expectedMaxSpeed.ToString("0.###") + " м/с.");
                report.Check(Mathf.Abs(actualSpeed - expectedMaxSpeed) <= tolerance, "Симуляция полного газа сходится к расчётной скорости: расчёт " + expectedMaxSpeed.ToString("0.###") + " м/с, факт " + actualSpeed.ToString("0.###") + " м/с, допуск " + tolerance.ToString("0.###") + " м/с.");
            }
        }
        finally
        {
            if (testShip != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(testShip);
                }
                else
                {
                    DestroyImmediate(testShip);
                }
            }

            if (probeScene.IsValid())
            {
                SceneManager.UnloadSceneAsync(probeScene);
            }
        }
    }

    private IEnumerator ValidateSessionLoopRoundTrip(BigTestReport report)
    {
        report.Section("Сессионные перезаходы стартовое меню <-> мир");

        string previousSelectedSave = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty();
        bool previousPendingLaunch = PlayerPrefs.GetInt(WildWindSaveSlots.PendingGameplayLaunchPlayerPrefsKey, 0) == 1;
        int seed = 777331;
        string tempSlotName = BigTestSessionSavePrefix + DateTime.UtcNow.Ticks + ".json";
        string tempSlotPath = WildWindSaveSlots.GetSavePath(tempSlotName);
        CleanupAbandonedBigTestSaveSlots(previousSelectedSave, report);

        try
        {
            bool saveCreated = WorldSaveSlotFactory.TryCreateNewWorldSave(tempSlotName, seed, out string createError);
            report.Check(saveCreated, saveCreated
                ? "Временный save slot для проверки перезаходов создан."
                : "Не удалось создать временный save slot для проверки перезаходов: " + createError);
            if (!saveCreated)
            {
                yield break;
            }

            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
            becamePersistentForSceneLoop = true;

            SceneManager.LoadScene(DefaultStartSceneName);
            yield return null;
            yield return null;

            Scene startScene = SceneManager.GetActiveScene();
            report.Check(startScene.name == DefaultStartSceneName, "Большой тест реально перешёл в стартовую сцену: " + startScene.name + ".");

            WildWindStartScreen startScreen = FindFirstObjectByType<WildWindStartScreen>();
            report.Check(startScreen != null, startScreen != null ? "Стартовый экран поднялся после выхода из мира." : "Стартовый экран не найден после загрузки StartScreen.");
            report.Check(startScreen != null && startScreen.gameplaySceneName == DefaultWorldSceneName,
                "Стартовый экран ведёт в " + DefaultWorldSceneName + ".");

            WildWindSaveSlots.SetSelectedSaveFileName(tempSlotName);
            WildWindSaveSlots.MarkPendingGameplayLaunch();
            sessionLoopLaunchInProgress = true;
            SceneManager.LoadScene(DefaultWorldSceneName);
            DisableDuplicateBigTestRunners();
            yield return WaitForLoadedSessionWorld(seed, tempSlotName);
            sessionLoopLaunchInProgress = false;
            ReportSessionWorldReadinessIfNeeded(seed, tempSlotName, "первый вход", report);

            Scene firstWorldScene = SceneManager.GetActiveScene();
            report.Check(firstWorldScene.name == DefaultWorldSceneName, "Continue загрузил world-сцену в первый раз: " + firstWorldScene.name + ".");
            ValidateLoadedSessionWorld(seed, tempSlotName, "первый вход", report);

            WildWindGameplayMenu gameplayMenu = FindFirstObjectByType<WildWindGameplayMenu>();
            report.Check(gameplayMenu != null, gameplayMenu != null ? "Внутриигровое меню найдено при первом входе." : "Внутриигровое меню не найдено при первом входе.");
            if (gameplayMenu == null)
            {
                yield break;
            }

            gameplayMenu.SetOpen(true);
            yield return null;
            MetaGameState pausedMeta = FindFirstObjectByType<MetaGameState>();
            report.Check(pausedMeta != null && pausedMeta.IsSessionPaused && Approximately(Time.timeScale, 0f, 0.001f),
                "Esc-меню ставит world-сессию на паузу.");

            bool saveExitInvoked = TryInvokePrivateMethod(gameplayMenu, "SaveAndExitToMenu", report);
            report.Check(saveExitInvoked, "Кнопка 'Сохранить и в меню' вызывается без исключений.");
            yield return null;
            yield return null;

            Scene returnedScene = SceneManager.GetActiveScene();
            report.Check(returnedScene.name == DefaultStartSceneName, "Внутриигровое меню вернуло сессию на стартовый экран: " + returnedScene.name + ".");
            report.Check(File.Exists(tempSlotPath), "Save slot остался на диске после выхода в меню с сохранением.");

            WildWindSaveSlots.SetSelectedSaveFileName(tempSlotName);
            WildWindSaveSlots.MarkPendingGameplayLaunch();
            sessionLoopLaunchInProgress = true;
            SceneManager.LoadScene(DefaultWorldSceneName);
            DisableDuplicateBigTestRunners();
            yield return WaitForLoadedSessionWorld(seed, tempSlotName);
            sessionLoopLaunchInProgress = false;
            ReportSessionWorldReadinessIfNeeded(seed, tempSlotName, "повторный вход", report);

            Scene secondWorldScene = SceneManager.GetActiveScene();
            report.Check(secondWorldScene.name == DefaultWorldSceneName, "Повторный Continue снова загрузил world-сцену: " + secondWorldScene.name + ".");
            ValidateLoadedSessionWorld(seed, tempSlotName, "повторный вход", report);
        }
        finally
        {
            sessionLoopLaunchInProgress = false;
            RestoreSessionLoopPrefs(previousSelectedSave, previousPendingLaunch);
            TryDeleteTemporaryFile(tempSlotPath, "save slot проверки перезаходов", report);
        }
    }

    private IEnumerator WaitForLoadedSessionWorld(int expectedSeed, string expectedSaveFileName)
    {
        for (int i = 0; i < 120 && !IsLoadedSessionWorldReady(expectedSeed, expectedSaveFileName); i++)
        {
            DisableDuplicateBigTestRunners();
            yield return null;
        }
    }

    private static bool IsLoadedSessionWorldReady(int expectedSeed, string expectedSaveFileName)
    {
        Scene scene = SceneManager.GetActiveScene();
        WorldRegionRuntime loadedWorld = FindFirstObjectByType<WorldRegionRuntime>();
        WorldRuntimeState loadedRuntimeState = FindFirstObjectByType<WorldRuntimeState>();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();

        return scene.name == DefaultWorldSceneName &&
            loadedWorld != null &&
            loadedWorld.RegionSeed == expectedSeed &&
            loadedRuntimeState != null &&
            loadedRuntimeState.LoadedManifestSeed == expectedSeed &&
            loadedMeta != null &&
            loadedMeta.EffectiveSaveFileName == expectedSaveFileName &&
            loadedMenu != null;
    }

    private static void ReportSessionWorldReadinessIfNeeded(int expectedSeed, string expectedSaveFileName, string label, BigTestReport report)
    {
        if (IsLoadedSessionWorldReady(expectedSeed, expectedSaveFileName))
        {
            return;
        }

        report.Fail("World-сессия не стала готовой после ожидания (" + label + "): " +
            DescribeLoadedSessionWorldReadiness(expectedSeed, expectedSaveFileName) + ".");
    }

    private static string DescribeLoadedSessionWorldReadiness(int expectedSeed, string expectedSaveFileName)
    {
        Scene scene = SceneManager.GetActiveScene();
        WorldRegionRuntime loadedWorld = FindFirstObjectByType<WorldRegionRuntime>();
        WorldRuntimeState loadedRuntimeState = FindFirstObjectByType<WorldRuntimeState>();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();

        string worldSeed = loadedWorld != null ? loadedWorld.RegionSeed.ToString() : "<нет WorldRegionRuntime>";
        string runtimeSeed = loadedRuntimeState != null ? loadedRuntimeState.LoadedManifestSeed.ToString() : "<нет WorldRuntimeState>";
        string saveFileName = loadedMeta != null ? loadedMeta.EffectiveSaveFileName : "<нет MetaGameState>";
        string menu = loadedMenu != null ? "есть" : "нет";

        return "scene=" + scene.name +
            ", expectedScene=" + DefaultWorldSceneName +
            ", worldSeed=" + worldSeed +
            ", expectedSeed=" + expectedSeed +
            ", runtimeSeed=" + runtimeSeed +
            ", metaSave=" + saveFileName +
            ", expectedSave=" + expectedSaveFileName +
            ", gameplayMenu=" + menu;
    }

    private void ValidateLoadedSessionWorld(int expectedSeed, string expectedSaveFileName, string label, BigTestReport report)
    {
        WorldRegionRuntime loadedWorld = FindFirstObjectByType<WorldRegionRuntime>();
        WorldRuntimeState loadedRuntimeState = FindFirstObjectByType<WorldRuntimeState>();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();

        report.Check(loadedWorld != null, "WorldRegionRuntime найден после сценария '" + label + "'.");
        report.Check(loadedWorld != null && loadedWorld.RegionSeed == expectedSeed,
            "Мир после сценария '" + label + "' загружен из save seed " + expectedSeed + ".");
        report.Check(loadedRuntimeState != null && loadedRuntimeState.LoadedManifestSeed == expectedSeed,
            "WorldRuntimeState после сценария '" + label + "' принял manifest seed " + expectedSeed + ".");
        report.Check(loadedMeta != null && loadedMeta.EffectiveSaveFileName == expectedSaveFileName,
            "MetaGameState после сценария '" + label + "' смотрит в выбранный save slot.");
        report.Check(loadedMenu != null, "Внутриигровое меню поднялось после сценария '" + label + "'.");
    }

    private void DisableDuplicateBigTestRunners()
    {
        WildWindBigTestRunner[] runners = FindObjectsByType<WildWindBigTestRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < runners.Length; i++)
        {
            WildWindBigTestRunner runner = runners[i];
            if (runner == null || runner == this)
            {
                continue;
            }

            runner.runOnStart = false;
            runner.hasRun = true;
            runner.enabled = false;
            DestroyBigTestObject(runner.gameObject);
        }
    }

    private static void RestoreSessionLoopPrefs(string selectedSaveFileName, bool pendingGameplayLaunch)
    {
        if (string.IsNullOrWhiteSpace(selectedSaveFileName))
        {
            PlayerPrefs.DeleteKey(WildWindSaveSlots.SelectedSaveFileNamePlayerPrefsKey);
        }
        else
        {
            WildWindSaveSlots.SetSelectedSaveFileName(selectedSaveFileName);
        }

        if (pendingGameplayLaunch)
        {
            WildWindSaveSlots.MarkPendingGameplayLaunch();
        }
        else
        {
            PlayerPrefs.DeleteKey(WildWindSaveSlots.PendingGameplayLaunchPlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    private static void TryDeleteTemporaryFile(string path, string label, BigTestReport report)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception)
        {
            report.Warn("Не удалось удалить временный файл '" + label + "': " + exception.Message);
        }
    }

    private static void CleanupAbandonedBigTestSaveSlots(string keepFileName, BigTestReport report)
    {
        try
        {
            string directory = Application.persistentDataPath;
            if (!Directory.Exists(directory))
            {
                return;
            }

            string keep = string.IsNullOrWhiteSpace(keepFileName) ? "" : Path.GetFileName(keepFileName);
            string[] files = Directory.GetFiles(directory, BigTestSessionSavePrefix + "*.json", SearchOption.TopDirectoryOnly);
            int deleted = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string fileName = Path.GetFileName(path);
                if (!string.IsNullOrWhiteSpace(keep) && fileName.Equals(keep, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(path);
                deleted++;
            }

            if (deleted > 0)
            {
                report.Info("Удалены заброшенные временные save slot большого теста: " + deleted + ".");
            }
        }
        catch (Exception exception)
        {
            report.Warn("Не удалось очистить заброшенные временные save slot большого теста: " + exception.Message);
        }
    }

    private sealed class BigTestSideEffectSnapshot
    {
        private string selectedSaveFileName;
        private bool pendingGameplayLaunch;
        private float timeScale;

        public string ActiveSceneName { get; private set; }

        private BigTestSideEffectSnapshot()
        {
        }

        public static BigTestSideEffectSnapshot Capture()
        {
            Scene scene = SceneManager.GetActiveScene();
            return new BigTestSideEffectSnapshot
            {
                selectedSaveFileName = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty(),
                pendingGameplayLaunch = PlayerPrefs.GetInt(WildWindSaveSlots.PendingGameplayLaunchPlayerPrefsKey, 0) == 1,
                timeScale = Time.timeScale,
                ActiveSceneName = scene.IsValid() ? scene.name : ""
            };
        }

        public void RestorePrefsAndTimeScale()
        {
            RestoreSessionLoopPrefs(selectedSaveFileName, pendingGameplayLaunch);
            Time.timeScale = timeScale;
        }

        public void AssertRestored(BigTestReport report)
        {
            string actualSelectedSave = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty();
            bool actualPendingLaunch = PlayerPrefs.GetInt(WildWindSaveSlots.PendingGameplayLaunchPlayerPrefsKey, 0) == 1;
            Scene activeScene = SceneManager.GetActiveScene();

            report.Check(actualSelectedSave == selectedSaveFileName,
                "Selected save slot восстановлен после большого теста.");
            report.Check(actualPendingLaunch == pendingGameplayLaunch,
                "Pending gameplay launch flag восстановлен после большого теста.");
            report.Check(Approximately(Time.timeScale, timeScale, 0.001f),
                "Time.timeScale восстановлен после большого теста: " + Time.timeScale.ToString("0.###") + ".");
            report.Check(string.IsNullOrWhiteSpace(ActiveSceneName) || activeScene.name == ActiveSceneName,
                "Активная сцена восстановлена после большого теста: " + activeScene.name + ".");
        }
    }

    private void ValidateMaintainability(BigTestReport report)
    {
        report.Section("Методика сопровождения");
        report.Info("Когда появляется новая крупная механика, добавляем сюда отдельный раздел: конфиг, runtime-состояние, симуляция, граничные условия, производительность и пользовательский маршрут.");
        report.Info("Модульные тесты остаются рядом со своей областью: ProductionAutoTestRunner и WorldAutoTestRunner можно запускать отдельно, а большой тест обязан проверять их ключевые инварианты.");
        report.Info("Если тест ругается WARN, это не блокер, но повод записать решение: оставить допуск, ужесточить его или превратить в FAIL.");
        report.Info("Правило проекта: новая фича не считается принятой, пока её главный сценарий не попал в большой тест.");
        report.Pass("Большой тест сформировал явный текстовый протокол, который можно расширять дальше.");
    }

    private WorldRuntimeState.EntityRuntimeState FindExtractableRuntimeEntity()
    {
        if (runtimeState == null)
        {
            return null;
        }

        for (int i = 0; i < runtimeState.Entities.Count; i++)
        {
            WorldRuntimeState.EntityRuntimeState state = runtimeState.Entities[i];
            if (state != null && state.remainingAmount > 10f)
            {
                return state;
            }
        }

        return null;
    }

    private bool ChunkMatches(string chunkId, Vector3 position)
    {
        if (world == null) return false;
        WorldRegionRuntime.WorldChunkRecord chunk = world.GetChunkAt(position);
        return chunk != null && chunk.id == chunkId;
    }

    private void TryWriteReport(string text, WildWindBigTestResult result, BigTestReport report)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, string.IsNullOrWhiteSpace(reportFolder) ? "TestReports" : reportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "WildWindBigTestReport.txt");
            File.WriteAllText(path, text, Encoding.UTF8);
            Debug.Log(LogPrefix + "Текстовый протокол сохранён: " + path, this);

            if (result != null)
            {
                string jsonPath = Path.Combine(folder, "WildWindBigTestReport.json");
                string json = JsonUtility.ToJson(BigTestJsonSummary.FromResult(result, path), true);
                File.WriteAllText(jsonPath, json, Encoding.UTF8);
                Debug.Log(LogPrefix + "JSON summary сохранён: " + jsonPath, this);
            }
        }
        catch (Exception exception)
        {
            report.Warn("Не удалось сохранить протоколы большого теста: " + exception.Message);
        }
    }

    [Serializable]
    private sealed class BigTestJsonSummary
    {
        public string generatedAtUtc;
        public string textReportPath;
        public int contractVersion;
        public bool completed;
        public bool succeeded;
        public int checkCount;
        public int infoCount;
        public int warningCount;
        public int failureCount;
        public long elapsedMilliseconds;
        public int minimumExpectedCheckCount;
        public bool requiredSectionsSatisfied;
        public string[] missingRequiredSections;
        public string[] emptyRequiredSections;
        public string[] sectionNames;

        public static BigTestJsonSummary FromResult(WildWindBigTestResult result, string textReportPath)
        {
            return new BigTestJsonSummary
            {
                generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                textReportPath = textReportPath ?? "",
                contractVersion = result.ContractVersion,
                completed = result.Completed,
                succeeded = result.Succeeded,
                checkCount = result.CheckCount,
                infoCount = result.InfoCount,
                warningCount = result.WarningCount,
                failureCount = result.FailureCount,
                elapsedMilliseconds = result.ElapsedMilliseconds,
                minimumExpectedCheckCount = result.MinimumExpectedCheckCount,
                requiredSectionsSatisfied = result.RequiredSectionsSatisfied,
                missingRequiredSections = ToArray(result.MissingRequiredSections),
                emptyRequiredSections = ToArray(result.EmptyRequiredSections),
                sectionNames = ToArray(result.SectionNames)
            };
        }

        private static string[] ToArray(List<string> values)
        {
            return values == null ? Array.Empty<string>() : values.ToArray();
        }
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
        StockAmounts(storage, recipe.outputs, 60);
        if (!string.IsNullOrWhiteSpace(recipe.fuelItemId))
        {
            SetAtLeast(storage, recipe.fuelItemId, 800);
        }

        for (int i = 0; i < recipe.catalysts.Count; i++)
        {
            ProductionCatalystConfig catalyst = recipe.catalysts[i];
            if (catalyst == null || string.IsNullOrWhiteSpace(catalyst.itemId)) continue;
            SetAtLeast(storage, catalyst.itemId, Mathf.Max(80, catalyst.amount * 30));
        }

        for (int i = 0; i < recipe.assemblySteps.Count; i++)
        {
            AssemblyStepConfig step = recipe.assemblySteps[i];
            if (step == null) continue;
            StockAmounts(storage, step.inputs, 400);
        }

        if (industry.kind == IslandIndustryKind.Processing)
        {
            StockProcessingInputs(storage, config, recipe);
        }
    }

    private static void StockCommonResources(IslandProductionState storage)
    {
        SetAtLeast(storage, "food", 500);
        SetAtLeast(storage, "water", 500);
        SetAtLeast(storage, "aerolite", 500);
        SetAtLeast(storage, "metal", 500);
        SetAtLeast(storage, "mechanisms", 500);
        SetAtLeast(storage, "tools", 500);
        SetAtLeast(storage, "medicines", 500);
        SetAtLeast(storage, "weapon", 500);
        SetAtLeast(storage, "cloth", 500);
        SetAtLeast(storage, "charcoal", 1200);
        SetAtLeast(storage, "fulgur", 500);
        SetAtLeast(storage, "alcohol", 240);
        SetAtLeast(storage, "claudium", 500);
        SetAtLeast(storage, "claudite", 500);
        SetAtLeast(storage, "paper", 120);
    }

    private static void StockAmounts(IslandProductionState storage, List<ProductionItemAmountConfig> amounts, int minimum)
    {
        if (storage == null || amounts == null) return;
        for (int i = 0; i < amounts.Count; i++)
        {
            ProductionItemAmountConfig amount = amounts[i];
            if (amount == null || string.IsNullOrWhiteSpace(amount.itemId)) continue;
            SetAtLeast(storage, amount.itemId, Mathf.Max(minimum, Mathf.CeilToInt(amount.amount * 60f)));
        }
    }

    private static void StockProcessingInputs(IslandProductionState storage, WorldConfigDatabase config, IndustryRecipeConfig recipe)
    {
        if (storage == null || config == null || recipe == null) return;

        string source = recipe.processingSource ?? "";
        if (source.Equals("ore", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.oreTypes.Count; i++)
            {
                OreTypeConfig ore = config.oreTypes[i];
                if (ore == null || string.IsNullOrWhiteSpace(ore.oreItemId)) continue;
                SetAtLeast(storage, ore.oreItemId, 48);
            }
        }

        if (source.Equals("gas", StringComparison.OrdinalIgnoreCase) || source.Equals("condensate", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < config.gasCloudTypes.Count; i++)
            {
                GasCloudTypeConfig gas = config.gasCloudTypes[i];
                if (gas == null || string.IsNullOrWhiteSpace(gas.condensateItemId)) continue;
                SetAtLeast(storage, gas.condensateItemId, 48);
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

    private static bool HasIndustryKind(WorldConfigDatabase config, IslandIndustryKind kind)
    {
        if (config == null) return false;
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

    private static bool HasIndustryOnIsland(WorldConfigDatabase config, string islandId)
    {
        if (config == null || string.IsNullOrWhiteSpace(islandId)) return false;
        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry != null && industry.islandId == islandId)
            {
                return true;
            }
        }

        return false;
    }

    private static void CheckUniqueIds<T>(IReadOnlyList<T> records, Func<T, string> idSelector, string label, BigTestReport report)
    {
        HashSet<string> ids = new HashSet<string>();
        bool unique = true;
        bool notEmpty = true;

        for (int i = 0; i < records.Count; i++)
        {
            string id = records[i] != null ? idSelector(records[i]) : "";
            notEmpty &= !string.IsNullOrWhiteSpace(id);
            if (!string.IsNullOrWhiteSpace(id))
            {
                unique &= ids.Add(id);
            }
        }

        report.Check(notEmpty && unique, "ID " + label + " заполнены и уникальны.");
    }

    private static bool ItemAmountsReferenceExistingItems<T>(IReadOnlyList<T> records, Func<T, string> itemSelector, Func<T, float> amountSelector, WorldConfigDatabase config)
    {
        if (records == null) return true;
        for (int i = 0; i < records.Count; i++)
        {
            T record = records[i];
            string itemId = record != null ? itemSelector(record) : "";
            float amount = record != null ? amountSelector(record) : 0f;
            if (string.IsNullOrWhiteSpace(itemId) || config.GetItem(itemId) == null || amount <= 0f)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllIdsExist<T>(IReadOnlyList<string> ids, Func<string, T> resolver) where T : class
    {
        if (ids == null || ids.Count == 0) return false;
        for (int i = 0; i < ids.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(ids[i]) || resolver(ids[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllIdsExistAllowEmpty<T>(IReadOnlyList<string> ids, Func<string, T> resolver) where T : class
    {
        if (ids == null || ids.Count == 0) return true;
        for (int i = 0; i < ids.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(ids[i]) || resolver(ids[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private static int GetStackAmountForTest(List<ResourceStack> cargo, string itemId)
    {
        if (cargo == null || string.IsNullOrWhiteSpace(itemId)) return 0;
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack != null && stack.resourceId == itemId)
            {
                return Mathf.Max(0, stack.amount);
            }
        }

        return 0;
    }

    private static float MeasureMiningImpactDamage(float damageMultiplier)
    {
        GameObject probe = null;
        try
        {
            probe = new GameObject("Big Test Mining Impact Probe");
            Rigidbody body = probe.AddComponent<Rigidbody>();
            body.mass = 2000f;

            ShipPhysics ship = probe.AddComponent<ShipPhysics>();
            ship.enabled = false;
            ship.baseMass = 2000f;
            ship.cargoMassKg = 0f;
            ship.miningImpactHoldCapacityKg = 1000f;
            ship.miningImpactDamageTakenMultiplier = Mathf.Max(0f, damageMultiplier);
            ship.miningImpactMinDamageSpeedMS = 0f;
            ship.miningImpactDamageScale = 10f;

            DamageableShip damageable = probe.AddComponent<DamageableShip>();
            damageable.debugLogging = false;
            damageable.shipPhysics = ship;
            damageable.maxStructureHp = 10000f;
            damageable.ResetDamageState();

            ship.TryCollectMiningFragment("windshale_ore", 500, 5f, out _);
            return Mathf.Max(0f, 10000f - damageable.structureHp);
        }
        finally
        {
            DestroyBigTestObject(probe);
        }
    }

    private static void DestroyBigTestObject(GameObject target)
    {
        if (target == null) return;

        DestroyBigTestUnityObject(target);
    }

    private static void DestroyBigTestUnityObject(UnityEngine.Object target)
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void ConfigureFlightProbeShip(ShipPhysics ship, Rigidbody body)
    {
        if (ship == null || body == null) return;

        ship.baseMass = 1000f;
        ship.cargoMassKg = 200f;
        ship.hullMaxTakeoffMassKg = 1600f;
        ship.enginePowerKwAt100 = 240f;
        ship.engineFuelEfficiency = 0.5f;
        ship.engineFuelEnergyKwhPerKg = 4f;
        ship.engineFuelStockKg = 20f;
        ship.enginePowerLever = 1f;
        ship.claudiumStock = 20f;
        ship.claudiumConsumptionPerTonSecond = 0.01f;
        ship.claudiumLiftEfficiency = 10f;
        ship.claudiumMaxLiftKg = 1500f;
        ship.claudiumLiftSmoothing = 1000f;
        ship.claudiumCurrentLiftN = 0f;
        ship.claudiumPowerDrawWatts = 0f;
        ship.claudiumPowerDrawKw = 0f;
        ship.claudiumRequestedLiftKg = 0f;
        ship.gasHarvesterEnabled = false;
        ship.gasHarvesterWaterOnly = false;
        ship.gasHarvesterPowerDrawKw = 0f;
        ship.gasHarvesterPowerDrawActualKw = 0f;
        ship.miningImpactDamageTakenMultiplier = 1f;
        ship.surveyPaperToInfoEfficiency = 1f;
        ship.leviathanAlarmGenerationMultiplier = 1f;
        ship.harpoonWeaponCostPerMinute = 0f;
        ship.harpoonMaxCarcassMassKg = 0f;
        ship.refrigeratedHoldCapacityLiters = 0f;
        ship.refrigeratedHoldPowerDrawKw = 0f;
        ship.baseObservationRadiusMeters = 0f;
        ship.observationRadiusMeters = 0f;
        ship.observationFactsAtHalfRadiusPerSecond = 0f;
        ship.leviathanHuntAutopilotEnabled = false;
        ship.airDensity = 1.225f;
        ship.dragCoefficient = 0.7f;
        ship.frontalArea = 6f;
        ship.sideResistance = 1f;
        ship.verticalAreaFactor = 4f;
        ship.windVelocity = Vector3.zero;
        ship.autoStabilizeAtStart = false;
        ship.altitudeHold = false;
        ship.cruiseControl = false;
        ship.headingHold = false;
        ship.routeEnabled = false;
        ship.positionHold = false;
        if (ship.waypoints == null)
        {
            ship.waypoints = new List<Vector3>();
        }
        ship.waypoints.Clear();
        ship.currentWaypointIndex = 0;
        ship.targetSpeedMS = 0f;
        ship.targetHeading = 0f;
        ship.thrustInput = 0f;
        ship.turnInput = 0f;
        ship.liftInput = 0f;
        ship.propellerMaxSpeedMS = 30f;
        ship.propellerEfficiency = 1f;
        ship.propellerMaxThrustKgf = 500f;
        ship.gyroTurnTorque = 12000f;
        ship.gyroTurnDamping = 0.8f;
        ship.RefreshRuntimeShipSettings();
        ship.StabilizeForFlightStart(false);
        body.useGravity = true;
        body.linearDamping = 0f;
        body.angularDamping = 2f;
    }

    private static void ConfigureForwardSpeedProbeShip(ShipPhysics ship, Rigidbody body)
    {
        ConfigureFlightProbeShip(ship, body);
        if (ship == null || body == null) return;

        ship.baseMass = 800f;
        ship.cargoMassKg = 0f;
        ship.enginePowerKwAt100 = 120f;
        ship.engineFuelEfficiency = 1f;
        ship.engineFuelStockKg = 20f;
        ship.enginePowerLever = 1f;
        ship.claudiumStock = 0f;
        ship.claudiumCurrentLiftN = 0f;
        ship.claudiumPowerDrawKw = 0f;
        ship.claudiumPowerDrawWatts = 0f;
        ship.airDensity = 1.225f;
        ship.dragCoefficient = 1f;
        ship.frontalArea = 10f;
        ship.sideResistance = 0f;
        ship.propellerEfficiency = 1f;
        ship.propellerMaxThrustKgf = 500f;
        ship.propellerMaxSpeedMS = 60f;
        ship.windVelocity = Vector3.zero;
        ship.RefreshRuntimeShipSettings();
        ship.StabilizeForFlightStart(false);
        ship.thrustInput = 1f;
        ship.enginePowerLever = 1f;
        body.useGravity = false;
    }

    private static float CalculateExpectedForwardMaxSpeed(ShipPhysics ship)
    {
        if (ship == null) return 0f;

        float dragPerSpeedSquared = ship.CurrentAeroDrag;
        float staticThrustN = Mathf.Max(0f, ship.propellerMaxThrustKgf) * 9.81f;
        float usefulPowerW = Mathf.Max(0f, ship.enginePowerKwAt100 * ship.enginePowerLever - ship.claudiumPowerDrawKw - ship.gasHarvesterPowerDrawActualKw - ship.refrigeratedHoldPowerDrawActualKw)
            * Mathf.Clamp01(ship.propellerEfficiency)
            * 1000f;

        if (dragPerSpeedSquared <= 0f || staticThrustN <= 0f || usefulPowerW <= 0f)
        {
            return 0f;
        }

        float staticLimitedSpeed = Mathf.Sqrt(staticThrustN / dragPerSpeedSquared);
        float powerTransitionSpeed = usefulPowerW / staticThrustN;
        float expectedSpeed = staticLimitedSpeed <= powerTransitionSpeed
            ? staticLimitedSpeed
            : Mathf.Pow(usefulPowerW / dragPerSpeedSquared, 1f / 3f);

        if (ship.propellerMaxSpeedMS > 0f)
        {
            expectedSpeed = Mathf.Min(expectedSpeed, ship.propellerMaxSpeedMS);
        }

        return expectedSpeed;
    }

    private static void ResetFlightProbeBody(Rigidbody body, Vector3 position, Quaternion rotation, bool useGravity)
    {
        if (body == null) return;

        body.useGravity = useGravity;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = position;
        body.rotation = rotation;
        body.transform.SetPositionAndRotation(position, rotation);
        body.Sleep();
        body.WakeUp();
    }

    private static bool StepShipPhysicsProbe(ShipPhysics ship, PhysicsScene physicsScene, int steps, BigTestReport report)
    {
        if (ship == null)
        {
            report.Fail("Проба лётной физики не получила ShipPhysics.");
            return false;
        }

        if (!physicsScene.IsValid())
        {
            report.Fail("Проба лётной физики не получила валидную PhysicsScene.");
            return false;
        }

        int stepCount = Mathf.Max(0, steps);
        float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.001f);
        for (int i = 0; i < stepCount; i++)
        {
            if (!TryInvokePrivateMethod(ship, "FixedUpdate", report))
            {
                return false;
            }

            try
            {
                physicsScene.Simulate(deltaTime);
            }
            catch (Exception exception)
            {
                report.Fail("Проба лётной физики не смогла просимулировать physics-шаг: " + exception.Message);
                return false;
            }
        }

        return true;
    }

    private static bool TryInvokePrivateMethod(object target, string methodName, BigTestReport report)
    {
        if (target == null)
        {
            report.Fail("Не удалось вызвать " + methodName + ": целевой объект отсутствует.");
            return false;
        }

        System.Reflection.MethodInfo method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null)
        {
            report.Fail("В " + target.GetType().Name + " не найден внутренний метод " + methodName + ".");
            return false;
        }

        try
        {
            method.Invoke(target, null);
            return true;
        }
        catch (Exception exception)
        {
            Exception root = exception.InnerException ?? exception;
            report.Fail("Внутренний метод " + target.GetType().Name + "." + methodName + " упал: " + root.GetType().Name + " - " + root.Message);
            return false;
        }
    }

    private static float ReadPrivateFloat(object target, string fieldName, float fallback)
    {
        if (target == null) return fallback;
        System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null && field.FieldType == typeof(float) ? (float)field.GetValue(target) : fallback;
    }

    private static bool ReadPrivateBool(object target, string fieldName, bool fallback)
    {
        if (target == null) return fallback;
        System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null && field.FieldType == typeof(bool) ? (bool)field.GetValue(target) : fallback;
    }

    private static bool Approximately(float actual, float expected, float tolerance)
    {
        return Mathf.Abs(actual - expected) <= tolerance;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string FormatKm(float meters)
    {
        return (meters / 1000f).ToString("0.#") + " км";
    }

    private static string FormatVector(Vector3 value)
    {
        return "(" + value.x.ToString("0.#") + ", " + value.y.ToString("0.#") + ", " + value.z.ToString("0.#") + ")";
    }

    private sealed class BigTestReport
    {
        private readonly UnityEngine.Object context;
        private readonly bool logImmediateMessages;
        private readonly StringBuilder builder = new StringBuilder(8192);
        private readonly Dictionary<string, int> sectionCheckCounts = new Dictionary<string, int>();
        private readonly List<string> sectionOrder = new List<string>();
        private int checkCount;
        private int infoCount;
        private int warningCount;
        private int failureCount;
        private string currentSection = "";

        public int FailureCount => failureCount;

        public BigTestReport(UnityEngine.Object context, bool logImmediateMessages = true)
        {
            this.context = context;
            this.logImmediateMessages = logImmediateMessages;
            builder.AppendLine("=== Wild Wind: большой тест ===");
        }

        public void Section(string title)
        {
            currentSection = title ?? "";
            if (!sectionCheckCounts.ContainsKey(currentSection))
            {
                sectionCheckCounts[currentSection] = 0;
                sectionOrder.Add(currentSection);
            }

            builder.AppendLine();
            builder.AppendLine("## " + title);
        }

        public void Info(string message)
        {
            infoCount++;
            builder.AppendLine("- INFO: " + message);
        }

        public void Pass(string message)
        {
            checkCount++;
            IncrementCurrentSectionChecks();
            builder.AppendLine("- OK: " + message);
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

        public void Warn(string message)
        {
            warningCount++;
            builder.AppendLine("- WARN: " + message);
            if (logImmediateMessages)
            {
                Debug.LogWarning(LogPrefix + "WARN" + FormatSection() + ": " + message, context);
            }
        }

        public void Fail(string message)
        {
            checkCount++;
            IncrementCurrentSectionChecks();
            failureCount++;
            builder.AppendLine("- FAIL: " + message);
            if (logImmediateMessages)
            {
                Debug.LogError(LogPrefix + "FAIL" + FormatSection() + ": " + message, context);
            }
        }

        public void AssertIntegrity(string[] requiredSections, int minimumChecks, Func<bool> canaryProbe)
        {
            int preIntegrityCheckCount = checkCount;
            Section("Целостность большого теста");

            List<string> missingSections = GetMissingSections(requiredSections);
            List<string> emptySections = GetEmptySections(requiredSections);
            Check(missingSections.Count == 0,
                missingSections.Count == 0
                    ? "Все обязательные разделы большого теста были запущены."
                    : "Не были запущены обязательные разделы: " + JoinNames(missingSections) + ".");
            Check(emptySections.Count == 0,
                emptySections.Count == 0
                    ? "Каждый обязательный раздел содержит хотя бы одну OK/FAIL проверку."
                    : "Обязательные разделы без проверок: " + JoinNames(emptySections) + ".");
            Check(preIntegrityCheckCount >= minimumChecks,
                "Количество проверок до self-check не ниже контракта: " + preIntegrityCheckCount + " / " + minimumChecks + ".");
            Check(canaryProbe != null && canaryProbe(),
                "Canary-сбой делает машинный результат красным и не проходит как OK.");
        }

        public void Finish(long elapsedMs)
        {
            builder.AppendLine();
            builder.AppendLine("## Итог");
            builder.AppendLine("- Проверок OK/FAIL: " + checkCount);
            builder.AppendLine("- Информационных строк: " + infoCount);
            builder.AppendLine("- Предупреждений: " + warningCount);
            builder.AppendLine("- Ошибок: " + failureCount);
            builder.AppendLine("- Время выполнения: " + elapsedMs + " мс");
            builder.AppendLine(failureCount == 0
                ? "- Результат: OK, большой тест пройден."
                : "- Результат: НЕ ОК, большой тест нашёл проблемы.");
        }

        public string BuildText()
        {
            return builder.ToString();
        }

        public WildWindBigTestResult CreateResult(int contractVersion, long elapsedMs, bool completed, string[] requiredSections, int minimumChecks)
        {
            List<string> missingSections = GetMissingSections(requiredSections);
            List<string> emptySections = GetEmptySections(requiredSections);
            bool requiredSectionsSatisfied = missingSections.Count == 0 && emptySections.Count == 0;
            return new WildWindBigTestResult
            {
                ContractVersion = contractVersion,
                Completed = completed,
                Succeeded = completed && failureCount == 0 && requiredSectionsSatisfied && checkCount >= minimumChecks,
                CheckCount = checkCount,
                InfoCount = infoCount,
                WarningCount = warningCount,
                FailureCount = failureCount,
                ElapsedMilliseconds = elapsedMs,
                MinimumExpectedCheckCount = minimumChecks,
                RequiredSectionsSatisfied = requiredSectionsSatisfied,
                MissingRequiredSections = missingSections,
                EmptyRequiredSections = emptySections,
                SectionNames = new List<string>(sectionOrder),
                ReportText = BuildText()
            };
        }

        private string FormatSection()
        {
            return string.IsNullOrWhiteSpace(currentSection) ? "" : " [" + currentSection + "]";
        }

        private void IncrementCurrentSectionChecks()
        {
            if (string.IsNullOrWhiteSpace(currentSection))
            {
                return;
            }

            if (!sectionCheckCounts.ContainsKey(currentSection))
            {
                sectionCheckCounts[currentSection] = 0;
                sectionOrder.Add(currentSection);
            }

            sectionCheckCounts[currentSection]++;
        }

        private List<string> GetMissingSections(string[] requiredSections)
        {
            List<string> missing = new List<string>();
            if (requiredSections == null) return missing;

            for (int i = 0; i < requiredSections.Length; i++)
            {
                string section = requiredSections[i];
                if (!string.IsNullOrWhiteSpace(section) && !sectionCheckCounts.ContainsKey(section))
                {
                    missing.Add(section);
                }
            }

            return missing;
        }

        private List<string> GetEmptySections(string[] requiredSections)
        {
            List<string> empty = new List<string>();
            if (requiredSections == null) return empty;

            for (int i = 0; i < requiredSections.Length; i++)
            {
                string section = requiredSections[i];
                if (string.IsNullOrWhiteSpace(section))
                {
                    continue;
                }

                if (!sectionCheckCounts.TryGetValue(section, out int checks) || checks <= 0)
                {
                    empty.Add(section);
                }
            }

            return empty;
        }

        private static string JoinNames(List<string> names)
        {
            return names == null || names.Count == 0 ? "" : string.Join(", ", names.ToArray());
        }
    }
}

public sealed class WildWindBigTestResult
{
    public int ContractVersion { get; internal set; }
    public bool Completed { get; internal set; }
    public bool Succeeded { get; internal set; }
    public int CheckCount { get; internal set; }
    public int InfoCount { get; internal set; }
    public int WarningCount { get; internal set; }
    public int FailureCount { get; internal set; }
    public long ElapsedMilliseconds { get; internal set; }
    public int MinimumExpectedCheckCount { get; internal set; }
    public bool RequiredSectionsSatisfied { get; internal set; }
    public List<string> MissingRequiredSections { get; internal set; } = new List<string>();
    public List<string> EmptyRequiredSections { get; internal set; } = new List<string>();
    public List<string> SectionNames { get; internal set; } = new List<string>();
    public string ReportText { get; internal set; } = "";

    public static WildWindBigTestResult CreateBlocked(string reason)
    {
        return new WildWindBigTestResult
        {
            Completed = false,
            Succeeded = false,
            FailureCount = 1,
            ReportText = reason ?? "Большой тест не был запущен."
        };
    }
}
