using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class FlagshipConstructorPreviewBuilder
{
    public const string ConstructorPreviewScenePath = "Assets/Scenes/FlagshipConstructorPreview.unity";
    private const string RootName = "Wild Wind Flagship Constructor Screen";

    [MenuItem("Wild Wind/Expeditions/Build Preview/2 Flagship Constructor")]
    public static void BuildConstructorPreview()
    {
        EnsureSceneFolder();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "FlagshipConstructorPreview";

        ConfigureCamera();
        CreateEventSystem();
        WildWindFlagshipConstructorScreen screen = CreateConstructorRoot();

        EditorSceneManager.SaveScene(scene, ConstructorPreviewScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = screen.gameObject;
        Debug.Log("[WildWindFlagshipConstructor] Constructor preview scene built: " + ConstructorPreviewScenePath);
    }

    [MenuItem("Wild Wind/Expeditions/Open Preview/2 Flagship Constructor")]
    public static void OpenConstructorPreview()
    {
        if (!File.Exists(ConstructorPreviewScenePath))
        {
            BuildConstructorPreview();
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ConstructorPreviewScenePath, OpenSceneMode.Single);
        WildWindFlagshipConstructorScreen screen = Object.FindFirstObjectByType<WildWindFlagshipConstructorScreen>();
        if (screen == null)
        {
            screen = CreateConstructorRoot();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject = screen.gameObject;
        Debug.Log("[WildWindFlagshipConstructor] Constructor preview scene opened: " + scene.path);
    }

    private static WildWindFlagshipConstructorScreen CreateConstructorRoot()
    {
        GameObject root = new GameObject(RootName);
        WildWindFlagshipConstructorScreen screen = root.AddComponent<WildWindFlagshipConstructorScreen>();
        screen.referenceResolution = new Vector2(1920f, 1080f);
        screen.flagshipRank = 3;
        screen.flagshipName = "Флагман R3 Горизонт";
        screen.RebuildEditableScreen();
        return screen;
    }

    private static void ConfigureCamera()
    {
        GameObject cameraObject = new GameObject("Flagship Constructor Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.024f, 0.030f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 10f;
    }

    private static void CreateEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            WildWindFlagshipConstructorScreen.EnsureEventSystem(existing.transform.parent);
            return;
        }

        WildWindFlagshipConstructorScreen.EnsureEventSystem(null);
    }

    private static void EnsureSceneFolder()
    {
        string folder = Path.GetDirectoryName(ConstructorPreviewScenePath);
        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
