using UnityEngine;

public static class WildWindStarterDelivery
{
    public const string FirstMissionId = "intro_food_to_aerolite";
    public const string SecondMissionId = "intro_aerolite_to_capital";
    public const string MissionId = FirstMissionId;
    public const string FoodItemId = "food";
    public const string AeroliteItemId = "aerolite";
    public const string SourceDockId = "Island1";
    public const string DestinationDockId = "Island2";
    public const string DestinationDisplayNameRu = "Аэролитовый остров";
    public const int DeliveryAmount = 20;
    public const int StartingCapitalFood = 40;
    public const float ExpectedDestinationDistanceMeters = 5371.45f;

    private static readonly DeliveryLeg[] Legs =
    {
        new DeliveryLeg
        {
            missionId = FirstMissionId,
            itemId = FoodItemId,
            cargoNameRu = "еда",
            sourceDockId = SourceDockId,
            sourceNameRu = "Ферма отца",
            destinationDockId = DestinationDockId,
            destinationNameRu = DestinationDisplayNameRu,
            amount = DeliveryAmount,
            startingSourceStock = StartingCapitalFood,
            missionTitleRu = "Отвези еду на аэролитовый остров",
            completionMessageRu = "Еда доставлена. Теперь можно поговорить со знакомой на аэролитовом острове."
        },
        new DeliveryLeg
        {
            missionId = SecondMissionId,
            itemId = AeroliteItemId,
            cargoNameRu = "аэролит",
            sourceDockId = DestinationDockId,
            sourceNameRu = DestinationDisplayNameRu,
            destinationDockId = "capital",
            destinationNameRu = "Столица",
            amount = 12,
            startingSourceStock = 24,
            missionTitleRu = "Отвези аэролит в столицу",
            completionMessageRu = "Аэролит доставлен. В столице есть инженер, которому стоит показать автоматона."
        }
    };

    public static void SeedNewGame(PlayerProgress progress)
    {
        if (progress == null)
        {
            return;
        }

        progress.Normalize();
        for (int i = 0; i < Legs.Length; i++)
        {
            DeliveryLeg leg = Legs[i];
            IslandProductionState source = progress.GetIslandProductionState(leg.sourceDockId, true);
            if (source.GetResourceAmount(leg.itemId) < leg.startingSourceStock)
            {
                source.SetResourceAmount(leg.itemId, leg.startingSourceStock);
            }

            progress.GetIslandProductionState(leg.destinationDockId, true);
        }

        progress.AcceptMission(FirstMissionId);
    }

    public static bool HasActiveDelivery(PlayerProgress progress)
    {
        return GetActiveLeg(progress) != null;
    }

    public static bool IsCompleted(PlayerProgress progress)
    {
        return progress != null && progress.IsMissionCompleted(SecondMissionId);
    }

    public static string GetMissionTitle(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? leg.missionTitleRu : "Первый маршрут готов";
    }

    public static string GetRouteText(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null
            ? "Маршрут: " + leg.sourceNameRu + " -> " + leg.destinationNameRu
            : "Маршрут: обучающий треугольник открыт";
    }

    public static string GetSourceStockLabel(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? leg.sourceNameRu + ", " + leg.cargoNameRu : "Источник";
    }

    public static string GetDestinationStockLabel(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? leg.destinationNameRu + ", " + leg.cargoNameRu : "Получатель";
    }

    public static string GetShipCargoLabel(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? "Трюм, " + leg.cargoNameRu : "Трюм";
    }

    public static string GetLoadButtonText(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? "Загрузить " + leg.cargoNameRu : "Загрузить";
    }

    public static string GetUnloadButtonText(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? "Выгрузить " + leg.cargoNameRu : "Выгрузить";
    }

    public static string GetActiveSourceDockId(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? leg.sourceDockId : "";
    }

    public static string GetActiveDestinationDockId(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? leg.destinationDockId : "";
    }

    public static int GetActiveDeliveryAmount(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? leg.amount : 0;
    }

    public static int GetSourceStock(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? GetIslandResource(progress, leg.sourceDockId, leg.itemId) : 0;
    }

    public static int GetDestinationStock(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? GetIslandResource(progress, leg.destinationDockId, leg.itemId) : 0;
    }

    public static int GetShipCargo(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null && progress != null ? progress.GetShipCargoAmount(leg.itemId) : 0;
    }

    public static int GetCapitalFood(PlayerProgress progress)
    {
        return GetSourceStock(progress);
    }

    public static int GetDestinationFood(PlayerProgress progress)
    {
        return GetDestinationStock(progress);
    }

    public static int GetShipFood(PlayerProgress progress)
    {
        return GetShipCargo(progress);
    }

    public static int GetRemainingDelivery(PlayerProgress progress)
    {
        DeliveryLeg leg = GetActiveLeg(progress);
        return leg != null ? GetRemainingDelivery(progress, leg) : 0;
    }

    public static bool TryLoadFood(MetaGameState meta, out string message)
    {
        message = "";
        if (!ResolveMeta(meta, out PlayerProgress progress, out message))
        {
            return false;
        }

        DeliveryLeg leg = GetActiveLeg(progress);
        if (leg == null)
        {
            message = "Первые доставки уже выполнены.";
            return false;
        }

        if (progress.currentMode != GameSessionMode.Docked || progress.currentDockId != leg.sourceDockId)
        {
            message = leg.cargoNameRu + " можно загрузить только на острове: " + leg.sourceNameRu + ".";
            return false;
        }

        int remaining = GetRemainingDelivery(progress, leg);
        int alreadyOnShip = progress.GetShipCargoAmount(leg.itemId);
        int amountToLoad = Mathf.Clamp(remaining - alreadyOnShip, 0, leg.amount);
        if (amountToLoad <= 0)
        {
            message = leg.cargoNameRu + " уже в трюме.";
            return true;
        }

        if (!meta.TryLoadShipCargoFromCurrentDock(leg.itemId, amountToLoad, out message))
        {
            return false;
        }

        progress.AcceptMission(leg.missionId);
        message = "Загружено: " + leg.cargoNameRu + " x" + amountToLoad + ".";
        return true;
    }

    public static bool TryUnloadFood(MetaGameState meta, out string message)
    {
        message = "";
        if (!ResolveMeta(meta, out PlayerProgress progress, out message))
        {
            return false;
        }

        DeliveryLeg leg = GetActiveLeg(progress);
        if (leg == null)
        {
            message = "Первые доставки уже выполнены.";
            return true;
        }

        if (progress.currentMode != GameSessionMode.Docked || progress.currentDockId != leg.destinationDockId)
        {
            message = leg.cargoNameRu + " нужно выгрузить на острове: " + leg.destinationNameRu + ".";
            return false;
        }

        int shipCargo = progress.GetShipCargoAmount(leg.itemId);
        int remaining = GetRemainingDelivery(progress, leg);
        int amountToUnload = Mathf.Min(shipCargo, remaining);
        if (amountToUnload <= 0)
        {
            message = "В трюме нет нужного груза для доставки.";
            return false;
        }

        if (!meta.TryUnloadShipCargoToCurrentDock(leg.itemId, amountToUnload, out message, false))
        {
            return false;
        }

        if (GetDestinationStock(progress) >= leg.amount)
        {
            progress.CompleteMission(leg.missionId);
            AcceptNextLeg(progress, leg);
            message = leg.completionMessageRu;
        }
        else
        {
            message = "Выгружено: " + leg.cargoNameRu + " x" + amountToUnload + ".";
        }

        return true;
    }

    private static void AcceptNextLeg(PlayerProgress progress, DeliveryLeg completedLeg)
    {
        if (progress == null || completedLeg == null)
        {
            return;
        }

        for (int i = 0; i < Legs.Length - 1; i++)
        {
            if (Legs[i] == completedLeg)
            {
                progress.AcceptMission(Legs[i + 1].missionId);
                return;
            }
        }
    }

    private static int GetRemainingDelivery(PlayerProgress progress, DeliveryLeg leg)
    {
        if (progress == null || leg == null || progress.IsMissionCompleted(leg.missionId))
        {
            return 0;
        }

        return Mathf.Max(0, leg.amount - GetIslandResource(progress, leg.destinationDockId, leg.itemId));
    }

    private static DeliveryLeg GetActiveLeg(PlayerProgress progress)
    {
        if (progress == null)
        {
            return Legs[0];
        }

        for (int i = 0; i < Legs.Length; i++)
        {
            DeliveryLeg leg = Legs[i];
            if (!progress.IsMissionCompleted(leg.missionId))
            {
                if (i == 0 || progress.IsMissionCompleted(Legs[i - 1].missionId))
                {
                    progress.AcceptMission(leg.missionId);
                    return leg;
                }

                return Legs[i - 1];
            }
        }

        return null;
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

    private static int GetIslandResource(PlayerProgress progress, string islandId, string itemId)
    {
        if (progress == null || string.IsNullOrWhiteSpace(islandId) || string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        IslandProductionState state = progress.GetIslandProductionState(islandId, false);
        return state != null ? state.GetResourceAmount(itemId) : 0;
    }

    private sealed class DeliveryLeg
    {
        public string missionId;
        public string itemId;
        public string cargoNameRu;
        public string sourceDockId;
        public string sourceNameRu;
        public string destinationDockId;
        public string destinationNameRu;
        public int amount;
        public int startingSourceStock;
        public string missionTitleRu;
        public string completionMessageRu;
    }
}
