using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TechTreeGraphWindow : EditorWindow
{
    private const string TreePath = "Assets/Data/TechTrees/WildWindTechTree.asset";
    private const float NodeWidth = 190f;
    private const float NodeHeight = 92f;
    private const float ToolbarHeight = 28f;
    private const float MinZoom = 0.35f;
    private const float MaxZoom = 1.8f;

    private TechTreeDefinitionSO tree;
    private Vector2 pan;
    private Vector2 canvasDragStart;
    private float zoom = 1f;
    private int selectedNodeIndex = -1;
    private int draggedNodeIndex = -1;
    private string pendingConnectionSourceId = "";

    private GUIStyle shipNodeStyle;
    private GUIStyle moduleNodeStyle;
    private GUIStyle premiumNodeStyle;
    private GUIStyle selectedNodeStyle;

    public static void OpenWindow()
    {
        TechTreeGraphWindow window = GetWindow<TechTreeGraphWindow>("Древо техники");
        window.LoadDefaultTree();
    }

    private void OnEnable()
    {
        LoadDefaultTree();
    }

    private void LoadDefaultTree()
    {
        tree = AssetDatabase.LoadAssetAtPath<TechTreeDefinitionSO>(TreePath);
    }

    private void CreateStyles()
    {
        shipNodeStyle = CreateNodeStyle(new Color(0.24f, 0.34f, 0.46f));
        moduleNodeStyle = CreateNodeStyle(new Color(0.32f, 0.39f, 0.28f));
        premiumNodeStyle = CreateNodeStyle(new Color(0.54f, 0.42f, 0.18f));
        selectedNodeStyle = CreateNodeStyle(new Color(0.18f, 0.48f, 0.72f));
    }

    private static GUIStyle CreateNodeStyle(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, color);
        texture.Apply();

        GUIStyle baseStyle = EditorStyles.helpBox ?? GUI.skin.box ?? GUIStyle.none;
        return new GUIStyle(baseStyle)
        {
            normal = { background = texture, textColor = Color.white },
            padding = new RectOffset(10, 10, 8, 8),
            alignment = TextAnchor.UpperLeft
        };
    }

    private void OnGUI()
    {
        if (shipNodeStyle == null)
        {
            CreateStyles();
        }

        DrawToolbar();

        if (tree == null)
        {
            DrawMissingTreeMessage();
            return;
        }

        Rect canvasRect = new Rect(0f, ToolbarHeight, position.width, position.height - ToolbarHeight);

        DrawCanvas(canvasRect);
        HandleCanvasEvents(canvasRect);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(ToolbarHeight)))
        {
            EditorGUI.BeginChangeCheck();
            TechTreeDefinitionSO selectedTree = (TechTreeDefinitionSO)EditorGUILayout.ObjectField(tree, typeof(TechTreeDefinitionSO), false, GUILayout.Width(320f));
            if (EditorGUI.EndChangeCheck())
            {
                tree = selectedTree;
                SelectNode(-1);
                Selection.activeObject = tree;
            }

            if (GUILayout.Button("Открыть стандартное", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                LoadDefaultTree();
                Selection.activeObject = tree;
            }

            if (GUILayout.Button("Добавить корпус", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                AddNode(TechTreeNodeKind.Hull);
            }

            if (GUILayout.Button("Добавить модуль", EditorStyles.toolbarButton, GUILayout.Width(130f)))
            {
                AddNode(TechTreeNodeKind.Module);
            }

            if (GUILayout.Button("Проверить", EditorStyles.toolbarButton, GUILayout.Width(90f)))
            {
                ValidateTree();
            }

            GUILayout.Space(8f);
            GUILayout.Label($"Масштаб: {Mathf.RoundToInt(zoom * 100f)}%", EditorStyles.miniBoldLabel, GUILayout.Width(95f));
            if (GUILayout.Button("100%", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                zoom = 1f;
            }

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(pendingConnectionSourceId))
            {
                GUILayout.Label($"Связь из: {pendingConnectionSourceId}", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("Отмена", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    pendingConnectionSourceId = "";
                }
            }
        }
    }

    private void DrawMissingTreeMessage()
    {
        EditorGUILayout.Space(24f);
        EditorGUILayout.HelpBox($"Древо не найдено по пути {TreePath}. Создай его через Wild Wind > Древо техники > Создать стартовое древо.", MessageType.Warning);
    }

    private void DrawCanvas(Rect canvasRect)
    {
        GUI.Box(canvasRect, GUIContent.none);
        DrawGrid(canvasRect, 24f, new Color(1f, 1f, 1f, 0.08f));
        DrawGrid(canvasRect, 120f, new Color(1f, 1f, 1f, 0.14f));

        GUI.BeginGroup(canvasRect);
        Rect localCanvasRect = new Rect(0f, 0f, canvasRect.width, canvasRect.height);

        DrawConnections(localCanvasRect);
        DrawNodes();

        GUI.EndGroup();
    }

    private void DrawGrid(Rect canvasRect, float spacing, Color color)
    {
        Handles.BeginGUI();
        Handles.color = color;

        float scaledSpacing = Mathf.Max(8f, spacing * zoom);
        float widthDivs = Mathf.Ceil(canvasRect.width / scaledSpacing);
        float heightDivs = Mathf.Ceil(canvasRect.height / scaledSpacing);
        Vector3 offset = new Vector3(pan.x % scaledSpacing, pan.y % scaledSpacing, 0f);

        for (int i = 0; i < widthDivs; i++)
        {
            float x = canvasRect.x + scaledSpacing * i + offset.x;
            Handles.DrawLine(new Vector3(x, canvasRect.y, 0f), new Vector3(x, canvasRect.yMax, 0f));
        }

        for (int j = 0; j < heightDivs; j++)
        {
            float y = canvasRect.y + scaledSpacing * j + offset.y;
            Handles.DrawLine(new Vector3(canvasRect.x, y, 0f), new Vector3(canvasRect.xMax, y, 0f));
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawConnections(Rect canvasRect)
    {
        Handles.BeginGUI();

        for (int targetIndex = 0; targetIndex < tree.nodes.Count; targetIndex++)
        {
            TechTreeNode targetNode = tree.nodes[targetIndex];
            if (targetNode == null) continue;

            Rect targetRect = GetNodeRect(targetNode);
            for (int p = 0; p < targetNode.prerequisiteNodeIds.Count; p++)
            {
                TechTreeNode sourceNode = tree.GetNode(targetNode.prerequisiteNodeIds[p]);
                if (sourceNode == null) continue;

                Rect sourceRect = GetNodeRect(sourceNode);
                DrawConnection(sourceRect, targetRect, targetNode.isPremium ? new Color(1f, 0.74f, 0.25f) : new Color(0.55f, 0.82f, 1f));
            }
        }

        Handles.EndGUI();
    }

    private void DrawConnection(Rect sourceRect, Rect targetRect, Color color)
    {
        Vector3 start = new Vector3(sourceRect.xMax, sourceRect.center.y, 0f);
        Vector3 end = new Vector3(targetRect.xMin, targetRect.center.y, 0f);
        Vector3 startTangent = start + Vector3.right * 70f;
        Vector3 endTangent = end + Vector3.left * 70f;

        Handles.DrawBezier(start, end, startTangent, endTangent, color, null, 3f);
        DrawArrow(end, color);
    }

    private void DrawArrow(Vector3 tip, Color color)
    {
        Handles.color = color;
        Handles.DrawAAConvexPolygon(
            tip,
            tip + new Vector3(-10f, -5f, 0f),
            tip + new Vector3(-10f, 5f, 0f));
        Handles.color = Color.white;
    }

    private void DrawNodes()
    {
        for (int i = 0; i < tree.nodes.Count; i++)
        {
            TechTreeNode node = tree.nodes[i];
            if (node == null) continue;

            Rect nodeRect = GetNodeRect(node);
            GUIStyle style = GetNodeStyle(node, i == selectedNodeIndex);

            GUI.Box(nodeRect, GUIContent.none, style);
            GUILayout.BeginArea(nodeRect);
            GUILayout.Label(node.displayName, EditorStyles.boldLabel);
            GUILayout.Label(GetNodeSubtitle(node), EditorStyles.miniLabel);
            GUILayout.Space(4f);
            GUILayout.Label($"Опыт: {node.researchCostXp}    Деньги: {node.purchasePrice}", EditorStyles.miniLabel);
            if (node.prerequisiteNodeIds.Count > 0)
            {
                GUILayout.Label($"Входов: {node.prerequisiteNodeIds.Count}", EditorStyles.miniLabel);
            }
            GUILayout.EndArea();
        }
    }

    private GUIStyle GetNodeStyle(TechTreeNode node, bool selected)
    {
        if (selected) return selectedNodeStyle;
        if (node.isPremium) return premiumNodeStyle;
        return node.kind == TechTreeNodeKind.Hull ? shipNodeStyle : moduleNodeStyle;
    }

    private string GetNodeSubtitle(TechTreeNode node)
    {
        if (node.kind == TechTreeNodeKind.Hull)
        {
            string premium = node.isPremium ? " | премиум" : "";
            return $"Корпус | уровень {node.tier}{premium}";
        }

        if (node.kind == TechTreeNodeKind.Fundamental)
        {
            return $"Исследование | уровень {node.tier}";
        }

        return $"Модуль | {GetModuleName(node.moduleKind)}";
    }

    private static string GetModuleName(TechTreeModuleKind moduleKind)
    {
        switch (moduleKind)
        {
            case TechTreeModuleKind.Engine: return "двигатель";
            case TechTreeModuleKind.ClaudiumLoop: return "контур";
            case TechTreeModuleKind.DeprecatedBalloon: return "не используется";
            case TechTreeModuleKind.Hull: return "корпус";
            case TechTreeModuleKind.Utility: return "вспом.";
            default: return "другое";
        }
    }

    private Rect GetNodeRect(TechTreeNode node)
    {
        return new Rect(WorldToCanvas(node.editorPosition), new Vector2(NodeWidth * zoom, NodeHeight * zoom));
    }

    private void HandleCanvasEvents(Rect canvasRect)
    {
        Event current = Event.current;
        Vector2 localMouse = current.mousePosition - canvasRect.position;

        if (!canvasRect.Contains(current.mousePosition)) return;

        if (current.type == EventType.MouseDown && current.button == 0)
        {
            int clickedNode = GetNodeIndexAt(localMouse);
            if (clickedNode >= 0)
            {
                if (!string.IsNullOrEmpty(pendingConnectionSourceId))
                {
                    AddConnection(pendingConnectionSourceId, tree.nodes[clickedNode]);
                    pendingConnectionSourceId = "";
                }
                else
                {
                    SelectNode(clickedNode);
                    draggedNodeIndex = clickedNode;
                }

                current.Use();
            }
            else
            {
                SelectNode(-1);
                draggedNodeIndex = -1;
            }
        }

        if (current.type == EventType.MouseDrag && current.button == 0 && draggedNodeIndex >= 0)
        {
            Undo.RecordObject(tree, "Переместить узел древа техники");
            tree.nodes[draggedNodeIndex].editorPosition += current.delta / zoom;
            EditorUtility.SetDirty(tree);
            current.Use();
            Repaint();
        }

        if (current.type == EventType.MouseUp && current.button == 0)
        {
            draggedNodeIndex = -1;
        }

        if (current.type == EventType.MouseDown && current.button == 2)
        {
            canvasDragStart = current.mousePosition;
            current.Use();
        }

        if (current.type == EventType.MouseDrag && current.button == 2)
        {
            pan += current.mousePosition - canvasDragStart;
            canvasDragStart = current.mousePosition;
            current.Use();
            Repaint();
        }

        if (current.type == EventType.ScrollWheel)
        {
            ZoomAt(localMouse, -current.delta.y * 0.05f);
            current.Use();
            Repaint();
        }

        if (current.type == EventType.ContextClick)
        {
            int clickedNode = GetNodeIndexAt(localMouse);
            if (clickedNode >= 0)
            {
                ShowNodeContextMenu(clickedNode);
                current.Use();
            }
        }
    }

    private int GetNodeIndexAt(Vector2 localMouse)
    {
        for (int i = tree.nodes.Count - 1; i >= 0; i--)
        {
            TechTreeNode node = tree.nodes[i];
            if (node != null && GetNodeRect(node).Contains(localMouse))
            {
                return i;
            }
        }

        return -1;
    }

    private void AddNode(TechTreeNodeKind kind)
    {
        if (tree == null) return;

        Undo.RecordObject(tree, "Добавить узел древа техники");

        int index = tree.nodes.Count + 1;
        TechTreeNode node = new TechTreeNode
        {
            nodeId = kind == TechTreeNodeKind.Hull ? $"hull_node_{index}" : $"module_node_{index}",
            displayName = kind == TechTreeNodeKind.Hull ? $"Корпус {index}" : $"Модуль {index}",
            kind = kind,
            tier = 1,
            researchCostXp = kind == TechTreeNodeKind.Hull ? 100 * index : 50,
            purchasePrice = kind == TechTreeNodeKind.Hull ? 250 * index : 100,
            editorPosition = CanvasToWorld(new Vector2(80f, 80f))
        };

        tree.nodes.Add(node);
        SelectNode(tree.nodes.Count - 1);
        EditorUtility.SetDirty(tree);
    }

    private void AddConnection(string sourceNodeId, TechTreeNode targetNode)
    {
        if (string.IsNullOrWhiteSpace(sourceNodeId) || targetNode == null) return;
        if (sourceNodeId == targetNode.nodeId) return;
        if (targetNode.prerequisiteNodeIds.Contains(sourceNodeId)) return;

        Undo.RecordObject(tree, "Добавить связь древа техники");
        targetNode.prerequisiteNodeIds.Add(sourceNodeId);
        EditorUtility.SetDirty(tree);
        Repaint();
    }

    private void DeleteSelectedNode()
    {
        if (selectedNodeIndex < 0 || selectedNodeIndex >= tree.nodes.Count) return;

        string deletedNodeId = tree.nodes[selectedNodeIndex].nodeId;

        Undo.RecordObject(tree, "Удалить узел древа техники");
        tree.nodes.RemoveAt(selectedNodeIndex);

        for (int i = 0; i < tree.nodes.Count; i++)
        {
            tree.nodes[i].prerequisiteNodeIds.Remove(deletedNodeId);
        }

        SelectNode(-1);
        pendingConnectionSourceId = "";
        EditorUtility.SetDirty(tree);
    }

    private void ShowNodeContextMenu(int nodeIndex)
    {
        SelectNode(nodeIndex);
        TechTreeNode node = tree.nodes[nodeIndex];

        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Начать связь из этого узла"), false, () => pendingConnectionSourceId = node.nodeId);
        menu.AddItem(new GUIContent("Удалить узел"), false, DeleteSelectedNode);
        menu.ShowAsContext();
    }

    public static void StartConnectionFromInspector(TechTreeDefinitionSO targetTree, string sourceNodeId)
    {
        if (targetTree == null || string.IsNullOrWhiteSpace(sourceNodeId)) return;

        TechTreeGraphWindow[] windows = Resources.FindObjectsOfTypeAll<TechTreeGraphWindow>();
        for (int i = 0; i < windows.Length; i++)
        {
            TechTreeGraphWindow window = windows[i];
            if (window == null || window.tree != targetTree) continue;

            window.pendingConnectionSourceId = sourceNodeId;
            window.Repaint();
        }
    }

    private void SelectNode(int nodeIndex)
    {
        selectedNodeIndex = nodeIndex;
        TechTreeConstructorEditor.SetSelectedNode(tree, nodeIndex);
        Repaint();
    }

    private void ValidateTree()
    {
        if (tree == null) return;

        List<string> issues = tree.ValidateTree();
        if (issues.Count == 0)
        {
            EditorUtility.DisplayDialog("Проверка древа техники", "Древо выглядит корректно.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Проверка древа техники", string.Join("\n", issues), "OK");
    }

    private Vector2 WorldToCanvas(Vector2 worldPosition)
    {
        return worldPosition * zoom + pan;
    }

    private Vector2 CanvasToWorld(Vector2 canvasPosition)
    {
        return (canvasPosition - pan) / zoom;
    }

    private void ZoomAt(Vector2 canvasMousePosition, float zoomDelta)
    {
        float oldZoom = zoom;
        float newZoom = Mathf.Clamp(zoom + zoomDelta, MinZoom, MaxZoom);
        if (Mathf.Approximately(oldZoom, newZoom)) return;

        Vector2 worldUnderMouse = CanvasToWorld(canvasMousePosition);
        zoom = newZoom;
        pan = canvasMousePosition - worldUnderMouse * zoom;
    }
}
