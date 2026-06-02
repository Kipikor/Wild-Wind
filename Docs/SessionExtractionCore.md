# Wild Wind: Session Extraction Core

Status: first session-extraction vertical slice implemented in code and covered by big-test checks.
Date: 2026-05-29.
Branch: codex/session-extraction-core.

## Core Shift

Wild Wind moves from an island logistics simulator toward a session-based extraction game.

The main loop is:

```text
Base -> choose sortie -> fly in a limited zone -> farm resources -> reach extraction boundary -> pay return reserves -> home -> process -> cascade production -> upgrade -> new sortie
```

The base is the only home dock in the core loop. The player does not freely take off from the base into an endless world. The base is a management layer for storage, fitting, processing, cascade production, repairs, refueling, research, and sortie selection.

Inter-island logistics, social needs, passenger traffic, and visible ship compartments are not part of the core loop.

## Sortie Zone

A sortie happens inside a limited cylindrical play area.

- Default radius: 5 km for the first implementation.
- Horizontal boundary: circular extraction ring.
- Vertical top: unrestricted for now.
- Bottom: storm layer remains dangerous.
- The player farms inside the cylinder.
- Extraction is only possible near the circular boundary.
- Runtime behavior does not physically clamp the ship against the horizontal ring; crossing it counts as reaching/passing the extraction threshold.
- The flight compass shows the base/exit side during an active sortie as a `BASE SLIP 12s` marker. The marker points outward from the sortie center through the ship, which is the direction the ship must move while outside the cylinder.

The sortie is not infinite in every direction. The boundary is part of the play: the player must reach it with enough reserves to return home.

## Return Home

Returning home is a calculated extraction, not a free button.

The ship must be near the sortie boundary and have enough coal and claudium for the off-screen trip back to base.

Return uses the ship's march engines, not the direct player-control engines.

March engines are strategic cruise drives:

- they are very efficient over long distances;
- they need a long stable claudium-slipstream alignment before handoff;
- the player cannot use them for manual flight, combat, mining, catching fragments, or hazard dodging;
- they activate only after the ship leaves the active cylinder and holds claudium slipstream on the base vector, then take over for off-screen travel such as extraction home, travel to a distant sortie theater, or late expedition relocation;
- they let even the R0 fallback handle roughly 200 km strategic return, while large expedition ships can cross about 5000 km in one to two days.

Activation rule:

1. The ship must be at or outside the circular sortie cylinder edge and try to leave the active mission area.
2. The ship must be above the storm layer.
3. The ship must have enough coal and claudium for the calculated return.
4. Claudium slipstream must be active.
5. The ship's horizontal movement or nose direction must stay within `15 degrees` of the base/outward vector, so the compass marker is usable as the exit aim.
6. The ship must maintain that slipstream exit condition for `12 seconds`.

Claudium slipstream can be enabled only above `15 m/s`; if the ship slows to `15 m/s` or below, the mode shuts off. It ramps in over `20 seconds`; during the ramp it linearly lowers aerodynamic drag to `5%` of normal and raises claudium consumption up to `10x`.

When the activation timer completes, the manual sortie ends and the march-engine extraction calculation begins. If the ship stops moving toward home, re-enters the mission area, disables claudium slipstream, enters the storm, or loses the required reserve state, the timer resets.

Return calculation uses:

- distance to base, for example 220 km or 1000 km;
- estimated march cruise speed;
- estimated return time;
- coal required for march-engine travel;
- claudium required for lift and loaded mass;
- current coal and claudium in ship tanks.

Example:

```text
Distance to base: 220 km
Estimated speed: 35 m/s
Return time: 104 min
Coal required: about 44 kg
Claudium required: about 14-20 kg, depending on loaded mass
Coal onboard: 50 kg
Claudium onboard: 21 kg
Extraction possible.
```

If reserves are insufficient, extraction is blocked and the UI must explain what is missing.

On successful extraction:

- required coal and claudium are consumed;
- sortie cargo transfers to base storage;
- the game returns to base mode;
- progress is saved.

## Failure

If the ship is destroyed during a sortie:

- the player returns to base;
- all extracted resources from the sortie are lost;
- the active ship, its fitted High/Mid/Low/Rig modules, cargo, and remaining tank reserves are destroyed or marked lost;
- the player is never soft-locked.

The fallback ship is the Pioneer.

## Pioneer Fallback

Pioneer is the reserve airfield of the game economy.

- It is weak.
- It is free.
- It is always available when the player has no usable ship.
- It receives a free minimal refuel of coal and claudium.
- At the base, if the active ship is the Pioneer/starter hull and the base has no matching fuel, refuel still tops the ship back to the minimal starter reserve without spending storage.
- In core mode, if a save points at a missing or invalid hull, the base treats it as a Pioneer fallback case and restores the starter hull during free fallback refuel.
- It can always attempt safe starter sorties.
The Pioneer keeps failure meaningful without forcing a full restart.

## Starter Resource Loop

The first safe sortie is not abstract cargo loading. It uses falling ore from resource boulders.

Current runtime rule:

- the safe ore sortie spawns overhead boulders inside the sortie cylinder;
- boulders periodically shed physical ore fragments;
- the player catches falling fragments by positioning the ship under them;
- starter safe ore can be caught by ordinary cargo space, while later impact mining modules can remain useful for heavier or dangerous fragments.

The player:

1. Flies to a safe ore field.
2. Holds the ship under a shedding ore boulder.
3. Catches falling fragments in the cargo hold.
4. Watches coal, claudium, mass, and cargo capacity.
5. Reaches the boundary.
6. Extracts home if reserves are sufficient.
7. Processes ore at base.
8. Uses processed materials for upgrades.

This makes the first loop about positioning, mass, resources, and extraction timing.

## Consumables

Coal and claudium remain core constraints.

- Coal powers engine travel, thrust, and some active systems.
- Claudium supports lift, loaded mass, altitude stability, and return viability.
- A full cargo hold makes return planning harder.
- The player should feel the decision: keep farming or leave now.

## Ship Fitting

Visible compartments are removed from the core direction.

Ships use EVE-like fitting slots:

- High: active work modules, such as drills, gas harvesters, harpoons, weapons.
- Mid: control and support, such as radar, stabilizers, shock absorbers, autopilot.
- Low: hull improvements, such as cargo, engine economy, armor, lift support.
- Rig: strong passive modifications with tradeoffs.

Ship visuals do not need to reflect every fitted module. Fitting affects stats, actions, and session behavior.

Current transition rule:

- fixed infrastructure slots such as maneuver engine, march engine, propeller, and claudium loop may remain required ship internals;
- special modules are classified into High, Mid, Low, or Rig bands;
- the starter core hull exposes multiple High slots plus Mid, Low, and Rig slots, so the first branch tools can coexist without visible compartments;
- sortie zones may require a fitted module in the appropriate band, for example a High gas harvester, High harpoon, High impact/salvage module, or Mid observation module; the gate checks the actual fitting band, not only the module id;
- legacy `utility` slot data may remain in old catalog/setup assets for non-core compatibility, but core assembly and fitting UI ignore those slots; sortie gates must be satisfied by High/Mid/Low/Rig fitting.
- the public fitting API in core mode accepts only current High/Mid/Low/Rig slots and rejects legacy `utility` or wrong-band module installs.
- the old free hull selector is blocked in core mode; ship replacement belongs to Pioneer fallback and base assembly/cascade systems instead of legacy catalog picking.

## Removed From Core Loop

These systems may remain as experiments or future layers, but they are not required for the first playable core:

- social needs;
- passenger traffic;
- inter-island logistics;
- local island supply chains;
- visible ship compartments;
- deck-grid construction;
- city-like base needs.

Current runtime rule:

- `MetaGameState.sessionExtractionCoreMode` is on by default;
- this mode syncs into `PlayerProgress.sessionExtractionCoreMode`;
- new saves and default gameplay sessions start docked at `capital`;
- legacy free flight and legacy flight missions are blocked while core mode is active; flight begins through sorties;
- the old starter food/aerolite delivery seed is not created for core-mode new games;
- core runtime spawns only the base/capital dock from config islands and suppresses legacy free-world gas clouds, mining rocks, and leviathans; sortie resources are spawned by sortie controllers instead;
- legacy island production/consumption, island social needs, passenger traffic, inter-island logistics, autonomous scout/gas/mining fleets, ambient mining world spawning, and direct flagship social/expedition APIs do not advance in the core runtime;
- public legacy flagship expedition return is also blocked in core mode and clears stale expedition state instead of teleporting the player to a legacy dock;
- starting a core sortie clears stale legacy flagship expedition state and does not auto-start old expedition gameplay;
- legacy dock timed processes such as idle mining, iron smelting, and timed missions are blocked in core mode so resources enter through sorties, base processing, and cascade production;
- stale legacy timed-process jobs loaded from older saves are stopped in core mode without paying old resources, money, experience, or mission completion;
- legacy flight mission start/completion rewards are blocked in core mode, so old mission routes cannot bypass sortie extraction;
- legacy XP research and money purchases in the old tech tree are blocked in core mode; unlocks come from base resource technology cycles and cascade production;
- legacy money, direct resource, and ship-XP rewards are blocked in core mode; research output is represented by base resources such as `fundamental_experience` and `design_experience`;
- runtime cargo collection in core mode is accepted only during an active sortie and only for that sortie's configured resource; outside a sortie, cargo can enter the base through extraction, processing, and cascade flows rather than hidden ship-cargo injection;
- legacy personal inventory is migrated into base storage in core mode, and new core starts do not seed the old personal ore/iron inventory or starting paper;
- legacy shop refresh seed/timer is disabled in core mode;
- legacy island cargo loading, unloading, and timed cargo-transfer jobs are blocked in core mode; ship cargo enters the loop through sortie collection and returns through extraction;
- normal docking is restricted to the base in core mode; non-base legacy `DockAt` calls are blocked, and an active sortie can end through boundary extraction or ship loss, not through legacy docking;
- the legacy simulators remain available for old tests and experiments when core mode is disabled;
- the main gameplay HUD in core mode shows base processing inputs, processed materials, starter kit counts, ship tanks, weapon loadout cargo, boundary distance, claudium-slipstream exit progress, compact High/Mid/Low/Rig fitting occupancy, a compact overview of all five processing branches, all eight cascade production types, the next cascade order, and exposes Process branch, Refuel, Load munitions, Next sortie, Start selected sortie, Extract home, Run cascade, and next starter fitting upgrade actions;
- the old HUD dock action is replaced by boundary extraction while a core sortie is active, and is enabled only when the return calculation allows extraction;
- the debug dock UI also hides the legacy assembly/compartment panel and foregrounds sortie launch, High/Mid/Low/Rig fitting, base storage, base processing, cascade production, and fitting upgrades instead of legacy free flight and island-city controls.

## Base

The base is a progression machine, not a social city sim.

It contains:

- storage;
- hangar and fitting;
- refueling;
- repairs;
- processing;
- cascade production;
- research and recipe unlocks;
- sortie selection.

Current runtime rule:

- the base has an explicit `BaseExtractionIndustryState`;
- it always contains the five processing branches and eight cascade production types;
- the player can select among five starter sortie zones: ore boulders, gas condensate, automaton wrecks, leviathan remains, and survey ruins;
- each starter sortie is a bounded 5 km radius cylinder for the first implementation, with its own entry point, primary processing branch, and starter resource item;
- the first starter sortie catalog currently represents a close safe theater: all five starter zones are about 220 km from base so the Pioneer can return with starter tanks while coal and claudium still matter;
- non-ore starter sorties are gated by fitting: gas wants a gas harvester, automatons want an impact/salvage module, leviathan remains want a harpoon, and survey ruins want observation gear;
- starter resource caches shed collectible fragments in non-ore starter sorties, while the safe ore sortie keeps the overhead boulder behavior;
- ore processing consumes extracted ore from base storage and outputs minerals from `Ore_type.csv`;
- gas processing consumes cloud condensates and outputs gas/material fractions from `Gas_cloud_type.csv`;
- automaton dismantling consumes `broken_automaton` salvage and outputs mechanisms, tools, automaton cores, and design experience;
- leviathan processing consumes carcasses and outputs fat, hides, mineral shell, and claudium glands;
- cybernetic deciphering consumes `rock_info`, `cloud_info`, or `leviathan_info` and outputs research experience;
- starter cascade orders are represented as production orders with inputs, outputs, load per production type, estimated bottleneck, and estimated time;
- the starter catalog currently includes airframe kit, module kit, and munition bundle orders; Pioneer remains the only ship and base production upgrades its fitting rather than commissioning another hull;
- the base overview exposes all five processing branches, all eight cascade production lines, and the next cascade order/bottleneck to make the home layer read as the main progression machine;
- base processing and cascade production lines have levels and upgradeable capacity; upgrades spend processed base materials such as ferron, silvate, and charcoal, then raise the line level and throughput;
- the starter airframe order consumes minerals and charcoal, produces `airframe_kit`, and records load against all eight production types.
- the starter module order consumes early ore/base materials, produces `module_kit`, and can install the first branch tools through normal fitting: `starter_gas_harvester` in High for gas, `starter_mining_hold` in High for automatons, `starter_harpoon_rig` in High for leviathans, and `starter_observation_post` in Mid for survey; automaton cores are reserved for later, stronger modules so early branch unlocks are not deadlocked behind their own resources.
- the starter munition bundle can be loaded only at the base into ship `weapon` cargo, giving weapons and harpoons a core-mode supply path after legacy island cargo loading is disabled.
- the first Low fitting upgrade consumes `airframe_kit` and installs `starter_cargo_rack` into a Low slot.

## Five Processing Branches

Processing branches turn extracted raw resources into usable industrial inputs.

1. Ore processing.
2. Gas processing.
3. Automaton dismantling.
4. Leviathan processing.
5. Cybernetic deciphering of information.

Processing is a flow layer. It feeds storage and cascade production.

## Eight Cascade Production Types

Cascade production uses production orders and production capacities, not hand-run micro-recipes.

1. Construction production
   - Beams, panels, frames, armor plates, hull structures, cargo holds, tanks.

2. Metallurgical production
   - Alloys, treated metals, aerolite materials, wire, billets, metal stock.

3. Mechanical production
   - Drives, reducers, bearings, engines, pumps, drills, winches, harpoon mechanisms.

4. Instrumentation production
   - Scanners, radars, communication systems, navigation, autopilots, sensors.

5. Chemical reactor production
   - Polymers, lubricants, liquids, reagents, fuels, adhesives.

6. Automaton production
   - Automatons, logic blocks, servo cores, autonomous work nodes.

7. Electrical production
   - Wiring, generators, batteries, coils, power circuits, electric motors.

8. Assembly production
   - Final assembly of ships, modules, rigs, base installations, ammunition, and upgrades.

## Cascade Principle

The player chooses a major goal, not every intermediate item.

Examples:

- build a ship;
- build a module;
- build a rig;
- upgrade a base installation;
- assemble an expedition-capable component.

The cascade planner unfolds the recipe tree, checks storage, estimates required production load, time, missing materials, and bottlenecks.

Current runtime rule:

- `CascadeProductionOrderDefinition` stores order inputs, outputs, and load per production type;
- the base estimates whether an order can run from storage;
- the estimate exposes the bottleneck production type and approximate time;
- core runtime accepts only cascade orders from the base production catalog; ad hoc public orders are rejected instead of becoming direct resource rewards;
- running an order spends inputs, adds outputs, and records load on each production line.

The main question is:

```text
Which production capacity is the bottleneck?
```

Not:

```text
Which tiny component should the player craft manually next?
```

## First Vertical Slice

The first implementation target is:

1. Base screen/state with storage, refuel, High/Mid/Low/Rig fitting actions, and safe sortie launch.
2. Starter sortie catalog for ore, gas, automaton, leviathan, and survey/info branches.
3. Bounded sortie cylinders with falling starter resources.
4. Ship cargo collection.
5. Coal and claudium consumption.
6. Boundary extraction with return reserve calculation.
7. Cargo transfer to base storage.
8. Ore processing into early material.
9. Starter fitting upgrades using cascade output.
10. Big test coverage for the full loop and all five processing branches.

This is the new playable spine.

Current verification rule:

- the big test contains a `Session extraction core` section;
- it verifies base start, fresh core seed cleanup, free-flight and legacy flight-mission blocking, legacy XP/money tech-tree blocking, legacy money/direct-resource/ship-XP reward blocking, legacy personal inventory migration, legacy shop refresh shutdown, legacy free-world actor suppression, legacy runtime shutdown, direct flagship social API blocking, public legacy flagship expedition return blocking, legacy dock timed-process blocking including stale save jobs, legacy island cargo-transfer blocking, non-base legacy `DockAt` blocking, legacy `DockAt` blocking during active sorties, legacy flagship expedition suppression during sortie start, base refuel, base home overview for all five processing branches and all eight cascade production types, base line upgrades from processed resources, default 5 km sortie catalog coverage for all five processing branches, HUD sortie cycling, fitting-gated sortie launch, legacy utility no longer satisfying core sortie gates, public fitting API blocking of legacy utility and wrong-band installs, public hull selector blocking, runtime ship-cargo collection blocked outside active sorties, wrong-resource runtime cargo blocking inside active sorties, ad hoc cascade order blocking, HUD selected sortie launch, extraction blocked inside the sortie cylinder, extraction blocked in the storm layer, extraction blocked without coal/claudium reserves, extraction blocked until the claudium-slipstream exit hold completes, HUD boundary extraction reserve consumption and return, cargo return, non-ore starter sortie cache spawning with collectible gas, automaton, leviathan, and survey fragments plus extraction home, all five processing branches, starter cascade catalog outputs, starter High/Mid branch upgrades from `module_kit`, base munition loading into ship `weapon` cargo, Low-slot upgrade from `airframe_kit`, High/Mid/Low/Rig fitting bands without visible legacy utility slots or legacy dock assembly UI, sortie-loss recovery through the Pioneer fallback with old fitted modules and tank reserves destroyed, missing-hull restoration through the Pioneer fallback, and free Pioneer minimum refuel with empty base fuel storage.
