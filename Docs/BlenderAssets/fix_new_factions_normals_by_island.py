import bpy
import bmesh
import json
import os


COLL_NAMES = [
    "ULP_Devourer_SAFE_ShipSet_R02_R10",
    "ULP_ClockworkArk_SAFE_ShipSet_R02_R10",
    "ULP_StoneVault_SAFE_ShipSet_R02_R10",
]
OUT_PATH = os.path.join(
    os.path.dirname(bpy.data.filepath),
    "NewFactionSafeRenders",
    "new_factions_safe_island_normals_fix.json",
)


def face_islands(bm):
    for f in bm.faces:
        f.tag = False
    islands = []
    for face in bm.faces:
        if face.tag:
            continue
        stack = [face]
        face.tag = True
        island = []
        while stack:
            current = stack.pop()
            island.append(current)
            for edge in current.edges:
                for neighbor in edge.link_faces:
                    if not neighbor.tag:
                        neighbor.tag = True
                        stack.append(neighbor)
        islands.append(island)
    return islands


def island_volume(faces):
    vol = 0.0
    for face in faces:
        verts = face.verts
        if len(verts) < 3:
            continue
        v0 = verts[0].co
        for i in range(1, len(verts) - 1):
            v1 = verts[i].co
            v2 = verts[i + 1].co
            vol += v0.dot(v1.cross(v2)) / 6.0
    return vol


def fix_object(obj):
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    islands = face_islands(bm)
    reversed_count = 0
    suspicious_zero = 0
    for island in islands:
        bmesh.ops.recalc_face_normals(bm, faces=island)
        volume = island_volume(island)
        if volume < -1e-7:
            bmesh.ops.reverse_faces(bm, faces=island)
            reversed_count += 1
        elif abs(volume) <= 1e-7:
            suspicious_zero += 1
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return {
        "islands": len(islands),
        "reversed": reversed_count,
        "zero_volume_islands": suspicious_zero,
    }


def collect_meshes():
    meshes = []
    for coll_name in COLL_NAMES:
        coll = bpy.data.collections.get(coll_name)
        if not coll:
            continue
        for obj in coll.all_objects:
            if obj.type == "MESH":
                meshes.append(obj)
    return meshes


os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
objects = collect_meshes()
fixed = {}
total_islands = 0
total_reversed = 0
total_zero = 0
for obj in objects:
    info = fix_object(obj)
    fixed[obj.name] = info
    total_islands += info["islands"]
    total_reversed += info["reversed"]
    total_zero += info["zero_volume_islands"]

bpy.context.view_layer.update()
remaining_negative = []
for obj in objects:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    for idx, island in enumerate(face_islands(bm)):
        vol = island_volume(island)
        if vol < -1e-7:
            remaining_negative.append((obj.name, idx, vol))
    bm.free()

report = {
    "mesh_objects": len(objects),
    "total_islands": total_islands,
    "islands_reversed": total_reversed,
    "zero_volume_islands": total_zero,
    "remaining_negative_islands": remaining_negative[:100],
    "remaining_negative_count": len(remaining_negative),
}
with open(OUT_PATH, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)

note_name = "ULP_NewFactions_SAFE_IslandNormalsFix.txt"
old = bpy.data.texts.get(note_name)
if old:
    bpy.data.texts.remove(old)
text = bpy.data.texts.new(note_name)
for key, value in report.items():
    text.write(f"{key}: {value}\n")

bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
print("ULP_NEW_FACTIONS_ISLAND_NORMALS_FIX_DONE")
print(json.dumps(report, ensure_ascii=False))
if remaining_negative:
    raise SystemExit(2)
