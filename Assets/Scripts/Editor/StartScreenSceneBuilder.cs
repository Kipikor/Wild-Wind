using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StartScreenSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/StartScreen.unity";
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
        AddSceneIfExists(scenes, WorldScenePath, true);
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

/// <summary>
/// Keeps the editor workflow anchored at the real game entry point.
/// Normal Play starts from StartScreen, and after Play the editor returns there.
/// </summary>
[InitializeOnLoad]
public static class WildWindEditorStartSceneGuard
{
    private const string StartScenePath = StartScreenSceneBuilder.ScenePath;
    private const string WorldScenePath = "Assets/Scenes/WildWindWorldScene.unity";
    private const string SuppressNextPlayStartSceneKey = "WildWind.SuppressNextPlayStartScene";

    static WildWindEditorStartSceneGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= EnsureNormalPlayStartsAtStartScreen;
        EditorApplication.delayCall += EnsureNormalPlayStartsAtStartScreen;
    }

    public static void UseWorldSceneForNextPlay()
    {
        SessionState.SetBool(SuppressNextPlayStartSceneKey, true);
        SceneAsset worldScene = LoadSceneAsset(WorldScenePath);
        if (worldScene != null)
        {
            EditorSceneManager.playModeStartScene = worldScene;
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (Application.isBatchMode)
        {
            return;
        }

        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (SessionState.GetBool(SuppressNextPlayStartSceneKey, false))
            {
                SessionState.SetBool(SuppressNextPlayStartSceneKey, false);
                return;
            }

            WildWindBigTestRunner.ClearEditorBigTestLaunchPending();
            EnsureNormalPlayStartsAtStartScreen();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            WildWindBigTestRunner.ClearEditorBigTestLaunchPending();
            EnsureNormalPlayStartsAtStartScreen();
            EditorApplication.delayCall -= OpenStartScreenAfterPlay;
            EditorApplication.delayCall += OpenStartScreenAfterPlay;
        }
    }

    private static void EnsureNormalPlayStartsAtStartScreen()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        SceneAsset startScene = LoadSceneAsset(StartScenePath);
        if (startScene != null && EditorSceneManager.playModeStartScene != startScene)
        {
            EditorSceneManager.playModeStartScene = startScene;
        }
    }

    private static void OpenStartScreenAfterPlay()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == StartScenePath)
        {
            return;
        }

        if (!File.Exists(StartScenePath))
        {
            Debug.LogWarning("[WildWindEditorStartSceneGuard] Start scene is missing: " + StartScenePath);
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[WildWindEditorStartSceneGuard] StartScreen was not opened because modified scenes were not saved.");
            return;
        }

        EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
    }

    private static SceneAsset LoadSceneAsset(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
    }
}
