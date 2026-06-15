using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class SessionConfigDatabase
{
    private readonly Dictionary<string, HullConfig> hullsById = new Dictionary<string, HullConfig>();
    private readonly Dictionary<string, ClaudiumLoopConfig> claudiumLoopsById = new Dictionary<string, ClaudiumLoopConfig>();
    private readonly Dictionary<string, ShipTreeEntryConfig> shipTreeEntriesById = new Dictionary<string, ShipTreeEntryConfig>();

    public List<HullConfig> hulls = new List<HullConfig>();
    public List<ClaudiumLoopConfig> claudiumLoops = new List<ClaudiumLoopConfig>();
    public List<ShipTreeEntryConfig> shipTreeEntries = new List<ShipTreeEntryConfig>();

    public HullConfig GetHull(string hullId)
    {
        if (string.IsNullOrWhiteSpace(hullId)) return null;
        hullsById.TryGetValue(hullId, out HullConfig hull);
        return hull;
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
        claudiumLoops.Clear();
        shipTreeEntries.Clear();
        hullsById.Clear();
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
                speedStiffness = Mathf.Max(0f, ParseFloat(Get(row, "speed_stiffness"))),
                speedDamping = Mathf.Max(0f, ParseFloat(Get(row, "speed_damping"))),
                structureHp = Mathf.Max(0f, ParseFloat(Get(row, "structure_hp"))),
                hullForwardThrustKgf = Mathf.Max(0f, ParseFloat(Get(row, "hull_forward_thrust_kgf"), 1200f)),
                hullCruiseReferenceSpeedMS = Mathf.Max(1f, ParseFloat(Get(row, "hull_cruise_reference_speed_ms"), 30f)),
                fuelConsumptionKgPerMinute = Mathf.Max(0f, ParseFloat(Get(row, "fuel_consumption_kg_per_minute"), 1.2f)),
                fuelResourceId = string.IsNullOrWhiteSpace(Get(row, "fuel_resource_id")) ? "charcoal" : Get(row, "fuel_resource_id")
            };

            if (string.IsNullOrWhiteSpace(hull.id)) continue;
            hulls.Add(hull);
            hullsById[hull.id] = hull;
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
                maxLiftKg = Mathf.Max(0f, ParseFloat(Get(row, "max_lift_kg")))
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
                factionId = Get(row, "faction_id"),
                factionNameRu = Get(row, "faction_name_ru"),
                shipClassId = Get(row, "ship_class_id"),
                shipClassNameRu = Get(row, "ship_class_name_ru"),
                classNameRu = Get(row, "class_name_ru"),
                roleId = Get(row, "role_id"),
                roleNameRu = Get(row, "role_name_ru"),
                catalogScope = Get(row, "catalog_scope"),
                branchId = Get(row, "branch_id"),
                branchNameRu = Get(row, "branch_name_ru"),
                treeTier = Mathf.Clamp(ParseInt(Get(row, "tree_tier"), 0), 0, 10),
                treeRow = Mathf.Max(0, ParseInt(Get(row, "tree_row"), 0)),
                requiredTechnologyId = Get(row, "required_technology"),
                hullId = Get(row, "hull_id"),
                claudiumLoopId = Get(row, "claudium_loop_id"),
                specialModuleId = Get(row, "special_module_id"),
                visualModelId = Get(row, "visual_model_id"),
                visualShapeId = Get(row, "visual_shape_id"),
                visualColor = ParseColor(Get(row, "visual_color_hex"), new Color(0.42f, 0.62f, 0.78f, 1f)),
                costCurrencyItemId = Get(row, "cost_currency_item"),
                costAmount = Mathf.Max(0, ParseInt(Get(row, "cost_amount"), 0)),
                firepower = Mathf.Max(0, ParseInt(Get(row, "firepower"), 0)),
                armor = Mathf.Max(0, ParseInt(Get(row, "armor"), 0)),
                durability = Mathf.Max(0, ParseInt(Get(row, "durability"), 0)),
                speed = Mathf.Max(0, ParseInt(Get(row, "speed"), 0)),
                maneuverability = Mathf.Max(0, ParseInt(Get(row, "maneuverability"), 0)),
                cargo = Mathf.Max(0, ParseInt(Get(row, "cargo"), 0)),
                utility = Mathf.Max(0, ParseInt(Get(row, "utility"), 0)),
                summaryRu = Get(row, "summary_ru")
            };

            if (string.IsNullOrWhiteSpace(entry.shipClassNameRu))
            {
                entry.shipClassNameRu = entry.classNameRu;
            }

            if (string.IsNullOrWhiteSpace(entry.classNameRu))
            {
                entry.classNameRu = entry.shipClassNameRu;
            }

            if (string.IsNullOrWhiteSpace(entry.visualShapeId) && !string.IsNullOrWhiteSpace(entry.visualModelId))
            {
                entry.visualShapeId = "square";
            }

            if (string.IsNullOrWhiteSpace(entry.branchId) && entry.IsDevelopmentRosterShip)
            {
                entry.branchId = string.IsNullOrWhiteSpace(entry.shipClassId) ? "ship_line" : entry.shipClassId + "_line";
            }

            if (string.IsNullOrWhiteSpace(entry.branchNameRu))
            {
                entry.branchNameRu = entry.ClassDisplayNameRu;
            }

            if (entry.treeTier <= 0)
            {
                entry.treeTier = entry.rank > 0 ? Mathf.Clamp(entry.rank, 1, 10) : 1;
            }

            entry.parentShipIds.AddRange(SplitInlineList(Get(row, "parent_ship_id")));
            entry.upgradeHullIds.AddRange(SplitInlineList(Get(row, "upgrade_hull_id")));
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
    public float speedStiffness;
    public float speedDamping;
    public float structureHp;
    public float hullForwardThrustKgf = 1200f;
    public float hullCruiseReferenceSpeedMS = 30f;
    public float fuelConsumptionKgPerMinute = 1.2f;
    public string fuelResourceId = "charcoal";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class ClaudiumLoopConfig
{
    public string id = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public string completedTechId = "";
    public float baseMassKg;
    public float maxLiftKg;

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? id : localNameRu;
}

public class ShipTreeEntryConfig
{
    public string shipId = "";
    public string localNameRu = "";
    public string localNameEn = "";
    public int rank;
    public string factionId = "";
    public string factionNameRu = "";
    public string shipClassId = "";
    public string shipClassNameRu = "";
    public string classNameRu = "";
    public string roleId = "";
    public string roleNameRu = "";
    public string catalogScope = "";
    public string branchId = "";
    public string branchNameRu = "";
    public int treeTier;
    public int treeRow;
    public List<string> parentShipIds = new List<string>();
    public string requiredTechnologyId = "";
    public string hullId = "";
    public string claudiumLoopId = "";
    public string specialModuleId = "";
    public List<string> upgradeHullIds = new List<string>();
    public List<string> upgradeClaudiumLoopIds = new List<string>();
    public List<string> upgradeSpecialModuleIds = new List<string>();
    public string visualModelId = "";
    public string visualShapeId = "";
    public Color visualColor = Color.white;
    public string costCurrencyItemId = "";
    public int costAmount;
    public int firepower;
    public int armor;
    public int durability;
    public int speed;
    public int maneuverability;
    public int cargo;
    public int utility;
    public string summaryRu = "";

    public string DisplayNameRu => string.IsNullOrWhiteSpace(localNameRu) ? shipId : localNameRu;
    public string FactionDisplayNameRu => string.IsNullOrWhiteSpace(factionNameRu) ? factionId : factionNameRu;
    public string ClassDisplayNameRu => string.IsNullOrWhiteSpace(shipClassNameRu) ? classNameRu : shipClassNameRu;
    public string BranchDisplayNameRu => string.IsNullOrWhiteSpace(branchNameRu) ? ClassDisplayNameRu : branchNameRu;
    public bool IsDevelopmentRosterShip => !string.IsNullOrWhiteSpace(factionId) || catalogScope == "development";
    public bool HasRuntimeHull => !string.IsNullOrWhiteSpace(hullId);
    public int TotalStatScore => firepower + armor + durability + speed + maneuverability + cargo + utility;
}
