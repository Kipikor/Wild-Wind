# Base Island Building Prefabs

These placeholder prefabs are loaded by `WildWindBaseIslandView` through `Resources.Load`.

Replacement contract:

- keep the prefab filename unless the resource path in `WildWindBaseIslandView` is changed;
- keep the prefab origin at the center of the building footprint on the ground;
- use Unity meters; the runtime city view scales the prefab bounds to the grid footprint;
- custom child meshes and materials are safe;
- placeholder material assets live in `Assets/Resources/BaseIsland/Materials`;
- placeholder texture assets live in `Assets/Resources/BaseIsland/Textures`;
- when replacing a building model, replace or reassign its `Body` / `Accent` material slots instead of relying on runtime-generated colors;
- colliders are optional because the runtime adds/registers a root click collider.
