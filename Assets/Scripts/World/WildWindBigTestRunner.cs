using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEngine.TestTools;
#endif
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public sealed class WildWindBigTestRunner : MonoBehaviour
{
    private const string LogPrefix = "[WildWindBigTest] ";
    private const string DefaultConfigFolder = "Data/Config";
    private const int BigTestContractVersion = 2;
    private const int MinimumExpectedCheckCount = 665;
    private const int MaxCapturedConsoleMessages = 32;
    private const string BigTestSessionSavePrefix = "wild_wind_big_test_session_";
#if UNITY_EDITOR
    private const string UsageAuditAfterBigTestArmedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Armed";
    private const string UsageAuditAfterBigTestRequestedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Requested";
#endif

    public const string BigTestEditorLaunchPlayerPrefsKey = "WildWind.BigTestEditorLaunch";
    public const string DefaultStartSceneName = "StartScreen";
    public const string DefaultWorldSceneName = "WildWindWorldScene";

    public static bool SuppressRunOnStartForAutomation { get; set; }
    public static bool IsSessionLoopLaunchInProgress => sessionLoopLaunchInProgress;
    public static bool IsMainWorldCheckInProgress => activeRunInProgress && !sessionLoopLaunchInProgress;

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
        "Pioneer ship catalog",
        "Starter hull visual asset contract",
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
        "Extraction mechanics and meta game",
        "Сессионные перезаходы стартовое меню <-> мир",
        "Защита побочных эффектов",
        "Методика сопровождения"
    };

    [Header("Большой тест")]
    [SerializeField, InspectorName("Запускать при старте Play Mode")] public bool runOnStart;
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

    [SerializeField, InspectorName("Gameplay Session")] public WildWindGameplaySession gameplaySession;
    [SerializeField, InspectorName("Gameplay HUD")] public WildWindGameplayHud gameplayHud;

    private bool hasRun;
    private bool becamePersistentForSceneLoop;
    private bool consoleMessageCaptureActive;
    private int capturedConsoleMessageCount;
    private readonly List<BigTestConsoleMessage> capturedConsoleMessages = new List<BigTestConsoleMessage>();
#if UNITY_EDITOR
    private bool usageCoverageCaptureActive;
    private bool usageCoverageWasEnabled;
#endif

    public WildWindBigTestResult LastResult { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaySessionState()
    {
        SuppressRunOnStartForAutomation = false;
        autoRunConsumedThisPlaySession = false;
        activeRunInProgress = false;
        sessionLoopLaunchInProgress = false;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapExplicitEditorLaunch()
    {
        TryStartPendingEditorBigTest();
    }

    public static bool TryStartPendingEditorBigTest()
    {
        if (!IsEditorBigTestLaunchPending() ||
            activeRunInProgress ||
            autoRunConsumedThisPlaySession ||
            SceneManager.GetActiveScene().name != DefaultWorldSceneName)
        {
            return false;
        }

        WildWindBigTestRunner runner = FindFirstObjectByType<WildWindBigTestRunner>();
        if (runner == null)
        {
            GameObject runnerObject = new GameObject("Wild Wind Big Test Runner");
            runner = runnerObject.AddComponent<WildWindBigTestRunner>();
        }

        runner.runOnStart = false;
        runner.logFullReportToConsole = true;
        runner.writeReportFile = true;
        runner.productionSimulationMinutes = 12f;
        runner.streamerAverageBudgetMs = 250f;
        runner.hasRun = false;

        autoRunConsumedThisPlaySession = true;
        runner.RunBigTest();
        return true;
    }
#endif

    public static bool IsBigTestTemporarySaveFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName) &&
            fileName.StartsWith(BigTestSessionSavePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsEditorBigTestLaunchPending()
    {
        return PlayerPrefs.GetInt(BigTestEditorLaunchPlayerPrefsKey, 0) == 1;
    }

    public static void MarkEditorBigTestLaunchPending()
    {
        PlayerPrefs.SetInt(BigTestEditorLaunchPlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    public static void ClearEditorBigTestLaunchPending()
    {
        PlayerPrefs.DeleteKey(BigTestEditorLaunchPlayerPrefsKey);
        PlayerPrefs.Save();
    }

    private IEnumerator Start()
    {
        if (!CanAutoRunOnStart(this))
        {
            yield break;
        }

        yield return null;
        if (!CanAutoRunOnStart(this))
        {
            yield break;
        }

        autoRunConsumedThisPlaySession = true;
        RunBigTest();
    }

    private static bool CanAutoRunOnStart(WildWindBigTestRunner runner)
    {
        return runner != null &&
            runner.runOnStart &&
            IsEditorBigTestLaunchPending() &&
            !SuppressRunOnStartForAutomation &&
            !autoRunConsumedThisPlaySession &&
            !activeRunInProgress;
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
#if UNITY_EDITOR
        BeginUsageCoverageCaptureIfArmed();
#endif

        BigTestSideEffectSnapshot sideEffects = BigTestSideEffectSnapshot.Capture();
        BigTestReport report = new BigTestReport(this);
        BeginConsoleMessageCapture();
        Stopwatch totalWatch = Stopwatch.StartNew();
        if (writeReportFile)
        {
            TryWriteRunStatus("started", null, report);
        }

        yield return EnsureWorldSceneForBigTest(report);

        RunChecked(report, () =>
        {
            ResolveReferences();
            DescribeTestScope(report);
            ValidateSceneContext(report);

            WorldConfigDatabase config = LoadConfig(report);
            ValidateConfigDatabase(config, report);
            ValidateLocalizationConfig(report);
            ValidateCargoStorageModel(config, report);
            ValidatePioneerShipCatalog(config, report);
            ValidateStarterHullVisualAssetContract(report);
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
            ValidateExtractionMechanicsAndMetaGame(report);
        });

        yield return RunCheckedCoroutine(report, ValidateSessionLoopRoundTrip(report));
        yield return RestoreAndValidateSideEffects(sideEffects, report);

        RunChecked(report, () => ValidateMaintainability(report));
        EndConsoleMessageCapture();
        ReportCapturedConsoleMessages(report, capturedConsoleMessages, capturedConsoleMessageCount);
        report.AssertIntegrity(RequiredSectionTitles, MinimumExpectedCheckCount, CanarySelfTestPasses);

        totalWatch.Stop();
        long elapsedMs = totalWatch.ElapsedMilliseconds;
        report.Finish(elapsedMs);
        LastResult = report.CreateResult(BigTestContractVersion, elapsedMs, true, RequiredSectionTitles, MinimumExpectedCheckCount);
        completed?.Invoke(LastResult);

        string text = report.BuildText();
        if (writeReportFile)
        {
            TryWriteReport(text, LastResult, report);
            TryWriteRunStatus("finished", LastResult, report);
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

#if UNITY_EDITOR
        WriteUsageCoverageSnapshotIfActive(report);
        RequestUsageAuditAfterBigTestIfArmed();
#endif

        ReleaseActiveRun();

        if (becamePersistentForSceneLoop)
        {
            Destroy(gameObject);
        }
    }

    private void ReleaseActiveRun()
    {
        EndConsoleMessageCapture();
        activeRunInProgress = false;
        sessionLoopLaunchInProgress = false;
        ClearEditorBigTestLaunchPending();
    }

#if UNITY_EDITOR
    private void BeginUsageCoverageCaptureIfArmed()
    {
        usageCoverageCaptureActive = false;
        if (!SessionState.GetBool(UsageAuditAfterBigTestArmedSessionKey, false))
        {
            return;
        }

        try
        {
            usageCoverageWasEnabled = Coverage.enabled;
            if (!Coverage.enabled)
            {
                Coverage.enabled = true;
            }

            Coverage.ResetAll();
            usageCoverageCaptureActive = true;
            Debug.Log(LogPrefix + "Usage coverage capture started for this big test.", this);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(LogPrefix + "Usage coverage capture could not be started: " + exception.Message, this);
        }
    }

    private void WriteUsageCoverageSnapshotIfActive(BigTestReport report)
    {
        if (!usageCoverageCaptureActive)
        {
            return;
        }

        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, "TestReports", "UsageAudit");
            Directory.CreateDirectory(folder);

            string lcovPath = Path.Combine(folder, "WildWindBigTestCoverage.lcov");
            string jsonPath = Path.Combine(folder, "WildWindBigTestCoverage.json");
            CoverageSnapshot snapshot = CoverageSnapshot.Capture(projectRoot);
            File.WriteAllText(lcovPath, snapshot.BuildLcov(), Encoding.UTF8);
            File.WriteAllText(jsonPath, JsonUtility.ToJson(snapshot, true), Encoding.UTF8);
            Debug.Log(LogPrefix + "Usage coverage snapshot saved: " + lcovPath, this);
        }
        catch (Exception exception)
        {
            report.Warn("Не удалось сохранить usage coverage snapshot: " + exception.Message);
        }
        finally
        {
            try
            {
                Coverage.enabled = usageCoverageWasEnabled;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LogPrefix + "Usage coverage enabled state could not be restored: " + exception.Message, this);
            }

            usageCoverageCaptureActive = false;
        }
    }

    private static void RequestUsageAuditAfterBigTestIfArmed()
    {
        if (!SessionState.GetBool(UsageAuditAfterBigTestArmedSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(UsageAuditAfterBigTestRequestedSessionKey, true);
    }

    [Serializable]
    private sealed class CoverageSnapshot
    {
        public string generatedAtUtc;
        public string projectRoot;
        public int coveredMethodCount;
        public int coveredFileCount;
        public List<CoverageFileSnapshot> files = new List<CoverageFileSnapshot>();

        public static CoverageSnapshot Capture(string projectRoot)
        {
            CoverageSnapshot snapshot = new CoverageSnapshot
            {
                generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                projectRoot = projectRoot ?? ""
            };

            Dictionary<string, CoverageFileSnapshot> byPath = new Dictionary<string, CoverageFileSnapshot>(StringComparer.OrdinalIgnoreCase);
            CoveredMethodStats[] stats = Coverage.GetStatsForAllCoveredMethods();
            snapshot.coveredMethodCount = stats != null ? stats.Length : 0;

            if (stats != null)
            {
                for (int i = 0; i < stats.Length; i++)
                {
                    System.Reflection.MethodBase method = stats[i].method;
                    if (method == null)
                    {
                        continue;
                    }

                    CoveredSequencePoint[] points = Coverage.GetSequencePointsFor(method);
                    if (points == null)
                    {
                        continue;
                    }

                    for (int p = 0; p < points.Length; p++)
                    {
                        CoveredSequencePoint point = points[p];
                        string path = NormalizeCoverageSnapshotPath(point.filename, projectRoot);
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            continue;
                        }

                        if (!byPath.TryGetValue(path, out CoverageFileSnapshot file))
                        {
                            file = new CoverageFileSnapshot { path = path };
                            byPath[path] = file;
                        }

                        file.totalSequencePoints++;
                        if (point.hitCount > 0)
                        {
                            file.coveredSequencePoints++;
                            file.AddLine(SafeUIntToInt(point.line), SafeUIntToInt(point.hitCount));
                        }
                    }
                }
            }

            snapshot.files = new List<CoverageFileSnapshot>(byPath.Values);
            snapshot.files.Sort((left, right) => string.Compare(left.path, right.path, StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < snapshot.files.Count; i++)
            {
                if (snapshot.files[i].coveredSequencePoints > 0)
                {
                    snapshot.coveredFileCount++;
                }
            }

            return snapshot;
        }

        public string BuildLcov()
        {
            StringBuilder builder = new StringBuilder(16384);
            for (int i = 0; i < files.Count; i++)
            {
                CoverageFileSnapshot file = files[i];
                if (file == null || file.lines.Count == 0)
                {
                    continue;
                }

                file.lines.Sort((left, right) => left.line.CompareTo(right.line));
                builder.AppendLine("SF:" + file.path);
                for (int lineIndex = 0; lineIndex < file.lines.Count; lineIndex++)
                {
                    CoverageLineSnapshot line = file.lines[lineIndex];
                    builder.AppendLine("DA:" + line.line + "," + line.hitCount);
                }

                builder.AppendLine("end_of_record");
            }

            return builder.ToString();
        }

        private static string NormalizeCoverageSnapshotPath(string path, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            string normalized = path.Replace('\\', '/');
            string root = (projectRoot ?? "").Replace('\\', '/').TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(root) &&
                normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(root.Length + 1);
            }

            int assetsIndex = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                normalized = normalized.Substring(assetsIndex + 1);
            }

            return normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : "";
        }

        private static int SafeUIntToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }

    [Serializable]
    private sealed class CoverageFileSnapshot
    {
        public string path;
        public int totalSequencePoints;
        public int coveredSequencePoints;
        public List<CoverageLineSnapshot> lines = new List<CoverageLineSnapshot>();

        public void AddLine(int lineNumber, int hitCount)
        {
            if (lineNumber <= 0)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].line == lineNumber)
                {
                    lines[i].hitCount += Mathf.Max(1, hitCount);
                    return;
                }
            }

            lines.Add(new CoverageLineSnapshot
            {
                line = lineNumber,
                hitCount = Mathf.Max(1, hitCount)
            });
        }
    }

    [Serializable]
    private sealed class CoverageLineSnapshot
    {
        public int line;
        public int hitCount;
    }
#endif

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

    private IEnumerator EnsureWorldSceneForBigTest(BigTestReport report)
    {
        ResolveReferences();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name == DefaultWorldSceneName &&
            world != null &&
            world.Profile != null &&
            world.Manifest != null &&
            world.Manifest.IsUsable)
        {
            yield break;
        }

        report.Info("Big test is switching to " + DefaultWorldSceneName + " before world checks.");
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
        becamePersistentForSceneLoop = true;
        SceneManager.LoadScene(DefaultWorldSceneName);
        yield return WaitForActiveScene(DefaultWorldSceneName);
        DisableDuplicateBigTestRunners();
        ResolveReferences();
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

    private void BeginConsoleMessageCapture()
    {
        EndConsoleMessageCapture();
        capturedConsoleMessages.Clear();
        capturedConsoleMessageCount = 0;
        consoleMessageCaptureActive = true;
        Application.logMessageReceived += HandleBigTestConsoleMessage;
    }

    private void EndConsoleMessageCapture()
    {
        if (!consoleMessageCaptureActive)
        {
            return;
        }

        Application.logMessageReceived -= HandleBigTestConsoleMessage;
        consoleMessageCaptureActive = false;
    }

    private void HandleBigTestConsoleMessage(string condition, string stackTrace, LogType type)
    {
        if (!consoleMessageCaptureActive || !IsFatalConsoleLog(type))
        {
            return;
        }

        if (!string.IsNullOrEmpty(condition) && condition.StartsWith(LogPrefix, StringComparison.Ordinal))
        {
            return;
        }

        capturedConsoleMessageCount++;
        if (capturedConsoleMessages.Count >= MaxCapturedConsoleMessages)
        {
            return;
        }

        capturedConsoleMessages.Add(new BigTestConsoleMessage
        {
            type = type,
            condition = condition ?? "",
            stackTrace = stackTrace ?? ""
        });
    }

    private static void ReportCapturedConsoleMessages(
        BigTestReport report,
        IReadOnlyList<BigTestConsoleMessage> messages,
        int totalMessageCount)
    {
        report.Section("Unity Console");
        if (totalMessageCount <= 0)
        {
            report.Pass("Unity Console emitted no Error, Assert or Exception messages during the big test.");
            return;
        }

        int visibleCount = messages != null ? Mathf.Min(messages.Count, MaxCapturedConsoleMessages) : 0;
        for (int i = 0; i < visibleCount; i++)
        {
            BigTestConsoleMessage message = messages[i];
            string stackTop = GetFirstStackLine(message.stackTrace);
            report.Fail("Unity Console " + message.type + ": " + GetFirstConsoleLine(message.condition)
                + (string.IsNullOrWhiteSpace(stackTop) ? "" : " | " + stackTop));
        }

        if (totalMessageCount > visibleCount)
        {
            report.Warn("Unity Console emitted " + (totalMessageCount - visibleCount) + " more fatal message(s) that were hidden after the first " + visibleCount + ".");
        }
    }

    private static bool IsFatalConsoleLog(LogType type)
    {
        return type == LogType.Error || type == LogType.Assert || type == LogType.Exception;
    }

    private static string GetFirstConsoleLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "<empty>";
        }

        string line = text.Replace("\r\n", "\n").Replace('\r', '\n');
        int newline = line.IndexOf('\n');
        if (newline >= 0)
        {
            line = line.Substring(0, newline);
        }

        return line.Length <= 220 ? line : line.Substring(0, 220) + "...";
    }

    private static string GetFirstStackLine(string stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return "";
        }

        string line = stackTrace.Replace("\r\n", "\n").Replace('\r', '\n');
        int newline = line.IndexOf('\n');
        if (newline >= 0)
        {
            line = line.Substring(0, newline);
        }

        return line.Length <= 180 ? line : line.Substring(0, 180) + "...";
    }

    public static WildWindBigTestResult RunCanarySelfTest()
    {
        BigTestReport report = new BigTestReport(null, false);
        report.Section("Canary");
        report.Fail("Ожидаемый canary FAIL: механизм ошибок должен делать результат красным.");
        report.Finish(0L);
        return report.CreateResult(BigTestContractVersion, 0L, true, new[] { "Canary" }, 1);
    }

    public static WildWindBigTestResult RunConsoleCanarySelfTest()
    {
        BigTestReport report = new BigTestReport(null, false);
        ReportCapturedConsoleMessages(
            report,
            new[]
            {
                new BigTestConsoleMessage
                {
                    type = LogType.Exception,
                    condition = "Expected console canary exception.",
                    stackTrace = "WildWindBigTestRunner.RunConsoleCanarySelfTest"
                }
            },
            1);
        report.Finish(0L);
        return report.CreateResult(BigTestContractVersion, 0L, true, new[] { "Unity Console" }, 1);
    }

    private static bool CanarySelfTestPasses()
    {
        WildWindBigTestResult result = RunCanarySelfTest();
        WildWindBigTestResult consoleResult = RunConsoleCanarySelfTest();
        return result != null &&
            result.Completed &&
            !result.Succeeded &&
            result.FailureCount == 1 &&
            result.CheckCount == 1 &&
            consoleResult != null &&
            consoleResult.Completed &&
            !consoleResult.Succeeded &&
            consoleResult.FailureCount == 1 &&
            consoleResult.CheckCount == 1;
    }

    public void ResetRunStateForEditor()
    {
        EndConsoleMessageCapture();
        capturedConsoleMessages.Clear();
        capturedConsoleMessageCount = 0;
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
        if (gameplaySession == null) gameplaySession = FindFirstObjectByType<WildWindGameplaySession>();
        if (gameplayHud == null) gameplayHud = FindFirstObjectByType<WildWindGameplayHud>();
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
        requiredKeys.AddRange(WildWindGameplayHud.RequiredLocalizationKeys);
        bool valid = WildWindLocalization.ValidateDefaultConfig(requiredKeys, out List<string> errors);
        if (valid)
        {
            report.Pass("Localization config is loaded and all start/gameplay HUD/menu keys have ru/en text.");
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
        report.Check(config.specialModules.Count == 5, "Special_module.csv contains only the five Pioneer starter fitting modules: " + config.specialModules.Count + ".");
        report.Check(config.hulls.Count == 1
            && config.engines.Count == 1
            && config.propellers.Count == 1
            && config.claudiumLoops.Count == 1,
            "Ship part CSVs contain only the Pioneer hull, engine, propeller and claudium loop.");
        report.Check(config.shipTreeEntries.Count == 1, "Ship_tree.csv contains only Pioneer: " + config.shipTreeEntries.Count + ".");
        report.Check(config.islandIndustries.Count >= 6, "Production_industry.csv содержит производственные линии: " + config.islandIndustries.Count + ".");
        report.Check(config.industryRecipes.Count >= 6, "Production_recipe.csv содержит производственные рецепты: " + config.industryRecipes.Count + ".");
        report.Check(config.islandArchetypes.Count == 5, "Island_archetype.csv содержит 5 типов островов: " + config.islandArchetypes.Count + ".");
        report.Check(config.islandArchetypeStages.Count >= 10, "Island_archetype_stage.csv содержит стадии развития островов: " + config.islandArchetypeStages.Count + ".");
        report.Check(config.islandSocialNeeds.Count == 7, "Island_social_need.csv содержит 7 общественных потребностей: " + config.islandSocialNeeds.Count + ".");
        report.Check(config.islandBuildings.Count >= 30, "Island_building.csv содержит производственные и сервисные здания: " + config.islandBuildings.Count + ".");
        report.Check(config.flagshipExpeditions.Count >= 1, "Expedition.csv содержит экспедиции флагманов: " + config.flagshipExpeditions.Count + ".");

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
        CheckUniqueIds(config.flagshipExpeditions, expedition => expedition.expeditionId, "экспедиций флагманов", report);

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

        bool entriesValid = config.shipTreeEntries.Count == 1;
        bool hasPioneerRoot = false;

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

        report.Check(entriesValid && hasPioneerRoot,
            "Ship_tree.csv defines the current catalog as Pioneer only.");

        report.Check(!ShipTreeHasCycles(config),
            "Дерево кораблей не содержит циклов по parent_ship_id.");

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

        bool flagshipExpeditionsValid = true;
        for (int i = 0; i < config.flagshipExpeditions.Count; i++)
        {
            FlagshipExpeditionDefinition expedition = config.flagshipExpeditions[i];
            flagshipExpeditionsValid &= expedition != null &&
                !string.IsNullOrWhiteSpace(expedition.expeditionId) &&
                !string.IsNullOrWhiteSpace(expedition.displayNameRu) &&
                !string.IsNullOrWhiteSpace(expedition.regionId) &&
                !string.IsNullOrWhiteSpace(expedition.sceneName) &&
                expedition.minimumFlagshipRank >= FlagshipInteriorSimulator.MinimumFlagshipRank &&
                expedition.moraleDrainMultiplier > 0f &&
                expedition.returnDockKind == DockingLocationKind.Island &&
                config.GetIsland(expedition.returnDockId) != null;
        }

        report.Check(flagshipExpeditionsValid, "Expedition.csv задает флагманские регионы с R3+ входом, сценой, множителем морали и возвратом в существующий док.");

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
                module.cargoVanCapacityKg >= 0f &&
                module.passengerSeatCapacity >= 0f &&
                module.bulkHoldCapacityKg >= 0f &&
                module.liquidTankCapacityKg >= 0f &&
                module.gasCylinderCapacityKg >= 0f &&
                module.miningImpactDamageTakenMultiplier >= 0f &&
                module.surveyPaperToInfoEfficiency >= 0f &&
                module.leviathanAlarmGenerationMultiplier >= 0f &&
                module.harpoonWeaponCostPerMinute >= 0f &&
                module.harpoonMaxCarcassMassKg >= 0f &&
                module.harpoonFlightDamage >= 0f &&
                module.harpoonRangeMeters >= 0f &&
                module.shipDockSlots >= 0f &&
                module.dockedShipMassFactor > 0f &&
                module.dockSupportClaudiumPerTonHour >= 0f &&
                techReferenceOk;

            if (module != null)
            {
                hasCargoStorageModule |= module.cargoVanCapacityKg > 0f ||
                    module.passengerSeatCapacity > 0f ||
                    module.bulkHoldCapacityKg > 0f ||
                    module.liquidTankCapacityKg > 0f ||
                    module.gasCylinderCapacityKg > 0f ||
                    module.shipDockSlots > 0f;
            }
        }

        report.Check(techValid && techTreeValid, "Current technology config keeps the minimal Pioneer unlock tree valid and acyclic.");
        report.Check(techValid && hasCargoStorageModule, "Starter modules can define shared cargo capacity without legacy social-service or dock-slot modules.");
    }

    private static bool TechnologyTreeMetadataValid(WorldConfigDatabase config)
    {
        if (config == null || config.technologies == null || config.technologies.Count == 0) return false;

        return config.GetTechnology("basic_airship") != null &&
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
        ItemConfig carcass = config.GetItem("windcalf_carcass");
        bool cargoMetadataValid = passengerTemplate != null &&
            passengerTemplate.cargoUnitKind == CargoUnitKind.Passenger &&
            passengerTemplate.cargoStorageKind == CargoStorageKind.Cabin &&
            Mathf.Abs(config.GetItemTransportMassKg(PassengerCargoIds.ToCapitalItemId, 3) - 300f) <= 0.001f &&
            water != null &&
            water.cargoUnitKind == CargoUnitKind.Piece &&
            water.cargoStorageKind == CargoStorageKind.Van &&
            sampleOre != null &&
            sampleOre.cargoUnitKind == CargoUnitKind.Piece &&
            sampleOre.cargoStorageKind == CargoStorageKind.Van &&
            carcass != null &&
            carcass.cargoUnitKind == CargoUnitKind.Piece &&
            carcass.cargoStorageKind == CargoStorageKind.Van;
        report.Check(cargoMetadataValid, "Cargo item metadata keeps passengers in cabins and resources in the shared van hold.");

        LogisticsShipMetrics cargoMetrics = new LogisticsShipMetrics
        {
            cargoCompartments = new List<CargoCompartmentDefinition>
            {
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.Cabin, capacity = 3f },
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.Van, capacity = 200f }
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
            ["windcalf_carcass"] = 80
        };
        bool cargoFits = CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, typedCargo, out _);
        float typedCargoMass = CargoStoragePlanner.GetCargoMassKg(config, typedCargo);
        Dictionary<string, int> passengerOverflow = new Dictionary<string, int> { [PassengerCargoIds.ToCapitalItemId] = 4 };
        Dictionary<string, int> mixedBulkOverflow = new Dictionary<string, int>
        {
            ["windshale_ore"] = 10,
            ["dawnspar_ore"] = 10,
            ["bluebrass_ore"] = 10
        };
        Dictionary<string, int> genericCargoOverflow = new Dictionary<string, int> { ["water"] = 201 };
        bool cargoLimitsWork = cargoFits &&
            Mathf.Abs(typedCargoMass - 425f) <= 0.001f &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, passengerOverflow, out _) &&
            CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, mixedBulkOverflow, out _) &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics, genericCargoOverflow, out _);
        report.Check(cargoLimitsWork, "Cargo planning checks cabin seats and the shared van weight limit without old docked-ship cargo.");

        PlayerProgress tankProgress = new PlayerProgress();
        tankProgress.Normalize();
        tankProgress.AddShipCargo("charcoal", 10);
        tankProgress.shipEngineFuelTank.Add("charcoal", 30f, 50f);
        tankProgress.shipClaudiumTank.Add("claudium", 12.5f, 25f);
        bool internalTanksAreSeparate =
            tankProgress.GetShipCargoAmount("charcoal") == 10 &&
            Approximately(tankProgress.GetShipCargoMassKg(config), 10f, 0.001f) &&
            Approximately(tankProgress.GetShipPayloadMassKg(config), 52.5f, 0.001f) &&
            tankProgress.shipEngineFuelTank.TrySpend("charcoal", 5.5f) &&
            tankProgress.GetShipCargoAmount("charcoal") == 10 &&
            Approximately(tankProgress.shipEngineFuelTank.GetAmount("charcoal"), 24.5f, 0.001f);
        report.Check(internalTanksAreSeparate, "Топливо и клавдий в баках считаются массой корабля, но не становятся грузом и не тратят cargo-стек.");

        PlayerProgress playerCargoProgress = new PlayerProgress();
        playerCargoProgress.Normalize();
        playerCargoProgress.AddShipCargo(PassengerCargoIds.ToCapitalItemId, 2);
        playerCargoProgress.AddShipCargo("water", 10);
        bool playerCargoUsesConfigMass = Mathf.Abs(playerCargoProgress.GetShipCargoMassKg(config) - 210f) <= 0.001f &&
            CargoStoragePlanner.TryValidateCargoStorage(config, cargoMetrics.cargoCompartments, playerCargoProgress.shipCargo, out _);
        report.Check(playerCargoUsesConfigMass, "Груз основного корабля игрока тоже считает массу через Item.csv и проходит ту же проверку отсеков.");
    }

    private static void ValidatePioneerShipCatalog(WorldConfigDatabase config, BigTestReport report)
    {
        report.Section("Pioneer ship catalog");

        if (config == null || !config.isLoaded)
        {
            report.Fail("Pioneer ship catalog checks stopped: config database is not loaded.");
            return;
        }

        bool onlyPioneerShipParts = config.shipTreeEntries.Count == 1
            && config.GetShipTreeEntry("pioneer") != null
            && config.hulls.Count == 1
            && config.GetHull(GameplaySessionSaveData.DefaultStarterHullId) != null
            && config.engines.Count == 1
            && config.GetEngine("starter_engine") != null
            && config.propellers.Count == 1
            && config.GetPropeller("starter_propeller") != null
            && config.claudiumLoops.Count == 1
            && config.GetClaudiumLoop("starter_claudium_loop") != null
            && config.specialModules.Count == 5
            && config.GetSpecialModule(SessionExtractionConstants.StarterCargoRackModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterGasHarvesterModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterMiningHoldModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterObservationPostModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterHarpoonModuleId) != null;
        report.Check(onlyPioneerShipParts,
            "Config ship catalog contains only Pioneer and the five starter fitting modules.");

        report.Check(R1ShipDesignCatalog.All.Count == 0 && R2TenderDesignCatalog.All.Count == 0,
            "Legacy R1/R2 design catalogs are empty while the session extraction prototype keeps only Pioneer.");

        ShipCatalogSO catalog = ScriptableObject.CreateInstance<ShipCatalogSO>();
        catalog.starterHullId = GameplaySessionSaveData.DefaultStarterHullId;
        catalog.parts = new List<ShipPartDefinitionSO>();

        try
        {
            int expectedParts = config.hulls.Count
                + config.engines.Count
                + config.propellers.Count
                + config.claudiumLoops.Count
                + config.specialModules.Count;
            int appliedParts = ShipAssemblyBuilder.ApplyCsvShipPartConfigs(catalog, config);
            report.Check(appliedParts == expectedParts && expectedParts == 9,
                "Pioneer CSV ship part configs sync into ShipCatalog: " + appliedParts + "/" + expectedParts + " parts.");

            PlayerProgress progress = new PlayerProgress();
            progress.sessionExtractionCoreMode = true;
            progress.Normalize();
            progress.ReplaceShipAssembly(GameplaySessionSaveData.DefaultStarterHullId);
            bool requiredModulesReady = ShipAssemblyBuilder.AutoInstallRequiredModules(catalog, null, progress, out string autoInstallMessage);
            ShipAssemblyResult result = null;
            bool assembled = requiredModulesReady
                && ShipAssemblyBuilder.TryBuild(catalog, null, progress, out result);
            report.Check(assembled
                && result != null
                && result.hull != null
                && result.hull.partId == GameplaySessionSaveData.DefaultStarterHullId
                && HasSlotType(result, SessionExtractionConstants.HighSlotTypeId)
                && HasSlotType(result, SessionExtractionConstants.MidSlotTypeId)
                && HasSlotType(result, SessionExtractionConstants.LowSlotTypeId)
                && HasSlotType(result, SessionExtractionConstants.RigSlotTypeId)
                && !HasSlotType(result, SessionExtractionConstants.LegacyUtilitySlotTypeId),
                assembled
                    ? "Pioneer assembles with High/Mid/Low/Rig fitting bands and no legacy utility slot."
                    : "Pioneer does not assemble from CSV catalog: "
                        + (requiredModulesReady ? (result != null ? result.message : "no result") : autoInstallMessage));

            string envelope = "assembly did not build.";
            bool flightEnvelopeOk = assembled
                && HasStablePioneerFlightEnvelope(result, 250f, 40f, 0.5f, 12f, out envelope);
            report.Check(flightEnvelopeOk,
                "Bare Pioneer has enough lift, hover reserve, thrust and speed for the starter sortie: " + envelope);
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

    private static void ValidateStarterHullVisualAssetContract(BigTestReport report)
    {
        report.Section("Starter hull visual asset contract");
#if UNITY_EDITOR
        const string sourceObjPath = "Assets/Data/ShipPrefabs/StarterHullBlender.obj";
        const string bodyMeshPath = "Assets/Data/ShipPrefabs/StarterHullBodyMesh.asset";
        const string turretBaseMeshPath = "Assets/Data/ShipPrefabs/StarterHullTurretBaseMesh.asset";
        const string turretMeshPath = "Assets/Data/ShipPrefabs/StarterHullTurretMesh.asset";
        const string barrelMeshPath = "Assets/Data/ShipPrefabs/StarterHullBarrelMesh.asset";
        const string starterHullPrefabPath = "Assets/Data/ShipPrefabs/StarterHull.prefab";
        const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
        const string worldScenePath = "Assets/Scenes/WildWindWorldScene.unity";
        const string oldObjGuid = "9a78659ed94342b4aaf97188d6cd263a";
        const string oldMergedMeshGuid = "e4d4202973e7486895171db449690183";
        const string bodyMeshGuid = "b8e4abdf44b04f54873370a03e6f2df1";
        const string turretBaseMeshGuid = "f24e857d69e0463084d8aaf64b844cd2";
        const string turretMeshGuid = "0aa0dfb898e24b80abf46bb40541d8f6";
        const string barrelMeshGuid = "57917e1b1a1e4bd8862d22ff4b151f31";
        string[] splitMeshReferences =
        {
            "m_Mesh: {fileID: 4300000, guid: " + bodyMeshGuid + ", type: 2}",
            "m_Mesh: {fileID: 4300000, guid: " + turretBaseMeshGuid + ", type: 2}",
            "m_Mesh: {fileID: 4300000, guid: " + turretMeshGuid + ", type: 2}",
            "m_Mesh: {fileID: 4300000, guid: " + barrelMeshGuid + ", type: 2}"
        };
        string[] forbiddenRuntimeVisualGuids = { oldObjGuid, oldMergedMeshGuid };

        report.Check(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), sourceObjPath)), "Starter hull Blender OBJ source exists.");
        string sourceObjText = ReadProjectText(sourceObjPath);
        bool sourceWeaponPartsOk = StarterHullSourceObjIncludesWeaponParts(sourceObjText, out string sourceWeaponPartsMessage);
        report.Check(sourceWeaponPartsOk,
            sourceWeaponPartsOk
                ? "Starter hull OBJ includes turret base, turret and barrel: " + sourceWeaponPartsMessage
                : "Starter hull OBJ is missing imported weapon parts: " + sourceWeaponPartsMessage);

        const ImportAssetOptions meshImportOptions = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
        AssetDatabase.Refresh(meshImportOptions);
        AssetDatabase.ImportAsset(bodyMeshPath, meshImportOptions);
        AssetDatabase.ImportAsset(turretBaseMeshPath, meshImportOptions);
        AssetDatabase.ImportAsset(turretMeshPath, meshImportOptions);
        AssetDatabase.ImportAsset(barrelMeshPath, meshImportOptions);
        EditorUtility.UnloadUnusedAssetsImmediate(true);

        Mesh bodyMesh = AssetDatabase.LoadAssetAtPath<Mesh>(bodyMeshPath);
        Mesh turretBaseMesh = AssetDatabase.LoadAssetAtPath<Mesh>(turretBaseMeshPath);
        Mesh turretMesh = AssetDatabase.LoadAssetAtPath<Mesh>(turretMeshPath);
        Mesh barrelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(barrelMeshPath);
        report.Check(bodyMesh != null && bodyMesh.vertexCount > 0,
            bodyMesh != null
                ? "Starter hull body mesh is loadable with " + bodyMesh.vertexCount + " vertices."
                : "Starter hull body mesh is missing.");
        if (bodyMesh != null)
        {
            Vector3 bodySize = bodyMesh.bounds.size;
            bool bodySizeOk = IsFinite(bodySize) &&
                bodySize.x >= 5f && bodySize.x <= 7f &&
                bodySize.y >= 2.5f && bodySize.y <= 4.25f &&
                bodySize.z >= 18f && bodySize.z <= 22f;
            report.Check(bodySizeOk,
                bodySizeOk
                    ? "Starter hull body mesh keeps the 20m boat envelope: " + FormatVector(bodySize) + "."
                    : "Starter hull body mesh has the wrong envelope: " + FormatVector(bodySize) + ".");

            bool noseForwardOk = StarterHullNoseFacesPositiveZ(bodyMesh.vertices, out string noseForwardMessage);
            report.Check(noseForwardOk,
                noseForwardOk
                    ? "Starter hull body mesh nose faces +Z: " + noseForwardMessage
                    : "Starter hull body mesh appears to face backward: " + noseForwardMessage);
        }

        int splitWeaponVertexCount = (turretBaseMesh != null ? turretBaseMesh.vertexCount : 0) +
            (turretMesh != null ? turretMesh.vertexCount : 0) +
            (barrelMesh != null ? barrelMesh.vertexCount : 0);
        int splitWeaponTriangleCount =
            CountMeshTriangles(turretBaseMesh) +
            CountMeshTriangles(turretMesh) +
            CountMeshTriangles(barrelMesh);
        bool splitWeaponMeshesOk = turretBaseMesh != null && turretBaseMesh.vertexCount >= 3000 &&
            turretMesh != null && turretMesh.vertexCount > 0 &&
            barrelMesh != null && barrelMesh.vertexCount > 0 &&
            splitWeaponTriangleCount >= 1500;
        report.Check(splitWeaponMeshesOk,
            splitWeaponMeshesOk
                ? "Starter hull weapon is split into loadable base/turret/barrel meshes: " +
                    splitWeaponVertexCount + " vertices, " + splitWeaponTriangleCount + " triangles."
                : "Starter hull split weapon meshes are missing or too small: " +
                    splitWeaponVertexCount + " vertices, " + splitWeaponTriangleCount + " triangles.");
        if (barrelMesh != null)
        {
            Vector3 barrelSize = barrelMesh.bounds.size;
            Vector3 barrelDiskSize;
            bool barrelDiskSizeLoaded = TryReadNativeMeshLocalAabbSize(barrelMeshPath, out barrelDiskSize);
            bool barrelRuntimeAxisOk = StarterHullBarrelAxisIsBakedAlongLocalZ(barrelSize);
            bool barrelDiskAxisOk = barrelDiskSizeLoaded && StarterHullBarrelAxisIsBakedAlongLocalZ(barrelDiskSize);
            bool barrelAxisOk = barrelRuntimeAxisOk || barrelDiskAxisOk;
            report.Check(barrelAxisOk,
                barrelRuntimeAxisOk
                    ? "Starter hull barrel mesh is baked along local +Z for Muzzle.forward firing: " + FormatVector(barrelSize) + "."
                    : barrelDiskAxisOk
                        ? "Starter hull barrel mesh disk asset is baked along local +Z; Unity runtime mesh cache still reported " +
                            FormatVector(barrelSize) + " before reimport completes, disk is " + FormatVector(barrelDiskSize) + "."
                        : "Starter hull barrel mesh is not baked along local +Z: runtime " + FormatVector(barrelSize) +
                            ", disk " + (barrelDiskSizeLoaded ? FormatVector(barrelDiskSize) : "unreadable") + ".");
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(starterHullPrefabPath);
        report.Check(prefab != null, "StarterHull prefab is loadable.");
        if (prefab != null)
        {
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            bool prefabHasEnabledRenderer = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    prefabHasEnabledRenderer = true;
                    break;
                }
            }

            bool prefabUsesSplitMeshes = MeshFiltersContainMesh(filters, bodyMesh) &&
                MeshFiltersContainMesh(filters, turretBaseMesh) &&
                MeshFiltersContainMesh(filters, turretMesh) &&
                MeshFiltersContainMesh(filters, barrelMesh);
            bool prefabHasGunHierarchy = ContainsChildNamed(prefab.transform, "\u041e\u0441\u043d\u043e\u0432\u0430\u043d\u0438\u0435_\u0442\u0443\u0440\u0435\u043b\u0438") &&
                ContainsChildNamed(prefab.transform, "\u0422\u0443\u0440\u0435\u043b\u044c") &&
                ContainsChildNamed(prefab.transform, "\u0421\u0442\u0432\u043e\u043b") &&
                ContainsChildNamed(prefab.transform, "Muzzle");
            report.Check(prefabUsesSplitMeshes, "StarterHull prefab references the split body, turret base, turret and barrel mesh assets.");
            report.Check(prefabHasGunHierarchy, "StarterHull prefab exposes turret hierarchy for gun binding: base, turret, barrel and Muzzle.");
            report.Check(prefabHasEnabledRenderer, "StarterHull prefab has an enabled renderer.");
            report.Check(!ContainsChildNamed(prefab.transform, "Session Balloon") &&
                !ContainsChildNamed(prefab.transform, "Session Cabin") &&
                !ContainsChildNamed(prefab.transform, "Player Ship Proxy"),
                "StarterHull prefab does not contain legacy fallback ship visuals.");
        }

        ValidateStarterHullSceneYaml(report, starterHullPrefabPath, splitMeshReferences, forbiddenRuntimeVisualGuids, "StarterHull prefab YAML");
        ValidateStarterHullSceneYaml(report, sampleScenePath, splitMeshReferences, forbiddenRuntimeVisualGuids, "SampleScene YAML");
        ValidateStarterHullSceneYaml(report, worldScenePath, splitMeshReferences, forbiddenRuntimeVisualGuids, "WorldScene YAML");
        bool starterGunfireDoesNotRequireCargo =
            StarterHullYamlHasFreeGunCost(starterHullPrefabPath) &&
            StarterHullYamlHasFreeGunCost(sampleScenePath) &&
            StarterHullYamlHasFreeGunCost(worldScenePath);
        report.Check(starterGunfireDoesNotRequireCargo,
            "Starter main gun fire is not blocked by empty weapon cargo in the prefab or starter scenes.");
        bool starterBarrelBindingIsCurrent =
            StarterHullYamlHasCurrentBarrelBinding(starterHullPrefabPath) &&
            StarterHullYamlHasCurrentBarrelBinding(sampleScenePath) &&
            StarterHullYamlHasCurrentBarrelBinding(worldScenePath);
        report.Check(starterBarrelBindingIsCurrent,
            "Starter barrel and Muzzle transforms match the current horizontal Blender barrel import.");
        ValidateStarterHullRuntimeTransformContract(report, starterHullPrefabPath, worldScenePath);

        string worldSceneText = ReadProjectText(worldScenePath);
        if (!string.IsNullOrEmpty(worldSceneText))
        {
            report.Check(!worldSceneText.Contains("m_Name: Player Ship Proxy"), "WorldScene has no legacy Player Ship Proxy object.");
            report.Check(!HasActiveLegacyWorldShipPrimitive(worldSceneText), "WorldScene has no active legacy primitive ship children.");
        }
#else
        report.Check(true, "Starter hull visual asset contract validation is editor-only and is skipped in player builds.");
#endif
    }

#if UNITY_EDITOR
    private static void ValidateStarterHullSceneYaml(BigTestReport report, string assetPath, string[] requiredMeshReferences, string[] forbiddenGuids, string label)
    {
        string text = ReadProjectText(assetPath);
        report.Check(!string.IsNullOrEmpty(text), label + " is readable.");
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        bool referencesAllSplitMeshes = true;
        if (requiredMeshReferences != null)
        {
            for (int i = 0; i < requiredMeshReferences.Length; i++)
            {
                if (!text.Contains(requiredMeshReferences[i]))
                {
                    referencesAllSplitMeshes = false;
                    break;
                }
            }
        }

        bool hasTurretHierarchyNames = text.Contains("m_Name: \"\\u041E\\u0441\\u043D\\u043E\\u0432\\u0430\\u043D\\u0438\\u0435_\\u0442\\u0443\\u0440\\u0435\\u043B\\u0438\"") &&
            text.Contains("m_Name: \"\\u0422\\u0443\\u0440\\u0435\\u043B\\u044C\"") &&
            text.Contains("m_Name: \"\\u0421\\u0442\\u0432\\u043E\\u043B\"") &&
            text.Contains("m_Name: Muzzle");
        bool avoidsForbiddenGuids = true;
        if (forbiddenGuids != null)
        {
            for (int i = 0; i < forbiddenGuids.Length; i++)
            {
                if (!string.IsNullOrEmpty(forbiddenGuids[i]) && text.Contains(forbiddenGuids[i]))
                {
                    avoidsForbiddenGuids = false;
                    break;
                }
            }
        }

        report.Check(referencesAllSplitMeshes, label + " references body, turret base, turret and barrel split meshes.");
        report.Check(hasTurretHierarchyNames, label + " keeps named gun hierarchy objects for runtime binding.");
        report.Check(avoidsForbiddenGuids, label + " does not reference source OBJ or old merged visual mesh GUIDs at runtime.");
    }

    private static int CountMeshTriangles(Mesh mesh)
    {
        return mesh != null && mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
    }

    private static bool MeshFiltersContainMesh(MeshFilter[] filters, Mesh mesh)
    {
        if (filters == null || mesh == null)
        {
            return false;
        }

        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] != null && filters[i].sharedMesh == mesh)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StarterHullYamlHasFreeGunCost(string assetPath)
    {
        string text = ReadProjectText(assetPath);
        return !string.IsNullOrEmpty(text) &&
            text.Contains("weaponShotCostKg: 0") &&
            !text.Contains("weaponShotCostKg: 0.1");
    }

    private static bool StarterHullYamlHasCurrentBarrelBinding(string assetPath)
    {
        string text = ReadProjectText(assetPath);
        return !string.IsNullOrEmpty(text) &&
            text.Contains("m_LocalPosition: {x: 0, y: 0.1120243, z: 0.4466348}") &&
            text.Contains("m_LocalPosition: {x: 0, y: 0, z: 1.5438662}");
    }

    private static bool StarterHullBarrelAxisIsBakedAlongLocalZ(Vector3 size)
    {
        return IsFinite(size) &&
            size.z >= 1.5f &&
            size.z > size.y * 3f &&
            size.z > size.x * 10f;
    }

    private static bool TryReadNativeMeshLocalAabbSize(string assetPath, out Vector3 size)
    {
        size = Vector3.zero;
        string text = ReadProjectText(assetPath);
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        const string extentMarker = "m_Extent:";
        int extentIndex = text.IndexOf(extentMarker, StringComparison.Ordinal);
        if (extentIndex < 0)
        {
            return false;
        }

        int lineEnd = text.IndexOf('\n', extentIndex);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        string extentLine = text.Substring(extentIndex, lineEnd - extentIndex);
        float x;
        float y;
        float z;
        if (!TryReadYamlVectorComponent(extentLine, "x", out x) ||
            !TryReadYamlVectorComponent(extentLine, "y", out y) ||
            !TryReadYamlVectorComponent(extentLine, "z", out z))
        {
            return false;
        }

        size = new Vector3(Mathf.Abs(x) * 2f, Mathf.Abs(y) * 2f, Mathf.Abs(z) * 2f);
        return IsFinite(size);
    }

    private static bool TryReadYamlVectorComponent(string line, string componentName, out float value)
    {
        value = 0f;
        string marker = componentName + ":";
        int valueStart = line.IndexOf(marker, StringComparison.Ordinal);
        if (valueStart < 0)
        {
            return false;
        }

        valueStart += marker.Length;
        int valueEnd = line.IndexOf(',', valueStart);
        int braceEnd = line.IndexOf('}', valueStart);
        if (valueEnd < 0 || braceEnd >= 0 && braceEnd < valueEnd)
        {
            valueEnd = braceEnd;
        }

        if (valueEnd < 0)
        {
            valueEnd = line.Length;
        }

        string token = line.Substring(valueStart, valueEnd - valueStart).Trim();
        return float.TryParse(
            token,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    private static void ValidateStarterHullRuntimeTransformContract(BigTestReport report, string starterHullPrefabPath, string worldScenePath)
    {
        string prefabText = ReadProjectText(starterHullPrefabPath);
        if (!string.IsNullOrEmpty(prefabText))
        {
            ValidateStarterHullTransformIsNotHalfTurnY(report, prefabText, "1441708272496266707",
                "StarterHull socket container is not rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, prefabText, "8292213452475038823",
                "StarterHull visible mesh child is not double-rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, prefabText, "701100000000000202",
                "StarterHull turret yaw pivot is not double-rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, prefabText, "701100000000000302",
                "StarterHull barrel pitch pivot is not double-rotated 180 degrees.");
        }

        string worldSceneText = ReadProjectText(worldScenePath);
        if (!string.IsNullOrEmpty(worldSceneText))
        {
            ValidateStarterHullTransformIsNotHalfTurnY(report, worldSceneText, "881536966",
                "WorldScene starter hull visual child is not double-rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, worldSceneText, "901536000102",
                "WorldScene turret yaw pivot is not double-rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, worldSceneText, "901536000202",
                "WorldScene barrel pitch pivot is not double-rotated 180 degrees.");
        }
    }

    private static void ValidateStarterHullTransformIsNotHalfTurnY(
        BigTestReport report,
        string yamlText,
        string transformFileId,
        string message)
    {
        bool exists = TryGetYamlBlock(yamlText, "--- !u!4 &" + transformFileId, out string block);
        report.Check(exists, message + " Transform YAML exists.");
        if (!exists)
        {
            return;
        }

        bool halfTurnY = block.Contains("m_LocalRotation: {x: 0, y: 1, z: 0, w: 0}") ||
            block.Contains("m_LocalEulerAnglesHint: {x: 0, y: 180, z: 0}");
        report.Check(!halfTurnY, message);
    }

    private static bool StarterHullNoseFacesPositiveZ(Vector3[] vertices, out string message)
    {
        message = "no vertices.";
        if (vertices == null || vertices.Length == 0)
        {
            return false;
        }

        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            if (!IsFinite(vertex))
            {
                continue;
            }

            minZ = Mathf.Min(minZ, vertex.z);
            maxZ = Mathf.Max(maxZ, vertex.z);
        }

        if (!IsFinite(minZ) || !IsFinite(maxZ) || maxZ <= minZ)
        {
            message = "invalid z bounds.";
            return false;
        }

        float length = maxZ - minZ;
        float sampleDepth = Mathf.Max(0.5f, length * 0.05f);
        float bowHalfWidth = 0f;
        float sternHalfWidth = 0f;
        int bowCount = 0;
        int sternCount = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            if (!IsFinite(vertex))
            {
                continue;
            }

            if (vertex.z >= maxZ - sampleDepth)
            {
                bowHalfWidth = Mathf.Max(bowHalfWidth, Mathf.Abs(vertex.x));
                bowCount++;
            }

            if (vertex.z <= minZ + sampleDepth)
            {
                sternHalfWidth = Mathf.Max(sternHalfWidth, Mathf.Abs(vertex.x));
                sternCount++;
            }
        }

        message = "bow(+Z) half-width " + bowHalfWidth.ToString("0.###") +
            "m across " + bowCount +
            " vertices, stern(-Z) half-width " + sternHalfWidth.ToString("0.###") +
            "m across " + sternCount + " vertices.";
        return bowCount > 0 && sternCount > 0 && sternHalfWidth >= 1f && bowHalfWidth <= sternHalfWidth * 0.65f;
    }

    private static bool StarterHullSourceObjIncludesWeaponParts(string sourceObjText, out string message)
    {
        if (string.IsNullOrEmpty(sourceObjText))
        {
            message = "source OBJ is empty.";
            return false;
        }

        bool hasTurretBase = sourceObjText.Contains("o \u041e\u0441\u043d\u043e\u0432\u0430\u043d\u0438\u0435_\u0442\u0443\u0440\u0435\u043b\u0438");
        bool hasTurret = sourceObjText.Contains("o \u0422\u0443\u0440\u0435\u043b\u044c");
        bool hasBarrel = sourceObjText.Contains("o \u0421\u0442\u0432\u043e\u043b");
        bool hasDuplicateMergedHull = sourceObjText.Contains("o ShipHull_Emitters_Unity");
        int materialBandCount = CountOccurrences(sourceObjText, "usemtl ");

        message = "base=" + hasTurretBase +
            ", turret=" + hasTurret +
            ", barrel=" + hasBarrel +
            ", duplicateMergedHull=" + hasDuplicateMergedHull +
            ", material switches=" + materialBandCount + ".";
        return hasTurretBase && hasTurret && hasBarrel && !hasDuplicateMergedHull && materialBandCount >= 3;
    }

    private static int CountOccurrences(string text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
        {
            return 0;
        }

        int count = 0;
        int index = 0;
        while (index < text.Length)
        {
            int found = text.IndexOf(value, index, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            count++;
            index = found + value.Length;
        }

        return count;
    }

    private static bool TryGetYamlBlock(string yamlText, string marker, out string block)
    {
        int start = yamlText.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            block = "";
            return false;
        }

        int end = yamlText.IndexOf("\n--- !u!", start + marker.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            end = yamlText.Length;
        }

        block = yamlText.Substring(start, end - start);
        return true;
    }

    private static string ReadProjectText(string assetPath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
    }

    private static bool HasActiveLegacyWorldShipPrimitive(string sceneText)
    {
        return HasActiveYamlObject(sceneText, "Balloon") ||
            HasActiveYamlObject(sceneText, "Keel Cabin") ||
            HasActiveYamlObject(sceneText, "Balloon Band Front") ||
            HasActiveYamlObject(sceneText, "Balloon Band Back");
    }

    private static bool HasActiveYamlObject(string sceneText, string objectName)
    {
        string marker = "  m_Name: " + objectName;
        int index = 0;
        while (index >= 0 && index < sceneText.Length)
        {
            int nameIndex = sceneText.IndexOf(marker, index, StringComparison.Ordinal);
            if (nameIndex < 0)
            {
                return false;
            }

            int blockEnd = sceneText.IndexOf("\n--- !u!", nameIndex, StringComparison.Ordinal);
            if (blockEnd < 0)
            {
                blockEnd = sceneText.Length;
            }

            string block = sceneText.Substring(nameIndex, blockEnd - nameIndex);
            if (block.Contains("  m_IsActive: 1"))
            {
                return true;
            }

            index = blockEnd + 1;
        }

        return false;
    }
#endif

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
        SpecialModuleConfig jaeger = config.GetSpecialModule("jaeger_harpoon_rig");
        SpecialModuleConfig jaegerMk2 = config.GetSpecialModule("jaeger_harpoon_rig_mk2");
        SpecialModuleConfig opora = config.GetSpecialModule("opora_crane_platform");
        SpecialModuleConfig fuelTender = config.GetSpecialModule("fuel_tender_tanks");
        SpecialModuleConfig parovoz = config.GetSpecialModule("parovoz_passenger_cabin");

        ValidateR1ShipCatalog(config, report);

        report.Check(waterStrider != null &&
            waterStrider.gasHarvesterWaterOnly &&
            waterStrider.gasHarvesterVolumeM3PerSecond > 0f &&
            waterStrider.gasHarvesterPowerDrawKw > 0f &&
            waterStrider.liquidTankCapacityKg >= 1000f,
            "Vodomerka module exists: water-only cloud harvester and 1000 l liquid tank.");

        report.Check(bulat != null &&
            bulat.miningImpactHoldCapacityKg >= 3000f &&
            Approximately(bulat.miningImpactDamageTakenMultiplier, 0.5f, 0.001f) &&
            bulat.bulkHoldCapacityKg >= 3000f,
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
            jaeger.cargoVanCapacityKg >= jaeger.harpoonMaxCarcassMassKg,
            "Eger base module exists: harpoon upkeep, 100 kg target limit and ordinary cargo room for trophies.");

        report.Check(jaeger != null &&
            jaegerMk2 != null &&
            Approximately(jaegerMk2.harpoonWeaponCostPerMinute, 4f, 0.001f) &&
            jaegerMk2.harpoonMaxCarcassMassKg >= 200f &&
            jaegerMk2.harpoonFlightDamage > jaeger.harpoonFlightDamage &&
            jaegerMk2.cargoVanCapacityKg >= jaegerMk2.harpoonMaxCarcassMassKg,
            "Eger upgraded harpoon exists: 200 kg limit, more damage and ordinary cargo room for trophies.");

        report.Check(opora != null &&
            opora.needRepairRecoveryPerHour > 0f &&
            opora.cargoVanCapacityKg >= 6000f,
            "Opora module exists: repair recovery and large van deck.");

        report.Check(fuelTender != null &&
            fuelTender.cargoVanCapacityKg >= 6400f,
            "Fuel tender module exists: large generic cargo capacity.");

        report.Check(parovoz != null &&
            parovoz.passengerSeatCapacity >= 15f &&
            parovoz.cargoVanCapacityKg > 0f,
            "Parovoz module exists: 15 passenger seats and supplies van.");

        LogisticsShipMetrics trophyCargoMetrics = new LogisticsShipMetrics
        {
            cargoCompartments = new List<CargoCompartmentDefinition>
            {
                new CargoCompartmentDefinition { storageKind = CargoStorageKind.Van, capacity = 100f }
            }
        };
        bool trophyCargoAcceptsCarcass = CargoStoragePlanner.TryValidateCargoStorage(
            config,
            trophyCargoMetrics,
            new Dictionary<string, int> { ["windcalf_carcass"] = 80 },
            out _);
        bool trophyCargoRejectsOverflow = !CargoStoragePlanner.TryValidateCargoStorage(
            config,
            trophyCargoMetrics,
            new Dictionary<string, int> { ["windcalf_carcass"] = 120 },
            out _);
        bool genericHoldAcceptsCarcass = CargoStoragePlanner.TryValidateCargoStorage(
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
        report.Check(trophyCargoAcceptsCarcass && trophyCargoRejectsOverflow && genericHoldAcceptsCarcass,
            "Leviathan carcasses are ordinary cargo by weight and do not need a special refrigerator compartment.");

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
            ValidateR2TenderCatalog(config, catalog, report);
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
                    Approximately(stats.Get(ShipStatId.LiquidTankCapacityKg, 0f), maximum ? 1500f : 1000f, 0.001f);
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
                    Approximately(stats.Get(ShipStatId.BulkHoldCapacityKg, 0f), 3000f, 0.001f) &&
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
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), maximum ? 250f : 150f, 0.001f) &&
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
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), maximum ? 14000f : 6000f, 0.001f);
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
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), 6400f, 0.001f);
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
            waterMk2.liquidTankCapacityKg >= 1500f &&
            waterMk2.baseMassKg > waterBase.baseMassKg,
            "Water Strider upgraded harvester keeps water-only mode and grows to a 1.5 t tank.");

        SpecialModuleConfig bulatBase = config.GetSpecialModule("bulat_impact_hold");
        SpecialModuleConfig bulatMk2 = config.GetSpecialModule("bulat_impact_hold_mk2");
        report.Check(bulatBase != null &&
            bulatMk2 != null &&
            bulatMk2.miningImpactHoldCapacityKg >= 3000f &&
            bulatMk2.bulkHoldCapacityKg >= 3000f &&
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

        SpecialModuleConfig jaegerBase = config.GetSpecialModule("jaeger_harpoon_rig");
        SpecialModuleConfig jaegerMk2 = config.GetSpecialModule("jaeger_harpoon_rig_mk2");
        report.Check(jaegerBase != null &&
            jaegerMk2 != null &&
            jaegerMk2.harpoonMaxCarcassMassKg >= 200f &&
            Approximately(jaegerMk2.harpoonWeaponCostPerMinute, jaegerBase.harpoonWeaponCostPerMinute * 2f, 0.001f) &&
            jaegerMk2.cargoVanCapacityKg >= jaegerMk2.harpoonMaxCarcassMassKg,
            "Eger upgraded harpoon catches 200 kg carcasses and doubles weapon drain without special cold storage.");

        SpecialModuleConfig oporaBase = config.GetSpecialModule("opora_crane_platform");
        SpecialModuleConfig oporaMk2 = config.GetSpecialModule("opora_crane_platform_mk2");
        report.Check(oporaBase != null &&
            oporaMk2 != null &&
            oporaMk2.needRepairRecoveryPerHour > oporaBase.needRepairRecoveryPerHour &&
            oporaMk2.cargoVanCapacityKg >= 14000f &&
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

    private static void ValidateR2TenderCatalog(WorldConfigDatabase config, ShipCatalogSO catalog, BigTestReport report)
    {
        report.Check(R2TenderDesignCatalog.All.Count == 5,
            "R2 tender catalog contains five follow-up tenders: Liquid Tanker, Gletcher, Vakhta, Boxvan Tender and Stapel.");

        string[] requiredR2TenderTechnologyIds =
        {
            "liquid_tender_tanks",
            "bulk_gas_tender",
            "workforce_health_tender",
            "boxed_goods_tender",
            "field_flying_dock"
        };

        bool allTenderTechRowsExist = true;
        for (int i = 0; i < requiredR2TenderTechnologyIds.Length; i++)
        {
            allTenderTechRowsExist &= config.GetTechnology(requiredR2TenderTechnologyIds[i]) != null;
        }

        report.Check(allTenderTechRowsExist,
            "R2 tender technology rows exist for liquid, bulk/gas, workforce/health, boxed goods and field dock roles.");

        for (int i = 0; i < R2TenderDesignCatalog.All.Count; i++)
        {
            R1ShipDesignDefinition design = R2TenderDesignCatalog.All[i];
            if (design == null) continue;

            ShipTreeEntryConfig entry = config.GetShipTreeEntry(design.shipId);
            bool treeEntrySynced = entry != null &&
                entry.rank == 2 &&
                entry.requiredTechnologyId == design.requiredTechId &&
                entry.hullId == design.hullId &&
                entry.engineId == design.engineId &&
                entry.propellerId == design.propellerId &&
                entry.claudiumLoopId == design.claudiumLoopId &&
                entry.specialModuleId == design.specialModuleId;
            report.Check(treeEntrySynced,
                design.displayNameRu + " is present in Ship_tree.csv as a rank 2 tender and matches its configured loadout.");

            report.Check(R1ConfiguredPartRowsExist(config, design),
                design.displayNameRu + " has CSV rows for hull, engine, propeller, claudium loop and role module.");

            report.Check(R1ConfiguredSlotsAllowParts(catalog, design),
                design.displayNameRu + " hull slots are locked to its tender loadout.");

            PlayerProgress progress = R1ShipDesignCatalog.CreateUnlockedProgress(design);
            bool assembled = ShipAssemblyBuilder.TryBuild(catalog, null, progress, out ShipAssemblyResult result);
            report.Check(assembled, assembled
                ? design.displayNameRu + " assembles from CSV catalog."
                : design.displayNameRu + " does not assemble from CSV catalog: " + result.message);

            if (!assembled || result == null || result.stats == null)
            {
                continue;
            }

            report.Check(R2TenderCoreStatsMatch(design, result.stats),
                design.displayNameRu + " core tender stats match configured mass, engine, lift and structure.");

            report.Check(R2TenderRoleStatsConfigured(design.shipId, result.stats),
                design.displayNameRu + " role stats are configured on the mandatory module.");

            report.Check(R2TenderCargoRulesMatch(config, design.shipId, result.stats),
                design.displayNameRu + " cargo rules use simplified weight-based freight.");
        }
    }

    private static bool R2TenderCoreStatsMatch(R1ShipDesignDefinition design, ShipStatBlock stats)
    {
        if (design == null || stats == null) return false;

        return Approximately(stats.Get(ShipStatId.BaseMass, 0f), design.expectedServiceMassKg, 1f) &&
            Approximately(stats.Get(ShipStatId.HullMaxTakeoffMassKg, 0f), design.expectedMaxTakeoffMassKg, 0.01f) &&
            Approximately(stats.Get(ShipStatId.EngineMaxPower, 0f), design.expectedEnginePowerKw, 0.01f) &&
            Approximately(stats.Get(ShipStatId.StructureHp, 0f), design.expectedStructureHp, 0.01f) &&
            Approximately(stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f), design.expectedClaudiumLiftEfficiency, 0.01f) &&
            stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f) >= design.expectedMaxTakeoffMassKg - 0.01f &&
            stats.Get(ShipStatId.PropellerMaxSpeedMS, 0f) > 0f;
    }

    private static bool R2TenderRoleStatsConfigured(string shipId, ShipStatBlock stats)
    {
        if (stats == null) return false;

        switch (shipId)
        {
            case "liquid_tanker":
                return Approximately(stats.Get(ShipStatId.LiquidTankCapacityKg, 0f), 8000f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), 0f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.BulkHoldCapacityKg, 0f), 0f, 0.001f);
            case "gletcher":
                return Approximately(stats.Get(ShipStatId.BulkHoldCapacityKg, 0f), 3000f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.GasCylinderCapacityKg, 0f), 4000f, 0.001f);
            case "vakhta":
                return Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), 5000f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedWorkforceRecoveryPerHour, 0f), 55f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedHealthRecoveryPerHour, 0f), 40f, 0.001f);
            case "boxvan_tender":
                return Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), 5700f, 0.001f);
            case "stapel":
                return Approximately(stats.Get(ShipStatId.ShipDockSlots, 0f), 2f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.ShipDockMaxClass, 0f), (float)ShipSizeClass.Boat, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.NeedRepairRecoveryPerHour, 0f), 40f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.CargoVanCapacityKg, 0f), 1200f, 0.001f);
            default:
                return false;
        }
    }

    private static bool R2TenderCargoRulesMatch(WorldConfigDatabase config, string shipId, ShipStatBlock stats)
    {
        if (config == null || stats == null) return false;

        LogisticsShipMetrics metrics = new LogisticsShipMetrics
        {
            cargoCompartments = CargoStoragePlanner.BuildStatCompartments(stats)
        };

        switch (shipId)
        {
            case "liquid_tanker":
                return CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["water"] = 8000 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["food"] = 1 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["windshale_ore"] = 1 }, out _) &&
                    !CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["water"] = 8001 }, out _);
            case "gletcher":
                return CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["windshale_ore"] = 3000, ["aer_silt"] = 4000 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["water"] = 1 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["food"] = 1 }, out _) &&
                    !CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["water"] = 7001 }, out _);
            case "vakhta":
                return CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["food"] = 3000, ["medicines"] = 1000 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["paper"] = 1 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["water"] = 1 }, out _) &&
                    !CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["passengers_to_capital"] = 1 }, out _);
            case "boxvan_tender":
                return CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["food"] = 1000, ["tools"] = 100 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["charcoal"] = 1 }, out _) &&
                    !CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["passengers_to_capital"] = 1 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["water"] = 1 }, out _);
            case "stapel":
                return CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["utility_boat_ship"] = 2 }, out _) &&
                    !CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["utility_boat_ship"] = 3 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["charcoal"] = 400, ["claudium"] = 200, ["tools"] = 20 }, out _) &&
                    CargoStoragePlanner.TryValidateCargoStorage(config, metrics, new Dictionary<string, int> { ["food"] = 1 }, out _);
            default:
                return false;
        }
    }

    private static bool R1RoleStatsConfigured(string shipId, ShipStatBlock stats)
    {
        if (stats == null) return false;

        switch (shipId)
        {
            case "water_strider":
                return stats.Get(ShipStatId.GasHarvesterWaterOnly, 0f) > 0.5f &&
                    stats.Get(ShipStatId.GasHarvesterVolumeM3PerSecond, 0f) > 0f &&
                    stats.Get(ShipStatId.LiquidTankCapacityKg, 0f) >= 1000f;
            case "bulat":
                return stats.Get(ShipStatId.MiningImpactHoldCapacityKg, 0f) >= 3000f &&
                    Approximately(stats.Get(ShipStatId.MiningImpactDamageTakenMultiplier, 0f), 0.5f, 0.001f) &&
                    stats.Get(ShipStatId.BulkHoldCapacityKg, 0f) >= 3000f;
            case "hornet":
                return stats.Get(ShipStatId.ObservationRadiusMeters, 0f) >= 700f &&
                    Approximately(stats.Get(ShipStatId.SurveyPaperToInfoEfficiency, 0f), 0.2f, 0.001f) &&
                    Approximately(stats.Get(ShipStatId.LeviathanAlarmGenerationMultiplier, 0f), 0.5f, 0.001f);
            case "jaeger":
                return Approximately(stats.Get(ShipStatId.HarpoonWeaponCostPerMinute, 0f), 2f, 0.001f) &&
                    stats.Get(ShipStatId.HarpoonMaxCarcassMassKg, 0f) >= 100f &&
                    stats.Get(ShipStatId.CargoVanCapacityKg, 0f) >= stats.Get(ShipStatId.HarpoonMaxCarcassMassKg, 0f) &&
                    stats.Get(ShipStatId.NeedSafetyRecoveryPerHour, 0f) > 0f;
            case "opora":
                return stats.Get(ShipStatId.NeedRepairRecoveryPerHour, 0f) > 0f &&
                    stats.Get(ShipStatId.CargoVanCapacityKg, 0f) >= 6000f;
            case "fuel_tender":
                return stats.Get(ShipStatId.CargoVanCapacityKg, 0f) >= 6400f;
            case "parovoz":
                return stats.Get(ShipStatId.PassengerSeatCapacity, 0f) >= 15f &&
                    stats.Get(ShipStatId.CargoVanCapacityKg, 0f) >= 250f &&
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

        PlayerProgress smallShipInteriorProgress = new PlayerProgress();
        smallShipInteriorProgress.Normalize();
        smallShipInteriorProgress.selectedHullId = "starter_hull";
        long flagshipStartTicks = DateTime.UtcNow.Ticks;
        FlagshipInteriorSimulator.Advance(config, smallShipInteriorProgress, flagshipStartTicks, flagshipStartTicks + TimeSpan.FromHours(3).Ticks);
        report.Check(smallShipInteriorProgress.flagshipInteriors.Count == 0, "R0-R2 ships do not create flagship interiors or social needs.");

        WorldConfigDatabase syntheticFlagshipConfig = new WorldConfigDatabase();
        syntheticFlagshipConfig.shipTreeEntries.Add(new ShipTreeEntryConfig
        {
            shipId = "synthetic_flagship",
            localNameRu = "Synthetic Flagship",
            rank = 3,
            hullId = "synthetic_flagship_hull"
        });
        PlayerProgress playerFlagshipRouteProgress = new PlayerProgress();
        playerFlagshipRouteProgress.Normalize();
        playerFlagshipRouteProgress.selectedHullId = "synthetic_flagship_hull";
        FlagshipInteriorState playerFlagshipRoute = FlagshipInteriorSimulator.EnsurePlayerFlagshipInterior(syntheticFlagshipConfig, playerFlagshipRouteProgress);
        FlagshipExpeditionDefinition syntheticExpedition = new FlagshipExpeditionDefinition
        {
            expeditionId = "synthetic_expedition",
            displayNameRu = "Synthetic Expedition",
            regionId = "synthetic_region",
            returnDockId = "capital",
            returnDockKind = DockingLocationKind.Island,
            minimumFlagshipRank = 3,
            moraleDrainMultiplier = 2f
        };
        playerFlagshipRouteProgress.activeExpedition.Begin(syntheticExpedition, flagshipStartTicks);
        bool playerFlagshipRouteStarted = FlagshipInteriorSimulator.StartExpedition(
            playerFlagshipRouteProgress,
            FlagshipInteriorSimulator.PlayerFlagshipId,
            flagshipStartTicks,
            out _);
        FlagshipNeedState playerFlagshipRouteMorale = playerFlagshipRoute != null
            ? playerFlagshipRoute.GetNeedState(FlagshipNeedIds.Morale, true)
            : null;
        if (playerFlagshipRouteMorale != null)
        {
            playerFlagshipRouteMorale.currentValue = playerFlagshipRouteMorale.maxValue;
            playerFlagshipRouteMorale.initialized = true;
        }
        float playerFlagshipRouteMoraleBefore = playerFlagshipRouteMorale != null ? playerFlagshipRouteMorale.currentValue : 0f;
        FlagshipInteriorSimulator.Advance(syntheticFlagshipConfig, playerFlagshipRouteProgress, flagshipStartTicks, flagshipStartTicks + TimeSpan.FromHours(1).Ticks);
        bool playerFlagshipRouteMoraleDrained = playerFlagshipRouteMorale != null &&
            playerFlagshipRouteMorale.currentValue <= playerFlagshipRouteMoraleBefore - 7.5f;
        bool playerFlagshipRouteReturned = FlagshipInteriorSimulator.CompleteExpeditionReturn(
            playerFlagshipRouteProgress,
            FlagshipInteriorSimulator.PlayerFlagshipId,
            out _);
        playerFlagshipRouteProgress.activeExpedition.Clear();
        report.Check(playerFlagshipRoute != null &&
            playerFlagshipRoute.flagshipId == FlagshipInteriorSimulator.PlayerFlagshipId &&
            playerFlagshipRoute.rank == 3 &&
            playerFlagshipRouteStarted &&
            playerFlagshipRouteProgress.activeExpedition != null &&
            !playerFlagshipRouteProgress.activeExpedition.active &&
            playerFlagshipRouteMoraleDrained &&
            playerFlagshipRouteReturned &&
            !playerFlagshipRoute.expeditionActive &&
            playerFlagshipRouteMorale != null &&
            Approximately(playerFlagshipRouteMorale.currentValue, playerFlagshipRouteMorale.maxValue, 0.001f),
            "Selected R3+ hull creates player flagship expedition state, drains morale by region multiplier and returns home cleanly.");

        PlayerProgress flagshipProgress = new PlayerProgress();
        flagshipProgress.Normalize();
        flagshipProgress.AddShipCargo("food", 20);
        flagshipProgress.AddShipCargo("tools", 20);
        flagshipProgress.AddShipCargo(PassengerCargoIds.ToShipItemId("test_flagship"), 2);
        int flagshipPassengerCargoBefore = flagshipProgress.GetShipCargoAmount(PassengerCargoIds.ToShipItemId("test_flagship"));
        FlagshipInteriorState flagship = flagshipProgress.GetFlagshipInteriorState("test_flagship", true);
        flagship.rank = 3;
        flagship.crewCapacity = 6;
        FlagshipInteriorSimulator.EnsureDefaultInterior(flagship);
        bool flagshipExpeditionStarted = FlagshipInteriorSimulator.StartExpedition(flagshipProgress, "test_flagship", flagshipStartTicks, out _);

        FlagshipNeedState flagshipStamina = flagship.GetNeedState(FlagshipNeedIds.Stamina, true);
        FlagshipNeedState flagshipMaintenance = flagship.GetNeedState(FlagshipNeedIds.Maintenance, true);
        FlagshipNeedState flagshipMorale = flagship.GetNeedState(FlagshipNeedIds.Morale, true);
        float flagshipMoraleBefore = flagshipMorale.currentValue;
        flagshipStamina.currentValue = 0f;
        flagshipMaintenance.currentValue = 0f;
        flagshipStamina.initialized = true;
        flagshipMaintenance.initialized = true;
        flagshipMorale.initialized = true;

        int flagshipRecoveryEvents = FlagshipInteriorSimulator.Advance(config, flagshipProgress, flagshipStartTicks, flagshipStartTicks + TimeSpan.FromHours(1).Ticks);
        bool flagshipNeedsRecovered = flagshipRecoveryEvents > 0 &&
            flagshipStamina.currentValue > 0f &&
            flagshipMaintenance.currentValue > 0f &&
            flagshipMorale.currentValue < flagshipMoraleBefore &&
            flagshipProgress.GetShipCargoAmount("food") < 20 &&
            flagshipProgress.GetShipCargoAmount("tools") < 20 &&
            flagshipProgress.GetShipCargoAmount(PassengerCargoIds.ToShipItemId("test_flagship")) == flagshipPassengerCargoBefore;
        bool flagshipMoraleRestoredByReturn = FlagshipInteriorSimulator.CompleteExpeditionReturn(flagshipProgress, "test_flagship", out _) &&
            !flagship.expeditionActive &&
            Approximately(flagshipMorale.currentValue, flagshipMorale.maxValue, 0.001f);
        report.Check(flagshipExpeditionStarted && flagshipNeedsRecovered && flagshipMoraleRestoredByReturn,
            "R3+ flagship service rooms restore stamina and maintenance, while morale is an expedition timer restored only by returning home.");

        FlagshipFailureState testFailure = FlagshipInteriorSimulator.AddFailure(
            flagship,
            "engine_room",
            FlagshipFailureSeverity.Major,
            FlagshipFailureEffectKind.EfficiencyPenalty,
            "",
            0.45f,
            200f,
            flagshipStartTicks);
        FlagshipInteriorSimulator.SetRoomMode(flagshipProgress, "test_flagship", "repair_workshop", FlagshipRoomMode.Off);
        FlagshipInteriorSimulator.Advance(config, flagshipProgress, flagshipStartTicks, flagshipStartTicks + TimeSpan.FromMinutes(30).Ticks);
        bool noRepairWhenWorkshopOff = testFailure != null && Approximately(testFailure.autoRepairProgress, 0f, 0.001f);

        for (int i = 0; i < FlagshipNeedIds.All.Length; i++)
        {
            FlagshipNeedState need = flagship.GetNeedState(FlagshipNeedIds.All[i], true);
            need.currentValue = need.maxValue;
        }

        FlagshipInteriorSimulator.SetRoomMode(flagshipProgress, "test_flagship", "repair_workshop", FlagshipRoomMode.Full);
        FlagshipInteriorSimulator.Advance(config, flagshipProgress, flagshipStartTicks + TimeSpan.FromMinutes(30).Ticks, flagshipStartTicks + TimeSpan.FromMinutes(60).Ticks);
        bool autoRepairProgressed = testFailure != null && testFailure.autoRepairProgress > 0f && testFailure.autoRepairProgress < testFailure.repairWorkRequired;
        bool manualRepairCompleted = testFailure != null &&
            FlagshipInteriorSimulator.CompleteManualRepair(flagshipProgress, "test_flagship", testFailure.failureId, out _) &&
            flagship.FindFailure(testFailure.failureId, out _) == null;
        report.Check(noRepairWhenWorkshopOff && autoRepairProgressed && manualRepairCompleted,
            "Flagship failures wait without repair rooms, progress under auto-repair and can be cleared instantly by a manual repair puzzle.");

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
        report.Check(world.Islands.Count == 6, "Учебный регион содержит столицу и пять островов: " + world.Islands.Count + ".");
        report.Check(world.CloudFields.Count == 3, "Учебный регион содержит только стартовые облака: " + world.CloudFields.Count + ".");
        report.Check(world.ResourceFields.Count == 1, "Учебный регион содержит одну учебную глыбу: " + world.ResourceFields.Count + ".");
        report.Check(world.LeviathanRegions.Count == 1, "Учебный регион содержит одну зону малых левиафанов: " + world.LeviathanRegions.Count + ".");
        report.Check(world.IcebergFields.Count == 0, "Учебный регион пока не содержит айсбергов: " + world.IcebergFields.Count + ".");

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
        bool generatedSessionUsable = generatedSave != null &&
            generatedSave.gameplaySession != null &&
            generatedSave.gameplaySession.IsUsable &&
            generatedSave.gameplaySession.mode == GameSessionMode.Docked &&
            generatedSave.gameplaySession.dockId == GameplaySessionSaveData.DefaultDockId &&
            generatedSave.progress != null &&
            generatedSave.progress.hasCurrentDockPosition;
        report.Check(generatedUsable, "Новая игра создаёт save data с версией, seed, manifest и runtime-заготовкой.");

        report.Check(generatedSessionUsable, "New world save includes a usable gameplay session with player ship pose and dock state.");

        string json = JsonUtility.ToJson(generatedSave, true);
        MetaGameSaveData loadedSave = JsonUtility.FromJson<MetaGameSaveData>(json);
        bool jsonRoundTrip = loadedSave != null &&
            loadedSave.worldManifest != null &&
            loadedSave.worldManifest.IsUsable &&
            loadedSave.worldManifest.seed == seed &&
            loadedSave.worldRuntime != null &&
            loadedSave.worldRuntime.IsUsable;
        bool sessionJsonRoundTrip = loadedSave != null &&
            loadedSave.gameplaySession != null &&
            loadedSave.gameplaySession.IsUsable &&
            loadedSave.gameplaySession.dockId == GameplaySessionSaveData.DefaultDockId &&
            loadedSave.gameplaySession.hasPlayerPose;
        report.Check(jsonRoundTrip, "Save slot мира проходит JSON round-trip.");

        report.Check(sessionJsonRoundTrip, "GameplaySessionSaveData survives the save slot JSON round-trip.");

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

            string previousSelectedForFlow = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty();
            bool previousPendingForFlow = PlayerPrefs.GetInt(WildWindSaveSlots.PendingGameplayLaunchPlayerPrefsKey, 0) == 1;
            try
            {
                bool flowPrepared = WildWindSessionFlow.TryPrepareExistingWorldLaunch(tempSlotName, out string flowPrepareError);
                bool flowSelected = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty() == tempSlotName;
                bool pendingLaunchConsumed = WildWindSaveSlots.ConsumePendingGameplayLaunch();
                bool pendingLaunchCleared = !WildWindSaveSlots.ConsumePendingGameplayLaunch();
                report.Check(flowPrepared && flowSelected && pendingLaunchConsumed && pendingLaunchCleared,
                    flowPrepared
                        ? "SessionFlow готовит Continue-слот к одноразовому запуску gameplay-сцены."
                        : "SessionFlow не смог подготовить Continue-слот: " + flowPrepareError);
            }
            finally
            {
                RestoreSessionLoopPrefs(previousSelectedForFlow, previousPendingForFlow);
            }
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

        string tempSessionSlotName = BigTestSessionSavePrefix + DateTime.UtcNow.Ticks + ".json";
        string tempSessionSlotPath = WildWindSaveSlots.GetSavePath(tempSessionSlotName);
        try
        {
            File.WriteAllText(tempSessionSlotPath, json);
            List<WildWindSaveSlotInfo> visibleSlots = WildWindSaveSlots.GetExistingSlots();
            bool tempSessionListed = false;
            for (int i = 0; i < visibleSlots.Count; i++)
            {
                if (visibleSlots[i] != null && visibleSlots[i].fileName == tempSessionSlotName)
                {
                    tempSessionListed = true;
                    break;
                }
            }

            report.Check(!tempSessionListed, "Temporary big test session save slots are hidden from Continue.");

            string previousSelectedForTempSession = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty();
            bool previousPendingForTempSession = PlayerPrefs.GetInt(WildWindSaveSlots.PendingGameplayLaunchPlayerPrefsKey, 0) == 1;
            try
            {
                bool rejectedAsPlayerSlot = !WildWindSessionFlow.TryPrepareExistingWorldLaunch(tempSessionSlotName, out string tempSessionPrepareError);
                report.Check(rejectedAsPlayerSlot,
                    rejectedAsPlayerSlot
                        ? "SessionFlow rejects temporary big test session saves outside the internal session loop."
                        : "SessionFlow accepted a temporary big test session save as a player slot: " + tempSessionPrepareError);
            }
            finally
            {
                RestoreSessionLoopPrefs(previousSelectedForTempSession, previousPendingForTempSession);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempSessionSlotPath))
                {
                    File.Delete(tempSessionSlotPath);
                }
            }
            catch (Exception exception)
            {
                report.Warn("Could not delete temporary big test session save slot: " + exception.Message);
            }
        }

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

        Vector3 originalFocusPosition = focus != null ? focus.position : Vector3.zero;
        Vector3 tickProbePosition = SelectFarSimulationProbePosition(originalFocusPosition);
        bool focusMoved = focus != null && (focus.position - tickProbePosition).sqrMagnitude > 0.001f;

        try
        {
            if (focus != null)
            {
                focus.position = tickProbePosition;
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
            report.Info("Последний tick: " + simulationTick.LastSummary + ", probe=" + FormatVector(tickProbePosition) + ".");
        }
        finally
        {
            if (focusMoved)
            {
                focus.position = originalFocusPosition;
                runtimeState.RefreshActiveBubble(originalFocusPosition, world.ActiveBubbleRadiusMeters);
                if (streamer != null)
                {
                    streamer.RefreshNow();
                }
            }
        }
    }

    private Vector3 SelectFarSimulationProbePosition(Vector3 originalPosition)
    {
        float probeAltitude = originalPosition.y > 1000f ? originalPosition.y : 2500f;
        Vector3[] candidates =
        {
            new Vector3(originalPosition.x, probeAltitude, originalPosition.z),
            new Vector3(0f, 2500f, -9000f),
            new Vector3(-9000f, 2500f, 0f),
            new Vector3(9000f, 2500f, -9000f),
            new Vector3(-18000f, 2500f, -12000f),
            new Vector3(18000f, 2500f, 18000f)
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (HasExtractableEntityOutsideActiveBubble(candidates[i]))
            {
                return candidates[i];
            }
        }

        return originalPosition;
    }

    private bool HasExtractableEntityOutsideActiveBubble(Vector3 probePosition)
    {
        if (world == null || runtimeState == null)
        {
            return false;
        }

        if (worldIndex != null)
        {
            worldIndex.EnsureBuilt(world);
        }

        float sqrRadius = world.ActiveBubbleRadiusMeters * world.ActiveBubbleRadiusMeters;
        for (int i = 0; i < runtimeState.Entities.Count; i++)
        {
            WorldRuntimeState.EntityRuntimeState state = runtimeState.Entities[i];
            if (state == null ||
                !IsFarSimulatedExtractableKind(state.kind) ||
                state.depleted ||
                state.remainingAmount <= 0.001f)
            {
                continue;
            }

            if (worldIndex != null &&
                worldIndex.TryGet(state.kind, state.id, out WorldEntityQueryResult record))
            {
                if (HorizontalSqrDistance(record.positionMeters, probePosition) > sqrRadius)
                {
                    return true;
                }
            }
            else if (!state.activeInBubble)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFarSimulatedExtractableKind(WorldEntityKind kind)
    {
        return kind == WorldEntityKind.CloudField ||
            kind == WorldEntityKind.ResourceField ||
            kind == WorldEntityKind.IcebergField;
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
        report.Check(controls.FlightTargetSpeedChangeMetersPerSecond > 0f, "Скорость изменения цели круиз-контроля положительная: " + controls.FlightTargetSpeedChangeMetersPerSecond.ToString("0.#") + " м/с за сек.");
        report.Check(controls.FlightTargetAltitudeChangeMetersPerSecond > 0f, "Скорость изменения целевой высоты положительная: " + controls.FlightTargetAltitudeChangeMetersPerSecond.ToString("0.#") + " м/с.");
        report.Check(controls.FlightTargetHeadingChangeDegreesPerSecond > 0f, "Скорость изменения целевого курса положительная: " + controls.FlightTargetHeadingChangeDegreesPerSecond.ToString("0.#") + " град/с.");
        report.Check(controls.FlightMaxReverseTargetSpeedMetersPerSecond >= 0f, "Лимит заднего хода для круиз-контроля не отрицательный: " + controls.FlightMaxReverseTargetSpeedMetersPerSecond.ToString("0.#") + " м/с.");

        ValidateWarshipsCameraContract(report);

        WildWindGameplayMenu gameplayMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        report.Check(gameplayMenu != null, gameplayMenu != null ? "Внутриигровое меню найдено в world-сессии." : "Внутриигровое меню не найдено.");
        WildWindGameplayHud hud = FindFirstObjectByType<WildWindGameplayHud>();
        report.Check(hud != null, hud != null ? "Внутриигровой HUD найден в world-сессии." : "Внутриигровой HUD не найден.");
    }

    private static void ValidateWarshipsCameraContract(BigTestReport report)
    {
        WorldDebugTravelController travelController = FindFirstObjectByType<WorldDebugTravelController>();
        report.Check(travelController != null,
            travelController != null ? "Flight camera controller is present for Warships-style aiming." : "Flight camera controller is missing.");
        if (travelController != null)
        {
            bool mouseLookEnabled = ReadPrivateBool(travelController, "warshipsFlightMouseLook", false);
            bool altReleasesCursor = ReadPrivateBool(travelController, "altReleasesCursor", false);
            float aimLookAheadMeters = ReadPrivateFloat(travelController, "flightAimLookAheadMeters", -1f);
            report.Check(mouseLookEnabled && altReleasesCursor && aimLookAheadMeters >= 50f,
                "Flight camera keeps Warships-style locked mouse look, Alt cursor release, and a forward aim point: "
                + "mouseLook=" + mouseLookEnabled
                + ", altRelease=" + altReleasesCursor
                + ", lookAhead=" + aimLookAheadMeters.ToString("0.#") + " m.");
        }

#if UNITY_EDITOR
        string cameraSource = ReadProjectText("Assets/Scripts/World/WorldDebugTravelController.cs");
        string shipSource = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        bool cameraLooksAhead = ContainsAllIgnoreCase(
            cameraSource,
            "LastCameraAimTarget",
            "GetCameraAimTarget",
            "TryGetManualGunCameraAimTarget",
            "flightAimLookAheadMeters");
        bool weaponDrawsAimRing = ContainsAllIgnoreCase(
            shipSource,
            "ResolveAimGuiPosition",
            "GameplayCursorLockedForMouseLook",
            "Screen.width * 0.5f",
            "WorldToScreenPoint",
            "TryReadMousePosition",
            "DrawAimRing");
        bool reticleRejectsScenePlanes = ContainsAllIgnoreCase(
            shipSource,
            "IsManualGunRaycastTarget",
            "DamageableShip",
            "Leviathan",
            "MiningRock");
        bool reticleUsesRangeSphere = ContainsAllIgnoreCase(
            shipSource,
            "ManualGunSphereAimSensitivityDegreesPerPixel",
            "manualGunSpherePitchDegrees",
            "ResolveManualGunSphereAimPoint",
            "ResolveCursorRayRangePoint",
            "center + fromCenter.normalized * range",
            "SmoothManualGunTargetPoint");
        bool reticleTargetsObjectsInsideSphere = ContainsAllIgnoreCase(
            shipSource,
            "TryRaycastCursorTarget(ray, maxRange",
            "maxRangeSqr",
            "hasHitTarget = true");
        bool verticalAimInverted = ContainsAllIgnoreCase(
            shipSource,
            "manualGunSpherePitchDegrees + aimDelta.y",
            "ManualGunSphereAimSensitivityDegreesPerPixel") &&
            cameraSource.Contains("cameraOrbitPitchDegrees + dragDelta.y");
        bool projectileTrailIsBrighter = ContainsAllIgnoreCase(
            shipSource,
            "GunProjectileTrailBrightnessMultiplier",
            "BuildGunProjectileTrailColor",
            "trail.startColor",
            "_EmissionColor");
        bool cameraUsesManualGunAim = ContainsAllIgnoreCase(
            cameraSource,
            "TryGetManualGunCameraAimTarget",
            "manualGunAimTarget") &&
            shipSource.Contains("TryGetManualGunCameraAimTarget");
        bool mouseLookScalesWithScopeFov = ContainsAllIgnoreCase(
            cameraSource,
            "GameplayMouseLookSensitivityScale",
            "UpdateGameplayMouseLookSensitivityScale",
            "GetFlightMouseLookSensitivityScale",
            "GetFieldOfViewSensitivityScale",
            "Mathf.Tan(Mathf.Clamp(currentVerticalFovDegrees",
            "GetOrbitSensitivityForTarget") &&
            ContainsAllIgnoreCase(
                shipSource,
                "GameplayMouseLookSensitivityScale",
                "sensitivityScale",
                "ManualGunSphereAimSensitivityDegreesPerPixel * sensitivityScale");
        bool wheelUsesWarshipsRoute = ContainsAllIgnoreCase(
            cameraSource,
            "flightCameraWheelRoute01",
            "GetFlightWheelCameraPosition",
            "FlightGunToStandardWheelUnits",
            "FlightStandardToHighWheelUnits",
            "FlightHighToScopeWheelUnits",
            "FlightShipStandardBackOffsetMeters",
            "ResolveFlightShipFlatForward",
            "ResolveFlightShipHighHeightMeters",
            "ResolveFlightShipLengthMeters",
            "ApplyFlightCameraWheelRoute",
            "ApplyFlightCameraWheelRouteDelta",
            "ApplyFlightScopeWheelDampedDelta",
            "GetRawFlightScopeWheelDelta",
            "GetFlightScopeStartWheelUnits",
            "GetFlightCameraScopeRatio",
            "GetFlightScopeVerticalFieldOfView",
            "FlightScopeReferenceRangeMeters = 20000f",
            "FlightScopeScreenWidthMetersAtReferenceRange = 1000f",
            "FlightScopeMinimumWheelEffectiveness = 0.2f");
        bool warshipsInspectorIsCompact = ContainsAllIgnoreCase(
            cameraSource,
            "HideInInspector",
            "CameraWheelEffectivenessMultiplier = 20f",
            "ApplyFlightCameraWheelRoute(scrollDelta * CameraWheelEffectivenessMultiplier)",
            "ApplyProgressiveZoom(scrollDelta * CameraWheelEffectivenessMultiplier)",
            "Mathf.Atan((screenWidth * 0.5f) / referenceRange)",
            "Mathf.Tan(horizontalRadians * 0.5f) / aspect",
            "Mathf.Exp(-reduction * scrollDelta / scopeLength)") &&
            !cameraSource.Contains("[Header(\"Warships Camera\")]") &&
            !cameraSource.Contains("wheelGunToShip") &&
            !cameraSource.Contains("wheelShipToHigh") &&
            !cameraSource.Contains("wheelHighToScope") &&
            !cameraSource.Contains("gunViewHeightMeters") &&
            !cameraSource.Contains("shipViewHeightMeters") &&
            !cameraSource.Contains("scopeFov") &&
            !cameraSource.Contains("[Header(\"Movement\")]") &&
            !cameraSource.Contains("[Header(\"Camera\")]") &&
            !cameraSource.Contains("[Header(\"Temporary Wheel Debug\")]");
        bool wheelGunViewStaysOverGun = cameraSource.Contains("gunAnchor + Vector3.up") &&
            !cameraSource.Contains("flightGunBackOffsetMeters");
        bool scopeUsesFovNotTargetDolly = cameraSource.Contains("return shipHighPosition;") &&
            !cameraSource.Contains("GetFlightTargetFocusCameraPosition");
        report.Check(cameraLooksAhead && cameraUsesManualGunAim && mouseLookScalesWithScopeFov && wheelUsesWarshipsRoute && warshipsInspectorIsCompact && wheelGunViewStaysOverGun && scopeUsesFovNotTargetDolly && weaponDrawsAimRing && reticleRejectsScenePlanes && reticleUsesRangeSphere && reticleTargetsObjectsInsideSphere && verticalAimInverted && projectileTrailIsBrighter,
            cameraLooksAhead && cameraUsesManualGunAim && mouseLookScalesWithScopeFov && wheelUsesWarshipsRoute && warshipsInspectorIsCompact && wheelGunViewStaysOverGun && scopeUsesFovNotTargetDolly && weaponDrawsAimRing && reticleRejectsScenePlanes && reticleUsesRangeSphere && reticleTargetsObjectsInsideSphere && verticalAimInverted && projectileTrailIsBrighter
                ? "Manual gunnery keeps the locked HUD cursor centered, hides the extra Warships camera sliders, scales camera and reticle mouse look by scope FOV, moves the wheel route at twentyfold effectiveness, damps only the final scope segment down to 20% wheel effectiveness, keeps the camera centered on the gun aim point, uses a Warships-style route from gun to ship to high view, applies FOV-only scope focus calibrated to 1000m screen width at 20km, inverts vertical sphere aim, draws bright projectile trails, and still lets targetable objects inside that sphere capture the cursor ray."
                : "Manual gunnery camera/reticle source contract is incomplete: cameraLooksAhead=" + cameraLooksAhead
                    + ", cameraUsesManualGunAim=" + cameraUsesManualGunAim
                    + ", mouseLookScalesWithScopeFov=" + mouseLookScalesWithScopeFov
                    + ", wheelUsesWarshipsRoute=" + wheelUsesWarshipsRoute
                    + ", warshipsInspectorIsCompact=" + warshipsInspectorIsCompact
                    + ", wheelGunViewStaysOverGun=" + wheelGunViewStaysOverGun
                    + ", scopeUsesFovNotTargetDolly=" + scopeUsesFovNotTargetDolly
                    + ", weaponDrawsAimRing=" + weaponDrawsAimRing
                    + ", reticleRejectsScenePlanes=" + reticleRejectsScenePlanes
                    + ", reticleUsesRangeSphere=" + reticleUsesRangeSphere
                    + ", reticleTargetsObjectsInsideSphere=" + reticleTargetsObjectsInsideSphere
                    + ", verticalAimInverted=" + verticalAimInverted
                    + ", projectileTrailIsBrighter=" + projectileTrailIsBrighter + ".");
#else
        report.Check(true, "Warships camera source contract is editor-only and skipped in player builds.");
#endif
    }

    private void ValidateShipWindAerodynamics(BigTestReport report)
    {
        report.Section("Корабль, ветер и лётная физика");
        ValidateActivePlayerShipVisual(report);
        ValidateBallisticFireControl(report);
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

            body.linearVelocity = new Vector3(14f, 0f, 0f);
            bool slipstreamBlockedBelowSpeed = !ship.TrySetClaudiumSlipstreamEnabled(true, out _);
            body.linearVelocity = new Vector3(16f, 0f, 0f);
            bool slipstreamEnabledAboveSpeed = ship.TrySetClaudiumSlipstreamEnabled(true, out _);
            ship.claudiumSlipstreamCharge01 = 1f;
            bool slipstreamFullEffect = Approximately(ship.ClaudiumSlipstreamDragMultiplier, 0.05f, 0.001f)
                && Approximately(ship.ClaudiumSlipstreamClaudiumMultiplier, 10f, 0.001f);
            body.linearVelocity = new Vector3(14f, 0f, 0f);
            bool slipstreamDisabledAfterSlowdown = TryInvokePrivateMethod(ship, "FixedUpdate", report)
                && !ship.claudiumSlipstreamEnabled;
            report.Check(slipstreamBlockedBelowSpeed
                && slipstreamEnabledAboveSpeed
                && slipstreamDisabledAfterSlowdown
                && slipstreamFullEffect,
                "Claudium slipstream activates only above 15 m/s, drops out below 15 m/s, then reaches x0.05 drag and x10 claudium consumption.");
            ship.TrySetClaudiumSlipstreamEnabled(false, out _);
            ship.claudiumSlipstreamCharge01 = 0f;

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

            float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            ship.thrustInput = 1f;
            ship.propellerPitch = 0f;
            ship.enginePowerLever = 0f;
            ship.engineResponseRate01PerSecond = 0.10f;
            if (TryInvokePrivateMethod(ship, "UpdateEngineThrottles", report)
                && TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedEngineStep = 0.10f * fixedDeltaTime;
                report.Check(Approximately(ship.propellerPitch, expectedEngineStep, 0.0002f)
                    && Approximately(ship.enginePowerLever, expectedEngineStep, 0.0002f),
                    "Двигатель догоняет ручку тяги с приёмистостью 10% максимума в секунду: pitch "
                    + ship.propellerPitch.ToString("0.0000")
                    + ", power "
                    + ship.enginePowerLever.ToString("0.0000") + ".");
            }

            ship.claudiumStock = 20f;
            ship.claudiumCurrentLiftN = 0f;
            ship.claudiumPowerDrawWatts = 0f;
            ship.claudiumPowerDrawKw = 0f;
            ship.claudiumLoopResponseRate01PerSecond = 0.10f;
            ship.enginePowerLever = 1f;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedLoopStepN = ship.claudiumMaxLiftKg * 9.81f * 0.10f * fixedDeltaTime;
                report.Check(Approximately(ship.claudiumCurrentLiftN, expectedLoopStepN, 0.05f),
                    "Клавдиевый контур меняет фактический подъём с приёмистостью 10% максимума в секунду: "
                    + ship.claudiumCurrentLiftN.ToString("0.###") + " Н.");
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
            ship.propellerPitch = 1f;
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

    private static void ValidateBallisticFireControl(BigTestReport report)
    {
        Vector3 origin = Vector3.zero;
        Vector3 target = new Vector3(1000f, 0f, 0f);
        Vector3 gravity = Physics.gravity;
        bool solvedDrop = BallisticFireControl.TrySolveLaunchVelocity(
            origin,
            target,
            Vector3.zero,
            300f,
            gravity,
            1500f,
            out Vector3 launchVelocity,
            out float flightTime,
            out Vector3 predicted);
        Vector3 impact = BallisticFireControl.EvaluatePosition(origin, launchVelocity, gravity, flightTime);
        float missMeters = Vector3.Distance(impact, predicted);
        report.Check(solvedDrop
            && launchVelocity.y > 0f
            && flightTime > target.magnitude / 300f
            && missMeters <= 1.5f,
            "Ballistic fire control elevates the gun to compensate shell drop: t "
            + flightTime.ToString("0.00")
            + " s, error "
            + missMeters.ToString("0.###")
            + " m.");

        float dragRetention = 0.58f;
        float dragPerMeter = BallisticFireControl.CalculateDragPerMeter(1500f, dragRetention);
        bool solvedDragDrop = BallisticFireControl.TrySolveLaunchVelocity(
            origin,
            target,
            Vector3.zero,
            300f,
            gravity,
            1500f,
            out Vector3 dragLaunchVelocity,
            out float dragFlightTime,
            out Vector3 dragPredicted,
            dragRetention);
        Vector3 dragImpact = BallisticFireControl.EvaluatePosition(origin, dragLaunchVelocity, gravity, dragFlightTime, dragPerMeter);
        float dragMissMeters = Vector3.Distance(dragImpact, dragPredicted);
        report.Check(solvedDragDrop
            && dragPerMeter > 0f
            && dragLaunchVelocity.y > launchVelocity.y
            && dragFlightTime > flightTime
            && dragMissMeters <= 5f,
            "Ballistic fire control solves drag-aware shell drop: t "
            + dragFlightTime.ToString("0.00")
            + " s, error "
            + dragMissMeters.ToString("0.###")
            + " m, drag "
            + dragPerMeter.ToString("0.000000")
            + ".");

        Vector3 movingTarget = new Vector3(900f, 40f, 0f);
        Vector3 targetVelocity = new Vector3(0f, 0f, 35f);
        bool solvedLead = BallisticFireControl.TrySolveLaunchVelocity(
            origin,
            movingTarget,
            targetVelocity,
            320f,
            gravity,
            1600f,
            out Vector3 leadVelocity,
            out float leadTime,
            out Vector3 leadPoint);
        Vector3 leadImpact = BallisticFireControl.EvaluatePosition(origin, leadVelocity, gravity, leadTime);
        report.Check(solvedLead
            && leadPoint.z > movingTarget.z + 1f
            && Vector3.Distance(leadImpact, leadPoint) <= 2f,
            "Automatic ballistic lead predicts a moving target point instead of aiming at current position: lead "
            + (leadPoint.z - movingTarget.z).ToString("0.##")
            + " m.");

        float testSpeed45 = 400f;
        Vector3 launch45 = new Vector3(testSpeed45 * 0.70710678f, testSpeed45 * 0.70710678f, 0f);
        float groundTime45 = -2f * launch45.y / gravity.y;
        Vector3 apex45 = BallisticFireControl.EvaluatePosition(origin, launch45, gravity, groundTime45 * 0.5f);
        Vector3 ground45 = BallisticFireControl.EvaluatePosition(origin, launch45, gravity, groundTime45);
        Vector3 range1500At45 = BallisticFireControl.EvaluatePosition(origin, launch45, gravity, 1500f / launch45.x);
        report.Check(apex45.y > 4000f
            && Mathf.Abs(ground45.y) <= 1.5f
            && ground45.x > 15000f
            && range1500At45.y > 1000f,
            "Projectile trajectory remains classic parabolic ballistics: 400 m/s at 45 degrees lands after "
            + groundTime45.ToString("0.0")
            + " s at "
            + ground45.x.ToString("0")
            + " m, so at 1500 m it is still rising at "
            + range1500At45.y.ToString("0")
            + " m.");
        float retainedHalfRange = BallisticFireControl.CalculateRangeVelocityRetention(750f, 1500f, 0.58f);
        float retainedMaxRange = BallisticFireControl.CalculateRangeVelocityRetention(1500f, 1500f, 0.58f);
        Vector3 oneSecondVelocity = BallisticFireControl.IntegrateVelocity(
            new Vector3(400f, 0f, 0f),
            gravity,
            1f,
            dragPerMeter);
        Vector3 dragRange1500At45 = BallisticFireControl.EvaluatePosition(
            origin,
            launch45,
            gravity,
            1500f / launch45.x,
            dragPerMeter);
        report.Check(retainedHalfRange > retainedMaxRange
            && Approximately(retainedMaxRange, 0.58f, 0.001f)
            && oneSecondVelocity.x < 390f
            && oneSecondVelocity.magnitude < 400f
            && dragRange1500At45.x < range1500At45.x - 100f
            && dragRange1500At45.y < range1500At45.y - 50f,
            "Air resistance bends projectile trajectories and bleeds speed: retention half "
            + retainedHalfRange.ToString("0.###")
            + ", max "
            + retainedMaxRange.ToString("0.###")
            + ", one-second speed "
            + oneSecondVelocity.magnitude.ToString("0.#")
            + " m/s, drag 45deg y "
            + dragRange1500At45.y.ToString("0")
            + ".");

        Vector2 spread = BallisticFireControl.CalculateSpreadRadii(40f, 12f, 750f, 1500f);
        report.Check(Approximately(spread.x, 20f, 0.001f) && Approximately(spread.y, 6f, 0.001f),
            "Gun dispersion ellipse scales with distance: horizontal "
            + spread.x.ToString("0.#")
            + " m, vertical "
            + spread.y.ToString("0.#")
            + " m at half range.");

        float nearPenetration = BallisticFireControl.CalculatePenetrationMultiplier(0f, 1500f, 0.5f, 320f, 320f);
        float farPenetration = BallisticFireControl.CalculatePenetrationMultiplier(1500f, 1500f, 0.5f, 180f, 320f);
        report.Check(nearPenetration > farPenetration
            && Approximately(nearPenetration, 1f, 0.001f)
            && farPenetration < 0.5f,
            "Armor-piercing penetration falls with range and retained velocity: near "
            + nearPenetration.ToString("0.###")
            + ", far "
            + farPenetration.ToString("0.###")
            + ".");

        GameObject shipObject = null;
        try
        {
            shipObject = new GameObject("Big Test Ballistic Ship");
            shipObject.AddComponent<Rigidbody>();
            ShipPhysics ship = shipObject.AddComponent<ShipPhysics>();
            ship.enabled = false;
            ShipGunGroup starterMainGroup = ship.shipGunGroups != null && ship.shipGunGroups.Count > 0
                ? ship.shipGunGroups[0]
                : null;
            report.Check(starterMainGroup != null
                && starterMainGroup.fireMode == ShipGunFireMode.Manual
                && Approximately(starterMainGroup.SecondsBetweenSalvos, 1f, 0.001f),
                "Starter main caliber reloads in 1 second: "
                + (starterMainGroup != null ? starterMainGroup.SecondsBetweenSalvos.ToString("0.###") : "missing")
                + " s.");
            ship.shipGunGroups = new List<ShipGunGroup>
            {
                new ShipGunGroup
                {
                    groupId = "main",
                    displayNameRu = "Serialized Old Main Gun",
                    fireMode = ShipGunFireMode.Manual,
                    enabled = true,
                    roundsPerMinute = 10f
                }
            };
            ship.manualGunGroupIndex = 0;
            bool serializedReloadRepaired = ship.BuildGunAimSolutionForPoint(null, new Vector3(80f, 0f, 0f), Vector3.zero, false, true, out _)
                && ship.shipGunGroups != null
                && ship.shipGunGroups.Count > 0
                && Approximately(ship.shipGunGroups[ship.manualGunGroupIndex].SecondsBetweenSalvos, 1f, 0.001f);
            report.Check(serializedReloadRepaired,
                "Serialized old main gun groups are forced back to 1 second reload at runtime.");
            ShipGunGroup group = new ShipGunGroup
            {
                groupId = "test",
                displayNameRu = "Test group",
                maxRangeMeters = 500f,
                muzzleVelocityMS = 200f,
                gravityScale = 0f,
                localMuzzleOffset = Vector3.zero,
                shell = new DamageShellPreset { velocityRetentionAtMaxRange = 1f }
            };

            bool clamped = ship.BuildGunAimSolutionForPoint(group, new Vector3(1000f, 0f, 0f), Vector3.zero, false, false, out BallisticAimSolution solution)
                && !solution.inRange
                && Approximately(solution.distanceFromShipCenter, 500f, 0.05f)
                && Approximately(solution.travelTimeSeconds, 2.5f, 0.05f);
            report.Check(clamped,
                "Gun group range is measured from ship center and clamps empty/out-of-range aim to max range: "
                + solution.distanceFromShipCenter.ToString("0.#")
                + " m.");

            ShipGunGroup dragAutoAimGroup = new ShipGunGroup
            {
                groupId = "drag_auto_aim_alignment",
                displayNameRu = "Drag Auto Aim Alignment Gun",
                maxRangeMeters = 1500f,
                muzzleVelocityMS = 300f,
                gravityScale = 1f,
                localMuzzleOffset = Vector3.zero,
                minElevationDegrees = -5f,
                maxElevationDegrees = 70f,
                shell = new DamageShellPreset
                {
                    displayNameRu = "Drag Alignment Shell",
                    velocityRetentionAtMaxRange = 0.58f
                }
            };
            Vector3 dragAimTarget = ship.transform.position + new Vector3(1000f, 0f, 0f);
            bool dragWeaponAimBuilt = ship.BuildGunAimSolutionForPoint(
                dragAutoAimGroup,
                dragAimTarget,
                Vector3.zero,
                false,
                true,
                out BallisticAimSolution dragWeaponAim);
            Vector3 directDragAimDirection = (dragAimTarget - dragWeaponAim.origin).sqrMagnitude > 0.001f
                ? (dragAimTarget - dragWeaponAim.origin).normalized
                : Vector3.forward;
            Vector3 simulatedDragProjectilePosition = SimulateDamageProjectileBallistics(
                dragWeaponAim.origin,
                dragWeaponAim.launchVelocity,
                dragAutoAimGroup.gravityScale,
                dragAutoAimGroup.maxRangeMeters,
                dragAutoAimGroup.shell.velocityRetentionAtMaxRange,
                dragWeaponAim.travelTimeSeconds,
                Mathf.Max(0.001f, Time.fixedDeltaTime),
                dragWeaponAim.predictedTargetPoint,
                out float dragProjectileClosestMeters);
            report.Check(dragWeaponAimBuilt
                && dragWeaponAim.valid
                && dragWeaponAim.dragPerMeter > 0f
                && dragWeaponAim.launchDirection.y > directDragAimDirection.y + 0.01f
                && dragProjectileClosestMeters <= 6f,
                "Weapon auto-elevation and real projectile integration agree with drag: aim elev "
                + (Mathf.Asin(Mathf.Clamp(dragWeaponAim.launchDirection.y, -1f, 1f)) * Mathf.Rad2Deg).ToString("0.##")
                + " deg, closest miss "
                + dragProjectileClosestMeters.ToString("0.###")
                + " m, final "
                + FormatVector(simulatedDragProjectilePosition)
                + ".");

            ship.shipGunGroups = new List<ShipGunGroup>
            {
                new ShipGunGroup
                {
                    groupId = "auto_only",
                    displayNameRu = "Auto-only legacy group",
                    fireMode = ShipGunFireMode.Automatic,
                    enabled = true
                }
            };
            ship.manualGunGroupIndex = 0;
            bool manualGroupRecovered = ship.BuildGunAimSolutionForPoint(null, new Vector3(120f, 0f, 40f), Vector3.zero, false, true, out BallisticAimSolution recoveredManualAim)
                && ship.shipGunGroups != null
                && ship.shipGunGroups.Count >= 2
                && ship.shipGunGroups[ship.manualGunGroupIndex] != null
                && ship.shipGunGroups[ship.manualGunGroupIndex].enabled
                && ship.shipGunGroups[ship.manualGunGroupIndex].fireMode == ShipGunFireMode.Manual
                && recoveredManualAim.maxRangeMeters > 0.001f;
            report.Check(manualGroupRecovered,
                "ShipPhysics repairs old runtime ships that have no manual main gun group before aiming or firing.");

            ship.weaponShotCostKg = 0f;
            ship.weaponShotAlarmRadiusMeters = 0f;
            GameObject testTurretObject = new GameObject("\u0422\u0443\u0440\u0435\u043b\u044c");
            testTurretObject.transform.SetParent(ship.transform, false);
            testTurretObject.transform.localPosition = new Vector3(0f, 2.5f, 6f);
            testTurretObject.transform.localRotation = Quaternion.identity;
            testTurretObject.transform.localScale = Vector3.one;

            GameObject testBarrelObject = new GameObject("\u0421\u0442\u0432\u043e\u043b");
            testBarrelObject.transform.SetParent(testTurretObject.transform, false);
            testBarrelObject.transform.localPosition = new Vector3(0f, 0.15f, 0.2f);
            testBarrelObject.transform.localRotation = Quaternion.identity;
            testBarrelObject.transform.localScale = Vector3.one;

            GameObject testMuzzleObject = new GameObject("Muzzle");
            testMuzzleObject.transform.SetParent(testBarrelObject.transform, false);
            testMuzzleObject.transform.localPosition = new Vector3(0f, 0.05f, 1.75f);
            testMuzzleObject.transform.localRotation = Quaternion.identity;
            testMuzzleObject.transform.localScale = Vector3.one;

            ShipGunGroup liveGroup = new ShipGunGroup
            {
                groupId = "runtime_test",
                displayNameRu = "Existing Turret Test Gun",
                maxRangeMeters = 500f,
                muzzleVelocityMS = 200f,
                gravityScale = 0f,
                roundsPerMinute = 600f,
                yawSpeedDegPerSecond = 20000f,
                elevationUpSpeedDegPerSecond = 20000f,
                elevationDownSpeedDegPerSecond = 20000f,
                minElevationDegrees = -10f,
                maxElevationDegrees = 70f,
                fireAlignmentToleranceDegrees = 2f,
                projectileTrailSeconds = 0f,
                localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
                shell = new DamageShellPreset
                {
                    displayNameRu = "Runtime Test Shell",
                    damagePoints = 1f,
                    projectileColor = Color.cyan
                }
            };
            liveGroup.muzzle = testTurretObject.transform;
            liveGroup.yawPivot = testTurretObject.transform;
            liveGroup.pitchPivot = testTurretObject.transform;
            int projectilesBefore = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None).Length;
            Vector3 runtimeTarget = ship.transform.position + new Vector3(120f, 6f, 60f);
            bool fired = ship.TryFireGunGroupAtPointForTests(liveGroup, runtimeTarget, Vector3.zero, out string fireReason);
            int projectilesAfter = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None).Length;
            Vector3 muzzleToTarget = runtimeTarget - (liveGroup.muzzle != null ? liveGroup.muzzle.position : ship.transform.position);
            bool mountAimed = liveGroup.muzzle != null
                && liveGroup.pitchPivot != null
                && muzzleToTarget.sqrMagnitude > 0.001f
                && Vector3.Dot(liveGroup.pitchPivot.forward, muzzleToTarget.normalized) > 0.95f;
            DamageProjectile spawnedGunProjectile = null;
            DamageProjectile[] spawnedProjectiles = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < spawnedProjectiles.Length; i++)
            {
                DamageProjectile projectile = spawnedProjectiles[i];
                if (projectile != null && projectile.name.Contains("Existing Turret Test Gun Projectile"))
                {
                    spawnedGunProjectile = projectile;
                    break;
                }
            }

            Rigidbody spawnedProjectileBody = spawnedGunProjectile != null ? spawnedGunProjectile.GetComponent<Rigidbody>() : null;
            Vector3 spawnedVelocity = spawnedProjectileBody != null ? spawnedProjectileBody.linearVelocity : Vector3.zero;
            bool projectileLeavesMuzzle = spawnedGunProjectile != null
                && liveGroup.muzzle != null
                && Vector3.Distance(spawnedGunProjectile.transform.position, liveGroup.muzzle.position) <= 0.05f
                && spawnedVelocity.sqrMagnitude > 0.001f
                && Vector3.Dot(spawnedVelocity.normalized, liveGroup.muzzle.forward.normalized) > 0.995f;
            report.Check(fired
                && projectilesAfter > projectilesBefore
                && liveGroup.muzzle != null
                && liveGroup.yawPivot != null
                && liveGroup.pitchPivot != null
                && mountAimed
                && !ContainsChildNamed(ship.transform, "RuntimeGunMounts"),
                "Existing gun hierarchy is bound, rotates toward the ballistic aim, and firing creates a visible projectile without runtime fallback: "
                + fireReason);
            report.Check(liveGroup.muzzle == testMuzzleObject.transform,
                "Gun binding overrides stale serialized turret muzzle references with the barrel child Muzzle.");
            report.Check(projectileLeavesMuzzle,
                "Gun projectile spawns at the bound Muzzle and leaves along the visible barrel direction.");

            ShipGunGroup slowTraverseGroup = new ShipGunGroup
            {
                groupId = "slow_traverse_test",
                displayNameRu = "Slow Traverse Test Gun",
                maxRangeMeters = 500f,
                muzzleVelocityMS = 200f,
                gravityScale = 0f,
                roundsPerMinute = 600f,
                yawSpeedDegPerSecond = 5f,
                elevationUpSpeedDegPerSecond = 5f,
                elevationDownSpeedDegPerSecond = 5f,
                minElevationDegrees = -10f,
                maxElevationDegrees = 70f,
                fireAlignmentToleranceDegrees = 1f,
                projectileTrailSeconds = 0f,
                localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
                shell = new DamageShellPreset { displayNameRu = "Slow Traverse Test Shell", damagePoints = 1f }
            };
            int projectilesBeforeSlowShot = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None).Length;
            bool slowShotFired = ship.TryFireGunGroupAtPointForTests(
                slowTraverseGroup,
                ship.transform.position + new Vector3(120f, 0f, 0f),
                Vector3.zero,
                out string slowReason);
            int projectilesAfterSlowShot = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None).Length;
            report.Check(!slowShotFired
                && projectilesAfterSlowShot == projectilesBeforeSlowShot
                && slowReason.Contains("доворачивается"),
                "Gun traverse speed gates firing until the turret has rotated onto target: " + slowReason);

            ShipGunGroup elevationLimitGroup = new ShipGunGroup
            {
                groupId = "elevation_limit_test",
                displayNameRu = "Elevation Limit Test Gun",
                maxRangeMeters = 500f,
                muzzleVelocityMS = 200f,
                gravityScale = 0f,
                roundsPerMinute = 600f,
                yawSpeedDegPerSecond = 20000f,
                elevationUpSpeedDegPerSecond = 20000f,
                elevationDownSpeedDegPerSecond = 20000f,
                minElevationDegrees = -5f,
                maxElevationDegrees = 10f,
                fireAlignmentToleranceDegrees = 1f,
                projectileTrailSeconds = 0f,
                localMuzzleOffset = new Vector3(0f, 2.7f, 7.85f),
                shell = new DamageShellPreset { displayNameRu = "Elevation Limit Test Shell", damagePoints = 1f }
            };
            int projectilesBeforeLimitShot = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None).Length;
            bool limitShotFired = ship.TryFireGunGroupAtPointForTests(
                elevationLimitGroup,
                ship.transform.position + new Vector3(40f, 120f, 40f),
                Vector3.zero,
                out string elevationReason);
            int projectilesAfterLimitShot = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None).Length;
            report.Check(!limitShotFired
                && projectilesAfterLimitShot == projectilesBeforeLimitShot
                && elevationReason.Contains("вне углов"),
                "Gun elevation limits block firing outside the allowed barrel arc: " + elevationReason);

            DamageProjectile[] projectiles = FindObjectsByType<DamageProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                DamageProjectile projectile = projectiles[i];
                if (projectile != null &&
                    (projectile.name.Contains("Existing Turret Test Gun Projectile") ||
                     projectile.name.Contains("Slow Traverse Test Gun Projectile") ||
                     projectile.name.Contains("Elevation Limit Test Gun Projectile")))
                {
                    Destroy(projectile.gameObject);
                }
            }
        }
        finally
        {
            if (shipObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(shipObject);
                }
                else
                {
                    DestroyImmediate(shipObject);
                }
            }
        }

        report.Check(DamageProjectile.MaxActiveProjectiles >= 300,
            "Projectile pool cap allows 200-300 simultaneous visible shells: "
            + DamageProjectile.MaxActiveProjectiles.ToString()
            + ".");
    }

    private void ValidateActivePlayerShipVisual(BigTestReport report)
    {
        ShipPhysics activeShip = metaGameState != null && metaGameState.shipLoader != null
            ? metaGameState.shipLoader.targetShip
            : null;
        if (activeShip == null)
        {
            activeShip = FindFirstObjectByType<ShipPhysics>();
        }

        report.Check(activeShip != null,
            activeShip != null
                ? "Active player ShipPhysics is present for visual validation: " + activeShip.name + "."
                : "Active player ShipPhysics is missing, so the world can fly as empty focus.");
        if (activeShip == null)
        {
            return;
        }

        MeshFilter[] meshFilters = activeShip.GetComponentsInChildren<MeshFilter>(true);
        MeshRenderer[] renderers = activeShip.GetComponentsInChildren<MeshRenderer>(true);
        int usableMeshCount = 0;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter filter = meshFilters[i];
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0)
            {
                usableMeshCount++;
            }
        }

        List<MeshRenderer> visibleRenderers = new List<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                visibleRenderers.Add(renderer);
            }
        }

        bool hasFallbackVisual = ContainsChildNamed(activeShip.transform, "Session Balloon") ||
            ContainsChildNamed(activeShip.transform, "Session Cabin") ||
            ContainsChildNamed(activeShip.transform, "Emergency Starter Hull Fallback") ||
            activeShip.name == "Player Session Ship" ||
            activeShip.name == "Player Ship Proxy";
        report.Check(!hasFallbackVisual, "Active ship is not a session fallback visual.");
        report.Check(usableMeshCount > 0, "Active ship has " + usableMeshCount + " usable mesh filter(s).");
        report.Check(visibleRenderers.Count > 0, "Active ship has " + visibleRenderers.Count + " active mesh renderer(s).");

        bool hasBounds = TryFindVisibleStarterHullMeshSize(meshFilters, out Vector3 size);
        bool boatSized = hasBounds && IsFinite(size) && size.x >= 5f && size.x <= 7f && size.y >= 2.5f && size.y <= 4.25f && size.z >= 18f && size.z <= 22f;
        report.Check(boatSized,
            boatSized
                ? "Active ship visible mesh size matches the 20m starter hull: " + FormatVector(size) + "."
                : "Active ship visible mesh size is empty or not the 20m starter hull: " + FormatVector(size) + ".");
    }

    private void ValidateExtractionMechanicsAndMetaGame(BigTestReport report)
    {
        report.Section("Extraction mechanics and meta game");

        WildWindMechanicsCatalog extraction = WildWindExtractionMechanics.CreateMinimalCatalog();
        report.Check(extraction != null, "Extraction mechanics catalog is available.");
        report.Check(extraction.ores.Count >= 5
            && extraction.ores.ContainsKey("ferron")
            && extraction.ores.ContainsKey("coal")
            && extraction.ores.ContainsKey("ichorite")
            && extraction.ores.ContainsKey("claudite")
            && extraction.ores.ContainsKey("sublikat"),
            "Minimal ore content covers useful rock, coal, ichor, claudium and sublikats.");
        report.Check(extraction.highModules.Contains("crusher")
            && extraction.highModules.Contains("advanced_ichor_crusher")
            && extraction.highModules.Contains("vibro_ram")
            && extraction.highModules.Contains("flamethrower")
            && extraction.highModules.Contains("harpoon")
            && extraction.highModules.Contains("cannon"),
            "High modules cover crusher, advanced crusher, vibro-ram, flamethrower, harpoon and guns.");
        report.Check(extraction.midModules.Contains("active_ram_shield")
            && extraction.midModules.Contains("crane")
            && extraction.midModules.Contains("hacker")
            && extraction.midModules.Contains("heater")
            && extraction.midModules.Contains("cooler"),
            "Mid modules cover active ram protection, crane, hacker and temperature control.");
        report.Check(extraction.lowModules.Contains("passive_ram_plating")
            && extraction.lowModules.Contains("thermal_insulation")
            && extraction.lowModules.Contains("ichor_chemseal")
            && extraction.lowModules.Contains("passive_belly_scanner"),
            "Low modules cover passive ram plating, insulation, ichor protection and passive scanning.");
        report.Check(extraction.drones.Contains("catcher")
            && extraction.drones.Contains("grabber")
            && extraction.drones.Contains("burner")
            && extraction.drones.Contains("tug")
            && extraction.drones.Contains("disarmer")
            && extraction.drones.Contains("opener")
            && extraction.drones.Contains("syringe")
            && extraction.drones.Contains("salvager"),
            "Drone content covers catcher, grabber, burner, tug, disarmer, opener, syringe and salvager.");
        report.Check(extraction.scanners["directional_high_scanner"].pattern == ScanPattern.Directional
            && extraction.scanners["omni_mid_scanner"].pattern == ScanPattern.Omnidirectional
            && extraction.scanners["passive_belly_scanner"].pattern == ScanPattern.Passive,
            "Scanner content has High directional, Mid omnidirectional and Low passive profiles.");

        OreMechanicsProfile ferron = extraction.ores["ferron"];
        ExtractionBoulderState boulder = WildWindExtractionMechanics.CreateBoulder("test_ferron", ferron, 16f);
        report.Check(boulder.ore == ferron && boulder.ore.usefulFraction > 0f && boulder.ore.usefulFraction < 1f,
            "A boulder carries exactly one ore profile and still contains waste rock.");
        ScanResult weakScan = WildWindExtractionMechanics.ApplyScanExposure(extraction.scanners["passive_belly_scanner"], boulder, 5f, 500f, 0.25f, true);
        ScanResult strongScan = WildWindExtractionMechanics.ApplyScanExposure(extraction.scanners["directional_high_scanner"], boulder, 8f, 250f, 0.9f, true);
        report.Check(strongScan.exposureAdded > weakScan.exposureAdded, "Directional High scanner adds more exposure to boulders than a passive Low scanner.");
        ExtractionBoulderState missedCone = WildWindExtractionMechanics.CreateBoulder("missed_cone", ferron, 16f);
        ScanResult missedScan = WildWindExtractionMechanics.ApplyScanExposure(extraction.scanners["directional_high_scanner"], missedCone, 8f, 250f, 0.9f, false);
        report.Check(missedScan.exposureAdded <= 0.001f, "Directional scanner adds no exposure when the target is outside the cone.");
        ExtractionBoulderState giant = WildWindExtractionMechanics.CreateBoulder("giant", ferron, 220f);
        report.Check(giant.exposureRequired > boulder.exposureRequired * 10f, "Giant boulders require much more exposure than starter rocks.");

        ExtractionChunkState chunk = WildWindExtractionMechanics.ShedChunk(boulder, 180f, 24f);
        report.Check(chunk != null && chunk.oreItemId == ferron.oreItemId && Approximately(chunk.usefulFraction, ferron.usefulFraction, 0.001f),
            "Falling chunks inherit the parent ore id and useful/waste ratio.");
        ExtractionShipProfile protectedCrusher = new ExtractionShipProfile { hasCrusher = true, ramShieldActive = true, passiveRamPlating = true, cargoCapacityKg = 1000f, hullHealth = 1000f };
        ChunkCatchResult catchResult = WildWindExtractionMechanics.ResolveChunkShipContact(chunk, protectedCrusher, true);
        report.Check(catchResult.chunkDestroyed && catchResult.oreCollectedKg == Mathf.Floor(180f * ferron.usefulFraction),
            "Crusher keeps only useful ore, discards waste and destroys the incoming chunk.");
        report.Check(catchResult.finalRamDamage < catchResult.rawRamDamage
            && Approximately(catchResult.finalRamDamage, catchResult.rawRamDamage * 0.4f, 0.01f),
            "Active 50% ram shield and passive 20% plating combine with diminishing returns into 60% total reduction.");
        ExtractionChunkState overflowChunk = WildWindExtractionMechanics.ShedChunk(boulder, 300f, 18f);
        ExtractionShipProfile fullCrusher = new ExtractionShipProfile { hasCrusher = true, cargoCapacityKg = 20f, cargoUsedKg = 20f, hullHealth = 1000f };
        ChunkCatchResult overflow = WildWindExtractionMechanics.ResolveChunkShipContact(overflowChunk, fullCrusher, true);
        report.Check(overflow.cargoOverflow && overflow.chunkDestroyed && overflow.oreCollectedKg <= 0.001f,
            "Overflowed crusher still destroys the chunk after impact, without adding impossible cargo.");
        ExtractionChunkState noCrusherChunk = WildWindExtractionMechanics.ShedChunk(boulder, 120f, 22f);
        ExtractionShipProfile transport = new ExtractionShipProfile { hasCrusher = false, cargoCapacityKg = 1000f, hullHealth = 1000f };
        ChunkCatchResult transportHit = WildWindExtractionMechanics.ResolveChunkShipContact(noCrusherChunk, transport, false);
        report.Check(transportHit.oreCollectedKg <= 0.001f && transportHit.finalRamDamage > 0f && transportHit.chunkDestroyed,
            "A transport without crusher receives ram damage and destroys the chunk but collects no ore.");
        ExtractionChunkState shotChunk = WildWindExtractionMechanics.ShedChunk(boulder, 90f, 18f);
        report.Check(WildWindExtractionMechanics.ShootChunkToDust(shotChunk) && shotChunk.destroyed,
            "A gun hit on a falling chunk breaks it into dust with no collection.");

        ProjectileProfile apShot = new ProjectileProfile { delivery = ShellDeliveryMode.ArmorPiercing, element = ExtractionDamageElement.Physical, damage = 160f, penetration = 1.2f };
        BoulderDamageResult shotDamage = WildWindExtractionMechanics.ApplyProjectileDamage(boulder, apShot, true);
        report.Check(shotDamage.healthDamage > 0f && shotDamage.chunks > 0 && boulder.health < boulder.maxHealth,
            "Normal gunfire damages a naked boulder and accelerates shedding.");
        ExtractionBoulderState iceBoulder = WildWindExtractionMechanics.CreateBoulder("ice", ferron, 14f, ice: true);
        BoulderDamageResult iceShot = WildWindExtractionMechanics.ApplyProjectileDamage(iceBoulder, apShot, true);
        report.Check(iceShot.chunks == 0 && iceBoulder.iceCrust, "Ice crust blocks natural shedding until it is melted.");

        ExtractionBoulderState armoredAp = WildWindExtractionMechanics.CreateBoulder("armored_ap", ferron, 14f, armored: true);
        float crustBeforeAp = armoredAp.crustHealth;
        BoulderDamageResult apCrust = WildWindExtractionMechanics.ApplyProjectileDamage(armoredAp, apShot, true);
        report.Check(apCrust.crustDamage > 0f && armoredAp.health == armoredAp.maxHealth && armoredAp.crustHealth < crustBeforeAp,
            "Armored crust absorbs hits as a separate health bar before ore can shed.");
        ExtractionBoulderState armoredHe = WildWindExtractionMechanics.CreateBoulder("armored_he", ferron, 14f, armored: true);
        ProjectileProfile heShot = new ProjectileProfile { delivery = ShellDeliveryMode.HighExplosive, element = ExtractionDamageElement.Physical, damage = 160f, penetration = 1.2f };
        float heCrustDamage = WildWindExtractionMechanics.ApplyProjectileDamage(armoredHe, heShot, true).crustDamage;
        report.Check(apCrust.crustDamage > heCrustDamage, "Armor-piercing shells damage armored crust more effectively than HE shells.");
        ExtractionBoulderState armoredElectric = WildWindExtractionMechanics.CreateBoulder("armored_electric", ferron, 14f, armored: true);
        ProjectileProfile electricShot = new ProjectileProfile { delivery = ShellDeliveryMode.ArmorPiercing, element = ExtractionDamageElement.Electric, damage = 160f, penetration = 1.2f };
        float electricCrustDamage = WildWindExtractionMechanics.ApplyProjectileDamage(armoredElectric, electricShot, true).crustDamage;
        report.Check(electricCrustDamage < heCrustDamage, "Armored crust has high electric resistance.");
        ExtractionBoulderState normalRamCrust = WildWindExtractionMechanics.CreateBoulder("normal_ram_crust", ferron, 14f, armored: true);
        ExtractionBoulderState vibroRamCrust = WildWindExtractionMechanics.CreateBoulder("vibro_ram_crust", ferron, 14f, armored: true);
        float normalRamDamage = WildWindExtractionMechanics.ApplyRamToBoulder(normalRamCrust, 9000f, 12f, false).crustDamage;
        float vibroRamDamage = WildWindExtractionMechanics.ApplyRamToBoulder(vibroRamCrust, 9000f, 12f, true).crustDamage;
        report.Check(vibroRamDamage > normalRamDamage * 15f, "Active vibro-ram doubles ram damage and adds a heavy crust multiplier.");
        ExtractionBoulderState lowSpeedPush = WildWindExtractionMechanics.CreateBoulder("push", ferron, 14f);
        BoulderDamageResult pushResult = WildWindExtractionMechanics.ApplyRamToBoulder(lowSpeedPush, 9000f, 1.5f, false);
        report.Check(pushResult.pushedWithoutDamage && lowSpeedPush.health == lowSpeedPush.maxHealth,
            "Low-speed ram pushes a boulder without damage.");
        ExtractionBoulderState impossibleIchorCrust = WildWindExtractionMechanics.CreateBoulder("no_ichor_crust", extraction.ores["ichorite"], 14f, armored: true, ichor: true);
        report.Check(impossibleIchorCrust.ichor && !impossibleIchorCrust.armoredCrust,
            "Ichor boulders normalize away armored crust, matching the no ichor+crust rule.");

        ExtractionBoulderState explosive = WildWindExtractionMechanics.CreateBoulder("boom", ferron, 14f, explosive: true);
        BoulderDamageResult explosion = WildWindExtractionMechanics.ApplyProjectileDamage(explosive, apShot, true);
        report.Check(explosion.exploded && explosive.IsDestroyed && explosion.chunks >= 8 && explosion.threatAdded >= 80f,
            "Explosive boulder direct hit destroys the parent, sprays chunks and raises threat.");
        GasCloudMechanicsState electricCloud = new GasCloudMechanicsState { id = "electric", electric = true, electricPulseThreshold = 10f };
        ExtractionBoulderState electricBoom = WildWindExtractionMechanics.CreateBoulder("electric_boom", ferron, 12f, explosive: true);
        report.Check(WildWindExtractionMechanics.AccumulateElectricPulse(electricCloud, electricBoom, 3f)
                && electricBoom.IsDestroyed,
            "Electric cloud accumulates charge and detonates an explosive boulder on pulse.");
        ExtractionBoulderState chainA = WildWindExtractionMechanics.CreateBoulder("chain_a", ferron, 10f, explosive: true);
        ExtractionBoulderState chainB = WildWindExtractionMechanics.CreateBoulder("chain_b", ferron, 10f, explosive: true);
        bool chainStarted = WildWindExtractionMechanics.ApplyProjectileDamage(chainA, apShot, true).exploded;
        bool chainContinued = WildWindExtractionMechanics.ApplyProjectileDamage(chainB, apShot, true).exploded;
        report.Check(chainStarted && chainContinued, "Explosive chunks can be represented as follow-up direct hits, enabling cascade explosions.");
        ExtractionBoulderState disarmedByFire = WildWindExtractionMechanics.CreateBoulder("fire_disarm", extraction.ores["coal"], 12f, explosive: true, ice: true);
        ThermalResult disarmHeat = WildWindExtractionMechanics.ApplyHeat(disarmedByFire, disarmedByFire.MassKg * 100f, 20f, true);
        report.Check(disarmHeat.explosiveDisarmed && !disarmedByFire.explosiveGas && disarmHeat.iceMelted,
            "Flamethrower melts ice and burns explosive gas out instead of detonating it.");

        ExtractionBoulderState smallHeat = WildWindExtractionMechanics.CreateBoulder("small_heat", ferron, 5f);
        ExtractionBoulderState largeHeat = WildWindExtractionMechanics.CreateBoulder("large_heat", ferron, 30f);
        ThermalResult smallThermal = WildWindExtractionMechanics.ApplyHeat(smallHeat, 500000f, 20f, true);
        ThermalResult largeThermal = WildWindExtractionMechanics.ApplyHeat(largeHeat, 500000f, 20f, true);
        report.Check(smallThermal.temperatureAfterC - smallThermal.temperatureBeforeC > largeThermal.temperatureAfterC - largeThermal.temperatureBeforeC,
            "The same flamethrower energy heats a small boulder much faster than a large one.");
        ExtractionBoulderState overheatTarget = WildWindExtractionMechanics.CreateBoulder("overheat", ferron, 12f);
        ExtractionBoulderState coldTarget = WildWindExtractionMechanics.CreateBoulder("cold_damage", ferron, 12f);
        overheatTarget.temperatureC = WildWindExtractionMechanics.BoulderOverheatThresholdC + 5f;
        float hotDamage = WildWindExtractionMechanics.ApplyProjectileDamage(overheatTarget, apShot, true).healthDamage;
        float coldDamage = WildWindExtractionMechanics.ApplyProjectileDamage(coldTarget, apShot, true).healthDamage;
        report.Check(hotDamage > coldDamage * 1.9f, "Overheated boulders take doubled projectile damage.");
        ExtractionBoulderState coalBoulder = WildWindExtractionMechanics.CreateBoulder("coal_heat", extraction.ores["coal"], 8f);
        ThermalResult coalBurn = WildWindExtractionMechanics.ApplyHeat(coalBoulder, coalBoulder.MassKg * 500f, 20f, true);
        report.Check(coalBurn.oreDestroyed && coalBoulder.oreDestroyedByHeat, "Coal ore burns into waste when heated past its threshold.");
        WildWindExtractionMechanics.DriftTemperatureTowardEnvironment(coalBoulder, -20f, 120f);
        report.Check(coalBoulder.temperatureC < 0f && coalBoulder.iceCrust && !coalBoulder.canShed,
            "Cold environment pulls bodies toward ambient temperature and can re-ice boulders.");

        GasCloudMechanicsState hotIchorCloud = new GasCloudMechanicsState
        {
            id = "hot_ichor",
            center = Vector3.zero,
            radiusMeters = 100f,
            hot = true,
            ichor = true,
            electric = true,
            temperatureOffsetC = 40f,
            opacity01 = 0.35f,
            remainingGasUnits = 1000f,
            harvestUnitsPerSecondAtCenter = 10f
        };
        CloudHarvestResult centerHarvest = WildWindExtractionMechanics.HarvestCloud(hotIchorCloud, Vector3.zero, 10f, 1f);
        CloudHarvestResult edgeHarvest = WildWindExtractionMechanics.HarvestCloud(hotIchorCloud, new Vector3(95f, 0f, 0f), 10f, 1f);
        report.Check(centerHarvest.harvestedUnits > edgeHarvest.harvestedUnits, "Gas harvesting is strongest near cloud center and weak at the edge.");
        report.Check(centerHarvest.thermalDamage > 0f && centerHarvest.ichorDamage > 0f && centerHarvest.electricDamage > 0f,
            "Dangerous clouds can apply thermal, ichor and electric damage.");
        float cloudVolumeBeforeBurn = hotIchorCloud.remainingGasUnits;
        report.Check(WildWindExtractionMechanics.BurnCloud(hotIchorCloud, 3f)
            && hotIchorCloud.remainingGasUnits < cloudVolumeBeforeBurn,
            "Flamethrower burns cloud volume away.");
        EnvironmentRegionProfile coldRegion = new EnvironmentRegionProfile { ambientTemperatureC = -20f, fogOpacity01 = 0.2f };
        EnvironmentSample cloudEnvironment = WildWindExtractionMechanics.SampleEnvironment(
            coldRegion,
            Vector3.zero,
            new List<GasCloudMechanicsState> { hotIchorCloud },
            null);
        report.Check(cloudEnvironment.temperatureC > coldRegion.ambientTemperatureC,
            "Cloud temperature adds on top of regional ambient temperature.");
        report.Check(cloudEnvironment.visibility01 < 0.8f,
            "Regional fog and cloud opacity reduce scanner/aim visibility.");
        EnvironmentSample altitudeLow = WildWindExtractionMechanics.SampleEnvironment(coldRegion, new Vector3(0f, 1000f, 0f), null, null);
        EnvironmentSample altitudeHigh = WildWindExtractionMechanics.SampleEnvironment(coldRegion, new Vector3(0f, 100000f, 0f), null, null);
        report.Check(altitudeLow.airDensity > altitudeHigh.airDensity && altitudeHigh.claudiumLiftFactor <= 0.001f,
            "Air density falls with altitude and claudium lift reaches zero near the 100 km ceiling.");
        EnvironmentSample stormFloor = WildWindExtractionMechanics.SampleEnvironment(coldRegion, Vector3.zero, null, null);
        EnvironmentSample aboveStorm = WildWindExtractionMechanics.SampleEnvironment(coldRegion, new Vector3(0f, 1200f, 0f), null, null);
        report.Check(stormFloor.stormDamagePerSecond > 900f && aboveStorm.stormDamagePerSecond <= 0.001f,
            "Storm layer is lethal near zero altitude and stops dealing damage above the storm top.");
        EnvironmentSample fieldSample = WildWindExtractionMechanics.SampleEnvironment(
            coldRegion,
            Vector3.zero,
            null,
            new List<ClaudiumFieldSource>
            {
                new ClaudiumFieldSource { center = Vector3.zero, radiusMeters = 100f, strength = 2f },
                new ClaudiumFieldSource { center = Vector3.zero, radiusMeters = 100f, strength = 3f }
            });
        report.Check(Approximately(fieldSample.claudiumFieldStrength, 5f, 0.001f), "Claudium fields stack additively.");
        ExtractionBoulderState claudiumRock = WildWindExtractionMechanics.CreateBoulder("claudium", extraction.ores["claudite"], 25f);
        report.Check(claudiumRock.ClaudiumFieldRadiusMeters > claudiumRock.radiusMeters * 4f,
            "Claudium ore creates a field whose radius grows faster than raw boulder radius.");

        ExtractionChunkState ichorChunk = new ExtractionChunkState { oreItemId = "ichor", massKg = 100f, usefulFraction = 0.4f, fallSpeedMS = 20f, ichorAcid = true };
        DroneTaskResult catcher = WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Catcher, null, ichorChunk, DroneIchorProtection.Basic);
        report.Check(catcher.success && catcher.playerAvoidedRamDamage && catcher.oreDeliveredKg > 0f && ichorChunk.destroyed,
            "Catcher drone brings a falling chunk to the ship without player ram damage.");
        ExtractionBoulderState armoredGrab = WildWindExtractionMechanics.CreateBoulder("armored_grab", ferron, 12f, armored: true);
        report.Check(!WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Grabber, armoredGrab, null, DroneIchorProtection.Basic).success,
            "Grabber drone cannot take ore directly from armored crust.");
        DroneTaskResult opener = WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Opener, armoredGrab, null, DroneIchorProtection.Basic);
        report.Check(opener.success && opener.targetDamage > 0f && armoredGrab.crustHealth < armoredGrab.crustMaxHealth,
            "Opener drone drills armored crust down.");
        ExtractionBoulderState droneExplosive = WildWindExtractionMechanics.CreateBoulder("drone_disarm", ferron, 12f, explosive: true);
        DroneTaskResult disarmer = WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Disarmer, droneExplosive, null, DroneIchorProtection.Basic);
        report.Check(disarmer.success && !droneExplosive.explosiveGas && disarmer.propertyRemoved,
            "Disarmer drone vents explosive gas safely.");
        ExtractionBoulderState burnerTarget = WildWindExtractionMechanics.CreateBoulder("burner", extraction.ores["ichorite"], 8f, explosive: true, ice: true, ichor: true);
        DroneTaskResult burner = WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Burner, burnerTarget, null, DroneIchorProtection.Basic);
        report.Check(burner.success && burner.propertyRemoved && !burnerTarget.explosiveGas && !burnerTarget.ichor,
            "Burner drone can remove ice/gas/ichor properties by controlled heat.");
        ExtractionBoulderState syringeTarget = WildWindExtractionMechanics.CreateBoulder("syringe", extraction.ores["ichorite"], 8f, ichor: true);
        DroneTaskResult syringe = WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Syringe, syringeTarget, null, DroneIchorProtection.Immune);
        report.Check(syringe.success && syringe.ichorDeliveredKg > 0f && syringe.droneDamageTaken <= 0.001f && !syringeTarget.ichor,
            "Syringe drone is ichor-immune, extracts ichor and brings it home.");
        report.Check(Approximately(WildWindExtractionMechanics.GetDroneIchorDamageMultiplier(DroneIchorProtection.None), 5f, 0.001f)
            && Approximately(WildWindExtractionMechanics.GetDroneIchorDamageMultiplier(DroneIchorProtection.Basic), 1f, 0.001f)
            && Approximately(WildWindExtractionMechanics.GetDroneIchorDamageMultiplier(DroneIchorProtection.Advanced), 0.5f, 0.001f)
            && Approximately(WildWindExtractionMechanics.GetDroneIchorDamageMultiplier(DroneIchorProtection.Immune), 0f, 0.001f),
            "Drone ichor protection supports 500%, 100%, 50% and immune damage profiles.");
        report.Check(WildWindExtractionMechanics.ResolveDroneTask(MiningDroneKind.Tug, boulder, null, DroneIchorProtection.Basic).success,
            "Tug drone can attach to and move a boulder as a non-damaging task.");

        ExtractionBoulderState relicHighHealth = WildWindExtractionMechanics.CreateBoulder("relic_high", ferron, 10f, relic: true);
        RelicExtractionResult highRelic = WildWindExtractionMechanics.TryExtractRelic(relicHighHealth, true, false);
        report.Check(!highRelic.success, "Relic cannot be extracted while the boulder has more than half health.");
        ExtractionBoulderState relicNoTool = WildWindExtractionMechanics.CreateBoulder("relic_no_tool", ferron, 10f, relic: true);
        relicNoTool.health = relicNoTool.maxHealth * 0.4f;
        report.Check(!WildWindExtractionMechanics.TryExtractRelic(relicNoTool, false, false).success,
            "Relic extraction requires a crane or catcher drone.");
        ExtractionBoulderState relicReady = WildWindExtractionMechanics.CreateBoulder("relic_ready", ferron, 10f, relic: true);
        relicReady.health = relicReady.maxHealth * 0.4f;
        RelicExtractionResult extractedRelic = WildWindExtractionMechanics.TryExtractRelic(relicReady, true, false);
        report.Check(extractedRelic.success && relicReady.relicExtracted, "Crane extracts a relic carefully once the boulder is below half health.");
        ExtractionBoulderState relicShot = WildWindExtractionMechanics.CreateBoulder("relic_shot", ferron, 10f, relic: true);
        WildWindExtractionMechanics.ApplyProjectileDamage(relicShot, apShot, true);
        report.Check(relicShot.relicDestroyed, "Shooting a relic-bearing boulder destroys the relic.");
        ExtractionBoulderState relicRam = WildWindExtractionMechanics.CreateBoulder("relic_ram", ferron, 10f, relic: true);
        WildWindExtractionMechanics.ApplyRamToBoulder(relicRam, 8000f, 8f, false);
        report.Check(relicRam.relicDestroyed, "Ramming a relic-bearing boulder destroys the relic.");
        ExtractionBoulderState relicFire = WildWindExtractionMechanics.CreateBoulder("relic_fire", ferron, 10f, relic: true);
        ThermalResult relicHeat = WildWindExtractionMechanics.ApplyHeat(relicFire, relicFire.MassKg, 20f, true);
        report.Check(relicHeat.relicDestroyed && relicFire.relicDestroyed, "Flamethrower interaction destroys a relic.");
        ExtractionBoulderState relicCold = WildWindExtractionMechanics.CreateBoulder("relic_cold", ferron, 10f, relic: true);
        ProjectileProfile coldPulse = new ProjectileProfile { delivery = ShellDeliveryMode.HighExplosive, element = ExtractionDamageElement.Cold, damage = 500f, triggersExplosiveGas = false };
        WildWindExtractionMechanics.ApplyProjectileDamage(relicCold, coldPulse, true);
        report.Check(!relicCold.relicDestroyed, "Cold interaction does not damage relics.");

        HackResult hackBlocked = WildWindExtractionMechanics.ResolveHackAttempt(20f, 100f, true, true, false);
        HackResult droneHack = WildWindExtractionMechanics.ResolveHackAttempt(120f, 100f, true, false, true);
        HackResult hackFail = WildWindExtractionMechanics.ResolveHackAttempt(120f, 100f, true, true, false);
        HackResult hackWin = WildWindExtractionMechanics.ResolveHackAttempt(120f, 100f, true, true, true);
        report.Check(!hackBlocked.canStartMinigame, "Hackable objects require exposure before the minigame starts.");
        report.Check(!droneHack.canStartMinigame, "Drones cannot perform the two-phase hacking minigame.");
        report.Check(hackFail.canStartMinigame && !hackFail.success && hackFail.threatAdded > 0f && !hackFail.shipDamaged,
            "Failed hack raises threat but no longer damages the ship.");
        report.Check(hackWin.success && hackWin.threatAdded < hackFail.threatAdded, "Successful hack opens the object with only low threat.");

        FireControlResult safeBlock = WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Safe, ExtractionObjectKind.Boulder, false, true, false, 1f, 1f, true, true);
        FireControlResult dangerousDrone = WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.DangerousOnly, ExtractionObjectKind.Drone, true, false, false, 0.5f, 0.2f, true, true);
        FireControlResult unknownResource = WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Everything, ExtractionObjectKind.Boulder, true, false, false, 1f, 1f, true, true);
        FireControlResult relicRisk = WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Everything, ExtractionObjectKind.Boulder, true, true, true, 1f, 1f, true, true);
        FireControlResult goodShot = WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Everything, ExtractionObjectKind.Drone, true, true, false, 0.9f, 1f, true, true);
        FireControlResult badShot = WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Everything, ExtractionObjectKind.Drone, true, true, false, 0.2f, 0f, true, true);
        report.Check(!safeBlock.allowed, "Safe fire mode blocks resource targets by default.");
        report.Check(dangerousDrone.allowed, "Dangerous-only fire mode still engages dangerous drones.");
        report.Check(!unknownResource.allowed, "Unknown resource objects are blocked until scanned.");
        report.Check(!relicRisk.allowed, "Fire control blocks shots when relic risk is known.");
        report.Check(goodShot.hitChance > badShot.hitChance, "Scan resolution and visibility improve auto-fire lead quality.");
        report.Check(!WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Everything, ExtractionObjectKind.Drone, true, true, false, 1f, 1f, false, true).allowed,
            "Fire control reports no-ammo blocker.");
        report.Check(!WildWindExtractionMechanics.ResolveFireAuthorization(FireAuthorizationMode.Everything, ExtractionObjectKind.Drone, true, true, false, 1f, 1f, true, false).allowed,
            "Fire control reports sector blocker.");

        SalvageResult islandWreck = WildWindExtractionMechanics.ResolveDroneWreckSalvage(200f, true, false, 500f);
        SalvageResult stormWreck = WildWindExtractionMechanics.ResolveDroneWreckSalvage(200f, false, false, 500f);
        SalvageResult caughtWreck = WildWindExtractionMechanics.ResolveDroneWreckSalvage(2000f, false, true, 500f);
        report.Check(islandWreck.salvageAvailable && !islandWreck.requiresOnSiteDismantling, "Drone wreck salvage is available when the wreck falls onto an island.");
        report.Check(!stormWreck.salvageAvailable, "Drone wrecks that miss islands are lost to the storm without a catcher.");
        report.Check(caughtWreck.salvageAvailable && caughtWreck.caughtBeforeStorm && caughtWreck.requiresOnSiteDismantling,
            "Catcher drone can intercept a heavy wreck, but large wrecks require on-site dismantling.");
        ExtractionBoulderState sublikat = WildWindExtractionMechanics.CreateBoulder("sublikat", extraction.ores["sublikat"], 6f, ice: true);
        BoulderDamageResult sublikatHit = WildWindExtractionMechanics.ApplyProjectileDamage(sublikat, apShot, true);
        report.Check(sublikat.ore.isSublikat && sublikatHit.chunks == 0,
            "Sublikats are simple high-altitude icy dust deposits: damage does not make them shed chunks.");

        WildWindMetaCatalog metaCatalog = WildWindMetaMechanics.CreateMinimalCatalog();
        MetaAccountState account = WildWindMetaMechanics.CreateFreshAccount(metaCatalog);
        report.Check(metaCatalog != null && account != null, "Meta mechanics catalog and account state are available.");
        report.Check(account.ships.Count == 1 && account.ships[0].shipId == "pioneer" && account.portSlots == WildWindMetaMechanics.InitialPortSlots,
            "Fresh account starts with five port slots and a free Pioneer fallback.");
        account.gold = 200;
        MetaOperationResult slotBuy = WildWindMetaMechanics.BuyPortSlot(account);
        report.Check(slotBuy.success && account.gold == 0 && account.portSlots == 6,
            "Port slot costs exactly 200 gold/doubloons.");
        MetaOperationResult lockedBuy = WildWindMetaMechanics.BuyT1Ship(account, metaCatalog, "hauler_t1");
        report.Check(!lockedBuy.success, "T1 ships are not all open immediately; locked tree ship cannot be bought.");
        account.unlockedShips.Add("hauler_t1");
        account.silver = 2000;
        MetaOperationResult haulerBuy = WildWindMetaMechanics.BuyT1Ship(account, metaCatalog, "hauler_t1");
        report.Check(haulerBuy.success && account.ships.Exists(s => s.shipId == "hauler_t1") && account.silver == 800,
            "Unlocked T1 ship is bought for silver and consumes a port slot.");
        MetaShipInstance hauler = account.ships.Find(s => s.shipId == "hauler_t1");
        hauler.rigs.Add("reinforced_keel_rig");
        MetaOperationResult haulerSale = WildWindMetaMechanics.SellShip(account, metaCatalog, hauler);
        report.Check(haulerSale.success && haulerSale.silverDelta == 600 && haulerSale.constructionXpDelta > 0,
            "Ship sale returns 50% silver and grants construction XP; rigs are sold with the hull.");
        MetaShipInstance pioneer = account.ships.Find(s => s.shipId == "pioneer");
        MetaOperationResult pioneerSale = WildWindMetaMechanics.SellShip(account, metaCatalog, pioneer);
        report.Check(pioneerSale.success && pioneerSale.silverDelta == 0 && pioneerSale.constructionXpDelta > 0 && account.ships.Exists(s => s.shipId == "pioneer"),
            "Pioneer sells for zero silver, still gives construction XP, and fallback Pioneer is restored.");
        Dictionary<string, int> sortieLoot = new Dictionary<string, int> { { "ferron", 10 } };
        MetaShipInstance lostPioneer = account.ships[0];
        MetaOperationResult lostShip = WildWindMetaMechanics.LoseShipInSortie(account, metaCatalog, lostPioneer, sortieLoot);
        report.Check(lostShip.success && sortieLoot.Count == 0 && account.ships.Count == 1 && account.ships[0].shipId == "pioneer",
            "Lost sortie deletes ship and loot, then restores only the fallback Pioneer.");
        MetaShipInstance resourceProbe = account.ships[0];
        int maxSorties = resourceProbe.remainingSorties;
        for (int i = 0; i < maxSorties; i++)
        {
            WildWindMetaMechanics.ConsumeShipSortie(resourceProbe);
        }
        report.Check(resourceProbe.remainingSorties == 0 && !WildWindMetaMechanics.ConsumeShipSortie(resourceProbe),
            "Ships have limited sortie resource and cannot launch once it is depleted.");

        WildWindMetaMechanics.AddStorage(account, "ognejar_ore", 20);
        MetaOperationResult processOne = WildWindMetaMechanics.ProcessOneCycle(account, metaCatalog.processing["ognejar_ore"]);
        int charcoalAfterOne = WildWindMetaMechanics.GetStorage(account, "charcoal");
        float charcoalBuffer = account.processingBuffers["buffer:charcoal"];
        MetaOperationResult processTwo = WildWindMetaMechanics.ProcessOneCycle(account, metaCatalog.processing["ognejar_ore"]);
        report.Check(processOne.success && processTwo.success && charcoalAfterOne == 0 && charcoalBuffer > 0f && WildWindMetaMechanics.GetStorage(account, "charcoal") == 1,
            "Processing consumes raw units, stores fractional output buffers and emits only whole material units.");
        report.Check(account.recipes.Count == 0, "T1 recipes do not exist in the recipe database; T1 goods are bought directly.");
        report.Check(WildWindMetaMechanics.LearnRecipe(account, "trader_t2_crusher", RecipeTier.T2)
            && WildWindMetaMechanics.LearnRecipe(account, "precursor_t3_frame", RecipeTier.T3),
            "T2 recipes come from traders and T3 recipes are learned from precursor fragments.");
        WildWindMetaMechanics.AddStorage(account, "datacore_industrial", 2);
        float t2YieldBefore = account.recipes["trader_t2_crusher"].yieldMultiplier;
        MetaOperationResult t2Upgrade = WildWindMetaMechanics.UpgradeRecipe(account, "trader_t2_crusher");
        report.Check(t2Upgrade.success && account.recipes["trader_t2_crusher"].yieldMultiplier > t2YieldBefore,
            "Datacores upgrade T2 recipe yield and speed in the database.");
        WildWindMetaMechanics.AddStorage(account, "datacore_precursor", 3);
        MetaOperationResult t3Upgrade = WildWindMetaMechanics.UpgradeRecipe(account, "precursor_t3_frame");
        report.Check(t3Upgrade.success && WildWindMetaMechanics.GetStorage(account, "datacore_precursor") == 0,
            "T3 recipe upgrades use more expensive precursor datacore costs.");
        ContainerLot containerLot = WildWindMetaMechanics.OpenContainer(metaCatalog.dailyContainerLots, 11);
        bool hasSeparateCharcoalLots = metaCatalog.dailyContainerLots.Exists(l => l.itemId == "charcoal" && l.amount == 10)
            && metaCatalog.dailyContainerLots.Exists(l => l.itemId == "charcoal" && l.amount == 50);
        report.Check(containerLot != null && !string.IsNullOrWhiteSpace(containerLot.itemId) && containerLot.amount > 0 && hasSeparateCharcoalLots,
            "Containers drop exactly one configured lot, and different quantities of the same item are separate lots.");
        List<VoucherState> vouchers = new List<VoucherState>
        {
            new VoucherState { type = VoucherType.Coins, rarity = VoucherRarity.Rare, multiplier = 2f },
            new VoucherState { type = VoucherType.Coins, rarity = VoucherRarity.Epic, multiplier = 2f },
            new VoucherState { type = VoucherType.Experience, rarity = VoucherRarity.Common, multiplier = 1.1f }
        };
        report.Check(Approximately(WildWindMetaMechanics.ApplyVoucherSet(vouchers, VoucherType.Coins, 100f), 200f, 0.001f),
            "Vouchers replace economic modifiers and only one voucher of each type affects a sortie.");
        report.Check(WildWindMetaMechanics.ValidateConsumableLoadout(new List<string> { "oil", "rations", "coolant", "paint", "tea" }, out _)
            && !WildWindMetaMechanics.ValidateConsumableLoadout(new List<string> { "oil", "oil" }, out _)
            && !WildWindMetaMechanics.ValidateConsumableLoadout(new List<string> { "a", "b", "c", "d", "e", "f" }, out _),
            "Pre-sortie consumables allow five slots and reject duplicates.");
        account.gold = 300;
        bool boughtPaidTrack = WildWindMetaMechanics.BuyWeeklyBattlePassPaidTrack(account);
        WildWindMetaMechanics.AddRepeatableBattlePassProgress(account, 2500, 1);
        report.Check(boughtPaidTrack && account.weeklyBattlePassPaidTrack && account.weeklyBattlePassPoints == 43,
            "Weekly battle pass paid track is bought with gold and repeatable quests add pass points.");
        bool contractA = WildWindMetaMechanics.ActivateBattlePassContract(account, "small_contract_a");
        bool contractBBlocked = !WildWindMetaMechanics.ActivateBattlePassContract(account, "small_contract_b");
        WildWindMetaMechanics.AbandonBattlePassContract(account);
        bool contractBAfterAbandon = WildWindMetaMechanics.ActivateBattlePassContract(account, "small_contract_b");
        report.Check(contractA && contractBBlocked && contractBAfterAbandon,
            "Small battle pass contracts are one-at-a-time and can be abandoned.");
        account.gold = 100;
        bool bundleBought = WildWindMetaMechanics.BuyInternalGoldBundle(account, 75, new Dictionary<string, int> { { "datacore_industrial", 1 } });
        report.Check(bundleBought && account.gold == 25 && WildWindMetaMechanics.GetStorage(account, "datacore_industrial") >= 1,
            "Bundle shop uses only internal gold and grants configured rewards.");
        account.commanderTalentPoints = 3;
        bool talentOn = WildWindMetaMechanics.ActivateCommanderTalent(account, "steady_hands", 2);
        bool talentOff = WildWindMetaMechanics.DeactivateCommanderTalent(account, "steady_hands", 2);
        report.Check(talentOn && talentOff && account.commanderTalentPoints == 3,
            "Commander talents are separate from technologies and can be deactivated to refund points.");
        report.Check(metaCatalog.ships.ContainsKey("pioneer_mining_t2")
            && metaCatalog.ships.ContainsKey("precursor_t3")
            && metaCatalog.ships["pioneer_mining_t2"].tier == 2
            && metaCatalog.ships["precursor_t3"].tier == 3,
            "Minimal ship sources include T2 modernization/special/event style ships and T3 precursor ships.");
        report.Check(account.ships[0].preinstalledModules.Count > 0,
            "Ships can have preinstalled built-in modules that are not treated as removable fittings.");

        WildWindGameplayHud hud = gameplayHud != null ? gameplayHud : FindFirstObjectByType<WildWindGameplayHud>();
        report.Check(hud != null && !string.IsNullOrWhiteSpace(hud.MechanicsLabReport),
            "Gameplay HUD exposes the mechanics lab report for manual poking.");
        bool labMining = hud != null && hud.RunMechanicsLabMiningForTests() && hud.MechanicsLabReport.Contains("Mining:");
        bool labHazards = hud != null && hud.RunMechanicsLabHazardsForTests() && hud.MechanicsLabReport.Contains("Hazards:");
        bool labRelic = hud != null && hud.RunMechanicsLabRelicForTests() && hud.MechanicsLabReport.Contains("Relic:");
        bool labMeta = hud != null && hud.RunMechanicsLabMetaForTests() && hud.MechanicsLabReport.Contains("Meta:");
        report.Check(labMining && labHazards && labRelic && labMeta,
            "HUD mechanics lab buttons run mining, hazard, relic and meta scenarios.");

        report.Check(hud != null && hud.IsMetaPortReadyForTests,
            "HUD exposes a WoWS-style meta port with ship carousel, resource strip and tab actions.");
        report.Check(hud != null && hud.IsMetaPortRuntimeBoundForTests && hud.MetaPortReport.Contains("source=runtime"),
            "HUD meta port binds to the live MetaGameState instead of staying as an isolated demo surface.");
        MetaGameState runtimeMetaForPort = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        WildWindMetaPortUiState runtimePortState = WildWindMetaPortUiState.CreateFromRuntime(runtimeMetaForPort);
        report.Check(runtimeMetaForPort != null && runtimePortState.IsRuntimeBound && runtimePortState.BuildReport().Contains("source=runtime"),
            "Meta port state can be constructed directly from runtime progress for out-of-sortie screens.");
        bool visitedAllMetaTabs = false;
        if (hud != null)
        {
            foreach (WildWindMetaPortTab tab in WildWindMetaPortUiState.Tabs)
            {
                string tabName = WildWindMetaPortUiState.GetTabDisplayName(tab);
                bool selected = hud.SelectMetaPortTabForTests(tab) && hud.MetaPortSelectedTab == tabName;
                report.Check(selected, "Meta port selects tab " + tabName + ".");
                report.Check(hud.MetaPortReport.Contains("tab=" + tabName),
                    "Meta port report follows selected tab " + tabName + ".");
                report.Check(MetaPortTabContentContainsExpectedMechanics(tab, hud.MetaPortContentForTests),
                    "Meta port tab " + tabName + " exposes its core mechanics.");
                bool primaryAction = hud.RunMetaPortPrimaryActionForTests();
                report.Check(primaryAction, "Meta port primary action works for " + tabName + ".");
                bool secondaryAction = hud.RunMetaPortSecondaryActionForTests();
                report.Check(secondaryAction, "Meta port secondary action works for " + tabName + ".");
            }

            visitedAllMetaTabs = hud.MetaPortReport.Contains("visited="
                + WildWindMetaPortUiState.Tabs.Count
                + "/"
                + WildWindMetaPortUiState.Tabs.Count);
        }

        report.Check(visitedAllMetaTabs, "Meta port records that every meta tab was visited.");
        bool carouselWorks = hud != null && hud.SelectNextMetaPortShipForTests() && hud.SelectPreviousMetaPortShipForTests();
        report.Check(carouselWorks, "Meta port ship carousel can move forward and back.");
        string metaPortReport = hud != null ? hud.MetaPortReport : "";
        report.Check(metaPortReport.Contains("ship=") && metaPortReport.Contains("slots=") && metaPortReport.Contains("last="),
            "Meta port report includes selected ship, port slot state and last action feedback.");
        bool fullScreenOpened = hud != null && hud.ToggleMetaPortScreenForTests() && hud.IsMetaPortScreenVisibleForTests;
        bool fullScreenClosed = hud != null && hud.ToggleMetaPortScreenForTests() && !hud.IsMetaPortScreenVisibleForTests;
        report.Check(fullScreenOpened && fullScreenClosed,
            "Docked HUD Port button flow can open and close the full-screen meta port.");

        GameObject metaPortScreenObject = null;
        WildWindMetaPortScreen metaPortScreen = null;
        try
        {
            metaPortScreenObject = new GameObject("Meta Port Screen Big Test");
            metaPortScreen = metaPortScreenObject.AddComponent<WildWindMetaPortScreen>();
            metaPortScreen.RebuildScreen();
            report.Check(metaPortScreen.IsReadyForTests,
                "Standalone meta port screen can be built as a pure out-of-sortie interface.");

            bool standaloneVisitedAll = false;
            foreach (WildWindMetaPortTab tab in WildWindMetaPortUiState.Tabs)
            {
                string tabName = WildWindMetaPortUiState.GetTabDisplayName(tab);
                bool selected = metaPortScreen.SelectTabForTests(tab) && metaPortScreen.SelectedTabName == tabName;
                report.Check(selected, "Standalone meta port selects tab " + tabName + ".");
                report.Check(metaPortScreen.Report.Contains("tab=" + tabName),
                    "Standalone meta port report follows selected tab " + tabName + ".");
                report.Check(MetaPortTabContentContainsExpectedMechanics(tab, metaPortScreen.ContentForTests),
                    "Standalone meta port tab " + tabName + " exposes its core mechanics.");
                report.Check(metaPortScreen.RunPrimaryActionForTests(),
                    "Standalone meta port primary action works for " + tabName + ".");
                report.Check(metaPortScreen.RunSecondaryActionForTests(),
                    "Standalone meta port secondary action works for " + tabName + ".");
            }

            standaloneVisitedAll = metaPortScreen.Report.Contains("visited="
                + WildWindMetaPortUiState.Tabs.Count
                + "/"
                + WildWindMetaPortUiState.Tabs.Count);
            report.Check(standaloneVisitedAll, "Standalone meta port records that every tab was visited.");
            report.Check(metaPortScreen.SelectNextShipForTests() && metaPortScreen.SelectPreviousShipForTests(),
                "Standalone meta port ship carousel can move forward and back.");
            report.Check(metaPortScreen.Report.Contains("ship=") && metaPortScreen.Report.Contains("slots=") && metaPortScreen.Report.Contains("last="),
                "Standalone meta port report includes selected ship, port slots and action feedback.");
        }
        finally
        {
            if (metaPortScreenObject != null)
            {
                Destroy(metaPortScreenObject);
            }
        }
    }

    private static bool MetaPortTabContentContainsExpectedMechanics(WildWindMetaPortTab tab, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        switch (tab)
        {
            case WildWindMetaPortTab.Port:
                return ContainsAllIgnoreCase(content, "WoWS-style", "Port slots", "Fallback Pioneer");
            case WildWindMetaPortTab.ShipTree:
                return ContainsAllIgnoreCase(content, "Tree path", "Hauler T1", "T2/T3 ships");
            case WildWindMetaPortTab.Fitting:
                return ContainsAllIgnoreCase(content, "High: crusher", "Mid: crane", "Low/Rig");
            case WildWindMetaPortTab.Sorties:
                return ContainsAllIgnoreCase(content, "Sortie card", "Vouchers", "Consumables");
            case WildWindMetaPortTab.Missions:
                return ContainsAllIgnoreCase(content, "Combat-mission board", "Daily quest", "Trader tasks");
            case WildWindMetaPortTab.Mining:
                return ContainsAllIgnoreCase(content, "Crusher collection", "Boulders", "Active order");
            case WildWindMetaPortTab.Gas:
                return ContainsAllIgnoreCase(content, "Clouds", "wind drift", "Drone harvesters");
            case WildWindMetaPortTab.Scanning:
                return ContainsAllIgnoreCase(content, "Scanning exposure", "High slot", "Visibility");
            case WildWindMetaPortTab.Hacking:
                return ContainsAllIgnoreCase(content, "two-phase", "minigame", "threat");
            case WildWindMetaPortTab.Threat:
                return ContainsAllIgnoreCase(content, "wanted level", "rebel drone", "Quiet weapons");
            case WildWindMetaPortTab.FireControl:
                return ContainsAllIgnoreCase(content, "Gun automation", "Ammo", "relic risk");
            case WildWindMetaPortTab.Drones:
                return ContainsAllIgnoreCase(content, "catchers", "Ichor protection", "Active order");
            case WildWindMetaPortTab.Leviathans:
                return ContainsAllIgnoreCase(content, "passports", "hazard lures", "Carcass");
            case WildWindMetaPortTab.Salvage:
                return ContainsAllIgnoreCase(content, "drone wrecks", "storm", "crane");
            case WildWindMetaPortTab.Sublikats:
                return ContainsAllIgnoreCase(content, "high-altitude", "ice", "damage destroys");
            case WildWindMetaPortTab.Relics:
                return ContainsAllIgnoreCase(content, "below half health", "fire", "decoding");
            case WildWindMetaPortTab.Environment:
                return ContainsAllIgnoreCase(content, "Region passport", "Claudium fields", "Temperature");
            case WildWindMetaPortTab.Processing:
                return ContainsAllIgnoreCase(content, "Five branches", "Priority", "Buffers");
            case WildWindMetaPortTab.Production:
                return ContainsAllIgnoreCase(content, "Cascade production", "Frame order", "Stored frame");
            case WildWindMetaPortTab.Recipes:
                return ContainsAllIgnoreCase(content, "T1 recipes", "T2 comes", "Datacores");
            case WildWindMetaPortTab.Traders:
                return ContainsAllIgnoreCase(content, "Trader cards", "Geologists", "Reputation");
            case WildWindMetaPortTab.Shop:
                return ContainsAllIgnoreCase(content, "internal gold", "Visible offers", "Gold balance");
            case WildWindMetaPortTab.Containers:
                return ContainsAllIgnoreCase(content, "drop exactly one", "Lots", "Last drop");
            case WildWindMetaPortTab.BattlePass:
                return ContainsAllIgnoreCase(content, "Weekly pass", "Points", "Small contract");
            case WildWindMetaPortTab.Events:
                return ContainsAllIgnoreCase(content, "Monthly event", "roulette", "Fragments");
            case WildWindMetaPortTab.Technologies:
                return ContainsAllIgnoreCase(content, "large passive book", "Unlocked", "Vibro Resonance");
            case WildWindMetaPortTab.Commander:
                return ContainsAllIgnoreCase(content, "Commander has level", "free points", "Active talents");
            default:
                return false;
        }
    }

    private static bool ContainsAllIgnoreCase(string content, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(content) || terms == null)
        {
            return false;
        }

        for (int i = 0; i < terms.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(terms[i]) ||
                content.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
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

            bool tempSlotExists = File.Exists(tempSlotPath);
            report.Check(tempSlotExists, tempSlotExists
                ? "Temporary session save slot exists before Continue: " + tempSlotName + "."
                : "Temporary session save slot is missing after creation: " + tempSlotPath + ".");
            if (!tempSlotExists)
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
            yield return WaitForActiveScene(DefaultStartSceneName);

            Scene startScene = SceneManager.GetActiveScene();
            report.Check(startScene.name == DefaultStartSceneName, "Большой тест реально перешёл в стартовую сцену: " + startScene.name + ".");

            WildWindStartScreen startScreen = FindFirstObjectByType<WildWindStartScreen>();
            report.Check(startScreen != null, startScreen != null ? "Стартовый экран поднялся после выхода из мира." : "Стартовый экран не найден после загрузки StartScreen.");
            report.Check(startScreen != null && startScreen.gameplaySceneName == DefaultWorldSceneName,
                "Стартовый экран ведёт в " + DefaultWorldSceneName + ".");

            sessionLoopLaunchInProgress = true;
            bool firstLaunchStarted = WildWindSessionFlow.TryContinueWorldAndEnter(tempSlotName, DefaultWorldSceneName, out string firstLaunchError);
            report.Check(firstLaunchStarted, firstLaunchStarted
                ? "SessionFlow запустил первый Continue в world-сцену."
                : "SessionFlow не смог запустить первый Continue: " + firstLaunchError);
            if (!firstLaunchStarted)
            {
                yield break;
            }

            DisableDuplicateBigTestRunners();
            yield return WaitForLoadedSessionWorld(seed, tempSlotName);
            sessionLoopLaunchInProgress = false;
            ReportSessionWorldReadinessIfNeeded(seed, tempSlotName, "первый вход", report);

            Scene firstWorldScene = SceneManager.GetActiveScene();
            report.Check(firstWorldScene.name == DefaultWorldSceneName, "Continue загрузил world-сцену в первый раз: " + firstWorldScene.name + ".");
            ValidateLoadedSessionWorld(seed, tempSlotName, "первый вход", report);

            ValidateSessionExtractionCoreLoop(report);

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
            yield return WaitForActiveScene(DefaultStartSceneName);

            Scene returnedScene = SceneManager.GetActiveScene();
            report.Check(returnedScene.name == DefaultStartSceneName, "Внутриигровое меню вернуло сессию на стартовый экран: " + returnedScene.name + ".");
            report.Check(File.Exists(tempSlotPath), "Save slot остался на диске после выхода в меню с сохранением.");

            sessionLoopLaunchInProgress = true;
            bool secondLaunchStarted = WildWindSessionFlow.TryContinueWorldAndEnter(tempSlotName, DefaultWorldSceneName, out string secondLaunchError);
            report.Check(secondLaunchStarted, secondLaunchStarted
                ? "SessionFlow запустил повторный Continue в world-сцену."
                : "SessionFlow не смог запустить повторный Continue: " + secondLaunchError);
            if (!secondLaunchStarted)
            {
                yield break;
            }

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

    private static IEnumerator WaitForActiveScene(string sceneName)
    {
        for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != sceneName; i++)
        {
            yield return null;
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
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        return scene.name == DefaultWorldSceneName &&
            loadedWorld != null &&
            loadedWorld.RegionSeed == expectedSeed &&
            loadedRuntimeState != null &&
            loadedRuntimeState.LoadedManifestSeed == expectedSeed &&
            loadedMeta != null &&
            loadedMeta.EffectiveSaveFileName == expectedSaveFileName &&
            loadedMenu != null &&
            loadedSession != null &&
            loadedSession.IsReady &&
            loadedSession.SelectedSaveFileName == expectedSaveFileName &&
            loadedHud != null &&
            loadedHud.IsReady;
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
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        string worldSeed = loadedWorld != null ? loadedWorld.RegionSeed.ToString() : "<нет WorldRegionRuntime>";
        string runtimeSeed = loadedRuntimeState != null ? loadedRuntimeState.LoadedManifestSeed.ToString() : "<нет WorldRuntimeState>";
        string saveFileName = loadedMeta != null ? loadedMeta.EffectiveSaveFileName : "<нет MetaGameState>";
        string menu = loadedMenu != null ? "есть" : "нет";

        string session = loadedSession != null ? (loadedSession.IsReady ? "ready" : "not ready") : "none";
        string hud = loadedHud != null ? (loadedHud.IsReady ? "ready" : "not ready") : "none";

        return "scene=" + scene.name +
            ", expectedScene=" + DefaultWorldSceneName +
            ", worldSeed=" + worldSeed +
            ", expectedSeed=" + expectedSeed +
            ", runtimeSeed=" + runtimeSeed +
            ", metaSave=" + saveFileName +
            ", expectedSave=" + expectedSaveFileName +
            ", gameplayMenu=" + menu +
            ", gameplaySession=" + session +
            ", gameplayHud=" + hud;
    }

    private void ValidateSessionExtractionCoreLoop(BigTestReport report)
    {
        report.Section("Session extraction core");

        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        report.Check(loadedMeta != null, "Session extraction has MetaGameState.");
        report.Check(loadedSession != null && loadedSession.IsReady, "Session extraction has ready GameplaySession.");
        report.Check(loadedHud != null && loadedHud.IsReady, "Session extraction HUD is present and ready.");
        if (loadedMeta == null || loadedMeta.progress == null)
        {
            return;
        }

        loadedMeta.sessionExtractionCoreMode = true;
        loadedMeta.EnsureProgressInitialized();
        PlayerProgress progress = loadedMeta.progress;
        WorldConfigDatabase config = loadedMeta.WorldConfig;
        string capitalId = loadedMeta.GetCapitalIslandId();

        report.Check(loadedMeta.IsSessionExtractionCoreMode && progress.sessionExtractionCoreMode,
            "Core mode is active and persisted into PlayerProgress.");
        report.Check(loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.currentDockKind == DockingLocationKind.Island
            && progress.currentDockId == capitalId,
            "New session starts docked at the base: " + progress.currentDockId + ".");
        report.Check(loadedSession == null || loadedSession.CurrentDockId == capitalId,
            "GameplaySession default dock is the base: " + (loadedSession != null ? loadedSession.CurrentDockId : "<missing>") + ".");

        PlayerProgress sessionCoreSnapshot = progress.Clone();
        loadedMeta.ReplaceProgress(new PlayerProgress());
        loadedMeta.sessionExtractionCoreMode = true;
        loadedMeta.EnsureProgressInitialized();
        PlayerProgress freshCoreProgress = loadedMeta.progress;
        IslandProductionState freshCoreBaseStorage = loadedMeta.GetCapitalStorageState();
        report.Check(freshCoreProgress != null
            && freshCoreBaseStorage != null
            && freshCoreProgress.GetResourceAmount("ore") == 0
            && freshCoreProgress.GetResourceAmount("iron") == 0
            && freshCoreBaseStorage.GetResourceAmount("paper") == 0
            && freshCoreProgress.money == 0
            && freshCoreProgress.nextShopRefreshUtcTicks == 0L
            && freshCoreProgress.shopSeed == 0,
            "Fresh core progress does not seed legacy personal ore/iron, starting paper, money, or shop refresh.");
        loadedMeta.ReplaceProgress(sessionCoreSnapshot);
        loadedMeta.sessionExtractionCoreMode = true;
        loadedMeta.EnsureProgressInitialized();
        progress = loadedMeta.progress;
        config = loadedMeta.WorldConfig;
        capitalId = loadedMeta.GetCapitalIslandId();

        loadedMeta.RefreshSessionExtractionRuntimeActors();
        int gasCloudActors = FindObjectsByType<GasCloud>(FindObjectsSortMode.None).Length;
        int miningRockActors = FindObjectsByType<MiningRock>(FindObjectsSortMode.None).Length;
        int leviathanActors = FindObjectsByType<Leviathan>(FindObjectsSortMode.None).Length;
        report.Check(loadedMeta.SpawnedConfiguredIslandCount <= 1
            && gasCloudActors == 0
            && miningRockActors == 0
            && leviathanActors == 0,
            "Core mode keeps runtime actors to the base dock and suppresses legacy free-world gas, mining, and leviathan spawns.");

        bool freeFlightBlocked = loadedSession != null
            ? !loadedSession.TryBeginFreeFlight(out _)
            : !loadedMeta.BeginFreeFlight();
        report.Check(freeFlightBlocked && loadedMeta.CurrentMode == GameSessionMode.Docked,
            "Core mode blocks legacy free flight; sorties are the flight entry point.");

        MissionDefinitionSO flightMissionProbe = ScriptableObject.CreateInstance<MissionDefinitionSO>();
        flightMissionProbe.missionId = "legacy_core_flight_mission_probe";
        flightMissionProbe.rewardMoney = 777;
        flightMissionProbe.rewardExperience = 333;
        int moneyBeforeLegacyFlightMission = progress.money;
        bool legacyFlightMissionStarted = loadedMeta.TryBeginFlightSession(flightMissionProbe);
        loadedMeta.CompleteFlightMission(flightMissionProbe);
        UnityEngine.Object.DestroyImmediate(flightMissionProbe);
        report.Check(!legacyFlightMissionStarted
            && loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.money == moneyBeforeLegacyFlightMission
            && !progress.acceptedMissionIds.Contains("legacy_core_flight_mission_probe")
            && !progress.completedMissionIds.Contains("legacy_core_flight_mission_probe"),
            "Core mode blocks legacy flight mission start and completion rewards; sorties are the only flight loop.");

        TechTreeDefinitionSO originalTechTree = loadedMeta.techTree;
        TechTreeDefinitionSO legacyTechTreeProbe = ScriptableObject.CreateInstance<TechTreeDefinitionSO>();
        legacyTechTreeProbe.nodes = new List<TechTreeNode>
        {
            new TechTreeNode
            {
                nodeId = "legacy_core_xp_research_probe",
                displayName = "Legacy core XP research probe",
                kind = TechTreeNodeKind.Fundamental,
                researchCostXp = 10
            },
            new TechTreeNode
            {
                nodeId = "legacy_core_money_purchase_probe",
                displayName = "Legacy core money purchase probe",
                kind = TechTreeNodeKind.Module,
                startsResearched = true,
                purchasePrice = 25
            }
        };
        loadedMeta.techTree = legacyTechTreeProbe;
        string xpShipId = string.IsNullOrWhiteSpace(progress.selectedHullId)
            ? GameplaySessionSaveData.DefaultStarterHullId
            : progress.selectedHullId;
        int xpBeforeTechProbeSeed = progress.GetShipExperience(xpShipId);
        int moneyBeforeTechProbeSeed = progress.money;
        progress.AddShipExperience(xpShipId, 25);
        progress.money += 50;
        int xpBeforeLegacyTechTree = progress.GetShipExperience(xpShipId);
        int moneyBeforeLegacyTechTree = progress.money;
        bool legacyXpResearchStarted = loadedMeta.TryResearchNode("legacy_core_xp_research_probe");
        bool legacyMoneyPurchaseStarted = loadedMeta.TryPurchaseNode("legacy_core_money_purchase_probe");
        loadedMeta.techTree = originalTechTree;
        UnityEngine.Object.DestroyImmediate(legacyTechTreeProbe);
        bool legacyTechTreeBlocked = !legacyXpResearchStarted
            && !legacyMoneyPurchaseStarted
            && progress.GetShipExperience(xpShipId) == xpBeforeLegacyTechTree
            && progress.money == moneyBeforeLegacyTechTree
            && !progress.researchedNodeIds.Contains("legacy_core_xp_research_probe")
            && !progress.purchasedNodeIds.Contains("legacy_core_xp_research_probe")
            && !progress.purchasedNodeIds.Contains("legacy_core_money_purchase_probe");
        progress.TrySpendShipExperience(xpShipId, Mathf.Max(0, progress.GetShipExperience(xpShipId) - xpBeforeTechProbeSeed));
        progress.money = moneyBeforeTechProbeSeed;
        report.Check(legacyTechTreeBlocked,
            "Core mode blocks legacy XP research and money purchases; unlocks come from base resource technologies and cascade production.");

        IslandProductionState legacyInventoryBaseStorage = loadedMeta.GetCapitalStorageState();
        string legacyInventoryProbeId = "legacy_personal_inventory_probe";
        int baseProbeBefore = legacyInventoryBaseStorage != null ? legacyInventoryBaseStorage.GetResourceAmount(legacyInventoryProbeId) : 0;
        int personalProbeBefore = progress.GetResourceAmount(legacyInventoryProbeId);
        progress.AddResource(legacyInventoryProbeId, 4);
        loadedMeta.EnsureProgressInitialized();
        int baseProbeAfterMigration = legacyInventoryBaseStorage != null ? legacyInventoryBaseStorage.GetResourceAmount(legacyInventoryProbeId) : 0;
        bool legacyInventoryMigrated = legacyInventoryBaseStorage != null
            && progress.GetResourceAmount(legacyInventoryProbeId) == 0
            && baseProbeAfterMigration == baseProbeBefore + personalProbeBefore + 4;
        legacyInventoryBaseStorage?.SetResourceAmount(legacyInventoryProbeId, baseProbeBefore);

        progress.nextShopRefreshUtcTicks = Math.Max(progress.lastProcessUtcTicks, DateTime.UtcNow.Ticks) - TimeSpan.FromSeconds(1).Ticks;
        progress.shopSeed = 1234567;
        int moneyBeforeCoreAddMoney = progress.money;
        string directResourceRewardProbeId = "legacy_core_direct_resource_reward";
        int directResourceBeforeCoreReward = legacyInventoryBaseStorage != null
            ? legacyInventoryBaseStorage.GetResourceAmount(directResourceRewardProbeId)
            : 0;
        string directXpRewardProbeId = "legacy_core_direct_xp_reward_ship";
        int directXpBeforeCoreReward = progress.GetShipExperience(directXpRewardProbeId);
        int selectedXpBeforeCoreReward = progress.GetShipExperience(xpShipId);
        loadedMeta.AddMoney(99);
        loadedMeta.AddResource(directResourceRewardProbeId, 7);
        loadedMeta.AddExperienceToShip(directXpRewardProbeId, 44);
        loadedMeta.AddExperienceToSelectedShip(33);
        loadedMeta.AdvanceRealTimeProcesses(new DateTime(
            Math.Max(progress.lastProcessUtcTicks, DateTime.UtcNow.Ticks) + TimeSpan.FromSeconds(5).Ticks,
            DateTimeKind.Utc));
        report.Check(legacyInventoryMigrated
            && progress.nextShopRefreshUtcTicks == 0L
            && progress.shopSeed == 0
            && progress.money == moneyBeforeCoreAddMoney
            && legacyInventoryBaseStorage != null
            && legacyInventoryBaseStorage.GetResourceAmount(directResourceRewardProbeId) == directResourceBeforeCoreReward
            && progress.GetShipExperience(directXpRewardProbeId) == directXpBeforeCoreReward
            && progress.GetShipExperience(xpShipId) == selectedXpBeforeCoreReward,
            "Core mode migrates legacy personal inventory into base storage, disables legacy shop refresh, and ignores legacy money/resource/ship-XP rewards.");

        int societyNeedsBefore = CountSocietyNeedStates(progress);
        long nowTicks = DateTime.UtcNow.Ticks;
        int passengerEvents = PassengerTrafficSimulator.Advance(config, progress, 60f);
        int societyEvents = IslandSocietySimulator.Advance(config, progress, 60f);
        int islandProductionEvents = IslandProductionSimulator.Advance(config, progress, nowTicks, nowTicks + TimeSpan.FromMinutes(5).Ticks);
        int islandIndustryEvents = IslandIndustrySimulator.Advance(config, progress, nowTicks, nowTicks + TimeSpan.FromMinutes(5).Ticks);
        int societyNeedsAfter = CountSocietyNeedStates(progress);
        report.Check(passengerEvents == 0
            && societyEvents == 0
            && islandProductionEvents == 0
            && islandIndustryEvents == 0
            && societyNeedsAfter == societyNeedsBefore,
            "Core mode does not tick legacy passengers, social needs, island production, or island industry.");

        WorldConfigDatabase coreFlagshipConfig = new WorldConfigDatabase();
        coreFlagshipConfig.shipTreeEntries.Add(new ShipTreeEntryConfig
        {
            shipId = "legacy_core_flagship",
            localNameRu = "Legacy Core Flagship",
            rank = 3,
            hullId = "legacy_core_flagship_hull"
        });
        PlayerProgress coreFlagshipProgress = new PlayerProgress();
        coreFlagshipProgress.sessionExtractionCoreMode = true;
        coreFlagshipProgress.selectedHullId = "legacy_core_flagship_hull";
        coreFlagshipProgress.Normalize();
        FlagshipInteriorState blockedCorePlayerInterior = FlagshipInteriorSimulator.EnsurePlayerFlagshipInterior(coreFlagshipConfig, coreFlagshipProgress);
        int playerFlagshipInteriorCountAfterCoreEnsure = coreFlagshipProgress.flagshipInteriors != null ? coreFlagshipProgress.flagshipInteriors.Count : 0;
        FlagshipInteriorState staleCoreFlagship = coreFlagshipProgress.GetFlagshipInteriorState("legacy_core_flagship", true);
        staleCoreFlagship.rank = 3;
        staleCoreFlagship.crewCapacity = 6;
        FlagshipInteriorSimulator.EnsureDefaultInterior(staleCoreFlagship);
        staleCoreFlagship.expeditionActive = true;
        staleCoreFlagship.expeditionStartedUtcTicks = nowTicks;
        FlagshipNeedState coreFlagshipMorale = staleCoreFlagship.GetNeedState(FlagshipNeedIds.Morale, true);
        FlagshipNeedState coreFlagshipStamina = staleCoreFlagship.GetNeedState(FlagshipNeedIds.Stamina, true);
        coreFlagshipMorale.currentValue = coreFlagshipMorale.maxValue;
        coreFlagshipMorale.initialized = true;
        coreFlagshipStamina.currentValue = 0f;
        coreFlagshipStamina.initialized = true;
        coreFlagshipProgress.AddShipCargo("food", 20);
        int coreFlagshipFoodBefore = coreFlagshipProgress.GetShipCargoAmount("food");
        float coreFlagshipMoraleBefore = coreFlagshipMorale.currentValue;
        float coreFlagshipStaminaBefore = coreFlagshipStamina.currentValue;
        FlagshipFailureState coreFlagshipFailure = FlagshipInteriorSimulator.AddFailure(
            staleCoreFlagship,
            "engine_room",
            FlagshipFailureSeverity.Major,
            FlagshipFailureEffectKind.EfficiencyPenalty,
            "",
            0.45f,
            200f,
            nowTicks);
        FlagshipRoomState coreFlagshipRepairRoom = staleCoreFlagship.GetRoomState("repair_workshop", false);
        FlagshipRoomMode coreFlagshipRepairRoomModeBefore = coreFlagshipRepairRoom != null ? coreFlagshipRepairRoom.mode : FlagshipRoomMode.Full;
        bool coreFlagshipStartBlocked = !FlagshipInteriorSimulator.StartExpedition(coreFlagshipProgress, "legacy_core_flagship", nowTicks, out _);
        bool coreFlagshipRoomModeBlocked = !FlagshipInteriorSimulator.SetRoomMode(coreFlagshipProgress, "legacy_core_flagship", "repair_workshop", FlagshipRoomMode.Off)
            && (coreFlagshipRepairRoom == null || coreFlagshipRepairRoom.mode == coreFlagshipRepairRoomModeBefore);
        int coreFlagshipAdvanceEvents = FlagshipInteriorSimulator.Advance(coreFlagshipConfig, coreFlagshipProgress, nowTicks, nowTicks + TimeSpan.FromHours(1).Ticks);
        bool coreFlagshipReturnBlocked = !FlagshipInteriorSimulator.CompleteExpeditionReturn(coreFlagshipProgress, "legacy_core_flagship", out _);
        bool coreFlagshipRepairBlocked = coreFlagshipFailure != null
            && !FlagshipInteriorSimulator.CompleteManualRepair(coreFlagshipProgress, "legacy_core_flagship", coreFlagshipFailure.failureId, out _)
            && staleCoreFlagship.FindFailure(coreFlagshipFailure.failureId, out _) != null;
        report.Check(blockedCorePlayerInterior == null
            && playerFlagshipInteriorCountAfterCoreEnsure == 0
            && coreFlagshipStartBlocked
            && coreFlagshipRoomModeBlocked
            && coreFlagshipAdvanceEvents == 0
            && coreFlagshipReturnBlocked
            && coreFlagshipRepairBlocked
            && staleCoreFlagship.expeditionActive
            && Approximately(coreFlagshipMorale.currentValue, coreFlagshipMoraleBefore, 0.001f)
            && Approximately(coreFlagshipStamina.currentValue, coreFlagshipStaminaBefore, 0.001f)
            && coreFlagshipProgress.GetShipCargoAmount("food") == coreFlagshipFoodBefore,
            "Core mode blocks direct flagship social/expedition APIs and does not tick stale flagship needs.");

        int logisticsShipsBefore = progress.logisticsShips != null ? progress.logisticsShips.Count : 0;
        int scoutShipsBefore = progress.scoutShips != null ? progress.scoutShips.Count : 0;
        int gasShipsBefore = progress.gasHarvesterShips != null ? progress.gasHarvesterShips.Count : 0;
        int miningShipsBefore = progress.miningShips != null ? progress.miningShips.Count : 0;
        int miningRocksBefore = progress.miningRocks != null ? progress.miningRocks.Count : 0;
        long autonomousFleetToTicks = nowTicks + TimeSpan.FromMinutes(15).Ticks;
        int legacyAutonomousEvents = 0;
        if (loadedMeta.logisticsFleet != null)
        {
            legacyAutonomousEvents += loadedMeta.logisticsFleet.Advance(config, progress, loadedMeta.CurrentCatalog, loadedMeta.techTree, nowTicks, autonomousFleetToTicks);
        }

        if (loadedMeta.scoutFleet != null)
        {
            legacyAutonomousEvents += loadedMeta.scoutFleet.Advance(config, progress, nowTicks, autonomousFleetToTicks);
        }

        if (loadedMeta.gasHarvesterFleet != null)
        {
            legacyAutonomousEvents += loadedMeta.gasHarvesterFleet.Advance(config, progress, loadedMeta.CurrentCatalog, loadedMeta.techTree, nowTicks, autonomousFleetToTicks);
        }

        legacyAutonomousEvents += MiningWorldSimulator.Advance(config, progress, nowTicks, autonomousFleetToTicks);
        if (loadedMeta.miningFleet != null)
        {
            legacyAutonomousEvents += loadedMeta.miningFleet.Advance(config, progress, loadedMeta.CurrentCatalog, loadedMeta.techTree, nowTicks, autonomousFleetToTicks);
        }

        report.Check(legacyAutonomousEvents == 0
            && (progress.logisticsShips != null ? progress.logisticsShips.Count : 0) == logisticsShipsBefore
            && (progress.scoutShips != null ? progress.scoutShips.Count : 0) == scoutShipsBefore
            && (progress.gasHarvesterShips != null ? progress.gasHarvesterShips.Count : 0) == gasShipsBefore
            && (progress.miningShips != null ? progress.miningShips.Count : 0) == miningShipsBefore
            && (progress.miningRocks != null ? progress.miningRocks.Count : 0) == miningRocksBefore,
            "Core mode does not tick legacy logistics, scout, gas, mining fleets, or the ambient mining world.");

        int activeProcessesBeforeLegacyStart = progress.activeProcesses != null ? progress.activeProcesses.Count : 0;
        int oreBeforeLegacyStart = progress.GetResourceAmount("ore");
        MissionDefinitionSO timedMissionProbe = ScriptableObject.CreateInstance<MissionDefinitionSO>();
        timedMissionProbe.missionId = "legacy_core_timed_mission_probe";
        timedMissionProbe.canRunAsTimedMission = true;
        bool idleMiningStartedInCore = loadedMeta.StartIdleMining();
        bool ironSmeltingStartedInCore = loadedMeta.StartIronSmelting();
        bool timedMissionStartedInCore = loadedMeta.StartTimedMission(timedMissionProbe);
        UnityEngine.Object.DestroyImmediate(timedMissionProbe);
        report.Check(!idleMiningStartedInCore
            && !ironSmeltingStartedInCore
            && !timedMissionStartedInCore
            && (progress.activeProcesses != null ? progress.activeProcesses.Count : 0) == activeProcessesBeforeLegacyStart
            && progress.GetResourceAmount("ore") == oreBeforeLegacyStart
            && !progress.acceptedMissionIds.Contains("legacy_core_timed_mission_probe"),
            "Core mode blocks legacy dock timed processes: idle mining, iron smelting, and timed missions cannot bypass sortie extraction.");

        progress.activeProcesses ??= new List<TimedProcessState>();
        progress.activeProcesses.Add(new TimedProcessState
        {
            processId = "legacy_core_stale_idle_mining",
            kind = TimedProcessKind.IdleMining,
            nextCompletionUtcTicks = Math.Max(progress.lastProcessUtcTicks, DateTime.UtcNow.Ticks) + TimeSpan.FromSeconds(1).Ticks,
            durationSeconds = 1,
            outputResourceId = "ore",
            outputAmount = 99
        });
        progress.activeProcesses.Add(new TimedProcessState
        {
            processId = "legacy_core_stale_crafting",
            kind = TimedProcessKind.Crafting,
            nextCompletionUtcTicks = Math.Max(progress.lastProcessUtcTicks, DateTime.UtcNow.Ticks) + TimeSpan.FromSeconds(1).Ticks,
            durationSeconds = 1,
            outputResourceId = "iron",
            outputAmount = 77
        });
        progress.activeProcesses.Add(new TimedProcessState
        {
            processId = "legacy_core_stale_mission",
            kind = TimedProcessKind.Mission,
            nextCompletionUtcTicks = Math.Max(progress.lastProcessUtcTicks, DateTime.UtcNow.Ticks) + TimeSpan.FromSeconds(1).Ticks,
            durationSeconds = 1,
            missionId = "legacy_core_stale_mission_probe",
            rewardMoney = 66,
            rewardExperience = 55,
            experienceShipId = "legacy_core_process_ship"
        });
        int oreBeforeStaleProcesses = progress.GetResourceAmount("ore");
        int ironBeforeStaleProcesses = progress.GetResourceAmount("iron");
        int moneyBeforeStaleProcesses = progress.money;
        int xpBeforeStaleProcesses = progress.GetShipExperience("legacy_core_process_ship");
        loadedMeta.AdvanceRealTimeProcesses(new DateTime(
            Math.Max(progress.lastProcessUtcTicks, DateTime.UtcNow.Ticks) + TimeSpan.FromSeconds(5).Ticks,
            DateTimeKind.Utc));
        report.Check((progress.activeProcesses != null ? progress.activeProcesses.Count : 0) == 0
            && progress.GetResourceAmount("ore") == oreBeforeStaleProcesses
            && progress.GetResourceAmount("iron") == ironBeforeStaleProcesses
            && progress.money == moneyBeforeStaleProcesses
            && progress.GetShipExperience("legacy_core_process_ship") == xpBeforeStaleProcesses
            && !progress.completedMissionIds.Contains("legacy_core_stale_mission_probe"),
            "Core mode stops stale legacy timed-process jobs without paying old resources, money, experience, or mission completion.");

        IslandProductionState initialBaseStorage = loadedMeta.GetCapitalStorageState();
        if (initialBaseStorage != null)
        {
            initialBaseStorage.AddResource("charcoal", 5);
            int baseCharcoalBeforeLegacyCargo = initialBaseStorage.GetResourceAmount("charcoal");
            int shipCharcoalBeforeLegacyCargo = progress.GetShipCargoAmount("charcoal");
            bool legacyCargoLoadStarted = loadedMeta.TryLoadShipCargoFromCurrentDock("charcoal", 1, out _);

            progress.AddShipCargo("windshale_ore", 1);
            int baseWindshaleBeforeLegacyCargo = initialBaseStorage.GetResourceAmount("windshale_ore");
            int shipWindshaleBeforeLegacyCargo = progress.GetShipCargoAmount("windshale_ore");
            bool legacyCargoUnloadStarted = loadedMeta.TryUnloadShipCargoToCurrentDock("windshale_ore", 1, out _);
            bool legacyCargoPublicMethodsBlocked = !legacyCargoLoadStarted
                && !legacyCargoUnloadStarted
                && initialBaseStorage.GetResourceAmount("charcoal") == baseCharcoalBeforeLegacyCargo
                && progress.GetShipCargoAmount("charcoal") == shipCharcoalBeforeLegacyCargo
                && initialBaseStorage.GetResourceAmount("windshale_ore") == baseWindshaleBeforeLegacyCargo
                && progress.GetShipCargoAmount("windshale_ore") == shipWindshaleBeforeLegacyCargo;
            progress.TrySpendShipCargo("windshale_ore", 1);

            progress.cargoTransfer ??= new CargoTransferState();
            progress.cargoTransfer.active = true;
            progress.cargoTransfer.islandId = capitalId;
            progress.cargoTransfer.startedUtcTicks = DateTime.UtcNow.Ticks;
            progress.cargoTransfer.secondsPerItem = 0.01f;
            progress.cargoTransfer.nextOperationUtcTicks = 0L;
            progress.cargoTransfer.currentOperationIndex = 0;
            progress.cargoTransfer.operations = new List<CargoTransferOperation>
            {
                new CargoTransferOperation
                {
                    itemId = "charcoal",
                    loadToShip = true,
                    remainingAmount = 1
                }
            };

            int baseCharcoalBeforeStaleTransfer = initialBaseStorage.GetResourceAmount("charcoal");
            int shipCharcoalBeforeStaleTransfer = progress.GetShipCargoAmount("charcoal");
            loadedMeta.AdvanceRealTimeProcesses(GetFutureProcessTime(progress, 2));
            report.Check(legacyCargoPublicMethodsBlocked
                && progress.cargoTransfer != null
                && !progress.cargoTransfer.active
                && initialBaseStorage.GetResourceAmount("charcoal") == baseCharcoalBeforeStaleTransfer
                && progress.GetShipCargoAmount("charcoal") == shipCharcoalBeforeStaleTransfer,
                "Core mode blocks legacy island cargo loading, unloading, and stale cargo-transfer jobs.");

            progress.shipEngineFuelTank.SetResource("charcoal");
            progress.shipClaudiumTank.SetResource("claudium");
            progress.shipEngineFuelTank.amountKg = 0f;
            progress.shipClaudiumTank.amountKg = 0f;
            SyncTestShipConsumables(loadedSession, progress);
            int fuelStorageBefore = initialBaseStorage.GetResourceAmount("charcoal") + initialBaseStorage.AddResource("charcoal", 120);
            int claudiumStorageBefore = initialBaseStorage.GetResourceAmount("claudium") + initialBaseStorage.AddResource("claudium", 80);
            bool canRefuelAtBase = loadedMeta.CanRefuelBaseShip(out string refuelReadyMessage);
            bool refueledAtBase = loadedMeta.TryRefuelBaseShip(out string refuelMessage);
            report.Check(canRefuelAtBase
                && refueledAtBase
                && progress.shipEngineFuelTank.GetAmount("charcoal") > 0f
                && progress.shipClaudiumTank.GetAmount("claudium") > 0f
                && initialBaseStorage.GetResourceAmount("charcoal") < fuelStorageBefore
                && initialBaseStorage.GetResourceAmount("claudium") < claudiumStorageBefore,
                "Core base can refuel coal and claudium from base storage before a sortie: "
                + refuelReadyMessage + " / " + refuelMessage);

            bool pioneerAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, loadedMeta.techTree, progress, out ShipAssemblyResult pioneerAssembly);
            float pioneerStarterPayloadKg = loadedMeta.startingFuelKg + loadedMeta.startingClaudiumKg + 25f;
            string pioneerFlightEnvelope = "assembly did not build.";
            bool pioneerFlightEnvelopeOk = pioneerAssemblyBuilt
                && HasStablePioneerFlightEnvelope(
                    pioneerAssembly,
                    pioneerStarterPayloadKg,
                    40f,
                    0.5f,
                    12f,
                    out pioneerFlightEnvelope);
            report.Check(pioneerFlightEnvelopeOk,
                "Pioneer fallback has enough lift, hover power reserve, thrust, and speed for a starter ore sortie: "
                + pioneerFlightEnvelope);
        }
        else
        {
            report.Check(false, "Core refuel test has base storage.");
        }

        string processingOverview = loadedMeta.GetBaseProcessingOverviewText();
        string cascadeOverview = loadedMeta.GetBaseCascadeProductionOverviewText();
        string nextCascadeOverview = loadedMeta.GetNextBaseCascadeOrderOverviewText();
        bool processingOverviewValid = processingOverview.Contains("Processing " + SessionExtractionIndustry.ProcessingBranches.Length);
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            processingOverviewValid &= processingOverview.Contains(SessionExtractionIndustry.GetProcessingDisplayName(SessionExtractionIndustry.ProcessingBranches[i]));
        }

        bool cascadeOverviewValid = cascadeOverview.Contains("Cascade " + SessionExtractionIndustry.CascadeProductionTypes.Length);
        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            cascadeOverviewValid &= cascadeOverview.Contains(SessionExtractionIndustry.GetProductionDisplayName(SessionExtractionIndustry.CascadeProductionTypes[i]));
        }

        report.Check(processingOverviewValid
            && cascadeOverviewValid
            && nextCascadeOverview.Contains("Next cascade")
            && nextCascadeOverview.Contains("Starter airframe kit"),
            "Base home overview exposes all five processing branches, all eight cascade production types, and the next cascade order.");

        SortieZoneDefinition defaultSortie = loadedMeta.CreateDefaultSafeOreSortieDefinition();
        report.Check(defaultSortie != null
            && Approximately(defaultSortie.radiusMeters, SessionExtractionConstants.DefaultSortieRadiusMeters, 0.1f)
            && defaultSortie.primaryBranch == BaseProcessingBranch.Ore,
            "Default safe sortie is a 1.5 km ore cylinder.");

        Vector3 outsideCylinderPosition = new Vector3(
            defaultSortie.centerPosition.x + defaultSortie.radiusMeters + 750f,
            defaultSortie.entryPosition.y + 2500f,
            defaultSortie.centerPosition.z);
        SortieSessionState outsideBoundarySession = new SortieSessionState
        {
            active = true,
            zone = defaultSortie.Clone()
        };
        outsideBoundarySession.Normalize();
        SortieReturnEstimate outsideBoundaryEstimate = SortieExtractionCalculator.Calculate(
            outsideBoundarySession,
            outsideCylinderPosition,
            new SortieReturnProfile { cruiseSpeedMS = defaultSortie.returnCruiseSpeedMS });
        Vector3 edgeCylinderPosition = new Vector3(
            defaultSortie.centerPosition.x + defaultSortie.radiusMeters,
            defaultSortie.entryPosition.y + 2500f,
            defaultSortie.centerPosition.z);
        SortieReturnEstimate edgeBoundaryEstimate = SortieExtractionCalculator.Calculate(
            outsideBoundarySession,
            edgeCylinderPosition,
            new SortieReturnProfile { cruiseSpeedMS = defaultSortie.returnCruiseSpeedMS });
        GameObject boundaryClampProbe = new GameObject("Boundary Clamp Probe");
        bool boundaryClampDisabledByDefault = !boundaryClampProbe.AddComponent<SortieBoundaryController>().clampToCylinder;
        if (Application.isPlaying)
        {
            Destroy(boundaryClampProbe);
        }
        else
        {
            DestroyImmediate(boundaryClampProbe);
        }
        report.Check(boundaryClampDisabledByDefault
            && outsideBoundaryEstimate.isNearBoundary
            && !outsideBoundaryEstimate.isInsideCylinder,
            "Sortie boundary is a non-solid extraction threshold: crossing the ring still counts as being at the boundary, but the runtime clamp is off by default.");
        report.Check(edgeBoundaryEstimate.isNearBoundary
            && !edgeBoundaryEstimate.isInsideCylinder,
            "The visible sortie ring itself counts as the exit edge, not as inside the cylinder.");

        ShipPhysics activeBoundaryShip = loadedMeta != null && loadedMeta.shipLoader != null
            ? loadedMeta.shipLoader.targetShip
            : null;
        GameObject staleBoundaryShipObject = new GameObject("Boundary Stale Ship Probe");
        Rigidbody staleBoundaryBody = staleBoundaryShipObject.AddComponent<Rigidbody>();
        staleBoundaryBody.isKinematic = true;
        staleBoundaryBody.useGravity = false;
        ShipPhysics staleBoundaryShip = staleBoundaryShipObject.AddComponent<ShipPhysics>();
        GameObject boundaryResolverObject = new GameObject("Boundary Resolver Probe");
        SortieBoundaryController boundaryResolver = boundaryResolverObject.AddComponent<SortieBoundaryController>();
        boundaryResolver.metaGameState = loadedMeta;
        boundaryResolver.targetShip = staleBoundaryShip;
        bool boundaryUsesLoaderShip = activeBoundaryShip != null
            && TryInvokePrivateMethod(boundaryResolver, "ResolveShip", report, out ShipPhysics resolvedBoundaryShip)
            && resolvedBoundaryShip == activeBoundaryShip;
        if (Application.isPlaying)
        {
            Destroy(staleBoundaryShipObject);
            Destroy(boundaryResolverObject);
        }
        else
        {
            DestroyImmediate(staleBoundaryShipObject);
            DestroyImmediate(boundaryResolverObject);
        }

        report.Check(boundaryUsesLoaderShip,
            "Sortie boundary controller resolves the active loader ship instead of a stale cached ShipPhysics.");

        List<SortieZoneDefinition> defaultSorties = loadedMeta.CreateDefaultSessionSortieDefinitions();
        HashSet<BaseProcessingBranch> sortieBranches = new HashSet<BaseProcessingBranch>();
        bool sortieCatalogValid = defaultSorties.Count == SessionExtractionIndustry.ProcessingBranches.Length;
        for (int i = 0; i < defaultSorties.Count; i++)
        {
            SortieZoneDefinition sortie = defaultSorties[i];
            sortieCatalogValid &= sortie != null
                && !string.IsNullOrWhiteSpace(sortie.sortieId)
                && !string.IsNullOrWhiteSpace(sortie.starterResourceItemId)
                && Approximately(sortie.radiusMeters, SessionExtractionConstants.DefaultSortieRadiusMeters, 0.1f)
                && sortie.distanceToBaseKm > 0f;
            if (sortie != null)
            {
                sortieBranches.Add(sortie.primaryBranch);
            }
        }

        report.Check(sortieCatalogValid && sortieBranches.Count == SessionExtractionIndustry.ProcessingBranches.Length,
            "Session extraction has a default 1.5 km sortie catalog for ore, gas, automatons, leviathans, and survey data.");

        bool cycledSortie = loadedHud != null
            ? loadedHud.TryCycleSessionSortie()
            : loadedMeta.SelectNextSessionSortie(out _);
        report.Check(cycledSortie
            && loadedMeta.GetSelectedSessionSortieDefinition().primaryBranch != BaseProcessingBranch.Ore,
            "HUD can cycle the selected extraction sortie before launch.");

        bool selectedGasBlockedWithoutFitting = !loadedMeta.CanBeginSelectedSessionSortie(out string gasBlockedReason);
        progress.InstallModule(SessionExtractionConstants.LegacyUtilitySlotId, SessionExtractionConstants.StarterGasHarvesterModuleId);
        bool selectedGasStillBlockedWithLegacyUtility = !loadedMeta.CanBeginSelectedSessionSortie(out string legacyUtilityReason);
        progress.InstallModule(SessionExtractionConstants.LegacyUtilitySlotId, "");
        progress.InstallModule(SessionExtractionConstants.StarterHighSlotId, SessionExtractionConstants.StarterGasHarvesterModuleId);
        bool selectedGasReadyWithFitting = loadedMeta.CanBeginSelectedSessionSortie(out string gasReadyReason);
        progress.InstallModule(SessionExtractionConstants.StarterHighSlotId, "");
        report.Check(selectedGasBlockedWithoutFitting
            && selectedGasStillBlockedWithLegacyUtility
            && selectedGasReadyWithFitting,
            "Selected gas sortie is gated by a fitted High gas harvester, not legacy utility: " + gasBlockedReason + " / " + legacyUtilityReason + " / " + gasReadyReason);

        bool coreInstallLegacyUtilityBlocked = !loadedMeta.InstallModule(SessionExtractionConstants.LegacyUtilitySlotId, SessionExtractionConstants.StarterGasHarvesterModuleId);
        bool coreInstallHighIntoLowBlocked = !loadedMeta.InstallModule(SessionExtractionConstants.StarterLowSlotId, SessionExtractionConstants.StarterGasHarvesterModuleId);
        bool coreInstallValidHighAllowed = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, SessionExtractionConstants.StarterGasHarvesterModuleId);
        bool coreInstallValidHighCleared = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, "");
        report.Check(coreInstallLegacyUtilityBlocked
            && coreInstallHighIntoLowBlocked
            && coreInstallValidHighAllowed
            && coreInstallValidHighCleared
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.LegacyUtilitySlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterLowSlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId)),
            "Core public fitting API rejects legacy utility and wrong-band installs while allowing valid High-slot modules.");

        bool highInstalledBeforeHullSelect = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, SessionExtractionConstants.StarterGasHarvesterModuleId);
        string hullBeforeLegacySelect = progress.selectedHullId;
        bool coreSelectHullBlocked = !loadedMeta.SelectHull("removed_legacy_hull");
        bool moduleKeptAfterBlockedHullSelect = progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId) == SessionExtractionConstants.StarterGasHarvesterModuleId;
        bool highClearedAfterHullSelectCheck = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, "");
        report.Check(highInstalledBeforeHullSelect
            && coreSelectHullBlocked
            && progress.selectedHullId == hullBeforeLegacySelect
            && moduleKeptAfterBlockedHullSelect
            && highClearedAfterHullSelectCheck
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId)),
            "Core public hull selector is blocked so ship replacement stays under base assembly control.");

        int cargoBeforeRuntimeNoSortie = progress.GetShipCargoAmount("windshale_ore");
        bool runtimeCargoWithoutSortieBlocked = !loadedMeta.TryAddShipCargoFromRuntime("windshale_ore", 1, out string runtimeCargoBlockReason);
        report.Check(runtimeCargoWithoutSortieBlocked
            && progress.GetShipCargoAmount("windshale_ore") == cargoBeforeRuntimeNoSortie,
            "Core runtime cargo collection is blocked outside active sorties: " + runtimeCargoBlockReason);

        progress.activeExpedition ??= new FlagshipExpeditionState();
        progress.activeExpedition.active = true;
        progress.activeExpedition.expeditionId = "legacy_core_direct_return";
        progress.activeExpedition.returnDockId = "legacy_return_dock";
        progress.activeExpedition.startedUtcTicks = DateTime.UtcNow.Ticks;
        GameSessionMode modeBeforeLegacyFlagshipReturn = loadedMeta.CurrentMode;
        string dockBeforeLegacyFlagshipReturn = progress.currentDockId;
        bool legacyFlagshipReturnBlocked = !loadedMeta.ReturnFromFlagshipExpedition();
        report.Check(legacyFlagshipReturnBlocked
            && progress.activeExpedition != null
            && !progress.activeExpedition.active
            && loadedMeta.CurrentMode == modeBeforeLegacyFlagshipReturn
            && progress.currentDockId == dockBeforeLegacyFlagshipReturn,
            "Core public flagship expedition return is blocked and stale expedition state is cleared.");

        GameSessionMode modeBeforeLegacyDock = loadedMeta.CurrentMode;
        string dockBeforeLegacyDock = progress.currentDockId;
        bool legacyDockOutsideSortieBlocked = !loadedMeta.DockAt("Island1", DockingLocationKind.Island);
        bool baseDockStillAllowed = loadedMeta.DockAt(capitalId, DockingLocationKind.Island);
        report.Check(legacyDockOutsideSortieBlocked
            && baseDockStillAllowed
            && loadedMeta.CurrentMode == modeBeforeLegacyDock
            && progress.currentDockId == capitalId
            && dockBeforeLegacyDock == capitalId,
            "Core public DockAt allows only the base dock outside active sorties.");

        progress.activeExpedition ??= new FlagshipExpeditionState();
        progress.activeExpedition.active = true;
        progress.activeExpedition.expeditionId = "legacy_core_tail";
        progress.activeExpedition.startedUtcTicks = DateTime.UtcNow.Ticks;
        loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeOreSortieId, out _);

        bool sortieStarted = loadedHud != null ? loadedHud.TryTakeOff() : loadedMeta.BeginSafeOreSortie();
        report.Check(sortieStarted
            && loadedMeta.HasActiveSortie
            && progress.currentMode == GameSessionMode.Flight,
            "HUD starts a safe ore sortie from the base and switches to flight.");
        report.Check(sortieStarted
            && progress.activeExpedition != null
            && !progress.activeExpedition.active,
            "Core sortie start clears legacy flagship expedition state and does not auto-start old expedition gameplay.");
        if (!sortieStarted || progress.activeSortie == null || progress.activeSortie.zone == null)
        {
            return;
        }

        SortieZoneDefinition zone = progress.activeSortie.zone;
        ShipPhysics entryShip = loadedMeta != null && loadedMeta.shipLoader != null
            ? loadedMeta.shipLoader.targetShip
            : null;
        if (entryShip == null && loadedSession != null && loadedSession.PlayerShipRoot != null)
        {
            entryShip = loadedSession.PlayerShipRoot.GetComponentInChildren<ShipPhysics>();
        }

        Rigidbody entryBody = entryShip != null ? entryShip.GetComponent<Rigidbody>() : null;
        Vector3 entryPosition = entryShip != null ? entryShip.transform.position : progress.currentFlightPosition;
        Vector3 centerToEntry = new Vector3(
            entryPosition.x - zone.centerPosition.x,
            0f,
            entryPosition.z - zone.centerPosition.z);
        Vector3 entryVelocity = entryBody != null ? entryBody.linearVelocity : Vector3.zero;
        Vector3 horizontalEntryVelocity = new Vector3(entryVelocity.x, 0f, entryVelocity.z);
        float entryDistance = centerToEntry.magnitude;
        float entrySpeed = horizontalEntryVelocity.magnitude;
        Vector3 toCenter = centerToEntry.sqrMagnitude > 0.001f ? -centerToEntry.normalized : Vector3.zero;
        float inwardVelocityDot = entrySpeed > 0.001f ? Vector3.Dot(horizontalEntryVelocity / entrySpeed, toCenter) : -1f;
        float inwardFacingDot = entryShip != null && centerToEntry.sqrMagnitude > 0.001f
            ? Vector3.Dot(new Vector3(entryShip.transform.forward.x, 0f, entryShip.transform.forward.z).normalized, toCenter)
            : -1f;
        float expectedEntrySpeed = entryShip != null ? entryShip.EstimateFullSlipstreamCruiseSpeedMS() : 0f;
        float expectedEntryOffset = expectedEntrySpeed * 20f;
        WildWindFlightControlBridge entryControls = FindFirstObjectByType<WildWindFlightControlBridge>();
        bool sortieEntryApproachReady = entryShip != null
            && entryBody != null
            && entryDistance > zone.radiusMeters
            && Mathf.Abs((entryDistance - zone.radiusMeters) - expectedEntryOffset) <= Mathf.Max(3f, expectedEntryOffset * 0.02f)
            && Approximately(entrySpeed, expectedEntrySpeed, Mathf.Max(0.5f, expectedEntrySpeed * 0.01f))
            && inwardVelocityDot >= 0.99f
            && inwardFacingDot >= 0.99f
            && entryShip.claudiumSlipstreamEnabled
            && Approximately(entryShip.ClaudiumSlipstreamCharge01, 1f, 0.001f)
            && entryControls != null
            && entryControls.ManualThrustNotch == 5
            && !entryControls.HeadingAssistEnabled
            && !entryShip.headingHold
            && !entryShip.altitudeHold;
        report.Check(sortieEntryApproachReady,
            "Sortie entry starts 20 seconds outside the zone edge at the sustainable full-slipstream max speed, full claudium slipstream, and no autopilot handoff.");

        int wrongResourceBeforeRuntimeActiveSortie = progress.GetShipCargoAmount(SessionExtractionConstants.StarterAirframeKitItemId);
        bool runtimeWrongResourceDuringSortieBlocked = !loadedMeta.TryAddShipCargoFromRuntime(
            SessionExtractionConstants.StarterAirframeKitItemId,
            1,
            out string runtimeWrongResourceReason);
        report.Check(runtimeWrongResourceDuringSortieBlocked
            && progress.GetShipCargoAmount(SessionExtractionConstants.StarterAirframeKitItemId) == wrongResourceBeforeRuntimeActiveSortie,
            "Core runtime cargo collection rejects resources outside the active sortie catalog: " + runtimeWrongResourceReason);

        int cargoBeforeRuntimeActiveSortie = progress.GetShipCargoAmount("windshale_ore");
        bool runtimeCargoDuringSortieAllowed = loadedMeta.TryAddShipCargoFromRuntime("windshale_ore", 1, out string runtimeCargoActiveReason);
        report.Check(runtimeCargoDuringSortieAllowed
            && progress.GetShipCargoAmount("windshale_ore") == cargoBeforeRuntimeActiveSortie + 1,
            "Core runtime cargo collection is allowed only inside active sorties: " + runtimeCargoActiveReason);

        Vector3 boundaryPosition = new Vector3(
            zone.centerPosition.x + zone.radiusMeters + Mathf.Max(5f, zone.extractionBoundaryToleranceMeters * 0.1f),
            Mathf.Max(zone.entryPosition.y, zone.stormFloorY + 50f),
            zone.centerPosition.z);
        if (loadedSession != null && loadedSession.PlayerShipRoot != null)
        {
            loadedSession.PlayerShipRoot.SetPositionAndRotation(boundaryPosition, Quaternion.identity);
            Rigidbody body = loadedSession.PlayerShipRoot.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                body.position = boundaryPosition;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        progress.SetFlightPose(boundaryPosition, Quaternion.identity);
        progress.AddShipCargo("windshale_ore", 25);
        IslandProductionState baseStorageBeforeExtraction = loadedMeta.GetCapitalStorageState();
        int baseOreBeforeExtraction = baseStorageBeforeExtraction != null ? baseStorageBeforeExtraction.GetResourceAmount("windshale_ore") : 0;
        int sortieOreBeforeExtraction = progress.GetShipCargoAmount("windshale_ore");

        progress.shipEngineFuelTank.SetResource("charcoal");
        progress.shipClaudiumTank.SetResource("claudium");
        progress.shipEngineFuelTank.TrySpend("charcoal", progress.shipEngineFuelTank.GetAmount("charcoal"));
        progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
        SyncTestShipConsumables(loadedSession, progress);
        SortieReturnEstimate estimateWithoutReserves = loadedMeta.GetActiveSortieReturnEstimate();
        bool extractedWithoutReserves = loadedMeta.TryExtractActiveSortie(out string insufficientReserveMessage);
        report.Check(!estimateWithoutReserves.canExtract
            && estimateWithoutReserves.isNearBoundary
            && (!estimateWithoutReserves.hasEnoughCoal || !estimateWithoutReserves.hasEnoughClaudium)
            && !extractedWithoutReserves
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Boundary extraction is blocked when coal or claudium reserves are insufficient: " + insufficientReserveMessage);

        progress.shipEngineFuelTank.Add("charcoal", 42.7f, 100000f);
        progress.shipClaudiumTank.Add("claudium", 10.1f, 100000f);
        SyncTestShipConsumables(loadedSession, progress);
        Vector3 screenshotReserveOutward = new Vector3(
            boundaryPosition.x - zone.centerPosition.x,
            0f,
            boundaryPosition.z - zone.centerPosition.z).normalized;
        SortieReturnEstimate screenshotReserveEstimate = loadedMeta.GetActiveSortieReturnEstimate();
        loadedMeta.RecordActiveSortieExtractionRunup(
            boundaryPosition,
            screenshotReserveOutward * 48f,
            screenshotReserveOutward,
            0.25f,
            true);
        bool screenshotReserveStartsTimer = progress.activeSortie.extractionRunupSeconds > 0f
            && loadedMeta.ActiveSortieExtractionRunupStatus.Contains("Slip charging");
        report.Check(screenshotReserveEstimate.hasEnoughCoal
            && screenshotReserveEstimate.hasEnoughClaudium
            && screenshotReserveStartsTimer,
            "Safe ore return reserves leave enough margin for a real boundary arrival and start the slip timer instead of blocking at 0/12.");

        progress.activeSortie.ResetExtractionRunup();
        progress.shipEngineFuelTank.TrySpend("charcoal", progress.shipEngineFuelTank.GetAmount("charcoal"));
        progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));

        progress.shipEngineFuelTank.Add("charcoal", estimateWithoutReserves.requiredCoalKg + 50f, 100000f);
        progress.shipClaudiumTank.Add("claudium", estimateWithoutReserves.requiredClaudiumKg + 50f, 100000f);
        SyncTestShipConsumables(loadedSession, progress);
        Vector3 stormBoundaryPosition = new Vector3(
            boundaryPosition.x,
            zone.stormFloorY - 10f,
            boundaryPosition.z);
        MoveSessionShip(loadedSession, progress, stormBoundaryPosition);
        SortieReturnEstimate estimateInStorm = loadedMeta.GetActiveSortieReturnEstimate();
        bool extractedFromStorm = loadedMeta.TryExtractActiveSortie(out string stormBlockMessage);
        report.Check(!estimateInStorm.canExtract
            && estimateInStorm.isNearBoundary
            && !estimateInStorm.isAboveStorm
            && estimateInStorm.hasEnoughCoal
            && estimateInStorm.hasEnoughClaudium
            && !extractedFromStorm
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Boundary extraction is blocked inside the storm layer even with enough return reserves: " + stormBlockMessage);

        Vector3 centerPosition = new Vector3(
            zone.centerPosition.x,
            Mathf.Max(zone.entryPosition.y, zone.stormFloorY + 50f),
            zone.centerPosition.z);
        if (loadedSession != null && loadedSession.PlayerShipRoot != null)
        {
            loadedSession.PlayerShipRoot.SetPositionAndRotation(centerPosition, Quaternion.identity);
            Rigidbody body = loadedSession.PlayerShipRoot.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                body.position = centerPosition;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        progress.SetFlightPose(centerPosition, Quaternion.identity);
        SortieReturnEstimate estimateAwayFromBoundary = loadedMeta.GetActiveSortieReturnEstimate();
        bool extractedAwayFromBoundary = loadedMeta.TryExtractActiveSortie(out string boundaryBlockMessage);
        report.Check(!estimateAwayFromBoundary.canExtract
            && !estimateAwayFromBoundary.isNearBoundary
            && !extractedAwayFromBoundary
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Extraction is blocked until the ship leaves the sortie cylinder: " + boundaryBlockMessage);

        bool legacyDockDuringSortie = loadedMeta.DockAt(capitalId, DockingLocationKind.Island);
        report.Check(!legacyDockDuringSortie
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Core sortie cannot be ended through legacy DockAt; the Extract home action remains required.");

        if (loadedSession != null && loadedSession.PlayerShipRoot != null)
        {
            loadedSession.PlayerShipRoot.SetPositionAndRotation(boundaryPosition, Quaternion.identity);
            Rigidbody body = loadedSession.PlayerShipRoot.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                body.position = boundaryPosition;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        progress.SetFlightPose(boundaryPosition, Quaternion.identity);
        SortieReturnEstimate estimateBeforeRunup = loadedMeta.GetActiveSortieReturnEstimate();
        bool extractedBeforeRunup = loadedMeta.TryExtractActiveSortie(out string runupBlockMessage);
        report.Check(!estimateBeforeRunup.canExtract
            && estimateBeforeRunup.isNearBoundary
            && estimateBeforeRunup.isAboveStorm
            && estimateBeforeRunup.hasEnoughCoal
            && estimateBeforeRunup.hasEnoughClaudium
            && !estimateBeforeRunup.hasExtractionRunup
            && !extractedBeforeRunup
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Boundary extraction requires 12 seconds of claudium slipstream movement toward base before the march return: " + runupBlockMessage);

        Vector3 outwardForForwardRunup = new Vector3(
            boundaryPosition.x - zone.centerPosition.x,
            0f,
            boundaryPosition.z - zone.centerPosition.z).normalized;
        bool forwardAimedRunup = loadedMeta.RecordActiveSortieExtractionRunup(
            boundaryPosition,
            Vector3.Cross(Vector3.up, outwardForForwardRunup).normalized * 20f,
            outwardForForwardRunup,
            zone.extractionRunupRequiredSeconds + 0.25f,
            true);
        SortieReturnEstimate forwardAimedEstimate = loadedMeta.GetActiveSortieReturnEstimate();
        report.Check(forwardAimedRunup
            && forwardAimedEstimate.hasExtractionRunup
            && loadedMeta.HasActiveSortie
            && loadedMeta.ActiveSortieExtractionRunupStatus.Contains("Slip ready"),
            "Claudium slipstream exit timer accepts a ship aimed at the base marker, but waits for the Extract home button instead of auto-teleporting.");
        progress.activeSortie.ResetExtractionRunup();

        if (loadedSession != null && loadedSession.PlayerShipRoot != null)
        {
            loadedSession.PlayerShipRoot.SetPositionAndRotation(
                boundaryPosition,
                Quaternion.LookRotation(outwardForForwardRunup, Vector3.up));
            Rigidbody body = loadedSession.PlayerShipRoot.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                body.position = boundaryPosition;
                body.rotation = Quaternion.LookRotation(outwardForForwardRunup, Vector3.up);
                body.linearVelocity = Vector3.Cross(Vector3.up, outwardForForwardRunup).normalized * 20f;
                body.angularVelocity = Vector3.zero;
            }
        }

        ShipPhysics loadedShipForRunup = loadedMeta.shipLoader != null ? loadedMeta.shipLoader.targetShip : null;
        if (loadedShipForRunup != null)
        {
            loadedShipForRunup.claudiumSlipstreamEnabled = true;
            loadedShipForRunup.claudiumSlipstreamCharge01 = 1f;
        }

        progress.SetFlightPose(boundaryPosition, Quaternion.LookRotation(outwardForForwardRunup, Vector3.up));
        bool metaRunupTickerInvoked = TryInvokePrivateMethod(loadedMeta, "UpdateActiveSortieExtractionRunupFromActiveShip", report);
        SortieReturnEstimate metaRunupTickerEstimate = loadedMeta.GetActiveSortieReturnEstimate();
        report.Check(metaRunupTickerInvoked
            && metaRunupTickerEstimate.extractionRunupSeconds > 0f
            && loadedMeta.ActiveSortieExtractionRunupStatus.Contains("Slip charging"),
            "MetaGameState owns the live claudium slipstream exit timer instead of relying on the visual boundary controller.");
        progress.activeSortie.ResetExtractionRunup();

        PrimeActiveSortieExtractionRunup(loadedMeta, progress);
        SortieReturnEstimate estimateReady = loadedMeta.GetActiveSortieReturnEstimate();
        report.Check(estimateReady.canExtract
            && estimateReady.hasExtractionRunup
            && estimateReady.requiredCoalKg > 0f
            && estimateReady.requiredClaudiumKg >= 0f,
            "Boundary extraction calculates return coal and claudium reserves after the claudium slipstream exit run.");
        float coalBeforeExtraction = progress.shipEngineFuelTank.GetAmount("charcoal");
        float claudiumBeforeExtraction = progress.shipClaudiumTank.GetAmount("claudium");

        bool extracted = loadedHud != null ? loadedHud.TryDockNearest() : loadedMeta.TryExtractActiveSortie(out _);
        string extractionMessage = loadedHud != null ? "HUD extraction action" : "Meta extraction action";
        IslandProductionState baseStorage = loadedMeta.GetCapitalStorageState();
        report.Check(extracted
            && loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.currentDockId == capitalId
            && !loadedMeta.HasActiveSortie,
            "HUD extraction returns home to base: " + extractionMessage);
        report.Check(extracted
            && Approximately(
                progress.shipEngineFuelTank.GetAmount("charcoal"),
                Mathf.Max(0f, coalBeforeExtraction - estimateReady.requiredCoalKg),
                0.001f)
            && Approximately(
                progress.shipClaudiumTank.GetAmount("claudium"),
                Mathf.Max(0f, claudiumBeforeExtraction - estimateReady.requiredClaudiumKg),
                0.001f),
            "Boundary extraction consumes the calculated coal and claudium return reserves.");
        report.Check(baseStorage != null && baseStorage.GetResourceAmount("windshale_ore") >= baseOreBeforeExtraction + sortieOreBeforeExtraction,
            "Extracted sortie ore is stored at the base.");
        if (baseStorage == null)
        {
            return;
        }

        int ferronBefore = baseStorage.GetResourceAmount("ferron");
        int silvateBefore = baseStorage.GetResourceAmount("silvate");
        bool oreProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.Ore, out string processingMessage);
        report.Check(oreProcessed
            && baseStorage.GetResourceAmount("ferron") > ferronBefore
            && baseStorage.GetResourceAmount("silvate") > silvateBefore,
            "Base ore processing converts extracted ore into minerals: " + processingMessage);

        baseStorage.AddResource("cloud_condensate", 24);
        baseStorage.AddResource(SessionExtractionConstants.BrokenAutomatonItemId, 12);
        baseStorage.AddResource("windcalf_carcass", 24);
        baseStorage.AddResource(SessionExtractionConstants.RockInfoItemId, 10);

        int waterBefore = baseStorage.GetResourceAmount("water");
        bool gasProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.Gas, out string gasMessage);
        report.Check(gasProcessed
            && baseStorage.GetResourceAmount("water") > waterBefore,
            "Base gas processing cracks condensate into gas materials: " + gasMessage);

        int automatonCoreBefore = baseStorage.GetResourceAmount(SessionExtractionConstants.AutomatonCoreItemId);
        bool automatonProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.AutomatonDismantling, out string automatonMessage);
        report.Check(automatonProcessed
            && baseStorage.GetResourceAmount(SessionExtractionConstants.AutomatonCoreItemId) > automatonCoreBefore,
            "Base automaton dismantling turns salvage into cores and shop materials: " + automatonMessage);

        int leviathanFatBefore = baseStorage.GetResourceAmount(SessionExtractionConstants.LeviathanFatItemId);
        bool leviathanProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.LeviathanProcessing, out string leviathanMessage);
        report.Check(leviathanProcessed
            && baseStorage.GetResourceAmount(SessionExtractionConstants.LeviathanFatItemId) > leviathanFatBefore,
            "Base leviathan processing butchers carcasses into biological materials: " + leviathanMessage);

        int fundamentalBefore = baseStorage.GetResourceAmount(SessionExtractionConstants.FundamentalExperienceItemId);
        bool cyberProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.CyberneticDeciphering, out string cyberMessage);
        report.Check(cyberProcessed
            && baseStorage.GetResourceAmount(SessionExtractionConstants.FundamentalExperienceItemId) > fundamentalBefore,
            "Base cybernetic deciphering turns survey data into research output: " + cyberMessage);

        bool allProcessingBranchesTracked = true;
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingLineState line = progress.baseIndustry.GetProcessing(SessionExtractionIndustry.ProcessingBranches[i]);
            allProcessingBranchesTracked &= line != null && line.totalProcessedUnits > 0f;
        }

        report.Check(allProcessingBranchesTracked,
            "All five base processing branches record throughput in the extraction core.");

        baseStorage.AddResource("ferron", 60);
        baseStorage.AddResource("silvate", 20);
        baseStorage.AddResource("charcoal", 20);
        BaseProcessingLineState oreProcessingLine = progress.baseIndustry.GetProcessing(BaseProcessingBranch.Ore);
        int oreProcessingLevelBefore = oreProcessingLine.level;
        float oreProcessingCapacityBefore = oreProcessingLine.capacityUnitsPerMinute;
        bool oreProcessingUpgraded = loadedMeta.TryUpgradeBaseProcessingBranch(BaseProcessingBranch.Ore, out string oreProcessingUpgradeMessage);
        CascadeProductionLineState assemblyLine = progress.baseIndustry.GetProduction(CascadeProductionType.Assembly);
        int assemblyLevelBefore = assemblyLine.level;
        float assemblyCapacityBefore = assemblyLine.capacityUnitsPerMinute;
        bool assemblyUpgraded = loadedMeta.TryUpgradeCascadeProductionType(CascadeProductionType.Assembly, out string assemblyUpgradeMessage);
        string nextUpgradeOverview = loadedMeta.GetNextBaseIndustryUpgradeOverviewText();
        report.Check(oreProcessingUpgraded
            && assemblyUpgraded
            && oreProcessingLine.level == oreProcessingLevelBefore + 1
            && oreProcessingLine.capacityUnitsPerMinute > oreProcessingCapacityBefore
            && assemblyLine.level == assemblyLevelBefore + 1
            && assemblyLine.capacityUnitsPerMinute > assemblyCapacityBefore
            && nextUpgradeOverview.Contains("Base upgrade"),
            "Base progression upgrades processing and cascade line levels from processed resources: "
            + oreProcessingUpgradeMessage + " / " + assemblyUpgradeMessage);

        baseStorage.AddResource("charcoal", 20);
        List<CascadeProductionOrderDefinition> starterOrders = loadedMeta.CreateStarterCascadeOrders();
        bool starterOrdersValid = starterOrders.Count == 3
            && config.GetItem(SessionExtractionConstants.StarterAirframeKitItemId) != null
            && config.GetItem(SessionExtractionConstants.StarterModuleKitItemId) != null
            && config.GetItem(SessionExtractionConstants.StarterMunitionBundleItemId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterHarpoonModuleId) != null;
        report.Check(starterOrdersValid,
            "Starter cascade catalog includes airframe, module and munition outputs backed by config, with no extra ship output.");

        CascadeProductionOrderDefinition rogueCascadeOrder = new CascadeProductionOrderDefinition
        {
            orderId = "legacy_core_rogue_free_output",
            displayName = "Legacy core rogue free output",
            outputs = new List<CascadeItemAmount>
            {
                new CascadeItemAmount
                {
                    itemId = SessionExtractionConstants.StarterAirframeKitItemId,
                    amount = 99
                }
            },
            loads = new List<CascadeProductionLoad>
            {
                new CascadeProductionLoad
                {
                    type = CascadeProductionType.Assembly,
                    loadUnits = 1f
                }
            }
        };
        int airframeKitsBeforeRogueOrder = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterAirframeKitItemId);
        CascadeProductionEstimate rogueEstimate = loadedMeta.EstimateBaseCascadeOrder(rogueCascadeOrder);
        bool rogueCascadeRan = loadedMeta.TryRunBaseCascadeOrder(rogueCascadeOrder, out string rogueCascadeMessage);
        report.Check(!rogueEstimate.canRun
            && !rogueCascadeRan
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterAirframeKitItemId) == airframeKitsBeforeRogueOrder,
            "Core cascade production rejects ad hoc public orders outside the base catalog: " + rogueCascadeMessage);

        CascadeProductionEstimate starterEstimate = loadedMeta.EstimateStarterAirframeCascade();
        report.Check(starterEstimate.canRun
            && starterEstimate.totalLoadUnits > 0f
            && starterEstimate.bottleneckMinutes > 0f,
            "Starter cascade order estimates load and bottleneck across production capacity.");

        bool cascadeComplete = loadedMeta.TryRunStarterAirframeCascade(out string cascadeMessage);
        report.Check(cascadeComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterAirframeKitItemId) >= 1,
            "Eight-type cascade production creates the starter airframe kit: " + cascadeMessage);

        bool allCascadeLinesLoaded = true;
        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            CascadeProductionLineState line = progress.baseIndustry.GetProduction(SessionExtractionIndustry.CascadeProductionTypes[i]);
            allCascadeLinesLoaded &= line != null && line.totalLoadApplied > 0f;
        }

        report.Check(allCascadeLinesLoaded,
            "Starter cascade order records load against all eight production types.");

        CascadeProductionEstimate moduleEstimate = loadedMeta.EstimateNextBaseCascadeOrder(out CascadeProductionOrderDefinition moduleOrder);
        bool moduleCascadeComplete = loadedMeta.TryRunNextBaseCascadeOrder(out string moduleCascadeMessage);
        CascadeProductionEstimate munitionEstimate = loadedMeta.EstimateNextBaseCascadeOrder(out CascadeProductionOrderDefinition munitionOrder);
        bool munitionCascadeComplete = loadedMeta.TryRunNextBaseCascadeOrder(out string munitionCascadeMessage);
        report.Check(moduleOrder != null
            && moduleOrder.orderId == SessionExtractionConstants.StarterModuleKitOrderId
            && moduleEstimate.canRun
            && moduleCascadeComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) >= 1,
            "Cascade catalog can produce a starter module kit from early ore materials: " + moduleCascadeMessage);

        int moduleKitsBeforeHighUpgrade = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId);
        bool highGasUpgradeInstalled = loadedMeta.TryInstallStarterGasHarvesterUpgrade(out string highGasUpgradeMessage);
        bool selectedGasAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeGasSortieId, out _);
        bool selectedGasReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string gasAfterUpgradeReason);
        report.Check(highGasUpgradeInstalled
            && selectedGasAfterUpgrade
            && selectedGasReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId) == SessionExtractionConstants.StarterGasHarvesterModuleId
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) == moduleKitsBeforeHighUpgrade - 1,
            "Starter module kit installs the first High gas harvester and unlocks gas sorties: " + highGasUpgradeMessage + " / " + gasAfterUpgradeReason);

        SortieResourceCacheController resourceCacheController = FindFirstObjectByType<SortieResourceCacheController>();
        if (resourceCacheController == null)
        {
            GameObject cacheControllerObject = new GameObject("Big Test Sortie Resource Cache Controller");
            resourceCacheController = cacheControllerObject.AddComponent<SortieResourceCacheController>();
        }

        ValidateStarterResourceCacheSortie(
            report,
            loadedMeta,
            loadedSession,
            progress,
            resourceCacheController,
            SessionExtractionConstants.DefaultSafeGasSortieId,
            "cloud_condensate",
            "Gas");

        baseStorage.AddResource("ferron", 12);
        baseStorage.AddResource("silvate", 3);
        baseStorage.AddResource("charcoal", 6);

        bool extraModuleKitsComplete = true;
        string extraModuleKitMessage = "";
        for (int i = 0; i < 3; i++)
        {
            extraModuleKitsComplete &= loadedMeta.TryRunBaseCascadeOrder(SessionExtractionIndustry.CreateStarterModuleKitOrder(), out extraModuleKitMessage);
        }

        report.Check(extraModuleKitsComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) >= 3,
            "Starter module kit order can be repeated to fit all starter branch tools: " + extraModuleKitMessage);

        bool miningHoldInstalled = loadedMeta.TryInstallStarterMiningHoldUpgrade(out string miningHoldUpgradeMessage);
        bool selectedAutomatonAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeAutomatonSortieId, out _);
        bool selectedAutomatonReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string automatonAfterUpgradeReason);
        report.Check(miningHoldInstalled
            && selectedAutomatonAfterUpgrade
            && selectedAutomatonReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterSecondHighSlotId) == SessionExtractionConstants.StarterMiningHoldModuleId,
            "Starter module kit installs the High impact/salvage module and unlocks automaton sorties: " + miningHoldUpgradeMessage + " / " + automatonAfterUpgradeReason);

        bool harpoonInstalled = loadedMeta.TryInstallStarterHarpoonUpgrade(out string harpoonUpgradeMessage);
        bool selectedLeviathanAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeLeviathanSortieId, out _);
        bool selectedLeviathanReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string leviathanAfterUpgradeReason);
        report.Check(harpoonInstalled
            && selectedLeviathanAfterUpgrade
            && selectedLeviathanReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterThirdHighSlotId) == SessionExtractionConstants.StarterHarpoonModuleId,
            "Starter module kit installs the High harpoon module and unlocks leviathan sorties: " + harpoonUpgradeMessage + " / " + leviathanAfterUpgradeReason);

        bool observationInstalled = loadedMeta.TryInstallStarterObservationUpgrade(out string observationUpgradeMessage);
        bool selectedSurveyAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeSurveySortieId, out _);
        bool selectedSurveyReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string surveyAfterUpgradeReason);
        report.Check(observationInstalled
            && selectedSurveyAfterUpgrade
            && selectedSurveyReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterMidSlotId) == SessionExtractionConstants.StarterObservationPostModuleId,
            "Starter module kit installs the Mid observation module and unlocks survey sorties: " + observationUpgradeMessage + " / " + surveyAfterUpgradeReason);

        ValidateStarterResourceCacheSortie(
            report,
            loadedMeta,
            loadedSession,
            progress,
            resourceCacheController,
            SessionExtractionConstants.DefaultSafeAutomatonSortieId,
            SessionExtractionConstants.BrokenAutomatonItemId,
            "Automaton");
        ValidateStarterResourceCacheSortie(
            report,
            loadedMeta,
            loadedSession,
            progress,
            resourceCacheController,
            SessionExtractionConstants.DefaultSafeLeviathanSortieId,
            "windcalf_carcass",
            "Leviathan");
        ValidateStarterResourceCacheSortie(
            report,
            loadedMeta,
            loadedSession,
            progress,
            resourceCacheController,
            SessionExtractionConstants.DefaultSafeSurveySortieId,
            SessionExtractionConstants.RockInfoItemId,
            "Survey");

        report.Check(munitionOrder != null
            && munitionOrder.orderId == SessionExtractionConstants.StarterMunitionBundleOrderId
            && munitionEstimate.canRun
            && munitionCascadeComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId) >= 4,
            "Cascade catalog can produce starter munition bundles from leviathan shell and minerals: " + munitionCascadeMessage);

        int munitionBundlesBeforeLoadout = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId);
        int weaponBeforeLoadout = progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId);
        bool starterMunitionsReady = loadedMeta.CanLoadStarterMunitionsAtBase(out string starterMunitionsReadyMessage);
        bool starterMunitionsLoaded = loadedMeta.TryLoadStarterMunitionsAtBase(out string starterMunitionsLoadMessage);
        report.Check(starterMunitionsReady
            && starterMunitionsLoaded
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId) == munitionBundlesBeforeLoadout - 1
            && progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId)
                == weaponBeforeLoadout + SessionExtractionConstants.StarterWeaponUnitsPerMunitionBundle,
            "Starter munition bundles can be loaded at the base into weapon cargo for sortie weapons and harpoons: "
            + starterMunitionsReadyMessage + " / " + starterMunitionsLoadMessage);

        bool lowUpgradeInstalled = loadedMeta.TryInstallStarterCargoRackUpgrade(out string upgradeMessage);
        report.Check(lowUpgradeInstalled
            && progress.GetInstalledModule(SessionExtractionConstants.StarterLowSlotId) == SessionExtractionConstants.StarterCargoRackModuleId,
            "The first fitting upgrade consumes the kit and installs into a Low slot: " + upgradeMessage);

        bool fittedPioneerAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, loadedMeta.techTree, progress, out ShipAssemblyResult fittedPioneerAssembly);
        float fittedPioneerPayloadKg = loadedMeta.startingFuelKg + loadedMeta.startingClaudiumKg + 250f;
        string fittedPioneerFlightEnvelope = "assembly did not build.";
        bool fittedPioneerFlightEnvelopeOk = fittedPioneerAssemblyBuilt
            && fittedPioneerAssembly.hull != null
            && fittedPioneerAssembly.hull.partId == GameplaySessionSaveData.DefaultStarterHullId
            && HasStablePioneerFlightEnvelope(
                fittedPioneerAssembly,
                fittedPioneerPayloadKg,
                40f,
                0.5f,
                12f,
                out fittedPioneerFlightEnvelope);
        report.Check(fittedPioneerFlightEnvelopeOk,
            "Fully fitted Pioneer still has enough lift and engine reserve to fly instead of falling: "
            + fittedPioneerFlightEnvelope);

        string fittingSummary = loadedMeta.GetCoreFittingSummaryText();
        string fittingCompact = loadedMeta.GetCoreFittingCompactText();
        report.Check(fittingSummary.Contains("High:")
            && fittingSummary.Contains("Mid:")
            && fittingSummary.Contains("Low:")
            && fittingSummary.Contains("Rig:")
            && fittingCompact.Contains("H 3/3")
            && fittingCompact.Contains("M 1/2")
            && fittingCompact.Contains("L 1/2")
            && fittingCompact.Contains("R 0/1")
            && !fittingSummary.Contains(SessionExtractionConstants.LegacyUtilitySlotTypeId)
            && !fittingCompact.Contains(SessionExtractionConstants.LegacyUtilitySlotTypeId)
            && !loadedMeta.UsesLegacyDockAssemblyUi,
            "Core dock UI exposes High/Mid/Low/Rig fitting instead of legacy assembly/utility slots: " + fittingCompact + " / " + fittingSummary);

        bool assemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, loadedMeta.techTree, progress, out ShipAssemblyResult assembly);
        report.Check(assemblyBuilt
            && HasSlotType(assembly, SessionExtractionConstants.HighSlotTypeId)
            && HasSlotType(assembly, SessionExtractionConstants.MidSlotTypeId)
            && HasSlotType(assembly, SessionExtractionConstants.LowSlotTypeId)
            && HasSlotType(assembly, SessionExtractionConstants.RigSlotTypeId)
            && HasSlotId(assembly, SessionExtractionConstants.StarterSecondHighSlotId)
            && HasSlotId(assembly, SessionExtractionConstants.StarterThirdHighSlotId)
            && HasSlotId(assembly, SessionExtractionConstants.StarterMidSlotId)
            && !HasSlotType(assembly, SessionExtractionConstants.LegacyUtilitySlotTypeId)
            && !HasSlotId(assembly, SessionExtractionConstants.LegacyUtilitySlotId),
            "Current core ship assembly exposes multiple High slots plus Mid/Low/Rig fitting bands without legacy utility slots.");

        bool lossSortieStarted = loadedMeta.BeginSafeOreSortie();
        if (lossSortieStarted)
        {
            progress.AddShipCargo("windshale_ore", 9);
            progress.shipEngineFuelTank.SetResource("charcoal");
            progress.shipClaudiumTank.SetResource("claudium");
            progress.shipEngineFuelTank.TrySpend("charcoal", progress.shipEngineFuelTank.GetAmount("charcoal"));
            progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
            progress.shipEngineFuelTank.Add("charcoal", loadedMeta.startingFuelKg + 500f, loadedMeta.startingFuelKg + 500f);
            progress.shipClaudiumTank.Add("claudium", loadedMeta.startingClaudiumKg + 500f, loadedMeta.startingClaudiumKg + 500f);
            SyncTestShipConsumables(loadedSession, progress);
        }

        bool recoveredFromLoss = lossSortieStarted && loadedMeta.LoseActiveSortieShipAndReturnToBase("big test sortie loss");
        report.Check(recoveredFromLoss
            && loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.currentDockId == capitalId
            && !loadedMeta.HasActiveSortie
            && progress.GetShipCargoAmount("windshale_ore") == 0
            && progress.selectedHullId == GameplaySessionSaveData.DefaultStarterHullId
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterMidSlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterLowSlotId))
            && Approximately(progress.shipEngineFuelTank.GetAmount("charcoal"), loadedMeta.startingFuelKg, 0.001f)
            && Approximately(progress.shipClaudiumTank.GetAmount("claudium"), loadedMeta.startingClaudiumKg, 0.001f),
            "Sortie ship loss returns to base, deletes sortie loot, destroys fitted modules and old tank reserves, restores Pioneer hull, and grants only starter fuel.");

        baseStorage = loadedMeta.GetCapitalStorageState();
        if (baseStorage != null)
        {
            baseStorage.SetResourceAmount("charcoal", 0);
            baseStorage.SetResourceAmount("claudium", 0);
        }

        progress.ReplaceShipAssembly("missing_session_core_hull");
        progress.shipEngineFuelTank.TrySpend("charcoal", progress.shipEngineFuelTank.GetAmount("charcoal"));
        progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
        SyncTestShipConsumables(loadedSession, progress);
        bool pioneerFreeRefuelReady = loadedMeta.CanRefuelBaseShip(out string pioneerFreeRefuelReadyMessage);
        bool pioneerFreeRefueled = loadedMeta.TryRefuelBaseShip(out string pioneerFreeRefuelMessage);
        report.Check(pioneerFreeRefuelReady
            && pioneerFreeRefueled
            && baseStorage != null
            && baseStorage.GetResourceAmount("charcoal") == 0
            && baseStorage.GetResourceAmount("claudium") == 0
            && progress.selectedHullId == GameplaySessionSaveData.DefaultStarterHullId
            && progress.shipEngineFuelTank.GetAmount("charcoal") >= loadedMeta.startingFuelKg
            && progress.shipClaudiumTank.GetAmount("claudium") >= loadedMeta.startingClaudiumKg,
            "Pioneer fallback restores a missing hull and receives a free minimal refuel even when base coal and claudium are empty: "
            + pioneerFreeRefuelReadyMessage + " / " + pioneerFreeRefuelMessage);
    }

    private static Vector3 GetSortieBoundaryProbePosition(SortieZoneDefinition zone)
    {
        if (zone == null)
        {
            return Vector3.zero;
        }

        zone.Normalize();
        return new Vector3(
            zone.centerPosition.x + zone.radiusMeters + Mathf.Max(5f, zone.extractionBoundaryToleranceMeters * 0.1f),
            Mathf.Max(zone.entryPosition.y, zone.stormFloorY + 50f),
            zone.centerPosition.z);
    }

    private static void MoveSessionShip(WildWindGameplaySession session, PlayerProgress progress, Vector3 position)
    {
        if (session != null && session.PlayerShipRoot != null)
        {
            session.PlayerShipRoot.SetPositionAndRotation(position, Quaternion.identity);
            Rigidbody body = session.PlayerShipRoot.GetComponentInChildren<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = Quaternion.identity;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        progress?.SetFlightPose(position, Quaternion.identity);
    }

    private static DateTime GetFutureProcessTime(PlayerProgress progress, int seconds)
    {
        long nowTicks = DateTime.UtcNow.Ticks;
        long baseTicks = Math.Max(progress != null ? progress.lastProcessUtcTicks : 0L, nowTicks);
        return new DateTime(baseTicks + TimeSpan.FromSeconds(Mathf.Max(1, seconds)).Ticks, DateTimeKind.Utc);
    }

    private static void SyncTestShipConsumables(WildWindGameplaySession session, PlayerProgress progress)
    {
        if (progress == null)
        {
            return;
        }

        ShipPhysics ship = session != null && session.PlayerShipRoot != null
            ? session.PlayerShipRoot.GetComponentInChildren<ShipPhysics>()
            : FindFirstObjectByType<ShipPhysics>();
        if (ship == null)
        {
            return;
        }

        string fuelId = string.IsNullOrWhiteSpace(ship.engineFuelId) ? "charcoal" : ship.engineFuelId;
        string claudiumId = string.IsNullOrWhiteSpace(ship.claudiumResourceId) ? "claudium" : ship.claudiumResourceId;
        progress.shipEngineFuelTank.SetResource(fuelId);
        progress.shipClaudiumTank.SetResource(claudiumId);
        ship.engineFuelStockKg = progress.shipEngineFuelTank.GetAmount(fuelId);
        ship.claudiumStock = progress.shipClaudiumTank.GetAmount(claudiumId);
    }

    private static void PrimeActiveSortieExtractionRunup(MetaGameState meta, PlayerProgress progress)
    {
        if (meta == null || progress?.activeSortie == null || !progress.activeSortie.active)
        {
            return;
        }

        SortieZoneDefinition zone = progress.activeSortie.zone;
        if (zone == null)
        {
            return;
        }

        zone.Normalize();
        Vector3 position = progress.activeSortie.lastKnownPosition;
        if (position == Vector3.zero)
        {
            position = GetSortieBoundaryProbePosition(zone);
        }

        Vector3 outward = new Vector3(
            position.x - zone.centerPosition.x,
            0f,
            position.z - zone.centerPosition.z);
        outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.right;

        float speedLimit = Mathf.Max(1f, zone.returnCruiseSpeedMS);
        float speed = Mathf.Max(speedLimit, speedLimit * Mathf.Clamp01(zone.extractionRunupSpeedRatio) + 1f);
        meta.RecordActiveSortieExtractionRunup(
            position,
            outward * speed,
            outward,
            zone.extractionRunupRequiredSeconds + 0.25f,
            true);
    }

    private static void ValidateStarterResourceCacheSortie(
        BigTestReport report,
        MetaGameState meta,
        WildWindGameplaySession session,
        PlayerProgress progress,
        SortieResourceCacheController cacheController,
        string sortieId,
        string expectedItemId,
        string label)
    {
        if (report == null)
        {
            return;
        }

        bool selected = meta != null && meta.SelectSessionSortie(sortieId, out _);
        bool ready = selected && meta.CanBeginSelectedSessionSortie(out _);
        bool started = ready && meta.BeginSelectedSessionSortie();
        bool cachesSpawned = false;
        bool fragmentSpawned = false;
        bool cargoExtracted = false;

        IslandProductionState baseStorage = meta != null ? meta.GetCapitalStorageState() : null;
        int storedBefore = baseStorage != null ? baseStorage.GetResourceAmount(expectedItemId) : 0;

        if (started && meta.HasActiveSortie && progress?.activeSortie?.zone != null)
        {
            if (cacheController != null)
            {
                cacheController.metaGameState = meta;
                cacheController.RefreshNow();
                cachesSpawned = cacheController.ActiveCacheCount > 0
                    && cacheController.ActiveResourceItemId == expectedItemId;
                fragmentSpawned = cacheController.TrySpawnImmediateFragmentForActiveSortie(out string fragmentItem)
                    && fragmentItem == expectedItemId
                    && MiningFragment.HasActiveItem(expectedItemId);
            }

            progress.AddShipCargo(expectedItemId, 8);
            MoveSessionShip(session, progress, GetSortieBoundaryProbePosition(progress.activeSortie.zone));

            SortieReturnEstimate estimate = meta.GetActiveSortieReturnEstimate();
            progress.shipEngineFuelTank.SetResource("charcoal");
            progress.shipClaudiumTank.SetResource("claudium");
            progress.shipEngineFuelTank.Add("charcoal", estimate.requiredCoalKg + 50f, 100000f);
            progress.shipClaudiumTank.Add("claudium", estimate.requiredClaudiumKg + 50f, 100000f);
            SyncTestShipConsumables(session, progress);
            PrimeActiveSortieExtractionRunup(meta, progress);
            cargoExtracted = meta.TryExtractActiveSortie(out _)
                && !meta.HasActiveSortie
                && meta.CurrentMode == GameSessionMode.Docked
                && baseStorage != null
                && baseStorage.GetResourceAmount(expectedItemId) >= storedBefore + 8;
        }

        report.Check(selected
            && ready
            && started
            && cachesSpawned
            && fragmentSpawned
            && cargoExtracted,
            label + " sortie uses the session resource cache controller, spawns collectible "
            + expectedItemId + " fragments, and extracts cargo home.");
    }

    private static int CountSocietyNeedStates(PlayerProgress progress)
    {
        if (progress == null || progress.islandProductions == null) return 0;

        int count = 0;
        for (int i = 0; i < progress.islandProductions.Count; i++)
        {
            IslandProductionState island = progress.islandProductions[i];
            if (island?.societyNeeds == null) continue;

            count += island.societyNeeds.Count;
        }

        return count;
    }

    private static bool HasSlotType(ShipAssemblyResult assembly, string slotTypeId)
    {
        if (assembly == null || assembly.slots == null || string.IsNullOrWhiteSpace(slotTypeId)) return false;

        for (int i = 0; i < assembly.slots.Count; i++)
        {
            ShipSlotDefinition slot = assembly.slots[i];
            if (slot != null && slot.slotTypeId == slotTypeId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSlotId(ShipAssemblyResult assembly, string slotId)
    {
        if (assembly == null || assembly.slots == null || string.IsNullOrWhiteSpace(slotId)) return false;

        for (int i = 0; i < assembly.slots.Count; i++)
        {
            ShipSlotDefinition slot = assembly.slots[i];
            if (slot != null && slot.slotId == slotId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStablePioneerFlightEnvelope(
        ShipAssemblyResult assembly,
        float payloadKg,
        float minPowerSurplusKw,
        float minHorizontalAccelerationMS2,
        float minSpeedMS,
        out string summary)
    {
        summary = "assembly missing.";
        if (assembly == null || assembly.stats == null)
        {
            return false;
        }

        ShipStatBlock stats = assembly.stats;
        float emptyMassKg = stats.Get(ShipStatId.BaseMass, 0f);
        float totalMassKg = emptyMassKg + Mathf.Max(0f, payloadKg);
        float enginePowerKw = stats.Get(ShipStatId.EngineMaxPower, 0f);
        float liftKgPerKw = stats.Get(ShipStatId.ClaudiumLiftEfficiency, 0f);
        float engineLiftKg = enginePowerKw * liftKgPerKw;
        float claudiumMaxLiftKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
        float hullLimitKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, 0f);
        float allowedTakeoffMassKg = Mathf.Min(engineLiftKg, Mathf.Min(claudiumMaxLiftKg, hullLimitKg));
        float hoverPowerKw = liftKgPerKw > 0f ? totalMassKg / liftKgPerKw : float.PositiveInfinity;
        float powerSurplusKw = enginePowerKw - hoverPowerKw;
        float propellerEfficiency = stats.Get(ShipStatId.PropellerEfficiency, 0f);
        float dragPerSpeedSquared = 0.5f
            * Mathf.Max(0f, stats.Get(ShipStatId.AirDensity, 1.225f))
            * Mathf.Max(0f, stats.Get(ShipStatId.DragCoefficient, 0f))
            * Mathf.Max(0f, stats.Get(ShipStatId.FrontalArea, 0f));
        float usefulPowerW = Mathf.Max(0f, powerSurplusKw) * Mathf.Clamp01(propellerEfficiency) * 1000f;
        float horizontalAccelerationMS2 = totalMassKg > 0f ? usefulPowerW / totalMassKg : 0f;
        float terminalSpeedMS = 0f;
        if (dragPerSpeedSquared > 0f && usefulPowerW > 0f)
        {
            terminalSpeedMS = Mathf.Pow(usefulPowerW / dragPerSpeedSquared, 1f / 3f);
        }

        summary = "mass " + totalMassKg.ToString("F0") + "/" + allowedTakeoffMassKg.ToString("F0") + " kg"
            + ", empty " + emptyMassKg.ToString("F0") + " kg"
            + ", hover " + hoverPowerKw.ToString("F0") + "/" + enginePowerKw.ToString("F0") + " kW"
            + ", surplus " + powerSurplusKw.ToString("F0") + " kW"
            + ", power accel " + horizontalAccelerationMS2.ToString("F2") + " m/s2"
            + ", terminal " + terminalSpeedMS.ToString("F0") + " m/s.";

        return totalMassKg <= allowedTakeoffMassKg + 0.001f
            && powerSurplusKw >= minPowerSurplusKw
            && propellerEfficiency > 0.1f
            && horizontalAccelerationMS2 >= minHorizontalAccelerationMS2
            && IsFinite(terminalSpeedMS)
            && terminalSpeedMS >= minSpeedMS;
    }

    private void ValidateGameplaySessionDockFlightCycle(BigTestReport report)
    {
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();

        report.Check(loadedSession != null && loadedSession.IsReady, "GameplaySession is ready before dock-flight-dock cycle.");
        report.Check(loadedMeta != null && loadedMeta.CurrentMode == GameSessionMode.Docked, "MetaGameState starts the session loop docked.");
        if (loadedSession == null || loadedMeta == null)
        {
            return;
        }

        bool flightStarted = loadedSession.TryBeginFreeFlight(out string flightReason);
        report.Check(flightStarted, "GameplaySession can start free flight: " + flightReason);
        report.Check(loadedSession.CurrentMode == GameSessionMode.Flight && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "GameplaySession and MetaGameState both switch to Flight.");

        bool docked = loadedSession.TryDockAtCurrentDock(out string dockReason);
        report.Check(docked, "GameplaySession can dock back at the current dock: " + dockReason);
        report.Check(loadedSession.CurrentMode == GameSessionMode.Docked && loadedMeta.CurrentMode == GameSessionMode.Docked,
            "GameplaySession and MetaGameState both return to Docked.");
    }

    private void ValidateStarterFoodDeliveryLoop(BigTestReport report)
    {
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        report.Check(loadedMeta != null, "Starter delivery has MetaGameState.");
        report.Check(loadedSession != null && loadedSession.IsReady, "Starter delivery has ready GameplaySession.");
        report.Check(loadedHud != null && loadedHud.IsReady, "Starter delivery HUD is present and ready.");
        if (loadedMeta == null || loadedMeta.progress == null || loadedSession == null || loadedHud == null)
        {
            return;
        }

        loadedMeta.EnsureProgressInitialized();
        PlayerProgress progress = loadedMeta.progress;
        WorldConfigDatabase config = loadedMeta.WorldConfig;
        IslandConfig source = config.GetIsland(WildWindStarterDelivery.SourceDockId);
        IslandConfig destination = config.GetIsland(WildWindStarterDelivery.DestinationDockId);
        report.Check(source != null, "Starter delivery source island exists in Island.csv.");
        report.Check(destination != null, "Starter delivery destination island exists in Island.csv.");
        if (source == null || destination == null)
        {
            return;
        }

        float distance = Vector3.Distance(source.position, destination.position);
        report.Check(Approximately(distance, WildWindStarterDelivery.ExpectedDestinationDistanceMeters, 1f),
            "Starter delivery destination is about 5.6 km from the farm: " + distance.ToString("0.#") + " m.");
        report.Check(progress.currentMode == GameSessionMode.Docked && progress.currentDockId == WildWindStarterDelivery.SourceDockId,
            "Starter delivery begins docked at the father's farm.");
        report.Check(WildWindStarterDelivery.GetCapitalFood(progress) >= WildWindStarterDelivery.DeliveryAmount,
            "Farm starts with food for the first delivery: " + WildWindStarterDelivery.GetCapitalFood(progress) + ".");
        report.Check(WildWindStarterDelivery.GetDestinationFood(progress) == 0,
            "Aerolite island starts without delivered food.");
        report.Check(loadedHud.IsDockedPanelVisible, "Starter delivery HUD shows docked/city controls at the farm.");

        int sourceBefore = WildWindStarterDelivery.GetCapitalFood(progress);
        bool loaded = loadedHud.TryLoadStarterFood();
        report.Check(loaded, "Starter delivery HUD loads food from the farm into the ship.");
        report.Check(WildWindStarterDelivery.GetShipFood(progress) == WildWindStarterDelivery.DeliveryAmount,
            "Ship cargo contains the delivery food.");
        report.Check(WildWindStarterDelivery.GetCapitalFood(progress) == sourceBefore - WildWindStarterDelivery.DeliveryAmount,
            "Farm stock decreases after loading food.");

        bool tookOff = loadedHud.TryTakeOff();
        report.Check(tookOff, "Starter delivery HUD starts flight from the farm.");
        report.Check(progress.currentMode == GameSessionMode.Flight && loadedHud.IsFlightPanelVisible,
            "Starter delivery switches to flight mode and HUD flight panel.");
        report.Check(!loadedHud.IsFlightControlsVisible, "Starter delivery flight HUD hides the obsolete direct movement button panel.");
        report.Check(loadedHud.IsFlightCompassVisible, "Starter delivery flight HUD shows the scrolling heading compass.");
        ValidateStarterFlightControls(report, loadedSession, destination.position);

        WorldDebugTravelController travelController = FindFirstObjectByType<WorldDebugTravelController>();
        Camera gameplayCamera = Camera.main;
        travelController?.ApplyCameraForTests(true);
        bool cameraTracksShip = travelController != null &&
            loadedSession.PlayerShipRoot != null &&
            travelController.CurrentMovementTarget == loadedSession.PlayerShipRoot &&
            gameplayCamera != null &&
            Vector3.Distance(gameplayCamera.transform.position, loadedSession.PlayerPosition) < 2500f;
        report.Check(cameraTracksShip,
            cameraTracksShip
                ? "Flight camera follows the visible player ship after takeoff."
                : "Flight camera is not bound to the player ship after takeoff.");
        if (travelController != null)
        {
            bool takeoffCameraClose = Mathf.Abs(travelController.CameraOrbitDistanceMeters - travelController.TakeoffCameraDistanceMeters) <= 0.25f;
            bool orbitPivotOnShip = loadedSession.PlayerShipRoot != null &&
                Vector3.Distance(travelController.LastCameraOrbitPivot, loadedSession.PlayerPosition) <= 0.25f;
            report.Check(takeoffCameraClose && orbitPivotOnShip,
                "Flight camera snaps to the configured ship-bound orbit distance after takeoff.");

            float yawBefore = travelController.CameraOrbitYawDegrees;
            float routeBefore = travelController.FlightCameraWheelRoute01;
            Vector3 cameraPositionBefore = gameplayCamera != null ? gameplayCamera.transform.position : Vector3.zero;
            travelController.AdjustCameraOrbitForTests(22f, -4f, 1f);
            bool orbitChanged = Mathf.Abs(Mathf.DeltaAngle(yawBefore, travelController.CameraOrbitYawDegrees)) > 10f &&
                travelController.FlightCameraWheelRoute01 > routeBefore &&
                gameplayCamera != null &&
                Vector3.Distance(gameplayCamera.transform.position, cameraPositionBefore) > 1f;
            report.Check(orbitChanged, "Flight camera supports orbit rotation and Warships wheel route movement.");

            Vector3 aimOffset = travelController.LastCameraAimTarget - travelController.LastCameraOrbitPivot;
            Vector3 horizontalAimOffset = Vector3.ProjectOnPlane(aimOffset, Vector3.up);
            bool cameraCenterAimsAhead = horizontalAimOffset.magnitude >= 50f;
            report.Check(cameraCenterAimsAhead,
                cameraCenterAimsAhead
                    ? "Flight camera centers the reticle ahead of the hull instead of on the ship body."
                    : "Flight camera reticle target is still too close to the ship body: " + FormatVector(aimOffset) + ".");

            ShipPhysics flightShip = loadedSession.PlayerShipRoot != null
                ? loadedSession.PlayerShipRoot.GetComponent<ShipPhysics>()
                : null;
            Vector3 elevatedGunAimTarget = Vector3.zero;
            bool elevatedGunAimSet = flightShip != null &&
                flightShip.SetManualGunSphereAimForTests(35f, 42f, out elevatedGunAimTarget);
            travelController.ApplyCameraForTests(true);
            float elevatedAimHeight = elevatedGunAimSet ? elevatedGunAimTarget.y - loadedSession.PlayerPosition.y : 0f;
            float cameraGunAimError = elevatedGunAimSet
                ? Vector3.Distance(travelController.LastCameraAimTarget, elevatedGunAimTarget)
                : float.PositiveInfinity;
            bool cameraFollowsElevatedGunAim = elevatedGunAimSet &&
                elevatedAimHeight >= 100f &&
                cameraGunAimError <= 0.5f;
            report.Check(cameraFollowsElevatedGunAim,
                cameraFollowsElevatedGunAim
                    ? "Flight camera center follows the elevated manual gun sphere aim point instead of a fixed plane."
                    : "Flight camera is not centered on elevated manual gun sphere aim: set=" + elevatedGunAimSet +
                      ", height=" + elevatedAimHeight.ToString("0.#") +
                      ", error=" + cameraGunAimError.ToString("0.###") + ".");

            bool cursorStateResolved = travelController.EvaluateGameplayCursorStateForTests(
                false,
                out CursorLockMode normalFlightCursorLock,
                out bool normalFlightCursorVisible);
            bool altCursorStateResolved = travelController.EvaluateGameplayCursorStateForTests(
                true,
                out CursorLockMode altFlightCursorLock,
                out bool altFlightCursorVisible);
            bool warshipsCursorMode =
                cursorStateResolved &&
                altCursorStateResolved &&
                normalFlightCursorLock == CursorLockMode.Locked &&
                !normalFlightCursorVisible &&
                altFlightCursorLock == CursorLockMode.None &&
                altFlightCursorVisible;
            report.Check(warshipsCursorMode,
                warshipsCursorMode
                    ? "Flight camera uses Warships-style locked mouse look, and Alt releases the cursor for UI."
                    : "Flight camera cursor mode is not Warships-style: normal=" + normalFlightCursorLock + "/" + normalFlightCursorVisible +
                      ", alt=" + altFlightCursorLock + "/" + altFlightCursorVisible + ".");

            travelController.ApplyCameraForTests(true);
            loadedHud.RefreshCompassForTests();
            bool compassTracksCamera = gameplayCamera != null &&
                Mathf.Abs(Mathf.DeltaAngle(loadedHud.LastCompassHeadingDegrees, gameplayCamera.transform.eulerAngles.y)) <= 1f;
            report.Check(compassTracksCamera, "Flight compass heading is bound to the camera view direction.");
        }

        Vector3 destinationDock = FindDockPositionOrConfigPosition(WildWindStarterDelivery.DestinationDockId, destination.position);
        bool dockedAtDestination = loadedSession.TryDockAt(
            WildWindStarterDelivery.DestinationDockId,
            DockingLocationKind.Island,
            destinationDock,
            out string dockReason);
        report.Check(dockedAtDestination, "Starter delivery can dock at the aerolite island: " + dockReason);
        report.Check(progress.currentMode == GameSessionMode.Docked && progress.currentDockId == WildWindStarterDelivery.DestinationDockId,
            "Starter delivery arrives docked at the aerolite island.");

        bool unloaded = loadedHud.TryUnloadStarterFood();
        report.Check(unloaded, "Starter delivery HUD unloads food at the aerolite island.");
        report.Check(WildWindStarterDelivery.GetShipFood(progress) == 0,
            "Ship cargo is empty after the first intro delivery.");
        IslandProductionState aeroliteStorage = progress.GetIslandProductionState(WildWindStarterDelivery.DestinationDockId, false);
        int deliveredFood = aeroliteStorage != null ? aeroliteStorage.GetResourceAmount(WildWindStarterDelivery.FoodItemId) : 0;
        report.Check(deliveredFood >= WildWindStarterDelivery.DeliveryAmount,
            "Aerolite island received the delivered food.");
        report.Check(progress.IsMissionCompleted(WildWindStarterDelivery.FirstMissionId),
            "First intro delivery mission is marked completed.");
        report.Check(WildWindStarterDelivery.GetActiveSourceDockId(progress) == WildWindStarterDelivery.DestinationDockId &&
            WildWindStarterDelivery.GetActiveDestinationDockId(progress) == "capital",
            "Second intro delivery activates from aerolite island to the capital.");

        int aeroliteBefore = WildWindStarterDelivery.GetSourceStock(progress);
        bool loadedAerolite = loadedHud.TryLoadStarterFood();
        report.Check(loadedAerolite, "Starter delivery HUD loads aerolite for the capital leg.");
        report.Check(WildWindStarterDelivery.GetShipCargo(progress) == WildWindStarterDelivery.GetActiveDeliveryAmount(progress),
            "Ship cargo contains the aerolite delivery.");
        report.Check(WildWindStarterDelivery.GetSourceStock(progress) == aeroliteBefore - WildWindStarterDelivery.GetActiveDeliveryAmount(progress),
            "Aerolite island stock decreases after loading aerolite.");

        bool tookOffToCapital = loadedHud.TryTakeOff();
        report.Check(tookOffToCapital, "Starter delivery HUD starts the second flight to the capital.");
        IslandConfig capital = config.GetIsland("capital");
        Vector3 capitalDock = capital != null ? FindDockPositionOrConfigPosition("capital", capital.position) : Vector3.zero;
        string capitalDockReason = "capital island missing";
        bool dockedAtCapital = capital != null && loadedSession.TryDockAt(
            "capital",
            DockingLocationKind.Island,
            capitalDock,
            out capitalDockReason);
        report.Check(dockedAtCapital, "Second intro delivery can dock at the capital: " + (dockedAtCapital ? "ok" : capitalDockReason));
        bool unloadedAerolite = loadedHud.TryUnloadStarterFood();
        report.Check(unloadedAerolite, "Starter delivery HUD unloads aerolite at the capital.");
        report.Check(WildWindStarterDelivery.IsCompleted(progress),
            "Intro delivery chain is marked completed after the capital leg.");
    }

    private void ValidateStarterFlightControls(BigTestReport report, WildWindGameplaySession loadedSession, Vector3 destinationPosition)
    {
        WildWindFlightControlBridge controls = FindFirstObjectByType<WildWindFlightControlBridge>();
        report.Check(controls != null && controls.IsReady, "Flight control bridge is present and bound to the player ship.");
        if (controls == null || loadedSession == null || loadedSession.PlayerShipRoot == null)
        {
            return;
        }

        ShipPhysics ship = controls.ControlledShip;
        report.Check(ship != null, "Flight control bridge exposes ShipPhysics for direct manual controls.");
        report.Check(controls.IsConnectedToGameplayShip, "Flight control bridge is connected to the visible gameplay ship, not a stray ShipPhysics.");
        if (ship == null)
        {
            return;
        }

        ship.engineAfterburnerEnabled = false;
        ship.engineCheatAfterburnerEnabled = false;
        controls.ToggleAfterburner();
        bool normalAfterburnerAddsTwentyPercent = ship.engineAfterburnerEnabled
            && !ship.engineCheatAfterburnerEnabled
            && Approximately(ship.EnginePowerLeverLimit, 1.2f, 0.001f)
            && Approximately(ship.EnginePowerCapacityKw, ship.enginePowerKwAt100 * 1.2f, 0.001f)
            && controls.StatusMessage.Contains("120");
        controls.ToggleCheatAfterburner();
        bool cheatAfterburnerAddsFiveHundredPercent = ship.engineAfterburnerEnabled
            && ship.engineCheatAfterburnerEnabled
            && Approximately(ship.EnginePowerLeverLimit, 6f, 0.001f)
            && Approximately(ship.EnginePowerCapacityKw, ship.enginePowerKwAt100 * 6f, 0.001f)
            && controls.StatusMessage.Contains("+500");
        controls.ToggleCheatAfterburner();
        bool cheatAfterburnerReturnsToNormalAfterburner = !ship.engineCheatAfterburnerEnabled
            && ship.engineAfterburnerEnabled
            && Approximately(ship.EnginePowerLeverLimit, 1.2f, 0.001f);
        controls.ToggleAfterburner();
        report.Check(normalAfterburnerAddsTwentyPercent
            && cheatAfterburnerAddsFiveHundredPercent
            && cheatAfterburnerReturnsToNormalAfterburner
            && !ship.engineAfterburnerEnabled
            && !ship.engineCheatAfterburnerEnabled
            && Approximately(ship.EnginePowerLeverLimit, 1f, 0.001f),
            "Normal afterburner stays at 120%, while the separate test cheat afterburner applies +500% and then returns to normal.");

        controls.ClearAllControlModes();
        controls.NudgeManualThrust(1f);
        controls.NudgeManualLift(1f);
        controls.NudgeManualTurn(-1f);
        controls.NudgeManualThrust(1f);
        controls.NudgeManualLift(1f);
        controls.NudgeManualTurn(-1f);
        controls.ApplyForTests();
        bool leverClickWritten = Approximately(ship.thrustInput, 0.4f, 0.001f) &&
            controls.ManualThrustNotch == 2 &&
            Approximately(ship.liftInput, 1f, 0.001f) &&
            !ship.altitudeHold &&
            Approximately(ship.turnInput, -0.5f, 0.001f);
        report.Check(leverClickWritten, "Manual HUD lever clicks latch into ShipPhysics, including direct lift instead of target altitude changes.");

        controls.ClearAllControlModes();
        controls.SetDirectInput(0.75f, 0.25f, -0.5f);
        controls.ApplyForTests();
        bool directControlWritten = Approximately(ship.thrustInput, 0.8f, 0.001f) &&
            controls.ManualThrustNotch == 4 &&
            Approximately(ship.sideInput, 0f, 0.001f) &&
            Approximately(ship.liftInput, 0.25f, 0.001f) &&
            Approximately(ship.turnInput, -0.5f, 0.001f) &&
            !ship.altitudeHold &&
            !ship.headingHold &&
            !ship.cruiseControl;
        report.Check(directControlWritten, "Direct flight controls write thrust, lift and turn without target altitude assist.");

        controls.ClearAllControlModes();
        controls.ApplyFlightInputForTests(new WildWindFlightInputState
        {
            thrust = 1f,
            lateral = -1f,
            lift = 1f,
            turn = -1f
        }, 0.2f);
        controls.ApplyForTests();
        bool keyboardManualWritten = Approximately(ship.thrustInput, 0.2f, 0.001f) &&
            controls.ManualThrustNotch == 1 &&
            Approximately(ship.sideInput, -1f, 0.001f) &&
            Approximately(ship.liftInput, 1f, 0.001f) &&
            !ship.altitudeHold &&
            Approximately(ship.turnInput, -1f, 0.001f);
        controls.ApplyFlightInputForTests(new WildWindFlightInputState { thrust = 1f }, 0.5f);
        controls.ApplyFlightInputForTests(new WildWindFlightInputState { thrust = 1f }, 0.3f);
        controls.ApplyForTests();
        bool keyboardHoldRepeats = Approximately(ship.thrustInput, 0.6f, 0.001f) &&
            controls.ManualThrustNotch == 3;
        controls.ApplyFlightInputForTests(new WildWindFlightInputState(), 0.2f);
        controls.ApplyForTests();
        bool keyboardReleaseKeepsThrottle = Approximately(ship.thrustInput, 0.6f, 0.001f) &&
            Approximately(ship.sideInput, 0f, 0.001f) &&
            Approximately(ship.liftInput, 0f, 0.001f) &&
            Approximately(ship.turnInput, 0f, 0.001f);

        controls.ClearAllControlModes();
        controls.ApplyFlightInputForTests(new WildWindFlightInputState { thrust = -1f }, 0.1f);
        controls.ApplyForTests();
        bool keyboardReverseFirstStep = Approximately(ship.thrustInput, -0.25f, 0.001f) &&
            controls.ManualThrustNotch == -1;
        controls.ApplyFlightInputForTests(new WildWindFlightInputState { thrust = -1f }, 0.5f);
        controls.ApplyFlightInputForTests(new WildWindFlightInputState { thrust = -1f }, 0.3f);
        controls.ApplyForTests();
        bool keyboardReverseFullStep = Approximately(ship.thrustInput, -1f, 0.001f) &&
            controls.ManualThrustNotch == -3;

        controls.ClearAllControlModes();
        Rigidbody controlledBody = ship.GetComponent<Rigidbody>();
        if (controlledBody != null)
        {
            controlledBody.linearVelocity = ship.transform.forward * 8f + ship.transform.right * 3f;
        }

        controls.ApplyForTests();
        bool zeroThrottleBrakeAssist = ship.neutralStopBrakeEnabled
            && !ship.cruiseControl
            && Approximately(ship.targetSpeedMS, 0f, 0.001f)
            && Approximately(ship.thrustInput, 0f, 0.001f)
            && Approximately(ship.sideInput, 0f, 0.001f);
        bool zeroThrottleBrakeNoBounce = false;
        if (controlledBody != null)
        {
            ship.cruiseControl = true;
            ship.targetSpeedMS = 0f;
            ship.neutralStopBrakeEnabled = false;
            ship.thrustInput = 0.8f;
            bool cruiseStopInvoked = TryInvokePrivateMethod(ship, "UpdateCruiseControl", report);
            bool cruiseStopUsesNeutralBrake = cruiseStopInvoked
                && ship.neutralStopBrakeEnabled
                && Approximately(ship.thrustInput, 0f, 0.001f);

            controlledBody.linearVelocity = ship.transform.forward * 0.2f;
            ship.neutralStopBrakeEnabled = true;
            ship.neutralStopBrakeMaxDecelerationMS2 = 8f;
            ship.neutralStopBrakeStopTimeSeconds = 0.75f;
            bool forwardBrakeInvoked = TryInvokePrivateMethod(ship, "ApplyNeutralStopBrake", report);
            Vector3 forwardAfter = Vector3.ProjectOnPlane(controlledBody.linearVelocity, Vector3.up);
            bool forwardBrakeDoesNotReverse = forwardBrakeInvoked
                && forwardAfter.magnitude < 0.2f
                && Vector3.Dot(forwardAfter, ship.transform.forward) >= -0.001f;

            controlledBody.linearVelocity = -ship.transform.forward * 0.2f;
            bool reverseBrakeInvoked = TryInvokePrivateMethod(ship, "ApplyNeutralStopBrake", report);
            Vector3 reverseAfter = Vector3.ProjectOnPlane(controlledBody.linearVelocity, Vector3.up);
            bool reverseBrakeDoesNotReverse = reverseBrakeInvoked
                && reverseAfter.magnitude < 0.2f
                && Vector3.Dot(reverseAfter, ship.transform.forward) <= 0.001f;

            controlledBody.linearVelocity = ship.transform.forward * 0.04f;
            bool deadzoneInvoked = TryInvokePrivateMethod(ship, "ApplyNeutralStopBrake", report);
            bool deadzoneSettles = deadzoneInvoked
                && Vector3.ProjectOnPlane(controlledBody.linearVelocity, Vector3.up).magnitude <= 0.001f;

            zeroThrottleBrakeNoBounce = cruiseStopUsesNeutralBrake
                && forwardBrakeDoesNotReverse
                && reverseBrakeDoesNotReverse
                && deadzoneSettles;
        }
        if (controlledBody != null)
        {
            controlledBody.linearVelocity = Vector3.zero;
            ship.neutralStopBrakeEnabled = false;
        }

        report.Check(keyboardManualWritten
            && keyboardHoldRepeats
            && keyboardReleaseKeepsThrottle
            && keyboardReverseFirstStep
            && keyboardReverseFullStep
            && zeroThrottleBrakeAssist,
            "Keyboard W/S uses forward 20% notches, reverse 25/50/100% notches, and Stop engages automatic braking.");
        report.Check(zeroThrottleBrakeNoBounce,
            "Stop braking cancels stale forward/reverse thrust near rest without bouncing across zero speed.");

        controls.ClearAllControlModes();
        controls.CycleAltitudeMode();
        controls.CycleHeadingMode();
        controls.CycleSpeedMode();
        controls.SetAutopilotAltitude(true);
        controls.SetAutopilotHeading(true);
        controls.SetAutopilotSpeed(true);
        bool targetCopied = controls.SetTargetFromActiveTask();
        controls.ToggleAutoDockOnArrival();
        controls.ApplyForTests();
        bool autopilotDisabled = !targetCopied &&
            !controls.IsFullAutopilot &&
            !controls.AutoDockOnArrival &&
            !controls.HasAutopilotTarget &&
            !controls.AutopilotAltitudeEnabled &&
            !controls.AutopilotHeadingEnabled &&
            !controls.AutopilotSpeedEnabled &&
            controls.AltitudeMode == WildWindAxisControlMode.Manual &&
            controls.HeadingMode == WildWindAxisControlMode.Manual &&
            controls.SpeedMode == WildWindAxisControlMode.Manual &&
            !ship.altitudeHold &&
            !ship.headingHold;
        report.Check(autopilotDisabled, "Player autopilot, target assignment and auto-dock controls are disabled.");

        report.Check(ship.maxAutoVerticalSpeed >= 10f && ship.maxStructuralVerticalSpeed >= 10f,
            "Manual vertical control allows up to 10 m/s within structural limits.");

        controls.ClearAllControlModes();
        controls.ClearAutopilotTarget();
        controls.ApplyForTests();
    }

    private static Vector3 FindDockPositionOrConfigPosition(string dockId, Vector3 fallback)
    {
        DockingPort[] docks = FindObjectsByType<DockingPort>(FindObjectsSortMode.None);
        for (int i = 0; i < docks.Length; i++)
        {
            DockingPort dock = docks[i];
            if (dock != null && dock.dockId == dockId)
            {
                return dock.DockPosition;
            }
        }

        return fallback;
    }

    private void ValidateLoadedSessionWorld(int expectedSeed, string expectedSaveFileName, string label, BigTestReport report)
    {
        WorldRegionRuntime loadedWorld = FindFirstObjectByType<WorldRegionRuntime>();
        WorldRuntimeState loadedRuntimeState = FindFirstObjectByType<WorldRuntimeState>();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        report.Check(loadedWorld != null, "WorldRegionRuntime найден после сценария '" + label + "'.");
        report.Check(loadedWorld != null && loadedWorld.RegionSeed == expectedSeed,
            "Мир после сценария '" + label + "' загружен из save seed " + expectedSeed + ".");
        report.Check(loadedRuntimeState != null && loadedRuntimeState.LoadedManifestSeed == expectedSeed,
            "WorldRuntimeState после сценария '" + label + "' принял manifest seed " + expectedSeed + ".");
        report.Check(loadedMeta != null && loadedMeta.EffectiveSaveFileName == expectedSaveFileName,
            "MetaGameState после сценария '" + label + "' смотрит в выбранный save slot.");
        report.Check(loadedMenu != null, "Внутриигровое меню поднялось после сценария '" + label + "'.");
        report.Check(loadedSession != null && loadedSession.IsReady,
            "GameplaySession is ready after session scenario '" + label + "'.");
        string expectedDockId = loadedMeta != null && loadedMeta.progress != null && !string.IsNullOrWhiteSpace(loadedMeta.progress.currentDockId)
            ? loadedMeta.progress.currentDockId
            : GameplaySessionSaveData.DefaultDockId;
        DockingLocationKind expectedDockKind = loadedMeta != null && loadedMeta.progress != null
            ? loadedMeta.progress.currentDockKind
            : DockingLocationKind.Island;
        report.Check(loadedSession != null && loadedSession.CurrentDockId == expectedDockId && loadedSession.CurrentDockKind == expectedDockKind,
            "GameplaySession knows the saved dock after session scenario '" + label + "': " + expectedDockId + ".");
        report.Check(loadedSession != null && loadedSession.PlayerShipRoot != null,
            "GameplaySession has a player ship root after session scenario '" + label + "'.");
        report.Check(loadedHud != null && loadedHud.IsReady,
            "Gameplay HUD is ready after session scenario '" + label + "'.");
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
        ValidateLegacyNearestWeaponApiRemoved(report);
        ValidateArmorDegradationRemoved(report);
        ValidateShipPhysicsUsesInputSystem(report);
        ValidateRuntimeGunFallbackRemoved(report);
        ValidateNoUnauthorizedEditorTools(report);
        report.Info("PROJECT RULE: NO NEW EDITOR TOOLS. Only the Big Test editor entry point is generally allowed; asset fixes must be made directly and checked here.");
        report.Info("Когда появляется новая крупная механика, добавляем сюда отдельный раздел: конфиг, runtime-состояние, симуляция, граничные условия, производительность и пользовательский маршрут.");
        report.Info("Модульные тесты остаются рядом со своей областью: ProductionAutoTestRunner и WorldAutoTestRunner можно запускать отдельно, а большой тест обязан проверять их ключевые инварианты.");
        report.Info("Если тест ругается WARN, это не блокер, но повод записать решение: оставить допуск, ужесточить его или превратить в FAIL.");
        report.Info("Правило проекта: новая фича не считается принятой, пока её главный сценарий не попал в большой тест.");
        report.Pass("Большой тест сформировал явный текстовый протокол, который можно расширять дальше.");
    }

    private static void ValidateLegacyNearestWeaponApiRemoved(BigTestReport report)
    {
#if UNITY_EDITOR
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string shipEditorText = ReadProjectText("Assets/Scripts/Editor/ShipPhysicsEditor.cs");
        string combinedText = shipPhysicsText + "\n" + shipEditorText;
        string oldMiningApi = "TryShoot" + "NearestMiningRock";
        string oldLeviathanShotApi = "TryShoot" + "Leviathan";
        string oldHarpoonApi = "TryFireHarpoonAt" + "NearestLeviathan";
        bool removed = !combinedText.Contains(oldMiningApi) &&
            !combinedText.Contains(oldLeviathanShotApi) &&
            !combinedText.Contains(oldHarpoonApi);
        report.Check(removed, "Legacy nearest-target weapon APIs are absent from ShipPhysics and its editor.");
#else
        report.Check(true, "Legacy nearest-target weapon API source scan is editor-only and skipped in player builds.");
#endif
    }

    private static void ValidateShipPhysicsUsesInputSystem(BigTestReport report)
    {
#if UNITY_EDITOR
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string[] legacyInputTokens =
        {
            "Input.mousePosition",
            "Input.GetMouseButton(",
            "Input.GetMouseButtonDown(",
            "Input.GetMouseButtonUp("
        };
        List<string> leftovers = new List<string>();
        for (int i = 0; i < legacyInputTokens.Length; i++)
        {
            if (shipPhysicsText.Contains(legacyInputTokens[i]))
            {
                leftovers.Add(legacyInputTokens[i]);
            }
        }

        report.Check(leftovers.Count == 0,
            leftovers.Count == 0
                ? "ShipPhysics gameplay input uses the Input System mouse API instead of legacy UnityEngine.Input."
                : "ShipPhysics still reads legacy UnityEngine.Input tokens: " + string.Join(", ", leftovers));
#else
        report.Check(true, "ShipPhysics Input System source scan is editor-only and skipped in player builds.");
#endif
    }

    private static void ValidateRuntimeGunFallbackRemoved(BigTestReport report)
    {
#if UNITY_EDITOR
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string starterHullPrefabText = ReadProjectText("Assets/Data/ShipPrefabs/StarterHull.prefab");
        string worldSceneText = ReadProjectText("Assets/Scenes/WildWindWorldScene.unity");
        string[] fallbackTokens =
        {
            "RuntimeGunMounts",
            "CreateRuntimeGunMount",
            "autoCreateRuntimeMount",
            "EnsureRuntimeGunMounts"
        };
        List<string> leftovers = new List<string>();
        for (int i = 0; i < fallbackTokens.Length; i++)
        {
            if (shipPhysicsText.Contains(fallbackTokens[i]) ||
                starterHullPrefabText.Contains(fallbackTokens[i]) ||
                worldSceneText.Contains(fallbackTokens[i]))
            {
                leftovers.Add(fallbackTokens[i]);
            }
        }

        report.Check(leftovers.Count == 0,
            leftovers.Count == 0
                ? "Ship guns bind existing turret hierarchy; runtime fallback gun mount generation is absent."
                : "Runtime gun fallback tokens remain: " + string.Join(", ", leftovers));
#else
        report.Check(true, "Runtime gun fallback source scan is editor-only and skipped in player builds.");
#endif
    }

    private static void ValidateNoUnauthorizedEditorTools(BigTestReport report)
    {
#if UNITY_EDITOR
        string projectRoot = Directory.GetCurrentDirectory();
        string editorDir = Path.Combine(projectRoot, "Assets", "Scripts", "Editor");
        List<string> violations = new List<string>();

        string forbiddenReimporter = Path.Combine(editorDir, "StarterHullBlenderReimporter.cs");
        if (File.Exists(forbiddenReimporter))
        {
            violations.Add("StarterHullBlenderReimporter.cs");
        }

        if (Directory.Exists(editorDir))
        {
            string[] files = Directory.GetFiles(editorDir, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string relativePath = ToProjectRelativePath(projectRoot, path);
                string text = File.ReadAllText(path);
                string[] lines = text.Split('\n');
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string trimmed = lines[lineIndex].TrimStart();
                    if (!trimmed.StartsWith("[MenuItem(\"Wild Wind/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool allowedBigTestMenu = string.Equals(relativePath, "Assets/Scripts/Editor/WildWindBigTestMenu.cs", StringComparison.Ordinal);
                    if (!allowedBigTestMenu)
                    {
                        violations.Add(relativePath + ":" + (lineIndex + 1).ToString());
                    }
                }
            }
        }

        report.Check(violations.Count == 0,
            violations.Count == 0
                ? "PROJECT RULE: no unauthorized Wild Wind editor tools are present; only the Big Test menu may live in the top-level Unity menu."
                : "Unauthorized editor tools are present: " + string.Join(", ", violations));
#else
        report.Check(true, "Editor tool policy source scan is editor-only and skipped in player builds.");
#endif
    }

    private static string ToProjectRelativePath(string projectRoot, string fullPath)
    {
        string normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
        string normalizedPath = fullPath.Replace('\\', '/');
        if (normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath.Substring(normalizedRoot.Length + 1);
        }

        return normalizedPath;
    }

    private static void ValidateArmorDegradationRemoved(BigTestReport report)
    {
#if UNITY_EDITOR
        string[] sourcePaths =
        {
            "Assets/Scripts/Systems/DamageModel.cs",
            "Assets/Scripts/Systems/DamageProjectile.cs",
            "Assets/Scripts/Systems/DamageTestBench.cs",
            "Assets/Scripts/Systems/PaintedArmorBody.cs",
            "Assets/Scripts/Systems/MeshArmorBody.cs",
            "Assets/Scripts/Editor/DamageTestSceneBuilder.cs",
            "Assets/Scripts/Editor/MeshArmorBodyEditor.cs",
            "Docs/ExtractionMechanicsDiscussion.md",
            "Docs/ShipPhysicsBalanceConstants.md"
        };

        string[] retiredTokens =
        {
            "armor" + "PlateDamage",
            "remaining" + "ArmorPlateHp",
            "max" + "ArmorPlateHp",
            "armor" + "Hp",
            "max" + "ArmorHp",
            "Armor" + "Integrity01",
            "Current" + "ArmorMm",
            "Apply" + "ArmorPlateDamage",
            "Reset" + "ArmorHp",
            "base" + "ArmorMm",
            "armor" + "Integrity01",
            "armor" + "DetailId",
            "hpPer" + "ArmorMm",
            "Mesh" + "ArmorDetail"
        };

        bool clean = true;
        StringBuilder leftovers = new StringBuilder();
        for (int i = 0; i < sourcePaths.Length; i++)
        {
            string text = ReadProjectText(sourcePaths[i]);
            for (int j = 0; j < retiredTokens.Length; j++)
            {
                if (!text.Contains(retiredTokens[j])) continue;
                clean = false;
                if (leftovers.Length > 0)
                {
                    leftovers.Append("; ");
                }

                leftovers.Append(sourcePaths[i]).Append(" contains retired armor degradation token");
                break;
            }
        }

        report.Check(clean, clean
            ? "Armor degradation state is absent from damage runtime, editors, tests, and local docs."
            : "Armor degradation state still has source leftovers: " + leftovers);
#else
        report.Check(true, "Armor degradation source scan is editor-only and skipped in player builds.");
#endif
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

    private void TryWriteRunStatus(string state, WildWindBigTestResult result, BigTestReport report)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, string.IsNullOrWhiteSpace(reportFolder) ? "TestReports" : reportFolder);
            Directory.CreateDirectory(folder);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("state: " + (string.IsNullOrWhiteSpace(state) ? "unknown" : state));
            builder.AppendLine("generatedAtUtc: " + DateTime.UtcNow.ToString("O"));
            builder.AppendLine("scene: " + SceneManager.GetActiveScene().name);
            if (result != null)
            {
                builder.AppendLine("succeeded: " + result.Succeeded);
                builder.AppendLine("completed: " + result.Completed);
                builder.AppendLine("checks: " + result.CheckCount);
                builder.AppendLine("failures: " + result.FailureCount);
            }

            File.WriteAllText(Path.Combine(folder, "WildWindBigTestStatus.txt"), builder.ToString(), Encoding.UTF8);
        }
        catch (Exception exception)
        {
            report?.Warn("Не удалось сохранить статус большого теста: " + exception.Message);
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
        ship.engineResponseRate01PerSecond = 0f;
        ship.neutralStopBrakeEnabled = false;
        ship.neutralStopBrakeMaxDecelerationMS2 = 8f;
        ship.neutralStopBrakeStopTimeSeconds = 0.75f;
        ship.neutralStopBrakeDeadzoneMS = 0.05f;
        ship.neutralStopBrakeAccelerationMS2 = 0f;
        ship.claudiumStock = 20f;
        ship.claudiumConsumptionPerTonSecond = 0.01f;
        ship.claudiumLiftEfficiency = 10f;
        ship.claudiumMaxLiftKg = 1500f;
        ship.claudiumLiftSmoothing = 1000f;
        ship.claudiumLoopResponseRate01PerSecond = 0f;
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
        ship.sideInput = 0f;
        ship.turnInput = 0f;
        ship.liftInput = 0f;
        ship.lateralOmniThrustKgf = 260f;
        ship.propellerMaxSpeedMS = 30f;
        ship.propellerEfficiency = 1f;
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
        ship.propellerMaxSpeedMS = 12f;
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
        float propellerPowerKw = Mathf.Max(0f, ship.enginePowerKwAt100 * ship.enginePowerLever - ship.claudiumPowerDrawKw - ship.gasHarvesterPowerDrawActualKw);
        float usefulPowerW = propellerPowerKw
            * Mathf.Clamp01(ship.propellerEfficiency)
            * 1000f;

        if (dragPerSpeedSquared <= 0f || usefulPowerW <= 0f)
        {
            return 0f;
        }

        return Mathf.Pow(usefulPowerW / dragPerSpeedSquared, 1f / 3f);
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

    private static bool TryInvokePrivateMethod<T>(object target, string methodName, BigTestReport report, out T value)
    {
        value = default;
        if (target == null)
        {
            report.Fail("Could not invoke " + methodName + ": target object is missing.");
            return false;
        }

        System.Reflection.MethodInfo method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null)
        {
            report.Fail(target.GetType().Name + " does not have private method " + methodName + ".");
            return false;
        }

        try
        {
            object result = method.Invoke(target, null);
            if (result is T typed)
            {
                value = typed;
                return true;
            }

            report.Fail(target.GetType().Name + "." + methodName + " returned an unexpected type.");
            return false;
        }
        catch (Exception exception)
        {
            Exception root = exception.InnerException ?? exception;
            report.Fail("Private method " + target.GetType().Name + "." + methodName + " failed: " + root.GetType().Name + " - " + root.Message);
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

    private static Vector3 SimulateDamageProjectileBallistics(
        Vector3 origin,
        Vector3 launchVelocity,
        float gravityScale,
        float maxRangeMeters,
        float velocityRetentionAtMaxRange,
        float flightTimeSeconds,
        float fixedDeltaSeconds,
        Vector3 closestPointTarget,
        out float closestDistanceMeters)
    {
        Vector3 position = origin;
        Vector3 velocity = launchVelocity;
        Vector3 gravity = Physics.gravity * Mathf.Max(0f, gravityScale);
        float dragPerMeter = BallisticFireControl.CalculateDragPerMeter(
            Mathf.Max(1f, maxRangeMeters),
            velocityRetentionAtMaxRange);
        float remaining = Mathf.Max(0f, flightTimeSeconds);
        float step = Mathf.Clamp(fixedDeltaSeconds, 0.001f, 0.05f);
        closestDistanceMeters = Vector3.Distance(position, closestPointTarget);

        while (remaining > 0.00001f)
        {
            float dt = Mathf.Min(step, remaining);
            Vector3 previousPosition = position;
            velocity = BallisticFireControl.IntegrateVelocity(velocity, gravity, dt, dragPerMeter);
            position += velocity * dt;
            closestDistanceMeters = Mathf.Min(
                closestDistanceMeters,
                DistancePointToSegment(closestPointTarget, previousPosition, position));
            remaining -= dt;
        }

        return position;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.000001f)
        {
            return Vector3.Distance(point, segmentStart);
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / lengthSqr);
        return Vector3.Distance(point, segmentStart + segment * t);
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

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static string FormatVector(Vector3 value)
    {
        return "(" + value.x.ToString("0.#") + ", " + value.y.ToString("0.#") + ", " + value.z.ToString("0.#") + ")";
    }

    private static bool ContainsChildNamed(Transform root, string childName)
    {
        if (root == null)
        {
            return false;
        }

        if (root.name == childName)
        {
            return true;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            if (ContainsChildNamed(root.GetChild(i), childName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindVisibleStarterHullMeshSize(MeshFilter[] filters, out Vector3 size)
    {
        size = Vector3.zero;
        if (filters == null)
        {
            return false;
        }

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount <= 0)
            {
                continue;
            }

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 meshSize = filter.sharedMesh.bounds.size;
            Vector3 scale = filter.transform.lossyScale;
            Vector3 scaledSize = new Vector3(
                Mathf.Abs(meshSize.x * scale.x),
                Mathf.Abs(meshSize.y * scale.y),
                Mathf.Abs(meshSize.z * scale.z));

            if (IsFinite(scaledSize) &&
                scaledSize.x >= 5f && scaledSize.x <= 7f &&
                scaledSize.y >= 2.5f && scaledSize.y <= 4.25f &&
                scaledSize.z >= 18f && scaledSize.z <= 22f)
            {
                size = scaledSize;
                return true;
            }

            if (size == Vector3.zero)
            {
                size = scaledSize;
            }
        }

        return false;
    }

    private sealed class BigTestConsoleMessage
    {
        public LogType type;
        public string condition;
        public string stackTrace;
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
