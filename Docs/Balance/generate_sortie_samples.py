from __future__ import annotations

import csv
import hashlib
import math
import random
import statistics
from collections import defaultdict
from pathlib import Path
from typing import Any

from calculate_quick_sortie_payback import (
    CONFIG_DIR,
    ROOT,
    SHIP_TREE_PATH,
    INTANGIBLE_EXTRACTION_MULTIPLIER,
    as_float,
    as_int,
    cargo_rating,
    gas_value_per_ton,
    leviathan_value_fe,
    ore_value_per_ton,
    read_csv,
    stat,
    write_csv,
)


SAMPLE_COUNT = 100

PROFILE_TOLERANCE = {
    "quick": 0.20,
    "normal": 0.25,
    "danger": 0.35,
    "elite": 0.45,
}

PROFILE_SPREAD = {
    "quick": 0.14,
    "normal": 0.20,
    "danger": 0.28,
    "elite": 0.38,
}

PROFILE_DESTROY_BASE = {
    "quick": 0.005,
    "normal": 0.025,
    "danger": 0.055,
    "elite": 0.085,
}

PROFILE_DESTROY_PRESSURE = {
    "quick": 0.06,
    "normal": 0.32,
    "danger": 0.55,
    "elite": 0.78,
}

PROFILE_DAMAGED_BASE = {
    "quick": 0.05,
    "normal": 0.12,
    "danger": 0.20,
    "elite": 0.30,
}

MATERIAL_ACTIVITIES = {"mining", "gas", "hunting", "salvage", "relic"}

AUTOMATON_ITEM_MASS_T = {
    "automaton_relay": 0.20,
    "automaton_contact_comb": 0.24,
    "automaton_coil": 0.28,
    "automaton_brass_valve": 0.38,
    "automaton_mainspring": 0.55,
    "automaton_pressure_gauge": 0.65,
    "automaton_calibration_gear": 0.70,
    "automaton_servo_joint": 0.80,
    "automaton_optic_lens": 0.90,
    "automaton_gyroscope": 0.95,
    "automaton_logic_drum": 1.10,
    "automaton_command_cylinder": 1.25,
    "automaton_servo_core": 1.35,
    "automaton_clock_brain": 1.50,
}

RELIC_CLASS_VALUE = {
    "cheap": 60_000.0,
    "medium": 180_000.0,
    "valuable": 520_000.0,
    "expensive": 880_000.0,
}

SERVICE_PAYLOAD = {
    "combat": [("munition_bundle", 0.50), ("weapon", 0.30), ("mechanisms", 0.20)],
    "courier": [("mechanisms", 0.45), ("munition_bundle", 0.25), ("weapon", 0.15), ("perfcards", 0.15)],
    "repair": [("mechanisms", 0.55), ("munition_bundle", 0.20), ("perfcards", 0.15), ("nobel", 0.10)],
}


def format_fe(value: float) -> str:
    return f"{value:.2f}"


def stable_seed(text: str) -> int:
    return int(hashlib.sha256(text.encode("utf-8")).hexdigest()[:16], 16)


def stable_float(text: str, low: float, high: float) -> float:
    value = stable_seed(text) / float(0xFFFFFFFFFFFFFFFF)
    return low + (high - low) * value


def weighted_choice(rng: random.Random, entries: list[tuple[dict[str, str], float]]) -> dict[str, str]:
    total = sum(max(0.0, weight) for _, weight in entries)
    if total <= 0:
        return entries[0][0]
    roll = rng.random() * total
    current = 0.0
    for entry, weight in entries:
        current += max(0.0, weight)
        if current >= roll:
            return entry
    return entries[-1][0]


def weighted_item_choice(rng: random.Random, entries: list[tuple[str, float]]) -> str:
    total = sum(max(0.0, weight) for _, weight in entries)
    roll = rng.random() * total
    current = 0.0
    for item_id, weight in entries:
        current += max(0.0, weight)
        if current >= roll:
            return item_id
    return entries[-1][0]


def add_payload(
    payloads: list[dict[str, Any]],
    sample_id: str,
    layer: str,
    source_id: str,
    item_id: str,
    amount: float,
    unit: str,
    value_fe: float,
    cargo_tons: float,
) -> None:
    if amount <= 0 or value_fe <= 0:
        return
    payloads.append(
        {
            "sample_id": sample_id,
            "layer": layer,
            "source_id": source_id,
            "item_id": item_id,
            "amount": f"{amount:.3f}",
            "unit": unit,
            "value_fe": format_fe(value_fe),
            "cargo_tons": f"{cargo_tons:.4f}",
        }
    )


def item_values_by_id() -> dict[str, float]:
    return {row["item_id"]: as_float(row["value_fe_per_unit"]) for row in read_csv(CONFIG_DIR / "resource_value_reference.csv")}


def item_categories_by_id() -> dict[str, str]:
    return {row["item_id"]: row["category"] for row in read_csv(CONFIG_DIR / "resource_value_reference.csv")}


def mission_factor(row: dict[str, str], profile: str, rng: random.Random) -> float:
    spread = PROFILE_SPREAD[profile]
    center = 1.0
    return rng.triangular(center - spread, center + spread, center)


def source_hint(profile: str, score: float, rank: int) -> float:
    if profile == "quick":
        return min(score, 8.0 + rank * 5.5)
    if profile == "normal":
        return min(score, 42.0)
    if profile == "danger":
        return min(score, 62.0)
    return min(score, 82.0)


def ore_columns() -> dict[str, str]:
    return {
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


def generate_ore_payload(
    sample_id: str,
    ship: dict[str, str],
    mission_profile: str,
    target_fe: float,
    rng: random.Random,
    values: dict[str, float],
) -> tuple[list[dict[str, Any]], float, float, str]:
    mining = stat(ship, "mining")
    rank = as_int(ship["rank"])
    cargo_tons = as_float(ship.get("cargo_capacity_tons", ""))
    payloads: list[dict[str, Any]] = []
    if mining <= 0 or cargo_tons <= 0 or target_fe <= 0:
        return payloads, 0.0, 0.0, "no_ore_tool"

    hint = source_hint(mission_profile, mining, rank)
    ores = []
    for ore in read_csv(CONFIG_DIR / "ore_deposits.csv"):
        min_mining = as_float(ore["min_mining"])
        if min_mining > mining:
            continue
        closeness = 1.0 / (1.0 + abs(min_mining - hint) / 18.0)
        value_density = ore_value_per_ton(ore, values)
        weight = (as_float(ore["base_weight"]) + as_float(ore["lucky_weight"]) * rng.random()) * closeness * (1.0 + value_density / 160_000.0)
        ores.append((ore, weight))
    if not ores:
        return payloads, 0.0, 0.0, "no_eligible_ore"

    load_factor = max(0.0, min(1.0, 0.55 + mining * 0.0045))
    crusher_budget = cargo_tons * load_factor * (0.75 + mining / 50.0)
    crusher_left = crusher_budget * stable_float(f"{sample_id}:crusher", 0.88, 1.08)
    cargo_left = cargo_tons * stable_float(f"{sample_id}:cargo", 0.78, 1.0)
    chunks = 1 + int(rng.random() * (2 if mission_profile in ("quick", "normal") else 4))
    remaining_fe = target_fe
    total_fe = 0.0
    total_cargo = 0.0
    sources: list[str] = []

    for _ in range(chunks + 3):
        if remaining_fe <= target_fe * 0.04 or crusher_left <= 0 or cargo_left <= 0:
            break
        ore = weighted_choice(rng, ores)
        ore_value = max(1.0, ore_value_per_ton(ore, values))
        useful_pct = max(1.0, 100.0 - as_float(ore["waste_pct"]))
        desired_fe = min(remaining_fe, target_fe / chunks * stable_float(f"{sample_id}:{ore['ore_id']}:{_}", 0.60, 1.55))
        raw_tons = desired_fe / ore_value
        raw_tons = min(raw_tons, crusher_left / max(0.01, as_float(ore["crusher_wear_per_ton"])))
        raw_tons = min(raw_tons, cargo_left / (useful_pct / 100.0))
        if raw_tons <= 0:
            continue
        crusher_left -= raw_tons * as_float(ore["crusher_wear_per_ton"])
        useful_tons = raw_tons * useful_pct / 100.0
        cargo_left -= useful_tons
        total_cargo += useful_tons
        source_fe = 0.0
        for column, item_id in ore_columns().items():
            kg = raw_tons * 1000.0 * as_float(ore[column]) / 100.0
            value = kg * values[item_id]
            source_fe += value
            add_payload(payloads, sample_id, "material", ore["ore_id"], item_id, kg, "kg", value, kg / 1000.0)
        total_fe += source_fe
        remaining_fe = max(0.0, target_fe - total_fe)
        sources.append(f"{ore['ore_id']} {raw_tons:.1f}t")

    return payloads, total_fe, total_cargo, "; ".join(sources) if sources else "ore_empty"


def gas_rows_with_weights(
    ship: dict[str, str],
    mission_profile: str,
    rng: random.Random,
    values: dict[str, float],
) -> list[tuple[dict[str, str], float]]:
    harvesting = stat(ship, "harvesting")
    hint_by_profile = {"quick": 0.25, "normal": 0.45, "danger": 0.68, "elite": 0.86}
    gases = read_csv(ROOT / "Assets" / "Data" / "Config" / "Gas_condensate_type.csv")
    densities = {gas["id_gas_condensate_type"]: gas_value_per_ton(gas, values) for gas in gases}
    min_density = min(densities.values())
    max_density = max(densities.values())
    weighted: list[tuple[dict[str, str], float]] = []
    for gas in gases:
        density_norm = (densities[gas["id_gas_condensate_type"]] - min_density) / max(1.0, max_density - min_density)
        score_gate = max(0.15, min(1.0, harvesting / (20.0 + density_norm * 80.0)))
        closeness = 1.0 / (1.0 + abs(density_norm - hint_by_profile[mission_profile]) * 4.0)
        weighted.append((gas, score_gate * closeness * (0.7 + rng.random() * 0.6)))
    return weighted


def generate_gas_payload(
    sample_id: str,
    ship: dict[str, str],
    mission_profile: str,
    target_fe: float,
    rng: random.Random,
    values: dict[str, float],
) -> tuple[list[dict[str, Any]], float, float, str]:
    harvesting = stat(ship, "harvesting")
    cargo_tons = as_float(ship.get("cargo_capacity_tons", ""))
    payloads: list[dict[str, Any]] = []
    if harvesting <= 0 or cargo_tons <= 0 or target_fe <= 0:
        return payloads, 0.0, 0.0, "no_gas_tool"

    filter_factor = max(0.18, min(1.0, 0.42 + harvesting * 0.007))
    cargo_left = cargo_tons * filter_factor * stable_float(f"{sample_id}:gas_cargo", 0.75, 1.05)
    remaining_fe = target_fe
    total_fe = 0.0
    total_cargo = 0.0
    sources: list[str] = []
    weighted = gas_rows_with_weights(ship, mission_profile, rng, values)
    clouds = 1 + int(rng.random() * (2 if mission_profile in ("quick", "normal") else 4))

    for index in range(clouds + 3):
        if remaining_fe <= target_fe * 0.04 or cargo_left <= 0:
            break
        gas = weighted_choice(rng, weighted)
        density = max(1.0, gas_value_per_ton(gas, values))
        desired_fe = min(remaining_fe, target_fe / clouds * stable_float(f"{sample_id}:{gas['id_gas_condensate_type']}:{index}", 0.70, 1.45))
        tons = min(cargo_left, desired_fe / density)
        if tons <= 0:
            continue
        cargo_left -= tons
        total_cargo += tons
        item_ids = [part.strip() for part in gas["composition_id_item"].split(",") if part.strip()]
        shares = [as_float(part.strip()) for part in gas["composition_share"].split(",") if part.strip()]
        source_fe = 0.0
        for item_id, share in zip(item_ids, shares):
            kg = tons * 1000.0 * share
            value = kg * values[item_id]
            source_fe += value
            add_payload(payloads, sample_id, "material", gas["id_gas_condensate_type"], item_id, kg, "kg", value, kg / 1000.0)
        total_fe += source_fe
        remaining_fe = max(0.0, target_fe - total_fe)
        sources.append(f"{gas['id_gas_condensate_type']} {tons:.1f}t")

    return payloads, total_fe, total_cargo, "; ".join(sources) if sources else "gas_empty"


def generate_leviathan_payload(
    sample_id: str,
    ship: dict[str, str],
    mission_profile: str,
    target_fe: float,
    rng: random.Random,
    values: dict[str, float],
) -> tuple[list[dict[str, Any]], float, float, str]:
    hunting = stat(ship, "hunting")
    payloads: list[dict[str, Any]] = []
    if hunting <= 0 or target_fe <= 0:
        return payloads, 0.0, 0.0, "no_hunting_tool"
    class_mass_limit = {"frigate": 45.0, "cruiser": 300.0, "battleship": 1500.0}.get(ship.get("ship_class_id", ""), 45.0)
    mass_limit = min(class_mass_limit, 5.0 + hunting * 18.0)
    hint_mass = {
        "quick": mass_limit * 0.20,
        "normal": mass_limit * 0.36,
        "danger": mass_limit * 0.58,
        "elite": mass_limit * 0.82,
    }[mission_profile]
    leviathans = []
    for leviathan in read_csv(CONFIG_DIR / "leviathan_butchery.csv"):
        mass = as_float(leviathan["mass_t"])
        if mass > mass_limit:
            continue
        closeness = 1.0 / (1.0 + abs(mass - hint_mass) / max(10.0, hint_mass))
        leviathans.append((leviathan, closeness * (0.7 + rng.random() * 0.6)))
    if not leviathans:
        return payloads, 0.0, 0.0, "no_eligible_leviathan"

    butcher_eff = max(0.25, min(1.0, 0.38 + hunting * 0.006))
    remaining_fe = target_fe
    total_fe = 0.0
    total_cargo = 0.0
    sources: list[str] = []
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
    attempts = 1 + int(rng.random() * (2 if mission_profile in ("quick", "normal") else 4))
    for index in range(attempts + 3):
        if remaining_fe <= target_fe * 0.04:
            break
        leviathan = weighted_choice(rng, leviathans)
        full_value = leviathan_value_fe(leviathan, values) * butcher_eff
        take_ratio = min(1.0, max(0.00001, remaining_fe / max(1.0, full_value)))
        take_ratio *= stable_float(f"{sample_id}:{leviathan['leviathan_id']}:{index}", 0.85, 1.08)
        take_ratio = min(1.0, take_ratio)
        source_fe = 0.0
        source_cargo = 0.0
        mass_kg = as_float(leviathan["mass_t"]) * 1000.0
        for column, item_id in fields.items():
            kg = mass_kg * as_float(leviathan[column]) / 100.0 * butcher_eff * take_ratio
            value = kg * values[item_id]
            source_fe += value
            source_cargo += kg / 1000.0
            add_payload(payloads, sample_id, "material", leviathan["leviathan_id"], item_id, kg, "kg", value, kg / 1000.0)
        total_fe += source_fe
        total_cargo += source_cargo
        remaining_fe = max(0.0, target_fe - total_fe)
        sources.append(f"{leviathan['leviathan_id']} {take_ratio:.2f}")
    return payloads, total_fe, total_cargo, "; ".join(sources) if sources else "leviathan_empty"


def generate_salvage_payload(
    sample_id: str,
    ship: dict[str, str],
    mission_profile: str,
    target_fe: float,
    rng: random.Random,
    values: dict[str, float],
    categories: dict[str, str],
) -> tuple[list[dict[str, Any]], float, float, str]:
    score = max(stat(ship, "salvage"), stat(ship, "warfare") * 0.65)
    payloads: list[dict[str, Any]] = []
    if score <= 0 or target_fe <= 0:
        return payloads, 0.0, 0.0, "no_salvage_tool"
    items = [item_id for item_id, category in categories.items() if category == "automaton_salvage"]
    difficulty_bias = {"quick": 0.25, "normal": 0.45, "danger": 0.68, "elite": 0.86}[mission_profile]
    min_value = min(values[item_id] for item_id in items)
    max_value = max(values[item_id] for item_id in items)
    weighted_items = []
    for item_id in items:
        value_norm = (values[item_id] - min_value) / max(1.0, max_value - min_value)
        gate = max(0.15, min(1.0, score / (25.0 + value_norm * 75.0)))
        closeness = 1.0 / (1.0 + abs(value_norm - difficulty_bias) * 3.5)
        weighted_items.append((item_id, gate * closeness * (0.8 + rng.random() * 0.5)))

    remaining_fe = target_fe
    total_fe = 0.0
    total_cargo = 0.0
    sources: list[str] = []
    loops = 3 + int(rng.random() * (4 if mission_profile in ("danger", "elite") else 2))
    for index in range(loops + 8):
        if remaining_fe <= target_fe * 0.03:
            break
        item_id = weighted_item_choice(rng, weighted_items)
        item_value = max(1.0, values[item_id])
        desired = min(remaining_fe, target_fe / loops * stable_float(f"{sample_id}:{item_id}:{index}", 0.45, 1.35))
        count = max(1, int(round(desired / item_value)))
        value = count * item_value
        cargo = count * AUTOMATON_ITEM_MASS_T.get(item_id, 0.55) * 0.05
        total_fe += value
        total_cargo += cargo
        remaining_fe = max(0.0, target_fe - total_fe)
        add_payload(payloads, sample_id, "material", "automaton_field", item_id, count, "pcs", value, cargo)
        sources.append(f"{item_id} x{count}")
    return payloads, total_fe, total_cargo, "; ".join(sources) if sources else "salvage_empty"


def generate_relic_payload(
    sample_id: str,
    ship: dict[str, str],
    mission_profile: str,
    target_fe: float,
    rng: random.Random,
) -> tuple[list[dict[str, Any]], float, float, str]:
    score = max(stat(ship, "hacking"), stat(ship, "survey"))
    payloads: list[dict[str, Any]] = []
    if score <= 0 or target_fe <= 0:
        return payloads, 0.0, 0.0, "no_relic_tool"
    class_bias = {"quick": "cheap", "normal": "medium", "danger": "valuable", "elite": "expensive"}[mission_profile]
    class_order = {"cheap": 0, "medium": 1, "valuable": 2, "expensive": 3}
    relics = []
    for relic in read_csv(CONFIG_DIR / "relic_decode.csv"):
        cls = relic["class"]
        gate = max(0.15, min(1.0, score / (25.0 + class_order[cls] * 22.0)))
        closeness = 1.0 / (1.0 + abs(class_order[cls] - class_order[class_bias]))
        relics.append((relic, gate * closeness * (0.8 + rng.random() * 0.5)))

    attempts = max(1, int(1 + score / 18.0))
    if mission_profile == "quick":
        attempts = max(1, attempts - 2)
    elif mission_profile == "elite":
        attempts += 2

    remaining_fe = target_fe
    total_fe = 0.0
    total_cargo = 0.0
    sources: list[str] = []
    for index in range(attempts + 4):
        if remaining_fe <= target_fe * 0.03:
            break
        relic = weighted_choice(rng, relics)
        base_value = RELIC_CLASS_VALUE[relic["class"]] * stable_float(f"{sample_id}:{relic['relic_id']}:{index}", 0.82, 1.20)
        value = min(base_value, remaining_fe * stable_float(f"{sample_id}:take:{index}", 0.75, 1.25))
        value = max(value, min(base_value, remaining_fe))
        cargo = as_float(relic["typical_weight_kg"]) / 1000.0
        total_fe += value
        total_cargo += cargo
        remaining_fe = max(0.0, target_fe - total_fe)
        add_payload(payloads, sample_id, "material", relic["relic_id"], f"relic_{relic['class']}", 1, "pcs", value, cargo)
        sources.append(relic["relic_id"])
    return payloads, total_fe, total_cargo, "; ".join(sources) if sources else "relic_empty"


def generate_service_payload(
    sample_id: str,
    activity: str,
    target_fe: float,
    rng: random.Random,
    values: dict[str, float],
) -> tuple[list[dict[str, Any]], float, float, str]:
    payloads: list[dict[str, Any]] = []
    if target_fe <= 0:
        return payloads, 0.0, 0.0, "no_service_material"
    entries = SERVICE_PAYLOAD.get(activity, SERVICE_PAYLOAD["courier"])
    total_fe = 0.0
    total_cargo = 0.0
    sources: list[str] = []
    for item_id, share in entries:
        desired = target_fe * share * stable_float(f"{sample_id}:{item_id}", 0.82, 1.18)
        value = values.get(item_id, 0.0)
        if value <= 0:
            continue
        count = max(0.05, desired / value)
        item_fe = count * value
        cargo = count * (0.001 if item_id in ("perfcards", "nobel") else 0.005)
        total_fe += item_fe
        total_cargo += cargo
        add_payload(payloads, sample_id, "material", f"{activity}_contract", item_id, count, "crate", item_fe, cargo)
        sources.append(f"{item_id} x{count}")
    return payloads, total_fe, total_cargo, "; ".join(sources)


def generate_material_payload(
    sample_id: str,
    ship: dict[str, str],
    mission_row: dict[str, str],
    target_fe: float,
    rng: random.Random,
    values: dict[str, float],
    categories: dict[str, str],
) -> tuple[list[dict[str, Any]], float, float, str]:
    activity = mission_row["primary_activity"]
    profile = mission_row["mission_profile"]
    if activity == "mining":
        return generate_ore_payload(sample_id, ship, profile, target_fe, rng, values)
    if activity == "gas":
        return generate_gas_payload(sample_id, ship, profile, target_fe, rng, values)
    if activity == "hunting":
        return generate_leviathan_payload(sample_id, ship, profile, target_fe, rng, values)
    if activity == "salvage":
        return generate_salvage_payload(sample_id, ship, profile, target_fe, rng, values, categories)
    if activity == "relic":
        return generate_relic_payload(sample_id, ship, profile, target_fe, rng)
    if activity == "combat":
        return generate_salvage_payload(sample_id, ship, profile, target_fe * 0.65, rng, values, categories)
    return generate_service_payload(sample_id, activity, target_fe, rng, values)


def outcome_for_sortie(mission_row: dict[str, str], rng: random.Random) -> tuple[str, float, float, float]:
    profile = mission_row["mission_profile"]
    success = as_float(mission_row["mission_success_factor"])
    miss = max(0.0, 1.0 - success)
    destroyed_chance = min(0.92, PROFILE_DESTROY_BASE[profile] + miss * miss * PROFILE_DESTROY_PRESSURE[profile])
    damaged_chance = min(0.75, PROFILE_DAMAGED_BASE[profile] + miss * 0.45)
    roll = rng.random()
    if roll < destroyed_chance:
        return "destroyed", destroyed_chance, damaged_chance, 100.0
    if roll < destroyed_chance + damaged_chance:
        damage = stable_float(f"{mission_row['ship_id']}:{profile}:{roll}:damage", 18.0, 72.0)
        return "extracted_damaged", destroyed_chance, damaged_chance, damage
    return "extracted_clean", destroyed_chance, damaged_chance, stable_float(f"{mission_row['ship_id']}:{profile}:{roll}:scratch", 0.0, 16.0)


def generate_intangible_rows(sample_id: str, mission_row: dict[str, str], generated_intangible_fe: float) -> list[dict[str, Any]]:
    activity = mission_row["primary_activity"]
    freight = as_float(mission_row["freight_fe"])
    reputation = as_float(mission_row["reputation_fe"])
    base_xp = as_int(mission_row["base_ship_xp"])
    base_mastery = as_int(mission_row["base_mastery_points"])
    base_intangible = as_float(mission_row["intangible_value_fe"])
    scale = generated_intangible_fe / base_intangible if base_intangible > 0 else 1.0
    rows: list[dict[str, Any]] = []
    add_payload(rows, sample_id, "intangible", f"{activity}_action", "freight_fe", freight * scale, "fe", freight * scale, 0.0)
    add_payload(rows, sample_id, "intangible", f"{activity}_action", "reputation_fe", reputation * scale, "fe", reputation * scale, 0.0)
    add_payload(rows, sample_id, "intangible", f"{activity}_action", "ship_xp", base_xp * scale, "xp", 0.0, 0.0)
    add_payload(rows, sample_id, "intangible", f"{activity}_action", "mastery_points", base_mastery * scale, "points", 0.0, 0.0)
    metric_name = {
        "combat": "damage_done",
        "repair": "repair_done",
        "courier": "delivery_score",
        "relic": "survey_hack_score",
        "mining": "site_control",
        "gas": "cloud_control",
        "hunting": "capture_pressure",
        "salvage": "field_suppression",
    }.get(activity, "mission_work")
    add_payload(rows, sample_id, "intangible", f"{activity}_action", metric_name, generated_intangible_fe * 0.35, "score", 0.0, 0.0)
    return rows


def sample_hash(sample: dict[str, Any], payloads: list[dict[str, Any]]) -> str:
    material = [
        sample["outcome"],
        sample["generated_material_fe"],
        sample["generated_intangible_fe"],
        sample["generated_extracted_total_fe"],
        sample["cargo_used_tons"],
        "|".join(f"{p['layer']}:{p['source_id']}:{p['item_id']}:{p['amount']}:{p['value_fe']}" for p in payloads),
    ]
    return hashlib.sha256("||".join(str(part) for part in material).encode("utf-8")).hexdigest()[:16]


def generate_sortie(
    sample_id: str,
    mission_row: dict[str, str],
    ship: dict[str, str],
    values: dict[str, float],
    categories: dict[str, str],
    seed: str,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    rng = random.Random(stable_seed(seed))
    profile = mission_row["mission_profile"]
    factor = mission_factor(mission_row, profile, rng)
    material_target = as_float(mission_row["material_value_fe"]) * factor * stable_float(f"{seed}:material", 0.90, 1.10)
    intangible_target = as_float(mission_row["intangible_value_fe"]) * factor * stable_float(f"{seed}:intangible", 0.92, 1.08)

    material_payloads, material_fe, cargo_used, source_summary = generate_material_payload(
        sample_id,
        ship,
        mission_row,
        material_target,
        rng,
        values,
        categories,
    )
    if material_fe <= 0 and material_target > 0:
        service_payloads, material_fe, cargo_used, source_summary = generate_service_payload(sample_id, mission_row["primary_activity"], material_target, rng, values)
        material_payloads.extend(service_payloads)

    intangible_payloads = generate_intangible_rows(sample_id, mission_row, intangible_target)
    outcome, destroyed_chance, damaged_chance, damage_percent = outcome_for_sortie(mission_row, rng)

    extracted_total = material_fe + intangible_target * INTANGIBLE_EXTRACTION_MULTIPLIER
    destroyed_total = intangible_target
    extracted_liquid = (
        as_float(mission_row["freight_fe"]) * factor * 2.0
        + min(material_fe, material_target) * 0.35
    )
    if outcome == "destroyed":
        awarded_material = 0.0
        awarded_intangible = intangible_target
        awarded_total = destroyed_total
        awarded_ship_xp = as_int(as_int(mission_row["base_ship_xp"]) * factor)
        awarded_mastery = as_int(as_int(mission_row["base_mastery_points"]) * factor)
    else:
        awarded_material = material_fe
        awarded_intangible = intangible_target * INTANGIBLE_EXTRACTION_MULTIPLIER
        awarded_total = extracted_total
        awarded_ship_xp = as_int(as_int(mission_row["base_ship_xp"]) * factor * 2.0)
        awarded_mastery = as_int(as_int(mission_row["base_mastery_points"]) * factor * 2.0)

    sample = {
        "sample_id": sample_id,
        "seed": seed,
        "mission_profile": profile,
        "mission_archetype": mission_row["mission_archetype"],
        "primary_activity": mission_row["primary_activity"],
        "ship_id": mission_row["ship_id"],
        "local_name_ru": mission_row["local_name_ru"],
        "rank": mission_row["rank"],
        "faction_id": mission_row["faction_id"],
        "ship_class_id": mission_row["ship_class_id"],
        "mission_success_factor": mission_row["mission_success_factor"],
        "outcome": outcome,
        "destroyed_chance": f"{destroyed_chance:.4f}",
        "damaged_chance": f"{damaged_chance:.4f}",
        "damage_percent": f"{damage_percent:.1f}",
        "expected_extracted_total_fe": mission_row["extracted_total_value_fe"],
        "expected_material_fe": mission_row["material_value_fe"],
        "expected_intangible_fe": mission_row["intangible_value_fe"],
        "generated_material_fe": format_fe(material_fe),
        "generated_intangible_fe": format_fe(intangible_target),
        "generated_extracted_total_fe": format_fe(extracted_total),
        "generated_destroyed_total_fe": format_fe(destroyed_total),
        "awarded_material_fe": format_fe(awarded_material),
        "awarded_intangible_fe": format_fe(awarded_intangible),
        "awarded_total_fe": format_fe(awarded_total),
        "awarded_ship_xp": awarded_ship_xp,
        "awarded_mastery_points": awarded_mastery,
        "cargo_capacity_tons": ship.get("cargo_capacity_tons", "0"),
        "cargo_used_tons": f"{cargo_used:.3f}",
        "source_summary": source_summary,
        "payload_count": len(material_payloads),
    }
    all_payloads = material_payloads + intangible_payloads
    sample["sample_hash"] = sample_hash(sample, all_payloads)
    return sample, all_payloads


def select_mission_rows(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    by_profile = {profile: [row for row in rows if row["mission_profile"] == profile] for profile in ("quick", "normal", "danger", "elite")}
    activities = ("combat", "mining", "gas", "hunting", "salvage", "relic", "courier", "repair")
    selected: list[dict[str, str]] = []
    for profile, candidates in by_profile.items():
        candidates = sorted(candidates, key=lambda row: (int(row["rank"]), row["primary_activity"], row["ship_id"]))
        for index in range(SAMPLE_COUNT // 4):
            rank = 2 + index % 9
            activity = activities[index % len(activities)]
            pool = [row for row in candidates if int(row["rank"]) == rank and row["primary_activity"] == activity]
            if not pool:
                pool = [row for row in candidates if int(row["rank"]) == rank]
            if not pool:
                pool = candidates
            rng = random.Random(stable_seed(f"select:{profile}:{index}:{rank}:{activity}"))
            selected.append(pool[rng.randrange(len(pool))])
    return selected[:SAMPLE_COUNT]


def validate_sample(sample: dict[str, Any], repeat: dict[str, Any]) -> tuple[bool, list[str]]:
    errors: list[str] = []
    profile = sample["mission_profile"]
    expected = as_float(sample["expected_extracted_total_fe"])
    generated = as_float(sample["generated_extracted_total_fe"])
    ratio = generated / expected if expected > 0 else 0.0
    sample["expected_ratio"] = f"{ratio:.4f}"
    tolerance = PROFILE_TOLERANCE[profile]
    if not (1.0 - tolerance <= ratio <= 1.0 + tolerance):
        errors.append(f"fe_ratio_out_of_range:{ratio:.3f}")
    cargo_used = as_float(sample["cargo_used_tons"])
    cargo_capacity = as_float(sample["cargo_capacity_tons"])
    if cargo_used > cargo_capacity * 1.03 + 0.01:
        errors.append(f"cargo_over_capacity:{cargo_used:.2f}>{cargo_capacity:.2f}")
    if sample["sample_hash"] != repeat["sample_hash"]:
        errors.append("not_reproducible")

    generated_material = as_float(sample["generated_material_fe"])
    generated_intangible = as_float(sample["generated_intangible_fe"])
    extracted_total = as_float(sample["generated_extracted_total_fe"])
    destroyed_total = as_float(sample["generated_destroyed_total_fe"])
    if abs(extracted_total - (generated_material + generated_intangible * 2.0)) > 0.08:
        errors.append("extracted_formula_mismatch")
    if abs(destroyed_total - generated_intangible) > 0.08:
        errors.append("destroyed_formula_mismatch")
    awarded_material = as_float(sample["awarded_material_fe"])
    awarded_total = as_float(sample["awarded_total_fe"])
    if sample["outcome"] == "destroyed":
        if awarded_material != 0:
            errors.append("destroyed_kept_material")
        if abs(awarded_total - generated_intangible) > 0.08:
            errors.append("destroyed_award_mismatch")
    else:
        if abs(awarded_material - generated_material) > 0.08:
            errors.append("extracted_material_mismatch")
        if abs(awarded_total - extracted_total) > 0.08:
            errors.append("extracted_award_mismatch")
    sample["validation_status"] = "pass" if not errors else "fail"
    sample["validation_errors"] = ";".join(errors)
    return not errors, errors


def summarize_samples(samples: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for sample in samples:
        groups[("all", "all")].append(sample)
        groups[(sample["mission_profile"], "all")].append(sample)
        groups[(sample["mission_profile"], sample["primary_activity"])].append(sample)
    rows: list[dict[str, Any]] = []
    for (profile, activity), group in sorted(groups.items()):
        ratios = [as_float(sample["expected_ratio"]) for sample in group]
        awards = [as_float(sample["awarded_total_fe"]) for sample in group]
        extracted = [as_float(sample["generated_extracted_total_fe"]) for sample in group]
        rows.append(
            {
                "mission_profile": profile,
                "primary_activity": activity,
                "sample_count": len(group),
                "pass_count": sum(1 for sample in group if sample["validation_status"] == "pass"),
                "destroyed_count": sum(1 for sample in group if sample["outcome"] == "destroyed"),
                "damaged_count": sum(1 for sample in group if sample["outcome"] == "extracted_damaged"),
                "avg_expected_ratio": f"{statistics.mean(ratios):.4f}",
                "min_expected_ratio": f"{min(ratios):.4f}",
                "max_expected_ratio": f"{max(ratios):.4f}",
                "avg_generated_extracted_fe": format_fe(statistics.mean(extracted)),
                "avg_awarded_total_fe": format_fe(statistics.mean(awards)),
            }
        )
    return rows


def main() -> None:
    mission_rows = read_csv(CONFIG_DIR / "mission_profile_ship_profit.csv")
    ships = {row["id_ship"]: row for row in read_csv(SHIP_TREE_PATH)}
    values = item_values_by_id()
    categories = item_categories_by_id()
    selected_rows = select_mission_rows(mission_rows)

    samples: list[dict[str, Any]] = []
    payloads: list[dict[str, Any]] = []
    validation_rows: list[dict[str, Any]] = []
    failures: list[str] = []

    for index, mission_row in enumerate(selected_rows, start=1):
        sample_id = f"S{index:03d}"
        seed = f"sortie-v1:{sample_id}:{mission_row['ship_id']}:{mission_row['mission_profile']}:{mission_row['mission_archetype']}"
        ship = ships[mission_row["ship_id"]]
        sample, sample_payloads = generate_sortie(sample_id, mission_row, ship, values, categories, seed)
        repeat, _ = generate_sortie(sample_id, mission_row, ship, values, categories, seed)
        ok, errors = validate_sample(sample, repeat)
        if not ok:
            failures.append(f"{sample_id}:{mission_row['ship_id']}:{','.join(errors)}")
        samples.append(sample)
        payloads.extend(sample_payloads)
        validation_rows.append(
            {
                "sample_id": sample_id,
                "ship_id": sample["ship_id"],
                "mission_profile": sample["mission_profile"],
                "primary_activity": sample["primary_activity"],
                "outcome": sample["outcome"],
                "expected_ratio": sample["expected_ratio"],
                "cargo_used_tons": sample["cargo_used_tons"],
                "cargo_capacity_tons": sample["cargo_capacity_tons"],
                "sample_hash": sample["sample_hash"],
                "validation_status": sample["validation_status"],
                "validation_errors": sample["validation_errors"],
            }
        )

    sample_fieldnames = [
        "sample_id",
        "seed",
        "mission_profile",
        "mission_archetype",
        "primary_activity",
        "ship_id",
        "local_name_ru",
        "rank",
        "faction_id",
        "ship_class_id",
        "mission_success_factor",
        "outcome",
        "destroyed_chance",
        "damaged_chance",
        "damage_percent",
        "expected_extracted_total_fe",
        "expected_material_fe",
        "expected_intangible_fe",
        "generated_material_fe",
        "generated_intangible_fe",
        "generated_extracted_total_fe",
        "generated_destroyed_total_fe",
        "awarded_material_fe",
        "awarded_intangible_fe",
        "awarded_total_fe",
        "awarded_ship_xp",
        "awarded_mastery_points",
        "cargo_capacity_tons",
        "cargo_used_tons",
        "source_summary",
        "payload_count",
        "sample_hash",
        "expected_ratio",
        "validation_status",
        "validation_errors",
    ]
    payload_fieldnames = ["sample_id", "layer", "source_id", "item_id", "amount", "unit", "value_fe", "cargo_tons"]
    validation_fieldnames = [
        "sample_id",
        "ship_id",
        "mission_profile",
        "primary_activity",
        "outcome",
        "expected_ratio",
        "cargo_used_tons",
        "cargo_capacity_tons",
        "sample_hash",
        "validation_status",
        "validation_errors",
    ]
    summary_rows = summarize_samples(samples)
    summary_fieldnames = [
        "mission_profile",
        "primary_activity",
        "sample_count",
        "pass_count",
        "destroyed_count",
        "damaged_count",
        "avg_expected_ratio",
        "min_expected_ratio",
        "max_expected_ratio",
        "avg_generated_extracted_fe",
        "avg_awarded_total_fe",
    ]

    write_csv(CONFIG_DIR / "sortie_generator_samples.csv", samples, sample_fieldnames)
    write_csv(CONFIG_DIR / "sortie_generator_payloads.csv", payloads, payload_fieldnames)
    write_csv(CONFIG_DIR / "sortie_generator_validation.csv", validation_rows, validation_fieldnames)
    write_csv(CONFIG_DIR / "sortie_generator_validation_summary.csv", summary_rows, summary_fieldnames)

    print(f"Generated {len(samples)} seeded sorties with {len(payloads)} payload rows.")
    print(f"Validation: {len(samples) - len(failures)} passed, {len(failures)} failed.")
    for row in summary_rows:
        if row["primary_activity"] == "all":
            print(
                "{profile}: samples={count}, pass={passed}, destroyed={destroyed}, damaged={damaged}, ratio_avg={ratio}, awarded_avg={awarded}".format(
                    profile=row["mission_profile"],
                    count=row["sample_count"],
                    passed=row["pass_count"],
                    destroyed=row["destroyed_count"],
                    damaged=row["damaged_count"],
                    ratio=row["avg_expected_ratio"],
                    awarded=row["avg_awarded_total_fe"],
                )
            )
    if failures:
        print("Failures:")
        for failure in failures[:20]:
            print(f" - {failure}")
        raise SystemExit(1)


if __name__ == "__main__":
    main()
