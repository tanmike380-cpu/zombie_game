"""Import exactly two user-supplied rock packages into a new formal resource category.

Run in isolated Blender --background --disable-autoexec, never the live zombie GUI.
The user's source license is unverified; this pipeline does not transmit any files.
"""
import argparse
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path, PurePosixPath
import shutil
import sys
import zipfile

import bpy
from mathutils import Vector

PROJECT_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PROJECT_ROOT / "tools"))
from environment_vegetation import get_file_hash, read_geometry_signatures, read_mesh_record

RESOURCE_DIRECTORY = PROJECT_ROOT / "art/resources"
FORMAL_SOURCE = PROJECT_ROOT / "art/Blender主文件/ZombieGame_Resources.blend"
PARENT_COLLECTION = "资源素材 · 用户提供矿石"
PACKAGES = (
    {"id": "IronOreRock", "archive": "/Users/tankaixi/Downloads/iron+rock+3d+model.zip",
     "archive_sha256": "ddc7fb536024e7c482a67660e91240e12995bb244b56607a28b58f001a74e9ed",
     "fbx": "tripo_convert_3e9f0c53-ed90-4031-9cb9-a5b79f880e8a.fbx",
     "channels": {
         "base_color": "tripo_convert_3e9f0c53-ed90-4031-9cb9-a5b79f880e8a.fbm/tripo_rgb_98c3e0a5-5eca-4dc2-a25b-e7f5cf82cd2b.jpg",
         "normal": "tripo_convert_3e9f0c53-ed90-4031-9cb9-a5b79f880e8a.fbm/tripo_normal_9a147e8f-1d4f-4150-acf2-ee0f87bc54bc.jpg",
         "metallic": "tripo_convert_3e9f0c53-ed90-4031-9cb9-a5b79f880e8a.fbm/iron_rock_3d_model_metallic.JPEG",
         "roughness": "tripo_convert_3e9f0c53-ed90-4031-9cb9-a5b79f880e8a.fbm/iron_rock_3d_model_roughness.JPEG"},
     "gallery_position": [0, 0, 0]},
    {"id": "GoldOreRock", "archive": "/Users/tankaixi/Downloads/gold+rock+3d+model.zip",
     "archive_sha256": "28b2194c829088bc5f762b0b26be7c1493573e8cd4890a35c3441b42cc7ca3df",
     "fbx": "tripo_convert_04ea4f60-d19b-472b-8fbf-5821421f6ec8.fbx",
     "channels": {
         "base_color": "tripo_convert_04ea4f60-d19b-472b-8fbf-5821421f6ec8.fbm/tripo_rgb_398671df-111f-42a0-a3be-6dd3337fb028.jpg",
         "normal": "tripo_convert_04ea4f60-d19b-472b-8fbf-5821421f6ec8.fbm/tripo_normal_953a4708-b36f-4fec-a076-84a394c53d00.jpg",
         "metallic": "tripo_convert_04ea4f60-d19b-472b-8fbf-5821421f6ec8.fbm/gold_rock_3d_model_metallic.JPEG",
         "roughness": "tripo_convert_04ea4f60-d19b-472b-8fbf-5821421f6ec8.fbm/gold_rock_3d_model_roughness.JPEG"},
     "gallery_position": [6, 0, 0]},
)


def save_report(report, filename):
    """Save a structured resource manifest or validation artifact."""
    RESOURCE_DIRECTORY.mkdir(parents=True, exist_ok=True)
    (RESOURCE_DIRECTORY / filename).write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def extract_package(package):
    """Extract only five pinned FBX/PBR-image members; reject additional or unsafe entries."""
    archive_path = Path(package["archive"])
    if get_file_hash(archive_path) != package["archive_sha256"]:
        raise ValueError(f"User package changed: {archive_path}")
    directory = RESOURCE_DIRECTORY / "user-src" / package["id"]
    members = {}
    with zipfile.ZipFile(archive_path) as archive:
        expected_names = {package["fbx"], *package["channels"].values()}
        if len(archive.infolist()) != 5 or set(archive.namelist()) != expected_names:
            raise ValueError(f"Unexpected members in {archive_path}")
        for entry in archive.infolist():
            member_path = PurePosixPath(entry.filename)
            if member_path.is_absolute() or ".." in member_path.parts or entry.is_dir() or (entry.external_attr >> 16) & 0o170000 == 0o120000:
                raise ValueError(f"Unsafe zip entry: {entry.filename}")
            if entry.file_size > 20_000_000:
                raise ValueError(f"Oversized member: {entry.filename}")
            content = archive.read(entry)  # zipfile verifies CRC; no execution or arbitrary extractall.
            output_path = directory / entry.filename
            content_hash = hashlib.sha256(content).hexdigest()
            if output_path.exists() and get_file_hash(output_path) != content_hash:
                raise ValueError(f"Refusing to overwrite changed source: {output_path}")
            output_path.parent.mkdir(parents=True, exist_ok=True)
            output_path.write_bytes(content)
            members[entry.filename] = {"path": str(output_path.relative_to(PROJECT_ROOT)), "bytes": len(content), "sha256": content_hash}
    return {**package, "members": members, "license_status": "user_provided_unverified_not_CC0",
            "source_author": "not supplied", "usage": "local independent showcase only; no redistribution/upload authorization"}


def read_bounds(objects):
    """Read imported world bounds and triangle/UV/material statistics."""
    bpy.context.view_layer.update()
    pieces = [read_mesh_record(obj, bpy.context.evaluated_depsgraph_get()) for obj in objects]
    bounds_min = [min(piece["bounds_min"][axis] for piece in pieces) for axis in range(3)]
    bounds_max = [max(piece["bounds_max"][axis] for piece in pieces) for axis in range(3)]
    return {"pieces": pieces, "triangles": sum(piece["triangles"] for piece in pieces),
            "bounds_min": bounds_min, "bounds_max": bounds_max,
            "dimensions_blender_xyz_m": [bounds_max[axis] - bounds_min[axis] for axis in range(3)]}


def restore_pbr_channels(objects, source_record):
    """Bind four real supplied PBR images; preserve unverified normal convention explicitly."""
    material = bpy.data.materials.new(source_record["id"] + "_UserPBR")
    material.use_nodes = True
    material.use_backface_culling = False
    shader = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    if shader is None:
        shader = material.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        output = next((node for node in material.node_tree.nodes if node.type == "OUTPUT_MATERIAL"), None)
        if output is None:
            output = material.node_tree.nodes.new("ShaderNodeOutputMaterial")
        material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    for channel, member in source_record["channels"].items():
        image_path = PROJECT_ROOT / source_record["members"][member]["path"]
        image = bpy.data.images.load(str(image_path), check_existing=True)
        image.colorspace_settings.name = "sRGB" if channel == "base_color" else "Non-Color"
        node = material.node_tree.nodes.new("ShaderNodeTexImage")
        node.image = image
        node.label = channel
        if channel == "normal":
            normal = material.node_tree.nodes.new("ShaderNodeNormalMap")
            normal.inputs["Strength"].default_value = 1
            material.node_tree.links.new(node.outputs["Color"], normal.inputs["Color"])
            material.node_tree.links.new(normal.outputs["Normal"], shader.inputs["Normal"])
        else:
            socket_name = {"base_color": "Base Color", "metallic": "Metallic", "roughness": "Roughness"}[channel]
            material.node_tree.links.new(node.outputs["Color"], shader.inputs[socket_name])
    material["normal_convention"] = "unverified; no green flip; as-encoded Blender preview; Unity must visually verify tangent Y direction"
    for obj in objects:
        slot_count = max(1, len(obj.data.materials))
        obj.data.materials.clear()
        for _ in range(slot_count):
            obj.data.materials.append(material)
    return material


def import_resource(source_record, parent_collection):
    """Import native FBX and ground by root translation without modifying its mesh or UV."""
    asset_id = source_record["id"]
    collection = bpy.data.collections.new("资源素材 · " + asset_id)
    parent_collection.children.link(collection)
    existing_objects = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(PROJECT_ROOT / source_record["members"][source_record["fbx"]]["path"]), use_custom_normals=True)
    imported = [obj for obj in bpy.data.objects if obj not in existing_objects]
    if not imported or any(obj.type != "MESH" or obj.parent or obj.modifiers for obj in imported):
        raise ValueError(f"Unexpected non-static or parented source model: {asset_id}")
    native_bounds = read_bounds(imported)
    if any(not piece["uv_layers"] for piece in native_bounds["pieces"]):
        raise ValueError(f"User resource missing UV: {asset_id}")
    dimensions = native_bounds["dimensions_blender_xyz_m"]
    if min(dimensions) <= 0 or max(dimensions) > 5:
        raise ValueError(f"Source unit calibration needs review, refusing invented scale: {asset_id}: {dimensions}")
    pivot = Vector(((native_bounds["bounds_min"][0] + native_bounds["bounds_max"][0]) / 2,
                    (native_bounds["bounds_min"][1] + native_bounds["bounds_max"][1]) / 2, native_bounds["bounds_min"][2]))
    root = bpy.data.objects.new(asset_id + "_Root", None)
    root.empty_display_type = "PLAIN_AXES"
    collection.objects.link(root)
    root.location = source_record["gallery_position"]
    bpy.context.view_layer.update()
    for index, obj in enumerate(imported):
        original_name = obj.name
        obj.name = asset_id if len(imported) == 1 else f"{asset_id}_Mesh{index + 1}"
        for old_collection in list(obj.users_collection):
            old_collection.objects.unlink(obj)
        collection.objects.link(obj)
        world_matrix = obj.matrix_world.copy()
        world_matrix.translation += Vector(source_record["gallery_position"]) - pivot
        obj.parent = root
        obj.matrix_world = world_matrix
        obj["user_original_object"] = original_name
        obj["user_source_archive_sha256"] = source_record["archive_sha256"]
    restore_pbr_channels(imported, source_record)
    grounded_bounds = read_bounds(imported)
    if any(abs(actual - expected) > .00001 for actual, expected in zip(grounded_bounds["dimensions_blender_xyz_m"], dimensions)):
        raise ValueError(f"Source proportions changed: {asset_id}")
    return {"id": asset_id, "source_scene": bpy.context.scene.name, "source_collection": collection.name,
            "source_root": root.name, "source_objects": [obj.name for obj in imported], "gallery_position": source_record["gallery_position"],
            "native_import": native_bounds, "source_pivot_translation_m": list(-pivot),
            "unit_calibration_uniform_scale": 1, "unit_note": "Blender native FBX unit conversion only; no manual rescale, axis deformation or vertex edit",
            "geometry_policy": "Original native mesh/UV/custom normals preserved. Parent/root translation only. Bound supplied PBR images; no new geometry.",
            **grounded_bounds}


def prepare_resources():
    """Create a new formal category only when the target does not already exist."""
    if not bpy.app.background or bpy.data.filepath or FORMAL_SOURCE.exists():
        raise ValueError("Requires independent factory startup and nonexistent Resources category file")
    sources = [extract_package(package) for package in PACKAGES]
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    parent = bpy.data.collections.new(PARENT_COLLECTION)
    bpy.context.scene.collection.children.link(parent)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    records = [import_resource(source, parent) for source in sources]
    signatures = read_geometry_signatures()
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(FORMAL_SOURCE), compress=True)
    bpy.ops.wm.open_mainfile(filepath=str(FORMAL_SOURCE))
    if signatures != read_geometry_signatures() or bpy.data.libraries or any(image.source == "FILE" and not image.packed_file for image in bpy.data.images):
        raise ValueError("Resource category reopen/portable validation failed")
    source_hash = get_file_hash(FORMAL_SOURCE)
    save_report({"schema_version": 2, "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)), "source_sha256": source_hash,
                 "sources": sources, "license_status": "user_provided_unverified_not_CC0", "uploads": "none",
                 "normal_convention": "unverified; original pixels retained; Unity tangent-Y visual check required"}, "source_manifest.json")
    save_report({"status": "formal_new_category_reopen_pass_pending_parent_preview", "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                 "source_sha256": source_hash, "source_bytes": FORMAL_SOURCE.stat().st_size, "packed_images": sum(bool(image.packed_file) for image in bpy.data.images),
                 "geometry_uv_material_transform_signatures": signatures, "records": records}, "source_validation.json")


def snapshot_old_resources(expected_hash):
    """Make recoverable inactive copies before replacing the user's exact old resource category."""
    if get_file_hash(FORMAL_SOURCE) != expected_hash or set(bpy.data.objects.keys()) != {"OreRock", "OreRock_Root", "GoldOreRock", "GoldOreRock_Root"}:
        raise ValueError("Old category changed or has new user objects; refusing replacement")
    recovery_parent = RESOURCE_DIRECTORY / "user-src/recovery"
    for existing in sorted(recovery_parent.glob("before-pbr-replacement-*")):
        saved_source = existing / FORMAL_SOURCE.name
        if saved_source.is_file() and get_file_hash(saved_source) == expected_hash:
            return existing  # A prior unsaved preparation attempt already protected the exact old source.
    directory = RESOURCE_DIRECTORY / "user-src/recovery" / f"before-pbr-replacement-{datetime.now():%Y%m%d-%H%M%S}"
    directory.mkdir(parents=True, exist_ok=False)
    shutil.copy2(FORMAL_SOURCE, directory / FORMAL_SOURCE.name)
    for filename in ("source_manifest.json", "source_validation.json", "native_validation.json", "export_validation.json", "handoff.json", "README.md"):
        old_file = RESOURCE_DIRECTORY / filename
        if old_file.exists():
            shutil.copy2(old_file, directory / filename)
    preview_directory = RESOURCE_DIRECTORY / "previews"
    if preview_directory.exists():
        shutil.move(str(preview_directory), str(directory / "old-previews"))
    for asset_id in ("OreRock", "GoldOreRock"):
        old_raw = RESOURCE_DIRECTORY / "user-src" / asset_id
        if old_raw.exists():
            shutil.move(str(old_raw), str(directory / asset_id))
    return directory


def replace_resources(expected_hash):
    """Replace only the two authorized old models, removing old geometry/material/images from active .blend."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != FORMAL_SOURCE:
        raise ValueError("Replacement requires fresh isolated formal Resources process")
    recovery = snapshot_old_resources(expected_hash)
    sources = [extract_package(package) for package in PACKAGES]
    removed = {"objects": list(bpy.data.objects.keys()), "meshes": list(bpy.data.meshes.keys()),
               "materials": list(bpy.data.materials.keys()), "images": list(bpy.data.images.keys())}
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection.name == PARENT_COLLECTION or collection.name.startswith("资源素材 · "):
            bpy.data.collections.remove(collection, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        bpy.data.images.remove(image)
    parent = bpy.data.collections.new(PARENT_COLLECTION)
    bpy.context.scene.collection.children.link(parent)
    records = [import_resource(source, parent) for source in sources]
    signatures = read_geometry_signatures()
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.file.pack_all()
    if get_file_hash(FORMAL_SOURCE) != expected_hash:
        raise ValueError("Formal source changed concurrently; refusing save")
    bpy.ops.wm.save_as_mainfile(filepath=str(FORMAL_SOURCE), compress=True)
    bpy.ops.wm.open_mainfile(filepath=str(FORMAL_SOURCE))
    expected_objects = {"IronOreRock", "IronOreRock_Root", "GoldOreRock", "GoldOreRock_Root"}
    if set(bpy.data.objects.keys()) != expected_objects or signatures != read_geometry_signatures():
        raise ValueError("New category reopen/object/geometry verification failed")
    if len(bpy.data.images) != 8 or any(not image.packed_file for image in bpy.data.images) or bpy.data.libraries:
        raise ValueError("New category must have exactly eight portable PBR images")
    if any("UserBaseColor" in material.name for material in bpy.data.materials) or "资源素材 · OreRock" in bpy.data.collections:
        raise ValueError("Old resource art remains active")
    source_hash = get_file_hash(FORMAL_SOURCE)
    save_report({"schema_version": 2, "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)), "source_sha256": source_hash,
                 "sources": sources, "license_status": "user_provided_unverified_not_CC0", "uploads": "none",
                 "normal_convention": "unverified; original pixels retained; Unity tangent-Y visual check required"}, "source_manifest.json")
    save_report({"status": "formal_replacement_reopen_pass_pending_parent_preview", "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                 "source_sha256": source_hash, "source_bytes": FORMAL_SOURCE.stat().st_size, "packed_images": len(bpy.data.images),
                 "predecessor_sha256": expected_hash, "recovery_snapshot_directory": str(recovery.relative_to(PROJECT_ROOT)),
                 "removed_old_active_datablocks": removed, "active_objects": sorted(expected_objects),
                 "active_images": list(bpy.data.images.keys()), "geometry_uv_material_transform_signatures": signatures,
                 "records": records}, "source_validation.json")


def export_pbr_material(material, texture_directory):
    """Export exact packed PBR files and declare normal convention uncertainty rather than fake GL."""
    channels = {}
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.label not in ("base_color", "normal", "metallic", "roughness"):
            continue
        image = node.image
        if not image.packed_file:
            raise ValueError(f"Unpacked PBR image: {image.name}")
        texture_path = texture_directory / Path(image.filepath).name
        texture_path.write_bytes(bytes(image.packed_file.data))
        channels[node.label] = {"path": str(texture_path.relative_to(PROJECT_ROOT)), "sha256": get_file_hash(texture_path),
                                "resolution": list(image.size), "colorspace": "sRGB" if node.label == "base_color" else "linear",
                                "component": "red" if node.label in ("metallic", "roughness") else "rgb"}
    if set(channels) != {"base_color", "normal", "metallic", "roughness"}:
        raise ValueError(f"Missing supplied PBR channel: {material.name}")
    return {"name": material.name, "alpha_mode": "opaque", "double_sided": True, "channels": channels,
            "metallic": 1, "metallic_multiplier": 1, "normal_strength": 1,
            "normal_convention": "unverified", "normal_preview_assumption": "as_encoded_no_green_flip; NOT asserted OpenGL",
            "normal_visual_check_required": True, "roughness_to_smoothness": "Convert once; Standard metallic R uses source metallic, smoothness A = 1 - source roughness",
            "missing_channels": ["height", "AO"], "license_status": "user_provided_unverified_not_CC0"}


def export_resources(expected_hash):
    """Export exact model assemblies and packed JPG bytes without saving temporary translations."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != FORMAL_SOURCE or get_file_hash(FORMAL_SOURCE) != expected_hash:
        raise ValueError("Export requires hash-matched formal Resources file")
    validation = json.loads((RESOURCE_DIRECTORY / "source_validation.json").read_text(encoding="utf-8"))
    records = []
    for source_record in validation["records"]:
        asset_id = source_record["id"]
        collection = bpy.data.collections[source_record["source_collection"]]
        meshes = [obj for obj in collection.objects if obj.type == "MESH"]
        root = bpy.data.objects[source_record["source_root"]]
        output_directory = PROJECT_ROOT / "Assets/_Tests/EnvironmentShowcase/Art/Resources" / asset_id
        texture_directory = output_directory / "Textures"
        texture_directory.mkdir(parents=True, exist_ok=True)
        material = meshes[0].data.materials[0]
        material_record = export_pbr_material(material, texture_directory)
        model_path = output_directory / f"{asset_id}.fbx"
        gallery_position = root.location.copy()
        try:
            root.location = (0, 0, 0)
            bpy.context.view_layer.update()
            bpy.ops.object.select_all(action="DESELECT")
            root.select_set(True)
            for obj in meshes:
                obj.select_set(True)
            bpy.context.view_layer.objects.active = root
            bpy.ops.export_scene.fbx(filepath=str(model_path), use_selection=True, object_types={"MESH", "EMPTY"},
                                     axis_forward="-Z", axis_up="Y", global_scale=1, apply_unit_scale=True,
                                     apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, use_mesh_modifiers=False,
                                     use_triangles=True, bake_anim=False, add_leaf_bones=False, path_mode="STRIP", embed_textures=False)
            pivot_bounds = read_bounds(meshes)
        finally:
            root.location = gallery_position
            bpy.context.view_layer.update()
        dimensions = source_record["dimensions_blender_xyz_m"]
        records.append({**source_record, "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                        "model_path": str(model_path.relative_to(PROJECT_ROOT)), "model_sha256": get_file_hash(model_path),
                        "dimensions_unity_xyz_m": [dimensions[0], dimensions[2], dimensions[1]], "pivot_bounds": pivot_bounds,
                        "unit_m": 1, "placement_scale": 1, "axis_forward": "-Z", "axis_up": "Y", "unity_import_scale": 1,
                        "placement_note": "Whole assembly uniform scale only; no mesh refit. About six decorative instances are a Unity fixture, not mining/income gameplay.",
                        "material_slots": [material.name], "materials": [material_record]})
    save_report({"schema_version": 2, "status": "source_export_complete_pending_parent_preview", "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                 "source_sha256": expected_hash, "source_scene": bpy.context.scene.name, "source_collection": PARENT_COLLECTION,
                 "source_manifest": "art/resources/source_manifest.json", "records": records}, "handoff.json")


def validate_exports():
    """Round-trip both FBX files and verify dimensions, triangles, UV, slots and color bytes."""
    if not bpy.app.background or bpy.data.filepath:
        raise ValueError("Round-trip requires independent factory startup")
    handoff = json.loads((RESOURCE_DIRECTORY / "handoff.json").read_text(encoding="utf-8"))
    results = []
    for expected in handoff["records"]:
        model_path = PROJECT_ROOT / expected["model_path"]
        if get_file_hash(model_path) != expected["model_sha256"]:
            raise ValueError(f"Export hash changed: {model_path}")
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.ops.import_scene.fbx(filepath=str(model_path), use_custom_normals=True)
        actual = read_bounds([obj for obj in bpy.data.objects if obj.type == "MESH"])
        if actual["triangles"] != expected["triangles"] or abs(actual["bounds_min"][2]) > .001:
            raise ValueError(f"Roundtrip triangles/grounding failed: {expected['id']}")
        if any(abs((actual["bounds_min"][axis] + actual["bounds_max"][axis]) / 2) > .001 for axis in (0, 1)):
            raise ValueError(f"Gallery offset leaked into export pivot: {expected['id']}")
        if any(abs(actual_value - source_value) > .001 for actual_value, source_value in zip(actual["dimensions_blender_xyz_m"], expected["dimensions_blender_xyz_m"])):
            raise ValueError(f"Roundtrip proportions failed: {expected['id']}")
        for piece, original in zip(actual["pieces"], expected["pieces"]):
            if not piece["uv_layers"] or piece["uv_layers"] != original["uv_layers"] or piece["materials"] != original["materials"]:
                raise ValueError(f"Roundtrip UV/material slots failed: {expected['id']}")
        for texture in expected["materials"][0]["channels"].values():
            if get_file_hash(PROJECT_ROOT / texture["path"]) != texture["sha256"]:
                raise ValueError(f"PBR bytes changed: {expected['id']}: {texture['path']}")
        results.append({"id": expected["id"], "status": "pass", **actual})
    save_report({"status": "fbx_roundtrip_pass", "source_blend": handoff["source_blend"], "source_sha256": handoff["source_sha256"], "records": results}, "export_validation.json")


def read_native_mesh_signature(obj):
    """Hash native mesh coordinates, topology, material indices, UV and rounded custom normals."""
    mesh = obj.data
    record = {"vertices": [list(vertex.co) for vertex in mesh.vertices],
              "loops": [loop.vertex_index for loop in mesh.loops],
              "polygons": [[polygon.loop_start, polygon.loop_total, polygon.material_index] for polygon in mesh.polygons],
              "uv_layers": {layer.name: [list(corner.uv) for corner in layer.data] for layer in mesh.uv_layers},
              "corner_normals_6dp": [[round(value, 6) for value in normal.vector] for normal in mesh.corner_normals]}
    return hashlib.sha256(json.dumps(record, sort_keys=True).encode()).hexdigest()


def validate_native_sources():
    """Compare fresh source FBX mesh/UV data against the saved category, without saving changes."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != FORMAL_SOURCE:
        raise ValueError("Native validation requires saved formal Resources category")
    signatures = {package["id"]: read_native_mesh_signature(bpy.data.objects[package["id"]]) for package in PACKAGES}
    source_hash = get_file_hash(FORMAL_SOURCE)
    results = []
    for package in PACKAGES:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        source_path = RESOURCE_DIRECTORY / "user-src" / package["id"] / package["fbx"]
        bpy.ops.import_scene.fbx(filepath=str(source_path), use_custom_normals=True)
        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
        if len(meshes) != 1 or read_native_mesh_signature(meshes[0]) != signatures[package["id"]]:
            raise ValueError(f"Native geometry/UV/custom-normal signature differs: {package['id']}")
        results.append({"id": package["id"], "status": "pass", "native_mesh_uv_normal_signature": signatures[package["id"]]})
    save_report({"status": "native_source_fidelity_pass", "source_sha256": source_hash, "records": results}, "native_validation.json")


def render_resources():
    """Render temporary neutral previews without saving any lights or camera into the category."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != FORMAL_SOURCE:
        raise ValueError("Preview requires the new saved Resources category")
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 750
    scene.render.resolution_percentage = 100
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    if background is None:
        background = scene.world.node_tree.nodes.new("ShaderNodeBackground")
        output = scene.world.node_tree.nodes.new("ShaderNodeOutputWorld")
        scene.world.node_tree.links.new(background.outputs[0], output.inputs[0])
    background.inputs[0].default_value = (.72, .74, .77, 1)
    background.inputs[1].default_value = .6
    sun = bpy.data.lights.new("Preview-only sun", "SUN")
    sun.energy = 2
    sun.angle = math.radians(12)
    sun_object = bpy.data.objects.new("Preview-only sun", sun)
    scene.collection.objects.link(sun_object)
    sun_object.rotation_euler = (.5, -.6, -.5)
    camera = bpy.data.cameras.new("Preview-only camera")
    camera.type = "ORTHO"
    camera_object = bpy.data.objects.new("Preview-only camera", camera)
    scene.collection.objects.link(camera_object)
    scene.camera = camera_object
    for package in PACKAGES:
        for other in PACKAGES:
            for obj in bpy.data.collections["资源素材 · " + other["id"]].objects:
                obj.hide_render = other["id"] != package["id"]
        objects = [obj for obj in bpy.data.collections["资源素材 · " + package["id"]].objects if obj.type == "MESH"]
        record = read_bounds(objects)
        center = Vector([(lower + upper) / 2 for lower, upper in zip(record["bounds_min"], record["bounds_max"])])
        size = max(record["dimensions_blender_xyz_m"])
        camera.ortho_scale = size * 1.6
        camera_object.location = center + Vector((1.2, -1.7, 1.1)) * size
        camera_object.rotation_euler = (center - camera_object.location).to_track_quat("-Z", "Y").to_euler()
        output_directory = RESOURCE_DIRECTORY / "previews"
        output_directory.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(output_directory / f"{package['id']}.png")
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)


def parse_arguments():
    """Select one explicit resource pipeline stage."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("prepare", "replace", "export", "verify", "render", "verify-native"))
    parser.add_argument("--expected-sha256")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1:])


if __name__ == "__main__":
    arguments = parse_arguments()
    if arguments.mode == "prepare":
        prepare_resources()
    elif arguments.mode == "replace":
        replace_resources(arguments.expected_sha256)
    elif arguments.mode == "export":
        export_resources(arguments.expected_sha256)
    elif arguments.mode == "render":
        render_resources()
    elif arguments.mode == "verify-native":
        validate_native_sources()
    else:
        validate_exports()
