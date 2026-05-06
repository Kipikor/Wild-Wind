using UnityEngine;

public class ShipLoader : MonoBehaviour
{
    public ShipCatalogSO catalog;
    public ShipPhysics targetShip;
    public string fallbackShipId = "";
    public bool applyOnStart = false;

    private void Reset()
    {
        targetShip = FindFirstObjectByType<ShipPhysics>();
    }

    private void Start()
    {
        if (!applyOnStart) return;

        string shipId = string.IsNullOrWhiteSpace(fallbackShipId) ? catalog?.starterShipId : fallbackShipId;
        ApplyShip(shipId);
    }

    public ShipDefinitionSO ApplyShip(string shipId)
    {
        ShipDefinitionSO definition = catalog != null ? catalog.GetShipById(shipId) : null;

        if (definition == null && catalog != null)
        {
            definition = catalog.GetStarterShip();
        }

        return ApplyShip(definition);
    }

    public ShipDefinitionSO ApplyShip(ShipDefinitionSO definition)
    {
        if (definition == null || targetShip == null) return null;

        targetShip.ApplyShipDefinition(definition);
        return definition;
    }
}
