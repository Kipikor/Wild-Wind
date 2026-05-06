using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgress
{
    public int money;
    public string selectedShipId = "";
    public List<string> unlockedShipIds = new List<string>();

    public bool IsShipUnlocked(string shipId)
    {
        return !string.IsNullOrWhiteSpace(shipId) && unlockedShipIds.Contains(shipId);
    }

    public void EnsureStarterShip(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return;

        UnlockShip(shipId);

        if (string.IsNullOrWhiteSpace(selectedShipId))
        {
            selectedShipId = shipId;
        }
    }

    public bool UnlockShip(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return false;
        if (unlockedShipIds.Contains(shipId)) return false;

        unlockedShipIds.Add(shipId);
        return true;
    }

    public bool SelectShip(string shipId)
    {
        if (!IsShipUnlocked(shipId)) return false;

        selectedShipId = shipId;
        return true;
    }
}
