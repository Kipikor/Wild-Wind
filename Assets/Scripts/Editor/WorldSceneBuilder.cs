using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/WildWindWorldScene.unity";
    private const string MaterialFolder = "Assets/Data/World/Materials";
    private const string VisualMaterialFolder = "Assets/Data/VisualTarget/Materials";
    private const string SpaceCloudWavesMaterialPath = VisualMaterialFolder + "/M_SpaceCloudWaves_CloudSea.mat";

    [MenuItem("Wild Wind/World/Build Final World Scene")]
    public static void BuildFinalWorldScene()
    {
        EnsureFolders();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "WildWindWorldScene";

        Transform root = new GameObject("Wild Wind World").transform;
        WildWindSettingsRoot settings = WorldSettingsPrefabBuilder.InstantiateSettingsPrefab(root);
        Transform focus = CreatePlayerFocus(root);
        WorldRegionRuntime runtime = CreateWorldRuntime(root, focus);

        CreateAtmosphereDefaults();
        Camera camera = CreateCameraAndLight(focus);
        CreateLocalStormSurface(root);
        CreateStarterIsland(root);
        CreateStarterShipProxy(root, focus);
        CreateWorldDataPreview(root, runtime, focus.position);
        WorldBubbleStreamer streamer = CreateWorldBubbleStreamer(root, runtime, focus);
        VisualPlayModeTuner tuner = CreateVisualTuner(root, focus);
        CreateDebugTravelController(root, focus, camera, tuner, streamer, settings != null ? settings.Controls : null);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        VisualAeroSceneSetup.ApplyToActiveScene();

        if (tuner != null)
        {
            tuner.ApplyNow();
            EditorUtility.SetDirty(tuner);
        }

        runtime.LogWorldSummary();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = runtime.gameObject;

        Debug.Log("[WorldScene] Final world scene built: " + ScenePath);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Data"))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Data/World"))
        {
            AssetDatabase.CreateFolder("Assets/Data", "World");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Data/World", "Materials");
        }
    }

    private static Transform CreatePlayerFocus(Transform root)
    {
        GameObject focusObject = new GameObject("Player Bubble Focus");
        focusObject.transform.SetParent(root, false);
        focusObject.transform.position = new Vector3(-900f, 2520f, -900f);
        return focusObject.transform;
    }

    private static WorldRegionRuntime CreateWorldRuntime(Transform root, Transform focus)
    {
        GameObject runtimeObject = new GameObject("World Region Runtime");
        runtimeObject.transform.SetParent(root, false);
        WorldRegionRuntime runtime = runtimeObject.AddComponent<WorldRegionRuntime>();
        runtime.ConfigureFinalRegion(focus);
        runtime.ConfigureDebugDraw(false, true);
        EditorUtility.SetDirty(runtime);
        return runtime;
    }

    private static void CreateAtmosphereDefaults()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.15f, 0.24f, 1f);
        RenderSettings.ambientIntensity = 0.34f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.24f, 0.32f, 0.48f, 1f);
        RenderSettings.fogStartDistance = 1200f;
        RenderSettings.fogEndDistance = 9000f;
    }

    private static Camera CreateCameraAndLight(Transform focus)
    {
        GameObject cameraObject = new GameObject("World Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(-1650f, 2740f, -1750f);
        cameraObject.transform.LookAt(focus.position + new Vector3(500f, 30f, 500f));

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 14000f;
        camera.clearFlags = CameraClearFlags.Skybox;

        GameObject lightObject = new GameObject("Moon Light");
        lightObject.transform.rotation = Quaternion.Euler(42f, 328f, 4f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.54f, 0.66f, 0.86f, 1f);
        light.intensity = 0.84f;

        return camera;
    }

    private static void CreateLocalStormSurface(Transform root)
    {
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
        surface.name = "Deadly Storm Surface Local Bubble";
        surface.transform.SetParent(root, false);
        surface.transform.position = Vector3.zero;
        surface.transform.localScale = new Vector3(1400f, 1f, 1400f);

        MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SpaceCloudWavesMaterialPath);
            if (material != null)
            {
                VisualPlayModeTuner.ApplyStormBottomMaterial(material);
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void CreateStarterIsland(Transform root)
    {
        Transform island = new GameObject("Capital Island Proxy - Greenhaven").transform;
        island.SetParent(root, false);
        island.position = new Vector3(0f, 2500f, 0f);

        Material rock = LoadVisualMaterial("M_Visual_Rock", new Color(0.16f, 0.21f, 0.27f));
        Material ground = LoadVisualMaterial("M_Visual_Ground", new Color(0.36f, 0.36f, 0.33f));
        Material wall = LoadVisualMaterial("M_Visual_Wall", new Color(0.39f, 0.37f, 0.33f));
        Material roof = LoadVisualMaterial("M_Visual_Roof", new Color(0.10f, 0.13f, 0.17f));
        Material lamp = LoadVisualMaterial("M_Visual_LampWarm", new Color(1f, 0.68f, 0.26f));

        CreatePrimitive("Island Rock", PrimitiveType.Cube, island, new Vector3(0f, -70f, 0f), new Vector3(980f, 140f, 720f), rock);
        CreatePrimitive("Landing Platform", PrimitiveType.Cube, island, new Vector3(0f, 12f, 0f), new Vector3(820f, 22f, 520f), ground);
        CreatePrimitive("Capital Warehouse", PrimitiveType.Cube, island, new Vector3(-120f, 72f, 30f), new Vector3(210f, 120f, 160f), wall);
        CreatePrimitive("Capital Roof", PrimitiveType.Cube, island, new Vector3(-120f, 142f, 30f), new Vector3(235f, 28f, 180f), roof);
        CreatePrimitive("Dock", PrimitiveType.Cube, island, new Vector3(-470f, 0f, -170f), new Vector3(360f, 18f, 90f), LoadVisualMaterial("M_Visual_Wood", new Color(0.40f, 0.27f, 0.16f)));

        for (int i = 0; i < 4; i++)
        {
            float x = -260f + i * 170f;
            CreatePrimitive("Warm Dock Lamp " + i, PrimitiveType.Sphere, island, new Vector3(x, 105f, -210f), new Vector3(22f, 22f, 22f), lamp);
        }
    }

    private static void CreateStarterShipProxy(Transform root, Transform focus)
    {
        Transform ship = new GameObject("Player Ship Proxy").transform;
        ship.SetParent(root, false);
        ship.position = focus.position;
        ship.rotation = Quaternion.Euler(0f, 42f, 0f);

        Material hull = LoadVisualMaterial("M_Visual_ShipHull", new Color(0.62f, 0.49f, 0.31f));
        Material band = LoadVisualMaterial("M_Visual_ShipBand", new Color(0.05f, 0.06f, 0.08f));
        Material wood = LoadVisualMaterial("M_Visual_WoodDark", new Color(0.20f, 0.14f, 0.10f));

        CreatePrimitive("Balloon", PrimitiveType.Capsule, ship, Vector3.zero, new Vector3(120f, 240f, 120f), hull, new Vector3(0f, 0f, 90f));
        CreatePrimitive("Keel Cabin", PrimitiveType.Cube, ship, new Vector3(0f, -115f, 0f), new Vector3(160f, 70f, 95f), wood);
        CreatePrimitive("Balloon Band Front", PrimitiveType.Cube, ship, new Vector3(-150f, 0f, 0f), new Vector3(16f, 220f, 220f), band);
        CreatePrimitive("Balloon Band Back", PrimitiveType.Cube, ship, new Vector3(150f, 0f, 0f), new Vector3(16f, 220f, 220f), band);
    }

    private static void CreateWorldDataPreview(Transform root, WorldRegionRuntime runtime, Vector3 focusPosition)
    {
        Transform preview = new GameObject("World Data Preview - nearby only").transform;
        preview.SetParent(root, false);

        Material cloudMaterial = CreateMaterial("M_World_Preview_Cloud", new Color(0.9f, 0.94f, 1f, 0.22f), true);
        Material resourceMaterial = CreateMaterial("M_World_Preview_Resource", new Color(0.15f, 0.75f, 1f, 0.38f), true);
        Material leviathanMaterial = CreateMaterial("M_World_Preview_Leviathan", new Color(0.70f, 0.18f, 1f, 0.28f), true);

        int cloudCount = 0;
        foreach (WorldRegionRuntime.WorldCloudFieldRecord cloud in runtime.CloudFields)
        {
            if (cloudCount >= 8) break;
            if (HorizontalDistance(cloud.centerMeters, focusPosition) > 11000f) continue;

            CreatePrimitive(
                "Data Cloud Field " + cloud.id,
                PrimitiveType.Sphere,
                preview,
                cloud.centerMeters,
                new Vector3(cloud.radiusMeters * 2f, Mathf.Max(80f, cloud.thicknessMeters), cloud.radiusMeters * 2f),
                cloudMaterial);
            cloudCount++;
        }

        int resourceCount = 0;
        foreach (WorldRegionRuntime.WorldResourceFieldRecord resource in runtime.ResourceFields)
        {
            if (resourceCount >= 5) break;
            if (HorizontalDistance(resource.centerMeters, focusPosition) > 15000f) continue;

            CreatePrimitive(
                "Data Resource Field " + resource.id,
                PrimitiveType.Cube,
                preview,
                resource.centerMeters,
                Vector3.one * Mathf.Max(120f, resource.radiusMeters * 0.38f),
                resourceMaterial);
            resourceCount++;
        }

        int leviathanCount = 0;
        foreach (WorldRegionRuntime.WorldLeviathanRegionRecord region in runtime.LeviathanRegions)
        {
            if (leviathanCount >= 2) break;
            if (HorizontalDistance(region.centerMeters, focusPosition) > 22000f) continue;

            CreatePrimitive(
                "Data Leviathan Region " + region.id,
                PrimitiveType.Sphere,
                preview,
                region.centerMeters,
                Vector3.one * Mathf.Max(400f, region.radiusMeters * 0.16f),
                leviathanMaterial);
            leviathanCount++;
        }
    }

    private static WorldBubbleStreamer CreateWorldBubbleStreamer(Transform root, WorldRegionRuntime runtime, Transform focus)
    {
        GameObject streamerObject = new GameObject("World Bubble Streamer");
        streamerObject.transform.SetParent(root, false);
        WorldBubbleStreamer streamer = streamerObject.AddComponent<WorldBubbleStreamer>();
        streamer.Configure(runtime, focus);
        EditorUtility.SetDirty(streamer);
        return streamer;
    }

    private static VisualPlayModeTuner CreateVisualTuner(Transform root, Transform focus)
    {
        GameObject tunerObject = new GameObject("Visual Play Mode Tuner");
        tunerObject.transform.SetParent(root, false);
        VisualPlayModeTuner tuner = tunerObject.AddComponent<VisualPlayModeTuner>();
        tuner.SetRuntimeView(
            focus,
            new Vector3(-1650f, 2740f, -1750f),
            new Vector3(-80f, 2560f, -80f),
            42f);
        EditorUtility.SetDirty(tuner);
        return tuner;
    }

    private static void CreateDebugTravelController(
        Transform root,
        Transform focus,
        Camera camera,
        VisualPlayModeTuner tuner,
        WorldBubbleStreamer streamer,
        WildWindControlSettings controls)
    {
        GameObject controllerObject = new GameObject("World Debug Travel Controller");
        controllerObject.transform.SetParent(root, false);
        WorldDebugTravelController controller = controllerObject.AddComponent<WorldDebugTravelController>();
        controller.Configure(focus, camera, tuner, streamer, controls);
        EditorUtility.SetDirty(controller);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        return CreatePrimitive(name, type, parent, localPosition, localScale, material, Vector3.zero);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, Vector3 euler)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = Quaternion.Euler(euler);
        primitive.transform.localScale = localScale;

        MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }

        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        return primitive;
    }

    private static Material LoadVisualMaterial(string name, Color fallback)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(VisualMaterialFolder + "/" + name + ".mat");
        return material != null ? material : CreateMaterial("M_World_Fallback_" + name, fallback, false);
    }

    private static Material CreateMaterial(string name, Color color, bool transparent)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetColor(material, color, transparent);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetColor(Material material, Color color, bool transparent)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (transparent)
        {
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
        }
        else
        {
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
        }
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);
        return Vector2.Distance(a2, b2);
    }
}
