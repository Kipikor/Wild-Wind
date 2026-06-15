import bpy
import os
from mathutils import Vector


OUT_DIR = os.path.join(os.path.dirname(bpy.data.filepath), "NewFactionSafeRenders")
TARGETS = {
    "Devourer_R10_Hunter": "ULP_Devourer_LeviathanHunterBattleship_Polished_R10_ROOT",
    "ClockworkArk_R10_Factory": "ULP_ClockworkArk_AutomatonFactoryBattleship_Polished_R10_ROOT",
    "StoneVault_R10_Foundry": "ULP_StoneVault_OreFoundryBattleship_Polished_R10_ROOT",
}


def world_bbox(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return (
        Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
        Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))),
    )


def bbox_for(objects):
    mn = Vector((1e18, 1e18, 1e18))
    mx = Vector((-1e18, -1e18, -1e18))
    for obj in objects:
        if obj.type != "MESH":
            continue
        a, b = world_bbox(obj)
        mn.x = min(mn.x, a.x)
        mn.y = min(mn.y, a.y)
        mn.z = min(mn.z, a.z)
        mx.x = max(mx.x, b.x)
        mx.y = max(mx.y, b.y)
        mx.z = max(mx.z, b.z)
    return mn, mx


def look_at(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_root(label, root_name, side=False):
    root = bpy.data.objects[root_name]
    visible = set(root.children_recursive)
    for obj in bpy.context.scene.objects:
        obj.hide_render = obj not in visible
    meshes = [o for o in visible if o.type == "MESH"]
    mn, mx = bbox_for(meshes)
    center = (mn + mx) * 0.5
    dims = mx - mn
    cam = bpy.data.objects.get("ULP_NewFactions_CloseupCamera")
    if not cam:
        cam_data = bpy.data.cameras.new("ULP_NewFactions_CloseupCamera")
        cam = bpy.data.objects.new("ULP_NewFactions_CloseupCamera", cam_data)
        bpy.context.scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_start = 0.1
    cam.data.clip_end = 100000.0
    cam.data.ortho_scale = max(dims.x * 0.85, dims.y * 1.5, dims.z * 4.0, 20)
    if side:
        cam.location = center + Vector((dims.x * 0.35 + 90, -dims.y * 2.8 - 140, dims.z * 1.0 + 45))
    else:
        cam.location = center + Vector((dims.x * 0.45 + 100, -dims.y * 2.2 - 120, dims.z * 4.2 + 120))
    look_at(cam, center)
    bpy.context.scene.camera = cam
    bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
    bpy.context.scene.display.shading.color_type = "MATERIAL"
    bpy.context.scene.render.resolution_x = 1600
    bpy.context.scene.render.resolution_y = 900
    bpy.context.scene.render.film_transparent = False
    path = os.path.join(OUT_DIR, f"closeup_{label}.png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


os.makedirs(OUT_DIR, exist_ok=True)
paths = {}
for label, root_name in TARGETS.items():
    if bpy.data.objects.get(root_name):
        paths[label] = render_root(label, root_name, side=("StoneVault" in label))
print("ULP_NEW_FACTIONS_CLOSEUPS_DONE")
print(paths)
