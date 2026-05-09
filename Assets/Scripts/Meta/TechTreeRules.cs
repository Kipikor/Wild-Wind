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
            reason = "Node is missing.";
            return false;
        }

        if (progress == null)
        {
            reason = "Progress is missing.";
            return false;
        }

        if (node.isPremium)
        {
            reason = "Premium nodes do not need research.";
            return false;
        }

        if (progress.IsNodeResearched(node.nodeId))
        {
            reason = "Node is already researched.";
            return false;
        }

        if (!HasAccess(node, progress))
        {
            reason = "No prerequisite path is researched.";
            return false;
        }

        if (node.researchCostXp <= 0)
        {
            return true;
        }

        if (!TryFindExperienceShip(node, progress, node.researchCostXp, out experienceShipId))
        {
            reason = "Not enough experience on an allowed ship.";
            return false;
        }

        return true;
    }

    public static bool CanPurchase(TechTreeNode node, PlayerProgress progress, out string reason)
    {
        reason = "";

        if (node == null)
        {
            reason = "Node is missing.";
            return false;
        }

        if (progress == null)
        {
            reason = "Progress is missing.";
            return false;
        }

        if (progress.IsNodePurchased(node.nodeId))
        {
            reason = "Node is already purchased.";
            return false;
        }

        bool researchSatisfied = node.isPremium || progress.IsNodeResearched(node.nodeId) || node.startsResearched;
        if (!researchSatisfied)
        {
            reason = "Node is not researched.";
            return false;
        }

        if (!HasAccess(node, progress) && !node.isPremium)
        {
            reason = "No prerequisite path is researched.";
            return false;
        }

        if (progress.money < node.purchasePrice)
        {
            reason = "Not enough money.";
            return false;
        }

        return true;
    }

    private static bool TryFindExperienceShip(TechTreeNode node, PlayerProgress progress, int requiredXp, out string shipId)
    {
        shipId = "";
        List<string> sourceIds = node.experienceShipIds;

        for (int i = 0; i < sourceIds.Count; i++)
        {
            string sourceShipId = sourceIds[i];
            if (!string.IsNullOrWhiteSpace(sourceShipId) && progress.GetShipExperience(sourceShipId) >= requiredXp)
            {
                shipId = sourceShipId;
                return true;
            }
        }

        string fallbackShipId = node.kind == TechTreeNodeKind.Module ? node.parentShipId : node.EffectiveShipId;
        if (!string.IsNullOrWhiteSpace(fallbackShipId) && progress.GetShipExperience(fallbackShipId) >= requiredXp)
        {
            shipId = fallbackShipId;
            return true;
        }

        return false;
    }
}
