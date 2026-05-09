using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShipCrashDetector : MonoBehaviour
{
    public MetaGameState metaGameState;
    public float crashRelativeSpeed = 14f;
    public float crashBelowAltitude = -10f;
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
            ReportCrash("below safe altitude");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (crashReported || !IsFlight()) return;
        if (collision.relativeVelocity.magnitude < crashRelativeSpeed) return;

        ReportCrash($"impact at {collision.relativeVelocity.magnitude:0.0} m/s");
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
        Debug.LogWarning("Flight crash: " + reason);

        if (metaGameState != null)
        {
            metaGameState.DockAt("crash_recovery", DockingLocationKind.Island);
        }
    }
}
