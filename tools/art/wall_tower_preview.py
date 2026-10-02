"""Place the imported cannon tower into modular wall bodies without touching end piers."""
import bpy

from wall_layout import plan_wall, plan_tower_slots, verify_tower_slots
from siege_preview import create_material, add_fixture_cube


def trim_wall_bodies(scene, objects, slots, clip_mesh, add_piece, units_per_tile):
    """Subtract tower intervals from wall meshes; preserve original end-pier instances."""
    for obj in list(objects[1:-1]):
        low = obj.location.x
        high = low+max(vertex.co.x for vertex in obj.data.vertices)
        intervals = [(low,high)]
        for slot in slots:
            cut_low,cut_high = slot["start"]*units_per_tile,slot["end"]*units_per_tile
            remaining = []
            for first,last in intervals:
                if last <= cut_low or first >= cut_high:
                    remaining.append((first,last))
                else:
                    if first < cut_low:
                        remaining.append((first,cut_low))
                    if last > cut_high:
                        remaining.append((cut_high,last))
            intervals = remaining
        if intervals == [(low,high)]:
            continue
        for first,last in intervals:
            if last-first > 1e-6:
                mesh = clip_mesh(obj.data,first-low,last-low,"Wall beside tower")
                add_piece(scene,mesh,"Wall_Middle_TowerJoin",first)
        bpy.data.objects.remove(obj,do_unlink=True)


def add_tower_examples(root, review, parts, build_run, create_stage, clip_mesh, add_piece):
    """Append examples to the existing wall kit, using the real cannon-tower art only."""
    asset_path = root / "art/models/gate-tower-pasture/GateTowerPasture.blend"
    with bpy.data.libraries.load(str(asset_path)) as (available,loaded):
        loaded.objects = ["Original — stone fortress 3d model"]
    source = loaded.objects[0]
    source.name = "Original cannon tower — preserved"
    bpy.data.scenes["00_ModularWall_Kit"].collection.objects.link(source)
    source.hide_render = True
    source.hide_viewport = True
    examples = (
        ("10_Tower_Middle",10,(("cannon",4),)),
        ("11_Tower_OddCell",10,(("cannon",3),)),
        ("12_Tower_MinimumRun",4,(("cannon",1),)),
        ("13_Tower_LeftPierRejected",10,(("cannon",0),)),
        ("14_Tower_RightPierRejected",10,(("cannon",8),)),
        ("15_Tower_Multiple",12,(("cannon",3),("cannon",7))),
        ("16_Tower_OverlapRejected",10,(("cannon",3),("cannon",4))))
    report = {"tests": verify_tower_slots(),"examples": {}}
    for name,length,requested in examples:
        wall = plan_wall(length)
        placement = plan_tower_slots(wall,requested)
        scene = bpy.data.scenes.new(name)
        bpy.context.window.scene = scene
        objects = build_run(scene,wall,parts,{})
        create_stage(scene,wall)
        if placement["valid"]:
            trim_wall_bodies(scene,objects,placement["slots"],clip_mesh,add_piece,.5)
            for slot in placement["slots"]:
                tower = bpy.data.objects.new("CannonTower_2x2",source.data)
                scene.collection.objects.link(tower)
                width = max(v.co.x for v in source.data.vertices)-min(v.co.x for v in source.data.vertices)
                tower.scale = (1/width,)*3
                tower.location.x = (slot["start"]+1)*.5
                tower["tower_start_cell"] = slot["start"]
        else:
            material = create_material("Rejected tower footprint",(.7,.12,.09))
            for _,start in requested:
                add_fixture_cube("Cannot replace end pier or occupied cells",((start+1)*.5,0,.006),(1,1,.01),material)
        scene.render.filepath = str(review / f"{name}.png")
        bpy.ops.render.render(write_still=True)
        report["examples"][name] = {"length":length,**placement}
    return report


def verify_tower_meshes(report):
    """Re-opened asset checks: two invariant piers and no retained wall under tower slots."""
    for name,placement in report["examples"].items():
        scene = bpy.data.scenes[name]
        piers = [o for o in scene.objects if o.name.startswith("Pier_")]
        assert len(piers) == 2 and all(tuple(o.scale) == (1,1,1) for o in piers)
        towers = [o for o in scene.objects if "tower_start_cell" in o]
        assert len(towers) == len(placement["slots"])
        for slot in placement["slots"]:
            for obj in scene.objects:
                if not obj.get("wall_module"):
                    continue
                low = obj.location.x+min(v.co.x for v in obj.data.vertices)
                high = obj.location.x+max(v.co.x for v in obj.data.vertices)
                assert high <= slot["start"]*.5+1e-5 or low >= slot["end"]*.5-1e-5
    return verify_tower_slots()
