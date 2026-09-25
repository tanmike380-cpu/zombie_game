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
from human_hands import refine_finger_curl, refine_holding_pose, restore_underarm_lining
from human_skin import paint_eyes, paint_skin

LOGGER = logging.getLogger(__name__)
PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_HASH = '8c2648e943647e54667ebb1d76c0db6a8cf2fffba048ad9d6aba111c649b27aa'
BODY_NAME = 'Body | continuous anatomical foundation'


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
    return parser.parse_args(sys.argv[sys.argv.index('--')+1:])


def main():
    logging.basicConfig(level=logging.INFO)
    args = parse_arguments()
    if args.source.resolve() == args.output.resolve():
        raise ValueError('Never overwrite the approved input checkpoint')
    if get_file_hash(args.source) != SOURCE_HASH:
        raise ValueError(f'Approved source hash mismatch: {args.source}')
    bpy.ops.wm.open_mainfile(filepath=str(args.source))
    body = bpy.data.objects[BODY_NAME]
    body_hash = get_body_hash(body)
    costume_hash = get_protected_geometry_hash()
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
