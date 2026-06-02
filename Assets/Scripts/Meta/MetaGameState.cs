using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MetaGameSaveData
{
    public const int CurrentVersion = 2;

    [InspectorName("Версия сохранения")]
    public int version = CurrentVersion;
    [InspectorName("Прогресс")]
    public PlayerProgress progress = new PlayerProgress();
    [InspectorName("World Manifest")]
    public WorldManifestData worldManifest;
    [InspectorName("World Runtime")]
    public WorldRuntimeSaveData worldRuntime;
    [InspectorName("Gameplay Session")]
    public GameplaySessionSaveData gameplaySession;
}

public partial class MetaGameState : MonoBehaviour
{
    private const float ExtractionRunupOutwardDotThreshold = 0.9659258f;
    private const float SortieEntryApproachSeconds = 20f;
    private const float SortieEntryDefaultMaxSpeedMS = 120f;

    [Header("Связи")]
    [InspectorName("Каталог кораблей")]
    public ShipCatalogSO catalog;
    [InspectorName("Древо техники")]
    public TechTreeDefinitionSO techTree;
    [InspectorName("Загрузчик корабля")]
    public ShipLoader shipLoader;
    [InspectorName("Контроллер миссии")]
    public MissionController missionController;
    [InspectorName("Логистический флот")]
    public LogisticsFleetController logisticsFleet;
    [InspectorName("Gas clouds")]
    public GasCloudManager gasCloudManager;
    [InspectorName("Gas autopilots")]
    public GasHarvesterFleetController gasHarvesterFleet;
    [InspectorName("Mining rocks")]
    public MiningRockManager miningRockManager;
    [InspectorName("Mining autopilots")]
    public MiningFleetController miningFleet;
    [InspectorName("Leviathans")]
    public LeviathanManager leviathanManager;
    [InspectorName("Разведывательный флот")]
    public ScoutFleetController scoutFleet;
    [InspectorName("World Runtime")]
    public WorldRegionRuntime worldRuntime;
    [InspectorName("World Index")]
    public WorldEntityIndex worldIndex;
    [InspectorName("World Runtime State")]
    public WorldRuntimeState worldRuntimeState;
    [InspectorName("Стартовые деньги")]
    public int startingMoney;
    [InspectorName("Прогресс игрока")]
    public PlayerProgress progress = new PlayerProgress();

    [Header("Сессия")]
    [InspectorName("Стартовый режим")]
    public GameSessionMode startingMode = GameSessionMode.Docked;
    [InspectorName("Стартовый док")]
    public string startingDockId = "capital";
    [InspectorName("Тип стартового дока")]
    public DockingLocationKind startingDockKind = DockingLocationKind.Island;
    [InspectorName("Автосохранение при стыковке")]
    [Tooltip("Ручное сохранение разрешено только в режиме стыковки. При выходе из игры текущий вылет сохраняется отдельно.")]
    public bool autoSaveOnDock = true;
    [InspectorName("Загружать сохранение при старте")]
    [Tooltip("Если во время прошлого запуска игра была закрыта в полете, загрузка вернет корабль в сохраненную точку вылета.")]
    public bool loadSavedGameOnAwake = true;
    [InspectorName("Имя файла сохранения")]
    public string saveFileName = "wild_wind_save.json";

    [Header("Стартовые ресурсы")]
    [InspectorName("Стартовая руда")]
    public int startingOre = 4;
    [InspectorName("Стартовое железо")]
    public int startingIron = 0;
    [InspectorName("Стартовое топливо на борту, кг")]
    public int startingFuelKg = 150;
    [InspectorName("Стартовый клавдий на борту, кг")]
    public int startingClaudiumKg = 75;
    [InspectorName("Стартовая бумага в столице, кг")]
    [Tooltip("Нужна разведчикам: 1 кг бумаги превращается в 1 кг научной информации.")]
    public int startingPaperKg = 120;

    [Header("Процессы реального времени")]
    [InspectorName("Обновлять процессы во время игры")]
    public bool processRealTimeWhilePlaying = true;
    [InspectorName("Пропустить стартовую догонку процессов")]
    [Tooltip("Для изолированных тестовых сцен: не прокручивает логистику, разведку и другие процессы в Awake.")]
    public bool skipInitialProcessCatchUp;
    [InspectorName("Интервал добычи руды, сек")]
    public int idleMiningIntervalSeconds = 60;
    [InspectorName("Руды за цикл добычи")]
    public int idleMiningOrePerCycle = 1;
    [InspectorName("Длительность плавки железа, сек")]
    public int ironSmeltingDurationSeconds = 120;
    [InspectorName("Цена плавки, руда")]
    public int ironSmeltingOreCost = 2;
    [InspectorName("Выход плавки, железо")]
    public int ironSmeltingIronOutput = 1;
    [InspectorName("Длительность миссии по умолчанию, сек")]
    public int defaultTimedMissionDurationSeconds = 300;
    [InspectorName("Интервал обновления магазина, сек")]
    [Tooltip("Через этот интервал меняется зерно магазина. Ассортимент можно строить от этого числа.")]
    public int shopRefreshIntervalSeconds = 3600;

    [Header("Конфиги мира")]
    [InspectorName("Папка конфигов от Assets")]
    [Tooltip("CSV-конфиги мира загружаются из этой папки при старте Play Mode.")]
    public string worldConfigFolder = "Data/Config";
    [InspectorName("Остров столицы")]
    [Tooltip("Остров, на котором находится лаборатория технологий и стартует новая игра.")]
    public string capitalIslandId = "capital";
    [InspectorName("Производство островов")]
    [Tooltip("Если включено, острова производят и потребляют товары по CSV-конфигам.")]
    public bool islandProductionEnabled = true;
    [InspectorName("Создавать острова из конфигов")]
    [Tooltip("Если включено, при старте Play Mode из Island.csv создаются видимые острова с DockingPort.")]
    public bool spawnConfigIslandsOnPlay = true;
    [InspectorName("Визуальный радиус острова")]
    [Tooltip("Размер простой временной модели острова. Радиус стыковки берется отдельно из Island.csv.")]
    public float configIslandVisualRadius = 80f;

    [Header("Session Extraction Core")]
    [InspectorName("Use session extraction core")]
    [Tooltip("When enabled, runtime uses base/sortie/resource turnover and does not tick legacy island social needs, passengers, or island supply chains.")]
    public bool sessionExtractionCoreMode = true;

    [Header("Отладочный интерфейс стыковки")]
    [InspectorName("Показывать интерфейс")]
    public bool showDockingDebugUI = true;
    [InspectorName("Ширина интерфейса")]
    public int debugUiWidth = 380;
    [InspectorName("Ресурс для отладки склада")]
    public string productionDebugResourceId = "charcoal";
    [InspectorName("Количество для отладки склада")]
    public int productionDebugAmount = 25;

    [Header("Аварии")]
    [InspectorName("Автоматически добавить детектор крушений")]
    [Tooltip("Если включено, на корабль будет добавлен детектор крушений: при аварии текущий корабль и груз теряются, игрок возвращается в город.")]
    public bool autoInstallCrashDetector = true;

    public GameSessionMode CurrentMode => progress != null ? progress.currentMode : startingMode;
    public bool IsDocked => CurrentMode == GameSessionMode.Docked;
    public bool HasActiveSortie => progress != null && progress.HasActiveSortie;
    public SortieSessionState ActiveSortie => progress != null ? progress.activeSortie : null;
    public string ActiveSortieExtractionRunupStatus => activeSortieExtractionRunupStatus;
    public bool IsSafeOreSortieActive => HasActiveSortie
        && ActiveSortie != null
        && ActiveSortie.zone != null
        && ActiveSortie.zone.sortieId == SessionExtractionConstants.DefaultSafeOreSortieId;
    public bool CanCatchStarterSortieFragmentsInCargo => HasActiveSortie
        && ActiveSortie != null
        && ActiveSortie.zone != null
        && IsDefaultSessionSortieId(ActiveSortie.zone.sortieId);
    public string EffectiveSaveFileName
    {
        get
        {
            string selectedFileName = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty();
            if (!string.IsNullOrWhiteSpace(selectedFileName) && saveFileName == WildWindSaveSlots.DefaultSaveFileName)
            {
                return selectedFileName;
            }

            return string.IsNullOrWhiteSpace(saveFileName) ? WildWindSaveSlots.DefaultSaveFileName : saveFileName;
        }
    }

    public string SavePath => Path.Combine(Application.persistentDataPath, EffectiveSaveFileName);

    private bool initialized;
    private bool isAdvancingProcesses;
    private bool suppressFlagshipMoraleDrain;
    private string lastSaveMessage = "";
    private Vector2 debugScroll;
    private WorldConfigDatabase worldConfig = new WorldConfigDatabase();
    private Transform spawnedConfigIslandRoot;
    private readonly List<CargoPlanEntry> cargoPlan = new List<CargoPlanEntry>();
    private string cargoPlanDockId = "";
    private string syncedFuelResourceId = "";
    private string syncedClaudiumResourceId = "";
    private float pendingFuelConsumedKg;
    private float pendingClaudiumConsumedKg;
    private string activeSortieExtractionRunupStatus = "";

    private ShipCatalogSO ActiveCatalog => catalog != null ? catalog : shipLoader != null ? shipLoader.catalog : null;
    public WorldConfigDatabase WorldConfig => worldConfig;
    public ShipCatalogSO CurrentCatalog => ActiveCatalog;
    public bool IsSessionExtractionCoreMode => sessionExtractionCoreMode;
    public bool UsesLegacyDockAssemblyUi => !sessionExtractionCoreMode;
    public int SpawnedConfiguredIslandCount => spawnedConfigIslandRoot != null ? spawnedConfigIslandRoot.childCount : 0;

    private void SyncSessionExtractionCoreMode()
    {
        if (progress == null) return;

        progress.sessionExtractionCoreMode = sessionExtractionCoreMode;
    }

    private void EnsureEconomyRuntimeStates()
    {
        if (progress == null || worldConfig == null || !worldConfig.isLoaded) return;

        if (sessionExtractionCoreMode)
        {
            progress.GetIslandProductionState(GetCapitalIslandId(), true)?.Normalize();
            progress.baseIndustry ??= new BaseExtractionIndustryState();
            progress.baseIndustry.Normalize();
            return;
        }

        IslandProductionSimulator.EnsureIslandStates(worldConfig, progress);
        IslandIndustrySimulator.EnsureIslandStates(worldConfig, progress);
    }

    private void EnsureLegacyAutonomousFleetRuntime()
    {
        if (sessionExtractionCoreMode || progress == null) return;

        logisticsFleet?.EnsureRuntimeShips(progress);
        gasHarvesterFleet?.EnsureRuntimeShips(progress);
        miningFleet?.EnsureRuntimeShips(progress);
        scoutFleet?.EnsureRuntimeShips(progress);
    }

    private class CargoPlanEntry
    {
        public string itemId = "";
        public int targetShipAmount;
        public string destroyAmountText = "1";
    }

    private struct CargoCapacityInfo
    {
        public bool assemblyValid;
        public bool canFly;
        public string reason;
        public float emptyMassKg;
        public float currentCargoKg;
        public float currentTankKg;
        public float maxCargoKg;
        public float fuelTankCapacityKg;
        public float claudiumTankCapacityKg;
        public float allowedTakeoffMassKg;
        public float engineLiftKg;
        public float claudiumMaxLiftKg;
        public float hullLimitKg;
        public List<CargoCompartmentDefinition> cargoCompartments;
    }

    private void EnsureWorldConfigLoaded()
    {
        if (worldConfig == null)
        {
            worldConfig = new WorldConfigDatabase();
        }

        if (!worldConfig.isLoaded)
        {
            worldConfig.LoadFromAssetsConfigFolder(worldConfigFolder);
            SyncCsvShipPartConfigs();
        }
    }

    private void ReloadWorldConfigs()
    {
        if (worldConfig == null)
        {
            worldConfig = new WorldConfigDatabase();
        }

        worldConfig.LoadFromAssetsConfigFolder(worldConfigFolder);
        SyncCsvShipPartConfigs();
        if (progress != null)
        {
            progress.Normalize();
            SyncSessionExtractionCoreMode();
            EnsureEconomyRuntimeStates();
            EnsureLogisticsFleet();
            EnsureGasSystems();
            EnsureMiningSystems();
            EnsureLeviathanSystems();
            EnsureScoutSystems();
            EnsureLegacyAutonomousFleetRuntime();
        }
    }

    private void SyncCsvShipPartConfigs()
    {
        if (worldConfig == null || !worldConfig.isLoaded) return;

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null) return;

        ShipAssemblyBuilder.ApplyCsvShipPartConfigs(activeCatalog, worldConfig);
    }

    private void EnsureLogisticsFleet()
    {
        if (logisticsFleet == null)
        {
            logisticsFleet = GetComponent<LogisticsFleetController>();
        }

        if (logisticsFleet == null)
        {
            logisticsFleet = FindFirstObjectByType<LogisticsFleetController>();
        }

        if (logisticsFleet == null && Application.isPlaying)
        {
            logisticsFleet = gameObject.AddComponent<LogisticsFleetController>();
            logisticsFleet.CreateExampleSetupIfEmpty();
        }

        if (logisticsFleet != null && logisticsFleet.metaGameState == null)
        {
            logisticsFleet.metaGameState = this;
        }
    }

    private void EnsureGasSystems()
    {
        if (gasCloudManager == null)
        {
            gasCloudManager = GetComponent<GasCloudManager>();
        }

        if (gasCloudManager == null)
        {
            gasCloudManager = FindFirstObjectByType<GasCloudManager>();
        }

        if (gasCloudManager == null && Application.isPlaying)
        {
            gasCloudManager = gameObject.AddComponent<GasCloudManager>();
        }

        if (gasCloudManager != null && gasCloudManager.metaGameState == null)
        {
            gasCloudManager.metaGameState = this;
        }

        if (gasHarvesterFleet == null)
        {
            gasHarvesterFleet = GetComponent<GasHarvesterFleetController>();
        }

        if (gasHarvesterFleet == null)
        {
            gasHarvesterFleet = FindFirstObjectByType<GasHarvesterFleetController>();
        }

        if (gasHarvesterFleet == null && Application.isPlaying)
        {
            gasHarvesterFleet = gameObject.AddComponent<GasHarvesterFleetController>();
            gasHarvesterFleet.CreateExampleSetupIfEmpty();
        }

        if (gasHarvesterFleet != null && gasHarvesterFleet.metaGameState == null)
        {
            gasHarvesterFleet.metaGameState = this;
        }
    }

    private void EnsureMiningSystems()
    {
        if (miningRockManager == null)
        {
            miningRockManager = GetComponent<MiningRockManager>();
        }

        if (miningRockManager == null)
        {
            miningRockManager = FindFirstObjectByType<MiningRockManager>();
        }

        if (miningRockManager == null && Application.isPlaying)
        {
            miningRockManager = gameObject.AddComponent<MiningRockManager>();
        }

        if (miningRockManager != null && miningRockManager.metaGameState == null)
        {
            miningRockManager.metaGameState = this;
        }

        if (miningFleet == null)
        {
            miningFleet = GetComponent<MiningFleetController>();
        }

        if (miningFleet == null)
        {
            miningFleet = FindFirstObjectByType<MiningFleetController>();
        }

        if (miningFleet == null && Application.isPlaying)
        {
            miningFleet = gameObject.AddComponent<MiningFleetController>();
            miningFleet.CreateExampleSetupIfEmpty();
        }

        if (miningFleet != null && miningFleet.metaGameState == null)
        {
            miningFleet.metaGameState = this;
        }
    }

    private void EnsureLeviathanSystems()
    {
        if (leviathanManager == null)
        {
            leviathanManager = GetComponent<LeviathanManager>();
        }

        if (leviathanManager == null)
        {
            leviathanManager = FindFirstObjectByType<LeviathanManager>();
        }

        if (leviathanManager == null && Application.isPlaying)
        {
            leviathanManager = gameObject.AddComponent<LeviathanManager>();
        }

        if (leviathanManager != null && leviathanManager.metaGameState == null)
        {
            leviathanManager.metaGameState = this;
        }
    }

    private void EnsureScoutSystems()
    {
        if (scoutFleet == null)
        {
            scoutFleet = GetComponent<ScoutFleetController>();
        }

        if (scoutFleet == null)
        {
            scoutFleet = FindFirstObjectByType<ScoutFleetController>();
        }

        if (scoutFleet == null && Application.isPlaying)
        {
            scoutFleet = gameObject.AddComponent<ScoutFleetController>();
            scoutFleet.CreateExampleSetupIfEmpty();
        }

        if (scoutFleet != null && scoutFleet.metaGameState == null)
        {
            scoutFleet.metaGameState = this;
        }
    }

    private void SpawnConfiguredIslands(bool forceRebuild = false)
    {
        if (!Application.isPlaying || !spawnConfigIslandsOnPlay) return;

        EnsureWorldConfigLoaded();
        if (worldConfig == null || !worldConfig.isLoaded) return;

        if (spawnedConfigIslandRoot != null && !forceRebuild)
        {
            return;
        }

        if (spawnedConfigIslandRoot != null)
        {
            Destroy(spawnedConfigIslandRoot.gameObject);
            spawnedConfigIslandRoot = null;
        }

        GameObject root = new GameObject("Острова из конфигов");
        spawnedConfigIslandRoot = root.transform;

        ShipPhysics activeShip = GetActiveShip();
        for (int i = 0; i < worldConfig.islands.Count; i++)
        {
            IslandConfig island = worldConfig.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;
            if (sessionExtractionCoreMode && !IsCapitalIsland(island.id)) continue;

            GameObject islandObject = new GameObject(GetIslandDisplayName(island));
            islandObject.transform.SetParent(spawnedConfigIslandRoot, false);
            islandObject.transform.position = island.position;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Визуал острова";
            visual.transform.SetParent(islandObject.transform, false);
            float visualRadius = Mathf.Max(1f, configIslandVisualRadius);
            visual.transform.localScale = new Vector3(visualRadius * 2f, 8f, visualRadius * 2f);
            visual.transform.localPosition = Vector3.down * 8f;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.28f, 0.46f, 0.26f);
            }

            DockingPort dock = islandObject.AddComponent<DockingPort>();
            dock.metaGameState = this;
            dock.targetShip = activeShip;
            dock.dockId = island.id;
            dock.displayName = GetIslandDisplayName(island);
            dock.kind = DockingLocationKind.Island;
            dock.dockingRadius = Mathf.Max(0.1f, island.dockingRadius);
            dock.canEndSession = true;
            dock.autoDockWhenInRange = false;
            dock.requireLeaveBeforeRedocking = true;
            dock.snapPoint = islandObject.transform;

            if (IsCapitalIsland(island.id))
            {
                CapitalResearchStation station = islandObject.AddComponent<CapitalResearchStation>();
                station.metaGameState = this;
                station.islandId = island.id;
                station.displayName = GetIslandDisplayName(island);
            }
        }
    }

    public void RefreshSessionExtractionRuntimeActors()
    {
        SpawnConfiguredWorldActors(true);
    }

    private void SpawnConfiguredWorldActors(bool forceRebuild = false)
    {
        SpawnConfiguredIslands(forceRebuild);
        if (sessionExtractionCoreMode)
        {
            ClearLegacyWorldActors();
            return;
        }

        gasCloudManager?.SpawnConfiguredClouds(forceRebuild);
        miningRockManager?.SpawnConfiguredRocks(forceRebuild);
        leviathanManager?.SpawnConfiguredLeviathans(forceRebuild);
    }

    private void ClearLegacyWorldActors()
    {
        gasCloudManager?.ClearConfiguredClouds();
        miningRockManager?.ClearConfiguredRocks();
        leviathanManager?.ClearConfiguredLeviathans();
    }

    public bool IsCapitalIsland(string islandId)
    {
        string expectedId = string.IsNullOrWhiteSpace(capitalIslandId) ? "capital" : capitalIslandId;
        return !string.IsNullOrWhiteSpace(islandId) && islandId == expectedId;
    }

    private static string GetIslandDisplayName(IslandConfig island)
    {
        if (island == null) return "Остров";
        return string.IsNullOrWhiteSpace(island.localNameRu) ? island.id : island.localNameRu;
    }

    private void Reset()
    {
        shipLoader = FindFirstObjectByType<ShipLoader>();
        missionController = FindFirstObjectByType<MissionController>();
        logisticsFleet = FindFirstObjectByType<LogisticsFleetController>();
        gasCloudManager = FindFirstObjectByType<GasCloudManager>();
        gasHarvesterFleet = FindFirstObjectByType<GasHarvesterFleetController>();
        miningRockManager = FindFirstObjectByType<MiningRockManager>();
        miningFleet = FindFirstObjectByType<MiningFleetController>();
        leviathanManager = FindFirstObjectByType<LeviathanManager>();
        scoutFleet = FindFirstObjectByType<ScoutFleetController>();
    }

    private void Awake()
    {
        CacheUnityTimeSettings();
        ResetProcessRealtimeClock();
        ReloadWorldConfigs();

        if (shipLoader == null)
        {
            shipLoader = FindFirstObjectByType<ShipLoader>();
        }

        if (missionController == null)
        {
            missionController = FindFirstObjectByType<MissionController>();
        }

        EnsureLogisticsFleet();
        EnsureGasSystems();
        EnsureMiningSystems();
        EnsureLeviathanSystems();
        EnsureScoutSystems();

        bool loadedGame = false;
        if (loadSavedGameOnAwake && !WildWindBigTestRunner.IsMainWorldCheckInProgress)
        {
            loadedGame = LoadGame();
        }

        EnsureProgressInitialized();
        SpawnConfiguredWorldActors();
        if (!skipInitialProcessCatchUp && (!loadedGame || !TryAdvanceOfflineProgressFromLastSave(DateTime.UtcNow, out _)))
        {
            AdvanceRealTimeProcessesSliced(DateTime.UtcNow);
        }

        ResetProcessRealtimeClock();
        ApplySelectedShip();
        ApplySessionModeToShip();
        InstallCrashDetectorIfNeeded();
    }

    private void Start()
    {
        EnsureGasSystems();
        EnsureMiningSystems();
        EnsureLeviathanSystems();
        EnsureScoutSystems();
        SpawnConfiguredWorldActors();
        ApplySelectedShip();
        ApplySessionModeToShip();
    }

    private void Update()
    {
        ApplyUnityTimeScale();
        if (sessionPaused)
        {
            return;
        }

        if (processRealTimeWhilePlaying)
        {
            EnsureProgressInitialized();
            AdvanceScaledRealTimeProcesses();
        }

        SyncShipConsumablesWithCargo(false);
        UpdateActiveSortieExtractionRunupFromActiveShip();
    }

    private void OnApplicationQuit()
    {
        TrySaveGame(true);
        RestoreUnityTimeSettings();
    }

    private void OnDisable()
    {
        RestoreUnityTimeSettings();
    }

    public void EnsureProgressInitialized()
    {
        progress ??= new PlayerProgress();
        progress.Normalize();
        EnsureWorldConfigLoaded();
        SyncSessionExtractionCoreMode();
        EnsureEconomyRuntimeStates();
        EnsureLogisticsFleet();
        EnsureGasSystems();
        EnsureMiningSystems();
        EnsureLeviathanSystems();
        EnsureScoutSystems();
        EnsureLegacyAutonomousFleetRuntime();
        if (sessionExtractionCoreMode)
        {
            MigrateLegacyPersonalInventoryToCapitalStorage();
            DisableLegacyShopForSessionCore();
        }

        if (initialized) return;

        if (progress == null)
        {
            progress = new PlayerProgress();
        }

        progress.Normalize();
        SyncSessionExtractionCoreMode();
        EnsureEconomyRuntimeStates();
        EnsureLegacyAutonomousFleetRuntime();
        if (sessionExtractionCoreMode)
        {
            MigrateLegacyPersonalInventoryToCapitalStorage();
            DisableLegacyShopForSessionCore();
        }

        if (progress.lastSavedUtcTicks == 0 && string.IsNullOrWhiteSpace(progress.currentDockId))
        {
            progress.SetDocked(startingDockId, startingDockKind);
        }

        if (progress.lastSavedUtcTicks == 0)
        {
            progress.currentMode = startingMode;
            if (startingMode == GameSessionMode.Docked)
            {
                progress.SetDocked(startingDockId, startingDockKind);
            }
        }

        ShipPartDefinitionSO starterHull = ActiveCatalog != null ? ActiveCatalog.GetStarterHull() : null;
        if (starterHull != null)
        {
            progress.EnsureStarterHull(starterHull.partId);
        }

        if (progress.money < startingMoney)
        {
            if (!sessionExtractionCoreMode)
            {
                progress.money = startingMoney;
            }
        }

        if (!progress.receivedStartingInventory)
        {
            if (!sessionExtractionCoreMode)
            {
                progress.AddResource("ore", startingOre);
                progress.AddResource("iron", startingIron);
            }

            AddStartingShipConsumables();
            progress.receivedStartingInventory = true;
        }

        if (!progress.receivedStartingPaper)
        {
            if (!sessionExtractionCoreMode)
            {
                AddStartingIslandSupplies();
                WildWindStarterDelivery.SeedNewGame(progress);
            }

            progress.receivedStartingPaper = true;
        }

        long nowTicks = DateTime.UtcNow.Ticks;
        if (progress.lastProcessUtcTicks == 0)
        {
            progress.lastProcessUtcTicks = nowTicks;
        }

        if (sessionExtractionCoreMode)
        {
            DisableLegacyShopForSessionCore();
        }
        else if (progress.nextShopRefreshUtcTicks == 0)
        {
            progress.nextShopRefreshUtcTicks = nowTicks + TimeSpan.FromSeconds(Mathf.Max(1, shopRefreshIntervalSeconds)).Ticks;
            progress.shopSeed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        ApplyStartingTechTreeNodes();
        ApplyStartingTechnologies();
        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out _);
        initialized = true;
    }

    private int MigrateLegacyPersonalInventoryToCapitalStorage()
    {
        if (!sessionExtractionCoreMode || progress == null || progress.inventory == null || progress.inventory.Count == 0)
        {
            return 0;
        }

        IslandProductionState capitalStorage = progress.GetIslandProductionState(GetCapitalIslandId(), true);
        if (capitalStorage == null)
        {
            return 0;
        }

        int moved = 0;
        for (int i = 0; i < progress.inventory.Count; i++)
        {
            ResourceStack stack = progress.inventory[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;

            moved += capitalStorage.AddResource(stack.resourceId, stack.amount);
        }

        progress.inventory.Clear();
        return moved;
    }

    private void DisableLegacyShopForSessionCore()
    {
        if (progress == null) return;

        progress.nextShopRefreshUtcTicks = 0L;
        progress.shopSeed = 0;
    }

    public PlayerProgress CreateProgressSnapshot()
    {
        EnsureProgressInitialized();
        return progress.Clone();
    }

    public void ReplaceProgress(PlayerProgress newProgress)
    {
        progress = newProgress != null ? newProgress.Clone() : new PlayerProgress();
        initialized = false;
        EnsureProgressInitialized();
        ApplySelectedShip();
    }

    public bool ApplySelectedShip()
    {
        if (shipLoader == null) return false;

        EnsureProgressInitialized();
        SyncCsvShipPartConfigs();
        if (shipLoader.catalog == null)
        {
            shipLoader.catalog = ActiveCatalog;
        }

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog != null && activeCatalog.HasAssemblyParts())
        {
            if (!shipLoader.ApplyAssembly(progress, techTree, out string message))
            {
                lastSaveMessage = message;
                return false;
            }
            else
            {
                RefreshSceneShipReferences(shipLoader.targetShip);
                ApplyFuelConfigToShip(shipLoader.targetShip);
                RefreshShipConsumablesFromTanks(shipLoader.targetShip, true);
                shipLoader.targetShip.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(worldConfig));
                shipLoader.targetShip.RefreshRuntimeShipSettings();
            }

            return true;
        }

        return false;
    }

    public bool CanAssembleCurrentShip(out string reason)
    {
        EnsureProgressInitialized();
        reason = "";

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null || !activeCatalog.HasAssemblyParts())
        {
            return true;
        }

        if (ShipAssemblyBuilder.TryBuild(activeCatalog, techTree, progress, out ShipAssemblyResult result))
        {
            reason = result.message;
            return true;
        }

        reason = result.message;
        return false;
    }

    public bool SelectHull(string hullId)
    {
        EnsureProgressInitialized();
        ShipCatalogSO activeCatalog = ActiveCatalog;
        ShipPartDefinitionSO hull = activeCatalog != null ? activeCatalog.GetPartById(hullId) : null;
        if (hull == null || !hull.IsHull || !ShipAssemblyBuilder.IsPartUsable(hull, techTree, progress)) return false;
        if (sessionExtractionCoreMode && progress.selectedHullId != hullId)
        {
            lastSaveMessage = "Legacy hull selector is disabled in session extraction core; ship replacement must use base assembly.";
            return false;
        }

        if (!progress.SelectHull(hullId)) return false;

        ApplySelectedShip();
        AutoSaveIfDocked();
        return true;
    }

    public bool InstallModule(string slotId, string moduleId)
    {
        EnsureProgressInitialized();
        if (!IsDocked) return false;
        if (sessionExtractionCoreMode && !CanInstallSessionCoreFittingModule(slotId, moduleId, out string reason))
        {
            lastSaveMessage = reason;
            return false;
        }

        progress.InstallModule(slotId, moduleId);
        ApplySelectedShip();
        AutoSaveIfDocked();
        return true;
    }

    private bool CanInstallSessionCoreFittingModule(string slotId, string moduleId, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(slotId))
        {
            reason = "No fitting slot selected.";
            return false;
        }

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null)
        {
            reason = "Ship catalog is missing.";
            return false;
        }

        ShipSlotDefinition slot = FindCurrentAssemblySlotForCoreFitting(activeCatalog, slotId);
        if (slot == null)
        {
            reason = "Slot is not available in session extraction fitting: " + slotId + ".";
            return false;
        }

        if (!IsSessionCoreFittingSlotType(slot.slotTypeId))
        {
            reason = "Session extraction fitting accepts only High, Mid, Low, and Rig module slots.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return true;
        }

        ShipPartDefinitionSO module = activeCatalog.GetPartById(moduleId);
        if (module == null || !module.IsModule)
        {
            reason = "Module is missing: " + moduleId + ".";
            return false;
        }

        if (!ShipAssemblyBuilder.IsPartUsable(module, techTree, progress))
        {
            reason = "Module is not researched: " + GetPartName(module) + ".";
            return false;
        }

        if (!module.CanFitSlot(slot))
        {
            reason = GetPartName(module) + " cannot fit " + GetSlotTypeDisplayName(slot.slotTypeId) + ".";
            return false;
        }

        return true;
    }

    private ShipSlotDefinition FindCurrentAssemblySlotForCoreFitting(ShipCatalogSO activeCatalog, string slotId)
    {
        List<ShipSlotDefinition> slots = GetAssemblySlotsForUi(activeCatalog);
        for (int i = 0; i < slots.Count; i++)
        {
            ShipSlotDefinition slot = slots[i];
            if (slot != null && slot.slotId == slotId)
            {
                return slot;
            }
        }

        return null;
    }

    private static bool IsSessionCoreFittingSlotType(string slotTypeId)
    {
        return slotTypeId == SessionExtractionConstants.HighSlotTypeId
            || slotTypeId == SessionExtractionConstants.MidSlotTypeId
            || slotTypeId == SessionExtractionConstants.LowSlotTypeId
            || slotTypeId == SessionExtractionConstants.RigSlotTypeId;
    }

    public void AddMoney(int amount)
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy money rewards are disabled in session extraction core.";
            return;
        }

        progress.money += Mathf.Max(0, amount);
    }

    public void AddResource(string resourceId, int amount)
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy direct resource rewards are disabled in session extraction core.";
            return;
        }

        progress.AddResource(resourceId, amount);
    }

    public void AddExperienceToSelectedShip(int amount)
    {
        EnsureProgressInitialized();
        AddExperienceToShip(GetSelectedExperienceTargetId(), amount);
    }

    public void AddExperienceToShip(string shipId, int amount)
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy ship XP rewards are disabled in session extraction core.";
            return;
        }

        progress.AddShipExperience(shipId, amount);
    }

    public bool TryResearchNode(string nodeId)
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy XP tech-tree research is disabled in session extraction core. Use base resource technology cycles and cascade production.";
            return false;
        }

        if (techTree == null) return false;

        TechTreeNode node = techTree.GetNode(nodeId);
        if (!TechTreeRules.CanResearch(node, progress, out string experienceShipId, out _)) return false;

        if (!progress.TrySpendShipExperience(experienceShipId, node.researchCostXp)) return false;

        progress.ResearchNode(node.nodeId);
        if (!node.RequiresPurchase)
        {
            progress.PurchaseNode(node.nodeId);
        }

        AutoSaveIfDocked();
        return true;
    }

    public bool TryPurchaseNode(string nodeId)
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy money tech-tree purchases are disabled in session extraction core. Use base resource technology cycles and cascade production.";
            return false;
        }

        if (techTree == null) return false;

        TechTreeNode node = techTree.GetNode(nodeId);
        if (!TechTreeRules.CanPurchase(node, progress, out _)) return false;

        progress.money -= Mathf.Max(0, node.purchasePrice);
        progress.PurchaseNode(node.nodeId);

        if (node.kind == TechTreeNodeKind.Hull && string.IsNullOrWhiteSpace(progress.selectedHullId))
        {
            progress.SelectHull(node.EffectivePartId);
        }

        AutoSaveIfDocked();
        return true;
    }

    public IReadOnlyList<TechnologyConfig> GetTechnologyConfigs()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return worldConfig != null ? worldConfig.technologies : null;
    }

    public IslandProductionState GetCapitalStorageState()
    {
        EnsureProgressInitialized();
        return progress.GetIslandProductionState(GetCapitalIslandId(), true);
    }

    public TechnologyResearchProgress GetTechnologyResearchProgress(string technologyId)
    {
        EnsureProgressInitialized();
        return progress.GetTechnologyProgress(technologyId, false);
    }

    public bool IsTechnologyCompleted(string technologyId)
    {
        EnsureProgressInitialized();
        return progress.IsTechnologyCompleted(technologyId);
    }

    public bool IsDockedAtCapital()
    {
        EnsureProgressInitialized();
        return IsDocked
            && progress.currentDockKind == DockingLocationKind.Island
            && IsCapitalIsland(progress.currentDockId);
    }

    public string GetCapitalIslandId()
    {
        return string.IsNullOrWhiteSpace(capitalIslandId) ? "capital" : capitalIslandId;
    }

    public bool TrySelectResearchTechnology(string technologyId)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        TechnologyConfig technology = worldConfig != null ? worldConfig.GetTechnology(technologyId) : null;
        if (!CanSelectResearchTechnology(technology, out string reason))
        {
            lastSaveMessage = reason;
            return false;
        }

        progress.activeResearchTechnologyId = technology.id;
        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        state.completedCycles = Mathf.Clamp(state.completedCycles, 0, Mathf.Max(1, technology.requiredCycles));

        TryStartOrContinueResearchCycle(technology, state, GetProcessUtcNow().Ticks, out reason);
        lastSaveMessage = string.IsNullOrWhiteSpace(reason) ? "Исследование выбрано: " + GetTechnologyDisplayName(technology) : reason;
        AutoSaveIfDocked();
        return true;
    }

    public bool CanSelectResearchTechnology(TechnologyConfig technology, out string reason)
    {
        reason = "";
        if (technology == null)
        {
            reason = "Технология не найдена.";
            return false;
        }

        if (!IsDockedAtCapital())
        {
            reason = "Исследования можно выбирать только в столице.";
            return false;
        }

        if (progress.IsTechnologyCompleted(technology.id))
        {
            reason = "Технология уже завершена.";
            return false;
        }

        if (!AreTechnologyPrerequisitesCompleted(technology, out reason))
        {
            return false;
        }

        string activeId = progress.activeResearchTechnologyId;
        if (!string.IsNullOrWhiteSpace(activeId) && activeId != technology.id)
        {
            TechnologyResearchProgress activeState = progress.GetTechnologyProgress(activeId, false);
            if (activeState != null && activeState.HasActiveCycle)
            {
                reason = "Сначала завершится текущий цикл исследования.";
                return false;
            }
        }

        return true;
    }

    public bool HasCapitalResourcesForCycle(TechnologyConfig technology)
    {
        if (technology == null) return false;
        IslandProductionState storage = GetCapitalStorageState();
        return HasTechnologyCycleCost(storage, technology);
    }

    public string GetTechnologyDisplayName(TechnologyConfig technology)
    {
        if (technology == null) return "";
        return string.IsNullOrWhiteSpace(technology.localNameRu) ? technology.id : technology.localNameRu;
    }

    public string FormatTechnologyCycleCost(TechnologyConfig technology)
    {
        if (technology == null || technology.cycleCost == null || technology.cycleCost.Count == 0)
        {
            return "без ресурсов";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < technology.cycleCost.Count; i++)
        {
            TechnologyCostConfig cost = technology.cycleCost[i];
            if (cost == null || string.IsNullOrWhiteSpace(cost.itemId) || cost.amount <= 0) continue;
            parts.Add(worldConfig.GetItemNameRu(cost.itemId) + " x" + cost.amount);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "без ресурсов";
    }

    public string GetTechnologyStatusText(TechnologyConfig technology)
    {
        if (technology == null) return "";
        EnsureProgressInitialized();

        if (progress.IsTechnologyCompleted(technology.id))
        {
            return "завершена";
        }

        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, false);
        int completedCycles = state != null ? state.completedCycles : 0;
        string cycleText = completedCycles + "/" + Mathf.Max(1, technology.requiredCycles);

        if (progress.activeResearchTechnologyId != technology.id)
        {
            return "не выбрана, циклы " + cycleText;
        }

        if (state != null && state.HasActiveCycle)
        {
            return "идет цикл, осталось " + FormatRemaining(state.activeCycleEndUtcTicks) + ", циклы " + cycleText;
        }

        return HasCapitalResourcesForCycle(technology)
            ? "ждет запуска цикла, циклы " + cycleText
            : "ждет ресурсы, циклы " + cycleText;
    }

    public bool TryBeginFlightSession(MissionDefinitionSO mission)
    {
        return TryBeginFlightSession(mission, null, false);
    }

    private bool TryBeginFlightSession(MissionDefinitionSO mission, FlagshipExpeditionDefinition expedition, bool allowSessionExtractionCoreSortie)
    {
        EnsureProgressInitialized();
        expedition?.Normalize();

        if (sessionExtractionCoreMode && !allowSessionExtractionCoreSortie)
        {
            progress.activeExpedition?.Clear();
            lastSaveMessage = "Legacy flight missions are disabled in session extraction core. Start a sortie from the base.";
            return false;
        }

        if (CurrentMode == GameSessionMode.Flight)
        {
            return true;
        }

        if (!IsDocked)
        {
            return false;
        }

        if (!AutoInstallRequiredModules(false, out string autoInstallReason))
        {
            lastSaveMessage = "Нельзя вылететь: " + autoInstallReason;
            return false;
        }

        if (!CanAssembleCurrentShip(out string assemblyReason))
        {
            lastSaveMessage = "Нельзя вылететь: " + assemblyReason;
            return false;
        }

        if (progress.cargoTransfer != null && progress.cargoTransfer.active)
        {
            lastSaveMessage = "Нельзя вылететь: погрузка еще идет.";
            return false;
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (!capacity.canFly)
        {
            lastSaveMessage = "Нельзя вылететь: " + capacity.reason;
            return false;
        }

        string missionId = mission != null ? mission.missionId : "";
        progress.AcceptMission(missionId);

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        if (!sessionExtractionCoreMode && expedition != null)
        {
            progress.activeExpedition.Begin(expedition, GetProcessUtcNow().Ticks);
        }
        else if (sessionExtractionCoreMode)
        {
            progress.activeExpedition?.Clear();
        }

        progress.SetFlight(missionId);
        string flagshipExpeditionMessage = "";
        bool flagshipExpeditionStarted = !sessionExtractionCoreMode
            && TryStartPlayerFlagshipExpedition(out flagshipExpeditionMessage);
        ApplySelectedShip();
        ApplySessionModeToShip();
        lastSaveMessage = "Вылет начат. Ручное сохранение доступно у дока, выход из игры сохранит текущий полет.";
        if (expedition != null)
        {
            lastSaveMessage += " Экспедиция: " + GetExpeditionDisplayName(expedition) + ".";
        }
        if (flagshipExpeditionStarted && !string.IsNullOrWhiteSpace(flagshipExpeditionMessage))
        {
            lastSaveMessage += " " + flagshipExpeditionMessage;
        }
        return true;
    }

    public bool BeginFreeFlight()
    {
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Free flight is disabled in session extraction core. Start a sortie from the base.";
            return false;
        }

        return TryBeginFlightSession(null);
    }

    public SortieZoneDefinition CreateDefaultSafeOreSortieDefinition()
    {
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeOreSortieId,
            displayName = SessionExtractionConstants.DefaultSafeOreSortieName,
            primaryBranch = BaseProcessingBranch.Ore,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "No required High module",
            starterResourceItemId = "windshale_ore",
            starterResourceChunkMin = 2,
            starterResourceChunkMax = 5,
            starterResourceShedIntervalSeconds = 1.75f,
            starterResourceColor = new Color(0.55f, 0.50f, 0.45f, 1f),
            centerPosition = Vector3.zero,
            entryPosition = new Vector3(0f, 350f, 0f),
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters,
            stormFloorY = 0f,
            extractionBoundaryToleranceMeters = 150f,
            distanceToBaseKm = 220f,
            returnCruiseSpeedMS = 35f,
            returnPowerLever = 0.7f,
            returnReserveMultiplier = 1.15f
        };
    }

    public List<SortieZoneDefinition> CreateDefaultSessionSortieDefinitions()
    {
        List<SortieZoneDefinition> sorties = new List<SortieZoneDefinition>
        {
            CreateDefaultSafeOreSortieDefinition(),
            CreateDefaultSafeGasSortieDefinition(),
            CreateDefaultSafeAutomatonSortieDefinition(),
            CreateDefaultSafeLeviathanSortieDefinition(),
            CreateDefaultSafeSurveySortieDefinition()
        };

        for (int i = 0; i < sorties.Count; i++)
        {
            sorties[i]?.Normalize();
        }

        return sorties;
    }

    public SortieZoneDefinition CreateDefaultSafeGasSortieDefinition()
    {
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeGasSortieId,
            displayName = SessionExtractionConstants.DefaultSafeGasSortieName,
            primaryBranch = BaseProcessingBranch.Gas,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "High gas harvester",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterGasHarvesterModuleId },
            starterResourceItemId = "cloud_condensate",
            starterResourceChunkMin = 2,
            starterResourceChunkMax = 4,
            starterResourceShedIntervalSeconds = 2.1f,
            starterResourceColor = new Color(0.65f, 0.82f, 1f, 1f),
            centerPosition = new Vector3(900f, 0f, 0f),
            entryPosition = new Vector3(900f, 360f, 0f),
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters,
            stormFloorY = 0f,
            extractionBoundaryToleranceMeters = 150f,
            distanceToBaseKm = 220f,
            returnCruiseSpeedMS = 35f,
            returnPowerLever = 0.7f,
            returnReserveMultiplier = 1.15f
        };
    }

    public SortieZoneDefinition CreateDefaultSafeAutomatonSortieDefinition()
    {
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeAutomatonSortieId,
            displayName = SessionExtractionConstants.DefaultSafeAutomatonSortieName,
            primaryBranch = BaseProcessingBranch.AutomatonDismantling,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "High impact or salvage module",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterMiningHoldModuleId },
            starterResourceItemId = SessionExtractionConstants.BrokenAutomatonItemId,
            starterResourceChunkMin = 1,
            starterResourceChunkMax = 2,
            starterResourceShedIntervalSeconds = 3.0f,
            starterResourceColor = new Color(0.78f, 0.76f, 0.68f, 1f),
            centerPosition = new Vector3(-900f, 0f, 650f),
            entryPosition = new Vector3(-900f, 340f, 650f),
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters,
            stormFloorY = 0f,
            extractionBoundaryToleranceMeters = 150f,
            distanceToBaseKm = 220f,
            returnCruiseSpeedMS = 35f,
            returnPowerLever = 0.7f,
            returnReserveMultiplier = 1.15f
        };
    }

    public SortieZoneDefinition CreateDefaultSafeLeviathanSortieDefinition()
    {
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeLeviathanSortieId,
            displayName = SessionExtractionConstants.DefaultSafeLeviathanSortieName,
            primaryBranch = BaseProcessingBranch.LeviathanProcessing,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "High harpoon module",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterHarpoonModuleId },
            starterResourceItemId = "windcalf_carcass",
            starterResourceChunkMin = 2,
            starterResourceChunkMax = 5,
            starterResourceShedIntervalSeconds = 3.4f,
            starterResourceColor = new Color(0.56f, 0.78f, 0.74f, 1f),
            centerPosition = new Vector3(0f, 0f, -1200f),
            entryPosition = new Vector3(0f, 380f, -1200f),
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters,
            stormFloorY = 0f,
            extractionBoundaryToleranceMeters = 160f,
            distanceToBaseKm = 220f,
            returnCruiseSpeedMS = 35f,
            returnPowerLever = 0.7f,
            returnReserveMultiplier = 1.15f
        };
    }

    public SortieZoneDefinition CreateDefaultSafeSurveySortieDefinition()
    {
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeSurveySortieId,
            displayName = SessionExtractionConstants.DefaultSafeSurveySortieName,
            primaryBranch = BaseProcessingBranch.CyberneticDeciphering,
            recommendedSlotBand = ShipFittingSlotBand.Mid,
            requiredFittingSummary = "Mid observation module",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterObservationPostModuleId },
            starterResourceItemId = SessionExtractionConstants.RockInfoItemId,
            starterResourceChunkMin = 1,
            starterResourceChunkMax = 3,
            starterResourceShedIntervalSeconds = 2.8f,
            starterResourceColor = new Color(0.72f, 0.88f, 0.92f, 1f),
            centerPosition = new Vector3(1250f, 0f, 850f),
            entryPosition = new Vector3(1250f, 370f, 850f),
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters,
            stormFloorY = 0f,
            extractionBoundaryToleranceMeters = 140f,
            distanceToBaseKm = 220f,
            returnCruiseSpeedMS = 35f,
            returnPowerLever = 0.7f,
            returnReserveMultiplier = 1.15f
        };
    }

    public SortieZoneDefinition GetSelectedSessionSortieDefinition()
    {
        EnsureProgressInitialized();

        string selectedId = progress != null && !string.IsNullOrWhiteSpace(progress.selectedSortieId)
            ? progress.selectedSortieId
            : SessionExtractionConstants.DefaultSafeOreSortieId;
        SortieZoneDefinition selected = FindDefaultSessionSortieDefinition(selectedId);
        if (selected != null) return selected;

        progress.selectedSortieId = SessionExtractionConstants.DefaultSafeOreSortieId;
        return CreateDefaultSafeOreSortieDefinition();
    }

    public string GetSelectedSessionSortieDisplayName()
    {
        SortieZoneDefinition selected = GetSelectedSessionSortieDefinition();
        return selected != null ? selected.displayName : SessionExtractionConstants.DefaultSafeOreSortieName;
    }

    public string GetSelectedSessionSortieRequirementText()
    {
        return GetSortieRequirementText(GetSelectedSessionSortieDefinition());
    }

    public bool CanBeginSelectedSessionSortie(out string reason)
    {
        return CanBeginSessionExtractionSortie(GetSelectedSessionSortieDefinition(), out reason);
    }

    public bool CanBeginSessionExtractionSortie(SortieZoneDefinition zone, out string reason)
    {
        reason = "";
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        if (zone == null)
        {
            reason = "Sortie is missing.";
            return false;
        }

        zone.Normalize();
        if (!IsDockedAtCapital())
        {
            reason = "Session sortie can start only from the base.";
            return false;
        }

        if (!HasRequiredSortieModule(zone, out string moduleReason))
        {
            reason = moduleReason;
            return false;
        }

        reason = "Sortie ready: " + zone.displayName + ".";
        return true;
    }

    public bool SelectSessionSortie(string sortieId, out string message)
    {
        EnsureProgressInitialized();
        message = "";

        SortieZoneDefinition sortie = FindDefaultSessionSortieDefinition(sortieId);
        if (sortie == null)
        {
            message = "Unknown sortie: " + sortieId + ".";
            lastSaveMessage = message;
            return false;
        }

        progress.selectedSortieId = sortie.sortieId;
        message = "Selected sortie: " + sortie.displayName + ".";
        lastSaveMessage = message;
        AutoSaveIfDocked();
        return true;
    }

    public bool SelectNextSessionSortie(out string message)
    {
        EnsureProgressInitialized();
        List<SortieZoneDefinition> sorties = CreateDefaultSessionSortieDefinitions();
        if (sorties.Count == 0)
        {
            message = "No sorties are configured.";
            lastSaveMessage = message;
            return false;
        }

        string current = progress.selectedSortieId ?? "";
        int index = 0;
        for (int i = 0; i < sorties.Count; i++)
        {
            if (sorties[i] != null && sorties[i].sortieId == current)
            {
                index = (i + 1) % sorties.Count;
                break;
            }
        }

        return SelectSessionSortie(sorties[index].sortieId, out message);
    }

    public bool BeginSelectedSessionSortie()
    {
        return BeginSessionExtractionSortie(GetSelectedSessionSortieDefinition());
    }

    public bool BeginSafeOreSortie()
    {
        return BeginSessionExtractionSortie(CreateDefaultSafeOreSortieDefinition());
    }

    private SortieZoneDefinition FindDefaultSessionSortieDefinition(string sortieId)
    {
        if (string.IsNullOrWhiteSpace(sortieId)) return null;

        List<SortieZoneDefinition> sorties = CreateDefaultSessionSortieDefinitions();
        for (int i = 0; i < sorties.Count; i++)
        {
            SortieZoneDefinition sortie = sorties[i];
            if (sortie != null && sortie.sortieId == sortieId)
            {
                return sortie;
            }
        }

        return null;
    }

    private static bool IsDefaultSessionSortieId(string sortieId)
    {
        return sortieId == SessionExtractionConstants.DefaultSafeOreSortieId
            || sortieId == SessionExtractionConstants.DefaultSafeGasSortieId
            || sortieId == SessionExtractionConstants.DefaultSafeAutomatonSortieId
            || sortieId == SessionExtractionConstants.DefaultSafeLeviathanSortieId
            || sortieId == SessionExtractionConstants.DefaultSafeSurveySortieId;
    }

    private bool HasRequiredSortieModule(SortieZoneDefinition zone, out string reason)
    {
        reason = "";
        if (zone == null) return true;

        zone.Normalize();
        if (zone.requiredModuleIds == null || zone.requiredModuleIds.Count == 0)
        {
            return true;
        }

        if (!ShipAssemblyBuilder.TryBuild(ActiveCatalog, techTree, progress, out ShipAssemblyResult assembly))
        {
            reason = "Sortie blocked: ship assembly is invalid. " + (assembly != null ? assembly.message : "");
            return false;
        }

        string requiredSlotTypeId = SessionExtractionFitting.GetSlotTypeId(zone.recommendedSlotBand);
        for (int installedIndex = 0; installedIndex < assembly.installedModules.Count; installedIndex++)
        {
            InstalledModuleState installed = assembly.installedModules[installedIndex];
            if (installed == null || string.IsNullOrWhiteSpace(installed.moduleId)) continue;
            if (!AssemblySlotMatchesType(assembly, installed.slotId, requiredSlotTypeId)) continue;

            for (int requiredIndex = 0; requiredIndex < zone.requiredModuleIds.Count; requiredIndex++)
            {
                if (installed.moduleId == zone.requiredModuleIds[requiredIndex])
                {
                    return true;
                }
            }
        }

        reason = "Sortie blocked: " + zone.displayName + " requires " + GetSortieRequirementText(zone) + ".";
        return false;
    }

    private static bool AssemblySlotMatchesType(ShipAssemblyResult assembly, string slotId, string slotTypeId)
    {
        if (assembly == null || string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(slotTypeId))
        {
            return false;
        }

        for (int i = 0; i < assembly.slots.Count; i++)
        {
            ShipSlotDefinition slot = assembly.slots[i];
            if (slot != null && slot.slotId == slotId)
            {
                return slot.slotTypeId == slotTypeId;
            }
        }

        return false;
    }

    private static string GetSortieRequirementText(SortieZoneDefinition zone)
    {
        if (zone == null) return "";

        zone.Normalize();
        if (!string.IsNullOrWhiteSpace(zone.requiredFittingSummary))
        {
            return zone.requiredFittingSummary;
        }

        if (zone.requiredModuleIds == null || zone.requiredModuleIds.Count == 0)
        {
            return "no required module";
        }

        return string.Join("/", zone.requiredModuleIds);
    }

    public bool BeginSessionExtractionSortie(SortieZoneDefinition zone)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        SortieZoneDefinition sortieZone = zone != null ? zone.Clone() : CreateDefaultSafeOreSortieDefinition();
        sortieZone.Normalize();
        if (!CanBeginSessionExtractionSortie(sortieZone, out string beginReason))
        {
            lastSaveMessage = beginReason;
            return false;
        }

        string launchDockId = progress.currentDockId;
        DockingLocationKind launchDockKind = progress.currentDockKind;
        Vector3 launchDockPosition = progress.hasCurrentDockPosition
            ? progress.currentDockPosition
            : GetCurrentShipPosition();

        if (!TryBeginFlightSession(null, null, true))
        {
            return false;
        }

        SortieEntryState entryState = BuildSortieEntryState(sortieZone, launchDockPosition, GetActiveShip());
        sortieZone.entryPosition = entryState.position;
        progress.BeginSortie(sortieZone, GetProcessUtcNow().Ticks, launchDockId, launchDockKind, launchDockPosition);
        PlaceShipAtSortieEntry(sortieZone, entryState);

        string saveFailure = "";
        if (!TrySaveGame(true))
        {
            saveFailure = " Save failed: " + lastSaveMessage;
        }

        lastSaveMessage = "Session sortie started: " + sortieZone.displayName + "." + saveFailure;
        return true;
    }

    public SortieReturnEstimate GetActiveSortieReturnEstimate()
    {
        EnsureProgressInitialized();

        if (!progress.HasActiveSortie)
        {
            return SortieExtractionCalculator.Calculate(null, Vector3.zero, default);
        }

        ShipPhysics ship = GetActiveShip();
        Vector3 position = ship != null ? ship.transform.position : progress.activeSortie.lastKnownPosition;
        progress.RememberSortiePosition(position);
        return SortieExtractionCalculator.Calculate(progress.activeSortie, position, BuildCurrentSortieReturnProfile());
    }

    public void RememberActiveSortiePosition(Vector3 position)
    {
        EnsureProgressInitialized();
        progress.RememberSortiePosition(position);
    }

    private void UpdateActiveSortieExtractionRunupFromActiveShip()
    {
        if (!sessionExtractionCoreMode || CurrentMode != GameSessionMode.Flight || !HasActiveSortie)
        {
            activeSortieExtractionRunupStatus = "";
            return;
        }

        ShipPhysics ship = GetActiveShip();
        if (ship == null || !ship.gameObject.activeInHierarchy)
        {
            progress.activeSortie?.ResetExtractionRunup();
            activeSortieExtractionRunupStatus = "Slip blocked: active ship missing.";
            return;
        }

        Rigidbody body = ship.GetComponent<Rigidbody>();
        float deltaSeconds = Time.deltaTime > 0f ? Time.deltaTime : Time.fixedDeltaTime;
        RecordActiveSortieExtractionRunup(
            ship.transform.position,
            body != null ? body.linearVelocity : Vector3.zero,
            ship.transform.forward,
            deltaSeconds,
            ship.ClaudiumSlipstreamActive);
    }

    public bool RecordActiveSortieExtractionRunup(
        Vector3 position,
        Vector3 velocity,
        Vector3 forward,
        float deltaSeconds,
        bool claudiumSlipstreamActive)
    {
        EnsureProgressInitialized();
        if (!progress.HasActiveSortie)
        {
            activeSortieExtractionRunupStatus = "";
            return false;
        }

        SortieSessionState sortie = progress.activeSortie;
        sortie.Normalize();
        SortieZoneDefinition zone = sortie.zone;
        if (zone == null)
        {
            sortie.ResetExtractionRunup();
            activeSortieExtractionRunupStatus = "Slip blocked: sortie zone missing.";
            return false;
        }

        SortieReturnEstimate estimate = SortieExtractionCalculator.Calculate(
            sortie,
            position,
            BuildCurrentSortieReturnProfile());
        Vector3 outward = GetSortieOutwardDirection(zone, position);
        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 horizontalForward = new Vector3(forward.x, 0f, forward.z);
        float speed = horizontalVelocity.magnitude;
        float velocityDot = speed > 0.001f ? Vector3.Dot(horizontalVelocity / speed, outward) : -1f;
        float facingDot = horizontalForward.sqrMagnitude > 0.001f ? Vector3.Dot(horizontalForward.normalized, outward) : -1f;
        bool velocityToBase = speed > 0.001f
            && velocityDot >= ExtractionRunupOutwardDotThreshold;
        bool facingToBase = horizontalForward.sqrMagnitude > 0.001f
            && facingDot >= ExtractionRunupOutwardDotThreshold;
        bool movingToBase = velocityToBase || facingToBase;
        bool canBuildRunup = !estimate.isInsideCylinder
            && estimate.isAboveStorm
            && estimate.hasEnoughCoal
            && estimate.hasEnoughClaudium
            && claudiumSlipstreamActive
            && movingToBase;

        if (canBuildRunup)
        {
            sortie.AddExtractionRunup(deltaSeconds);
        }
        else
        {
            sortie.ResetExtractionRunup();
        }

        progress.RememberSortiePosition(position);
        activeSortieExtractionRunupStatus = BuildExtractionRunupStatus(
            sortie,
            zone,
            estimate,
            claudiumSlipstreamActive,
            movingToBase,
            velocityDot,
            facingDot);
        return sortie.extractionRunupSeconds + 0.001f >= Mathf.Max(0f, zone.extractionRunupRequiredSeconds);
    }

    private static string BuildExtractionRunupStatus(
        SortieSessionState sortie,
        SortieZoneDefinition zone,
        SortieReturnEstimate estimate,
        bool claudiumSlipstreamActive,
        bool movingToBase,
        float velocityDot,
        float facingDot)
    {
        if (sortie == null || zone == null)
        {
            return "";
        }

        if (!estimate.isAboveStorm)
        {
            return "Slip blocked: storm layer.";
        }

        if (estimate.isInsideCylinder)
        {
            return "Slip blocked: leave cylinder.";
        }

        if (!estimate.hasEnoughCoal || !estimate.hasEnoughClaudium)
        {
            return "Slip blocked: reserves coal "
                + estimate.currentCoalKg.ToString("0.#")
                + "/"
                + estimate.requiredCoalKg.ToString("0.#")
                + ", claudium "
                + estimate.currentClaudiumKg.ToString("0.#")
                + "/"
                + estimate.requiredClaudiumKg.ToString("0.#")
                + ".";
        }

        if (!claudiumSlipstreamActive)
        {
            return "Slip blocked: slipstream off.";
        }

        if (!movingToBase)
        {
            return "Slip blocked: aim at BASE SLIP. V "
                + velocityDot.ToString("0.00")
                + ", F "
                + facingDot.ToString("0.00")
                + ".";
        }

        float requiredSeconds = Mathf.Max(0f, zone.extractionRunupRequiredSeconds);
        return sortie.extractionRunupSeconds + 0.001f >= requiredSeconds
            ? "Slip ready: press Extract home."
            : "Slip charging: "
                + sortie.extractionRunupSeconds.ToString("0.0")
                + "/"
                + requiredSeconds.ToString("0.0")
                + "s.";
    }

    public bool TryExtractActiveSortie(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!progress.HasActiveSortie)
        {
            message = "No active sortie.";
            lastSaveMessage = message;
            return false;
        }

        SortieReturnProfile profile = BuildCurrentSortieReturnProfile();
        ShipPhysics ship = GetActiveShip();
        Vector3 position = ship != null ? ship.transform.position : progress.activeSortie.lastKnownPosition;
        SortieReturnEstimate estimate = SortieExtractionCalculator.Calculate(progress.activeSortie, position, profile);
        if (!estimate.canExtract)
        {
            if (estimate.isInsideCylinder
                || !estimate.isNearBoundary
                || !estimate.isAboveStorm
                || !estimate.hasEnoughCoal
                || !estimate.hasEnoughClaudium)
            {
                progress.activeSortie.ResetExtractionRunup();
            }

            message = estimate.status;
            lastSaveMessage = message;
            return false;
        }

        string fuelId = string.IsNullOrWhiteSpace(profile.coalResourceId) ? GetStartingEngineFuelId() : profile.coalResourceId;
        string claudiumId = string.IsNullOrWhiteSpace(profile.claudiumResourceId) ? "claudium" : profile.claudiumResourceId;
        if (progress.shipEngineFuelTank.GetAmount(fuelId) + 0.001f < estimate.requiredCoalKg
            || progress.shipClaudiumTank.GetAmount(claudiumId) + 0.001f < estimate.requiredClaudiumKg)
        {
            message = estimate.status;
            lastSaveMessage = message;
            return false;
        }

        progress.shipEngineFuelTank.TrySpend(fuelId, estimate.requiredCoalKg);
        progress.shipClaudiumTank.TrySpend(claudiumId, estimate.requiredClaudiumKg);
        int transferred = TransferShipCargoToCapital();
        progress.ClearShipCargo();
        progress.StopCargoTransfer();

        string baseDockId = GetCapitalIslandId();
        progress.SetDocked(baseDockId, DockingLocationKind.Island, GetDockPositionOrFallback(baseDockId, DockingLocationKind.Island));
        SyncShipConsumablesWithCargo(true);
        ApplySessionModeToShip();

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        message = "Extraction complete: transferred " + transferred
            + " cargo units to base. Return cost: "
            + estimate.requiredCoalKg.ToString("F0") + " kg coal, "
            + estimate.requiredClaudiumKg.ToString("F0") + " kg claudium.";
        lastSaveMessage = message;
        return true;
    }

    public bool LoseActiveSortieShipAndReturnToBase(string reason = "")
    {
        EnsureProgressInitialized();
        progress.ClearActiveSortie();
        return LoseShipAndReturnToCity(reason);
    }

    public string GetBaseRefuelStatusText()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        IslandProductionState storage = IsDockedAtCapital() ? GetCapitalStorageState() : null;
        ResolveCurrentTankResourceIds(out string fuelId, out string claudiumId);

        int fuelStored = storage != null ? storage.GetResourceAmount(fuelId) : 0;
        int claudiumStored = storage != null ? storage.GetResourceAmount(claudiumId) : 0;
        return fuelId + " " + progress.shipEngineFuelTank.GetAmount(fuelId).ToString("F0")
            + "/" + capacity.fuelTankCapacityKg.ToString("F0")
            + " base " + fuelStored
            + ", " + claudiumId + " " + progress.shipClaudiumTank.GetAmount(claudiumId).ToString("F0")
            + "/" + capacity.claudiumTankCapacityKg.ToString("F0")
            + " base " + claudiumStored;
    }

    public string GetCoreFittingCompactText()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null)
        {
            return "Fit: no catalog";
        }

        List<ShipSlotDefinition> slots = GetAssemblySlotsForUi(activeCatalog);
        return "Fit H " + CountFittingBandText(slots, activeCatalog, SessionExtractionConstants.HighSlotTypeId)
            + " M " + CountFittingBandText(slots, activeCatalog, SessionExtractionConstants.MidSlotTypeId)
            + " L " + CountFittingBandText(slots, activeCatalog, SessionExtractionConstants.LowSlotTypeId)
            + " R " + CountFittingBandText(slots, activeCatalog, SessionExtractionConstants.RigSlotTypeId);
    }

    public string GetCoreFittingSummaryText()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null)
        {
            return "Fitting: no catalog";
        }

        List<ShipSlotDefinition> slots = GetAssemblySlotsForUi(activeCatalog);
        return "High: " + BuildFittingBandSummary(slots, activeCatalog, SessionExtractionConstants.HighSlotTypeId)
            + " | Mid: " + BuildFittingBandSummary(slots, activeCatalog, SessionExtractionConstants.MidSlotTypeId)
            + " | Low: " + BuildFittingBandSummary(slots, activeCatalog, SessionExtractionConstants.LowSlotTypeId)
            + " | Rig: " + BuildFittingBandSummary(slots, activeCatalog, SessionExtractionConstants.RigSlotTypeId);
    }

    public string GetBaseProcessingOverviewText()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        if (!sessionExtractionCoreMode)
        {
            return "";
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        IslandProductionState storage = GetCapitalStorageState();
        List<string> parts = new List<string>();
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingBranch branch = SessionExtractionIndustry.ProcessingBranches[i];
            BaseProcessingLineState line = progress.baseIndustry.GetProcessing(branch);
            TryGetAvailableBaseProcessingInput(branch, storage, out _, out int availableInput);
            parts.Add(SessionExtractionIndustry.GetProcessingDisplayName(branch)
                + " L" + line.level
                + " " + line.capacityUnitsPerMinute.ToString("F0") + "/m"
                + " in " + availableInput
                + " done " + line.totalProcessedUnits.ToString("F0"));
        }

        return "Processing " + SessionExtractionIndustry.ProcessingBranches.Length + ": " + string.Join(" | ", parts);
    }

    public string GetBaseCascadeProductionOverviewText()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        if (!sessionExtractionCoreMode)
        {
            return "";
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        List<string> parts = new List<string>();
        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            CascadeProductionType type = SessionExtractionIndustry.CascadeProductionTypes[i];
            CascadeProductionLineState line = progress.baseIndustry.GetProduction(type);
            parts.Add(SessionExtractionIndustry.GetProductionDisplayName(type)
                + " L" + line.level
                + " " + line.capacityUnitsPerMinute.ToString("F0") + "/m"
                + " load " + line.totalLoadApplied.ToString("F0"));
        }

        return "Cascade " + SessionExtractionIndustry.CascadeProductionTypes.Length + ": " + string.Join(" | ", parts);
    }

    public string GetNextBaseCascadeOrderOverviewText()
    {
        CascadeProductionEstimate estimate = EstimateNextBaseCascadeOrder(out CascadeProductionOrderDefinition order);
        if (order == null)
        {
            return "Next cascade: none.";
        }

        if (estimate != null && estimate.canRun)
        {
            return "Next cascade: " + order.displayName
                + ", bottleneck " + SessionExtractionIndustry.GetProductionDisplayName(estimate.bottleneck)
                + " ~" + estimate.bottleneckMinutes.ToString("F1") + "m.";
        }

        string blocked = estimate != null && !string.IsNullOrWhiteSpace(estimate.blockedReason)
            ? estimate.blockedReason
            : "blocked.";
        return "Next cascade: " + order.displayName + " - " + blocked;
    }

    public string GetNextBaseIndustryUpgradeOverviewText()
    {
        if (!TryResolveNextBaseIndustryUpgrade(
                out bool processing,
                out BaseProcessingBranch branch,
                out CascadeProductionType type,
                out List<CascadeItemAmount> cost,
                out int level,
                out bool canAfford,
                out string blockedReason))
        {
            return "Base upgrade: no base lines.";
        }

        string lineName = processing
            ? SessionExtractionIndustry.GetProcessingDisplayName(branch)
            : SessionExtractionIndustry.GetProductionDisplayName(type);
        return "Base upgrade: " + lineName
            + " L" + level + " -> L" + (level + 1)
            + " cost " + BuildItemCostText(cost)
            + (canAfford ? "." : " - " + blockedReason);
    }

    public bool CanUpgradeNextBaseIndustryLine(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        if (!TryResolveNextBaseIndustryUpgrade(
                out _,
                out _,
                out _,
                out _,
                out _,
                out bool canAfford,
                out message))
        {
            message = "No base industry lines are available.";
            return false;
        }

        return canAfford;
    }

    public bool TryUpgradeNextBaseIndustryLine(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!TryResolveNextBaseIndustryUpgrade(
                out bool processing,
                out BaseProcessingBranch branch,
                out CascadeProductionType type,
                out _,
                out _,
                out bool canAfford,
                out message)
            || !canAfford)
        {
            lastSaveMessage = message;
            return false;
        }

        return processing
            ? TryUpgradeBaseProcessingBranch(branch, out message)
            : TryUpgradeCascadeProductionType(type, out message);
    }

    public bool CanUpgradeBaseProcessingBranch(BaseProcessingBranch branch, out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return CanUpgradeBaseProcessingBranchInternal(branch, out _, out _, out message);
    }

    public bool TryUpgradeBaseProcessingBranch(BaseProcessingBranch branch, out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!CanUpgradeBaseProcessingBranchInternal(branch, out BaseProcessingLineState line, out List<CascadeItemAmount> cost, out message))
        {
            lastSaveMessage = message;
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (!SpendBaseIndustryUpgradeCost(storage, cost, out message))
        {
            lastSaveMessage = message;
            return false;
        }

        int oldLevel = line.level;
        float oldCapacity = line.capacityUnitsPerMinute;
        line.level = oldLevel + 1;
        line.capacityUnitsPerMinute = GetUpgradedProcessingCapacity(branch, line.level, oldCapacity);
        AutoSaveIfDocked();

        message = "Upgraded processing " + SessionExtractionIndustry.GetProcessingDisplayName(branch)
            + " to L" + line.level
            + ": " + oldCapacity.ToString("F0") + " -> " + line.capacityUnitsPerMinute.ToString("F0") + "/m.";
        lastSaveMessage = message;
        return true;
    }

    public bool CanUpgradeCascadeProductionType(CascadeProductionType type, out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return CanUpgradeCascadeProductionTypeInternal(type, out _, out _, out message);
    }

    public bool TryUpgradeCascadeProductionType(CascadeProductionType type, out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!CanUpgradeCascadeProductionTypeInternal(type, out CascadeProductionLineState line, out List<CascadeItemAmount> cost, out message))
        {
            lastSaveMessage = message;
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (!SpendBaseIndustryUpgradeCost(storage, cost, out message))
        {
            lastSaveMessage = message;
            return false;
        }

        int oldLevel = line.level;
        float oldCapacity = line.capacityUnitsPerMinute;
        line.level = oldLevel + 1;
        line.capacityUnitsPerMinute = GetUpgradedCascadeCapacity(type, line.level, oldCapacity);
        AutoSaveIfDocked();

        message = "Upgraded cascade " + SessionExtractionIndustry.GetProductionDisplayName(type)
            + " to L" + line.level
            + ": " + oldCapacity.ToString("F0") + " -> " + line.capacityUnitsPerMinute.ToString("F0") + "/m.";
        lastSaveMessage = message;
        return true;
    }

    public bool CanRefuelBaseShip(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Refuel is available only at the base.";
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            return false;
        }

        if (EnsurePioneerFallbackHullSelectedForCore())
        {
            ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out _);
            ApplySelectedShip();
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (!capacity.assemblyValid)
        {
            message = "Refuel blocked: " + capacity.reason;
            return false;
        }

        ResolveCurrentTankResourceIds(out string fuelId, out string claudiumId);
        bool canFuel = storage.GetResourceAmount(fuelId) > 0
            && progress.shipEngineFuelTank.GetAmount(fuelId) < capacity.fuelTankCapacityKg - 0.001f;
        bool canClaudium = storage.GetResourceAmount(claudiumId) > 0
            && progress.shipClaudiumTank.GetAmount(claudiumId) < capacity.claudiumTankCapacityKg - 0.001f;
        bool canFreePioneerRefuel = CanFreeRefuelPioneerFallback(fuelId, claudiumId, capacity);

        if (!canFuel && !canClaudium && !canFreePioneerRefuel)
        {
            message = "Refuel blocked: tanks are full or base has no matching coal/claudium.";
            return false;
        }

        message = canFreePioneerRefuel && !canFuel && !canClaudium
            ? "Pioneer free refuel ready: " + GetBaseRefuelStatusText() + "."
            : "Refuel ready: " + GetBaseRefuelStatusText() + ".";
        return true;
    }

    public bool TryRefuelBaseShip(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!CanRefuelBaseShip(out message))
        {
            lastSaveMessage = message;
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        CargoCapacityInfo capacity = CalculateCargoCapacity();
        ResolveCurrentTankResourceIds(out string fuelId, out string claudiumId);

        int fuelMoved = RefillTankFromStorage(progress.shipEngineFuelTank, fuelId, capacity.fuelTankCapacityKg, storage);
        int claudiumMoved = RefillTankFromStorage(progress.shipClaudiumTank, claudiumId, capacity.claudiumTankCapacityKg, storage);
        FreeRefuelPioneerFallback(fuelId, claudiumId, capacity, out float freeFuelKg, out float freeClaudiumKg);
        if (fuelMoved <= 0 && claudiumMoved <= 0 && freeFuelKg <= 0f && freeClaudiumKg <= 0f)
        {
            message = "Refuel blocked: no resources moved.";
            lastSaveMessage = message;
            return false;
        }

        SyncShipConsumablesWithCargo(true);
        AutoSaveIfDocked();

        message = "Refueled: " + fuelMoved + " kg " + fuelId
            + ", " + claudiumMoved + " kg " + claudiumId
            + (freeFuelKg > 0f || freeClaudiumKg > 0f
                ? " Pioneer free reserve: " + freeFuelKg.ToString("F0") + " kg " + fuelId
                    + ", " + freeClaudiumKg.ToString("F0") + " kg " + claudiumId + "."
                : ".");
        lastSaveMessage = message;
        return true;
    }

    private bool CanFreeRefuelPioneerFallback(string fuelId, string claudiumId, CargoCapacityInfo capacity)
    {
        if (!sessionExtractionCoreMode || progress == null || !IsDockedAtCapital() || !IsPioneerFallbackHullSelected())
        {
            return false;
        }

        float fuelTargetKg = Mathf.Max(0f, startingFuelKg);
        float claudiumTargetKg = Mathf.Max(0f, startingClaudiumKg);
        bool needsFuel = fuelTargetKg > 0f
            && progress.shipEngineFuelTank.GetAmount(fuelId) < fuelTargetKg - 0.001f;
        bool needsClaudium = claudiumTargetKg > 0f
            && progress.shipClaudiumTank.GetAmount(claudiumId) < claudiumTargetKg - 0.001f;
        return capacity.assemblyValid && (needsFuel || needsClaudium);
    }

    private void FreeRefuelPioneerFallback(string fuelId, string claudiumId, CargoCapacityInfo capacity, out float fuelAddedKg, out float claudiumAddedKg)
    {
        fuelAddedKg = 0f;
        claudiumAddedKg = 0f;
        if (!CanFreeRefuelPioneerFallback(fuelId, claudiumId, capacity))
        {
            return;
        }

        EnsurePioneerFallbackHullSelectedForCore();

        float fuelTargetKg = Mathf.Max(0f, startingFuelKg);
        if (fuelTargetKg > 0f)
        {
            float currentFuelKg = progress.shipEngineFuelTank.GetAmount(fuelId);
            fuelAddedKg = progress.shipEngineFuelTank.Add(
                fuelId,
                Mathf.Max(0f, fuelTargetKg - currentFuelKg),
                Mathf.Max(fuelTargetKg, capacity.fuelTankCapacityKg));
        }

        float claudiumTargetKg = Mathf.Max(0f, startingClaudiumKg);
        if (claudiumTargetKg > 0f)
        {
            float currentClaudiumKg = progress.shipClaudiumTank.GetAmount(claudiumId);
            claudiumAddedKg = progress.shipClaudiumTank.Add(
                claudiumId,
                Mathf.Max(0f, claudiumTargetKg - currentClaudiumKg),
                Mathf.Max(claudiumTargetKg, capacity.claudiumTankCapacityKg));
        }
    }

    private bool IsPioneerFallbackHullSelected()
    {
        if (progress == null) return false;

        string selectedHullId = progress.selectedHullId ?? "";
        ShipPartDefinitionSO starterHull = ActiveCatalog != null ? ActiveCatalog.GetStarterHull() : null;
        string starterHullId = starterHull != null ? starterHull.partId : GameplaySessionSaveData.DefaultStarterHullId;
        if (string.IsNullOrWhiteSpace(selectedHullId)
            || selectedHullId == GameplaySessionSaveData.DefaultStarterHullId
            || selectedHullId == starterHullId)
        {
            return true;
        }

        if (ActiveCatalog == null || starterHull == null)
        {
            return false;
        }

        ShipPartDefinitionSO selectedHull = ActiveCatalog.GetPartById(selectedHullId);
        return selectedHull == null || !selectedHull.IsHull;
    }

    private bool EnsurePioneerFallbackHullSelectedForCore()
    {
        if (!sessionExtractionCoreMode || progress == null || ActiveCatalog == null)
        {
            return false;
        }

        if (!IsPioneerFallbackHullSelected())
        {
            return false;
        }

        ShipPartDefinitionSO starterHull = ActiveCatalog.GetStarterHull();
        if (starterHull == null)
        {
            return false;
        }

        string selectedHullId = progress.selectedHullId ?? "";
        ShipPartDefinitionSO selectedHull = string.IsNullOrWhiteSpace(selectedHullId)
            ? null
            : ActiveCatalog.GetPartById(selectedHullId);
        if (string.IsNullOrWhiteSpace(selectedHullId) || selectedHull == null || !selectedHull.IsHull)
        {
            progress.ReplaceShipAssembly(starterHull.partId);
            return true;
        }

        return false;
    }

    public bool TryProcessBaseBatch(BaseProcessingBranch branch, out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!IsDockedAtCapital())
        {
            message = SessionExtractionIndustry.GetProcessingDisplayName(branch) + " processing is available only at the base.";
            lastSaveMessage = message;
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            lastSaveMessage = message;
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();

        return branch switch
        {
            BaseProcessingBranch.Ore => TryProcessOreBaseBatch(storage, out message),
            BaseProcessingBranch.Gas => TryProcessGasBaseBatch(storage, out message),
            BaseProcessingBranch.AutomatonDismantling => TryProcessAutomatonBaseBatch(storage, out message),
            BaseProcessingBranch.LeviathanProcessing => TryProcessLeviathanBaseBatch(storage, out message),
            BaseProcessingBranch.CyberneticDeciphering => TryProcessCyberInfoBaseBatch(storage, out message),
            _ => FailBaseProcessing("Unknown processing branch: " + branch + ".", out message)
        };
    }

    public bool TryProcessBaseOreBatch(out string message)
    {
        return TryProcessBaseBatch(BaseProcessingBranch.Ore, out message);
    }

    public bool TryProcessNextBaseBatch(out string message)
    {
        if (!TryGetNextProcessableBaseBranch(out BaseProcessingBranch branch))
        {
            message = IsDockedAtCapital()
                ? "No processable sortie resources in base storage."
                : "Processing is available only at the base.";
            lastSaveMessage = message;
            return false;
        }

        return TryProcessBaseBatch(branch, out message);
    }

    public bool HasProcessableBaseBatch()
    {
        return TryGetNextProcessableBaseBranch(out _);
    }

    public bool TryGetNextProcessableBaseBranch(out BaseProcessingBranch branch)
    {
        branch = BaseProcessingBranch.Ore;
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        if (!IsDockedAtCapital())
        {
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            return false;
        }

        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingBranch candidate = SessionExtractionIndustry.ProcessingBranches[i];
            if (TryGetAvailableBaseProcessingInput(candidate, storage, out _, out int available) && available > 0)
            {
                branch = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryProcessOreBaseBatch(IslandProductionState storage, out string message)
    {
        OreTypeConfig oreType = FindFirstStoredOreType(storage, out int availableOre);
        if (oreType == null || availableOre <= 0)
        {
            return FailBaseProcessing("No ore in base storage.", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(BaseProcessingBranch.Ore);
        int batchKg = GetProcessingBatchSize(line, availableOre);
        if (!storage.TrySpendResource(oreType.oreItemId, batchKg))
        {
            return FailBaseProcessing("Could not spend ore from base storage.", out message);
        }

        int outputTotal = AddOreProcessingOutputs(storage, oreType, batchKg);
        line.totalProcessedUnits += batchKg;
        AutoSaveIfDocked();

        message = "Processed " + batchKg + " kg " + oreType.oreItemId + " into " + outputTotal + " kg minerals.";
        lastSaveMessage = message;
        return true;
    }

    private bool TryProcessGasBaseBatch(IslandProductionState storage, out string message)
    {
        GasCloudTypeConfig gasType = FindFirstStoredGasCloudType(storage, out int availableCondensate);
        if (gasType == null || availableCondensate <= 0)
        {
            return FailBaseProcessing("No gas condensate in base storage.", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(BaseProcessingBranch.Gas);
        int batch = GetProcessingBatchSize(line, availableCondensate);
        if (!storage.TrySpendResource(gasType.condensateItemId, batch))
        {
            return FailBaseProcessing("Could not spend gas condensate from base storage.", out message);
        }

        int outputTotal = AddGasProcessingOutputs(storage, gasType, batch);
        line.totalProcessedUnits += batch;
        AutoSaveIfDocked();

        message = "Processed " + batch + " units " + gasType.condensateItemId + " into " + outputTotal + " gas materials.";
        lastSaveMessage = message;
        return true;
    }

    private bool TryProcessAutomatonBaseBatch(IslandProductionState storage, out string message)
    {
        string inputItemId = FindFirstStoredAutomatonInput(storage, out int availableSalvage);
        if (string.IsNullOrWhiteSpace(inputItemId) || availableSalvage <= 0)
        {
            return FailBaseProcessing("No automaton salvage in base storage.", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(BaseProcessingBranch.AutomatonDismantling);
        int batch = GetProcessingBatchSize(line, availableSalvage);
        if (!storage.TrySpendResource(inputItemId, batch))
        {
            return FailBaseProcessing("Could not spend automaton salvage from base storage.", out message);
        }

        int outputTotal = 0;
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.MechanismsItemId, batch, 0.40f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.ToolsItemId, batch, 0.20f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.AutomatonCoreItemId, batch, 0.20f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.10f);

        line.totalProcessedUnits += batch;
        AutoSaveIfDocked();

        message = "Dismantled " + batch + " units " + inputItemId + " into " + outputTotal + " automaton outputs.";
        lastSaveMessage = message;
        return true;
    }

    private bool TryProcessLeviathanBaseBatch(IslandProductionState storage, out string message)
    {
        LeviathanTypeConfig leviathanType = FindFirstStoredLeviathanType(storage, out int availableCarcass);
        if (leviathanType == null || availableCarcass <= 0)
        {
            return FailBaseProcessing("No leviathan carcass in base storage.", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(BaseProcessingBranch.LeviathanProcessing);
        int batch = GetProcessingBatchSize(line, availableCarcass);
        if (!storage.TrySpendResource(leviathanType.carcassItemId, batch))
        {
            return FailBaseProcessing("Could not spend leviathan carcass from base storage.", out message);
        }

        int outputTotal = 0;
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.LeviathanFatItemId, batch, 0.30f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.LeviathanHideItemId, batch, 0.25f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.MineralShellItemId, batch, 0.25f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.ClaudiumGlandItemId, batch, 0.08f);

        line.totalProcessedUnits += batch;
        AutoSaveIfDocked();

        message = "Processed " + batch + " kg " + leviathanType.carcassItemId + " into " + outputTotal + " leviathan materials.";
        lastSaveMessage = message;
        return true;
    }

    private bool TryProcessCyberInfoBaseBatch(IslandProductionState storage, out string message)
    {
        string infoItemId = FindFirstStoredCyberInfoInput(storage, out int availableInfo);
        if (string.IsNullOrWhiteSpace(infoItemId) || availableInfo <= 0)
        {
            return FailBaseProcessing("No cybernetic survey information in base storage.", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(BaseProcessingBranch.CyberneticDeciphering);
        int batch = GetProcessingBatchSize(line, availableInfo);
        if (!storage.TrySpendResource(infoItemId, batch))
        {
            return FailBaseProcessing("Could not spend survey information from base storage.", out message);
        }

        int outputTotal = 0;
        if (infoItemId == SessionExtractionConstants.LeviathanInfoItemId)
        {
            outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.85f);
            outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.FundamentalExperienceItemId, batch, 0.15f);
        }
        else if (infoItemId == SessionExtractionConstants.CloudInfoItemId)
        {
            outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.FundamentalExperienceItemId, batch, 0.70f);
            outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.30f);
        }
        else
        {
            outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.FundamentalExperienceItemId, batch, 0.85f);
            outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.15f);
        }

        line.totalProcessedUnits += batch;
        AutoSaveIfDocked();

        message = "Deciphered " + batch + " units " + infoItemId + " into " + outputTotal + " research outputs.";
        lastSaveMessage = message;
        return true;
    }

    private bool FailBaseProcessing(string message, out string outputMessage)
    {
        outputMessage = message;
        lastSaveMessage = outputMessage;
        return false;
    }

    public bool TryRunStarterAirframeCascade(out string message)
    {
        return TryRunBaseCascadeOrder(CreateStarterAirframeCascadeOrder(), out message);
    }

    public CascadeProductionOrderDefinition CreateStarterAirframeCascadeOrder()
    {
        return SessionExtractionIndustry.CreateStarterAirframeOrder();
    }

    public CascadeProductionEstimate EstimateStarterAirframeCascade()
    {
        return EstimateBaseCascadeOrder(CreateStarterAirframeCascadeOrder());
    }

    public List<CascadeProductionOrderDefinition> CreateStarterCascadeOrders()
    {
        return SessionExtractionIndustry.CreateStarterCascadeOrders();
    }

    public CascadeProductionEstimate EstimateNextBaseCascadeOrder(out CascadeProductionOrderDefinition selectedOrder)
    {
        selectedOrder = null;
        List<CascadeProductionOrderDefinition> orders = CreateStarterCascadeOrders();
        CascadeProductionEstimate firstEstimate = null;
        CascadeProductionOrderDefinition firstOrder = null;
        CascadeProductionEstimate firstRunnableEstimate = null;
        CascadeProductionOrderDefinition firstRunnableOrder = null;
        IslandProductionState storage = IsDockedAtCapital() ? GetCapitalStorageState() : null;

        for (int i = 0; i < orders.Count; i++)
        {
            CascadeProductionOrderDefinition order = orders[i];
            if (order == null) continue;

            CascadeProductionEstimate estimate = EstimateBaseCascadeOrder(order);
            if (firstEstimate == null)
            {
                firstEstimate = estimate;
                firstOrder = order;
            }

            if (estimate != null && estimate.canRun)
            {
                firstRunnableEstimate ??= estimate;
                firstRunnableOrder ??= order;

                if (StarterCascadeOrderNeedsOutput(order, storage))
                {
                    selectedOrder = order;
                    return estimate;
                }
            }
        }

        if (firstRunnableEstimate != null)
        {
            selectedOrder = firstRunnableOrder;
            return firstRunnableEstimate;
        }

        selectedOrder = firstOrder;
        if (firstEstimate != null)
        {
            return firstEstimate;
        }

        return new CascadeProductionEstimate
        {
            canRun = false,
            bottleneck = CascadeProductionType.Assembly,
            blockedReason = "No starter cascade orders are configured."
        };
    }

    private static bool StarterCascadeOrderNeedsOutput(CascadeProductionOrderDefinition order, IslandProductionState storage)
    {
        if (order == null || storage == null)
        {
            return false;
        }

        order.Normalize();
        int targetStock = GetStarterCascadeTargetStock(order.orderId);
        if (targetStock <= 0)
        {
            return true;
        }

        for (int i = 0; i < order.outputs.Count; i++)
        {
            CascadeItemAmount output = order.outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0) continue;
            if (storage.GetResourceAmount(output.itemId) < targetStock)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetStarterCascadeTargetStock(string orderId)
    {
        return orderId switch
        {
            SessionExtractionConstants.StarterAirframeOrderId => 1,
            SessionExtractionConstants.StarterModuleKitOrderId => 1,
            SessionExtractionConstants.StarterMunitionBundleOrderId => 4,
            _ => 0
        };
    }

    public bool TryRunNextBaseCascadeOrder(out string message)
    {
        CascadeProductionEstimate estimate = EstimateNextBaseCascadeOrder(out CascadeProductionOrderDefinition order);
        if (order == null || estimate == null || !estimate.canRun)
        {
            message = estimate != null && !string.IsNullOrWhiteSpace(estimate.blockedReason)
                ? estimate.blockedReason
                : "No runnable cascade order.";
            lastSaveMessage = message;
            return false;
        }

        return TryRunBaseCascadeOrder(order, out message);
    }

    public CascadeProductionEstimate EstimateBaseCascadeOrder(CascadeProductionOrderDefinition order)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        CascadeProductionEstimate estimate = new CascadeProductionEstimate
        {
            canRun = false,
            bottleneck = CascadeProductionType.Assembly,
            blockedReason = "Cascade order is missing."
        };

        if (order == null)
        {
            return estimate;
        }

        order.Normalize();
        if (sessionExtractionCoreMode && !IsKnownSessionCoreCascadeOrder(order))
        {
            estimate.blockedReason = "Cascade order is not part of the session extraction base catalog.";
            return estimate;
        }

        if (!IsDockedAtCapital())
        {
            estimate.blockedReason = "Cascade production is available only at the base.";
            return estimate;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            estimate.blockedReason = "Base storage is missing.";
            return estimate;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();

        if (order.outputs.Count == 0)
        {
            estimate.blockedReason = "Cascade order has no output.";
            return estimate;
        }

        if (order.loads.Count == 0)
        {
            estimate.blockedReason = "Cascade order has no production load.";
            return estimate;
        }

        for (int i = 0; i < order.inputs.Count; i++)
        {
            CascadeItemAmount input = order.inputs[i];
            if (input == null || string.IsNullOrWhiteSpace(input.itemId) || input.amount <= 0) continue;

            int available = storage.GetResourceAmount(input.itemId);
            if (available >= input.amount) continue;

            estimate.missingInputs.Add(new CascadeResourceGap
            {
                itemId = input.itemId,
                required = input.amount,
                available = available,
                missing = Mathf.Max(0, input.amount - available)
            });
        }

        for (int i = 0; i < order.loads.Count; i++)
        {
            CascadeProductionLoad load = order.loads[i];
            if (load == null || load.loadUnits <= 0f) continue;

            CascadeProductionLineState line = progress.baseIndustry.GetProduction(load.type);
            float minutes = load.loadUnits / Mathf.Max(0.1f, line.capacityUnitsPerMinute);
            estimate.totalLoadUnits += load.loadUnits;
            if (minutes > estimate.bottleneckMinutes)
            {
                estimate.bottleneck = load.type;
                estimate.bottleneckMinutes = minutes;
            }
        }

        if (estimate.missingInputs.Count > 0)
        {
            CascadeResourceGap gap = estimate.missingInputs[0];
            estimate.blockedReason = "Cascade blocked: need " + gap.required + " " + gap.itemId
                + ", have " + gap.available + ".";
            return estimate;
        }

        estimate.canRun = true;
        estimate.blockedReason = "";
        return estimate;
    }

    public bool TryRunBaseCascadeOrder(CascadeProductionOrderDefinition order, out string message)
    {
        message = "";
        CascadeProductionEstimate estimate = EstimateBaseCascadeOrder(order);
        if (order == null || !estimate.canRun)
        {
            message = string.IsNullOrWhiteSpace(estimate.blockedReason)
                ? "Cascade order is blocked."
                : estimate.blockedReason;
            lastSaveMessage = message;
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            lastSaveMessage = message;
            return false;
        }

        order.Normalize();
        for (int i = 0; i < order.inputs.Count; i++)
        {
            CascadeItemAmount input = order.inputs[i];
            if (input == null || string.IsNullOrWhiteSpace(input.itemId) || input.amount <= 0) continue;

            if (!storage.TrySpendResource(input.itemId, input.amount))
            {
                message = "Cascade blocked: could not spend " + input.amount + " " + input.itemId + ".";
                lastSaveMessage = message;
                return false;
            }
        }

        for (int i = 0; i < order.outputs.Count; i++)
        {
            CascadeItemAmount output = order.outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0) continue;
            storage.AddResource(output.itemId, output.amount);
        }

        ApplyCascadeOrderLoad(order);
        AutoSaveIfDocked();

        message = "Cascade complete: " + BuildCascadeOutputsText(order)
            + ". Bottleneck: " + SessionExtractionIndustry.GetProductionDisplayName(estimate.bottleneck)
            + " ~" + estimate.bottleneckMinutes.ToString("F1") + " min.";
        lastSaveMessage = message;
        return true;
    }

    public bool CanLoadStarterMunitionsAtBase(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!sessionExtractionCoreMode)
        {
            message = "Starter munitions are part of session extraction core.";
            return false;
        }

        if (!IsDockedAtCapital())
        {
            message = "Starter munitions can be loaded only at the base.";
            return false;
        }

        if (progress.HasActiveSortie)
        {
            message = "Finish the active sortie before loading munitions.";
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            return false;
        }

        if (storage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId) <= 0)
        {
            message = "Loadout blocked: build a munition bundle first.";
            return false;
        }

        int currentWeapon = progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId);
        int targetWeapon = Mathf.Max(1, SessionExtractionConstants.StarterWeaponLoadoutTargetUnits);
        if (currentWeapon >= targetWeapon)
        {
            message = "Weapon loadout is already stocked: " + currentWeapon + "/" + targetWeapon + ".";
            return false;
        }

        int loadUnits = GetStarterMunitionLoadUnits(currentWeapon);
        if (loadUnits <= 0)
        {
            message = "No weapon units can be loaded.";
            return false;
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (!capacity.assemblyValid || !capacity.canFly)
        {
            message = "Loadout blocked: " + capacity.reason;
            return false;
        }

        float addedMassKg = worldConfig != null
            ? worldConfig.GetItemTransportMassKg(SessionExtractionConstants.StarterWeaponCargoItemId, loadUnits)
            : loadUnits;
        float freeKg = Mathf.Max(0f, capacity.maxCargoKg - capacity.currentCargoKg - capacity.currentTankKg);
        if (freeKg + 0.001f < addedMassKg)
        {
            message = "Loadout blocked: need " + addedMassKg.ToString("F1")
                + " kg cargo room, free " + freeKg.ToString("F1") + " kg.";
            return false;
        }

        Dictionary<string, int> cargoAfterLoad = CargoStoragePlanner.ToCargoMap(progress.shipCargo);
        cargoAfterLoad[SessionExtractionConstants.StarterWeaponCargoItemId] =
            cargoAfterLoad.TryGetValue(SessionExtractionConstants.StarterWeaponCargoItemId, out int existingWeapon)
                ? existingWeapon + loadUnits
                : loadUnits;
        if (!CargoStoragePlanner.TryValidateCargoStorage(worldConfig, capacity.cargoCompartments, cargoAfterLoad, out string storageError))
        {
            message = storageError;
            return false;
        }

        message = "Starter munitions ready: load "
            + loadUnits + " kg "
            + SessionExtractionConstants.StarterWeaponCargoItemId
            + " from one "
            + SessionExtractionConstants.StarterMunitionBundleItemId
            + ".";
        return true;
    }

    public bool TryLoadStarterMunitionsAtBase(out string message)
    {
        if (!CanLoadStarterMunitionsAtBase(out message))
        {
            lastSaveMessage = message;
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        int loadUnits = GetStarterMunitionLoadUnits(progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId));
        if (storage == null || loadUnits <= 0 || !storage.TrySpendResource(SessionExtractionConstants.StarterMunitionBundleItemId, 1))
        {
            message = "Loadout blocked: could not spend "
                + SessionExtractionConstants.StarterMunitionBundleItemId + ".";
            lastSaveMessage = message;
            return false;
        }

        progress.AddShipCargo(SessionExtractionConstants.StarterWeaponCargoItemId, loadUnits);
        ApplyCargoMassToShip(GetActiveShip());
        ResetCargoPlan();
        AutoSaveIfDocked();

        message = "Loaded starter munitions: +" + loadUnits
            + " kg " + SessionExtractionConstants.StarterWeaponCargoItemId + ".";
        lastSaveMessage = message;
        return true;
    }

    private static int GetStarterMunitionLoadUnits(int currentWeaponUnits)
    {
        int targetWeapon = Mathf.Max(1, SessionExtractionConstants.StarterWeaponLoadoutTargetUnits);
        int perBundle = Mathf.Max(1, SessionExtractionConstants.StarterWeaponUnitsPerMunitionBundle);
        return Mathf.Min(perBundle, Mathf.Max(0, targetWeapon - Mathf.Max(0, currentWeaponUnits)));
    }

    private bool IsKnownSessionCoreCascadeOrder(CascadeProductionOrderDefinition order)
    {
        if (order == null) return false;

        List<CascadeProductionOrderDefinition> catalogOrders = CreateStarterCascadeOrders();
        for (int i = 0; i < catalogOrders.Count; i++)
        {
            CascadeProductionOrderDefinition catalogOrder = catalogOrders[i];
            if (CascadeOrdersMatch(order, catalogOrder))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CascadeOrdersMatch(CascadeProductionOrderDefinition left, CascadeProductionOrderDefinition right)
    {
        if (left == null || right == null) return false;

        left.Normalize();
        right.Normalize();
        return left.orderId == right.orderId
            && CascadeItemsMatch(left.inputs, right.inputs)
            && CascadeItemsMatch(left.outputs, right.outputs)
            && CascadeLoadsMatch(left.loads, right.loads);
    }

    private static bool CascadeItemsMatch(List<CascadeItemAmount> left, List<CascadeItemAmount> right)
    {
        if (left == null || right == null) return left == right;
        if (left.Count != right.Count) return false;

        for (int i = 0; i < left.Count; i++)
        {
            CascadeItemAmount leftItem = left[i];
            CascadeItemAmount rightItem = right[i];
            if (leftItem == null || rightItem == null) return false;
            if (leftItem.itemId != rightItem.itemId || leftItem.amount != rightItem.amount)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CascadeLoadsMatch(List<CascadeProductionLoad> left, List<CascadeProductionLoad> right)
    {
        if (left == null || right == null) return left == right;
        if (left.Count != right.Count) return false;

        for (int i = 0; i < left.Count; i++)
        {
            CascadeProductionLoad leftLoad = left[i];
            CascadeProductionLoad rightLoad = right[i];
            if (leftLoad == null || rightLoad == null) return false;
            if (leftLoad.type != rightLoad.type || !Mathf.Approximately(leftLoad.loadUnits, rightLoad.loadUnits))
            {
                return false;
            }
        }

        return true;
    }

    private bool CanUpgradeBaseProcessingBranchInternal(
        BaseProcessingBranch branch,
        out BaseProcessingLineState line,
        out List<CascadeItemAmount> cost,
        out string message)
    {
        line = null;
        cost = null;
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Base processing upgrades are available only at the base.";
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        line = progress.baseIndustry.GetProcessing(branch);
        cost = CreateProcessingUpgradeCost(line.level);
        if (!CanSpendBaseIndustryUpgradeCost(storage, cost, out string blockedReason))
        {
            message = "Processing upgrade blocked: " + blockedReason + ".";
            return false;
        }

        message = "Processing upgrade ready: " + SessionExtractionIndustry.GetProcessingDisplayName(branch)
            + " L" + line.level + " -> L" + (line.level + 1)
            + ", cost " + BuildItemCostText(cost) + ".";
        return true;
    }

    private bool CanUpgradeCascadeProductionTypeInternal(
        CascadeProductionType type,
        out CascadeProductionLineState line,
        out List<CascadeItemAmount> cost,
        out string message)
    {
        line = null;
        cost = null;
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Cascade upgrades are available only at the base.";
            return false;
        }

        IslandProductionState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        line = progress.baseIndustry.GetProduction(type);
        cost = CreateCascadeUpgradeCost(line.level);
        if (!CanSpendBaseIndustryUpgradeCost(storage, cost, out string blockedReason))
        {
            message = "Cascade upgrade blocked: " + blockedReason + ".";
            return false;
        }

        message = "Cascade upgrade ready: " + SessionExtractionIndustry.GetProductionDisplayName(type)
            + " L" + line.level + " -> L" + (line.level + 1)
            + ", cost " + BuildItemCostText(cost) + ".";
        return true;
    }

    private bool TryResolveNextBaseIndustryUpgrade(
        out bool processing,
        out BaseProcessingBranch branch,
        out CascadeProductionType type,
        out List<CascadeItemAmount> cost,
        out int level,
        out bool canAfford,
        out string blockedReason)
    {
        processing = true;
        branch = BaseProcessingBranch.Ore;
        type = CascadeProductionType.Assembly;
        cost = null;
        level = 0;
        canAfford = false;
        blockedReason = "";

        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        if (progress == null)
        {
            blockedReason = "Progress is missing.";
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        IslandProductionState storage = IsDockedAtCapital() ? GetCapitalStorageState() : null;

        bool foundAny = false;
        bool foundAffordable = false;
        bool bestProcessing = true;
        BaseProcessingBranch bestBranch = BaseProcessingBranch.Ore;
        CascadeProductionType bestType = CascadeProductionType.Assembly;
        List<CascadeItemAmount> bestCost = null;
        int bestLevel = int.MaxValue;
        string bestBlockedReason = "";

        bool foundFallback = false;
        bool fallbackProcessing = true;
        BaseProcessingBranch fallbackBranch = BaseProcessingBranch.Ore;
        CascadeProductionType fallbackType = CascadeProductionType.Assembly;
        List<CascadeItemAmount> fallbackCost = null;
        int fallbackLevel = int.MaxValue;
        string fallbackBlockedReason = "";

        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingBranch candidateBranch = SessionExtractionIndustry.ProcessingBranches[i];
            BaseProcessingLineState candidateLine = progress.baseIndustry.GetProcessing(candidateBranch);
            List<CascadeItemAmount> candidateCost = CreateProcessingUpgradeCost(candidateLine.level);
            string candidateBlocked = "";
            bool candidateAffordable = storage != null && CanSpendBaseIndustryUpgradeCost(storage, candidateCost, out candidateBlocked);
            if (storage == null)
            {
                candidateBlocked = IsDockedAtCapital() ? "base storage is missing" : "upgrades are available only at the base";
            }

            foundAny = true;
            if (!foundFallback || candidateLine.level < fallbackLevel)
            {
                foundFallback = true;
                fallbackProcessing = true;
                fallbackBranch = candidateBranch;
                fallbackCost = candidateCost;
                fallbackLevel = candidateLine.level;
                fallbackBlockedReason = candidateBlocked;
            }

            if (candidateAffordable && (!foundAffordable || candidateLine.level < bestLevel))
            {
                foundAffordable = true;
                bestProcessing = true;
                bestBranch = candidateBranch;
                bestCost = candidateCost;
                bestLevel = candidateLine.level;
                bestBlockedReason = "";
            }
        }

        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            CascadeProductionType candidateType = SessionExtractionIndustry.CascadeProductionTypes[i];
            CascadeProductionLineState candidateLine = progress.baseIndustry.GetProduction(candidateType);
            List<CascadeItemAmount> candidateCost = CreateCascadeUpgradeCost(candidateLine.level);
            string candidateBlocked = "";
            bool candidateAffordable = storage != null && CanSpendBaseIndustryUpgradeCost(storage, candidateCost, out candidateBlocked);
            if (storage == null)
            {
                candidateBlocked = IsDockedAtCapital() ? "base storage is missing" : "upgrades are available only at the base";
            }

            foundAny = true;
            if (!foundFallback || candidateLine.level < fallbackLevel)
            {
                foundFallback = true;
                fallbackProcessing = false;
                fallbackType = candidateType;
                fallbackCost = candidateCost;
                fallbackLevel = candidateLine.level;
                fallbackBlockedReason = candidateBlocked;
            }

            if (candidateAffordable && (!foundAffordable || candidateLine.level < bestLevel))
            {
                foundAffordable = true;
                bestProcessing = false;
                bestType = candidateType;
                bestCost = candidateCost;
                bestLevel = candidateLine.level;
                bestBlockedReason = "";
            }
        }

        if (!foundAny)
        {
            blockedReason = "No base industry lines are available.";
            return false;
        }

        processing = foundAffordable ? bestProcessing : fallbackProcessing;
        branch = foundAffordable ? bestBranch : fallbackBranch;
        type = foundAffordable ? bestType : fallbackType;
        cost = foundAffordable ? bestCost : fallbackCost;
        level = foundAffordable ? bestLevel : fallbackLevel;
        canAfford = foundAffordable;
        blockedReason = foundAffordable ? bestBlockedReason : fallbackBlockedReason;
        return true;
    }

    private static List<CascadeItemAmount> CreateProcessingUpgradeCost(int currentLevel)
    {
        int level = Mathf.Max(1, currentLevel);
        return new List<CascadeItemAmount>
        {
            new CascadeItemAmount { itemId = "ferron", amount = 4 * level },
            new CascadeItemAmount { itemId = "silvate", amount = 1 * level },
            new CascadeItemAmount { itemId = "charcoal", amount = 1 * level }
        };
    }

    private static List<CascadeItemAmount> CreateCascadeUpgradeCost(int currentLevel)
    {
        int level = Mathf.Max(1, currentLevel);
        return new List<CascadeItemAmount>
        {
            new CascadeItemAmount { itemId = "ferron", amount = 6 * level },
            new CascadeItemAmount { itemId = "silvate", amount = 2 * level },
            new CascadeItemAmount { itemId = "charcoal", amount = 2 * level }
        };
    }

    private static bool CanSpendBaseIndustryUpgradeCost(IslandProductionState storage, List<CascadeItemAmount> cost, out string blockedReason)
    {
        blockedReason = "";
        if (storage == null)
        {
            blockedReason = "base storage is missing";
            return false;
        }

        if (cost == null || cost.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < cost.Count; i++)
        {
            CascadeItemAmount item = cost[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;

            int available = storage.GetResourceAmount(item.itemId);
            if (available >= item.amount) continue;

            blockedReason = "need " + item.amount + " " + item.itemId + ", have " + available;
            return false;
        }

        return true;
    }

    private static bool SpendBaseIndustryUpgradeCost(IslandProductionState storage, List<CascadeItemAmount> cost, out string message)
    {
        message = "";
        if (!CanSpendBaseIndustryUpgradeCost(storage, cost, out string blockedReason))
        {
            message = "Upgrade blocked: " + blockedReason + ".";
            return false;
        }

        if (cost == null) return true;
        for (int i = 0; i < cost.Count; i++)
        {
            CascadeItemAmount item = cost[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;

            if (!storage.TrySpendResource(item.itemId, item.amount))
            {
                message = "Upgrade blocked: could not spend " + item.amount + " " + item.itemId + ".";
                return false;
            }
        }

        return true;
    }

    private static float GetUpgradedProcessingCapacity(BaseProcessingBranch branch, int level, float currentCapacity)
    {
        float baseCapacity = SessionExtractionIndustry.GetDefaultProcessingCapacity(branch);
        float targetCapacity = baseCapacity * (1f + 0.25f * Mathf.Max(0, level - 1));
        return Mathf.Max(currentCapacity, targetCapacity);
    }

    private static float GetUpgradedCascadeCapacity(CascadeProductionType type, int level, float currentCapacity)
    {
        float baseCapacity = SessionExtractionIndustry.GetDefaultProductionCapacity(type);
        float targetCapacity = baseCapacity * (1f + 0.25f * Mathf.Max(0, level - 1));
        return Mathf.Max(currentCapacity, targetCapacity);
    }

    private static string BuildItemCostText(List<CascadeItemAmount> cost)
    {
        if (cost == null || cost.Count == 0)
        {
            return "nothing";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < cost.Count; i++)
        {
            CascadeItemAmount item = cost[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;
            parts.Add(item.itemId + " x" + item.amount);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    public bool CanInstallStarterCargoRackUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterAirframeKitItemId,
            SessionExtractionConstants.StarterCargoRackModuleId,
            SessionExtractionConstants.StarterLowSlotId,
            SessionExtractionConstants.LowSlotTypeId,
            "Starter cargo rack is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterCargoRackUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterAirframeKitItemId,
            SessionExtractionConstants.StarterCargoRackModuleId,
            SessionExtractionConstants.StarterLowSlotId,
            SessionExtractionConstants.LowSlotTypeId,
            "Starter cargo rack is already installed.",
            "Installed first Low upgrade",
            out message);
    }

    public bool CanInstallStarterGasHarvesterUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterGasHarvesterModuleId,
            SessionExtractionConstants.StarterHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter gas harvester is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterGasHarvesterUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterGasHarvesterModuleId,
            SessionExtractionConstants.StarterHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter gas harvester is already installed.",
            "Installed High gas harvester",
            out message);
    }

    public bool CanInstallStarterMiningHoldUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterMiningHoldModuleId,
            SessionExtractionConstants.StarterSecondHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter mining hold is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterMiningHoldUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterMiningHoldModuleId,
            SessionExtractionConstants.StarterSecondHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter mining hold is already installed.",
            "Installed High impact/salvage module",
            out message);
    }

    public bool CanInstallStarterHarpoonUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterHarpoonModuleId,
            SessionExtractionConstants.StarterThirdHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter harpoon is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterHarpoonUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterHarpoonModuleId,
            SessionExtractionConstants.StarterThirdHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter harpoon is already installed.",
            "Installed High harpoon module",
            out message);
    }

    public bool CanInstallStarterObservationUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterObservationPostModuleId,
            SessionExtractionConstants.StarterMidSlotId,
            SessionExtractionConstants.MidSlotTypeId,
            "Starter observation post is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterObservationUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterObservationPostModuleId,
            SessionExtractionConstants.StarterMidSlotId,
            SessionExtractionConstants.MidSlotTypeId,
            "Starter observation post is already installed.",
            "Installed Mid observation module",
            out message);
    }

    public bool CanInstallNextStarterFittingUpgrade(out string message)
    {
        if (CanInstallStarterCargoRackUpgrade(out message)) return true;
        if (CanInstallStarterGasHarvesterUpgrade(out message)) return true;
        if (CanInstallStarterMiningHoldUpgrade(out message)) return true;
        if (CanInstallStarterHarpoonUpgrade(out message)) return true;
        if (CanInstallStarterObservationUpgrade(out message)) return true;
        return false;
    }

    public bool TryInstallNextStarterFittingUpgrade(out string message)
    {
        if (CanInstallStarterCargoRackUpgrade(out _)) return TryInstallStarterCargoRackUpgrade(out message);
        if (CanInstallStarterGasHarvesterUpgrade(out _)) return TryInstallStarterGasHarvesterUpgrade(out message);
        if (CanInstallStarterMiningHoldUpgrade(out _)) return TryInstallStarterMiningHoldUpgrade(out message);
        if (CanInstallStarterHarpoonUpgrade(out _)) return TryInstallStarterHarpoonUpgrade(out message);
        if (CanInstallStarterObservationUpgrade(out _)) return TryInstallStarterObservationUpgrade(out message);

        message = "No starter fitting upgrade is currently installable.";
        lastSaveMessage = message;
        return false;
    }

    public string GetNextStarterFittingUpgradeActionLabel()
    {
        if (CanInstallStarterCargoRackUpgrade(out _)) return "Install Low rack";
        if (CanInstallStarterGasHarvesterUpgrade(out _)) return "Install High gas";
        if (CanInstallStarterMiningHoldUpgrade(out _)) return "Install High salvage";
        if (CanInstallStarterHarpoonUpgrade(out _)) return "Install High harpoon";
        if (CanInstallStarterObservationUpgrade(out _)) return "Install Mid scout";
        return "Install module";
    }

    public bool BeginFlagshipExpedition(FlagshipExpeditionDefinition expedition)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        if (!CanBeginFlagshipExpedition(expedition, out string reason))
        {
            lastSaveMessage = reason;
            return false;
        }

        bool started = TryBeginFlightSession(null, expedition, false);
        if (!started)
        {
            progress.activeExpedition.Clear();
            return false;
        }

        if (!TryEnterFlagshipExpeditionScene(expedition, out string transitionMessage) &&
            !string.IsNullOrWhiteSpace(transitionMessage))
        {
            lastSaveMessage += " " + transitionMessage;
        }

        return true;
    }

    public bool BeginFlagshipExpedition(string expeditionId)
    {
        EnsureWorldConfigLoaded();
        return BeginFlagshipExpedition(worldConfig != null ? worldConfig.GetFlagshipExpedition(expeditionId) : null);
    }

    public bool ReturnFromFlagshipExpedition()
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            progress.activeExpedition?.Clear();
            lastSaveMessage = "Legacy flagship expedition return is disabled in session extraction core.";
            return false;
        }

        FlagshipExpeditionState expedition = progress.activeExpedition;
        if (expedition == null || !expedition.active)
        {
            lastSaveMessage = "Нет активной экспедиции.";
            return false;
        }

        expedition.Normalize();
        string expeditionName = GetExpeditionDisplayName(expedition);
        string dockId = string.IsNullOrWhiteSpace(expedition.returnDockId) ? GetCapitalIslandId() : expedition.returnDockId;
        DockingLocationKind dockKind = expedition.returnDockKind;
        Vector3 dockPosition = GetDockPositionOrFallback(dockId, dockKind);

        progress.SetDocked(dockId, dockKind, dockPosition);
        bool flagshipReturned = TryCompletePlayerFlagshipExpeditionAtDock(dockId, dockKind, out string flagshipReturnMessage);
        ApplySelectedShip();
        ApplySessionModeToShip();
        ResetCrashDetector();

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        lastSaveMessage = "Флагман вернулся из экспедиции: " + expeditionName + ".";
        if (flagshipReturned && !string.IsNullOrWhiteSpace(flagshipReturnMessage))
        {
            lastSaveMessage += " " + flagshipReturnMessage;
        }

        return true;
    }

    public bool CanBeginFlagshipExpedition(FlagshipExpeditionDefinition expedition, out string reason)
    {
        reason = "";
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        if (sessionExtractionCoreMode)
        {
            reason = "Flagship expeditions are disabled in session extraction core.";
            return false;
        }

        if (expedition == null)
        {
            reason = "Экспедиция не задана.";
            return false;
        }

        expedition.Normalize();

        if (progress.activeExpedition != null && progress.activeExpedition.active)
        {
            reason = "Экспедиция уже активна.";
            return false;
        }

        if (!IsDockedAtCapital())
        {
            reason = "Экспедицию можно начать только из столицы.";
            return false;
        }

        FlagshipInteriorState interior = FlagshipInteriorSimulator.EnsurePlayerFlagshipInterior(worldConfig, progress);
        if (interior == null)
        {
            reason = "Нужен флагман R" + expedition.minimumFlagshipRank + "+. Малые R0-R2 корабли в экспедиции не идут.";
            return false;
        }

        if (interior.rank < expedition.minimumFlagshipRank)
        {
            reason = "Нужен флагман R" + expedition.minimumFlagshipRank + "+, выбран R" + interior.rank + ".";
            return false;
        }

        reason = "Экспедиция доступна.";
        return true;
    }

    public IReadOnlyList<FlagshipExpeditionDefinition> GetFlagshipExpeditionConfigs()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        return worldConfig != null ? worldConfig.flagshipExpeditions : null;
    }

    private bool TryEnterFlagshipExpeditionScene(FlagshipExpeditionDefinition expedition, out string message)
    {
        message = "";
        if (expedition == null || string.IsNullOrWhiteSpace(expedition.sceneName))
        {
            return true;
        }

        string targetSceneName = expedition.sceneName.Trim();
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (string.Equals(targetSceneName, activeSceneName, StringComparison.Ordinal))
        {
            return true;
        }

        if (!TrySaveGame(true))
        {
            message = "Не удалось сохранить полет перед переходом в регион экспедиции.";
            return false;
        }

        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
            message = "Загрузка региона экспедиции: " + targetSceneName + ".";
            return true;
        }
        catch (Exception exception)
        {
            message = "Не удалось загрузить регион экспедиции: " + exception.Message;
            return false;
        }
    }

    private static string GetExpeditionDisplayName(FlagshipExpeditionDefinition expedition)
    {
        if (expedition == null) return "экспедиция";
        return string.IsNullOrWhiteSpace(expedition.displayNameRu) ? expedition.expeditionId : expedition.displayNameRu;
    }

    private static string GetExpeditionDisplayName(FlagshipExpeditionState expedition)
    {
        if (expedition == null) return "экспедиция";
        return string.IsNullOrWhiteSpace(expedition.displayNameRu) ? expedition.expeditionId : expedition.displayNameRu;
    }

    private bool TryStartPlayerFlagshipExpedition(out string message)
    {
        message = "";
        if (sessionExtractionCoreMode)
        {
            return false;
        }

        EnsureWorldConfigLoaded();

        FlagshipInteriorState interior = FlagshipInteriorSimulator.EnsurePlayerFlagshipInterior(worldConfig, progress);
        if (interior == null || interior.expeditionActive)
        {
            return false;
        }

        return FlagshipInteriorSimulator.StartExpedition(progress, FlagshipInteriorSimulator.PlayerFlagshipId, GetProcessUtcNow().Ticks, out message);
    }

    private bool TryCompletePlayerFlagshipExpeditionAtDock(string dockId, DockingLocationKind dockKind, out string message)
    {
        message = "";
        if (dockKind != DockingLocationKind.Island || !IsCapitalIsland(dockId))
        {
            return false;
        }

        bool hadActiveExpedition = progress != null &&
            progress.activeExpedition != null &&
            progress.activeExpedition.active;
        bool completed = TryCompletePlayerFlagshipExpedition(out message);
        if (hadActiveExpedition)
        {
            progress.activeExpedition.Clear();
            if (!completed && string.IsNullOrWhiteSpace(message))
            {
                message = "Экспедиция завершена: флагман вернулся в столицу.";
            }
        }

        return completed || hadActiveExpedition;
    }

    private bool TryCompletePlayerFlagshipExpedition(out string message)
    {
        message = "";
        FlagshipInteriorState interior = progress != null
            ? progress.GetFlagshipInteriorState(FlagshipInteriorSimulator.PlayerFlagshipId, false)
            : null;
        if (interior == null || !interior.expeditionActive)
        {
            return false;
        }

        return FlagshipInteriorSimulator.CompleteExpeditionReturn(progress, FlagshipInteriorSimulator.PlayerFlagshipId, out message);
    }

    public bool TryLoadShipCargoFromCurrentDock(string itemId, int amount, out string message, bool autoSave = true)
    {
        message = "";
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            message = "Legacy cargo loading is disabled in session extraction core. Use refuel, sortie extraction, and base processing.";
            lastSaveMessage = message;
            return false;
        }

        if (!TryResolveCurrentIslandStorage(out IslandConfig island, out IslandProductionState storage, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            message = "Некорректный груз.";
            return false;
        }

        if (storage.GetResourceAmount(itemId) < amount)
        {
            message = "На складе не хватает " + worldConfig.GetItemNameRu(itemId) + ".";
            return false;
        }

        Dictionary<string, int> plannedCargo = BuildShipCargoMap(itemId, amount);
        CargoCapacityInfo capacity = CalculateCargoCapacity();
        float plannedMassKg = CargoStoragePlanner.GetCargoMassKg(worldConfig, plannedCargo);
        if (plannedMassKg + capacity.currentTankKg > capacity.maxCargoKg + 0.001f)
        {
            message = "Нельзя загрузить: корабль перегружен.";
            return false;
        }

        if (!CargoStoragePlanner.TryValidateCargoStorage(worldConfig, capacity.cargoCompartments, plannedCargo, out string storageError))
        {
            message = "Нельзя загрузить: " + storageError;
            return false;
        }

        if (!storage.TrySpendResource(itemId, amount))
        {
            message = "Не удалось списать груз со склада " + GetIslandDisplayName(island) + ".";
            return false;
        }

        progress.AddShipCargo(itemId, amount);
        ApplyCargoMassToShip(GetActiveShip());
        if (autoSave)
        {
            AutoSaveIfDocked();
        }

        message = "Загружено: " + worldConfig.GetItemNameRu(itemId) + " x" + amount + ".";
        return true;
    }

    public bool TryUnloadShipCargoToCurrentDock(string itemId, int amount, out string message, bool autoSave = true)
    {
        message = "";
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            message = "Legacy cargo unloading is disabled in session extraction core. Extracted cargo returns through Extract home.";
            lastSaveMessage = message;
            return false;
        }

        if (!TryResolveCurrentIslandStorage(out IslandConfig island, out IslandProductionState storage, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            message = "Некорректный груз.";
            return false;
        }

        if (!progress.TrySpendShipCargo(itemId, amount))
        {
            message = "В трюме не хватает " + worldConfig.GetItemNameRu(itemId) + ".";
            return false;
        }

        storage.AddResource(itemId, amount);
        ApplyCargoMassToShip(GetActiveShip());
        if (autoSave)
        {
            AutoSaveIfDocked();
        }

        message = "Выгружено на склад " + GetIslandDisplayName(island) + ": " + worldConfig.GetItemNameRu(itemId) + " x" + amount + ".";
        return true;
    }

    public bool TryGetCurrentIslandStorage(out IslandConfig island, out IslandProductionState storage)
    {
        return TryResolveCurrentIslandStorage(out island, out storage, out _);
    }

    public bool AutoInstallRequiredModules(bool applyAndSave, out string message)
    {
        EnsureProgressInitialized();

        if (!ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out message))
        {
            return false;
        }

        if (applyAndSave)
        {
            ApplySelectedShip();
            AutoSaveIfDocked();
            lastSaveMessage = message;
        }

        return true;
    }

    public bool DockAt(string dockId, DockingLocationKind dockKind)
    {
        EnsureProgressInitialized();

        if (sessionExtractionCoreMode && progress.HasActiveSortie)
        {
            lastSaveMessage = "Docking is disabled during a core sortie. Reach the sortie boundary and extract home.";
            return false;
        }
        if (sessionExtractionCoreMode
            && (dockKind != DockingLocationKind.Island || !IsCapitalIsland(dockId)))
        {
            lastSaveMessage = "Legacy docks are disabled in session extraction core. The base is the only home dock.";
            return false;
        }

        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            ship.StopLeviathanHuntForDocking();
        }

        progress.SetDocked(dockId, dockKind, GetCurrentShipPosition());
        bool flagshipReturned = TryCompletePlayerFlagshipExpeditionAtDock(dockId, dockKind, out string flagshipReturnMessage);
        ApplySessionModeToShip();

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        if (flagshipReturned && !string.IsNullOrWhiteSpace(flagshipReturnMessage))
        {
            lastSaveMessage = string.IsNullOrWhiteSpace(lastSaveMessage)
                ? flagshipReturnMessage
                : lastSaveMessage + " " + flagshipReturnMessage;
        }

        return true;
    }

    public void CompleteFlightMission(MissionDefinitionSO mission)
    {
        EnsureProgressInitialized();

        if (sessionExtractionCoreMode)
        {
            progress.activeExpedition?.Clear();
            lastSaveMessage = "Legacy flight mission completion is disabled in session extraction core. Extract through the sortie boundary instead.";
            return;
        }

        if (mission != null)
        {
            AddMoney(mission.rewardMoney);
            AddExperienceToSelectedShip(mission.rewardExperience);
            progress.CompleteMission(mission.missionId);

            string dockId = string.IsNullOrWhiteSpace(mission.destinationDockId) ? "mission_destination" : mission.destinationDockId;
            DockAt(dockId, mission.destinationDockKind);
            return;
        }

        DockAt("unknown_dock", DockingLocationKind.Island);
    }

    public bool LoseShipAndReturnToCity(string reason = "")
    {
        EnsureProgressInitialized();

        if (missionController != null)
        {
            missionController.CancelMission();
        }

        ShipPartDefinitionSO starterHull = ActiveCatalog != null ? ActiveCatalog.GetStarterHull() : null;
        if (starterHull != null)
        {
            progress.ReplaceShipAssembly(starterHull.partId);
        }
        else
        {
            progress.ClearInstalledModules();
        }

        progress.ClearShipCargo();
        progress.ClearShipConsumableTanks();
        AddStartingShipConsumables();
        progress.StopCargoTransfer();
        ApplyStartingTechTreeNodes();
        ApplyStartingTechnologies();

        string assemblyMessage = "";
        bool assemblyReady = ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out assemblyMessage);
        string recoveryDockId = sessionExtractionCoreMode || string.IsNullOrWhiteSpace(startingDockId)
            ? GetCapitalIslandId()
            : startingDockId;
        DockingLocationKind recoveryDockKind = sessionExtractionCoreMode
            ? DockingLocationKind.Island
            : startingDockKind;
        Vector3 recoveryPosition = GetDockPositionOrFallback(recoveryDockId, recoveryDockKind);

        progress.SetDocked(recoveryDockId, recoveryDockKind, recoveryPosition);
        bool flagshipReturned = TryCompletePlayerFlagshipExpedition(out string flagshipReturnMessage);
        progress.activeExpedition?.Clear();
        ApplySelectedShip();
        ApplySessionModeToShip();
        ResetCrashDetector();

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        lastSaveMessage = "Корабль потерян. Возврат в город, выдан стартовый корабль.";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            lastSaveMessage += " Причина: " + reason + ".";
        }

        if (!assemblyReady && !string.IsNullOrWhiteSpace(assemblyMessage))
        {
            lastSaveMessage += " " + assemblyMessage;
        }

        if (flagshipReturned && !string.IsNullOrWhiteSpace(flagshipReturnMessage))
        {
            lastSaveMessage += " " + flagshipReturnMessage;
        }

        return assemblyReady;
    }

    public bool StartIdleMining()
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy idle mining is disabled in session extraction core. Use sorties and base processing.";
            return false;
        }

        if (!IsDocked) return false;
        if (progress.HasActiveProcess("idle_mining")) return false;

        DateTime now = GetProcessUtcNow();
        TimedProcessState process = new TimedProcessState
        {
            processId = "idle_mining",
            displayName = "Пассивная добыча руды",
            kind = TimedProcessKind.IdleMining,
            startedUtcTicks = now.Ticks,
            nextCompletionUtcTicks = now.Ticks + TimeSpan.FromSeconds(Mathf.Max(1, idleMiningIntervalSeconds)).Ticks,
            durationSeconds = Mathf.Max(1, idleMiningIntervalSeconds),
            repeat = true,
            remainingCycles = -1,
            outputResourceId = "ore",
            outputAmount = Mathf.Max(1, idleMiningOrePerCycle)
        };

        bool added = progress.AddActiveProcess(process);
        AutoSaveIfDocked();
        return added;
    }

    public bool StartIronSmelting()
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy iron smelting is disabled in session extraction core. Use base processing and cascade production.";
            return false;
        }

        if (!IsDocked) return false;
        if (!progress.TrySpendResource("ore", Mathf.Max(1, ironSmeltingOreCost))) return false;

        DateTime now = GetProcessUtcNow();
        string processId = "iron_smelting_" + now.Ticks;
        TimedProcessState process = new TimedProcessState
        {
            processId = processId,
            displayName = "Плавка железа",
            kind = TimedProcessKind.Crafting,
            startedUtcTicks = now.Ticks,
            nextCompletionUtcTicks = now.Ticks + TimeSpan.FromSeconds(Mathf.Max(1, ironSmeltingDurationSeconds)).Ticks,
            durationSeconds = Mathf.Max(1, ironSmeltingDurationSeconds),
            repeat = false,
            remainingCycles = 1,
            inputResourceId = "ore",
            inputAmount = Mathf.Max(1, ironSmeltingOreCost),
            outputResourceId = "iron",
            outputAmount = Mathf.Max(1, ironSmeltingIronOutput)
        };

        bool added = progress.AddActiveProcess(process);
        if (!added)
        {
            progress.AddResource("ore", Mathf.Max(1, ironSmeltingOreCost));
            return false;
        }

        AutoSaveIfDocked();
        return true;
    }

    public bool StartTimedMission(MissionDefinitionSO mission)
    {
        EnsureProgressInitialized();
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy timed missions are disabled in session extraction core. Use sortie contracts instead.";
            return false;
        }

        if (!IsDocked || mission == null || !mission.canRunAsTimedMission) return false;
        if (progress.HasActiveProcess("mission_" + mission.missionId)) return false;
        if (progress.IsMissionCompleted(mission.missionId)) return false;

        DateTime now = GetProcessUtcNow();
        int duration = mission.realTimeDurationSeconds > 0 ? mission.realTimeDurationSeconds : defaultTimedMissionDurationSeconds;

        TimedProcessState process = new TimedProcessState
        {
            processId = "mission_" + mission.missionId,
            displayName = string.IsNullOrWhiteSpace(mission.displayName) ? mission.missionId : mission.displayName,
            kind = TimedProcessKind.Mission,
            startedUtcTicks = now.Ticks,
            nextCompletionUtcTicks = now.Ticks + TimeSpan.FromSeconds(Mathf.Max(1, duration)).Ticks,
            durationSeconds = Mathf.Max(1, duration),
            repeat = false,
            remainingCycles = 1,
            missionId = mission.missionId,
            rewardMoney = mission.rewardMoney,
            rewardExperience = mission.rewardExperience,
            experienceShipId = GetSelectedExperienceTargetId()
        };

        progress.AcceptMission(mission.missionId);
        bool added = progress.AddActiveProcess(process);
        AutoSaveIfDocked();
        return added;
    }

    public int AdvanceRealTimeProcesses(DateTime utcNow)
    {
        if (isAdvancingProcesses || progress == null) return 0;

        isAdvancingProcesses = true;
        int completedCycles = 0;

        try
        {
            progress.Normalize();
            EnsureWorldConfigLoaded();
            SyncSessionExtractionCoreMode();
            EnsureEconomyRuntimeStates();

            if (progress.lastProcessUtcTicks <= 0)
            {
                progress.lastProcessUtcTicks = utcNow.Ticks;
            }

            long previousProcessTicks = progress.lastProcessUtcTicks;
            if (utcNow.Ticks <= previousProcessTicks)
            {
                return 0;
            }

            if (sessionExtractionCoreMode)
            {
                MigrateLegacyPersonalInventoryToCapitalStorage();
                DisableLegacyShopForSessionCore();
            }
            else
            {
                AdvanceShopRefresh(utcNow);
            }

            if (!sessionExtractionCoreMode && islandProductionEnabled)
            {
                completedCycles += IslandProductionSimulator.Advance(worldConfig, progress, previousProcessTicks, utcNow.Ticks);
                completedCycles += IslandIndustrySimulator.Advance(worldConfig, progress, previousProcessTicks, utcNow.Ticks);
            }

            if (!sessionExtractionCoreMode)
            {
                completedCycles += FlagshipInteriorSimulator.Advance(worldConfig, progress, previousProcessTicks, utcNow.Ticks, !suppressFlagshipMoraleDrain);
            }

            completedCycles += AdvanceCargoTransfer(utcNow);
            completedCycles += AdvanceTechnologyResearch(utcNow);
            if (!sessionExtractionCoreMode)
            {
                if (logisticsFleet != null)
                {
                    completedCycles += logisticsFleet.Advance(worldConfig, progress, ActiveCatalog, techTree, previousProcessTicks, utcNow.Ticks);
                }

                if (scoutFleet != null)
                {
                    completedCycles += scoutFleet.Advance(worldConfig, progress, previousProcessTicks, utcNow.Ticks);
                }

                if (gasHarvesterFleet != null)
                {
                    completedCycles += gasHarvesterFleet.Advance(worldConfig, progress, ActiveCatalog, techTree, previousProcessTicks, utcNow.Ticks);
                }

                completedCycles += MiningWorldSimulator.Advance(worldConfig, progress, previousProcessTicks, utcNow.Ticks);
                if (miningFleet != null)
                {
                    completedCycles += miningFleet.Advance(worldConfig, progress, ActiveCatalog, techTree, previousProcessTicks, utcNow.Ticks);
                }
            }

            if (sessionExtractionCoreMode)
            {
                StopLegacyTimedProcessesForSessionCore();
            }
            else
            {
                for (int i = progress.activeProcesses.Count - 1; i >= 0; i--)
                {
                    TimedProcessState process = progress.activeProcesses[i];
                    if (process == null)
                    {
                        progress.activeProcesses.RemoveAt(i);
                        continue;
                    }

                    process.Normalize();
                    if (process.nextCompletionUtcTicks <= 0 || utcNow.Ticks < process.nextCompletionUtcTicks)
                    {
                        continue;
                    }

                    long intervalTicks = TimeSpan.FromSeconds(Mathf.Max(1, process.durationSeconds)).Ticks;
                    long rawCycles = ((utcNow.Ticks - process.nextCompletionUtcTicks) / intervalTicks) + 1;
                    int cycles = (int)Math.Min(Math.Max(rawCycles, 1L), 10000L);

                    if (!process.repeat)
                    {
                        cycles = 1;
                    }
                    else if (process.remainingCycles > 0)
                    {
                        cycles = Mathf.Min(cycles, process.remainingCycles);
                    }

                    CompleteProcessCycles(process, cycles);
                    completedCycles += cycles;

                    if (process.repeat && (process.remainingCycles < 0 || process.remainingCycles > cycles))
                    {
                        if (process.remainingCycles > 0)
                        {
                            process.remainingCycles -= cycles;
                        }

                        process.nextCompletionUtcTicks += intervalTicks * cycles;
                    }
                    else
                    {
                        progress.activeProcesses.RemoveAt(i);
                    }
                }
            }

            progress.lastProcessUtcTicks = utcNow.Ticks;
        }
        finally
        {
            isAdvancingProcesses = false;
        }

        return completedCycles;
    }

    public bool TrySaveGame()
    {
        return TrySaveGame(false);
    }

    public bool TrySaveGameForSessionExit()
    {
        return TrySaveGame(true);
    }

    private bool TrySaveGame(bool allowFlightSave)
    {
        EnsureProgressInitialized();
        WildWindGameplaySession gameplaySession = WildWindGameplaySession.EnsureSessionForLoadedWorld(this, EffectiveSaveFileName);

        if (!IsDocked && !allowFlightSave)
        {
            lastSaveMessage = "Нельзя сохранить: сначала нужна стыковка.";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (Application.isPlaying && !sessionPaused)
        {
            AdvanceScaledRealTimeProcesses();
        }
        else if (sessionPaused)
        {
            ResetProcessRealtimeClock();
        }
        else
        {
            AdvanceRealTimeProcessesSliced(now);
        }
        SyncShipConsumablesWithCargo(false);
        if (IsDocked)
        {
            RememberCurrentDockPosition();
        }
        else
        {
            RememberCurrentFlightPose();
        }

        progress.lastSavedUtcTicks = now.Ticks;
        if (progress.lastProcessUtcTicks <= 0)
        {
            progress.lastProcessUtcTicks = now.Ticks;
        }
        progress.Normalize();

        MetaGameSaveData saveData = new MetaGameSaveData
        {
            version = MetaGameSaveData.CurrentVersion,
            progress = progress,
            worldManifest = CaptureWorldManifestForSave(),
            worldRuntime = CaptureWorldRuntimeForSave(),
            gameplaySession = CaptureGameplaySessionForSave(gameplaySession)
        };

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SavePath, JsonUtility.ToJson(saveData, true));
            lastSaveMessage = IsDocked ? "Сохранено: " + SavePath : "Полет сохранен: " + SavePath;
            return true;
        }
        catch (Exception exception)
        {
            lastSaveMessage = "Ошибка сохранения: " + exception.Message;
            Debug.LogWarning(lastSaveMessage);
            return false;
        }
    }

    public bool LoadGame()
    {
        string path = SavePath;
        if (!File.Exists(path))
        {
            lastSaveMessage = "Сохранение не найдено.";
            return false;
        }

        try
        {
            MetaGameSaveData saveData = JsonUtility.FromJson<MetaGameSaveData>(File.ReadAllText(path));
            if (saveData == null || saveData.progress == null)
            {
                lastSaveMessage = "Файл сохранения пуст.";
                return false;
            }

            progress = saveData.progress;
            ApplyWorldSaveData(saveData);
            ApplyGameplaySessionSaveData(saveData.gameplaySession);
            progress.Normalize();

            initialized = false;
            lastSaveMessage = "Загружено: " + path;
            return true;
        }
        catch (Exception exception)
        {
            lastSaveMessage = "Ошибка загрузки: " + exception.Message;
            Debug.LogWarning(lastSaveMessage);
            return false;
        }
    }

    private WorldManifestData CaptureWorldManifestForSave()
    {
        ResolveWorldSaveReferences();
        if (worldRuntime == null)
        {
            return null;
        }

        return WorldManifestData.FromRuntime(worldRuntime, "slot_" + Path.GetFileNameWithoutExtension(EffectiveSaveFileName));
    }

    private WorldRuntimeSaveData CaptureWorldRuntimeForSave()
    {
        ResolveWorldSaveReferences();
        return worldRuntimeState != null ? worldRuntimeState.CreateSaveData() : null;
    }

    private GameplaySessionSaveData CaptureGameplaySessionForSave(WildWindGameplaySession gameplaySession)
    {
        if (gameplaySession != null)
        {
            return gameplaySession.CreateSaveData();
        }

        ResolveWorldSaveReferences();
        WorldManifestData manifest = worldRuntime != null
            ? WorldManifestData.FromRuntime(worldRuntime, "slot_" + Path.GetFileNameWithoutExtension(EffectiveSaveFileName))
            : null;
        return GameplaySessionSaveData.CreateInitial(manifest, EffectiveSaveFileName, progress);
    }

    private void ApplyGameplaySessionSaveData(GameplaySessionSaveData gameplaySessionData)
    {
        WildWindGameplaySession gameplaySession = WildWindGameplaySession.EnsureSessionForLoadedWorld(this, EffectiveSaveFileName);
        if (gameplaySession == null)
        {
            return;
        }

        if (gameplaySessionData != null && gameplaySessionData.IsUsable)
        {
            gameplaySession.ApplySaveData(gameplaySessionData);
            return;
        }

        ResolveWorldSaveReferences();
        WorldManifestData manifest = worldRuntime != null
            ? WorldManifestData.FromRuntime(worldRuntime, "slot_" + Path.GetFileNameWithoutExtension(EffectiveSaveFileName))
            : null;
        gameplaySession.ApplySaveData(GameplaySessionSaveData.CreateInitial(manifest, EffectiveSaveFileName, progress));
    }

    private void ApplyWorldSaveData(MetaGameSaveData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        ResolveWorldSaveReferences();
        if (worldRuntime != null && saveData.worldManifest != null && saveData.worldManifest.IsUsable)
        {
            worldRuntime.LoadFromManifestData(saveData.worldManifest);
        }

        if (worldIndex != null && worldRuntime != null)
        {
            worldIndex.Configure(worldRuntime);
        }

        if (worldRuntimeState != null && worldRuntime != null)
        {
            Transform focus = worldRuntime.Focus != null ? worldRuntime.Focus : transform;
            worldRuntimeState.Configure(worldRuntime, worldIndex, focus);
            if (saveData.worldRuntime != null && saveData.worldRuntime.IsUsable)
            {
                worldRuntimeState.ApplySaveData(saveData.worldRuntime);
            }
            else
            {
                worldRuntimeState.InitializeFromWorld(false);
            }
        }
    }

    private void ResolveWorldSaveReferences()
    {
        if (worldRuntime == null)
        {
            worldRuntime = FindFirstObjectByType<WorldRegionRuntime>();
        }

        if (worldIndex == null)
        {
            worldIndex = FindFirstObjectByType<WorldEntityIndex>();
        }

        if (worldRuntimeState == null)
        {
            worldRuntimeState = FindFirstObjectByType<WorldRuntimeState>();
        }
    }

    public bool DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }

            progress = new PlayerProgress();
            initialized = false;
            EnsureProgressInitialized();
            ApplySelectedShip();
            ApplySessionModeToShip();
            lastSaveMessage = "Сохранение удалено.";
            return true;
        }
        catch (Exception exception)
        {
            lastSaveMessage = "Ошибка удаления: " + exception.Message;
            Debug.LogWarning(lastSaveMessage);
            return false;
        }
    }

    private void StopLegacyTimedProcessesForSessionCore()
    {
        if (progress == null || progress.activeProcesses == null || progress.activeProcesses.Count == 0) return;

        progress.activeProcesses.Clear();
        lastSaveMessage = "Legacy timed processes stopped: session extraction core uses sorties, base processing, cascade production, and technology cycles.";
    }

    private void CompleteProcessCycles(TimedProcessState process, int cycles)
    {
        if (process == null || cycles <= 0) return;

        if (!string.IsNullOrWhiteSpace(process.outputResourceId) && process.outputAmount > 0)
        {
            int totalAmount = (int)Math.Min((long)process.outputAmount * cycles, int.MaxValue);
            progress.AddResource(process.outputResourceId, totalAmount);
        }

        if (process.kind == TimedProcessKind.Mission)
        {
            if (process.rewardMoney > 0)
            {
                progress.money += process.rewardMoney;
            }

            if (process.rewardExperience > 0)
            {
                string shipId = string.IsNullOrWhiteSpace(process.experienceShipId) ? GetSelectedExperienceTargetId() : process.experienceShipId;
                progress.AddShipExperience(shipId, process.rewardExperience);
            }

            progress.CompleteMission(process.missionId);
        }
    }

    private void AdvanceShopRefresh(DateTime utcNow)
    {
        int intervalSeconds = Mathf.Max(1, shopRefreshIntervalSeconds);
        long intervalTicks = TimeSpan.FromSeconds(intervalSeconds).Ticks;

        if (progress.nextShopRefreshUtcTicks <= 0)
        {
            progress.nextShopRefreshUtcTicks = utcNow.Ticks + intervalTicks;
            progress.shopSeed = UnityEngine.Random.Range(1, int.MaxValue);
            return;
        }

        if (utcNow.Ticks < progress.nextShopRefreshUtcTicks) return;

        long refreshes = ((utcNow.Ticks - progress.nextShopRefreshUtcTicks) / intervalTicks) + 1;
        progress.nextShopRefreshUtcTicks += intervalTicks * refreshes;
        unchecked
        {
            progress.shopSeed = (progress.shopSeed * 1103515245) + 12345 + (int)refreshes;
        }
    }

    private int AdvanceCargoTransfer(DateTime utcNow)
    {
        if (progress == null || progress.cargoTransfer == null) return 0;

        CargoTransferState transfer = progress.cargoTransfer;
        transfer.Normalize();
        if (!transfer.active || !transfer.HasWork) return 0;
        if (sessionExtractionCoreMode)
        {
            StopCargoTransfer("Legacy cargo transfer stopped: session extraction core uses sortie extraction and base processing.");
            return 0;
        }

        IslandProductionState islandStorage = progress.GetIslandProductionState(transfer.islandId, true);
        if (islandStorage == null)
        {
            StopCargoTransfer("Погрузка остановлена: остров не найден.");
            return 0;
        }

        if (transfer.nextOperationUtcTicks <= 0)
        {
            transfer.nextOperationUtcTicks = utcNow.Ticks + TimeSpan.FromSeconds(Mathf.Max(0.01f, transfer.secondsPerItem)).Ticks;
        }

        int movedUnits = 0;
        int guard = 0;
        long intervalTicks = TimeSpan.FromSeconds(Mathf.Max(0.01f, transfer.secondsPerItem)).Ticks;
        while (transfer.active && transfer.HasWork && utcNow.Ticks >= transfer.nextOperationUtcTicks && guard < 10000)
        {
            CargoTransferOperation operation = transfer.operations[transfer.currentOperationIndex];
            if (operation == null || operation.remainingAmount <= 0)
            {
                transfer.currentOperationIndex++;
                continue;
            }

            if (!ApplyOneCargoTransferUnit(operation, islandStorage))
            {
                StopCargoTransfer("Погрузка остановлена: не хватает товара, грузоподъемности или подходящего отсека.");
                break;
            }

            operation.remainingAmount--;
            movedUnits++;
            transfer.nextOperationUtcTicks += intervalTicks;
            ApplyCargoMassToShip(GetActiveShip());

            if (operation.remainingAmount <= 0)
            {
                transfer.currentOperationIndex++;
            }

            guard++;
        }

        if (transfer.active && !transfer.HasWork)
        {
            StopCargoTransfer("Погрузка завершена.");
        }

        return movedUnits;
    }

    private bool ApplyOneCargoTransferUnit(CargoTransferOperation operation, IslandProductionState islandStorage)
    {
        if (operation == null || islandStorage == null || string.IsNullOrWhiteSpace(operation.itemId)) return false;

        if (operation.loadToShip)
        {
            CargoCapacityInfo capacity = CalculateCargoCapacity();
            Dictionary<string, int> cargoAfterLoad = CargoStoragePlanner.ToCargoMap(progress.shipCargo);
            cargoAfterLoad[operation.itemId] = cargoAfterLoad.TryGetValue(operation.itemId, out int current) ? current + 1 : 1;
            float cargoMassAfterLoad = CargoStoragePlanner.GetCargoMassKg(worldConfig, cargoAfterLoad);
            if (cargoMassAfterLoad + capacity.currentTankKg > capacity.maxCargoKg + 0.001f) return false;
            if (!CargoStoragePlanner.TryValidateCargoStorage(worldConfig, capacity.cargoCompartments, cargoAfterLoad, out _)) return false;
            if (!islandStorage.TrySpendResource(operation.itemId, 1)) return false;

            progress.AddShipCargo(operation.itemId, 1);
            return true;
        }

        if (!progress.TrySpendShipCargo(operation.itemId, 1)) return false;
        islandStorage.AddResource(operation.itemId, 1);
        return true;
    }

    private void StopCargoTransfer(string message)
    {
        if (progress?.cargoTransfer != null)
        {
            progress.cargoTransfer.active = false;
            progress.cargoTransfer.currentOperationIndex = 0;
            progress.cargoTransfer.operations.Clear();
        }

        lastSaveMessage = message;
    }

    private int AdvanceTechnologyResearch(DateTime utcNow)
    {
        if (progress == null || worldConfig == null || !worldConfig.isLoaded) return 0;
        if (string.IsNullOrWhiteSpace(progress.activeResearchTechnologyId)) return 0;

        TechnologyConfig technology = worldConfig.GetTechnology(progress.activeResearchTechnologyId);
        if (technology == null)
        {
            progress.activeResearchTechnologyId = "";
            return 0;
        }

        if (progress.IsTechnologyCompleted(technology.id))
        {
            progress.activeResearchTechnologyId = "";
            return 0;
        }

        if (!AreTechnologyPrerequisitesCompleted(technology, out _))
        {
            return 0;
        }

        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        IslandProductionState storage = progress.GetIslandProductionState(GetCapitalIslandId(), true);
        int changedCycles = 0;
        int guard = 0;

        while (guard < 10000)
        {
            guard++;

            if (state.completedCycles >= Mathf.Max(1, technology.requiredCycles))
            {
                CompleteTechnologyResearch(technology);
                changedCycles++;
                break;
            }

            if (!state.HasActiveCycle)
            {
                if (!TryStartTechnologyCycle(technology, state, storage, utcNow.Ticks, out _))
                {
                    break;
                }

                changedCycles++;
                if (state.activeCycleEndUtcTicks > utcNow.Ticks)
                {
                    break;
                }
            }

            if (!state.HasActiveCycle || utcNow.Ticks < state.activeCycleEndUtcTicks)
            {
                break;
            }

            long nextStartTicks = state.activeCycleEndUtcTicks;
            state.completedCycles++;
            state.activeCycleStartUtcTicks = 0;
            state.activeCycleEndUtcTicks = 0;
            changedCycles++;

            if (state.completedCycles >= Mathf.Max(1, technology.requiredCycles))
            {
                CompleteTechnologyResearch(technology);
                break;
            }

            if (!TryStartTechnologyCycle(technology, state, storage, nextStartTicks, out _))
            {
                break;
            }
        }

        return changedCycles;
    }

    private void CompleteTechnologyResearch(TechnologyConfig technology)
    {
        if (technology == null || progress == null) return;

        progress.CompleteTechnology(technology.id);
        progress.PurchaseNode(technology.id);

        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        state.completedCycles = Mathf.Max(state.completedCycles, Mathf.Max(1, technology.requiredCycles));
        state.activeCycleStartUtcTicks = 0;
        state.activeCycleEndUtcTicks = 0;

        if (progress.activeResearchTechnologyId == technology.id)
        {
            progress.activeResearchTechnologyId = "";
        }

        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out _);
        ApplySelectedShip();
        lastSaveMessage = "Технология завершена: " + GetTechnologyDisplayName(technology);
    }

    private bool TryStartOrContinueResearchCycle(TechnologyConfig technology, TechnologyResearchProgress state, long startTicks, out string reason)
    {
        reason = "";
        if (technology == null || state == null) return false;
        if (state.HasActiveCycle) return true;

        IslandProductionState storage = progress.GetIslandProductionState(GetCapitalIslandId(), true);
        return TryStartTechnologyCycle(technology, state, storage, startTicks, out reason);
    }

    private bool TryStartTechnologyCycle(TechnologyConfig technology, TechnologyResearchProgress state, IslandProductionState storage, long startTicks, out string reason)
    {
        reason = "";
        if (technology == null || state == null || storage == null)
        {
            reason = "Нет склада столицы для исследования.";
            return false;
        }

        if (!TrySpendTechnologyCycleCost(storage, technology, out reason))
        {
            return false;
        }

        int durationSeconds = Mathf.Max(0, technology.cycleTimeSeconds);
        state.activeCycleStartUtcTicks = startTicks;
        state.activeCycleEndUtcTicks = startTicks + TimeSpan.FromSeconds(durationSeconds).Ticks;
        reason = "Цикл исследования начат.";
        return true;
    }

    private bool AreTechnologyPrerequisitesCompleted(TechnologyConfig technology, out string reason)
    {
        reason = "";
        if (technology == null) return false;
        if (technology.prerequisiteTechnologyIds == null || technology.prerequisiteTechnologyIds.Count == 0) return true;

        for (int i = 0; i < technology.prerequisiteTechnologyIds.Count; i++)
        {
            string prerequisiteId = technology.prerequisiteTechnologyIds[i];
            if (string.IsNullOrWhiteSpace(prerequisiteId)) continue;

            if (!progress.IsTechnologyCompleted(prerequisiteId))
            {
                reason = "Нужна технология: " + worldConfig.GetTechnologyNameRu(prerequisiteId);
                return false;
            }
        }

        return true;
    }

    private bool HasTechnologyCycleCost(IslandProductionState storage, TechnologyConfig technology)
    {
        if (technology == null || storage == null) return false;
        if (technology.cycleCost == null || technology.cycleCost.Count == 0) return true;

        for (int i = 0; i < technology.cycleCost.Count; i++)
        {
            TechnologyCostConfig cost = technology.cycleCost[i];
            if (cost == null || string.IsNullOrWhiteSpace(cost.itemId) || cost.amount <= 0) continue;
            if (storage.GetResourceAmount(cost.itemId) < cost.amount) return false;
        }

        return true;
    }

    private bool TrySpendTechnologyCycleCost(IslandProductionState storage, TechnologyConfig technology, out string reason)
    {
        reason = "";
        if (technology == null || storage == null)
        {
            reason = "Нет склада столицы для исследования.";
            return false;
        }

        if (!HasTechnologyCycleCost(storage, technology))
        {
            reason = "На складе столицы не хватает ресурсов для цикла.";
            return false;
        }

        if (technology.cycleCost == null) return true;

        for (int i = 0; i < technology.cycleCost.Count; i++)
        {
            TechnologyCostConfig cost = technology.cycleCost[i];
            if (cost == null || string.IsNullOrWhiteSpace(cost.itemId) || cost.amount <= 0) continue;
            storage.TrySpendResource(cost.itemId, cost.amount);
        }

        return true;
    }

    public float GetRemainingShipCargoCapacityKg()
    {
        EnsureProgressInitialized();
        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (!capacity.assemblyValid) return 0f;
        return Mathf.Max(0f, capacity.maxCargoKg - capacity.currentCargoKg - capacity.currentTankKg);
    }

    public bool TryAddShipCargoFromRuntime(string resourceId, int amount, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            reason = "Нельзя добавить груз без id ресурса.";
            return false;
        }

        if (amount <= 0) return true;

        EnsureProgressInitialized();
        if (sessionExtractionCoreMode && !HasActiveSortie)
        {
            reason = "Runtime cargo collection is available only during an active core sortie.";
            return false;
        }
        if (sessionExtractionCoreMode && !CanCollectActiveSortieResource(resourceId, out reason))
        {
            return false;
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        float addedMassKg = worldConfig != null ? worldConfig.GetItemTransportMassKg(resourceId, amount) : amount;
        float freeKg = Mathf.Max(0f, capacity.maxCargoKg - capacity.currentCargoKg - capacity.currentTankKg);
        if (!capacity.assemblyValid || freeKg + 0.001f < addedMassKg)
        {
            reason = $"Не хватает грузоподъемности: нужно {addedMassKg:F1} кг, свободно {freeKg:F1} кг.";
            return false;
        }

        Dictionary<string, int> cargoAfterAdd = CargoStoragePlanner.ToCargoMap(progress.shipCargo);
        cargoAfterAdd[resourceId] = cargoAfterAdd.TryGetValue(resourceId, out int current) ? current + amount : amount;
        if (!CargoStoragePlanner.TryValidateCargoStorage(worldConfig, capacity.cargoCompartments, cargoAfterAdd, out string storageError))
        {
            reason = storageError;
            return false;
        }

        progress.AddShipCargo(resourceId, amount);
        ApplyCargoMassToShip(GetActiveShip());
        ResetCargoPlan();
        return true;
    }

    private bool CanCollectActiveSortieResource(string resourceId, out string reason)
    {
        reason = "";
        SortieZoneDefinition zone = progress != null && progress.activeSortie != null
            ? progress.activeSortie.zone
            : null;
        if (zone == null)
        {
            reason = "No active sortie resource catalog is available.";
            return false;
        }

        zone.Normalize();
        if (string.IsNullOrWhiteSpace(zone.starterResourceItemId))
        {
            reason = "Active sortie has no collectible resource configured.";
            return false;
        }

        if (resourceId == zone.starterResourceItemId)
        {
            return true;
        }

        reason = "Active sortie accepts only " + zone.starterResourceItemId + ", not " + resourceId + ".";
        return false;
    }

    public bool TrySpendFractionalShipCargoFromRuntime(string resourceId, float amountKg, ref float spendBufferKg, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            reason = "Нельзя потратить ресурс без id.";
            return false;
        }

        if (amountKg <= 0f) return true;

        EnsureProgressInitialized();
        progress.Normalize();

        spendBufferKg = Mathf.Clamp(spendBufferKg, 0f, 0.999f);
        int stockKg = progress.GetShipCargoAmount(resourceId);
        string resourceName = worldConfig != null ? worldConfig.GetItemNameRu(resourceId) : resourceId;
        if (stockKg <= 0)
        {
            spendBufferKg = 0f;
            reason = "На борту нет " + resourceName + ".";
            return false;
        }

        float availableKg = Mathf.Max(0f, stockKg - spendBufferKg);
        if (availableKg + 0.0001f < amountKg)
        {
            reason = $"Не хватает {resourceName}: нужно {amountKg:0.0} кг, доступно {availableKg:0.0} кг.";
            return false;
        }

        spendBufferKg += amountKg;
        int wholeKg = Mathf.FloorToInt(spendBufferKg + 0.0001f);
        if (wholeKg > 0)
        {
            if (!progress.TrySpendShipCargo(resourceId, wholeKg))
            {
                reason = "Не удалось списать " + resourceName + ".";
                return false;
            }

            spendBufferKg -= wholeKg;
            ApplyCargoMassToShip(GetActiveShip());
            ResetCargoPlan();
        }

        return true;
    }

    public float GetRemainingShipImpactCargoCapacityKg(float impactHoldCapacityKg)
    {
        return GetRemainingShipCargoCapacityKg();
    }

    public bool TryAddShipImpactCargoFromRuntime(string resourceId, int amount, float impactHoldCapacityKg, out string reason)
    {
        return TryAddShipCargoFromRuntime(resourceId, amount, out reason);
    }

    public bool UnloadShipImpactCargoToCurrentIsland(out string reason)
    {
        EnsureProgressInitialized();
        reason = "Отдельного кузова больше нет: пойманная руда лежит в общем грузе корабля и выгружается обычной разгрузкой.";
        return false;
    }

    private struct SortieEntryState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public float speedMS;
    }

    private SortieEntryState BuildSortieEntryState(SortieZoneDefinition zone, Vector3 launchDockPosition, ShipPhysics ship)
    {
        if (zone == null)
        {
            return new SortieEntryState
            {
                position = Vector3.zero,
                rotation = Quaternion.identity,
                velocity = Vector3.zero,
                speedMS = 0f
            };
        }

        zone.Normalize();
        float entrySpeedMS = ResolveSortieEntrySpeedMS(ship);
        Vector3 outward = FlattenSortieVector(launchDockPosition - zone.centerPosition);
        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = FlattenSortieVector(zone.entryPosition - zone.centerPosition);
        }

        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = Vector3.right;
        }

        outward.Normalize();
        Vector3 inbound = -outward;
        float spawnRadius = Mathf.Max(0f, zone.radiusMeters) + entrySpeedMS * SortieEntryApproachSeconds;
        float altitude = Mathf.Max(zone.entryPosition.y, zone.stormFloorY + 50f);
        Vector3 position = new Vector3(
            zone.centerPosition.x + outward.x * spawnRadius,
            altitude,
            zone.centerPosition.z + outward.z * spawnRadius);
        Quaternion rotation = inbound.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(inbound, Vector3.up)
            : Quaternion.identity;

        return new SortieEntryState
        {
            position = position,
            rotation = rotation,
            velocity = inbound * entrySpeedMS,
            speedMS = entrySpeedMS
        };
    }

    private static float ResolveSortieEntrySpeedMS(ShipPhysics ship)
    {
        if (ship != null)
        {
            return Mathf.Max(1f, ship.EstimateFullSlipstreamCruiseSpeedMS());
        }

        return SortieEntryDefaultMaxSpeedMS;
    }

    private void PlaceShipAtSortieEntry(SortieZoneDefinition zone, SortieEntryState entryState)
    {
        if (zone == null || progress == null) return;

        if (progress.activeSortie != null && progress.activeSortie.zone != null)
        {
            progress.activeSortie.zone.entryPosition = entryState.position;
        }

        progress.SetFlightPose(entryState.position, entryState.rotation);
        ApplySessionModeToShip();

        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            Rigidbody body = ship.GetComponent<Rigidbody>();
            ship.transform.SetPositionAndRotation(entryState.position, entryState.rotation);
            if (body != null)
            {
                body.position = entryState.position;
                body.rotation = entryState.rotation;
                body.linearVelocity = entryState.velocity;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            ship.altitudeHold = false;
            ship.targetAltitude = entryState.position.y;
            ship.headingHold = false;
            ship.targetHeading = HeadingFromSortieVector(zone.centerPosition - entryState.position);
            ship.cruiseControl = false;
            ship.thrustInput = 1f;
            ship.propellerPitch = 1f;
            ship.sideInput = 0f;
            ship.turnInput = 0f;
            ship.enginePowerLever = ship.CalculateEnginePowerLeverForPropellerEngagement(1f);
            ship.claudiumSlipstreamEnabled = true;
            ship.claudiumSlipstreamCharge01 = 1f;
        }

        WildWindFlightControlBridge controls = WildWindFlightControlBridge.EnsureInstance();
        if (controls != null)
        {
            controls.PrimeSortieEntryCruise(zone.centerPosition, entryState.speedMS);
            controls.ApplyNow();
        }

        progress.SetFlightPose(entryState.position, entryState.rotation);
    }

    private SortieReturnProfile BuildCurrentSortieReturnProfile()
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();

        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            ApplyFuelConfigToShip(ship);
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        SortieZoneDefinition zone = progress.activeSortie != null ? progress.activeSortie.zone : null;
        bool activeSortieReturn = zone != null;
        float returnPowerLever = activeSortieReturn ? zone.returnPowerLever : 0.7f;
        string fuelId = ship != null && !string.IsNullOrWhiteSpace(ship.engineFuelId)
            ? ship.engineFuelId
            : GetStartingEngineFuelId();
        string claudiumId = GetClaudiumResourceId(ship);
        float fuelEnergyKwhPerKg = ship != null ? ship.engineFuelEnergyKwhPerKg : 4f;
        ItemConfig fuel = worldConfig != null ? worldConfig.GetItem(fuelId) : null;
        if (fuel != null && fuel.energyKwhPerKg > 0f)
        {
            fuelEnergyKwhPerKg = fuel.energyKwhPerKg;
        }

        float emptyMassKg = capacity.emptyMassKg > 0f
            ? capacity.emptyMassKg
            : ship != null ? ship.baseMass : 0f;
        float cruiseSpeedMS = activeSortieReturn && zone.returnCruiseSpeedMS > 0f
            ? zone.returnCruiseSpeedMS
            : 35f * Mathf.Clamp(returnPowerLever, 0.1f, 1f);
        float enginePowerKw = ship != null ? ship.enginePowerKwAt100 : 0f;
        float cruisePowerKw = activeSortieReturn
            ? 0f
            : enginePowerKw * Mathf.Clamp(returnPowerLever, 0.05f, 1.2f);
        float claudiumConsumptionPerTonSecond = activeSortieReturn
            ? 0f
            : ship != null ? ship.claudiumConsumptionPerTonSecond : 0f;

        return new SortieReturnProfile
        {
            emptyMassKg = emptyMassKg,
            cargoMassKg = progress.GetShipPayloadMassKg(worldConfig),
            cruiseSpeedMS = cruiseSpeedMS,
            enginePowerKw = enginePowerKw,
            cruisePowerKw = cruisePowerKw,
            engineFuelEfficiency = ship != null ? ship.engineFuelEfficiency : 0.32f,
            fuelEnergyKwhPerKg = fuelEnergyKwhPerKg,
            claudiumConsumptionPerTonSecond = claudiumConsumptionPerTonSecond,
            currentCoalKg = progress.shipEngineFuelTank.GetAmount(fuelId),
            currentClaudiumKg = progress.shipClaudiumTank.GetAmount(claudiumId),
            coalResourceId = fuelId,
            claudiumResourceId = claudiumId
        };
    }

    private static Vector3 GetSortieOutwardDirection(SortieZoneDefinition zone, Vector3 position)
    {
        if (zone == null)
        {
            return Vector3.right;
        }

        zone.Normalize();
        Vector3 delta = new Vector3(
            position.x - zone.centerPosition.x,
            0f,
            position.z - zone.centerPosition.z);
        return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.right;
    }

    private static Vector3 FlattenSortieVector(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HeadingFromSortieVector(Vector3 value)
    {
        value = FlattenSortieVector(value);
        if (value.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        float angle = Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    private int TransferShipCargoToCapital()
    {
        EnsureProgressInitialized();

        IslandProductionState capitalStorage = GetCapitalStorageState();
        if (capitalStorage == null) return 0;

        int transferred = 0;
        progress.shipCargo ??= new List<ResourceStack>();
        for (int i = 0; i < progress.shipCargo.Count; i++)
        {
            ResourceStack stack = progress.shipCargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;

            transferred += capitalStorage.AddResource(stack.resourceId, stack.amount);
        }

        return transferred;
    }

    private OreTypeConfig FindFirstStoredOreType(IslandProductionState storage, out int availableOre)
    {
        availableOre = 0;
        if (storage == null || worldConfig == null || worldConfig.oreTypes == null) return null;

        for (int i = 0; i < worldConfig.oreTypes.Count; i++)
        {
            OreTypeConfig oreType = worldConfig.oreTypes[i];
            if (oreType == null || string.IsNullOrWhiteSpace(oreType.oreItemId)) continue;

            int amount = storage.GetResourceAmount(oreType.oreItemId);
            if (amount <= 0) continue;

            availableOre = amount;
            return oreType;
        }

        return null;
    }

    private GasCloudTypeConfig FindFirstStoredGasCloudType(IslandProductionState storage, out int availableCondensate)
    {
        availableCondensate = 0;
        if (storage == null || worldConfig == null || worldConfig.gasCloudTypes == null) return null;

        for (int i = 0; i < worldConfig.gasCloudTypes.Count; i++)
        {
            GasCloudTypeConfig gasType = worldConfig.gasCloudTypes[i];
            if (gasType == null || string.IsNullOrWhiteSpace(gasType.condensateItemId)) continue;

            int amount = storage.GetResourceAmount(gasType.condensateItemId);
            if (amount <= 0) continue;

            availableCondensate = amount;
            return gasType;
        }

        return null;
    }

    private LeviathanTypeConfig FindFirstStoredLeviathanType(IslandProductionState storage, out int availableCarcass)
    {
        availableCarcass = 0;
        if (storage == null || worldConfig == null || worldConfig.leviathanTypes == null) return null;

        for (int i = 0; i < worldConfig.leviathanTypes.Count; i++)
        {
            LeviathanTypeConfig leviathanType = worldConfig.leviathanTypes[i];
            if (leviathanType == null || string.IsNullOrWhiteSpace(leviathanType.carcassItemId)) continue;

            int amount = storage.GetResourceAmount(leviathanType.carcassItemId);
            if (amount <= 0) continue;

            availableCarcass = amount;
            return leviathanType;
        }

        return null;
    }

    private static string FindFirstStoredAutomatonInput(IslandProductionState storage, out int availableSalvage)
    {
        availableSalvage = 0;
        if (storage == null) return "";

        int brokenAutomatons = storage.GetResourceAmount(SessionExtractionConstants.BrokenAutomatonItemId);
        if (brokenAutomatons > 0)
        {
            availableSalvage = brokenAutomatons;
            return SessionExtractionConstants.BrokenAutomatonItemId;
        }

        return "";
    }

    private static string FindFirstStoredCyberInfoInput(IslandProductionState storage, out int availableInfo)
    {
        availableInfo = 0;
        if (storage == null) return "";

        int rockInfo = storage.GetResourceAmount(SessionExtractionConstants.RockInfoItemId);
        if (rockInfo > 0)
        {
            availableInfo = rockInfo;
            return SessionExtractionConstants.RockInfoItemId;
        }

        int cloudInfo = storage.GetResourceAmount(SessionExtractionConstants.CloudInfoItemId);
        if (cloudInfo > 0)
        {
            availableInfo = cloudInfo;
            return SessionExtractionConstants.CloudInfoItemId;
        }

        int leviathanInfo = storage.GetResourceAmount(SessionExtractionConstants.LeviathanInfoItemId);
        if (leviathanInfo > 0)
        {
            availableInfo = leviathanInfo;
            return SessionExtractionConstants.LeviathanInfoItemId;
        }

        return "";
    }

    private bool TryGetAvailableBaseProcessingInput(BaseProcessingBranch branch, IslandProductionState storage, out string inputItemId, out int available)
    {
        inputItemId = "";
        available = 0;
        if (storage == null) return false;

        switch (branch)
        {
            case BaseProcessingBranch.Ore:
            {
                OreTypeConfig oreType = FindFirstStoredOreType(storage, out available);
                inputItemId = oreType != null ? oreType.oreItemId : "";
                return oreType != null && available > 0;
            }
            case BaseProcessingBranch.Gas:
            {
                GasCloudTypeConfig gasType = FindFirstStoredGasCloudType(storage, out available);
                inputItemId = gasType != null ? gasType.condensateItemId : "";
                return gasType != null && available > 0;
            }
            case BaseProcessingBranch.AutomatonDismantling:
                inputItemId = FindFirstStoredAutomatonInput(storage, out available);
                return !string.IsNullOrWhiteSpace(inputItemId) && available > 0;
            case BaseProcessingBranch.LeviathanProcessing:
            {
                LeviathanTypeConfig leviathanType = FindFirstStoredLeviathanType(storage, out available);
                inputItemId = leviathanType != null ? leviathanType.carcassItemId : "";
                return leviathanType != null && available > 0;
            }
            case BaseProcessingBranch.CyberneticDeciphering:
                inputItemId = FindFirstStoredCyberInfoInput(storage, out available);
                return !string.IsNullOrWhiteSpace(inputItemId) && available > 0;
            default:
                return false;
        }
    }

    private static int GetProcessingBatchSize(BaseProcessingLineState line, int available)
    {
        if (line == null || available <= 0) return 0;
        return Mathf.Min(available, Mathf.Max(1, Mathf.FloorToInt(line.capacityUnitsPerMinute)));
    }

    private static int AddOreProcessingOutputs(IslandProductionState storage, OreTypeConfig oreType, int batchKg)
    {
        if (storage == null || oreType == null || oreType.composition == null || batchKg <= 0) return 0;

        int totalOutput = 0;
        for (int i = 0; i < oreType.composition.Count; i++)
        {
            OreMineralCompositionConfig composition = oreType.composition[i];
            if (composition == null || string.IsNullOrWhiteSpace(composition.mineralItemId) || composition.share <= 0f) continue;

            int outputKg = Mathf.FloorToInt(batchKg * composition.share + 0.0001f);
            if (outputKg <= 0 && batchKg > 0)
            {
                outputKg = 1;
            }

            totalOutput += storage.AddResource(composition.mineralItemId, outputKg);
        }

        return totalOutput;
    }

    private static int AddGasProcessingOutputs(IslandProductionState storage, GasCloudTypeConfig gasType, int batch)
    {
        if (storage == null || gasType == null || gasType.composition == null || batch <= 0) return 0;

        int totalOutput = 0;
        for (int i = 0; i < gasType.composition.Count; i++)
        {
            GasCloudCompositionConfig composition = gasType.composition[i];
            if (composition == null || string.IsNullOrWhiteSpace(composition.itemId) || composition.share <= 0f) continue;

            totalOutput += AddScaledProcessingOutput(storage, composition.itemId, batch, composition.share);
        }

        return totalOutput;
    }

    private static int AddScaledProcessingOutput(IslandProductionState storage, string itemId, int inputAmount, float share)
    {
        if (storage == null || string.IsNullOrWhiteSpace(itemId) || inputAmount <= 0 || share <= 0f) return 0;

        int outputAmount = Mathf.FloorToInt(inputAmount * share + 0.0001f);
        if (outputAmount <= 0)
        {
            outputAmount = 1;
        }

        return storage.AddResource(itemId, outputAmount);
    }

    private void ApplyCascadeOrderLoad(CascadeProductionOrderDefinition order)
    {
        if (progress == null || order == null) return;

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        order.Normalize();

        for (int i = 0; i < order.loads.Count; i++)
        {
            CascadeProductionLoad load = order.loads[i];
            if (load == null || load.loadUnits <= 0f) continue;

            CascadeProductionLineState line = progress.baseIndustry.GetProduction(load.type);
            line.totalLoadApplied += load.loadUnits;
        }
    }

    private static string BuildCascadeOutputsText(CascadeProductionOrderDefinition order)
    {
        if (order == null || order.outputs == null || order.outputs.Count == 0)
        {
            return "nothing";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < order.outputs.Count; i++)
        {
            CascadeItemAmount output = order.outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0) continue;

            parts.Add(output.itemId + " x" + output.amount);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    private bool TryInstallStarterFittingUpgrade(
        string kitItemId,
        string moduleId,
        string preferredSlotId,
        string slotTypeId,
        string alreadyInstalledMessage,
        string successMessagePrefix,
        out string message)
    {
        EnsureProgressInitialized();
        EnsureWorldConfigLoaded();
        message = "";

        if (!ResolveStarterFittingUpgrade(
                kitItemId,
                moduleId,
                preferredSlotId,
                slotTypeId,
                alreadyInstalledMessage,
                out IslandProductionState storage,
                out ShipPartDefinitionSO module,
                out ShipSlotDefinition slot,
                out message))
        {
            lastSaveMessage = message;
            return false;
        }

        if (!storage.TrySpendResource(kitItemId, 1))
        {
            message = "Upgrade blocked: could not spend " + kitItemId + ".";
            lastSaveMessage = message;
            return false;
        }

        progress.InstallModule(slot.slotId, module.partId);
        ApplySelectedShip();
        AutoSaveIfDocked();

        message = successMessagePrefix + ": " + GetPartName(module) + ".";
        lastSaveMessage = message;
        return true;
    }

    private bool ResolveStarterFittingUpgrade(
        string kitItemId,
        string moduleId,
        string preferredSlotId,
        string slotTypeId,
        string alreadyInstalledMessage,
        out IslandProductionState storage,
        out ShipPartDefinitionSO module,
        out ShipSlotDefinition slot,
        out string message)
    {
        storage = null;
        module = null;
        slot = null;
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Starter fitting upgrades are available only at the base.";
            return false;
        }

        storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            return false;
        }

        if (storage.GetResourceAmount(kitItemId) <= 0)
        {
            message = "Upgrade blocked: build " + kitItemId + " first.";
            return false;
        }

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null)
        {
            message = "Ship catalog is missing.";
            return false;
        }

        module = activeCatalog.GetPartById(moduleId);
        if (module == null || !module.IsModule)
        {
            message = "Upgrade module is missing: " + moduleId + ".";
            return false;
        }

        if (!ShipAssemblyBuilder.IsPartUsable(module, techTree, progress))
        {
            message = "Upgrade module is not researched: " + GetPartName(module) + ".";
            return false;
        }

        slot = FindAssemblySlotForUpgrade(activeCatalog, preferredSlotId, slotTypeId);
        if (slot == null)
        {
            message = "No " + GetSlotTypeDisplayName(slotTypeId) + " slot is available on the selected hull.";
            return false;
        }

        string currentModuleId = progress.GetInstalledModule(slot.slotId);
        if (currentModuleId == module.partId)
        {
            message = alreadyInstalledMessage;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(currentModuleId))
        {
            message = GetSlotTypeDisplayName(slotTypeId) + " slot is already occupied by " + currentModuleId + ".";
            return false;
        }

        if (!module.CanFitSlot(slot))
        {
            message = GetPartName(module) + " cannot fit " + slot.displayName + ".";
            return false;
        }

        return true;
    }

    private static string GetSlotTypeDisplayName(string slotTypeId)
    {
        return slotTypeId switch
        {
            SessionExtractionConstants.HighSlotTypeId => "High",
            SessionExtractionConstants.MidSlotTypeId => "Mid",
            SessionExtractionConstants.LowSlotTypeId => "Low",
            SessionExtractionConstants.RigSlotTypeId => "Rig",
            _ => string.IsNullOrWhiteSpace(slotTypeId) ? "fitting" : slotTypeId
        };
    }

    private ShipSlotDefinition FindAssemblySlotForUpgrade(ShipCatalogSO activeCatalog, string preferredSlotId, string slotTypeId)
    {
        List<ShipSlotDefinition> slots = GetAssemblySlotsForUi(activeCatalog);
        ShipSlotDefinition fallback = null;
        for (int i = 0; i < slots.Count; i++)
        {
            ShipSlotDefinition candidate = slots[i];
            if (candidate == null) continue;

            if (candidate.slotId == preferredSlotId)
            {
                return candidate;
            }

            if (fallback == null && candidate.slotTypeId == slotTypeId)
            {
                fallback = candidate;
            }
        }

        return fallback;
    }

    private string CountFittingBandText(List<ShipSlotDefinition> slots, ShipCatalogSO activeCatalog, string slotTypeId)
    {
        int total = 0;
        int filled = 0;
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ShipSlotDefinition slot = slots[i];
                if (slot == null || slot.slotTypeId != slotTypeId) continue;

                total++;
                ShipPartDefinitionSO module = activeCatalog != null ? activeCatalog.GetPartById(progress.GetInstalledModule(slot.slotId)) : null;
                if (module != null && module.IsModule && module.CanFitSlot(slot))
                {
                    filled++;
                }
            }
        }

        return filled + "/" + total;
    }

    private string BuildFittingBandSummary(List<ShipSlotDefinition> slots, ShipCatalogSO activeCatalog, string slotTypeId)
    {
        List<string> parts = new List<string>();
        if (slots != null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ShipSlotDefinition slot = slots[i];
                if (slot == null || slot.slotTypeId != slotTypeId) continue;

                ShipPartDefinitionSO module = activeCatalog != null ? activeCatalog.GetPartById(progress.GetInstalledModule(slot.slotId)) : null;
                string moduleName = module != null && module.IsModule && module.CanFitSlot(slot)
                    ? GetPartName(module)
                    : "empty";
                parts.Add(slot.displayName + "=" + moduleName);
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "none";
    }

    private string BuildStarterAirframeCascadeSummary()
    {
        CascadeProductionEstimate estimate = EstimateStarterAirframeCascade();
        return "Starter airframe kit bottleneck: "
            + SessionExtractionIndustry.GetProductionDisplayName(estimate.bottleneck)
            + " ~" + estimate.bottleneckMinutes.ToString("F1") + " min."
            + (estimate.canRun ? "" : " " + estimate.blockedReason);
    }

    private CargoCapacityInfo CalculateCargoCapacity()
    {
        CargoCapacityInfo info = new CargoCapacityInfo
        {
            assemblyValid = false,
            canFly = false,
            reason = "сборка корабля не проверена.",
            currentCargoKg = progress != null ? progress.GetShipCargoMassKg(worldConfig) : 0f,
            currentTankKg = progress != null ? progress.GetShipConsumableTankMassKg() : 0f
        };

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null || !activeCatalog.HasAssemblyParts())
        {
            ShipPhysics ship = GetActiveShip();
            if (ship == null)
            {
                info.reason = "корабль не найден.";
                return info;
            }

            info.assemblyValid = true;
            info.emptyMassKg = ship.baseMass;
            info.engineLiftKg = CalculateEngineLiftKg(ship.enginePowerKwAt100, ship.claudiumLiftEfficiency);
            info.claudiumMaxLiftKg = ship.claudiumMaxLiftKg;
            info.hullLimitKg = ship.hullMaxTakeoffMassKg;
            return CompleteCargoCapacity(info);
        }

        if (!ShipAssemblyBuilder.TryBuild(activeCatalog, techTree, progress, out ShipAssemblyResult result))
        {
            info.reason = result != null ? result.message : "сборка корабля неверная.";
            return info;
        }

        ShipPhysics activeShip = GetActiveShip();
        ShipStatBlock stats = result.stats;
        info.assemblyValid = true;
        info.cargoCompartments = CargoStoragePlanner.BuildStatCompartments(stats);
        if (activeShip != null)
        {
            info.emptyMassKg = activeShip.baseMass;
            info.engineLiftKg = CalculateEngineLiftKg(activeShip.enginePowerKwAt100, activeShip.claudiumLiftEfficiency);
            info.claudiumMaxLiftKg = activeShip.claudiumMaxLiftKg;
            info.hullLimitKg = activeShip.hullMaxTakeoffMassKg;
        }
        else
        {
            info.emptyMassKg = stats.Get(ShipStatId.BaseMass, 0f);
            info.engineLiftKg = CalculateEngineLiftKg(stats.Get(ShipStatId.EngineMaxPower, 0f), stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f));
            info.claudiumMaxLiftKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
            info.hullLimitKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, 2000f);
        }

        return CompleteCargoCapacity(info);
    }

    private static float CalculateEngineLiftKg(float enginePowerKw, float liftKgPerKw)
    {
        return Mathf.Max(0f, enginePowerKw) * Mathf.Max(0f, liftKgPerKw);
    }

    private CargoCapacityInfo CompleteCargoCapacity(CargoCapacityInfo info)
    {
        info.allowedTakeoffMassKg = Mathf.Min(info.engineLiftKg, Mathf.Min(info.claudiumMaxLiftKg, info.hullLimitKg));
        info.maxCargoKg = Mathf.Max(0f, info.allowedTakeoffMassKg - info.emptyMassKg);
        info.fuelTankCapacityKg = ShipConsumableTankMath.CalculateFuelTankCapacityKg(info.maxCargoKg);
        info.claudiumTankCapacityKg = ShipConsumableTankMath.CalculateClaudiumTankCapacityKg(info.maxCargoKg);

        if (!info.assemblyValid)
        {
            return info;
        }

        ClampPlayerConsumableTanks(ref info);

        if (info.engineLiftKg <= 0f)
        {
            info.reason = "двигатель и контур не дают подъемной силы.";
            return info;
        }

        if (info.claudiumMaxLiftKg <= 0f)
        {
            info.reason = "у клавдиевого контура нет максимальной подъемной силы.";
            return info;
        }

        if (info.hullLimitKg <= 0f)
        {
            info.reason = "у корпуса не задана максимальная взлетная масса.";
            return info;
        }

        if (info.emptyMassKg > info.allowedTakeoffMassKg + 0.001f)
        {
            info.reason = $"сухая масса {info.emptyMassKg:F0} кг больше разрешенной взлетной массы {info.allowedTakeoffMassKg:F0} кг.";
            return info;
        }

        float currentPayloadKg = info.currentCargoKg + info.currentTankKg;
        if (currentPayloadKg > info.maxCargoKg + 0.001f)
        {
            info.reason = $"полезная нагрузка {currentPayloadKg:F0} кг больше доступной грузоподъемности {info.maxCargoKg:F0} кг.";
            return info;
        }

        if (!CargoStoragePlanner.TryValidateCargoStorage(worldConfig, info.cargoCompartments, progress != null ? progress.shipCargo : null, out string storageError))
        {
            info.reason = storageError;
            return info;
        }

        info.canFly = true;
        info.reason = "масса в норме.";
        return info;
    }

    private void ClampPlayerConsumableTanks(ref CargoCapacityInfo info)
    {
        if (progress == null) return;

        progress.shipEngineFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        progress.shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        progress.shipEngineFuelTank.amountKg = Mathf.Min(progress.shipEngineFuelTank.amountKg, info.fuelTankCapacityKg);
        progress.shipClaudiumTank.amountKg = Mathf.Min(progress.shipClaudiumTank.amountKg, info.claudiumTankCapacityKg);
        info.currentTankKg = progress.GetShipConsumableTankMassKg();
    }

    private void ApplyCargoMassToShip(ShipPhysics ship)
    {
        if (ship == null || progress == null) return;

        ApplyFuelConfigToShip(ship);
        SyncTankResourceFromRuntime(progress.shipEngineFuelTank, ship.engineFuelId, ref syncedFuelResourceId, ref ship.engineFuelStockKg);
        string claudiumResourceId = GetClaudiumResourceId(ship);
        SyncTankResourceFromRuntime(progress.shipClaudiumTank, claudiumResourceId, ref syncedClaudiumResourceId, ref ship.claudiumStock);
        ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(worldConfig));
        ship.RefreshRuntimeShipSettings();
    }

    private void RefreshShipConsumablesFromTanks(ShipPhysics ship, bool resetRuntimeStock)
    {
        if (ship == null || progress == null) return;

        ApplyFuelConfigToShip(ship);
        string claudiumResourceId = GetClaudiumResourceId(ship);
        progress.shipEngineFuelTank.SetResource(ship.engineFuelId);
        progress.shipClaudiumTank.SetResource(claudiumResourceId);

        if (resetRuntimeStock || syncedFuelResourceId != ship.engineFuelId)
        {
            syncedFuelResourceId = ship.engineFuelId;
            ship.engineFuelStockKg = progress.shipEngineFuelTank.GetAmount(ship.engineFuelId);
        }

        if (resetRuntimeStock || syncedClaudiumResourceId != claudiumResourceId)
        {
            syncedClaudiumResourceId = claudiumResourceId;
            ship.claudiumStock = progress.shipClaudiumTank.GetAmount(claudiumResourceId);
        }
    }

    private void SyncShipConsumablesWithCargo(bool resetPendingConsumption)
    {
        ShipPhysics ship = GetActiveShip();
        if (ship == null || progress == null) return;

        if (resetPendingConsumption)
        {
            RefreshShipConsumablesFromTanks(ship, true);
            ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(worldConfig));
            ship.RefreshRuntimeShipSettings();
            return;
        }

        ApplyFuelConfigToShip(ship);
        SyncTankResourceFromRuntime(progress.shipEngineFuelTank, ship.engineFuelId, ref syncedFuelResourceId, ref ship.engineFuelStockKg);
        string claudiumResourceId = GetClaudiumResourceId(ship);
        SyncTankResourceFromRuntime(progress.shipClaudiumTank, claudiumResourceId, ref syncedClaudiumResourceId, ref ship.claudiumStock);

        ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(worldConfig));
        ship.RefreshRuntimeShipSettings();
    }

    private void SyncTankResourceFromRuntime(ShipConsumableTankState tank, string resourceId, ref string syncedResourceId, ref float runtimeStockKg)
    {
        if (tank == null || string.IsNullOrWhiteSpace(resourceId) || progress == null)
        {
            syncedResourceId = resourceId ?? "";
            runtimeStockKg = 0f;
            return;
        }

        if (syncedResourceId != resourceId)
        {
            syncedResourceId = resourceId;
            tank.SetResource(resourceId);
            runtimeStockKg = tank.GetAmount(resourceId);
            return;
        }

        float storedKg = tank.GetAmount(resourceId);
        float consumedKg = Mathf.Max(0f, storedKg - Mathf.Max(0f, runtimeStockKg));
        if (consumedKg > 0f)
        {
            tank.TrySpend(resourceId, consumedKg);
        }

        runtimeStockKg = tank.GetAmount(resourceId);
    }

    private static string GetClaudiumResourceId(ShipPhysics ship)
    {
        if (ship == null || string.IsNullOrWhiteSpace(ship.claudiumResourceId)) return "claudium";
        return ship.claudiumResourceId;
    }

    private void ResolveCurrentTankResourceIds(out string fuelId, out string claudiumId)
    {
        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            ApplyFuelConfigToShip(ship);
        }

        fuelId = ship != null && !string.IsNullOrWhiteSpace(ship.engineFuelId)
            ? ship.engineFuelId
            : GetStartingEngineFuelId();
        claudiumId = GetClaudiumResourceId(ship);
    }

    private void ApplyFuelConfigToShip(ShipPhysics ship)
    {
        if (ship == null || string.IsNullOrWhiteSpace(ship.engineFuelId)) return;

        EnsureWorldConfigLoaded();
        ItemConfig fuel = worldConfig != null ? worldConfig.GetItem(ship.engineFuelId) : null;
        if (fuel != null)
        {
            ship.engineFuelEnergyKwhPerKg = Mathf.Max(0f, fuel.energyKwhPerKg);
        }
    }

    private void ApplyStartingTechTreeNodes()
    {
        if (techTree == null) return;

        for (int i = 0; i < techTree.nodes.Count; i++)
        {
            TechTreeNode node = techTree.nodes[i];
            if (node == null) continue;

            if (node.startsResearched)
            {
                progress.ResearchNode(node.nodeId);
            }

            if (node.startsPurchased)
            {
                progress.PurchaseNode(node.nodeId);

                if (node.kind == TechTreeNodeKind.Hull)
                {
                    progress.EnsureStarterHull(node.EffectivePartId);
                }
            }
        }
    }

    private void ApplyStartingTechnologies()
    {
        if (worldConfig == null || !worldConfig.isLoaded || progress == null) return;

        for (int i = 0; i < worldConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = worldConfig.technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) continue;
            if (technology.prerequisiteTechnologyIds != null && technology.prerequisiteTechnologyIds.Count > 0) continue;
            if (technology.cycleCost != null && technology.cycleCost.Count > 0) continue;
            if (technology.cycleTimeSeconds > 0) continue;

            progress.CompleteTechnology(technology.id);
            progress.PurchaseNode(technology.id);

            TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
            state.completedCycles = Mathf.Max(state.completedCycles, Mathf.Max(1, technology.requiredCycles));
            state.activeCycleStartUtcTicks = 0;
            state.activeCycleEndUtcTicks = 0;
        }
    }

    private void AddStartingShipConsumables()
    {
        if (progress == null) return;

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (startingFuelKg > 0)
        {
            progress.shipEngineFuelTank.Add(GetStartingEngineFuelId(), startingFuelKg, Mathf.Max(startingFuelKg, capacity.fuelTankCapacityKg));
        }

        if (startingClaudiumKg > 0)
        {
            progress.shipClaudiumTank.Add("claudium", startingClaudiumKg, Mathf.Max(startingClaudiumKg, capacity.claudiumTankCapacityKg));
        }
    }

    private string GetStartingEngineFuelId()
    {
        string fuelId = shipLoader != null && shipLoader.targetShip != null ? shipLoader.targetShip.engineFuelId : "";
        return string.IsNullOrWhiteSpace(fuelId) ? "charcoal" : fuelId;
    }

    private void AddStartingIslandSupplies()
    {
        if (progress == null || startingPaperKg <= 0) return;

        IslandProductionState capitalStorage = progress.GetIslandProductionState(GetCapitalIslandId(), true);
        capitalStorage?.AddResource("paper", startingPaperKg);
    }

    private string GetSelectedExperienceTargetId()
    {
        if (progress == null) return "";
        return progress.selectedHullId;
    }

    private ShipPhysics GetActiveShip()
    {
        if (shipLoader != null)
        {
            if (shipLoader.targetShip == null)
            {
                shipLoader.targetShip = FindFirstObjectByType<ShipPhysics>();
            }

            return shipLoader.targetShip;
        }

        return FindFirstObjectByType<ShipPhysics>();
    }

    private void RefreshSceneShipReferences(ShipPhysics ship)
    {
        if (ship == null) return;

        if (missionController != null)
        {
            missionController.targetShip = ship;
            if (missionController.metaGameState == null)
            {
                missionController.metaGameState = this;
            }
        }

        DockingPort[] dockingPorts = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        for (int i = 0; i < dockingPorts.Length; i++)
        {
            DockingPort dock = dockingPorts[i];
            if (dock == null) continue;

            dock.targetShip = ship;
            if (dock.metaGameState == null)
            {
                dock.metaGameState = this;
            }
        }

        InstallCrashDetectorIfNeeded(ship);
    }

    private void ApplySessionModeToShip()
    {
        ShipPhysics ship = GetActiveShip();
        if (ship == null) return;

        InstallCrashDetectorIfNeeded(ship);
        ResetCrashDetector(ship);

        Rigidbody body = ship.GetComponent<Rigidbody>();
        bool docked = IsDocked;

        ship.routeEnabled = docked ? false : ship.routeEnabled;
        ship.enabled = !docked;
        if (docked)
        {
            ship.thrustInput = 0f;
            ship.turnInput = 0f;
            ship.liftInput = 0f;
            ship.cruiseControl = false;
            ship.altitudeHold = false;
            ship.headingHold = false;
            ship.positionHold = false;
        }

        if (body == null) return;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        if (docked)
        {
            if (progress.hasCurrentDockPosition)
            {
                body.position = progress.currentDockPosition;
                body.transform.position = progress.currentDockPosition;
                Physics.SyncTransforms();
            }

            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.Sleep();
        }
        else
        {
            if (progress.hasCurrentFlightPose)
            {
                body.position = progress.currentFlightPosition;
                body.rotation = progress.currentFlightRotation;
                body.transform.SetPositionAndRotation(progress.currentFlightPosition, progress.currentFlightRotation);
                Physics.SyncTransforms();
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            ship.StabilizeForFlightStart(true);
        }
    }

    private void RememberCurrentDockPosition()
    {
        if (!IsDocked || progress == null) return;

        ShipPhysics ship = GetActiveShip();
        WildWindGameplaySession gameplaySession = FindFirstObjectByType<WildWindGameplaySession>();
        progress.currentDockPosition = ship != null
            ? ship.transform.position
            : gameplaySession != null ? gameplaySession.PlayerPosition : Vector3.zero;
        progress.hasCurrentDockPosition = true;
    }

    private void RememberCurrentFlightPose()
    {
        if (CurrentMode != GameSessionMode.Flight || progress == null) return;

        ShipPhysics ship = GetActiveShip();
        if (ship == null)
        {
            WildWindGameplaySession gameplaySession = FindFirstObjectByType<WildWindGameplaySession>();
            if (gameplaySession == null) return;

            progress.SetFlightPose(gameplaySession.PlayerPosition, gameplaySession.PlayerRotation);
            return;
        }

        progress.SetFlightPose(ship.transform.position, ship.transform.rotation);
    }

    private Vector3 GetCurrentShipPosition()
    {
        ShipPhysics ship = GetActiveShip();
        return ship != null ? ship.transform.position : Vector3.zero;
    }

    private Vector3 GetDockPositionOrFallback(string dockId, DockingLocationKind dockKind)
    {
        DockingPort[] dockingPorts = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        for (int i = 0; i < dockingPorts.Length; i++)
        {
            DockingPort dock = dockingPorts[i];
            if (dock != null && dock.dockId == dockId)
            {
                return dock.DockPosition;
            }
        }

        EnsureWorldConfigLoaded();
        if (dockKind == DockingLocationKind.Island && worldConfig != null)
        {
            IslandConfig island = worldConfig.GetIsland(dockId);
            if (island != null)
            {
                return island.position;
            }
        }

        if (shipLoader != null && shipLoader.spawnPoint != null)
        {
            return shipLoader.spawnPoint.position;
        }

        return Vector3.zero;
    }

    private void InstallCrashDetectorIfNeeded()
    {
        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            InstallCrashDetectorIfNeeded(ship);
        }
    }

    private void InstallCrashDetectorIfNeeded(ShipPhysics ship)
    {
        if (!autoInstallCrashDetector || ship == null) return;

        ShipCrashDetector crashDetector = ship.GetComponent<ShipCrashDetector>();
        if (crashDetector == null)
        {
            crashDetector = ship.gameObject.AddComponent<ShipCrashDetector>();
        }

        crashDetector.metaGameState = this;
    }

    private void ResetCrashDetector()
    {
        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            ResetCrashDetector(ship);
        }
    }

    private static void ResetCrashDetector(ShipPhysics ship)
    {
        ShipCrashDetector crashDetector = ship.GetComponent<ShipCrashDetector>();
        if (crashDetector != null)
        {
            crashDetector.ResetCrashState();
        }
    }

    private void AutoSaveIfDocked()
    {
        if (autoSaveOnDock && IsDocked)
        {
            TrySaveGame();
        }
    }

    private void OnGUI()
    {
        if (!showDockingDebugUI || !Application.isPlaying) return;

        EnsureProgressInitialized();

        Rect area = new Rect(10f, 10f, debugUiWidth, Mathf.Max(220f, Screen.height - 20f));
        GUILayout.BeginArea(area, GUI.skin.box);
        debugScroll = GUILayout.BeginScrollView(debugScroll);

        GUILayout.Label("Мета-игра");
        GUILayout.Label("Режим: " + GetModeName(CurrentMode));
        if (sessionExtractionCoreMode)
        {
            GUILayout.Label("Core: session extraction");
        }

        GUILayout.Label("Док: " + progress.currentDockId + " (" + GetDockKindName(progress.currentDockKind) + ")");
        GUILayout.Label("Деньги: " + progress.money);
        GUILayout.Label("Руда: " + progress.GetResourceAmount("ore") + "  Железо: " + progress.GetResourceAmount("iron"));
        GUILayout.Label("Зерно магазина: " + progress.shopSeed + "  обновление через " + FormatRemaining(progress.nextShopRefreshUtcTicks));
        DrawTimeScaleUi();
        DrawSurveyDebugUi();
        if (!sessionExtractionCoreMode)
        {
            DrawFlagshipExpeditionUi();
        }

        if (!string.IsNullOrWhiteSpace(lastSaveMessage))
        {
            GUILayout.Label(lastSaveMessage);
        }

        GUILayout.Space(8f);

        if (IsDocked)
        {
            DrawDockedDebugUi();
        }
        else
        {
            DrawFlightDebugUi();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawFlagshipExpeditionUi()
    {
        FlagshipInteriorState interior = FlagshipInteriorSimulator.EnsurePlayerFlagshipInterior(worldConfig, progress);
        if (interior == null) return;

        FlagshipNeedState morale = interior.GetNeedState(FlagshipNeedIds.Morale, false);
        string moraleText = morale != null
            ? morale.currentValue.ToString("0") + "/" + morale.maxValue.ToString("0")
            : "-";
        string routeText = interior.expeditionActive ? "экспедиция" : "дома";
        GUILayout.Label("Флагман: " + routeText + "  Мораль: " + moraleText);
        if (!sessionExtractionCoreMode && progress.activeExpedition != null && progress.activeExpedition.active)
        {
            GUILayout.Label("Экспедиция: " + GetExpeditionDisplayName(progress.activeExpedition) + " / " + progress.activeExpedition.regionId);
        }
    }

    private void DrawSurveyDebugUi()
    {
        if (progress == null || progress.scoutedObjects == null) return;

        int knownCoordinates = 0;
        int completedFacts = 0;
        int infoKg = 0;
        for (int i = 0; i < progress.scoutedObjects.Count; i++)
        {
            ScoutedObjectState state = progress.scoutedObjects[i];
            if (state == null) continue;
            if (state.coordinatesKnown) knownCoordinates++;
            if (state.factsComplete) completedFacts++;
            infoKg += Mathf.FloorToInt(state.informationExtractedKg + 0.0001f);
        }

        GUILayout.Label($"Разведка: координаты {knownCoordinates}, изучено {completedFacts}, снято информации {infoKg} кг");

        int shown = 0;
        for (int i = progress.scoutedObjects.Count - 1; i >= 0 && shown < 3; i--)
        {
            ScoutedObjectState state = progress.scoutedObjects[i];
            if (state == null || !state.coordinatesKnown) continue;

            string name = string.IsNullOrWhiteSpace(state.displayName) ? state.objectId : state.displayName;
            string facts = state.factsComplete
                ? "сведения полные"
                : $"{state.factsProgress:F1}/{state.factsRequired:F1}";
            GUILayout.Label($"{GetScoutedKindName(state.kind)} {name}: {facts}, инф. {state.informationExtractedKg:F0}/{state.informationPotentialKg:F0} кг");
            if (state.factsComplete && !string.IsNullOrWhiteSpace(state.summaryRu))
            {
                GUILayout.Label(state.summaryRu);
            }

            shown++;
        }
    }

    private static string GetScoutedKindName(ScoutedObjectKind kind)
    {
        return kind switch
        {
            ScoutedObjectKind.GasCloud => "облако",
            ScoutedObjectKind.MiningRock => "глыба",
            ScoutedObjectKind.Leviathan => "левиафан",
            _ => "объект"
        };
    }

    private void DrawDockedDebugUi()
    {
        GUILayout.Label("Стыковка");
        if (UsesLegacyDockAssemblyUi)
        {
            DrawAssemblyUi();
        }
        if (!sessionExtractionCoreMode)
        {
            DrawCargoTransferUi();
        }

        GUILayout.Space(8f);

        if (GUILayout.Button(new GUIContent("Сохранить у дока", "Сохраняет прогресс только если корабль находится в режиме стыковки.")))
        {
            TrySaveGame();
        }

        if (GUILayout.Button(new GUIContent("Удалить сохранение и начать заново", "Удаляет файл сохранения и сбрасывает текущий мета-прогресс.")))
        {
            DeleteSave();
        }

        if (!sessionExtractionCoreMode)
        {
            if (missionController != null && missionController.mission != null && GUILayout.Button(new GUIContent("Вылететь на миссию", "Переводит игру в режим вылета. Прогресс сохранится после стыковки или аварийного возврата.")))
            {
                missionController.BeginMission();
            }

            if (GUILayout.Button(new GUIContent("Свободный вылет", "Начинает полет без активной миссии. Прогресс сохранится только после следующей стыковки.")))
            {
                BeginFreeFlight();
            }
        }

        DrawSessionExtractionDockedUi();
        if (sessionExtractionCoreMode)
        {
            return;
        }

        GUILayout.Space(6f);
        DrawExpeditionDebugControls();

        GUILayout.Space(8f);
        GUILayout.Label("Работы в реальном времени");

        GUI.enabled = !progress.HasActiveProcess("idle_mining");
        if (GUILayout.Button(new GUIContent("Запустить добычу руды", "Пассивно добавляет руду через заданный интервал реального времени.")))
        {
            StartIdleMining();
        }
        GUI.enabled = true;

        GUI.enabled = progress.GetResourceAmount("ore") >= ironSmeltingOreCost;
        if (GUILayout.Button(new GUIContent("Переплавить руду в железо", "Тратит руду и через таймер добавляет железо.")))
        {
            StartIronSmelting();
        }
        GUI.enabled = true;

        if (missionController != null && missionController.mission != null)
        {
            GUI.enabled = missionController.mission.canRunAsTimedMission;
            if (GUILayout.Button(new GUIContent("Отправить команду на миссию", "Миссия выполнится таймером без вылета корабля.")))
            {
                StartTimedMission(missionController.mission);
            }
            GUI.enabled = true;
        }

        DrawProcessList();
        DrawIslandProductionList();
    }

    private void DrawSessionExtractionDockedUi()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Session extraction");

        bool canStart = IsDockedAtCapital();
        SortieZoneDefinition selectedSortie = GetSelectedSessionSortieDefinition();
        GUILayout.Label("Selected sortie: " + (selectedSortie != null ? selectedSortie.displayName : "-"));
        GUILayout.Label("Fitting: " + GetSortieRequirementText(selectedSortie));
        bool canBeginSelected = CanBeginSessionExtractionSortie(selectedSortie, out string sortieBlockReason);
        GUI.enabled = canStart;
        if (GUILayout.Button(new GUIContent("Next sortie", "Cycles through starter extraction zones: ore, gas, automatons, leviathans, and survey data.")))
        {
            SelectNextSessionSortie(out _);
        }

        GUI.enabled = canStart && canBeginSelected;
        if (GUILayout.Button(new GUIContent("Start selected sortie", "Launches the selected extraction session: bounded cylinder, resource cache, manual Extract home.")))
        {
            BeginSelectedSessionSortie();
        }

        GUI.enabled = true;
        if (!canStart)
        {
            GUILayout.Label("Safe sorties start only from the base.");
        }
        else if (!canBeginSelected)
        {
            GUILayout.Label(sortieBlockReason);
        }

        GUILayout.Label("Fitting bands: High work modules, Mid support, Low hull upgrades, Rig passive modifiers.");
        GUILayout.Label("Slots: " + GetCoreFittingSummaryText());
        DrawBaseExtractionIndustryUi();
    }

    private void DrawBaseExtractionIndustryUi()
    {
        if (!IsDockedAtCapital())
        {
            return;
        }

        EnsureWorldConfigLoaded();
        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        IslandProductionState storage = GetCapitalStorageState();

        GUILayout.Space(6f);
        GUILayout.Label("Base processing branches");
        GUILayout.Label(GetBaseProcessingOverviewText());
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingBranch branch = SessionExtractionIndustry.ProcessingBranches[i];
            BaseProcessingLineState line = progress.baseIndustry.GetProcessing(branch);
            GUILayout.Label(SessionExtractionIndustry.GetProcessingDisplayName(branch)
                + ": cap " + line.capacityUnitsPerMinute.ToString("F0")
                + "/min, done " + line.totalProcessedUnits.ToString("F0"));
        }

        bool anyProcessable = false;
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingBranch branch = SessionExtractionIndustry.ProcessingBranches[i];
            bool canProcess = TryGetAvailableBaseProcessingInput(branch, storage, out string inputItemId, out int availableInput);
            anyProcessable |= canProcess;
            GUI.enabled = canProcess;
            if (GUILayout.Button(new GUIContent(
                "Process " + SessionExtractionIndustry.GetProcessingDisplayName(branch),
                "Consumes " + (string.IsNullOrWhiteSpace(inputItemId) ? "branch input" : inputItemId)
                    + " from base storage. Available: " + availableInput + ".")))
            {
                TryProcessBaseBatch(branch, out _);
            }
        }

        GUI.enabled = true;
        if (!anyProcessable)
        {
            GUILayout.Label("Processing waits for extracted sortie resources in base storage.");
        }

        GUILayout.Space(4f);
        GUILayout.Label("Base upgrades");
        GUILayout.Label(GetNextBaseIndustryUpgradeOverviewText());
        bool canUpgradeNext = CanUpgradeNextBaseIndustryLine(out string upgradeLineMessage);
        GUI.enabled = canUpgradeNext;
        if (GUILayout.Button(new GUIContent("Upgrade next base line", "Spends base materials to increase the next processing or cascade line level and capacity.")))
        {
            TryUpgradeNextBaseIndustryLine(out _);
        }

        GUI.enabled = true;
        if (!canUpgradeNext)
        {
            GUILayout.Label(upgradeLineMessage);
        }

        GUILayout.Space(4f);
        GUILayout.Label("Cascade production");
        GUILayout.Label(GetBaseCascadeProductionOverviewText());
        GUILayout.Label(GetNextBaseCascadeOrderOverviewText());
        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            CascadeProductionType type = SessionExtractionIndustry.CascadeProductionTypes[i];
            CascadeProductionLineState line = progress.baseIndustry.GetProduction(type);
            GUILayout.Label(SessionExtractionIndustry.GetProductionDisplayName(type)
                + ": cap " + line.capacityUnitsPerMinute.ToString("F0")
                + "/min, load " + line.totalLoadApplied.ToString("F0"));
        }

        List<CascadeProductionOrderDefinition> starterOrders = CreateStarterCascadeOrders();
        bool anyRunnableOrder = false;
        for (int i = 0; i < starterOrders.Count; i++)
        {
            CascadeProductionOrderDefinition order = starterOrders[i];
            if (order == null) continue;

            CascadeProductionEstimate estimate = EstimateBaseCascadeOrder(order);
            anyRunnableOrder |= estimate.canRun;
            GUI.enabled = estimate.canRun;
            if (GUILayout.Button(new GUIContent(
                "Run " + order.displayName,
                estimate.canRun
                    ? "Bottleneck: " + SessionExtractionIndustry.GetProductionDisplayName(estimate.bottleneck)
                        + " ~" + estimate.bottleneckMinutes.ToString("F1") + " min."
                    : estimate.blockedReason)))
            {
                TryRunBaseCascadeOrder(order, out _);
            }
        }

        GUI.enabled = true;
        if (!anyRunnableOrder)
        {
            CascadeProductionEstimate nextEstimate = EstimateNextBaseCascadeOrder(out CascadeProductionOrderDefinition nextOrder);
            GUILayout.Label(nextOrder != null
                ? nextOrder.displayName + ": " + nextEstimate.blockedReason
                : "No starter cascade orders are configured.");
        }

        bool canInstallUpgrade = CanInstallNextStarterFittingUpgrade(out string upgradeMessage);
        GUI.enabled = canInstallUpgrade;
        if (GUILayout.Button(new GUIContent(GetNextStarterFittingUpgradeActionLabel(), "Consumes a starter kit and installs the next core fitting upgrade.")))
        {
            TryInstallNextStarterFittingUpgrade(out _);
        }

        GUI.enabled = true;
        if (!canInstallUpgrade)
        {
            GUILayout.Label(upgradeMessage);
        }

        bool canLoadStarterMunitions = CanLoadStarterMunitionsAtBase(out string munitionLoadMessage);
        GUI.enabled = canLoadStarterMunitions;
        if (GUILayout.Button(new GUIContent("Load starter munitions", "Consumes a munition bundle at the base and loads weapon cargo for sortie weapons and harpoons.")))
        {
            TryLoadStarterMunitionsAtBase(out _);
        }

        GUI.enabled = true;
        if (!canLoadStarterMunitions)
        {
            GUILayout.Label(munitionLoadMessage);
        }
    }

    private void DrawSessionExtractionFlightUi()
    {
        if (!HasActiveSortie)
        {
            return;
        }

        SortieReturnEstimate estimate = GetActiveSortieReturnEstimate();
        SortieSessionState sortie = ActiveSortie;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;

        GUILayout.Space(8f);
        GUILayout.Label("Session extraction");
        if (zone != null)
        {
            GUILayout.Label($"{zone.displayName}: radius {zone.radiusMeters:F0} m, base {zone.distanceToBaseKm:F0} km away");
        }

        GUILayout.Label($"Boundary: {estimate.distanceToBoundaryMeters:F0} m, return {FormatDurationSeconds(estimate.returnTimeSeconds)}");
        GUILayout.Label($"Need: coal {estimate.requiredCoalKg:F0} kg, claudium {estimate.requiredClaudiumKg:F0} kg");
        GUILayout.Label($"Have: coal {estimate.currentCoalKg:F1} kg, claudium {estimate.currentClaudiumKg:F1} kg");
        GUILayout.Label(estimate.status);
        if (!string.IsNullOrWhiteSpace(activeSortieExtractionRunupStatus))
        {
            GUILayout.Label(activeSortieExtractionRunupStatus);
        }

        GUI.enabled = estimate.canExtract;
        if (GUILayout.Button(new GUIContent("Extract home", "Consumes return coal and claudium, transfers sortie cargo to base, and docks at home.")))
        {
            TryExtractActiveSortie(out _);
        }

        GUI.enabled = true;
    }

    private void DrawExpeditionDebugControls()
    {
        IReadOnlyList<FlagshipExpeditionDefinition> expeditions = GetFlagshipExpeditionConfigs();
        if (expeditions == null || expeditions.Count == 0)
        {
            GUILayout.Label("Экспедиции не загружены из Expedition.csv.");
            return;
        }

        GUILayout.Label("Экспедиции");
        for (int i = 0; i < expeditions.Count; i++)
        {
            FlagshipExpeditionDefinition expedition = expeditions[i];
            if (expedition == null) continue;

            bool canBegin = CanBeginFlagshipExpedition(expedition, out string reason);
            string label = GetExpeditionDisplayName(expedition) +
                "  R" + expedition.minimumFlagshipRank + "+  x" + expedition.moraleDrainMultiplier.ToString("0.##");

            GUI.enabled = canBegin;
            if (GUILayout.Button(new GUIContent(label, string.IsNullOrWhiteSpace(expedition.summaryRu) ? "Стартует экспедицию флагмана." : expedition.summaryRu)))
            {
                BeginFlagshipExpedition(expedition.expeditionId);
            }
            GUI.enabled = true;

            if (!canBegin && i == 0)
            {
                GUILayout.Label(reason);
            }
        }
    }

    private void DrawFlightDebugUi()
    {
        GUILayout.Label("Вылет");
        GUILayout.Label("Ручное сохранение у дока. При выходе из игры текущий вылет сохранится.");

        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            DrawRouteEtaFlightInfo(ship);
            GUILayout.Label($"Топливный бак: {progress.shipEngineFuelTank.GetAmount(ship.engineFuelId):F1} кг ({ship.engineFuelStockKg:F1} доступно)");
            string claudiumResourceId = GetClaudiumResourceId(ship);
            GUILayout.Label($"Клавдиевый бак: {progress.shipClaudiumTank.GetAmount(claudiumResourceId):F1} кг ({ship.claudiumStock:F1} доступно)");

            bool requestedPositionHold = GUILayout.Toggle(
                ship.positionHold,
                new GUIContent("Удерживать на месте", "Корабль запоминает текущую позицию и тягой сопротивляется ветру или дрейфу."));
            if (requestedPositionHold != ship.positionHold)
            {
                ship.positionHold = requestedPositionHold;
                if (requestedPositionHold)
                {
                    ship.targetHoldPosition = ship.transform.position;
                }
            }

            if (ship.positionHold)
            {
                GUILayout.Label($"Цель удержания: X {ship.targetHoldPosition.x:F1}  Z {ship.targetHoldPosition.z:F1}");
                if (GUILayout.Button(new GUIContent("Запомнить текущую позицию", "Переносит точку удержания туда, где корабль находится сейчас.")))
                {
                    ship.targetHoldPosition = ship.transform.position;
                }
            }
        }

        DrawSessionExtractionFlightUi();

        if (!sessionExtractionCoreMode && progress.activeExpedition != null && progress.activeExpedition.active)
        {
            GUILayout.Label("Активная экспедиция: " + GetExpeditionDisplayName(progress.activeExpedition));
            if (GUILayout.Button(new GUIContent("Вернуться из экспедиции", "Завершает экспедицию, возвращает флагман в столицу и восстанавливает мораль.")))
            {
                ReturnFromFlagshipExpedition();
            }
        }

        if (!sessionExtractionCoreMode)
        {
            DockingPort nearbyDock = FindAvailableDockingPort(ship);
        if (nearbyDock != null)
        {
            GUILayout.Label("Док в радиусе: " + nearbyDock.displayName);
            if (GUILayout.Button(new GUIContent("Стыковка", "Завершает вылет у выбранного дока. Охота и гарпун будут остановлены.")))
            {
                if (ship != null)
                {
                    ship.transform.position = nearbyDock.DockPosition;
                }

                DockAt(nearbyDock.dockId, nearbyDock.kind);
            }
        }
        else
        {
            GUILayout.Label("Стыковка недоступна: нет дока в радиусе.");
            GUI.enabled = false;
            GUILayout.Button(new GUIContent("Стыковка", "Подлети в радиус стыковки острова или корабля."));
            GUI.enabled = true;
        }

        }

        if (GUILayout.Button(new GUIContent("Потерять корабль", "Завершает вылет аварией: груз и текущая сборка теряются, игрок возвращается в город на стартовом корабле.")))
        {
            if (HasActiveSortie)
            {
                LoseActiveSortieShipAndReturnToBase("Manual sortie loss");
            }
            else
            {
                LoseShipAndReturnToCity("Ручной аварийный возврат");
            }
        }
    }

    private DockingPort FindAvailableDockingPort(ShipPhysics ship)
    {
        if (ship == null) return null;

        DockingPort[] dockingPorts = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        DockingPort best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < dockingPorts.Length; i++)
        {
            DockingPort dock = dockingPorts[i];
            if (dock == null || !dock.canEndSession || !dock.Contains(ship.transform.position)) continue;

            float distance = Vector3.Distance(ship.transform.position, dock.DockPosition);
            if (distance >= bestDistance) continue;

            best = dock;
            bestDistance = distance;
        }

        return best;
    }

    private void DrawRouteEtaFlightInfo(ShipPhysics ship)
    {
        if (ship == null || ship.waypoints == null || ship.waypoints.Count == 0) return;

        RouteEtaInfo eta = ship.GetCurrentRouteEta();
        GUILayout.Space(4f);
        GUILayout.Label("Путевая машина");

        if (!eta.hasTarget)
        {
            GUILayout.Label(eta.status);
            return;
        }

        string etaText = eta.canEstimate ? FormatDurationSeconds(eta.etaSeconds) : "нет оценки";
        GUILayout.Label($"Точка {eta.waypointIndex + 1}/{eta.waypointCount}: ETA {etaText}");
        GUILayout.Label($"До радиуса: {eta.horizontalRemaining:F0} м, высота {eta.verticalRemaining:F0} м");
        GUILayout.Label($"Скорость к точке: {eta.horizontalClosingSpeed:F1} м/с, вертикально {eta.verticalClosingSpeed:F1} м/с");

        if (!eta.routeEnabled || !eta.canEstimate || eta.etaSeconds <= 0f)
        {
            GUILayout.Label(eta.status);
        }
    }

    private void DrawCargoTransferUi()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Груз и погрузка");

        EnsureWorldConfigLoaded();
        CargoCapacityInfo capacity = CalculateCargoCapacity();
        GUILayout.Label($"Взлетная масса: {capacity.allowedTakeoffMassKg:F0} кг  Сухая: {capacity.emptyMassKg:F0} кг");
        GUILayout.Label($"Полезная масса: {(capacity.currentCargoKg + capacity.currentTankKg):F0}/{capacity.maxCargoKg:F0} кг  Груз: {capacity.currentCargoKg:F0} кг  Баки: {capacity.currentTankKg:F0} кг");
        GUILayout.Label($"Ограничения: двигатель+контур {capacity.engineLiftKg:F0} кг, контур {capacity.claudiumMaxLiftKg:F0} кг, корпус {capacity.hullLimitKg:F0} кг");

        if (!capacity.canFly)
        {
            GUILayout.Label("Вылет заблокирован: " + capacity.reason);
        }

        if (progress.cargoTransfer != null && progress.cargoTransfer.active)
        {
            int remaining = progress.cargoTransfer.GetRemainingUnits();
            GUILayout.Label($"Идет погрузка: осталось {remaining} ед., следующая операция через {FormatRemaining(progress.cargoTransfer.nextOperationUtcTicks)}");
            return;
        }

        if (worldConfig == null || !worldConfig.isLoaded)
        {
            GUILayout.Label("Конфиги мира не загружены.");
            return;
        }

        if (progress.currentDockKind != DockingLocationKind.Island)
        {
            GUILayout.Label("Погрузка доступна только на острове.");
            return;
        }

        IslandConfig island = worldConfig.GetIsland(progress.currentDockId);
        if (island == null)
        {
            GUILayout.Label("Для текущего дока нет острова в Island.csv.");
            return;
        }

        IslandProductionState storage = progress.GetIslandProductionState(island.id, true);
        DrawShipTankRefuelUi(storage, capacity);
        EnsureCargoPlan(island.id);

        Dictionary<string, int> plannedCargo = GetPlannedCargoMap();
        float plannedCargoMass = CargoStoragePlanner.GetCargoMassKg(worldConfig, plannedCargo);
        int operationCount = GetCargoPlanOperationCount();
        bool overload = plannedCargoMass + capacity.currentTankKg > capacity.maxCargoKg + 0.001f;
        bool storageOk = CargoStoragePlanner.TryValidateCargoStorage(worldConfig, capacity.cargoCompartments, plannedCargo, out string storageError);

        GUILayout.Label($"План: груз {plannedCargoMass:F0} кг, полезная масса {(plannedCargoMass + capacity.currentTankKg):F0}/{capacity.maxCargoKg:F0} кг, операций: {operationCount}, время: {operationCount * island.timeForOneItemLoadSeconds:F1} сек");
        if (overload)
        {
            GUILayout.Label("План перегружает корабль.");
        }
        if (!storageOk)
        {
            GUILayout.Label("План не помещается в отсеки: " + storageError);
        }

        for (int i = 0; i < worldConfig.items.Count; i++)
        {
            ItemConfig item = worldConfig.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
            DrawCargoPlanRow(item, storage);
        }

        GUI.enabled = operationCount > 0 && !overload && storageOk && capacity.emptyMassKg <= capacity.allowedTakeoffMassKg + 0.001f;
        if (GUILayout.Button(new GUIContent("Подтвердить погрузку", "Сначала выполняется выгрузка с борта на склад, затем загрузка со склада на борт. Каждая единица товара занимает время из Island.csv.")))
        {
            StartCargoTransfer(island, storage);
        }
        GUI.enabled = true;
    }

    private void DrawShipTankRefuelUi(IslandProductionState storage, CargoCapacityInfo capacity)
    {
        ShipPhysics ship = GetActiveShip();
        if (ship == null || storage == null || progress == null) return;

        string fuelId = string.IsNullOrWhiteSpace(ship.engineFuelId) ? GetStartingEngineFuelId() : ship.engineFuelId;
        string claudiumId = GetClaudiumResourceId(ship);
        GUILayout.Space(4f);
        GUILayout.Label($"Баки: {worldConfig.GetItemNameRu(fuelId)} {progress.shipEngineFuelTank.GetAmount(fuelId):F1}/{capacity.fuelTankCapacityKg:F0} кг, {worldConfig.GetItemNameRu(claudiumId)} {progress.shipClaudiumTank.GetAmount(claudiumId):F1}/{capacity.claudiumTankCapacityKg:F0} кг");

        GUILayout.BeginHorizontal();
        GUI.enabled = storage.GetResourceAmount(fuelId) > 0 && progress.shipEngineFuelTank.GetAmount(fuelId) < capacity.fuelTankCapacityKg - 0.001f;
        if (GUILayout.Button(new GUIContent("Заправить топливо", "Пополняет внутренний топливный бак со склада острова. Груз в трюме не тратится автоматически.")))
        {
            int moved = RefillTankFromStorage(progress.shipEngineFuelTank, fuelId, capacity.fuelTankCapacityKg, storage);
            if (moved > 0)
            {
                SyncShipConsumablesWithCargo(true);
                AutoSaveIfDocked();
            }
        }

        GUI.enabled = storage.GetResourceAmount(claudiumId) > 0 && progress.shipClaudiumTank.GetAmount(claudiumId) < capacity.claudiumTankCapacityKg - 0.001f;
        if (GUILayout.Button(new GUIContent("Заправить клавдий", "Пополняет внутренний клавдиевый бак со склада острова.")))
        {
            int moved = RefillTankFromStorage(progress.shipClaudiumTank, claudiumId, capacity.claudiumTankCapacityKg, storage);
            if (moved > 0)
            {
                SyncShipConsumablesWithCargo(true);
                AutoSaveIfDocked();
            }
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private static int RefillTankFromStorage(ShipConsumableTankState tank, string resourceId, float capacityKg, IslandProductionState storage)
    {
        if (tank == null || storage == null || string.IsNullOrWhiteSpace(resourceId) || capacityKg <= 0f) return 0;

        float current = tank.GetAmount(resourceId);
        int needed = Mathf.FloorToInt(Mathf.Max(0f, capacityKg - current) + 0.0001f);
        if (needed <= 0) return 0;

        int moved = Mathf.Min(needed, storage.GetResourceAmount(resourceId));
        if (moved <= 0 || !storage.TrySpendResource(resourceId, moved)) return 0;

        return Mathf.RoundToInt(tank.Add(resourceId, moved, capacityKg));
    }

    private void DrawCargoPlanRow(ItemConfig item, IslandProductionState storage)
    {
        int shipAmount = progress.GetShipCargoAmount(item.id);
        int storageAmount = storage != null ? storage.GetResourceAmount(item.id) : 0;
        CargoPlanEntry entry = GetCargoPlanEntry(item.id, shipAmount);
        entry.targetShipAmount = Mathf.Clamp(entry.targetShipAmount, 0, shipAmount + storageAmount);

        string itemName = worldConfig.GetItemNameRu(item.id);
        string unitLabel = GetCargoAmountUnitLabel(item);
        GUILayout.Label($"{itemName}: склад {storageAmount} {unitLabel}, борт {shipAmount} {unitLabel}, цель {entry.targetShipAmount} {unitLabel}");

        GUILayout.BeginHorizontal();
        GUI.enabled = entry.targetShipAmount > 0;
        if (GUILayout.Button("-10")) entry.targetShipAmount = Mathf.Max(0, entry.targetShipAmount - 10);
        if (GUILayout.Button("-1")) entry.targetShipAmount = Mathf.Max(0, entry.targetShipAmount - 1);
        GUI.enabled = entry.targetShipAmount < shipAmount + storageAmount;
        if (GUILayout.Button("+1")) entry.targetShipAmount = Mathf.Min(shipAmount + storageAmount, entry.targetShipAmount + 1);
        if (GUILayout.Button("+10")) entry.targetShipAmount = Mathf.Min(shipAmount + storageAmount, entry.targetShipAmount + 10);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = shipAmount > 0;
        if (GUILayout.Button(new GUIContent("Унич. 1 борт", "Мгновенно уничтожает 1 единицу этого товара на корабле.")))
        {
            progress.TrySpendShipCargo(item.id, 1);
            ApplyCargoMassToShip(GetActiveShip());
            ResetCargoPlan();
            AutoSaveIfDocked();
        }

        if (GUILayout.Button(new GUIContent("Унич. борт", "Мгновенно уничтожает весь этот товар на корабле.")))
        {
            progress.TrySpendShipCargo(item.id, shipAmount);
            ApplyCargoMassToShip(GetActiveShip());
            ResetCargoPlan();
            AutoSaveIfDocked();
        }

        GUI.enabled = storageAmount > 0;
        if (GUILayout.Button(new GUIContent("Унич. 1 склад", "Мгновенно уничтожает 1 единицу этого товара на складе острова.")))
        {
            storage.TrySpendResource(item.id, 1);
            ResetCargoPlan();
            AutoSaveIfDocked();
        }

        if (GUILayout.Button(new GUIContent("Унич. склад", "Мгновенно уничтожает весь этот товар на складе острова.")))
        {
            storage.TrySpendResource(item.id, storageAmount);
            ResetCargoPlan();
            AutoSaveIfDocked();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Уничтожить", GUILayout.Width(110f));
        entry.destroyAmountText = GUILayout.TextField(entry.destroyAmountText ?? "1", GUILayout.Width(60f));
        int destroyAmount = ParseCargoAmount(entry.destroyAmountText, Mathf.Max(shipAmount, storageAmount));

        GUI.enabled = destroyAmount > 0 && shipAmount >= destroyAmount;
        if (GUILayout.Button(new GUIContent("с борта", "Мгновенно уничтожает указанное количество товара на корабле.")))
        {
            progress.TrySpendShipCargo(item.id, destroyAmount);
            ApplyCargoMassToShip(GetActiveShip());
            ResetCargoPlan();
            AutoSaveIfDocked();
        }

        GUI.enabled = destroyAmount > 0 && storageAmount >= destroyAmount;
        if (GUILayout.Button(new GUIContent("со склада", "Мгновенно уничтожает указанное количество товара на складе острова.")))
        {
            storage.TrySpendResource(item.id, destroyAmount);
            ResetCargoPlan();
            AutoSaveIfDocked();
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private static string GetCargoAmountUnitLabel(ItemConfig item)
    {
        if (item == null) return "ед.";

        switch (item.cargoStorageKind)
        {
            case CargoStorageKind.Cabin:
                return "мест";
            case CargoStorageKind.ShipDock:
                return "шт.";
            default:
                return "шт.";
        }
    }

    private bool TryResolveCurrentIslandStorage(out IslandConfig island, out IslandProductionState storage, out string message)
    {
        island = null;
        storage = null;
        message = "";

        EnsureProgressInitialized();
        if (!IsDocked || progress.currentDockKind != DockingLocationKind.Island)
        {
            message = "Сначала нужна стыковка с островом.";
            return false;
        }

        island = worldConfig.GetIsland(progress.currentDockId);
        if (island == null)
        {
            message = "Текущий остров не найден: " + progress.currentDockId + ".";
            return false;
        }

        storage = progress.GetIslandProductionState(island.id, true);
        if (storage == null)
        {
            message = "Склад острова не найден: " + island.id + ".";
            return false;
        }

        return true;
    }

    private Dictionary<string, int> BuildShipCargoMap(string itemId, int deltaAmount)
    {
        Dictionary<string, int> cargo = new Dictionary<string, int>();
        if (progress == null)
        {
            return cargo;
        }

        progress.shipCargo ??= new List<ResourceStack>();
        for (int i = 0; i < progress.shipCargo.Count; i++)
        {
            ResourceStack stack = progress.shipCargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
            {
                continue;
            }

            cargo[stack.resourceId] = stack.amount;
        }

        if (!string.IsNullOrWhiteSpace(itemId) && deltaAmount != 0)
        {
            cargo.TryGetValue(itemId, out int current);
            int next = Mathf.Max(0, current + deltaAmount);
            if (next > 0)
            {
                cargo[itemId] = next;
            }
            else
            {
                cargo.Remove(itemId);
            }
        }

        return cargo;
    }

    private void EnsureCargoPlan(string islandId)
    {
        if (cargoPlanDockId == islandId && cargoPlan.Count > 0) return;

        cargoPlanDockId = islandId ?? "";
        cargoPlan.Clear();

        if (worldConfig == null || worldConfig.items == null) return;
        for (int i = 0; i < worldConfig.items.Count; i++)
        {
            ItemConfig item = worldConfig.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
            cargoPlan.Add(new CargoPlanEntry
            {
                itemId = item.id,
                targetShipAmount = progress.GetShipCargoAmount(item.id)
            });
        }
    }

    private CargoPlanEntry GetCargoPlanEntry(string itemId, int fallbackTarget)
    {
        for (int i = 0; i < cargoPlan.Count; i++)
        {
            CargoPlanEntry entry = cargoPlan[i];
            if (entry != null && entry.itemId == itemId)
            {
                return entry;
            }
        }

        CargoPlanEntry newEntry = new CargoPlanEntry { itemId = itemId, targetShipAmount = fallbackTarget };
        cargoPlan.Add(newEntry);
        return newEntry;
    }

    private void ResetCargoPlan()
    {
        cargoPlanDockId = "";
        cargoPlan.Clear();
    }

    private static int ParseCargoAmount(string text, int max)
    {
        if (!int.TryParse(text, out int amount)) return 0;
        return Mathf.Clamp(amount, 0, Mathf.Max(0, max));
    }

    private Dictionary<string, int> GetPlannedCargoMap()
    {
        Dictionary<string, int> map = new Dictionary<string, int>();
        for (int i = 0; i < cargoPlan.Count; i++)
        {
            CargoPlanEntry entry = cargoPlan[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) || entry.targetShipAmount <= 0) continue;
            map[entry.itemId] = entry.targetShipAmount;
        }

        return map;
    }

    private float GetPlannedCargoMassKg()
    {
        return CargoStoragePlanner.GetCargoMassKg(worldConfig, GetPlannedCargoMap());
    }

    private int GetCargoPlanOperationCount()
    {
        int total = 0;
        for (int i = 0; i < cargoPlan.Count; i++)
        {
            CargoPlanEntry entry = cargoPlan[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId)) continue;
            total += Mathf.Abs(entry.targetShipAmount - progress.GetShipCargoAmount(entry.itemId));
        }

        return total;
    }

    private void StartCargoTransfer(IslandConfig island, IslandProductionState storage)
    {
        if (island == null || storage == null || progress == null) return;
        if (sessionExtractionCoreMode)
        {
            lastSaveMessage = "Legacy cargo transfer is disabled in session extraction core.";
            return;
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (GetPlannedCargoMassKg() + capacity.currentTankKg > capacity.maxCargoKg + 0.001f)
        {
            lastSaveMessage = "Нельзя начать погрузку: план перегружает корабль.";
            return;
        }

        if (!CargoStoragePlanner.TryValidateCargoStorage(worldConfig, capacity.cargoCompartments, GetPlannedCargoMap(), out string storageError))
        {
            lastSaveMessage = "Нельзя начать погрузку: " + storageError;
            return;
        }

        List<CargoTransferOperation> operations = new List<CargoTransferOperation>();
        for (int i = 0; i < worldConfig.items.Count; i++)
        {
            ItemConfig item = worldConfig.items[i];
            if (item == null) continue;

            CargoPlanEntry entry = GetCargoPlanEntry(item.id, progress.GetShipCargoAmount(item.id));
            int current = progress.GetShipCargoAmount(item.id);
            if (entry.targetShipAmount < current)
            {
                operations.Add(new CargoTransferOperation
                {
                    itemId = item.id,
                    loadToShip = false,
                    remainingAmount = current - entry.targetShipAmount
                });
            }
        }

        for (int i = 0; i < worldConfig.items.Count; i++)
        {
            ItemConfig item = worldConfig.items[i];
            if (item == null) continue;

            CargoPlanEntry entry = GetCargoPlanEntry(item.id, progress.GetShipCargoAmount(item.id));
            int current = progress.GetShipCargoAmount(item.id);
            if (entry.targetShipAmount > current)
            {
                int loadAmount = entry.targetShipAmount - current;
                if (storage.GetResourceAmount(item.id) < loadAmount)
                {
                    lastSaveMessage = "Нельзя начать погрузку: на складе не хватает " + worldConfig.GetItemNameRu(item.id) + ".";
                    return;
                }

                operations.Add(new CargoTransferOperation
                {
                    itemId = item.id,
                    loadToShip = true,
                    remainingAmount = loadAmount
                });
            }
        }

        if (operations.Count == 0) return;

        progress.cargoTransfer ??= new CargoTransferState();
        progress.cargoTransfer.active = true;
        progress.cargoTransfer.islandId = island.id;
        progress.cargoTransfer.startedUtcTicks = GetProcessUtcNow().Ticks;
        progress.cargoTransfer.secondsPerItem = Mathf.Max(0.01f, island.timeForOneItemLoadSeconds);
        progress.cargoTransfer.nextOperationUtcTicks = progress.cargoTransfer.startedUtcTicks + TimeSpan.FromSeconds(progress.cargoTransfer.secondsPerItem).Ticks;
        progress.cargoTransfer.currentOperationIndex = 0;
        progress.cargoTransfer.operations = operations;
        ResetCargoPlan();
        AutoSaveIfDocked();
        lastSaveMessage = "Погрузка начата.";
    }

    private void DrawProcessList()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Активные процессы");

        if (progress.activeProcesses.Count == 0)
        {
            GUILayout.Label("Нет");
            return;
        }

        for (int i = 0; i < progress.activeProcesses.Count; i++)
        {
            TimedProcessState process = progress.activeProcesses[i];
            if (process == null) continue;

            string repeat = process.repeat ? " (повторяется)" : "";
            GUILayout.Label(process.displayName + repeat + " - " + FormatRemaining(process.nextCompletionUtcTicks));
        }
    }

    private void DrawIslandProductionList()
    {
        GUILayout.Space(8f);
        GUILayout.Label("Производство текущего острова");

        EnsureWorldConfigLoaded();
        if (GUILayout.Button(new GUIContent("Перезагрузить конфиги мира", "Повторно читает CSV из Assets/Data/Config без перезапуска Play Mode.")))
        {
            ReloadWorldConfigs();
            SpawnConfiguredWorldActors(true);
        }

        if (worldConfig == null || !worldConfig.isLoaded)
        {
            GUILayout.Label(string.IsNullOrWhiteSpace(worldConfig?.lastError) ? "Конфиги мира не загружены." : worldConfig.lastError);
            return;
        }

        if (progress.currentDockKind != DockingLocationKind.Island)
        {
            GUILayout.Label("Сейчас док не является островом.");
            return;
        }

        IslandConfig island = worldConfig.GetIsland(progress.currentDockId);
        if (island == null)
        {
            GUILayout.Label("Текущий док не найден в Island.csv: " + progress.currentDockId);
            return;
        }

        IslandProductionConfig production = worldConfig.GetProduction(island.productionId);
        IslandProductionState state = progress.GetIslandProductionState(island.id, true);
        if (state == null)
        {
            GUILayout.Label(GetIslandDisplayName(island) + ": склад не найден.");
            return;
        }

        GUILayout.Label(GetIslandDisplayName(island));
        GUILayout.Label($"Коорд.: X {island.position.x:F0}  Y {island.position.y:F0}  Z {island.position.z:F0}");
        GUILayout.Label($"Док: {island.dockingRadius:F0} м, погрузка {island.timeForOneItemLoadSeconds:F1} сек/кг");

        if (production != null)
        {
            string producedName = worldConfig.GetItemNameRu(production.productionItemId);
            int producedStored = state.GetResourceAmount(production.productionItemId);
            float multiplier = GetCurrentIslandProductionMultiplier(state, production);
            float currentRate = production.productionCountBasePerMinute * multiplier;

            GUILayout.Label($"Базовая генерация: {producedName}");
            GUILayout.Label($"Склад: {producedStored} кг");
            GUILayout.Label($"База: {production.productionCountBasePerMinute:F2} кг/мин");
            GUILayout.Label($"Бонусы: x{multiplier:F2}, сейчас {currentRate:F2} кг/мин");
            DrawCurrentIslandConsumption(state, production);
        }
        else
        {
            GUILayout.Label("Базовая генерация: не задана.");
        }

        DrawIslandStorageDebugControls(state);
        DrawCurrentIslandIndustries(island, state);
    }

    private void DrawIslandStorageDebugControls(IslandProductionState state)
    {
        if (state == null) return;

        GUILayout.Space(6f);
        GUILayout.Label("Отладка склада");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Ресурс", GUILayout.Width(60f));
        productionDebugResourceId = GUILayout.TextField(productionDebugResourceId ?? "", GUILayout.Width(140f));
        GUILayout.Label("кг", GUILayout.Width(24f));
        string amountText = GUILayout.TextField(Mathf.Max(0, productionDebugAmount).ToString(), GUILayout.Width(60f));
        if (int.TryParse(amountText, out int parsedAmount))
        {
            productionDebugAmount = Mathf.Max(0, parsedAmount);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = !string.IsNullOrWhiteSpace(productionDebugResourceId) && productionDebugAmount > 0;
        if (GUILayout.Button("Добавить"))
        {
            state.AddResource(productionDebugResourceId, productionDebugAmount);
            lastSaveMessage = "На склад добавлено: " + productionDebugResourceId + " x" + productionDebugAmount + ".";
        }

        GUI.enabled = !string.IsNullOrWhiteSpace(productionDebugResourceId) && productionDebugAmount > 0 && state.GetResourceAmount(productionDebugResourceId) >= productionDebugAmount;
        if (GUILayout.Button("Списать"))
        {
            state.TrySpendResource(productionDebugResourceId, productionDebugAmount);
            lastSaveMessage = "Со склада списано: " + productionDebugResourceId + " x" + productionDebugAmount + ".";
        }

        GUI.enabled = !string.IsNullOrWhiteSpace(productionDebugResourceId);
        if (GUILayout.Button("Обнулить"))
        {
            state.SetResourceAmount(productionDebugResourceId, 0);
            lastSaveMessage = "Ресурс на складе обнулен: " + productionDebugResourceId + ".";
        }

        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DrawCurrentIslandIndustries(IslandConfig island, IslandProductionState state)
    {
        if (island == null || state == null || worldConfig == null) return;

        GUILayout.Space(6f);
        GUILayout.Label("Производственные линии");

        int shown = 0;
        for (int i = 0; i < worldConfig.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = worldConfig.islandIndustries[i];
            if (industry == null || industry.islandId != island.id) continue;

            IndustryRecipeConfig recipe = worldConfig.GetIndustryRecipe(industry.recipeId);
            IslandIndustryState runtime = state.GetIndustryState(industry.id, true);
            shown++;

            GUILayout.Space(4f);
            GUILayout.Label(industry.DisplayNameRu + " [" + industry.kind + "]");
            if (recipe == null)
            {
                GUILayout.Label("Нет рецепта: " + industry.recipeId);
                continue;
            }

            GUILayout.Label("Рецепт: " + recipe.DisplayNameRu);
            GUILayout.Label(runtime.active ? "Идет цикл, осталось " + FormatRemaining(runtime.nextCompletionUtcTicks) : "Ожидание");
            GUILayout.Label("Циклов: " + runtime.completedCycles + ", срывов: " + runtime.failedCycles);

            if (industry.kind == IslandIndustryKind.Conversion)
            {
                GUILayout.Label("Маховик: x" + runtime.conversionMultiplier.ToString("0.##"));
            }

            if (industry.kind == IslandIndustryKind.Assembly)
            {
                GUILayout.Label("Этап: " + (runtime.activeStepIndex + 1) + "/" + Mathf.Max(1, recipe.assemblySteps.Count));
            }

            if (industry.kind == IslandIndustryKind.Reaction)
            {
                GUILayout.Label("Скорость реакции: x" + runtime.reactionSpeedMultiplier.ToString("0.#") + ", шанс текущей партии " + (runtime.activeReactionSuccessChance * 100f).ToString("0") + "%");
                GUILayout.BeginHorizontal();
                DrawReactionSpeedButton(runtime, 1f);
                DrawReactionSpeedButton(runtime, 5f);
                DrawReactionSpeedButton(runtime, 10f);
                DrawReactionSpeedButton(runtime, 25f);
                DrawReactionSpeedButton(runtime, 50f);
                GUILayout.EndHorizontal();
            }

            if (runtime.active && GUILayout.Button("Прервать цикл"))
            {
                IslandIndustrySimulator.CancelCycle(progress, island.id, industry.id, out lastSaveMessage);
            }

            if (!string.IsNullOrWhiteSpace(runtime.lastMessage))
            {
                GUILayout.Label(runtime.lastMessage);
            }
        }

        if (shown == 0)
        {
            GUILayout.Label("На острове нет производственных линий из Production_industry.csv.");
        }
    }

    private void DrawReactionSpeedButton(IslandIndustryState runtime, float speed)
    {
        bool previous = GUI.enabled;
        GUI.enabled = previous && runtime != null && !Mathf.Approximately(runtime.reactionSpeedMultiplier, speed);
        if (GUILayout.Button("x" + speed.ToString("0")))
        {
            runtime.reactionSpeedMultiplier = speed;
            runtime.lastMessage = "Скорость реакции изменена на x" + speed.ToString("0") + ".";
        }

        GUI.enabled = previous;
    }

    private float GetCurrentIslandProductionRate(IslandProductionState state, IslandProductionConfig production)
    {
        return production != null ? production.productionCountBasePerMinute * GetCurrentIslandProductionMultiplier(state, production) : 0f;
    }

    private float GetCurrentIslandProductionMultiplier(IslandProductionState state, IslandProductionConfig production)
    {
        if (state == null || production == null) return 1f;

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

    private void DrawCurrentIslandConsumption(IslandProductionState state, IslandProductionConfig production)
    {
        if (state == null || production == null || production.consumptions == null || production.consumptions.Count == 0)
        {
            GUILayout.Label("Потребление: нет.");
            return;
        }

        GUILayout.Space(4f);
        GUILayout.Label("Что нужно привозить:");
        for (int i = 0; i < production.consumptions.Count; i++)
        {
            IslandConsumptionConfig consumption = production.consumptions[i];
            if (consumption == null || string.IsNullOrWhiteSpace(consumption.itemId)) continue;

            IslandConsumptionState consumptionState = state.GetConsumptionState(consumption.itemId, false);
            bool satisfied = consumptionState != null && consumptionState.isSatisfied;
            int stored = state.GetResourceAmount(consumption.itemId);
            string itemName = worldConfig.GetItemNameRu(consumption.itemId);
            string interval = FormatDurationSeconds(60f / Mathf.Max(0.0001f, consumption.countPerMinute));
            string nextConsumption = FormatNextConsumptionTime(consumptionState, consumption, stored);

            GUILayout.Label($"{itemName}: есть {stored} кг");
            GUILayout.Label($"Нужно: {consumption.countPerMinute:F2} кг/мин, 1 кг каждые {interval}");
            GUILayout.Label($"Следующее списание: {nextConsumption}");
            GUILayout.Label($"Бонус: x{consumption.satisfiedProductionMultiplier:F2} - {(satisfied ? "активен" : "не активен")}");
        }
    }

    private static string FormatNextConsumptionTime(IslandConsumptionState state, IslandConsumptionConfig consumption, int stored)
    {
        if (consumption == null || consumption.countPerMinute <= 0f) return "-";
        if (state == null || !state.isSatisfied)
        {
            return stored > 0 ? "сейчас: активация за 1 кг" : "ожидает товар";
        }

        if (state.consumptionProgress >= 1f) return "сейчас, как только будет 1 кг";

        float remainingProgress = Mathf.Max(0f, 1f - state.consumptionProgress);
        float remainingSeconds = remainingProgress / consumption.countPerMinute * 60f;
        string suffix = stored > 0 ? "" : " (нет товара для продления)";
        return FormatDurationSeconds(remainingSeconds) + suffix;
    }

    private static string FormatDurationSeconds(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) return "-";

        int totalSeconds = Mathf.CeilToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int restSeconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}ч {minutes:D2}м";
        }

        if (minutes > 0)
        {
            return $"{minutes}м {restSeconds:D2}с";
        }

        return $"{restSeconds}с";
    }

    private void DrawAssemblyUi()
    {
        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null || !activeCatalog.HasAssemblyParts())
        {
            GUILayout.Label("В каталоге нет корпусов и модулей для сборки.");
            return;
        }

        GUILayout.Space(8f);
        GUILayout.Label("Сборка корабля");

        bool assembled = ShipAssemblyBuilder.TryBuild(activeCatalog, techTree, progress, out ShipAssemblyResult result);
        GUILayout.Label((assembled ? "Готов к вылету: " : "Нельзя вылететь: ") + result.message);
        if (assembled)
        {
            DrawAssemblySummary(result);
        }

        if (GUILayout.Button(new GUIContent("Заполнить обязательные слоты", "Поставит первый купленный подходящий модуль во все пустые обязательные слоты.")))
        {
            AutoInstallRequiredModules(true, out _);
        }

        GUILayout.Space(4f);
        GUILayout.Label("Корпус");
        for (int i = 0; i < activeCatalog.parts.Count; i++)
        {
            ShipPartDefinitionSO hull = activeCatalog.parts[i];
            if (hull == null || !hull.IsHull) continue;

            bool usable = ShipAssemblyBuilder.IsPartUsable(hull, techTree, progress);
            bool selected = progress.selectedHullId == hull.partId;
            GUILayout.BeginHorizontal();
            GUILayout.Label(GetPartName(hull) + (usable ? "" : " (не куплен)"));
            GUI.enabled = usable && !selected;
            if (GUILayout.Button(selected ? "Выбран" : "Выбрать", GUILayout.Width(90f)))
            {
                SelectHull(hull.partId);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);
        GUILayout.Label("Слоты");
        List<ShipSlotDefinition> slots = GetAssemblySlotsForUi(activeCatalog);
        for (int i = 0; i < slots.Count; i++)
        {
            DrawAssemblySlot(activeCatalog, slots[i]);
        }
    }

    private void DrawAssemblySlot(ShipCatalogSO activeCatalog, ShipSlotDefinition slot)
    {
        if (activeCatalog == null || slot == null) return;

        string currentModuleId = progress.GetInstalledModule(slot.slotId);
        ShipPartDefinitionSO currentModule = activeCatalog.GetPartById(currentModuleId);
        string required = slot.required ? "обязательный" : "необязательный";
        GUILayout.Label(slot.displayName + " [" + slot.slotTypeId + ", " + required + "]");

        GUILayout.BeginHorizontal();
        GUILayout.Label(currentModule != null ? GetPartName(currentModule) : "Пусто");
        GUI.enabled = currentModule != null;
        if (GUILayout.Button("Снять", GUILayout.Width(70f)))
        {
            InstallModule(slot.slotId, "");
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        bool hasAvailableModule = false;
        for (int i = 0; i < activeCatalog.parts.Count; i++)
        {
            ShipPartDefinitionSO module = activeCatalog.parts[i];
            if (module == null || !module.IsModule || !module.CanFitSlot(slot)) continue;
            if (!ShipAssemblyBuilder.IsPartUsable(module, techTree, progress)) continue;

            hasAvailableModule = true;
            GUI.enabled = currentModule == null || currentModule.partId != module.partId;
            if (GUILayout.Button("Поставить: " + GetPartName(module)))
            {
                InstallModule(slot.slotId, module.partId);
            }
            GUI.enabled = true;
        }

        if (!hasAvailableModule)
        {
            GUILayout.Label(slot.required ? "Нет купленных подходящих модулей. Вылет невозможен." : "Нет купленных подходящих модулей.");
        }
    }

    private void DrawAssemblySummary(ShipAssemblyResult result)
    {
        if (result == null || result.stats == null) return;

        ShipStatBlock stats = result.stats;
        GUILayout.Label("Итог сборки:");
        GUILayout.Label("Масса: " + stats.Get(ShipStatId.BaseMass, 0f).ToString("F0") + " кг");
        GUILayout.Label("Лимит корпуса: " + stats.Get(ShipStatId.HullMaxTakeoffMassKg, 0f).ToString("F0") + " кг взлетной массы");
        GUILayout.Label("Двигатель: " + stats.Get(ShipStatId.EngineMaxPower, 0f).ToString("F0") + " кВт на 100%");
        GUILayout.Label("Винт: расчет " + stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f).ToString("F1") + " м/с, тяга без жесткого потолка");
        GUILayout.Label("Клавдий: макс. подъем " + stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f).ToString("F0") + " кг");
    }

    private List<ShipSlotDefinition> GetAssemblySlotsForUi(ShipCatalogSO activeCatalog)
    {
        List<ShipSlotDefinition> slots = new List<ShipSlotDefinition>();
        if (activeCatalog == null) return slots;

        ShipPartDefinitionSO hull = activeCatalog.GetPartById(progress.selectedHullId);
        if (hull == null || !hull.IsHull)
        {
            hull = activeCatalog.GetStarterHull();
        }

        if (hull == null) return slots;

        AddAssemblySlotsForUi(slots, hull.slots, "", progress);
        for (int i = 0; i < slots.Count; i++)
        {
            ShipSlotDefinition slot = slots[i];
            ShipPartDefinitionSO module = activeCatalog.GetPartById(progress.GetInstalledModule(slot.slotId));
            if (module != null && module.IsModule && module.CanFitSlot(slot))
            {
                AddAssemblySlotsForUi(slots, module.grantedSlots, slot.slotId + ":" + module.partId + ":", progress);
            }
        }

        return slots;
    }

    private static void AddAssemblySlotsForUi(List<ShipSlotDefinition> target, List<ShipSlotDefinition> source, string prefix, PlayerProgress progress)
    {
        if (target == null || source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            ShipSlotDefinition slot = source[i];
            if (slot == null) continue;
            if (!ShipAssemblyBuilder.ShouldIncludeSlotForProgress(slot, progress)) continue;

            string slotId = string.IsNullOrWhiteSpace(slot.slotId) ? "slot_" + i : slot.slotId;
            target.Add(slot.CloneWithId(prefix + slotId));
        }
    }

    private static string GetPartName(ShipPartDefinitionSO part)
    {
        if (part == null) return "";
        return string.IsNullOrWhiteSpace(part.displayName) ? part.partId : part.displayName;
    }

    private void DrawTechTreeList()
    {
        if (techTree == null || techTree.nodes == null) return;

        GUILayout.Space(8f);
        GUILayout.Label("Техника");

        for (int i = 0; i < techTree.nodes.Count; i++)
        {
            TechTreeNode node = techTree.nodes[i];
            if (node == null) continue;

            bool researched = progress.IsNodeResearched(node.nodeId);
            bool purchased = progress.IsNodePurchased(node.nodeId);

            GUILayout.BeginHorizontal();
            string priceText = node.RequiresPurchase ? " / " + node.purchasePrice + " мон." : " / без покупки";
            GUILayout.Label(node.displayName + " опыт " + node.researchCostXp + priceText);

            GUI.enabled = !researched;
            if (GUILayout.Button("Исслед.", GUILayout.Width(80f)))
            {
                TryResearchNode(node.nodeId);
            }

            GUI.enabled = node.RequiresPurchase && researched && !purchased;
            if (GUILayout.Button("Купить", GUILayout.Width(70f)))
            {
                TryPurchaseNode(node.nodeId);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private string FormatRemaining(long targetUtcTicks)
    {
        if (targetUtcTicks <= 0) return "-";

        TimeSpan remaining = new DateTime(targetUtcTicks, DateTimeKind.Utc) - GetProcessUtcNow();
        if (remaining <= TimeSpan.Zero) return "ready";

        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        return $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private static string GetModeName(GameSessionMode mode)
    {
        return mode == GameSessionMode.Docked ? "Стыковка" : "Вылет";
    }

    private static string GetDockKindName(DockingLocationKind kind)
    {
        return kind == DockingLocationKind.Ship ? "корабль" : "остров";
    }
}
