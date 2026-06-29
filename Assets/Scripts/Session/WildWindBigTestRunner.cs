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
        "Pioneer ship catalog",
        "Starter hull visual asset contract",
        "Session legacy cleanup",
        "Session-only runtime",
        "Runtime account",
        "Р’РёР·СѓР°Р», РІС‹СЃРѕС‚РЅС‹Рµ СЃР»РѕРё Рё С‚СѓРјР°РЅ",
        "РќР°СЃС‚СЂРѕР№РєРё РїСЂРѕРµРєС‚Р° Рё СѓРїСЂР°РІР»РµРЅРёРµ",
        "РљРѕСЂР°Р±Р»СЊ, РІРµС‚РµСЂ Рё Р»С‘С‚РЅР°СЏ С„РёР·РёРєР°",
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
            ValidateShipWindAerodynamics(report);
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
        report.Info("Currently covered: CSV config, tech tree, ship tree, production, session-only runtime, runtime account, persistent progress, visual dependencies, settings, wind/aerodynamics, flight physics, direct port entry and progress reset.");
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
            && config.GetHull("cruiser203_hull") == null
            && config.claudiumLoops.Count == 0
            && config.GetClaudiumLoop("starter_claudium_loop") == null,
            "Old runtime hull and claudium-loop CSV rows are removed until the new Blender ships are imported.");
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
        report.Check(config.shipTreeEntries.Count == 465
            && config.GetShipTreeEntry("pioneer") == null
            && config.GetShipTreeEntry("cruiser203") == null,
            "Ship_tree.csv contains only the 465-ship faction development roster; old runtime Pioneer/Cruiser 203 rows are removed.");
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
        CheckUniqueIds(config.specialModules, module => module.id, "СЃРїРµС†РјРѕРґСѓР»РµР№", report);
        CheckUniqueIds(config.shipTreeEntries, ship => ship.shipId, "РєРѕСЂР°Р±Р»РµР№ РІ Ship_tree.csv", report);
        CheckUniqueIds(config.questDefinitions, quest => quest.id, "Quest.csv tasks", report);
        ValidateShipTreeConfig(config, report);
        ValidateQuestConfig(config, report);
        ValidateConfigReferences(config, report);
        ValidateSessionPortConfig(config, report);
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
        string licenses = ReadProjectText("Docs/Balance/PortConfigs/faction_r10_licenses.csv");
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

        bool marketReady = market.Contains("stone_crushing_crown")
            && market.Contains("mist_gas_membrane")
            && market.Contains("ark_precision_drive")
            && market.Contains("dev_harpoon_winch")
            && market.Contains("fquest_stone_vault_10")
            && market.Contains("fquest_factory_ark_20")
            && meta.Contains("FactionMarketItems")
            && meta.Contains("TryBuyFactionMarketItem")
            && meta.Contains("currencyItemId = string.IsNullOrWhiteSpace(spec.currencyItemId)")
            && licenses.Contains("price_currency,price_amount")
            && licenses.Contains("solid,700");
        report.Check(marketReady,
            "Faction shops expose normal goods, non-craftable component locks and Solid-priced one-use R10 licenses.");

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
            report.Fail("Ship_tree.csv РЅРµ Р·Р°РіСЂСѓР¶РµРЅ.");
            return;
        }

        bool entriesValid = config.shipTreeEntries.Count == 465;
        List<string> invalidShipTreeEntryIds = new List<string>();
        int developmentShipCount = 0;
        int developmentStarterCount = 0;
        int developmentBranchShipCount = 0;
        int placeholderDevelopmentModels = 0;
        int runtimeReadyDevelopmentHulls = 0;

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null)
            {
                entriesValid = false;
                continue;
            }

            bool isRoot = entry.rank == 0;
            bool isDevelopment = entry.IsDevelopmentRosterShip;
            bool isDevelopmentStarter = isDevelopment &&
                entry.treeTier == 1 &&
                string.Equals(entry.branchId, "starter", StringComparison.OrdinalIgnoreCase);
            if (isDevelopment)
            {
                developmentShipCount++;
                if (entry.treeTier == 1 && entry.branchId == "starter")
                {
                    developmentStarterCount++;
                }
                else
                {
                    developmentBranchShipCount++;
                }

                if (entry.visualModelId == "placeholder_square" && entry.visualShapeId == "square")
                {
                    placeholderDevelopmentModels++;
                }

                if (entry.HasRuntimeHull)
                {
                    runtimeReadyDevelopmentHulls++;
                }
            }

            bool entryValid = !string.IsNullOrWhiteSpace(entry.shipId) &&
                !string.IsNullOrWhiteSpace(entry.localNameRu) &&
                !string.IsNullOrWhiteSpace(entry.localNameEn) &&
                !string.IsNullOrWhiteSpace(entry.classNameRu) &&
                !string.IsNullOrWhiteSpace(entry.roleId) &&
                !string.IsNullOrWhiteSpace(entry.roleNameRu) &&
                !string.IsNullOrWhiteSpace(entry.summaryRu) &&
                (isRoot || isDevelopmentStarter || (entry.parentShipIds != null && entry.parentShipIds.Count > 0)) &&
                (string.IsNullOrWhiteSpace(entry.requiredTechnologyId) || config.GetTechnology(entry.requiredTechnologyId) != null) &&
                (string.IsNullOrWhiteSpace(entry.hullId) || config.GetHull(entry.hullId) != null) &&
                (string.IsNullOrWhiteSpace(entry.claudiumLoopId) || config.GetClaudiumLoop(entry.claudiumLoopId) != null) &&
                (string.IsNullOrWhiteSpace(entry.specialModuleId) || config.GetSpecialModule(entry.specialModuleId) != null) &&
                AllIdsExistAllowEmpty(entry.upgradeHullIds, config.GetHull) &&
                AllIdsExistAllowEmpty(entry.upgradeClaudiumLoopIds, config.GetClaudiumLoop) &&
                AllIdsExistAllowEmpty(entry.upgradeSpecialModuleIds, config.GetSpecialModule);

            if (isDevelopment)
            {
                bool placeholderOrRuntimeModel = entry.HasRuntimeHull ||
                    (entry.visualModelId == "placeholder_square" && entry.visualShapeId == "square");
                entryValid &= !string.IsNullOrWhiteSpace(entry.factionId) &&
                    !string.IsNullOrWhiteSpace(entry.factionNameRu) &&
                    IsKnownDevelopmentShipClass(entry.shipClassId) &&
                    !string.IsNullOrWhiteSpace(entry.shipClassNameRu) &&
                    !string.IsNullOrWhiteSpace(entry.branchId) &&
                    !string.IsNullOrWhiteSpace(entry.BranchDisplayNameRu) &&
                    entry.treeTier >= 1 &&
                    entry.treeTier <= 10 &&
                    entry.treeRow >= 0 &&
                    entry.rank == entry.treeTier &&
                    entry.catalogScope == "development" &&
                    placeholderOrRuntimeModel &&
                    !string.IsNullOrWhiteSpace(entry.costCurrencyItemId) &&
                    config.GetItem(entry.costCurrencyItemId) != null &&
                    (entry.treeTier == 1 ? entry.costAmount == 0 : entry.costAmount > 0) &&
                    entry.TotalStatScore > 0 &&
                    HasDevelopmentRatingScore(entry);
            }

            if (entry.parentShipIds != null)
            {
                for (int j = 0; j < entry.parentShipIds.Count; j++)
                {
                    string parentId = entry.parentShipIds[j];
                    ShipTreeEntryConfig parent = config.GetShipTreeEntry(parentId);
                    entryValid &= parent != null &&
                        parent.shipId != entry.shipId &&
                        parent.rank <= entry.rank;
                }
            }

            entriesValid &= entryValid;
            if (!entryValid && invalidShipTreeEntryIds.Count < 8)
            {
                invalidShipTreeEntryIds.Add(string.IsNullOrWhiteSpace(entry.shipId) ? "<empty>" : entry.shipId);
            }
        }

        report.Check(entriesValid
                && config.GetShipTreeEntry("pioneer") == null
                && config.GetShipTreeEntry("cruiser203") == null,
            "Ship_tree.csv gives every development ship faction/class/cost/model/stat fields and contains no old runtime Pioneer/Cruiser 203 rows. Actual: count="
            + config.shipTreeEntries.Count
            + "/465"
            + ", invalidExamples="
            + (invalidShipTreeEntryIds.Count == 0 ? "none" : string.Join(",", invalidShipTreeEntryIds)));

        report.Check(developmentShipCount == 465
                && developmentStarterCount == 6
                && developmentBranchShipCount == 459
                && placeholderDevelopmentModels == 465
                && runtimeReadyDevelopmentHulls == 0
                && ShipDevelopmentRosterCountsValid(config),
            "Development ship roster contains 6 faction starters plus 51 full R2-R10 branches.");

        report.Check(ShipDevelopmentTechTreeLayoutValid(config),
            "Development ship tree has supplier branches laid out across tiers I-X from faction starter ships.");

        report.Check(!ShipTreeHasCycles(config),
            "Р”РµСЂРµРІРѕ РєРѕСЂР°Р±Р»РµР№ РЅРµ СЃРѕРґРµСЂР¶РёС‚ С†РёРєР»РѕРІ РїРѕ parent_ship_id.");

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
            config.GetShipTreeEntry("pioneer") == null &&
            config.GetShipTreeEntry("cruiser203") == null &&
            config.GetHull(GameplaySessionAccountData.DefaultStarterHullId) == null &&
            config.GetHull("cruiser203_hull") == null &&
            config.GetClaudiumLoop("starter_claudium_loop") == null;
        report.Check(configClean,
            "Old runtime Pioneer/Cruiser203 ship records and starter claudium loop are removed from loaded config.");

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

        string shipTreeText = ReadProjectText("Assets/Data/Config/Ship_tree.csv");
        string hullText = ReadProjectText("Assets/Data/Config/Hull.csv");
        string claudiumLoopText = ReadProjectText("Assets/Data/Config/Claudium_loop.csv");
        string questText = ReadProjectText("Assets/Data/Config/Quest.csv");
        bool csvTextClean =
            !shipTreeText.Contains("cruiser203") &&
            !shipTreeText.Contains("starter_hull") &&
            !shipTreeText.Contains("starter_claudium_loop") &&
            !hullText.Contains("starter_hull") &&
            !hullText.Contains("cruiser203_hull") &&
            !claudiumLoopText.Contains("starter_claudium_loop") &&
            !questText.Contains("quest_select_cruiser203");

        report.Check(leftovers.Count == 0 && csvTextClean,
            leftovers.Count == 0 && csvTextClean
                ? "Old generated ship catalog assets, ship prefabs, ship parts and runtime CSV rows are gone."
                : "Old ship leftovers remain: "
                    + (leftovers.Count == 0 ? "no asset paths" : string.Join(", ", leftovers))
                    + ", csvClean="
                    + csvTextClean);
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

    private void ValidateShipWindAerodynamics(BigTestReport report)
    {
        report.Section("РљРѕСЂР°Р±Р»СЊ, РІРµС‚РµСЂ Рё Р»С‘С‚РЅР°СЏ С„РёР·РёРєР°");
        ValidateActivePlayerShipVisual(report);
        ValidateBallisticFireControl(report);
        ValidateArmorDamageModel(report);
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
            report.Check(Approximately(ship.CurrentWindAerodynamicFactor, 0.5f, 0.001f), "РђСЌСЂРѕРґРёРЅР°РјРёРєР° 0.5 РґР°С‘С‚ РєРѕСЌС„С„РёС†РёРµРЅС‚ РІРµС‚СЂР° 0.5.");
            report.Check(Approximately(ship.EffectiveWindVelocity.magnitude, 5f, 0.001f), "Р’РµС‚РµСЂ 10 Рј/СЃ РїСЂРё Р°СЌСЂРѕРґРёРЅР°РјРёРєРµ 0.5 РѕС‰СѓС‰Р°РµС‚СЃСЏ РєР°Рє 5 Рј/СЃ.");

            ship.dragCoefficient = 1.2f;
            ship.windVelocity = new Vector3(0f, 0f, 10f);
            report.Check(Approximately(ship.EffectiveWindVelocity.magnitude, 12f, 0.001f), "РџР»РѕС…Р°СЏ Р°СЌСЂРѕРґРёРЅР°РјРёРєР° 1.2 СѓСЃРёР»РёРІР°РµС‚ РІРѕР·РґРµР№СЃС‚РІРёРµ РІРµС‚СЂР° РґРѕ 12 Рј/СЃ.");

            ship.baseMaxSpeedMS = 50f;
            ship.hullCruiseReferenceSpeedMS = 50f;
            ship.slipstreamActivationSpeedRatio = 0.8f;
            ship.slipstreamMaxSpeedMultiplier = 5f;
            ship.slipstreamFuelConsumptionMultiplier = 2f;
            body.linearVelocity = new Vector3(39f, 0f, 0f);
            bool slipstreamBlockedBelowSpeed = !ship.TrySetClaudiumSlipstreamEnabled(true, out _);
            body.linearVelocity = new Vector3(41f, 0f, 0f);
            bool slipstreamEnabledAboveSpeed = ship.TrySetClaudiumSlipstreamEnabled(true, out _);
            ship.claudiumSlipstreamCharge01 = 1f;
            bool slipstreamFullEffect = Approximately(ship.CurrentMaxSpeedMS, 250f, 0.001f)
                && Approximately(ship.ClaudiumSlipstreamDragMultiplier, 1f, 0.001f)
                && Approximately(ship.CurrentFuelConsumptionMultiplier, 2f, 0.001f);
            body.linearVelocity = new Vector3(39f, 0f, 0f);
            bool slipstreamDisabledAfterSlowdown = TryInvokePrivateMethod(ship, "FixedUpdate", report)
                && !ship.claudiumSlipstreamEnabled;
            report.Check(slipstreamBlockedBelowSpeed
                && slipstreamEnabledAboveSpeed
                && slipstreamDisabledAfterSlowdown
                && slipstreamFullEffect,
                "Claudium slipstream uses a relative 80% clean-speed threshold, ramps max ход x5, and doubles fixed fuel burn.");
            ship.TrySetClaudiumSlipstreamEnabled(false, out _);
            ship.claudiumSlipstreamCharge01 = 0f;

            probeScene = SceneManager.CreateScene("Wild Wind Big Test Flight Probe", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            report.Check(probeScene.IsValid(), "РР·РѕР»РёСЂРѕРІР°РЅРЅР°СЏ СЃС†РµРЅР° РґР»СЏ РїСЂРѕРІРµСЂРєРё Р»С‘С‚РЅРѕР№ С„РёР·РёРєРё СЃРѕР·РґР°РЅР°.");
            if (!probeScene.IsValid())
            {
                return;
            }

            SceneManager.MoveGameObjectToScene(testShip, probeScene);
            PhysicsScene physicsScene = probeScene.GetPhysicsScene();
            report.Check(physicsScene.IsValid(), "РР·РѕР»РёСЂРѕРІР°РЅРЅР°СЏ 3D physics-СЃС†РµРЅР° РІР°Р»РёРґРЅР°.");
            if (!physicsScene.IsValid())
            {
                return;
            }

            ConfigureFlightProbeShip(ship, body);
            float expectedMass = ship.baseMass + ship.cargoMassKg;
            report.Check(Approximately(body.mass, expectedMass, 0.001f), "Rigidbody РїРѕР»СѓС‡Р°РµС‚ СЃСѓС…СѓСЋ РјР°СЃСЃСѓ Рё РіСЂСѓР·: " + body.mass.ToString("0.#") + " РєРі.");

            float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float fuelBeforeLift = ship.fuelStockKg;
            float claudiumBeforeLift = ship.claudiumStock;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedLiftN = body.mass * 9.81f;
                report.Check(Approximately(ship.claudiumRequestedLiftKg, body.mass, 0.5f), "РљР»Р°РІРґРёРµРІС‹Р№ РєРѕРЅС‚СѓСЂ Р·Р°РїСЂР°С€РёРІР°РµС‚ С‚СЂРёРјРјРёСЂСѓРµРјСѓСЋ РјР°СЃСЃСѓ РєРѕСЂР°Р±Р»СЏ: " + ship.claudiumRequestedLiftKg.ToString("0.#") + " РєРі.");
                report.Check(Approximately(ship.claudiumCurrentLiftN, expectedLiftN, expectedLiftN * 0.02f), "РљР»Р°РІРґРёРµРІС‹Р№ РєРѕРЅС‚СѓСЂ РІС‹РґР°С‘С‚ РїРѕРґСЉС‘РјРЅСѓСЋ СЃРёР»Сѓ РїСЂРёРјРµСЂРЅРѕ РІРµСЃР° РєРѕСЂР°Р±Р»СЏ: " + ship.claudiumCurrentLiftN.ToString("0.#") + " Рќ.");
                report.Check(Approximately(fuelBeforeLift, ship.fuelStockKg, 0.0001f)
                    && Approximately(claudiumBeforeLift, ship.claudiumStock, 0.0001f),
                    "Lift is free: it does not consume coal or claudium.");
            }

            float fuelBeforeTick = ship.fuelStockKg;
            if (TryInvokePrivateMethod(ship, "FixedUpdate", report))
            {
                float expectedFuelBurn = Mathf.Max(0f, ship.fuelConsumptionKgPerMinute)
                    * ship.CurrentFuelConsumptionMultiplier
                    / 60f
                    * fixedDeltaTime;
                report.Check(Approximately(fuelBeforeTick - ship.fuelStockKg, expectedFuelBurn, Mathf.Max(0.0001f, expectedFuelBurn * 0.05f)),
                    "Hull fuel burn uses the fixed kg-per-minute rate.");
            }

            ship.thrustInput = 1f;
            ship.hullThrustOutput = 0f;
            ship.hullThrustResponseRate01PerSecond = 0.10f;
            if (TryInvokePrivateMethod(ship, "UpdateHullThrustOutput", report)
                && TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedHullThrustStep = 0.10f * fixedDeltaTime;
                report.Check(Approximately(ship.hullThrustOutput, expectedHullThrustStep, 0.0002f),
                    "Hull thrust output follows the requested thrust with a 10% per second response: "
                    + ship.hullThrustOutput.ToString("0.0000") + ".");
            }

            ship.claudiumStock = 20f;
            ship.claudiumCurrentLiftN = 0f;
            ship.claudiumLoopResponseRate01PerSecond = 0.10f;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedLoopStepN = ship.claudiumMaxLiftKg * 9.81f * 0.10f * fixedDeltaTime;
                report.Check(Approximately(ship.claudiumCurrentLiftN, expectedLoopStepN, 0.05f),
                    "РљР»Р°РІРґРёРµРІС‹Р№ РєРѕРЅС‚СѓСЂ РјРµРЅСЏРµС‚ С„Р°РєС‚РёС‡РµСЃРєРёР№ РїРѕРґСЉС‘Рј СЃ РїСЂРёС‘РјРёСЃС‚РѕСЃС‚СЊСЋ 10% РјР°РєСЃРёРјСѓРјР° РІ СЃРµРєСѓРЅРґСѓ: "
                    + ship.claudiumCurrentLiftN.ToString("0.###") + " Рќ.");
            }

            ship.claudiumStock = 0f;
            ship.claudiumCurrentLiftN = 0f;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedLoopStepWithoutClaudiumN = ship.claudiumMaxLiftKg * 9.81f * 0.10f * fixedDeltaTime;
                report.Check(Approximately(ship.claudiumCurrentLiftN, expectedLoopStepWithoutClaudiumN, 0.05f)
                    && ship.claudiumStock <= 0.0001f,
                    "Lift loop works without claudium stock and does not consume claudium.");
            }

            ConfigureFlightProbeShip(ship, body);
            ResetFlightProbeBody(body, new Vector3(0f, 1000f, 0f), Quaternion.identity, true);
            float hoverStartY = body.position.y;
            if (StepShipPhysicsProbe(ship, physicsScene, 20, report))
            {
                float hoverDrift = Mathf.Abs(body.position.y - hoverStartY);
                report.Check(IsFinite(body.position) && IsFinite(body.linearVelocity), "РЎР±Р°Р»Р°РЅСЃРёСЂРѕРІР°РЅРЅС‹Р№ РїРѕР»С‘С‚ РЅРµ СЃРѕР·РґР°С‘С‚ NaN/Infinity РІ РїРѕР·РёС†РёРё Рё СЃРєРѕСЂРѕСЃС‚Рё.");
                report.Check(hoverDrift <= 0.25f && Mathf.Abs(body.linearVelocity.y) <= 0.5f, "РџСЂРё СЂР°Р±РѕС‡РµРј РєР»Р°РІРґРёРµРІРѕРј РєРѕРЅС‚СѓСЂРµ РєРѕСЂР°Р±Р»СЊ РґРµСЂР¶РёС‚ РІС‹СЃРѕС‚Сѓ: РґСЂРµР№С„ " + hoverDrift.ToString("0.###") + " Рј, vy " + body.linearVelocity.y.ToString("0.###") + " Рј/СЃ.");
            }

            ConfigureFlightProbeShip(ship, body);
            ship.claudiumStock = 0f;
            ResetFlightProbeBody(body, new Vector3(0f, 1000f, 0f), Quaternion.identity, true);
            float noClaudiumHoverStartY = body.position.y;
            if (StepShipPhysicsProbe(ship, physicsScene, 10, report))
            {
                float noClaudiumHoverDrift = Mathf.Abs(body.position.y - noClaudiumHoverStartY);
                report.Check(noClaudiumHoverDrift <= 0.25f && Mathf.Abs(body.linearVelocity.y) <= 0.5f,
                    "Ship lift does not require claudium stock: drift "
                    + noClaudiumHoverDrift.ToString("0.###")
                    + " m, vy "
                    + body.linearVelocity.y.ToString("0.###")
                    + " m/s.");
            }

            ConfigureFlightProbeShip(ship, body);
            ship.claudiumStock = 0f;
            ship.thrustInput = 1f;
            ship.hullThrustOutput = 1f;
            ResetFlightProbeBody(body, Vector3.zero, Quaternion.identity, false);
            if (StepShipPhysicsProbe(ship, physicsScene, 15, report))
            {
                Vector3 horizontalVelocity = body.linearVelocity;
                horizontalVelocity.y = 0f;
                report.Check(horizontalVelocity.z > 0.75f && ship.hullForwardThrustKgfCurrent > 0f, "Hull thrust accelerates the ship forward: v " + horizontalVelocity.magnitude.ToString("0.###") + " m/s, thrust " + ship.hullForwardThrustKgfCurrent.ToString("0.#") + " kgf.");
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
                report.Check(expectedMaxSpeed > 0f && IsFinite(expectedMaxSpeed), "Р Р°СЃС‡С‘С‚РЅР°СЏ РјР°РєСЃРёРјР°Р»СЊРЅР°СЏ СЃРєРѕСЂРѕСЃС‚СЊ РґР»СЏ С‚РµСЃС‚РѕРІРѕРіРѕ РєРѕСЂР°Р±Р»СЏ РєРѕРЅРµС‡РЅР°: " + expectedMaxSpeed.ToString("0.###") + " Рј/СЃ.");
                report.Check(Mathf.Abs(actualSpeed - expectedMaxSpeed) <= tolerance, "РЎРёРјСѓР»СЏС†РёСЏ РїРѕР»РЅРѕРіРѕ РіР°Р·Р° СЃС…РѕРґРёС‚СЃСЏ Рє СЂР°СЃС‡С‘С‚РЅРѕР№ СЃРєРѕСЂРѕСЃС‚Рё: СЂР°СЃС‡С‘С‚ " + expectedMaxSpeed.ToString("0.###") + " Рј/СЃ, С„Р°РєС‚ " + actualSpeed.ToString("0.###") + " Рј/СЃ, РґРѕРїСѓСЃРє " + tolerance.ToString("0.###") + " Рј/СЃ.");
            }

            ValidateShipPhysicsPushPreservesExternalImpulse(probeScene, physicsScene, report);
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
        string cameraSignatureBeforeDockScreen = cameraMovedBeforeDockScreen
            ? islandBeforeDockScreen.CityCameraSignatureForTests
            : "";
        bool dockScreenOpened = hud != null
            && hud.IsMetaDockScreenReadyForTests
            && hud.OpenMetaDockScreenForTests()
            && hud.IsMetaDockScreenVisibleForTests
            && hud.IsPortHudHiddenForDockScreenForTests
            && hud.MetaDockScreenPortButtonLabelForTests == "ПОРТ"
            && hud.IsMetaDockScreenPortButtonAtDockButtonSpotForTests;
        WildWindBaseIslandView islandAfterDockScreenOpen = dockScreenOpened
            ? WildWindBaseIslandView.EnsureForCurrentSessionScene()
            : null;
        report.Check(dockScreenOpened
                && islandAfterDockScreenOpen != null
                && !islandAfterDockScreenOpen.IsCityVisibleForTests,
            "Dock button switches to a separate Dock screen, hides the Port HUD, and shows a Port screen-exit button in the same control slot.");
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
                && islandAfterDockScreenReturn.CityCameraSignatureForTests == cameraSignatureBeforeDockScreen,
            "Returning from the Dock screen preserves the current city camera pivot, zoom and orbit instead of snapping away.");
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
        bool developmentWindowRendersFullTree = hud != null
            && hud.IsDevelopmentWindowCatalogReadyForTests
            && hud.DevelopmentWindowSupplierCountForTests == 6
            && hud.DevelopmentWindowTierColumnCountForTests == 10
            && hud.DevelopmentWindowTileCountForTests == 100
            && hud.DevelopmentWindowConnectionCountForTests == 99
            && developmentWindowText.Contains("465");
        report.Check(developmentWindowRendersFullTree,
            "Development window renders the full selected supplier ship tech tree with 10 tiers and 100 visible Capital tiles. Actual: suppliers="
            + (hud != null ? hud.DevelopmentWindowSupplierCountForTests : -1)
            + ", tiers="
            + (hud != null ? hud.DevelopmentWindowTierColumnCountForTests : -1)
            + ", tiles="
            + (hud != null ? hud.DevelopmentWindowTileCountForTests : -1)
            + ", connections="
            + (hud != null ? hud.DevelopmentWindowConnectionCountForTests : -1)
            + ", textHas465="
            + (!string.IsNullOrWhiteSpace(developmentWindowText) && developmentWindowText.Contains("465")));
        report.Check(hud != null
                && developmentWindowText.IndexOf("первый столичный", StringComparison.OrdinalIgnoreCase) >= 0,
            "Development window renders the selected ship lore below research and purchase costs.");
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
        report.Check(sessionSceneObjects != null && sessionSceneObjects.transform.childCount == 0,
            "Session Scene Objects remains only as an empty isolation anchor; old static port/resource visuals are not in the hierarchy.");
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
            && island.GetBuildingDefinitionMaxCountForTests("pve_dock") == 2,
            "City building catalog stores current placement limits: processing x2, archive x2, workshop x5, PVE docks x2.");
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
        int externalDockCountBefore = island.ExternalDockPlacedCountForTests;
        report.Check(dockCountBefore == 1,
            "Base island starts with exactly one built external PVE dock.");
        bool dockPlaced = island.PlaceCatalogBuildingForTests("pve_dock")
            && island.GetPlacedBuildingCountForTests("pve_dock") == dockCountBefore + 1
            && island.ExternalDockPlacedCountForTests == externalDockCountBefore + 1
            && island.BuildingCountForTests == cityBuildingsBeforeDock
            && island.GetBuildingDefinitionMaxCountForTests("pve_dock") == 2;
        report.Check(dockPlaced,
            "Dock catalog entry builds the second current PVE dock into the external dock-slot grid instead of occupying an inner city footprint.");
        bool dockLimitBlocked = !island.PlaceCatalogBuildingForTests("pve_dock")
            && island.GetPlacedBuildingCountForTests("pve_dock") == island.GetBuildingDefinitionMaxCountForTests("pve_dock")
            && island.BuildingCatalogStatusForTests.Contains("лимит");
        report.Check(dockLimitBlocked,
            "Dock catalog entry blocks a third PVE dock while only two docks are currently allowed.");
        bool dockMoved = island.MoveExternalDockToFirstFreeSlotForTests("pve_dock")
            && island.GetPlacedBuildingCountForTests("pve_dock") == dockCountBefore + 1;
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
            && !islandAfterDockClick.IsCityVisibleForTests;
        report.Check(dockScreenFromDock,
            "Clicking a built external dock opens the Dock screen directly instead of the building radial menu.");
        report.Check(dockScreenFromDock && baseHud.IsMetaDockGameplayReadyForTests,
            "Dock screen contains the concrete quick-mission ship slot, selected-ship panel, reward panel and battle/sell buttons.");
        if (baseHud != null && baseHud.IsMetaDockScreenVisibleForTests)
        {
            baseHud.ReturnFromMetaDockScreenToPortForTests();
            WildWindBaseIslandView.EnsureForCurrentSessionScene();
        }

        MetaGameState quickDockMeta = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        bool quickDockLoopWorks = false;
        bool quickDockRewardsVary = false;
        bool quickRawRewardsVary = false;
        bool dockResultWindowWorks = false;
        bool ordinaryDockMissionWorks = false;
        bool starterPortLoopWorks = false;
        bool emptyDockSlotOpensDevelopment = false;
        bool developmentPurchaseReturnsToDock = false;
        bool developmentTreeAllMouseButtonsPan = false;
        bool coreCombatDockLaunchWorks = false;
        int storageTotalBeforeQuickSortie = 0;
        int storageTotalAfterQuickSortie = 0;
        string quickDockRewardText = "";
        string quickDockResultWindowText = "";
        string secondQuickDockRewardText = "";
        string secondQuickDockResultWindowText = "";
        string ordinaryDockRewardText = "";
        string ordinaryDockResultWindowText = "";
        if (quickDockMeta != null && baseHud != null)
        {
            quickDockMeta.EnsureProgressInitialized();
            DockedDevelopmentShipState existingDockShip = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
            if (existingDockShip != null && existingDockShip.HasShip)
            {
                quickDockMeta.TrySellDevelopmentDockShip(existingDockShip.slotIndex, out _);
            }

            quickDockMeta.GetCapitalStorageState()?.AddResource("freight", 120000);
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
            bool boughtFromDevelopmentRoute = emptyDockSlotOpensDevelopment
                && baseHud.BuySelectedDevelopmentShipForTests();
            DockedDevelopmentShipState routeSlot = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
            developmentPurchaseReturnsToDock = boughtFromDevelopmentRoute
                && baseHud.IsMetaDockScreenVisibleForTests
                && !baseHud.IsDevelopmentWindowOpenForTests
                && routeSlot != null
                && routeSlot.HasShip;
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
            quickDockMeta.GetCapitalStorageState()?.AddResource("freight", 20000);
            bool boughtShip = developmentOpened && baseHud.BuySelectedDevelopmentShipForTests();
            PortStorageState quickStorage = quickDockMeta.GetCapitalStorageState();
            storageTotalBeforeQuickSortie = CountStorageResourceTotal(quickStorage);
            bool dockOpened = boughtShip
                && baseHud.OpenMetaDockScreenForTests()
                && baseHud.IsMetaDockGameplayReadyForTests;
            bool sortieRan = dockOpened && baseHud.RunQuickDockSortieForTests();
            quickDockRewardText = baseHud.MetaDockRewardTextForTests;
            quickDockResultWindowText = baseHud.MetaDockResultWindowTextForTests;
            bool firstResultWindowOpened = sortieRan
                && baseHud.IsMetaDockResultWindowVisibleForTests
                && !string.IsNullOrWhiteSpace(quickDockResultWindowText)
                && quickDockResultWindowText.Contains("FE:");
            int storageAfterFirstQuickSortie = CountStorageResourceTotal(quickStorage);
            bool secondSortieRan = sortieRan && baseHud.RunQuickDockSortieForTests();
            secondQuickDockRewardText = baseHud.MetaDockRewardTextForTests;
            secondQuickDockResultWindowText = baseHud.MetaDockResultWindowTextForTests;
            bool secondResultWindowOpened = secondSortieRan
                && baseHud.IsMetaDockResultWindowVisibleForTests
                && !string.IsNullOrWhiteSpace(secondQuickDockResultWindowText)
                && secondQuickDockResultWindowText.Contains("FE:")
                && !string.Equals(quickDockResultWindowText, secondQuickDockResultWindowText, StringComparison.Ordinal);
            storageTotalAfterQuickSortie = CountStorageResourceTotal(quickStorage);
            bool ordinaryMissionRan = secondSortieRan && baseHud.RunFirstOrdinaryDockMissionForTests();
            ordinaryDockRewardText = baseHud.MetaDockRewardTextForTests;
            ordinaryDockResultWindowText = baseHud.MetaDockResultWindowTextForTests;
            bool ordinaryResultWindowOpened = ordinaryMissionRan
                && baseHud.IsMetaDockResultWindowVisibleForTests
                && !string.IsNullOrWhiteSpace(ordinaryDockResultWindowText)
                && ordinaryDockResultWindowText.Contains("FE:");
            int storageTotalAfterOrdinaryMission = CountStorageResourceTotal(quickStorage);
            ordinaryDockMissionWorks = ordinaryMissionRan
                && storageTotalAfterOrdinaryMission > storageTotalAfterQuickSortie
                && !string.IsNullOrWhiteSpace(ordinaryDockRewardText)
                && ordinaryDockRewardText.Contains("FE:")
                && ordinaryDockRewardText.Contains("Нематериальное");
            bool concreteRewardText = !string.IsNullOrWhiteSpace(quickDockRewardText)
                && quickDockRewardText.Contains(" x")
                && !string.IsNullOrWhiteSpace(secondQuickDockRewardText)
                && secondQuickDockRewardText.Contains(" x");
            quickDockRewardsVary = secondSortieRan
                && storageAfterFirstQuickSortie > storageTotalBeforeQuickSortie
                && storageTotalAfterQuickSortie > storageAfterFirstQuickSortie
                && !string.Equals(quickDockRewardText, secondQuickDockRewardText, StringComparison.Ordinal);
            dockResultWindowWorks = firstResultWindowOpened
                && secondResultWindowOpened
                && ordinaryResultWindowOpened;
            bool soldShip = ordinaryMissionRan && baseHud.SellSelectedDockShipForTests();
            quickDockLoopWorks = developmentOpened
                && boughtShip
                && dockOpened
                && sortieRan
                && secondSortieRan
                && ordinaryDockMissionWorks
                && storageTotalAfterQuickSortie > storageTotalBeforeQuickSortie
                && concreteRewardText
                && quickDockRewardsVary
                && dockResultWindowWorks
                && soldShip;

            if (baseHud.IsMetaDockScreenVisibleForTests)
            {
                baseHud.ReturnFromMetaDockScreenToPortForTests();
                WildWindBaseIslandView.EnsureForCurrentSessionScene();
            }

            DockedDevelopmentShipState dockSlot = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
            if (dockSlot != null && dockSlot.HasShip)
            {
                quickDockMeta.TrySellDevelopmentDockShip(dockSlot.slotIndex, out _);
            }

            List<string> oreItemIds = GetOreItemIdsForBigTest(quickDockMeta.SessionConfig);
            int rawOreBefore = CountStorageItems(quickStorage, oreItemIds);
            bool boughtMiner = quickDockMeta.TryBuyDevelopmentShipToDock("stone_vault_starter", out _);
            string rawMessageA = "";
            string rawMessageB = "";
            string rawMessageC = "";
            bool rawSortieA = boughtMiner && quickDockMeta.TryRunQuickDevelopmentSortie(0, out rawMessageA);
            bool rawSortieB = rawSortieA && quickDockMeta.TryRunQuickDevelopmentSortie(0, out rawMessageB);
            bool rawSortieC = rawSortieB && quickDockMeta.TryRunQuickDevelopmentSortie(0, out rawMessageC);
            int rawOreAfter = CountStorageItems(quickStorage, oreItemIds);
            quickRawRewardsVary = rawSortieC
                && rawOreAfter > rawOreBefore
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
                bool starterShipBought = quickDockMeta.TryBuyDevelopmentShipToDock("stone_vault_starter", out _);
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
                bool paidShipAffordable = starterFreightAfter >= 9000;

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
                quickDockMeta.GetCapitalStorageState()?.AddResource("freight", 120000);
                bool coreShipBought = quickDockMeta.TryBuyDevelopmentShipToDock("capital_patrol_frigate_r02", out _);
                DockedDevelopmentShipState coreSlotBefore = quickDockMeta.GetSelectedDevelopmentDockShipSlot();
                int sortiesBeforeCoreCombat = coreSlotBefore != null ? coreSlotBefore.sortiesRemaining : -1;
                bool coreCombatStarted = coreShipBought && quickDockMeta.BeginCoreTacticalIntroCombatSortie();
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
            }
            finally
            {
                quickDockMeta.ReplaceProgress(coreCombatSnapshot);
            }
        }

        report.Check(quickDockLoopWorks,
            "Development tree can buy a ship into the dock, run two adaptive quick missions, run one ordinary generated mission, add concrete rewards to storage and sell the ship. Storage "
            + storageTotalBeforeQuickSortie
            + " -> "
            + storageTotalAfterQuickSortie
            + ", rewards="
            + quickDockRewardText + " / " + secondQuickDockRewardText + " / " + ordinaryDockRewardText + ".");
        report.Check(emptyDockSlotOpensDevelopment,
            "Clicking an empty dock ship slot acts as the Buy route and opens the Development window instead of only selecting an empty slot.");
        report.Check(developmentPurchaseReturnsToDock,
            "Buying from the Development window places the ship into the selected dock slot and returns to the Dock screen so the result is visible.");
        report.Check(developmentTreeAllMouseButtonsPan,
            "Development tree panning captures the pointer and scrolls with left, middle and right mouse buttons.");
        report.Check(quickRawRewardsVary,
            "A mining starter ship returns concrete ore from randomized quick sorties instead of the same fixed reward every run.");
        report.Check(dockResultWindowWorks,
            "Dock sortie result window opens for quick and ordinary missions and refreshes its concrete FE/reward report: "
            + quickDockResultWindowText + " / " + secondQuickDockResultWindowText + " / " + ordinaryDockResultWindowText + ".");
        report.Check(ordinaryDockMissionWorks,
            "Dock ordinary mission list can launch a selected generated mission and applies the material/intangible result: " + ordinaryDockRewardText + ".");
        report.Check(starterPortLoopWorks,
            "Starter port loop works end-to-end: quick sorties bring raw ore, refinery turns it into processed minerals, courier delivery grants Freight/mastery XP, and a paid R2 ship can be bought from the earned economy path.");
        report.Check(coreCombatDockLaunchWorks,
            "Dock battle launch starts a real Core Tactical sortie from the selected dock ship, enters Flight mode and consumes one dock sortie.");
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
            FactionMarketItemOffer stoneLicenseBefore = FindFactionMarketOfferForBigTest(stoneMarketBefore, "stone_vault_flagship_license");
            bool factionMarketStartsLocked = lockedGrateBefore != null
                && !lockedGrateBefore.unlocked
                && string.Equals(lockedGrateBefore.currencyItemId, "gems", StringComparison.OrdinalIgnoreCase)
                && stoneLicenseBefore != null
                && string.Equals(stoneLicenseBefore.currencyItemId, "solid", StringComparison.OrdinalIgnoreCase)
                && stoneLicenseBefore.priceAmount == 700;

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
                "Quest runtime accepts the starter task board, retroactively completes owned-resource tasks and can finish every seed quest after Cruiser203 cleanup: completed="
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
                "Faction runtime exposes six factions, five reputation thresholds, locked market offers, Solid-priced R10 licenses and building gates.");
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

        return sceneText.Contains("m_Name: Session Scene Objects")
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

            bool pioneerAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult pioneerAssembly);
            float pioneerStarterPayloadKg = loadedMeta.startingFuelKg + loadedMeta.startingClaudiumKg + 25f;
            string pioneerFlightEnvelope = "assembly did not build.";
            bool pioneerFlightEnvelopeOk = pioneerAssemblyBuilt
                && HasStablePioneerFlightEnvelope(
                    pioneerAssembly,
                    pioneerStarterPayloadKg,
                    0.5f,
                    12f,
                    out pioneerFlightEnvelope);
            report.Check(pioneerFlightEnvelopeOk,
                "Starter Pioneer recovery has enough lift, hull thrust, and speed for a starter ore sortie: "
                + pioneerFlightEnvelope);
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
            && !entryShip.headingHold
            && !entryShip.altitudeHold;
        report.Check(sortieEntryApproachReady,
            "Sortie entry starts 20 seconds outside the zone edge at the sustainable full-slipstream max speed, full claudium slipstream, and no autopilot handoff.");
        report.Check(Mathf.Sqrt(HorizontalSqrDistance(entryPosition, baseDockPosition)) >= SessionExtractionConstants.DefaultSortiePocketMinimumDockSeparationMeters,
            "Started sortie ship is placed in the isolated session pocket, not under or near the port.");

        locationIsolation = SortieLocationIsolationController.EnsureForLoadedGameplayScene();
        locationIsolation?.RefreshNow();
        bool portPocketHiddenDuringSortie = locationIsolation != null
            && locationIsolation.TargetCount > 0
            && locationIsolation.IsIsolationApplied
            && GameObject.Find(SortieLocationIsolationController.SessionSceneObjectsRootName) == null;
        report.Check(portPocketHiddenDuringSortie,
            "Active sortie hides the port/open-world scene pocket so the sortie is surrounded only by empty session space.");

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
            "Boundary extraction ignores empty coal and claudium tanks, but still waits for the slip exit runup: " + noRunupMessage);

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
            && Approximately(screenshotReserveEstimate.requiredCoalKg, 0f, 0.001f)
            && Approximately(screenshotReserveEstimate.requiredClaudiumKg, 0f, 0.001f)
            && screenshotReserveStartsTimer,
            "Safe ore extraction starts the slip timer at the boundary without coal or claudium reserves.");

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
        report.Check(!estimateInStorm.canExtract
            && estimateInStorm.isNearBoundary
            && !estimateInStorm.isAboveStorm
            && estimateInStorm.hasEnoughCoal
            && estimateInStorm.hasEnoughClaudium
            && !extractedFromStorm
            && loadedMeta.HasActiveSortie
            && loadedMeta.CurrentMode == GameSessionMode.Flight,
            "Boundary extraction is blocked inside the storm layer even though return costs are zero: " + stormBlockMessage);

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
            "Extraction is blocked until the ship leaves the sortie cylinder: " + boundaryBlockMessage);

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
            && Approximately(estimateReady.requiredCoalKg, 0f, 0.001f)
            && Approximately(estimateReady.requiredClaudiumKg, 0f, 0.001f),
            "Boundary extraction has zero coal and claudium return cost after the slip exit run.");
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
            && GameObject.Find(SortieLocationIsolationController.SessionSceneObjectsRootName) != null;
        report.Check(extracted && portPocketRestoredAfterExtraction,
            "Returning from a sortie restores the port scene pocket only after the Extract home transition.");
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
        int advancedProcessingCycles = loadedMeta.AdvanceRealTimeProcesses(new DateTime(processingStartTicks, DateTimeKind.Utc).AddSeconds(31));
        BaseProcessingOutputBufferState fatBufferBeforeCollect = butcheryFacility.GetOutputBuffer(SessionExtractionConstants.LeviathanFatItemId, false);
        bool leviathanBuffered = leviathanLoaded
            && advancedProcessingCycles >= 3
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

        bool fittedPioneerAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult fittedPioneerAssembly);
        float fittedPioneerPayloadKg = loadedMeta.startingFuelKg + loadedMeta.startingClaudiumKg + 250f;
        string fittedPioneerFlightEnvelope = "assembly did not build.";
        bool fittedPioneerFlightEnvelopeOk = fittedPioneerAssemblyBuilt
            && fittedPioneerAssembly.hull != null
            && fittedPioneerAssembly.hull.partId == GameplaySessionAccountData.DefaultStarterHullId
            && HasStablePioneerFlightEnvelope(
                fittedPioneerAssembly,
                fittedPioneerPayloadKg,
                0.5f,
                12f,
                out fittedPioneerFlightEnvelope);
        report.Check(fittedPioneerFlightEnvelopeOk,
            "Fully fitted Pioneer still has enough lift and hull thrust to fly instead of falling: "
            + fittedPioneerFlightEnvelope);

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
            "Sortie ship loss returns to base, deletes sortie loot, clears optional upgrades, restores Pioneer hull, and resets only internal recovery reserves.");

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
        bool pioneerRefuelReady = loadedMeta.CanRefuelBaseShip(out string pioneerRefuelReadyMessage);
        bool pioneerRefueled = loadedMeta.TryRefuelBaseShip(out string pioneerRefuelMessage);
        report.Check(!pioneerRefuelReady
            && !pioneerRefueled
            && baseStorage != null
            && baseStorage.GetResourceAmount("charcoal") == 0
            && baseStorage.GetResourceAmount("claudium") == 0
            && progress.shipFuelTank.GetAmount("charcoal") <= 0.001f
            && progress.shipClaudiumTank.GetAmount("claudium") <= 0.001f,
            "Base refuel is retired; missing coal and claudium never block or trigger a sortie preparation action: "
            + pioneerRefuelReadyMessage + " / " + pioneerRefuelMessage);
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
        int expectedCycles = Mathf.CeilToInt(amount / (float)Mathf.Max(1, facility.cycleInputUnits));
        double secondsToComplete = Math.Ceiling(Math.Max(1d, expectedCycles * facility.cycleDurationSeconds)) + 1d;
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
            + ", cycles " + expectedCycles
            + ", completed events " + completedEvents
            + ", ready " + readyTotal
            + " / " + loadMessage + " / " + collectMessage;

        return loaded
            && inputSpent
            && completedEvents >= expectedCycles
            && bunkerEmpty
            && readyTotal > 0
            && collected
            && outputsExact
            && buffersCleared;
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

    private static bool HasStablePioneerFlightEnvelope(
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
        float dragPerSpeedSquared = 0.5f
            * Mathf.Max(0f, stats.Get(ShipStatId.AirDensity, 1.225f))
            * Mathf.Max(0f, stats.Get(ShipStatId.DragCoefficient, 0f))
            * Mathf.Max(0f, stats.Get(ShipStatId.FrontalArea, 0f));
        float thrustN = hullForwardThrustKgf * 9.81f;
        float horizontalAccelerationMS2 = totalMassKg > 0f ? thrustN / totalMassKg : 0f;
        float terminalSpeedMS = 0f;
        if (dragPerSpeedSquared > 0f && thrustN > 0f)
        {
            terminalSpeedMS = Mathf.Sqrt(thrustN / dragPerSpeedSquared);
        }

        terminalSpeedMS = hullCruiseSpeedMS > 0f
            ? Mathf.Min(terminalSpeedMS, hullCruiseSpeedMS)
            : terminalSpeedMS;

        summary = "mass " + totalMassKg.ToString("F0") + "/" + allowedTakeoffMassKg.ToString("F0") + " kg"
            + ", empty " + emptyMassKg.ToString("F0") + " kg"
            + ", thrust " + hullForwardThrustKgf.ToString("F0") + " kgf"
            + ", accel " + horizontalAccelerationMS2.ToString("F2") + " m/s2"
            + ", cruise " + hullCruiseSpeedMS.ToString("F0") + " m/s"
            + ", terminal " + terminalSpeedMS.ToString("F0") + " m/s.";

        return totalMassKg <= allowedTakeoffMassKg + 0.001f
            && hullForwardThrustKgf > 0f
            && hullCruiseSpeedMS >= minSpeedMS
            && horizontalAccelerationMS2 >= minHorizontalAccelerationMS2
            && IsFinite(terminalSpeedMS)
            && terminalSpeedMS >= minSpeedMS;
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
#if UNITY_EDITOR
        string bootstrapText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalPrototypeBootstrap.cs");
        string sortieText = ReadProjectText("Assets/Scripts/Core/Tactical/CoreTacticalCombatSortieController.cs");
        string hudText = ReadProjectText("Assets/Scripts/UI/WildWindGameplayHud.cs");
        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");

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
            hudText.Contains("Dock Enter Core Combat") &&
            hudText.Contains("HandleMetaDockCoreCombatSortie, out metaDockQuickBattleButton") &&
            hudText.Contains("RunCoreCombatDockSortieForTests");

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
#else
        report.Check(true, "Core Tactical prototype transfer source scan is editor-only and skipped in player builds.");
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
                    string trimmed = lines[lineIndex].TrimStart();
                    if (!trimmed.StartsWith("[MenuItem(\"Wild Wind/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!allowedEditorToolFile)
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

    private void TryWriteReport(string text, WildWindBigTestResult result, BigTestReport report)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, string.IsNullOrWhiteSpace(reportFolder) ? "TestReports" : reportFolder);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "WildWindBigTestReport.txt");
            File.WriteAllText(path, text, Encoding.UTF8);
            Debug.Log(LogPrefix + "РўРµРєСЃС‚РѕРІС‹Р№ РїСЂРѕС‚РѕРєРѕР» СЃРѕС…СЂР°РЅС‘РЅ: " + path, this);

            if (result != null)
            {
                string jsonPath = Path.Combine(folder, "WildWindBigTestReport.json");
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
        ship.hullForwardThrustKgf = 1200f;
        ship.fuelConsumptionKgPerMinute = 1.2f;
        ship.fuelStockKg = 20f;
        ship.hullThrustOutput = 0f;
        ship.hullThrustResponseRate01PerSecond = 0f;
        ship.neutralStopBrakeEnabled = false;
        ship.neutralStopBrakeMaxDecelerationMS2 = 8f;
        ship.neutralStopBrakeStopTimeSeconds = 0.75f;
        ship.neutralStopBrakeDeadzoneMS = 0.05f;
        ship.neutralStopBrakeAccelerationMS2 = 0f;
        ship.claudiumStock = 20f;
        ship.claudiumMaxLiftKg = 1500f;
        ship.claudiumLoopResponseRate01PerSecond = 0f;
        ship.claudiumCurrentLiftN = 0f;
        ship.claudiumRequestedLiftKg = 0f;
        ship.miningImpactDamageTakenMultiplier = 1f;
        ship.airDensity = 1.225f;
        ship.dragCoefficient = 0.7f;
        ship.frontalArea = 6f;
        ship.sideResistance = 1f;
        ship.verticalAreaFactor = 4f;
        ship.windVelocity = Vector3.zero;
        ship.baseMaxSpeedMS = 0f;
        ship.loadSpeedMultiplier = 1f;
        ship.damageSpeedMultiplier = 1f;
        ship.nearMaxThrustFadeStartRatio = 0.72f;
        ship.nearMaxThrustFadeEndRatio = 1f;
        ship.slipstreamMaxSpeedMultiplier = 5f;
        ship.slipstreamActivationSpeedRatio = 0.8f;
        ship.slipstreamMaxRampSeconds = ShipPhysics.ClaudiumSlipstreamActivationSeconds;
        ship.slipstreamFuelConsumptionMultiplier = 2f;
        ship.claudiumSlipstreamEnabled = false;
        ship.claudiumSlipstreamCharge01 = 0f;
        ship.autoStabilizeAtStart = false;
        ship.altitudeHold = false;
        ship.cruiseControl = false;
        ship.headingHold = false;
        ship.targetSpeedMS = 0f;
        ship.targetHeading = 0f;
        ship.thrustInput = 0f;
        ship.sideInput = 0f;
        ship.turnInput = 0f;
        ship.liftInput = 0f;
        ship.lateralOmniThrustKgf = 260f;
        ship.hullCruiseReferenceSpeedMS = 30f;
        ship.baseMaxSpeedMS = 30f;
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
        ship.hullForwardThrustKgf = 1200f;
        ship.fuelConsumptionKgPerMinute = 1.2f;
        ship.fuelStockKg = 20f;
        ship.hullThrustOutput = 1f;
        ship.claudiumStock = 0f;
        ship.claudiumCurrentLiftN = 0f;
        ship.airDensity = 1.225f;
        ship.dragCoefficient = 1f;
        ship.frontalArea = 10f;
        ship.sideResistance = 0f;
        ship.hullCruiseReferenceSpeedMS = 12f;
        ship.baseMaxSpeedMS = 12f;
        ship.windVelocity = Vector3.zero;
        ship.RefreshRuntimeShipSettings();
        ship.StabilizeForFlightStart(false);
        ship.thrustInput = 1f;
        ship.hullThrustOutput = 1f;
        body.useGravity = false;
    }

    private static void ValidateArmorDamageModel(BigTestReport report)
    {
        GameObject target = null;
        try
        {
            target = new GameObject("Big Test Armor Damage Model");
            DamageableShip damageable = target.AddComponent<DamageableShip>();
            damageable.debugLogging = false;
            damageable.maxStructureHp = 1000f;
            damageable.ResetDamageState();

            ArmorSurface surface = new ArmorSurface
            {
                zoneId = "test_plate",
                displayNameRu = "Test plate",
                armorMm = 100f,
                ricochetAngleDeg = 89f,
                structureDamageMultiplier = 1f,
                highExplosiveSurfaceDamageMultiplier = 1f,
                ramDamageMultiplier = 1f
            };

            GameObject meshArmorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshArmorObject.name = "Big Test Mesh Armor Coverage";
            meshArmorObject.transform.SetParent(target.transform, false);
            MeshFilter meshFilter = meshArmorObject.GetComponent<MeshFilter>();
            MeshArmorBody meshArmor = meshArmorObject.AddComponent<MeshArmorBody>();
            meshArmor.owner = damageable;
            meshArmor.defaultArmorMm = 20f;
            meshArmor.EnsureMeshCollider();
            meshArmor.RebuildPlatesFromMesh();
            int meshTriangleCount = meshFilter != null && meshFilter.sharedMesh != null
                ? meshFilter.sharedMesh.triangles.Length / 3
                : 0;
            HashSet<int> coveredTriangles = new HashSet<int>();
            if (meshArmor.plates != null)
            {
                for (int plateIndex = 0; plateIndex < meshArmor.plates.Count; plateIndex++)
                {
                    MeshArmorPlate plate = meshArmor.plates[plateIndex];
                    if (plate == null || plate.triangleIndices == null) continue;

                    for (int triangleIndex = 0; triangleIndex < plate.triangleIndices.Count; triangleIndex++)
                    {
                        coveredTriangles.Add(plate.triangleIndices[triangleIndex]);
                    }
                }
            }

            bool meshArmorCoverageOk = meshTriangleCount > 0
                && coveredTriangles.Count == meshTriangleCount
                && meshArmor.plates != null
                && meshArmor.plates.Count > 0;
            report.Check(meshArmorCoverageOk,
                "MeshArmorBody rebuild assigns every non-degenerate mesh triangle to an armor plate: "
                + coveredTriangles.Count
                + "/"
                + meshTriangleCount
                + " triangles.");

            DamageHitContext partialHe = new DamageHitContext
            {
                shellType = DamageShellType.HighExplosive,
                shellName = "50 mm HE test",
                hullDamageOnPenetration = 200f,
                damagePoints = 200f,
                penetrationMm = 50f,
                hitNormal = Vector3.back,
                incomingDirection = Vector3.forward
            };
            DamageHitResult partialResult = damageable.ApplyHit(surface, partialHe);
            bool partialHeDamageOk = partialResult.outcome == DamageHitOutcome.ExplosiveSplash
                && Approximately(partialResult.structureDamage, 50f, 0.01f)
                && Approximately(damageable.structureHp, 950f, 0.01f)
                && damageable.explosiveSplashCount == 1
                && damageable.penetrationCount == 0;
            report.Check(partialHeDamageOk,
                "High explosive shells deal quadratic hull damage when HE penetration is below effective armor: "
                + partialResult.structureDamage.ToString("0.###")
                + " damage against 100 mm armor with 50 mm HE penetration.");

            DamageHitContext fullHe = partialHe;
            fullHe.shellName = "100 mm HE test";
            fullHe.penetrationMm = 100f;
            DamageHitResult fullResult = damageable.ApplyHit(surface, fullHe);
            bool fullHeDamageOk = fullResult.outcome == DamageHitOutcome.Penetration
                && Approximately(fullResult.structureDamage, 200f, 0.01f)
                && Approximately(damageable.structureHp, 750f, 0.01f)
                && damageable.penetrationCount == 1;
            report.Check(fullHeDamageOk,
                "High explosive shells deal full hull damage when HE penetration reaches effective armor.");

#if UNITY_EDITOR
            string projectileText = ReadProjectText("Assets/Scripts/Systems/DamageProjectile.cs");
            bool shellImpulseRemoved = !projectileText.Contains("ApplyHighExplosiveImpulse")
                && !projectileText.Contains("highExplosiveImpulseScale")
                && !projectileText.Contains("ForceMode.Impulse");
            report.Check(shellImpulseRemoved,
                shellImpulseRemoved
                    ? "Gun projectiles no longer apply physical push impulse; they only resolve armor damage."
                    : "DamageProjectile still contains explosive impulse code.");

            string shipLoaderText = ReadProjectText("Assets/Scripts/Meta/ShipLoader.cs");
            bool meshArmorLoaderFallback = shipLoaderText.Contains("AddComponent<MeshArmorBody>()")
                && shipLoaderText.Contains("RebuildPlatesFromMesh()")
                && shipLoaderText.Contains("DefaultFallbackMeshArmorMm")
                && shipLoaderText.Contains("AddComponent<ArmorZone>()");
            report.Check(meshArmorLoaderFallback,
                meshArmorLoaderFallback
                    ? "ShipLoader creates MeshArmorBody fallback for mesh hulls and keeps ArmorZone only as a no-mesh fallback."
                    : "ShipLoader armor fallback is not wired to MeshArmorBody.");

            string meshArmorBodyText = ReadProjectText("Assets/Scripts/Systems/MeshArmorBody.cs");
            string mediumFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Frigate_Medium_Blockout.fbx.meta");
            string korshunFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Imperial_PatrolFrigate_R02_Korshun.fbx.meta");
            string barbetFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Imperial_ArtilleryCruiser_R02_Barbet.fbx.meta");
            string valFbxMetaText = ReadProjectText("Assets/ShipImports/Models/BlenderShips/WW_Imperial_Battleship_Val.fbx.meta");
            bool blenderShipFbxImported = File.Exists(ProjectPath("Assets/ShipImports/Models/BlenderShips/WW_Frigate_Medium_Blockout.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/BlenderShips/WW_Imperial_PatrolFrigate_R02_Korshun.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/BlenderShips/WW_Imperial_ArtilleryCruiser_R02_Barbet.fbx"))
                && File.Exists(ProjectPath("Assets/ShipImports/Models/BlenderShips/WW_Imperial_Battleship_Val.fbx"));
            bool blenderShipFbxReadable = mediumFbxMetaText.Contains("isReadable: 1")
                && korshunFbxMetaText.Contains("isReadable: 1")
                && barbetFbxMetaText.Contains("isReadable: 1")
                && valFbxMetaText.Contains("isReadable: 1");
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
                    ? "Unity armor painting setup is removed; Blender ship FBX models remain readable and MeshArmorBody parses armor from Armor_XX material names."
                    : "Blender material armor import is not clean: old Unity armor painting setup remains, FBX sources are missing/unreadable, or material-name parsing is absent.");
#else
            report.Check(true, "DamageProjectile impulse source scan is editor-only and skipped in player builds.");
#endif
        }
        finally
        {
            DestroyBigTestObject(target);
        }
    }

    private static float CalculateExpectedForwardMaxSpeed(ShipPhysics ship)
    {
        if (ship == null) return 0f;

        return ship.CurrentMaxSpeedMS;
    }

    private static void ValidateShipPhysicsPushPreservesExternalImpulse(Scene probeScene, PhysicsScene physicsScene, BigTestReport report)
    {
        if (!probeScene.IsValid() || !physicsScene.IsValid())
        {
            report.Fail("Ship push physics probe did not receive a valid isolated scene.");
            return;
        }

        GameObject overspeedObject = null;
        GameObject strongObject = null;
        GameObject weakObject = null;
        try
        {
            overspeedObject = CreateShipPushProbeObject(
                "Big Test External Overspeed Probe",
                probeScene,
                Vector3.zero,
                Quaternion.identity,
                1000f,
                0f,
                5f);
            ShipPhysics overspeedShip = overspeedObject.GetComponent<ShipPhysics>();
            Rigidbody overspeedBody = overspeedObject.GetComponent<Rigidbody>();
            overspeedShip.thrustInput = 0f;
            overspeedShip.hullThrustOutput = 0f;
            overspeedBody.linearVelocity = new Vector3(0f, 0f, 20f);

            if (StepShipPhysicsProbes(physicsScene, 10, report, overspeedShip))
            {
                Vector3 overspeedVelocity = overspeedBody.linearVelocity;
                overspeedVelocity.y = 0f;
                report.Check(overspeedVelocity.magnitude > 19f && overspeedShip.CurrentMaxSpeedMS <= 5.1f,
                    "Ship max ход limits only own thrust: external overspeed "
                    + overspeedVelocity.magnitude.ToString("0.###")
                    + " m/s remains above max "
                    + overspeedShip.CurrentMaxSpeedMS.ToString("0.###")
                    + " m/s without Rigidbody velocity clamp.");
            }

            DestroyBigTestObject(overspeedObject);
            overspeedObject = null;

            strongObject = CreateShipPushProbeObject(
                "Big Test Strong Push Ship",
                probeScene,
                new Vector3(0f, 0f, -1.02f),
                Quaternion.identity,
                2000f,
                40f,
                18f);
            weakObject = CreateShipPushProbeObject(
                "Big Test Weak Push Ship",
                probeScene,
                new Vector3(0f, 0f, 1.02f),
                Quaternion.Euler(0f, 180f, 0f),
                1000f,
                10f,
                18f);

            ShipPhysics strongShip = strongObject.GetComponent<ShipPhysics>();
            ShipPhysics weakShip = weakObject.GetComponent<ShipPhysics>();
            Rigidbody strongBody = strongObject.GetComponent<Rigidbody>();
            Rigidbody weakBody = weakObject.GetComponent<Rigidbody>();
            float weakStartZ = weakBody.position.z;

            strongShip.thrustInput = 1f;
            strongShip.hullThrustOutput = 1f;
            weakShip.thrustInput = 1f;
            weakShip.hullThrustOutput = 1f;

            if (StepShipPhysicsProbes(physicsScene, 160, report, strongShip, weakShip))
            {
                bool weakPushedBack = weakBody.position.z > weakStartZ + 0.25f
                    && weakBody.linearVelocity.z > 0.25f;
                bool strongStillPushing = strongShip.hullForwardThrustKgfCurrent > weakShip.hullForwardThrustKgfCurrent;
                report.Check(weakPushedBack && strongStillPushing,
                    "More powerful Rigidbody ship can push a weaker ship in contact: weak z "
                    + weakStartZ.ToString("0.###")
                    + " -> "
                    + weakBody.position.z.ToString("0.###")
                    + ", weak vz "
                    + weakBody.linearVelocity.z.ToString("0.###")
                    + " m/s, thrust "
                    + strongShip.hullForwardThrustKgfCurrent.ToString("0.#")
                    + " vs "
                    + weakShip.hullForwardThrustKgfCurrent.ToString("0.#")
                    + " kgf.");
            }
        }
        finally
        {
            DestroyBigTestObject(overspeedObject);
            DestroyBigTestObject(strongObject);
            DestroyBigTestObject(weakObject);
        }
    }

    private static GameObject CreateShipPushProbeObject(
        string name,
        Scene scene,
        Vector3 position,
        Quaternion rotation,
        float massKg,
        float hullForwardThrustKgf,
        float maxSpeedMS)
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        probe.name = name;
        probe.transform.SetPositionAndRotation(position, rotation);
        probe.transform.localScale = new Vector3(2f, 1f, 2f);
        SceneManager.MoveGameObjectToScene(probe, scene);

        Rigidbody body = probe.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(1f, massKg);
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ShipPhysics ship = probe.AddComponent<ShipPhysics>();
        ship.enabled = false;
        ship.baseMass = Mathf.Max(1f, massKg);
        ship.cargoMassKg = 0f;
        ship.hullForwardThrustKgf = Mathf.Max(0f, hullForwardThrustKgf);
        ship.fuelConsumptionKgPerMinute = 1.2f;
        ship.fuelStockKg = 1000f;
        ship.hullThrustOutput = hullForwardThrustKgf > 0f ? 1f : 0f;
        ship.hullThrustResponseRate01PerSecond = 0f;
        ship.baseMaxSpeedMS = Mathf.Max(1f, maxSpeedMS);
        ship.hullCruiseReferenceSpeedMS = Mathf.Max(1f, maxSpeedMS);
        ship.airDensity = 0f;
        ship.dragCoefficient = 0f;
        ship.frontalArea = 0f;
        ship.sideResistance = 0f;
        ship.claudiumStock = 0f;
        ship.claudiumMaxLiftKg = 0f;
        ship.loadSpeedMultiplier = 1f;
        ship.damageSpeedMultiplier = 1f;
        ship.nearMaxThrustFadeStartRatio = 0.85f;
        ship.nearMaxThrustFadeEndRatio = 1f;
        ship.slipstreamMaxSpeedMultiplier = 5f;
        ship.slipstreamActivationSpeedRatio = 0.8f;
        ship.slipstreamFuelConsumptionMultiplier = 2f;
        ship.RefreshRuntimeShipSettings();
        ship.StabilizeForFlightStart(false);
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.mass = Mathf.Max(1f, massKg);
        return probe;
    }

    private static bool StepShipPhysicsProbes(PhysicsScene physicsScene, int steps, BigTestReport report, params ShipPhysics[] ships)
    {
        if (ships == null || ships.Length == 0)
        {
            report.Fail("Ship physics probe step did not receive ships.");
            return false;
        }

        if (!physicsScene.IsValid())
        {
            report.Fail("Ship physics probe step did not receive a valid PhysicsScene.");
            return false;
        }

        int stepCount = Mathf.Max(0, steps);
        float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.001f);
        for (int i = 0; i < stepCount; i++)
        {
            for (int shipIndex = 0; shipIndex < ships.Length; shipIndex++)
            {
                if (!TryInvokePrivateMethod(ships[shipIndex], "FixedUpdate", report))
                {
                    return false;
                }
            }

            try
            {
                physicsScene.Simulate(deltaTime);
            }
            catch (Exception exception)
            {
                report.Fail("Ship physics probe could not simulate a multi-ship physics step: " + exception.Message);
                return false;
            }
        }

        return true;
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
            report.Fail("РџСЂРѕР±Р° Р»С‘С‚РЅРѕР№ С„РёР·РёРєРё РЅРµ РїРѕР»СѓС‡РёР»Р° ShipPhysics.");
            return false;
        }

        if (!physicsScene.IsValid())
        {
            report.Fail("РџСЂРѕР±Р° Р»С‘С‚РЅРѕР№ С„РёР·РёРєРё РЅРµ РїРѕР»СѓС‡РёР»Р° РІР°Р»РёРґРЅСѓСЋ PhysicsScene.");
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
                report.Fail("РџСЂРѕР±Р° Р»С‘С‚РЅРѕР№ С„РёР·РёРєРё РЅРµ СЃРјРѕРіР»Р° РїСЂРѕСЃРёРјСѓР»РёСЂРѕРІР°С‚СЊ physics-С€Р°Рі: " + exception.Message);
                return false;
            }
        }

        return true;
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
