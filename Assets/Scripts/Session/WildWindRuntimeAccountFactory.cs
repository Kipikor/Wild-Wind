using UnityEngine;

public static class WildWindRuntimeAccountFactory
{
    public static MetaGameAccountData BuildNewAccountData()
    {
        PlayerProgress progress = new PlayerProgress();
        progress.Normalize();
        progress.selectedHullId = GameplaySessionAccountData.DefaultStarterHullId;
        progress.SetDocked(
            GameplaySessionAccountData.DefaultDockId,
            GameplaySessionAccountData.ResolveStarterDockPosition(GameplaySessionAccountData.DefaultDockId));

        return new MetaGameAccountData
        {
            version = MetaGameAccountData.CurrentVersion,
            progress = progress,
            gameplaySession = GameplaySessionAccountData.CreateInitial(GameplaySessionAccountData.DefaultAccountId, progress)
        };
    }
}
