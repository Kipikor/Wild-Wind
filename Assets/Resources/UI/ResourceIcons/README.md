Resource icons are loaded by item id through `WildWindResourceIconCatalog`.

Replacement rule:
- keep the filename equal to the `Item.csv` `id_item`;
- keep the file under `Assets/Resources/UI/ResourceIcons`;
- replace `windshale_ore.png` to change the icon for `windshale_ore`, and so on.

These icons are placeholder UI assets. They are intentionally simple, physical PNG files so the resource catalog window and the big test can depend on real project assets instead of runtime-only generated visuals.
