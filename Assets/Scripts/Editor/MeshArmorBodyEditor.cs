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

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Автосборка", EditorStyles.boldLabel);
        DrawProperty("normalAngleThresholdDeg", "Порог угла нормали");
        DrawProperty("planeDistanceThreshold", "Порог расстояния плоскости");
        DrawProperty("defaultArmorMm", "Броня по умолчанию, мм");

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
            Debug.Log($"[Урон] Mesh-броня собрана: бронелистов {armor.plates.Count}. Теперь можно настроить толщину каждого листа.", armor);
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

        armor.owner = ship;
        armor.colliderIsTrigger = false;
        armor.convexColliderForPhysics = true;
        armor.EnsureMeshCollider();
        if (armor.plates == null || armor.plates.Count == 0)
        {
            armor.RebuildPlatesFromMesh();
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
            DrawRelative(plate, "armorMm", "Толщина брони, мм");
            DrawRelative(plate, "triangleIndices", "Треугольники mesh");
            DrawRelative(plate, "debugColor", "Цвет отладки");
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
            EditorGUILayout.LabelField(
                plate.displayNameRu,
                $"{plate.armorMm:0.0} мм, треугольники {triangleCount}");
        }
    }
}
