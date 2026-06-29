using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WildWindBigTestMenu
{
    private const string SessionScenePath = "Assets/Scenes/WildWindSessionScene.unity";
    private const string AutomationRequestFileName = "WildWindBigTestLaunch.request";
    private const string MainHudScreenshotActiveKey = "WildWind.MainHudScreenshot.Active";
    private const string MainHudScreenshotPathKey = "WildWind.MainHudScreenshot.Path";
    private const string MainHudScreenshotStartedAtKey = "WildWind.MainHudScreenshot.StartedAt";
    private const string MainHudScreenshotIssuedKey = "WildWind.MainHudScreenshot.Issued";
    private const string MainHudScreenshotIssuedAtKey = "WildWind.MainHudScreenshot.IssuedAt";
    private const string MainHudScreenshotQuitKey = "WildWind.MainHudScreenshot.Quit";
    private const int MainHudScreenshotWidth = 1920;
    private const int MainHudScreenshotHeight = 1080;
    private static double nextAutomationPollTime;

    static WildWindBigTestMenu()
    {
        EditorApplication.update -= PollAutomationRequest;
        EditorApplication.update += PollAutomationRequest;
        EditorApplication.update -= PollMainHudScreenshotCapture;
        EditorApplication.update += PollMainHudScreenshotCapture;
    }

    [MenuItem("Wild Wind/Провести большой тест")]
    public static void RunBigTest()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[WildWindBigTest] Stop Play Mode before running the big test. The test starts from Edit Mode so it cannot consume the current gameplay launch/save.");
            return;
        }

        if (!File.Exists(SessionScenePath))
        {
            WildWindBigTestRunner.ClearEditorBigTestLaunchPending();
            Debug.LogError("[WildWindBigTest] Session scene is missing: " + SessionScenePath);
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            WildWindBigTestRunner.ClearEditorBigTestLaunchPending();
            WriteLaunchStatus("blocked", "Big test was not started because modified scenes were not saved.");
            Debug.LogWarning("[WildWindBigTest] Big test was not started because modified scenes were not saved.");
            return;
        }

        EditorSceneManager.OpenScene(SessionScenePath);
        WildWindBigTestRunner.MarkEditorBigTestLaunchPending();
        WriteLaunchStatus("launching", "Editor menu requested Play Mode for the big test.");
        Debug.Log("[WildWindBigTest] Starting Play Mode on session scene. Report: TestReports/WildWindBigTestReport.txt.");
        EditorApplication.isPlaying = true;
    }

    public static void RunBigTestAndQuit()
    {
        WildWindBigTestRunner.ArmEditorQuitAfterBigTest();
        RunBigTest();
    }

    [MenuItem("Wild Wind/Capture Main HUD Screenshot")]
    public static void CaptureMainHudScreenshot()
    {
        StartMainHudScreenshotCapture(false);
    }

    public static void CaptureMainHudScreenshotAndQuit()
    {
        StartMainHudScreenshotCapture(true);
    }

    [MenuItem("Wild Wind/Провести большой тест", true)]
    public static bool ValidateRunBigTest()
    {
        return !EditorApplication.isCompiling &&
            !EditorApplication.isPlaying &&
            !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void WriteLaunchStatus(string state, string message)
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, "TestReports");
            Directory.CreateDirectory(folder);

            string sceneName = EditorSceneManager.GetActiveScene().name;
            string text =
                "state: " + (string.IsNullOrWhiteSpace(state) ? "unknown" : state) + Environment.NewLine +
                "generatedAtUtc: " + DateTime.UtcNow.ToString("O") + Environment.NewLine +
                "scene: " + sceneName + Environment.NewLine +
                "message: " + (message ?? "") + Environment.NewLine;

            File.WriteAllText(Path.Combine(folder, "WildWindBigTestStatus.txt"), text);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WildWindBigTest] Could not write launch status: " + exception.Message);
        }
    }

    private static void PollAutomationRequest()
    {
        if (EditorApplication.timeSinceStartup < nextAutomationPollTime)
        {
            return;
        }

        nextAutomationPollTime = EditorApplication.timeSinceStartup + 1.0d;
        string requestPath = GetAutomationRequestPath();
        if (!File.Exists(requestPath))
        {
            return;
        }

        string requestText = "";
        try
        {
            requestText = File.ReadAllText(requestPath).Trim();
        }
        catch (Exception exception)
        {
            WriteLaunchStatus("blocked", "Could not read automation request: " + exception.Message);
            return;
        }

        bool stopPlayModeOnly = string.Equals(requestText, "stop-play-mode", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestText, "stop", StringComparison.OrdinalIgnoreCase);

        if (EditorApplication.isCompiling)
        {
            WriteLaunchStatus("waiting", "Automation request is waiting for Unity compilation.");
            return;
        }

        if (stopPlayModeOnly)
        {
            if (EditorApplication.isPlaying)
            {
                WriteLaunchStatus("stopping", "Automation request is stopping Play Mode.");
                EditorApplication.isPlaying = false;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                WriteLaunchStatus("waiting", "Automation request is waiting for Unity to return to Edit Mode.");
                return;
            }

            try
            {
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                WriteLaunchStatus("blocked", "Could not consume automation request: " + exception.Message);
                return;
            }

            WriteLaunchStatus("stopped", "Automation stop request completed; editor is in Edit Mode.");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            WriteLaunchStatus("waiting", "Automation request is stopping Play Mode before the big test.");
            EditorApplication.isPlaying = false;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            WriteLaunchStatus("waiting", "Automation request is waiting for Unity to return to Edit Mode.");
            return;
        }

        try
        {
            File.Delete(requestPath);
        }
        catch (Exception exception)
        {
            WriteLaunchStatus("blocked", "Could not consume automation request: " + exception.Message);
            return;
        }

        Debug.Log("[WildWindBigTest] Automation request consumed: " + requestPath);
        RunBigTest();
    }

    private static void StartMainHudScreenshotCapture(bool quitWhenDone)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[WildWindMainHudScreenshot] Wait until the editor returns to Edit Mode before capturing the main HUD.");
            return;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string reportFolder = Path.Combine(projectRoot, "TestReports");
        Directory.CreateDirectory(reportFolder);
        string screenshotPath = Path.Combine(reportFolder, "WildWindMainScreenHud.png");
        if (File.Exists(screenshotPath))
        {
            File.Delete(screenshotPath);
        }

        if (!File.Exists(SessionScenePath))
        {
            Debug.LogError("[WildWindMainHudScreenshot] Session scene is missing: " + SessionScenePath);
            if (quitWhenDone)
            {
                EditorApplication.Exit(1);
            }

            return;
        }

        SessionState.SetBool(MainHudScreenshotActiveKey, true);
        SessionState.SetString(MainHudScreenshotPathKey, screenshotPath);
        SessionState.SetString(MainHudScreenshotStartedAtKey, EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        SessionState.SetBool(MainHudScreenshotIssuedKey, false);
        SessionState.SetString(MainHudScreenshotIssuedAtKey, "0");
        SessionState.SetBool(MainHudScreenshotQuitKey, quitWhenDone);

        EditorSceneManager.OpenScene(SessionScenePath);
        Debug.Log("[WildWindMainHudScreenshot] Opening Play Mode for HUD screenshot: " + screenshotPath);
        EditorApplication.isPlaying = true;
    }

    private static void PollMainHudScreenshotCapture()
    {
        if (!SessionState.GetBool(MainHudScreenshotActiveKey, false))
        {
            return;
        }

        double startedAt = ParseMainHudScreenshotDouble(SessionState.GetString(MainHudScreenshotStartedAtKey, "0"));
        if (EditorApplication.timeSinceStartup - startedAt > 60d)
        {
            FinishMainHudScreenshotCapture(false, "Timed out while waiting for the main HUD screenshot.");
            return;
        }

        if (EditorApplication.isCompiling || (EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying))
        {
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = true;
            return;
        }

        string screenshotPath = SessionState.GetString(MainHudScreenshotPathKey, "");
        bool captureIssued = SessionState.GetBool(MainHudScreenshotIssuedKey, false);
        if (captureIssued)
        {
            double issuedAt = ParseMainHudScreenshotDouble(SessionState.GetString(MainHudScreenshotIssuedAtKey, "0"));
            if (File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0)
            {
                FinishMainHudScreenshotCapture(true, "Saved screenshot: " + screenshotPath);
                return;
            }

            if (EditorApplication.timeSinceStartup - issuedAt > 10d)
            {
                FinishMainHudScreenshotCapture(false, "Screenshot capture was issued, but the file was not written: " + screenshotPath);
            }

            return;
        }

        WildWindGameplayHud hud = UnityEngine.Object.FindFirstObjectByType<WildWindGameplayHud>();
        if (hud == null || !hud.IsReady || !hud.IsPortHudVisibleForTests)
        {
            return;
        }

        Screen.SetResolution(MainHudScreenshotWidth, MainHudScreenshotHeight, false);
        ScreenCapture.CaptureScreenshot(screenshotPath);
        SessionState.SetBool(MainHudScreenshotIssuedKey, true);
        SessionState.SetString(MainHudScreenshotIssuedAtKey, EditorApplication.timeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        Debug.Log("[WildWindMainHudScreenshot] Capture requested at "
            + MainHudScreenshotWidth
            + "x"
            + MainHudScreenshotHeight
            + ": "
            + screenshotPath);
    }

    private static void FinishMainHudScreenshotCapture(bool succeeded, string message)
    {
        bool quitWhenDone = SessionState.GetBool(MainHudScreenshotQuitKey, false);
        if (succeeded)
        {
            Debug.Log("[WildWindMainHudScreenshot] " + message);
        }
        else
        {
            Debug.LogError("[WildWindMainHudScreenshot] " + message);
        }

        ClearMainHudScreenshotState();
        EditorApplication.isPlaying = false;
        if (quitWhenDone)
        {
            EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }

    private static void ClearMainHudScreenshotState()
    {
        SessionState.SetBool(MainHudScreenshotActiveKey, false);
        SessionState.EraseString(MainHudScreenshotPathKey);
        SessionState.EraseString(MainHudScreenshotStartedAtKey);
        SessionState.SetBool(MainHudScreenshotIssuedKey, false);
        SessionState.EraseString(MainHudScreenshotIssuedAtKey);
        SessionState.SetBool(MainHudScreenshotQuitKey, false);
    }

    private static double ParseMainHudScreenshotDouble(string value)
    {
        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double result)
            ? result
            : 0d;
    }

    private static string GetAutomationRequestPath()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, "TestReports", AutomationRequestFileName);
    }
}
