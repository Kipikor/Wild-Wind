using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "КаталогКораблей", menuName = "Wild Wind/Мета/Каталог кораблей")]
public class ShipCatalogSO : ScriptableObject
{
    [Header("Сборка")]
    [InspectorName("Идентификатор стартового корпуса")]
    [Tooltip("Корпус, который выбирается у новой игры, если в сохранении еще нет сборки.")]
    public string starterHullId = "starter_hull";

    [InspectorName("Детали корабля")]
    [Tooltip("Корпуса и модули, которые можно использовать в сборке корабля.")]
    public List<ShipPartDefinitionSO> parts = new List<ShipPartDefinitionSO>();

    public ShipPartDefinitionSO GetPartById(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId) || parts == null) return null;

        for (int i = 0; i < parts.Count; i++)
        {
            ShipPartDefinitionSO part = parts[i];
            if (part != null && part.partId == partId)
            {
                return part;
            }
        }

        return null;
    }

    public ShipPartDefinitionSO GetStarterHull()
    {
        ShipPartDefinitionSO starter = GetPartById(starterHullId);
        if (starter != null && starter.IsHull) return starter;

        if (parts == null) return null;
        for (int i = 0; i < parts.Count; i++)
        {
            ShipPartDefinitionSO part = parts[i];
            if (part != null && part.IsHull)
            {
                return part;
            }
        }

        return null;
    }

    public bool HasAssemblyParts()
    {
        return GetStarterHull() != null;
    }
}
