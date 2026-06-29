# Blender Ship Import Pipeline

This pipeline keeps Blender as the blockout source and Unity as the prefab database.

## Export one ship from Blender

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --factory-startup -b "Docs\BlenderAssets\WildWind_Ships_Blockouts.blend" --python "Docs\BlenderAssets\export_ships_to_unity.py" -- --project-root "C:\Users\korki\Wild Wind" --ship ULP_Devourer_HarpoonCruiser_Polished_R07_ROOT
```

## Export a collection

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --factory-startup -b "Docs\BlenderAssets\WildWind_Ships_Blockouts.blend" --python "Docs\BlenderAssets\export_ships_to_unity.py" -- --project-root "C:\Users\korki\Wild Wind" --collection ULP_NewFactionShipSets_R02_R10
```

The exporter writes:

- `Assets/ShipImports/Models/<Faction>/<ShipId>.fbx`
- `Assets/ShipImports/Manifests/<Faction>/<ShipId>.ship.json`

## Build Unity prefabs

In Unity use:

- `Wild Wind/Ships/Import Blender Ships/Rebuild All Prefabs`
- `Wild Wind/Ships/Import Blender Ships/Rebuild Selected Manifest`
- Project context menu: `Assets/Wild Wind/Rebuild Ship Prefab From Manifest`

Each generated prefab receives:

- `ShipVisualDefinition` on the root.
- `ShipVisualPart` on imported child objects that are listed in the manifest.

Role rules are intentionally simple and name-based. `Fin`, `Wing`, `Tail`, `Armor`, `Hull`, and similar parts are hull. `Launcher`, `Gun`, `Torpedo`, `Harpoon`, and `PMK` are weapons. `XRay`, `Magnet`, `Siphon`, `Repair`, and `Dome` are equipment.
