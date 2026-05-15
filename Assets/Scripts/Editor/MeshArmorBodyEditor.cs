using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshArmorBody))]
public class MeshArmorBodyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshArmorBody armor = (MeshArmorBody)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Подготовка ProBuilder/Mesh брони", EditorStyles.boldLabel);

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

        if (GUILayout.Button("Сбросить прочность бронелистов"))
        {
            Undo.RecordObject(armor, "Reset mesh armor hp");
            armor.ResetArmorState();
            EditorUtility.SetDirty(armor);
            Debug.Log("[Урон] Прочность mesh-бронелистов сброшена.", armor);
        }

        DrawSummary(armor);
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
                $"{plate.CurrentArmorMm:0.0}/{plate.armorMm:0.0} мм, прочность {plate.armorHp:0.0}/{plate.maxArmorHp:0.0}, треугольники {triangleCount}");
        }
    }
}
