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
    private MetaGameState cachedMeta;

    public static int ActiveCount => ActiveFragments.Count;

    public static void GetActiveFragments(System.Collections.Generic.List<MiningFragment> results)
    {
        if (results == null) return;

        results.Clear();
        for (int i = ActiveFragments.Count - 1; i >= 0; i--)
        {
            MiningFragment fragment = ActiveFragments[i];
            if (fragment == null || fragment.amountKg <= 0)
            {
                ActiveFragments.RemoveAt(i);
                continue;
            }

            results.Add(fragment);
        }
    }

    public static bool HasActiveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        for (int i = 0; i < ActiveFragments.Count; i++)
        {
            MiningFragment fragment = ActiveFragments[i];
            if (fragment != null && fragment.amountKg > 0 && fragment.oreItemId == itemId)
            {
                return true;
            }
        }

        return false;
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

        ShipPhysics ship = GetShip();
        MetaGameState meta = GetMeta();
        bool starterSortieCatch = meta != null && meta.CanCatchStarterSortieFragmentsInCargo;
        if (ship == null || (ship.miningImpactHoldCapacityKg <= 0f && !starterSortieCatch)) return;

        float radius = Mathf.Max(0.1f, ship.miningCatchRadiusMeters);
        if (Vector3.Distance(transform.position, ship.transform.position) > radius) return;

        if (ship.TryCollectMiningFragment(oreItemId, amountKg, fallSpeedMS, out _))
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

    private MetaGameState GetMeta()
    {
        if (cachedMeta == null)
        {
            cachedMeta = FindFirstObjectByType<MetaGameState>();
        }

        return cachedMeta;
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
