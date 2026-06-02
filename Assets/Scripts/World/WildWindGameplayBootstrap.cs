using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class WildWindGameplayBootstrap
{
    private const string ShipCatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";
    private const string ShipLoaderObjectName = "Player Ship Loader";
    private const string PlayerShipProxyName = "Player Ship Proxy";
    private const string PlayerSessionShipName = "Player Session Ship";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallGameplayLaunchBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BootstrapGameplayLaunch();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootstrapGameplayLaunch();
    }

    private static void BootstrapGameplayLaunch()
    {
        if (WildWindBigTestRunner.IsMainWorldCheckInProgress)
        {
            return;
        }

        WorldRegionRuntime world = Object.FindFirstObjectByType<WorldRegionRuntime>();
        if (world == null)
        {
            return;
        }

        if (WildWindBigTestRunner.IsEditorBigTestLaunchPending() &&
            !WildWindBigTestRunner.IsSessionLoopLaunchInProgress)
        {
            WildWindSaveSlots.ClearPendingGameplayLaunch();
#if UNITY_EDITOR
            WildWindBigTestRunner.TryStartPendingEditorBigTest();
#endif
            return;
        }

        if (!WildWindSaveSlots.ConsumePendingGameplayLaunch())
        {
            return;
        }

        string selectedSave = WildWindSaveSlots.GetSelectedSaveFileNameOrEmpty();
        if (string.IsNullOrWhiteSpace(selectedSave))
        {
            Debug.LogWarning("[WildWindGameplayBootstrap] Gameplay launch requested, but no save slot is selected.");
            return;
        }

        if (WildWindBigTestRunner.IsBigTestTemporarySaveFileName(selectedSave) &&
            !WildWindBigTestRunner.IsSessionLoopLaunchInProgress)
        {
            PlayerPrefs.DeleteKey(WildWindSaveSlots.SelectedSaveFileNamePlayerPrefsKey);
            PlayerPrefs.Save();
            Debug.LogWarning("[WildWindGameplayBootstrap] Ignored abandoned big test save slot: " + selectedSave);
            return;
        }

        WorldEntityIndex index = Object.FindFirstObjectByType<WorldEntityIndex>();
        WorldRuntimeState runtimeState = Object.FindFirstObjectByType<WorldRuntimeState>();
        MetaGameState meta = Object.FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            GameObject metaObject = new GameObject("MetaGameState");
            metaObject.SetActive(false);
            meta = metaObject.AddComponent<MetaGameState>();
            ConfigureMeta(meta, world, index, runtimeState);
            metaObject.SetActive(true);
            WildWindGameplaySession.EnsureSessionForLoadedWorld(meta, selectedSave);
            Debug.Log("[WildWindGameplayBootstrap] Created MetaGameState and loaded selected save: " + selectedSave);
            return;
        }

        ConfigureMeta(meta, world, index, runtimeState);
        if (meta.LoadGame())
        {
            meta.EnsureProgressInitialized();
            WildWindGameplaySession.EnsureSessionForLoadedWorld(meta, selectedSave);
            Debug.Log("[WildWindGameplayBootstrap] Loaded selected save: " + selectedSave);
        }
        else
        {
            Debug.LogWarning("[WildWindGameplayBootstrap] Selected save could not be loaded: " + selectedSave);
        }
    }

    private static void ConfigureMeta(MetaGameState meta, WorldRegionRuntime world, WorldEntityIndex index, WorldRuntimeState runtimeState)
    {
        if (meta == null)
        {
            return;
        }

        meta.saveFileName = WildWindSaveSlots.DefaultSaveFileName;
        meta.loadSavedGameOnAwake = true;
        meta.worldRuntime = world;
        meta.worldIndex = index;
        meta.worldRuntimeState = runtimeState;
        EnsureGameplayBindings(meta, world);
    }

    private static void EnsureGameplayBindings(MetaGameState meta, WorldRegionRuntime world)
    {
        if (meta == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (meta.catalog == null)
        {
            meta.catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(ShipCatalogPath);
        }

        if (meta.techTree == null)
        {
            meta.techTree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TechTreePath);
        }
#endif

        if (meta.shipLoader == null)
        {
            meta.shipLoader = Object.FindFirstObjectByType<ShipLoader>();
        }

        if (meta.shipLoader == null)
        {
            GameObject loaderObject = new GameObject(ShipLoaderObjectName);
            meta.shipLoader = loaderObject.AddComponent<ShipLoader>();
        }

        if (meta.shipLoader.catalog == null)
        {
            meta.shipLoader.catalog = meta.catalog;
        }

        if (meta.shipLoader.targetShip == null)
        {
            meta.shipLoader.targetShip = EnsurePlayerShipPhysics(world);
        }

        if (meta.shipLoader.spawnPoint == null && meta.shipLoader.targetShip != null)
        {
            meta.shipLoader.spawnPoint = meta.shipLoader.targetShip.transform;
        }
    }

    private static ShipPhysics EnsurePlayerShipPhysics(WorldRegionRuntime world)
    {
        ShipPhysics ship = Object.FindFirstObjectByType<ShipPhysics>();
        if (ship != null)
        {
            EnsureShipRigidbody(ship);
            return ship;
        }

        GameObject shipObject = GameObject.Find(PlayerShipProxyName);
        if (shipObject == null)
        {
            shipObject = GameObject.Find(PlayerSessionShipName);
        }

        if (shipObject == null)
        {
            shipObject = new GameObject(PlayerSessionShipName);
            if (world != null && world.Focus != null)
            {
                shipObject.transform.position = world.Focus.position;
            }
        }

        ship = shipObject.GetComponent<ShipPhysics>();
        if (ship == null)
        {
            ship = shipObject.AddComponent<ShipPhysics>();
        }

        EnsureShipRigidbody(ship);
        return ship;
    }

    private static void EnsureShipRigidbody(ShipPhysics ship)
    {
        if (ship == null)
        {
            return;
        }

        Rigidbody body = ship.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = ship.gameObject.AddComponent<Rigidbody>();
        }

        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.useGravity = false;
        body.isKinematic = true;
    }
}
