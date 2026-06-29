from __future__ import annotations

import csv
import hashlib
import math
import statistics
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"
SHIP_TREE_PATH = ROOT / "Assets" / "Data" / "Config" / "Ship_tree.csv"

FE_SCALE_MULTIPLIER = 10
INTANGIBLE_EXTRACTION_MULTIPLIER = 2.0

MISSION_PROFILES = {
    "quick": {
        "local_name_ru": "Быстрая",
        "scope": "adaptive_ship_rank",
        "budget_key": "quick_base_fe",
        "fixed_base_fe": 0.00,
        "required_power": 0.00,
        "capacity_multiplier": 1.00,
        "ship_xp_multiplier": 0.50,
        "notes_ru": "Маленький безопасный быстрый вылет; не предназначен для окупаемости топовых кораблей.",
    },
    "normal": {
        "local_name_ru": "Обычная",
        "scope": "fixed_world_difficulty",
        "budget_key": "",
        "fixed_base_fe": 1_000_000.00,
        "required_power": 2_500.00,
        "capacity_multiplier": 1.15,
        "ship_xp_multiplier": 1.00,
        "notes_ru": "Стандартная адаптированная миссия по рангу корабля.",
    },
    "danger": {
        "local_name_ru": "Опасная",
        "scope": "fixed_world_difficulty",
        "budget_key": "",
        "fixed_base_fe": 1_624_000.00,
        "required_power": 4_200.00,
        "capacity_multiplier": 1.35,
        "ship_xp_multiplier": 1.45,
        "notes_ru": "Рискованный вылет с повышенной плотностью целей и ресурсов на старших рангах.",
    },
    "elite": {
        "local_name_ru": "Элитная",
        "scope": "fixed_world_difficulty",
        "budget_key": "",
        "fixed_base_fe": 1_950_000.00,
        "required_power": 7_200.00,
        "capacity_multiplier": 1.60,
        "ship_xp_multiplier": 2.20,
        "notes_ru": "Самая вкусная и сложная миссия; R10 должен окупаться здесь полной ценностью примерно за 5 вылетов.",
    },
}

CLASS_INCOME_MULTIPLIER = {
    "frigate": 0.92,
    "cruiser": 1.00,
    "battleship": 1.12,
}

ACTIVITY_LOCAL_NAME_RU = {
    "combat": "Бой",
    "mining": "Руда",
    "gas": "Газ",
    "hunting": "Левиафаны",
    "salvage": "Сальваж",
    "relic": "Реликты",
    "repair": "Ремонт",
    "courier": "Курьерка",
}

# Shares are FE-equivalent distribution of accumulated sortie value before
# extraction outcome is applied. Material value is lost on destruction.
# Intangible value is kept on destruction and doubled on successful extraction.
ACTIVITY_REWARD_PROFILE = {
    "combat": {
        "freight": 0.44,
        "resources": 0.20,
        "profile_currency": 0.05,
        "reputation": 0.03,
        "info": 0.00,
        "ship_xp_mult": 1.35,
        "mastery_mult": 0.80,
    },
    "mining": {
        "freight": 0.14,
        "resources": 0.68,
        "profile_currency": 0.08,
        "reputation": 0.02,
        "info": 0.00,
        "ship_xp_mult": 0.45,
        "mastery_mult": 1.00,
    },
    "gas": {
        "freight": 0.13,
        "resources": 0.66,
        "profile_currency": 0.10,
        "reputation": 0.02,
        "info": 0.00,
        "ship_xp_mult": 0.45,
        "mastery_mult": 1.00,
    },
    "hunting": {
        "freight": 0.18,
        "resources": 0.62,
        "profile_currency": 0.08,
        "reputation": 0.02,
        "info": 0.00,
        "ship_xp_mult": 0.60,
        "mastery_mult": 1.00,
    },
    "salvage": {
        "freight": 0.18,
        "resources": 0.56,
        "profile_currency": 0.12,
        "reputation": 0.02,
        "info": 0.00,
        "ship_xp_mult": 0.55,
        "mastery_mult": 1.05,
    },
    "relic": {
        "freight": 0.09,
        "resources": 0.20,
        "profile_currency": 0.06,
        "reputation": 0.04,
        "info": 0.40,
        "ship_xp_mult": 0.55,
        "mastery_mult": 1.30,
    },
    "repair": {
        "freight": 0.18,
        "resources": 0.18,
        "profile_currency": 0.04,
        "reputation": 0.46,
        "info": 0.00,
        "ship_xp_mult": 0.25,
        "mastery_mult": 1.20,
    },
    "courier": {
        "freight": 0.32,
        "resources": 0.12,
        "profile_currency": 0.03,
        "reputation": 0.42,
        "info": 0.00,
        "ship_xp_mult": 0.20,
        "mastery_mult": 1.15,
    },
}

MATERIAL_ACTIVITIES = {"mining", "gas", "hunting", "salvage", "relic"}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str]) -> None:
    with path.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def as_float(raw: str | int | float | None) -> float:
    if raw is None or raw == "":
        return 0.0
    return float(str(raw).replace(",", "."))


def as_int(raw: str | int | float | None) -> int:
    return int(round(as_float(raw)))


def format_fe(value: float) -> str:
    return f"{value:.2f}"


def format_float(value: float, digits: int = 2) -> str:
    return f"{value:.{digits}f}"


def deterministic_variance(seed: str, low: float = 0.94, high: float = 1.06) -> float:
    digest = hashlib.sha256(seed.encode("utf-8")).hexdigest()
    value = int(digest[:8], 16) / 0xFFFFFFFF
    return low + (high - low) * value


def rank_key(rank: int) -> str:
    return f"R{rank}"


def stat(row: dict[str, str], column: str) -> float:
    return max(0.0, min(100.0, as_float(row.get(column, ""))))


def cargo_rating(row: dict[str, str]) -> float:
    if row.get("cargo"):
        return stat(row, "cargo")
    tons = as_float(row.get("cargo_capacity_tons", ""))
    return max(0.0, min(100.0, tons / 8.0))


def activity_scores(ship: dict[str, str]) -> dict[str, float]:
    cargo = cargo_rating(ship)
    mobility = stat(ship, "mobility")
    stealth = stat(ship, "stealth")
    defense = stat(ship, "defense")

    scores = {
        "combat": stat(ship, "warfare"),
        "mining": stat(ship, "mining"),
        "gas": stat(ship, "harvesting"),
        "hunting": stat(ship, "hunting"),
        "salvage": stat(ship, "salvage"),
        "relic": max(stat(ship, "hacking") * 0.80 + stat(ship, "survey") * 0.20, stat(ship, "survey") * 0.60),
        "repair": stat(ship, "repair"),
        "courier": min(100.0, cargo * 0.52 + mobility * 0.28 + stealth * 0.15 + defense * 0.05),
    }
    return scores


def primary_activity(scores: dict[str, float]) -> str:
    return max(scores, key=lambda key: (scores[key], key))


def mission_base_fe(rank: int, mission_profile: str, rank_budget: dict[int, dict[str, float]]) -> float:
    profile = MISSION_PROFILES[mission_profile]
    if profile["scope"] == "adaptive_ship_rank":
        return rank_budget[rank][str(profile["budget_key"])]
    return as_float(profile["fixed_base_fe"])


def ship_power_estimate(ship: dict[str, str], rank_budget: dict[int, dict[str, float]]) -> float:
    rank = as_int(ship["rank"])
    base_power = rank_budget[rank]["power_budget"]
    survival_quality = (
        stat(ship, "defense") * 0.40
        + stat(ship, "warfare") * 0.30
        + stat(ship, "mobility") * 0.20
        + stat(ship, "stealth") * 0.10
    ) / 100.0
    return base_power * (0.90 + survival_quality * 0.20)


def mission_success_factor(ship: dict[str, str], mission_profile: str, rank_budget: dict[int, dict[str, float]]) -> float:
    profile = MISSION_PROFILES[mission_profile]
    required_power = as_float(profile["required_power"])
    if required_power <= 0:
        return 1.0

    ratio = ship_power_estimate(ship, rank_budget) / required_power
    if ratio >= 1.0:
        return 1.0
    if ratio <= 0.35:
        return 0.02
    return 0.02 + ((ratio - 0.35) / 0.65) * 0.98


def effectiveness_multiplier(ship: dict[str, str], scores: dict[str, float]) -> float:
    ordered = sorted(scores.values(), reverse=True)
    best = ordered[0] / 100.0
    second = (ordered[1] if len(ordered) > 1 else 0.0) / 100.0
    support = (
        stat(ship, "defense")
        + stat(ship, "mobility")
        + stat(ship, "stealth")
        + cargo_rating(ship)
    ) / 400.0
    return 0.70 + best * 0.38 + second * 0.14 + support * 0.18


def reward_outcome_split(accumulated_total_value_fe: float, profile: dict[str, float]) -> dict[str, float]:
    freight_fe = accumulated_total_value_fe * profile["freight"]
    resource_fe = accumulated_total_value_fe * profile["resources"]
    profile_currency_fe = accumulated_total_value_fe * profile["profile_currency"]
    reputation_fe = accumulated_total_value_fe * profile["reputation"]
    info_fe = accumulated_total_value_fe * profile["info"]

    material_value_fe = resource_fe + profile_currency_fe + info_fe
    intangible_value_fe = max(0.0, accumulated_total_value_fe - material_value_fe)
    extracted_total_value_fe = material_value_fe + intangible_value_fe * INTANGIBLE_EXTRACTION_MULTIPLIER
    extracted_freight_fe = freight_fe * INTANGIBLE_EXTRACTION_MULTIPLIER
    extracted_reputation_fe = reputation_fe * INTANGIBLE_EXTRACTION_MULTIPLIER
    extracted_liquid_fe = extracted_freight_fe + resource_fe + profile_currency_fe

    return {
        "freight_fe": freight_fe,
        "resource_fe": resource_fe,
        "profile_currency_fe": profile_currency_fe,
        "reputation_fe": reputation_fe,
        "info_fe": info_fe,
        "material_value_fe": material_value_fe,
        "intangible_value_fe": intangible_value_fe,
        "extraction_intangible_bonus_fe": intangible_value_fe * (INTANGIBLE_EXTRACTION_MULTIPLIER - 1.0),
        "destroyed_total_value_fe": intangible_value_fe,
        "destroyed_liquid_fe": freight_fe,
        "extracted_total_value_fe": extracted_total_value_fe,
        "extracted_liquid_fe": extracted_liquid_fe,
        "extracted_freight_fe": extracted_freight_fe,
        "extracted_reputation_fe": extracted_reputation_fe,
    }


def load_resource_values() -> dict[str, float]:
    return {
        row["item_id"]: as_float(row["value_fe_per_unit"])
        for row in read_csv(CONFIG_DIR / "resource_value_reference.csv")
    }


def ore_value_per_ton(ore: dict[str, str], values: dict[str, float]) -> float:
    ore_columns = {
        "iron_pct": "iron",
        "copper_pct": "copper",
        "magnesium_pct": "magnesium",
        "quartz_pct": "quartz",
        "charcoal_pct": "charcoal",
        "calcite_pct": "calcite",
        "sylvine_pct": "sylvine",
        "claudium_pct": "claudium",
        "monazite_pct": "monazite",
        "gems_pct": "gems",
    }
    return sum(as_float(ore[column]) * 0.01 * 1000.0 * values[item_id] for column, item_id in ore_columns.items())


def best_ore_capacity_fe(ship: dict[str, str], values: dict[str, float]) -> tuple[float, str]:
    mining = stat(ship, "mining")
    cargo_tons = as_float(ship.get("cargo_capacity_tons", ""))
    if mining <= 0 or cargo_tons <= 0:
        return 0.0, "Нет рудного профиля или трюма."

    best_value = 0.0
    best_note = "Нет доступной руды."
    load_factor = max(0.0, min(1.0, 0.55 + mining * 0.0045))
    crusher_budget = cargo_tons * load_factor * (0.75 + mining / 50.0)
    for ore in read_csv(CONFIG_DIR / "ore_deposits.csv"):
        if as_float(ore["min_mining"]) > mining:
            continue
        tons = min(cargo_tons, crusher_budget / as_float(ore["crusher_wear_per_ton"]))
        value = tons * ore_value_per_ton(ore, values)
        if value > best_value:
            best_value = value
            best_note = f"{ore['ore_id']} / {tons:.1f} т сырья"
    return best_value, best_note


def gas_value_per_ton(gas: dict[str, str], values: dict[str, float]) -> float:
    item_ids = [part.strip() for part in gas["composition_id_item"].split(",") if part.strip()]
    shares = [as_float(part.strip()) for part in gas["composition_share"].split(",") if part.strip()]
    return sum(values[item_id] * share * 1000.0 for item_id, share in zip(item_ids, shares))


def best_gas_capacity_fe(ship: dict[str, str], values: dict[str, float]) -> tuple[float, str]:
    harvesting = stat(ship, "harvesting")
    cargo_tons = as_float(ship.get("cargo_capacity_tons", ""))
    if harvesting <= 0 or cargo_tons <= 0:
        return 0.0, "Нет газового профиля или трюма."

    best_value = 0.0
    best_note = "Нет доступного облака."
    load_factor = max(0.0, min(1.0, 0.55 + harvesting * 0.0045))
    usable_tons = cargo_tons * load_factor
    for gas in read_csv(ROOT / "Assets" / "Data" / "Config" / "Gas_condensate_type.csv"):
        value = usable_tons * gas_value_per_ton(gas, values)
        if value > best_value:
            best_value = value
            best_note = f"{gas['id_gas_condensate_type']} / {usable_tons:.1f} т конденсата"
    return best_value, best_note


def leviathan_value_fe(row: dict[str, str], values: dict[str, float]) -> float:
    fields = {
        "meat_percent": "leviathan_meat",
        "fat_percent": "leviathan_fat",
        "hide_percent": "leviathan_hide",
        "tendon_percent": "leviathan_sinew",
        "bone_grit_percent": "bone_grit",
        "acid_percent": "acid",
        "ichor_percent": "leviathan_ichor",
        "amber_percent": "amber",
    }
    mass_kg = as_float(row["mass_t"]) * 1000.0
    return sum(as_float(row[column]) * 0.01 * mass_kg * values[item_id] for column, item_id in fields.items())


def best_leviathan_capacity_fe(ship: dict[str, str], values: dict[str, float]) -> tuple[float, str]:
    hunting = stat(ship, "hunting")
    if hunting <= 0:
        return 0.0, "Нет охотничьего профиля."

    ship_class = ship.get("ship_class_id", "")
    class_mass_limit = {
        "frigate": 45.0,
        "cruiser": 300.0,
        "battleship": 1500.0,
    }.get(ship_class, 45.0)
    score_mass_limit = 5.0 + hunting * 18.0
    mass_limit = min(class_mass_limit, score_mass_limit)

    best_value = 0.0
    best_note = "Нет подходящей туши."
    for leviathan in read_csv(CONFIG_DIR / "leviathan_butchery.csv"):
        if as_float(leviathan["mass_t"]) > mass_limit:
            continue
        value = leviathan_value_fe(leviathan, values)
        if value > best_value:
            best_value = value
            best_note = f"{leviathan['leviathan_id']} / {leviathan['mass_t']} т"
    return best_value, best_note


def salvage_capacity_fe(ship: dict[str, str], values: dict[str, float]) -> tuple[float, str]:
    salvage = stat(ship, "salvage")
    cargo_tons = as_float(ship.get("cargo_capacity_tons", ""))
    if salvage <= 0 or cargo_tons <= 0:
        return 0.0, "Нет сальважного профиля или трюма."

    best_density = max(value for item_id, value in values.items() if item_id.startswith("automaton_")) * 1000.0
    usable_tons = cargo_tons * max(0.25, min(1.0, 0.45 + salvage * 0.005))
    return usable_tons * best_density, f"точные узлы автоматонов / {usable_tons:.1f} т"


def relic_capacity_fe(ship: dict[str, str]) -> tuple[float, str]:
    score = max(stat(ship, "hacking"), stat(ship, "survey"))
    if score <= 0:
        return 0.0, "Нет исследовательского профиля."
    ship_class = ship.get("ship_class_id", "")
    base_attempts = {"frigate": 4, "cruiser": 7, "battleship": 10}.get(ship_class, 4)
    attempts = base_attempts + int(score // 18)
    expensive_relic_value = 720_000.0
    return attempts * expensive_relic_value, f"{attempts} вскрытий дорогих реликтов"


def abstract_contract_capacity_fe(ship: dict[str, str], total_value_fe: float, activity: str) -> tuple[float, str]:
    score = activity_scores(ship)[activity]
    multiplier = 1.15 + score / 160.0
    return total_value_fe * multiplier, "лимит задается плотностью целей/контрактом, не трюмом"


def activity_capacity_fe(ship: dict[str, str], activity: str, total_value_fe: float, values: dict[str, float]) -> tuple[float, str]:
    if activity == "mining":
        return best_ore_capacity_fe(ship, values)
    if activity == "gas":
        return best_gas_capacity_fe(ship, values)
    if activity == "hunting":
        return best_leviathan_capacity_fe(ship, values)
    if activity == "salvage":
        return salvage_capacity_fe(ship, values)
    if activity == "relic":
        return relic_capacity_fe(ship)
    return abstract_contract_capacity_fe(ship, total_value_fe, activity)


def load_rank_budget() -> dict[int, dict[str, float]]:
    rows = read_csv(CONFIG_DIR / "rank_budget.csv")
    result: dict[int, dict[str, float]] = {}
    for row in rows:
        budget_row = row["budget_row"]
        if not budget_row.startswith("R") or "*" in budget_row:
            continue
        rank = int(budget_row[1:])
        result[rank] = {
            "quick_base_fe": as_float(row["quick_mission_fe"]) * FE_SCALE_MULTIPLIER,
            "normal_base_fe": as_float(row["normal_mission_fe"]) * FE_SCALE_MULTIPLIER,
            "danger_base_fe": as_float(row["danger_mission_fe"]) * FE_SCALE_MULTIPLIER,
            "elite_base_fe": as_float(row["elite_mission_fe"]) * FE_SCALE_MULTIPLIER,
            "ship_xp_base": as_float(row["ship_xp_per_suitable_mission"]),
            "base_sorties": as_float(row["base_sorties"]),
            "power_budget": as_float(row["power_budget"]),
        }
    return result


def load_buy_multipliers() -> dict[int, float]:
    rows = read_csv(CONFIG_DIR / "ship_acquisition_channels.csv")
    result: dict[int, float] = {}
    for row in rows:
        rank = int(row["rank"].removeprefix("R"))
        result[rank] = as_float(row["buy_to_craft_multiplier"])
    return result


def load_craft_fe() -> dict[tuple[str, int], float]:
    rows = read_csv(CONFIG_DIR / "ship_recipe_fe_summary.csv")
    result: dict[tuple[str, int], float] = {}
    for row in rows:
        result[(row["ship_id"], int(row["economy_level"]))] = as_float(row["total_fe"])
    return result


def load_mission_requirements() -> dict[tuple[str, str], list[dict[str, str]]]:
    rows = read_csv(CONFIG_DIR / "mission_archetype_requirements.csv")
    grouped: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        grouped[(row["difficulty"], row["primary_activity"])].append(row)
    return grouped


def ship_stat_for_requirement(ship: dict[str, str], stat_id: str) -> float:
    if stat_id == "cargo":
        return cargo_rating(ship)
    return stat(ship, stat_id)


def requirement_score(
    ship: dict[str, str],
    mission_profile: str,
    primary_activity: str,
    requirements: dict[tuple[str, str], list[dict[str, str]]],
) -> tuple[float, str]:
    if mission_profile == "quick":
        return 1.0, "quick_adaptive"

    rows = requirements.get((mission_profile, primary_activity))
    if not rows:
        return 0.02, "missing_requirements"

    weighted = 0.0
    total_weight = 0.0
    hard_gate_ratios: list[float] = []
    parts: list[str] = []
    for row in rows:
        required = as_float(row["required_value"])
        weight = as_float(row["weight"])
        actual = ship_stat_for_requirement(ship, row["stat_id"])
        ratio = actual / required if required > 0 else 1.0
        weighted += min(ratio, 1.20) * weight
        total_weight += weight
        if row["hard_gate"] == "yes":
            hard_gate_ratios.append(ratio)
        parts.append(f"{row['stat_id']} {actual:.0f}/{required:.0f}")

    if total_weight <= 0:
        return 0.02, "zero_requirement_weight"

    weighted_ratio = weighted / total_weight
    hard_gate_ratio = min(hard_gate_ratios) if hard_gate_ratios else 1.0
    if hard_gate_ratio < 0.35:
        return 0.02, "; ".join(parts)

    factor = max(0.02, min(1.0, (weighted_ratio - 0.45) / 0.55))
    if hard_gate_ratio < 0.75:
        factor *= hard_gate_ratio / 0.75
    return max(0.02, min(1.0, factor)), "; ".join(parts)


def mission_archetype_id(
    mission_profile: str,
    primary_activity: str,
    requirements: dict[tuple[str, str], list[dict[str, str]]],
) -> str:
    if mission_profile == "quick":
        return "quick_adaptive"
    rows = requirements.get((mission_profile, primary_activity), [])
    if rows:
        return rows[0]["mission_archetype"]
    return f"{mission_profile}_{primary_activity}"


def average(values: list[float]) -> float:
    return sum(values) / len(values) if values else 0.0


def format_amount(value: float) -> str:
    if value >= 1000:
        return f"{value / 1000:.1f} т"
    if value >= 10:
        return f"{value:.0f} кг"
    return f"{value:.1f} кг"


def resource_breakdown_from_percent(
    row: dict[str, str],
    tons: float,
    columns: dict[str, str],
    values: dict[str, float],
) -> tuple[list[tuple[str, float]], float]:
    amounts: list[tuple[str, float]] = []
    total_value = 0.0
    for column, item_id in columns.items():
        percent = as_float(row[column])
        if percent <= 0:
            continue
        kg = tons * 1000.0 * percent * 0.01
        amounts.append((item_id, kg))
        total_value += kg * values[item_id]
    return amounts, total_value


def payload_string(amounts: list[tuple[str, float]], max_items: int = 5) -> str:
    parts = [f"{item_id} {format_amount(amount)}" for item_id, amount in amounts[:max_items]]
    if len(amounts) > max_items:
        parts.append(f"+{len(amounts) - max_items} поз.")
    return "; ".join(parts)


def best_ore_payload(target_fe: float, ship: dict[str, str], values: dict[str, float]) -> tuple[str, float, str, str]:
    mining = stat(ship, "mining")
    best_row: dict[str, str] | None = None
    best_vpt = 0.0
    for ore in read_csv(CONFIG_DIR / "ore_deposits.csv"):
        if as_float(ore["min_mining"]) > mining:
            continue
        value_per_ton = ore_value_per_ton(ore, values)
        if value_per_ton > best_vpt:
            best_vpt = value_per_ton
            best_row = ore
    if best_row is None or best_vpt <= 0:
        return "Нет доступной руды", 0.0, "", ""
    tons = target_fe / best_vpt
    columns = {
        "iron_pct": "iron",
        "copper_pct": "copper",
        "magnesium_pct": "magnesium",
        "quartz_pct": "quartz",
        "charcoal_pct": "charcoal",
        "calcite_pct": "calcite",
        "sylvine_pct": "sylvine",
        "claudium_pct": "claudium",
        "monazite_pct": "monazite",
        "gems_pct": "gems",
    }
    amounts, value = resource_breakdown_from_percent(best_row, tons, columns, values)
    return f"{best_row['ore_id']} {tons:.1f} т сырья", value, payload_string(amounts), best_row["ore_id"]


def best_gas_payload(target_fe: float, values: dict[str, float]) -> tuple[str, float, str, str]:
    best_row: dict[str, str] | None = None
    best_vpt = 0.0
    for gas in read_csv(ROOT / "Assets" / "Data" / "Config" / "Gas_condensate_type.csv"):
        value_per_ton = gas_value_per_ton(gas, values)
        if value_per_ton > best_vpt:
            best_vpt = value_per_ton
            best_row = gas
    if best_row is None or best_vpt <= 0:
        return "Нет доступного газа", 0.0, "", ""
    tons = target_fe / best_vpt
    item_ids = [part.strip() for part in best_row["composition_id_item"].split(",") if part.strip()]
    shares = [as_float(part.strip()) for part in best_row["composition_share"].split(",") if part.strip()]
    amounts = [(item_id, tons * 1000.0 * share) for item_id, share in zip(item_ids, shares)]
    value = sum(values[item_id] * amount for item_id, amount in amounts)
    return f"{best_row['id_gas_condensate_type']} {tons:.1f} т конденсата", value, payload_string(amounts), best_row["id_gas_condensate_type"]


def leviathan_payload(target_fe: float, ship: dict[str, str], values: dict[str, float]) -> tuple[str, float, str, str]:
    hunting = stat(ship, "hunting")
    ship_class = ship.get("ship_class_id", "")
    class_mass_limit = {
        "frigate": 45.0,
        "cruiser": 300.0,
        "battleship": 1500.0,
    }.get(ship_class, 45.0)
    score_mass_limit = 5.0 + hunting * 18.0
    mass_limit = min(class_mass_limit, score_mass_limit)
    candidates: list[tuple[float, dict[str, str]]] = []
    for leviathan in read_csv(CONFIG_DIR / "leviathan_butchery.csv"):
        if as_float(leviathan["mass_t"]) <= mass_limit:
            candidates.append((leviathan_value_fe(leviathan, values), leviathan))
    if not candidates:
        return "Нет подходящего левиафана", 0.0, "", ""
    value, row = min((pair for pair in candidates if pair[0] >= target_fe), default=max(candidates, key=lambda pair: pair[0]), key=lambda pair: pair[0])
    columns = {
        "meat_percent": "leviathan_meat",
        "fat_percent": "leviathan_fat",
        "hide_percent": "leviathan_hide",
        "tendon_percent": "leviathan_sinew",
        "bone_grit_percent": "bone_grit",
        "acid_percent": "acid",
        "ichor_percent": "leviathan_ichor",
        "amber_percent": "amber",
    }
    amounts, actual_value = resource_breakdown_from_percent(row, as_float(row["mass_t"]), columns, values)
    return f"{row['leviathan_id']} {row['mass_t']} т туши", actual_value, payload_string(amounts), row["leviathan_id"]


def salvage_payload(target_fe: float, values: dict[str, float]) -> tuple[str, float, str, str]:
    basket = [
        ("automaton_core", 0.34),
        ("automaton_servo_core", 0.24),
        ("automaton_command_cylinder", 0.17),
        ("automaton_logic_drum", 0.13),
        ("automaton_gyroscope", 0.07),
        ("automaton_optic_lens", 0.05),
    ]
    amounts: list[tuple[str, float]] = []
    total = 0.0
    for item_id, share in basket:
        count = math.ceil((target_fe * share) / values[item_id])
        amounts.append((item_id, count))
        total += count * values[item_id]
    note = f"точный сальваж {sum(amount for _, amount in amounts):.0f} узлов"
    parts = [f"{item_id} {amount:.0f} шт." for item_id, amount in amounts]
    return note, total, "; ".join(parts), "automaton_precision_bundle"


def relic_payload(target_fe: float) -> tuple[str, float, str, str]:
    relic_value = 720_000.0
    count = max(1, math.ceil(target_fe / relic_value))
    value = count * relic_value
    detail = f"дорогие вскрытия {count} шт.; ориентир: SP-пакеты, чертежи, одноразовые рецепты, лицензии"
    return f"{count} дорогих вскрытий реликтов", value, detail, "expensive_relic_decode"


def contract_payload(row: dict[str, object], target_fe: float, activity: str) -> tuple[str, float, str, str]:
    freight = as_float(row["freight_fe"])
    resources = as_float(row["resource_fe"])
    profile = as_float(row["profile_currency_fe"])
    reputation = as_float(row["reputation_fe"])
    info = as_float(row["info_fe"])
    material = as_float(row["material_value_fe"])
    intangible = as_float(row["intangible_value_fe"])
    bonus = as_float(row["extraction_intangible_bonus_fe"])
    label = {
        "combat": "боевой контракт",
        "courier": "курьерский контракт",
        "repair": "ремонтный контракт",
    }.get(activity, "контракт")
    details = (
        f"фрахт {freight:.0f} FE; добыча/трофеи {resources:.0f} FE; "
        f"профильная валюта {profile:.0f} FE; репутация {reputation:.0f} FE; "
        f"инфа {info:.0f} FE; материальное {material:.0f} FE; нематериальное {intangible:.0f} FE; бонус выхода {bonus:.0f} FE"
    )
    return f"{label} на {target_fe:.0f} FE+ ценности", as_float(row["total_value_fe"]), details, activity


def build_elite_r10_payload_rows(rows: list[dict[str, object]], values: dict[str, float]) -> list[dict[str, object]]:
    output: list[dict[str, object]] = []
    ships = {row["id_ship"]: row for row in read_csv(SHIP_TREE_PATH)}
    for row in rows:
        if row["mission_profile"] != "elite" or int(row["rank"]) != 10:
            continue
        ship = ships[str(row["ship_id"])]
        target = as_float(row["craft_full_eff_fe"]) / 5.0
        activity = str(row["primary_activity"])
        if activity == "mining":
            payload, value, details, source = best_ore_payload(target, ship, values)
        elif activity == "gas":
            payload, value, details, source = best_gas_payload(target, values)
        elif activity == "hunting":
            payload, value, details, source = leviathan_payload(target, ship, values)
        elif activity == "salvage":
            payload, value, details, source = salvage_payload(target, values)
        elif activity == "relic":
            payload, value, details, source = relic_payload(target)
        else:
            payload, value, details, source = contract_payload(row, target, activity)
        output.append(
            {
                "ship_id": row["ship_id"],
                "local_name_ru": row["local_name_ru"],
                "faction_id": row["faction_id"],
                "ship_class_id": row["ship_class_id"],
                "primary_activity": activity,
                "craft_full_eff_fe": row["craft_full_eff_fe"],
                "target_fe_per_elite_sortie_for_5_sortie_payback": format_fe(target),
                "modeled_total_value_fe": row["total_value_fe"],
                "modeled_liquid_fe": row["liquid_fe"],
                "required_payload": payload,
                "payload_source_id": source,
                "payload_details": details,
                "payload_value_fe": format_fe(value),
                "payload_vs_target": format_float(value / target if target > 0 else 0.0),
            }
        )
    return output


def summarize(rows: list[dict[str, object]], keys: list[str]) -> list[dict[str, object]]:
    grouped: dict[tuple[object, ...], list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        grouped[tuple(row[key] for key in keys)].append(row)

    output: list[dict[str, object]] = []
    for key_tuple, group in sorted(grouped.items(), key=lambda item: item[0]):
        result = {key: value for key, value in zip(keys, key_tuple)}
        result.update(
            {
                "ship_count": len(group),
                "avg_total_value_fe": format_fe(average([as_float(row["total_value_fe"]) for row in group])),
                "avg_liquid_fe": format_fe(average([as_float(row["liquid_fe"]) for row in group])),
                "avg_material_value_fe": format_fe(average([as_float(row["material_value_fe"]) for row in group])),
                "avg_intangible_value_fe": format_fe(average([as_float(row["intangible_value_fe"]) for row in group])),
                "avg_destroyed_total_value_fe": format_fe(average([as_float(row["destroyed_total_value_fe"]) for row in group])),
                "avg_extracted_total_value_fe": format_fe(average([as_float(row["extracted_total_value_fe"]) for row in group])),
                "avg_craft_full_eff_fe": format_fe(average([as_float(row["craft_full_eff_fe"]) for row in group])),
                "avg_craft_raw_fe": format_fe(average([as_float(row["craft_raw_fe"]) for row in group])),
                "avg_buy_estimate_fe": format_fe(average([as_float(row["buy_estimate_fe"]) for row in group])),
                "avg_craft_payback_sorties": format_float(average([as_float(row["craft_payback_sorties"]) for row in group])),
                "avg_buy_payback_sorties": format_float(average([as_float(row["buy_payback_sorties"]) for row in group])),
                "avg_craft_total_value_payback_sorties": format_float(average([as_float(row["craft_total_value_payback_sorties"]) for row in group])),
                "avg_buy_total_value_payback_sorties": format_float(average([as_float(row["buy_total_value_payback_sorties"]) for row in group])),
                "avg_craft_lifetimes_to_payback": format_float(average([as_float(row["craft_lifetimes_to_payback"]) for row in group])),
                "avg_buy_lifetimes_to_payback": format_float(average([as_float(row["buy_lifetimes_to_payback"]) for row in group])),
                "median_craft_payback_sorties": format_float(statistics.median([as_float(row["craft_payback_sorties"]) for row in group])),
                "median_buy_payback_sorties": format_float(statistics.median([as_float(row["buy_payback_sorties"]) for row in group])),
                "median_craft_total_value_payback_sorties": format_float(statistics.median([as_float(row["craft_total_value_payback_sorties"]) for row in group])),
                "median_buy_total_value_payback_sorties": format_float(statistics.median([as_float(row["buy_total_value_payback_sorties"]) for row in group])),
            }
        )
        output.append(result)
    return output


def build_reward_model_rows(rank_budget: dict[int, dict[str, float]]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for rank in range(2, 11):
        budget = rank_budget[rank]
        rows.append(
            {
                "rank": rank,
                "quick_base_fe": format_fe(budget["quick_base_fe"]),
                "ship_xp_base": as_int(budget["ship_xp_base"]),
                "base_sorties": as_int(budget["base_sorties"]),
                "fe_scale_multiplier": FE_SCALE_MULTIPLIER,
                "notes_ru": "Быстрая миссия адаптируется под ранг и профиль корабля; база взята из rank_budget в новой FE-деноминации.",
            }
        )
    return rows


def build_mission_profile_model_rows(rank_budget: dict[int, dict[str, float]]) -> list[dict[str, object]]:
    rows: list[dict[str, object]] = []
    for rank in range(2, 11):
        budget = rank_budget[rank]
        profile = MISSION_PROFILES["quick"]
        base = budget["quick_base_fe"]
        rows.append(
            {
                "mission_profile": "quick",
                "mission_profile_ru": profile["local_name_ru"],
                "scope": profile["scope"],
                "ship_rank": rank,
                "base_fe": format_fe(base),
                "required_power": format_fe(as_float(profile["required_power"])),
                "tuned_base_fe": format_fe(base),
                "ship_xp_base": as_int(budget["ship_xp_base"]),
                "ship_xp_multiplier": format_float(as_float(profile["ship_xp_multiplier"]), 2),
                "capacity_multiplier": format_float(as_float(profile["capacity_multiplier"]), 2),
                "base_sorties": as_int(budget["base_sorties"]),
                "fe_scale_multiplier": FE_SCALE_MULTIPLIER,
                "notes_ru": profile["notes_ru"],
            }
        )

    for mission_profile in ("normal", "danger", "elite"):
        profile = MISSION_PROFILES[mission_profile]
        base = as_float(profile["fixed_base_fe"])
        rows.append(
            {
                "mission_profile": mission_profile,
                "mission_profile_ru": profile["local_name_ru"],
                "scope": profile["scope"],
                "ship_rank": "any",
                "base_fe": format_fe(base),
                "required_power": format_fe(as_float(profile["required_power"])),
                "tuned_base_fe": format_fe(base),
                "ship_xp_base": "by_ship_rank",
                "ship_xp_multiplier": format_float(as_float(profile["ship_xp_multiplier"]), 2),
                "capacity_multiplier": format_float(as_float(profile["capacity_multiplier"]), 2),
                "base_sorties": "by_ship_rank",
                "fe_scale_multiplier": FE_SCALE_MULTIPLIER,
                "notes_ru": profile["notes_ru"],
            }
        )
    return rows


def main() -> None:
    ship_rows = [
        row
        for row in read_csv(SHIP_TREE_PATH)
        if row.get("catalog_scope") == "development" and 2 <= as_int(row.get("rank")) <= 10
    ]
    ships_by_id = {row["id_ship"]: row for row in ship_rows}
    craft_fe = load_craft_fe()
    rank_budget = load_rank_budget()
    buy_multipliers = load_buy_multipliers()
    resource_values = load_resource_values()
    mission_requirements = load_mission_requirements()

    rows: list[dict[str, object]] = []
    for ship_id, ship in sorted(ships_by_id.items()):
        rank = as_int(ship["rank"])
        ship_class = ship["ship_class_id"]
        scores = activity_scores(ship)
        activity = primary_activity(scores)
        profile = ACTIVITY_REWARD_PROFILE[activity]

        budget = rank_budget[rank]
        class_multiplier = CLASS_INCOME_MULTIPLIER.get(ship_class, 1.0)
        variance = deterministic_variance(ship_id)

        craft_full_eff_fe = craft_fe[(ship_id, 5)]
        craft_raw_fe = craft_fe[(ship_id, 0)]
        buy_estimate_fe = craft_full_eff_fe * buy_multipliers[rank]
        base_sorties = budget["base_sorties"]

        for mission_profile, mission in MISSION_PROFILES.items():
            success_factor, requirement_details = requirement_score(ship, mission_profile, activity, mission_requirements)
            archetype_id = mission_archetype_id(mission_profile, activity, mission_requirements)
            power_estimate = ship_power_estimate(ship, rank_budget)
            mission_base = mission_base_fe(rank, mission_profile, rank_budget)
            gross_total_value_fe = mission_base * effectiveness_multiplier(ship, scores) * class_multiplier * variance
            accumulated_total_value_fe = gross_total_value_fe * success_factor
            outcome = reward_outcome_split(accumulated_total_value_fe, profile)
            five_sortie_floor_fe = craft_full_eff_fe / 5.0 if mission_profile == "elite" and rank == 10 and success_factor >= 0.85 else 0.0
            floor_applied = outcome["extracted_total_value_fe"] < five_sortie_floor_fe
            if floor_applied:
                scale = five_sortie_floor_fe / outcome["extracted_total_value_fe"] if outcome["extracted_total_value_fe"] > 0 else 1.0
                accumulated_total_value_fe *= scale
                outcome = reward_outcome_split(accumulated_total_value_fe, profile)

            freight_fe = outcome["freight_fe"]
            resource_fe = outcome["resource_fe"]
            profile_currency_fe = outcome["profile_currency_fe"]
            reputation_fe = outcome["reputation_fe"]
            info_fe = outcome["info_fe"]
            material_value_fe = outcome["material_value_fe"]
            intangible_value_fe = outcome["intangible_value_fe"]
            extraction_intangible_bonus_fe = outcome["extraction_intangible_bonus_fe"]
            destroyed_total_value_fe = outcome["destroyed_total_value_fe"]
            destroyed_liquid_fe = outcome["destroyed_liquid_fe"]
            extracted_total_value_fe = outcome["extracted_total_value_fe"]
            extracted_liquid_fe = outcome["extracted_liquid_fe"]
            extracted_freight_fe = outcome["extracted_freight_fe"]
            extracted_reputation_fe = outcome["extracted_reputation_fe"]
            total_value_fe = extracted_total_value_fe
            liquid_fe = extracted_liquid_fe
            profile_value_fe = extracted_total_value_fe
            base_ship_xp = as_int(budget["ship_xp_base"] * profile["ship_xp_mult"] * as_float(mission["ship_xp_multiplier"]))
            base_mastery_points = as_int((4 + rank * 2 * profile["mastery_mult"]) * max(0.20, success_factor))
            extracted_ship_xp = as_int(base_ship_xp * INTANGIBLE_EXTRACTION_MULTIPLIER)
            extracted_mastery_points = as_int(base_mastery_points * INTANGIBLE_EXTRACTION_MULTIPLIER)

            craft_liquid_payback = craft_full_eff_fe / liquid_fe if liquid_fe > 0 else 0.0
            raw_craft_liquid_payback = craft_raw_fe / liquid_fe if liquid_fe > 0 else 0.0
            buy_liquid_payback = buy_estimate_fe / liquid_fe if liquid_fe > 0 else 0.0
            craft_total_payback = craft_full_eff_fe / profile_value_fe if profile_value_fe > 0 else 0.0
            raw_craft_total_payback = craft_raw_fe / profile_value_fe if profile_value_fe > 0 else 0.0
            buy_total_payback = buy_estimate_fe / profile_value_fe if profile_value_fe > 0 else 0.0
            capacity_target_fe = material_value_fe if activity in MATERIAL_ACTIVITIES else total_value_fe
            capacity_fe, capacity_note = activity_capacity_fe(ship, activity, capacity_target_fe, resource_values)
            capacity_fe *= as_float(mission["capacity_multiplier"])

            rows.append(
                {
                    "mission_profile": mission_profile,
                    "mission_profile_ru": mission["local_name_ru"],
                    "ship_id": ship_id,
                    "local_name_ru": ship["local_name_ru"],
                    "rank": rank,
                    "faction_id": ship["faction_id"],
                    "faction_name_ru": ship["faction_name_ru"],
                    "ship_class_id": ship_class,
                    "role_id": ship["role_id"],
                    "branch_id": ship["branch_id"],
                    "primary_activity": activity,
                    "primary_activity_ru": ACTIVITY_LOCAL_NAME_RU[activity],
                    "primary_activity_score": format_float(scores[activity]),
                    "combat_score": format_float(scores["combat"]),
                    "mining_score": format_float(scores["mining"]),
                    "gas_score": format_float(scores["gas"]),
                    "hunting_score": format_float(scores["hunting"]),
                    "salvage_score": format_float(scores["salvage"]),
                    "relic_score": format_float(scores["relic"]),
                    "repair_score": format_float(scores["repair"]),
                    "courier_score": format_float(scores["courier"]),
                    "effectiveness_multiplier": format_float(effectiveness_multiplier(ship, scores), 4),
                    "class_income_multiplier": format_float(class_multiplier, 2),
                    "mission_scope": mission["scope"],
                    "mission_archetype": archetype_id,
                    "mission_base_fe": format_fe(mission_base),
                    "mission_required_power": format_fe(as_float(mission["required_power"])),
                    "ship_power_estimate": format_fe(power_estimate),
                    "mission_success_factor": format_float(success_factor, 4),
                    "requirement_details": requirement_details,
                    "mission_capacity_multiplier": format_float(as_float(mission["capacity_multiplier"]), 2),
                    "five_sortie_floor_fe": format_fe(five_sortie_floor_fe),
                    "five_sortie_floor_applied": "yes" if floor_applied else "no",
                    "variance_multiplier": format_float(variance, 4),
                    "gross_total_value_fe": format_fe(gross_total_value_fe),
                    "accumulated_total_value_fe": format_fe(accumulated_total_value_fe),
                    "material_value_fe": format_fe(material_value_fe),
                    "intangible_value_fe": format_fe(intangible_value_fe),
                    "extraction_intangible_multiplier": format_float(INTANGIBLE_EXTRACTION_MULTIPLIER, 2),
                    "extraction_intangible_bonus_fe": format_fe(extraction_intangible_bonus_fe),
                    "destroyed_total_value_fe": format_fe(destroyed_total_value_fe),
                    "destroyed_liquid_fe": format_fe(destroyed_liquid_fe),
                    "extracted_total_value_fe": format_fe(extracted_total_value_fe),
                    "extracted_liquid_fe": format_fe(extracted_liquid_fe),
                    "total_value_fe": format_fe(total_value_fe),
                    "freight_fe": format_fe(freight_fe),
                    "extracted_freight_fe": format_fe(extracted_freight_fe),
                    "resource_fe": format_fe(resource_fe),
                    "profile_currency_fe": format_fe(profile_currency_fe),
                    "reputation_fe": format_fe(reputation_fe),
                    "extracted_reputation_fe": format_fe(extracted_reputation_fe),
                    "info_fe": format_fe(info_fe),
                    "liquid_fe": format_fe(liquid_fe),
                    "base_ship_xp": base_ship_xp,
                    "destroyed_ship_xp": base_ship_xp,
                    "extracted_ship_xp": extracted_ship_xp,
                    "ship_xp": extracted_ship_xp,
                    "base_mastery_points": base_mastery_points,
                    "destroyed_mastery_points": base_mastery_points,
                    "extracted_mastery_points": extracted_mastery_points,
                    "mastery_points": extracted_mastery_points,
                    "craft_full_eff_fe": format_fe(craft_full_eff_fe),
                    "craft_raw_fe": format_fe(craft_raw_fe),
                    "buy_estimate_fe": format_fe(buy_estimate_fe),
                    "base_sorties": as_int(base_sorties),
                    "craft_payback_sorties": format_float(craft_liquid_payback),
                    "raw_craft_payback_sorties": format_float(raw_craft_liquid_payback),
                    "buy_payback_sorties": format_float(buy_liquid_payback),
                    "craft_total_value_payback_sorties": format_float(craft_total_payback),
                    "raw_craft_total_value_payback_sorties": format_float(raw_craft_total_payback),
                    "buy_total_value_payback_sorties": format_float(buy_total_payback),
                    "craft_lifetimes_to_payback": format_float(craft_liquid_payback / base_sorties),
                    "raw_craft_lifetimes_to_payback": format_float(raw_craft_liquid_payback / base_sorties),
                    "buy_lifetimes_to_payback": format_float(buy_liquid_payback / base_sorties),
                    "activity_capacity_fe": format_fe(capacity_fe),
                    "activity_capacity_note": capacity_note,
                    "capacity_target_fe": format_fe(capacity_target_fe),
                    "capacity_vs_material_value": format_float(capacity_fe / material_value_fe if material_value_fe > 0 else 0.0),
                    "capacity_vs_required_value": format_float(capacity_fe / capacity_target_fe if capacity_target_fe > 0 else 0.0),
                    "capacity_vs_total_value": format_float(capacity_fe / total_value_fe if total_value_fe > 0 else 0.0),
                    "capacity_vs_liquid_value": format_float(capacity_fe / liquid_fe if liquid_fe > 0 else 0.0),
                    "capacity_vs_five_sortie_craft_target": format_float(capacity_fe / (craft_full_eff_fe / 5.0) if craft_full_eff_fe > 0 else 0.0),
                }
            )

    fieldnames = [
        "mission_profile",
        "mission_profile_ru",
        "ship_id",
        "local_name_ru",
        "rank",
        "faction_id",
        "faction_name_ru",
        "ship_class_id",
        "role_id",
        "branch_id",
        "primary_activity",
        "primary_activity_ru",
        "primary_activity_score",
        "combat_score",
        "mining_score",
        "gas_score",
        "hunting_score",
        "salvage_score",
        "relic_score",
        "repair_score",
        "courier_score",
        "effectiveness_multiplier",
        "class_income_multiplier",
        "mission_scope",
        "mission_archetype",
        "mission_base_fe",
        "mission_required_power",
        "ship_power_estimate",
        "mission_success_factor",
        "requirement_details",
        "mission_capacity_multiplier",
        "five_sortie_floor_fe",
        "five_sortie_floor_applied",
        "variance_multiplier",
        "gross_total_value_fe",
        "accumulated_total_value_fe",
        "material_value_fe",
        "intangible_value_fe",
        "extraction_intangible_multiplier",
        "extraction_intangible_bonus_fe",
        "destroyed_total_value_fe",
        "destroyed_liquid_fe",
        "extracted_total_value_fe",
        "extracted_liquid_fe",
        "total_value_fe",
        "freight_fe",
        "extracted_freight_fe",
        "resource_fe",
        "profile_currency_fe",
        "reputation_fe",
        "extracted_reputation_fe",
        "info_fe",
        "liquid_fe",
        "base_ship_xp",
        "destroyed_ship_xp",
        "extracted_ship_xp",
        "ship_xp",
        "base_mastery_points",
        "destroyed_mastery_points",
        "extracted_mastery_points",
        "mastery_points",
        "craft_full_eff_fe",
        "craft_raw_fe",
        "buy_estimate_fe",
        "base_sorties",
        "craft_payback_sorties",
        "raw_craft_payback_sorties",
        "buy_payback_sorties",
        "craft_total_value_payback_sorties",
        "raw_craft_total_value_payback_sorties",
        "buy_total_value_payback_sorties",
        "craft_lifetimes_to_payback",
        "raw_craft_lifetimes_to_payback",
        "buy_lifetimes_to_payback",
        "activity_capacity_fe",
        "activity_capacity_note",
        "capacity_target_fe",
        "capacity_vs_material_value",
        "capacity_vs_required_value",
        "capacity_vs_total_value",
        "capacity_vs_liquid_value",
        "capacity_vs_five_sortie_craft_target",
    ]

    write_csv(CONFIG_DIR / "quick_sortie_reward_model.csv", build_reward_model_rows(rank_budget), [
        "rank",
        "quick_base_fe",
        "ship_xp_base",
        "base_sorties",
        "fe_scale_multiplier",
        "notes_ru",
    ])
    write_csv(CONFIG_DIR / "mission_profile_reward_model.csv", build_mission_profile_model_rows(rank_budget), [
        "mission_profile",
        "mission_profile_ru",
        "scope",
        "ship_rank",
        "base_fe",
        "required_power",
        "tuned_base_fe",
        "ship_xp_base",
        "ship_xp_multiplier",
        "capacity_multiplier",
        "base_sorties",
        "fe_scale_multiplier",
        "notes_ru",
    ])
    write_csv(CONFIG_DIR / "mission_profile_ship_profit.csv", rows, fieldnames)

    quick_rows = [row for row in rows if row["mission_profile"] == "quick"]
    elite_r10_rows = [row for row in rows if row["mission_profile"] == "elite" and int(row["rank"]) == 10]
    write_csv(CONFIG_DIR / "quick_sortie_ship_profit.csv", quick_rows, fieldnames)
    write_csv(CONFIG_DIR / "elite_r10_capacity_check.csv", elite_r10_rows, fieldnames)
    write_csv(CONFIG_DIR / "elite_r10_required_payloads.csv", build_elite_r10_payload_rows(rows, resource_values), [
        "ship_id",
        "local_name_ru",
        "faction_id",
        "ship_class_id",
        "primary_activity",
        "craft_full_eff_fe",
        "target_fe_per_elite_sortie_for_5_sortie_payback",
        "modeled_total_value_fe",
        "modeled_liquid_fe",
        "required_payload",
        "payload_source_id",
        "payload_details",
        "payload_value_fe",
        "payload_vs_target",
    ])

    mission_rank_summary = summarize(rows, ["mission_profile", "rank"])
    mission_class_summary = summarize(rows, ["mission_profile", "rank", "ship_class_id"])
    mission_faction_summary = summarize(rows, ["mission_profile", "rank", "faction_id"])
    mission_activity_summary = summarize(rows, ["mission_profile", "rank", "primary_activity"])
    rank_summary = summarize(quick_rows, ["rank"])
    class_summary = summarize(quick_rows, ["rank", "ship_class_id"])
    faction_summary = summarize(quick_rows, ["rank", "faction_id"])
    activity_summary = summarize(quick_rows, ["rank", "primary_activity"])

    summary_fieldnames = [
        "rank",
        "ship_count",
        "avg_total_value_fe",
        "avg_liquid_fe",
        "avg_material_value_fe",
        "avg_intangible_value_fe",
        "avg_destroyed_total_value_fe",
        "avg_extracted_total_value_fe",
        "avg_craft_full_eff_fe",
        "avg_craft_raw_fe",
        "avg_buy_estimate_fe",
        "avg_craft_payback_sorties",
        "avg_buy_payback_sorties",
        "avg_craft_total_value_payback_sorties",
        "avg_buy_total_value_payback_sorties",
        "avg_craft_lifetimes_to_payback",
        "avg_buy_lifetimes_to_payback",
        "median_craft_payback_sorties",
        "median_buy_payback_sorties",
        "median_craft_total_value_payback_sorties",
        "median_buy_total_value_payback_sorties",
    ]
    mission_summary_fieldnames = ["mission_profile"] + summary_fieldnames
    write_csv(CONFIG_DIR / "mission_profile_rank_payback_summary.csv", mission_rank_summary, mission_summary_fieldnames)
    write_csv(
        CONFIG_DIR / "mission_profile_class_payback_summary.csv",
        mission_class_summary,
        ["mission_profile", "rank", "ship_class_id"] + summary_fieldnames[1:],
    )
    write_csv(
        CONFIG_DIR / "mission_profile_faction_payback_summary.csv",
        mission_faction_summary,
        ["mission_profile", "rank", "faction_id"] + summary_fieldnames[1:],
    )
    write_csv(
        CONFIG_DIR / "mission_profile_activity_payback_summary.csv",
        mission_activity_summary,
        ["mission_profile", "rank", "primary_activity"] + summary_fieldnames[1:],
    )
    write_csv(CONFIG_DIR / "quick_sortie_rank_payback_summary.csv", rank_summary, summary_fieldnames)
    write_csv(
        CONFIG_DIR / "quick_sortie_class_payback_summary.csv",
        class_summary,
        ["rank", "ship_class_id"] + summary_fieldnames[1:],
    )
    write_csv(
        CONFIG_DIR / "quick_sortie_faction_payback_summary.csv",
        faction_summary,
        ["rank", "faction_id"] + summary_fieldnames[1:],
    )
    write_csv(
        CONFIG_DIR / "quick_sortie_activity_payback_summary.csv",
        activity_summary,
        ["rank", "primary_activity"] + summary_fieldnames[1:],
    )

    print(f"Mission profile payback calculated for {len(rows)} ship/profile rows.")
    for summary in rank_summary:
        print(
            "R{rank}: avg liquid {liquid} FE, craft payback {craft} sorties, buy payback {buy} sorties".format(
                rank=summary["rank"],
                liquid=summary["avg_liquid_fe"],
                craft=summary["avg_craft_payback_sorties"],
                buy=summary["avg_buy_payback_sorties"],
            )
        )


if __name__ == "__main__":
    main()
