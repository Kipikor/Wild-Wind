using System;
using System.Collections.Generic;
using UnityEngine;

public static class IslandDevelopmentSimulator
{
    private const int ProjectStepCount = 3;
    private const int MaxProjectStepsPerAdvance = 512;
    private const float UnlockedStageProductionShare = 0.25f;

    public static void EnsureIslandStates(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || !config.isLoaded || progress == null) return;

        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(island.id, true);
            islandState.development ??= new IslandDevelopmentState();
            if (string.IsNullOrWhiteSpace(islandState.development.archetypeId))
            {
                islandState.development.archetypeId = island.archetypeId ?? "";
            }

            islandState.development.Normalize();
        }
    }

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || !config.isLoaded || progress == null || toUtcTicks <= fromUtcTicks) return 0;

        EnsureIslandStates(config, progress);

        int changed = 0;
        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null || string.IsNullOrWhiteSpace(island.id)) continue;

            IslandProductionState islandState = progress.GetIslandProductionState(island.id, true);
            changed += AdvanceProject(config, islandState, fromUtcTicks, toUtcTicks);
        }

        return changed;
    }

    public static bool TryCompleteNextStage(WorldConfigDatabase config, PlayerProgress progress, string islandId, out string message)
    {
        message = "";
        if (config == null || progress == null || string.IsNullOrWhiteSpace(islandId))
        {
            message = "No island or config.";
            return false;
        }

        IslandConfig island = config.GetIsland(islandId);
        IslandProductionState islandState = progress.GetIslandProductionState(islandId, true);
        if (island == null || islandState == null)
        {
            message = "Island not found.";
            return false;
        }

        EnsureIslandStates(config, progress);

        IslandArchetypeStageConfig nextStage = FindStage(config, islandState.development.archetypeId, islandState.development.completedStage + 1);
        if (nextStage == null)
        {
            message = "No next island stage.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(nextStage.triggerNeedItemId) &&
            !islandState.TrySpendResource(nextStage.triggerNeedItemId, 1))
        {
            message = "Need resource for island stage: " + nextStage.triggerNeedItemId + ".";
            return false;
        }

        islandState.development.completedStage = nextStage.stageIndex;
        if (nextStage.opensSocialNeeds)
        {
            islandState.development.socialNeedsUnlocked = true;
        }

        message = "Island stage completed: " + nextStage.id + ".";
        return true;
    }

    public static bool TryStartConstruction(WorldConfigDatabase config, PlayerProgress progress, string islandId, string buildingId, long utcTicks, out string message)
    {
        return TryStartProject(config, progress, islandId, buildingId, IslandConstructionProjectKind.Build, 1, 1f, utcTicks, out message);
    }

    public static bool TryStartUpgrade(WorldConfigDatabase config, PlayerProgress progress, string islandId, string buildingId, long utcTicks, out string message)
    {
        message = "";
        IslandProductionState islandState = progress != null ? progress.GetIslandProductionState(islandId, true) : null;
        IslandBuildingState buildingState = islandState != null ? islandState.GetBuildingState(buildingId, false) : null;
        IslandBuildingConfig building = config != null ? config.GetIslandBuilding(buildingId) : null;
        if (buildingState == null || !buildingState.built || building == null)
        {
            message = "Building is not built.";
            return false;
        }

        int targetLevel = buildingState.level + 1;
        if (targetLevel > Mathf.Max(1, building.maxUpgradeLevel))
        {
            message = "Building is already at max upgrade level.";
            return false;
        }

        return TryStartProject(config, progress, islandId, buildingId, IslandConstructionProjectKind.Upgrade, targetLevel, targetLevel, utcTicks, out message);
    }

    public static bool TryStartModernization(WorldConfigDatabase config, PlayerProgress progress, string islandId, string buildingId, long utcTicks, out string message)
    {
        message = "";
        IslandProductionState islandState = progress != null ? progress.GetIslandProductionState(islandId, true) : null;
        IslandBuildingState buildingState = islandState != null ? islandState.GetBuildingState(buildingId, false) : null;
        if (buildingState == null || !buildingState.built)
        {
            message = "Building is not built.";
            return false;
        }

        int targetLevel = buildingState.modernizationLevel + 1;
        if (targetLevel > 5)
        {
            message = "Building is already at max modernization level.";
            return false;
        }

        return TryStartProject(config, progress, islandId, buildingId, IslandConstructionProjectKind.Modernization, targetLevel, targetLevel * 2f, utcTicks, out message);
    }

    public static bool UsesExplicitBuildingRuntime(IslandProductionState islandState)
    {
        return islandState != null && islandState.development != null && islandState.development.HasAnyBuiltBuildings();
    }

    public static bool IsBuildingBuilt(IslandProductionState islandState, string buildingId)
    {
        if (string.IsNullOrWhiteSpace(buildingId)) return true;
        return !UsesExplicitBuildingRuntime(islandState) || islandState.development.IsBuildingBuilt(buildingId);
    }

    public static float GetBaseProductionMultiplier(WorldConfigDatabase config, IslandProductionState islandState)
    {
        if (config == null || islandState == null || islandState.development == null) return 1f;

        float multiplier = 1f;
        for (int i = 0; i < config.islandArchetypeStages.Count; i++)
        {
            IslandArchetypeStageConfig stage = config.islandArchetypeStages[i];
            if (stage == null ||
                stage.archetypeId != islandState.development.archetypeId ||
                stage.stageIndex > islandState.development.completedStage)
            {
                continue;
            }

            multiplier *= Mathf.Max(1f, stage.productionMultiplier);
        }

        return multiplier;
    }

    public static int AddUnlockedStageProduction(WorldConfigDatabase config, IslandProductionState islandState, int baseUnits)
    {
        if (config == null || islandState == null || islandState.development == null || baseUnits <= 0) return 0;

        int added = 0;
        int sideUnits = Mathf.Max(1, Mathf.FloorToInt(baseUnits * UnlockedStageProductionShare));
        for (int i = 0; i < config.islandArchetypeStages.Count; i++)
        {
            IslandArchetypeStageConfig stage = config.islandArchetypeStages[i];
            if (stage == null ||
                stage.archetypeId != islandState.development.archetypeId ||
                stage.stageIndex > islandState.development.completedStage ||
                string.IsNullOrWhiteSpace(stage.unlockedProductionItemId))
            {
                continue;
            }

            added += islandState.AddResource(stage.unlockedProductionItemId, sideUnits);
        }

        return added;
    }

    private static bool TryStartProject(
        WorldConfigDatabase config,
        PlayerProgress progress,
        string islandId,
        string buildingId,
        IslandConstructionProjectKind kind,
        int targetLevel,
        float costMultiplier,
        long utcTicks,
        out string message)
    {
        message = "";
        if (config == null || progress == null || string.IsNullOrWhiteSpace(islandId) || string.IsNullOrWhiteSpace(buildingId))
        {
            message = "No island or building.";
            return false;
        }

        IslandConfig island = config.GetIsland(islandId);
        IslandBuildingConfig building = config.GetIslandBuilding(buildingId);
        if (island == null || building == null)
        {
            message = "Island or building config not found.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(building.requiredTechnologyId) && !progress.IsTechnologyCompleted(building.requiredTechnologyId))
        {
            message = "Required technology is not completed: " + building.requiredTechnologyId + ".";
            return false;
        }

        EnsureIslandStates(config, progress);

        IslandProductionState islandState = progress.GetIslandProductionState(islandId, true);
        IslandDevelopmentState development = islandState.development;
        if (development.constructionProject.active)
        {
            message = "Another island project is active.";
            return false;
        }

        if (development.constructionRecoveryUntilUtcTicks > utcTicks)
        {
            message = "Island is recovering after construction.";
            return false;
        }

        IslandBuildingState buildingState = development.GetBuildingState(buildingId, false);
        if (kind == IslandConstructionProjectKind.Build && buildingState != null && buildingState.built)
        {
            message = "Duplicate buildings are not allowed.";
            return false;
        }

        if (!TrySpendScaledItems(islandState, building.constructionInputs, Mathf.Max(1f, costMultiplier)))
        {
            message = "Not enough construction resources.";
            return false;
        }

        IslandConstructionProjectState project = development.constructionProject;
        project.active = true;
        project.kind = kind;
        project.buildingId = buildingId;
        project.targetLevel = Mathf.Max(1, targetLevel);
        project.activeStepIndex = 0;
        project.lastMessage = "Project started: " + buildingId + ".";
        StartProjectStep(config, project, utcTicks);

        return true;
    }

    private static int AdvanceProject(WorldConfigDatabase config, IslandProductionState islandState, long fromTicks, long toTicks)
    {
        if (config == null || islandState == null || islandState.development == null) return 0;

        IslandConstructionProjectState project = islandState.development.constructionProject;
        if (project == null || !project.active) return 0;

        if (project.nextCompletionUtcTicks <= 0)
        {
            StartProjectStep(config, project, fromTicks);
        }

        int changed = 0;
        int guard = 0;
        while (project.active && project.nextCompletionUtcTicks <= toTicks && guard < MaxProjectStepsPerAdvance)
        {
            guard++;
            long completedTicks = project.nextCompletionUtcTicks;
            project.activeStepIndex++;
            changed++;

            if (project.activeStepIndex >= ProjectStepCount)
            {
                CompleteProject(config, islandState, completedTicks);
                break;
            }

            StartProjectStep(config, project, completedTicks);
        }

        return changed;
    }

    private static void CompleteProject(WorldConfigDatabase config, IslandProductionState islandState, long completedTicks)
    {
        IslandDevelopmentState development = islandState.development;
        IslandConstructionProjectState project = development.constructionProject;
        IslandBuildingConfig building = config.GetIslandBuilding(project.buildingId);
        IslandBuildingState buildingState = development.GetBuildingState(project.buildingId, true);

        switch (project.kind)
        {
            case IslandConstructionProjectKind.Build:
                buildingState.built = true;
                buildingState.level = Mathf.Max(1, project.targetLevel);
                break;
            case IslandConstructionProjectKind.Upgrade:
                buildingState.built = true;
                buildingState.level = Mathf.Max(buildingState.level, project.targetLevel);
                break;
            case IslandConstructionProjectKind.Modernization:
                buildingState.built = true;
                buildingState.modernizationLevel = Mathf.Max(buildingState.modernizationLevel, project.targetLevel);
                break;
        }

        project.lastMessage = "Project completed: " + project.buildingId + ".";
        project.active = false;
        project.Normalize();

        float recoveryHours = building != null ? Mathf.Max(0f, building.constructionRecoveryHours) : 0f;
        development.constructionRecoveryUntilUtcTicks = completedTicks + TimeSpan.FromHours(recoveryHours).Ticks;
    }

    private static void StartProjectStep(WorldConfigDatabase config, IslandConstructionProjectState project, long startTicks)
    {
        IslandBuildingConfig building = config.GetIslandBuilding(project.buildingId);
        long durationTicks = GetProjectStepDurationTicks(building, project.kind);
        project.cycleStartedUtcTicks = startTicks;
        project.nextCompletionUtcTicks = startTicks + durationTicks;
    }

    private static long GetProjectStepDurationTicks(IslandBuildingConfig building, IslandConstructionProjectKind kind)
    {
        float recoveryHours = building != null ? Mathf.Max(0.05f, building.constructionRecoveryHours) : 0.25f;
        float kindMultiplier = kind switch
        {
            IslandConstructionProjectKind.Upgrade => 0.75f,
            IslandConstructionProjectKind.Modernization => 1.25f,
            _ => 1f
        };

        float seconds = Mathf.Max(10f, recoveryHours * 3600f * kindMultiplier / ProjectStepCount);
        return TimeSpan.FromSeconds(seconds).Ticks;
    }

    private static IslandArchetypeStageConfig FindStage(WorldConfigDatabase config, string archetypeId, int stageIndex)
    {
        if (config == null || string.IsNullOrWhiteSpace(archetypeId) || stageIndex <= 0) return null;

        for (int i = 0; i < config.islandArchetypeStages.Count; i++)
        {
            IslandArchetypeStageConfig stage = config.islandArchetypeStages[i];
            if (stage != null && stage.archetypeId == archetypeId && stage.stageIndex == stageIndex)
            {
                return stage;
            }
        }

        return null;
    }

    private static bool TrySpendScaledItems(IslandProductionState islandState, List<ProductionItemAmountConfig> items, float multiplier)
    {
        if (islandState == null || !HasItems(islandState, items, multiplier)) return false;

        if (items == null) return true;
        for (int i = 0; i < items.Count; i++)
        {
            ProductionItemAmountConfig item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0f) continue;
            islandState.TrySpendResource(item.itemId, Mathf.Max(1, Mathf.CeilToInt(item.amount * multiplier)));
        }

        return true;
    }

    private static bool HasItems(IslandProductionState islandState, List<ProductionItemAmountConfig> items, float multiplier)
    {
        if (islandState == null) return false;
        if (items == null) return true;

        for (int i = 0; i < items.Count; i++)
        {
            ProductionItemAmountConfig item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.amount <= 0f) continue;

            int needed = Mathf.Max(1, Mathf.CeilToInt(item.amount * Mathf.Max(1f, multiplier)));
            if (islandState.GetResourceAmount(item.itemId) < needed)
            {
                return false;
            }
        }

        return true;
    }
}
