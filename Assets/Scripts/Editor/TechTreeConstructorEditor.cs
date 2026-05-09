using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TechTreeDefinitionSO))]
public class TechTreeConstructorEditor : Editor
{
    private const string TreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";
    private const string StarterShipPath = "Assets/Data/Ships/ShipDefinition.asset";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TechTreeDefinitionSO tree = (TechTreeDefinitionSO)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Конструктор", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Добавить корабль"))
        {
            AddShipNode(tree);
        }

        if (GUILayout.Button("Добавить модуль"))
        {
            AddModuleNode(tree);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Проверить древо"))
        {
            ShowValidation(tree);
        }
    }

    [MenuItem("Wild Wind/Древо техники/Открыть конструктор")]
    public static void OpenConstructor()
    {
        TechTreeGraphWindow.OpenWindow();
    }

    [MenuItem("Wild Wind/Древо техники/Создать стартовое древо")]
    public static void CreateStarterTree()
    {
        TechTreeDefinitionSO existingTree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TreePath);
        if (existingTree != null)
        {
            Selection.activeObject = existingTree;
            EditorGUIUtility.PingObject(existingTree);
            return;
        }

        TechTreeDefinitionSO tree = CreateInstance<TechTreeDefinitionSO>();
        ShipDefinitionSO starterShip = AssetDatabase.LoadAssetAtPath<ShipDefinitionSO>(StarterShipPath);

        TechTreeNode starterNode = new TechTreeNode
        {
            nodeId = "ship_tier1",
            displayName = "Корабль I",
            kind = TechTreeNodeKind.Ship,
            tier = 1,
            shipDefinition = starterShip,
            shipId = starterShip != null ? starterShip.shipId : "ship",
            startsResearched = true,
            startsPurchased = true
        };

        tree.nodes.Add(starterNode);
        AssetDatabase.CreateAsset(tree, TreePath);
        AssetDatabase.SaveAssets();

        Selection.activeObject = tree;
        EditorGUIUtility.PingObject(tree);
    }

    private static void AddShipNode(TechTreeDefinitionSO tree)
    {
        Undo.RecordObject(tree, "Добавить корабль в древо техники");

        int index = tree.nodes.Count + 1;
        tree.nodes.Add(new TechTreeNode
        {
            nodeId = $"ship_node_{index}",
            displayName = $"Корабль {index}",
            kind = TechTreeNodeKind.Ship,
            tier = Mathf.Clamp(index, 1, 10),
            researchCostXp = 100 * index,
            purchasePrice = 250 * index,
            editorPosition = new Vector2(index * 220f, 0f)
        });

        EditorUtility.SetDirty(tree);
    }

    private static void AddModuleNode(TechTreeDefinitionSO tree)
    {
        Undo.RecordObject(tree, "Добавить модуль в древо техники");

        int index = tree.nodes.Count + 1;
        tree.nodes.Add(new TechTreeNode
        {
            nodeId = $"module_node_{index}",
            displayName = $"Модуль {index}",
            kind = TechTreeNodeKind.Module,
            moduleKind = TechTreeModuleKind.Other,
            tier = 1,
            researchCostXp = 50,
            purchasePrice = 100,
            editorPosition = new Vector2(index * 220f, 120f)
        });

        EditorUtility.SetDirty(tree);
    }

    private static void ShowValidation(TechTreeDefinitionSO tree)
    {
        List<string> issues = tree.ValidateTree();
        if (issues.Count == 0)
        {
            EditorUtility.DisplayDialog("Проверка древа техники", "Древо выглядит корректно.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Проверка древа техники", string.Join("\n", issues), "OK");
    }
}
