"""Export a static Unity inspection model, not a crowd-animation replacement.

This output is generated from the tracked Blender master and is deliberately
kept out of Git and outside Resources so it is not bundled into playable builds.
"""
from pathlib import Path
from collections import defaultdict
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify_human_base import verify_master
from freeze_human_base import PROJECT_ROOT


def bake_eye_colors(mesh):
    """Preserve iris colors when exporting the Blender-only procedural eye shader."""
    material = mesh.materials[0].copy()
    ramp = next(node for node in material.node_tree.nodes if node.type == 'VALTORGB').color_ramp
    low = min(vertex.co.z for vertex in mesh.vertices)
    high = max(vertex.co.z for vertex in mesh.vertices)
    attribute = mesh.color_attributes.new(name='HumanEye', type='FLOAT_COLOR', domain='POINT')
    for vertex, item in zip(mesh.vertices, attribute.data):
        item.color = ramp.evaluate(1-(vertex.co.z-low)/(high-low))
    mesh.color_attributes.active_color = attribute
    shader = next(node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    color_node = material.node_tree.nodes.new('ShaderNodeVertexColor')
    color_node.layer_name = attribute.name
    material.node_tree.links.new(color_node.outputs['Color'], shader.inputs['Base Color'])
    mesh.materials[0] = material


def collect_preview_objects():
    """Evaluate the held pose and trim the scene down to visible geometry."""
    originals = list(bpy.context.scene.objects)
    for obj in originals:
        for modifier in obj.modifiers:
            if modifier.type == 'MULTIRES':
                modifier.levels = min(2, modifier.total_levels)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    previews = []
    for obj in originals:
        if obj.type not in ('MESH', 'CURVE') or obj.hide_render or obj.hide_get():
            continue
        # Lights, cameras, and the review floor are not character assets.
        if any(collection.name.startswith(('REVIEW', 'Review', '00 |')) for collection in obj.users_collection):
            continue
        if 'floor' in obj.name.lower() or 'ground' in obj.name.lower():
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
        mesh.validate()
        if '.eye.' in obj.name:
            bake_eye_colors(mesh)
        duplicate = bpy.data.objects.new(obj.name, mesh)
        bpy.context.scene.collection.objects.link(duplicate)
        duplicate.matrix_world = obj.matrix_world
        previews.append(duplicate)
    for obj in originals:
        bpy.data.objects.remove(obj, do_unlink=True)
    return previews


def merge_preview_parts(previews):
    """Group small rivet/seam meshes, retaining a separate replaceable weapon."""
    groups = defaultdict(list)
    for obj in previews:
        category = 'Weapon' if obj.name.startswith('veteran_') else 'Human'
        material_names = tuple(material.name for material in obj.data.materials)
        groups[(category, material_names)].append(obj)
    merged = []
    for (category, material_names), objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        objects[0].name = category+' | '+' + '.join(material_names)
        merged.append(objects[0])
    return merged


def export_preview():
    verify_master()
    previews = merge_preview_parts(collect_preview_objects())
    if not previews:
        raise ValueError('No visible character meshes to export')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in previews:
        obj.select_set(True)
    output = PROJECT_ROOT/'Assets/_Game/ArtGenerated/HumanBase/human_base.glb'
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(output), export_format='GLB', use_selection=True,
                              export_animations=False, export_skins=False, export_cameras=False,
                              export_lights=False, export_extras=True)
    print(f'HUMAN_PREVIEW_EXPORTED: {len(previews)} mesh objects; {output}', flush=True)


if __name__ == '__main__':
    export_preview()
