import bpy
import json
import math
import os
from mathutils import Vector


OUT_PATH = os.path.join(os.path.dirname(bpy.data.filepath), "NewFactionSafeRenders", "new_factions_safe_orientation.json")
COLL_NAMES = [
    "ULP_Devourer_SAFE_ShipSet_R02_R10",
    "ULP_ClockworkArk_SAFE_ShipSet_R02_R10",
    "ULP_StoneVault_SAFE_ShipSet_R02_R10",
]


roots = []
for coll_name in COLL_NAMES:
    coll = bpy.data.collections.get(coll_name)
    if coll:
        roots.extend([o for o in coll.objects if o.type == "EMPTY" and o.name.endswith("_ROOT")])

expected_z = math.radians(90)
bad_rotation = []
bad_forward = []
for root in roots:
    z = root.rotation_euler.z
    delta = abs(((z - expected_z + math.pi) % (2 * math.pi)) - math.pi)
    if delta > 0.0001:
        bad_rotation.append((root.name, z))
    forward_world = root.matrix_world.to_3x3() @ Vector((-1, 0, 0))
    if forward_world.y > -0.999:
        bad_forward.append((root.name, tuple(round(v, 5) for v in forward_world)))

result = {
    "root_count": len(roots),
    "expected_rotation_z_degrees": 90,
    "bad_rotation": bad_rotation,
    "bad_forward": bad_forward,
    "all_ok": len(roots) == 219 and not bad_rotation and not bad_forward,
}
os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
with open(OUT_PATH, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)
print("ULP_NEW_FACTIONS_ORIENTATION_VERIFY_DONE")
print(json.dumps(result, ensure_ascii=False))
if not result["all_ok"]:
    raise SystemExit(2)
