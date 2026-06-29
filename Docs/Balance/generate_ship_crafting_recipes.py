from __future__ import annotations

import csv
import hashlib
import math
from collections import defaultdict
from functools import lru_cache
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"
SHIP_TREE_PATH = ROOT / "Assets" / "Data" / "Config" / "Ship_tree.csv"

TIME_CURVE = {
    0: 2.50,
    1: 2.20,
    2: 1.90,
    3: 1.60,
    4: 1.30,
    5: 1.00,
}

ECONOMY_CURVE = {
    0: 2.00,
    1: 1.80,
    2: 1.60,
    3: 1.40,
    4: 1.20,
    5: 1.00,
}

# These are the old rank craft targets after the FE scale was moved one decimal up.
# The value is the desired full-efficiency material/license-free craft value for an
# average cruiser-like ship of that rank.
RANK_TARGET_FE = {
    2: 7_000,
    3: 18_000,
    4: 45_000,
    5: 110_000,
    6: 280_000,
    7: 700_000,
    8: 1_750_000,
    9: 4_400_000,
    10: 11_000_000,
}

CLASS_TARGET_MULTIPLIER = {
    "frigate": 0.74,
    "cruiser": 1.00,
    "battleship": 1.36,
}

CLASS_YARDS = {
    "frigate": ("frigate_yard", "Фрегатная верфь"),
    "cruiser": ("cruiser_yard", "Крейсерская верфь"),
    "battleship": ("battleship_yard", "Линкорная верфь"),
}

R10_LICENSE_BY_FACTION = {
    "capital": "capital_flagship_license",
    "wind_houses": "wind_houses_flagship_license",
    "mist_synod": "mist_synod_flagship_license",
    "stone_vault": "stone_vault_flagship_license",
    "factory_ark": "factory_ark_flagship_license",
    "devourers": "devourers_flagship_license",
}

RAW_GROUPS = {
    "base": [("iron", 2.0), ("charcoal", 0.7), ("copper", 0.6), ("calcite", 0.45), ("magnesium", 0.35)],
    "defense": [("iron", 1.4), ("calcite", 0.9), ("nickel", 0.45), ("bone_grit", 0.55), ("monazite", 0.18)],
    "mobility": [("magnesium", 1.2), ("claudium", 0.75), ("aerosil", 0.45), ("fulgur", 0.25)],
    "stealth": [("quartz", 0.7), ("magnesium", 0.55), ("vespar", 0.35), ("aerosil", 0.35)],
    "warfare": [("copper", 0.8), ("charcoal", 0.8), ("sylvine", 0.35), ("tin", 0.25), ("acid", 0.25), ("monazite", 0.18)],
    "mining": [("iron", 1.0), ("charcoal", 0.8), ("calcite", 0.65), ("quartz", 0.4), ("magnesium", 0.25)],
    "harvesting": [("aerosil", 0.65), ("vespar", 0.8), ("fulgur", 0.45), ("ionide", 0.25), ("nulgas", 0.12)],
    "hunting": [("leviathan_sinew", 0.8), ("leviathan_fat", 0.55), ("acid", 0.45), ("bone_grit", 0.35), ("leviathan_hide", 0.25)],
    "hacking": [("quartz", 0.75), ("copper", 0.55), ("ionide", 0.55), ("automaton_relay", 0.35), ("automaton_contact_comb", 0.3)],
    "salvage": [("automaton_coil", 0.5), ("automaton_brass_valve", 0.4), ("automaton_mainspring", 0.35), ("automaton_calibration_gear", 0.3)],
    "survey": [("quartz", 0.8), ("copper", 0.45), ("ionide", 0.6), ("automaton_optic_lens", 0.25), ("nickel", 0.35)],
    "repair": [("acid", 0.55), ("leviathan_ichor", 0.45), ("leviathan_hide", 0.3), ("vespar", 0.35), ("sylvine", 0.25)],
    "cargo": [("iron", 1.0), ("magnesium", 0.65), ("calcite", 0.45), ("sylvine", 0.25)],
    "hangar": [("automaton_relay", 0.35), ("automaton_contact_comb", 0.35), ("magnesium", 0.5), ("quartz", 0.35)],
}

MATERIAL_GROUPS = {
    "base": [("steel", 1.7), ("bronze", 0.45), ("brass", 0.45), ("resin", 0.35), ("bakelite", 0.35)],
    "defense": [("steel", 1.2), ("bulat", 0.9), ("bakelite", 0.45), ("rubber", 0.35), ("glass_ceramic", 0.3)],
    "mobility": [("elektron", 1.1), ("orkit", 0.85), ("invar", 0.45), ("aramid", 0.45), ("siloxane", 0.4)],
    "stealth": [("siloxane", 0.85), ("elektron", 0.7), ("textolite", 0.4), ("resin", 0.35)],
    "warfare": [("steel", 0.8), ("bulat", 0.9), ("brass", 0.45), ("aramid", 0.45), ("glass_ceramic", 0.25), ("munition_bundle", 0.35), ("weapon", 0.35)],
    "mining": [("steel", 0.9), ("bulat", 0.4), ("brass", 0.35), ("rubber", 0.35), ("resin", 0.25)],
    "harvesting": [("resin", 0.65), ("rubber", 0.65), ("fluoroplastic", 0.65), ("siloxane", 0.5), ("glass_ceramic", 0.35)],
    "hunting": [("aramid", 0.9), ("rubber", 0.55), ("resin", 0.4), ("bulat", 0.45), ("leviathan_sinew", 0.25)],
    "hacking": [("melchior", 0.75), ("invar", 0.75), ("textolite", 0.7), ("glass_ceramic", 0.45)],
    "salvage": [("textolite", 0.6), ("melchior", 0.5), ("invar", 0.55), ("automaton_servo_joint", 0.3), ("automaton_calibration_gear", 0.25)],
    "survey": [("melchior", 0.7), ("invar", 0.7), ("glass_ceramic", 0.65), ("textolite", 0.45)],
    "repair": [("resin", 0.55), ("rubber", 0.55), ("fluoroplastic", 0.45), ("leviathan_ichor", 0.3), ("siloxane", 0.25)],
    "cargo": [("steel", 0.75), ("elektron", 0.65), ("rubber", 0.45), ("brass", 0.35), ("resin", 0.3)],
    "hangar": [("textolite", 0.65), ("elektron", 0.55), ("rubber", 0.45), ("mechanisms", 0.45), ("brass", 0.3)],
}

PART_GROUPS = {
    "base": [("beam", 1.4), ("plating", 1.0), ("bulkhead", 0.9), ("deck_section", 0.8), ("hatch", 0.45)],
    "defense": [("armor_plate", 1.2), ("bulkhead", 1.0), ("pump", 0.35), ("sensor", 0.25), ("hatch", 0.35)],
    "mobility": [("engine", 1.2), ("transmission", 0.9), ("gyroscope", 0.55), ("actuator", 0.7), ("cowling", 0.45)],
    "stealth": [("cowling", 0.85), ("plating", 0.65), ("sensor", 0.25), ("gyroscope", 0.25)],
    "warfare": [("armor_plate", 0.65), ("deck_section", 0.65), ("hatch", 0.4), ("actuator", 0.75), ("gyroscope", 0.55), ("calculator", 0.45), ("weapon", 0.4), ("munition_bundle", 0.45)],
    "mining": [("beam", 0.75), ("bulkhead", 0.65), ("airlock", 0.45), ("actuator", 0.55), ("pump", 0.55)],
    "harvesting": [("compressor", 1.0), ("pump", 0.75), ("airlock", 0.45), ("cowling", 0.45), ("sensor", 0.35)],
    "hunting": [("actuator", 0.85), ("transmission", 0.65), ("pump", 0.45), ("airlock", 0.45), ("armor_plate", 0.4)],
    "hacking": [("sensor", 0.95), ("calculator", 0.95), ("gyroscope", 0.35), ("cowling", 0.25)],
    "salvage": [("actuator", 0.75), ("calculator", 0.55), ("sensor", 0.45), ("pump", 0.35), ("airlock", 0.35)],
    "survey": [("sensor", 1.1), ("calculator", 0.55), ("gyroscope", 0.45), ("cowling", 0.35)],
    "repair": [("pump", 0.85), ("sensor", 0.45), ("airlock", 0.4), ("calculator", 0.35)],
    "cargo": [("bulkhead", 0.8), ("deck_section", 0.75), ("airlock", 0.6), ("hatch", 0.65), ("actuator", 0.35), ("pump", 0.25)],
    "hangar": [("deck_section", 0.75), ("airlock", 0.65), ("hatch", 0.75), ("actuator", 0.55), ("sensor", 0.4), ("calculator", 0.35)],
}

BLOCK_GROUPS = {
    "base": [("power_block", 0.8), ("propulsion_block", 0.75), ("defense_block", 0.55), ("cargo_block", 0.35)],
    "defense": [("defense_block", 1.25), ("power_block", 0.35), ("instrument_block", 0.2)],
    "mobility": [("propulsion_block", 1.25), ("power_block", 0.75), ("instrument_block", 0.25)],
    "stealth": [("instrument_block", 0.55), ("propulsion_block", 0.45), ("cargo_block", 0.25)],
    "warfare": [("weapon_block", 1.35), ("instrument_block", 0.45), ("defense_block", 0.35), ("power_block", 0.25)],
    "mining": [("industry_block", 1.05), ("cargo_block", 0.75), ("power_block", 0.35)],
    "harvesting": [("industry_block", 1.05), ("cargo_block", 0.5), ("instrument_block", 0.35), ("power_block", 0.25)],
    "hunting": [("industry_block", 0.85), ("weapon_block", 0.75), ("cargo_block", 0.45), ("propulsion_block", 0.35)],
    "hacking": [("instrument_block", 1.15), ("hangar_block", 0.35), ("propulsion_block", 0.25)],
    "salvage": [("industry_block", 0.8), ("hangar_block", 0.75), ("instrument_block", 0.45)],
    "survey": [("instrument_block", 1.2), ("propulsion_block", 0.35), ("hangar_block", 0.25)],
    "repair": [("industry_block", 0.55), ("instrument_block", 0.5), ("defense_block", 0.45), ("hangar_block", 0.35)],
    "cargo": [("cargo_block", 1.15), ("industry_block", 0.35), ("defense_block", 0.2)],
    "hangar": [("hangar_block", 1.25), ("instrument_block", 0.55), ("cargo_block", 0.35), ("power_block", 0.25)],
}

FACTION_BONUSES = {
    "capital": {"base": 0.4, "defense": 0.25, "warfare": 0.25, "repair": 0.15, "survey": 0.15},
    "wind_houses": {"mobility": 0.7, "cargo": 0.35, "stealth": 0.3, "survey": 0.2},
    "mist_synod": {"harvesting": 0.75, "stealth": 0.35, "repair": 0.25, "warfare": 0.15},
    "stone_vault": {"defense": 0.85, "mining": 0.65, "base": 0.25},
    "factory_ark": {"salvage": 0.75, "hacking": 0.45, "hangar": 0.5, "survey": 0.25, "repair": 0.2},
    "devourers": {"hunting": 0.85, "warfare": 0.45, "mining": 0.2, "cargo": 0.15},
}

ROLE_KEYWORDS = [
    (("cargo", "hauler", "tug", "transport"), {"cargo": 0.8, "mobility": 0.15}),
    (("industrial", "industry", "refinery", "foundry", "combine", "chemical", "processor"), {"mining": 0.55, "harvesting": 0.45, "cargo": 0.45}),
    (("combat", "patrol", "gunline", "gun", "artillery", "heavy", "brawler", "assault", "ripper"), {"warfare": 0.75, "defense": 0.35}),
    (("bomb", "torpedo"), {"warfare": 0.65, "hunting": 0.2}),
    (("harpoon", "hunter", "leviathan"), {"hunting": 0.75, "warfare": 0.35, "cargo": 0.2}),
    (("recon", "research", "survey", "x_ray", "scout", "dome", "interceptor"), {"survey": 0.65, "hacking": 0.35, "mobility": 0.25, "stealth": 0.2}),
    (("repair",), {"repair": 0.9, "defense": 0.2, "survey": 0.15}),
    (("automaton", "ark", "tender", "factory", "citadel"), {"hangar": 0.6, "salvage": 0.5, "hacking": 0.25}),
    (("toxic", "siphon", "gas", "cloud"), {"harvesting": 0.7, "stealth": 0.25}),
]


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str]) -> None:
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def split_ids(raw: str) -> list[str]:
    return [part.strip() for part in raw.split(";") if part.strip()]


def split_amounts(raw: str) -> list[int]:
    return [int(part.strip()) for part in raw.split(";") if part.strip()]


def as_float(raw: str) -> float:
    return float(str(raw).replace(",", "."))


def as_int(row: dict[str, str], key: str) -> int:
    raw = row.get(key, "").strip()
    return int(float(raw)) if raw else 0


def deterministic_variance(key: str, spread: float = 0.06) -> float:
    digest = hashlib.sha256(key.encode("utf-8")).digest()
    value = int.from_bytes(digest[:4], "big") / 0xFFFFFFFF
    return 1.0 + (value * 2 - 1) * spread


def add_weight(weights: defaultdict[str, float], item_id: str, value: float) -> None:
    if value > 0:
        weights[item_id] += value


def add_group(weights: defaultdict[str, float], group_map: dict[str, list[tuple[str, float]]],
              category_weights: dict[str, float], multiplier: float) -> None:
    for category, category_weight in category_weights.items():
        if category_weight <= 0 or category not in group_map:
            continue
        for item_id, item_weight in group_map[category]:
            add_weight(weights, item_id, category_weight * item_weight * multiplier)


def category_weights(ship: dict[str, str]) -> dict[str, float]:
    weights = defaultdict(float)
    weights["base"] = 1.0

    defense = as_int(ship, "defense")
    mobility = as_int(ship, "mobility")
    stealth = as_int(ship, "stealth")
    warfare = as_int(ship, "warfare")
    mining = as_int(ship, "mining")
    harvesting = as_int(ship, "harvesting")
    hunting = as_int(ship, "hunting")
    hacking = as_int(ship, "hacking")
    salvage = as_int(ship, "salvage")
    survey = as_int(ship, "survey")
    repair = as_int(ship, "repair")
    cargo = as_int(ship, "cargo")

    weights["defense"] += defense / 85
    weights["mobility"] += mobility / 90
    weights["stealth"] += stealth / 100
    weights["warfare"] += warfare / 85
    weights["mining"] += mining / 75
    weights["harvesting"] += harvesting / 75
    weights["hunting"] += hunting / 75
    weights["hacking"] += hacking / 75
    weights["salvage"] += salvage / 75
    weights["survey"] += survey / 75
    weights["repair"] += repair / 75
    weights["cargo"] += max(cargo, as_int(ship, "cargo_capacity_tons") / 10) / 80

    ship_class = ship["ship_class_id"]
    if ship_class == "frigate":
        weights["mobility"] += 0.25
        weights["stealth"] += 0.15
    elif ship_class == "cruiser":
        weights["base"] += 0.15
        weights["defense"] += 0.1
    elif ship_class == "battleship":
        weights["defense"] += 0.45
        weights["warfare"] += 0.25
        weights["base"] += 0.2

    for category, bonus in FACTION_BONUSES.get(ship["faction_id"], {}).items():
        weights[category] += bonus

    role_text = " ".join([ship.get("role_id", ""), ship.get("branch_id", ""), ship.get("role_name_ru", "")]).lower()
    for keywords, bonuses in ROLE_KEYWORDS:
        if any(keyword in role_text for keyword in keywords):
            for category, bonus in bonuses.items():
                weights[category] += bonus

    if any(value > 0 for value in (hacking, salvage, survey)) or "factory_ark" == ship["faction_id"]:
        weights["hacking"] += 0.1
    if "repair" in role_text:
        weights["repair"] += 0.35

    return dict(weights)


def rank_layer(rank: int) -> str:
    if rank in (2, 3):
        return "raw_resources"
    if rank in (4, 5):
        return "prepared_materials"
    if rank in (6, 7):
        return "hull_and_machine_parts"
    if rank in (8, 9):
        return "ship_blocks"
    if rank == 10:
        return "faction_flagship_license"
    raise ValueError(f"Unsupported rank {rank}")


def layer_weights(rank: int, categories: dict[str, float]) -> defaultdict[str, float]:
    weights: defaultdict[str, float] = defaultdict(float)
    if rank in (2, 3):
        add_group(weights, RAW_GROUPS, categories, 1.0)
    elif rank in (4, 5):
        add_group(weights, MATERIAL_GROUPS, categories, 1.0)
        add_group(weights, RAW_GROUPS, categories, 0.18 if rank == 4 else 0.12)
    elif rank in (6, 7):
        add_group(weights, PART_GROUPS, categories, 1.0)
        add_group(weights, MATERIAL_GROUPS, categories, 0.24 if rank == 6 else 0.18)
        add_group(weights, RAW_GROUPS, categories, 0.05)
    elif rank in (8, 9):
        add_group(weights, BLOCK_GROUPS, categories, 1.0)
        add_group(weights, PART_GROUPS, categories, 0.35 if rank == 8 else 0.28)
        add_group(weights, MATERIAL_GROUPS, categories, 0.10)
    elif rank == 10:
        add_group(weights, BLOCK_GROUPS, categories, 1.15)
        add_group(weights, PART_GROUPS, categories, 0.42)
        add_group(weights, MATERIAL_GROUPS, categories, 0.22)
        add_group(weights, RAW_GROUPS, categories, 0.05)
    return weights


def max_inputs_for_rank(rank: int) -> int:
    if rank in (2, 3):
        return 8
    if rank in (4, 5):
        return 10
    if rank in (6, 7):
        return 12
    if rank in (8, 9):
        return 14
    return 16


class FeCalculator:
    def __init__(self) -> None:
        value_rows = read_csv(CONFIG_DIR / "resource_value_reference.csv")
        self.leaf_values = {row["item_id"]: as_float(row["value_fe_per_unit"]) for row in value_rows}

        recipes = read_csv(CONFIG_DIR / "item_production_recipes.csv")
        self.recipes_by_output = {row["output_item_id"]: row for row in recipes}
        self.cost_rows = {
            (row["recipe_id"], int(row["economy_level"])): row
            for row in read_csv(CONFIG_DIR / "item_recipe_cost_levels.csv")
        }
        self.visiting: set[tuple[str, int]] = set()

    @lru_cache(maxsize=None)
    def item_fe(self, item_id: str, economy_level: int) -> float:
        key = (item_id, economy_level)
        if key in self.visiting:
            raise ValueError(f"Recipe cycle detected at {item_id}")
        recipe = self.recipes_by_output.get(item_id)
        if recipe is None:
            if item_id not in self.leaf_values:
                raise ValueError(f"Missing FE value for {item_id}")
            return self.leaf_values[item_id]

        self.visiting.add(key)
        cost_row = self.cost_rows[(recipe["recipe_id"], economy_level)]
        input_ids = split_ids(cost_row["input_ids"])
        input_amounts = split_amounts(cost_row["input_amounts"])
        total = 0.0
        for input_id, amount in zip(input_ids, input_amounts):
            total += self.item_fe(input_id, economy_level) * amount
        self.visiting.remove(key)
        return total / as_float(recipe["output_amount"])


def target_fe(ship: dict[str, str]) -> float:
    rank = as_int(ship, "rank")
    base = RANK_TARGET_FE[rank]
    class_multiplier = CLASS_TARGET_MULTIPLIER.get(ship["ship_class_id"], 1.0)
    role_text = " ".join([ship.get("role_id", ""), ship.get("branch_id", "")]).lower()

    role_multiplier = 1.0
    if any(word in role_text for word in ("heavy", "battleship", "citadel", "foundry")):
        role_multiplier += 0.08
    if any(word in role_text for word in ("industrial", "factory", "combine", "refinery", "processor")):
        role_multiplier += 0.05
    if any(word in role_text for word in ("cargo", "hauler", "tug")):
        role_multiplier -= 0.05
    if any(word in role_text for word in ("recon", "scout", "research")):
        role_multiplier -= 0.04
    if "repair" in role_text:
        role_multiplier += 0.03

    stats = [
        as_int(ship, "defense"),
        as_int(ship, "mobility"),
        as_int(ship, "stealth"),
        as_int(ship, "warfare"),
        max(
            as_int(ship, "mining"),
            as_int(ship, "harvesting"),
            as_int(ship, "hunting"),
            as_int(ship, "hacking"),
            as_int(ship, "salvage"),
            as_int(ship, "survey"),
            as_int(ship, "repair"),
        ),
    ]
    complexity = sum(stats) / max(1, len(stats))
    complexity_multiplier = 0.90 + min(0.22, complexity / 500)
    return base * class_multiplier * role_multiplier * complexity_multiplier * deterministic_variance(ship["id_ship"])


def choose_amounts(ship: dict[str, str], fe: FeCalculator) -> list[tuple[str, int]]:
    rank = as_int(ship, "rank")
    categories = category_weights(ship)
    weights = layer_weights(rank, categories)
    if not weights:
        raise ValueError(f"No weights for {ship['id_ship']}")

    max_inputs = max_inputs_for_rank(rank)
    selected = sorted(weights.items(), key=lambda pair: (-pair[1], pair[0]))[:max_inputs]
    selected_weights = {item_id: weight for item_id, weight in selected}
    total_weight = sum(selected_weights.values())
    desired_fe = target_fe(ship)

    amounts: dict[str, int] = {}
    for item_id, weight in selected:
        unit_fe = fe.item_fe(item_id, 5)
        share = desired_fe * weight / total_weight
        amounts[item_id] = max(1, int(round(share / unit_fe)))

    # Keep the final value close to the rank target without forcing exact equality.
    for _ in range(4):
        current = sum(fe.item_fe(item_id, 5) * amount for item_id, amount in amounts.items())
        if current <= 0:
            break
        ratio = desired_fe / current
        if 0.92 <= ratio <= 1.08:
            break
        for item_id in list(amounts):
            amounts[item_id] = max(1, int(round(amounts[item_id] * ratio)))

    current = sum(fe.item_fe(item_id, 5) * amount for item_id, amount in amounts.items())
    anchor_item = max(selected, key=lambda pair: pair[1])[0]
    anchor_unit = fe.item_fe(anchor_item, 5)
    if current < desired_fe * 0.96:
        amounts[anchor_item] += max(1, math.ceil((desired_fe * 0.96 - current) / anchor_unit))
    elif current > desired_fe * 1.08 and amounts[anchor_item] > 1:
        removable = min(amounts[anchor_item] - 1, math.floor((current - desired_fe * 1.08) / anchor_unit))
        if removable > 0:
            amounts[anchor_item] -= removable

    return [(item_id, amounts[item_id]) for item_id, _ in selected if amounts[item_id] > 0]


def ids(inputs: list[tuple[str, int]]) -> str:
    return ";".join(item_id for item_id, _ in inputs)


def amounts(inputs: list[tuple[str, int]]) -> str:
    return ";".join(str(amount) for _, amount in inputs)


def scaled_amounts(inputs: list[tuple[str, int]], multiplier: float) -> list[int]:
    return [max(1, math.ceil(amount * multiplier)) for _, amount in inputs]


def format_fe(value: float) -> str:
    return f"{value:.2f}"


def generate() -> None:
    ships = [
        row for row in read_csv(SHIP_TREE_PATH)
        if row["catalog_scope"] == "development" and as_int(row, "rank") >= 2
    ]
    fe = FeCalculator()
    rank_time = {
        row["rank"]: as_float(row["base_time_min_level5_building1"])
        for row in read_csv(CONFIG_DIR / "ship_rank_craft_time_policy.csv")
    }

    recipe_rows: list[dict[str, object]] = []
    cost_rows: list[dict[str, object]] = []
    time_rows: list[dict[str, object]] = []
    time_summary_rows: list[dict[str, object]] = []

    yard_curve_rows: list[dict[str, object]] = []
    for ship_class_id, (building_id, local_name_ru) in CLASS_YARDS.items():
        for level in range(1, 31):
            speed = 1.0 + (level - 1) * 0.5 / 29
            yard_curve_rows.append(
                {
                    "building_id": building_id,
                    "local_name_ru": local_name_ru,
                    "ship_class_id": ship_class_id,
                    "yard_level": level,
                    "speed_multiplier": f"{speed:.4f}",
                    "time_multiplier": f"{1 / speed:.4f}",
                    "notes_ru": "Классовая верфь ускоряет только свой класс кораблей; кривая ускорения такая же, как у старой общей верфи." if level == 30 else "",
                }
            )

    for ship in ships:
        rank = as_int(ship, "rank")
        recipe_id = f"craft_{ship['id_ship']}"
        inputs = choose_amounts(ship, fe)
        license_id = R10_LICENSE_BY_FACTION.get(ship["faction_id"], "") if rank == 10 else ""
        license_amount = 1 if license_id else 0
        base_time = rank_time[f"R{rank}"]
        producer_building_id = CLASS_YARDS[ship["ship_class_id"]][0]

        recipe_rows.append(
            {
                "recipe_id": recipe_id,
                "ship_id": ship["id_ship"],
                "local_name_ru": ship["local_name_ru"],
                "rank": rank,
                "faction_id": ship["faction_id"],
                "faction_name_ru": ship["faction_name_ru"],
                "ship_class_id": ship["ship_class_id"],
                "ship_class_name_ru": ship["ship_class_name_ru"],
                "role_id": ship["role_id"],
                "role_name_ru": ship["role_name_ru"],
                "branch_id": ship["branch_id"],
                "craft_layer": rank_layer(rank),
                "producer_building_id": producer_building_id,
                "base_time_min_level5_yard1": f"{base_time:.2f}",
                "time_min_level5_yard30": f"{base_time / 1.5:.2f}",
                "required_license_item_id": license_id,
                "required_license_amount": license_amount,
                "input_ids_level5": ids(inputs),
                "input_amounts_level5": amounts(inputs),
                "target_material_fe_level5": format_fe(target_fe(ship)),
                "notes_ru": "Автосборка по рангу, классу, фракции, роли и характеристикам корабля.",
            }
        )

        for level, multiplier in ECONOMY_CURVE.items():
            level_amounts = scaled_amounts(inputs, multiplier)
            cost_rows.append(
                {
                    "recipe_id": recipe_id,
                    "ship_id": ship["id_ship"],
                    "economy_level": level,
                    "input_multiplier": f"{multiplier:.2f}",
                    "required_license_item_id": license_id,
                    "required_license_amount": license_amount,
                    "input_ids": ids(inputs),
                    "input_amounts": ";".join(str(value) for value in level_amounts),
                    "total_discountable_input_units": sum(level_amounts),
                }
            )

        for level, multiplier in TIME_CURVE.items():
            shipyard1 = base_time * multiplier
            time_rows.append(
                {
                "recipe_id": recipe_id,
                "ship_id": ship["id_ship"],
                "time_level": level,
                "time_multiplier": f"{multiplier:.2f}",
                "producer_building_id": producer_building_id,
                "time_min_yard1": f"{shipyard1:.2f}",
                "time_min_yard30": f"{shipyard1 / 1.5:.2f}",
            }
        )

    class_rank_counts: dict[tuple[str, int], int] = defaultdict(int)
    for ship in ships:
        class_rank_counts[(ship["ship_class_id"], as_int(ship, "rank"))] += 1
    for ship_class_id, (producer_building_id, local_name_ru) in CLASS_YARDS.items():
        for rank in range(2, 11):
            base_time = rank_time[f"R{rank}"]
            slowest = base_time * TIME_CURVE[0]
            fastest = base_time * TIME_CURVE[5] / 1.5
            time_summary_rows.append(
                {
                    "rank": rank,
                    "ship_class_id": ship_class_id,
                    "producer_building_id": producer_building_id,
                    "producer_building_name_ru": local_name_ru,
                    "ship_count": class_rank_counts.get((ship_class_id, rank), 0),
                    "min_time_level5_yard30_min": f"{fastest:.2f}",
                    "max_time_level0_yard1_min": f"{slowest:.2f}",
                    "min_time_level5_yard30_hours": f"{fastest / 60:.2f}",
                    "max_time_level0_yard1_hours": f"{slowest / 60:.2f}",
                }
            )

    write_csv(
        CONFIG_DIR / "ship_class_yard_speed_curve.csv",
        yard_curve_rows,
        [
            "building_id",
            "local_name_ru",
            "ship_class_id",
            "yard_level",
            "speed_multiplier",
            "time_multiplier",
            "notes_ru",
        ],
    )

    write_csv(
        CONFIG_DIR / "ship_crafting_recipes.csv",
        recipe_rows,
        [
            "recipe_id",
            "ship_id",
            "local_name_ru",
            "rank",
            "faction_id",
            "faction_name_ru",
            "ship_class_id",
            "ship_class_name_ru",
            "role_id",
            "role_name_ru",
            "branch_id",
            "craft_layer",
            "producer_building_id",
            "base_time_min_level5_yard1",
            "time_min_level5_yard30",
            "required_license_item_id",
            "required_license_amount",
            "input_ids_level5",
            "input_amounts_level5",
            "target_material_fe_level5",
            "notes_ru",
        ],
    )
    write_csv(
        CONFIG_DIR / "ship_recipe_cost_levels.csv",
        cost_rows,
        [
            "recipe_id",
            "ship_id",
            "economy_level",
            "input_multiplier",
            "required_license_item_id",
            "required_license_amount",
            "input_ids",
            "input_amounts",
            "total_discountable_input_units",
        ],
    )
    write_csv(
        CONFIG_DIR / "ship_recipe_time_levels.csv",
        time_rows,
        [
            "recipe_id",
            "ship_id",
            "time_level",
            "time_multiplier",
            "producer_building_id",
            "time_min_yard1",
            "time_min_yard30",
        ],
    )

    write_csv(
        CONFIG_DIR / "ship_time_summary.csv",
        time_summary_rows,
        [
            "rank",
            "ship_class_id",
            "producer_building_id",
            "producer_building_name_ru",
            "ship_count",
            "min_time_level5_yard30_min",
            "max_time_level0_yard1_min",
            "min_time_level5_yard30_hours",
            "max_time_level0_yard1_hours",
        ],
    )
    print(f"Generated {len(recipe_rows)} ship crafting recipes.")


if __name__ == "__main__":
    generate()
