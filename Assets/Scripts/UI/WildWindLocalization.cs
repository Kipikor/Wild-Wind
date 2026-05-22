using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class WildWindLocalization
{
    public const string DefaultRelativeConfigPath = "Data/Localization/Ui.csv";
    public const string LanguagePlayerPrefsKey = "WildWind.Language";
    public const WildWindLanguage DefaultLanguage = WildWindLanguage.Ru;

    private static readonly Dictionary<string, Entry> entriesByKey = new Dictionary<string, Entry>();
    private static bool loaded;
    private static WildWindLanguage currentLanguage = DefaultLanguage;

    public static event Action LanguageChanged;

    public static WildWindLanguage CurrentLanguage
    {
        get
        {
            LoadLanguagePreference();
            return currentLanguage;
        }
    }

    public static string CurrentLanguageCode => CurrentLanguage == WildWindLanguage.En ? "en" : "ru";

    public static void SetLanguage(WildWindLanguage language)
    {
        LoadLanguagePreference();
        if (currentLanguage == language)
        {
            return;
        }

        currentLanguage = language;
        PlayerPrefs.SetString(LanguagePlayerPrefsKey, ToCode(language));
        PlayerPrefs.Save();
        LanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        EnsureLoaded();
        if (TryGet(key, CurrentLanguage, out string value))
        {
            return value;
        }

        Debug.LogError("[WildWindLocalization] Missing localization key: " + key);
        return "!" + key + "!";
    }

    public static bool TryGet(string key, out string value)
    {
        return TryGet(key, CurrentLanguage, out value);
    }

    public static bool TryGet(string key, WildWindLanguage language, out string value)
    {
        EnsureLoaded();
        value = "";
        if (string.IsNullOrWhiteSpace(key) || !entriesByKey.TryGetValue(key, out Entry entry))
        {
            return false;
        }

        value = language == WildWindLanguage.En ? entry.en : entry.ru;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool HasKey(string key)
    {
        EnsureLoaded();
        return !string.IsNullOrWhiteSpace(key) && entriesByKey.ContainsKey(key);
    }

    public static bool ValidateDefaultConfig(IEnumerable<string> requiredKeys, out List<string> errors)
    {
        string path = GetDefaultConfigPath();
        return ValidateConfigFile(path, requiredKeys, out errors);
    }

    public static bool ValidateConfigFile(string path, IEnumerable<string> requiredKeys, out List<string> errors)
    {
        errors = new List<string>();
        Dictionary<string, Entry> parsed = LoadEntriesFromFile(path, errors);
        if (parsed.Count == 0)
        {
            errors.Add("Localization config has no rows: " + path);
        }

        if (requiredKeys != null)
        {
            foreach (string key in requiredKeys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add("Required localization key is empty.");
                    continue;
                }

                if (!parsed.TryGetValue(key, out Entry entry))
                {
                    errors.Add("Missing required localization key: " + key);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.ru))
                {
                    errors.Add("Missing ru text for key: " + key);
                }

                if (string.IsNullOrWhiteSpace(entry.en))
                {
                    errors.Add("Missing en text for key: " + key);
                }
            }
        }

        return errors.Count == 0;
    }

    public static string GetDefaultConfigPath()
    {
        return Path.Combine(Application.dataPath, DefaultRelativeConfigPath);
    }

    public static void ReloadForTests()
    {
        loaded = false;
        entriesByKey.Clear();
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        entriesByKey.Clear();

        List<string> errors = new List<string>();
        Dictionary<string, Entry> parsed = LoadEntriesFromFile(GetDefaultConfigPath(), errors);
        foreach (KeyValuePair<string, Entry> pair in parsed)
        {
            entriesByKey[pair.Key] = pair.Value;
        }

        for (int i = 0; i < errors.Count; i++)
        {
            Debug.LogError("[WildWindLocalization] " + errors[i]);
        }
    }

    private static Dictionary<string, Entry> LoadEntriesFromFile(string path, List<string> errors)
    {
        Dictionary<string, Entry> parsed = new Dictionary<string, Entry>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            errors.Add("Localization config not found: " + path);
            return parsed;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (Exception exception)
        {
            errors.Add("Localization config read failed: " + exception.Message);
            return parsed;
        }

        if (lines.Length <= 1)
        {
            errors.Add("Localization config has no data rows: " + path);
            return parsed;
        }

        List<string> headers = ParseCsvLine(lines[0]);
        int keyIndex = headers.IndexOf("key");
        int nameIndex = headers.IndexOf("#name");
        int ruIndex = headers.IndexOf("ru");
        int enIndex = headers.IndexOf("en");
        if (keyIndex < 0 || nameIndex < 0 || ruIndex < 0 || enIndex < 0)
        {
            errors.Add("Localization config header must be: key,#name,ru,en");
            return parsed;
        }

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> cells = ParseCsvLine(line);
            string key = GetCell(cells, keyIndex).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add("Localization row " + (lineIndex + 1) + " has empty key.");
                continue;
            }

            if (parsed.ContainsKey(key))
            {
                errors.Add("Duplicate localization key: " + key);
                continue;
            }

            Entry entry = new Entry
            {
                key = key,
                name = GetCell(cells, nameIndex),
                ru = GetCell(cells, ruIndex),
                en = GetCell(cells, enIndex)
            };

            if (string.IsNullOrWhiteSpace(entry.ru))
            {
                errors.Add("Localization key has empty ru text: " + key);
            }

            if (string.IsNullOrWhiteSpace(entry.en))
            {
                errors.Add("Localization key has empty en text: " + key);
            }

            parsed[key] = entry;
        }

        return parsed;
    }

    private static string GetCell(List<string> cells, int index)
    {
        return index >= 0 && index < cells.Count ? cells[index] : "";
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> cells = new List<string>();
        if (line == null)
        {
            cells.Add("");
            return cells;
        }

        StringBuilder cell = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                cells.Add(cell.ToString());
                cell.Length = 0;
            }
            else
            {
                cell.Append(c);
            }
        }

        cells.Add(cell.ToString());
        return cells;
    }

    private static void LoadLanguagePreference()
    {
        string value = PlayerPrefs.GetString(LanguagePlayerPrefsKey, ToCode(DefaultLanguage));
        currentLanguage = FromCode(value);
    }

    public static string ToCode(WildWindLanguage language)
    {
        return language == WildWindLanguage.En ? "en" : "ru";
    }

    public static WildWindLanguage FromCode(string value)
    {
        return string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)
            ? WildWindLanguage.En
            : WildWindLanguage.Ru;
    }

    private struct Entry
    {
        public string key;
        public string name;
        public string ru;
        public string en;
    }
}
