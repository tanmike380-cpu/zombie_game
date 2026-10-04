"""Make the approved tower's lower side walls vertical and join authored wall ends to them.

Only master gallery variants and derived battle meshes are edited. The user's raw
Tripo originals, central door, roof, cannon, UV coordinates and scene lights stay intact.
"""
import hashlib
import json
import math
import sys
from datetime import datetime
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

sys.path.insert(0, str(Path(__file__).parent))
from modular_stone_wall import clip_mesh

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame.blend"
REVIEW = ROOT / "Builds/ArtReview/ModularStoneWall"
GALLERY = "城墙评审 · 全部状态 00—16"
PLAYABLE = "城防实装 · 连续墙＋双炮塔"
SIDE_PLANE = .341
CONTACT_PLANE = .3325
SIDE_RELIEF = .02
BODY_TOP = .365


def mesh_signature(mesh):
    """Hash coordinates, topology and UVs to protect unrelated authored geometry."""
    payload = str([(tuple(vertex.co)) for vertex in mesh.vertices])
    payload += str([tuple(face.vertices) for face in mesh.polygons])
    payload += str([[tuple(loop.uv) for loop in layer.data] for layer in mesh.uv_layers])
    return hashlib.sha256(payload.encode()).hexdigest()


def capture_originals(scene):
    """Capture the user's root Collection, including newly imported zombies."""
    collection = bpy.data.collections["Collection"]
    return {obj.name: {"matrix": [list(row) for row in obj.matrix_world],
                       "geometry": mesh_signature(obj.data) if obj.type == "MESH" else None,
                       "data": obj.data.name if obj.data else None}
            for obj in collection.all_objects}


def verify_originals(scene, expected):
    if capture_originals(scene) != expected:
        raise AssertionError("A user's original model, rig or transform changed")


def rectify_lower_sides(source):
    """Flatten taper in side bands while retaining a tiny relief; central doorway is untouched."""
    mesh = source.copy()
    mesh.name = "CannonTower_RectangularLowerBody_Approved"
    changed = []
    protected = []
    for vertex in mesh.vertices:
        x, y, z = vertex.co
        if z >= BODY_TOP or abs(x) <= .24:
            protected.append((vertex.index, tuple(vertex.co)))
            continue
        weight = min(1, max(0, (abs(x) - .24) / .06))
        weight = weight * weight * (3 - 2 * weight)
        target = SIDE_PLANE + SIDE_RELIEF * (abs(x) - SIDE_PLANE)
        vertex.co.x = math.copysign(abs(x) * (1 - weight) + target * weight, x)
        changed.append(vertex.index)
    mesh.update()
    for index, position in protected:
        if tuple(mesh.vertices[index].co) != position:
            raise AssertionError("Door, roof or cannon protected vertex moved")
    assert len(mesh.vertices) == len(source.vertices)
    assert [[tuple(loop.uv) for loop in layer.data] for layer in mesh.uv_layers] == [
        [tuple(loop.uv) for loop in layer.data] for layer in source.uv_layers]
    mesh["rectangular_lower_body"] = True
    return mesh, len(changed), len(protected)


def verify_contact_surface(mesh):
    """Both wall ends penetrate the existing solid side surface, not its decoration bounds."""
    tree = BVHTree.FromPolygons([vertex.co for vertex in mesh.vertices],
                               [face.vertices for face in mesh.polygons])
    samples = []
    for z in (.005, .015, .025, .04, .07, .12, .18, .24, .30, .34, .36):
        for y in (-.101, -.10, -.05, 0, .05, .10, .12, .124):
            for sign in (-1, 1):
                hit, _, _, _ = tree.ray_cast((sign * 2, y, z), (-sign, 0, 0), 4)
                if hit is None or sign * hit.x < CONTACT_PLANE:
                    raise AssertionError(f"Unsealed tower side at y={y},z={z},side={sign}: {hit}")
                samples.append({"y": y, "z": z, "side": sign, "surface_x": hit.x,
                                "embed_depth_source": sign * hit.x - CONTACT_PLANE})
    return samples


def combine_meshes(parts, name):
    """Join source mesh copies without losing UV loops, smooth flags or source material slots."""
    vertices, faces, smoothing, material_indices = [], [], [], []
    uv_names = sorted({layer.name for mesh, _ in parts for layer in mesh.uv_layers})
    uv_values = {name: [] for name in uv_names}
    materials = []
    for mesh, transform in parts:
        offset = len(vertices)
        vertices.extend(transform @ vertex.co for vertex in mesh.vertices)
        slot_map = []
        for material in mesh.materials:
            if material not in materials:
                materials.append(material)
            slot_map.append(materials.index(material))
        for face in mesh.polygons:
            faces.append([index + offset for index in face.vertices])
            smoothing.append(face.use_smooth)
            material_indices.append(slot_map[face.material_index] if slot_map else 0)
            for uv_name in uv_names:
                layer = mesh.uv_layers.get(uv_name)
                uv_values[uv_name].extend(tuple(layer.data[index].uv) if layer else (0, 0)
                                          for index in face.loop_indices)
    result = bpy.data.meshes.new(name)
    result.from_pydata(vertices, [], faces)
    for material in materials:
        result.materials.append(material)
    for face, smooth, material_index in zip(result.polygons, smoothing, material_indices):
        face.use_smooth = smooth
        face.material_index = material_index
    for uv_name, values in uv_values.items():
        layer = result.uv_layers.new(name=uv_name)
        for loop, value in zip(layer.data, values):
            loop.uv = value
    result.update()
    return result


def close_side_contact_mesh(mesh, original):
    """Repair open Tripo side patches from inside using the original side-wall colour UVs."""
    original.calc_loop_triangles()
    triangles = [triangle for triangle in original.loop_triangles
                 if abs(original.polygons[triangle.polygon_index].normal.x) > .75
                 and abs(original.polygons[triangle.polygon_index].center.y) < .18
                 and original.polygons[triangle.polygon_index].center.z < BODY_TOP
                 and original.polygons[triangle.polygon_index].area > .002]
    tree = BVHTree.FromPolygons([vertex.co for vertex in original.vertices],
                               [triangle.vertices for triangle in triangles], all_triangles=True)
    vertices, faces, vertex_uvs = [], [], []
    uv_layer = original.uv_layers.active
    for sign in (-1, 1):
        offset = len(vertices)
        for row in range(17):
            z = .362 * row / 16
            for column in range(9):
                y = -.102 + .227 * column / 8
                vertices.append((sign * .340, y, z))
                point, _, index, _ = tree.find_nearest((sign * (.395 - .15 * z), y, z))
                triangle = triangles[index]
                coords = [original.vertices[i].co for i in triangle.vertices]
                uv_coords = [Vector((*uv_layer.data[i].uv, 0)) for i in triangle.loops]
                uv = barycentric_transform(point, *coords, *uv_coords)
                vertex_uvs.append((uv.x, uv.y))
        for row in range(16):
            for column in range(8):
                first = offset + row * 9 + column
                face = (first, first + 1, first + 10, first + 9)
                faces.append(face if sign > 0 else tuple(reversed(face)))
    patch = bpy.data.meshes.new("Tower_Side_Contact_Surface_Repair")
    patch.from_pydata(vertices, [], faces)
    for material in original.materials:
        patch.materials.append(material)
    layer = patch.uv_layers.new(name=uv_layer.name)
    for face in patch.polygons:
        for index in face.loop_indices:
            layer.data[index].uv = vertex_uvs[patch.loops[index].vertex_index]
    patch.update()
    result = combine_meshes([(mesh, Matrix.Identity(4)), (patch, Matrix.Identity(4))],
                            "CannonTower_RectangularLowerBody_Sealed")
    result["rectangular_lower_body"] = True
    return result


def create_extension(body, start, end, phase_origin, object_x):
    """Continue the existing brick repeat into the real tower shaft, never stretch its UVs."""
    pitch = max(vertex.co.x for vertex in body.vertices)
    cursor = start
    parts = []
    while cursor < end - 1e-7:
        tile = math.floor((cursor - phase_origin + 1e-7) / pitch)
        tile_start = phase_origin + tile * pitch
        stop = min(end, tile_start + pitch)
        mesh = clip_mesh(body, max(0, cursor - tile_start), stop - tile_start, "TowerJoin_Continuation")
        # The raw repeating wall has raised bases between feet. End continuations must
        # meet the tower's ground contact rather than expose a slit below a trimmed foot.
        for vertex in mesh.vertices:
            if vertex.co.z < .055:
                vertex.co.z = 0
        mesh.update()
        parts.append((mesh, Matrix.Translation((cursor - object_x, 0, 0))))
        cursor = stop
    return parts


def read_interval(obj):
    x = obj.matrix_basis.translation.x
    return x + min(vertex.co.x for vertex in obj.data.vertices), x + max(vertex.co.x for vertex in obj.data.vertices)


def extend_wall_contacts(collection, body, phase_origin):
    """Adjust authored wall ends in Blender, with the center passage left completely clear."""
    walls = [obj for obj in collection.objects if obj.type == "MESH" and obj.get("wall_module")]
    report = []
    for tower in [obj for obj in collection.objects if "tower_start_cell" in obj]:
        center = tower.matrix_basis.translation.x
        half_width = CONTACT_PLANE * tower.scale.x
        old_left, old_right = center - .5, center + .5
        left = max((obj for obj in walls if read_interval(obj)[1] <= old_left + 1e-5),
                   key=lambda obj: read_interval(obj)[1])
        right = min((obj for obj in walls if read_interval(obj)[0] >= old_right - 1e-5),
                    key=lambda obj: read_interval(obj)[0])
        for wall, start, end in ((left, old_left, center - half_width),
                                 (right, center + half_width, old_right)):
            parts = [(wall.data, Matrix.Identity(4))]
            parts.extend(create_extension(body, start, end, phase_origin, wall.matrix_basis.translation.x))
            wall.data = combine_meshes(parts, wall.data.name + "_RectTowerJoin")
            wall["rectangular_tower_contact"] = True
        tower["wall_join_half_width"] = half_width
        tower["rectangular_lower_body"] = True
        report.append({"tower": tower.name, "left_wall_end": center - half_width,
                       "right_wall_start": center + half_width, "doorway_width_source": half_width * 2})
    return report


def verify_joint_rays(collection):
    """Cast through both sides across every join; distinguish geometry from a dark baked texture."""
    vertices, faces = [], []
    for obj in collection.objects:
        if obj.type != "MESH" or not (obj.get("wall_module") or "tower_start_cell" in obj):
            continue
        offset = len(vertices)
        vertices.extend(obj.matrix_basis @ vertex.co for vertex in obj.data.vertices)
        faces.extend([index + offset for index in face.vertices] for face in obj.data.polygons)
    tree = BVHTree.FromPolygons(vertices, faces)
    count = 0
    for tower in [obj for obj in collection.objects if "tower_start_cell" in obj]:
        for sign in (-1, 1):
            joint = tower.matrix_basis.translation.x + sign * tower["wall_join_half_width"]
            for offset in (-.02, -.01, -.003, 0, .003, .01, .02):
                # The authored wall becomes crenellated above .30; its deliberate
                # notches are not leaks. Test the full continuous masonry below.
                for index in range(2, 31):
                    z = index * .01
                    for view_sign in (-1, 1):
                        hit, _, _, _ = tree.ray_cast((joint + offset, view_sign * 2, z), (0, -view_sign, 0), 4)
                        if hit is None:
                            raise AssertionError(f"Background-visible join hole: x={joint+offset},z={z},view={view_sign}")
                        count += 1
    return count


def rectify_master_variants():
    """Edit gallery variants only; raw user tower and all other original models stay immutable."""
    gallery = bpy.data.collections[GALLERY]
    towers = [obj for obj in gallery.all_objects if "tower_start_cell" in obj]
    if any(obj.get("rectangular_lower_body") for obj in towers):
        raise ValueError("Tower variants already rectified; refuse a second deformation")
    source = towers[0].data
    mesh, changed, protected = rectify_lower_sides(source)
    mesh = close_side_contact_mesh(mesh, source)
    samples = verify_contact_surface(mesh)
    for tower in towers:
        tower.data = mesh
    kit = bpy.data.collections["城墙 · 00_ModularWall_Kit"]
    body = next(obj.data for obj in kit.objects if obj.name.endswith(" · MiddleWall"))
    left = next(obj.data for obj in kit.objects if obj.name.endswith(" · StartPier"))
    phase_origin = max(vertex.co.x for vertex in left.vertices)
    contacts = []
    for collection in gallery.children:
        if any("tower_start_cell" in obj for obj in collection.objects):
            contacts.extend(extend_wall_contacts(collection, body, phase_origin))
    joint_rays = sum(verify_joint_rays(collection) for collection in gallery.children
                     if any("tower_start_cell" in obj for obj in collection.objects))
    return {"changed_tower_vertices": changed, "protected_tower_vertices": protected,
            "uv_unchanged": True, "contact_samples": samples, "contacts": contacts,
            "front_back_join_rays_without_holes": joint_rays}


def render_contact_views():
    """Render front/back/top of the edited review without saving cameras into the master."""
    original_scene = bpy.context.scene
    scene = bpy.data.scenes.new("Temporary tower contact QA")
    bpy.context.window.scene = scene
    for source in bpy.data.collections["城墙 · 10_Tower_Middle"].objects:
        if source.type != "MESH" or not (source.get("wall_module") or "tower_start_cell" in source):
            continue
        obj = source.copy()
        obj.parent = None
        obj.matrix_world = source.matrix_basis.copy()
        scene.collection.objects.link(obj)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = False
    scene.display.shading.show_cavity = False
    scene.display.shading.show_specular_highlight = False
    scene.display.shading.background_type = "VIEWPORT"
    scene.display.shading.background_color = (.13, .16, .19)
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1500, 850
    scene.render.resolution_percentage = 100
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 5.8
    scene.camera = camera
    for name, location in (("front", (5.6, -4, 2.7)), ("back", (0, 4, 2.7)), ("top", (2.5, -.001, 7)),
                           ("front-close", (3.2, -3, 1.2)), ("back-close", (1.8, 3, 1.2))):
        camera.data.ortho_scale = 1.8 if name.endswith("close") else 5.8
        camera.location = location
        camera.rotation_euler = (Vector((2.5, 0, .25)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(REVIEW / ("rectangular-tower-" + name + ".png"))
        bpy.ops.render.render(write_still=True)
    bpy.context.window.scene = original_scene
    bpy.data.scenes.remove(scene)


def refresh_playable_meshes():
    """Replace registered display meshes from the latest exports, preserving its gallery root."""
    assembly_path = ROOT / "art/models/modular-stone-wall/CoastalWallAssembly.blend"
    with bpy.data.libraries.load(str(assembly_path), link=False) as (available, loaded):
        loaded.objects = [name for name in available.objects if name.startswith("WallRun")]
    replacements = {obj["unity_asset"]: obj.data for obj in loaded.objects if obj.get("unity_asset")}
    updated = 0
    for obj in bpy.data.collections[PLAYABLE].objects:
        identity = obj.get("unity_asset")
        if identity:
            obj.data = replacements[identity]
            updated += 1
    if updated != 27:
        raise AssertionError(f"Expected 27 playable display parts, got {updated}")
    return updated


def prepare_master_view():
    """Open the saved master on the actual playable exhibit, not an opaque Python console."""
    bpy.context.view_layer.update()
    objects = [obj for obj in bpy.data.collections[PLAYABLE].objects if obj.type == "MESH"]
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    center = (low + high) * .5
    for screen in [bpy.context.screen]:
        candidates = [area for area in screen.areas if area.type in {"CONSOLE", "VIEW_3D"}]
        if not candidates:
            continue
        area = max(candidates, key=lambda item: item.width * item.height)
        area.type = "VIEW_3D"
        space = area.spaces.active
        space.region_3d.view_location = center
        space.region_3d.view_distance = 10
        space.region_3d.view_rotation = Vector((1, -1.5, 1.3)).to_track_quat("Z", "Y")
        space.region_3d.view_perspective = "ORTHO"
        space.shading.type = "SOLID"
        space.shading.light = "FLAT"
        space.shading.color_type = "TEXTURE"


def main():
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER.resolve():
        raise ValueError("Requires a background copy of the verified formal master")
    scene = bpy.context.scene
    original = capture_originals(scene)
    if "--refresh-playable" in sys.argv:
        count = refresh_playable_meshes()
        verify_originals(scene, original)
        prepare_master_view()
        bpy.ops.wm.save_as_mainfile(filepath=str(MASTER), compress=True)
        print(f"PLAYABLE RECTANGULAR TOWER REFRESH PASS: {count} parts; original models unchanged")
        return
    report = rectify_master_variants()
    bpy.context.view_layer.update()
    verify_originals(scene, original)
    report["original_objects_unchanged"] = len(original)
    if "--save" in sys.argv:
        import shutil
        backup = REVIEW / ("master-before-rectangular-tower-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".blend")
        shutil.copy2(MASTER, backup)
        report["backup"] = str(backup)
        bpy.ops.wm.save_as_mainfile(filepath=str(MASTER), compress=True)
    render_contact_views()
    (REVIEW / "rectangular-tower-verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print(f"RECTANGULAR TOWER PASS: {report['changed_tower_vertices']} edited vertices; {len(report['contact_samples'])} sealed side samples; originals unchanged")


if __name__ == "__main__":
    main()
