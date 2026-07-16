import argparse
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


def parse_args():
    argv = []
    if "--" in __import__("sys").argv:
        argv = __import__("sys").argv[__import__("sys").argv.index("--") + 1:]

    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--apply-world", action="store_true")
    parser.add_argument(
        "--unity-module-y-up",
        action="store_true",
        help="Bake reusable module meshes so Blender Z becomes Unity Y and Blender -Y becomes Unity +Z.")
    return parser.parse_args(argv)


def visible_mesh_objects():
    objects = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue

        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.hide_select = False
        objects.append(obj)

    return objects


def bake_world_meshes(objects):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    baked = []
    for source in objects:
        evaluated = source.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(
            evaluated,
            depsgraph=depsgraph,
            preserve_all_data_layers=True)
        mesh.transform(source.matrix_world)
        mesh.update(calc_edges=True)
        force_bake_missing_x_mirror(source, mesh)

        for material in source.data.materials:
            if material is not None and material.name not in mesh.materials:
                mesh.materials.append(material)

        obj = bpy.data.objects.new(source.name, mesh)
        bpy.context.collection.objects.link(obj)
        baked.append(obj)

    return baked


def force_bake_missing_x_mirror(source, mesh):
    # Side-mounted objects can legitimately have their own local Mirror modifier.
    # Do not mirror those across world X=0, or a right-side mount becomes a
    # full-width mesh whose bounds center sits on the ship centerline.
    source_name = (source.name or "").lower()
    if "_left" in source_name or "_right" in source_name:
        return

    try:
        if abs(source.matrix_world.translation.x) > 0.0001:
            return
    except Exception:
        return

    has_x_mirror = False
    for modifier in source.modifiers:
        if modifier.type != "MIRROR":
            continue

        try:
            if modifier.use_axis[0]:
                has_x_mirror = True
                break
        except Exception:
            has_x_mirror = True
            break

    if not has_x_mirror or not mesh.vertices or not mesh.polygons:
        return

    min_x = min(vertex.co.x for vertex in mesh.vertices)
    max_x = max(vertex.co.x for vertex in mesh.vertices)
    if min_x < -0.0001 or max_x <= 0.0001:
        return

    source_vertices = [vertex.co.copy() for vertex in mesh.vertices]
    source_polygons = [list(poly.vertices) for poly in mesh.polygons]
    source_material_indices = [poly.material_index for poly in mesh.polygons]
    source_uv_layers = []
    for uv_layer in mesh.uv_layers:
        source_uv_layers.append([data.uv.copy() for data in uv_layer.data])

    vertex_offset = len(source_vertices)
    mirrored_vertices = [Vector((-vertex.x, vertex.y, vertex.z)) for vertex in source_vertices]
    mesh.clear_geometry()
    mesh.from_pydata(
        source_vertices + mirrored_vertices,
        [],
        source_polygons + [[vertex_offset + index for index in reversed(poly)] for poly in source_polygons])
    mesh.update(calc_edges=True)

    for poly_index, material_index in enumerate(source_material_indices + source_material_indices):
        mesh.polygons[poly_index].material_index = material_index

    for source_uv_layer_index, source_uvs in enumerate(source_uv_layers):
        if source_uv_layer_index >= len(mesh.uv_layers):
            uv_layer = mesh.uv_layers.new(name="UVMap" if source_uv_layer_index == 0 else "UVMap." + str(source_uv_layer_index))
        else:
            uv_layer = mesh.uv_layers[source_uv_layer_index]

        original_loop_count = len(source_uvs)
        for loop_index in range(min(original_loop_count, len(uv_layer.data))):
            uv_layer.data[loop_index].uv = source_uvs[loop_index]
        for loop_index in range(original_loop_count, len(uv_layer.data)):
            source_index = loop_index - original_loop_count
            if 0 <= source_index < original_loop_count:
                uv_layer.data[loop_index].uv = source_uvs[source_index]


def select_only(objects):
    for obj in bpy.data.objects:
        obj.select_set(False)

    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        obj.hide_select = False
        obj.select_set(True)

    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def recalculate_selected_normals_outward(objects):
    if not objects:
        return

    select_only(objects)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake_unity_module_y_up(objects):
    rotation = Matrix.Rotation(-math.pi * 0.5, 4, "X")
    for obj in objects:
        if obj.type != "MESH" or obj.data is None:
            continue

        obj.data.transform(rotation)
        obj.data.update(calc_edges=True)


def main():
    args = parse_args()
    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)

    sources = visible_mesh_objects()
    if not sources:
        raise RuntimeError("No mesh objects found for export")

    exported_objects = bake_world_meshes(sources) if args.apply_world else sources
    if args.unity_module_y_up:
        bake_unity_module_y_up(exported_objects)

    recalculate_selected_normals_outward(exported_objects)
    select_only(exported_objects)

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
    print("MESH_COUNT", len(exported_objects))
    for obj in exported_objects:
        print("MESH", obj.name)


if __name__ == "__main__":
    main()
