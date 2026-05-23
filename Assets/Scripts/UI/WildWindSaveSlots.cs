using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class WildWindSaveSlots
{
    public const string SelectedSaveFileNamePlayerPrefsKey = "WildWind.SelectedSaveFileName";
    public const string PendingGameplayLaunchPlayerPrefsKey = "WildWind.PendingGameplayLaunch";
    public const string DefaultSaveFileName = "wild_wind_save.json";

    public static string GetSelectedSaveFileNameOrEmpty()
    {
        return PlayerPrefs.GetString(SelectedSaveFileNamePlayerPrefsKey, "");
    }

    public static string GetSelectedSaveFileNameOrDefault()
    {
        string selected = GetSelectedSaveFileNameOrEmpty();
        return string.IsNullOrWhiteSpace(selected) ? DefaultSaveFileName : SanitizeFileName(selected);
    }

    public static void SetSelectedSaveFileName(string fileName)
    {
        string sanitized = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = DefaultSaveFileName;
        }

        PlayerPrefs.SetString(SelectedSaveFileNamePlayerPrefsKey, sanitized);
        PlayerPrefs.Save();
    }

    public static void MarkPendingGameplayLaunch()
    {
        PlayerPrefs.SetInt(PendingGameplayLaunchPlayerPrefsKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumePendingGameplayLaunch()
    {
        bool pending = PlayerPrefs.GetInt(PendingGameplayLaunchPlayerPrefsKey, 0) == 1;
        if (pending)
        {
            PlayerPrefs.DeleteKey(PendingGameplayLaunchPlayerPrefsKey);
            PlayerPrefs.Save();
        }

        return pending;
    }

    public static void ClearPendingGameplayLaunch()
    {
        PlayerPrefs.DeleteKey(PendingGameplayLaunchPlayerPrefsKey);
        PlayerPrefs.Save();
    }

    public static string CreateNewWorldSaveFileName()
    {
        return "wild_wind_world_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json";
    }

    public static List<WildWindSaveSlotInfo> GetExistingSlots()
    {
        List<WildWindSaveSlotInfo> slots = new List<WildWindSaveSlotInfo>();
        string directory = Application.persistentDataPath;
        if (!Directory.Exists(directory))
        {
            return slots;
        }

        string[] files = Directory.GetFiles(directory, "wild_wind*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i];
            if (WildWindBigTestRunner.IsBigTestTemporarySaveFileName(Path.GetFileName(path)))
            {
                continue;
            }

            FileInfo info = new FileInfo(path);
            if (!TryCreateSlotInfo(path, info, out WildWindSaveSlotInfo slot))
            {
                continue;
            }

            slots.Add(slot);
        }

        slots.Sort((a, b) => b.lastWriteUtc.CompareTo(a.lastWriteUtc));
        return slots;
    }

    public static string GetSavePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, SanitizeFileName(fileName));
    }

    private static bool TryCreateSlotInfo(string path, FileInfo info, out WildWindSaveSlotInfo slot)
    {
        slot = null;
        if (info == null || !info.Exists)
        {
            return false;
        }

        try
        {
            MetaGameSaveData saveData = JsonUtility.FromJson<MetaGameSaveData>(File.ReadAllText(path));
            if (saveData == null ||
                saveData.version != MetaGameSaveData.CurrentVersion ||
                saveData.worldManifest == null ||
                !saveData.worldManifest.IsUsable)
            {
                return false;
            }

            slot = new WildWindSaveSlotInfo
            {
                fileName = info.Name,
                fullPath = info.FullName,
                lastWriteUtc = info.LastWriteTimeUtc,
                sizeBytes = info.Length,
                version = saveData.version,
                seed = saveData.worldManifest.seed
            };
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WildWindSaveSlots] Ignoring invalid save slot '" + info.Name + "': " + exception.Message);
            return false;
        }
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

public sealed class WildWindSaveSlotInfo
{
    public string fileName = "";
    public string fullPath = "";
    public DateTime lastWriteUtc;
    public long sizeBytes;
    public int version;
    public int seed;

    public string DisplayName
    {
        get
        {
            string localTime = lastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            string seedText = seed != 0 ? "  seed " + seed : "";
            return Path.GetFileNameWithoutExtension(fileName) + seedText + "  " + localTime;
        }
    }
}
