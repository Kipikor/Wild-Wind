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
#if UNITY_EDITOR
    private const string UsageAuditAfterBigTestArmedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Armed";
    private const string UsageAuditAfterBigTestRequestedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Requested";
#endif

    public const string BigTestEditorLaunchPlayerPrefsKey = "WildWind.BigTestEditorLaunch";
    public const string DefaultSessionSceneName = "WildWindSessionScene";

    public static bool SuppressRunOnStartForAutomation { get; set; }
    public static bool IsSessionLoopLaunchInProgress => sessionLoopLaunchInProgress;
    public static bool IsMainSessionCheckInProgress => activeRunInProgress && !sessionLoopLaunchInProgress;

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
        "Meta port runtime",
        "Direct port entry and account reset",
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

        yield return EnsureSessionSceneForBigTest(report);

        RunChecked(report, () =>
        {
            ResolveReferences();
            DescribeTestScope(report);
            ValidateSceneContext(report);

            SessionConfigDatabase config = LoadConfig(report);
            ValidateConfigDatabase(config, report);
            ValidateLocalizationConfig(report);
            ValidateCargoStorageModel(config, report);
            ValidatePioneerShipCatalog(config, report);
            ValidateStarterHullVisualAssetContract(report);
            ValidateCruiser203ShipContract(config, report);
            ValidateSessionLegacyCleanup(config, report);

            ValidateSessionOnlyRuntimeContract(report);
            ValidateRuntimeAccount(report);
            ValidateVisualAtmosphere(report);
            ValidateSettings(report);
            ValidateShipWindAerodynamics(report);
            ValidateMetaPortRuntime(report);
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
        report.Info("Currently covered: CSV config, tech tree, ship tree, production, session-only runtime, runtime account, visual dependencies, settings, wind/aerodynamics, flight physics, direct port entry and account reset.");
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
        report.Check(config.technologies.Count >= 1, "Technology.csv СЃРѕРґРµСЂР¶РёС‚ С‚РµС…РЅРѕР»РѕРіРёРё: " + config.technologies.Count + ".");
        report.Check(config.specialModules.Count == 5, "Special_module.csv contains only the five Pioneer starter fitting modules: " + config.specialModules.Count + ".");
        report.Check(config.hulls.Count == 2
            && config.GetHull("cruiser203_hull") != null
            && config.engines.Count == 1
            && config.propellers.Count == 1
            && config.claudiumLoops.Count == 1,
            "Ship part CSVs contain Pioneer plus Cruiser 203 hull, and one starter engine/propeller/claudium loop set.");
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
        report.Check(config.shipTreeEntries.Count == 2
            && config.GetShipTreeEntry("pioneer") != null
            && config.GetShipTreeEntry("cruiser203") != null,
            "Ship_tree.csv contains Pioneer and Cruiser 203: " + config.shipTreeEntries.Count + ".");
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
        CheckUniqueIds(config.specialModules, module => module.id, "СЃРїРµС†РјРѕРґСѓР»РµР№", report);
        CheckUniqueIds(config.shipTreeEntries, ship => ship.shipId, "РєРѕСЂР°Р±Р»РµР№ РІ Ship_tree.csv", report);
        ValidateShipTreeConfig(config, report);
        ValidateConfigReferences(config, report);
        ValidateSessionPortConfig(config, report);
    }

    private static void ValidateShipTreeConfig(SessionConfigDatabase config, BigTestReport report)
    {
        if (config == null || config.shipTreeEntries == null)
        {
            report.Fail("Ship_tree.csv РЅРµ Р·Р°РіСЂСѓР¶РµРЅ.");
            return;
        }

        bool entriesValid = config.shipTreeEntries.Count == 2;
        bool hasPioneerRoot = false;
        bool hasCruiser203 = false;

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
            hasCruiser203 |= entry.shipId == "cruiser203" &&
                entry.rank >= 1 &&
                entry.hullId == "cruiser203_hull" &&
                entry.parentShipIds != null &&
                entry.parentShipIds.Contains("pioneer");
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

        report.Check(entriesValid && hasPioneerRoot && hasCruiser203,
            "Ship_tree.csv keeps Pioneer as root and adds Cruiser 203 as a child ship.");

        report.Check(!ShipTreeHasCycles(config),
            "Р”РµСЂРµРІРѕ РєРѕСЂР°Р±Р»РµР№ РЅРµ СЃРѕРґРµСЂР¶РёС‚ С†РёРєР»РѕРІ РїРѕ parent_ship_id.");

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
                type.omniThrustKgf >= 0f;
        }

        report.Check(leviathansValid, "Р›РµРІРёР°С„Р°РЅС‹ Рё РёС… Р·РѕРЅС‹ РёРјРµСЋС‚ РІР°Р»РёРґРЅС‹Рµ С‚РёРїС‹, СЂР°Р·РјРµСЂС‹, Р·РґРѕСЂРѕРІСЊРµ Рё РІС‹СЃРѕС‚РЅС‹Рµ РґРёР°РїР°Р·РѕРЅС‹.");

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

        report.Check(techValid && techTreeValid, "Current technology config keeps the minimal Pioneer unlock tree valid and acyclic.");
        report.Check(techValid && hasCargoStorageModule, "Starter modules can define shared cargo capacity without legacy social-service or dock-slot modules.");
    }

    private static bool TechnologyTreeMetadataValid(SessionConfigDatabase config)
    {
        if (config == null || config.technologies == null || config.technologies.Count == 0) return false;

        return config.GetTechnology("basic_airship") != null &&
            TechnologyRanksRespectPrerequisites(config) &&
            !TechnologyTreeHasCycles(config);
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
        bool cargoMetadataValid =
            config.GetItem("passengers_to_capital") == null &&
            config.GetItem("passengers_to_island") == null &&
            config.GetItem("passengers_to_ship") == null &&
            water != null &&
            water.cargoUnitKind == CargoUnitKind.Piece &&
            water.cargoStorageKind == CargoStorageKind.Van &&
            sampleOre != null &&
            sampleOre.cargoUnitKind == CargoUnitKind.Piece &&
            sampleOre.cargoStorageKind == CargoStorageKind.Van &&
            carcass != null &&
            carcass.cargoUnitKind == CargoUnitKind.Piece &&
            carcass.cargoStorageKind == CargoStorageKind.Van;
        report.Check(cargoMetadataValid,
            "Cargo item metadata keeps session resources in the shared van hold and removes passenger-route cargo templates.");

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
            ["aer_silt"] = 5,
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
        tankProgress.shipEngineFuelTank.Add("charcoal", 30f, 50f);
        tankProgress.shipClaudiumTank.Add("claudium", 12.5f, 25f);
        bool internalTanksAreSeparate =
            tankProgress.GetShipCargoAmount("charcoal") == 10 &&
            Approximately(tankProgress.GetShipCargoMassKg(config), 10f, 0.001f) &&
            Approximately(tankProgress.GetShipPayloadMassKg(config), 52.5f, 0.001f) &&
            tankProgress.shipEngineFuelTank.TrySpend("charcoal", 5.5f) &&
            tankProgress.GetShipCargoAmount("charcoal") == 10 &&
            Approximately(tankProgress.shipEngineFuelTank.GetAmount("charcoal"), 24.5f, 0.001f);
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
    private static void ValidatePioneerShipCatalog(SessionConfigDatabase config, BigTestReport report)
    {
        report.Section("Pioneer ship catalog");

        if (config == null || !config.isLoaded)
        {
            report.Fail("Pioneer ship catalog checks stopped: config database is not loaded.");
            return;
        }

        bool sessionShipPartsReady = config.shipTreeEntries.Count == 2
            && config.GetShipTreeEntry("pioneer") != null
            && config.GetShipTreeEntry("cruiser203") != null
            && config.hulls.Count == 2
            && config.GetHull(GameplaySessionAccountData.DefaultStarterHullId) != null
            && config.GetHull("cruiser203_hull") != null
            && config.engines.Count == 1
            && config.GetEngine("starter_engine") != null
            && config.propellers.Count == 1
            && config.GetPropeller("starter_propeller") != null
            && config.claudiumLoops.Count == 1
            && config.GetClaudiumLoop("starter_claudium_loop") != null
            && config.specialModules.Count == 5
            && config.GetSpecialModule(SessionExtractionConstants.StarterCargoRackModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterGasExtractorModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterMiningHoldModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterObservationPostModuleId) != null
            && config.GetSpecialModule(SessionExtractionConstants.StarterLeviathanSalvageModuleId) != null;
        report.Check(sessionShipPartsReady,
            "Config ship catalog contains Pioneer, Cruiser 203 and the five starter fitting modules.");

        string shipAssemblyBuilderText = ReadProjectText("Assets/Scripts/Data/ShipAssemblyBuilder.cs");
        report.Check(!File.Exists(ProjectPath("Assets/Scripts/Data/R1ShipDesignCatalog.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Data/R2TenderDesignCatalog.cs")) &&
            !shipAssemblyBuilderText.Contains("R1ShipDesignCatalog") &&
            !shipAssemblyBuilderText.Contains("R2TenderDesignCatalog"),
            "Legacy R1/R2 design catalogs are removed while the session catalog keeps only Pioneer and Cruiser 203.");

        ShipCatalogSO catalog = ScriptableObject.CreateInstance<ShipCatalogSO>();
        catalog.starterHullId = GameplaySessionAccountData.DefaultStarterHullId;
        catalog.parts = new List<ShipPartDefinitionSO>();

        try
        {
            int expectedParts = config.hulls.Count
                + config.engines.Count
                + config.propellers.Count
                + config.claudiumLoops.Count
                + config.specialModules.Count;
            int appliedParts = ShipAssemblyBuilder.ApplyCsvShipPartConfigs(catalog, config);
            report.Check(appliedParts == expectedParts && expectedParts == 10,
                "Session CSV ship part configs sync into ShipCatalog: " + appliedParts + "/" + expectedParts + " parts.");

            PlayerProgress progress = new PlayerProgress();
            progress.Normalize();
            progress.ReplaceShipAssembly(GameplaySessionAccountData.DefaultStarterHullId);
            bool requiredModulesReady = ShipAssemblyBuilder.AutoInstallRequiredModules(catalog, progress, out string autoInstallMessage);
            ShipAssemblyResult result = null;
            bool assembled = requiredModulesReady
                && ShipAssemblyBuilder.TryBuild(catalog, progress, out result);
            report.Check(assembled
                && result != null
                && result.hull != null
                && result.hull.partId == GameplaySessionAccountData.DefaultStarterHullId
                && HasSlotType(result, SessionExtractionConstants.HighSlotTypeId)
                && HasSlotType(result, SessionExtractionConstants.MidSlotTypeId)
                && HasSlotType(result, SessionExtractionConstants.LowSlotTypeId)
                && HasSlotType(result, SessionExtractionConstants.RigSlotTypeId)
                && !HasSlotType(result, "utility"),
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
        const string sessionScenePath = "Assets/Scenes/WildWindSessionScene.unity";
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
        ValidateStarterHullSceneYaml(report, sessionScenePath, splitMeshReferences, forbiddenRuntimeVisualGuids, "Session scene YAML");
        bool starterGunfireDoesNotRequireCargo =
            StarterHullYamlHasFreeGunCost(starterHullPrefabPath) &&
            StarterHullYamlHasFreeGunCost(sessionScenePath);
        report.Check(starterGunfireDoesNotRequireCargo,
            "Starter main gun fire is not blocked by empty weapon cargo in the prefab or session scene.");
        bool starterBarrelBindingIsCurrent =
            StarterHullYamlHasCurrentBarrelBinding(starterHullPrefabPath) &&
            StarterHullYamlHasCurrentBarrelBinding(sessionScenePath);
        report.Check(starterBarrelBindingIsCurrent,
            "Starter barrel and Muzzle transforms match the current horizontal Blender barrel import.");
        ValidateStarterHullRuntimeTransformContract(report, starterHullPrefabPath, sessionScenePath);

        string sessionSceneText = ReadProjectText(sessionScenePath);
        if (!string.IsNullOrEmpty(sessionSceneText))
        {
            report.Check(!sessionSceneText.Contains("m_Name: Player Ship Proxy"), "Session scene has no legacy Player Ship Proxy object.");
            report.Check(!HasActiveLegacyShipPrimitive(sessionSceneText), "Session scene has no active legacy primitive ship children.");
        }
#else
        report.Check(true, "Starter hull visual asset contract validation is editor-only and is skipped in player builds.");
#endif
    }

    private static void ValidateCruiser203ShipContract(SessionConfigDatabase config, BigTestReport report)
    {
        report.Section("Cruiser 203 ship contract");

        HullConfig cruiserHull = config != null ? config.GetHull("cruiser203_hull") : null;
        ShipTreeEntryConfig cruiserShip = config != null ? config.GetShipTreeEntry("cruiser203") : null;
        bool configHasCruiser = cruiserHull != null &&
            cruiserShip != null &&
            cruiserShip.hullId == "cruiser203_hull" &&
            cruiserShip.parentShipIds != null &&
            cruiserShip.parentShipIds.Contains("pioneer") &&
            cruiserHull.baseMassKg >= 90000f &&
            cruiserHull.hullMaxTakeoffMassKg >= cruiserHull.baseMassKg &&
            cruiserHull.structureHp >= 15000f;
        report.Check(configHasCruiser,
            "Cruiser 203 is registered as a larger light cruiser hull in Hull.csv and Ship_tree.csv.");

#if UNITY_EDITOR
        const string exportJsonPath = "Assets/Data/ShipPrefabs/Cruiser203/Cruiser203BlenderExport.json";
        const string prefabPath = "Assets/Data/ShipPrefabs/Cruiser203Hull.prefab";
        const string partPath = "Assets/Data/ShipParts/Cruiser203Hull.asset";
        const string catalogPath = "Assets/Data/ShipCatalog.asset";
        const string prefabGuid = "1ce0d9e7633243ff906233022e4f0fba";
        const string partGuid = "8a2f74a6ae76468383d59d9e74e77de7";
        string[] meshAssetNames =
        {
            "Cruiser203Hull",
            "Cruiser203EmitterDeck",
            "Cruiser203TurretA_House",
            "Cruiser203TurretA_Ring",
            "Cruiser203TurretA_Barrel1",
            "Cruiser203TurretA_Barrel2",
            "Cruiser203TurretA_Barrel3",
            "Cruiser203TurretB_House",
            "Cruiser203TurretB_Ring",
            "Cruiser203TurretB_Barrel1",
            "Cruiser203TurretB_Barrel2",
            "Cruiser203TurretB_Barrel3",
            "Cruiser203TurretC_House",
            "Cruiser203TurretC_Ring",
            "Cruiser203TurretC_Barrel1",
            "Cruiser203TurretC_Barrel2",
            "Cruiser203TurretC_Barrel3",
            "Cruiser203CrusherHousing",
            "Cruiser203CrusherRotorR2",
            "Cruiser203CrusherRotorR1",
            "Cruiser203CrusherRotorC",
            "Cruiser203CrusherRotorL1",
            "Cruiser203CrusherRotorL2"
        };
        string[] meshGuids =
        {
            "9582a75b335446df96c5ac40990e23e9",
            "3d6208213def4c11a2062fbf6df63f23",
            "ae91b5908e934f798c4167b0b504bbc8",
            "44051a16cebf4195ab9fe189a7b6a78f",
            "4ab07c8c01dc47b08ef0d134ee9629c7",
            "ed55aba7b18347ee81b9e079c55168e6",
            "f59eb464f2fe4134aa3ac61dfdecdfb6",
            "0fb9bfbb23c44c55921efa1e8157284e",
            "80a7930189a2496dbe24aa0203df1bf0",
            "0f3b28a7026e4beeb40da557e04c93a7",
            "f1b4430da67f4c1797949d326501c28c",
            "19d25bc08d2f42b0be6201a0308463fc",
            "866fc061cb94430a95ad70e02b955d46",
            "ae80ba2901a745e196efe5283949f93c",
            "f7d9652737de41a8a4d99b13cf278f1e",
            "030262b6f14540339dd09ec589699281",
            "1b63606ac3a74503882b0663c8eeb9f8",
            "dd702c2200f4402697662c7696a2913e",
            "ef11468be620475da8230fd3072867d3",
            "199cc629e09742f182090abc02c33aa2",
            "976b00db66574e349bc887592e70b41d",
            "0e9a3a91527b4dd782d34c6d44aa41b3",
            "f0775694783348e8b9445f9c3796a44a"
        };

        string exportText = ReadProjectText(exportJsonPath);
        report.Check(!string.IsNullOrEmpty(exportText), "Cruiser 203 Blender export summary exists.");
        bool dimensionsOk = TryReadCruiserExportVector(exportText, "Cruiser203Hull", "dimensionsUnity", out Vector3 hullSize) &&
            hullSize.x >= 32f && hullSize.x <= 34f &&
            hullSize.y >= 29f && hullSize.y <= 31f &&
            hullSize.z >= 141f && hullSize.z <= 143.5f;
        report.Check(dimensionsOk,
            dimensionsOk
                ? "Cruiser 203 export keeps real Blender dimensions in meters: " + FormatVector(hullSize) + "."
                : "Cruiser 203 export dimensions are missing or not real-scale: " + FormatVector(hullSize) + ".");

        string prefabText = ReadProjectText(prefabPath);
        string partText = ReadProjectText(partPath);
        string catalogText = ReadProjectText(catalogPath);
        report.Check(!string.IsNullOrEmpty(prefabText), "Cruiser203Hull prefab YAML is readable.");
        report.Check(partText.Contains("partId: cruiser203_hull") &&
            partText.Contains("prefab: {fileID: 2030000000000000001, guid: " + prefabGuid + ", type: 3}"),
            "Cruiser203Hull part asset points at the cruiser prefab.");
        report.Check(catalogText.Contains("guid: " + partGuid),
            "ShipCatalog.asset includes Cruiser203Hull as a selectable hull part.");

        bool allObjFilesExist = true;
        bool allMetaGuidsMatch = true;
        bool prefabReferencesAllMeshes = !string.IsNullOrEmpty(prefabText);
        const ImportAssetOptions importOptions = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
        AssetDatabase.Refresh(importOptions);
        for (int i = 0; i < meshAssetNames.Length; i++)
        {
            string objPath = "Assets/Data/ShipPrefabs/Cruiser203/" + meshAssetNames[i] + ".obj";
            string fullObjPath = Path.Combine(Directory.GetCurrentDirectory(), objPath);
            allObjFilesExist &= File.Exists(fullObjPath);
            allMetaGuidsMatch &= ReadProjectMetaGuid(objPath + ".meta") == meshGuids[i];
            prefabReferencesAllMeshes &= prefabText.Contains("m_Mesh: {fileID: 4300000, guid: " + meshGuids[i] + ", type: 3}");
            AssetDatabase.ImportAsset(objPath, importOptions);
        }

        AssetDatabase.ImportAsset(prefabPath, importOptions);
        AssetDatabase.ImportAsset(partPath, importOptions);
        report.Check(allObjFilesExist && meshAssetNames.Length == 23,
            "Cruiser 203 exports all 23 OBJ mesh parts from Blender.");
        report.Check(allMetaGuidsMatch,
            "Cruiser 203 OBJ metas keep stable GUIDs for prefab references.");
        report.Check(prefabReferencesAllMeshes,
            "Cruiser203Hull prefab references every exported mesh part.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        report.Check(prefab != null, "Cruiser203Hull prefab is loadable.");
        if (prefab != null)
        {
            ShipPhysics physics = prefab.GetComponent<ShipPhysics>();
            MonoBehaviour crusher = FindComponentByTypeName(prefab, "ShipCrusherRotor");
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            bool hasEnabledRenderer = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                {
                    hasEnabledRenderer = true;
                    break;
                }
            }

            report.Check(hasEnabledRenderer, "Cruiser203Hull prefab has enabled renderers.");
            report.Check(ContainsChildNamed(prefab.transform, "GunYaw_A") &&
                ContainsChildNamed(prefab.transform, "GunYaw_B") &&
                ContainsChildNamed(prefab.transform, "GunYaw_C") &&
                ContainsChildNamed(prefab.transform, "GunPitch_A") &&
                ContainsChildNamed(prefab.transform, "GunPitch_B") &&
                ContainsChildNamed(prefab.transform, "GunPitch_C") &&
                ContainsChildNamed(prefab.transform, "GunMuzzle_A") &&
                ContainsChildNamed(prefab.transform, "GunMuzzle_B") &&
                ContainsChildNamed(prefab.transform, "GunMuzzle_C"),
                "Cruiser203Hull prefab exposes three turret yaw/pitch/muzzle hierarchies.");

            bool gunsConfigured = physics != null && physics.shipGunGroups != null && physics.shipGunGroups.Count == 3;
            bool gunsAreTriple203 = gunsConfigured;
            bool gunBindingsReady = gunsConfigured;
            bool yawLimitsReady = gunsConfigured;
            bool ballisticsReady = gunsConfigured;
            if (gunsConfigured)
            {
                for (int i = 0; i < physics.shipGunGroups.Count; i++)
                {
                    ShipGunGroup group = physics.shipGunGroups[i];
                    gunsAreTriple203 &= group != null &&
                        group.enabled &&
                        group.fireMode == ShipGunFireMode.Manual &&
                        group.barrelsPerSalvo == 3 &&
                        group.shell != null &&
                        Mathf.Abs(group.shell.caliberMm - 203f) <= 0.01f;
                    gunBindingsReady &= group != null &&
                        group.muzzle != null &&
                        group.yawPivot != null &&
                        group.pitchPivot != null;
                    yawLimitsReady &= group != null &&
                        group.useYawLimits &&
                        group.minYawDegrees > -181f &&
                        group.maxYawDegrees < 181f &&
                        group.maxYawDegrees > group.minYawDegrees;
                    ballisticsReady &= group != null &&
                        group.maxRangeMeters >= 22000f &&
                        group.muzzleVelocityMS >= 800f &&
                        group.projectileMassKg >= 120f &&
                        group.shell.velocityRetentionAtMaxRange < 0.75f;
                }
            }

            report.Check(gunsConfigured, "Cruiser203Hull prefab has exactly three configured gun groups.");
            report.Check(gunsAreTriple203, "Cruiser203Hull gun groups are manual triple 203 mm turrets.");
            report.Check(gunBindingsReady, "Cruiser203Hull gun groups bind muzzle, yaw and pitch pivots.");
            report.Check(yawLimitsReady, "Cruiser203Hull turrets have finite yaw sectors.");
            report.Check(ballisticsReady, "Cruiser203Hull turrets use heavy 203 mm ballistics with drag retention.");

            Transform[] rotors = null;
            bool crusherEnabled = false;
            if (crusher != null)
            {
                System.Reflection.PropertyInfo enabledProperty = crusher.GetType().GetProperty(
                    "CrusherEnabled",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                crusherEnabled = enabledProperty != null &&
                    enabledProperty.PropertyType == typeof(bool) &&
                    (bool)enabledProperty.GetValue(crusher, null);

                System.Reflection.FieldInfo rotorField = crusher.GetType().GetField(
                    "rotors",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                rotors = rotorField != null ? rotorField.GetValue(crusher) as Transform[] : null;
            }

            report.Check(crusher != null && crusherEnabled,
                "Cruiser203Hull prefab has an enabled aft crusher rotor component.");
            report.Check(rotors != null && rotors.Length == 5 &&
                ContainsChildNamed(prefab.transform, "Cruiser203CrusherRotorR2") &&
                ContainsChildNamed(prefab.transform, "Cruiser203CrusherRotorR1") &&
                ContainsChildNamed(prefab.transform, "Cruiser203CrusherRotorC") &&
                ContainsChildNamed(prefab.transform, "Cruiser203CrusherRotorL1") &&
                ContainsChildNamed(prefab.transform, "Cruiser203CrusherRotorL2"),
                "Cruiser203Hull crusher binds all five millstone rotors.");
        }

        string shipPhysicsText = ReadProjectText("Assets/Scripts/Systems/ShipPhysics.cs");
        string crusherText = ReadProjectText("Assets/Scripts/Systems/ShipCrusherRotor.cs");
        bool runtimeSupportsCruiser = shipPhysicsText.Contains("useYawLimits") &&
            shipPhysicsText.Contains("ClampGunYawDegrees") &&
            shipPhysicsText.Contains("withinYawLimits") &&
            shipPhysicsText.Contains("Main battery salvo") &&
            crusherText.Contains("class ShipCrusherRotor") &&
            crusherText.Contains("rotors");
        report.Check(runtimeSupportsCruiser,
            "Runtime supports cruiser turret yaw sectors, multi-turret salvos and crusher rotor animation.");
#else
        report.Check(true, "Cruiser 203 ship contract validation is editor-only and is skipped in player builds.");
#endif
    }

#if UNITY_EDITOR
    private static MonoBehaviour FindComponentByTypeName(GameObject root, string typeName)
    {
        if (root == null || string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == typeName)
            {
                return behaviour;
            }
        }

        return null;
    }

    private static string ReadProjectMetaGuid(string metaAssetPath)
    {
        string text = ReadProjectText(metaAssetPath);
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        const string marker = "guid:";
        int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return "";
        }

        int valueStart = markerIndex + marker.Length;
        int valueEnd = text.IndexOf('\n', valueStart);
        if (valueEnd < 0)
        {
            valueEnd = text.Length;
        }

        return text.Substring(valueStart, valueEnd - valueStart).Trim();
    }

    private static bool TryReadCruiserExportVector(string json, string assetName, string propertyName, out Vector3 value)
    {
        value = Vector3.zero;
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(assetName) || string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        string assetMarker = "\"assetName\": \"" + assetName + "\"";
        int assetIndex = json.IndexOf(assetMarker, StringComparison.Ordinal);
        if (assetIndex < 0)
        {
            return false;
        }

        string propertyMarker = "\"" + propertyName + "\":";
        int propertyIndex = json.IndexOf(propertyMarker, assetIndex, StringComparison.Ordinal);
        if (propertyIndex < 0)
        {
            return false;
        }

        int openBracket = json.IndexOf('[', propertyIndex);
        int closeBracket = openBracket >= 0 ? json.IndexOf(']', openBracket) : -1;
        if (openBracket < 0 || closeBracket < 0)
        {
            return false;
        }

        string[] parts = json.Substring(openBracket + 1, closeBracket - openBracket - 1)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        if (!float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) ||
            !float.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }

        value = new Vector3(x, y, z);
        return IsFinite(value);
    }

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

    private static void ValidateStarterHullRuntimeTransformContract(BigTestReport report, string starterHullPrefabPath, string sessionScenePath)
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

        string sessionSceneText = ReadProjectText(sessionScenePath);
        if (!string.IsNullOrEmpty(sessionSceneText))
        {
            ValidateStarterHullTransformIsNotHalfTurnY(report, sessionSceneText, "881536966",
                "Session scene starter hull visual child is not double-rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, sessionSceneText, "901536000102",
                "Session scene turret yaw pivot is not double-rotated 180 degrees.");
            ValidateStarterHullTransformIsNotHalfTurnY(report, sessionSceneText, "901536000202",
                "Session scene barrel pitch pivot is not double-rotated 180 degrees.");
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
        string fullPath = ProjectPath(assetPath);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : "";
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
        string mechanicsText = ReadProjectText("Assets/Scripts/Meta/WildWindMetaPortRuntimeModel.cs");
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
            !mechanicsText.Contains("worldHeightMeters") &&
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
        report.Check(generatedUsable, "Fresh runtime account contains progress, selected hull, base dock pose, and gameplay session data without save-file state.");

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
            "MetaGameState creates a runtime account snapshot for diagnostics without writing a save file.");

#if UNITY_EDITOR
        string metaText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string sessionText = ReadProjectText("Assets/Scripts/Session/WildWindGameplaySession.cs");
        string bootstrapText = ReadProjectText("Assets/Scripts/Session/WildWindGameplayBootstrap.cs");
        string flowText = ReadProjectText("Assets/Scripts/Session/WildWindSessionFlow.cs");
        bool accountStorageRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Session/WildWindAccountStorage.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Session/WildWindAccountStorage.cs.meta")) &&
            !metaText.Contains("File.WriteAllText") &&
            !metaText.Contains("File.ReadAllText") &&
            !metaText.Contains("Directory.CreateDirectory") &&
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
        report.Check(accountStorageRemoved,
            "Runtime account flow has no save-file storage, startup load, account filename, or persisted reload API.");
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

        report.Check(useAltitudeAtmosphere, "Р’С‹СЃРѕС‚РЅРѕРµ СѓРїСЂР°РІР»РµРЅРёРµ AERO-С‚СѓРјР°РЅРѕРј РІРєР»СЋС‡РµРЅРѕ.");
        report.Check(Approximately(transitionHalfWidth, 50f, 0.5f), "РџР»Р°РІРЅС‹Р№ РїРµСЂРµС…РѕРґ РІС‹СЃРѕС‚РЅС‹С… Р·РѕРЅ РґРµСЂР¶РёС‚СЃСЏ РѕРєРѕР»Рѕ +-50 Рј: " + transitionHalfWidth.ToString("0.#") + " Рј.");
        report.Check(violentVisibility > 0f && violentVisibility <= 150f, "Р’РёРґРёРјРѕСЃС‚СЊ СЏСЂРѕСЃС‚РЅРѕР№ Р±СѓСЂРё РѕРіСЂР°РЅРёС‡РµРЅР° РїСЂРёРјРµСЂРЅРѕ 100 Рј: " + violentVisibility.ToString("0.#") + " Рј.");
        report.Check(calmVisibility >= 800f && calmVisibility <= 1300f, "Р’РёРґРёРјРѕСЃС‚СЊ СЃРїРѕРєРѕР№РЅРѕР№ Р±СѓСЂРё РѕРєРѕР»Рѕ 1000 Рј: " + calmVisibility.ToString("0.#") + " Рј.");
        report.Check(deadlyDrawDistance > 0f && deadlyDrawDistance <= 120f, "РџРѕРІРµСЂС…РЅРѕСЃС‚СЊ СЃРјРµСЂС‚РµР»СЊРЅРѕР№ Р±СѓСЂРё СЂРёСЃСѓРµС‚СЃСЏ С‚РѕР»СЊРєРѕ РІР±Р»РёР·Рё: " + deadlyDrawDistance.ToString("0.#") + " Рј.");

        GameObject stormSurface = GameObject.Find("Deadly Storm Surface Local Bubble");
        report.Check(stormSurface != null, stormSurface != null ? "Р›РѕРєР°Р»СЊРЅР°СЏ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ СЃРјРµСЂС‚РµР»СЊРЅРѕР№ Р±СѓСЂРё РЅР°Р№РґРµРЅР°." : "Р›РѕРєР°Р»СЊРЅР°СЏ РїРѕРІРµСЂС…РЅРѕСЃС‚СЊ СЃРјРµСЂС‚РµР»СЊРЅРѕР№ Р±СѓСЂРё РЅРµ РЅР°Р№РґРµРЅР°.");
        if (stormSurface != null)
        {
            Renderer renderer = stormSurface.GetComponent<Renderer>();
            report.Check(renderer != null && renderer.sharedMaterial != null, "РЈ РїРѕРІРµСЂС…РЅРѕСЃС‚Рё Р±СѓСЂРё РЅР°Р·РЅР°С‡РµРЅ РјР°С‚РµСЂРёР°Р».");
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
            sessionCameraController != null ? "Flight camera controller is present for Warships-style aiming." : "Flight camera controller is missing.");
        if (sessionCameraController != null)
        {
            bool mouseLookEnabled = ReadPrivateBool(sessionCameraController, "warshipsFlightMouseLook", false);
            bool altReleasesCursor = ReadPrivateBool(sessionCameraController, "altReleasesCursor", false);
            float aimLookAheadMeters = ReadPrivateFloat(sessionCameraController, "flightAimLookAheadMeters", -1f);
            report.Check(mouseLookEnabled && altReleasesCursor && aimLookAheadMeters >= 50f,
                "Flight camera keeps Warships-style locked mouse look, Alt cursor release, and a forward aim point: "
                + "mouseLook=" + mouseLookEnabled
                + ", altRelease=" + altReleasesCursor
                + ", lookAhead=" + aimLookAheadMeters.ToString("0.#") + " m.");
        }

#if UNITY_EDITOR
        string cameraSource = ReadProjectText("Assets/Scripts/Session/WildWindSessionCameraController.cs");
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
            "collider == null) return false");
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
        report.Section("РљРѕСЂР°Р±Р»СЊ, РІРµС‚РµСЂ Рё Р»С‘С‚РЅР°СЏ С„РёР·РёРєР°");
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
            report.Check(Approximately(ship.CurrentWindAerodynamicFactor, 0.5f, 0.001f), "РђСЌСЂРѕРґРёРЅР°РјРёРєР° 0.5 РґР°С‘С‚ РєРѕСЌС„С„РёС†РёРµРЅС‚ РІРµС‚СЂР° 0.5.");
            report.Check(Approximately(ship.EffectiveWindVelocity.magnitude, 5f, 0.001f), "Р’РµС‚РµСЂ 10 Рј/СЃ РїСЂРё Р°СЌСЂРѕРґРёРЅР°РјРёРєРµ 0.5 РѕС‰СѓС‰Р°РµС‚СЃСЏ РєР°Рє 5 Рј/СЃ.");

            ship.dragCoefficient = 1.2f;
            ship.windVelocity = new Vector3(0f, 0f, 10f);
            report.Check(Approximately(ship.EffectiveWindVelocity.magnitude, 12f, 0.001f), "РџР»РѕС…Р°СЏ Р°СЌСЂРѕРґРёРЅР°РјРёРєР° 1.2 СѓСЃРёР»РёРІР°РµС‚ РІРѕР·РґРµР№СЃС‚РІРёРµ РІРµС‚СЂР° РґРѕ 12 Рј/СЃ.");

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

            float fuelBeforeLift = ship.engineFuelStockKg;
            float claudiumBeforeLift = ship.claudiumStock;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                float expectedLiftN = body.mass * 9.81f;
                report.Check(Approximately(ship.claudiumRequestedLiftKg, body.mass, 0.5f), "РљР»Р°РІРґРёРµРІС‹Р№ РєРѕРЅС‚СѓСЂ Р·Р°РїСЂР°С€РёРІР°РµС‚ С‚СЂРёРјРјРёСЂСѓРµРјСѓСЋ РјР°СЃСЃСѓ РєРѕСЂР°Р±Р»СЏ: " + ship.claudiumRequestedLiftKg.ToString("0.#") + " РєРі.");
                report.Check(Approximately(ship.claudiumCurrentLiftN, expectedLiftN, expectedLiftN * 0.02f), "РљР»Р°РІРґРёРµРІС‹Р№ РєРѕРЅС‚СѓСЂ РІС‹РґР°С‘С‚ РїРѕРґСЉС‘РјРЅСѓСЋ СЃРёР»Сѓ РїСЂРёРјРµСЂРЅРѕ РІРµСЃР° РєРѕСЂР°Р±Р»СЏ: " + ship.claudiumCurrentLiftN.ToString("0.#") + " Рќ.");
                report.Check(ship.engineGeneratedPowerKw + 0.001f >= ship.claudiumPowerDrawKw, "Р”РІРёРіР°С‚РµР»СЊ РїРѕРєСЂС‹РІР°РµС‚ РјРѕС‰РЅРѕСЃС‚СЊ РєР»Р°РІРґРёРµРІРѕРіРѕ РєРѕРЅС‚СѓСЂР°: " + ship.engineGeneratedPowerKw.ToString("0.#") + " / " + ship.claudiumPowerDrawKw.ToString("0.#") + " РєР’С‚.");
                report.Check(ship.engineFuelStockKg < fuelBeforeLift && ship.claudiumStock < claudiumBeforeLift, "РџРѕРґСЉС‘Рј С‚СЂР°С‚РёС‚ С‚РѕРїР»РёРІРѕ Рё РєР»Р°РІРґРёР№ РІ РѕРґРЅРѕРј С„РёР·РёС‡РµСЃРєРѕРј С‚РёРєРµ.");
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
                    "Р”РІРёРіР°С‚РµР»СЊ РґРѕРіРѕРЅСЏРµС‚ СЂСѓС‡РєСѓ С‚СЏРіРё СЃ РїСЂРёС‘РјРёСЃС‚РѕСЃС‚СЊСЋ 10% РјР°РєСЃРёРјСѓРјР° РІ СЃРµРєСѓРЅРґСѓ: pitch "
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
                    "РљР»Р°РІРґРёРµРІС‹Р№ РєРѕРЅС‚СѓСЂ РјРµРЅСЏРµС‚ С„Р°РєС‚РёС‡РµСЃРєРёР№ РїРѕРґСЉС‘Рј СЃ РїСЂРёС‘РјРёСЃС‚РѕСЃС‚СЊСЋ 10% РјР°РєСЃРёРјСѓРјР° РІ СЃРµРєСѓРЅРґСѓ: "
                    + ship.claudiumCurrentLiftN.ToString("0.###") + " Рќ.");
            }

            ship.claudiumStock = 0f;
            if (TryInvokePrivateMethod(ship, "UpdateSimplifiedClaudium", report))
            {
                report.Check(Approximately(ship.claudiumCurrentLiftN, 0f, 0.001f), "Р‘РµР· РєР»Р°РІРґРёСЏ РїРѕРґСЉС‘РјРЅР°СЏ СЃРёР»Р° РїР°РґР°РµС‚ РІ РЅРѕР»СЊ.");
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
            if (StepShipPhysicsProbe(ship, physicsScene, 10, report))
            {
                report.Check(body.linearVelocity.y < -0.75f, "Р‘РµР· РєР»Р°РІРґРёСЏ РєРѕСЂР°Р±Р»СЊ СЂРµР°Р»СЊРЅРѕ РЅР°С‡РёРЅР°РµС‚ РїР°РґР°С‚СЊ: vy " + body.linearVelocity.y.ToString("0.###") + " Рј/СЃ.");
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
                report.Check(horizontalVelocity.z > 0.75f && ship.propellerThrustKgf > 0f, "Р’РёРЅС‚ СЃ РґРѕСЃС‚СѓРїРЅРѕР№ РјРѕС‰РЅРѕСЃС‚СЊСЋ СЂР°Р·РіРѕРЅСЏРµС‚ РєРѕСЂР°Р±Р»СЊ РІРїРµСЂС‘Рґ: v " + horizontalVelocity.magnitude.ToString("0.###") + " Рј/СЃ, С‚СЏРіР° " + ship.propellerThrustKgf.ToString("0.#") + " РєРіСЃ.");
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

    private void ValidateMetaPortRuntime(BigTestReport report)
    {
        report.Section("Meta port runtime");

        WildWindMetaCatalog metaCatalog = WildWindMetaMechanics.CreateMinimalCatalog();
        MetaAccountState account = WildWindMetaMechanics.CreateFreshAccount(metaCatalog);
        report.Check(metaCatalog != null && account != null, "Meta mechanics catalog and account state are available.");
        report.Check(account.ships.Count == 1 && account.ships[0].shipId == "pioneer" && account.portSlots == WildWindMetaMechanics.InitialPortSlots,
            "Fresh account starts with five port slots and Starter Pioneer recovery.");
        bool sessionShipCatalogOnly = metaCatalog.ships.Count == 2
            && metaCatalog.ships.ContainsKey("pioneer")
            && metaCatalog.ships.ContainsKey("cruiser203_hull")
            && !metaCatalog.ships.ContainsKey("hauler_t1")
            && !metaCatalog.ships.ContainsKey("pioneer_mining_t2")
            && !metaCatalog.ships.ContainsKey("precursor_t3");
        report.Check(sessionShipCatalogOnly,
            "Minimal meta ship catalog keeps only session hulls: Pioneer and Cruiser 203.");
        MetaShipDefinition cruiserDefinition = metaCatalog.ships["cruiser203_hull"];
        account.ships.Add(new MetaShipInstance
        {
            shipId = cruiserDefinition.shipId,
            tier = cruiserDefinition.tier,
            remainingSorties = cruiserDefinition.maxSorties,
            maxSorties = cruiserDefinition.maxSorties
        });
        report.Check(account.ships.Exists(s => s.shipId == "cruiser203_hull"),
            "Single-account port state can hold Cruiser 203 without old ship-tree purchase flows.");
        account.ships.Clear();
        bool restoredPioneer = WildWindMetaMechanics.EnsureStarterPioneerRecovery(account, metaCatalog);
        report.Check(restoredPioneer && account.ships.Count == 1 && account.ships[0].shipId == "pioneer",
            "Starter Pioneer recovery is still a tiny account invariant, not a sell/loss economy loop.");
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
        string metaPortRuntimeText = ReadProjectText("Assets/Scripts/Meta/WildWindMetaPortRuntimeModel.cs");
        string metaPortModelText = ReadProjectText("Assets/Scripts/Meta/WildWindMetaPortModel.cs");
        bool extractionLabRemoved =
            !File.Exists(ProjectPath("Assets/Scripts/Meta/WildWindSessionMechanicsModel.cs")) &&
            !File.Exists(ProjectPath("Assets/Scripts/Meta/WildWindSessionMechanicsModel.cs.meta")) &&
            !metaPortRuntimeText.Contains("WildWindExtractionMechanics") &&
            !metaPortRuntimeText.Contains("WildWindMechanicsCatalog") &&
            !metaPortRuntimeText.Contains("ExtractionBoulderState") &&
            !metaPortRuntimeText.Contains("ExtractionChunkState") &&
            !metaPortRuntimeText.Contains("ProjectileProfile") &&
            !metaPortRuntimeText.Contains("ScanModuleProfile") &&
            !metaPortRuntimeText.Contains("FireAuthorizationMode");
        report.Check(extractionLabRemoved,
            "Old pure extraction-lab mechanics model is removed; Big Test now covers the runtime systems directly.");
        string metaGameStateText = ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs");
        string gameplayHudText = ReadProjectText("Assets/Scripts/UI/WildWindGameplayHud.cs");
        string metaPortScreenText = ReadProjectText("Assets/Scripts/UI/WildWindMetaPortScreen.cs");
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
        string starterHullPrefabText = ReadProjectText("Assets/Data/ShipPrefabs/StarterHull.prefab");
        string cruiserHullPrefabText = ReadProjectText("Assets/Data/ShipPrefabs/Cruiser203Hull.prefab");
        string sessionSceneText = ReadProjectText("Assets/Scenes/WildWindSessionScene.unity");
        bool oldMetaEconomyRemoved =
            !metaPortRuntimeText.Contains("ContainerLot") &&
            !metaPortRuntimeText.Contains("VoucherState") &&
            !metaPortRuntimeText.Contains("BattlePass") &&
            !metaPortRuntimeText.Contains("BuyInternalGoldBundle") &&
            !metaPortRuntimeText.Contains("CommanderTalent") &&
            !metaPortRuntimeText.Contains("MechanicsLab") &&
            !metaPortRuntimeText.Contains("WildWindSessionMechanicsProbe") &&
            !metaPortRuntimeText.Contains("RunMiningScenario") &&
            !metaPortRuntimeText.Contains("RunHazardScenario") &&
            !metaPortRuntimeText.Contains("RunMetaScenario") &&
            !gameplayHudText.Contains("MECHANICS LAB") &&
            !gameplayHudText.Contains("RunMechanicsLab") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Missions") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Shop") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Containers") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.BattlePass") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Events") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Commander") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Mining") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Gas") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.Scanning") &&
            !metaPortModelText.Contains("WildWindMetaPortTab.FireControl") &&
            !metaPortModelText.Contains("CreateDemo") &&
            !metaPortModelText.Contains("CreateStandalone") &&
            !metaPortModelText.Contains("\"Silver ") &&
            !metaPortModelText.Contains("CXP ") &&
            !metaPortModelText.Contains("IND ") &&
            !metaPortModelText.Contains("MIL ") &&
            !metaPortModelText.Contains("R&D ") &&
            !metaPortRuntimeText.Contains("silverCost") &&
            !metaPortRuntimeText.Contains("constructionXpUnlockCost") &&
            !metaPortRuntimeText.Contains("unlockedT1Modules") &&
            !metaPortRuntimeText.Contains("silverDelta") &&
            !metaPortRuntimeText.Contains("constructionXpDelta") &&
            !metaPortModelText.Contains("simulated reward") &&
            !metaPortModelText.Contains("Source: standalone") &&
            !metaPortModelText.Contains("Source: demo") &&
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
            !metaPortScreenText.Contains("[ExecuteAlways]") &&
            !metaPortScreenText.Contains("[ContextMenu") &&
            !metaPortScreenText.Contains("EditorSceneManager") &&
            !metaPortScreenText.Contains("MarkSceneDirtyIfEditing") &&
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
            !starterHullPrefabText.Contains("leviathanHunt") &&
            !starterHullPrefabText.Contains("waypoints: []") &&
            !starterHullPrefabText.Contains("positionHold") &&
            !cruiserHullPrefabText.Contains("waypoints: []") &&
            !cruiserHullPrefabText.Contains("positionHold") &&
            !sessionSceneText.Contains("leviathanHunt") &&
            !sessionSceneText.Contains("waypoints: []") &&
            !sessionSceneText.Contains("positionHold") &&
            !sessionSceneText.Contains("engineCheatAfterburnerEnabled");
        report.Check(oldMetaEconomyRemoved,
            "Old meta economy, demo port scaffolding, open-world silhouettes, preview rigs, runtime debug overlays and test-only flight cheats are removed.");
        report.Check(WildWindMetaPortUiState.Tabs.Count == 7,
            "Meta port keeps the compact session tabs only: port, ships, fitting, sorties, processing, production and tech.");
        report.Check(account.ships[0].preinstalledModules.Count > 0,
            "Ships can have preinstalled built-in modules that are not treated as removable fittings.");

        WildWindGameplayHud hud = gameplayHud != null ? gameplayHud : FindFirstObjectByType<WildWindGameplayHud>();
        report.Check(hud != null && hud.IsMetaPortReadyForTests,
            "HUD exposes a WoWS-style meta port with ship carousel, resource strip and tab actions.");
        report.Check(hud != null && hud.IsMetaPortRuntimeBoundForTests && hud.MetaPortReport.Contains("source=runtime"),
            "HUD meta port binds to the live MetaGameState instead of staying as an isolated unbound surface.");
        MetaGameState runtimeMetaForPort = metaGameState != null ? metaGameState : FindFirstObjectByType<MetaGameState>();
        WildWindMetaPortUiState runtimePortState = WildWindMetaPortUiState.CreateFromRuntime(runtimeMetaForPort);
        report.Check(runtimeMetaForPort != null && runtimePortState.IsRuntimeBound && runtimePortState.BuildReport().Contains("source=runtime"),
            "Meta port state can be constructed directly from runtime progress for out-of-sortie screens.");
        string runtimeResourceStrip = runtimePortState.BuildResourceStrip();
        bool runtimeResourceStripUsesBaseResources =
            runtimeResourceStrip.Contains("charcoal")
            && runtimeResourceStrip.Contains("ferron")
            && runtimeResourceStrip.Contains("silvate")
            && runtimeResourceStrip.Contains("weapon")
            && !runtimeResourceStrip.Contains("Silver")
            && !runtimeResourceStrip.Contains("CXP")
            && !runtimeResourceStrip.Contains("IND")
            && !runtimeResourceStrip.Contains("MIL")
            && !runtimeResourceStrip.Contains("R&D");
        report.Check(runtimeResourceStripUsesBaseResources,
            "Meta port resource strip shows runtime base resources instead of retired silver/XP wallets.");
        bool runtimePortShowsCruiser203 = runtimePortState.BuildShipCarousel().Contains("Cruiser 203")
            && runtimePortState.BuildTabContent().Contains("Cruiser 203");
        report.Check(runtimePortShowsCruiser203,
            "Meta port exposes Cruiser 203 as a selectable port ship.");
        bool runtimePortCanSelectCruiser203 = false;
        if (runtimeMetaForPort != null)
        {
            string hullBeforeCruiserProbe = runtimeMetaForPort.progress != null ? runtimeMetaForPort.progress.selectedHullId : "";
            runtimePortState.SelectTab(WildWindMetaPortTab.Port);
            for (int i = 0; i < 8 && !runtimePortState.SelectedShipLabel.Contains("Cruiser 203"); i++)
            {
                runtimePortState.SelectNextShip();
            }

            bool cruiserSelectedInPort = runtimePortState.SelectedShipLabel.Contains("Cruiser 203")
                && runtimePortState.GetSecondaryActionLabel().Contains("Cruiser 203");
            if (runtimeMetaForPort.IsDockedAtCapital())
            {
                WildWindMetaPortActionResult selectCruiserResult = runtimePortState.RunSecondaryAction();
                runtimePortCanSelectCruiser203 = cruiserSelectedInPort
                    && selectCruiserResult != null
                    && selectCruiserResult.success
                    && runtimeMetaForPort.progress != null
                    && runtimeMetaForPort.progress.selectedHullId == "cruiser203_hull";
                runtimeMetaForPort.TrySelectSessionCoreHull(
                    string.IsNullOrWhiteSpace(hullBeforeCruiserProbe) ? GameplaySessionAccountData.DefaultStarterHullId : hullBeforeCruiserProbe,
                    out _);
            }
            else
            {
                runtimePortCanSelectCruiser203 = cruiserSelectedInPort;
            }
        }

        report.Check(runtimePortCanSelectCruiser203,
            "Meta port Cruiser 203 selection is wired to the runtime hull chooser.");
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
        if (hud != null && hud.IsMetaPortScreenVisibleForTests)
        {
            hud.ToggleMetaPortScreenForTests();
        }

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
            metaPortScreen.BindState(WildWindMetaPortUiState.CreateFromRuntime(runtimeMetaForPort));
            metaPortScreen.RebuildScreen();
            report.Check(metaPortScreen.IsReadyForTests,
                "Full-screen meta port can be built from the runtime account state.");

            bool runtimeScreenVisitedAll = false;
            foreach (WildWindMetaPortTab tab in WildWindMetaPortUiState.Tabs)
            {
                string tabName = WildWindMetaPortUiState.GetTabDisplayName(tab);
                bool selected = metaPortScreen.SelectTabForTests(tab) && metaPortScreen.SelectedTabName == tabName;
                report.Check(selected, "Runtime meta port screen selects tab " + tabName + ".");
                report.Check(metaPortScreen.Report.Contains("tab=" + tabName),
                    "Runtime meta port screen report follows selected tab " + tabName + ".");
                report.Check(MetaPortTabContentContainsExpectedMechanics(tab, metaPortScreen.ContentForTests),
                    "Runtime meta port screen tab " + tabName + " exposes its core mechanics.");
                report.Check(metaPortScreen.RunPrimaryActionForTests(),
                    "Runtime meta port screen primary action works for " + tabName + ".");
                report.Check(metaPortScreen.RunSecondaryActionForTests(),
                    "Runtime meta port screen secondary action works for " + tabName + ".");
            }

            runtimeScreenVisitedAll = metaPortScreen.Report.Contains("visited="
                + WildWindMetaPortUiState.Tabs.Count
                + "/"
                + WildWindMetaPortUiState.Tabs.Count);
            report.Check(runtimeScreenVisitedAll, "Runtime meta port screen records that every tab was visited.");
            report.Check(metaPortScreen.SelectNextShipForTests() && metaPortScreen.SelectPreviousShipForTests(),
                "Runtime meta port screen ship carousel can move forward and back.");
            report.Check(metaPortScreen.Report.Contains("ship=") && metaPortScreen.Report.Contains("slots=") && metaPortScreen.Report.Contains("last="),
                "Runtime meta port screen report includes selected ship, port slots and action feedback.");
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
                return ContainsAllIgnoreCase(content, "Session hub", "Port ships", "Starter Pioneer recovery", "Cruiser 203");
            case WildWindMetaPortTab.Ships:
                return ContainsAllIgnoreCase(content, "Session roster", "Pioneer", "Cruiser 203");
            case WildWindMetaPortTab.Fitting:
                return ContainsAllIgnoreCase(content, "High:", "Mid:", "Low/Rig", "Fire control");
            case WildWindMetaPortTab.Sorties:
                return ContainsAllIgnoreCase(content, "Sortie board", "isolated session pocket", "Mining/gas/scanning", "Loadout validation");
            case WildWindMetaPortTab.Processing:
                return ContainsAllIgnoreCase(content, "Five base branches", "Priority", "Buffers");
            case WildWindMetaPortTab.Production:
                return ContainsAllIgnoreCase(content, "Cascade production", "Starter order", "airframe");
            case WildWindMetaPortTab.Technologies:
                return ContainsAllIgnoreCase(content, "single account", "Airship Basics");
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
        report.Section("Direct port entry and account reset");

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
            report.Check(firstHud != null && firstHud.IsMetaPortScreenVisibleForTests,
                "Docked direct entry opens the full port screen automatically.");

            ValidateSessionExtractionCoreLoop(report);

            MetaGameState firstMeta = FindFirstObjectByType<MetaGameState>();
            WildWindGameplayMenu gameplayMenu = FindFirstObjectByType<WildWindGameplayMenu>();
            report.Check(gameplayMenu != null, gameplayMenu != null ? "Gameplay menu is present in the direct port scene." : "Gameplay menu is missing in the direct port scene.");
            if (firstMeta == null || gameplayMenu == null || firstMeta.progress == null)
            {
                yield break;
            }

            const string runtimeProbeResourceId = "runtime_account_probe_resource";
            PortStorageState firstRuntimeStorage = firstMeta.GetCapitalStorageState();
            int runtimeProbeAmount = firstRuntimeStorage.GetResourceAmount(runtimeProbeResourceId) + 12345;
            firstRuntimeStorage.SetResourceAmount(runtimeProbeResourceId, runtimeProbeAmount);
            MetaGameAccountData runtimeSnapshot = firstMeta.CreateRuntimeAccountData();
            PortStorageState runtimeSnapshotStorage = runtimeSnapshot != null && runtimeSnapshot.progress != null
                ? runtimeSnapshot.progress.GetPortStorageState(firstMeta.GetCapitalPortId(), false)
                : null;
            report.Check(runtimeSnapshotStorage != null && runtimeSnapshotStorage.GetResourceAmount(runtimeProbeResourceId) == runtimeProbeAmount,
                "Runtime account snapshot reflects current in-memory progress without writing a save file.");

            SceneManager.LoadScene(DefaultSessionSceneName);
            DisableDuplicateBigTestRunners();
            yield return WaitForLoadedSession();
            ReportSessionReadinessIfNeeded("fresh runtime scene reload", report);
            ValidateLoadedSession("fresh runtime scene reload", report);

            MetaGameState reloadedMeta = FindFirstObjectByType<MetaGameState>();
            PortStorageState reloadedRuntimeStorage = reloadedMeta != null ? reloadedMeta.GetCapitalStorageState() : null;
            report.Check(reloadedMeta != null
                && reloadedMeta.progress != null
                && reloadedRuntimeStorage != null
                && reloadedRuntimeStorage.GetResourceAmount(runtimeProbeResourceId) != runtimeProbeAmount,
                "Direct scene reload starts a fresh runtime account instead of restoring disk progress.");

            WildWindGameplayMenu reloadedMenu = FindFirstObjectByType<WildWindGameplayMenu>();
            report.Check(reloadedMenu != null, reloadedMenu != null ? "Gameplay menu is present after fresh runtime reload." : "Gameplay menu is missing after fresh runtime reload.");
            if (reloadedMenu == null)
            {
                yield break;
            }

            reloadedMenu.SetOpen(true);
            yield return null;
            MetaGameState pausedMeta = FindFirstObjectByType<MetaGameState>();
            report.Check(pausedMeta != null && pausedMeta.IsSessionPaused && Approximately(Time.timeScale, 0f, 0.001f),
                "Esc menu pauses the session scene.");

            bool resetInvoked = TryInvokePrivateMethod(reloadedMenu, "ResetProgressCheat", report);
            report.Check(resetInvoked, "Reset progress cheat can be invoked by the gameplay menu without exceptions.");
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
                "Reset progress cheat returns the current runtime account to fresh docked port progress.");
            report.Check(resetHud != null && resetHud.IsMetaPortScreenVisibleForTests,
                "Reset progress cheat leaves the player in the visible port screen.");
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

            bool pioneerAssemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult pioneerAssembly);
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
                "Starter Pioneer recovery has enough lift, hover power reserve, thrust, and speed for a starter ore sortie: "
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

        bool cycledSortie = loadedHud != null
            ? loadedHud.TryCycleSessionSortie()
            : loadedMeta.SelectNextSessionSortie(out _);
        report.Check(cycledSortie
            && loadedMeta.GetSelectedSessionSortieDefinition().primaryBranch != BaseProcessingBranch.Ore,
            "HUD can cycle the selected extraction sortie before launch.");

        bool selectedGasSortie = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeGasSortieId, out string selectedGasMessage);
        bool selectedGasBlockedWithoutFitting = !loadedMeta.CanBeginSelectedSessionSortie(out string gasBlockedReason);
        progress.InstallModule("utility_01", SessionExtractionConstants.StarterGasExtractorModuleId);
        bool selectedGasStillBlockedWithLegacyUtility = !loadedMeta.CanBeginSelectedSessionSortie(out string legacyUtilityReason);
        progress.InstallModule("utility_01", "");
        progress.InstallModule(SessionExtractionConstants.StarterHighSlotId, SessionExtractionConstants.StarterGasExtractorModuleId);
        bool selectedGasReadyWithFitting = loadedMeta.CanBeginSelectedSessionSortie(out string gasReadyReason);
        progress.InstallModule(SessionExtractionConstants.StarterHighSlotId, "");
        report.Check(selectedGasSortie
            && selectedGasBlockedWithoutFitting
            && selectedGasStillBlockedWithLegacyUtility
            && selectedGasReadyWithFitting,
            "Selected gas sortie is gated by a fitted High gas extractor, not legacy utility: " + selectedGasMessage + " / " + gasBlockedReason + " / " + legacyUtilityReason + " / " + gasReadyReason);

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
            "Core public fitting API rejects legacy utility and wrong-band installs while allowing valid High-slot modules.");

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
        bool sortieStarted = loadedHud != null ? loadedHud.TryTakeOff() : loadedMeta.BeginSafeOreSortie();
        report.Check(sortieStarted
            && loadedMeta.HasActiveSortie
            && progress.currentMode == GameSessionMode.Flight,
            "HUD starts a safe ore sortie from the base and switches to flight.");
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
        PortStorageState baseStorageBeforeExtraction = loadedMeta.GetCapitalStorageState();
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
            && estimateReady.requiredCoalKg > 0f
            && estimateReady.requiredClaudiumKg >= 0f,
            "Boundary extraction calculates return coal and claudium reserves after the claudium slipstream exit run.");
        float coalBeforeExtraction = progress.shipEngineFuelTank.GetAmount("charcoal");
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
            "Base automaton dismantling turns wrecks into cores and session materials: " + automatonMessage);

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
        bool highGasUpgradeInstalled = loadedMeta.TryInstallStarterGasExtractorUpgrade(out string highGasUpgradeMessage);
        bool selectedGasAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeGasSortieId, out _);
        bool selectedGasReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string gasAfterUpgradeReason);
        report.Check(highGasUpgradeInstalled
            && selectedGasAfterUpgrade
            && selectedGasReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterHighSlotId) == SessionExtractionConstants.StarterGasExtractorModuleId
            && baseStorage.GetResourceAmount(SessionExtractionConstants.StarterModuleKitItemId) == moduleKitsBeforeHighUpgrade - 1,
            "Starter module kit installs the first High gas extractor and unlocks gas sorties: " + highGasUpgradeMessage + " / " + gasAfterUpgradeReason);

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
            "Starter module kit installs the High impact wreck collector and unlocks automaton sorties: " + miningHoldUpgradeMessage + " / " + automatonAfterUpgradeReason);

        bool salvageInstalled = loadedMeta.TryInstallStarterLeviathanSalvageUpgrade(out string salvageUpgradeMessage);
        bool selectedLeviathanAfterUpgrade = loadedMeta.SelectSessionSortie(SessionExtractionConstants.DefaultSafeLeviathanSortieId, out _);
        bool selectedLeviathanReadyAfterUpgrade = loadedMeta.CanBeginSelectedSessionSortie(out string leviathanAfterUpgradeReason);
        report.Check(salvageInstalled
            && selectedLeviathanAfterUpgrade
            && selectedLeviathanReadyAfterUpgrade
            && progress.GetInstalledModule(SessionExtractionConstants.StarterThirdHighSlotId) == SessionExtractionConstants.StarterLeviathanSalvageModuleId,
            "Starter module kit installs the High leviathan salvage rig and unlocks leviathan remains sorties: " + salvageUpgradeMessage + " / " + leviathanAfterUpgradeReason);

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
            && !fittingSummary.Contains("utility")
            && !fittingCompact.Contains("utility")
            && !ReadProjectText("Assets/Scripts/Meta/MetaGameState.cs").Contains("UsesLegacyDockAssemblyUi"),
            "Core dock UI exposes High/Mid/Low/Rig fitting instead of legacy assembly/utility slots: " + fittingCompact + " / " + fittingSummary);

        bool assemblyBuilt = ShipAssemblyBuilder.TryBuild(loadedMeta.CurrentCatalog, progress, out ShipAssemblyResult assembly);
        report.Check(assemblyBuilt
            && HasSlotType(assembly, SessionExtractionConstants.HighSlotTypeId)
            && HasSlotType(assembly, SessionExtractionConstants.MidSlotTypeId)
            && HasSlotType(assembly, SessionExtractionConstants.LowSlotTypeId)
            && HasSlotType(assembly, SessionExtractionConstants.RigSlotTypeId)
            && HasSlotId(assembly, SessionExtractionConstants.StarterSecondHighSlotId)
            && HasSlotId(assembly, SessionExtractionConstants.StarterThirdHighSlotId)
            && HasSlotId(assembly, SessionExtractionConstants.StarterMidSlotId)
            && !HasSlotType(assembly, "utility")
            && !HasSlotId(assembly, "utility_01"),
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
            && progress.selectedHullId == GameplaySessionAccountData.DefaultStarterHullId
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
            && progress.selectedHullId == GameplaySessionAccountData.DefaultStarterHullId
            && progress.shipEngineFuelTank.GetAmount("charcoal") >= loadedMeta.startingFuelKg
            && progress.shipClaudiumTank.GetAmount("claudium") >= loadedMeta.startingClaudiumKg,
            "Starter Pioneer recovery restores a missing hull and receives a free minimal refuel even when base coal and claudium are empty: "
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

        public string ActiveSceneName { get; private set; }

        private BigTestSideEffectSnapshot()
        {
        }

        public static BigTestSideEffectSnapshot Capture()
        {
            Scene scene = SceneManager.GetActiveScene();
            return new BigTestSideEffectSnapshot
            {
                timeScale = Time.timeScale,
                ActiveSceneName = scene.IsValid() ? scene.name : ""
            };
        }

        public void RestorePrefsAndTimeScale()
        {
            Time.timeScale = timeScale;
        }

        public void AssertRestored(BigTestReport report)
        {
            Scene activeScene = SceneManager.GetActiveScene();

            report.Check(Approximately(Time.timeScale, timeScale, 0.001f),
                "Time.timeScale Р Р†Р С•РЎРѓРЎРѓРЎвЂљР В°Р Р…Р С•Р Р†Р В»Р ВµР Р… Р С—Р С•РЎРѓР В»Р Вµ Р В±Р С•Р В»РЎРЉРЎв‚¬Р С•Р С–Р С• РЎвЂљР ВµРЎРѓРЎвЂљР В°: " + Time.timeScale.ToString("0.###") + ".");
            report.Check(string.IsNullOrWhiteSpace(ActiveSceneName) || activeScene.name == ActiveSceneName,
                "Р С’Р С”РЎвЂљР С‘Р Р†Р Р…Р В°РЎРЏ РЎРѓРЎвЂ Р ВµР Р…Р В° Р Р†Р С•РЎРѓРЎРѓРЎвЂљР В°Р Р…Р С•Р Р†Р В»Р ВµР Р…Р В° Р С—Р С•РЎРѓР В»Р Вµ Р В±Р С•Р В»РЎРЉРЎв‚¬Р С•Р С–Р С• РЎвЂљР ВµРЎРѓРЎвЂљР В°: " + activeScene.name + ".");
        }
    }
    private void ValidateMaintainability(BigTestReport report)
    {
        report.Section("РњРµС‚РѕРґРёРєР° СЃРѕРїСЂРѕРІРѕР¶РґРµРЅРёСЏ");
        ValidateLegacyNearestWeaponApiRemoved(report);
        ValidateArmorDegradationRemoved(report);
        ValidateSessionInputUsesInputSystem(report);
        ValidateRuntimeGunFallbackRemoved(report);
        ValidateNoUnauthorizedEditorTools(report);
        report.Info("PROJECT RULE: NO NEW EDITOR TOOLS. Only the Big Test editor entry point is generally allowed; asset fixes must be made directly and checked here.");
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
        string starterHullPrefabText = ReadProjectText("Assets/Data/ShipPrefabs/StarterHull.prefab");
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
                starterHullPrefabText.Contains(fallbackTokens[i]) ||
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
                bool allowedBigTestMenuFile = string.Equals(relativePath, "Assets/Scripts/Editor/WildWindBigTestMenu.cs", StringComparison.Ordinal);
                if (!allowedBigTestMenuFile)
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

                    if (!allowedBigTestMenuFile)
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
            "Assets/Scripts/Systems/PaintedArmorBody.cs",
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
        ship.miningImpactDamageTakenMultiplier = 1f;
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
        float propellerPowerKw = Mathf.Max(0f, ship.enginePowerKwAt100 * ship.enginePowerLever - ship.claudiumPowerDrawKw);
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
