using UnityEditor;
using UnityEngine;

public static class MissionSetupEditor
{
    private const string MissionPath = "Assets/Data/Missions/FirstDeliveryMission.asset";
    private const string MissionObjectName = "Mission";
    private const string StartPointName = "Mission Start";
    private const string DestinationPointName = "Mission Destination";

    [MenuItem("Wild Wind/Missions/Setup Test Mission")]
    public static void SetupTestMission()
    {
        MissionDefinitionSO mission = AssetDatabase.LoadAssetAtPath<MissionDefinitionSO>(MissionPath);
        if (mission == null)
        {
            EditorUtility.DisplayDialog("Mission Setup", $"Mission was not found at {MissionPath}.", "OK");
            return;
        }

        ShipPhysics ship = Object.FindFirstObjectByType<ShipPhysics>();
        if (ship == null)
        {
            EditorUtility.DisplayDialog("Mission Setup", "ShipPhysics was not found in the open scene.", "OK");
            return;
        }

        GameObject missionObject = GameObject.Find(MissionObjectName);
        if (missionObject == null)
        {
            missionObject = new GameObject(MissionObjectName);
            Undo.RegisterCreatedObjectUndo(missionObject, "Create Mission");
        }

        MissionController controller = missionObject.GetComponent<MissionController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<MissionController>(missionObject);
        }

        Transform startPoint = FindOrCreatePoint(StartPointName, mission.startPosition);
        Transform destinationPoint = FindOrCreatePoint(DestinationPointName, mission.destinationPosition);
        MetaGameState metaGameState = Object.FindFirstObjectByType<MetaGameState>();
        DockingPort destinationDock = destinationPoint.GetComponent<DockingPort>();
        if (destinationDock == null)
        {
            destinationDock = Undo.AddComponent<DockingPort>(destinationPoint.gameObject);
        }

        Undo.RecordObjects(new Object[] { controller, destinationDock }, "Setup Mission");
        controller.mission = mission;
        controller.targetShip = ship;
        controller.metaGameState = metaGameState;
        controller.startPoint = startPoint;
        controller.destinationPoint = destinationPoint;
        controller.startMissionOnPlay = true;
        controller.placeShipAtStart = true;

        destinationDock.metaGameState = metaGameState;
        destinationDock.targetShip = ship;
        destinationDock.dockId = string.IsNullOrWhiteSpace(mission.destinationDockId) ? "mission_destination" : mission.destinationDockId;
        destinationDock.displayName = string.IsNullOrWhiteSpace(mission.displayName) ? "Mission Destination" : mission.displayName;
        destinationDock.kind = mission.destinationDockKind;
        destinationDock.dockingRadius = Mathf.Max(mission.arrivalRadius, 1f);
        destinationDock.canEndSession = true;
        destinationDock.autoDockWhenInRange = true;
        destinationDock.snapPoint = destinationPoint;

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(destinationDock);
        Selection.activeGameObject = missionObject;
    }

    [MenuItem("Wild Wind/Missions/Setup Test Mission", true)]
    public static bool ValidateSetupTestMission()
    {
        return !Application.isPlaying;
    }

    private static Transform FindOrCreatePoint(string objectName, Vector3 position)
    {
        GameObject point = GameObject.Find(objectName);
        if (point == null)
        {
            point = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(point, $"Create {objectName}");
        }

        Undo.RecordObject(point.transform, $"Move {objectName}");
        point.transform.position = position;
        return point.transform;
    }
}
