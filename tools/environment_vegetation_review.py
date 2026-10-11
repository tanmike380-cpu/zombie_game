"""Render an isolated native-tree/RTS-LOD comparison without saving source changes."""
import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from environment_vegetation import append_source_object, load_recipe, read_mesh_record


def place_tree(obj, center_x):
    """Move a whole authored tree by translation only for the comparison gallery."""
    bpy.context.view_layer.update()
    record = read_mesh_record(obj, bpy.context.evaluated_depsgraph_get())
    center = Vector(((record["bounds_min"][0] + record["bounds_max"][0]) / 2,
                     (record["bounds_min"][1] + record["bounds_max"][1]) / 2,
                     record["bounds_min"][2]))
    obj.location += Vector((center_x, 0, 0)) - center
    obj.hide_render = False


def add_label(label, center_x):
    """Add review-only readable labels, never game exports."""
    text = bpy.data.curves.new("Review label", "FONT")
    text.body = label
    text.align_x = "CENTER"
    text.size = .25
    obj = bpy.data.objects.new("Review label " + label, text)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = (center_x, -1.3, -.25)
    obj.rotation_euler = (math.pi / 2, 0, 0)


def configure_review(output_path):
    """Use a neutral shared view/exposure for all three meshes in the temporary process."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 12
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 650
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(output_path)
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.8, .8, .8, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .65
    light = bpy.data.lights.new("Review-only sun", "SUN")
    light.energy = 2.5
    light_obj = bpy.data.objects.new("Review-only sun", light)
    scene.collection.objects.link(light_obj)
    light_obj.rotation_euler = (math.radians(35), math.radians(-20), math.radians(-35))
    camera = bpy.data.cameras.new("Review-only camera")
    camera.type = "ORTHO"
    camera.ortho_scale = 17
    camera_obj = bpy.data.objects.new("Review-only camera", camera)
    scene.collection.objects.link(camera_obj)
    camera_obj.location = (6, -18, 9)
    camera_obj.rotation_euler = (Vector((6, 0, 2.0)) - camera_obj.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera_obj
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0


def render_comparison(recipe_path, output_path):
    """Render source and derivatives; all mutations die with the background process."""
    if not bpy.app.background or "Vegetation_Review_v" not in bpy.data.filepath:
        raise ValueError("Review requires an isolated saved vegetation staging source")
    recipe = load_recipe(recipe_path)
    for obj in bpy.context.scene.objects:
        obj.hide_render = True
    collection = bpy.data.collections.new("Review-only native comparison")
    bpy.context.scene.collection.children.link(collection)
    tree_source = next(asset for asset in recipe["assets"] if asset["id"] == "TreeSmall_LOD0")
    native = append_source_object(dict(tree_source, id="Native tree reference"), collection)
    for obj, position, label in ((native, 0, "Native LOD1"),
                                 (bpy.data.objects["TreeSmall_LOD0"], 6, "RTS near"),
                                 (bpy.data.objects["TreeSmall_LOD1"], 12, "RTS far")):
        place_tree(obj, position)
        triangles = sum(len(face.vertices) - 2 for face in obj.data.polygons)
        add_label(f"{label} | {triangles:,} tris", position)
    output_path = Path(output_path).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    configure_review(output_path)
    bpy.ops.render.render(write_still=True)
    print(f"VEGETATION_VISUAL_REVIEW {output_path}", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--recipe", required=True)
    parser.add_argument("--output", required=True)
    arguments = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    render_comparison(arguments.recipe, arguments.output)
