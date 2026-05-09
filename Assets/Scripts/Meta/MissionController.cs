using UnityEngine;

public class MissionController : MonoBehaviour
{
    [Tooltip("Описание миссии: маршрут, награды, стыковка назначения и настройки выполнения в реальном времени.")]
    public MissionDefinitionSO mission;
    [Tooltip("Корабль игрока, который будет поставлен на старт и проверяться на прибытие.")]
    public ShipPhysics targetShip;
    [Tooltip("Состояние мета-игры. Через него миссия переводит игру в вылет и завершает полет стыковкой.")]
    public MetaGameState metaGameState;
    [Tooltip("Точка старта в сцене. Если не задана, используется стартовая позиция из описания миссии.")]
    public Transform startPoint;
    [Tooltip("Точка назначения в сцене. Если не задана, используется позиция назначения из описания миссии.")]
    public Transform destinationPoint;
    [Tooltip("Если включено, миссия стартует автоматически при запуске сцены.")]
    public bool startMissionOnPlay = true;
    [Tooltip("Если включено, при старте миссии корабль переносится в стартовую точку.")]
    public bool placeShipAtStart = true;

    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }
    public float DistanceToDestination { get; private set; }

    private void Reset()
    {
        targetShip = FindFirstObjectByType<ShipPhysics>();
        metaGameState = FindFirstObjectByType<MetaGameState>();
    }

    private void Awake()
    {
        if (targetShip == null)
        {
            targetShip = FindFirstObjectByType<ShipPhysics>();
        }

        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }
    }

    private void Start()
    {
        if (!startMissionOnPlay) return;

        if (metaGameState == null || metaGameState.CurrentMode == GameSessionMode.Flight)
        {
            BeginMission();
        }
    }

    private void Update()
    {
        if (!IsActive || IsCompleted || targetShip == null || mission == null) return;

        DistanceToDestination = Vector3.Distance(targetShip.transform.position, GetDestinationPosition());
        if (DistanceToDestination <= mission.arrivalRadius)
        {
            CompleteMission();
        }
    }

    [ContextMenu("Начать миссию")]
    public void BeginMission()
    {
        if (mission == null || targetShip == null) return;

        if (metaGameState != null && !metaGameState.TryBeginFlightSession(mission))
        {
            return;
        }

        IsCompleted = false;
        IsActive = true;

        if (placeShipAtStart)
        {
            PlaceShipAtStart();
        }

        DistanceToDestination = Vector3.Distance(targetShip.transform.position, GetDestinationPosition());
    }

    [ContextMenu("Завершить миссию")]
    public void CompleteMission()
    {
        if (!IsActive || IsCompleted || mission == null) return;

        IsCompleted = true;
        IsActive = false;

        if (metaGameState != null)
        {
            metaGameState.CompleteFlightMission(mission);
        }
    }

    public void CancelMission()
    {
        IsActive = false;
        IsCompleted = false;
        DistanceToDestination = 0f;
    }

    public Vector3 GetStartPosition()
    {
        return startPoint != null ? startPoint.position : mission != null ? mission.startPosition : Vector3.zero;
    }

    public Vector3 GetDestinationPosition()
    {
        return destinationPoint != null ? destinationPoint.position : mission != null ? mission.destinationPosition : Vector3.zero;
    }

    private void PlaceShipAtStart()
    {
        Transform shipTransform = targetShip.transform;
        shipTransform.position = GetStartPosition();

        Rigidbody shipRigidbody = targetShip.GetComponent<Rigidbody>();
        if (shipRigidbody == null) return;

        shipRigidbody.isKinematic = false;
        shipRigidbody.useGravity = true;
        shipRigidbody.linearVelocity = Vector3.zero;
        shipRigidbody.angularVelocity = Vector3.zero;
        shipRigidbody.WakeUp();
    }

    private void OnDrawGizmosSelected()
    {
        if (mission == null) return;

        Vector3 start = GetStartPosition();
        Vector3 destination = GetDestinationPosition();

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(start, 3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(destination, mission.arrivalRadius);
        Gizmos.DrawLine(start, destination);
    }
}
