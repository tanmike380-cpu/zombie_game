"""Author a modular wall kit from the user's Tripo mesh without touching source materials."""
import json
import hashlib
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from siege_preview import create_stage, create_material, add_fixture_cube, set_preview_workspace
from tripo_model_io import point_at
from wall_layout import plan_wall, verify_layout

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/ModularStoneWall"
MODELS = ROOT / "art/models/modular-stone-wall"
MODEL_UNITS_PER_TILE = .5
CUT_LEFT, CUT_RIGHT = -.34, .34


def clip_mesh(source_mesh, low, high, name):
    """Bisect copies, interpolating existing UVs; do not stretch vertices or repaint textures."""
    editable = bmesh.new()
    editable.from_mesh(source_mesh)
    for position, normal in ((low, (-1, 0, 0)), (high, (1, 0, 0))):
        result = bmesh.ops.bisect_plane(editable,
            geom=list(editable.verts)+list(editable.edges)+list(editable.faces),
            plane_co=(position, 0, 0), plane_no=normal, dist=1e-7, clear_outer=True)
        boundary = [edge for edge in result["geom_cut"]
                    if isinstance(edge, bmesh.types.BMEdge) and edge.is_boundary]
        if boundary:
            bmesh.ops.holes_fill(editable, edges=boundary, sides=0)
    for vertex in editable.verts:
        vertex.co.x -= low
    bmesh.ops.recalc_face_normals(editable, faces=list(editable.faces))
    mesh = bpy.data.meshes.new(name)
    editable.to_mesh(mesh)
    editable.free()
    for material in source_mesh.materials:
        mesh.materials.append(material)
    return mesh


def add_piece(scene, mesh, name, x):
    """Instance authored geometry at unit scale so every pier keeps the same shape."""
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.location.x = x
    obj["wall_module"] = True
    return obj


def build_run(scene, plan, parts, body_cache):
    """Use exactly two end piers, repeated middle geometry and a cropped final middle."""
    if not plan.valid:
        return []
    left, body, right = parts
    width = lambda mesh: max(vertex.co.x for vertex in mesh.vertices)
    length = plan.length*MODEL_UNITS_PER_TILE
    left_width, right_width, pitch = width(left), width(right), width(body)
    if length <= left_width+right_width:
        raise ValueError("Wall is too short for original intact piers")
    objects = [add_piece(scene, left, "Pier_Start", 0)]
    cursor, end = left_width, length-right_width
    while cursor < end-1e-6:
        remaining = min(pitch, end-cursor)
        key = round(remaining, 6)
        if key not in body_cache:
            body_cache[key] = body if abs(remaining-pitch) < 1e-6 else clip_mesh(body, 0, remaining, f"WallBody_Trim_{key}")
        objects.append(add_piece(scene, body_cache[key], f"Wall_Middle_{len(objects):02d}", cursor))
        cursor += remaining
    objects.append(add_piece(scene, right, "Pier_End", end))
    return objects


def create_wall_stage(scene, plan):
    """Show the two-tile integer corridor without changing imported lighting/materials."""
    create_stage(scene, "GreekFire")
    scene.render.resolution_x, scene.render.resolution_y = 1400, 800
    scene.render.resolution_percentage = 100
    length = plan.requested_length*MODEL_UNITS_PER_TILE
    grid_material = create_material("Wall review grid", (.26, .30, .33))
    for index in range(int(plan.requested_length)+1):
        add_fixture_cube("Tile boundary", (index*.5, 0, -.003), (.002, 1, .001), grid_material)
    for y in (-.5, 0, .5):
        add_fixture_cube("Reserved footprint depth", (length/2, y, -.003), (length, .002, .001), grid_material)
    scene.camera.location = (length*.85+1, -2.8, 1.8)
    point_at(scene.camera, (length/2, 0, .18))
    scene.camera.data.ortho_scale = max(1.9, length*1.02+1)


def export_kit(scene, parts):
    """Export unscaled reusable mesh modules, excluding stage fixtures."""
    bpy.context.window.scene = scene
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.export_scene.fbx(filepath=str(MODELS / "StoneWallModules.fbx"), use_selection=True,
        object_types={"MESH"}, bake_anim=False, path_mode="COPY", embed_textures=True,
        axis_forward="-Z", axis_up="Y")


def build_wall_kit():
    """Isolate the original wall, then author connected examples and inspectable modules."""
    REVIEW.mkdir(parents=True, exist_ok=True)
    MODELS.mkdir(parents=True, exist_ok=True)
    source = bpy.data.objects.get("stone wall") or bpy.data.objects.get("Original stone wall — preserved")
    if source is None:
        raise ValueError("Source scene must contain the imported 'stone wall' mesh")
    master = source.data.copy()
    master.name = "Original_Tripo_StoneWall"
    texture_hashes = {node.image.name: hashlib.sha256(node.image.packed_file.data).hexdigest()
                      for material in master.materials if material.use_nodes
                      for node in material.node_tree.nodes
                      if node.type == "TEX_IMAGE" and node.image and node.image.packed_file}
    low = min(vertex.co.x for vertex in master.vertices)
    high = max(vertex.co.x for vertex in master.vertices)
    parts = (clip_mesh(master, low, CUT_LEFT, "StonePier_Left"),
             clip_mesh(master, CUT_LEFT, CUT_RIGHT, "StoneWall_Middle"),
             clip_mesh(master, CUT_RIGHT, high, "StonePier_Right"))
    # Isolate assets only in this background process. Never save over the user's source scene.
    kit_scene = bpy.data.scenes.new("00_ModularWall_Kit")
    bpy.context.window.scene = kit_scene
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for scene in list(bpy.data.scenes):
        if scene != kit_scene:
            bpy.data.scenes.remove(scene)
    original = bpy.data.objects.new("Original stone wall — preserved", master)
    kit_scene.collection.objects.link(original)
    original.hide_render = True
    original.hide_set(True)
    kit_objects = [add_piece(kit_scene, mesh, name, index*.9)
                   for index, (mesh, name) in enumerate(zip(parts, ("StartPier", "MiddleWall", "EndPier")))]
    create_wall_stage(kit_scene, plan_wall(6))
    export_kit(kit_scene, kit_objects)
    examples = [
        ("01_Short_2x2", plan_wall(2)), ("02_Continuous_2x3", plan_wall(3)),
        ("03_Long_2x6", plan_wall(6)), ("04_Integer_2x7", plan_wall(7)),
        ("05_Forest_Stop", plan_wall(8, obstacles=((6, 0, 7, 1),))),
        ("06_Map_Edge", plan_wall(8, map_end=7)),
        ("07_NotEnoughSpace", plan_wall(8, obstacles=((1, 0, 2, 1),))),
        ("08_Integer_2x4", plan_wall(4)), ("09_Integer_2x5", plan_wall(5)),
    ]
    report = {"footprint": "Confirmed: depth 2 cells, integer lengths >=2; no fractional placement",
              "source": "user's stone wall mesh from 60728_autosave.blend; no material edits",
              "integration": "Blender art / footprint preview only; Unity drag placement not implemented",
              "layout_tests": verify_layout(), "source_texture_hashes": texture_hashes, "examples": {}}
    cache = {}
    for name, plan in examples:
        scene = bpy.data.scenes.new(name)
        bpy.context.window.scene = scene
        objects = build_run(scene, plan, parts, cache)
        create_wall_stage(scene, plan)
        if not plan.valid:
            material = create_material("Invalid footprint", (.65, .12, .10))
            add_fixture_cube("Cannot fit intact piers", (.25, 0, .004), (.5, 1, .008), material)
        elif "Forest" in name:
            material = create_material("Obstacle footprint placeholder", (.16, .32, .19))
            add_fixture_cube("Forest footprint — one cell", (3.25, .25, .18), (.5, .5, .36), material)
        elif "Map_Edge" in name:
            material = create_material("Map boundary", (.62, .18, .12))
            add_fixture_cube("Map edge limit", (3.5, 0, .004), (.008, 1.3, .008), material)
        bpy.context.view_layer.update()
        if plan.valid:
            assert sum(obj.name.startswith("Pier_") for obj in objects) == 2
            assert objects[0].data == parts[0] and objects[-1].data == parts[2]
            assert all(tuple(obj.scale) == (1, 1, 1) for obj in objects)
            for previous, following in zip(objects, objects[1:]):
                end = previous.location.x + max(vertex.co.x for vertex in previous.data.vertices)
                start = following.location.x + min(vertex.co.x for vertex in following.data.vertices)
                assert abs(end-start) < 1e-5, f"Unjoined wall seam: {name}"
        scene.render.filepath = str(REVIEW / f"{name}.png")
        bpy.ops.render.render(write_still=True)
        report["examples"][name] = {**plan.to_dict(), "piers": 2 if plan.valid else 0,
                                   "body_pieces": max(0, len(objects)-2), "pier_scale_unchanged": True}
    bpy.context.window.scene = bpy.data.scenes["03_Long_2x6"]
    set_preview_workspace(bpy.context.scene)
    bpy.ops.file.pack_all()
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    for mesh, name in zip(parts, ("StonePier_Left", "StoneWall_Middle", "StonePier_Right")):
        mesh.name = name
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "ModularStoneWall.blend"))
    (REVIEW / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print("Wall kit ready: preserved piers, integer lengths and exact cell-boundary stops", flush=True)


def verify_saved_kit():
    """Re-opened-file checks for intact pier instances, texture bytes, seams and invalid space."""
    report = json.loads((REVIEW / "verification.json").read_text())
    for name, digest in report["source_texture_hashes"].items():
        image = bpy.data.images.get(name)
        assert image and image.packed_file, f"Source texture missing: {name}"
        assert hashlib.sha256(image.packed_file.data).hexdigest() == digest, f"Texture changed: {name}"
    for name, result in report["examples"].items():
        assert all(isinstance(result[key], int) for key in ("requested_length", "length", "depth"))
        scene = bpy.data.scenes[name]
        bpy.context.window.scene = scene
        modules = sorted((obj for obj in scene.objects if obj.get("wall_module")), key=lambda obj: obj.location.x)
        assert not any(obj.type == "LIGHT" for obj in scene.objects), "Unexpected new lighting"
        assert len(modules) == result["piers"]+result["body_pieces"]
        if not result["valid"]:
            assert not modules, "Placed wall inside insufficient space"
            continue
        assert modules[0].data.name == "StonePier_Left" and modules[-1].data.name == "StonePier_Right"
        for obj in modules:
            assert tuple(obj.scale) == (1, 1, 1), "A pier or texture was stretched"
        for first, second in zip(modules, modules[1:]):
            end = first.location.x+max(vertex.co.x for vertex in first.data.vertices)
            start = second.location.x+min(vertex.co.x for vertex in second.data.vertices)
            assert abs(end-start) < 1e-5, "Open longitudinal seam"
        end = modules[-1].location.x+max(vertex.co.x for vertex in modules[-1].data.vertices)
        assert abs(end-result["length"]*MODEL_UNITS_PER_TILE) < 1e-5, "Endpoint exceeds approved space"
    assert not any("Fractional" in scene.name for scene in bpy.data.scenes)
    print(f"PASS: {len(report['examples'])} wall cases, {verify_layout()['cases']} layout checks, original textures and invariant piers", flush=True)


def inspect_wall():
    """Show the exact imported wall in an isolated flat-textured scene, leaving source intact."""
    REVIEW.mkdir(parents=True, exist_ok=True)
    source = bpy.data.objects.get("stone wall") or bpy.data.objects.get("Original stone wall — preserved")
    if source is None or source.type != "MESH":
        raise ValueError("Expected the user's mesh named 'stone wall'")
    scene = bpy.data.scenes.new("Wall inspection")
    bpy.context.window.scene = scene
    wall = source.copy()
    wall.data = source.data.copy()
    scene.collection.objects.link(wall)
    wall.location = (0, 0, 0)
    bpy.context.view_layer.update()
    points = [wall.matrix_world @ vertex.co for vertex in wall.data.vertices]
    low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    wall.location -= Vector(((low.x+high.x)/2, (low.y+high.y)/2, low.z))
    bpy.context.view_layer.update()
    create_stage(scene, "GreekFire")
    scene.camera.location = (1.1, -1.6, 1)
    scene.camera.data.ortho_scale = 1.4
    point_at(scene.camera, (0, 0, .2))
    scene.render.filepath = str(REVIEW / "source-wall.png")
    bpy.ops.render.render(write_still=True)
    record = {"name": source.name, "bounds": [list(low), list(high)],
              "matrix": [list(row) for row in source.matrix_world],
              "vertices": len(source.data.vertices), "materials": [m.name for m in source.data.materials]}
    (REVIEW / "source-audit.json").write_text(json.dumps(record, indent=2))
    print(json.dumps(record), flush=True)


if __name__ == "__main__":
    if "--verify" in sys.argv:
        verify_saved_kit()
    elif "--build" in sys.argv:
        build_wall_kit()
    else:
        inspect_wall()
