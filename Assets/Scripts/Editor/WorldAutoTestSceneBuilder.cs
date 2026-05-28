using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldAutoTestSceneBuilder
{
    public static void PrepareAutoCheckingWorldTestScene()
    {
        WorldSceneBuilder.BuildFinalWorldScene();

        WorldRegionRuntime world = Object.FindFirstObjectByType<WorldRegionRuntime>();
        WorldEntityIndex index = Object.FindFirstObjectByType<WorldEntityIndex>();
        WorldRuntimeState runtimeState = Object.FindFirstObjectByType<WorldRuntimeState>();
        WorldSimulationTick simulationTick = Object.FindFirstObjectByType<WorldSimulationTick>();
        WorldBubbleStreamer streamer = Object.FindFirstObjectByType<WorldBubbleStreamer>();
        Transform focus = world != null ? world.Focus : null;

        if (index != null && world != null)
        {
            index.Configure(world);
            EditorUtility.SetDirty(index);
        }

        if (runtimeState != null && world != null)
        {
            runtimeState.Configure(world, index, focus);
            EditorUtility.SetDirty(runtimeState);
        }

        if (simulationTick != null && world != null)
        {
            simulationTick.Configure(world, index, runtimeState, focus, streamer);
            EditorUtility.SetDirty(simulationTick);
        }

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
