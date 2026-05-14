using System.Collections.Generic;
using UnityEngine;

public class ShipAssemblyResult
{
    public bool isValid;
    public string message = "";
    public ShipPartDefinitionSO hull;
    public List<ShipSlotDefinition> slots = new List<ShipSlotDefinition>();
    public List<InstalledModuleState> installedModules = new List<InstalledModuleState>();
    public ShipStatBlock stats = new ShipStatBlock();
}

public static class ShipAssemblyBuilder
{
    public static bool TryBuild(ShipCatalogSO catalog, TechTreeDefinitionSO techTree, PlayerProgress progress, out ShipAssemblyResult result)
    {
        result = new ShipAssemblyResult();
        if (catalog == null)
        {
            result.message = "Каталог деталей не задан.";
            return false;
        }

        if (progress == null)
        {
            result.message = "Прогресс игрока не задан.";
            return false;
        }

        progress.Normalize();
        ShipPartDefinitionSO hull = catalog.GetPartById(progress.selectedHullId);
        if (hull == null || !hull.IsHull)
        {
            hull = catalog.GetStarterHull();
        }

        if (hull == null)
        {
            result.message = "Корпус не выбран или не найден.";
            return false;
        }

        if (!IsPartUsable(hull, techTree, progress))
        {
            result.message = "Корпус еще не куплен.";
            return false;
        }

        result.hull = hull;
        AddSlots(result.slots, hull.slots, "");
        if (!ValidateSlotIds(result.slots, out result.message))
        {
            return false;
        }

        if (!result.stats.ApplyPart(hull, out result.message))
        {
            return false;
        }

        for (int i = 0; i < result.slots.Count; i++)
        {
            ShipSlotDefinition slot = result.slots[i];
            string moduleId = progress.GetInstalledModule(slot.slotId);
            ShipPartDefinitionSO module = catalog.GetPartById(moduleId);

            if (module == null)
            {
                if (slot.required)
                {
                    result.message = "Не заполнен обязательный слот: " + slot.displayName;
                    return false;
                }

                continue;
            }

            if (!module.IsModule || !module.CanFitSlot(slot))
            {
                result.message = "Модуль не подходит в слот: " + module.displayName + " -> " + slot.displayName;
                return false;
            }

            if (!IsPartUsable(module, techTree, progress))
            {
                result.message = "Модуль еще не куплен: " + module.displayName;
                return false;
            }

            result.installedModules.Add(new InstalledModuleState { slotId = slot.slotId, moduleId = module.partId });
            if (!result.stats.ApplyPart(module, out result.message))
            {
                return false;
            }

            AddSlots(result.slots, module.grantedSlots, slot.slotId + ":" + module.partId + ":");
            if (!ValidateSlotIds(result.slots, out result.message))
            {
                return false;
            }
        }

        result.isValid = true;
        result.message = "Корабль собран.";
        return true;
    }

    public static bool AutoInstallRequiredModules(ShipCatalogSO catalog, TechTreeDefinitionSO techTree, PlayerProgress progress, out string message)
    {
        message = "";
        if (catalog == null || progress == null)
        {
            message = "Нет каталога или прогресса для сборки.";
            return false;
        }

        progress.Normalize();
        ShipPartDefinitionSO hull = catalog.GetPartById(progress.selectedHullId);
        if (hull == null || !hull.IsHull)
        {
            hull = catalog.GetStarterHull();
        }

        if (hull == null)
        {
            message = "Корпус не выбран.";
            return false;
        }

        List<ShipSlotDefinition> slots = new List<ShipSlotDefinition>();
        AddSlots(slots, hull.slots, "");

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            ShipSlotDefinition slot = slots[i];
            if (slot == null) continue;

            string installedId = progress.GetInstalledModule(slot.slotId);
            ShipPartDefinitionSO installedModule = catalog.GetPartById(installedId);
            bool installedIsValid = installedModule != null
                && installedModule.IsModule
                && installedModule.CanFitSlot(slot)
                && IsPartUsable(installedModule, techTree, progress);

            if (!installedIsValid && !string.IsNullOrWhiteSpace(installedId))
            {
                progress.InstallModule(slot.slotId, "");
                changed = true;
            }

            if (!installedIsValid && slot.required)
            {
                ShipPartDefinitionSO replacement = FindFirstAvailableModule(catalog, techTree, progress, slot);
                if (replacement == null)
                {
                    message = "Нет купленного подходящего модуля для обязательного слота: " + slot.displayName;
                    return false;
                }

                progress.InstallModule(slot.slotId, replacement.partId);
                installedModule = replacement;
                changed = true;
            }

            if (installedModule != null && installedModule.IsModule && installedModule.CanFitSlot(slot))
            {
                AddSlots(slots, installedModule.grantedSlots, slot.slotId + ":" + installedModule.partId + ":");
            }
        }

        message = changed ? "Обязательные слоты заполнены." : "Обязательные слоты уже заполнены.";
        return true;
    }

    public static bool IsPartUsable(ShipPartDefinitionSO part, TechTreeDefinitionSO techTree, PlayerProgress progress)
    {
        if (part == null || progress == null) return false;

        if (!string.IsNullOrWhiteSpace(part.completedTechId))
        {
            return progress.IsTechnologyCompleted(part.completedTechId);
        }

        return true;
    }

    public static ShipPartDefinitionSO FindFirstAvailableModule(ShipCatalogSO catalog, TechTreeDefinitionSO techTree, PlayerProgress progress, ShipSlotDefinition slot)
    {
        if (catalog == null || slot == null || catalog.parts == null) return null;

        for (int i = 0; i < catalog.parts.Count; i++)
        {
            ShipPartDefinitionSO part = catalog.parts[i];
            if (part != null && part.IsModule && part.CanFitSlot(slot) && IsPartUsable(part, techTree, progress))
            {
                return part;
            }
        }

        return null;
    }

    private static void AddSlots(List<ShipSlotDefinition> target, List<ShipSlotDefinition> source, string prefix)
    {
        if (target == null || source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            ShipSlotDefinition slot = source[i];
            if (slot == null) continue;

            string slotId = string.IsNullOrWhiteSpace(slot.slotId) ? "slot_" + i : slot.slotId;
            target.Add(slot.CloneWithId(prefix + slotId));
        }
    }

    private static bool ValidateSlotIds(List<ShipSlotDefinition> slots, out string error)
    {
        error = "";
        HashSet<string> ids = new HashSet<string>();

        for (int i = 0; i < slots.Count; i++)
        {
            ShipSlotDefinition slot = slots[i];
            if (slot == null) continue;

            if (string.IsNullOrWhiteSpace(slot.slotId))
            {
                error = "У слота нет идентификатора.";
                return false;
            }

            if (!ids.Add(slot.slotId))
            {
                error = "Повторяется идентификатор слота: " + slot.slotId;
                return false;
            }
        }

        return true;
    }
}

public class ShipStatBlock
{
    private readonly Dictionary<ShipStatId, float> setValues = new Dictionary<ShipStatId, float>();
    private readonly Dictionary<ShipStatId, float> addValues = new Dictionary<ShipStatId, float>();
    private readonly Dictionary<ShipStatId, float> multiplyValues = new Dictionary<ShipStatId, float>();
    private readonly HashSet<ShipStatId> setStats = new HashSet<ShipStatId>();
    private string engineFuelId = "";

    public string EngineFuelId => engineFuelId;

    public bool ApplyPart(ShipPartDefinitionSO part, out string error)
    {
        error = "";
        if (part == null) return true;

        if (!string.IsNullOrWhiteSpace(part.engineFuelId))
        {
            if (!string.IsNullOrWhiteSpace(engineFuelId) && engineFuelId != part.engineFuelId)
            {
                error = "Тип топлива двигателя задан несколькими деталями: " + engineFuelId + " и " + part.engineFuelId;
                return false;
            }

            engineFuelId = part.engineFuelId;
        }

        if (part.statModifiers == null) return true;

        for (int i = 0; i < part.statModifiers.Count; i++)
        {
            ShipStatModifier modifier = part.statModifiers[i];
            if (modifier == null) continue;

            if (modifier.operation == ShipStatOperation.Set)
            {
                if (setStats.Contains(modifier.stat))
                {
                    error = "Характеристика перезаписана двумя деталями: " + modifier.stat;
                    return false;
                }

                setValues[modifier.stat] = modifier.value;
                setStats.Add(modifier.stat);
            }
            else if (modifier.operation == ShipStatOperation.Add)
            {
                float current = addValues.TryGetValue(modifier.stat, out float value) ? value : 0f;
                addValues[modifier.stat] = current + modifier.value;
            }
            else if (modifier.operation == ShipStatOperation.Multiply)
            {
                float current = multiplyValues.TryGetValue(modifier.stat, out float value) ? value : 1f;
                multiplyValues[modifier.stat] = current * modifier.value;
            }
        }

        return true;
    }

    public float Get(ShipStatId stat, float fallback = 0f)
    {
        float value = setValues.TryGetValue(stat, out float setValue) ? setValue : fallback;
        if (addValues.TryGetValue(stat, out float addValue))
        {
            value += addValue;
        }

        if (multiplyValues.TryGetValue(stat, out float multiplyValue))
        {
            value *= multiplyValue;
        }

        return value;
    }

    public void ApplyTo(ShipPhysics ship)
    {
        if (ship == null) return;

        ship.baseMass = Mathf.Max(1f, Get(ShipStatId.BaseMass, ship.baseMass));
        ship.hullMaxTakeoffMassKg = Mathf.Max(1f, Get(ShipStatId.HullMaxTakeoffMassKg, ship.hullMaxTakeoffMassKg));
        ship.targetTrimMass = Mathf.Max(1f, Get(ShipStatId.TargetTrimMass, ship.baseMass));
        ship.propellerMaxSpeedMS = Mathf.Max(0f, Get(ShipStatId.PropellerMaxSpeedMS, ship.propellerMaxSpeedMS));
        ship.propellerEfficiency = Mathf.Max(0f, Get(ShipStatId.PropellerEfficiency, 0f));
        ship.propellerMaxThrustKgf = Mathf.Max(0f, Get(ShipStatId.PropellerMaxThrustKgf, ship.propellerMaxThrustKgf));
        ship.airDensity = Mathf.Max(0.01f, Get(ShipStatId.AirDensity, 1.225f));
        ship.dragCoefficient = Mathf.Max(0f, Get(ShipStatId.DragCoefficient, 0f));
        ship.frontalArea = Mathf.Max(0f, Get(ShipStatId.FrontalArea, 0f));
        ship.sideResistance = Mathf.Max(0f, Get(ShipStatId.SideResistance, 0f));
        ship.verticalAreaFactor = Mathf.Max(0f, Get(ShipStatId.VerticalAreaFactor, 0f));
        ship.gyroTurnTorque = Mathf.Max(0f, Get(ShipStatId.GyroTurnTorque, ship.gyroTurnTorque));
        ship.gyroTurnDamping = Mathf.Max(0f, Get(ShipStatId.GyroTurnDamping, ship.gyroTurnDamping));
        ship.maxAutoTurnRateDeg = Mathf.Max(0f, Get(ShipStatId.MaxAutoTurnRateDeg, 0f));
        ship.maxStructuralTurnRateDeg = Mathf.Max(0f, Get(ShipStatId.MaxStructuralTurnRateDeg, 0f));
        ship.maxStructuralVerticalSpeed = Mathf.Max(0.1f, Get(ShipStatId.MaxStructuralVerticalSpeed, 1f));
        ship.maxAutoVerticalSpeed = Mathf.Max(0.1f, Get(ShipStatId.MaxAutoVerticalSpeed, 1f));
        ship.altStiffness = Mathf.Max(0f, Get(ShipStatId.AltitudeStiffness, ship.altStiffness));
        ship.altDamping = Mathf.Max(0f, Get(ShipStatId.AltitudeDamping, ship.altDamping));
        ship.altDriftTolerance = Mathf.Max(0f, Get(ShipStatId.AltitudeDriftTolerance, ship.altDriftTolerance));
        ship.headingStiffness = Mathf.Max(0f, Get(ShipStatId.HeadingStiffness, ship.headingStiffness));
        ship.headingDamping = Mathf.Max(0f, Get(ShipStatId.HeadingDamping, ship.headingDamping));
        ship.waypointRadius = Mathf.Max(0.1f, Get(ShipStatId.WaypointRadius, ship.waypointRadius));
        ship.speedStiffness = Mathf.Max(0f, Get(ShipStatId.SpeedStiffness, ship.speedStiffness));
        ship.speedDamping = Mathf.Max(0f, Get(ShipStatId.SpeedDamping, ship.speedDamping));
        ship.claudiumConsumptionPerTonSecond = Mathf.Max(0f, Get(ShipStatId.ClaudiumConsumptionPerTonSecond, 0f));
        ship.claudiumLiftEfficiency = Mathf.Max(0f, Get(ShipStatId.ClaudiumLiftEfficiency, 0f));
        ship.claudiumMaxLiftKg = Mathf.Max(0f, Get(ShipStatId.ClaudiumMaxLiftKg, 0f));
        ship.claudiumLiftSmoothing = Mathf.Max(0f, Get(ShipStatId.ClaudiumLiftSmoothing, 0f));

        if (!string.IsNullOrWhiteSpace(engineFuelId))
        {
            ship.engineFuelId = engineFuelId;
        }

        ship.enginePowerKwAt100 = Mathf.Max(0f, Get(ShipStatId.EngineMaxPower, ship.enginePowerKwAt100));
        ship.engineFuelEfficiency = Mathf.Clamp(Get(ShipStatId.EngineFuelEfficiency, ship.engineFuelEfficiency), 0.01f, 0.95f);
        ship.gasHarvesterVolumeM3PerSecond = Mathf.Max(0f, Get(ShipStatId.GasHarvesterVolumeM3PerSecond, 0f));
        ship.gasHarvesterPowerDrawKw = Mathf.Max(0f, Get(ShipStatId.GasHarvesterPowerDrawKw, 0f));
        ship.gasHarvesterRadiusMeters = Mathf.Max(0f, Get(ShipStatId.GasHarvesterRadiusMeters, 0f));
        ship.gasHarvesterCycleSeconds = Mathf.Max(0.1f, Get(ShipStatId.GasHarvesterCycleSeconds, 5f));

        Rigidbody body = ship.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.mass = ship.GetTotalMassKg();
        }

        ship.RefreshRuntimeShipSettings();
    }
}
