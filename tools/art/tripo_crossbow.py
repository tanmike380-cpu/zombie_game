"""Crossbow animation review on the 67 original Tripo bones; never rebuild the body rig."""
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Matrix, Vector, Quaternion

sys.path.insert(0, str(Path(__file__).parent))
from tripo_model_io import import_model, audit_model, setup_render

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/TripoCrossbow"


def set_bone(rig, name, head, rotation):
    bone = rig.pose.bones[name]
    bone.matrix = Matrix.Translation(Vector(head)) @ rotation.to_matrix().to_4x4()
    bpy.context.view_layer.update()


def point_bone(rig, name, head, direction):
    bone = rig.data.bones[name]
    rotation = (bone.tail_local - bone.head_local).rotation_difference(Vector(direction)) @ bone.matrix_local.to_quaternion()
    set_bone(rig, name, head, rotation)


def pose_arm(rig, side, hand_matrix):
    upper, lower = rig.pose.bones[side + "_UpperArm"], rig.pose.bones[side + "_LowerArm"]
    shoulder = upper.head.copy()
    wrist = hand_matrix.translation
    vector = wrist - shoulder
    distance = min(vector.length, upper.length + lower.length - .0001)
    axis = vector.normalized()
    along = (upper.length ** 2 - lower.length ** 2 + distance ** 2) / (2 * distance)
    height = math.sqrt(max(0, upper.length ** 2 - along ** 2))
    pole = Vector((.30 if side == "Left" else -.32, -.05, .64)) - shoulder
    bend = (pole - axis * pole.dot(axis)).normalized()
    elbow = shoulder + axis * along + bend * height
    point_bone(rig, upper.name, shoulder, elbow - shoulder)
    point_bone(rig, lower.name, elbow, wrist - elbow)
    set_bone(rig, side + "_Hand", wrist, hand_matrix.to_quaternion())
    return max(0, vector.length - upper.length - lower.length)


def reset_pose(rig):
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
        bone.rotation_mode = "QUATERNION"
    bpy.context.view_layer.update()


def weapon_weights(mesh, report):
    weapon = []
    for component in report["components"]:
        lo, hi = component["min"], component["max"]
        if component["count"] == 117 or (lo[0] > .015 and hi[1] < -.035 and hi[2] < .58 and lo[2] > .4):
            weapon.extend(component["indices"])
        elif component["count"] < 200 and lo[2] > .38 and hi[2] < .65:
            for group in mesh.vertex_groups:
                group.remove(component["indices"])
            mesh.vertex_groups["Hips"].add(component["indices"], 1, "REPLACE")
    if len(weapon) != 285:
        raise ValueError(f"Crossbow island audit changed: {len(weapon)} vertices, expected 285")
    for group in mesh.vertex_groups:
        group.remove(weapon)
    mesh.vertex_groups["Right_Hand"].add(weapon, 1, "REPLACE")
    return len(weapon)


def repair_waist_weights(rig, mesh, report):
    changed = 0
    for index in report["components"][0]["indices"]:
        point = mesh.data.vertices[index].co
        if not (.44 < point.z < .64 and -.17 < point.x < .105 and point.y > -.075):
            continue
        in_arm = False
        for side in ("Left", "Right"):
            arm = rig.data.bones[side + "_LowerArm"]
            delta = arm.tail_local - arm.head_local
            t = max(0, min(1, (point - arm.head_local).dot(delta) / delta.length_squared))
            if (point - arm.head_local - t * delta).length < .034:
                in_arm = True
            hand = rig.data.bones[side + "_Hand"]
            if (point - (hand.head_local + hand.tail_local) * .5).length < .044:
                in_arm = True
        if in_arm:
            continue
        for group in mesh.vertex_groups:
            group.remove([index])
        blend = max(0, min(1, (point.z - .56) / .065))
        if blend < 1:
            mesh.vertex_groups["Hips"].add([index], 1 - blend, "REPLACE")
        if blend > 0:
            mesh.vertex_groups["Spine"].add([index], blend, "REPLACE")
        changed += 1
    return changed


def weapon_pose(rig, amount, tilt=0, left_slide=0, reload_reach=0, bob=0):
    hips = rig.pose.bones["Hips"]
    set_bone(rig, hips.name, hips.head.copy(), Quaternion(Vector((0, 0, 1)), -.8 * amount) @ hips.bone.matrix_local.to_quaternion())
    chest = rig.pose.bones["UpperChest"]
    set_bone(rig, chest.name, chest.head.copy(), Quaternion(Vector((0, 0, 1)), -.9 * amount) @ chest.bone.matrix_local.to_quaternion())
    head = rig.pose.bones["Head"]
    set_bone(rig, head.name, head.head.copy(), head.bone.matrix_local.to_quaternion())
    right_rest = rig.data.bones["Right_Hand"].matrix_local.copy()
    left_rest = rig.data.bones["Left_Hand"].matrix_local.copy()
    direction = Vector((.362, -.033, -.071)).normalized()
    rotation = direction.rotation_difference(Vector((0, -math.cos(tilt), -math.sin(tilt))))
    rotation = Quaternion().slerp(rotation, amount)
    grip = right_rest.translation.lerp(Vector((-.06, -.065, .765)), amount) + Vector((0, 0, bob))
    delta = Matrix.Translation(grip) @ rotation.to_matrix().to_4x4() @ Matrix.Translation(-right_rest.translation)
    right = delta @ right_rest
    left = delta @ left_rest
    left.translation += Vector((0, left_slide, 0))
    left.translation = left.translation.lerp(Vector((-.14, .035, .555)), reload_reach)
    error_right = pose_arm(rig, "Right", right)
    error_left = pose_arm(rig, "Left", left)
    return max(error_right, error_left)


def create_actions(rig):
    errors = []
    def idle(t):
        return weapon_pose(rig, .12, bob=math.sin(t * math.tau) * .002)
    def aim(t):
        return weapon_pose(rig, .12 + .88 * min(1, t / .8))
    def attack(t):
        if t < .12:
            return weapon_pose(rig, 1, bob=-.002 * math.sin(t / .12 * math.pi))
        if t < .32:
            return weapon_pose(rig, 1 - .35 * (t - .12) / .2, tilt=.35)
        if t < .55:
            return weapon_pose(rig, .65, tilt=.35, left_slide=.11 * (t - .32) / .23)
        if t < .72:
            return weapon_pose(rig, .65, tilt=.35, reload_reach=math.sin((t - .55) / .17 * math.pi) * .8)
        return weapon_pose(rig, .65 + .35 * (t - .72) / .28, tilt=.35 * (1 - (t - .72) / .28))
    def run(t):
        from humanoid_gait import pose_gait
        error = weapon_pose(rig, .18, bob=.002 * math.cos(t*math.tau*2))
        pose_gait(rig, t)
        return error
    actions = {}
    for name, duration, pose in [("Idle", 60, idle), ("Run", 24, run), ("Aim", 30, aim), ("Attack", 54, attack)]:
        rig.animation_data_create()
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        for frame in range(duration + 1):
            reset_pose(rig)
            errors.append(pose(frame / duration))
            for bone in rig.pose.bones:
                bone.keyframe_insert("location", frame=frame + 1)
                bone.keyframe_insert("rotation_quaternion", frame=frame + 1)
        actions[name] = action
    if max(errors) > .018:
        raise ValueError(f"Arm reach exceeds original skeleton by {max(errors):.4f} m")
    return actions, max(errors)


def main():
    rig, mesh = import_model(REVIEW / "source")
    report = audit_model(rig, mesh, REVIEW / "source-audit.json")
    original_bones = [(b.name, list(b.head_local), list(b.tail_local)) for b in rig.data.bones]
    changed_vertices = weapon_weights(mesh, report)
    repaired_waist = 0
    actions, max_reach_error = create_actions(rig)
    from humanoid_gait import add_motion_markers, STANCE
    stride=(rig.pose.bones['Left_UpperLeg'].length+rig.pose.bones['Left_LowerLeg'].length)*.55/STANCE
    add_motion_markers(rig,stride)
    if original_bones != [(b.name, list(b.head_local), list(b.tail_local)) for b in rig.data.bones]:
        raise ValueError("Source skeleton was changed")
    setup_render()
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 1, 55
    rig.animation_data.action = actions["Attack"]
    scene.frame_set(1)
    target = ROOT / "art/models/tripo-crossbow"
    target.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(target / "ByzantineCrossbow.blend"))
    for name, frame in [("Idle", 1), ("Aim", 31), ("Attack", 25), ("Run", 7)]:
        rig.animation_data.action = actions[name]
        scene.frame_set(frame)
        scene.render.filepath = str(REVIEW / (name.lower() + ".png"))
        bpy.ops.render.render(write_still=True)
    output = ROOT / "Assets/_Game/Art/TripoCrossbow"
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    mesh.select_set(True)
    for child in rig.children:
        if child.type=='EMPTY':child.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(output / "ByzantineCrossbow.fbx"), use_selection=True, add_leaf_bones=False,
                            bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
                            bake_anim_simplify_factor=0, path_mode="COPY", embed_textures=True, axis_forward="-Z", axis_up="Y")
    (REVIEW / "animation-audit.json").write_text(json.dumps({"source_bones": len(original_bones), "export_bones": len(rig.data.bones),
        "original_rest_bones_unchanged": True, "rigid_crossbow_vertices": changed_vertices,
        "waist_weight_repairs": repaired_waist, "max_arm_reach_error_source_m": max_reach_error, "actions": list(actions)}, indent=2))


if __name__ == "__main__":
    main()
