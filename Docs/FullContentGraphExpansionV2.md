# Wild Wind: расширение полного графа контента v2

Дата фиксации: 2026-05-18

Статус: расширенный черновой предконфиг. Это не финальный баланс и не окончательный канон названий. Документ нужен, чтобы увидеть большую связанную систему кораблей, технологий, рецептов, компонентов, островов и производств.

Главная цель v2: уйти от поверхностного списка к сетке, которая выдерживает годы контента.

## Ключевой замысел

Корабли - это верхушка пирамиды. Под ними лежат:

- ресурсы;
- острова;
- производства;
- технологии;
- рецепты;
- компоненты;
- доки;
- экипажи;
- опыт эксплуатации;
- автоматизация;
- редкие материалы;
- отчёты и знания.

Новый корабль должен означать, что мир игрока дорос до него.

## 7 рангов кораблей

Ранги не только увеличивают цифры, а меняют тип задач и цену эксплуатации.

```yaml
ship_ranks:
  rank_1_skiff:
    name: "Катер"
    scale: "1 пилот, ручное управление, короткие рейсы"
    doctrine: "учит действию"
    typical_crew: 1
    typical_cargo: 10-40
    typical_range_km: 5-20
    dock_need: "любой причал"
    description: "Личный инструмент пилота, почти продолжение рук."

  rank_2_small:
    name: "Малый корабль"
    scale: "1-3 человека, первые роли, локальная логистика"
    doctrine: "учит специализации"
    typical_crew: 1-3
    typical_cargo: 40-120
    typical_range_km: 20-60
    dock_need: "малый док"
    description: "Первый корабль, который строится сетью, а не выдаётся судьбой."

  rank_3_medium:
    name: "Средний корабль"
    scale: "4-10 человек, рабочая единица сети"
    doctrine: "учит инфраструктуре"
    typical_crew: 4-10
    typical_cargo: 120-450
    typical_range_km: 60-180
    dock_need: "подготовленный док"
    description: "Корабль, вокруг которого уже нужны люди, ремонт и расписание."

  rank_4_large:
    name: "Большой корабль"
    scale: "10-40 человек, дальние маршруты и сложные роли"
    doctrine: "учит сопровождению"
    typical_crew: 10-40
    typical_cargo: 450-1600
    typical_range_km: 180-700
    dock_need: "региональная верфь"
    description: "Судно, которое уже создаёт проблемы, если его не обслуживать."

  rank_5_heavy:
    name: "Тяжёлый корабль"
    scale: "40-120 человек, промышленная или военная платформа"
    doctrine: "учит флотской логистике"
    typical_crew: 40-120
    typical_cargo: 1600-5000
    typical_range_km: 700-1800
    dock_need: "тяжёлая верфь"
    description: "Не просто корабль, а передвижная причина для снабжения."

  rank_6_capital:
    name: "Capital"
    scale: "120-400 человек, стратегическая система"
    doctrine: "учит автономии"
    typical_crew: 120-400
    typical_cargo: 5000-14000
    typical_range_km: 1800-5000
    dock_need: "капитальная верфь"
    description: "Корабль, которому нужен флот, чтобы он мог быть самим собой."

  rank_7_city:
    name: "Корабль-город"
    scale: "400+ человек, мобильная цивилизация"
    doctrine: "учит жить в рейде"
    typical_crew: 400-2000
    typical_cargo: 14000+
    typical_range_km: 5000+
    dock_need: "городская верфь и сеть островов"
    description: "Летящая столица, которая несёт не груз, а способ существовать."
```

## Роли кораблей

Каждая роль проходит 7 рангов. Не все ранги обязаны быть доступны в первой версии, но место под них есть.

```yaml
ship_roles:
  courier: "малые быстрые доставки, документы, люди, редкие грузы"
  freight: "массовая логистика"
  mining: "добыча руды и глыб"
  gas: "сбор облаков и газов"
  leviathan: "наблюдение, охота, биоматериалы"
  repair: "ремонт и обслуживание флота"
  escort: "защита от автоматонов, опасностей и будущих врагов"
  scout: "разведка, карты, наблюдения"
  passenger: "перевозка рабочих, экипажей, пассажиров"
  industrial: "бортовое производство"
  command: "координация флота и экспедиций"
  ice: "высотные айсберги и сублиматы"
  archaeology: "руины, взлом, предтечи"
  supply: "флотское снабжение, топливо, ремонтные запасы"
  tug: "буксиры, швартовка, тяжёлые перемещения"
```

## Матрица кораблей: 15 ролей x 7 рангов

Итого базовая сетка: 105 корабельных позиций. Часть может быть пустой на ранних этапах, но матрица показывает, куда растёт игра.

| Роль | R1 Катер | R2 Малый | R3 Средний | R4 Большой | R5 Тяжёлый | R6 Capital | R7 Корабль-город |
|---|---|---|---|---|---|---|---|
| Курьер | Искра | Ласточка | Стриж | Почтовый клипер | Дальняя весть | Архивный гонец | Летучая канцелярия |
| Грузовик | Ручеёк | МАУ-1 Веретено | МАУ-2 Носильщик | МАУ-3 Дальник | Тяжёлый караван | Грузовая матка | Склад-город |
| Рудодобыча | Камнеклюв | Камнеклёв | Скальный жнец | Рудный буксир | Глыбодёр | Горная база | Летучий рудный город |
| Газосбор | Баночка | Облачная банка | Сифон | Газовый траулер | Конденсаторная баржа | Облачная фабрика | Атмосферный комбинат |
| Левиафаны | Наблюдатель | Крючник | Линехват | Гарпунный корвет | Китобойная артель | Биологическая база | Левиафановый институт |
| Ремонт | Заплатка | Мастерок | Заплатник | Ремонтный тендер | Полевая верфь | Восстановительная станция | Летучий ремонтный город |
| Эскорт | Сторожок | Клинок | Щитник | Грозовой корвет | Конвойный бастион | Охранная платформа | Крепость сопровождения |
| Разведка | Глазок | Ветерок | Дальний глаз | Картограф | Штормовой визир | Небесная обсерватория | Архив неба |
| Пассажиры/вахта | Лавка | Вахтовик | Смена | Рабочий перевозчик | Жилой клипер | Вахтовая база | Летучая слобода |
| Промышленный | Верстак | Малая мастерская | Цеховик | Фабричная палуба | Промышленная баржа | Фабричная станция | Завод-город |
| Командный | Сигнальщик | Диспетчер | Узел | Дальний журнал | Экспедиционный штаб | Командная станция | Горизонт |
| Лёд/сублиматы | Ледомер | Ледокрючник | Холодный траулер | Айсберговая баржа | Сублиматный комбайн | Ледяная база | Высотный ледовый город |
| Руины/археология | Щуп | Взломщик | Археолог | Руинный корвет | Предтечевый траулер | Архивная станция | Город-раскоп |
| Флотское снабжение | Канистра | Сухпай | Топливщик | Снабженец | Караванная база | Матка снабжения | Тыловой город |
| Буксир/док | Крючок | Причальщик | Доковый буксир | Тяжёлый буксир | Монтажный титан | Верфевой тягач | Двигатель города |

## Прогрессия ролей

### Курьер

Курьерская линия нужна, чтобы маленькие быстрые задачи не исчезали даже в большой игре.

```yaml
courier_progression:
  rank_1: { ship: "Искра", unlock: start, function: "доставить письмо, бумагу, малый груз" }
  rank_2: { ship: "Ласточка", unlock: tech_route_basics, function: "быстрые островные поручения" }
  rank_3: { ship: "Стриж", unlock: tech_alcohol_engine, function: "срочные детали, медикаменты, пилоты" }
  rank_4: { ship: "Почтовый клипер", unlock: tech_barometric_tables, function: "межрегиональная корреспонденция и rare-грузы" }
  rank_5: { ship: "Дальняя весть", unlock: tech_high_altitude_navigation, function: "связь с высокими форпостами" }
  rank_6: { ship: "Архивный гонец", unlock: tech_precursor_interfaces, function: "безопасная перевозка артефактов и ключей" }
  rank_7: { ship: "Летучая канцелярия", unlock: tech_city_ship_core, function: "мобильный административный центр экспедиции" }
```

Ключевые цифры: скорость, дальность, малая заметность, сохранность груза, точность маршрута.

### Грузовик

Грузовики - кровь мира. Их задача не быть красивыми, а делать сеть устойчивой.

```yaml
freight_progression:
  rank_1: { ship: "Ручеёк", unlock: start, function: "малые доставки между соседними островами" }
  rank_2: { ship: "МАУ-1 Веретено", unlock: tech_load_bearing_hull, function: "первый построенный грузовик" }
  rank_3: { ship: "МАУ-2 Носильщик", unlock: tech_steam_engine, function: "средние маршруты и тяжёлые материалы" }
  rank_4: { ship: "МАУ-3 Дальник", unlock: tech_prepared_dock, function: "дальние островные цепочки" }
  rank_5: { ship: "Тяжёлый караван", unlock: tech_route_dispatch, function: "массовое снабжение регионов" }
  rank_6: { ship: "Грузовая матка", unlock: tech_capital_shipyard, function: "флотский склад и снабжение рейда" }
  rank_7: { ship: "Склад-город", unlock: tech_city_ship_core, function: "плавающая логистическая столица" }
```

Ключевые цифры: груз, погрузка, дальность, топливо, экипаж, износ.

### Рудодобыча

Добыча руды вводит сырьё, дробную переработку и риск работы рядом с глыбами.

```yaml
mining_progression:
  rank_1: { ship: "Камнеклюв", unlock: tech_mining, function: "учебный захват малых глыб" }
  rank_2: { ship: "Камнеклёв", unlock: tech_ore_crushing, function: "первый рабочий добытчик" }
  rank_3: { ship: "Скальный жнец", unlock: tech_mining_grabber_tension, function: "стабильная добыча средних глыб" }
  rank_4: { ship: "Рудный буксир", unlock: tech_deep_vein_reading, function: "буксировка крупных глыб к хабу" }
  rank_5: { ship: "Глыбодёр", unlock: tech_heavy_cranes, function: "разбор больших полей" }
  rank_6: { ship: "Горная база", unlock: tech_capital_shipyard, function: "координация добывающих групп" }
  rank_7: { ship: "Летучий рудный город", unlock: tech_city_ship_core, function: "экспедиционная добыча и переработка на месте" }
```

Ключевые цифры: mining_rate, груз руды, прочность захвата, аварийность, переработка на борту.

### Газосбор

Газовая линия открывает облака как ресурс и связывает мир с химией.

```yaml
gas_progression:
  rank_1: { ship: "Баночка", unlock: tech_gas_condensation, function: "сбор учебных проб" }
  rank_2: { ship: "Облачная банка", unlock: tech_gas_tank_compaction, function: "малые газовые рейсы" }
  rank_3: { ship: "Сифон", unlock: tech_gas_membrane_filters, function: "выборочная фракция газов" }
  rank_4: { ship: "Газовый траулер", unlock: tech_react_batch_sensors, function: "опасные облака и дальние поля" }
  rank_5: { ship: "Конденсаторная баржа", unlock: tech_gas_preservation_chambers, function: "массовый сбор редких газов" }
  rank_6: { ship: "Облачная фабрика", unlock: tech_capital_shipyard, function: "сбор и переработка в рейде" }
  rank_7: { ship: "Атмосферный комбинат", unlock: tech_city_ship_core, function: "мобильный химико-газовый город" }
```

Ключевые цифры: gas_harvest_rate, gas_tank_capacity, утечки, риск партии, фракционный выход.

### Левиафаны

Линия начинается с наблюдения и только потом становится охотой.

```yaml
leviathan_progression:
  rank_1: { ship: "Наблюдатель", unlock: tech_leviathan_observation, function: "получает данные без боя" }
  rank_2: { ship: "Крючник", unlock: tech_harpoon_hunting, function: "малые гарпуны и приманки" }
  rank_3: { ship: "Линехват", unlock: tech_hunt_harpoon_tension, function: "охота на малых левиафанов" }
  rank_4: { ship: "Гарпунный корвет", unlock: tech_hunt_behavior_observation, function: "опасная охота с командой" }
  rank_5: { ship: "Китобойная артель", unlock: tech_biochemical_processing, function: "охота и разделка рядом с целью" }
  rank_6: { ship: "Биологическая база", unlock: tech_capital_shipyard, function: "наблюдения, охота, биолаборатория" }
  rank_7: { ship: "Левиафановый институт", unlock: tech_city_ship_core, function: "мобильная школа биологии и охоты" }
```

Ключевые цифры: observation_gain, hunting_power, crew_safety, bio_yield, сохранность органов.

### Ремонт

Ремонтные корабли превращают большие рейды из азартной вылазки в систему.

```yaml
repair_progression:
  rank_1: { ship: "Заплатка", unlock: tech_repair_workshop, function: "малые аварийные комплекты" }
  rank_2: { ship: "Мастерок", unlock: tech_hull_field_patch_standards, function: "ремонт малых кораблей" }
  rank_3: { ship: "Заплатник", unlock: tech_prepared_dock, function: "ремонт в маршруте" }
  rank_4: { ship: "Ремонтный тендер", unlock: tech_auto_diagnostic_network, function: "поддержка группы кораблей" }
  rank_5: { ship: "Полевая верфь", unlock: tech_assy_heavy_cranes, function: "крупный ремонт без столицы" }
  rank_6: { ship: "Восстановительная станция", unlock: tech_capital_shipyard, function: "ремонт capital-флота" }
  rank_7: { ship: "Летучий ремонтный город", unlock: tech_city_ship_core, function: "полный цикл обслуживания экспедиции" }
```

Ключевые цифры: repair_rate, ремонтные запасы, crew_required, automation, dock_slots.

### Эскорт

Эскорт нужен не для превращения игры в шутер, а для защиты долгих процессов.

```yaml
escort_progression:
  rank_1: { ship: "Сторожок", unlock: tech_def_light_turret_drills, function: "отпугивание мелких угроз" }
  rank_2: { ship: "Клинок", unlock: tech_basic_electricity, function: "первый боевой корпус" }
  rank_3: { ship: "Щитник", unlock: tech_convoy_spacing, function: "защита конвоев" }
  rank_4: { ship: "Грозовой корвет", unlock: tech_aero_lower_storm_protocols, function: "охрана в опасной погоде" }
  rank_5: { ship: "Конвойный бастион", unlock: tech_def_automaton_weakpoints, function: "защита от автоматонов" }
  rank_6: { ship: "Охранная платформа", unlock: tech_capital_shipyard, function: "зональная оборона рейда" }
  rank_7: { ship: "Крепость сопровождения", unlock: tech_city_ship_core, function: "оборонительная доктрина города" }
```

Ключевые цифры: escort_defense, route_safety, armor, repair_dependency, боезапас.

### Разведка

Разведка создаёт знания, маршруты и будущие цели.

```yaml
scout_progression:
  rank_1: { ship: "Глазок", unlock: tech_route_basics, function: "обзор ближайших островов" }
  rank_2: { ship: "Ветерок", unlock: tech_barometric_tables, function: "поиск облаков и глыб" }
  rank_3: { ship: "Дальний глаз", unlock: tech_nav_optical_rangefinding, function: "дальние разведрейсы" }
  rank_4: { ship: "Картограф", unlock: tech_nav_storm_prediction, function: "карты маршрутов и бурь" }
  rank_5: { ship: "Штормовой визир", unlock: tech_high_altitude_navigation, function: "работа на границе опасных слоёв" }
  rank_6: { ship: "Небесная обсерватория", unlock: tech_precursor_interfaces, function: "наблюдения дальних зон" }
  rank_7: { ship: "Архив неба", unlock: tech_city_ship_core, function: "мобильная картографическая цивилизация" }
```

Ключевые цифры: sensor_range, scout_speed, observation_gain, navigation_error, idle_report_detail.

### Пассажиры и вахта

Эта линия нужна, чтобы большие корабли и острова были человеческими системами.

```yaml
passenger_progression:
  rank_1: { ship: "Лавка", unlock: tech_crew_ration_planning, function: "перевозка 1-2 работников" }
  rank_2: { ship: "Вахтовик", unlock: tech_crew_watch_rotations, function: "рабочие смены на острова" }
  rank_3: { ship: "Смена", unlock: tech_crew_compact_quarters, function: "регулярная ротация экипажей" }
  rank_4: { ship: "Рабочий перевозчик", unlock: tech_crew_medical_shifts, function: "дальние вахты и медицина" }
  rank_5: { ship: "Жилой клипер", unlock: tech_crew_leisure_salons, function: "снижение усталости больших рейдов" }
  rank_6: { ship: "Вахтовая база", unlock: tech_capital_shipyard, function: "жизнь и ротация для fleet-операций" }
  rank_7: { ship: "Летучая слобода", unlock: tech_city_ship_core, function: "население корабля-города" }
```

Ключевые цифры: crew_fatigue_rate, morale_drain, passenger_capacity, medicine_use.

### Промышленный

Промышленные корабли начинают как мастерские и заканчивают как заводы в небе.

```yaml
industrial_progression:
  rank_1: { ship: "Верстак", unlock: tech_mfg_jig_fixtures, function: "ремесленные операции" }
  rank_2: { ship: "Малая мастерская", unlock: tech_basic_mechanisms, function: "простые компоненты в рейде" }
  rank_3: { ship: "Цеховик", unlock: tech_mfg_parallel_benches, function: "малое производство на борту" }
  rank_4: { ship: "Фабричная палуба", unlock: tech_auto_power_distribution, function: "серийные детали в рейде" }
  rank_5: { ship: "Промышленная баржа", unlock: tech_conv_continuous_feed, function: "переработка и производство" }
  rank_6: { ship: "Фабричная станция", unlock: tech_capital_shipyard, function: "мобильный завод рейда" }
  rank_7: { ship: "Завод-город", unlock: tech_city_ship_core, function: "автономная промышленность цивилизации" }
```

Ключевые цифры: factory_slots, throughput, worker_fatigue, defect_chance, onboard_storage.

### Командный

Командные корабли повышают не добычу сами, а способность держать сложную систему.

```yaml
command_progression:
  rank_1: { ship: "Сигнальщик", unlock: tech_log_route_dispatch, function: "координация 1 маршрута" }
  rank_2: { ship: "Диспетчер", unlock: tech_auto_relay_autopilot, function: "локальная сеть островов" }
  rank_3: { ship: "Узел", unlock: tech_auto_fleet_command_protocols, function: "малые группы кораблей" }
  rank_4: { ship: "Дальний журнал", unlock: tech_high_altitude_navigation, function: "дальние idle-рейды" }
  rank_5: { ship: "Экспедиционный штаб", unlock: tech_precursor_interfaces, function: "многоцелевая экспедиция" }
  rank_6: { ship: "Командная станция", unlock: tech_capital_shipyard, function: "управление capital-флотом" }
  rank_7: { ship: "Горизонт", unlock: tech_city_ship_core, function: "командование летучей цивилизацией" }
```

Ключевые цифры: command_slots, idle_report_detail, route_slots, automation, fleet_efficiency.

### Лёд и сублиматы

Эта роль открывает самый верхний слой мира.

```yaml
ice_progression:
  rank_1: { ship: "Ледомер", unlock: tech_ice_harpoon_anchoring, function: "поиск и проба льда" }
  rank_2: { ship: "Ледокрючник", unlock: tech_iceberg_harvesting, function: "малые айсберги" }
  rank_3: { ship: "Холодный траулер", unlock: tech_ice_thermal_cutting, function: "серийный сбор льда" }
  rank_4: { ship: "Айсберговая баржа", unlock: tech_ice_sublimate_sorting, function: "массовый лёд и первые редкости" }
  rank_5: { ship: "Сублиматный комбайн", unlock: tech_sublimate_systems, function: "целенаправленные сублиматы" }
  rank_6: { ship: "Ледяная база", unlock: tech_capital_shipyard, function: "высотная ледовая операция" }
  rank_7: { ship: "Высотный ледовый город", unlock: tech_city_ship_core, function: "постоянная цивилизация в ледяной зоне" }
```

Ключевые цифры: cold_resistance, ice_harvest_rate, sublimates_yield, claudium_lift_efficiency.

### Руины и археология

Руинная линия даёт рецепты, данные, автоматизацию и предтечевые ключи.

```yaml
archaeology_progression:
  rank_1: { ship: "Щуп", unlock: tech_automaton_ruins, function: "поиск слабых сигналов" }
  rank_2: { ship: "Взломщик", unlock: tech_blueprint_damage_reconstruction, function: "малые руинные контейнеры" }
  rank_3: { ship: "Археолог", unlock: tech_research_archive_indexing, function: "дешифровка архивов" }
  rank_4: { ship: "Руинный корвет", unlock: tech_def_automaton_weakpoints, function: "опасные руины с охраной" }
  rank_5: { ship: "Предтечевый траулер", unlock: tech_precursor_interfaces, function: "массовый вывоз артефактов" }
  rank_6: { ship: "Архивная станция", unlock: tech_capital_shipyard, function: "исследования прямо в рейде" }
  rank_7: { ship: "Город-раскоп", unlock: tech_city_ship_core, function: "долгая экспедиция в мёртвую цивилизацию" }
```

Ключевые цифры: artifact_decode_chance, blueprint_runs, research_speed, automaton_risk, cargo_security.

### Флотское снабжение

Флотское снабжение - причина, почему огромный рейд может длиться 10 часов и больше.

```yaml
supply_progression:
  rank_1: { ship: "Канистра", unlock: tech_cargo_standard_containers, function: "топливо и пайки для малых групп" }
  rank_2: { ship: "Сухпай", unlock: tech_crew_ration_planning, function: "локальное снабжение экипажей" }
  rank_3: { ship: "Топливщик", unlock: tech_engine_heat_recovery, function: "доставка топлива в рейде" }
  rank_4: { ship: "Снабженец", unlock: tech_log_priority_orders, function: "многоуровневые грузы конвоя" }
  rank_5: { ship: "Караванная база", unlock: tech_auto_cargo_sorting_ship, function: "поддержка большого флота" }
  rank_6: { ship: "Матка снабжения", unlock: tech_capital_shipyard, function: "стратегические запасы экспедиции" }
  rank_7: { ship: "Тыловой город", unlock: tech_city_ship_core, function: "самодостаточный тыл летучей цивилизации" }
```

Ключевые цифры: fuel_capacity, warehouse_capacity, route_slots, dock_slots, fleet_endurance.

### Буксир и доковые операции

Буксиры нужны, чтобы огромные объекты и корабли не были абстрактным UI.

```yaml
tug_progression:
  rank_1: { ship: "Крючок", unlock: tech_dock_marks, function: "швартовка малых судов" }
  rank_2: { ship: "Причальщик", unlock: tech_crane_signals, function: "помощь у островных доков" }
  rank_3: { ship: "Доковый буксир", unlock: tech_prepared_dock, function: "средние корабли и глыбы" }
  rank_4: { ship: "Тяжёлый буксир", unlock: tech_assy_heavy_cranes, function: "крупные модули и аварийные корабли" }
  rank_5: { ship: "Монтажный титан", unlock: tech_capital_shipyard, function: "capital-блоки и строительные операции" }
  rank_6: { ship: "Верфевой тягач", unlock: tech_city_ship_core, function: "сборка кораблей-городов" }
  rank_7: { ship: "Двигатель города", unlock: tech_city_ship_core, function: "маневровая система города как отдельный флот" }
```

Ключевые цифры: tow_power, docking_speed, assembly_parallelism, accident_chance.

## Компонентная архитектура кораблей

Каждый корабль собирается из:

```yaml
ship_component_slots:
  core_frame: "каркас ранга"
  lift_system: "клавдиевый контур/решётка"
  propulsion: "двигатель"
  control: "навигация и управление"
  role_module: "модуль роли"
  logistics_module: "трюм, баллоны, ремонтные запасы или экипажный блок"
  safety_module: "защита, аварийность, герметизация"
  automation_module: "ручное, релейное, автоматонное, предтечевое"
```

### Каркасы по рангам

| id | Название | Ранг | Производится | Основные ресурсы |
|---|---|---:|---|---|
| comp_frame_r1_skiff | Катерная рама | 1 | tools_workshop | wood, cloth, tools |
| comp_frame_r2_small | Малая силовая рама | 2 | shipyard_small | metal, beams, hull_mineral |
| comp_frame_r3_medium | Средняя силовая рама | 3 | shipyard_medium | metal, armor_mineral, mechanisms |
| comp_frame_r4_large | Большой силовой каркас | 4 | regional_shipyard | reinforced_beams, armor_plate, claudium |
| comp_frame_r5_heavy | Тяжёлый несущий каркас | 5 | heavy_shipyard | capital_beams, vibration_dampers, automation_parts |
| comp_frame_r6_capital | Capital-каркас | 6 | capital_assembly_yard | capital_frame_blocks, precursor_calibrators |
| comp_frame_r7_city | Городской силовой остов | 7 | city_shipyard | city_frame_blocks, mass_balance_core, claudium_grid |

### Подъёмные системы по рангам

| id | Название | Ранг | Производится | Основные ресурсы |
|---|---|---:|---|---|
| comp_lift_r1_bag | Простейший клавдиевый мешок | 1 | capital_greenhaven | cloth, claudium_trace |
| comp_lift_r2_loop | Малый клавдиевый контур | 2 | mechanics_workshop | claudium, conductive_mineral |
| comp_lift_r3_grid | Малая распределительная решётка | 3 | mechanics_workshop | claudium, voltite, mechanisms |
| comp_lift_r4_storm_grid | Штормовая решётка | 4 | regional_shipyard | resonant_claudium, armor_mineral |
| comp_lift_r5_high_altitude | Высотный контур | 5 | high_altitude_outpost | dry_sublimate, claudium, heatproof_membrane |
| comp_lift_r6_capital_grid | Capital-решётка | 6 | capital_assembly_yard | lift_control_core, resonant_claudium |
| comp_lift_r7_city_lattice | Городская подъёмная сеть | 7 | city_shipyard | mass_balance_core, claudium_grid_calibrator |

### Двигательные системы по рангам

| id | Название | Ранг | Производится | Основные ресурсы |
|---|---|---:|---|---|
| comp_engine_r1_prop | Ручной винтовой привод | 1 | tools_workshop | wood, tools |
| comp_engine_r2_alcohol | Спиртовой двигатель | 2 | mechanics_workshop | mechanisms, alcohol, metal |
| comp_engine_r3_steam | Паровой двигатель | 3 | mechanics_workshop | boiler_parts, coal, water |
| comp_engine_r4_turbine | Паротурбинный привод | 4 | regional_engine_yard | turbine_blades, thermal_mineral |
| comp_engine_r5_compound | Составной маршевый двигатель | 5 | heavy_shipyard | gearbox_block, fuel_mix, automation_parts |
| comp_engine_r6_capital | Capital-машинная секция | 6 | capital_assembly_yard | turbine_block, power_distribution_core |
| comp_engine_r7_city | Городская двигательная сеть | 7 | city_shipyard | city_engine_blocks, central_machine_supervision |

### Ролевые модули

| Роль | R1-R2 модуль | R3-R4 модуль | R5-R7 модуль |
|---|---|---|---|
| Курьер | courier_satchel, sealed_mailbox | fast_dispatch_room | archive_vault_network |
| Груз | small_cargo_module | standardized_hold | city_logistics_grid |
| Руда | mining_grabber | ore_tow_claw | mobile_mining_deck |
| Газ | gas_pod | fractioning_tanks | atmospheric_processing_deck |
| Левиафаны | observation_rack | harpoon_deck | bio_hunting_complex |
| Ремонт | patch_kit_bay | repair_bay | field_shipyard_deck |
| Эскорт | light_turret_mount | convoy_shield_deck | defense_command_network |
| Разведка | scout_scope | cartography_room | sky_archive_observatory |
| Пассажиры | bench_cabin | crew_rotation_deck | habitation_district |
| Промышленный | workbench_bay | factory_deck | industrial_city_deck |
| Командный | signal_post | command_bridge | capital_command_core |
| Лёд | ice_probe | iceberg_harpoon_deck | sublimates_processing_deck |
| Руины | ruin_probe | archive_lab | precursor_integration_deck |
| Снабжение | fuel_crates | fleet_supply_hold | strategic_supply_city |
| Буксир | tow_hook | heavy_winch | capital_mounting_cranes |

## Технологические линии для 7 рангов

### Ранговые открытия кораблей

```yaml
rank_unlock_technologies:
  tech_ship_rank_1_skiffs:
    name: "Катерные корпуса"
    unlocks: [rank_1_skiff]
    requires: [start]
    description: "Летать можно раньше, чем строить."

  tech_ship_rank_2_small_ships:
    name: "Малые корабли"
    unlocks: [rank_2_small, shipyard_small]
    requires: [tech_load_bearing_hull, tech_alcohol_engine, tech_claudium_loop]
    description: "Первый ранг, который сеть игрока собирает сама."

  tech_ship_rank_3_medium_ships:
    name: "Средние корабли"
    unlocks: [rank_3_medium, shipyard_medium]
    requires: [tech_prepared_dock, tech_steam_engine, tech_basic_electricity]
    description: "Корабль становится рабочей единицей островной сети."

  tech_ship_rank_4_large_ships:
    name: "Большие корабли"
    unlocks: [rank_4_large, regional_shipyard]
    requires: [tech_route_dispatch, tech_turbine_drive, tech_crew_watch_rotations]
    description: "Размер начинает требовать сопровождения и планового обслуживания."

  tech_ship_rank_5_heavy_ships:
    name: "Тяжёлые корабли"
    unlocks: [rank_5_heavy, heavy_shipyard]
    requires: [tech_automaton_salvage, tech_relay_logic, tech_heavy_cranes]
    description: "Корабль становится платформой, которую надо снабжать как остров."

  tech_ship_rank_6_capital_ships:
    name: "Capital-корабли"
    unlocks: [rank_6_capital, capital_assembly_yard]
    requires: [tech_capital_shipyard, tech_sublimate_systems, tech_precursor_interfaces]
    description: "Флот начинает вращаться вокруг одного большого решения."

  tech_ship_rank_7_city_ships:
    name: "Корабли-города"
    unlocks: [rank_7_city, city_shipyard]
    requires: [tech_city_ship_core, tech_expedition_society, tech_city_lift_network]
    description: "Игрок строит не транспорт, а летающую форму цивилизации."
```

### Ролевые технологии

Каждая роль имеет 7 ступеней. Ступень роли открывает новый корабль этой роли и улучшает связанную цифру.

```yaml
role_tech_pattern:
  tech_role_<role>_1:
    unlocks: "R1 корабль роли"
    bonus: "+5% к базовой эффективности роли"
  tech_role_<role>_2:
    unlocks: "R2 корабль роли"
    bonus: "+5% к эффективности, первый специализированный модуль"
  tech_role_<role>_3:
    unlocks: "R3 корабль роли"
    bonus: "+1 слот роли или +10% к профильному объёму"
  tech_role_<role>_4:
    unlocks: "R4 корабль роли"
    bonus: "дальний режим, снижение износа роли"
  tech_role_<role>_5:
    unlocks: "R5 корабль роли"
    bonus: "флотская версия роли"
  tech_role_<role>_6:
    unlocks: "R6 корабль роли"
    bonus: "capital-режим роли"
  tech_role_<role>_7:
    unlocks: "R7 корабль роли"
    bonus: "городской режим роли"
```

Примеры конкретизации:

```yaml
role_tech_examples:
  mining:
    1: { id: tech_role_mining_1, name: "Рудный захват", bonus: "+5% mining_rate", unlocks: ["Камнеклюв"] }
    2: { id: tech_role_mining_2, name: "Малые добывающие суда", bonus: "+5% mining_rate, -2% износ захвата", unlocks: ["Камнеклёв"] }
    3: { id: tech_role_mining_3, name: "Средние рудные операции", bonus: "+10% ore_hold", unlocks: ["Скальный жнец"] }
    4: { id: tech_role_mining_4, name: "Буксировка глыб", bonus: "+15% tow_power для руды", unlocks: ["Рудный буксир"] }
    5: { id: tech_role_mining_5, name: "Тяжёлая разработка глыб", bonus: "+1 добывающая группа", unlocks: ["Глыбодёр"] }
    6: { id: tech_role_mining_6, name: "Capital-горная база", bonus: "+20% координация добытчиков", unlocks: ["Горная база"] }
    7: { id: tech_role_mining_7, name: "Летучий рудный город", bonus: "переработка руды в рейде", unlocks: ["Летучий рудный город"] }

  command:
    1: { id: tech_role_command_1, name: "Сигнальные порядки", bonus: "+1 простой приказ", unlocks: ["Сигнальщик"] }
    2: { id: tech_role_command_2, name: "Островская диспетчерская", bonus: "+1 route_slot", unlocks: ["Диспетчер"] }
    3: { id: tech_role_command_3, name: "Групповое командование", bonus: "+1 command_slot", unlocks: ["Узел"] }
    4: { id: tech_role_command_4, name: "Дальняя рейсовая канцелярия", bonus: "+idle_report_detail", unlocks: ["Дальний журнал"] }
    5: { id: tech_role_command_5, name: "Экспедиционный штаб", bonus: "+fleet_efficiency", unlocks: ["Экспедиционный штаб"] }
    6: { id: tech_role_command_6, name: "Capital-командование", bonus: "+3 command_slots", unlocks: ["Командная станция"] }
    7: { id: tech_role_command_7, name: "Городская власть рейда", bonus: "управление city-сетью", unlocks: ["Горизонт"] }
```

## Производственные площадки для компонентов

| Площадка | Эпоха | Делает |
|---|---|---|
| tools_workshop | island_industry | R1 компоненты, инструменты, навигация, простые модули |
| mechanics_workshop | mining_mechanics | R2-R3 двигатели, механизмы, захваты, клапаны |
| shipyard_small | engines_routes | R2 корабли и малые рамы |
| shipyard_medium | engines_routes | R3 корабли, средние рамы, экипажные блоки |
| regional_shipyard | clouds_chemistry | R4 корабли, большие каркасы, газовые/химические модули |
| heavy_shipyard | automatons | R5 корабли, тяжёлые каркасы, автоматонные стойки |
| capital_assembly_yard | expedition | R6 корабли и capital-блоки |
| city_shipyard | expedition | R7 корабли-города |
| gas_hub | clouds_chemistry | газовые капсулы, баллоны, реакционные камеры |
| bio_lab | leviathan_bio | биотрюмы, мембраны, органические смазки, фильтры |
| relay_workshop | automatons | автопилоты, риги, реле, автоматизация |
| ice_harvest_dock | high_altitude | ледовые модули, сублиматные узлы |
| archive_lab | automatons | интерфейсы предтеч, дешифровка, рецепты |

## Рецептурные шаблоны кораблей

### Шаблон корабля R1

```yaml
ship_recipe_rank_1:
  facility: tools_workshop
  duration_min: 10-30
  components:
    comp_frame_r1_skiff: 1
    comp_lift_r1_bag: 1
    comp_engine_r1_prop: 1
    role_module_r1: 1
  resources: { wood: 5-12, cloth: 2-6, tools: 0-1, claudium: 0.2-1 }
```

### Шаблон корабля R2

```yaml
ship_recipe_rank_2:
  facility: shipyard_small
  duration_min: 40-120
  components:
    comp_frame_r2_small: 1
    comp_lift_r2_loop: 1
    comp_engine_r2_alcohol: 1
    comp_control_basic: 1
    role_module_r2: 1
    logistics_module_r2: 1
  resources: { metal: 15-40, mechanisms: 2-8, claudium: 2-6, tools: 2-8 }
```

### Шаблон корабля R3

```yaml
ship_recipe_rank_3:
  facility: shipyard_medium
  duration_min: 120-360
  components:
    comp_frame_r3_medium: 1
    comp_lift_r3_grid: 1
    comp_engine_r3_steam: 1
    comp_control_gyro: 1
    role_module_r3: 1
    safety_module_r3: 1
  resources: { metal: 60-150, mechanisms: 10-30, coal: 20-80, claudium: 8-20, crew_supplies: 10-40 }
```

### Шаблон корабля R4

```yaml
ship_recipe_rank_4:
  facility: regional_shipyard
  duration_min: 360-900
  components:
    comp_frame_r4_large: 1
    comp_lift_r4_storm_grid: 1
    comp_engine_r4_turbine: 1
    comp_control_optical: 1
    role_module_r4: 2
    safety_module_r4: 1
    automation_module_r4: 1
  resources: { metal: 200-500, mechanisms: 50-120, gas_parts: 20-80, claudium: 30-80, workers: 20-60 }
```

### Шаблон корабля R5

```yaml
ship_recipe_rank_5:
  facility: heavy_shipyard
  duration_min: 900-1800
  components:
    comp_frame_r5_heavy: 1
    comp_lift_r5_high_altitude: 1
    comp_engine_r5_compound: 1
    comp_control_relay: 1
    role_module_r5: 3
    automation_module_r5: 2
    crew_module_r5: 1
  resources: { metal: 700-1600, automation_parts: 80-200, bio_materials: 20-100, claudium: 100-250, crew_supplies: 100-300 }
```

### Шаблон корабля R6

```yaml
ship_recipe_rank_6:
  facility: capital_assembly_yard
  duration_min: 1800-6000
  assembly_stages: 5-9
  components:
    comp_frame_r6_capital: 1
    comp_lift_r6_capital_grid: 1
    comp_engine_r6_capital: 1
    comp_control_capital_bridge: 1
    role_module_r6: 4
    automation_module_r6: 3
    habitation_module_r6: 2
    dock_module_r6: 1
  resources: { metal: 3000-9000, automation_parts: 400-1200, sublimates: 80-300, precursor_parts: 10-80, crew_supplies: 1000-4000 }
```

### Шаблон корабля R7

```yaml
ship_recipe_rank_7:
  facility: city_shipyard
  duration_min: 6000+
  assembly_stages: 10-20
  components:
    comp_frame_r7_city: 1
    comp_lift_r7_city_lattice: 1
    comp_engine_r7_city: 1
    city_role_districts: 5-12
    city_habitation_districts: 3-8
    city_dock_districts: 2-6
    city_automation_core: 1
    precursor_city_interface: 1
  resources: { metal: 20000+, automation_parts: 3000+, sublimates: 1000+, precursor_parts: 200+, food: 10000+, water: 20000+, medicine: 2000+ }
```

## Технологии компонентов

Технологии компонентов открывают не корабль напрямую, а строительный блок, который потом используется разными ролями.

```yaml
component_technologies:
  frames:
    - tech_frame_r2_load_bearing
    - tech_frame_r3_stressed_truss
    - tech_frame_r4_storm_spine
    - tech_frame_r5_heavy_lattice
    - tech_frame_r6_capital_keel
    - tech_frame_r7_city_skeleton
  lift:
    - tech_lift_r2_claudium_loop
    - tech_lift_r3_distribution_grid
    - tech_lift_r4_storm_grid
    - tech_lift_r5_high_altitude_phase
    - tech_lift_r6_capital_grid
    - tech_lift_r7_city_lattice
  engines:
    - tech_engine_r2_alcohol
    - tech_engine_r3_steam
    - tech_engine_r4_turbine
    - tech_engine_r5_compound
    - tech_engine_r6_capital
    - tech_engine_r7_city_network
  control:
    - tech_control_basic_navigation
    - tech_control_gyro_block
    - tech_control_optical_bridge
    - tech_control_relay_bridge
    - tech_control_precursor_interface
    - tech_control_city_command_core
```

## Ресурсные последствия рангов

Каждый следующий ранг должен требовать не просто больше того же, а новый слой экономики.

| Ранг | Новый bottleneck |
|---:|---|
| R1 | базовые товары, дерево, ткань |
| R2 | металл, механизмы, клавдий |
| R3 | экипаж, уголь/пар, подготовленный док |
| R4 | газы, химия, дальние маршруты |
| R5 | автоматонные компоненты, риги, флотское снабжение |
| R6 | сублиматы, предтечевые интерфейсы, capital-доки |
| R7 | городская логистика, население, автономия, редкие ядра |

## Острова и верфи как развитие мира

Корабли нельзя строить только в столице. Сеть островов должна постепенно получать специализации.

```yaml
shipbuilding_island_progression:
  greenhaven:
    builds: [R1, R2, research, recycling]
    later_builds: [R6, R7]
    reason: "столица помнит старые стандарты"
  tool_island:
    builds: [R1, R2 components, tools, repair_kits]
    reason: "остров ручной промышленности"
  mining_hub:
    builds: [frames, metal, R3 mining, R3 freight]
    reason: "тяжёлые материалы рядом с переработкой"
  gas_hub:
    builds: [gas_modules, reaction_chambers, R3-R5 gas ships]
    reason: "герметичность и химическая культура"
  leviathan_guild:
    builds: [harpoons, bioholds, hunter ships, biofilters]
    reason: "органика требует отдельной школы"
  automaton_outpost:
    builds: [rigs, autopilots, automation_modules, archaeology ships]
    reason: "автоматизация рождается из руин"
  high_altitude_outpost:
    builds: [ice_modules, high_altitude_loops, R4-R6 ice ships]
    reason: "ледяные системы нельзя полноценно делать внизу"
  regional_shipyard:
    builds: [R4 large ships]
    reason: "первый крупный промышленный узел"
  heavy_shipyard:
    builds: [R5 heavy ships]
    reason: "корабль становится платформой"
  capital_yard:
    builds: [R6 capital ships, city blocks]
    reason: "верфь, вокруг которой работает мир"
  city_yard:
    builds: [R7 city ships]
    reason: "последняя сборка цивилизации"
```

## 10 итераций расширения

### Итерация 1: ранги

Добавлена 7-ранговая шкала, чтобы корабли росли не только численно, но и по типу забот.

### Итерация 2: роли

Роли расширены до 15 задач. Это даёт 105 корабельных позиций и убирает страх пустоты.

### Итерация 3: экономика рангов

Каждый ранг получил свой bottleneck: металл, экипаж, химия, автоматоны, сублиматы, городская логистика.

### Итерация 4: компонентные слоты

Корабль стал не рецептом, а набором слотов: каркас, подъём, двигатель, контроль, роль, логистика, безопасность, автоматизация.

### Итерация 5: производственные площадки

Каждая группа компонентов получила место производства: мастерская, малая верфь, средняя верфь, газовый хаб, биолаборатория, релейная мастерская, ледовый форпост.

### Итерация 6: ролевые технологии

Каждая роль получила технологическую лестницу I-VII, которая открывает корабли и улучшает профильные цифры.

### Итерация 7: ранговые технологии

Добавлены технологии открытия рангов кораблей, чтобы новый масштаб требовал зрелости мира.

### Итерация 8: рецептурные шаблоны

Для каждого ранга добавлен шаблон сборки. Это позволит быстро сгенерировать конкретные рецепты.

### Итерация 9: островная специализация

Верфи и производственные острова связаны с типами кораблей. Кораблестроение теперь растит карту мира.

### Итерация 10: проверяемость

Система стала пригодной для большого теста:

- у каждого корабля есть ранг, роль и серия;
- у каждого ранга есть технология открытия;
- у каждой роли есть 7 технологий;
- у каждого ранга есть компонентный шаблон;
- у каждого компонентного семейства есть место производства;
- у каждого нового ранга есть новый экономический bottleneck.

## Углубление v2.1: реестры для будущих CSV

Этот слой нужен, чтобы матрица перестала быть красивой таблицей и стала почти готовым набором конфигов.

Принцип: корабль не должен открываться одной технологией и строиться одним рецептом. Он должен быть местом пересечения:

- ранга;
- роли;
- серии корпуса;
- ролевого модуля;
- производственной площадки;
- ресурсного слоя;
- опыта, который игрок уже накопил в соответствующей деятельности.

### Формула корабля

```yaml
ship_formula:
  id: ship_<role>_r<rank>
  rank: R1-R7
  role: courier|freight|mining|gas|leviathan|repair|escort|scout|passenger|industrial|command|ice|archaeology|supply|tug
  requires_techs:
    - tech_ship_rank_<rank>
    - tech_role_<role>_<rank>
    - tech_component_set_<rank>
  requires_experience:
    - exp_<role>_operation
    - exp_shipbuilding
    - exp_design_from_previous_rank
  recipe:
    template: recipe_ship_rank_<rank>
    role_module: comp_role_<role>_r<rank>
    role_surcharge: recipe_surcharge_<role>_r<rank>
  result:
    ship_id: ship_<role>_r<rank>
    base_configuration: true
    upgrade_nodes: [hull, lift, engine, control, role, safety]
    rig_slots: "0 на R1, 1-2 на R2-R3, 3-5 на R4-R5, 6+ на R6-R7"
```

### Профильные цифры ролей

Эти цифры потом должны попасть в большой тест: у каждой роли есть свой главный показатель, вторичный показатель и свой риск.

| Роль | Главная цифра | Вторичная цифра | Риск/цена роли | Что чувствует игрок |
|---|---|---|---|---|
| Курьер | route_speed | cargo_safety | потеря срочности | быстрые маленькие решения остаются важными |
| Груз | cargo_capacity | loading_speed | топливо и износ | сеть начинает дышать регулярными потоками |
| Руда | mining_rate | ore_hold | аварии захвата | сырье появляется не из меню, а из рейсов |
| Газ | gas_harvest_rate | leak_resistance | порча партии | облака становятся полезной, но нервной добычей |
| Левиафаны | observation_gain | bio_yield | травмы и повреждения | сначала изучить, потом рисковать |
| Ремонт | repair_rate | spare_efficiency | расход деталей | большой флот можно держать вдали от столицы |
| Эскорт | threat_suppression | convoy_stability | боекомплект | idle-рейс становится защищённым процессом |
| Разведка | survey_speed | detection_range | потеря времени | карта мира открывается наблюдением |
| Пассажиры | crew_transfer_rate | comfort | усталость экипажа | люди становятся ресурсом сети |
| Промышленный | onboard_production | worker_fatigue | аварии производства | корабль начинает делать работу острова |
| Командный | command_slots | report_quality | перегруз диспетчеризации | игрок управляет системой, а не каждым катером |
| Лёд | ice_capture_rate | cold_resistance | обледенение | высота становится отдельной экономикой |
| Руины | hacking_power | artifact_safety | активация защиты | знание добывается как редкий груз |
| Снабжение | fleet_endurance | resupply_speed | порча запасов | рейд живёт не героизмом, а тылом |
| Буксир | tow_power | dock_precision | аварии стыковки | тяжёлые объекты становятся подвижными |

### Полный реестр ролевых технологий

Каждая строка задаёт 7 технологий одной роли. Это ещё не финальные названия, но уже рабочие узлы графа.

| Роль | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---|---|---|---|---|---|
| Курьер | tech_role_courier_1: Маршрутные записки | tech_role_courier_2: Срочные поручения | tech_role_courier_3: Опечатанные грузы | tech_role_courier_4: Межрегиональная почта | tech_role_courier_5: Высотная связь | tech_role_courier_6: Архивная перевозка | tech_role_courier_7: Канцелярия рейда |
| Груз | tech_role_freight_1: Малый трюм | tech_role_freight_2: Стандартные ящики | tech_role_freight_3: Весовая укладка | tech_role_freight_4: Дальние караваны | tech_role_freight_5: Контейнерная палуба | tech_role_freight_6: Флотский склад | tech_role_freight_7: Городская сортировка |
| Руда | tech_role_mining_1: Рудный захват | tech_role_mining_2: Малые добывающие суда | tech_role_mining_3: Средние рудные операции | tech_role_mining_4: Буксировка глыб | tech_role_mining_5: Тяжёлая разработка | tech_role_mining_6: Capital-горная база | tech_role_mining_7: Летучий рудный город |
| Газ | tech_role_gas_1: Пробные конденсаторы | tech_role_gas_2: Малые газовые банки | tech_role_gas_3: Мембранная фракция | tech_role_gas_4: Опасные облака | tech_role_gas_5: Массовая конденсация | tech_role_gas_6: Облачная фабрика | tech_role_gas_7: Атмосферный комбинат |
| Левиафаны | tech_role_leviathan_1: Полевые наблюдения | tech_role_leviathan_2: Малые гарпуны | tech_role_leviathan_3: Натяжение линей | tech_role_leviathan_4: Поведенческие карты | tech_role_leviathan_5: Разделочная артель | tech_role_leviathan_6: Биологическая база | tech_role_leviathan_7: Институт живых материалов |
| Ремонт | tech_role_repair_1: Аварийная заплата | tech_role_repair_2: Полевой инструмент | tech_role_repair_3: Маршрутный ремонт | tech_role_repair_4: Тендерные бригады | tech_role_repair_5: Полевая верфь | tech_role_repair_6: Capital-восстановление | tech_role_repair_7: Ремонтный город |
| Эскорт | tech_role_escort_1: Сторожевые посты | tech_role_escort_2: Конвойные курсы | tech_role_escort_3: Щитовое сопровождение | tech_role_escort_4: Дальняя охрана | tech_role_escort_5: Конвойный бастион | tech_role_escort_6: Охранная платформа | tech_role_escort_7: Крепость сопровождения |
| Разведка | tech_role_scout_1: Ручная съёмка | tech_role_scout_2: Маршрутные приметы | tech_role_scout_3: Оптическая мачта | tech_role_scout_4: Картографический стол | tech_role_scout_5: Штормовой визир | tech_role_scout_6: Небесная обсерватория | tech_role_scout_7: Архив неба |
| Пассажиры | tech_role_passenger_1: Пассажирская лавка | tech_role_passenger_2: Вахтовый отсек | tech_role_passenger_3: Смена экипажа | tech_role_passenger_4: Рабочие перевозки | tech_role_passenger_5: Жилой клипер | tech_role_passenger_6: Вахтовая база | tech_role_passenger_7: Летучая слобода |
| Промышленный | tech_role_industrial_1: Бортовой верстак | tech_role_industrial_2: Малая мастерская | tech_role_industrial_3: Механический цех | tech_role_industrial_4: Фабричная палуба | tech_role_industrial_5: Промышленная баржа | tech_role_industrial_6: Фабричная станция | tech_role_industrial_7: Завод-город |
| Командный | tech_role_command_1: Сигнальные порядки | tech_role_command_2: Островская диспетчерская | tech_role_command_3: Групповое командование | tech_role_command_4: Дальний журнал | tech_role_command_5: Экспедиционный штаб | tech_role_command_6: Capital-командование | tech_role_command_7: Городская власть рейда |
| Лёд | tech_role_ice_1: Ледовые пробы | tech_role_ice_2: Малый ледовый крюк | tech_role_ice_3: Термотрюм | tech_role_ice_4: Айсберговый гарпун | tech_role_ice_5: Сублиматный комбайн | tech_role_ice_6: Ледяная база | tech_role_ice_7: Высотный ледовый город |
| Руины | tech_role_archaeology_1: Архивный щуп | tech_role_archaeology_2: Замковые цилиндры | tech_role_archaeology_3: Дешифровальные столы | tech_role_archaeology_4: Руинный протокол | tech_role_archaeology_5: Предтечевый трал | tech_role_archaeology_6: Архивная станция | tech_role_archaeology_7: Город-раскоп |
| Снабжение | tech_role_supply_1: Канистры и сухпаи | tech_role_supply_2: Малый снабженец | tech_role_supply_3: Топливные рейсы | tech_role_supply_4: Флотские нормы | tech_role_supply_5: Караванная база | tech_role_supply_6: Матка снабжения | tech_role_supply_7: Тыловой город |
| Буксир | tech_role_tug_1: Причальные крюки | tech_role_tug_2: Малые лебёдки | tech_role_tug_3: Доковый буксир | tech_role_tug_4: Тяжёлая буксировка | tech_role_tug_5: Монтажные тяги | tech_role_tug_6: Верфевой тягач | tech_role_tug_7: Двигатель города |

### Реестр ролевых модулей

Ролевой модуль является главным отличием кораблей одного ранга. Остальные слоты часто совпадают.

| Роль | R1 | R2 | R3 | R4 | R5 | R6 | R7 |
|---|---|---|---|---|---|---|---|
| Курьер | comp_role_courier_r1_mail_satchel | comp_role_courier_r2_sealed_box | comp_role_courier_r3_dispatch_locker | comp_role_courier_r4_fast_sort_room | comp_role_courier_r5_high_altitude_pouch | comp_role_courier_r6_archive_vault | comp_role_courier_r7_mobile_chancery |
| Груз | comp_role_freight_r1_hand_hold | comp_role_freight_r2_standard_crates | comp_role_freight_r3_weighted_hold | comp_role_freight_r4_long_route_hold | comp_role_freight_r5_container_deck | comp_role_freight_r6_fleet_warehouse | comp_role_freight_r7_city_sorting_grid |
| Руда | comp_role_mining_r1_probe_claw | comp_role_mining_r2_ore_grabber | comp_role_mining_r3_crusher_pod | comp_role_mining_r4_boulder_tow_claw | comp_role_mining_r5_heavy_drill_deck | comp_role_mining_r6_mining_coordination_deck | comp_role_mining_r7_mobile_ore_city |
| Газ | comp_role_gas_r1_sample_flask | comp_role_gas_r2_cloud_pod | comp_role_gas_r3_membrane_fractioner | comp_role_gas_r4_reaction_safe_tanks | comp_role_gas_r5_mass_condenser_deck | comp_role_gas_r6_cloud_factory_deck | comp_role_gas_r7_atmospheric_combine |
| Левиафаны | comp_role_leviathan_r1_observation_rack | comp_role_leviathan_r2_small_harpoon | comp_role_leviathan_r3_line_tensioner | comp_role_leviathan_r4_hunting_deck | comp_role_leviathan_r5_bio_butchery_deck | comp_role_leviathan_r6_biology_base | comp_role_leviathan_r7_living_materials_institute |
| Ремонт | comp_role_repair_r1_patch_locker | comp_role_repair_r2_tool_bench | comp_role_repair_r3_route_repair_bay | comp_role_repair_r4_tender_workshop | comp_role_repair_r5_field_yard | comp_role_repair_r6_capital_restoration_deck | comp_role_repair_r7_repair_city |
| Эскорт | comp_role_escort_r1_watch_post | comp_role_escort_r2_light_mount | comp_role_escort_r3_shield_deck | comp_role_escort_r4_convoy_battery | comp_role_escort_r5_defense_bastion | comp_role_escort_r6_guard_platform_core | comp_role_escort_r7_fortress_command |
| Разведка | comp_role_scout_r1_survey_board | comp_role_scout_r2_route_marks | comp_role_scout_r3_optic_mast | comp_role_scout_r4_map_table | comp_role_scout_r5_storm_visor | comp_role_scout_r6_sky_observatory | comp_role_scout_r7_sky_archive |
| Пассажиры | comp_role_passenger_r1_bench | comp_role_passenger_r2_watch_cabin | comp_role_passenger_r3_crew_rotation_deck | comp_role_passenger_r4_worker_ferry_deck | comp_role_passenger_r5_habitable_clip | comp_role_passenger_r6_watch_base | comp_role_passenger_r7_flying_settlement |
| Промышленный | comp_role_industrial_r1_workbench | comp_role_industrial_r2_small_shop | comp_role_industrial_r3_machine_room | comp_role_industrial_r4_factory_deck | comp_role_industrial_r5_industrial_barge_deck | comp_role_industrial_r6_factory_station_core | comp_role_industrial_r7_factory_city |
| Командный | comp_role_command_r1_signal_post | comp_role_command_r2_dispatch_table | comp_role_command_r3_group_command_node | comp_role_command_r4_far_log_room | comp_role_command_r5_expedition_hq | comp_role_command_r6_capital_command_station | comp_role_command_r7_horizon_city_core |
| Лёд | comp_role_ice_r1_ice_probe | comp_role_ice_r2_ice_hook | comp_role_ice_r3_heated_hold | comp_role_ice_r4_iceberg_harpoon | comp_role_ice_r5_sublimate_harvester | comp_role_ice_r6_ice_base_deck | comp_role_ice_r7_high_altitude_ice_city |
| Руины | comp_role_archaeology_r1_archive_probe | comp_role_archaeology_r2_lock_cylinder_tools | comp_role_archaeology_r3_cipher_table | comp_role_archaeology_r4_ruin_protocol_room | comp_role_archaeology_r5_precursor_trawl | comp_role_archaeology_r6_archive_station_core | comp_role_archaeology_r7_excavation_city |
| Снабжение | comp_role_supply_r1_canister_rack | comp_role_supply_r2_small_resupply_hold | comp_role_supply_r3_fuel_tender_tanks | comp_role_supply_r4_fleet_norms_store | comp_role_supply_r5_caravan_base_hold | comp_role_supply_r6_supply_mothership_core | comp_role_supply_r7_rear_city_grid |
| Буксир | comp_role_tug_r1_mooring_hook | comp_role_tug_r2_small_winch | comp_role_tug_r3_dock_tug_gear | comp_role_tug_r4_heavy_winch_deck | comp_role_tug_r5_mounting_tensioners | comp_role_tug_r6_shipyard_tractor_core | comp_role_tug_r7_city_engine_link |

### Рецептурные надбавки ролей

Рецепт корабля ранга даёт базовый костяк, а роль добавляет профильные материалы.

| Роль | R1-R2 надбавка | R3-R4 надбавка | R5-R7 надбавка | Логика |
|---|---|---|---|---|
| Курьер | paper, cloth, wax | optics, sealed_cases | precursor_keys, archive_plates | важна сохранность малого груза |
| Груз | wood, cloth, crates | beams, mechanisms, standardized_containers | automated_sorters, city_conveyors | важна масса и скорость погрузки |
| Руда | tools, metal_teeth | crushers, reinforced_cable | heavy_cranes, ore_processors | добыча требует силовых узлов |
| Газ | cloth, simple_valves | membranes, pressure_valves, filters | inert_chambers, reaction_safety_cores | газ требует герметичности |
| Левиафаны | paper, bait, rope | harpoons, medical_supplies, cold_storage | bio_labs, preservation_gas, sensor_organs | сначала наблюдение, потом биоматериалы |
| Ремонт | tools, patches | spare_parts, diagnostic_tools | repair_automatons, self_calibration | ремонт ест детали, но экономит корабли |
| Эскорт | weapon_parts, armor_scraps | turrets, fire_control, ammo | tactical_matrix, armor_belts | защита рейда требует боезапаса |
| Разведка | paper, compass_parts | lenses, barometers, map_tables | sensor_cores, old_sky_maps | разведка превращает полёт в знание |
| Пассажиры | cloth, food | cabins, water, medicine | comfort_salons, morale_blocks | люди требуют не только места |
| Промышленный | tools, small_machines | machine_tools, boilers, workshops | factory_controllers, automated_lines | производство на борту требует энергии |
| Командный | signal_flags, lamps | radio_relays, report_tables | command_cores, machine_language | управление флотом требует информации |
| Лёд | hooks, warm_cloth | heated_holds, harpoons, insulation | sublimates, cold_labs, pressure_locks | высота требует тепла и герметичности |
| Руины | probes, paper | cipher_tools, access_keys | precursor_interfaces, archive_vaults | руины требуют взлома и сохранности |
| Снабжение | canisters, sacks | fuel_tanks, ration_rooms | strategic_stores, automated_distribution | рейд держится на тыле |
| Буксир | rope, hooks | winches, pressure_blocks | capital_cranes, mass_calibrators | тяжёлое движение требует точности |

### Производства, которые должны появиться под эти рецепты

```yaml
new_facility_backlog:
  shipyard_small:
    production_type: Assembly
    purpose: "R2 корабли, малые рамы, малые доковые операции"
  shipyard_medium:
    production_type: Assembly
    purpose: "R3 корабли, средние рамы, первые экипажные блоки"
  regional_shipyard:
    production_type: Assembly
    purpose: "R4 корабли и большие силовые каркасы"
  heavy_shipyard:
    production_type: Assembly
    purpose: "R5 корабли, тяжёлые каркасы, автоматонные стойки"
  capital_assembly_yard:
    production_type: Assembly
    purpose: "R6 корабли, capital-секции, многоэтапная сборка"
  city_shipyard:
    production_type: Assembly
    purpose: "R7 корабли-города, сборка из районов"
  engine_yard:
    production_type: Manufacturing
    purpose: "двигатели R2-R6, редукторы, валы, турбины"
  claudium_lift_shop:
    production_type: Reaction
    purpose: "клавдиевые контуры, решётки, стабилизаторы подъёма"
  pressure_and_gas_shop:
    production_type: Reaction
    purpose: "баллоны, мембраны, газовые предохранители"
  bio_material_lab:
    production_type: Processing
    purpose: "левиафановые ткани, мембраны, смазки, фильтры"
  relay_automation_shop:
    production_type: Manufacturing
    purpose: "реле, автопилоты, риги, диагностические узлы"
  precursor_archive_lab:
    production_type: Conversion
    purpose: "артефакты в знания, редкие рецепты, интерфейсы"
  sublimate_cold_lab:
    production_type: Processing
    purpose: "лёд в воду, сублиматы и high-tier порошки"
```

### Связь рангов с островами

| Ранг | Что должно быть развито в мире | Почему нельзя раньше |
|---:|---|---|
| R1 | столица, базовая мастерская | это учебный уровень |
| R2 | еда, дерево, инструменты, малый док | игрок впервые собирает снабжение |
| R3 | металл, уголь, механизмы, средний док | корабль требует цепочку руды |
| R4 | газовый хаб, химия, экипажные перевозки | дальние рейсы требуют реакций и людей |
| R5 | автоматонный форпост, релейная мастерская, тяжёлая верфь | без автоматизации игрок утонет в ручном труде |
| R6 | сублиматы, предтечи, capital-док, флот снабжения | capital-корабль является проектом региона |
| R7 | несколько зрелых регионов, городская верфь, постоянный тыл | корабль-город является проектом цивилизации |

### Физика рангов

В мире Wild Wind ранги кораблей являются физическими порогами.

- R1-R2 держатся на простом клавдиевом контуре, почти без распределения массы.
- R3 требует решётку: подъёмная сила должна быть распределена по корпусу, иначе корабль ломает сам себя.
- R4 требует антиштормовую стабилизацию, потому что площадь корпуса начинает ловить ветер как парус.
- R5 требует автоматонную диагностику: человек уже не успевает слышать все трещины и клапаны.
- R6 требует capital-каркас и обитаемые палубы: экипаж становится городским процессом.
- R7 требует распределённую клавдиевую сеть: это уже не корабль с двигателем, а летающая инфраструктура.

### 10 итераций связности: что именно добавлено

Это подробный протокол, чтобы завтра можно было продолжить не с ощущения "много букв", а с понятного слоя.

| Итерация | Что расширено | Что стало связаннее | Что потом тестировать |
|---:|---|---|---|
| 1 | 7 рангов кораблей | каждый ранг имеет новый экономический bottleneck | у каждого ранга есть tech, recipe, dock |
| 2 | 15 ролей | каждая роль имеет 7 корабельных позиций | 105 слотов кораблей заполнены |
| 3 | профильные цифры ролей | роль теперь улучшает конкретную механику | у роли есть main_stat и risk |
| 4 | ролевые технологии | 105 role-tech узлов дают прогрессию как навыки | все tech_role_* уникальны |
| 5 | ролевые модули | корабли одного ранга отличаются модулем, а не копипастой корпуса | у каждого ship есть role_module |
| 6 | рецептурные надбавки | роль добавляет ресурсы поверх рангового шаблона | recipe_surcharge существует для каждой роли |
| 7 | производственные площадки | корабли требуют островную экономику, а не один верстак | facility существует и соответствует эпохе |
| 8 | связь с островами | новый ранг означает новый уровень цивилизации | island_stage >= required_stage |
| 9 | физика рангов | рост корабля объяснён клавдием, массой, экипажем и автоматизацией | лор не противоречит цифрам |
| 10 | большой тест | появились проверяемые инварианты | BigTest может ругаться на пустые узлы |

### Минимальный вертикальный срез по новой системе

Чтобы не пытаться сразу сделать все 105 кораблей, первый настоящий игровой срез можно собрать так:

```yaml
vertical_slice_ships:
  freight:
    ranks: [R1, R2, R3]
    reason: "показывает доставку, груз и островную сеть"
  mining:
    ranks: [R1, R2, R3]
    reason: "показывает сырьё, руду и переработку"
  command:
    ranks: [R1, R2]
    reason: "показывает управление маршрутами"
  repair:
    ranks: [R1, R2]
    reason: "показывает износ и поддержку idle-сессий"
```

Минимальный набор даст:

- 10 реальных кораблей;
- 10 ролевых технологий;
- 3 ранговые технологии;
- 3 верфи/мастерские;
- 20-30 рецептов компонентов;
- понятный BigTest на связность.

## Правила для будущего большого теста

```yaml
big_test_rules_v2:
  ship_matrix:
    expect_roles: 15
    expect_ranks_per_role: 7
    expect_ship_slots: 105
  ranks:
    every_rank_has_unlock_technology: true
    every_rank_has_recipe_template: true
    every_rank_has_new_bottleneck: true
  roles:
    every_role_has_7_role_techs: true
    every_role_has_profile_stat: true
    every_role_has_role_module_family: true
  components:
    every_ship_uses_component_slots: [core_frame, lift_system, propulsion, control, role_module]
    every_component_family_has_facility: true
    every_component_family_has_epoch: true
  economy:
    no_rank_uses_resource_before_epoch: true
    no_ship_without_dock_requirement: true
    no_capital_without_crew_and_supply_requirement: true
```

## Что завтра править первым

1. Утвердить или переименовать 7 рангов.
2. Утвердить список ролей: оставить 15, сократить или добавить.
3. Выбрать 2-3 роли для первого вертикального среза.
4. Для этих ролей расписать реальные корабли R1-R3 в CSV.
5. Не трогать R6-R7 баланс до тех пор, пока первые ранги не стали понятными.

Предложение первого вертикального среза:

- грузовик;
- рудодобыча;
- командный/диспетчерский;
- ремонт.

Эти четыре роли уже дадут ощущение живой сети: возить, добывать, управлять, чинить.
