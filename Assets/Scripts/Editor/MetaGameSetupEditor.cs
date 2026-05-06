using UnityEditor;
using UnityEngine;

public static class MetaGameSetupEditor
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string MetaGameObjectName = "Meta Game";

    [MenuItem("Wild Wind/Meta/Setup Meta Game")]
    public static void SetupMetaGame()
    {
        ShipPhysics ship = Object.FindFirstObjectByType<ShipPhysics>();
        if (ship == null)
        {
            EditorUtility.DisplayDialog("Meta Game Setup", "ShipPhysics was not found in the open scene.", "OK");
            return;
        }

        ShipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Meta Game Setup", $"Catalog was not found at {CatalogPath}.", "OK");
            return;
        }

        GameObject metaGameObject = GameObject.Find(MetaGameObjectName);
        if (metaGameObject == null)
        {
            metaGameObject = new GameObject(MetaGameObjectName);
            Undo.RegisterCreatedObjectUndo(metaGameObject, "Create Meta Game");
        }

        ShipLoader loader = metaGameObject.GetComponent<ShipLoader>();
        if (loader == null)
        {
            loader = Undo.AddComponent<ShipLoader>(metaGameObject);
        }

        MetaGameState metaGameState = metaGameObject.GetComponent<MetaGameState>();
        if (metaGameState == null)
        {
            metaGameState = Undo.AddComponent<MetaGameState>(metaGameObject);
        }

        Undo.RecordObjects(new Object[] { loader, metaGameState }, "Setup Meta Game");

        loader.catalog = catalog;
        loader.targetShip = ship;
        loader.applyOnStart = false;

        metaGameState.catalog = catalog;
        metaGameState.shipLoader = loader;

        EditorUtility.SetDirty(loader);
        EditorUtility.SetDirty(metaGameState);
        Selection.activeGameObject = metaGameObject;
    }

    [MenuItem("Wild Wind/Meta/Setup Meta Game", true)]
    public static bool ValidateSetupMetaGame()
    {
        return !Application.isPlaying;
    }
}
