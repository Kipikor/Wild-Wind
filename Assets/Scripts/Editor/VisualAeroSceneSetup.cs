using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class VisualAeroSceneSetup
{
    private const string ScenePath = "Assets/Scenes/VisualTargetScene.unity";
    private const string AssetFolder = "Assets/Data/VisualTarget";
    private const string RendererPath = AssetFolder + "/VisualTargetUniversalRenderer.asset";
    private const string AeroSourceMaterialPath = "Assets/Mirza/AERO - Volumetric Fog & Mist/Materials/Volumetric Fog.mat";
    private const string AeroVisualMaterialPath = AssetFolder + "/M_AERO_VisualTravelFog.mat";
    private const string SkyboxSourcePath = "Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset.mat";
    private const string SkyboxVisualPath = AssetFolder + "/M_AllSky_VisualTravelSkybox.mat";
    private const string TrueCloudNoisePath = "Assets/TrueClouds/ExampleScenes/Scenes/Sky/CloudsNoize.png";
    private const int TrueCloudLayer = 8;
    private const int TrueCloudLightLayer = 10;

    [MenuItem("Wild Wind/Visual/Apply AERO To Visual Target Scene")]
    public static void ApplyToVisualTargetScene()
    {
        EnsureFolders();

        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogWarning("[VisualAERO] Scene not found. Build it first: Wild Wind/Visual/Build Visual Target Scene");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Material fogMaterial = EnsureAeroMaterial();
        int rendererIndex = EnsureVisualRenderer(fogMaterial);
        Material skyboxMaterial = ConfigureSceneAtmosphere();
        ConfigureCamera(rendererIndex);
        ConfigureTrueCloudCamera();
        ConfigureController(fogMaterial);
        ConfigurePlayModeTuner(fogMaterial, skyboxMaterial);

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VisualAERO] AERO fog and AllSky skybox applied to " + ScenePath);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }

        if (!AssetDatabase.IsValidFolder(AssetFolder))
        {
            AssetDatabase.CreateFolder("Assets/Data", "VisualTarget");
        }
    }

    private static Material EnsureAeroMaterial()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(AeroSourceMaterialPath);
        if (source == null)
        {
            Debug.LogWarning("[VisualAERO] AERO source material was not found: " + AeroSourceMaterialPath);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(AeroVisualMaterialPath);
        if (material == null)
        {
            material = new Material(source) { name = "M_AERO_VisualTravelFog" };
            AssetDatabase.CreateAsset(material, AeroVisualMaterialPath);
        }

        material.shader = source.shader;
        material.CopyPropertiesFromMaterial(source);
        TuneFogMaterial(material);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void TuneFogMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetFloat(material, "_Density", 0.0247f);
        SetFloat(material, "_Max_Distance", 39.8f);
        SetFloat(material, "_Steps", 18f);
        SetFloat(material, "_Remap_Min", 0.12f);
        SetFloat(material, "_Remap_Max", 0.92f);
        SetFloat(material, "_Scattering_Remap_Min", 0.55f);
        SetFloat(material, "_Scattering_Remap_Max", 0.98f);
        SetFloat(material, "_Anisotropy", 0.35f);
        SetFloat(material, "_Anisotropy_Blend", 0.25f);
        SetFloat(material, "_ADDITIONAL_LIGHTS", 0f);
        SetColor(material, "_Colour", new Color(0.18f, 0.28f, 0.43f, 0.24f));

        material.EnableKeyword("_MAIN_LIGHT");
        material.EnableKeyword("_AMBIENT_LIGHT");
        material.DisableKeyword("_ADDITIONAL_LIGHTS");
        material.EnableKeyword("_OUTPUT_COMPOSITE");
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static int EnsureVisualRenderer(Material fogMaterial)
    {
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (rendererData == null)
        {
            rendererData = CreateUniversalRendererData();
        }

        EnsureAeroRendererFeature(rendererData, fogMaterial);

        UniversalRenderPipelineAsset urpAsset = GetActiveUrpAsset();
        if (urpAsset == null)
        {
            Debug.LogWarning("[VisualAERO] Active URP asset not found. Camera renderer index was not assigned.");
            return -1;
        }

        SerializedObject serializedAsset = new SerializedObject(urpAsset);
        SerializedProperty rendererList = serializedAsset.FindProperty("m_RendererDataList");
        if (rendererList == null)
        {
            Debug.LogWarning("[VisualAERO] URP renderer list was not found.");
            return -1;
        }

        int rendererIndex = FindRendererIndex(rendererList, rendererData);
        if (rendererIndex < 0)
        {
            rendererIndex = rendererList.arraySize;
            rendererList.InsertArrayElementAtIndex(rendererIndex);
            rendererList.GetArrayElementAtIndex(rendererIndex).objectReferenceValue = rendererData;
        }

        SetBool(serializedAsset, "m_RequireDepthTexture", true);
        SetBool(serializedAsset, "m_RequireOpaqueTexture", true);
        serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urpAsset);
        return rendererIndex;
    }

    private static UniversalRendererData CreateUniversalRendererData()
    {
        UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        rendererData.name = "VisualTargetUniversalRenderer";
        AssetDatabase.CreateAsset(rendererData, RendererPath);
        EditorUtility.SetDirty(rendererData);
        return rendererData;
    }

    private static void EnsureAeroRendererFeature(UniversalRendererData rendererData, Material fogMaterial)
    {
        FullScreenPassRendererFeature feature = FindFeature<FullScreenPassRendererFeature>(rendererData, "AERO Volumetric Fog");
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = "AERO Volumetric Fog";
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);
        }

        feature.SetActive(true);
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
        feature.fetchColorBuffer = true;
        feature.requirements = ScriptableRenderPassInput.None;
        feature.passMaterial = fogMaterial;
        feature.passIndex = 0;
        feature.bindDepthStencilAttachment = false;

        UpdateRendererFeatureMap(rendererData);
        rendererData.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(rendererData);
    }

    private static T FindFeature<T>(UniversalRendererData rendererData, string featureName) where T : ScriptableRendererFeature
    {
        for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
        {
            if (rendererData.rendererFeatures[i] is T feature && feature.name == featureName)
            {
                return feature;
            }
        }

        return null;
    }

    private static void UpdateRendererFeatureMap(UniversalRendererData rendererData)
    {
        SerializedObject serializedRenderer = new SerializedObject(rendererData);
        SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        if (featureMap == null)
        {
            return;
        }

        featureMap.arraySize = rendererData.rendererFeatures.Count;
        for (int i = 0; i < rendererData.rendererFeatures.Count; i++)
        {
            ScriptableRendererFeature feature = rendererData.rendererFeatures[i];
            if (feature != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
            {
                featureMap.GetArrayElementAtIndex(i).longValue = localId;
            }
        }

        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
    }

    private static UniversalRenderPipelineAsset GetActiveUrpAsset()
    {
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset graphicsAsset)
        {
            return graphicsAsset;
        }

        if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qualityAsset)
        {
            return qualityAsset;
        }

        return null;
    }

    private static int FindRendererIndex(SerializedProperty rendererList, ScriptableRendererData rendererData)
    {
        for (int i = 0; i < rendererList.arraySize; i++)
        {
            if (rendererList.GetArrayElementAtIndex(i).objectReferenceValue == rendererData)
            {
                return i;
            }
        }

        return -1;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static Material ConfigureSceneAtmosphere()
    {
        Material skybox = EnsureSkyboxMaterial();
        if (skybox != null)
        {
            RenderSettings.skybox = skybox;
        }
        else
        {
            Debug.LogWarning("[VisualAERO] Skybox was not found: " + SkyboxSourcePath);
        }

        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.16f, 0.28f);
        RenderSettings.ambientIntensity = 0.327f;

        Light moon = FindLight("Moon Directional Light");
        if (moon != null)
        {
            moon.color = new Color(0.54f, 0.66f, 0.86f);
            moon.intensity = 0.84f;
            moon.transform.rotation = Quaternion.Euler(42f, 328f, 4f);
            EditorUtility.SetDirty(moon);
        }

        return skybox;
    }

    private static Material EnsureSkyboxMaterial()
    {
        Material source = AssetDatabase.LoadAssetAtPath<Material>(SkyboxSourcePath);
        if (source == null)
        {
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SkyboxVisualPath);
        if (material == null)
        {
            material = new Material(source) { name = "M_AllSky_VisualTravelSkybox" };
            AssetDatabase.CreateAsset(material, SkyboxVisualPath);
        }

        material.shader = source.shader;
        material.CopyPropertiesFromMaterial(source);
        SetFloat(material, "_Exposure", 0.829f);
        SetFloat(material, "_Rotation", 189f);
        SetColor(material, "_Tint", new Color(0.48f, 0.58f, 0.82f, 0.5f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Light FindLight(string objectName)
    {
        GameObject lightObject = GameObject.Find(objectName);
        return lightObject != null ? lightObject.GetComponent<Light>() : null;
    }

    private static void ConfigureCamera(int rendererIndex)
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].clearFlags = CameraClearFlags.Skybox;
            cameras[i].backgroundColor = new Color(0.08f, 0.12f, 0.18f);
            cameras[i].allowHDR = true;

            UniversalAdditionalCameraData cameraData = cameras[i].GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            if (rendererIndex >= 0)
            {
                cameraData.SetRenderer(rendererIndex);
            }

            EditorUtility.SetDirty(cameras[i]);
            EditorUtility.SetDirty(cameraData);
        }
    }

    private static void ConfigureTrueCloudCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Type cloudCameraType = FindType("TrueClouds.CloudCamera3D");
        if (cloudCameraType == null)
        {
            Debug.LogWarning("[VisualAERO] TrueClouds CloudCamera3D type was not found.");
            return;
        }

        Component cloudCamera = camera.GetComponent(cloudCameraType);
        if (cloudCamera == null)
        {
            cloudCamera = camera.gameObject.AddComponent(cloudCameraType);
        }

        int cloudMask = 1 << TrueCloudLayer;
        int lightMask = 1 << TrueCloudLightLayer;
        camera.cullingMask &= ~(cloudMask | lightMask);

        SetField(cloudCamera, "CloudsMask", new LayerMask { value = cloudMask });
        SetField(cloudCamera, "LightMask", new LayerMask { value = lightMask });
        SetField(cloudCamera, "WorldBlockingMask", new LayerMask { value = ~(cloudMask | lightMask) });
        SetField(cloudCamera, "ResolutionDivider", 3);
        SetField(cloudCamera, "WorldDepthResolutionDivider", 2);
        SetEnumField(cloudCamera, "DepthPrecision", 5);
        SetField(cloudCamera, "LateCut", true);
        SetField(cloudCamera, "BlurRadius", 16f);
        SetEnumField(cloudCamera, "BlurQuality", 5);
        SetField(cloudCamera, "LateCutThreshohld", 0.02f);
        SetField(cloudCamera, "LateCutPower", 1.45f);
        SetField(cloudCamera, "UseDepthFiltering", true);
        SetField(cloudCamera, "DepthFilteringPower", 0.18f);
        SetField(cloudCamera, "UseNoise", true);
        SetField(cloudCamera, "Noise", AssetDatabase.LoadAssetAtPath<Texture2D>(TrueCloudNoisePath));
        SetField(cloudCamera, "Wind", new Vector3(-1.6f, -0.22f, -0.55f));
        SetField(cloudCamera, "NoiseScale", 4f);
        SetField(cloudCamera, "DepthNoiseScale", 5f);
        SetField(cloudCamera, "NormalNoisePower", 1.85f);
        SetField(cloudCamera, "DepthNoisePower", 0.36f);
        SetField(cloudCamera, "DisplacementNoisePower", 1.7f);
        SetField(cloudCamera, "NoiseSinTimeScale", 0.10f);
        SetField(cloudCamera, "DistanceToClouds", 32f);
        SetField(cloudCamera, "Light", FindLight("Moon Directional Light")?.transform);
        SetField(cloudCamera, "UseRamp", false);
        SetField(cloudCamera, "LightColor", new Color(0.62f, 0.74f, 0.94f));
        SetField(cloudCamera, "ShadowColor", new Color(0.08f, 0.14f, 0.24f));
        SetField(cloudCamera, "LightEnd", 0.64f);
        SetField(cloudCamera, "HaloPower", 2.2f);
        SetField(cloudCamera, "HaloDistance", 0.65f);
        SetField(cloudCamera, "FallbackDistance", 1.1f);

        SetField(cloudCamera, "blurFastShader", Shader.Find("Hidden/Clouds/BlurFast"));
        SetField(cloudCamera, "blurShader", Shader.Find("Hidden/Clouds/Blur"));
        SetField(cloudCamera, "blurHQShader", Shader.Find("Hidden/Clouds/BlurHQ"));
        SetField(cloudCamera, "depthBlurShader", Shader.Find("Hidden/Clouds/DepthBlur"));
        SetField(cloudCamera, "depthShader", Shader.Find("Hidden/Clouds/Depth"));
        SetField(cloudCamera, "cloudShader", Shader.Find("Hidden/Clouds/Clouds"));
        SetField(cloudCamera, "clearColorShader", Shader.Find("Hidden/Clouds/ClearColor"));

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(cloudCamera);
    }

    private static void ConfigureController(Material fogMaterial)
    {
        Type controllerType = FindType("Mirza.AERO.VolumetricFogController");
        if (controllerType == null)
        {
            Debug.LogWarning("[VisualAERO] VolumetricFogController type was not found.");
            return;
        }

        GameObject controllerObject = GameObject.Find("AERO Visual Fog Controller");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("AERO Visual Fog Controller");
        }

        Component controller = controllerObject.GetComponent(controllerType);
        if (controller == null)
        {
            controller = controllerObject.AddComponent(controllerType);
        }

        controllerType.GetField("material")?.SetValue(controller, fogMaterial);
        controllerType.GetField("additionalLightCountBase")?.SetValue(controller, 0);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigurePlayModeTuner(Material fogMaterial, Material skyboxMaterial)
    {
        GameObject tunerObject = GameObject.Find("Visual Play Mode Tuner");
        if (tunerObject == null)
        {
            tunerObject = new GameObject("Visual Play Mode Tuner");
        }

        VisualPlayModeTuner tuner = tunerObject.GetComponent<VisualPlayModeTuner>();
        if (tuner == null)
        {
            tuner = tunerObject.AddComponent<VisualPlayModeTuner>();
        }

        Transform cloudSea = FindTransform("Cloud Sea");
        tuner.ConfigureReferences(
            Camera.main,
            FindLight("Moon Directional Light"),
            fogMaterial,
            skyboxMaterial,
            FindTransform("Atmospheric Cloud Banks"),
            FindTransform("Fog Particle Wisps"),
            cloudSea,
            FindCloudSeaMaterial(cloudSea),
            FindTransform("Distant Island Silhouettes"));
        EditorUtility.SetDirty(tuner);
    }

    private static Material FindCloudSeaMaterial(Transform cloudSea)
    {
        if (cloudSea == null)
        {
            return null;
        }

        MeshRenderer renderer = cloudSea.GetComponentInChildren<MeshRenderer>(true);
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static Transform FindTransform(string objectName)
    {
        GameObject gameObject = GameObject.Find(objectName);
        return gameObject != null ? gameObject.transform : null;
    }

    private static void SetField(Component component, string fieldName, object value)
    {
        if (component == null)
        {
            return;
        }

        System.Reflection.FieldInfo field = component.GetType().GetField(fieldName);
        if (field == null)
        {
            return;
        }

        if (value == null && field.FieldType.IsValueType)
        {
            return;
        }

        field.SetValue(component, value);
    }

    private static void SetEnumField(Component component, string fieldName, int enumValue)
    {
        if (component == null)
        {
            return;
        }

        System.Reflection.FieldInfo field = component.GetType().GetField(fieldName);
        if (field == null || !field.FieldType.IsEnum)
        {
            return;
        }

        field.SetValue(component, Enum.ToObject(field.FieldType, enumValue));
    }

    private static Type FindType(string fullName)
    {
        System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
