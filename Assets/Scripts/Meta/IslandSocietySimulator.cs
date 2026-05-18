using System.Collections.Generic;
using UnityEngine;

public static class IslandSocietySimulator
{
    private static readonly Dictionary<IslandNeedKind, int> LoadScratch = new Dictionary<IslandNeedKind, int>();

    public static void EnsureIslandStates(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || !config.isLoaded || progress == null) return;

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(island.id, true);
            EnsureNeedStates(config, islandState);
        }
    }

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, float deltaMinutes)
    {
        if (config == null || !config.isLoaded || progress == null || deltaMinutes <= 0f) return 0;

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

                IslandSocietyNeedState state = islandState.GetSocietyNeedState(need.id, true);
                InitializeNeedState(state, need);

                int load = LoadScratch.TryGetValue(need.kind, out int value) ? value : 0;
                float decay = (need.baseDecayPerHour + load * need.loadDecayPerHour) * deltaHours;
                if (decay > 0f)
                {
                    state.currentValue = Mathf.Max(0f, state.currentValue - decay);
                }

                changed += RestoreNeedFromStorage(islandState, state, need);
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

    private static void EnsureNeedStates(WorldConfigDatabase config, IslandProductionState islandState)
    {
        if (config == null || islandState == null) return;

        for (int i = 0; i < config.islandSocialNeeds.Count; i++)
        {
            IslandSocialNeedConfig need = config.islandSocialNeeds[i];
            if (need == null || string.IsNullOrWhiteSpace(need.id)) continue;

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

    private static int RestoreNeedFromStorage(IslandProductionState islandState, IslandSocietyNeedState state, IslandSocialNeedConfig need)
    {
        if (islandState == null || state == null || need == null || string.IsNullOrWhiteSpace(need.recoveryItemId)) return 0;

        float deficit = Mathf.Max(0f, need.maxValue - state.currentValue);
        if (deficit <= 0.001f) return 0;

        int neededItems = Mathf.Max(1, Mathf.CeilToInt(deficit / Mathf.Max(1f, need.restorePerItem)));
        int availableItems = islandState.GetResourceAmount(need.recoveryItemId);
        int spentItems = Mathf.Min(neededItems, availableItems);
        if (spentItems <= 0) return 0;

        if (!islandState.TrySpendResource(need.recoveryItemId, spentItems)) return 0;

        state.currentValue = Mathf.Min(need.maxValue, state.currentValue + spentItems * need.restorePerItem);
        return spentItems;
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

                AddLoad(result, IslandNeedKind.Workforce, building.workforceLoad);
                AddLoad(result, IslandNeedKind.Health, building.healthLoad);
                AddLoad(result, IslandNeedKind.Safety, building.safetyLoad);
                AddLoad(result, IslandNeedKind.Comfort, building.comfortLoad);
                AddLoad(result, IslandNeedKind.Creativity, building.creativityLoad);
            }

            return;
        }

        for (int i = 0; i < config.islandIndustries.Count; i++)
        {
            IslandIndustryConfig industry = config.islandIndustries[i];
            if (industry == null || industry.islandId != islandId || string.IsNullOrWhiteSpace(industry.buildingId)) continue;

            IslandBuildingConfig building = config.GetIslandBuilding(industry.buildingId);
            if (building == null || building.TotalNeedLoad <= 0) continue;

            AddLoad(result, IslandNeedKind.Workforce, building.workforceLoad);
            AddLoad(result, IslandNeedKind.Health, building.healthLoad);
            AddLoad(result, IslandNeedKind.Safety, building.safetyLoad);
            AddLoad(result, IslandNeedKind.Comfort, building.comfortLoad);
            AddLoad(result, IslandNeedKind.Creativity, building.creativityLoad);
        }
    }

    private static void AddLoad(Dictionary<IslandNeedKind, int> loads, IslandNeedKind kind, int value)
    {
        if (value <= 0) return;

        loads.TryGetValue(kind, out int current);
        loads[kind] = current + value;
    }
}
