using UnityEngine;

public static class WildWindGameplayBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapGameplayLaunch()
    {
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

        WorldRegionRuntime world = Object.FindFirstObjectByType<WorldRegionRuntime>();
        if (world == null)
        {
            Debug.LogWarning("[WildWindGameplayBootstrap] Gameplay launch reached a scene without WorldRegionRuntime.");
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
            Debug.Log("[WildWindGameplayBootstrap] Created MetaGameState and loaded selected save: " + selectedSave);
            return;
        }

        ConfigureMeta(meta, world, index, runtimeState);
        if (meta.LoadGame())
        {
            meta.EnsureProgressInitialized();
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
    }
}
