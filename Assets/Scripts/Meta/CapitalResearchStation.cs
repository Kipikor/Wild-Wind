using UnityEngine;

public class CapitalResearchStation : MonoBehaviour
{
    [InspectorName("Мета-игра")]
    public MetaGameState metaGameState;

    [InspectorName("Остров")]
    public string islandId = "capital";

    [InspectorName("Название")]
    public string displayName = "Столица";

    public MetaGameState Meta
    {
        get
        {
            if (metaGameState == null)
            {
                metaGameState = FindFirstObjectByType<MetaGameState>();
            }

            return metaGameState;
        }
    }

    public bool IsPlayerDockedHere
    {
        get
        {
            MetaGameState meta = Meta;
            return meta != null
                && meta.IsDocked
                && meta.progress != null
                && meta.progress.currentDockKind == DockingLocationKind.Island
                && meta.progress.currentDockId == islandId;
        }
    }
}
