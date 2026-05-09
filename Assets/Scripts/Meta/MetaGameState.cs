using System;
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

public class MetaGameState : MonoBehaviour
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
    [InspectorName("Стартовые деньги")]
    public int startingMoney;
    [InspectorName("Прогресс игрока")]
    public PlayerProgress progress = new PlayerProgress();

    [Header("Сессия")]
    [InspectorName("Стартовый режим")]
    public GameSessionMode startingMode = GameSessionMode.Docked;
    [InspectorName("Стартовый док")]
    public string startingDockId = "starter_island";
    [InspectorName("Тип стартового дока")]
    public DockingLocationKind startingDockKind = DockingLocationKind.Island;
    [InspectorName("Автосохранение при стыковке")]
    [Tooltip("Сохранение разрешено только в режиме стыковки. Перед вылетом также создается чекпоинт последней стыковки.")]
    public bool autoSaveOnDock = true;
    [InspectorName("Загружать сохранение при старте")]
    [Tooltip("Если во время прошлого запуска игра была закрыта в полете, загрузка вернет игрока к последней стыковке.")]
    public bool loadSavedGameOnAwake = true;
    [InspectorName("Имя файла сохранения")]
    public string saveFileName = "wild_wind_save.json";

    [Header("Стартовые ресурсы")]
    [InspectorName("Стартовая руда")]
    public int startingOre = 4;
    [InspectorName("Стартовое железо")]
    public int startingIron = 0;

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

    [Header("Отладочный интерфейс стыковки")]
    [InspectorName("Показывать интерфейс")]
    public bool showDockingDebugUI = true;
    [InspectorName("Ширина интерфейса")]
    public int debugUiWidth = 380;

    [Header("Аварии")]
    [InspectorName("Автоматически добавить детектор крушений")]
    [Tooltip("Если включено, на корабль будет добавлен детектор крушений, который откатывает полет при аварии.")]
    public bool autoInstallCrashDetector = true;

    public GameSessionMode CurrentMode => progress != null ? progress.currentMode : startingMode;
    public bool IsDocked => CurrentMode == GameSessionMode.Docked;
    public string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private bool initialized;
    private bool isAdvancingProcesses;
    private string lastSaveMessage = "";
    private Vector2 debugScroll;

    private ShipCatalogSO ActiveCatalog => catalog != null ? catalog : shipLoader != null ? shipLoader.catalog : null;

    private void Reset()
    {
        shipLoader = FindFirstObjectByType<ShipLoader>();
        missionController = FindFirstObjectByType<MissionController>();
    }

    private void Awake()
    {
        if (shipLoader == null)
        {
            shipLoader = FindFirstObjectByType<ShipLoader>();
        }

        if (missionController == null)
        {
            missionController = FindFirstObjectByType<MissionController>();
        }

        if (loadSavedGameOnAwake)
        {
            LoadGame();
        }

        EnsureProgressInitialized();
        AdvanceRealTimeProcesses(DateTime.UtcNow);
        ApplySessionModeToShip();
        InstallCrashDetectorIfNeeded();
    }

    private void Start()
    {
        ApplySelectedShip();
        ApplySessionModeToShip();
    }

    private void Update()
    {
        if (!processRealTimeWhilePlaying) return;

        EnsureProgressInitialized();
        AdvanceRealTimeProcesses(DateTime.UtcNow);
    }

    private void OnApplicationQuit()
    {
        TrySaveGame();
    }

    public void EnsureProgressInitialized()
    {
        progress ??= new PlayerProgress();
        progress.Normalize();

        if (initialized) return;

        if (progress == null)
        {
            progress = new PlayerProgress();
        }

        progress.Normalize();

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

        ShipDefinitionSO starterShip = ActiveCatalog != null ? ActiveCatalog.GetStarterShip() : null;
        if (starterShip != null)
        {
            progress.EnsureStarterShip(starterShip.shipId);
        }

        if (progress.money < startingMoney)
        {
            progress.money = startingMoney;
        }

        if (!progress.receivedStartingInventory)
        {
            progress.AddResource("ore", startingOre);
            progress.AddResource("iron", startingIron);
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

    public ShipDefinitionSO ApplySelectedShip()
    {
        if (shipLoader == null) return null;

        EnsureProgressInitialized();
        if (shipLoader.catalog == null)
        {
            shipLoader.catalog = ActiveCatalog;
        }

        return shipLoader.ApplyShip(progress.selectedShipId);
    }

    public bool SelectShip(string shipId)
    {
        EnsureProgressInitialized();
        if (!progress.SelectShip(shipId)) return false;

        ApplySelectedShip();
        AutoSaveIfDocked();
        return true;
    }

    public bool TryBuyShip(string shipId)
    {
        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null) return false;

        EnsureProgressInitialized();

        ShipDefinitionSO ship = activeCatalog.GetShipById(shipId);
        if (ship == null) return false;
        if (progress.IsShipUnlocked(ship.shipId)) return false;
        if (progress.money < ship.purchasePrice) return false;

        progress.money -= ship.purchasePrice;
        progress.UnlockShip(ship.shipId);
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
        AddExperienceToShip(progress.selectedShipId, amount);
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

        if (node.kind == TechTreeNodeKind.Ship)
        {
            progress.UnlockShip(node.EffectiveShipId);
        }

        AutoSaveIfDocked();
        return true;
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

        string missionId = mission != null ? mission.missionId : "";
        progress.AcceptMission(missionId);

        if (autoSaveOnDock)
        {
            TrySaveGame();
        }

        progress.SetFlight(missionId);
        ApplySelectedShip();
        ApplySessionModeToShip();
        lastSaveMessage = "Вылет начат. Сохранение заблокировано до стыковки.";
        return true;
    }

    public bool BeginFreeFlight()
    {
        return TryBeginFlightSession(null);
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

    public bool StartIdleMining()
    {
        EnsureProgressInitialized();
        if (!IsDocked) return false;
        if (progress.HasActiveProcess("idle_mining")) return false;

        DateTime now = DateTime.UtcNow;
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

        DateTime now = DateTime.UtcNow;
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

        DateTime now = DateTime.UtcNow;
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
            experienceShipId = progress.selectedShipId
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
            AdvanceShopRefresh(utcNow);

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
        EnsureProgressInitialized();

        if (!IsDocked)
        {
            lastSaveMessage = "Нельзя сохранить: сначала нужна стыковка.";
            return false;
        }

        DateTime now = DateTime.UtcNow;
        AdvanceRealTimeProcesses(now);
        RememberCurrentDockPosition();
        progress.lastSavedUtcTicks = now.Ticks;
        progress.lastProcessUtcTicks = now.Ticks;
        progress.Normalize();

        MetaGameSaveData saveData = new MetaGameSaveData { progress = progress };

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SavePath, JsonUtility.ToJson(saveData, true));
            lastSaveMessage = "Сохранено: " + SavePath;
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

            if (progress.currentMode != GameSessionMode.Docked)
            {
                progress.SetDocked(startingDockId, startingDockKind);
            }

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

    public bool RollbackToLastDock(string reason = "")
    {
        bool loaded = LoadGame();
        if (!loaded)
        {
            progress = new PlayerProgress();
            initialized = false;
            EnsureProgressInitialized();
        }

        if (missionController != null)
        {
            missionController.CancelMission();
        }

        ApplySelectedShip();
        ApplySessionModeToShip();
        ResetCrashDetector();

        lastSaveMessage = string.IsNullOrWhiteSpace(reason)
            ? "Откат к последней стыковке выполнен."
            : "Откат к последней стыковке выполнен. " + reason;

        return loaded;
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
                string shipId = string.IsNullOrWhiteSpace(process.experienceShipId) ? progress.selectedShipId : process.experienceShipId;
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

                if (node.kind == TechTreeNodeKind.Ship)
                {
                    progress.UnlockShip(node.EffectiveShipId);
                }
            }
        }
    }

    private void ApplySessionModeToShip()
    {
        ShipPhysics ship = shipLoader != null ? shipLoader.targetShip : FindFirstObjectByType<ShipPhysics>();
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
        }

        if (body == null) return;

        if (docked)
        {
            if (progress.hasCurrentDockPosition)
            {
                ship.transform.position = progress.currentDockPosition;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.Sleep();
        }
        else
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.WakeUp();
        }
    }

    private void RememberCurrentDockPosition()
    {
        if (!IsDocked || progress == null) return;

        progress.currentDockPosition = GetCurrentShipPosition();
        progress.hasCurrentDockPosition = true;
    }

    private Vector3 GetCurrentShipPosition()
    {
        ShipPhysics ship = shipLoader != null ? shipLoader.targetShip : FindFirstObjectByType<ShipPhysics>();
        return ship != null ? ship.transform.position : Vector3.zero;
    }

    private void InstallCrashDetectorIfNeeded()
    {
        ShipPhysics ship = shipLoader != null ? shipLoader.targetShip : FindFirstObjectByType<ShipPhysics>();
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
        ShipPhysics ship = shipLoader != null ? shipLoader.targetShip : FindFirstObjectByType<ShipPhysics>();
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

        if (GUILayout.Button(new GUIContent("Сохранить у дока", "Сохраняет прогресс только если корабль находится в режиме стыковки.")))
        {
            TrySaveGame();
        }

        if (GUILayout.Button(new GUIContent("Удалить сохранение и начать заново", "Удаляет файл сохранения и сбрасывает текущий мета-прогресс.")))
        {
            DeleteSave();
        }

        if (missionController != null && missionController.mission != null && GUILayout.Button(new GUIContent("Вылететь на миссию", "Создает чекпоинт стыковки и переводит игру в режим вылета.")))
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
        DrawShipList();
        DrawTechTreeList();
    }

    private void DrawFlightDebugUi()
    {
        GUILayout.Label("Вылет");
        GUILayout.Label("Сохранение заблокировано до стыковки.");

        if (GUILayout.Button(new GUIContent("Состыковаться здесь", "Завершает вылет в текущей точке и сохраняет новый чекпоинт.")))
        {
            DockAt("field_dock", DockingLocationKind.Island);
        }

        if (GUILayout.Button(new GUIContent("Откатиться к последней стыковке", "Отменяет текущий вылет и возвращает сохраненный прогресс последнего дока.")))
        {
            RollbackToLastDock("Ручной откат.");
        }
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

    private void DrawShipList()
    {
        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null || activeCatalog.ships == null) return;

        GUILayout.Space(8f);
        GUILayout.Label("Корабли");

        for (int i = 0; i < activeCatalog.ships.Count; i++)
        {
            ShipDefinitionSO ship = activeCatalog.ships[i];
            if (ship == null) continue;

            bool unlocked = progress.IsShipUnlocked(ship.shipId);
            bool selected = progress.selectedShipId == ship.shipId;
            GUILayout.BeginHorizontal();
            GUILayout.Label(ship.displayName + " " + ship.purchasePrice + " мон.");

            GUI.enabled = unlocked && !selected;
            if (GUILayout.Button("Выбрать", GUILayout.Width(90f)))
            {
                SelectShip(ship.shipId);
            }

            GUI.enabled = !unlocked && progress.money >= ship.purchasePrice;
            if (GUILayout.Button("Купить", GUILayout.Width(70f)))
            {
                TryBuyShip(ship.shipId);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
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
            GUILayout.Label(node.displayName + " опыт " + node.researchCostXp + " / " + node.purchasePrice + " мон.");

            GUI.enabled = !researched;
            if (GUILayout.Button("Исслед.", GUILayout.Width(80f)))
            {
                TryResearchNode(node.nodeId);
            }

            GUI.enabled = researched && !purchased;
            if (GUILayout.Button("Купить", GUILayout.Width(70f)))
            {
                TryPurchaseNode(node.nodeId);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    private static string FormatRemaining(long targetUtcTicks)
    {
        if (targetUtcTicks <= 0) return "-";

        TimeSpan remaining = new DateTime(targetUtcTicks, DateTimeKind.Utc) - DateTime.UtcNow;
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
