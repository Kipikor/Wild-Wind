using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "НовоеДревоТехники", menuName = "Wild Wind/Мета/Древо техники")]
public class TechTreeDefinitionSO : ScriptableObject
{
    [InspectorName("Узлы древа")]
    public List<TechTreeNode> nodes = new List<TechTreeNode>();

    public TechTreeNode GetNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            TechTreeNode node = nodes[i];
            if (node != null && node.nodeId == nodeId)
            {
                return node;
            }
        }

        return null;
    }

    public bool ContainsNode(string nodeId)
    {
        return GetNode(nodeId) != null;
    }

    public List<string> ValidateTree()
    {
        List<string> issues = new List<string>();
        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < nodes.Count; i++)
        {
            TechTreeNode node = nodes[i];
            if (node == null)
            {
                issues.Add($"Узел #{i} пустой.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.nodeId))
            {
                issues.Add($"Узел #{i}: не заполнен идентификатор узла.");
                continue;
            }

            if (!ids.Add(node.nodeId))
            {
                issues.Add($"Повторяется идентификатор узла: {node.nodeId}.");
            }

            if (node.kind == TechTreeNodeKind.Ship && string.IsNullOrWhiteSpace(node.EffectiveShipId))
            {
                issues.Add($"Корабль {node.nodeId}: нет идентификатора корабля или паспорта корабля.");
            }

            if (node.kind == TechTreeNodeKind.Module && string.IsNullOrWhiteSpace(node.parentShipId))
            {
                issues.Add($"Модуль {node.nodeId}: не указан родительский корабль.");
            }

            for (int p = 0; p < node.prerequisiteNodeIds.Count; p++)
            {
                string prerequisiteId = node.prerequisiteNodeIds[p];
                if (!string.IsNullOrWhiteSpace(prerequisiteId) && GetNode(prerequisiteId) == null)
                {
                    issues.Add($"Узел {node.nodeId}: условие {prerequisiteId} не найдено в древе.");
                }
            }
        }

        return issues;
    }
}

public enum TechTreeNodeKind
{
    [InspectorName("Корабль")]
    Ship,
    [InspectorName("Модуль")]
    Module
}

public enum TechTreeModuleKind
{
    [InspectorName("Двигатель")]
    Engine,
    [InspectorName("Клавдиевый контур")]
    ClaudiumLoop,
    [InspectorName("Баллон")]
    Balloon,
    [InspectorName("Корпус")]
    Hull,
    [InspectorName("Вспомогательный модуль")]
    Utility,
    [InspectorName("Другое")]
    Other
}

[Serializable]
public class TechTreeNode
{
    [Header("Основное")]
    [InspectorName("Идентификатор узла")]
    public string nodeId = "node";
    [InspectorName("Название")]
    public string displayName = "Узел";
    [InspectorName("Тип узла")]
    public TechTreeNodeKind kind = TechTreeNodeKind.Ship;
    [InspectorName("Уровень техники")]
    [Range(1, 10)] public int tier = 1;

    [Header("Корабль")]
    [InspectorName("Паспорт корабля")]
    public ShipDefinitionSO shipDefinition;
    [InspectorName("Идентификатор корабля")]
    public string shipId = "";
    [InspectorName("Премиумная техника")]
    public bool isPremium;

    [Header("Модуль")]
    [InspectorName("Идентификатор корабля-владельца")]
    public string parentShipId = "";
    [InspectorName("Тип модуля")]
    public TechTreeModuleKind moduleKind = TechTreeModuleKind.Other;

    [Header("Прогрессия")]
    [InspectorName("Стоимость исследования, опыт")]
    public int researchCostXp;
    [InspectorName("Цена покупки, деньги")]
    public int purchasePrice;
    [InspectorName("Исследован с начала")]
    public bool startsResearched;
    [InspectorName("Куплен с начала")]
    public bool startsPurchased;
    [InspectorName("Условия доступа, любой один идентификатор")]
    public List<string> prerequisiteNodeIds = new List<string>();
    [InspectorName("С каких кораблей можно тратить опыт")]
    public List<string> experienceShipIds = new List<string>();

    [Header("Конструктор")]
    [InspectorName("Позиция в будущем нодовом редакторе")]
    public Vector2 editorPosition;

    public string EffectiveShipId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(shipId)) return shipId;
            return shipDefinition != null ? shipDefinition.shipId : "";
        }
    }
}
