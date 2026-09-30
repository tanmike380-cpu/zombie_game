"""Author a temporary charge loop on the user's unchanged 67-bone Tripo exploder rig."""
import math
import sys
from pathlib import Path
import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0, str(Path(__file__).parent))
from tripo_model_io import import_model, audit_model, setup_render

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/TripoExploder"


def rotate_bone(rig, name, angle):
    bone = rig.pose.bones[name]
    axis = bone.bone.matrix_local.to_quaternion().inverted() @ Vector((1, 0, 0))
    bone.rotation_quaternion = Quaternion(axis, angle)


def create_actions(rig):
    """Keep mesh weights and rest bones untouched; these are draft motions, not supplied Tripo clips."""
    rig.animation_data_create()
    actions = {}
    for name, duration in (("Idle", 30), ("Run", 24), ("Attack", 24)):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        for frame in range(duration + 1):
            phase = frame / duration * math.tau
            for bone in rig.pose.bones:
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = Quaternion()
                bone.location = (0, 0, 0)
            if name == "Run":
                for side, sign in (("Left", 1), ("Right", -1)):
                    swing = math.sin(phase) * sign
                    rotate_bone(rig, side + "_UpperLeg", swing * .42)
                    rotate_bone(rig, side + "_LowerLeg", max(0, -swing) * .52)
                    rotate_bone(rig, side + "_UpperArm", -swing * .24)
                    rotate_bone(rig, side + "_LowerArm", -.2)
                rig.pose.bones["Hips"].location.y = abs(math.sin(phase)) * .014
            elif name == "Attack":
                rotate_bone(rig, "Spine", -.08 * math.sin(phase))
            else:
                rotate_bone(rig, "Spine", .012 * math.sin(phase))
            for bone in rig.pose.bones:
                bone.keyframe_insert("rotation_quaternion", frame=frame + 1)
                bone.keyframe_insert("location", frame=frame + 1)
        actions[name] = action
    return actions


def main():
    rig, mesh = import_model(REVIEW / "source")
    audit_model(rig, mesh, REVIEW / "source-audit.json")
    original = [(bone.name, tuple(bone.head_local), tuple(bone.tail_local)) for bone in rig.data.bones]
    actions = create_actions(rig)
    assert original == [(bone.name, tuple(bone.head_local), tuple(bone.tail_local)) for bone in rig.data.bones]
    setup_render()
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.cycles.samples = 8
    scene.render.resolution_percentage = 70
    rig.animation_data.action = actions["Run"]
    scene.frame_set(7)
    master = ROOT / "art/models/tripo-exploder"
    master.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(master / "TripoExploder.blend"))
    scene.render.filepath = str(REVIEW / "charge-draft.png")
    bpy.ops.render.render(write_still=True)
    output = ROOT / "Assets/_Game/Art/TripoExploder"
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True); mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(output / "TripoExploder.fbx"), use_selection=True,
                            add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=False,
                            bake_anim_use_all_actions=True, bake_anim_simplify_factor=0,
                            path_mode="COPY", embed_textures=True, axis_forward="-Z", axis_up="Y")
    print("[TripoExploder] 67 original bones and original skin weights preserved; draft idle/run/attack exported")


if __name__ == "__main__":
    main()
