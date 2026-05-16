using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MiningSetupEditor
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";

    [MenuItem("Wild Wind/Mining/Prepare Starter Mining Ship")]
    public static void PrepareStarterMiningShipMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        PrepareStarterMiningShip(meta);
        Debug.Log("[Майнинг] Стартовый корабль подготовлен: установлен противоударный кузов, добавлены топливо и клавдий.");
    }

    [MenuItem("Wild Wind/Mining/Prepare Mining Test Scene")]
    public static void PrepareMiningTestSceneMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        PrepareStarterMiningShip(meta);
        PrepareExampleMiningAutopilot(meta);
        StockAllIslandTestFuel(meta);
        Debug.Log("[Майнинг] Тестовая сцена подготовлена: связи сцены, стартовый корабль и пример автопилота готовы.");
    }

    [MenuItem("Wild Wind/Mining/Stock All Islands Test Fuel")]
    public static void StockAllIslandTestFuelMenu()
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);
        int changed = StockAllIslandTestFuel(meta);
        Debug.Log("[Майнинг] Тестовое топливо и клавдий на островах пополнены. Добавлено/поднято до минимума: " + changed + " кг.");
    }

    public static void PrepareStarterMiningShip(MetaGameState meta)
    {
        if (meta == null) return;

        EnsureSceneLinks(meta);
        Undo.RecordObject(meta, "Prepare starter mining ship");

        meta.EnsureProgressInitialized();
        meta.progress.SelectHull("starter_hull");
        ShipAssemblyBuilder.AutoInstallRequiredModules(meta.CurrentCatalog, meta.techTree, meta.progress, out _);
        meta.progress.InstallModule("utility_01", "starter_mining_hold");
        meta.progress.SetShipCargoAmount("wood", 100);
        meta.progress.SetShipCargoAmount("claudium", 50);
        meta.progress.SetShipCargoAmount("weapon", 5);
        meta.progress.shipWeaponSpendBufferKg = 0f;
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
        if (meta == null) return;
        Undo.RecordObject(meta, "Prepare mining scene");

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

        if (meta.miningRockManager == null)
        {
            meta.miningRockManager = Object.FindFirstObjectByType<MiningRockManager>();
        }

        if (meta.miningRockManager == null)
        {
            meta.miningRockManager = Undo.AddComponent<MiningRockManager>(meta.gameObject);
        }

        meta.miningRockManager.metaGameState = meta;
        meta.miningRockManager.spawnRocksOnPlay = true;

        if (meta.miningFleet == null)
        {
            meta.miningFleet = Object.FindFirstObjectByType<MiningFleetController>();
        }

        if (meta.miningFleet == null)
        {
            meta.miningFleet = Undo.AddComponent<MiningFleetController>(meta.gameObject);
        }

        meta.miningFleet.metaGameState = meta;
        meta.spawnConfigIslandsOnPlay = true;

        EditorUtility.SetDirty(meta);
        EditorUtility.SetDirty(meta.shipLoader);
        EditorUtility.SetDirty(meta.miningRockManager);
        EditorUtility.SetDirty(meta.miningFleet);
    }

    private static void PrepareExampleMiningAutopilot(MetaGameState meta)
    {
        if (meta == null || meta.miningFleet == null) return;

        Undo.RecordObject(meta.miningFleet, "Prepare example mining autopilot");
        meta.miningFleet.CreateExampleSetupIfEmpty();
        meta.miningFleet.debugLogging = true;

        meta.EnsureProgressInitialized();
        StockAllIslandTestFuel(meta);

        meta.miningFleet.EnsureRuntimeShips(meta.progress);
        EditorUtility.SetDirty(meta.miningFleet);
        EditorUtility.SetDirty(meta);
    }

    private static int StockAllIslandTestFuel(MetaGameState meta)
    {
        if (meta == null || meta.miningFleet == null) return 0;

        meta.EnsureProgressInitialized();
        int changed = meta.miningFleet.DebugStockAllIslandFuelAndClaudium(meta.WorldConfig, meta.progress);
        EditorUtility.SetDirty(meta);
        return changed;
    }
}

[CustomEditor(typeof(MiningRockManager))]
public class MiningRockManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MiningRockManager manager = (MiningRockManager)target;
        if (Application.isPlaying && GUILayout.Button("Пересоздать рудные глыбы из конфигов"))
        {
            manager.SpawnConfiguredRocks(true);
        }
    }
}

[CustomEditor(typeof(MiningRock))]
public class MiningRockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MiningRock rock = (MiningRock)target;
        MiningRockState state = rock.ProgressState;
        MiningZoneConfig zone = rock.ZoneConfig;
        OreTypeConfig oreType = rock.OreTypeConfig;
        MetaGameState meta = rock.Meta;

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Статус глыбы", EditorStyles.boldLabel);

        if (state == null || zone == null)
        {
            EditorGUILayout.HelpBox("Глыба еще не связана с сохраненным состоянием.", MessageType.Info);
            return;
        }

        DateTime now = meta != null ? meta.CurrentProcessUtcNow : DateTime.UtcNow;
        long nowTicks = now.Ticks;
        float ageSeconds = state.spawnedUtcTicks > 0
            ? Mathf.Max(0f, (float)new TimeSpan(nowTicks - state.spawnedUtcTicks).TotalSeconds)
            : 0f;
        float timeToFallSeconds = Mathf.Max(0f, zone.LifetimeSeconds - ageSeconds);
        float timeToApexSeconds = Mathf.Max(0f, zone.ascentDurationSeconds - ageSeconds);
        float timeToNextShedSeconds = state.nextNaturalShedUtcTicks > 0
            ? Mathf.Max(0f, (float)new TimeSpan(state.nextNaturalShedUtcTicks - nowTicks).TotalSeconds)
            : -1f;

        EditorGUILayout.LabelField("id", state.rockId);
        EditorGUILayout.LabelField("Руда", oreType != null ? $"{oreType.localNameRu} ({oreType.oreItemId})" : state.oreTypeId);
        EditorGUILayout.LabelField("Взлетает до", zone.apexY.ToString("0") + " м");
        EditorGUILayout.LabelField("До вершины", timeToApexSeconds > 0f ? FormatDuration(timeToApexSeconds) : "уже снижается");
        EditorGUILayout.LabelField("Упадет в бурю через", FormatDuration(timeToFallSeconds));
        EditorGUILayout.LabelField("Осталось в глыбе", $"{state.remainingOreKg:0} / {zone.rockOreKg:0} кг");
        EditorGUILayout.LabelField("Следующий обвал", timeToNextShedSeconds >= 0f ? FormatDuration(timeToNextShedSeconds) : "-");
        EditorGUILayout.LabelField("Последний обвал", state.lastNaturalShedAmountKg > 0 ? $"{state.lastNaturalShedAmountKg} кг" : "-");
        EditorGUILayout.Vector3Field("Позиция", rock.transform.position);
    }

    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }

    private static string FormatDuration(float seconds)
    {
        TimeSpan value = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        if (value.TotalHours >= 1d)
        {
            return $"{(int)value.TotalHours:D2}:{value.Minutes:D2}:{value.Seconds:D2}";
        }

        return $"{value.Minutes:D2}:{value.Seconds:D2}";
    }
}

[CustomEditor(typeof(MiningFleetController))]
public class MiningFleetControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MiningFleetController fleet = (MiningFleetController)target;
        if (GUILayout.Button("Создать пример, если пусто"))
        {
            Undo.RecordObject(fleet, "Create mining example");
            fleet.CreateExampleSetupIfEmpty();
            EditorUtility.SetDirty(fleet);
        }

        MetaGameState meta = fleet.Meta;
        if (meta != null && GUILayout.Button("Засыпать тестовое топливо на острова"))
        {
            meta.EnsureProgressInitialized();
            int changed = fleet.DebugStockAllIslandFuelAndClaudium(meta.WorldConfig, meta.progress);
            EditorUtility.SetDirty(meta);
            Debug.Log("[Майнинг] Тестовое топливо и клавдий на островах пополнены. Добавлено/поднято до минимума: " + changed + " кг.", fleet);
        }

        if (Application.isPlaying)
        {
            DrawRuntime(fleet);
        }
        else
        {
            EditorGUILayout.HelpBox("Статусы майнеров появятся здесь в Play Mode. Автопилоты можно настраивать прямо в списке выше.", MessageType.Info);
        }
    }

    private static void DrawRuntime(MiningFleetController fleet)
    {
        LocalizedInspector.Section("Статус");

        MetaGameState meta = fleet.Meta;
        if (meta == null || meta.progress == null)
        {
            EditorGUILayout.HelpBox("Нет MetaGameState или прогресса.", MessageType.Warning);
            return;
        }

        meta.EnsureProgressInitialized();

        if (meta.progress.miningShips == null || meta.progress.miningShips.Count == 0)
        {
            EditorGUILayout.LabelField("Майнинговых автопилотов пока нет в прогрессе.");
            return;
        }

        for (int i = 0; i < meta.progress.miningShips.Count; i++)
        {
            MiningShipState state = meta.progress.miningShips[i];
            if (state == null) continue;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(state.displayName) ? state.shipId : state.displayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("id", state.shipId);
            EditorGUILayout.LabelField("Дом", state.homeIslandId);
            EditorGUILayout.LabelField("Статус", FormatStatus(state.status));
            EditorGUILayout.LabelField("Глыба", string.IsNullOrWhiteSpace(state.targetRockId) ? "-" : state.targetRockId);
            EditorGUILayout.LabelField("Руда", string.IsNullOrWhiteSpace(state.targetOreTypeId) ? "-" : state.targetOreTypeId);
            EditorGUILayout.LabelField("Событие через", FormatCurrentEvent(state, meta.CurrentProcessUtcNow));
            EditorGUILayout.Vector3Field("Позиция", state.lastKnownPosition);
            EditorGUILayout.LabelField("Груз", FormatCargo(state.cargo));
            EditorGUILayout.LabelField("Буфер", state.miningBufferKg.ToString("0.00") + " кг");
            EditorGUILayout.LabelField("Рейсы", state.completedTrips.ToString());
            EditorGUILayout.LabelField("Сообщение", string.IsNullOrWhiteSpace(state.lastError) ? "-" : state.lastError);
            EditorGUILayout.EndVertical();
        }
    }

    private static string FormatStatus(MiningShipStatus status)
    {
        return status switch
        {
            MiningShipStatus.Idle => "Ожидает",
            MiningShipStatus.FlyingToRock => "Летит к глыбе",
            MiningShipStatus.Mining => "Ждет осыпь",
            MiningShipStatus.Returning => "Возвращается",
            MiningShipStatus.Unloading => "Разгружается",
            MiningShipStatus.WaitingForResources => "Ждет ресурсы",
            MiningShipStatus.Error => "Ошибка",
            _ => status.ToString()
        };
    }

    private static string FormatCurrentEvent(MiningShipState state, DateTime now)
    {
        if (state == null) return "-";

        if (state.status == MiningShipStatus.FlyingToRock)
        {
            return "прибытие к глыбе " + FormatRemaining(state.flightArrivesUtcTicks, now);
        }

        if (state.status == MiningShipStatus.Mining)
        {
            return "проверка осыпи " + FormatRemaining(state.nextEventUtcTicks, now);
        }

        if (state.status == MiningShipStatus.Returning)
        {
            return "возврат домой " + FormatRemaining(state.flightArrivesUtcTicks, now);
        }

        if (state.status == MiningShipStatus.Unloading)
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
