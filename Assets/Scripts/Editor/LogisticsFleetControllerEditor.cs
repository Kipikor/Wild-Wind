using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LogisticsFleetController))]
public class LogisticsFleetControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Симуляция");
        LocalizedInspector.Property(serializedObject, "metaGameState", "Мета-игра", "MetaGameState, который хранит склады, сохранение и каталог кораблей.");
        LocalizedInspector.Property(serializedObject, "simulationEnabled", "Симуляция включена", "Если выключить, грузовики не будут продвигаться по маршрутам.");
        LocalizedInspector.Property(serializedObject, "reserveMultiplier", "Запас топлива и клавдия", "Множитель к расчетному расходу на рейс.");
        LocalizedInspector.Property(serializedObject, "cruiseSpeedFactor", "Доля крейсерской скорости", "Доля от максимальной скорости винта для виртуального рейса.");
        LocalizedInspector.Property(serializedObject, "cruisePowerLever", "Ручка мощности в рейсе", "Доля мощности двигателя для расчета расхода топлива.");
        LocalizedInspector.Property(serializedObject, "fallbackSecondsPerItem", "Секунд на 1 кг", "Запасное время погрузки, если остров не найден в конфиге.");
        LocalizedInspector.Property(serializedObject, "defaultClaudiumResourceId", "Ресурс клавдия", "Ресурс, который логистические корабли используют как клавдий по умолчанию.");
        LocalizedInspector.Property(serializedObject, "debugLogging", "Debug logging", "Temporary route event log in Unity Console.");

        LocalizedInspector.Section("Настройка");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("routes"), new GUIContent("Маршруты"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ships"), new GUIContent("Грузовики"), true);

        serializedObject.ApplyModifiedProperties();

        LogisticsFleetController fleet = (LogisticsFleetController)target;
        if (GUILayout.Button("Создать пример, если пусто"))
        {
            Undo.RecordObject(fleet, "Create Logistics Example");
            fleet.CreateExampleSetupIfEmpty();
            EditorUtility.SetDirty(fleet);
        }

        if (Application.isPlaying)
        {
            DrawRuntime(fleet);
        }
        else
        {
            EditorGUILayout.HelpBox("Статусы грузовиков появятся здесь в Play Mode. Маршруты и корабли можно настраивать прямо в списках выше.", MessageType.Info);
        }
    }

    private static void DrawRuntime(LogisticsFleetController fleet)
    {
        LocalizedInspector.Section("Статус");

        MetaGameState meta = fleet.Meta;
        if (meta == null || meta.progress == null)
        {
            EditorGUILayout.HelpBox("Нет MetaGameState или прогресса.", MessageType.Warning);
            return;
        }

        meta.EnsureProgressInitialized();

        if (meta.progress.logisticsShips == null || meta.progress.logisticsShips.Count == 0)
        {
            EditorGUILayout.LabelField("Грузовиков пока нет в прогрессе.");
            return;
        }

        for (int i = 0; i < meta.progress.logisticsShips.Count; i++)
        {
            LogisticsShipState ship = meta.progress.logisticsShips[i];
            if (ship == null) continue;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(ship.displayName) ? ship.shipId : ship.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("id", ship.shipId);
            EditorGUILayout.LabelField("Маршрут", ship.routeId);
            EditorGUILayout.LabelField("Статус", ship.status.ToString());
            EditorGUILayout.LabelField("Остров", ship.currentIslandId);
            EditorGUILayout.LabelField("Цель", string.IsNullOrWhiteSpace(ship.targetIslandId) ? "-" : ship.targetIslandId);
            EditorGUILayout.LabelField("Событие через", FormatCurrentEvent(ship, meta.CurrentProcessUtcNow));
            EditorGUILayout.Vector3Field("Позиция", ship.lastKnownPosition);
            EditorGUILayout.LabelField("Груз", FormatCargo(ship));
            EditorGUILayout.LabelField("Круги маршрута", ship.completedRouteLoops.ToString());
            EditorGUILayout.LabelField("Сообщение", string.IsNullOrWhiteSpace(ship.lastError) ? "-" : ship.lastError);

            EditorGUILayout.EndVertical();
        }
    }

    private static string FormatCurrentEvent(LogisticsShipState ship, DateTime now)
    {
        if (ship == null) return "-";

        if (ship.status == LogisticsShipStatus.Flying)
        {
            return "прибытие " + FormatRemaining(ship.flightArrivesUtcTicks, now);
        }

        if (ship.status == LogisticsShipStatus.Loading)
        {
            return "погрузка " + FormatRemaining(ship.nextEventUtcTicks, now);
        }

        return "-";
    }

    private static string FormatRemaining(long targetUtcTicks, DateTime now)
    {
        if (targetUtcTicks <= 0) return "-";

        TimeSpan remaining = new DateTime(targetUtcTicks, DateTimeKind.Utc) - now;
        if (remaining <= TimeSpan.Zero) return "готово";

        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }

        return $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
    }

    private static string FormatCargo(LogisticsShipState ship)
    {
        if (ship == null || ship.cargo == null || ship.cargo.Count == 0) return "пусто";

        string text = "";
        for (int i = 0; i < ship.cargo.Count; i++)
        {
            ResourceStack stack = ship.cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            if (text.Length > 0) text += ", ";
            text += stack.resourceId + " x" + stack.amount;
        }

        return text.Length > 0 ? text : "пусто";
    }
}
