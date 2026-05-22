using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class WildWindSaveSlots
{
    public const string SelectedSaveFileNamePlayerPrefsKey = "WildWind.SelectedSaveFileName";
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
            FileInfo info = new FileInfo(path);
            slots.Add(new WildWindSaveSlotInfo
            {
                fileName = info.Name,
                fullPath = info.FullName,
                lastWriteUtc = info.LastWriteTimeUtc,
                sizeBytes = info.Length
            });
        }

        slots.Sort((a, b) => b.lastWriteUtc.CompareTo(a.lastWriteUtc));
        return slots;
    }

    public static string GetSavePath(string fileName)
    {
        return Path.Combine(Application.persistentDataPath, SanitizeFileName(fileName));
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

    public string DisplayName
    {
        get
        {
            string localTime = lastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            return Path.GetFileNameWithoutExtension(fileName) + "  " + localTime;
        }
    }
}
