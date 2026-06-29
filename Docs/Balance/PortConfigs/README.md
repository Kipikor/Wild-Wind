# Port balance config draft

These CSV files are design configs for the port-side economy. They are not wired into Unity runtime yet.

Related model doc:

- `../EconomyProgressionDepth.md` - sustainable rank, high-rank impulse ships, and progression layers.
- `../MetaProgressionLoop.md` - whole-game meta loop, faction gates, subscriptions, impulses, and live progression length.
- `../ProgressionLengthEstimate.md` - mission count estimate to sustainable R10 and upper T1.

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
- `activity_reward_roles.csv` - main reward role for each recurring activity.
- `extraction_activity_limits.csv` - per-sortie field limiters for ore, gas, relics, automatons, and leviathans.
- `ship_rank_sustainability.csv` - rank break-even targets for replacing ships through sortie income.
- `economy_progression_stages.csv` - coarse player progression layers from start to R10/T2 entry.
- `progression_length_estimate.csv` - rough mission count estimate for reaching sustainable R10.
- `progression_length_modes.csv` - target progression-length modes from narrow R10 rush to completionist.
- `processing_efficiency_sources.csv` - sources that move processing efficiency from 15% to the current 88% cap.
- `faction_gate_policy.csv` - repeated faction bottleneck policy for components, recipes, and market bypasses.
- `economy_impulse_sources.csv` - events, repair dock, high-rank gifts, bundles, and subscriptions as controlled boosts.
- `faction_currency_roles.csv` - solid, freight, and profile faction currencies.
- `ship_acquisition_channels.csv` - buy/craft/repair acquisition policy by ship rank.
- `ore_deposits.csv` - ore access thresholds and processing composition for quick ore sortie calculations.
- `quick_ore_sortie_examples.csv` - calculated examples for the quick ore sortie model.
- `ore_profit_extremes.csv` - lower and upper FE examples for ore extraction plus base processing.
- `activity_budget.csv` - daily/weekly source and sink budget targets.
- `port_building_catalog.csv` - full island and port building catalog.
- `port_building_level_curve.csv` and `port_building_group_curve.csv` - common 30-level island building curve and group multipliers.
- `island_expansion.csv`, `builder_queue.csv`, `dock_slots.csv` - island zones, builders, ship docks, courier pads, repair slots, and contract slots.
- `building_catalog.csv` and `building_level_curve.csv` - only the 8 cascade production buildings and their 30-level production curve.
- `production_depth.csv` - processing depth and R-band requirements.
- `ship_hull_parts.csv` - canonical base pool of eight simple hull/corpus parts.
- `ship_machine_instrument_parts.csv` - canonical base pool of eight machine/instrument ship parts.
- `ship_prepared_material_recipes.csv` - R4-R5 prepared material recipe draft: alloys, polymers, ceramics, and gas/leviathan additives.
- `ship_part_recipes.csv` - R6-R7 hull and machine/instrument part recipe draft.
- `ship_large_blocks.csv` - canonical R8-R9 pool of eight large ship blocks.
- `ship_block_recipes.csv` - R8-R9 large ship block recipe draft.
- `recipe_time_upgrade_curve.csv` - five recipe time upgrades from new recipe to mastered recipe.
- `recipe_efficiency_upgrade_curve.csv` - five recipe economy upgrades from double input cost to normal input cost.
- `recipe_building_speed_curve.csv` - production building speed curve from level 1 to 30.
- `craft_progression_scope_policy.csv` - which craft scopes have recipe upgrades and which only use building speed.
- `shipyard_build_speed_curve.csv` - shipyard speed curve for all ship construction recipes.
- `builder_yard_speed_policy.csv` - building upgrade speed policy: builder yard applies, no individual building-upgrade recipe mastery.
- `item_production_recipes.csv` - numeric prepared material, ship part, and ship block recipes.
- `item_recipe_cost_levels.csv` - calculated input amounts for each item recipe economy level.
- `item_recipe_time_levels.csv` - calculated production times for each item recipe time level.
- `ship_rank_craft_time_policy.csv` - ship craft time anchors from R2 start to R10 full city.
- `faction_r10_licenses.csv` - one-use faction flagship licenses for tenth-rank ship crafting.
- `ship_r10_recipe_policy.csv` - tenth-rank craft policy: license plus heavier block-based recipes.
- `ship_r10_recipe_profiles.csv` - numeric tenth-rank craft profiles by ship class.
- `ship_r10_recipe_cost_levels.csv` - calculated input amounts for tenth-rank craft economy levels.
- `ship_r10_recipe_time_levels.csv` - calculated tenth-rank craft times for recipe time levels.
- `ship_crafting_rank_layers.csv` - current craft-layer rule by ship rank from free R1 through R10.
- `ship_crafting_depth_mix.csv` - target mix of current/previous/early production depth by public and virtual ship rank.
- `research_curve.csv`, `research_nodes.csv`, `sp_packages.csv` - archive/knowledge economy.
- `courier_orders.csv`, `capital_airplane.csv`, `repair_dock.csv`, `repair_jobs.csv` - recurring port systems.
- `faction_markets.csv`, `faction_quest_templates.csv` - faction stores and quests.
- `bundle_shop.csv`, `recharge_track.csv`, `coupon_config.csv`, `container_loot.csv` - shops and random rewards.
- `ship_event_overview.csv`, `ship_event_tasks.csv`, `ship_event_shop.csv`, `ship_event_roulette.csv`, `ship_event_recharge.csv` - first two-week ship event.
- `achievement_templates.csv`, `mastery_curve.csv` - long account progression.
- `automaton_modules.csv`, `leviathan_butchery.csv`, `relic_decode.csv` - port-side processing configs for special loot.

