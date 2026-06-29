from __future__ import annotations

import csv
import math
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CONFIG_DIR = ROOT / "Docs" / "Balance" / "PortConfigs"

TIME_CURVE = [
    (0, 2.50, "Новый рецепт: много ручной подгонки, простаивания и брака."),
    (1, 2.20, "Первая технологическая карта убирает грубые задержки."),
    (2, 1.90, "Предтеченские схемы стабилизируют повторяемость операции."),
    (3, 1.60, "Оснастка и контроль операций уже работают серийно."),
    (4, 1.30, "Почти отлаженная линия с малым количеством ручной работы."),
    (5, 1.00, "Полностью выученный рецепт, нормальное расчетное время."),
]

EFFICIENCY_CURVE = [
    (0, 2.00, "Новый рецепт: двойной расход из-за брака, лишней подгонки и пережога."),
    (1, 1.80, "Убраны самые грубые потери сырья."),
    (2, 1.60, "Снижены отходы и повторная обработка."),
    (3, 1.40, "Нормальная карта раскроя и меньше испорченных узлов."),
    (4, 1.20, "Почти серийный расход с небольшим запасом на брак."),
    (5, 1.00, "Эталонный расход рецепта."),
]


def recipe(recipe_id: str, output_id: str, name: str, layer: str, building: str,
           output_amount: int, base_time_min: float, inputs: list[tuple[str, int]],
           notes: str) -> dict:
    return {
        "recipe_id": recipe_id,
        "output_item_id": output_id,
        "local_name_ru": name,
        "layer": layer,
        "building_id": building,
        "output_amount": output_amount,
        "base_time_min": base_time_min,
        "inputs": inputs,
        "notes_ru": notes,
    }


RECIPES = [
    recipe("make_steel", "steel", "Сталь", "prepared_material", "metallurgy", 10, 4,
           [("iron", 16), ("charcoal", 8), ("calcite", 3)],
           "Базовая конструкционная партия: железо, уголь и минеральная присадка."),
    recipe("make_bronze", "bronze", "Бронза", "prepared_material", "metallurgy", 10, 5,
           [("copper", 10), ("tin", 4), ("bone_grit", 4)],
           "Износостойкий металл для втулок, петель и спокойной тяжелой механики."),
    recipe("make_brass", "brass", "Латунь", "prepared_material", "metallurgy", 10, 5,
           [("copper", 10), ("zinc", 5), ("sylvine", 3)],
           "Приборная и арматурная партия для клапанов, фитингов и шлюзовой механики."),
    recipe("make_elektron", "elektron", "Электрон", "prepared_material", "metallurgy", 10, 6,
           [("magnesium", 12), ("zinc", 3), ("aerosil", 8)],
           "Легкий магниевый материал для быстрых корпусов, кожухов и грузовых узлов."),
    recipe("make_bulat", "bulat", "Булат", "prepared_material", "metallurgy", 10, 12,
           [("iron", 20), ("monazite", 3), ("acid", 5)],
           "Дорогая прочная сталь для брони, валов, зубьев и силовых деталей."),
    recipe("make_melchior", "melchior", "Мельхиор", "prepared_material", "metallurgy", 10, 10,
           [("copper", 12), ("nickel", 5), ("ionide", 3)],
           "Приборный проводящий материал для сенсоров, куполов и стабильных контактов."),
    recipe("make_invar", "invar", "Инвар", "prepared_material", "metallurgy", 10, 14,
           [("iron", 14), ("nickel", 6), ("nulgas", 2)],
           "Стабильный металл для точных рам, гироскопов и дальномерных узлов."),
    recipe("make_orkit", "orkit", "Оркит", "prepared_material", "metallurgy", 10, 16,
           [("claudium", 8), ("fulgur", 4), ("bone_grit", 6)],
           "Летный силовой материал для тяги, подъемных контуров и дорогих узлов."),
    recipe("make_resin", "resin", "Смола", "prepared_material", "chemical_reactor", 10, 5,
           [("vespar", 6), ("leviathan_fat", 4), ("acid", 2)],
           "Герметик и связующее для ранних полимеров."),
    recipe("make_rubber", "rubber", "Каучук", "prepared_material", "chemical_reactor", 10, 6,
           [("vespar", 5), ("leviathan_hide", 2), ("sylvine", 3)],
           "Эластичная партия для уплотнений, шлангов, подвесов и мягкой защиты."),
    recipe("make_bakelite", "bakelite", "Бакелит", "prepared_material", "chemical_reactor", 10, 6,
           [("aerosil", 8), ("calcite", 5), ("acid", 2)],
           "Жесткий изоляционный материал для панелей и приборных корпусов."),
    recipe("make_textolite", "textolite", "Текстолит", "prepared_material", "chemical_reactor", 10, 8,
           [("leviathan_hide", 3), ("quartz", 8), ("ionide", 2)],
           "Слоистый приборный материал для плат, рамок и точной изоляции."),
    recipe("make_fluoroplastic", "fluoroplastic", "Фторопласт", "prepared_material", "chemical_reactor", 10, 14,
           [("nulgas", 3), ("acid", 5), ("sylvine", 5)],
           "Химстойкий материал для агрессивных газов, клапанов и продвинутых уплотнений."),
    recipe("make_aramid", "aramid", "Арамид", "prepared_material", "chemical_reactor", 10, 12,
           [("leviathan_sinew", 4), ("fulgur", 3), ("calcite", 4)],
           "Прочное волокно для тяг, тросов, гарпунов и защитных оболочек."),
    recipe("make_siloxane", "siloxane", "Силоксан", "prepared_material", "chemical_reactor", 10, 8,
           [("quartz", 8), ("vespar", 4), ("acid", 3)],
           "Гибкий кремнийорганический материал для кожухов, покрытий и температурной защиты."),
    recipe("make_glass_ceramic", "glass_ceramic", "Стеклокерамика", "prepared_material", "chemical_reactor", 10, 10,
           [("quartz", 10), ("bone_grit", 6), ("fulgur", 2)],
           "Жаростойкая оптическая и изоляционная партия для окон, сопел и сенсоров."),

    recipe("make_beam", "beam", "Балка", "corpus_part", "construction", 1, 10,
           [("steel", 8), ("iron", 16)],
           "Несущая корпусная деталь: сталь и простая масса железа."),
    recipe("make_plating", "plating", "Обшивка", "corpus_part", "construction", 1, 12,
           [("steel", 6), ("resin", 4), ("magnesium", 5)],
           "Тонкая внешняя кожа корпуса с герметиком и легкой добавкой."),
    recipe("make_armor_plate", "armor_plate", "Бронелист", "corpus_part", "construction", 1, 24,
           [("steel", 12), ("bulat", 5), ("bone_grit", 8)],
           "Толстая защитная плита с дорогой твердой добавкой."),
    recipe("make_bulkhead", "bulkhead", "Переборка", "corpus_part", "construction", 1, 14,
           [("steel", 8), ("bakelite", 4), ("sylvine", 5)],
           "Внутренняя стенка, разделяющая отсеки и изолирующая повреждения."),
    recipe("make_deck_section", "deck_section", "Палубная секция", "corpus_part", "construction", 1, 16,
           [("steel", 10), ("bakelite", 3), ("quartz", 6)],
           "Рабочая верхняя секция под оборудование, башни и люки."),
    recipe("make_airlock", "airlock", "Шлюз", "corpus_part", "construction", 1, 18,
           [("steel", 8), ("brass", 4), ("rubber", 4)],
           "Герметичный переход со стальной коробкой и мягкими уплотнениями."),
    recipe("make_cowling", "cowling", "Кожух", "corpus_part", "construction", 1, 22,
           [("elektron", 5), ("siloxane", 4), ("glass_ceramic", 3)],
           "Легкая защитная оболочка горячих и выступающих узлов."),
    recipe("make_hatch", "hatch", "Люк", "corpus_part", "construction", 1, 14,
           [("steel", 6), ("bronze", 4), ("rubber", 3)],
           "Закрываемый доступ с петлями, крышкой и уплотнением."),

    recipe("make_engine", "engine", "Двигатель", "machine_instrument_part", "mechanical", 1, 18,
           [("orkit", 10), ("charcoal", 50), ("automaton_mainspring", 5), ("automaton_servo_core", 2)],
           "Тяговая машина: летный материал, топливо и восстановленные автоматоновые узлы."),
    recipe("make_actuator", "actuator", "Привод", "machine_instrument_part", "mechanical", 1, 16,
           [("aramid", 6), ("copper", 8), ("automaton_servo_joint", 4), ("automaton_coil", 5)],
           "Силовая тяга вокруг точного шарнира и управляющей катушки."),
    recipe("make_transmission", "transmission", "Передача", "machine_instrument_part", "mechanical", 1, 17,
           [("bulat", 6), ("automaton_calibration_gear", 5), ("automaton_mainspring", 4), ("leviathan_fat", 6)],
           "Зубья, валы, калибровка, возврат и густая смазка."),
    recipe("make_pump", "pump", "Насос", "machine_instrument_part", "mechanical", 1, 15,
           [("rubber", 5), ("acid", 6), ("automaton_brass_valve", 4), ("automaton_pressure_gauge", 3)],
           "Уплотнения, химстойкая обработка и готовая точная арматура давления."),
    recipe("make_compressor", "compressor", "Компрессор", "machine_instrument_part", "mechanical", 1, 20,
           [("fluoroplastic", 5), ("vespar", 10), ("automaton_brass_valve", 5), ("automaton_pressure_gauge", 4)],
           "Газовая машина со стойкими прокладками, клапанами и давлениемерами."),
    recipe("make_gyroscope", "gyroscope", "Гироскоп", "machine_instrument_part", "instrumentation", 1, 22,
           [("invar", 5), ("ionide", 6), ("automaton_gyroscope", 4), ("automaton_calibration_gear", 3)],
           "Стабильный металл, тонкий сигнал и точное автоматонное сердце узла."),
    recipe("make_sensor", "sensor", "Сенсор", "machine_instrument_part", "instrumentation", 1, 20,
           [("glass_ceramic", 4), ("ionide", 6), ("automaton_optic_lens", 4), ("automaton_relay", 5)],
           "Оптика, сигнал и точная коммутация для разведки и наведения."),
    recipe("make_calculator", "calculator", "Вычислитель", "machine_instrument_part", "instrumentation", 1, 24,
           [("textolite", 5), ("ionide", 8), ("automaton_logic_drum", 3), ("automaton_command_cylinder", 3), ("automaton_contact_comb", 5)],
           "Платы, сигнал, память, команды и контактная схема."),

    recipe("make_power_block", "power_block", "Силовой блок", "ship_block", "assembly", 1, 120,
           [("orkit", 20), ("engine", 4), ("transmission", 2), ("pump", 2), ("compressor", 2), ("beam", 8), ("bulkhead", 4), ("cowling", 3), ("claudium", 30), ("fulgur", 12), ("nulgas", 5), ("automaton_core", 3), ("automaton_servo_core", 3), ("automaton_coil", 8)],
           "Крупное питание корабля: тяга, газовая часть, корпусная защита и точное управление."),
    recipe("make_propulsion_block", "propulsion_block", "Ходовой блок", "ship_block", "assembly", 1, 110,
           [("engine", 4), ("transmission", 4), ("gyroscope", 2), ("actuator", 4), ("beam", 6), ("deck_section", 3), ("cowling", 4), ("invar", 8), ("aramid", 12), ("leviathan_fat", 10), ("magnesium", 20), ("ionide", 10), ("automaton_calibration_gear", 8)],
           "Большой узел движения: тяга, стабилизация, передачи, легкие материалы и смазка."),
    recipe("make_instrument_block", "instrument_block", "Приборный блок", "ship_block", "assembly", 1, 100,
           [("sensor", 5), ("calculator", 4), ("gyroscope", 3), ("cowling", 3), ("deck_section", 2), ("glass_ceramic", 12), ("textolite", 12), ("melchior", 10), ("invar", 6), ("ionide", 20), ("quartz", 25), ("automaton_optic_lens", 8), ("automaton_relay", 10), ("automaton_command_cylinder", 5), ("automaton_contact_comb", 10)],
           "Навигация, X-Ray, взлом, расчет огня и точные автоматонные детали."),
    recipe("make_weapon_block", "weapon_block", "Орудийный блок", "ship_block", "assembly", 1, 115,
           [("actuator", 4), ("calculator", 2), ("gyroscope", 2), ("armor_plate", 3), ("deck_section", 3), ("hatch", 4), ("bulat", 15), ("steel", 25), ("brass", 10), ("aramid", 10), ("weapon", 6), ("munition_bundle", 8), ("automaton_servo_joint", 8), ("automaton_calibration_gear", 8)],
           "Места башен, приводы, наведение, броня, боезапас и обслуживающие люки."),
    recipe("make_defense_block", "defense_block", "Защитный блок", "ship_block", "assembly", 1, 130,
           [("armor_plate", 6), ("bulkhead", 6), ("plating", 6), ("hatch", 4), ("pump", 3), ("sensor", 2), ("steel", 30), ("bulat", 12), ("bakelite", 10), ("rubber", 8), ("bone_grit", 20), ("acid", 8), ("leviathan_hide", 6), ("automaton_pressure_gauge", 5)],
           "Броня, переборки, аварийные насосы, датчики, изоляция и живучие материалы."),
    recipe("make_cargo_block", "cargo_block", "Грузовой блок", "ship_block", "assembly", 1, 90,
           [("bulkhead", 8), ("deck_section", 5), ("airlock", 3), ("hatch", 6), ("actuator", 4), ("pump", 2), ("plating", 8), ("elektron", 10), ("rubber", 10), ("brass", 8), ("magnesium", 20), ("leviathan_hide", 6), ("automaton_servo_joint", 6), ("automaton_relay", 6)],
           "Не пустая коробка, а загрузка, створки, герметизация, крепления и доступ к добыче."),
    recipe("make_industry_block", "industry_block", "Промысловый блок", "ship_block", "assembly", 1, 140,
           [("compressor", 5), ("pump", 5), ("actuator", 4), ("sensor", 4), ("calculator", 3), ("airlock", 4), ("cowling", 4), ("deck_section", 4), ("armor_plate", 3), ("fluoroplastic", 15), ("aramid", 12), ("leviathan_sinew", 10), ("acid", 15), ("automaton_command_cylinder", 6), ("automaton_pressure_gauge", 8), ("automaton_servo_core", 4)],
           "Руда, газ, левиафаны и сальваж в одном большом промышленном узле."),
    recipe("make_hangar_block", "hangar_block", "Ангарный блок", "ship_block", "assembly", 1, 125,
           [("deck_section", 6), ("bulkhead", 6), ("airlock", 5), ("hatch", 8), ("actuator", 6), ("sensor", 4), ("calculator", 4), ("pump", 4), ("plating", 6), ("rubber", 12), ("textolite", 12), ("mechanisms", 20), ("automaton_core", 4), ("automaton_relay", 10), ("automaton_contact_comb", 10), ("automaton_servo_joint", 8)],
           "Запуск, возврат, хранение и обслуживание дронов, автоматонов и сервисных машин."),
]


R10_PROFILES = [
    {
        "profile_id": "r10_frigate",
        "ship_class_ru": "Фрегат",
        "craft_multiplier_vs_r9": 2.00,
        "base_time_min": 900,
        "blocks": [("power_block", 1), ("propulsion_block", 1), ("instrument_block", 1), ("weapon_block", 1)],
        "extras": [("steel", 40), ("orkit", 15), ("sensor", 2), ("calculator", 2), ("automaton_core", 1)],
        "notes_ru": "Десятый фрегат остается фрегатом, но требует взрослую блоковую сборку и фракционную лицензию.",
    },
    {
        "profile_id": "r10_cruiser",
        "ship_class_ru": "Крейсер",
        "craft_multiplier_vs_r9": 2.25,
        "base_time_min": 900,
        "blocks": [("power_block", 1), ("propulsion_block", 1), ("instrument_block", 1), ("weapon_block", 1), ("defense_block", 1), ("cargo_block", 1)],
        "extras": [("steel", 80), ("bulat", 20), ("orkit", 30), ("engine", 2), ("sensor", 4), ("calculator", 4), ("automaton_core", 2), ("automaton_servo_core", 2)],
        "notes_ru": "Десятый крейсер уже собирается как основная взрослая машина ветки.",
    },
    {
        "profile_id": "r10_battleship",
        "ship_class_ru": "Линкор",
        "craft_multiplier_vs_r9": 2.50,
        "base_time_min": 900,
        "blocks": [("power_block", 1), ("propulsion_block", 1), ("instrument_block", 1), ("weapon_block", 2), ("defense_block", 2), ("industry_block", 1), ("hangar_block", 1)],
        "extras": [("steel", 150), ("bulat", 50), ("orkit", 60), ("engine", 4), ("sensor", 8), ("calculator", 8), ("automaton_core", 5), ("automaton_servo_core", 5), ("leviathan_ichor", 10), ("nulgas", 10)],
        "notes_ru": "Десятый линкор является вершиной текущей корабельной прогрессии и съедает почти весь набор блоков.",
    },
]


def write_csv(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def ids(inputs: list[tuple[str, int]]) -> str:
    return ";".join(item_id for item_id, _ in inputs)


def amounts(inputs: list[tuple[str, int]]) -> str:
    return ";".join(str(amount) for _, amount in inputs)


def scaled_amounts(inputs: list[tuple[str, int]], multiplier: float) -> list[int]:
    return [max(1, math.ceil(amount * multiplier)) for _, amount in inputs]


def write_progression_configs() -> None:
    write_csv(
        CONFIG_DIR / "recipe_time_upgrade_curve.csv",
        ["time_level", "time_multiplier", "time_reduction_vs_level0_percent", "notes_ru"],
        [
            {
                "time_level": level,
                "time_multiplier": f"{multiplier:.2f}",
                "time_reduction_vs_level0_percent": round((1 - multiplier / TIME_CURVE[0][1]) * 100, 2),
                "notes_ru": notes,
            }
            for level, multiplier, notes in TIME_CURVE
        ],
    )

    write_csv(
        CONFIG_DIR / "recipe_efficiency_upgrade_curve.csv",
        ["economy_level", "input_multiplier", "input_saving_vs_level0_percent", "notes_ru"],
        [
            {
                "economy_level": level,
                "input_multiplier": f"{multiplier:.2f}",
                "input_saving_vs_level0_percent": round((1 - multiplier / EFFICIENCY_CURVE[0][1]) * 100, 2),
                "notes_ru": notes,
            }
            for level, multiplier, notes in EFFICIENCY_CURVE
        ],
    )

    building_rows = []
    for level in range(1, 31):
        speed = 1.0 + (level - 1) * 0.5 / 29
        building_rows.append({
            "building_level": level,
            "speed_multiplier": f"{speed:.4f}",
            "time_multiplier": f"{1 / speed:.4f}",
            "notes_ru": "Максимум здания ускоряет все рецепты этого здания примерно в полтора раза." if level == 30 else "",
        })
    write_csv(
        CONFIG_DIR / "recipe_building_speed_curve.csv",
        ["building_level", "speed_multiplier", "time_multiplier", "notes_ru"],
        building_rows,
    )

    recipe_rows = []
    for r in RECIPES:
        recipe_rows.append({
            "recipe_id": r["recipe_id"],
            "output_item_id": r["output_item_id"],
            "local_name_ru": r["local_name_ru"],
            "layer": r["layer"],
            "building_id": r["building_id"],
            "output_amount": r["output_amount"],
            "base_time_min_level5_building1": f"{r['base_time_min']:.2f}",
            "input_ids": ids(r["inputs"]),
            "input_amounts_level5": amounts(r["inputs"]),
            "notes_ru": r["notes_ru"],
        })
    write_csv(
        CONFIG_DIR / "item_production_recipes.csv",
        [
            "recipe_id", "output_item_id", "local_name_ru", "layer", "building_id",
            "output_amount", "base_time_min_level5_building1", "input_ids",
            "input_amounts_level5", "notes_ru",
        ],
        recipe_rows,
    )

    cost_rows = []
    for r in RECIPES:
        for level, multiplier, _ in EFFICIENCY_CURVE:
            level_amounts = scaled_amounts(r["inputs"], multiplier)
            cost_rows.append({
                "recipe_id": r["recipe_id"],
                "economy_level": level,
                "input_multiplier": f"{multiplier:.2f}",
                "input_ids": ids(r["inputs"]),
                "input_amounts": ";".join(str(v) for v in level_amounts),
                "total_input_units": sum(level_amounts),
            })
    write_csv(
        CONFIG_DIR / "item_recipe_cost_levels.csv",
        ["recipe_id", "economy_level", "input_multiplier", "input_ids", "input_amounts", "total_input_units"],
        cost_rows,
    )

    time_rows = []
    for r in RECIPES:
        for level, multiplier, _ in TIME_CURVE:
            time_building1 = r["base_time_min"] * multiplier
            time_building30 = time_building1 / 1.5
            time_rows.append({
                "recipe_id": r["recipe_id"],
                "time_level": level,
                "time_multiplier": f"{multiplier:.2f}",
                "time_min_building1": f"{time_building1:.2f}",
                "time_min_building30": f"{time_building30:.2f}",
            })
    write_csv(
        CONFIG_DIR / "item_recipe_time_levels.csv",
        ["recipe_id", "time_level", "time_multiplier", "time_min_building1", "time_min_building30"],
        time_rows,
    )

    rank_targets = [
        ("R2", 1.07, "Ранний корабль: около 4 минут на минимальной прокачке."),
        ("R3", 2.50, "Последний простой ранг."),
        ("R4", 6.00, "Первое заметное ожидание материалов."),
        ("R5", 15.00, "Середина ранней промышленности."),
        ("R6", 40.00, "Первый серьезный слой деталей."),
        ("R7", 90.00, "Развитые детали и промышленные узлы."),
        ("R8", 180.00, "Первые крупные блоки."),
        ("R9", 330.00, "Почти верхняя блоковая сборка."),
        ("R10", 600.00, "Полная прокачка города: корабль десятого ранга делается 10 часов."),
    ]
    rank_rows = []
    for rank, full_time, notes in rank_targets:
        base_time = full_time * 1.5
        new_time = base_time * TIME_CURVE[0][1]
        rank_rows.append({
            "rank": rank,
            "time_min_level5_building30": f"{full_time:.2f}",
            "base_time_min_level5_building1": f"{base_time:.2f}",
            "time_min_level0_building1": f"{new_time:.2f}",
            "notes_ru": notes,
        })
    write_csv(
        CONFIG_DIR / "ship_rank_craft_time_policy.csv",
        ["rank", "time_min_level5_building30", "base_time_min_level5_building1", "time_min_level0_building1", "notes_ru"],
        rank_rows,
    )

    r10_rows = []
    r10_cost_rows = []
    r10_time_rows = []
    for p in R10_PROFILES:
        combined = p["blocks"] + p["extras"]
        r10_rows.append({
            "profile_id": p["profile_id"],
            "ship_class_ru": p["ship_class_ru"],
            "craft_multiplier_vs_r9": f"{p['craft_multiplier_vs_r9']:.2f}",
            "required_license_rule_ru": "1 флагманская лицензия своей фракции, расходуется при сборке",
            "base_time_min_level5_building1": f"{p['base_time_min']:.2f}",
            "time_min_level5_building30": f"{p['base_time_min'] / 1.5:.2f}",
            "time_min_level0_building1": f"{p['base_time_min'] * TIME_CURVE[0][1]:.2f}",
            "input_ids_level5": ids(combined),
            "input_amounts_level5": amounts(combined),
            "notes_ru": p["notes_ru"],
        })
        for level, multiplier, _ in EFFICIENCY_CURVE:
            level_amounts = scaled_amounts(combined, multiplier)
            r10_cost_rows.append({
                "profile_id": p["profile_id"],
                "economy_level": level,
                "input_multiplier": f"{multiplier:.2f}",
                "required_license_amount": 1,
                "input_ids": ids(combined),
                "input_amounts": ";".join(str(v) for v in level_amounts),
                "total_discountable_input_units": sum(level_amounts),
            })
        for level, multiplier, _ in TIME_CURVE:
            time_building1 = p["base_time_min"] * multiplier
            r10_time_rows.append({
                "profile_id": p["profile_id"],
                "time_level": level,
                "time_multiplier": f"{multiplier:.2f}",
                "time_min_building1": f"{time_building1:.2f}",
                "time_min_building30": f"{time_building1 / 1.5:.2f}",
            })
    write_csv(
        CONFIG_DIR / "ship_r10_recipe_profiles.csv",
        [
            "profile_id", "ship_class_ru", "craft_multiplier_vs_r9", "required_license_rule_ru",
            "base_time_min_level5_building1", "time_min_level5_building30",
            "time_min_level0_building1", "input_ids_level5", "input_amounts_level5", "notes_ru",
        ],
        r10_rows,
    )
    write_csv(
        CONFIG_DIR / "ship_r10_recipe_cost_levels.csv",
        ["profile_id", "economy_level", "input_multiplier", "required_license_amount", "input_ids", "input_amounts", "total_discountable_input_units"],
        r10_cost_rows,
    )
    write_csv(
        CONFIG_DIR / "ship_r10_recipe_time_levels.csv",
        ["profile_id", "time_level", "time_multiplier", "time_min_building1", "time_min_building30"],
        r10_time_rows,
    )


if __name__ == "__main__":
    write_progression_configs()
    print("Recipe progression configs generated.")
