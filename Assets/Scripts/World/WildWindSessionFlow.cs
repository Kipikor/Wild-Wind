using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WildWindSessionFlow
{
    public const string DefaultStartSceneName = "StartScreen";
    public const string DefaultGameplaySceneName = "WildWindWorldScene";

    public static bool TryPrepareNewWorldLaunch(out string fileName, out int seed, out string error)
    {
        fileName = WildWindSaveSlots.CreateNewWorldSaveFileName();
        seed = WorldSaveSlotFactory.CreateSeed();

        if (!WorldSaveSlotFactory.TryCreateNewWorldSave(fileName, seed, out error))
        {
            fileName = "";
            return false;
        }

        return TryPrepareExistingWorldLaunch(fileName, out error);
    }

    public static bool TryPrepareExistingWorldLaunch(string fileName, out string error)
    {
        error = "";
        string cleanFileName = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(cleanFileName))
        {
            error = "Save file name is empty.";
            return false;
        }

        if (WildWindBigTestRunner.IsBigTestTemporarySaveFileName(cleanFileName) &&
            !WildWindBigTestRunner.IsSessionLoopLaunchInProgress)
        {
            error = "Temporary big test save slots cannot be launched from the player menu.";
            return false;
        }

        string path = WildWindSaveSlots.GetSavePath(cleanFileName);
        if (!File.Exists(path))
        {
            error = "Save file does not exist: " + cleanFileName;
            return false;
        }

        if (!TryValidateSaveSlot(path, out error))
        {
            return false;
        }

        WildWindSaveSlots.SetSelectedSaveFileName(cleanFileName);
        WildWindSaveSlots.MarkPendingGameplayLaunch();
        return true;
    }

    public static bool TryCreateNewWorldAndEnter(string gameplaySceneName, out string fileName, out int seed, out string error)
    {
        if (!TryPrepareNewWorldLaunch(out fileName, out seed, out error))
        {
            return false;
        }

        if (TryLoadScene(ResolveSceneName(gameplaySceneName, DefaultGameplaySceneName), out error))
        {
            return true;
        }

        WildWindSaveSlots.ClearPendingGameplayLaunch();
        return false;
    }

    public static bool TryContinueWorldAndEnter(string fileName, string gameplaySceneName, out string error)
    {
        if (!TryPrepareExistingWorldLaunch(fileName, out error))
        {
            return false;
        }

        if (TryLoadScene(ResolveSceneName(gameplaySceneName, DefaultGameplaySceneName), out error))
        {
            return true;
        }

        WildWindSaveSlots.ClearPendingGameplayLaunch();
        return false;
    }

    public static bool TrySaveAndExitToMenu(MetaGameState meta, string menuSceneName, out string error)
    {
        error = "";
        if (meta == null)
        {
            error = "MetaGameState is missing.";
            return false;
        }

        if (!meta.TrySaveGameForSessionExit())
        {
            error = "Save failed before returning to menu.";
            return false;
        }

        meta.SetSessionPaused(false);
        return TryLoadScene(ResolveSceneName(menuSceneName, DefaultStartSceneName), out error);
    }

    public static bool TryExitToMenuWithoutSave(MetaGameState meta, string menuSceneName, out string error)
    {
        if (meta != null)
        {
            meta.SetSessionPaused(false);
        }
        else
        {
            Time.timeScale = 1f;
        }

        return TryLoadScene(ResolveSceneName(menuSceneName, DefaultStartSceneName), out error);
    }

    private static bool TryValidateSaveSlot(string path, out string error)
    {
        error = "";
        try
        {
            MetaGameSaveData saveData = JsonUtility.FromJson<MetaGameSaveData>(File.ReadAllText(path));
            if (saveData == null)
            {
                error = "Save data is empty.";
                return false;
            }

            if (saveData.version != MetaGameSaveData.CurrentVersion)
            {
                error = "Save version is not supported: " + saveData.version;
                return false;
            }

            if (saveData.worldManifest == null || !saveData.worldManifest.IsUsable)
            {
                error = "Save does not contain a usable world manifest.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryLoadScene(string sceneName, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            error = "Scene name is empty.";
            return false;
        }

        try
        {
            SceneManager.LoadScene(sceneName);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string ResolveSceneName(string sceneName, string fallback)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? fallback : sceneName.Trim();
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "";
        }

        string clean = Path.GetFileName(fileName.Trim());
        return string.IsNullOrWhiteSpace(clean) ? "" : clean;
    }
}
