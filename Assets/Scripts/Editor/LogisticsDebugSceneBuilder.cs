using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LogisticsDebugSceneBuilder
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";
    private const string DebugSaveFileName = "wild_wind_logistics_debug_save.json";

    [MenuItem("Wild Wind/Logistics/Build Debug Scene")]
    public static void BuildDebugScene()
    {
        ShipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(CatalogPath);
        TechTreeDefinitionSO techTree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TechTreePath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Logistics debug", "Ship catalog is missing: " + CatalogPath, "OK");
            return;
        }

        GameObject metaObject = FindOrCreateMetaObject();
        ShipLoader loader = EnsureComponent<ShipLoader>(metaObject);
        MetaGameState meta = EnsureComponent<MetaGameState>(metaObject);
        LogisticsFleetController fleet = EnsureComponent<LogisticsFleetController>(metaObject);

        Transform spawnPoint = FindOrCreateTransform("Logistics Debug Ship Spawn", new Vector3(0f, 120f, -120f));
        Undo.RecordObjects(new UnityEngine.Object[] { loader, meta, fleet, spawnPoint }, "Build logistics debug scene");

        loader.catalog = catalog;
        loader.spawnPoint = spawnPoint;
        loader.targetShip = UnityEngine.Object.FindFirstObjectByType<ShipPhysics>();
        loader.destroySpawnedShipOnRebuild = true;
        loader.disableSceneShipWhenSpawning = true;

        meta.catalog = catalog;
        meta.techTree = techTree;
        meta.shipLoader = loader;
        meta.logisticsFleet = fleet;
        meta.startingDockId = "capital";
        meta.startingDockKind = DockingLocationKind.Island;
        meta.startingMode = GameSessionMode.Docked;
        meta.loadSavedGameOnAwake = false;
        meta.saveFileName = DebugSaveFileName;
        meta.processRealTimeWhilePlaying = true;
        meta.islandProductionEnabled = false;
        meta.gameTimeScale = 8f;
        meta.accelerateUnityTimeScale = true;
        meta.maxUnityTimeScale = 4f;
        meta.maxAcceleratedProcessStepSeconds = 10f;
        meta.processOfflineProgressOnLoad = true;
        meta.maxOfflineCatchUpHours = 240f;
        meta.spawnConfigIslandsOnPlay = true;
        meta.configIslandVisualRadius = 18f;
        meta.showDockingDebugUI = true;

        ConfigureFleet(fleet, meta);
        ConfigureProgress(meta);
        EnsureCameraAndLight();

        EditorUtility.SetDirty(loader);
        EditorUtility.SetDirty(meta);
        EditorUtility.SetDirty(fleet);
        EditorSceneManager.MarkSceneDirty(metaObject.scene);
        Selection.activeGameObject = metaObject;

        Debug.Log("[LogisticsDebug] Debug scene configured. Enter Play Mode or run Wild Wind/Logistics/Simulate 10 Minutes.");
    }

    [MenuItem("Wild Wind/Logistics/Simulate 10 Minutes")]
    public static void SimulateTenMinutes()
    {
        MetaGameState meta = UnityEngine.Object.FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            EditorUtility.DisplayDialog("Logistics debug", "No MetaGameState found. Run Build Debug Scene first.", "OK");
            return;
        }

        if (meta.logisticsFleet == null)
        {
            meta.logisticsFleet = UnityEngine.Object.FindFirstObjectByType<LogisticsFleetController>();
        }

        if (meta.logisticsFleet != null)
        {
            meta.logisticsFleet.debugLogging = true;
        }

        meta.EnsureProgressInitialized();
        if (meta.progress.lastProcessUtcTicks <= 0)
        {
            meta.progress.lastProcessUtcTicks = DateTime.UtcNow.Ticks;
        }

        DateTime from = new DateTime(meta.progress.lastProcessUtcTicks, DateTimeKind.Utc);
        DateTime to = from.AddMinutes(10);
        bool previousIslandProduction = meta.islandProductionEnabled;
        meta.islandProductionEnabled = false;
        int changed;
        try
        {
            changed = meta.AdvanceRealTimeProcesses(to);
        }
        finally
        {
            meta.islandProductionEnabled = previousIslandProduction;
        }

        Debug.Log("[LogisticsDebug] Simulated 10 logistics minutes, changed events=" + changed + ". Island production was disabled for this editor check.");
        LogFleetSummary(meta);

        EditorUtility.SetDirty(meta);
        if (meta.logisticsFleet != null)
        {
            EditorUtility.SetDirty(meta.logisticsFleet);
        }
    }

    [MenuItem("Wild Wind/Meta/Fast Forward 100 Hours")]
    public static void FastForwardHundredHours()
    {
        MetaGameState meta = UnityEngine.Object.FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            EditorUtility.DisplayDialog("Fast forward", "No MetaGameState found.", "OK");
            return;
        }

        meta.EnsureProgressInitialized();
        bool previousDebugLogging = meta.logisticsFleet != null && meta.logisticsFleet.debugLogging;
        if (meta.logisticsFleet != null)
        {
            meta.logisticsFleet.debugLogging = false;
        }

        int changed;
        try
        {
            changed = meta.FastForwardSimulationHours(100f);
        }
        finally
        {
            if (meta.logisticsFleet != null)
            {
                meta.logisticsFleet.debugLogging = previousDebugLogging;
            }
        }

        Debug.Log("[MetaDebug] Fast-forwarded 100 hours, changed events=" + changed + ".");

        EditorUtility.SetDirty(meta);
        if (meta.logisticsFleet != null)
        {
            EditorUtility.SetDirty(meta.logisticsFleet);
        }
    }

    private static void ConfigureFleet(LogisticsFleetController fleet, MetaGameState meta)
    {
        fleet.metaGameState = meta;
        fleet.simulationEnabled = true;
        fleet.reserveMultiplier = 1.15f;
        fleet.cruiseSpeedFactor = 0.8f;
        fleet.cruisePowerLever = 0.75f;
        fleet.fallbackSecondsPerItem = 1f;
        fleet.defaultClaudiumResourceId = "claudium";
        fleet.debugLogging = true;

        fleet.routes = new List<LogisticsRouteDefinition>
        {
            new LogisticsRouteDefinition
            {
                routeId = "debug_food_charcoal_tools",
                displayName = "Debug food charcoal tools",
                loop = true,
                stops = new List<LogisticsRouteStop>
                {
                    Stop("Island1", null, Orders(("food", 20))),
                    Stop("Island6", Orders(("food", 10)), Orders(("charcoal", 10))),
                    Stop("Island5", Orders(("food", 10), ("charcoal", 10)), Orders(("tools", 20)))
                }
            },
            new LogisticsRouteDefinition
            {
                routeId = "debug_tools_food_reverse",
                displayName = "Debug reverse tools food",
                loop = true,
                stops = new List<LogisticsRouteStop>
                {
                    Stop("Island5", null, Orders(("tools", 10))),
                    Stop("Island6", Orders(("tools", 5)), Orders(("charcoal", 10))),
                    Stop("Island1", Orders(("tools", 5), ("charcoal", 10)), Orders(("food", 10)))
                }
            }
        };

        fleet.ships = new List<LogisticsShipDefinition>
        {
            Ship("debug_logi_01", "Debug Hauler 01", "debug_food_charcoal_tools", "Island1"),
            Ship("debug_logi_02", "Debug Hauler 02", "debug_tools_food_reverse", "Island5")
        };
    }

    private static void ConfigureProgress(MetaGameState meta)
    {
        meta.progress = new PlayerProgress();
        meta.progress.SetDocked("capital", DockingLocationKind.Island);
        meta.progress.receivedStartingInventory = true;
        meta.progress.lastProcessUtcTicks = DateTime.UtcNow.Ticks;
        meta.progress.nextShopRefreshUtcTicks = DateTime.UtcNow.AddHours(1).Ticks;
        meta.progress.shopSeed = 1;
        meta.progress.SetShipCargoAmount("charcoal", 50);
        meta.progress.SetShipCargoAmount("claudium", 25);

        PrimeStorage(meta.progress, "capital", ("charcoal", 500), ("claudium", 500), ("food", 200), ("tools", 200), ("metal", 100), ("mechanisms", 100));
        PrimeStorage(meta.progress, "Island1", ("food", 500), ("charcoal", 500), ("claudium", 500));
        PrimeStorage(meta.progress, "Island5", ("tools", 500), ("charcoal", 500), ("claudium", 500));
        PrimeStorage(meta.progress, "Island6", ("charcoal", 500), ("food", 200), ("tools", 200), ("claudium", 500));

        meta.progress.logisticsShips.Clear();
        meta.logisticsFleet?.EnsureRuntimeShips(meta.progress);
    }

    private static LogisticsRouteStop Stop(string islandId, List<LogisticsCargoOrder> unload, List<LogisticsCargoOrder> load)
    {
        return new LogisticsRouteStop
        {
            islandId = islandId,
            unload = unload ?? new List<LogisticsCargoOrder>(),
            load = load ?? new List<LogisticsCargoOrder>(),
            refuelEngine = true,
            targetFuelKg = 30,
            refillClaudium = true,
            claudiumResourceId = "claudium",
            targetClaudiumKg = 15
        };
    }

    private static List<LogisticsCargoOrder> Orders(params (string itemId, int amount)[] items)
    {
        List<LogisticsCargoOrder> orders = new List<LogisticsCargoOrder>();
        if (items == null) return orders;

        for (int i = 0; i < items.Length; i++)
        {
            orders.Add(new LogisticsCargoOrder { itemId = items[i].itemId, amount = items[i].amount });
        }

        return orders;
    }

    private static LogisticsShipDefinition Ship(string shipId, string displayName, string routeId, string startingIslandId)
    {
        return new LogisticsShipDefinition
        {
            enabled = true,
            shipId = shipId,
            displayName = displayName,
            routeId = routeId,
            startingIslandId = startingIslandId,
            hullId = "starter_hull",
            claudiumResourceId = "claudium",
            autoInstallRequiredModules = true
        };
    }

    private static void PrimeStorage(PlayerProgress progress, string islandId, params (string itemId, int amount)[] resources)
    {
        IslandProductionState storage = progress.GetIslandProductionState(islandId, true);
        for (int i = 0; i < resources.Length; i++)
        {
            storage.SetResourceAmount(resources[i].itemId, resources[i].amount);
        }
    }

    private static GameObject FindOrCreateMetaObject()
    {
        MetaGameState existing = UnityEngine.Object.FindFirstObjectByType<MetaGameState>();
        if (existing != null) return existing.gameObject;

        GameObject metaObject = GameObject.Find("Meta Game");
        if (metaObject != null) return metaObject;

        metaObject = new GameObject("Meta Game");
        Undo.RegisterCreatedObjectUndo(metaObject, "Create meta game");
        return metaObject;
    }

    private static T EnsureComponent<T>(GameObject owner) where T : Component
    {
        T component = owner.GetComponent<T>();
        if (component != null) return component;
        return Undo.AddComponent<T>(owner);
    }

    private static Transform FindOrCreateTransform(string objectName, Vector3 position)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing == null)
        {
            existing = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(existing, "Create " + objectName);
        }

        existing.transform.position = position;
        existing.transform.rotation = Quaternion.identity;
        return existing.transform;
    }

    private static void EnsureCameraAndLight()
    {
        if (UnityEngine.Object.FindFirstObjectByType<Camera>() == null)
        {
            GameObject cameraObject = new GameObject("Debug Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create debug camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 420f, -700f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        if (UnityEngine.Object.FindFirstObjectByType<Light>() == null)
        {
            GameObject lightObject = new GameObject("Debug Directional Light");
            Undo.RegisterCreatedObjectUndo(lightObject, "Create debug light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    private static void LogFleetSummary(MetaGameState meta)
    {
        if (meta == null || meta.progress == null || meta.progress.logisticsShips == null) return;

        for (int i = 0; i < meta.progress.logisticsShips.Count; i++)
        {
            LogisticsShipState ship = meta.progress.logisticsShips[i];
            if (ship == null) continue;
            Debug.Log("[LogisticsDebug] " + ship.shipId
                + " status=" + ship.status
                + " island=" + ship.currentIslandId
                + " target=" + ship.targetIslandId
                + " loops=" + ship.completedRouteLoops
                + " cargo=" + FormatCargo(ship.cargo)
                + " message=" + ship.lastError);
        }
    }

    private static string FormatCargo(List<ResourceStack> cargo)
    {
        if (cargo == null || cargo.Count == 0) return "empty";

        string text = "";
        for (int i = 0; i < cargo.Count; i++)
        {
            ResourceStack stack = cargo[i];
            if (stack == null || string.IsNullOrWhiteSpace(stack.resourceId) || stack.amount <= 0) continue;
            if (text.Length > 0) text += ", ";
            text += stack.resourceId + "=" + stack.amount;
        }

        return text.Length > 0 ? text : "empty";
    }
}
