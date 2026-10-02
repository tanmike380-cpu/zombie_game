"""Review the imported tower passage and straw pasture without changing the live scene."""
import hashlib
import json
import math
import sys
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from gate_passage import GatePassage, verify_passage
from pasture_materials import color_pasture, add_straw_cover, bake_base_color
from siege_preview import create_stage, set_preview_workspace, create_material, add_fixture_cube
from tripo_model_io import point_at

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/GateTowerPasture"
MODELS = ROOT / "art/models/gate-tower-pasture"
FRAME_END = 144


def copy_mesh(source, name):
    """Copy local model geometry without editing source topology or materials."""
    obj = bpy.data.objects.new(name, source.data.copy())
    bpy.context.scene.collection.objects.link(obj)
    return obj


def make_unlit(material, opacity=1.):
    """Use original base colour under an unlit transparent shader; keep source materials intact."""
    result = material.copy()
    result.name = material.name+" — unlit review"
    result.use_nodes = True
    nodes, links = result.node_tree.nodes, result.node_tree.links
    output = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
    shader = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
    emission = nodes.new("ShaderNodeEmission")
    if shader:
        color_input = shader.inputs["Base Color"]
        if color_input.is_linked:
            links.new(color_input.links[0].from_socket, emission.inputs["Color"])
        else:
            emission.inputs["Color"].default_value = color_input.default_value
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    mix.inputs[0].default_value = 1-opacity
    links.new(emission.outputs[0], mix.inputs[1])
    links.new(transparent.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], output.inputs["Surface"])
    result.surface_render_method = "DITHERED"
    return result, mix.inputs[0]


def setup_unlit_stage(scene, target, camera_location, scale):
    """Retain baked colour presentation, adding no light objects."""
    create_stage(scene, "GreekFire")
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 8
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 960, 720
    scene.camera.location = camera_location
    scene.camera.data.ortho_scale = scale
    point_at(scene.camera, target)
    scene.world = bpy.data.worlds.new(f"{scene.name} unlit background")
    scene.world.use_nodes = True
    next(node for node in scene.world.node_tree.nodes if node.type == "BACKGROUND").inputs["Color"].default_value = (.09,.115,.14,1)
    floor = next(obj for obj in scene.objects if obj.get("review_fixture"))
    floor.data.materials[0] = make_unlit(floor.data.materials[0])[0]


def add_actor(source_path, name, height, action_name=None):
    """Append an existing authored character and its original rig; do not rebuild bones."""
    with bpy.data.libraries.load(str(source_path)) as (available, loaded):
        loaded.objects = list(available.objects)
        loaded.actions = [entry for entry in available.actions if entry == action_name]
    rig = next(obj for obj in loaded.objects if obj.type == "ARMATURE")
    meshes = [obj for obj in loaded.objects if obj.type == "MESH" and
              any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers)]
    bpy.context.scene.collection.objects.link(rig)
    for obj in meshes:
        bpy.context.scene.collection.objects.link(obj)
        for index, material in enumerate(obj.data.materials):
            obj.data.materials[index] = make_unlit(material)[0]
    rig.animation_data_clear()
    rig.location = (0,0,0)
    rig.rotation_euler = (0,0,0)
    rig.scale = (1,1,1)
    if loaded.actions:
        track = rig.animation_data_create().nla_tracks.new()
        strip = track.strips.new("Existing walk cycle", 1, loaded.actions[0])
        strip.repeat = 8
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    low, high = min(p.z for p in points), max(p.z for p in points)
    factor = height/(high-low)
    rig.scale = (factor,)*3
    rig.location.z = -low*factor
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    rig.parent = root
    root["review_fixture"] = True
    return root, meshes


def add_outline(mesh):
    """Create an expanded backface shell on the same rig for a readable cyan silhouette."""
    shell = mesh.copy()
    shell.data = mesh.data.copy()
    shell.name = "Friendly cyan silhouette — review"
    bpy.context.scene.collection.objects.link(shell)
    for vertex in shell.data.vertices:
        vertex.co += vertex.normal*.008
    material = bpy.data.materials.new("Friendly silhouette cyan")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    output = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (.03,.72,1,1)
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    geometry = nodes.new("ShaderNodeNewGeometry")
    mix = nodes.new("ShaderNodeMixShader")
    links.new(geometry.outputs["Backfacing"], mix.inputs[0])
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(emission.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], output.inputs["Surface"])
    shell.data.materials.clear()
    shell.data.materials.append(material)
    return shell


def animate_passage(scene, tower, friend, enemy, outlines):
    """Bake the geometric permission fixture, not hard-coded enemy-through-tower movement."""
    policy = GatePassage()
    friend_xy, enemy_xy = (0.,-2.), (.62,-2.)
    opacity_socket = make_unlit(tower.data.materials[0])
    tower.data.materials[0], opacity = opacity_socket
    records = []
    for frame in range(1, FRAME_END+1):
        scene.frame_set(frame)
        direction = 1 if frame < 77 else -1
        friend_xy = policy.advance("friendly", friend_xy, (0.,friend_xy[1]+direction*.055), .14)
        enemy_xy = policy.advance("zombie", enemy_xy, (.62,enemy_xy[1]+.045), .14)
        friend.location = (*friend_xy, 0)
        friend.rotation_euler.z = math.pi if direction == 1 else 0
        enemy.location = (*enemy_xy, 0)
        friend.keyframe_insert("location", frame=frame)
        friend.keyframe_insert("rotation_euler", frame=frame)
        enemy.keyframe_insert("location", frame=frame)
        inside = abs(friend_xy[1]) <= 1.15
        opacity.default_value = .80 if inside else 0
        opacity.keyframe_insert("default_value", frame=frame)
        for shell in outlines:
            shell.hide_render = not inside
            shell.hide_viewport = not inside
            shell.keyframe_insert("hide_render", frame=frame)
            shell.keyframe_insert("hide_viewport", frame=frame)
        assert enemy_xy[1] < -1, "Enemy entered the footprint"
        records.append({"frame": frame, "friendly": friend_xy, "enemy": enemy_xy, "ghost": inside})
    assert max(row["friendly"][1] for row in records) > 2
    assert records[-1]["friendly"][1] < -1
    scene.frame_start, scene.frame_end = 1, FRAME_END
    return records


def build_tower_scene(source):
    """Author 2x2 footprint, original tower and reversible friendly crossing review."""
    scene = bpy.data.scenes.new("01_Tower_FriendlyPassage_2x2")
    bpy.context.window.scene = scene
    tower = copy_mesh(source, "Gate tower — original Tripo appearance")
    width = max(v.co.x for v in tower.data.vertices)-min(v.co.x for v in tower.data.vertices)
    tower.scale = (2/width,)*3
    tower["footprint_cells"] = [2,2]
    setup_unlit_stage(scene, (0,0,.5), (3.2,-4.8,4), 5.2)
    grid = make_unlit(create_material("Grid footprint", (.22,.39,.46)))[0]
    for x in (-1,0,1):
        add_fixture_cube("Cell boundary", (x,0,.002), (.012,2,.003), grid)
    for y in (-1,0,1):
        add_fixture_cube("Cell boundary", (0,y,.002), (2,.012,.003), grid)
    friend, meshes = add_actor(ROOT / "art/models/tripo-crossbow/ByzantineCrossbow.blend", "Friendly passage fixture", .68, "Run")
    outlines = [add_outline(obj) for obj in meshes]
    enemy, _ = add_actor(ROOT / "art/models/tripo-exploder/TripoExploder.blend", "Enemy blocked fixture", .72)
    records = animate_passage(scene, tower, friend, enemy, outlines)
    return scene, records


def build_pasture_scene(source):
    """Keep source topology, add editable material and separate continuous straw cover."""
    scene = bpy.data.scenes.new("02_Pasture_StrawMaterial")
    bpy.context.window.scene = scene
    obj = copy_mesh(source, "Pasture — original mesh with new palette")
    material = color_pasture(obj)
    straw = add_straw_cover(obj)
    setup_unlit_stage(scene, (0,0,.13), (1.5,-2,1.5), 1.42)
    bake_base_color(obj, material, MODELS / "Pasture_BaseColor.png")
    scene.cycles.samples = 8
    assert len(obj.data.vertices) == len(source.data.vertices)
    return scene, {"source_vertices": len(source.data.vertices),
                   "straw_triangles": sum(len(p.vertices)-2 for p in straw.data.polygons),
                   "source_geometry_unchanged": True, "new_texture_resolution": [1024,1024]}


def build_review():
    """Create local, inspectable Blender review assets; leave the live imported scene alone."""
    MODELS.mkdir(parents=True, exist_ok=True)
    tower_source = bpy.data.objects.get("stone fortress 3d model") or bpy.data.objects.get("Original — stone fortress 3d model")
    pasture_source = bpy.data.objects.get("medieval farmhouse 3d model") or bpy.data.objects.get("Original — medieval farmhouse 3d model")
    if tower_source is None or pasture_source is None:
        raise ValueError("Input must include the original imported tower and pasture meshes")
    source_hashes = {node.image.name: hashlib.sha256(node.image.packed_file.data).hexdigest()
                     for mat in tower_source.data.materials for node in mat.node_tree.nodes
                     if node.type == "TEX_IMAGE" and node.image and node.image.packed_file}
    tower_scene, records = build_tower_scene(tower_source)
    pasture_scene, pasture_report = build_pasture_scene(pasture_source)
    # Save original architecture in a separate, hidden review collection for recovery.
    archive = bpy.data.scenes.new("00_OriginalArchitecture_Preserved")
    bpy.context.window.scene = archive
    for source in (tower_source, pasture_source):
        copy_mesh(source, "Original — "+source.name.removeprefix("Original — "))
    for scene in list(bpy.data.scenes):
        if scene not in (archive,tower_scene,pasture_scene):
            bpy.data.scenes.remove(scene)
    for obj in list(bpy.data.objects):
        if not obj.users_scene:
            bpy.data.objects.remove(obj, do_unlink=True)
    for scene, frames, prefix in ((tower_scene,(1,40,76,113),"tower"),(pasture_scene,(1,),"pasture")):
        bpy.context.window.scene = scene
        for frame in frames:
            scene.frame_set(frame)
            scene.render.filepath = str(REVIEW / f"{prefix}-{frame:03d}.png")
            bpy.ops.render.render(write_still=True)
    report = {"scope": "Blender visual/geometric preview, NOT Unity runtime navigation",
              "tower_footprint": [2,2], "gate_policy": verify_passage(), "animation_frames": records,
              "tower_original_texture_hashes": source_hashes, "pasture": pasture_report,
              "lighting": "No new lights. Original tower baked colour retained. Pasture uses unlit colour only."}
    (REVIEW / "verification.json").write_text(json.dumps(report, indent=2))
    shutil.copyfile(Path(__file__).parent / "templates/gate_tower_review.html", REVIEW / "review.html")
    bpy.context.window.scene = tower_scene
    configure_workspace(tower_scene)
    bpy.ops.file.pack_all()
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "GateTowerPasture.blend"))
    print("PASS: tower crossing in both directions; enemy blocked all frames; pasture geometry retained", flush=True)


def configure_workspace(scene):
    """Open material preview so transparency is visible, unlike solid Workbench mode."""
    set_preview_workspace(scene)
    scene.frame_set(40)
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "MATERIAL"
                    space.shading.use_scene_lights = True
                    space.shading.use_scene_world = True


def render_video():
    """Render saved tower animation as frames for local review, without modifying authoring assets."""
    scene = bpy.data.scenes["01_Tower_FriendlyPassage_2x2"]
    bpy.context.window.scene = scene
    scene.render.resolution_percentage = 75
    scene.cycles.samples = 4
    scene.render.filepath = str(REVIEW / "frames/tower-")
    scene.render.image_settings.file_format = "PNG"
    scene.frame_step = 2
    bpy.ops.render.render(animation=True)


def verify_saved_review():
    """Check packed source textures, visible outline state and frame-by-frame exclusion."""
    report = json.loads((REVIEW / "verification.json").read_text())
    for name, digest in report["tower_original_texture_hashes"].items():
        image = bpy.data.images[name]
        assert hashlib.sha256(image.packed_file.data).hexdigest() == digest
    scene = bpy.data.scenes["01_Tower_FriendlyPassage_2x2"]
    bpy.context.window.scene = scene
    friend = next(o for o in scene.objects if o.name.startswith("Friendly passage fixture"))
    enemy = next(o for o in scene.objects if o.name.startswith("Enemy blocked fixture"))
    shells = [o for o in scene.objects if o.name.startswith("Friendly cyan silhouette")]
    tower = next(o for o in scene.objects if o.name.startswith("Gate tower —"))
    assert list(tower["footprint_cells"]) == [2,2]
    for row in report["animation_frames"]:
        scene.frame_set(row["frame"])
        assert abs(friend.location.y-row["friendly"][1]) < 1e-5
        assert enemy.location.y < -1
        assert all(shell.hide_render != row["ghost"] for shell in shells)
    for scene in bpy.data.scenes:
        assert not any(o.type == "LIGHT" for o in scene.objects)
    obj = bpy.data.objects["Pasture — original mesh with new palette"]
    original = bpy.data.objects["Original — medieval farmhouse 3d model"]
    assert [tuple(v.co) for v in obj.data.vertices] == [tuple(v.co) for v in original.data.vertices]
    assert bpy.data.images["Pasture_BaseColor_1024"].packed_file
    print(f"PASS: {verify_passage()['cases']} permission checks, {FRAME_END} frames, unchanged tower texture and pasture geometry", flush=True)


if __name__ == "__main__":
    if "--verify" in sys.argv:
        verify_saved_review()
    elif "--video" in sys.argv:
        render_video()
    else:
        build_review()
