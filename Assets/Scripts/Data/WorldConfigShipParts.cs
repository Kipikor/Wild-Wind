using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class WorldConfigDatabase
{
    private readonly Dictionary<string, HullConfig> hullsById = new Dictionary<string, HullConfig>();
    private readonly Dictionary<string, EngineConfig> enginesById = new Dictionary<string, EngineConfig>();
    private readonly Dictionary<string, PropellerConfig> propellersById = new Dictionary<string, PropellerConfig>();
    private readonly Dictionary<string, ClaudiumLoopConfig> claudiumLoopsById = new Dictionary<string, ClaudiumLoopConfig>();
    private readonly Dictionary<string, ShipTreeEntryConfig> shipTreeEntriesById = new Dictionary<string, ShipTreeEntryConfig>();

    public List<HullConfig> hulls = new List<HullConfig>();
    public List<EngineConfig> engines = new List<EngineConfig>();
    public List<PropellerConfig> propellers = new List<PropellerConfig>();
    public List<ClaudiumLoopConfig> claudiumLoops = new List<ClaudiumLoopConfig>();
    public List<ShipTreeEntryConfig> shipTreeEntries = new List<ShipTreeEntryConfig>();

    public HullConfig GetHull(string hullId)
    {
        if (string.IsNullOrWhiteSpace(hullId)) return null;
        hullsById.TryGetValue(hullId, out HullConfig hull);
        return hull;
    }

    public EngineConfig GetEngine(string engineId)
    {
        if (string.IsNullOrWhiteSpace(engineId)) return null;
        enginesById.TryGetValue(engineId, out EngineConfig engine);
        return engine;
    }

    public PropellerConfig GetPropeller(string propellerId)
    {
        if (string.IsNullOrWhiteSpace(propellerId)) return null;
        propellersById.TryGetValue(propellerId, out PropellerConfig propeller);
        return propeller;
    }

    public ClaudiumLoopConfig GetClaudiumLoop(string loopId)
    {
        if (string.IsNullOrWhiteSpace(loopId)) return null;
        claudiumLoopsById.TryGetValue(loopId, out ClaudiumLoopConfig loop);
        return loop;
    }

    public ShipTreeEntryConfig GetShipTreeEntry(string shipId)
    {
        if (string.IsNullOrWhiteSpace(shipId)) return null;
        shipTreeEntriesById.TryGetValue(shipId, out ShipTreeEntryConfig entry);
        return entry;
    }

    private void ClearShipPartConfigs()
    {
        hulls.Clear();
        engines.Clear();
        propellers.Clear();
        claudiumLoops.Clear();
        shipTreeEntries.Clear();
        hullsById.Clear();
        enginesById.Clear();
        propellersById.Clear();
        claudiumLoopsById.Clear();
        shipTreeEntriesById.Clear();
    }

    private void LoadHulls(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            HullConfig hull = new HullConfig
            {
                id = Get(row, "id_hull"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                completedTechId = Get(row, "complited_tech"),
                baseMassKg = Mathf.Max(0f, ParseFloat(Get(row, "base_mass_kg"))),
                hullMaxTakeoffMassKg = Mathf.Max(0f, ParseFloat(Get(row, "hull_max_takeoff_mass_kg"))),
                airDensity = Mathf.Max(0f, ParseFloat(Get(row, "air_density"), 1.225f)),
                dragCoefficient = Mathf.Max(0f, ParseFloat(Get(row, "drag_coefficient"))),
                frontalAreaM2 = Mathf.Max(0f, ParseFloat(Get(row, "frontal_area_m2"))),
                sideResistance = Mathf.Max(0f, ParseFloat(Get(row, "side_resistance"))),
                verticalAreaFactor = Mathf.Max(0f, ParseFloat(Get(row, "vertical_area_factor"))),
                gyroTurnTorqueNm = Mathf.Max(0f, ParseFloat(Get(row, "gyro_turn_torque_nm"))),
                gyroTurnDamping = Mathf.Max(0f, ParseFloat(Get(row, "gyro_turn_damping"))),
                maxAutoTurnRateDeg = Mathf.Max(0f, ParseFloat(Get(row, "max_auto_turn_rate_deg"))),
                maxStructuralTurnRateDeg = Mathf.Max(0f, ParseFloat(Get(row, "max_structural_turn_rate_deg"))),
                maxStructuralVerticalSpeedMS = Mathf.Max(0f, ParseFloat(Get(row, "max_structural_vertical_speed_ms"))),
                maxAutoVerticalSpeedMS = Mathf.Max(0f, ParseFloat(Get(row, "max_auto_vertical_speed_ms"))),
                altitudeStiffness = Mathf.Max(0f, ParseFloat(Get(row, "altitude_stiffness"))),
                altitudeDamping = Mathf.Max(0f, ParseFloat(Get(row, "altitude_damping"))),
                altitudeDriftToleranceM = Mathf.Max(0f, ParseFloat(Get(row, "altitude_drift_tolerance_m"))),
                headingStiffness = Mathf.Max(0f, ParseFloat(Get(row, "heading_stiffness"))),
                headingDamping = Mathf.Max(0f, ParseFloat(Get(row, "heading_damping"))),
                waypointRadiusM = Mathf.Max(0f, ParseFloat(Get(row, "waypoint_radius_m"))),
                speedStiffness = Mathf.Max(0f, ParseFloat(Get(row, "speed_stiffness"))),
                speedDamping = Mathf.Max(0f, ParseFloat(Get(row, "speed_damping"))),
                structureHp = Mathf.Max(0f, ParseFloat(Get(row, "structure_hp")))
            };

            if (string.IsNullOrWhiteSpace(hull.id)) continue;
            hulls.Add(hull);
            hullsById[hull.id] = hull;
        }
    }

    private void LoadEngines(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            EngineConfig engine = new EngineConfig
            {
                id = Get(row, "id_engine"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                completedTechId = Get(row, "complited_tech"),
                baseMassKg = Mathf.Max(0f, ParseFloat(Get(row, "base_mass_kg"))),
                fuelId = Get(row, "fuel_id"),
                maxPowerKw = Mathf.Max(0f, ParseFloat(Get(row, "max_power_kw"))),
                fuelEfficiency = Mathf.Clamp01(ParseFloat(Get(row, "fuel_efficiency")))
            };

            if (string.IsNullOrWhiteSpace(engine.id)) continue;
            engines.Add(engine);
            enginesById[engine.id] = engine;
        }
    }

    private void LoadPropellers(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            PropellerConfig propeller = new PropellerConfig
            {
                id = Get(row, "id_propeller"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                completedTechId = Get(row, "complited_tech"),
                baseMassKg = Mathf.Max(0f, ParseFloat(Get(row, "base_mass_kg"))),
                maxSpeedMS = Mathf.Max(0f, ParseFloat(Get(row, "max_speed_ms"))),
                efficiency = Mathf.Clamp01(ParseFloat(Get(row, "efficiency"))),
                maxThrustKgf = Mathf.Max(0f, ParseFloat(Get(row, "max_thrust_kgf")))
            };

            if (string.IsNullOrWhiteSpace(propeller.id)) continue;
            propellers.Add(propeller);
            propellersById[propeller.id] = propeller;
        }
    }

    private void LoadClaudiumLoops(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            ClaudiumLoopConfig loop = new ClaudiumLoopConfig
            {
                id = Get(row, "id_claudium_loop"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                completedTechId = Get(row, "complited_tech"),
                baseMassKg = Mathf.Max(0f, ParseFloat(Get(row, "base_mass_kg"))),
                claudiumConsumptionPerTonSecond = Mathf.Max(0f, ParseFloat(Get(row, "claudium_consumption_per_ton_second"))),
                liftKgPerKw = Mathf.Max(0f, ParseFloat(Get(row, "lift_kg_per_kw"))),
                maxLiftKg = Mathf.Max(0f, ParseFloat(Get(row, "max_lift_kg"))),
                liftSmoothing = Mathf.Max(0f, ParseFloat(Get(row, "lift_smoothing")))
            };

            if (string.IsNullOrWhiteSpace(loop.id)) continue;
            claudiumLoops.Add(loop);
            claudiumLoopsById[loop.id] = loop;
        }
    }

    private void LoadShipTree(string path)
    {
        foreach (Dictionary<string, string> row in ReadCsv(path))
        {
            ShipTreeEntryConfig entry = new ShipTreeEntryConfig
            {
                shipId = Get(row, "id_ship"),
                localNameRu = Get(row, "local_name_ru"),
                localNameEn = Get(row, "local_name_en"),
                rank = Mathf.Max(0, ParseInt(Get(row, "rank"), 0)),
                classNameRu = Get(row, "class_name_ru"),
                roleId = Get(row, "role_id"),
                roleNameRu = Get(row, "role_name_ru"),
                requiredTechnologyId = Get(row, "required_technology"),
                hullId = Get(row, "hull_id"),
                engineId = Get(row, "engine_id"),
                propellerId = Get(row, "propeller_id"),
                claudiumLoopId = Get(row, "claudium_loop_id"),
                specialModuleId = Get(row, "special_module_id"),
                summaryRu = Get(row, "summary_ru")
            };

            entry.parentShipIds.AddRange(SplitInlineList(Get(row, "parent_ship_id")));
            entry.upgradeHullIds.AddRange(SplitInlineList(Get(row, "upgrade_hull_id")));
            entry.upgradeEngineIds.AddRange(SplitInlineList(Get(row, "upgrade_engine_id")));
            entry.upgradePropellerIds.AddRange(SplitInlineList(Get(row, "upgrade_propeller_id")));
            entry.upgradeClaudiumLoopIds.AddRange(SplitInlineList(Get(row, "upgrade_claudium_loop_id")));
            entry.upgradeSpecialModuleIds.AddRange(SplitInlineList(Get(row, "upgrade_special_module_id")));

            if (string.IsNullOrWhiteSpace(entry.shipId)) continue;
            shipTreeEntries.Add(entry);
            shipTreeEntriesById[entry.shipId] = entry;
        }
    }
}

public class HullConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string completedTechId = "";
    public float baseMassKg;
    public float hullMaxTakeoffMassKg;
    public float airDensity = 1.225f;
    public float dragCoefficient;
    public float frontalAreaM2;
    public float sideResistance;
    public float verticalAreaFactor;
    public float gyroTurnTorqueNm;
    public float gyroTurnDamping;
    public float maxAutoTurnRateDeg;
    public float maxStructuralTurnRateDeg;
    public float maxStructuralVerticalSpeedMS;
    public float maxAutoVerticalSpeedMS;
    public float altitudeStiffness;
    public float altitudeDamping;
    public float altitudeDriftToleranceM;
    public float headingStiffness;
    public float headingDamping;
    public float waypointRadiusM;
    public float speedStiffness;
    public float speedDamping;
    public float structureHp;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class EngineConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string completedTechId = "";
    public float baseMassKg;
    public string fuelId = "";
    public float maxPowerKw;
    public float fuelEfficiency;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class PropellerConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string completedTechId = "";
    public float baseMassKg;
    public float maxSpeedMS;
    public float efficiency;
    public float maxThrustKgf;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class ClaudiumLoopConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string completedTechId = "";
    public float baseMassKg;
    public float claudiumConsumptionPerTonSecond;
    public float liftKgPerKw;
    public float maxLiftKg;
    public float liftSmoothing;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class ShipTreeEntryConfig
{
    public string shipId = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public int rank;
    public string classNameRu = "";
    public string roleId = "";
    public string roleNameRu = "";
    public List<string> parentShipIds = new List<string>();
    public string requiredTechnologyId = "";
    public string hullId = "";
    public string engineId = "";
    public string propellerId = "";
    public string claudiumLoopId = "";
    public string specialModuleId = "";
    public List<string> upgradeHullIds = new List<string>();
    public List<string> upgradeEngineIds = new List<string>();
    public List<string> upgradePropellerIds = new List<string>();
    public List<string> upgradeClaudiumLoopIds = new List<string>();
    public List<string> upgradeSpecialModuleIds = new List<string>();
    public string summaryRu = "";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? shipId : localNameRu;
}
