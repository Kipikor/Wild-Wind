using UnityEngine;
using UnityEngine.SceneManagement;

public static class WildWindGameplayBootstrap
{
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
        if (WildWindBigTestRunner.IsMainSessionCheckInProgress)
        {
            return;
        }

        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        if (WildWindBigTestRunner.IsEditorBigTestLaunchPending() &&
            !WildWindBigTestRunner.IsSessionLoopLaunchInProgress)
        {
#if UNITY_EDITOR
            WildWindBigTestRunner.TryStartPendingEditorBigTest();
#endif
            return;
        }

        MetaGameState meta = Object.FindFirstObjectByType<MetaGameState>();
        if (meta == null)
        {
            GameObject metaObject = new GameObject("MetaGameState");
            metaObject.SetActive(false);
            meta = metaObject.AddComponent<MetaGameState>();
            ConfigureMeta(meta);
            metaObject.SetActive(true);
            WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(meta, meta.RuntimeAccountId);
            Debug.Log("[WildWindGameplayBootstrap] Created MetaGameState and entered the runtime account port: " + meta.RuntimeAccountId);
            return;
        }

        ConfigureMeta(meta);
        meta.EnsureProgressInitialized();
        WildWindGameplaySession.EnsureSessionForLoadedGameplayScene(meta, meta.RuntimeAccountId);
        Debug.Log("[WildWindGameplayBootstrap] Entered the runtime account port: " + meta.RuntimeAccountId);
    }

    private static void ConfigureMeta(MetaGameState meta)
    {
        if (meta == null)
        {
            return;
        }

        meta.runtimeAccountId = GameplaySessionAccountData.DefaultAccountId;
        EnsureGameplayBindings(meta);
    }

    private static void EnsureGameplayBindings(MetaGameState meta)
    {
        if (meta == null)
        {
            return;
        }

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
            meta.shipLoader.targetShip = EnsurePlayerShipPhysics();
        }

        if (meta.shipLoader.spawnPoint == null && meta.shipLoader.targetShip != null)
        {
            meta.shipLoader.spawnPoint = meta.shipLoader.targetShip.transform;
        }
    }

    private static ShipPhysics EnsurePlayerShipPhysics()
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
            shipObject.transform.position = GameplaySessionAccountData.ResolveStarterDockPosition(GameplaySessionAccountData.DefaultDockId);
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
