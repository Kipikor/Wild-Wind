# Wild Wind Blender Assets

This folder is the safe storage for Blender source work and export helpers.

## Current Asset Layout

- `Ships/` contains current ship source files:
  - `WW_Imperial_PatrolFrigate_R02_Korshun.blend`
  - `WW_Imperial_ArtilleryCruiser_R02_Barbet.blend`
  - `WW_Imperial_Battleship_Val.blend`
- `Turrets/` contains reusable bases, turret templates, weapon turrets, utility turrets, and torpedo launcher variants.
- Root `.blend` files are shared weapon/module work files only; playable ship sources live in `Ships/`.
- Unity-ready ship exports live in `Assets/ShipImports/Models/BlenderShips/`.
- Unity-ready base, turret, rocket-pod, utility-module, and torpedo-launcher exports live in `Assets/ShipImports/Models/Turrets/`.

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
- Bases are adapters, not hard compatibility locks. Any turret may be mounted on any base type if its scale and pivot are suitable for the slot.
- Bases may extend slightly below the hull mount surface so they sink cleanly into uneven hull geometry.
- Workpiece templates should use a real half mesh plus a `Mirror` modifier where symmetry is needed. Edit one side only.
- Keep Blender Face Orientation overlay enabled while creating, importing,
  checking, saving, or exporting meshes, so backfacing/internal surfaces are
  visible as red immediately.
- After creating or importing a mesh, check face orientation/normals immediately.
  Exterior surfaces must not show as red/backfacing. Recalculate or fix normals
  outward before saving, exporting, or calling the object done.
- Before export, check normals again: no red/backfacing outside surfaces.

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
- Export reusable base/turret/module FBX files to `Assets/ShipImports/Models/Turrets`.
- Keep Read/Write enabled on imported model meshes if Unity needs to rebuild armor plates from mesh data.
- Do not rely on the old Unity armor-painting scene. Blender materials are now the source of armor zones.

## Reusable Turret/Module Export

Reusable bases, turrets, rocket pods, utility modules, and torpedo launchers use a
stricter export command than full ships. Author them in Blender with:

- Blender `Z` as real height/up.
- Blender `Y` as the long weapon/module axis.
- The mesh origin at the rotation/mount pivot, usually the bottom center of the
  turret or module.
- Applied/clean transforms on the authored mesh, or export with `--apply-world`
  so the exporter bakes the visible result.

Export them with `--unity-module-y-up`:

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --factory-startup -b "Docs\BlenderAssets\Turrets\WW_Turret_76mm_Twin.blend" --python "Docs\BlenderAssets\export_blender_asset_to_fbx.py" -- --out "Assets\ShipImports\Models\Turrets\WW_Turret_76mm_Twin.fbx" --apply-world --unity-module-y-up
```

The flag bakes Blender `Z` into Unity `Y`, and Blender `-Y` into Unity `+Z`.
Without it, Unity imports long guns, torpedo tubes, scanners, and utility heads
as if their length were vertical height, so they stand on their nose in the dock.

After export, run the Big Test and inspect:

- `TestReports/PortDockTurretImportAudit.png`
- the Big Test line that says the reusable imports "lie upright in Unity"

## Export Blockers And Sanity Checks

These are the recurring mistakes that make exported ships/modules unusable in
Unity. Check them before calling an export done.

- Keep Face Orientation enabled while authoring and checking the asset. No
  exterior surface may be red/backfacing. Fix normals outward before saving or
  exporting.
- Use `--apply-world` when exporting authored ship/module meshes whose visible
  Blender transform must be baked into the FBX.
- Use `--unity-module-y-up` for separate reusable turrets, rocket pods,
  torpedo launchers, magnets, scanners, repair emitters, and other utility
  modules. Without this flag the long axis becomes vertical in Unity and the
  asset stands on its nose.
- Do not export old shared placeholder bundles into the active import folders.
  Active modules must be separate FBX files, not stale objects from
  `WW_WeaponModules.fbx`.
- Do not let exporter mirror-fixes bake side-authored objects across the global
  ship centerline. Objects named `_Left` or `_Right`, and any object whose world
  `X` position is not near zero, must keep their own side position. A mirror
  modifier on such an object is local authoring data, not permission to create a
  new full-width mesh centered on the ship.
- After exporting a ship, re-import the FBX in Blender or inspect Unity renderer
  bounds for side pairs. Left/right mount meshes must have opposite signed local
  centers and similar sizes.
- Korshun reference check: `Korshun_Base_S_Torpedo_Left` should remain near
  `X=-10.295`, `Korshun_Base_S_Torpedo_Right` should remain near `X=+10.295`,
  and each side should stay about `3.599 m` wide. If a side base imports with
  center `X=0` or width around `24 m`, the exporter mirrored it across the whole
  ship and the FBX is broken.
- If equipment appears on the center mount, only appears on one side, overlaps
  another module, or floats away from its base, inspect exported FBX bounds
  first. Do not patch placement code blindly until the exported mount geometry
  has been proven correct.
- Big Test now has a Korshun auxiliary magnet visual regression. It must keep
  left and right active module instances on opposite side mounts, not on the
  centerline.

`WW_Turret_100mm_Single` is the authored Korshun 100 mm single turret saved from
`Korshun_100mm_Single_Blockout`. Its source `.blend` keeps the editable half-mesh
plus `Mirror_X_Centerline`; its Unity `.fbx` is exported with
`--apply-world --unity-module-y-up`. Do not replace it with the old boxy
placeholder that previously used the same name.

`WW_Turret_100mm_Twin_PMK` is the separate authored 100 mm twin PMK turret
extracted from `Docs/BlenderAssets/Ships/WW_Imperial_ArtilleryCruiser_R02_Barbet.blend`.

Do not export the old shared `WW_WeaponModules.fbx` into
`Assets/ShipImports/Models/Turrets/`. That folder should contain separate active
FBX files only.
