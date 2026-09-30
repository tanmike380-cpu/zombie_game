"""Author a temporary charge loop on the user's unchanged 67-bone Tripo exploder rig."""
import math
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Quaternion, Vector

sys.path.insert(0, str(Path(__file__).parent))
from tripo_model_io import import_model, audit_model, setup_render

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/TripoExploder"


def paint_pustules(mesh):
    """Strengthen the source's lavender pustule pigment, retaining its UVs and fine texture."""
    material = mesh.data.materials[0]
    texture = next(node.image for node in material.node_tree.nodes if node.type == "TEX_IMAGE" and node.image)
    pixels = np.array(texture.pixels[:], dtype=np.float32).reshape((-1, 4))
    rgb = pixels[:, :3]
    red, green, blue = rgb[:, 0].copy(), rgb[:, 1].copy(), rgb[:, 2].copy()
    # Diseased sacs are lavender; cloth, skin and leather are brown/yellow. Soft boundaries avoid paint seams.
    mask = np.clip((blue / (green + .0001) - 1.015) / .10, 0, 1)
    mask *= np.clip((red / (green + .0001) - 1.015) / .08, 0, 1)
    mask *= np.clip((np.minimum(red, blue) / (np.maximum(red, blue) + .0001) - .60) / .25, 0, 1)
    mask *= np.clip((np.max(rgb, axis=1) - .025) / .07, 0, 1)
    mask = expand_pustule_coverage(mesh, texture.size[:], mask)
    luminance = red * .2126 + green * .7152 + blue * .0722
    purple = luminance[:, None] * np.array([1.50, .22, 2.10], dtype=np.float32)
    rgb[:] = np.clip(rgb * (1 - mask[:, None] * .98) + purple * mask[:, None] * .98, 0, 1)
    texture.pixels.foreach_set(pixels.ravel())
    texture.filepath_raw = str(ROOT / "Assets/_Game/Art/TripoExploder/Textures/ExploderPustules.png")
    texture.file_format = "PNG"
    texture.save()
    texture.pack()
    print(f"[TripoExploder] recoloured lavender pustule texels={np.count_nonzero(mask > .5)}; topology and UVs unchanged")


def expand_pustule_coverage(mesh, size, pigment):
    """Expand existing pustule pigment over adjacent 3D surfaces, then paint through the original UVs."""
    width, height = size
    uv = np.array([loop.uv[:] for loop in mesh.data.uv_layers.active.data])
    positions = np.array([tuple(mesh.matrix_world @ vertex.co) for vertex in mesh.data.vertices])
    vertex_pigment = np.zeros(len(positions), dtype=np.float32)
    for loop in mesh.data.loops:
        u, v = uv[loop.index]
        sample = pigment[min(height-1, max(0, int(v*height))) * width + min(width-1, max(0, int(u*width)))]
        vertex_pigment[loop.vertex_index] = max(vertex_pigment[loop.vertex_index], sample)
    seeds = positions[(vertex_pigment > .4) & (positions[:, 2] > .57)]
    if not len(seeds):
        raise RuntimeError("No pustule colour seeds on upper body; refusing whole-body tint")
    weights = np.zeros(len(positions), dtype=np.float32)
    for start in range(0, len(positions), 256):
        distance = np.sqrt(((positions[start:start+256, None, :] - seeds[None, :, :])**2).sum(axis=2).min(axis=1))
        weights[start:start+256] = np.clip((.065 - distance) / .020, 0, 1)
    painted = np.zeros((height, width), dtype=np.float32)
    mesh.data.calc_loop_triangles()
    for triangle in mesh.data.loop_triangles:
        points = uv[list(triangle.loops)] * np.array([width-1, height-1])
        values = weights[list(triangle.vertices)]
        if values.max() == 0:
            continue
        low = np.maximum(0, np.floor(points.min(axis=0)).astype(int))
        high = np.minimum([width-1, height-1], np.ceil(points.max(axis=0)).astype(int))
        x, y = np.meshgrid(np.arange(low[0], high[0]+1), np.arange(low[1], high[1]+1))
        a, b, c = points
        denominator = (b[1]-c[1])*(a[0]-c[0]) + (c[0]-b[0])*(a[1]-c[1])
        if abs(denominator) < .0001:
            continue
        first = ((b[1]-c[1])*(x-c[0]) + (c[0]-b[0])*(y-c[1])) / denominator
        second = ((c[1]-a[1])*(x-c[0]) + (a[0]-c[0])*(y-c[1])) / denominator
        third = 1-first-second
        inside = (first >= -.002) & (second >= -.002) & (third >= -.002)
        interpolated = (first*values[0] + second*values[1] + third*values[2]) * inside
        patch = painted[low[1]:high[1]+1, low[0]:high[0]+1]
        np.maximum(patch, interpolated, out=patch)
    # Two texels of bleed protect UV seams and mipmaps without touching the source mesh.
    for _ in range(2):
        painted = np.maximum.reduce([painted, np.roll(painted, 1, 0), np.roll(painted, -1, 0), np.roll(painted, 1, 1), np.roll(painted, -1, 1)])
    return np.clip(painted.ravel(), 0, 1)


def rotate_bone(rig, name, angle):
    bone = rig.pose.bones[name]
    axis = bone.bone.matrix_local.to_quaternion().inverted() @ Vector((1, 0, 0))
    bone.rotation_quaternion = Quaternion(axis, angle)


def create_actions(rig):
    """Keep mesh weights and rest bones untouched; these are draft motions, not supplied Tripo clips."""
    rig.animation_data_create()
    actions = {}
    for name, duration in (("Idle", 30), ("Walk", 36), ("Run", 20), ("Attack", 18)):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        for frame in range(duration + 1):
            phase = frame / duration * math.tau
            for bone in rig.pose.bones:
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = Quaternion()
                bone.location = (0, 0, 0)
            if name in ("Run", "Walk"):
                charging = name == "Run"
                rotate_bone(rig, "Spine", .20 if charging else .025)
                rotate_bone(rig, "Chest", .10 if charging else 0)
                rotate_bone(rig, "Head", -.10 if charging else 0)
                for side, sign in (("Left", 1), ("Right", -1)):
                    swing = math.sin(phase) * sign
                    rotate_bone(rig, side + "_UpperLeg", swing * (.65 if charging else .24))
                    rotate_bone(rig, side + "_LowerLeg", max(0, -swing) * (.95 if charging else .36))
                    rotate_bone(rig, side + "_UpperArm", (-.55 if charging else 0) - swing * (.28 if charging else .13))
                    rotate_bone(rig, side + "_LowerArm", -.55 if charging else -.12)
                rig.pose.bones["Hips"].location.y = abs(math.sin(phase)) * (.026 if charging else .008)
            elif name == "Attack":
                # Contract, then wrench the torso open: a self-detonation, not an ordinary walking/punch loop.
                progress = frame / duration
                compress = math.sin(math.pi * min(1, progress / .65))
                burst = max(0, (progress - .55) / .45)
                rotate_bone(rig, "Spine", .16 + .26 * compress - .27 * burst)
                rotate_bone(rig, "Chest", .12 * compress - .18 * burst)
                rotate_bone(rig, "Head", .12 * compress - .25 * burst)
                for side in ("Left", "Right"):
                    rotate_bone(rig, side + "_UpperArm", -.30 - .5 * compress + .4 * burst)
                    rotate_bone(rig, side + "_LowerArm", -.4 - .45 * compress + .3 * burst)
                    rotate_bone(rig, side + "_UpperLeg", -.22 * compress)
                    rotate_bone(rig, side + "_LowerLeg", .4 * compress)
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
    paint_pustules(mesh)
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
    print("[TripoExploder] 67 original bones and original skin weights preserved; distinct idle/walk/charge/attack exported")


if __name__ == "__main__":
    main()
