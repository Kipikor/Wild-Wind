using System.Collections.Generic;

public static class TechTreeRules
{
    public static bool HasAccess(TechTreeNode node, PlayerProgress progress)
    {
        if (node == null || progress == null) return false;
        if (node.prerequisiteNodeIds.Count == 0) return true;

        for (int i = 0; i < node.prerequisiteNodeIds.Count; i++)
        {
            string prerequisiteId = node.prerequisiteNodeIds[i];
            if (progress.IsNodeResearched(prerequisiteId) || progress.IsNodePurchased(prerequisiteId))
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanResearch(TechTreeNode node, PlayerProgress progress, out string experienceShipId, out string reason)
    {
        experienceShipId = "";
        reason = "";

        if (node == null)
        {
            reason = "Узел не найден.";
            return false;
        }

        if (progress == null)
        {
            reason = "Прогресс игрока не найден.";
            return false;
        }

        if (node.isPremium)
        {
            reason = "Премиум-узлы не требуют исследования.";
            return false;
        }

        if (progress.IsNodeResearched(node.nodeId))
        {
            reason = "Узел уже исследован.";
            return false;
        }

        if (!HasAccess(node, progress))
        {
            reason = "Не исследовано ни одно условие доступа.";
            return false;
        }

        if (node.researchCostXp <= 0)
        {
            return true;
        }

        if (!TryFindExperienceShip(node, progress, node.researchCostXp, out experienceShipId))
        {
            reason = "Недостаточно опыта на подходящем корабле или корпусе.";
            return false;
        }

        return true;
    }

    public static bool CanPurchase(TechTreeNode node, PlayerProgress progress, out string reason)
    {
        reason = "";

        if (node == null)
        {
            reason = "Узел не найден.";
            return false;
        }

        if (progress == null)
        {
            reason = "Прогресс игрока не найден.";
            return false;
        }

        if (!node.RequiresPurchase)
        {
            reason = "Фундаментальные исследования не покупаются за деньги.";
            return false;
        }

        if (progress.IsNodePurchased(node.nodeId))
        {
            reason = "Узел уже куплен.";
            return false;
        }

        bool researchSatisfied = node.isPremium || progress.IsNodeResearched(node.nodeId) || node.startsResearched;
        if (!researchSatisfied)
        {
            reason = "Узел еще не исследован.";
            return false;
        }

        if (!HasAccess(node, progress) && !node.isPremium)
        {
            reason = "Не исследовано ни одно условие доступа.";
            return false;
        }

        if (progress.money < node.purchasePrice)
        {
            reason = "Недостаточно денег.";
            return false;
        }

        return true;
    }

    private static bool TryFindExperienceShip(TechTreeNode node, PlayerProgress progress, int requiredXp, out string shipId)
    {
        shipId = "";
        List<string> sourceIds = node.experienceShipIds ?? new List<string>();

        for (int i = 0; i < sourceIds.Count; i++)
        {
            string sourceShipId = sourceIds[i];
            if (!string.IsNullOrWhiteSpace(sourceShipId) && progress.GetShipExperience(sourceShipId) >= requiredXp)
            {
                shipId = sourceShipId;
                return true;
            }
        }

        if (TryUseExperienceSource(progress.selectedHullId, progress, requiredXp, out shipId)) return true;
        if (TryUseExperienceSource(node.EffectivePartId, progress, requiredXp, out shipId)) return true;

        return false;
    }

    private static bool TryUseExperienceSource(string sourceId, PlayerProgress progress, int requiredXp, out string shipId)
    {
        shipId = "";
        if (!string.IsNullOrWhiteSpace(sourceId) && progress.GetShipExperience(sourceId) >= requiredXp)
        {
            shipId = sourceId;
            return true;
        }

        return false;
    }
}
