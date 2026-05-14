using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CapitalResearchStation))]
public class CapitalResearchStationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Столица");
        LocalizedInspector.Property(serializedObject, "metaGameState", "Мета-игра", "MetaGameState, который хранит прогресс, склады и технологии.");
        LocalizedInspector.Property(serializedObject, "islandId", "Остров", "Идентификатор столичного острова из Island.csv.");
        LocalizedInspector.Property(serializedObject, "displayName", "Название", "Название столицы в инспекторе.");

        serializedObject.ApplyModifiedProperties();

        CapitalResearchStation station = (CapitalResearchStation)target;
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Исследования управляются здесь в Play Mode, когда столица создана из Island.csv.", MessageType.Info);
            return;
        }

        MetaGameState meta = station.Meta;
        if (meta == null)
        {
            EditorGUILayout.HelpBox("В сцене не найден MetaGameState.", MessageType.Warning);
            return;
        }

        meta.EnsureProgressInitialized();
        meta.AdvanceRealTimeProcesses(meta.CurrentProcessUtcNow);

        DrawDockState(station, meta);
        DrawCapitalStorage(meta);
        DrawTechnologies(meta);

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private static void DrawDockState(CapitalResearchStation station, MetaGameState meta)
    {
        LocalizedInspector.Section("Состояние");
        EditorGUILayout.LabelField("Текущий док", meta.progress.currentDockId);
        EditorGUILayout.LabelField("Стыковка со столицей", station.IsPlayerDockedHere ? "да" : "нет");

        if (!station.IsPlayerDockedHere)
        {
            EditorGUILayout.HelpBox("Новое исследование можно выбрать только когда корабль состыкован со столицей. Уже запущенные циклы продолжают идти на складе столицы.", MessageType.Info);
        }
    }

    private static void DrawCapitalStorage(MetaGameState meta)
    {
        LocalizedInspector.Section("Склад столицы");

        IslandProductionState storage = meta.GetCapitalStorageState();
        if (storage == null || storage.storage == null || storage.storage.Count == 0)
        {
            EditorGUILayout.LabelField("Склад пуст.");
            return;
        }

        for (int i = 0; i < storage.storage.Count; i++)
        {
            ResourceStack stack = storage.storage[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            EditorGUILayout.LabelField(stack.resourceId, stack.amount.ToString());
        }
    }

    private static void DrawTechnologies(MetaGameState meta)
    {
        LocalizedInspector.Section("Технологии");

        IReadOnlyList<TechnologyConfig> technologies = meta.GetTechnologyConfigs();
        if (technologies == null || technologies.Count == 0)
        {
            EditorGUILayout.HelpBox("Technology.csv не загружен или пуст.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < technologies.Count; i++)
        {
            TechnologyConfig technology = technologies[i];
            if (technology == null || string.IsNullOrWhiteSpace(technology.id)) continue;

            DrawTechnology(meta, technology);
        }
    }

    private static void DrawTechnology(MetaGameState meta, TechnologyConfig technology)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.LabelField(meta.GetTechnologyDisplayName(technology), EditorStyles.boldLabel);
        EditorGUILayout.LabelField("id", technology.id);
        EditorGUILayout.LabelField("Статус", meta.GetTechnologyStatusText(technology));
        EditorGUILayout.LabelField("Цикл", $"{technology.cycleTimeSeconds} сек, {technology.requiredCycles} циклов");
        EditorGUILayout.LabelField("Цена цикла", meta.FormatTechnologyCycleCost(technology));
        EditorGUILayout.LabelField("Зависимости", FormatPrerequisites(technology));

        TechnologyResearchProgress progress = meta.GetTechnologyResearchProgress(technology.id);
        if (progress != null && progress.HasActiveCycle)
        {
            float duration = Mathf.Max(0.001f, (progress.activeCycleEndUtcTicks - progress.activeCycleStartUtcTicks) / (float)System.TimeSpan.TicksPerSecond);
            float elapsed = Mathf.Clamp((meta.CurrentProcessUtcNow.Ticks - progress.activeCycleStartUtcTicks) / (float)System.TimeSpan.TicksPerSecond, 0f, duration);
            Rect rect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(rect, elapsed / duration, "Текущий цикл");
        }

        bool canSelect = meta.CanSelectResearchTechnology(technology, out string reason);
        GUI.enabled = canSelect;
        if (GUILayout.Button(meta.progress.activeResearchTechnologyId == technology.id ? "Выбрано" : "Исследовать"))
        {
            meta.TrySelectResearchTechnology(technology.id);
            EditorUtility.SetDirty(meta);
        }

        GUI.enabled = true;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            EditorGUILayout.HelpBox(reason, MessageType.None);
        }

        EditorGUILayout.EndVertical();
    }

    private static string FormatPrerequisites(TechnologyConfig technology)
    {
        if (technology == null || technology.prerequisiteTechnologyIds == null || technology.prerequisiteTechnologyIds.Count == 0)
        {
            return "нет";
        }

        List<string> ids = new List<string>();
        for (int i = 0; i < technology.prerequisiteTechnologyIds.Count; i++)
        {
            string id = technology.prerequisiteTechnologyIds[i];
            if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
        }

        return ids.Count > 0 ? string.Join(", ", ids) : "нет";
    }
}
