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

    public static int ApplyCsvShipPartConfigs(ShipCatalogSO catalog, WorldConfigDatabase config)
    {
        if (catalog == null || config == null) return 0;
        catalog.parts ??= new List<ShipPartDefinitionSO>();

        int applied = 0;

        if (config.hulls != null)
        {
            for (int i = 0; i < config.hulls.Count; i++)
            {
                HullConfig hullConfig = config.hulls[i];
                if (hullConfig == null || string.IsNullOrWhiteSpace(hullConfig.id)) continue;

                ShipPartDefinitionSO part = GetOrCreateCatalogPart(catalog, hullConfig.id, ShipPartKind.Hull);
                part.displayName = hullConfig.DisplayNameRu;
                part.description = "CSV hull config.";
                part.completedTechId = hullConfig.completedTechId ?? "";
                part.engineFuelId = "";
                part.allowedCargoItemIds = new List<string>();
                part.compatibleSlotTypeIds = new List<string>();
                part.grantedSlots = new List<ShipSlotDefinition>();
                part.slots = BuildHullSlots(hullConfig, config);
                part.statModifiers = BuildHullStatModifiers(hullConfig);
                applied++;
            }
        }

        if (config.engines != null)
        {
            for (int i = 0; i < config.engines.Count; i++)
            {
                EngineConfig engineConfig = config.engines[i];
                if (engineConfig == null || string.IsNullOrWhiteSpace(engineConfig.id)) continue;

                ShipPartDefinitionSO part = GetOrCreateCatalogPart(catalog, engineConfig.id, ShipPartKind.Module);
                part.displayName = engineConfig.DisplayNameRu;
                part.description = "CSV engine config.";
                part.completedTechId = engineConfig.completedTechId ?? "";
                part.engineFuelId = engineConfig.fuelId ?? "";
                part.allowedCargoItemIds = new List<string>();
                part.slots = new List<ShipSlotDefinition>();
                part.compatibleSlotTypeIds = new List<string> { "engine_main" };
                part.grantedSlots = new List<ShipSlotDefinition>();
                part.statModifiers = BuildEngineStatModifiers(engineConfig);
                applied++;
            }
        }

        if (config.propellers != null)
        {
            for (int i = 0; i < config.propellers.Count; i++)
            {
                PropellerConfig propellerConfig = config.propellers[i];
                if (propellerConfig == null || string.IsNullOrWhiteSpace(propellerConfig.id)) continue;

                ShipPartDefinitionSO part = GetOrCreateCatalogPart(catalog, propellerConfig.id, ShipPartKind.Module);
                part.displayName = propellerConfig.DisplayNameRu;
                part.description = "CSV propeller config.";
                part.completedTechId = propellerConfig.completedTechId ?? "";
                part.engineFuelId = "";
                part.allowedCargoItemIds = new List<string>();
                part.slots = new List<ShipSlotDefinition>();
                part.compatibleSlotTypeIds = new List<string> { "propeller_main" };
                part.grantedSlots = new List<ShipSlotDefinition>();
                part.statModifiers = BuildPropellerStatModifiers(propellerConfig);
                applied++;
            }
        }

        if (config.claudiumLoops != null)
        {
            for (int i = 0; i < config.claudiumLoops.Count; i++)
            {
                ClaudiumLoopConfig loopConfig = config.claudiumLoops[i];
                if (loopConfig == null || string.IsNullOrWhiteSpace(loopConfig.id)) continue;

                ShipPartDefinitionSO part = GetOrCreateCatalogPart(catalog, loopConfig.id, ShipPartKind.Module);
                part.displayName = loopConfig.DisplayNameRu;
                part.description = "CSV claudium loop config.";
                part.completedTechId = loopConfig.completedTechId ?? "";
                part.engineFuelId = "";
                part.allowedCargoItemIds = new List<string>();
                part.slots = new List<ShipSlotDefinition>();
                part.compatibleSlotTypeIds = new List<string> { "claudium_loop" };
                part.grantedSlots = new List<ShipSlotDefinition>();
                part.statModifiers = BuildClaudiumLoopStatModifiers(loopConfig);
                applied++;
            }
        }

        applied += ApplySpecialModuleConfigs(catalog, config);
        return applied;
    }

    public static int ApplySpecialModuleConfigs(ShipCatalogSO catalog, WorldConfigDatabase config)
    {
        if (catalog == null || config == null || config.specialModules == null) return 0;
        catalog.parts ??= new List<ShipPartDefinitionSO>();

        int applied = 0;
        for (int i = 0; i < config.specialModules.Count; i++)
        {
            SpecialModuleConfig moduleConfig = config.specialModules[i];
            if (moduleConfig == null || string.IsNullOrWhiteSpace(moduleConfig.id)) continue;

            ShipPartDefinitionSO part = catalog.GetPartById(moduleConfig.id);
            if (part == null)
            {
                part = GetOrCreateCatalogPart(catalog, moduleConfig.id, ShipPartKind.Module);
            }
            else
            {
                part.kind = ShipPartKind.Module;
            }

            part.displayName = moduleConfig.DisplayNameRu;
            if (!string.IsNullOrWhiteSpace(moduleConfig.descriptionRu))
            {
                part.description = moduleConfig.descriptionRu;
            }

            part.completedTechId = moduleConfig.completedTechId ?? "";
            part.engineFuelId = "";
            part.allowedCargoItemIds = new List<string>();
            if (moduleConfig.allowedCargoItemIds != null)
            {
                part.allowedCargoItemIds.AddRange(moduleConfig.allowedCargoItemIds);
            }

            part.slots = new List<ShipSlotDefinition>();
            part.compatibleSlotTypeIds = new List<string>();
            if (moduleConfig.compatibleSlotTypeIds != null)
            {
                part.compatibleSlotTypeIds.AddRange(moduleConfig.compatibleSlotTypeIds);
            }

            part.grantedSlots = new List<ShipSlotDefinition>();
            part.statModifiers = BuildSpecialModuleStatModifiers(moduleConfig);
            applied++;
        }

        return applied;
    }

    private static ShipPartDefinitionSO GetOrCreateCatalogPart(ShipCatalogSO catalog, string partId, ShipPartKind kind)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(partId)) return null;
        catalog.parts ??= new List<ShipPartDefinitionSO>();

        ShipPartDefinitionSO part = catalog.GetPartById(partId);
        if (part == null)
        {
            part = ScriptableObject.CreateInstance<ShipPartDefinitionSO>();
            part.name = partId;
            part.partId = partId;
            catalog.parts.Add(part);
        }

        part.partId = partId;
        part.kind = kind;
        return part;
    }

    private static List<ShipSlotDefinition> BuildHullSlots(HullConfig hullConfig, WorldConfigDatabase config)
    {
        List<ShipSlotDefinition> slots = new List<ShipSlotDefinition>();
        if (hullConfig == null) return slots;

        if (R1ShipDesignCatalog.TryGetByHullId(hullConfig.id, out R1ShipDesignDefinition r1Design))
        {
            AddDesignSlots(slots, r1Design);
            return slots;
        }

        if (R2TenderDesignCatalog.TryGetByHullId(hullConfig.id, out R1ShipDesignDefinition r2TenderDesign))
        {
            AddDesignSlots(slots, r2TenderDesign);
            return slots;
        }

        if (hullConfig.id == "starter_hull")
        {
            slots.Add(CreateRequiredSlot("engine_main", "Маршевый двигатель", "engine_main", "starter_engine"));
            slots.Add(CreateRequiredSlot("propeller_main", "Винт", "propeller_main", "starter_propeller"));
            slots.Add(CreateRequiredSlot("claudium_loop", "Клавдиевый контур", "claudium_loop", "starter_claudium_loop"));
            slots.Add(new ShipSlotDefinition
            {
                slotId = "utility_01",
                displayName = "Вспомогательный слот",
                slotTypeId = "utility",
                required = false,
                allowedPartIds = new List<string>()
            });
            return slots;
        }

        slots.Add(CreateRequiredSlot("engine_main", "Маршевый двигатель", "engine_main"));
        slots.Add(CreateRequiredSlot("propeller_main", "Винт", "propeller_main"));
        slots.Add(CreateRequiredSlot("claudium_loop", "Клавдиевый контур", "claudium_loop"));
        slots.Add(new ShipSlotDefinition
        {
            slotId = "utility_01",
            displayName = "Вспомогательный слот",
            slotTypeId = "utility",
            required = false,
            allowedPartIds = new List<string>()
        });
        return slots;
    }

    private static void AddDesignSlots(List<ShipSlotDefinition> slots, R1ShipDesignDefinition design)
    {
        if (slots == null || design == null) return;

        slots.Add(CreateRequiredSlot(R1ShipDesignCatalog.EngineSlotId, "Маршевый двигатель", "engine_main", design.GetAllowedEngineIds()));
        slots.Add(CreateRequiredSlot(R1ShipDesignCatalog.PropellerSlotId, "Винт", "propeller_main", design.GetAllowedPropellerIds()));
        slots.Add(CreateRequiredSlot(R1ShipDesignCatalog.ClaudiumLoopSlotId, "Клавдиевый контур", "claudium_loop", design.GetAllowedClaudiumLoopIds()));
        slots.Add(CreateRequiredSlot(R1ShipDesignCatalog.RoleModuleSlotId, "Ролевой модуль", "utility", design.GetAllowedSpecialModuleIds()));
    }

    private static ShipSlotDefinition CreateRequiredSlot(string slotId, string displayName, string slotTypeId, string allowedPartId = "")
    {
        ShipSlotDefinition slot = new ShipSlotDefinition
        {
            slotId = slotId,
            displayName = displayName,
            slotTypeId = slotTypeId,
            required = true,
            allowedPartIds = new List<string>()
        };

        if (!string.IsNullOrWhiteSpace(allowedPartId))
        {
            slot.allowedPartIds.Add(allowedPartId);
        }

        return slot;
    }

    private static ShipSlotDefinition CreateRequiredSlot(string slotId, string displayName, string slotTypeId, List<string> allowedPartIds)
    {
        ShipSlotDefinition slot = CreateRequiredSlot(slotId, displayName, slotTypeId);
        if (allowedPartIds == null) return slot;

        for (int i = 0; i < allowedPartIds.Count; i++)
        {
            string allowedPartId = allowedPartIds[i];
            if (!string.IsNullOrWhiteSpace(allowedPartId) && !slot.allowedPartIds.Contains(allowedPartId))
            {
                slot.allowedPartIds.Add(allowedPartId);
            }
        }

        return slot;
    }

    private static List<ShipStatModifier> BuildHullStatModifiers(HullConfig hullConfig)
    {
        List<ShipStatModifier> modifiers = new List<ShipStatModifier>();
        if (hullConfig == null) return modifiers;

        AddStat(modifiers, ShipStatId.BaseMass, ShipStatOperation.Set, hullConfig.baseMassKg);
        AddStat(modifiers, ShipStatId.HullMaxTakeoffMassKg, ShipStatOperation.Set, hullConfig.hullMaxTakeoffMassKg);
        AddStat(modifiers, ShipStatId.AirDensity, ShipStatOperation.Set, hullConfig.airDensity);
        AddStat(modifiers, ShipStatId.DragCoefficient, ShipStatOperation.Set, hullConfig.dragCoefficient);
        AddStat(modifiers, ShipStatId.FrontalArea, ShipStatOperation.Set, hullConfig.frontalAreaM2);
        AddStat(modifiers, ShipStatId.SideResistance, ShipStatOperation.Set, hullConfig.sideResistance);
        AddStat(modifiers, ShipStatId.VerticalAreaFactor, ShipStatOperation.Set, hullConfig.verticalAreaFactor);
        AddStat(modifiers, ShipStatId.GyroTurnTorque, ShipStatOperation.Set, hullConfig.gyroTurnTorqueNm);
        AddStat(modifiers, ShipStatId.GyroTurnDamping, ShipStatOperation.Set, hullConfig.gyroTurnDamping);
        AddStat(modifiers, ShipStatId.MaxAutoTurnRateDeg, ShipStatOperation.Set, hullConfig.maxAutoTurnRateDeg);
        AddStat(modifiers, ShipStatId.MaxStructuralTurnRateDeg, ShipStatOperation.Set, hullConfig.maxStructuralTurnRateDeg);
        AddStat(modifiers, ShipStatId.MaxStructuralVerticalSpeed, ShipStatOperation.Set, hullConfig.maxStructuralVerticalSpeedMS);
        AddStat(modifiers, ShipStatId.MaxAutoVerticalSpeed, ShipStatOperation.Set, hullConfig.maxAutoVerticalSpeedMS);
        AddStat(modifiers, ShipStatId.AltitudeStiffness, ShipStatOperation.Set, hullConfig.altitudeStiffness);
        AddStat(modifiers, ShipStatId.AltitudeDamping, ShipStatOperation.Set, hullConfig.altitudeDamping);
        AddStat(modifiers, ShipStatId.AltitudeDriftTolerance, ShipStatOperation.Set, hullConfig.altitudeDriftToleranceM);
        AddStat(modifiers, ShipStatId.HeadingStiffness, ShipStatOperation.Set, hullConfig.headingStiffness);
        AddStat(modifiers, ShipStatId.HeadingDamping, ShipStatOperation.Set, hullConfig.headingDamping);
        AddStat(modifiers, ShipStatId.WaypointRadius, ShipStatOperation.Set, hullConfig.waypointRadiusM);
        AddStat(modifiers, ShipStatId.SpeedStiffness, ShipStatOperation.Set, hullConfig.speedStiffness);
        AddStat(modifiers, ShipStatId.SpeedDamping, ShipStatOperation.Set, hullConfig.speedDamping);
        AddStat(modifiers, ShipStatId.StructureHp, ShipStatOperation.Set, hullConfig.structureHp);
        return modifiers;
    }

    private static List<ShipStatModifier> BuildEngineStatModifiers(EngineConfig engineConfig)
    {
        List<ShipStatModifier> modifiers = new List<ShipStatModifier>();
        if (engineConfig == null) return modifiers;

        AddStat(modifiers, ShipStatId.BaseMass, ShipStatOperation.Add, engineConfig.baseMassKg);
        AddStat(modifiers, ShipStatId.EngineMaxPower, ShipStatOperation.Set, engineConfig.maxPowerKw);
        AddStat(modifiers, ShipStatId.EngineFuelEfficiency, ShipStatOperation.Set, engineConfig.fuelEfficiency);
        return modifiers;
    }

    private static List<ShipStatModifier> BuildPropellerStatModifiers(PropellerConfig propellerConfig)
    {
        List<ShipStatModifier> modifiers = new List<ShipStatModifier>();
        if (propellerConfig == null) return modifiers;

        AddStat(modifiers, ShipStatId.BaseMass, ShipStatOperation.Add, propellerConfig.baseMassKg);
        AddStat(modifiers, ShipStatId.PropellerMaxSpeedMS, ShipStatOperation.Set, propellerConfig.maxSpeedMS);
        AddStat(modifiers, ShipStatId.PropellerEfficiency, ShipStatOperation.Set, propellerConfig.efficiency);
        AddStat(modifiers, ShipStatId.PropellerMaxThrustKgf, ShipStatOperation.Set, propellerConfig.maxThrustKgf);
        return modifiers;
    }

    private static List<ShipStatModifier> BuildClaudiumLoopStatModifiers(ClaudiumLoopConfig loopConfig)
    {
        List<ShipStatModifier> modifiers = new List<ShipStatModifier>();
        if (loopConfig == null) return modifiers;

        AddStat(modifiers, ShipStatId.BaseMass, ShipStatOperation.Add, loopConfig.baseMassKg);
        AddStat(modifiers, ShipStatId.ClaudiumConsumptionPerTonSecond, ShipStatOperation.Set, loopConfig.claudiumConsumptionPerTonSecond);
        AddStat(modifiers, ShipStatId.ClaudiumLiftEfficiency, ShipStatOperation.Set, loopConfig.liftKgPerKw);
        AddStat(modifiers, ShipStatId.ClaudiumMaxLiftKg, ShipStatOperation.Set, loopConfig.maxLiftKg);
        AddStat(modifiers, ShipStatId.ClaudiumLiftSmoothing, ShipStatOperation.Set, loopConfig.liftSmoothing);
        return modifiers;
    }

    private static void AddStat(List<ShipStatModifier> modifiers, ShipStatId stat, ShipStatOperation operation, float value)
    {
        if (modifiers == null || value <= 0f) return;

        modifiers.Add(new ShipStatModifier
        {
            stat = stat,
            operation = operation,
            value = value
        });
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

    private static List<ShipStatModifier> BuildSpecialModuleStatModifiers(SpecialModuleConfig moduleConfig)
    {
        List<ShipStatModifier> modifiers = new List<ShipStatModifier>();
        if (moduleConfig == null) return modifiers;

        AddSpecialModuleStat(modifiers, ShipStatId.BaseMass, ShipStatOperation.Add, moduleConfig.baseMassKg);
        AddSpecialModuleStat(modifiers, ShipStatId.GasHarvesterVolumeM3PerSecond, ShipStatOperation.Set, moduleConfig.gasHarvesterVolumeM3PerSecond);
        AddSpecialModuleStat(modifiers, ShipStatId.GasHarvesterPowerDrawKw, ShipStatOperation.Set, moduleConfig.gasHarvesterPowerDrawKw);
        AddSpecialModuleStat(modifiers, ShipStatId.GasHarvesterRadiusMeters, ShipStatOperation.Set, moduleConfig.gasHarvesterRadiusMeters);
        AddSpecialModuleStat(modifiers, ShipStatId.GasHarvesterCycleSeconds, ShipStatOperation.Set, moduleConfig.gasHarvesterCycleSeconds);
        AddSpecialModuleStat(modifiers, ShipStatId.GasHarvesterWaterOnly, ShipStatOperation.Set, moduleConfig.gasHarvesterWaterOnly ? 1f : 0f);
        AddSpecialModuleStat(modifiers, ShipStatId.MiningImpactHoldCapacityKg, ShipStatOperation.Set, moduleConfig.miningImpactHoldCapacityKg);
        AddSpecialModuleStat(modifiers, ShipStatId.MiningImpactDamageTakenMultiplier, ShipStatOperation.Set, moduleConfig.miningImpactDamageTakenMultiplier);
        AddSpecialModuleStat(modifiers, ShipStatId.ObservationRadiusMeters, ShipStatOperation.Set, moduleConfig.observationRadiusMeters);
        AddSpecialModuleStat(modifiers, ShipStatId.ObservationFactsAtHalfRadiusPerSecond, ShipStatOperation.Set, moduleConfig.observationFactsAtHalfRadiusPerSecond);
        AddSpecialModuleStat(modifiers, ShipStatId.ObservationRockInfoEfficiency, ShipStatOperation.Set, moduleConfig.observationRockInfoEfficiency);
        AddSpecialModuleStat(modifiers, ShipStatId.ObservationCloudInfoEfficiency, ShipStatOperation.Set, moduleConfig.observationCloudInfoEfficiency);
        AddSpecialModuleStat(modifiers, ShipStatId.ObservationLeviathanInfoEfficiency, ShipStatOperation.Set, moduleConfig.observationLeviathanInfoEfficiency);
        AddSpecialModuleStat(modifiers, ShipStatId.SurveyPaperToInfoEfficiency, ShipStatOperation.Set, moduleConfig.surveyPaperToInfoEfficiency);
        AddSpecialModuleStat(modifiers, ShipStatId.LeviathanAlarmGenerationMultiplier, ShipStatOperation.Set, moduleConfig.leviathanAlarmGenerationMultiplier);
        AddSpecialModuleStat(modifiers, ShipStatId.HarpoonWeaponCostPerMinute, ShipStatOperation.Set, moduleConfig.harpoonWeaponCostPerMinute);
        AddSpecialModuleStat(modifiers, ShipStatId.HarpoonMaxCarcassMassKg, ShipStatOperation.Set, moduleConfig.harpoonMaxCarcassMassKg);
        AddSpecialModuleStat(modifiers, ShipStatId.HarpoonFlightDamage, ShipStatOperation.Set, moduleConfig.harpoonFlightDamage);
        AddSpecialModuleStat(modifiers, ShipStatId.HarpoonRangeMeters, ShipStatOperation.Set, moduleConfig.harpoonRangeMeters);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedWorkforceRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needWorkforceRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedHealthRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needHealthRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedSafetyRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needSafetyRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedComfortRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needComfortRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedCreativityRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needCreativityRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedRepairRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needRepairRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.NeedCapitalConnectionRecoveryPerHour, ShipStatOperation.Add, moduleConfig.needCapitalConnectionRecoveryPerHour);
        AddSpecialModuleStat(modifiers, ShipStatId.CargoVanCapacityUnits, ShipStatOperation.Add, moduleConfig.cargoVanCapacityUnits);
        AddSpecialModuleStat(modifiers, ShipStatId.PassengerSeatCapacity, ShipStatOperation.Add, moduleConfig.passengerSeatCapacity);
        AddSpecialModuleStat(modifiers, ShipStatId.BulkHoldCapacityLiters, ShipStatOperation.Add, moduleConfig.bulkHoldCapacityLiters);
        AddSpecialModuleStat(modifiers, ShipStatId.LiquidTankCapacityLiters, ShipStatOperation.Add, moduleConfig.liquidTankCapacityLiters);
        AddSpecialModuleStat(modifiers, ShipStatId.GasCylinderCapacityLiters, ShipStatOperation.Add, moduleConfig.gasCylinderCapacityLiters);
        AddSpecialModuleStat(modifiers, ShipStatId.RefrigeratedHoldCapacityLiters, ShipStatOperation.Add, moduleConfig.refrigeratedHoldCapacityLiters);
        AddSpecialModuleStat(modifiers, ShipStatId.RefrigeratedHoldPowerDrawKw, ShipStatOperation.Set, moduleConfig.refrigeratedHoldPowerDrawKw);
        AddSpecialModuleStat(modifiers, ShipStatId.ShipDockSlots, ShipStatOperation.Add, moduleConfig.shipDockSlots);
        if (moduleConfig.shipDockSlots > 0f)
        {
            AddSpecialModuleStat(modifiers, ShipStatId.ShipDockMaxClass, ShipStatOperation.Set, (float)moduleConfig.shipDockMaxClass);
            AddSpecialModuleStat(modifiers, ShipStatId.DockedShipMassFactor, ShipStatOperation.Set, moduleConfig.dockedShipMassFactor);
            AddSpecialModuleStat(modifiers, ShipStatId.DockSupportClaudiumPerTonHour, ShipStatOperation.Set, moduleConfig.dockSupportClaudiumPerTonHour);
        }
        return modifiers;
    }

    private static void AddSpecialModuleStat(List<ShipStatModifier> modifiers, ShipStatId stat, ShipStatOperation operation, float value)
    {
        if (value <= 0f) return;

        modifiers.Add(new ShipStatModifier
        {
            stat = stat,
            operation = operation,
            value = value
        });
    }
}

public class ShipStatBlock
{
    private readonly Dictionary<ShipStatId, float> setValues = new Dictionary<ShipStatId, float>();
    private readonly Dictionary<ShipStatId, float> addValues = new Dictionary<ShipStatId, float>();
    private readonly Dictionary<ShipStatId, float> multiplyValues = new Dictionary<ShipStatId, float>();
    private readonly HashSet<ShipStatId> setStats = new HashSet<ShipStatId>();
    private readonly List<string> allowedCargoItemIds = new List<string>();
    private string engineFuelId = "";

    public string EngineFuelId => engineFuelId;
    public IReadOnlyList<string> AllowedCargoItemIds => allowedCargoItemIds;

    public bool ApplyPart(ShipPartDefinitionSO part, out string error)
    {
        error = "";
        if (part == null) return true;

        if (part.allowedCargoItemIds != null)
        {
            for (int i = 0; i < part.allowedCargoItemIds.Count; i++)
            {
                string itemId = part.allowedCargoItemIds[i];
                if (!string.IsNullOrWhiteSpace(itemId) && !allowedCargoItemIds.Contains(itemId))
                {
                    allowedCargoItemIds.Add(itemId);
                }
            }
        }

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
        ship.gasHarvesterWaterOnly = Get(ShipStatId.GasHarvesterWaterOnly, ship.gasHarvesterWaterOnly ? 1f : 0f) > 0.5f;
        ship.miningImpactHoldCapacityKg = Mathf.Max(0f, Get(ShipStatId.MiningImpactHoldCapacityKg, 0f));
        ship.miningImpactDamageTakenMultiplier = Mathf.Max(0f, Get(ShipStatId.MiningImpactDamageTakenMultiplier, ship.miningImpactDamageTakenMultiplier));
        ship.observationRadiusMeters = Mathf.Max(ship.baseObservationRadiusMeters, Get(ShipStatId.ObservationRadiusMeters, ship.baseObservationRadiusMeters));
        ship.observationFactsAtHalfRadiusPerSecond = Mathf.Max(0.01f, Get(ShipStatId.ObservationFactsAtHalfRadiusPerSecond, 1f));
        ship.observationRockInfoEfficiency = Mathf.Clamp01(Get(ShipStatId.ObservationRockInfoEfficiency, 0f));
        ship.observationCloudInfoEfficiency = Mathf.Clamp01(Get(ShipStatId.ObservationCloudInfoEfficiency, 0f));
        ship.observationLeviathanInfoEfficiency = Mathf.Clamp01(Get(ShipStatId.ObservationLeviathanInfoEfficiency, 0f));
        ship.surveyPaperToInfoEfficiency = Mathf.Clamp(Get(ShipStatId.SurveyPaperToInfoEfficiency, ship.surveyPaperToInfoEfficiency), 0.01f, 1f);
        ship.leviathanAlarmGenerationMultiplier = Mathf.Max(0f, Get(ShipStatId.LeviathanAlarmGenerationMultiplier, ship.leviathanAlarmGenerationMultiplier));
        ship.harpoonWeaponCostPerMinute = Mathf.Max(0f, Get(ShipStatId.HarpoonWeaponCostPerMinute, ship.harpoonWeaponCostPerMinute));
        ship.harpoonMaxCarcassMassKg = Mathf.Max(0f, Get(ShipStatId.HarpoonMaxCarcassMassKg, ship.harpoonMaxCarcassMassKg));
        ship.leviathanWeaponShotFlightDamage = Mathf.Max(0f, Get(ShipStatId.HarpoonFlightDamage, ship.leviathanWeaponShotFlightDamage));
        ship.harpoonRangeMeters = Mathf.Max(0f, Get(ShipStatId.HarpoonRangeMeters, ship.harpoonRangeMeters));
        ship.refrigeratedHoldCapacityLiters = Mathf.Max(0f, Get(ShipStatId.RefrigeratedHoldCapacityLiters, ship.refrigeratedHoldCapacityLiters));
        ship.refrigeratedHoldPowerDrawKw = Mathf.Max(0f, Get(ShipStatId.RefrigeratedHoldPowerDrawKw, ship.refrigeratedHoldPowerDrawKw));

        DamageableShip damageableShip = ship.GetComponentInParent<DamageableShip>();
        if (damageableShip != null)
        {
            damageableShip.maxStructureHp = Mathf.Max(1f, Get(ShipStatId.StructureHp, damageableShip.maxStructureHp));
            damageableShip.structureHp = Mathf.Clamp(damageableShip.structureHp, 0f, damageableShip.maxStructureHp);
        }

        Rigidbody body = ship.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.mass = ship.GetTotalMassKg();
        }

        ship.RefreshRuntimeShipSettings();
    }
}
