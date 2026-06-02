using System.Collections.Generic;
using UnityEngine;

public static class IslandSocietySimulator
{
    private const string CapitalIslandId = "capital";
    private static readonly Dictionary<IslandNeedKind, int> LoadScratch = new Dictionary<IslandNeedKind, int>();

    public static void EnsureIslandStates(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || !config.isLoaded || progress == null) return;
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return;

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(island.id, true);
            EnsureNeedStates(config, islandState, island.id);
        }
    }

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, float deltaMinutes)
    {
        if (config == null || !config.isLoaded || progress == null || deltaMinutes <= 0f) return 0;
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return 0;

        EnsureIslandStates(config, progress);

        int changed = 0;
        float deltaHours = Mathf.Max(0f, deltaMinutes / 60f);
        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(island.id, true);
            BuildIslandNeedLoad(config, islandState, island.id, LoadScratch);

            for (int n = 0; n < config.islandSocialNeeds.Count; n++)
            {
                IslandSocialNeedConfig need = config.islandSocialNeeds[n];
                if (need == null || string.IsNullOrWhiteSpace(need.id)) continue;
                if (ShouldSkipNeedForIsland(need, island.id, islandState)) continue;

                IslandSocietyNeedState state = islandState.GetSocietyNeedState(need.id, true);
                InitializeNeedState(state, need);

                int load = LoadScratch.TryGetValue(need.kind, out int value) ? value : 0;
                float decay = (need.baseDecayPerHour + load * need.loadDecayPerHour) * deltaHours;
                if (decay > 0f)
                {
                    state.currentValue = Mathf.Max(0f, state.currentValue - decay);
                }

                changed += RestoreNeedFromStorage(config, islandState, state, need, island.id, deltaHours);
            }
        }

        return changed;
    }

    public static float CalculateProductionMultiplier(WorldConfigDatabase config, IslandProductionState islandState, IslandIndustryConfig industry)
    {
        if (config == null || islandState == null || industry == null || string.IsNullOrWhiteSpace(industry.buildingId)) return 1f;

        IslandBuildingConfig building = config.GetIslandBuilding(industry.buildingId);
        if (building == null || building.TotalNeedLoad <= 0) return 1f;

        int unmetNeeds = 0;
        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null || building.GetNeedLoad(need.kind) <= 0) continue;

            IslandSocietyNeedState state = islandState.GetSocietyNeedState(need.id, true);
            InitializeNeedState(state, need);
            if (state.currentValue < need.satisfiedThreshold)
            {
                unmetNeeds++;
            }
        }

        return unmetNeeds <= 0 ? 1f : Mathf.Pow(0.5f, unmetNeeds);
    }

    private static void EnsureNeedStates(WorldConfigDatabase config, IslandProductionState islandState, string islandId)
    {
        if (config == null || islandState == null) return;

        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null || string.IsNullOrWhiteSpace(need.id)) continue;
            if (ShouldSkipNeedForIsland(need, islandId, islandState)) continue;

            IslandSocietyNeedState state = islandState.GetSocietyNeedState(need.id, true);
            InitializeNeedState(state, need);
        }
    }

    private static void InitializeNeedState(IslandSocietyNeedState state, IslandSocialNeedConfig need)
    {
        if (state == null || need == null || state.initialized) return;

        state.currentValue = need.maxValue;
        state.initialized = true;
    }

    private static int RestoreNeedFromStorage(WorldConfigDatabase config, IslandProductionState islandState, IslandSocietyNeedState state, IslandSocialNeedConfig need, string islandId, float deltaHours)
    {
        string recoveryItemId = ResolveRecoveryItemId(need, islandId);
        if (config == null || islandState == null || state == null || need == null || string.IsNullOrWhiteSpace(recoveryItemId)) return 0;

        float deficit = Mathf.Max(0f, need.maxValue - state.currentValue);
        if (deficit <= 0.001f) return 0;

        float recoveryPerHour = CalculateNeedRecoveryPointsPerHour(config, islandState, islandId, need);
        state.recoveryCapacityProgress += Mathf.Max(0f, recoveryPerHour) * Mathf.Max(0f, deltaHours);
        if (state.recoveryCapacityProgress <= 0.001f && state.recoveryItemProgress <= 0.001f) return 0;

        int spentItems = 0;
        int guard = 0;
        while (deficit > 0.001f && state.recoveryCapacityProgress > 0.001f && guard < 1000)
        {
            guard++;
            if (state.recoveryItemProgress <= 0.001f)
            {
                if (!islandState.TrySpendResource(recoveryItemId, 1)) break;

                state.recoveryItemProgress += Mathf.Max(1f, need.restorePerItem);
                spentItems++;
            }

            float restored = Mathf.Min(deficit, state.recoveryCapacityProgress, state.recoveryItemProgress);
            if (restored <= 0.001f) break;

            state.currentValue = Mathf.Min(need.maxValue, state.currentValue + restored);
            state.recoveryCapacityProgress = Mathf.Max(0f, state.recoveryCapacityProgress - restored);
            state.recoveryItemProgress = Mathf.Max(0f, state.recoveryItemProgress - restored);
            deficit = Mathf.Max(0f, need.maxValue - state.currentValue);
        }

        return spentItems;
    }

    private static float CalculateNeedRecoveryPointsPerHour(WorldConfigDatabase config, IslandProductionState islandState, string islandId, IslandSocialNeedConfig need)
    {
        if (config == null || need == null) return 0f;

        float recovery = GetBaseNeedRecoveryPointsPerHour(need);
        if (islandState == null || islandState.development == null)
        {
            return recovery;
        }

        if (IslandDevelopmentSimulator.UsesExplicitBuildingRuntime(islandState))
        {
            IslandDevelopmentState development = islandState.development;
            for (int i = 0; i < development.buildings.Count; i++)
            {
                IslandBuildingState buildingState = development.buildings[i];
                if (buildingState == null || !buildingState.built) continue;

                IslandBuildingConfig building = config.GetIslandBuilding(buildingState.buildingId);
                if (IsServiceBuildingForNeed(building, need))
                {
                    recovery += CalculateServiceBuildingRecoveryPointsPerHour(need, buildingState);
                }
            }

            return recovery;
        }

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null || industry.islandId != islandId || string.IsNullOrWhiteSpace(industry.buildingId)) continue;

            IslandBuildingConfig building = config.GetIslandBuilding(industry.buildingId);
            if (IsServiceBuildingForNeed(building, need))
            {
                recovery += CalculateServiceBuildingRecoveryPointsPerHour(need, null);
            }
        }

        return recovery;
    }

    public static float GetBaseNeedRecoveryPointsPerHour(IslandSocialNeedConfig need)
    {
        if (need == null) return 0f;
        return Mathf.Max(0.1f, Mathf.Max(1f, need.restorePerItem) * 0.2f);
    }

    private static float CalculateServiceBuildingRecoveryPointsPerHour(IslandSocialNeedConfig need, IslandBuildingState buildingState)
    {
        if (need == null) return 0f;

        int level = buildingState != null ? Mathf.Max(1, buildingState.level) : 1;
        int modernization = buildingState != null ? Mathf.Clamp(buildingState.modernizationLevel, 0, 5) : 0;
        float serviceFactor = 2f + level * 1.5f + modernization * 0.75f;
        return Mathf.Max(1f, need.restorePerItem) * serviceFactor;
    }

    private static bool IsServiceBuildingForNeed(IslandBuildingConfig building, IslandSocialNeedConfig need)
    {
        if (building == null || need == null || string.IsNullOrWhiteSpace(building.serviceRole)) return false;
        if (building.serviceRole == need.id) return true;

        switch (need.kind)
        {
            case IslandNeedKind.Workforce:
                return building.serviceRole == "need_workforce";
            case IslandNeedKind.Health:
                return building.serviceRole == "need_health";
            case IslandNeedKind.Safety:
                return building.serviceRole == "need_safety";
            case IslandNeedKind.Comfort:
                return building.serviceRole == "need_comfort";
            case IslandNeedKind.Creativity:
                return building.serviceRole == "need_creativity";
            case IslandNeedKind.Repair:
                return building.serviceRole == "need_repair" || building.serviceRole == "repair";
            case IslandNeedKind.CapitalConnection:
                return building.serviceRole == "need_capital_connection" || building.serviceRole == "passenger";
            default:
                return false;
        }
    }

    private static void BuildIslandNeedLoad(WorldConfigDatabase config, IslandProductionState islandState, string islandId, Dictionary<IslandNeedKind, int> result)
    {
        result.Clear();
        if (config == null || string.IsNullOrWhiteSpace(islandId)) return;

        if (IslandDevelopmentSimulator.UsesExplicitBuildingRuntime(islandState))
        {
            IslandDevelopmentState development = islandState.development;
            for (int i = 0; i < development.buildings.Count; i++)
            {
                IslandBuildingState buildingState = development.buildings[i];
                if (buildingState == null || !buildingState.built) continue;

                IslandBuildingConfig building = config.GetIslandBuilding(buildingState.buildingId);
                if (building == null || building.TotalNeedLoad <= 0) continue;

                AddBuildingNeedLoads(config, result, building);
            }

            AddIslandScaleNeedLoads(islandState, islandId, result);
            return;
        }

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null || industry.islandId != islandId || string.IsNullOrWhiteSpace(industry.buildingId)) continue;

            IslandBuildingConfig building = config.GetIslandBuilding(industry.buildingId);
            if (building == null || building.TotalNeedLoad <= 0) continue;

            AddBuildingNeedLoads(config, result, building);
        }

        AddIslandScaleNeedLoads(islandState, islandId, result);
    }

    private static void AddBuildingNeedLoads(WorldConfigDatabase config, Dictionary<IslandNeedKind, int> loads, IslandBuildingConfig building)
    {
        if (config == null || building == null || loads == null) return;

        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null) continue;
            AddLoad(loads, need.kind, building.GetNeedLoad(need.kind));
        }
    }

    private static void AddIslandScaleNeedLoads(IslandProductionState islandState, string islandId, Dictionary<IslandNeedKind, int> loads)
    {
        if (islandState == null || loads == null || IsCapitalIsland(islandId) || !IsSettledIsland(islandState)) return;

        int stageLoad = Mathf.Clamp(1 + islandState.development.completedStage, 1, 5);
        AddLoad(loads, IslandNeedKind.CapitalConnection, stageLoad);
    }

    private static void AddLoad(Dictionary<IslandNeedKind, int> loads, IslandNeedKind kind, int value)
    {
        if (value <= 0) return;

        loads.TryGetValue(kind, out int current);
        loads[kind] = current + value;
    }

    private static string ResolveRecoveryItemId(IslandSocialNeedConfig need, string islandId)
    {
        if (need == null) return "";
        if (need.kind == IslandNeedKind.CapitalConnection)
        {
            return IsCapitalIsland(islandId) ? "" : PassengerCargoIds.ToIslandItemId(islandId);
        }

        return need.recoveryItemId ?? "";
    }

    private static bool ShouldSkipNeedForIsland(IslandSocialNeedConfig need, string islandId, IslandProductionState islandState)
    {
        return need != null &&
            need.kind == IslandNeedKind.CapitalConnection &&
            (IsCapitalIsland(islandId) || !IsSettledIsland(islandState));
    }

    private static bool IsCapitalIsland(string islandId)
    {
        return string.Equals(islandId, CapitalIslandId, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSettledIsland(IslandProductionState islandState)
    {
        if (islandState == null || islandState.development == null) return false;
        if (islandState.development.completedStage > 0 || islandState.development.socialNeedsUnlocked) return true;

        List<IslandBuildingState> buildings = islandState.development.buildings;
        if (buildings == null) return false;
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null && buildings[i].built) return true;
        }

        return false;
    }
}

public static class PassengerCargoIds
{
    public const int PassengerMassKg = 100;
    public const string ToCapitalItemId = "passengers_to_capital";
    public const string ToIslandTemplateItemId = "passengers_to_island";
    public const string ToShipTemplateItemId = "passengers_to_ship";

    public static string ToIslandItemId(string islandId)
    {
        return "passengers_to_" + SanitizeDestinationId(islandId);
    }

    public static string ToShipItemId(string shipId)
    {
        return "passengers_to_ship_" + SanitizeDestinationId(shipId);
    }

    private static string SanitizeDestinationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.Length > 0 ? builder.ToString() : "unknown";
    }
}

public static class PassengerTrafficSimulator
{
    private const string DefaultCapitalIslandId = "capital";

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, float deltaMinutes, string capitalIslandId = DefaultCapitalIslandId)
    {
        if (config == null || !config.isLoaded || progress == null || deltaMinutes <= 0f) return 0;
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return 0;

        string capitalId = string.IsNullOrWhiteSpace(capitalIslandId) ? DefaultCapitalIslandId : capitalIslandId;
        IslandProductionState capitalStorage = progress.GetIslandProductionState(capitalId, true);
        int changed = AbsorbCapitalPassengers(capitalStorage);
        float deltaHours = Mathf.Max(0f, deltaMinutes / 60f);

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;
            if (string.Equals(island.id, capitalId, System.StringComparison.OrdinalIgnoreCase)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(island.id, true);
            if (!IsSettledIsland(islandState)) continue;

            float size = CalculateIslandTrafficSize(islandState);
            float toCapitalPassengers = (0.25f + 0.35f * size) * deltaHours;
            float fromCapitalPassengers = (0.2f + 0.3f * size) * deltaHours;

            changed += GeneratePassengerCargo(islandState, PassengerCargoIds.ToCapitalItemId, ref islandState.passengersToCapitalProgress, toCapitalPassengers);
            changed += GeneratePassengerCargo(capitalStorage, PassengerCargoIds.ToIslandItemId(island.id), ref islandState.passengersFromCapitalProgress, fromCapitalPassengers);
        }

        changed += AbsorbCapitalPassengers(capitalStorage);
        return changed;
    }

    public static int AbsorbCapitalPassengers(IslandProductionState capitalStorage)
    {
        if (capitalStorage == null) return 0;

        int amount = capitalStorage.GetResourceAmount(PassengerCargoIds.ToCapitalItemId);
        if (amount <= 0) return 0;

        return capitalStorage.TrySpendResource(PassengerCargoIds.ToCapitalItemId, amount) ? amount : 0;
    }

    private static int GeneratePassengerCargo(IslandProductionState storage, string itemId, ref float passengerProgress, float passengerCount)
    {
        if (storage == null || string.IsNullOrWhiteSpace(itemId) || passengerCount <= 0f) return 0;

        passengerProgress = Mathf.Max(0f, passengerProgress + passengerCount);
        int wholePassengers = Mathf.FloorToInt(passengerProgress);
        if (wholePassengers <= 0) return 0;

        passengerProgress -= wholePassengers;
        storage.AddResource(itemId, wholePassengers);
        return wholePassengers;
    }

    private static float CalculateIslandTrafficSize(IslandProductionState islandState)
    {
        if (islandState == null || islandState.development == null) return 1f;

        int builtBuildings = 0;
        List<IslandBuildingState> buildings = islandState.development.buildings;
        if (buildings != null)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null && buildings[i].built) builtBuildings++;
            }
        }

        return Mathf.Clamp(1f + islandState.development.completedStage * 0.5f + builtBuildings * 0.15f, 1f, 8f);
    }

    private static bool IsSettledIsland(IslandProductionState islandState)
    {
        if (islandState == null || islandState.development == null) return false;
        if (islandState.development.completedStage > 0 || islandState.development.socialNeedsUnlocked) return true;

        List<IslandBuildingState> buildings = islandState.development.buildings;
        if (buildings == null) return false;
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null && buildings[i].built) return true;
        }

        return false;
    }
}
