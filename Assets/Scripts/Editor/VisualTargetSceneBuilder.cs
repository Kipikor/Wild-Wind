using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VisualTargetSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/VisualTargetScene.unity";
    private const string AltitudeTestScenePath = "Assets/Scenes/VisualAltitudeCompositionScene.unity";
    private const string MaterialFolder = "Assets/Data/VisualTarget/Materials";
    private const int TrueCloudLayer = 8;
    private const string TrueCloudMaterialPath = "Assets/TrueClouds/ExampleScenes/Materials/CloudMaterial.mat";
    private const string SpaceCloudWavesSourceMaterialPath = "Assets/ShadowVision/SpaceCloudWaves/Examples/SpaceCloudWaves/SpaceCloudWaves.mat";
    private const string FogParticlePrefabPath = "Assets/Fog Particles/Prefabs/Bluish Fog.prefab";
    private const string FogParticleWhiteMaterialPath = "Assets/Fog Particles/Material/Fog-Material White.mat";

    public static void BuildVisualTargetScene()
    {
        EnsureFolders();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "VisualTargetScene";

        VisualPalette palette = CreatePalette();
        ConfigureAtmosphere(palette);
        CreateCameraAndLight();
        CreateDistantIslands(palette);
        CreateAtmosphericCloudBanks(palette);
        CreateFogParticleWisps();
        CreateCloudSea(palette);
        CreateIsland(palette);
        CreateShip(palette);
        CreateHud(palette);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        VisualAeroSceneSetup.ApplyToActiveScene();
        Debug.Log("[VisualTarget] Scene built with current visual setup: " + ScenePath);
    }

    [MenuItem("Wild Wind/Visual/Build Visual Scene")]
    public static void BuildAltitudeCompositionTestScene()
    {
        EnsureFolders();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "VisualAltitudeCompositionScene";

        VisualPalette palette = CreatePalette();
        ConfigureAtmosphere(palette);
        CreateCameraAndLight();
        ConfigureAltitudeTestCamera();
        CreateCloudSea(palette);
        Transform composition = CreateAltitudeTestComposition(palette);

        EditorSceneManager.SaveScene(scene, AltitudeTestScenePath);
        AssetDatabase.SaveAssets();
        VisualAeroSceneSetup.ApplyToActiveScene();
        ConfigureAltitudeCompositionTuner(composition);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[VisualTarget] Altitude composition test scene built: " + AltitudeTestScenePath);
    }

    public static void AddFogParticleWispsToCurrentScene()
    {
        GameObject existing = GameObject.Find("Fog Particle Wisps");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        CreateFogParticleWisps();
        VisualAeroSceneSetup.ApplyToActiveScene();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log("[VisualTarget] Fog Particle Wisps added to current scene.");
    }

    public static void RebuildAltitudeTestCloudsInActiveScene()
    {
        EnsureFolders();

        GameObject compositionObject = GameObject.Find("Altitude Test Composition");
        if (compositionObject == null)
        {
            Debug.LogWarning("[VisualTarget] Altitude Test Composition was not found. Build the test scene first.");
            return;
        }

        Transform composition = compositionObject.transform;
        DestroyNamedChild(composition, "Atmospheric Cloud Banks");
        DestroyNamedChild(composition, "Fog Particle Wisps");

        VisualPalette palette = CreatePalette();
        CreateAltitudeTestClouds(composition, palette);
        CreateAltitudeTestFogWisps(composition);
        VisualAeroSceneSetup.ApplyToActiveScene();

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrWhiteSpace(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log("[VisualTarget] Altitude test clouds rebuilt in active scene.");
    }

    private static void DestroyNamedChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
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

        if (!AssetDatabase.IsValidFolder("Assets/Data/VisualTarget"))
        {
            AssetDatabase.CreateFolder("Assets/Data", "VisualTarget");
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Data/VisualTarget", "Materials");
        }
    }

    private static VisualPalette CreatePalette()
    {
        return new VisualPalette
        {
            Rock = Material("M_Visual_Rock", new Color(0.16f, 0.21f, 0.27f)),
            RockDark = Material("M_Visual_RockDark", new Color(0.06f, 0.09f, 0.14f)),
            RockFar = Material("M_Visual_RockFar", new Color(0.05f, 0.09f, 0.17f)),
            Ground = Material("M_Visual_Ground", new Color(0.36f, 0.36f, 0.33f)),
            Wood = Material("M_Visual_Wood", new Color(0.40f, 0.27f, 0.16f)),
            WoodDark = Material("M_Visual_WoodDark", new Color(0.20f, 0.14f, 0.10f)),
            Wall = Material("M_Visual_Wall", new Color(0.39f, 0.37f, 0.33f)),
            Roof = Material("M_Visual_Roof", new Color(0.10f, 0.13f, 0.17f)),
            Metal = Material("M_Visual_Metal", new Color(0.13f, 0.14f, 0.15f)),
            ShipHull = Material("M_Visual_ShipHull", new Color(0.62f, 0.49f, 0.31f)),
            ShipBand = Material("M_Visual_ShipBand", new Color(0.05f, 0.06f, 0.08f)),
            Window = Material("M_Visual_WindowWarm", new Color(1.0f, 0.72f, 0.30f), true),
            Lamp = Material("M_Visual_LampWarm", new Color(1.0f, 0.68f, 0.26f), true),
            Crystal = Material("M_Visual_Crystal", new Color(0.0f, 0.78f, 1.0f), true),
            CloudDistant = Material("M_Visual_DistantCloud", new Color(0.82f, 0.84f, 0.86f, 0.34f), false, true),
            CloudLight = Material("M_Visual_DistantCloudLight", new Color(1f, 1f, 1f, 0.32f), false, true),
            TrueCloudVolume = TrueCloudMaterial("M_TrueClouds_VisualVolume", Color.white, 0.82f),
            CloudSea = SpaceCloudWavesMaterial("M_SpaceCloudWaves_CloudSea"),
            Panel = Material("M_Visual_HudPanel", new Color(0.025f, 0.04f, 0.06f, 0.86f), false, true),
            PanelSoft = Material("M_Visual_HudPanelSoft", new Color(0.035f, 0.055f, 0.075f, 0.72f), false, true)
        };
    }

    private static Material Material(string name, Color color, bool emission = false, bool transparent = false)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (shader != null && material.shader != shader)
        {
            material.shader = shader;
        }

        SetColor(material, color, emission, transparent);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material TrueCloudMaterial(string name, Color color, float noisePower)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material source = AssetDatabase.LoadAssetAtPath<Material>(TrueCloudMaterialPath);
        if (material == null)
        {
            material = source != null ? new Material(source) { name = name } : Material(name, color, false, false);
            if (!AssetDatabase.Contains(material))
            {
                AssetDatabase.CreateAsset(material, path);
            }
        }
        else if (source != null)
        {
            material.CopyPropertiesFromMaterial(source);
        }

        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_NoizePower")) material.SetFloat("_NoizePower", noisePower);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material SpaceCloudWavesMaterial(string name)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SpaceCloudWavesSourceMaterialPath);
        bool applyDefaults = false;
        if (material == null)
        {
            material = source != null
                ? new Material(source) { name = name }
                : Material(name, new Color(0.04f, 0.12f, 0.24f), false, false);
            applyDefaults = true;

            if (!AssetDatabase.Contains(material))
            {
                AssetDatabase.CreateAsset(material, path);
            }
        }
        else if (source != null && material.shader != source.shader)
        {
            material.shader = source.shader;
        }

        if (source == null)
        {
            Debug.LogWarning("[VisualTarget] Space Cloud Waves material was not found: " + SpaceCloudWavesSourceMaterialPath);
            return material;
        }

        if (!applyDefaults)
        {
            return material;
        }

        VisualPlayModeTuner.ApplyStormBottomMaterial(material);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureOpaqueWhiteFogParticleMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FogParticleWhiteMaterialPath);
        if (material == null)
        {
            Debug.LogWarning("[VisualTarget] White Fog Particles material was not found: " + FogParticleWhiteMaterialPath);
            return null;
        }

        SetMaterialColor(material, "_BaseColor", Color.white);
        SetMaterialColor(material, "_Color", Color.white);
        SetMaterialFloat(material, "_Surface", 0f);
        SetMaterialFloat(material, "_AlphaClip", 0f);
        SetMaterialFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        SetMaterialFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        SetMaterialFloat(material, "_ZWrite", 1f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetMaterialColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void SetColor(Material material, Color color, bool emission, bool transparent)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        if (emission)
        {
            Color emissionColor = color * 2.2f;
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (transparent)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private static void ConfigureAtmosphere(VisualPalette palette)
    {
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.14f, 0.21f, 0.32f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.20f, 0.29f);
    }

    private static void CreateCameraAndLight()
    {
        GameObject cameraObject = new GameObject("Visual Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.tag = "MainCamera";
        camera.backgroundColor = new Color(0.12f, 0.19f, 0.30f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.fieldOfView = 37.4f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 600f;
        cameraObject.transform.position = new Vector3(-24f, 10.5f, -25f);
        LookAt(cameraObject.transform, new Vector3(0.5f, 1.2f, 0f));

        GameObject moonObject = new GameObject("Moon Directional Light");
        Light moon = moonObject.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.48f, 0.58f, 0.74f);
        moon.intensity = 1.35f;
        moonObject.transform.rotation = Quaternion.Euler(44f, -38f, 5f);
    }

    private static void ConfigureAltitudeTestCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.farClipPlane = 12000f;
        camera.nearClipPlane = 0.1f;
        camera.fieldOfView = 37.4f;
        EditorUtility.SetDirty(camera);
    }

    private static void ConfigureAltitudeCompositionTuner(Transform composition)
    {
        VisualPlayModeTuner tuner = UnityEngine.Object.FindFirstObjectByType<VisualPlayModeTuner>();
        if (tuner == null)
        {
            GameObject tunerObject = new GameObject("Visual Play Mode Tuner");
            tuner = tunerObject.AddComponent<VisualPlayModeTuner>();
        }

        tuner.ConfigureCompositionRig(composition, true);
        EditorUtility.SetDirty(tuner);
    }

    private static void CreateDistantIslands(VisualPalette palette)
    {
        Transform root = new GameObject("Distant Island Silhouettes").transform;
        CreateFloatingRock("Far Island West", root, new Vector3(-43f, 2f, 48f), new Vector3(13f, 3.6f, 8f), palette.RockFar);
        CreatePrimitive("Far Tower West", PrimitiveType.Cube, root, new Vector3(-44f, 6.4f, 47f), new Vector3(0.9f, 4.5f, 0.9f), palette.RockFar);
        CreatePrimitive("Far Chimney West", PrimitiveType.Cube, root, new Vector3(-39f, 7f, 49f), new Vector3(0.5f, 5.2f, 0.5f), palette.RockFar);

        CreateFloatingRock("Far Island Center", root, new Vector3(-18f, 8.5f, 58f), new Vector3(10f, 3.4f, 7f), palette.RockFar);
        CreatePrimitive("Far Tower Center", PrimitiveType.Cube, root, new Vector3(-18f, 12.4f, 58f), new Vector3(0.8f, 4.2f, 0.8f), palette.RockFar);

        CreateFloatingRock("Far Island East", root, new Vector3(29f, 4.4f, 54f), new Vector3(7f, 2.8f, 4.8f), palette.RockFar);
        CreatePrimitive("Far Tower East", PrimitiveType.Cube, root, new Vector3(29f, 7.8f, 54f), new Vector3(0.55f, 3.4f, 0.55f), palette.RockFar);
    }

    private static void CreateAtmosphericCloudBanks(VisualPalette palette)
    {
        Transform root = new GameObject("Atmospheric Cloud Banks").transform;
        root.gameObject.layer = TrueCloudLayer;

        CreateCloudPuff(root, "Lower Cloud Left A", new Vector3(-25f, -4.8f, 14f), new Vector3(9f, 2.3f, 5.4f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Lower Cloud Left B", new Vector3(-18f, -5.4f, 20f), new Vector3(12f, 2.6f, 6.4f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Lower Cloud Center", new Vector3(-5f, -6.2f, 25f), new Vector3(15f, 2.8f, 7.0f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Lower Cloud Right", new Vector3(16f, -5.0f, 28f), new Vector3(13f, 2.5f, 6.2f), palette.TrueCloudVolume);

        CreateCloudPuff(root, "Horizon Cloud West", new Vector3(-38f, 4.8f, 62f), new Vector3(13f, 2.2f, 5.2f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Horizon Cloud Center", new Vector3(-12f, 6.2f, 70f), new Vector3(18f, 2.5f, 6.0f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Horizon Cloud East", new Vector3(22f, 5.4f, 66f), new Vector3(14f, 2.1f, 5.4f), palette.TrueCloudVolume);
    }

    private static void CreateCloudPuff(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject puff = CreatePrimitive(name, PrimitiveType.Sphere, parent, position, scale, material);
        puff.layer = TrueCloudLayer;
        puff.transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
    }

    private static void CreateFogParticleWisps()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogParticlePrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[VisualTarget] Fog Particles prefab was not found: " + FogParticlePrefabPath);
            return;
        }

        Transform root = new GameObject("Fog Particle Wisps").transform;
        CreateFogWisp(prefab, root, "Near Port Fog Wisp", new Vector3(-12f, 0.3f, -7f), new Vector3(0f, -18f, 0f), new Vector3(22f, 4f, 12f), 7f, 18f, 7f, -0.06f, 0.04f);
        CreateFogWisp(prefab, root, "Dock Fog Wisp", new Vector3(-7f, 1.5f, 1f), new Vector3(0f, 8f, 0f), new Vector3(18f, 3.2f, 8f), 5f, 13f, 5f, -0.04f, 0.02f);
        CreateFogWisp(prefab, root, "Lower Cloud Break A", new Vector3(-22f, -2.6f, 15f), new Vector3(0f, 22f, 0f), new Vector3(34f, 6f, 18f), 10f, 24f, 9f, 0.03f, -0.02f);
        CreateFogWisp(prefab, root, "Lower Cloud Break B", new Vector3(17f, -2.2f, 18f), new Vector3(0f, -28f, 0f), new Vector3(32f, 5f, 16f), 9f, 22f, 8f, -0.02f, 0.03f);
        CreateFogWisp(prefab, root, "Horizon Fog Veil", new Vector3(-3f, 3.8f, 48f), new Vector3(0f, 0f, 0f), new Vector3(58f, 7f, 12f), 12f, 30f, 10f, -0.08f, 0.01f);
    }

    private static void CreateFogWisp(
        GameObject prefab,
        Transform parent,
        string name,
        Vector3 position,
        Vector3 euler,
        Vector3 shapeScale,
        float startSizeMin,
        float startSizeMax,
        float emissionRate,
        float driftX,
        float driftZ)
    {
        GameObject wisp = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (wisp == null)
        {
            return;
        }

        wisp.name = name;
        wisp.transform.SetParent(parent, false);
        wisp.transform.localPosition = position;
        wisp.transform.localRotation = Quaternion.Euler(euler);
        wisp.transform.localScale = Vector3.one;

        ParticleSystem particleSystem = wisp.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = TwoConstants(10f, 22f);
        main.startSpeed = TwoConstants(0.02f, 0.18f);
        main.startSize = TwoConstants(startSizeMin, startSizeMax);
        main.startColor = TwoColors(new Color(0.88f, 0.90f, 0.94f, 0.46f), new Color(1f, 1f, 1f, 0.74f));
        main.maxParticles = 760;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = Constant(emissionRate);

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = shapeScale;

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = TwoConstants(driftX - 0.08f, driftX + 0.08f);
        velocity.y = TwoConstants(-0.015f, 0.045f);
        velocity.z = TwoConstants(driftZ - 0.08f, driftZ + 0.08f);

        ParticleSystemRenderer renderer = wisp.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Material whiteFogMaterial = EnsureOpaqueWhiteFogParticleMaterial();
            if (whiteFogMaterial != null)
            {
                renderer.sharedMaterial = whiteFogMaterial;
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.maxParticleSize = 0.35f;
            renderer.sortingFudge = -0.15f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static ParticleSystem.MinMaxCurve Constant(float value)
    {
        return new ParticleSystem.MinMaxCurve(value);
    }

    private static ParticleSystem.MinMaxCurve TwoConstants(float min, float max)
    {
        ParticleSystem.MinMaxCurve curve = new ParticleSystem.MinMaxCurve();
        curve.mode = ParticleSystemCurveMode.TwoConstants;
        curve.constantMin = min;
        curve.constantMax = max;
        return curve;
    }

    private static ParticleSystem.MinMaxGradient TwoColors(Color min, Color max)
    {
        ParticleSystem.MinMaxGradient gradient = new ParticleSystem.MinMaxGradient();
        gradient.mode = ParticleSystemGradientMode.TwoColors;
        gradient.colorMin = min;
        gradient.colorMax = max;
        return gradient;
    }

    private static void CreateCloudSea(VisualPalette palette)
    {
        Transform root = new GameObject("Cloud Sea").transform;
        root.localPosition = new Vector3(0f, -5.8f, 0f);

        GameObject surface = new GameObject("Space Cloud Waves Surface");
        surface.transform.SetParent(root, false);
        surface.transform.localPosition = new Vector3(0f, 0f, 36f);

        MeshFilter filter = surface.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateCloudSeaMesh(180f, 170f, 96, 72);

        MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = palette.CloudSea;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static Transform CreateAltitudeTestComposition(VisualPalette palette)
    {
        Transform root = new GameObject("Altitude Test Composition").transform;
        root.position = new Vector3(0f, 2500f, 0f);

        Transform island = new GameObject("Test Island").transform;
        island.SetParent(root, false);
        CreateFloatingRock("Test Island Rock Body", island, Vector3.zero, new Vector3(13.5f, 4.8f, 9f), palette.Rock);
        CreatePrimitive("Test Stone Platform", PrimitiveType.Cube, island, new Vector3(0f, 2.1f, 0f), new Vector3(13.2f, 0.5f, 8.2f), palette.Ground);
        CreateDock(island, palette);
        CreateFactory(island, palette);
        CreateWarehouse(island, palette);
        CreateCrane(island, palette);
        CreateCrystal(island, palette);
        CreateLamps(island, palette);

        CreateAltitudeTestShip(root, palette);
        CreateAltitudeTestClouds(root, palette);
        CreateAltitudeTestFogWisps(root);
        return root;
    }

    private static void CreateAltitudeTestShip(Transform parent, VisualPalette palette)
    {
        Transform ship = new GameObject("Test Player Airship").transform;
        ship.SetParent(parent, false);
        ship.localPosition = new Vector3(-17.5f, 3.15f, -6.8f);
        ship.localRotation = Quaternion.Euler(0f, 22f, 0f);

        CreatePrimitive("Balloon Body", PrimitiveType.Capsule, ship, Vector3.zero, new Vector3(1.45f, 2.7f, 1.45f), palette.ShipHull, new Vector3(0f, 0f, 90f));
        CreatePrimitive("Balloon Nose Band", PrimitiveType.Cube, ship, new Vector3(-1.95f, 0f, 0f), new Vector3(0.22f, 2.5f, 2.5f), palette.ShipBand);
        CreatePrimitive("Balloon Middle Band", PrimitiveType.Cube, ship, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 2.75f, 2.75f), palette.ShipBand);
        CreatePrimitive("Balloon Tail Band", PrimitiveType.Cube, ship, new Vector3(1.95f, 0f, 0f), new Vector3(0.22f, 2.5f, 2.5f), palette.ShipBand);

        CreatePrimitive("Gondola", PrimitiveType.Cube, ship, new Vector3(0f, -1.45f, 0f), new Vector3(2.2f, 0.9f, 1.15f), palette.WoodDark);
        CreateWindow(ship, palette, new Vector3(-0.55f, -1.45f, -0.6f), new Vector3(0.46f, 0.4f, 0.08f));
        CreateWindow(ship, palette, new Vector3(0.3f, -1.45f, -0.6f), new Vector3(0.46f, 0.4f, 0.08f));

        Transform propellerRoot = new GameObject("Propeller").transform;
        propellerRoot.SetParent(ship, false);
        propellerRoot.localPosition = new Vector3(-2.8f, -0.15f, 0f);
        CreatePrimitive("Propeller Hub", PrimitiveType.Cylinder, propellerRoot, Vector3.zero, new Vector3(0.22f, 0.18f, 0.22f), palette.Metal, new Vector3(90f, 0f, 0f));
        CreatePrimitive("Propeller Blade A", PrimitiveType.Cube, propellerRoot, Vector3.zero, new Vector3(0.16f, 1.5f, 0.1f), palette.WoodDark);
        CreatePrimitive("Propeller Blade B", PrimitiveType.Cube, propellerRoot, Vector3.zero, new Vector3(1.5f, 0.16f, 0.1f), palette.WoodDark);
    }

    private static void CreateAltitudeTestClouds(Transform parent, VisualPalette palette)
    {
        Transform root = new GameObject("Atmospheric Cloud Banks").transform;
        root.SetParent(parent, false);
        root.gameObject.layer = TrueCloudLayer;

        CreateCloudPuff(root, "Sky Fill Front Low", new Vector3(-20f, 28f, -72f), new Vector3(260f, 46f, 118f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Front High", new Vector3(28f, 98f, -42f), new Vector3(280f, 58f, 130f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Overhead A", new Vector3(-92f, 150f, 48f), new Vector3(310f, 68f, 160f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Overhead B", new Vector3(112f, 142f, 92f), new Vector3(330f, 70f, 170f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Center Sheet", new Vector3(0f, 96f, 156f), new Vector3(390f, 62f, 205f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Left Wall", new Vector3(-250f, 82f, 162f), new Vector3(290f, 58f, 210f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Right Wall", new Vector3(260f, 76f, 176f), new Vector3(300f, 58f, 220f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Horizon Low", new Vector3(-16f, 52f, 340f), new Vector3(520f, 74f, 240f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Horizon High", new Vector3(34f, 160f, 410f), new Vector3(560f, 94f, 260f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Sky Fill Far Blanket", new Vector3(0f, 124f, 680f), new Vector3(760f, 112f, 330f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Low Rolling Cloud Floor", new Vector3(14f, -18f, 110f), new Vector3(320f, 24f, 150f), palette.TrueCloudVolume);
        CreateCloudPuff(root, "Soft White Veil Around Island", new Vector3(0f, 32f, 34f), new Vector3(210f, 38f, 118f), palette.TrueCloudVolume);
        CreateAltitudeStressCloudField(root, palette);
    }

    private static void CreateAltitudeStressCloudField(Transform root, VisualPalette palette)
    {
        int index = 0;
        for (int ring = 0; ring < 9; ring++)
        {
            float radius = 620f + ring * 545f;
            int count = 14 + ring * 4;
            for (int i = 0; i < count; i++)
            {
                float normalized = i / (float)count;
                float angle = normalized * Mathf.PI * 2f + ring * 0.37f;
                float wobble = Mathf.Sin(i * 2.17f + ring * 1.31f) * 140f;
                float x = Mathf.Cos(angle) * (radius + wobble);
                float z = Mathf.Sin(angle) * (radius + wobble);
                float y = 35f + Mathf.Sin(i * 0.91f + ring * 0.43f) * 85f + ring * 26f;
                float width = 420f + ring * 56f + Mathf.Sin(i * 0.73f) * 90f;
                float height = 48f + ring * 8f + Mathf.Abs(Mathf.Sin(i * 1.19f)) * 48f;
                float depth = 230f + ring * 34f + Mathf.Cos(i * 0.61f) * 70f;

                CreateCloudPuff(
                    root,
                    "Stress TrueCloud Bank " + index,
                    new Vector3(x, y, z),
                    new Vector3(width, height, depth),
                    palette.TrueCloudVolume);
                index++;
            }
        }
    }

    private static void CreateAltitudeTestFogWisps(Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogParticlePrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[VisualTarget] Fog Particles prefab was not found: " + FogParticlePrefabPath);
            return;
        }

        Transform root = new GameObject("Fog Particle Wisps").transform;
        root.SetParent(parent, false);
        CreateFogWisp(prefab, root, "Near White Cloud Veil", new Vector3(-18f, 34f, -52f), new Vector3(0f, -10f, 0f), new Vector3(260f, 44f, 110f), 34f, 78f, 18f, -0.04f, 0.02f);
        CreateFogWisp(prefab, root, "Island White Cloud Wrap", new Vector3(4f, 34f, 26f), new Vector3(0f, 12f, 0f), new Vector3(220f, 42f, 120f), 32f, 74f, 17f, 0.03f, -0.02f);
        CreateFogWisp(prefab, root, "Mid White Cloud Drift", new Vector3(-28f, 78f, 138f), new Vector3(0f, 4f, 0f), new Vector3(360f, 48f, 170f), 46f, 104f, 20f, -0.02f, 0.02f);
        CreateFogWisp(prefab, root, "Far White Cloud Blanket", new Vector3(8f, 118f, 330f), new Vector3(0f, -3f, 0f), new Vector3(520f, 64f, 230f), 60f, 138f, 22f, -0.015f, 0.01f);
        CreateFogWisp(prefab, root, "High White Cloud Fibers", new Vector3(30f, 176f, 210f), new Vector3(0f, 20f, 0f), new Vector3(460f, 50f, 210f), 52f, 126f, 16f, 0.02f, -0.01f);
        CreateAltitudeStressFogField(prefab, root);
    }

    private static void CreateAltitudeStressFogField(GameObject prefab, Transform root)
    {
        for (int i = 0; i < 28; i++)
        {
            float normalized = i / 28f;
            float angle = normalized * Mathf.PI * 2f;
            float radius = 380f + (i % 7) * 610f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = 28f + (i % 5) * 42f;
            float yaw = normalized * 360f + 17f;
            Vector3 shapeScale = new Vector3(520f + (i % 4) * 110f, 58f + (i % 3) * 24f, 220f + (i % 5) * 70f);

            CreateFogWisp(
                prefab,
                root,
                "Stress White Fog Cloud " + i,
                new Vector3(x, y, z),
                new Vector3(0f, yaw, 0f),
                shapeScale,
                54f,
                132f,
                24f + (i % 4) * 3f,
                Mathf.Sin(i * 0.67f) * 0.04f,
                Mathf.Cos(i * 0.57f) * 0.04f);
        }
    }

    private static void CreateIsland(VisualPalette palette)
    {
        Transform island = new GameObject("Greenhaven Visual Island").transform;
        CreateFloatingRock("Greenhaven Rock Body", island, Vector3.zero, new Vector3(13.5f, 4.8f, 9f), palette.Rock);
        CreatePrimitive("Stone Platform", PrimitiveType.Cube, island, new Vector3(0f, 2.1f, 0f), new Vector3(13.2f, 0.5f, 8.2f), palette.Ground);

        CreateDock(island, palette);
        CreateFactory(island, palette);
        CreateWarehouse(island, palette);
        CreateCrane(island, palette);
        CreateCrystal(island, palette);
        CreateLamps(island, palette);
    }

    private static void CreateDock(Transform parent, VisualPalette palette)
    {
        for (int i = 0; i < 7; i++)
        {
            CreatePrimitive("Dock Plank " + i, PrimitiveType.Cube, parent, new Vector3(-7.1f - i * 0.65f, 2.25f, -0.6f), new Vector3(0.52f, 0.15f, 2.2f), palette.Wood);
        }

        CreatePrimitive("Dock Rail Left", PrimitiveType.Cube, parent, new Vector3(-9.2f, 2.75f, -1.75f), new Vector3(4.5f, 0.16f, 0.16f), palette.WoodDark);
        CreatePrimitive("Dock Rail Right", PrimitiveType.Cube, parent, new Vector3(-9.2f, 2.75f, 0.55f), new Vector3(4.5f, 0.16f, 0.16f), palette.WoodDark);
        for (int i = 0; i < 4; i++)
        {
            CreatePrimitive("Dock Pile " + i, PrimitiveType.Cube, parent, new Vector3(-7.4f - i * 1.2f, 1.45f, -1.85f), new Vector3(0.18f, 1.6f, 0.18f), palette.WoodDark);
            CreatePrimitive("Dock Pile R " + i, PrimitiveType.Cube, parent, new Vector3(-7.4f - i * 1.2f, 1.45f, 0.65f), new Vector3(0.18f, 1.6f, 0.18f), palette.WoodDark);
        }
    }

    private static void CreateFactory(Transform parent, VisualPalette palette)
    {
        CreatePrimitive("Factory Main", PrimitiveType.Cube, parent, new Vector3(-0.7f, 3.35f, 0.2f), new Vector3(2.7f, 2.5f, 2.6f), palette.Wall);
        CreatePrimitive("Factory Side", PrimitiveType.Cube, parent, new Vector3(1.45f, 3.0f, -0.7f), new Vector3(2.0f, 1.8f, 2.1f), palette.Wall);
        CreatePrimitive("Factory Roof Main", PrimitiveType.Cube, parent, new Vector3(-0.7f, 4.7f, 0.2f), new Vector3(2.9f, 0.24f, 2.8f), palette.Roof);
        CreatePrimitive("Factory Roof Side", PrimitiveType.Cube, parent, new Vector3(1.45f, 4.0f, -0.7f), new Vector3(2.2f, 0.22f, 2.3f), palette.Roof);
        CreatePrimitive("Chimney", PrimitiveType.Cylinder, parent, new Vector3(-0.95f, 5.95f, 0.55f), new Vector3(0.34f, 1.3f, 0.34f), palette.Metal);

        CreateSmoke(parent, palette, new Vector3(-0.95f, 7.1f, 0.55f));
        CreateWindow(parent, palette, new Vector3(-2.1f, 3.5f, -1.12f), new Vector3(0.08f, 0.52f, 0.45f));
        CreateWindow(parent, palette, new Vector3(-0.15f, 3.65f, -1.12f), new Vector3(0.08f, 0.52f, 0.45f));
        CreateWindow(parent, palette, new Vector3(2.48f, 3.15f, -0.75f), new Vector3(0.08f, 0.5f, 0.42f));
    }

    private static void CreateWarehouse(Transform parent, VisualPalette palette)
    {
        CreatePrimitive("Warehouse", PrimitiveType.Cube, parent, new Vector3(5.0f, 3.0f, 1.45f), new Vector3(2.6f, 1.8f, 2.0f), palette.Wall);
        CreatePrimitive("Warehouse Roof", PrimitiveType.Cube, parent, new Vector3(5.0f, 4.0f, 1.45f), new Vector3(2.9f, 0.24f, 2.25f), palette.Roof);
        CreateWindow(parent, palette, new Vector3(3.65f, 3.2f, 1.45f), new Vector3(0.08f, 0.52f, 0.45f));

        CreatePrimitive("Cargo Crate A", PrimitiveType.Cube, parent, new Vector3(2.65f, 2.65f, 0.65f), new Vector3(0.9f, 0.7f, 0.9f), palette.Wood);
        CreatePrimitive("Cargo Crate B", PrimitiveType.Cube, parent, new Vector3(3.55f, 2.55f, -0.8f), new Vector3(0.8f, 0.55f, 0.8f), palette.Wood);
        CreatePrimitive("Cargo Crate C", PrimitiveType.Cube, parent, new Vector3(1.75f, 2.55f, -1.4f), new Vector3(0.75f, 0.55f, 0.75f), palette.Wood);
    }

    private static void CreateCrane(Transform parent, VisualPalette palette)
    {
        CreatePrimitive("Crane Base", PrimitiveType.Cube, parent, new Vector3(3.6f, 2.7f, -2.65f), new Vector3(1.55f, 0.9f, 1.55f), palette.WoodDark);
        CreatePrimitive("Crane Tower", PrimitiveType.Cube, parent, new Vector3(3.6f, 4.0f, -2.65f), new Vector3(0.62f, 2.0f, 0.62f), palette.WoodDark);
        CreatePrimitive("Crane Cabin", PrimitiveType.Cube, parent, new Vector3(4.4f, 4.55f, -2.65f), new Vector3(1.0f, 0.82f, 0.9f), palette.Wall);
        CreatePrimitive("Crane Boom", PrimitiveType.Cube, parent, new Vector3(2.25f, 5.1f, -2.65f), new Vector3(3.7f, 0.26f, 0.24f), palette.WoodDark, new Vector3(0f, 0f, -30f));
        CreatePrimitive("Crane Cable", PrimitiveType.Cube, parent, new Vector3(0.8f, 4.2f, -2.65f), new Vector3(0.07f, 1.6f, 0.07f), palette.Metal);
        CreatePrimitive("Crane Hook", PrimitiveType.Cube, parent, new Vector3(0.8f, 3.35f, -2.65f), new Vector3(0.28f, 0.28f, 0.18f), palette.Metal);
    }

    private static void CreateCrystal(Transform parent, VisualPalette palette)
    {
        GameObject crystal = new GameObject("Claudium Crystal");
        crystal.transform.SetParent(parent, false);
        crystal.transform.localPosition = new Vector3(0f, -1.85f, 1.2f);
        crystal.transform.localScale = new Vector3(0.9f, 1.9f, 0.9f);
        MeshFilter filter = crystal.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateDiamondMesh();
        crystal.AddComponent<MeshRenderer>().sharedMaterial = palette.Crystal;

        Light glow = crystal.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(0.0f, 0.72f, 1.0f);
        glow.intensity = 5.5f;
        glow.range = 7f;

        for (int i = 0; i < 5; i++)
        {
            GameObject shard = Object.Instantiate(crystal, parent);
            shard.name = "Claudium Shard " + i;
            shard.transform.localScale = new Vector3(0.22f, 0.7f, 0.22f);
            shard.transform.localPosition = new Vector3(-1.1f + i * 0.55f, -1.95f + (i % 2) * 0.25f, 1.0f + i * 0.16f);
            shard.transform.localRotation = Quaternion.Euler(10f * i, 30f * i, -12f * i);
            Object.DestroyImmediate(shard.GetComponent<Light>());
        }
    }

    private static void CreateLamps(Transform parent, VisualPalette palette)
    {
        CreateLamp(parent, palette, new Vector3(-3.8f, 3.0f, -2.6f));
        CreateLamp(parent, palette, new Vector3(-1.2f, 3.0f, -2.25f));
        CreateLamp(parent, palette, new Vector3(5.8f, 3.0f, -1.5f));
    }

    private static void CreateLamp(Transform parent, VisualPalette palette, Vector3 position)
    {
        CreatePrimitive("Lamp Post", PrimitiveType.Cube, parent, position + new Vector3(0f, -0.3f, 0f), new Vector3(0.12f, 1.2f, 0.12f), palette.Metal);
        CreatePrimitive("Lamp Glow", PrimitiveType.Sphere, parent, position + new Vector3(0f, 0.42f, 0f), new Vector3(0.22f, 0.22f, 0.22f), palette.Lamp);
        GameObject lightObject = new GameObject("Lamp Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = position + new Vector3(0f, 0.45f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.64f, 0.24f);
        light.intensity = 2.3f;
        light.range = 4.4f;
    }

    private static void CreateSmoke(Transform parent, VisualPalette palette, Vector3 origin)
    {
        Material smoke = Material("M_Visual_Smoke", new Color(0.42f, 0.48f, 0.55f, 0.55f), false, true);
        for (int i = 0; i < 5; i++)
        {
            Vector3 position = origin + new Vector3(i * 0.2f, i * 0.35f, i * 0.08f);
            CreatePrimitive("Smoke Puff " + i, PrimitiveType.Sphere, parent, position, Vector3.one * (0.5f + i * 0.15f), smoke);
        }
    }

    private static void CreateShip(VisualPalette palette)
    {
        Transform ship = new GameObject("Player Airship Visual Mock").transform;
        ship.position = new Vector3(-11.8f, 3.15f, -6.8f);
        ship.rotation = Quaternion.Euler(0f, 22f, 0f);

        CreatePrimitive("Balloon Body", PrimitiveType.Capsule, ship, Vector3.zero, new Vector3(1.45f, 2.7f, 1.45f), palette.ShipHull, new Vector3(0f, 0f, 90f));
        CreatePrimitive("Balloon Nose Band", PrimitiveType.Cube, ship, new Vector3(-1.95f, 0f, 0f), new Vector3(0.22f, 2.5f, 2.5f), palette.ShipBand);
        CreatePrimitive("Balloon Middle Band", PrimitiveType.Cube, ship, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 2.75f, 2.75f), palette.ShipBand);
        CreatePrimitive("Balloon Tail Band", PrimitiveType.Cube, ship, new Vector3(1.95f, 0f, 0f), new Vector3(0.22f, 2.5f, 2.5f), palette.ShipBand);

        CreatePrimitive("Gondola", PrimitiveType.Cube, ship, new Vector3(0f, -1.45f, 0f), new Vector3(2.2f, 0.9f, 1.15f), palette.WoodDark);
        CreateWindow(ship, palette, new Vector3(-0.55f, -1.45f, -0.6f), new Vector3(0.46f, 0.4f, 0.08f));
        CreateWindow(ship, palette, new Vector3(0.3f, -1.45f, -0.6f), new Vector3(0.46f, 0.4f, 0.08f));

        Transform propellerRoot = new GameObject("Propeller").transform;
        propellerRoot.SetParent(ship, false);
        propellerRoot.localPosition = new Vector3(-2.8f, -0.15f, 0f);
        CreatePrimitive("Propeller Hub", PrimitiveType.Cylinder, propellerRoot, Vector3.zero, new Vector3(0.22f, 0.18f, 0.22f), palette.Metal, new Vector3(90f, 0f, 0f));
        CreatePrimitive("Propeller Blade A", PrimitiveType.Cube, propellerRoot, Vector3.zero, new Vector3(0.16f, 1.5f, 0.1f), palette.WoodDark);
        CreatePrimitive("Propeller Blade B", PrimitiveType.Cube, propellerRoot, Vector3.zero, new Vector3(1.5f, 0.16f, 0.1f), palette.WoodDark);
    }

    private static void CreateHud(VisualPalette palette)
    {
        GameObject canvasObject = new GameObject("Visual Target HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = GetFont();
        Color text = new Color(0.92f, 0.78f, 0.55f);
        Color muted = new Color(0.72f, 0.65f, 0.54f);
        Color gold = new Color(1f, 0.67f, 0.22f);

        RectTransform islandPanel = Panel(canvasObject.transform, "Island Panel", new Vector2(28f, -20f), new Vector2(420f, 270f), Anchor.TopLeft, palette);
        TextLine(islandPanel, "Гринхейвен", new Vector2(90f, -34f), 28, text, font, TextAnchor.MiddleLeft, new Vector2(250f, 36f));
        TextLine(islandPanel, "Торговый узел", new Vector2(90f, -70f), 20, muted, font, TextAnchor.MiddleLeft, new Vector2(250f, 28f));
        TextLine(islandPanel, "⚓", new Vector2(45f, -49f), 52, gold, font, TextAnchor.MiddleCenter, new Vector2(70f, 70f));
        TextLine(islandPanel, "Репутация: Дружелюбный (40/100)", new Vector2(90f, -118f), 19, text, font, TextAnchor.MiddleLeft, new Vector2(300f, 28f));
        Progress(islandPanel, new Vector2(32f, -156f), new Vector2(360f, 13f), 0.40f, gold);
        TextLine(islandPanel, "Доставка: Ткань", new Vector2(32f, -202f), 19, text, font, TextAnchor.MiddleLeft, new Vector2(260f, 28f));
        TextLine(islandPanel, "- Доставьте 60 Ткани в столицу", new Vector2(32f, -236f), 17, text, font, TextAnchor.MiddleLeft, new Vector2(310f, 28f));
        TextLine(islandPanel, "20 / 60", new Vector2(315f, -236f), 18, text, font, TextAnchor.MiddleRight, new Vector2(80f, 28f));

        RectTransform questPanel = Panel(canvasObject.transform, "Quest Panel", new Vector2(-24f, -16f), new Vector2(350f, 130f), Anchor.TopRight, palette);
        TextLine(questPanel, "Отслеживать заказ", new Vector2(72f, -36f), 22, text, font, TextAnchor.MiddleLeft, new Vector2(240f, 28f));
        TextLine(questPanel, "Доставьте 60 Ткани в столицу", new Vector2(24f, -82f), 17, text, font, TextAnchor.MiddleLeft, new Vector2(265f, 26f));
        TextLine(questPanel, "20 / 60", new Vector2(255f, -110f), 19, text, font, TextAnchor.MiddleRight, new Vector2(70f, 24f));

        RectTransform cargoPanel = Panel(canvasObject.transform, "Cargo Panel", new Vector2(18f, 24f), new Vector2(300f, 265f), Anchor.BottomLeft, palette);
        TextLine(cargoPanel, "ГРУЗ", new Vector2(24f, -35f), 19, text, font, TextAnchor.MiddleLeft, new Vector2(100f, 26f));
        TextLine(cargoPanel, "80 / 200", new Vector2(178f, -35f), 20, text, font, TextAnchor.MiddleRight, new Vector2(90f, 26f));
        TextLine(cargoPanel, "□  Ткань                         20", new Vector2(28f, -88f), 20, text, font, TextAnchor.MiddleLeft, new Vector2(240f, 28f));
        TextLine(cargoPanel, "◇  Металл                      30", new Vector2(28f, -132f), 20, text, font, TextAnchor.MiddleLeft, new Vector2(240f, 28f));
        TextLine(cargoPanel, "×  Инструменты              10", new Vector2(28f, -176f), 20, text, font, TextAnchor.MiddleLeft, new Vector2(240f, 28f));
        TextLine(cargoPanel, "○  Топливо                    20", new Vector2(28f, -220f), 20, text, font, TextAnchor.MiddleLeft, new Vector2(240f, 28f));

        RectTransform statusPanel = Panel(canvasObject.transform, "Status Bar", new Vector2(0f, 26f), new Vector2(890f, 142f), Anchor.BottomCenter, palette);
        StatusCell(statusPanel, "СКОРОСТЬ", "42", "КМ/Ч", new Vector2(70f, -26f), text, font);
        StatusCell(statusPanel, "ВЫСОТА", "128", "М", new Vector2(240f, -26f), text, font);
        StatusCell(statusPanel, "ТОПЛИВО", "76%", "", new Vector2(420f, -26f), text, font);
        StatusCell(statusPanel, "КЛАУДИЙ", "62%", "", new Vector2(595f, -26f), text, font);
        StatusCell(statusPanel, "ГРУЗ", "80 / 200", "", new Vector2(760f, -26f), text, font);

        RectTransform controlsPanel = Panel(canvasObject.transform, "Controls Panel", new Vector2(-24f, 24f), new Vector2(230f, 290f), Anchor.BottomRight, palette);
        ControlLine(controlsPanel, "W", "Тяга", -38f, text, font);
        ControlLine(controlsPanel, "S", "Тормоз", -78f, text, font);
        ControlLine(controlsPanel, "A   D", "Крен", -118f, text, font);
        ControlLine(controlsPanel, "Space", "Поднять", -158f, text, font);
        ControlLine(controlsPanel, "L Shift", "Опустить", -198f, text, font);
        ControlLine(controlsPanel, "X", "Автопилот", -238f, text, font);

        CreateCompass(canvasObject.transform, text, font);
        CreateCrosshair(canvasObject.transform, gold);
    }

    private static void StatusCell(RectTransform parent, string title, string value, string suffix, Vector2 position, Color color, Font font)
    {
        TextLine(parent, title, position, 17, color, font, TextAnchor.MiddleCenter, new Vector2(130f, 22f));
        TextLine(parent, value, position + new Vector2(0f, -48f), 34, color, font, TextAnchor.MiddleCenter, new Vector2(140f, 38f));
        if (!string.IsNullOrWhiteSpace(suffix))
        {
            TextLine(parent, suffix, position + new Vector2(0f, -84f), 18, color, font, TextAnchor.MiddleCenter, new Vector2(120f, 24f));
        }
    }

    private static void ControlLine(RectTransform parent, string key, string label, float y, Color color, Font font)
    {
        TextLine(parent, key, new Vector2(26f, y), 17, color, font, TextAnchor.MiddleCenter, new Vector2(62f, 28f));
        TextLine(parent, label, new Vector2(96f, y), 18, color, font, TextAnchor.MiddleLeft, new Vector2(120f, 28f));
    }

    private static void CreateCompass(Transform canvas, Color color, Font font)
    {
        RectTransform compass = new GameObject("Compass").AddComponent<RectTransform>();
        compass.SetParent(canvas, false);
        compass.anchorMin = new Vector2(0.5f, 1f);
        compass.anchorMax = new Vector2(0.5f, 1f);
        compass.pivot = new Vector2(0.5f, 1f);
        compass.anchoredPosition = new Vector2(0f, -22f);
        compass.sizeDelta = new Vector2(620f, 64f);
        TextLine(compass, "W", new Vector2(210f, -22f), 24, color, font, TextAnchor.MiddleCenter, new Vector2(70f, 28f));
        TextLine(compass, "N", new Vector2(520f, -22f), 24, color, font, TextAnchor.MiddleCenter, new Vector2(70f, 28f));
        Image line = UiImage(compass, "Compass Line", new Color(0.85f, 0.62f, 0.32f, 0.48f));
        line.rectTransform.anchoredPosition = new Vector2(310f, -48f);
        line.rectTransform.sizeDelta = new Vector2(600f, 2f);
    }

    private static void CreateCrosshair(Transform canvas, Color color)
    {
        RectTransform root = new GameObject("Crosshair").AddComponent<RectTransform>();
        root.SetParent(canvas, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(50f, 50f);
        CrosshairBar(root, "Top", new Vector2(0f, 16f), new Vector2(3f, 12f), color);
        CrosshairBar(root, "Bottom", new Vector2(0f, -16f), new Vector2(3f, 12f), color);
        CrosshairBar(root, "Left", new Vector2(-16f, 0f), new Vector2(12f, 3f), color);
        CrosshairBar(root, "Right", new Vector2(16f, 0f), new Vector2(12f, 3f), color);
    }

    private static void CrosshairBar(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Image image = UiImage(parent, name, new Color(color.r, color.g, color.b, 0.85f));
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Anchor anchor, VisualPalette palette)
    {
        Image image = UiImage(parent, name, new Color(0.025f, 0.04f, 0.06f, 0.86f));
        RectTransform rect = image.rectTransform;
        SetAnchor(rect, anchor);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Progress(RectTransform parent, Vector2 position, Vector2 size, float value, Color fillColor)
    {
        Image background = UiImage(parent, "Progress Background", new Color(0.32f, 0.38f, 0.44f, 0.45f));
        background.rectTransform.anchorMin = new Vector2(0f, 1f);
        background.rectTransform.anchorMax = new Vector2(0f, 1f);
        background.rectTransform.pivot = new Vector2(0f, 1f);
        background.rectTransform.anchoredPosition = position;
        background.rectTransform.sizeDelta = size;

        Image fill = UiImage(background.rectTransform, "Progress Fill", fillColor);
        fill.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        fill.rectTransform.anchoredPosition = Vector2.zero;
        fill.rectTransform.sizeDelta = new Vector2(size.x * Mathf.Clamp01(value), size.y);
    }

    private static void TextLine(RectTransform parent, string text, Vector2 position, int size, Color color, Font font, TextAnchor anchor, Vector2 dimensions)
    {
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(parent, false);
        Text uiText = textObject.AddComponent<Text>();
        uiText.text = text;
        uiText.font = font;
        uiText.fontSize = size;
        uiText.color = color;
        uiText.alignment = anchor;
        uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform rect = uiText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
    }

    private static Image UiImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Font GetFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void SetAnchor(RectTransform rect, Anchor anchor)
    {
        switch (anchor)
        {
            case Anchor.TopLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                break;
            case Anchor.TopRight:
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                break;
            case Anchor.BottomLeft:
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                break;
            case Anchor.BottomRight:
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                break;
            case Anchor.BottomCenter:
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                break;
        }
    }

    private static void CreateWindow(Transform parent, VisualPalette palette, Vector3 position, Vector3 scale)
    {
        CreatePrimitive("Warm Window", PrimitiveType.Cube, parent, position, scale, palette.Window);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = position;
        gameObject.transform.localRotation = Quaternion.Euler(euler);
        gameObject.transform.localScale = scale;
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        return gameObject;
    }

    private static void CreateFloatingRock(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject rock = new GameObject(name);
        rock.transform.SetParent(parent, false);
        rock.transform.localPosition = position;
        rock.transform.localScale = scale;
        MeshFilter filter = rock.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateIslandMesh();
        rock.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Mesh CreateIslandMesh()
    {
        Vector3[] top =
        {
            new Vector3(-0.55f, 0.35f, -0.43f),
            new Vector3(-0.08f, 0.48f, -0.55f),
            new Vector3(0.48f, 0.38f, -0.36f),
            new Vector3(0.58f, 0.28f, 0.12f),
            new Vector3(0.28f, 0.35f, 0.44f),
            new Vector3(-0.34f, 0.30f, 0.48f),
            new Vector3(-0.62f, 0.24f, 0.08f)
        };

        Vector3 lower = new Vector3(0.05f, -0.58f, 0.04f);
        MeshBuilder builder = new MeshBuilder();
        for (int i = 1; i < top.Length - 1; i++)
        {
            builder.Triangle(top[0], top[i], top[i + 1]);
        }

        for (int i = 0; i < top.Length; i++)
        {
            Vector3 a = top[i];
            Vector3 b = top[(i + 1) % top.Length];
            builder.Triangle(a, b, lower);
        }

        return builder.ToMesh("Floating Island Mesh");
    }

    private static Mesh CreateDiamondMesh()
    {
        Vector3 top = new Vector3(0f, 0.6f, 0f);
        Vector3 bottom = new Vector3(0f, -0.7f, 0f);
        Vector3[] ring =
        {
            new Vector3(0.35f, 0f, 0f),
            new Vector3(0f, 0f, 0.35f),
            new Vector3(-0.35f, 0f, 0f),
            new Vector3(0f, 0f, -0.35f)
        };

        MeshBuilder builder = new MeshBuilder();
        for (int i = 0; i < ring.Length; i++)
        {
            Vector3 a = ring[i];
            Vector3 b = ring[(i + 1) % ring.Length];
            builder.Triangle(top, a, b);
            builder.Triangle(bottom, b, a);
        }

        return builder.ToMesh("Crystal Diamond Mesh");
    }

    private static Mesh CreateCloudSeaMesh(float width, float depth, int xSegments, int zSegments)
    {
        int xCount = xSegments + 1;
        int zCount = zSegments + 1;
        Vector3[] vertices = new Vector3[xCount * zCount];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[xSegments * zSegments * 6];

        for (int z = 0; z < zCount; z++)
        {
            float v = z / (float)zSegments;
            for (int x = 0; x < xCount; x++)
            {
                float u = x / (float)xSegments;
                int index = z * xCount + x;
                vertices[index] = new Vector3((u - 0.5f) * width, 0f, (v - 0.5f) * depth);
                uvs[index] = new Vector2(u, v);
            }
        }

        int triangle = 0;
        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int lowerLeft = z * xCount + x;
                int lowerRight = lowerLeft + 1;
                int upperLeft = lowerLeft + xCount;
                int upperRight = upperLeft + 1;

                triangles[triangle++] = lowerLeft;
                triangles[triangle++] = upperLeft;
                triangles[triangle++] = lowerRight;
                triangles[triangle++] = lowerRight;
                triangles[triangle++] = upperLeft;
                triangles[triangle++] = upperRight;
            }
        }

        Mesh mesh = new Mesh { name = "Space Cloud Waves Grid" };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void LookAt(Transform transform, Vector3 target)
    {
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private enum Anchor
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        BottomCenter
    }

    private sealed class VisualPalette
    {
        public Material Rock;
        public Material RockDark;
        public Material RockFar;
        public Material Ground;
        public Material Wood;
        public Material WoodDark;
        public Material Wall;
        public Material Roof;
        public Material Metal;
        public Material ShipHull;
        public Material ShipBand;
        public Material Window;
        public Material Lamp;
        public Material Crystal;
        public Material CloudDistant;
        public Material CloudLight;
        public Material TrueCloudVolume;
        public Material CloudSea;
        public Material Panel;
        public Material PanelSoft;
    }

    private sealed class MeshBuilder
    {
        private readonly System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        private readonly System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
