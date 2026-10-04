"""Export approved master walls with shared registration; never refit separate pieces."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).parent))
from modular_stone_wall import clip_mesh
from coastal_exports import ROOT, OUTPUT, export_model

SOURCE_SCALE = 18.0
FRONT_COLLECTION = "城墙 · 15_Tower_Multiple"
KIT_COLLECTION = "城墙 · 00_ModularWall_Kit"
ASSEMBLY_PATH = ROOT / "art/models/modular-stone-wall/CoastalWallAssembly.blend"


def load_master_collections():
    """Require reviewed collections in the registered master, never an old fallback file."""
    registry = json.loads((ROOT / "art/blender_master.json").read_text())
    master_path = ROOT / registry["master_blend"]
    if not master_path.is_file():
        raise FileNotFoundError(f"Registered Blender master missing: {master_path}")
    with bpy.data.libraries.load(str(master_path), link=False) as (available, loaded):
        missing = {FRONT_COLLECTION, KIT_COLLECTION} - set(available.collections)
        if missing:
            raise ValueError(f"Master has not consolidated reviewed walls: {sorted(missing)}")
        loaded.collections = [FRONT_COLLECTION, KIT_COLLECTION]
    return master_path, loaded.collections


def copy_registered_mesh(source, identity):
    """Bake original local registration and uniform scale, not the gallery translation."""
    mesh = source.data.copy()
    mesh.name = identity + "_RegisteredMesh"
    mesh.transform(Matrix.Scale(SOURCE_SCALE, 4) @ source.matrix_basis)
    return mesh


def read_bounds(mesh):
    """Read real vertex bounds, never use bounds to deform authored pieces."""
    if not mesh.vertices:
        raise ValueError(f"Empty authored mesh: {mesh.name}")
    low = Vector(tuple(min(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3)))
    high = Vector(tuple(max(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3)))
    return low, high


def map_unity_point(point, origin, yaw):
    """Convert FBX (-x,z,-y) followed by the shared Unity Y rotation."""
    angle = math.radians(180 - yaw)
    cosine, sine = math.cos(angle), math.sin(angle)
    return Vector((origin[0] + cosine * point.x - sine * point.y,
                   point.z, origin[1] + sine * point.x + cosine * point.y))


def bind_export_texture(mesh, relative_texture, identity):
    """Use the preserved exported colour file instead of an expired Tripo temp-file path."""
    texture_path = (OUTPUT / identity / relative_texture).resolve()
    if not texture_path.is_file():
        raise FileNotFoundError(f"Preserved source colour texture missing: {texture_path}")
    image = bpy.data.images.load(str(texture_path), check_existing=True)
    for index, source in enumerate(list(mesh.materials)):
        if source is None or not source.use_nodes:
            continue
        material = source.copy()
        node = next((node for node in material.node_tree.nodes
                     if node.type == "TEX_IMAGE" and node.image), None)
        if node is not None:
            node.image = image
        mesh.materials[index] = material


def export_registered_piece(scene, mesh, identity, origin, yaw, source_name, tower=False, pier=False):
    """Export in common run space and hand off exact placement and world bounds."""
    obj = bpy.data.objects.new(identity, mesh)
    scene.collection.objects.link(obj)
    texture = ("../GateTower/tripo_image_6a319d87_0.png" if tower
               else "../Wall/tripo_image_2a6beea7_0.png")
    bind_export_texture(mesh, texture, identity)
    record = export_model(obj, identity, shared_texture=texture)
    record["master_source_object"] = source_name
    if tower:
        record["friendly_passage"] = False
        record["solid_defense_tower"] = True
    sideways = yaw in (90, 270)
    points = [map_unity_point(vertex.co, origin, yaw) for vertex in mesh.vertices]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    center, size = (low + high) * .5, high - low
    region = {"asset": identity, "label": "GATE TOWER" if tower else (
                  "FORTRESS END WALL" if pier else "FORTRESS WALL BODY"),
              "x": center.x, "z": center.z,
              "width": 18 if tower else (5 if sideways else size.x),
              "depth": 16 if tower else (size.z if sideways else 5),
              "height": max(2, size.y), "sideways": sideways, "pier": pier,
              "position": {"x": origin[0], "y": 0, "z": origin[1]},
              "yaw": yaw, "scale": 1,
              "expected_bounds_min": {"x": low.x, "y": low.y, "z": low.z},
              "expected_bounds_max": {"x": high.x, "y": high.y, "z": high.z}}
    if tower:
        region["friendly_passage"] = False
        region["solid_defense_tower"] = True
    obj.location = (origin[0], origin[1], 0)
    obj.rotation_euler.z = math.radians(180 - yaw)
    obj["unity_asset"] = identity
    obj["source_master_object"] = source_name
    obj["common_registration_preserved"] = True
    return record, region


def verify_front_intervals(scene):
    """Check real shaft contact planes, allowing wall ends beneath roof/foot decoration."""
    intervals = []
    for obj in scene.objects:
        if obj.name.startswith("WallRunFront"):
            low, high = read_bounds(obj.data)
            if "contact_start" in obj:
                low.x, high.x = obj["contact_start"], obj["contact_end"]
            intervals.append((low.x, high.x, obj.name))
    intervals.sort()
    assert abs(intervals[0][0]) < 1e-4 and abs(intervals[-1][1] - 108) < 1e-4
    for first, following in zip(intervals, intervals[1:]):
        if abs(first[1] - following[0]) > 1e-4:
            raise AssertionError(f"Approved front seam/overlap mismatch: {first} -> {following}")


def export_approved_front(scene, collection, records, regions):
    """Use the exact reviewed two-tower wall, preserving its approved relative scale."""
    sources = sorted((obj for obj in collection.objects if obj.type == "MESH" and
                      (obj.get("wall_module") or "tower_start_cell" in obj)), key=lambda obj: obj.name)
    towers = 0
    for index, source in enumerate(sources):
        tower = "tower_start_cell" in source
        identity = f"WallRunFrontTower_{towers:02d}" if tower else f"WallRunFront_{index:02d}"
        if tower:
            towers += 1
        mesh = copy_registered_mesh(source, identity)
        record, region = export_registered_piece(scene, mesh, identity, (-19, -55), 0,
                                                 source.name, tower, "Pier_" in source.name)
        if tower:
            half_width = source.get("wall_join_half_width", .5) * SOURCE_SCALE
            center = source.matrix_basis.translation.x * SOURCE_SCALE
            obj = scene.objects[identity]
            obj["contact_start"], obj["contact_end"] = center - half_width, center + half_width
            region["wall_contact_width"] = 2 * half_width
            record["rectangular_lower_body"] = bool(source.get("rectangular_lower_body"))
        records.append(record)
        regions.append(region)
    if towers != 2:
        raise AssertionError(f"Approved front requires two original towers, got {towers}")
    verify_front_intervals(scene)


def load_side_parts(collection):
    """Reuse original kit geometry, ignoring its exploded-view display translations."""
    parts = []
    for source_name in ("StartPier", "MiddleWall", "EndPier"):
        source = next((obj for obj in collection.objects if obj.type == "MESH" and
                       obj.name.endswith(" · " + source_name)), None)
        if source is None:
            raise ValueError(f"Missing original kit part in master: {source_name}")
        mesh = source.data.copy()
        mesh.transform(Matrix.Scale(SOURCE_SCALE, 4))
        parts.append(mesh)
    return parts


def build_side_sections(parts, length):
    """Repeat approved middle mesh between intact end piers, with no cross-section scaling."""
    left, body, right = parts
    left_width, right_width, pitch = (read_bounds(mesh)[1].x for mesh in (left, right, body))
    spans = [(0, left_width, left, True), (length - right_width, length, right, True)]
    cursor = left_width
    while cursor < length - right_width - 1e-5:
        end = min(cursor + pitch, length - right_width)
        spans.append((cursor, end, body, False))
        cursor = end
    for start, end, source, pier in sorted(spans, key=lambda span: span[0]):
        mesh = source.copy() if pier else clip_mesh(source, 0, end - start, "Registered side body")
        mesh.transform(Matrix.Translation((start, 0, 0)))
        yield mesh, pier


def export_sides(scene, collection, records, regions):
    """End side walls at front centerline, sealing corners within the front end piers."""
    parts = load_side_parts(collection)
    for side, origin, yaw in (("Left", (-124.5, -55), 270), ("Right", (-21.5, -124.5), 90)):
        for index, (mesh, pier) in enumerate(build_side_sections(parts, 69.5)):
            identity = f"WallRun{side}_{index:02d}"
            record, region = export_registered_piece(scene, mesh, identity, origin, yaw,
                                                     KIT_COLLECTION, pier=pier)
            records.append(record)
            regions.append(region)


def save_handoff(master_path, records, regions):
    """Write traceable derived outputs; do not save over the live master."""
    layout = {"schema_version": 2, "source_blend": str(master_path.relative_to(ROOT)),
              "source_collection": FRONT_COLLECTION, "uniform_source_scale": SOURCE_SCALE,
              "note": "Approved 15_Tower_Multiple, shared registration; no per-piece Unity fitting.",
              "regions": regions}
    (ROOT / "Assets/_Game/Resources/CoastalWallLayout.json").write_text(json.dumps(layout, indent=2))
    manifest_path = OUTPUT / "manifest.json"
    manifest = json.loads(manifest_path.read_text())
    for record in records:
        record["master_blend"] = str(master_path.relative_to(ROOT))
    manifest["models"] = [record for record in manifest["models"]
                          if not record["id"].startswith("WallRun")] + records
    manifest_path.write_text(json.dumps(manifest, indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(ASSEMBLY_PATH))


def main():
    """Build derived exports from the saved master, never resetting the live Blender app."""
    if not bpy.app.background:
        raise RuntimeError("Use background Blender; this exporter must not reset the live layout")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    master_path, (front, kit) = load_master_collections()
    scene = bpy.context.scene
    scene.name = "Coastal · Approved Master Wall Assembly"
    records, regions = [], []
    export_approved_front(scene, front, records, regions)
    export_sides(scene, kit, records, regions)
    save_handoff(master_path, records, regions)
    print(f"MASTER WALL EXPORT PASS: {len(regions)} pieces; front 108; 2 original towers", flush=True)


if __name__ == "__main__":
    main()
