using UnityEditor;
using UnityEngine;

public static class WorldSettingsPrefabBuilder
{
    public const string SettingsPrefabPath = "Assets/Prefabs/Settings/WildWindSettings.prefab";

    public static void CreateOrUpdateSettingsPrefab()
    {
        GameObject prefab = EnsureSettingsPrefabAsset();
        Selection.activeObject = prefab;
        Debug.Log("[WildWindSettings] Settings prefab ready: " + SettingsPrefabPath);
    }

    public static GameObject EnsureSettingsPrefabAsset()
    {
        EnsureFolders();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
        if (prefab == null)
        {
            GameObject root = CreateSettingsHierarchy();
            prefab = PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(SettingsPrefabPath);
        try
        {
            EnsureSettingsHierarchy(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, SettingsPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
    }

    public static WildWindSettingsRoot InstantiateSettingsPrefab(Transform parent)
    {
        GameObject prefab = EnsureSettingsPrefabAsset();
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            return null;
        }

        instance.name = prefab.name;
        instance.transform.SetParent(parent, false);
        WildWindSettingsRoot settings = instance.GetComponent<WildWindSettingsRoot>();
        if (settings != null)
        {
            settings.Configure(instance.GetComponentInChildren<WildWindControlSettings>(true));
            EditorUtility.SetDirty(settings);
        }

        return settings;
    }

    private static GameObject CreateSettingsHierarchy()
    {
        GameObject root = new GameObject("Wild Wind Settings");
        EnsureSettingsHierarchy(root);
        return root;
    }

    private static void EnsureSettingsHierarchy(GameObject root)
    {
        root.name = "Wild Wind Settings";

        WildWindSettingsRoot settingsRoot = root.GetComponent<WildWindSettingsRoot>();
        if (settingsRoot == null)
        {
            settingsRoot = root.AddComponent<WildWindSettingsRoot>();
        }

        Transform controlsTransform = root.transform.Find("Controls");
        if (controlsTransform == null)
        {
            GameObject controlsObject = new GameObject("Controls");
            controlsObject.transform.SetParent(root.transform, false);
            controlsTransform = controlsObject.transform;
        }

        WildWindControlSettings controls = controlsTransform.GetComponent<WildWindControlSettings>();
        if (controls == null)
        {
            controls = controlsTransform.gameObject.AddComponent<WildWindControlSettings>();
        }

        settingsRoot.Configure(controls);
        EditorUtility.SetDirty(settingsRoot);
        EditorUtility.SetDirty(controls);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Settings"))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Settings");
        }
    }
}
