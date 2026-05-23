using System;
using System.IO;
using UnityEngine;

public static class WorldSaveSlotFactory
{
    public static int CreateSeed()
    {
        unchecked
        {
            long ticks = DateTime.UtcNow.Ticks;
            return (int)(ticks ^ (ticks >> 32) ^ Environment.TickCount);
        }
    }

    public static bool TryCreateNewWorldSave(string fileName, int seed, out string error)
    {
        error = "";
        string path = WildWindSaveSlots.GetSavePath(fileName);
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Save path is empty.";
            return false;
        }

        MetaGameSaveData saveData = BuildNewWorldSaveData(seed);
        if (saveData == null || saveData.worldManifest == null || !saveData.worldManifest.IsUsable)
        {
            error = "Generated world manifest is empty.";
            return false;
        }

        if (saveData.gameplaySession != null)
        {
            saveData.gameplaySession.selectedSaveFileName = Path.GetFileName(fileName ?? "");
        }

        try
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, JsonUtility.ToJson(saveData, true));
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static MetaGameSaveData BuildNewWorldSaveData(int seed)
    {
        WorldManifestData manifest = GenerateStarterManifest(seed);
        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        progress.selectedHullId = GameplaySessionSaveData.DefaultStarterHullId;
        progress.SetDocked(
            GameplaySessionSaveData.DefaultDockId,
            DockingLocationKind.Island,
            GameplaySessionSaveData.ResolveStarterDockPosition(manifest, GameplaySessionSaveData.DefaultDockId));
        WildWindStarterDelivery.SeedNewGame(progress);

        return new MetaGameSaveData
        {
            version = MetaGameSaveData.CurrentVersion,
            progress = progress,
            worldManifest = manifest,
            worldRuntime = WorldRuntimeSaveData.CreateInitial(manifest),
            gameplaySession = GameplaySessionSaveData.CreateInitial(manifest, "", progress)
        };
    }

    public static WorldManifestData GenerateStarterManifest(int seed)
    {
        GameObject temporaryObject = null;
        try
        {
            temporaryObject = new GameObject("World Save Slot Generation Runtime");
            temporaryObject.hideFlags = HideFlags.HideAndDontSave;
            WorldRegionRuntime runtime = temporaryObject.AddComponent<WorldRegionRuntime>();
            runtime.ConfigureProceduralWorld(seed, WorldRegionRuntime.DefaultWorldSizeMeters, WorldRegionRuntime.DefaultChunkSizeMeters);
            return WorldManifestData.FromRuntime(runtime, "seed_" + seed);
        }
        finally
        {
            if (temporaryObject != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(temporaryObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(temporaryObject);
                }
            }
        }
    }
}
