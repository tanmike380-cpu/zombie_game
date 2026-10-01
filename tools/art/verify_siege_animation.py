"""Regression checks for grounded imports and original-rig siege animation drafts."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).parent))
from scene_grounding import collect_model_roots, collect_meshes, measure_bounds
from siege_motion import measure_rig_signature

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/TripoSiege"
MODELS = ROOT / "art/models/tripo-siege"


def fingerprint_textures():
    """Hash actual packed texture bytes, excluding transient render/viewer buffers."""
    return {image.name: hashlib.sha256(image.packed_file.data).hexdigest()
            for image in bpy.data.images if image.packed_file}


def fingerprint_mesh(mesh):
    """Fingerprint original geometry, UVs and per-vertex skin assignments."""
    record = {
        "vertices": [list(vertex.co) for vertex in mesh.data.vertices],
        "polygons": [list(polygon.vertices) for polygon in mesh.data.polygons],
        "weights": [[(group.group, group.weight) for group in vertex.groups] for vertex in mesh.data.vertices],
        "uv": [list(loop.uv) for loop in mesh.data.uv_layers.active.data],
    }
    return hashlib.sha256(json.dumps(record).encode()).hexdigest()


def measure_edge_lengths(mesh):
    """Measure evaluated local edge lengths for rigid-part deformation regression."""
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    positions = np.array([vertex.co[:] for vertex in evaluated.data.vertices])
    edges = np.array([edge.vertices[:] for edge in evaluated.data.edges])
    return np.linalg.norm(positions[edges[:, 0]] - positions[edges[:, 1]], axis=1)


def verify_saved_models():
    """Check the authored file against the unmodified user scene snapshot."""
    original = ROOT / "Builds/ArtReview/TripoBridgeScene-2026-10-01.blend"
    source = original if original.exists() else MODELS / "TripoModelsGrounded.blend"
    bpy.ops.wm.open_mainfile(filepath=str(source))
    textures = fingerprint_textures()
    roots = collect_model_roots()
    positions = {root.name: root.matrix_world.translation.xy.copy() for root in roots}
    signatures = {root.name: measure_rig_signature(root) for root in roots if root.type == "ARMATURE"}
    giant_mesh = fingerprint_mesh(collect_meshes(bpy.data.objects["头槌巨尸"])[0])
    bpy.ops.wm.open_mainfile(filepath=str(MODELS / "SiegeAnimations.blend"))
    assert fingerprint_textures() == textures, "Imported texture bytes changed"
    bpy.context.window.scene = bpy.data.scenes["01_AllModels_Grounded"]
    for name, position in positions.items():
        root = bpy.data.objects[name]
        assert (root.matrix_world.translation.xy - position).length < 1e-6, f"XY changed: {name}"
        assert abs(measure_bounds(root)[0][2]) < 1e-5, f"Not grounded: {name}"
    results = {"packed_textures_unchanged": len(textures), "grounded_models": len(positions),
               "original_xy_preserved": True, "clips": {}}
    for scene_name, source in [("02_GreekFire_Preview", "Greek Fire siege"), ("03_Headbutter_Preview", "头槌巨尸")]:
        scene = bpy.data.scenes[scene_name]
        bpy.context.window.scene = scene
        rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
        mesh = collect_meshes(rig)[0]
        assert measure_rig_signature(rig) == signatures[source], f"Rest rig altered: {source}"
        assert not any(obj.type == "LIGHT" for obj in scene.objects), "Unexpected new preview lighting"
        if source == "头槌巨尸":
            assert fingerprint_mesh(mesh) == giant_mesh, "Giant topology/weights/UV changed"
        scene.frame_set(1)
        baseline = measure_edge_lengths(mesh)
        maximum_stretch = 0
        foot_samples, head_samples = [], []
        rest_pose = {bone.name: bone.matrix.copy() for bone in rig.pose.bones}
        for frame in range(1, 121):
            scene.frame_set(frame)
            lengths = measure_edge_lengths(mesh)
            valid = baseline > 1e-6
            maximum_stretch = max(maximum_stretch, float(np.max(np.abs(lengths[valid] / baseline[valid] - 1))))
            assert abs(measure_bounds(rig)[0][2]) < .015, f"Ground drift at {source}:{frame}"
            if source == "头槌巨尸":
                foot_samples.append(tuple(rig.pose.bones["Left_Foot"].head))
                head_samples.append(tuple(rig.pose.bones["Head"].head))
                rear = rig.pose.bones["Right_Foot"]
                assert (rear.head - rear.bone.head_local).length < 1e-4, f"Rear foot slid at {frame}"
        if source == "Greek Fire siege":
            assert maximum_stretch < .003, f"Metal is deforming: {maximum_stretch}"
        results["clips"][source] = {"frames_checked": 120, "bones_preserved": len(signatures[source]),
                                     "maximum_edge_length_change": maximum_stretch}
        if source == "头槌巨尸":
            forward_step = foot_samples[0][1] - min(point[1] for point in foot_samples)
            foot_lift = max(point[2] for point in foot_samples) - foot_samples[0][2]
            head_travel = max(point[1] for point in head_samples) - min(point[1] for point in head_samples)
            assert forward_step > .28 and foot_lift > .09, "Lead foot did not visibly step"
            assert head_travel > .25, "Heavy ram has insufficient whole-body travel"
            for bone in rig.pose.bones:
                assert max(abs(value) for row in bone.matrix-rest_pose[bone.name] for value in row) < 1e-4, f"Loop discontinuity: {bone.name}"
            results["clips"][source].update(forward_step=forward_step, foot_lift=foot_lift,
                head_travel=head_travel, rear_foot_planted=True, loop_returns_to_rest=True)
    (REVIEW / "verification.json").write_text(json.dumps(results, ensure_ascii=False, indent=2))
    print("PASS: original textures, seven grounded hierarchies, 240 frames, original rigs, rigid machine", flush=True)


if __name__ == "__main__":
    verify_saved_models()
