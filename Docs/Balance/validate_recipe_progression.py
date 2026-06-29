from __future__ import annotations

import csv
import math
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"
ITEMS_PATH = ROOT / "Assets" / "Data" / "Config" / "Item.csv"
RESOURCE_CATEGORY_PATH = ROOT / "Assets" / "Data" / "Config" / "Resource_category.csv"
ICON_DIR = ROOT / "Assets" / "Resources" / "UI" / "ResourceIcons"

EXPECTED_TIME_MULTIPLIERS = {
    0: 2.50,
    1: 2.20,
    2: 1.90,
    3: 1.60,
    4: 1.30,
    5: 1.00,
}

EXPECTED_EFFICIENCY_MULTIPLIERS = {
    0: 2.00,
    1: 1.80,
    2: 1.60,
    3: 1.40,
    4: 1.20,
    5: 1.00,
}

NEW_CONFIG_FILES = [
    "recipe_time_upgrade_curve.csv",
    "recipe_efficiency_upgrade_curve.csv",
    "recipe_building_speed_curve.csv",
    "item_production_recipes.csv",
    "item_recipe_cost_levels.csv",
    "item_recipe_time_levels.csv",
    "ship_rank_craft_time_policy.csv",
    "ship_r10_recipe_profiles.csv",
    "ship_r10_recipe_cost_levels.csv",
    "ship_r10_recipe_time_levels.csv",
    "ship_r10_fe_extremes.csv",
    "faction_r10_licenses.csv",
    "craft_progression_scope_policy.csv",
    "shipyard_build_speed_curve.csv",
    "ship_class_yard_speed_curve.csv",
    "builder_yard_speed_policy.csv",
    "ship_crafting_recipes.csv",
    "ship_recipe_cost_levels.csv",
    "ship_recipe_time_levels.csv",
    "ship_recipe_fe_summary.csv",
    "ship_rank_fe_summary.csv",
    "ship_faction_fe_summary.csv",
    "ship_time_summary.csv",
    "ship_full_chain_time.csv",
    "ship_full_chain_time_summary.csv",
    "sortie_extraction_outcome_policy.csv",
    "mission_archetype_requirements.csv",
    "mission_profile_reward_model.csv",
    "mission_profile_ship_profit.csv",
    "mission_profile_rank_payback_summary.csv",
    "mission_profile_class_payback_summary.csv",
    "mission_profile_faction_payback_summary.csv",
    "mission_profile_activity_payback_summary.csv",
    "quick_sortie_reward_model.csv",
    "quick_sortie_ship_profit.csv",
    "quick_sortie_rank_payback_summary.csv",
    "quick_sortie_class_payback_summary.csv",
    "quick_sortie_faction_payback_summary.csv",
    "quick_sortie_activity_payback_summary.csv",
    "elite_r10_capacity_check.csv",
    "elite_r10_required_payloads.csv",
]

CLASS_YARDS = {
    "frigate": "frigate_yard",
    "cruiser": "cruiser_yard",
    "battleship": "battleship_yard",
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def split_ids(raw: str) -> list[str]:
    return [part.strip() for part in raw.split(";") if part.strip()]


def split_amounts(raw: str) -> list[int]:
    return [int(part.strip()) for part in raw.split(";") if part.strip()]


def as_float(value: str) -> float:
    return float(value.replace(",", "."))


def approx(left: float, right: float, eps: float = 0.011) -> bool:
    return abs(left - right) <= eps


def check(condition: bool, message: str, errors: list[str]) -> None:
    if not condition:
        errors.append(message)


def validate() -> list[str]:
    errors: list[str] = []

    items = read_csv(ITEMS_PATH)
    item_ids = {row["id_item"] for row in items}
    resource_values = read_csv(CONFIG_DIR / "resource_value_reference.csv")
    value_by_item_id = {row["item_id"]: row for row in resource_values}
    for item_id in value_by_item_id:
        check(item_id in item_ids, f"Resource value references missing item {item_id}", errors)
        check(as_float(value_by_item_id[item_id]["value_fe_per_unit"]) > 0, f"Resource value for {item_id} must be positive", errors)

    icons = {p.stem for p in ICON_DIR.glob("*.png")}
    missing_icons = sorted(item_ids - icons)
    for item_id in missing_icons:
        errors.append(f"Missing resource icon: {item_id}")

    categories = read_csv(RESOURCE_CATEGORY_PATH)
    for category in categories:
        for item_id in re.split(r"\|", category["item_ids"]):
            item_id = item_id.strip()
            if item_id and item_id not in item_ids:
                errors.append(f"Resource category {category['category_id']} references missing item {item_id}")

    time_curve = read_csv(CONFIG_DIR / "recipe_time_upgrade_curve.csv")
    check(len(time_curve) == 6, "Time curve must contain levels 0..5", errors)
    for row in time_curve:
        level = int(row["time_level"])
        multiplier = as_float(row["time_multiplier"])
        check(level in EXPECTED_TIME_MULTIPLIERS, f"Unexpected time level {level}", errors)
        if level in EXPECTED_TIME_MULTIPLIERS:
            check(approx(multiplier, EXPECTED_TIME_MULTIPLIERS[level]), f"Time level {level} multiplier mismatch: {multiplier}", errors)

    efficiency_curve = read_csv(CONFIG_DIR / "recipe_efficiency_upgrade_curve.csv")
    check(len(efficiency_curve) == 6, "Efficiency curve must contain levels 0..5", errors)
    for row in efficiency_curve:
        level = int(row["economy_level"])
        multiplier = as_float(row["input_multiplier"])
        check(level in EXPECTED_EFFICIENCY_MULTIPLIERS, f"Unexpected economy level {level}", errors)
        if level in EXPECTED_EFFICIENCY_MULTIPLIERS:
            check(approx(multiplier, EXPECTED_EFFICIENCY_MULTIPLIERS[level]), f"Economy level {level} multiplier mismatch: {multiplier}", errors)

    building_curve = read_csv(CONFIG_DIR / "recipe_building_speed_curve.csv")
    check(len(building_curve) == 30, "Building speed curve must contain 30 levels", errors)
    if building_curve:
        first = building_curve[0]
        last = building_curve[-1]
        check(int(first["building_level"]) == 1 and approx(as_float(first["speed_multiplier"]), 1.0), "Building level 1 speed must be 1.0", errors)
        check(int(last["building_level"]) == 30 and approx(as_float(last["speed_multiplier"]), 1.5), "Building level 30 speed must be 1.5", errors)

    scope_policy = {row["scope_id"]: row for row in read_csv(CONFIG_DIR / "craft_progression_scope_policy.csv")}
    for required_scope in ("item_production", "ship_crafting", "building_upgrade"):
        check(required_scope in scope_policy, f"Missing craft progression scope {required_scope}", errors)
    if "item_production" in scope_policy:
        item_scope = scope_policy["item_production"]
        check(item_scope["has_recipe_time_upgrade"] == "yes", "Item production must have recipe time upgrades", errors)
        check(item_scope["has_recipe_economy_upgrade"] == "yes", "Item production must have recipe economy upgrades", errors)
        check(item_scope["time_curve_config"] == "recipe_time_upgrade_curve.csv", "Item production must use shared time curve", errors)
        check(item_scope["economy_curve_config"] == "recipe_efficiency_upgrade_curve.csv", "Item production must use shared economy curve", errors)
    if "ship_crafting" in scope_policy:
        ship_scope = scope_policy["ship_crafting"]
        check(ship_scope["has_recipe_time_upgrade"] == "yes", "Ship crafting must have recipe time upgrades", errors)
        check(ship_scope["has_recipe_economy_upgrade"] == "yes", "Ship crafting must have recipe economy upgrades", errors)
        check(ship_scope["producer_building_id"] == "class_shipyard", "Ship crafting must use class-specific shipyards", errors)
        check(ship_scope["building_speed_config"] == "ship_class_yard_speed_curve.csv", "Ship crafting must use class yard speed curve", errors)
    if "building_upgrade" in scope_policy:
        building_scope = scope_policy["building_upgrade"]
        check(building_scope["has_recipe_time_upgrade"] == "no", "Building upgrades must not have recipe time upgrades", errors)
        check(building_scope["has_recipe_economy_upgrade"] == "no", "Building upgrades must not have recipe economy upgrades", errors)
        check(building_scope["producer_building_id"] == "builder_yard", "Building upgrades must be sped up by builder yard", errors)

    shipyard_curve = read_csv(CONFIG_DIR / "shipyard_build_speed_curve.csv")
    check(len(shipyard_curve) == 30, "Shipyard speed curve must contain 30 levels", errors)
    if shipyard_curve:
        first = shipyard_curve[0]
        last = shipyard_curve[-1]
        check(int(first["shipyard_level"]) == 1 and approx(as_float(first["speed_multiplier"]), 1.0), "Shipyard level 1 speed must be 1.0", errors)
        check(int(last["shipyard_level"]) == 30 and approx(as_float(last["speed_multiplier"]), 1.5), "Shipyard level 30 speed must be 1.5", errors)

    class_yard_curve = read_csv(CONFIG_DIR / "ship_class_yard_speed_curve.csv")
    check(len(class_yard_curve) == 90, "Class yard speed curve must contain 3 classes x 30 levels", errors)
    class_yard_seen = {(row["ship_class_id"], int(row["yard_level"])) for row in class_yard_curve}
    for ship_class_id, building_id in CLASS_YARDS.items():
        rows = [row for row in class_yard_curve if row["ship_class_id"] == ship_class_id]
        check(len(rows) == 30, f"Class yard curve must contain 30 levels for {ship_class_id}", errors)
        for level in range(1, 31):
            check((ship_class_id, level) in class_yard_seen, f"Missing {ship_class_id} yard level {level}", errors)
        if rows:
            first = sorted(rows, key=lambda row: int(row["yard_level"]))[0]
            last = sorted(rows, key=lambda row: int(row["yard_level"]))[-1]
            check(first["building_id"] == building_id, f"{ship_class_id} yard must use building {building_id}", errors)
            check(last["building_id"] == building_id, f"{ship_class_id} yard must use building {building_id}", errors)
            check(approx(as_float(first["speed_multiplier"]), 1.0), f"{ship_class_id} yard level 1 speed must be 1.0", errors)
            check(approx(as_float(last["speed_multiplier"]), 1.5), f"{ship_class_id} yard level 30 speed must be 1.5", errors)

    builder_policy_rows = read_csv(CONFIG_DIR / "builder_yard_speed_policy.csv")
    check(len(builder_policy_rows) == 1, "Builder yard speed policy must contain one rule", errors)
    if builder_policy_rows:
        builder_policy = builder_policy_rows[0]
        check(builder_policy["speed_source_building_id"] == "builder_yard", "Builder speed source must be builder_yard", errors)
        check(builder_policy["has_individual_recipe_time_upgrade"] == "no", "Building upgrades must have no individual time mastery", errors)
        check(builder_policy["has_individual_recipe_economy_upgrade"] == "no", "Building upgrades must have no individual economy mastery", errors)
        check(builder_policy["can_use_outputs_from_same_building"] == "yes", "Building upgrades must be allowed to consume own-building outputs", errors)

    recipes = read_csv(CONFIG_DIR / "item_production_recipes.csv")
    recipe_by_id = {row["recipe_id"]: row for row in recipes}
    recipe_outputs = {row["output_item_id"] for row in recipes}
    check(len(recipe_by_id) == len(recipes), "Duplicate recipe ids in item_production_recipes.csv", errors)
    check(len(recipes) >= 40, "Expected at least prepared materials, parts, and blocks in item recipes", errors)

    for recipe in recipes:
        output_id = recipe["output_item_id"]
        check(output_id in item_ids, f"Recipe {recipe['recipe_id']} outputs missing item {output_id}", errors)
        check(int(recipe["output_amount"]) > 0, f"Recipe {recipe['recipe_id']} output amount must be positive", errors)
        check(as_float(recipe["base_time_min_level5_building1"]) > 0, f"Recipe {recipe['recipe_id']} base time must be positive", errors)
        ids = split_ids(recipe["input_ids"])
        amounts = split_amounts(recipe["input_amounts_level5"])
        check(len(ids) == len(amounts), f"Recipe {recipe['recipe_id']} input id/amount length mismatch", errors)
        for item_id, amount in zip(ids, amounts):
            check(item_id in item_ids, f"Recipe {recipe['recipe_id']} references missing input {item_id}", errors)
            if item_id not in recipe_outputs:
                check(item_id in value_by_item_id, f"Recipe leaf input {item_id} has no FE value", errors)
            check(amount > 0, f"Recipe {recipe['recipe_id']} has non-positive amount for {item_id}", errors)

    cost_levels = read_csv(CONFIG_DIR / "item_recipe_cost_levels.csv")
    expected_cost_rows = len(recipes) * 6
    check(len(cost_levels) == expected_cost_rows, f"Item cost levels row count mismatch: {len(cost_levels)} != {expected_cost_rows}", errors)
    seen_cost = set()
    for row in cost_levels:
        recipe_id = row["recipe_id"]
        level = int(row["economy_level"])
        seen_cost.add((recipe_id, level))
        check(recipe_id in recipe_by_id, f"Cost levels reference unknown recipe {recipe_id}", errors)
        if recipe_id in recipe_by_id and level in EXPECTED_EFFICIENCY_MULTIPLIERS:
            base_amounts = split_amounts(recipe_by_id[recipe_id]["input_amounts_level5"])
            expected = [max(1, math.ceil(amount * EXPECTED_EFFICIENCY_MULTIPLIERS[level])) for amount in base_amounts]
            actual = split_amounts(row["input_amounts"])
            check(expected == actual, f"Cost level mismatch for {recipe_id} L{level}: {actual} != {expected}", errors)
    for recipe_id in recipe_by_id:
        for level in EXPECTED_EFFICIENCY_MULTIPLIERS:
            check((recipe_id, level) in seen_cost, f"Missing cost row for {recipe_id} L{level}", errors)

    time_levels = read_csv(CONFIG_DIR / "item_recipe_time_levels.csv")
    expected_time_rows = len(recipes) * 6
    check(len(time_levels) == expected_time_rows, f"Item time levels row count mismatch: {len(time_levels)} != {expected_time_rows}", errors)
    seen_time = set()
    for row in time_levels:
        recipe_id = row["recipe_id"]
        level = int(row["time_level"])
        seen_time.add((recipe_id, level))
        check(recipe_id in recipe_by_id, f"Time levels reference unknown recipe {recipe_id}", errors)
        if recipe_id in recipe_by_id and level in EXPECTED_TIME_MULTIPLIERS:
            base_time = as_float(recipe_by_id[recipe_id]["base_time_min_level5_building1"])
            expected_building1 = base_time * EXPECTED_TIME_MULTIPLIERS[level]
            expected_building30 = expected_building1 / 1.5
            check(approx(as_float(row["time_min_building1"]), expected_building1), f"Building1 time mismatch for {recipe_id} L{level}", errors)
            check(approx(as_float(row["time_min_building30"]), expected_building30), f"Building30 time mismatch for {recipe_id} L{level}", errors)
    for recipe_id in recipe_by_id:
        for level in EXPECTED_TIME_MULTIPLIERS:
            check((recipe_id, level) in seen_time, f"Missing time row for {recipe_id} L{level}", errors)

    engine = recipe_by_id.get("make_engine")
    if engine:
        check(engine["input_ids"] == "orkit;charcoal;automaton_mainspring;automaton_servo_core", "Engine input ids changed unexpectedly", errors)
        check(engine["input_amounts_level5"] == "10;50;5;2", "Engine level 5 amounts must be 10;50;5;2", errors)
        engine_times = {(row["recipe_id"], int(row["time_level"])): row for row in time_levels}
        row0 = engine_times.get(("make_engine", 0))
        row5 = engine_times.get(("make_engine", 5))
        if row0:
            check(approx(as_float(row0["time_min_building1"]), 45.0), "Engine max time must be 45 min at time L0/building L1", errors)
        if row5:
            check(approx(as_float(row5["time_min_building30"]), 12.0), "Engine min time must be 12 min at time L5/building L30", errors)

    rank_policy = {row["rank"]: row for row in read_csv(CONFIG_DIR / "ship_rank_craft_time_policy.csv")}
    check("R2" in rank_policy, "Missing R2 ship time policy", errors)
    check("R10" in rank_policy, "Missing R10 ship time policy", errors)
    if "R2" in rank_policy:
        check(approx(as_float(rank_policy["R2"]["time_min_level0_building1"]), 4.01, 0.05), "R2 minimal progression time should be about 4 minutes", errors)
    if "R10" in rank_policy:
        check(approx(as_float(rank_policy["R10"]["time_min_level5_building30"]), 600.0), "R10 full progression time must be 600 minutes", errors)

    licenses = read_csv(CONFIG_DIR / "faction_r10_licenses.csv")
    license_item_ids = [row["license_item_id"] for row in licenses]
    for row in licenses:
        check(row["license_item_id"] in item_ids, f"Missing R10 license item {row['license_item_id']}", errors)
        check(row["license_item_id"] in value_by_item_id, f"R10 license {row['license_item_id']} has no FE value", errors)
        check(row["price_currency"] == "solid", f"R10 license {row['license_item_id']} must cost solid", errors)
        check(int(row["price_amount"]) == 700, f"R10 license {row['license_item_id']} must cost 700 solid", errors)
        check(int(row["consumed_per_craft"]) == 1, f"R10 license {row['license_item_id']} must be consumed one per craft", errors)
    check(len(license_item_ids) == len(set(license_item_ids)), "Duplicate R10 license item ids", errors)

    r10_profiles = read_csv(CONFIG_DIR / "ship_r10_recipe_profiles.csv")
    profile_by_id = {row["profile_id"]: row for row in r10_profiles}
    check(set(profile_by_id) == {"r10_frigate", "r10_cruiser", "r10_battleship"}, "R10 profiles must be frigate/cruiser/battleship", errors)
    for profile in r10_profiles:
        ids = split_ids(profile["input_ids_level5"])
        amounts = split_amounts(profile["input_amounts_level5"])
        check(len(ids) == len(amounts), f"R10 profile {profile['profile_id']} input id/amount length mismatch", errors)
        for item_id, amount in zip(ids, amounts):
            check(item_id in item_ids, f"R10 profile {profile['profile_id']} references missing input {item_id}", errors)
            if item_id not in recipe_outputs:
                check(item_id in value_by_item_id, f"R10 profile leaf input {item_id} has no FE value", errors)
            check(amount > 0, f"R10 profile {profile['profile_id']} has non-positive amount for {item_id}", errors)
        check(approx(as_float(profile["time_min_level5_building30"]), 600.0), f"R10 profile {profile['profile_id']} full time must be 600 minutes", errors)
        check(approx(as_float(profile["time_min_level0_building1"]), 2250.0), f"R10 profile {profile['profile_id']} new recipe time must be 2250 minutes", errors)

    r10_cost_rows = read_csv(CONFIG_DIR / "ship_r10_recipe_cost_levels.csv")
    check(len(r10_cost_rows) == len(r10_profiles) * 6, "R10 cost level row count mismatch", errors)
    for row in r10_cost_rows:
        profile_id = row["profile_id"]
        level = int(row["economy_level"])
        check(profile_id in profile_by_id, f"R10 cost levels reference unknown profile {profile_id}", errors)
        check(int(row["required_license_amount"]) == 1, f"R10 profile {profile_id} license amount must stay 1", errors)
        if profile_id in profile_by_id and level in EXPECTED_EFFICIENCY_MULTIPLIERS:
            base_amounts = split_amounts(profile_by_id[profile_id]["input_amounts_level5"])
            expected = [max(1, math.ceil(amount * EXPECTED_EFFICIENCY_MULTIPLIERS[level])) for amount in base_amounts]
            actual = split_amounts(row["input_amounts"])
            check(expected == actual, f"R10 cost level mismatch for {profile_id} L{level}: {actual} != {expected}", errors)

    r10_time_rows = read_csv(CONFIG_DIR / "ship_r10_recipe_time_levels.csv")
    check(len(r10_time_rows) == len(r10_profiles) * 6, "R10 time level row count mismatch", errors)
    for row in r10_time_rows:
        profile_id = row["profile_id"]
        level = int(row["time_level"])
        check(profile_id in profile_by_id, f"R10 time levels reference unknown profile {profile_id}", errors)
        if profile_id in profile_by_id and level in EXPECTED_TIME_MULTIPLIERS:
            base_time = as_float(profile_by_id[profile_id]["base_time_min_level5_building1"])
            expected_building1 = base_time * EXPECTED_TIME_MULTIPLIERS[level]
            expected_building30 = expected_building1 / 1.5
            check(approx(as_float(row["time_min_building1"]), expected_building1), f"R10 building1 time mismatch for {profile_id} L{level}", errors)
            check(approx(as_float(row["time_min_building30"]), expected_building30), f"R10 building30 time mismatch for {profile_id} L{level}", errors)

    development_ships = [
        row for row in read_csv(ITEMS_PATH.parent / "Ship_tree.csv")
        if row["catalog_scope"] == "development" and int(row["rank"]) >= 2
    ]
    development_ship_ids = {row["id_ship"] for row in development_ships}

    ship_recipes = read_csv(CONFIG_DIR / "ship_crafting_recipes.csv")
    ship_recipe_by_id = {row["recipe_id"]: row for row in ship_recipes}
    ship_recipe_by_ship_id = {row["ship_id"]: row for row in ship_recipes}
    check(len(ship_recipes) == len(development_ships), f"Ship recipe count mismatch: {len(ship_recipes)} != {len(development_ships)}", errors)
    check({row["ship_id"] for row in ship_recipes} == development_ship_ids, "Ship recipes must cover every non-pioneer development ship exactly", errors)
    check(len(ship_recipe_by_id) == len(ship_recipes), "Duplicate recipe ids in ship_crafting_recipes.csv", errors)
    check(len(ship_recipe_by_ship_id) == len(ship_recipes), "Duplicate ship ids in ship_crafting_recipes.csv", errors)

    item_recipe_outputs = {row["output_item_id"] for row in recipes}
    for ship_recipe in ship_recipes:
        rank = int(ship_recipe["rank"])
        check(2 <= rank <= 10, f"Ship recipe {ship_recipe['recipe_id']} has unsupported rank {rank}", errors)
        expected_yard = CLASS_YARDS.get(ship_recipe["ship_class_id"])
        check(expected_yard is not None, f"Ship recipe {ship_recipe['recipe_id']} has unknown ship class {ship_recipe['ship_class_id']}", errors)
        if expected_yard is not None:
            check(ship_recipe["producer_building_id"] == expected_yard, f"Ship recipe {ship_recipe['recipe_id']} must be produced by {expected_yard}", errors)
        ids = split_ids(ship_recipe["input_ids_level5"])
        amounts = split_amounts(ship_recipe["input_amounts_level5"])
        check(len(ids) == len(amounts), f"Ship recipe {ship_recipe['recipe_id']} input id/amount length mismatch", errors)
        check(len(ids) > 0, f"Ship recipe {ship_recipe['recipe_id']} must have inputs", errors)
        for item_id, amount in zip(ids, amounts):
            check(item_id in item_ids, f"Ship recipe {ship_recipe['recipe_id']} references missing input {item_id}", errors)
            if item_id not in item_recipe_outputs:
                check(item_id in value_by_item_id, f"Ship recipe leaf input {item_id} has no FE value", errors)
            check(amount > 0, f"Ship recipe {ship_recipe['recipe_id']} has non-positive amount for {item_id}", errors)
        license_id = ship_recipe["required_license_item_id"]
        license_amount = int(ship_recipe["required_license_amount"])
        if rank == 10:
            check(license_id in license_item_ids, f"R10 ship recipe {ship_recipe['recipe_id']} must require faction flagship license", errors)
            check(license_amount == 1, f"R10 ship recipe {ship_recipe['recipe_id']} must consume one license", errors)
        else:
            check(license_id == "", f"Non-R10 ship recipe {ship_recipe['recipe_id']} must not require license", errors)
            check(license_amount == 0, f"Non-R10 ship recipe {ship_recipe['recipe_id']} license amount must be 0", errors)

    ship_cost_rows = read_csv(CONFIG_DIR / "ship_recipe_cost_levels.csv")
    check(len(ship_cost_rows) == len(ship_recipes) * 6, "Ship cost level row count mismatch", errors)
    seen_ship_cost = set()
    for row in ship_cost_rows:
        recipe_id = row["recipe_id"]
        level = int(row["economy_level"])
        seen_ship_cost.add((recipe_id, level))
        check(recipe_id in ship_recipe_by_id, f"Ship cost level references unknown recipe {recipe_id}", errors)
        if recipe_id in ship_recipe_by_id and level in EXPECTED_EFFICIENCY_MULTIPLIERS:
            base_amounts = split_amounts(ship_recipe_by_id[recipe_id]["input_amounts_level5"])
            expected = [max(1, math.ceil(amount * EXPECTED_EFFICIENCY_MULTIPLIERS[level])) for amount in base_amounts]
            actual = split_amounts(row["input_amounts"])
            check(expected == actual, f"Ship cost level mismatch for {recipe_id} L{level}: {actual} != {expected}", errors)
    for recipe_id in ship_recipe_by_id:
        for level in EXPECTED_EFFICIENCY_MULTIPLIERS:
            check((recipe_id, level) in seen_ship_cost, f"Missing ship cost row for {recipe_id} L{level}", errors)

    ship_time_rows = read_csv(CONFIG_DIR / "ship_recipe_time_levels.csv")
    check(len(ship_time_rows) == len(ship_recipes) * 6, "Ship time level row count mismatch", errors)
    seen_ship_time = set()
    for row in ship_time_rows:
        recipe_id = row["recipe_id"]
        level = int(row["time_level"])
        seen_ship_time.add((recipe_id, level))
        check(recipe_id in ship_recipe_by_id, f"Ship time level references unknown recipe {recipe_id}", errors)
        if recipe_id in ship_recipe_by_id and level in EXPECTED_TIME_MULTIPLIERS:
            base_time = as_float(ship_recipe_by_id[recipe_id]["base_time_min_level5_yard1"])
            expected_yard = ship_recipe_by_id[recipe_id]["producer_building_id"]
            expected_yard1 = base_time * EXPECTED_TIME_MULTIPLIERS[level]
            expected_yard30 = expected_yard1 / 1.5
            check(row["producer_building_id"] == expected_yard, f"Ship time row {recipe_id} L{level} must use {expected_yard}", errors)
            check(approx(as_float(row["time_min_yard1"]), expected_yard1), f"Yard1 time mismatch for {recipe_id} L{level}", errors)
            check(approx(as_float(row["time_min_yard30"]), expected_yard30), f"Yard30 time mismatch for {recipe_id} L{level}", errors)
    for recipe_id in ship_recipe_by_id:
        for level in EXPECTED_TIME_MULTIPLIERS:
            check((recipe_id, level) in seen_ship_time, f"Missing ship time row for {recipe_id} L{level}", errors)

    ship_fe_rows = read_csv(CONFIG_DIR / "ship_recipe_fe_summary.csv")
    check(len(ship_fe_rows) == len(ship_recipes) * 6, "Ship FE summary row count mismatch", errors)
    rank_fe_rows = read_csv(CONFIG_DIR / "ship_rank_fe_summary.csv")
    check(len(rank_fe_rows) >= 9 * 3 * 6, "Ship rank FE summary must cover ranks/classes/levels", errors)
    ship_time_summary_rows = read_csv(CONFIG_DIR / "ship_time_summary.csv")
    check(len(ship_time_summary_rows) == 27, "Ship time summary must cover 3 classes x 9 ranks", errors)
    for row in ship_time_summary_rows:
        ship_class_id = row["ship_class_id"]
        rank = int(row["rank"])
        expected_yard = CLASS_YARDS.get(ship_class_id)
        check(expected_yard is not None, f"Ship time summary has unknown ship class {ship_class_id}", errors)
        if expected_yard is not None:
            check(row["producer_building_id"] == expected_yard, f"Ship time summary {ship_class_id} R{rank} must use {expected_yard}", errors)
        expected_fastest = as_float(rank_policy[f"R{rank}"]["base_time_min_level5_building1"]) / 1.5
        expected_slowest = as_float(rank_policy[f"R{rank}"]["base_time_min_level5_building1"]) * EXPECTED_TIME_MULTIPLIERS[0]
        check(approx(as_float(row["min_time_level5_yard30_min"]), expected_fastest), f"Fastest time mismatch for {ship_class_id} R{rank}", errors)
        check(approx(as_float(row["max_time_level0_yard1_min"]), expected_slowest), f"Slowest time mismatch for {ship_class_id} R{rank}", errors)

    full_chain_time_rows = read_csv(CONFIG_DIR / "ship_full_chain_time.csv")
    check(len(full_chain_time_rows) == len(ship_recipes) * 2, "Full chain time must contain min/max scenario for every ship", errors)
    full_chain_keys = {(row["recipe_id"], row["scenario_id"]) for row in full_chain_time_rows}
    for recipe_id in ship_recipe_by_id:
        check((recipe_id, "min_mastered_chain") in full_chain_keys, f"Missing mastered full-chain time for {recipe_id}", errors)
        check((recipe_id, "max_untrained_chain") in full_chain_keys, f"Missing untrained full-chain time for {recipe_id}", errors)
    for row in full_chain_time_rows:
        final_assembly = as_float(row["final_assembly_min"])
        item_work = as_float(row["item_work_min"])
        total_work = as_float(row["total_work_min"])
        staged_parallel = as_float(row["staged_parallel_min"])
        check(final_assembly > 0, f"Full-chain row {row['recipe_id']} has non-positive final assembly time", errors)
        check(item_work >= 0, f"Full-chain row {row['recipe_id']} has negative item work time", errors)
        check(total_work >= final_assembly, f"Full-chain total work must include final assembly for {row['recipe_id']}", errors)
        check(staged_parallel >= final_assembly, f"Full-chain staged time must include final assembly for {row['recipe_id']}", errors)
        check(total_work + 0.01 >= staged_parallel, f"Full-chain total work must be at least staged time for {row['recipe_id']}", errors)

    full_chain_summary_rows = read_csv(CONFIG_DIR / "ship_full_chain_time_summary.csv")
    check(len(full_chain_summary_rows) == 54, "Full chain time summary must cover 3 classes x 9 ranks x 2 scenarios", errors)
    for scenario_id in ("min_mastered_chain", "max_untrained_chain"):
        rank_avg: dict[int, list[float]] = {}
        for row in full_chain_time_rows:
            if row["scenario_id"] == scenario_id:
                rank_avg.setdefault(int(row["rank"]), []).append(as_float(row["staged_parallel_min"]))
        previous_avg = 0.0
        for rank in range(2, 11):
            check(rank in rank_avg, f"Missing full-chain rows for rank {rank} scenario {scenario_id}", errors)
            if rank in rank_avg:
                current_avg = sum(rank_avg[rank]) / len(rank_avg[rank])
                check(current_avg > previous_avg, f"Average staged full-chain time must increase by rank for {scenario_id}: R{rank}={current_avg} <= {previous_avg}", errors)
                previous_avg = current_avg
    for level in (0, 5):
        rank_avg: dict[int, list[float]] = {}
        for row in ship_fe_rows:
            if int(row["economy_level"]) == level:
                rank_avg.setdefault(int(row["rank"]), []).append(as_float(row["total_fe"]))
        previous_avg = 0.0
        for rank in range(2, 11):
            check(rank in rank_avg, f"Missing FE rows for rank {rank} economy level {level}", errors)
            if rank in rank_avg:
                current_avg = sum(rank_avg[rank]) / len(rank_avg[rank])
                check(current_avg > previous_avg, f"Average FE must increase by rank at economy level {level}: R{rank}={current_avg} <= {previous_avg}", errors)
                previous_avg = current_avg

    quick_reward_model = read_csv(CONFIG_DIR / "quick_sortie_reward_model.csv")
    check(len(quick_reward_model) == 9, "Quick sortie reward model must cover ranks 2..10", errors)
    previous_quick_base = 0.0
    for row in quick_reward_model:
        rank = int(row["rank"])
        check(2 <= rank <= 10, f"Quick reward model has unexpected rank {rank}", errors)
        check(int(row["fe_scale_multiplier"]) == 10, "Quick reward model must explicitly use x10 FE denomination", errors)
        quick_base = as_float(row["quick_base_fe"])
        check(quick_base > previous_quick_base, f"Quick base FE must increase by rank: R{rank}={quick_base} <= {previous_quick_base}", errors)
        previous_quick_base = quick_base

    mission_profiles = ("quick", "normal", "danger", "elite")
    mission_profile_model = read_csv(CONFIG_DIR / "mission_profile_reward_model.csv")
    check(len(mission_profile_model) == 12, "Mission profile reward model must contain 9 adaptive quick rows plus 3 fixed difficulty rows", errors)
    quick_model_rows = [row for row in mission_profile_model if row["mission_profile"] == "quick"]
    check(len(quick_model_rows) == 9, "Mission profile reward model must contain quick rows for ship ranks 2..10", errors)
    previous_quick_mission_base = 0.0
    for rank in range(2, 11):
        rows = [row for row in quick_model_rows if row["ship_rank"] == str(rank)]
        check(len(rows) == 1, f"Missing adaptive quick model for ship rank {rank}", errors)
        if rows:
            row = rows[0]
            tuned_base = as_float(row["tuned_base_fe"])
            check(row["scope"] == "adaptive_ship_rank", "Quick mission model must be rank-adaptive", errors)
            check(tuned_base > previous_quick_mission_base, f"Quick tuned base FE must increase by ship rank: R{rank}={tuned_base} <= {previous_quick_mission_base}", errors)
            previous_quick_mission_base = tuned_base
    fixed_model = {
        row["mission_profile"]: row
        for row in mission_profile_model
        if row["mission_profile"] in ("normal", "danger", "elite")
    }
    check(set(fixed_model) == {"normal", "danger", "elite"}, "Mission model must contain fixed normal/danger/elite rows", errors)
    for mission_profile, row in fixed_model.items():
        check(row["scope"] == "fixed_world_difficulty", f"{mission_profile} must be fixed world difficulty, not rank adaptive", errors)
        check(row["ship_rank"] == "any", f"{mission_profile} model must use ship_rank=any", errors)
        check(as_float(row["capacity_multiplier"]) >= 1.0, f"{mission_profile} capacity multiplier must be at least 1", errors)
    if set(fixed_model) == {"normal", "danger", "elite"}:
        normal_base = as_float(fixed_model["normal"]["tuned_base_fe"])
        danger_base = as_float(fixed_model["danger"]["tuned_base_fe"])
        elite_base = as_float(fixed_model["elite"]["tuned_base_fe"])
        check(normal_base < danger_base < elite_base, "Fixed mission FE order must be normal < danger < elite", errors)
        check(as_float(fixed_model["normal"]["required_power"]) < as_float(fixed_model["danger"]["required_power"]) < as_float(fixed_model["elite"]["required_power"]), "Fixed mission required power order must be normal < danger < elite", errors)

    extraction_policy_rows = read_csv(CONFIG_DIR / "sortie_extraction_outcome_policy.csv")
    extraction_layers = {row["reward_layer"] for row in extraction_policy_rows}
    check(
        extraction_layers == {"material_cargo", "intangible_progress", "successful_sortie_total", "destroyed_sortie_total"},
        "Sortie extraction outcome policy must define material, intangible, successful total, and destroyed total layers",
        errors,
    )

    mission_requirement_rows = read_csv(CONFIG_DIR / "mission_archetype_requirements.csv")
    requirement_difficulties = ("normal", "danger", "elite")
    requirement_activities = ("combat", "mining", "gas", "hunting", "salvage", "relic", "courier", "repair")
    allowed_requirement_stats = {
        "defense",
        "mobility",
        "stealth",
        "cargo",
        "warfare",
        "mining",
        "harvesting",
        "hunting",
        "hacking",
        "salvage",
        "survey",
        "repair",
    }
    requirement_groups: dict[tuple[str, str], list[dict[str, str]]] = {}
    for row in mission_requirement_rows:
        difficulty = row["difficulty"]
        activity = row["primary_activity"]
        stat_id = row["stat_id"]
        check(difficulty in requirement_difficulties, f"Unknown mission requirement difficulty {difficulty}", errors)
        check(activity in requirement_activities, f"Unknown mission requirement activity {activity}", errors)
        check(stat_id in allowed_requirement_stats, f"Unknown mission requirement stat {stat_id}", errors)
        check(as_float(row["required_value"]) > 0, f"Mission requirement value must be positive for {difficulty}/{activity}/{stat_id}", errors)
        check(as_float(row["weight"]) > 0, f"Mission requirement weight must be positive for {difficulty}/{activity}/{stat_id}", errors)
        check(row["hard_gate"] in ("yes", "no"), f"Mission requirement hard_gate must be yes/no for {difficulty}/{activity}/{stat_id}", errors)
        if difficulty in requirement_difficulties and activity in requirement_activities:
            requirement_groups.setdefault((difficulty, activity), []).append(row)
            check(row["mission_archetype"].startswith(f"{difficulty}_"), f"Mission archetype {row['mission_archetype']} must start with {difficulty}_", errors)
    for difficulty in requirement_difficulties:
        for activity in requirement_activities:
            group = requirement_groups.get((difficulty, activity), [])
            check(group, f"Missing mission requirements for {difficulty}/{activity}", errors)
            if not group:
                continue
            weight_total = sum(as_float(row["weight"]) for row in group)
            check(approx(weight_total, 1.0), f"Mission requirement weights must sum to 1 for {difficulty}/{activity}, got {weight_total}", errors)
            check(any(row["hard_gate"] == "yes" for row in group), f"Mission requirement group needs a hard gate for {difficulty}/{activity}", errors)

    mission_ship_profit = read_csv(CONFIG_DIR / "mission_profile_ship_profit.csv")
    check(len(mission_ship_profit) == len(ship_recipes) * 4, "Mission profile profit must contain every non-pioneer ship for all 4 mission profiles", errors)
    mission_profit_keys = {(row["mission_profile"], row["ship_id"]) for row in mission_ship_profit}
    for mission_profile in mission_profiles:
        for ship_id in ship_recipe_by_ship_id:
            check((mission_profile, ship_id) in mission_profit_keys, f"Missing mission profit row for {mission_profile} {ship_id}", errors)
    for row in mission_ship_profit:
        ship_id = row["ship_id"]
        total_value = as_float(row["total_value_fe"])
        liquid = as_float(row["liquid_fe"])
        accumulated_total = as_float(row["accumulated_total_value_fe"])
        material_value = as_float(row["material_value_fe"])
        intangible_value = as_float(row["intangible_value_fe"])
        destroyed_total = as_float(row["destroyed_total_value_fe"])
        destroyed_liquid = as_float(row["destroyed_liquid_fe"])
        extracted_total = as_float(row["extracted_total_value_fe"])
        extracted_liquid = as_float(row["extracted_liquid_fe"])
        freight = as_float(row["freight_fe"])
        extracted_freight = as_float(row["extracted_freight_fe"])
        resource = as_float(row["resource_fe"])
        profile_currency = as_float(row["profile_currency_fe"])
        info = as_float(row["info_fe"])
        reputation = as_float(row["reputation_fe"])
        extracted_reputation = as_float(row["extracted_reputation_fe"])
        extraction_multiplier = as_float(row["extraction_intangible_multiplier"])
        extraction_bonus = as_float(row["extraction_intangible_bonus_fe"])
        craft_full = as_float(row["craft_full_eff_fe"])
        craft_payback = as_float(row["craft_payback_sorties"])
        craft_total_payback = as_float(row["craft_total_value_payback_sorties"])
        check(row["mission_profile"] in mission_profiles, f"Unknown mission profile {row['mission_profile']}", errors)
        success_factor = as_float(row["mission_success_factor"])
        check(0 < success_factor <= 1.0, f"Mission success factor must be in (0, 1] for {row['mission_profile']} {ship_id}", errors)
        if row["mission_profile"] == "quick":
            check(approx(success_factor, 1.0), f"Quick success factor must be 1 for {ship_id}", errors)
            check(row["mission_scope"] == "adaptive_ship_rank", f"Quick row must be rank-adaptive for {ship_id}", errors)
            check(row["mission_archetype"] == "quick_adaptive", f"Quick row must use quick_adaptive archetype for {ship_id}", errors)
        else:
            check(row["mission_scope"] == "fixed_world_difficulty", f"{row['mission_profile']} row must be fixed difficulty for {ship_id}", errors)
            requirement_key = (row["mission_profile"], row["primary_activity"])
            check(requirement_key in requirement_groups, f"Mission profit row has no requirement group for {requirement_key} {ship_id}", errors)
            if requirement_key in requirement_groups:
                expected_archetype = requirement_groups[requirement_key][0]["mission_archetype"]
                check(row["mission_archetype"] == expected_archetype, f"Mission archetype mismatch for {row['mission_profile']} {ship_id}: {row['mission_archetype']} != {expected_archetype}", errors)
            check(row.get("requirement_details", "") != "", f"Mission row must keep requirement details for {row['mission_profile']} {ship_id}", errors)
        check(total_value > 0, f"Mission profile total value must be positive for {row['mission_profile']} {ship_id}", errors)
        check(liquid > 0, f"Mission profile liquid value must be positive for {row['mission_profile']} {ship_id}", errors)
        check(total_value + 0.01 >= liquid, f"Mission profile liquid value cannot exceed total value for {row['mission_profile']} {ship_id}", errors)
        check(approx(extraction_multiplier, 2.0), f"Extraction intangible multiplier must be 2 for {row['mission_profile']} {ship_id}", errors)
        check(approx(material_value, resource + profile_currency + info, 0.05), f"Material value split mismatch for {row['mission_profile']} {ship_id}", errors)
        check(approx(accumulated_total, material_value + intangible_value, 0.05), f"Accumulated value split mismatch for {row['mission_profile']} {ship_id}", errors)
        check(approx(extraction_bonus, intangible_value, 0.05), f"Extraction bonus must equal base intangible value for {row['mission_profile']} {ship_id}", errors)
        check(approx(destroyed_total, intangible_value, 0.05), f"Destroyed sortie must keep only intangible value for {row['mission_profile']} {ship_id}", errors)
        check(approx(destroyed_liquid, freight, 0.05), f"Destroyed liquid must keep only base freight for {row['mission_profile']} {ship_id}", errors)
        check(approx(extracted_freight, freight * 2.0, 0.05), f"Extracted freight must be doubled for {row['mission_profile']} {ship_id}", errors)
        check(approx(extracted_reputation, reputation * 2.0, 0.05), f"Extracted reputation must be doubled for {row['mission_profile']} {ship_id}", errors)
        check(approx(extracted_total, material_value + intangible_value * 2.0, 0.05), f"Extracted total must be material plus double intangible for {row['mission_profile']} {ship_id}", errors)
        check(approx(total_value, extracted_total, 0.05), f"Legacy total_value_fe must mirror extracted total for {row['mission_profile']} {ship_id}", errors)
        check(approx(extracted_liquid, extracted_freight + resource + profile_currency, 0.05), f"Extracted liquid split mismatch for {row['mission_profile']} {ship_id}", errors)
        check(approx(liquid, extracted_liquid, 0.05), f"Legacy liquid_fe must mirror extracted liquid for {row['mission_profile']} {ship_id}", errors)
        check(int(row["destroyed_ship_xp"]) == int(row["base_ship_xp"]), f"Destroyed ship XP must keep base XP for {row['mission_profile']} {ship_id}", errors)
        check(int(row["extracted_ship_xp"]) == int(row["base_ship_xp"]) * 2, f"Extracted ship XP must be doubled for {row['mission_profile']} {ship_id}", errors)
        check(int(row["ship_xp"]) == int(row["extracted_ship_xp"]), f"Legacy ship_xp must mirror extracted ship XP for {row['mission_profile']} {ship_id}", errors)
        check(int(row["destroyed_mastery_points"]) == int(row["base_mastery_points"]), f"Destroyed mastery must keep base mastery for {row['mission_profile']} {ship_id}", errors)
        check(int(row["extracted_mastery_points"]) == int(row["base_mastery_points"]) * 2, f"Extracted mastery must be doubled for {row['mission_profile']} {ship_id}", errors)
        check(int(row["mastery_points"]) == int(row["extracted_mastery_points"]), f"Legacy mastery must mirror extracted mastery for {row['mission_profile']} {ship_id}", errors)
        check(approx(craft_payback, craft_full / liquid, 0.03), f"Mission profile liquid payback mismatch for {row['mission_profile']} {ship_id}", errors)
        check(approx(craft_total_payback, craft_full / total_value, 0.03), f"Mission profile total payback mismatch for {row['mission_profile']} {ship_id}", errors)

    mission_rank_summary = read_csv(CONFIG_DIR / "mission_profile_rank_payback_summary.csv")
    check(len(mission_rank_summary) == 36, "Mission profile rank summary must cover 4 profiles x 9 ranks", errors)
    for mission_profile in mission_profiles:
        rows = sorted([row for row in mission_rank_summary if row["mission_profile"] == mission_profile], key=lambda row: int(row["rank"]))
        check(len(rows) == 9, f"Mission profile rank summary must contain 9 rows for {mission_profile}", errors)
        previous_liquid = 0.0
        for row in rows:
            rank = int(row["rank"])
            liquid = as_float(row["avg_liquid_fe"])
            check(int(row["ship_count"]) == 51, f"Mission rank summary {mission_profile} R{rank} must contain 51 ships", errors)
            check(liquid > previous_liquid, f"Average mission liquid FE must increase by rank for {mission_profile}: R{rank}={liquid} <= {previous_liquid}", errors)
            previous_liquid = liquid
    elite_r10_rows = [row for row in mission_rank_summary if row["mission_profile"] == "elite" and int(row["rank"]) == 10]
    check(len(elite_r10_rows) == 1, "Mission rank summary must contain elite R10 row", errors)
    if elite_r10_rows:
        elite_r10 = elite_r10_rows[0]
        elite_total_payback = as_float(elite_r10["avg_craft_total_value_payback_sorties"])
        elite_liquid_payback = as_float(elite_r10["avg_craft_payback_sorties"])
        check(4.0 <= elite_total_payback <= 5.5, f"Elite R10 total-value craft payback should be about 5 sorties, got {elite_total_payback}", errors)
        check(elite_liquid_payback <= 8.5, f"Elite R10 liquid craft payback should stay under 8.5 sorties, got {elite_liquid_payback}", errors)

    mission_class_summary = read_csv(CONFIG_DIR / "mission_profile_class_payback_summary.csv")
    check(len(mission_class_summary) == 108, "Mission class summary must cover 4 profiles x 3 classes x 9 ranks", errors)
    mission_faction_summary = read_csv(CONFIG_DIR / "mission_profile_faction_payback_summary.csv")
    check(len(mission_faction_summary) == 216, "Mission faction summary must cover 4 profiles x 6 factions x 9 ranks", errors)
    mission_activity_summary = read_csv(CONFIG_DIR / "mission_profile_activity_payback_summary.csv")
    check(len(mission_activity_summary) > 0, "Mission activity summary must not be empty", errors)

    elite_capacity_rows = read_csv(CONFIG_DIR / "elite_r10_capacity_check.csv")
    check(len(elite_capacity_rows) == 51, "Elite R10 capacity check must cover every R10 ship", errors)
    elite_success_by_ship = {
        row["ship_id"]: as_float(row["mission_success_factor"])
        for row in elite_capacity_rows
    }
    for row in elite_capacity_rows:
        capacity_vs_target = as_float(row["capacity_vs_five_sortie_craft_target"])
        capacity_vs_required = as_float(row["capacity_vs_required_value"])
        success_factor = as_float(row["mission_success_factor"])
        if success_factor >= 0.85:
            check(capacity_vs_target >= 1.0, f"Elite R10 capacity cannot cover five-sortie craft target for {row['ship_id']}: {capacity_vs_target}", errors)
        check(capacity_vs_required >= 1.0, f"Elite R10 capacity cannot cover required mission value for {row['ship_id']}: {capacity_vs_required}", errors)

    elite_payload_rows = read_csv(CONFIG_DIR / "elite_r10_required_payloads.csv")
    check(len(elite_payload_rows) == 51, "Elite R10 required payloads must cover every R10 ship", errors)
    for row in elite_payload_rows:
        target = as_float(row["target_fe_per_elite_sortie_for_5_sortie_payback"])
        payload_value = as_float(row["payload_value_fe"])
        check(target > 0, f"Elite payload target must be positive for {row['ship_id']}", errors)
        check(payload_value > 0, f"Elite payload value must be positive for {row['ship_id']}", errors)
        if elite_success_by_ship.get(row["ship_id"], 0.0) >= 0.85:
            check(payload_value + 0.01 >= target, f"Elite payload must cover five-sortie target for {row['ship_id']}", errors)
            check(as_float(row["payload_vs_target"]) >= 1.0, f"Elite payload ratio must be at least 1 for {row['ship_id']}", errors)

    quick_ship_profit = read_csv(CONFIG_DIR / "quick_sortie_ship_profit.csv")
    check(len(quick_ship_profit) == len(ship_recipes), "Quick sortie profit must contain every non-pioneer ship recipe", errors)
    quick_profit_by_ship = {row["ship_id"]: row for row in quick_ship_profit}
    check(len(quick_profit_by_ship) == len(quick_ship_profit), "Duplicate ship ids in quick sortie profit", errors)
    for ship_id, recipe in ship_recipe_by_ship_id.items():
        check(ship_id in quick_profit_by_ship, f"Missing quick sortie profit row for {ship_id}", errors)
        if ship_id not in quick_profit_by_ship:
            continue
        row = quick_profit_by_ship[ship_id]
        total_value = as_float(row["total_value_fe"])
        liquid = as_float(row["liquid_fe"])
        craft_full = as_float(row["craft_full_eff_fe"])
        craft_raw = as_float(row["craft_raw_fe"])
        buy_estimate = as_float(row["buy_estimate_fe"])
        craft_payback = as_float(row["craft_payback_sorties"])
        raw_payback = as_float(row["raw_craft_payback_sorties"])
        buy_payback = as_float(row["buy_payback_sorties"])
        check(total_value > 0, f"Quick sortie total value must be positive for {ship_id}", errors)
        check(liquid > 0, f"Quick sortie liquid value must be positive for {ship_id}", errors)
        check(total_value + 0.01 >= liquid, f"Quick sortie liquid value cannot exceed total value for {ship_id}", errors)
        check(craft_full > 0, f"Quick sortie craft full FE must be positive for {ship_id}", errors)
        check(craft_raw >= craft_full, f"Quick sortie raw craft FE must be >= full craft FE for {ship_id}", errors)
        check(buy_estimate >= craft_full, f"Quick sortie buy estimate must be >= craft FE for {ship_id}", errors)
        check(approx(craft_payback, craft_full / liquid, 0.03), f"Craft payback mismatch for {ship_id}", errors)
        check(approx(raw_payback, craft_raw / liquid, 0.03), f"Raw craft payback mismatch for {ship_id}", errors)
        check(approx(buy_payback, buy_estimate / liquid, 0.03), f"Buy payback mismatch for {ship_id}", errors)
        check(row["primary_activity"] in ("combat", "mining", "gas", "hunting", "salvage", "relic", "repair", "courier"), f"Unknown primary activity for {ship_id}", errors)

    quick_rank_summary = read_csv(CONFIG_DIR / "quick_sortie_rank_payback_summary.csv")
    check(len(quick_rank_summary) == 9, "Quick sortie rank payback summary must cover ranks 2..10", errors)
    previous_liquid = 0.0
    previous_craft_payback = 0.0
    previous_buy_payback = 0.0
    for row in quick_rank_summary:
        rank = int(row["rank"])
        liquid = as_float(row["avg_liquid_fe"])
        craft_payback = as_float(row["avg_craft_payback_sorties"])
        buy_payback = as_float(row["avg_buy_payback_sorties"])
        check(int(row["ship_count"]) == 51, f"Quick rank summary R{rank} must contain 51 ships", errors)
        check(liquid > previous_liquid, f"Average quick liquid FE must increase by rank: R{rank}={liquid} <= {previous_liquid}", errors)
        check(craft_payback > previous_craft_payback, f"Average craft payback must increase by rank: R{rank}={craft_payback} <= {previous_craft_payback}", errors)
        check(buy_payback > previous_buy_payback, f"Average buy payback must increase by rank: R{rank}={buy_payback} <= {previous_buy_payback}", errors)
        previous_liquid = liquid
        previous_craft_payback = craft_payback
        previous_buy_payback = buy_payback

    quick_class_summary = read_csv(CONFIG_DIR / "quick_sortie_class_payback_summary.csv")
    check(len(quick_class_summary) == 27, "Quick class payback summary must cover 3 classes x 9 ranks", errors)
    quick_faction_summary = read_csv(CONFIG_DIR / "quick_sortie_faction_payback_summary.csv")
    check(len(quick_faction_summary) == 54, "Quick faction payback summary must cover 6 factions x 9 ranks", errors)
    quick_activity_summary = read_csv(CONFIG_DIR / "quick_sortie_activity_payback_summary.csv")
    check(len(quick_activity_summary) > 0, "Quick activity payback summary must not be empty", errors)

    for filename in NEW_CONFIG_FILES:
        path = CONFIG_DIR / filename
        if path.exists():
            text = path.read_text(encoding="utf-8-sig")
            if re.search(r"\bT[1-9]\b", text):
                errors.append(f"Internal tier marker leaked into {filename}")
        else:
            errors.append(f"Missing generated config {filename}")

    return errors


if __name__ == "__main__":
    validation_errors = validate()
    if validation_errors:
        print("Recipe progression validation failed:")
        for error in validation_errors:
            print(f"- {error}")
        sys.exit(1)

    print("Recipe progression validation passed.")
