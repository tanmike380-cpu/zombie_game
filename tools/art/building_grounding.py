"""Author opt-in building ground transitions; never replace frozen source variants.

The old architecture, including steps and wall footings, is reused byte-for-byte.
Logical grid occupancy is metadata. Only an independent paper-thin PBR surface
with an authored vertex-alpha border is added to the new review variants.
"""
import copy
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

sys.path.insert(0, str(Path(__file__).parent))
import grid_footprints as grid
from fortified_city_assembly import capture_protected_state, verify_protected_state, read_mesh_record, hash_file

ROOT = Path(__file__).resolve().parents[2]
MASTER = ROOT / "art/Blender主文件/ZombieGame.blend"
EXPECTED_SHA = "69704fa392d17bcc253d669699b0001031c91884cf8fdc544d582e00d3e55d2d"
REVIEW = ROOT / "Builds/ArtReview/BuildingGrounding-v1"
OUTPUT = ROOT / "art/models/grounding-review"
HANDOFF = ROOT / "art/grounding_review_handoff.json"
COLLECTION = "建筑接地评审 · 基地石庭＋工坊夯土软边"
SPECS = [
    {"old_id":"Headquarters12", "id":"HeadquartersGroundedReview", "source_id":"Headquarters",
     "pack":"cobblestone_floor_02", "radius_m":1.1, "fade_m":1.1},
    {"old_id":"Workshop8", "id":"WorkshopGroundedReview", "source_id":"Workshop",
     "pack":"dirt", "radius_m":.9, "fade_m":.9},
]


def verify_entry_source():
    """Require the frozen general source, never the live unsaved Zombie document."""
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER:
        raise RuntimeError("Grounding authoring must run in background on the formal general master")
    if hash_file(MASTER)!=EXPECTED_SHA or len(bpy.data.objects)!=486:
        raise RuntimeError("General source changed; require a new protection audit before authoring")
    if COLLECTION in bpy.data.collections:
        raise RuntimeError("Grounding review collection already exists; refuse duplicate authoring")


def create_surface_material(pack_id):
    """Load licensed existing PBR maps and explicitly wire vertex alpha in Blender."""
    manifest = json.loads((ROOT/"art/terrain_material_manifest.json").read_text())
    pack = next(item for item in manifest["packs"] if item["id"]==pack_id)
    folder = OUTPUT/"textures"
    folder.mkdir(parents=True,exist_ok=True)
    channels,images = {},{}
    for channel,md5 in zip(("diff","nor_gl","rough"),pack["md5"]):
        source = ROOT/"Assets/_Game/Resources/FreeEnvironment"/pack_id/f"{pack_id}_{channel}_1k.jpg"
        if hashlib.md5(source.read_bytes()).hexdigest()!=md5:
            raise AssertionError(f"Licensed PBR source changed: {source}")
        destination = folder/source.name
        shutil.copy2(source,destination)
        image = bpy.data.images.load(str(destination),check_existing=False)
        image.name = "GroundReview_"+pack_id+"_"+channel
        image.colorspace_settings.name = "sRGB" if channel=="diff" else "Non-Color"
        image.pack()
        images[channel] = image
        channels[channel] = {"file":str(destination.relative_to(ROOT)),"sha256":hash_file(destination),
            "colorspace":image.colorspace_settings.name,"resolution":list(image.size)}
    material = bpy.data.materials.new("GroundReview_"+pack_id)
    material.use_nodes = True
    material.diffuse_color = (.48,.42,.31,1) if pack_id=="cobblestone_floor_02" else (.27,.22,.15,1)
    shader = next(node for node in material.node_tree.nodes if node.type=="BSDF_PRINCIPLED")
    shader.inputs["Metallic"].default_value = 0
    nodes,links = material.node_tree.nodes,material.node_tree.links
    textures = {}
    for channel,image in images.items():
        node = nodes.new("ShaderNodeTexImage")
        node.image = image
        node.extension = "REPEAT"
        textures[channel] = node
    links.new(textures["diff"].outputs["Color"],shader.inputs["Base Color"])
    links.new(textures["rough"].outputs["Color"],shader.inputs["Roughness"])
    normal = nodes.new("ShaderNodeNormalMap")
    normal.inputs["Strength"].default_value = .35
    links.new(textures["nor_gl"].outputs["Color"],normal.inputs["Color"])
    links.new(normal.outputs["Normal"],shader.inputs["Normal"])
    alpha = nodes.new("ShaderNodeVertexColor")
    alpha.layer_name = "GroundEdgeOpacity"
    links.new(alpha.outputs["Alpha"],shader.inputs["Alpha"])
    if hasattr(material,"surface_render_method"):
        values = {item.identifier for item in material.bl_rna.properties["surface_render_method"].enum_items}
        if "BLENDED" in values:
            material.surface_render_method = "BLENDED"
    metadata = {"name":material.name,"license":manifest["license"],
        "author":manifest["authors"][pack_id],"source":pack["source"],"license_url":manifest["license_url"],
        "channels":channels,"metallic":0,"normal_strength":.35,"uv_period_m":2,
        "base_color_multiplier":[1,1,1,1],"alpha_mode":"blend","vertex_alpha_channel":"COLOR0.a",
        "blender_color_attribute":"GroundEdgeOpacity","shadow_casting":False,"receive_shadows":True,
        "double_sided":False,"z_write":False,
        "note":"Dedicated opt-in ground material: real existing PBR maps plus authored vertex alpha; not the old opaque grid slab material."}
    return material,metadata


def read_edge_opacity(x,y,half_width,half_depth,radius,fade_width):
    """Smooth rounded footprint with a small organic edge variation, inside occupancy."""
    qx,qy = abs(x)-(half_width-radius),abs(y)-(half_depth-radius)
    distance = math.hypot(max(qx,0),max(qy,0))+min(max(qx,qy),0)-radius
    variation = .08*math.sin(x*2.3+y*.8)+.055*math.sin(y*3.1-x*.7)
    value = max(0,min(1,(-distance-.15+variation)/fade_width))
    return value*value*(3-2*value)


def create_surface_mesh(identity,footprint,material,spec):
    """Make an open horizontal surface: no thickness, vertical sides or new collision."""
    width,depth = footprint
    columns,rows = round(width/.2),round(depth/.2)
    vertices = [(column*width/columns-width/2,row*depth/rows-depth/2,-.005)
                for row in range(rows+1) for column in range(columns+1)]
    faces = []
    for row in range(rows):
        for column in range(columns):
            first = row*(columns+1)+column
            faces.append((first,first+1,first+columns+2,first+columns+1))
    mesh = bpy.data.meshes.new(identity+"_ThinFeatherSurface")
    mesh.from_pydata(vertices,[],faces)
    mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="UVMap")
    color = mesh.color_attributes.new(name="GroundEdgeOpacity",type="FLOAT_COLOR",domain="CORNER")
    mesh.color_attributes.active_color = color
    for face in mesh.polygons:
        for index in face.loop_indices:
            co = mesh.vertices[mesh.loops[index].vertex_index].co
            uv.data[index].uv = (co.x/2,co.y/2)
            opacity = read_edge_opacity(co.x,co.y,width/2,depth/2,spec["radius_m"],spec["fade_m"])
            color.data[index].color = (1,1,1,opacity)
    mesh.update()
    return mesh


def append_review_variants(records,materials):
    """Place new variants in free master-layout space; reuse exact architecture data/matrices."""
    collection = bpy.data.collections.new(COLLECTION)
    bpy.context.scene.collection.children.link(collection)
    points = [obj.matrix_world@Vector(corner) for obj in bpy.context.scene.objects
              if obj.type=="MESH" for corner in obj.bound_box]
    origin = Vector((max(point.x for point in points)+3,min(point.y for point in points)+2,0))
    results = []
    for index,spec in enumerate(SPECS):
        previous = next(record for record in records if record["id"]==spec["old_id"])
        root = bpy.data.objects.new(spec["id"]+" · 展示锚点",None)
        collection.objects.link(root)
        root.location = origin+Vector((index*2.2,0,0))
        root.scale = (1/previous["uniform_source_scale"],)*3
        root["logical_footprint_m"] = previous["grid"]
        root["occupancy_is_not_visible_platform"] = True
        old = bpy.data.objects[previous["architecture"]]
        architecture = old.copy()
        architecture.name = spec["id"]+"_OriginalArchitecture"
        collection.objects.link(architecture)
        architecture.parent = root
        architecture.matrix_parent_inverse = Matrix.Identity(4)
        architecture.matrix_basis = old.matrix_basis.copy()
        ground = bpy.data.objects.new(spec["id"]+"_FeatherGround",
            create_surface_mesh(spec["id"],previous["grid"],materials[spec["pack"]][0],spec))
        collection.objects.link(ground)
        ground.parent = root
        if hasattr(ground,"visible_shadow"):
            ground.visible_shadow = False
        record = copy.deepcopy(previous)
        record.update({"id":spec["id"],"source_id":spec["source_id"],"root":root.name,
            "architecture":architecture.name,"ground":ground.name,"source_collection":collection.name,
            "previous_preview_asset_id":previous["id"],"previous_export":previous["export"]["file"],
            "previous_export_sha256":previous["export"]["sha256"],"old_architecture_object":old.name,
            "architecture_matrix_unchanged":True,"root_registration_to_previous":"identity; same local origin and same +0.025m terrain offset",
            "logical_footprint_m":previous["grid"],"ground_surface_source_z_m":-.005,
            "grid_ground_surface_m":-.005,
            "unity_ground_offset_m":.025,"ground_height_above_flat_terrain_m":.020,
            "removed_original_architecture_faces":0,"removed_original_architecture_vertices":0,
            "removed_from_new_variant_only":previous["ground"],"visual_ground_thickness_m":0,
            "ground_material":materials[spec["pack"]][1],"edge_fade_m":spec["fade_m"],
            "ground_shadow_casting":False,"ground_receive_shadows":True})
        record.pop("export_folder",None)
        record.pop("edges",None)
        results.append(record)
    bpy.context.view_layer.update()
    return results


def create_preview_ground(scene):
    """A basic licensed-grass test surface at terrain height, not a new world asset."""
    mesh = bpy.data.meshes.new("Temporary grounding preview terrain")
    mesh.from_pydata([(-23,-15,-.025),(23,-15,-.025),(23,15,-.025),(-23,15,-.025)],[],[(0,1,2,3)])
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        point = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (point.x/3,point.y/3)
    material = bpy.data.materials.new("Temporary licensed grass preview")
    material.use_nodes = True
    shader = next(node for node in material.node_tree.nodes if node.type=="BSDF_PRINCIPLED")
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = bpy.data.images.load(str(ROOT/"Assets/_Game/Resources/FreeEnvironment/grass_path_2/grass_path_2_diff_1k.jpg"),check_existing=False)
    material.node_tree.links.new(texture.outputs["Color"],shader.inputs["Base Color"])
    shader.inputs["Roughness"].default_value = .95
    mesh.materials.append(material)
    obj = bpy.data.objects.new("Temporary terrain-height comparison plane",mesh)
    scene.collection.objects.link(obj)


def render_comparisons(records,previous_records):
    """Use identical temporary camera/lighting for before/after; preserve authored scene state."""
    original_scene = bpy.context.window.scene
    before_data = {key:set(getattr(bpy.data,key)) for key in ("objects","meshes","materials","images","worlds","lights","cameras","scenes")}
    for phase in ("before","after"):
        scene = bpy.data.scenes.new("Temporary grounding "+phase)
        bpy.context.window.scene = scene
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 16
        scene.cycles.use_denoising = True
        scene.render.resolution_x,scene.render.resolution_y = 1500,1000
        scene.render.resolution_percentage = 100
        scene.view_settings.view_transform = "AgX"
        world = bpy.data.worlds.new("Temporary grounding diagnostic world")
        world.use_nodes = True
        background = next(node for node in world.node_tree.nodes if node.type=="BACKGROUND")
        background.inputs["Color"].default_value = (.5,.55,.62,1)
        background.inputs["Strength"].default_value = .7
        scene.world = world
        sun = bpy.data.objects.new("Temporary grounding diagnostic sun",bpy.data.lights.new("Temporary grounding sun","SUN"))
        scene.collection.objects.link(sun)
        sun.rotation_euler = (.45,-.55,-.35)
        sun.data.energy = 2
        sun.data.angle = .08
        create_preview_ground(scene)
        for index,record in enumerate(records):
            selected = record if phase=="after" else next(item for item in previous_records if item["id"]==record["previous_preview_asset_id"])
            for key in ("architecture","ground"):
                source = bpy.data.objects[selected[key]]
                obj = source.copy()
                obj.parent = None
                scene.collection.objects.link(obj)
                obj.matrix_world = Matrix.Translation((-8 if index==0 else 8,0,0))@source.matrix_basis
        camera = bpy.data.objects.new("Temporary grounding comparison camera",bpy.data.cameras.new("Temporary grounding comparison camera"))
        scene.collection.objects.link(camera)
        scene.camera = camera
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 36
        for view,location in (("isometric",(20,-30,30)),("top",(0,0,45))):
            camera.location = location
            camera.rotation_euler = (Vector((0,0,1))-camera.location).to_track_quat("-Z","Y").to_euler()
            scene.render.filepath = str(REVIEW/f"{phase}-{view}.png")
            bpy.ops.render.render(write_still=True)
        bpy.context.window.scene = original_scene
        for obj in list(scene.objects):
            bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(scene)
    for key in ("meshes","materials","images","worlds","lights","cameras"):
        for item in set(getattr(bpy.data,key))-before_data[key]:
            if item.users==0:
                getattr(bpy.data,key).remove(item)


def verify_alpha_roundtrip(record):
    """Check actual imported FBX color alpha, not just its material declaration."""
    ground = bpy.data.objects[record["ground"]]
    colors = ground.data.color_attributes.active_color
    if colors is None:
        raise AssertionError("FBX roundtrip lost ground vertex colors")
    tree = KDTree(len(ground.data.vertices))
    for vertex in ground.data.vertices:
        point = ground.matrix_world@vertex.co
        tree.insert(point,vertex.index)
    tree.balance()
    values = {}
    for loop in ground.data.loops:
        color_index = loop.vertex_index if colors.domain=="POINT" else loop.index
        values.setdefault(loop.vertex_index,[]).append(colors.data[color_index].color[3])
    maximum = 0
    for x,y,alpha in record["authored_alpha_samples"]:
        _,index,distance = tree.find(Vector((-x,-y,-.005)))
        if distance>.0001:
            raise AssertionError("FBX alpha sample no longer has the authored ground position")
        maximum = max(maximum,min(abs(alpha-value) for value in values[index]))
    if maximum>1/255+.00001:
        raise AssertionError(f"FBX altered vertex opacity: {maximum}")
    return {"passed":True,"channel":"COLOR0.a","maximum_alpha_error":maximum,
            "sample_count":len(record["authored_alpha_samples"])}


def inspect_variant(record):
    """Prove house preservation and soft open surface before saving."""
    architecture = bpy.data.objects[record["architecture"]]
    old = bpy.data.objects[record["old_architecture_object"]]
    if read_mesh_record(architecture.data)!=read_mesh_record(old.data) or architecture.matrix_basis!=old.matrix_basis:
        raise AssertionError("Grounding variant altered original architecture or its local registration")
    ground = bpy.data.objects[record["ground"]]
    color = ground.data.color_attributes.active_color
    alpha_by_vertex = {}
    for loop in ground.data.loops:
        alpha_by_vertex[loop.vertex_index] = color.data[loop.index].color[3]
    alphas = list(alpha_by_vertex.values())
    record["authored_alpha_samples"] = [[vertex.co.x,vertex.co.y,alpha_by_vertex[vertex.index]] for vertex in ground.data.vertices]
    if min(alphas)!=0 or max(alphas)!=1 or not any(0<value<1 for value in alphas):
        raise AssertionError("Grounding surface has no genuine feathered opacity transition")
    if any(abs(vertex.co.z+.005)>.000001 for vertex in ground.data.vertices):
        raise AssertionError("Grounding surface is not the declared zero-thickness plane")
    return {"architecture_geometry_uv_materials_unchanged":True,"architecture_local_matrix_unchanged":True,
        "original_architecture_faces_removed":0,"logical_footprint_m":record["grid"],
        "ground_vertices":len(ground.data.vertices),"ground_triangles":sum(len(face.vertices)-2 for face in ground.data.polygons),
        "ground_thickness_m":0,"ground_surface_source_z_m":-.005,"alpha_min":min(alphas),"alpha_max":max(alphas),
        "alpha_transition_vertices":sum(0<value<1 for value in alphas),
        "closed_plate_sidewalls":False,"passed":True}


def author_grounding_review():
    """Protect, author, render, freeze and export the independent review source."""
    verify_entry_source()
    REVIEW.mkdir(parents=True,exist_ok=True)
    previous_handoff = json.loads((ROOT/"art/grid_footprint_handoff.json").read_text())
    protected = capture_protected_state()
    (REVIEW/"protected-486.json").write_text(json.dumps(protected,ensure_ascii=False,indent=2))
    materials = {spec["pack"]:create_surface_material(spec["pack"]) for spec in SPECS}
    records = append_review_variants(previous_handoff["assets"],materials)
    for record in records:
        record["source_validation"] = inspect_variant(record)
    render_comparisons(records,previous_handoff["assets"])
    grid.REVIEW,grid.EXPECTED_SHA = REVIEW,EXPECTED_SHA
    saved = grid.save_source(protected)
    grid.OUTPUT = OUTPUT
    for record in records:
        bpy.ops.wm.open_mainfile(filepath=str(MASTER))
        record["export"] = grid.export_variant(record,record["ground_material"])
        record["export"]["vertex_alpha_roundtrip"] = verify_alpha_roundtrip(record)
        record.pop("authored_alpha_samples")
    handoff = {"schema_version":1,"status":"Blender source frozen; Unity visual review pending",
        "source_blend":str(MASTER.relative_to(ROOT)),"master_file":str(MASTER.relative_to(ROOT)),
        "source_before_sha256":EXPECTED_SHA,**saved,"collection":COLLECTION,"assets":records,
        "source_revision_chain":previous_handoff["source_revision_chain"]+[saved["master_sha256"]],
        "protected_prior_486_proof":{"passed":True,"counts":saved["protected_counts"],
            "snapshot":str((REVIEW/"protected-486.json").relative_to(ROOT))},
        "old_grid_handoff_untouched":"art/grid_footprint_handoff.json",
        "ground_shader_contract":"Only new ground uses source vertex COLOR0.a as smooth alpha, real PBR channels, ZWrite Off, Cast Off, Receive On. Architecture remains opaque. No old material or render pipeline migration.",
        "placement_note":"Same root origin and whole placement as prior HQ/Workshop, terrain+0.025m. Original architecture local matrix is identical. New ground local Y=-0.005m after axis conversion; top at terrain+0.020m. Logical occupancy does not require a visible slab.",
        "source_previews":str(REVIEW.relative_to(ROOT)),"live_unsaved_zombie_untouched":True}
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("GROUNDING SOURCE FROZEN",json.dumps({"master_sha256":saved["master_sha256"],"exports":[record["export"]["file"] for record in records]}))


def verify_frozen_source():
    """Reopen the final master independently and check protected art and old exports."""
    handoff = json.loads(HANDOFF.read_text())
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER or hash_file(MASTER)!=handoff["master_sha256"]:
        raise AssertionError("Grounding source no longer matches its handoff")
    counts = verify_protected_state(json.loads((REVIEW/"protected-486.json").read_text()))
    results = []
    for record in handoff["assets"]:
        result = inspect_variant(record)
        record.pop("authored_alpha_samples")
        if hash_file(ROOT/record["export"]["file"])!=record["export"]["sha256"]:
            raise AssertionError("New grounding FBX changed after roundtrip")
        results.append({"id":record["id"],**result,
            "roundtrip":record["export"]["roundtrip"],"alpha_roundtrip":record["export"]["vertex_alpha_roundtrip"]})
    frozen_grid = json.loads((ROOT/"art/grid_footprint_handoff.json").read_text())
    old_exports = [{"file":asset["export"]["file"],"sha256":asset["export"]["sha256"]} for asset in frozen_grid["assets"]]
    old_exports.append({"file":"art/models/fortified-city/FortifiedCityWalls.fbx",
        "sha256":"47b89d7734871bbe0dd000cdb0eebd894e13d43e69698e52bd6cd8ce0ee05e2d"})
    for old in old_exports:
        if hash_file(ROOT/old["file"])!=old["sha256"]:
            raise AssertionError(f"Frozen original export changed: {old['file']}")
    proof = {"passed":True,"source_sha256":handoff["master_sha256"],"protected_prior_486_counts":counts,
        "variants":results,"frozen_exports_unchanged":old_exports,
        "lighting_cameras_worlds_old_layout_preserved":True,"live_unsaved_zombie_untouched":True,
        "no_original_architecture_geometry_was_cut":True,"unity_visual_review_pending":True}
    report = REVIEW/"final-source-verification.json"
    report.write_text(json.dumps(proof,ensure_ascii=False,indent=2))
    handoff["protected_prior_486_proof"].update({"passed":True,"report":str(report.relative_to(ROOT))})
    handoff["frozen_export_proof"] = {"passed":True,"report":str(report.relative_to(ROOT))}
    HANDOFF.write_text(json.dumps(handoff,ensure_ascii=False,indent=2))
    print("GROUNDING FINAL SOURCE PASS",json.dumps(proof))


if __name__=="__main__":
    if "--verify-frozen" in sys.argv:
        verify_frozen_source()
    else:
        author_grounding_review()
