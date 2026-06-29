from __future__ import annotations

import csv
import math
import statistics
import sys
from collections import defaultdict
from functools import lru_cache
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"

SCENARIOS = [
    {
        "scenario_id": "max_untrained_chain",
        "scenario_ru": "Максимум: все рецепты новые, все здания первого уровня",
        "economy_level": 0,
        "time_level": 0,
        "building_level": 1,
        "item_time_column": "time_min_building1",
        "ship_time_column": "time_min_yard1",
    },
    {
        "scenario_id": "min_mastered_chain",
        "scenario_ru": "Минимум: все рецепты выучены, все здания тридцатого уровня",
        "economy_level": 5,
        "time_level": 5,
        "building_level": 30,
        "item_time_column": "time_min_building30",
        "ship_time_column": "time_min_yard30",
    },
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


def format_time(value: float) -> str:
    return f"{value:.2f}"


class ProductionTimeCalculator:
    def __init__(self) -> None:
        self.item_recipes = read_csv(CONFIG_DIR / "item_production_recipes.csv")
        self.recipes_by_output = {row["output_item_id"]: row for row in self.item_recipes}
        self.cost_rows = {
            (row["recipe_id"], int(row["economy_level"])): row
            for row in read_csv(CONFIG_DIR / "item_recipe_cost_levels.csv")
        }
        self.time_rows = {
            (row["recipe_id"], int(row["time_level"])): row
            for row in read_csv(CONFIG_DIR / "item_recipe_time_levels.csv")
        }
        self.ship_recipes = {
            row["recipe_id"]: row
            for row in read_csv(CONFIG_DIR / "ship_crafting_recipes.csv")
        }
        self.ship_cost_rows = {
            (row["recipe_id"], int(row["economy_level"])): row
            for row in read_csv(CONFIG_DIR / "ship_recipe_cost_levels.csv")
        }
        self.ship_time_rows = {
            (row["recipe_id"], int(row["time_level"])): row
            for row in read_csv(CONFIG_DIR / "ship_recipe_time_levels.csv")
        }

    @lru_cache(maxsize=None)
    def recipe_depth(self, item_id: str) -> int:
        recipe = self.recipes_by_output.get(item_id)
        if recipe is None:
            return 0
        input_depth = 0
        for input_id in split_ids(recipe["input_ids"]):
            input_depth = max(input_depth, self.recipe_depth(input_id))
        return input_depth + 1

    def expand_item_demands(self, initial_demands: dict[str, int], economy_level: int) -> tuple[dict[str, int], dict[str, int]]:
        demands: defaultdict[str, int] = defaultdict(int)
        for item_id, amount in initial_demands.items():
            demands[item_id] += amount

        production_batches: dict[str, int] = {}
        max_depth = max((self.recipe_depth(item_id) for item_id in demands), default=0)
        for depth in range(max_depth, 0, -1):
            craftable_at_depth = sorted(
                item_id
                for item_id, amount in list(demands.items())
                if amount > 0 and self.recipe_depth(item_id) == depth and item_id in self.recipes_by_output
            )
            for item_id in craftable_at_depth:
                required_amount = demands[item_id]
                recipe = self.recipes_by_output[item_id]
                output_amount = int(recipe["output_amount"])
                batches = math.ceil(required_amount / output_amount)
                production_batches[item_id] = production_batches.get(item_id, 0) + batches
                cost_row = self.cost_rows[(recipe["recipe_id"], economy_level)]
                for input_id, amount in zip(split_ids(cost_row["input_ids"]), split_amounts(cost_row["input_amounts"])):
                    demands[input_id] += amount * batches

        return dict(demands), production_batches

    def calculate_ship(self, recipe_id: str, scenario: dict[str, object]) -> dict[str, float | dict[str, float] | dict[str, int]]:
        ship_recipe = self.ship_recipes[recipe_id]
        economy_level = int(scenario["economy_level"])
        time_level = int(scenario["time_level"])
        ship_cost = self.ship_cost_rows[(recipe_id, economy_level)]
        ship_time = self.ship_time_rows[(recipe_id, time_level)]

        initial_demands = {
            item_id: amount
            for item_id, amount in zip(split_ids(ship_cost["input_ids"]), split_amounts(ship_cost["input_amounts"]))
        }
        _, production_batches = self.expand_item_demands(initial_demands, economy_level)

        work_by_building: defaultdict[str, float] = defaultdict(float)
        work_by_layer: defaultdict[str, float] = defaultdict(float)
        for item_id, batches in production_batches.items():
            recipe = self.recipes_by_output[item_id]
            time_row = self.time_rows[(recipe["recipe_id"], time_level)]
            batch_time = as_float(time_row[str(scenario["item_time_column"])])
            total_time = batch_time * batches
            building_id = recipe["building_id"]
            layer = recipe["layer"]
            work_by_building[building_id] += total_time
            work_by_layer[layer] += total_time

        final_assembly_time = as_float(ship_time[str(scenario["ship_time_column"])])
        producer_building_id = ship_recipe["producer_building_id"]
        work_by_building[producer_building_id] += final_assembly_time
        work_by_layer["ship_final_assembly"] += final_assembly_time

        material_parallel = max(
            work_by_building.get("metallurgy", 0.0),
            work_by_building.get("chemical_reactor", 0.0),
        )
        part_parallel = max(
            work_by_building.get("construction", 0.0),
            work_by_building.get("mechanical", 0.0),
            work_by_building.get("instrumentation", 0.0),
        )
        block_parallel = work_by_building.get("assembly", 0.0)
        staged_parallel = material_parallel + part_parallel + block_parallel + final_assembly_time

        item_work = sum(work_by_building.values()) - final_assembly_time
        total_work = item_work + final_assembly_time
        return {
            "final_assembly_min": final_assembly_time,
            "item_work_min": item_work,
            "total_work_min": total_work,
            "staged_parallel_min": staged_parallel,
            "work_by_building": dict(work_by_building),
            "work_by_layer": dict(work_by_layer),
            "production_batches": production_batches,
        }


def summarize(values: list[float]) -> tuple[float, float, float, float]:
    return min(values), statistics.mean(values), statistics.median(values), max(values)


def calculate() -> list[dict[str, object]]:
    calculator = ProductionTimeCalculator()
    rows: list[dict[str, object]] = []

    for recipe_id, ship_recipe in calculator.ship_recipes.items():
        for scenario in SCENARIOS:
            result = calculator.calculate_ship(recipe_id, scenario)
            rows.append(
                {
                    "recipe_id": recipe_id,
                    "ship_id": ship_recipe["ship_id"],
                    "local_name_ru": ship_recipe["local_name_ru"],
                    "rank": ship_recipe["rank"],
                    "faction_id": ship_recipe["faction_id"],
                    "ship_class_id": ship_recipe["ship_class_id"],
                    "role_id": ship_recipe["role_id"],
                    "branch_id": ship_recipe["branch_id"],
                    "producer_building_id": ship_recipe["producer_building_id"],
                    "scenario_id": scenario["scenario_id"],
                    "scenario_ru": scenario["scenario_ru"],
                    "economy_level": scenario["economy_level"],
                    "time_level": scenario["time_level"],
                    "building_level": scenario["building_level"],
                    "final_assembly_min": format_time(float(result["final_assembly_min"])),
                    "item_work_min": format_time(float(result["item_work_min"])),
                    "total_work_min": format_time(float(result["total_work_min"])),
                    "staged_parallel_min": format_time(float(result["staged_parallel_min"])),
                    "total_work_hours": format_time(float(result["total_work_min"]) / 60),
                    "staged_parallel_hours": format_time(float(result["staged_parallel_min"]) / 60),
                    "metallurgy_min": format_time(result["work_by_building"].get("metallurgy", 0.0)),
                    "chemical_reactor_min": format_time(result["work_by_building"].get("chemical_reactor", 0.0)),
                    "construction_min": format_time(result["work_by_building"].get("construction", 0.0)),
                    "mechanical_min": format_time(result["work_by_building"].get("mechanical", 0.0)),
                    "instrumentation_min": format_time(result["work_by_building"].get("instrumentation", 0.0)),
                    "assembly_min": format_time(result["work_by_building"].get("assembly", 0.0)),
                }
            )

    write_csv(
        CONFIG_DIR / "ship_full_chain_time.csv",
        rows,
        [
            "recipe_id",
            "ship_id",
            "local_name_ru",
            "rank",
            "faction_id",
            "ship_class_id",
            "role_id",
            "branch_id",
            "producer_building_id",
            "scenario_id",
            "scenario_ru",
            "economy_level",
            "time_level",
            "building_level",
            "final_assembly_min",
            "item_work_min",
            "total_work_min",
            "staged_parallel_min",
            "total_work_hours",
            "staged_parallel_hours",
            "metallurgy_min",
            "chemical_reactor_min",
            "construction_min",
            "mechanical_min",
            "instrumentation_min",
            "assembly_min",
        ],
    )

    grouped: defaultdict[tuple[str, str, str, str], list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        grouped[(str(row["rank"]), str(row["ship_class_id"]), str(row["producer_building_id"]), str(row["scenario_id"]))].append(row)

    summary_rows: list[dict[str, object]] = []
    for (rank, ship_class_id, producer_building_id, scenario_id), group_rows in sorted(
        grouped.items(), key=lambda item: (int(item[0][0]), item[0][1], item[0][3])
    ):
        final_values = [as_float(row["final_assembly_min"]) for row in group_rows]
        total_work_values = [as_float(row["total_work_min"]) for row in group_rows]
        staged_values = [as_float(row["staged_parallel_min"]) for row in group_rows]
        _, avg_final, _, _ = summarize(final_values)
        min_work, avg_work, median_work, max_work = summarize(total_work_values)
        min_staged, avg_staged, median_staged, max_staged = summarize(staged_values)
        scenario = next(s for s in SCENARIOS if s["scenario_id"] == scenario_id)
        summary_rows.append(
            {
                "rank": rank,
                "ship_class_id": ship_class_id,
                "producer_building_id": producer_building_id,
                "scenario_id": scenario_id,
                "scenario_ru": scenario["scenario_ru"],
                "ship_count": len(group_rows),
                "avg_final_assembly_min": format_time(avg_final),
                "min_total_work_min": format_time(min_work),
                "avg_total_work_min": format_time(avg_work),
                "median_total_work_min": format_time(median_work),
                "max_total_work_min": format_time(max_work),
                "min_staged_parallel_min": format_time(min_staged),
                "avg_staged_parallel_min": format_time(avg_staged),
                "median_staged_parallel_min": format_time(median_staged),
                "max_staged_parallel_min": format_time(max_staged),
                "avg_total_work_hours": format_time(avg_work / 60),
                "avg_staged_parallel_hours": format_time(avg_staged / 60),
            }
        )

    write_csv(
        CONFIG_DIR / "ship_full_chain_time_summary.csv",
        summary_rows,
        [
            "rank",
            "ship_class_id",
            "producer_building_id",
            "scenario_id",
            "scenario_ru",
            "ship_count",
            "avg_final_assembly_min",
            "min_total_work_min",
            "avg_total_work_min",
            "median_total_work_min",
            "max_total_work_min",
            "min_staged_parallel_min",
            "avg_staged_parallel_min",
            "median_staged_parallel_min",
            "max_staged_parallel_min",
            "avg_total_work_hours",
            "avg_staged_parallel_hours",
        ],
    )
    return rows


if __name__ == "__main__":
    try:
        result_rows = calculate()
    except Exception as exc:
        print(f"Ship crafting time calculation failed: {exc}")
        sys.exit(1)

    print(f"Calculated full-chain time for {len(result_rows)} ship/scenario rows.")
    print("Average staged full-chain time by rank at max mastery:")
    rank_values: defaultdict[int, list[float]] = defaultdict(list)
    for row in result_rows:
        if row["scenario_id"] == "min_mastered_chain":
            rank_values[int(row["rank"])].append(as_float(row["staged_parallel_min"]))
    for rank in sorted(rank_values):
        print(f"- R{rank}: {statistics.mean(rank_values[rank]) / 60:.2f} h")
