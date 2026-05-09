using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "КаталогКораблей", menuName = "Wild Wind/Мета/Каталог кораблей")]
public class ShipCatalogSO : ScriptableObject
{
    [InspectorName("Идентификатор стартового корабля")]
    [Tooltip("Идентификатор корабля, который игрок получает при первом запуске новой игры.")]
    public string starterShipId = "ship";
    [InspectorName("Корабли")]
    [Tooltip("Все корабли, которые могут быть открыты, куплены или выбраны через мета-прогресс.")]
    public List<ShipDefinitionSO> ships = new List<ShipDefinitionSO>();

    public ShipDefinitionSO GetShipById(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;

        for (int i = 0; i < ships.Count; i++)
        {
            ShipDefinitionSO ship = ships[i];
            if (ship != null && ship.shipId == shipId)
            {
                return ship;
            }
        }

        return null;
    }

    public ShipDefinitionSO GetStarterShip()
    {
        ShipDefinitionSO starter = GetShipById(starterShipId);
        if (starter != null) return starter;

        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i] != null) return ships[i];
        }

        return null;
    }

    public bool ContainsShip(string shipId)
    {
        return GetShipById(shipId) != null;
    }
}
