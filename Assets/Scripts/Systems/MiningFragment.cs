using UnityEngine;

public class MiningFragment : MonoBehaviour
{
    public string oreItemId = "";
    public int amountKg = 1;
    public float fallSpeedMS = 4f;
    public float stormY = -100f;
    public float spinSpeedDeg = 90f;

    private ShipPhysics cachedShip;

    public void Initialize(string itemId, int amount, float fallSpeed, float stormLevelY, Color color)
    {
        oreItemId = itemId ?? "";
        amountKg = Mathf.Max(1, amount);
        fallSpeedMS = Mathf.Max(0.1f, fallSpeed);
        stormY = stormLevelY;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    private void Update()
    {
        transform.position += Vector3.down * (fallSpeedMS * Time.deltaTime);
        transform.Rotate(Vector3.up, spinSpeedDeg * Time.deltaTime, Space.World);

        if (transform.position.y <= stormY)
        {
            Destroy(gameObject);
            return;
        }

        ShipPhysics ship = GetShip();
        if (ship == null || ship.miningImpactHoldCapacityKg <= 0f) return;

        float radius = Mathf.Max(0.1f, ship.miningCatchRadiusMeters);
        if (Vector3.Distance(transform.position, ship.transform.position) > radius) return;

        if (ship.TryCollectMiningFragment(oreItemId, amountKg, out _))
        {
            Destroy(gameObject);
        }
    }

    private ShipPhysics GetShip()
    {
        if (cachedShip == null)
        {
            cachedShip = FindFirstObjectByType<ShipPhysics>();
        }

        return cachedShip;
    }
}
