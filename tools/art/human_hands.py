"""Reversible grip corrections for the approved human base; no rest-mesh edits."""
import math

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
