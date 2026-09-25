"""Fit separate phalange chains to the unchanged stock using surface distances."""
import math

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from human_hands import (GUN_AXIS, GUN_ORIGIN, build_finger_transforms,
                         get_anatomical_basis, get_rest_joints, get_palm_frame)


def build_stock_surface():
    depsgraph = bpy.context.evaluated_depsgraph_get()
    stock = bpy.data.objects['veteran_matchlock_carved_stock']
    evaluated = stock.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    points = [stock.matrix_world @ vertex.co for vertex in mesh.vertices]
    polygons = [tuple(face.vertices) for face in mesh.polygons]
    surface = BVHTree.FromPolygons(points, polygons)
    evaluated.to_mesh_clear()
    return surface


def get_stock_distance(point, surface):
    location, normal, _, distance = surface.find_nearest(point)
    return distance if (point-location).dot(normal) >= 0 else -distance


def get_barrel_distance(point, weapon_inverse=None):
    if weapon_inverse is not None:
        point = weapon_inverse @ point
    relative = point-GUN_ORIGIN
    along = relative.dot(GUN_AXIS)
    up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    radius = .016 if along <= .68 else .0125
    radial = math.hypot(relative.y, relative.dot(up)-.022)-radius
    axial = max(.24-along, along-1.50, 0)
    return math.hypot(max(0, radial), axial) if axial else radial


def score_finger(parameters, joints, transform, surface, name, weapon_inverse=None):
    transforms = build_finger_transforms(joints, parameters[:3], False, parameters[3])
    radius = {'index': .0075, 'middle': .008, 'ring': .0075, 'little': .006}[name]
    palm_angle = math.degrees(get_palm_frame(joints[0]).to_quaternion().angle)
    # Include the palm: limiting finger joints alone missed the old double curl.
    score = .0001*max(0, palm_angle+sum(parameters[:3])-165)**2
    for segment in range(3):
        for fraction in (.35, .70, 1.0):
            local = transforms[segment] @ joints[segment].lerp(joints[segment+1], fraction)
            world = transform @ local
            distance = get_stock_distance(world, surface)
            clearance = radius*(1-.15*segment)
            score += 35*max(0, clearance-distance)**2
            score += 45*max(0, clearance+.001-get_barrel_distance(world, weapon_inverse))**2
            if segment == 2:
                score += (distance-clearance-.001)**2*(4 if fraction == 1 else 1)
    return score


def optimize_finger(initial, joints, transform, surface, name, weapon_inverse=None):
    parameters = [15, initial[1], initial[2], 0.0]
    bounds = [(5, 70), (15, 80), (5, 45), (-7, 7)]
    score = score_finger(parameters, joints, transform, surface, name, weapon_inverse)
    for step in (20, 10, 5, 2):
        for _ in range(8):
            improved = False
            for index in range(4):
                for sign in (-1, 1):
                    candidate = parameters.copy()
                    candidate[index] = max(bounds[index][0], min(bounds[index][1], candidate[index]+sign*step))
                    cost = score_finger(candidate, joints, transform, surface, name, weapon_inverse)
                    if cost < score:
                        parameters, score, improved = candidate, cost, True
            if not improved:
                break
    return parameters, score


def get_hand_contact_frame(side, pitch_degrees):
    """A hand-only wrist hinge; upstream arm bones and wrist position stay fixed."""
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    hand = rig.pose.bones[f'hand.{side}']
    posed_hand = hand.matrix @ rig.data.bones[hand.name].matrix_local.inverted()
    basis = get_anatomical_basis(side)
    unpitched = posed_hand @ Matrix.Translation(get_rest_joints(side)[2]) @ basis.to_4x4()
    direction = unpitched.to_3x3() @ Vector((1, 0, 0))
    axis = direction.cross(Vector((0, 0, -1))).normalized()
    world_rotation = Matrix.Rotation(math.radians(pitch_degrees), 3, axis)
    local_rotation = unpitched.to_3x3().inverted() @ world_rotation @ unpitched.to_3x3()
    return unpitched @ local_rotation.to_4x4(), local_rotation


def fit_finger_contacts(body, side, config, chains, transform, weapon_inverse=None):
    """Solve within joint-angle limits; never reposition wrist, gun or arm."""
    surface = build_stock_surface()
    role = 'trigger' if side < 0 else 'support'
    solved = {}
    for name, entry in config.items():
        if name == 'thumb':
            continue  # Explicit opposition directions preserve the thumb silhouette.
        parameters, score = optimize_finger(entry[role], chains[name], transform, surface, name, weapon_inverse)
        solved[name] = parameters
        print('FINGER_CONTACT_FIT', role, name, parameters, round(score, 6), flush=True)
    return solved


def measure_thumb_exposure(body, settings):
    """Sample distal thumbs against the stock/barrel from the front review view."""
    surface = build_stock_surface()
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    keys = body.data.shape_keys.key_blocks
    tip = Vector(settings['fingers']['thumb']['joints'][-1])
    up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    view = Vector((0, -math.cos(math.radians(16)), math.sin(math.radians(16))))
    report = {}
    for side in (-1, 1):
        basis = get_anatomical_basis(side)
        wrist = get_rest_joints(side)[2]
        bone = rig.pose.bones[f'hand.{side}']
        transform = bone.matrix @ rig.data.bones[bone.name].matrix_local.inverted() @ body.matrix_world
        visible, heights = 0, []
        for vertex in body.data.vertices:
            local = basis.transposed() @ (body.matrix_world @ vertex.co-wrist)
            if (local-tip).length > .020:
                continue
            point = vertex.co.copy()
            for key in list(keys)[1:]:
                point += (key.data[vertex.index].co-keys[0].data[vertex.index].co)*key.value
            world = transform @ point
            heights.append((world-GUN_ORIGIN).dot(up))
            occluded = surface.ray_cast(world+view*.001, view, .160)[0] is not None
            occluded |= any(get_barrel_distance(world+view*(step*.002)) < 0 for step in range(1, 81))
            visible += not occluded
        if not heights:
            raise ValueError(f'No distal thumb vertices for hand {side}')
        report[str(side)] = {'sample_count': len(heights), 'gun_unoccluded_samples': visible,
                            'maximum_height_above_stock_axis_m': max(heights)}
    return report


def measure_hand_clearance(body, weapon_inverse=None):
    """Report deep stock/barrel intersections for fully hand-weighted vertices."""
    surface = build_stock_surface()
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    keys = body.data.shape_keys.key_blocks
    report = {}
    for side in (-1, 1):
        group_index = body.vertex_groups[f'hand.{side}'].index
        bone = rig.pose.bones[f'hand.{side}']
        transform = bone.matrix @ rig.data.bones[bone.name].matrix_local.inverted()
        tested, penetrations, maximum_depth = 0, 0, 0.0
        for vertex in body.data.vertices:
            if not any(group.group == group_index and group.weight > .99 for group in vertex.groups):
                continue
            point = vertex.co.copy()
            for key in list(keys)[1:]:
                point += (key.data[vertex.index].co-keys[0].data[vertex.index].co)*key.value
            world = transform @ (body.matrix_world @ point)
            distance = min(get_stock_distance(world, surface), get_barrel_distance(world, weapon_inverse))
            tested += 1
            if distance < -.002:
                penetrations += 1
                maximum_depth = max(maximum_depth, -distance)
        report[str(side)] = {'tested_vertices': tested, 'deep_intersections': penetrations,
                              'maximum_depth_m': maximum_depth}
    return report


def find_clear_contact(point, surface, weapon_inverse=None):
    """Find the smallest local pad correction outside both wood and barrel."""
    clearance = .0015
    def is_clear(candidate):
        return min(get_stock_distance(candidate, surface), get_barrel_distance(candidate, weapon_inverse)) >= clearance
    if is_clear(point):
        return point
    location, normal, _, _ = surface.find_nearest(point)
    candidates = []
    closest = location+normal*(clearance+.0001)
    if is_clear(closest):
        candidates.append(closest)
    up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    lateral = Vector((0, 1, 0))
    if weapon_inverse is not None:
        rotation = weapon_inverse.to_3x3().inverted()
        up, lateral = rotation @ up, rotation @ lateral
    for index in range(24):
        angle = index*math.tau/24
        direction = up*math.cos(angle)+lateral*math.sin(angle)
        lower, upper = 0.0, .050
        if not is_clear(point+direction*upper):
            continue
        for _ in range(12):
            middle = (lower+upper)*.5
            if is_clear(point+direction*middle):
                upper = middle
            else:
                lower = middle
        candidates.append(point+direction*(upper+.0001))
    if not candidates:
        raise ValueError('No safe local hand contact correction within 5cm')
    return min(candidates, key=lambda candidate: (candidate-point).length_squared)


def resolve_hand_contacts(body, weapon_inverse=None):
    """Small corrective hand shape; never modify the gun or any non-hand vertex."""
    from human_hands import GRIP_KEY
    surface = build_stock_surface()
    rig = bpy.data.objects['Equipment | editable holding pose rig']
    keys = body.data.shape_keys.key_blocks
    changed = 0
    maximum = 0.0
    for side in (-1, 1):
        group_index = body.vertex_groups[f'hand.{side}'].index
        bone = rig.pose.bones[f'hand.{side}']
        transform = bone.matrix @ rig.data.bones[bone.name].matrix_local.inverted() @ body.matrix_world
        inverse = transform.to_3x3().inverted()
        for vertex in body.data.vertices:
            rest = body.matrix_world @ vertex.co
            if abs(rest.x) <= .33 or not .73 < rest.z < .99:
                continue
            if not any(group.group == group_index and group.weight > .99 for group in vertex.groups):
                continue
            point = vertex.co.copy()
            for key in list(keys)[1:]:
                point += (key.data[vertex.index].co-keys[0].data[vertex.index].co)*key.value
            world = transform @ point
            correction = find_clear_contact(world, surface, weapon_inverse)-world
            if correction.length > .000001:
                # Leave a small elastic margin instead of projecting every vertex
                # onto exactly the same contact plane, which can flatten triangles.
                keys[GRIP_KEY].data[vertex.index].co += inverse @ (correction*.90)
                changed += 1
                maximum = max(maximum, correction.length)
    return {'corrected_vertices': changed, 'maximum_correction_m': maximum}
