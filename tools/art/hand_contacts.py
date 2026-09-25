"""Fit separate phalange chains to the unchanged stock using surface distances."""
import math

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from human_hands import GUN_AXIS, GUN_ORIGIN, build_finger_transforms, get_anatomical_basis, get_rest_joints


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


def get_barrel_distance(point):
    relative = point-GUN_ORIGIN
    along = relative.dot(GUN_AXIS)
    up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    radius = .016 if along <= .68 else .0125
    radial = math.hypot(relative.y, relative.dot(up)-.022)-radius
    axial = max(.24-along, along-1.50, 0)
    return math.hypot(max(0, radial), axial) if axial else radial


def score_finger(parameters, joints, transform, surface, name, thumb_target):
    transforms = build_finger_transforms(joints, parameters[:3], name == 'thumb', parameters[3])
    radius = {'index': .0075, 'middle': .008, 'ring': .0075, 'little': .006, 'thumb': .009}[name]
    score = 0
    for segment in range(3):
        for fraction in (.35, .70, 1.0):
            local = transforms[segment] @ joints[segment].lerp(joints[segment+1], fraction)
            world = transform @ local
            distance = get_stock_distance(world, surface)
            clearance = radius*(1-.15*segment)
            score += 35*max(0, clearance-distance)**2
            score += 45*max(0, clearance+.001-get_barrel_distance(world))**2
            if segment == 2:
                score += (distance-clearance-.001)**2*(4 if fraction == 1 else 1)
    if name == 'thumb':
        tip = transform @ (transforms[2] @ joints[3])
        score += 6*(tip-thumb_target).length_squared
    return score


def optimize_finger(initial, joints, transform, surface, name, thumb_target):
    parameters = [15, initial[1], initial[2], 0.0]
    bounds = [(-15, 45), (10, 90), (0, 60), (-7, 7)]
    if name == 'thumb':
        parameters = [15, 20, 15, 0.0]
        bounds = [(-80, 80), (-10, 65), (0, 45), (-90, 90)]
    score = score_finger(parameters, joints, transform, surface, name, thumb_target)
    if name == 'thumb':
        for opposition in (-60, 0, 60):
            for adduction in (-70, 0, 70):
                for flexion in (0, 30, 60):
                    candidate = [opposition, flexion, 20, adduction]
                    cost = score_finger(candidate, joints, transform, surface, name, thumb_target)
                    if cost < score:
                        parameters, score = candidate, cost
    for step in (20, 10, 5, 2):
        for _ in range(8):
            improved = False
            for index in range(4):
                for sign in (-1, 1):
                    candidate = parameters.copy()
                    candidate[index] = max(bounds[index][0], min(bounds[index][1], candidate[index]+sign*step))
                    cost = score_finger(candidate, joints, transform, surface, name, thumb_target)
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


def fit_finger_contacts(body, side, config, chains, transform):
    """Solve within joint-angle limits; never reposition wrist, gun or arm."""
    surface = build_stock_surface()
    role = 'trigger' if side < 0 else 'support'
    thumb_target = GUN_ORIGIN+GUN_AXIS*(.35 if side < 0 else .72)+Vector((0, .025, -.030))
    solved = {}
    for name, entry in config.items():
        parameters, score = optimize_finger(entry[role], chains[name], transform, surface, name, thumb_target)
        solved[name] = parameters
        print('FINGER_CONTACT_FIT', role, name, parameters, round(score, 6), flush=True)
    return solved


def measure_hand_clearance(body):
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
            distance = min(get_stock_distance(world, surface), get_barrel_distance(world))
            tested += 1
            if distance < -.002:
                penetrations += 1
                maximum_depth = max(maximum_depth, -distance)
        report[str(side)] = {'tested_vertices': tested, 'deep_intersections': penetrations,
                              'maximum_depth_m': maximum_depth}
    return report


def find_clear_contact(point, surface):
    """Find the smallest local pad correction outside both wood and barrel."""
    clearance = .0015
    def is_clear(candidate):
        return min(get_stock_distance(candidate, surface), get_barrel_distance(candidate)) >= clearance
    if is_clear(point):
        return point
    location, normal, _, _ = surface.find_nearest(point)
    candidates = []
    closest = location+normal*(clearance+.0001)
    if is_clear(closest):
        candidates.append(closest)
    up = Vector((-GUN_AXIS.z, 0, GUN_AXIS.x))
    for index in range(24):
        angle = index*math.tau/24
        direction = up*math.cos(angle)+Vector((0, 1, 0))*math.sin(angle)
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


def resolve_hand_contacts(body):
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
            correction = find_clear_contact(world, surface)-world
            if correction.length > .000001:
                # Leave a small elastic margin instead of projecting every vertex
                # onto exactly the same contact plane, which can flatten triangles.
                keys[GRIP_KEY].data[vertex.index].co += inverse @ (correction*.90)
                changed += 1
                maximum = max(maximum, correction.length)
    return {'corrected_vertices': changed, 'maximum_correction_m': maximum}
