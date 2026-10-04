"""Derive solid stone defense towers from approved sources in the formal master.

The user explicitly requested plain rectangular lower piers. Upper meshes retain
their original vertices, UVs and materials; all raw models remain recoverable.
"""
import hashlib
import json
import math
import shutil
import sys
from datetime import datetime
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).parent))
from rectify_gate_tower import (MASTER, ROOT, GALLERY, combine_meshes,
                               capture_originals, verify_originals, verify_joint_rays)

REVIEW = ROOT / "Builds/ArtReview/SolidDefenseTowers"
GROUP = "防御塔 · 实心方正石墩"
SPECS = {
    "GateTower": {"source": "stone fortress 3d model", "name": "炮塔 · 实心方正石墩",
                  "cut": .395, "bounds": (-.341, .341, -.25, .32),
                  "uv": (.210, .555, .230, .588), "tile": (.055, .09)},
    "FireTower": {"source": "fire siege tower", "name": "喷火塔 · 实心方正石墩",
                  "cut": .345, "bounds": (-.252, .443, -.26, .45),
                  "stone_source": "stone fortress 3d model",
                  "uv": (.210, .555, .230, .588), "tile": (.055, .09)},
}


def clip_upper_mesh(source, height):
    """Remove only the lower shaft; interpolate original UVs along the cut boundary."""
    editable = bmesh.new()
    editable.from_mesh(source)
    bmesh.ops.bisect_plane(editable, geom=list(editable.verts)+list(editable.edges)+list(editable.faces),
                           plane_co=(0, 0, height), plane_no=(0, 0, -1),
                           dist=1e-7, clear_outer=True)
    mesh = bpy.data.meshes.new(source.name + "_PreservedUpper")
    editable.to_mesh(mesh)
    editable.free()
    for material in source.materials:
        mesh.materials.append(material)
    return mesh


def build_stone_pier(source, spec):
    """Build a closed rectangular pier with original stone texture patches, no door or props."""
    xmin, xmax, ymin, ymax = spec["bounds"]
    zmax = spec["cut"] + .002
    corners = [(xmin,ymin,0),(xmax,ymin,0),(xmax,ymax,0),(xmin,ymax,0),
               (xmin,ymin,zmax),(xmax,ymin,zmax),(xmax,ymax,zmax),(xmin,ymax,zmax)]
    sides = [(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7),(3,2,1,0)]
    vertices, faces, uvs = [], [], []
    umin,vmin,umax,vmax = spec["uv"]
    for face in sides:
        origin, horizontal, vertical = Vector(corners[face[0]]), Vector(corners[face[1]]), Vector(corners[face[3]])
        horizontal -= origin
        vertical -= origin
        columns = max(1, math.ceil(horizontal.length / spec["tile"][0]))
        rows = max(1, math.ceil(vertical.length / spec["tile"][1]))
        for row in range(rows):
            for column in range(columns):
                offset = len(vertices)
                for du,dv in ((0,0),(1,0),(1,1),(0,1)):
                    vertices.append(origin + horizontal*((column+du)/columns) + vertical*((row+dv)/rows))
                    u = du if column%2 == 0 else 1-du
                    v = dv if row%2 == 0 else 1-dv
                    uvs.append((umin+(umax-umin)*u,vmin+(vmax-vmin)*v))
                faces.append(tuple(offset+i for i in range(4)))
    mesh = bpy.data.meshes.new("SolidStonePier_" + spec["source"])
    mesh.from_pydata(vertices, [], faces)
    for material in source.materials:
        mesh.materials.append(material)
    layer = mesh.uv_layers.new(name=source.uv_layers.active.name)
    for loop in mesh.loops:
        layer.data[loop.index].uv = uvs[loop.vertex_index]
    mesh.update()
    return mesh


def verify_solid_pier(mesh, spec):
    """Sample all four side walls through the full lower height, including the former door."""
    tree = BVHTree.FromPolygons([v.co for v in mesh.vertices],[p.vertices for p in mesh.polygons])
    xmin,xmax,ymin,ymax = spec["bounds"]
    count = 0
    for step in range(1,20):
        z=spec["cut"]*step/20
        for sample in range(1,20):
            x=xmin+(xmax-xmin)*sample/20
            y=ymin+(ymax-ymin)*sample/20
            for origin,direction in (((x,-2,z),(0,1,0)),((x,2,z),(0,-1,0)),
                                     ((-2,y,z),(1,0,0)),((2,y,z),(-1,0,0))):
                if tree.ray_cast(origin,direction,4)[0] is None:
                    raise AssertionError("Unsealed tower lower wall at " + str(origin))
                count += 1
    return count


def derive_tower_mesh(source, spec):
    upper = clip_upper_mesh(source, spec["cut"])
    protected = {tuple(v.co) for v in source.vertices if v.co.z > spec["cut"]+1e-6}
    if not protected.issubset({tuple(v.co) for v in upper.vertices}):
        raise AssertionError("Protected upper vertices changed")
    stone_source = bpy.data.objects[spec["stone_source"]].data if "stone_source" in spec else source
    pier = build_stone_pier(stone_source, spec)
    pier.uv_layers.active.name = source.uv_layers.active.name
    result = combine_meshes([(upper,Matrix.Identity(4)),(pier,Matrix.Identity(4))], spec["name"])
    result["solid_defense_tower"] = True
    return result, {"protected_upper_vertices":len(protected), "closed_surface_rays":verify_solid_pier(result,spec),
                    "base_bounds":spec["bounds"], "base_height":spec["cut"]}


def export_tower_variants():
    """Export master variants and record exact provenance; no geometry is constructed here."""
    from coastal_exports import export_model, OUTPUT
    manifest_path = OUTPUT / "manifest.json"
    manifest = json.loads(manifest_path.read_text())
    for identity,spec in SPECS.items():
        obj = bpy.data.objects[spec["name"]]
        record = export_model(obj, identity)
        record.update({"master_source_object": obj.name,
                       "source_original_object": spec["source"], "friendly_passage": False,
                       "source_master": str(MASTER.relative_to(ROOT)), "solid_defense_tower": True})
        manifest["models"] = [record if item["id"]==identity else item for item in manifest["models"]]
    manifest_path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
    print("SOLID TOWER EXPORTS READY: GateTower, FireTower",flush=True)


def render_preview(obj, identity):
    """Temporary inspection only; never add cameras or lights to the saved master."""
    original_scene = bpy.context.scene
    scene = bpy.data.scenes.new("Temporary solid tower QA")
    bpy.context.window.scene = scene
    clone = obj.copy()
    clone.parent = None
    clone.matrix_world = Matrix.Identity(4)
    scene.collection.objects.link(clone)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_shadows = False
    scene.display.shading.show_specular_highlight = False
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 1.5
    scene.camera = camera
    for view,location in (("front",(1.5,-2,1.4)),("back",(-1.5,2,1.2))):
        camera.location=location
        camera.rotation_euler=(Vector((0,0,.4))-camera.location).to_track_quat("-Z","Y").to_euler()
        scene.render.filepath=str(REVIEW/(identity+"-"+view+".png"))
        bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=original_scene
    bpy.data.scenes.remove(scene)


def main():
    if not bpy.app.background or Path(bpy.data.filepath).resolve()!=MASTER.resolve():
        raise ValueError("Requires background read of the registered formal master")
    if "--export" in sys.argv:
        export_tower_variants()
        return
    if bpy.data.collections.get(GROUP) and "--refresh-variants" not in sys.argv:
        raise ValueError("Solid variants already exist; refuse duplicate rebuild")
    original_hash=hashlib.sha256(MASTER.read_bytes()).hexdigest()
    original_mtime=MASTER.stat().st_mtime_ns
    originals=capture_originals(bpy.context.scene)
    group=bpy.data.collections.get(GROUP)
    if group is None:
        group=bpy.data.collections.new(GROUP)
        bpy.context.scene.collection.children.link(group)
    REVIEW.mkdir(parents=True,exist_ok=True)
    report={"source_master_sha256":original_hash,"towers":{}}
    for index,(identity,spec) in enumerate(SPECS.items()):
        source=bpy.data.objects[spec["source"]]
        mesh,checks=derive_tower_mesh(source.data,spec)
        obj=bpy.data.objects.get(spec["name"])
        if obj is None:
            obj=bpy.data.objects.new(spec["name"],mesh)
            obj.location=(24+index*2.5,13,0)
            group.objects.link(obj)
        else:
            obj.data=mesh
        obj["source_original_object"]=source.name
        obj["unity_asset"]=identity
        obj["friendly_passage"]=False
        report["towers"][identity]=checks
        if identity=="GateTower":
            towers=[tower for tower in bpy.data.collections[GALLERY].all_objects if "tower_start_cell" in tower]
            for tower in towers:
                tower.data=mesh
                tower["friendly_passage"]=False
                tower["solid_defense_tower"]=True
            report["gallery_towers_updated"]=len(towers)
        render_preview(obj,identity)
    report["join_rays"]=sum(verify_joint_rays(collection) for collection in bpy.data.collections[GALLERY].children
                             if any("tower_start_cell" in obj for obj in collection.objects))
    verify_originals(bpy.context.scene,originals)
    report["original_objects_unchanged"]=len(originals)
    if MASTER.stat().st_mtime_ns!=original_mtime or hashlib.sha256(MASTER.read_bytes()).hexdigest()!=original_hash:
        raise RuntimeError("Master changed during background edit; refuse overwrite")
    if "--save-master" in sys.argv:
        backup=REVIEW/("master-before-solid-towers-"+datetime.now().strftime("%Y%m%d-%H%M%S")+".blend")
        shutil.copy2(MASTER,backup)
        bpy.ops.wm.save_as_mainfile(filepath=str(MASTER),compress=True)
        report["backup"]=str(backup.relative_to(ROOT))
        report["master_sha256"]=hashlib.sha256(MASTER.read_bytes()).hexdigest()
    else:
        bpy.ops.wm.save_as_mainfile(filepath=str(REVIEW/"SolidTowers_Candidate.blend"),compress=True)
    (REVIEW/"verification.json").write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print("SOLID TOWERS QA",json.dumps(report,ensure_ascii=False),flush=True)


if __name__=="__main__":
    main()
