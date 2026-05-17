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
    [SerializeField, InspectorName("Туманные частицы")] private Transform fogParticlesRoot;
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

    [Header("Высотные слои")]
    [SerializeField, InspectorName("Управлять слоями по высоте")] private bool useAltitudeAtmosphere = true;
    [SerializeField, InspectorName("Источник высоты")] private Transform altitudeSource = null;
    [SerializeField, Range(0f, 100000f), InspectorName("Высота предпросмотра")] private float previewAltitudeMeters = 128f;
    [SerializeField, InspectorName("Y смертельной бури")] private float deadlyStormY = 0f;
    [SerializeField, Range(20f, 300f), InspectorName("Показ поверхности бури до")] private float deadlyStormDrawDistance = 100f;
    [SerializeField, Range(0f, 500f), InspectorName("Полуширина перехода")] private float altitudeTransitionHalfWidth = 50f;
    [SerializeField, Range(100f, 2500f), InspectorName("Верх яростной бури")] private float violentStormCeiling = 1000f;
    [SerializeField, Range(500f, 4000f), InspectorName("Верх спокойной бури")] private float calmStormCeiling = 2000f;
    [SerializeField, Range(2000f, 20000f), InspectorName("Верх зоны обитания")] private float habitationCeiling = 10000f;
    [SerializeField, Range(20f, 500f), InspectorName("Видимость яростной бури")] private float violentStormVisibility = 100f;
    [SerializeField, Range(200f, 2500f), InspectorName("Видимость спокойной бури")] private float calmStormVisibility = 1000f;
    [SerializeField, Range(1000f, 12000f), InspectorName("Видимость зоны обитания")] private float habitationVisibility = 6000f;
    [SerializeField, Range(1000f, 20000f), InspectorName("Техническая видимость верха")] private float upperTechnicalVisibility = 8000f;
    [SerializeField, InspectorName("Цвет яростной бури")] private Color violentStormFogColor = new Color(0.055f, 0.065f, 0.09f, 1f);
    [SerializeField, InspectorName("Цвет спокойной бури")] private Color calmStormFogColor = new Color(0.20f, 0.22f, 0.27f, 1f);
    [SerializeField, InspectorName("Цвет белой пелены")] private Color habitationFogColor = new Color(0.62f, 0.70f, 0.82f, 1f);
    [SerializeField, InspectorName("Цвет верхней дымки")] private Color upperFogColor = new Color(0.35f, 0.47f, 0.66f, 1f);

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
    [SerializeField, InspectorName("Показывать туманные частицы")] private bool showFogParticles = true;
    [SerializeField, InspectorName("Показывать дальние острова")] private bool showDistantIslands = true;
    [SerializeField, InspectorName("Применять постоянно")] private bool applyContinuously = true;

    public void ConfigureReferences(
        Camera camera,
        Light moon,
        Material fogMaterial,
        Material skyMaterial,
        Transform clouds,
        Transform fogParticles,
        Transform cloudSea,
        Material cloudSeaMat,
        Transform distantIslands)
    {
        visualCamera = camera;
        moonLight = moon;
        aeroFogMaterial = fogMaterial;
        skyboxMaterial = skyMaterial;
        cloudRoot = clouds;
        fogParticlesRoot = fogParticles;
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

    public string GetAtmosphereDebugText()
    {
        AltitudeAtmosphere atmosphere = BuildAltitudeAtmosphere();
        string surfaceState = atmosphere.showDeadlyStormSurface ? "видна" : "скрыта";
        return string.Format(
            CultureInfo.InvariantCulture,
            "Высота над бурей: {0:0.#} м. Слой: {1}. Видимость: {2:0.#} м. Поверхность смертельной бури: {3}.",
            atmosphere.altitudeAboveStorm,
            atmosphere.layerName,
            atmosphere.visibility,
            surfaceState);
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

        if (fogParticlesRoot == null)
        {
            GameObject fogParticles = GameObject.Find("Fog Particle Wisps");
            fogParticlesRoot = fogParticles != null ? fogParticles.transform : null;
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

        AltitudeAtmosphere atmosphere = BuildAltitudeAtmosphere();
        float appliedDensity = useAltitudeAtmosphere ? atmosphere.density : fogDensity;
        float appliedMaxDistance = useAltitudeAtmosphere ? atmosphere.visibility : fogMaxDistance;
        float appliedAlpha = useAltitudeAtmosphere ? atmosphere.alpha : fogAlpha;
        Color appliedColor = useAltitudeAtmosphere ? atmosphere.color : fogColor;

        SetFloat(aeroFogMaterial, "_Density", appliedDensity);
        SetFloat(aeroFogMaterial, "_Max_Distance", appliedMaxDistance);
        SetFloat(aeroFogMaterial, "_ADDITIONAL_LIGHTS", 0f);
        SetColor(aeroFogMaterial, "_Colour", new Color(appliedColor.r, appliedColor.g, appliedColor.b, appliedAlpha));
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
        AltitudeAtmosphere atmosphere = BuildAltitudeAtmosphere();
        bool cloudSeaVisible = showCloudSea && (!useAltitudeAtmosphere || atmosphere.showDeadlyStormSurface);

        if (cloudSeaRoot != null)
        {
            cloudSeaRoot.gameObject.SetActive(cloudSeaVisible);
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

    private AltitudeAtmosphere BuildAltitudeAtmosphere()
    {
        float altitude = Mathf.Max(0f, GetWorldAltitude() - deadlyStormY);
        float transitionHalfWidth = Mathf.Max(0f, altitudeTransitionHalfWidth);

        AltitudeAtmosphere violent = CreateViolentStormAtmosphere(altitude);
        AltitudeAtmosphere calm = CreateCalmStormAtmosphere(altitude);
        AltitudeAtmosphere habitation = CreateHabitationAtmosphere(altitude);
        AltitudeAtmosphere upper = CreateUpperAtmosphere(altitude);

        if (TryBlendAtmospheres(altitude, violentStormCeiling, transitionHalfWidth, violent, calm, out AltitudeAtmosphere blended))
        {
            return blended;
        }

        if (TryBlendAtmospheres(altitude, calmStormCeiling, transitionHalfWidth, calm, habitation, out blended))
        {
            return blended;
        }

        if (TryBlendAtmospheres(altitude, habitationCeiling, transitionHalfWidth, habitation, upper, out blended))
        {
            return blended;
        }

        if (altitude < violentStormCeiling)
        {
            return violent;
        }

        if (altitude < calmStormCeiling)
        {
            return calm;
        }

        if (altitude < habitationCeiling)
        {
            return habitation;
        }

        return upper;
    }

    private AltitudeAtmosphere CreateViolentStormAtmosphere(float altitude)
    {
        return new AltitudeAtmosphere(
            altitude,
            "Яростная буря",
            violentStormVisibility,
            0.03f,
            0.62f,
            violentStormFogColor,
            altitude <= deadlyStormDrawDistance);
    }

    private AltitudeAtmosphere CreateCalmStormAtmosphere(float altitude)
    {
        return new AltitudeAtmosphere(
            altitude,
            "Спокойная буря",
            calmStormVisibility,
            0.014f,
            0.38f,
            calmStormFogColor,
            false);
    }

    private AltitudeAtmosphere CreateHabitationAtmosphere(float altitude)
    {
        return new AltitudeAtmosphere(
            altitude,
            "Зона обитания",
            habitationVisibility,
            0.0065f,
            0.20f,
            habitationFogColor,
            false);
    }

    private AltitudeAtmosphere CreateUpperAtmosphere(float altitude)
    {
        return new AltitudeAtmosphere(
            altitude,
            "Разреженная зона",
            upperTechnicalVisibility,
            0.0035f,
            0.12f,
            upperFogColor,
            false);
    }

    private static bool TryBlendAtmospheres(
        float altitude,
        float boundary,
        float halfWidth,
        AltitudeAtmosphere lower,
        AltitudeAtmosphere upper,
        out AltitudeAtmosphere blended)
    {
        blended = lower;
        if (halfWidth <= 0f || altitude < boundary - halfWidth || altitude > boundary + halfWidth)
        {
            return false;
        }

        float t = Mathf.InverseLerp(boundary - halfWidth, boundary + halfWidth, altitude);
        t = Mathf.SmoothStep(0f, 1f, t);
        blended = AltitudeAtmosphere.Lerp(lower, upper, t);
        return true;
    }

    private float GetWorldAltitude()
    {
        return altitudeSource != null ? altitudeSource.position.y : previewAltitudeMeters;
    }

    private void ApplyLayers()
    {
        if (cloudRoot != null)
        {
            cloudRoot.gameObject.SetActive(showClouds);
        }

        if (fogParticlesRoot != null)
        {
            fogParticlesRoot.gameObject.SetActive(showFogParticles);
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
        builder.AppendLine("useAltitudeAtmosphere=" + useAltitudeAtmosphere);
        builder.AppendLine("previewAltitudeMeters=" + Format(previewAltitudeMeters));
        builder.AppendLine("deadlyStormY=" + Format(deadlyStormY));
        builder.AppendLine("deadlyStormDrawDistance=" + Format(deadlyStormDrawDistance));
        builder.AppendLine("altitudeTransitionHalfWidth=" + Format(altitudeTransitionHalfWidth));
        builder.AppendLine("violentStormCeiling=" + Format(violentStormCeiling));
        builder.AppendLine("calmStormCeiling=" + Format(calmStormCeiling));
        builder.AppendLine("habitationCeiling=" + Format(habitationCeiling));
        builder.AppendLine("violentStormVisibility=" + Format(violentStormVisibility));
        builder.AppendLine("calmStormVisibility=" + Format(calmStormVisibility));
        builder.AppendLine("habitationVisibility=" + Format(habitationVisibility));
        builder.AppendLine("upperTechnicalVisibility=" + Format(upperTechnicalVisibility));
        builder.AppendLine("violentStormFogColor=" + Format(violentStormFogColor));
        builder.AppendLine("calmStormFogColor=" + Format(calmStormFogColor));
        builder.AppendLine("habitationFogColor=" + Format(habitationFogColor));
        builder.AppendLine("upperFogColor=" + Format(upperFogColor));
        builder.AppendLine("computedAtmosphere=" + GetAtmosphereDebugText());
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
        builder.AppendLine("showFogParticles=" + showFogParticles);
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

    private readonly struct AltitudeAtmosphere
    {
        public readonly float altitudeAboveStorm;
        public readonly string layerName;
        public readonly float visibility;
        public readonly float density;
        public readonly float alpha;
        public readonly Color color;
        public readonly bool showDeadlyStormSurface;

        public AltitudeAtmosphere(
            float altitudeAboveStorm,
            string layerName,
            float visibility,
            float density,
            float alpha,
            Color color,
            bool showDeadlyStormSurface)
        {
            this.altitudeAboveStorm = altitudeAboveStorm;
            this.layerName = layerName;
            this.visibility = visibility;
            this.density = density;
            this.alpha = alpha;
            this.color = color;
            this.showDeadlyStormSurface = showDeadlyStormSurface;
        }

        public static AltitudeAtmosphere Lerp(AltitudeAtmosphere lower, AltitudeAtmosphere upper, float t)
        {
            t = Mathf.Clamp01(t);
            string layerName = t <= 0.001f
                ? lower.layerName
                : t >= 0.999f
                    ? upper.layerName
                    : "Переход: " + lower.layerName + " -> " + upper.layerName;

            return new AltitudeAtmosphere(
                Mathf.Lerp(lower.altitudeAboveStorm, upper.altitudeAboveStorm, t),
                layerName,
                Mathf.Lerp(lower.visibility, upper.visibility, t),
                Mathf.Lerp(lower.density, upper.density, t),
                Mathf.Lerp(lower.alpha, upper.alpha, t),
                Color.Lerp(lower.color, upper.color, t),
                lower.showDeadlyStormSurface || upper.showDeadlyStormSurface);
        }
    }
}
