"""Read-only source geometry/material audit and temporary diagnostic renders.

This script never saves a blend or exports an asset. All output is isolated
diagnostic evidence; the live unsaved Zombie document is never contacted.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).parent))
from fortified_city_assembly import capture_protected_state, verify_protected_state

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame.blend"
SOURCE_SHA = "34ce14d5150a0ae7a9e232775f6f7863180052feea29be4fabc8faf1ad097ef9"
OUTPUT = ROOT / "docs/versions/V0.10/evidence/render-source"
SPECS = [
    ("headquarters", "Headquarters12_OriginalArchitecture", None),
    ("workshop", "Workshop8_OriginalArchitecture", None),
    ("wall", "00_ModularWall_Kit · MiddleWall", 18),
    ("pier", "00_ModularWall_Kit · StartPier", 18),
]


def hash_file(path):
    """Return the source file digest without modifying it."""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_metric_matrix(obj, scale):
    """Return geometry's meter transform, excluding master gallery placement."""
    return Matrix.Scale(scale, 4) if scale else obj.matrix_basis.copy()


def summarize_normals(mesh):
    """Measure face smoothing, geometric dihedrals and corner-normal continuity."""
    adjacent = {}
    for face in mesh.polygons:
        for loop_index in face.loop_indices:
            edge_index = mesh.loops[loop_index].edge_index
            adjacent.setdefault(edge_index, []).append(face.index)
    independent_normals = recompute_face_normals(mesh)
    normal_errors = [math.degrees(face.normal.angle(independent_normals[face.index]))
                     for face in mesh.polygons if independent_normals[face.index].length > 0]
    angles, smooth_hard, continuous_hard = [], 0, 0
    corner_vectors = [normal.vector.copy() for normal in mesh.corner_normals]
    for edge_index, face_indices in adjacent.items():
        if len(face_indices) != 2:
            continue
        first, second = (mesh.polygons[index] for index in face_indices)
        angle = math.degrees(first.normal.angle(second.normal))
        angles.append(angle)
        if angle < 60:
            continue
        if first.use_smooth and second.use_smooth and not mesh.edges[edge_index].use_edge_sharp:
            smooth_hard += 1
        differences = []
        for vertex_index in mesh.edges[edge_index].vertices:
            pairs = [[corner_vectors[index] for index in face.loop_indices
                      if mesh.loops[index].vertex_index == vertex_index] for face in (first, second)]
            if pairs[0] and pairs[1]:
                differences.append(math.degrees(pairs[0][0].angle(pairs[1][0])))
        if differences and max(differences) < 5:
            continuous_hard += 1
    values = np.array(angles)
    return {"smooth_faces": sum(face.use_smooth for face in mesh.polygons),
            "face_count": len(mesh.polygons),
            "sharp_marked_edges": sum(edge.use_edge_sharp for edge in mesh.edges),
            "has_custom_normals": mesh.has_custom_normals,
            "adjacent_face_edges": len(angles),
            "dihedral_degrees_p50_p90_p99": np.percentile(values, [50, 90, 99]).tolist(),
            "geometric_edges_ge60deg": int(np.sum(values >= 60)),
            "geometric_edges_ge85deg": int(np.sum(values >= 85)),
            "ge60_both_smooth_without_sharp_mark": smooth_hard,
            "ge60_corner_normals_continuous_lt5deg": continuous_hard,
            "stored_vs_independent_newell_normal_error_deg_p50_p99_max":
                np.percentile(normal_errors, [50, 99, 100]).tolist(),
            "interpretation_limit": "Few sharp dihedrals can mean a rounded dense contour, not proof of no intended architectural corners."}


def recompute_face_normals(mesh):
    """Independently recompute polygon Newell normals to audit the dihedral definition."""
    results = []
    for face in mesh.polygons:
        points = [mesh.vertices[index].co for index in face.vertices]
        normal = Vector((0, 0, 0))
        for index, point in enumerate(points):
            normal += point.cross(points[(index+1) % len(points)])
        results.append(normal.normalized())
    return results


def measure_uv(mesh, matrix, resolution=2048):
    """Estimate per-triangle density and unique atlas coverage at 512-sample resolution."""
    mesh.calc_loop_triangles()
    layer = mesh.uv_layers.active
    if layer is None:
        return {"has_uv": False}
    coverage = np.zeros((512, 512), dtype=bool)
    densities, weighted, total_area, total_uv_area = [], [], 0, 0
    for triangle in mesh.loop_triangles:
        points = [matrix @ mesh.vertices[index].co for index in triangle.vertices]
        area = (points[1] - points[0]).cross(points[2] - points[0]).length / 2
        uvs = np.array([layer.data[index].uv[:] for index in triangle.loops])
        uv_area = abs(float(np.linalg.det(np.array([uvs[1]-uvs[0], uvs[2]-uvs[0]])))) / 2
        total_area += area
        total_uv_area += uv_area
        if area > 1e-10 and uv_area > 1e-12:
            densities.append(math.sqrt(uv_area * resolution**2 / area))
            weighted.append(area)
        rasterize_triangle(coverage, uvs)
    values, weights = np.array(densities), np.array(weighted)
    return {"has_uv": True, "atlas_resolution": [resolution, resolution],
            "uv_summed_triangle_area": total_uv_area,
            "unique_atlas_coverage_fraction_512_estimate": float(coverage.mean()),
            "geometry_surface_area_m2": total_area,
            "px_per_meter_triangle_p10_p50_p90": np.percentile(values, [10, 50, 90]).tolist(),
            "px_per_meter_area_weighted_mean": float(np.sum(values*weights)/weights.sum()),
            "limit": "Coverage estimate is unique occupancy, summed UV area may overlap. Density is actual 2048 atlas per geometry triangle, not a single flat facade's guaranteed detail."}


def rasterize_triangle(mask, uvs):
    """Mark UV triangle samples inside the 0-1 tile for atlas occupancy diagnostics."""
    low = np.maximum(0, np.floor(uvs.min(axis=0)*512).astype(int))
    high = np.minimum(511, np.ceil(uvs.max(axis=0)*512).astype(int))
    if np.any(high < low):
        return
    xs, ys = np.meshgrid(np.arange(low[0], high[0]+1), np.arange(low[1], high[1]+1))
    points = np.stack(((xs+.5)/512, (ys+.5)/512), axis=-1)
    signs = []
    for index in range(3):
        start, end = uvs[index], uvs[(index+1)%3]
        signs.append((points[..., 0]-start[0])*(end[1]-start[1])
                     -(points[..., 1]-start[1])*(end[0]-start[0]))
    inside = np.all(np.array(signs) >= -1e-10, axis=0) | np.all(np.array(signs) <= 1e-10, axis=0)
    mask[low[1]:high[1]+1, low[0]:high[0]+1] |= inside


def inspect_materials(obj):
    """Record actual connected textures; do not infer missing PBR maps from color."""
    results = []
    for material in obj.data.materials:
        nodes = material.node_tree.nodes if material.use_nodes else []
        textures = [{"image": node.image.name, "resolution": list(node.image.size),
                     "filepath": node.image.filepath, "packed": bool(node.image.packed_file),
                     "outputs_to": [link.to_node.type+":"+link.to_socket.name
                                    for output in node.outputs for link in output.links]}
                    for node in nodes if node.type == "TEX_IMAGE" and node.image]
        shaders = [node for node in nodes if node.type == "BSDF_PRINCIPLED"]
        channels = {name: {"linked": shader.inputs[name].is_linked,
                           "source_nodes": [link.from_node.type for link in shader.inputs[name].links]}
                    for shader in shaders for name in ("Base Color", "Normal", "Roughness", "Metallic")}
        normal_maps = [{"name": node.name, "strength": node.inputs["Strength"].default_value,
                        "color_linked": node.inputs["Color"].is_linked,
                        "color_default": list(node.inputs["Color"].default_value)}
                       for node in nodes if node.type == "NORMAL_MAP"]
        results.append({"name": material.name, "textures": textures, "shader_channels": channels,
                        "normal_map_nodes": normal_maps})
    return results


def inspect_asset(asset_id, object_name, scale):
    """Quantify one approved source without changing mesh, UV or normal data."""
    obj = bpy.data.objects[object_name]
    matrix = read_metric_matrix(obj, scale)
    points = [matrix @ vertex.co for vertex in obj.data.vertices]
    bounds = [[min(point[index] for point in points), max(point[index] for point in points)] for index in range(3)]
    return {"id": asset_id, "object": object_name, "mesh": obj.data.name,
            "bounds_blender_xyz_m": bounds, "vertices": len(obj.data.vertices),
            "triangles": sum(len(face.vertices)-2 for face in obj.data.polygons),
            "modifiers": [{"name": mod.name, "type": mod.type} for mod in obj.modifiers],
            "normals": summarize_normals(obj.data), "uv": measure_uv(obj.data, matrix),
            "materials": inspect_materials(obj),
            "front_marker": locate_front_marker(obj, matrix) if asset_id == "headquarters" else None}


def locate_front_marker(obj, matrix):
    """Identify a low central stair vertex by exact UV for Unity orientation verification."""
    target = Vector((0, -5.4, .3))
    vertex = min(obj.data.vertices, key=lambda item: ((matrix @ item.co)-target).length_squared)
    uvs = [list(obj.data.uv_layers.active.data[loop.index].uv)
           for loop in obj.data.loops if loop.vertex_index == vertex.index]
    return {"semantic": "Low center of the broad front stair, native negative Y",
            "mesh_vertex_index": vertex.index, "native_mesh_coordinate": list(vertex.co),
            "architecture_local_blender_xyz_m": list(matrix @ vertex.co),
            "exact_uv_locator": uvs,
            "unity_note": "Locate the matching UV vertex in runtime mesh, then TransformPoint to world; this resolves handedness/FBX root ambiguity without moving the asset."}


def create_diagnostic_material(mode):
    """Build an isolated override material, never modify an original material."""
    material = bpy.data.materials.new("Diagnostic only "+mode)
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
    if mode == "normal":
        geometry = nodes.new("ShaderNodeNewGeometry")
        mapping = nodes.new("ShaderNodeVectorMath")
        mapping.operation = "MULTIPLY_ADD"
        mapping.inputs[1].default_value = (.5, .5, .5)
        mapping.inputs[2].default_value = (.5, .5, .5)
        emission = nodes.new("ShaderNodeEmission")
        links.new(geometry.outputs["Normal"], mapping.inputs[0])
        links.new(mapping.outputs["Vector"], emission.inputs["Color"])
        links.new(emission.outputs[0], output.inputs["Surface"])
    else:
        shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
        shader.inputs["Base Color"].default_value = (.45, .45, .45, 1)
        shader.inputs["Roughness"].default_value = .65
    return material


def create_diagnostic_scene():
    """Create temporary lighting/camera in a separate unsaved scene."""
    scene = bpy.data.scenes.new("Read only source diagnostic")
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1200, 1000
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("Diagnostic only world")
    world.use_nodes = True
    background = next(node for node in world.node_tree.nodes if node.type == "BACKGROUND")
    background.inputs["Color"].default_value = (.35, .4, .48, 1)
    background.inputs["Strength"].default_value = .6
    scene.world = world
    sun = bpy.data.objects.new("Diagnostic only sun", bpy.data.lights.new("Diagnostic only sun", "SUN"))
    sun.rotation_euler, sun.data.energy = (.5, -.65, -.5), 3
    scene.collection.objects.link(sun)
    camera = bpy.data.objects.new("Diagnostic only camera", bpy.data.cameras.new("Diagnostic only camera"))
    camera.data.type, camera.data.ortho_scale = "ORTHO", 17
    scene.collection.objects.link(camera)
    scene.camera = camera
    return scene, camera


def render_asset_views(scene, camera, spec, materials):
    """Render real source silhouette and normal fields, without save or asset export."""
    asset_id, object_name, scale = spec
    source = bpy.data.objects[object_name]
    obj = source.copy()
    obj.parent = None
    obj.matrix_world = read_metric_matrix(source, scale)
    scene.collection.objects.link(obj)
    center = sum((obj.matrix_world @ Vector(corner) for corner in obj.bound_box), Vector()) / 8
    obj.location -= Vector((center.x, center.y, 0))
    focus = Vector((0, 0, center.z))
    for mode, direction in (("clay", (15, -22, 17)), ("normal", (15, -22, 17)),
                            ("textured", (15, -22, 17)), ("textured-rear", (-15, 22, 17))):
        if asset_id != "headquarters" and mode == "textured-rear":
            continue
        scene.view_layers[0].material_override = materials.get(mode)
        camera.location = focus + Vector(direction)
        camera.rotation_euler = (focus-camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(OUTPUT/f"{asset_id}-{mode}.png")
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(obj, do_unlink=True)


def render_controlled_normals(scene, camera, spec, material):
    """A temporary angle-split clone only, not flat shading or a production source fix."""
    asset_id, object_name, scale = spec
    source = bpy.data.objects[object_name]
    mesh = source.data.copy()
    mesh.normals_split_custom_set([(0, 0, 0)] * len(mesh.loops))
    mesh.set_sharp_from_angle(angle=math.radians(60))
    obj = bpy.data.objects.new("Diagnostic only angle split "+asset_id, mesh)
    obj.matrix_world = read_metric_matrix(source, scale)
    scene.collection.objects.link(obj)
    center = sum((obj.matrix_world @ Vector(corner) for corner in obj.bound_box), Vector()) / 8
    obj.location -= Vector((center.x, center.y, 0))
    focus = Vector((0, 0, center.z))
    camera.location = focus + Vector((15, -22, 17))
    camera.rotation_euler = (focus-camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.view_layers[0].material_override = material
    scene.render.filepath = str(OUTPUT/f"{asset_id}-clay-controlled-normals.png")
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.meshes.remove(mesh)


def run_diagnostic():
    """Read approved sources, write evidence only, then verify no source changes."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER or hash_file(MASTER) != SOURCE_SHA:
        raise RuntimeError("Read-only diagnostic requires the exact frozen general master in background")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    protected = capture_protected_state()
    results = [inspect_asset(*spec) for spec in SPECS]
    report = {"source_file": str(MASTER.relative_to(ROOT)), "source_sha256": SOURCE_SHA,
              "read_only": True, "assets": results,
              "hq_front": "Blender -Y: broad stair/arched entrance/columned portico. Unity front sign requires vertex marker check; do not infer from the FBX root's Z180 alone.",
              "limits": "No source concept-to-Tripo generation link exists in inspected handoffs; concept candidates must not be asserted as exact source. No automatic quality grade from area/face counts."}
    (OUTPUT/"source-metrics.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    if "--metrics-only" not in sys.argv:
        original_scene = bpy.context.window.scene
        scene, camera = create_diagnostic_scene()
        bpy.context.window.scene = scene
        materials = {mode: create_diagnostic_material(mode) for mode in ("clay", "normal")}
        for spec in SPECS:
            if "--controls-only" not in sys.argv:
                render_asset_views(scene, camera, spec, materials)
            if spec[0] in ("headquarters", "wall"):
                render_controlled_normals(scene, camera, spec, materials["clay"])
        bpy.context.window.scene = original_scene
    report["original_protected_state"] = verify_protected_state(protected)
    report["source_sha256_after"] = hash_file(MASTER)
    if report["source_sha256_after"] != SOURCE_SHA:
        raise AssertionError("Formal source changed during read-only diagnostic")
    report["no_source_or_exports_saved"] = True
    (OUTPUT/"source-metrics.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print("READ ONLY SOURCE DIAGNOSTICS PASS", json.dumps({"source": SOURCE_SHA, "assets": len(results)}))


if __name__ == "__main__":
    run_diagnostic()
