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
            WildWindBigTestRunner runner = EnsureRunnerInActiveScene();
            runner.ResetRunStateForEditor();
            runner.RunBigTest();
            Selection.activeGameObject = runner.gameObject;
            return;
        }

        WorldSceneBuilder.BuildFinalWorldScene();
        WildWindBigTestRunner preparedRunner = EnsureRunnerInActiveScene();
        preparedRunner.runOnStart = true;
        preparedRunner.logFullReportToConsole = true;
        preparedRunner.writeReportFile = true;
        preparedRunner.productionSimulationMinutes = 12f;
        preparedRunner.streamerAverageBudgetMs = 250f;
        preparedRunner.ResetRunStateForEditor();

        EditorUtility.SetDirty(preparedRunner);
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        Selection.activeGameObject = preparedRunner.gameObject;
        Debug.Log("[WildWindBigTest] Сцена большого теста готова. Включаю Play Mode, протокол появится в Console и TestReports/WildWindBigTestReport.txt.");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Wild Wind/Провести большой тест", true)]
    public static bool ValidateRunBigTest()
    {
        return !EditorApplication.isCompiling &&
            (EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode);
    }

    private static WildWindBigTestRunner EnsureRunnerInActiveScene()
    {
        WildWindBigTestRunner runner = Object.FindFirstObjectByType<WildWindBigTestRunner>();
        if (runner == null)
        {
            GameObject runnerObject = new GameObject("Wild Wind Big Test Runner");
            Transform root = GameObject.Find("Wild Wind World") != null ? GameObject.Find("Wild Wind World").transform : null;
            if (root != null)
            {
                runnerObject.transform.SetParent(root, false);
            }

            Undo.RegisterCreatedObjectUndo(runnerObject, "Create Wild Wind Big Test Runner");
            runner = runnerObject.AddComponent<WildWindBigTestRunner>();
        }
        else
        {
            Undo.RecordObject(runner, "Configure Wild Wind Big Test Runner");
        }

        runner.world = Object.FindFirstObjectByType<WorldRegionRuntime>();
        runner.worldIndex = Object.FindFirstObjectByType<WorldEntityIndex>();
        runner.runtimeState = Object.FindFirstObjectByType<WorldRuntimeState>();
        runner.simulationTick = Object.FindFirstObjectByType<WorldSimulationTick>();
        runner.streamer = Object.FindFirstObjectByType<WorldBubbleStreamer>();
        runner.focus = runner.world != null ? runner.world.Focus : null;
        runner.visualTuner = Object.FindFirstObjectByType<VisualPlayModeTuner>();
        runner.settings = Object.FindFirstObjectByType<WildWindSettingsRoot>();
        runner.metaGameState = Object.FindFirstObjectByType<MetaGameState>();
        EditorUtility.SetDirty(runner);
        return runner;
    }
}
