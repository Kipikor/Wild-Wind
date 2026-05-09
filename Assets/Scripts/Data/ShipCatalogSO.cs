using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShipCatalog", menuName = "Wild Wind/Meta/Ship Catalog")]
public class ShipCatalogSO : ScriptableObject
{
    [InspectorName("ID стартового корабля")]
    public string starterShipId = "ship";
    [InspectorName("Корабли")]
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
