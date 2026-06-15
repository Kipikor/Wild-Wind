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
    private static double nextAutomationPollTime;

    static WildWindBigTestMenu()
    {
        EditorApplication.update -= PollAutomationRequest;
        EditorApplication.update += PollAutomationRequest;
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

    private static string GetAutomationRequestPath()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, "TestReports", AutomationRequestFileName);
    }
}
