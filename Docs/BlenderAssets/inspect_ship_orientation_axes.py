import bpy
from mathutils import Vector


def bbox_for_root(root_name):
    root = bpy.data.objects.get(root_name)
    if not root:
        return None
    meshes = [o for o in root.children_recursive if o.type == "MESH"]
    if not meshes:
        return None
    mn = Vector((1e18, 1e18, 1e18))
    mx = Vector((-1e18, -1e18, -1e18))
    for obj in meshes:
        for corner in obj.bound_box:
            p = obj.matrix_world @ Vector(corner)
            mn.x = min(mn.x, p.x)
            mn.y = min(mn.y, p.y)
            mn.z = min(mn.z, p.z)
            mx.x = max(mx.x, p.x)
            mx.y = max(mx.y, p.y)
            mx.z = max(mx.z, p.z)
    d = mx - mn
    return {
        "root": root_name,
        "dims": (round(d.x, 2), round(d.y, 2), round(d.z, 2)),
        "rotation_z": round(root.rotation_euler.z, 6),
        "loc": tuple(round(v, 2) for v in root.location),
    }


targets = [
    "ULP_Pioneer_ROOT",
    "ULP_LongRangeHeavyCruiser_ROOT",
    "ULP_WindHouseStarterFrigate_ROOT",
    "ULP_MistSynodStarterFrigate_ROOT",
    "ULP_Devourer_LeviathanHunterBattleship_Polished_R10_ROOT",
    "ULP_ClockworkArk_AutomatonFactoryBattleship_Polished_R10_ROOT",
    "ULP_StoneVault_OreFoundryBattleship_Polished_R10_ROOT",
]
for target in targets:
    print(bbox_for_root(target))

