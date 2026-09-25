"""Validate the saved production master in a fresh Blender process."""
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from freeze_human_base import (BODY_NAME, PROJECT_ROOT, get_body_hash, get_file_hash,
                              get_protected_geometry_hash, get_hand_only_scope_hash, render_previews,
                              render_hand_study)
from human_hands import GRIP_KEY, get_hand_basis, get_palm_frame


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def verify_hand_pose(rig, side, expected):
    """Measure actual saved bone matrices rather than trusting the build report."""
    hand = rig.pose.bones[f'hand.{side}']
    forearm = rig.pose.bones[f'forearm.{side}']
    transform = hand.matrix @ rig.data.bones[hand.name].matrix_local.inverted()
    forward = transform.to_3x3() @ get_hand_basis(side)[0]
    angle = math.degrees(forward.angle(forearm.tail-forearm.head))
    require(angle < 35, f'Wrist {side} is folded: {angle:.1f} degrees')
    require(abs(angle-expected['wrist_angle_degrees']) < .1, f'Wrist {side} report mismatch')
    require((hand.head-Vector(expected['wrist_world'])).length < .0001, f'Wrist {side} moved')
    require((forearm.tail-hand.head).length < .0001, f'Wrist {side} detached from forearm')


def verify_finger_volume(body):
    """Regression: distal curl limiting must not flatten fingertip surfaces."""
    key = body.data.shape_keys.key_blocks[GRIP_KEY]
    minimum_ratio = 1.0
    for face in body.data.polygons:
        if not all(abs((body.matrix_world @ body.data.vertices[index].co).x) > .33 and
                   .73 < (body.matrix_world @ body.data.vertices[index].co).z < .99
                   for index in face.vertices):
            continue
        for index in range(1, len(face.vertices)-1):
            indices = (face.vertices[0], face.vertices[index], face.vertices[index+1])
            original = [body.data.vertices[item].co for item in indices]
            posed = [key.data[item].co for item in indices]
            source_area = (original[1]-original[0]).cross(original[2]-original[0]).length
            target_area = (posed[1]-posed[0]).cross(posed[2]-posed[0]).length
            if source_area > 1e-10:
                ratio = target_area/source_area
                minimum_ratio = min(minimum_ratio, ratio)
                require(ratio > .02, f'Collapsed finger triangle on face {face.index}: {ratio:.5f}')
    print(f'FINGER_SURFACE_VERIFIED: minimum triangle area ratio {minimum_ratio:.4f}', flush=True)


def verify_master(master=None):
    master = master or PROJECT_ROOT/'art/characters/human_base/human_base.blend'
    report = json.loads(master.with_suffix('.verification.json').read_text())
    require(get_file_hash(master) == report['master_sha256'], 'Master changed without review/manifest update')
    bpy.ops.wm.open_mainfile(filepath=str(master))
    body = bpy.data.objects[BODY_NAME]
    require(get_body_hash(body) == report['body_rest_sha256'], 'Approved rest body changed')
    require(get_protected_geometry_hash() == report['costume_geometry_sha256'], 'Approved costume/weapon changed')
    if 'frozen_except_hands_sha256' in report:
        require(get_hand_only_scope_hash() == report['frozen_except_hands_sha256'],
                'Hand-only edit changed frozen geometry, pose, colors, or materials')
        require(get_file_hash(PROJECT_ROOT/'art/characters/human_base/hand_grip_pose.json') == report['hand_parameters_sha256'],
                'Hand parameters changed without regenerating and reviewing the model')
        from hand_contacts import measure_hand_clearance
        clearance = measure_hand_clearance(body)
        for side, result in clearance.items():
            require(result['deep_intersections'] == 0, f'Hand {side} still has deep stock/barrel intersections')
            require(result == report['hand_clearance_after'][side], f'Hand {side} clearance report is stale')
        print('HAND_CONTACT_SAMPLES_VERIFIED: no intersections deeper than 2mm in sampled base vertices', flush=True)
        if report.get('hand_revision', 0) >= 3:
            verify_grip_readability(body, report)
    require(body.data.color_attributes.get('HumanSkin') is not None, 'Missing skin color attribute')
    require(body.data.materials[0].name == 'HumanBase | warm skin', 'Body still has review clay')
    verify_finger_volume(body)
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    for side in (-1, 1):
        verify_hand_pose(rig, side, report['hands'][str(side)])
    eyes = [obj for obj in bpy.data.objects if '.eye.' in obj.name]
    require(len(eyes) == 2, 'Expected two original eyes')
    require(all(obj.data.materials[0].name == 'HumanBase | eyes' for obj in eyes), 'Unpainted eye')
    require(not bpy.context.scene['runtime_animation_ready'], 'Do not label the arm study a complete runtime rig')
    print('HUMAN_BASE_VERIFIED: protected geometry, skin, eyes, connected wrists below 35 degrees', flush=True)


def verify_grip_readability(body, report):
    """Catch the hidden-thumb and compounded-palm-curl regressions explicitly."""
    from hand_contacts import measure_thumb_exposure
    settings = json.loads((PROJECT_ROOT/'art/characters/human_base/hand_grip_pose.json').read_text())
    exposure = measure_thumb_exposure(body, settings)
    verify_thumb_exposure(exposure)
    require(exposure == report['thumb_exposure'], 'Thumb exposure report is stale')

    for role, fingers in report['hand_articulation'].items():
        for name, parameters in fingers.items():
            require(all(0 <= angle <= 85 for angle in parameters['angles']),
                    f'{role}/{name} has a reversed or excessive joint angle')
            root = Vector(settings['fingers'][name]['joints'][0])
            palm_angle = math.degrees(get_palm_frame(root).to_quaternion().angle)
            total = palm_angle+sum(parameters['angles'])
            require(palm_angle <= 24 and total <= 166, f'{role}/{name} has compounded palm/finger overcurl')
            require(abs(total-parameters['total_curl_degrees']) < .001, f'{role}/{name} curl report is stale')
    print('GRIP_READABILITY_VERIFIED: exposed thumbs; positive joint flexion; total curl below 166 degrees', flush=True)


def verify_thumb_exposure(exposure):
    """Reject distal thumbs folded below, or completely hidden by, the gun."""
    for side, measurements in exposure.items():
        require(measurements['gun_unoccluded_samples'] >= 12, f'Thumb {side} is hidden behind the gun')
        require(measurements['maximum_height_above_stock_axis_m'] > .009,
                f'Thumb {side} is folded underneath the stock')


def verify_freeze_guard():
    """Negative controls prove non-hand changes are rejected without saving them."""
    original = get_hand_only_scope_hash()
    body = bpy.data.objects[BODY_NAME]
    vertex = next(vertex for vertex in body.data.vertices if (body.matrix_world @ vertex.co).z > 1.65)
    point = vertex.co.copy()
    vertex.co.x += .001
    require(get_hand_only_scope_hash() != original, 'Freeze guard missed a face edit')
    vertex.co = point
    material = body.data.materials[0]
    color = tuple(material.diffuse_color)
    material.diffuse_color[0] += .01
    require(get_hand_only_scope_hash() != original, 'Freeze guard missed a skin-color edit')
    material.diffuse_color = color
    weapon = bpy.data.objects['veteran_matchlock_carved_stock']
    location = weapon.location.copy()
    weapon.location.x += .001
    bpy.context.view_layer.update()
    require(get_hand_only_scope_hash() != original, 'Freeze guard missed a weapon move')
    weapon.location = location
    bpy.context.view_layer.update()
    require(get_hand_only_scope_hash() == original, 'Freeze guard test failed to restore the in-memory scene')
    print('FREEZE_GUARD_NEGATIVE_CONTROLS_PASSED: face, skin color, weapon', flush=True)


if __name__ == '__main__':
    candidate = Path(sys.argv[sys.argv.index('--master')+1]) if '--master' in sys.argv else None
    verify_master(candidate)
    if '--test-freeze-guard' in sys.argv:
        verify_freeze_guard()
    if '--render' in sys.argv:
        render_previews(PROJECT_ROOT/'Builds/ArtReview/HumanBase-v1', preview_only=False)
    if '--render-hands' in sys.argv:
        review_dir = (Path(sys.argv[sys.argv.index('--review-dir')+1]) if '--review-dir' in sys.argv
                      else PROJECT_ROOT/'Builds/ArtReview/HandRefinement')
        render_hand_study(review_dir)
