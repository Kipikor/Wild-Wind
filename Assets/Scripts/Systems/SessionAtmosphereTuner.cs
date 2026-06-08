using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering.Universal;
public sealed class SessionAtmosphereTuner : MonoBehaviour
{
    private const int DefaultRendererIndex = 0;
    private const int AeroRendererIndex = 1;

    [Header("Ссылки")]
    [SerializeField, InspectorName("Камера")] private Camera visualCamera;
    [SerializeField, InspectorName("Лунный свет")] private Light moonLight;
    [SerializeField, InspectorName("Материал AERO")] private Material aeroFogMaterial;
    [SerializeField, InspectorName("Материал неба")] private Material skyboxMaterial;
    [SerializeField, InspectorName("Облака TrueClouds")] private Transform cloudRoot;
    [SerializeField, InspectorName("Туманные частицы")] private Transform fogParticlesRoot;
    [SerializeField, InspectorName("Облачное море")] private Transform cloudSeaRoot;
    [SerializeField, InspectorName("Материал облачного моря")] private Material cloudSeaMaterial;

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
    [SerializeField, Range(0f, 100000f), InspectorName("Fallback Altitude, m")] private float fallbackAltitudeMeters = 128f;
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
    [SerializeField, InspectorName("Применять постоянно")] private bool applyContinuously = true;

    [Header("Runtime Performance")]
    [SerializeField, InspectorName("Lean runtime atmosphere")] private bool leanRuntimeAtmosphere = true;
    [SerializeField, InspectorName("Use Unity fog fallback")] private bool useUnityFogFallback = true;
    [SerializeField, InspectorName("Disable TrueClouds runtime")] private bool disableTrueCloudsInLeanRuntime = true;
    [SerializeField, InspectorName("Disable AERO runtime")] private bool disableAeroInLeanRuntime = true;

    private Camera cachedPerformanceCamera;
    private Behaviour[] cachedCameraBehaviours;
    private Behaviour cachedAeroFogController;
    private Camera cachedRendererSelectionCamera;
    private int cachedRendererIndex = -1;
    private Material runtimeSkyboxFallbackMaterial;
    private int lastRuntimeApplyFrame = -1;

    public void ConfigureReferences(
        Camera camera,
        Light moon,
        Material fogMaterial,
        Material skyMaterial,
        Transform clouds,
        Transform fogParticles,
        Transform cloudSea,
        Material cloudSeaMat)
    {
        visualCamera = camera;
        moonLight = moon;
        aeroFogMaterial = fogMaterial;
        skyboxMaterial = skyMaterial;
        cloudRoot = clouds;
        fogParticlesRoot = fogParticles;
        cloudSeaRoot = cloudSea;
        cloudSeaMaterial = cloudSeaMat;
        ApplyRendererSelection();
        ApplyNow();
    }

    public void SetRuntimeView(Transform newAltitudeSource, Vector3 newCameraPosition, Vector3 newCameraTarget, float newCameraFov)
    {
        altitudeSource = newAltitudeSource;
        if (newAltitudeSource != null)
        {
            fallbackAltitudeMeters = Mathf.Max(0f, newAltitudeSource.position.y);
        }

        cameraPosition = newCameraPosition;
        cameraTarget = newCameraTarget;
        cameraFov = Mathf.Clamp(newCameraFov, 20f, 70f);
        ApplyRendererSelection();
        ApplyNow();
    }

    public void ApplyNow()
    {
        if (Application.isPlaying && lastRuntimeApplyFrame == Time.frameCount)
        {
            return;
        }

        if (Application.isPlaying)
        {
            lastRuntimeApplyFrame = Time.frameCount;
        }

        ResolveMissingReferences();
        ApplyRendererSelection();
        ApplyExpensiveAtmosphereState();
        ApplyCamera();
        ApplySkybox();
        ApplyFog();
        ApplyLight();
        ApplyCloudSea();
        ApplyLayers();
    }

    public string GetAtmosphereStatusText()
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

    private void OnEnable()
    {
        ResolveMissingReferences();
        ApplyRendererSelection();
        ApplyNow();
    }

    private void Update()
    {
        if (applyContinuously && !Application.isPlaying)
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
    }

    private void ApplyCamera()
    {
        if (visualCamera == null)
        {
            return;
        }

        if (Application.isPlaying)
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

    private void ApplyRendererSelection()
    {
        if (visualCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData cameraData = visualCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            return;
        }

        if (visualCamera != cachedRendererSelectionCamera)
        {
            cachedRendererSelectionCamera = visualCamera;
            cachedRendererIndex = -1;
        }

        int rendererIndex = Application.isBatchMode || ShouldUseLeanRuntimeAtmosphere()
            ? DefaultRendererIndex
            : AeroRendererIndex;
        if (cachedRendererIndex == rendererIndex)
        {
            return;
        }

        cameraData.SetRenderer(rendererIndex);
        cachedRendererIndex = rendererIndex;
    }

    private void ApplySkybox()
    {
        Material activeSkyboxMaterial = ResolveSkyboxMaterial();
        if (visualCamera != null)
        {
            visualCamera.clearFlags = activeSkyboxMaterial != null
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;
            visualCamera.backgroundColor = new Color(0.12f, 0.18f, 0.28f, 1f);
        }

        if (activeSkyboxMaterial == null)
        {
            return;
        }

        RenderSettings.skybox = activeSkyboxMaterial;
        SetFloat(activeSkyboxMaterial, "_Exposure", skyExposure);
        SetFloat(activeSkyboxMaterial, "_Rotation", skyRotation);
        SetColor(activeSkyboxMaterial, "_Tint", skyTint);
        SetColor(activeSkyboxMaterial, "_SkyTint", new Color(skyTint.r, skyTint.g, skyTint.b, 1f));
        SetColor(activeSkyboxMaterial, "_GroundColor", new Color(0.08f, 0.11f, 0.16f, 1f));
        SetFloat(activeSkyboxMaterial, "_AtmosphereThickness", 0.85f);
        SetFloat(activeSkyboxMaterial, "_SunSize", 0.035f);
    }

    private Material ResolveSkyboxMaterial()
    {
        if (IsUsableSkyboxMaterial(skyboxMaterial))
        {
            return skyboxMaterial;
        }

        if (IsUsableSkyboxMaterial(runtimeSkyboxFallbackMaterial))
        {
            return runtimeSkyboxFallbackMaterial;
        }

        Shader fallbackShader = Shader.Find("Skybox/Procedural");
        if (fallbackShader == null || !fallbackShader.isSupported)
        {
            return null;
        }

        runtimeSkyboxFallbackMaterial = new Material(fallbackShader)
        {
            name = "Runtime Lean Skybox",
            hideFlags = HideFlags.DontSave
        };
        return runtimeSkyboxFallbackMaterial;
    }

    private static bool IsUsableSkyboxMaterial(Material material)
    {
        return material != null &&
            material.shader != null &&
            material.shader.isSupported &&
            material.shader.name != "Hidden/InternalErrorShader";
    }

    private void ApplyFog()
    {
        AltitudeAtmosphere atmosphere = BuildAltitudeAtmosphere();
        float appliedDensity = useAltitudeAtmosphere ? atmosphere.density : fogDensity;
        float appliedMaxDistance = useAltitudeAtmosphere ? atmosphere.visibility : fogMaxDistance;
        float appliedAlpha = useAltitudeAtmosphere ? atmosphere.alpha : fogAlpha;
        Color appliedColor = useAltitudeAtmosphere ? atmosphere.color : fogColor;

        if (ShouldUseLeanRuntimeAtmosphere())
        {
            ApplyUnityFogFallback(appliedColor, appliedAlpha, appliedMaxDistance);
            return;
        }

        if (useUnityFogFallback)
        {
            RenderSettings.fog = false;
        }

        if (aeroFogMaterial == null)
        {
            return;
        }

        SetFloat(aeroFogMaterial, "_Density", appliedDensity);
        SetFloat(aeroFogMaterial, "_Max_Distance", appliedMaxDistance);
        SetFloat(aeroFogMaterial, "_ADDITIONAL_LIGHTS", 0f);
        SetColor(aeroFogMaterial, "_Colour", new Color(appliedColor.r, appliedColor.g, appliedColor.b, appliedAlpha));
        aeroFogMaterial.DisableKeyword("_ADDITIONAL_LIGHTS");
    }

    private void ApplyUnityFogFallback(Color appliedColor, float appliedAlpha, float appliedMaxDistance)
    {
        if (!useUnityFogFallback)
        {
            RenderSettings.fog = false;
            return;
        }

        float visibility = Mathf.Max(80f, appliedMaxDistance);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = appliedColor;
        RenderSettings.fogDensity = Mathf.Clamp(1.35f / visibility, 0.00008f, 0.014f) *
            Mathf.Clamp01(Mathf.Max(0.25f, appliedAlpha));
        RenderSettings.fogStartDistance = 0f;
        RenderSettings.fogEndDistance = visibility;
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
        bool cloudSeaVisible = !ShouldUseLeanRuntimeAtmosphere() && showCloudSea && (!useAltitudeAtmosphere || atmosphere.showDeadlyStormSurface);

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
        return altitudeSource != null ? altitudeSource.position.y : fallbackAltitudeMeters;
    }

    private void ApplyLayers()
    {
        if (cloudRoot != null)
        {
            cloudRoot.gameObject.SetActive(showClouds && !ShouldUseLeanRuntimeAtmosphere());
        }

        if (fogParticlesRoot != null)
        {
            fogParticlesRoot.gameObject.SetActive(showFogParticles && !ShouldUseLeanRuntimeAtmosphere());
        }

    }

    private bool ShouldUseLeanRuntimeAtmosphere()
    {
        return Application.isPlaying && leanRuntimeAtmosphere;
    }

    private void ApplyExpensiveAtmosphereState()
    {
        bool leanRuntime = ShouldUseLeanRuntimeAtmosphere();
        ResolvePerformanceReferences();

        if (cachedCameraBehaviours != null && disableTrueCloudsInLeanRuntime)
        {
            for (int i = 0; i < cachedCameraBehaviours.Length; i++)
            {
                Behaviour behaviour = cachedCameraBehaviours[i];
                if (behaviour == null) continue;

                System.Type type = behaviour.GetType();
                if (type != null && type.FullName != null && type.FullName.StartsWith("TrueClouds."))
                {
                    behaviour.enabled = !leanRuntime && showClouds;
                }
            }
        }

        if (cachedAeroFogController != null && disableAeroInLeanRuntime)
        {
            cachedAeroFogController.enabled = !leanRuntime;
        }
    }

    private void ResolvePerformanceReferences()
    {
        if (visualCamera != cachedPerformanceCamera)
        {
            cachedPerformanceCamera = visualCamera;
            cachedCameraBehaviours = visualCamera != null ? visualCamera.GetComponents<Behaviour>() : null;
        }

        if (cachedAeroFogController != null)
        {
            return;
        }

        GameObject aeroControllerObject = GameObject.Find("AERO Visual Fog Controller");
        if (aeroControllerObject == null)
        {
            return;
        }

        Behaviour[] behaviours = aeroControllerObject.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null) continue;

            System.Type type = behaviour.GetType();
            if (type != null && type.FullName != null && type.FullName.Contains("VolumetricFogController"))
            {
                cachedAeroFogController = behaviour;
                return;
            }
        }
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
