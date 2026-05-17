using System.Globalization;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class VisualPlayModeTuner : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField, InspectorName("Камера")] private Camera visualCamera;
    [SerializeField, InspectorName("Лунный свет")] private Light moonLight;
    [SerializeField, InspectorName("Материал AERO")] private Material aeroFogMaterial;
    [SerializeField, InspectorName("Материал неба")] private Material skyboxMaterial;
    [SerializeField, InspectorName("Облака TrueClouds")] private Transform cloudRoot;
    [SerializeField, InspectorName("Облачное море")] private Transform cloudSeaRoot;
    [SerializeField, InspectorName("Материал облачного моря")] private Material cloudSeaMaterial;
    [SerializeField, InspectorName("Дальние острова")] private Transform distantIslandsRoot;

    [Header("Камера")]
    [SerializeField, InspectorName("Позиция камеры")] private Vector3 cameraPosition = new Vector3(-24f, 10.5f, -25f);
    [SerializeField, InspectorName("Точка взгляда")] private Vector3 cameraTarget = new Vector3(0.5f, 1.2f, 0f);
    [SerializeField, Range(20f, 70f), InspectorName("Угол обзора")] private float cameraFov = 37.4f;

    [Header("Небо")]
    [SerializeField, Range(0f, 2f), InspectorName("Экспозиция неба")] private float skyExposure = 0.829f;
    [SerializeField, Range(0f, 360f), InspectorName("Поворот неба")] private float skyRotation = 189f;
    [SerializeField, InspectorName("Оттенок неба")] private Color skyTint = new Color(0.48f, 0.58f, 0.82f, 0.5f);

    [Header("AERO туман")]
    [SerializeField, Range(0f, 0.03f), InspectorName("Плотность")] private float fogDensity = 0.0247f;
    [SerializeField, Range(10f, 260f), InspectorName("Дистанция")] private float fogMaxDistance = 39.8f;
    [SerializeField, Range(0f, 1f), InspectorName("Прозрачность")] private float fogAlpha = 0.24f;
    [SerializeField, InspectorName("Цвет")] private Color fogColor = new Color(0.18f, 0.28f, 0.43f, 1f);

    [Header("Свет")]
    [SerializeField, InspectorName("Цвет окружения")] private Color ambientColor = new Color(0.10f, 0.16f, 0.28f);
    [SerializeField, Range(0f, 2f), InspectorName("Сила окружения")] private float ambientIntensity = 0.327f;
    [SerializeField, InspectorName("Цвет луны")] private Color moonColor = new Color(0.54f, 0.66f, 0.86f);
    [SerializeField, Range(0f, 4f), InspectorName("Сила луны")] private float moonIntensity = 0.84f;
    [SerializeField, InspectorName("Поворот луны")] private Vector3 moonEuler = new Vector3(42f, 328f, 4f);

    [Header("Облачное море")]
    [SerializeField, InspectorName("Показывать облачное море")] private bool showCloudSea = true;
    [SerializeField, InspectorName("Управлять материалом из тюнера")] private bool overrideCloudSeaMaterial = false;
    [SerializeField, Range(-12f, 6f), InspectorName("Высота моря")] private float cloudSeaYOffset = -5.8f;
    [SerializeField, Range(0.25f, 2.5f), InspectorName("Масштаб моря")] private float cloudSeaScale = 1f;
    [SerializeField, ColorUsage(true, true), InspectorName("Тёмная масса")] private Color cloudSeaMainColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField, ColorUsage(true, true), InspectorName("Цвет глубины")] private Color cloudSeaFogColor = new Color(0f, 1.2850108f, 3.1194968f, 1f);
    [SerializeField, ColorUsage(true, true), InspectorName("Светящиеся гребни")] private Color cloudSeaHighlightColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField, Range(0f, 14f), InspectorName("Плотность альфы")] private float cloudSeaAlphaDensity = 0.2f;
    [SerializeField, Range(0f, 24f), InspectorName("Высота волн")] private float cloudSeaWaveHeight = 0.65f;
    [SerializeField, Range(0f, 8f), InspectorName("Скорость волн")] private float cloudSeaWaveSpeed = 6.44f;
    [SerializeField, Range(0f, 4f), InspectorName("Сила смещения")] private float cloudSeaOffsetStrength = 0.9f;

    [Header("Слои")]
    [SerializeField, InspectorName("Показывать облака")] private bool showClouds = true;
    [SerializeField, InspectorName("Показывать дальние острова")] private bool showDistantIslands = true;
    [SerializeField, InspectorName("Применять постоянно")] private bool applyContinuously = true;

    public void ConfigureReferences(
        Camera camera,
        Light moon,
        Material fogMaterial,
        Material skyMaterial,
        Transform clouds,
        Transform cloudSea,
        Material cloudSeaMat,
        Transform distantIslands)
    {
        visualCamera = camera;
        moonLight = moon;
        aeroFogMaterial = fogMaterial;
        skyboxMaterial = skyMaterial;
        cloudRoot = clouds;
        cloudSeaRoot = cloudSea;
        cloudSeaMaterial = cloudSeaMat;
        distantIslandsRoot = distantIslands;
        CaptureFromScene();
        ApplyNow();
    }

    [ContextMenu("Применить сейчас")]
    public void ApplyNow()
    {
        ApplyCamera();
        ApplySkybox();
        ApplyFog();
        ApplyLight();
        ApplyCloudSea();
        ApplyLayers();
    }

    [ContextMenu("Захватить текущую сцену")]
    public void CaptureFromScene()
    {
        if (visualCamera != null)
        {
            cameraPosition = visualCamera.transform.position;
            cameraFov = visualCamera.fieldOfView;
        }

        if (moonLight != null)
        {
            moonColor = moonLight.color;
            moonIntensity = moonLight.intensity;
            moonEuler = moonLight.transform.rotation.eulerAngles;
        }

        ambientColor = RenderSettings.ambientLight;
        ambientIntensity = RenderSettings.ambientIntensity;

        if (skyboxMaterial != null)
        {
            skyExposure = GetFloat(skyboxMaterial, "_Exposure", skyExposure);
            skyRotation = GetFloat(skyboxMaterial, "_Rotation", skyRotation);
            skyTint = GetColor(skyboxMaterial, "_Tint", skyTint);
        }

        if (aeroFogMaterial != null)
        {
            fogDensity = GetFloat(aeroFogMaterial, "_Density", fogDensity);
            fogMaxDistance = GetFloat(aeroFogMaterial, "_Max_Distance", fogMaxDistance);
            Color colour = GetColor(aeroFogMaterial, "_Colour", fogColor);
            fogColor = new Color(colour.r, colour.g, colour.b, 1f);
            fogAlpha = colour.a;
        }

        if (cloudSeaRoot != null)
        {
            cloudSeaYOffset = cloudSeaRoot.localPosition.y;
            cloudSeaScale = cloudSeaRoot.localScale.x;
        }

        if (cloudSeaMaterial != null)
        {
            cloudSeaMainColor = GetColor(cloudSeaMaterial, "_MainColor", cloudSeaMainColor);
            cloudSeaFogColor = GetColor(cloudSeaMaterial, "_FogColor", cloudSeaFogColor);
            cloudSeaHighlightColor = GetColor(cloudSeaMaterial, "_HighlightColor", cloudSeaHighlightColor);
            cloudSeaAlphaDensity = GetFloat(cloudSeaMaterial, "_AlphaDensity", cloudSeaAlphaDensity);
            cloudSeaWaveHeight = GetFloat(cloudSeaMaterial, "_WaveMaxHeight", cloudSeaWaveHeight);
            cloudSeaWaveSpeed = GetFloat(cloudSeaMaterial, "_WaveSpeed", cloudSeaWaveSpeed);
            cloudSeaOffsetStrength = GetFloat(cloudSeaMaterial, "_OffsetStrength", cloudSeaOffsetStrength);
        }
    }

    [ContextMenu("Скопировать настройки для Codex")]
    public void CopySettingsForCodex()
    {
        string text = BuildSettingsText();
#if UNITY_EDITOR
        EditorGUIUtility.systemCopyBuffer = text;
#endif
        Debug.Log(text, this);
    }

    [ContextMenu("Пресет: грозовое дно")]
    public void ApplyStormBottomPreset()
    {
        ResolveMissingReferences();

        showCloudSea = true;
        overrideCloudSeaMaterial = false;
        cloudSeaYOffset = -5.8f;
        cloudSeaScale = 1f;
        cloudSeaMainColor = new Color(0f, 0f, 0f, 0f);
        cloudSeaFogColor = new Color(0f, 1.2850108f, 3.1194968f, 1f);
        cloudSeaHighlightColor = new Color(0f, 0f, 0f, 0f);
        cloudSeaAlphaDensity = 0.2f;
        cloudSeaWaveHeight = 0.65f;
        cloudSeaWaveSpeed = 6.44f;
        cloudSeaOffsetStrength = 0.9f;

        ApplyStormBottomMaterial(cloudSeaMaterial);
        ApplyNow();
        MarkDirty(this);
    }

    public static void ApplyStormBottomMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetColor(material, "_MainColor", new Color(0f, 0f, 0f, 0f));
        SetColor(material, "_FogColor", new Color(0f, 1.2850108f, 3.1194968f, 1f));
        SetColor(material, "_HighlightColor", new Color(0f, 0f, 0f, 0f));
        SetFloat(material, "_AlphaClipThreshold", 0f);
        SetFloat(material, "_AlphaDensity", 0.2f);
        SetFloat(material, "_DiffuseDistanceFalloff", 0f);
        SetFloat(material, "_DiffuseScrollSpeed", 0f);
        SetFloat(material, "_FadeDistance", 303.3f);
        SetFloat(material, "_FadeTransition", 3f);
        SetFloat(material, "_FogDistance", 16750f);
        SetFloat(material, "_FogDistortionMagnitude", 180.94f);
        SetFloat(material, "_FogHeightPower", 62.9f);
        SetFloat(material, "_FogScrollSpeed", 0.01f);
        SetFloat(material, "_HighlightDistortionMagnitude", 0.75f);
        SetFloat(material, "_HighlightHeightPower", 5.2f);
        SetFloat(material, "_HighlightNormalStrength", 0.45f);
        SetFloat(material, "_OffsetScale", 11.9f);
        SetFloat(material, "_OffsetSpeed", 0.08f);
        SetFloat(material, "_OffsetStrength", 0.9f);
        SetFloat(material, "_ParalaxOffset", 0f);
        SetFloat(material, "_ParallaxNormalStrength", 0f);
        SetFloat(material, "_ReceiveShadows", 1f);
        SetFloat(material, "_WaveDistance", 0.26f);
        SetFloat(material, "_WaveMaxHeight", 0.65f);
        SetFloat(material, "_WaveSpeed", 6.44f);
        SetFloat(material, "_WaveStrength", 0.73f);
        MarkDirty(material);
    }

    private void OnEnable()
    {
        ResolveMissingReferences();
        ApplyNow();
    }

    private void OnValidate()
    {
        ResolveMissingReferences();
        if (applyContinuously)
        {
            ApplyNow();
        }
    }

    private void Update()
    {
        if (applyContinuously)
        {
            ApplyNow();
        }
    }

    private void ResolveMissingReferences()
    {
        if (visualCamera == null)
        {
            visualCamera = Camera.main;
        }

        if (moonLight == null)
        {
            GameObject moon = GameObject.Find("Moon Directional Light");
            moonLight = moon != null ? moon.GetComponent<Light>() : null;
        }

        if (cloudRoot == null)
        {
            GameObject clouds = GameObject.Find("Atmospheric Cloud Banks");
            cloudRoot = clouds != null ? clouds.transform : null;
        }

        if (cloudSeaRoot == null)
        {
            GameObject cloudSea = GameObject.Find("Cloud Sea");
            cloudSeaRoot = cloudSea != null ? cloudSea.transform : null;
        }

        if (cloudSeaMaterial == null && cloudSeaRoot != null)
        {
            MeshRenderer renderer = cloudSeaRoot.GetComponentInChildren<MeshRenderer>(true);
            cloudSeaMaterial = renderer != null ? renderer.sharedMaterial : null;
        }

        if (distantIslandsRoot == null)
        {
            GameObject islands = GameObject.Find("Distant Island Silhouettes");
            distantIslandsRoot = islands != null ? islands.transform : null;
        }
    }

    private void ApplyCamera()
    {
        if (visualCamera == null)
        {
            return;
        }

        visualCamera.transform.position = cameraPosition;
        Vector3 direction = cameraTarget - cameraPosition;
        if (direction.sqrMagnitude > 0.001f)
        {
            visualCamera.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        visualCamera.fieldOfView = cameraFov;
    }

    private void ApplySkybox()
    {
        if (skyboxMaterial == null)
        {
            return;
        }

        RenderSettings.skybox = skyboxMaterial;
        SetFloat(skyboxMaterial, "_Exposure", skyExposure);
        SetFloat(skyboxMaterial, "_Rotation", skyRotation);
        SetColor(skyboxMaterial, "_Tint", skyTint);
        MarkDirty(skyboxMaterial);
    }

    private void ApplyFog()
    {
        if (aeroFogMaterial == null)
        {
            return;
        }

        SetFloat(aeroFogMaterial, "_Density", fogDensity);
        SetFloat(aeroFogMaterial, "_Max_Distance", fogMaxDistance);
        SetFloat(aeroFogMaterial, "_ADDITIONAL_LIGHTS", 0f);
        SetColor(aeroFogMaterial, "_Colour", new Color(fogColor.r, fogColor.g, fogColor.b, fogAlpha));
        aeroFogMaterial.DisableKeyword("_ADDITIONAL_LIGHTS");
        MarkDirty(aeroFogMaterial);
    }

    private void ApplyLight()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;

        if (moonLight == null)
        {
            return;
        }

        moonLight.color = moonColor;
        moonLight.intensity = moonIntensity;
        moonLight.transform.rotation = Quaternion.Euler(moonEuler);
    }

    private void ApplyCloudSea()
    {
        if (cloudSeaRoot != null)
        {
            cloudSeaRoot.gameObject.SetActive(showCloudSea);
            cloudSeaRoot.localPosition = new Vector3(0f, cloudSeaYOffset, 0f);
            cloudSeaRoot.localScale = Vector3.one * cloudSeaScale;
        }

        if (!overrideCloudSeaMaterial || cloudSeaMaterial == null)
        {
            return;
        }

        SetColor(cloudSeaMaterial, "_MainColor", cloudSeaMainColor);
        SetColor(cloudSeaMaterial, "_FogColor", cloudSeaFogColor);
        SetColor(cloudSeaMaterial, "_HighlightColor", cloudSeaHighlightColor);
        SetFloat(cloudSeaMaterial, "_AlphaDensity", cloudSeaAlphaDensity);
        SetFloat(cloudSeaMaterial, "_WaveMaxHeight", cloudSeaWaveHeight);
        SetFloat(cloudSeaMaterial, "_WaveSpeed", cloudSeaWaveSpeed);
        SetFloat(cloudSeaMaterial, "_OffsetStrength", cloudSeaOffsetStrength);
        MarkDirty(cloudSeaMaterial);
    }

    private void ApplyLayers()
    {
        if (cloudRoot != null)
        {
            cloudRoot.gameObject.SetActive(showClouds);
        }

        if (distantIslandsRoot != null)
        {
            distantIslandsRoot.gameObject.SetActive(showDistantIslands);
        }
    }

    private string BuildSettingsText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[VisualPlayModeTuner]");
        builder.AppendLine("cameraPosition=" + Format(cameraPosition));
        builder.AppendLine("cameraTarget=" + Format(cameraTarget));
        builder.AppendLine("cameraFov=" + Format(cameraFov));
        builder.AppendLine("skyExposure=" + Format(skyExposure));
        builder.AppendLine("skyRotation=" + Format(skyRotation));
        builder.AppendLine("skyTint=" + Format(skyTint));
        builder.AppendLine("fogDensity=" + Format(fogDensity));
        builder.AppendLine("fogMaxDistance=" + Format(fogMaxDistance));
        builder.AppendLine("fogAlpha=" + Format(fogAlpha));
        builder.AppendLine("fogColor=" + Format(fogColor));
        builder.AppendLine("ambientColor=" + Format(ambientColor));
        builder.AppendLine("ambientIntensity=" + Format(ambientIntensity));
        builder.AppendLine("moonColor=" + Format(moonColor));
        builder.AppendLine("moonIntensity=" + Format(moonIntensity));
        builder.AppendLine("moonEuler=" + Format(moonEuler));
        builder.AppendLine("showClouds=" + showClouds);
        builder.AppendLine("showCloudSea=" + showCloudSea);
        builder.AppendLine("overrideCloudSeaMaterial=" + overrideCloudSeaMaterial);
        builder.AppendLine("cloudSeaYOffset=" + Format(cloudSeaYOffset));
        builder.AppendLine("cloudSeaScale=" + Format(cloudSeaScale));
        builder.AppendLine("cloudSeaMainColor=" + Format(cloudSeaMainColor));
        builder.AppendLine("cloudSeaFogColor=" + Format(cloudSeaFogColor));
        builder.AppendLine("cloudSeaHighlightColor=" + Format(cloudSeaHighlightColor));
        builder.AppendLine("cloudSeaAlphaDensity=" + Format(cloudSeaAlphaDensity));
        builder.AppendLine("cloudSeaWaveHeight=" + Format(cloudSeaWaveHeight));
        builder.AppendLine("cloudSeaWaveSpeed=" + Format(cloudSeaWaveSpeed));
        builder.AppendLine("cloudSeaOffsetStrength=" + Format(cloudSeaOffsetStrength));
        builder.AppendLine("showDistantIslands=" + showDistantIslands);
        return builder.ToString();
    }

    private static float GetFloat(Material material, string property, float fallback)
    {
        return material != null && material.HasProperty(property) ? material.GetFloat(property) : fallback;
    }

    private static Color GetColor(Material material, string property, Color fallback)
    {
        return material != null && material.HasProperty(property) ? material.GetColor(property) : fallback;
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetColor(Material material, string property, Color value)
    {
        if (material != null && material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static string Format(Vector3 value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "({0:0.###}, {1:0.###}, {2:0.###})",
            value.x,
            value.y,
            value.z);
    }

    private static string Format(Color value)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})",
            value.r,
            value.g,
            value.b,
            value.a);
    }

    private static string Format(float value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static void MarkDirty(Object target)
    {
#if UNITY_EDITOR
        if (target != null)
        {
            EditorUtility.SetDirty(target);
        }
#endif
    }
}
