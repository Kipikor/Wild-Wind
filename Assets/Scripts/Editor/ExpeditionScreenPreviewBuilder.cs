using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ExpeditionScreenPreviewBuilder
{
    public const string SelectionPreviewScenePath = "Assets/Scenes/ExpeditionSelectionPreview.unity";
    private const string RootName = "Wild Wind Expedition Selection Screen";

    [MenuItem("Wild Wind/Expeditions/Build Preview/1 Expedition Selection")]
    public static void BuildSelectionPreview()
    {
        EnsureSceneFolder();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ExpeditionSelectionPreview";

        ConfigureCamera();
        CreateEventSystem();
        WildWindExpeditionSelectionScreen screen = CreateSelectionScreenRoot();

        EditorSceneManager.SaveScene(scene, SelectionPreviewScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = screen.gameObject;
        Debug.Log("[WildWindExpeditions] Expedition selection preview scene built: " + SelectionPreviewScenePath);
    }

    [MenuItem("Wild Wind/Expeditions/Open Preview/1 Expedition Selection")]
    public static void OpenSelectionPreview()
    {
        if (!File.Exists(SelectionPreviewScenePath))
        {
            BuildSelectionPreview();
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(SelectionPreviewScenePath, OpenSceneMode.Single);
        WildWindExpeditionSelectionScreen screen = Object.FindFirstObjectByType<WildWindExpeditionSelectionScreen>();
        if (screen == null)
        {
            screen = CreateSelectionScreenRoot();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Selection.activeGameObject = screen.gameObject;
        Debug.Log("[WildWindExpeditions] Expedition selection preview scene opened: " + scene.path);
    }

    private static WildWindExpeditionSelectionScreen CreateSelectionScreenRoot()
    {
        GameObject root = new GameObject(RootName);
        WildWindExpeditionSelectionScreen screen = root.AddComponent<WildWindExpeditionSelectionScreen>();
        screen.configFolder = "Data/Config";
        screen.loadCsvConfigs = true;
        screen.previewFlagshipRank = 3;
        screen.previewMoralePercent = 100f;
        screen.referenceResolution = new Vector2(1920f, 1080f);
        screen.RebuildEditableScreen();
        return screen;
    }

    private static void ConfigureCamera()
    {
        GameObject cameraObject = new GameObject("Expedition Preview Camera");
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
            WildWindExpeditionSelectionScreen.EnsureEventSystem(existing.transform.parent);
            return;
        }

        WildWindExpeditionSelectionScreen.EnsureEventSystem(null);
    }

    private static void EnsureSceneFolder()
    {
        string folder = Path.GetDirectoryName(SelectionPreviewScenePath);
        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
    }
}
