"""Reversible grip corrections for the approved human base; no rest-mesh edits."""
import math
import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

GUN_ORIGIN = Vector((-.370, -.330, 1.320))
GUN_AXIS = Vector((.940, 0, -.342)).normalized()
GRIP_KEY = 'Equipment pose | relaxed fingers around matchlock'
KNUCKLE_DISTANCE = .082
GRIP_RADIUS = .036


def get_rest_joints(side):
    """Return the approved arm rig's world-space rest joints."""
    return (Vector((side*.178, .010, 1.443)),
            Vector((side*.282, .005, 1.214)),
            Vector((side*.382, -.055, .955)))


def get_hand_basis(side):
    forward = Vector((side*.330, 0, -.944)).normalized()
    across = Vector((0, -side, 0))
    return forward, across, forward.cross(across).normalized()


def refine_finger_curl(body):
    """Keep the palm straight; curl distal fingers with a larger grip radius."""
    key = body.data.shape_keys.key_blocks[GRIP_KEY]
    inverse = body.matrix_world.inverted()
    for source, target in zip(body.data.vertices, key.data):
        target.co = source.co
        point = body.matrix_world @ source.co
        if abs(point.x) < .33 or not .73 < point.z < .99:
            continue
        side = 1 if point.x > 0 else -1
        wrist = get_rest_joints(side)[2]
        forward, across, normal = get_hand_basis(side)
        relative = point-wrist
        length, width, depth = (relative.dot(axis) for axis in (forward, across, normal))
        if length > KNUCKLE_DISTANCE:
            arc_length = length-KNUCKLE_DISTANCE
            angle = min(2.35, arc_length/GRIP_RADIUS)
            # Continue tangentially after the final joint. Hard angle clamping
            # collapses all distal vertices onto one slice, flattening fingertips.
            extension = max(0, arc_length-2.35*GRIP_RADIUS)
            length = KNUCKLE_DISTANCE+(GRIP_RADIUS-depth)*math.sin(angle)+extension*math.cos(angle)
            depth = GRIP_RADIUS-(GRIP_RADIUS-depth)*math.cos(angle)+extension*math.sin(angle)
        target.co = inverse @ (wrist+forward*length+across*width+normal*depth)
    key.value = 1


def solve_elbow(shoulder, wrist, upper_length, forearm_length, side):
    delta = wrist-shoulder
    distance = delta.length
    if not abs(upper_length-forearm_length) < distance < upper_length+forearm_length:
        raise ValueError(f'Unreachable hand grip: side={side}, distance={distance:.3f}m')
    direction = delta/distance
    along = (upper_length**2-forearm_length**2+distance**2)/(2*distance)
    height = math.sqrt(max(0, upper_length**2-along**2))
    pole = Vector((side*.7, -.04, 1.16))-shoulder
    bend = (pole-direction*pole.dot(direction)).normalized()
    return shoulder+direction*along+bend*height


def set_world_bone(rig, name, head, rotation):
    rest = rig.data.bones[name]
    transform = Matrix.Translation(head) @ rotation.to_4x4() @ Matrix.Translation(-rest.head_local)
    rig.pose.bones[name].matrix = transform @ rest.matrix_local
    bpy.context.view_layer.update()


def refine_holding_pose(rig):
    """Align wrist and palm without altering arm lengths or gun placement."""
    report = {}
    for side, distance in [(-1, .320), (1, .730)]:
        shoulder, rest_elbow, rest_wrist = get_rest_joints(side)
        forward = Vector((.82, -.45, -.35) if side < 0 else (0, -.85, -.53)).normalized()
        across = (GUN_AXIS-forward*GUN_AXIS.dot(forward)).normalized()
        normal = forward.cross(across).normalized()
        grip = GUN_ORIGIN+GUN_AXIS*distance+Vector((0, 0, -.009))
        wrist = grip-forward*KNUCKLE_DISTANCE-normal*GRIP_RADIUS-across*(side*.045)
        elbow = solve_elbow(shoulder, wrist, (rest_elbow-shoulder).length,
                            (rest_wrist-rest_elbow).length, side)
        upper_rotation = (rest_elbow-shoulder).rotation_difference(elbow-shoulder).to_matrix()
        forearm_rotation = (rest_wrist-rest_elbow).rotation_difference(wrist-elbow).to_matrix()
        hand_rotation = Matrix((forward, across, normal)).transposed() @ Matrix(get_hand_basis(side))
        set_world_bone(rig, f'upper.{side}', shoulder, upper_rotation)
        set_world_bone(rig, f'forearm.{side}', elbow, forearm_rotation)
        set_world_bone(rig, f'hand.{side}', wrist, hand_rotation)
        angle = math.degrees(forward.angle(wrist-elbow))
        report[str(side)] = {'wrist_angle_degrees': angle, 'grip_world': list(grip),
                             'wrist_world': list(wrist), 'elbow_world': list(elbow)}
    return report


def restore_underarm_lining():
    """Reveal the existing inner cloth where the relaxed arms expose the armhole."""
    tunic = next(obj for obj in bpy.data.objects if obj.name.startswith('Tunic |'))
    group = tunic.vertex_groups['Equipment | inner tunic below finished neckline']
    indices = [vertex.index for vertex in tunic.data.vertices if vertex.co.z < 1.470]
    group.add(indices, 1, 'REPLACE')
    lining_group = tunic.vertex_groups.new(name='HumanBase | underarm lining fit')
    for vertex in tunic.data.vertices:
        weight = max(0, min(1, (vertex.co.z-1.255)/.035))
        lining_group.add([vertex.index], weight, 'REPLACE')
    modifier = tunic.modifiers.new('HumanBase | fit inner lining below outer sleeve', 'SHRINKWRAP')
    modifier.target = bpy.data.objects['Body | continuous anatomical foundation']
    modifier.wrap_method = 'NEAREST_SURFACEPOINT'
    modifier.wrap_mode = 'ON_SURFACE'
    modifier.offset = .003
    modifier.vertex_group = lining_group.name


def get_anatomical_basis(side):
    """Mirror both hands into the same palm-facing coordinate system."""
    forward, across, normal = get_hand_basis(side)
    return Matrix((forward, across*side, normal)).transposed()


def get_wrapped_palm_point(point):
    """Keep the accepted cupped palm while replacing only distal articulation."""
    length, width, depth = point
    if length <= KNUCKLE_DISTANCE:
        return point.copy()
    arc_length = length-KNUCKLE_DISTANCE
    angle = min(2.35, arc_length/GRIP_RADIUS)
    extension = max(0, arc_length-2.35*GRIP_RADIUS)
    return Vector((KNUCKLE_DISTANCE+(GRIP_RADIUS-depth)*math.sin(angle)+extension*math.cos(angle),
                   width, GRIP_RADIUS-(GRIP_RADIUS-depth)*math.cos(angle)+extension*math.sin(angle)))


def get_palm_frame(joint):
    angle = max(0, min(2.35, (joint.x-KNUCKLE_DISTANCE)/GRIP_RADIUS))
    return Matrix.Translation(get_wrapped_palm_point(joint)) @ Matrix.Rotation(-angle, 4, 'Y') @ Matrix.Translation(-joint)


def build_finger_transforms(joints, angles, is_thumb=False, adduction=0):
    """Forward-kinematics rotations around three distinct anatomical joints."""
    transforms = []
    parent = get_palm_frame(joints[0]) @ Matrix.Translation(joints[0]) @ Matrix.Rotation(math.radians(adduction), 4, 'Z') @ Matrix.Translation(-joints[0])
    for index, angle in enumerate(angles):
        direction = (joints[index+1]-joints[index]).normalized()
        axis = direction.cross(Vector((0, 0, 1))).normalized()
        if is_thumb and index == 0:
            axis = Vector((1, -.20, 0)).normalized()
        rotation = Matrix.Rotation(math.radians(angle), 4, axis)
        joint_transform = Matrix.Translation(joints[index]) @ rotation @ Matrix.Translation(-joints[index])
        parent = parent @ joint_transform
        transforms.append(parent.copy())
    return transforms


def locate_finger(point, chains):
    """Find the nearest phalanx; proximal falloff preserves palm webbing."""
    candidates = []
    for name, joints in chains.items():
        distance_along = 0
        for index in range(3):
            start, end = joints[index:index+2]
            segment = end-start
            parameter = max(0, min(1, (point-start).dot(segment)/segment.length_squared))
            closest = start+segment*parameter
            distance = (point-closest).length
            candidates.append((distance, name, index, parameter, distance_along+parameter*segment.length))
            distance_along += segment.length
    return min(candidates)


def blend_rigid_transforms(point, first, second, weight, pivot):
    """Quaternion rotation blending avoids collapsing knuckles like linear blends."""
    rotation_a, rotation_b = first.to_quaternion(), second.to_quaternion()
    rotation = rotation_a.slerp(rotation_b, weight)
    return first @ pivot+rotation @ (point-pivot)


def deform_finger_point(point, joints, transforms, segment_index, parameter, is_thumb=False):
    lengths = [(joints[index+1]-joints[index]).length for index in range(3)]
    root_width = .040 if is_thumb else .011
    if segment_index == 0 and parameter*lengths[0] < root_width:
        weight = max(0, min(1, parameter*lengths[0]/root_width))
        weight = weight*weight*(3-2*weight)
        first = get_palm_frame(joints[0])
        posed = blend_rigid_transforms(point, first, transforms[0], weight, joints[0])
        # Apply the joint rotation once. Blending a partially rotated position
        # again squares the falloff and pinches the thumb's thenar webbing.
        return posed+(get_wrapped_palm_point(point)-first @ point)*(1-weight)
    joint_width = .007
    start_distance = parameter*lengths[segment_index]
    end_distance = (1-parameter)*lengths[segment_index]
    if segment_index > 0 and start_distance < joint_width:
        return blend_rigid_transforms(point, transforms[segment_index-1], transforms[segment_index],
                                      .5+.5*start_distance/joint_width, joints[segment_index])
    if segment_index < 2 and end_distance < joint_width:
        return blend_rigid_transforms(point, transforms[segment_index], transforms[segment_index+1],
                                      .5-.5*end_distance/joint_width, joints[segment_index+1])
    return transforms[segment_index] @ point


def refine_articulated_grip(body, config_path):
    """Replace the rubber-tube curl with per-finger joint articulation only."""
    settings = json.loads(Path(config_path).read_text())
    config = settings['fingers']
    chains = {name: [Vector(point) for point in entry['joints']] for name, entry in config.items()}
    key = body.data.shape_keys.key_blocks[GRIP_KEY]
    inverse = body.matrix_world.inverted()
    changed = 0
    for side in (-1, 1):
        basis = get_anatomical_basis(side)
        wrist = get_rest_joints(side)[2]
        role = 'trigger' if side < 0 else 'support'
        from hand_contacts import fit_finger_contacts, get_hand_contact_frame
        transform, pitch = get_hand_contact_frame(side, settings['hand_pitch_degrees'][role])
        solved = fit_finger_contacts(body, side, config, chains, transform)
        transforms = {name: build_finger_transforms(chains[name], entry[:3], name == 'thumb', entry[3])
                      for name, entry in solved.items()}
        for vertex, target in zip(body.data.vertices, key.data):
            world = body.matrix_world @ vertex.co
            if world.x*side < .33 or not .73 < world.z < .99:
                continue
            point = basis.transposed() @ (world-wrist)
            distance, name, segment_index, parameter, _ = locate_finger(point, chains)
            deformed = get_wrapped_palm_point(point)
            if distance < (.033 if name == 'thumb' else .024):
                deformed = deform_finger_point(point, chains[name], transforms[name], segment_index, parameter, name == 'thumb')
            wrist_falloff = max(0, min(1, (point.x-.008)/.040))
            wrist_falloff = wrist_falloff*wrist_falloff*(3-2*wrist_falloff)
            deformed = deformed.lerp(pitch @ deformed, wrist_falloff)
            target.co = inverse @ (wrist+basis @ deformed)
            changed += 1
    return changed
