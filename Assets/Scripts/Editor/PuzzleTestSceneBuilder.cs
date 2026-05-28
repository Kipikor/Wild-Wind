using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PuzzleTestSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/PuzzleTestScene.unity";

    public static void BuildPuzzleTestScene()
    {
        EnsureSceneFolder();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "PuzzleTestScene";

        Transform root = new GameObject("Wild Wind Puzzle Test Scene").transform;
        CreateCameraAndLight();
        CreateWorkshopProps(root);
        CreatePuzzleHub(root);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[WildWindPuzzles] Puzzle test scene built: " + ScenePath);
    }

    public static void OpenPuzzleTestScene()
    {
        if (!File.Exists(ScenePath))
        {
            BuildPuzzleTestScene();
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsureBuildSettings();
        Debug.Log("[WildWindPuzzles] Puzzle test scene opened: " + scene.path);
    }

    public static void UsePuzzleTestSceneForNextPlay()
    {
        if (!File.Exists(ScenePath))
        {
            BuildPuzzleTestScene();
        }

        WildWindEditorStartSceneGuard.UseSceneForNextPlay(ScenePath);
        Debug.Log("[WildWindPuzzles] Next Play Mode will start from: " + ScenePath);
    }

    private static void CreatePuzzleHub(Transform root)
    {
        GameObject hubObject = new GameObject("Wild Wind Puzzle Test Hub");
        hubObject.transform.SetParent(root, false);
        WildWindPuzzleTestHub hub = hubObject.AddComponent<WildWindPuzzleTestHub>();
        Selection.activeGameObject = hub.gameObject;
    }

    private static void CreateCameraAndLight()
    {
        GameObject cameraObject = new GameObject("Puzzle Test Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 3.4f, -8.5f);
        cameraObject.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.022f, 0.024f, 1f);
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 80f;

        GameObject lightObject = new GameObject("Puzzle Test Work Light");
        lightObject.transform.rotation = Quaternion.Euler(48f, 320f, 12f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.95f, 0.84f, 0.64f, 1f);
        light.intensity = 1.05f;
    }

    private static void CreateWorkshopProps(Transform root)
    {
        Material table = CreateMaterial("Puzzle Test Table", new Color(0.20f, 0.16f, 0.12f, 1f));
        Material metal = CreateMaterial("Puzzle Test Metal", new Color(0.18f, 0.22f, 0.23f, 1f));
        Material brass = CreateMaterial("Puzzle Test Brass", new Color(0.66f, 0.46f, 0.22f, 1f));
        Material glow = CreateMaterial("Puzzle Test Glow", new Color(0.24f, 0.86f, 1f, 1f));

        CreatePrimitive(root, "Workbench", PrimitiveType.Cube, new Vector3(0f, -1.2f, 0f), new Vector3(8.6f, 0.35f, 3.2f), table);
        CreatePrimitive(root, "Back Plate", PrimitiveType.Cube, new Vector3(0f, 1.2f, 1.1f), new Vector3(7.4f, 3.8f, 0.28f), metal);
        CreatePrimitive(root, "Left Pipe", PrimitiveType.Cylinder, new Vector3(-3.6f, 1.1f, 0.75f), new Vector3(0.18f, 2.6f, 0.18f), brass, new Vector3(0f, 0f, 0f));
        CreatePrimitive(root, "Right Pipe", PrimitiveType.Cylinder, new Vector3(3.4f, 0.9f, 0.75f), new Vector3(0.16f, 2.2f, 0.16f), brass, new Vector3(0f, 0f, 0f));
        CreatePrimitive(root, "Hanging Coil", PrimitiveType.Sphere, new Vector3(0f, 2.0f, 0.70f), new Vector3(1.1f, 0.18f, 1.1f), glow);

        for (int i = 0; i < 5; i++)
        {
            CreatePrimitive(root, "Bench Tool " + i, PrimitiveType.Cube, new Vector3(-2.8f + i * 0.75f, -0.86f, -0.20f), new Vector3(0.45f, 0.08f, 0.12f), i % 2 == 0 ? brass : metal);
        }
    }

    private static void CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        CreatePrimitive(parent, name, type, position, scale, material, Vector3.zero);
    }

    private static void CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3 euler)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localRotation = Quaternion.Euler(euler);
        obj.transform.localScale = scale;

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Material CreateMaterial(string name, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        AddSceneIfMissing(scenes, StartScreenSceneBuilder.ScenePath, true);
        AddSceneIfMissing(scenes, "Assets/Scenes/WildWindWorldScene.unity", true);
        AddSceneIfMissing(scenes, ScenePath, true);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == path)
            {
                return;
            }
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
