"""Assemble the user's unchanged wall kit into a registered northern-entry city ring.

Run only in background Blender on the saved general master. No live GUI access,
new world meshes, re-fitting, unit/gameplay edits, or old-handoff restoration.
"""

import hashlib
import json
import math
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree


ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame.blend"
REVIEW = ROOT / "Builds/ArtReview/FortifiedCity"
OUTPUT = ROOT / "art/models/fortified-city"
HANDOFF = ROOT / "art/fortified_city_handoff.json"
GROUP = "城防实装 · 北入口完整城市围墙"
KIT = "城墙 · 00_ModularWall_Kit"
SOURCE_SCALE = 18.0
CORNER_EMBED_M = 1.2
EXPECTED_SOURCE_SHA = "1472ab550fd4837a49caea1856ee58ece99b7702b77ee4f15d9d082df1117d66"


def hash_file(path):
    """Return a SHA-256 of an existing source or generated artifact."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def hash_payload(payload):
    return hashlib.sha256(json.dumps(payload, sort_keys=True).encode()).hexdigest()


def read_rna_values(owner):
    """Read scalar/array settings while representing ID pointers without mutation."""
    values = {}
    for prop in owner.bl_rna.properties:
        key = prop.identifier
        if key in {"rna_type", "users", "tag", "is_updated", "is_updated_data",
                   "is_updated_transform", "original", "is_evaluated", "session_uid"} or prop.type == "COLLECTION":
            continue
        try:
            value = getattr(owner, key)
            if prop.type == "POINTER":
                values[key] = value.name if isinstance(value, bpy.types.ID) else str(type(value))
            elif prop.is_array:
                values[key] = list(value)
            elif isinstance(value, (str, int, float, bool)) or value is None:
                values[key] = value
        except (AttributeError, TypeError, ValueError):
            continue
    return values


def read_nodes(owner):
    tree = getattr(owner, "node_tree", None)
    if tree is None:
        return None
    nodes = []
    for node in tree.nodes:
        sockets = []
        for socket in node.inputs:
            value = getattr(socket, "default_value", None)
            if value is not None:
                try:
                    value = list(value)
                except TypeError:
                    pass
            sockets.append([socket.identifier, value])
        nodes.append([node.name, read_rna_values(node), sockets])
    return {"nodes": nodes, "links": sorted([
        [link.from_node.name, link.from_socket.identifier,
         link.to_node.name, link.to_socket.identifier] for link in tree.links])}


def read_mesh_record(mesh):
    """Hash authored vertex/topology/UV/material data; shared instance counts may grow."""
    return hash_payload({
        "vertices": [list(vertex.co) for vertex in mesh.vertices],
        "edges": [list(edge.vertices) for edge in mesh.edges],
        "faces": [[list(face.vertices), face.material_index, face.use_smooth]
                  for face in mesh.polygons],
        "uv": [[layer.name, [list(loop.uv) for loop in layer.data]]
               for layer in mesh.uv_layers],
        "materials": [material.name if material else None for material in mesh.materials],
        "custom_normals": mesh.has_custom_normals,
    })


def read_action_record(action):
    curves = []
    for layer in action.layers:
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                for curve in bag.fcurves:
                    curves.append([curve.data_path, curve.array_index,
                                   [[list(point.co), list(point.handle_left),
                                     list(point.handle_right), point.interpolation]
                                    for point in curve.keyframe_points]])
    return hash_payload({"settings": read_rna_values(action), "curves": curves})


def capture_protected_state():
    """Capture the actual current disk layout, never substitute a historical handoff."""
    return {
        "objects": {obj.name: {
            "type": obj.type, "data": obj.data.name if obj.data else None,
            "parent": obj.parent.name if obj.parent else None,
            "world": [list(row) for row in obj.matrix_world],
            "basis": [list(row) for row in obj.matrix_basis],
            "hidden": [obj.hide_render, obj.hide_viewport, obj.hide_get()],
            "collections": sorted(collection.name for collection in obj.users_collection),
            "constraints": [read_rna_values(item) for item in obj.constraints],
            "modifiers": [read_rna_values(item) for item in obj.modifiers],
            "action": obj.animation_data.action.name if obj.animation_data and obj.animation_data.action else None,
        } for obj in bpy.data.objects},
        "meshes": {mesh.name: read_mesh_record(mesh) for mesh in bpy.data.meshes},
        "materials": {material.name: hash_payload([read_rna_values(material), read_nodes(material)])
                      for material in bpy.data.materials},
        "images": {image.name: {"filepath": str(Path(bpy.path.abspath(image.filepath)).resolve()), "size": list(image.size),
                                "colorspace": image.colorspace_settings.name,
                                "packed": hashlib.sha256(image.packed_file.data).hexdigest()
                                if image.packed_file else None}
                   for image in bpy.data.images},
        "actions": {action.name: read_action_record(action) for action in bpy.data.actions},
        "collections": {collection.name: {"objects": sorted(collection.objects.keys()),
                                           "children": sorted(collection.children.keys()),
                                           "hidden": [collection.hide_render, collection.hide_viewport]}
                        for collection in bpy.data.collections},
        "lights": {light.name: hash_payload([read_rna_values(light), read_nodes(light)])
                   for light in bpy.data.lights},
        "cameras": {camera.name: hash_payload(read_rna_values(camera)) for camera in bpy.data.cameras},
        "worlds": {world.name: hash_payload([read_rna_values(world), read_nodes(world)])
                   for world in bpy.data.worlds},
        "armatures": {armature.name: hash_payload([
            [bone.name, bone.parent.name if bone.parent else None,
             [list(row) for row in bone.matrix_local]] for bone in armature.bones])
                      for armature in bpy.data.armatures},
        "scenes": {scene.name: {"frame": scene.frame_current,
                                 "camera": scene.camera.name if scene.camera else None,
                                 "world": scene.world.name if scene.world else None,
                                 "render": read_rna_values(scene.render),
                                 "view": read_rna_values(scene.view_settings)}
                   for scene in bpy.data.scenes},
    }


def verify_protected_state(expected):
    actual = capture_protected_state()
    for category, records in expected.items():
        for name, record in records.items():
            if actual[category].get(name) != record:
                raise AssertionError(f"Protected {category} changed: {name}")
    return {category: len(records) for category, records in expected.items()}


def read_bounds(mesh):
    points = [vertex.co for vertex in mesh.vertices]
    return (Vector([min(point[axis] for point in points) for axis in range(3)]),
            Vector([max(point[axis] for point in points) for axis in range(3)]))


def require_sources():
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER:
        raise RuntimeError("Require background Blender on the formal general master")
    if hash_file(MASTER) != EXPECTED_SOURCE_SHA:
        raise RuntimeError("General master changed since approved audit; re-audit before authoring")
    if GROUP in bpy.data.collections:
        raise RuntimeError("City collection already present; refuse duplicate authoring")
    kit = bpy.data.collections.get(KIT)
    if kit is None:
        raise RuntimeError(f"Current master lacks original wall kit: {KIT}")
    return {name: next(obj for obj in kit.objects if obj.name.endswith(" · " + name))
            for name in ("StartPier", "MiddleWall", "EndPier")}


def plan_assembly(sources):
    """Plan the whole ring from original whole modules with no mesh stretching/cutting."""
    left, body, right = [sources[name] for name in ("StartPier", "MiddleWall", "EndPier")]
    left_low, left_high = read_bounds(left.data)
    body_low, body_high = read_bounds(body.data)
    right_low, right_high = read_bounds(right.data)
    pier_width = left_high.x - left_low.x
    pier_depth = left_high.y - left_low.y
    pitch = body_high.x - body_low.x
    width = 2 * pier_width + 3 * pitch
    corner_embed = CORNER_EMBED_M / SOURCE_SCALE
    depth = 2 * pier_depth + 4 * pitch - 2 * corner_embed
    half_width, half_depth = width * .5, depth * .5
    # Pier bases were already 0.0375977 units high in the unchanged kit. Ground each
    # instance via translation, preserving its geometry; no levitating corner feet.
    records = []

    def add_record(name, source, location, yaw=0, role="wall"):
        low, _ = read_bounds(source.data)
        matrix = Matrix.Translation(Vector((*location, -low.z))) @ Matrix.Rotation(yaw, 4, "Z")
        records.append({"name": name, "source": source, "matrix": matrix, "role": role})

    for side, y in (("South", -half_depth + pier_depth * .5),
                    ("North", half_depth - pier_depth * .5)):
        for corner, x, source in (("West", -half_width, left),
                                   ("East", half_width - pier_width, right)):
            add_record(f"CityWall_{side}_{corner}Pier", source, (x, y), role="corner_pier")
        for index in range(3):
            if side == "North" and index == 1:
                continue
            x = -half_width + pier_width + index * pitch
            add_record(f"CityWall_{side}_Body_{index:02d}", body, (x, y))

    for side, x in (("West", -half_width + pier_width * .5),
                    ("East", half_width - pier_width * .5)):
        for index in range(4):
            y = -half_depth + pier_depth - corner_embed + index * pitch
            add_record(f"CityWall_{side}_Body_{index:02d}", body, (x, y), math.pi * .5)

    north_y = half_depth - pier_depth * .5
    opening_start = -half_width + pier_width + pitch
    opening_end = opening_start + pitch
    add_record("CityWall_North_PortalWestPier", right, (opening_start, north_y), role="portal_pier")
    add_record("CityWall_North_PortalEastPier", left, (opening_end - pier_width, north_y), role="portal_pier")
    return records, {"body_pitch": pitch, "pier_width": pier_width, "pier_depth": pier_depth,
                     "corner_embed_m": CORNER_EMBED_M,
                     "nominal_width": width, "nominal_depth": depth,
                     "portal": {"center": [0, north_y], "clear_width": pitch - 2 * pier_width,
                                "clear_min_x": opening_start + pier_width,
                                "clear_max_x": opening_end - pier_width,
                                "recess_min_x": opening_start, "recess_max_x": opening_end}}


def read_record_points(record):
    return [record["matrix"] @ vertex.co for vertex in record["source"].data.vertices]


def inspect_assembly(records, dimensions):
    """Probe exterior wall coverage and actual open portal without mutating the source."""
    vertices, polygons, bounds = [], [], []
    for record in records:
        points = read_record_points(record)
        offset = len(vertices)
        vertices.extend(points)
        polygons.extend([[index + offset for index in face.vertices]
                         for face in record["source"].data.polygons])
        bounds.append({"name": record["name"],
                       "min": [min(point[axis] for point in points) for axis in range(3)],
                       "max": [max(point[axis] for point in points) for axis in range(3)]})
    tree = BVHTree.FromPolygons(vertices, polygons)
    low = Vector([min(point[axis] for point in vertices) for axis in range(3)])
    high = Vector([max(point[axis] for point in vertices) for axis in range(3)])
    missing, samples = [], 0
    half_width, half_depth = dimensions["nominal_width"] * .5, dimensions["nominal_depth"] * .5
    portal = dimensions["portal"]
    for axis, minimum, maximum in ((0, -half_width, half_width), (1, -half_depth, half_depth)):
        for step in range(1, 401):
            along = minimum + (maximum - minimum) * step / 401
            for sign in (-1, 1):
                if axis == 0 and sign == 1 and portal["clear_min_x"] < along < portal["clear_max_x"]:
                    continue
                for height_m in (.8, 1.6, 3.2, 4.8, 5.8):
                    # Probe only boundary surfaces (max 8.1 m deep) rather than letting
                    # a ray miss the near wall and falsely hit its opposite counterpart.
                    distance = 8.1 / SOURCE_SCALE
                    if axis == 0:
                        start = Vector((along, sign * (half_depth + .1), height_m / SOURCE_SCALE))
                        direction = Vector((0, -sign, 0))
                    else:
                        start = Vector((sign * (half_width + .1), along, height_m / SOURCE_SCALE))
                        direction = Vector((-sign, 0, 0))
                    hit = tree.ray_cast(start, direction, distance)[0]
                    samples += 1
                    if hit is None:
                        missing.append({"along_axis": axis, "along": along * SOURCE_SCALE,
                                        "side": sign, "height_m": height_m})
    open_hits = []
    for lateral in (-.4, -.2, 0, .2, .4):
        x = lateral * portal["clear_width"]
        for height_m in (.4, 1, 2.2, 3.8):
            hit = tree.ray_cast(Vector((x, half_depth + .1, height_m / SOURCE_SCALE)),
                                Vector((0, -1, 0)), .6)[0]
            if hit is not None:
                open_hits.append([x * SOURCE_SCALE, height_m])
    radial_misses, radial_samples = [], 0
    for step in range(1440):
        angle = math.tau * step / 1440
        direction = Vector((math.cos(angle), math.sin(angle), 0))
        north_x = direction.x / direction.y * dimensions["portal"]["center"][1] if direction.y > 0 else None
        # The original piers taper above their broad feet, so the open portal
        # recess widens naturally with height. Only its foot clearance is 6.489 m.
        in_portal_sector = north_x is not None and portal["recess_min_x"] <= north_x <= portal["recess_max_x"]
        if in_portal_sector:
            continue
        for height_m in (.8, 1.6, 3.2, 4.8, 5.4):
            hit = tree.ray_cast(Vector((0, 0, height_m / SOURCE_SCALE)), direction, 4)[0]
            radial_samples += 1
            if hit is None:
                radial_misses.append({"angle_degrees": math.degrees(angle), "height_m": height_m,
                                       "north_intersection_x_m": north_x * SOURCE_SCALE if north_x is not None else None})
    return {"bounds_min": list(low * SOURCE_SCALE), "bounds_max": list(high * SOURCE_SCALE),
            "bounds_size": list((high - low) * SOURCE_SCALE),
            "component_bounds_native": bounds, "boundary_ray_samples": samples,
            "boundary_ray_misses": missing, "portal_unexpected_hits": open_hits,
            "radial_ray_samples": radial_samples, "radial_ray_misses": radial_misses,
            "ground_minima_m": {record["name"]: min(point.z for point in read_record_points(record)) * SOURCE_SCALE
                                 for record in records}}


def run_audit():
    sources = require_sources()
    records, dimensions = plan_assembly(sources)
    validation = inspect_assembly(records, dimensions)
    REVIEW.mkdir(parents=True, exist_ok=True)
    report = {"source_sha256": hash_file(MASTER), "scene_objects": len(bpy.context.scene.objects),
              "sources": {name: {"object": obj.name, "mesh": obj.data.name,
                                   "geometry_uv_sha256": read_mesh_record(obj.data)}
                          for name, obj in sources.items()},
              "dimensions_native": dimensions, "uniform_source_scale": SOURCE_SCALE,
              "validation": validation}
    (REVIEW / "assembly-proposal.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print("CITY ASSEMBLY AUDIT", json.dumps({"size_m": validation["bounds_size"],
        "ray_misses": len(validation["boundary_ray_misses"]),
        "radial_misses": len(validation["radial_ray_misses"]),
        "portal_hits": len(validation["portal_unexpected_hits"]),
        "portal_clear_m": dimensions["portal"]["clear_width"] * SOURCE_SCALE}))
    return sources, records, dimensions, validation


def render_inspection(records):
    """Render only a temporary diagnostic scene; original lights/cameras are untouched."""
    original = bpy.context.window.scene
    scene = bpy.data.scenes.new("Temporary city ring inspection")
    bpy.context.window.scene = scene
    for record in records:
        obj = bpy.data.objects.new(record["name"] + "_Inspection", record["source"].data)
        scene.collection.objects.link(obj)
        obj.matrix_world = record["matrix"]
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1500, 1200
    scene.render.resolution_percentage = 100
    camera = bpy.data.objects.new("Temporary city inspection camera", bpy.data.cameras.new("Temporary city inspection camera"))
    scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 4.1
    scene.camera = camera
    for name, location in (("top", (0, 0, 5)), ("north", (3.5, 4.5, 3.8))):
        camera.location = location
        camera.rotation_euler = (Vector((0, 0, .1)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(REVIEW / f"source-ring-{name}.png")
        bpy.ops.render.render(write_still=True)
    bpy.context.window.scene = original
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(scene)


def append_source_collection(records):
    """Append linked original meshes in a clear native-scale area of the master layout."""
    scene = bpy.context.scene
    collection = bpy.data.collections.new(GROUP)
    scene.collection.children.link(collection)
    existing_points = [obj.matrix_world @ Vector(corner) for obj in scene.objects
                       if obj.type == "MESH" and not obj.hide_viewport for corner in obj.bound_box]
    origin = Vector((max(point.x for point in existing_points) + 5,
                     min(point.y for point in existing_points) + 2, 0))
    root = bpy.data.objects.new("城市围墙 · 总布局展示锚点（导出整体18倍）", None)
    collection.objects.link(root)
    root.location = origin
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = .25
    root["export_uniform_source_scale"] = SOURCE_SCALE
    root["export_pivot"] = "Whole ring XY center at ground Z=0; display translation excluded"
    root["portal_status"] = "Open north entrance; no authored gate leaf or closable gate source"
    for record in records:
        obj = bpy.data.objects.new(record["name"], record["source"].data)
        collection.objects.link(obj)
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_basis = record["matrix"]
        obj["source_object"] = record["source"].name
        obj["source_mesh_sha256"] = read_mesh_record(record["source"].data)
        obj["city_component_role"] = record["role"]
        obj["geometry_unchanged"] = True
        obj["grounding_translation_native_z"] = record["matrix"].translation.z
    bpy.context.view_layer.update()
    return {"collection": collection.name, "root": root.name, "display_translation": list(origin),
            "scene": scene.name, "objects": [record["name"] for record in records]}


def save_protected_master(protected, records):
    """Create recoverable disk snapshot, save a candidate, reopen it, then promote safely."""
    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    backup = REVIEW / f"master-before-city-{timestamp}.blend"
    candidate = REVIEW / f"master-city-verified-candidate-{timestamp}.blend"
    original_image_paths = {image.name: image.filepath for image in bpy.data.images}
    shutil.copy2(MASTER, backup)
    if hash_file(backup) != EXPECTED_SOURCE_SHA:
        raise AssertionError("Recovery snapshot does not match protected source bytes")
    retention = []
    for image in bpy.data.images:
        if image.users == 0:
            image.use_fake_user = True
            retention.append({"image": image.name, "change": "Fake User retention only; bytes/path unchanged"})
    source_record = append_source_collection(records)
    counts = verify_protected_state(protected)
    bpy.ops.wm.save_as_mainfile(filepath=str(candidate), compress=True, relative_remap=True)
    bpy.ops.wm.open_mainfile(filepath=str(candidate))
    verify_protected_state(protected)
    if hash_file(MASTER) != EXPECTED_SOURCE_SHA:
        raise AssertionError("Master changed while authoring candidate; refuse promotion")
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER), compress=True, relative_remap=True)
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    verify_protected_state(protected)
    for name, filepath in original_image_paths.items():
        if bpy.data.images[name].filepath != filepath:
            raise AssertionError(f"Final master changed the original stored image path: {name}")
    return {**source_record, "protected_counts": counts, "recovery_snapshot": str(backup.relative_to(ROOT)),
            "candidate": str(candidate.relative_to(ROOT)), "retention": retention,
            "master_sha256": hash_file(MASTER), "objects_after": len(bpy.data.objects)}


def copy_export_material(source, output):
    """Use a temporary material/image copy; do not change original packed image paths."""
    material = source.copy()
    material.name = "FortifiedCity_WallOriginalTexture"
    textures = []
    for index, node in enumerate(material.node_tree.nodes if material.use_nodes else []):
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        original = node.image
        extension = Path(original.filepath).suffix or ".png"
        filename = f"wall-original-{index:02d}{extension}"
        path = output / filename
        if original.packed_file:
            path.write_bytes(original.packed_file.data)
        else:
            source_path = Path(bpy.path.abspath(original.filepath))
            if not source_path.is_file():
                raise FileNotFoundError(f"Missing source wall image: {source_path}")
            shutil.copy2(source_path, path)
        # Loading the extracted bytes directly avoids Image.copy trying to repack
        # an expired Tripo temporary pathname. The authored packed source is read-only.
        image = bpy.data.images.load(str(path), check_existing=False)
        image.colorspace_settings.name = original.colorspace_settings.name
        node.image = image
        textures.append({"source_image": original.name, "file": str(path.relative_to(ROOT)),
                         "sha256": hash_file(path), "size": list(original.size),
                         "colorspace": original.colorspace_settings.name})
    shader = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    constants = {"name": material.name, "source_material": source.name,
                 "base_color_texture": textures[0]["file"] if textures else None,
                 "base_color_sha256": textures[0]["sha256"] if textures else None,
                 "base_color_multiplier": [1, 1, 1, 1] if textures else list(source.diffuse_color),
                 "normal_map": None, "roughness_map": None, "height_map": None,
                 "note": "Original baked color only. No invented PBR maps or guessed scanning channels."}
    if shader:
        constants["metallic"] = shader.inputs["Metallic"].default_value
        constants["roughness"] = shader.inputs["Roughness"].default_value
        constants["smoothness"] = 1 - constants["roughness"]
    return material, textures, constants


def capture_export_expectations(objects):
    """Capture world-space geometry and face-corner UVs for independent FBX round trip."""
    expected = {}
    for obj in objects:
        points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        uv = obj.data.uv_layers.active
        loops = [[list(points[loop.vertex_index]), list(uv.data[loop.index].uv)]
                 for loop in obj.data.loops] if uv else []
        expected[obj.name] = {"points": points, "loops": loops}
    return expected


def verify_export_roundtrip(fbx, expected):
    """Re-import the FBX in a fresh background scene and measure actual geometry/UV errors."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=False)
    max_vertex_error, max_uv_error, uv_samples = 0.0, 0.0, 0
    for name, source in expected.items():
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            raise AssertionError(f"FBX round trip lost component: {name}")
        points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        for first, second in ((source["points"], points), (points, source["points"])):
            tree = KDTree(len(second))
            for index, point in enumerate(second):
                tree.insert(point, index)
            tree.balance()
            max_vertex_error = max(max_vertex_error, max(tree.find(point)[2] for point in first))
        if source["loops"]:
            tree = KDTree(len(source["loops"]))
            for index, (point, _) in enumerate(source["loops"]):
                tree.insert(Vector(point), index)
            tree.balance()
            uv = obj.data.uv_layers.active
            if uv is None:
                raise AssertionError(f"FBX round trip lost UV layer: {name}")
            for loop in obj.data.loops:
                candidates = tree.find_range(points[loop.vertex_index], .0001)
                if not candidates:
                    raise AssertionError(f"FBX loop position diverged: {name}")
                error = min((Vector(source["loops"][index][1]) - uv.data[loop.index].uv).length
                            for _, index, _ in candidates)
                max_uv_error = max(max_uv_error, error)
                uv_samples += 1
    if max_vertex_error > .0001 or max_uv_error > .00001:
        raise AssertionError(f"FBX round trip deformed geometry/UV: {max_vertex_error}, {max_uv_error}")
    return {"components": len(expected), "maximum_vertex_error_m": max_vertex_error,
            "maximum_uv_error": max_uv_error, "uv_loop_samples": uv_samples,
            "passed": True}


def read_fbx_global_settings(path):
    """Read actual binary FBX units/axes instead of assuming exporter defaults."""
    from io_scene_fbx import parse_fbx
    document, _ = parse_fbx.parse(str(path))
    settings = next(item for item in document.elems if item.id == b"GlobalSettings")
    properties = next(item for item in settings.elems if item.id == b"Properties70")
    return {item.props[0].decode(): item.props[4] for item in properties.elems
            if item.props[0].decode() in {"UnitScaleFactor", "OriginalUnitScaleFactor",
                "UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign"}}


def export_assembly(records):
    """Export one registered hierarchy containing every unchanged wall/pier component."""
    OUTPUT.mkdir(parents=True, exist_ok=True)
    scene = bpy.data.scenes.new("Temporary city export")
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    bpy.context.window.scene = scene
    root = bpy.data.objects.new("FortifiedCityWalls", None)
    scene.collection.objects.link(root)
    root.rotation_euler.z = math.pi
    material, textures, constants = copy_export_material(records[0]["source"].data.materials[0], OUTPUT)
    objects = []
    for record in records:
        # Source has already been saved and verified. Rename only the newly added
        # display instances in this transient process so exported component names
        # are stable rather than receiving Blender's .001 suffix.
        display = bpy.data.objects.get(record["name"])
        if display is not None:
            display.name = "TransientDisplay__" + record["name"]
        # Copying a mesh datablock is exact, not a geometry edit. This lets temporary
        # export materials use valid files without changing source mesh slots.
        mesh = record["source"].data.copy()
        mesh.materials.clear()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(record["name"], mesh)
        scene.collection.objects.link(obj)
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_basis = Matrix.Scale(SOURCE_SCALE, 4) @ record["matrix"]
        objects.append(obj)
    for name, location in (("PortalNorth", (0, records[0]["portal_y"] * SOURCE_SCALE, 0)),
                           ("EastReference", (10, 0, 0)), ("GroundOrigin", (0, 0, 0))):
        anchor = bpy.data.objects.new(name, None)
        scene.collection.objects.link(anchor)
        anchor.parent = root
        anchor.location = location
    bpy.context.view_layer.update()
    expected = capture_export_expectations(objects)
    for obj in scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    fbx = OUTPUT / "FortifiedCityWalls.fbx"
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True,
        object_types={"MESH", "EMPTY"}, use_mesh_modifiers=False,
        add_leaf_bones=False, bake_anim=False, global_scale=1,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=False)
    return {"file": str(fbx.relative_to(ROOT)), "sha256": hash_file(fbx),
            "textures": textures, "materials": [constants],
            "fbx_global_settings_verified": read_fbx_global_settings(fbx),
            "roundtrip": verify_export_roundtrip(fbx, expected)}


def read_unity_layout_bounds(validation, components):
    """Report actual asymmetric exterior relief and conservative city floor rectangle."""
    low, high = [Vector(validation[key]) for key in ("bounds_min", "bounds_max")]
    center, size = (low + high) * .5, high - low
    exterior = {"min": [low.x, low.z, low.y], "max": [high.x, high.z, high.y],
                "center": [center.x, center.z, center.y], "size": [size.x, size.z, size.y]}
    interior = {}
    for side, axis, sign in (("West", 0, 1), ("East", 0, -1),
                             ("South", 2, 1), ("North", 2, -1)):
        candidates = [item["expected_unity_bounds_center_m"][axis] + sign * item["expected_unity_bounds_size_m"][axis] * .5
                      for item in components if item["name"].startswith(f"CityWall_{side}_Body")]
        key = ("min_" if sign == 1 else "max_") + ("x" if axis == 0 else "z")
        interior[key] = max(candidates) if sign == 1 else min(candidates)
    interior["note"] = "Conservative rectangle bounded by unchanged body AABBs; leave building setback."
    return exterior, interior


def build_assembly():
    """Thin orchestration: protect, append source, freeze, export, and write handoff."""
    sources, records, dimensions, validation = run_audit()
    if validation["radial_ray_misses"] or validation["portal_unexpected_hits"]:
        raise AssertionError("City ring has an unsealed radial boundary or obstructed portal")
    protected = capture_protected_state()
    (REVIEW / "protected-state.json").write_text(json.dumps(protected, ensure_ascii=False, indent=2))
    source_signatures = {name: {"object": obj.name, "mesh": obj.data.name,
                                "geometry_uv_sha256": read_mesh_record(obj.data)}
                         for name, obj in sources.items()}
    component_records = [{"name": record["name"], "source_object": record["source"].name,
                          "source_mesh": record["source"].data.name,
                          "source_geometry_uv_sha256": read_mesh_record(record["source"].data),
                          "role": record["role"], "local_matrix_native": [list(row) for row in record["matrix"]],
                          "local_matrix_meters": [list(row) for row in Matrix.Scale(SOURCE_SCALE, 4) @ record["matrix"]]}
                         for record in records]
    for record, component in zip(records, component_records):
        points = read_record_points(record)
        low = Vector([min(point[axis] for point in points) for axis in range(3)]) * SOURCE_SCALE
        high = Vector([max(point[axis] for point in points) for axis in range(3)]) * SOURCE_SCALE
        center, size = (low + high) * .5, high - low
        component["expected_unity_bounds_center_m"] = [center.x, center.z, center.y]
        component["expected_unity_bounds_size_m"] = [size.x, size.z, size.y]
        component["local_yaw_unity_degrees"] = -math.degrees(record["matrix"].to_euler().z)
    source_result = save_protected_master(protected, records)
    # Re-resolve references after the mandatory candidate and master reopen.
    for record in records:
        record["source"] = bpy.data.objects[next(item["source_object"] for item in component_records
                                                if item["name"] == record["name"])]
        record["portal_y"] = dimensions["portal"]["center"][1]
    export = export_assembly(records)
    exterior, interior = read_unity_layout_bounds(validation, component_records)
    handoff = {"schema_version": 1, "status": "Source frozen; Unity import/visual review pending",
        "master_blend": str(MASTER.relative_to(ROOT)), "source_before_sha256": EXPECTED_SOURCE_SHA,
        **source_result, "sources": source_signatures, "components": component_records,
        "uniform_source_scale": SOURCE_SCALE, "export": export,
        "axis_units": {"blender_source": "Z up; native authored dimensions; display translation excluded",
                       "fbx": "Meters; UnitScaleFactor 100 (centimeters per meter); axis -Z forward/Y up",
                       "export_root_rotation_blender_z_degrees": 180,
                       "expected_unity_mapping": "Source native (x,y,z) -> Unity (18*x,18*z,18*y)",
                       "unity": "useFileScale=true, globalScale=1; verify PortalNorth is +Z, EastReference +X; no extra 0.01 scale"},
        "root_pivot": "Nominal perimeter centerline XY0, ground Z0; Unity place the whole root only. Actual asymmetric wall relief AABB center is recorded separately.",
        "whole_bounds_unity_m": exterior, "interior_clear_rectangle_unity_m": interior,
        "fbx_global_settings_verified": export["fbx_global_settings_verified"],
        "portal": {"status": "Open entrance; no authored gate leaf or closable gate",
                   "center_unity_m": [0, 0, dimensions["portal"]["center"][1] * SOURCE_SCALE],
                   "outward_direction_unity": [0, 0, 1],
                   "minimum_foot_clearance_m": dimensions["portal"]["clear_width"] * SOURCE_SCALE,
                   "full_recess_width_m": dimensions["body_pitch"] * SOURCE_SCALE,
                   "upper_profile": "Original pier tapers; opening widens above the feet; not a rectangular closable doorway"},
        "dimensions_native": dimensions, "validation": validation,
        "seam_check": {"method": "1440 radial directions at five body heights, except declared 12.24m portal recess",
                       "passed": not validation["radial_ray_misses"],
                       "unsealed_sample_rays": len(validation["radial_ray_misses"]),
                       "max_seam_gap_m": None,
                       "gap_metric_note": "No sampled open seam; finite ray probes are not an exact analytical maximum-gap measurement",
                       "aabb_diagnostic_note": "AABB boundary rays include empty curved-corner outline and tapered portal recess; retained as diagnostics, not hidden or called failed contact rays"},
        "saved_source_verification": "Builds/ArtReview/FortifiedCity/saved-source-verification.json",
        "missing_sources": ["Authored closable stone/wood gate", "No solid tower invented or restored from old missing collection", "No long cliff module available; use Unity Terrain and existing real rock PBR"],
        "preservation_note": "All 394 actual preexisting objects and existing mesh/UV/material/image/action/light/camera/layout records preserved. Live unsaved Zombie category GUI never accessed."}
    HANDOFF.write_text(json.dumps(handoff, ensure_ascii=False, indent=2))
    print("CITY SOURCE FROZEN", json.dumps({"master_sha256": source_result["master_sha256"],
        "objects": source_result["objects_after"], "export": export["file"], "roundtrip": export["roundtrip"]}))


def verify_saved_assembly():
    """Recheck frozen saved source and export without adding, saving, or relocating art."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER:
        raise RuntimeError("Saved source verification requires background formal general master")
    handoff = json.loads(HANDOFF.read_text())
    if hash_file(MASTER) != handoff["master_sha256"]:
        raise AssertionError("Saved master hash no longer matches the frozen city handoff")
    protected = json.loads((REVIEW / "protected-state.json").read_text())
    counts = verify_protected_state(protected)
    records = []
    maximum_matrix_error = 0.0
    for component in handoff["components"]:
        obj = bpy.data.objects[component["name"]]
        matrix_error = max(abs(first - second) for row_a, row_b in zip(obj.matrix_basis, component["local_matrix_native"])
                           for first, second in zip(row_a, row_b))
        maximum_matrix_error = max(maximum_matrix_error, matrix_error)
        if matrix_error > .000001:
            raise AssertionError(f"Saved source component placement changed: {obj.name}")
        if read_mesh_record(obj.data) != component["source_geometry_uv_sha256"]:
            raise AssertionError(f"Saved city component source geometry changed: {obj.name}")
        records.append({"name": obj.name, "source": bpy.data.objects[component["source_object"]],
                        "matrix": obj.matrix_basis.copy(), "role": component["role"]})
    validation = inspect_assembly(records, handoff["dimensions_native"])
    if validation["radial_ray_misses"] or validation["portal_unexpected_hits"]:
        raise AssertionError("Saved city ring no longer seals around its declared open entrance")
    export = handoff["export"]
    if hash_file(ROOT / export["file"]) != export["sha256"]:
        raise AssertionError("City FBX differs from frozen handoff")
    for texture in export["textures"]:
        if hash_file(ROOT / texture["file"]) != texture["sha256"]:
            raise AssertionError(f"City exported texture differs: {texture['file']}")
    settings = read_fbx_global_settings(ROOT / export["file"])
    if settings["UnitScaleFactor"] != 100.0:
        raise AssertionError("Export unit metadata changed")
    report = {"passed": True, "protected_counts": counts,
              "master_sha256": handoff["master_sha256"], "components": len(records),
              "maximum_new_component_matrix_error_native": maximum_matrix_error,
              "new_component_matrix_tolerance_native": .000001,
              "radial_samples": validation["radial_ray_samples"], "radial_misses": 0,
              "all_component_feet_zero": all(abs(value) < .00001 for value in validation["ground_minima_m"].values()),
              "fbx_global_settings": settings}
    (REVIEW / "saved-source-verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print("SAVED CITY SOURCE PASS", json.dumps(report))
    if "--render" in sys.argv:
        render_inspection(records)


if __name__ == "__main__":
    if "--verify-saved" in sys.argv:
        verify_saved_assembly()
    elif "--build" in sys.argv:
        build_assembly()
    else:
        sources, records, dimensions, validation = run_audit()
        if "--render" in sys.argv:
            render_inspection(records)
