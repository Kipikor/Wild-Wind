using UnityEngine;

public class MiningFragment : MonoBehaviour
{
    private static readonly System.Collections.Generic.List<MiningFragment> ActiveFragments = new System.Collections.Generic.List<MiningFragment>();
    private static readonly MaterialPropertyBlock ColorBlock = new MaterialPropertyBlock();
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    public const int DefaultMaxActiveFragments = 160;

    public string oreItemId = "";
    public int amountKg = 1;
    public float fallSpeedMS = 4f;
    public float stormY = -100f;
    public float spinSpeedDeg = 90f;
    public float maxLifetimeSeconds = 45f;

    private ShipPhysics cachedShip;
    private MetaGameState cachedMeta;
    private float spawnedAtTime;

    public static int ActiveCount => ActiveFragments.Count;
    public static int MaxActiveFragments { get; set; } = DefaultMaxActiveFragments;
    public static bool CanSpawnMore => ActiveCount < Mathf.Max(1, MaxActiveFragments);

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
        ApplyRendererColor(renderer, color);
        spawnedAtTime = Time.time;
    }

    private void Update()
    {
        transform.position += Vector3.down * (fallSpeedMS * Time.deltaTime);
        transform.Rotate(Vector3.up, spinSpeedDeg * Time.deltaTime, Space.World);

        if (transform.position.y <= stormY || Time.time - spawnedAtTime >= Mathf.Max(1f, maxLifetimeSeconds))
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
        spawnedAtTime = Time.time;
        if (!ActiveFragments.Contains(this))
        {
            ActiveFragments.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveFragments.Remove(this);
    }

    public static void ApplyRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;

        renderer.GetPropertyBlock(ColorBlock);
        ColorBlock.SetColor(BaseColorPropertyId, color);
        ColorBlock.SetColor(ColorPropertyId, color);
        renderer.SetPropertyBlock(ColorBlock);
    }
}
