using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DamageTestSceneBuilder
{
    private const string GeneratedPrefix = "Damage Test ";

    [MenuItem("Wild Wind/Damage/Build Shooting Test Scene")]
    public static void BuildShootingTestScene()
    {
        ClearGeneratedObjects();

        DamageableShip target = CreateTargetShip();
        DamageTestBench bench = CreateBench(target);
        EnsureCameraAndLight(target.transform, bench.transform);

        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        Selection.activeGameObject = bench.gameObject;
        Debug.Log("[Урон] Тестовая сцена обстрела собрана. Выбери Damage Test Bench и жми кнопки выстрела в инспекторе.");
    }

    private static void ClearGeneratedObjects()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            GameObject obj = objects[i];
            if (obj != null && obj.scene.IsValid() && obj.name.StartsWith(GeneratedPrefix))
            {
                Undo.DestroyObjectImmediate(obj);
            }
        }
    }

    private static DamageableShip CreateTargetShip()
    {
        GameObject root = new GameObject(GeneratedPrefix + "Target Ship");
        Undo.RegisterCreatedObjectUndo(root, "Create damage target");
        root.transform.position = new Vector3(0f, 80f, 35f);

        Rigidbody body = Undo.AddComponent<Rigidbody>(root);
        body.isKinematic = true;
        body.useGravity = false;
        body.mass = 1800f;

        DamageableShip damageable = Undo.AddComponent<DamageableShip>(root);
        damageable.shipId = "damage_test_target";
        damageable.displayNameRu = "Бронекорпус-мишень";
        damageable.maxStructureHp = 600f;
        damageable.structureHp = 600f;
        damageable.debugLogging = true;
        damageable.applyModuleEffectsToShipPhysics = false;
        damageable.modules = new List<ShipDamageModuleState>
        {
            Module("hull", "Корпус", 260f, 0.6f),
            Module("engine", "Двигатель", 120f, 1f),
            Module("propeller", "Винт", 90f, 1f),
            Module("claudium_loop", "Клавдиевый контур", 140f, 1f),
            Module("cargo", "Грузовой отсек", 100f, 0.8f)
        };

        CreatePaintedArmorBody(root.transform, damageable);

        CreateModuleMarker(root.transform, damageable, "engine", "Двигатель", new Vector3(4f, 0f, 3.2f), new Vector3(3f, 2f, 2.2f), new Color(0.8f, 0.25f, 0.15f), false);
        CreateModuleMarker(root.transform, damageable, "claudium_loop", "Контур", new Vector3(-3.5f, 0f, -0.5f), new Vector3(2.6f, 2.6f, 2.6f), new Color(0.2f, 0.8f, 1f), false);
        CreateModuleMarker(root.transform, damageable, "cargo", "Груз", new Vector3(0f, -0.6f, -1.5f), new Vector3(4.5f, 2.2f, 3f), new Color(0.7f, 0.55f, 0.2f), false);
        CreateModuleMarker(root.transform, damageable, "propeller", "Внешний винт", new Vector3(0f, 0f, 6.8f), new Vector3(7f, 1f, 1f), new Color(0.45f, 0.7f, 1f), true);

        return damageable;
    }

    private static DamageTestBench CreateBench(DamageableShip target)
    {
        GameObject benchObject = new GameObject(GeneratedPrefix + "Bench");
        Undo.RegisterCreatedObjectUndo(benchObject, "Create damage bench");
        benchObject.transform.position = new Vector3(0f, 80f, -80f);
        benchObject.transform.LookAt(target.GetAimPoint());

        GameObject muzzleObject = new GameObject(GeneratedPrefix + "Muzzle");
        Undo.RegisterCreatedObjectUndo(muzzleObject, "Create damage muzzle");
        muzzleObject.transform.SetParent(benchObject.transform, false);
        muzzleObject.transform.localPosition = Vector3.zero;
        muzzleObject.transform.localRotation = Quaternion.identity;

        GameObject muzzleVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(muzzleVisual, "Create damage muzzle visual");
        muzzleVisual.name = GeneratedPrefix + "Muzzle Visual";
        muzzleVisual.transform.SetParent(muzzleObject.transform, false);
        muzzleVisual.transform.localPosition = Vector3.forward * 2f;
        muzzleVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        muzzleVisual.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
        Collider muzzleCollider = muzzleVisual.GetComponent<Collider>();
        if (muzzleCollider != null)
        {
            Undo.DestroyObjectImmediate(muzzleCollider);
        }
        SetColor(muzzleVisual, new Color(0.12f, 0.12f, 0.12f));

        DamageTestBench bench = Undo.AddComponent<DamageTestBench>(benchObject);
        bench.target = target;
        bench.muzzle = muzzleObject.transform;
        bench.muzzleAxis = DamageMuzzleAxis.LocalForward;
        bench.maxRangeMeters = 300f;
        bench.spawnPhysicalProjectilesInPlayMode = true;
        bench.muzzleVelocityMS = 160f;
        bench.projectileMassKg = 8f;
        bench.ramZoneId = "front";
        return bench;
    }

    private static PaintedArmorBody CreatePaintedArmorBody(Transform parent, DamageableShip owner)
    {
        GameObject armorObject = new GameObject(GeneratedPrefix + "Painted Armor Body");
        Undo.RegisterCreatedObjectUndo(armorObject, "Create painted armor body");
        armorObject.transform.SetParent(parent, false);
        armorObject.transform.localPosition = Vector3.zero;
        armorObject.transform.localRotation = Quaternion.identity;
        armorObject.transform.localScale = Vector3.one;

        PaintedArmorBody armor = Undo.AddComponent<PaintedArmorBody>(armorObject);
        armor.owner = owner;
        armor.boxSize = new Vector3(18f, 6f, 10f);
        armor.faces = new List<PaintedArmorFace>
        {
            PaintedFace(PaintedArmorBoxFace.FrontNegativeZ, "front", "Лобовая плита", 65f, 260f, Color.red),
            PaintedFace(PaintedArmorBoxFace.RearPositiveZ, "rear", "Корма", 28f, 130f, Color.yellow),
            PaintedFace(PaintedArmorBoxFace.LeftNegativeX, "left", "Левый борт", 38f, 170f, new Color(1f, 0.5f, 0.1f)),
            PaintedFace(PaintedArmorBoxFace.RightPositiveX, "right", "Правый борт", 38f, 170f, new Color(1f, 0.5f, 0.1f)),
            PaintedFace(PaintedArmorBoxFace.TopPositiveY, "top", "Верхняя палуба", 22f, 120f, Color.cyan),
            PaintedFace(PaintedArmorBoxFace.BottomNegativeY, "bottom", "Нижняя броня", 18f, 110f, Color.blue)
        };
        armor.EnsureCollider();
        armor.RebuildVisualMesh();
        return armor;
    }

    private static PaintedArmorFace PaintedFace(
        PaintedArmorBoxFace side,
        string id,
        string nameRu,
        float armorMm,
        float armorHp,
        Color color)
    {
        return new PaintedArmorFace
        {
            face = side,
            zoneId = id,
            displayNameRu = nameRu,
            armorMm = armorMm,
            maxArmorHp = Mathf.Max(1f, armorHp),
            armorHp = Mathf.Max(1f, armorHp),
            ricochetAngleDeg = 70f,
            debugColor = new Color(color.r, color.g, color.b, 0.75f)
        };
    }

    private static void CreateModuleMarker(
        Transform parent,
        DamageableShip owner,
        string moduleId,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        bool externalModule)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(marker, "Create module marker");
        marker.name = GeneratedPrefix + "Module " + name;
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = localScale;
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        DamageableModuleHitbox hitbox = Undo.AddComponent<DamageableModuleHitbox>(marker);
        hitbox.owner = owner;
        hitbox.moduleId = moduleId;
        hitbox.displayNameRu = name;
        hitbox.defaultMaxHp = owner != null && owner.GetModule(moduleId) != null
            ? owner.GetModule(moduleId).maxHp
            : 100f;
        hitbox.externalModule = externalModule;
        hitbox.transparentWhenDestroyed = true;
        hitbox.debugColor = new Color(color.r, color.g, color.b, 0.25f);

        SetColor(marker, color);
    }

    private static ShipDamageModuleState Module(string id, string nameRu, float hp, float weight)
    {
        return new ShipDamageModuleState
        {
            moduleId = id,
            displayNameRu = nameRu,
            maxHp = hp,
            hp = hp,
            damageWeight = weight
        };
    }

    private static void EnsureCameraAndLight(Transform target, Transform bench)
    {
        GameObject cameraObject = new GameObject(GeneratedPrefix + "Camera");
        Undo.RegisterCreatedObjectUndo(cameraObject, "Create damage camera");
        cameraObject.transform.position = new Vector3(0f, 94f, -120f);
        cameraObject.transform.LookAt(target.position);
        Camera camera = Undo.AddComponent<Camera>(cameraObject);
        camera.fieldOfView = 45f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        camera.depth = 10f;
        camera.tag = "MainCamera";

        GameObject lightObject = new GameObject(GeneratedPrefix + "Directional Light");
        Undo.RegisterCreatedObjectUndo(lightObject, "Create damage light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = Undo.AddComponent<Light>(lightObject);
        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    private static void SetColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader != null)
        {
            renderer.sharedMaterial = new Material(shader);
            renderer.sharedMaterial.color = color;
        }
    }
}

[CustomEditor(typeof(DamageTestBench))]
public class DamageTestBenchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DamageTestBench bench = (DamageTestBench)target;

        DrawScriptField();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Цель", EditorStyles.boldLabel);
        DrawProperty("target", "Цель", "Корабль или бронекорпус, по которому стреляет тестовый стенд.");
        DrawProperty("muzzle", "Ствол", "Точка, из которой выходит снаряд и рисуется прицельный луч.");
        DrawProperty("muzzleAxis", "Ось ствола", "Какая локальная ось объекта ствола считается направлением выстрела.");
        DrawProperty("maxRangeMeters", "Дальность стрельбы, м", "Максимальная длина тестового луча и raycast-выстрела.");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Стрельба", EditorStyles.boldLabel);
        DrawProperty("spawnPhysicalProjectilesInPlayMode", "Создавать физические снаряды в Play Mode");
        DrawProperty("muzzleVelocityMS", "Начальная скорость снаряда, м/с");
        DrawProperty("projectileMassKg", "Масса снаряда, кг");
        DrawProperty("projectileRadiusMeters", "Радиус снаряда, м");
        DrawProperty("obliqueShotSideOffsetMeters", "Боковое смещение косого выстрела, м");
        DrawProperty("obliqueShotForwardOffsetMeters", "Продольное смещение косого выстрела, м");
        DrawShellPreset(serializedObject.FindProperty("armorPiercingShell"), "Бронебойный снаряд");
        DrawShellPreset(serializedObject.FindProperty("highExplosiveShell"), "Фугасный снаряд");
        DrawProperty("highExplosiveImpulseScale", "Масштаб импульса фугаса", "Импульс фугаса = урон снаряда * масштаб. При пробитии брони импульс утраивается.");
        DrawProperty("highExplosiveMaxTargetDeltaVelocityMS", "Макс. Δv от фугаса, м/с");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Таран", EditorStyles.boldLabel);
        DrawProperty("ramZoneId", "ID бронезоны тарана");
        DrawProperty("rammerMassKg", "Масса таранящего объекта, кг");
        DrawProperty("ramTargetMassKg", "Масса цели, кг");
        DrawProperty("ramRelativeSpeedMS", "Скорость удара, м/с");
        DrawProperty("ramMinDamageSpeedMS", "Минимальная скорость урона, м/с");
        DrawProperty("ramDamageScale", "Масштаб урона тарана", "Урон считается как sqrt(энергия удара в кДж) * масштаб.");
        DrawProperty("rammerDamageMultiplier", "Модификатор урона таранящего");
        DrawProperty("ramPushElasticity", "Упругость толчка");
        DrawProperty("ramMaxTargetDeltaVelocityMS", "Макс. скорость толчка цели, м/с");
        DrawProperty("ramKeepTargetAnchored", "Цель закреплена для теста", "Если включено, урон считается, но физический толчок от тарана и фугаса не применяется.");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Отладка", EditorStyles.boldLabel);
        DrawProperty("debugLogging", "Писать логи");
        DrawProperty("drawAimRayInScene", "Показывать прицельный луч в сцене");
        DrawProperty("drawAimRayOnlyWhenSelected", "Показывать луч только при выборе");
        DrawProperty("aimRayHitMarkerRadius", "Радиус маркера попадания");
        DrawProperty("aimRayArmorHitColor", "Цвет попадания в броню");
        DrawProperty("aimRayOtherHitColor", "Цвет попадания в другой объект");
        DrawProperty("aimRayMissColor", "Цвет промаха");
        DrawProperty("aimRayObliqueColor", "Цвет косого луча");
        DrawProperty("drawObliqueShotRays", "Показывать косые лучи");
        DrawProperty("lastMessage", "Последнее сообщение");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Обстрел", EditorStyles.boldLabel);

        if (GUILayout.Button("Выстрел: бронебойный"))
        {
            RecordBenchAndTarget(bench, "Fire AP shell");
            bench.FireArmorPiercing();
            MarkBenchAndTargetDirty(bench);
        }

        if (GUILayout.Button("Косой ББ слева"))
        {
            RecordBenchAndTarget(bench, "Fire oblique AP shell left");
            bench.FireArmorPiercingObliqueLeft();
            MarkBenchAndTargetDirty(bench);
        }

        if (GUILayout.Button("Косой ББ справа"))
        {
            RecordBenchAndTarget(bench, "Fire oblique AP shell right");
            bench.FireArmorPiercingObliqueRight();
            MarkBenchAndTargetDirty(bench);
        }

        if (GUILayout.Button("Выстрел: фугас"))
        {
            RecordBenchAndTarget(bench, "Fire HE shell");
            bench.FireHighExplosive();
            MarkBenchAndTargetDirty(bench);
        }

        if (GUILayout.Button("Сымитировать таран"))
        {
            RecordBenchAndTarget(bench, "Simulate ram");
            bench.SimulateRam();
            MarkBenchAndTargetDirty(bench);
        }

        if (GUILayout.Button("Сбросить повреждения цели"))
        {
            RecordBenchAndTarget(bench, "Reset target damage");
            bench.ResetTargetDamage();
            MarkBenchAndTargetDirty(bench);
        }

        if (!string.IsNullOrWhiteSpace(bench.lastMessage))
        {
            EditorGUILayout.HelpBox(bench.lastMessage, MessageType.Info);
        }

        if (bench.target != null)
        {
            DrawDamageSummary(bench.target);
        }
    }

    private void DrawScriptField()
    {
        SerializedProperty script = serializedObject.FindProperty("m_Script");
        if (script == null) return;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(script, new GUIContent("Скрипт"));
        }
    }

    private void DrawProperty(string propertyName, string label, string tooltip = "")
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    private static void DrawShellPreset(SerializedProperty shell, string label)
    {
        if (shell == null) return;

        shell.isExpanded = EditorGUILayout.Foldout(shell.isExpanded, label, true);
        if (!shell.isExpanded) return;

        EditorGUI.indentLevel++;
        DrawRelative(shell, "displayNameRu", "Название");
        DrawRelative(shell, "shellType", "Тип снаряда");
        DrawRelative(shell, "caliberMm", "Калибр, мм");
        DrawRelative(shell, "damagePoints", "Старый общий урон");
        DrawRelative(shell, "hullDamageOnPenetration", "Урон корпусу при пробитии");
        DrawRelative(shell, "armorPlateDamage", "Урон бронелисту");
        DrawRelative(shell, "moduleDamage", "Урон модулю");
        DrawRelative(shell, "penetrationMm", "Пробитие, мм");
        DrawRelative(shell, "explosiveRadiusMeters", "Радиус фугаса, м");
        DrawRelative(shell, "normalizationDegrees", "Нормализация, град");
        DrawRelative(shell, "penetrationRollSpread", "Разброс пробития");
        DrawRelative(shell, "projectileColor", "Цвет снаряда");
        EditorGUI.indentLevel--;
    }

    private static void DrawRelative(SerializedProperty parent, string propertyName, string label, string tooltip = "")
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property == null) return;

        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    private static void RecordBenchAndTarget(DamageTestBench bench, string action)
    {
        Undo.RecordObject(bench, action);
        if (bench.target != null)
        {
            Undo.RecordObject(bench.target, action);
        }
    }

    private static void MarkBenchAndTargetDirty(DamageTestBench bench)
    {
        EditorUtility.SetDirty(bench);
        if (bench.target != null)
        {
            EditorUtility.SetDirty(bench.target);
        }
        SceneView.RepaintAll();
    }

    public static void DrawDamageSummary(DamageableShip ship)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Состояние цели", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Структура", $"{ship.structureHp:0.0} / {ship.maxStructureHp:0.0}");
        EditorGUILayout.LabelField("Попадания", $"{ship.hitCount}, пробития {ship.penetrationCount}, рикошеты {ship.ricochetCount}, непробития {ship.noPenetrationCount}, фугасы {ship.explosiveSplashCount}, тараны {ship.impactCount}");
        EditorGUILayout.LabelField("Последнее попадание", $"{ship.lastHitOutcome} / {ship.lastHitZoneId}");
        EditorGUILayout.LabelField("Угол и броня", $"{ship.lastHitImpactAngleDeg:0}°, {ship.lastHitArmorMm:0}->{ship.lastHitEffectiveArmorMm:0} мм, пробитие {ship.lastHitPenetrationMm:0} мм");

        if (ship.modules != null)
        {
            for (int i = 0; i < ship.modules.Count; i++)
            {
                ShipDamageModuleState module = ship.modules[i];
                if (module == null) continue;
                string status = module.IsDestroyed ? " уничтожен" : "";
                EditorGUILayout.LabelField(module.displayNameRu, $"{module.hp:0.0} / {module.maxHp:0.0}{status}");
            }
        }

        if (ship.recentEvents != null && ship.recentEvents.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Последние события", EditorStyles.boldLabel);
            for (int i = 0; i < ship.recentEvents.Count; i++)
            {
                EditorGUILayout.LabelField(ship.recentEvents[i], EditorStyles.wordWrappedLabel);
            }
        }
    }
}

[CustomEditor(typeof(DamageableShip))]
public class DamageableShipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DamageableShip ship = (DamageableShip)target;
        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Сбросить повреждения"))
        {
            Undo.RecordObject(ship, "Reset damage");
            ship.ResetDamageState();
            EditorUtility.SetDirty(ship);
        }

        DamageTestBenchEditor.DrawDamageSummary(ship);
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}

[CustomEditor(typeof(PaintedArmorBody))]
public class PaintedArmorBodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PaintedArmorBody armor = (PaintedArmorBody)target;
        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Пересобрать визуал бронекороба"))
        {
            Undo.RecordObject(armor, "Rebuild painted armor visual");
            armor.EnsureCollider();
            armor.RebuildVisualMesh();
            EditorUtility.SetDirty(armor);
            SceneView.RepaintAll();
        }

        if (armor.faces == null || armor.faces.Count == 0) return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Покрашенные грани", EditorStyles.boldLabel);
        for (int i = 0; i < armor.faces.Count; i++)
        {
            PaintedArmorFace face = armor.faces[i];
            if (face == null) continue;
            EditorGUILayout.LabelField(face.displayNameRu, $"{face.CurrentArmorMm:0.0}/{face.armorMm:0.0} мм, прочность {face.armorHp:0.0}/{face.maxArmorHp:0.0}, id={face.zoneId}");
        }
    }
}
