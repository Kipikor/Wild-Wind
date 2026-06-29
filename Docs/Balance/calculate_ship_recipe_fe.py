from __future__ import annotations

import csv
from pathlib import Path

from calculate_ship_crafting_fe import calculate


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


if __name__ == "__main__":
    calculate()
    print("R10 FE totals from full ship crafting recipes:")
    for row in read_csv(CONFIG_DIR / "ship_r10_fe_extremes.csv"):
        if row["economy_level"] in ("0", "5"):
            label = "min upgrades" if row["economy_level"] == "0" else "max upgrades"
            print(
                f"- {row['ship_class_ru']} ({label}): "
                f"{row['avg_total_mfe']}M FE avg, "
                f"{float(row['min_total_fe']) / 1_000_000:.3f}M min, "
                f"{float(row['max_total_fe']) / 1_000_000:.3f}M max"
            )
