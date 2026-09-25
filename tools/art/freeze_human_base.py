"""Promote the approved Blender character without editing its review checkpoint.

Run in Blender, not system Python. The saved master is editable and self-contained.
"""
import argparse
import hashlib
import json
import logging
import math
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from human_hands import (GRIP_KEY, refine_finger_curl, refine_holding_pose,
                         restore_underarm_lining, refine_articulated_grip)
from human_skin import paint_eyes, paint_skin

LOGGER = logging.getLogger(__name__)
PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_HASH = '8c2648e943647e54667ebb1d76c0db6a8cf2fffba048ad9d6aba111c649b27aa'
BODY_NAME = 'Body | continuous anatomical foundation'
HAND_REVIEW_SOURCE_HASH = '5358b8e300df2a7fea15a7953651470f3acae3c56d01bab425f7c1ee59437a1c'


def get_file_hash(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def get_body_hash(body):
    """Protect rest positions, original shape keys, and object transform."""
    digest = hashlib.sha256()
    for vertex in body.data.vertices:
        digest.update(struct.pack('fff', *vertex.co))
    for key in list(body.data.shape_keys.key_blocks)[:2]:
        digest.update(key.name.encode())
        digest.update(struct.pack('f', key.value))
        for vertex in key.data:
            digest.update(struct.pack('fff', *vertex.co))
    for row in body.matrix_world:
        digest.update(struct.pack('ffff', *row))
    return digest.hexdigest()


def get_protected_geometry_hash():
    """Also protect approved armor, helmet, accessories, and weapon geometry."""
    digest = hashlib.sha256()
    for obj in sorted(bpy.data.objects, key=lambda item: item.name):
        if obj.type != 'MESH' or obj.name == BODY_NAME:
            continue
        digest.update(obj.name.encode())
        for vertex in obj.data.vertices:
            digest.update(struct.pack('fff', *vertex.co))
        for row in obj.matrix_world:
            digest.update(struct.pack('ffff', *row))
    return digest.hexdigest()


def get_hand_only_scope_hash():
    """Protect everything on the character except grip-key vertices in the hands."""
    digest = hashlib.sha256()
    digest.update(get_body_hash(bpy.data.objects[BODY_NAME]).encode())
    digest.update(get_protected_geometry_hash().encode())
    for obj in sorted(bpy.data.objects, key=lambda item: item.name):
        if obj.type in ('CAMERA', 'LIGHT'):
            continue
        digest.update(repr((obj.name, obj.hide_render, obj.hide_get())).encode())
        for row in obj.matrix_world:
            digest.update(struct.pack('ffff', *row))
        for modifier in obj.modifiers:
            values = [(name, str(getattr(modifier, name))) for name in
                      ('type', 'show_viewport', 'show_render', 'levels', 'render_levels',
                       'offset', 'vertex_group', 'wrap_mode', 'thickness') if hasattr(modifier, name)]
            digest.update(repr(values).encode())
        if obj.type == 'CURVE':
            digest.update(repr((obj.data.bevel_depth, obj.data.bevel_resolution)).encode())
            for spline in obj.data.splines:
                for point in spline.points:
                    digest.update(struct.pack('ffff', *point.co))
                for point in spline.bezier_points:
                    for coordinates in (point.co, point.handle_left, point.handle_right):
                        digest.update(struct.pack('fff', *coordinates))
        if obj.type == 'ARMATURE':
            for bone in obj.pose.bones:
                digest.update(bone.name.encode())
                for row in bone.matrix:
                    digest.update(struct.pack('ffff', *row))
        if obj.type != 'MESH':
            continue
        for face in obj.data.polygons:
            digest.update(repr((tuple(face.vertices), face.material_index)).encode())
        for vertex in obj.data.vertices:
            digest.update(repr([(group.group, group.weight) for group in vertex.groups]).encode())
        for attribute in obj.data.color_attributes:
            for item in attribute.data:
                digest.update(struct.pack('ffff', *item.color))
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                digest.update(repr((key.name, key.value)).encode())
                for index, item in enumerate(key.data):
                    world = obj.matrix_world @ obj.data.vertices[index].co
                    is_editable_hand = obj.name == BODY_NAME and key.name == GRIP_KEY and abs(world.x) > .33 and .73 < world.z < .99
                    if not is_editable_hand:
                        digest.update(struct.pack('fff', *item.co))
    for material in sorted(bpy.data.materials, key=lambda item: item.name):
        digest.update(repr((material.name, tuple(material.diffuse_color))).encode())
        if not material.node_tree:
            continue
        for node in material.node_tree.nodes:
            digest.update(repr((node.name, node.type)).encode())
            if hasattr(node, 'color_ramp'):
                digest.update(repr([(element.position, tuple(element.color))
                                    for element in node.color_ramp.elements]).encode())
            for property_name in ('operation', 'blend_type', 'layer_name', 'attribute_name'):
                if hasattr(node, property_name):
                    digest.update(str(getattr(node, property_name)).encode())
            for socket in node.inputs:
                if hasattr(socket, 'default_value'):
                    value = socket.default_value
                    value = tuple(value) if hasattr(value, '__len__') and not isinstance(value, str) else str(value)
                    digest.update(repr((socket.name, value)).encode())
        digest.update(repr([(link.from_node.name, link.from_socket.name, link.to_node.name, link.to_socket.name)
                            for link in material.node_tree.links]).encode())
    return digest.hexdigest()


def configure_view(yaw, pitch, target, scale, resolution):
    scene = bpy.context.scene
    yaw, pitch = math.radians(yaw), math.radians(pitch)
    offset = Vector((math.sin(yaw)*math.cos(pitch), -math.cos(yaw)*math.cos(pitch), math.sin(pitch)))*6
    scene.camera.location = Vector(target)+offset
    scene.camera.rotation_euler = (-offset).to_track_quat('-Z', 'Y').to_euler()
    scene.camera.data.ortho_scale = scale
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100


def render_previews(output_dir, preview_only, hands_only=False):
    output_dir.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.cycles.samples = 24 if preview_only else 48
    views = [('detail', 22, 4, (.06, -.08, 1.39), 1.20, (1200, 1050))]
    if hands_only:
        views = [('hands', 0, 12, (.03, -.28, 1.15), .70, (1400, 1000))]
    elif not preview_only:
        views += [('full', 25, 3, (.08, -.04, .96), 2.20, (1100, 1250)),
                  ('rts', 25, 42, (.08, -.04, .96), 2.20, (1100, 1250)),
                  ('hands', 0, 12, (.03, -.28, 1.15), .70, (1400, 1000))]
    for name, yaw, pitch, target, scale, resolution in views:
        configure_view(yaw, pitch, target, scale, resolution)
        scene.render.filepath = str(output_dir/f'{name}.png')
        bpy.ops.render.render(write_still=True)


def render_hand_study(output_dir):
    """Show both complete hands and high-angle contact views on the saved model."""
    output_dir.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.cycles.samples = 40
    views = [('hands_pair', 0, 16, (.12, -.29, 1.19), .90, (1600, 1000)),
             ('thumbs_top', 0, 70, (.12, -.29, 1.19), .90, (1600, 1000)),
             ('trigger_hand', -30, 35, (-.08, -.30, 1.20), .40, (1000, 1000)),
             ('support_hand', 30, 40, (.32, -.30, 1.075), .40, (1000, 1000)),
             ('detail', 22, 4, (.06, -.08, 1.39), 1.20, (1200, 1050))]
    for name, yaw, pitch, target, scale, resolution in views:
        configure_view(yaw, pitch, target, scale, resolution)
        scene.render.filepath = str(output_dir/f'{name}.png')
        bpy.ops.render.render(write_still=True)


def save_master(output_path):
    output_path.parent.mkdir(parents=True, exist_ok=True)
    configure_view(25, 3, (.08, -.04, .96), 2.20, (1100, 1250))
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_location = Vector((.08, -.04, .96))
                space.region_3d.view_distance = 3.0
                space.region_3d.view_rotation = bpy.context.scene.camera.rotation_euler.to_quaternion()
                space.shading.type = 'MATERIAL'
    bpy.context.scene['human_base_id'] = 'human-matchlock-v1'
    bpy.context.scene['approved_geometry'] = True
    bpy.context.scene['variation_policy'] = 'Palette and weapon only; anatomy/costume changes require explicit approval.'
    bpy.context.scene['runtime_animation_ready'] = False
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output_path), compress=True)


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--preview-only', action='store_true')
    parser.add_argument('--hands-only', action='store_true')
    parser.add_argument('--refine-hands', action='store_true', help='Change only hands on the frozen production master')
    parser.add_argument('--no-render', action='store_true')
    return parser.parse_args(sys.argv[sys.argv.index('--')+1:])


def main():
    logging.basicConfig(level=logging.INFO)
    args = parse_arguments()
    if args.source.resolve() == args.output.resolve():
        raise ValueError('Never overwrite the approved input checkpoint')
    expected_hash = HAND_REVIEW_SOURCE_HASH if args.refine_hands else SOURCE_HASH
    if get_file_hash(args.source) != expected_hash:
        raise ValueError(f'Approved source hash mismatch: {args.source}')
    bpy.ops.wm.open_mainfile(filepath=str(args.source))
    body = bpy.data.objects[BODY_NAME]
    body_hash = get_body_hash(body)
    costume_hash = get_protected_geometry_hash()
    if args.refine_hands:
        from hand_contacts import measure_hand_clearance, resolve_hand_contacts, measure_thumb_exposure
        scope_hash = get_hand_only_scope_hash()
        clearance_before = measure_hand_clearance(body)
        config_path = PROJECT_ROOT/'art/characters/human_base/hand_grip_pose.json'
        hand_changes = refine_articulated_grip(body, config_path)
        corrections = resolve_hand_contacts(body)
        if get_hand_only_scope_hash() != scope_hash:
            raise ValueError('Hand-only update changed a frozen model component')
        report = json.loads(args.source.with_suffix('.verification.json').read_text())
        report['hand_revision'] = 3
        report['hand_articulation'] = hand_changes['articulation']
        report['thumb_exposure'] = measure_thumb_exposure(body, json.loads(config_path.read_text()))
        report['hand_parameters_sha256'] = get_file_hash(PROJECT_ROOT/'art/characters/human_base/hand_grip_pose.json')
        report['hand_clearance_before'] = clearance_before
        report['hand_clearance_after'] = measure_hand_clearance(body)
        report['hand_contact_corrections'] = corrections
        LOGGER.info('Hand intersections: before=%s; after=%s', clearance_before, report['hand_clearance_after'])
        report['hands_only_source_sha256'] = expected_hash
        report['frozen_except_hands_sha256'] = scope_hash
        # Save without resetting any object/pose/material or viewport from the master.
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(args.output), compress=True)
        report['master_sha256'] = get_file_hash(args.output)
        args.output.with_suffix('.verification.json').write_text(json.dumps(report, indent=2)+'\n')
        if not args.no_render:
            render_previews(PROJECT_ROOT/'Builds/ArtReview/HandRefinement', args.preview_only, args.hands_only)
        LOGGER.info('Hand-only revision saved; other components hash: %s', scope_hash)
        return
    refine_finger_curl(body)
    pose_report = refine_holding_pose(bpy.data.objects['Equipment | editable holding pose rig'])
    restore_underarm_lining()
    paint_skin(body)
    eye_count = paint_eyes()
    if get_body_hash(body) != body_hash or get_protected_geometry_hash() != costume_hash:
        raise ValueError('Protected anatomy/costume geometry changed')
    save_master(args.output)
    render_previews(PROJECT_ROOT/'Builds/ArtReview/HumanBase-v1', args.preview_only, args.hands_only)
    report = {'id': 'human-matchlock-v1', 'approved_source_sha256': SOURCE_HASH,
              'master_sha256': get_file_hash(args.output), 'body_rest_sha256': body_hash,
              'costume_geometry_sha256': costume_hash, 'protected_geometry_unchanged': True,
              'hands': pose_report, 'colored_eyes': eye_count, 'runtime_animation_ready': False}
    args.output.with_suffix('.verification.json').write_text(json.dumps(report, indent=2)+'\n')
    LOGGER.info('Frozen human master: %s; pose: %s', args.output, pose_report)


if __name__ == '__main__':
    main()
