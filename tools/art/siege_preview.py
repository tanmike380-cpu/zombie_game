"""Blender-only staging and flame timing placeholders, not production Unity VFX."""
import math
import random

import bpy
from mathutils import Vector

from siege_motion import FRAME_END, interpolate_keys
from tripo_model_io import point_at


def create_material(name, color, emission=0):
    """Create a review-stage material, never modify imported character materials."""
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Roughness"].default_value = .8
    if emission:
        shader.inputs["Emission Color"].default_value = (*color, 1)
        shader.inputs["Emission Strength"].default_value = emission
    return material


def add_fixture_cube(name, location, scale, material):
    """Add a presentation-only ground/wall element."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj["review_fixture"] = True
    obj.scale = scale
    obj.data.materials.append(material)
    bevel = obj.modifiers.new("Soft masonry edges", "BEVEL")
    bevel.width = .016
    bevel.segments = 2
    return obj


def create_stage(scene, kind):
    """Show baked Tripo texture colors without adding lights or changing source materials."""
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = False
    scene.display.shading.show_cavity = False
    scene.display.shading.show_specular_highlight = False
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (.10, .12, .14)
    scene.render.resolution_x = 960
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, FRAME_END
    scene.view_settings.view_transform = "Standard"
    ground = create_material("Review floor", (.12, .15, .18))
    add_fixture_cube("Preview floor — not exported", (0, 0, -.035), (200, 200, .06), ground)
    target = (0, .35, .38) if kind == "GreekFire" else (0, -.13, .43)
    bpy.ops.object.camera_add(location=(2, 2.5, 1.65) if kind == "GreekFire" else (2, -.9, 1.25))
    scene.camera = bpy.context.object
    scene.camera.data.type = "ORTHO"
    scene.camera.data.ortho_scale = 2.2 if kind == "GreekFire" else 1.65
    point_at(scene.camera, target)
    if kind == "Headbutter":
        material = create_material("Review wall", (.27, .29, .30))
        for row in range(3):
            for column in range(5):
                add_fixture_cube("Impact target masonry", ((column-2)*.215 + (row%2)*.06, -.69, .10+row*.20),
                                 (.207, .19, .194), material)
    return scene.camera


def create_flame_preview(rig):
    """Animate lightweight flame-shaped puffs attached to the source nozzle bone."""
    rng = random.Random(42)
    materials = [create_material("Flame amber", (1, .12, .008), 2.5),
                 create_material("Flame gold", (1, .42, .018), 3),
                 create_material("Flame core", (1, .78, .20), 4)]
    bone = rig.data.bones["tripo::Head_0"]
    rest_inverse = bone.matrix_local.inverted()
    for index in range(28):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1)
        puff = bpy.context.object
        puff.name = f"PreviewFlame_{index:02d}_not_for_export"
        puff["review_fixture"] = True
        puff.data.materials.append(materials[index % len(materials)])
        for polygon in puff.data.polygons:
            polygon.use_smooth = True
        for vertex in puff.data.vertices:
            vertex.co *= rng.uniform(.82, 1.17)
        for frame in range(1, FRAME_END + 1):
            bpy.context.scene.frame_set(frame)
            age = ((frame - 58) * .072 + index / 28) % 1
            envelope = interpolate_keys(frame, [(1, 0), (57, 0), (62, 1), (85, 1), (99, 0), (120, 0)])
            travel = .85 * age
            source_point = Vector((math.sin(index * 2.1 + age * 4)*.035*age,
                                   .49 + travel, .461 + .10*age*age))
            puff.location = rig.matrix_world @ rig.pose.bones[bone.name].matrix @ rest_inverse @ source_point
            radius = (.016 + .060*age) * envelope
            puff.scale = (radius, radius*(1.7 + .5*math.sin(frame + index)), radius)
            puff.keyframe_insert("location", frame=frame)
            puff.keyframe_insert("scale", frame=frame)


def set_preview_workspace(scene):
    """Make the saved file open in an immediately playable, textured camera view."""
    scene.frame_set(1)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "CONSOLE":
                area.type = "VIEW_3D"
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "SOLID"
                    space.shading.light = "FLAT"
                    space.shading.color_type = "TEXTURE"
                    space.shading.show_shadows = False
                    space.shading.show_cavity = False
                    space.shading.show_specular_highlight = False
                    space.overlay.show_overlays = False
                    space.region_3d.view_perspective = "CAMERA"
