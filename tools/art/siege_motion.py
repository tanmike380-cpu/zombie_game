"""Draft motion clips on imported rigs; no gameplay timing or balance authoring."""
import math

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector

FRAME_END = 120
WHEELS = [("bone_12", -.225, .145), ("bone_13", .225, .145),
          ("bone_14", -.225, -.26), ("bone_22", .225, -.26)]
TURRET_PIVOT = (0, -.19, .35)


def interpolate_keys(frame, keys):
    """Smoothly interpolate an artist-authored scalar pose curve."""
    for (start, first), (end, last) in zip(keys, keys[1:]):
        if start <= frame <= end:
            fraction = (frame - start) / (end - start)
            fraction = fraction * fraction * (3 - 2 * fraction)
            return first + (last - first) * fraction
    return keys[0][1] if frame < keys[0][0] else keys[-1][1]


def start_action(rig, name):
    """Attach a new action without overwriting any imported action."""
    rig.animation_data_clear()
    rig.animation_data_create()
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion()
        bone.location = (0, 0, 0)
        bone.scale = (1, 1, 1)
    return action


def rotate_bone(rig, name, angle, axis=(1, 0, 0)):
    """Set a rotation about an armature-space axis, keeping source rest bones intact."""
    bone = rig.pose.bones[name]
    local_axis = bone.bone.matrix_local.to_quaternion().inverted() @ Vector(axis)
    bone.rotation_quaternion = Quaternion(local_axis, angle)


def key_pose(rig, frame, names):
    """Bake only animated channels; unchanged limbs retain their imported rest pose."""
    for name in names:
        rig.pose.bones[name].keyframe_insert("rotation_quaternion", frame=frame)


def pose_leg_step(rig, side, offset):
    """Solve the original two leg bones toward a foot target without scaling bones."""
    thigh, shin, foot = [rig.pose.bones[f"{side}_{part}"]
                         for part in ("UpperLeg", "LowerLeg", "Foot")]
    bpy.context.view_layer.update()
    hip = thigh.head.copy()
    ankle = foot.bone.head_local + Vector(offset)
    direction = (ankle - hip).normalized()
    upper_length, lower_length = thigh.bone.length, shin.bone.length
    distance = (ankle - hip).length
    if distance >= upper_length + lower_length:
        raise ValueError(f"Unreachable {side} step target: {distance:.4f}")
    along = (upper_length**2 - lower_length**2 + distance**2) / (2 * distance)
    rest_direction = (foot.bone.head_local - thigh.bone.head_local).normalized()
    knee_side = shin.bone.head_local - thigh.bone.head_local
    knee_side -= rest_direction * knee_side.dot(rest_direction)
    knee_side = rest_direction.rotation_difference(direction) @ knee_side.normalized()
    knee = hip + direction * along + knee_side * math.sqrt(max(0, upper_length**2-along**2))
    for bone, start, end in ((thigh, hip, knee), (shin, knee, ankle)):
        rotation = (bone.bone.tail_local - bone.bone.head_local).rotation_difference(end-start)
        bone.matrix = Matrix.Translation(start) @ rotation.to_matrix().to_4x4() @ bone.bone.matrix_local.to_3x3().to_4x4()
        bpy.context.view_layer.update()
    foot.matrix = Matrix.Translation(ankle) @ foot.bone.matrix_local.to_3x3().to_4x4()
    bpy.context.view_layer.update()


def create_headbutt(rig):
    """Weight shift, lead-foot step, fast whole-body ram, recoil and planted recovery."""
    names = ["Spine", "Chest", "UpperChest", "Neck", "Head",
             "Left_UpperArm", "Right_UpperArm", "Left_LowerArm", "Right_LowerArm"]
    leg_names = [f"{side}_{part}" for side in ("Left", "Right")
                 for part in ("UpperLeg", "LowerLeg", "Foot")]
    missing = set(names + leg_names + ["Hips"]) - set(rig.pose.bones.keys())
    if missing:
        raise ValueError(f"Headbutter rig lacks required source bones: {sorted(missing)}")
    action = start_action(rig, "Headbutter_StepAndHeavyRam_Draft")
    for frame in range(1, FRAME_END + 1):
        # Slow anticipation and a six-frame strike; the planted step precedes contact.
        drive = interpolate_keys(frame, [(1, 0), (12, 0), (39, -.32), (51, -.32),
                                         (60, .44), (62, .44), (69, .20), (82, .12), (112, 0), (120, 0)])
        brace = interpolate_keys(frame, [(1, 0), (42, 1), (63, 1), (95, 0), (120, 0)])
        hip_y = interpolate_keys(frame, [(1, 0), (12, 0), (28, .12), (42, .03), (51, 0),
                                         (60, -.18), (63, -.18), (72, -.09), (84, -.09), (112, 0), (120, 0)])
        hip_z = interpolate_keys(frame, [(1, 0), (39, -.03), (51, -.03),
                                         (60, -.04), (69, -.05), (84, -.035), (112, 0), (120, 0)])
        hips = rig.pose.bones["Hips"]
        hips.location = hips.bone.matrix_local.to_3x3().inverted() @ Vector((0, hip_y, hip_z))
        hips.keyframe_insert("location", frame=frame)
        rotate_bone(rig, "Spine", drive * .55)
        rotate_bone(rig, "Chest", drive * .30)
        rotate_bone(rig, "UpperChest", drive * .15)
        rotate_bone(rig, "Neck", -drive * .30)
        rotate_bone(rig, "Head", -drive * .25)
        for side in ("Left", "Right"):
            rotate_bone(rig, side + "_UpperArm", -drive * .95 + brace * 1.10)
            rotate_bone(rig, side + "_LowerArm", -brace * 1.80)
        key_pose(rig, frame, names)
        step_y = interpolate_keys(frame, [(1, 0), (30, 0), (48, -.29), (94, -.29), (112, 0), (120, 0)])
        step_z = interpolate_keys(frame, [(1, 0), (30, 0), (39, .095), (48, 0),
                                          (94, 0), (103, .075), (112, 0), (120, 0)])
        rear_y = interpolate_keys(frame, [(1, 0), (16, 0), (32, -.12), (80, -.12), (94, 0), (120, 0)])
        rear_z = interpolate_keys(frame, [(1, 0), (16, 0), (24, .075), (32, 0),
                                          (80, 0), (87, .065), (94, 0), (120, 0)])
        pose_leg_step(rig, "Left", (0, step_y, step_z))
        pose_leg_step(rig, "Right", (0, rear_y, rear_z))
        for name in leg_names:
            bone = rig.pose.bones[name]
            bone.keyframe_insert("location", frame=frame)
            bone.keyframe_insert("rotation_quaternion", frame=frame)
    return action


def assign_rigid_turret(mesh, source_audit):
    """Bind complete upper islands to one rigid turret; preserve chassis and all UVs."""
    nozzle = source_audit["components"][0]
    if not (nozzle["count"] == 316 and nozzle["max"][1] > .49):
        raise ValueError("Unexpected Greek-fire topology; re-audit nozzle island before binding")
    all_indices = list(range(len(mesh.data.vertices)))
    for group in mesh.vertex_groups:
        group.remove(all_indices)
    root_group = mesh.vertex_groups.get("tripo::Root")
    nozzle_group = mesh.vertex_groups.get("tripo::Head_0")
    if root_group is None or nozzle_group is None:
        raise ValueError("Expected original Tripo mechanical source groups not found")
    turret_indices = {index for part in source_audit["components"]
                      if part["min"][2] > .27 and part["max"][2] > .42
                      for index in part["indices"]}
    if not set(nozzle["indices"]).issubset(turret_indices) or len(turret_indices) < 1000:
        raise ValueError("Upper turret selection must include the boiler, nozzle and fittings")
    root_group.add([index for index in all_indices if index not in turret_indices], 1, "REPLACE")
    nozzle_group.add(list(turret_indices), 1, "REPLACE")
    for name, center_x, center_y in WHEELS:
        wheel_indices = []
        for part in source_audit["components"]:
            low, high = part["min"], part["max"]
            if (low[2] >= .009 and high[2] <= .29 and
                    low[0] > center_x-.055 and high[0] < center_x+.055 and
                    low[1] > center_y-.135 and high[1] < center_y+.135):
                wheel_indices.extend(part["indices"])
        if len(wheel_indices) < 74:
            raise ValueError(f"Wheel identification failed for {name}")
        root_group.remove(wheel_indices)
        mesh.vertex_groups[name].add(wheel_indices, 1, "REPLACE")
    return len(turret_indices)


def create_greek_fire(rig):
    """Demonstrate tracking, acquiring a target, firing and returning to center."""
    action = start_action(rig, "GreekFire_UpperTurretTrackAndSpray_Draft")
    for frame in range(1, FRAME_END + 1):
        travel = interpolate_keys(frame, [(1, 0), (18, 0), (90, .22), (100, .22), (120, 0)])
        rig.location.y = travel
        rig.keyframe_insert("location", frame=frame)
        yaw = interpolate_keys(frame, [(1, 0), (20, .26), (42, -.26),
                                      (55, .12), (91, .12), (108, 0), (120, 0)])
        turret = rig.pose.bones["tripo::Head_0"]
        pivot = Vector(TURRET_PIVOT)
        turret.matrix = (Matrix.Translation(pivot) @ Matrix.Rotation(yaw, 4, "Z")
                         @ Matrix.Translation(-pivot) @ turret.bone.matrix_local)
        bpy.context.view_layer.update()
        turret.keyframe_insert("location", frame=frame)
        turret.keyframe_insert("rotation_quaternion", frame=frame)
        for name, center_x, center_y in WHEELS:
            bone = rig.pose.bones[name]
            center = Vector((center_x, center_y, .137))
            # Rigid rotation around the measured axle, preserving source rest matrices.
            bone.matrix = (Matrix.Translation(center) @ Matrix.Rotation(-travel/.125, 4, "X")
                           @ Matrix.Translation(-center) @ bone.bone.matrix_local)
            bpy.context.view_layer.update()
            bone.keyframe_insert("rotation_quaternion", frame=frame)
            bone.keyframe_insert("location", frame=frame)
            bone.keyframe_insert("scale", frame=frame)
    return action


def remove_support_feet(mesh, source_audit):
    """Remove only four audited disconnected stabilizer islands, preserving all wheels."""
    supports = [part for part in source_audit["components"]
                if part["min"][2] < .012 and part["max"][2] < .17
                and (part["min"][1] > .27 or part["max"][1] < -.32)]
    if sorted(part["count"] for part in supports) != [44, 44, 46, 46]:
        raise ValueError("Cannot identify exactly four original stabilizer islands; refusing mesh deletion")
    indices = {index for part in supports for index in part["indices"]}
    # Remove the separate bolt heads mounted on the deleted ground pads as well.
    for part in source_audit["components"]:
        if part["max"][2] < .05 and (part["min"][1] > .37 or part["max"][1] < -.43):
            indices.update(part["indices"])
    editable = bmesh.new()
    editable.from_mesh(mesh.data)
    editable.verts.ensure_lookup_table()
    bmesh.ops.delete(editable, geom=[editable.verts[index] for index in indices], context="VERTS")
    editable.to_mesh(mesh.data)
    editable.free()
    mesh.data.update()
    return len(indices)


def measure_rig_signature(rig):
    """Capture names and rest matrices so authoring cannot silently replace the rig."""
    return [(bone.name, tuple(value for row in bone.matrix_local for value in row))
            for bone in rig.data.bones]
