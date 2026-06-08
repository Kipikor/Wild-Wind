using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MetaGameAccountData
{
    public const int CurrentVersion = 2;

    [InspectorName("Версия аккаунта")]
    public int version = CurrentVersion;
    [InspectorName("Прогресс")]
    public PlayerProgress progress = new PlayerProgress();
    [InspectorName("Gameplay Session")]
    public GameplaySessionAccountData gameplaySession;
}

public partial class MetaGameState : MonoBehaviour
{
    private const float ExtractionRunupOutwardDotThreshold = 0.9659258f;
    private const float SortieEntryApproachSeconds = 20f;
    private const float SortieEntryDefaultMaxSpeedMS = 120f;
    private const string Cruiser203HullId = "cruiser203_hull";

    [Header("Связи")]
    [InspectorName("Каталог кораблей")]
    public ShipCatalogSO catalog;
    [InspectorName("Ship loader")]
    public ShipLoader shipLoader;
    [InspectorName("Прогресс игрока")]
    public PlayerProgress progress = new PlayerProgress();

    [Header("Сессия")]
    [InspectorName("Стартовый режим")]
    public GameSessionMode startingMode = GameSessionMode.Docked;
    [InspectorName("Стартовый док")]
    public string startingDockId = "capital";
    [InspectorName("Runtime Account Id")]
    [Tooltip("Single runtime account identifier. The session build does not read or write save files.")]
    public string runtimeAccountId = GameplaySessionAccountData.DefaultAccountId;

    [Header("Стартовые ресурсы")]
    [InspectorName("Стартовое топливо на борту, кг")]
    public int startingFuelKg = 150;
    [InspectorName("Стартовый клавдий на борту, кг")]
    public int startingClaudiumKg = 75;
    [Header("Процессы реального времени")]
    [InspectorName("Обновлять процессы во время игры")]
    public bool processRealTimeWhilePlaying = true;
    [InspectorName("Пропустить стартовую догонку процессов")]
    [Tooltip("Для изолированных тестовых сцен: не прокручивает логистику, разведку и другие процессы в Awake.")]
    public bool skipInitialProcessCatchUp;

    [Header("Конфиги сессии")]
    [InspectorName("Папка конфигов от Assets")]
    [Tooltip("CSV-конфиги сессионной игры загружаются из этой папки при старте Play Mode.")]
    public string sessionConfigFolder = "Data/Config";
    [InspectorName("Порт столицы")]
    [Tooltip("Порт, в котором находится лаборатория технологий и стартует новая игра.")]
    public string capitalPortId = "capital";
    [InspectorName("Визуальный радиус порта")]
    [Tooltip("Размер простой временной модели порта. Радиус стыковки берется отдельно из Port.csv.")]
    public float configPortVisualRadius = 80f;

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
    public string RuntimeAccountId => string.IsNullOrWhiteSpace(runtimeAccountId)
        ? GameplaySessionAccountData.DefaultAccountId
        : runtimeAccountId.Trim();

    private bool initialized;
    private bool isAdvancingProcesses;
    private string lastAccountMessage = "";
    private SessionConfigDatabase sessionConfig = new SessionConfigDatabase();
    private Transform spawnedConfigPortRoot;
    private string syncedFuelResourceId = "";
    private string syncedClaudiumResourceId = "";
    private float pendingFuelConsumedKg;
    private float pendingClaudiumConsumedKg;
    private string activeSortieExtractionRunupStatus = "";

    private ShipCatalogSO ActiveCatalog => catalog != null ? catalog : shipLoader != null ? shipLoader.catalog : null;
    public SessionConfigDatabase SessionConfig => sessionConfig;
    public ShipCatalogSO CurrentCatalog => ActiveCatalog;
    public int SpawnedConfiguredPortCount => spawnedConfigPortRoot != null ? spawnedConfigPortRoot.childCount : 0;

    private void EnsureEconomyRuntimeStates()
    {
        if (progress == null || sessionConfig == null || !sessionConfig.isLoaded) return;

        progress.GetPortStorageState(GetCapitalPortId(), true)?.Normalize();
        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
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

    private void EnsureSessionConfigLoaded()
    {
        if (sessionConfig == null)
        {
            sessionConfig = new SessionConfigDatabase();
        }

        if (!sessionConfig.isLoaded)
        {
            sessionConfig.LoadFromAssetsConfigFolder(sessionConfigFolder);
            SyncCsvShipPartConfigs();
        }
    }

    private void ReloadSessionConfigs()
    {
        if (sessionConfig == null)
        {
            sessionConfig = new SessionConfigDatabase();
        }

        sessionConfig.LoadFromAssetsConfigFolder(sessionConfigFolder);
        SyncCsvShipPartConfigs();
        if (progress != null)
        {
            progress.Normalize();
            EnsureEconomyRuntimeStates();
        }
    }

    private void SyncCsvShipPartConfigs()
    {
        if (sessionConfig == null || !sessionConfig.isLoaded) return;

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null) return;

        ShipAssemblyBuilder.ApplyCsvShipPartConfigs(activeCatalog, sessionConfig);
    }

    private void SpawnSessionDockingPorts(bool forceRebuild = false)
    {
        if (!Application.isPlaying) return;

        EnsureSessionConfigLoaded();
        if (sessionConfig == null || !sessionConfig.isLoaded) return;

        if (spawnedConfigPortRoot != null && !forceRebuild)
        {
            return;
        }

        if (spawnedConfigPortRoot != null)
        {
            Destroy(spawnedConfigPortRoot.gameObject);
            spawnedConfigPortRoot = null;
        }

        GameObject root = new GameObject("Session Port Dock");
        spawnedConfigPortRoot = root.transform;

        ShipPhysics activeShip = GetActiveShip();
        for (int i = 0; i < sessionConfig.ports.Count; i++)
        {
            PortConfig port = sessionConfig.ports[i];
            if (port == null || string.IsNullOrWhiteSpace(port.id)) continue;
            if (!IsCapitalPort(port.id)) continue;

            GameObject portObject = new GameObject(GetPortDisplayName(port) + " Port");
            portObject.transform.SetParent(spawnedConfigPortRoot, false);
            portObject.transform.position = port.position;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Port Dock Visual";
            visual.transform.SetParent(portObject.transform, false);
            float visualRadius = Mathf.Max(1f, configPortVisualRadius);
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

            DockingPort dock = portObject.AddComponent<DockingPort>();
            dock.targetShip = activeShip;
            dock.dockId = port.id;
            dock.displayName = GetPortDisplayName(port);
            dock.dockingRadius = Mathf.Max(0.1f, port.dockingRadius);
            dock.canEndSession = true;
            dock.snapPoint = portObject.transform;
        }
    }

    public void RefreshSessionExtractionRuntimeActors()
    {
        SpawnConfiguredSessionActors(true);
    }

    private void SpawnConfiguredSessionActors(bool forceRebuild = false)
    {
        SpawnSessionDockingPorts(forceRebuild);
    }

    public bool IsCapitalPort(string portId)
    {
        string expectedId = string.IsNullOrWhiteSpace(capitalPortId) ? "capital" : capitalPortId;
        return !string.IsNullOrWhiteSpace(portId) && portId == expectedId;
    }

    private static string GetPortDisplayName(PortConfig port)
    {
        if (port == null) return "Порт";
        return string.IsNullOrWhiteSpace(port.localNameRu) ? port.id : port.localNameRu;
    }

    private void Reset()
    {
        shipLoader = FindFirstObjectByType<ShipLoader>();
    }

    private void Awake()
    {
        CacheUnityTimeSettings();
        ResetProcessRealtimeClock();
        ReloadSessionConfigs();

        if (shipLoader == null)
        {
            shipLoader = FindFirstObjectByType<ShipLoader>();
        }


        EnsureProgressInitialized();
        SpawnConfiguredSessionActors();
        if (!skipInitialProcessCatchUp)
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
        SpawnConfiguredSessionActors();
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
        EnsureSessionConfigLoaded();
        EnsureEconomyRuntimeStates();

        if (initialized) return;

        if (progress == null)
        {
            progress = new PlayerProgress();
        }

        progress.Normalize();
        EnsureEconomyRuntimeStates();

        bool freshProgress = !progress.receivedStartingInventory &&
            !progress.hasCurrentDockPosition &&
            !progress.hasCurrentFlightPose;

        if (string.IsNullOrWhiteSpace(progress.currentDockId))
        {
            progress.SetDocked(startingDockId);
        }

        if (freshProgress)
        {
            progress.currentMode = startingMode;
            if (startingMode == GameSessionMode.Docked)
            {
                progress.SetDocked(startingDockId);
            }
        }

        ShipPartDefinitionSO starterHull = ActiveCatalog != null ? ActiveCatalog.GetStarterHull() : null;
        if (starterHull != null)
        {
            progress.EnsureStarterHull(starterHull.partId);
        }

        if (!progress.receivedStartingInventory)
        {
            AddStartingShipConsumables();
            progress.receivedStartingInventory = true;
        }

        long nowTicks = DateTime.UtcNow.Ticks;
        if (progress.lastProcessUtcTicks == 0)
        {
            progress.lastProcessUtcTicks = nowTicks;
        }

        ApplyStartingTechnologies();
        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out _);
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
        SyncCsvShipPartConfigs();
        if (shipLoader.catalog == null)
        {
            shipLoader.catalog = ActiveCatalog;
        }

        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog != null && activeCatalog.HasAssemblyParts())
        {
            if (!shipLoader.ApplyAssembly(progress, out string message))
            {
                lastAccountMessage = message;
                return false;
            }
            else
            {
                RefreshSceneShipReferences(shipLoader.targetShip);
                ApplyFuelConfigToShip(shipLoader.targetShip);
                RefreshShipConsumablesFromTanks(shipLoader.targetShip, true);
                shipLoader.targetShip.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(sessionConfig));
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

        if (ShipAssemblyBuilder.TryBuild(activeCatalog, progress, out ShipAssemblyResult result))
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
        if (hull == null || !hull.IsHull || !ShipAssemblyBuilder.IsPartUsable(hull, progress)) return false;
        if (progress.selectedHullId != hullId)
        {
            lastAccountMessage = "Hull selector is disabled; ship replacement must use base assembly.";
            return false;
        }

        if (!progress.SelectHull(hullId)) return false;

        ApplySelectedShip();
        RefreshRuntimeAccountIfDocked();
        return true;
    }

    public bool TrySelectSessionCoreHull(string hullId, out string message)
    {
        EnsureProgressInitialized();
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Ship selection is available only at the base.";
            lastAccountMessage = message;
            return false;
        }

        ShipCatalogSO activeCatalog = ActiveCatalog;
        ShipPartDefinitionSO hull = activeCatalog != null ? activeCatalog.GetPartById(hullId) : null;
        if (hull == null || !hull.IsHull)
        {
            message = "Session hull is missing from ShipCatalog: " + hullId + ".";
            lastAccountMessage = message;
            return false;
        }

        if (!ShipAssemblyBuilder.IsPartUsable(hull, progress))
        {
            message = "Session hull is not researched: " + GetPartName(hull) + ".";
            lastAccountMessage = message;
            return false;
        }

        PlayerProgress previousProgress = progress.Clone();
        progress.ReplaceShipAssembly(hull.partId);

        bool autoInstalled = ShipAssemblyBuilder.AutoInstallRequiredModules(activeCatalog, progress, out string autoInstallMessage);
        ShipAssemblyResult result = null;
        bool assembled = autoInstalled && ShipAssemblyBuilder.TryBuild(activeCatalog, progress, out result);
        if (!autoInstalled || !assembled)
        {
            message = "Cannot assemble selected session hull: "
                + (!string.IsNullOrWhiteSpace(autoInstallMessage)
                    ? autoInstallMessage
                    : result != null ? result.message : "unknown assembly error")
                + ".";
            progress = previousProgress;
            ApplySelectedShip();
            lastAccountMessage = message;
            return false;
        }

        if (!ApplySelectedShip())
        {
            message = string.IsNullOrWhiteSpace(lastAccountMessage)
                ? "Cannot spawn selected session hull."
                : lastAccountMessage;
            progress = previousProgress;
            ApplySelectedShip();
            lastAccountMessage = message;
            return false;
        }

        RefreshRuntimeAccountIfDocked();
        message = "Selected session hull: " + GetPartName(hull) + ". " + autoInstallMessage;
        lastAccountMessage = message;
        return true;
    }

    public bool InstallModule(string slotId, string moduleId)
    {
        EnsureProgressInitialized();
        if (!IsDocked) return false;
        if (!CanInstallSessionCoreFittingModule(slotId, moduleId, out string reason))
        {
            lastAccountMessage = reason;
            return false;
        }

        progress.InstallModule(slotId, moduleId);
        ApplySelectedShip();
        RefreshRuntimeAccountIfDocked();
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

        if (!ShipAssemblyBuilder.IsPartUsable(module, progress))
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

    public IReadOnlyList<TechnologyConfig> GetTechnologyConfigs()
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return sessionConfig != null ? sessionConfig.technologies : null;
    }

    public PortStorageState GetCapitalStorageState()
    {
        EnsureProgressInitialized();
        return progress.GetPortStorageState(GetCapitalPortId(), true);
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
            && IsCapitalPort(progress.currentDockId);
    }

    public string GetCapitalPortId()
    {
        return string.IsNullOrWhiteSpace(capitalPortId) ? "capital" : capitalPortId;
    }

    public bool TrySelectResearchTechnology(string technologyId)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();

        TechnologyConfig technology = sessionConfig != null ? sessionConfig.GetTechnology(technologyId) : null;
        if (!CanSelectResearchTechnology(technology, out string reason))
        {
            lastAccountMessage = reason;
            return false;
        }

        progress.activeResearchTechnologyId = technology.id;
        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        state.completedCycles = Mathf.Clamp(state.completedCycles, 0, Mathf.Max(1, technology.requiredCycles));

        TryStartOrContinueResearchCycle(technology, state, GetProcessUtcNow().Ticks, out reason);
        lastAccountMessage = string.IsNullOrWhiteSpace(reason) ? "Исследование выбрано: " + GetTechnologyDisplayName(technology) : reason;
        RefreshRuntimeAccountIfDocked();
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
        PortStorageState storage = GetCapitalStorageState();
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
            parts.Add(sessionConfig.GetItemNameRu(cost.itemId) + " x" + cost.amount);
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

    private bool TryEnterFlightForSessionSortie()
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
            lastAccountMessage = "Нельзя вылететь: " + autoInstallReason;
            return false;
        }

        if (!CanAssembleCurrentShip(out string assemblyReason))
        {
            lastAccountMessage = "Нельзя вылететь: " + assemblyReason;
            return false;
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        if (!capacity.canFly)
        {
            lastAccountMessage = "Нельзя вылететь: " + capacity.reason;
            return false;
        }

        progress.SetFlight();
        ApplySelectedShip();
        ApplySessionModeToShip();
        lastAccountMessage = "Sortie flight started. Extraction returns cargo home.";
        return true;
    }

    public SortieZoneDefinition CreateDefaultSafeOreSortieDefinition()
    {
        Vector3 center = GetDefaultSortiePocketCenter(0f, 0f);
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
            centerPosition = center,
            entryPosition = GetDefaultSortiePocketEntry(center, 350f),
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
        Vector3 center = GetDefaultSortiePocketCenter(SessionExtractionConstants.DefaultSortiePocketSpacingMeters, 0f);
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeGasSortieId,
            displayName = SessionExtractionConstants.DefaultSafeGasSortieName,
            primaryBranch = BaseProcessingBranch.Gas,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "High gas extractor",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterGasExtractorModuleId },
            starterResourceItemId = "cloud_condensate",
            starterResourceChunkMin = 2,
            starterResourceChunkMax = 4,
            starterResourceShedIntervalSeconds = 2.1f,
            starterResourceColor = new Color(0.65f, 0.82f, 1f, 1f),
            centerPosition = center,
            entryPosition = GetDefaultSortiePocketEntry(center, 360f),
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
        Vector3 center = GetDefaultSortiePocketCenter(-SessionExtractionConstants.DefaultSortiePocketSpacingMeters, SessionExtractionConstants.DefaultSortiePocketSpacingMeters * 0.75f);
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeAutomatonSortieId,
            displayName = SessionExtractionConstants.DefaultSafeAutomatonSortieName,
            primaryBranch = BaseProcessingBranch.AutomatonDismantling,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "High impact wreck collector",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterMiningHoldModuleId },
            starterResourceItemId = SessionExtractionConstants.BrokenAutomatonItemId,
            starterResourceChunkMin = 1,
            starterResourceChunkMax = 2,
            starterResourceShedIntervalSeconds = 3.0f,
            starterResourceColor = new Color(0.78f, 0.76f, 0.68f, 1f),
            centerPosition = center,
            entryPosition = GetDefaultSortiePocketEntry(center, 340f),
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
        Vector3 center = GetDefaultSortiePocketCenter(0f, -SessionExtractionConstants.DefaultSortiePocketSpacingMeters);
        return new SortieZoneDefinition
        {
            sortieId = SessionExtractionConstants.DefaultSafeLeviathanSortieId,
            displayName = SessionExtractionConstants.DefaultSafeLeviathanSortieName,
            primaryBranch = BaseProcessingBranch.LeviathanProcessing,
            recommendedSlotBand = ShipFittingSlotBand.High,
            requiredFittingSummary = "High leviathan salvage rig",
            requiredModuleIds = new List<string> { SessionExtractionConstants.StarterLeviathanSalvageModuleId },
            starterResourceItemId = "windcalf_carcass",
            starterResourceChunkMin = 2,
            starterResourceChunkMax = 5,
            starterResourceShedIntervalSeconds = 3.4f,
            starterResourceColor = new Color(0.56f, 0.78f, 0.74f, 1f),
            centerPosition = center,
            entryPosition = GetDefaultSortiePocketEntry(center, 380f),
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
        Vector3 center = GetDefaultSortiePocketCenter(SessionExtractionConstants.DefaultSortiePocketSpacingMeters * 1.25f, SessionExtractionConstants.DefaultSortiePocketSpacingMeters * 0.8f);
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
            centerPosition = center,
            entryPosition = GetDefaultSortiePocketEntry(center, 370f),
            radiusMeters = SessionExtractionConstants.DefaultSortieRadiusMeters,
            stormFloorY = 0f,
            extractionBoundaryToleranceMeters = 140f,
            distanceToBaseKm = 220f,
            returnCruiseSpeedMS = 35f,
            returnPowerLever = 0.7f,
            returnReserveMultiplier = 1.15f
        };
    }

    private static Vector3 GetDefaultSortiePocketCenter(float offsetX, float offsetZ)
    {
        float origin = SessionExtractionConstants.DefaultSortiePocketOriginMeters;
        return new Vector3(origin + offsetX, 0f, origin + offsetZ);
    }

    private static Vector3 GetDefaultSortiePocketEntry(Vector3 center, float altitudeMeters)
    {
        return new Vector3(center.x, Mathf.Max(50f, altitudeMeters), center.z);
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
        EnsureSessionConfigLoaded();

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
            lastAccountMessage = message;
            return false;
        }

        progress.selectedSortieId = sortie.sortieId;
        message = "Selected sortie: " + sortie.displayName + ".";
        lastAccountMessage = message;
        RefreshRuntimeAccountIfDocked();
        return true;
    }

    public bool SelectNextSessionSortie(out string message)
    {
        EnsureProgressInitialized();
        List<SortieZoneDefinition> sorties = CreateDefaultSessionSortieDefinitions();
        if (sorties.Count == 0)
        {
            message = "No sorties are configured.";
            lastAccountMessage = message;
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

        if (!ShipAssemblyBuilder.TryBuild(ActiveCatalog, progress, out ShipAssemblyResult assembly))
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
        EnsureSessionConfigLoaded();

        SortieZoneDefinition sortieZone = zone != null ? zone.Clone() : CreateDefaultSafeOreSortieDefinition();
        sortieZone.Normalize();
        if (!CanBeginSessionExtractionSortie(sortieZone, out string beginReason))
        {
            lastAccountMessage = beginReason;
            return false;
        }

        string launchDockId = progress.currentDockId;
        Vector3 launchDockPosition = progress.hasCurrentDockPosition
            ? progress.currentDockPosition
            : GetCurrentShipPosition();

        if (!TryEnterFlightForSessionSortie())
        {
            return false;
        }

        SortieEntryState entryState = BuildSortieEntryState(sortieZone, launchDockPosition, GetActiveShip());
        sortieZone.entryPosition = entryState.position;
        progress.BeginSortie(sortieZone, GetProcessUtcNow().Ticks, launchDockId, launchDockPosition);
        PlaceShipAtSortieEntry(sortieZone, entryState);

        lastAccountMessage = "Session sortie started: " + sortieZone.displayName + ".";
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
        if (CurrentMode != GameSessionMode.Flight || !HasActiveSortie)
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
        EnsureSessionConfigLoaded();
        message = "";

        if (!progress.HasActiveSortie)
        {
            message = "No active sortie.";
            lastAccountMessage = message;
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
            lastAccountMessage = message;
            return false;
        }

        string fuelId = string.IsNullOrWhiteSpace(profile.coalResourceId) ? GetStartingEngineFuelId() : profile.coalResourceId;
        string claudiumId = string.IsNullOrWhiteSpace(profile.claudiumResourceId) ? "claudium" : profile.claudiumResourceId;
        if (progress.shipEngineFuelTank.GetAmount(fuelId) + 0.001f < estimate.requiredCoalKg
            || progress.shipClaudiumTank.GetAmount(claudiumId) + 0.001f < estimate.requiredClaudiumKg)
        {
            message = estimate.status;
            lastAccountMessage = message;
            return false;
        }

        progress.shipEngineFuelTank.TrySpend(fuelId, estimate.requiredCoalKg);
        progress.shipClaudiumTank.TrySpend(claudiumId, estimate.requiredClaudiumKg);
        int transferred = TransferShipCargoToCapital();
        progress.ClearShipCargo();

        string baseDockId = GetCapitalPortId();
        progress.SetDocked(baseDockId, GetDockPositionOrFallback(baseDockId));
        SyncShipConsumablesWithCargo(true);
        ApplySessionModeToShip();

        message = "Extraction complete: transferred " + transferred
            + " cargo units to base. Return cost: "
            + estimate.requiredCoalKg.ToString("F0") + " kg coal, "
            + estimate.requiredClaudiumKg.ToString("F0") + " kg claudium.";
        lastAccountMessage = message;
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
        EnsureSessionConfigLoaded();

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        PortStorageState storage = IsDockedAtCapital() ? GetCapitalStorageState() : null;
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
        EnsureSessionConfigLoaded();

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
        EnsureSessionConfigLoaded();

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
        EnsureSessionConfigLoaded();

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        PortStorageState storage = GetCapitalStorageState();
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
        EnsureSessionConfigLoaded();

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
        EnsureSessionConfigLoaded();

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
        EnsureSessionConfigLoaded();
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
            lastAccountMessage = message;
            return false;
        }

        return processing
            ? TryUpgradeBaseProcessingBranch(branch, out message)
            : TryUpgradeCascadeProductionType(type, out message);
    }

    public bool CanUpgradeBaseProcessingBranch(BaseProcessingBranch branch, out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return CanUpgradeBaseProcessingBranchInternal(branch, out _, out _, out message);
    }

    public bool TryUpgradeBaseProcessingBranch(BaseProcessingBranch branch, out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        message = "";

        if (!CanUpgradeBaseProcessingBranchInternal(branch, out BaseProcessingLineState line, out List<CascadeItemAmount> cost, out message))
        {
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        if (!SpendBaseIndustryUpgradeCost(storage, cost, out message))
        {
            lastAccountMessage = message;
            return false;
        }

        int oldLevel = line.level;
        float oldCapacity = line.capacityUnitsPerMinute;
        line.level = oldLevel + 1;
        line.capacityUnitsPerMinute = GetUpgradedProcessingCapacity(branch, line.level, oldCapacity);
        RefreshRuntimeAccountIfDocked();

        message = "Upgraded processing " + SessionExtractionIndustry.GetProcessingDisplayName(branch)
            + " to L" + line.level
            + ": " + oldCapacity.ToString("F0") + " -> " + line.capacityUnitsPerMinute.ToString("F0") + "/m.";
        lastAccountMessage = message;
        return true;
    }

    public bool CanUpgradeCascadeProductionType(CascadeProductionType type, out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return CanUpgradeCascadeProductionTypeInternal(type, out _, out _, out message);
    }

    public bool TryUpgradeCascadeProductionType(CascadeProductionType type, out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        message = "";

        if (!CanUpgradeCascadeProductionTypeInternal(type, out CascadeProductionLineState line, out List<CascadeItemAmount> cost, out message))
        {
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        if (!SpendBaseIndustryUpgradeCost(storage, cost, out message))
        {
            lastAccountMessage = message;
            return false;
        }

        int oldLevel = line.level;
        float oldCapacity = line.capacityUnitsPerMinute;
        line.level = oldLevel + 1;
        line.capacityUnitsPerMinute = GetUpgradedCascadeCapacity(type, line.level, oldCapacity);
        RefreshRuntimeAccountIfDocked();

        message = "Upgraded cascade " + SessionExtractionIndustry.GetProductionDisplayName(type)
            + " to L" + line.level
            + ": " + oldCapacity.ToString("F0") + " -> " + line.capacityUnitsPerMinute.ToString("F0") + "/m.";
        lastAccountMessage = message;
        return true;
    }

    public bool CanRefuelBaseShip(out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Refuel is available only at the base.";
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            return false;
        }

        if (EnsureStarterPioneerRecoveryHullSelectedForCore())
        {
            ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out _);
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
        bool canFreePioneerRefuel = CanFreeRefuelStarterPioneerRecovery(fuelId, claudiumId, capacity);

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
        EnsureSessionConfigLoaded();
        message = "";

        if (!CanRefuelBaseShip(out message))
        {
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        CargoCapacityInfo capacity = CalculateCargoCapacity();
        ResolveCurrentTankResourceIds(out string fuelId, out string claudiumId);

        int fuelMoved = RefillTankFromStorage(progress.shipEngineFuelTank, fuelId, capacity.fuelTankCapacityKg, storage);
        int claudiumMoved = RefillTankFromStorage(progress.shipClaudiumTank, claudiumId, capacity.claudiumTankCapacityKg, storage);
        FreeRefuelStarterPioneerRecovery(fuelId, claudiumId, capacity, out float freeFuelKg, out float freeClaudiumKg);
        if (fuelMoved <= 0 && claudiumMoved <= 0 && freeFuelKg <= 0f && freeClaudiumKg <= 0f)
        {
            message = "Refuel blocked: no resources moved.";
            lastAccountMessage = message;
            return false;
        }

        SyncShipConsumablesWithCargo(true);
        RefreshRuntimeAccountIfDocked();

        message = "Refueled: " + fuelMoved + " kg " + fuelId
            + ", " + claudiumMoved + " kg " + claudiumId
            + (freeFuelKg > 0f || freeClaudiumKg > 0f
                ? " Pioneer free reserve: " + freeFuelKg.ToString("F0") + " kg " + fuelId
                    + ", " + freeClaudiumKg.ToString("F0") + " kg " + claudiumId + "."
                : ".");
        lastAccountMessage = message;
        return true;
    }

    private static int RefillTankFromStorage(ShipConsumableTankState tank, string resourceId, float capacityKg, PortStorageState storage)
    {
        if (tank == null || storage == null || string.IsNullOrWhiteSpace(resourceId) || capacityKg <= 0f) return 0;

        int available = storage.GetResourceAmount(resourceId);
        int needed = Mathf.FloorToInt(Mathf.Max(0f, capacityKg - tank.GetAmount(resourceId)));
        int moved = Mathf.Min(available, needed);
        if (moved <= 0 || !storage.TrySpendResource(resourceId, moved)) return 0;

        float added = tank.Add(resourceId, moved, capacityKg);
        int accepted = Mathf.FloorToInt(added + 0.001f);
        if (accepted < moved)
        {
            storage.AddResource(resourceId, moved - accepted);
        }

        return accepted;
    }

    private bool CanFreeRefuelStarterPioneerRecovery(string fuelId, string claudiumId, CargoCapacityInfo capacity)
    {
        if (progress == null || !IsDockedAtCapital() || !IsStarterPioneerRecoveryHullSelected())
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

    private void FreeRefuelStarterPioneerRecovery(string fuelId, string claudiumId, CargoCapacityInfo capacity, out float fuelAddedKg, out float claudiumAddedKg)
    {
        fuelAddedKg = 0f;
        claudiumAddedKg = 0f;
        if (!CanFreeRefuelStarterPioneerRecovery(fuelId, claudiumId, capacity))
        {
            return;
        }

        EnsureStarterPioneerRecoveryHullSelectedForCore();

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

    private bool IsStarterPioneerRecoveryHullSelected()
    {
        if (progress == null) return false;

        string selectedHullId = progress.selectedHullId ?? "";
        ShipPartDefinitionSO starterHull = ActiveCatalog != null ? ActiveCatalog.GetStarterHull() : null;
        string starterHullId = starterHull != null ? starterHull.partId : GameplaySessionAccountData.DefaultStarterHullId;
        if (string.IsNullOrWhiteSpace(selectedHullId)
            || selectedHullId == GameplaySessionAccountData.DefaultStarterHullId
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

    private bool EnsureStarterPioneerRecoveryHullSelectedForCore()
    {
        if (progress == null || ActiveCatalog == null)
        {
            return false;
        }

        if (!IsStarterPioneerRecoveryHullSelected())
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
        EnsureSessionConfigLoaded();
        message = "";

        if (!IsDockedAtCapital())
        {
            message = SessionExtractionIndustry.GetProcessingDisplayName(branch) + " processing is available only at the base.";
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            lastAccountMessage = message;
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
            lastAccountMessage = message;
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
        EnsureSessionConfigLoaded();

        if (!IsDockedAtCapital())
        {
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
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

    private bool TryProcessOreBaseBatch(PortStorageState storage, out string message)
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
        RefreshRuntimeAccountIfDocked();

        message = "Processed " + batchKg + " kg " + oreType.oreItemId + " into " + outputTotal + " kg minerals.";
        lastAccountMessage = message;
        return true;
    }

    private bool TryProcessGasBaseBatch(PortStorageState storage, out string message)
    {
        GasCondensateTypeConfig gasType = FindFirstStoredGasCondensateType(storage, out int availableCondensate);
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
        RefreshRuntimeAccountIfDocked();

        message = "Processed " + batch + " units " + gasType.condensateItemId + " into " + outputTotal + " gas materials.";
        lastAccountMessage = message;
        return true;
    }

    private bool TryProcessAutomatonBaseBatch(PortStorageState storage, out string message)
    {
        string inputItemId = FindFirstStoredAutomatonInput(storage, out int availableWrecks);
        if (string.IsNullOrWhiteSpace(inputItemId) || availableWrecks <= 0)
        {
            return FailBaseProcessing("No automaton wrecks in base storage.", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(BaseProcessingBranch.AutomatonDismantling);
        int batch = GetProcessingBatchSize(line, availableWrecks);
        if (!storage.TrySpendResource(inputItemId, batch))
        {
            return FailBaseProcessing("Could not spend automaton wrecks from base storage.", out message);
        }

        int outputTotal = 0;
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.MechanismsItemId, batch, 0.40f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.ToolsItemId, batch, 0.20f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.AutomatonCoreItemId, batch, 0.20f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.10f);

        line.totalProcessedUnits += batch;
        RefreshRuntimeAccountIfDocked();

        message = "Dismantled " + batch + " units " + inputItemId + " into " + outputTotal + " automaton outputs.";
        lastAccountMessage = message;
        return true;
    }

    private bool TryProcessLeviathanBaseBatch(PortStorageState storage, out string message)
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
        RefreshRuntimeAccountIfDocked();

        message = "Processed " + batch + " kg " + leviathanType.carcassItemId + " into " + outputTotal + " leviathan materials.";
        lastAccountMessage = message;
        return true;
    }

    private bool TryProcessCyberInfoBaseBatch(PortStorageState storage, out string message)
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
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.FundamentalExperienceItemId, batch, 0.85f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.15f);

        line.totalProcessedUnits += batch;
        RefreshRuntimeAccountIfDocked();

        message = "Deciphered " + batch + " units " + infoItemId + " into " + outputTotal + " research outputs.";
        lastAccountMessage = message;
        return true;
    }

    private bool FailBaseProcessing(string message, out string outputMessage)
    {
        outputMessage = message;
        lastAccountMessage = outputMessage;
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
        PortStorageState storage = IsDockedAtCapital() ? GetCapitalStorageState() : null;

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

    private static bool StarterCascadeOrderNeedsOutput(CascadeProductionOrderDefinition order, PortStorageState storage)
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
            lastAccountMessage = message;
            return false;
        }

        return TryRunBaseCascadeOrder(order, out message);
    }

    public CascadeProductionEstimate EstimateBaseCascadeOrder(CascadeProductionOrderDefinition order)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();

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
        if (!IsKnownSessionCoreCascadeOrder(order))
        {
            estimate.blockedReason = "Cascade order is not part of the session extraction base catalog.";
            return estimate;
        }

        if (!IsDockedAtCapital())
        {
            estimate.blockedReason = "Cascade production is available only at the base.";
            return estimate;
        }

        PortStorageState storage = GetCapitalStorageState();
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
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        if (storage == null)
        {
            message = "Base storage is missing.";
            lastAccountMessage = message;
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
                lastAccountMessage = message;
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
        RefreshRuntimeAccountIfDocked();

        message = "Cascade complete: " + BuildCascadeOutputsText(order)
            + ". Bottleneck: " + SessionExtractionIndustry.GetProductionDisplayName(estimate.bottleneck)
            + " ~" + estimate.bottleneckMinutes.ToString("F1") + " min.";
        lastAccountMessage = message;
        return true;
    }

    public bool CanLoadStarterMunitionsAtBase(out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        message = "";

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

        PortStorageState storage = GetCapitalStorageState();
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

        float addedMassKg = sessionConfig != null
            ? sessionConfig.GetItemTransportMassKg(SessionExtractionConstants.StarterWeaponCargoItemId, loadUnits)
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
        if (!CargoStoragePlanner.TryValidateCargoStorage(sessionConfig, capacity.cargoCompartments, cargoAfterLoad, out string storageError))
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
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        int loadUnits = GetStarterMunitionLoadUnits(progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId));
        if (storage == null || loadUnits <= 0 || !storage.TrySpendResource(SessionExtractionConstants.StarterMunitionBundleItemId, 1))
        {
            message = "Loadout blocked: could not spend "
                + SessionExtractionConstants.StarterMunitionBundleItemId + ".";
            lastAccountMessage = message;
            return false;
        }

        progress.AddShipCargo(SessionExtractionConstants.StarterWeaponCargoItemId, loadUnits);
        ApplyCargoMassToShip(GetActiveShip());
        RefreshRuntimeAccountIfDocked();

        message = "Loaded starter munitions: +" + loadUnits
            + " kg " + SessionExtractionConstants.StarterWeaponCargoItemId + ".";
        lastAccountMessage = message;
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

        PortStorageState storage = GetCapitalStorageState();
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

        PortStorageState storage = GetCapitalStorageState();
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
        EnsureSessionConfigLoaded();
        if (progress == null)
        {
            blockedReason = "Progress is missing.";
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        PortStorageState storage = IsDockedAtCapital() ? GetCapitalStorageState() : null;

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

    private static bool CanSpendBaseIndustryUpgradeCost(PortStorageState storage, List<CascadeItemAmount> cost, out string blockedReason)
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

    private static bool SpendBaseIndustryUpgradeCost(PortStorageState storage, List<CascadeItemAmount> cost, out string message)
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
        EnsureSessionConfigLoaded();
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

    public bool CanInstallStarterGasExtractorUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterGasExtractorModuleId,
            SessionExtractionConstants.StarterHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter gas extractor is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterGasExtractorUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterGasExtractorModuleId,
            SessionExtractionConstants.StarterHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter gas extractor is already installed.",
            "Installed High gas extractor",
            out message);
    }

    public bool CanInstallStarterMiningHoldUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
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
            "Installed High impact wreck collector",
            out message);
    }

    public bool CanInstallStarterLeviathanSalvageUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return ResolveStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterLeviathanSalvageModuleId,
            SessionExtractionConstants.StarterThirdHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter leviathan salvage rig is already installed.",
            out _, out _, out _, out message);
    }

    public bool TryInstallStarterLeviathanSalvageUpgrade(out string message)
    {
        return TryInstallStarterFittingUpgrade(
            SessionExtractionConstants.StarterModuleKitItemId,
            SessionExtractionConstants.StarterLeviathanSalvageModuleId,
            SessionExtractionConstants.StarterThirdHighSlotId,
            SessionExtractionConstants.HighSlotTypeId,
            "Starter leviathan salvage rig is already installed.",
            "Installed High leviathan salvage rig",
            out message);
    }

    public bool CanInstallStarterObservationUpgrade(out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
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
        if (CanInstallStarterGasExtractorUpgrade(out message)) return true;
        if (CanInstallStarterMiningHoldUpgrade(out message)) return true;
        if (CanInstallStarterLeviathanSalvageUpgrade(out message)) return true;
        if (CanInstallStarterObservationUpgrade(out message)) return true;
        return false;
    }

    public bool TryInstallNextStarterFittingUpgrade(out string message)
    {
        if (CanInstallStarterCargoRackUpgrade(out _)) return TryInstallStarterCargoRackUpgrade(out message);
        if (CanInstallStarterGasExtractorUpgrade(out _)) return TryInstallStarterGasExtractorUpgrade(out message);
        if (CanInstallStarterMiningHoldUpgrade(out _)) return TryInstallStarterMiningHoldUpgrade(out message);
        if (CanInstallStarterLeviathanSalvageUpgrade(out _)) return TryInstallStarterLeviathanSalvageUpgrade(out message);
        if (CanInstallStarterObservationUpgrade(out _)) return TryInstallStarterObservationUpgrade(out message);

        message = "No starter fitting upgrade is currently installable.";
        lastAccountMessage = message;
        return false;
    }

    public string GetNextStarterFittingUpgradeActionLabel()
    {
        if (CanInstallStarterCargoRackUpgrade(out _)) return "Install Low rack";
        if (CanInstallStarterGasExtractorUpgrade(out _)) return "Install High gas";
        if (CanInstallStarterMiningHoldUpgrade(out _)) return "Install High wreck";
        if (CanInstallStarterLeviathanSalvageUpgrade(out _)) return "Install High salvage";
        if (CanInstallStarterObservationUpgrade(out _)) return "Install Mid scout";
        return "Install module";
    }

    public bool AutoInstallRequiredModules(bool applyToRuntime, out string message)
    {
        EnsureProgressInitialized();

        if (!ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out message))
        {
            return false;
        }

        if (applyToRuntime)
        {
            ApplySelectedShip();
            RefreshRuntimeAccountIfDocked();
            lastAccountMessage = message;
        }

        return true;
    }

    public bool DockAt(string dockId)
    {
        EnsureProgressInitialized();

        if (progress.HasActiveSortie)
        {
            lastAccountMessage = "Docking is disabled during a sortie. Reach the sortie boundary and extract home.";
            return false;
        }
        if (!IsCapitalPort(dockId))
        {
            lastAccountMessage = "Non-base docks are disabled. The base is the only home dock.";
            return false;
        }

        progress.SetDocked(dockId, GetCurrentShipPosition());
        ApplySessionModeToShip();

        return true;
    }

    public bool LoseShipAndReturnToCity(string reason = "")
    {
        EnsureProgressInitialized();

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
        ApplyStartingTechnologies();

        string assemblyMessage = "";
        bool assemblyReady = ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out assemblyMessage);
        string recoveryDockId = GetCapitalPortId();
        Vector3 recoveryPosition = GetDockPositionOrFallback(recoveryDockId);

        progress.SetDocked(recoveryDockId, recoveryPosition);
        ApplySelectedShip();
        ApplySessionModeToShip();
        ResetCrashDetector();

        lastAccountMessage = "Корабль потерян. Возврат в город, выдан стартовый корабль.";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            lastAccountMessage += " Причина: " + reason + ".";
        }

        if (!assemblyReady && !string.IsNullOrWhiteSpace(assemblyMessage))
        {
            lastAccountMessage += " " + assemblyMessage;
        }

        return assemblyReady;
    }

    public int AdvanceRealTimeProcesses(DateTime utcNow)
    {
        if (isAdvancingProcesses || progress == null) return 0;

        isAdvancingProcesses = true;
        int completedCycles = 0;

        try
        {
            progress.Normalize();
            EnsureSessionConfigLoaded();
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

            completedCycles += AdvanceTechnologyResearch(utcNow);

            progress.lastProcessUtcTicks = utcNow.Ticks;
        }
        finally
        {
            isAdvancingProcesses = false;
        }

        return completedCycles;
    }

    public MetaGameAccountData CreateRuntimeAccountData()
    {
        EnsureProgressInitialized();
        WildWindGameplaySession gameplaySession = WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(this, RuntimeAccountId);
        RefreshRuntimeAccountState();

        return new MetaGameAccountData
        {
            version = MetaGameAccountData.CurrentVersion,
            progress = progress,
            gameplaySession = CreateRuntimeGameplaySessionData(gameplaySession)
        };
    }

    private void RefreshRuntimeAccountState()
    {
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

        if (progress.lastProcessUtcTicks <= 0)
        {
            progress.lastProcessUtcTicks = now.Ticks;
        }
        progress.Normalize();
    }

    private GameplaySessionAccountData CreateRuntimeGameplaySessionData(WildWindGameplaySession gameplaySession)
    {
        if (gameplaySession != null)
        {
            return gameplaySession.CreateAccountData();
        }

        return GameplaySessionAccountData.CreateInitial(RuntimeAccountId, progress);
    }

    public bool ResetAccountProgressForCheat(out string message)
    {
        progress = new PlayerProgress();
        initialized = false;
        EnsureProgressInitialized();
        SpawnConfiguredSessionActors(true);
        ApplySelectedShip();
        ApplySessionModeToShip();

        WildWindGameplaySession gameplaySession = WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(this, RuntimeAccountId);
        if (gameplaySession != null)
        {
            gameplaySession.ApplyAccountData(GameplaySessionAccountData.CreateInitial(RuntimeAccountId, progress));
        }

        lastAccountMessage = "Account progress reset.";
        message = lastAccountMessage;
        return true;
    }
    private int AdvanceTechnologyResearch(DateTime utcNow)
    {
        if (progress == null || sessionConfig == null || !sessionConfig.isLoaded) return 0;
        if (string.IsNullOrWhiteSpace(progress.activeResearchTechnologyId)) return 0;

        TechnologyConfig technology = sessionConfig.GetTechnology(progress.activeResearchTechnologyId);
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
        PortStorageState storage = progress.GetPortStorageState(GetCapitalPortId(), true);
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
        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        state.completedCycles = Mathf.Max(state.completedCycles, Mathf.Max(1, technology.requiredCycles));
        state.activeCycleStartUtcTicks = 0;
        state.activeCycleEndUtcTicks = 0;

        if (progress.activeResearchTechnologyId == technology.id)
        {
            progress.activeResearchTechnologyId = "";
        }

        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out _);
        ApplySelectedShip();
        lastAccountMessage = "Технология завершена: " + GetTechnologyDisplayName(technology);
    }

    private bool TryStartOrContinueResearchCycle(TechnologyConfig technology, TechnologyResearchProgress state, long startTicks, out string reason)
    {
        reason = "";
        if (technology == null || state == null) return false;
        if (state.HasActiveCycle) return true;

        PortStorageState storage = progress.GetPortStorageState(GetCapitalPortId(), true);
        return TryStartTechnologyCycle(technology, state, storage, startTicks, out reason);
    }

    private bool TryStartTechnologyCycle(TechnologyConfig technology, TechnologyResearchProgress state, PortStorageState storage, long startTicks, out string reason)
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
                reason = "Нужна технология: " + sessionConfig.GetTechnologyNameRu(prerequisiteId);
                return false;
            }
        }

        return true;
    }

    private bool HasTechnologyCycleCost(PortStorageState storage, TechnologyConfig technology)
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

    private bool TrySpendTechnologyCycleCost(PortStorageState storage, TechnologyConfig technology, out string reason)
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
        if (!HasActiveSortie)
        {
            reason = "Runtime cargo collection is available only during an active core sortie.";
            return false;
        }
        if (!CanCollectActiveSortieResource(resourceId, out reason))
        {
            return false;
        }

        CargoCapacityInfo capacity = CalculateCargoCapacity();
        float addedMassKg = sessionConfig != null ? sessionConfig.GetItemTransportMassKg(resourceId, amount) : amount;
        float freeKg = Mathf.Max(0f, capacity.maxCargoKg - capacity.currentCargoKg - capacity.currentTankKg);
        if (!capacity.assemblyValid || freeKg + 0.001f < addedMassKg)
        {
            reason = $"Не хватает грузоподъемности: нужно {addedMassKg:F1} кг, свободно {freeKg:F1} кг.";
            return false;
        }

        Dictionary<string, int> cargoAfterAdd = CargoStoragePlanner.ToCargoMap(progress.shipCargo);
        cargoAfterAdd[resourceId] = cargoAfterAdd.TryGetValue(resourceId, out int current) ? current + amount : amount;
        if (!CargoStoragePlanner.TryValidateCargoStorage(sessionConfig, capacity.cargoCompartments, cargoAfterAdd, out string storageError))
        {
            reason = storageError;
            return false;
        }

        progress.AddShipCargo(resourceId, amount);
        ApplyCargoMassToShip(GetActiveShip());
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
        string resourceName = sessionConfig != null ? sessionConfig.GetItemNameRu(resourceId) : resourceId;
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
        }

        return true;
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
        EnsureSessionConfigLoaded();

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
        ItemConfig fuel = sessionConfig != null ? sessionConfig.GetItem(fuelId) : null;
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
            cargoMassKg = progress.GetShipPayloadMassKg(sessionConfig),
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

        PortStorageState capitalStorage = GetCapitalStorageState();
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

    private OreTypeConfig FindFirstStoredOreType(PortStorageState storage, out int availableOre)
    {
        availableOre = 0;
        if (storage == null || sessionConfig == null || sessionConfig.oreTypes == null) return null;

        for (int i = 0; i < sessionConfig.oreTypes.Count; i++)
        {
            OreTypeConfig oreType = sessionConfig.oreTypes[i];
            if (oreType == null || string.IsNullOrWhiteSpace(oreType.oreItemId)) continue;

            int amount = storage.GetResourceAmount(oreType.oreItemId);
            if (amount <= 0) continue;

            availableOre = amount;
            return oreType;
        }

        return null;
    }

    private GasCondensateTypeConfig FindFirstStoredGasCondensateType(PortStorageState storage, out int availableCondensate)
    {
        availableCondensate = 0;
        if (storage == null || sessionConfig == null || sessionConfig.gasCondensateTypes == null) return null;

        for (int i = 0; i < sessionConfig.gasCondensateTypes.Count; i++)
        {
            GasCondensateTypeConfig gasType = sessionConfig.gasCondensateTypes[i];
            if (gasType == null || string.IsNullOrWhiteSpace(gasType.condensateItemId)) continue;

            int amount = storage.GetResourceAmount(gasType.condensateItemId);
            if (amount <= 0) continue;

            availableCondensate = amount;
            return gasType;
        }

        return null;
    }

    private LeviathanTypeConfig FindFirstStoredLeviathanType(PortStorageState storage, out int availableCarcass)
    {
        availableCarcass = 0;
        if (storage == null || sessionConfig == null || sessionConfig.leviathanTypes == null) return null;

        for (int i = 0; i < sessionConfig.leviathanTypes.Count; i++)
        {
            LeviathanTypeConfig leviathanType = sessionConfig.leviathanTypes[i];
            if (leviathanType == null || string.IsNullOrWhiteSpace(leviathanType.carcassItemId)) continue;

            int amount = storage.GetResourceAmount(leviathanType.carcassItemId);
            if (amount <= 0) continue;

            availableCarcass = amount;
            return leviathanType;
        }

        return null;
    }

    private static string FindFirstStoredAutomatonInput(PortStorageState storage, out int availableWrecks)
    {
        availableWrecks = 0;
        if (storage == null) return "";

        int brokenAutomatons = storage.GetResourceAmount(SessionExtractionConstants.BrokenAutomatonItemId);
        if (brokenAutomatons > 0)
        {
            availableWrecks = brokenAutomatons;
            return SessionExtractionConstants.BrokenAutomatonItemId;
        }

        return "";
    }

    private static string FindFirstStoredCyberInfoInput(PortStorageState storage, out int availableInfo)
    {
        availableInfo = 0;
        if (storage == null) return "";

        int rockInfo = storage.GetResourceAmount(SessionExtractionConstants.RockInfoItemId);
        if (rockInfo > 0)
        {
            availableInfo = rockInfo;
            return SessionExtractionConstants.RockInfoItemId;
        }

        return "";
    }

    private bool TryGetAvailableBaseProcessingInput(BaseProcessingBranch branch, PortStorageState storage, out string inputItemId, out int available)
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
                GasCondensateTypeConfig gasType = FindFirstStoredGasCondensateType(storage, out available);
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

    private static int AddOreProcessingOutputs(PortStorageState storage, OreTypeConfig oreType, int batchKg)
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

    private static int AddGasProcessingOutputs(PortStorageState storage, GasCondensateTypeConfig gasType, int batch)
    {
        if (storage == null || gasType == null || gasType.composition == null || batch <= 0) return 0;

        int totalOutput = 0;
        for (int i = 0; i < gasType.composition.Count; i++)
        {
            GasCondensateCompositionConfig composition = gasType.composition[i];
            if (composition == null || string.IsNullOrWhiteSpace(composition.itemId) || composition.share <= 0f) continue;

            totalOutput += AddScaledProcessingOutput(storage, composition.itemId, batch, composition.share);
        }

        return totalOutput;
    }

    private static int AddScaledProcessingOutput(PortStorageState storage, string itemId, int inputAmount, float share)
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
        EnsureSessionConfigLoaded();
        message = "";

        if (!ResolveStarterFittingUpgrade(
                kitItemId,
                moduleId,
                preferredSlotId,
                slotTypeId,
                alreadyInstalledMessage,
                out PortStorageState storage,
                out ShipPartDefinitionSO module,
                out ShipSlotDefinition slot,
                out message))
        {
            lastAccountMessage = message;
            return false;
        }

        if (!storage.TrySpendResource(kitItemId, 1))
        {
            message = "Upgrade blocked: could not spend " + kitItemId + ".";
            lastAccountMessage = message;
            return false;
        }

        progress.InstallModule(slot.slotId, module.partId);
        ApplySelectedShip();
        RefreshRuntimeAccountIfDocked();

        message = successMessagePrefix + ": " + GetPartName(module) + ".";
        lastAccountMessage = message;
        return true;
    }

    private bool ResolveStarterFittingUpgrade(
        string kitItemId,
        string moduleId,
        string preferredSlotId,
        string slotTypeId,
        string alreadyInstalledMessage,
        out PortStorageState storage,
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

        if (!ShipAssemblyBuilder.IsPartUsable(module, progress))
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
            currentCargoKg = progress != null ? progress.GetShipCargoMassKg(sessionConfig) : 0f,
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

        if (!ShipAssemblyBuilder.TryBuild(activeCatalog, progress, out ShipAssemblyResult result))
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

        if (!CargoStoragePlanner.TryValidateCargoStorage(sessionConfig, info.cargoCompartments, progress != null ? progress.shipCargo : null, out string storageError))
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
        ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(sessionConfig));
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
            ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(sessionConfig));
            ship.RefreshRuntimeShipSettings();
            return;
        }

        ApplyFuelConfigToShip(ship);
        SyncTankResourceFromRuntime(progress.shipEngineFuelTank, ship.engineFuelId, ref syncedFuelResourceId, ref ship.engineFuelStockKg);
        string claudiumResourceId = GetClaudiumResourceId(ship);
        SyncTankResourceFromRuntime(progress.shipClaudiumTank, claudiumResourceId, ref syncedClaudiumResourceId, ref ship.claudiumStock);

        ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(sessionConfig));
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

        EnsureSessionConfigLoaded();
        ItemConfig fuel = sessionConfig != null ? sessionConfig.GetItem(ship.engineFuelId) : null;
        if (fuel != null)
        {
            ship.engineFuelEnergyKwhPerKg = Mathf.Max(0f, fuel.energyKwhPerKg);
        }
    }

    private void ApplyStartingTechnologies()
    {
        if (sessionConfig == null || !sessionConfig.isLoaded || progress == null) return;

        for (int i = 0; i < sessionConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = sessionConfig.technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) continue;
            if (technology.prerequisiteTechnologyIds != null && technology.prerequisiteTechnologyIds.Count > 0) continue;
            if (technology.cycleCost != null && technology.cycleCost.Count > 0) continue;
            if (technology.cycleTimeSeconds > 0) continue;

            progress.CompleteTechnology(technology.id);
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

        DockingPort[] dockingPorts = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        for (int i = 0; i < dockingPorts.Length; i++)
        {
            DockingPort dock = dockingPorts[i];
            if (dock == null) continue;

            dock.targetShip = ship;
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

        ship.enabled = !docked;
        if (docked)
        {
            ship.thrustInput = 0f;
            ship.turnInput = 0f;
            ship.liftInput = 0f;
            ship.cruiseControl = false;
            ship.altitudeHold = false;
            ship.headingHold = false;
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

    private Vector3 GetDockPositionOrFallback(string dockId)
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

        EnsureSessionConfigLoaded();
        if (sessionConfig != null)
        {
            PortConfig port = sessionConfig.GetPort(dockId);
            if (port != null)
            {
                return port.position;
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

    private void RefreshRuntimeAccountIfDocked()
    {
        if (IsDocked)
        {
            RememberCurrentDockPosition();
        }
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


}
