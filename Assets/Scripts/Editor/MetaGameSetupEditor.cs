using UnityEditor;
using UnityEngine;

public static class MetaGameSetupEditor
{
    private const string CatalogPath = "Assets/Data/ShipCatalog.asset";
    private const string TechTreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";
    private const string MetaGameObjectName = "Мета-игра";

    public static void SetupMetaGame()
    {
        ShipPhysics ship = Object.FindFirstObjectByType<ShipPhysics>();
        if (ship == null)
        {
            EditorUtility.DisplayDialog("Сборка мета-игры", "В открытой сцене не найден ShipPhysics.", "OK");
            return;
        }

        ShipCatalogSO catalog = AssetDatabase.LoadAssetAtPath<ShipCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            EditorUtility.DisplayDialog("Сборка мета-игры", $"Каталог кораблей не найден по пути {CatalogPath}.", "OK");
            return;
        }

        TechTreeDefinitionSO techTree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TechTreePath);
        MissionController missionController = Object.FindFirstObjectByType<MissionController>();

        GameObject metaGameObject = GameObject.Find(MetaGameObjectName);
        if (metaGameObject == null)
        {
            metaGameObject = new GameObject(MetaGameObjectName);
            Undo.RegisterCreatedObjectUndo(metaGameObject, "Создать мета-игру");
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

        Undo.RecordObjects(new Object[] { loader, metaGameState }, "Настроить мета-игру");

        loader.catalog = catalog;
        loader.targetShip = ship;
        metaGameState.catalog = catalog;
        metaGameState.techTree = techTree;
        metaGameState.shipLoader = loader;
        metaGameState.missionController = missionController;
        metaGameState.showDockingDebugUI = true;

        EditorUtility.SetDirty(loader);
        EditorUtility.SetDirty(metaGameState);
        Selection.activeGameObject = metaGameObject;
    }

    public static bool ValidateSetupMetaGame()
    {
        return !Application.isPlaying;
    }
}
