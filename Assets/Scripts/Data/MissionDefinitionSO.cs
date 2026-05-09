using UnityEngine;

[CreateAssetMenu(fileName = "NewMissionDefinition", menuName = "Wild Wind/Missions/Mission Definition")]
public class MissionDefinitionSO : ScriptableObject
{
    [Header("Main")]
    public string missionId = "first_delivery";
    public string displayName = "First Delivery";
    [TextArea] public string description = "";

    [Header("Route")]
    public Vector3 startPosition = Vector3.zero;
    public Vector3 destinationPosition = new Vector3(0f, 60f, 300f);
    public float arrivalRadius = 10f;

    [Header("Docking")]
    public string destinationDockId = "destination_island";
    public DockingLocationKind destinationDockKind = DockingLocationKind.Island;

    [Header("Reward")]
    public int rewardMoney = 100;
    public int rewardExperience = 50;

    [Header("Real Time")]
    public bool canRunAsTimedMission = true;
    public int realTimeDurationSeconds = 300;
}
