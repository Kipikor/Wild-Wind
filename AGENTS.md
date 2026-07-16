# Wild Wind Codex Instructions

This file is the persistent project memory for Codex. New Codex chats working
from this repository must treat it as project-level instructions before making
changes.

## Project Shape

- Wild Wind is a Unity game with a real 3D city/meta scene and tactical sorties.
- The user usually discusses design in Russian; answer in Russian unless asked otherwise.
- Prefer implementing small, working vertical slices over speculative framework work.
- Do not revert unrelated user changes. The worktree is often intentionally dirty.

## UI References

- UI screenshots, SVGs, and mockups are references for layout, proportions, colors,
  controls, and visual language.
- Do not add a reference image/SVG/PNG as a game background or full-screen overlay
  unless the user explicitly says to insert that image as an asset.
- Main menu/meta UI must sit as live UI over the real 3D city. The 3D scene is not
  to be covered by a flat reference picture.
- If a visual request is ambiguous, ask whether the asset is a reference or should
  become an in-game image.

## Blender Ship Blockouts

- When the user asks for a new ship starter/base setup in Blender, create only the
  base ship kit unless they explicitly ask for more.
- The base ship kit is:
  1. one ship-size hull cube with the correct length;
  2. all available turret-base asset types laid out nearby in S, M, and L sizes;
  3. one citadel cube;
  4. one engine cube.
- Do not add turrets, guns, superstructures, secondary weapons, lights, cameras,
  labels, pivot marker objects, decorative geometry, reference planes, or extra
  root/helper objects to a base ship kit unless the user explicitly asks for them.
- Imported Blender base assets must be unparented after import. Keep only the
  requested mesh objects in the scene, with no parent links, constraints, hidden
  roots, or dotted relationship lines.
- Put a Mirror modifier on the ship hull cube for manual half-hull editing when
  the user is shaping a symmetric hull. The visible full hull size must remain the
  requested ship size.
- Symmetric Blender blockout placeholders/blanks must also be built as half-meshes
  first, then mirrored with a Mirror modifier. Do not put a Mirror modifier on a
  complete full-width blank; the source mesh should be only one side of the final
  visible object.
- For mirrored Blender blanks, keep the Mirror modifier visible in edit mode but
  leave `show_on_cage` disabled, so only the real source half can be selected and
  edited.
- Use existing saved turret-base assets from `Docs/BlenderAssets/Turrets` for
  base kits instead of inventing new base shapes.

## Core Gameplay Contracts

- Core tactical control is strategic: ships are selected, ordered, and act mostly
  through autonomous weapon/equipment groups.
- Ship movement, combat, missions, economy, quests, and events should be built from
  explicit gameplay contracts rather than hidden one-off logic.
- Important gameplay facts should be represented as domain events. For example:
  enemy killed, damage dealt, sortie started, sortie completed, loot collected,
  resource mined, objective completed.
- Quests, daily tasks, achievements, events, rewards, reputation, and economy should
  listen to events and apply their own filters. They must not directly mutate or
  reset each other.
- One combat event may progress several systems at once. For example, killing a
  pirate can progress both "kill any enemy" and "kill pirates" objectives.
- Progress must be persistent across sortie return where the design expects it.
- Rewards should be claimable only once unless the design explicitly says otherwise.

## Big Test Is The Head Of Everything

- The big test is the project's main QA spine. Treat it as the first place to lock
  down important behavior.
- Any change touching meta progression, missions, sortie flow, rewards, economy,
  quests, combat results, save/progress, or major HUD behavior should add or update
  checks in `Assets/Scripts/Session/WildWindBigTestRunner.cs` and/or the related
  PlayMode tests.
- A feature is not really done if the player can complete it manually once but the
  big test cannot prove the core contract.
- When adding a new system, include at least the minimal regression path:
  start state, action, expected progress/result, persistence/return if relevant,
  and a guard against double reward or accidental reset.
- If a proper big-test check is not feasible in the current turn, say that clearly
  in the final response and explain the remaining risk.
- After running Play Mode, Big Test, or Unity automation, stop Play Mode before
  finishing the turn so the editor is left in Edit Mode for the user.

## Before Implementing Risky Features

- For missions, quests, economy, UI references, and cross-system behavior, first
  state the intended contract in a few concrete bullets.
- Ask a clarifying question when a reasonable implementation could go in two
  meaningfully different directions, especially for reference images and reward
  ownership.
- Keep code changes close to the existing project patterns.
- Run a relevant compile/test check before finishing when code was changed. At
  minimum, use the existing C# build when a full Unity run is not available.
