using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ProductionTestSceneBuilder
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";
    private const string DebugSaveFileName = "wild_wind_production_debug_save.json";

    [MenuItem("Wild Wind/Production/Prepare All Production Test Scene")]
    public static void PrepareAllProductionTestScene()
    {
        PrepareProductionTestScene(null);
    }

    [MenuItem("Wild Wind/Production/Prepare Auto-Checking Production Test Scene")]
    public static void PrepareAutoCheckingProductionTestScene()
    {
        MetaGameState meta = PrepareProductionTestScene(null);
        if (meta == null)
        {
            return;
        }

        Undo.RecordObject(meta, "Prepare production auto-check scene");
        meta.processRealTimeWhilePlaying = false;
        meta.skipInitialProcessCatchUp = true;
        meta.gameTimeScale = 1f;
        meta.accelerateUnityTimeScale = false;
        EnsureAutoTestRunner(meta);

        EditorUtility.SetDirty(meta);
        EditorSceneManager.MarkSceneDirty(meta.gameObject.scene);
        Debug.Log("[ProductionTest] Auto-check scene prepared. Enter Play Mode: ProductionAutoTestRunner will write OK/FAIL to the Console.");
    }

    [MenuItem("Wild Wind/Production/Prepare Generation Test Scene")]
    public static void PrepareGenerationTestScene()
    {
        PrepareProductionTestScene(IslandIndustryKind.Generation);
    }

    [MenuItem("Wild Wind/Production/Prepare Processing Test Scene")]
    public static void PrepareProcessingTestScene()
    {
        PrepareProductionTestScene(IslandIndustryKind.Processing);
    }

    [MenuItem("Wild Wind/Production/Prepare Manufacturing Test Scene")]
    public static void PrepareManufacturingTestScene()
    {
        PrepareProductionTestScene(IslandIndustryKind.Manufacturing);
    }

    [MenuItem("Wild Wind/Production/Prepare Reaction Test Scene")]
    public static void PrepareReactionTestScene()
    {
        PrepareProductionTestScene(IslandIndustryKind.Reaction);
    }

    [MenuItem("Wild Wind/Production/Prepare Conversion Test Scene")]
    public static void PrepareConversionTestScene()
    {
        PrepareProductionTestScene(IslandIndustryKind.Conversion);
    }

    [MenuItem("Wild Wind/Production/Prepare Assembly Test Scene")]
    public static void PrepareAssemblyTestScene()
    {
        PrepareProductionTestScene(IslandIndustryKind.Assembly);
    }

    [MenuItem("Wild Wind/Production/Simulate 10 Minutes")]
    public static void SimulateTenMinutes()
    {
        MetaGameState meta = Object.FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            EditorUtility.DisplayDialog("Production test", "No MetaGameState found. Run a Production test scene builder first.", "OK");
            return;
        }

        meta.EnsureProgressInitialized();
        if (meta.progress.lastProcessUtcTicks <= 0)
        {
            meta.progress.lastProcessUtcTicks = DateTime.UtcNow.Ticks;
        }

        DateTime from = new DateTime(meta.progress.lastProcessUtcTicks, DateTimeKind.Utc);
        int changed = meta.AdvanceRealTimeProcesses(from.AddMinutes(10));
        Debug.Log("[ProductionTest] Simulated 10 minutes, changed events=" + changed + ".");
        EditorUtility.SetDirty(meta);
    }

    private static MetaGameState PrepareProductionTestScene(IslandIndustryKind? focus)
    {
        MetaGameState meta = FindOrCreateMetaGameState();
        EnsureSceneLinks(meta);

        Undo.RecordObject(meta, "Prepare production test scene");
        meta.startingDockId = "capital";
        meta.startingDockKind = DockingLocationKind.Island;
        meta.startingMode = GameSessionMode.Docked;
        meta.loadSavedGameOnAwake = false;
        meta.saveFileName = DebugSaveFileName;
        meta.processRealTimeWhilePlaying = true;
        meta.skipInitialProcessCatchUp = false;
        meta.islandProductionEnabled = true;
        meta.gameTimeScale = 16f;
        meta.accelerateUnityTimeScale = true;
        meta.maxUnityTimeScale = 4f;
        meta.maxAcceleratedProcessStepSeconds = 10f;
        meta.spawnConfigIslandsOnPlay = true;
        meta.configIslandVisualRadius = 20f;
        meta.showDockingDebugUI = true;
        meta.productionDebugResourceId = focus == IslandIndustryKind.Processing ? "windshale_ore" : "charcoal";
        meta.productionDebugAmount = 25;

        meta.progress = new PlayerProgress();
        meta.EnsureProgressInitialized();
        meta.progress.SetDocked("capital", DockingLocationKind.Island);
        StockCapital(meta, focus);
        DisableAutoTestRunnerIfPresent();

        EditorUtility.SetDirty(meta);
        EditorSceneManager.MarkSceneDirty(meta.gameObject.scene);
        Selection.activeGameObject = meta.gameObject;

        string focusText = focus.HasValue ? focus.Value.ToString() : "all";
        Debug.Log("[ProductionTest] Production test scene prepared for " + focusText + ". Enter Play Mode and use the MetaGame debug window.");
        return meta;
    }

    private static void EnsureAutoTestRunner(MetaGameState meta)
    {
        if (meta == null) return;

        ProductionAutoTestRunner runner = Object.FindFirstObjectByType<ProductionAutoTestRunner>();
        if (runner == null)
        {
            GameObject runnerObject = new GameObject("ProductionAutoTestRunner");
            Undo.RegisterCreatedObjectUndo(runnerObject, "Create ProductionAutoTestRunner");
            runner = runnerObject.AddComponent<ProductionAutoTestRunner>();
        }
        else
        {
            Undo.RecordObject(runner, "Configure ProductionAutoTestRunner");
        }

        runner.metaGameState = meta;
        runner.runOnStart = true;
        runner.simulatedMinutes = 12f;
        runner.logSuccessfulChecks = false;
        runner.quietMode = true;
        runner.disableLiveProcessTick = true;
        Selection.activeGameObject = runner.gameObject;
        EditorUtility.SetDirty(runner);
    }

    private static void DisableAutoTestRunnerIfPresent()
    {
        ProductionAutoTestRunner runner = Object.FindFirstObjectByType<ProductionAutoTestRunner>();
        if (runner == null) return;

        Undo.RecordObject(runner, "Disable production auto-check runner");
        runner.runOnStart = false;
        EditorUtility.SetDirty(runner);
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
    }

    private static void StockCapital(MetaGameState meta, IslandIndustryKind? focus)
    {
        if (meta == null || meta.progress == null) return;

        IslandProductionState storage = meta.progress.GetIslandProductionState("capital", true);
        if (storage == null) return;

        if (!focus.HasValue || focus.Value == IslandIndustryKind.Processing)
        {
            storage.SetResourceAmount("windshale_ore", 30);
            storage.SetResourceAmount("dawnspar_ore", 20);
            storage.SetResourceAmount("mist_condensate", 20);
            storage.SetResourceAmount("cloud_condensate", 20);
            storage.SetResourceAmount("charcoal", Mathf.Max(storage.GetResourceAmount("charcoal"), 80));
        }

        if (!focus.HasValue || focus.Value == IslandIndustryKind.Manufacturing)
        {
            storage.SetResourceAmount("metal", Mathf.Max(storage.GetResourceAmount("metal"), 80));
            storage.SetResourceAmount("mechanisms", Mathf.Max(storage.GetResourceAmount("mechanisms"), 40));
            storage.SetResourceAmount("charcoal", Mathf.Max(storage.GetResourceAmount("charcoal"), 80));
        }

        if (!focus.HasValue || focus.Value == IslandIndustryKind.Reaction)
        {
            storage.SetResourceAmount("fulgur", Mathf.Max(storage.GetResourceAmount("fulgur"), 80));
            storage.SetResourceAmount("charcoal", Mathf.Max(storage.GetResourceAmount("charcoal"), 120));
            storage.SetResourceAmount("alcohol", Mathf.Max(storage.GetResourceAmount("alcohol"), 40));
        }

        if (!focus.HasValue || focus.Value == IslandIndustryKind.Conversion)
        {
            storage.SetResourceAmount("claudite", Mathf.Max(storage.GetResourceAmount("claudite"), 120));
        }

        if (!focus.HasValue || focus.Value == IslandIndustryKind.Assembly)
        {
            storage.SetResourceAmount("metal", Mathf.Max(storage.GetResourceAmount("metal"), 120));
            storage.SetResourceAmount("mechanisms", Mathf.Max(storage.GetResourceAmount("mechanisms"), 40));
            storage.SetResourceAmount("tools", Mathf.Max(storage.GetResourceAmount("tools"), 40));
            storage.SetResourceAmount("cloth", Mathf.Max(storage.GetResourceAmount("cloth"), 80));
        }

        if (!focus.HasValue || focus.Value == IslandIndustryKind.Generation)
        {
            storage.SetResourceAmount("paper", Mathf.Max(storage.GetResourceAmount("paper"), 20));
        }
    }
}
