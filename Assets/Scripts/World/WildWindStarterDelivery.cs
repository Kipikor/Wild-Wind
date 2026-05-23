using UnityEngine;

public static class WildWindStarterDelivery
{
    public const string MissionId = "starter_food_delivery";
    public const string FoodItemId = "food";
    public const string SourceDockId = "capital";
    public const string DestinationDockId = "starter_delivery_island";
    public const string DestinationDisplayNameRu = "Ближний остров";
    public const int DeliveryAmount = 20;
    public const int StartingCapitalFood = 40;
    public const float ExpectedDestinationDistanceMeters = 1000f;

    public static void SeedNewGame(PlayerProgress progress)
    {
        if (progress == null)
        {
            return;
        }

        progress.Normalize();
        IslandProductionState capital = progress.GetIslandProductionState(SourceDockId, true);
        if (capital.GetResourceAmount(FoodItemId) < StartingCapitalFood)
        {
            capital.SetResourceAmount(FoodItemId, StartingCapitalFood);
        }

        IslandProductionState destination = progress.GetIslandProductionState(DestinationDockId, true);
        destination.SetResourceAmount(FoodItemId, 0);

        progress.AcceptMission(MissionId);
    }

    public static int GetCapitalFood(PlayerProgress progress)
    {
        return GetIslandFood(progress, SourceDockId);
    }

    public static int GetDestinationFood(PlayerProgress progress)
    {
        return GetIslandFood(progress, DestinationDockId);
    }

    public static int GetShipFood(PlayerProgress progress)
    {
        return progress != null ? progress.GetShipCargoAmount(FoodItemId) : 0;
    }

    public static bool IsCompleted(PlayerProgress progress)
    {
        return progress != null && progress.IsMissionCompleted(MissionId);
    }

    public static int GetRemainingDelivery(PlayerProgress progress)
    {
        if (IsCompleted(progress))
        {
            return 0;
        }

        return Mathf.Max(0, DeliveryAmount - GetDestinationFood(progress));
    }

    public static bool TryLoadFood(MetaGameState meta, out string message)
    {
        message = "";
        if (!ResolveMeta(meta, out PlayerProgress progress, out message))
        {
            return false;
        }

        if (IsCompleted(progress))
        {
            message = "Доставка уже выполнена.";
            return false;
        }

        if (progress.currentMode != GameSessionMode.Docked || progress.currentDockId != SourceDockId)
        {
            message = "Еду можно загрузить только в столице.";
            return false;
        }

        int remaining = GetRemainingDelivery(progress);
        int alreadyOnShip = GetShipFood(progress);
        int amountToLoad = Mathf.Clamp(remaining - alreadyOnShip, 0, DeliveryAmount);
        if (amountToLoad <= 0)
        {
            message = "Еда уже в трюме.";
            return true;
        }

        if (!meta.TryLoadShipCargoFromCurrentDock(FoodItemId, amountToLoad, out message))
        {
            return false;
        }

        progress.AcceptMission(MissionId);
        message = "Еда загружена: " + amountToLoad + ".";
        return true;
    }

    public static bool TryUnloadFood(MetaGameState meta, out string message)
    {
        message = "";
        if (!ResolveMeta(meta, out PlayerProgress progress, out message))
        {
            return false;
        }

        if (IsCompleted(progress))
        {
            message = "Доставка уже выполнена.";
            return true;
        }

        if (progress.currentMode != GameSessionMode.Docked || progress.currentDockId != DestinationDockId)
        {
            message = "Еду нужно выгрузить на Ближнем острове.";
            return false;
        }

        int shipFood = GetShipFood(progress);
        int remaining = GetRemainingDelivery(progress);
        int amountToUnload = Mathf.Min(shipFood, remaining);
        if (amountToUnload <= 0)
        {
            message = "В трюме нет еды для доставки.";
            return false;
        }

        if (!meta.TryUnloadShipCargoToCurrentDock(FoodItemId, amountToUnload, out message, false))
        {
            return false;
        }

        if (GetDestinationFood(progress) >= DeliveryAmount)
        {
            progress.CompleteMission(MissionId);
            message = "Доставка выполнена.";
        }
        else
        {
            message = "Еда выгружена: " + amountToUnload + ".";
        }

        return true;
    }

    private static bool ResolveMeta(MetaGameState meta, out PlayerProgress progress, out string message)
    {
        progress = null;
        if (meta == null)
        {
            message = "MetaGameState не найден.";
            return false;
        }

        meta.EnsureProgressInitialized();
        progress = meta.progress;
        if (progress == null)
        {
            message = "Состояние игрока не найдено.";
            return false;
        }

        message = "";
        return true;
    }

    private static int GetIslandFood(PlayerProgress progress, string islandId)
    {
        if (progress == null || string.IsNullOrWhiteSpace(islandId))
        {
            return 0;
        }

        IslandProductionState state = progress.GetIslandProductionState(islandId, false);
        return state != null ? state.GetResourceAmount(FoodItemId) : 0;
    }
}
