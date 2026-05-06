using UnityEngine;

public class MetaGameState : MonoBehaviour
{
    public ShipCatalogSO catalog;
    public ShipLoader shipLoader;
    public int startingMoney;
    public PlayerProgress progress = new PlayerProgress();

    private bool initialized;
    private ShipCatalogSO ActiveCatalog => catalog != null ? catalog : shipLoader != null ? shipLoader.catalog : null;

    private void Reset()
    {
        shipLoader = FindFirstObjectByType<ShipLoader>();
    }

    private void Awake()
    {
        EnsureProgressInitialized();
    }

    private void Start()
    {
        ApplySelectedShip();
    }

    public void EnsureProgressInitialized()
    {
        if (initialized) return;

        ShipDefinitionSO starterShip = ActiveCatalog != null ? ActiveCatalog.GetStarterShip() : null;
        if (starterShip != null)
        {
            progress.EnsureStarterShip(starterShip.shipId);
        }

        if (progress.money < startingMoney)
        {
            progress.money = startingMoney;
        }

        initialized = true;
    }

    public ShipDefinitionSO ApplySelectedShip()
    {
        if (shipLoader == null) return null;

        EnsureProgressInitialized();
        if (shipLoader.catalog == null)
        {
            shipLoader.catalog = ActiveCatalog;
        }

        return shipLoader.ApplyShip(progress.selectedShipId);
    }

    public bool SelectShip(string shipId)
    {
        EnsureProgressInitialized();
        if (!progress.SelectShip(shipId)) return false;

        ApplySelectedShip();
        return true;
    }

    public bool TryBuyShip(string shipId)
    {
        ShipCatalogSO activeCatalog = ActiveCatalog;
        if (activeCatalog == null) return false;

        EnsureProgressInitialized();

        ShipDefinitionSO ship = activeCatalog.GetShipById(shipId);
        if (ship == null) return false;
        if (progress.IsShipUnlocked(ship.shipId)) return false;
        if (progress.money < ship.purchasePrice) return false;

        progress.money -= ship.purchasePrice;
        progress.UnlockShip(ship.shipId);
        return true;
    }
}
