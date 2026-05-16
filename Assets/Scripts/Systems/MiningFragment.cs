using UnityEngine;

public class MiningFragment : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<MiningFragment> ActiveFragments = new System.Collections.Generic.List<MiningFragment>();

    public string oreItemId = "";
    public int amountKg = 1;
    public float fallSpeedMS = 4f;
    public float stormY = -100f;
    public float spinSpeedDeg = 90f;

    private ShipPhysics cachedShip;

    public static MiningFragment FindNearestForLeviathan(Vector3 position, float radiusMeters)
    {
        MiningFragment best = null;
        float bestSqr = Mathf.Max(0f, radiusMeters) * Mathf.Max(0f, radiusMeters);
        for (int i = 0; i < ActiveFragments.Count; i++)
        {
            MiningFragment fragment = ActiveFragments[i];
            if (fragment == null || fragment.amountKg <= 0) continue;

            float sqr = (fragment.transform.position - position).sqrMagnitude;
            if (sqr > bestSqr) continue;

            best = fragment;
            bestSqr = sqr;
        }

        return best;
    }

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

        Leviathan eater = Leviathan.FindFragmentEater(transform.position);
        if (eater != null && eater.TryEatOre(oreItemId, amountKg))
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

    private void OnEnable()
    {
        if (!ActiveFragments.Contains(this))
        {
            ActiveFragments.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveFragments.Remove(this);
    }
}
