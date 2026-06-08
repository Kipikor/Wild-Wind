using UnityEngine;

public sealed class ShipCrusherRotor : MonoBehaviour
{
    [SerializeField] private bool crusherEnabled = true;
    [SerializeField] private Vector3 localAxis = Vector3.forward;
    [SerializeField] private float degreesPerSecond = 180f;
    [SerializeField] private Transform[] rotors = System.Array.Empty<Transform>();

    public bool CrusherEnabled
    {
        get => crusherEnabled;
        set => crusherEnabled = value;
    }

    private void Update()
    {
        if (!crusherEnabled || rotors == null || rotors.Length == 0)
        {
            return;
        }

        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.forward;
        float delta = degreesPerSecond * Time.deltaTime;
        for (int i = 0; i < rotors.Length; i++)
        {
            Transform rotor = rotors[i];
            if (rotor == null)
            {
                continue;
            }

            float direction = (i & 1) == 0 ? 1f : -1f;
            rotor.Rotate(axis, delta * direction, Space.Self);
        }
    }
}
