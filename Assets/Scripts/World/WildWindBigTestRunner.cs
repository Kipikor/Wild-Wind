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

    private IEnumerator Start()
    {
        if (!runOnStart)
        {
            yield break;
        }

        yield return null;
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

        hasRun = true;
        BigTestReport report = new BigTestReport(this);
        Stopwatch totalWatch = Stopwatch.StartNew();

        try
        {
            ResolveReferences();
            DescribeTestScope(report);
            ValidateSceneContext(report);

            WorldConfigDatabase config = LoadConfig(report);
            ValidateConfigDatabase(config, report);
            ValidateProductionSimulation(config, report);

            ValidateWorldDataManifest(report);
            ValidateWorldRuntime(report);
            ValidateWorldEntityIndex(report);
            ValidateWorldRuntimeState(report);
            ValidateWorldSimulationTick(report);
            ValidateBubbleStreaming(report);
            ValidateVisualAtmosphere(report);
            ValidateSettings(report);
            ValidateShipWindAerodynamics(report);
            ValidateMaintainability(report);
        }
        catch (Exception exception)
        {
            report.Fail("Большой тест упал исключением: " + exception.GetType().Name + " - " + exception.Message);
            Debug.LogException(exception, this);
        }

        totalWatch.Stop();
        report.Finish(totalWatch.ElapsedMilliseconds);

        string text = report.BuildText();
        if (writeReportFile)
        {
            TryWriteReport(text, report);
        }

        if (logFullReportToConsole)
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
    }

    public void ResetRunStateForEditor()
    {
        hasRun = false;
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
        report.Info("Кнопка: Wild Wind/Провести большой тест.");
        report.Info("Назначение: один общий дотошный протокол по текущей сборке игры.");
        report.Info("Сейчас покрыто: CSV-конфиги, производства, мир 100x100 км, чанки, высотные зоны, активный пузырь, визуальные зависимости, настройки, ветер/аэродинамика.");
        report.Info("Допуски: размер мира +-1 м, размер чанка +-1 м, среднее обновление пузыря <= " + streamerAverageBudgetMs.ToString("0.#") + " мс, симуляция производств " + productionSimulationMinutes.ToString("0.#") + " мин.");
        report.Info("Принцип: FAIL = сломано или противоречит текущему ТЗ; WARN = подозрительно, но можно продолжать; OK = проверено явно.");
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
        report.Check(config.islandIndustries.Count >= 6, "Production_industry.csv содержит производственные линии: " + config.islandIndustries.Count + ".");
        report.Check(config.industryRecipes.Count >= 6, "Production_recipe.csv содержит производственные рецепты: " + config.industryRecipes.Count + ".");
        report.Check(config.islandArchetypes.Count == 5, "Island_archetype.csv содержит 5 типов островов: " + config.islandArchetypes.Count + ".");
        report.Check(config.islandArchetypeStages.Count >= 10, "Island_archetype_stage.csv содержит стадии развития островов: " + config.islandArchetypeStages.Count + ".");
        report.Check(config.islandSocialNeeds.Count == 5, "Island_social_need.csv содержит 5 общественных потребностей: " + config.islandSocialNeeds.Count + ".");
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
        CheckUniqueIds(config.islandIndustries, industry => industry.id, "линий производств", report);
        CheckUniqueIds(config.industryRecipes, recipe => recipe.id, "рецептов производств", report);
        CheckUniqueIds(config.islandArchetypes, archetype => archetype.id, "типов островов", report);
        CheckUniqueIds(config.islandArchetypeStages, stage => stage.id, "стадий островов", report);
        CheckUniqueIds(config.islandSocialNeeds, need => need.id, "общественных потребностей", report);
        CheckUniqueIds(config.islandBuildings, building => building.id, "островных зданий", report);

        ValidateConfigReferences(config, report);
        ValidateIslandDevelopmentConfig(config, report);
        ValidateIndustryConfig(config, report);
    }

    private static void ValidateConfigReferences(WorldConfigDatabase config, BigTestReport report)
    {
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
                tech.cycleTimeSeconds >= 0 &&
                tech.requiredCycles > 0 &&
                AllIdsExistAllowEmpty(tech.prerequisiteTechnologyIds, config.GetTechnology) &&
                ItemAmountsReferenceExistingItems(tech.cycleCost, c => c.itemId, c => c.amount, config);
        }

        for (int i = 0; i < config.specialModules.Count; i++)
        {
            SpecialModuleConfig module = config.specialModules[i];
            bool techReferenceOk = module == null ||
                string.IsNullOrWhiteSpace(module.completedTechId) ||
                config.GetTechnology(module.completedTechId) != null;
            techValid &= module != null &&
                !string.IsNullOrWhiteSpace(module.id) &&
                module.baseMassKg >= 0f &&
                techReferenceOk;
        }

        report.Check(techValid, "Технологии и спецмодули имеют валидные зависимости, стоимость и массу.");
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
            config.GetItem("design_experience") != null;
        report.Check(coreItemsExist, "Магистральные ресурсы и конструкторский опыт заведены в Item.csv.");

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
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null)
            {
                needsValid = false;
                continue;
            }

            needKinds.Add(need.kind);
            needsValid &= config.GetItem(need.recoveryItemId) != null &&
                need.maxValue > 0f &&
                need.restorePerItem > 0f &&
                need.loadDecayPerHour > 0f;
        }

        report.Check(needsValid && needKinds.Count == 5, "Пять общественных потребностей валидны и восстанавливаются ресурсами.");

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
                building.creativityLoad <= 5;

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
        IslandProductionState capital = progress.GetIslandProductionState("capital", true);
        capital.SetResourceAmount("food", 5);
        capital.SetResourceAmount("medicines", 5);
        capital.SetResourceAmount("weapon", 5);
        capital.SetResourceAmount("cloth", 5);
        capital.SetResourceAmount("paper", 5);

        IslandSocietySimulator.EnsureIslandStates(config, progress);
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            IslandSocietyNeedState state = capital.GetSocietyNeedState(need.id, true);
            state.currentValue = 0f;
            state.initialized = true;
        }

        int restored = IslandSocietySimulator.Advance(config, progress, 1f);
        bool restoredAllNeeds = restored >= 5;
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            IslandSocietyNeedState state = capital.GetSocietyNeedState(need.id, false);
            restoredAllNeeds &= state != null && state.currentValue > 0f;
        }

        report.Check(restoredAllNeeds, "Остров сам восстанавливает общественные потребности ресурсами со склада.");

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
    }

    private void ValidateShipWindAerodynamics(BigTestReport report)
    {
        report.Section("Корабль, ветер и аэродинамика");
        GameObject testShip = null;
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
        }
        finally
        {
            if (testShip != null)
            {
                Destroy(testShip);
            }
        }
    }

    private void ValidateMaintainability(BigTestReport report)
    {
        report.Section("Методика сопровождения");
        report.Info("Когда появляется новая крупная механика, добавляем сюда отдельный раздел: конфиг, runtime-состояние, симуляция, граничные условия, производительность.");
        report.Info("Модульные тесты остаются рядом со своей областью: ProductionAutoTestRunner и WorldAutoTestRunner можно запускать отдельно, а большой тест обязан проверять их ключевые инварианты.");
        report.Info("Если тест ругается WARN, это не блокер, но повод записать решение: оставить допуск, ужесточить его или превратить в FAIL.");
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

    private void TryWriteReport(string text, BigTestReport report)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, string.IsNullOrWhiteSpace(reportFolder) ? "TestReports" : reportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "WildWindBigTestReport.txt");
            File.WriteAllText(path, text, Encoding.UTF8);
            Debug.Log(LogPrefix + "Текстовый протокол сохранён: " + path, this);
        }
        catch (Exception exception)
        {
            report.Warn("Не удалось сохранить текстовый протокол: " + exception.Message);
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
        SetAtLeast(storage, "wood", 500);
        SetAtLeast(storage, "metal", 500);
        SetAtLeast(storage, "mechanisms", 500);
        SetAtLeast(storage, "tools", 500);
        SetAtLeast(storage, "medicines", 500);
        SetAtLeast(storage, "weapon", 500);
        SetAtLeast(storage, "cloth", 500);
        SetAtLeast(storage, "charcoal", 1200);
        SetAtLeast(storage, "sulfur", 500);
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
        private readonly StringBuilder builder = new StringBuilder(8192);
        private int checkCount;
        private int infoCount;
        private int warningCount;
        private int failureCount;
        private string currentSection = "";

        public int FailureCount => failureCount;

        public BigTestReport(UnityEngine.Object context)
        {
            this.context = context;
            builder.AppendLine("=== Wild Wind: большой тест ===");
        }

        public void Section(string title)
        {
            currentSection = title;
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
            Debug.LogWarning(LogPrefix + "WARN" + FormatSection() + ": " + message, context);
        }

        public void Fail(string message)
        {
            checkCount++;
            failureCount++;
            builder.AppendLine("- FAIL: " + message);
            Debug.LogError(LogPrefix + "FAIL" + FormatSection() + ": " + message, context);
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

        private string FormatSection()
        {
            return string.IsNullOrWhiteSpace(currentSection) ? "" : " [" + currentSection + "]";
        }
    }
}
