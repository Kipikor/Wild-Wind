using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WildWindBigTestMenu
{
    [MenuItem("Wild Wind/Провести большой тест")]
    public static void RunBigTest()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[WildWindBigTest] Stop Play Mode before running the big test. The test starts from Edit Mode so it cannot consume the current gameplay launch/save.");
            return;
        }

        WorldSceneBuilder.BuildFinalWorldScene();
        RemoveSceneBigTestRunnersFromActiveScene();
        WildWindSaveSlots.ClearPendingGameplayLaunch();
        WildWindBigTestRunner.MarkEditorBigTestLaunchPending();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log("[WildWindBigTest] Сцена большого теста готова. Включаю Play Mode, протокол появится в Console и TestReports/WildWindBigTestReport.txt.");
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

    private static void RemoveSceneBigTestRunnersFromActiveScene()
    {
        WildWindBigTestRunner[] runners = Object.FindObjectsByType<WildWindBigTestRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < runners.Length; i++)
        {
            WildWindBigTestRunner runner = runners[i];
            if (runner == null)
            {
                continue;
            }

            Object.DestroyImmediate(runner.gameObject);
        }
    }
}
