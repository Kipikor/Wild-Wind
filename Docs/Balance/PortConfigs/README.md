# Port Balance Config Draft

These CSV files are design configs for the port-side economy. Runtime ship data
comes from `Assets/Data/Config/Ship_catalog.csv`.

## Current Playable Ship Scope

Only these player ships are active:

- `capital_patrol_frigate_r02` - Коршун
- `capital_artillery_cruiser_r02` - Барбет
- `capital_heavy_battleship_r02` - Вал

Enemy square targets used by tactical prototypes are outside this player-ship
scope and are not described here.

## Current Ship Files

- `mission_profile_ship_profit.csv`
- `quick_sortie_ship_profit.csv`
- `ship_acquisition_channels.csv`
- `ship_crafting_recipes.csv`
- `ship_full_chain_time.csv`
- `ship_recipe_cost_levels.csv`
- `ship_recipe_fe_summary.csv`
- `ship_recipe_time_levels.csv`
- `ship_crafting_rank_layers.csv`
- `ship_rank_craft_time_policy.csv`

Older branch, license, and high-rank generated ship tables were removed from the
active design layer. New ships must be added explicitly through the current
catalog and then reflected in these files.

## Non-Ship Economy Files

The rest of this folder still contains port economy, extraction, faction,
research, courier, repair, event, and production drafts. Those files are not a
player-ship roster and should not be used as proof that another playable ship
exists.
