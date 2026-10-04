"""Append reviewed wall states to the user's live layout without replacing original art."""
import json
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "art/models/modular-stone-wall/ModularStoneWall.blend"
DESTINATION = ROOT / "art/Blender主文件/ZombieGame.blend"
REPORT = ROOT / "Builds/ArtReview/ModularStoneWall/master-gallery-report.json"
GALLERY_NAME = "城墙评审 · 全部状态 00—16"


def read_object_record(obj):
    """Capture identity and transforms of pre-existing user objects."""
    return {"name": obj.name, "type": obj.type,
            "matrix": [list(row) for row in obj.matrix_world],
            "data": obj.data.name if obj.data else None,
            "hide_render": obj.hide_render, "hide_viewport": obj.hide_viewport}


def find_gallery_origin(scene):
    """Place the gallery beyond existing visible models, not beyond remote lights."""
    points = [obj.matrix_world @ Vector(corner) for obj in scene.objects
              if obj.type == "MESH" and not obj.hide_get() and not obj.hide_viewport
              for corner in obj.bound_box]
    if not points:
        raise ValueError("Live master has no visible user meshes; refuse guessing another scene")
    return Vector((max(point.x for point in points) + 3,
                   min(point.y for point in points), 0))


def is_gallery_object(obj):
    """Keep authored models and small review markers, excluding cameras/floors/lights."""
    return obj.type in {"MESH", "CURVE", "FONT", "EMPTY"} and not (
        obj.name.startswith("Preview floor") or obj.name.startswith("Original"))


def append_case(scene, gallery, source_scene, index, origin):
    """Copy an accepted case with a common translated root and unchanged local geometry."""
    collection = bpy.data.collections.new(f"城墙 · {source_scene.name}")
    gallery.children.link(collection)
    root = bpy.data.objects.new(f"布局锚点 · {source_scene.name}", None)
    collection.objects.link(root)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = .15
    root.location = origin + Vector(((index % 3) * 8, (index // 3) * 2.6, 0))
    root["source_scene"] = source_scene.name
    root["source_blend"] = str(SOURCE.relative_to(ROOT))
    root["review_only"] = "Rejected" in source_scene.name or "NotEnough" in source_scene.name
    copied = []
    for obj in source_scene.objects:
        if not is_gallery_object(obj):
            continue
        if obj.parent is not None:
            raise ValueError(f"Reviewed wall object has unexpected parent: {obj.name}")
        clone = obj.copy()
        clone.name = f"{source_scene.name} · {obj.name}"
        collection.objects.link(clone)
        clone.parent = root
        clone.matrix_parent_inverse = Matrix.Identity(4)
        # These reviewed meshes have no parent. Basis is authored TRS and avoids stale
        # matrix_world caches from source scenes that are not the active view layer.
        clone.matrix_basis = obj.matrix_basis.copy()
        clone.hide_viewport = False
        clone.hide_set(False)
        copied.append(clone)
    return {"source_scene": source_scene.name, "collection": collection.name,
            "root": root.name, "translation": list(root.location),
            "objects": [obj.name for obj in copied], "review_only": root["review_only"]}


def append_original_reference(gallery, source_scene, origin):
    """Keep the original wall visible beside the kit without changing the original user wall."""
    source = next(obj for obj in source_scene.objects if obj.name.startswith("Original stone wall"))
    collection = bpy.data.collections.new("城墙 · 原始完整模型参考")
    gallery.children.link(collection)
    clone = source.copy()
    collection.objects.link(clone)
    clone.name = "原始城墙 · 未拆分参考"
    clone.parent = None
    clone.matrix_world = Matrix.Translation(origin + Vector((0, -2.3, 0)))
    clone.hide_viewport = False
    clone.hide_render = False
    clone.hide_set(False)
    return clone.name


def consolidate_gallery():
    """Snapshot the live master, append all review states, and save the approved master path."""
    registered = json.loads((ROOT / "art/blender_master.json").read_text())
    current = Path(bpy.data.filepath).resolve()
    expected = (ROOT / registered["master_blend"]).resolve()
    if current != expected:
        raise ValueError(f"Live Blender file differs from registry: {current} != {expected}")
    if GALLERY_NAME in bpy.data.collections:
        raise ValueError("Gallery already exists; refuse duplicate import")
    if DESTINATION.exists() and current != DESTINATION.resolve():
        raise ValueError("Destination exists but is not the live master; refuse overwrite")
    scene = bpy.context.scene
    previous = {obj.name: read_object_record(obj) for obj in scene.objects}
    original_scene_names = list(bpy.data.scenes.keys())
    origin = find_gallery_origin(scene)
    snapshot = ROOT / "Builds/ArtReview/ModularStoneWall" / (
        "master-before-gallery-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
    snapshot.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(snapshot), copy=True, compress=True)
    with bpy.data.libraries.load(str(SOURCE), link=False) as (available, loaded):
        loaded.scenes = sorted(available.scenes)
    source_scenes = list(loaded.scenes)
    if len(source_scenes) != 17:
        raise ValueError(f"Expected 17 approved source scenes, found {len(source_scenes)}")
    gallery = bpy.data.collections.new(GALLERY_NAME)
    scene.collection.children.link(gallery)
    cases = [append_case(scene, gallery, source_scene, index, origin)
             for index, source_scene in enumerate(source_scenes)]
    original_name = append_original_reference(gallery, source_scenes[0], origin)
    for source_scene in source_scenes:
        bpy.data.scenes.remove(source_scene)
    bpy.context.view_layer.update()
    for name, record in previous.items():
        if read_object_record(scene.objects[name]) != record:
            raise AssertionError(f"Original user object changed: {name}")
    assert list(bpy.data.scenes.keys()) == original_scene_names
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(DESTINATION), compress=True)
    report = {"master_blend": str(DESTINATION.relative_to(ROOT)), "scene": scene.name,
              "previous_master": str(current), "recovery_snapshot": str(snapshot),
              "original_objects_unchanged": len(previous), "gallery": gallery.name,
              "original_reference": original_name, "cases": cases,
              "lights_and_camera_unchanged": True}
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print(f"WALL GALLERY PASS: {len(cases)} cases; {len(previous)} original objects unchanged; {DESTINATION}")


def append_playable_assembly():
    """Append the exact exported battle assembly as a scaled gallery exhibit in the master."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != DESTINATION.resolve():
        raise ValueError("Assembly consolidation requires a background copy of the formal master")
    name = "城防实装 · 连续墙＋双炮塔"
    if name in bpy.data.collections:
        raise ValueError("Playable assembly already exists; refuse duplicate import")
    scene = bpy.context.scene
    previous = {obj.name: read_object_record(obj) for obj in scene.objects}
    snapshot = REPORT.parent / ("master-before-playable-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(snapshot), copy=True, compress=True)
    source_path = ROOT / "art/models/modular-stone-wall/CoastalWallAssembly.blend"
    with bpy.data.libraries.load(str(source_path), link=False) as (available, loaded):
        loaded.objects = [item for item in available.objects if item.startswith("WallRun")]
    objects = [obj for obj in loaded.objects if obj.get("unity_asset")]
    if len(objects) != 27:
        raise ValueError(f"Expected 27 registered exported objects, got {len(objects)}")
    collection = bpy.data.collections.new(name)
    scene.collection.children.link(collection)
    root = bpy.data.objects.new("实装装配 · 展示锚点（整体1比18）", None)
    collection.objects.link(root)
    gallery_report = json.loads(REPORT.read_text())
    origin = Vector(gallery_report["cases"][0]["translation"]) + Vector((16, -6.5, 0))
    root.matrix_world = Matrix.Translation(origin) @ Matrix.Scale(1 / 18, 4) @ Matrix.Translation((127, 124.5, 0))
    root["display_scale_only"] = 1 / 18
    root["source_assembly"] = str(source_path.relative_to(ROOT))
    root["unity_handoff"] = "Assets/_Game/Resources/CoastalWallLayout.json"
    for obj in objects:
        collection.objects.link(obj)
        matrix = obj.matrix_basis.copy()
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_basis = matrix
    bpy.context.view_layer.update()
    for object_name, record in previous.items():
        if read_object_record(scene.objects[object_name]) != record:
            raise AssertionError(f"Original master object changed: {object_name}")
    bpy.ops.wm.save_as_mainfile(filepath=str(DESTINATION), compress=True)
    gallery_report["playable_assembly"] = {"collection": name, "pieces": len(objects),
                                           "display_root": root.name,
                                           "preexisting_objects_unchanged": len(previous)}
    REPORT.write_text(json.dumps(gallery_report, ensure_ascii=False, indent=2))
    print(f"PLAYABLE ASSEMBLY PASS: {len(objects)} pieces; {len(previous)} existing objects unchanged")


def merge_latest_user_save():
    """Use the newly saved complete user scene as base, then append our gallery and assembly."""
    expected = ROOT / "art/Blender主文件/ZombieGame_Master.blend/未命名.blend"
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != expected.resolve():
        raise ValueError("Latest-save merge only accepts the exact user-saved predecessor in background")
    if GALLERY_NAME in bpy.data.collections:
        raise ValueError("Latest user source already has a gallery; refuse duplicate import")
    scene = bpy.context.scene
    previous = {obj.name: read_object_record(obj) for obj in scene.objects}
    action_names = sorted(bpy.data.actions.keys())
    snapshot = REPORT.parent / ("formal-before-latest-merge-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
    shutil.copy2(DESTINATION, snapshot)
    with bpy.data.libraries.load(str(snapshot), link=False) as (available, loaded):
        if GALLERY_NAME not in available.collections:
            raise ValueError("Formal predecessor lacks verified wall gallery")
        loaded.collections = [GALLERY_NAME]
    gallery = loaded.collections[0]
    scene.collection.children.link(gallery)
    origin = find_gallery_origin_from_objects(scene, previous)
    report = json.loads(REPORT.read_text())
    old_origin = Vector(report["cases"][0]["translation"])
    shift = Vector((max(0, origin.x - old_origin.x), 0, 0))
    for obj in gallery.all_objects:
        if obj.parent is None:
            obj.location += shift
    for case in report["cases"]:
        case["translation"][0] += shift.x
    bpy.context.view_layer.update()
    for object_name, record in previous.items():
        if read_object_record(scene.objects[object_name]) != record:
            raise AssertionError(f"Latest user object changed: {object_name}")
    if sorted(bpy.data.actions.keys()) != action_names:
        raise AssertionError("Unexpected action change while importing static wall gallery")
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(DESTINATION), compress=True)
    report["latest_user_source"] = str(expected.relative_to(ROOT))
    report["latest_user_objects_unchanged"] = len(previous)
    report["formal_before_merge_backup"] = str(snapshot)
    report["user_actions"] = action_names
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2))
    append_playable_assembly()


def find_gallery_origin_from_objects(scene, records):
    """Find free gallery space from user models only, excluding already appended exhibits."""
    points = [scene.objects[name].matrix_world @ Vector(corner) for name in records
              if scene.objects[name].type == "MESH" and not scene.objects[name].hide_get()
              for corner in scene.objects[name].bound_box]
    return Vector((max(point.x for point in points) + 3, min(point.y for point in points), 0))


if __name__ == "__main__":
    if "--merge-latest" in sys.argv:
        merge_latest_user_save()
    elif "--append-playable" in sys.argv:
        append_playable_assembly()
    else:
        consolidate_gallery()
