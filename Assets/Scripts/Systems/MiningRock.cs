using System.Collections.Generic;
using UnityEngine;

public class MiningRock : MonoBehaviour
{
    private static readonly List<MiningRock> ActiveRocks = new List<MiningRock>();

    public string rockId = "";
    public string oreTypeId = "";
    public string displayName = "";

    private MetaGameState meta;
    private MiningRockState state;
    private Renderer cachedRenderer;
    private long lastVisualNaturalShedUtcTicks;

    public bool IsDepleted => state == null || state.remainingOreKg <= 0.001f;
    public MetaGameState Meta => meta;
    public MiningRockState ProgressState => state;
    public MiningZoneConfig ZoneConfig => GetZone();
    public OreTypeConfig OreTypeConfig => GetOreType();

    public static MiningRock FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        for (int i = 0; i < ActiveRocks.Count; i++)
        {
            MiningRock rock = ActiveRocks[i];
            if (rock != null && rock.rockId == id && !rock.IsDepleted)
            {
                return rock;
            }
        }

        return null;
    }

    public static bool ShootNearest(Vector3 origin, float rangeMeters, out string message)
    {
        message = "Нет глыбы в дальности выстрела.";
        MiningRock best = null;
        float bestDistance = Mathf.Max(0f, rangeMeters);

        for (int i = 0; i < ActiveRocks.Count; i++)
        {
            MiningRock rock = ActiveRocks[i];
            if (rock == null || rock.IsDepleted) continue;

            float distance = Vector3.Distance(origin, rock.transform.position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = rock;
            }
        }

        if (best == null) return false;
        return best.BreakOffByShot(out message);
    }

    public void Initialize(MetaGameState owner, MiningRockState progressState)
    {
        meta = owner;
        state = progressState;
        rockId = state != null ? state.rockId : "";
        oreTypeId = state != null ? state.oreTypeId : "";

        OreTypeConfig oreType = GetOreType();
        displayName = oreType != null && !string.IsNullOrWhiteSpace(oreType.localNameRu) ? oreType.localNameRu : rockId;

        cachedRenderer = GetComponent<Renderer>();
        lastVisualNaturalShedUtcTicks = state != null ? state.lastNaturalShedUtcTicks : 0;
        ApplyVisual();
        UpdatePosition();
    }

    private void Update()
    {
        if (meta == null || state == null || meta.WorldConfig == null)
        {
            Destroy(gameObject);
            return;
        }

        long nowTicks = meta.CurrentProcessUtcNow.Ticks;
        if (!MiningWorldSimulator.IsRockActive(meta.WorldConfig, state, nowTicks))
        {
            Destroy(gameObject);
            return;
        }

        UpdatePosition();
        SpawnNewNaturalShedFragments();
        ApplyVisual();
    }

    public bool BreakOffByShot(out string message)
    {
        OreTypeConfig oreType = GetOreType();
        if (oreType == null || state == null)
        {
            message = "Глыба не связана с типом руды.";
            return false;
        }

        int amount = Mathf.Max(1, oreType.shotShedKg);
        int spawned = SpawnFragments(amount, true);
        message = spawned > 0
            ? "Выстрел отколол " + spawned + " кг: " + oreType.oreItemId + "."
            : "Выстрел попал, но глыба уже почти пуста.";
        return spawned > 0;
    }

    private void UpdatePosition()
    {
        MiningZoneConfig zone = GetZone();
        if (zone == null || meta == null || state == null) return;

        transform.position = MiningWorldSimulator.CalculateRockPosition(zone, state, meta.CurrentProcessUtcNow.Ticks);
    }

    private void SpawnNewNaturalShedFragments()
    {
        OreTypeConfig oreType = GetOreType();
        if (oreType == null || state == null) return;
        if (state.lastNaturalShedUtcTicks <= 0 || state.lastNaturalShedUtcTicks <= lastVisualNaturalShedUtcTicks) return;
        if (state.lastNaturalShedAmountKg <= 0 || state.lastNaturalShedOreItemId != oreType.oreItemId) return;

        SpawnFragments(state.lastNaturalShedAmountKg, false, false);
        lastVisualNaturalShedUtcTicks = state.lastNaturalShedUtcTicks;
    }

    private int SpawnFragments(int requestedKg, bool shot, bool consumeRock = true)
    {
        OreTypeConfig oreType = GetOreType();
        MiningZoneConfig zone = GetZone();
        if (oreType == null || zone == null || state == null || requestedKg <= 0) return 0;

        int actualKg = consumeRock ? Mathf.FloorToInt(MiningWorldSimulator.HarvestRock(state, requestedKg)) : requestedKg;
        int remaining = actualKg;
        while (remaining > 0)
        {
            int chunkKg = shot ? Mathf.Min(remaining, Random.Range(1, 4)) : remaining;
            remaining -= chunkKg;

            GameObject fragmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fragmentObject.name = oreType.oreItemId + "_fragment";
            fragmentObject.transform.position = transform.position
                + Vector3.down * Mathf.Max(1f, zone.rockRadiusMeters * 0.75f)
                + Random.insideUnitSphere * Mathf.Max(1f, zone.rockRadiusMeters * 0.2f);
            fragmentObject.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.6f, Mathf.Clamp01(chunkKg / 4f));

            Collider collider = fragmentObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            MiningFragment fragment = fragmentObject.AddComponent<MiningFragment>();
            fragment.Initialize(oreType.oreItemId, chunkKg, oreType.fragmentFallSpeedMS, zone.stormY, oreType.color);
        }

        return actualKg;
    }

    private void ApplyVisual()
    {
        MiningZoneConfig zone = GetZone();
        OreTypeConfig oreType = GetOreType();
        if (zone == null) return;

        float fullness = state != null && zone.rockOreKg > 0f ? Mathf.Clamp01(state.remainingOreKg / zone.rockOreKg) : 1f;
        float radius = Mathf.Max(1f, zone.rockRadiusMeters) * Mathf.Lerp(0.45f, 1f, Mathf.Pow(fullness, 1f / 3f));
        transform.localScale = new Vector3(radius * 2.2f, radius * 1.5f, radius * 1.9f);

        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedRenderer != null && oreType != null)
        {
            cachedRenderer.material.color = oreType.color;
        }
    }

    private MiningZoneConfig GetZone()
    {
        return meta != null && meta.WorldConfig != null && state != null ? meta.WorldConfig.GetMiningZone(state.zoneId) : null;
    }

    private OreTypeConfig GetOreType()
    {
        return meta != null && meta.WorldConfig != null && state != null ? meta.WorldConfig.GetOreType(state.oreTypeId) : null;
    }

    private void OnEnable()
    {
        if (!ActiveRocks.Contains(this))
        {
            ActiveRocks.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveRocks.Remove(this);
    }
}
