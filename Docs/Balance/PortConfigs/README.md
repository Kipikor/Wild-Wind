# Port balance config draft

These CSV files are design configs for the port-side economy. They are not wired into Unity runtime yet.

Scope:

- mission output is treated as an external stable input;
- one mission consumes one ship sortie;
- combat mission tuning is outside this config layer;
- port systems convert mission resources into freight, solid, ships, progression, unlocks, and event rewards.

Main internal unit:

- `freight_equiv` means internal freight-equivalent value, not necessarily direct sell price.
- Direct Wind Houses buyback is intentionally much worse than `freight_equiv`.

Files:

- `rank_budget.csv` - R1-R10 and virtual R11*-R15* economy budgets.
- `currency_value.csv` - internal value and store behavior for currencies.
- `mission_baseline.csv` - stable mission-output assumption by rank and activity.
- `activity_budget.csv` - daily/weekly source and sink budget targets.
- `port_building_catalog.csv` - full island and port building catalog.
- `port_building_level_curve.csv` and `port_building_group_curve.csv` - common 30-level island building curve and group multipliers.
- `island_expansion.csv`, `builder_queue.csv`, `dock_slots.csv` - island zones, builders, ship docks, courier pads, repair slots, and contract slots.
- `building_catalog.csv` and `building_level_curve.csv` - only the 8 cascade production buildings and their 30-level production curve.
- `production_depth.csv` - processing depth and R-band requirements.
- `research_curve.csv`, `research_nodes.csv`, `sp_packages.csv` - archive/knowledge economy.
- `courier_orders.csv`, `capital_airplane.csv`, `repair_dock.csv`, `repair_jobs.csv` - recurring port systems.
- `faction_markets.csv`, `faction_quest_templates.csv` - faction stores and quests.
- `bundle_shop.csv`, `recharge_track.csv`, `coupon_config.csv`, `container_loot.csv` - shops and random rewards.
- `ship_event_overview.csv`, `ship_event_tasks.csv`, `ship_event_shop.csv`, `ship_event_roulette.csv`, `ship_event_recharge.csv` - first two-week ship event.
- `achievement_templates.csv`, `mastery_curve.csv` - long account progression.
- `automaton_modules.csv`, `leviathan_butchery.csv`, `relic_decode.csv` - port-side processing configs for special loot.
