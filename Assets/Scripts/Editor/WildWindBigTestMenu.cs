using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WildWindBigTestMenu
{
    private const string SessionScenePath = "Assets/Scenes/WildWindSessionScene.unity";

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
            Debug.LogError("[WildWindBigTest] Session scene is missing: " + SessionScenePath);
            return;
        }

        WildWindBigTestRunner.MarkEditorBigTestLaunchPending();
        WriteLaunchStatus("launching", "Editor menu requested Play Mode for the big test.");

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[WildWindBigTest] Big test was not started because modified scenes were not saved.");
            return;
        }

        EditorSceneManager.OpenScene(SessionScenePath);
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
}
