using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

[Serializable]
public class MetaGameProgressSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string runtimeAccountId = GameplaySessionAccountData.DefaultAccountId;
    public long savedUtcTicks;
    public PlayerProgress progress = new PlayerProgress();
}

public partial class MetaGameState : MonoBehaviour
{
    private const float ExtractionRunupOutwardDotThreshold = 0.9659258f;
    private const float SortieEntryApproachSeconds = 20f;
    private const float SortieEntryDefaultMaxSpeedMS = 120f;
    private const string Cruiser203HullId = "cruiser203_hull";
    private const string PersistentProgressSaveFileName = "wildwind_meta_progress_v1.json";
    private const string PersistentProgressBigTestSaveFileName = "wildwind_meta_progress_bigtest_v1.json";
    private const float PersistentProgressAutosaveIntervalSeconds = 1f;
    private const int BaseCascadeQueueSlotCount = 5;
    public const int CourierOrderSlotCount = 8;
    public const int CourierCancelCooldownSeconds = 15 * 60;
    private const int KnowledgeMaxLevel = 5;
    private const float BaseArchiveKnowledgeSpPerMinute = 3f;
    private const string CourierFreightRewardItemId = "freight";
    private const string CourierExperienceRewardItemId = SessionExtractionConstants.DesignExperienceItemId;
    private static readonly string[] CourierClientNames =
    {
        "Маяк Северный",
        "Караванная пристань",
        "Дом Вихря",
        "Лоцманский двор",
        "Бирюзовый причал",
        "Штормовая почта",
        "Станция Гребня",
        "Южный буй"
    };
    private static readonly CourierResourceSpec[] CourierResourceCatalog =
    {
        new CourierResourceSpec("windshale_ore", 18, 34, 3),
        new CourierResourceSpec("dawnspar_ore", 12, 24, 4),
        new CourierResourceSpec("cloud_condensate", 18, 36, 4),
        new CourierResourceSpec(SessionExtractionConstants.BrokenAutomatonItemId, 8, 18, 7),
        new CourierResourceSpec("windcalf_carcass", 10, 22, 6),
        new CourierResourceSpec(SessionExtractionConstants.RockInfoItemId, 8, 16, 9),
        new CourierResourceSpec("ferron", 6, 14, 12),
        new CourierResourceSpec("silvate", 4, 10, 18),
        new CourierResourceSpec("cloth", 8, 18, 10),
        new CourierResourceSpec("tools", 3, 8, 26),
        new CourierResourceSpec("charcoal", 12, 28, 4),
        new CourierResourceSpec("claudium", 6, 14, 16),
        new CourierResourceSpec("metal", 6, 16, 12),
        new CourierResourceSpec("mechanisms", 3, 8, 30)
    };

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
    [Tooltip("Single runtime account identifier saved with the persistent meta progress file.")]
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
    private string lastPersistentProgressFingerprint = "";
    private float nextPersistentProgressAutosaveTime;
    private bool persistentProgressLoadAttempted;
    private bool persistentProgressLoadedThisSession;
    private bool useBigTestPersistentProgressFile;

    private ShipCatalogSO ActiveCatalog => catalog != null ? catalog : shipLoader != null ? shipLoader.catalog : null;
    public SessionConfigDatabase SessionConfig => sessionConfig;
    public ShipCatalogSO CurrentCatalog => ActiveCatalog;
    public int SpawnedConfiguredPortCount => spawnedConfigPortRoot != null ? spawnedConfigPortRoot.childCount : 0;
    public string PersistentProgressSavePathForTests => ResolvePersistentProgressSavePath();
    public bool HasPersistentProgressSaveForTests => File.Exists(ResolvePersistentProgressSavePath());
    public bool PersistentProgressLoadedThisSessionForTests => persistentProgressLoadedThisSession;
    public bool IsUsingBigTestPersistentProgressForTests => IsUsingBigTestPersistentProgress();
    public static bool IsBigTestProgressSandboxActiveForTests => ShouldUseBigTestProgressSaveFile();

    public IReadOnlyList<QuestDefinitionConfig> GetQuestDefinitions()
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return sessionConfig != null ? sessionConfig.questDefinitions : null;
    }

    public IReadOnlyList<QuestState> GetQuestStates()
    {
        EnsureProgressInitialized();
        RefreshQuestProgress(false);
        return progress.questStates;
    }

    public QuestState GetQuestState(string questId)
    {
        EnsureProgressInitialized();
        RefreshQuestProgress(false);
        return progress.GetQuestState(questId, false);
    }

    public int GetQuestCompletedCountForTests()
    {
        EnsureProgressInitialized();
        RefreshQuestProgress(false);
        int count = 0;
        if (progress.questStates == null) return 0;
        for (int i = 0; i < progress.questStates.Count; i++)
        {
            QuestState state = progress.questStates[i];
            if (state != null && state.completed) count++;
        }

        return count;
    }

    public int GetQuestClaimedCountForTests()
    {
        EnsureProgressInitialized();
        RefreshQuestProgress(false);
        int count = 0;
        if (progress.questStates == null) return 0;
        for (int i = 0; i < progress.questStates.Count; i++)
        {
            QuestState state = progress.questStates[i];
            if (state != null && state.claimed) count++;
        }

        return count;
    }

    public bool RecordQuestEventForTests(string objectiveType, string targetId, int amount = 1)
    {
        return RecordQuestEvent(objectiveType, targetId, amount);
    }

    public int RefreshQuestProgressForTests(bool allowAutoClaim = true)
    {
        EnsureProgressInitialized();
        return RefreshQuestProgress(allowAutoClaim);
    }

    public bool TryClaimQuestReward(string questId, out string message)
    {
        message = "";
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        RefreshQuestProgress(false);

        QuestDefinitionConfig quest = sessionConfig != null ? sessionConfig.GetQuestDefinition(questId) : null;
        QuestState state = progress.GetQuestState(questId, false);
        if (quest == null || state == null || !state.completed)
        {
            message = "Квест не готов к награде.";
            lastAccountMessage = message;
            return false;
        }

        if (state.claimed)
        {
            message = "Награда уже получена.";
            lastAccountMessage = message;
            return false;
        }

        ClaimQuestReward(quest, state, false);
        message = "Награда за квест получена: " + quest.DisplayNameRu + ".";
        lastAccountMessage = message;
        return true;
    }

    private bool RecordQuestEvent(string objectiveType, string targetId, int amount = 1)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        if (progress == null || amount <= 0 || string.IsNullOrWhiteSpace(objectiveType))
        {
            return false;
        }

        AddQuestEventMetric(objectiveType, targetId, amount);
        int changed = RefreshQuestProgress(true);
        MarkPersistentProgressDirty();
        return changed > 0;
    }

    private void AddQuestEventMetric(string objectiveType, string targetId, int amount)
    {
        if (progress == null || amount <= 0 || string.IsNullOrWhiteSpace(objectiveType))
        {
            return;
        }

        string metricType = NormalizeQuestKey(objectiveType);
        string metricTarget = NormalizeQuestKey(targetId);
        progress.AddQuestMetric(metricType, metricTarget, amount);
        if (metricTarget != "any")
        {
            progress.AddQuestMetric(metricType, "any", amount);
        }
    }

    private PortStorageState GetCapitalStorageStateForQuestRuntime()
    {
        progress ??= new PlayerProgress();
        progress.Normalize();
        return progress.GetPortStorageState(GetCapitalPortId(), true);
    }

    private int RefreshQuestProgress(bool allowAutoClaim)
    {
        if (progress == null)
        {
            return 0;
        }

        EnsureSessionConfigLoaded();
        if (sessionConfig == null || sessionConfig.questDefinitions == null || sessionConfig.questDefinitions.Count == 0)
        {
            return 0;
        }

        progress.Normalize();
        int changed = 0;
        for (int pass = 0; pass < 4; pass++)
        {
            bool changedThisPass = false;
            for (int i = 0; i < sessionConfig.questDefinitions.Count; i++)
            {
                QuestDefinitionConfig quest = sessionConfig.questDefinitions[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.id) || !IsQuestAvailable(quest))
                {
                    continue;
                }

                QuestState state = progress.GetQuestState(quest.id, true);
                state.targetAmount = Mathf.Max(1, quest.targetAmount);
                if (!state.accepted)
                {
                    state.accepted = true;
                    state.acceptedUtcTicks = DateTime.UtcNow.Ticks;
                    state.baselineAmount = quest.IsRetroactive ? 0 : GetQuestObjectiveRawAmount(quest);
                    changed++;
                    changedThisPass = true;
                }

                int rawAmount = GetQuestObjectiveRawAmount(quest);
                int amount = Mathf.Max(0, rawAmount - state.baselineAmount);
                int clamped = Mathf.Min(state.targetAmount, amount);
                if (state.currentAmount != clamped)
                {
                    state.currentAmount = clamped;
                    changed++;
                    changedThisPass = true;
                }

                if (!state.completed && state.currentAmount >= state.targetAmount)
                {
                    state.completed = true;
                    state.completedUtcTicks = DateTime.UtcNow.Ticks;
                    changed++;
                    changedThisPass = true;
                }

                if (allowAutoClaim && state.completed && !state.claimed && quest.IsAutoClaim)
                {
                    ClaimQuestReward(quest, state, true);
                    changed++;
                    changedThisPass = true;
                }
            }

            if (!changedThisPass)
            {
                break;
            }
        }

        if (changed > 0)
        {
            MarkPersistentProgressDirty();
        }

        return changed;
    }

    private bool IsQuestAvailable(QuestDefinitionConfig quest)
    {
        if (quest == null) return false;
        if (string.IsNullOrWhiteSpace(quest.requiredQuestId)) return true;

        QuestState required = progress.GetQuestState(quest.requiredQuestId, false);
        return required != null && required.completed;
    }

    private void ClaimQuestReward(QuestDefinitionConfig quest, QuestState state, bool automatic)
    {
        if (quest == null || state == null || state.claimed)
        {
            return;
        }

        state.claimed = true;
        state.completed = true;
        state.claimedUtcTicks = DateTime.UtcNow.Ticks;

        if (!string.IsNullOrWhiteSpace(quest.rewardItemId) && quest.rewardAmount > 0)
        {
            PortStorageState storage = GetCapitalStorageStateForQuestRuntime();
            storage?.AddResource(quest.rewardItemId, quest.rewardAmount);
            progress.AddQuestMetric("resource_acquired", quest.rewardItemId, quest.rewardAmount);
            progress.AddQuestMetric("resource_acquired", "any", quest.rewardAmount);
        }

        lastAccountMessage = (automatic ? "Квест выполнен: " : "Награда получена: ") + quest.DisplayNameRu + ".";
        MarkPersistentProgressDirty();
    }

    private int GetQuestObjectiveRawAmount(QuestDefinitionConfig quest)
    {
        if (quest == null) return 0;

        string type = NormalizeQuestKey(quest.objectiveType);
        string target = NormalizeQuestKey(quest.targetId);
        switch (type)
        {
            case "resource_owned":
                return GetCapitalStorageStateForQuestRuntime()?.GetResourceAmount(target) ?? 0;
            case "resource_acquired":
            case "resource_spent":
            case "courier_sent":
            case "courier_cancelled":
            case "technology_started":
            case "cascade_order_completed":
            case "sortie_completed":
            case "event":
                return GetQuestMetricValue(type, target);
            case "technology_completed":
                return GetTechnologyCompletedQuestAmount(target);
            case "base_processing_level":
                return GetBaseProcessingQuestLevel(target);
            case "cascade_line_level":
                return GetCascadeProductionQuestLevel(target);
            case "ship_module_installed":
                return GetInstalledModuleQuestAmount(target);
            case "ship_hull_selected":
                return target == "any" || NormalizeQuestKey(progress.selectedHullId) == target ? 1 : 0;
            default:
                return GetQuestMetricValue(type, target);
        }
    }

    private int GetQuestMetricValue(string objectiveType, string targetId)
    {
        if (progress == null) return 0;
        string type = NormalizeQuestKey(objectiveType);
        string target = NormalizeQuestKey(targetId);
        return target == "any"
            ? progress.GetQuestMetricValue(type, "any")
            : progress.GetQuestMetricValue(type, target);
    }

    private int GetTechnologyCompletedQuestAmount(string targetId)
    {
        if (progress == null || progress.completedTechnologyIds == null)
        {
            return 0;
        }

        string target = NormalizeQuestKey(targetId);
        if (target == "any")
        {
            return progress.completedTechnologyIds.Count;
        }

        return progress.IsTechnologyCompleted(targetId) || progress.IsTechnologyCompleted(target) ? 1 : 0;
    }

    private int GetBaseProcessingQuestLevel(string targetId)
    {
        if (progress == null || progress.baseIndustry == null)
        {
            return 0;
        }

        if (!TryParseBaseProcessingBranch(targetId, out BaseProcessingBranch branch))
        {
            return 0;
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(branch);
        return line != null ? Mathf.Max(0, line.level) : 0;
    }

    private int GetCascadeProductionQuestLevel(string targetId)
    {
        if (progress == null || progress.baseIndustry == null)
        {
            return 0;
        }

        if (!TryParseCascadeProductionType(targetId, out CascadeProductionType type))
        {
            return 0;
        }

        CascadeProductionLineState line = progress.baseIndustry.GetProduction(type);
        return line != null ? Mathf.Max(0, line.level) : 0;
    }

    private int GetInstalledModuleQuestAmount(string targetId)
    {
        if (progress == null || progress.installedModules == null)
        {
            return 0;
        }

        string target = NormalizeQuestKey(targetId);
        int count = 0;
        for (int i = 0; i < progress.installedModules.Count; i++)
        {
            InstalledModuleState module = progress.installedModules[i];
            if (module == null || string.IsNullOrWhiteSpace(module.moduleId)) continue;
            if (target == "any" || NormalizeQuestKey(module.moduleId) == target)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryParseBaseProcessingBranch(string targetId, out BaseProcessingBranch branch)
    {
        string normalized = NormalizeQuestKey(targetId).Replace("_", "");
        foreach (BaseProcessingBranch value in Enum.GetValues(typeof(BaseProcessingBranch)))
        {
            if (NormalizeQuestKey(value.ToString()).Replace("_", "") == normalized)
            {
                branch = value;
                return true;
            }
        }

        branch = default;
        return false;
    }

    private static bool TryParseCascadeProductionType(string targetId, out CascadeProductionType type)
    {
        string normalized = NormalizeQuestKey(targetId).Replace("_", "");
        foreach (CascadeProductionType value in Enum.GetValues(typeof(CascadeProductionType)))
        {
            if (NormalizeQuestKey(value.ToString()).Replace("_", "") == normalized)
            {
                type = value;
                return true;
            }
        }

        type = default;
        return false;
    }

    private static string NormalizeQuestKey(string value)
    {
        return PlayerProgress.NormalizeQuestMetricPart(value);
    }

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
        public float liftCapacityKg;
        public float claudiumMaxLiftKg;
        public float hullLimitKg;
        public List<CargoCompartmentDefinition> cargoCompartments;
    }

    private readonly struct ProcessingOutputShare
    {
        public readonly string itemId;
        public readonly float share;

        public ProcessingOutputShare(string itemId, float share)
        {
            this.itemId = itemId ?? "";
            this.share = Mathf.Max(0f, share);
        }
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
        useBigTestPersistentProgressFile = ShouldUseBigTestProgressSaveFile();
        TryLoadPersistentProgressOnAwake();

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
            AutoSavePersistentProgressIfNeeded();
            return;
        }

        if (processRealTimeWhilePlaying)
        {
            EnsureProgressInitialized();
            AdvanceScaledRealTimeProcesses();
        }

        SyncShipConsumablesWithCargo(false);
        UpdateActiveSortieExtractionRunupFromActiveShip();
        AutoSavePersistentProgressIfNeeded();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SavePersistentProgressNow();
        }
    }

    private void OnApplicationQuit()
    {
        SavePersistentProgressNow();
        RestoreUnityTimeSettings();
    }

    private void OnDisable()
    {
        SavePersistentProgressNow();
        RestoreUnityTimeSettings();
    }

    public void EnsureProgressInitialized()
    {
        progress ??= new PlayerProgress();
        progress.Normalize();
        EnsureSessionConfigLoaded();
        EnsureEconomyRuntimeStates();

        if (EnsureStartingCourierSuppliesGranted())
        {
            MarkPersistentProgressDirty();
        }

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

        if (!progress.receivedStartingProcessingSamples)
        {
            AddStartingBaseProcessingSamples();
            progress.receivedStartingProcessingSamples = true;
        }

        if (!progress.receivedStartingExpansionCurrency)
        {
            AddStartingBaseExpansionCurrency();
            progress.receivedStartingExpansionCurrency = true;
        }

        long nowTicks = DateTime.UtcNow.Ticks;
        if (progress.lastProcessUtcTicks == 0)
        {
            progress.lastProcessUtcTicks = nowTicks;
        }

        EnsureCourierServiceReady(progress.lastProcessUtcTicks > 0 ? progress.lastProcessUtcTicks : nowTicks);
        ApplyStartingTechnologies();
        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out _);
        RefreshQuestProgress(true);
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
        MarkPersistentProgressDirty();
    }

    private bool EnsureStartingCourierSuppliesGranted()
    {
        if (progress == null || progress.receivedStartingCourierSupplies)
        {
            return false;
        }

        PortStorageState storage = progress.GetPortStorageState(GetCapitalPortId(), true);
        if (storage == null)
        {
            return false;
        }

        storage.AddResource("windshale_ore", 260);
        storage.AddResource("dawnspar_ore", 160);
        storage.AddResource("cloud_condensate", 260);
        storage.AddResource(SessionExtractionConstants.BrokenAutomatonItemId, 140);
        storage.AddResource("windcalf_carcass", 220);
        storage.AddResource(SessionExtractionConstants.RockInfoItemId, 140);
        storage.AddResource("ferron", 140);
        storage.AddResource("silvate", 90);
        storage.AddResource("cloth", 90);
        storage.AddResource("tools", 56);
        storage.AddResource("charcoal", 260);
        storage.AddResource("claudium", 180);
        storage.AddResource("metal", 120);
        storage.AddResource("mechanisms", 56);
        storage.AddResource("weapon", 32);
        progress.receivedStartingCourierSupplies = true;
        return true;
    }

    public bool SavePersistentProgressNow()
    {
        return TrySavePersistentProgress(true);
    }

    public bool SavePersistentProgressNowForTests()
    {
        return SavePersistentProgressNow();
    }

    public bool ResetPersistentProgressFromSettings(out string message)
    {
        return ResetAccountProgressForCheat(out message);
    }

    public static string GetPersistentProgressSavePathForTests(bool bigTestPath)
    {
        string fileName = bigTestPath ? PersistentProgressBigTestSaveFileName : PersistentProgressSaveFileName;
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public static bool DeletePersistentProgressSaveForTests(bool bigTestPath)
    {
        return DeletePersistentProgressSaveFile(GetPersistentProgressSavePathForTests(bigTestPath), out _);
    }

    private void TryLoadPersistentProgressOnAwake()
    {
        if (persistentProgressLoadAttempted || !Application.isPlaying)
        {
            return;
        }

        persistentProgressLoadAttempted = true;
        string path = ResolvePersistentProgressSavePath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            MetaGameProgressSaveData data = JsonUtility.FromJson<MetaGameProgressSaveData>(json);
            if (data == null || data.progress == null || data.version <= 0 || data.version > MetaGameProgressSaveData.CurrentVersion)
            {
                Debug.LogWarning("[MetaGameState] Ignoring unsupported progress save: " + path, this);
                return;
            }

            progress = data.progress.Clone();
            if (!string.IsNullOrWhiteSpace(data.runtimeAccountId))
            {
                runtimeAccountId = data.runtimeAccountId.Trim();
            }

            initialized = false;
            persistentProgressLoadedThisSession = true;
            lastPersistentProgressFingerprint = BuildPersistentProgressFingerprint();
            nextPersistentProgressAutosaveTime = Time.unscaledTime + PersistentProgressAutosaveIntervalSeconds;
            Debug.Log("[MetaGameState] Loaded persistent progress: " + path, this);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[MetaGameState] Could not load persistent progress from " + path + ": " + exception.Message, this);
        }
    }

    private void AutoSavePersistentProgressIfNeeded()
    {
        if (!Application.isPlaying || !initialized || progress == null)
        {
            return;
        }

        if (Time.unscaledTime < nextPersistentProgressAutosaveTime)
        {
            return;
        }

        nextPersistentProgressAutosaveTime = Time.unscaledTime + PersistentProgressAutosaveIntervalSeconds;
        TrySavePersistentProgress(false);
    }

    private bool TrySavePersistentProgress(bool force)
    {
        if (!Application.isPlaying || progress == null)
        {
            return false;
        }

        PreparePersistentProgressForSave();
        string fingerprint = BuildPersistentProgressFingerprint();
        if (!force && fingerprint == lastPersistentProgressFingerprint)
        {
            return true;
        }

        MetaGameProgressSaveData data = BuildPersistentProgressSaveData(DateTime.UtcNow.Ticks);
        string json = JsonUtility.ToJson(data, true);
        string path = ResolvePersistentProgressSavePath();
        try
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
            lastPersistentProgressFingerprint = fingerprint;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[MetaGameState] Could not save persistent progress to " + path + ": " + exception.Message, this);
            return false;
        }
    }

    private void PreparePersistentProgressForSave()
    {
        if (progress == null)
        {
            return;
        }

        if (initialized)
        {
            SyncShipConsumablesWithCargo(false);
            if (IsDocked)
            {
                RememberCurrentDockPosition();
            }
            else
            {
                RememberCurrentFlightPose();
            }
        }

        if (progress.lastProcessUtcTicks <= 0)
        {
            progress.lastProcessUtcTicks = DateTime.UtcNow.Ticks;
        }

        RefreshQuestProgress(true);
        progress.Normalize();
    }

    private MetaGameProgressSaveData BuildPersistentProgressSaveData(long savedUtcTicks)
    {
        return new MetaGameProgressSaveData
        {
            version = MetaGameProgressSaveData.CurrentVersion,
            runtimeAccountId = RuntimeAccountId,
            savedUtcTicks = savedUtcTicks,
            progress = progress != null ? progress.Clone() : new PlayerProgress()
        };
    }

    private string BuildPersistentProgressFingerprint()
    {
        MetaGameProgressSaveData data = BuildPersistentProgressSaveData(0);
        return JsonUtility.ToJson(data, false);
    }

    private void MarkPersistentProgressDirty()
    {
        lastPersistentProgressFingerprint = "";
        nextPersistentProgressAutosaveTime = 0f;
    }

    private string ResolvePersistentProgressSavePath()
    {
        return GetPersistentProgressSavePathForTests(IsUsingBigTestPersistentProgress());
    }

    private bool IsUsingBigTestPersistentProgress()
    {
        return useBigTestPersistentProgressFile || ShouldUseBigTestProgressSaveFile();
    }

    private static bool ShouldUseBigTestProgressSaveFile()
    {
        return WildWindBigTestRunner.IsProgressSandboxActive;
    }

    private static bool DeletePersistentProgressSaveFile(string path, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Progress save path is empty.";
            return false;
        }

        try
        {
            string tempPath = path + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
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
        RecordQuestEvent("ship_hull_selected", hull.partId, 1);
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
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            RecordQuestEvent("ship_module_installed", moduleId, 1);
        }

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

    public IReadOnlyList<ShipTreeEntryConfig> GetShipTreeEntryConfigs()
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        return sessionConfig != null ? sessionConfig.shipTreeEntries : null;
    }

    public string GetDevelopmentShipRosterSummaryText(int maxRows = 8)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        if (sessionConfig == null || sessionConfig.shipTreeEntries == null || sessionConfig.shipTreeEntries.Count == 0)
        {
            return "Корабельный каталог не загружен.";
        }

        Dictionary<string, int> factionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> classCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int developmentCount = 0;
        int placeholderModels = 0;
        int runtimeReadyHulls = 0;

        for (int i = 0; i < sessionConfig.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = sessionConfig.shipTreeEntries[i];
            if (entry == null || !entry.IsDevelopmentRosterShip)
            {
                continue;
            }

            developmentCount++;
            IncrementCount(factionCounts, entry.factionId);
            IncrementCount(classCounts, entry.shipClassId);
            if (entry.visualShapeId == "square" || entry.visualModelId == "placeholder_square")
            {
                placeholderModels++;
            }

            if (entry.HasRuntimeHull)
            {
                runtimeReadyHulls++;
            }
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Корабли развития: " + developmentCount + " корпусов.");
        builder.AppendLine("Фракции: " + FormatCounts(factionCounts));
        builder.AppendLine("Классы: " + FormatCounts(classCounts));
        builder.AppendLine("Модели: square placeholders " + placeholderModels + ", летные hull " + runtimeReadyHulls + ".");
        builder.AppendLine();
        builder.AppendLine("Первые позиции:");

        int shown = 0;
        maxRows = Mathf.Clamp(maxRows, 1, 20);
        for (int i = 0; i < sessionConfig.shipTreeEntries.Count && shown < maxRows; i++)
        {
            ShipTreeEntryConfig entry = sessionConfig.shipTreeEntries[i];
            if (entry == null || !entry.IsDevelopmentRosterShip)
            {
                continue;
            }

            builder.Append("- ");
            builder.Append(entry.DisplayNameRu);
            builder.Append(" | ");
            builder.Append(string.IsNullOrWhiteSpace(entry.FactionDisplayNameRu) ? "no faction" : entry.FactionDisplayNameRu);
            builder.Append(" | ");
            builder.Append(string.IsNullOrWhiteSpace(entry.ClassDisplayNameRu) ? "no class" : entry.ClassDisplayNameRu);
            builder.Append(" | ");
            builder.Append(entry.costAmount);
            builder.Append(" ");
            builder.Append(string.IsNullOrWhiteSpace(entry.costCurrencyItemId) ? "cost" : entry.costCurrencyItemId);
            builder.Append(" | ");
            builder.Append(string.IsNullOrWhiteSpace(entry.visualModelId) ? "no model" : entry.visualModelId);
            if (entry.HasRuntimeHull)
            {
                builder.Append(" | runtime hull");
            }

            builder.AppendLine();
            shown++;
        }

        return builder.ToString().TrimEnd();
    }

    private static void IncrementCount(Dictionary<string, int> counts, string key)
    {
        if (counts == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        counts.TryGetValue(key, out int current);
        counts[key] = current + 1;
    }

    private static string FormatCounts(Dictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
        {
            return "-";
        }

        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, int> pair in counts)
        {
            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }

            builder.Append(pair.Key);
            builder.Append(" ");
            builder.Append(pair.Value);
        }

        return builder.ToString();
    }

    public PortStorageState GetCapitalStorageState()
    {
        EnsureProgressInitialized();
        return progress.GetPortStorageState(GetCapitalPortId(), true);
    }

    public IReadOnlyList<CourierOrderSlotState> GetCourierOrderSlots()
    {
        EnsureProgressInitialized();
        int changed = EnsureCourierServiceReady(GetProcessUtcNow().Ticks);
        if (changed > 0)
        {
            MarkPersistentProgressDirty();
        }

        return progress.courierService.slots;
    }

    public CourierOrderSlotState GetCourierOrderSlot(int slotIndex)
    {
        EnsureProgressInitialized();
        int changed = EnsureCourierServiceReady(GetProcessUtcNow().Ticks);
        if (changed > 0)
        {
            MarkPersistentProgressDirty();
        }

        return progress.courierService.GetSlot(Mathf.Clamp(slotIndex, 0, CourierOrderSlotCount - 1), false);
    }

    public bool CanSendCourierOrder(int slotIndex, out string reason)
    {
        EnsureProgressInitialized();
        EnsureCourierServiceReady(GetProcessUtcNow().Ticks);
        CourierOrderSlotState slot = progress.courierService.GetSlot(Mathf.Clamp(slotIndex, 0, CourierOrderSlotCount - 1), false);
        PortStorageState storage = GetCapitalStorageState();
        return CanSendCourierOrder(slot, storage, out reason);
    }

    public bool TrySendCourierOrder(int slotIndex, out string message)
    {
        EnsureProgressInitialized();
        long nowTicks = GetProcessUtcNow().Ticks;
        EnsureCourierServiceReady(nowTicks);
        int normalizedSlotIndex = Mathf.Clamp(slotIndex, 0, CourierOrderSlotCount - 1);
        CourierOrderSlotState slot = progress.courierService.GetSlot(normalizedSlotIndex, false);
        PortStorageState storage = GetCapitalStorageState();
        if (!CanSendCourierOrder(slot, storage, out message))
        {
            lastAccountMessage = message;
            return false;
        }

        for (int i = 0; i < slot.inputs.Count; i++)
        {
            CascadeItemAmount input = slot.inputs[i];
            if (input == null) continue;
            storage.TrySpendResource(input.itemId, input.amount);
            AddQuestEventMetric("resource_spent", input.itemId, input.amount);
        }

        storage.AddResource(CourierFreightRewardItemId, slot.freightReward);
        storage.AddResource(CourierExperienceRewardItemId, slot.designExperienceReward);
        AddQuestEventMetric("resource_acquired", CourierFreightRewardItemId, slot.freightReward);
        AddQuestEventMetric("resource_acquired", CourierExperienceRewardItemId, slot.designExperienceReward);
        AddQuestEventMetric("courier_sent", "any", 1);
        string completedClient = slot.clientName;
        int freightReward = slot.freightReward;
        int experienceReward = slot.designExperienceReward;
        GenerateCourierOrder(slot, nowTicks);
        MarkPersistentProgressDirty();
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

        message = "Курьер отправлен: " + completedClient
            + ". Награда: фрахт x" + freightReward
            + ", очки освоения x" + experienceReward + ".";
        lastAccountMessage = message;
        return true;
    }

    public bool TryCancelCourierOrder(int slotIndex, out string message)
    {
        EnsureProgressInitialized();
        long nowTicks = GetProcessUtcNow().Ticks;
        EnsureCourierServiceReady(nowTicks);
        int normalizedSlotIndex = Mathf.Clamp(slotIndex, 0, CourierOrderSlotCount - 1);
        CourierOrderSlotState slot = progress.courierService.GetSlot(normalizedSlotIndex, false);
        if (slot == null || !slot.HasActiveOrder)
        {
            message = "На площадке нет активной курьерской заявки.";
            lastAccountMessage = message;
            return false;
        }

        string cancelledClient = slot.clientName;
        slot.ClearOrder();
        slot.cooldownCompleteUtcTicks = nowTicks + TimeSpan.FromSeconds(CourierCancelCooldownSeconds).Ticks;
        AddQuestEventMetric("courier_cancelled", "any", 1);
        MarkPersistentProgressDirty();
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

        message = "Заявка отменена: " + cancelledClient + ". Новая появится через 15 минут.";
        lastAccountMessage = message;
        return true;
    }

    public bool TryCancelAllCourierOrders(out string message)
    {
        EnsureProgressInitialized();
        long nowTicks = GetProcessUtcNow().Ticks;
        EnsureCourierServiceReady(nowTicks);

        int cancelled = 0;
        for (int i = 0; i < CourierOrderSlotCount; i++)
        {
            CourierOrderSlotState slot = progress.courierService.GetSlot(i, false);
            if (slot == null || !slot.HasActiveOrder)
            {
                continue;
            }

            slot.ClearOrder();
            slot.cooldownCompleteUtcTicks = nowTicks + TimeSpan.FromSeconds(CourierCancelCooldownSeconds).Ticks;
            cancelled++;
        }

        if (cancelled <= 0)
        {
            message = "Нет активных курьерских заявок для отмены.";
            lastAccountMessage = message;
            return false;
        }

        AddQuestEventMetric("courier_cancelled", "any", cancelled);
        MarkPersistentProgressDirty();
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);
        message = "Отменено курьерских заявок: " + cancelled + ". Обновление через 15 минут.";
        lastAccountMessage = message;
        return true;
    }

    public int GetCourierOrderCooldownRemainingSeconds(int slotIndex)
    {
        EnsureProgressInitialized();
        long nowTicks = GetProcessUtcNow().Ticks;
        EnsureCourierServiceReady(nowTicks);
        CourierOrderSlotState slot = progress.courierService.GetSlot(Mathf.Clamp(slotIndex, 0, CourierOrderSlotCount - 1), false);
        if (slot == null || slot.cooldownCompleteUtcTicks <= nowTicks)
        {
            return 0;
        }

        return Mathf.CeilToInt((float)((slot.cooldownCompleteUtcTicks - nowTicks) / (double)TimeSpan.TicksPerSecond));
    }

    private int AdvanceCourierServiceOrders(long utcTicks)
    {
        return EnsureCourierServiceReady(utcTicks);
    }

    private int EnsureCourierServiceReady(long utcTicks)
    {
        if (progress == null)
        {
            return 0;
        }

        progress.courierService ??= new CourierServiceState();
        progress.courierService.Normalize();

        int changed = 0;
        for (int i = 0; i < CourierOrderSlotCount; i++)
        {
            CourierOrderSlotState slot = progress.courierService.GetSlot(i, true);
            slot.slotIndex = i;
            slot.Normalize();
            if (slot.IsCoolingDownAt(utcTicks))
            {
                continue;
            }

            if (slot.cooldownCompleteUtcTicks > 0L || !slot.HasActiveOrder)
            {
                GenerateCourierOrder(slot, utcTicks);
                changed++;
            }
        }

        return changed;
    }

    private void GenerateCourierOrder(CourierOrderSlotState slot, long utcTicks)
    {
        if (slot == null)
        {
            return;
        }

        slot.inputs ??= new List<CascadeItemAmount>();
        slot.inputs.Clear();
        slot.cooldownCompleteUtcTicks = 0L;
        slot.generation = Mathf.Max(0, slot.generation) + 1;
        slot.orderId = "wind_houses_courier_" + (slot.slotIndex + 1) + "_" + slot.generation;

        int seed = BuildCourierOrderSeed(slot.slotIndex, slot.generation);
        slot.clientName = CourierClientNames[PositiveMod(seed, CourierClientNames.Length)];
        int inputCount = 1 + PositiveMod(seed / 7, 3);
        int totalValue = 0;
        for (int i = 0; i < inputCount; i++)
        {
            CourierResourceSpec spec = CourierResourceCatalog[PositiveMod(seed + i * 13, CourierResourceCatalog.Length)];
            int amountRange = Mathf.Max(1, spec.maxAmount - spec.minAmount + 1);
            int amount = spec.minAmount + PositiveMod(seed / (i + 3) + i * 17 + slot.slotIndex, amountRange);
            AddCourierInput(slot.inputs, spec.itemId, amount);
            totalValue += amount * spec.freightValue;
        }

        float payoutMultiplier = 1.18f + 0.03f * PositiveMod(seed / 11, 7);
        slot.freightReward = Mathf.Max(100, Mathf.RoundToInt(totalValue * payoutMultiplier / 10f) * 10);
        slot.designExperienceReward = Mathf.Max(2, Mathf.RoundToInt(slot.freightReward / 90f) + inputCount);
        slot.Normalize();
    }

    private bool CanSendCourierOrder(CourierOrderSlotState slot, PortStorageState storage, out string reason)
    {
        reason = "";
        if (slot == null)
        {
            reason = "Курьерская площадка не найдена.";
            return false;
        }

        if (slot.cooldownCompleteUtcTicks > GetProcessUtcNow().Ticks)
        {
            reason = "Заявка обновляется после отмены.";
            return false;
        }

        if (!slot.HasActiveOrder)
        {
            reason = "На площадке нет активной курьерской заявки.";
            return false;
        }

        if (storage == null)
        {
            reason = "Склад столицы не найден.";
            return false;
        }

        for (int i = 0; i < slot.inputs.Count; i++)
        {
            CascadeItemAmount input = slot.inputs[i];
            if (input == null) continue;
            int available = storage.GetResourceAmount(input.itemId);
            if (available < input.amount)
            {
                string itemName = sessionConfig != null ? sessionConfig.GetItemNameRu(input.itemId) : input.itemId;
                reason = "Не хватает: " + itemName + " x" + (input.amount - available) + ".";
                return false;
            }
        }

        return true;
    }

    private static void AddCourierInput(List<CascadeItemAmount> inputs, string itemId, int amount)
    {
        if (inputs == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return;
        }

        for (int i = 0; i < inputs.Count; i++)
        {
            CascadeItemAmount input = inputs[i];
            if (input != null && input.itemId == itemId)
            {
                input.amount += amount;
                return;
            }
        }

        inputs.Add(new CascadeItemAmount { itemId = itemId, amount = amount });
    }

    private static int BuildCourierOrderSeed(int slotIndex, int generation)
    {
        unchecked
        {
            int seed = 17;
            seed = seed * 31 + slotIndex * 73856093;
            seed = seed * 31 + generation * 19349663;
            return seed;
        }
    }

    private static int PositiveMod(int value, int divisor)
    {
        if (divisor <= 0)
        {
            return 0;
        }

        int mod = value % divisor;
        return mod < 0 ? mod + divisor : mod;
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
        state.completedCycles = Mathf.Clamp(state.completedCycles, 0, GetTechnologyMaxLevel(technology));
        state.activeCycleStartUtcTicks = 0;
        state.activeCycleEndUtcTicks = 0;

        if (!EnsureTechnologyLevelRequirementsPaid(technology, state, out reason))
        {
            progress.activeResearchTechnologyId = "";
            lastAccountMessage = reason;
            return false;
        }

        MarkPersistentProgressDirty();
        string researchMessage = "Архивы вливают SP в знание: " + GetTechnologyDisplayName(technology)
            + " L" + GetTechnologyNextLevel(technology).ToString()
            + " (" + GetArchiveKnowledgeSpPerMinute(technology.categoryId).ToString("0.##") + " SP/мин).";
        lastAccountMessage = researchMessage;
        RefreshRuntimeAccountIfDocked();
        AddQuestEventMetric("technology_started", technology.id, 1);
        RefreshQuestProgress(true);
        lastAccountMessage = researchMessage;
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
            reason = "Знание уже изучено.";
            return false;
        }

        if (!IsKnowledgeAvailableForResearch(technology))
        {
            reason = "Сначала нужно открыть книгу/право на знание: " + GetTechnologyDisplayName(technology) + ".";
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
            if (activeState != null && activeState.currentLevelSpProgress > 0.001f)
            {
                reason = "Архивы уже вливают SP в другое знание; свободное перекладывание после старта запрещено.";
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

    public bool IsKnowledgeAvailableForResearch(TechnologyConfig technology)
    {
        EnsureProgressInitialized();
        if (technology == null) return false;
        if (technology.id == "basic_airship") return true;
        if (technology.rank <= 1) return true;
        if (progress.IsKnowledgeUnlocked(technology.id)) return true;

        return AreTechnologyPrerequisitesCompleted(technology, out _);
    }

    public bool UnlockKnowledgeFromBook(string technologyId, out string reason)
    {
        reason = "";
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();

        TechnologyConfig technology = sessionConfig != null ? sessionConfig.GetTechnology(technologyId) : null;
        if (technology == null)
        {
            reason = "Знание не найдено.";
            return false;
        }

        bool changed = progress.UnlockKnowledge(technology.id);
        MarkPersistentProgressDirty();
        reason = changed
            ? "Книга открыла знание: " + GetTechnologyDisplayName(technology) + "."
            : "Знание уже открыто книгой: " + GetTechnologyDisplayName(technology) + ".";
        RefreshRuntimeAccountIfDocked();
        return true;
    }

    public void GrantKnowledgeSpPackage(string packageId, string scopeKind, string scopeId, int amountSp)
    {
        EnsureProgressInitialized();
        progress.AddKnowledgeSpPackage(packageId, scopeKind, scopeId, amountSp);
        MarkPersistentProgressDirty();
        RefreshRuntimeAccountIfDocked();
    }

    public bool TryApplyKnowledgeSpPackage(string packageId, string technologyId, out int appliedSp, out int burnedSp, out string reason)
    {
        appliedSp = 0;
        burnedSp = 0;
        reason = "";
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();

        KnowledgeSpPackageState package = progress.GetKnowledgeSpPackage(packageId);
        TechnologyConfig technology = sessionConfig != null ? sessionConfig.GetTechnology(technologyId) : null;
        if (package == null)
        {
            reason = "SP-пакет не найден.";
            return false;
        }

        if (technology == null)
        {
            reason = "Знание не найдено.";
            return false;
        }

        if (!CanApplyKnowledgeSpPackage(package, technology, out reason))
        {
            return false;
        }

        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        if (!EnsureTechnologyLevelRequirementsPaid(technology, state, out reason))
        {
            return false;
        }

        int levelCost = GetTechnologyLevelSpCost(technology, GetTechnologyNextLevel(technology));
        float remaining = Mathf.Max(0f, levelCost - state.currentLevelSpProgress);
        appliedSp = Mathf.Min(package.amountSp, Mathf.CeilToInt(remaining));
        burnedSp = Mathf.Max(0, package.amountSp - appliedSp);
        state.currentLevelSpProgress = Mathf.Min(levelCost, state.currentLevelSpProgress + appliedSp);
        progress.RemoveKnowledgeSpPackage(package.packageId);

        if (state.currentLevelSpProgress + 0.001f >= levelCost)
        {
            CompleteTechnologyLevel(technology, state);
        }

        MarkPersistentProgressDirty();
        lastAccountMessage = "SP-пакет вложен в " + GetTechnologyDisplayName(technology)
            + ": +" + appliedSp.ToString() + " SP"
            + (burnedSp > 0 ? ", перелив сгорел " + burnedSp.ToString() + " SP." : ".");
        reason = lastAccountMessage;
        RefreshRuntimeAccountIfDocked();
        return true;
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

    public string FormatTechnologyLevelCost(TechnologyConfig technology)
    {
        return FormatTechnologyCycleCost(technology);
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
        int completedLevels = state != null ? state.completedCycles : 0;
        int nextLevel = Mathf.Clamp(completedLevels + 1, 1, GetTechnologyMaxLevel(technology));
        int levelCost = GetTechnologyLevelSpCost(technology, nextLevel);
        float spProgress = state != null ? state.currentLevelSpProgress : 0f;
        string levelText = completedLevels + "/" + GetTechnologyMaxLevel(technology);
        string spText = Mathf.FloorToInt(spProgress).ToString() + "/" + levelCost.ToString() + " SP";

        if (progress.activeResearchTechnologyId != technology.id)
        {
            return IsKnowledgeAvailableForResearch(technology)
                ? "доступно, уровни " + levelText + ", текущий " + spText
                : "нужна книга/право, уровни " + levelText;
        }

        return HasCapitalResourcesForCycle(technology)
            ? "активно, Архив +" + GetArchiveKnowledgeSpPerMinute(technology.categoryId).ToString("0.##") + " SP/мин, " + spText
            : state != null && state.currentLevelRequirementsPaid
            ? "активно, Архив +" + GetArchiveKnowledgeSpPerMinute(technology.categoryId).ToString("0.##") + " SP/мин, " + spText
            : "активно, но ждёт ресурсы уровня, " + spText;
    }

    public int GetTechnologyLevelSpCost(TechnologyConfig technology, int level)
    {
        if (technology == null) return 0;
        if (technology.id == "basic_airship") return 0;

        int normalizedLevel = Mathf.Clamp(level, 1, GetTechnologyMaxLevel(technology));
        if (technology.spCostByLevel != null && normalizedLevel - 1 < technology.spCostByLevel.Count)
        {
            return Mathf.Max(1, technology.spCostByLevel[normalizedLevel - 1]);
        }

        return normalizedLevel switch
        {
            1 => 1000,
            2 => 5000,
            3 => 20000,
            4 => 80000,
            _ => 250000
        };
    }

    public float GetTechnologyCurrentLevelSpProgress(TechnologyConfig technology)
    {
        if (technology == null) return 0f;
        EnsureProgressInitialized();
        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, false);
        return state != null ? Mathf.Max(0f, state.currentLevelSpProgress) : 0f;
    }

    public float GetArchiveKnowledgeSpPerMinute(string categoryId = "")
    {
        float multiplier = 1f + Mathf.Max(0f, GetTechnologyModifierValue("research_speed"));
        return Mathf.Max(0f, BaseArchiveKnowledgeSpPerMinute * multiplier);
    }

    public int GetTechnologyCompletedLevel(TechnologyConfig technology)
    {
        if (technology == null) return 0;
        EnsureProgressInitialized();

        int maxLevel = Mathf.Max(1, technology.requiredCycles);
        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, false);
        if (state != null)
        {
            return Mathf.Clamp(state.completedCycles, 0, maxLevel);
        }

        return progress.IsTechnologyCompleted(technology.id) ? maxLevel : 0;
    }

    public float GetTechnologyModifierValue(string modifierId)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        if (sessionConfig == null || sessionConfig.technologies == null || string.IsNullOrWhiteSpace(modifierId))
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < sessionConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = sessionConfig.technologies[i];
            int level = GetTechnologyCompletedLevel(technology);
            if (technology == null || level <= 0 || technology.modifierGrants == null)
            {
                continue;
            }

            for (int grantIndex = 0; grantIndex < technology.modifierGrants.Count; grantIndex++)
            {
                TechnologyModifierGrantConfig grant = technology.modifierGrants[grantIndex];
                if (grant == null || grant.modifierId != modifierId)
                {
                    continue;
                }

                if (grant.operation == "unlock")
                {
                    total = Mathf.Max(total, 1f);
                }
                else
                {
                    total += grant.valuePerLevel * level;
                }
            }
        }

        return total;
    }

    public string GetTechnologyBoardOverviewText(string selectedCategoryId, int maxRows = 8)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        if (sessionConfig == null || sessionConfig.technologies == null || sessionConfig.technologies.Count == 0)
        {
            return "Технологии не загружены.";
        }

        string categoryId = string.IsNullOrWhiteSpace(selectedCategoryId)
            ? GetFirstTechnologyCategoryId()
            : selectedCategoryId;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Рубрикаторы: " + GetTechnologyCategoryStripText(categoryId));
        builder.AppendLine("Дерево: " + GetTechnologyCategoryName(categoryId) + " | узлы: " + CountTechnologiesInCategory(categoryId));
        builder.AppendLine("Колонки: I-V | знание: 5 уровней, SP-стоимость, ресурсы уровня, книги/locks, modifier");
        builder.AppendLine("Архивы: " + GetArchiveKnowledgeSpPerMinute(categoryId).ToString("0.##") + " SP/мин в выбранное активное знание");

        List<TechnologyConfig> technologies = GetTechnologiesInCategory(categoryId);
        technologies.Sort(CompareTechnologyTreePosition);
        int shown = 0;
        maxRows = Mathf.Clamp(maxRows, 1, 24);
        for (int i = 0; i < technologies.Count && shown < maxRows; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null) continue;

            int level = GetTechnologyCompletedLevel(technology);
            int nextLevel = Mathf.Clamp(level + 1, 1, GetTechnologyMaxLevel(technology));
            int levelSpCost = GetTechnologyLevelSpCost(technology, nextLevel);
            float levelSpProgress = GetTechnologyCurrentLevelSpProgress(technology);
            builder.Append("[").Append(technology.treeColumn).Append(":").Append(technology.treeRow).Append("] ");
            builder.Append(GetTechnologyDisplayName(technology));
            builder.Append(" ");
            builder.Append(level).Append("/").Append(GetTechnologyMaxLevel(technology));
            builder.Append(" | SP L").Append(nextLevel).Append(" ");
            builder.Append(Mathf.FloorToInt(levelSpProgress)).Append("/").Append(levelSpCost);
            builder.Append(" | resources ").Append(FormatTechnologyLevelCost(technology));
            builder.Append(" | ").Append(FormatTechnologyModifierGrants(technology));
            if (!IsKnowledgeAvailableForResearch(technology))
            {
                builder.Append(" | нужна книга/право");
            }

            if (technology.prerequisiteTechnologyIds != null && technology.prerequisiteTechnologyIds.Count > 0)
            {
                builder.Append(" | req ").Append(string.Join("+", technology.prerequisiteTechnologyIds));
            }

            builder.AppendLine();
            shown++;
        }

        builder.AppendLine("Активные модификаторы: " + GetTechnologyModifierSummaryText(6));
        return builder.ToString().TrimEnd();
    }

    public string GetFirstTechnologyCategoryId()
    {
        EnsureSessionConfigLoaded();
        if (sessionConfig == null || sessionConfig.technologies == null)
        {
            return "";
        }

        for (int i = 0; i < sessionConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = sessionConfig.technologies[i];
            if (technology != null && !string.IsNullOrWhiteSpace(technology.categoryId) && technology.categoryId != "starter")
            {
                return technology.categoryId;
            }
        }

        return "";
    }

    public string GetNextTechnologyCategoryId(string currentCategoryId)
    {
        EnsureSessionConfigLoaded();
        List<string> categories = GetTechnologyCategoryIds();
        if (categories.Count == 0)
        {
            return "";
        }

        int currentIndex = 0;
        for (int i = 0; i < categories.Count; i++)
        {
            if (string.Equals(categories[i], currentCategoryId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        return categories[(currentIndex + 1) % categories.Count];
    }

    public IReadOnlyList<string> GetTechnologyCategoryIdsForUi()
    {
        EnsureSessionConfigLoaded();
        return GetTechnologyCategoryIds();
    }

    public string GetTechnologyModifierSummaryText(int maxRows = 8)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        if (sessionConfig == null || sessionConfig.modifierDefinitions == null || sessionConfig.modifierDefinitions.Count == 0)
        {
            return "нет каталога модификаторов";
        }

        StringBuilder builder = new StringBuilder();
        int shown = 0;
        maxRows = Mathf.Clamp(maxRows, 1, 32);
        for (int i = 0; i < sessionConfig.modifierDefinitions.Count && shown < maxRows; i++)
        {
            ModifierDefinitionConfig modifier = sessionConfig.modifierDefinitions[i];
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.id)) continue;

            float value = GetTechnologyModifierValue(modifier.id);
            if (Mathf.Abs(value) <= 0.0001f && modifier.id != "starter_airship_unlock")
            {
                continue;
            }

            if (shown > 0) builder.Append("; ");
            builder.Append(sessionConfig.GetModifierNameRu(modifier.id));
            builder.Append(" ");
            builder.Append(FormatModifierValue(value, modifier.valueKind));
            shown++;
        }

        return shown > 0 ? builder.ToString() : "пока нет активных бонусов";
    }

    private List<string> GetTechnologyCategoryIds()
    {
        List<string> categories = new List<string>();
        if (sessionConfig == null || sessionConfig.technologies == null)
        {
            return categories;
        }

        for (int i = 0; i < sessionConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = sessionConfig.technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.categoryId) || technology.categoryId == "starter")
            {
                continue;
            }

            bool exists = false;
            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                if (string.Equals(categories[categoryIndex], technology.categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                categories.Add(technology.categoryId);
            }
        }

        return categories;
    }

    private string GetTechnologyCategoryStripText(string selectedCategoryId)
    {
        List<string> categories = GetTechnologyCategoryIds();
        if (categories.Count == 0)
        {
            return "нет рубрик";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < categories.Count; i++)
        {
            string categoryId = categories[i];
            string label = GetTechnologyCategoryName(categoryId) + " " + CountTechnologiesInCategory(categoryId);
            parts.Add(string.Equals(categoryId, selectedCategoryId, StringComparison.OrdinalIgnoreCase) ? "[" + label + "]" : label);
        }

        return string.Join(" | ", parts);
    }

    private string GetTechnologyCategoryName(string categoryId)
    {
        if (sessionConfig == null || sessionConfig.technologies == null)
        {
            return categoryId ?? "";
        }

        for (int i = 0; i < sessionConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = sessionConfig.technologies[i];
            if (technology != null && string.Equals(technology.categoryId, categoryId, StringComparison.OrdinalIgnoreCase))
            {
                return technology.CategoryDisplayNameRu;
            }
        }

        return categoryId ?? "";
    }

    private int CountTechnologiesInCategory(string categoryId)
    {
        return GetTechnologiesInCategory(categoryId).Count;
    }

    private List<TechnologyConfig> GetTechnologiesInCategory(string categoryId)
    {
        List<TechnologyConfig> result = new List<TechnologyConfig>();
        if (sessionConfig == null || sessionConfig.technologies == null || string.IsNullOrWhiteSpace(categoryId))
        {
            return result;
        }

        for (int i = 0; i < sessionConfig.technologies.Count; i++)
        {
            TechnologyConfig technology = sessionConfig.technologies[i];
            if (technology != null && string.Equals(technology.categoryId, categoryId, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(technology);
            }
        }

        return result;
    }

    private static int CompareTechnologyTreePosition(TechnologyConfig left, TechnologyConfig right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int columnComparison = left.treeColumn.CompareTo(right.treeColumn);
        if (columnComparison != 0) return columnComparison;
        int rowComparison = left.treeRow.CompareTo(right.treeRow);
        return rowComparison != 0 ? rowComparison : string.Compare(left.id, right.id, StringComparison.OrdinalIgnoreCase);
    }

    private string FormatTechnologyModifierGrants(TechnologyConfig technology)
    {
        if (technology == null || technology.modifierGrants == null || technology.modifierGrants.Count == 0)
        {
            return "modifier -";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < technology.modifierGrants.Count; i++)
        {
            TechnologyModifierGrantConfig grant = technology.modifierGrants[i];
            if (grant == null || string.IsNullOrWhiteSpace(grant.modifierId)) continue;

            ModifierDefinitionConfig modifier = sessionConfig != null ? sessionConfig.GetModifierDefinition(grant.modifierId) : null;
            string valueKind = modifier != null ? modifier.valueKind : "percent";
            parts.Add(sessionConfig.GetModifierNameRu(grant.modifierId) + " " + FormatModifierValue(grant.valuePerLevel, valueKind) + "/ур.");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "modifier -";
    }

    private static string FormatModifierValue(float value, string valueKind)
    {
        if (valueKind == "unlock")
        {
            return value > 0.5f ? "открыто" : "закрыто";
        }

        if (valueKind == "percent" || valueKind == "multiplier")
        {
            return (value >= 0f ? "+" : "") + (value * 100f).ToString("0.#") + "%";
        }

        return (value >= 0f ? "+" : "") + value.ToString("0.##");
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
            distanceToBaseKm = SessionExtractionConstants.DefaultSafeSortieDistanceToBaseKm,
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
            distanceToBaseKm = SessionExtractionConstants.DefaultSafeSortieDistanceToBaseKm,
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
            distanceToBaseKm = SessionExtractionConstants.DefaultSafeSortieDistanceToBaseKm,
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
            distanceToBaseKm = SessionExtractionConstants.DefaultSafeSortieDistanceToBaseKm,
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
            distanceToBaseKm = SessionExtractionConstants.DefaultSafeSortieDistanceToBaseKm,
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

        string fuelId = string.IsNullOrWhiteSpace(profile.coalResourceId) ? GetStartingFuelId() : profile.coalResourceId;
        string claudiumId = string.IsNullOrWhiteSpace(profile.claudiumResourceId) ? "claudium" : profile.claudiumResourceId;
        if (progress.shipFuelTank.GetAmount(fuelId) + 0.001f < estimate.requiredCoalKg
            || progress.shipClaudiumTank.GetAmount(claudiumId) + 0.001f < estimate.requiredClaudiumKg)
        {
            message = estimate.status;
            lastAccountMessage = message;
            return false;
        }

        progress.shipFuelTank.TrySpend(fuelId, estimate.requiredCoalKg);
        progress.shipClaudiumTank.TrySpend(claudiumId, estimate.requiredClaudiumKg);
        string completedSortieId = progress.activeSortie != null && progress.activeSortie.zone != null
            ? progress.activeSortie.zone.sortieId
            : "any";
        int transferred = TransferShipCargoToCapital();
        progress.ClearShipCargo();

        string baseDockId = GetCapitalPortId();
        progress.SetDocked(baseDockId, GetDockPositionOrFallback(baseDockId));
        SyncShipConsumablesWithCargo(true);
        ApplySessionModeToShip();
        AddQuestEventMetric("sortie_completed", completedSortieId, 1);
        RefreshQuestProgress(true);

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
        return fuelId + " " + progress.shipFuelTank.GetAmount(fuelId).ToString("F0")
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

        return "Cascade " + SessionExtractionIndustry.CascadeProductionTypes.Length
            + " queue " + progress.baseIndustry.ActiveCascadeQueueCount + "/" + BaseCascadeQueueSlotCount
            + ": " + string.Join(" | ", parts);
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
        for (int i = 0; cost != null && i < cost.Count; i++)
        {
            CascadeItemAmount item = cost[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;
            AddQuestEventMetric("resource_spent", item.itemId, item.amount);
        }

        int oldLevel = line.level;
        float oldCapacity = line.capacityUnitsPerMinute;
        line.level = oldLevel + 1;
        line.capacityUnitsPerMinute = GetUpgradedProcessingCapacity(branch, line.level, oldCapacity);
        RefreshRuntimeAccountIfDocked();
        AddQuestEventMetric("base_processing_level", branch.ToString(), line.level);
        RefreshQuestProgress(true);

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
        for (int i = 0; cost != null && i < cost.Count; i++)
        {
            CascadeItemAmount item = cost[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;
            AddQuestEventMetric("resource_spent", item.itemId, item.amount);
        }

        int oldLevel = line.level;
        float oldCapacity = line.capacityUnitsPerMinute;
        line.level = oldLevel + 1;
        line.capacityUnitsPerMinute = GetUpgradedCascadeCapacity(type, line.level, oldCapacity);
        RefreshRuntimeAccountIfDocked();
        AddQuestEventMetric("cascade_line_level", type.ToString(), line.level);
        RefreshQuestProgress(true);

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
            && progress.shipFuelTank.GetAmount(fuelId) < capacity.fuelTankCapacityKg - 0.001f;
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

        int fuelMoved = RefillTankFromStorage(progress.shipFuelTank, fuelId, capacity.fuelTankCapacityKg, storage);
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
            && progress.shipFuelTank.GetAmount(fuelId) < fuelTargetKg - 0.001f;
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
            float currentFuelKg = progress.shipFuelTank.GetAmount(fuelId);
            fuelAddedKg = progress.shipFuelTank.Add(
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

        if (!TryGetAvailableBaseProcessingInput(branch, storage, out string inputItemId, out int available) || available <= 0)
        {
            return FailBaseProcessing("No processable input for " + SessionExtractionIndustry.GetProcessingDisplayName(branch) + ".", out message);
        }

        BaseProcessingLineState line = progress.baseIndustry.GetProcessing(branch);
        BaseProcessingFacilityState facility = GetBaseProcessingFacilityState("legacy_batch_" + branch, branch, line.level);
        int neededForCycle = Mathf.Max(0, facility.cycleInputUnits - facility.BunkerLoadUnits);
        int moved = 0;
        if (neededForCycle > 0)
        {
            moved = Mathf.Min(Mathf.Min(neededForCycle, available), facility.BunkerFreeUnits);
            if (moved > 0 && storage.TrySpendResource(inputItemId, moved))
            {
                AddQuestEventMetric("resource_spent", inputItemId, moved);
                int loaded = facility.AddBunker(inputItemId, moved);
                if (loaded < moved)
                {
                    storage.AddResource(inputItemId, moved - loaded);
                }

                moved = loaded;
            }
        }

        if (facility.BunkerLoadUnits < facility.cycleInputUnits)
        {
            message = "Loaded " + moved + " input units, waiting for " + facility.cycleInputUnits + " units in the bunker.";
            lastAccountMessage = message;
            RefreshRuntimeAccountIfDocked();
            RefreshQuestProgress(true);
            return moved > 0;
        }

        int processed = CompleteBaseProcessingFacilityCycle(facility);
        int collected = CollectBaseProcessingOutputsToStorage(facility, storage);
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

        message = "Processed " + processed + " units in "
            + SessionExtractionIndustry.GetProcessingDisplayName(branch)
            + ". Collected whole outputs: " + collected + ".";
        lastAccountMessage = message;
        return processed > 0;
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

    public BaseProcessingFacilityState GetBaseProcessingFacilityState(string facilityId, BaseProcessingBranch branch, int buildingLevel)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        return progress.baseIndustry.GetProcessingFacility(facilityId, branch, buildingLevel);
    }

    public List<string> GetBaseProcessingInputItemIds(BaseProcessingBranch branch)
    {
        EnsureSessionConfigLoaded();
        List<string> itemIds = new List<string>();

        switch (branch)
        {
            case BaseProcessingBranch.Ore:
                if (sessionConfig != null && sessionConfig.oreTypes != null)
                {
                    for (int i = 0; i < sessionConfig.oreTypes.Count; i++)
                    {
                        AddUniqueItemId(itemIds, sessionConfig.oreTypes[i]?.oreItemId);
                    }
                }
                break;
            case BaseProcessingBranch.Gas:
                if (sessionConfig != null && sessionConfig.gasCondensateTypes != null)
                {
                    for (int i = 0; i < sessionConfig.gasCondensateTypes.Count; i++)
                    {
                        AddUniqueItemId(itemIds, sessionConfig.gasCondensateTypes[i]?.condensateItemId);
                    }
                }
                break;
            case BaseProcessingBranch.AutomatonDismantling:
                AddUniqueItemId(itemIds, SessionExtractionConstants.BrokenAutomatonItemId);
                break;
            case BaseProcessingBranch.LeviathanProcessing:
                if (sessionConfig != null && sessionConfig.leviathanTypes != null)
                {
                    for (int i = 0; i < sessionConfig.leviathanTypes.Count; i++)
                    {
                        AddUniqueItemId(itemIds, sessionConfig.leviathanTypes[i]?.carcassItemId);
                    }
                }
                break;
            case BaseProcessingBranch.CyberneticDeciphering:
                AddUniqueItemId(itemIds, SessionExtractionConstants.RockInfoItemId);
                break;
        }

        return itemIds;
    }

    public List<string> GetBaseProcessingOutputItemIds(BaseProcessingBranch branch)
    {
        List<string> outputs = new List<string>();
        List<string> inputs = GetBaseProcessingInputItemIds(branch);
        for (int i = 0; i < inputs.Count; i++)
        {
            List<ProcessingOutputShare> composition = GetProcessingComposition(branch, inputs[i]);
            for (int outputIndex = 0; outputIndex < composition.Count; outputIndex++)
            {
                AddUniqueItemId(outputs, composition[outputIndex].itemId);
            }
        }

        return outputs;
    }

    public bool TryLoadBaseProcessingInput(
        string facilityId,
        BaseProcessingBranch branch,
        int buildingLevel,
        string itemId,
        int amount,
        out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        message = "";

        if (!IsDockedAtCapital())
        {
            message = "Переработка доступна только в городе.";
            lastAccountMessage = message;
            return false;
        }

        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || GetProcessingComposition(branch, itemId).Count == 0)
        {
            message = "Этот ресурс не подходит для выбранной переработки.";
            lastAccountMessage = message;
            return false;
        }

        PortStorageState storage = GetCapitalStorageState();
        BaseProcessingFacilityState facility = GetBaseProcessingFacilityState(facilityId, branch, buildingLevel);
        if (storage == null || facility == null)
        {
            message = "Нет склада или здания переработки.";
            lastAccountMessage = message;
            return false;
        }

        int available = storage.GetResourceAmount(itemId);
        int moved = Mathf.Min(Mathf.Min(Mathf.Max(0, amount), available), facility.BunkerFreeUnits);
        if (moved <= 0)
        {
            message = facility.BunkerFreeUnits <= 0 ? "Бункер заполнен." : "На складе нет выбранного сырья.";
            lastAccountMessage = message;
            return false;
        }

        if (!storage.TrySpendResource(itemId, moved))
        {
            message = "Не удалось забрать сырье со склада.";
            lastAccountMessage = message;
            return false;
        }

        int loaded = facility.AddBunker(itemId, moved);
        AddQuestEventMetric("resource_spent", itemId, loaded);
        if (loaded < moved)
        {
            storage.AddResource(itemId, moved - loaded);
        }

        message = "Загружено в бункер: " + (sessionConfig != null ? sessionConfig.GetItemNameRu(itemId) : itemId) + " x" + loaded + ".";
        lastAccountMessage = message;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);
        return loaded > 0;
    }

    public bool TryLoadAllBaseProcessingInputs(
        string facilityId,
        BaseProcessingBranch branch,
        int buildingLevel,
        out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        message = "";

        PortStorageState storage = GetCapitalStorageState();
        BaseProcessingFacilityState facility = GetBaseProcessingFacilityState(facilityId, branch, buildingLevel);
        if (!IsDockedAtCapital() || storage == null || facility == null)
        {
            message = "Нет доступа к складу города.";
            lastAccountMessage = message;
            return false;
        }

        int totalMoved = 0;
        List<string> inputs = GetBaseProcessingInputItemIds(branch);
        for (int i = 0; i < inputs.Count && facility.BunkerFreeUnits > 0; i++)
        {
            string itemId = inputs[i];
            int available = storage.GetResourceAmount(itemId);
            int moved = Mathf.Min(available, facility.BunkerFreeUnits);
            if (moved <= 0 || !storage.TrySpendResource(itemId, moved)) continue;

            int loaded = facility.AddBunker(itemId, moved);
            AddQuestEventMetric("resource_spent", itemId, loaded);
            totalMoved += loaded;
            if (loaded < moved)
            {
                storage.AddResource(itemId, moved - loaded);
            }
        }

        message = totalMoved > 0
            ? "Загружено сырья: " + totalMoved + "."
            : facility.BunkerFreeUnits <= 0 ? "Бункер заполнен." : "На складе нет подходящего сырья.";
        lastAccountMessage = message;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);
        return totalMoved > 0;
    }

    public bool TryClearBaseProcessingBunker(
        string facilityId,
        BaseProcessingBranch branch,
        int buildingLevel,
        out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();

        PortStorageState storage = GetCapitalStorageState();
        BaseProcessingFacilityState facility = GetBaseProcessingFacilityState(facilityId, branch, buildingLevel);
        if (!IsDockedAtCapital() || storage == null || facility == null)
        {
            message = "Нет доступа к складу города.";
            lastAccountMessage = message;
            return false;
        }

        int returned = 0;
        facility.bunker ??= new List<ResourceStack>();
        for (int i = facility.bunker.Count - 1; i >= 0; i--)
        {
            ResourceStack stack = facility.bunker[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0)
            {
                facility.bunker.RemoveAt(i);
                continue;
            }

            returned += storage.AddResource(stack.resourceId, stack.amount);
            facility.bunker.RemoveAt(i);
        }

        facility.cycleElapsedSeconds = 0f;
        message = returned > 0 ? "Бункер очищен, возвращено: " + returned + "." : "Бункер пуст.";
        lastAccountMessage = message;
        RefreshRuntimeAccountIfDocked();
        return returned > 0;
    }

    public bool TryCollectBaseProcessingOutputs(
        string facilityId,
        BaseProcessingBranch branch,
        int buildingLevel,
        out string message)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();

        PortStorageState storage = GetCapitalStorageState();
        BaseProcessingFacilityState facility = GetBaseProcessingFacilityState(facilityId, branch, buildingLevel);
        if (!IsDockedAtCapital() || storage == null || facility == null)
        {
            message = "Нет доступа к складу города.";
            lastAccountMessage = message;
            return false;
        }

        int collected = 0;
        facility.outputBuffers ??= new List<BaseProcessingOutputBufferState>();
        for (int i = 0; i < facility.outputBuffers.Count; i++)
        {
            BaseProcessingOutputBufferState buffer = facility.outputBuffers[i];
            if (buffer == null || string.IsNullOrWhiteSpace(buffer.itemId) || buffer.readyAmount <= 0) continue;

            int readyAmount = buffer.readyAmount;
            collected += storage.AddResource(buffer.itemId, readyAmount);
            AddQuestEventMetric("resource_acquired", buffer.itemId, readyAmount);
            buffer.readyAmount = 0;
            buffer.Normalize();
        }

        message = collected > 0 ? "Забрано готовых выходов: " + collected + "." : "Готовых целых единиц пока нет.";
        lastAccountMessage = message;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);
        return collected > 0;
    }

    private static void AddUniqueItemId(List<string> itemIds, string itemId)
    {
        if (itemIds == null || string.IsNullOrWhiteSpace(itemId)) return;
        itemId = itemId.Trim();
        if (!itemIds.Contains(itemId))
        {
            itemIds.Add(itemId);
        }
    }

    private List<ProcessingOutputShare> GetProcessingComposition(BaseProcessingBranch branch, string inputItemId)
    {
        List<ProcessingOutputShare> outputs = new List<ProcessingOutputShare>();
        if (string.IsNullOrWhiteSpace(inputItemId)) return outputs;
        EnsureSessionConfigLoaded();

        switch (branch)
        {
            case BaseProcessingBranch.Ore:
            {
                OreTypeConfig oreType = FindOreTypeByItemId(inputItemId);
                if (oreType != null && oreType.composition != null)
                {
                    for (int i = 0; i < oreType.composition.Count; i++)
                    {
                        OreMineralCompositionConfig composition = oreType.composition[i];
                        if (composition == null) continue;
                        outputs.Add(new ProcessingOutputShare(composition.mineralItemId, composition.share));
                    }
                }
                break;
            }
            case BaseProcessingBranch.Gas:
            {
                GasCondensateTypeConfig gasType = FindGasCondensateTypeByItemId(inputItemId);
                if (gasType != null && gasType.composition != null)
                {
                    for (int i = 0; i < gasType.composition.Count; i++)
                    {
                        GasCondensateCompositionConfig composition = gasType.composition[i];
                        if (composition == null) continue;
                        outputs.Add(new ProcessingOutputShare(composition.itemId, composition.share));
                    }
                }
                break;
            }
            case BaseProcessingBranch.AutomatonDismantling:
                if (inputItemId == SessionExtractionConstants.BrokenAutomatonItemId)
                {
                    outputs.Add(new ProcessingOutputShare(SessionExtractionConstants.MechanismsItemId, 0.40f));
                    outputs.Add(new ProcessingOutputShare(SessionExtractionConstants.ToolsItemId, 0.20f));
                    outputs.Add(new ProcessingOutputShare(SessionExtractionConstants.AutomatonCoreItemId, 0.20f));
                    outputs.Add(new ProcessingOutputShare(SessionExtractionConstants.DesignExperienceItemId, 0.10f));
                }
                break;
            case BaseProcessingBranch.LeviathanProcessing:
            {
                LeviathanTypeConfig leviathanType = FindLeviathanTypeByCarcassItemId(inputItemId);
                if (leviathanType != null && leviathanType.composition != null)
                {
                    for (int i = 0; i < leviathanType.composition.Count; i++)
                    {
                        LeviathanButcheryCompositionConfig composition = leviathanType.composition[i];
                        if (composition == null) continue;
                        outputs.Add(new ProcessingOutputShare(composition.itemId, composition.share));
                    }
                }
                break;
            }
            case BaseProcessingBranch.CyberneticDeciphering:
                if (inputItemId == SessionExtractionConstants.RockInfoItemId)
                {
                    outputs.Add(new ProcessingOutputShare(SessionExtractionConstants.FundamentalExperienceItemId, 0.85f));
                    outputs.Add(new ProcessingOutputShare(SessionExtractionConstants.DesignExperienceItemId, 0.15f));
                }
                break;
        }

        for (int i = outputs.Count - 1; i >= 0; i--)
        {
            ProcessingOutputShare output = outputs[i];
            if (string.IsNullOrWhiteSpace(output.itemId) || output.share <= 0f)
            {
                outputs.RemoveAt(i);
            }
        }

        return outputs;
    }

    private OreTypeConfig FindOreTypeByItemId(string oreItemId)
    {
        if (string.IsNullOrWhiteSpace(oreItemId) || sessionConfig == null || sessionConfig.oreTypes == null) return null;
        for (int i = 0; i < sessionConfig.oreTypes.Count; i++)
        {
            OreTypeConfig oreType = sessionConfig.oreTypes[i];
            if (oreType != null && oreType.oreItemId == oreItemId)
            {
                return oreType;
            }
        }

        return null;
    }

    private GasCondensateTypeConfig FindGasCondensateTypeByItemId(string condensateItemId)
    {
        if (string.IsNullOrWhiteSpace(condensateItemId) || sessionConfig == null || sessionConfig.gasCondensateTypes == null) return null;
        for (int i = 0; i < sessionConfig.gasCondensateTypes.Count; i++)
        {
            GasCondensateTypeConfig gasType = sessionConfig.gasCondensateTypes[i];
            if (gasType != null && gasType.condensateItemId == condensateItemId)
            {
                return gasType;
            }
        }

        return null;
    }

    private LeviathanTypeConfig FindLeviathanTypeByCarcassItemId(string carcassItemId)
    {
        if (string.IsNullOrWhiteSpace(carcassItemId) || sessionConfig == null || sessionConfig.leviathanTypes == null) return null;
        for (int i = 0; i < sessionConfig.leviathanTypes.Count; i++)
        {
            LeviathanTypeConfig leviathanType = sessionConfig.leviathanTypes[i];
            if (leviathanType != null && leviathanType.carcassItemId == carcassItemId)
            {
                return leviathanType;
            }
        }

        return null;
    }

    private int AdvanceBaseProcessingFacilities(float elapsedSeconds)
    {
        if (progress == null || progress.baseIndustry == null || elapsedSeconds <= 0f) return 0;
        progress.baseIndustry.Normalize();

        int completedCycles = 0;
        List<BaseProcessingFacilityState> facilities = progress.baseIndustry.processingFacilities;
        if (facilities == null || facilities.Count == 0) return 0;

        for (int i = 0; i < facilities.Count; i++)
        {
            completedCycles += AdvanceBaseProcessingFacility(facilities[i], elapsedSeconds);
        }

        return completedCycles;
    }

    private int AdvanceBaseCascadeProduction(DateTime utcNow)
    {
        if (progress == null || progress.baseIndustry == null) return 0;
        progress.baseIndustry.Normalize();

        List<CascadeProductionQueueItemState> queue = progress.baseIndustry.cascadeQueue;
        if (queue == null || queue.Count == 0) return 0;

        PortStorageState storage = GetCapitalStorageState();
        if (storage == null) return 0;

        int completed = 0;
        long nowTicks = utcNow.Ticks;
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            CascadeProductionQueueItemState item = queue[i];
            if (item == null)
            {
                queue.RemoveAt(i);
                continue;
            }

            item.Normalize();
            if (!item.IsCompleteAt(nowTicks)) continue;

            CompleteBaseCascadeQueueItem(item, storage);
            queue.RemoveAt(i);
            completed++;
        }

        if (completed > 0)
        {
            RefreshRuntimeAccountIfDocked();
        }

        return completed;
    }

    private void CompleteBaseCascadeQueueItem(CascadeProductionQueueItemState item, PortStorageState storage)
    {
        if (item == null || storage == null) return;
        item.Normalize();

        for (int i = 0; i < item.outputs.Count; i++)
        {
            CascadeItemAmount output = item.outputs[i];
            if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.amount <= 0) continue;
            storage.AddResource(output.itemId, output.amount);
            AddQuestEventMetric("resource_acquired", output.itemId, output.amount);
        }

        AddQuestEventMetric("cascade_order_completed", item.orderId, item.quantity);
        ApplyCascadeProductionLoad(item.loads);
        RefreshQuestProgress(true);
        lastAccountMessage = "Cascade complete: " + BuildCascadeOutputsText(item.outputs) + ".";
    }

    private int AdvanceBaseProcessingFacility(BaseProcessingFacilityState facility, float elapsedSeconds)
    {
        if (facility == null || elapsedSeconds <= 0f) return 0;
        facility.Normalize();
        if (facility.BunkerLoadUnits < facility.cycleInputUnits)
        {
            facility.cycleElapsedSeconds = 0f;
            return 0;
        }

        facility.cycleElapsedSeconds += elapsedSeconds;
        int completedCycles = 0;
        int guard = 0;
        while (guard < 1000
            && facility.cycleElapsedSeconds >= facility.cycleDurationSeconds
            && facility.BunkerLoadUnits >= facility.cycleInputUnits)
        {
            guard++;
            facility.cycleElapsedSeconds -= facility.cycleDurationSeconds;
            if (CompleteBaseProcessingFacilityCycle(facility) <= 0)
            {
                break;
            }

            completedCycles++;
        }

        if (facility.BunkerLoadUnits < facility.cycleInputUnits)
        {
            facility.cycleElapsedSeconds = 0f;
        }

        return completedCycles;
    }

    private int CompleteBaseProcessingFacilityCycle(BaseProcessingFacilityState facility)
    {
        if (facility == null) return 0;
        facility.Normalize();
        int batch = Mathf.Min(facility.cycleInputUnits, facility.BunkerLoadUnits);
        if (batch <= 0 || facility.BunkerLoadUnits < facility.cycleInputUnits) return 0;

        int seed = CreateProcessingSeed(facility);
        System.Random random = new System.Random(seed);
        int processed = 0;
        for (int i = 0; i < batch; i++)
        {
            string inputItemId = PickRandomBunkerItem(facility, random);
            if (string.IsNullOrWhiteSpace(inputItemId) || !facility.TrySpendBunker(inputItemId, 1))
            {
                continue;
            }

            AddProcessingOutputFractions(facility, inputItemId, 1);
            processed++;
        }

        if (processed > 0)
        {
            BaseProcessingLineState line = progress?.baseIndustry?.GetProcessing(facility.branch);
            if (line != null)
            {
                line.totalProcessedUnits += processed;
            }

            facility.totalProcessedUnits += processed;
        }

        return processed;
    }

    private int CreateProcessingSeed(BaseProcessingFacilityState facility)
    {
        int seed = 17;
        string id = facility != null ? facility.facilityId ?? "" : "";
        for (int i = 0; i < id.Length; i++)
        {
            seed = unchecked(seed * 31 + id[i]);
        }

        seed = unchecked(seed * 31 + Mathf.RoundToInt((facility != null ? facility.totalProcessedUnits : 0f) * 10f));
        return seed == int.MinValue ? 17 : Mathf.Abs(seed);
    }

    private static string PickRandomBunkerItem(BaseProcessingFacilityState facility, System.Random random)
    {
        if (facility == null || random == null || facility.BunkerLoadUnits <= 0) return "";
        int target = random.Next(facility.BunkerLoadUnits);
        facility.bunker ??= new List<ResourceStack>();
        for (int i = 0; i < facility.bunker.Count; i++)
        {
            ResourceStack stack = facility.bunker[i];
            if (stack == null || stack.amount <= 0) continue;

            if (target < stack.amount)
            {
                return stack.resourceId;
            }

            target -= stack.amount;
        }

        return "";
    }

    private void AddProcessingOutputFractions(BaseProcessingFacilityState facility, string inputItemId, int inputAmount)
    {
        if (facility == null || string.IsNullOrWhiteSpace(inputItemId) || inputAmount <= 0) return;

        List<ProcessingOutputShare> composition = GetProcessingComposition(facility.branch, inputItemId);
        for (int i = 0; i < composition.Count; i++)
        {
            ProcessingOutputShare output = composition[i];
            if (string.IsNullOrWhiteSpace(output.itemId) || output.share <= 0f) continue;

            float produced = inputAmount * output.share * Mathf.Clamp01(facility.efficiency);
            if (produced <= 0f) continue;

            BaseProcessingOutputBufferState buffer = facility.GetOutputBuffer(output.itemId, true);
            if (buffer == null) continue;

            buffer.fractionalAmount += produced;
            int whole = Mathf.FloorToInt(buffer.fractionalAmount + 0.0001f);
            if (whole > 0)
            {
                buffer.readyAmount += whole;
                buffer.fractionalAmount -= whole;
            }

            buffer.Normalize();
        }
    }

    private int CollectBaseProcessingOutputsToStorage(BaseProcessingFacilityState facility, PortStorageState storage)
    {
        if (facility == null || storage == null) return 0;
        int collected = 0;
        facility.outputBuffers ??= new List<BaseProcessingOutputBufferState>();
        for (int i = 0; i < facility.outputBuffers.Count; i++)
        {
            BaseProcessingOutputBufferState buffer = facility.outputBuffers[i];
            if (buffer == null || string.IsNullOrWhiteSpace(buffer.itemId) || buffer.readyAmount <= 0) continue;

            collected += storage.AddResource(buffer.itemId, buffer.readyAmount);
            buffer.readyAmount = 0;
            buffer.Normalize();
        }

        return collected;
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
        AddQuestEventMetric("resource_spent", oreType.oreItemId, batchKg);

        int outputTotal = AddOreProcessingOutputs(storage, oreType, batchKg);
        AddQuestEventMetric("resource_acquired", "any", outputTotal);
        line.totalProcessedUnits += batchKg;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

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
        AddQuestEventMetric("resource_spent", gasType.condensateItemId, batch);

        int outputTotal = AddGasProcessingOutputs(storage, gasType, batch);
        AddQuestEventMetric("resource_acquired", "any", outputTotal);
        line.totalProcessedUnits += batch;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

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
        AddQuestEventMetric("resource_spent", inputItemId, batch);

        int outputTotal = 0;
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.MechanismsItemId, batch, 0.40f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.ToolsItemId, batch, 0.20f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.AutomatonCoreItemId, batch, 0.20f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.10f);
        AddQuestEventMetric("resource_acquired", "any", outputTotal);

        line.totalProcessedUnits += batch;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

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
        AddQuestEventMetric("resource_spent", leviathanType.carcassItemId, batch);

        int outputTotal = AddLeviathanProcessingOutputs(storage, leviathanType, batch);
        AddQuestEventMetric("resource_acquired", "any", outputTotal);

        line.totalProcessedUnits += batch;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

        message = "Processed " + batch + " kg " + leviathanType.carcassItemId + " into " + outputTotal + " leviathan materials.";
        lastAccountMessage = message;
        return true;
    }

    private int AddLeviathanProcessingOutputs(PortStorageState storage, LeviathanTypeConfig leviathanType, int batch)
    {
        if (leviathanType != null && leviathanType.composition != null && leviathanType.composition.Count > 0)
        {
            int outputTotal = 0;
            for (int i = 0; i < leviathanType.composition.Count; i++)
            {
                LeviathanButcheryCompositionConfig output = leviathanType.composition[i];
                if (output == null || string.IsNullOrWhiteSpace(output.itemId) || output.share <= 0f) continue;
                outputTotal += AddScaledProcessingOutput(storage, output.itemId, batch, output.share);
            }

            return outputTotal;
        }

        int fallbackTotal = 0;
        fallbackTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.LeviathanMeatItemId, batch, 0.35f);
        fallbackTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.LeviathanFatItemId, batch, 0.20f);
        fallbackTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.LeviathanHideItemId, batch, 0.20f);
        fallbackTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.ClaudiumItemId, batch, 0.08f);
        fallbackTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.LeviathanSinewItemId, batch, 0.10f);
        fallbackTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.BonePlateItemId, batch, 0.07f);
        return fallbackTotal;
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
        AddQuestEventMetric("resource_spent", infoItemId, batch);

        int outputTotal = 0;
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.FundamentalExperienceItemId, batch, 0.85f);
        outputTotal += AddScaledProcessingOutput(storage, SessionExtractionConstants.DesignExperienceItemId, batch, 0.15f);
        AddQuestEventMetric("resource_acquired", "any", outputTotal);

        line.totalProcessedUnits += batch;
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

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

                if (StarterCascadeOrderNeedsOutput(order, storage, GetPendingCascadeOutputAmount(order)))
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

    private static bool StarterCascadeOrderNeedsOutput(CascadeProductionOrderDefinition order, PortStorageState storage, int pendingOutputAmount)
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
            if (storage.GetResourceAmount(output.itemId) + Mathf.Max(0, pendingOutputAmount) < targetStock)
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

    private int GetPendingCascadeOutputAmount(CascadeProductionOrderDefinition order)
    {
        if (progress == null || progress.baseIndustry == null || order == null || order.outputs == null)
        {
            return 0;
        }

        int total = 0;
        progress.baseIndustry.Normalize();
        List<CascadeProductionQueueItemState> queue = progress.baseIndustry.cascadeQueue;
        if (queue == null || queue.Count == 0) return 0;

        for (int i = 0; i < queue.Count; i++)
        {
            CascadeProductionQueueItemState queued = queue[i];
            if (queued == null || queued.outputs == null) continue;
            for (int outputIndex = 0; outputIndex < order.outputs.Count; outputIndex++)
            {
                CascadeItemAmount expectedOutput = order.outputs[outputIndex];
                if (expectedOutput == null || string.IsNullOrWhiteSpace(expectedOutput.itemId)) continue;
                for (int queuedOutputIndex = 0; queuedOutputIndex < queued.outputs.Count; queuedOutputIndex++)
                {
                    CascadeItemAmount queuedOutput = queued.outputs[queuedOutputIndex];
                    if (queuedOutput != null && queuedOutput.itemId == expectedOutput.itemId)
                    {
                        total += Mathf.Max(0, queuedOutput.amount);
                    }
                }
            }
        }

        return total;
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
        return EstimateBaseCascadeOrder(order, 1);
    }

    public CascadeProductionEstimate EstimateBaseCascadeOrder(CascadeProductionOrderDefinition order, int quantity)
    {
        EnsureProgressInitialized();
        EnsureSessionConfigLoaded();
        quantity = Mathf.Max(1, quantity);

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
            int required = MultiplyCascadeAmount(input.amount, quantity);
            if (available >= required) continue;

            estimate.missingInputs.Add(new CascadeResourceGap
            {
                itemId = input.itemId,
                required = required,
                available = available,
                missing = Mathf.Max(0, required - available)
            });
        }

        for (int i = 0; i < order.loads.Count; i++)
        {
            CascadeProductionLoad load = order.loads[i];
            if (load == null || load.loadUnits <= 0f) continue;

            CascadeProductionLineState line = progress.baseIndustry.GetProduction(load.type);
            float totalLoad = load.loadUnits * quantity;
            float minutes = totalLoad / Mathf.Max(0.1f, line.capacityUnitsPerMinute);
            estimate.totalLoadUnits += totalLoad;
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
        return TryQueueBaseCascadeOrder(order, 1, out message);
    }

    public bool TryQueueBaseCascadeOrder(CascadeProductionOrderDefinition order, int quantity, out string message)
    {
        message = "";
        quantity = Mathf.Max(1, quantity);
        CascadeProductionEstimate estimate = EstimateBaseCascadeOrder(order, quantity);
        if (order == null || !estimate.canRun)
        {
            message = string.IsNullOrWhiteSpace(estimate.blockedReason)
                ? "Cascade order is blocked."
                : estimate.blockedReason;
            lastAccountMessage = message;
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        if (progress.baseIndustry.ActiveCascadeQueueCount >= BaseCascadeQueueSlotCount)
        {
            message = "Cascade queue is full.";
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

            int amount = MultiplyCascadeAmount(input.amount, quantity);
            if (!storage.TrySpendResource(input.itemId, amount))
            {
                message = "Cascade blocked: could not spend " + amount + " " + input.itemId + ".";
                lastAccountMessage = message;
                return false;
            }

            AddQuestEventMetric("resource_spent", input.itemId, amount);
        }

        DateTime startUtc = GetProcessUtcNow();
        if (progress.lastProcessUtcTicks <= 0L)
        {
            progress.lastProcessUtcTicks = startUtc.Ticks;
        }

        float durationSeconds = Mathf.Max(1f, estimate.bottleneckMinutes * 60f);
        long durationTicks = TimeSpan.FromSeconds(durationSeconds).Ticks;
        CascadeProductionQueueItemState queued = CreateCascadeQueueItem(order, quantity, startUtc.Ticks, startUtc.Ticks + durationTicks);
        progress.baseIndustry.cascadeQueue.Add(queued);
        RefreshRuntimeAccountIfDocked();
        RefreshQuestProgress(true);

        message = "Cascade queued: " + BuildCascadeOutputsText(queued.outputs)
            + ". Bottleneck: " + SessionExtractionIndustry.GetProductionDisplayName(estimate.bottleneck)
            + " ~" + estimate.bottleneckMinutes.ToString("F1") + " min.";
        lastAccountMessage = message;
        return true;
    }

    private CascadeProductionQueueItemState CreateCascadeQueueItem(
        CascadeProductionOrderDefinition order,
        int quantity,
        long startedUtcTicks,
        long completeUtcTicks)
    {
        quantity = Mathf.Max(1, quantity);
        CascadeProductionQueueItemState item = new CascadeProductionQueueItemState
        {
            queueId = order.orderId + "_" + startedUtcTicks + "_" + progress.baseIndustry.ActiveCascadeQueueCount,
            orderId = order.orderId,
            displayName = order.displayName,
            quantity = quantity,
            startedUtcTicks = Math.Max(0L, startedUtcTicks),
            completeUtcTicks = Math.Max(startedUtcTicks + TimeSpan.TicksPerSecond, completeUtcTicks),
            inputs = CloneCascadeItems(order.inputs, quantity),
            outputs = CloneCascadeItems(order.outputs, quantity),
            loads = CloneCascadeLoads(order.loads, quantity)
        };
        item.Normalize();
        return item;
    }

    private static List<CascadeItemAmount> CloneCascadeItems(List<CascadeItemAmount> source, int quantity)
    {
        List<CascadeItemAmount> result = new List<CascadeItemAmount>();
        if (source == null) return result;

        quantity = Mathf.Max(1, quantity);
        for (int i = 0; i < source.Count; i++)
        {
            CascadeItemAmount item = source[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;
            result.Add(new CascadeItemAmount
            {
                itemId = item.itemId,
                amount = MultiplyCascadeAmount(item.amount, quantity)
            });
        }

        return result;
    }

    private static List<CascadeProductionLoad> CloneCascadeLoads(List<CascadeProductionLoad> source, int quantity)
    {
        List<CascadeProductionLoad> result = new List<CascadeProductionLoad>();
        if (source == null) return result;

        quantity = Mathf.Max(1, quantity);
        for (int i = 0; i < source.Count; i++)
        {
            CascadeProductionLoad load = source[i];
            if (load == null || load.loadUnits <= 0f) continue;
            result.Add(new CascadeProductionLoad
            {
                type = load.type,
                loadUnits = load.loadUnits * quantity
            });
        }

        return result;
    }

    private static int MultiplyCascadeAmount(int amount, int quantity)
    {
        amount = Mathf.Max(0, amount);
        quantity = Mathf.Max(1, quantity);
        if (amount == 0) return 0;
        return amount > int.MaxValue / quantity ? int.MaxValue : amount * quantity;
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
            float elapsedSeconds = (float)Math.Min(86400d, (utcNow.Ticks - previousProcessTicks) / (double)TimeSpan.TicksPerSecond);
            completedCycles += AdvanceBaseProcessingFacilities(elapsedSeconds);
            completedCycles += AdvanceBaseCascadeProduction(utcNow);
            completedCycles += AdvanceCourierServiceOrders(utcNow.Ticks);

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
        string savePath = ResolvePersistentProgressSavePath();
        bool saveDeleted = DeletePersistentProgressSaveFile(savePath, out string deleteError);
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

        MarkPersistentProgressDirty();
        SavePersistentProgressNow();

        lastAccountMessage = saveDeleted
            ? "Account progress reset and save cleared."
            : "Account progress reset, but save cleanup reported: " + deleteError;
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
        if (!EnsureTechnologyLevelRequirementsPaid(technology, state, out _))
        {
            return 0;
        }

        if (progress.lastProcessUtcTicks <= 0 || utcNow.Ticks <= progress.lastProcessUtcTicks)
        {
            return 0;
        }

        float elapsedSeconds = (float)Math.Min(86400d, (utcNow.Ticks - progress.lastProcessUtcTicks) / (double)TimeSpan.TicksPerSecond);
        float generatedSp = GetArchiveKnowledgeSpPerMinute(technology.categoryId) * elapsedSeconds / 60f;
        if (generatedSp <= 0f)
        {
            return 0;
        }

        int nextLevel = GetTechnologyNextLevel(technology);
        int levelCost = GetTechnologyLevelSpCost(technology, nextLevel);
        state.currentLevelSpProgress = Mathf.Min(levelCost, state.currentLevelSpProgress + generatedSp);
        state.activeCycleStartUtcTicks = 0;
        state.activeCycleEndUtcTicks = 0;
        MarkPersistentProgressDirty();

        if (state.currentLevelSpProgress + 0.001f >= levelCost)
        {
            CompleteTechnologyLevel(technology, state);
            return 1;
        }

        lastAccountMessage = "Архивы добавили " + generatedSp.ToString("0.#") + " SP в " + GetTechnologyDisplayName(technology) + ".";
        return 1;
    }

    private void CompleteTechnologyResearch(TechnologyConfig technology)
    {
        if (technology == null || progress == null) return;

        progress.CompleteTechnology(technology.id);
        TechnologyResearchProgress state = progress.GetTechnologyProgress(technology.id, true);
        state.completedCycles = Mathf.Max(state.completedCycles, GetTechnologyMaxLevel(technology));
        state.currentLevelSpProgress = 0f;
        state.currentLevelRequirementsPaid = false;
        state.activeCycleStartUtcTicks = 0;
        state.activeCycleEndUtcTicks = 0;

        if (progress.activeResearchTechnologyId == technology.id)
        {
            progress.activeResearchTechnologyId = "";
        }

        ShipAssemblyBuilder.AutoInstallRequiredModules(ActiveCatalog, progress, out _);
        ApplySelectedShip();
        string completionMessage = "Технология завершена: " + GetTechnologyDisplayName(technology);
        AddQuestEventMetric("technology_completed", technology.id, 1);
        RefreshQuestProgress(true);
        lastAccountMessage = completionMessage;
    }

    private void CompleteTechnologyLevel(TechnologyConfig technology, TechnologyResearchProgress state)
    {
        if (technology == null || state == null || progress == null) return;

        state.completedCycles = Mathf.Clamp(state.completedCycles + 1, 0, GetTechnologyMaxLevel(technology));
        state.currentLevelSpProgress = 0f;
        state.currentLevelRequirementsPaid = false;
        state.activeCycleStartUtcTicks = 0;
        state.activeCycleEndUtcTicks = 0;

        if (state.completedCycles >= GetTechnologyMaxLevel(technology))
        {
            CompleteTechnologyResearch(technology);
            return;
        }

        if (progress.activeResearchTechnologyId == technology.id)
        {
            progress.activeResearchTechnologyId = "";
        }

        MarkPersistentProgressDirty();
        lastAccountMessage = "Уровень знания изучен: " + GetTechnologyDisplayName(technology)
            + " L" + state.completedCycles.ToString() + ".";
    }

    private bool EnsureTechnologyLevelRequirementsPaid(TechnologyConfig technology, TechnologyResearchProgress state, out string reason)
    {
        reason = "";
        if (technology == null || state == null)
        {
            reason = "Знание не найдено.";
            return false;
        }

        if (state.completedCycles >= GetTechnologyMaxLevel(technology))
        {
            return true;
        }

        if (state.currentLevelRequirementsPaid)
        {
            return true;
        }

        PortStorageState storage = progress.GetPortStorageState(GetCapitalPortId(), true);
        if (!TrySpendTechnologyCycleCost(storage, technology, out reason))
        {
            return false;
        }

        state.currentLevelRequirementsPaid = true;
        MarkPersistentProgressDirty();
        return true;
    }

    private int GetTechnologyMaxLevel(TechnologyConfig technology)
    {
        return technology == null
            ? 1
            : Mathf.Clamp(Mathf.Max(1, technology.requiredCycles), 1, KnowledgeMaxLevel);
    }

    private int GetTechnologyNextLevel(TechnologyConfig technology)
    {
        if (technology == null) return 1;
        TechnologyResearchProgress state = progress != null ? progress.GetTechnologyProgress(technology.id, false) : null;
        int completedLevels = state != null ? state.completedCycles : 0;
        return Mathf.Clamp(completedLevels + 1, 1, GetTechnologyMaxLevel(technology));
    }

    private bool CanApplyKnowledgeSpPackage(KnowledgeSpPackageState package, TechnologyConfig technology, out string reason)
    {
        reason = "";
        if (package == null || package.amountSp <= 0)
        {
            reason = "SP-пакет пуст.";
            return false;
        }

        if (technology == null)
        {
            reason = "Знание не найдено.";
            return false;
        }

        if (progress.IsTechnologyCompleted(technology.id))
        {
            reason = "Знание уже изучено.";
            return false;
        }

        if (!IsKnowledgeAvailableForResearch(technology))
        {
            reason = "Сначала нужна книга/право на знание.";
            return false;
        }

        if (!AreTechnologyPrerequisitesCompleted(technology, out reason))
        {
            return false;
        }

        string scope = string.IsNullOrWhiteSpace(package.scopeKind) ? "universal" : package.scopeKind.Trim().ToLowerInvariant();
        if (scope == "universal")
        {
            return true;
        }

        if (scope == "category")
        {
            bool matches = string.Equals(package.scopeId, technology.categoryId, StringComparison.OrdinalIgnoreCase);
            if (!matches) reason = "SP-пакет не подходит к рубрике знания.";
            return matches;
        }

        if (scope == "tree" || scope == "branch")
        {
            bool matches = string.Equals(package.scopeId, technology.branch, StringComparison.OrdinalIgnoreCase);
            if (!matches) reason = "SP-пакет не подходит к древу знания.";
            return matches;
        }

        reason = "Неизвестный тип SP-пакета.";
        return false;
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
            reason = "На складе столицы не хватает ресурсов для уровня знания.";
            return false;
        }

        if (technology.cycleCost == null) return true;

        for (int i = 0; i < technology.cycleCost.Count; i++)
        {
            TechnologyCostConfig cost = technology.cycleCost[i];
            if (cost == null || string.IsNullOrWhiteSpace(cost.itemId) || cost.amount <= 0) continue;
            storage.TrySpendResource(cost.itemId, cost.amount);
            AddQuestEventMetric("resource_spent", cost.itemId, cost.amount);
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
            ship.hullThrustOutput = 1f;
            ship.sideInput = 0f;
            ship.turnInput = 0f;
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
        string fuelId = ship != null && !string.IsNullOrWhiteSpace(ship.fuelResourceId)
            ? ship.fuelResourceId
            : GetStartingFuelId();
        string claudiumId = GetClaudiumResourceId(ship);

        float emptyMassKg = capacity.emptyMassKg > 0f
            ? capacity.emptyMassKg
            : ship != null ? ship.baseMass : 0f;
        float cruiseSpeedMS = activeSortieReturn && zone.returnCruiseSpeedMS > 0f
            ? zone.returnCruiseSpeedMS
            : 35f * Mathf.Clamp(returnPowerLever, 0.1f, 1f);
        float coalBurnKgPerSecond = ship != null
            ? Mathf.Max(0f, ship.fuelConsumptionKgPerMinute) * ship.CurrentFuelConsumptionMultiplier / 60f
            : 0f;

        return new SortieReturnProfile
        {
            emptyMassKg = emptyMassKg,
            cargoMassKg = progress.GetShipPayloadMassKg(sessionConfig),
            cruiseSpeedMS = cruiseSpeedMS,
            coalBurnKgPerSecond = coalBurnKgPerSecond,
            claudiumBurnKgPerSecond = 0f,
            currentCoalKg = progress.shipFuelTank.GetAmount(fuelId),
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
            AddQuestEventMetric("resource_acquired", stack.resourceId, stack.amount);
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
        ApplyCascadeProductionLoad(order.loads);
    }

    private void ApplyCascadeProductionLoad(List<CascadeProductionLoad> loads)
    {
        if (progress == null || loads == null) return;

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();

        for (int i = 0; i < loads.Count; i++)
        {
            CascadeProductionLoad load = loads[i];
            if (load == null || load.loadUnits <= 0f) continue;

            CascadeProductionLineState line = progress.baseIndustry.GetProduction(load.type);
            line.totalLoadApplied += load.loadUnits;
        }
    }

    private readonly struct CourierResourceSpec
    {
        public readonly string itemId;
        public readonly int minAmount;
        public readonly int maxAmount;
        public readonly int freightValue;

        public CourierResourceSpec(string itemId, int minAmount, int maxAmount, int freightValue)
        {
            this.itemId = itemId ?? "";
            this.minAmount = Mathf.Max(1, minAmount);
            this.maxAmount = Mathf.Max(this.minAmount, maxAmount);
            this.freightValue = Mathf.Max(1, freightValue);
        }
    }

    private static string BuildCascadeOutputsText(CascadeProductionOrderDefinition order)
    {
        return order != null ? BuildCascadeOutputsText(order.outputs) : "nothing";
    }

    private static string BuildCascadeOutputsText(List<CascadeItemAmount> outputs)
    {
        if (outputs == null || outputs.Count == 0)
        {
            return "nothing";
        }

        List<string> parts = new List<string>();
        for (int i = 0; i < outputs.Count; i++)
        {
            CascadeItemAmount output = outputs[i];
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
        AddQuestEventMetric("resource_spent", kitItemId, 1);

        progress.InstallModule(slot.slotId, module.partId);
        ApplySelectedShip();
        RefreshRuntimeAccountIfDocked();
        AddQuestEventMetric("ship_module_installed", module.partId, 1);
        RefreshQuestProgress(true);

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
            info.liftCapacityKg = ship.claudiumMaxLiftKg;
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
            info.liftCapacityKg = activeShip.claudiumMaxLiftKg;
            info.claudiumMaxLiftKg = activeShip.claudiumMaxLiftKg;
            info.hullLimitKg = activeShip.hullMaxTakeoffMassKg;
        }
        else
        {
            info.emptyMassKg = stats.Get(ShipStatId.BaseMass, 0f);
            info.liftCapacityKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
            info.claudiumMaxLiftKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
            info.hullLimitKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, 2000f);
        }

        return CompleteCargoCapacity(info);
    }

    private CargoCapacityInfo CompleteCargoCapacity(CargoCapacityInfo info)
    {
        info.allowedTakeoffMassKg = Mathf.Min(info.liftCapacityKg, Mathf.Min(info.claudiumMaxLiftKg, info.hullLimitKg));
        info.maxCargoKg = Mathf.Max(0f, info.allowedTakeoffMassKg - info.emptyMassKg);
        info.fuelTankCapacityKg = ShipConsumableTankMath.CalculateFuelTankCapacityKg(info.maxCargoKg);
        info.claudiumTankCapacityKg = ShipConsumableTankMath.CalculateClaudiumTankCapacityKg(info.maxCargoKg);

        if (!info.assemblyValid)
        {
            return info;
        }

        ClampPlayerConsumableTanks(ref info);

        if (info.liftCapacityKg <= 0f)
        {
            info.reason = "клавдиевый контур не дает подъемной силы.";
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

        progress.shipFuelTank ??= new ShipConsumableTankState { resourceId = "charcoal" };
        progress.shipClaudiumTank ??= new ShipConsumableTankState { resourceId = "claudium" };
        progress.shipFuelTank.amountKg = Mathf.Min(progress.shipFuelTank.amountKg, info.fuelTankCapacityKg);
        progress.shipClaudiumTank.amountKg = Mathf.Min(progress.shipClaudiumTank.amountKg, info.claudiumTankCapacityKg);
        info.currentTankKg = progress.GetShipConsumableTankMassKg();
    }

    private void ApplyCargoMassToShip(ShipPhysics ship)
    {
        if (ship == null || progress == null) return;

        ApplyFuelConfigToShip(ship);
        SyncTankResourceFromRuntime(progress.shipFuelTank, ship.fuelResourceId, ref syncedFuelResourceId, ref ship.fuelStockKg);
        string claudiumResourceId = GetClaudiumResourceId(ship);
        SyncTankResourceFromRuntime(progress.shipClaudiumTank, claudiumResourceId, ref syncedClaudiumResourceId, ref ship.claudiumStock);
        ship.cargoMassKg = Mathf.Max(0f, progress.GetShipPayloadMassKg(sessionConfig));
        ship.RefreshRuntimeShipSettings();
    }

    private void RefreshShipConsumablesFromTanks(ShipPhysics ship, bool resetRuntimeStock)
    {
        if (ship == null || progress == null) return;

        string claudiumResourceId = GetClaudiumResourceId(ship);
        progress.shipFuelTank.SetResource(ship.fuelResourceId);
        progress.shipClaudiumTank.SetResource(claudiumResourceId);

        if (resetRuntimeStock || syncedFuelResourceId != ship.fuelResourceId)
        {
            syncedFuelResourceId = ship.fuelResourceId;
            ship.fuelStockKg = progress.shipFuelTank.GetAmount(ship.fuelResourceId);
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
        SyncTankResourceFromRuntime(progress.shipFuelTank, ship.fuelResourceId, ref syncedFuelResourceId, ref ship.fuelStockKg);
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

        fuelId = ship != null && !string.IsNullOrWhiteSpace(ship.fuelResourceId)
            ? ship.fuelResourceId
            : GetStartingFuelId();
        claudiumId = GetClaudiumResourceId(ship);
    }

    private void ApplyFuelConfigToShip(ShipPhysics ship)
    {
        if (ship == null || string.IsNullOrWhiteSpace(ship.fuelResourceId)) return;
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
            progress.shipFuelTank.Add(GetStartingFuelId(), startingFuelKg, Mathf.Max(startingFuelKg, capacity.fuelTankCapacityKg));
        }

        if (startingClaudiumKg > 0)
        {
            progress.shipClaudiumTank.Add("claudium", startingClaudiumKg, Mathf.Max(startingClaudiumKg, capacity.claudiumTankCapacityKg));
        }
    }

    private void AddStartingBaseProcessingSamples()
    {
        if (progress == null) return;

        PortStorageState storage = progress.GetPortStorageState(GetCapitalPortId(), true);
        if (storage == null) return;

        storage.AddResource("windshale_ore", 90);
        storage.AddResource("dawnspar_ore", 45);
        storage.AddResource("cloud_condensate", 90);
        storage.AddResource(SessionExtractionConstants.BrokenAutomatonItemId, 45);
        storage.AddResource("windcalf_carcass", 75);
        storage.AddResource(SessionExtractionConstants.RockInfoItemId, 45);
    }

    private void AddStartingBaseExpansionCurrency()
    {
        if (progress == null) return;

        PortStorageState storage = progress.GetPortStorageState(GetCapitalPortId(), true);
        if (storage == null) return;

        storage.AddResource("freight", 125480);
    }

    private string GetStartingFuelId()
    {
        string fuelId = shipLoader != null && shipLoader.targetShip != null ? shipLoader.targetShip.fuelResourceId : "";
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
