"""Eight-direction vehicle choreography on the existing rigid Tripo rig, not unit balance."""
import math

import bpy
from mathutils import Matrix, Vector

from siege_motion import WHEELS, interpolate_keys, start_action

SEGMENT_FRAMES = 72
DIRECTIONS = [
    ("Forward", 0, 1), ("Backward", 0, -1),
    ("Left", math.pi/2, 1), ("Right", -math.pi/2, 1),
    ("FrontLeft", math.pi/4, 1), ("FrontRight", -math.pi/4, 1),
    ("BackLeft", math.pi*3/4, 1), ("BackRight", -math.pi*3/4, 1),
]
MOVEMENT_END = SEGMENT_FRAMES * len(DIRECTIONS)


def wheel_roll_angle(distance, lateral_offset, yaw, radius=.125):
    """Blender is Z-up, chassis forward +Y: bottom of a forward-rolling wheel moves -Y."""
    if radius<=0:raise ValueError('Wheel radius must be positive')
    return -(distance+lateral_offset*yaw)/radius


def sample_vehicle_motion(frame):
    """Return yaw and signed path distance; turn while stopped, then roll out and back."""
    segment = min((frame-1)//SEGMENT_FRAMES, len(DIRECTIONS)-1)
    local_frame = (frame-1) % SEGMENT_FRAMES + 1
    name, heading, travel_sign = DIRECTIONS[segment]
    yaw = interpolate_keys(local_frame, [(1, 0), (14, heading), (58, heading), (72, 0)])
    distance = travel_sign * interpolate_keys(local_frame, [(1, 0), (15, 0), (33, .55),
                                                          (39, .55), (57, 0), (72, 0)])
    return name, yaw, distance


def create_directional_movement(rig):
    """Bake chassis yaw/world travel and axle rolling without replacing original bones."""
    action = start_action(rig, "GreekFire_EightDirectionMovement_Draft")
    rig.rotation_mode = "XYZ"
    for frame in range(1, MOVEMENT_END+1):
        _, yaw, distance = sample_vehicle_motion(frame)
        rig.location.x = -math.sin(yaw)*distance
        rig.location.y = math.cos(yaw)*distance
        rig.rotation_euler = (0, 0, yaw)
        rig.keyframe_insert("location", frame=frame)
        rig.keyframe_insert("rotation_euler", frame=frame)
        for name, center_x, center_y in WHEELS:
            bone = rig.pose.bones[name]
            pivot = Vector((center_x, center_y, .137))
            wheel_angle = wheel_roll_angle(distance,center_x,yaw)
            bone.matrix = (Matrix.Translation(pivot) @ Matrix.Rotation(wheel_angle, 4, "X")
                           @ Matrix.Translation(-pivot) @ bone.bone.matrix_local)
            bpy.context.view_layer.update()
            for channel in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(channel, frame=frame)
    return action
