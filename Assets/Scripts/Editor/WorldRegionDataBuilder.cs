using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class WorldRegionDataBuilder
{
    public const string FolderPath = "Assets/Data/World";
    public const string ProfilePath = FolderPath + "/WorldRegionProfile.asset";
    public const string ManifestPath = FolderPath + "/WorldRegionManifest.asset";

    public static void RebuildWorldDataMenu()
    {
        EnsureAndRebuild(out WorldRegionProfile profile, out WorldRegionManifest manifest, true);
        Selection.activeObject = manifest != null ? manifest : profile;
    }

    public static void EnsureAndRebuild(out WorldRegionProfile profile, out WorldRegionManifest manifest, bool refreshActiveRuntime)
    {
        EnsureWorldDataAssets(out profile, out manifest);
        RebuildManifest(profile, manifest);

        if (refreshActiveRuntime)
        {
            RefreshActiveRuntime(profile, manifest);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WorldData] Rebuilt seeded world data. Seed: " + (profile != null ? profile.Seed.ToString() : "<none>") + ", chunks: " + (manifest != null ? manifest.Chunks.Count.ToString() : "0") + ".");
    }

    public static void EnsureWorldDataAssets(out WorldRegionProfile profile, out WorldRegionManifest manifest)
    {
        EnsureFolders();

        profile = AssetDatabase.LoadAssetAtPath<WorldRegionProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<WorldRegionProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        manifest = AssetDatabase.LoadAssetAtPath<WorldRegionManifest>(ManifestPath);
        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<WorldRegionManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
        }
    }

    public static void RebuildManifest(WorldRegionProfile profile, WorldRegionManifest manifest)
    {
        if (profile == null || manifest == null)
        {
            Debug.LogError("[WorldData] Cannot rebuild world data: profile or manifest is missing.");
            return;
        }

        GameObject temporaryObject = new GameObject("World Data Build Runtime");
        temporaryObject.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            WorldRegionRuntime runtime = temporaryObject.AddComponent<WorldRegionRuntime>();
            runtime.ConfigureWorldData(profile, null);
            runtime.GenerateStarterRegion();
            manifest.CaptureFromRuntime(profile, runtime);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(manifest);
        }
        finally
        {
            Object.DestroyImmediate(temporaryObject);
        }
    }

    private static void RefreshActiveRuntime(WorldRegionProfile profile, WorldRegionManifest manifest)
    {
        WorldRegionRuntime runtime = Object.FindFirstObjectByType<WorldRegionRuntime>();
        if (runtime == null)
        {
            return;
        }

        Undo.RecordObject(runtime, "Apply seeded world data");
        runtime.ConfigureWorldData(profile, manifest);
        runtime.GenerateStarterRegion();
        runtime.ConfigureDebugDraw(false, true);
        EditorUtility.SetDirty(runtime);

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data"))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }

        if (!AssetDatabase.IsValidFolder(FolderPath))
        {
            AssetDatabase.CreateFolder("Assets/Data", "World");
        }
    }
}
