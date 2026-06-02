using System;
using System.Collections.Generic;
using UnityEngine;

public enum FlagshipRoomMode
{
    Off = 0,
    Quarter = 25,
    Half = 50,
    Full = 100
}

public enum FlagshipRoomKind
{
    Command,
    Engine,
    Production,
    Mess,
    Medical,
    Security,
    Quarters,
    Repair,
    Communications,
    Storage
}

public enum FlagshipFailureSeverity
{
    Minor,
    Medium,
    Major,
    Critical
}

public enum FlagshipFailureEffectKind
{
    EfficiencyPenalty,
    NeedDrain,
    RepairPenalty,
    ControlSpin,
    ForcedDescent,
    Shutdown
}

public enum FlagshipManualRepairTaskSize
{
    Quick,
    Medium,
    Long
}

public static class FlagshipNeedIds
{
    public const string Stamina = "need_workforce";
    public const string Health = "need_health";
    public const string Order = "need_safety";
    public const string Comfort = "need_comfort";
    public const string Focus = "need_creativity";
    public const string Maintenance = "need_repair";
    public const string Morale = "need_morale";
    public const string LegacyHomeConnection = "need_capital_connection";

    public static readonly string[] All =
    {
        Stamina,
        Health,
        Order,
        Comfort,
        Focus,
        Maintenance,
        Morale
    };
}

[Serializable]
public class FlagshipInteriorState
{
    public string flagshipId = "";
    public string hullId = "";
    public int rank;
    public int crewCapacity;
    public List<FlagshipRoomState> rooms = new List<FlagshipRoomState>();
    public List<FlagshipNeedState> needs = new List<FlagshipNeedState>();
    public bool expeditionActive;
    public long expeditionStartedUtcTicks;
    public int nextFailureSequence = 1;
    public float controlSpinSeverity01;
    public float forcedDescentSeverity01;
    public string lastMessage = "";

    public void Normalize()
    {
        flagshipId ??= "";
        hullId ??= "";
        rank = Mathf.Max(0, rank);
        crewCapacity = Mathf.Max(0, crewCapacity);
        rooms ??= new List<FlagshipRoomState>();
        needs ??= new List<FlagshipNeedState>();
        if (expeditionStartedUtcTicks < 0) expeditionStartedUtcTicks = 0;
        if (!expeditionActive) expeditionStartedUtcTicks = 0;
        nextFailureSequence = Mathf.Max(1, nextFailureSequence);
        controlSpinSeverity01 = Mathf.Clamp01(controlSpinSeverity01);
        forcedDescentSeverity01 = Mathf.Clamp01(forcedDescentSeverity01);
        lastMessage ??= "";

        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            FlagshipRoomState room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.roomId))
            {
                rooms.RemoveAt(i);
                continue;
            }

            room.Normalize();
        }

        for (int i = needs.Count - 1; i >= 0; i--)
        {
            FlagshipNeedState need = needs[i];
            if (need == null || string.IsNullOrWhiteSpace(need.needId))
            {
                needs.RemoveAt(i);
                continue;
            }

            need.Normalize();
            if (need.needId == FlagshipNeedIds.LegacyHomeConnection)
            {
                need.needId = FlagshipNeedIds.Morale;
            }
        }

        MergeDuplicateNeeds();
    }

    public FlagshipRoomState GetRoomState(string roomId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return null;
        rooms ??= new List<FlagshipRoomState>();

        for (int i = 0; i < rooms.Count; i++)
        {
            FlagshipRoomState room = rooms[i];
            if (room != null && room.roomId == roomId)
            {
                return room;
            }
        }

        if (!createIfMissing) return null;

        FlagshipRoomState newRoom = new FlagshipRoomState { roomId = roomId };
        rooms.Add(newRoom);
        return newRoom;
    }

    public FlagshipNeedState GetNeedState(string needId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(needId)) return null;
        needs ??= new List<FlagshipNeedState>();

        for (int i = 0; i < needs.Count; i++)
        {
            FlagshipNeedState need = needs[i];
            if (need != null && need.needId == needId)
            {
                return need;
            }
        }

        if (!createIfMissing) return null;

        FlagshipNeedState newNeed = new FlagshipNeedState { needId = needId };
        needs.Add(newNeed);
        return newNeed;
    }

    public FlagshipFailureState FindFailure(string failureId, out FlagshipRoomState room)
    {
        room = null;
        if (string.IsNullOrWhiteSpace(failureId) || rooms == null) return null;

        for (int i = 0; i < rooms.Count; i++)
        {
            FlagshipRoomState candidateRoom = rooms[i];
            if (candidateRoom == null || candidateRoom.failures == null) continue;

            for (int j = 0; j < candidateRoom.failures.Count; j++)
            {
                FlagshipFailureState failure = candidateRoom.failures[j];
                if (failure != null && failure.failureId == failureId)
                {
                    room = candidateRoom;
                    return failure;
                }
            }
        }

        return null;
    }

    private void MergeDuplicateNeeds()
    {
        for (int i = 0; i < needs.Count; i++)
        {
            FlagshipNeedState keep = needs[i];
            if (keep == null) continue;

            for (int j = needs.Count - 1; j > i; j--)
            {
                FlagshipNeedState duplicate = needs[j];
                if (duplicate == null || duplicate.needId != keep.needId) continue;

                keep.currentValue = Mathf.Max(keep.currentValue, duplicate.currentValue);
                keep.maxValue = Mathf.Max(keep.maxValue, duplicate.maxValue);
                keep.recoveryCapacityProgress = Mathf.Max(keep.recoveryCapacityProgress, duplicate.recoveryCapacityProgress);
                keep.recoveryItemProgress = Mathf.Max(keep.recoveryItemProgress, duplicate.recoveryItemProgress);
                keep.initialized |= duplicate.initialized;
                needs.RemoveAt(j);
            }
        }
    }
}

[Serializable]
public class FlagshipRoomState
{
    public string roomId = "";
    public FlagshipRoomMode mode = FlagshipRoomMode.Full;
    public float faultProgress;
    public string assignedRepairFailureId = "";
    public List<FlagshipFailureState> failures = new List<FlagshipFailureState>();

    public void Normalize()
    {
        roomId ??= "";
        if (!Enum.IsDefined(typeof(FlagshipRoomMode), mode))
        {
            mode = FlagshipRoomMode.Full;
        }

        faultProgress = Mathf.Max(0f, faultProgress);
        assignedRepairFailureId ??= "";
        failures ??= new List<FlagshipFailureState>();

        for (int i = failures.Count - 1; i >= 0; i--)
        {
            FlagshipFailureState failure = failures[i];
            if (failure == null || string.IsNullOrWhiteSpace(failure.failureId))
            {
                failures.RemoveAt(i);
                continue;
            }

            failure.Normalize();
            if (failure.resolved)
            {
                failures.RemoveAt(i);
            }
        }
    }
}

[Serializable]
public class FlagshipNeedState
{
    public string needId = "";
    public float currentValue;
    public float maxValue;
    public float recoveryCapacityProgress;
    public float recoveryItemProgress;
    public bool initialized;

    public void Normalize()
    {
        needId ??= "";
        maxValue = Mathf.Max(0f, maxValue);
        currentValue = Mathf.Clamp(currentValue, 0f, maxValue);
        recoveryCapacityProgress = Mathf.Max(0f, recoveryCapacityProgress);
        recoveryItemProgress = Mathf.Max(0f, recoveryItemProgress);
    }
}

[Serializable]
public class FlagshipFailureState
{
    public string failureId = "";
    public string roomId = "";
    public string displayName = "";
    public FlagshipFailureSeverity severity = FlagshipFailureSeverity.Minor;
    public FlagshipFailureEffectKind effectKind = FlagshipFailureEffectKind.EfficiencyPenalty;
    public string extraNeedId = "";
    public float effectAmount = 0.1f;
    public float repairWorkRequired = 20f;
    public float autoRepairProgress;
    public long createdUtcTicks;
    public bool autoRepairing;
    public bool resolved;

    public float AutoRepair01 => repairWorkRequired <= 0f ? 1f : Mathf.Clamp01(autoRepairProgress / repairWorkRequired);

    public FlagshipManualRepairTaskSize ManualRepairTaskSize
    {
        get
        {
            switch (severity)
            {
                case FlagshipFailureSeverity.Minor:
                    return FlagshipManualRepairTaskSize.Quick;
                case FlagshipFailureSeverity.Medium:
                    return FlagshipManualRepairTaskSize.Medium;
                default:
                    return FlagshipManualRepairTaskSize.Long;
            }
        }
    }

    public void Normalize()
    {
        failureId ??= "";
        roomId ??= "";
        displayName ??= "";
        extraNeedId ??= "";
        if (!Enum.IsDefined(typeof(FlagshipFailureSeverity), severity))
        {
            severity = FlagshipFailureSeverity.Minor;
        }

        if (!Enum.IsDefined(typeof(FlagshipFailureEffectKind), effectKind))
        {
            effectKind = FlagshipFailureEffectKind.EfficiencyPenalty;
        }

        effectAmount = Mathf.Max(0f, effectAmount);
        repairWorkRequired = Mathf.Max(1f, repairWorkRequired);
        autoRepairProgress = Mathf.Clamp(autoRepairProgress, 0f, repairWorkRequired);
    }
}

public class FlagshipRoomDefinition
{
    public string roomId = "";
    public string displayName = "";
    public FlagshipRoomKind kind;
    public int minRank = FlagshipInteriorSimulator.MinimumFlagshipRank;
    public float baseFaultsPerHour = 0.04f;
    public int repairSlots;
    public float repairPowerPerHour;
    public List<FlagshipNeedRate> needLoads = new List<FlagshipNeedRate>();
    public List<FlagshipNeedRate> needRecovery = new List<FlagshipNeedRate>();
    public List<FlagshipNeedRate> needMaxBonuses = new List<FlagshipNeedRate>();
    public List<FlagshipFailureTemplate> failures = new List<FlagshipFailureTemplate>();
}

public class FlagshipNeedRate
{
    public string needId = "";
    public float amount;

    public FlagshipNeedRate(string needId, float amount)
    {
        this.needId = needId;
        this.amount = amount;
    }
}

public class FlagshipFailureTemplate
{
    public string failureKey = "";
    public string displayName = "";
    public FlagshipFailureSeverity severity = FlagshipFailureSeverity.Minor;
    public FlagshipFailureEffectKind effectKind = FlagshipFailureEffectKind.EfficiencyPenalty;
    public string extraNeedId = "";
    public float effectAmount = 0.1f;
    public float repairWorkRequired = 20f;
    public float weight = 1f;
}

public static class FlagshipInteriorSimulator
{
    public const int MinimumFlagshipRank = 3;
    public const string PlayerFlagshipId = "player_flagship";

    private const float MaxStepMinutes = 5f;
    private const float RepairStaminaCostPerWork = 0.05f;
    private const float RepairMaintenanceCostPerWork = 0.10f;
    private const float MoraleDrainPerExpeditionHour = 4f;

    private static readonly List<FlagshipRoomDefinition> defaultRoomDefinitions = BuildDefaultRoomDefinitions();

    public static FlagshipInteriorState EnsurePlayerFlagshipInterior(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (progress == null) return null;

        progress.Normalize();
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return null;

        return EnsurePlayerFlagshipInteriorInternal(config, progress);
    }

    public static int Advance(WorldConfigDatabase config, PlayerProgress progress, long fromUtcTicks, long toUtcTicks, bool advanceExpeditionMorale = true)
    {
        if (progress == null || toUtcTicks <= fromUtcTicks) return 0;

        progress.Normalize();
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return 0;

        EnsurePlayerFlagshipInteriorInternal(config, progress);

        int changed = 0;
        if (progress.flagshipInteriors == null) return changed;

        for (int i = 0; i < progress.flagshipInteriors.Count; i++)
        {
            FlagshipInteriorState interior = progress.flagshipInteriors[i];
            if (interior == null) continue;

            interior.Normalize();
            if (!IsFlagshipRank(interior.rank)) continue;

            EnsureDefaultInterior(interior);
            changed += AdvanceInterior(config, progress, interior, fromUtcTicks, toUtcTicks, advanceExpeditionMorale);
        }

        return changed;
    }

    public static bool CompleteManualRepair(PlayerProgress progress, string flagshipId, string failureId, out string message)
    {
        message = "";
        if (progress == null || string.IsNullOrWhiteSpace(flagshipId) || string.IsNullOrWhiteSpace(failureId))
        {
            message = "No flagship or failure id.";
            return false;
        }

        if (SessionExtractionCoreRuntime.IsCoreMode(progress))
        {
            message = "Flagship repairs are disabled in session extraction core.";
            return false;
        }

        FlagshipInteriorState interior = progress.GetFlagshipInteriorState(flagshipId, false);
        if (interior == null)
        {
            message = "Flagship interior not found.";
            return false;
        }

        FlagshipRoomState room;
        FlagshipFailureState failure = interior.FindFailure(failureId, out room);
        if (failure == null || room == null)
        {
            message = "Failure already repaired or not found.";
            return false;
        }

        room.failures.Remove(failure);
        room.assignedRepairFailureId = room.assignedRepairFailureId == failureId ? "" : room.assignedRepairFailureId;
        interior.lastMessage = "Manual repair completed: " + failure.displayName + ".";
        message = interior.lastMessage;
        return true;
    }

    public static bool SetRoomMode(PlayerProgress progress, string flagshipId, string roomId, FlagshipRoomMode mode)
    {
        if (SessionExtractionCoreRuntime.IsCoreMode(progress)) return false;

        FlagshipInteriorState interior = progress != null ? progress.GetFlagshipInteriorState(flagshipId, false) : null;
        FlagshipRoomState room = interior != null ? interior.GetRoomState(roomId, false) : null;
        if (room == null || !Enum.IsDefined(typeof(FlagshipRoomMode), mode)) return false;

        room.mode = mode;
        return true;
    }

    public static bool StartExpedition(PlayerProgress progress, string flagshipId, long utcTicks, out string message)
    {
        message = "";
        if (SessionExtractionCoreRuntime.IsCoreMode(progress))
        {
            message = "Flagship expeditions are disabled in session extraction core.";
            return false;
        }

        FlagshipInteriorState interior = progress != null ? progress.GetFlagshipInteriorState(flagshipId, false) : null;
        if (interior == null)
        {
            message = "Flagship interior not found.";
            return false;
        }

        EnsureDefaultInterior(interior);
        interior.expeditionActive = true;
        interior.expeditionStartedUtcTicks = Math.Max(0L, utcTicks);
        interior.lastMessage = "Экспедиция начата: мораль начала снижаться.";
        message = interior.lastMessage;
        return true;
    }

    public static bool CompleteExpeditionReturn(PlayerProgress progress, string flagshipId, out string message)
    {
        message = "";
        if (SessionExtractionCoreRuntime.IsCoreMode(progress))
        {
            message = "Flagship expeditions are disabled in session extraction core.";
            return false;
        }

        FlagshipInteriorState interior = progress != null ? progress.GetFlagshipInteriorState(flagshipId, false) : null;
        if (interior == null)
        {
            message = "Flagship interior not found.";
            return false;
        }

        EnsureDefaultInterior(interior);
        ResetMoraleAfterReturn(interior);
        interior.lastMessage = "Экспедиция завершена: мораль восстановлена в столице.";
        message = interior.lastMessage;
        return true;
    }

    public static void ResetMoraleAfterReturn(FlagshipInteriorState interior)
    {
        if (interior == null) return;

        interior.expeditionActive = false;
        interior.expeditionStartedUtcTicks = 0;
        ApplyNeedCaps(interior);
        FlagshipNeedState morale = interior.GetNeedState(FlagshipNeedIds.Morale, true);
        if (morale == null) return;

        morale.currentValue = morale.maxValue;
        morale.recoveryCapacityProgress = 0f;
        morale.recoveryItemProgress = 0f;
        morale.initialized = true;
    }

    public static void EnsureDefaultInterior(FlagshipInteriorState interior)
    {
        if (interior == null) return;

        interior.Normalize();
        if (!IsFlagshipRank(interior.rank)) return;

        if (interior.crewCapacity <= 0)
        {
            interior.crewCapacity = GetDefaultCrewForRank(interior.rank);
        }

        for (int i = 0; i < FlagshipNeedIds.All.Length; i++)
        {
            interior.GetNeedState(FlagshipNeedIds.All[i], true);
        }

        for (int i = 0; i < defaultRoomDefinitions.Count; i++)
        {
            FlagshipRoomDefinition definition = defaultRoomDefinitions[i];
            if (definition == null || definition.minRank > interior.rank) continue;

            FlagshipRoomState room = interior.GetRoomState(definition.roomId, true);
            if (!Enum.IsDefined(typeof(FlagshipRoomMode), room.mode))
            {
                room.mode = FlagshipRoomMode.Full;
            }
        }

        ApplyNeedCaps(interior);
    }

    public static FlagshipFailureState AddFailure(
        FlagshipInteriorState interior,
        string roomId,
        FlagshipFailureSeverity severity,
        FlagshipFailureEffectKind effectKind,
        string extraNeedId,
        float effectAmount,
        float repairWorkRequired,
        long utcTicks)
    {
        if (interior == null || string.IsNullOrWhiteSpace(roomId)) return null;

        FlagshipRoomState room = interior.GetRoomState(roomId, true);
        int sequence = Mathf.Max(1, interior.nextFailureSequence++);
        FlagshipFailureState failure = new FlagshipFailureState
        {
            failureId = roomId + "_failure_" + sequence,
            roomId = roomId,
            displayName = severity + " " + effectKind,
            severity = severity,
            effectKind = effectKind,
            extraNeedId = extraNeedId ?? "",
            effectAmount = Mathf.Max(0f, effectAmount),
            repairWorkRequired = Mathf.Max(1f, repairWorkRequired),
            createdUtcTicks = utcTicks
        };

        room.failures.Add(failure);
        interior.lastMessage = "Failure added: " + failure.displayName + ".";
        return failure;
    }

    public static float GetRoomEffectiveOutput01(FlagshipInteriorState interior, FlagshipRoomState room)
    {
        FlagshipRoomDefinition definition = room != null ? GetRoomDefinition(room.roomId) : null;
        return GetRoomEffectiveOutput01(interior, room, definition);
    }

    public static bool IsFlagshipRank(int rank)
    {
        return rank >= MinimumFlagshipRank;
    }

    private static int AdvanceInterior(WorldConfigDatabase config, PlayerProgress progress, FlagshipInteriorState interior, long fromUtcTicks, long toUtcTicks, bool advanceExpeditionMorale)
    {
        int changed = 0;
        long cursor = fromUtcTicks;
        long maxStepTicks = TimeSpan.FromMinutes(MaxStepMinutes).Ticks;
        int guard = 0;

        while (cursor < toUtcTicks && guard < 100000)
        {
            guard++;
            long next = Math.Min(toUtcTicks, cursor + maxStepTicks);
            float deltaHours = Mathf.Max(0f, (float)new TimeSpan(next - cursor).TotalHours);
            if (deltaHours > 0f)
            {
                changed += AdvanceInteriorStep(config, progress, interior, deltaHours, next, advanceExpeditionMorale);
            }

            cursor = next;
        }

        return changed;
    }

    private static int AdvanceInteriorStep(WorldConfigDatabase config, PlayerProgress progress, FlagshipInteriorState interior, float deltaHours, long utcTicks, bool advanceExpeditionMorale)
    {
        int changed = 0;
        interior.controlSpinSeverity01 = 0f;
        interior.forcedDescentSeverity01 = 0f;

        ApplyNeedCaps(interior);
        if (advanceExpeditionMorale)
        {
            changed += AdvanceMorale(interior, deltaHours, GetExpeditionMoraleDrainMultiplier(progress, interior));
        }
        changed += AdvanceRoomNeeds(config, progress, interior, deltaHours);
        changed += AdvanceFailures(interior, deltaHours, utcTicks);
        changed += AdvanceAutoRepair(interior, deltaHours);
        ApplyActiveFailureHazards(interior);
        return changed;
    }

    private static int AdvanceMorale(FlagshipInteriorState interior, float deltaHours, float drainMultiplier)
    {
        if (interior == null || !interior.expeditionActive || deltaHours <= 0f) return 0;

        FlagshipNeedState morale = interior.GetNeedState(FlagshipNeedIds.Morale, true);
        if (morale == null) return 0;

        if (!morale.initialized)
        {
            morale.currentValue = Mathf.Max(1f, morale.maxValue);
            morale.initialized = true;
        }

        float before = morale.currentValue;
        morale.currentValue = Mathf.Max(0f, morale.currentValue - MoraleDrainPerExpeditionHour * Mathf.Max(0f, drainMultiplier) * deltaHours);
        return morale.currentValue < before - 0.001f ? 1 : 0;
    }

    private static float GetExpeditionMoraleDrainMultiplier(PlayerProgress progress, FlagshipInteriorState interior)
    {
        if (progress == null || interior == null || interior.flagshipId != PlayerFlagshipId)
        {
            return 1f;
        }

        FlagshipExpeditionState expedition = progress.activeExpedition;
        if (expedition == null || !expedition.active)
        {
            return 1f;
        }

        expedition.Normalize();
        return Mathf.Max(0f, expedition.moraleDrainMultiplier);
    }

    private static int AdvanceRoomNeeds(WorldConfigDatabase config, PlayerProgress progress, FlagshipInteriorState interior, float deltaHours)
    {
        int changed = 0;

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            FlagshipRoomDefinition definition = room != null ? GetRoomDefinition(room.roomId) : null;
            if (room == null || definition == null) continue;

            float loadScale = GetModeNeedScale(room.mode);
            float recoveryScale = GetModeOutputScale(room.mode);
            if (loadScale <= 0f && recoveryScale <= 0f) continue;

            float roomNeedFactor = GetRoomNeedFactor(interior, definition);

            for (int j = 0; j < definition.needLoads.Count; j++)
            {
                FlagshipNeedRate load = definition.needLoads[j];
                if (load == null || string.IsNullOrWhiteSpace(load.needId) || load.amount <= 0f) continue;

                DrainNeed(interior, load.needId, load.amount * loadScale * deltaHours);
                changed++;
            }

            for (int j = 0; j < definition.needRecovery.Count; j++)
            {
                FlagshipNeedRate recovery = definition.needRecovery[j];
                if (recovery == null || string.IsNullOrWhiteSpace(recovery.needId) || recovery.amount <= 0f) continue;

                float points = recovery.amount * recoveryScale * roomNeedFactor * deltaHours;
                changed += RestoreNeed(config, progress, interior, recovery.needId, points);
            }
        }

        ApplyFailureNeedDrains(interior, deltaHours);
        return changed;
    }

    private static int AdvanceFailures(FlagshipInteriorState interior, float deltaHours, long utcTicks)
    {
        int changed = 0;

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            FlagshipRoomDefinition definition = room != null ? GetRoomDefinition(room.roomId) : null;
            if (room == null || definition == null || room.mode == FlagshipRoomMode.Off) continue;

            float output01 = GetRoomEffectiveOutput01(interior, room, definition);
            float lowOutputMultiplier = output01 < 0.25f ? 4f : output01 < 0.5f ? 2.5f : output01 < 0.75f ? 1.6f : 1f;
            float activeFailureMultiplier = 1f + Mathf.Max(0, room.failures.Count) * 0.35f;
            float modeStress = GetModeStressScale(room.mode);
            float gain = definition.baseFaultsPerHour * modeStress * lowOutputMultiplier * activeFailureMultiplier * deltaHours;
            if (gain <= 0f) continue;

            room.faultProgress += gain;
            if (room.faultProgress < 1f) continue;

            room.faultProgress = Mathf.Max(0f, room.faultProgress - 1f);
            FlagshipFailureTemplate template = PickFailureTemplate(definition, interior, room, utcTicks);
            if (template == null) continue;

            FlagshipFailureState failure = AddFailureFromTemplate(interior, room, template, utcTicks);
            if (failure != null)
            {
                changed++;
            }
        }

        return changed;
    }

    private static int AdvanceAutoRepair(FlagshipInteriorState interior, float deltaHours)
    {
        List<FlagshipRepairProvider> providers = BuildRepairProviders(interior);
        if (providers.Count == 0) return 0;

        List<FlagshipFailureTarget> targets = CollectFailureTargets(interior);
        if (targets.Count == 0) return 0;

        targets.Sort(CompareFailurePriority);
        int changed = 0;
        int targetIndex = 0;

        for (int i = 0; i < providers.Count && targetIndex < targets.Count; i++)
        {
            FlagshipRepairProvider provider = providers[i];
            if (provider.repairPowerPerHour <= 0f) continue;

            FlagshipFailureTarget target = targets[targetIndex++];
            FlagshipFailureState failure = target.failure;
            FlagshipRoomState room = target.room;
            if (failure == null || room == null) continue;

            float work = provider.repairPowerPerHour * deltaHours;
            if (work <= 0f) continue;

            DrainNeed(interior, FlagshipNeedIds.Stamina, work * RepairStaminaCostPerWork);
            DrainNeed(interior, FlagshipNeedIds.Maintenance, work * RepairMaintenanceCostPerWork);

            failure.autoRepairing = true;
            failure.autoRepairProgress = Mathf.Min(failure.repairWorkRequired, failure.autoRepairProgress + work);
            provider.room.assignedRepairFailureId = failure.failureId;
            room.assignedRepairFailureId = failure.failureId;
            changed++;

            if (failure.autoRepairProgress + 0.001f >= failure.repairWorkRequired)
            {
                room.failures.Remove(failure);
                room.assignedRepairFailureId = "";
                provider.room.assignedRepairFailureId = "";
                interior.lastMessage = "Auto repair completed: " + failure.displayName + ".";
            }
        }

        return changed;
    }

    private static void ApplyActiveFailureHazards(FlagshipInteriorState interior)
    {
        if (interior.rooms == null) return;

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            if (room == null || room.failures == null) continue;

            for (int j = 0; j < room.failures.Count; j++)
            {
                FlagshipFailureState failure = room.failures[j];
                if (failure == null) continue;

                switch (failure.effectKind)
                {
                    case FlagshipFailureEffectKind.ControlSpin:
                        interior.controlSpinSeverity01 = Mathf.Max(interior.controlSpinSeverity01, Mathf.Clamp01(failure.effectAmount));
                        break;
                    case FlagshipFailureEffectKind.ForcedDescent:
                        interior.forcedDescentSeverity01 = Mathf.Max(interior.forcedDescentSeverity01, Mathf.Clamp01(failure.effectAmount));
                        break;
                }
            }
        }
    }

    private static void ApplyNeedCaps(FlagshipInteriorState interior)
    {
        Dictionary<string, float> caps = BuildBaseNeedCaps(interior);

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            FlagshipRoomDefinition definition = room != null ? GetRoomDefinition(room.roomId) : null;
            if (definition == null) continue;

            for (int j = 0; j < definition.needMaxBonuses.Count; j++)
            {
                FlagshipNeedRate bonus = definition.needMaxBonuses[j];
                if (bonus == null || string.IsNullOrWhiteSpace(bonus.needId) || bonus.amount <= 0f) continue;
                caps[bonus.needId] = GetDictionaryValue(caps, bonus.needId) + bonus.amount;
            }
        }

        for (int i = 0; i < FlagshipNeedIds.All.Length; i++)
        {
            string needId = FlagshipNeedIds.All[i];
            FlagshipNeedState state = interior.GetNeedState(needId, true);
            float maxValue = Mathf.Max(1f, GetDictionaryValue(caps, needId));
            state.maxValue = maxValue;
            if (!state.initialized)
            {
                state.currentValue = maxValue;
                state.initialized = true;
            }
            else
            {
                state.currentValue = Mathf.Clamp(state.currentValue, 0f, maxValue);
            }
        }
    }

    private static Dictionary<string, float> BuildBaseNeedCaps(FlagshipInteriorState interior)
    {
        int crew = Mathf.Max(1, interior.crewCapacity > 0 ? interior.crewCapacity : GetDefaultCrewForRank(interior.rank));
        int rank = Mathf.Max(MinimumFlagshipRank, interior.rank);
        Dictionary<string, float> result = new Dictionary<string, float>();
        result[FlagshipNeedIds.Stamina] = crew * 10f;
        result[FlagshipNeedIds.Health] = crew * 10f;
        result[FlagshipNeedIds.Order] = crew * 6f;
        result[FlagshipNeedIds.Comfort] = crew * 6f;
        result[FlagshipNeedIds.Focus] = crew * 4f;
        result[FlagshipNeedIds.Maintenance] = crew * 4f + rank * 30f;
        result[FlagshipNeedIds.Morale] = 100f;
        return result;
    }

    private static int RestoreNeed(WorldConfigDatabase config, PlayerProgress progress, FlagshipInteriorState interior, string needId, float recoveryPoints)
    {
        if (recoveryPoints <= 0f) return 0;

        FlagshipNeedState need = interior.GetNeedState(needId, true);
        if (need == null || need.maxValue <= 0f) return 0;

        float deficit = Mathf.Max(0f, need.maxValue - need.currentValue);
        if (deficit <= 0.001f) return 0;

        need.recoveryCapacityProgress += recoveryPoints;
        int spentItems = 0;
        int guard = 0;
        while (deficit > 0.001f && need.recoveryCapacityProgress > 0.001f && guard < 1000)
        {
            guard++;
            if (need.recoveryItemProgress <= 0.001f)
            {
                float restorePerItem;
                if (!TrySpendNeedRecoveryItem(config, progress, interior, needId, out restorePerItem))
                {
                    break;
                }

                need.recoveryItemProgress += Mathf.Max(1f, restorePerItem);
                spentItems++;
            }

            float restored = Mathf.Min(deficit, need.recoveryCapacityProgress, need.recoveryItemProgress);
            if (restored <= 0.001f) break;

            need.currentValue = Mathf.Min(need.maxValue, need.currentValue + restored);
            need.recoveryCapacityProgress = Mathf.Max(0f, need.recoveryCapacityProgress - restored);
            need.recoveryItemProgress = Mathf.Max(0f, need.recoveryItemProgress - restored);
            deficit = Mathf.Max(0f, need.maxValue - need.currentValue);
        }

        return spentItems;
    }

    private static bool TrySpendNeedRecoveryItem(WorldConfigDatabase config, PlayerProgress progress, FlagshipInteriorState interior, string needId, out float restorePerItem)
    {
        restorePerItem = 0f;
        if (progress == null || string.IsNullOrWhiteSpace(needId)) return false;

        if (needId == FlagshipNeedIds.Morale)
        {
            return false;
        }

        IslandSocialNeedConfig need = config != null ? config.GetIslandSocialNeed(needId) : null;
        string itemId = need != null ? need.recoveryItemId : GetFallbackRecoveryItemId(needId);
        restorePerItem = need != null ? Mathf.Max(1f, need.restorePerItem) : 20f;
        return !string.IsNullOrWhiteSpace(itemId) && progress.TrySpendShipCargo(itemId, 1);
    }

    private static string GetFallbackRecoveryItemId(string needId)
    {
        switch (needId)
        {
            case FlagshipNeedIds.Stamina:
                return "food";
            case FlagshipNeedIds.Health:
                return "medicines";
            case FlagshipNeedIds.Order:
                return "weapon";
            case FlagshipNeedIds.Comfort:
                return "cloth";
            case FlagshipNeedIds.Focus:
                return "paper";
            case FlagshipNeedIds.Maintenance:
                return "tools";
            default:
                return "";
        }
    }

    private static void DrainNeed(FlagshipInteriorState interior, string needId, float amount)
    {
        if (interior == null || amount <= 0f || string.IsNullOrWhiteSpace(needId)) return;

        FlagshipNeedState state = interior.GetNeedState(needId, true);
        if (state == null) return;

        if (!state.initialized)
        {
            state.currentValue = Mathf.Max(1f, state.maxValue);
            state.initialized = true;
        }

        state.currentValue = Mathf.Max(0f, state.currentValue - amount);
    }

    private static void ApplyFailureNeedDrains(FlagshipInteriorState interior, float deltaHours)
    {
        if (interior.rooms == null) return;

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            if (room == null || room.failures == null) continue;

            for (int j = 0; j < room.failures.Count; j++)
            {
                FlagshipFailureState failure = room.failures[j];
                if (failure == null || failure.effectKind != FlagshipFailureEffectKind.NeedDrain) continue;

                string needId = string.IsNullOrWhiteSpace(failure.extraNeedId) ? FlagshipNeedIds.Maintenance : failure.extraNeedId;
                DrainNeed(interior, needId, failure.effectAmount * deltaHours);
            }
        }
    }

    private static List<FlagshipRepairProvider> BuildRepairProviders(FlagshipInteriorState interior)
    {
        List<FlagshipRepairProvider> providers = new List<FlagshipRepairProvider>();
        if (interior.rooms == null) return providers;

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            FlagshipRoomDefinition definition = room != null ? GetRoomDefinition(room.roomId) : null;
            if (room == null || definition == null || definition.repairSlots <= 0 || room.mode == FlagshipRoomMode.Off) continue;

            float needFactor = GetRoomNeedFactor(interior, definition);
            float repairPenalty = GetRepairPenalty(room);
            float repairPower = definition.repairPowerPerHour * GetModeOutputScale(room.mode) * needFactor * repairPenalty;
            if (repairPower <= 0.001f) continue;

            for (int slot = 0; slot < definition.repairSlots; slot++)
            {
                providers.Add(new FlagshipRepairProvider
                {
                    room = room,
                    repairPowerPerHour = repairPower
                });
            }
        }

        return providers;
    }

    private static List<FlagshipFailureTarget> CollectFailureTargets(FlagshipInteriorState interior)
    {
        List<FlagshipFailureTarget> targets = new List<FlagshipFailureTarget>();
        if (interior.rooms == null) return targets;

        for (int i = 0; i < interior.rooms.Count; i++)
        {
            FlagshipRoomState room = interior.rooms[i];
            if (room == null || room.failures == null) continue;

            for (int j = 0; j < room.failures.Count; j++)
            {
                FlagshipFailureState failure = room.failures[j];
                if (failure == null) continue;

                targets.Add(new FlagshipFailureTarget
                {
                    room = room,
                    failure = failure
                });
            }
        }

        return targets;
    }

    private static int CompareFailurePriority(FlagshipFailureTarget a, FlagshipFailureTarget b)
    {
        int severityCompare = b.failure.severity.CompareTo(a.failure.severity);
        if (severityCompare != 0) return severityCompare;
        return a.failure.createdUtcTicks.CompareTo(b.failure.createdUtcTicks);
    }

    private static float GetRoomEffectiveOutput01(FlagshipInteriorState interior, FlagshipRoomState room, FlagshipRoomDefinition definition)
    {
        if (interior == null || room == null || definition == null || room.mode == FlagshipRoomMode.Off) return 0f;

        float output = GetModeOutputScale(room.mode) * GetRoomNeedFactor(interior, definition);
        if (room.failures != null)
        {
            for (int i = 0; i < room.failures.Count; i++)
            {
                FlagshipFailureState failure = room.failures[i];
                if (failure == null) continue;

                if (failure.effectKind == FlagshipFailureEffectKind.EfficiencyPenalty ||
                    failure.effectKind == FlagshipFailureEffectKind.RepairPenalty ||
                    failure.effectKind == FlagshipFailureEffectKind.Shutdown)
                {
                    output *= Mathf.Clamp01(1f - failure.effectAmount);
                }
            }
        }

        return Mathf.Clamp01(output);
    }

    private static float GetRoomNeedFactor(FlagshipInteriorState interior, FlagshipRoomDefinition definition)
    {
        if (interior == null || definition == null) return 1f;
        if (definition.needLoads == null || definition.needLoads.Count == 0) return GetMoraleEfficiencyFactor(interior);

        float factor = 1f;
        for (int i = 0; i < definition.needLoads.Count; i++)
        {
            FlagshipNeedRate load = definition.needLoads[i];
            if (load == null || string.IsNullOrWhiteSpace(load.needId) || load.amount <= 0f) continue;

            FlagshipNeedState need = interior.GetNeedState(load.needId, false);
            if (need == null || need.maxValue <= 0f) continue;

            factor = Mathf.Min(factor, Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(need.currentValue / need.maxValue)));
        }

        factor = Mathf.Min(factor, GetMoraleEfficiencyFactor(interior));
        return Mathf.Clamp01(factor);
    }

    private static float GetMoraleEfficiencyFactor(FlagshipInteriorState interior)
    {
        FlagshipNeedState morale = interior.GetNeedState(FlagshipNeedIds.Morale, false);
        if (morale == null || morale.maxValue <= 0f)
        {
            return 1f;
        }

        return Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(morale.currentValue / morale.maxValue));
    }

    private static float GetRepairPenalty(FlagshipRoomState room)
    {
        if (room == null || room.failures == null) return 1f;

        float factor = 1f;
        for (int i = 0; i < room.failures.Count; i++)
        {
            FlagshipFailureState failure = room.failures[i];
            if (failure != null && failure.effectKind == FlagshipFailureEffectKind.RepairPenalty)
            {
                factor *= Mathf.Clamp01(1f - failure.effectAmount);
            }
        }

        return Mathf.Clamp01(factor);
    }

    private static FlagshipFailureState AddFailureFromTemplate(FlagshipInteriorState interior, FlagshipRoomState room, FlagshipFailureTemplate template, long utcTicks)
    {
        if (interior == null || room == null || template == null) return null;

        int sequence = Mathf.Max(1, interior.nextFailureSequence++);
        FlagshipFailureState failure = new FlagshipFailureState
        {
            failureId = room.roomId + "_" + template.failureKey + "_" + sequence,
            roomId = room.roomId,
            displayName = string.IsNullOrWhiteSpace(template.displayName) ? template.failureKey : template.displayName,
            severity = template.severity,
            effectKind = template.effectKind,
            extraNeedId = template.extraNeedId ?? "",
            effectAmount = Mathf.Max(0f, template.effectAmount),
            repairWorkRequired = Mathf.Max(1f, template.repairWorkRequired),
            createdUtcTicks = utcTicks
        };

        room.failures.Add(failure);
        interior.lastMessage = "Failure started: " + failure.displayName + ".";
        return failure;
    }

    private static FlagshipFailureTemplate PickFailureTemplate(FlagshipRoomDefinition definition, FlagshipInteriorState interior, FlagshipRoomState room, long utcTicks)
    {
        if (definition == null || definition.failures == null || definition.failures.Count == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < definition.failures.Count; i++)
        {
            FlagshipFailureTemplate template = definition.failures[i];
            if (template == null) continue;
            totalWeight += Mathf.Max(0f, template.weight);
        }

        if (totalWeight <= 0f) return definition.failures[0];

        float roll = Hash01(interior.flagshipId, room.roomId, interior.nextFailureSequence, utcTicks) * totalWeight;
        for (int i = 0; i < definition.failures.Count; i++)
        {
            FlagshipFailureTemplate template = definition.failures[i];
            if (template == null) continue;

            roll -= Mathf.Max(0f, template.weight);
            if (roll <= 0f) return template;
        }

        return definition.failures[definition.failures.Count - 1];
    }

    private static FlagshipInteriorState EnsurePlayerFlagshipInteriorInternal(WorldConfigDatabase config, PlayerProgress progress)
    {
        if (config == null || progress == null || string.IsNullOrWhiteSpace(progress.selectedHullId)) return null;
        if (!TryResolveShipRankForHull(config, progress.selectedHullId, out int rank) || !IsFlagshipRank(rank)) return null;

        FlagshipInteriorState interior = progress.GetFlagshipInteriorState(PlayerFlagshipId, true);
        interior.flagshipId = PlayerFlagshipId;
        interior.hullId = progress.selectedHullId;
        interior.rank = rank;
        if (interior.crewCapacity <= 0)
        {
            interior.crewCapacity = GetDefaultCrewForRank(rank);
        }

        EnsureDefaultInterior(interior);
        return interior;
    }

    private static bool TryResolveShipRankForHull(WorldConfigDatabase config, string hullId, out int rank)
    {
        rank = 0;
        if (config == null || string.IsNullOrWhiteSpace(hullId) || config.shipTreeEntries == null) return false;

        for (int i = 0; i < config.shipTreeEntries.Count; i++)
        {
            ShipTreeEntryConfig entry = config.shipTreeEntries[i];
            if (entry == null) continue;

            if (entry.hullId == hullId || Contains(entry.upgradeHullIds, hullId))
            {
                rank = entry.rank;
                return true;
            }
        }

        return false;
    }

    private static bool Contains(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value)) return false;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == value) return true;
        }
        return false;
    }

    private static int GetDefaultCrewForRank(int rank)
    {
        switch (rank)
        {
            case 3:
                return 6;
            case 4:
                return 15;
            case 5:
                return 40;
            case 6:
                return 120;
            case 7:
                return 350;
            case 8:
                return 1000;
            default:
                return Mathf.Max(1, rank * 2);
        }
    }

    private static FlagshipRoomDefinition GetRoomDefinition(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return null;
        for (int i = 0; i < defaultRoomDefinitions.Count; i++)
        {
            FlagshipRoomDefinition definition = defaultRoomDefinitions[i];
            if (definition != null && definition.roomId == roomId)
            {
                return definition;
            }
        }

        return null;
    }

    private static float GetModeNeedScale(FlagshipRoomMode mode)
    {
        switch (mode)
        {
            case FlagshipRoomMode.Quarter:
                return 0.25f;
            case FlagshipRoomMode.Half:
                return 0.50f;
            case FlagshipRoomMode.Full:
                return 1f;
            default:
                return 0f;
        }
    }

    private static float GetModeOutputScale(FlagshipRoomMode mode)
    {
        switch (mode)
        {
            case FlagshipRoomMode.Quarter:
                return 0.20f;
            case FlagshipRoomMode.Half:
                return 0.55f;
            case FlagshipRoomMode.Full:
                return 1f;
            default:
                return 0f;
        }
    }

    private static float GetModeStressScale(FlagshipRoomMode mode)
    {
        switch (mode)
        {
            case FlagshipRoomMode.Quarter:
                return 0.35f;
            case FlagshipRoomMode.Half:
                return 0.65f;
            case FlagshipRoomMode.Full:
                return 1f;
            default:
                return 0f;
        }
    }

    private static float GetDictionaryValue(Dictionary<string, float> values, string key)
    {
        if (values == null || string.IsNullOrWhiteSpace(key)) return 0f;
        return values.TryGetValue(key, out float value) ? value : 0f;
    }

    private static float Hash01(string flagshipId, string roomId, int sequence, long ticks)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (flagshipId != null ? flagshipId.GetHashCode() : 0);
            hash = hash * 31 + (roomId != null ? roomId.GetHashCode() : 0);
            hash = hash * 31 + sequence;
            hash = hash * 31 + ticks.GetHashCode();
            uint positive = (uint)hash;
            return (positive % 10000u) / 10000f;
        }
    }

    private static List<FlagshipRoomDefinition> BuildDefaultRoomDefinitions()
    {
        List<FlagshipRoomDefinition> result = new List<FlagshipRoomDefinition>();

        result.Add(Room("bridge", "Command bridge", FlagshipRoomKind.Command, 3, 0.045f)
            .Load(FlagshipNeedIds.Focus, 0.8f)
            .Load(FlagshipNeedIds.Order, 0.4f)
            .Load(FlagshipNeedIds.Maintenance, 0.5f)
            .Failure("gyro_drift", "Gyro drift", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.ControlSpin, "", 0.35f, 70f, 2f)
            .Failure("relay_blackout", "Command relay blackout", FlagshipFailureSeverity.Major, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.45f, 130f, 1f));

        result.Add(Room("engine_room", "Engine room", FlagshipRoomKind.Engine, 3, 0.080f)
            .Load(FlagshipNeedIds.Stamina, 1.2f)
            .Load(FlagshipNeedIds.Order, 0.4f)
            .Load(FlagshipNeedIds.Maintenance, 1.0f)
            .Failure("steam_leak", "Steam leak", FlagshipFailureSeverity.Minor, FlagshipFailureEffectKind.NeedDrain, FlagshipNeedIds.Maintenance, 4f, 30f, 3f)
            .Failure("shaft_alignment", "Shaft alignment failure", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.25f, 80f, 2f)
            .Failure("lift_coupling", "Lift coupling failure", FlagshipFailureSeverity.Critical, FlagshipFailureEffectKind.ForcedDescent, "", 0.65f, 240f, 0.45f));

        result.Add(Room("mess_hall", "Mess hall", FlagshipRoomKind.Mess, 3, 0.030f)
            .Load(FlagshipNeedIds.Maintenance, 0.2f)
            .Recover(FlagshipNeedIds.Stamina, 14f)
            .Recover(FlagshipNeedIds.Comfort, 2f)
            .Max(FlagshipNeedIds.Stamina, 30f)
            .Failure("galley_clog", "Galley clog", FlagshipFailureSeverity.Minor, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.25f, 25f, 2f)
            .Failure("ration_spoilage", "Ration spoilage", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.NeedDrain, FlagshipNeedIds.Comfort, 3f, 55f, 1f));

        result.Add(Room("crew_quarters", "Crew quarters", FlagshipRoomKind.Quarters, 3, 0.025f)
            .Load(FlagshipNeedIds.Maintenance, 0.2f)
            .Recover(FlagshipNeedIds.Comfort, 4f)
            .Recover(FlagshipNeedIds.Stamina, 1f)
            .Max(FlagshipNeedIds.Stamina, 40f)
            .Max(FlagshipNeedIds.Comfort, 55f)
            .Failure("berth_damage", "Berth damage", FlagshipFailureSeverity.Minor, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.20f, 25f, 1f));

        result.Add(Room("repair_workshop", "Repair workshop", FlagshipRoomKind.Repair, 3, 0.055f)
            .Load(FlagshipNeedIds.Stamina, 0.7f)
            .Load(FlagshipNeedIds.Focus, 0.3f)
            .Recover(FlagshipNeedIds.Maintenance, 8f)
            .Repair(1, 60f)
            .Max(FlagshipNeedIds.Maintenance, 60f)
            .Failure("tool_lift_jam", "Tool lift jam", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.RepairPenalty, "", 0.45f, 70f, 2f)
            .Failure("parts_sorter", "Parts sorter failure", FlagshipFailureSeverity.Minor, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.25f, 35f, 1f));

        result.Add(Room("radio_room", "Signal room", FlagshipRoomKind.Communications, 3, 0.035f)
            .Load(FlagshipNeedIds.Focus, 0.5f)
            .Load(FlagshipNeedIds.Maintenance, 0.3f)
            .Recover(FlagshipNeedIds.Focus, 1f)
            .Failure("signal_backlog", "Signal backlog", FlagshipFailureSeverity.Minor, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.35f, 30f, 1f));

        result.Add(Room("medical_bay", "Medical bay", FlagshipRoomKind.Medical, 4, 0.040f)
            .Load(FlagshipNeedIds.Stamina, 0.4f)
            .Load(FlagshipNeedIds.Focus, 0.3f)
            .Recover(FlagshipNeedIds.Health, 10f)
            .Max(FlagshipNeedIds.Health, 50f)
            .Failure("sterilizer_fault", "Sterilizer fault", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.45f, 70f, 1f));

        result.Add(Room("security_post", "Security post", FlagshipRoomKind.Security, 4, 0.040f)
            .Load(FlagshipNeedIds.Stamina, 0.5f)
            .Load(FlagshipNeedIds.Focus, 0.2f)
            .Recover(FlagshipNeedIds.Order, 7f)
            .Max(FlagshipNeedIds.Order, 45f)
            .Failure("watch_confusion", "Watch confusion", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.NeedDrain, FlagshipNeedIds.Order, 4f, 65f, 1f));

        result.Add(Room("factory_deck", "Factory deck", FlagshipRoomKind.Production, 5, 0.095f)
            .Load(FlagshipNeedIds.Stamina, 2.2f)
            .Load(FlagshipNeedIds.Order, 1.0f)
            .Load(FlagshipNeedIds.Focus, 0.8f)
            .Load(FlagshipNeedIds.Maintenance, 1.7f)
            .Failure("line_desync", "Production line desync", FlagshipFailureSeverity.Medium, FlagshipFailureEffectKind.EfficiencyPenalty, "", 0.35f, 90f, 2f)
            .Failure("industrial_fire", "Industrial fire", FlagshipFailureSeverity.Critical, FlagshipFailureEffectKind.Shutdown, "", 0.90f, 260f, 0.5f));

        return result;
    }

    private static FlagshipRoomBuilder Room(string id, string displayName, FlagshipRoomKind kind, int minRank, float faultsPerHour)
    {
        return new FlagshipRoomBuilder(new FlagshipRoomDefinition
        {
            roomId = id,
            displayName = displayName,
            kind = kind,
            minRank = minRank,
            baseFaultsPerHour = faultsPerHour
        });
    }

    private struct FlagshipRepairProvider
    {
        public FlagshipRoomState room;
        public float repairPowerPerHour;
    }

    private struct FlagshipFailureTarget
    {
        public FlagshipRoomState room;
        public FlagshipFailureState failure;
    }

    private sealed class FlagshipRoomBuilder
    {
        private readonly FlagshipRoomDefinition definition;

        public FlagshipRoomBuilder(FlagshipRoomDefinition definition)
        {
            this.definition = definition;
        }

        public FlagshipRoomBuilder Load(string needId, float amount)
        {
            definition.needLoads.Add(new FlagshipNeedRate(needId, amount));
            return this;
        }

        public FlagshipRoomBuilder Recover(string needId, float amount)
        {
            definition.needRecovery.Add(new FlagshipNeedRate(needId, amount));
            return this;
        }

        public FlagshipRoomBuilder Max(string needId, float amount)
        {
            definition.needMaxBonuses.Add(new FlagshipNeedRate(needId, amount));
            return this;
        }

        public FlagshipRoomBuilder Repair(int slots, float powerPerHour)
        {
            definition.repairSlots = Mathf.Max(0, slots);
            definition.repairPowerPerHour = Mathf.Max(0f, powerPerHour);
            return this;
        }

        public FlagshipRoomBuilder Failure(
            string key,
            string displayName,
            FlagshipFailureSeverity severity,
            FlagshipFailureEffectKind effectKind,
            string extraNeedId,
            float effectAmount,
            float repairWork,
            float weight)
        {
            definition.failures.Add(new FlagshipFailureTemplate
            {
                failureKey = key,
                displayName = displayName,
                severity = severity,
                effectKind = effectKind,
                extraNeedId = extraNeedId,
                effectAmount = effectAmount,
                repairWorkRequired = repairWork,
                weight = weight
            });
            return this;
        }

        public static implicit operator FlagshipRoomDefinition(FlagshipRoomBuilder builder)
        {
            return builder.definition;
        }
    }
}
