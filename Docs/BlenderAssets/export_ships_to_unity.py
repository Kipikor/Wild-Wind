import argparse
import datetime as _datetime
import json
import re
import sys
from pathlib import Path

import bpy


SCHEMA = "wildwind.blender_ship_manifest.v1"
DEFAULT_OUT_ROOT = "Assets/ShipImports"
DEFAULT_COLLECTION = ""


WEAPON_KEYWORDS = (
    "launcher", "missile", "rocket", "torpedo", "turret", "gun", "cannon",
    "barrel", "muzzle", "pmk", "autocannon", "machinegun", "battery",
    "harpoon", "tube", "cell", "mg", "mm", "defense", "individual",
    "mantlet",
)

EQUIPMENT_KEYWORDS = (
    "xray", "x_ray", "x-ray", "scanner", "sensor", "radar", "dome",
    "magnet", "siphon", "separator", "repair", "welder", "beam",
    "factory", "industrial", "cargo", "crane", "claw", "manipulator",
    "intake", "repeller", "module", "equipment", "pod", "tank", "core",
    "canister", "cylinder", "drum", "engine", "reactor", "bay",
    "antenna", "claudium", "comms", "mast",
    "funnel", "afterburner", "nozzle", "hackingdish", "hackdish", "dish",
    "receiver", "feed", "boom", "node", "parabolic", "pedestal", "yawmesh",
)

HULL_KEYWORDS = (
    "hull", "mainhull", "body", "armor", "armour", "fin", "wing", "tail",
    "rudder", "keel", "nose", "prow", "spine", "belt", "plate", "deck",
    "block", "shell", "rootedintohull", "pylon", "strut", "brace",
    "mount", "base", "housing", "panel", "cap", "rib", "skirt",
    "tooth", "teeth", "tusk", "jaw", "arch", "box", "bracket",
    "bridge", "command", "citadel", "superstructure", "tier", "roof",
    "rooted", "rankprogression", "platform", "step", "lip", "hatch",
    "egg", "round",
)

DETAIL_KEYWORDS = (
    "line", "glow", "window", "light", "lamp", "decal", "flag", "trim",
    "stripe", "mark", "emblem", "inset", "cyan", "amber", "maroon",
    "accent", "vent", "duct", "grille", "slot", "darkport",
    "chamferhint",
)


def parse_args():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    parser = argparse.ArgumentParser(description="Export Wild Wind Blender ship roots to Unity FBX + manifest files.")
    parser.add_argument("--project-root", default=str(Path.cwd()), help="Unity project root. Defaults to current working directory.")
    parser.add_argument("--out", default=DEFAULT_OUT_ROOT, help="Output root inside the Unity project.")
    parser.add_argument("--ship", action="append", default=[], help="Ship root object name. Can be passed more than once.")
    parser.add_argument("--collection", default=DEFAULT_COLLECTION, help="Collection to export roots from.")
    parser.add_argument("--faction", action="append", default=[], help="Faction filter after root detection. Can be passed more than once.")
    parser.add_argument("--all", action="store_true", help="Export every object whose name ends with _ROOT.")
    parser.add_argument("--actual-ships", action="store_true", help="Keep only rank-line ships and one starter ship per faction.")
    parser.add_argument("--list", action="store_true", help="List matching roots without exporting.")
    parser.add_argument("--manifest-only", action="store_true", help="Rewrite JSON manifests without exporting FBX files.")
    parser.add_argument("--quiet", action="store_true", help="Only print a short completion summary.")
    return parser.parse_args(argv)


def safe_name(value):
    value = value or "unnamed"
    value = re.sub(r"[^A-Za-z0-9_.-]+", "_", value)
    return value.strip("._") or "unnamed"


def normalize_for_match(value):
    return re.sub(r"[^a-z0-9]+", "", (value or "").lower())


def detect_faction(name):
    compact = normalize_for_match(name)
    if "windhouse" in compact or "wildhouse" in compact:
        return "WindHouse"
    if "devourer" in compact:
        return "Devourer"
    if "clockworkark" in compact or "ark" in compact:
        return "ClockworkArk"
    if "stonevault" in compact or "vault" in compact:
        return "StoneVault"
    if "synod" in compact or "mist" in compact:
        return "MistSynod"
    if "imperial" in compact or "empire" in compact:
        return "Imperial"
    imperial_roots = (
        "battleship330", "cargofrigate", "industrialbattleship",
        "industrialcruiser", "lightartillerycruiser", "lightreconcruiser",
        "logisticsfrigate", "longrangeheavycruiser", "patrolfrigate",
        "pioneer", "reconfrigate", "repaircruiser",
    )
    if any(root in compact for root in imperial_roots):
        return "Imperial"
    return "UnknownFaction"


def detect_rank(name):
    if is_starter_root(name):
        return "R01"

    match = re.search(r"(?:^|_)R(\d{1,2})(?:_|$)", name or "", re.IGNORECASE)
    if not match:
        return ""
    return "R" + match.group(1).zfill(2)


def is_starter_root(name):
    name = name or ""
    compact = normalize_for_match(name)
    if "starter" in compact or compact.endswith("pioneerroot") or compact == "ulppioneerroot":
        return True

    starters = {
        "ULP_Pioneer_ROOT",
        "ULP_WindHouseStarterFrigate_ROOT",
        "ULP_MistSynodStarterFrigate_ROOT",
        "ULP_ClockworkArk_StarterXRayPod_ROOT",
        "ULP_Devourer_StarterHarpoonSkiff_ROOT",
        "ULP_StoneVault_StarterMagnetDigger_ROOT",
    }
    return name in starters


def detect_tier(name):
    rank = detect_rank(name)
    match = re.match(r"R(\d{1,2})$", rank or "", re.IGNORECASE)
    if not match:
        return 0
    return int(match.group(1))


def detect_class(name):
    compact = normalize_for_match(name)
    if is_starter_root(name):
        return "Starter"
    if "battleship" in compact or "lin" in compact:
        return "Battleship"
    if "cruiser" in compact:
        return "Cruiser"
    if "frigate" in compact or "skiff" in compact:
        return "Frigate"
    return "UnknownClass"


def is_actual_ship_root(name):
    name = name or ""
    if re.search(r"_Polished_R\d{2}_ROOT$", name, re.IGNORECASE):
        return True

    starters = {
        "ULP_Pioneer_ROOT",
        "ULP_WindHouseStarterFrigate_ROOT",
        "ULP_MistSynodStarterFrigate_ROOT",
        "ULP_ClockworkArk_StarterXRayPod_ROOT",
        "ULP_Devourer_StarterHarpoonSkiff_ROOT",
        "ULP_StoneVault_StarterMagnetDigger_ROOT",
    }
    return name in starters


def detect_branch(name):
    branch = re.sub(r"^ULP_", "", name or "")
    branch = re.sub(r"_ROOT$", "", branch)
    branch = re.sub(r"_Polished", "", branch)
    branch = re.sub(r"_R\d{1,2}(?=_|$)", "", branch)
    return branch


def custom_role(obj):
    for key in ("ULP_PartRole", "ShipPartRole", "part_role", "role"):
        value = obj.get(key)
        if value:
            value = str(value).strip().lower()
            if value in ("hull", "weapon", "equipment", "detail"):
                return value.title()
    return ""


def local_part_name(root, obj):
    name = obj.name or ""
    ship_id = root.name[:-5] if root.name.endswith("_ROOT") else root.name
    if name.startswith(ship_id + "_"):
        return name[len(ship_id) + 1:]
    root_prefix = root.name + "_"
    if name.startswith(root_prefix):
        return name[len(root_prefix):]
    return name


def classify_role(root, obj):
    explicit = custom_role(obj)
    if explicit:
        return explicit

    compact = normalize_for_match(local_part_name(root, obj))
    if any(keyword in compact for keyword in WEAPON_KEYWORDS):
        return "Weapon"
    if any(keyword in compact for keyword in EQUIPMENT_KEYWORDS):
        return "Equipment"
    if any(keyword in compact for keyword in HULL_KEYWORDS):
        return "Hull"
    if any(keyword in compact for keyword in DETAIL_KEYWORDS):
        return "Detail"
    return "Unknown"


def classify_category(root, obj, role):
    compact = normalize_for_match(local_part_name(root, obj))
    categories = (
        ("Harpoon", ("harpoon",)),
        ("TorpedoLauncher", ("torpedo",)),
        ("MissileLauncher", ("launcher", "missile", "rocket")),
        ("MainGun", ("turret", "cannon", "gun", "barrel", "mm", "mantlet", "individual")),
        ("Autocannon", ("autocannon", "machinegun", "pmk", "mg", "defense")),
        ("RepairBeam", ("repair", "welder", "beam")),
        ("Scanner", ("xray", "scanner", "sensor", "radar", "dome", "antenna", "comms", "mast", "hackingdish", "hackdish", "dish", "receiver", "feed", "boom", "node", "parabolic", "pedestal", "yawmesh")),
        ("Engine", ("funnel", "afterburner", "nozzle")),
        ("Magnet", ("magnet",)),
        ("GasSiphon", ("siphon", "separator")),
        ("Industrial", ("factory", "industrial", "cargo", "crane", "claw", "manipulator", "intake", "pod", "tank", "core", "canister", "bay", "claudium")),
        ("HullFin", ("fin", "wing", "tail", "rudder")),
        ("HullArmor", ("armor", "armour", "plate", "belt")),
        ("Hull", ("hull", "body", "nose", "prow", "keel", "spine", "pylon", "mount", "base", "panel", "cap", "rib", "tooth", "tusk", "jaw", "box", "bracket", "bridge", "command", "citadel", "superstructure", "tier", "roof", "rooted", "platform", "step", "lip", "hatch", "egg", "round")),
        ("Detail", ("line", "glow", "window", "light", "lamp", "decal", "flag", "trim", "inset", "accent", "vent", "duct", "grille", "slot", "darkport", "chamferhint")),
    )

    for category, keywords in categories:
        if any(keyword in compact for keyword in keywords):
            return category
    return role


def iter_collection_recursive(collection):
    for obj in collection.objects:
        yield obj
    for child in collection.children:
        yield from iter_collection_recursive(child)


def get_roots(args):
    roots = []
    seen = set()

    def add_root(obj):
        if obj and obj.name not in seen:
            seen.add(obj.name)
            roots.append(obj)

    for ship_name in args.ship:
        obj = bpy.data.objects.get(ship_name)
        if obj is None and not ship_name.endswith("_ROOT"):
            obj = bpy.data.objects.get(ship_name + "_ROOT")
        if obj is None:
            raise RuntimeError("Ship root not found: " + ship_name)
        add_root(obj)

    if args.collection:
        collection = bpy.data.collections.get(args.collection)
        if collection is None:
            raise RuntimeError("Collection not found: " + args.collection)
        for obj in iter_collection_recursive(collection):
            if obj.name.endswith("_ROOT"):
                add_root(obj)

    if args.all or (not roots and not args.collection and not args.ship):
        for obj in bpy.data.objects:
            if obj.name.endswith("_ROOT"):
                add_root(obj)

    if args.actual_ships:
        roots = [root for root in roots if is_actual_ship_root(root.name)]

    if args.faction:
        wanted_factions = {normalize_for_match(faction) for faction in args.faction}
        roots = [root for root in roots if normalize_for_match(detect_faction(root.name)) in wanted_factions]

    roots.sort(key=lambda item: item.name)
    return roots


def collect_hierarchy(root):
    result = []
    stack = [root]
    while stack:
        obj = stack.pop()
        result.append(obj)
        children = list(obj.children)
        children.sort(key=lambda item: item.name, reverse=True)
        stack.extend(children)
    return result


def blender_object_path(root, obj):
    parts = []
    cursor = obj
    while cursor is not None:
        parts.append(cursor.name)
        if cursor == root:
            break
        cursor = cursor.parent
    return "/".join(reversed(parts))


def asset_path(project_root, full_path):
    project_root = project_root.resolve()
    full_path = full_path.resolve()
    try:
        return full_path.relative_to(project_root).as_posix()
    except ValueError:
        return full_path.as_posix()


def source_collection_name(root):
    if root.users_collection:
        return root.users_collection[0].name
    return ""


def make_manifest(root, hierarchy, project_root, model_path):
    parts = []
    for obj in hierarchy:
        if obj.type != "MESH":
            continue
        role = classify_role(root, obj)
        parts.append({
            "objectName": obj.name,
            "path": blender_object_path(root, obj),
            "role": role,
            "category": classify_category(root, obj, role),
        })

    return {
        "schema": SCHEMA,
        "shipId": root.name[:-5] if root.name.endswith("_ROOT") else root.name,
        "rootName": root.name,
        "faction": detect_faction(root.name),
        "branch": detect_branch(root.name),
        "rank": detect_rank(root.name),
        "tier": detect_tier(root.name),
        "isStarter": is_starter_root(root.name),
        "shipClass": detect_class(root.name),
        "sourceBlend": bpy.data.filepath,
        "sourceCollection": source_collection_name(root),
        "exportedAtUtc": _datetime.datetime.now(_datetime.UTC).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "forwardAxis": "FBX axis_forward=-Z axis_up=Y; Unity prefab root is identity, visual child carries FBX axis correction",
        "modelAssetPath": asset_path(project_root, model_path),
        "parts": parts,
    }


def ensure_select_only(objects, active):
    make_view_layer_exportable()
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_select = False
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active


def make_view_layer_exportable():
    for collection in bpy.data.collections:
        collection.hide_viewport = False
        collection.hide_render = False

    def visit_layer_collection(layer_collection):
        layer_collection.exclude = False
        layer_collection.hide_viewport = False
        layer_collection.holdout = False
        layer_collection.indirect_only = False
        for child in layer_collection.children:
            visit_layer_collection(child)

    visit_layer_collection(bpy.context.view_layer.layer_collection)


def export_root(root, project_root, out_root, manifest_only=False):
    hierarchy = collect_hierarchy(root)
    ship_id = root.name[:-5] if root.name.endswith("_ROOT") else root.name
    faction = detect_faction(root.name)
    model_dir = out_root / "Models" / safe_name(faction)
    manifest_dir = out_root / "Manifests" / safe_name(faction)
    model_dir.mkdir(parents=True, exist_ok=True)
    manifest_dir.mkdir(parents=True, exist_ok=True)

    model_path = model_dir / (safe_name(ship_id) + ".fbx")
    manifest_path = manifest_dir / (safe_name(ship_id) + ".ship.json")

    if not manifest_only:
        ensure_select_only(hierarchy, root)
        bpy.ops.export_scene.fbx(
            filepath=str(model_path),
            use_selection=True,
            object_types={"EMPTY", "MESH"},
            add_leaf_bones=False,
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            use_triangles=True,
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            global_scale=1.0,
            path_mode="AUTO",
        )

    manifest = make_manifest(root, hierarchy, project_root, model_path)
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=True), encoding="utf-8")
    return ship_id, model_path, manifest_path, len(manifest["parts"])


def main():
    args = parse_args()
    project_root = Path(args.project_root).resolve()
    out_root = Path(args.out)
    if not out_root.is_absolute():
        out_root = project_root / out_root

    roots = get_roots(args)
    if args.list:
        for root in roots:
            print(root.name)
        print("Roots:", len(roots))
        return

    if not roots:
        raise RuntimeError("No ship roots matched export selectors.")

    out_root.mkdir(parents=True, exist_ok=True)
    if not args.quiet:
        print("Exporting", len(roots), "ship root(s) to", out_root)
    for root in roots:
        ship_id, model_path, manifest_path, part_count = export_root(root, project_root, out_root, args.manifest_only)
        if not args.quiet:
            print("  exported" if not args.manifest_only else "  wrote manifest", ship_id, "parts", part_count)
            print("    model   ", asset_path(project_root, model_path))
            print("    manifest", asset_path(project_root, manifest_path))
    if args.quiet:
        print("Wrote", len(roots), "ship manifest(s)" if args.manifest_only else "ship export(s)")


if __name__ == "__main__":
    main()
