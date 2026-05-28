using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GasHarvestingSetupEditor
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";

    public static void PrepareStarterGasShipMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        PrepareStarterGasShip(meta);
        Debug.Log("[Газовый харвестинг] Стартовый корабль подготовлен: установлен харвестер облаков, добавлены топливо и клавдий.");
    }

    public static void PrepareGasTestSceneMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        PrepareStarterGasShip(meta);
        PrepareExampleGasAutopilot(meta);
        StockAllIslandTestFuel(meta);
        Debug.Log("[Газовый харвестинг] Тестовая сцена подготовлена: связи сцены, стартовый корабль и пример автопилота готовы.");
    }

    public static void StockAllIslandTestFuelMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        int changed = StockAllIslandTestFuel(meta);
        Debug.Log("[Газовый харвестинг] Тестовое топливо и клавдий на островах пополнены. Добавлено/поднято до минимума: " + changed + " кг.");
    }

    public static void PrepareStarterGasShip(MetaGameState meta)
    {
        if (meta == null) return;

        EnsureSceneLinks(meta);
        Undo.RecordObject(meta, "Prepare starter gas ship");

        meta.EnsureProgressInitialized();
        meta.progress.SelectHull("starter_hull");
        ShipAssemblyBuilder.AutoInstallRequiredModules(meta.CurrentCatalog, meta.techTree, meta.progress, out _);
        meta.progress.InstallModule("utility_01", "starter_gas_harvester");
        meta.progress.SetShipCargoAmount("charcoal", 80);
        meta.progress.SetShipCargoAmount("claudium", 35);
        meta.ApplySelectedShip();

        EditorUtility.SetDirty(meta);
    }

    private static MetaGameState FindOrCreateMetaGameState()
    {
        MetaGameState meta = Object.FindFirstObjectByType<MetaGameState>();
        if (meta != null) return meta;

        GameObject gameObject = new GameObject("MetaGameState");
        Undo.RegisterCreatedObjectUndo(gameObject, "Create MetaGameState");
        return gameObject.AddComponent<MetaGameState>();
    }

    private static void EnsureSceneLinks(MetaGameState meta)
    {
        Undo.RecordObject(meta, "Prepare gas harvesting scene");

        ShipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(CatalogPath);
        if (catalog != null)
        {
            meta.catalog = catalog;
        }

        TechTreeDefinitionSO techTree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TechTreePath);
        if (techTree != null)
        {
            meta.techTree = techTree;
        }

        if (meta.shipLoader == null)
        {
            meta.shipLoader = Object.FindFirstObjectByType<ShipLoader>();
        }

        if (meta.shipLoader == null)
        {
            GameObject loaderObject = new GameObject("ShipLoader");
            Undo.RegisterCreatedObjectUndo(loaderObject, "Create ShipLoader");
            meta.shipLoader = loaderObject.AddComponent<ShipLoader>();
        }

        meta.shipLoader.catalog = meta.catalog;
        if (meta.shipLoader.targetShip == null)
        {
            meta.shipLoader.targetShip = Object.FindFirstObjectByType<ShipPhysics>();
        }

        if (meta.gasCloudManager == null)
        {
            meta.gasCloudManager = Object.FindFirstObjectByType<GasCloudManager>();
        }

        if (meta.gasCloudManager == null)
        {
            meta.gasCloudManager = Undo.AddComponent<GasCloudManager>(meta.gameObject);
        }

        meta.gasCloudManager.metaGameState = meta;
        meta.gasCloudManager.spawnConfigCloudsOnPlay = true;

        if (meta.gasHarvesterFleet == null)
        {
            meta.gasHarvesterFleet = Object.FindFirstObjectByType<GasHarvesterFleetController>();
        }

        if (meta.gasHarvesterFleet == null)
        {
            meta.gasHarvesterFleet = Undo.AddComponent<GasHarvesterFleetController>(meta.gameObject);
        }

        meta.gasHarvesterFleet.metaGameState = meta;
        meta.spawnConfigIslandsOnPlay = true;

        EditorUtility.SetDirty(meta);
        EditorUtility.SetDirty(meta.shipLoader);
        EditorUtility.SetDirty(meta.gasCloudManager);
        EditorUtility.SetDirty(meta.gasHarvesterFleet);
    }

    private static void PrepareExampleGasAutopilot(MetaGameState meta)
    {
        if (meta == null || meta.gasHarvesterFleet == null) return;

        Undo.RecordObject(meta.gasHarvesterFleet, "Prepare example gas autopilot");
        meta.gasHarvesterFleet.CreateExampleSetupIfEmpty();
        meta.gasHarvesterFleet.debugLogging = true;

        meta.EnsureProgressInitialized();
        StockAllIslandTestFuel(meta);

        meta.gasHarvesterFleet.EnsureRuntimeShips(meta.progress);
        EditorUtility.SetDirty(meta.gasHarvesterFleet);
        EditorUtility.SetDirty(meta);
    }

    private static int StockAllIslandTestFuel(MetaGameState meta)
    {
        if (meta == null || meta.gasHarvesterFleet == null) return 0;

        meta.EnsureProgressInitialized();
        int changed = meta.gasHarvesterFleet.DebugStockAllIslandFuelAndClaudium(meta.WorldConfig, meta.progress);
        EditorUtility.SetDirty(meta);
        return changed;
    }
}

[CustomEditor(typeof(GasCloudManager))]
public class GasCloudManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GasCloudManager manager = (GasCloudManager)target;
        if (Application.isPlaying && GUILayout.Button("Пересоздать облака из конфигов"))
        {
            manager.SpawnConfiguredClouds(true);
        }
    }
}

[CustomEditor(typeof(GasHarvesterFleetController))]
public class GasHarvesterFleetControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GasHarvesterFleetController fleet = (GasHarvesterFleetController)target;
        if (GUILayout.Button("Создать пример, если пусто"))
        {
            Undo.RecordObject(fleet, "Create gas harvester example");
            fleet.CreateExampleSetupIfEmpty();
            EditorUtility.SetDirty(fleet);
        }

        MetaGameState meta = fleet.Meta;

        if (meta != null && GUILayout.Button("Засыпать тестовое топливо на острова"))
        {
            meta.EnsureProgressInitialized();
            int changed = fleet.DebugStockAllIslandFuelAndClaudium(meta.WorldConfig, meta.progress);
            EditorUtility.SetDirty(meta);
            Debug.Log("[Газовый харвестинг] Тестовое топливо и клавдий на островах пополнены. Добавлено/поднято до минимума: " + changed + " кг.", fleet);
        }

        if (Application.isPlaying)
        {
            DrawRuntime(fleet);
        }
        else
        {
            EditorGUILayout.HelpBox("Статусы газовых сборщиков появятся здесь в Play Mode. Автопилоты можно настраивать прямо в списке выше.", MessageType.Info);
        }
    }

    private static void DrawRuntime(GasHarvesterFleetController fleet)
    {
        LocalizedInspector.Section("Статус");

        MetaGameState meta = fleet.Meta;
        if (meta == null || meta.progress == null)
        {
            EditorGUILayout.HelpBox("Нет MetaGameState или прогресса.", MessageType.Warning);
            return;
        }

        meta.EnsureProgressInitialized();

        if (meta.progress.gasHarvesterShips == null || meta.progress.gasHarvesterShips.Count == 0)
        {
            EditorGUILayout.LabelField("Газовых сборщиков пока нет в прогрессе.");
            return;
        }

        for (int i = 0; i < meta.progress.gasHarvesterShips.Count; i++)
        {
            GasHarvesterShipState state = meta.progress.gasHarvesterShips[i];
            if (state == null) continue;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(state.displayName) ? state.shipId : state.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("id", state.shipId);
            EditorGUILayout.LabelField("Дом", state.homeIslandId);
            EditorGUILayout.LabelField("Статус", FormatStatus(state.status));
            EditorGUILayout.LabelField("Цель", string.IsNullOrWhiteSpace(state.targetCloudId) ? "-" : state.targetCloudId);
            EditorGUILayout.LabelField("Событие через", FormatCurrentEvent(state, meta.CurrentProcessUtcNow));
            EditorGUILayout.Vector3Field("Позиция", state.lastKnownPosition);
            EditorGUILayout.LabelField("Груз", FormatCargo(state.cargo));
            EditorGUILayout.LabelField("Буфер", state.harvestBufferKg.ToString("0.00") + " кг");
            EditorGUILayout.LabelField("Рейсы", state.completedTrips.ToString());
            EditorGUILayout.LabelField("Сообщение", string.IsNullOrWhiteSpace(state.lastError) ? "-" : state.lastError);
            EditorGUILayout.EndVertical();
        }
    }

    private static string FormatStatus(GasHarvesterShipStatus status)
    {
        return status switch
        {
            GasHarvesterShipStatus.Idle => "Ожидает",
            GasHarvesterShipStatus.FlyingToCloud => "Летит к облаку",
            GasHarvesterShipStatus.Harvesting => "Добывает",
            GasHarvesterShipStatus.Returning => "Возвращается",
            GasHarvesterShipStatus.Unloading => "Разгружается",
            GasHarvesterShipStatus.WaitingForResources => "Ждет ресурсы",
            GasHarvesterShipStatus.Error => "Ошибка",
            _ => status.ToString()
        };
    }

    private static string FormatCurrentEvent(GasHarvesterShipState state, DateTime now)
    {
        if (state == null) return "-";

        if (state.status == GasHarvesterShipStatus.FlyingToCloud)
        {
            return "прибытие к облаку " + FormatRemaining(state.flightArrivesUtcTicks, now);
        }

        if (state.status == GasHarvesterShipStatus.Harvesting)
        {
            return "цикл добычи " + FormatRemaining(state.nextEventUtcTicks, now);
        }

        if (state.status == GasHarvesterShipStatus.Returning)
        {
            return "возврат домой " + FormatRemaining(state.flightArrivesUtcTicks, now);
        }

        if (state.status == GasHarvesterShipStatus.Unloading)
        {
            return "разгрузка " + FormatRemaining(state.nextEventUtcTicks, now);
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

    private static string FormatCargo(System.Collections.Generic.List<ResourceStack> cargo)
    {
        if (cargo == null || cargo.Count == 0) return "пусто";

        string text = "";
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            if (text.Length > 0) text += ", ";
            text += stack.resourceId + " x" + stack.amount;
        }

        return text.Length > 0 ? text : "пусто";
    }
}
