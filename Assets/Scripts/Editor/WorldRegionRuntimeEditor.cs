using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WorldRegionRuntime))]
public sealed class WorldRegionRuntimeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        WorldRegionRuntime runtime = (WorldRegionRuntime)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Сгенерировать регион", GUILayout.Height(26f)))
            {
                Undo.RecordObject(runtime, "Generate world region");
                runtime.GenerateStarterRegion();
                EditorUtility.SetDirty(runtime);
            }

            if (GUILayout.Button("Сводка в лог", GUILayout.Height(26f)))
            {
                runtime.LogWorldSummary();
            }
        }

        EditorGUILayout.HelpBox(
            "Это пока модель мира-данных: 100 чанков по 10 км. Детальные GameObject должны появляться только в активном пузыре вокруг корабля.",
            MessageType.Info);
    }
}
