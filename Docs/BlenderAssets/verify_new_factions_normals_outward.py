import bpy
import bmesh
import json
import os
from mathutils import Vector


COLL_NAMES = [
    "ULP_Devourer_SAFE_ShipSet_R02_R10",
    "ULP_ClockworkArk_SAFE_ShipSet_R02_R10",
    "ULP_StoneVault_SAFE_ShipSet_R02_R10",
]
OUT_PATH = os.path.join(
    os.path.dirname(bpy.data.filepath),
    "NewFactionSafeRenders",
    "new_factions_safe_outward_normals_check.json",
)


def face_islands(bm):
    for face in bm.faces:
        face.tag = False
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


def island_centroid(faces):
    seen = set()
    total = Vector((0, 0, 0))
    count = 0
    for face in faces:
        for vert in face.verts:
            if vert.index in seen:
                continue
            seen.add(vert.index)
            total += vert.co
            count += 1
    return total / max(count, 1)


objects = []
for coll_name in COLL_NAMES:
    coll = bpy.data.collections.get(coll_name)
    if not coll:
        continue
    objects.extend([o for o in coll.all_objects if o.type == "MESH"])

bad_faces = []
island_count = 0
for obj in objects:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()
    for idx, island in enumerate(face_islands(bm)):
        island_count += 1
        center = island_centroid(island)
        for face in island:
            outward = face.calc_center_median() - center
            if outward.length > 1e-8 and face.normal.dot(outward.normalized()) < -1e-5:
                bad_faces.append((obj.name, idx, face.index))
                break
    bm.free()

report = {
    "mesh_objects": len(objects),
    "islands": island_count,
    "islands_with_inward_faces": len(bad_faces),
    "sample": bad_faces[:100],
    "all_ok": len(objects) == 656 and not bad_faces,
}
os.makedirs(os.path.dirname(OUT_PATH), exist_ok=True)
with open(OUT_PATH, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=2)
print("ULP_NEW_FACTIONS_OUTWARD_NORMALS_VERIFY_DONE")
print(json.dumps(report, ensure_ascii=False))
if bad_faces:
    raise SystemExit(2)
