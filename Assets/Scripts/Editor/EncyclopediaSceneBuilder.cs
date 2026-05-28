using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#else
using UnityEngine.UI;
#endif

public static class EncyclopediaSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/WildWindEncyclopediaScene.unity";

    public static void BuildEncyclopediaScene()
    {
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "WildWindEncyclopediaScene";

        ConfigureCamera();
        CreateEventSystem();
        CreateEncyclopediaRoot();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        Debug.Log("[WildWindEncyclopedia] Сцена энциклопедии собрана: " + ScenePath + ". В Play Mode откроется черновая вики с поиском, категориями, CSV-записями, кораблями, технологиями и модулями.");
    }

    public static void AddEncyclopediaToCurrentScene()
    {
        CreateEventSystem();

        WildWindEncyclopedia existing = Object.FindFirstObjectByType<WildWindEncyclopedia>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[WildWindEncyclopedia] Энциклопедия уже есть в текущей сцене: " + existing.gameObject.name + ".");
            return;
        }

        WildWindEncyclopedia encyclopedia = CreateEncyclopediaRoot();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Selection.activeGameObject = encyclopedia.gameObject;
        Debug.Log("[WildWindEncyclopedia] Энциклопедия добавлена в текущую сцену. Она появится поверх игры в Play Mode.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }

    private static void ConfigureCamera()
    {
        GameObject cameraObject = new GameObject("Encyclopedia Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.028f, 0.040f, 1f);
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
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static WildWindEncyclopedia CreateEncyclopediaRoot()
    {
        GameObject encyclopediaObject = new GameObject("Wild Wind Encyclopedia");
        WildWindEncyclopedia encyclopedia = encyclopediaObject.AddComponent<WildWindEncyclopedia>();
        encyclopedia.configFolder = "Data/Config";
        encyclopedia.loadCsvConfigs = true;
        encyclopedia.maxVisibleEntries = 260;
        Selection.activeGameObject = encyclopediaObject;
        return encyclopedia;
    }
}
