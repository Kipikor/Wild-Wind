import argparse
from pathlib import Path

import bpy


def parse_args():
    argv = []
    if "--" in __import__("sys").argv:
        argv = __import__("sys").argv[__import__("sys").argv.index("--") + 1:]

    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    return parser.parse_args(argv)


def main():
    args = parse_args()
    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    for obj in bpy.data.objects:
        obj.select_set(False)
    exported = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue

        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.hide_select = False
        obj.select_set(True)
        exported.append(obj.name)

    if not exported:
        raise RuntimeError("No mesh objects found for export")

    bpy.ops.export_scene.fbx(
        filepath=str(out_path),
        use_selection=True,
        object_types={"MESH"},
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

    print("EXPORTED", out_path)
    print("MESH_COUNT", len(exported))
    for name in exported:
        print("MESH", name)


if __name__ == "__main__":
    main()
