"""Author licensed neutral landmarks in isolation, then merge only approved collections.

Never opens or saves the live zombie document. Run with Blender --disable-autoexec.
"""
import argparse
from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
import shutil
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from environment_vegetation import get_file_hash, read_geometry_signatures, read_mesh_record, read_scene_identity

PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIRECTORY = PROJECT_ROOT / "art/environment/landmarks-src"
FORMAL_SOURCE = PROJECT_ROOT / "art/Blender主文件/ZombieGame_Environment.blend"
STAGING_SOURCE = SOURCE_DIRECTORY / "Landmarks_Review_v7.blend"
PARENT_COLLECTION = "环境地标 · 中立荒废建筑"
COLLECTION_PREFIX = "环境地标 · "
ASSET_IDS = ("EuropeanStoneHouse", "NorthAfricanRuins", "MiddleEasternCourtyard")
GALLERY_POSITIONS = ((48, 12, 0), (62, 12, 0), (76, 12, 0))
RUINS_SOURCE = SOURCE_DIRECTORY / "fantasy_ruins_pack_for_blender.blend"
FARMHOUSE_SOURCE = SOURCE_DIRECTORY / "ruined_farmhouse/ruined_farmhouse.blend"
EXPECTED_RAW_HASHES = {
    RUINS_SOURCE: "1f712b434c09e61c85ffb7f5f3753ccfa239a2dfe62a0ebdfe7e5103d36862e1",
    FARMHOUSE_SOURCE: "c8b96e2bd9d2526d1982b18e6b6985191bd416b3108a9422f8a002847dd29cc6",
}


def write_report(report, path):
    """Persist reproducible structured validation evidence."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def append_native_mesh(source_path, object_name, collection, name):
    """Append a copied native mesh, leaving source geometry and UV untouched."""
    if get_file_hash(source_path) != EXPECTED_RAW_HASHES[source_path]:
        raise ValueError(f"Licensed source changed: {source_path}")
    with bpy.data.libraries.load(str(source_path), link=False) as (available, loaded):
        if object_name not in available.objects:
            raise ValueError(f"Missing native object {object_name} in {source_path}")
        loaded.objects = [object_name]
    obj = loaded.objects[0]
    if obj.type != "MESH" or obj.parent:
        raise ValueError(f"Unsupported source mesh: {object_name}")
    collection.objects.link(obj)
    obj.data = obj.data.copy()
    obj.name = name
    obj.hide_render = False
    obj.hide_viewport = False
    obj["landmark_original_source"] = str(source_path.relative_to(PROJECT_ROOT))
    obj["landmark_original_object"] = object_name
    obj["landmark_original_sha256"] = get_file_hash(source_path)
    # Native gallery translations do not belong to the exported assembly.
    obj.matrix_world.translation = Vector((0, 0, 0))
    for modifier in list(obj.modifiers):
        if modifier.type != "NODES" or object_name != "ruined_farmhouse":
            raise ValueError(f"Unexpected native modifier on {object_name}: {modifier.name}")
        # Blender's automatic-smooth compatibility node is not geometry authoring.
        obj.modifiers.remove(modifier)
    return obj


def normalize_mesh(obj, scale=1, position=(0, 0, 0), rotation=0):
    """Apply uniform source proportions and a bottom-centered assembly pivot."""
    bpy.context.view_layer.update()
    record = read_mesh_record(obj, bpy.context.evaluated_depsgraph_get())
    center = Vector(((record["bounds_min"][0] + record["bounds_max"][0]) / 2,
                     (record["bounds_min"][1] + record["bounds_max"][1]) / 2,
                     record["bounds_min"][2]))
    source_matrix = obj.matrix_world.copy()
    transform = Matrix.Translation(-center) @ source_matrix
    obj.data.transform(Matrix.Scale(scale, 4) @ transform)
    obj.data.update()
    obj.matrix_world = Matrix.Translation(Vector(position)) @ Matrix.Rotation(rotation, 4, "Z")
    bpy.context.view_layer.update()
    obj["native_uniform_scale"] = scale
    return obj


def create_material(name, channels, roughness=.92, tint=(1, 1, 1, 1)):
    """Wire actual image channels; constants are explicit when source maps are absent."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.use_backface_culling = False
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Specular IOR Level"].default_value = .12
    shader.inputs["Metallic"].default_value = 0
    material["landmark_base_color_tint"] = list(tint)
    material["landmark_missing_roughness_constant"] = roughness
    for channel, image_path in channels.items():
        node = nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(str(image_path), check_existing=True)
        node.label = channel
        node.image.colorspace_settings.name = "sRGB" if channel == "base_color" else "Non-Color"
        if channel == "base_color":
            if tint != (1, 1, 1, 1):
                multiply = nodes.new("ShaderNodeMixRGB")
                multiply.blend_type = "MULTIPLY"
                multiply.inputs[0].default_value = 1
                multiply.inputs[2].default_value = tint
                links.new(node.outputs["Color"], multiply.inputs[1])
                links.new(multiply.outputs[0], shader.inputs["Base Color"])
            else:
                links.new(node.outputs["Color"], shader.inputs["Base Color"])
        elif channel == "normal_gl":
            normal = nodes.new("ShaderNodeNormalMap")
            normal.inputs["Strength"].default_value = .45
            links.new(node.outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], shader.inputs["Normal"])
        elif channel == "roughness":
            links.new(node.outputs["Color"], shader.inputs["Roughness"])
    return material


def create_collection(asset_id, parent_collection):
    """Make one clearly named independent building collection."""
    collection = bpy.data.collections.new(COLLECTION_PREFIX + asset_id)
    parent_collection.children.link(collection)
    return collection


def create_european_house(parent_collection):
    """Preserve bobjh's intact ruined farmhouse mesh and original baked atlas UV."""
    collection = create_collection(ASSET_IDS[0], parent_collection)
    obj = append_native_mesh(FARMHOUSE_SOURCE, "ruined_farmhouse", collection, ASSET_IDS[0])
    normalize_mesh(obj)
    material = create_material("EuropeanStoneHouse_AuthoredAtlas", {
        "base_color": SOURCE_DIRECTORY / "ruined_farmhouse/ruined_farmhouse.png",
        "normal_gl": SOURCE_DIRECTORY / "ruined_farmhouse/ruined_farmhouseNormal.png",
    })
    obj.data.materials.clear()
    obj.data.materials.append(material)
    obj["landmark_changes"] = "Pivot translation only. Restored Blender-Internal atlas to Principled; native mesh and UV retained."
    return collection


def create_north_african_ruins(parent_collection):
    """Assemble an abandoned older stone gateway using Ron Kapaun's authored ruins."""
    collection = create_collection(ASSET_IDS[1], parent_collection)
    pieces = (("Object.002", "NorthAfricanRuins_Arch", .55, (0, -1.9, 0), 0),
              ("Object", "NorthAfricanRuins_Corner", .55, (0, 1.7, 0), 0),
              ("Object.003", "NorthAfricanRuins_FallenPillar", .55, (3.0, 1.8, 0), 0))
    for original, name, scale, position, rotation in pieces:
        obj = append_native_mesh(RUINS_SOURCE, original, collection, name)
        normalize_mesh(obj, scale, position, rotation)
        obj["landmark_changes"] = "Native mesh/UV/materials retained; uniform .55 assembly scale and rigid placement."
    return collection


def create_metric_uv(obj):
    """Add wall-scale UV for the new clay design, retaining native UV as provenance."""
    uv_layer = preserve_native_uv(obj)
    for uv_layer in obj.data.uv_layers:
        uv_layer.active_render = False
    uv_layer = obj.data.uv_layers["LandmarkUV_2m"]
    for polygon in obj.data.polygons:
        dominant_axis = max(range(3), key=lambda axis: abs(polygon.normal[axis]))
        axes = (1, 2) if dominant_axis == 0 else ((0, 2) if dominant_axis == 1 else (0, 1))
        for loop_index in polygon.loop_indices:
            vertex = obj.data.vertices[obj.data.loops[loop_index].vertex_index].co
            uv_layer.data[loop_index].uv = (vertex[axes[0]] / 2, vertex[axes[1]] / 2)
    uv_layer.active_render = True
    obj.data.uv_layers.active = uv_layer


def preserve_native_uv(obj):
    """Keep native UV in a provenance layer and put renderer/export UV in channel zero."""
    original = obj.data.uv_layers[0]
    source_name = original.name
    backup = obj.data.uv_layers.new(name="NativeSourceUV_" + source_name)
    for native_loop, saved_loop in zip(original.data, backup.data):
        saved_loop.uv = native_loop.uv
    original.name = "LandmarkUV_2m"
    original.active_render = True
    obj.data.uv_layers.active = original
    return original


def extract_native_timbers(collection):
    """Keep authored collapsed roof timber faces, not generated replacement props."""
    obj = append_native_mesh(RUINS_SOURCE, "CathedralRuinOne300", collection,
                             "MiddleEasternCourtyard_CollapsedTimbers")
    native_mesh = bmesh.new()
    native_mesh.from_mesh(obj.data)
    timber_index = next(index for index, material in enumerate(obj.data.materials)
                        if material.name.startswith("WoodDebris17"))
    bmesh.ops.delete(native_mesh, geom=[face for face in native_mesh.faces
                                      if face.material_index != timber_index], context="FACES")
    bmesh.ops.delete(native_mesh, geom=[vertex for vertex in native_mesh.verts
                                      if not vertex.link_faces], context="VERTS")
    native_mesh.to_mesh(obj.data)
    native_mesh.free()
    timber_material = obj.data.materials[timber_index]
    obj.data.materials.clear()
    obj.data.materials.append(timber_material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
    preserve_native_uv(obj)
    normalize_mesh(obj, .30, (0, 0, .04), math.radians(15))
    obj["landmark_changes"] = "Only original WoodDebris17 faces selected, source UV retained; uniform .30, rigid placement."
    return obj


def cut_wall_opening(obj, position, dimensions):
    """Cut a deep recessed window through new-design native wall; cutter is never exported."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    cutter = bpy.context.object
    cutter.name = "Authoring-only window cutter"
    cutter.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    modifier = obj.modifiers.new("Authored recessed window", "BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.use_self = True
    modifier.object = cutter
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def create_middle_eastern_courtyard(parent_collection):
    """Design a roofless clay courtyard from licensed broken-wall meshes and native timber."""
    collection = create_collection(ASSET_IDS[2], parent_collection)
    clay_channels = {channel: SOURCE_DIRECTORY / f"clay_plaster/clay_plaster_{suffix}_1k.png"
                     for channel, suffix in (("base_color", "diff"), ("normal_gl", "nor_gl"),
                                             ("roughness", "rough"))}
    clay = create_material("MiddleEasternCourtyard_ClayPlaster", clay_channels)
    soot = create_material("MiddleEasternCourtyard_WeatheredClay", clay_channels,
                           tint=(.48, .39, .30, 1))
    pieces = (("Object.004", "MiddleEasternCourtyard_RearWall", .58, (0, 2.3, 0), 0),
              ("Object.004", "MiddleEasternCourtyard_WestWall", .42, (-3.4, -.15, 0), math.pi / 2),
              ("Object.004", "MiddleEasternCourtyard_EastWall", .42, (3.4, -.15, 0), -math.pi / 2),
              ("Object", "MiddleEasternCourtyard_WestRoom", .35, (-2.95, -1.5, 0), 0),
              ("Object", "MiddleEasternCourtyard_EastRoom", .35, (2.95, -1.5, 0), math.pi),
              ("Object.004", "MiddleEasternCourtyard_Alcove", .24, (-1.75, .9, 0), 0))
    for index, (original, name, scale, position, rotation) in enumerate(pieces):
        obj = append_native_mesh(RUINS_SOURCE, original, collection, name)
        normalize_mesh(obj, scale, position, rotation)
        if index == 0:
            cut_wall_opening(obj, (-1.7, 2.3, 1.55), (.65, 1.6, .85))
            cut_wall_opening(obj, (1.7, 2.3, 1.55), (.65, 1.6, .85))
        obj.data.materials.clear()
        obj.data.materials.append(clay)
        obj.data.materials.append(soot)
        for polygon in obj.data.polygons:
            polygon.material_index = 1 if polygon.center.z < .34 else 0
        create_metric_uv(obj)
        bevel = obj.modifiers.new("New-design worn clay edge finish", "BEVEL")
        bevel.width = .025
        bevel.segments = 2
        bevel.affect = "EDGES"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=bevel.name)
        obj["landmark_changes"] = "New Blender-authored courtyard assembly; native broken-wall silhouette, added recessed windows and separate 2m clay UV."
    extract_native_timbers(collection)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.all_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["MiddleEasternCourtyard_RearWall"]
    bpy.ops.object.join()
    bpy.context.object.name = "MiddleEasternCourtyard"
    for layer in bpy.context.object.data.uv_layers:
        layer.active_render = layer.name == "LandmarkUV_2m"
    bpy.context.object.data.uv_layers.active = bpy.context.object.data.uv_layers["LandmarkUV_2m"]
    return collection


def translate_collection(collection, offset):
    """Move the whole authored building without independently refitting its pieces."""
    for obj in collection.all_objects:
        obj.location += Vector(offset)


def inspect_landmarks():
    """Return assembly bounds, source pieces, UV, material channels and triangle totals."""
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    records = []
    for asset_id, gallery_position in zip(ASSET_IDS, GALLERY_POSITIONS):
        collection = bpy.data.collections[COLLECTION_PREFIX + asset_id]
        pieces = [read_mesh_record(obj, depsgraph) for obj in collection.all_objects if obj.type == "MESH"]
        bounds_min = [min(piece["bounds_min"][axis] for piece in pieces) for axis in range(3)]
        bounds_max = [max(piece["bounds_max"][axis] for piece in pieces) for axis in range(3)]
        records.append({"id": asset_id, "source_scene": bpy.context.scene.name,
                        "source_collection": collection.name, "source_objects": [piece["name"] for piece in pieces],
                        "triangles": sum(piece["triangles"] for piece in pieces), "pieces": pieces,
                        "dimensions_blender_xyz_m": [bounds_max[axis] - bounds_min[axis] for axis in range(3)],
                        "bounds_min": bounds_min, "gallery_translation": list(gallery_position)})
    return records


def prepare_staging():
    """Save only a new isolated source for visual review, never a user document."""
    if not bpy.app.background or bpy.data.filepath or STAGING_SOURCE.exists():
        raise ValueError("Preparation requires factory startup and an unused staging destination")
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    parent = bpy.data.collections.new(PARENT_COLLECTION)
    bpy.context.scene.collection.children.link(parent)
    collections = (create_european_house(parent), create_north_african_ruins(parent),
                   create_middle_eastern_courtyard(parent))
    for collection, position in zip(collections, GALLERY_POSITIONS):
        translate_collection(collection, position)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGING_SOURCE))
    write_report({"status": "isolated_visual_review_pending", "source": str(STAGING_SOURCE),
                  "records": inspect_landmarks()}, PROJECT_ROOT / "art/environment/landmark_preparation.json")


def render_landmarks(output_directory):
    """Render one consistent isometric evidence image per building; do not save review lights."""
    if not bpy.app.background or PARENT_COLLECTION not in bpy.data.collections:
        raise ValueError("Render requires a saved landmark source")
    output_directory = Path(output_directory)
    output_directory.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new("Review-only neutral world")
    scene.world.use_nodes = True
    world_nodes = scene.world.node_tree.nodes
    background = next((node for node in world_nodes if node.type == "BACKGROUND"), None)
    if background is None:
        background = world_nodes.new("ShaderNodeBackground")
        output = next((node for node in world_nodes if node.type == "OUTPUT_WORLD"), None)
        if output is None:
            output = world_nodes.new("ShaderNodeOutputWorld")
        scene.world.node_tree.links.new(background.outputs[0], output.inputs[0])
    background.inputs[0].default_value = (.7, .73, .76, 1)
    background.inputs[1].default_value = .65
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0
    for obj in scene.objects:
        obj.hide_render = True
    sun = bpy.data.lights.new("Review-only sun", "SUN")
    sun.energy = 2.4
    sun.angle = math.radians(10)
    sun_obj = bpy.data.objects.new("Review-only sun", sun)
    scene.collection.objects.link(sun_obj)
    sun_obj.rotation_euler = (.5, -.6, -.5)
    camera = bpy.data.cameras.new("Review-only camera")
    camera.type = "ORTHO"
    camera_obj = bpy.data.objects.new("Review-only camera", camera)
    scene.collection.objects.link(camera_obj)
    scene.camera = camera_obj
    for record in inspect_landmarks():
        collection = bpy.data.collections[record["source_collection"]]
        for obj in collection.all_objects:
            obj.hide_render = False
        position = Vector(record["gallery_translation"])
        camera.ortho_scale = max(record["dimensions_blender_xyz_m"][:2]) * 1.55
        camera_obj.location = position + Vector((11, -15, 11))
        camera_obj.rotation_euler = (position + Vector((0, 0, 1.3)) - camera_obj.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output_directory / f"{record['id']}.png")
        bpy.context.view_layer.update()
        bpy.ops.render.render(write_still=True)
        for obj in collection.all_objects:
            obj.hide_render = True


def merge_approved_source(expected_sha256):
    """Append reviewed collections with hash guards, preserving every existing datablock."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != FORMAL_SOURCE or bpy.data.is_dirty:
        raise ValueError("Merge requires a fresh independent process reading the formal environment file")
    if get_file_hash(FORMAL_SOURCE) != expected_sha256 or PARENT_COLLECTION in bpy.data.collections:
        raise ValueError("Formal source changed or landmark collection already exists")
    prior_signatures = read_geometry_signatures()
    prior_identity = read_persistent_identity()
    prior_handoff = json.loads((PROJECT_ROOT / "art/environment/vegetation_handoff_candidate.json").read_text(encoding="utf-8"))
    prior_exports = {record["model_path"]: record["model_sha256"] for record in prior_handoff["records"]}
    for export_path, expected_hash in prior_exports.items():
        if get_file_hash(PROJECT_ROOT / export_path) != expected_hash:
            raise ValueError(f"Existing vegetation export changed before merge: {export_path}")
    existing_names = set(bpy.data.objects.keys())
    prior_images = {image.name: hashlib.sha256(bytes(image.packed_file.data)).hexdigest()
                    for image in bpy.data.images if image.packed_file}
    snapshot = PROJECT_ROOT / "art/environment/snapshots" / f"before-landmarks-{datetime.now():%Y%m%d-%H%M%S}.blend"
    snapshot.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(FORMAL_SOURCE, snapshot)
    with bpy.data.libraries.load(str(STAGING_SOURCE), link=False) as (available, loaded):
        if PARENT_COLLECTION not in available.collections:
            raise ValueError("Missing reviewed collection in staging")
        loaded.collections = [PARENT_COLLECTION]
    bpy.context.scene.collection.children.link(loaded.collections[0])
    current_signatures = read_geometry_signatures()
    current_identity = read_persistent_identity()
    if current_identity["scenes"] != prior_identity["scenes"]:
        raise ValueError("Appending landmarks changed existing scenes")
    for name, identity in prior_identity["objects"].items():
        if current_identity["objects"].get(name) != identity:
            raise ValueError(f"Existing object identity changed: {name}")
    if any(current_signatures.get(name) != signature for name, signature in prior_signatures.items()):
        raise ValueError("Existing object mesh/UV/material/transform signature changed")
    if get_file_hash(FORMAL_SOURCE) != expected_sha256:
        raise ValueError("Formal source changed during authoring; refusing overwrite")
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(FORMAL_SOURCE), compress=True)
    bpy.ops.wm.open_mainfile(filepath=str(FORMAL_SOURCE))
    signatures = read_geometry_signatures()
    if any(signatures.get(name) != signature for name, signature in prior_signatures.items()):
        raise ValueError("Reopen changed existing mesh signatures")
    for name, expected_hash in prior_images.items():
        image = bpy.data.images.get(name)
        if not image or not image.packed_file or hashlib.sha256(bytes(image.packed_file.data)).hexdigest() != expected_hash:
            raise ValueError(f"Existing packed texture changed: {name}")
    if not existing_names.issubset(set(bpy.data.objects.keys())):
        raise ValueError("Existing object lost after reopen")
    if bpy.data.libraries or any(image.source == "FILE" and not image.packed_file for image in bpy.data.images):
        raise ValueError("Formal category source is not portable")
    reopened_identity = read_persistent_identity()
    if any(reopened_identity["objects"].get(name) != identity for name, identity in prior_identity["objects"].items()):
        raise ValueError("Reopen changed existing object identity")
    for export_path, expected_hash in prior_exports.items():
        if get_file_hash(PROJECT_ROOT / export_path) != expected_hash:
            raise ValueError(f"Existing vegetation export changed after merge: {export_path}")
    write_report({"status": "formal_category_source_reopened", "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                  "source_sha256": get_file_hash(FORMAL_SOURCE), "predecessor_sha256": expected_sha256,
                  "recovery_snapshot": str(snapshot.relative_to(PROJECT_ROOT)),
                  "existing_geometry_uv_material_transform_signatures": prior_signatures,
                  "existing_packed_image_hashes": prior_images, "existing_objects_preserved": len(existing_names),
                  "existing_export_hashes_unchanged": prior_exports,
                  "existing_images_preserved": len(prior_images), "packed_images": sum(bool(image.packed_file) for image in bpy.data.images),
                  "external_libraries": len(bpy.data.libraries), "source_bytes": FORMAL_SOURCE.stat().st_size,
                  "records": inspect_landmarks()}, PROJECT_ROOT / "art/environment/landmark_category_validation.json")


def read_persistent_identity():
    """Use stable datablock names, never process pointers, across file reopening."""
    identity = read_scene_identity()
    for name, record in identity["objects"].items():
        obj = bpy.data.objects[name]
        record["data"] = obj.data.name if obj.data else None
    return identity


def read_material_signatures():
    """Fingerprint actual existing shader nodes, constants, links and image bindings."""
    signatures = {}
    for material in bpy.data.materials:
        nodes = []
        if material.node_tree:
            for node in material.node_tree.nodes:
                inputs = []
                for socket in node.inputs:
                    if not hasattr(socket, "default_value"):
                        continue
                    value = socket.default_value
                    if not isinstance(value, (str, int, float, bool)):
                        try:
                            value = list(value)
                        except TypeError:
                            value = getattr(value, "name", None)
                    inputs.append([socket.identifier, value])
                nodes.append({"name": node.name, "type": node.bl_idname, "mute": node.mute,
                              "inputs": inputs, "image": node.image.name if node.type == "TEX_IMAGE" and node.image else None})
            links = sorted([link.from_node.name, link.from_socket.identifier, link.to_node.name, link.to_socket.identifier]
                           for link in material.node_tree.links)
        else:
            links = []
        record = {"nodes": sorted(nodes, key=lambda node: node["name"]), "links": links,
                  "use_nodes": material.use_nodes, "backface_culling": material.use_backface_culling,
                  "diffuse_color": list(material.diffuse_color)}
        signatures[material.name] = hashlib.sha256(json.dumps(record, sort_keys=True).encode()).hexdigest()
    return signatures


def validate_saved_extension(snapshot_path, expected_sha256):
    """Audit a saved extension against its exact recovery snapshot without another save."""
    snapshot = Path(snapshot_path)
    predecessor_hash = get_file_hash(snapshot)
    if predecessor_hash != "22cf18b836561446acae85f98b97086e40cac6ec23e88f91eebe05c8310ad56f":
        raise ValueError("Recovery snapshot differs from approved predecessor")
    bpy.ops.wm.open_mainfile(filepath=str(snapshot))
    prior_signatures = read_geometry_signatures()
    prior_identity = read_persistent_identity()
    prior_materials = read_material_signatures()
    prior_collections = {collection.name: {"children": list(collection.children.keys()),
                        "objects": list(collection.objects.keys()), "hide_render": collection.hide_render,
                        "hide_viewport": collection.hide_viewport} for collection in bpy.data.collections}
    prior_images = {image.name: hashlib.sha256(bytes(image.packed_file.data)).hexdigest()
                    for image in bpy.data.images if image.packed_file}
    if get_file_hash(FORMAL_SOURCE) != expected_sha256:
        raise ValueError("Extended formal source changed before read-only validation")
    bpy.ops.wm.open_mainfile(filepath=str(FORMAL_SOURCE))
    current_signatures = read_geometry_signatures()
    current_identity = read_persistent_identity()
    current_materials = read_material_signatures()
    if current_identity["scenes"] != prior_identity["scenes"]:
        raise ValueError("Existing scenes changed")
    for name, signature in prior_signatures.items():
        if current_signatures.get(name) != signature or current_identity["objects"].get(name) != prior_identity["objects"][name]:
            raise ValueError(f"Existing mesh/UV/material/transform changed: {name}")
    for name, expected_hash in prior_images.items():
        image = bpy.data.images.get(name)
        if not image or not image.packed_file or hashlib.sha256(bytes(image.packed_file.data)).hexdigest() != expected_hash:
            raise ValueError(f"Existing packed image changed: {name}")
    if any(current_materials.get(name) != signature for name, signature in prior_materials.items()):
        raise ValueError("Existing material shader fingerprint changed")
    for name, expected in prior_collections.items():
        collection = bpy.data.collections.get(name)
        if not collection or {"children": list(collection.children.keys()), "objects": list(collection.objects.keys()),
                              "hide_render": collection.hide_render, "hide_viewport": collection.hide_viewport} != expected:
            raise ValueError(f"Existing collection changed: {name}")
    if bpy.data.libraries or any(image.source == "FILE" and not image.packed_file for image in bpy.data.images):
        raise ValueError("Extended source is not portable")
    prior_handoff = json.loads((PROJECT_ROOT / "art/environment/vegetation_handoff_candidate.json").read_text(encoding="utf-8"))
    exports = {record["model_path"]: record["model_sha256"] for record in prior_handoff["records"]}
    if any(get_file_hash(PROJECT_ROOT / path) != expected_hash for path, expected_hash in exports.items()):
        raise ValueError("Existing vegetation FBX checksum changed")
    write_report({"status": "formal_category_source_reopened", "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                  "source_sha256": expected_sha256, "predecessor_sha256": predecessor_hash,
                  "recovery_snapshot": str(snapshot.relative_to(PROJECT_ROOT)),
                  "existing_geometry_uv_material_transform_signatures": prior_signatures,
                  "existing_packed_image_hashes": prior_images, "existing_export_hashes_unchanged": exports,
                  "existing_material_shader_signatures": prior_materials, "existing_collections_unchanged": prior_collections,
                  "existing_objects_preserved": len(prior_identity["objects"]), "existing_images_preserved": len(prior_images),
                  "packed_images": sum(bool(image.packed_file) for image in bpy.data.images),
                  "external_libraries": len(bpy.data.libraries), "source_bytes": FORMAL_SOURCE.stat().st_size,
                  "audit_note": "Process-pointer identity check after save was corrected to stable datablock names; fresh snapshot-versus-formal read-only audit passed.",
                  "records": inspect_landmarks()}, PROJECT_ROOT / "art/environment/landmark_category_validation.json")


def export_image_channel(image, output_directory):
    """Copy exact packed author bytes, with no fabricated channel conversion."""
    if not image.packed_file:
        raise ValueError(f"Unpacked landmark texture: {image.name}")
    filename = Path(image.filepath).name
    image_path = output_directory / filename
    packed_bytes = bytes(image.packed_file.data)
    packed_sha256 = hashlib.sha256(packed_bytes).hexdigest()
    if image_path.exists() and get_file_hash(image_path) != packed_sha256:
        raise ValueError(f"Texture basename collision: {image_path}")
    image_path.write_bytes(packed_bytes)
    return {"path": str(image_path.relative_to(PROJECT_ROOT)), "sha256": packed_sha256,
            "resolution": list(image.size)}


def export_material_record(material, output_directory):
    """Record real diffuse/normal/rough images and explicit absent-map constants."""
    channels = {}
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        targets = [link.to_socket.name for link in material.node_tree.links if link.from_node == node]
        target_types = [link.to_node.type for link in material.node_tree.links if link.from_node == node]
        if node.label in ("base_color", "normal_gl", "roughness"):
            channel = node.label
        elif "NORMAL_MAP" in target_types:
            channel = "normal_gl"
        elif "Color" in targets or "Base Color" in targets:
            channel = "base_color"
        else:
            continue
        record = export_image_channel(node.image, output_directory)
        record.update(colorspace="sRGB" if channel == "base_color" else "linear",
                      component="red" if channel == "roughness" else "rgb")
        channels[channel] = record
    if "base_color" not in channels:
        raise ValueError(f"Missing authored diffuse channel: {material.name}")
    shader = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    normal_node = next((node for node in material.node_tree.nodes if node.type == "NORMAL_MAP"), None)
    roughness = shader.inputs["Roughness"].default_value if shader else .95
    normal_strength = normal_node.inputs["Strength"].default_value if normal_node else 1
    return {"name": material.name, "channels": channels, "alpha_mode": "opaque", "double_sided": True,
            "metallic": 0, "roughness_constant_if_missing": roughness,
            "normal_strength": normal_strength, "base_color_tint_linear": list(material.get("landmark_base_color_tint", (1, 1, 1, 1))),
            "roughness_to_smoothness": "Convert once in Unity; absent roughness uses explicit authored constant, not fake PBR map",
            "source_diffuse_note": "Original legacy artist atlas, may contain baked shading" if not material.name.startswith("MiddleEasternCourtyard_") else "Poly Haven CC0 photographed clay plaster"}


def export_landmarks(expected_sha256, output_directory):
    """Export approved whole assemblies at their common authored pivot without saving mutations."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != FORMAL_SOURCE or bpy.data.is_dirty:
        raise ValueError("Export requires the fresh formal category source in an independent process")
    if get_file_hash(FORMAL_SOURCE) != expected_sha256:
        raise ValueError("Formal source changed before export")
    source_records = inspect_landmarks()
    for record in source_records:
        collection = bpy.data.collections[record["source_collection"]]
        meshes = [obj for obj in collection.all_objects if obj.type == "MESH"]
        asset_directory = Path(output_directory) / record["id"]
        texture_directory = asset_directory / "Textures"
        texture_directory.mkdir(parents=True, exist_ok=True)
        materials = list({material.name: material for obj in meshes for material in obj.data.materials}.values())
        record["materials"] = [export_material_record(material, texture_directory) for material in materials]
        record["material_slots"] = [material.name for material in materials]
        record["assembly_local_matrices_blender"] = {}
        for obj in meshes:
            local_matrix = obj.matrix_world.copy()
            local_matrix.translation -= Vector(record["gallery_translation"])
            record["assembly_local_matrices_blender"][obj.name] = [list(row) for row in local_matrix]
        model_path = asset_directory / f"{record['id']}.fbx"
        original_matrices = {obj: obj.matrix_world.copy() for obj in meshes}
        try:
            bpy.ops.object.select_all(action="DESELECT")
            for obj in meshes:
                obj.matrix_world.translation -= Vector(record["gallery_translation"])
                obj.select_set(True)
            bpy.context.view_layer.objects.active = meshes[0]
            bpy.context.view_layer.update()
            bpy.ops.export_scene.fbx(filepath=str(model_path), use_selection=True, object_types={"MESH"},
                                     global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                                     axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                                     use_mesh_modifiers=False, use_triangles=True, bake_anim=False,
                                     path_mode="STRIP", embed_textures=False, add_leaf_bones=False)
        finally:
            for obj, matrix in original_matrices.items():
                obj.matrix_world = matrix
            bpy.context.view_layer.update()
        record.update(model_path=str(model_path.relative_to(PROJECT_ROOT)), model_sha256=get_file_hash(model_path),
                      source_blend=str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)),
                      unit_m=1, placement_scale=1, axis_forward="-Z", axis_up="Y",
                      axis_conversion="FBX baked once. Unity import scale 1. Whole assembly only; never center/refit parts",
                      uv0_note="FBX UV0 is author atlas for Europe/NorthAfrica and combined clay/wood UV for new courtyard",
                      placement_note="Neutral ground landmark; whole root uniform scale only. Native roofless interiors expose Terrain, no invented floor.")
        dimensions = record["dimensions_blender_xyz_m"]
        record["dimensions_unity_xyz_m"] = [dimensions[0], dimensions[2], dimensions[1]]
    handoff = {"schema_version": 1, "status": "parent_approved_preview_source_pending_unity_visual_acceptance",
               "source_blend": str(FORMAL_SOURCE.relative_to(PROJECT_ROOT)), "source_sha256": expected_sha256,
               "source_scene": bpy.context.scene.name, "source_collection": PARENT_COLLECTION,
               "license_manifest": "art/environment/landmark_source_manifest.json",
               "attribution": "art/environment/landmark_attribution.txt", "records": source_records}
    write_report(handoff, PROJECT_ROOT / "art/environment/landmark_handoff.json")


def verify_landmark_exports(handoff_path):
    """Round-trip all pieces, UV0, common bounds, slots and exact source texture bytes."""
    if not bpy.app.background or bpy.data.filepath:
        raise ValueError("FBX validation requires independent factory startup")
    handoff = json.loads(Path(handoff_path).read_text(encoding="utf-8"))
    records = []
    for expected in handoff["records"]:
        model_path = PROJECT_ROOT / expected["model_path"]
        if get_file_hash(model_path) != expected["model_sha256"]:
            raise ValueError(f"FBX changed: {model_path}")
        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.ops.import_scene.fbx(filepath=str(model_path), use_custom_normals=True)
        bpy.context.view_layer.update()
        objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        pieces = [read_mesh_record(obj, bpy.context.evaluated_depsgraph_get()) for obj in objects]
        triangles = sum(piece["triangles"] for piece in pieces)
        bounds_min = [min(piece["bounds_min"][axis] for piece in pieces) for axis in range(3)]
        bounds_max = [max(piece["bounds_max"][axis] for piece in pieces) for axis in range(3)]
        dimensions = [bounds_max[axis] - bounds_min[axis] for axis in range(3)]
        if triangles != expected["triangles"] or abs(bounds_min[2]) > .001:
            raise ValueError(f"FBX geometry/pivot mismatch: {expected['id']}")
        if len(pieces) != len(expected["pieces"]) or any(not piece["uv_layers"] for piece in pieces):
            raise ValueError(f"FBX assembly/UV mismatch: {expected['id']}")
        source_pieces = {piece["name"]: piece for piece in expected["pieces"]}
        for piece in pieces:
            source_piece = source_pieces.get(piece["name"])
            if source_piece is None or piece["materials"] != source_piece["materials"]:
                raise ValueError(f"FBX material slots changed: {expected['id']}: {piece['name']}")
        if any(abs(actual - wanted) > .001 for actual, wanted in zip(dimensions, expected["dimensions_blender_xyz_m"])):
            raise ValueError(f"FBX dimensions changed: {expected['id']}: {dimensions}")
        for material in expected["materials"]:
            for channel in material["channels"].values():
                if get_file_hash(PROJECT_ROOT / channel["path"]) != channel["sha256"]:
                    raise ValueError(f"Texture changed: {channel['path']}")
        records.append({"id": expected["id"], "status": "pass", "triangles": triangles,
                        "dimensions_blender_xyz_m": dimensions, "bounds_min": bounds_min, "pieces": pieces})
    write_report({"status": "fbx_roundtrip_pass", "source_blend": handoff["source_blend"],
                  "source_sha256": handoff["source_sha256"], "records": records},
                 PROJECT_ROOT / "art/environment/landmark_export_validation.json")


def parse_arguments():
    """Read only explicit authoring/review/merge modes."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("prepare", "render", "merge", "export", "verify-exports", "validate-extension"))
    parser.add_argument("--snapshot")
    parser.add_argument("--expected-sha256")
    parser.add_argument("--handoff", default=str(PROJECT_ROOT / "art/environment/landmark_handoff.json"))
    parser.add_argument("--output", default=str(PROJECT_ROOT / "art/environment/inspection/landmarks"))
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1:])


if __name__ == "__main__":
    arguments = parse_arguments()
    if arguments.mode == "prepare":
        prepare_staging()
    elif arguments.mode == "render":
        render_landmarks(arguments.output)
    elif arguments.mode == "merge":
        merge_approved_source(arguments.expected_sha256)
    elif arguments.mode == "export":
        export_landmarks(arguments.expected_sha256, arguments.output)
    elif arguments.mode == "validate-extension":
        validate_saved_extension(arguments.snapshot, arguments.expected_sha256)
    else:
        verify_landmark_exports(arguments.handoff)
