import bpy
import bmesh
import math
import os
import sys
from datetime import datetime
from mathutils import Vector


# Generates the three remembered factions into the currently opened .blend.
# Usage:
# blender -b SOURCE.blend --python generate_new_factions_safe.py -- OUTPUT.blend
#
# Forward direction is negative X. The generator keeps object count low:
# each ship has a root empty plus 3-4 mesh objects, with attached modules
# built into those meshes to avoid loose floating micro-parts.


STAMP = datetime.now().strftime("%Y%m%d_%H%M%S")
argv = sys.argv
OUTPUT_BLEND = None
if "--" in argv:
    tail = argv[argv.index("--") + 1 :]
    if tail:
        OUTPUT_BLEND = tail[0]


def mat(name, color):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.diffuse_color = color
    return material


MATS = {
    "dev_hull": mat("ULP_MAT_Devourer_DarkIron", (0.10, 0.09, 0.08, 1)),
    "dev_armor": mat("ULP_MAT_Devourer_BloodArmor", (0.30, 0.05, 0.04, 1)),
    "dev_bone": mat("ULP_MAT_Devourer_BoneHarpoons", (0.78, 0.68, 0.48, 1)),
    "dev_metal": mat("ULP_MAT_Devourer_BlackGunmetal", (0.05, 0.05, 0.045, 1)),
    "ark_hull": mat("ULP_MAT_ClockworkArk_OiledBronze", (0.23, 0.21, 0.15, 1)),
    "ark_panel": mat("ULP_MAT_ClockworkArk_TanModule", (0.42, 0.34, 0.22, 1)),
    "ark_dark": mat("ULP_MAT_ClockworkArk_DarkMachinery", (0.08, 0.08, 0.07, 1)),
    "ark_cyan": mat("ULP_MAT_ClockworkArk_XRayCyan", (0.0, 0.82, 1.0, 1)),
    "stone_hull": mat("ULP_MAT_StoneVault_DarkStone", (0.18, 0.18, 0.16, 1)),
    "stone_plate": mat("ULP_MAT_StoneVault_TopArmor", (0.28, 0.28, 0.24, 1)),
    "stone_under": mat("ULP_MAT_StoneVault_UndersideGear", (0.075, 0.07, 0.06, 1)),
    "stone_magnet": mat("ULP_MAT_StoneVault_MagnetBlue", (0.06, 0.42, 0.78, 1)),
    "stone_amber": mat("ULP_MAT_StoneVault_AmberLamp", (0.95, 0.55, 0.10, 1)),
}


def ensure_clean_collection(name, parent=None):
    old = bpy.data.collections.get(name)
    if old:
        old.name = f"{name}_OLD_KEEP_{STAMP}"
        old.hide_viewport = True
        old.hide_render = True
    coll = bpy.data.collections.new(name)
    if parent:
        parent.children.link(coll)
    else:
        bpy.context.scene.collection.children.link(coll)
    return coll


TOP = ensure_clean_collection("ULP_NewFactionShipSets_SAFE_R02_R10")
COLLS = {
    "Devourer": ensure_clean_collection("ULP_Devourer_SAFE_ShipSet_R02_R10", TOP),
    "ClockworkArk": ensure_clean_collection("ULP_ClockworkArk_SAFE_ShipSet_R02_R10", TOP),
    "StoneVault": ensure_clean_collection("ULP_StoneVault_SAFE_ShipSet_R02_R10", TOP),
}


class MeshKit:
    def __init__(self, name, mats):
        self.name = name
        self.mats = mats
        self.mat_index = {m.name: i for i, m in enumerate(mats)}
        self.verts = []
        self.faces = []
        self.face_mats = []

    def _add(self, verts, faces, material):
        off = len(self.verts)
        self.verts.extend(verts)
        idx = self.mat_index[material.name]
        for face in faces:
            self.faces.append(tuple(off + v for v in face))
            self.face_mats.append(idx)

    def box(self, loc, size, material):
        x, y, z = loc
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        verts = [
            (x - sx, y - sy, z - sz),
            (x + sx, y - sy, z - sz),
            (x + sx, y + sy, z - sz),
            (x - sx, y + sy, z - sz),
            (x - sx, y - sy, z + sz),
            (x + sx, y - sy, z + sz),
            (x + sx, y + sy, z + sz),
            (x - sx, y + sy, z + sz),
        ]
        faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        self._add(verts, faces, material)

    def cyl_x(self, loc, length, radius, material, sides=8):
        x0, y0, z0 = loc
        verts = []
        for x in (x0 - length / 2, x0 + length / 2):
            for i in range(sides):
                a = 2 * math.pi * i / sides
                verts.append((x, y0 + math.cos(a) * radius, z0 + math.sin(a) * radius))
        faces = []
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((i, j, sides + j, sides + i))
        faces.append(tuple(range(sides - 1, -1, -1)))
        faces.append(tuple(range(sides, sides * 2)))
        self._add(verts, faces, material)

    def cyl_z(self, loc, height, radius, material, sides=8):
        x0, y0, z0 = loc
        verts = []
        for z in (z0 - height / 2, z0 + height / 2):
            for i in range(sides):
                a = 2 * math.pi * i / sides
                verts.append((x0 + math.cos(a) * radius, y0 + math.sin(a) * radius, z))
        faces = []
        for i in range(sides):
            j = (i + 1) % sides
            faces.append((i, sides + i, sides + j, j))
        faces.append(tuple(range(sides - 1, -1, -1)))
        faces.append(tuple(range(sides, sides * 2)))
        self._add(verts, faces, material)

    def cone_x(self, loc, length, radius, material, sides=8, forward_neg=True):
        x0, y0, z0 = loc
        tip_x = x0 - length / 2 if forward_neg else x0 + length / 2
        base_x = x0 + length / 2 if forward_neg else x0 - length / 2
        verts = [(tip_x, y0, z0)]
        for i in range(sides):
            a = 2 * math.pi * i / sides
            verts.append((base_x, y0 + math.cos(a) * radius, z0 + math.sin(a) * radius))
        faces = []
        for i in range(sides):
            faces.append((0, 1 + i, 1 + ((i + 1) % sides)))
        faces.append(tuple(range(sides, 0, -1)))
        self._add(verts, faces, material)

    def loft_x(self, sections, material, sides=8):
        verts = []
        for x, width, height, zc in sections:
            for i in range(sides):
                a = 2 * math.pi * i / sides
                verts.append((x, math.cos(a) * width / 2, zc + math.sin(a) * height / 2))
        faces = []
        for r in range(len(sections) - 1):
            for i in range(sides):
                j = (i + 1) % sides
                faces.append((r * sides + i, r * sides + j, (r + 1) * sides + j, (r + 1) * sides + i))
        faces.append(tuple(range(sides - 1, -1, -1)))
        faces.append(tuple(range((len(sections) - 1) * sides, len(sections) * sides)))
        self._add(verts, faces, material)

    def tri_fin_top(self, x, y_thick, z, length, height, material):
        y0, y1 = -y_thick / 2, y_thick / 2
        verts = [
            (x, y0, z),
            (x + length, y0, z),
            (x + length * 0.55, y0, z + height),
            (x, y1, z),
            (x + length, y1, z),
            (x + length * 0.55, y1, z + height),
        ]
        faces = [(0, 1, 2), (3, 5, 4), (0, 3, 4, 1), (1, 4, 5, 2), (2, 5, 3, 0)]
        self._add(verts, faces, material)

    def tri_fin_side(self, x, y, z, length, out, thick, material, sign):
        z0, z1 = z - thick / 2, z + thick / 2
        verts = [
            (x, y, z0),
            (x + length, y, z0),
            (x + length * 0.4, y + sign * out, z0),
            (x, y, z1),
            (x + length, y, z1),
            (x + length * 0.4, y + sign * out, z1),
        ]
        faces = [(0, 2, 1), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)]
        self._add(verts, faces, material)

    def make(self, name, parent, coll):
        if not self.verts:
            return None
        mesh = bpy.data.meshes.new(name + "_Mesh")
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.update()
        for m in self.mats:
            mesh.materials.append(m)
        for poly, idx in zip(mesh.polygons, self.face_mats):
            poly.material_index = idx
        obj = bpy.data.objects.new(name, mesh)
        coll.objects.link(obj)
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
        obj.location = (0, 0, 0)
        obj.rotation_euler = (0, 0, 0)
        obj.scale = (1, 1, 1)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        return obj


def top_at(x, sections):
    ss = sorted(sections, key=lambda s: s[0])
    if x <= ss[0][0]:
        return ss[0][3] + ss[0][2] / 2
    if x >= ss[-1][0]:
        return ss[-1][3] + ss[-1][2] / 2
    for a, b in zip(ss, ss[1:]):
        if a[0] <= x <= b[0]:
            t = (x - a[0]) / (b[0] - a[0])
            return (a[3] * (1 - t) + b[3] * t) + (a[2] * (1 - t) + b[2] * t) / 2
    return 0


def bottom_at(x, sections):
    ss = sorted(sections, key=lambda s: s[0])
    if x <= ss[0][0]:
        return ss[0][3] - ss[0][2] / 2
    if x >= ss[-1][0]:
        return ss[-1][3] - ss[-1][2] / 2
    for a, b in zip(ss, ss[1:]):
        if a[0] <= x <= b[0]:
            t = (x - a[0]) / (b[0] - a[0])
            return (a[3] * (1 - t) + b[3] * t) - (a[2] * (1 - t) + b[2] * t) / 2
    return 0


def root(coll, name, loc, faction, branch=None, rank=None):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = 4
    obj.location = loc
    obj["ULP_NewFactionSet_SAFE"] = True
    obj["ULP_Faction"] = faction
    if branch:
        obj["ULP_Branch"] = branch
    if rank:
        obj["ULP_Rank"] = rank
    coll.objects.link(obj)
    return obj


def dev_hull_sections(cls, branch, rank):
    t = 0 if rank is None else (rank - 2) / 8
    if cls == "starter":
        L, W, H = 34, 8.5, 6
    elif cls == "frigate":
        L, W, H = 44 + 34 * t, 10 + 4.6 * t, 7 + 2.8 * t
    elif cls == "cruiser":
        L, W, H = 112 + 62 * t, 24 + 9 * t, 15 + 5.5 * t
    else:
        L, W, H = 232 + 80 * t, 48 + 18 * t, 29 + 9 * t
    if branch in {"BrawlerFrigate", "CannonCruiser", "LeviathanHunterBattleship"}:
        W *= 1.10
    if branch in {"ButcherFrigate", "RipperCruiser", "LeviathanProcessorBattleship"}:
        W *= 1.20
        H *= 1.06
    zc = max(0.8, H * 0.16) + H / 2
    sections = [
        (-L * 0.50, W * 0.10, H * 0.14, zc - H * 0.02),
        (-L * 0.40, W * 0.42, H * 0.46, zc),
        (-L * 0.22, W * 0.88, H * 0.82, zc),
        (L * 0.08, W, H, zc),
        (L * 0.32, W * 0.78, H * 0.82, zc),
        (L * 0.48, W * 0.40, H * 0.44, zc - H * 0.02),
    ]
    return L, W, H, sections


def ark_hull_sections(cls, branch, rank):
    t = 0 if rank is None else (rank - 2) / 8
    if cls == "starter":
        L, W, H = 32, 8, 6
    elif cls == "frigate":
        L, W, H = 44 + 34 * t, 10 + 5 * t, 7 + 2.8 * t
    elif cls == "cruiser":
        L, W, H = 112 + 68 * t, 23 + 10 * t, 15 + 6 * t
    else:
        L, W, H = 232 + 86 * t, 48 + 22 * t, 30 + 10 * t
    if branch in {"AutomatonTenderFrigate", "AutomatonRecoveryCruiser", "AutomatonFactoryBattleship"}:
        W *= 1.20
        H *= 1.08
    zc = max(0.8, H * 0.16) + H / 2
    sections = [
        (-L * 0.50, W * 0.28, H * 0.36, zc),
        (-L * 0.36, W * 0.72, H * 0.76, zc),
        (-L * 0.12, W * 0.92, H * 0.94, zc),
        (L * 0.16, W, H, zc),
        (L * 0.38, W * 0.78, H * 0.78, zc),
        (L * 0.50, W * 0.42, H * 0.48, zc),
    ]
    return L, W, H, sections


def stone_hull_sections(cls, branch, rank):
    t = 0 if rank is None else (rank - 2) / 8
    if cls == "starter":
        L, W, H = 34, 11, 5.5
    elif cls == "frigate":
        L, W, H = 46 + 36 * t, 15 + 6 * t, 6.2 + 2.3 * t
    elif cls == "cruiser":
        L, W, H = 112 + 70 * t, 34 + 13 * t, 13 + 4.8 * t
    else:
        L, W, H = 236 + 84 * t, 68 + 24 * t, 25 + 8 * t
    if branch in {"OreTugFrigate", "MagnetHaulerCruiser", "RefineryCruiser", "OreFoundryBattleship"}:
        W *= 1.18
    zc = max(1.3, H * 0.22) + H / 2
    sections = [
        (-L * 0.50, W * 0.34, H * 0.30, zc - H * 0.04),
        (-L * 0.36, W * 0.78, H * 0.55, zc),
        (-L * 0.10, W, H * 0.72, zc + H * 0.03),
        (L * 0.22, W * 0.98, H * 0.70, zc + H * 0.02),
        (L * 0.45, W * 0.62, H * 0.42, zc - H * 0.02),
    ]
    return L, W, H, sections


def add_forward_turret(kit, prefix, x, y, zbase, scale, barrels, housing, barrel, short=True):
    sx, sy, sz = (2.4 * scale, (1.55 + barrels * 0.50) * scale, 1.0 * scale)
    kit.box((x, y, zbase + sz / 2 - 0.06 * scale), (sx, sy, sz), housing)
    length = (2.6 if short else 4.5) * scale
    step = 0.56 * scale
    offs = [0] if barrels == 1 else [(i - (barrels - 1) / 2) * step for i in range(barrels)]
    for off in offs:
        kit.cyl_x((x - sx / 2 - length / 2 + 0.08 * scale, y + off, zbase + sz * 0.58), length, 0.17 * scale, barrel, 8)


def build_devourer_ship(parent, coll, name, cls, branch, rank):
    L, W, H, sec = dev_hull_sections(cls, branch, rank)
    hull = MeshKit(name + "_Hull", [MATS["dev_hull"], MATS["dev_armor"]])
    hull.loft_x(sec, MATS["dev_hull"], 8)
    for i, rx in enumerate([-0.24, 0.06, 0.30]):
        x = L * rx
        hull.box((x, 0, top_at(x, sec) + 0.07), (L * 0.12, W * 0.52, H * 0.045), MATS["dev_armor"])
    hull.make(name + "_MainToothedHull", parent, coll)

    feat = MeshKit(name + "_HarpoonFeature", [MATS["dev_bone"], MATS["dev_metal"]])
    base_scale = max(0.75, H / 8)
    teeth = 2 if cls == "starter" else 3 if cls == "frigate" else 4
    for i in range(teeth):
        y = (i - (teeth - 1) / 2) * W * 0.17
        tooth_len = 1.55 * base_scale
        # Base overlaps the nose ring a little; the previous rough pass left teeth visually detached.
        feat.cone_x((-L * 0.50 - tooth_len / 2 + 0.10 * base_scale, y, sec[1][3]), tooth_len, 0.24 * base_scale, MATS["dev_bone"], 4, True)
    harpoons = 1 if cls == "starter" else 1 + (rank >= 4) + (rank >= 7)
    if cls == "cruiser":
        harpoons += 1 + (rank >= 8)
    if cls == "battleship":
        harpoons += 2 + (rank >= 7) + (rank >= 10)
    h_offsets = [0] if harpoons == 1 else [(i - (harpoons - 1) / 2) * W * 0.18 for i in range(harpoons)]
    for i, y in enumerate(h_offsets):
        x = -L * (0.28 if i < 4 else 0.16)
        z = top_at(x, sec) - 0.04
        s = base_scale * (1.15 if cls != "frigate" else 1.0)
        feat.box((x, y, z + 0.42 * s), (2.0 * s, 1.15 * s, 0.90 * s), MATS["dev_metal"])
        shaft_len = (5.2 if cls == "frigate" else 7.4) * s
        sx = x - 1.0 * s - shaft_len / 2 + 0.10 * s
        sz = z + 0.54 * s
        feat.cyl_x((sx, y, sz), shaft_len, 0.10 * s, MATS["dev_metal"], 6)
        feat.cone_x((sx - shaft_len / 2 - 0.58 * s, y, sz), 1.15 * s, 0.30 * s, MATS["dev_bone"], 4, True)
    feat.make(name + "_ForwardHarpoonsAndTeeth", parent, coll)

    gear = MeshKit(name + "_Weapons", [MATS["dev_metal"], MATS["dev_armor"], MATS["dev_bone"]])
    turret_count = 1 if cls == "starter" else 1 + (rank >= 5) + (rank >= 9)
    if cls == "cruiser":
        turret_count += 1
    if cls == "battleship":
        turret_count += 2
    for i in range(turret_count):
        x = L * [-0.06, 0.16, 0.32, -0.22, 0.42][i % 5]
        y = 0 if i < 3 else (W * 0.20 if i % 2 else -W * 0.20)
        barrels = 2 if branch in {"BrawlerFrigate", "CannonCruiser", "LeviathanHunterBattleship"} else 1
        add_forward_turret(gear, name + f"_StubGun{i+1:02d}", x, y, top_at(x, sec) - 0.04, max(0.68, H / 10), barrels, MATS["dev_metal"], MATS["dev_metal"], True)
    tail_x = L * 0.30
    gear.tri_fin_top(tail_x, W * 0.06, top_at(tail_x, sec) - 0.08, L * 0.12, H * 0.48, MATS["dev_armor"])
    if rank and rank >= 6:
        for sign in (-1, 1):
            gear.tri_fin_side(L * 0.24, sign * W * 0.41, sec[3][3], L * 0.10, W * 0.22, H * 0.07, MATS["dev_armor"], sign)
    gear.make(name + "_ShortGunsAndFins", parent, coll)


def build_ark_ship(parent, coll, name, cls, branch, rank):
    L, W, H, sec = ark_hull_sections(cls, branch, rank)
    hull = MeshKit(name + "_Hull", [MATS["ark_hull"], MATS["ark_panel"]])
    hull.loft_x(sec, MATS["ark_hull"], 8)
    for rx in [-0.30, -0.08, 0.14, 0.34]:
        x = L * rx
        hull.box((x, 0, top_at(x, sec) + 0.08), (L * 0.07, W * 0.72, H * 0.045), MATS["ark_panel"])
    hull.make(name + "_MainModularHull", parent, coll)

    sensor = MeshKit(name + "_XRay", [MATS["ark_dark"], MATS["ark_panel"], MATS["ark_cyan"]])
    count = 1 if cls == "starter" else 1 + (rank >= 7)
    for i in range(count):
        x = L * (-0.16 if i == 0 else 0.18)
        z = top_at(x, sec) - 0.04
        s = max(0.72, H / (8.5 if i == 0 else 11)) * 1.35
        sensor.box((x, 0, z + 0.18 * s), (1.7 * s, 1.35 * s, 0.40 * s), MATS["ark_dark"])
        if cls != "frigate" and i == 0:
            sensor.cyl_z((x, 0, z + 0.92 * s), 1.20 * s, 0.18 * s, MATS["ark_dark"], 8)
            hz = z + 1.55 * s
        else:
            hz = z + 0.65 * s
        sensor.box((x, 0, hz), (1.15 * s, 0.92 * s, 0.70 * s), MATS["ark_panel"])
        sensor.cyl_x((x - 0.64 * s, 0, hz), 0.25 * s, 0.36 * s, MATS["ark_cyan"], 8)
    sensor.make(name + "_FactionXRayModules", parent, coll)

    kit = MeshKit(name + "_Modules", [MATS["ark_dark"], MATS["ark_panel"], MATS["ark_cyan"]])
    if cls != "starter":
        pod_count = 1 + (rank >= 4) + (rank >= 6) + (rank >= 9)
        if cls == "cruiser":
            pod_count += 1
        if cls == "battleship":
            pod_count += 2
        for i in range(pod_count):
            for sign in (-1, 1):
                if "Scout" in branch and i > 1 and sign < 0:
                    continue
                x = L * [-0.26, -0.08, 0.12, 0.30, 0.42][i % 5]
                z = sec[2][3] - H * 0.05
                kit.box((x, sign * W * 0.49, z), (L * 0.06, W * 0.10, H * 0.20), MATS["ark_dark"])
                kit.box((x, sign * W * 0.58, z), (L * 0.08, W * 0.16, H * 0.24), MATS["ark_panel"])
                kit.box((x - L * 0.02, sign * W * 0.66, z), (L * 0.03, W * 0.035, H * 0.09), MATS["ark_cyan"])
    if any(word in branch for word in ["Escort", "Combat", "Citadel"]):
        guns = 1 if cls == "starter" else 1 + (rank >= 4) + (rank >= 7)
        if cls == "cruiser":
            guns += 1
        if cls == "battleship":
            guns += 2
        for i in range(guns):
            x = L * [-0.24, -0.02, 0.18, 0.34, 0.43][i % 5]
            add_forward_turret(kit, name + f"_EscortGun{i+1:02d}", x, 0 if i < 3 else W * (0.18 if i % 2 else -0.18), top_at(x, sec) - 0.04, max(0.65, H / 11), 1, MATS["ark_dark"], MATS["ark_dark"], True)
    else:
        arm_count = 1 if "Tender" in branch or "Recovery" in branch or "Factory" in branch else 0
        if cls == "battleship":
            arm_count += 1
        for i in range(arm_count):
            x = L * (0.08 + i * 0.18)
            z = top_at(x, sec)
            kit.box((x, 0, z + 0.25), (L * 0.07, W * 0.18, 0.55), MATS["ark_dark"])
            kit.box((x - L * 0.03, 0, z + 0.70), (L * 0.11, 0.32, 0.26), MATS["ark_dark"])
    kit.make(name + "_ModularPodsAndTools", parent, coll)


def build_stone_ship(parent, coll, name, cls, branch, rank):
    L, W, H, sec = stone_hull_sections(cls, branch, rank)
    hull = MeshKit(name + "_Hull", [MATS["stone_hull"], MATS["stone_plate"]])
    hull.loft_x(sec, MATS["stone_hull"], 8)
    for rx in [-0.28, -0.10, 0.08, 0.27]:
        x = L * rx
        hull.box((x, 0, top_at(x, sec) + 0.09), (L * 0.11, W * 0.80, H * 0.05), MATS["stone_plate"])
    hull.make(name + "_MainArmoredHull", parent, coll)

    mag = MeshKit(name + "_Magnets", [MATS["stone_under"], MATS["stone_magnet"]])
    magnet_count = 1 if cls == "starter" else 1 + (rank >= 6) * 2
    positions = [(-L * 0.08, 0)]
    if magnet_count > 1:
        positions.extend([(L * 0.15, W * 0.36), (L * 0.15, -W * 0.36)])
    for i, (x, y) in enumerate(positions):
        s = max(0.95, H / (6.5 if cls != "frigate" else 7.4))
        b = bottom_at(x, sec) + 0.05
        mag.box((x, y, b - 0.28 * s), (0.40 * s, 0.40 * s, 0.65 * s), MATS["stone_under"])
        z = b - 0.92 * s
        mag.box((x + 0.32 * s, y, z), (0.30 * s, 1.12 * s, 0.42 * s), MATS["stone_under"])
        mag.box((x - 0.12 * s, y + 0.40 * s, z), (0.90 * s, 0.24 * s, 0.34 * s), MATS["stone_under"])
        mag.box((x - 0.12 * s, y - 0.40 * s, z), (0.90 * s, 0.24 * s, 0.34 * s), MATS["stone_under"])
        mag.box((x + 0.52 * s, y, z), (0.10 * s, 0.70 * s, 0.32 * s), MATS["stone_magnet"])
    mag.make(name + "_FactionMagnets", parent, coll)

    kit = MeshKit(name + "_GunsAndIndustry", [MATS["stone_plate"], MATS["stone_under"], MATS["stone_amber"]])
    gun_count = 1 if cls == "starter" else 1 + (rank >= 4) + (rank >= 8)
    if cls == "cruiser":
        gun_count += 1
    if cls == "battleship":
        gun_count += 2
    for i in range(gun_count):
        x = L * [-0.25, -0.06, 0.13, 0.30, 0.42][i % 5]
        y = 0 if i < 3 else W * (0.18 if i % 2 else -0.18)
        add_forward_turret(kit, name + f"_MassiveSingleGun{i+1:02d}", x, y, top_at(x, sec) - 0.05, max(0.82, H / 8.8), 1, MATS["stone_plate"], MATS["stone_under"], True)
    if any(word in branch for word in ["Ore", "Hauler", "Refinery", "Foundry"]):
        for i, rx in enumerate([-0.20, 0.02, 0.24]):
            x = L * rx
            b = bottom_at(x, sec)
            kit.box((x, 0, b - 0.23), (L * 0.08, W * 0.38, 0.48), MATS["stone_under"])
            kit.box((x - L * 0.02, W * 0.20, b - 0.23), (L * 0.02, 0.06, 0.18), MATS["stone_amber"])
    kit.make(name + "_MiningGunsAndUndersideGear", parent, coll)


def build_all():
    ranks = range(2, 11)
    start_y = 3150
    faction_gap = 1820
    row_gap = 175
    spacing = {"frigate": 112, "cruiser": 220, "battleship": 380}
    specs = [
        (
            "Devourer",
            "ULP_Devourer",
            COLLS["Devourer"],
            build_devourer_ship,
            ("StarterHarpoonSkiff", "starter"),
            [
                ("HarpoonFrigate", "frigate"),
                ("BrawlerFrigate", "frigate"),
                ("ButcherFrigate", "frigate"),
                ("HarpoonCruiser", "cruiser"),
                ("CannonCruiser", "cruiser"),
                ("RipperCruiser", "cruiser"),
                ("LeviathanHunterBattleship", "battleship"),
                ("LeviathanProcessorBattleship", "battleship"),
            ],
        ),
        (
            "ClockworkArk",
            "ULP_ClockworkArk",
            COLLS["ClockworkArk"],
            build_ark_ship,
            ("StarterXRayPod", "starter"),
            [
                ("XRayScoutFrigate", "frigate"),
                ("AutomatonTenderFrigate", "frigate"),
                ("EscortFrigate", "frigate"),
                ("ModularSurveyCruiser", "cruiser"),
                ("AutomatonRecoveryCruiser", "cruiser"),
                ("CombatArkCruiser", "cruiser"),
                ("MechanizedCitadelBattleship", "battleship"),
                ("AutomatonFactoryBattleship", "battleship"),
            ],
        ),
        (
            "StoneVault",
            "ULP_StoneVault",
            COLLS["StoneVault"],
            build_stone_ship,
            ("StarterMagnetDigger", "starter"),
            [
                ("MagnetScoutFrigate", "frigate"),
                ("SingleGunDiggerFrigate", "frigate"),
                ("OreTugFrigate", "frigate"),
                ("ArmoredGunCruiser", "cruiser"),
                ("MagnetHaulerCruiser", "cruiser"),
                ("RefineryCruiser", "cruiser"),
                ("SiegeDiggerBattleship", "battleship"),
                ("OreFoundryBattleship", "battleship"),
            ],
        ),
    ]
    expected = []
    for fi, (faction, prefix, coll, builder, starter, branches) in enumerate(specs):
        fy = start_y + fi * faction_gap
        sname, scls = starter
        sroot_name = f"{prefix}_{sname}_ROOT"
        sr = root(coll, sroot_name, (-170, fy - 115, 0), faction, sname, None)
        builder(sr, coll, sroot_name[:-5], scls, sname, None)
        expected.append(sroot_name)
        for bi, (branch, cls) in enumerate(branches):
            y = fy + bi * row_gap
            for ri, rank in enumerate(ranks):
                rname = f"{prefix}_{branch}_Polished_R{rank:02d}_ROOT"
                rr = root(coll, rname, (ri * spacing[cls], y, 0), faction, branch, rank)
                builder(rr, coll, rname[:-5], cls, branch, rank)
                expected.append(rname)
    return expected


def world_bbox(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    return (
        Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
        Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))),
    )


def bboxes_touch(a, b, margin=0.04):
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


def signed_volume(obj):
    if obj.type != "MESH":
        return 0
    me = obj.data
    mw = obj.matrix_world
    vol = 0.0
    for poly in me.polygons:
        ids = poly.vertices
        if len(ids) < 3:
            continue
        v0 = mw @ me.vertices[ids[0]].co
        for i in range(1, len(ids) - 1):
            v1 = mw @ me.vertices[ids[i]].co
            v2 = mw @ me.vertices[ids[i + 1]].co
            vol += v0.dot(v1.cross(v2)) / 6.0
    return vol


def audit(expected):
    bpy.context.view_layer.update()
    roots = [bpy.data.objects[n] for n in expected if bpy.data.objects.get(n)]
    missing = [n for n in expected if not bpy.data.objects.get(n)]
    floating = []
    negative_volume = []
    negative_scale = []
    meshes = []
    for r in roots:
        kids = [o for o in r.children if o.type == "MESH"]
        meshes.extend(kids)
        if len(kids) > 1:
            bbs = {o: world_bbox(o) for o in kids}
            hulls = [o for o in kids if "Hull" in o.name]
            for o in kids:
                if "Hull" in o.name:
                    continue
                if not any(bboxes_touch(bbs[o], bbs[h]) for h in hulls):
                    floating.append(o.name)
    for o in meshes:
        if any(v < -0.0001 for v in o.scale):
            negative_scale.append(o.name)
        vol = signed_volume(o)
        if vol < -0.0001:
            o.data.flip_normals()
            o.data.update()
    bpy.context.view_layer.update()
    for o in meshes:
        vol = signed_volume(o)
        if vol < -0.0001:
            negative_volume.append((o.name, vol))
    return {
        "expected_roots": len(expected),
        "actual_roots": len(roots),
        "missing_roots": missing,
        "mesh_objects": len(meshes),
        "floating_parts": floating,
        "negative_scale": negative_scale,
        "negative_volume": negative_volume,
    }


def write_notes(report):
    for name in ["ULP_NewFactions_SAFE_DesignNotes.txt", "ULP_NewFactions_SAFE_Audit.txt"]:
        old = bpy.data.texts.get(name)
        if old:
            bpy.data.texts.remove(old)
    notes = bpy.data.texts.new("ULP_NewFactions_SAFE_DesignNotes.txt")
    notes.write("Safe generated ship sets for Devourer, Clockwork Ark, and Stone Vault.\\n")
    notes.write("Each faction has 1 starter ship plus 8 branches with ranks R02-R10.\\n")
    notes.write("Per faction: 3 frigate branches, 3 cruiser branches, 2 battleship branches.\\n")
    notes.write("Devourer: harpoons forward, teeth, short large-caliber guns.\\n")
    notes.write("Clockwork Ark: X-Ray on every ship, modular automaton-harvesting pods.\\n")
    notes.write("Stone Vault: magnet on every ship, squat armored mining hulls.\\n")
    audit_text = bpy.data.texts.new("ULP_NewFactions_SAFE_Audit.txt")
    for key, value in report.items():
        if isinstance(value, list):
            audit_text.write(f"{key}: {len(value)} {value[:40]}\\n")
        else:
            audit_text.write(f"{key}: {value}\\n")


expected_roots = build_all()
report = audit(expected_roots)
write_notes(report)

if OUTPUT_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=OUTPUT_BLEND)
else:
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)

print("ULP_NEW_FACTIONS_SAFE_GENERATION_DONE")
print(report)
