"""Reopen both saved Blender studies and verify geometry and delivery boundaries."""
import hashlib
import json
from pathlib import Path
import sys

import bmesh
import bpy

sys.path.insert(0,str(Path(__file__).resolve().parent))
from boss_models import ROOT, OUTPUT, validate_model

HUMAN_MASTER_HASH = '15c99426bc5d823b526d77330273788a1f9127b70135590462d3c5b83be7aac2'


def verify_saved_candidate(kind):
    """Independently reopen the on-disk file, not the previous build's live scene."""
    path = OUTPUT/kind/(kind+'.blend')
    expected = json.loads((path.parent/'verification.json').read_text())
    actual_hash = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual_hash != expected['blend_sha256']:
        raise ValueError(f'Saved source hash mismatch: {path}')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    body = bpy.data.objects['Body | continuous '+kind]
    result = validate_model(body,kind=='carrier')
    mesh = bmesh.new()
    mesh.from_mesh(body.data)
    result['nonmanifold_skin_edges'] = sum(not edge.is_manifold for edge in mesh.edges)
    mesh.free()
    if result['nonmanifold_skin_edges']:
        raise ValueError(f'{kind}: skin is not closed')
    if any(obj.type=='ARMATURE' for obj in bpy.context.scene.objects):
        raise ValueError(f'{kind}: unexpected rig in static study')
    result['saved_file_reopened'] = True
    result['render_views'] = []
    for view in ('overview','rts','rear'):
        image = path.parent/(view+'.png')
        if not image.exists() or image.stat().st_size < 10000:
            raise ValueError(f'Missing actual render: {image}')
        result['render_views'].append(view)
    return result


def main():
    master=ROOT/'art/characters/human_base/human_base.blend'
    if hashlib.sha256(master.read_bytes()).hexdigest()!=HUMAN_MASTER_HASH:
        raise ValueError('Approved human master differs; do not overwrite it to pass this check')
    reports={kind:verify_saved_candidate(kind) for kind in ('headbutter','carrier')}
    reports['human_master_unchanged']=True
    (OUTPUT/'reopen_verification.json').write_text(json.dumps(reports,indent=2)+'\n')
    print(json.dumps(reports,indent=2))


if __name__=='__main__':
    main()
