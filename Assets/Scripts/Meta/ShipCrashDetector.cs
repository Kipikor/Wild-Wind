using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipCrashDetector : MonoBehaviour
{
    [InspectorName("Состояние меты")]
    public MetaGameState metaGameState;
    [InspectorName("Скорость удара для крушения")]
    [Tooltip("Если относительная скорость столкновения выше этого значения, полет считается потерянным.")]
    public float crashRelativeSpeed = 14f;
    [InspectorName("Высота крушения")]
    [Tooltip("Если корабль опустится ниже этой высоты в полете, корабль теряется, а игрок возвращается в город на стартовом корабле.")]
    public float crashBelowAltitude = -10f;
    [InspectorName("Крушение ниже высоты")]
    public bool crashWhenBelowAltitude = true;

    private bool crashReported;

    private void Reset()
    {
        metaGameState = FindFirstObjectByType<MetaGameState>();
    }

    private void Awake()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }
    }

    private void Update()
    {
        if (crashReported || !crashWhenBelowAltitude || !IsFlight()) return;

        if (transform.position.y < crashBelowAltitude)
        {
            ReportCrash("ниже безопасной высоты");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (crashReported || !IsFlight()) return;
        if (collision.relativeVelocity.magnitude < crashRelativeSpeed) return;

        ReportCrash($"удар на скорости {collision.relativeVelocity.magnitude:0.0} м/с");
    }

    public void ResetCrashState()
    {
        crashReported = false;
    }

    private bool IsFlight()
    {
        return metaGameState != null && metaGameState.CurrentMode == GameSessionMode.Flight;
    }

    private void ReportCrash(string reason)
    {
        crashReported = true;
        Debug.LogWarning("Крушение в вылете: " + reason);

        if (metaGameState != null)
        {
            if (metaGameState.HasActiveSortie)
            {
                metaGameState.LoseActiveSortieShipAndReturnToBase(reason);
            }
            else
            {
                metaGameState.LoseShipAndReturnToCity(reason);
            }
        }
    }
}
