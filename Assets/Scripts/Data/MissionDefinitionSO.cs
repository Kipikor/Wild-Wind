using UnityEngine;

[CreateAssetMenu(fileName = "НоваяМиссия", menuName = "Wild Wind/Миссии/Описание миссии")]
public class MissionDefinitionSO : ScriptableObject
{
    [Header("Основное")]
    [InspectorName("Идентификатор миссии")]
    [Tooltip("Технический идентификатор миссии. Используется в сохранениях, поэтому после релиза миссии его лучше не менять.")]
    public string missionId = "first_delivery";
    [InspectorName("Название")]
    public string displayName = "Первая доставка";
    [InspectorName("Описание")]
    [TextArea] public string description = "";

    [Header("Маршрут")]
    [InspectorName("Стартовая точка")]
    public Vector3 startPosition = Vector3.zero;
    [InspectorName("Точка назначения")]
    public Vector3 destinationPosition = new Vector3(0f, 60f, 300f);
    [InspectorName("Радиус прибытия")]
    [Tooltip("Расстояние до точки назначения, на котором миссия считается доставленной.")]
    public float arrivalRadius = 10f;

    [Header("Стыковка")]
    [InspectorName("Идентификатор дока назначения")]
    [Tooltip("Идентификатор стыковки, куда игрок попадает после успешного завершения миссии.")]
    public string destinationDockId = "destination_island";
    [InspectorName("Тип дока назначения")]
    public DockingLocationKind destinationDockKind = DockingLocationKind.Island;

    [Header("Награда")]
    [InspectorName("Деньги")]
    public int rewardMoney = 100;
    [InspectorName("Опыт корабля")]
    public int rewardExperience = 50;

    [Header("Реальное время")]
    [InspectorName("Можно выполнять без вылета")]
    [Tooltip("Если включено, миссию можно отправить как процесс в реальном времени из интерфейса стыковки.")]
    public bool canRunAsTimedMission = true;
    [InspectorName("Длительность, сек")]
    [Tooltip("Сколько реальных секунд длится выполнение миссии без вылета.")]
    public int realTimeDurationSeconds = 300;
}
