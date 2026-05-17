using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(VisualPlayModeTuner))]
public sealed class VisualPlayModeTunerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Пульт визуальной настройки сцены. В Play Mode меняй значения здесь: они применяются сразу, если включено поле \"Применять постоянно\".",
            MessageType.Info);

        Section("Ссылки");
        Field("visualCamera", "Камера", "Главная камера визуальной сцены.");
        Field("moonLight", "Лунный свет", "Directional Light, который задаёт холодный верхний свет.");
        Field("aeroFogMaterial", "Материал AERO-тумана", "Материал fullscreen-тумана AERO.");
        Field("skyboxMaterial", "Материал неба", "Настроенная копия skybox из AllSky.");
        Field("cloudRoot", "Облака TrueClouds", "Корневой объект декоративных объёмных облаков.");
        Field("fogParticlesRoot", "Туманные частицы", "Корневой объект локальных мягких облачков из Fog Particles.");
        Field("cloudSeaRoot", "Облачное море", "Корневой объект нижнего слоя Space Cloud Waves.");
        Field("cloudSeaMaterial", "Материал облачного моря", "Настроенная копия материала Space Cloud Waves.");
        Field("distantIslandsRoot", "Дальние острова", "Корневой объект силуэтов островов на горизонте.");

        Section("Камера");
        Field("cameraPosition", "Позиция камеры", "Откуда смотрим.");
        Field("cameraTarget", "Точка взгляда", "Куда камера смотрит.");
        Field("cameraFov", "Угол обзора", "Шире значение даёт больше воздуха, меньшее приближает кадр.");

        Section("Небо");
        Field("skyExposure", "Яркость неба", "0 темнее, 1 стандартно, выше 1 ярче.");
        Field("skyRotation", "Поворот неба", "Крутит skybox вокруг сцены.");
        Field("skyTint", "Оттенок неба", "Общий цветовой фильтр skybox.");

        Section("AERO туман");
        Field("fogDensity", "Плотность тумана", "Главная ручка: больше значение сильнее замыливает даль.");
        Field("fogMaxDistance", "Дальность тумана", "На каком расстоянии AERO перестаёт накапливать туман.");
        Field("fogAlpha", "Сила наложения", "Прозрачность итогового тумана поверх картинки.");
        Field("fogColor", "Цвет тумана", "Цвет воздушной дымки.");

        Section("Высотные слои");
        Field("useAltitudeAtmosphere", "Управлять слоями по высоте", "Если включено, AERO-туман и видимость смертельной бури зависят от высоты корабля.");
        Field("altitudeSource", "Источник высоты", "Обычно сюда можно дать корабль. Если пусто, используется высота предпросмотра ниже.");
        Field("previewAltitudeMeters", "Высота предпросмотра", "Ручная высота для проверки слоёв прямо в визуальной сцене.");
        Field("deadlyStormY", "Y смертельной бури", "Нулевая поверхность смертельной бури в мировых координатах.");
        Field("deadlyStormDrawDistance", "Показ поверхности бури до", "До какой высоты над бурей вообще отрисовывается Space Cloud Waves.");
        Field("violentStormCeiling", "Верх яростной бури", "Ниже этого значения действует самый плотный слой.");
        Field("calmStormCeiling", "Верх спокойной бури", "Ниже этого значения буря ещё опасна, но уже читается спокойнее.");
        Field("habitationCeiling", "Верх зоны обитания", "Ниже этого значения живёт основная часть мира.");
        Field("violentStormVisibility", "Видимость яростной бури", "Сейчас по твоему ТЗ: 100 метров.");
        Field("calmStormVisibility", "Видимость спокойной бури", "Видимость слоя 1000-2000 м.");
        Field("habitationVisibility", "Видимость зоны обитания", "Техническая дальность видимости в зоне обитания.");
        Field("upperTechnicalVisibility", "Техническая видимость верха", "Дальность для разреженной и верхней зоны.");
        Field("violentStormFogColor", "Цвет яростной бури", "Тёмная грозовая пелена ниже 1000 м.");
        Field("calmStormFogColor", "Цвет спокойной бури", "Менее плотная грозовая пелена 1000-2000 м.");
        Field("habitationFogColor", "Цвет белой пелены", "Белёсое облачное дно, когда смотришь вниз из зоны обитания.");
        Field("upperFogColor", "Цвет верхней дымки", "Холодная дальняя дымка верхних высот.");

        Section("Свет");
        Field("ambientColor", "Цвет окружения", "Общий холодный свет без направления.");
        Field("ambientIntensity", "Сила окружения", "Насколько сильно ambient подсвечивает тени.");
        Field("moonColor", "Цвет луны", "Цвет направленного света.");
        Field("moonIntensity", "Сила луны", "Интенсивность направленного света.");
        Field("moonEuler", "Поворот луны", "Направление света в градусах.");

        Section("Облачное море");
        Field("showCloudSea", "Показывать облачное море", "Включает или выключает нижний слой Space Cloud Waves.");
        Field("overrideCloudSeaMaterial", "Управлять материалом из тюнера", "Если включено, тюнер каждый кадр записывает цвет, плотность и волны в материал. Если выключено, крути материал напрямую в инспекторе.");
        Field("cloudSeaYOffset", "Высота моря", "Поднимает или опускает облачное море под кораблём и островом.");
        Field("cloudSeaScale", "Масштаб моря", "Растягивает или сжимает всю поверхность облачного моря.");
        Field("cloudSeaMainColor", "Тёмная масса", "Основной цвет нижней облачной толщи.");
        Field("cloudSeaFogColor", "Цвет глубины", "Цвет дальнего и глубокого слоя.");
        Field("cloudSeaHighlightColor", "Светящиеся гребни", "HDR-цвет подсвеченных волн.");
        Field("cloudSeaAlphaDensity", "Плотность альфы", "Насколько плотно материал собирает облачную поверхность.");
        Field("cloudSeaWaveHeight", "Высота волн", "Амплитуда движения вершин на сетке.");
        Field("cloudSeaWaveSpeed", "Скорость волн", "Скорость анимации волн.");
        Field("cloudSeaOffsetStrength", "Сила смещения", "Общая сила шумового смещения поверхности.");

        Section("Слои");
        Field("showClouds", "Показывать облака", "Включает или выключает декоративные облачные банки TrueClouds.");
        Field("showFogParticles", "Показывать туманные частицы", "Включает или выключает ближние мягкие клочья тумана из Fog Particles.");
        Field("showDistantIslands", "Показывать дальние острова", "Включает или выключает силуэты островов на горизонте.");
        Field("applyContinuously", "Применять постоянно", "Если включено, изменения обновляются каждый кадр в Play Mode.");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        VisualPlayModeTuner tuner = (VisualPlayModeTuner)target;
        EditorGUILayout.HelpBox(tuner.GetAtmosphereDebugText(), MessageType.None);

        if (GUILayout.Button("Применить сейчас"))
        {
            tuner.ApplyNow();
        }

        if (GUILayout.Button("Захватить текущую сцену"))
        {
            tuner.CaptureFromScene();
            EditorUtility.SetDirty(tuner);
        }

        string saveButton = EditorApplication.isPlaying
            ? "Сохранить после выхода из Play Mode"
            : "Сохранить текущие настройки в сцену";
        if (GUILayout.Button(saveButton))
        {
            VisualPlayModeTunerPlayModeSaver.QueueSave(tuner);
        }

        if (VisualPlayModeTunerPlayModeSaver.HasPendingSnapshot)
        {
            EditorGUILayout.HelpBox(
                "Снимок настроек запомнен. Теперь останови Play Mode, и Unity применит его к сцене и материалам.",
                MessageType.Info);

            if (GUILayout.Button("Отменить отложенное сохранение"))
            {
                VisualPlayModeTunerPlayModeSaver.ClearPendingSnapshot();
            }
        }

        if (GUILayout.Button("Пресет: грозовое дно"))
        {
            tuner.ApplyStormBottomPreset();
            EditorUtility.SetDirty(tuner);
            EditorSceneManager.MarkSceneDirty(tuner.gameObject.scene);
        }

        if (GUILayout.Button("Скопировать настройки для Codex"))
        {
            tuner.CopySettingsForCodex();
        }

        EditorGUILayout.HelpBox(
            "Если поймал хороший вид в Play Mode, нажми \"Сохранить после выхода из Play Mode\", а потом останови Play Mode. Обычное копирование настроек оставлено как запасной ручной способ.",
            MessageType.Warning);
    }

    private void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void Field(string propertyName, string label, string tooltip)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }
    }
}

[InitializeOnLoad]
internal static class VisualStormBottomPresetEditor
{
    static VisualStormBottomPresetEditor()
    {
    }

    [MenuItem("Wild Wind/Visual/Применить пресет грозового дна", false, 120)]
    private static void ApplyPresetFromMenu()
    {
        Renderer selectedRenderer = FindSelectedRenderer();
        if (selectedRenderer != null)
        {
            ApplyPresetToRenderer(selectedRenderer);
            return;
        }

        VisualPlayModeTuner tuner = FindSceneTuner();
        if (tuner != null)
        {
            tuner.ApplyStormBottomPreset();
            EditorUtility.SetDirty(tuner);
            EditorSceneManager.MarkSceneDirty(tuner.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Visual] Пресет грозового дна применён через Visual Play Mode Tuner.");
            return;
        }

        Debug.LogWarning("[Visual] Не найден ни выделенный Mesh Renderer, ни Visual Play Mode Tuner.");
    }

    [MenuItem("CONTEXT/MeshRenderer/Пресет: грозовое дно")]
    private static void ApplyPresetFromMeshRenderer(MenuCommand command)
    {
        if (command.context is Renderer renderer)
        {
            ApplyPresetToRenderer(renderer);
        }
    }

    private static Renderer FindSelectedRenderer()
    {
        if (Selection.activeGameObject == null)
        {
            return null;
        }

        Renderer renderer = Selection.activeGameObject.GetComponent<Renderer>();
        return renderer != null ? renderer : Selection.activeGameObject.GetComponentInChildren<Renderer>(true);
    }

    private static void ApplyPresetToRenderer(Renderer renderer)
    {
        int appliedCount = 0;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            VisualPlayModeTuner.ApplyStormBottomMaterial(material);
            EditorUtility.SetDirty(material);
            appliedCount++;
        }

        if (appliedCount <= 0)
        {
            Debug.LogWarning("[Visual] У выделенного рендера нет материала для пресета грозового дна.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Visual] Пресет грозового дна применён к материалам: " + appliedCount + ".");
    }

    private static VisualPlayModeTuner FindSceneTuner()
    {
        VisualPlayModeTuner[] tuners = Resources.FindObjectsOfTypeAll<VisualPlayModeTuner>();
        for (int i = 0; i < tuners.Length; i++)
        {
            VisualPlayModeTuner tuner = tuners[i];
            if (tuner != null && !EditorUtility.IsPersistent(tuner) && tuner.gameObject.scene.IsValid())
            {
                return tuner;
            }
        }

        return null;
    }
}

[InitializeOnLoad]
internal static class VisualSelectedMaterialPlayModeSaver
{
    private const string PendingKey = "WildWind.VisualSelectedMaterial.PendingSave";
    private const string SnapshotKey = "WildWind.VisualSelectedMaterial.Snapshot";

    static VisualSelectedMaterialPlayModeSaver()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Wild Wind/Visual/Сохранить материалы выделенного рендера после Play Mode", false, 130)]
    private static void QueueSaveFromSelection()
    {
        Renderer renderer = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Renderer>()
            : null;

        if (renderer == null)
        {
            Debug.LogWarning("[Visual] Выдели объект с Mesh Renderer, например Space Cloud Waves Surface.");
            return;
        }

        QueueSave(renderer);
    }

    [MenuItem("Wild Wind/Visual/Сохранить материалы выделенного рендера после Play Mode", true)]
    private static bool CanQueueSaveFromSelection()
    {
        return Selection.activeGameObject != null
            && Selection.activeGameObject.GetComponent<Renderer>() != null;
    }

    [MenuItem("CONTEXT/MeshRenderer/Сохранить материалы после Play Mode")]
    private static void QueueSaveFromMeshRenderer(MenuCommand command)
    {
        if (command.context is Renderer renderer)
        {
            QueueSave(renderer);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ApplyPendingSnapshot();
        }
    }

    private static void QueueSave(Renderer renderer)
    {
        MaterialSelectionSnapshot snapshot = new MaterialSelectionSnapshot
        {
            scenePath = renderer.gameObject.scene.path,
            objectPath = GetTransformPath(renderer.transform),
            materials = new System.Collections.Generic.List<MaterialSnapshot>()
        };

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Debug.LogWarning("[Visual] Материал " + material.name + " не является asset-файлом, его нельзя сохранить после Play Mode.");
                continue;
            }

            snapshot.materials.Add(new MaterialSnapshot
            {
                assetPath = assetPath,
                json = EditorJsonUtility.ToJson(material)
            });
        }

        if (snapshot.materials.Count == 0)
        {
            Debug.LogWarning("[Visual] У выделенного рендера нет материалов-asset для сохранения.");
            return;
        }

        EditorPrefs.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
        EditorPrefs.SetBool(PendingKey, true);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("[Visual] Материалы запомнены. Останови Play Mode, и Unity сохранит их в asset-файлы.");
            return;
        }

        ApplyPendingSnapshot();
    }

    private static void ApplyPendingSnapshot()
    {
        if (!EditorPrefs.GetBool(PendingKey, false))
        {
            return;
        }

        string json = EditorPrefs.GetString(SnapshotKey, string.Empty);
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(SnapshotKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        MaterialSelectionSnapshot snapshot = JsonUtility.FromJson<MaterialSelectionSnapshot>(json);
        if (snapshot == null || snapshot.materials == null || snapshot.materials.Count == 0)
        {
            Debug.LogWarning("[Visual] Не удалось прочитать сохранённые материалы выделенного рендера.");
            return;
        }

        int savedCount = 0;
        for (int i = 0; i < snapshot.materials.Count; i++)
        {
            MaterialSnapshot materialSnapshot = snapshot.materials[i];
            if (materialSnapshot == null || string.IsNullOrWhiteSpace(materialSnapshot.assetPath) || string.IsNullOrWhiteSpace(materialSnapshot.json))
            {
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialSnapshot.assetPath);
            if (material == null)
            {
                Debug.LogWarning("[Visual] Не найден материал для сохранения: " + materialSnapshot.assetPath);
                continue;
            }

            EditorJsonUtility.FromJsonOverwrite(materialSnapshot.json, material);
            EditorUtility.SetDirty(material);
            savedCount++;
        }

        if (savedCount <= 0)
        {
            Debug.LogWarning("[Visual] Не удалось сохранить материалы выделенного рендера.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Visual] Сохранены материалы выделенного рендера: " + savedCount + ". Объект: " + snapshot.objectPath);
    }

    private static string GetTransformPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }

    [System.Serializable]
    private sealed class MaterialSelectionSnapshot
    {
        public string scenePath;
        public string objectPath;
        public System.Collections.Generic.List<MaterialSnapshot> materials;
    }

    [System.Serializable]
    private sealed class MaterialSnapshot
    {
        public string assetPath;
        public string json;
    }
}

[InitializeOnLoad]
internal static class VisualPlayModeTunerPlayModeSaver
{
    private const string PendingKey = "WildWind.VisualPlayModeTuner.PendingSave";
    private const string SnapshotKey = "WildWind.VisualPlayModeTuner.Snapshot";

    static VisualPlayModeTunerPlayModeSaver()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static bool HasPendingSnapshot => EditorPrefs.GetBool(PendingKey, false);

    public static void QueueSave(VisualPlayModeTuner tuner)
    {
        if (tuner == null)
        {
            return;
        }

        VisualTunerSnapshot snapshot = Capture(tuner);
        EditorPrefs.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
        EditorPrefs.SetBool(PendingKey, true);

        if (EditorApplication.isPlaying)
        {
            Debug.Log("[VisualPlayModeTuner] Настройки запомнены. Останови Play Mode, и они сохранятся в сцену.", tuner);
        }
        else
        {
            ApplyPendingSnapshot();
        }
    }

    public static void ClearPendingSnapshot()
    {
        EditorPrefs.DeleteKey(PendingKey);
        EditorPrefs.DeleteKey(SnapshotKey);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ApplyPendingSnapshot();
        }
    }

    private static VisualTunerSnapshot Capture(VisualPlayModeTuner tuner)
    {
        tuner.CaptureFromScene();

        SerializedObject serializedTuner = new SerializedObject(tuner);
        serializedTuner.Update();

        VisualTunerSnapshot snapshot = new VisualTunerSnapshot
        {
            scenePath = tuner.gameObject.scene.path,
            cameraPosition = Vector3Value(serializedTuner, "cameraPosition"),
            cameraTarget = Vector3Value(serializedTuner, "cameraTarget"),
            cameraFov = FloatValue(serializedTuner, "cameraFov"),
            skyExposure = FloatValue(serializedTuner, "skyExposure"),
            skyRotation = FloatValue(serializedTuner, "skyRotation"),
            skyTint = ColorValue(serializedTuner, "skyTint"),
            fogDensity = FloatValue(serializedTuner, "fogDensity"),
            fogMaxDistance = FloatValue(serializedTuner, "fogMaxDistance"),
            fogAlpha = FloatValue(serializedTuner, "fogAlpha"),
            fogColor = ColorValue(serializedTuner, "fogColor"),
            useAltitudeAtmosphere = BoolValue(serializedTuner, "useAltitudeAtmosphere"),
            previewAltitudeMeters = FloatValue(serializedTuner, "previewAltitudeMeters"),
            deadlyStormY = FloatValue(serializedTuner, "deadlyStormY"),
            deadlyStormDrawDistance = FloatValue(serializedTuner, "deadlyStormDrawDistance"),
            violentStormCeiling = FloatValue(serializedTuner, "violentStormCeiling"),
            calmStormCeiling = FloatValue(serializedTuner, "calmStormCeiling"),
            habitationCeiling = FloatValue(serializedTuner, "habitationCeiling"),
            violentStormVisibility = FloatValue(serializedTuner, "violentStormVisibility"),
            calmStormVisibility = FloatValue(serializedTuner, "calmStormVisibility"),
            habitationVisibility = FloatValue(serializedTuner, "habitationVisibility"),
            upperTechnicalVisibility = FloatValue(serializedTuner, "upperTechnicalVisibility"),
            violentStormFogColor = ColorValue(serializedTuner, "violentStormFogColor"),
            calmStormFogColor = ColorValue(serializedTuner, "calmStormFogColor"),
            habitationFogColor = ColorValue(serializedTuner, "habitationFogColor"),
            upperFogColor = ColorValue(serializedTuner, "upperFogColor"),
            ambientColor = ColorValue(serializedTuner, "ambientColor"),
            ambientIntensity = FloatValue(serializedTuner, "ambientIntensity"),
            moonColor = ColorValue(serializedTuner, "moonColor"),
            moonIntensity = FloatValue(serializedTuner, "moonIntensity"),
            moonEuler = Vector3Value(serializedTuner, "moonEuler"),
            showCloudSea = BoolValue(serializedTuner, "showCloudSea"),
            overrideCloudSeaMaterial = BoolValue(serializedTuner, "overrideCloudSeaMaterial"),
            cloudSeaYOffset = FloatValue(serializedTuner, "cloudSeaYOffset"),
            cloudSeaScale = FloatValue(serializedTuner, "cloudSeaScale"),
            cloudSeaMainColor = ColorValue(serializedTuner, "cloudSeaMainColor"),
            cloudSeaFogColor = ColorValue(serializedTuner, "cloudSeaFogColor"),
            cloudSeaHighlightColor = ColorValue(serializedTuner, "cloudSeaHighlightColor"),
            cloudSeaAlphaDensity = FloatValue(serializedTuner, "cloudSeaAlphaDensity"),
            cloudSeaWaveHeight = FloatValue(serializedTuner, "cloudSeaWaveHeight"),
            cloudSeaWaveSpeed = FloatValue(serializedTuner, "cloudSeaWaveSpeed"),
            cloudSeaOffsetStrength = FloatValue(serializedTuner, "cloudSeaOffsetStrength"),
            showClouds = BoolValue(serializedTuner, "showClouds"),
            showFogParticles = BoolValue(serializedTuner, "showFogParticles"),
            showDistantIslands = BoolValue(serializedTuner, "showDistantIslands"),
            applyContinuously = BoolValue(serializedTuner, "applyContinuously")
        };

        CaptureMaterial(serializedTuner, "aeroFogMaterial", ref snapshot.aeroFogMaterialPath, ref snapshot.aeroFogMaterialJson);
        CaptureMaterial(serializedTuner, "skyboxMaterial", ref snapshot.skyboxMaterialPath, ref snapshot.skyboxMaterialJson);
        CaptureMaterial(serializedTuner, "cloudSeaMaterial", ref snapshot.cloudSeaMaterialPath, ref snapshot.cloudSeaMaterialJson);
        return snapshot;
    }

    private static void ApplyPendingSnapshot()
    {
        if (!HasPendingSnapshot)
        {
            return;
        }

        string json = EditorPrefs.GetString(SnapshotKey, string.Empty);
        ClearPendingSnapshot();

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        VisualTunerSnapshot snapshot = JsonUtility.FromJson<VisualTunerSnapshot>(json);
        if (snapshot == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.scenePath) && EditorSceneManager.GetActiveScene().path != snapshot.scenePath)
        {
            EditorSceneManager.OpenScene(snapshot.scenePath, OpenSceneMode.Single);
        }

        VisualPlayModeTuner tuner = FindSceneTuner();
        if (tuner == null)
        {
            Debug.LogWarning("[VisualPlayModeTuner] Не нашёл Visual Play Mode Tuner для применения сохранённых настроек.");
            return;
        }

        ApplyMaterial(snapshot.aeroFogMaterialPath, snapshot.aeroFogMaterialJson);
        ApplyMaterial(snapshot.skyboxMaterialPath, snapshot.skyboxMaterialJson);
        ApplyMaterial(snapshot.cloudSeaMaterialPath, snapshot.cloudSeaMaterialJson);

        SerializedObject serializedTuner = new SerializedObject(tuner);
        serializedTuner.Update();
        SetVector3(serializedTuner, "cameraPosition", snapshot.cameraPosition);
        SetVector3(serializedTuner, "cameraTarget", snapshot.cameraTarget);
        SetFloat(serializedTuner, "cameraFov", snapshot.cameraFov);
        SetFloat(serializedTuner, "skyExposure", snapshot.skyExposure);
        SetFloat(serializedTuner, "skyRotation", snapshot.skyRotation);
        SetColor(serializedTuner, "skyTint", snapshot.skyTint);
        SetFloat(serializedTuner, "fogDensity", snapshot.fogDensity);
        SetFloat(serializedTuner, "fogMaxDistance", snapshot.fogMaxDistance);
        SetFloat(serializedTuner, "fogAlpha", snapshot.fogAlpha);
        SetColor(serializedTuner, "fogColor", snapshot.fogColor);
        SetBool(serializedTuner, "useAltitudeAtmosphere", snapshot.useAltitudeAtmosphere);
        SetFloat(serializedTuner, "previewAltitudeMeters", snapshot.previewAltitudeMeters);
        SetFloat(serializedTuner, "deadlyStormY", snapshot.deadlyStormY);
        SetFloat(serializedTuner, "deadlyStormDrawDistance", snapshot.deadlyStormDrawDistance);
        SetFloat(serializedTuner, "violentStormCeiling", snapshot.violentStormCeiling);
        SetFloat(serializedTuner, "calmStormCeiling", snapshot.calmStormCeiling);
        SetFloat(serializedTuner, "habitationCeiling", snapshot.habitationCeiling);
        SetFloat(serializedTuner, "violentStormVisibility", snapshot.violentStormVisibility);
        SetFloat(serializedTuner, "calmStormVisibility", snapshot.calmStormVisibility);
        SetFloat(serializedTuner, "habitationVisibility", snapshot.habitationVisibility);
        SetFloat(serializedTuner, "upperTechnicalVisibility", snapshot.upperTechnicalVisibility);
        SetColor(serializedTuner, "violentStormFogColor", snapshot.violentStormFogColor);
        SetColor(serializedTuner, "calmStormFogColor", snapshot.calmStormFogColor);
        SetColor(serializedTuner, "habitationFogColor", snapshot.habitationFogColor);
        SetColor(serializedTuner, "upperFogColor", snapshot.upperFogColor);
        SetColor(serializedTuner, "ambientColor", snapshot.ambientColor);
        SetFloat(serializedTuner, "ambientIntensity", snapshot.ambientIntensity);
        SetColor(serializedTuner, "moonColor", snapshot.moonColor);
        SetFloat(serializedTuner, "moonIntensity", snapshot.moonIntensity);
        SetVector3(serializedTuner, "moonEuler", snapshot.moonEuler);
        SetBool(serializedTuner, "showCloudSea", snapshot.showCloudSea);
        SetBool(serializedTuner, "overrideCloudSeaMaterial", snapshot.overrideCloudSeaMaterial);
        SetFloat(serializedTuner, "cloudSeaYOffset", snapshot.cloudSeaYOffset);
        SetFloat(serializedTuner, "cloudSeaScale", snapshot.cloudSeaScale);
        SetColor(serializedTuner, "cloudSeaMainColor", snapshot.cloudSeaMainColor);
        SetColor(serializedTuner, "cloudSeaFogColor", snapshot.cloudSeaFogColor);
        SetColor(serializedTuner, "cloudSeaHighlightColor", snapshot.cloudSeaHighlightColor);
        SetFloat(serializedTuner, "cloudSeaAlphaDensity", snapshot.cloudSeaAlphaDensity);
        SetFloat(serializedTuner, "cloudSeaWaveHeight", snapshot.cloudSeaWaveHeight);
        SetFloat(serializedTuner, "cloudSeaWaveSpeed", snapshot.cloudSeaWaveSpeed);
        SetFloat(serializedTuner, "cloudSeaOffsetStrength", snapshot.cloudSeaOffsetStrength);
        SetBool(serializedTuner, "showClouds", snapshot.showClouds);
        SetBool(serializedTuner, "showFogParticles", snapshot.showFogParticles);
        SetBool(serializedTuner, "showDistantIslands", snapshot.showDistantIslands);
        SetBool(serializedTuner, "applyContinuously", snapshot.applyContinuously);
        serializedTuner.ApplyModifiedPropertiesWithoutUndo();

        tuner.ApplyNow();
        EditorUtility.SetDirty(tuner);

        Scene scene = tuner.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[VisualPlayModeTuner] Настройки из Play Mode сохранены в сцену и материалы.", tuner);
    }

    private static void CaptureMaterial(SerializedObject serializedTuner, string propertyName, ref string path, ref string json)
    {
        Material material = ObjectValue<Material>(serializedTuner, propertyName);
        if (material == null)
        {
            return;
        }

        path = AssetDatabase.GetAssetPath(material);
        json = EditorJsonUtility.ToJson(material);
    }

    private static void ApplyMaterial(string path, string json)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            return;
        }

        EditorJsonUtility.FromJsonOverwrite(json, material);
        EditorUtility.SetDirty(material);
    }

    private static VisualPlayModeTuner FindSceneTuner()
    {
        VisualPlayModeTuner[] tuners = Resources.FindObjectsOfTypeAll<VisualPlayModeTuner>();
        for (int i = 0; i < tuners.Length; i++)
        {
            VisualPlayModeTuner tuner = tuners[i];
            if (tuner != null && !EditorUtility.IsPersistent(tuner) && tuner.gameObject.scene.IsValid())
            {
                return tuner;
            }
        }

        return null;
    }

    private static float FloatValue(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.floatValue : 0f;
    }

    private static bool BoolValue(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.boolValue;
    }

    private static Vector3 Vector3Value(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.vector3Value : Vector3.zero;
    }

    private static Color ColorValue(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.colorValue : Color.white;
    }

    private static T ObjectValue<T>(SerializedObject serializedObject, string propertyName) where T : Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    private static void SetColor(SerializedObject serializedObject, string propertyName, Color value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }

    [System.Serializable]
    private sealed class VisualTunerSnapshot
    {
        public string scenePath;
        public Vector3 cameraPosition;
        public Vector3 cameraTarget;
        public float cameraFov;
        public float skyExposure;
        public float skyRotation;
        public Color skyTint;
        public float fogDensity;
        public float fogMaxDistance;
        public float fogAlpha;
        public Color fogColor;
        public bool useAltitudeAtmosphere;
        public float previewAltitudeMeters;
        public float deadlyStormY;
        public float deadlyStormDrawDistance;
        public float violentStormCeiling;
        public float calmStormCeiling;
        public float habitationCeiling;
        public float violentStormVisibility;
        public float calmStormVisibility;
        public float habitationVisibility;
        public float upperTechnicalVisibility;
        public Color violentStormFogColor;
        public Color calmStormFogColor;
        public Color habitationFogColor;
        public Color upperFogColor;
        public Color ambientColor;
        public float ambientIntensity;
        public Color moonColor;
        public float moonIntensity;
        public Vector3 moonEuler;
        public bool showCloudSea;
        public bool overrideCloudSeaMaterial;
        public float cloudSeaYOffset;
        public float cloudSeaScale;
        public Color cloudSeaMainColor;
        public Color cloudSeaFogColor;
        public Color cloudSeaHighlightColor;
        public float cloudSeaAlphaDensity;
        public float cloudSeaWaveHeight;
        public float cloudSeaWaveSpeed;
        public float cloudSeaOffsetStrength;
        public bool showClouds;
        public bool showFogParticles;
        public bool showDistantIslands;
        public bool applyContinuously;
        public string aeroFogMaterialPath;
        public string aeroFogMaterialJson;
        public string skyboxMaterialPath;
        public string skyboxMaterialJson;
        public string cloudSeaMaterialPath;
        public string cloudSeaMaterialJson;
    }
}
