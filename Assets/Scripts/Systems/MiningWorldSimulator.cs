using System;
using UnityEngine;

public static class MiningWorldSimulator
{
    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || !config.isLoaded || progress == null || toUtcTicks <= 0) return 0;

        EnsureRuntime(config, progress, toUtcTicks);
        int changed = AdvanceNaturalShedding(config, progress, fromUtcTicks, toUtcTicks);
        changed += RemoveExpiredRocks(config, progress, toUtcTicks);

        if (toUtcTicks <= fromUtcTicks) return changed;

        for (int i = 0; i < config.miningZones.Count; i++)
        {
            MiningZoneConfig zone = config.miningZones[i];
            if (zone == null || string.IsNullOrWhiteSpace(zone.id) || zone.maxActiveRocks <= 0) continue;

            MiningZoneState zoneState = progress.GetMiningZoneState(zone.id, true);
            int activeCount = CountActiveRocks(config, progress, zone.id, toUtcTicks);
            if (activeCount >= zone.maxActiveRocks) continue;

            long intervalTicks = TimeSpan.FromSeconds(Mathf.Max(1f, zone.spawnIntervalSeconds)).Ticks;
            if (zoneState.lastSpawnUtcTicks > 0 && toUtcTicks - zoneState.lastSpawnUtcTicks < intervalTicks) continue;

            SpawnRock(config, progress, zone, zoneState, toUtcTicks, 0f);
            zoneState.lastSpawnUtcTicks = toUtcTicks;
            changed++;
        }

        return changed;
    }

    public static void EnsureRuntime(WorldConfigDatabase config, PlayerProgress progress, long utcTicks)
    {
        if (config == null || !config.isLoaded || progress == null) return;
        progress.Normalize();

        for (int i = 0; i < config.miningZones.Count; i++)
        {
            MiningZoneConfig zone = config.miningZones[i];
            if (zone == null || string.IsNullOrWhiteSpace(zone.id)) continue;

            MiningZoneState zoneState = progress.GetMiningZoneState(zone.id, true);
            if (zoneState.initialRocksSpawned) continue;

            int count = Mathf.Min(Mathf.Max(0, zone.initialRockCount), Mathf.Max(0, zone.maxActiveRocks));
            for (int rockIndex = 0; rockIndex < count; rockIndex++)
            {
                float ageFraction = Mathf.Clamp01(zone.initialAgeFraction + (rockIndex - count * 0.5f) * 0.08f);
                SpawnRock(config, progress, zone, zoneState, utcTicks, ageFraction);
            }

            zoneState.initialRocksSpawned = true;
            zoneState.lastSpawnUtcTicks = utcTicks;
        }
    }

    public static bool IsRockActive(WorldConfigDatabase config, MiningRockState rock, long utcTicks)
    {
        if (config == null || rock == null || rock.remainingOreKg <= 0.001f) return false;

        MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
        if (zone == null) return false;

        Vector3 position = CalculateRockPosition(zone, rock, utcTicks);
        return position.y > zone.stormY;
    }

    public static bool IsRockSafeForAutopilot(WorldConfigDatabase config, MiningRockState rock, long utcTicks)
    {
        if (!IsRockActive(config, rock, utcTicks)) return false;

        MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
        Vector3 position = CalculateRockPosition(zone, rock, utcTicks);
        return position.y >= zone.stormY + zone.stormSafetyClearanceY;
    }

    public static Vector3 CalculateRockPosition(MiningZoneConfig zone, MiningRockState rock, long utcTicks)
    {
        if (zone == null || rock == null) return Vector3.zero;

        float ageSeconds = rock.spawnedUtcTicks > 0
            ? Mathf.Max(0f, (float)new TimeSpan(utcTicks - rock.spawnedUtcTicks).TotalSeconds)
            : 0f;

        float y;
        if (ageSeconds <= zone.ascentDurationSeconds)
        {
            float t = Mathf.Clamp01(ageSeconds / Mathf.Max(1f, zone.ascentDurationSeconds));
            y = Mathf.Lerp(zone.spawnY, zone.apexY, Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            y = zone.apexY - (ageSeconds - zone.ascentDurationSeconds) * Mathf.Max(0.1f, zone.descentSpeedMS);
        }

        return new Vector3(rock.spawnPosition.x, y, rock.spawnPosition.z);
    }

    public static float HarvestRock(MiningRockState rock, float requestedKg)
    {
        if (rock == null || requestedKg <= 0f || rock.remainingOreKg <= 0f) return 0f;

        float harvested = Mathf.Min(requestedKg, rock.remainingOreKg);
        rock.remainingOreKg = Mathf.Max(0f, rock.remainingOreKg - harvested);
        return harvested;
    }

    public static void EnsureNextNaturalShed(WorldConfigDatabase config, MiningRockState rock, long utcTicks)
    {
        if (config == null || rock == null || rock.nextNaturalShedUtcTicks > 0) return;

        MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
        OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
        if (zone == null || oreType == null) return;

        ScheduleNextNaturalShed(zone, oreType, rock, utcTicks);
    }

    public static bool TryProcessNextNaturalShed(
        WorldConfigDatabase config,
        MiningRockState rock,
        long utcTicks,
        out int amountKg,
        out string oreItemId)
    {
        amountKg = 0;
        oreItemId = "";
        if (config == null || rock == null || rock.remainingOreKg <= 0.001f) return false;

        MiningZoneConfig zone = config.GetMiningZone(rock.zoneId);
        OreTypeConfig oreType = config.GetOreType(rock.oreTypeId);
        if (zone == null || oreType == null || string.IsNullOrWhiteSpace(oreType.oreItemId)) return false;

        EnsureNextNaturalShed(config, rock, utcTicks);
        if (rock.nextNaturalShedUtcTicks <= 0 || rock.nextNaturalShedUtcTicks > utcTicks) return false;

        long eventTicks = rock.nextNaturalShedUtcTicks;
        if (!IsRockActive(config, rock, eventTicks))
        {
            rock.nextNaturalShedUtcTicks = 0;
            return false;
        }

        amountKg = CalculateNaturalChunkKg(zone, oreType, rock);
        float removed = HarvestRock(rock, amountKg);
        amountKg = Mathf.FloorToInt(removed + 0.0001f);
        oreItemId = oreType.oreItemId;

        rock.lastNaturalShedUtcTicks = eventTicks;
        rock.lastNaturalShedAmountKg = amountKg;
        rock.lastNaturalShedOreItemId = oreType.oreItemId;
        rock.shedBufferKg += amountKg;
        rock.shedEventCounter++;
        ScheduleNextNaturalShed(zone, oreType, rock, eventTicks);

        return amountKg > 0;
    }

    public static int CountActiveRocks(WorldConfigDatabase config, PlayerProgress progress, string zoneId, long utcTicks)
    {
        if (config == null || progress == null || progress.miningRocks == null) return 0;

        int count = 0;
        for (int i = 0; i < progress.miningRocks.Count; i++)
        {
            MiningRockState rock = progress.miningRocks[i];
            if (rock == null || rock.zoneId != zoneId) continue;
            if (IsRockActive(config, rock, utcTicks)) count++;
        }

        return count;
    }

    private static int RemoveExpiredRocks(WorldConfigDatabase config, PlayerProgress progress, long utcTicks)
    {
        if (config == null || progress == null || progress.miningRocks == null) return 0;

        int removed = 0;
        for (int i = progress.miningRocks.Count - 1; i >= 0; i--)
        {
            MiningRockState rock = progress.miningRocks[i];
            if (rock == null || !IsRockActive(config, rock, utcTicks))
            {
                progress.miningRocks.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    private static int AdvanceNaturalShedding(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks)
    {
        if (config == null || progress == null || progress.miningRocks == null || toUtcTicks <= 0) return 0;

        int changed = 0;
        long scheduleFrom = fromUtcTicks > 0 ? fromUtcTicks : toUtcTicks;
        for (int i = 0; i < progress.miningRocks.Count; i++)
        {
            MiningRockState rock = progress.miningRocks[i];
            if (rock == null || rock.remainingOreKg <= 0.001f) continue;

            EnsureNextNaturalShed(config, rock, scheduleFrom);
            int guard = 0;
            while (rock.nextNaturalShedUtcTicks > 0 && rock.nextNaturalShedUtcTicks <= toUtcTicks && guard++ < 64)
            {
                if (TryProcessNextNaturalShed(config, rock, toUtcTicks, out int amountKg, out _))
                {
                    changed += Mathf.Max(1, amountKg);
                }
                else
                {
                    break;
                }
            }
        }

        return changed;
    }

    private static MiningRockState SpawnRock(
        WorldConfigDatabase config,
        PlayerProgress progress,
        MiningZoneConfig zone,
        MiningZoneState zoneState,
        long utcTicks,
        float initialAgeFraction)
    {
        if (config == null || progress == null || zone == null || zoneState == null) return null;

        string oreTypeId = PickOreType(zone, zoneState.spawnCounter);
        OreTypeConfig oreType = config.GetOreType(oreTypeId);
        if (oreType == null && config.oreTypes.Count > 0)
        {
            oreType = config.oreTypes[Mathf.Abs(zoneState.spawnCounter) % config.oreTypes.Count];
            oreTypeId = oreType.id;
        }

        int seed = Mathf.Max(0, zoneState.spawnCounter++);
        Vector3 spawnPosition = PickSpawnPosition(config, zone, seed);
        float lifetime = Mathf.Max(1f, zone.LifetimeSeconds);
        long spawnedTicks = utcTicks - TimeSpan.FromSeconds(lifetime * Mathf.Clamp01(initialAgeFraction)).Ticks;

        MiningRockState rock = new MiningRockState
        {
            rockId = zone.id + "_rock_" + seed.ToString("0000"),
            zoneId = zone.id,
            oreTypeId = oreTypeId,
            spawnedUtcTicks = spawnedTicks,
            spawnPosition = spawnPosition,
            remainingOreKg = Mathf.Max(1f, zone.rockOreKg)
        };

        ApplyInitialNaturalErosion(zone, oreType, rock, seed, initialAgeFraction);

        progress.miningRocks.Add(rock);
        EnsureNextNaturalShed(config, rock, utcTicks);
        return rock;
    }

    private static void ApplyInitialNaturalErosion(MiningZoneConfig zone, OreTypeConfig oreType, MiningRockState rock, int seed, float initialAgeFraction)
    {
        if (zone == null || oreType == null || rock == null || initialAgeFraction <= 0f) return;

        float targetLostByStorm = Mathf.Lerp(0.52f, 0.72f, Mathf.Clamp01(oreType.naturalShedKgPerMinute));
        float jitter = Mathf.Lerp(0.85f, 1.15f, Unit01(seed * 37 + 11));
        float lostKg = zone.rockOreKg * Mathf.Clamp01(initialAgeFraction) * targetLostByStorm * jitter;
        HarvestRock(rock, lostKg);
    }

    private static void ScheduleNextNaturalShed(MiningZoneConfig zone, OreTypeConfig oreType, MiningRockState rock, long fromTicks)
    {
        if (zone == null || oreType == null || rock == null)
        {
            return;
        }

        int rockSeed = StableHash(rock.rockId);
        float instability = Mathf.Clamp01(oreType.naturalShedKgPerMinute);
        float minSeconds = Mathf.Lerp(8f, 5f, instability);
        float maxSeconds = Mathf.Lerp(20f, 14f, instability);
        float intervalSeconds = Mathf.Lerp(minSeconds, maxSeconds, Unit01(rock.shedEventCounter * 97 + rockSeed));
        rock.nextNaturalShedUtcTicks = fromTicks + TimeSpan.FromSeconds(intervalSeconds).Ticks;
    }

    private static int CalculateNaturalChunkKg(MiningZoneConfig zone, OreTypeConfig oreType, MiningRockState rock)
    {
        float average = CalculateAverageNaturalChunkKg(zone, oreType);
        int rockSeed = StableHash(rock.rockId);
        float roll = Unit01(rock.shedEventCounter * 131 + rockSeed);
        float rareRoll = Unit01(rock.shedEventCounter * 197 + 17);
        float chunk = Mathf.Lerp(average * 0.45f, average * 1.55f, roll);
        if (rareRoll > 0.88f)
        {
            chunk *= Mathf.Lerp(1.4f, 2.2f, rareRoll);
        }

        return Mathf.Clamp(Mathf.RoundToInt(chunk), 1, Mathf.CeilToInt(Mathf.Max(1f, rock.remainingOreKg)));
    }

    private static float CalculateAverageNaturalChunkKg(MiningZoneConfig zone, OreTypeConfig oreType)
    {
        float lifetimeSeconds = Mathf.Max(60f, zone.LifetimeSeconds);
        float averageIntervalSeconds = Mathf.Lerp(14f, 9.5f, Mathf.Clamp01(oreType.naturalShedKgPerMinute));
        float expectedEvents = Mathf.Max(1f, lifetimeSeconds / averageIntervalSeconds);
        float targetLostByStorm = Mathf.Lerp(0.52f, 0.72f, Mathf.Clamp01(oreType.naturalShedKgPerMinute));
        float targetLostKg = Mathf.Max(1f, zone.rockOreKg * targetLostByStorm);
        float sizeFloor = Mathf.Max(1f, zone.rockRadiusMeters * 0.35f);
        return Mathf.Max(sizeFloor, targetLostKg / expectedEvents);
    }

    private static float Unit01(int seed)
    {
        float value = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + text[i];
                }
            }

            return hash;
        }
    }

    private static string PickOreType(MiningZoneConfig zone, int seed)
    {
        if (zone == null || zone.oreTypeIds == null || zone.oreTypeIds.Count == 0) return "";
        int index = Mathf.Abs(seed) % zone.oreTypeIds.Count;
        return zone.oreTypeIds[index];
    }

    private static Vector2 GetSpawnOffset(int seed, float radius)
    {
        float a = Mathf.Abs(Mathf.Sin(seed * 12.9898f) * 43758.5453f);
        float b = Mathf.Abs(Mathf.Sin((seed + 17) * 78.233f) * 24634.6345f);
        float angle = (a - Mathf.Floor(a)) * Mathf.PI * 2f;
        float distance = Mathf.Sqrt(b - Mathf.Floor(b)) * Mathf.Max(1f, radius);
        return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
    }

    private static Vector3 PickSpawnPosition(WorldConfigDatabase config, MiningZoneConfig zone, int seed)
    {
        Vector3 best = new Vector3(zone.center.x, zone.spawnY, zone.center.z);
        float bestClearance = float.MinValue;

        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector2 offset = GetSpawnOffset(seed * 97 + attempt * 31, zone.radiusMeters);
            Vector3 candidate = new Vector3(zone.center.x + offset.x, zone.spawnY, zone.center.z + offset.y);
            float clearance = GetIslandClearance(config, zone, candidate);
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                best = candidate;
            }

            if (clearance > 0f)
            {
                return candidate;
            }
        }

        return best;
    }

    private static bool IntersectsIsland(WorldConfigDatabase config, MiningZoneConfig zone, Vector3 spawnPosition)
    {
        return GetIslandClearance(config, zone, spawnPosition) <= 0f;
    }

    private static float GetIslandClearance(WorldConfigDatabase config, MiningZoneConfig zone, Vector3 spawnPosition)
    {
        if (config == null || zone == null || config.islands == null) return float.MaxValue;

        float bestClearance = float.MaxValue;
        for (int i = 0; i < config.islands.Count; i++)
        {
            IslandConfig island = config.islands[i];
            if (island == null) continue;

            Vector2 a = new Vector2(spawnPosition.x, spawnPosition.z);
            Vector2 b = new Vector2(island.position.x, island.position.z);
            float islandSafetyRadius = Mathf.Max(120f, island.dockingRadius);
            float radius = islandSafetyRadius + Mathf.Max(0f, zone.rockRadiusMeters);
            bestClearance = Mathf.Min(bestClearance, Vector2.Distance(a, b) - radius);
        }

        return bestClearance == float.MaxValue ? float.MaxValue : bestClearance;
    }
}
