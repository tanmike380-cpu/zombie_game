"""Derive two review poses from one frozen character; never overwrite the master."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from freeze_human_base import BODY_NAME, PROJECT_ROOT, configure_view, get_file_hash
from human_hands import (GUN_AXIS, GUN_ORIGIN, GRIP_KEY, get_hand_basis,
                         get_rest_joints, refine_articulated_grip, set_world_bone, solve_elbow)
from hand_contacts import measure_hand_clearance, resolve_hand_contacts
from verify_human_base import verify_master, verify_finger_volume
from human_rts_hands import HAND_PREFIX, MASK_GROUP, build_rts_hands, measure_rts_hands
from matchlock_stock import STOCK_NAME, refit_stock, get_stock_coordinates, verify_weapon_fit

MASTER = PROJECT_ROOT/'art/characters/human_base/human_base.blend'
PRESETS = PROJECT_ROOT/'art/characters/human_base/weapon_pose_presets.json'
HAND_CONFIG = PROJECT_ROOT/'art/characters/human_base/hand_grip_pose.json'
REVIEW_DIR = PROJECT_ROOT/'Builds/ArtReview/WeaponPoses-v3'
HEAD_KEY = 'Pose study | gentle aiming head tilt'


def is_head_equipment(obj):
    return '.eye.' in obj.name or any(label in obj.name for label in
                                    ('helmet', 'tassel', 'neck guard', 'neck lame'))


def get_identity_hash():
    """Freeze local geometry, materials and non-posing transforms across studies."""
    digest = hashlib.sha256()
    for obj in sorted(bpy.data.objects, key=lambda item: item.name):
        if obj.type not in ('MESH', 'CURVE', 'ARMATURE') or obj.name.startswith(HAND_PREFIX):
            continue
        digest.update(obj.name.encode())
        if not (obj.name.startswith('veteran_') or is_head_equipment(obj)):
            digest.update(repr(tuple(tuple(row) for row in obj.matrix_world)).encode())
        if obj.type == 'MESH':
            for vertex in obj.data.vertices:
                if obj.name != STOCK_NAME:
                    digest.update(struct.pack('fff', *vertex.co))
                digest.update(repr([(group.group, group.weight) for group in vertex.groups
                                    if obj.vertex_groups[group.group].name != MASK_GROUP]).encode())
            for face in obj.data.polygons:
                digest.update(repr((tuple(face.vertices), face.material_index)).encode())
            for attribute in obj.data.color_attributes:
                for item in attribute.data:
                    digest.update(struct.pack('ffff', *item.color))
            if obj.data.shape_keys:
                for key in obj.data.shape_keys.key_blocks:
                    if key.name == HEAD_KEY:
                        continue
                    for index, item in enumerate(key.data):
                        rest = obj.matrix_world @ obj.data.vertices[index].co
                        if key.name == GRIP_KEY and abs(rest.x) > .33 and .73 < rest.z < .99:
                            continue
                        digest.update(struct.pack('fff', *item.co))
        elif obj.type == 'CURVE':
            for spline in obj.data.splines:
                for point in spline.points:
                    digest.update(repr(tuple(point.co)).encode())
                for point in spline.bezier_points:
                    digest.update(repr((tuple(point.co), tuple(point.handle_left), tuple(point.handle_right))).encode())
        elif obj.type == 'ARMATURE':
            for bone in obj.data.bones:
                digest.update(repr((bone.name, tuple(bone.head_local), tuple(bone.tail_local))).encode())
    for material in sorted(bpy.data.materials, key=lambda item: item.name):
        digest.update(repr((material.name, tuple(material.diffuse_color))).encode())
        if material.node_tree:
            for node in material.node_tree.nodes:
                for socket in node.inputs:
                    if hasattr(socket, 'default_value'):
                        value = socket.default_value
                        digest.update(repr(tuple(value) if hasattr(value, '__len__') else value).encode())
    return digest.hexdigest()


def get_weapon_transform(preset):
    """Rigid assembly transform keeps the original stock, barrel and fittings intact."""
    axis = Vector(preset['gun_axis']).normalized()
    lateral = Vector((0, 0, 1)).cross(axis).normalized()
    up = axis.cross(lateral).normalized()
    source_up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    source_basis = Matrix((GUN_AXIS, Vector((0, 1, 0)), source_up)).transposed()
    target_basis = Matrix((axis, lateral, up)).transposed()
    rotation = target_basis @ source_basis.transposed()
    return Matrix.Translation(Vector(preset['gun_origin'])) @ rotation.to_4x4() @ Matrix.Translation(-GUN_ORIGIN)


def pose_arms(preset, weapon_transform):
    """Solve both elbows after placing hand contacts in weapon-relative coordinates."""
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    result = {}
    for side in (-1, 1):
        role = 'trigger' if side < 0 else 'support'
        axis = Vector(preset['gun_axis']).normalized()
        lateral = Vector((0, 0, 1)).cross(axis).normalized()
        up = axis.cross(lateral).normalized()
        local = Vector(preset['hand_forward_weapon'][role])
        forward = (axis*local.x+lateral*local.y+up*local.z).normalized()
        across_local = preset.get('hand_across_weapon', {}).get(role, [1, 0, 0])
        across_reference = axis*across_local[0]+lateral*across_local[1]+up*across_local[2]
        across = (across_reference-forward*across_reference.dot(forward)).normalized()
        normal = forward.cross(across).normalized()
        contact = Vector(preset['gun_origin'])+axis*preset['grip_distance_m'][role]+up*preset['grip_height_m'][role]
        wrist = contact-forward*.085-across*(side*.040)-normal*.030
        offsets = preset.get('wrist_offsets_weapon', {}).get(role)
        if offsets:
            wrist = Vector(preset['gun_origin'])+axis*(preset['grip_distance_m'][role]+offsets[0])+lateral*offsets[1]+up*offsets[2]
        rotation = Matrix((forward, across, normal)).transposed() @ Matrix(get_hand_basis(side))
        shoulder, rest_elbow, rest_wrist = get_rest_joints(side)
        elbow = solve_elbow(shoulder, wrist, (rest_elbow-shoulder).length,
                            (rest_wrist-rest_elbow).length, side, preset['elbow_poles'][role])
        if offsets:
            forward = (wrist-elbow).normalized()
            across = (axis-forward*axis.dot(forward)).normalized()
            normal = forward.cross(across).normalized()
            rotation = Matrix((forward, across, normal)).transposed() @ Matrix(get_hand_basis(side))
        upper_rotation = (rest_elbow-shoulder).rotation_difference(elbow-shoulder).to_matrix()
        forearm_rotation = (rest_wrist-rest_elbow).rotation_difference(wrist-elbow).to_matrix()
        set_world_bone(rig, f'upper.{side}', shoulder, upper_rotation)
        set_world_bone(rig, f'forearm.{side}', elbow, forearm_rotation)
        set_world_bone(rig, f'hand.{side}', wrist, rotation)
        forward = rotation @ get_hand_basis(side)[0]
        result[role] = {'wrist': list(wrist), 'elbow': list(elbow),
                        'wrist_angle_degrees': math.degrees(forward.angle(wrist-elbow))}
    return result


def pose_head(body, degrees, lean=0):
    """Small reversible neck/head rotation; facial geometry and colors stay authored."""
    if degrees == 0:
        return
    pivot = Vector((0, .015, 1.535))
    transform = Matrix.Translation(pivot+Vector((lean, 0, 0))) @ Matrix.Rotation(math.radians(degrees), 4, 'X') @ Matrix.Translation(-pivot)
    key = body.shape_key_add(name=HEAD_KEY, from_mix=False)
    inverse = body.matrix_world.inverted()
    for source, target in zip(body.data.vertices, key.data):
        world = body.matrix_world @ source.co
        weight = max(0, min(1, (world.z-1.495)/.065))
        weight = weight*weight*(3-2*weight)
        target.co = inverse @ world.lerp(transform @ world, weight)
    key.value = 1
    for obj in bpy.data.objects:
        if is_head_equipment(obj):
            obj.matrix_world = transform @ obj.matrix_world
    bpy.context.view_layer.update()


def build_pose(name, preset, source_hash):
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    identity = get_identity_hash()
    refit_stock(preset)
    body = bpy.data.objects[BODY_NAME]
    transform = get_weapon_transform(preset)
    arms = pose_arms(preset, transform)
    for obj in bpy.data.objects:
        if obj.name.startswith('veteran_'):
            obj.matrix_world = transform @ obj.matrix_world
    bpy.context.view_layer.update()
    settings = json.loads(HAND_CONFIG.read_text())
    settings['hand_pitch_degrees'] = {'trigger': 0, 'support': 0}
    axis = Vector(preset['gun_axis']).normalized()
    lateral = Vector((0, 0, 1)).cross(axis).normalized()
    up = axis.cross(lateral).normalized()
    settings['thumb_directions_world'] = {
        role: [list(axis*direction[0]+lateral*direction[1]+up*direction[2]) for direction in directions]
        for role, directions in preset['thumb_directions_weapon'].items()}
    inverse = transform.inverted()
    simplified = preset.get('hand_style') == 'rts_mitten'
    if simplified:
        hands = build_rts_hands(body, preset, inverse)
        corrections = {}
    else:
        hands = refine_articulated_grip(body, HAND_CONFIG, settings, inverse)
        corrections = resolve_hand_contacts(body, inverse)
    pose_head(body, preset['head_pitch_degrees'], preset.get('head_lean_m', 0))
    if get_identity_hash() != identity:
        raise ValueError(f'{name}: frozen character identity changed')
    if not simplified:
        verify_finger_volume(body)
    clearance = measure_rts_hands(body, inverse) if simplified else measure_hand_clearance(body, inverse)
    if any(hand['deep_intersections'] for hand in clearance.values()):
        raise ValueError(f'{name}: deep hand/weapon intersections: {clearance}')
    bpy.context.scene['pose_study'] = name
    bpy.context.scene['pose_study_status'] = 'Unapproved static study; no runtime animation replacement'
    bpy.context.scene['runtime_animation_ready'] = False
    output = REVIEW_DIR/name
    output.mkdir(parents=True, exist_ok=True)
    configure_view(*preset['views'][0][1:])
    bpy.context.preferences.filepaths.save_version = 0
    blend_path = output/f'{name}.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    report = {'pose': name, 'source_sha256': source_hash, 'identity_sha256': identity,
              'preset_sha256': hashlib.sha256(json.dumps(preset, sort_keys=True).encode()).hexdigest(),
              'blend_sha256': get_file_hash(blend_path), 'weapon_transform': [list(row) for row in transform],
              'arms': arms, 'hands': hands, 'contacts': clearance, 'corrections': corrections,
              'runtime_animation_ready': False}
    (output/'verification.json').write_text(json.dumps(report, indent=2)+'\n')
    print('POSE_STUDY_SAVED', name, arms, flush=True)


def verify_pose(name, preset):
    """Reload the saved study; verify intact identity, wrist continuity and contacts."""
    report_path = REVIEW_DIR/name/'verification.json'
    report = json.loads(report_path.read_text())
    blend_path = REVIEW_DIR/name/f'{name}.blend'
    if report['blend_sha256'] != get_file_hash(blend_path):
        raise ValueError(f'{name}: saved study changed without verification')
    preset_hash = hashlib.sha256(json.dumps(preset, sort_keys=True).encode()).hexdigest()
    if report['preset_sha256'] != preset_hash or report['source_sha256'] != get_file_hash(MASTER):
        raise ValueError(f'{name}: rebuild this study from current source/settings')
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    source_identity = get_identity_hash()
    refit_stock(preset)
    expected_stock = get_stock_coordinates()
    weapon_matrices = {obj.name: obj.matrix_world.copy() for obj in bpy.data.objects if obj.name.startswith('veteran_')}
    bpy.ops.wm.open_mainfile(filepath=str(blend_path))
    if get_stock_coordinates() != expected_stock:
        raise ValueError(f'{name}: stock differs from the authorized full-length wood refit')
    if get_identity_hash() != source_identity or report['identity_sha256'] != source_identity:
        raise ValueError(f'{name}: character identity differs from the frozen master')
    weapon_transform = get_weapon_transform(preset)
    if preset.get('stock_refit'):
        verify_weapon_fit(name, preset)
    for object_name, matrix in weapon_matrices.items():
        actual = bpy.data.objects[object_name].matrix_world
        expected = weapon_transform @ matrix
        if max(abs(actual[row][column]-expected[row][column]) for row in range(4) for column in range(4)) > .00001:
            raise ValueError(f'{name}: weapon assembly shifted internally: {object_name}')
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    for side in (-1, 1):
        hand, forearm = rig.pose.bones[f'hand.{side}'], rig.pose.bones[f'forearm.{side}']
        transform = hand.matrix @ rig.data.bones[hand.name].matrix_local.inverted()
        direction = transform.to_3x3() @ get_hand_basis(side)[0]
        angle = math.degrees(direction.angle(forearm.tail-forearm.head))
        if angle > 35 or (hand.head-forearm.tail).length > .0001:
            raise ValueError(f'{name}: disconnected/folded wrist {side}: {angle:.1f} degrees')
    body = bpy.data.objects[BODY_NAME]
    if preset.get('hand_style') == 'rts_mitten':
        contacts = measure_rts_hands(body, weapon_transform.inverted())
    else:
        verify_finger_volume(body)
        contacts = measure_hand_clearance(body, weapon_transform.inverted())
    if contacts != report['contacts'] or any(hand['deep_intersections'] for hand in contacts.values()):
        raise ValueError(f'{name}: invalid hand contact report')
    print('POSE_STUDY_VERIFIED', name, 'identity, rigid weapon, wrists, visible hand contact samples', flush=True)


def render_pose(name, preset, draft=False):
    output = REVIEW_DIR/name
    bpy.ops.wm.open_mainfile(filepath=str(output/f'{name}.blend'))
    bpy.context.scene.cycles.samples = 12 if draft else 40
    for label, yaw, pitch, target, scale, resolution in preset['views'][:2 if draft else None]:
        size = tuple(int(value*.65) for value in resolution) if draft else resolution
        configure_view(yaw, pitch, target, scale, size)
        bpy.context.scene.render.filepath = str(output/f'{label}{"_draft" if draft else ""}.png')
        bpy.ops.render.render(write_still=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pose', choices=('all', 'aim', 'chest'), default='all')
    parser.add_argument('--render-only', action='store_true')
    parser.add_argument('--no-render', action='store_true')
    parser.add_argument('--draft', action='store_true')
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    presets = json.loads(PRESETS.read_text())
    source_hash = get_file_hash(MASTER)
    if source_hash != presets['source_sha256']:
        raise ValueError('Pose presets must be reviewed for the current human master')
    if not args.render_only and not args.verify_only:
        verify_master()
    for name, preset in presets['poses'].items():
        if args.pose not in ('all', name):
            continue
        if args.verify_only:
            verify_pose(name, preset)
            continue
        if not args.render_only:
            build_pose(name, preset, source_hash)
        verify_pose(name, preset)
        if not args.no_render:
            render_pose(name, preset, args.draft)
    if get_file_hash(MASTER) != source_hash:
        raise ValueError('The approved source was overwritten')


if __name__ == '__main__':
    main()
