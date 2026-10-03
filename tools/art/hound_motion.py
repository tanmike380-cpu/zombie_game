"""Foot-targeted clips on the user's original quadruped rig, never a replacement mesh."""
import math
import bpy
from mathutils import Matrix, Vector
from siege_motion import start_action, rotate_bone

# Original Tripo chains, identified from their rest positions, not humanoid names.
LEGS = (
    ('tripo::0_Left_Limb_4', 'tripo::0_Left_Limb_5', 0),
    ('bone_18', 'bone_19', .5),
    ('tripo::1_Left_Limb_3', 'tripo::1_Left_Limb_4', .5),
    ('tripo::0_Right_Limb_3', 'tripo::0_Right_Limb_4', 0),
)
STANCE = .58
SWEEP = .22
STRIDE = SWEEP / STANCE


def foot_offset(phase):
    """Constant ground speed in stance; eased lift/return while the opposite pair supports."""
    phase %= 1
    if phase < STANCE:
        return Vector((0, -SWEEP/2 + SWEEP*phase/STANCE, 0))
    flight = (phase-STANCE)/(1-STANCE)
    ease = flight*flight*(3-2*flight)
    return Vector((0, SWEEP/2-SWEEP*ease, .065*math.sin(math.pi*flight)))


def create_leg_targets(rig):
    targets = []
    for lower_name, foot_name, phase in LEGS:
        lower, foot = rig.pose.bones[lower_name], rig.pose.bones[foot_name]
        target = bpy.data.objects.new('Hound foot target', None)
        bpy.context.scene.collection.objects.link(target)
        target.matrix_world = rig.matrix_world @ foot.bone.matrix_local
        ik = lower.constraints.new('IK')
        ik.target = target
        ik.chain_count = 3
        ik.use_stretch = False
        ik.iterations = 100
        for bone in (lower, lower.parent, lower.parent.parent):
            bone.ik_stretch = 0
        rotation = foot.constraints.new('COPY_ROTATION')
        rotation.target = target
        targets.append((target, foot_name, phase, ik, rotation))
    return targets


def create_hound_actions(rig):
    """Bake IK to normal bone transforms; exported FBX needs no external constraint targets."""
    actions = {}
    for name, count in (('Idle', 48), ('Run', 32), ('Attack', 30)):
        action = start_action(rig, 'Hound_Planted_'+name)
        rig.location = (0, 0, 0)
        bpy.context.view_layer.update()
        targets = create_leg_targets(rig)
        snapshots = []
        for index in range(count+1):
            for bone in rig.pose.bones:
                bone.matrix_basis = Matrix.Identity(4)
            bpy.context.view_layer.update()
            phase = index/count
            for target, foot_name, offset, _, _ in targets:
                foot = rig.data.bones[foot_name]
                target.matrix_world = rig.matrix_world @ foot.matrix_local
                if name == 'Run':
                    target.location += foot_offset(phase+offset)
            bite = math.sin(math.pi*phase)**2 if name == 'Attack' else 0
            rotate_bone(rig, 'tripo::Head_0', -.20*bite + (.012*math.sin(phase*math.tau) if name == 'Idle' else 0))
            rotate_bone(rig, 'tripo::Head_1', .24*bite)
            bpy.context.view_layer.update()
            snapshots.append({bone.name: bone.matrix.copy() for bone in rig.pose.bones})
        for target, foot_name, _, ik, rotation in targets:
            rig.pose.bones[foot_name].constraints.remove(rotation)
            bpy.data.objects.remove(target, do_unlink=True)
        # Constraints belong to pose bones, not the armature ID.
        for bone in rig.pose.bones:
            for constraint in list(bone.constraints):
                if constraint.type == 'IK':
                    bone.constraints.remove(constraint)
        meshes = [obj for obj in rig.children if obj.type == 'MESH']
        for index, pose in enumerate(snapshots):
            rig.location = (0, 0, 0)
            for bone in rig.pose.bones:
                bone.matrix = pose[bone.name]
                bpy.context.view_layer.update()
                bone.keyframe_insert('location', frame=index+1)
                bone.keyframe_insert('rotation_quaternion', frame=index+1)
            bpy.context.view_layer.update()
            graph = bpy.context.evaluated_depsgraph_get()
            lowest = min((obj.matrix_world @ vertex.co).z for obj in meshes
                         for vertex in obj.evaluated_get(graph).data.vertices)
            rig.location.z = -lowest
            rig.keyframe_insert('location', frame=index+1)
        actions[name] = action
    return actions
