using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WildWindBigTestMenu
{
    private const string WorldScenePath = "Assets/Scenes/WildWindWorldScene.unity";

    [MenuItem("Wild Wind/Провести большой тест")]
    public static void RunBigTest()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[WildWindBigTest] Stop Play Mode before running the big test. The test starts from Edit Mode so it cannot consume the current gameplay launch/save.");
            return;
        }

        if (!File.Exists(WorldScenePath))
        {
            Debug.Log("[WildWindBigTest] World scene is missing, rebuilding once before the big test: " + WorldScenePath);
            WorldSceneBuilder.BuildFinalWorldScene();
        }

        WildWindSaveSlots.ClearPendingGameplayLaunch();
        WildWindUsageAudit.DisarmForBigTest();
        WildWindBigTestRunner.MarkEditorBigTestLaunchPending();
        WriteLaunchStatus("launching", "Editor menu requested Play Mode for the big test.");

        Debug.Log("[WildWindBigTest] Starting Play Mode on existing world scene. Report: TestReports/WildWindBigTestReport.txt.");
        WildWindEditorStartSceneGuard.UseWorldSceneForNextPlay();
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
