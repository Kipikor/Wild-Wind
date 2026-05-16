using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SurveyObjectInspectorState))]
public class SurveyObjectInspectorStateDrawer : PropertyDrawer
{
    private const float Gap = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            line.y += line.height + Gap;
            DrawRelative(ref line, property, "known", "Известен игроку", "Да, если любой наблюдатель уже видел объект и записал его координаты.");
            DrawRelative(ref line, property, "coordinatesKnown", "Координаты известны", "Открываются сразу при первом попадании объекта в радиус наблюдения.");
            DrawRelative(ref line, property, "factsComplete", "Сведения собраны полностью", "Полные сведения нужны автопилотам промысла, чтобы выбирать этот объект как цель.");
            DrawRelative(ref line, property, "factsProgressRu", "Сведения собрано", "Сколько сведений набрано из требуемого объема.");
            DrawRelative(ref line, property, "factsProgressPercent", "Сведения собрано, %", "Процент заполнения сведений об объекте.");
            DrawRelative(ref line, property, "factsRemaining", "Сведений осталось", "Сколько единиц наблюдения еще нужно до полного открытия характеристик.");
            DrawRelative(ref line, property, "informationItemRu", "Тип информации", "Какой ресурс научной информации получается при переработке бумаги около этого объекта.");
            DrawRelative(ref line, property, "informationPotentialKg", "Информации всего, кг", "Полный потенциальный объем научной информации в объекте.");
            DrawRelative(ref line, property, "informationExtractedKg", "Информации собрано, кг", "Сколько кг научной информации уже снято с объекта.");
            DrawRelative(ref line, property, "informationProgressPercent", "Информации собрано, %", "Процент снятой научной информации от полного потенциала.");
            DrawRelative(ref line, property, "informationRemainingKg", "Информации осталось, кг", "Сколько кг научной информации еще можно собрать.");
            DrawRelative(ref line, property, "lastKnownPosition", "Последние известные координаты", "Координаты из разведданных или текущая позиция для отладки, если объект еще не известен.");
            DrawRelative(ref line, property, "summaryRu", "Открытая сводка", "Описание появляется после полного сбора сведений и обновляется повторным наблюдением.");
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        height += Gap + GetRelativeHeight(property, "known");
        height += Gap + GetRelativeHeight(property, "coordinatesKnown");
        height += Gap + GetRelativeHeight(property, "factsComplete");
        height += Gap + GetRelativeHeight(property, "factsProgressRu");
        height += Gap + GetRelativeHeight(property, "factsProgressPercent");
        height += Gap + GetRelativeHeight(property, "factsRemaining");
        height += Gap + GetRelativeHeight(property, "informationItemRu");
        height += Gap + GetRelativeHeight(property, "informationPotentialKg");
        height += Gap + GetRelativeHeight(property, "informationExtractedKg");
        height += Gap + GetRelativeHeight(property, "informationProgressPercent");
        height += Gap + GetRelativeHeight(property, "informationRemainingKg");
        height += Gap + GetRelativeHeight(property, "lastKnownPosition");
        height += Gap + GetRelativeHeight(property, "summaryRu");
        return height;
    }

    private static void DrawRelative(ref Rect line, SerializedProperty parent, string propertyName, string label, string tooltip)
    {
        SerializedProperty child = parent.FindPropertyRelative(propertyName);
        if (child == null)
        {
            return;
        }

        line.height = EditorGUI.GetPropertyHeight(child, true);
        EditorGUI.PropertyField(line, child, new GUIContent(label, tooltip), true);
        line.y += line.height + Gap;
    }

    private static float GetRelativeHeight(SerializedProperty parent, string propertyName)
    {
        SerializedProperty child = parent.FindPropertyRelative(propertyName);
        return child != null ? EditorGUI.GetPropertyHeight(child, true) : 0f;
    }
}

[CustomEditor(typeof(GasCloud))]
public class GasCloudEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty script = serializedObject.FindProperty("m_Script");
        if (script != null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(script, new GUIContent("Скрипт"));
            }
        }

        LocalizedInspector.Section("Газовое облако");
        LocalizedInspector.Property(serializedObject, "cloudId", "id облака", "Уникальный id конкретного облака.");
        LocalizedInspector.Property(serializedObject, "cloudTypeId", "Тип облака", "id типа из Gas_cloud_type.csv.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Название для отладки и будущего интерфейса.");
        LocalizedInspector.Property(serializedObject, "condensateItemId", "Ресурс концентрата", "Какой сырой концентрат кладется в груз после сбора.");
        LocalizedInspector.Property(serializedObject, "condensateLitersPerCubicMeter", "Конденсатность, л/м3", "Сколько литров сырого концентрата содержится в одном кубометре облака.");
        LocalizedInspector.Property(serializedObject, "initialVolumeLiters", "Начальный объем, л", "Сколько литров концентрата было в облаке при создании.");
        LocalizedInspector.Property(serializedObject, "remainingVolumeLiters", "Осталось, л", "Сколько литров концентрата еще не собрано.");
        LocalizedInspector.Property(serializedObject, "visualColor", "Цвет облака", "Цвет отладочной визуализации облака.");

        LocalizedInspector.Section("Разведка");
        LocalizedInspector.Property(serializedObject, "survey", "Паспорт разведки", "Известность объекта, прогресс сведений и объем научной информации.");

        serializedObject.ApplyModifiedProperties();
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}
