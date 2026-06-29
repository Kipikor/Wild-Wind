# Ship Armor Material Rules

Date fixed: 2026-06-28.

Armor for Blender ships is authored through Blender materials. Unity reads the
armor value from the material name.

Unity-side armor painting tools are retired. Do not add a Unity scene/tool for
manual armor painting: paint the mesh in Blender, export the FBX, and let
`MeshArmorBody` parse the `Armor_XX` material names at runtime.

For now the armor materials also keep their visible debug colors in-game. Later
they can be replaced by production ship materials while preserving the material
names or another armor metadata channel.

Working material format:

```text
Armor_XX
```

`XX` is armor thickness in millimeters. Keep names short. The geometry/selected
faces define the zone; the material name defines only the armor value.

Color convention: green is thin armor, yellow is medium armor, red is thick
armor.

## Korshun, Imperial Patrol Frigate R2

Blender file:

```text
Docs/BlenderAssets/Ships/WW_Imperial_PatrolFrigate_R02_Korshun.blend
```

Material set:

```text
Armor_15
Armor_20
Armor_25
Armor_30
Armor_45
Armor_50
Armor_55
```

Current painted armor area estimate:

| Armor | Painted area |
| --- | ---: |
| 15 mm | 231.68 m2 |
| 20 mm | 397.46 m2 |
| 25 mm | 230.39 m2 |
| 30 mm | 698.02 m2 |
| 45 mm | 198.17 m2 |
| 50 mm | 352.01 m2 |

`Armor_55` exists as a reserve material, but is not assigned to faces in the
current painted version.

Armor mass estimate with steel density 7850 kg/m3: about 507 t.

Full ship mass target for this configuration: about 900-1200 t.

Current model bounds: about 65.8 m long, 29.1 m wide, 10.1 m high.

## Barbet, Imperial Heavy Artillery Cruiser

Blender file:

```text
Docs/BlenderAssets/Ships/WW_Imperial_ArtilleryCruiser_R02_Barbet.blend
```

Material set:

```text
Armor_20
Armor_30
Armor_45
Armor_60
Armor_80
Armor_100
Armor_120
Armor_140
Armor_160
```

Current starter assignment:

| Current object group | Assigned armor |
| --- | ---: |
| Machine guns / 30 mm mounts | 20 mm |
| S-size bases | 30 mm |
| 100 mm twin secondary turrets | 60 mm |
| Main cruiser hull and M-size bases | 80 mm |
| Engine cube and L-size bases | 120 mm |
| 254 mm twin main turrets | 140 mm |
| Citadel cube | 160 mm |

`Armor_45` and `Armor_100` are available for manual face painting, but the
starter assignment does not use them yet.

Recommended armor layout:

| Zone | Armor |
| --- | ---: |
| Thin covers, machine guns, light external parts | 20-30 mm |
| Deck, weak ends, turret roofs | 45-60 mm |
| Regular outer hull outside citadel | 80 mm |
| Main belt / thick side plates | 100-120 mm |
| Engine module | 120 mm |
| Large turret bases / barbette structures | 120 mm |
| 254 mm main turrets, front plates | 140 mm |
| 254 mm main turrets, sides and roof | 80-100 mm |
| Citadel | 160 mm |

Design intent: Barbet should reliably shrug off 76-100 mm weapons at bad angles,
but it should not be safe against a clean 254 mm broadside hit. A 254 mm hit with
a good angle should be dangerous for cruiser armor.

## Val, Imperial Battleship

Blender file:

```text
Docs/BlenderAssets/Ships/WW_Imperial_Battleship_Val.blend
```

Material set:

```text
Armor_30
Armor_45
Armor_60
Armor_80
Armor_120
Armor_160
Armor_220
Armor_280
Armor_340
```

Current starter assignment:

| Current object group | Assigned armor |
| --- | ---: |
| 30 mm machine guns | 30 mm |
| S-size bases | 45 mm |
| Medium utility equipment / magnets | 60 mm |
| 76 mm quadruple turrets and M-size bases | 80 mm |
| Regular battleship hull starter material | 160 mm |
| Engine cube | 220 mm |
| XL bases and 354 mm triple main turrets | 280 mm |
| Citadel cube | 340 mm |

`Armor_120` is available for manual face painting, but the starter assignment
does not use it yet. Use it for thicker deck/superstructure transitions,
secondary barbettes, and protected machinery shoulders when painting the hull by
faces.

Current starter armor mass estimate: about 134 600 t. This is intentionally a
coarse starter value because the hull blockout is assigned as whole objects, not
as carefully separated armor plates yet.

Design intent: Val is the upper end of the current ship scale. It should shrug
off cruiser-caliber weapons unless they hit weak plates or modules, but 354 mm
and future 406-460 mm weapons should remain meaningful against its main armor.
