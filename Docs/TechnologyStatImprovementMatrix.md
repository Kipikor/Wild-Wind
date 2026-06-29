# Wild Wind: матрица технологий для улучшения чисел

Дата фиксации: 2026-05-18

Статус: черновой список для конфигов. Это не финальный баланс, а EVE-подобная матрица: у каждой важной цифры игры должен быть хотя бы один технологический путь улучшения.

## Главный принцип

В Wild Wind технологии улучшают не абстрактные параметры вроде "+10% ко всему", а конкретные числа конкретных систем.

Пример хорошей технологии:

```yaml
id: tech_claudium_distributed_injection
name: "Распределительный впрыск клавдия"
levels: 5
effect_per_level: "-2% расхода клавдия у клавдиевого контура соответствующего уровня"
applies_to: [ship_node_claudium_loop]
description: "Клавдий не усиливают количеством. Его учатся распределять точнее."
```

Правило: если в игре есть числовой параметр, он должен попасть в один из трёх классов.

- **Улучшается технологией**: игрок может целенаправленно развивать эту цифру.
- **Улучшается корабельным узлом/ригом**: цифра растёт через конкретный корабль.
- **Не улучшается намеренно**: это физический закон мира или балансный якорь.

## Балансные соглашения

Технологии уровня I-V должны давать небольшие, но долгосрочно значимые прибавки.

Базовые ориентиры:

- маленький универсальный бонус: 1-2% за уровень;
- нормальный специализированный бонус: 3-5% за уровень;
- редкий дорогой бонус: 6-8% за уровень;
- дискретный бонус: +1 слот или +1 параллельная задача на уровнях II/IV/V;
- снижение риска: -0.5..-2 процентных пункта за уровень;
- снижение расхода ресурсов: -1..-3% за уровень;
- ускорение цикла: -2..-5% времени за уровень.

Чтобы игра не ломалась от перемножения:

- однотипные бонусы внутри одной семьи складываются;
- разные семьи могут перемножаться;
- у каждой цифры должен быть мягкий предел или дорогая цена улучшения;
- риги могут давать сильнее технологии, но почти всегда с минусом;
- узлы корабля дают основную силу конкретному кораблю;
- технологии дают цивилизационную зрелость всей сети.

## Карта чисел

### Корабль

```yaml
ship_stats:
  cargo_capacity: "грузоподъёмность"
  gas_tank_capacity: "объём газовых баллонов"
  fuel_capacity: "запас топлива"
  claudium_capacity: "запас/рабочий объём клавдия"
  max_speed: "максимальная скорость"
  acceleration: "разгон"
  braking: "торможение"
  turn_rate: "крен/поворот"
  range_km: "практическая дальность"
  fuel_use: "расход топлива"
  claudium_use: "расход клавдия"
  claudium_lift_efficiency: "эффективность подъёмного контура"
  aerodynamics: "коэффициент воздействия ветра"
  storm_damage_taken: "получаемый урон от бури"
  cold_damage_taken: "получаемый урон от холода"
  hull_hp: "прочность корпуса"
  armor: "броневая защита"
  wear_rate: "скорость износа"
  repair_rate: "скорость ремонта"
  maintenance_cost: "стоимость обслуживания"
  crew_required: "требуемый экипаж"
  crew_fatigue_rate: "усталость экипажа"
  morale_drain: "падение морали"
  command_slots: "командные слоты"
  dock_slots: "доковые слоты"
  factory_slots: "производственные слоты на борту"
  automation: "уровень автоматизации"
  mining_rate: "скорость добычи руды"
  gas_harvest_rate: "скорость сбора газа"
  hunting_power: "сила охоты/гарпунов"
  sensor_range: "дальность обнаружения"
  navigation_error: "ошибка маршрута"
  stealth: "скрытность"
```

### Производства

```yaml
production_stats:
  generation_base_rate: "базовая скорость выработки острова"
  generation_need_bonus: "бонус от закрытых потребностей"
  processing_cycle_time: "длительность цикла переработки"
  processing_yield: "выход полезных фракций"
  processing_fuel_cost: "топливо на цикл"
  processing_energy_cost: "энергия на цикл"
  manufacturing_cycle_time: "длительность крафта"
  manufacturing_input_cost: "стоимость входов"
  manufacturing_output_amount: "количество выхода"
  recipe_quality_bonus: "качество рецепта"
  reaction_speed_limit: "максимальный ползунок реакции"
  reaction_failure_chance: "шанс сорвать партию"
  catalyst_efficiency: "эффект катализатора"
  reaction_batch_size: "размер партии"
  conversion_spinup_rate: "разгон маховика"
  conversion_decay_rate: "затухание маховика"
  conversion_max_multiplier: "потолок маховика"
  assembly_stage_time: "длительность этапа сборки"
  assembly_input_cost: "ресурсы этапа сборки"
  assembly_parallelism: "сколько этапов/проектов можно вести"
  worker_fatigue: "усталость рабочих"
  defect_chance: "шанс брака"
  warehouse_capacity: "склад"
  throughput: "пропускная способность"
```

### Мир и логистика

```yaml
world_logistics_stats:
  route_slots: "количество маршрутов"
  island_dock_radius: "радиус стыковки"
  docking_speed: "скорость погрузки/разгрузки"
  local_pilot_speed: "скорость островских пилотов"
  local_collection_radius: "радиус локального сбора"
  idle_report_detail: "подробность отчёта idle-сессии"
  scout_speed: "скорость разведки"
  observation_gain: "получение наблюдений"
  research_speed: "скорость исследований"
  research_cost: "стоимость исследований"
  blueprint_runs: "число прогонов ограниченного рецепта"
  artifact_decode_chance: "шанс дешифровки артефакта"
```

## Корабельные технологии

### Груз и трюмы

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_cargo_rational_stowage | Рациональная укладка груза | 5 | cargo_capacity | +4% грузового объёма за уровень |
| tech_cargo_weight_balance | Балансировка грузовой палубы | 5 | aerodynamics, turn_rate | -2% воздействия ветра и +2% поворота у грузовых кораблей |
| tech_cargo_modular_crates | Модульные грузовые ячейки | 5 | docking_speed, cargo_capacity | +3% скорость разгрузки, +2% груз |
| tech_cargo_standard_containers | Стандартные контейнеры | 5 | throughput | +5% пропускная способность складов и трюмов |
| tech_cargo_hazard_separation | Раздельное хранение опасных грузов | 5 | defect_chance, reaction_failure_chance | -1 п.п. риска аварии при перевозке реагентов |

Художественный смысл: грузовик становится сильнее не потому, что его "бафнули", а потому что цивилизация научилась упаковывать, крепить, учитывать и быстро разгружать.

### Двигатели и скорость

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_engine_alcohol_injection | Спиртовой впрыск | 5 | max_speed, fuel_use | +3% скорость спиртовых двигателей, -1% расход |
| tech_engine_valve_lubrication | Клапанная смазка | 5 | fuel_use, wear_rate | -2% расход топлива, -3% износ двигателя |
| tech_engine_steam_nozzle_geometry | Геометрия паровых сопел | 5 | max_speed, acceleration | +3% скорость и +3% разгон паровых машин |
| tech_engine_gearbox_ratio | Редукторные передаточные числа | 5 | acceleration, fuel_use | +4% разгон, -1% расход |
| tech_engine_emergency_overpressure | Аварийный наддув | 5 | max_speed | +5% кратковременная мощность, но +2% износ при использовании |
| tech_engine_heat_recovery | Возврат тепла | 5 | range_km, fuel_use | +3% дальность, -2% расход топлива |

### Клавдий и подъём

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_claudium_loop_calibration | Калибровка клавдиевого контура | 5 | claudium_lift_efficiency | +3% эффективность подъёма |
| tech_claudium_distributed_injection | Распределительный впрыск клавдия | 5 | claudium_use | -2% расход клавдия на соответствующем уровне контура |
| tech_claudium_grid_balancing | Балансировка клавдиевой решётки | 5 | storm_damage_taken, aerodynamics | -3% урон от турбулентности, -2% воздействие ветра |
| tech_claudium_leak_detection | Поиск утечек клавдия | 5 | claudium_use, maintenance_cost | -2% расход, -2% обслуживание |
| tech_claudium_high_altitude_phase | Высотная фаза клавдия | 5 | claudium_lift_efficiency | +4% эффективность в разреженной/ледяной зоне |
| tech_claudium_mass_centering | Центровка подъёмной массы | 5 | turn_rate, storm_damage_taken | +3% управляемость, -2% штормовой урон |

### Аэродинамика и буря

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_aero_load_bearing_fairing | Обтекатели несущего корпуса | 5 | aerodynamics | -4% коэффициент воздействия ветра |
| tech_aero_storm_skin | Штормовая обшивка | 5 | storm_damage_taken, hull_hp | -4% урон от бури, +2% прочность |
| tech_aero_rudder_surfaces | Маневровые плоскости | 5 | turn_rate, braking | +4% поворот, +3% торможение |
| tech_aero_vibration_dampers | Виброгасящие узлы | 5 | wear_rate, crew_fatigue_rate | -3% износ, -2% усталость экипажа |
| tech_aero_lower_storm_protocols | Протоколы нижней бури | 5 | storm_damage_taken, navigation_error | -5% урон в яростной буре, -3% ошибка маршрута |

### Корпус, броня, износ

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_hull_frame_stress_maps | Карты напряжения каркаса | 5 | hull_hp, wear_rate | +4% прочность, -2% износ |
| tech_hull_reinforced_bulkheads | Усиленные переборки | 5 | hull_hp, storm_damage_taken | +5% прочность, -2% штормовой урон |
| tech_hull_armor_belting | Бронепояс отсеков | 5 | armor | +5% броня |
| tech_hull_lightweight_lattice | Облегчённая ферма | 5 | max_speed, cargo_capacity | +2% скорость, +2% груз при лёгких корпусах |
| tech_hull_crack_monitoring | Контроль трещин | 5 | wear_rate, maintenance_cost | -4% износ, -2% обслуживание |
| tech_hull_field_patch_standards | Стандарт полевых заплат | 5 | repair_rate | +5% скорость ремонта корпуса |

### Экипаж, мораль, усталость

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_crew_watch_rotations | Вахтовые графики | 5 | crew_fatigue_rate | -5% усталость экипажа |
| tech_crew_ration_planning | Вахтовое питание | 5 | morale_drain, crew_fatigue_rate | -3% падение морали, -2% усталость |
| tech_crew_compact_quarters | Компактные жилые блоки | 5 | crew_required, morale_drain | -2% требуемый экипаж на крупных кораблях, -1% моральный штраф тесноты |
| tech_crew_medical_shifts | Медицинские смены | 5 | cold_damage_taken, crew_fatigue_rate | -3% холодовой ущерб экипажу, -2% усталость |
| tech_crew_leisure_salons | Салоны комфорт-класса | 5 | morale_drain | -5% падение морали в дальних рейдах |
| tech_crew_training_drills | Учебные тревоги | 5 | repair_rate, defect_chance | +3% ремонт, -1 п.п. аварийность |

### Навигация, сенсоры, разведка

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_nav_barometric_tables | Барометрические таблицы | 5 | navigation_error | -4% ошибка маршрута |
| tech_nav_gyro_stabilization | Гироскопическая стабилизация | 5 | turn_rate, navigation_error | +3% поворот, -3% ошибка |
| tech_nav_optical_rangefinding | Оптическое дальномерение | 5 | sensor_range | +5% дальность обнаружения |
| tech_nav_storm_prediction | Штормовой прогноз | 5 | storm_damage_taken, route_safety | -3% урон от бури, +3% безопасность маршрута |
| tech_nav_leviathan_pattern_reading | Чтение левиафановых потоков | 5 | navigation_error, claudium_lift_efficiency | -2% ошибка, +2% подъём в зоне обитания |
| tech_nav_high_altitude_sighting | Высотное визирование | 5 | sensor_range, range_km | +4% сенсоры, +2% дальность в высоких слоях |

### Автоматизация корабля

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_auto_relay_autopilot | Релейный автопилот | 5 | automation, navigation_error | +0.2 automation за уровень, -2% ошибка маршрута |
| tech_auto_diagnostic_network | Диагностическая сеть | 5 | wear_rate, repair_rate | -3% износ, +3% ремонт |
| tech_auto_power_distribution | Автораспределение мощности | 5 | fuel_use, factory_slots | -2% расход, +1 бортовой производственный слот на уровнях III/V |
| tech_auto_cargo_sorting_ship | Бортовая сортировка груза | 5 | docking_speed, crew_required | +4% разгрузка, -2% требуемый экипаж грузовых операций |
| tech_auto_fleet_command_protocols | Протоколы командования флотом | 5 | command_slots | +1 командный слот на уровнях II/IV/V |

## Добывающие технологии

### Руда

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_mining_grabber_tension | Натяжение рудного захвата | 5 | mining_rate | +5% скорость добычи руды |
| tech_mining_ore_surveying | Рудная съёмка | 5 | processing_yield | +2% полезный выход при переработке разведанной руды |
| tech_mining_fragment_control | Контроль дробления глыб | 5 | defect_chance, mining_rate | -1 п.п. потерь, +2% добыча |
| tech_mining_cargo_stabilization | Стабилизация рудного груза | 5 | cargo_capacity, wear_rate | +2% рудный груз, -2% износ добытчиков |
| tech_mining_deep_vein_reading | Чтение глубинных жил | 5 | rare_resource_chance | +1 п.п. шанс редких минералов |

### Газы

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_gas_diffuser_geometry | Геометрия облачного диффузора | 5 | gas_harvest_rate | +5% сбор газа |
| tech_gas_tank_compaction | Уплотнение газовых баллонов | 5 | gas_tank_capacity | +5% объём газовых баллонов |
| tech_gas_membrane_filters | Мембранная фильтрация газа | 5 | processing_yield | +3% выход нужной газовой фракции |
| tech_gas_leak_protocols | Протоколы утечек | 5 | defect_chance, crew_safety | -1 п.п. аварии газовых операций |
| tech_gas_preservation_chambers | Консервирующие камеры | 5 | storage_loss | -5% потери нестабильных газов |

### Левиафаны

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_hunt_harpoon_tension | Натяжение гарпунных тросов | 5 | hunting_power | +5% сила охоты |
| tech_hunt_behavior_observation | Поведенческие наблюдения | 5 | hunting_power, crew_safety | +2% охота, -2% риск экипажа |
| tech_hunt_carcass_preservation | Сохранение туши | 5 | processing_yield | +4% выход органов и тканей |
| tech_hunt_biohazard_protocols | Биобезопасность охоты | 5 | defect_chance, medicine_use | -1 п.п. аварийность, -2% расход медикаментов |
| tech_hunt_nonlethal_tagging | Нелетальное мечение | 5 | observation_gain | +5% получение наблюдений без охоты |

### Айсберги и сублиматы

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_ice_harpoon_anchoring | Якорение ледового гарпуна | 5 | ice_harvest_rate | +5% сбор айсбергов |
| tech_ice_thermal_cutting | Тепловая резка льда | 5 | processing_cycle_time | -4% время переработки льда |
| tech_ice_sublimate_sorting | Сортировка сублиматов | 5 | rare_resource_chance | +1 п.п. шанс ценных сублиматов |
| tech_ice_cold_chain | Холодовая цепь | 5 | storage_loss, cold_damage_taken | -5% потери, -3% холодовой урон |
| tech_ice_high_altitude_crew | Высотные ледовые смены | 5 | crew_fatigue_rate | -5% усталость экипажа в ледяной зоне |

## Производственные технологии

### Generation

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_gen_need_fulfillment | Нормы закрытия потребностей | 5 | generation_need_bonus | +4% бонус от закрытых потребностей |
| tech_gen_local_tooling | Локальная оснастка | 5 | generation_base_rate | +3% базовая выработка островов |
| tech_gen_worker_routines | Рабочие распорядки | 5 | worker_fatigue, generation_base_rate | -3% усталость, +2% выработка |
| tech_gen_storage_flow | Поток со склада на производство | 5 | throughput | +5% пропускная способность островного склада |

### Processing

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_proc_cycle_optimization | Оптимизация цикла переработки | 5 | processing_cycle_time | -4% время цикла |
| tech_proc_fraction_recovery | Возврат мелких фракций | 5 | processing_yield | +3% выход полезных фракций |
| tech_proc_fuel_metering | Дозировка топлива переработки | 5 | processing_fuel_cost | -3% расход топлива |
| tech_proc_energy_balancing | Балансировка энергии цикла | 5 | processing_energy_cost | -3% расход энергии |
| tech_proc_waste_separation | Отделение пустой породы | 5 | defect_chance | -1 п.п. потерь/засоров |

### Manufacturing

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_mfg_jig_fixtures | Сборочные приспособления | 5 | manufacturing_cycle_time | -4% время производства |
| tech_mfg_material_nesting | Раскрой материалов | 5 | manufacturing_input_cost | -2% расход входов |
| tech_mfg_quality_routines | Контроль качества | 5 | defect_chance, recipe_quality_bonus | -1 п.п. брака, +1% качество |
| tech_mfg_parallel_benches | Параллельные верстаки | 5 | assembly_parallelism | +1 малый параллельный проект на уровнях III/V |
| tech_mfg_worker_ergonomics | Эргономика рабочих мест | 5 | worker_fatigue | -4% усталость рабочих |

### Reaction

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_react_batch_sensors | Датчики партии | 5 | reaction_failure_chance | -1 п.п. шанс срыва партии |
| tech_react_catalyst_recovery | Возврат катализатора | 5 | catalyst_efficiency | +5% эффективность катализатора |
| tech_react_heat_gradient | Тепловой градиент реакции | 5 | reaction_speed_limit | +2 к максимальному безопасному множителю реакции |
| tech_react_pressure_relief | Сброс давления | 5 | reaction_failure_chance | -1 п.п. аварийность быстрых реакций |
| tech_react_batch_scaling | Масштабирование партии | 5 | reaction_batch_size | +5% размер партии |

### Conversion

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_conv_flywheel_bearings | Подшипники маховика | 5 | conversion_decay_rate | -5% затухание маховика |
| tech_conv_startup_rhythm | Ритм запуска маховика | 5 | conversion_spinup_rate | +4% разгон |
| tech_conv_load_prediction | Прогноз нагрузки | 5 | conversion_missed_cycle_penalty | -5% штраф за пропуск цикла |
| tech_conv_mass_balancing | Массовая балансировка конверсии | 5 | conversion_max_multiplier | +0.25 к максимальному множителю на уровнях II/IV/V |
| tech_conv_continuous_feed | Непрерывная подача | 5 | throughput | +5% снабжение маховиковых линий |

### Assembly

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_assy_stage_planning | Планирование этапов сборки | 5 | assembly_stage_time | -4% время этапа |
| tech_assy_preloaded_kits | Предзагруженные комплекты | 5 | throughput, assembly_stage_time | +5% подача, -2% время |
| tech_assy_heavy_cranes | Тяжёлые краны | 5 | assembly_parallelism | +1 крупный проект на уровнях IV/V |
| tech_assy_precision_alignment | Точная центровка сборки | 5 | defect_chance, ship_wear_rate | -1 п.п. брака, -2% будущий износ корабля |
| tech_assy_modular_blocks | Модульные блоки | 5 | assembly_input_cost | -2% расход компонентов на повторяющихся блоках |

## Острова и логистика

### Склады, доки, маршруты

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_log_warehouse_shelves | Складские стеллажи | 5 | warehouse_capacity | +8% склад острова |
| tech_log_dock_marks | Разметка дока | 5 | island_dock_radius, docking_speed | +3% радиус стыковки, +3% разгрузка |
| tech_log_crane_signals | Крановые сигналы | 5 | docking_speed | +5% скорость погрузки |
| tech_log_route_dispatch | Диспетчеризация маршрутов | 5 | route_slots | +1 активный маршрут на уровнях II/IV/V |
| tech_log_priority_orders | Приоритеты снабжения | 5 | idle_report_detail, route_efficiency | +5% эффективность автоснабжения |
| tech_log_local_pilot_training | Подготовка островских пилотов | 5 | local_pilot_speed | +5% скорость локальных пилотов |
| tech_log_collection_radius | Карта локального сбора | 5 | local_collection_radius | +5% радиус островского сбора |

### Idle-отчёты и контроль

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_report_route_ledgers | Рейсовые журналы | 5 | idle_report_detail | +1 уровень подробности отчётов на I/III/V |
| tech_report_incident_tags | Метки инцидентов | 5 | idle_report_detail | Отчёты лучше показывают аварии, простои и дефициты |
| tech_report_resource_attribution | Источник ресурсов | 5 | idle_report_detail | Отчёт показывает, какой остров/корабль дал результат |
| tech_report_risk_forecast | Прогноз риска | 5 | route_safety | Игрок заранее видит риск длинной idle-сессии |

## Исследования, знания, рецепты

### Опыт и исследования

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_research_archive_indexing | Индексация архивов | 5 | research_speed | +4% скорость исследований по артефактам |
| tech_research_field_notebooks | Полевые журналы | 5 | observation_gain | +5% наблюдения от разведки и левиафанов |
| tech_research_recipe_comparison | Сравнение рецептов | 5 | recipe_quality_bonus | +2% шанс улучшить рецепт после анализа |
| tech_research_failed_run_analysis | Анализ сорванных партий | 5 | research_speed, reaction_failure_chance | +2% химический опыт, -0.5 п.п. риск повторной партии |
| tech_research_ship_teardown | Разбор изношенных кораблей | 5 | design_experience_gain | +5% конструкторский опыт от списанных кораблей |

### Чертежи и ограниченные рецепты

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_blueprint_clean_copying | Чистое копирование чертежей | 5 | blueprint_runs | +1 прогон к ограниченным рецептам на уровнях III/V |
| tech_blueprint_damage_reconstruction | Восстановление повреждённых чертежей | 5 | artifact_decode_chance | +4% шанс восстановления |
| tech_blueprint_process_optimization | Оптимизация процесса по чертежу | 5 | manufacturing_input_cost, manufacturing_cycle_time | -1% входы, -2% время для изученного рецепта |
| tech_blueprint_variant_catalogue | Каталог вариантов | 5 | recipe_discovery_chance | +3% шанс получить альтернативный рецепт |

## Боевые и опасные технологии

Пока бой не является главным фокусом, но цифры охраны, автоматонов и левиафанов должны иметь место в системе.

| id | Название | Уровни | Улучшает | Эффект |
|---|---|---:|---|---|
| tech_def_light_turret_drills | Учения лёгких турелей | 5 | escort_defense | +4% эффективность эскорта |
| tech_def_harpoon_recoil_dampers | Демпферы отдачи гарпуна | 5 | hunting_power, wear_rate | +3% охота, -2% износ гарпуна |
| tech_def_automaton_weakpoints | Слабые места автоматонов | 5 | automaton_combat_efficiency | +5% эффективность против автоматонов |
| tech_def_convoy_spacing | Дистанции конвоя | 5 | route_safety | -3% риск потерь в рейде |
| tech_def_emergency_smoke_lamps | Аварийные дымовые лампы | 5 | escape_chance | +3% шанс уйти от опасности |

## Корабельные узлы I-V и связанные технологии

Узел корабля - главный носитель силы конкретного судна. Технологии должны не заменять узлы, а открывать и усиливать их.

```yaml
ship_node_upgrade_families:
  hull:
    level_1: "базовая целостность"
    level_2: "несущий корпус"
    level_3: "усиленные переборки"
    level_4: "штормовая рама"
    level_5: "сублиматная центровка"
    supporting_techs: [tech_hull_frame_stress_maps, tech_hull_reinforced_bulkheads, tech_assy_precision_alignment]

  engine:
    level_1: "базовый двигатель"
    level_2: "спиртовой двигатель"
    level_3: "паротурбинный привод"
    level_4: "форсированный привод"
    level_5: "автоматонное управление мощностью"
    supporting_techs: [tech_engine_alcohol_injection, tech_engine_steam_nozzle_geometry, tech_engine_heat_recovery]

  claudium_loop:
    level_1: "малый контур"
    level_2: "распределительный впрыск"
    level_3: "балансировка решётки"
    level_4: "высотная фаза"
    level_5: "предтечевая обратная связь"
    supporting_techs: [tech_claudium_loop_calibration, tech_claudium_distributed_injection, tech_claudium_high_altitude_phase]

  cargo:
    level_1: "малый трюм"
    level_2: "модульные ячейки"
    level_3: "стандартные контейнеры"
    level_4: "автосортировка"
    level_5: "экспедиционная грузовая сеть"
    supporting_techs: [tech_cargo_rational_stowage, tech_cargo_modular_crates, tech_auto_cargo_sorting_ship]

  navigation:
    level_1: "карты и компас"
    level_2: "барометрические таблицы"
    level_3: "гироскопический блок"
    level_4: "оптический дальномер"
    level_5: "предтечевый навигационный интерфейс"
    supporting_techs: [tech_nav_barometric_tables, tech_nav_gyro_stabilization, tech_nav_optical_rangefinding]

  automation:
    level_1: "ручное управление"
    level_2: "релейный автопилот"
    level_3: "диагностическая сеть"
    level_4: "автораспределение мощности"
    level_5: "центральный машинный надзор"
    supporting_techs: [tech_auto_relay_autopilot, tech_auto_diagnostic_network, tech_auto_power_distribution]
```

## Coverage-чеклист

Этот раздел нужен для будущего большого теста.

```yaml
coverage_expectations:
  cargo_capacity: [tech_cargo_rational_stowage, cargo_node_upgrades, cargo_rigs]
  max_speed: [tech_engine_alcohol_injection, tech_engine_steam_nozzle_geometry, engine_node_upgrades]
  fuel_use: [tech_engine_valve_lubrication, tech_engine_heat_recovery, economy_rigs]
  claudium_use: [tech_claudium_distributed_injection, claudium_loop_node_upgrades]
  aerodynamics: [tech_aero_load_bearing_fairing, tech_cargo_weight_balance, storm_rigs]
  storm_damage_taken: [tech_aero_storm_skin, tech_aero_lower_storm_protocols, hull_node_upgrades]
  cold_damage_taken: [tech_ice_cold_chain, tech_crew_medical_shifts, heated_crew_blocks]
  wear_rate: [tech_hull_crack_monitoring, tech_auto_diagnostic_network, maintenance_rigs]
  repair_rate: [tech_hull_field_patch_standards, tech_crew_training_drills, repair_ship_roles]
  crew_fatigue_rate: [tech_crew_watch_rotations, tech_crew_ration_planning, tech_ice_high_altitude_crew]
  mining_rate: [tech_mining_grabber_tension, mining_node_upgrades]
  gas_harvest_rate: [tech_gas_diffuser_geometry, gas_collector_nodes]
  hunting_power: [tech_hunt_harpoon_tension, tech_def_harpoon_recoil_dampers]
  processing_yield: [tech_proc_fraction_recovery, tech_mining_ore_surveying, tech_gas_membrane_filters]
  reaction_failure_chance: [tech_react_batch_sensors, tech_react_pressure_relief, tech_cargo_hazard_separation]
  conversion_spinup_rate: [tech_conv_startup_rhythm]
  conversion_decay_rate: [tech_conv_flywheel_bearings]
  assembly_stage_time: [tech_assy_stage_planning, tech_assy_preloaded_kits]
  warehouse_capacity: [tech_log_warehouse_shelves]
  route_slots: [tech_log_route_dispatch]
  research_speed: [tech_research_archive_indexing]
  blueprint_runs: [tech_blueprint_clean_copying]
```

Правило для теста: если появляется новый числовой параметр, он должен быть добавлен в этот coverage-чеклист или явно помечен как неулучшаемый закон мира.
