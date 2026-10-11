"""Inspect licensed vegetation and append only reviewed collections to a verified source.

Run inspection with Blender --background SOURCE.blend --python THIS.py -- inspect.
Preparation/export are isolated background operations; append never opens or saves a file.
"""
import argparse
import array
from datetime import datetime
import hashlib
import json
from pathlib import Path
import shutil
import sys

import bpy
from mathutils import Vector

PROJECT_ROOT = Path(__file__).resolve().parents[1]
COLLECTION_PREFIX = "环境素材 · Poly Haven · "
STAGING_COLLECTION = "环境评审 · Poly Haven 植被"


def get_file_hash(file_path):
    """Return SHA-256 without loading large Blender sources in memory."""
    digest = hashlib.sha256()
    with Path(file_path).open("rb") as source_file:
        for chunk in iter(lambda: source_file.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_image_nodes(node_tree, visited=None):
    """Collect image channels including nested shader node groups."""
    visited = set() if visited is None else visited
    if node_tree is None or node_tree.as_pointer() in visited:
        return []
    visited.add(node_tree.as_pointer())
    image_list = []
    for node in node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image:
            image_path = Path(bpy.path.abspath(node.image.filepath)).resolve()
            image_list.append({"node": node.name, "image": node.image.name,
                               "path": str(image_path), "exists": image_path.is_file(),
                               "size": list(node.image.size), "colorspace": node.image.colorspace_settings.name,
                               "channels": node.image.channels,
                               "links": [f"{link.from_socket.name}->{link.to_node.name}:{link.to_socket.name}"
                                         for link in node_tree.links if link.from_node == node]})
        if node.type == "GROUP":
            image_list.extend(read_image_nodes(node.node_tree, visited))
    return image_list


def read_material_record(material):
    """Report source shader images and alpha modes without converting them."""
    return {"name": material.name,
            "surface_render_method": getattr(material, "surface_render_method", None),
            "blend_method": getattr(material, "blend_method", None),
            "use_backface_culling": material.use_backface_culling,
            "images": read_image_nodes(material.node_tree)}


def read_mesh_record(obj, depsgraph):
    """Count original/evaluated triangles and world bounds for one source object."""
    obj.data.calc_loop_triangles()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        mesh.calc_loop_triangles()
        points = [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
        bounds_min = [min(point[axis] for point in points) for axis in range(3)] if points else [0] * 3
        bounds_max = [max(point[axis] for point in points) for axis in range(3)] if points else [0] * 3
        return {"name": obj.name, "mesh": obj.data.name, "vertices": len(obj.data.vertices),
                "triangles": len(obj.data.loop_triangles), "evaluated_triangles": len(mesh.loop_triangles),
                "bounds_min": bounds_min, "bounds_max": bounds_max,
                "dimensions_m": [bounds_max[axis] - bounds_min[axis] for axis in range(3)],
                "uv_layers": list(obj.data.uv_layers.keys()), "parent": obj.parent.name if obj.parent else None,
                "matrix_world": [list(row) for row in obj.matrix_world],
                "hide_viewport": obj.hide_viewport, "hide_render": obj.hide_render,
                "collections": [collection.name for collection in obj.users_collection],
                "modifiers": [{"name": modifier.name, "type": modifier.type} for modifier in obj.modifiers],
                "materials": [material.name if material else None for material in obj.data.materials]}
    finally:
        evaluated.to_mesh_clear()


def inspect_source():
    """Return complete read-only source provenance and native mesh/LOD inventory."""
    source_path = Path(bpy.data.filepath).resolve()
    if not source_path.is_file():
        raise ValueError("Inspection requires an existing Blender source file")
    depsgraph = bpy.context.evaluated_depsgraph_get()
    return {"source_blend": str(source_path), "source_sha256": get_file_hash(source_path),
            "blender_version": bpy.app.version_string, "active_scene": bpy.context.scene.name,
            "unit_scale": bpy.context.scene.unit_settings.scale_length,
            "scenes": list(bpy.data.scenes.keys()),
            "collections": [{"name": collection.name, "children": list(collection.children.keys()),
                             "objects": list(collection.objects.keys()),
                             "hide_viewport": collection.hide_viewport,
                             "hide_render": collection.hide_render} for collection in bpy.data.collections],
            "meshes": [read_mesh_record(obj, depsgraph) for obj in bpy.data.objects if obj.type == "MESH"],
            "materials": [read_material_record(material) for material in bpy.data.materials]}


def save_report(report, report_path):
    """Write only a requested report, or print JSON for read-only diagnostics."""
    report_text = json.dumps(report, ensure_ascii=False, indent=2)
    if report_path:
        report_path = Path(report_path).resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(report_text + "\n", encoding="utf-8")
        print(f"VEGETATION_REPORT {report_path}", flush=True)
    else:
        print(report_text, flush=True)


def load_recipe(recipe_path):
    """Read an explicit selection recipe; no implicit source/LOD guesses."""
    recipe = json.loads(Path(recipe_path).read_text(encoding="utf-8"))
    if not recipe.get("assets"):
        raise ValueError(f"Empty vegetation recipe: {recipe_path}")
    for asset in recipe["assets"]:
        source_path = (PROJECT_ROOT / asset["source_blend"]).resolve()
        if not source_path.is_file():
            raise ValueError(f"Missing licensed source: {source_path}")
        if get_file_hash(source_path) != asset["source_sha256"]:
            raise ValueError(f"Source hash changed: {source_path}")
    return recipe


def append_source_object(asset, collection):
    """Append one named native source object and repair only source texture paths."""
    source_path = (PROJECT_ROOT / asset["source_blend"]).resolve()
    with bpy.data.libraries.load(str(source_path), link=False) as (available, loaded):
        if asset["source_object"] not in available.objects:
            raise ValueError(f"Missing source object {asset['source_object']} in {source_path}")
        loaded.objects = [asset["source_object"]]
    obj = loaded.objects[0]
    if obj.type != "MESH" or obj.parent or obj.modifiers:
        raise ValueError(f"Only independent native static meshes are supported: {obj.name}")
    collection.objects.link(obj)
    obj.name = asset["id"]
    obj.hide_viewport = False
    obj.hide_render = False
    for material in obj.data.materials:
        if material:
            repair_image_paths(material.node_tree, source_path.parent)
    return obj


def repair_image_paths(node_tree, source_directory, visited=None):
    """Resolve relative source textures without replacing material nodes or channels."""
    visited = set() if visited is None else visited
    if node_tree is None or node_tree.as_pointer() in visited:
        return
    visited.add(node_tree.as_pointer())
    for node in node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image:
            image_path = Path(bpy.path.abspath(node.image.filepath)).resolve()
            if not image_path.is_file():
                image_path = source_directory / "textures" / Path(node.image.filepath).name
            if not image_path.is_file():
                raise ValueError(f"Missing source material texture: {node.image.filepath}")
            node.image.filepath = str(image_path)
        if node.type == "GROUP":
            repair_image_paths(node.node_tree, source_directory, visited)


def prepare_asset(asset, parent_collection, index):
    """Create a native-derived review asset, preserving UV/materials and proportion."""
    collection = bpy.data.collections.new(COLLECTION_PREFIX + asset["id"])
    parent_collection.children.link(collection)
    obj = append_source_object(asset, collection)
    bpy.context.view_layer.update()
    source_record = read_mesh_record(obj, bpy.context.evaluated_depsgraph_get())
    obj.data = obj.data.copy()
    target_triangles = asset.get("target_triangles")
    if asset.get("material_decimation_ratios"):
        obj = decimate_material_parts(obj, asset["material_decimation_ratios"])
    elif target_triangles and source_record["triangles"] > target_triangles:
        modifier = obj.modifiers.new("Explicit CC0 RTS decimation", "DECIMATE")
        # Leave budget headroom for the protected native extrema.
        modifier.ratio = target_triangles * .85 / source_record["triangles"]
        modifier.use_collapse_triangulate = True
        # Retain the existing silhouette extrema; no newly authored geometry is added.
        bounds_group = obj.vertex_groups.new(name="Preserve native silhouette extrema")
        extreme_indices = [vertex.index for vertex in obj.data.vertices
                           if any(abs((obj.matrix_world @ vertex.co)[axis] - limit) < .015
                                  for axis in range(3)
                                  for limit in (source_record["bounds_min"][axis], source_record["bounds_max"][axis]))]
        bounds_group.add(extreme_indices, 1.0, "REPLACE")
        modifier.vertex_group = bounds_group.name
        modifier.invert_vertex_group = True
        modifier.vertex_group_factor = 1.0
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    # Pivot translation only: neither aspect ratio nor authored rotation is refitted.
    bpy.context.view_layer.update()
    record = read_mesh_record(obj, bpy.context.evaluated_depsgraph_get())
    # Disconnected native leaf cards impose a collapse floor; report actual count.
    if target_triangles and record["triangles"] > target_triangles * 1.2:
        raise ValueError(f"Decimation missed {asset['id']} triangle budget: {record['triangles']} > {target_triangles}")
    bounds_error = [abs(record["dimensions_m"][axis] / source_record["dimensions_m"][axis] - 1)
                    for axis in range(3) if source_record["dimensions_m"][axis] > 0]
    if max(bounds_error, default=0) > .03:
        raise ValueError(f"Decimation changed {asset['id']} bounds by more than 3%: {bounds_error}")
    pivot = Vector(((record["bounds_min"][0] + record["bounds_max"][0]) / 2,
                    (record["bounds_min"][1] + record["bounds_max"][1]) / 2,
                    record["bounds_min"][2]))
    obj.location -= pivot
    obj.location += Vector((index * 6.0, 0, 0))
    obj["vegetation_asset_id"] = asset["id"]
    obj["source_blend"] = asset["source_blend"]
    obj["source_object"] = asset["source_object"]
    obj["source_sha256"] = asset["source_sha256"]
    obj["license"] = "CC0-1.0"
    obj["export_gallery_translation"] = [index * 6.0, 0, 0]
    return {"id": asset["id"], "collection": collection.name, "object": obj.name,
            "source": asset, "source_triangles": source_record["triangles"],
            "triangles": record["triangles"], "dimensions_m": record["dimensions_m"],
            "bounds_relative_error": bounds_error,
            "uv_layers": record["uv_layers"], "pivot_translation": list(pivot),
            "gallery_translation": [index * 6.0, 0, 0],
            "materials": [read_material_record(material) for material in obj.data.materials if material]}


def decimate_material_parts(obj, material_ratios):
    """Decimate original material islands separately in their shared source coordinates."""
    original_name = obj.name
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    parts = list(bpy.context.selected_objects)
    for part in parts:
        material = next((material for material in part.data.materials if material), None)
        if material is None:
            raise ValueError(f"Tree source part has no material: {part.name}")
        matched = [ratio for suffix, ratio in material_ratios.items() if suffix in material.name]
        if len(matched) != 1:
            raise ValueError(f"Missing/ambiguous material decimation ratio: {material.name}")
        bpy.context.view_layer.objects.active = part
        modifier = part.modifiers.new("CC0 material-aware RTS decimation", "DECIMATE")
        modifier.ratio = matched[0]
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.join()
    obj.name = original_name
    return obj


def prepare_staging(recipe_path, staging_path):
    """Save a new isolated review file; never opens, overwrites or modifies a master."""
    staging_path = Path(staging_path).resolve()
    if not bpy.app.background or bpy.data.filepath or staging_path.exists():
        raise ValueError("Prepare requires factory-startup background and a NEW staging path")
    recipe = load_recipe(recipe_path)
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    parent_collection = bpy.data.collections.new(STAGING_COLLECTION)
    bpy.context.scene.collection.children.link(parent_collection)
    assets = [prepare_asset(asset, parent_collection, index) for index, asset in enumerate(recipe["assets"])]
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    staging_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(staging_path), compress=True)
    return {"status": "review_only_not_export_approved", "staging_blend": str(staging_path),
            "staging_sha256": get_file_hash(staging_path), "collection": parent_collection.name,
            "assets": assets, "proportion_policy": "translation-only pivot; native dimensions retained"}


def read_scene_identity():
    """Snapshot existing object transforms/data and scene identities for append validation."""
    return {"scenes": list(bpy.data.scenes.keys()),
            "objects": {obj.name: {"data": obj.data.as_pointer() if obj.data else None,
                                    "matrix": [list(row) for row in obj.matrix_world],
                                    "parent": obj.parent.name if obj.parent else None,
                                    "hide_viewport": obj.hide_viewport, "hide_render": obj.hide_render}
                        for obj in bpy.data.objects}}


def append_staging(staging_path, expected_source, origin):
    """Append a reviewed collection to the live source, with recovery copy and no save."""
    expected_source = Path(expected_source).resolve()
    if bpy.app.background or Path(bpy.data.filepath).resolve() != expected_source:
        raise ValueError("Append requires the live, explicitly verified source filepath")
    if STAGING_COLLECTION in bpy.data.collections:
        raise ValueError("Environment review collection already exists; refuse duplicate append")
    staging_path = Path(staging_path).resolve()
    if not staging_path.is_file():
        raise ValueError(f"Missing reviewed staging: {staging_path}")
    previous = read_scene_identity()
    from datetime import datetime
    snapshot_path = PROJECT_ROOT / "art/environment/snapshots" / ("before-append-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
    snapshot_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(snapshot_path), copy=True, compress=True)
    with bpy.data.libraries.load(str(staging_path), link=False) as (available, loaded):
        if STAGING_COLLECTION not in available.collections:
            raise ValueError(f"Missing review collection in {staging_path}")
        loaded.collections = [STAGING_COLLECTION]
    collection = loaded.collections[0]
    bpy.context.scene.collection.children.link(collection)
    for obj in collection.all_objects:
        obj.location += Vector(origin)
        obj["master_gallery_translation"] = list(origin)
    bpy.context.view_layer.update()
    current = read_scene_identity()
    if current["scenes"] != previous["scenes"]:
        raise AssertionError("Append unexpectedly changed existing scenes")
    for name, record in previous["objects"].items():
        if current["objects"].get(name) != record:
            raise AssertionError(f"Append changed existing source object: {name}")
    return {"status": "appended_unsaved_parent_must_review_and_save", "source_blend": str(expected_source),
            "scene": bpy.context.scene.name, "collection": collection.name,
            "recovery_snapshot": str(snapshot_path), "existing_objects_unchanged": len(previous["objects"]),
            "origin": origin, "objects": list(collection.all_objects.keys())}


def identify_image_channel(image_path):
    """Identify explicit official suffixes; never infer height from a color image."""
    for suffix, channel in (("_alpha_", "alpha"), ("_nor_gl_", "normal_gl"), ("_rough_", "roughness"),
                            ("_ao_", "ao"), ("_diff_", "base_color"), ("_mask_", "source_mask")):
        if suffix in image_path.name:
            return channel
    raise ValueError(f"Unrecognized source texture channel: {image_path}")


def copy_material_maps(material, source_id, output_directory):
    """Copy exact source PBR channels with hashes, preferring licensed PNG data maps."""
    channels = {}
    for image in read_image_nodes(material.node_tree):
        image_path = Path(image["path"])
        channel = identify_image_channel(image_path)
        if "_dry_diff_" in image_path.name:
            channel = "base_color_dry_not_default"
        png_path = image_path.with_suffix(".png")
        if png_path.is_file():
            image_path = png_path
        if not image_path.is_file():
            # Portable review files pack their Blender textures. Exact exported PNG
            # maps remain alongside FBX for repeat exports without raw-download paths.
            exported_map = output_directory / source_id / "Textures" / png_path.name
            if not exported_map.is_file():
                raise ValueError(f"Missing external export map for packed source image: {image_path}")
            image_path = exported_map
        destination = output_directory / source_id / "Textures" / image_path.name
        destination.parent.mkdir(parents=True, exist_ok=True)
        if image_path.resolve() != destination.resolve():
            shutil.copy2(image_path, destination)
        channels[channel] = {"path": str(destination.relative_to(PROJECT_ROOT)),
                             "sha256": get_file_hash(destination), "resolution": image["size"],
                             "colorspace": "sRGB" if channel.startswith("base_color") else "linear",
                             "component": "red" if channel in {"alpha", "roughness", "ao"} else "rgb"}
    return {"name": material.name, "channels": channels, "double_sided": not material.use_backface_culling,
            "alpha_mode": "cutout" if "alpha" in channels else "opaque", "alpha_cutoff": .5,
            "metallic": 0, "roughness_to_smoothness": "Unity converts once; smoothness=1-roughness"}


def export_candidate(output_directory, expected_source, is_candidate, asset_id=None):
    """Export exact prepared meshes, requiring saved approved source unless explicitly candidate."""
    source_path = Path(bpy.data.filepath).resolve()
    if not bpy.app.background or not source_path.is_file() or bpy.data.is_dirty:
        raise ValueError("Export requires an unmodified saved background source")
    if not is_candidate and (not expected_source or source_path != Path(expected_source).resolve()):
        raise ValueError("Final export requires the parent-approved --expected-source")
    parent_collection = bpy.data.collections.get(STAGING_COLLECTION)
    if not parent_collection:
        raise ValueError("Source has no prepared environment collection")
    output_directory = Path(output_directory).resolve()
    output_directory.relative_to(PROJECT_ROOT)
    records = []
    objects = [obj for obj in parent_collection.all_objects if obj.type == "MESH"]
    if asset_id:
        objects = [obj for obj in objects if obj.get("vegetation_asset_id") == asset_id]
        if len(objects) != 1:
            raise ValueError(f"Expected one explicitly selected export asset: {asset_id}")
    for obj in objects:
        asset_id = obj["vegetation_asset_id"]
        source_id = Path(obj["source_blend"]).parent.name
        destination = output_directory / source_id / (asset_id + ".fbx")
        destination.parent.mkdir(parents=True, exist_ok=True)
        materials = [copy_material_maps(material, source_id, output_directory) for material in obj.data.materials if material]
        original_matrix = obj.matrix_world.copy()
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        obj.location -= Vector(obj.get("export_gallery_translation", (0, 0, 0)))
        obj.location -= Vector(obj.get("master_gallery_translation", (0, 0, 0)))
        bpy.context.view_layer.update()
        mesh_record = read_mesh_record(obj, bpy.context.evaluated_depsgraph_get())
        try:
            bpy.ops.export_scene.fbx(filepath=str(destination), use_selection=True, object_types={"MESH"},
                                     use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False,
                                     axis_forward="-Z", axis_up="Y", global_scale=1.0,
                                     apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                                     bake_space_transform=True, path_mode="AUTO", embed_textures=False)
        finally:
            obj.matrix_world = original_matrix
        records.append({"id": asset_id, "model_path": str(destination.relative_to(PROJECT_ROOT)),
                        "model_sha256": get_file_hash(destination), "source_blend": str(source_path.relative_to(PROJECT_ROOT)),
                        "source_scene": bpy.context.scene.name, "source_collection": obj.users_collection[0].name,
                        "source_object": obj.name, "original_source": obj["source_blend"],
                        "original_object": obj["source_object"], "original_sha256": obj["source_sha256"],
                        "triangles": mesh_record["triangles"], "dimensions_blender_xyz_m": mesh_record["dimensions_m"],
                        "dimensions_unity_xyz_m": [mesh_record["dimensions_m"][0], mesh_record["dimensions_m"][2], mesh_record["dimensions_m"][1]],
                        "pivot_bounds_min": mesh_record["bounds_min"], "uv_layers": mesh_record["uv_layers"],
                        "material_slots": [material.name if material else None for material in obj.data.materials],
                        "materials": materials, "lod_index": 1 if asset_id == "TreeSmall_LOD1" else 0,
                        "lod_screen_relative_transition": .015 if asset_id == "TreeSmall_LOD1" else .18 if asset_id == "TreeSmall_LOD0" else None,
                        "placement_scale": 1, "unit_m": 1, "axis_forward": "-Z", "axis_up": "Y",
                        "axis_conversion": "FBX baked once; Unity importer globalScale=1; no per-axis mesh refit"})
    return {"schema_version": 1, "status": "candidate_not_approved" if is_candidate else "parent_approved_source_export",
            "source_blend": str(source_path.relative_to(PROJECT_ROOT)), "source_sha256": get_file_hash(source_path),
            "license": "CC0-1.0", "license_url": "https://polyhaven.com/license", "records": records}


def read_geometry_signatures():
    """Hash geometry, UV, slots and transforms for existing asset preservation checks."""
    signatures = {}
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        digest = hashlib.sha256()
        for collection, attribute, typecode, width in ((mesh.vertices, "co", "f", 3),
                                                     (mesh.loops, "vertex_index", "i", 1),
                                                     (mesh.polygons, "material_index", "i", 1)):
            values = array.array(typecode, [0]) * (len(collection) * width)
            collection.foreach_get(attribute, values)
            digest.update(values.tobytes())
        for layer in mesh.uv_layers:
            values = array.array("f", [0]) * (len(layer.data) * 2)
            layer.data.foreach_get("uv", values)
            digest.update(layer.name.encode())
            digest.update(values.tobytes())
        digest.update(json.dumps({"matrix": [list(row) for row in obj.matrix_world],
                                  "materials": [material.name for material in mesh.materials]}, sort_keys=True).encode())
        signatures[obj.name] = digest.hexdigest()
    return signatures


def extend_formal_source(recipe_path, asset_id, expected_source):
    """Append one untouched native asset to a hash-verified formal category with recovery snapshot."""
    source_path = Path(bpy.data.filepath).resolve()
    handoff_path = PROJECT_ROOT / "art/environment/vegetation_handoff_candidate.json"
    handoff = json.loads(handoff_path.read_text(encoding="utf-8"))
    if not bpy.app.background or bpy.data.is_dirty or source_path != Path(expected_source).resolve():
        raise ValueError("Formal extension requires an unmodified background copy of the approved category")
    if source_path != (PROJECT_ROOT / handoff["source_blend"]).resolve() or get_file_hash(source_path) != handoff["source_sha256"]:
        raise ValueError("Formal source changed since handoff; preserve user edits and refuse overwrite")
    assets = [asset for asset in load_recipe(recipe_path)["assets"] if asset["id"] == asset_id]
    if len(assets) != 1 or asset_id in bpy.data.objects:
        raise ValueError(f"Asset missing/duplicate/already appended: {asset_id}")
    asset = assets[0]
    if asset.get("target_triangles") or asset.get("material_decimation_ratios"):
        raise ValueError("Native rock extension forbids decimation or mesh reshaping")
    previous_identity = read_scene_identity()
    previous_signatures = read_geometry_signatures()
    snapshot = PROJECT_ROOT / "art/environment/snapshots" / ("before-native-rock-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
    snapshot.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path, snapshot)
    parent_collection = bpy.data.collections[STAGING_COLLECTION]
    index = int(max((obj.get("export_gallery_translation", (0, 0, 0))[0] for obj in parent_collection.all_objects), default=-6) / 6) + 1
    prepared_asset = prepare_asset(asset, parent_collection, index)
    bpy.context.view_layer.update()
    current_identity = read_scene_identity()
    if current_identity["scenes"] != previous_identity["scenes"]:
        raise AssertionError("Native extension changed existing scenes")
    for name, record in previous_identity["objects"].items():
        if current_identity["objects"].get(name) != record:
            raise AssertionError(f"Native extension changed existing object: {name}")
    if get_file_hash(source_path) != handoff["source_sha256"]:
        raise ValueError("Formal source externally changed during preparation; refuse overwrite")
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path), compress=True)
    bpy.ops.wm.open_mainfile(filepath=str(source_path))
    current_signatures = read_geometry_signatures()
    if any(current_signatures.get(name) != signature for name, signature in previous_signatures.items()):
        raise AssertionError("Existing geometry/UV/material/transform changed after category reload")
    images = [image for image in bpy.data.images if image.source == "FILE"]
    if not all(image.packed_file for image in images) or bpy.data.libraries:
        raise AssertionError("Formal source is not portable after extension")
    return {"status": "formal_category_source_test_only", "source_blend": str(source_path.relative_to(PROJECT_ROOT)),
            "source_sha256": get_file_hash(source_path), "packed_images": len(images),
            "recovery_snapshot": str(snapshot.relative_to(PROJECT_ROOT)),
            "previous_source_sha256": handoff["source_sha256"], "existing_objects_unchanged": len(previous_identity["objects"]),
            "geometry_signatures": current_signatures, "new_asset": prepared_asset,
            "source_reopened_and_verified": True}


def verify_exports(handoff_path):
    """Re-import FBX in an isolated process and verify geometry, pivot, slots and hashes."""
    if not bpy.app.background or bpy.data.filepath:
        raise ValueError("FBX roundtrip validation requires factory-startup background")
    handoff = json.loads(Path(handoff_path).read_text(encoding="utf-8"))
    results = []
    for record in handoff["records"]:
        model_path = PROJECT_ROOT / record["model_path"]
        if get_file_hash(model_path) != record["model_sha256"]:
            raise ValueError(f"Export hash mismatch: {model_path}")
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.ops.import_scene.fbx(filepath=str(model_path), use_custom_normals=True)
        bpy.context.view_layer.update()
        objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        if len(objects) != 1:
            raise ValueError(f"Expected one native mesh in {model_path}, found {len(objects)}")
        mesh_record = read_mesh_record(objects[0], bpy.context.evaluated_depsgraph_get())
        if mesh_record["triangles"] != record["triangles"] or not mesh_record["uv_layers"]:
            raise ValueError(f"FBX triangle/UV mismatch: {record['id']}")
        if abs(mesh_record["bounds_min"][2]) > .001:
            raise ValueError(f"FBX grounding pivot mismatch: {record['id']}")
        for actual, expected in zip(mesh_record["dimensions_m"], record["dimensions_blender_xyz_m"]):
            if abs(actual - expected) > .001:
                raise ValueError(f"FBX dimension mismatch: {record['id']}: {actual} != {expected}")
        if len(mesh_record["materials"]) != len(record["material_slots"]):
            raise ValueError(f"FBX material slot mismatch: {record['id']}")
        for material in record["materials"]:
            for channel in material["channels"].values():
                if get_file_hash(PROJECT_ROOT / channel["path"]) != channel["sha256"]:
                    raise ValueError(f"Texture hash mismatch: {channel['path']}")
        results.append({"id": record["id"], "triangles": mesh_record["triangles"],
                        "uv_layers": mesh_record["uv_layers"], "dimensions_m": mesh_record["dimensions_m"],
                        "bounds_min": mesh_record["bounds_min"], "material_slots": mesh_record["materials"],
                        "model_and_texture_hashes": "pass"})
    return {"status": "fbx_roundtrip_pass", "records": results}


def parse_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("inspect", "prepare", "append", "export", "verify-exports", "extend"))
    parser.add_argument("--report")
    parser.add_argument("--recipe")
    parser.add_argument("--staging")
    parser.add_argument("--expected-source")
    parser.add_argument("--origin", type=float, nargs=3)
    parser.add_argument("--output")
    parser.add_argument("--candidate", action="store_true")
    parser.add_argument("--handoff")
    parser.add_argument("--asset-id")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])


def run_pipeline():
    arguments = parse_arguments()
    if arguments.mode == "inspect":
        if not bpy.app.background:
            raise ValueError("Inspection must run in a background source process")
        report = inspect_source()
    elif arguments.mode == "prepare":
        if not arguments.recipe or not arguments.staging:
            raise ValueError("Prepare requires --recipe and --staging")
        report = prepare_staging(arguments.recipe, arguments.staging)
    elif arguments.mode == "append":
        if not arguments.staging or not arguments.expected_source or arguments.origin is None:
            raise ValueError("Append requires --staging, --expected-source and --origin x y z")
        report = append_staging(arguments.staging, arguments.expected_source, arguments.origin)
    elif arguments.mode == "export":
        if not arguments.output:
            raise ValueError("Export requires --output")
        report = export_candidate(arguments.output, arguments.expected_source, arguments.candidate, arguments.asset_id)
    elif arguments.mode == "verify-exports":
        if not arguments.handoff:
            raise ValueError("Verify-exports requires --handoff")
        report = verify_exports(arguments.handoff)
    else:
        if not arguments.recipe or not arguments.asset_id or not arguments.expected_source:
            raise ValueError("Extend requires --recipe, --asset-id and --expected-source")
        report = extend_formal_source(arguments.recipe, arguments.asset_id, arguments.expected_source)
    save_report(report, arguments.report)


if __name__ == "__main__":
    run_pipeline()
