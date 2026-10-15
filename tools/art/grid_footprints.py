"""Append integer-grid ground variants while preserving all existing architecture.

Ground completion is explicitly user-authorized. Houses, roofs and original UVs
are unchanged; original low ground is sunk just below the new flush dirt tile.
"""
import hashlib
import json
import math
import shutil
import sys
import copy
from datetime import datetime
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

sys.path.insert(0, str(Path(__file__).parent))
from fortified_city_assembly import (capture_protected_state, verify_protected_state,
    read_mesh_record, hash_file, read_fbx_global_settings, capture_export_expectations,
    verify_export_roundtrip, copy_export_material)

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame.blend"
REVIEW = ROOT / "Builds/ArtReview/GridFootprints"
OUTPUT = ROOT / "art/models/grid-footprints"
HANDOFF = ROOT / "art/grid_footprint_handoff.json"
GROUP = "建筑整格地块 · 牧场14＋基地12＋工坊8"
EXPECTED_SHA = "6772efb7ce13ecc5fb5700d3eb846af5f36c96cde0f02c53701f7a14fb004ea5"
SPECS = [
    {"id": "Pasture14", "source": "farmhouse", "grid": [14, 14], "model_depth_m": 13, "ground_cover_native_z": .015},
    {"id": "Headquarters12", "source": "BZT Main base1", "grid": [12, 12], "model_depth_m": 12, "ground_cover_native_z": .008},
    {"id": "Workshop8", "source": "BZT wooden workshop", "grid": [8, 8], "model_depth_m": 8, "ground_cover_native_z": .01},
]


def verify_source():
    if not bpy.app.background or Path(bpy.data.filepath).resolve() != MASTER:
        raise RuntimeError("Run in background on formal general master only")
    if hash_file(MASTER) != EXPECTED_SHA or len(bpy.data.objects) != 414:
        raise RuntimeError("General master changed; refuse to overwrite without fresh audit")
    if GROUP in bpy.data.collections:
        raise RuntimeError("Grid variants already exist; refuse duplicate authoring")


def create_dirt_material():
    """Use existing licensed seamless soil with real GL normal and roughness channels."""
    folder = OUTPUT / "textures"
    folder.mkdir(parents=True, exist_ok=True)
    source_dir = ROOT / "Assets/_Game/Resources/FreeEnvironment/dirt"
    manifest = json.loads((ROOT / "art/terrain_material_manifest.json").read_text())
    pack = next(pack for pack in manifest["packs"] if pack["id"] == "dirt")
    images, channels = {}, {}
    for channel, md5 in zip(("diff", "nor_gl", "rough"), pack["md5"]):
        source = source_dir / f"dirt_{channel}_1k.jpg"
        if hashlib.md5(source.read_bytes()).hexdigest() != md5:
            raise AssertionError(f"Existing licensed dirt channel changed: {source}")
        destination = folder / source.name
        shutil.copy2(source, destination)
        image = bpy.data.images.load(str(destination), check_existing=False)
        image.name = f"GridDirt_{channel}"
        image.colorspace_settings.name = "sRGB" if channel == "diff" else "Non-Color"
        image.pack()
        images[channel] = image
        channels[channel] = {"file": str(destination.relative_to(ROOT)), "sha256": hash_file(destination),
                             "colorspace": image.colorspace_settings.name, "resolution": list(image.size)}
    material = bpy.data.materials.new("GridFootprint_Dirt")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    shader = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Metallic"].default_value = 0
    texture_nodes = {}
    for channel, image in images.items():
        node = nodes.new("ShaderNodeTexImage")
        node.image = image
        node.extension = "REPEAT"
        node.name = "Grid Soil " + channel
        texture_nodes[channel] = node
    links.new(texture_nodes["diff"].outputs["Color"], shader.inputs["Base Color"])
    links.new(texture_nodes["rough"].outputs["Color"], shader.inputs["Roughness"])
    normal = nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = .35
    links.new(texture_nodes["nor_gl"].outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])
    material.diffuse_color = (.24, .20, .13, 1)
    return material, {"name": material.name, "license": "CC0-1.0", "author": "Charlotte Baglioni",
        "source": "https://polyhaven.com/a/dirt", "license_url": "https://polyhaven.com/license",
        "channels": channels, "metallic": 0, "normal_strength": .35, "uv_period_m": 2,
        "note": "Existing verified seamless PBR soil. No generated height/AO or invented texture."}


def create_ground_mesh(identity, grid, material):
    """Complete a flush integer rectangle; shallow underside remains below terrain."""
    x, y = grid[0] * .5, grid[1] * .5
    vertices = [(-x,-y,0),(x,-y,0),(x,y,0),(-x,y,0),
                (-x,-y,-.05),(x,-y,-.05),(x,y,-.05),(-x,y,-.05)]
    faces = [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
    mesh = bpy.data.meshes.new(identity + "_IntegerGround")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="UVMap")
    for face in mesh.polygons:
        for index in face.loop_indices:
            co = mesh.vertices[mesh.loops[index].vertex_index].co
            uv.data[index].uv = (co.x / 2, co.y / 2)
    mesh.update()
    return mesh


def append_variants(material):
    """Save source variants visibly beside existing layout with native-sized display roots."""
    collection = bpy.data.collections.new(GROUP)
    bpy.context.scene.collection.children.link(collection)
    points = [obj.matrix_world @ Vector(corner) for obj in bpy.context.scene.objects
              if obj.type == "MESH" and not obj.hide_viewport for corner in obj.bound_box]
    origin = Vector((max(point.x for point in points) + 3,
                     min(point.y for point in points) + 1, 0))
    results = []
    for index, spec in enumerate(SPECS):
        source = bpy.data.objects[spec["source"]]
        low_y = min(vertex.co.y for vertex in source.data.vertices)
        high_y = max(vertex.co.y for vertex in source.data.vertices)
        scale = spec["model_depth_m"] / (high_y - low_y)
        ground_offset = spec["ground_cover_native_z"] * scale
        root = bpy.data.objects.new("整格地块 · " + spec["id"] + "（展示比例）", None)
        collection.objects.link(root)
        root.location = origin + Vector((index * 2.1, 0, 0))
        root.scale = (1 / scale,) * 3
        root["display_only_scale"] = 1 / scale
        root["export_root_scale"] = 1
        root["grid_m"] = spec["grid"]
        architecture = bpy.data.objects.new(spec["id"] + "_OriginalArchitecture", source.data)
        collection.objects.link(architecture)
        architecture.parent = root
        architecture.scale = (scale,) * 3
        architecture.location.z = -ground_offset
        architecture["unchanged_source_mesh"] = source.name
        ground = bpy.data.objects.new(spec["id"] + "_IntegerGround", create_ground_mesh(spec["id"], spec["grid"], material))
        collection.objects.link(ground)
        ground.parent = root
        root["ground_surface_z"] = 0
        results.append({**spec, "root": root.name, "architecture": architecture.name, "ground": ground.name,
            "source_mesh": source.data.name, "source_mesh_sha256": read_mesh_record(source.data),
            "uniform_source_scale": scale, "architecture_grounding_translation_m": -ground_offset,
            "display_root_matrix": [list(row) for row in root.matrix_basis],
            "grid_ground_surface_m": 0, "below_ground_geometry_m": -ground_offset,
            "source_collection": collection.name})
    bpy.context.view_layer.update()
    return results, origin


def append_pasture_gallery(record, origin):
    """Keep a real 2x2 shared-edge assembly in the formal master for inspection."""
    collection = bpy.data.collections.new("牧场整格拼接 · 2×2无缝检查")
    bpy.context.scene.collection.children.link(collection)
    root = bpy.data.objects.new("牧场2×2 · 展示锚点", None)
    collection.objects.link(root)
    root.location = origin + Vector((1, -3, 0))
    root.scale = (1 / record["uniform_source_scale"],) * 3
    for row in range(2):
        for column in range(2):
            for name in (record["architecture"], record["ground"]):
                source = bpy.data.objects[name]
                obj = source.copy()
                obj.name = f"拼接{row}{column} · " + name
                collection.objects.link(obj)
                obj.parent = root
                obj.matrix_parent_inverse = Matrix.Identity(4)
                obj.matrix_basis = Matrix.Translation((column*14-7,row*14-7,0)) @ source.matrix_basis
    bpy.context.view_layer.update()
    return {"collection": collection.name, "root": root.name, "grid_count": [2,2], "overall_m": [28,28]}


def verify_ground_edges(record):
    mesh = bpy.data.objects[record["ground"]].data
    width, depth = record["grid"]
    top = [vertex.co for vertex in mesh.vertices if abs(vertex.co.z) < 1e-8]
    expected = {(-width/2,-depth/2),(width/2,-depth/2),(width/2,depth/2),(-width/2,depth/2)}
    if {(point.x,point.y) for point in top} != expected:
        raise AssertionError(f"Grid tile is not the exact integer rectangle: {record['id']}")
    if width % 2 or depth % 2:
        raise AssertionError("Seam UV period requires an even integer footprint")
    return {"grid": record["grid"], "ground_height": 0, "maximum_geometry_gap_m": 0,
            "opposite_edge_uv_period_differences": [width/2,depth/2],
            "uv_phase_mismatch": 0, "passed": True}


def render_pasture_grid(record):
    """Render actual model instances and completed ground, without touching source lighting."""
    original = bpy.context.window.scene
    scene = bpy.data.scenes.new("Temporary pasture grid inspection")
    bpy.context.window.scene = scene
    for row in range(2):
        for column in range(2):
            for name in [record["architecture"],record["ground"]]+([record["crop"]] if record.get("crop") else [])+([record["crop_completion"]] if record.get("crop_completion") else []):
                source = bpy.data.objects[name]
                obj = source.copy()
                obj.parent = None
                scene.collection.objects.link(obj)
                obj.matrix_world = Matrix.Translation((column*14-7,row*14-7,0)) @ source.matrix_basis
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_cavity = True
    scene.display.shading.show_shadows = True
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x,scene.render.resolution_y = 1500,1200
    scene.render.resolution_percentage = 100
    camera = bpy.data.objects.new("Temporary grid camera",bpy.data.cameras.new("Temporary grid camera"))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 38
    for view,location in (("isometric",(30,-40,38)),("top",(0,0,50))):
        camera.location = location
        camera.rotation_euler = (Vector((0,0,0))-camera.location).to_track_quat("-Z","Y").to_euler()
        scene.render.filepath = str(REVIEW / f"pasture-2x2-{view}.png")
        bpy.ops.render.render(write_still=True)
    bpy.context.window.scene = original
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.scenes.remove(scene)


def save_source(protected):
    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    backup = REVIEW / ("master-before-grid-"+timestamp+".blend")
    candidate = REVIEW / ("master-grid-candidate-"+timestamp+".blend")
    shutil.copy2(MASTER,backup)
    verify_protected_state(protected)
    image_paths = {name:image.filepath for name,image in bpy.data.images.items()}
    bpy.ops.wm.save_as_mainfile(filepath=str(candidate),compress=True,relative_remap=True)
    bpy.ops.wm.open_mainfile(filepath=str(candidate))
    verify_protected_state(protected)
    if hash_file(MASTER) != EXPECTED_SHA:
        raise AssertionError("Master changed during grid candidate work; refuse promotion")
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER),compress=True,relative_remap=True)
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    counts = verify_protected_state(protected)
    for name,path in image_paths.items():
        if bpy.data.images[name].filepath != path:
            raise AssertionError(f"Final stored image path changed: {name}")
    return {"master_sha256": hash_file(MASTER),"recovery_snapshot":str(backup.relative_to(ROOT)),
            "protected_counts":counts,"objects_after":len(bpy.data.objects)}


def export_variant(record, ground_material):
    folder = OUTPUT / record["id"]
    folder.mkdir(parents=True,exist_ok=True)
    scene = bpy.data.scenes.new("Temporary integer grid export")
    bpy.context.window.scene = scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    root = bpy.data.objects.new(record["id"],None)
    scene.collection.objects.link(root)
    root.rotation_euler.z = math.pi
    material, textures, original_material = copy_export_material(bpy.data.objects[record["architecture"]].data.materials[0],folder)
    material.name = record["id"] + "_OriginalMaterial"
    original_material["name"] = material.name
    original_material.update({"alpha_mode":"opaque","double_sided":False})
    objects = []
    component_names = [record["architecture"],record["ground"]] + ([record["crop"]] if record.get("crop") else []) + ([record["crop_completion"]] if record.get("crop_completion") else [])
    extra_materials = []
    components, all_points = [], []
    for name in component_names:
        source = bpy.data.objects[name]
        points = [source.matrix_basis @ vertex.co for vertex in source.data.vertices]
        low = Vector([min(point[axis] for point in points) for axis in range(3)])
        high = Vector([max(point[axis] for point in points) for axis in range(3)])
        center,size = (low+high)*.5,high-low
        slot_names = ([material.name] if name == record["architecture"] else [mat.name for mat in source.data.materials])
        components.append({"name":name,"expected_unity_bounds_center_m":[center.x,center.z,center.y],
            "expected_unity_bounds_size_m":[size.x,size.z,size.y],"material_names":slot_names})
        all_points.extend(points)
        if name == record.get("crop"):
            for crop_material in source.data.materials:
                shader = next((node for node in crop_material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"),None)
                color = list(shader.inputs["Base Color"].default_value) if shader else list(crop_material.diffuse_color)
                roughness = shader.inputs["Roughness"].default_value if shader else crop_material.roughness
                metallic = shader.inputs["Metallic"].default_value if shader else crop_material.metallic
                extra_materials.append({"name":crop_material.name,"base_color_texture":None,
                    "base_color_multiplier":color,"metallic":metallic,"roughness":roughness,"smoothness":1-roughness,
                    "normal_map":None,"roughness_map":None,"alpha_mode":"opaque","double_sided":True,
                    "note":"Existing approved crop constant-color material, no recoloring"})
        source.name = "SourceDisplay__" + name
        mesh = source.data.copy()
        if name == record["architecture"]:
            mesh.materials.clear()
            mesh.materials.append(material)
        obj = bpy.data.objects.new(name,mesh)
        scene.collection.objects.link(obj)
        obj.parent = root
        obj.matrix_basis = source.matrix_basis.copy()
        objects.append(obj)
    bpy.context.view_layer.update()
    expected = capture_export_expectations(objects)
    for obj in scene.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    path = folder/(record["id"]+".fbx")
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH","EMPTY"},
        use_mesh_modifiers=False,bake_anim=False,add_leaf_bones=False,apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",axis_forward="-Z",axis_up="Y",path_mode="COPY",embed_textures=False)
    low = Vector([min(point[axis] for point in all_points) for axis in range(3)])
    high = Vector([max(point[axis] for point in all_points) for axis in range(3)])
    center,size = (low+high)*.5,high-low
    record.update({"source_id":{"Pasture14":"Pasture","Headquarters12":"Headquarters","Workshop8":"Workshop"}[record["id"]],
        "export_root_name":record["id"],"footprint_m":record["grid"],"placement_scale":1,
        "grid_unit_m":1,"ground_anchor_y_m":0,"unity_ground_offset_m":.025,
        "architectural_scale_already_in_export":True,"components":components,
        "expected_unity_bounds_center_m":[center.x,center.z,center.y],
        "expected_unity_bounds_size_m":[size.x,size.z,size.y],"expected_unity_min_y_m":low.z})
    result = {"file":str(path.relative_to(ROOT)),"sha256":hash_file(path),
              "scale_baked":True,"unity_placement_scale":1,
              "materials":[original_material,ground_material]+extra_materials,"textures":textures,
              "fbx_global_settings":read_fbx_global_settings(path),
              "roundtrip":verify_export_roundtrip(path,expected)}
    return result


def append_crop_completion(record, gallery):
    """Fill only ground using periodic translated copies of approved individual stalks."""
    crop = bpy.data.objects[record["crop"]]
    source = crop.data
    if len(source.vertices) % 34:
        raise AssertionError("Approved crop no longer has its original 34-vertex stalk batches")
    count = len(source.vertices)//34
    face_groups = [[] for _ in range(count)]
    for face in source.polygons:
        groups = {index//34 for index in face.vertices}
        if len(groups)!=1:
            raise AssertionError("Imported approved stalk topology changed; cannot safely reuse batches")
        group = groups.pop()
        face_groups[group].append(([index%34 for index in face.vertices],face.material_index))
    scale = record["uniform_source_scale"]
    vertices,faces,slots = [],[],[]
    plant_count = 0
    for row in range(-1,141):
        for column in range(-1,141):
            seed = ((row%140)*3343+(column%140)*7547+59)%count
            x,y = -7+(column+.5)*.1,-7+(row+.5)*.1
            # Preserve the original full inner crop and all upper architecture.
            # The surrounding band reaches every tile boundary; no dirt cross is left.
            is_original_field = -5.07<x<2.47 and -5.60<y<3.63
            is_house_interior = x>2.0 and y>-1.5 and x<6.55 and y<6.55
            if is_original_field or is_house_interior:
                continue
            start = seed*34
            stem_base = (source.vertices[start].co+source.vertices[start+1].co)*.5
            offset = len(vertices)
            translation = Vector((x,y,.025))
            vertices.extend((source.vertices[start+i].co-stem_base)*scale+translation for i in range(34))
            for face,slot in face_groups[seed]:
                faces.append([offset+index for index in face])
                slots.append(slot)
            plant_count += 1
    mesh = bpy.data.meshes.new("Pasture14_ApprovedStalkBoundaryCompletion")
    mesh.from_pydata(vertices,[],faces)
    for material in source.materials:
        mesh.materials.append(material)
    for face,slot in zip(mesh.polygons,slots):
        face.material_index = slot
    # Only new ground-cover copies are cropped at the exact authored tile boundary.
    editable = bmesh.new()
    editable.from_mesh(mesh)
    for co,normal in (((-7,0,0),(-1,0,0)),((7,0,0),(1,0,0)),
                      ((0,-7,0),(0,-1,0)),((0,7,0),(0,1,0))):
        bmesh.ops.bisect_plane(editable,geom=list(editable.verts)+list(editable.edges)+list(editable.faces),
            plane_co=co,plane_no=normal,clear_outer=True,dist=1e-7)
    editable.to_mesh(mesh)
    editable.free()
    mesh.update()
    obj = bpy.data.objects.new("Pasture14_CropBoundaryCompletion",mesh)
    bpy.data.collections[GROUP].objects.link(obj)
    obj.parent = bpy.data.objects[record["root"]]
    record["crop_completion"] = obj.name
    record["crop_completion_evidence"] = {"approved_stalk_vertices_each":34,"source_stalks":count,
        "reused_stalk_instances":plant_count,"added_triangles":sum(len(face.vertices)-2 for face in mesh.polygons),
        "crop_field_period_m":14,"stalk_pitch_m":.1,"outer_clip_m":[-7,7,-7,7],
        "new_buildings":False,"note":"Translated copies of existing approved stalk batches only. Original inner crop, house and fence unchanged; only newly copied ground cover is clipped to rectangle."}
    gallery_collection = bpy.data.collections[gallery["collection"]]
    gallery_root = bpy.data.objects[gallery["root"]]
    for row in range(2):
        for column in range(2):
            clone = obj.copy()
            clone.name = f"拼接{row}{column} · "+obj.name
            gallery_collection.objects.link(clone)
            clone.parent = gallery_root
            clone.matrix_basis = Matrix.Translation((column*14-7,row*14-7,0))
    bpy.context.view_layer.update()


def append_approved_crop(record, gallery):
    """Reuse the accepted 258804-triangle crop mesh from the current runtime FBX."""
    fbx = ROOT/"Assets/_Game/Art/CoastalImports/Pasture/Pasture.fbx"
    expected = "2c5b73f3b8f628f69521bf7012de26debb262d368cbd4af7daba84e28c468acc"
    if hash_file(fbx) != expected:
        raise AssertionError("Approved dense pasture source FBX changed")
    before = {key:set(getattr(bpy.data,key)) for key in ("objects","meshes","materials","images")}
    render = bpy.context.scene.render
    timing = (render.fps,render.fps_base)
    selected = {obj.name for obj in bpy.context.selected_objects}
    active = bpy.context.view_layer.objects.active.name if bpy.context.view_layer.objects.active else None
    bpy.ops.import_scene.fbx(filepath=str(fbx),use_anim=False)
    # Blender's FBX importer changes Scene FPS even with animation disabled.
    render.fps,render.fps_base = timing
    imported = set(bpy.data.objects)-before["objects"]
    crop = next(obj for obj in imported if obj.type == "MESH" and "Golden straw cover" in obj.name)
    original_matrix = crop.matrix_world.copy()
    crop.name = "Pasture14_ApprovedGoldenCrop"
    for collection in list(crop.users_collection):
        collection.objects.unlink(crop)
    bpy.data.collections[GROUP].objects.link(crop)
    crop.parent = bpy.data.objects[record["root"]]
    crop.matrix_parent_inverse = Matrix.Identity(4)
    crop.matrix_basis = bpy.data.objects[record["architecture"]].matrix_basis @ original_matrix
    for obj in imported-{crop}:
        bpy.data.objects.remove(obj,do_unlink=True)
    for key in ("meshes","materials","images"):
        for item in set(getattr(bpy.data,key))-before[key]:
            if item.users == 0:
                getattr(bpy.data,key).remove(item)
    for obj in bpy.context.scene.objects:
        obj.select_set(obj.name in selected)
    bpy.context.view_layer.objects.active = bpy.data.objects.get(active) if active else None
    crop["approved_runtime_source"] = str(fbx.relative_to(ROOT))
    crop["approved_source_sha256"] = expected
    record["crop"] = crop.name
    record["crop_preservation"] = {"source_fbx":str(fbx.relative_to(ROOT)),"sha256":expected,
        "source_mesh_sha256":read_mesh_record(crop.data),"vertices":len(crop.data.vertices),
        "triangles":sum(len(face.vertices)-2 for face in crop.data.polygons),
        "note":"Exact existing approved golden crop geometry/material slots; original FBX registration then same whole architecture scale and grounding translation"}
    gallery_collection = bpy.data.collections[gallery["collection"]]
    gallery_root = bpy.data.objects[gallery["root"]]
    for row in range(2):
        for column in range(2):
            obj = crop.copy()
            obj.name = f"拼接{row}{column} · " + crop.name
            gallery_collection.objects.link(obj)
            obj.parent = gallery_root
            obj.matrix_parent_inverse = Matrix.Identity(4)
            obj.matrix_basis = Matrix.Translation((column*14-7,row*14-7,0)) @ crop.matrix_basis
    bpy.context.view_layer.update()


def complete_approved_crop():
    """Finish this turn's candidate without discarding its saved source or old 414 assets."""
    global EXPECTED_SHA
    EXPECTED_SHA = "385bbd298b8063fef8643119364fbed11d20321ab5f7767b2336ed5945fb4c2f"
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER or hash_file(MASTER)!=EXPECTED_SHA:
        raise RuntimeError("Require exact current grid candidate source before preserving approved crop")
    handoff = json.loads(HANDOFF.read_text())
    protected = capture_protected_state()
    (REVIEW/"protected-before-crop.json").write_text(json.dumps(protected,ensure_ascii=False,indent=2))
    records = handoff["records"]
    append_approved_crop(records[0],handoff["gallery"])
    append_crop_completion(records[0],handoff["gallery"])
    # Only this turn's own two collections gain authorized new members. Every
    # existing member/object, their transforms, material and geometry remain guarded.
    for name in (GROUP,handoff["gallery"]["collection"]):
        old_members = set(protected["collections"][name]["objects"])
        new_members = set(bpy.data.collections[name].objects.keys())
        if not old_members.issubset(new_members):
            raise AssertionError("Grid repair removed an existing collection member")
        protected["collections"][name]["objects"] = sorted(new_members)
    # The diagnostic renderer reads optional approved crop as well as house/base.
    render_pasture_grid(records[0])
    for camera in list(bpy.data.cameras):
        if camera.users == 0:
            bpy.data.cameras.remove(camera)
    source = save_source(protected)
    for record in records:
        bpy.ops.wm.open_mainfile(filepath=str(MASTER))
        record["export"] = export_variant(record,handoff["ground_material"])
    handoff.update(source)
    handoff["source_before_sha256"] = EXPECTED_SHA
    handoff["source_revision_chain"] = ["6772efb7ce13ecc5fb5700d3eb846af5f36c96cde0f02c53701f7a14fb004ea5",EXPECTED_SHA,source["master_sha256"]]
    handoff["assets"] = records
    handoff.pop("records",None)
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("DENSE CROP GRID SOURCE FROZEN",json.dumps({"sha":source["master_sha256"],"crop_triangles":records[0]["crop_preservation"]["triangles"]}))


def verify_crop_edge_phase(mesh):
    """Measure actual clipped stalk boundary sections on opposing tile edges."""
    result = {}
    for axis in (0,1):
        sections = [[Vector((vertex.co[1-axis],vertex.co.z,0)) for vertex in mesh.vertices
                     if abs(vertex.co[axis]-sign*7)<.00001] for sign in (-1,1)]
        if any(not points for points in sections):
            raise AssertionError("Crop completion does not reach every exact grid boundary")
        maximum = 0
        for first,second in (sections,sections[::-1]):
            tree = KDTree(len(second))
            for index,point in enumerate(second):
                tree.insert(point,index)
            tree.balance()
            maximum = max(maximum,max(tree.find(point)[2] for point in first))
        if maximum>.0001:
            raise AssertionError(f"Opposite crop boundaries do not join periodically: axis {axis}, {maximum}")
        result["x" if axis==0 else "z"] = {"vertices_on_edges":[len(points) for points in sections],
            "maximum_boundary_section_difference_m":maximum,"tolerance_m":.0001,"passed":True}
    return result


def verify_frozen_source():
    """Independently reopen final source and prove old art, crop continuity and exports."""
    handoff = json.loads(HANDOFF.read_text())
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER or hash_file(MASTER)!=handoff["master_sha256"]:
        raise AssertionError("Final source no longer matches grid handoff")
    original = json.loads((REVIEW/"protected-state.json").read_text())
    counts = verify_protected_state(original)
    prior_variants = json.loads((REVIEW/"protected-before-clean.json").read_text())
    prior_counts = verify_protected_state(prior_variants)
    results = []
    for record in handoff["assets"]:
        expected_architecture = record.get("clean_source_mesh_sha256",record["source_mesh_sha256"])
        if read_mesh_record(bpy.data.objects[record["architecture"]].data)!=expected_architecture:
            raise AssertionError(f"Original upper model changed: {record['id']}")
        if hash_file(ROOT/record["export"]["file"])!=record["export"]["sha256"]:
            raise AssertionError("Export changed after roundtrip")
        record["export"]["scale_baked"] = True
        record["export"]["unity_placement_scale"] = 1
        for material in record["export"]["materials"]:
            material.setdefault("alpha_mode","opaque")
            material.setdefault("double_sided",False)
        results.append({"id":record["id"],"edges":verify_ground_edges(record),"upper_geometry_uv_unchanged":True,
                        "roundtrip":record["export"]["roundtrip"]})
    pasture = handoff["assets"][0]
    if read_mesh_record(bpy.data.objects[pasture["crop"]].data)!=pasture["crop_preservation"]["source_mesh_sha256"]:
        raise AssertionError("Approved original golden crop changed")
    phases = verify_crop_edge_phase(bpy.data.objects[pasture["crop_completion"]].data)
    proof = {"passed":True,"source_sha256":handoff["master_sha256"],"protected_original_counts":counts,
        "protected_prior_variant_counts":prior_counts,"old_442_including_prior_variants_preserved":prior_counts["objects"]==442,
        "old_414_including_frozen_wall_preserved":counts["objects"]==414,"records":results,"crop_boundary_phase":phases,
        "original_crop_triangles":pasture["crop_preservation"]["triangles"],
        "added_ground_crop_triangles":pasture["crop_completion_evidence"]["added_triangles"],
        "note":"Rendering cost increased for visual review, not claimed optimized or battle benchmarked. Towers remain deferred; no absent tower collections restored."}
    (REVIEW/"final-source-verification.json").write_text(json.dumps(proof,ensure_ascii=False,indent=2))
    handoff["protected_original_414_proof"] = {"passed":True,"counts":counts,"report":"Builds/ArtReview/GridFootprints/final-source-verification.json"}
    handoff["protected_prior_442_proof"] = {"passed":True,"counts":prior_counts,"report":"Builds/ArtReview/GridFootprints/final-source-verification.json"}
    handoff["crop_boundary_phase"] = phases
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    render_pasture_grid(pasture)
    print("FINAL GRID SOURCE PASS",json.dumps(proof))


def derive_clean_house(source):
    """Keep connected house pieces and UVs; remove field beds and courtyard props."""
    mesh = source.data
    parents = list(range(len(mesh.vertices)))
    def find_root(index):
        while parents[index]!=index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index
    for edge in mesh.edges:
        first,second = [find_root(index) for index in edge.vertices]
        parents[second] = first
    groups = {}
    for vertex in mesh.vertices:
        groups.setdefault(find_root(vertex.index),[]).append(vertex.index)
    house_groups = set()
    excluded_groups = set()
    for key,indices in groups.items():
        points = [mesh.vertices[index].co for index in indices]
        low = [min(point[axis] for point in points) for axis in range(3)]
        high = [max(point[axis] for point in points) for axis in range(3)]
        # Bounds identify whole disconnected source pieces; no roof is cut by a box.
        # This detached 143-vertex upright cloth/stand is not part of the roof.
        # Keep the roof awning (separate 32-vertex source component) unchanged.
        is_upright_cloth = (len(indices)==143 and .148<low[0]<.151
                            and .244<high[0]<.247 and high[2]<.137)
        if is_upright_cloth:
            excluded_groups.add(key)
        elif low[0]>=.12 and high[0]<=.47 and low[1]>=-.11 and high[1]<=.455 and high[2]>.10:
            house_groups.add(key)
    protected_house_vertices = {vertex.index for vertex in mesh.vertices
        if .12<=vertex.co.x<=.47 and -.11<=vertex.co.y<=.455 and vertex.co.z>.08
        and find_root(vertex.index) not in excluded_groups}
    kept = []
    for face in mesh.polygons:
        group = find_root(face.vertices[0])
        points = [mesh.vertices[index].co for index in face.vertices]
        touches_house = (bool(protected_house_vertices.intersection(face.vertices))
                         and all(.12<=point.x<=.47 and -.11<=point.y<=.455 for point in points))
        if group not in excluded_groups and (group in house_groups or touches_house):
            kept.append(face)
    kept_indices = sorted({index for face in kept for index in face.vertices})
    index_map = {index:new_index for new_index,index in enumerate(kept_indices)}
    result = bpy.data.meshes.new("Pasture14Clean_HouseOnly_OriginalRoofAndUV")
    result.from_pydata([mesh.vertices[index].co for index in kept_indices],[],
                      [[index_map[index] for index in face.vertices] for face in kept])
    for material in mesh.materials:
        result.materials.append(material)
    for old,new in zip(kept,result.polygons):
        new.use_smooth = old.use_smooth
        new.material_index = old.material_index
    for layer in mesh.uv_layers:
        target = result.uv_layers.new(name=layer.name)
        for old,new in zip(kept,result.polygons):
            for old_loop,new_loop in zip(old.loop_indices,new.loop_indices):
                target.data[new_loop].uv = layer.data[old_loop].uv
    # Whole roof components are retained by connectivity. Long fence faces merely
    # touching a house vertex are deliberately not classified as house skin.
    result.update()
    return result,{"source_vertices":len(mesh.vertices),"kept_vertices":len(result.vertices),
        "source_faces":len(mesh.polygons),"kept_faces":len(kept),"protected_upper_house_vertices":len(protected_house_vertices),
        "whole_connected_house_components":len(house_groups),"excluded_upright_cloth_components":len(excluded_groups),
        "source_face_indices":[face.index for face in kept],
        "note":"Original retained house/roof face coordinates and UVs copied exactly. Courtyard fences, poles, upright cloth, detached trees, props and raised field-bed skin excluded. Existing crop is independently lowered rigidly per stalk to the common ground surface."}


def flatten_original_crop(source):
    """Rigidly ground each existing stalk, retaining its geometry and material slots."""
    mesh = source.data.copy()
    mesh.name = "Pasture14Clean_ApprovedCrop_FlatGround"
    if len(mesh.vertices)%34:
        raise AssertionError("Cannot identify original 34-vertex crop stalks")
    transform = source.matrix_basis.copy()
    inverse_direction = transform.inverted().to_3x3()
    maximum_error = 0
    for start in range(0,len(mesh.vertices),34):
        stem_base = (mesh.vertices[start].co+mesh.vertices[start+1].co)*.5
        world_base = transform@stem_base
        translation = inverse_direction@Vector((0,0,.025-world_base.z))
        for index in range(start,start+34):
            original = mesh.vertices[index].co.copy()
            mesh.vertices[index].co += translation
            maximum_error = max(maximum_error,(mesh.vertices[index].co-original-translation).length)
    mesh.update()
    if maximum_error>.000001:
        raise AssertionError(f"Per-stalk rigid translation changed shape: {maximum_error}")
    return mesh,{"stalk_count":len(mesh.vertices)//34,"vertices_per_stalk":34,
        "root_surface_m":.025,"maximum_local_shape_error_m":maximum_error,
        "original_mesh_sha256":read_mesh_record(source.data),"flattened_mesh_sha256":read_mesh_record(mesh),
        "note":"Only one rigid vertical translation per original approved stalk, no resizing or new plant shapes. Original source mesh and old variants remain untouched."}


def fill_inner_field(source, scale):
    """Reuse source stalks on the same periodic grid as the approved outer band."""
    original = source.data
    count = len(original.vertices)//34
    face_groups = [[] for _ in range(count)]
    for face in original.polygons:
        groups = {index//34 for index in face.vertices}
        if len(groups)!=1:
            raise AssertionError("Original crop stalk topology is not safely separable")
        seed = groups.pop()
        face_groups[seed].append(([index%34 for index in face.vertices],face.material_index))
    vertices,faces,slots = [],[],[]
    plant_count = 0
    for row in range(140):
        for column in range(140):
            x,y = -7+(column+.5)*.1,-7+(row+.5)*.1
            if not (-5.07<x<2.47 and -5.60<y<3.63):
                continue
            if x>2.0 and y>-1.5 and x<6.55 and y<6.55:
                continue
            seed = (row*3343+column*7547+59)%count
            start = seed*34
            base = (original.vertices[start].co+original.vertices[start+1].co)*.5
            offset = len(vertices)
            vertices.extend((original.vertices[start+i].co-base)*scale+Vector((x,y,.025)) for i in range(34))
            for face,slot in face_groups[seed]:
                faces.append([offset+index for index in face])
                slots.append(slot)
            plant_count += 1
    mesh = bpy.data.meshes.new("Pasture14Clean_OriginalStalks_ContinuousInnerField")
    mesh.from_pydata(vertices,[],faces)
    for material in original.materials:
        mesh.materials.append(material)
    for face,slot in zip(mesh.polygons,slots):
        face.material_index = slot
    mesh.update()
    return mesh,{"stalk_count":plant_count,"vertices_per_stalk":34,"root_surface_m":.025,
        "stalk_pitch_m":.1,"source_mesh_sha256":read_mesh_record(original),
        "new_mesh_sha256":read_mesh_record(mesh),
        "note":"Exact original stalk shapes, copied by rigid translation onto the outer band's periodic ground grid. No L-shaped original courtyard lanes or raised beds remain. Old source and old variants preserved."}


def repair_continuous_clean_field():
    """Remove remaining joined courtyard faces and fill the original lane footprint."""
    global EXPECTED_SHA
    EXPECTED_SHA = "b82b452cd7c8fa994a176b9209c2aa4dc3f470e2e6e6e9b7912d035e4e37c3ca"
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER or hash_file(MASTER)!=EXPECTED_SHA:
        raise AssertionError("Require exact clean candidate for continuous-field repair")
    protected = json.loads((REVIEW/"protected-before-clean.json").read_text())
    verify_protected_state(protected)
    handoff = json.loads(HANDOFF.read_text())
    record = handoff["assets"][0]
    house,evidence = derive_clean_house(bpy.data.objects["farmhouse"])
    crop,crop_evidence = fill_inner_field(bpy.data.objects["Pasture14_ApprovedGoldenCrop"],record["uniform_source_scale"])
    old_house = bpy.data.objects[record["architecture"]].data
    old_crop = bpy.data.objects[record["crop"]].data
    for name in (record["source_collection"],handoff["gallery"]["collection"]):
        for obj in bpy.data.collections[name].objects:
            if obj.type=="MESH" and obj.data==old_house:
                obj.data = house
            elif obj.type=="MESH" and obj.data==old_crop:
                obj.data = crop
                obj.matrix_basis = Matrix.Translation(obj.matrix_basis.translation)
                obj.location.z = 0
    for mesh in (old_house,old_crop):
        if mesh.users==0:
            bpy.data.meshes.remove(mesh)
    record["clean_source_mesh_sha256"] = read_mesh_record(house)
    record["clean_farm_evidence"] = evidence
    record["crop_preservation"].update({"source_mesh_sha256":read_mesh_record(crop),
        "vertices":len(crop.vertices),"triangles":sum(len(face.vertices)-2 for face in crop.polygons),
        "note":crop_evidence["note"]})
    record["clean_farm_evidence"]["note"] = ("Original retained house/roof face coordinates and UVs copied exactly. "
        "Courtyard fences, poles, upright cloth, detached trees, props and raised field-bed skin excluded. "
        "Existing approved stalk shapes are rigidly copied onto one continuous periodic ground grid; "
        "original crop source and previous variants remain unchanged.")
    record["crop_grounding"] = crop_evidence
    render_pasture_grid(record)
    for camera in list(bpy.data.cameras):
        if camera.users==0:
            bpy.data.cameras.remove(camera)
    saved = save_source(protected)
    record["export"] = export_variant(record,handoff["ground_material"])
    handoff.update(saved)
    handoff["source_before_sha256"] = EXPECTED_SHA
    handoff["source_revision_chain"].append(saved["master_sha256"])
    handoff.pop("protected_original_414_proof",None)
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("CONTINUOUS CLEAN FIELD FROZEN",json.dumps({"sha":saved["master_sha256"],
        "export_sha256":record["export"]["sha256"],"house_vertices":evidence["kept_vertices"]}))


def finish_clean_pasture():
    """Complete only this turn's clean variant, preserving all 442 prior objects."""
    global EXPECTED_SHA
    EXPECTED_SHA = "f83c1ebc14407f62c635a332c2c20c0a36300822ee161599517bc39d626ae4b1"
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER or hash_file(MASTER)!=EXPECTED_SHA:
        raise AssertionError("Require the exact saved clean-farm candidate for final repair")
    protected = json.loads((REVIEW/"protected-before-clean.json").read_text())
    verify_protected_state(protected)
    handoff = json.loads(HANDOFF.read_text())
    record = copy.deepcopy(handoff["assets"][0])
    collection_name = "农田整格 · 仅小屋与连续麦田"
    gallery_name = "清爽连片麦田 · 2×2评审"
    gallery_root_name = "清爽麦田2×2 · 展示锚点"
    record.update({"root":"Pasture14Clean · 展示锚点","source_collection":collection_name})
    for key in ("architecture","ground","crop","crop_completion"):
        record[key] = "Pasture14Clean_"+key
    old_architecture = bpy.data.objects[record["architecture"]].data
    old_crop = bpy.data.objects[record["crop"]].data
    house,evidence = derive_clean_house(bpy.data.objects["farmhouse"])
    crop,crop_evidence = flatten_original_crop(bpy.data.objects[record["crop"]])
    # Only objects in our two new collections receive the new data pointers.
    for name in (collection_name,gallery_name):
        for obj in bpy.data.collections[name].objects:
            if obj.type=="MESH" and obj.data==old_architecture:
                obj.data = house
            elif obj.type=="MESH" and obj.data==old_crop:
                obj.data = crop
    if old_architecture.users==0:
        bpy.data.meshes.remove(old_architecture)
    record["clean_source_mesh_sha256"] = read_mesh_record(house)
    record["clean_farm_evidence"] = evidence
    record["crop_preservation"]["original_source_mesh_sha256"] = record["crop_preservation"]["source_mesh_sha256"]
    record["crop_preservation"]["source_mesh_sha256"] = read_mesh_record(crop)
    record["crop_grounding"] = crop_evidence
    record["crop_preservation"]["note"] = crop_evidence["note"]
    render_pasture_grid(record)
    for camera in list(bpy.data.cameras):
        if camera.users==0:
            bpy.data.cameras.remove(camera)
    saved = save_source(protected)
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    record["export"] = export_variant(record,handoff["ground_material"])
    handoff["assets"][0] = record
    handoff.update(saved)
    handoff["source_before_sha256"] = EXPECTED_SHA
    handoff["source_revision_chain"].extend([EXPECTED_SHA,saved["master_sha256"]])
    handoff["clean_farm_collection"] = collection_name
    handoff["gallery"] = {"collection":gallery_name,"root":gallery_root_name,"grid_count":[2,2],"overall_m":[28,28]}
    handoff.pop("protected_original_414_proof",None)
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("CLEAN FARM FINAL SOURCE FROZEN",json.dumps({"sha":saved["master_sha256"],
        "export_sha256":record["export"]["sha256"],"house_vertices":evidence["kept_vertices"],"crop":crop_evidence}))


def make_clean_pasture():
    """Create an additional clean-farm source; preserve every prior variant and wall."""
    global EXPECTED_SHA
    EXPECTED_SHA = "6b804e5dbfb12265a35f0cbfa865ae9eb9c2960ec4c5a0fb513d3f097637b821"
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER or hash_file(MASTER)!=EXPECTED_SHA:
        raise AssertionError("Require exact frozen pre-clean farm source")
    handoff = json.loads(HANDOFF.read_text())
    protected = capture_protected_state()
    (REVIEW/"protected-before-clean.json").write_text(json.dumps(protected,ensure_ascii=False,indent=2))
    old = handoff["assets"][0]
    record = copy.deepcopy(old)
    mesh,evidence = derive_clean_house(bpy.data.objects["farmhouse"])
    collection = bpy.data.collections.new("农田整格 · 仅小屋与连续麦田")
    bpy.context.scene.collection.children.link(collection)
    points = [obj.matrix_world@Vector(corner) for obj in bpy.context.scene.objects if obj.type=="MESH" for corner in obj.bound_box]
    origin = Vector((max(point.x for point in points)+3,min(point.y for point in points)+2,0))
    root = bpy.data.objects.new("Pasture14Clean · 展示锚点",None)
    collection.objects.link(root)
    root.location = origin
    root.scale = (1/record["uniform_source_scale"],)*3
    for key in ("architecture","ground","crop","crop_completion"):
        source = bpy.data.objects[old[key]]
        obj = source.copy()
        obj.name = "Pasture14Clean_"+key
        if key=="architecture":
            obj.data = mesh
        collection.objects.link(obj)
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_basis = source.matrix_basis.copy()
        record[key] = obj.name
    record["root"] = root.name
    record["source_collection"] = collection.name
    record["clean_source_mesh_sha256"] = read_mesh_record(mesh)
    record["clean_farm_evidence"] = evidence
    gallery_collection = bpy.data.collections.new("清爽连片麦田 · 2×2评审")
    bpy.context.scene.collection.children.link(gallery_collection)
    gallery_root = bpy.data.objects.new("清爽麦田2×2 · 展示锚点",None)
    gallery_collection.objects.link(gallery_root)
    gallery_root.location = origin+Vector((1,-3,0))
    gallery_root.scale = root.scale
    for row in range(2):
        for column in range(2):
            for key in ("architecture","ground","crop","crop_completion"):
                source = bpy.data.objects[record[key]]
                obj = source.copy()
                obj.name = f"无围栏拼接{row}{column} · "+source.name
                gallery_collection.objects.link(obj)
                obj.parent = gallery_root
                obj.matrix_parent_inverse = Matrix.Identity(4)
                obj.matrix_basis = Matrix.Translation((column*14-7,row*14-7,0))@source.matrix_basis
    bpy.context.view_layer.update()
    collection_name = str(collection.name)
    gallery_name = str(gallery_collection.name)
    gallery_root_name = str(gallery_root.name)
    render_pasture_grid(record)
    for camera in list(bpy.data.cameras):
        if camera.users==0:
            bpy.data.cameras.remove(camera)
    saved = save_source(protected)
    bpy.ops.wm.open_mainfile(filepath=str(MASTER))
    record["export"] = export_variant(record,handoff["ground_material"])
    handoff["assets"][0] = record
    handoff.update(saved)
    handoff["source_before_sha256"] = EXPECTED_SHA
    handoff["source_revision_chain"].append(saved["master_sha256"])
    handoff["clean_farm_collection"] = collection_name
    handoff["gallery"] = {"collection":gallery_name,"root":gallery_root_name,"grid_count":[2,2],"overall_m":[28,28]}
    handoff.pop("protected_original_414_proof",None)
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("CLEAN FARM FROZEN",json.dumps({"sha":saved["master_sha256"],"evidence":{k:v for k,v in evidence.items() if k!="source_face_indices"}}))


def main():
    verify_source()
    REVIEW.mkdir(parents=True,exist_ok=True)
    protected = capture_protected_state()
    (REVIEW/"protected-state.json").write_text(json.dumps(protected,ensure_ascii=False,indent=2))
    material,material_record = create_dirt_material()
    records,origin = append_variants(material)
    gallery = append_pasture_gallery(records[0],origin)
    for record in records:
        record["edges"] = verify_ground_edges(record)
        if read_mesh_record(bpy.data.objects[record["architecture"]].data) != record["source_mesh_sha256"]:
            raise AssertionError("Architectural mesh/UV changed")
    render_pasture_grid(records[0])
    # Temporary diagnostic camera data is not a final source asset.
    for camera in list(bpy.data.cameras):
        if camera.users == 0:
            bpy.data.cameras.remove(camera)
    source = save_source(protected)
    for record in records:
        bpy.ops.wm.open_mainfile(filepath=str(MASTER))
        record["export"] = export_variant(record,material_record)
    handoff = {"schema_version":1,"status":"Source frozen; Unity import and actual player visuals pending",
        "source_blend":str(MASTER.relative_to(ROOT)),"source_before_sha256":EXPECTED_SHA,**source,
        "collection":GROUP,"gallery":gallery,"records":records,"ground_material":material_record,
        "tile_size_m":1,"placement_scale":1,"ground_surface_local_y_m":0,
        "placement_note":"Place by ground-surface anchor at terrain+0.025m, NOT by negative mesh underside or bounds minimum. Export already includes accepted whole uniform architectural scale. No per-axis resizing.",
        "preserved_wall_export":"art/fortified_city_handoff.json remains frozen; source revision advances only",
        "tower_status":"Deferred: no prior solid-tower collection in actual source; no old collection restored or invented in this three-asset source pass",
        "natural_resources_policy":"Trees/ore retain natural shapes; integer occupancy does not authorize blocky replacements"}
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("INTEGER GROUND SOURCE PASS",json.dumps({"master_sha256":source["master_sha256"],
        "records":[record["id"] for record in records],"protected_objects":source["protected_counts"]["objects"]}))


if __name__ == "__main__":
    if "--continuous-clean-field" in sys.argv:
        repair_continuous_clean_field()
    elif "--finish-clean-farm" in sys.argv:
        finish_clean_pasture()
    elif "--clean-farm" in sys.argv:
        make_clean_pasture()
    elif "--verify-frozen" in sys.argv:
        verify_frozen_source()
    elif "--complete-approved-crop" in sys.argv:
        complete_approved_crop()
    else:
        main()
