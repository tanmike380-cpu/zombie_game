"""Foot-targeted in-place gait on original Tripo bones; no rest-bone or mesh edits."""
import math
import bpy
from mathutils import Matrix, Vector
from tripo_model_io import resolve_humanoid_bone

STANCE = .56


def place_leg(rig, side, target, forward):
    """Two-bone analytic IK with an explicit forward knee pole, preserving limb lengths."""
    thigh, shin, foot = [resolve_humanoid_bone(rig, side+'_'+part)
                         for part in ('UpperLeg', 'LowerLeg', 'Foot')]
    hip = thigh.head.copy()
    vector = target-hip
    distance = vector.length
    if distance >= thigh.length+shin.length-.00001:
        raise ValueError(f'{rig.name}: {side} foot target exceeds original leg reach')
    axis = vector.normalized()
    along = (thigh.length**2-shin.length**2+distance**2)/(2*distance)
    pole = (forward-axis*forward.dot(axis)).normalized()
    knee = hip+axis*along+pole*math.sqrt(max(0, thigh.length**2-along**2))
    for bone, start, end in ((thigh, hip, knee), (shin, knee, target)):
        rotation = (bone.bone.tail_local-bone.bone.head_local).rotation_difference(end-start)
        bone.matrix = Matrix.Translation(start) @ rotation.to_matrix().to_4x4() @ bone.bone.matrix_local.to_3x3().to_4x4()
        bpy.context.view_layer.update()
    foot.matrix = Matrix.Translation(target) @ foot.bone.matrix_local.to_3x3().to_4x4()
    bpy.context.view_layer.update()


def pose_gait(rig, phase, forward=Vector((0, -1, 0))):
    """Planted stance, raised passing foot and opposing steps; return source-space stride."""
    hips = resolve_humanoid_bone(rig, 'Hips')
    leg_length = sum(resolve_humanoid_bone(rig, 'Left_'+part).length for part in ('UpperLeg', 'LowerLeg'))
    sweep = leg_length*.55
    matrix = hips.matrix.copy()
    matrix.translation.z -= leg_length*(.085+.014*math.cos(phase*math.tau*2))
    hips.matrix = matrix
    bpy.context.view_layer.update()
    for side, offset in (('Left', 0), ('Right', .5)):
        foot = resolve_humanoid_bone(rig, side+'_Foot')
        step = (phase+offset) % 1
        if step < STANCE:
            travel = sweep*(.5-step/STANCE)
            lift = 0
        else:
            flight = (step-STANCE)/(1-STANCE)
            # Continuous position and velocity at lift-off/contact.
            ease = flight*flight*(3-2*flight)
            travel = sweep*(ease-.5)
            lift = leg_length*.17*math.sin(math.pi*flight)**2
        target = foot.bone.head_local+forward*travel+Vector((0, 0, lift))
        place_leg(rig, side, target, forward)
    return sweep/STANCE


def create_gait(rig, name, upper_pose=None):
    """Bake original bones, verify longitudinal foot travel and seamless loop endpoints."""
    from siege_motion import start_action
    action = start_action(rig, name)
    samples = {side: [] for side in ('Left', 'Right')}
    for frame in range(33):
        for bone in rig.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        if upper_pose:
            upper_pose(frame/32)
        stride = pose_gait(rig, frame/32)
        for bone in rig.pose.bones:
            bone.keyframe_insert('location', frame=frame+1)
            bone.keyframe_insert('rotation_quaternion', frame=frame+1)
        for side in samples:
            samples[side].append(resolve_humanoid_bone(rig, side+'_Foot').head.copy())
    for side, points in samples.items():
        width = max(p.x for p in points)-min(p.x for p in points)
        length = max(p.y for p in points)-min(p.y for p in points)
        assert width < .0001 and length > .15, f'{rig.name}: lateral gait / short stride {side}'
        assert (points[0]-points[-1]).length < .0001, 'Open gait loop'
    return action, stride


def add_motion_markers(rig, stride):
    """Export stride and stature to Unity so cycle speed follows actual travelled distance."""
    for name, position in (('ForwardOrigin', (0,0,0)), ('ForwardAim',(0,-1,0)),
                           ('StrideEnd',(0,-stride,0)), ('StatureTop',(0,0,1))):
        marker = next((obj for obj in rig.children if obj.name == name), None)
        if marker is None:
            marker = bpy.data.objects.new(name, None)
            bpy.context.scene.collection.objects.link(marker)
            marker.parent = rig
        marker.location = position
