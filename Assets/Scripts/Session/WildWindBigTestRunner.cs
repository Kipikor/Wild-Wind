using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
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
    private const int MinimumExpectedCheckCount = 380;
    private const int MaxCapturedConsoleMessages = 32;
    private const string BigTestEditorLaunchUtcTicksPlayerPrefsKey = "WildWind.BigTestEditorLaunch.UtcTicks";
    private const long BigTestEditorLaunchPendingMaxAgeTicks = TimeSpan.TicksPerMinute * 10L;
#if UNITY_EDITOR
    private const string UsageAuditAfterBigTestArmedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Armed";
    private const string UsageAuditAfterBigTestRequestedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Requested";
    private const string QuitEditorAfterBigTestSessionKey = "WildWind.BigTest.QuitEditorAfterRun";
#endif

    public const string BigTestEditorLaunchPlayerPrefsKey = "WildWind.BigTestEditorLaunch";
    public const string DefaultSessionSceneName = "WildWindSessionScene";

    public static bool SuppressRunOnStartForAutomation { get; set; }
    public static bool IsSessionLoopLaunchInProgress => sessionLoopLaunchInProgress;
    public static bool IsMainSessionCheckInProgress => activeRunInProgress && !sessionLoopLaunchInProgress;
    public static bool IsProgressSandboxActive => IsEditorBigTestLaunchPending() || IsMainSessionCheckInProgress || IsSessionLoopLaunchInProgress;

    private static bool autoRunConsumedThisPlaySession;
    private static bool activeRunInProgress;
    private static bool sessionLoopLaunchInProgress;

    private static readonly string[] RequiredSectionTitles =
    {
        "РџР°СЃРїРѕСЂС‚ РїСЂРѕРІРµСЂРєРё",
        "РЎС†РµРЅР° Рё РєРѕРЅС‚РµРєСЃС‚ Р·Р°РїСѓСЃРєР°",
        "CSV-РєРѕРЅС„РёРіРё",
        "Localization",
        "Р“СЂСѓР·РѕРІС‹Рµ РµРґРёРЅРёС†С‹ Рё РѕС‚СЃРµРєРё РєРѕСЂР°Р±Р»РµР№",
        "Legacy ship asset cleanup",
        "Session legacy cleanup",
        "Session-only runtime",
        "Runtime account",
        "Р’РёР·СѓР°Р», РІС‹СЃРѕС‚РЅС‹Рµ СЃР»РѕРё Рё С‚СѓРјР°РЅ",
        "РќР°СЃС‚СЂРѕР№РєРё РїСЂРѕРµРєС‚Р° Рё СѓРїСЂР°РІР»РµРЅРёРµ",
        "Strategic ship runtime",
        "Knowledge screen runtime",
        "Base island runtime",
        "Direct port entry and persistent progress reset",
        "Р—Р°С‰РёС‚Р° РїРѕР±РѕС‡РЅС‹С… СЌС„С„РµРєС‚РѕРІ",
        "РњРµС‚РѕРґРёРєР° СЃРѕРїСЂРѕРІРѕР¶РґРµРЅРёСЏ"
    };

    [Header("Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚")]
    [SerializeField, InspectorName("Р—Р°РїСѓСЃРєР°С‚СЊ РїСЂРё СЃС‚Р°СЂС‚Рµ Play Mode")] public bool runOnStart;
    [SerializeField, InspectorName("РџРёСЃР°С‚СЊ РїРѕР»РЅС‹Р№ РїСЂРѕС‚РѕРєРѕР» РІ Console")] public bool logFullReportToConsole = true;
    [SerializeField, InspectorName("РЎРѕС…СЂР°РЅСЏС‚СЊ С‚РµРєСЃС‚РѕРІС‹Р№ РїСЂРѕС‚РѕРєРѕР»")] public bool writeReportFile = true;
    [SerializeField, InspectorName("РџР°РїРєР° РїСЂРѕС‚РѕРєРѕР»РѕРІ РѕС‚ РєРѕСЂРЅСЏ РїСЂРѕРµРєС‚Р°")] public string reportFolder = "TestReports";
    [SerializeField, InspectorName("РЎРёРјСѓР»СЏС†РёСЏ РїСЂРѕРёР·РІРѕРґСЃС‚РІ, РјРёРЅСѓС‚")] public float productionSimulationMinutes = 12f;

    [Header("РЎСЃС‹Р»РєРё СЃС†РµРЅС‹")]
    [SerializeField, InspectorName("Р¤РѕРєСѓСЃ РёРіСЂРѕРєР°")] public Transform focus;
    [SerializeField, InspectorName("РЎРµСЃСЃРёРѕРЅРЅР°СЏ Р°С‚РјРѕСЃС„РµСЂР°")] public SessionAtmosphereTuner sessionAtmosphereTuner;
    [SerializeField, InspectorName("РќР°СЃС‚СЂРѕР№РєРё")] public WildWindSettingsRoot settings;
    [SerializeField, InspectorName("РњРµС‚Р°-СЃРѕСЃС‚РѕСЏРЅРёРµ")] public MetaGameState metaGameState;

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
            SceneManager.GetActiveScene().name != DefaultSessionSceneName)
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
        runner.hasRun = false;

        autoRunConsumedThisPlaySession = true;
        runner.RunBigTest();
        return true;
    }
#endif

    public static bool IsEditorBigTestLaunchPending()
    {
        if (PlayerPrefs.GetInt(BigTestEditorLaunchPlayerPrefsKey, 0) != 1)
        {
            return false;
        }

        string ticksText = PlayerPrefs.GetString(BigTestEditorLaunchUtcTicksPlayerPrefsKey, "");
        if (!long.TryParse(ticksText, out long launchedTicks) ||
            DateTime.UtcNow.Ticks - launchedTicks > BigTestEditorLaunchPendingMaxAgeTicks)
        {
            ClearEditorBigTestLaunchPending();
            return false;
        }

        return true;
    }

    public static void MarkEditorBigTestLaunchPending()
    {
        PlayerPrefs.SetInt(BigTestEditorLaunchPlayerPrefsKey, 1);
        PlayerPrefs.SetString(BigTestEditorLaunchUtcTicksPlayerPrefsKey, DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
    }

    public static void ClearEditorBigTestLaunchPending()
    {
        PlayerPrefs.DeleteKey(BigTestEditorLaunchPlayerPrefsKey);
        PlayerPrefs.DeleteKey(BigTestEditorLaunchUtcTicksPlayerPrefsKey);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    public static void ArmEditorQuitAfterBigTest()
    {
        SessionState.SetBool(QuitEditorAfterBigTestSessionKey, true);
    }
#endif

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

    [ContextMenu("РџСЂРѕРІРµСЃС‚Рё Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚")]
    public void RunBigTest()
    {
        if (hasRun)
        {
            Debug.Log(LogPrefix + "Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ СѓР¶Рµ Р·Р°РїСѓСЃРєР°Р»СЃСЏ РЅР° СЌС‚РѕРј РѕР±СЉРµРєС‚Рµ.", this);
            return;
        }

        if (activeRunInProgress)
        {
            Debug.LogWarning(LogPrefix + "Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ СѓР¶Рµ РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ РґСЂСѓРіРёРј runner'РѕРј.", this);
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
            completed?.Invoke(LastResult ?? WildWindBigTestResult.CreateBlocked("Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ СѓР¶Рµ Р·Р°РїСѓСЃРєР°Р»СЃСЏ РЅР° СЌС‚РѕРј РѕР±СЉРµРєС‚Рµ."));
            yield break;
        }

        if (activeRunInProgress)
        {
            completed?.Invoke(WildWindBigTestResult.CreateBlocked("Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ СѓР¶Рµ РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ РґСЂСѓРіРёРј runner'РѕРј."));
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

        MetaGameState.DeletePersistentProgressSaveForTests(true);
        yield return EnsureSessionSceneForBigTest(report);
        ResolveReferences();
        if (metaGameState != null)
        {
            metaGameState.ResetAccountProgressForCheat(out _);
        }

        RunChecked(report, () =>
        {
            ResolveReferences();
            DescribeTestScope(report);
            ValidateSceneContext(report);

            SessionConfigDatabase config = LoadConfig(report);
            ValidateConfigDatabase(config, report);
            ValidateLocalizationConfig(report);
            ValidateCargoStorageModel(config, report);
            ValidateLegacyShipAssetCleanup(config, report);
            ValidateSessionLegacyCleanup(config, report);

            ValidateSessionOnlyRuntimeContract(report);
            ValidateRuntimeAccount(report);
            ValidateVisualAtmosphere(report);
            ValidateSettings(report);
            ValidateStrategicShipRuntime(report);
            ValidateKnowledgeScreenRuntime(report);
            ValidateBaseIslandRuntime(report);
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

#if UNITY_EDITOR
        QuitEditorAfterBatchBigTestIfArmed();
#endif

        if (becamePersistentForSceneLoop)
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void QuitEditorAfterBatchBigTestIfArmed()
    {
        if (!SessionState.GetBool(QuitEditorAfterBigTestSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(QuitEditorAfterBigTestSessionKey, false);
        int exitCode = LastResult != null && LastResult.Succeeded ? 0 : 1;
        Debug.Log(LogPrefix + "Batch big test finished; quitting Unity Editor with exit code " + exitCode + ".", this);
        EditorApplication.Exit(exitCode);
    }
#endif

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
            report.Warn("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ usage coverage snapshot: " + exception.Message);
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
            report.Fail("Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ СѓРїР°Р» РёСЃРєР»СЋС‡РµРЅРёРµРј: " + exception.GetType().Name + " - " + exception.Message);
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
                report.Fail("РђСЃРёРЅС…СЂРѕРЅРЅР°СЏ С‡Р°СЃС‚СЊ Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р° СѓРїР°Р»Р° РёСЃРєР»СЋС‡РµРЅРёРµРј: " + exception.GetType().Name + " - " + exception.Message);
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

    private IEnumerator EnsureSessionSceneForBigTest(BigTestReport report)
    {
        ResolveReferences();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name == DefaultSessionSceneName)
        {
            yield break;
        }

        report.Info("Big test is switching to the session gameplay scene: " + DefaultSessionSceneName + ".");
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
        becamePersistentForSceneLoop = true;
        SceneManager.LoadScene(DefaultSessionSceneName);
        yield return WaitForActiveScene(DefaultSessionSceneName);
        DisableDuplicateBigTestRunners();
        ResolveReferences();
    }

    private IEnumerator RestoreAndValidateSideEffects(BigTestSideEffectSnapshot snapshot, BigTestReport report)
    {
        report.Section("Р—Р°С‰РёС‚Р° РїРѕР±РѕС‡РЅС‹С… СЌС„С„РµРєС‚РѕРІ");
        if (snapshot == null)
        {
            report.Fail("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРЅСЏС‚СЊ snapshot РїРѕР±РѕС‡РЅС‹С… СЌС„С„РµРєС‚РѕРІ РїРµСЂРµРґ СЃС‚Р°СЂС‚РѕРј Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р°.");
            yield break;
        }

        snapshot.RestorePrefsAndTimeScale();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrWhiteSpace(snapshot.ActiveSceneName) &&
            activeScene.name != snapshot.ActiveSceneName)
        {
            report.Warn("Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ Р·Р°РІРµСЂС€Р°РµС‚ РїСЂРѕРІРµСЂРєСѓ РІ СЃС†РµРЅРµ '" + activeScene.name + "', РІРѕСЃСЃС‚Р°РЅР°РІР»РёРІР°СЋ '" + snapshot.ActiveSceneName + "'.");
            SceneManager.LoadScene(snapshot.ActiveSceneName);
            DisableDuplicateBigTestRunners();
            yield return null;
            DisableDuplicateBigTestRunners();
            yield return null;
        }

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

        if (IsIgnorableHeadlessConsoleMessage(condition, type))
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

    private static bool IsIgnorableHeadlessConsoleMessage(string condition, LogType type)
    {
        return type == LogType.Error
            && Application.isBatchMode
            && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null
            && string.Equals(GetFirstConsoleLine(condition), "RenderTexture.Create failed", StringComparison.Ordinal);
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
        report.Fail("РћР¶РёРґР°РµРјС‹Р№ canary FAIL: РјРµС…Р°РЅРёР·Рј РѕС€РёР±РѕРє РґРѕР»Р¶РµРЅ РґРµР»Р°С‚СЊ СЂРµР·СѓР»СЊС‚Р°С‚ РєСЂР°СЃРЅС‹Рј.");
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
        if (focus == null)
        {
            GameObject focusObject = GameObject.Find("Player Bubble Focus");
            focus = focusObject != null ? focusObject.transform : null;
        }

        if (sessionAtmosphereTuner == null) sessionAtmosphereTuner = FindFirstObjectByType<SessionAtmosphereTuner>();
        if (settings == null) settings = FindFirstObjectByType<WildWindSettingsRoot>();
        if (metaGameState == null) metaGameState = FindFirstObjectByType<MetaGameState>();
        if (gameplaySession == null) gameplaySession = FindFirstObjectByType<WildWindGameplaySession>();
        if (gameplayHud == null) gameplayHud = FindFirstObjectByType<WildWindGameplayHud>();
    }

    private void DescribeTestScope(BigTestReport report)
    {
        report.Section("РџР°СЃРїРѕСЂС‚ РїСЂРѕРІРµСЂРєРё");
        report.Info("Р’РµСЂСЃРёСЏ РєРѕРЅС‚СЂР°РєС‚Р° Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р°: " + BigTestContractVersion + ".");
        report.Info("ID Р·Р°РїСѓСЃРєР°: " + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".");
        report.Info("РљРЅРѕРїРєР°: Wild Wind/РџСЂРѕРІРµСЃС‚Рё Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚.");
        report.Info("РќР°Р·РЅР°С‡РµРЅРёРµ: РѕРґРёРЅ РѕР±С‰РёР№ РґРѕС‚РѕС€РЅС‹Р№ РїСЂРѕС‚РѕРєРѕР» РїРѕ С‚РµРєСѓС‰РµР№ СЃР±РѕСЂРєРµ РёРіСЂС‹.");
        report.Info("Currently covered: CSV config, tech tree, flat ship catalog, production, session-only runtime, runtime account, persistent progress, visual dependencies, settings, strategic ship runtime, direct port entry and progress reset.");
        report.Info("Р”РѕРїСѓСЃРєРё: СЃРёРјСѓР»СЏС†РёСЏ РїСЂРѕРёР·РІРѕРґСЃС‚РІ " + productionSimulationMinutes.ToString("0.#") + " РјРёРЅ, session-only СЃС†РµРЅР° Р±РµР· open-world runtime objects Рё Р±РµР· world manifest РІ account data.");
        report.Info("РџСЂРёРЅС†РёРї: FAIL = СЃР»РѕРјР°РЅРѕ РёР»Рё РїСЂРѕС‚РёРІРѕСЂРµС‡РёС‚ С‚РµРєСѓС‰РµРјСѓ РўР—; WARN = РїРѕРґРѕР·СЂРёС‚РµР»СЊРЅРѕ, РЅРѕ РјРѕР¶РЅРѕ РїСЂРѕРґРѕР»Р¶Р°С‚СЊ; OK = РїСЂРѕРІРµСЂРµРЅРѕ СЏРІРЅРѕ.");
        report.Pass("РџР°СЃРїРѕСЂС‚ Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р° СЃС„РѕСЂРјРёСЂРѕРІР°РЅ Рё РїРѕРїР°РґС‘С‚ РІ РјР°С€РёРЅРЅРѕ-С‡РёС‚Р°РµРјС‹Р№ СЂРµР·СѓР»СЊС‚Р°С‚.");
    }

    private void ValidateSceneContext(BigTestReport report)
    {
        report.Section("РЎС†РµРЅР° Рё РєРѕРЅС‚РµРєСЃС‚ Р·Р°РїСѓСЃРєР°");
        Scene scene = SceneManager.GetActiveScene();
        report.Check(scene.IsValid(), "РђРєС‚РёРІРЅР°СЏ СЃС†РµРЅР° РІР°Р»РёРґРЅР°: " + (scene.IsValid() ? scene.name : "<РЅРµС‚ СЃС†РµРЅС‹>") + ".");
        report.Check(Application.isPlaying, "РўРµСЃС‚ РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ РІ Play Mode, runtime-РєРѕРјРїРѕРЅРµРЅС‚С‹ СЂРµР°Р»СЊРЅРѕ РёРЅРёС†РёР°Р»РёР·РёСЂСѓСЋС‚СЃСЏ.");
        report.Info("Unity: " + Application.unityVersion + ".");
        report.Info("РџР»Р°С‚С„РѕСЂРјР°: " + Application.platform + ".");
        report.Info("Р’СЂРµРјСЏ Р·Р°РїСѓСЃРєР°: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ".");
        report.Check(WildWindGameplayBootstrap.IsRuntimeFramePolicyAppliedForTests(),
            "Gameplay runtime starts capped to "
            + WildWindGameplayBootstrap.RuntimeTargetFrameRate
            + " FPS and on the lean quality profile: quality="
            + QualitySettings.GetQualityLevel()
            + ", vSync="
            + QualitySettings.vSyncCount
            + ", targetFrameRate="
            + Application.targetFrameRate
            + ", renderFrameInterval="
            + WildWindGameplayBootstrap.GetRenderFrameIntervalForTests()
            + ".");

        Camera mainCamera = Camera.main;
        report.Check(mainCamera != null, mainCamera != null ? "MainCamera РЅР°Р№РґРµРЅР°: " + mainCamera.name + "." : "MainCamera РЅРµ РЅР°Р№РґРµРЅР°.");
        if (mainCamera != null)
        {
            report.Check(mainCamera.farClipPlane >= 8000f, "Far Clip РєР°РјРµСЂС‹ РґРѕСЃС‚Р°С‚РѕС‡РµРЅ РґР»СЏ С‚РµРєСѓС‰РµРіРѕ РІРёР·СѓР°Р»СЊРЅРѕРіРѕ РїСѓР·С‹СЂСЏ: " + mainCamera.farClipPlane.ToString("0.#") + " Рј.");
            if (mainCamera.nearClipPlane > 1f)
            {
                report.Warn("Near Clip РєР°РјРµСЂС‹ Р±РѕР»СЊС€Рµ 1 Рј. Р”Р»СЏ РјРµР»РєРёС… РєРѕСЂР°Р±РµР»СЊРЅС‹С… РґРµС‚Р°Р»РµР№ СЌС‚Рѕ РјРѕР¶РµС‚ Р±С‹С‚СЊ РіСЂСѓР±РѕРІР°С‚Рѕ: " + mainCamera.nearClipPlane.ToString("0.###") + ".");
            }
            else
            {
                report.Pass("Near Clip РєР°РјРµСЂС‹ РїРѕРґС…РѕРґРёС‚ РґР»СЏ РјРµР»РєРёС… РґРµС‚Р°Р»РµР№: " + mainCamera.nearClipPlane.ToString("0.###") + ".");
            }
        }

        if (mainCamera != null)
        {
            report.Check(mainCamera.clearFlags == CameraClearFlags.Skybox,
                "MainCamera clears to the skybox so the flight scene cannot fall back to a black background.");
        }

        report.Check(RenderSettings.skybox != null,
            RenderSettings.skybox != null
                ? "Runtime skybox material is assigned: " + RenderSettings.skybox.name + "."
                : "Runtime skybox material is missing.");

        if (RenderSettings.fog)
        {
            report.Pass("Unity fog РІРєР»СЋС‡С‘РЅ РєР°Рє Р±Р°Р·РѕРІР°СЏ СЃС‚СЂР°С…РѕРІРѕС‡РЅР°СЏ РґС‹РјРєР°.");
        }
        else if (sessionAtmosphereTuner != null)
        {
            report.Pass("Unity fog РІС‹РєР»СЋС‡РµРЅ, СЌС‚Рѕ РґРѕРїСѓСЃС‚РёРјРѕ: РІС‹СЃРѕС‚РЅРѕР№ РІРёРґРёРјРѕСЃС‚СЊСЋ СѓРїСЂР°РІР»СЏРµС‚ SessionAtmosphereTuner/AERO.");
        }
        else
        {
            report.Warn("Unity fog РІС‹РєР»СЋС‡РµРЅ Рё SessionAtmosphereTuner РЅРµ РЅР°Р№РґРµРЅ. Р’РёРґРёРјРѕСЃС‚СЊ РјРѕР¶РµС‚ РѕСЃС‚Р°С‚СЊСЃСЏ Р±РµР· СЃС‚СЂР°С…РѕРІРѕС‡РЅРѕРіРѕ РѕРіСЂР°РЅРёС‡РµРЅРёСЏ.");
        }
    }

    private SessionConfigDatabase LoadConfig(BigTestReport report)
    {
        report.Section("CSV-РєРѕРЅС„РёРіРё");
        SessionConfigDatabase config = null;

        if (metaGameState != null)
        {
            metaGameState.EnsureProgressInitialized();
            config = metaGameState.SessionConfig;
            if (config != null && config.isLoaded)
            {
                report.Pass("РљРѕРЅС„РёРіРё РІР·СЏС‚С‹ РёР· MetaGameState.");
                return config;
            }
        }

        config = new SessionConfigDatabase();
        config.LoadFromAssetsConfigFolder(DefaultConfigFolder);
        report.Check(config.isLoaded, config.isLoaded
            ? "CSV-РєРѕРЅС„РёРіРё Р·Р°РіСЂСѓР¶РµРЅС‹ РёР· Assets/" + DefaultConfigFolder + "."
            : "CSV-РєРѕРЅС„РёРіРё РЅРµ Р·Р°РіСЂСѓР·РёР»РёСЃСЊ: " + config.lastError);
        return config;
    }

    private void ValidateLocalizationConfig(BigTestReport report)
    {
        report.Section("Localization");
        bool defaultLanguageIsRussian = WildWindLocalization.DefaultLanguage == WildWindLanguage.Ru;
        report.Check(defaultLanguageIsRussian, "Default UI language is Russian.");

        List<string> requiredKeys = new List<string>();
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
        report.Check(FileHasUtf8Bom("Assets/Data/Localization/Ui.csv"),
            "Ui.csv declares UTF-8 with BOM so Russian labels stay readable in Windows spreadsheet tools.");

        string hudSource = ReadProjectText("Assets/Scripts/UI/WildWindGameplayHud.cs");
        string localizationCsv = ReadProjectText("Assets/Data/Localization/Ui.csv");
        string itemCsv = ReadProjectText("Assets/Data/Config/Item.csv");
        bool foodDeliveryTerminologyRemoved =
            !hudSource.Contains("TryLoadStarterFood") &&
            !hudSource.Contains("TryUnloadStarterFood") &&
            !hudSource.Contains("load_food") &&
            !hudSource.Contains("unload_food") &&
            !hudSource.Contains("Load Food") &&
            !hudSource.Contains("Unload Food") &&
            !localizationCsv.Contains("food") &&
            !localizationCsv.Contains("Food") &&
            !itemCsv.Contains("food,") &&
            !itemCsv.Contains(",Food,");
        report.Check(foodDeliveryTerminologyRemoved,
            "Old food-delivery HUD/localization/item markers are removed; port actions describe session processing and base work.");
    }

    private void ValidateConfigDatabase(SessionConfigDatabase config, BigTestReport report)
    {
        if (config == null || !config.isLoaded)
        {
            report.Fail("РџСЂРѕРІРµСЂРєРё CSV РѕСЃС‚Р°РЅРѕРІР»РµРЅС‹: РЅРµС‚ Р·Р°РіСЂСѓР¶РµРЅРЅРѕР№ Р±Р°Р·С‹ РєРѕРЅС„РёРіРѕРІ.");
            return;
        }

        string specialModuleCsvText = ReadProjectText("Assets/Data/Config/Special_module.csv");
        ValidateResourceCatalogConfig(config, report);
        ValidateQuickSortieRewardSourceConfig(config, report);
        ValidateCourierServiceDesignConfig(report);
        ValidateCapitalAirplaneDesignConfig(report);
        ValidateRepairDockDesignConfig(report);
        ValidateFactionProgressionDesignConfig(report);
        bool sortieGeneratorValid = SortieRewardGenerator.ValidateGeneratorForTests(config, out string sortieGeneratorSummary);
        report.Check(sortieGeneratorValid,
            "Sortie reward generator creates reproducible quick/normal/danger/elite concrete payloads and validates material/intangible extraction rules: "
            + sortieGeneratorSummary + ".");
        report.Check(config.items.Count >= 20, "Item.csv СЃРѕРґРµСЂР¶РёС‚ РїСЂРµРґРјРµС‚С‹: " + config.items.Count + ".");
        report.Check(config.ports.Count == 1 && config.GetPort("capital") != null,
            "Port.csv is reduced to the single session port dock: " + config.ports.Count + ".");
        report.Check(File.Exists(ProjectPath("Assets/Data/Config/Port.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island.csv")),
            "Session port config lives in Port.csv and legacy Island.csv is removed.");
        report.Check(config.gasCondensateTypes.Count >= 1, "Gas_condensate_type.csv СЃРѕРґРµСЂР¶РёС‚ С‚РёРїС‹ РѕР±Р»Р°РєРѕРІ: " + config.gasCondensateTypes.Count + ".");
        report.Check(File.Exists(ProjectPath("Assets/Data/Config/Gas_condensate_type.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Gas_cloud_type.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Gas_cloud.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Mining_zone.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Leviathan_zone.csv")),
            "Gas condensate config is renamed for session economy; concrete open-world cloud/mining/leviathan zone CSVs are removed.");
        report.Check(config.oreTypes.Count >= 1, "Ore_type.csv СЃРѕРґРµСЂР¶РёС‚ С‚РёРїС‹ СЂСѓРґС‹: " + config.oreTypes.Count + ".");
        report.Check(config.leviathanTypes.Count >= 1, "Leviathan_type.csv СЃРѕРґРµСЂР¶РёС‚ С‚РёРїС‹ Р»РµРІРёР°С„Р°РЅРѕРІ: " + config.leviathanTypes.Count + ".");
        report.Check(config.technologies.Count >= 17
                && config.modifierDefinitions.Count >= 17,
            "Technology.csv and Modifier_catalog.csv contain the first Last War-style research board: "
            + config.technologies.Count
            + " technologies, "
            + config.modifierDefinitions.Count
            + " modifiers.");
        report.Check(config.specialModules.Count == 5, "Special_module.csv contains only the five temporary starter fitting modules: " + config.specialModules.Count + ".");
        report.Check(config.hulls.Count == 0
            && config.GetHull(GameplaySessionAccountData.DefaultStarterHullId) == null
            && config.claudiumLoops.Count == 0,
            "Runtime hull and claudium-loop CSV rows are disabled while the current Blender ship catalog owns playable ship selection.");
        report.Check(!File.Exists(ProjectPath("Assets/Data/Config/Engine.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Propeller.csv")),
            "Engine and propeller CSVs are removed; hull physics owns thrust and fuel.");
        report.Check(config.GetSpecialModule("starter_gas_harvester") == null
            && config.GetSpecialModule(SessionExtractionConstants.StarterGasExtractorModuleId) != null
            && config.GetSpecialModule("starter_harpoon_rig") == null
            && config.GetSpecialModule(SessionExtractionConstants.StarterLeviathanSalvageModuleId) != null
            && !specialModuleCsvText.Contains("gas_harvester")
            && !specialModuleCsvText.Contains("observation_rock_info_efficiency")
            && !specialModuleCsvText.Contains("observation_cloud_info_efficiency")
            && !specialModuleCsvText.Contains("observation_leviathan_info_efficiency")
            && !specialModuleCsvText.Contains("observation_radius_m")
            && !specialModuleCsvText.Contains("observation_facts_at_half_radius_per_second")
            && !specialModuleCsvText.Contains("survey_paper_to_info_efficiency")
            && !specialModuleCsvText.Contains("leviathan_alarm_generation_multiplier")
            && !specialModuleCsvText.Contains("harpoon_"),
            "Special_module.csv keeps only session fitting gates and no active cloud-harvester, survey-radius or harpoon columns.");
        string coreTacticalBalanceCsvText = ReadProjectText("Assets/Data/Config/Core_tactical_balance.csv");
        CoreTacticalBalanceConfig tacticalBalance = config.coreTacticalBalance;
        bool coreTacticalBalanceReady = tacticalBalance != null
            && coreTacticalBalanceCsvText.Contains("explosive_radius_reference_mass_kg")
            && coreTacticalBalanceCsvText.Contains("explosive_radius_reference_m")
            && coreTacticalBalanceCsvText.Contains("explosive_radius_mass_exponent")
            && Approximately(tacticalBalance.explosiveRadiusReferenceMassKg, 50f, 0.001f)
            && Approximately(tacticalBalance.explosiveRadiusReferenceMeters, 20f, 0.001f)
            && Approximately(tacticalBalance.explosiveRadiusMassExponent, 0.5f, 0.001f);
        report.Check(coreTacticalBalanceReady,
            coreTacticalBalanceReady
                ? "Core_tactical_balance.csv exposes the configurable explosive radius anchor: 50 kg -> 20 m, exponent 0.5."
                : "Core_tactical_balance.csv must expose the configurable explosive radius anchor used by Core Tactical blast-radius math.");
        ValidateKorshunComponentConfig(config, report);
        report.Check(config.shipTreeEntries.Count == 3
            && config.GetShipTreeEntry("capital_patrol_frigate_r02") != null
            && config.GetShipTreeEntry("capital_artillery_cruiser_r02") != null
            && config.GetShipTreeEntry("capital_heavy_battleship_r02") != null,
            "Ship_catalog.csv contains only the current flat playable ship catalog: Korshun, Barbet and Val.");
        report.Check(!File.Exists(ProjectPath("Assets/Data/Config/Island_production.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Production_industry.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Production_recipe.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_archetype.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_archetype_stage.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_social_need.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_building.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Expedition.csv")),
            "Legacy island production/development/society and expedition CSVs are removed.");

        CheckUniqueIds(config.items, item => item.id, "РїСЂРµРґРјРµС‚РѕРІ", report);
        CheckUniqueIds(config.ports, port => port.id, "ports", report);
        CheckUniqueIds(config.gasCondensateTypes, cloudType => cloudType.id, "С‚РёРїРѕРІ РѕР±Р»Р°РєРѕРІ", report);
        CheckUniqueIds(config.oreTypes, ore => ore.id, "С‚РёРїРѕРІ СЂСѓРґС‹", report);
        CheckUniqueIds(config.leviathanTypes, type => type.id, "С‚РёРїРѕРІ Р»РµРІРёР°С„Р°РЅРѕРІ", report);
        CheckUniqueIds(config.technologies, tech => tech.id, "С‚РµС…РЅРѕР»РѕРіРёР№", report);
        CheckUniqueIds(config.modifierDefinitions, modifier => modifier.id, "technology modifiers", report);
        CheckUniqueIds(config.korshunHullPackages, package => package.id, "Korshun hull packages", report);
        CheckUniqueIds(config.korshunPowerPlants, powerPlant => powerPlant.id, "Korshun power plants", report);
        CheckUniqueIds(config.korshunWeaponPackages, weapon => weapon.id, "Korshun main weapon packages", report);
        CheckUniqueIds(config.korshunAuxiliaryPackages, auxiliary => auxiliary.id, "Korshun auxiliary packages", report);
        CheckUniqueIds(config.shipCitadelPackages, citadel => citadel.id, "ship citadel packages", report);
        report.Check(
            ComponentPackageTextIsEncodingClean(config),
            "Ship component package UI text contains no mojibake markers.");
        CheckUniqueIds(config.specialModules, module => module.id, "СЃРїРµС†РјРѕРґСѓР»РµР№", report);
        CheckUniqueIds(config.shipTreeEntries, ship => ship.shipId, "ships in Ship_catalog.csv", report);
        CheckUniqueIds(config.questDefinitions, quest => quest.id, "Quest.csv tasks", report);
        ValidateShipTreeConfig(config, report);
        ValidateQuestConfig(config, report);
        ValidateConfigReferences(config, report);
        ValidateSessionPortConfig(config, report);
    }

    private static void ValidateKorshunComponentConfig(SessionConfigDatabase config, BigTestReport report)
    {
        if (config == null)
        {
            report.Fail("Korshun component config is missing because SessionConfigDatabase is null.");
            return;
        }

        KorshunHullPackageConfig patrol = config.GetKorshunHullPackage("korshun_hull_patrol");
        KorshunHullPackageConfig assault = config.GetKorshunHullPackage("korshun_hull_assault");
        KorshunHullPackageConfig fast = config.GetKorshunHullPackage("korshun_hull_fast");
        KorshunHullPackageConfig cargo = config.GetKorshunHullPackage("korshun_hull_cargo");
        KorshunHullPackageConfig stealth = config.GetKorshunHullPackage("korshun_hull_stealth");
        KorshunHullPackageConfig patrolPlus = config.GetKorshunHullPackage("korshun_hull_patrol_plus");
        KorshunHullPackageConfig assaultPlus = config.GetKorshunHullPackage("korshun_hull_assault_plus");
        KorshunHullPackageConfig fastPlus = config.GetKorshunHullPackage("korshun_hull_fast_plus");
        KorshunHullPackageConfig cargoPlus = config.GetKorshunHullPackage("korshun_hull_cargo_plus");
        KorshunHullPackageConfig stealthPlus = config.GetKorshunHullPackage("korshun_hull_stealth_plus");
        KorshunPowerPlantConfig steamGas = config.GetKorshunPowerPlant("korshun_power_steam_gas");
        bool hullPackagesReady = config.korshunHullPackages.Count >= 10
            && patrol != null
            && assault != null
            && fast != null
            && cargo != null
            && stealth != null
            && patrolPlus != null
            && assaultPlus != null
            && fastPlus != null
            && cargoPlus != null
            && stealthPlus != null
            && Approximately(patrol.lengthM, 60f, 0.01f)
            && Approximately(patrol.structureHp, 16000f, 0.01f)
            && Approximately(patrol.kineticResistancePercent, 48f, 0.01f)
            && Approximately(patrol.thermalResistancePercent, 18f, 0.01f)
            && Approximately(patrol.chemicalResistancePercent, 18f, 0.01f)
            && Approximately(patrol.explosiveResistancePercent, 28f, 0.01f)
            && Approximately(patrol.cargoCapacityTons, 40f, 0.01f)
            && Approximately(patrol.cruiseSpeedMS, 46f, 0.01f)
            && Approximately(patrol.accelerationMS2, 13.24f, 0.01f)
            && Approximately(patrol.turnRateDegPerSecond, 25f, 0.01f)
            && Approximately(patrol.detectionRangeM, 4800f, 0.01f)
            && assault.structureHp > patrol.structureHp
            && assault.kineticResistancePercent > patrol.kineticResistancePercent
            && assault.cruiseSpeedMS < patrol.cruiseSpeedMS
            && fast.cruiseSpeedMS > patrol.cruiseSpeedMS
            && fast.kineticResistancePercent < patrol.kineticResistancePercent
            && cargo.cargoCapacityTons > patrol.cargoCapacityTons
            && stealth.detectionRangeM < patrol.detectionRangeM
            && steamGas != null
            && Approximately(patrol.cruiseSpeedMS + steamGas.speedDeltaMS, 60f, 0.01f)
            && Approximately(patrol.accelerationMS2 + steamGas.accelerationDeltaMS2, 17.27f, 0.02f)
            && Approximately(patrol.turnRateDegPerSecond + steamGas.turnRateDeltaDegPerSecond, 30f, 0.01f)
            && PlusUpgradeLinks(patrolPlus, patrol.id)
            && PlusUpgradeLinks(assaultPlus, assault.id)
            && PlusUpgradeLinks(fastPlus, fast.id)
            && PlusUpgradeLinks(cargoPlus, cargo.id)
            && PlusUpgradeLinks(stealthPlus, stealth.id)
            && patrolPlus.structureHp >= patrol.structureHp * 1.45f
            && patrolPlus.kineticResistancePercent > patrol.kineticResistancePercent
            && assaultPlus.structureHp >= assault.structureHp * 1.45f
            && fastPlus.cruiseSpeedMS >= fast.cruiseSpeedMS * 1.25f
            && cargoPlus.cargoCapacityTons >= cargo.cargoCapacityTons * 1.45f
            && stealthPlus.detectionRangeM <= stealth.detectionRangeM * 0.7f;
        report.Check(hullPackagesReady,
            "Korshun hull packages encode five base variants and five expensive plus variants around the raw 60 m/s, 8-second frigate package baseline before the x2.25 class speed multiplier.");

        KorshunPowerPlantConfig turbogenerator = config.GetKorshunPowerPlant("korshun_power_turbogenerator");
        KorshunPowerPlantConfig capacitor = config.GetKorshunPowerPlant("korshun_power_capacitor");
        KorshunPowerPlantConfig generator = config.GetKorshunPowerPlant("korshun_power_generator");
        KorshunPowerPlantConfig armored = config.GetKorshunPowerPlant("korshun_power_armored");
        KorshunPowerPlantConfig steamGasPlus = config.GetKorshunPowerPlant("korshun_power_steam_gas_plus");
        KorshunPowerPlantConfig turbogeneratorPlus = config.GetKorshunPowerPlant("korshun_power_turbogenerator_plus");
        KorshunPowerPlantConfig capacitorPlus = config.GetKorshunPowerPlant("korshun_power_capacitor_plus");
        KorshunPowerPlantConfig generatorPlus = config.GetKorshunPowerPlant("korshun_power_generator_plus");
        KorshunPowerPlantConfig armoredPlus = config.GetKorshunPowerPlant("korshun_power_armored_plus");
        bool powerPlantsReady = config.korshunPowerPlants.Count >= 10
            && steamGas != null
            && turbogenerator != null
            && capacitor != null
            && generator != null
            && armored != null
            && steamGasPlus != null
            && turbogeneratorPlus != null
            && capacitorPlus != null
            && generatorPlus != null
            && armoredPlus != null
            && Approximately(steamGas.speedDeltaMS, 14f, 0.01f)
            && Approximately(steamGas.accelerationDeltaMS2, 4.03f, 0.01f)
            && Approximately(steamGas.batteryCapacity, 0f, 0.01f)
            && Approximately(steamGas.energyGenerationPerSecond, 0f, 0.01f)
            && !steamGas.allowsElectronicEquipment
            && turbogenerator.allowsElectronicEquipment
            && capacitor.batteryCapacity > turbogenerator.batteryCapacity
            && generator.energyGenerationPerSecond > turbogenerator.energyGenerationPerSecond
            && armored.moduleHp > steamGas.moduleHp
            && armored.speedDeltaMS < 0f
            && PlusUpgradeLinks(steamGasPlus, steamGas.id)
            && PlusUpgradeLinks(turbogeneratorPlus, turbogenerator.id)
            && PlusUpgradeLinks(capacitorPlus, capacitor.id)
            && PlusUpgradeLinks(generatorPlus, generator.id)
            && PlusUpgradeLinks(armoredPlus, armored.id)
            && steamGasPlus.speedDeltaMS >= steamGas.speedDeltaMS * 1.45f
            && turbogeneratorPlus.energyGenerationPerSecond >= turbogenerator.energyGenerationPerSecond * 1.45f
            && capacitorPlus.batteryCapacity >= capacitor.batteryCapacity * 1.45f
            && generatorPlus.energyGenerationPerSecond >= generator.energyGenerationPerSecond * 1.45f
            && armoredPlus.moduleHp >= armored.moduleHp * 1.4f;
        report.Check(powerPlantsReady,
            "Korshun power plants encode five base variants and five expensive plus variants.");

        KorshunWeaponPackageConfig machinegun = config.GetKorshunWeaponPackage("korshun_main_37mm_mg_aura");
        KorshunWeaponPackageConfig autocannon = config.GetKorshunWeaponPackage("korshun_main_57mm_triple_autocannon");
        KorshunWeaponPackageConfig twin76 = config.GetKorshunWeaponPackage("korshun_main_76mm_twin");
        KorshunWeaponPackageConfig single100 = config.GetKorshunWeaponPackage("korshun_main_100mm_single");
        KorshunWeaponPackageConfig nurs = config.GetKorshunWeaponPackage("korshun_main_nurs_turret");
        KorshunWeaponPackageConfig mortar = config.GetKorshunWeaponPackage("korshun_main_200mm_mortar");
        KorshunWeaponPackageConfig machinegunPlus = config.GetKorshunWeaponPackage("korshun_main_37mm_mg_aura_plus");
        KorshunWeaponPackageConfig autocannonPlus = config.GetKorshunWeaponPackage("korshun_main_57mm_triple_autocannon_plus");
        KorshunWeaponPackageConfig twin76Plus = config.GetKorshunWeaponPackage("korshun_main_76mm_twin_plus");
        KorshunWeaponPackageConfig single100Plus = config.GetKorshunWeaponPackage("korshun_main_100mm_single_plus");
        KorshunWeaponPackageConfig nursPlus = config.GetKorshunWeaponPackage("korshun_main_nurs_turret_plus");
        KorshunWeaponPackageConfig mortarPlus = config.GetKorshunWeaponPackage("korshun_main_200mm_mortar_plus");
        bool weaponsReady = config.korshunWeaponPackages.Count >= 12
            && machinegun != null
            && autocannon != null
            && twin76 != null
            && single100 != null
            && nurs != null
            && mortar != null
            && machinegunPlus != null
            && autocannonPlus != null
            && twin76Plus != null
            && single100Plus != null
            && nursPlus != null
            && mortarPlus != null
            && machinegun.isMachinegunAura
            && machinegun.damageType == CoreTacticalDamageType.Kinetic
            && Approximately(machinegun.rangeM, 1000f, 0.01f)
            && Approximately(machinegun.machinegunDamagePerSecond, 120f, 0.01f)
            && autocannon.barrelsOrProjectiles == 3
            && Approximately(autocannon.rangeM, 2400f, 0.01f)
            && autocannon.damageType == CoreTacticalDamageType.Kinetic
            && twin76.barrelsOrProjectiles == 2
            && twin76.damageType == CoreTacticalDamageType.Kinetic
            && Approximately(twin76.resistanceIgnorePercent, 24f, 0.01f)
            && single100.barrelsOrProjectiles == 1
            && single100.rangeM > twin76.rangeM
            && single100.resistanceIgnorePercent > twin76.resistanceIgnorePercent
            && single100.shotsPerMinute < twin76.shotsPerMinute
            && nurs.barrelsOrProjectiles == 16
            && nurs.damageType == CoreTacticalDamageType.Explosive
            && Approximately(nurs.reloadSeconds, 12f, 0.01f)
            && Approximately(nurs.damage, 220f, 0.01f)
            && Approximately(nurs.projectileSpeedMS, 420f, 0.01f)
            && Approximately(nurs.splashRadiusM, 6.3f, 0.01f)
            && Approximately(nurs.explosiveKg, 5f, 0.01f)
            && mortar.rangeM < twin76.rangeM
            && Approximately(mortar.rangeM, 600f, 0.01f)
            && mortar.damage > single100.damage
            && Approximately(mortar.explosiveKg, 12.5f, 0.01f)
            && PlusUpgradeLinks(machinegunPlus, machinegun.id)
            && PlusUpgradeLinks(autocannonPlus, autocannon.id)
            && PlusUpgradeLinks(twin76Plus, twin76.id)
            && PlusUpgradeLinks(single100Plus, single100.id)
            && PlusUpgradeLinks(nursPlus, nurs.id)
            && PlusUpgradeLinks(mortarPlus, mortar.id)
            && machinegunPlus.machinegunDamagePerSecond >= machinegun.machinegunDamagePerSecond * 1.45f
            && autocannonPlus.damage >= autocannon.damage * 1.4f
            && twin76Plus.damage >= twin76.damage * 1.45f
            && single100Plus.resistanceIgnorePercent > single100.resistanceIgnorePercent
            && nursPlus.barrelsOrProjectiles > nurs.barrelsOrProjectiles
            && Approximately(nursPlus.reloadSeconds, 10f, 0.01f)
            && Approximately(nursPlus.damage, 330f, 0.01f)
            && Approximately(nursPlus.projectileSpeedMS, 480f, 0.01f)
            && Approximately(nursPlus.splashRadiusM, 7.1f, 0.01f)
            && Approximately(nursPlus.explosiveKg, 6.25f, 0.01f)
            && Approximately(mortarPlus.rangeM, 700f, 0.01f)
            && mortarPlus.damage >= mortar.damage * 1.45f;
        report.Check(weaponsReady,
            "Korshun main weapon packages cover six base weapons and six expensive plus upgrades.");

        KorshunAuxiliaryPackageConfig torpedoes = config.GetKorshunAuxiliaryPackage("korshun_aux_torpedo_triple_side");
        KorshunAuxiliaryPackageConfig sideNurs = config.GetKorshunAuxiliaryPackage("korshun_aux_side_nurs");
        KorshunAuxiliaryPackageConfig harpoon = config.GetKorshunAuxiliaryPackage("korshun_aux_harpoon");
        KorshunAuxiliaryPackageConfig magnet = config.GetKorshunAuxiliaryPackage("korshun_aux_magnet");
        KorshunAuxiliaryPackageConfig salvageMagnet = config.GetKorshunAuxiliaryPackage("korshun_aux_salvage_magnet");
        KorshunAuxiliaryPackageConfig siphon = config.GetKorshunAuxiliaryPackage("korshun_aux_siphon");
        KorshunAuxiliaryPackageConfig cloudConcentrator = config.GetKorshunAuxiliaryPackage("korshun_aux_cloud_concentrator");
        KorshunAuxiliaryPackageConfig repairBeam = config.GetKorshunAuxiliaryPackage("korshun_aux_repair_beam");
        KorshunAuxiliaryPackageConfig scannerHacker = config.GetKorshunAuxiliaryPackage("korshun_aux_scanner_hacker");
        KorshunAuxiliaryPackageConfig torpedoesPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_torpedo_triple_side_plus");
        KorshunAuxiliaryPackageConfig sideNursPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_side_nurs_plus");
        KorshunAuxiliaryPackageConfig harpoonPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_harpoon_plus");
        KorshunAuxiliaryPackageConfig magnetPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_magnet_plus");
        KorshunAuxiliaryPackageConfig salvageMagnetPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_salvage_magnet_plus");
        KorshunAuxiliaryPackageConfig siphonPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_siphon_plus");
        KorshunAuxiliaryPackageConfig cloudConcentratorPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_cloud_concentrator_plus");
        KorshunAuxiliaryPackageConfig repairBeamPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_repair_beam_plus");
        KorshunAuxiliaryPackageConfig scannerHackerPlus = config.GetKorshunAuxiliaryPackage("korshun_aux_scanner_hacker_plus");
        bool auxiliaryReady = config.korshunAuxiliaryPackages.Count >= 19
            && torpedoes != null
            && sideNurs != null
            && harpoon != null
            && magnet != null
            && salvageMagnet != null
            && siphon != null
            && cloudConcentrator != null
            && repairBeam != null
            && scannerHacker != null
            && torpedoesPlus != null
            && sideNursPlus != null
            && harpoonPlus != null
            && magnetPlus != null
            && salvageMagnetPlus != null
            && siphonPlus != null
            && cloudConcentratorPlus != null
            && repairBeamPlus != null
            && scannerHackerPlus != null
            && Approximately(torpedoes.rangeM, 10000f, 0.01f)
            && Approximately(torpedoes.damage, 5000f, 0.01f)
            && Approximately(torpedoes.projectileSpeedMS, 120f, 0.01f)
            && Approximately(torpedoes.projectileHp, 450f, 0.01f)
            && Approximately(torpedoes.explosiveKg, 120f, 0.01f)
            && torpedoes.projectilesPerSalvo == 6
            && !torpedoes.requiresEnergy
            && Approximately(sideNurs.reloadSeconds, 12f, 0.01f)
            && Approximately(sideNurs.damage, 220f, 0.01f)
            && Approximately(sideNurs.projectileSpeedMS, 420f, 0.01f)
            && Approximately(sideNurs.explosiveKg, 5f, 0.01f)
            && string.Equals(harpoon.kind, "harpoon", StringComparison.OrdinalIgnoreCase)
            && string.Equals(harpoon.slotRole, "auxiliary", StringComparison.OrdinalIgnoreCase)
            && Approximately(harpoon.rangeM, 1800f, 0.01f)
            && Approximately(harpoon.damage, 55f, 0.01f)
            && Approximately(harpoon.projectileSpeedMS, 360f, 0.01f)
            && Approximately(harpoon.projectileHp, 100f, 0.01f)
            && Approximately(harpoon.explosiveKg, 1000f, 0.01f)
            && Approximately(harpoon.reloadSeconds, 5f, 0.01f)
            && Approximately(harpoon.energyCost, 280f, 0.01f)
            && Approximately(harpoon.cycleSeconds, 300f, 0.01f)
            && Approximately(harpoon.repairHpPerCycle, 2f, 0.01f)
            && !harpoon.requiresEnergy
            && magnet.requiresEnergy
            && Approximately(magnet.energyCost, 35f, 0.01f)
            && Approximately(magnet.cycleSeconds, 8f, 0.01f)
            && string.Equals(salvageMagnet.kind, "salvage_magnet", StringComparison.OrdinalIgnoreCase)
            && string.Equals(salvageMagnet.slotRole, "auxiliary", StringComparison.OrdinalIgnoreCase)
            && Approximately(salvageMagnet.rangeM, 1000f, 0.01f)
            && Approximately(salvageMagnet.damage, 22f, 0.01f)
            && Approximately(salvageMagnet.energyCost, 20f, 0.01f)
            && Approximately(salvageMagnet.cycleSeconds, 5f, 0.01f)
            && salvageMagnet.requiresEnergy
            && string.Equals(siphon.kind, "siphon", StringComparison.OrdinalIgnoreCase)
            && string.Equals(siphon.slotRole, "auxiliary", StringComparison.OrdinalIgnoreCase)
            && Approximately(siphon.damage, 24f, 0.01f)
            && siphon.projectilesPerSalvo == 1
            && siphon.requiresEnergy
            && string.Equals(cloudConcentrator.kind, "cloud_concentrator", StringComparison.OrdinalIgnoreCase)
            && string.Equals(cloudConcentrator.slotRole, "small", StringComparison.OrdinalIgnoreCase)
            && Approximately(cloudConcentrator.damage, 20f, 0.01f)
            && Approximately(cloudConcentrator.cycleSeconds, 5f, 0.01f)
            && cloudConcentrator.requiresEnergy
            && Approximately(repairBeam.energyCost, 50f, 0.01f)
            && Approximately(repairBeam.cycleSeconds, 10f, 0.01f)
            && Approximately(repairBeam.cooldownSeconds, 5f, 0.01f)
            && Approximately(repairBeam.repairHpPerCycle, 700f, 0.01f)
            && Approximately(scannerHacker.energyCost, 45f, 0.01f)
            && Approximately(scannerHacker.cycleSeconds, 8f, 0.01f)
            && PlusUpgradeLinks(torpedoesPlus, torpedoes.id)
            && PlusUpgradeLinks(sideNursPlus, sideNurs.id)
            && PlusUpgradeLinks(harpoonPlus, harpoon.id)
            && Approximately(harpoonPlus.rangeM, 2200f, 0.01f)
            && Approximately(harpoonPlus.projectileSpeedMS, 440f, 0.01f)
            && PlusUpgradeLinks(magnetPlus, magnet.id)
            && PlusUpgradeLinks(salvageMagnetPlus, salvageMagnet.id)
            && PlusUpgradeLinks(siphonPlus, siphon.id)
            && PlusUpgradeLinks(cloudConcentratorPlus, cloudConcentrator.id)
            && PlusUpgradeLinks(repairBeamPlus, repairBeam.id)
            && PlusUpgradeLinks(scannerHackerPlus, scannerHacker.id)
            && torpedoesPlus.damage >= torpedoes.damage * 1.45f
            && Approximately(torpedoesPlus.explosiveKg, 180f, 0.01f)
            && sideNursPlus.projectilesPerSalvo > sideNurs.projectilesPerSalvo
            && Approximately(sideNursPlus.reloadSeconds, 10f, 0.01f)
            && Approximately(sideNursPlus.damage, 330f, 0.01f)
            && Approximately(sideNursPlus.projectileSpeedMS, 480f, 0.01f)
            && Approximately(sideNursPlus.explosiveKg, 6.25f, 0.01f)
            && harpoonPlus.damage > harpoon.damage
            && harpoonPlus.projectileHp > harpoon.projectileHp
            && Approximately(harpoonPlus.explosiveKg, 1500f, 0.01f)
            && harpoonPlus.reloadSeconds < harpoon.reloadSeconds
            && magnetPlus.rangeM > magnet.rangeM
            && salvageMagnetPlus.damage > salvageMagnet.damage
            && Approximately(salvageMagnetPlus.energyCost, 24f, 0.01f)
            && salvageMagnetPlus.cycleSeconds < salvageMagnet.cycleSeconds
            && siphonPlus.projectilesPerSalvo > siphon.projectilesPerSalvo
            && cloudConcentratorPlus.damage > cloudConcentrator.damage
            && repairBeamPlus.repairHpPerCycle >= repairBeam.repairHpPerCycle * 1.45f
            && scannerHackerPlus.rangeM > scannerHacker.rangeM;
        report.Check(auxiliaryReady,
            "Korshun auxiliary packages cover combat utilities, harpoons, salvage magnet, siphon, cloud concentrator and expensive plus upgrades.");

        string korshunWeaponCsvText = ReadProjectText("Assets/Data/Config/Korshun_weapon_packages.csv");
        string korshunAuxiliaryCsvText = ReadProjectText("Assets/Data/Config/Korshun_auxiliary_packages.csv");
        string barbetWeaponCsvText = ReadProjectText("Assets/Data/Config/Barbet_weapon_packages.csv");
        bool explosiveMassConfigReady =
            korshunWeaponCsvText.Contains("explosive_kg") &&
            korshunAuxiliaryCsvText.Contains("explosive_kg") &&
            barbetWeaponCsvText.Contains("explosive_kg") &&
            nurs != null &&
            nursPlus != null &&
            mortar != null &&
            torpedoes != null &&
            torpedoesPlus != null &&
            Approximately(nurs.explosiveKg, 5f, 0.01f) &&
            Approximately(nursPlus.explosiveKg, 6.25f, 0.01f) &&
            Approximately(mortar.explosiveKg, 12.5f, 0.01f) &&
            Approximately(torpedoes.explosiveKg, 120f, 0.01f) &&
            Approximately(torpedoesPlus.explosiveKg, 180f, 0.01f);
        report.Check(explosiveMassConfigReady,
            explosiveMassConfigReady
                ? "Explosive weapons encode explosive_kg so blast radius can be derived from the shared 50 kg -> 20 m balance anchor."
                : "Explosive weapon configs must expose explosive_kg for rockets, torpedoes and HE shells instead of hiding blast radius as one-off numbers.");

        ShipTreeEntryConfig korshunShip = config.GetShipTreeEntry("capital_patrol_frigate_r02");
        DockedDevelopmentShipState defaultRuntimeSlot = CreateKorshunRuntimeProbeSlot(
            machinegun != null ? machinegun.id : "korshun_main_37mm_mg_aura",
            torpedoes != null ? torpedoes.id : "korshun_aux_torpedo_triple_side");
        bool defaultRuntimeReady = ValidateKorshunRuntimeProbe(
            config,
            korshunShip,
            defaultRuntimeSlot,
            expectMachineGun: true,
            expectAutocannon: false,
            expectApAutocannon: false,
            out string defaultRuntimeDetails);
        report.Check(defaultRuntimeReady,
            defaultRuntimeReady
                ? "Default Korshun runtime profile enters Core Tactical from the fast hull at 162 m/s after the whole 72 m/s package speed is multiplied by the frigate x2.25 class speed rule, with infinite package weapons, machine-gun main weapon and manual TRP torpedoes."
                : "Default Korshun runtime profile is broken: " + defaultRuntimeDetails);

        DockedDevelopmentShipState single100RuntimeSlot = CreateKorshunRuntimeProbeSlot(
            single100 != null ? single100.id : "korshun_main_100mm_single",
            torpedoes != null ? torpedoes.id : "korshun_aux_torpedo_triple_side");
        bool single100RuntimeReady = ValidateKorshunRuntimeProbe(
            config,
            korshunShip,
            single100RuntimeSlot,
            expectMachineGun: false,
            expectAutocannon: true,
            expectApAutocannon: true,
            out string single100RuntimeDetails);
        report.Check(single100RuntimeReady,
            single100RuntimeReady
                ? "Korshun 100 mm loadout builds a live AP gun package with no phantom MSL weapon group and keeps the 162 m/s fast-hull combat speed."
                : "Korshun 100 mm runtime loadout is broken: " + single100RuntimeDetails);

        DockedDevelopmentShipState nursRuntimeSlot = CreateKorshunRuntimeProbeSlot(
            nurs != null ? nurs.id : "korshun_main_nurs_turret",
            "");
        bool nursRuntimeReady = ValidateKorshunNursRuntimeProbe(
            config,
            korshunShip,
            nursRuntimeSlot,
            out string nursRuntimeDetails);
        report.Check(nursRuntimeReady,
            nursRuntimeReady
                ? "Korshun NURS loadout builds two unguided chaotic Core Tactical rocket clouds with target-tracked burst aim, 12-second reload, explosion-radius proximity, range-end detonation, HUD cooldown countdown, compact damage radius and an active MSL weapon group."
                : "Korshun NURS runtime loadout is broken: " + nursRuntimeDetails);

        DockedDevelopmentShipState fastRuntimeSlot = CreateKorshunRuntimeProbeSlot(
            machinegun != null ? machinegun.id : "korshun_main_37mm_mg_aura",
            torpedoes != null ? torpedoes.id : "korshun_aux_torpedo_triple_side",
            "korshun_hull_fast",
            "korshun_power_steam_gas");
        bool fastRuntimeReady = ValidateKorshunRuntimeProbe(
            config,
            korshunShip,
            fastRuntimeSlot,
            expectMachineGun: true,
            expectAutocannon: false,
            expectApAutocannon: false,
            out string fastRuntimeDetails,
            expectedSpeedMS: 162f,
            expectedAccelerationMS2: 46.63f,
            expectedYawDegPerSecond: 36f,
            expectedHullPackageId: "korshun_hull_fast",
            expectedPowerPackageId: "korshun_power_steam_gas");
        report.Check(fastRuntimeReady,
            fastRuntimeReady
                ? "Korshun fast hull runtime profile enters Core Tactical at 162 m/s: (58 m/s hull + 14 m/s steam-gas power) x2.25."
                : "Korshun fast hull runtime profile is broken: " + fastRuntimeDetails);

        KorshunHullPackageConfig barbetAssault = config.GetKorshunHullPackage("barbet_hull_assault");
        KorshunHullPackageConfig barbetArtillery = config.GetKorshunHullPackage("barbet_hull_artillery");
        KorshunHullPackageConfig barbetRangefinder = config.GetKorshunHullPackage("barbet_hull_rangefinder");
        KorshunPowerPlantConfig barbetSteamGas = config.GetKorshunPowerPlant("barbet_power_steam_gas");
        ShipCitadelPackageConfig barbetCitadel = config.GetShipCitadelPackage("barbet_citadel_standard");
        ShipCitadelPackageConfig barbetArmoredCitadel = config.GetShipCitadelPackage("barbet_citadel_armored");
        KorshunWeaponPackageConfig barbet152 = config.GetKorshunWeaponPackage("barbet_main_152mm_quad_he");
        KorshunWeaponPackageConfig barbet234 = config.GetKorshunWeaponPackage("barbet_main_234mm_ap");
        KorshunWeaponPackageConfig barbetSmall57 = config.GetKorshunWeaponPackage("barbet_small_57mm_autocannon");
        KorshunAuxiliaryPackageConfig barbetMagnet = config.GetKorshunAuxiliaryPackage("barbet_small_magnet");
        KorshunAuxiliaryPackageConfig barbetSmallSiphon = config.GetKorshunAuxiliaryPackage("barbet_small_siphon");
        KorshunAuxiliaryPackageConfig barbetHarpoon = config.GetKorshunAuxiliaryPackage("barbet_small_harpoon");
        bool barbetSmallSiphonReady = barbetSmallSiphon != null
            && string.Equals(barbetSmallSiphon.shipId, "capital_artillery_cruiser_r02", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetSmallSiphon.SlotRoleOrDefault, "small", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetSmallSiphon.kind, "siphon", StringComparison.OrdinalIgnoreCase)
            && barbetSmallSiphon.projectilesPerSalvo == 2
            && Approximately(barbetSmallSiphon.damage, 32f, 0.01f)
            && Approximately(barbetSmallSiphon.energyCost, 12f, 0.01f);
        report.Check(barbetSmallSiphonReady,
            barbetSmallSiphonReady
                ? "Barbet small slot exposes a selectable two-channel cloud siphon package instead of a fixed hull visual."
                : "Barbet small slot must expose barbet_small_siphon as a selectable siphon equipment package.");
        bool barbetHarpoonReady = barbetHarpoon != null
            && string.Equals(barbetHarpoon.shipId, "capital_artillery_cruiser_r02", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetHarpoon.SlotRoleOrDefault, "small", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetHarpoon.kind, "harpoon", StringComparison.OrdinalIgnoreCase)
            && Approximately(barbetHarpoon.rangeM, 400f, 0.01f)
            && Approximately(barbetHarpoon.damage, 85f, 0.01f)
            && Approximately(barbetHarpoon.projectileSpeedMS, 220f, 0.01f)
            && Approximately(barbetHarpoon.projectileHp, 180f, 0.01f)
            && Approximately(barbetHarpoon.reloadSeconds, 5f, 0.01f)
            && Approximately(barbetHarpoon.cycleSeconds, 360f, 0.01f)
            && !barbetHarpoon.requiresEnergy;
        report.Check(barbetHarpoonReady,
            barbetHarpoonReady
                ? "Barbet small slot also exposes a selectable heavy harpoon cannon package."
                : "Barbet small slot must expose barbet_small_harpoon as selectable harpoon equipment.");
        bool barbetReady = barbetAssault != null
            && barbetArtillery != null
            && barbetRangefinder != null
            && barbetSteamGas != null
            && barbetCitadel != null
            && barbetArmoredCitadel != null
            && barbet152 != null
            && barbet234 != null
            && barbetSmall57 != null
            && barbetMagnet != null
            && barbetSmallSiphonReady
            && barbetHarpoonReady
            && string.Equals(barbet152.slotRole, "main", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbet234.shellType, "AP", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetSmall57.slotRole, "small", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetMagnet.SlotRoleOrDefault, "small", StringComparison.OrdinalIgnoreCase)
            && Approximately(barbetArtillery.cruiseSpeedMS + barbetSteamGas.speedDeltaMS, 45f, 0.01f)
            && Approximately(barbetArtillery.accelerationMS2 + barbetSteamGas.accelerationDeltaMS2, 8.64f, 0.03f)
            && Approximately(barbetArtillery.turnRateDegPerSecond + barbetSteamGas.turnRateDeltaDegPerSecond, 18f, 0.01f)
            && barbetAssault.structureHp > barbetArtillery.structureHp
            && barbetRangefinder.weaponRangeMultiplier > 1f
            && barbetArtillery.reloadMultiplier < 1f
            && barbetArmoredCitadel.kineticResistanceBonusPercent > barbetCitadel.kineticResistanceBonusPercent;
        report.Check(barbetReady,
            "Barbet component packages load from runtime CSVs around the 45 m/s, 12-second cruiser combat baseline: hulls, power plants, citadel, main guns, PMK/S weapons and interchangeable small-slot magnetic/siphon equipment.");

        DockedDevelopmentShipState barbetRuntimeSlot = new DockedDevelopmentShipState
        {
            slotIndex = 1,
            shipId = "capital_artillery_cruiser_r02",
            sortiesRemaining = MetaGameState.DevelopmentDockShipMaxSorties
        };
        barbetRuntimeSlot.SetLoadoutPackageId("hull", "barbet_hull_artillery");
        barbetRuntimeSlot.SetLoadoutPackageId("power", "barbet_power_steam_gas");
        barbetRuntimeSlot.SetLoadoutPackageId("citadel", "barbet_citadel_standard");
        barbetRuntimeSlot.SetLoadoutPackageId("small", "barbet_small_siphon");
        ShipTreeEntryConfig barbetShip = config.GetShipTreeEntry("capital_artillery_cruiser_r02");
        CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot barbetRuntimeProfile;
        bool barbetRuntimeReady = barbetReady
            && barbetShip != null
            && CoreTacticalCombatSortieController.TryBuildRuntimeProfileSnapshotForTests(barbetShip, barbetRuntimeSlot, config, out barbetRuntimeProfile)
            && string.Equals(barbetRuntimeProfile.classId, "cruiser", StringComparison.OrdinalIgnoreCase)
            && Approximately(barbetRuntimeProfile.maxForwardSpeedMS, 67.5f, 0.01f)
            && Approximately(barbetRuntimeProfile.forwardAccelerationMS2, 12.95f, 0.03f)
            && Approximately(barbetRuntimeProfile.maxForwardSpeedMS * 2.3025851f / Mathf.Max(0.01f, barbetRuntimeProfile.forwardAccelerationMS2), 12f, 0.03f)
            && string.Equals(barbetRuntimeProfile.hullPackageId, "barbet_hull_artillery", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetRuntimeProfile.powerPackageId, "barbet_power_steam_gas", StringComparison.OrdinalIgnoreCase)
            && string.Equals(barbetRuntimeProfile.smallPackageId, "barbet_small_siphon", StringComparison.OrdinalIgnoreCase);
        report.Check(barbetRuntimeReady,
            barbetRuntimeReady
                ? "Barbet cruiser runtime profile enters Core Tactical at 67.5 m/s from the whole 45 m/s package speed after the cruiser x1.5 class speed multiplier and preserves the selected small siphon package."
                : "Barbet cruiser runtime profile must apply the cruiser x1.5 class speed multiplier and preserve the selected small siphon package.");
    }

    private static DockedDevelopmentShipState CreateKorshunRuntimeProbeSlot(
        string mainPackageId,
        string auxiliaryPackageId,
        string hullPackageId = "korshun_hull_fast",
        string powerPackageId = "korshun_power_steam_gas")
    {
        DockedDevelopmentShipState slot = new DockedDevelopmentShipState
        {
            slotIndex = 0,
            shipId = "capital_patrol_frigate_r02",
            sortiesRemaining = MetaGameState.DevelopmentDockShipMaxSorties
        };
        slot.SetLoadoutPackageId("hull", hullPackageId);
        slot.SetLoadoutPackageId("power", powerPackageId);
        slot.SetLoadoutPackageId("main", mainPackageId);
        slot.SetLoadoutPackageId("auxiliary", auxiliaryPackageId);
        return slot;
    }

    private static bool ValidateKorshunRuntimeProbe(
        SessionConfigDatabase config,
        ShipTreeEntryConfig ship,
        DockedDevelopmentShipState slot,
        bool expectMachineGun,
        bool expectAutocannon,
        bool expectApAutocannon,
        out string details,
        float expectedSpeedMS = 162f,
        float expectedAccelerationMS2 = 46.63f,
        float expectedYawDegPerSecond = 36f,
        string expectedHullPackageId = "korshun_hull_fast",
        string expectedPowerPackageId = "korshun_power_steam_gas")
    {
        details = "";
        if (config == null)
        {
            details = "SessionConfigDatabase is null.";
            return false;
        }

        if (ship == null)
        {
            details = "capital_patrol_frigate_r02 is missing from Ship_catalog.csv.";
            return false;
        }

        if (!CoreTacticalCombatSortieController.TryBuildRuntimeProfileSnapshotForTests(ship, slot, config, out CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot profile))
        {
            details = "runtime profile snapshot could not be built.";
            return false;
        }

        CoreTacticalShipMotor probe = null;
        try
        {
            probe = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                null,
                profile.shipId,
                "Big Test Korshun Runtime Probe",
                Vector3.zero,
                Quaternion.identity,
                profile.hullSizeMeters,
                profile.maxForwardSpeedMS,
                profile.forwardAccelerationMS2,
                profile.brakingAccelerationMS2,
                profile.maxYawRateDegPerSecond,
                profile.maxReverseSpeedMS,
                profile.maxLateralSpeedMS,
                profile.massKg,
                false,
                new Color(0.42f, 0.48f, 0.45f, 1f));

            bool configured = CoreTacticalCombatSortieController.TryConfigureRuntimeLoadoutForTests(
                probe,
                ship,
                slot,
                config,
                out CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot runtime);
            CoreTacticalWeaponControl weaponControl = probe != null ? probe.GetComponent<CoreTacticalWeaponControl>() : null;
            CoreTacticalFrigateAutocannonBattery autocannon = probe != null ? probe.GetComponent<CoreTacticalFrigateAutocannonBattery>() : null;
            CoreTacticalMachineGunMountBattery machineGun = probe != null ? probe.GetComponent<CoreTacticalMachineGunMountBattery>() : null;
            CoreTacticalMissileLauncher[] launchers = probe != null ? probe.GetComponents<CoreTacticalMissileLauncher>() : Array.Empty<CoreTacticalMissileLauncher>();

            float acceleration90PercentSeconds = expectedSpeedMS * 2.3025851f / Mathf.Max(0.01f, expectedAccelerationMS2);
            bool movementProfileOk = configured
                && probe != null
                && Approximately(runtime.maxForwardSpeedMS, expectedSpeedMS, 0.01f)
                && Approximately(probe.maxForwardSpeedMS, expectedSpeedMS, 0.01f)
                && !Approximately(probe.maxForwardSpeedMS, 34f, 0.01f)
                && Approximately(runtime.forwardAccelerationMS2, expectedAccelerationMS2, 0.03f)
                && Approximately(runtime.brakingAccelerationMS2, expectedAccelerationMS2, 0.03f)
                && Approximately(acceleration90PercentSeconds, 8f, 0.03f)
                && Approximately(runtime.maxYawRateDegPerSecond, expectedYawDegPerSecond, 0.01f)
                && Approximately(profile.hullSizeMeters.z, 60f, 0.01f)
                && string.Equals(runtime.hullPackageId, expectedHullPackageId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(runtime.powerPackageId, expectedPowerPackageId, StringComparison.OrdinalIgnoreCase);
            bool weaponControlOk = weaponControl != null
                && !weaponControl.fireSuppressed
                && runtime.activeWeaponGroupCount > 0
                && runtime.hasAnyFireableWeapon
                && runtime.hasAnyNonMissileWeapon
                && !runtime.missileActive
                && runtime.torpedoActive;
            bool machineGunOk = expectMachineGun
                ? machineGun != null && runtime.machineGunActive && !runtime.autocannonActive
                : machineGun == null && !runtime.machineGunActive;
            bool autocannonOk = expectAutocannon
                ? autocannon != null
                    && runtime.autocannonActive
                    && autocannon.maxRangeMeters >= 4000f
                    && autocannon.reloadSeconds > 2.5f
                    && autocannon.reloadSeconds < 4.5f
                    && (!expectApAutocannon || autocannon.detonationMode == CoreTacticalProjectileDetonationMode.DirectImpact)
                : autocannon == null && !runtime.autocannonActive;
            bool torpedoOk = ValidateTorpedoRuntimeLaunchers(launchers, out int torpedoLauncherCount)
                && torpedoLauncherCount == 2;

            details = "speed="
                + (probe != null ? probe.maxForwardSpeedMS.ToString("0.##") : "null")
                + ", accel="
                + runtime.forwardAccelerationMS2.ToString("0.##")
                + ", t90="
                + acceleration90PercentSeconds.ToString("0.##")
                + ", yaw="
                + runtime.maxYawRateDegPerSecond.ToString("0.##")
                + ", main="
                + runtime.mainPackageId
                + ", weaponGroups="
                + runtime.activeWeaponGroupCount
                + ", MG="
                + runtime.machineGunActive
                + ", AC="
                + runtime.autocannonActive
                + ", MSL="
                + runtime.missileActive
                + ", TRP="
                + runtime.torpedoActive
                + ", torpedoLaunchers="
                + torpedoLauncherCount
                + ".";
            return movementProfileOk && weaponControlOk && machineGunOk && autocannonOk && torpedoOk;
        }
        finally
        {
            if (probe != null)
            {
                DestroyBigTestObject(probe.gameObject);
            }
        }
    }

    private static bool ValidateKorshunNursRuntimeProbe(
        SessionConfigDatabase config,
        ShipTreeEntryConfig ship,
        DockedDevelopmentShipState slot,
        out string details)
    {
        details = "";
        if (config == null || ship == null || slot == null)
        {
            details = "config, ship or slot is missing.";
            return false;
        }

        if (!CoreTacticalCombatSortieController.TryBuildRuntimeProfileSnapshotForTests(ship, slot, config, out CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot profile))
        {
            details = "runtime profile snapshot could not be built.";
            return false;
        }

        CoreTacticalShipMotor probe = null;
        CoreTacticalShipMotor movingTarget = null;
        try
        {
            probe = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                null,
                profile.shipId,
                "Big Test Korshun NURS Runtime Probe",
                Vector3.zero,
                Quaternion.identity,
                profile.hullSizeMeters,
                profile.maxForwardSpeedMS,
                profile.forwardAccelerationMS2,
                profile.brakingAccelerationMS2,
                profile.maxYawRateDegPerSecond,
                profile.maxReverseSpeedMS,
                profile.maxLateralSpeedMS,
                profile.massKg,
                false,
                new Color(0.42f, 0.48f, 0.45f, 1f));

            bool configured = CoreTacticalCombatSortieController.TryConfigureRuntimeLoadoutForTests(
                probe,
                ship,
                slot,
                config,
                out CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot runtime);
            CoreTacticalMissileLauncher[] launchers = probe != null ? probe.GetComponents<CoreTacticalMissileLauncher>() : Array.Empty<CoreTacticalMissileLauncher>();
            CoreTacticalMissileLauncher missileLauncher = null;
            int missileLauncherCount = 0;
            int totalAutomaticBurstProjectiles = 0;
            float minimumMissileColorLuminance = float.PositiveInfinity;
            float minimumTrailColorLuminance = float.PositiveInfinity;
            float maximumFanOffsetDegrees = 0f;
            float minimumCloudScatterDegrees = float.PositiveInfinity;
            float minimumChaosAmplitudeDegrees = float.PositiveInfinity;
            float minimumChaosFrequencyHz = float.PositiveInfinity;
            bool missileLaunchersOk = true;
            for (int i = 0; i < launchers.Length; i++)
            {
                CoreTacticalMissileLauncher launcher = launchers[i];
                if (launcher == null || launcher.weaponGroup != CoreTacticalWeaponGroup.Missiles)
                {
                    continue;
                }

                missileLauncher ??= launcher;
                missileLauncherCount++;
                totalAutomaticBurstProjectiles += launcher.AutomaticBurstProjectileCount;
                float missileColorLuminance = launcher.missileColor.r * 0.2126f + launcher.missileColor.g * 0.7152f + launcher.missileColor.b * 0.0722f;
                float trailColorLuminance = launcher.trailColor.r * 0.2126f + launcher.trailColor.g * 0.7152f + launcher.trailColor.b * 0.0722f;
                minimumMissileColorLuminance = Mathf.Min(minimumMissileColorLuminance, missileColorLuminance);
                minimumTrailColorLuminance = Mathf.Min(minimumTrailColorLuminance, trailColorLuminance);
                float launcherFanOffsetDegrees = launcher.AutomaticBurstLauncherFanOffsetDegreesForTests;
                maximumFanOffsetDegrees = Mathf.Max(maximumFanOffsetDegrees, Mathf.Abs(launcherFanOffsetDegrees));
                minimumCloudScatterDegrees = Mathf.Min(minimumCloudScatterDegrees, launcher.AutomaticBurstCloudScatterDegreesForTests);
                minimumChaosAmplitudeDegrees = Mathf.Min(minimumChaosAmplitudeDegrees, launcher.AutomaticBurstChaosAmplitudeDegreesForTests);
                minimumChaosFrequencyHz = Mathf.Min(minimumChaosFrequencyHz, launcher.AutomaticBurstChaosFrequencyHzForTests);

                missileLaunchersOk &= !launcher.manualLaunchOnly
                    && launcher.ProjectilesPerManualSalvo == 8
                    && launcher.AutomaticBurstProjectileCount == 8
                    && Approximately(launcher.launchIntervalSeconds, 12f, 0.01f)
                    && Approximately(launcher.AutomaticBurstShotIntervalSeconds, 0.12f, 0.001f)
                    && launcher.AutomaticBurstSpreadDegrees >= 5f
                    && launcher.AutomaticBurstUsesChaoticCloudForTests
                    && launcher.AutomaticBurstCloudScatterDegreesForTests >= 6f
                    && launcher.AutomaticBurstShotIntervalJitterSecondsForTests >= 0.04f
                    && launcher.AutomaticBurstChaosAmplitudeDegreesForTests >= 12f
                    && launcher.AutomaticBurstChaosFrequencyHzForTests >= 2.2f
                    && Mathf.Abs(launcherFanOffsetDegrees) <= 0.1f
                    && launcher.guidanceMode == CoreTacticalMissileGuidanceMode.DirectChase
                    && Approximately(launcher.missileTurnRateDegPerSecond, 0f, 0.001f)
                    && Approximately(launcher.missileSpeedMS, 420f, 0.01f)
                    && Approximately(launcher.missileDamage, 220f, 0.01f)
                    && Approximately(launcher.explosionRadiusMeters, 6.324f, 0.02f)
                    && Approximately(launcher.proximityRadiusMeters, 6.324f, 0.02f)
                    && Approximately(
                        CoreTacticalGuidedMissile.ResolveDetonationSensitivityRadiusForTests(1f, launcher.explosionRadiusMeters),
                        launcher.explosionRadiusMeters,
                        0.02f)
                    && !launcher.fullDamageInsideExplosionRadius
                    && string.Equals(launcher.visualLauncherRole, "main_rocket", StringComparison.OrdinalIgnoreCase)
                    && missileColorLuminance > 0.20f
                    && trailColorLuminance > 0.20f
                    && launcher.trailColor.a > 0.50f;
            }

            bool predictedUnguidedLaunchAim = false;
            bool missileHudCooldownCountdown = false;
            bool automaticBurstTargetTracking = false;
            if (missileLauncher != null)
            {
                missileLauncher.SetReloadCooldownRemainingSecondsForTests(7.6f);
                string cooldownEight = CoreTacticalCombatSortieController.GetWeaponCooldownTextForTests(probe, CoreTacticalWeaponGroup.Missiles);
                missileLauncher.SetReloadCooldownRemainingSecondsForTests(1.6f);
                string cooldownTwo = CoreTacticalCombatSortieController.GetWeaponCooldownTextForTests(probe, CoreTacticalWeaponGroup.Missiles);
                missileLauncher.SetReloadCooldownRemainingSecondsForTests(0f);
                string cooldownReady = CoreTacticalCombatSortieController.GetWeaponCooldownTextForTests(probe, CoreTacticalWeaponGroup.Missiles);
                string noReloadText = CoreTacticalCombatSortieController.GetWeaponCooldownTextForTests(probe, CoreTacticalWeaponGroup.MachineGuns);
                missileHudCooldownCountdown = cooldownEight == "8"
                    && cooldownTwo == "2"
                    && string.IsNullOrWhiteSpace(cooldownReady)
                    && string.IsNullOrWhiteSpace(noReloadText);

                movingTarget = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                    null,
                    "big_test_nurs_moving_target",
                    "Big Test NURS Moving Target",
                    new Vector3(1000f, 0f, 0f),
                    Quaternion.identity,
                    new Vector3(20f, 8f, 60f),
                    80f,
                    20f,
                    20f,
                    30f,
                    8f,
                    4f,
                    900000f,
                    false,
                    new Color(0.7f, 0.2f, 0.2f, 1f));
                if (movingTarget.Body != null)
                {
                    movingTarget.Body.linearVelocity = new Vector3(0f, 0f, 100f);
                }

                Vector3 launchDirection = missileLauncher.GetInitialLaunchDirectionForTests(Vector3.zero, movingTarget);
                predictedUnguidedLaunchAim = launchDirection.x > 0.80f && launchDirection.z > 0.15f;

                Vector3 trackedBefore = missileLauncher.GetAutomaticBurstTrackedCenterDirectionForTests(movingTarget);
                Vector3 movedTargetPosition = new Vector3(1000f, 0f, 520f);
                movingTarget.transform.position = movedTargetPosition;
                if (movingTarget.Body != null)
                {
                    movingTarget.Body.position = movedTargetPosition;
                    movingTarget.Body.linearVelocity = new Vector3(0f, 0f, 100f);
                }

                Vector3 trackedAfter = missileLauncher.GetAutomaticBurstTrackedCenterDirectionForTests(movingTarget);
                automaticBurstTargetTracking = Vector3.Angle(trackedBefore, trackedAfter) > 10f
                    && trackedAfter.z > trackedBefore.z + 0.15f;
            }

            if (float.IsPositiveInfinity(minimumMissileColorLuminance))
            {
                minimumMissileColorLuminance = 0f;
            }

            if (float.IsPositiveInfinity(minimumTrailColorLuminance))
            {
                minimumTrailColorLuminance = 0f;
            }

            if (float.IsPositiveInfinity(minimumCloudScatterDegrees))
            {
                minimumCloudScatterDegrees = 0f;
            }

            if (float.IsPositiveInfinity(minimumChaosAmplitudeDegrees))
            {
                minimumChaosAmplitudeDegrees = 0f;
            }

            if (float.IsPositiveInfinity(minimumChaosFrequencyHz))
            {
                minimumChaosFrequencyHz = 0f;
            }

            bool guidedExplosionContractOk = ValidateGuidedMissileExplosionContract(out string guidedExplosionDetails);
            bool runtimeOk = configured
                && runtime.missileActive
                && !runtime.torpedoActive
                && missileLauncherCount == 2
                && totalAutomaticBurstProjectiles == 16
                && missileLaunchersOk
                && maximumFanOffsetDegrees <= 0.1f
                && minimumCloudScatterDegrees >= 6f
                && minimumChaosAmplitudeDegrees >= 12f
                && minimumChaosFrequencyHz >= 2.2f
                && guidedExplosionContractOk
                && predictedUnguidedLaunchAim
                && automaticBurstTargetTracking
                && missileHudCooldownCountdown;

            details = "configured="
                + configured
                + ", MSL="
                + runtime.missileActive
                + ", TRP="
                + runtime.torpedoActive
                + ", missileLaunchers="
                + missileLauncherCount
                + ", damage="
                + (missileLauncher != null ? missileLauncher.missileDamage.ToString("0.###") : "null")
                + ", radius="
                + (missileLauncher != null ? missileLauncher.explosionRadiusMeters.ToString("0.###") : "null")
                + ", burstEach="
                + (missileLauncher != null ? missileLauncher.AutomaticBurstProjectileCount.ToString() : "null")
                + ", burstTotal="
                + totalAutomaticBurstProjectiles
                + ", reload="
                + (missileLauncher != null ? missileLauncher.launchIntervalSeconds.ToString("0.###") : "null")
                + ", turn="
                + (missileLauncher != null ? missileLauncher.missileTurnRateDegPerSecond.ToString("0.###") : "null")
                + ", predictedLaunchAim="
                + predictedUnguidedLaunchAim
                + ", burstTargetTracking="
                + automaticBurstTargetTracking
                + ", hudCooldown="
                + missileHudCooldownCountdown
                + ", maxFanOffset="
                + maximumFanOffsetDegrees.ToString("0.###")
                + ", minCloudScatter="
                + minimumCloudScatterDegrees.ToString("0.###")
                + ", minChaosAmplitude="
                + minimumChaosAmplitudeDegrees.ToString("0.###")
                + ", minChaosFrequency="
                + minimumChaosFrequencyHz.ToString("0.###")
                + ", guidedExplosion="
                + guidedExplosionDetails
                + ", minColorLum="
                + minimumMissileColorLuminance.ToString("0.###")
                + ", minTrailLum="
                + minimumTrailColorLuminance.ToString("0.###")
                + ".";
            return runtimeOk;
        }
        finally
        {
            if (movingTarget != null)
            {
                UnityEngine.Object.DestroyImmediate(movingTarget.gameObject);
            }

            if (probe != null)
            {
                DestroyBigTestObject(probe.gameObject);
            }
        }
    }

    private static bool ValidateTorpedoRuntimeLaunchers(CoreTacticalMissileLauncher[] launchers, out int torpedoLauncherCount)
    {
        torpedoLauncherCount = 0;
        if (launchers == null)
        {
            return false;
        }

        for (int i = 0; i < launchers.Length; i++)
        {
            CoreTacticalMissileLauncher launcher = launchers[i];
            if (launcher == null)
            {
                continue;
            }

            if (launcher.weaponGroup == CoreTacticalWeaponGroup.Missiles)
            {
                return false;
            }

            if (launcher.weaponGroup != CoreTacticalWeaponGroup.Torpedoes)
            {
                continue;
            }

            torpedoLauncherCount++;
            float missileColorLuminance = launcher.missileColor.r * 0.2126f + launcher.missileColor.g * 0.7152f + launcher.missileColor.b * 0.0722f;
            float trailColorLuminance = launcher.trailColor.r * 0.2126f + launcher.trailColor.g * 0.7152f + launcher.trailColor.b * 0.0722f;
            if (!launcher.manualLaunchOnly
                || launcher.ProjectilesPerManualSalvo != 3
                || !Approximately(launcher.ManualAimSectorDegrees, 120f, 0.01f)
                || !Approximately(launcher.manualFanAngleDegrees, 15f, 0.01f)
                || !Approximately(launcher.missileSpeedMS, 120f, 0.01f)
                || !Approximately(launcher.explosionRadiusMeters, 30.984f, 0.02f)
                || !Approximately(launcher.proximityRadiusMeters, 30.984f, 0.02f)
                || !launcher.fullDamageInsideExplosionRadius
                || missileColorLuminance <= 0.25f
                || trailColorLuminance <= 0.20f
                || launcher.trailColor.a <= 0.50f)
            {
                return false;
            }
        }

        return torpedoLauncherCount > 0;
    }

    private static bool ValidateGuidedMissileExplosionContract(out string details)
    {
        details = "";
        GameObject root = null;
        try
        {
            root = new GameObject("Big Test Guided Missile Explosion Contract");
            Vector3 origin = new Vector3(940000f, 940000f, 940000f);
            CoreTacticalPrototypeHealth enemyHealth = CreateGuidedMissileExplosionTarget(
                root.transform,
                "Big Test Missile Splash Enemy",
                CoreTacticalCombatTeam.Enemy,
                origin + Vector3.right * 5f,
                Color.red);
            CoreTacticalPrototypeHealth friendlyHealth = CreateGuidedMissileExplosionTarget(
                root.transform,
                "Big Test Missile Splash Friendly",
                CoreTacticalCombatTeam.Friendly,
                origin + Vector3.forward * 5f,
                Color.green);

            float sensitivityRadius = CoreTacticalGuidedMissile.ResolveDetonationSensitivityRadiusForTests(1f, 6f);
            int damagedCount = CoreTacticalGuidedMissile.ApplyExplosionDamageForTests(
                origin,
                null,
                CoreTacticalCombatTeam.Enemy,
                100f,
                6f,
                false);
            bool ok = Approximately(sensitivityRadius, 6f, 0.001f)
                && damagedCount == 1
                && enemyHealth != null
                && friendlyHealth != null
                && enemyHealth.currentHealth < 1000f
                && Approximately(friendlyHealth.currentHealth, 1000f, 0.001f);
            details = "sensitivity="
                + sensitivityRadius.ToString("0.###")
                + ", damaged="
                + damagedCount
                + ", enemyHp="
                + (enemyHealth != null ? enemyHealth.currentHealth.ToString("0.###") : "null")
                + ", friendlyHp="
                + (friendlyHealth != null ? friendlyHealth.currentHealth.ToString("0.###") : "null");
            return ok;
        }
        finally
        {
            DestroyBigTestObject(root);
        }
    }

    private static CoreTacticalPrototypeHealth CreateGuidedMissileExplosionTarget(
        Transform parent,
        string name,
        CoreTacticalCombatTeam team,
        Vector3 position,
        Color color)
    {
        GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetObject.name = name;
        targetObject.transform.SetParent(parent, false);
        targetObject.transform.position = position;
        CoreTacticalShipMotor ship = targetObject.AddComponent<CoreTacticalShipMotor>();
        ship.InitializePrototypeShip(name.ToLowerInvariant().Replace(" ", "_"), name, new Vector3(4f, 4f, 4f), color);
        CoreTacticalCombatant combatant = targetObject.AddComponent<CoreTacticalCombatant>();
        combatant.team = team;
        combatant.ship = ship;
        CoreTacticalPrototypeHealth health = targetObject.AddComponent<CoreTacticalPrototypeHealth>();
        health.maxHealth = 1000f;
        health.destroyOnDeath = false;
        health.ResetHealth();
        return health;
    }

    private static bool ValidateKorshunActiveRuntimeSnapshot(
        CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot runtime,
        out string details,
        float expectedSpeedMS = 135f,
        float expectedAccelerationMS2 = 38.86f,
        float expectedYawDegPerSecond = 30f,
        float expectedEntrySpeedMS = -1f,
        float expectedTargetDistanceMeters = 0f,
        string expectedHullPackageId = "korshun_hull_patrol",
        string expectedPowerPackageId = "korshun_power_steam_gas",
        bool expectedMissileActive = false,
        bool expectedTorpedoActive = true)
    {
        float acceleration90PercentSeconds = expectedSpeedMS * 2.3025851f / Mathf.Max(0.01f, expectedAccelerationMS2);
        float expectedInitialSpeedMS = expectedEntrySpeedMS > 0f ? expectedEntrySpeedMS : expectedSpeedMS;
        bool movementOk = string.Equals(runtime.shipId, "capital_patrol_frigate_r02", StringComparison.OrdinalIgnoreCase)
            && Approximately(runtime.maxForwardSpeedMS, expectedSpeedMS, 0.01f)
            && Approximately(runtime.actualMaxForwardSpeedMS, expectedSpeedMS, 0.01f)
            && !Approximately(runtime.actualMaxForwardSpeedMS, 34f, 0.01f)
            && Approximately(runtime.actualForwardAccelerationMS2, expectedAccelerationMS2, 0.03f)
            && Approximately(runtime.actualBrakingAccelerationMS2, expectedAccelerationMS2, 0.03f)
            && Approximately(acceleration90PercentSeconds, 8f, 0.03f)
            && Approximately(runtime.actualMaxYawRateDegPerSecond, expectedYawDegPerSecond, 0.01f)
            && Approximately(runtime.actualFlatSpeedMS, expectedInitialSpeedMS, 0.5f)
            && Approximately(
                runtime.actualTargetDistanceMeters,
                expectedTargetDistanceMeters,
                Mathf.Max(1f, expectedTargetDistanceMeters * 0.01f))
            && Approximately(runtime.hullSizeMeters.z, 60f, 0.01f)
            && string.Equals(runtime.hullPackageId, expectedHullPackageId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(runtime.powerPackageId, expectedPowerPackageId, StringComparison.OrdinalIgnoreCase);
        bool weaponsOk = runtime.activeWeaponGroupCount > 0
            && runtime.hasAnyFireableWeapon
            && runtime.hasAnyNonMissileWeapon
            && runtime.missileActive == expectedMissileActive
            && runtime.torpedoActive == expectedTorpedoActive
            && !string.IsNullOrWhiteSpace(runtime.mainPackageId);
        details = "ship="
            + runtime.shipId
            + ", profileSpeed="
            + runtime.maxForwardSpeedMS.ToString("0.##")
            + ", actualSpeed="
            + runtime.actualMaxForwardSpeedMS.ToString("0.##")
            + ", actualAccel="
            + runtime.actualForwardAccelerationMS2.ToString("0.##")
            + ", t90="
            + acceleration90PercentSeconds.ToString("0.##")
            + ", actualYaw="
            + runtime.actualMaxYawRateDegPerSecond.ToString("0.##")
            + ", flatSpeed="
            + runtime.actualFlatSpeedMS.ToString("0.##")
            + "/"
            + expectedInitialSpeedMS.ToString("0.##")
            + ", targetDistance="
            + runtime.actualTargetDistanceMeters.ToString("0.##")
            + ", main="
            + runtime.mainPackageId
            + ", weaponGroups="
            + runtime.activeWeaponGroupCount
            + ", nonMissile="
            + runtime.hasAnyNonMissileWeapon
            + ", MSL="
            + runtime.missileActive
            + ", TRP="
            + runtime.torpedoActive
            + ".";
        return movementOk && weaponsOk;
    }

    private static bool PlusUpgradeLinks(KorshunHullPackageConfig upgrade, string baseId)
    {
        return upgrade != null &&
            string.Equals(upgrade.basePackageId, baseId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(upgrade.upgradeLevel, "plus", StringComparison.OrdinalIgnoreCase) &&
            upgrade.powerMultiplier >= 1.4f &&
            upgrade.upgradeCostMultiplier >= 4f;
    }

    private static bool PlusUpgradeLinks(KorshunPowerPlantConfig upgrade, string baseId)
    {
        return upgrade != null &&
            string.Equals(upgrade.basePackageId, baseId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(upgrade.upgradeLevel, "plus", StringComparison.OrdinalIgnoreCase) &&
            upgrade.powerMultiplier >= 1.4f &&
            upgrade.upgradeCostMultiplier >= 4f;
    }

    private static bool PlusUpgradeLinks(KorshunWeaponPackageConfig upgrade, string baseId)
    {
        return upgrade != null &&
            string.Equals(upgrade.basePackageId, baseId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(upgrade.upgradeLevel, "plus", StringComparison.OrdinalIgnoreCase) &&
            upgrade.powerMultiplier >= 1.4f &&
            upgrade.upgradeCostMultiplier >= 4f;
    }

    private static bool PlusUpgradeLinks(KorshunAuxiliaryPackageConfig upgrade, string baseId)
    {
        return upgrade != null &&
            string.Equals(upgrade.basePackageId, baseId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(upgrade.upgradeLevel, "plus", StringComparison.OrdinalIgnoreCase) &&
            upgrade.powerMultiplier >= 1.4f &&
            upgrade.upgradeCostMultiplier >= 4f;
    }

    private static void ValidateResourceCatalogConfig(SessionConfigDatabase config, BigTestReport report)
    {
        string[] currencyIds =
        {
            "freight",
            "solid"
        };

        string[] sublikatIds =
        {
            "resonant_sublikat",
            "null_sublikat",
            "phase_sublikat",
            "vector_sublikat",
            "gray_sublikat",
            "spectral_sublikat",
            "coronal_sublikat",
            "deep_sublikat",
            "inertial_sublikat"
        };

        string[] automatonPartIds =
        {
            "automaton_relay",
            "automaton_coil",
            "automaton_contact_comb",
            "automaton_brass_valve",
            "automaton_mainspring",
            "automaton_calibration_gear",
            "automaton_gyroscope",
            "automaton_optic_lens",
            "automaton_pressure_gauge",
            "automaton_servo_joint",
            "automaton_logic_drum",
            "automaton_command_cylinder",
            "automaton_servo_core"
        };

        string[] factionCurrencyIds =
        {
            "gems",
            "nobel",
            "perfcards",
            "amber"
        };

        string[] factionComponentIds =
        {
            "capital_ordnance_blank",
            "capital_turret_ring",
            "capital_breech_group",
            "capital_rangefinder_prism",
            "capital_casemate_insert",
            "wind_magnetic_coil",
            "wind_turbine_blade",
            "wind_cargo_sling",
            "wind_course_gyro",
            "wind_launch_cup",
            "mist_gas_membrane",
            "mist_separator_cassette",
            "mist_polymer_cell",
            "mist_pyrophoric_paste",
            "mist_cartridge",
            "stone_crushing_crown",
            "stone_throat_grate",
            "stone_armor_wedge",
            "stone_gun_cradle",
            "stone_quarry_insert",
            "ark_precision_drive",
            "ark_servo_ring",
            "ark_counting_cell",
            "ark_repair_lens",
            "ark_hangar_cradle",
            "dev_harpoon_winch",
            "dev_tension_drum",
            "dev_hook_chain",
            "dev_bone_cutter",
            "dev_bomb_cowling"
        };

        report.Check(ItemsExistWithMass(config, currencyIds, 0f),
            "Freight and Solid are real Item.csv currency rows with zero cargo mass.");
        report.Check(ItemsExistWithMass(config, factionCurrencyIds, 0f),
            "Faction currencies for the six reputation shops exist as zero-mass Item.csv rows.");
        report.Check(ItemsExistWithMass(config, sublikatIds, 0.1f),
            "All nine canonical sublikats from the resource draft exist as tangible Item.csv rows.");
        report.Check(ItemIdsExist(config, automatonPartIds),
            "Automaton salvage has concrete part item rows beyond the broken wreck/core placeholders.");
        report.Check(ItemIdsExist(config, factionComponentIds),
            "All thirty faction-only component locks exist as concrete Item.csv rows.");
        report.Check(
            ItemHasRuName(config, "freight", "Фрахт") &&
            ItemHasRuName(config, "solid", "Солид") &&
            ItemHasRuName(config, "charcoal", "Уголь") &&
            ItemHasRuName(config, "water", "Вода") &&
            ItemHasRuName(config, "windshale_ore", "Ветровой сланец") &&
            ItemHasRuName(config, "mist_condensate", "Концентрат сухой дымки") &&
            ItemHasRuName(config, "leviathan_meat", "Мясо левиафана") &&
            ItemHasRuName(config, "leviathan_fat", "Ворвань левиафана") &&
            ItemHasRuName(config, "leviathan_ichor", "Ихор левиафана") &&
            ItemHasRuName(config, "aerosil", "Аэросил") &&
            ItemHasRuName(config, "ionide", "Ионид") &&
            ItemHasRuName(config, "bone_grit", "Костяная мука") &&
            ItemHasRuName(config, "resonant_sublikat", "Резонансный субликат"),
            "Item.csv keeps readable UTF-8 Russian names for representative resources and currencies.");
        report.Check(FilesHaveUtf8Bom(new[]
            {
                "Assets/Data/Config/Item.csv",
                "Assets/Data/Config/Resource_category.csv",
                "Assets/Data/Config/Ore_type.csv",
                "Assets/Data/Config/Gas_condensate_type.csv",
                "Assets/Data/Config/Leviathan_type.csv"
            }),
            "Resource CSVs with Russian names declare UTF-8 with BOM for readable manual review.");

        string resourceCatalogPath = "Assets/Data/Config/Resource_category.csv";
        string resourceCatalogText = ReadProjectText(resourceCatalogPath);
        bool resourceCatalogValid =
            File.Exists(ProjectPath(resourceCatalogPath)) &&
            resourceCatalogText.Contains("currency,") &&
            resourceCatalogText.Contains("faction_currency,") &&
            resourceCatalogText.Contains("ore,") &&
            resourceCatalogText.Contains("processed_mineral,") &&
            resourceCatalogText.Contains("cloud_condensate,") &&
            resourceCatalogText.Contains("cloud_gas_fraction,") &&
            resourceCatalogText.Contains("leviathan_carcass,") &&
            resourceCatalogText.Contains("leviathan_butchery,") &&
            resourceCatalogText.Contains("faction_component,") &&
            resourceCatalogText.Contains("automaton_salvage,") &&
            resourceCatalogText.Contains("sublikat,") &&
            resourceCatalogText.Contains("freight|solid") &&
            resourceCatalogText.Contains("gems|nobel|perfcards|amber") &&
            resourceCatalogText.Contains("resonant_sublikat") &&
            resourceCatalogText.Contains("windshale_ore") &&
            resourceCatalogText.Contains("mist_condensate") &&
            resourceCatalogText.Contains("leviathan_meat") &&
            resourceCatalogText.Contains("leviathan_fat") &&
            resourceCatalogText.Contains("leviathan_ichor") &&
            resourceCatalogText.Contains("aerosil") &&
            resourceCatalogText.Contains("ionide") &&
            resourceCatalogText.Contains("bone_grit") &&
            resourceCatalogText.Contains("acid") &&
            resourceCatalogText.Contains("stone_crushing_crown") &&
            resourceCatalogText.Contains("mist_gas_membrane") &&
            resourceCatalogText.Contains("ark_precision_drive") &&
            resourceCatalogText.Contains("dev_harpoon_winch") &&
            !resourceCatalogText.Contains("claudium_gland") &&
            resourceCatalogText.Contains("automaton_servo_joint");
        report.Check(resourceCatalogValid,
            "Resource_category.csv materializes resource families in Unity config, including currencies, extraction resources, automaton parts and sublikats.");
        report.Check(config.resourceCategories.Count >= 13
                && config.GetResourceCategory("faction_component") != null
                && ResourceCategoryItemsResolve(config),
            "Resource_category.csv is loaded into runtime resource category configs and every listed item resolves to Item.csv.");
        report.Check(AllItemIconAssetsExist(config),
            "Every Item.csv resource has a physical PNG icon asset under Assets/Resources/UI/ResourceIcons.");
    }

    private static void ValidateQuickSortieRewardSourceConfig(SessionConfigDatabase config, BigTestReport report)
    {
        bool fileExists = File.Exists(ProjectPath("Assets/Data/Config/Quick_sortie_reward_source.csv"));
        bool countOk = config.quickSortieRewardSources != null && config.quickSortieRewardSources.Count >= 45;
        bool activitiesOk = QuickSortieSourcesCoverActivities(config, new[]
        {
            "mining",
            "harvesting",
            "hunting",
            "hacking",
            "salvage",
            "survey"
        });
        bool referencesOk = QuickSortieSourcesResolve(config, out string referenceMessage);
        report.Check(fileExists && countOk && activitiesOk && referencesOk,
            "Quick_sortie_reward_source.csv maps adaptive quick-mission ratings to concrete rewards: count="
            + (config.quickSortieRewardSources != null ? config.quickSortieRewardSources.Count : 0)
            + ", activities="
            + activitiesOk
            + ", references="
            + referenceMessage + ".");
    }

    private static void ValidateCourierServiceDesignConfig(BigTestReport report)
    {
        string roles = ReadProjectText("Docs/Balance/PortConfigs/activity_reward_roles.csv");
        string orders = ReadProjectText("Docs/Balance/PortConfigs/courier_orders.csv");
        string design = ReadProjectText("Docs/Balance/CourierServiceDesign.md");
        string sortieGenerator = ReadProjectText("Assets/Scripts/Meta/SortieRewardGenerator.cs");

        bool terminologySeparated = roles.Contains("port_courier")
            && roles.Contains("mission_logistics")
            && !roles.Contains("mission_courier")
            && roles.Contains("Портовая курьерка")
            && roles.Contains("Логистика в вылете")
            && design.Contains("Курьерство - только портовая")
            && design.Contains("Логистика - похожие задачи внутри вылета")
            && sortieGenerator.Contains("case \"courier\": return \"Логистика\";")
            && sortieGenerator.Contains("case \"courier\": return \"логистика\";");
        report.Check(terminologySeparated,
            "Port courier service and sortie logistics are explicitly separated in balance docs/configs and public sortie labels.");

        bool progressionOutlined = orders.Contains("building_level_min")
            && orders.Contains("unlock_mastery_level")
            && orders.Contains("rarity")
            && orders.Contains("customer_faction_pool")
            && orders.Contains("reputation_min")
            && orders.Contains("raw_cosmic")
            && orders.Contains("prepared_materials")
            && orders.Contains("blocks_and_rare_components")
            && orders.Contains("legendary")
            && design.Contains("примерно на 20 уровней")
            && design.Contains("репутацию фракции-заказчика");
        report.Check(progressionOutlined,
            "Courier order balance config captures building-level progression, rarity, faction reputation and the raw-to-blocks request ladder.");
    }

    private static void ValidateCapitalAirplaneDesignConfig(BigTestReport report)
    {
        string roles = ReadProjectText("Docs/Balance/PortConfigs/activity_reward_roles.csv");
        string airplane = ReadProjectText("Docs/Balance/PortConfigs/capital_airplane.csv");
        string design = ReadProjectText("Docs/Balance/CapitalAirplaneDesign.md");
        string city = ReadProjectText("Assets/Resources/BaseIsland/City_building.csv");
        string islandView = ReadProjectText("Assets/Scripts/City/WildWindBaseIslandView.cs");
        string meta = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");

        bool designFixed = roles.Contains("capital_airplane")
            && roles.Contains("Главный регулярный источник солида")
            && airplane.Contains("building_level_min")
            && airplane.Contains("request_stage")
            && airplane.Contains("cooldown_hours")
            && airplane.Contains("solid_full")
            && airplane.Contains("capital_plane_l20")
            && airplane.Contains("фракционные валюты")
            && design.Contains("24 часа")
            && design.Contains("следующий не появляется сразу")
            && design.Contains("Неполная отправка")
            && design.Contains("главный регулярный источник солидов");
        report.Check(designFixed,
            "Capital airplane design/config fixes the daily 24-hour solid faucet, full-load rule and raw-to-blocks request ladder.");

        bool runtimeConnected = city.Contains("capital_airdock,Столичный аэродром")
            && islandView.Contains("CapitalAirdockBuildingId = \"capital_airdock\"")
            && islandView.Contains("OpenCapitalAirplaneWindowForTests")
            && islandView.Contains("Отправить полностью")
            && meta.Contains("CapitalAirplaneCycleSeconds = 24 * 60 * 60")
            && meta.Contains("TrySendCapitalAirplane")
            && meta.Contains("GetCapitalAirplaneState");
        report.Check(runtimeConnected,
            "Capital airdock is seeded into the city and connected to runtime state, UI opening and full-send action.");
    }

    private static void ValidateRepairDockDesignConfig(BigTestReport report)
    {
        string repairDock = ReadProjectText("Docs/Balance/PortConfigs/repair_dock.csv");
        string repairJobs = ReadProjectText("Docs/Balance/PortConfigs/repair_jobs.csv");
        string design = ReadProjectText("Docs/Balance/RepairDockDesign.md");
        string city = ReadProjectText("Assets/Resources/BaseIsland/City_building.csv");
        string islandView = ReadProjectText("Assets/Scripts/City/WildWindBaseIslandView.cs");
        string meta = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");

        bool designFixed = repairDock.Contains("service_level_min")
            && repairDock.Contains("target_rank")
            && repairDock.Contains("top_rank_chance_percent")
            && repairDock.Contains("repair_cost_buy_percent_min")
            && repairJobs.Contains("lower_stage_policy")
            && repairJobs.Contains("claim_to_port_or_sell")
            && design.Contains("persistent damaged ship")
            && design.Contains("no reroll")
            && design.Contains("lower-stage resources")
            && design.Contains("claim or sell");
        report.Check(designFixed,
            "Repair Dock design/config fixes persistent random wrecks, mastery/building rank curve, lower-stage repair resources and claim/sell outcomes.");

        bool runtimeConnected = city.Contains("repair_dock,")
            && islandView.Contains("RepairDockBuildingId = \"repair_dock\"")
            && islandView.Contains("OpenRepairDockWindowForTests")
            && meta.Contains("RepairDockSlotCount = 2")
            && meta.Contains("TryRunRepairDockWork")
            && meta.Contains("TryClaimRepairedDockShip")
            && meta.Contains("TrySellRepairDockShip")
            && meta.Contains("PickRepairDockRank")
            && meta.Contains("BuildRepairDockInputs");
        report.Check(runtimeConnected,
            "Repair Dock is seeded into the city and connected to runtime state, generated wrecks, UI opening, work, claim and sale actions.");
    }

    private static void ValidateFactionProgressionDesignConfig(BigTestReport report)
    {
        string reputation = ReadProjectText("Docs/Balance/PortConfigs/faction_reputation_levels.csv");
        string dailyTasks = ReadProjectText("Docs/Balance/PortConfigs/faction_daily_tasks.csv");
        string market = ReadProjectText("Docs/Balance/PortConfigs/faction_market_items.csv");
        string gates = ReadProjectText("Docs/Balance/PortConfigs/faction_gate_policy.csv");
        string design = ReadProjectText("Docs/Balance/FactionQuestAndMarketDesign.md");
        string meta = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string progress = ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs");

        bool reputationCurveReady = reputation.Contains("100")
            && reputation.Contains("500")
            && reputation.Contains("3000")
            && reputation.Contains("12000")
            && reputation.Contains("50000")
            && meta.Contains("FactionReputationThresholds = { 100, 500, 3000, 12000, 50000 }")
            && meta.Contains("GetFactionReputationLevel")
            && meta.Contains("GetFactionReputationPoints");
        report.Check(reputationCurveReady,
            "Faction reputation has five non-decaying star thresholds wired into runtime: 100/500/3000/12000/50000.");

        bool dailyTasksReady = dailyTasks.Contains("daily_slots")
            && dailyTasks.Contains("total_reputation_per_day")
            && dailyTasks.Contains("capital,5")
            && dailyTasks.Contains("wind_houses,5")
            && dailyTasks.Contains("mist_synod,5")
            && dailyTasks.Contains("stone_vault,5")
            && dailyTasks.Contains("factory_ark,5")
            && dailyTasks.Contains("devourers,5")
            && meta.Contains("FactionDailyTaskTemplates")
            && meta.Contains("TryCompleteFactionDailyTask")
            && progress.Contains("FactionDailyTaskState");
        report.Check(dailyTasksReady,
            "Faction daily tasks are documented and wired as five deterministic daily jobs per faction with currency, mastery and reputation rewards.");

        bool marketReady = market.Contains("stone_throat_grate")
            && market.Contains("mist_separator_cassette")
            && market.Contains("ark_servo_ring")
            && market.Contains("dev_tension_drum")
            && market.Contains("fquest_stone_vault_10")
            && market.Contains("fquest_factory_ark_10")
            && meta.Contains("FactionMarketItems")
            && meta.Contains("TryBuyFactionMarketItem")
            && meta.Contains("currencyItemId = string.IsNullOrWhiteSpace(spec.currencyItemId)");
        report.Check(marketReady,
            "Faction shops expose normal goods and non-craftable component locks without ship-license offers.");

        bool gatesReady = gates.Contains("processing:ore")
            && gates.Contains("processing:gas")
            && gates.Contains("processing:automatondismantling")
            && gates.Contains("processing:leviathanprocessing")
            && gates.Contains("cascade:metallurgy")
            && gates.Contains("cascade:mechanical")
            && meta.Contains("FactionBuildingGates")
            && meta.Contains("CanPassFactionGateForUpgrade")
            && meta.Contains("GetProcessingGateScope")
            && meta.Contains("GetCascadeGateScope");
        report.Check(gatesReady,
            "High-level base processing and production upgrades are gated by faction reputation policy.");

        bool campaignDesignReady = design.Contains("50")
            && design.Contains("100, 500, 3000, 12000")
            && design.Contains("5")
            && design.Contains("R5+");
        report.Check(campaignDesignReady,
            "Faction campaign design fixes fifty-step quest chains, daily reputation flow and component-lock purpose.");
    }

    private static bool QuickSortieSourcesCoverActivities(SessionConfigDatabase config, IReadOnlyList<string> activityIds)
    {
        if (config == null || config.quickSortieRewardSources == null || activityIds == null)
        {
            return false;
        }

        for (int activityIndex = 0; activityIndex < activityIds.Count; activityIndex++)
        {
            bool found = false;
            for (int i = 0; i < config.quickSortieRewardSources.Count; i++)
            {
                QuickSortieRewardSourceConfig source = config.quickSortieRewardSources[i];
                if (source != null && string.Equals(source.activityId, activityIds[activityIndex], StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static bool QuickSortieSourcesResolve(SessionConfigDatabase config, out string message)
    {
        message = "ok";
        if (config == null || config.quickSortieRewardSources == null || config.quickSortieRewardSources.Count == 0)
        {
            message = "missing";
            return false;
        }

        int invalidCount = 0;
        string firstInvalid = "";
        for (int i = 0; i < config.quickSortieRewardSources.Count; i++)
        {
            QuickSortieRewardSourceConfig source = config.quickSortieRewardSources[i];
            bool valid = source != null
                && !string.IsNullOrWhiteSpace(source.activityId)
                && source.minRating >= 1
                && source.minRating <= 100
                && source.weight > 0f
                && QuickSortieSourceResolves(config, source);
            if (valid)
            {
                continue;
            }

            invalidCount++;
            if (string.IsNullOrWhiteSpace(firstInvalid))
            {
                firstInvalid = source == null ? "<null>" : source.activityId + ":" + source.sourceKind + ":" + source.sourceId;
            }
        }

        if (invalidCount > 0)
        {
            message = invalidCount + " invalid, first=" + firstInvalid;
            return false;
        }

        return true;
    }

    private static bool QuickSortieSourceResolves(SessionConfigDatabase config, QuickSortieRewardSourceConfig source)
    {
        if (config == null || source == null || string.IsNullOrWhiteSpace(source.sourceKind) || string.IsNullOrWhiteSpace(source.sourceId))
        {
            return false;
        }

        switch (source.sourceKind)
        {
            case "ore":
                OreTypeConfig ore = config.GetOreType(source.sourceId);
                return ore != null && config.GetItem(ore.oreItemId) != null;
            case "gas":
                GasCondensateTypeConfig gas = config.GetGasCondensateType(source.sourceId);
                return gas != null && config.GetItem(gas.condensateItemId) != null;
            case "leviathan":
                LeviathanTypeConfig leviathan = config.GetLeviathanType(source.sourceId);
                return leviathan != null && config.GetItem(leviathan.carcassItemId) != null;
            case "item":
                return config.GetItem(source.sourceId) != null;
            default:
                return false;
        }
    }

    private static bool ResourceCategoryItemsResolve(SessionConfigDatabase config)
    {
        if (config == null || config.resourceCategories == null || config.resourceCategories.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < config.resourceCategories.Count; i++)
        {
            ResourceCategoryConfig category = config.resourceCategories[i];
            if (category == null || string.IsNullOrWhiteSpace(category.id) || category.itemIds == null || category.itemIds.Count == 0)
            {
                return false;
            }

            for (int itemIndex = 0; itemIndex < category.itemIds.Count; itemIndex++)
            {
                if (config.GetItem(category.itemIds[itemIndex]) == null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AllItemIconAssetsExist(SessionConfigDatabase config)
    {
        if (config == null || config.items == null || config.items.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < config.items.Count; i++)
        {
            ItemConfig item = config.items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.id))
            {
                return false;
            }

            string assetPath = WildWindResourceIconCatalog.GetAssetPathForTests(item.id);
            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(ProjectPath(assetPath)))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateShipTreeConfig(SessionConfigDatabase config, BigTestReport report)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            report.Fail("Ship_catalog.csv is not loaded.");
            return;
        }

        string[] expectedShipIds =
        {
            "capital_patrol_frigate_r02",
            "capital_artillery_cruiser_r02",
            "capital_heavy_battleship_r02"
        };
        HashSet<string> expectedIds = new HashSet<string>(expectedShipIds, StringComparer.OrdinalIgnoreCase);
        bool entriesValid = config.shipTreeEntries.Count == expectedShipIds.Length;
        List<string> invalidShipCatalogEntryIds = new List<string>();
        int catalogShipCount = 0;
        int runtimeReadyHulls = 0;

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null)
            {
                entriesValid = false;
                continue;
            }

            if (entry.IsDevelopmentRosterShip)
            {
                catalogShipCount++;
                if (entry.HasRuntimeHull)
                {
                    runtimeReadyHulls++;
                }
            }

            bool entryValid = !string.IsNullOrWhiteSpace(entry.shipId) &&
                expectedIds.Contains(entry.shipId) &&
                !string.IsNullOrWhiteSpace(entry.localNameRu) &&
                !string.IsNullOrWhiteSpace(entry.localNameEn) &&
                !string.IsNullOrWhiteSpace(entry.classNameRu) &&
                !string.IsNullOrWhiteSpace(entry.roleId) &&
                !string.IsNullOrWhiteSpace(entry.roleNameRu) &&
                !string.IsNullOrWhiteSpace(entry.summaryRu) &&
                string.Equals(entry.catalogScope, "catalog", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(entry.branchId) &&
                (entry.parentShipIds == null || entry.parentShipIds.Count == 0) &&
                (string.IsNullOrWhiteSpace(entry.requiredTechnologyId) || config.GetTechnology(entry.requiredTechnologyId) != null) &&
                (string.IsNullOrWhiteSpace(entry.hullId) || config.GetHull(entry.hullId) != null) &&
                (string.IsNullOrWhiteSpace(entry.claudiumLoopId) || config.GetClaudiumLoop(entry.claudiumLoopId) != null) &&
                (string.IsNullOrWhiteSpace(entry.specialModuleId) || config.GetSpecialModule(entry.specialModuleId) != null) &&
                AllIdsExistAllowEmpty(entry.upgradeHullIds, config.GetHull) &&
                AllIdsExistAllowEmpty(entry.upgradeClaudiumLoopIds, config.GetClaudiumLoop) &&
                AllIdsExistAllowEmpty(entry.upgradeSpecialModuleIds, config.GetSpecialModule);

            entryValid &= !string.IsNullOrWhiteSpace(entry.factionId) &&
                !string.IsNullOrWhiteSpace(entry.factionNameRu) &&
                IsKnownDevelopmentShipClass(entry.shipClassId) &&
                !string.IsNullOrWhiteSpace(entry.shipClassNameRu) &&
                entry.rank > 0 &&
                entry.treeTier >= 1 &&
                entry.treeRow >= 0 &&
                entry.HasRuntimeHull &&
                !string.IsNullOrWhiteSpace(entry.visualModelId) &&
                !string.IsNullOrWhiteSpace(entry.costCurrencyItemId) &&
                config.GetItem(entry.costCurrencyItemId) != null &&
                entry.costAmount > 0 &&
                entry.TotalStatScore > 0 &&
                HasDevelopmentRatingScore(entry);

            entriesValid &= entryValid;
            if (!entryValid && invalidShipCatalogEntryIds.Count < 8)
            {
                invalidShipCatalogEntryIds.Add(string.IsNullOrWhiteSpace(entry.shipId) ? "<empty>" : entry.shipId);
            }
        }

        report.Check(entriesValid
                && catalogShipCount == expectedShipIds.Length
                && runtimeReadyHulls == expectedShipIds.Length
                && config.GetShipTreeEntry("capital_patrol_frigate_r02") != null
                && config.GetShipTreeEntry("capital_artillery_cruiser_r02") != null
                && config.GetShipTreeEntry("capital_heavy_battleship_r02") != null,
            "Ship_catalog.csv gives every current ship faction/class/cost/model/stat field and no extra playable ship rows. Actual: count="
            + config.shipTreeEntries.Count
            + "/3"
            + ", invalidExamples="
            + (invalidShipCatalogEntryIds.Count == 0 ? "none" : string.Join(",", invalidShipCatalogEntryIds)));

        report.Check(ShipCatalogIsFlat(config),
            "Ship catalog is flat: no branch ids, no parent links and no tech-tree rows.");

        report.Check(!ShipTreeHasCycles(config),
            "Ship catalog has no parent_ship_id cycles.");

    }

    private static bool ShipCatalogIsFlat(SessionConfigDatabase config)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            return false;
        }

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(entry.branchId)
                || (entry.parentShipIds != null && entry.parentShipIds.Count > 0))
            {
                return false;
            }
        }

        return true;
    }
    private static bool ShipDevelopmentRosterCountsValid(SessionConfigDatabase config)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            return false;
        }

        string[] factionIds =
        {
            "capital",
            "wind_houses",
            "mist_synod",
            "stone_vault",
            "factory_ark",
            "devourers"
        };
        int[] expectedBranchCounts = { 11, 9, 8, 7, 8, 8 };

        for (int factionIndex = 0; factionIndex < factionIds.Length; factionIndex++)
        {
            int starterCount = 0;
            Dictionary<string, int> branchCounts = new Dictionary<string, int>();
            for (int i = 0; i < config.shipTreeEntries.Count; i++)
            {
                ShipTreeEntryConfig entry = config.shipTreeEntries[i];
                if (entry == null || !entry.IsDevelopmentRosterShip || entry.factionId != factionIds[factionIndex])
                {
                    continue;
                }

                if (entry.treeTier == 1 && entry.branchId == "starter")
                {
                    starterCount++;
                    continue;
                }

                if (!branchCounts.ContainsKey(entry.branchId))
                {
                    branchCounts[entry.branchId] = 0;
                }

                branchCounts[entry.branchId]++;
            }

            if (starterCount != 1 || branchCounts.Count != expectedBranchCounts[factionIndex])
            {
                return false;
            }

            foreach (KeyValuePair<string, int> pair in branchCounts)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value != 9)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ShipDevelopmentTechTreeLayoutValid(SessionConfigDatabase config)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            return false;
        }

        string[] factionIds =
        {
            "capital",
            "wind_houses",
            "mist_synod",
            "stone_vault",
            "factory_ark",
            "devourers"
        };

        for (int factionIndex = 0; factionIndex < factionIds.Length; factionIndex++)
        {
            ShipTreeEntryConfig starter = FindDevelopmentStarter(config, factionIds[factionIndex]);
            if (starter == null ||
                starter.treeTier != 1 ||
                starter.rank != 1 ||
                starter.parentShipIds == null ||
                starter.parentShipIds.Count != 0)
            {
                return false;
            }

            Dictionary<string, List<ShipTreeEntryConfig>> branches = GetDevelopmentBranches(config, factionIds[factionIndex]);
            foreach (KeyValuePair<string, List<ShipTreeEntryConfig>> pair in branches)
            {
                List<ShipTreeEntryConfig> branchShips = pair.Value;
                if (branchShips.Count != 9)
                {
                    return false;
                }

                string expectedParent = starter.shipId;
                int expectedRow = branchShips[0].treeRow;
                for (int shipIndex = 0; shipIndex < branchShips.Count; shipIndex++)
                {
                    ShipTreeEntryConfig ship = branchShips[shipIndex];
                    int expectedTier = shipIndex + 2;
                    if (ship.treeTier != expectedTier ||
                        ship.rank != expectedTier ||
                        ship.treeRow != expectedRow ||
                        ship.parentShipIds == null ||
                        ship.parentShipIds.Count != 1 ||
                        ship.parentShipIds[0] != expectedParent)
                    {
                        return false;
                    }

                    expectedParent = ship.shipId;
                }
            }
        }

        return true;
    }

    private static ShipTreeEntryConfig FindDevelopmentStarter(SessionConfigDatabase config, string factionId)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            return null;
        }

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry != null &&
                entry.IsDevelopmentRosterShip &&
                entry.factionId == factionId &&
                entry.treeTier == 1 &&
                entry.branchId == "starter")
            {
                return entry;
            }
        }

        return null;
    }

    private static Dictionary<string, List<ShipTreeEntryConfig>> GetDevelopmentBranches(SessionConfigDatabase config, string factionId)
    {
        Dictionary<string, List<ShipTreeEntryConfig>> result = new Dictionary<string, List<ShipTreeEntryConfig>>();
        if (config == null || config.shipTreeEntries == null)
        {
            return result;
        }

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null ||
                !entry.IsDevelopmentRosterShip ||
                entry.factionId != factionId ||
                entry.branchId == "starter")
            {
                continue;
            }

            if (!result.TryGetValue(entry.branchId, out List<ShipTreeEntryConfig> branchShips))
            {
                branchShips = new List<ShipTreeEntryConfig>();
                result[entry.branchId] = branchShips;
            }

            branchShips.Add(entry);
        }

        foreach (KeyValuePair<string, List<ShipTreeEntryConfig>> pair in result)
        {
            pair.Value.Sort(CompareShipTreeEntriesByTier);
        }

        return result;
    }

    private static List<ShipTreeEntryConfig> GetDevelopmentBranchShips(SessionConfigDatabase config, string factionId, string shipClassId)
    {
        List<ShipTreeEntryConfig> result = new List<ShipTreeEntryConfig>();
        if (config == null || config.shipTreeEntries == null)
        {
            return result;
        }

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null || !entry.IsDevelopmentRosterShip)
            {
                continue;
            }

            if (entry.factionId == factionId && entry.shipClassId == shipClassId)
            {
                result.Add(entry);
            }
        }

        result.Sort(CompareShipTreeEntriesByTier);
        return result;
    }

    private static int CompareShipTreeEntriesByTier(ShipTreeEntryConfig left, ShipTreeEntryConfig right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;
        int tierComparison = left.treeTier.CompareTo(right.treeTier);
        return tierComparison != 0 ? tierComparison : string.Compare(left.shipId, right.shipId, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountDevelopmentShips(SessionConfigDatabase config, string factionId, string shipClassId)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null || !entry.IsDevelopmentRosterShip)
            {
                continue;
            }

            if (entry.factionId == factionId && entry.shipClassId == shipClassId)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsKnownDevelopmentShipClass(string shipClassId)
    {
        return shipClassId == "prototype" ||
            shipClassId == "destroyer" ||
            shipClassId == "frigate" ||
            shipClassId == "cruiser" ||
            shipClassId == "battleship";
    }

    private static bool HasDevelopmentRatingScore(ShipTreeEntryConfig entry)
    {
        if (entry == null)
        {
            return false;
        }

        int score = Mathf.Max(0, entry.defenseRating)
            + Mathf.Max(0, entry.mobilityRating)
            + Mathf.Max(0, entry.stealthRating)
            + Mathf.Max(0, entry.warfareRating)
            + Mathf.Max(0, entry.miningRating)
            + Mathf.Max(0, entry.harvestingRating)
            + Mathf.Max(0, entry.huntingRating)
            + Mathf.Max(0, entry.hackingRating)
            + Mathf.Max(0, entry.salvageRating)
            + Mathf.Max(0, entry.surveyRating)
            + Mathf.Max(0, entry.repairRating);
        return score > 0 && entry.cargoCapacityTons > 0f;
    }

    private static bool ShipTreeHasCycles(SessionConfigDatabase config)
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

    private static bool ShipTreeVisitHasCycle(ShipTreeEntryConfig entry, SessionConfigDatabase config, HashSet<string> visiting, HashSet<string> visited)
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

    private static void ValidateQuestConfig(SessionConfigDatabase config, BigTestReport report)
    {
        if (config == null || config.questDefinitions == null)
        {
            report.Fail("Quest.csv is not loaded.");
            return;
        }

        string[] requiredObjectiveTypes =
        {
            "resource_owned",
            "resource_acquired",
            "resource_spent",
            "courier_sent",
            "courier_cancelled",
            "technology_started",
            "technology_completed",
            "base_processing_level",
            "cascade_line_level",
            "cascade_order_completed",
            "sortie_completed",
            "ship_module_installed",
            "ship_hull_selected",
            "faction_reputation"
        };

        string[] factionIds =
        {
            "capital",
            "wind_houses",
            "mist_synod",
            "stone_vault",
            "factory_ark",
            "devourers"
        };

        HashSet<string> objectiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> questIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> campaignCountsByFaction = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> campaignFinalTargetsByFaction = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool rowsValid = config.questDefinitions.Count >= 320;
        bool hasRetroactive = false;
        bool hasFromAccept = false;
        bool hasAutoClaim = false;
        bool hasManualClaim = false;
        bool factionCampaignRewardsValid = true;

        for (int i = 0; i < config.questDefinitions.Count; i++)
        {
            QuestDefinitionConfig quest = config.questDefinitions[i];
            if (quest == null)
            {
                rowsValid = false;
                continue;
            }

            questIds.Add(quest.id);
            objectiveTypes.Add(quest.objectiveType);
            hasRetroactive |= quest.IsRetroactive;
            hasFromAccept |= string.Equals(quest.activationMode, QuestDefinitionConfig.ActivationFromAccept, StringComparison.OrdinalIgnoreCase);
            hasAutoClaim |= quest.IsAutoClaim;
            hasManualClaim |= string.Equals(quest.claimMode, QuestDefinitionConfig.ClaimManual, StringComparison.OrdinalIgnoreCase);

            bool validActivation = quest.IsRetroactive
                || string.Equals(quest.activationMode, QuestDefinitionConfig.ActivationFromAccept, StringComparison.OrdinalIgnoreCase);
            bool validClaim = quest.IsAutoClaim
                || string.Equals(quest.claimMode, QuestDefinitionConfig.ClaimManual, StringComparison.OrdinalIgnoreCase);
            rowsValid &= !string.IsNullOrWhiteSpace(quest.id)
                && !string.IsNullOrWhiteSpace(quest.localNameRu)
                && !string.IsNullOrWhiteSpace(quest.categoryId)
                && !string.IsNullOrWhiteSpace(quest.objectiveType)
                && !string.IsNullOrWhiteSpace(quest.targetId)
                && quest.targetAmount > 0
                && validActivation
                && validClaim
                && (string.IsNullOrWhiteSpace(quest.rewardItemId) || config.GetItem(quest.rewardItemId) != null)
                && (string.IsNullOrWhiteSpace(quest.rewardItem2Id) || config.GetItem(quest.rewardItem2Id) != null)
                && quest.rewardFreightAmount >= 0
                && quest.rewardMasteryAmount >= 0
                && quest.rewardReputationAmount >= 0
                && (quest.rewardReputationAmount <= 0 || IsKnownFactionIdForBigTest(quest.rewardReputationFactionId, factionIds));

            if (string.Equals(quest.questKind, "campaign", StringComparison.OrdinalIgnoreCase)
                && IsKnownFactionIdForBigTest(quest.factionId, factionIds))
            {
                campaignCountsByFaction.TryGetValue(quest.factionId, out int count);
                campaignCountsByFaction[quest.factionId] = count + 1;
                campaignFinalTargetsByFaction.TryGetValue(quest.factionId, out int currentFinalTarget);
                if (quest.targetAmount > currentFinalTarget)
                {
                    campaignFinalTargetsByFaction[quest.factionId] = quest.targetAmount;
                }

                factionCampaignRewardsValid &= string.Equals(quest.objectiveType, "faction_reputation", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(quest.targetId, quest.factionId, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(quest.rewardItemId)
                    && config.GetItem(quest.rewardItemId) != null
                    && quest.rewardFreightAmount > 0
                    && quest.rewardMasteryAmount > 0
                    && quest.rewardReputationAmount > 0;
            }
        }

        for (int i = 0; i < config.questDefinitions.Count; i++)
        {
            QuestDefinitionConfig quest = config.questDefinitions[i];
            if (quest == null || string.IsNullOrWhiteSpace(quest.requiredQuestId)) continue;
            rowsValid &= questIds.Contains(quest.requiredQuestId);
        }

        bool objectivesCovered = true;
        for (int i = 0; i < requiredObjectiveTypes.Length; i++)
        {
            objectivesCovered &= objectiveTypes.Contains(requiredObjectiveTypes[i]);
        }

        bool factionCampaignsReady = true;
        for (int i = 0; i < factionIds.Length; i++)
        {
            campaignCountsByFaction.TryGetValue(factionIds[i], out int count);
            campaignFinalTargetsByFaction.TryGetValue(factionIds[i], out int finalTarget);
            factionCampaignsReady &= count == 50 && finalTarget >= 50000;
        }

        report.Check(rowsValid
                && hasRetroactive
                && hasFromAccept
                && hasAutoClaim
                && hasManualClaim,
            "Quest.csv defines the seed board plus faction campaigns with valid rewards, prerequisites, retroactive/from-accept activation and auto/manual claim modes: "
            + config.questDefinitions.Count + ".");
        report.Check(objectivesCovered,
            "Quest.csv covers resource, courier, knowledge, production, sortie, fitting, ship-selection and faction-reputation objective families.");
        report.Check(factionCampaignsReady && factionCampaignRewardsValid,
            "Quest.csv adds six fifty-step faction campaign chains ending at 50000 reputation and paying faction currency, Freight, mastery, reputation and component rewards.");
    }

    private static void ValidateConfigReferences(SessionConfigDatabase config, BigTestReport report)
    {
        bool obsoleteResourcesRemoved = config.GetItem("sulfur") == null &&
            config.GetItem("wood") == null &&
            config.GetItem("cloud_info") == null &&
            config.GetItem("leviathan_info") == null;
        report.Check(obsoleteResourcesRemoved, "Item.csv РѕС‡РёС‰РµРЅ РѕС‚ РЅРµР°РєС‚СѓР°Р»СЊРЅС‹С… СЂРµСЃСѓСЂСЃРѕРІ: СЃРµСЂС‹ Рё РґСЂРµРІРµСЃРёРЅС‹ РЅРµС‚.");

        PortConfig capitalPort = config.GetPort("capital");
        report.Check(capitalPort != null
            && config.ports.Count == 1
            && capitalPort.dockingRadius > 0f
            && capitalPort.position.y > 1000f,
            "Only the isolated capital port remains in Port.csv; sortie locations are not reachable from it.");

        bool gasValid = true;
        for (int i = 0; i < config.gasCondensateTypes.Count; i++)
        {
            GasCondensateTypeConfig type = config.gasCondensateTypes[i];
            gasValid &= type != null &&
                config.GetItem(type.condensateItemId) != null &&
                type.condensateLitersPerCubicMeter > 0f &&
                ItemAmountsReferenceExistingItems(type.composition, c => c.itemId, c => c.share, config);
        }

        report.Check(gasValid, "Р“Р°Р·РѕРІС‹Рµ РѕР±Р»Р°РєР° Рё РёС… С‚РёРїС‹ СЃСЃС‹Р»Р°СЋС‚СЃСЏ РЅР° СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёРµ РїСЂРµРґРјРµС‚С‹/С‚РёРїС‹ Рё РёРјРµСЋС‚ РґРѕР±С‹РІР°РµРјС‹Р№ РѕР±СЉС‘Рј.");

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

        report.Check(oreValid, "Р СѓРґР° Рё Р·РѕРЅС‹ РґРѕР±С‹С‡Рё РёРјРµСЋС‚ РІР°Р»РёРґРЅС‹Рµ РїСЂРµРґРјРµС‚С‹, РјРёРЅРµСЂР°Р»С‹, РѕР±СЉС‘Рј Рё РІС‹СЃРѕС‚РЅСѓСЋ С‚СЂР°РµРєС‚РѕСЂРёСЋ.");

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
                type.omniThrustKgf >= 0f &&
                type.composition != null &&
                type.composition.Count > 0 &&
                ItemAmountsReferenceExistingItems(type.composition, c => c.itemId, c => c.share, config);
        }

        report.Check(leviathansValid, "Р›РµРІРёР°С„Р°РЅС‹ Рё РёС… Р·РѕРЅС‹ РёРјРµСЋС‚ РІР°Р»РёРґРЅС‹Рµ С‚РёРїС‹, СЂР°Р·РјРµСЂС‹, Р·РґРѕСЂРѕРІСЊРµ Рё РІС‹СЃРѕС‚РЅС‹Рµ РґРёР°РїР°Р·РѕРЅС‹.");

        bool techValid = true;
        bool modifierCatalogValid = config.modifierDefinitions.Count >= 17;
        for (int i = 0; i < config.modifierDefinitions.Count; i++)
        {
            ModifierDefinitionConfig modifier = config.modifierDefinitions[i];
            modifierCatalogValid &= modifier != null &&
                !string.IsNullOrWhiteSpace(modifier.id) &&
                !string.IsNullOrWhiteSpace(modifier.localNameRu) &&
                !string.IsNullOrWhiteSpace(modifier.localNameEn) &&
                !string.IsNullOrWhiteSpace(modifier.categoryId) &&
                !string.IsNullOrWhiteSpace(modifier.valueKind) &&
                !string.IsNullOrWhiteSpace(modifier.stackingRule) &&
                !string.IsNullOrWhiteSpace(modifier.defaultOperation);
        }

        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig tech = config.technologies[i];
            bool isStarterTech = tech != null && tech.id == "basic_airship";
            techValid &= tech != null &&
                !string.IsNullOrWhiteSpace(tech.id) &&
                !string.IsNullOrWhiteSpace(tech.localNameRu) &&
                !string.IsNullOrWhiteSpace(tech.localNameEn) &&
                tech.rank >= 0 &&
                !string.IsNullOrWhiteSpace(tech.branch) &&
                !string.IsNullOrWhiteSpace(tech.categoryId) &&
                !string.IsNullOrWhiteSpace(tech.CategoryDisplayNameRu) &&
                tech.treeColumn >= 0 &&
                tech.treeRow >= 0 &&
                !string.IsNullOrWhiteSpace(tech.iconText) &&
                !string.IsNullOrWhiteSpace(tech.unlockSummaryRu) &&
                tech.cycleTimeSeconds >= 0 &&
                tech.requiredCycles > 0 &&
                TechnologySpCostsValid(tech) &&
                AllIdsExistAllowEmpty(tech.prerequisiteTechnologyIds, config.GetTechnology) &&
                ItemAmountsReferenceExistingItems(tech.cycleCost, c => c.itemId, c => c.amount, config) &&
                TechnologyModifierGrantsValid(tech, config);

            if (!isStarterTech)
            {
                techValid &= tech.requiredCycles == 5 &&
                    tech.cycleTimeSeconds == 0 &&
                    tech.treeColumn >= 1 &&
                    tech.treeColumn <= 5 &&
                    tech.modifierGrants != null &&
                    tech.modifierGrants.Count > 0 &&
                    tech.cycleCost != null &&
                    tech.cycleCost.Count > 0;
            }
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
                module.cargoVanCapacityKg >= 0f &&
                module.bulkHoldCapacityKg >= 0f &&
                module.liquidTankCapacityKg >= 0f &&
                module.gasCylinderCapacityKg >= 0f &&
                module.miningImpactDamageTakenMultiplier >= 0f &&
                techReferenceOk;

            if (module != null)
            {
                hasCargoStorageModule |= module.cargoVanCapacityKg > 0f ||
                    module.bulkHoldCapacityKg > 0f ||
                    module.liquidTankCapacityKg > 0f ||
                    module.gasCylinderCapacityKg > 0f;
            }
        }

        report.Check(techValid && modifierCatalogValid && techTreeValid,
            "Knowledge config has rubrics, 5-level nodes, SP costs, resource costs, locks/books and modifier grants without timer cycles.");
        report.Check(techValid && hasCargoStorageModule, "Starter modules can define shared cargo capacity without legacy social-service or dock-slot modules.");
    }

    private static bool TechnologyTreeMetadataValid(SessionConfigDatabase config)
    {
        if (config == null || config.technologies == null || config.technologies.Count == 0) return false;

        return config.GetTechnology("basic_airship") != null &&
            config.GetModifierDefinition("research_speed") != null &&
            CountTechnologyCategories(config) >= 5 &&
            TechnologyCategoryExists(config, "base") &&
            TechnologyCategoryExists(config, "production") &&
            TechnologyCategoryExists(config, "ships") &&
            TechnologyCategoryExists(config, "logistics") &&
            TechnologyCategoryExists(config, "research") &&
            TechnologyFiveLevelBoardExists(config) &&
            TechnologyRanksRespectPrerequisites(config) &&
            !TechnologyTreeHasCycles(config);
    }

    private static bool TechnologyModifierGrantsValid(TechnologyConfig technology, SessionConfigDatabase config)
    {
        if (technology == null || config == null || technology.modifierGrants == null)
        {
            return false;
        }

        for (int i = 0; i < technology.modifierGrants.Count; i++)
        {
            TechnologyModifierGrantConfig grant = technology.modifierGrants[i];
            if (grant == null ||
                string.IsNullOrWhiteSpace(grant.modifierId) ||
                config.GetModifierDefinition(grant.modifierId) == null ||
                string.IsNullOrWhiteSpace(grant.operation))
            {
                return false;
            }

            if (grant.operation != "unlock" && Mathf.Abs(grant.valuePerLevel) <= 0.0001f)
            {
                return false;
            }
        }

        return technology.id == "basic_airship" || technology.modifierGrants.Count > 0;
    }

    private static bool TechnologySpCostsValid(TechnologyConfig technology)
    {
        if (technology == null || technology.spCostByLevel == null)
        {
            return false;
        }

        if (technology.id == "basic_airship")
        {
            return technology.spCostByLevel.Count == 0 ||
                (technology.spCostByLevel.Count == 1 && technology.spCostByLevel[0] == 0);
        }

        if (technology.spCostByLevel.Count < 5)
        {
            return false;
        }

        int previous = 0;
        for (int i = 0; i < 5; i++)
        {
            int cost = technology.spCostByLevel[i];
            if (cost <= previous)
            {
                return false;
            }

            previous = cost;
        }

        return technology.spCostByLevel[0] == 1000 &&
            technology.spCostByLevel[1] == 5000 &&
            technology.spCostByLevel[2] == 20000 &&
            technology.spCostByLevel[3] == 80000 &&
            technology.spCostByLevel[4] >= 250000;
    }

    private static int CountTechnologyCategories(SessionConfigDatabase config)
    {
        if (config == null || config.technologies == null)
        {
            return 0;
        }

        HashSet<string> categories = new HashSet<string>();
        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.categoryId) || technology.categoryId == "starter")
            {
                continue;
            }

            categories.Add(technology.categoryId);
        }

        return categories.Count;
    }

    private static bool TechnologyCategoryExists(SessionConfigDatabase config, string categoryId)
    {
        if (config == null || config.technologies == null || string.IsNullOrWhiteSpace(categoryId))
        {
            return false;
        }

        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            if (technology != null && technology.categoryId == categoryId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TechnologyFiveLevelBoardExists(SessionConfigDatabase config)
    {
        if (config == null || config.technologies == null)
        {
            return false;
        }

        int fiveLevelNodes = 0;
        for (int i = 0; i < config.technologies.Count; i++)
        {
            TechnologyConfig technology = config.technologies[i];
            if (technology == null || technology.id == "basic_airship")
            {
                continue;
            }

            if (technology.requiredCycles == 5 &&
                technology.treeColumn >= 1 &&
                technology.treeColumn <= 5 &&
                technology.treeRow >= 0)
            {
                fiveLevelNodes++;
            }
        }

        return fiveLevelNodes >= 15;
    }

    private static bool TechnologyBranchExists(SessionConfigDatabase config, string branch)
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

    private static bool TechnologyRanksRespectPrerequisites(SessionConfigDatabase config)
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

    private static bool TechnologyTreeHasCycles(SessionConfigDatabase config)
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

    private static bool TechnologyVisitHasCycle(TechnologyConfig technology, SessionConfigDatabase config, HashSet<string> visiting, HashSet<string> visited)
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

    private static void ValidateCargoStorageModel(SessionConfigDatabase config, BigTestReport report)
    {
        report.Section("Р“СЂСѓР·РѕРІС‹Рµ РµРґРёРЅРёС†С‹ Рё РѕС‚СЃРµРєРё РєРѕСЂР°Р±Р»РµР№");

        if (config == null || !config.isLoaded)
        {
            report.Fail("Cargo storage checks stopped: config database is not loaded.");
            return;
        }

        ItemConfig water = config.GetItem("water");
        ItemConfig sampleOre = config.GetItem("windshale_ore");
        ItemConfig carcass = config.GetItem("windcalf_carcass");
        string itemCsvText = ReadProjectText("Assets/Data/Config/Item.csv");
        bool cargoMetadataValid =
            config.GetItem("passengers_to_capital") == null &&
            config.GetItem("passengers_to_island") == null &&
            config.GetItem("passengers_to_ship") == null &&
            water != null &&
            Approximately(water.massKgPerUnit, 1f, 0.001f) &&
            sampleOre != null &&
            Approximately(sampleOre.massKgPerUnit, 1f, 0.001f) &&
            carcass != null &&
            Approximately(carcass.massKgPerUnit, 1f, 0.001f) &&
            !itemCsvText.Contains("cargo_unit_kind") &&
            !itemCsvText.Contains("cargo_storage_kind") &&
            !itemCsvText.Contains("energy_kwh_per_kg");
        report.Check(cargoMetadataValid,
            "Item.csv keeps only item identity and per-unit mass; obsolete cargo unit/storage and item energy columns are removed.");

        List<CargoCompartmentDefinition> cargoCompartments = new List<CargoCompartmentDefinition>
        {
            new CargoCompartmentDefinition { storageKind = CargoStorageKind.Van, capacity = 200f }
        };
        Dictionary<string, int> typedCargo = new Dictionary<string, int>
        {
            ["charcoal"] = 10,
            ["windshale_ore"] = 12,
            ["dawnspar_ore"] = 8,
            ["water"] = 10,
            ["aerosil"] = 5,
            ["windcalf_carcass"] = 80
        };
        bool cargoFits = CargoStoragePlanner.TryValidateCargoStorage(config, cargoCompartments, typedCargo, out _);
        float typedCargoMass = CargoStoragePlanner.GetCargoMassKg(config, typedCargo);
        Dictionary<string, int> mixedBulkCargo = new Dictionary<string, int>
        {
            ["windshale_ore"] = 10,
            ["dawnspar_ore"] = 10,
            ["bluebrass_ore"] = 10
        };
        Dictionary<string, int> genericCargoOverflow = new Dictionary<string, int> { ["water"] = 201 };
        bool cargoLimitsWork = cargoFits &&
            Mathf.Abs(typedCargoMass - 125f) <= 0.001f &&
            CargoStoragePlanner.TryValidateCargoStorage(config, cargoCompartments, mixedBulkCargo, out _) &&
            !CargoStoragePlanner.TryValidateCargoStorage(config, cargoCompartments, genericCargoOverflow, out _);
        report.Check(cargoLimitsWork,
            "Cargo planning checks the shared van weight limit without old passenger/cabin route cargo.");

        PlayerProgress tankProgress = new PlayerProgress();
        tankProgress.Normalize();
        tankProgress.AddShipCargo("charcoal", 10);
        tankProgress.shipFuelTank.Add("charcoal", 30f, 50f);
        tankProgress.shipClaudiumTank.Add("claudium", 12.5f, 25f);
        bool internalTanksAreSeparate =
            tankProgress.GetShipCargoAmount("charcoal") == 10 &&
            Approximately(tankProgress.GetShipCargoMassKg(config), 10f, 0.001f) &&
            Approximately(tankProgress.GetShipPayloadMassKg(config), 52.5f, 0.001f) &&
            tankProgress.shipFuelTank.TrySpend("charcoal", 5.5f) &&
            tankProgress.GetShipCargoAmount("charcoal") == 10 &&
            Approximately(tankProgress.shipFuelTank.GetAmount("charcoal"), 24.5f, 0.001f);
        report.Check(internalTanksAreSeparate,
            "Fuel and claudium tanks count as ship payload but stay separate from cargo stacks.");

        PlayerProgress playerCargoProgress = new PlayerProgress();
        playerCargoProgress.Normalize();
        playerCargoProgress.AddShipCargo("water", 10);
        playerCargoProgress.AddShipCargo("windshale_ore", 12);
        bool playerCargoUsesConfigMass = Mathf.Abs(playerCargoProgress.GetShipCargoMassKg(config) - 22f) <= 0.001f &&
            CargoStoragePlanner.TryValidateCargoStorage(config, cargoCompartments, playerCargoProgress.shipCargo, out _);
        report.Check(playerCargoUsesConfigMass,
            "Player ship cargo mass is computed through Item.csv and passes the same van compartment check.");
    }

    private static void ValidateLegacyShipAssetCleanup(SessionConfigDatabase config, BigTestReport report)
    {
        report.Section("Legacy ship asset cleanup");

        bool configClean = config != null &&
            config.isLoaded &&
            config.shipTreeEntries.Count == 3 &&
            config.hulls.Count == 0 &&
            config.claudiumLoops.Count == 0;
        report.Check(configClean,
            "Runtime ship config keeps only the three current playable catalog rows and no legacy hull or claudium-loop rows.");

        string[] removedPaths =
        {
            "Assets/Data/ShipCatalog.asset",
            "Assets/Data/ShipCatalog.asset.meta",
            "Assets/Data/ShipParts",
            "Assets/Data/ShipParts.meta",
            "Assets/Data/ShipPrefabs",
            "Assets/Data/ShipPrefabs.meta"
        };

        List<string> leftovers = new List<string>();
        for (int i = 0; i < removedPaths.Length; i++)
        {
            string path = removedPaths[i];
            if (File.Exists(ProjectPath(path)) || Directory.Exists(ProjectPath(path)))
            {
                leftovers.Add(path);
            }
        }

        string shipCatalogText = ReadProjectText("Assets/Data/Config/Ship_catalog.csv");
        bool csvTextClean =
            !File.Exists(ProjectPath("Assets/Data/Config/" + "Ship_" + "tree.csv")) &&
            CountCsvDataRows(shipCatalogText) == 3 &&
            shipCatalogText.Contains("capital_patrol_frigate_r02") &&
            shipCatalogText.Contains("capital_artillery_cruiser_r02") &&
            shipCatalogText.Contains("capital_heavy_battleship_r02");

        report.Check(leftovers.Count == 0 && csvTextClean,
            leftovers.Count == 0 && csvTextClean
                ? "Old generated ship catalog assets, ship prefabs, ship parts and runtime CSV rows are gone."
                : "Old ship leftovers remain: "
                    + (leftovers.Count == 0 ? "no asset paths" : string.Join(", ", leftovers))
                    + ", csvClean="
                    + csvTextClean);
    }

    private static int CountCsvDataRows(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return 0;
        }

        string[] lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int rows = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                rows++;
            }
        }

        return rows;
    }

            #if UNITY_EDITOR
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
        string fullPath = ProjectPath(assetPath);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
    }

    private static bool UrpAssetUsesLeanRuntimeDefaults(string assetText)
    {
        return assetText.Contains("m_RequireDepthTexture: 0") &&
            assetText.Contains("m_RequireOpaqueTexture: 0") &&
            assetText.Contains("m_SupportsHDR: 0") &&
            assetText.Contains("m_MainLightShadowsSupported: 0") &&
            assetText.Contains("m_AdditionalLightsRenderingMode: 0") &&
            assetText.Contains("m_AdditionalLightsPerObjectLimit: 0") &&
            assetText.Contains("m_ShadowDistance: 0") &&
            assetText.Contains("m_AnyShadowsSupported: 0") &&
            assetText.Contains("m_UseAdaptivePerformance: 0");
    }

    private static bool VisualTargetRendererAvoidsAeroFullscreenPass(string assetText)
    {
        return assetText.Contains("m_IntermediateTextureMode: 0") &&
            !HasActiveAeroFullscreenPass(assetText);
    }

    private static bool HasActiveAeroFullscreenPass(string assetText)
    {
        const string marker = "m_Name: AERO Volumetric Fog";
        int markerIndex = assetText.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        int blockStart = assetText.LastIndexOf("--- !u!", markerIndex, StringComparison.Ordinal);
        if (blockStart < 0)
        {
            blockStart = markerIndex;
        }

        int blockEnd = assetText.IndexOf("\n--- !u!", markerIndex, StringComparison.Ordinal);
        if (blockEnd < 0)
        {
            blockEnd = assetText.Length;
        }

        string block = assetText.Substring(blockStart, blockEnd - blockStart);
        return block.Contains("FullScreenPassRendererFeature") &&
            block.Contains("m_Active: 1");
    }

    private static string ProjectPath(string assetPath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
    }

    private static bool HasActiveLegacyShipPrimitive(string sceneText)
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

    private static void ValidateSessionPortConfig(SessionConfigDatabase config, BigTestReport report)
    {
        if (config == null || !config.isLoaded)
        {
            report.Fail("Session port config checks stopped: config database is not loaded.");
            return;
        }

        PortConfig capital = config.GetPort("capital");
        bool capitalOnlyPort = config.ports.Count == 1 &&
            capital != null &&
            capital.id == "capital" &&
            capital.dockingRadius >= 100f &&
            capital.position.y >= 1000f;
        report.Check(capitalOnlyPort,
            "Session config keeps one isolated capital port and no reachable legacy island chain.");

        bool legacyIslandCsvsRemoved =
            !File.Exists(ProjectPath("Assets/Data/Config/Island_production.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Production_industry.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Production_recipe.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_archetype.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_archetype_stage.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_social_need.csv")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Island_building.csv"));
        report.Check(legacyIslandCsvsRemoved,
            "Legacy island production, development and society CSV files are absent.");
    }

    private void ValidateSessionLegacyCleanup(SessionConfigDatabase config, BigTestReport report)
    {
        report.Section("Session legacy cleanup");

        string playerProgressText = ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs");
        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string metaTimeText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.Time.cs");
        string configText = ReadProjectText("Assets/Scripts/Data/SessionConfigDatabase.cs");
        string gameplaySessionText = ReadProjectText("Assets/Scripts/Session/WildWindGameplaySession.cs");
        string cameraText = ReadProjectText("Assets/Scripts/Session/WildWindSessionCameraController.cs");
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string retiredDockRuntimeText = ReadProjectText("Assets/Scripts/Meta/WildWind" + "Meta" + "Port" + "RuntimeModel.cs");
        string sceneText = ReadProjectText("Assets/Scenes/WildWindSessionScene.unity");
        string projectText = ReadProjectText("Assembly-CSharp.csproj");

        bool islandRuntimeSourcesRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/IslandDevelopmentSimulator.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/IslandIndustrySimulator.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/IslandSocietySimulator.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/CapitalResearchStation.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/WorldConfigIndustry.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/WorldConfigIslandDevelopment.cs"));
        report.Check(islandRuntimeSourcesRemoved,
            "Legacy island production, industry, society and research station runtime sources are removed.");

        bool progressLegacyFieldsRemoved =
            !playerProgressText.Contains("TimedProcessState") &&
            !playerProgressText.Contains("TimedProcessKind") &&
            !playerProgressText.Contains("CargoTransferState") &&
            !playerProgressText.Contains("CargoTransferOperation") &&
            !playerProgressText.Contains("activeProcesses") &&
            !playerProgressText.Contains("cargoTransfer") &&
            !playerProgressText.Contains("nextShopRefreshUtcTicks") &&
            !playerProgressText.Contains("shopSeed") &&
            !playerProgressText.Contains("lastSavedUtcTicks") &&
            !playerProgressText.Contains("shipImpactCargo") &&
            !playerProgressText.Contains("ShipImpactCargo") &&
            !playerProgressText.Contains("sessionExtractionCoreMode") &&
            !playerProgressText.Contains("GasCloud,") &&
            !playerProgressText.Contains("ScoutedObjectKind.GasCloud") &&
            !playerProgressText.Contains("ScoutedObjectKind") &&
            !playerProgressText.Contains("ScoutedObjectState") &&
            !playerProgressText.Contains("scoutedObjects") &&
            !playerProgressText.Contains("GasCloudState") &&
            !playerProgressText.Contains("MiningRockState") &&
            !playerProgressText.Contains("MiningZoneState") &&
            !playerProgressText.Contains("gasClouds") &&
            !playerProgressText.Contains("miningRocks") &&
            !playerProgressText.Contains("miningZones") &&
            !playerProgressText.Contains("DockingLocationKind") &&
            !playerProgressText.Contains("IslandSocietyNeedState") &&
            !playerProgressText.Contains("IslandDevelopmentState") &&
            !playerProgressText.Contains("IslandIndustryState");
        report.Check(progressLegacyFieldsRemoved,
            "PlayerProgress no longer serializes timed jobs, cargo transfers, shop refresh, ship-dock destinations or island development state.");

        bool metaLegacyApisRemoved =
            !metaText.Contains("StartIdleMining") &&
            !metaText.Contains("StartIronSmelting") &&
            !metaText.Contains("TryLoadShipCargoFromCurrentDock") &&
            !metaText.Contains("TryUnloadShipCargoToCurrentDock") &&
            !metaText.Contains("AdvanceCargoTransfer") &&
            !metaText.Contains("ShipImpactCargo") &&
            !metaText.Contains("AdvanceShopRefresh") &&
            !metaText.Contains("sessionExtractionCoreMode") &&
            !metaText.Contains("IsSessionExtractionCoreMode") &&
            !metaText.Contains("gasCloudManager") &&
            !metaText.Contains("miningRockManager") &&
            !metaText.Contains("leviathanManager") &&
            !metaText.Contains("AddComponent<GasCloudManager") &&
            !metaText.Contains("ClearRuntimeActors<GasCloud") &&
            !metaText.Contains("AddComponent<MiningRockManager") &&
            !metaText.Contains("AddComponent<LeviathanManager") &&
            !metaText.Contains("ClearRuntimeActors<Leviathan") &&
            !File.Exists(ProjectPath("Assets/Scripts/Systems/GasCloud.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Systems/GasCloudManager.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Systems/MiningRockManager.cs")) &&
            !projectText.Contains("GasCloud.cs") &&
            !projectText.Contains("GasCloudManager.cs") &&
            !projectText.Contains("MiningRockManager.cs") &&
            !projectText.Contains("SurveySystem.cs") &&
            !shipPhysicsText.Contains("class LeviathanManager") &&
            !shipPhysicsText.Contains("public class Leviathan") &&
            !shipPhysicsText.Contains("public class HarpoonTether") &&
            !shipPhysicsText.Contains("TryFireHarpoonAt") &&
            !shipPhysicsText.Contains("activeHarpoon") &&
            !metaText.Contains("IslandProductionSimulator") &&
            !metaText.Contains("IslandIndustrySimulator") &&
            !metaText.Contains("IslandSocietySimulator") &&
            !metaText.Contains("PassengerTrafficSimulator");
        report.Check(metaLegacyApisRemoved,
            "MetaGameState exposes no legacy dock jobs, island cargo-transfer or island simulation APIs.");

        bool offlineSaveCatchupRemoved =
            !metaTimeText.Contains("processOfflineProgressOnLoad") &&
            !metaTimeText.Contains("maxOfflineCatchUpHours") &&
            !metaTimeText.Contains("TryAdvanceOfflineProgressFromLastSave") &&
            !metaText.Contains("TryAdvanceOfflineProgressFromLastSave");
        report.Check(offlineSaveCatchupRemoved,
            "Single-account runtime has no automatic offline catch-up from a previous save timestamp.");

        bool configLegacyTypesRemoved =
            !configText.Contains("IslandProductionConfig") &&
            !configText.Contains("IslandIndustryConfig") &&
            !configText.Contains("IndustryRecipeConfig") &&
            !configText.Contains("IslandArchetype") &&
            !configText.Contains("IslandSocialNeed") &&
            !configText.Contains("IslandBuildingConfig") &&
            !configText.Contains("GasCloudConfig") &&
            !configText.Contains("GasCloudTypeConfig") &&
            !configText.Contains("GasCloudCompositionConfig") &&
            !configText.Contains("gasCloudTypes") &&
            !configText.Contains("MiningZoneConfig") &&
            !configText.Contains("LeviathanZoneConfig") &&
            !configText.Contains("LoadGasClouds") &&
            !configText.Contains("LoadGasCloudTypes") &&
            !configText.Contains("LoadMiningZones") &&
            !configText.Contains("LoadLeviathanZones") &&
            !configText.Contains("observationLeviathanInfoEfficiency") &&
            !configText.Contains("leviathanAlarmGenerationMultiplier") &&
            !configText.Contains("harpoonWeaponCostPerMinute") &&
            !configText.Contains("harpoonRangeMeters") &&
            !configText.Contains("GetProduction") &&
            !configText.Contains("LoadProductions");
        report.Check(configLegacyTypesRemoved,
            "SessionConfigDatabase does not parse the removed island production/development config families.");

        bool sceneLegacySerializationRemoved =
            !sceneText.Contains("nextShopRefreshUtcTicks") &&
            !sceneText.Contains("shopSeed") &&
            !sceneText.Contains("activeProcesses") &&
            !sceneText.Contains("cargoTransfer:") &&
            !sceneText.Contains("idleMiningIntervalSeconds") &&
            !sceneText.Contains("ironSmeltingDurationSeconds") &&
            !sceneText.Contains("defaultTimedMissionDurationSeconds") &&
            !sceneText.Contains("missionController:") &&
            !sceneText.Contains("logisticsFleet:") &&
            !sceneText.Contains("gasHarvesterFleet:") &&
            !sceneText.Contains("gasHarvester") &&
            !sceneText.Contains("observationRockInfoEfficiency") &&
            !sceneText.Contains("observationCloudInfoEfficiency") &&
            !sceneText.Contains("observationLeviathanInfoEfficiency") &&
            !sceneText.Contains("baseObservationRadiusMeters") &&
            !sceneText.Contains("observationRadiusMeters") &&
            !sceneText.Contains("observationFactsAtHalfRadiusPerSecond") &&
            !sceneText.Contains("surveyPaperToInfoEfficiency") &&
            !sceneText.Contains("leviathanAlarmGenerationMultiplier") &&
            !sceneText.Contains("harpoon") &&
            !sceneText.Contains("activeHarpoon") &&
            !sceneText.Contains("scoutedObjects") &&
            !sceneText.Contains("miningFleet:") &&
            !sceneText.Contains("scoutFleet:") &&
            !sceneText.Contains("islandProductionEnabled") &&
            !sceneText.Contains("spawnConfigIslandsOnPlay") &&
            !sceneText.Contains("startingOre") &&
            !sceneText.Contains("startingIron") &&
            !sceneText.Contains("startingPaperKg") &&
            !sceneText.Contains("lastSavedUtcTicks") &&
            !sceneText.Contains("shipImpactCargo") &&
            !sceneText.Contains("sessionExtractionCoreMode") &&
            !sceneText.Contains("gasClouds:") &&
            !sceneText.Contains("miningRocks:") &&
            !sceneText.Contains("miningZones:") &&
            !sceneText.Contains("gasCloudManager:") &&
            !sceneText.Contains("miningRockManager:") &&
            !sceneText.Contains("leviathanManager:") &&
            !sceneText.Contains("Data Cloud Field") &&
            !sceneText.Contains("Data Leviathan Region") &&
            !sceneText.Contains("Leviathan Region") &&
            !sceneText.Contains("Leviathan Territory") &&
            !sceneText.Contains("Cloud Field -") &&
            !sceneText.Contains("Cloud Volume") &&
            !sceneText.Contains("dockKind:") &&
            !sceneText.Contains("currentDockKind:") &&
            !sceneText.Contains("startingDockKind:") &&
            !sceneText.Contains("launchedFromDockKind:") &&
            !sceneText.Contains("processOfflineProgressOnLoad") &&
            !sceneText.Contains("maxOfflineCatchUpHours");
        report.Check(sceneLegacySerializationRemoved,
            "Session scene no longer serializes removed shop, timed-process, fleet, mission, cargo-transfer, dock-kind, offline-save-catchup or island-production fields.");

        bool sessionVocabularyClean =
            !metaText.Contains("SpawnConfiguredWorldActors") &&
            !metaText.Contains("ClearConfiguredWorldActors") &&
            !gameplaySessionText.Contains("WorldManifestData") &&
            !gameplaySessionText.Contains("WorldRegionRuntime") &&
            !cameraText.Contains("worldCamera") &&
            !retiredDockRuntimeText.Contains("worldHeightMeters") &&
            !sceneText.Contains("  world: {fileID:") &&
            !sceneText.Contains("  runtimeState: {fileID:") &&
            !sceneText.Contains("  streamer: {fileID:") &&
            !sceneText.Contains("worldCamera:");
        report.Check(sessionVocabularyClean,
            "Session runtime no longer carries stale open-world serialized fields or misleading world-prefixed session identifiers.");

        bool sessionRenamedFilesActive =
            File.Exists(ProjectPath("Assets/Scenes/WildWindSessionScene.unity")) &&
            File.Exists(ProjectPath("Assets/Scripts/Data/SessionConfigDatabase.cs")) &&
            File.Exists(ProjectPath("Assets/Scripts/Data/SessionConfigShipParts.cs")) &&
            Directory.Exists(ProjectPath("Assets/Scripts/Session")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Systems/MiningResourceSimulator.cs")) &&
            !projectText.Contains("MiningResourceSimulator.cs") &&
            !File.Exists(ProjectPath("Assets/Scenes/WildWindWorldScene.unity")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/WorldConfigDatabase.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/WorldConfigShipParts.cs")) &&
            !Directory.Exists(ProjectPath("Assets/Scripts/World")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Systems/MiningWorldSimulator.cs"));
        report.Check(sessionRenamedFilesActive,
            "Session scene, runtime folder and config database use current session names, and the retired mining world simulator is absent.");

        bool projectLegacyReferencesRemoved =
            !projectText.Contains("IslandDevelopmentSimulator.cs") &&
            !projectText.Contains("IslandIndustrySimulator.cs") &&
            !projectText.Contains("IslandSocietySimulator.cs") &&
            !projectText.Contains("WorldConfigIndustry.cs") &&
            !projectText.Contains("WorldConfigIslandDevelopment.cs") &&
            !projectText.Contains("MissionDefinitionSO.cs") &&
            !projectText.Contains("FlagshipInteriorSimulator.cs") &&
            !projectText.Contains("WildWindWorldScene.unity") &&
            !projectText.Contains(@"Assets\Scripts\World\") &&
            !projectText.Contains("Assets/Scripts/World/") &&
            !projectText.Contains("WorldConfigDatabase.cs") &&
            !projectText.Contains("WorldConfigShipParts.cs") &&
            !projectText.Contains("MiningWorldSimulator.cs") &&
            !projectText.Contains("WildWindDesignMechanicsModel.cs") &&
            !projectText.Contains("VisualPlayModeTuner.cs") &&
            !projectText.Contains("DamageTestBench.cs");
        report.Check(projectLegacyReferencesRemoved,
            "C# project file no longer references removed legacy scripts.");

        if (config != null && config.isLoaded)
        {
            report.Check(config.ports.Count == 1 && config.GetPort("capital") != null,
                "Loaded config contains only the session port record.");
            report.Check(config.GetItem("passengers_to_capital") == null &&
                config.GetItem("passengers_to_island") == null &&
                config.GetItem("passengers_to_ship") == null,
                "Passenger cargo templates are removed from Item.csv with the old route economy.");
        }
    }
    private void ValidateSessionOnlyRuntimeContract(BigTestReport report)
    {
        report.Section("Session-only runtime");

        Scene scene = SceneManager.GetActiveScene();
        report.Check(scene.name == DefaultSessionSceneName,
            "Gameplay entry scene is loaded as the session scene: " + scene.name + ".");

        MetaGameState meta = FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            GameObject metaObject = new GameObject("MetaGameState");
            meta = metaObject.AddComponent<MetaGameState>();
        }

        meta.EnsureProgressInitialized();

        WildWindGameplaySession session = WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(meta, meta.RuntimeAccountId);
        WildWindGameplayHud hud = FindFirstObjectByType<WildWindGameplayHud>();
        WildWindGameplayMenu menu = FindFirstObjectByType<WildWindGameplayMenu>();
        WildWindFlightControlBridge bridge = FindFirstObjectByType<WildWindFlightControlBridge>();

        report.Check(meta.progress != null,
            "Session runtime owns progress without requiring open-world progress state or a core-mode toggle.");
        report.Check(session != null && session.IsReady,
            "GameplaySession is ready without open-world runtime objects.");
        report.Check(hud != null && hud.IsReady,
            "Gameplay HUD bootstraps from the session scene, not from a world runtime object.");
        report.Check(menu != null,
            "Gameplay menu bootstraps from the session scene, not from a world runtime object.");
        report.Check(bridge != null && bridge.IsReady,
            "Flight control bridge binds the session ship without a world runtime object.");

        report.Check(meta.CurrentMode == GameSessionMode.Docked
            && meta.progress.currentDockId == GameplaySessionAccountData.DefaultDockId,
            "New session starts docked at the base: " + meta.progress.currentDockId + ".");

#if UNITY_EDITOR
        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        string firstEnabledScene = "";
        int enabledSceneCount = 0;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            if (buildScenes[i] != null && buildScenes[i].enabled)
            {
                enabledSceneCount++;
                if (string.IsNullOrWhiteSpace(firstEnabledScene))
                {
                    firstEnabledScene = buildScenes[i].path;
                }
            }
        }

        report.Check(enabledSceneCount == 1 && firstEnabledScene == "Assets/Scenes/WildWindSessionScene.unity",
            "Build Settings contains only the session port scene: " + firstEnabledScene + " (" + enabledSceneCount + " enabled).");
        report.Check(!File.Exists(ProjectPath("Assets/Scenes/StartScreen.unity")),
            "StartScreen scene asset is removed from the session-only build.");
        report.Check(!File.Exists(ProjectPath("Assets/Scripts/UI/WildWindStartScreen.cs")),
            "StartScreen runtime script is removed from the session-only build.");

        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string sessionText = ReadProjectText("Assets/Scripts/Session/WildWindGameplaySession.cs");
        string flowText = ReadProjectText("Assets/Scripts/Session/WildWindSessionFlow.cs");
        bool noSessionAccountWorldManifest =
            !metaText.Contains("worldManifest") &&
            !metaText.Contains("worldRuntime") &&
            !sessionText.Contains("World" + "ManifestData") &&
            !sessionText.Contains("World" + "RegionRuntime") &&
            !flowText.Contains("world manifest");
        report.Check(noSessionAccountWorldManifest,
            "Session account contract no longer requires world manifest, world runtime state, chunks, or bubble streaming.");
        bool flowHasNoStartScreenContract =
            !flowText.Contains("StartScreen") &&
            !flowText.Contains("TryPrepareExistingWorldLaunch") &&
            !flowText.Contains("TryContinueWorldAndEnter") &&
            !flowText.Contains("TryCreateNewWorldAndEnter");
        report.Check(flowHasNoStartScreenContract,
            "SessionFlow has no start screen, save-slot selection, or pending gameplay launch contract.");
#else
        report.Check(true, "Session account source scan is editor-only and skipped in player builds.");
#endif
    }

    private void ValidateRuntimeAccount(BigTestReport report)
    {
        report.Section("Runtime account");

        MetaGameAccountData generatedAccount = WildWindRuntimeAccountFactory.BuildNewAccountData();
        bool generatedUsable = generatedAccount != null &&
            generatedAccount.version == MetaGameAccountData.CurrentVersion &&
            generatedAccount.progress != null &&
            generatedAccount.gameplaySession != null &&
            generatedAccount.gameplaySession.IsUsable &&
            generatedAccount.gameplaySession.accountId == GameplaySessionAccountData.DefaultAccountId &&
            generatedAccount.gameplaySession.mode == GameSessionMode.Docked &&
            generatedAccount.gameplaySession.dockId == GameplaySessionAccountData.DefaultDockId &&
            generatedAccount.progress.hasCurrentDockPosition;
        report.Check(generatedUsable, "Fresh runtime account contains progress, selected hull, base dock pose, and gameplay session data.");

        MetaGameState runtimeMeta = FindFirstObjectByType<MetaGameState>();
        if (runtimeMeta == null)
        {
            GameObject metaObject = new GameObject("Runtime Account Probe MetaGameState");
            runtimeMeta = metaObject.AddComponent<MetaGameState>();
        }

        runtimeMeta.EnsureProgressInitialized();
        MetaGameAccountData runtimeSnapshot = runtimeMeta.CreateRuntimeAccountData();
        bool runtimeSnapshotUsable = runtimeSnapshot != null &&
            runtimeSnapshot.version == MetaGameAccountData.CurrentVersion &&
            runtimeSnapshot.progress == runtimeMeta.progress &&
            runtimeSnapshot.gameplaySession != null &&
            runtimeSnapshot.gameplaySession.IsUsable &&
            runtimeSnapshot.gameplaySession.accountId == runtimeMeta.RuntimeAccountId;
        report.Check(runtimeSnapshotUsable,
            "MetaGameState creates a runtime account snapshot for diagnostics from the current progress state.");
        string normalProgressPath = MetaGameState.GetPersistentProgressSavePathForTests(false);
        string bigTestProgressPath = MetaGameState.GetPersistentProgressSavePathForTests(true);
        report.Check(!string.Equals(normalProgressPath, bigTestProgressPath, StringComparison.OrdinalIgnoreCase)
                && runtimeMeta.IsUsingBigTestPersistentProgressForTests
                && string.Equals(runtimeMeta.PersistentProgressSavePathForTests, bigTestProgressPath, StringComparison.OrdinalIgnoreCase),
            "Big Test uses an isolated persistent progress save file and cannot consume the player's real long-term progress save.");

#if UNITY_EDITOR
        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string sessionText = ReadProjectText("Assets/Scripts/Session/WildWindGameplaySession.cs");
        string bootstrapText = ReadProjectText("Assets/Scripts/Session/WildWindGameplayBootstrap.cs");
        string flowText = ReadProjectText("Assets/Scripts/Session/WildWindSessionFlow.cs");
        bool persistentProgressStorageScoped =
            !File.Exists(ProjectPath("Assets/Scripts/Session/WildWindAccountStorage.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Session/WildWindAccountStorage.cs.meta")) &&
            metaText.Contains("PersistentProgressSaveFileName") &&
            metaText.Contains("PersistentProgressBigTestSaveFileName") &&
            metaText.Contains("IsProgressSandboxActive") &&
            metaText.Contains("Application.persistentDataPath") &&
            metaText.Contains("File.WriteAllText") &&
            metaText.Contains("File.ReadAllText") &&
            metaText.Contains("Directory.CreateDirectory") &&
            metaText.Contains("DeletePersistentProgressSaveFile") &&
            !metaText.Contains("FindFirstObjectByType<WildWindBigTestRunner>() != null") &&
            !metaText.Contains("AccountPath") &&
            !metaText.Contains("EffectiveAccountFileName") &&
            !metaText.Contains("TryPersistAccount") &&
            !metaText.Contains("LoadAccount") &&
            !metaText.Contains("autoPersistAccountOnDock") &&
            !metaText.Contains("loadAccountOnAwake") &&
            !metaText.Contains("accountFileName") &&
            !metaText.Contains("wild_wind_account") &&
            !sessionText.Contains("Account File") &&
            !sessionText.Contains("accountFileName") &&
            !sessionText.Contains("savedUtcTicks") &&
            !bootstrapText.Contains("AssetDatabase") &&
            !bootstrapText.Contains("ShipCatalogPath") &&
            !bootstrapText.Contains("LoadAccount") &&
            !flowText.Contains("TryPersistAccount");
        report.Check(persistentProgressStorageScoped,
            "Runtime progress persistence is scoped to MetaGameState JSON saves and does not restore the removed legacy account storage flow.");
#else
        report.Check(true, "Runtime account source scan is editor-only and skipped in player builds.");
#endif
    }
    private void ValidateVisualAtmosphere(BigTestReport report)
    {
        report.Section("Р’РёР·СѓР°Р», РІС‹СЃРѕС‚РЅС‹Рµ СЃР»РѕРё Рё С‚СѓРјР°РЅ");
        report.Check(sessionAtmosphereTuner != null, sessionAtmosphereTuner != null ? "SessionAtmosphereTuner РЅР°Р№РґРµРЅ." : "SessionAtmosphereTuner РЅРµ РЅР°Р№РґРµРЅ.");
        if (sessionAtmosphereTuner == null)
        {
            return;
        }

        report.Info(sessionAtmosphereTuner.GetAtmosphereStatusText());

        float transitionHalfWidth = ReadPrivateFloat(sessionAtmosphereTuner, "altitudeTransitionHalfWidth", -1f);
        float violentVisibility = ReadPrivateFloat(sessionAtmosphereTuner, "violentStormVisibility", -1f);
        float calmVisibility = ReadPrivateFloat(sessionAtmosphereTuner, "calmStormVisibility", -1f);
        float deadlyDrawDistance = ReadPrivateFloat(sessionAtmosphereTuner, "deadlyStormDrawDistance", -1f);
        bool useAltitudeAtmosphere = ReadPrivateBool(sessionAtmosphereTuner, "useAltitudeAtmosphere", false);
        bool leanRuntimeAtmosphere = ReadPrivateBool(sessionAtmosphereTuner, "leanRuntimeAtmosphere", false);
        bool useUnityFogFallback = ReadPrivateBool(sessionAtmosphereTuner, "useUnityFogFallback", false);

        report.Check(leanRuntimeAtmosphere && useUnityFogFallback,
            "Runtime atmosphere uses a lean Unity fog fallback by default instead of always running expensive AERO/TrueClouds passes.");
        sessionAtmosphereTuner.ApplyNow();
        report.Check(sessionAtmosphereTuner.IsLeanRuntimeAtmosphereActiveForTests
            && sessionAtmosphereTuner.EnabledTrueCloudsBehaviourCountForTests == 0
            && !sessionAtmosphereTuner.IsAeroFogControllerEnabledForTests
            && !sessionAtmosphereTuner.IsCameraPostProcessingEnabledForTests,
            "Lean runtime atmosphere keeps TrueClouds, AERO and camera post-processing disabled during the docked/start presentation.");
        bool cityMeshRendererOverride = sessionAtmosphereTuner.IsDockedCityMeshRendererOverrideActiveForTests;
        report.Check(sessionAtmosphereTuner.IsCameraUsingLeanRendererForTests,
            cityMeshRendererOverride
                ? "Lean runtime keeps post effects disabled but allows the docked 3D city to use a mesh-compatible Universal renderer: "
                    + sessionAtmosphereTuner.CameraRendererNameForTests + "."
                : "Lean runtime camera keeps a mesh-compatible Universal renderer so the flight skybox remains visible: "
                    + sessionAtmosphereTuner.CameraRendererNameForTests + ".");
        Camera atmosphereCamera = Camera.main;
        report.Check(atmosphereCamera == null || atmosphereCamera.clearFlags == CameraClearFlags.Skybox,
            atmosphereCamera != null
                ? "Lean runtime camera still clears to the skybox after atmosphere tuning."
                : "Lean runtime skybox clear check skipped because MainCamera is missing.");

        string universalRpAssetText = ReadProjectText("Assets/Settings/UniversalRP.asset");
        string visualTargetRendererText = ReadProjectText("Assets/Data/VisualTarget/VisualTargetUniversalRenderer.asset");
        report.Check(UrpAssetUsesLeanRuntimeDefaults(universalRpAssetText),
            "UniversalRP asset does not globally force depth texture, opaque texture, HDR, adaptive performance, shadows or additional per-object lights.");
        report.Check(VisualTargetRendererAvoidsAeroFullscreenPass(visualTargetRendererText),
            "VisualTarget renderer keeps the AERO fullscreen pass inactive and no longer forces an intermediate texture.");

        report.Check(useAltitudeAtmosphere, "Р’С‹СЃРѕС‚РЅРѕРµ СѓРїСЂР°РІР»РµРЅРёРµ AERO-С‚СѓРјР°РЅРѕРј РІРєР»СЋС‡РµРЅРѕ.");
        report.Check(Approximately(transitionHalfWidth, 50f, 0.5f), "РџР»Р°РІРЅС‹Р№ РїРµСЂРµС…РѕРґ РІС‹СЃРѕС‚РЅС‹С… Р·РѕРЅ РґРµСЂР¶РёС‚СЃСЏ РѕРєРѕР»Рѕ +-50 Рј: " + transitionHalfWidth.ToString("0.#") + " Рј.");
        report.Check(violentVisibility > 0f && violentVisibility <= 150f, "Р’РёРґРёРјРѕСЃС‚СЊ СЏСЂРѕСЃС‚РЅРѕР№ Р±СѓСЂРё РѕРіСЂР°РЅРёС‡РµРЅР° РїСЂРёРјРµСЂРЅРѕ 100 Рј: " + violentVisibility.ToString("0.#") + " Рј.");
        report.Check(calmVisibility >= 800f && calmVisibility <= 1300f, "Р’РёРґРёРјРѕСЃС‚СЊ СЃРїРѕРєРѕР№РЅРѕР№ Р±СѓСЂРё РѕРєРѕР»Рѕ 1000 Рј: " + calmVisibility.ToString("0.#") + " Рј.");
        report.Check(deadlyDrawDistance > 0f && deadlyDrawDistance <= 120f, "РџРѕРІРµСЂС…РЅРѕСЃС‚СЊ СЃРјРµСЂС‚РµР»СЊРЅРѕР№ Р±СѓСЂРё СЂРёСЃСѓРµС‚СЃСЏ С‚РѕР»СЊРєРѕ РІР±Р»РёР·Рё: " + deadlyDrawDistance.ToString("0.#") + " Рј.");

        GameObject stormSurface = FindSceneGameObjectIncludingInactive("Deadly Storm Surface Local Bubble");
        report.Check(stormSurface != null, stormSurface != null ? "Р›РѕРєР°Р»СЊРЅР°СЏ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ СЃРјРµСЂС‚РµР»СЊРЅРѕР№ Р±СѓСЂРё РЅР°Р№РґРµРЅР°." : "Р›РѕРєР°Р»СЊРЅР°СЏ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ СЃРјРµСЂС‚РµР»СЊРЅРѕР№ Р±СѓСЂРё РЅРµ РЅР°Р№РґРµРЅР°.");
        if (stormSurface != null)
        {
            Renderer renderer = stormSurface.GetComponent<Renderer>();
            report.Check(renderer != null && renderer.sharedMaterial != null, "РЈ РїРѕРІРµСЂС…РЅРѕСЃС‚Рё Р±СѓСЂРё РЅР°Р·РЅР°С‡РµРЅ РјР°С‚РµСЂРёР°Р».");
            report.Check(!leanRuntimeAtmosphere || !stormSurface.activeSelf,
                "Lean runtime keeps the expensive deadly storm surface inactive unless the non-lean atmosphere stack is explicitly enabled.");
            if (renderer != null && renderer.sharedMaterial != null)
            {
                report.Info("РњР°С‚РµСЂРёР°Р» Р±СѓСЂРё: " + renderer.sharedMaterial.name + ", shader=" + renderer.sharedMaterial.shader.name + ".");
            }
        }

        if (GameObject.Find("AERO Visual Fog Controller") == null)
        {
            report.Warn("AERO Visual Fog Controller РЅРµ РЅР°Р№РґРµРЅ РІ СЃС†РµРЅРµ. Р•СЃР»Рё С‚СѓРјР°РЅ РІРёРґРµРЅ С‡РµСЂРµР· Renderer Feature, СЌС‚Рѕ РјРѕР¶РµС‚ Р±С‹С‚СЊ РЅРѕСЂРјР°Р»СЊРЅРѕ, РЅРѕ СЃС‚РѕРёС‚ РїСЂРѕРІРµСЂРёС‚СЊ СЃС†РµРЅСѓ РіР»Р°Р·Р°РјРё.");
        }
        else
        {
            report.Pass("AERO Visual Fog Controller РЅР°Р№РґРµРЅ РІ СЃС†РµРЅРµ.");
        }
    }

    private void ValidateSettings(BigTestReport report)
    {
        report.Section("РќР°СЃС‚СЂРѕР№РєРё РїСЂРѕРµРєС‚Р° Рё СѓРїСЂР°РІР»РµРЅРёРµ");
        report.Check(settings != null, settings != null ? "WildWindSettingsRoot РЅР°Р№РґРµРЅ." : "WildWindSettingsRoot РЅРµ РЅР°Р№РґРµРЅ.");
        WildWindControlSettings controls = settings != null ? settings.Controls : FindFirstObjectByType<WildWindControlSettings>();
        report.Check(controls != null, controls != null ? "РњРѕРґСѓР»СЊ Controls РЅР°Р№РґРµРЅ." : "РњРѕРґСѓР»СЊ Controls РЅРµ РЅР°Р№РґРµРЅ.");
        if (controls == null)
        {
            return;
        }

        report.Check(controls.SessionCruiseSpeedMetersPerSecond > 0f, "Session camera cruise speed is positive: " + controls.SessionCruiseSpeedMetersPerSecond.ToString("0.#") + " m/s.");
        report.Check(controls.SessionVerticalSpeedMetersPerSecond > 0f, "Session camera vertical speed is positive: " + controls.SessionVerticalSpeedMetersPerSecond.ToString("0.#") + " m/s.");
        report.Check(controls.SessionSprintMultiplier >= 1f, "Session camera sprint multiplier is at least 1: x" + controls.SessionSprintMultiplier.ToString("0.#") + ".");
        report.Check(controls.SessionCameraFollowSharpness > 0f && controls.SessionCameraFollowSharpness <= 1f, "Session camera follow sharpness is in 0..1: " + controls.SessionCameraFollowSharpness.ToString("0.###") + ".");

        ValidateWarshipsCameraContract(report);

        WildWindGameplayMenu gameplayMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        report.Check(gameplayMenu != null,
            gameplayMenu != null ? "Gameplay menu is present in the session scene." : "Gameplay menu is missing.");
        WildWindGameplayHud hud = FindFirstObjectByType<WildWindGameplayHud>();
        report.Check(hud != null,
            hud != null ? "Gameplay HUD is present in the session scene." : "Gameplay HUD is missing.");
    }

    private static void ValidateWarshipsCameraContract(BigTestReport report)
    {
        WildWindSessionCameraController sessionCameraController = FindFirstObjectByType<WildWindSessionCameraController>();
        report.Check(sessionCameraController != null,
            sessionCameraController != null ? "Flight camera controller is present for free sortie orbit." : "Flight camera controller is missing.");
        if (sessionCameraController != null)
        {
            bool rigidFlightCamera = ReadPrivateBool(sessionCameraController, "rigidFlightCamera", true);
            float minPitch = ReadPrivateFloat(sessionCameraController, "minCameraPitchDegrees", 999f);
            float maxPitch = ReadPrivateFloat(sessionCameraController, "maxCameraPitchDegrees", -999f);
            bool cursorStateEvaluated = sessionCameraController.EvaluateGameplayCursorStateForTests(false, out CursorLockMode lockState, out bool cursorVisible);
            report.Check(cursorStateEvaluated && lockState == CursorLockMode.None && cursorVisible,
                "Flight camera keeps the gameplay cursor free and visible instead of locking mouse look: "
                + "lock=" + lockState
                + ", visible=" + cursorVisible + ".");
            report.Check(minPitch <= -70f && maxPitch >= 70f,
                "Flight camera allows free vertical orbit below and above the ship: "
                + minPitch.ToString("0.#") + ".." + maxPitch.ToString("0.#") + " deg.");
            report.Check(!rigidFlightCamera,
                "Flight camera uses smoothed follow in flight instead of rigid raw physics poses.");
        }

#if UNITY_EDITOR
        string cameraSource = ReadProjectText("Assets/Scripts/Session/WildWindSessionCameraController.cs");
        bool cameraUsesUnscaledTime =
            cameraSource.Contains("Time.unscaledDeltaTime > 0f") &&
            !cameraSource.Contains("Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime)");
        report.Check(cameraUsesUnscaledTime,
            "Flight camera smoothing uses unscaled delta time and is not amplified by simulation time scale.");
        bool cameraUsesRmbOrbit = ContainsAllIgnoreCase(
            cameraSource,
            "rightButton.isPressed",
            "ReadCameraOrbitDragDelta",
            "cameraOrbitPitchDegrees + dragDelta.y");
        bool cursorIsNeverLockedForFlight = ContainsAllIgnoreCase(
            cameraSource,
            "CursorLockMode.None",
            "cursorVisible = true",
            "GameplayCursorLockedForMouseLook = false") &&
            !cameraSource.Contains("ShouldUseLockedFlightMouseLook") &&
            !cameraSource.Contains("Mouse lockedMouse");
        bool flightUsesFreeOrbit = ContainsAllIgnoreCase(
            cameraSource,
            "Vector3 targetPosition = GetOrbitCameraPosition",
            "return orbitPivot",
            "ApplyProgressiveZoom(scrollDelta * CameraWheelEffectivenessMultiplier)",
            "GetCameraOrbitFrame");
        bool oldAimZoomIsBypassed =
            !cameraSource.Contains("TryGetManualGunCameraAimTarget") &&
            !cameraSource.Contains("ApplyFlightCameraWheelRoute(scrollDelta * CameraWheelEffectivenessMultiplier)") &&
            !cameraSource.Contains("GetFlightMouseLookSensitivityScale()\r\n    {\r\n        float scopeRatio");
        report.Check(cameraUsesRmbOrbit && cursorIsNeverLockedForFlight && flightUsesFreeOrbit && oldAimZoomIsBypassed,
            cameraUsesRmbOrbit && cursorIsNeverLockedForFlight && flightUsesFreeOrbit && oldAimZoomIsBypassed
                ? "Flight camera is a free RMB orbit: cursor stays visible, wheel changes distance only, and old manual-gunnery aim/zoom is bypassed."
                : "Free flight camera source contract is incomplete: rmbOrbit=" + cameraUsesRmbOrbit
                    + ", cursorFree=" + cursorIsNeverLockedForFlight
                    + ", freeOrbit=" + flightUsesFreeOrbit
                    + ", oldAimZoomBypassed=" + oldAimZoomIsBypassed + ".");
#else
        report.Check(true, "Free flight camera source contract is editor-only and skipped in player builds.");
#endif
    }

    private void ValidateStrategicShipRuntime(BigTestReport report)
    {
        report.Section("Strategic ship runtime");
        ValidateActivePlayerShipVisual(report);
        ValidateBallisticFireControl(report);
        ValidateCoreTacticalDamageResistanceModel(report);

        GameObject testShip = null;
        try
        {
            testShip = new GameObject("Big Test Strategic Ship Runtime");
            Rigidbody body = testShip.AddComponent<Rigidbody>();
            ShipPhysics ship = testShip.AddComponent<ShipPhysics>();
            ship.baseMass = 1200f;
            ship.cargoMassKg = 300f;
            ship.hullForwardThrustKgf = 900f;
            ship.hullCruiseReferenceSpeedMS = 42f;
            ship.baseMaxSpeedMS = 42f;
            ship.strategicVerticalSpeedMS = 8f;
            ship.strategicYawRateDegPerSecond = 24f;
            ship.RefreshRuntimeShipSettings();

            CoreTacticalShipMotor motor = testShip.GetComponent<CoreTacticalShipMotor>();
            report.Check(motor != null,
                "ShipPhysics installs CoreTacticalShipMotor as the live movement motor.");
            report.Check(body != null
                && !body.useGravity
                && Approximately(body.mass, ship.GetTotalMassKg(), 0.001f),
                "Strategic ships use a non-gravity Rigidbody with mass synchronized from cargo.");

            ship.SetStrategicInputState(1f, 0.35f, 0.25f, 0.5f, false, false, 0f, false, 0f, false, 0f);
            bool ticked = TryInvokePrivateMethod(ship, "FixedUpdate", report);
            Vector3 commandDelta = motor != null ? motor.TargetPosition - ship.transform.position : Vector3.zero;
            report.Check(ticked
                && motor != null
                && commandDelta.sqrMagnitude > 1f,
                "Strategic thrust/lateral/lift/turn input is converted to CoreTacticalShipMotor commands.");

            report.Check(motor != null
                && Approximately(motor.massKg, ship.GetTotalMassKg(), 0.001f)
                && motor.maxForwardSpeedMS >= ship.CleanBaseMaxSpeedMS - 0.001f,
                "Strategic motor receives runtime mass and speed from the ship assembly stats.");
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
            "Range-scaled resistance ignore falls with range and retained velocity: near "
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
                && starterMainGroup.fireMode == ShipGunFireMode.Automatic
                && starterMainGroup.automaticTargetsDamageableShips
                && Approximately(starterMainGroup.SecondsBetweenSalvos, 2f, 0.001f),
                "Starter main caliber is automatic and reloads in 2 seconds: "
                + (starterMainGroup != null ? starterMainGroup.SecondsBetweenSalvos.ToString("0.###") : "missing")
                + " s.");
            report.Check(starterMainGroup != null
                && starterMainGroup.minElevationDegrees <= -70f
                && starterMainGroup.maxElevationDegrees >= 70f,
                "Starter main caliber uses airship elevation arcs instead of a sea-surface depression floor: "
                + (starterMainGroup != null ? starterMainGroup.minElevationDegrees.ToString("0.#") : "missing")
                + ".."
                + (starterMainGroup != null ? starterMainGroup.maxElevationDegrees.ToString("0.#") : "missing")
                + " deg.");
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
                && Approximately(ship.shipGunGroups[ship.manualGunGroupIndex].SecondsBetweenSalvos, 2f, 0.001f)
                && ship.shipGunGroups[ship.manualGunGroupIndex].fireMode == ShipGunFireMode.Automatic
                && ship.shipGunGroups[ship.manualGunGroupIndex].automaticTargetsDamageableShips
                && ship.shipGunGroups[ship.manualGunGroupIndex].minElevationDegrees <= -70f
                && ship.shipGunGroups[ship.manualGunGroupIndex].maxElevationDegrees >= 70f;
            report.Check(serializedReloadRepaired,
                "Serialized old main gun groups are forced to automatic 2 second reload and airship elevation arcs at runtime.");
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
            bool automaticMainRecovered = ship.BuildGunAimSolutionForPoint(null, new Vector3(120f, 0f, 40f), Vector3.zero, false, true, out BallisticAimSolution recoveredManualAim)
                && ship.shipGunGroups != null
                && ship.shipGunGroups.Count >= 2
                && ship.shipGunGroups[ship.manualGunGroupIndex] != null
                && ship.shipGunGroups[ship.manualGunGroupIndex].enabled
                && ship.shipGunGroups[ship.manualGunGroupIndex].groupId == "main"
                && ship.shipGunGroups[ship.manualGunGroupIndex].fireMode == ShipGunFireMode.Automatic
                && ship.shipGunGroups[ship.manualGunGroupIndex].automaticTargetsDamageableShips
                && recoveredManualAim.maxRangeMeters > 0.001f;
            report.Check(automaticMainRecovered,
                "ShipPhysics repairs old runtime ships that have no automatic main gun group before aiming or firing.");

            ship.weaponShotCostKg = 0f;
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
                && ContainsAllIgnoreCase(slowReason, "yaw", "elev"),
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
                && ContainsAllIgnoreCase(elevationReason, "-5", "10"),
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
                : "Active player ShipPhysics is missing, so the session would run with an empty focus.");
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

        WildWindBaseIslandView island = WildWindBaseIslandView.EnsureForCurrentSessionScene();
        bool dockedCityHidesShip = island != null
            && island.IsCityVisibleForTests
            && island.SuppressedActiveShipRendererCountForTests > 0;

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
        report.Check(visibleRenderers.Count > 0 || dockedCityHidesShip,
            dockedCityHidesShip
                ? "Docked city view suppresses the active ship renderers during meta start: "
                    + island.SuppressedActiveShipRendererCountForTests + "."
                : "Active ship has " + visibleRenderers.Count + " active mesh renderer(s).");

        bool hasBounds = TryFindVisibleShipMeshSize(meshFilters, !dockedCityHidesShip, out Vector3 size);
        report.Check(hasBounds && IsFinite(size) && size.sqrMagnitude > 0.01f,
            hasBounds
                ? "Active ship mesh has finite visible bounds: " + FormatVector(size) + "."
                : "Active ship mesh size is empty or unreadable.");
    }

    private void ValidateKnowledgeScreenRuntime(BigTestReport report)
    {
        report.Section("Knowledge screen runtime");

        string oldPortTypePrefix = "WildWind" + "Meta" + "Port";
        string oldPortRuntimePath = "Assets/Scripts/Meta/WildWind" + "Meta" + "Port" + "RuntimeModel.cs";
        string oldPortModelPath = "Assets/Scripts/Meta/WildWind" + "Meta" + "Port" + "Model.cs";
        string oldPortScreenPath = "Assets/Scripts/UI/WildWind" + "Meta" + "Port" + "Screen.cs";
        string metaGameStateText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string gameplayHudText = ReadProjectText("Assets/Scripts/UI/WildWindGameplayHud.cs");
        string knowledgeScreenText = ReadProjectText("Assets/Scripts/UI/WildWindKnowledgeScreen.cs");
        string controlSettingsText = ReadProjectText("Assets/Scripts/Session/WildWindControlSettings.cs");
        string flightControlText = ReadProjectText("Assets/Scripts/Session/WildWindFlightControlBridge.cs");
        string sessionCameraText = ReadProjectText("Assets/Scripts/Session/WildWindSessionCameraController.cs");
        string sessionAtmosphereText = ReadProjectText("Assets/Scripts/Systems/SessionAtmosphereTuner.cs");
        string miningResourceText = ReadProjectText("Assets/Scripts/Systems/MiningResourceSimulator.cs");
        string dockingPortText = ReadProjectText("Assets/Scripts/Meta/DockingPort.cs");
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string shipAssemblyText = ReadProjectText("Assets/Scripts/Data/ShipAssemblyBuilder.cs");
        string sessionConfigShipPartsText = ReadProjectText("Assets/Scripts/Data/SessionConfigShipParts.cs");
        string sessionConfigDatabaseText = ReadProjectText("Assets/Scripts/Data/SessionConfigDatabase.cs");
        string projectFileText = ReadProjectText("Assembly-CSharp.csproj");
        string cargoModelText = ReadProjectText("Assets/Scripts/Data/CargoModel.cs");
        string cargoStoragePlannerText = ReadProjectText("Assets/Scripts/Meta/CargoStoragePlanner.cs");
        string shipPartDefinitionText = ReadProjectText("Assets/Scripts/Data/ShipPartDefinitionSO.cs");
        string hullCsvText = ReadProjectText("Assets/Data/Config/Hull.csv");
        string specialModuleCsvText = ReadProjectText("Assets/Data/Config/Special_module.csv");
        string itemCsvText = ReadProjectText("Assets/Data/Config/Item.csv");
        string localizationCsvText = ReadProjectText("Assets/Data/Localization/Ui.csv");
        string sessionSceneText = ReadProjectText("Assets/Scenes/WildWindSessionScene.unity");
        string oldPortWindowTitle = "WILD WIND " + "PORT";
        string oldPortButtonTitle = "META " + "PORT";
        bool oldPortSourcesRemoved =
            !File.Exists(ProjectPath(oldPortRuntimePath)) &&
            !File.Exists(ProjectPath(oldPortRuntimePath + ".meta")) &&
            !File.Exists(ProjectPath(oldPortModelPath)) &&
            !File.Exists(ProjectPath(oldPortModelPath + ".meta")) &&
            !File.Exists(ProjectPath(oldPortScreenPath)) &&
            !File.Exists(ProjectPath(oldPortScreenPath + ".meta")) &&
            !gameplayHudText.Contains(oldPortTypePrefix) &&
            !knowledgeScreenText.Contains(oldPortTypePrefix) &&
            !projectFileText.Contains(oldPortTypePrefix) &&
            !gameplayHudText.Contains(oldPortWindowTitle) &&
            !gameplayHudText.Contains(oldPortButtonTitle) &&
            !gameplayHudText.Contains("Window" + "Meta" + "PortId") &&
            !gameplayHudText.Contains("Toggle" + "Meta" + "PortScreen") &&
            !gameplayHudText.Contains("Open" + "Meta" + "PortScreen");
        report.Check(oldPortSourcesRemoved,
            "Old full-screen dock console sources, state model and runtime demo model are removed.");

        bool knowledgeScreenOwnsResearch =
            knowledgeScreenText.Contains("public sealed class WildWindKnowledgeScreen") &&
            knowledgeScreenText.Contains("АРХИВЫ") &&
            knowledgeScreenText.Contains("GetTechnologyBoardOverviewText") &&
            knowledgeScreenText.Contains("TrySelectResearchTechnology") &&
            knowledgeScreenText.Contains("GetNextTechnologyCategoryId");
        report.Check(knowledgeScreenOwnsResearch,
            "Archives use a dedicated knowledge screen backed directly by the live technology board.");

        bool extractionLabRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/WildWindSessionMechanicsModel.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/WildWindSessionMechanicsModel.cs.meta"));
        report.Check(extractionLabRemoved,
            "Old pure extraction-lab mechanics model is removed; Big Test now covers the runtime systems directly.");

        bool oldMetaEconomyRemoved =
            !gameplayHudText.Contains("MECHANICS LAB") &&
            !gameplayHudText.Contains("RunMechanicsLab") &&
            !sessionAtmosphereText.Contains("distantPorts") &&
            !sessionAtmosphereText.Contains("Distant Port Silhouettes") &&
            !sessionSceneText.Contains("distantPortsRoot") &&
            !sessionSceneText.Contains("showDistantPorts") &&
            !metaGameStateText.Contains("showDockingDebugUI") &&
            !metaGameStateText.Contains("DrawDockedDebugUi") &&
            !metaGameStateText.Contains("TrySelectSessionCoreTestHull") &&
            !metaGameStateText.Contains("Test hull") &&
            !sessionSceneText.Contains("showDockingDebugUI") &&
            !sessionCameraText.Contains("Mouse Wheel Tick Debug") &&
            !sessionCameraText.Contains("showWheelTickCounter") &&
            !sessionSceneText.Contains("showWheelTickCounter") &&
            !sessionAtmosphereText.Contains("useCompositionRig") &&
            !sessionAtmosphereText.Contains("ConfigureCompositionRig") &&
            !sessionAtmosphereText.Contains("Altitude Test Composition") &&
            !sessionAtmosphereText.Contains("previewAltitudeMeters") &&
            !sessionAtmosphereText.Contains("GetAtmosphereDebugText") &&
            !sessionCameraText.Contains("Test Altitude") &&
            !sessionCameraText.Contains("Move To Capital") &&
            !sessionCameraText.Contains("MoveToCapital") &&
            !sessionSceneText.Contains("useCompositionRig") &&
            !sessionSceneText.Contains("compositionRoot") &&
            !sessionSceneText.Contains("Preview") &&
            !gameplayHudText.Contains("CheatAfterburner") &&
            !gameplayHudText.Contains("Cheat Afterburner") &&
            !gameplayHudText.Contains("IsFlightControlsVisible") &&
            !gameplayHudText.Contains("IsAutopilotPanelVisible") &&
            !sessionAtmosphereText.Contains("[ExecuteAlways]") &&
            !sessionAtmosphereText.Contains("[ContextMenu") &&
            !sessionAtmosphereText.Contains("CaptureFromScene") &&
            !sessionAtmosphereText.Contains("CopySettingsForCodex") &&
            !sessionAtmosphereText.Contains("BuildSettingsText") &&
            !sessionAtmosphereText.Contains("UNITY_EDITOR") &&
            !controlSettingsText.Contains("Flight Assist Rates") &&
            !controlSettingsText.Contains("FlightTargetSpeedChangeMetersPerSecond") &&
            !controlSettingsText.Contains("FlightTargetAltitudeChangeMetersPerSecond") &&
            !controlSettingsText.Contains("FlightTargetHeadingChangeDegreesPerSecond") &&
            !flightControlText.Contains("WildWindAxisControlMode") &&
            !flightControlText.Contains("AutoDock") &&
            !flightControlText.Contains("Autopilot") &&
            !flightControlText.Contains("SetTargetFromActiveTask") &&
            !flightControlText.Contains("Target Move Step") &&
            !flightControlText.Contains("AP off") &&
            !dockingPortText.Contains("autoDockWhenInRange") &&
            !dockingPortText.Contains("requireLeaveBeforeRedocking") &&
            !miningResourceText.Contains("IsRockSafeForAutopilot") &&
            !shipPhysicsText.Contains("RouteEtaInfo") &&
            !shipPhysicsText.Contains("routeWaypoints") &&
            !shipPhysicsText.Contains("routeWaypointIndex") &&
            !shipPhysicsText.Contains("routeWaypointRadiusMeters") &&
            !shipPhysicsText.Contains("routeEnabled") &&
            !shipPhysicsText.Contains("GenerateRandomRoute") &&
            !shipPhysicsText.Contains("UpdateRouteMachine") &&
            !shipPhysicsText.Contains("StopLeviathanHuntForDocking") &&
            !shipPhysicsText.Contains("UpdateLeviathanHuntAutopilot") &&
            !shipPhysicsText.Contains("StopRouteForFullMiningHold") &&
            !shipPhysicsText.Contains("GetCurrentRouteEta") &&
            !shipPhysicsText.Contains("leviathanHuntAutopilot") &&
            !shipPhysicsText.Contains("FindNearestSurveyedLeviathan") &&
            !shipAssemblyText.Contains("WaypointRadius") &&
            !sessionConfigShipPartsText.Contains("waypoint_radius_m") &&
            !sessionConfigShipPartsText.Contains("waypointRadiusM") &&
            !shipPartDefinitionText.Contains("WaypointRadius") &&
            !hullCsvText.Contains("waypoint_radius_m") &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/TechTreeDefinitionSO.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/TechTreeDefinitionSO.cs.meta")) &&
            !File.Exists(ProjectPath("Assets/Data/TechTrees/WildWindTechTree.asset")) &&
            !File.Exists(ProjectPath("Assets/Data/TechTrees/WildWindTechTree.asset.meta")) &&
            !projectFileText.Contains("TechTreeDefinitionSO") &&
            !shipAssemblyText.Contains("TechTreeDefinitionSO") &&
            !shipAssemblyText.Contains("techTree") &&
            !metaGameStateText.Contains("techTree") &&
            !sessionSceneText.Contains("techTree:") &&
            !sessionConfigDatabaseText.Contains("NeedWorkforceRecoveryPerHour") &&
            !sessionConfigDatabaseText.Contains("passengerSeatCapacity") &&
            !sessionConfigDatabaseText.Contains("shipDockSlots") &&
            !sessionConfigDatabaseText.Contains("IsPassengerCargoItemId") &&
            !cargoModelText.Contains("Passenger") &&
            !cargoModelText.Contains("ShipDock") &&
            !cargoModelText.Contains("ShipSizeClass") &&
            !cargoStoragePlannerText.Contains("CargoStorageKind.Cabin") &&
            !cargoStoragePlannerText.Contains("CargoStorageKind.ShipDock") &&
            !shipAssemblyText.Contains("NeedWorkforceRecoveryPerHour") &&
            !shipAssemblyText.Contains("PassengerSeatCapacity") &&
            !shipAssemblyText.Contains("ShipDockSlots") &&
            !shipPartDefinitionText.Contains("NeedWorkforceRecoveryPerHour") &&
            !shipPartDefinitionText.Contains("PassengerSeatCapacity") &&
            !shipPartDefinitionText.Contains("ShipDockSlots") &&
            !specialModuleCsvText.Contains("need_workforce_recovery_per_hour") &&
            !specialModuleCsvText.Contains("passenger_seat_capacity") &&
            !specialModuleCsvText.Contains("ship_dock_slots") &&
            !itemCsvText.Contains("ship_size_class") &&
            !itemCsvText.Contains("docked_transport_mass_factor") &&
            !sessionSceneText.Contains("leviathanHunt") &&
            !sessionSceneText.Contains("waypoints: []") &&
            !sessionSceneText.Contains("positionHold") &&
            !sessionSceneText.Contains("engineCheatAfterburnerEnabled");
        report.Check(oldMetaEconomyRemoved,
            "Old economy scaffolding, open-world silhouettes, preview rigs, runtime debug overlays and test-only flight cheats are removed.");

        WildWindGameplayHud hud = gameplayHud != null ? gameplayHud : FindFirstObjectByType<WildWindGameplayHud>();
        MetaGameState runtimeMetaForPort = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        SessionConfigDatabase runtimeConfigForHud = runtimeMetaForPort != null ? runtimeMetaForPort.SessionConfig : null;
        report.Check(hud != null && hud.IsReady,
            "HUD builds the docked meta frame without the retired full-screen dock console.");
        string oldMissionPanelName = "Docked " + "Panel";
        string oldMissionCycleApi = "TryCycle" + "SessionSortie";
        string oldMissionTakeoffApi = "Try" + "TakeOff";
        string oldMissionDebugFlag = "Show Legacy " + "Docked Debug Panel";
        string oldMissionNextButton = "Next " + "Sortie";
        bool oldMissionLauncherRemoved =
            !gameplayHudText.Contains(oldMissionPanelName) &&
            !gameplayHudText.Contains(oldMissionCycleApi) &&
            !gameplayHudText.Contains(oldMissionTakeoffApi) &&
            !gameplayHudText.Contains(oldMissionDebugFlag) &&
            !gameplayHudText.Contains(oldMissionNextButton) &&
            !localizationCsvText.Contains("game.hud." + "takeoff") &&
            !localizationCsvText.Contains("game.hud." + "process_base") &&
            !localizationCsvText.Contains("game.hud." + "run_base_work") &&
            !localizationCsvText.Contains("game.hud." + "base_inputs") &&
            !localizationCsvText.Contains("game.hud." + "base_materials") &&
            !localizationCsvText.Contains("game.hud." + "ship_supplies");
        report.Check(oldMissionLauncherRemoved,
            "Retired six-button mission launcher panel, sortie cycling button and launch-localization keys are removed from the HUD.");
        report.Check(hud != null && hud.IsMetaPlayerProfileReadyForTests,
            "HUD exposes the first rough top-left meta player profile block.");
        report.Check(hud != null
                && hud.IsMetaPlayerProfileVisibleForTests
                && hud.MetaPlayerProfileTitleForTests == "Капитан Ветров"
                && hud.MetaPlayerProfileMasteryFillForTests > 0.5f,
            "Meta player profile shows a placeholder portrait/title and mastery progress bar without a duplicate top level badge.");
        report.Check(hud != null && hud.IsMetaResourceCounterStripReadyForTests,
            "HUD exposes the five top-center meta resource counter blocks.");
        report.Check(hud != null
                && hud.IsMetaResourceCounterStripVisibleForTests
                && hud.MetaResourceCounterCountForTests == 5
                && hud.GetMetaResourceCounterAmountForTests(0) == "1.24M"
                && hud.GetMetaResourceCounterAmountForTests(1) == "853K"
                && hud.GetMetaResourceCounterAmountForTests(2) == "412K"
                && hud.GetMetaResourceCounterAmountForTests(3) == "18.7K"
                && hud.GetMetaResourceCounterAmountForTests(4) == "2450",
            "Meta resource counters render compact placeholder amounts with icon and plus affordances.");
        report.Check(WildWindGameplayHud.FormatMetaResourceAmountForTests(1240000) == "1.24M"
                && WildWindGameplayHud.FormatMetaResourceAmountForTests(853000) == "853K"
                && WildWindGameplayHud.FormatMetaResourceAmountForTests(18700) == "18.7K"
                && WildWindGameplayHud.FormatMetaResourceAmountForTests(2450) == "2450",
            "Meta resource counter formatter keeps short readable labels for thousands and millions.");
        report.Check(hud != null && hud.IsMetaTopRightButtonsReadyForTests,
            "HUD exposes four top-right meta buttons; settings contains the progress reset action while the other windows stay placeholder-thin.");
        report.Check(hud != null && hud.IsSettingsResetProgressButtonReadyForTests,
            "Settings window exposes the persistent progress reset button.");
        report.Check(hud != null
                && hud.IsMetaTopRightButtonsVisibleForTests
                && hud.MetaTopRightButtonCountForTests == 4,
            "Top-right meta buttons are visible in the docked meta HUD.");
        bool allMetaTopRightWindowsOpen = hud != null;
        for (int i = 0; hud != null && i < hud.MetaTopRightButtonCountForTests; i++)
        {
            allMetaTopRightWindowsOpen &= hud.PressMetaTopRightButtonForTests(i) && hud.IsMetaTopRightWindowOpenForTests(i);
        }

        report.Check(allMetaTopRightWindowsOpen,
            "Each top-right meta button opens its modal window through the real Button.onClick binding.");
        report.Check(hud != null && hud.IsMetaLeftSideButtonsReadyForTests,
            "HUD exposes the five right-side meta buttons for events, merchants, inventory, knowledge and shop.");
        report.Check(hud != null && hud.AreMainHudReferenceIconsReadyForTests,
            "Main meta HUD loads the extracted SVG icon assets for the right rail plus Projects and Dock buttons instead of falling back to text glyphs.");
        bool allMetaLeftSideWindowsOpen = hud != null;
        for (int i = 0; hud != null && i < hud.MetaLeftSideButtonCountForTests; i++)
        {
            allMetaLeftSideWindowsOpen &= hud.PressMetaLeftSideButtonForTests(i) && hud.IsMetaLeftSideWindowOpenForTests(i);
        }

        report.Check(allMetaLeftSideWindowsOpen,
            "Each right-side meta button opens its matching window through the real Button.onClick binding.");
        bool metaWindowsClosedAfterClickContract = hud != null && hud.CloseAllHudWindowsForTests();
        report.Check(metaWindowsClosedAfterClickContract,
            "Meta button click contract closes cleanly before the Dock screen flow continues.");
        report.Check(hud != null
                && hud.IsMetaLeftSideButtonsVisibleForTests
                && hud.MetaLeftSideButtonCountForTests == 5
                && hud.IsMetaDockButtonReadyForTests
                && hud.IsMetaDockButtonVisibleForTests
                && hud.MetaDockButtonLabelForTests == "В док"
                && hud.IsMetaSideRailOnRightForTests,
            "Right-side meta rail is visible in docked mode and includes the large round Dock button.");
        WildWindBaseIslandView islandBeforeDockScreen = WildWindBaseIslandView.EnsureForCurrentSessionScene();
        bool cameraMovedBeforeDockScreen = islandBeforeDockScreen != null
            && islandBeforeDockScreen.OffsetCityCameraForTests(2.5f, -1.75f, 13f, -4f, -1.25f);
        bool dockScreenOpened = hud != null
            && hud.IsMetaDockScreenReadyForTests
            && hud.PressMetaDockButtonForTests()
            && hud.IsMetaDockScreenVisibleForTests
            && hud.IsPortHudHiddenForDockScreenForTests
            && hud.MetaDockScreenPortButtonLabelForTests == "ПОРТ"
            && hud.IsMetaDockScreenPortButtonAtDockButtonSpotForTests;
        WildWindBaseIslandView islandAfterDockScreenOpen = dockScreenOpened
            ? WildWindBaseIslandView.EnsureForCurrentSessionScene()
            : null;
        bool dockScreenStartsSmoothEllipseBlend = islandAfterDockScreenOpen != null
            && islandAfterDockScreenOpen.IsCityCameraHomeBlendActiveForTests
            && islandAfterDockScreenOpen.IsCityCameraTransformBlendActiveForTests
            && islandAfterDockScreenOpen.IsCityCameraTransformBlendTargetingPortDockForTests
            && !islandAfterDockScreenOpen.IsPortDockCameraOrbitActiveForTests;
        bool dockScreenFocusedPhysicalPort = islandAfterDockScreenOpen != null
            && islandAfterDockScreenOpen.FocusSelectedDevelopmentDockShipForTests()
            && islandAfterDockScreenOpen.IsCityVisibleForTests
            && !string.IsNullOrWhiteSpace(islandAfterDockScreenOpen.FocusedPortDockKeyForTests)
            && islandAfterDockScreenOpen.PortDockBattleshipBerthFitsForTests;
        bool dockScreenUsesEllipticPortCamera = dockScreenFocusedPhysicalPort
            && islandAfterDockScreenOpen.IsPortDockCameraOrbitActiveForTests
            && islandAfterDockScreenOpen.IsPortDockCameraOrbitLookingAtShipForTests
            && islandAfterDockScreenOpen.OffsetPortDockCameraOrbitForTests(32f)
            && islandAfterDockScreenOpen.IsPortDockCameraOrbitLookingAtShipForTests;
        report.Check(dockScreenOpened
                && islandAfterDockScreenOpen != null
                && dockScreenStartsSmoothEllipseBlend
                && dockScreenFocusedPhysicalPort
                && dockScreenUsesEllipticPortCamera,
            "Main HUD Dock button opens the Dock screen through its real Button.onClick binding as a UI layer over the live city, hides the Port HUD, starts a smooth transform blend to the physical berth ellipse, keeps a battleship-class berth available, and puts the camera on an elliptic orbit around the docked ship.");
        bool returnedFromDockScreen = hud != null
            && hud.ReturnFromMetaDockScreenToPortForTests()
            && !hud.IsMetaDockScreenVisibleForTests
            && hud.IsMetaDockButtonVisibleForTests
            && hud.MetaDockButtonLabelForTests == "В док";
        WildWindBaseIslandView islandAfterDockScreenReturn = returnedFromDockScreen
            ? WildWindBaseIslandView.EnsureForCurrentSessionScene()
            : null;
        report.Check(returnedFromDockScreen
                && islandAfterDockScreenReturn != null
                && islandAfterDockScreenReturn.IsCityVisibleForTests,
            "Port button on the Dock screen returns to the port HUD without using a modal window.");
        report.Check(cameraMovedBeforeDockScreen
                && returnedFromDockScreen
                && islandAfterDockScreenReturn != null
                && islandAfterDockScreenReturn.IsCityCameraHomeBlendActiveForTests
                && islandAfterDockScreenReturn.IsCityCameraTransformBlendActiveForTests
                && islandAfterDockScreenReturn.IsCityCameraTransformBlendTargetingPreservedStateForTests
                && !islandAfterDockScreenReturn.IsPortDockCameraOrbitActiveForTests
                && !islandAfterDockScreenReturn.IsPortDockWorldInteractionSuppressedForTests,
            "Returning from the Dock screen leaves the port orbit and starts a smooth return to the exact city camera pose saved before Dock opened.");
        if (islandAfterDockScreenReturn != null)
        {
            islandAfterDockScreenReturn.SnapCityCameraHomeForTests();
        }

        bool allMetaLeftSideWindowsOpenAfterDockReturn = hud != null;
        for (int i = 0; hud != null && i < hud.MetaLeftSideButtonCountForTests; i++)
        {
            allMetaLeftSideWindowsOpenAfterDockReturn &= hud.OpenMetaLeftSideWindowForTests(i) && hud.IsMetaLeftSideWindowOpenForTests(i);
        }

        report.Check(allMetaLeftSideWindowsOpenAfterDockReturn,
            "Each right-side meta window can still open after returning from the Dock screen without mutating gameplay state.");
        string developmentWindowText = hud != null ? hud.DevelopmentWindowTextForTests : "";
        bool developmentWindowRendersFlatCatalog = hud != null
            && hud.IsDevelopmentWindowCatalogReadyForTests
            && hud.DevelopmentWindowSupplierCountForTests == 1
            && hud.DevelopmentWindowTierColumnCountForTests == 0
            && hud.DevelopmentWindowTileCountForTests == 3
            && hud.DevelopmentWindowConnectionCountForTests == 0
            && developmentWindowText.Contains("3");
        report.Check(developmentWindowRendersFlatCatalog,
            "Development window renders the flat ship catalog with three current ship cards and no tree links. Actual: suppliers="
            + (hud != null ? hud.DevelopmentWindowSupplierCountForTests : -1)
            + ", tiers="
            + (hud != null ? hud.DevelopmentWindowTierColumnCountForTests : -1)
            + ", tiles="
            + (hud != null ? hud.DevelopmentWindowTileCountForTests : -1)
            + ", connections="
            + (hud != null ? hud.DevelopmentWindowConnectionCountForTests : -1)
            + ", textHas3="
            + (!string.IsNullOrWhiteSpace(developmentWindowText) && developmentWindowText.Contains("3")));
        report.Check(hud != null
                && developmentWindowText.IndexOf("Коршун", StringComparison.OrdinalIgnoreCase) >= 0,
            "Development window renders the selected flat-catalog ship details below research and purchase costs.");
        report.Check(hud != null && hud.AreOpenHudWindowsModalForTests,
            "Open HUD windows are centered modal overlays with a fade backdrop, no minimize button and close-only chrome.");
        report.Check(hud != null
                && hud.IsMetaQuestPanelReadyForTests
                && hud.IsMetaQuestPanelVisibleForTests
                && hud.IsMetaQuestPanelPinnedLeftForTests
                && hud.MetaQuestVisibleCountForTests >= 1
                && hud.MetaQuestVisibleCountForTests <= 3
                && hud.MetaQuestMaxCountForTests == 3,
            "HUD exposes a configurable left-wall quest panel with one to three visible quest rows.");
        report.Check(hud != null
                && hud.GetMetaQuestCounterForTests(0) == "4/6"
                && hud.GetMetaQuestCounterForTests(1) == "0/1"
                && hud.GetMetaQuestCounterForTests(2) == "2/3"
                && hud.MetaAllTasksButtonLabelForTests == "Все задачи",
            "Meta quest rows show icon, two-line copy, progress counters and the All Tasks button.");
        report.Check(hud != null
                && hud.OpenMetaAllTasksWindowForTests()
                && hud.IsMetaAllTasksWindowOpenForTests
                && hud.IsMetaAllTasksWindowModalForTests,
            "Meta quest panel All Tasks button opens an empty centered modal window.");
        report.Check(hud != null
                && hud.CloseMetaAllTasksWindowByBackdropForTests()
                && !hud.IsMetaAllTasksWindowOpenForTests,
            "Clicking the modal backdrop closes the open HUD window.");
        report.Check(hud != null
                && hud.IsMetaProjectPanelReadyForTests
                && hud.IsMetaProjectPanelVisibleForTests
                && hud.IsMetaProjectPanelBottomLeftForTests
                && hud.MetaProjectVisibleCountForTests >= 1
                && hud.MetaProjectVisibleCountForTests <= 5
                && hud.MetaProjectMaxCountForTests == 5
                && hud.MetaProjectHomeButtonLabelForTests == "\u2708"
                && hud.MetaProjectGridButtonLabelForTests == "\u2693"
                && hud.MetaProjectCameraButtonLabelForTests == "\u25A3",
            "HUD exposes a lower-left project queue, with separate home/grid/camera controls above the queue and up to five project cards.");
        report.Check(hud != null
                && hud.GetMetaProjectTimerForTests(0) == "Готово"
                && hud.GetMetaProjectTimerForTests(1) == "2ч 10м"
                && hud.GetMetaProjectTimerForTests(2) == "46м",
            "Meta project cards show title, amount, icon, timer and bottom-up completion fill.");
        report.Check(hud != null && runtimeConfigForHud != null && hud.IsResourceCatalogReadyForTests && hud.ResourceCatalogWindowItemCountForTests >= runtimeConfigForHud.items.Count,
            "HUD exposes an openable resource catalog window populated from Item.csv with real icon sprites.");
        report.Check(hud != null && hud.OpenResourceCatalogWindowForTests() && hud.IsResourceCatalogWindowOpenForTests,
            "HUD resource catalog window can be opened without entering an unfinished interface flow.");
        bool hudKnowledgeOpened = hud != null
            && hud.OpenKnowledgeScreenForTests()
            && hud.IsKnowledgeScreenVisibleForTests
            && hud.IsKnowledgeScreenReadyForTests;
        string hudKnowledgeContent = hud != null ? hud.KnowledgeScreenContentForTests : "";
        string hudKnowledgeReport = hud != null ? hud.KnowledgeScreenReportForTests : "";
        report.Check(hudKnowledgeOpened
                && ContainsAllIgnoreCase(hudKnowledgeContent, "SP")
                && hudKnowledgeReport.Contains("source=runtime"),
            "HUD opens the Archives knowledge screen from the live account state.");
        report.Check(hud != null
                && hud.KnowledgeScreenHasVisibleTextForTests
                && hud.KnowledgeScreenSortingOrderForTests > 875,
            "HUD Archives knowledge screen has visible text frames above the base building overlay.");
        report.Check(hud != null
                && hud.KnowledgeScreenHasRubricTabsForTests
                && hud.KnowledgeScreenHasTreeViewForTests
                && hud.KnowledgeScreenHasInspectorPanelForTests
                && hud.KnowledgeScreenVisibleTechnologyNodeCountForTests >= 3
                && hud.KnowledgeScreenVisibleTreeConnectionCountForTests >= 1
                && hud.KnowledgeScreenVisibleTreeColumnCountForTests >= 10,
            "HUD Archives knowledge screen renders left rubric tabs, a scrollable 10-column branching knowledge tree, and a right-side knowledge inspector.");
        string hudCategoryBefore = hud != null ? hud.KnowledgeScreenSelectedCategoryForTests : "";
        bool hudCategoryChanged = hud != null
            && hud.RunKnowledgeSecondaryActionForTests()
            && !string.IsNullOrWhiteSpace(hud.KnowledgeScreenSelectedCategoryForTests)
            && (hud.KnowledgeScreenSelectedCategoryForTests != hudCategoryBefore || ContainsAllIgnoreCase(hud.KnowledgeScreenContentForTests, "SP"));
        report.Check(hudCategoryChanged,
            "Archives knowledge screen cycles knowledge rubrics without a retired tab model.");
        report.Check(hud != null
                && hud.RunKnowledgePrimaryActionForTests()
                && hud.KnowledgeScreenReportForTests.Contains("last="),
            "Archives knowledge screen can select or evaluate the next available knowledge target.");
        if (hud != null)
        {
            hud.CloseKnowledgeScreenForTests();
        }

        GameObject knowledgeScreenObject = null;
        WildWindKnowledgeScreen knowledgeScreen = null;
        try
        {
            knowledgeScreenObject = new GameObject("Knowledge Screen Big Test");
            knowledgeScreen = knowledgeScreenObject.AddComponent<WildWindKnowledgeScreen>();
            knowledgeScreen.BindRuntime(runtimeMetaForPort);
            knowledgeScreen.RebuildScreen();
            knowledgeScreen.SetVisible(true);
            string standaloneContent = knowledgeScreen.ContentForTests;
            report.Check(knowledgeScreen.IsReadyForTests
                    && ContainsAllIgnoreCase(standaloneContent, "SP")
                    && knowledgeScreen.Report.Contains("source=runtime")
                    && knowledgeScreen.HasVisibleTextForTests,
                "Standalone Archives knowledge screen can be built directly from runtime progress.");
            report.Check(knowledgeScreen.HasRubricTabsForTests
                    && knowledgeScreen.HasTreeViewForTests
                    && knowledgeScreen.HasInspectorPanelForTests
                    && knowledgeScreen.VisibleTechnologyNodeCountForTests >= 3
                    && knowledgeScreen.VisibleTreeConnectionCountForTests >= 1
                    && knowledgeScreen.VisibleTreeColumnCountForTests >= 10
                    && ContainsAllIgnoreCase(knowledgeScreen.SelectedTechnologyDetailsForTests, "SP"),
                "Standalone Archives knowledge screen exposes rubric tabs, branching tree nodes and selected-knowledge modifier details.");
            string categoryBefore = knowledgeScreen.SelectedCategoryIdForTests;
            bool categoryCycleWorks = knowledgeScreen.RunSecondaryActionForTests()
                && !string.IsNullOrWhiteSpace(knowledgeScreen.SelectedCategoryIdForTests)
                && (knowledgeScreen.SelectedCategoryIdForTests != categoryBefore || ContainsAllIgnoreCase(knowledgeScreen.ContentForTests, "SP"));
            report.Check(categoryCycleWorks,
                "Standalone Archives knowledge screen cycles knowledge rubrics.");
            report.Check(knowledgeScreen.RunPrimaryActionForTests()
                    && knowledgeScreen.Report.Contains("last="),
                "Standalone Archives knowledge screen can select or evaluate a knowledge target.");
        }
        finally
        {
            if (knowledgeScreenObject != null)
            {
                Destroy(knowledgeScreenObject);
            }
        }
    }

    private void ValidateBaseIslandRuntime(BigTestReport report)
    {
        report.Section("Base island runtime");

        WildWindBaseIslandView island = WildWindBaseIslandView.EnsureForCurrentSessionScene();
        report.Check(island != null,
            island != null
                ? "Base island runtime view exists in the session scene."
                : "Base island runtime view is missing from the session scene.");
        if (island == null)
        {
            return;
        }

        report.Check(island.IsReadyForTests,
            "Base island builds its runtime visuals and placeholder window.");
        report.Check(island.UsesIsoCityCameraForTests,
            "Docked city view uses the perspective isometric city camera controller.");
        report.Check(island.PrototypeToolbarHiddenForTests,
            "Docked base island hides the standalone city prototype toolbar so it cannot overlap the main HUD.");
        report.Check(island.CityGridWidthForTests == 40
            && island.CityGridHeightForTests == 40
            && island.CityOpenCellCountForTests >= 400
            && island.CityFogCellCountForTests > 0
            && island.CityDebrisCellCountForTests > 0
            && island.ExpansionRegionCountForTests == 10
            && island.ExpansionMarkerCountForTests > 0
            && island.ExternalDockSlotCountForTests == island.ExpectedExternalDockSlotCountForTests,
            "Base island uses the 40x40 square expansion grid with a front-corner open start, fogged chunks, debris chunks and external 5x8 dock slots only on the currently open outer perimeter.");
        report.Check(island.CityCellDataCountForTests == island.CityGridWidthForTests * island.CityGridHeightForTests
            && island.CityTileColliderCountForTests == 1
            && island.CityTileBatchRendererCountForTests <= 7,
            "Base island grid keeps 40x40 cells as data while rendering/picking them through one ground collider and batched tile renderers: cells "
            + island.CityCellDataCountForTests
            + ", tile colliders "
            + island.CityTileColliderCountForTests
            + ", tile renderers "
            + island.CityTileBatchRendererCountForTests + ".");
        report.Check(island.IsCityVisibleForTests
            && island.EnabledCityRendererCountForTests > 0
            && island.EnabledCityBuildingRendererCountForTests > 0
            && island.EnabledCityTileRendererCountForTests > 0
            && island.CameraVisibleCityRendererCountForTests > 0
            && island.SuppressedCityRendererCountForTests == 0,
            "Docked city is actually renderable, not just instantiated: active="
            + island.IsCityVisibleForTests
            + ", enabledRenderers="
            + island.EnabledCityRendererCountForTests
            + ", enabledBuildings="
            + island.EnabledCityBuildingRendererCountForTests
            + ", enabledTiles="
            + island.EnabledCityTileRendererCountForTests
            + ", cameraVisible="
            + island.CameraVisibleCityRendererCountForTests
            + ", suppressedCity="
            + island.SuppressedCityRendererCountForTests + ".");
        Vector3 islandVisualSize = island.CityIslandWorldSizeForTests;
        report.Check(island.CityIslandFootprintCoversGridForTests
            && island.CityIslandGridOverhangCellsForTests >= 0.65f
            && island.CityIslandGridMaxOverhangCellsForTests <= 2.25f
            && islandVisualSize.y >= 18f
            && islandVisualSize.y >= Mathf.Min(islandVisualSize.x, islandVisualSize.z) * 0.38f,
            "Docked city rests on a tight square-like floating iceberg island instead of an oversized thin plate: size="
            + islandVisualSize.ToString("0.##")
            + ", min overhang "
            + island.CityIslandGridOverhangCellsForTests.ToString("0.##")
            + " cells, max overhang "
            + island.CityIslandGridMaxOverhangCellsForTests.ToString("0.##")
            + " cells.");
        report.Check(island.CityRaycastLayerForTests == 30
            && island.CityInteractiveColliderLayerMismatchCountForTests == 0,
            "Base island hover raycasts are isolated to the city interaction layer instead of scanning every physics collider in the session scene.");
        report.Check(island.PortDockBattleshipBerthFitsForTests,
            "External PVE dock berths are large enough for a battleship-class port preview instead of only fitting a small square dock tile.");
        report.Check(island.PortDockPreviewUsesRealShipLengthScaleForTests,
            "External dock ship previews preserve real ship-length ratios: Korshun 60 m, Barbet 150 m, Val 330 m.");
        report.Check(island.SuppressedSessionRendererCountForTests > 0,
            "Docked city view suppresses old session-world renderers while the isometric city is visible: "
            + island.SuppressedSessionRendererCountForTests + ".");
        report.Check(island.SuppressedActiveShipRendererCountForTests > 0,
            "Docked city view hides the active selected ship until the player opens dock/flight presentation: "
            + island.SuppressedActiveShipRendererCountForTests + ".");
        report.Check(island.IsCityCameraLeanRenderStateActiveForTests,
            "Docked city camera disables HDR/MSAA, camera post-processing, depth texture and opaque texture while the low-poly base view is visible.");
        report.Check(island.IsCityCameraUsingMeshCompatibleRendererForTests,
            "Docked city camera uses a mesh-compatible Universal renderer so the 3D city grid and buildings are visible: "
            + island.CityCameraRendererNameForTests + ".");
        GameObject sessionSceneObjects = GameObject.Find(SortieLocationIsolationController.SessionSceneObjectsRootName);
        report.Check(sessionSceneObjects == null || sessionSceneObjects.transform.childCount == 0,
            "Session Scene Objects is absent or empty; old static port/resource visuals are not in the hierarchy.");
        report.Check(GameObject.Find("Capital Port Proxy - Greenhaven") == null
            && GameObject.Find("Data Resource Field ore_field_tutorial_00") == null,
            "Docked session scene no longer contains the old capital proxy or tutorial resource field visuals.");
        report.Check(SessionSceneOmitsLegacyStaticVisualsForTests(ReadProjectText("Assets/Scenes/WildWindSessionScene.unity")),
            "WildWindSessionScene file omits the old static port/resource pocket; base island runtime owns docked city visuals.");
        report.Check(island.BuildingDefinitionCountForTests >= 25,
            "Base island loads the material city building catalog with all current building definitions: " + island.BuildingDefinitionCountForTests + ".");
        report.Check(!island.HasBuildingDefinitionForTests("dock")
            && !island.HasBuildingDefinitionForTests("project_yard"),
            "City building catalog omits legacy Dock and Project Yard entries; docks are handled by their own systems.");

        string[] footprintIds =
        {
            "anchor_house", "refinery", "gas_separator", "workshop", "scrapyard", "butchery", "laboratory",
            "archive", "trader_pavilion", "courier_service", "pve_dock", "relic", "capital_building", "pub", "monument",
            "beacon", "old_mechanism", "construction_yard", "metallurgy", "mechanical", "instrumentation",
            "chemical_reactor", "automaton_workshop", "electrical_workshop", "assembly_hall"
        };
        int[] footprintWidths =
        {
            4, 3, 4, 3, 3, 3, 3,
            4, 3, 4, 5, 2, 3, 2, 2,
            3, 2, 3, 3, 4, 3,
            4, 3, 3, 4
        };
        int[] footprintHeights =
        {
            4, 4, 2, 3, 4, 4, 3,
            4, 3, 3, 8, 2, 3, 3, 2,
            3, 2, 4, 3, 2, 3,
            3, 4, 4, 4
        };
        bool footprintsOk = true;
        for (int i = 0; i < footprintIds.Length; i++)
        {
            footprintsOk = island.TryGetBuildingDefinitionFootprintForTests(footprintIds[i], out int width, out int height)
                && width == footprintWidths[i]
                && height == footprintHeights[i]
                && footprintsOk;
        }
        report.Check(footprintsOk,
            "City building catalog records the requested rectangular footprints for core, special and eight cascade production buildings.");
        report.Check(island.GetBuildingDefinitionMaxCountForTests("refinery") == 2
            && island.GetBuildingDefinitionMaxCountForTests("gas_separator") == 2
            && island.GetBuildingDefinitionMaxCountForTests("scrapyard") == 2
            && island.GetBuildingDefinitionMaxCountForTests("butchery") == 2
            && island.GetBuildingDefinitionMaxCountForTests("laboratory") == 2
            && island.GetBuildingDefinitionMaxCountForTests("archive") == 2
            && island.GetBuildingDefinitionMaxCountForTests("workshop") == 5
            && island.GetBuildingDefinitionMaxCountForTests("pve_dock") == 3,
            "City building catalog stores current placement limits: processing x2, archive x2, workshop x5, PVE docks x3.");
        report.Check(island.BuildingCountForTests >= 9,
            "Base island exposes the initial clickable city buildings from the catalog: " + island.BuildingCountForTests + ".");

        string[] requiredBuildings =
        {
            "anchor_house",
            "refinery",
            "gas_separator",
            "workshop",
            "scrapyard",
            "butchery",
            "laboratory",
            "archive",
            "trader_pavilion"
        };
        for (int i = 0; i < requiredBuildings.Length; i++)
        {
            string buildingId = requiredBuildings[i];
            if (island.BuildingDefinitionHasPrefabForTests(buildingId))
            {
                report.Check(island.BuildingPrefabExistsForTests(buildingId),
                    "Base island prefab asset exists for building entry " + buildingId + ".");
                report.Check(island.BuildingUsedPrefabForTests(buildingId),
                    "Base island instantiated prefab asset for building entry " + buildingId + ".");
                report.Check(island.BuildingPrefabHasPhysicalMaterialsForTests(buildingId),
                    "Base island prefab " + buildingId + " has authored material and texture assets.");
            }
            else
            {
                report.Check(island.IsBuildingUsingProceduralVisualForTests(buildingId),
                    "Base island building " + buildingId + " uses a catalog visual kind while a replacement prefab is absent.");
            }

            report.Check(island.HasBuildingForTests(buildingId),
                "Base island contains building entry " + buildingId + ".");
            report.Check(island.BuildingHasVisualForTests(buildingId),
                "Base island building " + buildingId + " has a runtime visual renderer.");
            report.Check(island.BuildingHasEnabledVisualForTests(buildingId),
                "Base island building " + buildingId + " has an enabled runtime visual renderer.");
            report.Check(island.BuildingHasColliderForTests(buildingId),
                "Base island building " + buildingId + " has a runtime click collider.");
        }

        bool cityBuildingVisualRecovery =
            island.DeactivateBuildingVisualForTests("anchor_house")
            && !island.BuildingHasEnabledVisualForTests("anchor_house")
            && island.RecoverDefaultBuildingsForTests()
            && island.HasBuildingForTests("anchor_house")
            && island.BuildingHasEnabledVisualForTests("anchor_house")
            && island.BuildingHasColliderForTests("anchor_house");
        report.Check(cityBuildingVisualRecovery,
            cityBuildingVisualRecovery
                ? "Base island restores missing runtime building visuals instead of leaving the city without buildings."
                : "Base island must prune stale building records and recreate default building visuals if the runtime city loses them.");

        WildWindGameplayHud archiveHud = FindFirstObjectByType<WildWindGameplayHud>();
        bool archiveOwnsKnowledgeWindow = archiveHud != null
            && island.SelectBuildingForTests("archive")
            && island.OpenBuildingActionForTests(1)
            && archiveHud.IsKnowledgeScreenVisibleForTests
            && archiveHud.KnowledgeScreenHasVisibleTextForTests
            && archiveHud.KnowledgeScreenReportForTests.Contains("source=runtime")
            && ContainsAllIgnoreCase(archiveHud.KnowledgeScreenContentForTests, "SP")
            && !island.IsProcessingWindowOpenForTests
            && island.BuildingDefinitionHasPrefabForTests("archive")
            && island.BuildingUsedPrefabForTests("archive")
            && island.BuildingPrefabHasPhysicalMaterialsForTests("archive");
        if (archiveHud != null && archiveHud.IsKnowledgeScreenVisibleForTests)
        {
            archiveHud.CloseKnowledgeScreenForTests();
        }

        bool laboratoryNoLongerOwnsKnowledgeWindow = island.SelectBuildingForTests("laboratory")
            && island.OpenBuildingActionForTests(1)
            && !island.IsProcessingWindowOpenForTests
            && island.CloseWindowForTests();
        report.Check(archiveOwnsKnowledgeWindow && laboratoryNoLongerOwnsKnowledgeWindow,
            "Archive is the seeded authored knowledge building and opens the dedicated SP screen instead of the processing window; Laboratory no longer owns knowledge.");

        bool moveWithoutDuplicate = island.MoveBuildingWithRuntimeRefreshForTests(
            "workshop",
            16,
            16,
            out int exactWorkshopCountWhileMoving,
            out int exactWorkshopCountAfterMove,
            out int totalBuildingsBeforeMove,
            out int totalBuildingsAfterMove)
            && exactWorkshopCountWhileMoving == 0
            && exactWorkshopCountAfterMove == 1
            && totalBuildingsAfterMove == totalBuildingsBeforeMove;
        report.Check(moveWithoutDuplicate,
            "Moving a seeded base building through a runtime refresh does not duplicate it or occupy multiple footprints.");

        bool catalogOpened = island.OpenBuildingCatalogForTests();
        int archiveCountBefore = island.GetPlacedBuildingCountForTests("archive");
        bool archivePlaced = catalogOpened
            && island.PlaceCatalogBuildingForTests("archive")
            && island.GetPlacedBuildingCountForTests("archive") == archiveCountBefore + 1;
        report.Check(archivePlaced,
            "Grid project tool opens the building catalog and can place the second Archive on the first free city footprint.");
        bool archiveLimitBlocked = !island.PlaceCatalogBuildingForTests("archive")
            && island.GetPlacedBuildingCountForTests("archive") == island.GetBuildingDefinitionMaxCountForTests("archive")
            && island.BuildingCatalogStatusForTests.Contains("лимит");
        report.Check(archiveLimitBlocked,
            "Building catalog blocks placement when the configured per-building limit is reached.");

        int cityBuildingsBeforeDock = island.BuildingCountForTests;
        int dockCountBefore = island.GetPlacedBuildingCountForTests("pve_dock");
        report.Check(dockCountBefore == 3
                && island.ExternalDockPlacedCountForTests >= dockCountBefore
                && island.BuildingCountForTests == cityBuildingsBeforeDock
                && island.GetBuildingDefinitionMaxCountForTests("pve_dock") == 3,
            "Base island starts with three built external PVE docks so docked ships can be previewed in separate physical berths.");
        bool dockLimitBlocked = !island.PlaceCatalogBuildingForTests("pve_dock")
            && island.GetPlacedBuildingCountForTests("pve_dock") == island.GetBuildingDefinitionMaxCountForTests("pve_dock")
            && island.BuildingCatalogStatusForTests.Contains("лимит");
        report.Check(dockLimitBlocked,
            "Dock catalog entry blocks a fourth PVE dock while three physical PVE berths are currently allowed.");
        bool dockMoved = island.MoveExternalDockToFirstFreeSlotForTests("pve_dock")
            && island.GetPlacedBuildingCountForTests("pve_dock") == dockCountBefore;
        report.Check(dockMoved,
            "Built docks can move between their own external slots without duplicating or using the city grid.");
        report.Check(island.ShowExternalDockHoverForTests("pve_dock"),
            "Built external docks use the same camera-visible hover outline system as city buildings.");
        WildWindGameplayHud baseHud = FindFirstObjectByType<WildWindGameplayHud>();
        WildWindBaseIslandView islandAfterDockClick = null;
        bool dockScreenFromDock = baseHud != null
            && island.OpenExternalDockForTests("pve_dock")
            && baseHud.IsMetaDockScreenVisibleForTests
            && (islandAfterDockClick = WildWindBaseIslandView.EnsureForCurrentSessionScene()) != null
            && islandAfterDockClick.IsCityVisibleForTests
            && islandAfterDockClick.FocusSelectedDevelopmentDockShipForTests()
            && !string.IsNullOrWhiteSpace(islandAfterDockClick.FocusedPortDockKeyForTests);
        report.Check(dockScreenFromDock,
            "Clicking a built external dock opens the Dock screen directly over the live city and focuses that physical berth instead of the building radial menu.");
        report.Check(dockScreenFromDock && baseHud.IsMetaDockGameplayReadyForTests,
            "Dock screen contains the concrete quick-mission ship slot, selected-ship panel, reward panel and battle/sell buttons.");
        if (baseHud != null && baseHud.IsMetaDockScreenVisibleForTests)
        {
            baseHud.ReturnFromMetaDockScreenToPortForTests();
            WildWindBaseIslandView.EnsureForCurrentSessionScene();
        }

        MetaGameState quickDockMeta = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        bool quickDockLoopWorks = false;
        bool quickRawRewardsVary = false;
        bool starterPortLoopWorks = false;
        bool emptyDockSlotOpensDevelopment = false;
        bool developmentPurchaseReturnsToDock = false;
        bool developmentDockFreePurchaseWorks = false;
        bool developmentTreeAllMouseButtonsPan = false;
        bool coreCombatDockLaunchWorks = false;
        bool coreCombatRuntimeShipWorks = false;
        bool dockLoadoutDefaultsWork = false;
        bool portDockPreviewWorks = false;
        bool portDockSlotSwitchKeepsOrbit = false;
        bool portDockLoadoutPreviewUpdates = false;
        bool portDockKorshunMainLoadoutVisualMapCoversConfig = false;
        bool portDockKorshunMainLoadoutVisualsMounted = false;
        bool portDockKorshunAuxiliaryLoadoutVisualMapCoversConfig = false;
        bool portDockKorshunAuxiliaryLoadoutVisualsMounted = false;
        bool portDockKorshunLoadoutVisualsClean = false;
        string portDockLoadoutPreviewDetails = "";
        string coreCombatRuntimeShipDetails = "";
        bool coreCombatCommandProjectionWorks = false;
        string coreCombatCommandProjectionDetails = "";
        bool coreCombatMagnetIdleBeamSuppressed = false;
        string coreCombatMagnetIdleBeamDetails = "";
        bool coreCombatMagnetLoadoutVisualsWork = false;
        string coreCombatMagnetLoadoutVisualDetails = "";
        if (quickDockMeta != null && baseHud != null)
        {
            quickDockMeta.EnsureProgressInitialized();
            DockedDevelopmentShipState existingDockShip = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
            if (existingDockShip != null && existingDockShip.HasShip)
            {
                quickDockMeta.TrySellDevelopmentDockShip(existingDockShip.slotIndex, out _);
            }

            bool dockOpenedForEmptySlot = baseHud.OpenMetaDockScreenForTests()
                && baseHud.IsMetaDockGameplayReadyForTests;
            emptyDockSlotOpensDevelopment = dockOpenedForEmptySlot
                && baseHud.PressMetaDockShipSlotForTests(0)
                && baseHud.IsDevelopmentWindowOpenForTests
                && baseHud.IsDevelopmentWindowVisibleForTests
                && !baseHud.IsMetaDockScreenVisibleForTests;
            developmentTreeAllMouseButtonsPan = emptyDockSlotOpensDevelopment
                && baseHud.DragDevelopmentTreeForTests(PointerEventData.InputButton.Left, new Vector2(-160f, 160f))
                && baseHud.DragDevelopmentTreeForTests(PointerEventData.InputButton.Middle, new Vector2(80f, -80f))
                && baseHud.DragDevelopmentTreeForTests(PointerEventData.InputButton.Right, new Vector2(-90f, 90f));
            PortStorageState routeBuyStorage = quickDockMeta.GetCapitalStorageState();
            int freightBeforeRouteBuy = routeBuyStorage != null ? routeBuyStorage.GetResourceAmount("freight") : 0;
            bool boughtFromDevelopmentRoute = emptyDockSlotOpensDevelopment
                && baseHud.BuySelectedDevelopmentShipForTests();
            int freightAfterRouteBuy = routeBuyStorage != null ? routeBuyStorage.GetResourceAmount("freight") : 0;
            DockedDevelopmentShipState routeSlot = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
            developmentPurchaseReturnsToDock = boughtFromDevelopmentRoute
                && baseHud.IsMetaDockScreenVisibleForTests
                && !baseHud.IsDevelopmentWindowOpenForTests
                && routeSlot != null
                && routeSlot.HasShip;
            developmentDockFreePurchaseWorks = developmentPurchaseReturnsToDock
                && MetaGameState.GetDevelopmentDockShipPurchaseCost(null) == 0
                && freightAfterRouteBuy == freightBeforeRouteBuy;
            if (routeSlot != null && routeSlot.HasShip)
            {
                quickDockMeta.TrySellDevelopmentDockShip(routeSlot.slotIndex, out _);
            }

            if (baseHud.IsMetaDockScreenVisibleForTests)
            {
                baseHud.ReturnFromMetaDockScreenToPortForTests();
                WildWindBaseIslandView.EnsureForCurrentSessionScene();
            }

            bool developmentOpened = baseHud.OpenDevelopmentWindowForTests()
                && baseHud.IsDevelopmentWindowVisibleForTests
                && baseHud.IsDevelopmentWindowCatalogReadyForTests;
            bool boughtShip = developmentOpened && baseHud.BuySelectedDevelopmentShipForTests();
            bool boughtSecondPreviewShip = false;
            if (boughtShip)
            {
                quickDockMeta.SelectDevelopmentDockSlot(1);
                boughtSecondPreviewShip = quickDockMeta.TryBuyDevelopmentShipToDock("capital_artillery_cruiser_r02", out _);
                quickDockMeta.SelectDevelopmentDockSlot(0);
            }

            bool dockOpened = boughtShip
                && baseHud.OpenMetaDockScreenForTests()
                && baseHud.IsMetaDockGameplayReadyForTests;
            bool coreOnlyDockUi = dockOpened
                && baseHud.MetaDockMissionOfferCountForTests == 0
                && !baseHud.IsMetaDockResultWindowVisibleForTests;
            WildWindBaseIslandView dockPreviewIsland = dockOpened
                ? WildWindBaseIslandView.EnsureForCurrentSessionScene()
                : null;
            portDockPreviewWorks = dockPreviewIsland != null
                && dockPreviewIsland.FocusSelectedDevelopmentDockShipForTests()
                && dockPreviewIsland.IsPortDockBackdropReadyForTests
                && dockPreviewIsland.IsPortDockWorldInteractionSuppressedForTests;
            portDockSlotSwitchKeepsOrbit = portDockPreviewWorks
                && boughtSecondPreviewShip
                && baseHud.PressMetaDockShipSlotForTests(1)
                && dockPreviewIsland.FocusedPortDockSlotIndexForTests == 1
                && dockPreviewIsland.IsPortDockCameraOrbitActiveForTests
                && dockPreviewIsland.IsCityCameraHomeBlendActiveForTests
                && dockPreviewIsland.IsPortDockCameraOrbitLookingAtShipForTests
                && dockPreviewIsland.IsPortDockWorldInteractionSuppressedForTests;
            if (dockOpened)
            {
                baseHud.PressMetaDockShipSlotForTests(0);
            }

            if (portDockPreviewWorks)
            {
                List<DockedShipLoadoutSlotView> previewLoadoutSlots = quickDockMeta.GetDevelopmentDockLoadoutSlotsForUi(0);
                int mainLoadoutIndex = -1;
                int auxiliaryLoadoutIndex = -1;
                if (previewLoadoutSlots != null)
                {
                    for (int i = 0; i < previewLoadoutSlots.Count; i++)
                    {
                        DockedShipLoadoutSlotView view = previewLoadoutSlots[i];
                        if (view != null && string.Equals(view.slotId, "main", StringComparison.OrdinalIgnoreCase))
                        {
                            mainLoadoutIndex = i;
                        }

                        if (view != null && string.Equals(view.slotId, "auxiliary", StringComparison.OrdinalIgnoreCase))
                        {
                            auxiliaryLoadoutIndex = i;
                        }
                    }
                }

                string previewSignatureBefore = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                bool uiCycledMainLoadout = mainLoadoutIndex >= 0 && baseHud.PressMetaDockLoadoutSlotForTests(mainLoadoutIndex);
                string previewSignatureAfter = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                int generatedWeaponVisuals = dockPreviewIsland.SelectedPortDockPreviewGeneratedWeaponVisualCountForTests;
                string mainLoadoutMountFailureSignature = "";
                string mainLoadoutMountFailureDetails = "";
                portDockKorshunMainLoadoutVisualMapCoversConfig = dockPreviewIsland.KorshunMainLoadoutPreviewVisualMapCoversConfigForTests;
                portDockKorshunMainLoadoutVisualsMounted = dockPreviewIsland.SelectedPortDockPreviewMainWeaponMountedForTests;
                portDockKorshunLoadoutVisualsClean = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualsCleanForTests;
                string portDockKorshunLoadoutVisualCleanFailureDetails = portDockKorshunLoadoutVisualsClean
                    ? ""
                    : dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualCleanDetailsForTests;
                if (!portDockKorshunMainLoadoutVisualsMounted)
                {
                    mainLoadoutMountFailureSignature = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                    mainLoadoutMountFailureDetails = dockPreviewIsland.SelectedPortDockPreviewMainWeaponMountDetailsForTests;
                }

                portDockKorshunAuxiliaryLoadoutVisualMapCoversConfig = dockPreviewIsland.KorshunAuxiliaryLoadoutPreviewVisualMapCoversConfigForTests;
                portDockKorshunAuxiliaryLoadoutVisualsMounted = dockPreviewIsland.SelectedPortDockPreviewAuxiliaryWeaponMountedForTests;
                int mainLoadoutOptionCount = mainLoadoutIndex >= 0 && mainLoadoutIndex < previewLoadoutSlots.Count && previewLoadoutSlots[mainLoadoutIndex] != null
                    ? previewLoadoutSlots[mainLoadoutIndex].optionCount
                    : 0;
                if (mainLoadoutOptionCount > 1)
                {
                    for (int cycle = 1; cycle < mainLoadoutOptionCount; cycle++)
                    {
                        bool cycleMounted = baseHud.PressMetaDockLoadoutSlotForTests(mainLoadoutIndex)
                            && dockPreviewIsland.SelectedPortDockPreviewGeneratedWeaponVisualCountForTests >= 2
                            && dockPreviewIsland.SelectedPortDockPreviewMainWeaponMountedForTests;
                        bool cycleClean = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualsCleanForTests;
                        if (!cycleClean && string.IsNullOrWhiteSpace(portDockKorshunLoadoutVisualCleanFailureDetails))
                        {
                            portDockKorshunLoadoutVisualCleanFailureDetails = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualCleanDetailsForTests;
                        }

                        if (!cycleMounted && string.IsNullOrWhiteSpace(mainLoadoutMountFailureSignature))
                        {
                            mainLoadoutMountFailureSignature = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                            mainLoadoutMountFailureDetails = dockPreviewIsland.SelectedPortDockPreviewMainWeaponMountDetailsForTests;
                        }

                        portDockKorshunMainLoadoutVisualsMounted &= cycleMounted;
                        portDockKorshunLoadoutVisualsClean &= cycleClean;
                    }
                }

                string auxiliarySignatureBefore = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                bool uiCycledAuxiliaryLoadout = auxiliaryLoadoutIndex >= 0 && baseHud.PressMetaDockLoadoutSlotForTests(auxiliaryLoadoutIndex);
                string auxiliarySignatureAfter = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                int auxiliaryLoadoutOptionCount = auxiliaryLoadoutIndex >= 0 && auxiliaryLoadoutIndex < previewLoadoutSlots.Count && previewLoadoutSlots[auxiliaryLoadoutIndex] != null
                    ? previewLoadoutSlots[auxiliaryLoadoutIndex].optionCount
                    : 0;
                string auxiliaryLoadoutMountFailureSignature = "";
                string auxiliaryLoadoutMountFailureDetails = "";
                portDockKorshunAuxiliaryLoadoutVisualsMounted &= uiCycledAuxiliaryLoadout
                    && !string.Equals(auxiliarySignatureBefore, auxiliarySignatureAfter, StringComparison.Ordinal)
                    && dockPreviewIsland.SelectedPortDockPreviewGeneratedWeaponVisualCountForTests >= 4
                    && dockPreviewIsland.SelectedPortDockPreviewAuxiliaryWeaponMountedForTests;
                bool auxiliaryClean = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualsCleanForTests;
                if (!auxiliaryClean && string.IsNullOrWhiteSpace(portDockKorshunLoadoutVisualCleanFailureDetails))
                {
                    portDockKorshunLoadoutVisualCleanFailureDetails = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualCleanDetailsForTests;
                }

                portDockKorshunLoadoutVisualsClean &= auxiliaryClean;
                if (!portDockKorshunAuxiliaryLoadoutVisualsMounted && string.IsNullOrWhiteSpace(auxiliaryLoadoutMountFailureDetails))
                {
                    auxiliaryLoadoutMountFailureDetails = dockPreviewIsland.SelectedPortDockPreviewAuxiliaryWeaponMountDetailsForTests;
                }

                if (auxiliaryLoadoutOptionCount > 1)
                {
                    for (int cycle = 1; cycle < auxiliaryLoadoutOptionCount; cycle++)
                    {
                        bool cycleMounted = baseHud.PressMetaDockLoadoutSlotForTests(auxiliaryLoadoutIndex)
                            && dockPreviewIsland.SelectedPortDockPreviewGeneratedWeaponVisualCountForTests >= 4
                            && dockPreviewIsland.SelectedPortDockPreviewAuxiliaryWeaponMountedForTests;
                        bool cycleClean = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualsCleanForTests;
                        if (!cycleClean && string.IsNullOrWhiteSpace(portDockKorshunLoadoutVisualCleanFailureDetails))
                        {
                            portDockKorshunLoadoutVisualCleanFailureDetails = dockPreviewIsland.SelectedPortDockPreviewKorshunLoadoutVisualCleanDetailsForTests;
                        }

                        if (!cycleMounted && string.IsNullOrWhiteSpace(auxiliaryLoadoutMountFailureSignature))
                        {
                            auxiliaryLoadoutMountFailureSignature = dockPreviewIsland.SelectedPortDockPreviewLoadoutSignatureForTests;
                            auxiliaryLoadoutMountFailureDetails = dockPreviewIsland.SelectedPortDockPreviewAuxiliaryWeaponMountDetailsForTests;
                        }

                        portDockKorshunAuxiliaryLoadoutVisualsMounted &= cycleMounted;
                        portDockKorshunLoadoutVisualsClean &= cycleClean;
                    }
                }

                portDockLoadoutPreviewUpdates = uiCycledMainLoadout
                    && !string.Equals(previewSignatureBefore, previewSignatureAfter, StringComparison.Ordinal)
                    && previewSignatureAfter.Contains("|main=")
                    && generatedWeaponVisuals >= 2
                    && portDockKorshunMainLoadoutVisualMapCoversConfig
                    && portDockKorshunMainLoadoutVisualsMounted
                    && portDockKorshunAuxiliaryLoadoutVisualMapCoversConfig
                    && portDockKorshunAuxiliaryLoadoutVisualsMounted
                    && portDockKorshunLoadoutVisualsClean;
                portDockLoadoutPreviewDetails = "index="
                    + mainLoadoutIndex
                    + "/"
                    + auxiliaryLoadoutIndex
                    + ", before="
                    + previewSignatureBefore
                    + ", after="
                    + previewSignatureAfter
                    + ", generated="
                    + generatedWeaponVisuals
                    + ", map="
                    + portDockKorshunMainLoadoutVisualMapCoversConfig
                    + ", mounted="
                    + portDockKorshunMainLoadoutVisualsMounted
                    + ", mainMountFail="
                    + mainLoadoutMountFailureSignature
                    + ", mainMountDetails="
                    + mainLoadoutMountFailureDetails
                    + ", auxBefore="
                    + auxiliarySignatureBefore
                    + ", auxAfter="
                    + auxiliarySignatureAfter
                    + ", auxMap="
                    + portDockKorshunAuxiliaryLoadoutVisualMapCoversConfig
                    + ", auxMounted="
                    + portDockKorshunAuxiliaryLoadoutVisualsMounted
                    + ", clean="
                    + portDockKorshunLoadoutVisualsClean
                    + ", cleanDetails="
                    + portDockKorshunLoadoutVisualCleanFailureDetails
                    + ", auxMountFail="
                    + auxiliaryLoadoutMountFailureSignature
                    + ", auxMountDetails="
                    + auxiliaryLoadoutMountFailureDetails
                    + ".";
            }

            bool soldShip = dockOpened && baseHud.SellSelectedDockShipForTests();
            if (boughtSecondPreviewShip)
            {
                quickDockMeta.TrySellDevelopmentDockShip(1, out _);
            }

            quickDockLoopWorks = developmentOpened
                && boughtShip
                && dockOpened
                && coreOnlyDockUi
                && portDockPreviewWorks
                && soldShip;

            if (baseHud.IsMetaDockScreenVisibleForTests)
            {
                baseHud.ReturnFromMetaDockScreenToPortForTests();
                WildWindBaseIslandView.EnsureForCurrentSessionScene();
            }

            PortStorageState quickStorage = quickDockMeta.GetCapitalStorageState();
            DockedDevelopmentShipState dockSlot = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
            if (dockSlot != null && dockSlot.HasShip)
            {
                quickDockMeta.TrySellDevelopmentDockShip(dockSlot.slotIndex, out _);
            }

            bool boughtMiner = quickDockMeta.TryBuyDevelopmentShipToDock("capital_patrol_frigate_r02", out _);
            int rawRewardStacksBefore = CountStorageResourceTotal(quickStorage);
            string rawMessageA = "";
            string rawMessageB = "";
            string rawMessageC = "";
            bool rawSortieA = boughtMiner && quickDockMeta.TryRunQuickDevelopmentSortie(0, out rawMessageA);
            bool rawSortieB = rawSortieA && quickDockMeta.TryRunQuickDevelopmentSortie(0, out rawMessageB);
            bool rawSortieC = rawSortieB && quickDockMeta.TryRunQuickDevelopmentSortie(0, out rawMessageC);
            int rawRewardStacksAfter = CountStorageResourceTotal(quickStorage);
            quickRawRewardsVary = rawSortieC
                && rawRewardStacksAfter > rawRewardStacksBefore
                && !string.Equals(rawMessageA, rawMessageB, StringComparison.Ordinal)
                && !string.Equals(rawMessageB, rawMessageC, StringComparison.Ordinal);
            quickDockMeta.TrySellDevelopmentDockShip(0, out _);

            PlayerProgress starterLoopSnapshot = quickDockMeta.CreateProgressSnapshot();
            try
            {
                quickDockMeta.ReplaceProgress(new PlayerProgress());
                quickDockMeta.EnsureProgressInitialized();
                PortStorageState starterStorage = quickDockMeta.GetCapitalStorageState();
                int starterFreightBefore = starterStorage != null ? starterStorage.GetResourceAmount("freight") : 0;
                int starterExperienceBefore = starterStorage != null ? starterStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) : 0;
                bool starterShipBought = quickDockMeta.TryBuyDevelopmentShipToDock("capital_patrol_frigate_r02", out _);
                bool starterSortieA = starterShipBought && quickDockMeta.TryRunQuickDevelopmentSortie(0, out _);
                bool starterSortieB = starterSortieA && quickDockMeta.TryRunQuickDevelopmentSortie(0, out _);
                bool starterSortieC = starterSortieB && quickDockMeta.TryRunQuickDevelopmentSortie(0, out _);
                string starterLoadMessage = "";
                string starterCollectMessage = "";
                bool starterOreLoaded = starterSortieC && quickDockMeta.TryLoadAllBaseProcessingInputs("refinery", BaseProcessingBranch.Ore, 5, out starterLoadMessage);
                long starterProcessTicks = Math.Max(DateTime.UtcNow.Ticks, quickDockMeta.progress.lastProcessUtcTicks);
                quickDockMeta.progress.lastProcessUtcTicks = starterProcessTicks;
                int starterCycles = starterOreLoaded
                    ? quickDockMeta.AdvanceRealTimeProcesses(new DateTime(starterProcessTicks, DateTimeKind.Utc).AddSeconds(130))
                    : 0;
                bool starterCollected = starterCycles > 0
                    && quickDockMeta.TryCollectBaseProcessingOutputs("refinery", BaseProcessingBranch.Ore, 5, out starterCollectMessage);
                int starterProcessedMinerals = starterStorage != null
                    ? starterStorage.GetResourceAmount("iron") + starterStorage.GetResourceAmount("calcite")
                    : 0;
                int starterCourierSends = 0;
                string starterCourierMessage = "";
                if (starterCollected)
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        if (!quickDockMeta.TrySendCourierOrder(0, out starterCourierMessage))
                        {
                            break;
                        }

                        starterCourierSends++;
                    }
                }

                int starterFreightAfter = starterStorage != null ? starterStorage.GetResourceAmount("freight") : 0;
                int starterExperienceAfter = starterStorage != null ? starterStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) : 0;
                bool paidShipAffordable = MetaGameState.GetDevelopmentDockShipPurchaseCost(null) == 0 || starterFreightAfter >= 9000;

                DockedDevelopmentShipState starterDockShip = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
                if (starterDockShip != null && starterDockShip.HasShip)
                {
                    quickDockMeta.TrySellDevelopmentDockShip(starterDockShip.slotIndex, out _);
                }

                bool paidShipBought = quickDockMeta.TryBuyDevelopmentShipToDock("capital_patrol_frigate_r02", out _);
                starterPortLoopWorks = starterShipBought
                    && starterSortieC
                    && starterOreLoaded
                    && starterCycles > 0
                    && starterCollected
                    && starterProcessedMinerals > 0
                    && starterCourierSends >= 2
                    && starterFreightAfter > starterFreightBefore
                    && starterExperienceAfter > starterExperienceBefore
                    && paidShipAffordable
                    && paidShipBought
                    && !string.IsNullOrWhiteSpace(starterLoadMessage)
                    && !string.IsNullOrWhiteSpace(starterCollectMessage)
                    && !string.IsNullOrWhiteSpace(starterCourierMessage);
            }
            finally
            {
                quickDockMeta.ReplaceProgress(starterLoopSnapshot);
            }

            PlayerProgress coreCombatSnapshot = quickDockMeta.CreateProgressSnapshot();
            try
            {
                quickDockMeta.ReplaceProgress(new PlayerProgress());
                quickDockMeta.EnsureProgressInitialized();
                bool coreShipBought = quickDockMeta.TryBuyDevelopmentShipToDock("capital_patrol_frigate_r02", out _);
                List<DockedShipLoadoutSlotView> coreLoadoutSlots = coreShipBought
                    ? quickDockMeta.GetDevelopmentDockLoadoutSlotsForUi(0)
                    : null;
                bool hasHullLoadout = false;
                bool hasMainLoadout = false;
                bool hasAuxiliaryLoadout = false;
                if (coreLoadoutSlots != null)
                {
                    for (int i = 0; i < coreLoadoutSlots.Count; i++)
                    {
                        DockedShipLoadoutSlotView loadout = coreLoadoutSlots[i];
                        if (loadout == null || string.IsNullOrWhiteSpace(loadout.selectedPackageId))
                        {
                            continue;
                        }

                        hasHullLoadout |= string.Equals(loadout.slotId, "hull", StringComparison.OrdinalIgnoreCase);
                        hasMainLoadout |= string.Equals(loadout.slotId, "main", StringComparison.OrdinalIgnoreCase);
                        hasAuxiliaryLoadout |= string.Equals(loadout.slotId, "auxiliary", StringComparison.OrdinalIgnoreCase);
                    }
                }

                DockedDevelopmentShipState selectedAfterDefaults = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
                bool fastHullDefaulted = selectedAfterDefaults != null
                    && selectedAfterDefaults.loadoutDefaultsVersion >= 1
                    && string.Equals(selectedAfterDefaults.GetLoadoutPackageId("hull"), "korshun_hull_fast", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(selectedAfterDefaults.GetLoadoutPackageId("power"), "korshun_power_steam_gas", StringComparison.OrdinalIgnoreCase);
                bool cycledMainLoadout = coreShipBought && quickDockMeta.TryCycleDevelopmentDockLoadoutPackage(0, "main", out _);
                DockedDevelopmentShipState selectedAfterCycle = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
                DockedDevelopmentShipState selectedForCoreCombat = selectedAfterCycle ?? selectedAfterDefaults;
                if (selectedForCoreCombat != null)
                {
                    selectedForCoreCombat.SetLoadoutPackageId("auxiliary", "korshun_aux_magnet");
                }

                dockLoadoutDefaultsWork = coreLoadoutSlots != null
                    && coreLoadoutSlots.Count >= 4
                    && hasHullLoadout
                    && hasMainLoadout
                    && hasAuxiliaryLoadout
                    && fastHullDefaulted
                    && cycledMainLoadout
                    && selectedAfterCycle != null
                    && string.Equals(selectedAfterCycle.GetLoadoutPackageId("hull"), "korshun_hull_fast", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(selectedAfterCycle.GetLoadoutPackageId("main"));
                DockedDevelopmentShipState coreSlotBefore = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
                int sortiesBeforeCoreCombat = coreSlotBefore != null ? coreSlotBefore.sortiesRemaining : -1;
                bool coreCombatStarted = coreShipBought && quickDockMeta.BeginCoreTacticalIntroCombatSortie();
                CoreTacticalCombatSortieController coreCombatController = null;
                DockedDevelopmentShipState coreSlotAfter = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
                SortieSessionState coreSortie = quickDockMeta.ActiveSortie;
                coreCombatDockLaunchWorks = coreCombatStarted
                    && quickDockMeta.CurrentMode == GameSessionMode.Flight
                    && quickDockMeta.HasActiveSortie
                    && CoreTacticalCombatSortieController.IsCoreTacticalCombatSortieActive(quickDockMeta)
                    && coreSortie != null
                    && coreSortie.zone != null
                    && coreSortie.zone.sortieId == SessionExtractionConstants.CoreTacticalIntroCombatSortieId
                    && coreSlotAfter != null
                    && coreSlotAfter.sortiesRemaining == Mathf.Max(0, sortiesBeforeCoreCombat - 1);
                if (coreCombatDockLaunchWorks)
                {
                    coreCombatController = CoreTacticalCombatSortieController.EnsureForActiveSortie(quickDockMeta);
                    if (coreCombatController == null)
                    {
                        coreCombatRuntimeShipDetails = "Core Tactical controller was not created.";
                    }
                    else if (!coreCombatController.TryBuildForTests())
                    {
                        coreCombatRuntimeShipDetails = "Core Tactical controller refused to build for the active sortie.";
                    }
                    else if (!CoreTacticalCombatSortieController.TryGetActivePlayerRuntimeSnapshotForTests(out CoreTacticalCombatSortieController.CoreTacticalRuntimeProfileSnapshot activeRuntime))
                    {
                        coreCombatRuntimeShipDetails = "Core Tactical player runtime snapshot is unavailable after build.";
                    }
                    else
                    {
                        coreCombatRuntimeShipWorks = ValidateKorshunActiveRuntimeSnapshot(
                            activeRuntime,
                            out coreCombatRuntimeShipDetails,
                            expectedSpeedMS: 162f,
                            expectedAccelerationMS2: 46.63f,
                            expectedYawDegPerSecond: 36f,
                            expectedEntrySpeedMS: 365f,
                            expectedTargetDistanceMeters: 20000f,
                            expectedHullPackageId: "korshun_hull_fast",
                            expectedPowerPackageId: "korshun_power_steam_gas",
                            expectedTorpedoActive: false);
                        coreCombatCommandProjectionWorks = ValidateCoreTacticalLiveCommandProjection(
                            coreCombatController.GetComponent<CoreTacticalFleetController>(),
                            out coreCombatCommandProjectionDetails);
                        coreCombatMagnetIdleBeamSuppressed = ValidateNoIdleMagnetAuxiliaryBeamEmitters(
                            out coreCombatMagnetIdleBeamDetails);
                        coreCombatMagnetLoadoutVisualsWork = ValidateKorshunCombatMagnetLoadoutVisuals(
                            out coreCombatMagnetLoadoutVisualDetails);
                    }

                    if (coreCombatController != null)
                    {
                        DestroyBigTestObject(coreCombatController.gameObject);
                    }
                }
            }
            finally
            {
                quickDockMeta.ReplaceProgress(coreCombatSnapshot);
            }
        }

        report.Check(quickDockLoopWorks,
            "Development tree can buy a ship into the dock, the dock exposes no quick/ordinary mission launch UI, and the ship can still be sold.");
        report.Check(portDockPreviewWorks,
            "A bought dock ship appears as its authored imported ship model at the physical external berth while the Dock screen stays over the live city.");
        report.Check(portDockSlotSwitchKeepsOrbit,
            "Switching between bought dock ships blends directly from one port ellipse to the next, keeps the port orbit active, and suppresses city/dock picking under the Dock screen.");
        report.Check(portDockLoadoutPreviewUpdates,
            portDockLoadoutPreviewUpdates
                ? "Changing the Korshun main and auxiliary packages from the Dock screen replaces mounted ship visuals across all configured options."
                : "Changing the Korshun main and auxiliary packages from the Dock screen must replace mounted ship visuals across all configured options. " + portDockLoadoutPreviewDetails);
        report.Check(emptyDockSlotOpensDevelopment,
            "Clicking an empty dock ship slot acts as the Buy route and opens the Development window instead of only selecting an empty slot.");
        report.Check(developmentPurchaseReturnsToDock,
            "Buying from the Development window places the ship into the selected dock slot and returns to the Dock screen so the result is visible.");
        report.Check(developmentDockFreePurchaseWorks,
            "Current dock prototype buys ships from the Development window without requiring or spending Freight.");
        report.Check(developmentTreeAllMouseButtonsPan,
            "Development tree panning captures the pointer and scrolls with left, middle and right mouse buttons.");
        report.Check(dockLoadoutDefaultsWork,
            "Development dock creates persistent fast-hull default loadout selections and can cycle the selected main weapon before launching core combat.");
        report.Check(quickRawRewardsVary,
            "The internal starter economy simulation still returns concrete randomized quick-sortie rewards instead of the same fixed reward every run.");
        report.Check(starterPortLoopWorks,
            "Starter port loop works end-to-end: quick sorties bring raw ore, refinery turns it into processed minerals, courier delivery grants Freight/mastery XP, and an R2 dock ship can be bought after the economy path.");
        report.Check(coreCombatDockLaunchWorks,
            "Dock battle launch starts a real Core Tactical sortie from the selected dock ship, enters Flight mode and consumes one dock sortie.");
        report.Check(coreCombatRuntimeShipWorks,
            coreCombatRuntimeShipWorks
                    ? "Dock battle launch builds the actual active Korshun player ship from the selected fast hull, starts in full-speed 365 m/s slip toward the center fly-through, keeps live infinite package weapons, and has no phantom MSL group."
                : "Dock battle launch built a broken active player ship: " + coreCombatRuntimeShipDetails);
        report.Check(coreCombatMagnetLoadoutVisualsWork,
            coreCombatMagnetLoadoutVisualsWork
                ? "Dock battle launch applies the selected Korshun auxiliary visuals in combat: magnet loadout mounts two magnets and hides default torpedo launchers. " + coreCombatMagnetLoadoutVisualDetails
                : "Dock battle launch must apply the selected Korshun auxiliary visuals in combat instead of leaving torpedo visuals on a magnet loadout. " + coreCombatMagnetLoadoutVisualDetails);
        report.Check(coreCombatCommandProjectionWorks,
            coreCombatCommandProjectionWorks
                ? "Dock battle launch live Core Tactical camera projects simulated RMB clicks back onto the clicked screen pixels: " + coreCombatCommandProjectionDetails
                : "Dock battle launch live Core Tactical camera must project simulated RMB clicks back onto the clicked screen pixels: " + coreCombatCommandProjectionDetails);
        report.Check(coreCombatMagnetIdleBeamSuppressed,
            coreCombatMagnetIdleBeamSuppressed
                ? "Dock battle launch keeps magnet beams event-driven: no idle Magnet auxiliary beam emitter is installed before a fragment is actually being delivered."
                : "Dock battle launch must not draw idle magnet beams when no ore fragment is being delivered: " + coreCombatMagnetIdleBeamDetails);
        bool cameraHomeButtonStartsBlend = island.OffsetCityCameraForTests(-3f, 2f, -11f, 3f, 1.2f)
            && baseHud != null
            && baseHud.PressMetaProjectCameraButtonForTests()
            && island.IsCityCameraHomeBlendActiveForTests
            && !island.IsCityCameraAtDefaultViewForTests;
        report.Check(cameraHomeButtonStartsBlend,
            "Lower-left camera control starts a smooth return to the base city camera pose instead of teleporting instantly.");

        MetaGameState runtimeMeta = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        if (runtimeMeta != null)
        {
            runtimeMeta.EnsureProgressInitialized();
        }

        bool knowledgeSpModelWorks = false;
        if (runtimeMeta != null && runtimeMeta.progress != null && runtimeMeta.SessionConfig != null)
        {
            PlayerProgress knowledgeSnapshot = runtimeMeta.CreateProgressSnapshot();
            try
            {
                runtimeMeta.ReplaceProgress(new PlayerProgress());
                runtimeMeta.EnsureProgressInitialized();
                TechnologyConfig knowledge = runtimeMeta.SessionConfig.GetTechnology("tech_base_storehouse_ledgers");
                PortStorageState knowledgeStorage = runtimeMeta.GetCapitalStorageState();
                if (knowledge != null && knowledgeStorage != null)
                {
                    for (int i = 0; i < knowledge.cycleCost.Count; i++)
                    {
                        TechnologyCostConfig cost = knowledge.cycleCost[i];
                        if (cost == null || string.IsNullOrWhiteSpace(cost.itemId)) continue;
                        knowledgeStorage.AddResource(cost.itemId, Mathf.Max(0, cost.amount));
                    }

                    long knowledgeStartTicks = DateTime.UtcNow.Ticks;
                    runtimeMeta.progress.lastProcessUtcTicks = knowledgeStartTicks;
                    bool selectedKnowledge = runtimeMeta.TrySelectResearchTechnology(knowledge.id);
                    TechnologyResearchProgress selectedState = runtimeMeta.GetTechnologyResearchProgress(knowledge.id);
                    bool requirementsPaidOnSelect = selectedState != null && selectedState.currentLevelRequirementsPaid;
                    int progressedEvents = runtimeMeta.AdvanceRealTimeProcesses(new DateTime(knowledgeStartTicks, DateTimeKind.Utc).AddMinutes(10));
                    TechnologyResearchProgress progressedState = runtimeMeta.GetTechnologyResearchProgress(knowledge.id);
                    float archiveSpProgress = progressedState != null ? progressedState.currentLevelSpProgress : 0f;
                    runtimeMeta.GrantKnowledgeSpPackage("big_test_base_sp", "category", "base", 50000);
                    bool packageApplied = runtimeMeta.TryApplyKnowledgeSpPackage(
                        "big_test_base_sp",
                        knowledge.id,
                        out int appliedSp,
                        out int burnedSp,
                        out string packageReason);
                    bool bookUnlocked = runtimeMeta.UnlockKnowledgeFromBook("tech_base_queue_dispatch", out string bookReason)
                        && runtimeMeta.progress.IsKnowledgeUnlocked("tech_base_queue_dispatch");

                    knowledgeSpModelWorks = selectedKnowledge
                        && selectedState != null
                        && requirementsPaidOnSelect
                        && progressedEvents > 0
                        && archiveSpProgress >= 29f
                        && archiveSpProgress < runtimeMeta.GetTechnologyLevelSpCost(knowledge, 1)
                        && packageApplied
                        && appliedSp > 0
                        && burnedSp > 40000
                        && runtimeMeta.GetTechnologyCompletedLevel(knowledge) == 1
                        && bookUnlocked
                        && !string.IsNullOrWhiteSpace(packageReason)
                        && !string.IsNullOrWhiteSpace(bookReason);
                }
            }
            finally
            {
                runtimeMeta.ReplaceProgress(knowledgeSnapshot);
            }
        }

        report.Check(knowledgeSpModelWorks,
            "Knowledge research uses Archive SP output, book/right unlocks, SP packages and burned overflow instead of timer accelerators.");

        bool courierBuildingSeeded = island.HasBuildingDefinitionForTests("courier_service")
            && island.HasBuildingForTests("courier_service")
            && island.BuildingHasVisualForTests("courier_service")
            && island.BuildingHasColliderForTests("courier_service");
        report.Check(courierBuildingSeeded,
            "Courier Service building for the Wind Houses is seeded into the starting base island and is clickable.");

        IReadOnlyList<CourierOrderSlotState> courierSlots = runtimeMeta != null ? runtimeMeta.GetCourierOrderSlots() : null;
        bool courierSlotsReady = courierSlots != null && courierSlots.Count >= MetaGameState.CourierOrderSlotCount;
        if (courierSlotsReady)
        {
            for (int i = 0; i < MetaGameState.CourierOrderSlotCount; i++)
            {
                CourierOrderSlotState slot = runtimeMeta.GetCourierOrderSlot(i);
                courierSlotsReady &= slot != null
                    && slot.slotIndex == i
                    && slot.HasActiveOrder
                    && !string.IsNullOrWhiteSpace(slot.customerFactionId)
                    && !string.IsNullOrWhiteSpace(slot.customerFactionNameRu)
                    && slot.inputs.Count >= 1
                    && slot.inputs.Count <= 3
                    && slot.freightReward > 0
                    && slot.designExperienceReward > 0
                    && slot.reputationReward > 0;
            }
        }

        report.Check(courierSlotsReady,
            "Courier Service initializes eight persistent courier order slots, each with a customer faction, 1-3 requested resources, freight/mastery rewards and small reputation.");

        PortStorageState courierStorage = runtimeMeta != null ? runtimeMeta.GetCapitalStorageState() : null;
        CourierOrderSlotState sendSlot = runtimeMeta != null ? runtimeMeta.GetCourierOrderSlot(0) : null;
        List<CascadeItemAmount> sendInputs = sendSlot != null ? CloneCascadeItemsForTest(sendSlot.inputs) : new List<CascadeItemAmount>();
        int sendFreightReward = sendSlot != null ? sendSlot.freightReward : 0;
        int sendExperienceReward = sendSlot != null ? sendSlot.designExperienceReward : 0;
        int sendReputationReward = sendSlot != null ? sendSlot.reputationReward : 0;
        string sendFactionId = sendSlot != null ? sendSlot.customerFactionId : "";
        string sendFactionName = sendSlot != null ? sendSlot.customerFactionNameRu : "";
        string sendOrderId = sendSlot != null ? sendSlot.orderId : "";
        for (int i = 0; i < sendInputs.Count && courierStorage != null; i++)
        {
            CascadeItemAmount input = sendInputs[i];
            if (input == null) continue;
            if (courierStorage.GetResourceAmount(input.itemId) < input.amount)
            {
                courierStorage.AddResource(input.itemId, input.amount - courierStorage.GetResourceAmount(input.itemId));
            }
        }

        int[] courierInputBefore = CaptureStorageAmounts(courierStorage, sendInputs);
        int courierFreightBefore = courierStorage != null ? courierStorage.GetResourceAmount("freight") : 0;
        int courierExperienceBefore = courierStorage != null ? courierStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) : 0;
        int courierReputationBefore = runtimeMeta != null && runtimeMeta.progress != null
            ? runtimeMeta.progress.GetQuestMetricValue("faction_reputation", sendFactionId)
            : 0;
        bool courierWindowOpened = island.OpenCourierServiceWindowForTests()
            && island.IsCourierWindowOpenForTests
            && island.IsWindowModalForTests
            && island.OpenWindowTitleForTests.Contains("Курьерская служба")
            && island.WindowContentForTests.Contains(string.IsNullOrWhiteSpace(sendFactionName) ? "Ветровые Дома" : sendFactionName)
            && island.WindowContentForTests.Contains("Награда");
        bool courierSent = courierWindowOpened
            && island.SelectCourierOrderForTests(0)
            && island.SendSelectedCourierOrderForTests();
        CourierOrderSlotState refreshedSendSlot = runtimeMeta != null ? runtimeMeta.GetCourierOrderSlot(0) : null;
        bool courierSendEconomy = courierSent
            && runtimeMeta != null
            && runtimeMeta.progress != null
            && courierStorage != null
            && StorageAmountsMatchDelta(courierStorage, sendInputs, courierInputBefore, -1)
            && courierStorage.GetResourceAmount("freight") == courierFreightBefore + sendFreightReward
            && courierStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) == courierExperienceBefore + sendExperienceReward
            && runtimeMeta.progress.GetQuestMetricValue("faction_reputation", sendFactionId) == courierReputationBefore + sendReputationReward
            && refreshedSendSlot != null
            && refreshedSendSlot.HasActiveOrder
            && refreshedSendSlot.orderId != sendOrderId;
        report.Check(courierWindowOpened && courierSendEconomy,
            "Courier Service window opens from the base building; sending an order spends storage goods, grants Freight, mastery XP and customer-faction reputation, then rolls the next courier ship.");

        CourierOrderSlotState cancelSlot = runtimeMeta != null ? runtimeMeta.GetCourierOrderSlot(1) : null;
        string cancelledOrderId = cancelSlot != null ? cancelSlot.orderId : "";
        bool courierCancelled = island.SelectCourierOrderForTests(1)
            && island.CancelSelectedCourierOrderForTests();
        int cooldownRemaining = runtimeMeta != null ? runtimeMeta.GetCourierOrderCooldownRemainingSeconds(1) : 0;
        CourierOrderSlotState coolingSlot = runtimeMeta != null ? runtimeMeta.GetCourierOrderSlot(1) : null;
        bool courierCooldown = courierCancelled
            && coolingSlot != null
            && !coolingSlot.HasActiveOrder
            && cooldownRemaining > MetaGameState.CourierCancelCooldownSeconds - 30
            && island.WindowContentForTests.Contains("Заявка отменена");
        if (runtimeMeta != null)
        {
            runtimeMeta.FastForwardSimulation(TimeSpan.FromMinutes(16));
        }

        island.SelectCourierOrderForTests(1);
        CourierOrderSlotState rerolledSlot = runtimeMeta != null ? runtimeMeta.GetCourierOrderSlot(1) : null;
        bool courierRerollsAfterCooldown = rerolledSlot != null
            && rerolledSlot.HasActiveOrder
            && runtimeMeta.GetCourierOrderCooldownRemainingSeconds(1) == 0
            && rerolledSlot.orderId != cancelledOrderId
            && island.WindowContentForTests.Contains("Награда");
        island.CloseWindowForTests();
        report.Check(courierCooldown && courierRerollsAfterCooldown,
            "Cancelling a courier order starts a 15-minute refresh timer, blocks the slot, then restores a new persistent order after the cooldown.");

        bool capitalAirdockSeeded = island.HasBuildingForTests("capital_airdock");
        report.Check(capitalAirdockSeeded,
            "Capital Airdock is seeded into the starting city so the daily solid airplane is reachable from the base.");

        CapitalAirplaneState airplaneState = runtimeMeta != null ? runtimeMeta.GetCapitalAirplaneState(1) : null;
        PortStorageState airplaneStorage = runtimeMeta != null ? runtimeMeta.GetCapitalStorageState() : null;
        List<CascadeItemAmount> airplaneInputs = airplaneState != null ? CloneCascadeItemsForTest(airplaneState.inputs) : new List<CascadeItemAmount>();
        for (int i = 0; i < airplaneInputs.Count && airplaneStorage != null; i++)
        {
            CascadeItemAmount input = airplaneInputs[i];
            if (input == null) continue;
            int available = airplaneStorage.GetResourceAmount(input.itemId);
            if (available < input.amount)
            {
                airplaneStorage.AddResource(input.itemId, input.amount - available);
            }
        }

        string airplaneIdBefore = airplaneState != null ? airplaneState.planeId : "";
        int airplaneSolidReward = airplaneState != null ? airplaneState.solidReward : 0;
        int airplaneFreightReward = airplaneState != null ? airplaneState.freightReward : 0;
        int airplaneExperienceReward = airplaneState != null ? airplaneState.designExperienceReward : 0;
        int airplaneRemainingBefore = runtimeMeta != null ? runtimeMeta.GetCapitalAirplaneRemainingSeconds(1) : 0;
        int[] airplaneInputBefore = CaptureStorageAmounts(airplaneStorage, airplaneInputs);
        int airplaneSolidBefore = airplaneStorage != null ? airplaneStorage.GetResourceAmount("solid") : 0;
        int airplaneFreightBefore = airplaneStorage != null ? airplaneStorage.GetResourceAmount("freight") : 0;
        int airplaneExperienceBefore = airplaneStorage != null ? airplaneStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) : 0;

        bool airplaneWindowOpened = island.OpenCapitalAirplaneWindowForTests()
            && island.IsCapitalAirplaneWindowOpenForTests
            && island.IsWindowModalForTests
            && island.OpenWindowTitleForTests.Contains("Столичный аэродром")
            && island.WindowContentForTests.Contains("Столичный самолет")
            && island.WindowContentForTests.Contains("24 часа")
            && island.WindowContentForTests.Contains("Солиды")
            && island.WindowContentForTests.Contains("Требуется загрузить");
        bool airplaneSent = airplaneWindowOpened && island.SendCapitalAirplaneForTests();
        CapitalAirplaneState airplaneSentState = runtimeMeta != null ? runtimeMeta.GetCapitalAirplaneState(1) : null;
        bool airplaneStateSent = airplaneSentState != null && airplaneSentState.sent;
        bool airplaneSamePlane = airplaneSentState != null && airplaneSentState.planeId == airplaneIdBefore;
        bool airplaneRemainingFresh = airplaneRemainingBefore > 0
            && airplaneRemainingBefore <= MetaGameState.CapitalAirplaneCycleSeconds;
        bool airplaneInputsSpent = StorageAmountsMatchDelta(airplaneStorage, airplaneInputs, airplaneInputBefore, -1);
        bool airplaneSolidGranted = airplaneStorage != null
            && airplaneStorage.GetResourceAmount("solid") == airplaneSolidBefore + airplaneSolidReward;
        bool airplaneFreightGranted = airplaneStorage != null
            && airplaneStorage.GetResourceAmount("freight") == airplaneFreightBefore + airplaneFreightReward;
        bool airplaneExperienceGranted = airplaneStorage != null
            && airplaneStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) == airplaneExperienceBefore + airplaneExperienceReward;
        bool airplaneSentMessageVisible = island.WindowContentForTests.Contains("Новый прилетит");
        bool airplaneEconomy = airplaneSent
            && airplaneStorage != null
            && airplaneStateSent
            && airplaneSamePlane
            && airplaneRemainingFresh
            && airplaneInputsSpent
            && airplaneSolidGranted
            && airplaneFreightGranted
            && airplaneExperienceGranted
            && airplaneSentMessageVisible;
        bool airplaneSecondSendBlocked = !island.SendCapitalAirplaneForTests()
            && airplaneSentState != null
            && airplaneSentState.sent
            && runtimeMeta.GetCapitalAirplaneState(1).planeId == airplaneIdBefore;
        if (runtimeMeta != null)
        {
            runtimeMeta.FastForwardSimulation(TimeSpan.FromSeconds(Math.Max(1, airplaneRemainingBefore + 60)));
        }

        CapitalAirplaneState airplaneNextState = runtimeMeta != null ? runtimeMeta.GetCapitalAirplaneState(1) : null;
        bool airplaneRerollsDaily = airplaneNextState != null
            && airplaneNextState.HasPlane
            && !airplaneNextState.sent
            && airplaneNextState.planeId != airplaneIdBefore
            && runtimeMeta.GetCapitalAirplaneRemainingSeconds(1) > MetaGameState.CapitalAirplaneCycleSeconds - 300;
        island.CloseWindowForTests();
        report.Check(airplaneWindowOpened && airplaneEconomy && airplaneSecondSendBlocked && airplaneRerollsDaily,
            "Capital airplane window opens from the airdock; full send spends the requested goods, grants Solid/Freight/mastery, blocks repeat sending, then rerolls only after the daily cycle."
            + " Parts: window=" + airplaneWindowOpened
            + ", economy=" + airplaneEconomy
            + " (sent=" + airplaneSent
            + ", stateSent=" + airplaneStateSent
            + ", samePlane=" + airplaneSamePlane
            + ", remainingFresh=" + airplaneRemainingFresh
            + ", inputs=" + airplaneInputsSpent
            + ", solid=" + airplaneSolidGranted
            + ", freight=" + airplaneFreightGranted
            + ", mastery=" + airplaneExperienceGranted
            + ", message=" + airplaneSentMessageVisible + ")"
            + ", secondBlocked=" + airplaneSecondSendBlocked
            + ", dailyReroll=" + airplaneRerollsDaily + ".");

        bool repairDockSeeded = island.HasBuildingDefinitionForTests("repair_dock")
            && island.GetPlacedBuildingCountForTests("repair_dock") == 1
            && island.GetBuildingDefinitionMaxCountForTests("repair_dock") == 2;
        report.Check(repairDockSeeded,
            "Repair Dock is seeded as a real external dock building with two configured repair slots.");

        RepairDockSlotState repairSlotA = runtimeMeta != null ? runtimeMeta.GetRepairDockSlot(0, 1) : null;
        RepairDockSlotState repairSlotB = runtimeMeta != null ? runtimeMeta.GetRepairDockSlot(1, 1) : null;
        bool repairSlotsReady = repairSlotA != null
            && repairSlotB != null
            && repairSlotA.HasWreck
            && repairSlotB.HasWreck
            && repairSlotA.shipRank >= 2
            && repairSlotA.repairCostFe > 0
            && repairSlotA.sellRewardFreight > 0
            && repairSlotA.inputs != null
            && repairSlotA.inputs.Count >= 3;
        report.Check(repairSlotsReady,
            "Repair Dock initializes persistent random damaged ships with rank, repair FE budget, sale reward and concrete resource inputs.");

        PortStorageState repairStorage = runtimeMeta != null ? runtimeMeta.GetCapitalStorageState() : null;
        DockedDevelopmentShipState occupiedRepairClaimSlot = runtimeMeta != null ? runtimeMeta.GetSelectedDevelopmentDockShipSlot() : null;
        if (runtimeMeta != null && occupiedRepairClaimSlot != null && occupiedRepairClaimSlot.HasShip)
        {
            runtimeMeta.TrySellDevelopmentDockShip(occupiedRepairClaimSlot.slotIndex, out _);
        }

        bool repairDockWindowOpened = island.OpenRepairDockWindowForTests()
            && island.IsRepairDockWindowOpenForTests
            && island.IsWindowModalForTests
            && island.WindowContentForTests.Contains("FE");
        bool repairedForClaim = TopUpAndCompleteRepairDockSlotForTest(runtimeMeta, island, repairStorage, 0, 1, out string claimedRepairShipId, out int claimRepairSteps);
        bool claimedRepairedShip = repairedForClaim && island.ClaimSelectedRepairDockShipForTests();
        DockedDevelopmentShipState claimedDockSlot = runtimeMeta != null ? runtimeMeta.GetSelectedDevelopmentDockShipSlot() : null;
        bool repairClaimWorks = claimedRepairedShip
            && claimedDockSlot != null
            && claimedDockSlot.HasShip
            && claimedDockSlot.shipId == claimedRepairShipId
            && claimedDockSlot.sortiesRemaining == MetaGameState.DevelopmentDockShipMaxSorties;
        if (runtimeMeta != null && claimedDockSlot != null && claimedDockSlot.HasShip)
        {
            runtimeMeta.TrySellDevelopmentDockShip(claimedDockSlot.slotIndex, out _);
        }

        int repairFreightBeforeSell = repairStorage != null ? repairStorage.GetResourceAmount("freight") : 0;
        bool repairedForSale = TopUpAndCompleteRepairDockSlotForTest(runtimeMeta, island, repairStorage, 1, 1, out string soldRepairShipId, out int sellRepairSteps);
        RepairDockSlotState saleSlotBeforeSell = runtimeMeta != null ? runtimeMeta.GetRepairDockSlot(1, 1) : null;
        int repairSellReward = saleSlotBeforeSell != null ? saleSlotBeforeSell.sellRewardFreight : 0;
        bool soldRepairedShip = repairedForSale && island.SellSelectedRepairDockShipForTests();
        int repairFreightAfterSell = repairStorage != null ? repairStorage.GetResourceAmount("freight") : 0;
        RepairDockSlotState saleSlotAfterSell = runtimeMeta != null ? runtimeMeta.GetRepairDockSlot(1, 1) : null;
        bool repairSellWorks = soldRepairedShip
            && repairSellReward > 0
            && repairFreightAfterSell == repairFreightBeforeSell + repairSellReward
            && saleSlotAfterSell != null
            && saleSlotAfterSell.HasWreck
            && saleSlotAfterSell.shipId != soldRepairShipId;
        island.CloseWindowForTests();
        report.Check(repairDockWindowOpened && repairClaimWorks && repairSellWorks,
            "Repair Dock window opens from the external dock; a damaged ship can be restored from resource work steps, claimed into the port slot, or sold for Freight."
            + " Parts: window=" + repairDockWindowOpened
            + ", claim=" + repairClaimWorks
            + " (" + claimedRepairShipId + ", steps=" + claimRepairSteps + ")"
            + ", sell=" + repairSellWorks
            + " (" + soldRepairShipId + ", steps=" + sellRepairSteps + ", freight=" + repairSellReward + ").");

        int westMidCost = island.GetExpansionRegionClearCostForTests("west_mid");
        if (runtimeMeta != null && runtimeMeta.progress != null)
        {
            BaseIslandExpansionRegionState westMidState = runtimeMeta.progress.GetBaseIslandExpansionRegionState("west_mid", true);
            westMidState.status = BaseIslandExpansionRegionStatus.Debris;
            westMidState.clearingCompleteUtcTicks = 0;
        }

        PortStorageState expansionStorage = runtimeMeta != null ? runtimeMeta.GetCapitalStorageState() : null;
        if (expansionStorage != null && expansionStorage.GetResourceAmount("freight") < westMidCost)
        {
            expansionStorage.AddResource("freight", westMidCost);
        }

        int expansionFreightBefore = expansionStorage != null ? expansionStorage.GetResourceAmount("freight") : 0;
        bool expansionWindowOpened = island.OpenExpansionRegionForTests("west_mid")
            && island.IsExpansionWindowOpenForTests
            && island.IsWindowModalForTests
            && island.WindowContentForTests.Contains("Стоимость")
            && island.WindowContentForTests.Contains("фрахта");
        bool expansionStarted = island.BeginExpansionClearingForTests("west_mid");
        int expansionFreightAfterStart = expansionStorage != null ? expansionStorage.GetResourceAmount("freight") : 0;
        bool expansionCompleted = island.ForceCompleteExpansionClearingForTests("west_mid")
            && island.CityOpenCellCountForTests >= (400 + 10 * 10);
        island.CloseWindowForTests();
        report.Check(expansionWindowOpened
            && expansionStarted
            && expansionCompleted
            && westMidCost > 0
            && expansionFreightAfterStart == expansionFreightBefore - westMidCost,
            "Base island expansion chunks open a modal debris-clearing window, spend freight, run a timer state and become buildable land.");

        string progressBefore = runtimeMeta != null && runtimeMeta.progress != null
            ? JsonUtility.ToJson(runtimeMeta.progress)
            : "";

        WildWindGameplayHud cityHud = FindFirstObjectByType<WildWindGameplayHud>();
        bool hoverShown = island.ShowBuildingHoverForTests("refinery")
            && island.IsBuildingHoverLabelVisibleForTests
            && island.BuildingInfoNameForTests.Contains("Рефайнери")
            && island.BuildingInfoLevelForTests == "5 уровень";
        report.Check(hoverShown,
            "Hovering a base building shows its name and level label above the building.");

        bool selected = island.SelectBuildingForTests("refinery")
            && island.IsBuildingActionMenuOpenForTests
            && island.VisibleBuildingActionButtonCountForTests == 3
            && island.BuildingInfoNameForTests.Contains("Рефайнери")
            && island.BuildingInfoLevelForTests == "5 уровень"
            && (cityHud == null || cityHud.IsBaseBuildingFocusHidingPortHudForTests);
        report.Check(selected,
            "Clicking a base building hides the port HUD and shows the building label plus three action buttons.");

        bool dismissed = island.DismissBuildingActionMenuForTests()
            && !island.IsBuildingActionMenuOpenForTests
            && !island.IsWindowOpenForTests
            && (cityHud == null || cityHud.IsPortHudVisibleForTests);
        report.Check(dismissed,
            "Clicking outside the building action buttons restores the normal port HUD.");
        string progressAfterPassiveSelection = runtimeMeta != null && runtimeMeta.progress != null
            ? JsonUtility.ToJson(runtimeMeta.progress)
            : "";
        report.Check(progressBefore == progressAfterPassiveSelection,
            "Hovering, selecting, and dismissing a base building does not mutate PlayerProgress.");

        bool allActionWindowsOpen = true;
        bool secondWindowBlocked = false;
        for (int actionIndex = 0; actionIndex < 3; actionIndex++)
        {
            bool expectsProcessingWindow = actionIndex == 1;
            bool actionOpened = island.SelectBuildingForTests("refinery")
                && island.OpenBuildingActionForTests(actionIndex)
                && island.IsWindowOpenForTests
                && island.IsWindowModalForTests
                && island.OpenWindowTitleForTests.Contains("Рефайнери")
                && island.WindowExitButtonLabelForTests == "Выйти"
                && (expectsProcessingWindow
                    ? island.IsProcessingWindowOpenForTests
                        && island.WindowContentForTests.Contains("Бункер")
                        && island.WindowContentForTests.Contains("Выходы")
                    : string.IsNullOrEmpty(island.WindowContentForTests));

            if (actionIndex == 0)
            {
                secondWindowBlocked = !island.TryOpenBuildingForTests("workshop")
                    && island.IsWindowOpenForTests
                    && island.OpenWindowTitleForTests.Contains("Рефайнери");
            }

            bool exited = island.ExitBuildingActionWindowForTests()
                && !island.IsWindowOpenForTests
                && !island.IsBuildingActionMenuOpenForTests
                && (cityHud == null || cityHud.IsPortHudVisibleForTests);
            allActionWindowsOpen = allActionWindowsOpen && actionOpened && exited;
        }

        report.Check(allActionWindowsOpen,
            "Base building action buttons open modal windows; processing buildings expose the bunker/output panel and Exit restores the initial HUD state.");
        report.Check(secondWindowBlocked,
            "A base building action modal blocks selecting another building until the current window is closed.");

        bool directWorkWindowOpened = island.TryOpenBuildingForTests("refinery")
            && island.IsWindowOpenForTests
            && island.IsWindowModalForTests
            && island.IsProcessingWindowOpenForTests
            && !island.IsBuildingActionMenuOpenForTests
            && (cityHud == null || !cityHud.IsBaseBuildingFocusHidingPortHudForTests);
        bool directWorkWindowExited = island.ExitBuildingActionWindowForTests()
            && !island.IsWindowOpenForTests
            && !island.IsBuildingActionMenuOpenForTests
            && (cityHud == null || cityHud.IsPortHudVisibleForTests);
        report.Check(directWorkWindowOpened && directWorkWindowExited,
            "Runtime building click opens the building work window directly without the intermediate action menu.");

        bool cascadeCatalogOpened = island.OpenCascadeCatalogWindowForTests("workshop")
            && island.IsCascadeCatalogWindowOpenForTests
            && island.IsWindowModalForTests
            && island.OpenWindowTitleForTests.Contains("Каталог")
            && island.WindowExitButtonLabelForTests == "Выйти"
            && island.WindowContentForTests.Contains("каталог рецептов")
            && island.WindowContentForTests.Contains("Состав")
            && island.WindowContentForTests.Contains("Топ-3")
            && (island.WindowContentForTests.Contains("Получим") || island.WindowContentForTests.Contains("+1 уровень"));
        bool cascadeCatalogExited = island.ExitBuildingActionWindowForTests()
            && !island.IsWindowOpenForTests
            && !island.IsBuildingActionMenuOpenForTests
            && (cityHud == null || cityHud.IsPortHudVisibleForTests);
        report.Check(cascadeCatalogOpened && cascadeCatalogExited,
            "Cascade production buildings open a modal recipe catalog with rubrics, recipe plan, top bottlenecks and result preview.");

        bool refineryProcessingFacilityMaterialized = runtimeMeta != null
            && runtimeMeta.progress != null
            && runtimeMeta.progress.baseIndustry != null
            && runtimeMeta.progress.baseIndustry.processingFacilities.Exists(facility =>
                facility != null
                && facility.facilityId == "refinery"
                && facility.branch == BaseProcessingBranch.Ore);
        report.Check(refineryProcessingFacilityMaterialized,
            "Opening a refinery work action materializes an ore processing facility state for that building.");

        PortStorageState bubbleStorage = runtimeMeta != null ? runtimeMeta.GetCapitalStorageState() : null;
        BaseProcessingFacilityState refineryFacility = runtimeMeta != null
            ? runtimeMeta.GetBaseProcessingFacilityState("refinery", BaseProcessingBranch.Ore, 5)
            : null;
        BaseProcessingOutputBufferState bubbleIronBuffer = refineryFacility != null
            ? refineryFacility.GetOutputBuffer("iron", true)
            : null;
        BaseProcessingOutputBufferState bubbleCalciteBuffer = refineryFacility != null
            ? refineryFacility.GetOutputBuffer("calcite", true)
            : null;
        int ironBeforeBubbleCollect = bubbleStorage != null ? bubbleStorage.GetResourceAmount("iron") : 0;
        int calciteBeforeBubbleCollect = bubbleStorage != null ? bubbleStorage.GetResourceAmount("calcite") : 0;
        if (bubbleIronBuffer != null)
        {
            bubbleIronBuffer.readyAmount = 7;
            bubbleIronBuffer.fractionalAmount = 0.42f;
        }

        if (bubbleCalciteBuffer != null)
        {
            bubbleCalciteBuffer.readyAmount = 3;
            bubbleCalciteBuffer.fractionalAmount = 0.25f;
        }

        bool processingCollectBubbleVisible = island.IsProcessingCollectBubbleVisibleForTests("refinery")
            && island.ProcessingCollectBubbleItemIdForTests("refinery") == "iron"
            && island.ProcessingCollectBubbleAmountForTests("refinery") == 10
            && island.ProcessingCollectBubbleCountForTests >= 1;
        bool processingCollectBubbleClicked = island.ClickProcessingCollectBubbleForTests("refinery");
        bool processingCollectBubbleCollected = processingCollectBubbleClicked
            && bubbleStorage != null
            && bubbleIronBuffer != null
            && bubbleCalciteBuffer != null
            && bubbleStorage.GetResourceAmount("iron") == ironBeforeBubbleCollect + 7
            && bubbleStorage.GetResourceAmount("calcite") == calciteBeforeBubbleCollect + 3
            && bubbleIronBuffer.readyAmount == 0
            && bubbleCalciteBuffer.readyAmount == 0
            && bubbleIronBuffer.fractionalAmount > 0f
            && !island.IsProcessingCollectBubbleVisibleForTests("refinery");
        report.Check(processingCollectBubbleVisible && processingCollectBubbleCollected,
            "Ready processing outputs show a clickable city collect bubble with the largest resource icon and total amount; clicking it collects all whole outputs.");

        ValidateQuestRuntime(runtimeMeta, report);
    }

    private static void ValidateQuestRuntime(MetaGameState runtimeMeta, BigTestReport report)
    {
        report.Section("Quest runtime");
        if (runtimeMeta == null)
        {
            report.Fail("Quest runtime cannot run without MetaGameState.");
            return;
        }

        PlayerProgress snapshot = runtimeMeta.CreateProgressSnapshot();
        try
        {
            runtimeMeta.ReplaceProgress(new PlayerProgress());
            runtimeMeta.EnsureProgressInitialized();

            IReadOnlyList<QuestDefinitionConfig> definitions = runtimeMeta.GetQuestDefinitions();
            IReadOnlyList<QuestState> initialStates = runtimeMeta.GetQuestStates();
            bool initialBoardReady = definitions != null
                && definitions.Count >= 20
                && initialStates != null
                && IsQuestClaimedForBigTest(runtimeMeta, "quest_have_start_freight")
                && IsQuestClaimedForBigTest(runtimeMeta, "quest_collect_claudium")
                && IsQuestAcceptedForBigTest(runtimeMeta, "quest_spend_first_freight");

            runtimeMeta.RecordQuestEventForTests("resource_spent", "freight", 99);
            bool fromAcceptBeforeComplete = IsQuestCurrentForBigTest(runtimeMeta, "quest_spend_first_freight", 99, false, false);
            runtimeMeta.RecordQuestEventForTests("resource_spent", "freight", 1);
            bool fromAcceptCompletesAtDelta = IsQuestCurrentForBigTest(runtimeMeta, "quest_spend_first_freight", 100, true, false);
            bool manualSpendClaimed = ClaimQuestForBigTest(runtimeMeta, "quest_spend_first_freight");

            runtimeMeta.RecordQuestEventForTests("resource_acquired", "windshale_ore", 50);
            bool windshaleCollected = IsQuestCompletedForBigTest(runtimeMeta, "quest_collect_windshale")
                && ClaimQuestForBigTest(runtimeMeta, "quest_collect_windshale");
            runtimeMeta.RecordQuestEventForTests("resource_spent", "windshale_ore", 20);
            bool windshaleSpendAutoClaimed = IsQuestClaimedForBigTest(runtimeMeta, "quest_spend_windshale");

            runtimeMeta.RecordQuestEventForTests("courier_sent", "any", 1);
            bool firstCourierCompleted = IsQuestCompletedForBigTest(runtimeMeta, "quest_send_first_courier")
                && ClaimQuestForBigTest(runtimeMeta, "quest_send_first_courier");
            runtimeMeta.RecordQuestEventForTests("courier_sent", "any", 2);
            bool threeCouriersCompleted = IsQuestCompletedForBigTest(runtimeMeta, "quest_send_three_couriers")
                && ClaimQuestForBigTest(runtimeMeta, "quest_send_three_couriers");
            runtimeMeta.RecordQuestEventForTests("courier_cancelled", "any", 1);
            bool courierCancelCompleted = IsQuestCompletedForBigTest(runtimeMeta, "quest_cancel_courier")
                && ClaimQuestForBigTest(runtimeMeta, "quest_cancel_courier");

            runtimeMeta.RecordQuestEventForTests("technology_started", "tech_base_storehouse_ledgers", 1);
            bool knowledgeStartAutoClaimed = IsQuestClaimedForBigTest(runtimeMeta, "quest_start_knowledge");
            runtimeMeta.progress.CompleteTechnology("tech_base_storehouse_ledgers");
            runtimeMeta.RecordQuestEventForTests("technology_completed", "tech_base_storehouse_ledgers", 1);
            bool knowledgeCompleteClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_complete_storehouse")
                && ClaimQuestForBigTest(runtimeMeta, "quest_complete_storehouse");

            runtimeMeta.progress.baseIndustry ??= new BaseExtractionIndustryState();
            runtimeMeta.progress.baseIndustry.Normalize();
            runtimeMeta.progress.baseIndustry.GetProcessing(BaseProcessingBranch.Ore).level = 2;
            runtimeMeta.progress.baseIndustry.GetProcessing(BaseProcessingBranch.Gas).level = 2;
            runtimeMeta.progress.baseIndustry.GetProduction(CascadeProductionType.Assembly).level = 2;
            runtimeMeta.RefreshQuestProgressForTests(true);
            bool processingLevelClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_ore_processing_l2")
                && ClaimQuestForBigTest(runtimeMeta, "quest_ore_processing_l2")
                && IsQuestClaimedForBigTest(runtimeMeta, "quest_gas_processing_l2")
                && IsQuestCompletedForBigTest(runtimeMeta, "quest_cascade_assembly_l2")
                && ClaimQuestForBigTest(runtimeMeta, "quest_cascade_assembly_l2");

            runtimeMeta.RecordQuestEventForTests("cascade_order_completed", SessionExtractionConstants.StarterAirframeKitItemId, 1);
            bool airframeOrderClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_airframe_order")
                && ClaimQuestForBigTest(runtimeMeta, "quest_airframe_order");
            runtimeMeta.RecordQuestEventForTests("cascade_order_completed", SessionExtractionConstants.StarterModuleKitItemId, 1);
            bool moduleOrderAutoClaimed = IsQuestClaimedForBigTest(runtimeMeta, "quest_module_order");

            runtimeMeta.RecordQuestEventForTests("sortie_completed", SessionExtractionConstants.DefaultSafeOreSortieId, 1);
            bool sortieQuestClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_safe_ore_sortie")
                && ClaimQuestForBigTest(runtimeMeta, "quest_safe_ore_sortie");

            runtimeMeta.progress.InstallModule("quest_test_high", SessionExtractionConstants.StarterGasExtractorModuleId);
            runtimeMeta.RefreshQuestProgressForTests(true);
            bool gasModuleClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_install_gas_extractor")
                && ClaimQuestForBigTest(runtimeMeta, "quest_install_gas_extractor");
            runtimeMeta.progress.InstallModule("quest_test_low", SessionExtractionConstants.StarterMiningHoldModuleId);
            runtimeMeta.RefreshQuestProgressForTests(true);
            bool miningModuleClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_install_mining_hold")
                && ClaimQuestForBigTest(runtimeMeta, "quest_install_mining_hold");

            runtimeMeta.RecordQuestEventForTests("resource_acquired", SessionExtractionConstants.DesignExperienceItemId, 10);
            bool manualRewardClaimed = IsQuestCompletedForBigTest(runtimeMeta, "quest_claim_manual_reward")
                && ClaimQuestForBigTest(runtimeMeta, "quest_claim_manual_reward");

            IReadOnlyList<FactionDefinitionView> factionDefinitions = runtimeMeta.GetFactionDefinitions();
            bool factionDefinitionsReady = factionDefinitions != null
                && factionDefinitions.Count == 6
                && runtimeMeta.GetFactionReputationRequiredForLevel(1) == 100
                && runtimeMeta.GetFactionReputationRequiredForLevel(5) == 50000
                && runtimeMeta.GetFactionReputationLevel("stone_vault") == 0;
            bool oreGateBlockedInitially = !runtimeMeta.CanPassFactionGateForUpgrade("processing:ore", 5, out string oreGateInitialMessage)
                && !string.IsNullOrWhiteSpace(oreGateInitialMessage);
            bool oreGateBelowThresholdOpen = runtimeMeta.CanPassFactionGateForUpgrade("processing:ore", 4, out _);
            IReadOnlyList<FactionMarketItemOffer> stoneMarketBefore = runtimeMeta.GetFactionMarketOffers("stone_vault");
            FactionMarketItemOffer lockedGrateBefore = FindFactionMarketOfferForBigTest(stoneMarketBefore, "stone_throat_grate");
            bool factionMarketStartsLocked = lockedGrateBefore != null
                && !lockedGrateBefore.unlocked
                && string.Equals(lockedGrateBefore.currencyItemId, "gems", StringComparison.OrdinalIgnoreCase);

            IReadOnlyList<FactionDailyTaskOffer> stoneDaily = runtimeMeta.GetFactionDailyTasks("stone_vault");
            bool stoneDailyReady = stoneDaily != null && stoneDaily.Count == 5;
            FactionDailyTaskOffer daily = stoneDailyReady ? stoneDaily[0] : null;
            PortStorageState factionStorage = runtimeMeta.GetCapitalStorageState();
            int dailyInputBefore = 0;
            int dailyCurrencyBefore = 0;
            int dailyMasteryBefore = 0;
            int dailyRepBefore = runtimeMeta.GetFactionReputationPoints("stone_vault");
            if (daily != null && factionStorage != null)
            {
                int available = factionStorage.GetResourceAmount(daily.inputItemId);
                if (available < daily.inputAmount)
                {
                    factionStorage.AddResource(daily.inputItemId, daily.inputAmount - available);
                }

                dailyInputBefore = factionStorage.GetResourceAmount(daily.inputItemId);
                dailyCurrencyBefore = factionStorage.GetResourceAmount(daily.rewardCurrencyItemId);
                dailyMasteryBefore = factionStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId);
            }

            bool dailyCompleted = daily != null && runtimeMeta.TryCompleteFactionDailyTask(daily.taskId, out _);
            bool dailySecondBlocked = daily != null && !runtimeMeta.TryCompleteFactionDailyTask(daily.taskId, out _);
            IReadOnlyList<FactionDailyTaskOffer> stoneDailyAfter = runtimeMeta.GetFactionDailyTasks("stone_vault");
            bool dailyEconomyValid = dailyCompleted
                && dailySecondBlocked
                && factionStorage != null
                && daily != null
                && factionStorage.GetResourceAmount(daily.inputItemId) == dailyInputBefore - daily.inputAmount
                && factionStorage.GetResourceAmount(daily.rewardCurrencyItemId) == dailyCurrencyBefore + daily.rewardCurrencyAmount
                && factionStorage.GetResourceAmount(SessionExtractionConstants.DesignExperienceItemId) == dailyMasteryBefore + daily.masteryReward
                && runtimeMeta.GetFactionReputationPoints("stone_vault") == dailyRepBefore + daily.reputationReward
                && runtimeMeta.GetFactionReputationLevel("stone_vault") == 0
                && stoneDailyAfter != null
                && stoneDailyAfter.Count == 5
                && stoneDailyAfter[0].completed;

            runtimeMeta.RecordQuestEventForTests("faction_reputation", "stone_vault", 50000);
            runtimeMeta.RefreshQuestProgressForTests(true);
            bool stoneCampaignPrefixClaimed = ClaimFactionCampaignPrefixForBigTest(runtimeMeta, "stone_vault", 10);
            IReadOnlyList<FactionMarketItemOffer> stoneMarketAfter = runtimeMeta.GetFactionMarketOffers("stone_vault");
            FactionMarketItemOffer unlockedGrateAfter = FindFactionMarketOfferForBigTest(stoneMarketAfter, "stone_throat_grate");
            int grateBeforeBuy = factionStorage != null ? factionStorage.GetResourceAmount("stone_throat_grate") : 0;
            int gemsBeforeBuy = factionStorage != null ? factionStorage.GetResourceAmount("gems") : 0;
            if (factionStorage != null && unlockedGrateAfter != null && gemsBeforeBuy < unlockedGrateAfter.priceAmount)
            {
                factionStorage.AddResource("gems", unlockedGrateAfter.priceAmount - gemsBeforeBuy);
                gemsBeforeBuy = factionStorage.GetResourceAmount("gems");
            }

            bool boughtFactionComponent = runtimeMeta.TryBuyFactionMarketItem("stone_vault", "stone_throat_grate", 1, out _);
            bool factionMarketPurchaseValid = boughtFactionComponent
                && factionStorage != null
                && unlockedGrateAfter != null
                && unlockedGrateAfter.unlocked
                && factionStorage.GetResourceAmount("stone_throat_grate") == grateBeforeBuy + 1
                && factionStorage.GetResourceAmount("gems") == gemsBeforeBuy - unlockedGrateAfter.priceAmount;
            bool oreGateOpenAfterStoneRep = runtimeMeta.CanPassFactionGateForUpgrade("processing:ore", 5, out _);
            bool gasGateStillBlocked = !runtimeMeta.CanPassFactionGateForUpgrade("processing:gas", 5, out _);

            int completedCount = runtimeMeta.GetQuestCompletedCountForTests();
            int claimedCount = runtimeMeta.GetQuestClaimedCountForTests();
            report.Check(initialBoardReady
                    && completedCount >= 19
                    && claimedCount >= 19,
                "Quest runtime accepts the starter task board, retroactively completes owned-resource tasks and can finish every seed quest after ship-roster cleanup: completed="
                + completedCount + ", claimed=" + claimedCount + ".");
            report.Check(fromAcceptBeforeComplete
                    && fromAcceptCompletesAtDelta
                    && manualSpendClaimed
                    && windshaleCollected
                    && windshaleSpendAutoClaimed
                    && firstCourierCompleted
                    && threeCouriersCompleted
                    && courierCancelCompleted
                    && knowledgeStartAutoClaimed
                    && knowledgeCompleteClaimed
                    && processingLevelClaimed
                    && airframeOrderClaimed
                    && moduleOrderAutoClaimed
                    && sortieQuestClaimed
                    && gasModuleClaimed
                    && miningModuleClaimed
                    && manualRewardClaimed,
                "Quest runtime covers from-accept deltas, manual claims, auto claims, couriers, knowledge, production, sortie and fitting events.");
            report.Check(factionDefinitionsReady
                    && oreGateBlockedInitially
                    && oreGateBelowThresholdOpen
                    && factionMarketStartsLocked,
                "Faction runtime exposes six factions, five reputation thresholds, locked market offers and building gates.");
            report.Check(stoneDailyReady && dailyEconomyValid,
                "Faction daily tasks generate five deterministic jobs per faction and completing one spends cargo, pays faction currency, mastery and +50 reputation once.");
            report.Check(stoneCampaignPrefixClaimed
                    && factionMarketPurchaseValid
                    && oreGateOpenAfterStoneRep
                    && gasGateStillBlocked,
                "Faction campaign reputation unlocks component purchases and opens only the matching faction building gate.");
        }
        finally
        {
            runtimeMeta.ReplaceProgress(snapshot);
        }
    }

    private static bool IsQuestAcceptedForBigTest(MetaGameState meta, string questId)
    {
        QuestState state = meta != null ? meta.GetQuestState(questId) : null;
        return state != null && state.accepted;
    }

    private static bool IsQuestCompletedForBigTest(MetaGameState meta, string questId)
    {
        QuestState state = meta != null ? meta.GetQuestState(questId) : null;
        return state != null && state.completed;
    }

    private static bool IsQuestClaimedForBigTest(MetaGameState meta, string questId)
    {
        QuestState state = meta != null ? meta.GetQuestState(questId) : null;
        return state != null && state.claimed;
    }

    private static bool IsQuestCurrentForBigTest(MetaGameState meta, string questId, int currentAmount, bool completed, bool claimed)
    {
        QuestState state = meta != null ? meta.GetQuestState(questId) : null;
        return state != null
            && state.currentAmount == currentAmount
            && state.completed == completed
            && state.claimed == claimed;
    }

    private static bool ClaimQuestForBigTest(MetaGameState meta, string questId)
    {
        QuestState state = meta != null ? meta.GetQuestState(questId) : null;
        if (state == null || !state.completed)
        {
            return false;
        }

        if (state.claimed)
        {
            return true;
        }

        return meta.TryClaimQuestReward(questId, out _);
    }

    private static bool ClaimFactionCampaignPrefixForBigTest(MetaGameState meta, string factionId, int count)
    {
        if (meta == null || string.IsNullOrWhiteSpace(factionId) || count <= 0)
        {
            return false;
        }

        for (int i = 1; i <= count; i++)
        {
            string questId = "fquest_" + factionId + "_" + i.ToString("00");
            meta.RefreshQuestProgressForTests(true);
            if (!ClaimQuestForBigTest(meta, questId))
            {
                return false;
            }
        }

        meta.RefreshQuestProgressForTests(true);
        return true;
    }

    private static FactionMarketItemOffer FindFactionMarketOfferForBigTest(IReadOnlyList<FactionMarketItemOffer> offers, string itemId)
    {
        if (offers == null || string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        for (int i = 0; i < offers.Count; i++)
        {
            FactionMarketItemOffer offer = offers[i];
            if (offer != null && string.Equals(offer.itemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                return offer;
            }
        }

        return null;
    }

    private static bool IsKnownFactionIdForBigTest(string factionId, IReadOnlyList<string> knownFactionIds)
    {
        if (string.IsNullOrWhiteSpace(factionId) || knownFactionIds == null)
        {
            return false;
        }

        for (int i = 0; i < knownFactionIds.Count; i++)
        {
            if (string.Equals(factionId, knownFactionIds[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SessionSceneOmitsLegacyStaticVisualsForTests(string sceneText)
    {
        if (string.IsNullOrWhiteSpace(sceneText))
        {
            return false;
        }

        return !sceneText.Contains("m_Name: Session Data - Sortie Runtime")
            && !sceneText.Contains("Port -")
            && !sceneText.Contains("Resource Field -")
            && !sceneText.Contains("Capital Port Proxy - Greenhaven")
            && !sceneText.Contains("Data Resource Field ore_field_tutorial_00")
            && !sceneText.Contains("Port Rock")
            && !sceneText.Contains("Resource Marker")
            && !sceneText.Contains("Warm Dock Lamp");
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
        report.Section("Direct port entry and persistent progress reset");

        try
        {
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
            becamePersistentForSceneLoop = true;

            sessionLoopLaunchInProgress = true;
            bool enteredPort = WildWindSessionFlow.TryEnterPort(DefaultSessionSceneName, out string enterError);
            report.Check(enteredPort, enteredPort
                ? "SessionFlow enters the gameplay port scene directly."
                : "SessionFlow could not enter the gameplay port scene: " + enterError);
            if (!enteredPort)
            {
                yield break;
            }

            DisableDuplicateBigTestRunners();
            yield return WaitForLoadedSession();
            ReportSessionReadinessIfNeeded("direct port entry", report);

            Scene firstSessionScene = SceneManager.GetActiveScene();
            report.Check(firstSessionScene.name == DefaultSessionSceneName, "Direct entry loaded the session scene: " + firstSessionScene.name + ".");
            ValidateLoadedSession("direct port entry", report);

            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }

            WildWindGameplayHud firstHud = FindFirstObjectByType<WildWindGameplayHud>();
            WildWindBaseIslandView firstIsland = WildWindBaseIslandView.EnsureForCurrentSessionScene();
            report.Check(firstHud != null
                    && firstIsland != null
                    && !firstHud.IsKnowledgeScreenVisibleForTests
                    && firstIsland.IsCityVisibleForTests,
                "Docked direct entry starts on the city map without opening a full-screen knowledge overlay.");

            ValidateSessionExtractionCoreLoop(report);

            MetaGameState firstMeta = FindFirstObjectByType<MetaGameState>();
            WildWindGameplayMenu gameplayMenu = FindFirstObjectByType<WildWindGameplayMenu>();
            report.Check(gameplayMenu != null, gameplayMenu != null ? "Gameplay menu is present in the direct port scene." : "Gameplay menu is missing in the direct port scene.");
            if (firstMeta == null || gameplayMenu == null || firstMeta.progress == null)
            {
                yield break;
            }

            firstMeta.ResetAccountProgressForCheat(out _);
            const string runtimeProbeResourceId = "runtime_account_probe_resource";
            PortStorageState firstRuntimeStorage = firstMeta.GetCapitalStorageState();
            int runtimeProbeAmount = firstRuntimeStorage.GetResourceAmount(runtimeProbeResourceId) + 12345;
            firstRuntimeStorage.SetResourceAmount(runtimeProbeResourceId, runtimeProbeAmount);
            MetaGameAccountData runtimeSnapshot = firstMeta.CreateRuntimeAccountData();
            PortStorageState runtimeSnapshotStorage = runtimeSnapshot != null && runtimeSnapshot.progress != null
                ? runtimeSnapshot.progress.GetPortStorageState(firstMeta.GetCapitalPortId(), false)
                : null;
            report.Check(runtimeSnapshotStorage != null && runtimeSnapshotStorage.GetResourceAmount(runtimeProbeResourceId) == runtimeProbeAmount,
                "Runtime account snapshot reflects current in-memory progress.");
            bool probeSaved = firstMeta.SavePersistentProgressNowForTests();
            report.Check(probeSaved && firstMeta.HasPersistentProgressSaveForTests,
                "Runtime progress save writes current account progress to the persistent progress file.");

            SceneManager.LoadScene(DefaultSessionSceneName);
            DisableDuplicateBigTestRunners();
            yield return WaitForLoadedSession();
            ReportSessionReadinessIfNeeded("persistent runtime scene reload", report);
            ValidateLoadedSession("persistent runtime scene reload", report);

            MetaGameState reloadedMeta = FindFirstObjectByType<MetaGameState>();
            PortStorageState reloadedRuntimeStorage = reloadedMeta != null ? reloadedMeta.GetCapitalStorageState() : null;
            report.Check(reloadedMeta != null
                && reloadedMeta.progress != null
                && reloadedMeta.PersistentProgressLoadedThisSessionForTests
                && reloadedRuntimeStorage != null
                && reloadedRuntimeStorage.GetResourceAmount(runtimeProbeResourceId) == runtimeProbeAmount,
                "Direct scene reload restores persisted runtime progress from disk.");

            WildWindGameplayMenu reloadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();
            report.Check(reloadedMenu != null, reloadedMenu != null ? "Gameplay menu is present after persistent runtime reload." : "Gameplay menu is missing after persistent runtime reload.");
            if (reloadedMenu == null)
            {
                yield break;
            }

            reloadedMenu.SetOpen(true);
            yield return null;
            MetaGameState pausedMeta = FindFirstObjectByType<MetaGameState>();
            report.Check(pausedMeta != null && pausedMeta.IsSessionPaused && Approximately(Time.timeScale, 0f, 0.001f),
                "Esc menu pauses the session scene.");
            reloadedMenu.SetOpen(false);
            yield return null;

            WildWindGameplayHud reloadedHud = FindFirstObjectByType<WildWindGameplayHud>();
            bool resetInvoked = reloadedHud != null
                && reloadedHud.OpenMetaTopRightWindowForTests(3)
                && reloadedHud.PressSettingsResetProgressForTests();
            report.Check(resetInvoked, "Settings window can reset persistent runtime progress without exceptions.");
            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }

            MetaGameState resetMeta = FindFirstObjectByType<MetaGameState>();
            WildWindGameplayHud resetHud = FindFirstObjectByType<WildWindGameplayHud>();
            PortStorageState resetStorage = resetMeta != null ? resetMeta.GetCapitalStorageState() : null;
            bool resetProgressFresh = resetMeta != null &&
                resetMeta.progress != null &&
                resetStorage != null &&
                resetStorage.GetResourceAmount(runtimeProbeResourceId) != runtimeProbeAmount &&
                resetMeta.CurrentMode == GameSessionMode.Docked &&
                resetMeta.progress.currentDockId == GameplaySessionAccountData.DefaultDockId;
            report.Check(resetProgressFresh,
                "Settings progress reset returns the current runtime account to fresh docked port progress.");
            WildWindBaseIslandView resetIsland = WildWindBaseIslandView.EnsureForCurrentSessionScene();
            report.Check(resetHud != null
                    && resetIsland != null
                    && !resetHud.IsKnowledgeScreenVisibleForTests
                    && resetIsland.IsCityVisibleForTests,
                "Settings progress reset leaves the player on the city map instead of opening a full-screen overlay.");
        }
        finally
        {
            sessionLoopLaunchInProgress = false;
        }
    }
    private static IEnumerator WaitForActiveScene(string sceneName)
    {
        for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != sceneName; i++)
        {
            yield return null;
        }
    }

    private IEnumerator WaitForLoadedSession()
    {
        for (int i = 0; i < 120 && !IsLoadedSessionReady(); i++)
        {
            DisableDuplicateBigTestRunners();
            yield return null;
        }
    }

    private static bool IsLoadedSessionReady()
    {
        Scene scene = SceneManager.GetActiveScene();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        return scene.name == DefaultSessionSceneName &&
            loadedMeta != null &&
            loadedMeta.RuntimeAccountId == GameplaySessionAccountData.DefaultAccountId &&
            loadedMenu != null &&
            loadedSession != null &&
            loadedSession.IsReady &&
            loadedSession.AccountId == GameplaySessionAccountData.DefaultAccountId &&
            loadedHud != null &&
            loadedHud.IsReady;
    }

    private static void ReportSessionReadinessIfNeeded(string label, BigTestReport report)
    {
        if (IsLoadedSessionReady())
        {
            return;
        }

        report.Fail("Session did not become ready after waiting (" + label + "): " +
            DescribeLoadedSessionReadiness() + ".");
    }

    private static string DescribeLoadedSessionReadiness()
    {
        Scene scene = SceneManager.GetActiveScene();
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        string accountId = loadedMeta != null ? loadedMeta.RuntimeAccountId : "<no MetaGameState>";
        string menu = loadedMenu != null ? "present" : "missing";
        string session = loadedSession != null ? (loadedSession.IsReady ? "ready" : "not ready") : "none";
        string hud = loadedHud != null ? (loadedHud.IsReady ? "ready" : "not ready") : "none";

        return "scene=" + scene.name +
            ", expectedScene=" + DefaultSessionSceneName +
            ", metaAccount=" + accountId +
            ", expectedAccount=" + GameplaySessionAccountData.DefaultAccountId +
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

        loadedMeta.EnsureProgressInitialized();
        PlayerProgress progress = loadedMeta.progress;
        SessionConfigDatabase config = loadedMeta.SessionConfig;
        string capitalId = loadedMeta.GetCapitalPortId();

        report.Check(progress != null,
            "Session extraction is the only runtime mode and does not persist a core-mode toggle.");
        report.Check(loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.currentDockId == capitalId,
            "New session starts docked at the base: " + progress.currentDockId + ".");
        report.Check(loadedSession == null || loadedSession.CurrentDockId == capitalId,
            "GameplaySession default dock is the base: " + (loadedSession != null ? loadedSession.CurrentDockId : "<missing>") + ".");

        PlayerProgress sessionCoreSnapshot = progress.Clone();
        loadedMeta.ReplaceProgress(new PlayerProgress());
        loadedMeta.EnsureProgressInitialized();
        PlayerProgress freshCoreProgress = loadedMeta.progress;
        PortStorageState freshCoreBaseStorage = loadedMeta.GetCapitalStorageState();
        report.Check(freshCoreProgress != null
            && freshCoreBaseStorage != null
            && freshCoreProgress.GetResourceAmount("ore") == 0
            && freshCoreProgress.GetResourceAmount("iron") == 0
            && freshCoreBaseStorage.GetResourceAmount("paper") == 0,
            "Fresh core progress does not seed legacy personal ore/iron or starting paper.");
        report.Check(freshCoreBaseStorage != null
            && freshCoreBaseStorage.GetResourceAmount("windshale_ore") > 0
            && freshCoreBaseStorage.GetResourceAmount("cloud_condensate") > 0
            && freshCoreBaseStorage.GetResourceAmount(SessionExtractionConstants.StarterAutomatonPartItemId) > 0
            && freshCoreBaseStorage.GetResourceAmount("windcalf_carcass") > 0
            && freshCoreBaseStorage.GetResourceAmount(SessionExtractionConstants.RockInfoItemId) > 0,
            "Fresh base storage seeds temporary sample inputs for each processing window.");
        report.Check(freshCoreBaseStorage != null
            && freshCoreBaseStorage.GetResourceAmount("freight") >= 4000
            && freshCoreBaseStorage.GetResourceAmount("freight") < 10000,
            "Fresh base storage seeds only a small starter Freight purse; early growth must come from sorties, processing and courier orders.");
        report.Check(freshCoreProgress != null
            && freshCoreProgress.receivedStartingCourierSupplies
            && freshCoreBaseStorage != null
            && freshCoreBaseStorage.GetResourceAmount("iron") == 0
            && freshCoreBaseStorage.GetResourceAmount("calcite") == 0
            && freshCoreBaseStorage.GetResourceAmount("tools") >= 2
            && freshCoreBaseStorage.GetResourceAmount("claudium") >= 100
            && freshCoreBaseStorage.GetResourceAmount("paper") == 0,
            "Fresh and existing saves receive only a minimal service starter pack; processed minerals must come from base processing.");
        loadedMeta.ReplaceProgress(sessionCoreSnapshot);
        loadedMeta.EnsureProgressInitialized();
        progress = loadedMeta.progress;
        config = loadedMeta.SessionConfig;
        capitalId = loadedMeta.GetCapitalPortId();

        loadedMeta.RefreshSessionExtractionRuntimeActors();
        report.Check(loadedMeta.SpawnedConfiguredPortCount <= 1,
            "Session runtime keeps actors to the base dock and suppresses legacy free-world gas, mining, and leviathan spawns.");

        string metaSource = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string sessionSource = ReadProjectText("Assets/Scripts/Session/WildWindGameplaySession.cs");
        bool legacyFlightMissionApiRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Data/MissionDefinitionSO.cs")) &&
            !metaSource.Contains("TryBeginFlightSession") &&
            !metaSource.Contains("CompleteFlightMission") &&
            !metaSource.Contains("StartTimedMission") &&
            !metaSource.Contains("BeginFreeFlight") &&
            !sessionSource.Contains("TryBeginFreeFlight");
        report.Check(legacyFlightMissionApiRemoved && loadedMeta.CurrentMode == GameSessionMode.Docked,
            "Legacy free-flight and mission-flight APIs are removed; sorties are the only flight entry point.");

        string playerProgressSource = ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs");
        string projectFileSource = ReadProjectText("Assembly-CSharp.csproj");
        bool legacyMoneyXpRuntimeRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/TechTreeRules.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/TechTreeRules.cs.meta")) &&
            !projectFileSource.Contains("TechTreeRules.cs") &&
            !playerProgressSource.Contains("public int money") &&
            !playerProgressSource.Contains("ShipExperienceWallet") &&
            !playerProgressSource.Contains("GetShipExperience") &&
            !playerProgressSource.Contains("AddShipExperience") &&
            !playerProgressSource.Contains("TrySpendShipExperience") &&
            !playerProgressSource.Contains("researchedNodeIds") &&
            !playerProgressSource.Contains("purchasedNodeIds") &&
            !playerProgressSource.Contains("IsNodeResearched") &&
            !playerProgressSource.Contains("IsNodePurchased") &&
            !playerProgressSource.Contains("ResearchNode") &&
            !playerProgressSource.Contains("PurchaseNode") &&
            playerProgressSource.Contains("completedTechnologyIds") &&
            !metaSource.Contains("startingMoney") &&
            !metaSource.Contains("AddMoney") &&
            !metaSource.Contains("AddResource(string resourceId, int amount)") &&
            !metaSource.Contains("AddExperienceToSelectedShip") &&
            !metaSource.Contains("AddExperienceToShip") &&
            !metaSource.Contains("TryResearchNode") &&
            !metaSource.Contains("TryPurchaseNode");
        report.Check(legacyMoneyXpRuntimeRemoved,
            "Legacy money, ship XP, node progress, direct reward and XP/money tech-tree APIs are removed; unlocks come from base resource technologies and cascade production.");

        bool islandSimulationRuntimeRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/IslandDevelopmentSimulator.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/IslandIndustrySimulator.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/IslandSocietySimulator.cs")) &&
            !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("PassengerTrafficSimulator") &&
            !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("IslandProductionSimulator") &&
            !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("IslandIndustrySimulator") &&
            !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("IslandSocietySimulator");
        report.Check(islandSimulationRuntimeRemoved,
            "Legacy passenger, social-needs, island production and island industry simulation runtimes are removed.");
        bool flagshipRuntimeRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/FlagshipInteriorSimulator.cs")) &&
            !File.Exists(ProjectPath("Assets/Data/Config/Expedition.csv")) &&
            !ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs").Contains("flagshipInteriors") &&
            !ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs").Contains("activeExpedition") &&
            !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("FlagshipInteriorSimulator");
        report.Check(flagshipRuntimeRemoved,
            "Legacy flagship interiors and expeditions are removed instead of being ticked or blocked at runtime.");
        bool autonomousFleetSourcesRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/LogisticsFleetController.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/ScoutFleetController.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/GasHarvesterFleetController.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/MiningFleetController.cs"));
        string metaRuntimeText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        bool metaDoesNotAutoAddLegacyFleets =
            !metaRuntimeText.Contains("LogisticsFleetController") &&
            !metaRuntimeText.Contains("ScoutFleetController") &&
            !metaRuntimeText.Contains("GasHarvesterFleetController") &&
            !metaRuntimeText.Contains("MiningFleetController") &&
            !metaRuntimeText.Contains("EnsureLegacyAutonomousFleetRuntime");
        string sessionSceneText = ReadProjectText("Assets/Scenes/WildWindSessionScene.unity");
        bool sceneDoesNotSerializeLegacyFleets =
            !sessionSceneText.Contains("Assembly-CSharp::LogisticsFleetController") &&
            !sessionSceneText.Contains("Assembly-CSharp::ScoutFleetController") &&
            !sessionSceneText.Contains("Assembly-CSharp::GasHarvesterFleetController") &&
            !sessionSceneText.Contains("Assembly-CSharp::MiningFleetController");
        report.Check(autonomousFleetSourcesRemoved && metaDoesNotAutoAddLegacyFleets && sceneDoesNotSerializeLegacyFleets,
            "Session-only runtime removes legacy autonomous fleet controllers instead of ticking or auto-adding them.");

        bool timedAndCargoRuntimeRemoved =
            !metaRuntimeText.Contains("StartIdleMining") &&
            !metaRuntimeText.Contains("StartIronSmelting") &&
            !metaRuntimeText.Contains("StartTimedMission") &&
            !metaRuntimeText.Contains("TryLoadShipCargoFromCurrentDock") &&
            !metaRuntimeText.Contains("TryUnloadShipCargoToCurrentDock") &&
            !metaRuntimeText.Contains("AdvanceCargoTransfer") &&
            !ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs").Contains("TimedProcessState") &&
            !ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs").Contains("activeProcesses") &&
            !ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs").Contains("CargoTransferState") &&
            !ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs").Contains("cargoTransfer");
        report.Check(timedAndCargoRuntimeRemoved,
            "Legacy dock timed jobs, timed missions and island cargo-transfer runtime are physically removed.");

        PortStorageState initialBaseStorage = loadedMeta.GetCapitalStorageState();
        if (initialBaseStorage != null)
        {
            progress.shipFuelTank.SetResource("charcoal");
            progress.shipClaudiumTank.SetResource("claudium");
            progress.shipFuelTank.amountKg = 0f;
            progress.shipClaudiumTank.amountKg = 0f;
            SyncTestShipConsumables(loadedSession, progress);
            int fuelStorageBefore = initialBaseStorage.GetResourceAmount("charcoal");
            int claudiumStorageBefore = initialBaseStorage.GetResourceAmount("claudium");
            bool selectedOreWithoutConsumables = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeOreSortieId, out _);
            bool canBeginWithoutConsumables = loadedMeta.CanBeginSelectedSessionSortie(out string noConsumableSortieMessage);
            report.Check(selectedOreWithoutConsumables
                && canBeginWithoutConsumables
                && progress.shipFuelTank.GetAmount("charcoal") <= 0.001f
                && progress.shipClaudiumTank.GetAmount("claudium") <= 0.001f
                && initialBaseStorage.GetResourceAmount("charcoal") == fuelStorageBefore
                && initialBaseStorage.GetResourceAmount("claudium") == claudiumStorageBefore,
                "Core sortie preparation needs no coal or claudium refuel; built-in ship systems are ready: "
                + noConsumableSortieMessage);

            bool baseShipAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult baseShipAssembly);
            float baseShipStarterPayloadKg = loadedMeta.startingFuelKg + loadedMeta.startingClaudiumKg + 25f;
            string baseShipFlightEnvelope = "assembly did not build.";
            bool baseShipFlightEnvelopeOk = baseShipAssemblyBuilt
                && HasStableBaseShipFlightEnvelope(
                    baseShipAssembly,
                    baseShipStarterPayloadKg,
                    0.5f,
                    12f,
                    out baseShipFlightEnvelope);
            report.Check(baseShipFlightEnvelopeOk,
                "Base ship recovery has enough lift, hull thrust, and speed for an ore sortie: "
                + baseShipFlightEnvelope);
        }
        else
        {
            report.Check(false, "Core no-refuel sortie test has base storage.");
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
            && defaultSortie.primaryBranch == BaseProcessingBranch.Ore
            && Approximately(defaultSortie.entryPosition.y, SessionExtractionConstants.DefaultSortieEntryAltitudeMeters, 0.1f),
            "Default safe sortie is a 1.5 km ore cylinder at the playable sortie altitude.");

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
                && Approximately(sortie.entryPosition.y, SessionExtractionConstants.DefaultSortieEntryAltitudeMeters, 0.1f)
                && sortie.distanceToBaseKm > 0f;
            if (sortie != null)
            {
                sortieBranches.Add(sortie.primaryBranch);
            }
        }

        report.Check(sortieCatalogValid && sortieBranches.Count == SessionExtractionIndustry.ProcessingBranches.Length,
            "Session extraction has a default 1.5 km sortie catalog for ore, gas, automatons, leviathans, and survey data.");

        Vector3 baseDockPosition = GameplaySessionAccountData.ResolveStarterDockPosition(capitalId);
        bool sortieLocationsSeparatedFromPort = defaultSorties.Count == SessionExtractionIndustry.ProcessingBranches.Length;
        for (int i = 0; i < defaultSorties.Count; i++)
        {
            SortieZoneDefinition sortie = defaultSorties[i];
            if (sortie == null)
            {
                sortieLocationsSeparatedFromPort = false;
                continue;
            }

            sortie.Normalize();
            float centerDistance = Mathf.Sqrt(HorizontalSqrDistance(sortie.centerPosition, baseDockPosition));
            float sortieEntryDockDistance = Mathf.Sqrt(HorizontalSqrDistance(sortie.entryPosition, baseDockPosition));
            sortieLocationsSeparatedFromPort &= centerDistance >= SessionExtractionConstants.DefaultSortiePocketMinimumDockSeparationMeters
                && sortieEntryDockDistance >= SessionExtractionConstants.DefaultSortiePocketMinimumDockSeparationMeters;
        }

        report.Check(sortieLocationsSeparatedFromPort,
            "Default sortie locations are physically separated from the port pocket; sortie space is not spawned under the dock.");

        bool cycledSortie = loadedMeta.SelectNextSessionSortie(out _);
        report.Check(cycledSortie
            && loadedMeta.GetSelectedSessionSortieDefinition().primaryBranch != BaseProcessingBranch.Ore,
            "Runtime can cycle the selected extraction sortie before launch without the retired mission launcher panel.");

        bool selectedGasSortie = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeGasSortieId, out string selectedGasMessage);
        bool selectedGasReadyWithoutFitting = loadedMeta.CanBeginSelectedSessionSortie(out string gasReadyReason);
        bool selectedAutomatonSortie = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeAutomatonSortieId, out string selectedAutomatonMessage);
        bool selectedAutomatonReadyWithoutFitting = loadedMeta.CanBeginSelectedSessionSortie(out string automatonReadyReason);
        bool selectedLeviathanSortie = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeLeviathanSortieId, out string selectedLeviathanMessage);
        bool selectedLeviathanReadyWithoutFitting = loadedMeta.CanBeginSelectedSessionSortie(out string leviathanReadyReason);
        bool selectedSurveySortie = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeSurveySortieId, out string selectedSurveyMessage);
        bool selectedSurveyReadyWithoutFitting = loadedMeta.CanBeginSelectedSessionSortie(out string surveyReadyReason);
        report.Check(selectedGasSortie
            && selectedGasReadyWithoutFitting
            && selectedAutomatonSortie
            && selectedAutomatonReadyWithoutFitting
            && selectedLeviathanSortie
            && selectedLeviathanReadyWithoutFitting
            && selectedSurveySortie
            && selectedSurveyReadyWithoutFitting,
            "Selected gas, automaton, leviathan, and survey sorties use built-in ship systems instead of external fitting gates: "
            + selectedGasMessage + " / " + gasReadyReason
            + " / " + selectedAutomatonMessage + " / " + automatonReadyReason
            + " / " + selectedLeviathanMessage + " / " + leviathanReadyReason
            + " / " + selectedSurveyMessage + " / " + surveyReadyReason);

        bool coreInstallLegacyUtilityBlocked = !loadedMeta.InstallModule("utility_01", SessionExtractionConstants.StarterGasExtractorModuleId);
        bool coreInstallHighIntoLowBlocked = !loadedMeta.InstallModule(SessionExtractionConstants.StarterLowSlotId, SessionExtractionConstants.StarterGasExtractorModuleId);
        bool coreInstallValidHighAllowed = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, SessionExtractionConstants.StarterGasExtractorModuleId);
        bool coreInstallValidHighCleared = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, "");
        report.Check(coreInstallLegacyUtilityBlocked
            && coreInstallHighIntoLowBlocked
            && coreInstallValidHighAllowed
            && coreInstallValidHighCleared
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule("utility_01"))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterLowSlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId)),
            "Technical assembly API rejects legacy utility and wrong-band installs while preserving valid optional upgrade installs.");

        bool highInstalledBeforeHullSelect = loadedMeta.InstallModule(SessionExtractionConstants.StarterHighSlotId, SessionExtractionConstants.StarterGasExtractorModuleId);
        string hullBeforeLegacySelect = progress.selectedHullId;
        bool coreSelectHullBlocked = !loadedMeta.SelectHull("removed_legacy_hull");
        bool moduleKeptAfterBlockedHullSelect = progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId) == SessionExtractionConstants.StarterGasExtractorModuleId;
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

        GameSessionMode modeBeforeLegacyDock = loadedMeta.CurrentMode;
        string dockBeforeLegacyDock = progress.currentDockId;
        bool legacyDockOutsideSortieBlocked = !loadedMeta.DockAt("Island1");
        bool baseDockStillAllowed = loadedMeta.DockAt(capitalId);
        report.Check(legacyDockOutsideSortieBlocked
            && baseDockStillAllowed
            && loadedMeta.CurrentMode == modeBeforeLegacyDock
            && progress.currentDockId == capitalId
            && dockBeforeLegacyDock == capitalId,
            "Core public DockAt allows only the base dock outside active sorties.");

        loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeOreSortieId, out _);

        SortieLocationIsolationController locationIsolation = SortieLocationIsolationController.EnsureForLoadedGameplayScene();
        locationIsolation?.RefreshNow();
        bool sortieStarted = loadedMeta.BeginSafeOreSortie();
        report.Check(sortieStarted
            && loadedMeta.HasActiveSortie
            && progress.currentMode == GameSessionMode.Flight,
            "Runtime starts a safe ore sortie from the base and switches to flight without the retired mission launcher panel.");
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
            && !entryShip.StrategicStopCommand;
        string sortieEntryDetails =
            "distance=" + entryDistance.ToString("0.###", CultureInfo.InvariantCulture)
            + ", radius=" + zone.radiusMeters.ToString("0.###", CultureInfo.InvariantCulture)
            + ", expectedOffset=" + expectedEntryOffset.ToString("0.###", CultureInfo.InvariantCulture)
            + ", speed=" + entrySpeed.ToString("0.###", CultureInfo.InvariantCulture)
            + ", expectedSpeed=" + expectedEntrySpeed.ToString("0.###", CultureInfo.InvariantCulture)
            + ", inwardVelocityDot=" + inwardVelocityDot.ToString("0.###", CultureInfo.InvariantCulture)
            + ", inwardFacingDot=" + inwardFacingDot.ToString("0.###", CultureInfo.InvariantCulture)
            + ", slip=" + (entryShip != null && entryShip.claudiumSlipstreamEnabled).ToString()
            + ", charge=" + (entryShip != null ? entryShip.ClaudiumSlipstreamCharge01.ToString("0.###", CultureInfo.InvariantCulture) : "n/a")
            + ", notch=" + (entryControls != null ? entryControls.ManualThrustNotch.ToString(CultureInfo.InvariantCulture) : "n/a")
            + ", stop=" + (entryShip != null && entryShip.StrategicStopCommand).ToString();
        report.Check(sortieEntryApproachReady,
            sortieEntryApproachReady
                ? "Sortie entry starts 20 seconds outside the zone edge at the sustainable full-slipstream max speed, full claudium slipstream, and no autopilot handoff."
                : "Sortie entry must start 20 seconds outside the zone edge at full-slipstream speed: " + sortieEntryDetails);
        report.Check(Mathf.Sqrt(HorizontalSqrDistance(entryPosition, baseDockPosition)) >= SessionExtractionConstants.DefaultSortiePocketMinimumDockSeparationMeters,
            "Started sortie ship is placed in the isolated session pocket, not under or near the port.");

        locationIsolation = SortieLocationIsolationController.EnsureForLoadedGameplayScene();
        locationIsolation?.RefreshNow();
        bool portPocketHiddenDuringSortie = locationIsolation != null
            && locationIsolation.IsIsolationApplied
            && GameObject.Find(SortieLocationIsolationController.SessionSceneObjectsRootName) == null;
        report.Check(portPocketHiddenDuringSortie,
            "Active sortie hides or omits the port/open-world scene pocket so the sortie is surrounded only by empty session space.");

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
            Mathf.Max(
                zone.entryPosition.y,
                zone.stormFloorY + SessionExtractionConstants.DefaultSortieEntryAltitudeMeters),
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
        PortStorageState baseStorageBeforeExtraction = loadedMeta.GetCapitalStorageState();
        int baseOreBeforeExtraction = baseStorageBeforeExtraction != null ? baseStorageBeforeExtraction.GetResourceAmount("windshale_ore") : 0;
        int sortieOreBeforeExtraction = progress.GetShipCargoAmount("windshale_ore");
        LowGradeOreStackState baseLowGradeBefore = baseStorageBeforeExtraction != null ? baseStorageBeforeExtraction.GetLowGradeOreStack("windshale_ore", false) : null;
        float baseLowGradeRawBefore = baseLowGradeBefore != null ? baseLowGradeBefore.rawMassKg : 0f;
        float baseLowGradeUsefulBefore = baseLowGradeBefore != null ? baseLowGradeBefore.usefulOreKg : 0f;
        if (baseStorageBeforeExtraction != null)
        {
            baseStorageBeforeExtraction.AddLowGradeOre("windshale_ore", 1000f, 80f, "Windshale");
        }

        progress.AddShipLowGradeOreCargo("windshale_ore", 120f, 24f, "Windshale");

        progress.shipFuelTank.SetResource("charcoal");
        progress.shipClaudiumTank.SetResource("claudium");
        progress.shipFuelTank.TrySpend("charcoal", progress.shipFuelTank.GetAmount("charcoal"));
        progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
        SyncTestShipConsumables(loadedSession, progress);
        SortieReturnEstimate estimateWithoutReserves = loadedMeta.GetActiveSortieReturnEstimate();
        bool extractedWithoutRunup = loadedMeta.TryExtractActiveSortie(out string noRunupMessage);
        report.Check(!estimateWithoutReserves.canExtract
            && estimateWithoutReserves.isNearBoundary
            && estimateWithoutReserves.hasEnoughCoal
            && estimateWithoutReserves.hasEnoughClaudium
            && Approximately(estimateWithoutReserves.requiredCoalKg, 0f, 0.001f)
            && Approximately(estimateWithoutReserves.requiredClaudiumKg, 0f, 0.001f)
            && !extractedWithoutRunup
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Boundary extraction ignores empty coal and claudium tanks, but still waits for the 5-second outside-circle hold: " + noRunupMessage);

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
            false);
        bool screenshotReserveStartsTimer = progress.activeSortie.extractionRunupSeconds > 0f
            && loadedMeta.ActiveSortieExtractionRunupStatus.Contains("Exit holding");
        report.Check(screenshotReserveEstimate.hasEnoughCoal
            && screenshotReserveEstimate.hasEnoughClaudium
            && Approximately(screenshotReserveEstimate.requiredCoalKg, 0f, 0.001f)
            && Approximately(screenshotReserveEstimate.requiredClaudiumKg, 0f, 0.001f)
            && screenshotReserveStartsTimer,
            "Safe ore extraction starts the outside-circle timer without coal, claudium, or slipstream.");

        progress.activeSortie.ResetExtractionRunup();
        progress.shipFuelTank.TrySpend("charcoal", progress.shipFuelTank.GetAmount("charcoal"));
        progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
        SyncTestShipConsumables(loadedSession, progress);
        Vector3 stormBoundaryPosition = new Vector3(
            boundaryPosition.x,
            zone.stormFloorY - 10f,
            boundaryPosition.z);
        MoveSessionShip(loadedSession, progress, stormBoundaryPosition);
        SortieReturnEstimate estimateInStorm = loadedMeta.GetActiveSortieReturnEstimate();
        bool extractedFromStorm = loadedMeta.TryExtractActiveSortie(out string stormBlockMessage);
        bool stormLayerStillCountsOutsideCircle = loadedMeta.RecordActiveSortieExtractionRunup(
            stormBoundaryPosition,
            Vector3.zero,
            Vector3.left,
            0.25f,
            false);
        report.Check(!estimateInStorm.canExtract
            && estimateInStorm.isNearBoundary
            && !estimateInStorm.isAboveStorm
            && estimateInStorm.hasEnoughCoal
            && estimateInStorm.hasEnoughClaudium
            && !extractedFromStorm
            && !stormLayerStillCountsOutsideCircle
            && progress.activeSortie.extractionRunupSeconds > 0f
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Boundary extraction does not care about the storm layer; outside-circle hold still starts there, but extraction waits for the full hold: " + stormBlockMessage);

        Vector3 centerPosition = new Vector3(
            zone.centerPosition.x,
            Mathf.Max(
                zone.entryPosition.y,
                zone.stormFloorY + SessionExtractionConstants.DefaultSortieEntryAltitudeMeters),
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
            "Extraction is blocked until the ship leaves the mission circle: " + boundaryBlockMessage);

        bool legacyDockDuringSortie = loadedMeta.DockAt(capitalId);
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
            "Boundary extraction requires 5 seconds outside the mission circle before the instant home return: " + runupBlockMessage);

        Vector3 outwardForForwardRunup = new Vector3(
            boundaryPosition.x - zone.centerPosition.x,
            0f,
            boundaryPosition.z - zone.centerPosition.z).normalized;
        bool forwardAimedRunup = loadedMeta.RecordActiveSortieExtractionRunup(
            boundaryPosition,
            Vector3.zero,
            Vector3.Cross(Vector3.up, outwardForForwardRunup).normalized,
            zone.extractionRunupRequiredSeconds + 0.25f,
            false);
        SortieReturnEstimate forwardAimedEstimate = loadedMeta.GetActiveSortieReturnEstimate();
        report.Check(forwardAimedRunup
            && forwardAimedEstimate.hasExtractionRunup
            && loadedMeta.HasActiveSortie
            && loadedMeta.ActiveSortieExtractionRunupStatus.Contains("Exit ready"),
            "Outside-circle exit timer accepts a stationary non-slip ship; after 5 seconds it waits for the Extract home button instead of auto-teleporting.");
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
            loadedShipForRunup.claudiumSlipstreamEnabled = false;
            loadedShipForRunup.claudiumSlipstreamCharge01 = 1f;
        }

        progress.SetFlightPose(boundaryPosition, Quaternion.LookRotation(outwardForForwardRunup, Vector3.up));
        bool metaRunupTickerInvoked = TryInvokePrivateMethod(loadedMeta, "UpdateActiveSortieExtractionRunupFromActiveShip", report);
        SortieReturnEstimate metaRunupTickerEstimate = loadedMeta.GetActiveSortieReturnEstimate();
        report.Check(metaRunupTickerInvoked
            && metaRunupTickerEstimate.extractionRunupSeconds > 0f
            && loadedMeta.ActiveSortieExtractionRunupStatus.Contains("Exit holding"),
            "MetaGameState owns the live outside-circle exit timer instead of relying on slipstream or the visual boundary controller.");
        progress.activeSortie.ResetExtractionRunup();

        PrimeActiveSortieExtractionRunup(loadedMeta, progress);
        SortieReturnEstimate estimateReady = loadedMeta.GetActiveSortieReturnEstimate();
        report.Check(estimateReady.canExtract
            && estimateReady.hasExtractionRunup
            && Approximately(estimateReady.requiredCoalKg, 0f, 0.001f)
            && Approximately(estimateReady.requiredClaudiumKg, 0f, 0.001f),
            "Boundary extraction has zero coal and claudium return cost after the 5-second outside-circle hold.");
        float coalBeforeExtraction = progress.shipFuelTank.GetAmount("charcoal");
        float claudiumBeforeExtraction = progress.shipClaudiumTank.GetAmount("claudium");

        bool extracted = loadedHud != null ? loadedHud.TryDockNearest() : loadedMeta.TryExtractActiveSortie(out _);
        string extractionMessage = loadedHud != null ? "HUD extraction action" : "Meta extraction action";
        PortStorageState baseStorage = loadedMeta.GetCapitalStorageState();
        report.Check(extracted
            && loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.currentDockId == capitalId
            && !loadedMeta.HasActiveSortie,
            "HUD extraction returns home to base: " + extractionMessage);
        locationIsolation?.RefreshNow();
        bool portPocketRestoredAfterExtraction = locationIsolation != null
            && !locationIsolation.IsIsolationApplied
            && (locationIsolation.TargetCount == 0 || GameObject.Find(SortieLocationIsolationController.SessionSceneObjectsRootName) != null);
        report.Check(extracted && portPocketRestoredAfterExtraction,
            "Returning from a sortie restores the port scene pocket only after the Extract home transition when that pocket exists.");
        report.Check(extracted
            && Approximately(
                progress.shipFuelTank.GetAmount("charcoal"),
                coalBeforeExtraction,
                0.001f)
            && Approximately(
                progress.shipClaudiumTank.GetAmount("claudium"),
                claudiumBeforeExtraction,
                0.001f),
            "Boundary extraction returns home without consuming coal or claudium reserves.");
        report.Check(baseStorage != null && baseStorage.GetResourceAmount("windshale_ore") >= baseOreBeforeExtraction + sortieOreBeforeExtraction,
            "Extracted sortie ore is stored at the base.");
        LowGradeOreStackState extractedLowGradeOre = baseStorage != null ? baseStorage.GetLowGradeOreStack("windshale_ore", false) : null;
        float expectedLowGradeRaw = baseLowGradeRawBefore + 1120f;
        float expectedLowGradeUseful = baseLowGradeUsefulBefore + 104f;
        report.Check(extractedLowGradeOre != null
            && Approximately(extractedLowGradeOre.rawMassKg, expectedLowGradeRaw, 0.001f)
            && Approximately(extractedLowGradeOre.usefulOreKg, expectedLowGradeUseful, 0.001f)
            && Approximately(extractedLowGradeOre.UsefulConcentration01, expectedLowGradeUseful / expectedLowGradeRaw, 0.0001f)
            && (progress.shipLowGradeOreCargo == null || progress.shipLowGradeOreCargo.Count == 0),
            "Extracted low-grade ore concentrate is stored at the base and blends by raw/useful mass.");
        if (baseStorage == null)
        {
            return;
        }

        baseStorage.AddResource("windshale_ore", 100);
        baseStorage.AddResource("cloud_condensate", 100);
        baseStorage.AddResource(SessionExtractionConstants.StarterAutomatonPartItemId, 100);
        baseStorage.AddResource(SessionExtractionConstants.RockInfoItemId, 100);

        bool oreProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.Ore, out string processingMessage);
        BaseProcessingFacilityState oreBatchFacility = loadedMeta.GetBaseProcessingFacilityState("legacy_batch_" + BaseProcessingBranch.Ore, BaseProcessingBranch.Ore, 1);
        report.Check(oreProcessed
            && oreBatchFacility.totalProcessedUnits > 0f
            && oreBatchFacility.outputBuffers.Count > 0,
            "Base ore processing consumes a cycle and stores fractional mineral output buffers: " + processingMessage);

        List<string> oreItemIdsForLowGradeProbe = GetOreItemIdsForBigTest(loadedMeta.SessionConfig);
        for (int i = 0; i < oreItemIdsForLowGradeProbe.Count; i++)
        {
            baseStorage.SetResourceAmount(oreItemIdsForLowGradeProbe[i], 0);
        }

        baseStorage.AddLowGradeOre("windshale_ore", 100f, 22f, "Windshale");
        LowGradeOreStackState lowGradeBeforeProcessing = baseStorage.GetLowGradeOreStack("windshale_ore", false);
        float lowGradeRawBeforeProcessing = lowGradeBeforeProcessing != null ? lowGradeBeforeProcessing.rawMassKg : 0f;
        float oreProcessedBeforeLowGrade = oreBatchFacility.totalProcessedUnits;
        bool lowGradeOreProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.Ore, out string lowGradeProcessingMessage);
        LowGradeOreStackState lowGradeAfterProcessing = baseStorage.GetLowGradeOreStack("windshale_ore", false);
        float lowGradeRawAfterProcessing = lowGradeAfterProcessing != null ? lowGradeAfterProcessing.rawMassKg : 0f;
        report.Check(lowGradeOreProcessed
            && lowGradeRawAfterProcessing < lowGradeRawBeforeProcessing
            && oreBatchFacility.totalProcessedUnits > oreProcessedBeforeLowGrade
            && lowGradeProcessingMessage.Contains("low-grade"),
            "Base ore processing can consume low-grade concentrate by raw mass while mineral output is limited by useful concentration: " + lowGradeProcessingMessage);

        bool gasProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.Gas, out string gasMessage);
        BaseProcessingFacilityState gasBatchFacility = loadedMeta.GetBaseProcessingFacilityState("legacy_batch_" + BaseProcessingBranch.Gas, BaseProcessingBranch.Gas, 1);
        report.Check(gasProcessed
            && gasBatchFacility.totalProcessedUnits > 0f
            && gasBatchFacility.outputBuffers.Count > 0,
            "Base gas processing consumes a cycle and stores fractional gas-material output buffers: " + gasMessage);

        bool automatonProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.AutomatonDismantling, out string automatonMessage);
        BaseProcessingFacilityState automatonBatchFacility = loadedMeta.GetBaseProcessingFacilityState("legacy_batch_" + BaseProcessingBranch.AutomatonDismantling, BaseProcessingBranch.AutomatonDismantling, 1);
        report.Check(automatonProcessed
            && automatonBatchFacility.totalProcessedUnits > 0f
            && automatonBatchFacility.outputBuffers.Count > 0,
            "Base automaton dismantling consumes a cycle and buffers dismantled outputs: " + automatonMessage);

        bool cyberProcessed = loadedMeta.TryProcessBaseBatch(BaseProcessingBranch.CyberneticDeciphering, out string cyberMessage);
        BaseProcessingFacilityState cyberBatchFacility = loadedMeta.GetBaseProcessingFacilityState("legacy_batch_" + BaseProcessingBranch.CyberneticDeciphering, BaseProcessingBranch.CyberneticDeciphering, 1);
        report.Check(cyberProcessed
            && cyberBatchFacility.totalProcessedUnits > 0f
            && cyberBatchFacility.outputBuffers.Count > 0,
            "Base cybernetic deciphering consumes a cycle and buffers research output: " + cyberMessage);

        baseStorage.AddResource("windcalf_carcass", 60);
        int carcassBeforeLoad = baseStorage.GetResourceAmount("windcalf_carcass");
        int leviathanFatBefore = baseStorage.GetResourceAmount(SessionExtractionConstants.LeviathanFatItemId);
        BaseProcessingFacilityState butcheryFacility = loadedMeta.GetBaseProcessingFacilityState("butchery", BaseProcessingBranch.LeviathanProcessing, 5);
        bool leviathanLoaded = loadedMeta.TryLoadBaseProcessingInput("butchery", BaseProcessingBranch.LeviathanProcessing, 5, "windcalf_carcass", 45, out string leviathanLoadMessage);
        long processingStartTicks = Math.Max(DateTime.UtcNow.Ticks, progress.lastProcessUtcTicks);
        progress.lastProcessUtcTicks = processingStartTicks;
        double leviathanProcessingSeconds = CalculateTimedProcessingSecondsToProcessUnits(butcheryFacility, 45);
        int advancedProcessingCycles = loadedMeta.AdvanceRealTimeProcesses(new DateTime(processingStartTicks, DateTimeKind.Utc).AddSeconds(leviathanProcessingSeconds));
        BaseProcessingOutputBufferState fatBufferBeforeCollect = butcheryFacility.GetOutputBuffer(SessionExtractionConstants.LeviathanFatItemId, false);
        bool leviathanBuffered = leviathanLoaded
            && advancedProcessingCycles >= 45
            && baseStorage.GetResourceAmount("windcalf_carcass") == carcassBeforeLoad - 45
            && butcheryFacility.BunkerLoadUnits == 0
            && baseStorage.GetResourceAmount(SessionExtractionConstants.LeviathanFatItemId) == leviathanFatBefore
            && fatBufferBeforeCollect != null
            && fatBufferBeforeCollect.readyAmount >= 1
            && fatBufferBeforeCollect.fractionalAmount > 0f;
        report.Check(leviathanBuffered,
            "Timed leviathan processing loads carcass units into a bunker, runs 10-second cycles, and buffers only whole-ready outputs: " + leviathanLoadMessage);

        bool leviathanCollected = loadedMeta.TryCollectBaseProcessingOutputs("butchery", BaseProcessingBranch.LeviathanProcessing, 5, out string leviathanCollectMessage);
        BaseProcessingOutputBufferState fatBufferAfterCollect = butcheryFacility.GetOutputBuffer(SessionExtractionConstants.LeviathanFatItemId, false);
        report.Check(leviathanCollected
            && baseStorage.GetResourceAmount(SessionExtractionConstants.LeviathanFatItemId) > leviathanFatBefore
            && fatBufferAfterCollect != null
            && fatBufferAfterCollect.readyAmount == 0
            && fatBufferAfterCollect.fractionalAmount > 0f,
            "Collect all moves whole processed leviathan outputs to storage while leaving fractional progress in the building: " + leviathanCollectMessage);

        baseStorage.AddResource("windshale_ore", 45);
        baseStorage.AddResource("cloud_condensate", 45);
        baseStorage.AddResource(SessionExtractionConstants.StarterAutomatonPartItemId, 45);
        baseStorage.AddResource("windcalf_carcass", 45);
        baseStorage.AddResource(SessionExtractionConstants.RockInfoItemId, 45);
        bool timedProcessingExact = true;
        string timedProcessingMessage = "";
        timedProcessingExact &= RunTimedProcessingFacilityForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            "bigtest_timed_ore",
            BaseProcessingBranch.Ore,
            5,
            "windshale_ore",
            45,
            out timedProcessingMessage);
        timedProcessingExact &= RunTimedProcessingFacilityForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            "bigtest_timed_gas",
            BaseProcessingBranch.Gas,
            5,
            "cloud_condensate",
            45,
            out timedProcessingMessage);
        timedProcessingExact &= RunTimedProcessingFacilityForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            "bigtest_timed_automaton",
            BaseProcessingBranch.AutomatonDismantling,
            5,
            SessionExtractionConstants.StarterAutomatonPartItemId,
            45,
            out timedProcessingMessage);
        timedProcessingExact &= RunTimedProcessingFacilityForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            "bigtest_timed_leviathan",
            BaseProcessingBranch.LeviathanProcessing,
            5,
            "windcalf_carcass",
            45,
            out timedProcessingMessage);
        timedProcessingExact &= RunTimedProcessingFacilityForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            "bigtest_timed_cyber",
            BaseProcessingBranch.CyberneticDeciphering,
            5,
            SessionExtractionConstants.RockInfoItemId,
            45,
            out timedProcessingMessage);
        report.Check(timedProcessingExact,
            "Timed processing for all five branches loads storage, advances offscreen and collects exactly the ready whole outputs: "
            + timedProcessingMessage);

        bool allProcessingBranchesTracked = true;
        for (int i = 0; i < SessionExtractionIndustry.ProcessingBranches.Length; i++)
        {
            BaseProcessingLineState line = progress.baseIndustry.GetProcessing(SessionExtractionIndustry.ProcessingBranches[i]);
            allProcessingBranchesTracked &= line != null && line.totalProcessedUnits > 0f;
        }

        report.Check(allProcessingBranchesTracked,
            "All five base processing branches record throughput in the extraction core.");

        baseStorage.AddResource("iron", 60);
        baseStorage.AddResource("calcite", 20);
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
            && config.GetSpecialModule(SessionExtractionConstants.StarterLeviathanSalvageModuleId) != null;
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

        int airframeKitsBeforeCascade = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterAirframeKitItemId);
        bool cascadeComplete = QueueAndCompleteCascadeOrderForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            loadedMeta.CreateStarterAirframeCascadeOrder(),
            1,
            out string cascadeMessage);
        report.Check(cascadeComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterAirframeKitItemId) == airframeKitsBeforeCascade + 1,
            "Eight-type cascade production queues, advances by meta time, and creates exactly one starter airframe kit: " + cascadeMessage);

        bool allCascadeLinesLoaded = true;
        for (int i = 0; i < SessionExtractionIndustry.CascadeProductionTypes.Length; i++)
        {
            CascadeProductionLineState line = progress.baseIndustry.GetProduction(SessionExtractionIndustry.CascadeProductionTypes[i]);
            allCascadeLinesLoaded &= line != null && line.totalLoadApplied > 0f;
        }

        report.Check(allCascadeLinesLoaded,
            "Starter cascade order records load against all eight production types.");

        CascadeProductionOrderDefinition moduleOrder = SessionExtractionIndustry.CreateStarterModuleKitOrder();
        CascadeProductionEstimate moduleEstimate = loadedMeta.EstimateBaseCascadeOrder(moduleOrder);
        int moduleKitsBeforeModuleOrder = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId);
        string moduleCascadeMessage = "No module order.";
        bool moduleCascadeComplete = moduleOrder != null
            && QueueAndCompleteCascadeOrderForBigTest(
                loadedMeta,
                progress,
                baseStorage,
                moduleOrder,
                1,
                out moduleCascadeMessage);
        report.Check(moduleOrder != null
            && moduleOrder.orderId == SessionExtractionConstants.StarterModuleKitOrderId
            && moduleEstimate.canRun
            && moduleCascadeComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) == moduleKitsBeforeModuleOrder + 1,
            "Cascade catalog queues and time-completes a starter module kit from early ore materials: " + moduleCascadeMessage);

        int moduleKitsBeforeHighUpgrade = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId);
        bool highGasUpgradeInstalled = loadedMeta.TryInstallStarterGasExtractorUpgrade(out string highGasUpgradeMessage);
        bool selectedGasAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeGasSortieId, out _);
        bool selectedGasReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string gasAfterUpgradeReason);
        report.Check(highGasUpgradeInstalled
            && selectedGasAfterUpgrade
            && selectedGasReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId) == SessionExtractionConstants.StarterGasExtractorModuleId
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) == moduleKitsBeforeHighUpgrade - 1,
            "Starter module kit can still install the optional gas extractor upgrade while gas sorties remain built-in: " + highGasUpgradeMessage + " / " + gasAfterUpgradeReason);

        SortieResourceCacheController resourceCacheController = FindFirstObjectByType<SortieResourceCacheController>();
        if (resourceCacheController == null)
        {
            GameObject cacheControllerObject = new GameObject("Big Test Sortie Resource Cache Controller");
            resourceCacheController = cacheControllerObject.AddComponent<SortieResourceCacheController>();
        }

        report.Check(MiningFragment.DefaultMaxActiveFragments <= 200 && MiningFragment.MaxActiveFragments <= 200,
            "Mining fragments are capped so high-altitude sortie drops cannot accumulate hundreds of active sphere renderers.");

        ValidateStarterResourceCacheSortie(
            report,
            loadedMeta,
            loadedSession,
            progress,
            resourceCacheController,
            SessionExtractionConstants.DefaultSafeGasSortieId,
            "cloud_condensate",
            "Gas");

        baseStorage.AddResource("iron", 12);
        baseStorage.AddResource("calcite", 3);
        baseStorage.AddResource("charcoal", 6);

        int moduleKitsBeforeExtraOrders = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId);
        bool extraModuleKitsComplete = QueueAndCompleteCascadeOrderForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            SessionExtractionIndustry.CreateStarterModuleKitOrder(),
            3,
            out string extraModuleKitMessage);

        report.Check(extraModuleKitsComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) == moduleKitsBeforeExtraOrders + 3,
            "Starter module kit order supports quantity, saves one timed queue item, and produces exactly three kits: " + extraModuleKitMessage);

        bool miningHoldInstalled = loadedMeta.TryInstallStarterMiningHoldUpgrade(out string miningHoldUpgradeMessage);
        bool selectedAutomatonAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeAutomatonSortieId, out _);
        bool selectedAutomatonReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string automatonAfterUpgradeReason);
        report.Check(miningHoldInstalled
            && selectedAutomatonAfterUpgrade
            && selectedAutomatonReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterSecondHighSlotId) == SessionExtractionConstants.StarterMiningHoldModuleId,
            "Starter module kit can still install the optional wreck collector while automaton sorties remain built-in: " + miningHoldUpgradeMessage + " / " + automatonAfterUpgradeReason);

        bool salvageInstalled = loadedMeta.TryInstallStarterLeviathanSalvageUpgrade(out string salvageUpgradeMessage);
        bool selectedLeviathanAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeLeviathanSortieId, out _);
        bool selectedLeviathanReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string leviathanAfterUpgradeReason);
        report.Check(salvageInstalled
            && selectedLeviathanAfterUpgrade
            && selectedLeviathanReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterThirdHighSlotId) == SessionExtractionConstants.StarterLeviathanSalvageModuleId,
            "Starter module kit can still install the optional leviathan salvage rig while leviathan sorties remain built-in: " + salvageUpgradeMessage + " / " + leviathanAfterUpgradeReason);

        bool observationInstalled = loadedMeta.TryInstallStarterObservationUpgrade(out string observationUpgradeMessage);
        bool selectedSurveyAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeSurveySortieId, out _);
        bool selectedSurveyReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string surveyAfterUpgradeReason);
        report.Check(observationInstalled
            && selectedSurveyAfterUpgrade
            && selectedSurveyReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterMidSlotId) == SessionExtractionConstants.StarterObservationPostModuleId,
            "Starter module kit can still install the optional observation module while survey sorties remain built-in: " + observationUpgradeMessage + " / " + surveyAfterUpgradeReason);

        ValidateStarterResourceCacheSortie(
            report,
            loadedMeta,
            loadedSession,
            progress,
            resourceCacheController,
            SessionExtractionConstants.DefaultSafeAutomatonSortieId,
            SessionExtractionConstants.StarterAutomatonPartItemId,
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

        baseStorage.AddResource(SessionExtractionConstants.BoneGritItemId, 2);
        baseStorage.AddResource("iron", 2);
        baseStorage.AddResource("charcoal", 2);
        CascadeProductionOrderDefinition munitionOrder = SessionExtractionIndustry.CreateStarterMunitionBundleOrder();
        CascadeProductionEstimate munitionEstimate = loadedMeta.EstimateBaseCascadeOrder(munitionOrder);
        int munitionBundlesBeforeCascade = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId);
        bool munitionCascadeComplete = QueueAndCompleteCascadeOrderForBigTest(
            loadedMeta,
            progress,
            baseStorage,
            munitionOrder,
            1,
            out string munitionCascadeMessage);
        report.Check(munitionOrder != null
            && munitionOrder.orderId == SessionExtractionConstants.StarterMunitionBundleOrderId
            && munitionEstimate.canRun
            && munitionCascadeComplete
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId) == munitionBundlesBeforeCascade + 4,
            "Cascade catalog queues and time-completes starter munition bundles from leviathan shell and minerals: " + munitionCascadeMessage);

        int munitionBundlesBeforeLoadout = baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId);
        int weaponBeforeLoadout = progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId);
        bool starterMunitionsReady = loadedMeta.CanLoadStarterMunitionsAtBase(out string starterMunitionsReadyMessage);
        bool starterMunitionsLoaded = loadedMeta.TryLoadStarterMunitionsAtBase(out string starterMunitionsLoadMessage);
        report.Check(starterMunitionsReady
            && starterMunitionsLoaded
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterMunitionBundleItemId) == munitionBundlesBeforeLoadout - 1
            && progress.GetShipCargoAmount(SessionExtractionConstants.StarterWeaponCargoItemId)
                == weaponBeforeLoadout + SessionExtractionConstants.StarterWeaponUnitsPerMunitionBundle,
            "Starter munition bundles can be loaded at the base into weapon cargo for sortie weapons: "
            + starterMunitionsReadyMessage + " / " + starterMunitionsLoadMessage);

        bool lowUpgradeInstalled = loadedMeta.TryInstallStarterCargoRackUpgrade(out string upgradeMessage);
        report.Check(lowUpgradeInstalled
            && progress.GetInstalledModule(SessionExtractionConstants.StarterLowSlotId) == SessionExtractionConstants.StarterCargoRackModuleId,
            "The first fitting upgrade consumes the kit and installs into a Low slot: " + upgradeMessage);

        bool fittedBaseShipAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult fittedBaseShipAssembly);
        float fittedBaseShipPayloadKg = loadedMeta.startingFuelKg + loadedMeta.startingClaudiumKg + 250f;
        string fittedBaseShipFlightEnvelope = "assembly did not build.";
        bool fittedBaseShipFlightEnvelopeOk = fittedBaseShipAssemblyBuilt
            && fittedBaseShipAssembly.hull != null
            && fittedBaseShipAssembly.hull.partId == GameplaySessionAccountData.DefaultStarterHullId
            && HasStableBaseShipFlightEnvelope(
                fittedBaseShipAssembly,
                fittedBaseShipPayloadKg,
                0.5f,
                12f,
                out fittedBaseShipFlightEnvelope);
        report.Check(fittedBaseShipFlightEnvelopeOk,
            "Fully fitted base ship still has enough lift and hull thrust to fly instead of falling: "
            + fittedBaseShipFlightEnvelope);

        string fittingSummary = loadedMeta.GetCoreFittingSummaryText();
        string fittingCompact = loadedMeta.GetCoreFittingCompactText();
        report.Check(fittingSummary.Contains("built in")
            && fittingSummary.Contains("guns")
            && fittingSummary.Contains("crusher")
            && fittingSummary.Contains("sensors")
            && fittingCompact.Contains("Built-in kit")
            && !fittingSummary.Contains("High:")
            && !fittingSummary.Contains("Mid:")
            && !fittingSummary.Contains("Low:")
            && !fittingSummary.Contains("Rig:")
            && !fittingCompact.Contains("H ")
            && !fittingCompact.Contains("M ")
            && !fittingCompact.Contains("L ")
            && !fittingCompact.Contains("R ")
            && !fittingSummary.Contains("utility")
            && !fittingCompact.Contains("utility")
            && !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("UsesLegacyDockAssemblyUi"),
            "Core dock UI exposes the built-in ship kit instead of slot-band fitting or legacy utility slots: " + fittingCompact + " / " + fittingSummary);

        bool assemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult assembly);
        report.Check(assemblyBuilt
            && !HasSlotType(assembly, "utility")
            && !HasSlotId(assembly, "utility_01"),
            "Current core ship assembly remains valid as a technical layer while public sortie prep hides slot bands.");

        bool lossSortieStarted = loadedMeta.BeginSafeOreSortie();
        if (lossSortieStarted)
        {
            progress.AddShipCargo("windshale_ore", 9);
            progress.shipFuelTank.SetResource("charcoal");
            progress.shipClaudiumTank.SetResource("claudium");
            progress.shipFuelTank.TrySpend("charcoal", progress.shipFuelTank.GetAmount("charcoal"));
            progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
            progress.shipFuelTank.Add("charcoal", loadedMeta.startingFuelKg + 500f, loadedMeta.startingFuelKg + 500f);
            progress.shipClaudiumTank.Add("claudium", loadedMeta.startingClaudiumKg + 500f, loadedMeta.startingClaudiumKg + 500f);
            SyncTestShipConsumables(loadedSession, progress);
        }

        bool recoveredFromLoss = lossSortieStarted && loadedMeta.LoseActiveSortieShipAndReturnToBase("big test sortie loss");
        report.Check(recoveredFromLoss
            && loadedMeta.CurrentMode == GameSessionMode.Docked
            && progress.currentDockId == capitalId
            && !loadedMeta.HasActiveSortie
            && progress.GetShipCargoAmount("windshale_ore") == 0
            && progress.selectedHullId == GameplaySessionAccountData.DefaultStarterHullId
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterMidSlotId))
            && string.IsNullOrWhiteSpace(progress.GetInstalledModule(SessionExtractionConstants.StarterLowSlotId))
            && Approximately(progress.shipFuelTank.GetAmount("charcoal"), loadedMeta.startingFuelKg, 0.001f)
            && Approximately(progress.shipClaudiumTank.GetAmount("claudium"), loadedMeta.startingClaudiumKg, 0.001f),
            "Sortie ship loss returns to base, deletes sortie loot, clears optional upgrades, restores the base hull, and resets only internal recovery reserves.");

        baseStorage = loadedMeta.GetCapitalStorageState();
        if (baseStorage != null)
        {
            baseStorage.SetResourceAmount("charcoal", 0);
            baseStorage.SetResourceAmount("claudium", 0);
        }

        progress.ReplaceShipAssembly("missing_session_core_hull");
        progress.shipFuelTank.TrySpend("charcoal", progress.shipFuelTank.GetAmount("charcoal"));
        progress.shipClaudiumTank.TrySpend("claudium", progress.shipClaudiumTank.GetAmount("claudium"));
        SyncTestShipConsumables(loadedSession, progress);
        bool baseShipRefuelReady = loadedMeta.CanRefuelBaseShip(out string baseShipRefuelReadyMessage);
        bool baseShipRefueled = loadedMeta.TryRefuelBaseShip(out string baseShipRefuelMessage);
        report.Check(!baseShipRefuelReady
            && !baseShipRefueled
            && baseStorage != null
            && baseStorage.GetResourceAmount("charcoal") == 0
            && baseStorage.GetResourceAmount("claudium") == 0
            && progress.shipFuelTank.GetAmount("charcoal") <= 0.001f
            && progress.shipClaudiumTank.GetAmount("claudium") <= 0.001f,
            "Base refuel is retired; missing coal and claudium never block or trigger a sortie preparation action: "
            + baseShipRefuelReadyMessage + " / " + baseShipRefuelMessage);
    }

    private static bool RunTimedProcessingFacilityForBigTest(
        MetaGameState meta,
        PlayerProgress progress,
        PortStorageState storage,
        string facilityId,
        BaseProcessingBranch branch,
        int level,
        string inputItemId,
        int amount,
        out string message)
    {
        message = "";
        if (meta == null || progress == null || storage == null || string.IsNullOrWhiteSpace(inputItemId) || amount <= 0)
        {
            message = "Timed processing test missing meta, progress, storage or input.";
            return false;
        }

        BaseProcessingFacilityState facility = meta.GetBaseProcessingFacilityState(facilityId, branch, level);
        if (facility == null)
        {
            message = "Timed processing facility was not created.";
            return false;
        }

        int inputBefore = storage.GetResourceAmount(inputItemId);
        bool loaded = meta.TryLoadBaseProcessingInput(facilityId, branch, level, inputItemId, amount, out string loadMessage);

        long startTicks = Math.Max(DateTime.UtcNow.Ticks, progress.lastProcessUtcTicks);
        progress.lastProcessUtcTicks = startTicks;
        int expectedProcessedUnits = amount;
        double secondsToComplete = CalculateTimedProcessingSecondsToProcessUnits(facility, amount);
        int completedEvents = loaded
            ? meta.AdvanceRealTimeProcesses(new DateTime(startTicks, DateTimeKind.Utc).AddSeconds(secondsToComplete))
            : 0;

        List<string> outputIds = new List<string>();
        List<int> readyAmounts = new List<int>();
        List<int> storageBeforeCollect = new List<int>();
        int readyTotal = 0;
        facility.outputBuffers ??= new List<BaseProcessingOutputBufferState>();
        for (int i = 0; i < facility.outputBuffers.Count; i++)
        {
            BaseProcessingOutputBufferState buffer = facility.outputBuffers[i];
            if (buffer == null || string.IsNullOrWhiteSpace(buffer.itemId) || buffer.readyAmount <= 0) continue;

            outputIds.Add(buffer.itemId);
            readyAmounts.Add(buffer.readyAmount);
            storageBeforeCollect.Add(storage.GetResourceAmount(buffer.itemId));
            readyTotal += buffer.readyAmount;
        }

        bool collected = meta.TryCollectBaseProcessingOutputs(facilityId, branch, level, out string collectMessage);
        bool outputsExact = outputIds.Count > 0;
        bool buffersCleared = outputIds.Count > 0;
        for (int i = 0; i < outputIds.Count; i++)
        {
            string outputId = outputIds[i];
            int expectedStorageAmount = storageBeforeCollect[i] + readyAmounts[i];
            outputsExact &= storage.GetResourceAmount(outputId) == expectedStorageAmount;

            BaseProcessingOutputBufferState buffer = facility.GetOutputBuffer(outputId, false);
            buffersCleared &= buffer != null && buffer.readyAmount == 0;
        }

        bool inputSpent = storage.GetResourceAmount(inputItemId) == inputBefore - amount;
        bool bunkerEmpty = facility.BunkerLoadUnits == 0;
        message = SessionExtractionIndustry.GetProcessingDisplayName(branch)
            + " loaded " + amount
            + ", expected processed units " + expectedProcessedUnits
            + ", completed events " + completedEvents
            + ", ready " + readyTotal
            + " / " + loadMessage + " / " + collectMessage;

        return loaded
            && inputSpent
            && completedEvents >= expectedProcessedUnits
            && bunkerEmpty
            && readyTotal > 0
            && collected
            && outputsExact
            && buffersCleared;
    }

    private static double CalculateTimedProcessingSecondsToProcessUnits(BaseProcessingFacilityState facility, int units)
    {
        if (facility == null || units <= 0)
        {
            return 1d;
        }

        float unitsPerMinute = Mathf.Max(0.1f, facility.processingUnitsPerMinute);
        return Math.Ceiling(units * 60d / unitsPerMinute) + 2d;
    }

    private static bool QueueAndCompleteCascadeOrderForBigTest(
        MetaGameState meta,
        PlayerProgress progress,
        PortStorageState storage,
        CascadeProductionOrderDefinition order,
        int quantity,
        out string message)
    {
        message = "";
        if (meta == null || progress == null || storage == null || order == null)
        {
            message = "Cascade test missing meta, progress, storage or order.";
            return false;
        }

        quantity = Mathf.Max(1, quantity);
        order.Normalize();
        CascadeProductionEstimate estimate = meta.EstimateBaseCascadeOrder(order, quantity);
        if (estimate == null || !estimate.canRun)
        {
            message = estimate != null && !string.IsNullOrWhiteSpace(estimate.blockedReason)
                ? estimate.blockedReason
                : "Cascade estimate blocked.";
            return false;
        }

        progress.baseIndustry ??= new BaseExtractionIndustryState();
        progress.baseIndustry.Normalize();
        int queueCountBefore = progress.baseIndustry.ActiveCascadeQueueCount;
        int[] inputBefore = CaptureStorageAmounts(storage, order.inputs);
        int[] outputBefore = CaptureStorageAmounts(storage, order.outputs);

        long startTicks = Math.Max(DateTime.UtcNow.Ticks, progress.lastProcessUtcTicks);
        progress.lastProcessUtcTicks = startTicks;
        bool queued = meta.TryQueueBaseCascadeOrder(order, quantity, out string queueMessage);
        progress.baseIndustry.Normalize();
        int queueCountAfterQueue = progress.baseIndustry.ActiveCascadeQueueCount;

        bool inputsSpent = StorageAmountsMatchDelta(storage, order.inputs, inputBefore, -quantity);
        bool outputsNotGrantedEarly = StorageAmountsMatchExact(storage, order.outputs, outputBefore);
        int[] outputAfterQueue = CaptureStorageAmounts(storage, order.outputs);

        double secondsToComplete = Math.Ceiling(Math.Max(1d, estimate.bottleneckMinutes * 60d)) + 1d;
        DateTime completeAt = new DateTime(startTicks, DateTimeKind.Utc).AddSeconds(secondsToComplete);
        int completedEvents = queued ? meta.AdvanceRealTimeProcesses(completeAt) : 0;
        progress.baseIndustry.Normalize();

        bool outputsGrantedExactly = StorageAmountsMatchDelta(storage, order.outputs, outputAfterQueue, quantity);
        bool queueCompleted = progress.baseIndustry.ActiveCascadeQueueCount == queueCountBefore;
        bool queuedOneSlot = queueCountAfterQueue == queueCountBefore + 1;

        message = queueMessage
            + " / quantity " + quantity
            + " / advanced " + secondsToComplete.ToString("0.#") + "s"
            + " / completed events " + completedEvents
            + " / input spent " + inputsSpent
            + " / output exact " + outputsGrantedExactly;

        return queued
            && queuedOneSlot
            && inputsSpent
            && outputsNotGrantedEarly
            && completedEvents >= 1
            && outputsGrantedExactly
            && queueCompleted;
    }

    private static int[] CaptureStorageAmounts(PortStorageState storage, List<CascadeItemAmount> items)
    {
        if (items == null) return Array.Empty<int>();

        int[] amounts = new int[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            CascadeItemAmount item = items[i];
            amounts[i] = item != null ? storage.GetResourceAmount(item.itemId) : 0;
        }

        return amounts;
    }

    private static List<CascadeItemAmount> CloneCascadeItemsForTest(List<CascadeItemAmount> items)
    {
        List<CascadeItemAmount> clone = new List<CascadeItemAmount>();
        if (items == null)
        {
            return clone;
        }

        for (int i = 0; i < items.Count; i++)
        {
            CascadeItemAmount item = items[i];
            if (item == null)
            {
                continue;
            }

            clone.Add(new CascadeItemAmount { itemId = item.itemId, amount = item.amount });
        }

        return clone;
    }

    private static bool TopUpAndCompleteRepairDockSlotForTest(
        MetaGameState meta,
        WildWindBaseIslandView island,
        PortStorageState storage,
        int slotIndex,
        int buildingLevel,
        out string shipId,
        out int workStepsRun)
    {
        shipId = "";
        workStepsRun = 0;
        if (meta == null || island == null || storage == null)
        {
            return false;
        }

        if (!island.SelectRepairDockSlotForTests(slotIndex))
        {
            return false;
        }

        for (int guard = 0; guard < 16; guard++)
        {
            RepairDockSlotState slot = meta.GetRepairDockSlot(slotIndex, buildingLevel);
            if (slot == null || !slot.HasWreck)
            {
                return false;
            }

            if (slot.repaired)
            {
                shipId = slot.shipId;
                return workStepsRun > 0;
            }

            List<CascadeItemAmount> stepInputs = meta.GetRepairDockCurrentWorkInputs(slotIndex, buildingLevel);
            for (int i = 0; i < stepInputs.Count; i++)
            {
                CascadeItemAmount input = stepInputs[i];
                if (input == null || string.IsNullOrWhiteSpace(input.itemId) || input.amount <= 0)
                {
                    continue;
                }

                int available = storage.GetResourceAmount(input.itemId);
                if (available < input.amount)
                {
                    storage.AddResource(input.itemId, input.amount - available);
                }
            }

            if (!island.RunSelectedRepairDockWorkForTests())
            {
                return false;
            }

            workStepsRun++;
        }

        RepairDockSlotState finalSlot = meta.GetRepairDockSlot(slotIndex, buildingLevel);
        shipId = finalSlot != null ? finalSlot.shipId : "";
        return finalSlot != null && finalSlot.repaired && workStepsRun > 0;
    }

    private static bool StorageAmountsMatchDelta(PortStorageState storage, List<CascadeItemAmount> items, int[] before, int quantity)
    {
        if (items == null) return true;
        if (before == null || before.Length != items.Count) return false;

        for (int i = 0; i < items.Count; i++)
        {
            CascadeItemAmount item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0) continue;

            int expected = before[i] + item.amount * quantity;
            if (storage.GetResourceAmount(item.itemId) != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool StorageAmountsMatchExact(PortStorageState storage, List<CascadeItemAmount> items, int[] expected)
    {
        if (items == null) return true;
        if (expected == null || expected.Length != items.Count) return false;

        for (int i = 0; i < items.Count; i++)
        {
            CascadeItemAmount item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;
            if (storage.GetResourceAmount(item.itemId) != expected[i])
            {
                return false;
            }
        }

        return true;
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
            Mathf.Max(
                zone.entryPosition.y,
                zone.stormFloorY + SessionExtractionConstants.DefaultSortieEntryAltitudeMeters),
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

        string fuelId = string.IsNullOrWhiteSpace(ship.fuelResourceId) ? "charcoal" : ship.fuelResourceId;
        string claudiumId = string.IsNullOrWhiteSpace(ship.claudiumResourceId) ? "claudium" : ship.claudiumResourceId;
        progress.shipFuelTank.SetResource(fuelId);
        progress.shipClaudiumTank.SetResource(claudiumId);
        ship.fuelStockKg = progress.shipFuelTank.GetAmount(fuelId);
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
            false);
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

        PortStorageState baseStorage = meta != null ? meta.GetCapitalStorageState() : null;
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
            progress.shipFuelTank.SetResource("charcoal");
            progress.shipClaudiumTank.SetResource("claudium");
            progress.shipFuelTank.Add("charcoal", estimate.requiredCoalKg + 50f, 100000f);
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

    private static bool HasStableBaseShipFlightEnvelope(
        ShipAssemblyResult assembly,
        float payloadKg,
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
        float claudiumMaxLiftKg = stats.Get(ShipStatId.ClaudiumMaxLiftKg, 0f);
        float hullLimitKg = stats.Get(ShipStatId.HullMaxTakeoffMassKg, 0f);
        float allowedTakeoffMassKg = Mathf.Min(claudiumMaxLiftKg, hullLimitKg);
        float hullForwardThrustKgf = stats.Get(ShipStatId.HullForwardThrustKgf, 0f);
        float hullCruiseSpeedMS = stats.Get(ShipStatId.HullCruiseReferenceSpeedMS, 0f);
        float thrustN = hullForwardThrustKgf * 9.81f;
        float horizontalAccelerationMS2 = totalMassKg > 0f ? thrustN / totalMassKg : 0f;
        float strategicSpeedMS = Mathf.Max(0f, hullCruiseSpeedMS);

        summary = "mass " + totalMassKg.ToString("F0") + "/" + allowedTakeoffMassKg.ToString("F0") + " kg"
            + ", empty " + emptyMassKg.ToString("F0") + " kg"
            + ", thrust " + hullForwardThrustKgf.ToString("F0") + " kgf"
            + ", accel " + horizontalAccelerationMS2.ToString("F2") + " m/s2"
            + ", strategic cruise " + strategicSpeedMS.ToString("F0") + " m/s.";

        return totalMassKg <= allowedTakeoffMassKg + 0.001f
            && hullForwardThrustKgf > 0f
            && strategicSpeedMS >= minSpeedMS
            && horizontalAccelerationMS2 >= minHorizontalAccelerationMS2
            && IsFinite(strategicSpeedMS);
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

    private void ValidateLoadedSession(string label, BigTestReport report)
    {
        MetaGameState loadedMeta = FindFirstObjectByType<MetaGameState>();
        WildWindGameplayMenu loadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();
        WildWindGameplaySession loadedSession = FindFirstObjectByType<WildWindGameplaySession>();
        WildWindGameplayHud loadedHud = FindFirstObjectByType<WildWindGameplayHud>();

        report.Check(loadedMeta != null && loadedMeta.RuntimeAccountId == GameplaySessionAccountData.DefaultAccountId,
            "MetaGameState after scenario '" + label + "' uses the runtime account id.");
        report.Check(loadedMenu != null,
            "Gameplay menu is ready after session scenario '" + label + "'.");
        report.Check(loadedSession != null && loadedSession.IsReady,
            "GameplaySession is ready after session scenario '" + label + "'.");
        string expectedDockId = loadedMeta != null && loadedMeta.progress != null && !string.IsNullOrWhiteSpace(loadedMeta.progress.currentDockId)
            ? loadedMeta.progress.currentDockId
            : GameplaySessionAccountData.DefaultDockId;
        report.Check(loadedSession != null && loadedSession.CurrentDockId == expectedDockId,
            "GameplaySession knows the current dock after session scenario '" + label + "': " + expectedDockId + ".");
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

    private sealed class BigTestSideEffectSnapshot
    {
        private float timeScale;
        private string normalProgressSavePath;
        private string normalProgressSaveText;
        private bool normalProgressSaveExisted;
        private bool normalProgressSaveTouched;
        private string normalProgressSaveRestoreError;

        public string ActiveSceneName { get; private set; }

        private BigTestSideEffectSnapshot()
        {
        }

        public static BigTestSideEffectSnapshot Capture()
        {
            Scene scene = SceneManager.GetActiveScene();
            string normalSavePath = MetaGameState.GetPersistentProgressSavePathForTests(false);
            bool normalSaveExists = File.Exists(normalSavePath);
            return new BigTestSideEffectSnapshot
            {
                timeScale = Time.timeScale,
                ActiveSceneName = scene.IsValid() ? scene.name : "",
                normalProgressSavePath = normalSavePath,
                normalProgressSaveExisted = normalSaveExists,
                normalProgressSaveText = normalSaveExists ? File.ReadAllText(normalSavePath, Encoding.UTF8) : ""
            };
        }

        public void RestorePrefsAndTimeScale()
        {
            Time.timeScale = timeScale;
            RestoreNormalProgressSaveIfNeeded();
        }

        public void AssertRestored(BigTestReport report)
        {
            Scene activeScene = SceneManager.GetActiveScene();

            report.Check(Approximately(Time.timeScale, timeScale, 0.001f),
                "Time.timeScale Р Р†Р С•РЎРѓРЎРѓРЎвЂљР В°Р Р…Р С•Р Р†Р В»Р ВµР Р… Р С—Р С•РЎРѓР В»Р Вµ Р В±Р С•Р В»РЎРЉРЎв‚¬Р С•Р С–Р С• РЎвЂљР ВµРЎРѓРЎвЂљР В°: " + Time.timeScale.ToString("0.###") + ".");
            report.Check(string.IsNullOrWhiteSpace(ActiveSceneName) || activeScene.name == ActiveSceneName,
                "Р С’Р С”РЎвЂљР С‘Р Р†Р Р…Р В°РЎРЏ РЎРѓРЎвЂ Р ВµР Р…Р В° Р Р†Р С•РЎРѓРЎРѓРЎвЂљР В°Р Р…Р С•Р Р†Р В»Р ВµР Р…Р В° Р С—Р С•РЎРѓР В»Р Вµ Р В±Р С•Р В»РЎРЉРЎв‚¬Р С•Р С–Р С• РЎвЂљР ВµРЎРѓРЎвЂљР В°: " + activeScene.name + ".");
            report.Check(!normalProgressSaveTouched && string.IsNullOrWhiteSpace(normalProgressSaveRestoreError),
                normalProgressSaveTouched
                    ? "Big Test unexpectedly touched the player's real persistent progress save and restored it: " + normalProgressSavePath + "."
                    : "Big Test left the player's real persistent progress save untouched: " + normalProgressSavePath + ".");
        }

        private void RestoreNormalProgressSaveIfNeeded()
        {
            if (string.IsNullOrWhiteSpace(normalProgressSavePath))
            {
                return;
            }

            try
            {
                bool existsNow = File.Exists(normalProgressSavePath);
                string currentText = existsNow ? File.ReadAllText(normalProgressSavePath, Encoding.UTF8) : "";
                if (existsNow == normalProgressSaveExisted && string.Equals(currentText, normalProgressSaveText, StringComparison.Ordinal))
                {
                    return;
                }

                normalProgressSaveTouched = true;
                string folder = Path.GetDirectoryName(normalProgressSavePath);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string tempPath = normalProgressSavePath + ".bigtest-restore";
                if (normalProgressSaveExisted)
                {
                    File.WriteAllText(tempPath, normalProgressSaveText, Encoding.UTF8);
                    if (File.Exists(normalProgressSavePath))
                    {
                        File.Delete(normalProgressSavePath);
                    }

                    File.Move(tempPath, normalProgressSavePath);
                }
                else if (File.Exists(normalProgressSavePath))
                {
                    File.Delete(normalProgressSavePath);
                }
            }
            catch (Exception exception)
            {
                normalProgressSaveRestoreError = exception.Message;
            }
        }
    }
    private void ValidateMaintainability(BigTestReport report)
    {
        report.Section("РњРµС‚РѕРґРёРєР° СЃРѕРїСЂРѕРІРѕР¶РґРµРЅРёСЏ");
        ValidateLegacyNearestWeaponApiRemoved(report);
        ValidateArmorDegradationRemoved(report);
        ValidateSessionInputUsesInputSystem(report);
        ValidateRuntimeGunFallbackRemoved(report);
        ValidateCoreTacticalPrototypeTransfer(report);
        ValidateNoUnauthorizedEditorTools(report);
        report.Info("PROJECT RULE: no ad-hoc editor tools. Only the Big Test menu is allowed.");
        report.Info("РљРѕРіРґР° РїРѕСЏРІР»СЏРµС‚СЃСЏ РЅРѕРІР°СЏ РєСЂСѓРїРЅР°СЏ РјРµС…Р°РЅРёРєР°, РґРѕР±Р°РІР»СЏРµРј СЃСЋРґР° РѕС‚РґРµР»СЊРЅС‹Р№ СЂР°Р·РґРµР»: РєРѕРЅС„РёРі, runtime-СЃРѕСЃС‚РѕСЏРЅРёРµ, СЃРёРјСѓР»СЏС†РёСЏ, РіСЂР°РЅРёС‡РЅС‹Рµ СѓСЃР»РѕРІРёСЏ, РїСЂРѕРёР·РІРѕРґРёС‚РµР»СЊРЅРѕСЃС‚СЊ Рё РїРѕР»СЊР·РѕРІР°С‚РµР»СЊСЃРєРёР№ РјР°СЂС€СЂСѓС‚.");
        report.Info("РњРѕРґСѓР»СЊРЅС‹Рµ С‚РµСЃС‚С‹ РѕСЃС‚Р°СЋС‚СЃСЏ СЂСЏРґРѕРј СЃРѕ СЃРІРѕРµР№ РѕР±Р»Р°СЃС‚СЊСЋ; Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚ РѕР±СЏР·Р°РЅ РїСЂРѕРІРµСЂСЏС‚СЊ РєР»СЋС‡РµРІС‹Рµ РёРЅРІР°СЂРёР°РЅС‚С‹ СЃРµСЃСЃРёРѕРЅРЅРѕР№ СЃР±РѕСЂРєРё.");
        report.Info("Р•СЃР»Рё С‚РµСЃС‚ СЂСѓРіР°РµС‚СЃСЏ WARN, СЌС‚Рѕ РЅРµ Р±Р»РѕРєРµСЂ, РЅРѕ РїРѕРІРѕРґ Р·Р°РїРёСЃР°С‚СЊ СЂРµС€РµРЅРёРµ: РѕСЃС‚Р°РІРёС‚СЊ РґРѕРїСѓСЃРє, СѓР¶РµСЃС‚РѕС‡РёС‚СЊ РµРіРѕ РёР»Рё РїСЂРµРІСЂР°С‚РёС‚СЊ РІ FAIL.");
        report.Info("РџСЂР°РІРёР»Рѕ РїСЂРѕРµРєС‚Р°: РЅРѕРІР°СЏ С„РёС‡Р° РЅРµ СЃС‡РёС‚Р°РµС‚СЃСЏ РїСЂРёРЅСЏС‚РѕР№, РїРѕРєР° РµС‘ РіР»Р°РІРЅС‹Р№ СЃС†РµРЅР°СЂРёР№ РЅРµ РїРѕРїР°Р» РІ Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚.");
        report.Pass("Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ СЃС„РѕСЂРјРёСЂРѕРІР°Р» СЏРІРЅС‹Р№ С‚РµРєСЃС‚РѕРІС‹Р№ РїСЂРѕС‚РѕРєРѕР», РєРѕС‚РѕСЂС‹Р№ РјРѕР¶РЅРѕ СЂР°СЃС€РёСЂСЏС‚СЊ РґР°Р»СЊС€Рµ.");
    }

    private static void ValidateLegacyNearestWeaponApiRemoved(BigTestReport report)
    {
#if UNITY_EDITOR
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string oldMiningApi = "TryShoot" + "NearestMiningRock";
        string oldLeviathanShotApi = "TryShoot" + "Leviathan";
        string oldHarpoonApi = "TryFireHarpoonAt" + "NearestLeviathan";
        bool removed = !shipPhysicsText.Contains(oldMiningApi) &&
            !shipPhysicsText.Contains(oldLeviathanShotApi) &&
            !shipPhysicsText.Contains(oldHarpoonApi);
        report.Check(removed, "Legacy nearest-target weapon APIs are absent from ShipPhysics.");
#else
        report.Check(true, "Legacy nearest-target weapon API source scan is editor-only and skipped in player builds.");
#endif
    }

    private static void ValidateSessionInputUsesInputSystem(BigTestReport report)
    {
#if UNITY_EDITOR
        string[] sourcePaths =
        {
            "Assets/Scripts/Systems/ShipPhysics.cs",
            "Assets/Scripts/Session/WildWindSessionCameraController.cs",
            "Assets/Scripts/Session/WildWindControlSettings.cs",
            "Assets/Scripts/Session/WildWindFlightControlBridge.cs",
            "Assets/Scripts/UI/WildWindGameplayMenu.cs"
        };

        string[] legacyInputTokens =
        {
            "ENABLE_LEGACY_INPUT_MANAGER",
            "Input.mousePosition",
            "Input.mouseScrollDelta",
            "Input.GetAxisRaw(",
            "Input.GetKey(",
            "Input.GetKeyDown(",
            "Input.GetMouseButton(",
            "Input.GetMouseButtonDown(",
            "Input.GetMouseButtonUp("
        };

        List<string> leftovers = new List<string>();
        for (int pathIndex = 0; pathIndex < sourcePaths.Length; pathIndex++)
        {
            string path = sourcePaths[pathIndex];
            string text = ReadProjectText(path);
            for (int tokenIndex = 0; tokenIndex < legacyInputTokens.Length; tokenIndex++)
            {
                string token = legacyInputTokens[tokenIndex];
                if (text.Contains(token))
                {
                    leftovers.Add(path + ":" + token);
                }
            }
        }

        report.Check(leftovers.Count == 0,
            leftovers.Count == 0
                ? "Session gameplay input uses the Input System API without legacy UnityEngine.Input fallbacks."
                : "Session gameplay input still reads legacy UnityEngine.Input tokens: " + string.Join(", ", leftovers));
#else
        report.Check(true, "Session Input System source scan is editor-only and skipped in player builds.");
#endif
    }

    private static void ValidateRuntimeGunFallbackRemoved(BigTestReport report)
    {
#if UNITY_EDITOR
        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string sessionSceneText = ReadProjectText("Assets/Scenes/WildWindSessionScene.unity");
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
                sessionSceneText.Contains(fallbackTokens[i]))
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

    private static void ValidateCoreTacticalPrototypeTransfer(BigTestReport report)
    {
        ValidateCoreTacticalMotorArrivalBraking(report);
        ValidateCoreTacticalOreTargetingRules(report);
        ValidateCoreTacticalLeviathanBehavior(report);
        ValidateCoreTacticalMiningEquipmentGates(report);
        ValidateLowGradeOreConcentrateCargo(report);
        ValidateRawCloudCondensateCargo(report);
        ValidateCoreTacticalAutomatonWreckSalvage(report);
        ValidateCoreTacticalHarpoonBehavior(report);
        ValidateCoreTacticalMissionObjectiveGate(report);

#if UNITY_EDITOR
        string bootstrapText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalPrototypeBootstrap.cs");
        string sortieText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCombatSortieController.cs");
        string shipMotorText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalShipMotor.cs");
        string fleetText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalFleetController.cs");
        string cameraRigText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCameraRig.cs");
        string hudText = ReadProjectText("Assets/Scripts/UI/WildWindGameplayHud.cs");
        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string playerProgressText = ReadProjectText("Assets/Scripts/Meta/PlayerProgress.cs");
        string coreTacticalDamageText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalDamageModel.cs");
        string coreTacticalOreMiningText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalOreMining.cs");
        string leviathanText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalLeviathan.cs");
        string coreTacticalUtilityBeamText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalUtilityBeamVisual.cs");
        string coreTacticalSiphonIntakeText = coreTacticalUtilityBeamText;
        string sessionConfigKorshunComponentsText = ReadProjectText("Assets/Scripts/Data/SessionConfigKorshunComponents.cs");
        string sessionConfigDatabaseText = ReadProjectText("Assets/Scripts/Data/SessionConfigDatabase.cs");
        string barbetAuxiliaryCsvText = ReadProjectText("Assets/Data/Config/Barbet_auxiliary_packages.csv");

        bool loadoutInstallersExposed =
            bootstrapText.Contains("ConfigureFrigateAutocannonLoadout") &&
            bootstrapText.Contains("ConfigureCruiserArtilleryLoadout") &&
            bootstrapText.Contains("ConfigureBattleshipFullLoadout") &&
            bootstrapText.Contains("RetargetLoadout");

        bool mainSortieUsesDevelopmentShip =
            sortieText.Contains("GetSelectedDevelopmentDockShipSlot") &&
            sortieText.Contains("BuildPlayerRuntimeProfile") &&
            sortieText.Contains("ResolveSelectedShipEntry");

        bool mainSortieInstallsTransferredWeapons =
            sortieText.Contains("ConfigureRuntimeLoadout") &&
            sortieText.Contains("ConfigureFrigateAutocannonLoadout") &&
            sortieText.Contains("ConfigureCruiserArtilleryLoadout") &&
            sortieText.Contains("ConfigureBattleshipFullLoadout");

        bool battleshipPrototypeEffectsKept =
            bootstrapText.Contains("ConfigureBattleshipMissileLauncher") &&
            bootstrapText.Contains("ConfigureBattleshipMachineGunAura") &&
            bootstrapText.Contains("CoreTacticalMissileGuidanceMode.PredictedIntercept") &&
            bootstrapText.Contains("CoreTacticalMissileGuidanceMode.DirectChase");

        bool enemyLoadoutsTargetPlayer =
            sortieText.Contains("CoreTacticalCombatTeam.Friendly") &&
            sortieText.Contains("SpawnEnemyCruisers");

        bool dockPrimaryBattleButtonStartsCoreCombat =
            hudText.Contains("Dock Core Tactical Combat Sortie") &&
            hudText.Contains("HandleMetaDockCoreCombatSortie, out metaDockCoreCombatButton") &&
            hudText.Contains("RunCoreCombatDockSortieForTests") &&
            !hudText.Contains("Dock Enter Core Combat") &&
            !hudText.Contains("Dock Manual Quick Sortie") &&
            !hudText.Contains("Dock Run Ordinary Mission") &&
            !hudText.Contains("Dock Mission Panel") &&
            !hudText.Contains("HandleMetaDockQuickBattle") &&
            !hudText.Contains("HandleMetaDockManualSortie") &&
            !hudText.Contains("HandleMetaDockRunSelectedMission");

        bool coreCombatConsumesDockSortie =
            metaText.Contains("Core combat blocked: buy or select a dock ship first.") &&
            metaText.Contains("TryEnterFlightForSessionSortie(bool useDockShipLaunch = false)") &&
            metaText.Contains("BeginSessionExtractionSortie(CreateCoreTacticalIntroCombatSortieDefinition(), true)") &&
            metaText.Contains("dockSlot.sortiesRemaining = Mathf.Max(0, dockSlot.sortiesRemaining - 1);") &&
            metaText.Contains("core_tactical_sortie_started");

        bool transferReady = loadoutInstallersExposed &&
            mainSortieUsesDevelopmentShip &&
            mainSortieInstallsTransferredWeapons &&
            battleshipPrototypeEffectsKept &&
            enemyLoadoutsTargetPlayer &&
            dockPrimaryBattleButtonStartsCoreCombat &&
            coreCombatConsumesDockSortie;

        report.Check(transferReady,
            transferReady
                ? "Core Tactical prototype weapon loadouts are exposed and wired into the main combat sortie."
                : "Core Tactical prototype weapon transfer is incomplete: missing loadout installers, selected-ship binding, main-sortie weapon install, battleship missile/machine-gun effects, enemy targeting, dock battle button binding, or dock sortie consumption.");

        bool combatHudShowsCurrentAndMaxSpeed =
            sortieText.Contains("FormatShipSpeedLine") &&
            sortieText.Contains("ship.maxForwardSpeedMS") &&
            sortieText.Contains("DrawSelectedShipStats") &&
            sortieText.Contains("DrawMissionHud");
        report.Check(combatHudShowsCurrentAndMaxSpeed,
            combatHudShowsCurrentAndMaxSpeed
                ? "Core Tactical HUD shows current/max ship speed so a maneuvering Korshun cannot look like a stale 34 m/s profile."
                : "Core Tactical HUD must show current/max ship speed, not only current velocity.");

        bool infiniteWeaponGroups =
            bootstrapText.Contains("Torpedoes = 6") &&
            bootstrapText.Contains("public CoreTacticalWeaponGroup weaponGroup = CoreTacticalWeaponGroup.Missiles;") &&
            bootstrapText.Contains("CanFireWeapon(weaponGroup)") &&
            bootstrapText.Contains("SetRuntimeWeaponGroupActive") &&
            bootstrapText.Contains("ClearRuntimeWeaponGroups") &&
            !bootstrapText.Contains("TryConsumeWeapon" + "Ammo") &&
            !bootstrapText.Contains("GetRemaining(") &&
            !bootstrapText.Contains("GetCapacity(") &&
            sortieText.Contains("CoreTacticalWeaponGroup.Torpedoes => \"TRP\"") &&
            sortieText.Contains("torpedoLike ? CoreTacticalWeaponGroup.Torpedoes : CoreTacticalWeaponGroup.Missiles") &&
            sortieText.Contains("weaponControl.ClearRuntimeWeaponGroups();") &&
            sortieText.Contains("weaponControl?.SetRuntimeWeaponGroupActive(weaponGroup)") &&
            !sortieText.Contains("Ammo" + "Capacity") &&
            !sortieText.Contains("ammo" + "Capacity") &&
            bootstrapText.Contains("manualLaunchOnly") &&
            bootstrapText.Contains("TryLaunchManualFan") &&
            bootstrapText.Contains("ReloadCooldownRemainingSeconds") &&
            bootstrapText.Contains("SetReloadCooldownRemainingSecondsForTests") &&
            bootstrapText.Contains("manualAimSectorDegrees") &&
            bootstrapText.Contains("IsDirectionInsideManualSector") &&
            bootstrapText.Contains("automaticBurstUsesChaoticCloud") &&
            bootstrapText.Contains("GetChaoticCloudShotDirection") &&
            bootstrapText.Contains("GetAutomaticBurstTrackedCenterDirection") &&
            bootstrapText.Contains("GetAutomaticBurstTrackedCenterDirectionForTests") &&
            bootstrapText.Contains("GetUnguidedChaosDirection") &&
            bootstrapText.Contains("unguidedChaosAmplitudeRad") &&
            bootstrapText.Contains("ExplodeAt(transform.position)") &&
            bootstrapText.Contains("DetonationSensitivityRadiusMeters") &&
            bootstrapText.Contains("ResolveDetonationSensitivityRadiusForTests") &&
            bootstrapText.Contains("ApplyExplosionDamageForTests") &&
            sortieText.Contains("BeginTorpedoAim") &&
            sortieText.Contains("DrawTorpedoAimSector") &&
            sortieText.Contains("BuildWeaponCooldownText") &&
            sortieText.Contains("FormatWeaponCooldownSeconds") &&
            sortieText.Contains("GetWeaponCooldownTextForTests") &&
            sortieText.Contains("automaticBurstUsesChaoticCloud = rocketSalvoLike") &&
            sortieText.Contains("automaticBurstShotIntervalJitterSeconds") &&
            sortieText.Contains("int launcherCount = Mathf.Clamp(requestedLauncherCount, 1, 4);") &&
            sortieText.Contains("launcher.sideSign = i == 0 ? -1 : 1") &&
            sortieText.Contains("Mathf.CeilToInt(Mathf.Max(1, projectilesPerSalvo) / (float)launcherCount)") &&
            fleetText.Contains("TacticalPointerInputBlocked");
        report.Check(infiniteWeaponGroups,
            infiniteWeaponGroups
                ? "Core Tactical weapons use active infinite weapon groups with reload/cooldown-only HUD countdowns; TRP keeps player-only side-sector aiming while NURS uses chaotic cloud salvos instead of a torpedo fan."
                : "Core Tactical weapons must not keep runtime shot stock, capacity HUD checks, phantom MSL stock, old shot-consumption gates, or hidden reloads without HUD countdown text.");

        bool coreTacticalCommandPointPrecise =
            fleetText.Contains("BeginDraftCommand(point)") &&
            fleetText.Contains("HandleCommandInput();") &&
            fleetText.Contains("ProcessCommandPointer(") &&
            fleetText.Contains("ProcessCommandPointerForTests") &&
            fleetText.Contains("GetCommandPointMarkerCenterForTests") &&
            fleetText.Contains("mouse.rightButton.wasPressedThisFrame") &&
            fleetText.Contains("mouse.rightButton.isPressed") &&
            fleetText.Contains("mouse.rightButton.wasReleasedThisFrame") &&
            fleetText.Contains("TryReadInputSystemScreenPosition(out Vector2 screenPosition)") &&
            fleetText.Contains("return TryReadInputSystemScreenPosition(out position);") &&
            fleetText.Contains("rightMouseDownTracked = TryBeginScreenCommand(screenPosition)") &&
            fleetText.Contains("CancelCommandDraft") &&
            fleetText.Contains("TryBeginScreenCommand") &&
            fleetText.Contains("UpdateDraftTargetFromScreenProjection") &&
            fleetText.Contains("UpdateDraftPreview();") &&
            fleetText.Contains("draftTarget = point") &&
            fleetText.Contains("markerCenter = GetCommandPointMarkerCenterForTests();") &&
            !fleetText.Contains("MaintainCommandInputState") &&
            !fleetText.Contains("HandleCommandGuiEvent") &&
            !fleetText.Contains("ResolveCommandEventScreenPosition") &&
            !fleetText.Contains("EventType.MouseDown") &&
            !fleetText.Contains("EventType.MouseDrag") &&
            !fleetText.Contains("EventType.MouseUp") &&
            !fleetText.Contains("currentEvent.Use") &&
            !fleetText.Contains("Event.current") &&
            !fleetText.Contains("GuiToScreenPosition") &&
            !fleetText.Contains("TryReadGuiMousePosition") &&
            !fleetText.Contains("CaptureGuiMousePosition") &&
            !fleetText.Contains("lastGuiMouse") &&
            !fleetText.Contains("TryBuildCommandDragBasis") &&
            !fleetText.Contains("TryProjectCommandDragAxis") &&
            !fleetText.Contains("UpdateDraftTargetFromScreenDelta") &&
            !fleetText.Contains("commandPressTarget") &&
            !fleetText.Contains("commandDragWorldPerPixelX") &&
            !fleetText.Contains("commandDragWorldPerPixelY") &&
            !fleetText.Contains("CommandDragBasisProbePixels") &&
            !fleetText.Contains("CommandDragMaxMetersPerPixel") &&
            fleetText.Contains("position = mouse.position.ReadValue();") &&
            fleetText.Contains("return IsScreenPositionInsideGameView(position);") &&
            fleetText.Contains("IsScreenPositionInsideGameView") &&
            !fleetText.Contains("SyncInputCameraTransformForProjection") &&
            !fleetText.Contains("inputCameraRig.ApplyCurrentTransformForInput()") &&
            fleetText.Contains("TacticalPointerInputBlocked") &&
            fleetText.Contains("|| !HasSelectedShips()") &&
            fleetText.Contains("HasSelectedShipsForInput => HasSelectedShips()") &&
            fleetText.Contains("IsCommandDraftActiveForInput") &&
            fleetText.Contains("SetInputCamera(Camera camera)") &&
            fleetText.Contains("InputCameraForTests") &&
            fleetText.Contains("TryProjectScreenPointToCommandPlaneForTests") &&
            fleetText.Contains("TryBuildCameraRayFromScreenPointForTests") &&
            fleetText.Contains("TryProjectCameraScreenPointToCommandPlane") &&
            fleetText.Contains("TryBuildCameraRayFromScreenPoint") &&
            fleetText.Contains("float normalizedX = viewportX * 2f - 1f") &&
            fleetText.Contains("camera.fieldOfView * 0.5f * Mathf.Deg2Rad") &&
            fleetText.Contains("ray = new Ray(camera.transform.position, worldDirection.normalized)") &&
            fleetText.Contains("IsScreenPointInsideCamera(mainCamera, screenPosition)") &&
            fleetText.Contains("SetCommandGridWorldAnchor(Vector3 anchor)") &&
            fleetText.Contains("commandGridWorldAnchorSet ? commandGridWorldAnchor : GetFleetCenter()") &&
            fleetText.Contains("ScreenPointOutsideCameraTolerancePixels = 64f") &&
            fleetText.Contains("GetCommandGridCenter") &&
            fleetText.Contains("gridMeshFilter.transform.position = GetCommandGridCenter(safeStep)") &&
            fleetText.Contains("CommandGridTargetScreenPixels = 1.6f") &&
            fleetText.Contains("CommandGridMaxLineWidthStepFraction = 0.07f") &&
            fleetText.Contains("ResolveGridLineWidth(safeStep)") &&
            fleetText.Contains("EstimateCommandPlaneMetersPerPixel") &&
            fleetText.Contains("RenderQueue.Transparent") &&
            fleetText.Contains("Core Tactical Command Point Marker") &&
            fleetText.Contains("Core Tactical Command Point Center") &&
            fleetText.Contains("UpdateCommandPointMarker();") &&
            fleetText.Contains("ship.SetCommand(") &&
            !fleetText.Contains("rightMouseCommandGesture") &&
            !fleetText.Contains("draftHoldFacingMode") &&
            !fleetText.Contains("Core Tactical Command Ghost") &&
            !fleetText.Contains("Shift+RMB") &&
            !fleetText.Contains("draftTarget = currentPoint") &&
            !fleetText.Contains("draftTarget = releasePoint") &&
            sortieText.Contains("cameraRig.ApplyCurrentTransformForInput();") &&
            sortieText.Contains("ApplyTacticalCameraAfterFleetSpawn();") &&
            sortieText.Contains("fleet.SetCommandGridWorldAnchor(missionCenter)") &&
            sortieText.Contains("CreateMissionBoundaryMarker();") &&
            sortieText.Contains("Core Tactical Mission Boundary") &&
            sortieText.Contains("camera.usePhysicalProperties = false") &&
            sortieText.Contains("camera.lensShift = Vector2.zero") &&
            sortieText.Contains("camera.clearFlags = CameraClearFlags.Skybox") &&
            !sortieText.Contains("camera.clearFlags = CameraClearFlags.SolidColor") &&
            sortieText.Contains("camera.ResetProjectionMatrix();") &&
            sortieText.Contains("camera.ResetAspect();") &&
            sortieText.Contains("fleet.SetInputCamera(camera);") &&
            sortieText.Contains("CoreTacticalFleetController.TryProjectCameraScreenPointToCommandPlane") &&
            bootstrapText.Contains("fleet.SetInputCamera(camera)") &&
            bootstrapText.Contains("camera.clearFlags = CameraClearFlags.Skybox") &&
            !bootstrapText.Contains("camera.clearFlags = CameraClearFlags.SolidColor") &&
            cameraRigText.Contains("fleet.SetInputCamera(targetCamera)") &&
            cameraRigText.Contains("ApplyCurrentTransformForInput") &&
            cameraRigText.Contains("float scrollNotches = NormalizeScrollNotches(mouse.scroll.ReadValue().y)") &&
            cameraRigText.Contains("ApplyWheelZoom(scrollNotches, mousePosition)") &&
            cameraRigText.Contains("private static float NormalizeScrollNotches(float rawScroll)") &&
            !cameraRigText.Contains("private void OnGUI") &&
            !cameraRigText.Contains("EventType.ScrollWheel") &&
            !cameraRigText.Contains("ResolveScrollEventScreenPosition") &&
            !cameraRigText.Contains("NormalizeGuiScrollNotches") &&
            !cameraRigText.Contains("GuiToScreenPosition") &&
            !cameraRigText.Contains("currentEvent.Use") &&
            cameraRigText.Contains("TryReadInputScreenPosition") &&
            !cameraRigText.Contains("fleet.TryReadGameViewMousePositionForInput") &&
            cameraRigText.Contains("ApplyWheelZoom(float scrollNotches, Vector2 mousePosition)") &&
            cameraRigText.Contains("zoomSmoothTimeSeconds = 0.16f") &&
            cameraRigText.Contains("targetCamera.ResetWorldToCameraMatrix();") &&
            cameraRigText.Contains("targetCamera.ResetProjectionMatrix();") &&
            cameraRigText.Contains("targetDistanceMeters") &&
            cameraRigText.Contains("StopSmoothZoomForInput") &&
            cameraRigText.Contains("zoomDistanceVelocity = 0f") &&
            cameraRigText.Contains("UpdateSmoothZoom(Time.unscaledDeltaTime)") &&
            cameraRigText.Contains("Mathf.SmoothDamp(") &&
            cameraRigText.Contains("KeepSmoothZoomAnchorUnderMouse") &&
            cameraRigText.Contains("TryProjectScreenPointToCommandPlane(") &&
            cameraRigText.Contains("smoothZoomScreenAnchor") &&
            cameraRigText.Contains("smoothZoomWorldAnchor") &&
            cameraRigText.Contains("CalculateCameraPose(focusPoint, safeDistance") &&
            cameraRigText.Contains("CancelSmoothZoomAnchor") &&
            cameraRigText.Contains("Vector3 correction = smoothZoomWorldAnchor - anchorAfterZoom") &&
            cameraRigText.Contains("focusPoint += correction") &&
            cameraRigText.Contains("CoreTacticalFleetController.TryProjectCameraScreenPointToCommandPlane") &&
            cameraRigText.Contains("!HasFleetCommandDraft()") &&
            cameraRigText.Contains("!HasSelectedFleetShips()") &&
            cameraRigText.Contains("fleet.HasSelectedShipsForInput");
        report.Check(coreTacticalCommandPointPrecise,
            coreTacticalCommandPointPrecise
                ? "Core Tactical RMB movement commands are driven from Update/Input System screen coordinates, not IMGUI events, and project through the already-rendered tactical camera without mutating it during the click."
                : "Core Tactical RMB movement commands must use Update/Input System screen coordinates with no IMGUI Event.current/EventType/GUIMouse fallback, and must not move/freeze the tactical camera while resolving a click.");

        bool commandProjectionProbeOk = ValidateCoreTacticalCommandProjectionProbe(out string commandProjectionDetails);
        report.Check(commandProjectionProbeOk,
            commandProjectionProbeOk
                ? "Core Tactical simulated RMB clicks place the command marker exactly on the camera ray/command-plane hit across zoom levels: " + commandProjectionDetails
                : "Core Tactical simulated RMB clicks must keep screen click, projected command point and marker center aligned across zoom levels: " + commandProjectionDetails);

        bool coreTacticalProjectileTypesSeparated =
            bootstrapText.Contains("CoreTacticalProjectileDetonationMode.DirectImpact") &&
            bootstrapText.Contains("DirectImpactMissRangeMultiplier = 1.5f") &&
            bootstrapText.Contains("TryApplyDirectDamageAt") &&
            sortieText.Contains("GetPackageDetonationMode") &&
            sortieText.Contains("battery.detonationMode = detonationMode");
        report.Check(coreTacticalProjectileTypesSeparated,
            coreTacticalProjectileTypesSeparated
                ? "Core Tactical AP shells use direct impact and quietly expire at 1.5x range while HE shells keep air-burst splash behavior."
                : "Core Tactical AP/HE projectile behavior is not separated cleanly or AP miss lifetime is not capped at 1.5x range.");

        bool coreTacticalStrategicDamageModel =
            coreTacticalDamageText.Contains("CoreTacticalDamageProfile") &&
            coreTacticalDamageText.Contains("CoreTacticalDamageType") &&
            coreTacticalDamageText.Contains("CoreTacticalResistanceSet") &&
            coreTacticalDamageText.Contains("Kinetic") &&
            coreTacticalDamageText.Contains("Thermal") &&
            coreTacticalDamageText.Contains("Chemical") &&
            coreTacticalDamageText.Contains("Explosive") &&
            coreTacticalDamageText.Contains("effectiveResistancePercent") &&
            coreTacticalDamageText.Contains("fireDamagePerSecondMaxHealthFraction = 0.005f") &&
            coreTacticalDamageText.Contains("fireDurationSeconds = 15f") &&
            coreTacticalDamageText.Contains("emergencyTeamActivationDelaySeconds = 1f") &&
            coreTacticalDamageText.Contains("emergencyTeamActiveSeconds = 8f") &&
            coreTacticalDamageText.Contains("ResolveFireSectorCount") &&
            coreTacticalDamageText.Contains("ScheduleEmergencyTeam") &&
            coreTacticalDamageText.Contains("RepairAllMalfunctions") &&
            bootstrapText.Contains("CoreTacticalDamageRequest.Create") &&
            bootstrapText.Contains("CoreTacticalDamageRequest.Kinetic") &&
            coreTacticalOreMiningText.Contains("CoreTacticalDamageRequest.Chemical") &&
            coreTacticalOreMiningText.Contains("CoreTacticalDamageRequest.Thermal") &&
            leviathanText.Contains("CoreTacticalDamageRequest.Kinetic") &&
            bootstrapText.Contains("missileDamageType") &&
            bootstrapText.Contains("shellDamageType") &&
            sortieText.Contains("ConfigureRuntimeDamageProfile") &&
            sortieText.Contains("profile.structureHp") &&
            sortieText.Contains("profile.resistances") &&
            sortieText.Contains("FormatDamageResistances") &&
            shipMotorText.Contains("damageMobilityMultiplier") &&
            bootstrapText.Contains("CoreTacticalWeaponVisualMaterialUtility") &&
            bootstrapText.Contains("CreateVisibleMaterial") &&
            bootstrapText.Contains("CreateTransparentMaterial") &&
            bootstrapText.Contains("CreateVertexColorTransparentMaterial") &&
            bootstrapText.Contains("ApplyVisibleColor") &&
            bootstrapText.Contains("ResolveVisibleColor") &&
            bootstrapText.Contains("ApplyMissileRendererColor") &&
            bootstrapText.Contains("ResolveMaterialVisibleColor") &&
            bootstrapText.Contains("ApplyMissileMaterialColor") &&
            bootstrapText.Contains("ConfigureMissileTrailMaterial") &&
            bootstrapText.Contains("MinTracerGlowWidthMeters") &&
            bootstrapText.Contains("MinBurstVisualRadiusMeters") &&
            bootstrapText.Contains("BurstVisualLifetimeSeconds") &&
            sortieText.Contains("ExplosiveRadiusReferenceMassKg = 50f") &&
            sortieText.Contains("ExplosiveRadiusReferenceMeters = 20f") &&
            sortieText.Contains("ExplosiveRadiusMassExponent = 0.5f") &&
            sortieText.Contains("ResolveExplosiveSplashRadiusMeters") &&
            sessionConfigKorshunComponentsText.Contains("explosiveKg") &&
            sessionConfigDatabaseText.Contains("LoadCoreTacticalBalance") &&
            sessionConfigDatabaseText.Contains("explosiveRadiusReferenceMassKg") &&
            sessionConfigDatabaseText.Contains("explosiveRadiusReferenceMeters") &&
            sessionConfigDatabaseText.Contains("explosiveRadiusMassExponent") &&
            bootstrapText.Contains("CreateVertexColorTransparentMaterial(color)") &&
            bootstrapText.Contains("sharedBurstMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial") &&
            bootstrapText.Contains("EmissionColorPropertyId") &&
            bootstrapText.Contains("RenderQueue.Transparent") &&
            bootstrapText.Contains("_SURFACE_TYPE_TRANSPARENT");
        report.Check(coreTacticalStrategicDamageModel,
            coreTacticalStrategicDamageModel
                ? "Core Tactical damage uses four damage types with percent resistances, resistance ignore, fire sectors, emergency team, engine mobility damage, glow-width projectile tracers and shared non-black burst/weapon visual materials."
                : "Core Tactical damage must use kinetic/thermal/chemical/explosive resistances instead of old armor/penetration shortcuts, while preserving fire sectors, emergency team, engine mobility damage, tracer glow-width safeguards and shared non-black burst/weapon visual color safeguards.");

        bool coreTacticalCombatLoadoutVisualBinding =
            bootstrapText.Contains("visualLauncherRole") &&
            bootstrapText.Contains("TryBindMissileLauncher(sideSign, visualLauncherRole") &&
            bootstrapText.Contains("TryBindUtilityModule") &&
            bootstrapText.Contains("Korshun_CombatAux_Magnet_Left") &&
            bootstrapText.Contains("Korshun_CombatAux_Magnet_Right") &&
            bootstrapText.Contains("Korshun_CombatAux_GasSiphon_Left") &&
            bootstrapText.Contains("Korshun_CombatAux_GasSiphon_Right") &&
            bootstrapText.Contains("Barbet_CombatSmall_GasSiphon_Left") &&
            bootstrapText.Contains("Barbet_CombatSmall_GasSiphon_Right") &&
            bootstrapText.Contains("Barbet_CombatSmall_Magnet_Left") &&
            bootstrapText.Contains("Barbet_CombatSmall_Magnet_Right") &&
            bootstrapText.Contains("Korshun_CombatAux_RepairBeam_Left") &&
            bootstrapText.Contains("Korshun_CombatAux_RepairBeam_Right") &&
            bootstrapText.Contains("Korshun_CombatAux_HackingDish_Left") &&
            bootstrapText.Contains("Korshun_CombatAux_HackingDish_Right") &&
            bootstrapText.Contains("CreateVertexColorTransparentMaterial(color)") &&
            sortieText.Contains("ApplyImportedShipCombatLoadoutVisual") &&
            sortieText.Contains("ApplyImportedKorshunCombatLoadoutVisual") &&
            sortieText.Contains("ApplyImportedBarbetCombatSmallVisual") &&
            sortieText.Contains("Korshun_CombatAux_Magnet_Left") &&
            sortieText.Contains("Korshun_CombatAux_Magnet_Right") &&
            sortieText.Contains("Korshun_CombatAux_GasSiphon_Left") &&
            sortieText.Contains("Korshun_CombatAux_GasSiphon_Right") &&
            sortieText.Contains("Barbet_CombatSmall_Magnet_Left") &&
            sortieText.Contains("Barbet_CombatSmall_Magnet_Right") &&
            sortieText.Contains("Barbet_CombatSmall_GasSiphon_Left") &&
            sortieText.Contains("Barbet_CombatSmall_GasSiphon_Right") &&
            barbetAuxiliaryCsvText.Contains("barbet_small_siphon") &&
            barbetAuxiliaryCsvText.Contains(",small,siphon,") &&
            sortieText.Contains("Korshun_CombatAux_RepairBeam_Left") &&
            sortieText.Contains("Korshun_CombatAux_RepairBeam_Right") &&
            sortieText.Contains("Korshun_CombatAux_HackingDish_Left") &&
            sortieText.Contains("Korshun_CombatAux_HackingDish_Right") &&
            sortieText.Contains("\"main_rocket\"") &&
            sortieText.Contains("\"side_rocket\"") &&
            sortieText.Contains("WW_Turret_Magnet_Single") &&
            sortieText.Contains("WW_Turret_GasSiphon_Single") &&
            sortieText.Contains("WW_Turret_RepairBeam_Single") &&
            sortieText.Contains("WW_Turret_HackingDish_Single") &&
            sortieText.Contains("int launcherCount = Mathf.Clamp(requestedLauncherCount, 1, 4);") &&
            sortieText.Contains("launcher.sideSign = i == 0 ? -1 : 1") &&
            sortieText.Contains("package.cycleSeconds") &&
            sortieText.Contains("package.cooldownSeconds") &&
            sortieText.Contains("package.repairHpPerCycle") &&
            sortieText.Contains("SetDescendantActiveByNameContains(modelRoot, \"Korshun_TorpedoLauncher_3Tube\", false)") &&
            coreTacticalOreMiningText.Contains("GetMagnetCatchPoint") &&
            coreTacticalOreMiningText.Contains("IsInsideMagnetSideArc") &&
            coreTacticalOreMiningText.Contains("magnetSideArcDegrees") &&
            coreTacticalOreMiningText.Contains("GetUtilityModuleCatchPoint(\"magnet\"") &&
            coreTacticalOreMiningText.Contains("GetUtilityModuleCatchPoint(\"salvage_magnet\"") &&
            coreTacticalOreMiningText.Contains("TryBindUtilityModule(\"siphon\"") &&
            coreTacticalOreMiningText.Contains("ApplyYawToward") &&
            coreTacticalOreMiningText.Contains("GetVisualSideDirection") &&
            coreTacticalUtilityBeamText.Contains("AuxiliaryBeamChannel") &&
            coreTacticalUtilityBeamText.Contains("new AuxiliaryBeamChannel(-1)") &&
            coreTacticalUtilityBeamText.Contains("new AuxiliaryBeamChannel(1)") &&
            coreTacticalUtilityBeamText.Contains("IsInsideSideArc") &&
            coreTacticalUtilityBeamText.Contains("sideArcDegrees") &&
            coreTacticalUtilityBeamText.Contains("ApplyYawToward") &&
            coreTacticalUtilityBeamText.Contains("GetVisualSideDirection") &&
            coreTacticalUtilityBeamText.Contains("reservedTargets") &&
            coreTacticalUtilityBeamText.Contains("GetBeamOrigin(int sideSign") &&
            coreTacticalUtilityBeamText.Contains("TryBindUtilityModule(utilityKind, sideSign") &&
            coreTacticalUtilityBeamText.Contains("\"scanner_hacker\"") &&
            coreTacticalUtilityBeamText.Contains("\"repair\"") &&
            coreTacticalUtilityBeamText.Contains("TickRepairChannel") &&
            coreTacticalUtilityBeamText.Contains("TickScannerChannel") &&
            coreTacticalUtilityBeamText.Contains("repairHpPerCycle") &&
            coreTacticalUtilityBeamText.Contains("cooldownRemaining") &&
            coreTacticalUtilityBeamText.Contains("completedCycles") &&
            coreTacticalUtilityBeamText.Contains("health.currentHealth = Mathf.Min") &&
            !coreTacticalUtilityBeamText.Contains("private CoreTacticalUtilityBeamVisual leftBeam") &&
            !coreTacticalUtilityBeamText.Contains("private CoreTacticalUtilityBeamVisual rightBeam") &&
            !coreTacticalUtilityBeamText.Contains("Mathf.Min(rangeMeters * 0.28f");
        report.Check(coreTacticalCombatLoadoutVisualBinding,
            coreTacticalCombatLoadoutVisualBinding
                ? "Core Tactical combat loadout visuals bind selected Korshun utilities and Barbet small-slot magnet/siphon equipment to imported left/right module visuals, keep rocket trails color-safe, rotate utility modules toward their side-arc targets, and emit utility beams only from actual module visuals."
                : "Core Tactical combat loadout visuals must bind selected Korshun utilities and Barbet small-slot gas siphons/magnets to left/right module visuals, hide default torpedoes on non-torpedo auxiliary loadouts, keep rocket trails color-safe, rotate utility modules toward side-arc targets, and avoid ship-center or idle utility beams.");

        CoreTacticalWeaponVisualAuditResult weaponVisualAuditResult = AuditCoreTacticalWeaponVisualMaterials(report);
        report.Check(weaponVisualAuditResult.AllClear,
            weaponVisualAuditResult.AllClear
                ? "Core Tactical weapon visual screenshot confirms colored shells, rockets, torpedoes, tracers, bursts and utility beams including repair/magnet/salvage/drill/scanner are visible. Audit image: " + weaponVisualAuditResult.ImagePath + ". " + weaponVisualAuditResult.Summary
                : "Core Tactical weapon/utility beam visual screenshot failed or still looks too dark. " + weaponVisualAuditResult.Summary);

        bool coreTacticalOreMiningPrototype =
            coreTacticalOreMiningText.Contains("CoreTacticalOreBoulder") &&
            coreTacticalOreMiningText.Contains("CoreTacticalOreFragment") &&
            coreTacticalOreMiningText.Contains("fragmentLifetimeSeconds = 20f") &&
            coreTacticalOreMiningText.Contains("AdvanceLifetimeForTests") &&
            coreTacticalOreMiningText.Contains("CoreTacticalMiningRig") &&
            coreTacticalOreMiningText.Contains("densityKgPerCubicMeter") &&
            coreTacticalOreMiningText.Contains("healthPerDiameterMeter") &&
            coreTacticalOreMiningText.Contains("fragmentMassPerIntegrityPointKg") &&
            coreTacticalOreMiningText.Contains("ellipsoidVolume") &&
            coreTacticalOreMiningText.Contains("usefulOreConcentration01") &&
            coreTacticalOreMiningText.Contains("weaponRetention01") &&
            coreTacticalOreMiningText.Contains("magnetMaxChunkMassKg") &&
            coreTacticalOreMiningText.Contains("MagnetDeliveryChannel") &&
            coreTacticalOreMiningText.Contains("magnetDeliveryChannels") &&
            coreTacticalOreMiningText.Contains("CoreTacticalOreTargetingRules") &&
            coreTacticalOreMiningText.Contains("IsAutomaticCombatTarget") &&
            coreTacticalOreMiningText.Contains("IsValidExplicitTarget") &&
            coreTacticalOreMiningText.Contains("SetInstalledModules") &&
            coreTacticalOreMiningText.Contains("IsModuleInstalled") &&
            coreTacticalOreMiningText.Contains("GetModuleCooldownRemainingSeconds") &&
            coreTacticalOreMiningText.Contains("GetModuleCooldown01") &&
            coreTacticalOreMiningText.Contains("SetModuleCycleTimerForTests") &&
            coreTacticalOreMiningText.Contains("AddDirtyOreForTests") &&
            coreTacticalOreMiningText.Contains("HasAnyInstalledMiningModule") &&
            !coreTacticalOreMiningText.Contains("FindObjectsByType<CoreTacticalOreBoulder>") &&
            bootstrapText.Contains("CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam)") &&
            bootstrapText.Contains("CoreTacticalOreTargetingRules.IsValidExplicitTarget(candidate, targetTeam)") &&
            fleetText.Contains("TryToggleOreBoulderPriorityTargetFromHit") &&
            fleetText.Contains("ClearPriorityTargetForSelectedShips") &&
            sortieText.Contains("ConfigurePlayerMiningModules") &&
            sortieText.Contains("ApplyMiningModulePackage") &&
            sortieText.Contains("!playerMiningRig.IsModuleInstalled(module)") &&
            sortieText.Contains("GetMiningModuleCooldownTextForTests") &&
            sortieText.Contains("GetMiningModuleCooldownShutterForTests") &&
            coreTacticalOreMiningText.Contains("Core Tactical Mining Magnet Beam Left") &&
            coreTacticalOreMiningText.Contains("Core Tactical Mining Magnet Beam Right") &&
            !coreTacticalOreMiningText.Contains("activeMagnetDeliveryFragment") &&
            coreTacticalOreMiningText.Contains("magnetRangeMeters = 500f") &&
            coreTacticalOreMiningText.Contains("CalculateMagnetDeliveryEnergyCost") &&
            coreTacticalOreMiningText.Contains("ContinuePaidMagnetDelivery") &&
            coreTacticalOreMiningText.Contains("IsMagnetDeliveryInRange") &&
            coreTacticalOreMiningText.Contains("ReleaseMagnetDelivery") &&
            coreTacticalOreMiningText.Contains("Magnet released: fragment left beam range.") &&
            coreTacticalOreMiningText.Contains("GetMagnetCatchPoint(int sideSign") &&
            coreTacticalOreMiningText.Contains("IsInsideMagnetSideArc") &&
            coreTacticalOreMiningText.Contains("magnetSideArcDegrees") &&
            coreTacticalOreMiningText.Contains("GetUtilityModuleCatchPoint(\"magnet\"") &&
            coreTacticalOreMiningText.Contains("GetUtilityModuleCatchPoint(\"salvage_magnet\"") &&
            coreTacticalOreMiningText.Contains("ApplyYawToward") &&
            coreTacticalOreMiningText.Contains("PullToward(catchPoint, magnetPullSpeedMS * deltaSeconds, true)") &&
            coreTacticalOreMiningText.Contains("Magnet waiting: need ") &&
            !coreTacticalOreMiningText.Contains("Magnet stopped: no energy.") &&
            coreTacticalOreMiningText.Contains("CoreTacticalUtilityBeamPalette.Magnet") &&
            coreTacticalOreMiningText.Contains("CoreTacticalUtilityBeamPalette.Drill") &&
            coreTacticalOreMiningText.Contains("drillLossReduction01") &&
            coreTacticalOreMiningText.Contains("crusherRawKgPerCycle") &&
            coreTacticalOreMiningText.Contains("inventoryCapacityKg") &&
            coreTacticalOreMiningText.Contains("InventoryFreeKg") &&
            coreTacticalOreMiningText.Contains("BuildInventoryRows") &&
            coreTacticalOreMiningText.Contains("SetStartingInventoryRows") &&
            coreTacticalOreMiningText.Contains("SetCargoFull") &&
            coreTacticalOreMiningText.Contains("magnetEnabled = false") &&
            coreTacticalOreMiningText.Contains("drillEnabled = false") &&
            coreTacticalOreMiningText.Contains("crusherEnabled = false") &&
            coreTacticalOreMiningText.Contains("TryAddShipCargoFromRuntime") &&
            coreTacticalOreMiningText.Contains("TryAddShipLowGradeOreFromRuntime") &&
            coreTacticalOreMiningText.Contains("TryFlushDirtyOreToRuntimeCargo") &&
            coreTacticalOreMiningText.Contains("TotalOreCollectedKg") &&
            coreTacticalOreMiningText.Contains("countMissionProgress") &&
            metaText.Contains("FindFirstStoredLowGradeOreType") &&
            metaText.Contains("TrySpendLowGradeOre") &&
            metaText.Contains("Processed \" + rawBatchKg + \" kg low-grade") &&
            sortieText.Contains("SpawnCoreTacticalOreBoulders") &&
            sortieText.Contains("ResolveOreBoulderNaturalIntegrityLossPerSecond") &&
            sortieText.Contains("health.currentHealth = definition.maxHealth") &&
            sortieText.Contains("ConfigurePlayerMiningRig") &&
            sortieText.Contains("CoreTacticalMiningModule.Crusher") &&
            sortieText.Contains("TacticalHudActionKind.Inventory") &&
            sortieText.Contains("DrawShipInventoryWindow") &&
            sortieText.Contains("HandleOreBoulderInspectionInput") &&
            sortieText.Contains("DrawOreBoulderInfoPanel") &&
            sortieText.Contains("SetInspectedOreBoulderAsPriorityTarget") &&
            sortieText.Contains("CoreTacticalOreTargetingRules.IsValidExplicitTarget(targetShip, CoreTacticalCombatTeam.Enemy)") &&
            sortieText.Contains("ClearSelectedShipPriorityTarget") &&
            sortieText.Contains("autocannon.barrelsPerMount = Mathf.Max(1, package.barrelsOrProjectiles)") &&
            !sortieText.Contains("autocannon.reloadSeconds = Mathf.Max(0.05f, reloadSeconds / Mathf.Max(1, package.barrelsOrProjectiles))") &&
            bootstrapText.Contains("barrelsPerMount") &&
            bootstrapText.Contains("PendingAutocannonShot") &&
            bootstrapText.Contains("ScheduleMountSalvo") &&
            bootstrapText.Contains("barrelShotSpacingSeconds") &&
            metaText.Contains("TryAddShipLowGradeOreFromRuntime") &&
            metaText.Contains("BuildShipCargoValidationMap") &&
            metaText.Contains("capitalStorage.AddLowGradeOre") &&
            playerProgressText.Contains("shipLowGradeOreCargo") &&
            playerProgressText.Contains("LowGradeOreStackState") &&
            playerProgressText.Contains("AddLowGradeOre") &&
            sortieText.Contains("BuildStartingInventoryRows") &&
            sortieText.Contains("TryFlushDirtyOreToRuntimeCargo") &&
            sortieText.Contains("cargoCapacityTons") &&
            sortieText.Contains("RequiredEnemyKillObjectiveCount = 4") &&
            sortieText.Contains("RequiredOreObjectiveKg = 300f") &&
            sortieText.Contains("AreCoreMissionObjectivesCompleteForTests") &&
            sortieText.Contains("AreMissionObjectivesComplete()") &&
            sortieText.Contains("Mine any ore") &&
            fleetText.Contains("GetComponent<CoreTacticalOreBoulder>()") &&
            bootstrapText.Contains("NotifyOreBoulderDamage") &&
            !sortieText.Contains("SafeOreSortieController") &&
            !sortieText.Contains("SortieResourceCacheController") &&
            !sortieText.Contains("MiningFragment") &&
            !sortieText.Contains("ShipPhysics") &&
            !sortieText.Contains("ClearLegacySortieRuntimeObjects") &&
            !coreTacticalOreMiningText.Contains("MiningFragment") &&
            !coreTacticalOreMiningText.Contains("ShipPhysics");
        report.Check(coreTacticalOreMiningPrototype,
            coreTacticalOreMiningPrototype
                ? "Core Tactical mining has its own dense HP-scaled ore boulders, damage-shed fragments, colored per-module side-arc rotating magnet/drill utility beams, prepaid per-channel player magnet delivery, drill laser, crusher cycle, low-grade concentrate cargo persistence, inventory window, cargo-capacity intake gate, drill/crusher energy shutoff, ore inspection target button, multi-barrel frigate salvos and sortie cargo deposit without legacy ShipPhysics mining fragments."
                : "Core Tactical mining prototype is incomplete or still references legacy safe-ore/resource-cache/ShipPhysics mining fragments, or it lacks dense boulder math, colored per-module side-arc rotating magnet/drill utility beams, prepaid per-channel magnet delivery, drill laser, crusher cycle, low-grade concentrate persistence, inventory/cargo-capacity intake, energy shutoff, ore inspection target contracts, or multi-barrel frigate salvos.");

        GameObject fragmentLifetimeProbe = null;
        try
        {
            fragmentLifetimeProbe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fragmentLifetimeProbe.name = "Big Test Ore Fragment Dust Lifetime";
            fragmentLifetimeProbe.transform.localScale = Vector3.one * 2f;
            CoreTacticalOreFragment fragmentLifetime = fragmentLifetimeProbe.AddComponent<CoreTacticalOreFragment>();
            fragmentLifetime.fragmentLifetimeSeconds = 20f;
            fragmentLifetime.Initialize("windshale", "windshale_ore", "Windshale", 100f, 0.25f, 1f, -1000f, Color.gray);
            Vector3 initialFragmentScale = fragmentLifetimeProbe.transform.localScale;
            fragmentLifetime.AdvanceLifetimeForTests(10f);
            bool fragmentShrinksMidlife = Approximately(fragmentLifetime.LifetimeSecondsForTests, 10f, 0.001f)
                && fragmentLifetimeProbe.transform.localScale.x < initialFragmentScale.x
                && fragmentLifetimeProbe.transform.localScale.x > initialFragmentScale.x * 0.04f;
            fragmentLifetime.captured = true;
            fragmentLifetime.AdvanceLifetimeForTests(50f);
            bool fragmentLifetimePausesCaptured = Approximately(fragmentLifetime.LifetimeSecondsForTests, 10f, 0.001f);
            fragmentLifetime.captured = false;
            fragmentLifetime.AdvanceLifetimeForTests(10f);
            bool fragmentDustExpiryOk = fragmentLifetime.LifetimeSecondsForTests >= 20f
                && fragmentLifetimeProbe.transform.localScale.x <= initialFragmentScale.x * 0.05f;
            bool oreFragmentLifetimeOk = fragmentShrinksMidlife && fragmentLifetimePausesCaptured && fragmentDustExpiryOk;
            report.Check(oreFragmentLifetimeOk,
                oreFragmentLifetimeOk
                    ? "Core Tactical ore fragments visibly crumble toward dust over 20 seconds, and the lifetime pauses while a fragment is captured."
                    : "Core Tactical ore fragment lifetime failed: verify 20 second dust shrink and captured pause. Parts: shrink="
                        + fragmentShrinksMidlife
                        + ", pause="
                        + fragmentLifetimePausesCaptured
                        + ", expiry="
                        + fragmentDustExpiryOk
                        + ".");
        }
        finally
        {
            DestroyBigTestObject(fragmentLifetimeProbe);
        }

        bool coreTacticalCloudCondensatePrototype =
            coreTacticalOreMiningText.Contains("CoreTacticalGasCloudDefinition") &&
            coreTacticalOreMiningText.Contains("CoreTacticalGasCloud") &&
            coreTacticalOreMiningText.Contains("rawVolumeLiters") &&
            coreTacticalOreMiningText.Contains("usefulVolumeLiters") &&
            coreTacticalOreMiningText.Contains("chemicalDamagePerMinute") &&
            coreTacticalOreMiningText.Contains("driftVelocityMS") &&
            coreTacticalOreMiningText.Contains("CanHarvest") &&
            coreTacticalOreMiningText.Contains("ExtractRawCondensate") &&
            coreTacticalOreMiningText.Contains("ApplyChemicalContactDamage") &&
            coreTacticalOreMiningText.Contains("FindObjectsByType<CoreTacticalShipMotor>") &&
            coreTacticalOreMiningText.Contains("CoreTacticalDamageRequest.Chemical") &&
            coreTacticalOreMiningText.Contains("UsesPlainTransparentCloudMaterialForTests") &&
            coreTacticalOreMiningText.Contains("LobeRendererCountForTests") &&
            coreTacticalOreMiningText.Contains("CoreTacticalMiningModule.Siphon") &&
            coreTacticalOreMiningText.Contains("CoreTacticalMiningModule.CloudConcentrator") &&
            coreTacticalOreMiningText.Contains("siphonChannelCount") &&
            coreTacticalOreMiningText.Contains("siphonLitersPerSecond") &&
            coreTacticalOreMiningText.Contains("siphonEnergyPerSecond") &&
            coreTacticalOreMiningText.Contains("AddRawCloudCondensate") &&
            coreTacticalOreMiningText.Contains("UpdateSiphonForTests") &&
            coreTacticalOreMiningText.Contains("CoreTacticalSiphonIntakeVisual") &&
            coreTacticalOreMiningText.Contains("ShowSiphonIntake") &&
            coreTacticalOreMiningText.Contains("GetSiphonIntakeOrigin") &&
            !coreTacticalOreMiningText.Contains("Core Tactical Cloud Siphon Beam") &&
            coreTacticalOreMiningText.Contains("ProcessCloudConcentratorCycle") &&
            coreTacticalOreMiningText.Contains("cloudConcentratorWaterLitersPerCycle * (1f - concentration)") &&
            coreTacticalSiphonIntakeText.Contains("CoreTacticalSiphonIntakeVisual") &&
            coreTacticalSiphonIntakeText.Contains("RingCount = 4") &&
            coreTacticalSiphonIntakeText.Contains("SegmentCount = 48") &&
            coreTacticalSiphonIntakeText.Contains("axisDistance = Mathf.Lerp(coneLength, 0.75f, easedTravel)") &&
            coreTacticalSiphonIntakeText.Contains("radius = Mathf.Lerp(outerRadius, innerRadius, easedTravel)") &&
            coreTacticalSiphonIntakeText.Contains("time * 0.62f") &&
            coreTacticalSiphonIntakeText.Contains("radius * 0.075f") &&
            !coreTacticalUtilityBeamText.Contains("CoreTacticalUtilityBeamPalette.Siphon") &&
            sortieText.Contains("CoreTacticalGasCloudCount = 3") &&
            sortieText.Contains("SpawnCoreTacticalGasClouds") &&
            sortieText.Contains("BuildCoreTacticalGasCloudDefinition") &&
            sortieText.Contains("SpawnCoreTacticalGasCloud") &&
            sortieText.Contains("BuildCoreTacticalGasCloudDefinition(config, \"common_cloud\", true") &&
            sortieText.Contains("BuildCoreTacticalGasCloudDefinition(config, \"wet_cloud\", true") &&
            sortieText.Contains("BuildCoreTacticalGasCloudDefinition(config, \"common_cloud\", false") &&
            sortieText.Contains("condensateItemId = harvestable && gasType != null ? gasType.condensateItemId : \"\"") &&
            sortieText.Contains("usefulLiters = 0f") &&
            sortieText.Contains("harvestable = harvestable") &&
            sortieText.Contains("lobeOffsets = new[] { Vector3.zero }") &&
            sortieText.Contains("lobeSizes = new[] { new Vector3(560f, 180f, 380f) }") &&
            !sortieText.Contains("new Vector3(-90f, 0f, -35f)") &&
            !sortieText.Contains("new Vector3(-130f, 0f, 0f)") &&
            sortieText.Contains("CoreTacticalMiningModule.Siphon") &&
            sortieText.Contains("CoreTacticalMiningModule.CloudConcentrator") &&
            sortieText.Contains("keyboard.sKey.wasPressedThisFrame") &&
            sortieText.Contains("keyboard.kKey.wasPressedThisFrame") &&
            metaText.Contains("TryAddShipRawCloudCondensateFromRuntime") &&
            metaText.Contains("FindFirstStoredRawCloudCondensateType") &&
            metaText.Contains("TrySpendRawCloudCondensate") &&
            metaText.Contains("capitalStorage.AddRawCloudCondensate") &&
            playerProgressText.Contains("RawCloudCondensateStackState") &&
            playerProgressText.Contains("shipRawCloudCondensateCargo") &&
            playerProgressText.Contains("rawCloudCondensate");
        report.Check(coreTacticalCloudCondensatePrototype,
            coreTacticalCloudCondensatePrototype
                ? "Core Tactical cloud condensate contract is wired: three drifting gas clouds include two harvestable raw condensates plus one empty water cloud, each cloud renders as one plain transparent ellipsoid, contact gas damage uses chemical resistance, siphon channels harvest only while installed/enabled/energized, cloud concentrator vents water without creating clean resources, and raw condensate persists through ship and port stacks."
                : "Core Tactical cloud condensate contract is incomplete: it must spawn two harvestable clouds plus one empty water cloud, render each cloud as one plain transparent ellipsoid, apply contact gas damage through chemical resistance, gate harvesting behind siphon channels, keep concentrator output as water venting, and persist raw/useful condensate stacks through ship and port storage.");

        bool claudianSlipContractKept =
            sortieText.Contains("ship.maxForwardSpeedMS * 0.8f") &&
            sortieText.Contains("EnterActiveSlipAtFullSpeed") &&
            sortieText.Contains("playerSlipDrive.EnterActiveSlipAtFullSpeed(exitDirection)") &&
            sortieText.Contains("playerShip.SetCommand(entryFlyThroughTarget, exitDirection)") &&
            sortieText.Contains("PrimePlayerEntryVelocity(playerShip, exitDirection, ClaudianSlipTargetSpeedMS)") &&
            sortieText.Contains("flatVelocity.magnitude >= speedFloor - 0.1f") &&
            sortieText.Contains("ClaudianSlipTargetSpeedMS = 365f") &&
            sortieText.Contains("ClaudianSlipOverspeedBrakeSeconds = 8f") &&
            sortieText.Contains("targetForwardSpeedMS = ClaudianSlipTargetSpeedMS") &&
            sortieText.Contains("overspeedBrakeSeconds = ClaudianSlipOverspeedBrakeSeconds") &&
            sortieText.Contains("rampDownSeconds = 8.0f") &&
            sortieText.Contains("Mathf.MoveTowards(") &&
            sortieText.Contains("ApplyState(false, 1f)") &&
            sortieText.Contains("ApplyOverspeedBrake(false, deltaSeconds)") &&
            sortieText.Contains("ApplyOverspeedBrake(active, deltaSeconds)") &&
            sortieText.Contains("direction * Mathf.Max(0f, targetForwardSpeedMS)") &&
            sortieText.Contains("Mathf.Max(1f, Mathf.Max(0f, targetForwardSpeedMS) / baseSpeed)") &&
            sortieText.Contains("ship.forwardAccelerationMultiplier = Mathf.Max(1f, speedMultiplier)") &&
            sortieText.Contains("requireOutsideMissionZone = false") &&
            sortieText.Contains("IsBlockedByMissionZone") &&
            sortieText.Contains("HandleTacticalActionHotkeys") &&
            sortieText.Contains("keyboard.fKey.wasPressedThisFrame") &&
            sortieText.Contains("keyboard.yKey.wasPressedThisFrame") &&
            !sortieText.Contains("TryArmClaudianSlipForExit") &&
            sortieText.Contains("TrySetArmedWhenAllowed") &&
            sortieText.Contains("HasClaudianSlipInterference") &&
            sortieText.Contains("currentForwardSpeedMS + 0.05f < Mathf.Max(0f, minimumEngageSpeedMS)") &&
            shipMotorText.Contains("forwardAccelerationMultiplier") &&
            shipMotorText.Contains("maxYawRateDegPerSecond * Mathf.Deg2Rad");
        report.Check(claudianSlipContractKept,
            claudianSlipContractKept
                ? "Core Tactical Claudian slip uses a shared 365 m/s speed, starts the sortie as a full-speed fly-through toward the map center, works anywhere once the ship is fast enough, is controlled by the slip hotkey instead of AUTO EXIT, and brakes overspeed back to normal max over 8s."
                : "Core Tactical Claudian slip must use shared 365 m/s travel, start the sortie as a full-speed fly-through toward the map center, work anywhere with only speed/interference blockers, stay independent from AUTO EXIT, and explicitly brake overspeed to normal max over 8s.");

        bool coreTacticalEnemyMovementScaled =
            sortieText.Contains("ClassFrigateAverageSpeedMS = 60f") &&
            sortieText.Contains("ClassCruiserAverageSpeedMS = 45f") &&
            sortieText.Contains("ClassBattleshipAverageSpeedMS = 30f") &&
            sortieText.Contains("ClassFrigateSpeedMultiplier = 2.25f") &&
            sortieText.Contains("ClassCruiserSpeedMultiplier = 1.5f") &&
            sortieText.Contains("ClassBattleshipSpeedMultiplier = 1f") &&
            sortieText.Contains("ClassFrigateAverageYawDegPerSecond = 30f") &&
            sortieText.Contains("ClassCruiserAverageYawDegPerSecond = 18f") &&
            sortieText.Contains("ClassBattleshipAverageYawDegPerSecond = 12f") &&
            sortieText.Contains("ClassFrigateAcceleration90PercentSeconds = 8f") &&
            sortieText.Contains("ClassCruiserAcceleration90PercentSeconds = 12f") &&
            sortieText.Contains("ClassBattleshipAcceleration90PercentSeconds = 20f") &&
            sortieText.Contains("speed += powerPlant.speedDeltaMS") &&
            sortieText.Contains("speed *= ResolveClassSpeedMultiplier(effectiveClassId)") &&
            sortieText.Contains("CalculateClassAccelerationMS2") &&
            sortieText.Contains("EnemyFrigateMaxSpeedMS = ClassFrigateAverageSpeedMS * ClassFrigateSpeedMultiplier") &&
            sortieText.Contains("EnemyCruiserMaxSpeedMS = ClassCruiserAverageSpeedMS * ClassCruiserSpeedMultiplier") &&
            sortieText.Contains("EnemyFrigateAccelerationMS2 = EnemyFrigateMaxSpeedMS * NaturalLogTen") &&
            sortieText.Contains("EnemyCruiserAccelerationMS2 = EnemyCruiserMaxSpeedMS * NaturalLogTen") &&
            sortieText.Contains("EnemyFrigateBrakingMS2 = EnemyFrigateAccelerationMS2") &&
            sortieText.Contains("EnemyCruiserBrakingMS2 = EnemyCruiserAccelerationMS2") &&
            metaText.Contains("DevelopmentFrigateSpeedMultiplier = 2.25f") &&
            metaText.Contains("DevelopmentCruiserSpeedMultiplier = 1.5f") &&
            metaText.Contains("speed += power.speedDeltaMS") &&
            metaText.Contains("speed *= ResolveDevelopmentDockClassSpeedMultiplier(effectiveClassId)") &&
            !sortieText.Contains("maxForwardSpeedMS = 190f") &&
            !sortieText.Contains("maxForwardSpeedMS = 102f");
        report.Check(coreTacticalEnemyMovementScaled,
            coreTacticalEnemyMovementScaled
                ? "Core Tactical player profiles, dock profiles and intro enemies multiply the whole configured package speed by class: frigates x2.25, cruisers x1.5, battleships x1."
                : "Core Tactical class movement must multiply the whole configured package speed by class: frigates x2.25, cruisers x1.5, battleships x1.");

        bool coreTacticalMotorDoesNotDragCapCruiseSpeed =
            shipMotorText.Contains("body.linearDamping = 0f") &&
            shipMotorText.Contains("Mathf.Exp") &&
            shipMotorText.Contains("forwardAccelerationMS2") &&
            !shipMotorText.Contains("body.linearDamping = 0.22f");
        report.Check(coreTacticalMotorDoesNotDragCapCruiseSpeed,
            coreTacticalMotorDoesNotDragCapCruiseSpeed
                ? "Core Tactical ship motor disables Rigidbody linear damping so a scaled Korshun profile is not capped around 40 m/s."
                : "Core Tactical ship motor must not use Rigidbody linear damping that caps a scaled Korshun profile around 40 m/s.");

        bool coreTacticalMotorKeepsCruiseSpeedWhileTurning =
            shipMotorText.Contains("noseFirstYawReadyDeg = 95f") &&
            shipMotorText.Contains("noseFirstYawHardGateDeg = 170f") &&
            shipMotorText.Contains("noseFirstYawReadyDeg = Mathf.Clamp(noseFirstYawReadyDeg, 1f, 140f)") &&
            shipMotorText.Contains("noseFirstYawHardGateDeg = Mathf.Clamp(noseFirstYawHardGateDeg, noseFirstYawReadyDeg + 1f, 179f)") &&
            bootstrapText.Contains("motor.noseFirstYawReadyDeg = 95f") &&
            bootstrapText.Contains("motor.noseFirstYawHardGateDeg = 170f") &&
            !bootstrapText.Contains("motor.noseFirstYawHardGateDeg = 110f");
        report.Check(coreTacticalMotorKeepsCruiseSpeedWhileTurning,
            coreTacticalMotorKeepsCruiseSpeedWhileTurning
                ? "Core Tactical ship motor keeps cruise speed through normal turns instead of throttling Korshun back to the old low-speed behavior."
                : "Core Tactical ship motor yaw-readiness can still throttle normal turns; do not allow the old low-speed Korshun behavior back.");

        bool coreTacticalMotorArrivalStable =
            shipMotorText.Contains("Mathf.Exp") &&
            shipMotorText.Contains("CalculateDynamicSlowdownDistance") &&
            shipMotorText.Contains("CalculateArrivalSpeedLimit") &&
            shipMotorText.Contains("CalculateLateralDriftDampingVelocity") &&
            shipMotorText.Contains("ArrivalHardBrakeMultiplier") &&
            shipMotorText.Contains("currentClosingSpeed") &&
            shipMotorText.Contains("lateralStopDistance") &&
            shipMotorText.Contains("TryCompleteFlatArrival") &&
            shipMotorText.Contains("targetPosition.x = body.position.x") &&
            shipMotorText.Contains("targetPosition.z = body.position.z") &&
            shipMotorText.Contains("arrivalLockSpeedMS") &&
            !shipMotorText.Contains("body.MovePosition(settledPosition)") &&
            bootstrapText.Contains("arrivalRadiusMeters = Mathf.Clamp(size.z * 0.14f, 8f, 42f)") &&
            bootstrapText.Contains("arrivalLockSpeedMS = Mathf.Max(1.5f, maxForwardSpeedMS * 0.05f)");
        report.Check(coreTacticalMotorArrivalStable,
            coreTacticalMotorArrivalStable
                ? "Core Tactical ship motor uses current-speed arrival braking, lateral drift damping, and a size-based arrival radius without snapping to the exact marker."
                : "Core Tactical ship motor must brake from current/slip speed and damp lateral drift so ships stop inside the command radius without orbiting it.");

        bool dockCoreLaunchUsesDevelopmentMotion =
            metaText.Contains("BeginSessionExtractionSortie(CreateCoreTacticalIntroCombatSortieDefinition(), true)") &&
            metaText.Contains("TryBuildDevelopmentDockShipMotionProfile") &&
            metaText.Contains("ApplyDevelopmentDockShipMotionProfile") &&
            metaText.Contains("ResolveDevelopmentDockClassSpeedMultiplier") &&
            metaText.Contains("KorshunPreferredDefaultHullPackageId = \"korshun_hull_fast\"") &&
            metaText.Contains("ShouldRefreshDevelopmentDockDefaultPackage") &&
            metaText.Contains("slot.loadoutDefaultsVersion = DevelopmentDockLoadoutDefaultsVersion") &&
            metaText.Contains("ship.baseMaxSpeedMS = speed") &&
            metaText.Contains("ship.hullForwardThrustKgf") &&
            !metaText.Contains("CreateQuickAdaptiveManualSortieDefinition") &&
            !metaText.Contains("BeginQuickAdaptiveManualSessionSortie") &&
            !metaText.Contains("GetDevelopmentDockOrdinaryMissionOffers") &&
            !metaText.Contains("TryRunDevelopmentDockOrdinaryMission");
        report.Check(dockCoreLaunchUsesDevelopmentMotion,
            dockCoreLaunchUsesDevelopmentMotion
                ? "Core Combat is the only dock combat launch route and applies selected development hull and power-plant motion to the live session ship."
                : "Dock launch cleanup is incomplete: only Core Combat should remain, and it must still apply selected development hull and power-plant motion.");
#else
        report.Check(true, "Core Tactical prototype transfer source scan is editor-only and skipped in player builds.");
#endif
    }

    private static void ValidateCoreTacticalMotorArrivalBraking(BigTestReport report)
    {
        GameObject probe = null;
        try
        {
            probe = new GameObject("Big Test Core Tactical Motor Arrival Brake Probe");
            probe.transform.SetPositionAndRotation(new Vector3(0f, 80f, 0f), Quaternion.identity);
            Rigidbody body = probe.AddComponent<Rigidbody>();
            CoreTacticalShipMotor motor = probe.AddComponent<CoreTacticalShipMotor>();
            motor.InitializePrototypeShip(
                "arrival_brake_probe",
                "Arrival Brake Probe",
                new Vector3(10f, 4f, 32f),
                Color.cyan);
            motor.obstacleAvoidanceEnabled = false;
            motor.maxForwardSpeedMS = 72f;
            motor.maxReverseSpeedMS = 10f;
            motor.maxLateralSpeedMS = 8f;
            motor.forwardSpeedMultiplier = 1f;
            motor.forwardAccelerationMultiplier = 1f;
            motor.forwardAccelerationMS2 = 72f * 2.3025851f / 8f;
            motor.brakingAccelerationMS2 = motor.forwardAccelerationMS2;
            motor.lateralAccelerationMS2 = 12f;
            motor.slowdownDistanceMeters = 46f;
            motor.arrivalRadiusMeters = 8f;
            motor.arrivalLockSpeedMS = 2f;
            motor.maxYawRateDegPerSecond = 120f;
            motor.finalFacingDistanceMeters = 24f;

            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.position = probe.transform.position;
            body.rotation = probe.transform.rotation;
            body.linearVelocity = new Vector3(0f, 0f, 365f);

            Vector3 commandPoint = new Vector3(0f, 80f, 3800f);
            motor.SetCommand(commandPoint, Vector3.forward);

            float deltaSeconds = Mathf.Max(0.001f, Time.fixedDeltaTime);
            float minimumDistance = float.PositiveInfinity;
            float lastDistance = float.PositiveInfinity;
            float finalFlatSpeed = float.PositiveInfinity;
            bool ticked = true;
            for (int i = 0; i < 2400; i++)
            {
                ticked = TryInvokePrivateMethod(motor, "FixedUpdate", report);
                if (!ticked)
                {
                    break;
                }

                Vector3 nextPosition = body.position + body.linearVelocity * deltaSeconds;
                body.position = nextPosition;
                probe.transform.position = nextPosition;

                Vector3 flatDelta = commandPoint - body.position;
                flatDelta.y = 0f;
                lastDistance = flatDelta.magnitude;
                minimumDistance = Mathf.Min(minimumDistance, lastDistance);
                Vector3 flatVelocity = body.linearVelocity;
                flatVelocity.y = 0f;
                finalFlatSpeed = flatVelocity.magnitude;

                if (lastDistance <= motor.arrivalRadiusMeters + 0.5f
                    && finalFlatSpeed <= motor.arrivalLockSpeedMS + 0.5f)
                {
                    break;
                }
            }

            bool straightArrivalBrakeStable = ticked
                && minimumDistance <= motor.arrivalRadiusMeters + 1.5f
                && lastDistance <= motor.arrivalRadiusMeters + 1.5f
                && finalFlatSpeed <= motor.arrivalLockSpeedMS + 0.75f;

            Vector3 lateralStartPosition = new Vector3(0f, 80f, 0f);
            Quaternion lateralStartRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            probe.transform.SetPositionAndRotation(lateralStartPosition, lateralStartRotation);
            body.position = lateralStartPosition;
            body.rotation = lateralStartRotation;
            body.angularVelocity = Vector3.zero;
            body.linearVelocity = new Vector3(70f, 0f, 0f);
            motor.SetCommand(new Vector3(0f, 80f, 800f), Vector3.forward);
            float initialLateralSpeed = Mathf.Abs(body.linearVelocity.x);
            ticked = ticked && TryInvokePrivateMethod(motor, "FixedUpdate", report);
            float dampedLateralSpeed = Mathf.Abs(body.linearVelocity.x);
            bool lateralDriftDamped = ticked && dampedLateralSpeed < initialLateralSpeed - 0.05f;

            report.Check(straightArrivalBrakeStable && lateralDriftDamped,
                straightArrivalBrakeStable && lateralDriftDamped
                    ? "Core Tactical motor brakes an overspeed ship into the command point and damps tangential drift: distance "
                        + lastDistance.ToString("0.###")
                        + " m, speed "
                        + finalFlatSpeed.ToString("0.###")
                        + " m/s, lateral "
                        + initialLateralSpeed.ToString("0.###")
                        + " -> "
                        + dampedLateralSpeed.ToString("0.###")
                        + " m/s."
                    : "Core Tactical motor must brake before the command point and damp side drift instead of overshooting/orbiting: straight min distance "
                        + minimumDistance.ToString("0.###")
                        + " m, final distance "
                        + lastDistance.ToString("0.###")
                        + " m, speed "
                        + finalFlatSpeed.ToString("0.###")
                        + " m/s, lateral "
                        + initialLateralSpeed.ToString("0.###")
                        + " -> "
                        + dampedLateralSpeed.ToString("0.###")
                        + " m/s.");
        }
        finally
        {
            DestroyBigTestObject(probe);
        }
    }

    private static void ValidateCoreTacticalOreTargetingRules(BigTestReport report)
    {
        GameObject normalObject = null;
        GameObject boulderObject = null;
        try
        {
            normalObject = new GameObject("Big Test Core Tactical Enemy Target Probe");
            Rigidbody normalBody = normalObject.AddComponent<Rigidbody>();
            normalBody.useGravity = false;
            CoreTacticalShipMotor normalMotor = normalObject.AddComponent<CoreTacticalShipMotor>();
            CoreTacticalCombatant normalCombatant = normalObject.AddComponent<CoreTacticalCombatant>();
            normalCombatant.team = CoreTacticalCombatTeam.Enemy;
            normalCombatant.ship = normalMotor;

            boulderObject = new GameObject("Big Test Core Tactical Ore Target Probe");
            Rigidbody boulderBody = boulderObject.AddComponent<Rigidbody>();
            boulderBody.useGravity = false;
            CoreTacticalShipMotor boulderMotor = boulderObject.AddComponent<CoreTacticalShipMotor>();
            CoreTacticalCombatant boulderCombatant = boulderObject.AddComponent<CoreTacticalCombatant>();
            boulderCombatant.team = CoreTacticalCombatTeam.Enemy;
            boulderCombatant.ship = boulderMotor;
            boulderObject.AddComponent<CoreTacticalOreBoulder>();

            bool normalAuto = CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(normalCombatant, CoreTacticalCombatTeam.Enemy);
            bool boulderNotAuto = !CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(boulderCombatant, CoreTacticalCombatTeam.Enemy);
            bool boulderExplicit = CoreTacticalOreTargetingRules.IsValidExplicitTarget(boulderMotor, CoreTacticalCombatTeam.Enemy);
            CoreTacticalPriorityTargetControl priority = normalObject.AddComponent<CoreTacticalPriorityTargetControl>();
            priority.SetPriorityTarget(boulderMotor);
            bool priorityAcceptsBoulder = priority.TryGetPriorityTarget(CoreTacticalCombatTeam.Enemy, out CoreTacticalShipMotor resolved)
                && resolved == boulderMotor;
            bool oreTargetingRulesKept = normalAuto && boulderNotAuto && boulderExplicit && priorityAcceptsBoulder;

            report.Check(oreTargetingRulesKept,
                oreTargetingRulesKept
                    ? "Core Tactical ore boulders are explicit-only targets: weapons skip them during automatic target search, but priority targeting accepts them."
                    : "Core Tactical ore boulders must not be automatic weapon targets, but must remain valid explicit priority targets.");
        }
        finally
        {
            DestroyBigTestObject(normalObject);
            DestroyBigTestObject(boulderObject);
        }
    }

    private static void ValidateCoreTacticalLeviathanBehavior(BigTestReport report)
    {
#if UNITY_EDITOR
        string leviathanText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalLeviathan.cs");
        string sortieText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCombatSortieController.cs");
        string fleetText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalFleetController.cs");
        string oreMiningText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalOreMining.cs");
        string bootstrapText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalPrototypeBootstrap.cs");
        string designText = ReadProjectText("Docs/SessionExtractionCore.md");
        bool structuralOk =
            leviathanText.Contains("BiteAllowedTargetLengthRatio = 0.35f") &&
            leviathanText.Contains("LethalBiteTargetLengthRatio = 0.20f") &&
            leviathanText.Contains("TooSmallLeviathanToTargetRatio = 0.30f") &&
            leviathanText.Contains("EdibleBodyLengthRatio = 1f / 3f") &&
            leviathanText.Contains("DefaultAttackCommitSeconds = 20f") &&
            leviathanText.Contains("aggressionPerSecondNear = 0.095f") &&
            leviathanText.Contains("aggressionPerSecondClose = 0.064f") &&
            leviathanText.Contains("aggressionDecayPerSecond = 1f / 30f") &&
            leviathanText.Contains("artilleryReportAggression = 0.02f") &&
            leviathanText.Contains("directDamageAggression = 0.315f") &&
            leviathanText.Contains("nearExplosionAggression = 0.06f") &&
            leviathanText.Contains("CommandStalkTarget") &&
            leviathanText.Contains("TryIdleWander") &&
            leviathanText.Contains("CalculateProximityAggressionRateForTests") &&
            leviathanText.Contains("CalculateNetProximityAggressionRateForTests") &&
            leviathanText.Contains("DecayPreAttackAggression") &&
            leviathanText.Contains("CoreTacticalShipMotor attackTarget = IsLiveTarget(currentTarget)") &&
            leviathanText.Contains("CoreTacticalShipMotor aggressionTarget = target") &&
            leviathanText.Contains("outer01 * outer01") &&
            leviathanText.Contains("NotifyArtilleryReport") &&
            leviathanText.Contains("NotifyProjectileImpact") &&
            leviathanText.Contains("NotifyDirectDamageForTests") &&
            leviathanText.Contains("TryEatOreFragmentForTests") &&
            leviathanText.Contains("TryEatOreBoulderForTests") &&
            leviathanText.Contains("TryEatAutomatonWreckForTests") &&
            leviathanText.Contains("ApplyContactAttackDamageForTests") &&
            leviathanText.Contains("ShouldAvoidCloudForTests") &&
            leviathanText.Contains("passiveRegenerationPercentPerSecond") &&
            sortieText.Contains("SpawnCoreTacticalLeviathans(center)") &&
            sortieText.Contains("BuildLeviathanProfile") &&
            sortieText.Contains("new Color(0.56f, 0.20f, 0.86f, 1f)") &&
            sortieText.Contains("leviathan.Aggression01") &&
            sortieText.Contains("inspectedLeviathan") &&
            sortieText.Contains("DrawLeviathanInfoPanel") &&
            sortieText.Contains("DrawInspectedShipInfoPanel") &&
            sortieText.Contains("DrawGasCloudInfoPanel") &&
            sortieText.Contains("SetInspectedLeviathanAsPriorityTarget") &&
            sortieText.Contains("IsShipTargetingInspectedLeviathan") &&
            sortieText.Contains("TryInspectScreenPointFromSelection") &&
            sortieText.Contains("TryInspectScreenRectFromSelection") &&
            sortieText.Contains("TryInspectHitFromSelection") &&
            sortieText.Contains("TryGetWorldObjectScreenRect") &&
            sortieText.Contains("InspectionPickMinimumHalfSizePixels") &&
            sortieText.Contains("for (int i = 0; i < enemyShips.Count; i++)") &&
            sortieText.Contains("for (int i = 0; i < gasClouds.Count; i++)") &&
            sortieText.Contains("InspectShip(enemy)") &&
            sortieText.Contains("InspectGasCloud(cloud)") &&
            sortieText.Contains("IsInspectableCombatShip") &&
            sortieText.Contains("ATTACK") &&
            sortieText.Contains("CLEAR") &&
            fleetText.Contains("TryInspectScreenRectFromSelection(selectionRect)") &&
            fleetText.Contains("TryInspectScreenPointFromSelection(mousePosition)") &&
            fleetText.Contains("TryInspectHitFromSelection(hit)") &&
            fleetText.Contains("GetComponent<CoreTacticalLeviathanController>() != null") &&
            oreMiningText.Contains("ConsumeByLeviathan") &&
            oreMiningText.Contains("ApproximateLengthMeters") &&
            oreMiningText.Contains("ChemicalDamagePerMinute") &&
            oreMiningText.Contains("DriftVelocityMS") &&
            oreMiningText.Contains("leviathan.IsAutomaticWeaponTarget") &&
            bootstrapText.Contains("createDeathExplosionVisual") &&
            leviathanText.Contains("health.createDeathExplosionVisual = false") &&
            bootstrapText.Contains("NotifyArtilleryReport(muzzlePosition, maxRangeMeters, owner)") &&
            bootstrapText.Contains("NotifyProjectileImpact(position, explosionRadiusMeters, 0.06f)") &&
            designText.Contains("Leviathan Baseline Combat Contract");
        report.Check(structuralOk,
            structuralOk
                ? "Core Tactical leviathan contract is documented and wired: purple predator spawns, aggression bar, inspection/attack panel, bite thresholds, too-large-prey retreat, direct-hit anger, stalking, wandering, boulder/wreck feeding and cloud avoidance are present in source."
                : "Core Tactical leviathan source wiring is incomplete: verify docs, spawn, purple/aggression HUD, inspection/attack panel, bite thresholds, too-large-prey retreat, direct-hit anger, stalking, wandering, feeding and cloud avoidance.");
#else
        report.Check(true, "Core Tactical leviathan source scan is editor-only and skipped in player builds.");
#endif

        GameObject root = null;
        CoreTacticalShipMotor leviathanShip = null;
        CoreTacticalShipMotor tinyTarget = null;
        CoreTacticalShipMotor mediumTarget = null;
        CoreTacticalShipMotor hugeTarget = null;
        CoreTacticalShipMotor smallLeviathanShip = null;
        GameObject attackSnackObject = null;
        GameObject fragmentObject = null;
        GameObject boulderSnackObject = null;
        GameObject wreckSnackObject = null;
        CoreTacticalShipMotor idleLeviathanShip = null;
        GameObject cloudObject = null;
        GameObject inspectCameraObject = null;
        RenderTexture inspectTexture = null;
        GameObject tinyInspectObject = null;
        try
        {
            root = new GameObject("Big Test Core Tactical Leviathan Contract Root");
            Vector3 origin = new Vector3(980000f, 980000f, 980000f);
            Color leviathanPurple = new Color(0.56f, 0.20f, 0.86f, 1f);

            leviathanShip = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                null,
                "big_test_leviathan_100m",
                "Big Test 100m Leviathan",
                origin,
                Quaternion.identity,
                new Vector3(24f, 15f, 100f),
                90f,
                32f,
                42f,
                26f,
                12f,
                12f,
                1400000f,
                false,
                leviathanPurple);
            leviathanShip.transform.SetParent(root.transform, true);
            CoreTacticalCombatant leviathanCombatant = leviathanShip.GetComponent<CoreTacticalCombatant>();
            if (leviathanCombatant != null)
            {
                leviathanCombatant.team = CoreTacticalCombatTeam.Enemy;
                leviathanCombatant.ship = leviathanShip;
            }

            CoreTacticalPrototypeHealth leviathanHealth = leviathanShip.GetComponent<CoreTacticalPrototypeHealth>();
            if (leviathanHealth != null)
            {
                leviathanHealth.maxHealth = 2000f;
                leviathanHealth.ResetHealth();
            }

            CoreTacticalDamageProfile leviathanDamageProfile = leviathanShip.GetComponent<CoreTacticalDamageProfile>();
            if (leviathanDamageProfile != null)
            {
                leviathanDamageProfile.ConfigureDefense(
                    "leviathan",
                    2000f,
                    CoreTacticalDamageProfile.ResolveClassBaselineResistances("leviathan"),
                    0f,
                    0f);
            }

            CoreTacticalLeviathanController leviathan = leviathanShip.gameObject.AddComponent<CoreTacticalLeviathanController>();
            leviathan.Configure(100f);
            leviathan.aggressionRadiusMeters = 900f;
            leviathan.closeAggressionRadiusMeters = 280f;
            bool calmAggressionCurveOk =
                Approximately(1f / Mathf.Max(0.001f, leviathan.aggressionDecayPerSecond), 30f, 0.01f) &&
                Approximately(leviathan.CalculateNetProximityAggressionRateForTests(0f), 0.125f, 0.004f) &&
                Approximately(1f / Mathf.Max(0.001f, leviathan.CalculateNetProximityAggressionRateForTests(0f)), 8f, 0.35f) &&
                Approximately(leviathan.CalculateNetProximityAggressionRateForTests(140f), 0.05f, 0.006f) &&
                Approximately(1f / Mathf.Max(0.001f, leviathan.CalculateNetProximityAggressionRateForTests(140f)), 20f, 2.5f) &&
                Approximately(leviathan.CalculateNetProximityAggressionRateForTests(450f), 0f, 0.001f) &&
                leviathan.artilleryReportAggression <= 0.021f &&
                Approximately(leviathan.directDamageAggression, 0.315f, 0.001f) &&
                leviathan.nearExplosionAggression <= 0.061f;
            bool leviathanDeathBurstDisabledOk = leviathanHealth != null && !leviathanHealth.createDeathExplosionVisual;

            tinyTarget = CreateLeviathanTestShip(
                root.transform,
                "big_test_tiny_bite_target",
                "Big Test Tiny Bite Target",
                origin + new Vector3(120f, 0f, 0f),
                new Vector3(6f, 4f, 20f),
                60000f,
                1000f,
                CoreTacticalCombatTeam.Friendly);
            mediumTarget = CreateLeviathanTestShip(
                root.transform,
                "big_test_medium_ram_target",
                "Big Test Medium Ram Target",
                origin + new Vector3(360f, 0f, 0f),
                new Vector3(12f, 7f, 60f),
                520000f,
                1800f,
                CoreTacticalCombatTeam.Friendly);
            hugeTarget = CreateLeviathanTestShip(
                root.transform,
                "big_test_huge_safe_target",
                "Big Test Huge Safe Target",
                origin + new Vector3(650f, 0f, 0f),
                new Vector3(70f, 36f, 360f),
                9000000f,
                15000f,
                CoreTacticalCombatTeam.Friendly);

            smallLeviathanShip = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                null,
                "big_test_small_leviathan_20m",
                "Big Test 20m Leviathan",
                origin + new Vector3(0f, 0f, 520f),
                Quaternion.identity,
                new Vector3(6f, 4f, 20f),
                120f,
                48f,
                58f,
                44f,
                18f,
                18f,
                25000f,
                false,
                leviathanPurple);
            smallLeviathanShip.transform.SetParent(root.transform, true);
            CoreTacticalLeviathanController smallLeviathan = smallLeviathanShip.gameObject.AddComponent<CoreTacticalLeviathanController>();
            smallLeviathan.Configure(20f);

            bool thresholdsOk =
                CoreTacticalLeviathanController.CanBiteTarget(100f, 34.9f) &&
                !CoreTacticalLeviathanController.CanBiteTarget(100f, 35.1f) &&
                CoreTacticalLeviathanController.IsLethalBiteTarget(100f, 19.9f) &&
                !CoreTacticalLeviathanController.IsLethalBiteTarget(100f, 20.1f) &&
                CoreTacticalLeviathanController.IsLeviathanTooSmallForTarget(29.9f, 100f) &&
                !CoreTacticalLeviathanController.IsLeviathanTooSmallForTarget(30.1f, 100f) &&
                CoreTacticalLeviathanController.CanEatBodyByLength(100f, 33.3f) &&
                !CoreTacticalLeviathanController.CanEatBodyByLength(100f, 34f);

            CoreTacticalPrototypeHealth tinyHealth = tinyTarget != null ? tinyTarget.GetComponent<CoreTacticalPrototypeHealth>() : null;
            float biteDamage = leviathan.ApplyAttackDamageForTests(tinyTarget, 44f);
            bool lethalBiteOk = biteDamage > 0f && tinyHealth != null && tinyHealth.currentHealth <= 0f;

            CoreTacticalPrototypeHealth mediumHealth = mediumTarget != null ? mediumTarget.GetComponent<CoreTacticalPrototypeHealth>() : null;
            float mediumBefore = mediumHealth != null ? mediumHealth.currentHealth : 0f;
            float slowRamDamage = leviathan.ApplyContactAttackDamageForTests(mediumTarget, 12f);
            float mediumAfterSlowRam = mediumHealth != null ? mediumHealth.currentHealth : 0f;
            float fastRamDamage = leviathan.ApplyContactAttackDamageForTests(mediumTarget, 44f);
            bool minimumRamSpeedOk = Approximately(slowRamDamage, 0f, 0.001f)
                && mediumHealth != null
                && Approximately(mediumAfterSlowRam, mediumBefore, 0.001f)
                && fastRamDamage > 0f
                && mediumHealth.currentHealth < mediumAfterSlowRam;
            if (mediumHealth != null)
            {
                mediumHealth.ResetHealth();
            }

            bool tooLargeRetreatOk = smallLeviathan.ShouldRetreatFromTargetForTests(hugeTarget);
            smallLeviathan.AddAggressionForTests(1f, hugeTarget);
            bool tooLargeDoesNotAggroOk = Approximately(smallLeviathan.Aggression01, 0f, 0.001f);
            bool calmNotAutomatic = !CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(leviathanCombatant, CoreTacticalCombatTeam.Enemy);
            smallLeviathan.NotifyDirectDamageForTests(mediumTarget);
            bool directDamageAggressionOk = smallLeviathan.CurrentTarget == mediumTarget
                && Approximately(smallLeviathan.Aggression01, 0.315f, 0.002f);

            Vector3 stalkCommandBefore = leviathanShip.TargetPosition;
            leviathan.AddAggressionForTests(0.2f, mediumTarget);
            leviathan.TickForTests(0.1f);
            bool stalkWhileBuildingOk = !leviathan.IsAttackCommitted
                && leviathan.CurrentTarget == mediumTarget
                && Vector3.Distance(leviathanShip.TargetPosition, stalkCommandBefore) > 1f;

            idleLeviathanShip = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
                null,
                "big_test_idle_leviathan_30m",
                "Big Test Idle Leviathan",
                origin + new Vector3(5000f, 0f, 5000f),
                Quaternion.identity,
                new Vector3(8f, 5f, 30f),
                115f,
                42f,
                54f,
                36f,
                16f,
                16f,
                65000f,
                false,
                leviathanPurple);
            idleLeviathanShip.transform.SetParent(root.transform, true);
            CoreTacticalLeviathanController idleLeviathan = idleLeviathanShip.gameObject.AddComponent<CoreTacticalLeviathanController>();
            idleLeviathan.Configure(30f);
            Vector3 idleCommandBefore = idleLeviathanShip.TargetPosition;
            idleLeviathan.ForceIdleWanderForTests();
            idleLeviathan.TickForTests(0.1f);
            bool idleWanderOk = Vector3.Distance(idleLeviathanShip.TargetPosition, idleCommandBefore) > 20f;

            bool explicitTargetOk = CoreTacticalOreTargetingRules.IsValidExplicitTarget(leviathanShip, CoreTacticalCombatTeam.Enemy);
            bool inspectableShipOk = CoreTacticalCombatSortieController.IsInspectableCombatShipForTests(leviathanShip)
                && !CoreTacticalCombatSortieController.IsInspectableCombatShipForTests(mediumTarget);
            attackSnackObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            attackSnackObject.name = "Big Test Leviathan Attack Priority Snack";
            attackSnackObject.transform.SetParent(root.transform, false);
            attackSnackObject.transform.position = leviathanShip.transform.position + new Vector3(10f, 0f, 10f);
            CoreTacticalOreFragment attackSnack = attackSnackObject.AddComponent<CoreTacticalOreFragment>();
            attackSnack.Initialize("windshale", "windshale_ore", "Windshale", 1000f, 0.10f, 3f, origin.y - 500f, Color.gray);
            if (leviathanHealth != null)
            {
                leviathanHealth.currentHealth = leviathanHealth.maxHealth * 0.45f;
            }

            leviathan.AddAggressionForTests(1f, mediumTarget);
            bool angryAutomatic = CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(leviathanCombatant, CoreTacticalCombatTeam.Enemy);
            leviathan.TickForTests(0.1f);
            bool attackCommitOk = leviathan.IsAttackCommitted
                && leviathan.CurrentTarget == mediumTarget
                && attackSnack.rawMassKg > 0f;
            DestroyBigTestObject(attackSnackObject);
            attackSnackObject = null;
            if (leviathanHealth != null)
            {
                leviathanHealth.ResetHealth();
            }

            CoreTacticalLeviathanController reportLeviathan = smallLeviathan;
            int reportAffected = CoreTacticalLeviathanController.NotifyArtilleryReport(
                smallLeviathanShip.transform.position + new Vector3(35f, 0f, 0f),
                300f,
                mediumTarget);
            bool artilleryReportOk = reportAffected > 0 && reportLeviathan.Aggression01 > 0f;

            int explosionAffected = CoreTacticalLeviathanController.NotifyProjectileImpact(leviathanShip.transform.position, 120f, 0.5f);
            bool projectileImpactOk = explosionAffected > 0 && leviathan.Aggression01 > 0f;

            fragmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fragmentObject.name = "Big Test Leviathan Ore Snack";
            fragmentObject.transform.SetParent(root.transform, false);
            fragmentObject.transform.position = leviathanShip.transform.position + new Vector3(10f, 0f, 0f);
            CoreTacticalOreFragment fragment = fragmentObject.AddComponent<CoreTacticalOreFragment>();
            fragment.Initialize("windshale", "windshale_ore", "Windshale", 1000f, 0.10f, 3f, origin.y - 500f, Color.gray);
            if (leviathanHealth != null)
            {
                leviathanHealth.currentHealth = 1000f;
            }

            float healthBeforeSnack = leviathanHealth != null ? leviathanHealth.currentHealth : 0f;
            bool ateFragment = leviathan.TryEatOreFragmentForTests(fragment);
            bool feedingOk = ateFragment
                && fragment.rawMassKg <= 0f
                && leviathanHealth != null
                && leviathanHealth.currentHealth > healthBeforeSnack;

            boulderSnackObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boulderSnackObject.name = "Big Test Leviathan Boulder Snack";
            boulderSnackObject.transform.SetParent(root.transform, false);
            boulderSnackObject.transform.position = leviathanShip.transform.position + new Vector3(16f, 0f, 0f);
            CoreTacticalPrototypeHealth boulderSnackHealth = boulderSnackObject.AddComponent<CoreTacticalPrototypeHealth>();
            CoreTacticalOreBoulder boulderSnack = boulderSnackObject.AddComponent<CoreTacticalOreBoulder>();
            boulderSnack.Initialize(new CoreTacticalOreBoulderDefinition
            {
                oreTypeId = "windshale",
                oreItemId = "windshale_ore",
                displayName = "Big Test Edible Boulder",
                sizeMeters = new Vector3(20f, 20f, 20f),
                densityKgPerCubicMeter = 3000f,
                healthPerDiameterMeter = 10f
            }, boulderSnackHealth, origin.y - 500f);
            if (leviathanHealth != null)
            {
                leviathanHealth.currentHealth = 1000f;
            }

            float healthBeforeBoulderSnack = leviathanHealth != null ? leviathanHealth.currentHealth : 0f;
            bool ateBoulder = leviathan.TryEatOreBoulderForTests(boulderSnack);
            bool boulderFeedingOk = ateBoulder
                && boulderSnack.PhysicalMassKg <= 0f
                && leviathanHealth != null
                && leviathanHealth.currentHealth > healthBeforeBoulderSnack;

            wreckSnackObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wreckSnackObject.name = "Big Test Leviathan Wreck Snack";
            wreckSnackObject.transform.SetParent(root.transform, false);
            wreckSnackObject.transform.position = leviathanShip.transform.position + new Vector3(22f, 0f, 0f);
            CoreTacticalAutomatonWreck wreckSnack = wreckSnackObject.AddComponent<CoreTacticalAutomatonWreck>();
            wreckSnack.Initialize(
                "Big Test Edible Wreck",
                12000f,
                140f,
                -20f,
                CoreTacticalAutomatonWreck.BuildDefaultManifest(20f),
                20f * 0.38f);
            if (leviathanHealth != null)
            {
                leviathanHealth.currentHealth = 1000f;
            }

            float healthBeforeWreckSnack = leviathanHealth != null ? leviathanHealth.currentHealth : 0f;
            bool ateWreck = leviathan.TryEatAutomatonWreckForTests(wreckSnack);
            bool wreckFeedingOk = ateWreck
                && wreckSnack.currentHealth <= 0f
                && leviathanHealth != null
                && leviathanHealth.currentHealth > healthBeforeWreckSnack;

            cloudObject = new GameObject("Big Test Leviathan Harmful Cloud");
            cloudObject.transform.SetParent(root.transform, false);
            cloudObject.transform.position = leviathanShip.transform.position;
            CoreTacticalGasCloud cloud = cloudObject.AddComponent<CoreTacticalGasCloud>();
            cloud.Initialize(new CoreTacticalGasCloudDefinition
            {
                condensateItemId = "cloud_condensate",
                displayName = "Big Test Harmful Cloud",
                rawVolumeLiters = 1000f,
                usefulVolumeLiters = 120f,
                chemicalDamagePerMinute = 60f,
                harvestable = true,
                lobeOffsets = new[] { Vector3.zero },
                lobeSizes = new[] { new Vector3(180f, 120f, 180f) }
            });
            float healthBeforeCloud = leviathanHealth != null ? leviathanHealth.currentHealth : 0f;
            cloud.ApplyChemicalContactDamageForTests(60f);
            bool cloudOk = leviathan.ShouldAvoidCloudForTests(cloud)
                && leviathanHealth != null
                && leviathanHealth.currentHealth < healthBeforeCloud;

            bool armorOk = leviathanDamageProfile != null
                && leviathanDamageProfile.KineticResistancePercent >= 40f
                && leviathanDamageProfile.ChemicalResistancePercent >= 90f
                && leviathanDamageProfile.ThermalResistancePercent >= 80f;
            bool purpleOk = Approximately(leviathanShip.normalColor.r, leviathanPurple.r, 0.001f)
                && Approximately(leviathanShip.normalColor.g, leviathanPurple.g, 0.001f)
                && Approximately(leviathanShip.normalColor.b, leviathanPurple.b, 0.001f);

            inspectCameraObject = new GameObject("Big Test Leviathan Selection Camera");
            inspectCameraObject.transform.SetParent(root.transform, false);
            inspectCameraObject.transform.position = origin + new Vector3(0f, 0f, -220f);
            inspectCameraObject.transform.rotation = Quaternion.identity;
            Camera inspectCamera = inspectCameraObject.AddComponent<Camera>();
            inspectCamera.orthographic = true;
            inspectCamera.orthographicSize = 80f;
            inspectCamera.nearClipPlane = 0.1f;
            inspectCamera.farClipPlane = 1000f;
            inspectTexture = new RenderTexture(800, 600, 16);
            inspectCamera.targetTexture = inspectTexture;

            tinyInspectObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tinyInspectObject.name = "Big Test Tiny Screen Pick Target";
            tinyInspectObject.transform.SetParent(root.transform, false);
            tinyInspectObject.transform.position = origin + new Vector3(0f, 0f, 40f);
            tinyInspectObject.transform.localScale = Vector3.one * 0.01f;
            bool tinyScreenPickOk = CoreTacticalCombatSortieController.TryGetWorldObjectScreenRectForTests(inspectCamera, tinyInspectObject, out Rect tinyScreenRect)
                && tinyScreenRect.width >= 24f
                && tinyScreenRect.height >= 24f
                && CoreTacticalCombatSortieController.BuildInspectionPickRectForTests(tinyScreenRect.center).Overlaps(tinyScreenRect, true);
            inspectCamera.targetTexture = null;
            inspectCamera.enabled = false;
            inspectCameraObject.SetActive(false);
            if (inspectTexture != null)
            {
                inspectTexture.Release();
                DestroyBigTestUnityObject(inspectTexture);
                inspectTexture = null;
            }

            bool behaviorOk = thresholdsOk
                && lethalBiteOk
                && calmAggressionCurveOk
                && leviathanDeathBurstDisabledOk
                && minimumRamSpeedOk
                && tooLargeRetreatOk
                && tooLargeDoesNotAggroOk
                && directDamageAggressionOk
                && stalkWhileBuildingOk
                && idleWanderOk
                && calmNotAutomatic
                && explicitTargetOk
                && inspectableShipOk
                && angryAutomatic
                && attackCommitOk
                && artilleryReportOk
                && projectileImpactOk
                && feedingOk
                && boulderFeedingOk
                && wreckFeedingOk
                && cloudOk
                && armorOk
                && purpleOk
                && tinyScreenPickOk;
            report.Check(behaviorOk,
                behaviorOk
                    ? "Core Tactical leviathan behavior works: bite/lethal thresholds, ram damage, death without ship burst, too-large-prey retreat, direct-hit anger, stalking while aggression builds, idle wandering, enemy click/box inspection pick area, hungry aggression commit, artillery/explosion wakeup, fragment/boulder/wreck feeding, cloud damage/avoidance, armor profile and purple identity are all covered."
                    : "Core Tactical leviathan behavior contract is broken: thresholds="
                        + thresholdsOk
                        + ", lethalBite="
                        + lethalBiteOk
                        + ", calmAggressionCurve="
                        + calmAggressionCurveOk
                        + ", deathBurstDisabled="
                        + leviathanDeathBurstDisabledOk
                        + ", minimumRamSpeed="
                        + minimumRamSpeedOk
                        + ", tooLargeRetreat="
                        + tooLargeRetreatOk
                        + ", tooLargeNoAggro="
                        + tooLargeDoesNotAggroOk
                        + ", directDamageAggression="
                        + directDamageAggressionOk
                        + ", stalkWhileBuilding="
                        + stalkWhileBuildingOk
                        + ", idleWander="
                        + idleWanderOk
                        + ", calmNotAuto="
                        + calmNotAutomatic
                        + ", explicitTarget="
                        + explicitTargetOk
                        + ", inspectableShip="
                        + inspectableShipOk
                        + ", angryAuto="
                        + angryAutomatic
                        + ", attackCommit="
                        + attackCommitOk
                        + ", artilleryReport="
                        + artilleryReportOk
                        + ", projectileImpact="
                        + projectileImpactOk
                        + ", feeding="
                        + feedingOk
                        + ", boulderFeeding="
                        + boulderFeedingOk
                        + ", wreckFeeding="
                        + wreckFeedingOk
                        + ", cloud="
                        + cloudOk
                        + ", armor="
                        + armorOk
                        + ", purple="
                        + purpleOk
                        + ", tinyScreenPick="
                        + tinyScreenPickOk
                        + ".");
        }
        finally
        {
            if (inspectTexture != null)
            {
                inspectTexture.Release();
                DestroyBigTestUnityObject(inspectTexture);
            }

            DestroyBigTestObject(root);
            if (root == null)
            {
                if (leviathanShip != null) DestroyBigTestObject(leviathanShip.gameObject);
                if (tinyTarget != null) DestroyBigTestObject(tinyTarget.gameObject);
                if (mediumTarget != null) DestroyBigTestObject(mediumTarget.gameObject);
                if (hugeTarget != null) DestroyBigTestObject(hugeTarget.gameObject);
                if (smallLeviathanShip != null) DestroyBigTestObject(smallLeviathanShip.gameObject);
                if (idleLeviathanShip != null) DestroyBigTestObject(idleLeviathanShip.gameObject);
                DestroyBigTestObject(attackSnackObject);
                DestroyBigTestObject(fragmentObject);
                DestroyBigTestObject(boulderSnackObject);
                DestroyBigTestObject(wreckSnackObject);
                DestroyBigTestObject(cloudObject);
                DestroyBigTestObject(inspectCameraObject);
                DestroyBigTestObject(tinyInspectObject);
            }
        }
    }

    private static CoreTacticalShipMotor CreateLeviathanTestShip(
        Transform parent,
        string shipId,
        string displayName,
        Vector3 position,
        Vector3 hullSizeMeters,
        float massKg,
        float healthHp,
        CoreTacticalCombatTeam team)
    {
        CoreTacticalShipMotor ship = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
            null,
            shipId,
            displayName,
            position,
            Quaternion.identity,
            hullSizeMeters,
            60f,
            20f,
            24f,
            28f,
            8f,
            6f,
            massKg,
            false,
            new Color(0.74f, 0.24f, 0.18f, 1f));
        if (parent != null)
        {
            ship.transform.SetParent(parent, true);
        }

        CoreTacticalCombatant combatant = ship.GetComponent<CoreTacticalCombatant>();
        if (combatant != null)
        {
            combatant.team = team;
            combatant.ship = ship;
        }

        CoreTacticalPrototypeHealth health = ship.GetComponent<CoreTacticalPrototypeHealth>();
        if (health != null)
        {
            health.maxHealth = Mathf.Max(1f, healthHp);
            health.ResetHealth();
        }

        return ship;
    }

    private static void ValidateCoreTacticalMiningEquipmentGates(BigTestReport report)
    {
        GameObject probe = null;
        GameObject cycleProbe = null;
        try
        {
            probe = new GameObject("Big Test Core Tactical Mining Equipment Gate Probe");
            CoreTacticalMiningRig rig = probe.AddComponent<CoreTacticalMiningRig>();
            rig.Initialize(null, null, CoreTacticalCombatTeam.Enemy, 1000f, 0f);
            rig.SetInstalledModules(true, false, false);

            bool magnetOnlyInstalled =
                rig.IsModuleInstalled(CoreTacticalMiningModule.Magnet) &&
                rig.IsModuleEnabled(CoreTacticalMiningModule.Magnet) &&
                !rig.IsModuleInstalled(CoreTacticalMiningModule.Drill) &&
                !rig.IsModuleInstalled(CoreTacticalMiningModule.Crusher) &&
                !rig.IsModuleInstalled(CoreTacticalMiningModule.Siphon) &&
                !rig.IsModuleInstalled(CoreTacticalMiningModule.CloudConcentrator) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Drill) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Crusher) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Siphon) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.CloudConcentrator);

            rig.ToggleModule(CoreTacticalMiningModule.Drill);
            rig.ToggleModule(CoreTacticalMiningModule.Crusher);
            rig.ToggleModule(CoreTacticalMiningModule.Siphon);
            rig.ToggleModule(CoreTacticalMiningModule.CloudConcentrator);
            bool absentModulesStayOff =
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Drill) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Crusher) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Siphon) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.CloudConcentrator);

            string statusLine = rig.BuildInstalledModuleStatusLine();
            bool statusShowsOnlyInstalledModule =
                statusLine.Contains("M ") &&
                !statusLine.Contains("L ") &&
                !statusLine.Contains("C ") &&
                !statusLine.Contains("S ") &&
                !statusLine.Contains("K ");

            rig.SetInstalledModules(false, false, false, true, true);
            bool gasModulesInstalled =
                !rig.IsModuleInstalled(CoreTacticalMiningModule.Magnet) &&
                !rig.IsModuleInstalled(CoreTacticalMiningModule.Drill) &&
                !rig.IsModuleInstalled(CoreTacticalMiningModule.Crusher) &&
                rig.IsModuleInstalled(CoreTacticalMiningModule.Siphon) &&
                rig.IsModuleInstalled(CoreTacticalMiningModule.CloudConcentrator) &&
                rig.IsModuleEnabled(CoreTacticalMiningModule.Siphon) &&
                rig.IsModuleEnabled(CoreTacticalMiningModule.CloudConcentrator);

            rig.ToggleModule(CoreTacticalMiningModule.Siphon);
            rig.ToggleModule(CoreTacticalMiningModule.CloudConcentrator);
            bool gasModulesToggleOff =
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Siphon) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.CloudConcentrator);

            rig.SetInstalledModules(false, false, false, true, true);
            bool gasModulesStayOffAfterLoadoutRefresh =
                rig.IsModuleInstalled(CoreTacticalMiningModule.Siphon) &&
                rig.IsModuleInstalled(CoreTacticalMiningModule.CloudConcentrator) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.Siphon) &&
                !rig.IsModuleEnabled(CoreTacticalMiningModule.CloudConcentrator);

            cycleProbe = new GameObject("Big Test Core Tactical Mining Module Cooldown Probe");
            CoreTacticalMiningRig cycleRig = cycleProbe.AddComponent<CoreTacticalMiningRig>();
            cycleRig.Initialize(null, null, CoreTacticalCombatTeam.Enemy, 1000f, 0f);
            cycleRig.SetInstalledModules(false, false, true, false, true);
            cycleRig.crusherCycleSeconds = 5f;
            cycleRig.cloudConcentratorCycleSeconds = 5f;
            bool addedDirtyOre = cycleRig.AddDirtyOreForTests("windshale_ore", 100f, 20f, "Windshale");
            bool addedCloudCondensate = cycleRig.AddRawCloudCondensateForTests("cloud_condensate", 100f, 20f, "Common Cloud");
            cycleRig.SetModuleCycleTimerForTests(CoreTacticalMiningModule.Crusher, 2.1f);
            cycleRig.SetModuleCycleTimerForTests(CoreTacticalMiningModule.CloudConcentrator, 2.1f);
            string crusherCooldownText = CoreTacticalCombatSortieController.GetMiningModuleCooldownTextForTests(cycleRig, CoreTacticalMiningModule.Crusher);
            string concentratorCooldownText = CoreTacticalCombatSortieController.GetMiningModuleCooldownTextForTests(cycleRig, CoreTacticalMiningModule.CloudConcentrator);
            float crusherShutter = CoreTacticalCombatSortieController.GetMiningModuleCooldownShutterForTests(cycleRig, CoreTacticalMiningModule.Crusher);
            float concentratorShutter = CoreTacticalCombatSortieController.GetMiningModuleCooldownShutterForTests(cycleRig, CoreTacticalMiningModule.CloudConcentrator);
            bool moduleCooldownsVisible =
                addedDirtyOre
                && addedCloudCondensate
                && crusherCooldownText == "3"
                && concentratorCooldownText == "3"
                && Approximately(crusherShutter, 2.9f / 5f, 0.001f)
                && Approximately(concentratorShutter, 2.9f / 5f, 0.001f);

            bool equipmentGateWorks = magnetOnlyInstalled && absentModulesStayOff && statusShowsOnlyInstalledModule && gasModulesInstalled && gasModulesToggleOff && gasModulesStayOffAfterLoadoutRefresh && moduleCooldownsVisible;
            report.Check(equipmentGateWorks,
                equipmentGateWorks
                    ? "Core Tactical mining equipment is loadout-gated: magnet, drill, crusher, siphon and cloud concentrator only exist when their loadout packages install them, installed gas modules can be toggled off, a repeated loadout refresh preserves the manual OFF state, and crusher/concentrator cycles surface a 5-second HUD cooldown."
                    : "Core Tactical mining equipment must be loadout-gated, must not re-enable a manually disabled siphon/concentrator during a repeated loadout refresh, and crusher/concentrator cycles must show HUD cooldown. Status: " + statusLine + ", crusherCooldown=" + crusherCooldownText + ", concentratorCooldown=" + concentratorCooldownText);
        }
        finally
        {
            DestroyBigTestObject(probe);
            DestroyBigTestObject(cycleProbe);
        }
    }

    private static void ValidateCoreTacticalMissionObjectiveGate(BigTestReport report)
    {
        string sortieText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCombatSortieController.cs");
        string shipMotorText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalShipMotor.cs");
        bool optionalMissionObjectivesWork =
            !CoreTacticalCombatSortieController.AreCoreMissionObjectivesCompleteForTests(3, 300f) &&
            !CoreTacticalCombatSortieController.AreCoreMissionObjectivesCompleteForTests(4, 299.9f) &&
            CoreTacticalCombatSortieController.AreCoreMissionObjectivesCompleteForTests(4, 300f);
        bool exitAlwaysWorks =
            sortieText.Contains("EnsureExitAvailable(zone)") &&
            sortieText.Contains("EnsureExitAvailable();") &&
            sortieText.Contains("available = !extractionComplete") &&
            sortieText.Contains("disabled = extractionComplete") &&
            sortieText.Contains("GUI.enabled = wasEnabled && !extractionComplete") &&
            sortieText.Contains("Optional bonus objectives: ") &&
            sortieText.Contains("CancelAutoExit()") &&
            sortieText.Contains("autoExitRequested = false;") &&
            sortieText.Contains("playerSlipDrive.SetArmed(false)") &&
            sortieText.Contains("playerShip.StopCommandAtCurrentPosition()") &&
            sortieText.Contains("autoExitRequested && extractionUnlocked") &&
            sortieText.Contains("ResolveCurrentExitDirection()") &&
            sortieText.Contains("ResolveCurrentExitCommandPosition(forward)") &&
            sortieText.Contains("ExitCommandBoundaryMarginMeters") &&
            sortieText.Contains("ExitCommandOutwardStepMeters") &&
            shipMotorText.Contains("StopCommandAtCurrentPosition") &&
            !sortieText.Contains("available = extractionUnlocked && !extractionComplete") &&
            !sortieText.Contains("disabled = !extractionUnlocked || extractionComplete") &&
            !sortieText.Contains("GUI.enabled = wasEnabled && extractionUnlocked && !extractionComplete") &&
            !sortieText.Contains("if (!extractionUnlocked || extractionComplete || playerShip == null)") &&
            !sortieText.Contains("playerShip.transform.position + forward * 20000f") &&
            !sortieText.Contains("Complete objectives: ");

        report.Check(optionalMissionObjectivesWork && exitAlwaysWorks,
            optionalMissionObjectivesWork && exitAlwaysWorks
                ? "Core Tactical mission treats 4 enemy kills and 300 kg of any ore as optional bonus objectives while AUTO EXIT stays available at any time, uses the nearest mission-circle exit direction, keeps sortie-entry slip separate from AUTO EXIT, and toggles off to restore ship control."
                : "Core Tactical mission exit must not be gated by optional enemy/ore objectives; keep AUTO EXIT available immediately, make it drive toward the nearest mission-circle exit instead of a stale map vector, keep sortie-entry slip separate from AUTO EXIT, and make repeated AUTO EXIT cancel the autopilot and restore ship control.");
    }

    private static void ValidateLowGradeOreConcentrateCargo(BigTestReport report)
    {
        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        progress.AddShipLowGradeOreCargo("windshale_ore", 100f, 10f, "Windshale");
        progress.AddShipLowGradeOreCargo("windshale_ore", 50f, 25f, "Windshale");
        LowGradeOreStackState shipStack = progress.GetShipLowGradeOreStack("windshale_ore", false);
        bool shipBlendOk = shipStack != null
            && Approximately(shipStack.rawMassKg, 150f, 0.001f)
            && Approximately(shipStack.usefulOreKg, 35f, 0.001f)
            && Approximately(shipStack.UsefulConcentration01, 35f / 150f, 0.0001f)
            && Approximately(progress.GetShipCargoMassKg(null), 150f, 0.001f);

        PortStorageState storage = new PortStorageState { portId = "capital" };
        storage.AddLowGradeOre("windshale_ore", 10000f, 800f, "Windshale");
        storage.AddLowGradeOre("windshale_ore", 12f, 1.2f, "Windshale");
        storage.AddLowGradeOre("windshale_ore", 20f, 4f, "Windshale");
        if (shipStack != null)
        {
            storage.AddLowGradeOre(shipStack.oreItemId, shipStack.rawMassKg, shipStack.usefulOreKg, shipStack.displayName);
        }

        LowGradeOreStackState portStack = storage.GetLowGradeOreStack("windshale_ore", false);
        float expectedRawKg = 10182f;
        float expectedUsefulKg = 840.2f;
        bool portBlendOk = portStack != null
            && Approximately(portStack.rawMassKg, expectedRawKg, 0.001f)
            && Approximately(portStack.usefulOreKg, expectedUsefulKg, 0.001f)
            && Approximately(portStack.UsefulConcentration01, expectedUsefulKg / expectedRawKg, 0.0001f);

        progress.ClearShipCargo();
        bool clearOk = progress.shipLowGradeOreCargo != null
            && progress.shipLowGradeOreCargo.Count == 0
            && progress.shipCargo != null
            && progress.shipCargo.Count == 0;

        report.Check(shipBlendOk && portBlendOk && clearOk,
            shipBlendOk && portBlendOk && clearOk
                ? "Low-grade ore concentrate persists as raw mass plus useful ore, blends by ore type on ship and in port, contributes cargo mass, and is cleared with ship cargo after unloading."
                : "Low-grade ore concentrate cargo must blend by raw/useful mass, count against ship cargo mass, persist into port stockpiles, and clear with ship cargo.");
    }

    private static void ValidateRawCloudCondensateCargo(BigTestReport report)
    {
        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        progress.AddShipRawCloudCondensateCargo("cloud_condensate", 100f, 20f, "Common Cloud");
        progress.AddShipRawCloudCondensateCargo("cloud_condensate", 50f, 25f, "Common Cloud");
        progress.AddShipRawCloudCondensateCargo("wet_condensate", 10f, 5f, "Wet Cloud");
        RawCloudCondensateStackState shipStack = progress.GetShipRawCloudCondensateStack("cloud_condensate", false);
        RawCloudCondensateStackState wetShipStack = progress.GetShipRawCloudCondensateStack("wet_condensate", false);
        bool shipBlendOk = shipStack != null
            && wetShipStack != null
            && Approximately(shipStack.rawLiters, 150f, 0.001f)
            && Approximately(shipStack.usefulLiters, 45f, 0.001f)
            && Approximately(shipStack.UsefulConcentration01, 45f / 150f, 0.0001f)
            && Approximately(wetShipStack.rawLiters, 10f, 0.001f)
            && Approximately(wetShipStack.usefulLiters, 5f, 0.001f)
            && Approximately(progress.GetShipCargoMassKg(null), 160f, 0.001f);

        PortStorageState storage = new PortStorageState { portId = "capital" };
        storage.AddRawCloudCondensate("cloud_condensate", 10000f, 800f, "Common Cloud");
        storage.AddRawCloudCondensate("cloud_condensate", 12f, 1.2f, "Common Cloud");
        if (shipStack != null)
        {
            storage.AddRawCloudCondensate(shipStack.condensateItemId, shipStack.rawLiters, shipStack.usefulLiters, shipStack.displayName);
        }

        RawCloudCondensateStackState portStack = storage.GetRawCloudCondensateStack("cloud_condensate", false);
        float expectedRawLiters = 10162f;
        float expectedUsefulLiters = 846.2f;
        bool portBlendOk = portStack != null
            && Approximately(portStack.rawLiters, expectedRawLiters, 0.001f)
            && Approximately(portStack.usefulLiters, expectedUsefulLiters, 0.001f)
            && Approximately(portStack.UsefulConcentration01, expectedUsefulLiters / expectedRawLiters, 0.0001f);

        float concentrationBeforeSpend = portStack != null ? portStack.UsefulConcentration01 : 0f;
        bool spendOk = portStack != null
            && storage.TrySpendRawCloudCondensate("cloud_condensate", 100f, out float spentUsefulLiters)
            && Approximately(spentUsefulLiters, 100f * concentrationBeforeSpend, 0.001f)
            && Approximately(portStack.rawLiters, expectedRawLiters - 100f, 0.001f);

        bool concentratorOk = false;
        bool capacityOk = false;
        bool liveSiphonOk = false;
        bool siphonIntakeVisualOk = false;
        bool emptyWaterCloudOk = false;
        bool gasDamageOk = false;
        bool singleEllipsoidCloudRenderOk = false;
        GameObject concentratorProbe = null;
        GameObject capacityProbe = null;
        GameObject liveSiphonRoot = null;
        GameObject emptyCloudObject = null;
        GameObject damageRoot = null;
        GameObject singleEllipsoidCloudObject = null;
        try
        {
            concentratorProbe = new GameObject("Big Test Raw Cloud Condensate Concentrator Probe");
            CoreTacticalMiningRig concentratorRig = concentratorProbe.AddComponent<CoreTacticalMiningRig>();
            concentratorRig.Initialize(null, null, CoreTacticalCombatTeam.Enemy, 1000f, 0f);
            concentratorRig.SetInstalledModules(false, false, false, false, true);
            concentratorRig.cloudConcentratorWaterLitersPerCycle = 20f;
            bool addedForConcentrator = concentratorRig.AddRawCloudCondensateForTests("cloud_condensate", 100f, 20f, "Common Cloud");
            float removedWaterLiters = concentratorRig.ProcessCloudConcentratorCycleForTests();
            concentratorOk = addedForConcentrator
                && Approximately(removedWaterLiters, 16f, 0.001f)
                && Approximately(concentratorRig.GetRawCloudCondensateRawLitersForTests("cloud_condensate"), 84f, 0.001f)
                && Approximately(concentratorRig.GetRawCloudCondensateConcentrationForTests("cloud_condensate"), 20f / 84f, 0.0001f);

            capacityProbe = new GameObject("Big Test Raw Cloud Condensate Capacity Probe");
            CoreTacticalMiningRig capacityRig = capacityProbe.AddComponent<CoreTacticalMiningRig>();
            capacityRig.Initialize(null, null, CoreTacticalCombatTeam.Enemy, 100f, 0f);
            capacityRig.SetInstalledModules(false, false, false, true, false);
            bool firstCapacityAdd = capacityRig.AddRawCloudCondensateForTests("cloud_condensate", 100f, 10f, "Common Cloud");
            bool rejectedCapacityAdd = !capacityRig.AddRawCloudCondensateForTests("cloud_condensate", 1f, 0.1f, "Common Cloud");
            capacityOk = firstCapacityAdd
                && rejectedCapacityAdd
                && !capacityRig.IsModuleEnabled(CoreTacticalMiningModule.Siphon)
                && capacityRig.GetModuleStatus(CoreTacticalMiningModule.Siphon) == "FULL";

            liveSiphonRoot = new GameObject("Big Test Raw Cloud Condensate Live Siphon Probe");
            Vector3 liveSiphonPosition = new Vector3(920000f, 920000f, 920000f);
            GameObject liveShipObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            liveShipObject.name = "Big Test Cloud Siphon Ship";
            liveShipObject.transform.SetParent(liveSiphonRoot.transform, false);
            liveShipObject.transform.position = liveSiphonPosition;
            Rigidbody liveShipBody = liveShipObject.GetComponent<Rigidbody>();
            if (liveShipBody == null)
            {
                liveShipBody = liveShipObject.AddComponent<Rigidbody>();
            }

            liveShipBody.useGravity = false;
            CoreTacticalShipMotor liveShip = liveShipObject.AddComponent<CoreTacticalShipMotor>();
            liveShip.InitializePrototypeShip("big_test_cloud_siphon_ship", "Big Test Cloud Siphon Ship", new Vector3(10f, 8f, 60f), Color.cyan);
            CoreTacticalMiningRig liveRig = liveShipObject.AddComponent<CoreTacticalMiningRig>();
            liveRig.Initialize(liveShip, null, CoreTacticalCombatTeam.Enemy, 1000f, 0f);
            liveRig.siphonLitersPerSecond = 40f;
            liveRig.siphonEnergyPerSecond = 0f;
            liveRig.siphonChannelCount = 2;
            liveRig.SetInstalledModules(false, false, false, true, false);

            GameObject liveCloudObject = new GameObject("Big Test Harvestable Cloud");
            liveCloudObject.transform.SetParent(liveSiphonRoot.transform, false);
            liveCloudObject.transform.position = liveSiphonPosition + Vector3.forward * 12f;
            CoreTacticalGasCloud harvestCloud = liveCloudObject.AddComponent<CoreTacticalGasCloud>();
            harvestCloud.Initialize(new CoreTacticalGasCloudDefinition
            {
                condensateItemId = "cloud_condensate",
                displayName = "Common Cloud",
                rawVolumeLiters = 500f,
                usefulVolumeLiters = 125f,
                chemicalDamagePerMinute = 0f,
                harvestable = true,
                lobeOffsets = new[] { Vector3.zero },
                lobeSizes = new[] { new Vector3(80f, 80f, 80f) }
            });

            GameObject liveEmptyCloudObject = new GameObject("Big Test Nearby Empty Water Cloud");
            liveEmptyCloudObject.transform.SetParent(liveSiphonRoot.transform, false);
            liveEmptyCloudObject.transform.position = liveSiphonPosition;
            CoreTacticalGasCloud liveEmptyCloud = liveEmptyCloudObject.AddComponent<CoreTacticalGasCloud>();
            liveEmptyCloud.Initialize(new CoreTacticalGasCloudDefinition
            {
                condensateItemId = "",
                displayName = "Nearby Empty Water Cloud",
                rawVolumeLiters = 900f,
                usefulVolumeLiters = 0f,
                chemicalDamagePerMinute = 0f,
                harvestable = false,
                lobeOffsets = new[] { Vector3.zero },
                lobeSizes = new[] { new Vector3(90f, 90f, 90f) }
            });

            liveRig.ToggleModule(CoreTacticalMiningModule.Siphon);
            float rawBeforeDisabledSiphon = harvestCloud.RawVolumeLiters;
            liveRig.SetInstalledModules(false, false, false, true, false);
            liveRig.UpdateSiphonForTests(1f);
            bool disabledSiphonDoesNotHarvest = Approximately(harvestCloud.RawVolumeLiters, rawBeforeDisabledSiphon, 0.001f)
                && Approximately(liveRig.GetRawCloudCondensateRawLitersForTests("cloud_condensate"), 0f, 0.001f)
                && !liveRig.IsModuleEnabled(CoreTacticalMiningModule.Siphon);
            liveRig.ToggleModule(CoreTacticalMiningModule.Siphon);
            liveRig.UpdateSiphonForTests(1f);
            CoreTacticalSiphonIntakeVisual intakeVisual = liveShipObject.GetComponentInChildren<CoreTacticalSiphonIntakeVisual>(true);
            LineRenderer[] intakeRings = intakeVisual != null ? intakeVisual.GetComponentsInChildren<LineRenderer>(true) : Array.Empty<LineRenderer>();
            int activeIntakeRingCount = 0;
            for (int i = 0; i < intakeRings.Length; i++)
            {
                if (intakeRings[i] != null && intakeRings[i].gameObject.activeSelf && intakeRings[i].positionCount >= 49)
                {
                    activeIntakeRingCount++;
                }
            }

            siphonIntakeVisualOk = intakeVisual != null
                && activeIntakeRingCount >= 4
                && liveShipObject.GetComponentInChildren<CoreTacticalUtilityBeamVisual>(true) == null;
            liveSiphonOk = disabledSiphonDoesNotHarvest
                && Approximately(harvestCloud.RawVolumeLiters, 420f, 0.001f)
                && Approximately(harvestCloud.UsefulVolumeLiters, 105f, 0.001f)
                && Approximately(liveRig.GetRawCloudCondensateRawLitersForTests("cloud_condensate"), 80f, 0.001f)
                && Approximately(liveRig.GetRawCloudCondensateConcentrationForTests("cloud_condensate"), 0.25f, 0.0001f)
                && Approximately(liveRig.TotalCloudCondensateCollectedLiters, 80f, 0.001f)
                && string.Equals(liveRig.activeSiphonTargetName, "Common Cloud", StringComparison.Ordinal)
                && Approximately(liveEmptyCloud.RawVolumeLiters, 900f, 0.001f)
                && siphonIntakeVisualOk;

            CoreTacticalGasCloudDefinition emptyWaterDefinition = new CoreTacticalGasCloudDefinition
            {
                condensateItemId = "",
                displayName = "Empty water cloud",
                rawVolumeLiters = 11000f,
                usefulVolumeLiters = 0f,
                chemicalDamagePerMinute = 12f,
                harvestable = false,
                lobeOffsets = new[] { Vector3.zero },
                lobeSizes = new[] { new Vector3(120f, 60f, 120f) }
            };
            emptyCloudObject = new GameObject("Big Test Empty Water Cloud");
            CoreTacticalGasCloud emptyCloud = emptyCloudObject.AddComponent<CoreTacticalGasCloud>();
            emptyCloud.Initialize(emptyWaterDefinition);
            float extractedEmptyRaw = emptyCloud.ExtractRawCondensate(100f, out float extractedEmptyUseful);
            emptyWaterCloudOk = !emptyCloud.CanHarvest
                && Approximately(extractedEmptyRaw, 0f, 0.001f)
                && Approximately(extractedEmptyUseful, 0f, 0.001f)
                && Approximately(emptyCloud.RawVolumeLiters, 11000f, 0.001f);

            singleEllipsoidCloudObject = new GameObject("Big Test Single Ellipsoid Transparent Cloud");
            CoreTacticalGasCloud singleEllipsoidCloud = singleEllipsoidCloudObject.AddComponent<CoreTacticalGasCloud>();
            singleEllipsoidCloud.Initialize(new CoreTacticalGasCloudDefinition
            {
                condensateItemId = "cloud_condensate",
                displayName = "Single Ellipsoid Cloud",
                rawVolumeLiters = 1000f,
                usefulVolumeLiters = 150f,
                chemicalDamagePerMinute = 0f,
                harvestable = true,
                lobeOffsets = new[] { Vector3.zero },
                lobeSizes = new[] { new Vector3(80f, 44f, 72f) }
            });
            singleEllipsoidCloudRenderOk = singleEllipsoidCloud.LobeRendererCountForTests == 1
                && singleEllipsoidCloud.UsesPlainTransparentCloudMaterialForTests;

            damageRoot = new GameObject("Big Test Raw Cloud Condensate Damage Probe Root");
            Vector3 isolatedPosition = new Vector3(900000f, 900000f, 900000f);
            GameObject shipObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shipObject.name = "Big Test Gas Contact Ship";
            shipObject.transform.SetParent(damageRoot.transform, false);
            shipObject.transform.position = isolatedPosition;
            CoreTacticalShipMotor ship = shipObject.AddComponent<CoreTacticalShipMotor>();
            ship.InitializePrototypeShip("big_test_gas_ship", "Big Test Gas Ship", new Vector3(10f, 8f, 60f), Color.gray);
            shipObject.AddComponent<CoreTacticalMiningRig>().SetInstalledModules(false, false, false, false, false);
            CoreTacticalPrototypeHealth health = shipObject.AddComponent<CoreTacticalPrototypeHealth>();
            health.maxHealth = 1000f;
            health.ResetHealth();
            CoreTacticalDamageProfile profile = shipObject.AddComponent<CoreTacticalDamageProfile>();
            profile.resistances = new CoreTacticalResistanceSet(0f, 0f, 50f, 0f);

            GameObject cloudObject = new GameObject("Big Test Gas Contact Cloud");
            cloudObject.transform.SetParent(damageRoot.transform, false);
            cloudObject.transform.position = isolatedPosition;
            CoreTacticalGasCloud cloud = cloudObject.AddComponent<CoreTacticalGasCloud>();
            cloud.Initialize(new CoreTacticalGasCloudDefinition
            {
                condensateItemId = "cloud_condensate",
                displayName = "Common Cloud",
                rawVolumeLiters = 1000f,
                usefulVolumeLiters = 100f,
                chemicalDamagePerMinute = 60f,
                harvestable = true,
                lobeOffsets = new[] { Vector3.zero },
                lobeSizes = new[] { new Vector3(100f, 100f, 100f) }
            });
            cloud.ApplyChemicalContactDamageForTests(60f);
            gasDamageOk = Approximately(health.currentHealth, 970f, 0.01f)
                && !ship.GetComponent<CoreTacticalMiningRig>().IsModuleInstalled(CoreTacticalMiningModule.Siphon);
        }
        finally
        {
            DestroyBigTestObject(concentratorProbe);
            DestroyBigTestObject(capacityProbe);
            DestroyBigTestObject(liveSiphonRoot);
            DestroyBigTestObject(emptyCloudObject);
            DestroyBigTestObject(damageRoot);
            DestroyBigTestObject(singleEllipsoidCloudObject);
        }

        progress.ClearShipCargo();
        bool clearOk = progress.shipRawCloudCondensateCargo != null
            && progress.shipRawCloudCondensateCargo.Count == 0
            && progress.shipCargo != null
            && progress.shipCargo.Count == 0;

        bool rawCloudCondensateOk = shipBlendOk
            && portBlendOk
            && spendOk
            && concentratorOk
            && capacityOk
            && liveSiphonOk
            && emptyWaterCloudOk
            && singleEllipsoidCloudRenderOk
            && gasDamageOk
            && clearOk;
        report.Check(rawCloudCondensateOk,
            rawCloudCondensateOk
                ? "Raw cloud condensate blends by condensate item as raw/useful liters, counts against ship cargo, unloads into port storage, can be spent by concentration, the cloud concentrator vents only water, full cargo disables siphon intake, a live enabled siphon drains harvestable clouds into ship inventory with visible inward intake rings instead of a beam, disabled siphons and empty water clouds do not harvest, clouds render as one plain transparent ellipsoid, and gas contact damage applies even without a siphon."
                : "Raw cloud condensate contract is broken: shipBlend="
                    + shipBlendOk
                    + ", portBlend="
                    + portBlendOk
                    + ", spend="
                    + spendOk
                    + ", concentrator="
                    + concentratorOk
                    + ", capacity="
                    + capacityOk
                    + ", liveSiphon="
                    + liveSiphonOk
                    + ", emptyWater="
                    + emptyWaterCloudOk
                    + ", singleEllipsoid="
                    + singleEllipsoidCloudRenderOk
                    + ", gasDamage="
                    + gasDamageOk
                    + ", clear="
                    + clearOk
                    + ".");
    }

    private static void ValidateCoreTacticalAutomatonWreckSalvage(BigTestReport report)
    {
        string oreMiningText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalOreMining.cs");
        string sortieText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCombatSortieController.cs");
        string bootstrapText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalPrototypeBootstrap.cs");
        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string damageProjectileText = ReadProjectText("Assets/Scripts/Systems/DamageProjectile.cs");
        string itemCsvText = ReadProjectText("Assets/Data/Config/Item.csv");
        string resourceCategoryCsvText = ReadProjectText("Assets/Data/Config/Resource_category.csv");
        string korshunAuxiliaryCsvText = ReadProjectText("Assets/Data/Config/Korshun_auxiliary_packages.csv");
        string barbetAuxiliaryCsvText = ReadProjectText("Assets/Data/Config/Barbet_auxiliary_packages.csv");

        bool structuralOk =
            oreMiningText.Contains("CoreTacticalAutomatonWreck") &&
            oreMiningText.Contains("CoreTacticalAutomatonWreckSpawner") &&
            oreMiningText.Contains("salvageManifest") &&
            oreMiningText.Contains("accessBuildupPercent") &&
            oreMiningText.Contains("TryExtractSalvage") &&
            oreMiningText.Contains("spawnDamageGraceSeconds") &&
            oreMiningText.Contains("despawnAfterSeconds = 60f") &&
            oreMiningText.Contains("uncapturedLifetimeSeconds") &&
            oreMiningText.Contains("CoreTacticalFlakBurstVisual.Create") &&
            oreMiningText.Contains("salvageWreckRangeMeters = 1000f") &&
            oreMiningText.Contains("salvageHoldDistanceExtraMeters = 50f") &&
            oreMiningText.Contains("salvageWreckTowForceKg = 200f") &&
            oreMiningText.Contains("salvageWreckHoldForceMultiplier = 10f") &&
            oreMiningText.Contains("Mathf.Min(0.1f") &&
            oreMiningText.Contains("CoreTacticalUtilityBeamPalette.Salvage") &&
            oreMiningText.Contains("- ownerShip.transform.forward * aftOffset") &&
            oreMiningText.Contains("ReleaseWreckSalvage") &&
            oreMiningText.Contains("salvageMagnetInstalled") &&
            sortieText.Contains("SpawnSmallAutomatons") &&
            sortieText.Contains("float[] sizesMeters = { 5f, 10f, 20f }") &&
            sortieText.Contains("ConfigureAutomatonWreckSpawner") &&
            sortieText.Contains("EstimateAutomatonWreckMassKg") &&
            sortieText.Contains("size <= 5.5f ? -50f : size <= 180f ? -20f : 20f") &&
            sortieText.Contains("RuntimeLoadoutKind.None") &&
            sortieText.Contains("kind == \"salvage_magnet\"") &&
            bootstrapText.Contains("normalizedKind.Contains(\"magnet\")") &&
            bootstrapText.Contains("GetComponent<CoreTacticalAutomatonWreckSpawner>()") &&
            bootstrapText.Contains("TrySphereCastWreck") &&
            bootstrapText.Contains("ApplyExplosionDamageToWrecks") &&
            damageProjectileText.Contains("CoreTacticalAutomatonWreck") &&
            metaText.Contains("automaton_salvage") &&
            itemCsvText.Contains("automaton_relay") &&
            itemCsvText.Contains("automaton_servo_joint") &&
            resourceCategoryCsvText.Contains("automaton_salvage") &&
            korshunAuxiliaryCsvText.Contains("korshun_aux_salvage_magnet") &&
            korshunAuxiliaryCsvText.Contains(",auxiliary,salvage_magnet,") &&
            korshunAuxiliaryCsvText.Contains("1000,22") &&
            korshunAuxiliaryCsvText.Contains("20,5") &&
            barbetAuxiliaryCsvText.Contains("barbet_small_salvage_magnet");
        report.Check(structuralOk,
            structuralOk
                ? "Core Tactical automaton wreck salvage is structurally wired: 5/10/20m automatons spawn, deaths create HP wrecks with fixed large-part salvage manifests, simple automatons have easy access difficulty, free wrecks have 60-second visual despawn, Korshun/Barbet salvage magnets expose selectable equipment slots with sane energy costs, salvage beams have a 1000m wreck range, stern-tow force physics, at least 10x captured fall slowdown, a separate salvage beam palette, access buildup cycles, projectile collisions, and automaton_salvage runtime cargo."
                : "Automaton wreck salvage wiring is incomplete: verify small automaton spawn, death-to-wreck spawner, fixed large-part manifest extraction, easy current access difficulty, 60-second free-wreck despawn visual, Korshun/Barbet salvage_magnet equipment/energy, stern tow force, at least 10x captured fall slowdown, salvage beam color, projectile collision, and automaton_salvage cargo acceptance.");

        bool manifestAndAccessOk = false;
        bool liveSalvageOk = false;
        bool cruiserWreckTowOk = false;
        bool breakOk = false;
        bool lifetimeDespawnOk = false;
        bool projectileCollisionOk = false;
        bool freshWreckGraceOk = false;
        GameObject root = null;
        GameObject accessObject = null;
        GameObject fallbackObject = null;
        GameObject deathObject = null;
        GameObject freshWreckObject = null;
        GameObject shipObject = null;
        GameObject wreckObject = null;
        GameObject cruiserWreckObject = null;
        GameObject breakWreckObject = null;
        GameObject lifetimeWreckObject = null;
        GameObject lifetimeDamageProbeObject = null;
        GameObject collisionWreckObject = null;
        try
        {
            root = new GameObject("Big Test Automaton Wreck Salvage Root");
            Vector3 basePosition = new Vector3(940000f, 940000f, 940000f);

            accessObject = new GameObject("Big Test Automaton Access Wreck");
            accessObject.transform.SetParent(root.transform, false);
            accessObject.transform.position = basePosition + Vector3.up * 200f;
            CoreTacticalAutomatonWreck accessWreck = accessObject.AddComponent<CoreTacticalAutomatonWreck>();
            accessWreck.Initialize(
                "Access Wreck",
                100f,
                50f,
                50f,
                new List<CoreTacticalInventoryRow>
                {
                    new CoreTacticalInventoryRow { itemId = "automaton_relay", displayName = "Automaton relay", amountKg = 0.8f }
                },
                2f);
            bool failedWithoutConsuming = !accessWreck.TryExtractSalvage(
                    20f,
                    7f,
                    out _,
                    out bool failedEmpty,
                    out float failChance)
                && !failedEmpty
                && Approximately(failChance, 0f, 0.001f)
                && accessWreck.RemainingSalvageCount == 1
                && Approximately(accessWreck.AccessBuildupPercent, 7f, 0.001f);
            bool successConsumesOne = accessWreck.TryExtractSalvage(
                    150f,
                    7f,
                    out CoreTacticalInventoryRow extracted,
                    out bool emptied,
                    out float successChance)
                && emptied
                && successChance >= 99.9f
                && extracted.itemId == "automaton_relay"
                && accessWreck.RemainingSalvageCount == 0
                && Approximately(accessWreck.AccessBuildupPercent, 0f, 0.001f);
            List<CoreTacticalInventoryRow> default5mManifest = CoreTacticalAutomatonWreck.BuildDefaultManifest(5f);
            List<CoreTacticalInventoryRow> default20mManifest = CoreTacticalAutomatonWreck.BuildDefaultManifest(20f);
            List<CoreTacticalInventoryRow> default150mManifest = CoreTacticalAutomatonWreck.BuildDefaultManifest(150f);
            float default5mKg = SumInventoryRowsKg(default5mManifest);
            float default20mKg = SumInventoryRowsKg(default20mManifest);
            float default150mKg = SumInventoryRowsKg(default150mManifest);
            bool defaultManifestOk = default20mManifest.Count >= 7
                && default20mManifest.Exists(row => row.itemId == "automaton_relay")
                && default20mManifest.Exists(row => row.itemId == "automaton_servo_joint")
                && default20mManifest.Exists(row => row.itemId == "automaton_command_cylinder")
                && default5mKg >= 80f
                && default20mKg >= 2500f
                && default150mKg >= 20000f;
            fallbackObject = new GameObject("Big Test Empty Manifest Automaton Wreck");
            fallbackObject.transform.SetParent(root.transform, false);
            CoreTacticalAutomatonWreck fallbackWreck = fallbackObject.AddComponent<CoreTacticalAutomatonWreck>();
            fallbackWreck.Initialize(
                "Empty Manifest Wreck",
                50f,
                30f,
                -50f,
                Array.Empty<CoreTacticalInventoryRow>(),
                1.5f);
            bool fallbackManifestOk = fallbackWreck.RemainingSalvageCount == 1
                && fallbackWreck.TryExtractSalvage(
                    150f,
                    0f,
                    out CoreTacticalInventoryRow fallbackRow,
                    out bool fallbackEmptied,
                    out _)
                && fallbackEmptied
                && fallbackRow.itemId == "automaton_relay"
                && fallbackRow.amountKg >= 35f;

            deathObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deathObject.name = "Big Test Fresh Death Automaton";
            deathObject.transform.SetParent(root.transform, false);
            deathObject.transform.position = basePosition + Vector3.up * 260f;
            CoreTacticalPrototypeHealth deathHealth = deathObject.AddComponent<CoreTacticalPrototypeHealth>();
            deathHealth.maxHealth = 10f;
            deathHealth.ResetHealth();
            CoreTacticalAutomatonWreckSpawner deathSpawner = deathObject.AddComponent<CoreTacticalAutomatonWreckSpawner>();
            deathSpawner.Configure(
                "Fresh Death Wreck",
                50f,
                30f,
                -50f,
                1.5f,
                Array.Empty<CoreTacticalInventoryRow>());
            deathHealth.ApplyDamage(50f, "Big Test lethal explosion");
            Physics.SyncTransforms();
            CoreTacticalAutomatonWreck[] spawnedWrecks = UnityEngine.Object.FindObjectsByType<CoreTacticalAutomatonWreck>(FindObjectsSortMode.None);
            CoreTacticalAutomatonWreck freshWreck = null;
            for (int i = 0; i < spawnedWrecks.Length; i++)
            {
                if (spawnedWrecks[i] != null && string.Equals(spawnedWrecks[i].displayName, "Fresh Death Wreck", StringComparison.Ordinal))
                {
                    freshWreck = spawnedWrecks[i];
                    break;
                }
            }

            float freshHealthBefore = freshWreck != null ? freshWreck.currentHealth : -1f;
            int freshExplosionHits = CoreTacticalAutomatonWreck.ApplyExplosionDamageToWrecks(
                deathObject.transform.position,
                10f,
                100f,
                "Big Test same-frame explosion");
            freshWreckGraceOk = freshWreck != null
                && freshExplosionHits >= 1
                && Approximately(freshWreck.currentHealth, freshHealthBefore, 0.001f)
                && freshWreck.RemainingSalvageCount >= 1;
            freshWreckObject = freshWreck != null ? freshWreck.gameObject : null;
            if (freshWreckObject != null)
            {
                freshWreckObject.SetActive(false);
            }

            manifestAndAccessOk = failedWithoutConsuming && successConsumesOne && defaultManifestOk && fallbackManifestOk;

            shipObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shipObject.name = "Big Test Automaton Salvage Ship";
            shipObject.transform.SetParent(root.transform, false);
            shipObject.transform.SetPositionAndRotation(basePosition, Quaternion.identity);
            Rigidbody shipBody = shipObject.GetComponent<Rigidbody>();
            if (shipBody == null)
            {
                shipBody = shipObject.AddComponent<Rigidbody>();
            }

            shipBody.useGravity = false;
            CoreTacticalShipMotor ship = shipObject.AddComponent<CoreTacticalShipMotor>();
            ship.InitializePrototypeShip("big_test_salvage_ship", "Big Test Salvage Ship", new Vector3(12f, 8f, 80f), Color.cyan);
            CoreTacticalMiningRig rig = shipObject.AddComponent<CoreTacticalMiningRig>();
            rig.Initialize(ship, null, CoreTacticalCombatTeam.Enemy, 1000f, 0f);
            rig.SetInstalledModules(true, false, false);
            rig.salvageMagnetInstalled = true;
            rig.magnetRangeMeters = 1000f;
            rig.salvageWreckRangeMeters = 1000f;
            rig.salvageWreckEnergyPerSecond = 0f;
            rig.salvageWreckCycleSeconds = 0.1f;
            rig.salvageAccessRatingPercent = 150f;
            rig.salvageWreckTowForceKg = 200f;
            rig.salvageWreckHoldForceMultiplier = 10f;

            wreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wreckObject.name = "Big Test Live Automaton Salvage Wreck";
            wreckObject.transform.SetParent(root.transform, false);
            wreckObject.transform.position = basePosition + Vector3.right * 120f;
            CoreTacticalAutomatonWreck liveWreck = wreckObject.AddComponent<CoreTacticalAutomatonWreck>();
            liveWreck.Initialize(
                "Live Salvage Wreck",
                100f,
                60f,
                -10f,
                new List<CoreTacticalInventoryRow>
                {
                    new CoreTacticalInventoryRow { itemId = "automaton_relay", displayName = "Automaton relay", amountKg = 0.8f }
                },
                2f);
            Physics.SyncTransforms();
            rig.UpdateMagnetForTests(0.05f);
            bool capturedLiveWreck = liveWreck.captured && string.Equals(rig.activeSalvageTargetName, "Live Salvage Wreck", StringComparison.Ordinal);
            bool liveTowStartsBehind = liveWreck.transform.position.z < basePosition.z - 0.01f;
            for (int i = 0; i < 4; i++)
            {
                rig.UpdateMagnetForTests(0.11f);
            }

            liveSalvageOk = capturedLiveWreck
                && liveTowStartsBehind
                && rig.GetAutomatonSalvageUnitsForTests("automaton_relay") == 1
                && Approximately(rig.GetAutomatonSalvageKgForTests("automaton_relay"), 0.8f, 0.001f)
                && liveWreck.RemainingSalvageCount == 0;

            cruiserWreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cruiserWreckObject.name = "Big Test Cruiser Automaton Salvage Wreck";
            cruiserWreckObject.transform.SetParent(root.transform, false);
            cruiserWreckObject.transform.position = basePosition + Vector3.right * 180f;
            CoreTacticalAutomatonWreck cruiserWreck = cruiserWreckObject.AddComponent<CoreTacticalAutomatonWreck>();
            cruiserWreck.Initialize(
                "Cruiser Salvage Wreck",
                45000f,
                500f,
                -20f,
                CoreTacticalAutomatonWreck.BuildDefaultManifest(150f),
                57f);
            rig.salvageWreckCycleSeconds = 100f;
            Physics.SyncTransforms();
            Vector3 cruiserStart = cruiserWreck.transform.position;
            rig.UpdateMagnetForTests(0.05f);
            bool capturedCruiserWreck = cruiserWreck.captured && string.Equals(rig.activeSalvageTargetName, "Cruiser Salvage Wreck", StringComparison.Ordinal);
            rig.UpdateMagnetForTests(1f);
            float cruiserTowDistance = Vector3.Distance(cruiserStart, cruiserWreck.transform.position);
            bool cruiserTowIsMassLimited = cruiserTowDistance > 0.05f && cruiserTowDistance < 2f;
            bool cruiserHoldSlowsAtLeastTenfold = cruiserWreck.capturedFallSpeedMultiplier <= 0.1001f;
            shipObject.transform.position = basePosition + Vector3.forward * 5000f;
            Physics.SyncTransforms();
            rig.UpdateMagnetForTests(0.05f);
            shipObject.transform.position = basePosition;
            Physics.SyncTransforms();
            cruiserWreckTowOk = capturedCruiserWreck
                && cruiserTowIsMassLimited
                && cruiserHoldSlowsAtLeastTenfold
                && !cruiserWreck.captured
                && cruiserWreck.RemainingSalvageCount >= 7;

            breakWreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            breakWreckObject.name = "Big Test Breakable Automaton Salvage Wreck";
            breakWreckObject.transform.SetParent(root.transform, false);
            breakWreckObject.transform.position = basePosition + Vector3.right * 140f;
            CoreTacticalAutomatonWreck breakWreck = breakWreckObject.AddComponent<CoreTacticalAutomatonWreck>();
            breakWreck.Initialize(
                "Breakable Wreck",
                100f,
                60f,
                -10f,
                new List<CoreTacticalInventoryRow>
                {
                    new CoreTacticalInventoryRow { itemId = "automaton_coil", displayName = "Automaton coil", amountKg = 1.1f }
                },
                2f);
            rig.salvageWreckCycleSeconds = 100f;
            Physics.SyncTransforms();
            rig.UpdateMagnetForTests(0.05f);
            bool capturedBreakWreck = breakWreck.captured;
            shipObject.transform.position = basePosition + Vector3.right * 5000f;
            Physics.SyncTransforms();
            rig.UpdateMagnetForTests(0.05f);
            breakOk = capturedBreakWreck
                && !breakWreck.captured
                && breakWreck.RemainingSalvageCount == 1
                && rig.LastInventoryMessage.Contains("left beam range");

            lifetimeWreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lifetimeWreckObject.name = "Big Test Lifetime Automaton Salvage Wreck";
            lifetimeWreckObject.transform.SetParent(root.transform, false);
            lifetimeWreckObject.transform.position = basePosition + Vector3.left * 140f;
            CoreTacticalAutomatonWreck lifetimeWreck = lifetimeWreckObject.AddComponent<CoreTacticalAutomatonWreck>();
            lifetimeWreck.Initialize(
                "Lifetime Wreck",
                100f,
                60f,
                -10f,
                new List<CoreTacticalInventoryRow>
                {
                    new CoreTacticalInventoryRow { itemId = "automaton_relay", displayName = "Automaton relay", amountKg = 1.2f }
                },
                2f);
            lifetimeWreck.despawnAfterSeconds = 1f;
            lifetimeDamageProbeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lifetimeDamageProbeObject.name = "Big Test Lifetime Wreck No-Damage Probe";
            lifetimeDamageProbeObject.transform.SetParent(root.transform, false);
            lifetimeDamageProbeObject.transform.position = lifetimeWreckObject.transform.position + Vector3.right * 1.5f;
            CoreTacticalPrototypeHealth lifetimeDamageProbe = lifetimeDamageProbeObject.AddComponent<CoreTacticalPrototypeHealth>();
            lifetimeDamageProbe.maxHealth = 100f;
            lifetimeDamageProbe.ResetHealth();
            lifetimeWreck.AdvanceLifetimeForTests(0.45f);
            bool lifetimeTicksWhileFree = lifetimeWreck.currentHealth > 0.001f
                && Approximately(lifetimeWreck.UncapturedLifetimeSecondsForTests, 0.45f, 0.001f);
            lifetimeWreck.SetCaptured(true, "Big Test beam", 0.1f);
            lifetimeWreck.AdvanceLifetimeForTests(5f);
            bool lifetimeStopsWhileCaptured = lifetimeWreck.currentHealth > 0.001f
                && lifetimeWreck.captured
                && Approximately(lifetimeWreck.UncapturedLifetimeSecondsForTests, 0f, 0.001f);
            lifetimeWreck.SetCaptured(false);
            lifetimeWreck.AdvanceLifetimeForTests(0.99f);
            bool lifetimeRestartsAfterRelease = lifetimeWreck.currentHealth > 0.001f
                && !lifetimeWreck.captured
                && Approximately(lifetimeWreck.UncapturedLifetimeSecondsForTests, 0.99f, 0.001f);
            lifetimeWreck.AdvanceLifetimeForTests(0.02f);
            lifetimeDespawnOk = lifetimeTicksWhileFree
                && lifetimeStopsWhileCaptured
                && lifetimeRestartsAfterRelease
                && lifetimeWreck.ExpiredByLifetimeForTests
                && lifetimeWreck.currentHealth <= 0.001f
                && Approximately(lifetimeDamageProbe.currentHealth, 100f, 0.001f);

            collisionWreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            collisionWreckObject.name = "Big Test Projectile Collision Automaton Wreck";
            collisionWreckObject.transform.SetParent(root.transform, false);
            collisionWreckObject.transform.position = basePosition + Vector3.forward * 280f;
            CoreTacticalAutomatonWreck collisionWreck = collisionWreckObject.AddComponent<CoreTacticalAutomatonWreck>();
            collisionWreck.Initialize(
                "Projectile Collision Wreck",
                100f,
                40f,
                0f,
                CoreTacticalAutomatonWreck.BuildDefaultManifest(5f),
                2f);
            collisionWreck.DisableSpawnDamageGraceForTests();
            Physics.SyncTransforms();
            bool sphereCastHitsWreck = CoreTacticalAutomatonWreck.TrySphereCastWreck(
                collisionWreckObject.transform.position + Vector3.left * 20f,
                collisionWreckObject.transform.position + Vector3.right * 20f,
                1f,
                out CoreTacticalAutomatonWreck hitWreck,
                out _)
                && hitWreck == collisionWreck;
            int damagedWrecks = CoreTacticalAutomatonWreck.ApplyExplosionDamageToWrecks(
                collisionWreckObject.transform.position,
                12f,
                25f,
                "Big Test explosion");
            bool explosionDamagesWreck = collisionWreck.currentHealth < 40f;
            collisionWreck.ApplyProjectileDamage(100f, "Big Test direct hit", collisionWreckObject.transform.position);
            projectileCollisionOk = sphereCastHitsWreck
                && damagedWrecks >= 1
                && explosionDamagesWreck
                && collisionWreck.currentHealth <= 0.001f;
        }
        finally
        {
            DestroyBigTestObject(accessObject);
            DestroyBigTestObject(fallbackObject);
            DestroyBigTestObject(deathObject);
            DestroyBigTestObject(freshWreckObject);
            DestroyBigTestObject(wreckObject);
            DestroyBigTestObject(cruiserWreckObject);
            DestroyBigTestObject(breakWreckObject);
            DestroyBigTestObject(lifetimeWreckObject);
            DestroyBigTestObject(lifetimeDamageProbeObject);
            DestroyBigTestObject(collisionWreckObject);
            DestroyBigTestObject(shipObject);
            DestroyBigTestObject(root);
        }

        bool functionalOk = manifestAndAccessOk && liveSalvageOk && cruiserWreckTowOk && breakOk && lifetimeDespawnOk && projectileCollisionOk && freshWreckGraceOk;
        report.Check(functionalOk,
            functionalOk
                ? "Automaton wreck salvage functional contract holds: fixed manifests do not lose loot on failed access, empty manifests get a heavy fallback relay, failures add buildup, a salvage magnet captures and strips a small wreck into cargo from a stern tow point, cruiser wrecks can be caught, move slowly under 200 kg tow force, and fall at least 10x slower while held, the beam breaks when the ship outruns range, free wrecks visually expire after their lifetime while held wrecks pause and restart the timer after release, fresh death wrecks survive the same lethal explosion, and wreck HP intercepts later projectile/explosion damage."
                : "Automaton wreck salvage functional contract failed: verify fixed-manifest access buildup, heavy fallback loot, live salvage capture/extraction, stern tow, cruiser wreck capture/slow tow and at least 10x held fall slowdown, beam range break/release, free-wreck lifetime despawn pause/restart, fresh death wreck grace, and projectile/explosion damage against wreck HP. Parts: manifest="
                    + manifestAndAccessOk
                    + ", live="
                    + liveSalvageOk
                    + ", cruiserTow="
                    + cruiserWreckTowOk
                    + ", break="
                    + breakOk
                    + ", lifetime="
                    + lifetimeDespawnOk
                    + ", freshGrace="
                    + freshWreckGraceOk
                    + ", projectile="
                    + projectileCollisionOk
                    + ".");
    }

    private static void ValidateCoreTacticalHarpoonBehavior(BigTestReport report)
    {
#if UNITY_EDITOR
        string harpoonText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalHarpoon.cs");
        string sortieText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCombatSortieController.cs");
        string bootstrapText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalPrototypeBootstrap.cs");
        string korshunAuxiliaryCsvText = ReadProjectText("Assets/Data/Config/Korshun_auxiliary_packages.csv");
        string barbetAuxiliaryCsvText = ReadProjectText("Assets/Data/Config/Barbet_auxiliary_packages.csv");
        bool structuralOk =
            harpoonText.Contains("CoreTacticalHarpoonLauncher") &&
            harpoonText.Contains("CoreTacticalHarpoonProjectile") &&
            harpoonText.Contains("CoreTacticalHarpoonLink") &&
            harpoonText.Contains("CoreTacticalDamageType.Kinetic") &&
            harpoonText.Contains("DefaultProjectileRadiusMeters = 8f") &&
            harpoonText.Contains("DefaultFlightCableMaxLengthMeters = 400f") &&
            harpoonText.Contains("DefaultCableLifetimeSeconds = 10f") &&
            harpoonText.Contains("DefaultCableMinimumLengthMeters = 50f") &&
            harpoonText.Contains("DefaultCableWinchSpeedMS = 10f") &&
            harpoonText.Contains("DefaultProjectileReturnSpeedMS = 100f") &&
            harpoonText.Contains("DesignateTarget(") &&
            harpoonText.Contains("TryLaunchAtDesignatedTarget") &&
            harpoonText.Contains("CanLaunchAtTarget") &&
            harpoonText.Contains("CreateTargeted") &&
            harpoonText.Contains("flightCableLine") &&
            harpoonText.Contains("flightCableMaxLengthMeters") &&
            harpoonText.Contains("CableRangeMeters") &&
            harpoonText.Contains("maximumCableLengthMeters") &&
            harpoonText.Contains("EnforceMaximumCableLength") &&
            harpoonText.Contains("FinishOrReturn(attached)") &&
            harpoonText.Contains("BeginReturnToLauncher") &&
            harpoonText.Contains("ManualAimSectorDegrees => Mathf.Clamp(manualAimSectorDegrees, 1f, 185f)") &&
            harpoonText.Contains("ownerTransform.right * safeSide") &&
            harpoonText.Contains("initialDistance > CableRangeMeters") &&
            harpoonText.Contains("Mathf.MoveTowards(restLengthMeters, minimumRestLengthMeters, winchSpeedMS * deltaSeconds)") &&
            harpoonText.Contains("remainingDurabilitySeconds = Mathf.Max(0f, remainingDurabilitySeconds - deltaSeconds)") &&
            harpoonText.Contains("CalculateLeadTargetPoint") &&
            harpoonText.Contains("targetBody.linearVelocity") &&
            harpoonText.Contains("targetLocalAnchor") &&
            harpoonText.Contains("launcher.GetCableAnchorPosition()") &&
            harpoonText.Contains("distance * 0.06f") &&
            !harpoonText.Contains("MaxWearMultiplier") &&
            !harpoonText.Contains("TensionDamageTickSeconds") &&
            !harpoonText.Contains("ApplyWearAndDamage") &&
            !harpoonText.Contains("tensionDamagePerWearSecond") &&
            !harpoonText.Contains("Mathf.Min(rangeMeters, flightCableMaxLengthMeters)") &&
            !harpoonText.Contains("IsFlightCableOverextended") &&
            !harpoonText.Contains("Vector3.Distance(launchPosition, targetPoint) > CableRangeMeters") &&
            !harpoonText.Contains("targetPoint = intendedTargetTransform.position") &&
            !harpoonText.Contains("TryAttachIntendedTarget") &&
            sortieText.Contains("kind == \"harpoon\"") &&
            sortieText.Contains("ConfigureHarpoonPackage") &&
            sortieText.Contains("TryPickHarpoonTargetAtScreenPoint") &&
            sortieText.Contains("TryDesignateHarpoonTarget") &&
            sortieText.Contains("HarpoonTargetPickRadiusPixels") &&
            sortieText.Contains("BuildHarpoonTargetPickRect") &&
            sortieText.Contains("Harpoon Target Lock Ring") &&
            sortieText.Contains("HandleHudActionRightClick") &&
            sortieText.Contains("ToggleHarpoonAutoCatch") &&
            sortieText.Contains("UpdateHarpoonAutoCatch") &&
            sortieText.Contains("launcher.ClearDesignatedTarget();") &&
            sortieText.Contains("launcher.CanLaunchAtTarget") &&
            sortieText.Contains("DrawActionOrbit") &&
            sortieText.Contains("launcher.ManualRangeMeters") &&
            !sortieText.Contains("TryLaunchManualHarpoon(torpedoAimPoint)") &&
            !sortieText.Contains("|| launcher.HasDesignatedTarget") &&
            sortieText.Contains("ReleaseActiveLink(\"manual release\")") &&
            sortieText.Contains("CoreTacticalHarpoonLink.HasActiveLinkForShip") &&
            sortieText.Contains("Korshun_CombatAux_HarpoonCannon_Left") &&
            sortieText.Contains("Barbet_CombatSmall_HarpoonCannon_Left") &&
            bootstrapText.Contains("FindProjectedSide(\"Harpoon\"") &&
            korshunAuxiliaryCsvText.Contains("korshun_aux_harpoon") &&
            korshunAuxiliaryCsvText.Contains(",auxiliary,harpoon,") &&
            barbetAuxiliaryCsvText.Contains("barbet_small_harpoon") &&
            barbetAuxiliaryCsvText.Contains(",small,harpoon,");
        report.Check(structuralOk,
            structuralOk
                ? "Core Tactical harpoons are structurally wired as selectable side-slot target-designation equipment with weapon flight range separated from a strict 400m cable, 185-degree side arcs, 8m projectile catch radius, 10-second cable lifetime, 10m/s retraction to 50m, 100m/s miss return, lead aiming, nearest-target auto-catch refresh, sticky cursor target acquisition with a lock ring, right-click auto-catch, non-homing ballistic shots, hit-point anchoring, kinetic pierce, cable release, slip interference, and Korshun/Barbet placeholder visuals."
                : "Core Tactical harpoon wiring is incomplete: verify selectable target designation, separated flight range and strict 400m cable, 185-degree left/right side arcs, 8m projectile catch radius, 10-second lifetime, 10m/s retraction to 50m, 100m/s miss return, no cable wear/tension damage, lead aiming, nearest-target auto-catch refresh, sticky cursor target acquisition/ring, right-click auto-catch, non-homing projectile arc, launcher-to-hit-point anchoring, release button, slip blocking, and side-slot visuals.");
#endif

        GameObject root = null;
        GameObject ownerObject = null;
        GameObject targetObject = null;
        GameObject wreckObject = null;
        try
        {
            root = new GameObject("Big Test Harpoon Root");
            ownerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ownerObject.name = "Big Test Harpoon Owner";
            ownerObject.transform.SetParent(root.transform, false);
            ownerObject.transform.position = Vector3.zero;
            CoreTacticalShipMotor owner = ownerObject.AddComponent<CoreTacticalShipMotor>();
            owner.InitializePrototypeShip("big_test_harpoon_owner", "Big Test Harpoon Owner", new Vector3(12f, 8f, 80f), Color.cyan);
            CoreTacticalWeaponControl weaponControl = ownerObject.AddComponent<CoreTacticalWeaponControl>();
            weaponControl.SetRuntimeWeaponGroupActive(CoreTacticalWeaponGroup.Torpedoes);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Big Test Harpoon Target";
            targetObject.transform.SetParent(root.transform, false);
            targetObject.transform.position = new Vector3(-160f, 0f, 0f);
            CoreTacticalShipMotor target = targetObject.AddComponent<CoreTacticalShipMotor>();
            target.InitializePrototypeShip("big_test_harpoon_target", "Big Test Harpoon Target", new Vector3(10f, 7f, 55f), Color.magenta);
            CoreTacticalPrototypeHealth targetHealth = targetObject.AddComponent<CoreTacticalPrototypeHealth>();
            targetHealth.maxHealth = 1000f;
            targetHealth.destroyOnDeath = false;
            targetHealth.ResetHealth();

            CoreTacticalHarpoonLauncher launcher = ownerObject.AddComponent<CoreTacticalHarpoonLauncher>();
            launcher.Configure(owner, -1, "Big Test Harpoon", 1800f, 55f, 360f, 5f, 280f);
            CoreTacticalHarpoonLauncher rightLauncher = ownerObject.AddComponent<CoreTacticalHarpoonLauncher>();
            rightLauncher.Configure(owner, 1, "Big Test Harpoon Right", 1800f, 55f, 360f, 5f, 280f);
            bool sideArcOk = Approximately(launcher.ManualAimSectorDegrees, 185f, 0.01f)
                && Approximately(rightLauncher.ManualAimSectorDegrees, 185f, 0.01f)
                && launcher.IsDirectionInsideManualSector(Vector3.left)
                && !launcher.IsDirectionInsideManualSector(Vector3.right)
                && rightLauncher.IsDirectionInsideManualSector(Vector3.right)
                && !rightLauncher.IsDirectionInsideManualSector(Vector3.left);

            target.transform.position = new Vector3(160f, 0f, 0f);
            bool wrongSideDesignationRejected = !launcher.DesignateTarget(target.transform, target.Body, targetHealth, null)
                && !launcher.HasDesignatedTarget;
            launcher.ClearDesignatedTarget();
            launcher.SetReloadCooldownRemainingSecondsForTests(0f);
            target.transform.position = new Vector3(-160f, 0f, 0f);
            bool staleTargetDesignated = launcher.DesignateTarget(target.transform, target.Body, targetHealth, null);
            target.transform.position = new Vector3(-2200f, 0f, 0f);
            bool staleTargetLaunchBlocked = staleTargetDesignated
                && !launcher.TryLaunchAtDesignatedTarget()
                && !launcher.HasDesignatedTarget
                && !launcher.HasFlyingProjectile;
            launcher.ClearDesignatedTarget();

            GameObject farAutoObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            farAutoObject.name = "Big Test Far Auto Harpoon Target";
            farAutoObject.transform.SetParent(root.transform, false);
            farAutoObject.transform.position = new Vector3(-420f, 0f, 120f);
            CoreTacticalShipMotor farAutoShip = farAutoObject.AddComponent<CoreTacticalShipMotor>();
            farAutoShip.InitializePrototypeShip("big_test_far_harpoon_auto", "Far Harpoon Auto Target", new Vector3(10f, 7f, 55f), Color.red);
            GameObject nearAutoObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nearAutoObject.name = "Big Test Near Auto Harpoon Target";
            nearAutoObject.transform.SetParent(root.transform, false);
            nearAutoObject.transform.position = new Vector3(-140f, 0f, -80f);
            CoreTacticalShipMotor nearAutoShip = nearAutoObject.AddComponent<CoreTacticalShipMotor>();
            nearAutoShip.InitializePrototypeShip("big_test_near_harpoon_auto", "Near Harpoon Auto Target", new Vector3(10f, 7f, 55f), Color.red);
            bool nearestAutoTargetOk = CoreTacticalCombatSortieController.TrySelectNearestHarpoonTargetForTests(
                    owner,
                    launcher,
                    new[] { farAutoShip, nearAutoShip },
                    out Transform autoBestTarget)
                && autoBestTarget == nearAutoShip.transform;

            rightLauncher.SetReloadCooldownRemainingSecondsForTests(0f);
            Vector3 missAimPoint = rightLauncher.GetManualLaunchPosition() + Vector3.right * 240f;
            bool missLaunched = rightLauncher.TryLaunchManualHarpoon(missAimPoint);
            CoreTacticalHarpoonProjectile missProjectile = rightLauncher.ActiveProjectileForTests;
            bool missReturnOk = false;
            if (missProjectile != null)
            {
                missProjectile.transform.position = rightLauncher.GetManualLaunchPosition() + Vector3.right * 300f;
                Vector3 missReturnStart = missProjectile.transform.position;
                missProjectile.BeginReturnForTests();
                missProjectile.StepReturnForTests(1f);
                float returnStepDistance = Vector3.Distance(missReturnStart, missProjectile.transform.position);
                bool blockedDuringReturn = !rightLauncher.TryLaunchManualHarpoon(missAimPoint);
                missProjectile.StepReturnForTests(5f);
                missReturnOk = missLaunched
                    && returnStepDistance >= 99f
                    && returnStepDistance <= 101f
                    && blockedDuringReturn
                    && !rightLauncher.HasFlyingProjectile;
            }

            target.transform.position = new Vector3(-520f, 0f, 0f);
            if (target.Body != null)
            {
                target.Body.linearVelocity = new Vector3(0f, 0f, 60f);
            }

            bool farTargetDesignated = launcher.DesignateTarget(target.transform, target.Body, targetHealth, null);
            bool farTargetLaunchStarted = farTargetDesignated && launcher.TryLaunchAtDesignatedTarget();
            CoreTacticalHarpoonProjectile farTargetProjectile = launcher.ActiveProjectileForTests;
            bool farTargetLeadOk = farTargetProjectile != null
                && farTargetProjectile.TargetPointForTests.z > target.transform.position.z + 20f
                && Mathf.Abs(farTargetProjectile.TargetPointForTests.x - target.transform.position.x) <= 5f
                && Vector3.Distance(launcher.GetManualLaunchPosition(), farTargetProjectile.TargetPointForTests) <= launcher.ManualRangeMeters + 1f;
            bool farTargetLaunchOk = farTargetDesignated
                && farTargetLaunchStarted
                && launcher.HasFlyingProjectile
                && farTargetProjectile != null
                && farTargetProjectile.HasFlightCableForTests
                && launcher.ManualRangeMeters >= 1799f
                && Approximately(launcher.CableRangeMeters, 400f, 0.01f)
                && farTargetLeadOk;
            if (farTargetProjectile != null)
            {
                launcher.NotifyProjectileEnded(farTargetProjectile, false);
                DestroyBigTestObject(farTargetProjectile.gameObject);
            }

            if (target.Body != null)
            {
                target.Body.linearVelocity = Vector3.zero;
            }

            launcher.SetReloadCooldownRemainingSecondsForTests(0f);
            target.transform.position = new Vector3(-160f, 0f, 0f);
            bool targetDesignated = launcher.DesignateTarget(target.transform, target.Body, targetHealth, null);
            bool targetLaunchStarted = targetDesignated && launcher.TryLaunchAtDesignatedTarget();
            CoreTacticalHarpoonProjectile designatedProjectile = launcher.ActiveProjectileForTests;
            Vector3 lockedProjectileTarget = designatedProjectile != null ? designatedProjectile.TargetPointForTests : Vector3.zero;
            bool ballisticNoHomingOk = false;
            if (designatedProjectile != null)
            {
                target.transform.position += Vector3.forward * 120f;
                bool updateOk = TryInvokePrivateMethod(designatedProjectile, "Update", report);
                ballisticNoHomingOk = updateOk
                    && Vector3.Distance(designatedProjectile.TargetPointForTests, lockedProjectileTarget) <= 0.001f
                    && Vector3.Distance(designatedProjectile.TargetPointForTests, target.transform.position) > 10f;
                target.transform.position -= Vector3.forward * 120f;
            }

            bool targetDesignationOk = launcher.ManualRangeMeters >= 1799f
                && Approximately(launcher.CableRangeMeters, 400f, 0.01f)
                && sideArcOk
                && wrongSideDesignationRejected
                && staleTargetLaunchBlocked
                && nearestAutoTargetOk
                && missReturnOk
                && farTargetLaunchOk
                && targetDesignated
                && targetLaunchStarted
                && launcher.HasFlyingProjectile
                && designatedProjectile != null
                && designatedProjectile.HasFlightCableForTests
                && Approximately(designatedProjectile.MaxFlightCableLengthMetersForTests, 400f, 0.01f)
                && ballisticNoHomingOk
                && launcher.ReloadCooldownRemainingSeconds <= 5.05f;
            if (designatedProjectile != null)
            {
                launcher.NotifyProjectileEnded(designatedProjectile, false);
                DestroyBigTestObject(designatedProjectile.gameObject);
            }

            target.transform.position = new Vector3(-460f, 0f, 0f);
            float healthBeforeTooFar = targetHealth.currentHealth;
            bool tooFarAttached = launcher.TryAttachFromProjectile(
                target.transform,
                target.Body,
                targetHealth,
                null,
                target.transform.position,
                Vector3.right);
            bool maxCableRejectOk = !tooFarAttached
                && !launcher.HasActiveLink
                && Approximately(targetHealth.currentHealth, healthBeforeTooFar, 0.001f);

            target.transform.position = new Vector3(-160f, 0f, 0f);
            float healthBeforePierce = targetHealth.currentHealth;
            bool attached = launcher.TryAttachFromProjectile(
                target.transform,
                target.Body,
                targetHealth,
                null,
                target.transform.position,
                Vector3.right);
            float pierceDamage = healthBeforePierce - targetHealth.currentHealth;
            CoreTacticalHarpoonLink link = launcher.ActiveLink;
            bool attachAndPierceOk = attached
                && launcher.HasActiveLink
                && link != null
                && link.RestLengthMeters <= 400.01f
                && link.MaximumCableLengthMeters <= 400.01f
                && link.RestLengthMeters > 50f
                && Approximately(link.RemainingDurabilitySeconds, 10f, 0.01f)
                && targetHealth.currentHealth < healthBeforePierce
                && pierceDamage > 0f
                && pierceDamage <= 80f
                && CoreTacticalHarpoonLink.HasActiveLinkForShip(owner)
                && CoreTacticalHarpoonLink.HasActiveLinkForShip(target);

            bool hardCableMaxOk = false;
            if (link != null)
            {
                Vector3 overextendedPosition = launcher.GetCableAnchorPosition() + Vector3.left * 700f;
                target.transform.position = overextendedPosition;
                if (target.Body != null)
                {
                    target.Body.position = overextendedPosition;
                    target.Body.linearVelocity = Vector3.zero;
                }

                link.ApplyTensionForTests(0.1f, 1000f);
                Vector3 enforcedTargetPosition = target.Body != null ? target.Body.position : target.transform.position;
                hardCableMaxOk = Vector3.Distance(launcher.GetCableAnchorPosition(), enforcedTargetPosition) <= 400.5f;
                target.transform.position = new Vector3(-160f, 0f, 0f);
                if (target.Body != null)
                {
                    target.Body.position = target.transform.position;
                    target.Body.linearVelocity = Vector3.zero;
                }
            }

            float restBeforeRetraction = link != null ? link.RestLengthMeters : -1f;
            float durabilityBeforeRetraction = link != null ? link.RemainingDurabilitySeconds : -1f;
            float healthBeforeRetraction = targetHealth.currentHealth;
            if (link != null)
            {
                link.ApplyTensionForTests(1f, 1000f);
            }

            bool retractionOk = link != null
                && link.RestLengthMeters <= restBeforeRetraction - 9.9f
                && link.RestLengthMeters >= 49.99f
                && Approximately(link.RemainingDurabilitySeconds, durabilityBeforeRetraction - 1f, 0.01f)
                && Approximately(targetHealth.currentHealth, healthBeforeRetraction, 0.001f)
                && link.Stress01 <= 0.001f;
            launcher.ReleaseActiveLink("Big Test release");
            bool releaseOk = !launcher.HasActiveLink;

            float healthBeforeLifetimePierce = targetHealth.currentHealth;
            bool lifetimeAttached = launcher.TryAttachFromProjectile(
                target.transform,
                target.Body,
                targetHealth,
                null,
                target.transform.position,
                Vector3.right);
            CoreTacticalHarpoonLink lifetimeLink = launcher.ActiveLink;
            bool lifetimeStartsAtTen = lifetimeAttached
                && lifetimeLink != null
                && Approximately(lifetimeLink.RemainingDurabilitySeconds, 10f, 0.01f)
                && targetHealth.currentHealth < healthBeforeLifetimePierce;
            if (lifetimeLink != null)
            {
                lifetimeLink.ApplyTensionForTests(10.5f, 1000f);
            }

            bool lifetimeReleaseOk = lifetimeStartsAtTen && !launcher.HasActiveLink;
            launcher.SetReloadCooldownRemainingSecondsForTests(5f);
            bool cooldownHudOk = string.Equals(
                CoreTacticalCombatSortieController.GetWeaponCooldownTextForTests(owner, CoreTacticalWeaponGroup.Torpedoes),
                "5",
                StringComparison.Ordinal);

            wreckObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wreckObject.name = "Big Test Harpoon Wreck";
            wreckObject.transform.SetParent(root.transform, false);
            wreckObject.transform.position = new Vector3(-180f, 0f, 0f);
            CoreTacticalAutomatonWreck wreck = wreckObject.AddComponent<CoreTacticalAutomatonWreck>();
            wreck.Initialize(
                "Harpoon Wreck",
                1800f,
                500f,
                -50f,
                CoreTacticalAutomatonWreck.BuildDefaultManifest(20f),
                6f);
            wreck.DisableSpawnDamageGraceForTests();
            bool wreckAttached = launcher.TryAttachFromProjectile(
                wreck.transform,
                wreck.GetComponent<Rigidbody>(),
                null,
                wreck,
                wreck.transform.position,
                Vector3.right);
            float wreckStartDistance = Vector3.Distance(owner.transform.position, wreck.transform.position);
            if (launcher.ActiveLink != null)
            {
                launcher.ActiveLink.ApplyTensionForTests(1f, 1000f);
            }

            bool wreckTetherOk = wreckAttached
                && wreck.currentHealth < 500f
                && CoreTacticalHarpoonLink.HasActiveLinkForShip(owner)
                && Vector3.Distance(owner.transform.position, wreck.transform.position) <= wreckStartDistance;

            bool functionalOk = targetDesignationOk
                && maxCableRejectOk
                && attachAndPierceOk
                && hardCableMaxOk
                && retractionOk
                && releaseOk
                && lifetimeReleaseOk
                && cooldownHudOk
                && wreckTetherOk;
            report.Check(functionalOk,
                functionalOk
                    ? "Core Tactical harpoon live contract holds: left and right launchers fire only into their own 185-degree side arcs, stale/wrong-side targets are rejected, auto-catch picks the nearest valid target per launcher, missed bolts return at 100m/s before the launcher frees up, target designation auto-launches a faster flatter non-homing shot beyond 400m when inside 1800m ballistic range, leads moving targets, rejects cable attachment beyond strict 400m, kinetic pierce is modest, active cable links hold ships and wrecks from the launcher anchor to the hit point, hard-clamp active cable length to 400m, retract at 10m/s to 50m without tension damage or wear, expire after 10 seconds, manual release, HUD cooldown, slip blocker and wreck tethering all work."
                    : "Core Tactical harpoon live contract failed: targetDesignation="
                        + targetDesignationOk
                        + " (side="
                        + sideArcOk
                        + ", wrongSide="
                        + wrongSideDesignationRejected
                        + ", stale="
                        + staleTargetLaunchBlocked
                        + ", nearest="
                        + nearestAutoTargetOk
                        + ", missReturn="
                        + missReturnOk
                        + ", farLaunch="
                        + farTargetLaunchOk
                        + ", targetLaunch="
                        + targetLaunchStarted
                        + ", ballistic="
                        + ballisticNoHomingOk
                        + ")"
                        + ", maxCable="
                        + maxCableRejectOk
                        + ", attach="
                        + attachAndPierceOk
                        + ", hardCable="
                        + hardCableMaxOk
                        + ", retract="
                        + retractionOk
                        + ", release="
                        + releaseOk
                        + ", lifetime="
                        + lifetimeReleaseOk
                        + ", cooldown="
                        + cooldownHudOk
                        + ", wreck="
                        + wreckTetherOk
                        + ".");
        }
        finally
        {
            DestroyBigTestObject(wreckObject);
            DestroyBigTestObject(targetObject);
            DestroyBigTestObject(ownerObject);
            DestroyBigTestObject(root);
        }
    }

    private static float SumInventoryRowsKg(IReadOnlyList<CoreTacticalInventoryRow> rows)
    {
        if (rows == null)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            total += Mathf.Max(0f, rows[i].amountKg);
        }

        return total;
    }

    private static bool ValidateCoreTacticalCommandProjectionProbe(out string details)
    {
        GameObject root = null;
        int sampleCount = 0;
        const float screenClickToleranceMeters = 0.08f;
        const float fixedWorldRoundTripToleranceMeters = 1f;
        const float screenReprojectionTolerancePixels = 2f;
        float maxProjectionError = 0f;
        float maxMarkerError = 0f;
        float maxIssuedTargetError = 0f;
        float maxScreenReprojectionErrorPixels = 0f;
        try
        {
            int screenWidth = Mathf.Max(1, Screen.width);
            int screenHeight = Mathf.Max(1, Screen.height);
            root = new GameObject("Big Test Core Tactical Command Projection Probe");

            GameObject fleetObject = new GameObject("Big Test Core Tactical Fleet");
            fleetObject.transform.SetParent(root.transform, false);
            CoreTacticalFleetController fleet = fleetObject.AddComponent<CoreTacticalFleetController>();
            fleet.showPrototypeHud = false;
            fleet.selectAllOnStart = false;
            fleet.commandPlaneAltitudeMeters = 80f;

            GameObject shipObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shipObject.name = "Big Test Core Tactical Command Ship";
            shipObject.transform.SetParent(root.transform, false);
            shipObject.transform.position = new Vector3(0f, fleet.commandPlaneAltitudeMeters, 0f);
            shipObject.transform.rotation = Quaternion.identity;
            Rigidbody body = shipObject.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = shipObject.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            CoreTacticalShipMotor ship = shipObject.AddComponent<CoreTacticalShipMotor>();
            ship.InitializePrototypeShip(
                "big_test_command_ship",
                "Big Test Command Ship",
                new Vector3(18f, 6f, 48f),
                new Color(0.2f, 0.45f, 0.9f, 1f));
            ship.SetSelected(true);
            fleet.RegisterShip(ship);

            GameObject cameraObject = new GameObject("Big Test Core Tactical Command Camera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.aspect = screenWidth / Mathf.Max(1f, screenHeight);
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20000f;

            CoreTacticalCameraRig rig = cameraObject.AddComponent<CoreTacticalCameraRig>();
            rig.fleet = fleet;
            rig.targetCamera = camera;
            rig.yawDegrees = -138f;
            rig.elevationDegrees = 58f;
            fleet.SetInputCamera(camera);

            Rect[] cameraRects =
            {
                new Rect(0f, 0f, 1f, 1f),
                new Rect(0.13f, 0.08f, 0.74f, 0.82f)
            };
            float[] zoomDistances = { 240f, 520f, 1200f };
            Vector3 fixedWorldPoint = new Vector3(36f, fleet.commandPlaneAltitudeMeters, 44f);

            for (int rectIndex = 0; rectIndex < cameraRects.Length; rectIndex++)
            {
                camera.rect = cameraRects[rectIndex];
                Rect pixelRect = GetCameraPixelRectForBigTest(camera, screenWidth, screenHeight);
                camera.aspect = pixelRect.width / Mathf.Max(1f, pixelRect.height);
                Vector2[] screenSamples =
                {
                    pixelRect.center,
                    new Vector2(Mathf.Lerp(pixelRect.xMin, pixelRect.xMax, 0.28f), Mathf.Lerp(pixelRect.yMin, pixelRect.yMax, 0.42f)),
                    new Vector2(Mathf.Lerp(pixelRect.xMin, pixelRect.xMax, 0.72f), Mathf.Lerp(pixelRect.yMin, pixelRect.yMax, 0.64f))
                };

                for (int zoomIndex = 0; zoomIndex < zoomDistances.Length; zoomIndex++)
                {
                    rig.distanceMeters = zoomDistances[zoomIndex];
                    rig.ApplyCurrentTransformForInput();

                    for (int sampleIndex = 0; sampleIndex < screenSamples.Length; sampleIndex++)
                    {
                        if (!TryValidateCommandProjectionSample(
                            fleet,
                            ship,
                            camera,
                            screenSamples[sampleIndex],
                            out float projectionError,
                            out float markerError,
                            out float issuedTargetError,
                            out float screenReprojectionErrorPixels,
                            null,
                            screenClickToleranceMeters,
                            screenReprojectionTolerancePixels))
                        {
                            details = "screen sample failed at cameraRect="
                                + cameraRects[rectIndex].ToString("0.###")
                                + ", zoom="
                                + zoomDistances[zoomIndex].ToString("0.#")
                                + ", point="
                                + screenSamples[sampleIndex].ToString("0.#")
                                + ", projectionError="
                                + projectionError.ToString("0.###")
                                + ", markerError="
                                + markerError.ToString("0.###")
                                + ", issuedError="
                                + issuedTargetError.ToString("0.###")
                                + ", screenReprojectionError="
                                + screenReprojectionErrorPixels.ToString("0.###")
                                + " px.";
                            return false;
                        }

                        sampleCount++;
                        maxProjectionError = Mathf.Max(maxProjectionError, projectionError);
                        maxMarkerError = Mathf.Max(maxMarkerError, markerError);
                        maxIssuedTargetError = Mathf.Max(maxIssuedTargetError, issuedTargetError);
                        maxScreenReprojectionErrorPixels = Mathf.Max(maxScreenReprojectionErrorPixels, screenReprojectionErrorPixels);
                    }

                    Vector3 fixedScreen = camera.WorldToScreenPoint(fixedWorldPoint);
                    Vector2 fixedScreenPoint = new Vector2(fixedScreen.x, fixedScreen.y);
                    if (fixedScreen.z <= 0f || !IsScreenPointInsideForBigTest(fixedScreenPoint, pixelRect))
                    {
                        details = "fixed world point is not visible at cameraRect="
                            + cameraRects[rectIndex].ToString("0.###")
                            + ", zoom="
                            + zoomDistances[zoomIndex].ToString("0.#")
                            + ", screen="
                            + fixedScreen.ToString("0.###")
                            + ".";
                        return false;
                    }

                    if (!TryValidateCommandProjectionSample(
                        fleet,
                        ship,
                        camera,
                        fixedScreenPoint,
                        out float fixedProjectionError,
                        out float fixedMarkerError,
                        out float fixedIssuedTargetError,
                        out float fixedScreenReprojectionErrorPixels,
                        fixedWorldPoint,
                        fixedWorldRoundTripToleranceMeters,
                        screenReprojectionTolerancePixels))
                    {
                        details = "fixed world point failed at cameraRect="
                            + cameraRects[rectIndex].ToString("0.###")
                            + ", zoom="
                            + zoomDistances[zoomIndex].ToString("0.#")
                            + ", screen="
                            + fixedScreenPoint.ToString("0.#")
                            + ", projectionError="
                            + fixedProjectionError.ToString("0.###")
                            + ", markerError="
                            + fixedMarkerError.ToString("0.###")
                            + ", issuedError="
                            + fixedIssuedTargetError.ToString("0.###")
                            + ", screenReprojectionError="
                            + fixedScreenReprojectionErrorPixels.ToString("0.###")
                            + " px.";
                        return false;
                    }

                    sampleCount++;
                    maxProjectionError = Mathf.Max(maxProjectionError, fixedProjectionError);
                    maxMarkerError = Mathf.Max(maxMarkerError, fixedMarkerError);
                    maxIssuedTargetError = Mathf.Max(maxIssuedTargetError, fixedIssuedTargetError);
                    maxScreenReprojectionErrorPixels = Mathf.Max(maxScreenReprojectionErrorPixels, fixedScreenReprojectionErrorPixels);
                }
            }

            details = "samples="
                + sampleCount
                + ", maxProjectionError="
                + maxProjectionError.ToString("0.###")
                + " m, maxMarkerError="
                + maxMarkerError.ToString("0.###")
                + " m, maxIssuedTargetError="
                + maxIssuedTargetError.ToString("0.###")
                + " m, maxScreenReprojectionError="
                + maxScreenReprojectionErrorPixels.ToString("0.###")
                + " px, screenClickTolerance="
                + screenClickToleranceMeters.ToString("0.###")
                + " m, fixedWorldRoundTripTolerance="
                + fixedWorldRoundTripToleranceMeters.ToString("0.###")
                + " m, screenReprojectionTolerance="
                + screenReprojectionTolerancePixels.ToString("0.###")
                + " px.";
            return sampleCount > 0;
        }
        finally
        {
            DestroyBigTestObject(root);
        }
    }

    private static bool ValidateNoIdleMagnetAuxiliaryBeamEmitters(out string details)
    {
        CoreTacticalAuxiliaryBeamEmitter[] emitters =
            UnityEngine.Object.FindObjectsByType<CoreTacticalAuxiliaryBeamEmitter>(FindObjectsSortMode.None);
        int total = emitters != null ? emitters.Length : 0;
        int magnetCount = 0;
        for (int i = 0; emitters != null && i < emitters.Length; i++)
        {
            CoreTacticalAuxiliaryBeamEmitter emitter = emitters[i];
            if (emitter != null && emitter.palette == CoreTacticalUtilityBeamPalette.Magnet)
            {
                magnetCount++;
            }
        }

        details = "emitters=" + total + ", magnetEmitters=" + magnetCount + ".";
        return magnetCount == 0;
    }

    private static bool ValidateKorshunCombatMagnetLoadoutVisuals(out string details)
    {
        CoreTacticalShipMotor[] ships = UnityEngine.Object.FindObjectsByType<CoreTacticalShipMotor>(FindObjectsSortMode.None);
        CoreTacticalShipMotor playerShip = null;
        for (int i = 0; ships != null && i < ships.Length; i++)
        {
            CoreTacticalShipMotor ship = ships[i];
            if (ship == null || IsBigTestProbeShip(ship))
            {
                continue;
            }

            CoreTacticalCombatant combatant = ship.GetComponent<CoreTacticalCombatant>();
            if (ship.IsSelected && (combatant == null || combatant.team == CoreTacticalCombatTeam.Friendly))
            {
                playerShip = ship;
                break;
            }
        }

        for (int i = 0; playerShip == null && ships != null && i < ships.Length; i++)
        {
            CoreTacticalShipMotor ship = ships[i];
            if (ship == null || IsBigTestProbeShip(ship))
            {
                continue;
            }

            CoreTacticalCombatant combatant = ship.GetComponent<CoreTacticalCombatant>();
            if (combatant != null && combatant.team == CoreTacticalCombatTeam.Friendly)
            {
                playerShip = ship;
                break;
            }
        }

        if (playerShip == null)
        {
            details = "player ship is missing.";
            return false;
        }

        Transform root = playerShip.transform;
        int leftMagnets = CountActiveDescendantsByNameContains(root, "Korshun_CombatAux_Magnet_Left");
        int rightMagnets = CountActiveDescendantsByNameContains(root, "Korshun_CombatAux_Magnet_Right");
        int magnets = CountActiveDescendantsByNameContains(root, "Korshun_CombatAux_Magnet");
        int defaultTorpedoes = CountActiveDescendantsByNameContains(root, "Korshun_TorpedoLauncher_3Tube");
        Bounds leftMagnetBounds = default;
        Bounds rightMagnetBounds = default;
        bool leftMagnetPlaced = TryFindActiveDescendantByExactName(root, "Korshun_CombatAux_Magnet_Left", out Transform leftMagnet)
            && TryCalculateRendererBoundsInSpace(leftMagnet, root, out leftMagnetBounds);
        bool rightMagnetPlaced = TryFindActiveDescendantByExactName(root, "Korshun_CombatAux_Magnet_Right", out Transform rightMagnet)
            && TryCalculateRendererBoundsInSpace(rightMagnet, root, out rightMagnetBounds);
        bool shipBoundsReady = TryCalculateRendererBoundsInSpace(root, root, out Bounds shipVisualBounds);
        float sideThreshold = Mathf.Max(0.08f, shipBoundsReady ? shipVisualBounds.size.x * 0.12f : 0.25f);
        float leftX = leftMagnetPlaced ? leftMagnetBounds.center.x : 0f;
        float rightX = rightMagnetPlaced ? rightMagnetBounds.center.x : 0f;
        bool magnetsSitOnOppositeSideMounts = leftMagnetPlaced
            && rightMagnetPlaced
            && Mathf.Abs(leftX) > sideThreshold
            && Mathf.Abs(rightX) > sideThreshold
            && leftX * rightX < 0f
            && Mathf.Abs(rightX - leftX) > sideThreshold * 2f;

        details = "ship="
            + playerShip.name
            + ", magnets="
            + magnets
            + ", leftMagnets="
            + leftMagnets
            + ", rightMagnets="
            + rightMagnets
            + ", activeDefaultTorpedoes="
            + defaultTorpedoes
            + ", leftCenter="
            + (leftMagnetPlaced ? FormatVector3ForTests(leftMagnetBounds.center) : "<missing>")
            + ", rightCenter="
            + (rightMagnetPlaced ? FormatVector3ForTests(rightMagnetBounds.center) : "<missing>")
            + ", sideThreshold="
            + sideThreshold.ToString("0.###", CultureInfo.InvariantCulture)
            + ", shipVisualWidth="
            + (shipBoundsReady ? shipVisualBounds.size.x.ToString("0.###", CultureInfo.InvariantCulture) : "<missing>")
            + ".";
        return magnets >= 2
            && leftMagnets >= 1
            && rightMagnets >= 1
            && defaultTorpedoes == 0
            && magnetsSitOnOppositeSideMounts;
    }

    private static bool IsBigTestProbeShip(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return true;
        }

        for (Transform current = ship.transform; current != null; current = current.parent)
        {
            string name = current.name ?? "";
            if (name.StartsWith("Big Test ", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf(" Probe", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindActiveDescendantByExactName(Transform root, string objectName, out Transform match)
    {
        match = null;
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; descendants != null && i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];
            if (descendant == null || descendant == root || !descendant.gameObject.activeInHierarchy)
            {
                continue;
            }

            string descendantName = descendant.name ?? "";
            if (string.Equals(descendantName, objectName, StringComparison.OrdinalIgnoreCase)
                || descendantName.StartsWith(objectName + ".", StringComparison.OrdinalIgnoreCase))
            {
                match = descendant;
                return true;
            }
        }

        return false;
    }

    private static bool TryCalculateRendererBoundsInSpace(Transform root, Transform reference, out Bounds bounds)
    {
        bounds = default;
        if (root == null || reference == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        for (int i = 0; renderers != null && i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 worldCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 localCorner = reference.InverseTransformPoint(worldCorner);
                        if (!initialized)
                        {
                            bounds = new Bounds(localCorner, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        return initialized;
    }

    private static string FormatVector3ForTests(Vector3 value)
    {
        return "("
            + value.x.ToString("0.##", CultureInfo.InvariantCulture)
            + ","
            + value.y.ToString("0.##", CultureInfo.InvariantCulture)
            + ","
            + value.z.ToString("0.##", CultureInfo.InvariantCulture)
            + ")";
    }

    private static int CountActiveDescendantsByNameContains(Transform root, string token)
    {
        if (root == null || string.IsNullOrWhiteSpace(token))
        {
            return 0;
        }

        int count = 0;
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; descendants != null && i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];
            if (descendant == null || descendant == root || !descendant.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (descendant.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count++;
            }
        }

        return count;
    }

    private static bool ValidateCoreTacticalLiveCommandProjection(CoreTacticalFleetController fleet, out string details)
    {
        const float screenClickToleranceMeters = 0.08f;
        const float screenReprojectionTolerancePixels = 2f;
        details = "";
        if (fleet == null)
        {
            details = "fleet is missing.";
            return false;
        }

        Camera camera = fleet.InputCameraForTests;
        if (camera == null || !camera.enabled)
        {
            details = "active tactical camera is missing or disabled.";
            return false;
        }

        CoreTacticalShipMotor selectedShip = null;
        IReadOnlyList<CoreTacticalShipMotor> ships = fleet.Ships;
        for (int i = 0; ships != null && i < ships.Count; i++)
        {
            CoreTacticalShipMotor ship = ships[i];
            if (ship != null && ship.IsSelected)
            {
                selectedShip = ship;
                break;
            }
        }

        if (selectedShip == null && ships != null && ships.Count > 0)
        {
            selectedShip = ships[0];
            selectedShip.SetSelected(true);
        }

        if (selectedShip == null)
        {
            details = "selected ship is missing.";
            return false;
        }

        CoreTacticalCameraRig rig = camera.GetComponent<CoreTacticalCameraRig>();
        int screenWidth = Mathf.Max(1, Screen.width);
        int screenHeight = Mathf.Max(1, Screen.height);
        Rect pixelRect = GetCameraPixelRectForBigTest(camera, screenWidth, screenHeight);
        if (pixelRect.width <= 1f || pixelRect.height <= 1f)
        {
            details = "camera pixelRect is invalid: " + pixelRect.ToString("0.###") + ".";
            return false;
        }

        Vector2[] screenSamples =
        {
            pixelRect.center,
            new Vector2(Mathf.Lerp(pixelRect.xMin, pixelRect.xMax, 0.33f), Mathf.Lerp(pixelRect.yMin, pixelRect.yMax, 0.45f)),
            new Vector2(Mathf.Lerp(pixelRect.xMin, pixelRect.xMax, 0.68f), Mathf.Lerp(pixelRect.yMin, pixelRect.yMax, 0.62f))
        };
        float[] zoomDistances = rig != null
            ? new[] { 520f, Mathf.Clamp(rig.distanceMeters, rig.minDistanceMeters, rig.maxDistanceMeters), 12000f }
            : new[] { 0f };
        int sampleCount = 0;
        float maxMarkerError = 0f;
        float maxIssuedTargetError = 0f;
        float maxScreenReprojectionErrorPixels = 0f;
        for (int zoomIndex = 0; zoomIndex < zoomDistances.Length; zoomIndex++)
        {
            if (rig != null)
            {
                rig.distanceMeters = Mathf.Clamp(zoomDistances[zoomIndex], rig.minDistanceMeters, rig.maxDistanceMeters);
                rig.ApplyCurrentTransformForInput();
            }

            for (int sampleIndex = 0; sampleIndex < screenSamples.Length; sampleIndex++)
            {
                if (!TryValidateCommandProjectionSample(
                    fleet,
                    selectedShip,
                    camera,
                    screenSamples[sampleIndex],
                    out float projectionError,
                    out float markerError,
                    out float issuedTargetError,
                    out float screenReprojectionErrorPixels,
                    null,
                    screenClickToleranceMeters,
                    screenReprojectionTolerancePixels))
                {
                    Vector3 markerCenter = fleet.GetCommandPointMarkerCenterForTests();
                    Vector3 reprojected = camera.WorldToScreenPoint(markerCenter);
                    Vector2 reprojected2 = new Vector2(reprojected.x, reprojected.y);
                    Vector2 reprojectionDelta = reprojected2 - screenSamples[sampleIndex];
                    details = "sample failed at zoom="
                        + zoomDistances[zoomIndex].ToString("0.#")
                        + ", point="
                        + screenSamples[sampleIndex].ToString("0.#")
                        + ", reprojected="
                        + reprojected2.ToString("0.###")
                        + ", delta="
                        + reprojectionDelta.ToString("0.###")
                        + ", screen="
                        + screenWidth.ToString()
                        + "x"
                        + screenHeight.ToString()
                        + ", cameraPixelRect="
                        + pixelRect.ToString("0.###")
                        + ", cameraRect="
                        + camera.rect.ToString("0.###")
                        + ", cameraPixels="
                        + camera.pixelWidth.ToString()
                        + "x"
                        + camera.pixelHeight.ToString()
                        + ", aspect="
                        + camera.aspect.ToString("0.###")
                        + ", projectionError="
                        + projectionError.ToString("0.###")
                        + ", markerError="
                        + markerError.ToString("0.###")
                        + ", issuedError="
                        + issuedTargetError.ToString("0.###")
                        + ", screenReprojectionError="
                        + screenReprojectionErrorPixels.ToString("0.###")
                        + " px.";
                    return false;
                }

                sampleCount++;
                maxMarkerError = Mathf.Max(maxMarkerError, markerError);
                maxIssuedTargetError = Mathf.Max(maxIssuedTargetError, issuedTargetError);
                maxScreenReprojectionErrorPixels = Mathf.Max(maxScreenReprojectionErrorPixels, screenReprojectionErrorPixels);
            }
        }

        details = "samples="
            + sampleCount
            + ", pixelRect="
            + pixelRect.ToString("0.###")
            + ", maxMarkerError="
            + maxMarkerError.ToString("0.###")
            + " m, maxIssuedTargetError="
            + maxIssuedTargetError.ToString("0.###")
            + " m, maxScreenReprojectionError="
            + maxScreenReprojectionErrorPixels.ToString("0.###")
            + " px.";
        return sampleCount > 0;
    }

    private static bool TryValidateCommandProjectionSample(
        CoreTacticalFleetController fleet,
        CoreTacticalShipMotor ship,
        Camera camera,
        Vector2 screenPoint,
        out float projectionError,
        out float markerError,
        out float issuedTargetError,
        out float screenReprojectionErrorPixels,
        Vector3? expectedWorldPointOverride = null,
        float toleranceMeters = 0.08f,
        float screenTolerancePixels = 1.5f)
    {
        projectionError = float.PositiveInfinity;
        markerError = float.PositiveInfinity;
        issuedTargetError = float.PositiveInfinity;
        screenReprojectionErrorPixels = float.PositiveInfinity;
        if (fleet == null || ship == null || camera == null)
        {
            return false;
        }

        if (!CoreTacticalFleetController.TryBuildCameraRayFromScreenPointForTests(camera, screenPoint, out Ray _))
        {
            return false;
        }

        if (!fleet.TryProjectScreenPointToCommandPlaneForTests(screenPoint, out Vector3 projectedPoint))
        {
            return false;
        }

        Vector3 expectedWorldPoint = expectedWorldPointOverride ?? projectedPoint;
        projectionError = Vector3.Distance(projectedPoint, expectedWorldPoint);
        if (!fleet.ProcessCommandPointerForTests(
            screenPoint,
            true,
            true,
            false,
            out Vector3 commandTarget,
            out Vector3 markerCenter))
        {
            return false;
        }

        markerError = Vector3.Distance(markerCenter, expectedWorldPoint);
        float commandTargetError = Vector3.Distance(commandTarget, expectedWorldPoint);
        screenReprojectionErrorPixels = CalculateScreenReprojectionErrorPixels(camera, markerCenter, screenPoint);
        if (!fleet.ProcessCommandPointerForTests(
            screenPoint,
            false,
            true,
            false,
            out commandTarget,
            out markerCenter))
        {
            return false;
        }

        markerError = Mathf.Max(markerError, Vector3.Distance(markerCenter, expectedWorldPoint));
        commandTargetError = Mathf.Max(commandTargetError, Vector3.Distance(commandTarget, expectedWorldPoint));
        screenReprojectionErrorPixels = Mathf.Max(
            screenReprojectionErrorPixels,
            CalculateScreenReprojectionErrorPixels(camera, markerCenter, screenPoint));
        if (!fleet.ProcessCommandPointerForTests(
            screenPoint,
            false,
            false,
            true,
            out commandTarget,
            out markerCenter))
        {
            return false;
        }

        commandTargetError = Mathf.Max(commandTargetError, Vector3.Distance(commandTarget, expectedWorldPoint));
        issuedTargetError = Mathf.Max(commandTargetError, Vector3.Distance(ship.TargetPosition, expectedWorldPoint));
        screenReprojectionErrorPixels = Mathf.Max(
            screenReprojectionErrorPixels,
            CalculateScreenReprojectionErrorPixels(camera, ship.TargetPosition, screenPoint));
        float safeToleranceMeters = Mathf.Max(0.001f, toleranceMeters);
        float safeScreenTolerancePixels = Mathf.Max(0.001f, screenTolerancePixels);
        return projectionError <= safeToleranceMeters
            && markerError <= safeToleranceMeters
            && issuedTargetError <= safeToleranceMeters
            && screenReprojectionErrorPixels <= safeScreenTolerancePixels;
    }

    private static Rect GetCameraPixelRectForBigTest(Camera camera, int screenWidth, int screenHeight)
    {
        if (camera == null)
        {
            return new Rect(0f, 0f, screenWidth, screenHeight);
        }

        Rect pixelRect = camera.pixelRect;
        if (pixelRect.width > 0.01f && pixelRect.height > 0.01f)
        {
            return pixelRect;
        }

        Rect normalized = camera.rect;
        return new Rect(
            normalized.xMin * screenWidth,
            normalized.yMin * screenHeight,
            normalized.width * screenWidth,
            normalized.height * screenHeight);
    }

    private static float CalculateScreenReprojectionErrorPixels(Camera camera, Vector3 worldPoint, Vector2 expectedScreenPoint)
    {
        if (camera == null)
        {
            return float.PositiveInfinity;
        }

        Vector3 reprojected = camera.WorldToScreenPoint(worldPoint);
        if (reprojected.z <= 0f)
        {
            return float.PositiveInfinity;
        }

        return Vector2.Distance(new Vector2(reprojected.x, reprojected.y), expectedScreenPoint);
    }

    private static bool IsScreenPointInsideForBigTest(Vector2 screenPoint, Rect pixelRect)
    {
        const float tolerancePixels = 1.5f;
        return screenPoint.x >= pixelRect.xMin - tolerancePixels
            && screenPoint.x <= pixelRect.xMax + tolerancePixels
            && screenPoint.y >= pixelRect.yMin - tolerancePixels
            && screenPoint.y <= pixelRect.yMax + tolerancePixels;
    }

    private static void ValidateNoUnauthorizedEditorTools(BigTestReport report)
    {
#if UNITY_EDITOR
        string projectRoot = Directory.GetCurrentDirectory();
        string[] editorDirs =
        {
            Path.Combine(projectRoot, "Assets", "Scripts", "Editor"),
            Path.Combine(projectRoot, "Assets", "Editor")
        };
        List<string> violations = new List<string>();

        string forbiddenReimporter = Path.Combine(projectRoot, "Assets", "Scripts", "Editor", "StarterHullBlenderReimporter.cs");
        if (File.Exists(forbiddenReimporter))
        {
            violations.Add("StarterHullBlenderReimporter.cs");
        }

        for (int editorDirIndex = 0; editorDirIndex < editorDirs.Length; editorDirIndex++)
        {
            string editorDir = editorDirs[editorDirIndex];
            if (!Directory.Exists(editorDir))
            {
                continue;
            }

            string[] files = Directory.GetFiles(editorDir, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string relativePath = ToProjectRelativePath(projectRoot, path);
                bool allowedEditorToolFile =
                    string.Equals(relativePath, "Assets/Scripts/Editor/WildWindBigTestMenu.cs", StringComparison.Ordinal);
                if (!allowedEditorToolFile)
                {
                    violations.Add(relativePath);
                }

                string text = File.ReadAllText(path);
                string[] lines = text.Split('\n');
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string trimmed = lines[lineIndex].Trim();
                    if (!trimmed.StartsWith("[MenuItem(", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool allowedBigTestMenuItem =
                        allowedEditorToolFile &&
                        (string.Equals(trimmed, "[MenuItem(\"Wild Wind/Провести большой тест\")]", StringComparison.Ordinal)
                            || string.Equals(trimmed, "[MenuItem(\"Wild Wind/Провести большой тест\", true)]", StringComparison.Ordinal));
                    if (!allowedBigTestMenuItem)
                    {
                        violations.Add(relativePath + ":" + (lineIndex + 1).ToString());
                    }
                }
            }
        }

        report.Check(violations.Count == 0,
            violations.Count == 0
                ? "PROJECT RULE: no unauthorized Wild Wind editor tools are present; Big Test is the only allowed top-level Unity tool."
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
            "Assets/Scripts/Systems/MeshArmorBody.cs",
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

    private string GetReportFolderPath()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, string.IsNullOrWhiteSpace(reportFolder) ? "TestReports" : reportFolder);
    }

    private string GetTextReportPath()
    {
        return Path.Combine(GetReportFolderPath(), "WildWindBigTestReport.txt");
    }

    private string GetJsonReportPath()
    {
        return Path.Combine(GetReportFolderPath(), "WildWindBigTestReport.json");
    }

    private void TryWriteReport(string text, WildWindBigTestResult result, BigTestReport report)
    {
        try
        {
            string folder = GetReportFolderPath();
            Directory.CreateDirectory(folder);
            string path = GetTextReportPath();
            File.WriteAllText(path, text, Encoding.UTF8);
            Debug.Log(LogPrefix + "РўРµРєСЃС‚РѕРІС‹Р№ РїСЂРѕС‚РѕРєРѕР» СЃРѕС…СЂР°РЅС‘РЅ: " + path, this);

            if (result != null)
            {
                string jsonPath = GetJsonReportPath();
                string json = JsonUtility.ToJson(BigTestJsonSummary.FromResult(result, path), true);
                File.WriteAllText(jsonPath, json, Encoding.UTF8);
                Debug.Log(LogPrefix + "JSON summary СЃРѕС…СЂР°РЅС‘РЅ: " + jsonPath, this);
            }
        }
        catch (Exception exception)
        {
            report.Warn("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ РїСЂРѕС‚РѕРєРѕР»С‹ Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р°: " + exception.Message);
        }
    }

    private void TryWriteRunStatus(string state, WildWindBigTestResult result, BigTestReport report)
    {
        try
        {
            string folder = GetReportFolderPath();
            Directory.CreateDirectory(folder);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("state: " + (string.IsNullOrWhiteSpace(state) ? "unknown" : state));
            builder.AppendLine("generatedAtUtc: " + DateTime.UtcNow.ToString("O"));
            builder.AppendLine("scene: " + SceneManager.GetActiveScene().name);
            builder.AppendLine("reportTxt: " + GetTextReportPath());
            builder.AppendLine("reportJson: " + GetJsonReportPath());
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
            report?.Warn("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ СЃС‚Р°С‚СѓСЃ Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р°: " + exception.Message);
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

        report.Check(notEmpty && unique, "ID " + label + " Р·Р°РїРѕР»РЅРµРЅС‹ Рё СѓРЅРёРєР°Р»СЊРЅС‹.");
    }

    private static bool ComponentPackageTextIsEncodingClean(SessionConfigDatabase config)
    {
        if (config == null)
        {
            return false;
        }

        return PackageTextIsEncodingClean(config.korshunHullPackages, package => package.localNameRu, package => package.roleRu, package => package.notesRu)
            && PackageTextIsEncodingClean(config.korshunPowerPlants, powerPlant => powerPlant.localNameRu, powerPlant => powerPlant.notesRu)
            && PackageTextIsEncodingClean(config.korshunWeaponPackages, weapon => weapon.localNameRu, weapon => weapon.notesRu)
            && PackageTextIsEncodingClean(config.korshunAuxiliaryPackages, auxiliary => auxiliary.localNameRu, auxiliary => auxiliary.notesRu)
            && PackageTextIsEncodingClean(config.shipCitadelPackages, citadel => citadel.localNameRu, citadel => citadel.notesRu);
    }

    private static bool PackageTextIsEncodingClean<T>(IReadOnlyList<T> records, params Func<T, string>[] textSelectors)
    {
        if (records == null)
        {
            return true;
        }

        for (int i = 0; i < records.Count; i++)
        {
            T record = records[i];
            if (record == null)
            {
                continue;
            }

            for (int selectorIndex = 0; selectorIndex < textSelectors.Length; selectorIndex++)
            {
                Func<T, string> selector = textSelectors[selectorIndex];
                if (selector != null && ContainsMojibakeMarker(selector(record)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ContainsMojibakeMarker(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            if (current == '\u00D0' || current == '\u00D1')
            {
                return true;
            }

            if (i + 1 >= text.Length || (current != '\u0420' && current != '\u0421'))
            {
                continue;
            }

            if (IsWindows1251MojibakeTail(text[i + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWindows1251MojibakeTail(char value)
    {
        return (value >= '\u0400' && value <= '\u040F')
            || (value >= '\u0450' && value <= '\u045F')
            || (value >= '\u00A0' && value <= '\u00BF')
            || value == '\u0490'
            || value == '\u0491'
            || value == '\u2013'
            || value == '\u2014'
            || value == '\u2018'
            || value == '\u2019'
            || value == '\u201A'
            || value == '\u201C'
            || value == '\u201D'
            || value == '\u201E'
            || value == '\u2020'
            || value == '\u2021'
            || value == '\u2022'
            || value == '\u2026'
            || value == '\u2030'
            || value == '\u2039'
            || value == '\u203A'
            || value == '\u20AC'
            || value == '\u2116'
            || value == '\u2122';
    }

    private static bool ItemAmountsReferenceExistingItems<T>(IReadOnlyList<T> records, Func<T, string> itemSelector, Func<T, float> amountSelector, SessionConfigDatabase config)
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

    private static int CountStorageResourceTotal(PortStorageState storage)
    {
        if (storage == null || storage.storage == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < storage.storage.Count; i++)
        {
            ResourceStack stack = storage.storage[i];
            if (stack == null || stack.amount <= 0)
            {
                continue;
            }

            total += stack.amount;
        }

        return total;
    }

    private static List<string> GetOreItemIdsForBigTest(SessionConfigDatabase config)
    {
        List<string> itemIds = new List<string>();
        if (config == null || config.oreTypes == null)
        {
            return itemIds;
        }

        for (int i = 0; i < config.oreTypes.Count; i++)
        {
            OreTypeConfig oreType = config.oreTypes[i];
            if (oreType == null || string.IsNullOrWhiteSpace(oreType.oreItemId))
            {
                continue;
            }

            if (!itemIds.Contains(oreType.oreItemId))
            {
                itemIds.Add(oreType.oreItemId);
            }
        }

        return itemIds;
    }

    private static int CountStorageItems(PortStorageState storage, IReadOnlyList<string> itemIds)
    {
        if (storage == null || itemIds == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < itemIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(itemIds[i]))
            {
                continue;
            }

            total += storage.GetResourceAmount(itemIds[i]);
        }

        return total;
    }

    private static bool ItemIdsExist(SessionConfigDatabase config, IReadOnlyList<string> itemIds)
    {
        if (config == null || itemIds == null || itemIds.Count == 0) return false;
        for (int i = 0; i < itemIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(itemIds[i]) || config.GetItem(itemIds[i]) == null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ItemHasRuName(SessionConfigDatabase config, string itemId, string expectedRuName)
    {
        ItemConfig item = config != null ? config.GetItem(itemId) : null;
        return item != null && item.localNameRu == expectedRuName;
    }

    private static bool FilesHaveUtf8Bom(IReadOnlyList<string> assetPaths)
    {
        if (assetPaths == null || assetPaths.Count == 0) return false;
        for (int i = 0; i < assetPaths.Count; i++)
        {
            if (!FileHasUtf8Bom(assetPaths[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FileHasUtf8Bom(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return false;
        string fullPath = ProjectPath(assetPath);
        if (!File.Exists(fullPath)) return false;

        byte[] bytes = File.ReadAllBytes(fullPath);
        return bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;
    }

    private static bool ItemsExistWithMass(SessionConfigDatabase config, IReadOnlyList<string> itemIds, float expectedMassKgPerUnit)
    {
        if (config == null || itemIds == null || itemIds.Count == 0) return false;
        for (int i = 0; i < itemIds.Count; i++)
        {
            string itemId = itemIds[i];
            ItemConfig item = string.IsNullOrWhiteSpace(itemId) ? null : config.GetItem(itemId);
            if (item == null || Mathf.Abs(item.massKgPerUnit - expectedMassKgPerUnit) > 0.0001f)
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

#if UNITY_EDITOR
    private static CoreTacticalWeaponVisualAuditResult AuditCoreTacticalWeaponVisualMaterials(BigTestReport report)
    {
        CoreTacticalWeaponVisualAuditResult result = new CoreTacticalWeaponVisualAuditResult
        {
            AllClear = true,
            ImagePath = "TestReports/CoreTacticalWeaponVisualAudit.png"
        };

        GameObject auditRoot = null;
        List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();
        try
        {
            auditRoot = new GameObject("Big Test Core Tactical Weapon Visual Audit");

            Material shellMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(1f, 0.76f, 0.22f, 1f), 2.2f));
            Material heavyShellMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(1f, 0.94f, 0.48f, 1f), 2.4f));
            Material rocketMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(1f, 0.46f, 0.12f, 1f), 2.2f));
            Material rocketTrailMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateTransparentMaterial(new Color(1f, 0.62f, 0.20f, 0.92f), 1.35f));
            Material torpedoMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(0.70f, 0.94f, 1f, 1f), 2.1f));
            Material torpedoTrailMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateTransparentMaterial(new Color(0.32f, 0.74f, 1f, 0.86f), 1.2f));
            Material tracerVertexMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(new Color(0.72f, 0.48f, 0.24f, 0.78f)));
            Material burstMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateTransparentMaterial(new Color(1f, 0.78f, 0.18f, 0.75f), 1.6f));
            Material utilityBeamMaterial = TrackBigTestObject(ownedObjects, CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(new Color(0.22f, 0.92f, 1f, 0.92f)));

            AddWeaponVisualAuditPrimitive(auditRoot.transform, "30 mm shell", PrimitiveType.Sphere, new Vector3(-8f, 1.25f, 0f), new Vector3(2.4f, 0.45f, 0.45f), shellMaterial);
            AddWeaponVisualAuditLabel(auditRoot.transform, "30 mm shell", new Vector3(-9.2f, 2.15f, -0.4f), true);
            AddWeaponVisualAuditPrimitive(auditRoot.transform, "PMK shell", PrimitiveType.Sphere, new Vector3(-4.6f, 1.25f, 0f), new Vector3(2.7f, 0.55f, 0.55f), heavyShellMaterial);
            AddWeaponVisualAuditLabel(auditRoot.transform, "PMK shell", new Vector3(-5.8f, 2.15f, -0.4f), true);

            AddWeaponVisualAuditLine(auditRoot.transform, "MG tracer line", new Vector3(-2.6f, 1.15f, 0f), new Vector3(0.4f, 1.65f, 0f), tracerVertexMaterial, new Color(1f, 0.72f, 0.22f, 0.86f), new Color(1f, 0.30f, 0.10f, 0.14f), 0.26f, 0.08f);
            AddWeaponVisualAuditTracerQuad(auditRoot.transform, "MG tracer mesh", new Vector3(-2.5f, 0.72f, 0.02f), new Vector3(0.35f, 0.95f, 0.02f), 0.22f, new Color(1f, 0.62f, 0.16f, 0.78f), tracerVertexMaterial, ownedObjects);
            AddWeaponVisualAuditLabel(auditRoot.transform, "MG tracer", new Vector3(-2.75f, 2.15f, -0.4f), true);

            AddWeaponVisualAuditLine(auditRoot.transform, "Rocket trail", new Vector3(1.0f, 1.25f, 0f), new Vector3(3.9f, 1.25f, 0f), rocketTrailMaterial, new Color(1f, 0.62f, 0.20f, 0.92f), new Color(1f, 0.26f, 0.08f, 0.12f), 0.44f, 0.08f);
            AddWeaponVisualAuditPrimitive(auditRoot.transform, "Rocket body", PrimitiveType.Capsule, new Vector3(4.3f, 1.25f, 0f), new Vector3(0.52f, 1.55f, 0.52f), rocketMaterial, Quaternion.Euler(0f, 0f, 90f));
            AddWeaponVisualAuditLabel(auditRoot.transform, "rocket + trail", new Vector3(2.0f, 2.15f, -0.4f), true);

            AddWeaponVisualAuditLine(auditRoot.transform, "Torpedo trail", new Vector3(5.6f, 1.25f, 0f), new Vector3(8.5f, 1.25f, 0f), torpedoTrailMaterial, new Color(0.32f, 0.74f, 1f, 0.86f), new Color(0.12f, 0.32f, 1f, 0.12f), 0.42f, 0.08f);
            AddWeaponVisualAuditPrimitive(auditRoot.transform, "Torpedo body", PrimitiveType.Capsule, new Vector3(8.9f, 1.25f, 0f), new Vector3(0.55f, 1.75f, 0.55f), torpedoMaterial, Quaternion.Euler(0f, 0f, 90f));
            AddWeaponVisualAuditLabel(auditRoot.transform, "torpedo + trail", new Vector3(6.45f, 2.15f, -0.4f), true);

            AddWeaponVisualAuditDisk(auditRoot.transform, "HE burst", new Vector3(11.0f, 1.28f, 0f), 1.25f, burstMaterial, ownedObjects);
            AddWeaponVisualAuditLabel(auditRoot.transform, "HE burst", new Vector3(10.15f, 2.75f, -0.4f), true);

            AddUtilityBeamVisualAuditLine(auditRoot.transform, "Repair beam", CoreTacticalUtilityBeamPalette.Repair, utilityBeamMaterial, new Vector3(-8.6f, -1.15f, 0f), new Vector3(-5.7f, -0.85f, 0f), 0.34f);
            AddWeaponVisualAuditLabel(auditRoot.transform, "repair beam", new Vector3(-7.35f, -0.22f, -0.4f), true);
            AddUtilityBeamVisualAuditLine(auditRoot.transform, "Magnet beam", CoreTacticalUtilityBeamPalette.Magnet, utilityBeamMaterial, new Vector3(-4.5f, -1.15f, 0f), new Vector3(-1.6f, -0.85f, 0f), 0.34f);
            AddWeaponVisualAuditLabel(auditRoot.transform, "magnet beam", new Vector3(-3.2f, -0.22f, -0.4f), true);
            AddUtilityBeamVisualAuditLine(auditRoot.transform, "Salvage beam", CoreTacticalUtilityBeamPalette.Salvage, utilityBeamMaterial, new Vector3(-4.5f, -2.35f, 0f), new Vector3(-1.6f, -2.05f, 0f), 0.30f);
            AddWeaponVisualAuditLabel(auditRoot.transform, "salvage beam", new Vector3(-3.2f, -1.42f, -0.4f), true);
            AddUtilityBeamVisualAuditLine(auditRoot.transform, "Drill beam", CoreTacticalUtilityBeamPalette.Drill, utilityBeamMaterial, new Vector3(-0.4f, -1.15f, 0f), new Vector3(2.5f, -0.85f, 0f), 0.32f);
            AddWeaponVisualAuditLabel(auditRoot.transform, "drill beam", new Vector3(0.85f, -0.22f, -0.4f), true);
            AddUtilityBeamVisualAuditLine(auditRoot.transform, "Scanner beam", CoreTacticalUtilityBeamPalette.Scanner, utilityBeamMaterial, new Vector3(3.7f, -1.15f, 0f), new Vector3(6.6f, -0.85f, 0f), 0.34f);
            AddWeaponVisualAuditLabel(auditRoot.transform, "scanner beam", new Vector3(5.0f, -0.22f, -0.4f), true);

            if (TryCaptureCoreTacticalWeaponVisualAuditImage(auditRoot.transform, result.ImagePath, out string imageError, out string imageSummary, out bool pixelAuditOk))
            {
                result.AllClear = pixelAuditOk;
                result.Summary = imageSummary;
                report.Info("Core Tactical weapon visual audit image saved: " + result.ImagePath + ". " + imageSummary);
            }
            else
            {
                result.AllClear = false;
                result.Summary = "Image capture failed: " + imageError;
            }
        }
        catch (Exception exception)
        {
            result.AllClear = false;
            result.Summary = "Weapon visual audit exception: " + exception.Message;
        }
        finally
        {
            DestroyBigTestObject(auditRoot);
            for (int i = 0; i < ownedObjects.Count; i++)
            {
                DestroyBigTestUnityObject(ownedObjects[i]);
            }
        }

        return result;
    }

    private static T TrackBigTestObject<T>(List<UnityEngine.Object> ownedObjects, T unityObject)
        where T : UnityEngine.Object
    {
        if (unityObject != null)
        {
            ownedObjects.Add(unityObject);
        }

        return unityObject;
    }

    private static void AddWeaponVisualAuditPrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = "Weapon Visual Audit " + name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localRotation = rotation ?? Quaternion.identity;
        primitive.transform.localScale = scale;
        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        DisableBigTestColliders(primitive.transform);
    }

    private static void AddWeaponVisualAuditLine(Transform parent, string name, Vector3 start, Vector3 end, Material material, Color startColor, Color endColor, float startWidth, float endWidth)
    {
        GameObject lineObject = new GameObject("Weapon Visual Audit " + name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startColor = startColor;
        line.endColor = endColor;
        line.startWidth = startWidth;
        line.endWidth = endWidth;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private static void AddUtilityBeamVisualAuditLine(
        Transform parent,
        string name,
        CoreTacticalUtilityBeamPalette palette,
        Material material,
        Vector3 start,
        Vector3 end,
        float width)
    {
        CoreTacticalUtilityBeamVisual.ResolvePalette(palette, out Color startColor, out Color middleColor, out Color endColor);
        GameObject lineObject = new GameObject("Weapon Visual Audit " + name);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.positionCount = 4;
        Vector3 bend = Vector3.up * width * 1.4f;
        line.SetPosition(0, start);
        line.SetPosition(1, Vector3.Lerp(start, end, 0.33f) + bend);
        line.SetPosition(2, Vector3.Lerp(start, end, 0.68f) - bend * 0.55f);
        line.SetPosition(3, end);
        line.startWidth = width;
        line.endWidth = width * 0.30f;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.colorGradient = BuildUtilityBeamVisualAuditGradient(startColor, middleColor, endColor);
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private static Gradient BuildUtilityBeamVisualAuditGradient(Color startColor, Color middleColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(middleColor, 0.52f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(startColor.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(middleColor.a), 0.52f),
                new GradientAlphaKey(Mathf.Clamp01(endColor.a), 1f)
            });
        return gradient;
    }

    private static void AddWeaponVisualAuditTracerQuad(Transform parent, string name, Vector3 tail, Vector3 head, float width, Color color, Material material, List<UnityEngine.Object> ownedObjects)
    {
        GameObject quadObject = new GameObject("Weapon Visual Audit " + name);
        quadObject.transform.SetParent(parent, false);
        MeshFilter meshFilter = quadObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = quadObject.AddComponent<MeshRenderer>();
        Mesh mesh = TrackBigTestObject(ownedObjects, new Mesh { name = name + " Mesh" });
        Vector3 up = Vector3.up * (width * 0.5f);
        Color headColor = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.12f));
        mesh.vertices = new[] { tail - up, tail + up, head + up, head - up };
        mesh.colors = new[] { color, color, headColor, headColor };
        mesh.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0, true);
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private static void AddWeaponVisualAuditDisk(Transform parent, string name, Vector3 position, float radius, Material material, List<UnityEngine.Object> ownedObjects)
    {
        const int SegmentCount = 32;
        GameObject diskObject = new GameObject("Weapon Visual Audit " + name);
        diskObject.transform.SetParent(parent, false);
        diskObject.transform.localPosition = position;
        MeshFilter meshFilter = diskObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = diskObject.AddComponent<MeshRenderer>();
        Mesh mesh = TrackBigTestObject(ownedObjects, new Mesh { name = name + " Mesh" });
        Vector3[] vertices = new Vector3[SegmentCount + 1];
        int[] indices = new int[SegmentCount * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = i / (float)SegmentCount * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            int index = i * 3;
            indices[index] = 0;
            indices[index + 1] = i + 1;
            indices[index + 2] = i == SegmentCount - 1 ? 1 : i + 2;
        }

        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private static void AddWeaponVisualAuditLabel(Transform parent, string text, Vector3 position, bool ok)
    {
        GameObject label = new GameObject("Weapon Visual Audit Label " + text);
        label.transform.SetParent(parent, false);
        label.transform.localPosition = position;
        label.transform.localRotation = Quaternion.identity;
        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.color = ok ? new Color(0.78f, 1f, 0.78f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.18f;
        textMesh.fontSize = 24;
    }

    private static bool TryCaptureCoreTacticalWeaponVisualAuditImage(Transform auditRoot, string relativePath, out string error, out string pixelSummary, out bool pixelAuditOk)
    {
        error = "";
        pixelSummary = "";
        pixelAuditOk = false;
        if (auditRoot == null)
        {
            error = "audit root missing";
            return false;
        }

        if (!TryCalculateWorldRendererBounds(auditRoot, out Bounds sceneBounds))
        {
            error = "audit bounds missing";
            return false;
        }

        GameObject lightObject = new GameObject("Big Test Core Tactical Weapon Visual Audit Light");
        GameObject cameraObject = new GameObject("Big Test Core Tactical Weapon Visual Audit Camera");
        RenderTexture renderTexture = null;
        Texture2D capture = null;
        try
        {
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.85f;
            lightObject.transform.rotation = Quaternion.Euler(40f, -25f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.014f, 0.020f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(4.2f, sceneBounds.size.x * 0.25f);
            camera.transform.position = sceneBounds.center + new Vector3(0f, 0f, -18f);
            camera.transform.LookAt(sceneBounds.center);

            renderTexture = new RenderTexture(1500, 820, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            capture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            capture.Apply();
            RenderTexture.active = previous;

            pixelAuditOk = AnalyzeCoreTacticalWeaponVisualAuditPixels(capture, out pixelSummary);

            string fullPath = ProjectPath(relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, capture.EncodeToPNG());
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyBigTestUnityObject(renderTexture);
            }

            if (capture != null)
            {
                DestroyBigTestUnityObject(capture);
            }

            DestroyBigTestObject(cameraObject);
            DestroyBigTestObject(lightObject);
        }
    }

    private static bool AnalyzeCoreTacticalWeaponVisualAuditPixels(Texture2D capture, out string summary)
    {
        summary = "no capture";
        if (capture == null)
        {
            return false;
        }

        Color32[] pixels = capture.GetPixels32();
        int coloredPixels = 0;
        int orangePixels = 0;
        int cyanPixels = 0;
        int greenPixels = 0;
        int purplePixels = 0;
        int brightPixels = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];
            float r = pixel.r / 255f;
            float g = pixel.g / 255f;
            float b = pixel.b / 255f;
            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            float chroma = max - min;
            float luminance = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            if (luminance > 0.12f && chroma > 0.08f)
            {
                coloredPixels++;
            }

            if (r > 0.58f && g > 0.20f && g < 0.88f && b < 0.38f)
            {
                orangePixels++;
            }

            if (b > 0.50f && g > 0.38f && r < 0.78f)
            {
                cyanPixels++;
            }

            if (g > 0.52f && r < 0.62f && b < 0.78f)
            {
                greenPixels++;
            }

            if (b > 0.52f && r > 0.30f && g < 0.70f)
            {
                purplePixels++;
            }

            if (luminance > 0.48f)
            {
                brightPixels++;
            }
        }

        bool ok = coloredPixels > 1600
            && orangePixels > 420
            && cyanPixels > 260
            && greenPixels > 260
            && purplePixels > 140
            && brightPixels > 500;
        summary = "pixels colored=" + coloredPixels
            + ", orange=" + orangePixels
            + ", cyan=" + cyanPixels
            + ", green=" + greenPixels
            + ", purple=" + purplePixels
            + ", bright=" + brightPixels
            + ".";
        return ok;
    }

    private static PortDockTurretImportAuditResult AuditPortDockTurretImports(BigTestReport report)
    {
        PortDockTurretImportAuditResult result = new PortDockTurretImportAuditResult
        {
            AllClear = true,
            ImagePath = "TestReports/PortDockTurretImportAudit.png"
        };

        PortDockTurretImportSpec[] specs =
        {
            new PortDockTurretImportSpec("WW_Turret_30mm_Single", 1.7f, 0.65f),
            new PortDockTurretImportSpec("WW_Turret_76mm_Twin", 2.1f, 0.55f),
            new PortDockTurretImportSpec("WW_Turret_100mm_Single", 3.2f, 0.45f),
            new PortDockTurretImportSpec("WW_Turret_100mm_Twin_PMK", 4.3f, 0.65f),
            new PortDockTurretImportSpec("WW_Turret_200mm_Mortar", 3.0f, 0.75f),
            new PortDockTurretImportSpec("WW_Turret_TorpedoLauncher_3Tube", 2.2f, 0.55f),
            new PortDockTurretImportSpec("WW_Turret_RocketLauncher_Pod", 2.2f, 0.80f),
            new PortDockTurretImportSpec("WW_Turret_Magnet_Single", 2.7f, 0.70f),
            new PortDockTurretImportSpec("WW_Turret_GasSiphon_Single", 2.8f, 0.75f),
            new PortDockTurretImportSpec("WW_Turret_RepairBeam_Single", 2.1f, 0.45f),
            new PortDockTurretImportSpec("WW_Turret_HackingDish_Single", 5.2f, 0.85f)
        };

        GameObject auditRoot = null;
        StringBuilder summary = new StringBuilder();
        try
        {
            const string baseAssetPath = "Assets/ShipImports/Models/Turrets/WW_Base_Small_W_D7m.fbx";
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(baseAssetPath);
            if (basePrefab == null)
            {
                result.AllClear = false;
                result.Summary = "Port turret import audit could not load WW_Base_Small_W_D7m.fbx.";
                return result;
            }

            auditRoot = new GameObject("Big Test Port Dock Turret Import Audit");
            int columns = 3;
            float cellX = 15f;
            float cellZ = 15f;

            for (int i = 0; i < specs.Length; i++)
            {
                PortDockTurretImportSpec spec = specs[i];
                int column = i % columns;
                int row = i / columns;
                Vector3 cellCenter = new Vector3((column - 1) * cellX, 0f, row * cellZ);

                GameObject stand = new GameObject(spec.ModelId + "_AuditStand");
                stand.transform.SetParent(auditRoot.transform, false);

                GameObject baseInstance = Instantiate(basePrefab, stand.transform);
                baseInstance.name = spec.ModelId + "_AuditBase";
                baseInstance.transform.localPosition = Vector3.zero;
                baseInstance.transform.localRotation = Quaternion.identity;
                baseInstance.transform.localScale = Vector3.one;
                DisableBigTestColliders(baseInstance.transform);

                if (!TryCalculateWorldRendererBounds(baseInstance.transform, out Bounds baseBounds))
                {
                    result.AllClear = false;
                    summary.Append(spec.ModelId).Append(": base bounds missing; ");
                    continue;
                }

                baseInstance.transform.position += new Vector3(
                    cellCenter.x - baseBounds.center.x,
                    -baseBounds.min.y,
                    cellCenter.z - baseBounds.center.z);
                TryCalculateWorldRendererBounds(baseInstance.transform, out baseBounds);

                string assetPath = "Assets/ShipImports/Models/Turrets/" + spec.ModelId + ".fbx";
                GameObject modulePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (modulePrefab == null)
                {
                    result.AllClear = false;
                    summary.Append(spec.ModelId).Append(": missing; ");
                    continue;
                }

                GameObject moduleInstance = Instantiate(modulePrefab, stand.transform);
                moduleInstance.name = spec.ModelId + "_AuditModule";
                moduleInstance.transform.localPosition = Vector3.zero;
                moduleInstance.transform.localRotation = Quaternion.identity;
                moduleInstance.transform.localScale = Vector3.one;
                DisableBigTestColliders(moduleInstance.transform);

                if (!TryCalculateWorldRendererBounds(moduleInstance.transform, out Bounds moduleBounds))
                {
                    result.AllClear = false;
                    summary.Append(spec.ModelId).Append(": module bounds missing; ");
                    continue;
                }

                moduleInstance.transform.position += new Vector3(
                    baseBounds.center.x - moduleBounds.center.x,
                    baseBounds.max.y - moduleBounds.min.y,
                    baseBounds.center.z - moduleBounds.center.z);
                TryCalculateWorldRendererBounds(moduleInstance.transform, out moduleBounds);

                float horizontalSpan = Mathf.Max(moduleBounds.size.x, moduleBounds.size.z);
                bool heightOk = moduleBounds.size.y <= spec.MaxUnityHeightMeters + 0.05f;
                bool majorAxisNotVertical = horizontalSpan > 0.001f
                    && moduleBounds.size.y <= horizontalSpan * spec.MaxHeightToHorizontalRatio + 0.05f;
                bool ok = heightOk && majorAxisNotVertical;
                result.AllClear &= ok;

                summary.Append(spec.ModelId)
                    .Append(": xyz=")
                    .Append(FormatVector3(moduleBounds.size))
                    .Append(ok ? " OK; " : " FAIL; ");

                AddBigTestAuditLabel(stand.transform, spec.ModelId, cellCenter + new Vector3(-6f, baseBounds.max.y + 0.15f, -5.2f), ok);
            }

            result.Summary = summary.ToString();
            if (TryCapturePortDockTurretAuditImage(auditRoot.transform, result.ImagePath, out string imageError))
            {
                report.Info("Port turret import audit image saved: " + result.ImagePath);
            }
            else
            {
                result.AllClear = false;
                result.Summary += " Image capture failed: " + imageError;
            }
        }
        catch (Exception exception)
        {
            result.AllClear = false;
            result.Summary = "Port turret import audit exception: " + exception.Message;
        }
        finally
        {
            DestroyBigTestObject(auditRoot);
        }

        return result;
    }

    private static bool TryCapturePortDockTurretAuditImage(Transform auditRoot, string relativePath, out string error)
    {
        error = "";
        if (auditRoot == null)
        {
            error = "audit root missing";
            return false;
        }

        if (!TryCalculateWorldRendererBounds(auditRoot, out Bounds sceneBounds))
        {
            error = "audit bounds missing";
            return false;
        }

        GameObject lightObject = new GameObject("Big Test Port Dock Turret Audit Light");
        GameObject cameraObject = new GameObject("Big Test Port Dock Turret Audit Camera");
        RenderTexture renderTexture = null;
        Texture2D capture = null;
        try
        {
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.045f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(8f, Mathf.Max(sceneBounds.size.x * 0.45f, sceneBounds.size.z * 0.45f));
            camera.transform.position = sceneBounds.center + new Vector3(22f, 20f, -30f);
            camera.transform.LookAt(sceneBounds.center + Vector3.up * 1.25f);

            renderTexture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            capture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            capture.Apply();
            RenderTexture.active = previous;

            string fullPath = ProjectPath(relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(fullPath, capture.EncodeToPNG());
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                DestroyBigTestUnityObject(renderTexture);
            }

            if (capture != null)
            {
                DestroyBigTestUnityObject(capture);
            }

            DestroyBigTestObject(cameraObject);
            DestroyBigTestObject(lightObject);
        }
    }

    private static bool TryCalculateWorldRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    private static void DisableBigTestColliders(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private static void AddBigTestAuditLabel(Transform parent, string text, Vector3 position, bool ok)
    {
        GameObject label = new GameObject("Label_" + text);
        label.transform.SetParent(parent, true);
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text.Replace("WW_Turret_", "").Replace("_", " ");
        textMesh.color = ok ? new Color(0.75f, 1f, 0.75f, 1f) : new Color(1f, 0.35f, 0.35f, 1f);
        textMesh.anchor = TextAnchor.UpperLeft;
        textMesh.characterSize = 0.55f;
        textMesh.fontSize = 34;
    }

    private static string FormatVector3(Vector3 value)
    {
        return value.x.ToString("0.###", CultureInfo.InvariantCulture)
            + "/"
            + value.y.ToString("0.###", CultureInfo.InvariantCulture)
            + "/"
            + value.z.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class PortDockTurretImportSpec
    {
        public readonly string ModelId;
        public readonly float MaxUnityHeightMeters;
        public readonly float MaxHeightToHorizontalRatio;

        public PortDockTurretImportSpec(string modelId, float maxUnityHeightMeters, float maxHeightToHorizontalRatio)
        {
            ModelId = modelId;
            MaxUnityHeightMeters = maxUnityHeightMeters;
            MaxHeightToHorizontalRatio = maxHeightToHorizontalRatio;
        }
    }

    private sealed class PortDockTurretImportAuditResult
    {
        public bool AllClear;
        public string ImagePath = "";
        public string Summary = "";
    }

    private sealed class CoreTacticalWeaponVisualAuditResult
    {
        public bool AllClear;
        public string ImagePath = "";
        public string Summary = "";
    }
#endif

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

    private static void ValidateCoreTacticalDamageResistanceModel(BigTestReport report)
    {
        GameObject target = null;
        try
        {
            GameObject tacticalDamageObject = new GameObject("Big Test Core Tactical Damage Model");
            CoreTacticalPrototypeHealth tacticalHealth = tacticalDamageObject.AddComponent<CoreTacticalPrototypeHealth>();
            CoreTacticalDamageProfile tacticalProfile = tacticalDamageObject.AddComponent<CoreTacticalDamageProfile>();
            tacticalHealth.maxHealth = 1000f;
            tacticalProfile.ConfigureDefense("frigate", 1000f, new CoreTacticalResistanceSet(40f, 0f, 0f, 20f), 400f, 200f);
            tacticalHealth.ResetHealth();
            CoreTacticalDamageRequest tacticalKineticRequest = CoreTacticalDamageRequest.Kinetic(100f, "Kinetic resistance test", 10f);
            tacticalKineticRequest.damageSpread = 0f;
            tacticalHealth.ApplyDamage(tacticalKineticRequest);
            bool kineticResistanceOk = Approximately(tacticalHealth.currentHealth, 930f, 0.01f);
            tacticalHealth.ResetHealth();
            CoreTacticalDamageRequest tacticalIgnoreRequest = CoreTacticalDamageRequest.Kinetic(100f, "Kinetic ignore test", 100f);
            tacticalIgnoreRequest.damageSpread = 0f;
            tacticalHealth.ApplyDamage(tacticalIgnoreRequest);
            bool tacticalIgnoreOk = Approximately(tacticalHealth.currentHealth, 900f, 0.01f);
            tacticalHealth.ResetHealth();
            CoreTacticalDamageRequest tacticalFireRequest = CoreTacticalDamageRequest.Thermal(100f, "Thermal fire tactical test", 0f, 100f);
            tacticalFireRequest.damageSpread = 0f;
            tacticalHealth.ApplyDamage(tacticalFireRequest);
            bool tacticalFireOk = tacticalProfile.activeFireCount == 1 && tacticalProfile.fireSectorCount == 2;
            report.Check(kineticResistanceOk && tacticalIgnoreOk && tacticalFireOk,
                "Core Tactical strategic damage profile resolves typed resistances, resistance ignore, thermal fire, and class fire sectors.");
            DestroyBigTestObject(tacticalDamageObject);

            target = new GameObject("Big Test Legacy Typed Damage Bridge");
            DamageableShip legacyDamage = target.AddComponent<DamageableShip>();
            legacyDamage.maxStructureHp = 1000f;
            legacyDamage.ResetDamageState();
            ArmorZone legacyZone = target.AddComponent<ArmorZone>();
            legacyZone.SetResistances(new CoreTacticalResistanceSet(50f, 0f, 0f, 40f));
            DamageHitContext legacyContext = new DamageHitContext
            {
                shellType = DamageShellType.ArmorPiercing,
                damageType = CoreTacticalDamageType.Kinetic,
                shellName = "Legacy kinetic bridge probe",
                damagePoints = 100f,
                hullDamageOnPenetration = 100f,
                resistanceIgnorePercent = 20f,
                hitNormal = Vector3.back,
                incomingDirection = Vector3.forward
            };
            DamageHitResult legacyResult = legacyZone.ReceiveHit(legacyContext);
            bool legacyBridgeOk = Approximately(legacyDamage.structureHp, 930f, 0.01f)
                && legacyResult.damageType == CoreTacticalDamageType.Kinetic
                && Approximately(legacyResult.effectiveResistancePercent, 30f, 0.01f)
                && legacyResult.outcome == DamageHitOutcome.Penetration;
            report.Check(legacyBridgeOk,
                "Legacy DamageableShip/ArmorZone bridge resolves the same typed resistance and resistance-ignore contract as Core Tactical.");
            DestroyBigTestObject(target);
            target = null;

#if UNITY_EDITOR
            string projectileText = ReadProjectText("Assets/Scripts/Systems/DamageProjectile.cs");
            bool shellImpulseRemoved = !projectileText.Contains("ApplyHighExplosiveImpulse")
                && !projectileText.Contains("highExplosiveImpulseScale")
                && !projectileText.Contains("ForceMode.Impulse");
            report.Check(shellImpulseRemoved,
                shellImpulseRemoved
                    ? "Gun projectiles no longer apply physical push impulse; they resolve typed resistance damage."
                    : "DamageProjectile still contains explosive impulse code.");

            string shipLoaderText = ReadProjectText("Assets/Scripts/Meta/ShipLoader.cs");
            bool meshArmorLoaderFallback = shipLoaderText.Contains("AddComponent<MeshArmorBody>()")
                && shipLoaderText.Contains("RebuildPlatesFromMesh()")
                && shipLoaderText.Contains("DefaultFallbackMeshArmorMm")
                && shipLoaderText.Contains("defaultResistances")
                && shipLoaderText.Contains("SetResistances")
                && shipLoaderText.Contains("AddComponent<ArmorZone>()");
            report.Check(meshArmorLoaderFallback,
                meshArmorLoaderFallback
                    ? "ShipLoader creates MeshArmorBody/ArmorZone fallback hit surfaces with typed resistance defaults."
                    : "ShipLoader typed resistance fallback is not wired to MeshArmorBody/ArmorZone.");

            string meshArmorBodyText = ReadProjectText("Assets/Scripts/Systems/MeshArmorBody.cs");
            string blenderShipsFolder = ProjectPath("Assets/ShipImports/Models/BlenderShips");
            string[] expectedShipFbxNames =
            {
                "WW_Imperial_PatrolFrigate_R02_Korshun.fbx",
                "WW_Imperial_PatrolFrigate_R02_Korshun_76mm_Twin.fbx",
                "WW_Imperial_CargoFrigate_Vozchik.fbx",
                "WW_Imperial_ArtilleryCruiser_R02_Barbet.fbx",
                "WW_Imperial_Battleship_Val.fbx"
            };
            HashSet<string> expectedShipFbxSet = new HashSet<string>(expectedShipFbxNames, StringComparer.OrdinalIgnoreCase);
            string[] importedShipFbxPaths = Directory.Exists(blenderShipsFolder)
                ? Directory.GetFiles(blenderShipsFolder, "*.fbx", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            bool blenderShipFbxImported = importedShipFbxPaths.Length >= expectedShipFbxNames.Length;
            for (int i = 0; i < expectedShipFbxNames.Length; i++)
            {
                string expectedName = expectedShipFbxNames[i];
                blenderShipFbxImported &= File.Exists(Path.Combine(blenderShipsFolder, expectedName));
            }
            for (int i = 0; i < importedShipFbxPaths.Length; i++)
            {
                string importedName = Path.GetFileName(importedShipFbxPaths[i]);
                blenderShipFbxImported &= !string.IsNullOrWhiteSpace(importedName)
                    && (expectedShipFbxSet.Contains(importedName)
                        || importedName.StartsWith("WW_Imperial_", StringComparison.OrdinalIgnoreCase));
            }
            string korshunFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Imperial_PatrolFrigate_R02_Korshun.fbx.meta");
            string barbetFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Imperial_ArtilleryCruiser_R02_Barbet.fbx.meta");
            string valFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Imperial_Battleship_Val.fbx.meta");
            bool blenderShipFbxReadable = korshunFbxMetaText.Contains("isReadable: 1")
                && barbetFbxMetaText.Contains("isReadable: 1")
                && valFbxMetaText.Contains("isReadable: 1");
            string turretImportsFolder = ProjectPath("Assets/ShipImports/Models/Turrets");
            string[] turretImportPaths = Directory.Exists(turretImportsFolder)
                ? Directory.GetFiles(turretImportsFolder, "*.fbx", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            bool separateTurretImportsReady = turretImportPaths.Length >= 20
                && !File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_WeaponModules.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Base_Small_W.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Base_Cone_Small.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Base_Cross_Small.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_30mm_Single.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_76mm_Twin.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_100mm_Single.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_100mm_Twin_PMK.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_200mm_Mortar.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_TorpedoLauncher_3Tube.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_RocketLauncher_Pod.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_Magnet_Single.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_GasSiphon_Single.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_RepairBeam_Single.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/Turrets/WW_Turret_HackingDish_Single.fbx"));
            string baseIslandViewText = ReadProjectText("Assets/Scripts/City/WildWindBaseIslandView.cs");
            string blenderTurretExporterText = ReadProjectText("Docs/BlenderAssets/export_blender_asset_to_fbx.py");
            bool portPreviewUsesAuthoredShipsWithMappedLoadoutVisuals = baseIslandViewText.Contains("ApplyPortDockImportedModelLoadoutVisibility")
                && baseIslandViewText.Contains("ApplyPortDockKorshunMainLoadoutVisual")
                && baseIslandViewText.Contains("ApplyPortDockKorshunAuxiliaryLoadoutVisual")
                && baseIslandViewText.Contains("Korshun_PortPreview_Turret_30mm_Single_Fore")
                && baseIslandViewText.Contains("Korshun_PortPreview_Autocannon_57mm_Triple_Fore")
                && baseIslandViewText.Contains("WW_Turret_30mm_Single")
                && baseIslandViewText.Contains("WW_Turret_RocketLauncher_Pod")
                && baseIslandViewText.Contains("WW_Turret_TorpedoLauncher_3Tube")
                && baseIslandViewText.Contains("WW_Turret_76mm_Twin")
                && baseIslandViewText.Contains("WW_Turret_100mm_Single")
                && baseIslandViewText.Contains("WW_Turret_200mm_Mortar")
                && baseIslandViewText.Contains("WW_Turret_Magnet_Single")
                && baseIslandViewText.Contains("WW_Turret_GasSiphon_Single")
                && baseIslandViewText.Contains("WW_Turret_RepairBeam_Single")
                && baseIslandViewText.Contains("WW_Turret_HackingDish_Single")
                && baseIslandViewText.Contains("Korshun_PortAuxPreview_")
                && baseIslandViewText.Contains("37mm_mg_aura")
                && baseIslandViewText.Contains("57mm_triple_autocannon")
                && baseIslandViewText.Contains("korshun_main_76mm_twin")
                && baseIslandViewText.Contains("100mm_single")
                && baseIslandViewText.Contains("nurs_turret")
                && baseIslandViewText.Contains("200mm_mortar")
                && baseIslandViewText.Contains("torpedo_triple_side")
                && baseIslandViewText.Contains("side_nurs")
                && baseIslandViewText.Contains("siphon")
                && baseIslandViewText.Contains("repair_beam")
                && baseIslandViewText.Contains("scanner_hacker")
                && baseIslandViewText.Contains("Quaternion.Euler(0f, yawDegrees, 0f)")
                && !baseIslandViewText.Contains("pitchDegrees")
                && blenderTurretExporterText.Contains("force_bake_missing_x_mirror")
                && blenderTurretExporterText.Contains("source_name = (source.name or \"\").lower()")
                && blenderTurretExporterText.Contains("\"_left\" in source_name")
                && blenderTurretExporterText.Contains("\"_right\" in source_name")
                && blenderTurretExporterText.Contains("abs(source.matrix_world.translation.x) > 0.0001")
                && blenderTurretExporterText.Contains("unity-module-y-up")
                && !baseIslandViewText.Contains("LoadPortDockTurretModelPrefab")
                && !baseIslandViewText.Contains("BuildPortDockKorshunLoadoutOverlay")
                && !baseIslandViewText.Contains("CreateKorshunPreviewImportedMountPair")
                && !baseIslandViewText.Contains("CreateKorshunPreviewMortarPair");
            PortDockTurretImportAuditResult turretImportAuditResult = AuditPortDockTurretImports(report);
            bool materialArmorImportReady = meshArmorBodyText.Contains("useMaterialArmorNames")
                && meshArmorBodyText.Contains("TryRebuildPlatesFromMaterials")
                && meshArmorBodyText.Contains("TryParseArmorMaterialName")
                && meshArmorBodyText.Contains("mesh.GetTriangles(subMesh)")
                && meshArmorBodyText.Contains("mesh.isReadable");
            bool unityArmorPaintRemoved = !File.Exists(ProjectPath("Assets/Scenes/WildWindArmorSetup.unity"))
                && !File.Exists(ProjectPath("Assets/Scripts/Systems/WildWindArmorSetupSceneTool.cs"))
                && !File.Exists(ProjectPath("Assets/Scripts/Systems/PaintedArmorBody.cs"))
                && !projectileText.Contains("PaintedArmorBody")
                && !shipLoaderText.Contains("PaintedArmorBody");
            bool blenderMaterialArmorReady = unityArmorPaintRemoved
                && blenderShipFbxImported
                && blenderShipFbxReadable
                && materialArmorImportReady;
            report.Check(blenderMaterialArmorReady,
                blenderMaterialArmorReady
                    ? "Unity armor painting setup is removed; Blender ship FBX models remain readable and MeshArmorBody treats legacy Armor_XX material names as resistance hints."
                    : "Blender resistance-hint import is not clean: old Unity armor painting setup remains, FBX sources are missing/unreadable, or material-name parsing is absent.");
            report.Check(separateTurretImportsReady && portPreviewUsesAuthoredShipsWithMappedLoadoutVisuals && turretImportAuditResult.AllClear,
                separateTurretImportsReady && portPreviewUsesAuthoredShipsWithMappedLoadoutVisuals && turretImportAuditResult.AllClear
                    ? "Reusable Blender bases, turrets, rocket pods, utility mounts, and torpedo launchers remain exported as separate Unity FBX imports, lie upright in Unity, keep side-authored Left/Right meshes from being mirrored across the ship centerline, and dock preview maps Korshun main and auxiliary packages onto mounted ship visuals. Audit image: " + turretImportAuditResult.ImagePath
                    : "Separate turret/base import contract is broken, at least one Unity-imported module is standing on its nose, a side-authored Left/Right mesh may be mirrored across the ship centerline, or the dock preview is not mapping Korshun main and auxiliary packages onto mounted ship visuals cleanly. " + turretImportAuditResult.Summary);
#else
            report.Check(true, "DamageProjectile impulse source scan is editor-only and skipped in player builds.");
#endif
        }
        finally
        {
            DestroyBigTestObject(target);
        }
    }

    private static bool TryInvokePrivateMethod(object target, string methodName, BigTestReport report)
    {
        if (target == null)
        {
            report.Fail("РќРµ СѓРґР°Р»РѕСЃСЊ РІС‹Р·РІР°С‚СЊ " + methodName + ": С†РµР»РµРІРѕР№ РѕР±СЉРµРєС‚ РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚.");
            return false;
        }

        System.Reflection.MethodInfo method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null)
        {
            report.Fail("Р’ " + target.GetType().Name + " РЅРµ РЅР°Р№РґРµРЅ РІРЅСѓС‚СЂРµРЅРЅРёР№ РјРµС‚РѕРґ " + methodName + ".");
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
            report.Fail("Р’РЅСѓС‚СЂРµРЅРЅРёР№ РјРµС‚РѕРґ " + target.GetType().Name + "." + methodName + " СѓРїР°Р»: " + root.GetType().Name + " - " + root.Message);
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

    private static GameObject FindSceneGameObjectIncludingInactive(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
        {
            return activeObject;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            Scene scene = candidate.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                return candidate;
            }
        }

        return null;
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
        return (meters / 1000f).ToString("0.#") + " РєРј";
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

    private static bool TryFindVisibleShipMeshSize(MeshFilter[] filters, bool requireEnabledRenderer, out Vector3 size)
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
            if (requireEnabledRenderer && (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy))
            {
                continue;
            }

            Vector3 meshSize = filter.sharedMesh.bounds.size;
            Vector3 scale = filter.transform.lossyScale;
            Vector3 scaledSize = new Vector3(
                Mathf.Abs(meshSize.x * scale.x),
                Mathf.Abs(meshSize.y * scale.y),
                Mathf.Abs(meshSize.z * scale.z));

            if (IsFinite(scaledSize) && scaledSize.sqrMagnitude > 0.01f)
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
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
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
            AppendReportLine("=== Wild Wind: Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚ ===");
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
            AppendReportLine("## " + title);
        }

        public void Info(string message)
        {
            infoCount++;
            AppendReportLine("- INFO: " + message);
        }

        public void Pass(string message)
        {
            checkCount++;
            IncrementCurrentSectionChecks();
            AppendReportLine("- OK: " + message);
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
            string normalizedMessage = NormalizeReportText(message);
            AppendReportLine("- WARN: " + normalizedMessage);
            if (logImmediateMessages)
            {
                Debug.LogWarning(LogPrefix + "WARN" + FormatSection() + ": " + normalizedMessage, context);
            }
        }

        public void Fail(string message)
        {
            checkCount++;
            IncrementCurrentSectionChecks();
            failureCount++;
            string normalizedMessage = NormalizeReportText(message);
            AppendReportLine("- FAIL: " + normalizedMessage);
            if (logImmediateMessages)
            {
                Debug.LogError(LogPrefix + "FAIL" + FormatSection() + ": " + normalizedMessage, context);
            }
        }

        public void AssertIntegrity(string[] requiredSections, int minimumChecks, Func<bool> canaryProbe)
        {
            int preIntegrityCheckCount = checkCount;
            Section("Р¦РµР»РѕСЃС‚РЅРѕСЃС‚СЊ Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р°");

            List<string> missingSections = GetMissingSections(requiredSections);
            List<string> emptySections = GetEmptySections(requiredSections);
            Check(missingSections.Count == 0,
                missingSections.Count == 0
                    ? "Р’СЃРµ РѕР±СЏР·Р°С‚РµР»СЊРЅС‹Рµ СЂР°Р·РґРµР»С‹ Р±РѕР»СЊС€РѕРіРѕ С‚РµСЃС‚Р° Р±С‹Р»Рё Р·Р°РїСѓС‰РµРЅС‹."
                    : "РќРµ Р±С‹Р»Рё Р·Р°РїСѓС‰РµРЅС‹ РѕР±СЏР·Р°С‚РµР»СЊРЅС‹Рµ СЂР°Р·РґРµР»С‹: " + JoinNames(missingSections) + ".");
            Check(emptySections.Count == 0,
                emptySections.Count == 0
                    ? "РљР°Р¶РґС‹Р№ РѕР±СЏР·Р°С‚РµР»СЊРЅС‹Р№ СЂР°Р·РґРµР» СЃРѕРґРµСЂР¶РёС‚ С…РѕС‚СЏ Р±С‹ РѕРґРЅСѓ OK/FAIL РїСЂРѕРІРµСЂРєСѓ."
                    : "РћР±СЏР·Р°С‚РµР»СЊРЅС‹Рµ СЂР°Р·РґРµР»С‹ Р±РµР· РїСЂРѕРІРµСЂРѕРє: " + JoinNames(emptySections) + ".");
            Check(preIntegrityCheckCount >= minimumChecks,
                "РљРѕР»РёС‡РµСЃС‚РІРѕ РїСЂРѕРІРµСЂРѕРє РґРѕ self-check РЅРµ РЅРёР¶Рµ РєРѕРЅС‚СЂР°РєС‚Р°: " + preIntegrityCheckCount + " / " + minimumChecks + ".");
            Check(canaryProbe != null && canaryProbe(),
                "Canary-СЃР±РѕР№ РґРµР»Р°РµС‚ РјР°С€РёРЅРЅС‹Р№ СЂРµР·СѓР»СЊС‚Р°С‚ РєСЂР°СЃРЅС‹Рј Рё РЅРµ РїСЂРѕС…РѕРґРёС‚ РєР°Рє OK.");
        }

        public void Finish(long elapsedMs)
        {
            builder.AppendLine();
            AppendReportLine("## РС‚РѕРі");
            AppendReportLine("- РџСЂРѕРІРµСЂРѕРє OK/FAIL: " + checkCount);
            AppendReportLine("- РРЅС„РѕСЂРјР°С†РёРѕРЅРЅС‹С… СЃС‚СЂРѕРє: " + infoCount);
            AppendReportLine("- РџСЂРµРґСѓРїСЂРµР¶РґРµРЅРёР№: " + warningCount);
            AppendReportLine("- РћС€РёР±РѕРє: " + failureCount);
            AppendReportLine("- Р’СЂРµРјСЏ РІС‹РїРѕР»РЅРµРЅРёСЏ: " + elapsedMs + " РјСЃ");
            AppendReportLine(failureCount == 0
                ? "- Р РµР·СѓР»СЊС‚Р°С‚: OK, Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚ РїСЂРѕР№РґРµРЅ."
                : "- Р РµР·СѓР»СЊС‚Р°С‚: РќР• РћРљ, Р±РѕР»СЊС€РѕР№ С‚РµСЃС‚ РЅР°С€С‘Р» РїСЂРѕР±Р»РµРјС‹.");
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
            return string.IsNullOrWhiteSpace(currentSection) ? "" : " [" + NormalizeReportText(currentSection) + "]";
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

        private void AppendReportLine(string line)
        {
            builder.AppendLine(NormalizeReportText(line));
        }

        private static string NormalizeReportText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? "";
            }

            string current = text;
            for (int i = 0; i < 3; i++)
            {
                int currentScore = GetMojibakeScore(current);
                if (currentScore <= 0 || !TryRepairMojibakeOnce(current, out string repaired))
                {
                    break;
                }

                int repairedScore = GetMojibakeScore(repaired);
                if (repairedScore >= currentScore)
                {
                    break;
                }

                current = repaired;
            }

            return current;
        }

        private static bool TryRepairMojibakeOnce(string text, out string repaired)
        {
            repaired = text;
            byte[] bytes = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                if (!TryGetWindows1251Byte(text[i], out bytes[i]))
                {
                    return false;
                }
            }

            try
            {
                repaired = StrictUtf8.GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        private static int GetMojibakeScore(string text)
        {
            int score = 0;
            for (int i = 0; i < text.Length - 1; i++)
            {
                char lead = text[i];
                if ((lead == 'Р' || lead == 'С') && TryGetWindows1251Byte(text[i + 1], out byte trail) && trail >= 0x80 && trail <= 0xBF)
                {
                    score += 4;
                }
                else if (lead == 'В' && TryGetWindows1251Byte(text[i + 1], out trail) && trail >= 0x80 && trail <= 0xBF)
                {
                    score++;
                }
            }

            return score;
        }

        private static bool TryGetWindows1251Byte(char value, out byte encoded)
        {
            if (value <= 0x7F || (value >= 0x80 && value <= 0x9F))
            {
                encoded = (byte)value;
                return true;
            }

            if (value >= 'А' && value <= 'я')
            {
                encoded = (byte)(0xC0 + (value - 'А'));
                return true;
            }

            switch (value)
            {
                case '\u0402': encoded = 0x80; return true;
                case '\u0403': encoded = 0x81; return true;
                case '\u201A': encoded = 0x82; return true;
                case '\u0453': encoded = 0x83; return true;
                case '\u201E': encoded = 0x84; return true;
                case '\u2026': encoded = 0x85; return true;
                case '\u2020': encoded = 0x86; return true;
                case '\u2021': encoded = 0x87; return true;
                case '\u20AC': encoded = 0x88; return true;
                case '\u2030': encoded = 0x89; return true;
                case '\u0409': encoded = 0x8A; return true;
                case '\u2039': encoded = 0x8B; return true;
                case '\u040A': encoded = 0x8C; return true;
                case '\u040C': encoded = 0x8D; return true;
                case '\u040B': encoded = 0x8E; return true;
                case '\u040F': encoded = 0x8F; return true;
                case '\u0452': encoded = 0x90; return true;
                case '\u2018': encoded = 0x91; return true;
                case '\u2019': encoded = 0x92; return true;
                case '\u201C': encoded = 0x93; return true;
                case '\u201D': encoded = 0x94; return true;
                case '\u2022': encoded = 0x95; return true;
                case '\u2013': encoded = 0x96; return true;
                case '\u2014': encoded = 0x97; return true;
                case '\u2122': encoded = 0x99; return true;
                case '\u0459': encoded = 0x9A; return true;
                case '\u203A': encoded = 0x9B; return true;
                case '\u045A': encoded = 0x9C; return true;
                case '\u045C': encoded = 0x9D; return true;
                case '\u045B': encoded = 0x9E; return true;
                case '\u045F': encoded = 0x9F; return true;
                case '\u00A0': encoded = 0xA0; return true;
                case '\u040E': encoded = 0xA1; return true;
                case '\u045E': encoded = 0xA2; return true;
                case '\u0408': encoded = 0xA3; return true;
                case '\u00A4': encoded = 0xA4; return true;
                case '\u0490': encoded = 0xA5; return true;
                case '\u00A6': encoded = 0xA6; return true;
                case '\u00A7': encoded = 0xA7; return true;
                case '\u0401': encoded = 0xA8; return true;
                case '\u00A9': encoded = 0xA9; return true;
                case '\u0404': encoded = 0xAA; return true;
                case '\u00AB': encoded = 0xAB; return true;
                case '\u00AC': encoded = 0xAC; return true;
                case '\u00AD': encoded = 0xAD; return true;
                case '\u00AE': encoded = 0xAE; return true;
                case '\u0407': encoded = 0xAF; return true;
                case '\u00B0': encoded = 0xB0; return true;
                case '\u00B1': encoded = 0xB1; return true;
                case '\u0406': encoded = 0xB2; return true;
                case '\u0456': encoded = 0xB3; return true;
                case '\u0491': encoded = 0xB4; return true;
                case '\u00B5': encoded = 0xB5; return true;
                case '\u00B6': encoded = 0xB6; return true;
                case '\u00B7': encoded = 0xB7; return true;
                case '\u0451': encoded = 0xB8; return true;
                case '\u2116': encoded = 0xB9; return true;
                case '\u0454': encoded = 0xBA; return true;
                case '\u00BB': encoded = 0xBB; return true;
                case '\u0458': encoded = 0xBC; return true;
                case '\u0405': encoded = 0xBD; return true;
                case '\u0455': encoded = 0xBE; return true;
                case '\u0457': encoded = 0xBF; return true;
                default:
                    encoded = 0;
                    return false;
            }
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
            ReportText = reason ?? "Р‘РѕР»СЊС€РѕР№ С‚РµСЃС‚ РЅРµ Р±С‹Р» Р·Р°РїСѓС‰РµРЅ."
        };
    }
}
