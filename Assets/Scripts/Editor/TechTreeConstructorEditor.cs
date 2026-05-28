using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TechTreeDefinitionSO))]
public class TechTreeConstructorEditor : Editor
{
    private const string SelectedNodeSessionKeyPrefix = "WildWind.TechTree.SelectedNode.";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        LocalizedInspector.Section("Древо техники");
        SerializedProperty nodes = serializedObject.FindProperty("nodes");
        int selectedNodeIndex = GetSelectedNodeIndex((TechTreeDefinitionSO)target);
        bool hasSelectedNode = selectedNodeIndex >= 0 && selectedNodeIndex < nodes.arraySize;

        if (hasSelectedNode)
        {
            DrawSelectedNode(nodes, selectedNodeIndex);
        }
        else
        {
            EditorGUILayout.HelpBox("Выбери ноду в визуальном редакторе, чтобы открыть ее поля здесь.", MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        LocalizedInspector.DrawTechTreeNodeList(nodes, "Все узлы древа");

        serializedObject.ApplyModifiedProperties();

        TechTreeDefinitionSO tree = (TechTreeDefinitionSO)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Конструктор", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Добавить корпус"))
        {
            AddHullNode(tree);
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

    public static void SetSelectedNode(TechTreeDefinitionSO tree, int nodeIndex)
    {
        if (tree == null) return;

        SessionState.SetInt(GetSelectedNodeSessionKey(tree), nodeIndex);
        Selection.activeObject = tree;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    public static int GetSelectedNodeIndex(TechTreeDefinitionSO tree)
    {
        if (tree == null) return -1;
        return SessionState.GetInt(GetSelectedNodeSessionKey(tree), -1);
    }

    public static void OpenConstructor()
    {
        TechTreeGraphWindow.OpenWindow();
    }

    public static void CreateStarterTree()
    {
        ShipAssemblySetupEditor.BuildStarterAssemblySetup();
    }

    private static void AddHullNode(TechTreeDefinitionSO tree)
    {
        Undo.RecordObject(tree, "Добавить корпус в древо техники");

        int index = tree.nodes.Count + 1;
        tree.nodes.Add(new TechTreeNode
        {
            nodeId = $"hull_node_{index}",
            displayName = $"Корпус {index}",
            kind = TechTreeNodeKind.Hull,
            tier = Mathf.Clamp(index, 1, 10),
            researchCostXp = 100 * index,
            purchasePrice = 250 * index,
            editorPosition = new Vector2(index * 220f, 0f)
        });

        SetSelectedNode(tree, tree.nodes.Count - 1);
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

        SetSelectedNode(tree, tree.nodes.Count - 1);
        EditorUtility.SetDirty(tree);
    }

    private void DrawSelectedNode(SerializedProperty nodes, int selectedNodeIndex)
    {
        TechTreeDefinitionSO tree = (TechTreeDefinitionSO)target;
        SerializedProperty node = nodes.GetArrayElementAtIndex(selectedNodeIndex);
        SerializedProperty nodeId = node.FindPropertyRelative("nodeId");
        SerializedProperty displayName = node.FindPropertyRelative("displayName");
        string title = !string.IsNullOrWhiteSpace(displayName.stringValue) ? displayName.stringValue : nodeId.stringValue;

        EditorGUILayout.LabelField("Выбранная нода", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox($"Редактируется: {title}", MessageType.None);
        LocalizedInspector.DrawTechTreeNodeProperties(node);

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Начать связь из ноды", "После нажатия кликни другую ноду в визуальном редакторе, чтобы добавить условие доступа.")))
            {
                TechTreeGraphWindow.StartConnectionFromInspector(tree, nodeId.stringValue);
            }

            if (GUILayout.Button(new GUIContent("Удалить ноду", "Удаляет выбранную ноду и убирает ссылки на нее из условий доступа.")))
            {
                DeleteNode(nodes, selectedNodeIndex);
            }
        }
    }

    private void DeleteNode(SerializedProperty nodes, int selectedNodeIndex)
    {
        TechTreeDefinitionSO tree = (TechTreeDefinitionSO)target;
        string deletedNodeId = nodes.GetArrayElementAtIndex(selectedNodeIndex).FindPropertyRelative("nodeId").stringValue;

        Undo.RecordObject(tree, "Удалить узел древа техники");
        nodes.DeleteArrayElementAtIndex(selectedNodeIndex);
        serializedObject.ApplyModifiedProperties();

        for (int i = 0; i < tree.nodes.Count; i++)
        {
            tree.nodes[i].prerequisiteNodeIds.Remove(deletedNodeId);
        }

        SetSelectedNode(tree, -1);
        EditorUtility.SetDirty(tree);
        GUIUtility.ExitGUI();
    }

    private static string GetSelectedNodeSessionKey(TechTreeDefinitionSO tree)
    {
        return SelectedNodeSessionKeyPrefix + tree.GetInstanceID();
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
