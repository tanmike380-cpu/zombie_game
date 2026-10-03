"""Export accepted Blender wall modules as destructible, tower-replaced Unity sections."""
import json
import math
import sys
from pathlib import Path
import bpy
from mathutils import Matrix

sys.path.insert(0, str(Path(__file__).parent))
from modular_stone_wall import clip_mesh, add_piece
from coastal_exports import ROOT, OUTPUT, export_model


def build_piece(scene, parts, start, end, length):
    """Intersect one health section with the original repeating body / two intact piers."""
    left, body, right = parts
    width = lambda mesh: max(v.co.x for v in mesh.vertices)
    left_width, right_width, pitch = width(left), width(right), width(body)
    spans = [(0, left_width, left), (length-right_width, length, right)]
    cursor = left_width
    while cursor < length-right_width-.00001:
        last = min(cursor+pitch, length-right_width)
        spans.append((cursor, last, body));cursor=last
    pieces = []
    for low, high, mesh in spans:
        first, last = max(start,low), min(end,high)
        if last-first <= .00001:
            continue
        cut = clip_mesh(mesh, first-low, last-low, 'Authored wall slice')
        pieces.append(add_piece(scene, cut, 'Imported wall section', first-start))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in pieces:obj.select_set(True)
    bpy.context.view_layer.objects.active=pieces[0]
    bpy.ops.object.join()
    result=pieces[0]
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    return result


def export_run(scene, parts, run_id, length, origin, sideways, slots, records, regions):
    """Keep full end piers, replace only middle intervals, split damage sections at cell edges."""
    protected=math.ceil(max(max(v.co.x for v in parts[i].vertices) for i in (0,2)))
    assert all(start>=protected and start+width<=length-protected for start,width in slots)
    for (start,width),(following,_) in zip(slots,slots[1:]):assert start+width<=following
    cuts={0,protected,length-protected,length}
    cuts.update(range(protected,length-protected,2))
    for start,width in slots:cuts.update((start,start+width))
    cuts=sorted(cuts)
    for index,(start,end) in enumerate(zip(cuts,cuts[1:])):
        if any(start>=first and end<=first+width for first,width in slots):continue
        obj=build_piece(scene,parts,start,end,length)
        identity=f'WallRun{run_id}_{index:02d}'
        record=export_model(obj,identity,shared_texture='../Wall/tripo_image_2a6beea7_0.png')
        # All sections share the source texture instead of copying it into every imported prefab.
        records.append(record)
        center=(start+end)*.5
        regions.append({'asset':identity,'x':origin[0]+(0 if sideways else center),
                        'z':origin[1]+(center if sideways else 0),
                        'width':5 if sideways else end-start,'depth':end-start if sideways else 5,
                        'sideways':sideways,'pier':start<protected or end>length-protected})
        obj.location=(origin[0]+(0 if sideways else start),origin[1]+(start if sideways else 0),0)
        if sideways:obj.rotation_euler.z=math.pi/2


def main():
    """No new architecture: re-use saved meshes / UVs and uniformly enlarge the whole kit."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source=ROOT/'art/models/modular-stone-wall/ModularStoneWall.blend'
    with bpy.data.libraries.load(str(source)) as (_,loaded):
        loaded.meshes=['StonePier_Left','StoneWall_Middle','StonePier_Right']
    parts=[]
    for mesh in loaded.meshes:
        if mesh is None:raise ValueError('Accepted Blender wall module is missing')
        mesh=mesh.copy();mesh.transform(Matrix.Scale(18,4));parts.append(mesh)
    scene=bpy.data.scenes.new('Coastal — Original Modular Walls')
    bpy.context.window.scene=scene
    records=[];regions=[]
    export_run(scene,parts,'Front',74,(-127,-55),False,((20,10),(44,10)),records,regions)
    export_run(scene,parts,'Left',67,(-124.5,-124.5),True,(),records,regions)
    export_run(scene,parts,'Right',67,(-55.5,-124.5),True,(),records,regions)
    with bpy.data.libraries.load(str(source)) as (_,loaded):
        loaded.objects=['Original cannon tower — preserved']
    original_tower=loaded.objects[0]
    if original_tower is None:raise ValueError('Accepted cannon tower source is missing')
    tower_width=max(v.co.x for v in original_tower.data.vertices)-min(v.co.x for v in original_tower.data.vertices)
    for x in (-102,-78):
        tower=bpy.data.objects.new('Original cannon tower in wall slot',original_tower.data)
        scene.collection.objects.link(tower);tower.location=(x,-55,0);tower.scale=(10/tower_width,)*3
    layout={'source_blend':str(source.relative_to(ROOT)), 'uniform_source_scale':18,
            'note':'Fortress-scale footprint; original end piers protected; no wall mesh beneath tower slots.',
            'regions':regions}
    (ROOT/'Assets/_Game/Resources/CoastalWallLayout.json').write_text(json.dumps(layout,indent=2))
    manifest=json.loads((OUTPUT/'manifest.json').read_text())
    manifest['models']=[record for record in manifest['models'] if not record['id'].startswith('WallRun')]+records
    (OUTPUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
    # Source master stays intact; this is the inspectable assembled import, not another replacement design.
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/models/modular-stone-wall/CoastalWallAssembly.blend'))
    print(f'MODULAR UNITY EXPORT PASS: {len(regions)} damage sections, source piers unchanged, 2 tower holes',flush=True)


if __name__=='__main__':main()
