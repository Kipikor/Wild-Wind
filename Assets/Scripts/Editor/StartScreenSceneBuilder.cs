using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StartScreenSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/StartScreen.unity";
    private const string GameplayScenePath = "Assets/Scenes/SampleScene.unity";
    private const string WorldScenePath = "Assets/Scenes/WildWindWorldScene.unity";
    private const string RootName = "Wild Wind Start Screen";

    [MenuItem("Wild Wind/Start Screen/Build Scene")]
    [MenuItem("Wild Wind/UI/Build Start Screen")]
    public static void BuildStartScreen()
    {
        EnsureSceneFolder();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "StartScreen";

        WildWindStartScreen startScreen = CreateOrResetStartScreenRoot();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettings();
        Selection.activeGameObject = startScreen.gameObject;
        Debug.Log("[WildWindStartScreen] Editable start screen scene built: " + ScenePath);
    }

    [MenuItem("Wild Wind/Start Screen/Rebuild Open Scene UI")]
    public static void RebuildOpenStartScreen()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[WildWindStartScreen] No active scene to rebuild.");
            return;
        }

        WildWindStartScreen startScreen = Object.FindFirstObjectByType<WildWindStartScreen>();
        if (startScreen == null)
        {
            startScreen = CreateOrResetStartScreenRoot();
        }
        else
        {
            startScreen.gameObject.name = RootName;
            startScreen.gameplaySceneName = "WildWindWorldScene";
            startScreen.referenceResolution = new Vector2(1920f, 1080f);
            startScreen.panelWidth = 520f;
            startScreen.RebuildEditableScreen();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrWhiteSpace(scene.path))
        {
            EditorSceneManager.SaveScene(scene);
        }

        EnsureBuildSettings();
        Selection.activeGameObject = startScreen.gameObject;
        Debug.Log("[WildWindStartScreen] Open scene UI rebuilt.");
    }

    [MenuItem("Wild Wind/Start Screen/Open Scene")]
    public static void OpenStartScreen()
    {
        if (!File.Exists(ScenePath))
        {
            BuildStartScreen();
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureBuildSettings();

        WildWindStartScreen startScreen = Object.FindFirstObjectByType<WildWindStartScreen>();
        if (startScreen != null)
        {
            Selection.activeGameObject = startScreen.gameObject;
        }

        Debug.Log("[WildWindStartScreen] Start screen scene opened: " + scene.path);
    }

    private static WildWindStartScreen CreateOrResetStartScreenRoot()
    {
        GameObject root = new GameObject(RootName);
        WildWindStartScreen startScreen = root.AddComponent<WildWindStartScreen>();
        startScreen.gameplaySceneName = "WildWindWorldScene";
        startScreen.referenceResolution = new Vector2(1920f, 1080f);
        startScreen.panelWidth = 520f;
        startScreen.RebuildEditableScreen();
        return startScreen;
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        AddSceneIfExists(scenes, ScenePath, true);
        AddSceneIfExists(scenes, GameplayScenePath, true);
        AddSceneIfExists(scenes, WorldScenePath, false);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddSceneIfExists(List<EditorBuildSettingsScene> scenes, string path, bool enabled)
    {
        if (!System.IO.File.Exists(path))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(path, enabled));
    }

    private static void EnsureSceneFolder()
    {
        string folder = Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
