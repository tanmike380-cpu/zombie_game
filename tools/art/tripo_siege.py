"""Audit and author siege previews on the user's original Tripo import rigs."""
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from scene_grounding import collect_model_roots, collect_meshes, ground_models, measure_bounds
from tripo_model_io import audit_model, setup_render, point_at
from siege_motion import create_headbutt, create_greek_fire, assign_rigid_turret, remove_support_feet, measure_rig_signature, FRAME_END
from siege_preview import create_stage, create_flame_preview, set_preview_workspace, create_material, add_fixture_cube
from siege_locomotion import create_directional_movement, DIRECTIONS, MOVEMENT_END, SEGMENT_FRAMES

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Builds/ArtReview/TripoSiege"
MODELS = ROOT / "art/models/tripo-siege"
RIG_NAMES = ("Greek Fire siege", "头槌巨尸")


def duplicate_model(source, scene):
    """Copy a complete hierarchy for authoring; the grounded imported scene stays untouched."""
    mapping = {}
    for original in [source, *source.children_recursive]:
        duplicate = original.copy()
        if original.data:
            duplicate.data = original.data.copy()
        scene.collection.objects.link(duplicate)
        mapping[original] = duplicate
    for original, duplicate in mapping.items():
        duplicate.parent = mapping.get(original.parent)
        for modifier in duplicate.modifiers:
            if modifier.type == "ARMATURE" and modifier.object in mapping:
                modifier.object = mapping[modifier.object]
    root = mapping[source]
    root.location.x = 0
    root.location.y = 0
    root.show_in_front = False
    bpy.context.view_layer.update()
    return root


def validate_motion(rig, kind, signature):
    """Check source rest bones, planted grounding and finite evaluated geometry over the clip."""
    if measure_rig_signature(rig) != signature:
        raise RuntimeError(f"Source rest skeleton changed: {kind}")
    scene = bpy.context.scene
    samples = []
    for frame in (1, 20, 42, 55, 60, 63, 85, 100, 120):
        scene.frame_set(frame)
        low, high = measure_bounds(rig)
        if not (-.015 < low[2] < .015) or high[2] > 1.5:
            raise RuntimeError(f"Unexpected motion bounds for {kind} frame {frame}: {low}, {high}")
        samples.append({"frame": frame, "min": low, "max": high})
    scene.frame_set(1)
    return samples


def export_animation(rig, kind):
    """Export only the original model hierarchy with its baked draft clip, never fixtures."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [rig, *rig.children_recursive]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(MODELS / f"{kind}AnimationDraft.fbx"),
                             use_selection=True, object_types={"ARMATURE", "MESH"},
                             add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
                             path_mode="COPY", embed_textures=True, axis_forward="-Z", axis_up="Y")


def build_previews():
    """Build two isolated animation scenes from grounded source models."""
    imported_scene = bpy.context.scene
    imported_scene.name = "01_AllModels_Grounded"
    report = {"scope": "Blender motion previews only; no AI, damage, HP or production flame effects", "models": {}}
    for kind, rig_name in zip(("GreekFire", "Headbutter"), RIG_NAMES):
        source = bpy.data.objects[rig_name]
        signature = measure_rig_signature(source)
        scene = bpy.data.scenes.new("02_GreekFire_Preview" if kind == "GreekFire" else "03_Headbutter_Preview")
        bpy.context.window.scene = scene
        rig = duplicate_model(source, scene)
        if kind == "GreekFire":
            audit = json.loads((REVIEW / "source-0.json").read_text())
            assign_rigid_turret(collect_meshes(rig)[0], audit)
            remove_support_feet(collect_meshes(rig)[0], audit)
            ground_models([rig])
            action = create_greek_fire(rig)
        else:
            action = create_headbutt(rig)
        create_stage(scene, kind)
        samples = validate_motion(rig, kind, signature)
        export_animation(rig, kind)
        if kind == "GreekFire":
            create_flame_preview(rig)
            markers = [(1, "Search"), (55, "Target locked"), (60, "Spray start"), (86, "Spray stop"), (120, "Rest")]
        else:
            markers = [(1, "Rest"), (18, "Wind-up"), (52, "Drive"), (60, "HEAD IMPACT"), (70, "Recover"), (120, "Rest")]
        for frame, name in markers:
            scene.timeline_markers.new(name, frame=frame)
        report["models"][kind] = {"source": rig_name, "source_bones_preserved": len(signature),
                                  "action": action.name, "frames": [1, FRAME_END], "fps": 24,
                                  "grounding_samples": samples,
                                  "weights": "rigid upper assembly/chassis/wheels in preview copy" if kind == "GreekFire" else "unchanged Tripo weights"}
        scene.frame_set(60)
        scene.render.filepath = str(REVIEW / f"{kind}-impact.png")
        bpy.ops.render.render(write_still=True)
        set_preview_workspace(scene)
    # Apply the approved stabilizer removal to the static working lineup as well.
    # The separate original snapshot and initial grounded source remain recoverable.
    bpy.context.window.scene = imported_scene
    greek_source = bpy.data.objects[RIG_NAMES[0]]
    audit = json.loads((REVIEW / "source-0.json").read_text())
    removed = remove_support_feet(collect_meshes(greek_source)[0], audit)
    report["static_lineup_grounding"] = ground_models(collect_model_roots())
    report["greek_fire_removed_support_vertices"] = removed
    bpy.ops.object.camera_add(location=(6, -8, 8))
    imported_scene.camera = bpy.context.object
    imported_scene.camera.name = "Grounded lineup review camera"
    imported_scene.camera.data.type = "ORTHO"
    imported_scene.camera.data.ortho_scale = 8
    point_at(imported_scene.camera, (0, -.5, .35))
    bpy.context.window.scene = bpy.data.scenes["02_GreekFire_Preview"]
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "SiegeAnimations.blend"))
    (REVIEW / "animation-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2))
    print("Siege preview scenes and FBX clips ready", flush=True)


def render_video_frames():
    """Render preview frames with an optional kind argument for inexpensive iteration."""
    arguments = sys.argv[sys.argv.index("--") + 1:]
    kind = arguments[arguments.index("--video") + 1]
    scene_names = {"GreekFire": "02_GreekFire_Preview", "Headbutter": "03_Headbutter_Preview",
                   "GreekFireMovement": "04_GreekFire_Movement"}
    scene = bpy.data.scenes[scene_names[kind]]
    bpy.context.window.scene = scene
    scene.render.resolution_percentage = 75
    scene.cycles.samples = 8
    scene.render.image_settings.file_format = "PNG"
    folder = REVIEW / kind
    folder.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(folder / "frame-")
    bpy.ops.render.render(animation=True)


def update_headbutt_preview():
    """Revise only the giant action in the saved working file; preserve other scenes."""
    scene = bpy.data.scenes["03_Headbutter_Preview"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    signature = measure_rig_signature(rig)
    action = create_headbutt(rig)
    samples = validate_motion(rig, "Headbutter", signature)
    export_animation(rig, "Headbutter")
    scene.timeline_markers.clear()
    for frame, name in [(12, "Wind-up"), (16, "Right foot advances"), (32, "Right foot planted"),
                        (48, "Left foot planted"), (60, "HEAVY HEAD IMPACT"), (69, "Recoil"),
                        (80, "Step back"), (112, "Rest")]:
        scene.timeline_markers.new(name, frame=frame)
    # Match the demonstration wall to the forehead contact; lighting remains untouched.
    scene.frame_set(60)
    mesh = collect_meshes(rig)[0]
    head_group = mesh.vertex_groups["Head"].index
    head_indices = [vertex.index for vertex in mesh.data.vertices
                    if any(group.group == head_group and group.weight > .45 for group in vertex.groups)]
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    forehead_y = min((evaluated.matrix_world @ evaluated.data.vertices[index].co).y for index in head_indices)
    for obj in scene.objects:
        if obj.name.startswith("Impact target masonry"):
            obj.location.y = forehead_y - .095 + .012
    scene.camera.location = (2, .4, 1.2)
    point_at(scene.camera, (0, -.13, .43))
    scene.render.filepath = str(REVIEW / "Headbutter-impact.png")
    bpy.ops.render.render(write_still=True)
    report_path = REVIEW / "animation-report.json"
    report = json.loads(report_path.read_text())
    report["models"]["Headbutter"].update(action=action.name, grounding_samples=samples,
        choreography="Right foot advances during anticipation (16-32); left foot plants at 48 before impact at 60; both feet planted during strike")
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2))
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "SiegeAnimations.blend"))
    print("Updated headbutter only: step, heavy ram, recoil; materials and lights unchanged", flush=True)


def update_greek_fire_preview():
    """Turn the existing whole upper assembly without rebuilding the model or scene."""
    scene = bpy.data.scenes["02_GreekFire_Preview"]
    bpy.context.window.scene = scene
    scene.frame_set(1)
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    signature = measure_rig_signature(rig)
    mesh = collect_meshes(rig)[0]
    audit = audit_model(rig, mesh, REVIEW / "greek-current-audit.json")
    turret_count = assign_rigid_turret(mesh, audit)
    action = create_greek_fire(rig)
    samples = validate_motion(rig, "GreekFire", signature)
    export_animation(rig, "GreekFire")
    for obj in list(scene.objects):
        if obj.name.startswith("PreviewFlame_") and obj.get("review_fixture"):
            bpy.data.objects.remove(obj, do_unlink=True)
    create_flame_preview(rig)
    scene.frame_set(60)
    scene.render.filepath = str(REVIEW / "GreekFire-impact.png")
    bpy.ops.render.render(write_still=True)
    report_path = REVIEW / "animation-report.json"
    report = json.loads(report_path.read_text())
    report["models"]["GreekFire"].update(action=action.name, grounding_samples=samples,
        weights="Rigid complete upper assembly / chassis / four wheels", turret_vertices=turret_count)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2))
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "SiegeAnimations.blend"))
    print("Updated Greek Fire: rigid upper assembly yaw; chassis, lights and textures preserved", flush=True)


def update_movement_preview():
    """Add reusable eight-direction motion without changing the existing spray demonstration."""
    source_scene = bpy.data.scenes["02_GreekFire_Preview"]
    bpy.context.window.scene = source_scene
    source_scene.frame_set(1)
    source = next(obj for obj in source_scene.objects if obj.type == "ARMATURE")
    scene = bpy.data.scenes.get("04_GreekFire_Movement")
    if scene is None:
        scene = bpy.data.scenes.new("04_GreekFire_Movement")
        bpy.context.window.scene = scene
        rig = duplicate_model(source, scene)
        create_stage(scene, "GreekFire")
        grid_material = create_material("Movement preview grid", (.22, .26, .29))
        for index in range(-4, 5):
            for axis in (0, 1):
                location = [0, 0, -.003]
                location[axis] = index*.25
                scale = [.003, 2, .001] if axis == 0 else [2, .003, .001]
                add_fixture_cube("Movement grid — not exported", location, scale, grid_material)
        scene.camera.location = (2, 2.5, 2)
        scene.camera.data.ortho_scale = 2.8
        point_at(scene.camera, (0, 0, .35))
    else:
        bpy.context.window.scene = scene
        scene.frame_set(1)
        rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    mesh = collect_meshes(rig)[0]
    movement_audit = audit_model(rig, mesh, REVIEW / "greek-movement-audit.json")
    assign_rigid_turret(mesh, movement_audit)
    action = create_directional_movement(rig)
    scene.frame_start, scene.frame_end = 1, MOVEMENT_END
    scene.timeline_markers.clear()
    for index, (name, _, _) in enumerate(DIRECTIONS):
        scene.timeline_markers.new(name, frame=index*SEGMENT_FRAMES+1)
    scene.frame_set(1)
    export_animation(rig, "GreekFireMovement")
    scene.frame_set(33)
    scene.render.filepath = str(REVIEW / "GreekFireMovement-impact.png")
    bpy.ops.render.render(write_still=True)
    report_path = REVIEW / "animation-report.json"
    report = json.loads(report_path.read_text())
    report["models"]["GreekFireMovement"] = {"action": action.name, "fps": 24,
        "frames": [1, MOVEMENT_END], "directions": [name for name, _, _ in DIRECTIONS],
        "scope": "Blender root-motion direction showcase; not Unity routing or gameplay speed"}
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2))
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "SiegeAnimations.blend"))
    print("Eight-direction Greek Fire movement scene and FBX ready", flush=True)


def render_reference():
    """Render imported assets without changing their mesh, materials or rest skeleton."""
    setup_render()
    scene = bpy.context.scene
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 850
    scene.cycles.samples = 12
    for index, rig_name in enumerate(RIG_NAMES):
        rig = bpy.data.objects[rig_name]
        visible = set([rig, *rig.children_recursive])
        for obj in scene.objects:
            if obj.type in {"MESH", "ARMATURE"}:
                obj.hide_render = obj not in visible
        low, high = measure_bounds(rig)
        center = (Vector(low) + Vector(high)) * .5
        direction = 1 if index == 0 else -1
        camera = scene.camera
        camera.location = center + Vector((1.4, direction * 2.2, 1.2))
        camera.data.ortho_scale = 1.65
        point_at(camera, center)
        for light in [obj for obj in scene.objects if obj.type == "LIGHT"]:
            light.location.x += center.x
            point_at(light, center)
        scene.render.filepath = str(REVIEW / f"reference-{index}.png")
        bpy.ops.render.render(write_still=True)
        for light in [obj for obj in scene.objects if obj.type == "LIGHT"]:
            light.location.x -= center.x


def save_audit():
    """Save source bone/weight geometry audit and initial reference renders."""
    REVIEW.mkdir(parents=True, exist_ok=True)
    MODELS.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.frame_set(1)
    roots = collect_model_roots()
    grounding = ground_models(roots)
    (REVIEW / "grounding.json").write_text(json.dumps(grounding, ensure_ascii=False, indent=2))
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(MODELS / "TripoModelsGrounded.blend"))
    for index, rig_name in enumerate(RIG_NAMES):
        rig = bpy.data.objects[rig_name]
        mesh = collect_meshes(rig)[0]
        audit = audit_model(rig, mesh, REVIEW / f"source-{index}.json")
        audit["matrix_world"] = [list(row) for row in rig.matrix_world]
        audit["mesh_matrix"] = [list(row) for row in mesh.matrix_world]
        audit["vertex_groups"] = [group.name for group in mesh.vertex_groups]
        (REVIEW / f"source-{index}.json").write_text(json.dumps(audit, ensure_ascii=False, indent=2))
    print("Grounding and rig audits saved", flush=True)


if __name__ == "__main__":
    if "--headbutt" in sys.argv:
        update_headbutt_preview()
    elif "--greek-fire" in sys.argv:
        update_greek_fire_preview()
    elif "--movement" in sys.argv:
        update_movement_preview()
    elif "--build" in sys.argv:
        build_previews()
    elif "--video" in sys.argv:
        render_video_frames()
    elif "--reference" in sys.argv:
        render_reference()
    else:
        save_audit()
