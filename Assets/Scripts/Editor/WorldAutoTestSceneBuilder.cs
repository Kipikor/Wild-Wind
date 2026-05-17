using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldAutoTestSceneBuilder
{
    [MenuItem("Wild Wind/World/Prepare Auto-Checking World Test Scene")]
    public static void PrepareAutoCheckingWorldTestScene()
    {
        WorldSceneBuilder.BuildFinalWorldScene();

        WorldRegionRuntime world = Object.FindFirstObjectByType<WorldRegionRuntime>();
        WorldBubbleStreamer streamer = Object.FindFirstObjectByType<WorldBubbleStreamer>();
        Transform focus = world != null ? world.Focus : null;

        GameObject runnerObject = GameObject.Find("World Auto Test Runner");
        if (runnerObject == null)
        {
            runnerObject = new GameObject("World Auto Test Runner");
        }

        WorldAutoTestRunner runner = runnerObject.GetComponent<WorldAutoTestRunner>();
        if (runner == null)
        {
            runner = runnerObject.AddComponent<WorldAutoTestRunner>();
        }

        runner.world = world;
        runner.streamer = streamer;
        runner.focus = focus;
        runner.runOnStart = true;
        runner.quietMode = true;
        runner.logSuccessfulChecks = false;
        runner.refreshBudgetMs = 250f;

        EditorUtility.SetDirty(runner);
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        Selection.activeGameObject = runnerObject;
        Debug.Log("[WorldAutoTest] Сцена автопроверки мира готова. Войди в Play Mode, runner выведет OK/FAIL в Console.");
    }
}
