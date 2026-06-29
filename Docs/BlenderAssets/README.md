# Wild Wind Blender Assets

This folder is the safe storage for Blender source work and export helpers.

## Current Asset Layout

- `Ships/` contains current ship source files:
  - `WW_Imperial_PatrolFrigate_R02_Korshun.blend`
  - `WW_Imperial_ArtilleryCruiser_R02_Barbet.blend`
  - `WW_Imperial_Battleship_Val.blend`
  - `WW_Frigate_Medium_Blockout.blend`
- `Turrets/` contains reusable bases, turret templates, weapon turrets, utility turrets, and torpedo launcher variants.
- Root `.blend` files are older/common work files and faction blockouts.
- Unity-ready ship exports live in `Assets/ShipImports/Models/BlenderShips/`.

## Scale

Use meters as Blender units.

- Frigate: about 60 m.
- Cruiser: about 150 m.
- Battleship: about 300-350 m.

Weapon, turret, and base sizes should read correctly against those hull lengths.

## Starter Ship Kit

When creating a new ship starter scene, keep it minimal unless more detail is explicitly requested.

The default starter kit is:

- one ship-size hull block with a `Mirror` modifier for symmetric editing;
- all needed base assets laid out nearby in the requested sizes;
- one citadel cube;
- one engine cube.

Do not add turrets, guns, superstructures, lights, cameras, labels, pivot markers, reference planes, helper roots, or parent chains to a starter kit unless explicitly requested.

## Turret And Base Contract

- Base and turret are separate objects.
- The base is static on the hull.
- The turret rotates around its real object origin/pivot and must not drift while rotating.
- The hull mount point and turret rotation point are different concepts.
- Bases may extend slightly below the hull mount surface so they sink cleanly into uneven hull geometry.
- Workpiece templates should use a real half mesh plus a `Mirror` modifier where symmetry is needed. Edit one side only.
- Before export, check normals: no red/backfacing outside surfaces.

## Armor Materials

Armor is painted in Blender with materials. Unity reads material names through `MeshArmorBody`.

Valid armor material names must contain an armor marker and a number. The first number is parsed as armor thickness in millimeters.

Accepted markers include:

- `Armor`
- `Armour`
- `Bron`
- `br_`
- `_br_`

Examples:

- `Armor_25`
- `Armor_55_Engine`
- `Armor_90_Deck`
- `Bron_160_Citadel`

Material color is for Blender visibility and can remain on the imported model. Non-armor material names fall back to the default mesh armor value.

## Export Notes

- Keep `.blend` source files in `Docs/BlenderAssets`.
- Export ship FBX files to `Assets/ShipImports/Models/BlenderShips`.
- Keep Read/Write enabled on imported model meshes if Unity needs to rebuild armor plates from mesh data.
- Do not rely on the old Unity armor-painting scene. Blender materials are now the source of armor zones.
