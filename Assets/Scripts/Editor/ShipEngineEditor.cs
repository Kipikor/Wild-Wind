using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShipEngine))]
[CanEditMultipleObjects]
public class ShipEngineEditor : Editor
{
    SerializedProperty engineNameProp;
    SerializedProperty maxPowerProp;
    SerializedProperty responsivenessProp;
    SerializedProperty startingRPMProp;
    SerializedProperty throttleProp;
    SerializedProperty currentRPMProp;

    void OnEnable()
    {
        engineNameProp = serializedObject.FindProperty("engineName");
        maxPowerProp = serializedObject.FindProperty("maxPower");
        responsivenessProp = serializedObject.FindProperty("responsiveness");
        startingRPMProp = serializedObject.FindProperty("startingRPM");
        throttleProp = serializedObject.FindProperty("throttle");
        currentRPMProp = serializedObject.FindProperty("currentRPM");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Настройки двигателя", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(engineNameProp, new GUIContent("Название двигателя"));
        EditorGUILayout.PropertyField(maxPowerProp, new GUIContent("Макс. мощность (л.с.)"));
        EditorGUILayout.PropertyField(startingRPMProp, new GUIContent("Стартовые обороты (0..1)"));
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Динамика", EditorStyles.boldLabel);
        EditorGUILayout.Slider(responsivenessProp, 0.01f, 2.0f, new GUIContent("Отзывчивость", "Как быстро двигатель набирает обороты. 2.0 - мгновенно, 0.1 - очень медленно."));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Текущее состояние", EditorStyles.boldLabel);

        // Отрисовка газа (Throttle)
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        EditorGUILayout.PropertyField(throttleProp, new GUIContent("Подача топлива (Газ)"));
        EditorGUI.EndDisabledGroup();

        // Отрисовка оборотов (RPM) в виде прогресс-бара
        float rpmValue = currentRPMProp.floatValue;
        Rect rect = EditorGUILayout.GetControlRect(false, 20);
        EditorGUI.ProgressBar(rect, rpmValue, $"Обороты: {(rpmValue * 100f):F0}%");

        EditorGUILayout.Space(5);
        
        // Инфо-панель (только если выделен ОДИН объект)
        if (!serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.Space(5);
            ShipEngine engine = (ShipEngine)target;
            float powerOutput = engine.GetPowerOutput();
            EditorGUILayout.HelpBox($"Текущая отдача: {powerOutput:F0} л.с.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
        
        // Постоянная перерисовка в Play Mode только если окно активно
        if (Application.isPlaying && Resources.FindObjectsOfTypeAll<EditorWindow>().Length > 0)
        {
            Repaint();
        }
    }
}
