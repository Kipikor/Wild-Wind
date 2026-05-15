using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MetaGameSaveData
{
    [InspectorName("Версия сохранения")]
    public int version = 1;
    [InspectorName("Прогресс")]
    public PlayerProgress progress = new PlayerProgress();
}

public partial class MetaGameState : MonoBehaviour
{
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
    public int startingFuelKg = 50;
    [InspectorName("Стартовый клавдий на борту, кг")]
    public int startingClaudiumKg = 25;

    [Header("Процессы реального времени")]
    [InspectorName("Обновлять процессы во время игры")]
    public bool processRealTimeWhilePlaying = true;
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

    [Header("Отладочный интерфейс стыковки")]
    [InspectorName("Показывать интерфейс")]
    public bool showDockingDebugUI = true;
    [InspectorName("Ширина интерфейса")]
    public int debugUiWidth = 380;

    [Header("Аварии")]
    [InspectorName("Автоматически добавить детектор крушений")]
    [Tooltip("Если включено, на корабль будет добавлен детектор крушений: при аварии текущий корабль и груз теряются, игрок возвращается в город.")]
    public bool autoInstallCrashDetector = true;

    public GameSessionMode CurrentMode => progress != null ? progress.currentMode : startingMode;
    public bool IsDocked => CurrentMode == GameSessionMode.Docked;
    public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private bool initialized;
    private bool isAdvancingProcesses;
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

    private ShipCatalogSO ActiveCatalog => catalog != null ? catalog : shipLoader != null ? shipLoader.catalog : null;
    public WorldConfigDatabase WorldConfig => worldConfig;
    public ShipCatalogSO CurrentCatalog => ActiveCatalog;

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
        public float maxCargoKg;
        public float allowedTakeoffMassKg;
        public float engineLiftKg;
        public float claudiumMaxLiftKg;
        public float hullLimitKg;
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
        }
    }

    private void ReloadWorldConfigs()
    {
        if (worldConfig == null)
        {
            worldConfig = new WorldConfigDatabase();
        }

        worldConfig.LoadFromAssetsConfigFolder(worldConfigFolder);
        if (progress != null)
        {
            progress.Normalize();
            IslandProductionSimulator.EnsureIslandStates(worldConfig, progress);
            EnsureLogisticsFleet();
            EnsureGasSystems();
            EnsureMiningSystems();
            logisticsFleet?.EnsureRuntimeShips(progress);
            gasHarvesterFleet?.EnsureRuntimeShips(progress);
            miningFleet?.EnsureRuntimeShips(progress);
        }
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
            dock.autoDockWhenInRange = true;
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

        bool loadedGame = false;
        if (loadSavedGameOnAwake)
        {
            loadedGame = LoadGame();
        }

        EnsureProgressInitialized();
        SpawnConfiguredIslands();
        gasCloudManager?.SpawnConfiguredClouds();
        miningRockManager?.SpawnConfiguredRocks();
        if (!loadedGame || !TryAdvanceOfflineProgressFromLastSave(DateTime.UtcNow, out _))
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
        SpawnConfiguredIslands();
        gasCloudManager?.SpawnConfiguredClouds();
        miningRockManager?.SpawnConfiguredRocks();
        ApplySelectedShip();
        ApplySessionModeToShip();
    }

    private void Update()
    {
        ApplyUnityTimeScale();

        if (processRealTimeWhilePlaying)
        {
            EnsureProgressInitialized();
            AdvanceScaledRealTimeProcesses();
        }

        SyncShipConsumablesWithCargo(false);
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
        IslandProductionSimulator.EnsureIslandStates(worldConfig, progress);
        EnsureLogisticsFleet();
        EnsureGasSystems();
        EnsureMiningSystems();
        logisticsFleet?.EnsureRuntimeShips(progress);
        gasHarvesterFleet?.EnsureRuntimeShips(progress);
        miningFleet?.EnsureRuntimeShips(progress);

        if (initialized) return;

        if (progress == null)
        {
            progress = new PlayerProgress();
        }

        progress.Normalize();
        IslandProductionSimulator.EnsureIslandStates(worldConfig, progress);
        logisticsFleet?.EnsureRuntimeShips(progress);
        gasHarvesterFleet?.EnsureRuntimeShips(progress);
        miningFleet?.EnsureRuntimeShips(progress);

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
            progress.money = startingMoney;
        }

        if (!progress.receivedStartingInventory)
        {
            progress.AddResource("ore", startingOre);
            progress.AddResource("iron", startingIron);
            AddStartingShipConsumables();
            progress.receivedStartingInventory = true;
        }

        long nowTicks = DateTime.UtcNow.Ticks;
        if (progress.lastProcessUtcTicks == 0)
        {
            progress.lastProcessUtcTicks = nowTicks;
        }

        if (progress.nextShopRefreshUtcTicks == 0)
        {
            progress.nextShopRefreshUtcTicks = nowTicks + TimeSpan.FromSeconds(Mathf.Max(1, shopRefreshIntervalSeconds)).Ticks;
            progress.shopSeed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        ApplyStartingTechTreeNodes();
        ApplyStartingTechnologies();
        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out _);
        initialized = true;
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
                ApplyCargoMassToShip(shipLoader.targetShip);
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

        if (!progress.SelectHull(hullId)) return false;

        ApplySelectedShip();
        AutoSaveIfDocked();
        return true;
    }

    public bool InstallModule(string slotId, string moduleId)
    {
        EnsureProgressInitialized();
        if (!IsDocked) return false;

        progress.InstallModule(slotId, moduleId);
        ApplySelectedShip();
        AutoSaveIfDocked();
        return true;
    }

    public void AddMoney(int amount)
    {
        EnsureProgressInitialized();
        progress.money += Mathf.Max(0, amount);
    }

    public void AddResource(string resourceId, int amount)
    {
        EnsureProgressInitialized();
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
        progress.AddShipExperience(shipId, amount);
    }

    public bool TryResearchNode(string nodeId)
    {
        if (techTree == null) return false;

        EnsureProgressInitialized();

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
        if (techTree == null) return false;

        EnsureProgressInitialized();

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
        EnsureProgressInitialized();

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

        progress.SetFlight(missionId);
        ApplySelectedShip();
        ApplySessionModeToShip();
        lastSaveMessage = "Вылет начат. Ручное сохранение доступно у дока, выход из игры сохранит текущий полет.";
        return true;
    }

    public bool BeginFreeFlight()
    {
        return TryBeginFlightSession(null);
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

        progress.SetDocked(dockId, dockKind, GetCurrentShipPosition());
        ApplySessionModeToShip();

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        return true;
    }

    public void CompleteFlightMission(MissionDefinitionSO mission)
    {
        EnsureProgressInitialized();

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
        AddStartingShipConsumables();
        progress.StopCargoTransfer();
        ApplyStartingTechTreeNodes();
        ApplyStartingTechnologies();

        string assemblyMessage = "";
        bool assemblyReady = ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, techTree, progress, out assemblyMessage);
        string recoveryDockId = string.IsNullOrWhiteSpace(startingDockId) ? GetCapitalIslandId() : startingDockId;
        DockingLocationKind recoveryDockKind = startingDockKind;
        Vector3 recoveryPosition = GetDockPositionOrFallback(recoveryDockId, recoveryDockKind);

        progress.SetDocked(recoveryDockId, recoveryDockKind, recoveryPosition);
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

        return assemblyReady;
    }

    public bool StartIdleMining()
    {
        EnsureProgressInitialized();
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
            IslandProductionSimulator.EnsureIslandStates(worldConfig, progress);

            if (progress.lastProcessUtcTicks <= 0)
            {
                progress.lastProcessUtcTicks = utcNow.Ticks;
            }

            long previousProcessTicks = progress.lastProcessUtcTicks;
            if (utcNow.Ticks <= previousProcessTicks)
            {
                return 0;
            }

            AdvanceShopRefresh(utcNow);

            if (islandProductionEnabled)
            {
                completedCycles += IslandProductionSimulator.Advance(worldConfig, progress, previousProcessTicks, utcNow.Ticks);
            }

            completedCycles += AdvanceCargoTransfer(utcNow);
            completedCycles += AdvanceTechnologyResearch(utcNow);
            if (logisticsFleet != null)
            {
                completedCycles += logisticsFleet.Advance(worldConfig, progress, ActiveCatalog, techTree, previousProcessTicks, utcNow.Ticks);
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

    private bool TrySaveGame(bool allowFlightSave)
    {
        EnsureProgressInitialized();

        if (!IsDocked && !allowFlightSave)
        {
            lastSaveMessage = "Нельзя сохранить: сначала нужна стыковка.";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (Application.isPlaying)
        {
            AdvanceScaledRealTimeProcesses();
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

        MetaGameSaveData saveData = new MetaGameSaveData { progress = progress };

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
                StopCargoTransfer("Погрузка остановлена: не хватает товара или грузоподъемности.");
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
            if (capacity.currentCargoKg + 1f > capacity.maxCargoKg + 0.001f) return false;
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
        return Mathf.Max(0f, capacity.maxCargoKg - capacity.currentCargoKg);
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
        CargoCapacityInfo capacity = CalculateCargoCapacity();
        int freeKg = Mathf.FloorToInt(Mathf.Max(0f, capacity.maxCargoKg - capacity.currentCargoKg));
        if (!capacity.assemblyValid || freeKg < amount)
        {
            reason = $"Не хватает грузоподъемности: нужно {amount} кг, свободно {freeKg} кг.";
            return false;
        }

        progress.AddShipCargo(resourceId, amount);
        ApplyCargoMassToShip(GetActiveShip());
        ResetCargoPlan();
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

    private CargoCapacityInfo CalculateCargoCapacity()
    {
        CargoCapacityInfo info = new CargoCapacityInfo
        {
            assemblyValid = false,
            canFly = false,
            reason = "сборка корабля не проверена.",
            currentCargoKg = progress != null ? progress.GetShipCargoMassKg() : 0f
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

    private static CargoCapacityInfo CompleteCargoCapacity(CargoCapacityInfo info)
    {
        info.allowedTakeoffMassKg = Mathf.Min(info.engineLiftKg, Mathf.Min(info.claudiumMaxLiftKg, info.hullLimitKg));
        info.maxCargoKg = Mathf.Max(0f, info.allowedTakeoffMassKg - info.emptyMassKg);

        if (!info.assemblyValid)
        {
            return info;
        }

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

        if (info.currentCargoKg > info.maxCargoKg + 0.001f)
        {
            info.reason = $"груз {info.currentCargoKg:F0} кг больше доступной грузоподъемности {info.maxCargoKg:F0} кг.";
            return info;
        }

        info.canFly = true;
        info.reason = "масса в норме.";
        return info;
    }

    private void ApplyCargoMassToShip(ShipPhysics ship)
    {
        if (ship == null || progress == null) return;

        ship.cargoMassKg = Mathf.Max(0, progress.GetShipCargoMassKg());
        RefreshShipConsumablesFromCargo(ship, true);
        ship.RefreshRuntimeShipSettings();
    }

    private void RefreshShipConsumablesFromCargo(ShipPhysics ship, bool resetPendingConsumption)
    {
        if (ship == null || progress == null) return;

        ApplyFuelConfigToShip(ship);
        if (resetPendingConsumption || syncedFuelResourceId != ship.engineFuelId)
        {
            syncedFuelResourceId = ship.engineFuelId;
            pendingFuelConsumedKg = 0f;
        }

        string claudiumResourceId = GetClaudiumResourceId(ship);
        if (resetPendingConsumption || syncedClaudiumResourceId != claudiumResourceId)
        {
            syncedClaudiumResourceId = claudiumResourceId;
            pendingClaudiumConsumedKg = 0f;
        }

        ship.engineFuelStockKg = GetAvailableCargoResourceKg(ship.engineFuelId, pendingFuelConsumedKg);
        ship.claudiumStock = GetAvailableCargoResourceKg(claudiumResourceId, pendingClaudiumConsumedKg);
    }

    private void SyncShipConsumablesWithCargo(bool resetPendingConsumption)
    {
        ShipPhysics ship = GetActiveShip();
        if (ship == null || progress == null) return;

        if (resetPendingConsumption)
        {
            RefreshShipConsumablesFromCargo(ship, true);
            ship.cargoMassKg = Mathf.Max(0, progress.GetShipCargoMassKg());
            ship.RefreshRuntimeShipSettings();
            return;
        }

        ApplyFuelConfigToShip(ship);
        SyncCargoResourceFromRuntime(ship.engineFuelId, ref syncedFuelResourceId, ref pendingFuelConsumedKg, ref ship.engineFuelStockKg);
        string claudiumResourceId = GetClaudiumResourceId(ship);
        SyncCargoResourceFromRuntime(claudiumResourceId, ref syncedClaudiumResourceId, ref pendingClaudiumConsumedKg, ref ship.claudiumStock);

        ship.cargoMassKg = Mathf.Max(0, progress.GetShipCargoMassKg());
        ship.RefreshRuntimeShipSettings();
    }

    private void SyncCargoResourceFromRuntime(string resourceId, ref string syncedResourceId, ref float pendingConsumedKg, ref float runtimeStockKg)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || progress == null)
        {
            syncedResourceId = resourceId ?? "";
            pendingConsumedKg = 0f;
            runtimeStockKg = 0f;
            return;
        }

        if (syncedResourceId != resourceId)
        {
            syncedResourceId = resourceId;
            pendingConsumedKg = 0f;
            runtimeStockKg = progress.GetShipCargoAmount(resourceId);
            return;
        }

        int cargoAmount = progress.GetShipCargoAmount(resourceId);
        pendingConsumedKg = Mathf.Clamp(pendingConsumedKg, 0f, Mathf.Max(0, cargoAmount));
        float expectedRuntimeStock = GetAvailableCargoResourceKg(resourceId, pendingConsumedKg);
        float consumedSinceLastSync = Mathf.Max(0f, expectedRuntimeStock - Mathf.Max(0f, runtimeStockKg));
        pendingConsumedKg += consumedSinceLastSync;

        int wholeKgToSpend = Mathf.Min(cargoAmount, Mathf.FloorToInt(pendingConsumedKg + 0.0001f));
        if (wholeKgToSpend > 0 && progress.TrySpendShipCargo(resourceId, wholeKgToSpend))
        {
            pendingConsumedKg -= wholeKgToSpend;
            cargoAmount = progress.GetShipCargoAmount(resourceId);
        }

        pendingConsumedKg = Mathf.Clamp(pendingConsumedKg, 0f, Mathf.Max(0, cargoAmount));
        runtimeStockKg = GetAvailableCargoResourceKg(resourceId, pendingConsumedKg);
    }

    private float GetAvailableCargoResourceKg(string resourceId, float pendingConsumedKg)
    {
        if (progress == null || string.IsNullOrWhiteSpace(resourceId)) return 0f;
        return Mathf.Max(0f, progress.GetShipCargoAmount(resourceId) - Mathf.Max(0f, pendingConsumedKg));
    }

    private static string GetClaudiumResourceId(ShipPhysics ship)
    {
        if (ship == null || string.IsNullOrWhiteSpace(ship.claudiumResourceId)) return "claudium";
        return ship.claudiumResourceId;
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

        if (startingFuelKg > 0)
        {
            progress.AddShipCargo("wood", startingFuelKg);
        }

        if (startingClaudiumKg > 0)
        {
            progress.AddShipCargo("claudium", startingClaudiumKg);
        }
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

        if (docked)
        {
            if (progress.hasCurrentDockPosition)
            {
                ship.transform.position = progress.currentDockPosition;
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
                ship.transform.SetPositionAndRotation(progress.currentFlightPosition, progress.currentFlightRotation);
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

        progress.currentDockPosition = GetCurrentShipPosition();
        progress.hasCurrentDockPosition = true;
    }

    private void RememberCurrentFlightPose()
    {
        if (CurrentMode != GameSessionMode.Flight || progress == null) return;

        ShipPhysics ship = GetActiveShip();
        if (ship == null) return;

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
        GUILayout.Label("Док: " + progress.currentDockId + " (" + GetDockKindName(progress.currentDockKind) + ")");
        GUILayout.Label("Деньги: " + progress.money);
        GUILayout.Label("Руда: " + progress.GetResourceAmount("ore") + "  Железо: " + progress.GetResourceAmount("iron"));
        GUILayout.Label("Зерно магазина: " + progress.shopSeed + "  обновление через " + FormatRemaining(progress.nextShopRefreshUtcTicks));
        DrawTimeScaleUi();

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

    private void DrawDockedDebugUi()
    {
        GUILayout.Label("Стыковка");
        DrawAssemblyUi();
        DrawCargoTransferUi();
        GUILayout.Space(8f);

        if (GUILayout.Button(new GUIContent("Сохранить у дока", "Сохраняет прогресс только если корабль находится в режиме стыковки.")))
        {
            TrySaveGame();
        }

        if (GUILayout.Button(new GUIContent("Удалить сохранение и начать заново", "Удаляет файл сохранения и сбрасывает текущий мета-прогресс.")))
        {
            DeleteSave();
        }

        if (missionController != null && missionController.mission != null && GUILayout.Button(new GUIContent("Вылететь на миссию", "Переводит игру в режим вылета. Прогресс сохранится после стыковки или аварийного возврата.")))
        {
            missionController.BeginMission();
        }

        if (GUILayout.Button(new GUIContent("Свободный вылет", "Начинает полет без активной миссии. Прогресс сохранится только после следующей стыковки.")))
        {
            BeginFreeFlight();
        }

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

    private void DrawFlightDebugUi()
    {
        GUILayout.Label("Вылет");
        GUILayout.Label("Ручное сохранение у дока. При выходе из игры текущий вылет сохранится.");

        ShipPhysics ship = GetActiveShip();
        if (ship != null)
        {
            DrawRouteEtaFlightInfo(ship);
            GUILayout.Label($"Топливо на борту: {progress.GetShipCargoAmount(ship.engineFuelId)} кг ({ship.engineFuelStockKg:F1} доступно)");
            string claudiumResourceId = GetClaudiumResourceId(ship);
            GUILayout.Label($"Клавдий на борту: {progress.GetShipCargoAmount(claudiumResourceId)} кг ({ship.claudiumStock:F1} доступно)");

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

        if (GUILayout.Button(new GUIContent("Состыковаться здесь", "Завершает вылет в текущей точке и сохраняет новый док.")))
        {
            DockAt("field_dock", DockingLocationKind.Island);
        }

        if (GUILayout.Button(new GUIContent("Потерять корабль", "Завершает вылет аварией: груз и текущая сборка теряются, игрок возвращается в город на стартовом корабле.")))
        {
            LoseShipAndReturnToCity("Ручной аварийный возврат");
        }
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
        GUILayout.Label($"Груз: {capacity.currentCargoKg:F0}/{capacity.maxCargoKg:F0} кг");
        GUILayout.Label($"Ограничения: двигатель+контур {capacity.engineLiftKg:F0} кг, контур {capacity.claudiumMaxLiftKg:F0} кг, корпус {capacity.hullLimitKg:F0} кг");

        if (!capacity.canFly)
        {
            GUILayout.Label("Вылет заблокирован: " + capacity.reason);
        }

        if (progress.cargoTransfer != null && progress.cargoTransfer.active)
        {
            int remaining = progress.cargoTransfer.GetRemainingUnits();
            GUILayout.Label($"Идет погрузка: осталось {remaining} кг, следующая операция через {FormatRemaining(progress.cargoTransfer.nextOperationUtcTicks)}");
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
        EnsureCargoPlan(island.id);

        int plannedCargoMass = GetPlannedCargoMassKg();
        int operationCount = GetCargoPlanOperationCount();
        bool overload = plannedCargoMass > capacity.maxCargoKg + 0.001f;

        GUILayout.Label($"План: {plannedCargoMass}/{capacity.maxCargoKg:F0} кг, операций: {operationCount}, время: {operationCount * island.timeForOneItemLoadSeconds:F1} сек");
        if (overload)
        {
            GUILayout.Label("План перегружает корабль.");
        }

        for (int i = 0; i < worldConfig.items.Count; i++)
        {
            ItemConfig item = worldConfig.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id)) continue;
            DrawCargoPlanRow(item, storage);
        }

        GUI.enabled = operationCount > 0 && !overload && capacity.emptyMassKg <= capacity.allowedTakeoffMassKg + 0.001f;
        if (GUILayout.Button(new GUIContent("Подтвердить погрузку", "Сначала выполняется выгрузка с борта на склад, затем загрузка со склада на борт. Каждая единица товара занимает время из Island.csv.")))
        {
            StartCargoTransfer(island, storage);
        }
        GUI.enabled = true;
    }

    private void DrawCargoPlanRow(ItemConfig item, IslandProductionState storage)
    {
        int shipAmount = progress.GetShipCargoAmount(item.id);
        int storageAmount = storage != null ? storage.GetResourceAmount(item.id) : 0;
        CargoPlanEntry entry = GetCargoPlanEntry(item.id, shipAmount);
        entry.targetShipAmount = Mathf.Clamp(entry.targetShipAmount, 0, shipAmount + storageAmount);

        string itemName = worldConfig.GetItemNameRu(item.id);
        GUILayout.Label($"{itemName}: склад {storageAmount} кг, борт {shipAmount} кг, цель {entry.targetShipAmount} кг");

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
        if (GUILayout.Button(new GUIContent("Унич. 1 борт", "Мгновенно уничтожает 1 кг этого товара на корабле.")))
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
        if (GUILayout.Button(new GUIContent("Унич. 1 склад", "Мгновенно уничтожает 1 кг этого товара на складе острова.")))
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
        GUILayout.Label("Уничтожить, кг", GUILayout.Width(110f));
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

    private int GetPlannedCargoMassKg()
    {
        int total = 0;
        for (int i = 0; i < cargoPlan.Count; i++)
        {
            CargoPlanEntry entry = cargoPlan[i];
            if (entry == null) continue;
            total += Mathf.Max(0, entry.targetShipAmount);
        }

        return total;
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

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (GetPlannedCargoMassKg() > capacity.maxCargoKg + 0.001f)
        {
            lastSaveMessage = "Нельзя начать погрузку: план перегружает корабль.";
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
            SpawnConfiguredIslands(true);
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
        if (production == null || state == null)
        {
            GUILayout.Label(GetIslandDisplayName(island) + ": производство не задано.");
            return;
        }

        string producedName = worldConfig.GetItemNameRu(production.productionItemId);
        int producedStored = state.GetResourceAmount(production.productionItemId);
        float multiplier = GetCurrentIslandProductionMultiplier(state, production);
        float currentRate = production.productionCountBasePerMinute * multiplier;

        GUILayout.Label(GetIslandDisplayName(island));
        GUILayout.Label($"Производит: {producedName}");
        GUILayout.Label($"Склад: {producedStored} кг");
        GUILayout.Label($"База: {production.productionCountBasePerMinute:F2} кг/мин");
        GUILayout.Label($"Бонусы: x{multiplier:F2}, сейчас {currentRate:F2} кг/мин");
        GUILayout.Label($"Коорд.: X {island.position.x:F0}  Y {island.position.y:F0}  Z {island.position.z:F0}");
        GUILayout.Label($"Док: {island.dockingRadius:F0} м, погрузка {island.timeForOneItemLoadSeconds:F1} сек/кг");

        DrawCurrentIslandConsumption(state, production);
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
        GUILayout.Label("Винт: " + stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f).ToString("F1") + " м/с, тяга " + stats.Get(ShipStatId.PropellerMaxThrustKgf, 0f).ToString("F0") + " кгс");
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

        AddAssemblySlotsForUi(slots, hull.slots, "");
        for (int i = 0; i < slots.Count; i++)
        {
            ShipSlotDefinition slot = slots[i];
            ShipPartDefinitionSO module = activeCatalog.GetPartById(progress.GetInstalledModule(slot.slotId));
            if (module != null && module.IsModule && module.CanFitSlot(slot))
            {
                AddAssemblySlotsForUi(slots, module.grantedSlots, slot.slotId + ":" + module.partId + ":");
            }
        }

        return slots;
    }

    private static void AddAssemblySlotsForUi(List<ShipSlotDefinition> target, List<ShipSlotDefinition> source, string prefix)
    {
        if (target == null || source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            ShipSlotDefinition slot = source[i];
            if (slot == null) continue;

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
