import bpy
import json
import math
import os
from mathutils import Vector


OUT_DIR = os.path.join(os.path.dirname(bpy.data.filepath), "NewFactionSafeRenders")
TOP_NAME = "ULP_NewFactionShipSets_SAFE_R02_R10"
FACTION_COLLECTIONS = {
    "Devourer": "ULP_Devourer_SAFE_ShipSet_R02_R10",
    "ClockworkArk": "ULP_ClockworkArk_SAFE_ShipSet_R02_R10",
    "StoneVault": "ULP_StoneVault_SAFE_ShipSet_R02_R10",
}


def world_bbox(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return (
        Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
        Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))),
    )


def signed_volume(obj):
    me = obj.data
    mw = obj.matrix_world
    vol = 0.0
    for p in me.polygons:
        ids = p.vertices
        if len(ids) < 3:
            continue
        v0 = mw @ me.vertices[ids[0]].co
        for i in range(1, len(ids) - 1):
            v1 = mw @ me.vertices[ids[i]].co
            v2 = mw @ me.vertices[ids[i + 1]].co
            vol += v0.dot(v1.cross(v2)) / 6.0
    return vol


def touch(a, b, margin=0.04):
    amn, amx = a
    bmn, bmx = b
    return (
        amn.x - margin <= bmx.x
        and amx.x + margin >= bmn.x
        and amn.y - margin <= bmx.y
        and amx.y + margin >= bmn.y
        and amn.z - margin <= bmx.z
        and amx.z + margin >= bmn.z
    )


def look_at(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def bbox_for_objects(objects):
    mins = Vector((1e18, 1e18, 1e18))
    maxs = Vector((-1e18, -1e18, -1e18))
    for obj in objects:
        if obj.type != "MESH":
            continue
        mn, mx = world_bbox(obj)
        mins.x = min(mins.x, mn.x)
        mins.y = min(mins.y, mn.y)
        mins.z = min(mins.z, mn.z)
        maxs.x = max(maxs.x, mx.x)
        maxs.y = max(maxs.y, mx.y)
        maxs.z = max(maxs.z, mx.z)
    return mins, maxs


def audit():
    report = {}
    for faction, coll_name in FACTION_COLLECTIONS.items():
        coll = bpy.data.collections.get(coll_name)
        if not coll:
            report[faction] = {"missing_collection": coll_name}
            continue
        roots = [o for o in coll.objects if o.type == "EMPTY" and o.name.endswith("_ROOT")]
        meshes = [o for o in coll.all_objects if o.type == "MESH"]
        branch_ranks = {}
        starter = []
        floating = []
        negative_scale = []
        negative_volume = []
        for r in roots:
            rank = r.get("ULP_Rank")
            branch = r.get("ULP_Branch")
            if rank:
                branch_ranks.setdefault(branch, []).append(int(rank))
            else:
                starter.append(r.name)
            kids = [o for o in r.children if o.type == "MESH"]
            hulls = [o for o in kids if "Hull" in o.name]
            bbs = {o: world_bbox(o) for o in kids}
            for o in kids:
                if "Hull" in o.name:
                    continue
                if hulls and not any(touch(bbs[o], bbs[h]) for h in hulls):
                    floating.append(o.name)
        for o in meshes:
            if any(v < -0.0001 for v in o.scale):
                negative_scale.append(o.name)
            if signed_volume(o) < -0.0001:
                negative_volume.append(o.name)
        missing_rank_branches = {
            branch: sorted(set(range(2, 11)) - set(ranks))
            for branch, ranks in branch_ranks.items()
            if sorted(ranks) != list(range(2, 11))
        }
        report[faction] = {
            "roots": len(roots),
            "starters": len(starter),
            "rank_branches": len(branch_ranks),
            "branch_ranks": {k: sorted(v) for k, v in sorted(branch_ranks.items())},
            "missing_rank_branches": missing_rank_branches,
            "meshes": len(meshes),
            "floating": floating,
            "negative_scale": negative_scale,
            "negative_volume": negative_volume,
        }
    return report


def render_collection(faction, coll_name):
    coll = bpy.data.collections[coll_name]
    visible = set(coll.all_objects)
    for obj in bpy.context.scene.objects:
        obj.hide_render = obj not in visible
    meshes = [o for o in visible if o.type == "MESH"]
    mins, maxs = bbox_for_objects(meshes)
    center = (mins + maxs) * 0.5
    dims = maxs - mins
    cam = bpy.data.objects.get("ULP_NewFactions_AuditCamera")
    if not cam:
        cam_data = bpy.data.cameras.new("ULP_NewFactions_AuditCamera")
        cam = bpy.data.objects.new("ULP_NewFactions_AuditCamera", cam_data)
        bpy.context.scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.clip_start = 0.1
    cam.data.clip_end = 100000.0
    cam.data.ortho_scale = max(dims.x * 0.92, dims.y * 1.18, dims.z * 7.0, 80)
    cam.location = center + Vector((dims.x * 0.50 + 320, -dims.y * 0.78 - 320, dims.z * 8.5 + 380))
    look_at(cam, center)
    bpy.context.scene.camera = cam
    bpy.context.scene.render.engine = "BLENDER_WORKBENCH"
    bpy.context.scene.display.shading.color_type = "MATERIAL"
    bpy.context.scene.render.resolution_x = 1920
    bpy.context.scene.render.resolution_y = 1080
    bpy.context.scene.render.film_transparent = False
    path = os.path.join(OUT_DIR, f"new_factions_safe_{faction}.png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


os.makedirs(OUT_DIR, exist_ok=True)
report = audit()
paths = {}
for faction, coll_name in FACTION_COLLECTIONS.items():
    if bpy.data.collections.get(coll_name):
        paths[faction] = render_collection(faction, coll_name)

result = {"audit": report, "renders": paths}
with open(os.path.join(OUT_DIR, "new_factions_safe_audit.json"), "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)
print("ULP_NEW_FACTIONS_SAFE_AUDIT_RENDER_DONE")
print(json.dumps(result, ensure_ascii=False))
