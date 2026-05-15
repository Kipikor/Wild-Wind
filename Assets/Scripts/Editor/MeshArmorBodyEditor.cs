using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshArmorBody))]
public class MeshArmorBodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        MeshArmorBody armor = (MeshArmorBody)target;

        DrawScriptField();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Броневая mesh-оболочка", EditorStyles.boldLabel);
        DrawProperty("owner", "Корабль-владелец");
        DrawProperty("meshCollider", "MeshCollider");
        DrawProperty("colliderIsTrigger", "Сделать коллайдер триггером");
        DrawProperty("convexColliderForPhysics", "Convex коллайдер для физики", "Нужно включить, если цель должна двигаться от Rigidbody.");
        DrawProperty("ricochetAngleDeg", "Угол рикошета, град", "Общий угол авторикошета для всех бронелистов этой mesh-брони.");
        DrawPlates(serializedObject.FindProperty("plates"));
        DrawDetails(serializedObject.FindProperty("details"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Автосборка", EditorStyles.boldLabel);
        DrawProperty("normalAngleThresholdDeg", "Порог угла нормали");
        DrawProperty("planeDistanceThreshold", "Порог расстояния плоскости");
        DrawProperty("defaultArmorMm", "Броня по умолчанию, мм");
        DrawProperty("hpPerArmorMm", "Прочность на 1 мм брони");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Отладка", EditorStyles.boldLabel);
        DrawProperty("drawPlates", "Показывать бронелисты");
        DrawProperty("drawOnlyWhenSelected", "Показывать только при выборе");
        DrawProperty("maxDrawnTriangles", "Рисовать не больше треугольников");

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Подготовка ProBuilder/Mesh брони", EditorStyles.boldLabel);

        if (GUILayout.Button("Собрать самостоятельную цель из этого mesh"))
        {
            AssembleStandaloneTarget(armor);
        }

        if (GUILayout.Button("Подготовить MeshCollider"))
        {
            Undo.RecordObject(armor, "Prepare mesh armor collider");
            armor.EnsureMeshCollider();
            EditorUtility.SetDirty(armor);
            Debug.Log("[Урон] MeshCollider для mesh-брони подготовлен.", armor);
        }

        if (GUILayout.Button("Собрать бронелисты из Mesh"))
        {
            Undo.RecordObject(armor, "Build mesh armor plates");
            armor.EnsureMeshCollider();
            armor.RebuildPlatesFromMesh();
            EditorUtility.SetDirty(armor);
            SceneView.RepaintAll();
            Debug.Log($"[Урон] Mesh-броня собрана: бронелистов {armor.plates.Count}. Теперь можно настроить толщину и прочность каждого листа.", armor);
        }

        if (GUILayout.Button("Каждый бронелист - отдельная бронедеталь"))
        {
            Undo.RecordObject(armor, "Make each plate separate armor detail");
            armor.MakeEachPlateSeparateDetail();
            EditorUtility.SetDirty(armor);
            Debug.Log("[Урон] Для каждого бронелиста создана отдельная бронедеталь.", armor);
        }

        if (GUILayout.Button("Весь корпус - одна бронедеталь"))
        {
            Undo.RecordObject(armor, "Make single armor detail");
            armor.MakeSingleArmorDetail();
            EditorUtility.SetDirty(armor);
            Debug.Log("[Урон] Все бронелисты привязаны к одной бронедетали.", armor);
        }

        if (GUILayout.Button("Сбросить прочность бронедеталей"))
        {
            Undo.RecordObject(armor, "Reset mesh armor hp");
            armor.ResetArmorState();
            EditorUtility.SetDirty(armor);
            Debug.Log("[Урон] Прочность mesh-бронедеталей сброшена.", armor);
        }

        DrawSummary(armor);
    }

    private static void AssembleStandaloneTarget(MeshArmorBody armor)
    {
        if (armor == null) return;

        GameObject targetObject = armor.gameObject;
        Undo.RecordObject(armor, "Assemble standalone mesh armor target");

        DamageableShip ship = targetObject.GetComponent<DamageableShip>();
        if (ship == null)
        {
            ship = Undo.AddComponent<DamageableShip>(targetObject);
        }
        else
        {
            Undo.RecordObject(ship, "Assemble standalone mesh armor target");
        }

        Rigidbody body = targetObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody>(targetObject);
        }
        else
        {
            Undo.RecordObject(body, "Assemble standalone mesh armor target");
        }

        body.mass = Mathf.Max(1f, body.mass);
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ship.shipId = string.IsNullOrWhiteSpace(ship.shipId) || ship.shipId == "target_ship"
            ? targetObject.name.Replace(" ", "_").ToLowerInvariant()
            : ship.shipId;
        ship.displayNameRu = string.IsNullOrWhiteSpace(ship.displayNameRu) || ship.displayNameRu == "Цель"
            ? targetObject.name
            : ship.displayNameRu;
        ship.applyModuleEffectsToShipPhysics = false;
        ship.EnsureDefaultModules();
        EnsureDefaultModuleHitboxes(targetObject.transform, ship, armor);

        armor.owner = ship;
        armor.colliderIsTrigger = false;
        armor.convexColliderForPhysics = true;
        armor.EnsureMeshCollider();
        if (armor.plates == null || armor.plates.Count == 0)
        {
            armor.RebuildPlatesFromMesh();
        }
        if (armor.details == null || armor.details.Count == 0)
        {
            armor.MakeEachPlateSeparateDetail();
        }

        DamageTestBench[] benches = FindObjectsByType<DamageTestBench>(FindObjectsSortMode.None);
        for (int i = 0; i < benches.Length; i++)
        {
            DamageTestBench bench = benches[i];
            if (bench == null) continue;

            Undo.RecordObject(bench, "Point damage bench to mesh armor target");
            bench.target = ship;
            EditorUtility.SetDirty(bench);
        }

        EditorUtility.SetDirty(armor);
        EditorUtility.SetDirty(ship);
        EditorUtility.SetDirty(body);
        SceneView.RepaintAll();
        Selection.activeGameObject = targetObject;
        Debug.Log($"[Урон] MeshArmorBody собран как самостоятельная цель: {targetObject.name}. DamageTestBench обновлено: {benches.Length}.", armor);
    }

    private static void EnsureDefaultModuleHitboxes(Transform root, DamageableShip owner, MeshArmorBody armor)
    {
        if (root == null || owner == null) return;

        Bounds bounds = GetLocalArmorBounds(armor);
        Vector3 size = new Vector3(
            Mathf.Max(1f, bounds.size.x),
            Mathf.Max(1f, bounds.size.y),
            Mathf.Max(1f, bounds.size.z));
        Vector3 center = bounds.center;

        EnsureModuleHitbox(
            root,
            owner,
            "engine",
            "Двигатель",
            center + new Vector3(size.x * 0.22f, 0f, size.z * 0.24f),
            new Vector3(size.x * 0.24f, size.y * 0.34f, size.z * 0.24f),
            new Color(0.85f, 0.25f, 0.15f),
            false);

        EnsureModuleHitbox(
            root,
            owner,
            "claudium_loop",
            "Клавдиевый контур",
            center + new Vector3(-size.x * 0.24f, 0f, 0f),
            new Vector3(size.x * 0.22f, size.y * 0.42f, size.z * 0.26f),
            new Color(0.2f, 0.8f, 1f),
            false);

        EnsureModuleHitbox(
            root,
            owner,
            "cargo",
            "Грузовой отсек",
            center + new Vector3(0f, -size.y * 0.12f, -size.z * 0.12f),
            new Vector3(size.x * 0.46f, size.y * 0.34f, size.z * 0.34f),
            new Color(0.72f, 0.55f, 0.2f),
            false);

        EnsureModuleHitbox(
            root,
            owner,
            "propeller",
            "Внешний винт",
            center + new Vector3(0f, 0f, size.z * 0.72f),
            new Vector3(size.x * 0.5f, size.y * 0.16f, size.z * 0.12f),
            new Color(0.45f, 0.7f, 1f),
            true);
    }

    private static Bounds GetLocalArmorBounds(MeshArmorBody armor)
    {
        if (armor != null)
        {
            MeshFilter filter = armor.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                return filter.sharedMesh.bounds;
            }
        }

        return new Bounds(Vector3.zero, new Vector3(8f, 4f, 8f));
    }

    private static void EnsureModuleHitbox(
        Transform root,
        DamageableShip owner,
        string moduleId,
        string displayName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        bool externalModule)
    {
        DamageableModuleHitbox existing = FindModuleHitbox(root, moduleId);
        if (existing != null)
        {
            Undo.RecordObject(existing, "Update mesh armor module hitbox");
            existing.owner = owner;
            existing.displayNameRu = displayName;
            existing.externalModule = externalModule;
            existing.transparentWhenDestroyed = true;
            existing.defaultMaxHp = owner.GetModule(moduleId) != null ? owner.GetModule(moduleId).maxHp : existing.defaultMaxHp;
            existing.transform.localPosition = localPosition;
            existing.transform.localRotation = Quaternion.identity;
            existing.transform.localScale = ClampModuleScale(localScale);
            existing.debugColor = new Color(color.r, color.g, color.b, 0.25f);
            SetModuleMarkerColor(existing.gameObject, color);
            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(existing.gameObject);
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(marker, "Create mesh armor module hitbox");
        marker.name = "Боевой модуль " + displayName;
        marker.transform.SetParent(root, false);
        marker.transform.localPosition = localPosition;
        marker.transform.localRotation = Quaternion.identity;
        marker.transform.localScale = ClampModuleScale(localScale);

        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        DamageableModuleHitbox hitbox = Undo.AddComponent<DamageableModuleHitbox>(marker);
        hitbox.owner = owner;
        hitbox.moduleId = moduleId;
        hitbox.displayNameRu = displayName;
        hitbox.defaultMaxHp = owner.GetModule(moduleId) != null ? owner.GetModule(moduleId).maxHp : 100f;
        hitbox.externalModule = externalModule;
        hitbox.transparentWhenDestroyed = true;
        hitbox.debugColor = new Color(color.r, color.g, color.b, 0.25f);

        SetModuleMarkerColor(marker, color);
        EditorUtility.SetDirty(marker);
        EditorUtility.SetDirty(hitbox);
    }

    private static DamageableModuleHitbox FindModuleHitbox(Transform root, string moduleId)
    {
        if (root == null || string.IsNullOrWhiteSpace(moduleId)) return null;

        DamageableModuleHitbox[] hitboxes = root.GetComponentsInChildren<DamageableModuleHitbox>(true);
        for (int i = 0; i < hitboxes.Length; i++)
        {
            DamageableModuleHitbox hitbox = hitboxes[i];
            if (hitbox != null && hitbox.moduleId == moduleId)
            {
                return hitbox;
            }
        }

        return null;
    }

    private static Vector3 ClampModuleScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Max(0.2f, scale.x),
            Mathf.Max(0.2f, scale.y),
            Mathf.Max(0.2f, scale.z));
    }

    private static void SetModuleMarkerColor(GameObject marker, Color color)
    {
        if (marker == null) return;

        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null) return;

        renderer.sharedMaterial = new Material(shader);
        renderer.sharedMaterial.color = color;
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

    private static void DrawPlates(SerializedProperty plates)
    {
        if (plates == null) return;

        plates.isExpanded = EditorGUILayout.Foldout(plates.isExpanded, $"Бронелисты ({plates.arraySize})", true);
        if (!plates.isExpanded) return;

        EditorGUI.indentLevel++;
        plates.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Количество", plates.arraySize));
        for (int i = 0; i < plates.arraySize; i++)
        {
            SerializedProperty plate = plates.GetArrayElementAtIndex(i);
            SerializedProperty name = plate.FindPropertyRelative("displayNameRu");
            string title = !string.IsNullOrWhiteSpace(name != null ? name.stringValue : "")
                ? name.stringValue
                : $"Бронелист {i + 1}";

            plate.isExpanded = EditorGUILayout.Foldout(plate.isExpanded, title, true);
            if (!plate.isExpanded) continue;

            EditorGUI.indentLevel++;
            DrawRelative(plate, "plateId", "ID бронелиста");
            DrawRelative(plate, "displayNameRu", "Название");
            DrawRelative(plate, "armorDetailId", "ID бронедетали");
            DrawRelative(plate, "armorMm", "Толщина брони, мм");
            DrawRelative(plate, "triangleIndices", "Треугольники mesh");
            DrawRelative(plate, "debugColor", "Цвет отладки");
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawDetails(SerializedProperty details)
    {
        if (details == null) return;

        details.isExpanded = EditorGUILayout.Foldout(details.isExpanded, $"Бронедетали ({details.arraySize})", true);
        if (!details.isExpanded) return;

        EditorGUI.indentLevel++;
        details.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Количество", details.arraySize));
        for (int i = 0; i < details.arraySize; i++)
        {
            SerializedProperty detail = details.GetArrayElementAtIndex(i);
            SerializedProperty name = detail.FindPropertyRelative("displayNameRu");
            string title = !string.IsNullOrWhiteSpace(name != null ? name.stringValue : "")
                ? name.stringValue
                : $"Бронедеталь {i + 1}";

            detail.isExpanded = EditorGUILayout.Foldout(detail.isExpanded, title, true);
            if (!detail.isExpanded) continue;

            EditorGUI.indentLevel++;
            DrawRelative(detail, "detailId", "ID бронедетали");
            DrawRelative(detail, "displayNameRu", "Название");
            DrawRelative(detail, "maxArmorHp", "Максимальная прочность");
            DrawRelative(detail, "armorHp", "Текущая прочность");
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawRelative(SerializedProperty parent, string propertyName, string label, string tooltip = "")
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property == null) return;

        EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), true);
    }

    private static void DrawSummary(MeshArmorBody armor)
    {
        if (armor.plates == null || armor.plates.Count == 0) return;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Бронелисты", EditorStyles.boldLabel);
        for (int i = 0; i < armor.plates.Count; i++)
        {
            MeshArmorPlate plate = armor.plates[i];
            if (plate == null) continue;

            int triangleCount = plate.triangleIndices != null ? plate.triangleIndices.Count : 0;
            MeshArmorDetail detail = FindDetail(armor, plate.armorDetailId);
            float integrity = detail != null ? detail.ArmorIntegrity01 : 1f;
            float currentArmorMm = plate.armorMm * integrity;
            string detailText = detail != null
                ? $"{detail.displayNameRu} {detail.armorHp:0.0}/{detail.maxArmorHp:0.0}"
                : "деталь не найдена";
            EditorGUILayout.LabelField(
                plate.displayNameRu,
                $"{currentArmorMm:0.0}/{plate.armorMm:0.0} мм, {detailText}, треугольники {triangleCount}");
        }
    }

    private static MeshArmorDetail FindDetail(MeshArmorBody armor, string detailId)
    {
        if (armor == null || armor.details == null || string.IsNullOrWhiteSpace(detailId)) return null;

        for (int i = 0; i < armor.details.Count; i++)
        {
            MeshArmorDetail detail = armor.details[i];
            if (detail != null && detail.detailId == detailId)
            {
                return detail;
            }
        }

        return null;
    }
}
