# Wild Wind: полный черновой граф контента

Дата фиксации: 2026-05-17

Статус: предварительный предконфиг. Это не финальный баланс и не канон названий. Документ нужен, чтобы увидеть полную связанную картину: ресурсы, технологии, острова, производства, рецепты, компоненты кораблей и сами корабли.

Главная задача: завтра пройтись по этому графу и решить, что оставить, что переименовать, что расширить, что выкинуть.

## Как читать документ

Каждая сущность получает:

- `id` - будущий стабильный ID для конфига;
- `epoch` - эпоха появления;
- `family` или `area` - семейство;
- `produced_at` - где производится или добывается;
- `unlocked_by` - технология или стадия острова;
- `used_for` - зачем нужна;
- `description` - художественное объяснение.

Числа здесь приблизительные. Они нужны не для баланса, а чтобы понять порядок величин.

## Эпохи

```yaml
epochs:
  supply:
    name: "Эпоха снабжения"
    unlocks: "Первые маршруты, склады, простые островные потребности."
  island_industry:
    name: "Эпоха островной промышленности"
    unlocks: "Первые заводы, островские ангары, локальные пилоты."
  mining_mechanics:
    name: "Эпоха механики и руды"
    unlocks: "Добыча глыб, дробилки, металлы, механизмы."
  engines_routes:
    name: "Эпоха двигателей и дальних рейсов"
    unlocks: "Новые корабли, узлы I-V, дальние маршруты."
  clouds_chemistry:
    name: "Эпоха облаков и химии"
    unlocks: "Газосбор, реакции, катализаторы, топливные смеси."
  leviathan_bio:
    name: "Эпоха левиафанов и биоматериалов"
    unlocks: "Охота, наблюдения, органика, биолаборатории."
  automatons:
    name: "Эпоха автоматонов"
    unlocks: "Руины, salvage, риги, автоматизация."
  high_altitude:
    name: "Высотная эпоха"
    unlocks: "Ледяная зона, айсберги, сублиматы, высотные контуры."
  expedition:
    name: "Экспедиционная эпоха"
    unlocks: "Корабли-городa, автономные рейды, capital-системы."
```

## Ресурсные семейства

### Магистральная логистика

Эти ресурсы игрок возит часто. Они должны быть видимыми, понятными и хорошо группироваться в интерфейсе.

```yaml
core_logistics:
  count_target: 40-60
  resources:
    food:
      name: "Еда"
      epoch: supply
      produced_at: [island_food_garden, farmstead_generation]
      used_for: [island_needs, crew_supply, worker_efficiency, long_routes]
      description: "Самый простой способ сказать острову, что цивилизация ещё жива."
    water:
      name: "Вода"
      epoch: supply
      produced_at: [island_water_condensers, cloud_water_condensing]
      used_for: [crew_supply, chemistry, medicine, steam_systems]
      description: "В мире облаков вода всё равно остаётся тяжёлой логистикой."
    cloth:
      name: "Ткань"
      epoch: supply
      produced_at: [textile_looms]
      used_for: [clothing, filters, sails, crew_comfort, early_island_needs]
      description: "Тёплая ткань делает далёкий рейс чуть менее враждебным."
    wood:
      name: "Древесина"
      epoch: supply
      produced_at: [timber_yards]
      used_for: [early_ships, crates, tools, fuel, island_buildings]
      description: "Первый строительный материал слабого, но живого мира."
    coal:
      name: "Уголь"
      epoch: supply
      produced_at: [coal_mires, charcoal_kilns]
      used_for: [steam_power, smelting, heating, reactions]
      description: "Грязное тепло, на котором держится ранняя промышленность."
    paper:
      name: "Бумага"
      epoch: supply
      produced_at: [paper_mill]
      used_for: [scouting, records, research, route_reports]
      description: "Бумага превращает полёт в знание, а знание в технологию."
    tools:
      name: "Инструменты"
      epoch: island_industry
      produced_at: [tools_workshop]
      used_for: [island_upgrades, production_buildings, maintenance]
      description: "Первый ресурс, который говорит: остров уже не просто выживает."
    metal:
      name: "Металл"
      epoch: mining_mechanics
      produced_at: [small_foundry]
      used_for: [mechanisms, ship_components, docks, weapons]
      description: "Грубая основа всего, что перестаёт быть деревянным."
    mechanisms:
      name: "Механизмы"
      epoch: mining_mechanics
      produced_at: [mechanics_workshop]
      used_for: [engines, factories, docks, ship_nodes]
      description: "Шестерни, валы и клапаны, в которых мир учится повторять движение."
    fuel_mix:
      name: "Топливная смесь"
      epoch: clouds_chemistry
      produced_at: [chemical_reactor]
      used_for: [advanced_engines, reactions, distant_routes]
      description: "Более капризное, но более плотное сердце дальних рейсов."
    medicine:
      name: "Медикаменты"
      epoch: supply
      produced_at: [herbal_station, bio_lab]
      used_for: [crew_health, expedition_recovery, island_needs]
      description: "Когда маршрут длится часы, здоровье становится грузом."
    repair_kits:
      name: "Ремонтные комплекты"
      epoch: engines_routes
      produced_at: [repair_workshop]
      used_for: [ship_maintenance, island_hangars, expedition_support]
      description: "Маленькие ящики, которые отделяют аварийную посадку от возвращения домой."
    claudium:
      name: "Клавдий"
      epoch: mining_mechanics
      produced_at: [claudite_processing, cloud_trace_processing]
      used_for: [lift_systems, ship_upgrades, high_altitude_contours]
      description: "Вещество, из-за которого мир вообще держится над бурей."
    workers:
      name: "Рабочие"
      epoch: island_industry
      produced_at: [capital_population, island_housing]
      used_for: [factories, docks, expedition_crews]
      description: "Не ресурс в моральном смысле, но главный ограничитель любой промышленности."
```

### Минералы и руды

Руда добывается как сырьё. В Processing-цикле она даёт несколько минералов дробными долями, которые копятся до целых единиц.

```yaml
ore_and_minerals:
  count_target: 70-90
  families:
    hull_ore:
      role: "каркасы, рамы, корпуса"
      first_tech: tech_mining
      example_ores: [gromite, frameite, archstone, grey_skeleton]
      example_minerals: [hull_mineral, frame_mineral, beam_mineral]
    armor_ore:
      role: "броня, защитные кожухи, тяжёлые доки"
      first_tech: tech_reinforced_plating
      example_ores: [bastionite, shieldstone, blackplate]
      example_minerals: [armor_mineral, impact_mineral]
    light_ore:
      role: "лёгкие корпуса, высотные корабли"
      first_tech: tech_light_frame
      example_ores: [aerolite, featherstone, dry_slate]
      example_minerals: [light_mineral, porous_mineral]
    conductive_ore:
      role: "реле, катушки, приборы"
      first_tech: tech_basic_electricity
      example_ores: [voltite, sparkstone, coil_ore]
      example_minerals: [conductive_mineral, contact_mineral]
    thermal_ore:
      role: "котлы, печи, теплообменники"
      first_tech: tech_steam_engine
      example_ores: [caldorite, furnace_stone, red_slate]
      example_minerals: [thermal_mineral, furnace_lining]
    precision_ore:
      role: "точная механика, оптика, навигация"
      first_tech: tech_precision_tools
      example_ores: [mechanite, calibrum, clockstone]
      example_minerals: [precision_mineral, calibration_grain]
    claudium_ore:
      role: "подъёмные контуры, высотные системы"
      first_tech: tech_claudium_loop
      example_ores: [claudite, blue_claudite, resonant_claudite]
      example_minerals: [claudium_crystal, resonant_claudium]
```

### Газы

Газы добываются из облачных полей. Часть газов идёт в топливо, часть в реакции, часть в приборы и high-tier материалы.

```yaml
cloud_gases:
  count_target: 60-80
  families:
    fuel_gases:
      examples: [fulgrin, thermogen, sootgas, brightgas, stormgas]
      used_for: [fuel_mix, lamps, burners, advanced_reactions]
    reaction_gases:
      examples: [reactin, oxidant, reductin, sulfogen, nitrogenit]
      used_for: [chemistry, explosives, fertilizers, metal_treatment]
    corrosive_gases:
      examples: [mordant, acid_vapor, alkaline_mist, chlorite_gas]
      used_for: [etching, cleaning, bleaching, weapons]
    inert_gases:
      examples: [sedron, quietgas, void_gas, dry_gas, preserving_gas]
      used_for: [safe_reactions, storage, precision_devices]
    instrument_gases:
      examples: [lumin, sparkin, arcgas, echoran, visorin]
      used_for: [lamps, relays, sensors, optics, navigation]
    high_tier_gases:
      examples: [storm_ichor, mirror_vapor, black_fulgrin, white_inert, grey_stabilin]
      used_for: [endgame_reactions, capital_systems, precursor_research]
```

### Левиафановые материалы

Левиафаны дают не один ресурс, а биологический набор. Наблюдение может быть полезнее охоты на ранних стадиях.

```yaml
leviathan_materials:
  count_target: 140-200
  families:
    hides_membranes:
      examples: [leviathan_hide, thick_hide, gas_membrane, heatproof_membrane, acidproof_membrane]
      used_for: [flexible_armor, gas_bags, filters, reaction_chambers]
    tendons_fibers:
      examples: [leviathan_tendon, power_sinew, nerve_fiber, armored_fiber]
      used_for: [cables, dampers, harpoons, sensitive_devices]
    bones_shells:
      examples: [leviathan_bone, porous_bone, horn_material, resonant_bone]
      used_for: [light_composites, filters, handles, stabilizers]
    fats_oils:
      examples: [leviathan_fat, fine_bio_oil, heatproof_lubricant, black_leviathan_fat]
      used_for: [lubricants, fuel, sealants, precision_mechanics]
    glands_secretions:
      examples: [acid_gland, poison_gland, enzyme_gland, electric_gland, gas_gland]
      used_for: [chemistry, medicine, weapons, batteries]
    organs:
      examples: [swim_bladder, resonant_organ, sensory_organ, heat_exchange_organ, nerve_node]
      used_for: [gas_systems, navigation, sensors, cooling, high_tier_research]
```

### Автоматоны и предтечи

Автоматоны дают автоматизацию. Предтечи дают знания, ключи, калибраторы и ограниченные рецепты.

```yaml
automaton_and_precursor:
  automaton_components_target: 140-220
  precursor_artifacts_target: 200-400
  automaton_families:
    mechanics: [gear_block, precision_gear, planetary_reducer, bearing_node, balance_flywheel]
    actuators: [servo_joint, heavy_servo, steering_actuator, hydraulic_piston]
    sensors: [optical_sensor, rangefinder_eye, pressure_sensor, acoustic_resonator]
    logic: [logic_relay, relay_matrix, command_module, factory_controller]
    power: [power_circuit, capacitor_node, load_regulator, emergency_starter]
    repair: [diagnostic_module, self_repair_grid, crack_monitor, service_protocol]
  precursor_families:
    archives: [archive_plate, encrypted_data_cylinder, old_sky_map, crash_log]
    calibrators: [mass_balance_calibrator, claudium_grid_calibrator, pressure_standard]
    control_cores: [navigation_core, lift_control_core, factory_control_core, capital_command_core]
    access_keys: [precursor_access_key, archive_key, service_key, capital_identity_key]
    intact_nodes: [mass_balance_core, old_navigation_heart, safe_reaction_core, central_precursor_node]
```

## Острова

Остров - это не только точка на карте, а узел прогрессии. У каждого острова есть тип, базовая production-выработка, потребности, стадии развития, склад, доки и возможные производства.

### Общие стадии острова

```yaml
island_stages:
  stage_0_sleeping:
    name: "Тусклый остров"
    requirements: "Нет стабильного снабжения."
    effects: "Производит мало, склад маленький, дока нет."
  stage_1_supplied:
    name: "Снабжённый остров"
    requirements: "Закрыты первые потребности."
    effects: "+production, склад, простые заявки."
  stage_2_workshop:
    name: "Мастерской остров"
    requirements: "Еда, ткань/вода, инструменты, дерево."
    effects: "Можно строить первые производства."
  stage_3_hangar:
    name: "Остров с ангаром"
    requirements: "Инструменты, механизмы, стартовый корабль из столицы."
    effects: "Локальные пилоты выполняют сбор вокруг острова."
  stage_4_specialized:
    name: "Специализированный узел"
    requirements: "Профильная технология, стабильное снабжение."
    effects: "Остров становится частью промышленной сети."
  stage_5_automated:
    name: "Автоматизированный узел"
    requirements: "Автоматонные компоненты, диспетчерская, релейная сеть."
    effects: "Сам поддерживает маршруты, производство и локальный сбор."
```

### Типы островов

```yaml
island_types:
  capital_greenhaven:
    name: "Гринхейвен"
    base_role: "столица, бесконечные стартовые корабли, исследования, центральный склад"
    stage_focus: [research, starter_ships, ship_recycling, central_orders]
    description: "Торговый узел, где слабая цивилизация ещё помнит, что значит строить будущее."

  food_island:
    name: "Пищевой остров"
    base_production: food
    early_needs: [cloth, water]
    later_needs: [tools, medicine, workers]
    possible_buildings: [farmstead_generation, crew_ration_kitchen, fermenter]
    description: "Остров, который кормит сеть и первым показывает силу взаимного снабжения."

  textile_island:
    name: "Ткацкий остров"
    base_production: cloth
    early_needs: [food, water]
    later_needs: [tools, resin, filters]
    possible_buildings: [textile_looms, filter_workshop, clothing_hall]
    description: "Ткань здесь не роскошь, а инфраструктура тепла, фильтрации и долгих рейсов."

  water_island:
    name: "Водосборный остров"
    base_production: water
    early_needs: [coal, cloth]
    later_needs: [tools, pipes, filters]
    possible_buildings: [water_condensers, steam_condensate_station, cloud_water_dock]
    description: "Остров, который учит игрока, что даже вода в небе требует логистики."

  timber_island:
    name: "Древесный остров"
    base_production: wood
    early_needs: [food, tools]
    later_needs: [coal, mechanisms, workers]
    possible_buildings: [sawmill, charcoal_kiln, beam_yard]
    description: "Первый строительный позвоночник ранней сети."

  coal_island:
    name: "Угольная марь"
    base_production: coal
    early_needs: [food, cloth]
    later_needs: [tools, water, repair_kits]
    possible_buildings: [coal_yard, charcoal_refinery, boiler_supply_depot]
    description: "Грязный, тёплый и незаменимый узел ранней индустрии."

  paper_island:
    name: "Бумажный остров"
    base_production: paper
    early_needs: [wood, water]
    later_needs: [tools, cloth, archives]
    possible_buildings: [paper_mill, route_archive, scout_registry]
    description: "Здесь полёты превращаются в записи, а записи - в исследования."

  tool_island:
    name: "Инструментальный остров"
    base_production: tools
    starts_locked: true
    unlocks_at: stage_2_workshop
    needs_to_build: [food, wood, metal]
    possible_buildings: [tools_workshop, precision_tools_shop, repair_workshop]
    description: "Первый остров, который игрок не просто снабжает, а выбирает и строит."

  mining_hub_island:
    name: "Горный узел"
    base_production: none
    unlocks_by: tech_mining
    needs_to_build: [tools, mechanisms, fuel_mix]
    possible_buildings: [ore_crusher, mineral_yard, small_foundry]
    description: "Не производит сам, но связывает рудные глыбы с промышленностью."

  gas_hub_island:
    name: "Газовый узел"
    base_production: none
    unlocks_by: tech_gas_condensation
    needs_to_build: [pipes, filters, mechanisms, water]
    possible_buildings: [gas_condenser, gas_splitter, chemical_reactor]
    description: "Остров, где облака перестают быть фоном и становятся сырьём."

  leviathan_guild_island:
    name: "Левиафановая артель"
    base_production: none
    unlocks_by: tech_leviathan_observation
    needs_to_build: [paper, medicine, harpoons, cold_storage]
    possible_buildings: [observation_post, hunting_dock, bio_lab]
    description: "Граница между познанием живого мира и его опасной добычей."

  automaton_ruin_outpost:
    name: "Руинный форпост"
    base_production: none
    unlocks_by: tech_automaton_ruins
    needs_to_build: [weapons, tools, paper, mechanisms]
    possible_buildings: [salvage_yard, archive_lab, relay_workshop]
    description: "Место, где новая цивилизация разбирает кости старой."

  high_altitude_ice_outpost:
    name: "Высотный ледовый форпост"
    base_production: none
    unlocks_by: tech_iceberg_harvesting
    needs_to_build: [heating, harpoons, claudium, medicine]
    possible_buildings: [ice_harvest_dock, sublimates_lab, cold_storage]
    description: "Редкая точка, где небо становится холоднее, чем пустота."
```

## Производства и здания

```yaml
facilities:
  farmstead_generation:
    type: Generation
    island_types: [food_island]
    unlocked_by: stage_0_sleeping
    outputs: [food]
    boosted_by: [cloth, water, tools]
    description: "Поля, теплицы и пищевые мастерские, которые оживают от снабжения."

  textile_looms:
    type: Manufacturing
    island_types: [textile_island]
    unlocked_by: tech_island_workshop
    inputs: [fiber, water, tools]
    outputs: [cloth]
    description: "Островная ткацкая линия для одежды, фильтров и парусины."

  water_condensers:
    type: Generation
    island_types: [water_island]
    unlocked_by: stage_1_supplied
    outputs: [water]
    boosted_by: [coal, filters, tools]
    description: "Конденсаторы, которые вытягивают воду из влажных облачных потоков."

  sawmill:
    type: Manufacturing
    island_types: [timber_island]
    unlocked_by: tech_island_workshop
    inputs: [wood, tools]
    outputs: [boards, beams]
    description: "Первое место, где древесина становится строительной геометрией."

  charcoal_kiln:
    type: Processing
    island_types: [timber_island, coal_island]
    unlocked_by: tech_charcoal_burning
    inputs: [wood]
    outputs: [charcoal, resin_trace]
    description: "Грязный, но надёжный мост от дерева к теплу."

  tools_workshop:
    type: Manufacturing
    island_types: [tool_island]
    unlocked_by: tech_toolmaking
    inputs: [metal, wood, food]
    outputs: [tools]
    description: "Первый завод, который строится решением игрока, а не существует изначально."

  ore_crusher:
    type: Processing
    island_types: [mining_hub_island]
    unlocked_by: tech_ore_crushing
    inputs: [ore_unit, fuel_mix]
    outputs: [mineral_fraction_pool]
    description: "Дробит одну единицу руды за цикл и копит минералы до целых единиц."

  small_foundry:
    type: Manufacturing
    island_types: [mining_hub_island]
    unlocked_by: tech_basic_smelting
    inputs: [hull_mineral, coal, tools]
    outputs: [metal]
    description: "Место, где камень впервые становится корпусом."

  mechanics_workshop:
    type: Manufacturing
    island_types: [tool_island, mining_hub_island]
    unlocked_by: tech_basic_mechanisms
    inputs: [metal, tools, precision_mineral]
    outputs: [mechanisms]
    description: "Шестерни и клапаны, без которых дальний рейс остаётся мечтой."

  gas_condenser:
    type: Processing
    island_types: [gas_hub_island]
    unlocked_by: tech_gas_condensation
    inputs: [cloud_charge, fuel_mix]
    outputs: [gas_fraction_pool]
    description: "Переводит облако из красоты в промышленное сырьё."

  chemical_reactor:
    type: Reaction
    island_types: [gas_hub_island]
    unlocked_by: tech_chemical_reactions
    inputs: [reaction_gas, acid, catalyst]
    outputs: [chemical_product]
    description: "Долгие партии, риск срыва и сладкий соблазн ускорения."

  bio_lab:
    type: Processing
    island_types: [leviathan_guild_island]
    unlocked_by: tech_biochemical_processing
    inputs: [leviathan_tissue, medicine, filters]
    outputs: [bio_materials]
    description: "Лаборатория, где охота превращается в материалы, а наблюдения - в понимание."

  salvage_yard:
    type: Processing
    island_types: [automaton_ruin_outpost]
    unlocked_by: tech_automaton_salvage
    inputs: [automaton_wreck, tools]
    outputs: [automaton_components, automaton_data]
    description: "Разборочная площадка для машин, которые пережили свою цивилизацию."

  relay_workshop:
    type: Manufacturing
    island_types: [automaton_ruin_outpost]
    unlocked_by: tech_relay_logic
    inputs: [logic_relay, conductive_mineral, precision_tools]
    outputs: [automation_parts]
    description: "Здесь старые реле учатся работать на новую сеть."

  shipyard_small:
    type: Assembly
    island_types: [capital_greenhaven, tool_island]
    unlocked_by: tech_small_shipbuilding
    inputs: [ship_components]
    outputs: [small_ships]
    description: "Первый док, где корабль становится не подарком столицы, а проектом."

  shipyard_medium:
    type: Assembly
    island_types: [capital_greenhaven, mining_hub_island]
    unlocked_by: tech_prepared_dock
    inputs: [medium_ship_components]
    outputs: [medium_ships]
    description: "Подготовленный док, требующий снабжения, людей и терпения."

  capital_assembly_yard:
    type: Assembly
    island_types: [capital_greenhaven]
    unlocked_by: tech_capital_shipyard
    inputs: [capital_blocks]
    outputs: [capital_ships]
    description: "Не завод, а событие в жизни всей цивилизации."

  ice_harvest_dock:
    type: Processing
    island_types: [high_altitude_ice_outpost]
    unlocked_by: tech_iceberg_harvesting
    inputs: [iceberg_chunk, heat, harpoon_wear]
    outputs: [water, sublimates]
    description: "Гарпуны, холод и редкие вещества из верхнего неба."
```

## Ключевые технологии

```yaml
technologies:
  tech_route_basics:
    name: "Основы маршрутов"
    epoch: supply
    type: organizational
    requires: [paper, completed_delivery_3]
    unlocks: [route_planning, island_need_tracking]
    description: "Линия на карте становится обещанием, а не надеждой."

  tech_island_workshop:
    name: "Островная мастерская"
    epoch: island_industry
    type: engineering_platform
    requires: [food, wood, tools_intro]
    unlocks: [tools_workshop, sawmill, textile_looms]
    description: "Остров получает место, где потребность превращается в производство."

  tech_toolmaking:
    name: "Инструментальное дело"
    epoch: island_industry
    type: production_process
    requires: [metal, wood, production_experience_1]
    unlocks: [recipe_tools_basic, island_stage_2_workshop]
    description: "Инструмент - первый ресурс, который ускоряет почти всё."

  tech_island_hangars:
    name: "Островские ангары"
    epoch: island_industry
    type: engineering_platform
    requires: [tools, wood, route_experience_1]
    unlocks: [local_pilots, island_collection_jobs, starter_ship_delivery]
    description: "Остров перестаёт ждать игрока и делает первые самостоятельные рейсы."

  tech_mining:
    name: "Шахтёрство"
    epoch: mining_mechanics
    type: fundamental
    requires: [tools, scouting_reports, route_experience_2]
    unlocks: [mining_zones, ore_units, mining_hub_island]
    description: "Летающие глыбы перестают быть пейзажем и становятся обещанием металла."

  tech_ore_crushing:
    name: "Механическая дробилка руды"
    epoch: mining_mechanics
    type: production_process
    requires: [tech_mining, tools, coal]
    unlocks: [ore_crusher, mineral_fraction_pool]
    description: "Один кусок руды за циклом раскрывает то, что было спрятано внутри."

  tech_basic_smelting:
    name: "Малая плавка"
    epoch: mining_mechanics
    type: production_process
    requires: [hull_mineral, coal, tech_ore_crushing]
    unlocks: [metal, small_foundry, reinforced_parts]
    description: "Остров впервые получает тяжёлый жар настоящей промышленности."

  tech_basic_mechanisms:
    name: "Базовые механизмы"
    epoch: mining_mechanics
    type: production_process
    requires: [metal, tools, precision_mineral]
    unlocks: [mechanisms, valves, bearings, gear_blocks]
    description: "Мир начинает не просто производить, а двигаться."

  tech_alcohol_engine:
    name: "Спиртовой двигатель"
    epoch: engines_routes
    type: engineering_platform
    requires: [fuel_spirit, mechanisms, design_experience_1]
    unlocks: [small_engine_alcohol, ship_series_mau]
    description: "Первый двигатель, который делает дальний рейс регулярным делом."

  tech_load_bearing_hull:
    name: "Несущий корпус"
    epoch: engines_routes
    type: engineering_platform
    requires: [metal, beams, hull_mineral, design_experience_1]
    unlocks: [ship_mau_1, hull_node_2, reinforced_frame]
    description: "Корпус перестаёт быть оболочкой и становится системой распределения нагрузки."

  tech_claudium_loop:
    name: "Клавдиевый контур"
    epoch: engines_routes
    type: engineering_platform
    requires: [claudium, conductive_mineral, mechanisms]
    unlocks: [small_claudium_loop, lift_node_upgrades]
    description: "Клавдий получает форму, а корабль - более честное право висеть в небе."

  tech_distributed_claudium_injection:
    name: "Распределительный впрыск клавдия"
    epoch: high_altitude
    type: precise_upgrade
    requires: [tech_claudium_loop, voltite, altitude_observations]
    effects: ["-2% расхода клавдия на 2 уровне модернизации клавдиевого контура"]
    unlocks: [claudium_loop_node_2_efficiency]
    description: "Не больше клавдия, а умнее путь, которым он проходит через контур."

  tech_gas_condensation:
    name: "Газовая конденсация"
    epoch: clouds_chemistry
    type: fundamental
    requires: [filters, water, mechanisms, cloud_scouting]
    unlocks: [gas_cloud_harvesting, gas_condenser, cloud_gases]
    description: "Облако становится месторождением, если научиться слушать его плотность."

  tech_chemical_reactions:
    name: "Управляемые реакции"
    epoch: clouds_chemistry
    type: production_process
    requires: [reaction_gas, acid, tech_gas_condensation]
    unlocks: [chemical_reactor, catalysts, reaction_speed_slider]
    description: "Партия может стать прорывом, а может погибнуть от жадности к скорости."

  tech_leviathan_observation:
    name: "Наблюдение левиафанов"
    epoch: leviathan_bio
    type: fundamental
    requires: [paper, scout_reports, route_experience_2]
    unlocks: [leviathan_data, flight_biology_techs, observation_post]
    description: "Некоторые существа нельзя сначала победить. Их нужно понять."

  tech_harpoon_hunting:
    name: "Гарпунная охота"
    epoch: leviathan_bio
    type: engineering_platform
    requires: [tech_leviathan_observation, weapons, power_sinew]
    unlocks: [harpoon_launcher, hunter_ships, leviathan_tissue_processing]
    description: "Верёвка, сталь и страх, натянутые между кораблём и живым небом."

  tech_biochemical_processing:
    name: "Биохимическая переработка"
    epoch: leviathan_bio
    type: production_process
    requires: [leviathan_tissue, medicine, filters]
    unlocks: [bio_lab, membranes, bio_lubricants, bio_filters]
    description: "Органика становится материалом, если обращаться с ней осторожнее, чем с рудой."

  tech_automaton_ruins:
    name: "Руины автоматонов"
    epoch: automatons
    type: fundamental
    requires: [weapons, paper, scouting_reports]
    unlocks: [ruin_sites, automaton_wrecks, precursor_archives]
    description: "Старая цивилизация не умерла молча. Её машины всё ещё отвечают."

  tech_automaton_salvage:
    name: "Разбор автоматонов"
    epoch: automatons
    type: production_process
    requires: [tech_automaton_ruins, tools, repair_kits]
    unlocks: [salvage_yard, automaton_components]
    description: "Каждый снятый релейный блок - вопрос к тем, кто построил мир до нас."

  tech_relay_logic:
    name: "Релейная логика"
    epoch: automatons
    type: engineering_platform
    requires: [logic_relay, conductive_mineral, automaton_data]
    unlocks: [automation_parts, route_automation, rigs_t1]
    description: "Автоматизация начинается с малого щелчка реле."

  tech_iceberg_harvesting:
    name: "Высотные айсберги"
    epoch: high_altitude
    type: fundamental
    requires: [high_altitude_scouting, heating, claudium_loop_2]
    unlocks: [iceberg_fields, ice_harpoon, sublimates]
    description: "В ледяной тишине наверху лежат вещества, которых нет на этой планете."

  tech_capital_shipyard:
    name: "Капитальная верфь"
    epoch: expedition
    type: engineering_platform
    requires: [automation_parts, prepared_dock, expedition_reports, capital_frame_design]
    unlocks: [capital_assembly_yard, capital_blocks, city_ship_projects]
    description: "Верфь становится не зданием, а центром новой цивилизации."
```

### Дополнительные технологии, закрывающие связи

```yaml
additional_technologies:
  tech_charcoal_burning:
    name: "Углежжение"
    epoch: island_industry
    type: production_process
    requires: [wood, island_workshop]
    unlocks: [charcoal_kiln, charcoal, early_smelting_boost]
    description: "Дерево становится плотным теплом для печей и мастерских."

  tech_reinforced_plating:
    name: "Усиленная обшивка"
    epoch: mining_mechanics
    type: precise_upgrade
    requires: [armor_mineral, metal, tech_basic_smelting]
    unlocks: [armor_plate, hull_node_armor_upgrades]
    description: "Корабль учится принимать удар не всем телом, а правильным слоем."

  tech_light_frame:
    name: "Лёгкая рама"
    epoch: mining_mechanics
    type: engineering_platform
    requires: [light_mineral, metal, tech_load_bearing_hull]
    unlocks: [light_frame_components, scout_ship_variants]
    description: "Меньше массы, больше нервности, выше цена ошибки."

  tech_basic_electricity:
    name: "Базовая электрика"
    epoch: engines_routes
    type: engineering_platform
    requires: [conductive_mineral, mechanisms, paper]
    unlocks: [wire, contact_plate, signal_lamps, basic_sensors]
    description: "Первые провода связывают приборы так же, как маршруты связывают острова."

  tech_precision_tools:
    name: "Точные инструменты"
    epoch: mining_mechanics
    type: production_process
    requires: [tools, precision_mineral, fine_oil]
    unlocks: [precision_tools, calibration_recipes, navigation_upgrades]
    description: "Мир начинает различать не только много и мало, но и точно."

  tech_small_shipbuilding:
    name: "Малая верфь"
    epoch: engines_routes
    type: engineering_platform
    requires: [tech_load_bearing_hull, tools, beams, metal]
    unlocks: [shipyard_small, small_ship_components]
    description: "Момент, когда корабль впервые собирается руками игроковой сети."

  tech_steam_engine:
    name: "Паровой двигатель"
    epoch: engines_routes
    type: engineering_platform
    requires: [coal, mechanisms, thermal_mineral, water]
    unlocks: [comp_steam_engine, medium_ships, boiler_blocks]
    description: "Тяжёлый, прожорливый, надёжный. Машина для кораблей, которые уже не игрушки."

  tech_prepared_dock:
    name: "Подготовленный док"
    epoch: engines_routes
    type: engineering_platform
    requires: [metal, mechanisms, tools, island_stage_3_hangar]
    unlocks: [shipyard_medium, medium_ship_components, dock_supply_needs]
    description: "Больший корабль требует не место посадки, а обслуживающую культуру."

  tech_repair_workshop:
    name: "Ремонтная мастерская"
    epoch: engines_routes
    type: production_process
    requires: [tools, mechanisms, cloth, metal]
    unlocks: [repair_kits, comp_repair_bay, ship_repair_tender_1]
    description: "Износ перестаёт быть концом и становится частью цикла знаний."

  tech_high_altitude_navigation:
    name: "Высотная навигация"
    epoch: high_altitude
    type: engineering_platform
    requires: [tech_iceberg_harvesting, optical_sublimate, altitude_observations]
    unlocks: [high_altitude_routes, comp_high_altitude_loop, expedition_altitude_reports]
    description: "Наверху меньше ориентиров, и потому каждый прибор важнее."

  tech_sublimate_systems:
    name: "Сублиматные системы"
    epoch: high_altitude
    type: engineering_platform
    requires: [sublimates, precision_tools, tech_high_altitude_navigation]
    unlocks: [sublimate_insulation, high_tier_reactors, capital_sublimate_blocks]
    description: "Экзотическая пыль верхнего неба становится инженерным языком."

  tech_precursor_interfaces:
    name: "Интерфейсы предтеч"
    epoch: automatons
    type: engineering_platform
    requires: [precursor_archive, interface_plate, automaton_data]
    unlocks: [precursor_control_links, advanced_rigs, capital_command_interfaces]
    description: "Новая цивилизация учится просить старые машины о невозможном."

  tech_city_ship_core:
    name: "Корабль-город"
    epoch: expedition
    type: fundamental
    requires: [tech_capital_shipyard, tech_sublimate_systems, tech_precursor_interfaces, expedition_reports]
    unlocks: [ship_city_horizon, city_ship_blocks, expedition_city_management]
    description: "Корабль перестаёт быть транспортом и становится образом жизни."
```

## Рецепты

Формат ниже преднамеренно похож на будущие CSV/JSON-конфиги.

```yaml
recipes:
  recipe_tools_basic:
    type: Manufacturing
    facility: tools_workshop
    unlocked_by: tech_toolmaking
    duration_min: 8
    inputs: { metal: 2, wood: 1, food: 0.2 }
    outputs: { tools: 1 }
    description: "Грубый набор инструмента для островных построек и обслуживания."

  recipe_precision_tools:
    type: Manufacturing
    facility: tools_workshop
    unlocked_by: tech_precision_tools
    duration_min: 18
    inputs: { tools: 1, precision_mineral: 2, fine_bio_oil: 0.2 }
    outputs: { precision_tools: 1 }
    description: "Инструменты, которые уже измеряют, а не просто режут."

  recipe_ore_crush_common:
    type: Processing
    facility: ore_crusher
    unlocked_by: tech_ore_crushing
    duration_min: 5
    fuel: { fuel_mix: 0.1 }
    inputs: { ore_unit: 1 }
    outputs_fractional: { hull_mineral: 0.55, light_mineral: 0.2, conductive_mineral: 0.05 }
    description: "Дробление обычной глыбы с накоплением минералов до целых единиц."

  recipe_metal_basic:
    type: Manufacturing
    facility: small_foundry
    unlocked_by: tech_basic_smelting
    duration_min: 12
    inputs: { hull_mineral: 3, coal: 2, tools: 0.1 }
    outputs: { metal: 2 }
    description: "Первая грубая плавка, достаточно хорошая для рам и крепежа."

  recipe_mechanisms_basic:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_basic_mechanisms
    duration_min: 20
    inputs: { metal: 3, tools: 1, precision_mineral: 1 }
    outputs: { mechanisms: 1 }
    description: "Шестерни, оси, клапаны и всё, что начинает двигать остров."

  recipe_small_claudium_loop:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_claudium_loop
    duration_min: 35
    inputs: { claudium: 2, conductive_mineral: 2, mechanisms: 1, precision_tools: 1 }
    outputs: { comp_small_claudium_loop: 1 }
    description: "Малый контур, достаточно стабильный для кораблей ранней серии."

  recipe_alcohol_engine:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_alcohol_engine
    duration_min: 30
    inputs: { mechanisms: 2, metal: 4, fuel_spirit: 3, valve_lubricant: 0.5 }
    outputs: { comp_alcohol_engine: 1 }
    description: "Двигатель, который пахнет спиртом, маслом и первым настоящим расстоянием."

  recipe_light_hull_frame:
    type: Manufacturing
    facility: small_foundry
    unlocked_by: tech_load_bearing_hull
    duration_min: 40
    inputs: { metal: 8, beams: 4, hull_mineral: 2, tools: 1 }
    outputs: { comp_light_hull_frame: 1 }
    description: "Рама, которая держит не только груз, но и будущую модернизацию."

  recipe_cargo_module_small:
    type: Manufacturing
    facility: shipyard_small
    unlocked_by: tech_small_shipbuilding
    duration_min: 25
    inputs: { boards: 6, metal: 2, cloth: 3, rope: 2 }
    outputs: { comp_small_cargo_module: 1 }
    description: "Складская утроба малого корабля."

  recipe_basic_navigation:
    type: Manufacturing
    facility: tools_workshop
    unlocked_by: tech_route_basics
    duration_min: 18
    inputs: { paper: 5, glass: 1, tools: 1, signal_lamp: 1 }
    outputs: { comp_basic_navigation: 1 }
    description: "Карты, лампы и простые приборы, без которых корабль летит только на упрямстве."

  recipe_mining_grabber:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_mining
    duration_min: 35
    inputs: { metal: 8, mechanisms: 2, tools: 2, hull_mineral: 2 }
    outputs: { comp_mining_grabber: 1 }
    description: "Механический коготь для захвата небольших рудных глыб."

  recipe_gas_condenser_pod:
    type: Manufacturing
    facility: gas_condenser
    unlocked_by: tech_gas_condensation
    duration_min: 45
    inputs: { glass: 6, filters: 4, pipes: 4, mechanisms: 2, quietgas: 1 }
    outputs: { comp_gas_condenser_pod: 1 }
    description: "Герметичная капсула, где облако становится грузом."

  recipe_gas_fraction_fuel:
    type: Processing
    facility: gas_condenser
    unlocked_by: tech_gas_condensation
    duration_min: 10
    fuel: { fuel_mix: 0.2 }
    inputs: { cloud_charge_fuel: 1 }
    outputs_fractional: { fulgrin: 0.5, thermogen: 0.25, sootgas: 0.15 }
    description: "Облако распадается на запахи будущего топлива."

  recipe_fuel_mix_basic:
    type: Reaction
    facility: chemical_reactor
    unlocked_by: tech_chemical_reactions
    duration_min: 45
    speed_range: "x1..x50"
    base_failure_chance: 0.03
    inputs: { fulgrin: 2, alcohol: 1, stabilizer: 0.5 }
    catalysts: { grey_stabilin: -0.02, bio_catalyst: -0.015 }
    outputs: { fuel_mix: 3 }
    description: "Полезная реакция, которую легко испортить нетерпением."

  recipe_harpoon_launcher:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_harpoon_hunting
    duration_min: 60
    inputs: { metal: 8, power_sinew: 2, mechanisms: 2, weapons: 1 }
    outputs: { comp_harpoon_launcher: 1 }
    description: "Механизм, который делает спор с левиафаном физическим."

  recipe_repair_bay:
    type: Manufacturing
    facility: repair_workshop
    unlocked_by: tech_repair_workshop
    duration_min: 55
    inputs: { repair_kits: 4, tools: 4, mechanisms: 3, metal: 10, cloth: 6 }
    outputs: { comp_repair_bay: 1 }
    description: "Мастерская, сжатая до корабельного отсека."

  recipe_automaton_relay_parts:
    type: Processing
    facility: salvage_yard
    unlocked_by: tech_automaton_salvage
    duration_min: 25
    inputs: { automaton_wreck: 1, tools: 1 }
    outputs_fractional: { logic_relay: 0.3, gear_block: 0.4, automaton_data: 0.2, broken_scrap: 0.6 }
    description: "Разборка старой машины на новые причины для автоматизации."

  recipe_rig_cargo_t1:
    type: Manufacturing
    facility: relay_workshop
    unlocked_by: tech_relay_logic
    duration_min: 50
    inputs: { logic_relay: 2, metal: 4, cloth: 2, mechanisms: 1 }
    outputs: { rig_expanded_hold_t1: 1 }
    description: "Больше места в трюме ценой хуже управляемого корабля."

  recipe_relay_autopilot:
    type: Manufacturing
    facility: relay_workshop
    unlocked_by: tech_relay_logic
    duration_min: 70
    inputs: { logic_relay: 4, navigation_block: 1, conductive_mineral: 4, paper: 12, automaton_data: 1 }
    outputs: { comp_relay_autopilot: 1 }
    description: "Автоматонный помощник, который держит маршрут, пока человек занят системой."

  recipe_ice_to_sublimates:
    type: Processing
    facility: ice_harvest_dock
    unlocked_by: tech_iceberg_harvesting
    duration_min: 30
    inputs: { iceberg_chunk: 1, coal: 2, filters: 1 }
    outputs_fractional: { water: 20, dry_sublimate: 0.35, optical_sublimate: 0.15, unstable_sublimate: 0.03 }
    description: "Лёд отдаёт воду сразу, а чудо - только терпеливым."

  recipe_ice_harvest_gear:
    type: Manufacturing
    facility: ice_harvest_dock
    unlocked_by: tech_iceberg_harvesting
    duration_min: 95
    inputs: { comp_harpoon_launcher: 1, heatproof_membrane: 3, mechanisms: 6, dry_sublimate: 2, repair_kits: 3 }
    outputs: { comp_ice_harvest_gear: 1 }
    description: "Гарпунная система, утеплённая и усиленная для работы с высотным льдом."

  recipe_medium_hull_frame:
    type: Manufacturing
    facility: shipyard_medium
    unlocked_by: tech_prepared_dock
    duration_min: 90
    inputs: { metal: 30, armor_mineral: 8, beams: 10, mechanisms: 4, tools: 4 }
    outputs: { comp_medium_hull_frame: 1 }
    description: "Средний каркас, после которого кораблю уже нужен настоящий док."

  recipe_steam_engine:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_steam_engine
    duration_min: 80
    inputs: { mechanisms: 8, thermal_mineral: 6, metal: 18, coal: 10, water: 8 }
    outputs: { comp_steam_engine: 1 }
    description: "Паровая машина, в которой дальний рейс впервые становится тяжёлой промышленностью."

  recipe_cold_bio_hold:
    type: Manufacturing
    facility: bio_lab
    unlocked_by: tech_biochemical_processing
    duration_min: 70
    inputs: { gas_membrane: 3, filters: 4, medicine: 3, metal: 8, preserving_gas: 2 }
    outputs: { comp_cold_bio_hold: 1 }
    description: "Холодный отсек, чтобы добыча левиафана доехала органикой, а не запахом."

  recipe_automation_rack:
    type: Manufacturing
    facility: relay_workshop
    unlocked_by: tech_relay_logic
    duration_min: 75
    inputs: { logic_relay: 5, relay_matrix: 1, conductive_mineral: 6, mechanisms: 3 }
    outputs: { comp_automation_rack: 1 }
    description: "Стойка реле, которая превращает корабль в участника сети."

  recipe_high_altitude_loop:
    type: Manufacturing
    facility: mechanics_workshop
    unlocked_by: tech_high_altitude_navigation
    duration_min: 120
    inputs: { comp_small_claudium_loop: 2, claudium: 12, resonant_claudium: 3, dry_sublimate: 4, precision_tools: 2 }
    outputs: { comp_high_altitude_loop: 1 }
    description: "Контур, который ещё держит подъём там, где обычный клавдий слабеет."

  recipe_heated_crew_block:
    type: Manufacturing
    facility: shipyard_medium
    unlocked_by: tech_iceberg_harvesting
    duration_min: 90
    inputs: { thermal_mineral: 6, heatproof_membrane: 3, medicine: 4, cloth: 12, coal: 10 }
    outputs: { comp_heated_crew_block: 1 }
    description: "Тёплый карман жизни внутри ледяного слоя."

  recipe_large_hull_frame:
    type: Assembly
    facility: capital_assembly_yard
    unlocked_by: tech_capital_shipyard
    duration_min: 240
    inputs: { comp_medium_hull_frame: 4, metal: 120, armor_mineral: 40, automation_parts: 10 }
    outputs: { comp_large_hull_frame: 1 }
    description: "Большой каркас, который требует уже не мастерской, а города вокруг верфи."

  recipe_factory_deck_section:
    type: Assembly
    facility: capital_assembly_yard
    unlocked_by: tech_capital_shipyard
    duration_min: 300
    inputs: { mechanisms: 80, automation_parts: 40, tools: 60, metal: 200, relay_matrix: 6 }
    outputs: { comp_factory_deck_section: 1 }
    description: "Фабрика, собранная как отсек, чтобы производить уже во время рейда."

  recipe_command_bridge:
    type: Assembly
    facility: capital_assembly_yard
    unlocked_by: tech_precursor_interfaces
    duration_min: 180
    inputs: { precision_tools: 10, optical_sublimate: 6, navigation_core: 1, relay_matrix: 4, paper: 50 }
    outputs: { comp_command_bridge: 1 }
    description: "Место, где карта, приборы и воля командира становятся одной системой."

  recipe_expedition_hangar:
    type: Assembly
    facility: capital_assembly_yard
    unlocked_by: tech_capital_shipyard
    duration_min: 220
    inputs: { metal: 100, mechanisms: 30, dock_pylon: 4, crane_block: 2, automation_parts: 12 }
    outputs: { comp_expedition_hangar: 1 }
    description: "Ангар, который несёт внутри себя не корабли, а маленькую флотилию задач."
```

## Компоненты кораблей

```yaml
ship_components:
  comp_light_hull_frame:
    name: "Лёгкая силовая рама"
    produced_at: small_foundry
    unlocked_by: tech_load_bearing_hull
    used_in: [ship_mau_1, ship_scout_spirit_1, ship_mining_picker_1]
    description: "Первый каркас, рассчитанный на модернизации, а не только на полёт."

  comp_alcohol_engine:
    name: "Спиртовой двигатель"
    produced_at: mechanics_workshop
    unlocked_by: tech_alcohol_engine
    used_in: [ship_mau_1, ship_scout_spirit_1]
    description: "Нерв раннего дальнего корабля."

  comp_small_claudium_loop:
    name: "Малый клавдиевый контур"
    produced_at: mechanics_workshop
    unlocked_by: tech_claudium_loop
    used_in: [ship_mau_1, ship_mining_picker_1, ship_gas_jar_1]
    description: "Стабилизированная подъёмная петля для малых корпусов."

  comp_small_cargo_module:
    name: "Малый грузовой модуль"
    produced_at: shipyard_small
    unlocked_by: tech_small_shipbuilding
    used_in: [ship_mau_1, ship_supply_moth_1]
    description: "Не просто ящик, а система доступа, крепежа и учёта груза."

  comp_basic_navigation:
    name: "Базовый навигационный пост"
    produced_at: tools_workshop
    unlocked_by: tech_route_basics
    used_in: [all_small_ships]
    description: "Компас, карты, лампы и место, где пилот перестаёт гадать."

  comp_mining_grabber:
    name: "Рудный захват"
    produced_at: mechanics_workshop
    unlocked_by: tech_mining
    used_in: [ship_mining_picker_1]
    description: "Коготь для летающих глыб."

  comp_gas_condenser_pod:
    name: "Газосборная капсула"
    produced_at: gas_condenser
    unlocked_by: tech_gas_condensation
    used_in: [ship_gas_jar_1]
    description: "Герметичный живот корабля для облачных фракций."

  comp_harpoon_launcher:
    name: "Гарпунная установка"
    produced_at: mechanics_workshop
    unlocked_by: tech_harpoon_hunting
    used_in: [ship_hunter_linehook_1, ship_ice_hook_1]
    description: "То, чем корабль цепляется за слишком опасные вещи."

  comp_repair_bay:
    name: "Малый ремонтный отсек"
    produced_at: repair_workshop
    unlocked_by: tech_repair_workshop
    used_in: [ship_repair_tender_1]
    description: "Передвижная мастерская, которая возвращает рейду терпение."

  comp_relay_autopilot:
    name: "Релейный автопилот"
    produced_at: relay_workshop
    unlocked_by: tech_relay_logic
    used_in: [ship_automaton_runner_1, ship_expedition_ledger_1]
    description: "Маленькая машина, которая помнит маршрут лучше человека."

  comp_ice_harvest_gear:
    name: "Айсберговый сборочный комплект"
    produced_at: ice_harvest_dock
    unlocked_by: tech_iceberg_harvesting
    used_in: [ship_ice_hook_1]
    description: "Гарпун, тёплый трюм и проклятья высотных рабочих."

  comp_factory_deck_section:
    name: "Секция фабричной палубы"
    produced_at: capital_assembly_yard
    unlocked_by: tech_capital_shipyard
    used_in: [ship_city_horizon]
    description: "Целый завод, собранный так, чтобы лететь."

  comp_medium_hull_frame:
    name: "Средняя силовая рама"
    produced_at: shipyard_medium
    unlocked_by: tech_prepared_dock
    used_in: [ship_hunter_linehook_1, ship_repair_tender_1, ship_automaton_runner_1, ship_ice_hook_1]
    description: "Каркас для корабля, который уже требует экипаж, док и план обслуживания."

  comp_steam_engine:
    name: "Паровой двигатель"
    produced_at: mechanics_workshop
    unlocked_by: tech_steam_engine
    used_in: [ship_hunter_linehook_1, ship_repair_tender_1]
    description: "Тяжёлая машина, меняющая малое судно на настоящий рабочий корабль."

  comp_cold_bio_hold:
    name: "Холодный биотрюм"
    produced_at: bio_lab
    unlocked_by: tech_biochemical_processing
    used_in: [ship_hunter_linehook_1]
    description: "Герметичный холодный отсек для органов, мембран и опасных желез."

  comp_automation_rack:
    name: "Стойка автоматонной автоматики"
    produced_at: relay_workshop
    unlocked_by: tech_relay_logic
    used_in: [ship_automaton_runner_1]
    description: "Релейная нервная система корабля, который меньше ждёт приказа."

  comp_high_altitude_loop:
    name: "Высотный клавдиевый контур"
    produced_at: mechanics_workshop
    unlocked_by: tech_high_altitude_navigation
    used_in: [ship_ice_hook_1]
    description: "Контур, рассчитанный на разреженный холод и плохую подъёмную силу."

  comp_heated_crew_block:
    name: "Обогреваемый экипажный блок"
    produced_at: shipyard_medium
    unlocked_by: tech_iceberg_harvesting
    used_in: [ship_ice_hook_1]
    description: "Не роскошь, а условие выживания выше нормального неба."

  comp_large_hull_frame:
    name: "Большой силовой каркас"
    produced_at: capital_assembly_yard
    unlocked_by: tech_capital_shipyard
    used_in: [ship_expedition_ledger_1]
    description: "Каркас, который сам становится проектом снабжения."

  comp_command_bridge:
    name: "Командный мостик"
    produced_at: capital_assembly_yard
    unlocked_by: tech_precursor_interfaces
    used_in: [ship_expedition_ledger_1]
    description: "Глаза и голос дальнего рейда."

  comp_expedition_hangar:
    name: "Экспедиционный ангар"
    produced_at: capital_assembly_yard
    unlocked_by: tech_capital_shipyard
    used_in: [ship_expedition_ledger_1]
    description: "Место для малых кораблей, которые делают большой рейд возможным."
```

## Корабли

Числа грубые, только для ощущения.

```yaml
ships:
  ship_starter_skiff:
    name: "Стартовый катер"
    role: courier
    rank: skiff
    series: simple
    unlocked_by: start
    built_at: capital_greenhaven
    cost: "бесплатный, безлимитный"
    stats: { cargo: 20, speed: 65, range_km: 8, crew: 1, aerodynamics: 0.75, claudium_efficiency: 0.6 }
    functions: [manual_delivery, scout_nearby, bring_to_island_hangar]
    description: "Почти бесполезный, но бесконечно важный. Первый способ сказать миру: я полечу."

  ship_mau_1:
    name: "МАУ-1 Веретено"
    role: freight
    rank: small
    series: alcohol_load_bearing
    unlocked_by: [tech_alcohol_engine, tech_load_bearing_hull, tech_claudium_loop]
    built_at: shipyard_small
    components:
      comp_light_hull_frame: 1
      comp_alcohol_engine: 1
      comp_small_claudium_loop: 1
      comp_small_cargo_module: 2
      comp_basic_navigation: 1
    resource_cost_estimate: { metal: 18, wood: 12, cloth: 8, mechanisms: 4, claudium: 3, tools: 4 }
    stats: { cargo: 90, speed: 82, range_km: 35, crew: 1, aerodynamics: 0.65, fuel_use: 1.0, claudium_efficiency: 0.8 }
    nodes: [hull, engine, claudium_loop, cargo, navigation]
    rigs: [rig_expanded_hold_t1, rig_economy_engine_t1, rig_storm_plating_t1]
    functions: [long_delivery, stable_routes, island_supply]
    description: "Первый корабль, который ощущается не выданным, а построенным."

  ship_mining_picker_1:
    name: "Камнеклёв"
    role: mining
    rank: small
    series: mechanical
    unlocked_by: [tech_mining, tech_ore_crushing, tech_claudium_loop]
    built_at: shipyard_small
    components:
      comp_light_hull_frame: 1
      comp_alcohol_engine: 1
      comp_small_claudium_loop: 1
      comp_mining_grabber: 1
      comp_basic_navigation: 1
    resource_cost_estimate: { metal: 22, mechanisms: 6, tools: 5, claudium: 3, hull_mineral: 4 }
    stats: { cargo: 55, speed: 55, range_km: 25, crew: 2, aerodynamics: 0.9, mining_rate: 1.0, claudium_efficiency: 0.7 }
    functions: [mine_ore_chunks, tow_small_rocks, generate_mining_experience]
    description: "Неловкий маленький добытчик, который впервые возвращает домой не товар, а сырьё."

  ship_gas_jar_1:
    name: "Облачная банка"
    role: gas_collector
    rank: small
    series: claudium_grid
    unlocked_by: [tech_gas_condensation, tech_claudium_loop]
    built_at: shipyard_small
    components:
      comp_light_hull_frame: 1
      comp_small_claudium_loop: 1
      comp_gas_condenser_pod: 1
      comp_basic_navigation: 1
      comp_alcohol_engine: 1
    resource_cost_estimate: { metal: 20, glass: 6, filters: 4, mechanisms: 5, claudium: 4 }
    stats: { cargo: 40, gas_tank: 60, speed: 60, range_km: 30, crew: 2, aerodynamics: 0.8 }
    functions: [harvest_clouds, bring_gas_to_hub, unlock_gas_samples]
    description: "Корабль с прозрачным животом, который учит пилота не бояться облаков."

  ship_hunter_linehook_1:
    name: "Линехват"
    role: leviathan_hunter
    rank: medium
    series: bio_hunter
    unlocked_by: [tech_harpoon_hunting, tech_biochemical_processing]
    built_at: shipyard_medium
    components:
      comp_medium_hull_frame: 1
      comp_steam_engine: 1
      comp_harpoon_launcher: 2
      comp_cold_bio_hold: 1
      comp_basic_navigation: 1
    resource_cost_estimate: { metal: 60, mechanisms: 18, weapons: 8, power_sinew: 4, medicine: 5 }
    stats: { cargo: 80, speed: 70, range_km: 55, crew: 5, aerodynamics: 0.95, hunting_power: 1.0 }
    functions: [hunt_small_leviathans, collect_organs, generate_leviathan_observations]
    description: "Не самый сильный корабль, но первый, которому живое небо отвечает сопротивлением."

  ship_repair_tender_1:
    name: "Заплатник"
    role: repair
    rank: medium
    series: steam_support
    unlocked_by: [tech_repair_workshop, tech_prepared_dock]
    built_at: shipyard_medium
    components:
      comp_medium_hull_frame: 1
      comp_steam_engine: 1
      comp_repair_bay: 2
      comp_small_cargo_module: 2
      comp_basic_navigation: 1
    resource_cost_estimate: { metal: 55, repair_kits: 12, mechanisms: 12, tools: 8, cloth: 10 }
    stats: { cargo: 70, speed: 58, range_km: 60, crew: 6, repair_rate: 1.0, aerodynamics: 1.05 }
    functions: [repair_ships_in_route, support_idle_raids, reduce_ship_loss]
    description: "Корабль, который делает длинный рейд не смелее, а устойчивее."

  ship_automaton_runner_1:
    name: "Релейщик"
    role: automation_support
    rank: medium
    series: automaton
    unlocked_by: [tech_relay_logic, tech_automaton_salvage]
    built_at: shipyard_medium
    components:
      comp_medium_hull_frame: 1
      comp_relay_autopilot: 2
      comp_automation_rack: 1
      comp_basic_navigation: 1
    resource_cost_estimate: { metal: 45, logic_relay: 8, conductive_mineral: 10, mechanisms: 10, automaton_data: 4 }
    stats: { cargo: 50, speed: 75, range_km: 80, crew: 3, automation: 1.0, aerodynamics: 0.8 }
    functions: [route_automation, island_dispatch_support, remote_orders]
    description: "Полукорабль-полумашина, которая связывает острова в сеть без постоянной руки игрока."

  ship_ice_hook_1:
    name: "Ледокрючник"
    role: ice_harvester
    rank: medium
    series: high_altitude
    unlocked_by: [tech_iceberg_harvesting, tech_distributed_claudium_injection]
    built_at: shipyard_medium
    components:
      comp_medium_hull_frame: 1
      comp_high_altitude_loop: 1
      comp_ice_harvest_gear: 1
      comp_heated_crew_block: 1
      comp_basic_navigation: 1
    resource_cost_estimate: { metal: 70, claudium: 12, heatproof_membrane: 4, medicine: 8, mechanisms: 18 }
    stats: { cargo: 90, speed: 45, range_km: 90, crew: 6, cold_resistance: 1.0, claudium_efficiency: 1.2 }
    functions: [harvest_icebergs, bring_sublimates, operate_in_ice_zone]
    description: "Медленный корабль с тёплым сердцем для самого холодного слоя мира."

  ship_expedition_ledger_1:
    name: "Дальний журнал"
    role: command_expedition
    rank: large
    series: expedition
    unlocked_by: [tech_capital_shipyard, tech_relay_logic, tech_high_altitude_navigation]
    built_at: capital_assembly_yard
    components:
      comp_large_hull_frame: 1
      comp_command_bridge: 1
      comp_relay_autopilot: 4
      comp_expedition_hangar: 1
      comp_factory_deck_section: 1
    resource_cost_estimate: { metal: 300, mechanisms: 80, automation_parts: 40, claudium: 45, medicine: 30, food: 100 }
    stats: { cargo: 600, speed: 38, range_km: 400, crew: 35, command_slots: 4, automation: 2.0 }
    functions: [coordinate_small_fleet, long_idle_reports, mobile_storage, expedition_command]
    description: "Корабль, на котором рейс становится маленьким обществом."

  ship_city_horizon:
    name: "Горизонт"
    role: city_ship
    rank: city
    series: sublimates_automaton_precursor
    unlocked_by: [tech_capital_shipyard, tech_city_ship_core, tech_sublimate_systems, tech_precursor_interfaces]
    built_at: capital_assembly_yard
    assembly_project: true
    stages:
      - { id: city_stage_frame, name: "Силовой каркас", inputs: { capital_frame_block: 12, hull_mineral: 200, metal: 1200 } }
      - { id: city_stage_lift, name: "Клавдиевая решётка", inputs: { claudium: 600, resonant_claudium: 80, lift_control_core: 2 } }
      - { id: city_stage_power, name: "Энергетика", inputs: { turbine_block: 8, boiler_block: 12, thermal_mineral: 300 } }
      - { id: city_stage_factory, name: "Фабричные палубы", inputs: { comp_factory_deck_section: 8, automation_parts: 200 } }
      - { id: city_stage_life, name: "Жилые и медицинские блоки", inputs: { food: 2000, medicine: 400, water: 3000, comfort_goods: 500 } }
      - { id: city_stage_docks, name: "Доки и ангары", inputs: { dock_pylon: 12, crane_block: 8, hangar_gate: 6 } }
      - { id: city_stage_command, name: "Командное ядро", inputs: { capital_command_core: 1, navigation_core: 2, relay_matrix: 20 } }
    stats: { cargo: 12000, speed: 12, range_km: 5000, crew: 600, dock_slots: 12, factory_slots: 10, command_slots: 12 }
    functions: [mobile_city, expedition_base, onboard_production, fleet_command, deep_region_operation]
    description: "Не корабль, а обещание, что мир игрока научился лететь целиком."
```

## Основные цепочки прогрессии

### Цепочка 1: оживление стартовых островов

```text
еда -> остров ткани
ткань -> остров еды
оба острова получают stage_1_supplied
избыток еды -> древесный остров
древесина -> бумажный остров и мастерская
бумага -> разведка и первые исследования
```

Смысл: игрок видит, что доставка меняет мир.

### Цепочка 2: первый завод инструментов

```text
еда + древесина + металл -> tools_workshop -> инструменты
инструменты -> stage_2_workshop для островов
инструменты -> новые производства
инструменты -> островские ангары
```

Смысл: игрок впервые выбирает место производства, а не просто обслуживает готовые острова.

### Цепочка 3: шахтёрство и первый построенный корабль

```text
tech_mining -> рудные глыбы
ore_crusher -> минералы
small_foundry -> металл
mechanics_workshop -> механизмы
tech_alcohol_engine + tech_load_bearing_hull + tech_claudium_loop
-> компоненты МАУ-1
-> ship_mau_1
```

Смысл: новый корабль становится итогом всей ранней сети.

### Цепочка 4: облака и химия

```text
tech_gas_condensation -> gas_condenser
газовые фракции -> chemical_reactor
реакции -> fuel_mix, acids, stabilizers
fuel_mix -> дальние рейсы и Processing
stabilizers -> безопасные реакции
```

Смысл: облака становятся промышленным слоем.

### Цепочка 5: левиафаны

```text
наблюдения + бумага -> tech_leviathan_observation
tech_harpoon_hunting -> охотничьи корабли
туша -> bio_lab
ткани/органы/жиры -> мембраны, смазки, фильтры, биореакции
```

Смысл: живой мир даёт не только ресурсы, но и знания о полёте.

### Цепочка 6: автоматоны

```text
руины -> автоматонные обломки + архивы
salvage_yard -> реле, сенсоры, данные
relay_workshop -> automation_parts
automation_parts -> риги, автопилоты, фабричные контроллеры
```

Смысл: автоматизация не покупается, а добывается из погибшей цивилизации.

### Цепочка 7: высота и сублиматы

```text
high_altitude_scouting -> ледяная зона
ice_hook_ship -> айсберги
ice_harvest_dock -> вода + сублиматы
сублиматы -> high-tier оптика, изоляция, реакторы, capital-системы
```

Смысл: верхний мир даёт редкие вещества, но требует развитых кораблей и защиты.

### Цепочка 8: корабль-город

```text
стабильная сеть островов
+ автоматизация
+ high-tier материалы
+ доки
+ экипаж и снабжение
+ старые корабли дают опыт
-> capital_assembly_yard
-> проект "Горизонт"
```

Смысл: корабль-город - не покупка, а доказательство зрелости мира.

## Что должен проверять большой тест будущего

- Все ресурсы имеют семейство, эпоху и способ получения.
- Каждый магистральный ресурс где-то производится и где-то нужен.
- Каждый редкий ресурс имеет хотя бы один важный use case.
- Каждая технология имеет требования и unlocks.
- Каждая технология открывает дверь: ресурс, рецепт, производство, корабль, узел, риг или механику.
- Все рецепты ссылаются на существующие ресурсы, технологии и производства.
- Каждый ship_component где-то производится.
- Каждый корабль имеет роль, ранг, серию, технологии, компоненты и функции.
- Каждый компонент корабля используется хотя бы в одном корабле или capital-блоке.
- Каждый остров имеет стадии развития и потребности.
- Нет корабля, который требует компонент из более поздней эпохи без явного high-tier требования.
- Нет рецепта, который требует ресурс, не открытый ни одной технологией.
- Нет технологии, которая ничего не открывает.
- Нет resource family без хотя бы одного производства или добычи.

## Первые кандидаты на реальные CSV

Сначала стоит перенести не всё, а вертикальный кусок:

1. Ресурсы: food, water, cloth, wood, coal, paper, tools, metal, mechanisms, claudium, fuel_mix, repair_kits.
2. Острова: capital, food, textile, water, timber, coal, paper, tool, mining_hub.
3. Технологии: route_basics, island_workshop, toolmaking, island_hangars, mining, ore_crushing, smelting, mechanisms, alcohol_engine, load_bearing_hull, claudium_loop.
4. Производства: farmstead, textile_looms, water_condensers, sawmill, tools_workshop, ore_crusher, foundry, mechanics_workshop, shipyard_small.
5. Корабли: starter_skiff, MAU-1, mining_picker.
6. Тест: проверить, что из стартовой сети можно дойти до MAU-1 без ручного читинга.

Это будет первый живой скелет всей огромной системы.
