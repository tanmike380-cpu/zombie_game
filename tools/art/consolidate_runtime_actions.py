"""Audit existing runtime FBX rigs and consolidate their animations without re-rigging.

Default is read-only. --candidate saves an inspectable copy under Builds, never the
master. Promoting that copy requires a fresh live-editor/unsaved-work check.
"""
import hashlib
import json
import math
import shutil
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame.blend"
REVIEW = ROOT / "Builds/ArtReview/RuntimeActionConsolidation"
GROUP = "动画参考 · Unity现用资产"
COASTAL = ROOT / "Assets/_Game/Art/CoastalImports"
SOURCES = {
    "Archer": (COASTAL / "Archer/Archer.fbx", "BZT archer Elite"),
    "Soldier": (COASTAL / "Soldier/Soldier.fbx", "medieval soldier 3d model"),
    "Walker": (COASTAL / "Walker/Walker.fbx", "zombie 3d model"),
    "ExploderLatest": (COASTAL / "ExploderLatest/ExploderLatest.fbx", "Explode zombie"),
    "Hound": (COASTAL / "Hound/Hound.fbx", "Hunger Dog"),
    "Headbutter": (COASTAL / "Headbutter/Headbutter.fbx", "Hammer zombie"),
    "CageBoss": (COASTAL / "CageBoss/CageBoss.fbx", "wooden cage zombie"),
    "GreekFire": (COASTAL / "GreekFire/GreekFire.fbx", "Greek Fire siege"),
    "Crossbow": (ROOT / "Assets/_Game/Art/TripoCrossbow/ByzantineCrossbow.fbx", "BZT crossbow Elite"),
    "Exploder": (ROOT / "Assets/_Game/Art/TripoExploder/TripoExploder.fbx", "Explode zombie"),
}


def get_signature(obj):
    """Protect originals including geometry, UVs, skeleton rest transforms and placement."""
    payload = [obj.type, obj.data.name if obj.data else None, [list(row) for row in obj.matrix_world]]
    if obj.type == "MESH":
        payload.extend([[list(vertex.co) for vertex in obj.data.vertices],
                        [list(face.vertices) for face in obj.data.polygons],
                        [[list(loop.uv) for loop in layer.data] for layer in obj.data.uv_layers]])
    if obj.type == "ARMATURE":
        payload.append([(bone.name, bone.parent.name if bone.parent else None,
                         [list(row) for row in bone.matrix_local]) for bone in obj.data.bones])
    return hashlib.sha256(json.dumps(payload).encode()).hexdigest()


def compare_rest_rigs(original, imported):
    """Name equality alone is insufficient: compare rest axes and hierarchy as well."""
    first, second = original.data.bones, imported.data.bones
    names_equal = set(first.keys()) == set(second.keys())
    if not names_equal:
        return {"compatible": False, "reason": "bone_names_or_count_differ",
                "original_bones": len(first), "imported_bones": len(second)}
    maximum = max(abs(a - b) for name in first.keys()
                  for row_a, row_b in zip(first[name].matrix_local, second[name].matrix_local)
                  for a, b in zip(row_a, row_b))
    hierarchy_equal = all((first[name].parent.name if first[name].parent else None)
                          == (second[name].parent.name if second[name].parent else None)
                          for name in first.keys())
    return {"compatible": maximum < .00001 and hierarchy_equal,
            "maximum_rest_matrix_error": maximum, "hierarchy_equal": hierarchy_equal}


def import_reference(identity, path, source_name, group, position):
    """Append the untouched exported rig/mesh plus its own clips, not a forced retarget."""
    original = bpy.data.objects[source_name]
    old_objects, old_actions = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    objects = list(set(bpy.data.objects) - old_objects)
    actions = list(set(bpy.data.actions) - old_actions)
    rig = next(obj for obj in objects if obj.type == "ARMATURE")
    comparison = compare_rest_rigs(original, rig)
    collection = bpy.data.collections.new("动作参考 · " + identity)
    group.children.link(collection)
    anchor = bpy.data.objects.new(identity + " · 展示定位", None)
    collection.objects.link(anchor)
    anchor.location = position
    object_names = {obj: obj.name for obj in objects}
    action_names = {action: action.name for action in actions}
    for obj in objects:
        for owner in list(obj.users_collection):
            owner.objects.unlink(obj)
        collection.objects.link(obj)
        if obj.parent is None:
            obj.parent = anchor
        obj["runtime_source_fbx"] = str(path.relative_to(ROOT))
        obj["master_original"] = source_name
        obj.name = identity + " · " + obj.name
    for action in actions:
        action.use_fake_user = True
        action["runtime_source_fbx"] = str(path.relative_to(ROOT))
        action["archived_not_for_runtime"] = "Archived_" in action.name
        action.name = identity + "::" + action.name
    clips = []
    for obj in objects:
        matching = [action for action in actions
                    if action_names[action].split("|")[0] == object_names[obj]]
        if matching:
            preferred = next((action for action in matching if action_names[action].endswith("|Idle")), matching[0])
            obj.animation_data_create().action = preferred
            obj["available_actions"] = json.dumps([action.name for action in matching])
        if obj == rig:
            clips = [{"action": action.name, "frames": list(action.frame_range),
                      "archived": bool(action.get("archived_not_for_runtime"))} for action in matching]
    bpy.context.scene.frame_set(1)
    if not clips:
        raise AssertionError("No rig actions mapped for " + identity)
    evaluate_clip_samples(rig, objects, clips)
    return {"identity": identity, "source": str(path.relative_to(ROOT)), "source_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "collection": collection.name, "rig": rig.name, "comparison": comparison,
            "clips": clips, "all_actions": [action.name for action in actions],
            "placement": list(position), "objects": len(objects)}


def evaluate_clip_samples(rig, objects, clips):
    """Check each existing clip evaluates on its own imported rig with finite mesh vertices."""
    preferred = rig.animation_data.action
    for clip in clips:
        rig.animation_data.action = bpy.data.actions[clip["action"]]
        for frame in (clip["frames"][0], sum(clip["frames"]) / 2, clip["frames"][1]):
            bpy.context.scene.frame_set(int(frame))
            graph = bpy.context.evaluated_depsgraph_get()
            for obj in objects:
                if obj.type != "MESH":
                    continue
                evaluated = obj.evaluated_get(graph)
                mesh = evaluated.to_mesh()
                if not all(math.isfinite(coordinate) for vertex in mesh.vertices for coordinate in vertex.co):
                    raise AssertionError("Invalid evaluated vertex in " + clip["action"])
                evaluated.to_mesh_clear()
        clip["finite_evaluation_samples"] = 3
    rig.animation_data.action = preferred
    bpy.context.scene.frame_set(1)


def main():
    """Build a separate candidate after verifying every existing scene object is unchanged."""
    import sys
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER.resolve():
        raise ValueError("Run only on a background read of the registered formal master")
    if bpy.data.collections.get(GROUP):
        raise ValueError("Runtime action references already present; refusing duplicates")
    original_hash = hashlib.sha256(MASTER.read_bytes()).hexdigest()
    original_mtime = MASTER.stat().st_mtime_ns
    originals = {obj: get_signature(obj) for obj in bpy.context.scene.objects}
    old_fps = bpy.context.scene.render.fps
    group = bpy.data.collections.new(GROUP)
    bpy.context.scene.collection.children.link(group)
    points = [obj.matrix_world @ Vector(corner) for obj in originals if obj.type == "MESH" for corner in obj.bound_box]
    right = max(point.x for point in points) + 4
    records = [import_reference(identity, path, source, group, (right + index % 5 * 4, index // 5 * 4, 0))
               for index, (identity, (path, source)) in enumerate(SOURCES.items())]
    bpy.context.scene.render.fps = old_fps
    bpy.context.view_layer.update()
    for obj, signature in originals.items():
        if get_signature(obj) != signature:
            raise AssertionError("Unrelated source object changed: " + obj.name)
    if MASTER.stat().st_mtime_ns != original_mtime or hashlib.sha256(MASTER.read_bytes()).hexdigest() != original_hash:
        raise RuntimeError("Master changed during audit; candidate would be stale")
    report = {"master_sha256_before": original_hash, "existing_objects_unchanged": len(originals), "records": records}
    REVIEW.mkdir(parents=True, exist_ok=True)
    (REVIEW / "audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    if "--candidate" in sys.argv:
        bpy.ops.wm.save_as_mainfile(filepath=str(REVIEW / "ZombieGame_WithRuntimeActions.blend"), compress=True)
    if "--save-master" in sys.argv:
        backup = REVIEW / ("master-before-actions-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
        shutil.copy2(MASTER, backup)
        bpy.ops.wm.save_as_mainfile(filepath=str(MASTER), compress=True)
        report["backup"] = str(backup.relative_to(ROOT))
        report["master_sha256_after"] = hashlib.sha256(MASTER.read_bytes()).hexdigest()
        (REVIEW / "audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print("ACTION CONSOLIDATION AUDIT", json.dumps(report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
