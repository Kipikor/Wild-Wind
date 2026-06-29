from __future__ import annotations

import csv
import statistics
import sys
from collections import defaultdict
from functools import lru_cache
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"


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


def format_fe(value: float) -> str:
    return f"{value:.2f}"


class FeCalculator:
    def __init__(self) -> None:
        self.leaf_values = {
            row["item_id"]: as_float(row["value_fe_per_unit"])
            for row in read_csv(CONFIG_DIR / "resource_value_reference.csv")
        }
        self.recipes_by_output = {
            row["output_item_id"]: row
            for row in read_csv(CONFIG_DIR / "item_production_recipes.csv")
        }
        self.item_cost_rows = {
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
                raise ValueError(f"Missing FE value for leaf item {item_id}")
            return self.leaf_values[item_id]

        self.visiting.add(key)
        cost_row = self.item_cost_rows.get((recipe["recipe_id"], economy_level))
        if cost_row is None:
            raise ValueError(f"Missing item cost row for {recipe['recipe_id']} L{economy_level}")
        total = 0.0
        for input_id, amount in zip(split_ids(cost_row["input_ids"]), split_amounts(cost_row["input_amounts"])):
            total += self.item_fe(input_id, economy_level) * amount
        self.visiting.remove(key)
        return total / as_float(recipe["output_amount"])

    def license_fe(self, item_id: str) -> float:
        if not item_id:
            return 0.0
        if item_id not in self.leaf_values:
            raise ValueError(f"Missing FE value for license {item_id}")
        return self.leaf_values[item_id]


def summarize(values: list[float]) -> tuple[float, float, float, float]:
    return min(values), statistics.mean(values), statistics.median(values), max(values)


def calculate() -> list[dict[str, object]]:
    fe = FeCalculator()
    recipes = {row["recipe_id"]: row for row in read_csv(CONFIG_DIR / "ship_crafting_recipes.csv")}
    cost_rows = read_csv(CONFIG_DIR / "ship_recipe_cost_levels.csv")

    summary_rows: list[dict[str, object]] = []
    for row in cost_rows:
        recipe = recipes[row["recipe_id"]]
        economy_level = int(row["economy_level"])
        discountable_fe = 0.0
        for input_id, amount in zip(split_ids(row["input_ids"]), split_amounts(row["input_amounts"])):
            discountable_fe += fe.item_fe(input_id, economy_level) * amount

        license_fe = fe.license_fe(row["required_license_item_id"]) * int(row["required_license_amount"])
        total_fe = discountable_fe + license_fe
        summary_rows.append(
            {
                "recipe_id": row["recipe_id"],
                "ship_id": row["ship_id"],
                "local_name_ru": recipe["local_name_ru"],
                "rank": recipe["rank"],
                "faction_id": recipe["faction_id"],
                "faction_name_ru": recipe["faction_name_ru"],
                "ship_class_id": recipe["ship_class_id"],
                "role_id": recipe["role_id"],
                "branch_id": recipe["branch_id"],
                "craft_layer": recipe["craft_layer"],
                "economy_level": economy_level,
                "input_multiplier": row["input_multiplier"],
                "discountable_fe": format_fe(discountable_fe),
                "license_fe": format_fe(license_fe),
                "total_fe": format_fe(total_fe),
                "total_mfe": f"{total_fe / 1_000_000:.3f}",
            }
        )

    write_csv(
        CONFIG_DIR / "ship_recipe_fe_summary.csv",
        summary_rows,
        [
            "recipe_id",
            "ship_id",
            "local_name_ru",
            "rank",
            "faction_id",
            "faction_name_ru",
            "ship_class_id",
            "role_id",
            "branch_id",
            "craft_layer",
            "economy_level",
            "input_multiplier",
            "discountable_fe",
            "license_fe",
            "total_fe",
            "total_mfe",
        ],
    )

    rank_groups: defaultdict[tuple[str, str, int], list[dict[str, object]]] = defaultdict(list)
    faction_groups: defaultdict[tuple[str, str, int], list[dict[str, object]]] = defaultdict(list)
    for row in summary_rows:
        rank_groups[(str(row["rank"]), str(row["ship_class_id"]), int(row["economy_level"]))].append(row)
        faction_groups[(str(row["rank"]), str(row["faction_id"]), int(row["economy_level"]))].append(row)

    rank_summary_rows: list[dict[str, object]] = []
    for (rank, ship_class, economy_level), rows in sorted(rank_groups.items(), key=lambda item: (int(item[0][0]), item[0][1], item[0][2])):
        totals = [as_float(row["total_fe"]) for row in rows]
        discountables = [as_float(row["discountable_fe"]) for row in rows]
        licenses = [as_float(row["license_fe"]) for row in rows]
        min_total, avg_total, median_total, max_total = summarize(totals)
        rank_summary_rows.append(
            {
                "rank": rank,
                "ship_class_id": ship_class,
                "economy_level": economy_level,
                "ship_count": len(rows),
                "min_total_fe": format_fe(min_total),
                "avg_total_fe": format_fe(avg_total),
                "median_total_fe": format_fe(median_total),
                "max_total_fe": format_fe(max_total),
                "avg_discountable_fe": format_fe(statistics.mean(discountables)),
                "avg_license_fe": format_fe(statistics.mean(licenses)),
            }
        )

    write_csv(
        CONFIG_DIR / "ship_rank_fe_summary.csv",
        rank_summary_rows,
        [
            "rank",
            "ship_class_id",
            "economy_level",
            "ship_count",
            "min_total_fe",
            "avg_total_fe",
            "median_total_fe",
            "max_total_fe",
            "avg_discountable_fe",
            "avg_license_fe",
        ],
    )

    faction_summary_rows: list[dict[str, object]] = []
    for (rank, faction_id, economy_level), rows in sorted(faction_groups.items(), key=lambda item: (int(item[0][0]), item[0][1], item[0][2])):
        totals = [as_float(row["total_fe"]) for row in rows]
        min_total, avg_total, median_total, max_total = summarize(totals)
        faction_summary_rows.append(
            {
                "rank": rank,
                "faction_id": faction_id,
                "economy_level": economy_level,
                "ship_count": len(rows),
                "min_total_fe": format_fe(min_total),
                "avg_total_fe": format_fe(avg_total),
                "median_total_fe": format_fe(median_total),
                "max_total_fe": format_fe(max_total),
            }
        )

    write_csv(
        CONFIG_DIR / "ship_faction_fe_summary.csv",
        faction_summary_rows,
        [
            "rank",
            "faction_id",
            "economy_level",
            "ship_count",
            "min_total_fe",
            "avg_total_fe",
            "median_total_fe",
            "max_total_fe",
        ],
    )

    r10_groups: defaultdict[tuple[str, int], list[dict[str, object]]] = defaultdict(list)
    for row in summary_rows:
        if str(row["rank"]) == "10":
            r10_groups[(str(row["ship_class_id"]), int(row["economy_level"]))].append(row)

    class_ru = {
        "frigate": "Фрегат",
        "cruiser": "Крейсер",
        "battleship": "Линкор",
    }
    r10_rows: list[dict[str, object]] = []
    for (ship_class_id, economy_level), rows in sorted(r10_groups.items(), key=lambda item: (item[0][0], item[0][1])):
        totals = [as_float(row["total_fe"]) for row in rows]
        discountables = [as_float(row["discountable_fe"]) for row in rows]
        licenses = [as_float(row["license_fe"]) for row in rows]
        min_total, avg_total, median_total, max_total = summarize(totals)
        r10_rows.append(
            {
                "profile_id": f"r10_{ship_class_id}",
                "ship_class_ru": class_ru.get(ship_class_id, ship_class_id),
                "ship_class_id": ship_class_id,
                "economy_level": economy_level,
                "ship_count": len(rows),
                "input_multiplier": rows[0]["input_multiplier"],
                "avg_discountable_fe": format_fe(statistics.mean(discountables)),
                "avg_license_fe": format_fe(statistics.mean(licenses)),
                "avg_total_fe": format_fe(avg_total),
                "median_total_fe": format_fe(median_total),
                "min_total_fe": format_fe(min_total),
                "max_total_fe": format_fe(max_total),
                "avg_total_mfe": f"{avg_total / 1_000_000:.3f}",
            }
        )

    write_csv(
        CONFIG_DIR / "ship_r10_fe_extremes.csv",
        r10_rows,
        [
            "profile_id",
            "ship_class_ru",
            "ship_class_id",
            "economy_level",
            "ship_count",
            "input_multiplier",
            "avg_discountable_fe",
            "avg_license_fe",
            "avg_total_fe",
            "median_total_fe",
            "min_total_fe",
            "max_total_fe",
            "avg_total_mfe",
        ],
    )
    return summary_rows


if __name__ == "__main__":
    try:
        rows = calculate()
    except Exception as exc:
        print(f"Ship crafting FE calculation failed: {exc}")
        sys.exit(1)

    print(f"Calculated FE for {len(rows)} ship recipe level rows.")
    print("Average full-efficiency totals by rank:")
    rank_values: defaultdict[int, list[float]] = defaultdict(list)
    for row in rows:
        if int(row["economy_level"]) == 5:
            rank_values[int(row["rank"])].append(as_float(row["total_fe"]))
    for rank in sorted(rank_values):
        print(f"- R{rank}: {statistics.mean(rank_values[rank]) / 1_000_000:.3f}M FE")
