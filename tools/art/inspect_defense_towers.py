"""Render existing tower sources without saving changes to the master."""
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Builds/ArtReview/SolidDefenseTowers"
OUTPUT.mkdir(parents=True, exist_ok=True)
original_scene = bpy.context.scene
for source_name, identity in (("fire siege tower", "fire"), ("stone fortress 3d model", "cannon")):
    scene = bpy.data.scenes.new("Temporary source inspection")
    bpy.context.window.scene = scene
    source = bpy.data.objects[source_name]
    obj = source.copy()
    obj.parent = None
    obj.location = (0, 0, 0)
    obj.rotation_euler = (0, 0, 0)
    scene.collection.objects.link(obj)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = False
    scene.display.shading.show_specular_highlight = False
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.55
    camera.location = (1.5, -2, 1.4)
    camera.rotation_euler = (Vector((0, 0, .4)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = str(OUTPUT / (identity + "-before.png"))
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene = original_scene
    bpy.data.scenes.remove(scene)
