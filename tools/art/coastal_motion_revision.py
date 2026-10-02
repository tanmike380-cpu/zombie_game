"""Revise poses only on a snapshot; keep imported geometry, skin weights and colours intact."""
import math
import json
from pathlib import Path
import sys
import bpy
sys.path.insert(0,str(Path(__file__).parent))
from coastal_exports import ROOT, OUTPUT, export_model, create_basic_actions, rotate_bone
from siege_motion import start_action, key_pose, WHEELS, TURRET_PIVOT


def create_heavy_walk(rig):
    """Slow opposing steps, modest knee flex and counter-swing; no running hunch or root slide."""
    action=start_action(rig,'Heavy_Planted_Walk')
    for frame in range(49):
        phase=frame/48*math.tau
        for side,sign in (('Left',1),('Right',-1)):
            swing=math.sin(phase)*sign
            rotate_bone(rig,side+'_UpperLeg',swing*.28)
            rotate_bone(rig,side+'_LowerLeg',max(0,-swing)*.35)
            rotate_bone(rig,side+'_Foot',-swing*.12)
            rotate_bone(rig,side+'_UpperArm',-swing*.07)
            rotate_bone(rig,side+'_LowerArm',-.12)
        rotate_bone(rig,'Spine',math.sin(phase)*.014)
        key_pose(rig,frame+1,[bone.name for bone in rig.pose.bones])
    return action


def create_melee_attack(rig):
    """Readable shoulder-led two-arm strike followed by recoil, on original rig channels."""
    action=start_action(rig,'Zombie_Readable_Strike')
    for frame in range(31):
        phase=frame/30
        reach=math.sin(phase*math.pi)
        for side in ('Left','Right'):
            rotate_bone(rig,side+'_UpperArm',-reach*1.2)
            rotate_bone(rig,side+'_LowerArm',-.45+.3*reach)
        rotate_bone(rig,'Spine',reach*.18)
        key_pose(rig,frame+1,[bone.name for bone in rig.pose.bones])
    return action


def main():
    bpy.context.scene.render.fps=30
    manifest=json.loads((OUTPUT/'manifest.json').read_text())
    with bpy.data.libraries.load(str(ROOT/'art/models/tripo-siege/SiegeAnimations.blend')) as (_,loaded):
        loaded.objects=['头槌巨尸.001','Greek Fire siege.001','tripo_node_bdfaf021.001']
    ram,greek,mesh=loaded.objects
    for obj in (greek,mesh):bpy.context.scene.collection.objects.link(obj)
    markers={'TurretPivot':TURRET_PIVOT,'Muzzle':(0,.5,.465),'MuzzleForward':(0,.6,.465)}
    for name,x,y in WHEELS:markers['Axle_'+name]=(x,y,.137)
    for name,point in markers.items():
        marker=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(marker);marker.parent=greek;marker.location=point
    replacements={'GreekFire':export_model(greek,'GreekFire',{'Attack':greek.animation_data.action})}
    for name,identity in [('Hammer zombie','Headbutter'),('wooden cage zombie','CageBoss'),('zombie 3d model','Walker')]:
        rig=bpy.data.objects[name]
        actions=create_basic_actions(rig,identity)
        actions['Attack']=ram.animation_data.action if identity=='Headbutter' else create_melee_attack(rig)
        if identity!='Walker':actions['Run']=create_heavy_walk(rig)
        replacements[identity]=export_model(rig,identity,actions)
    manifest['models']=[replacements.get(record['id'],record) for record in manifest['models']]
    (OUTPUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
    print('MOTION REVISION PASS: 4 assets, original geometry and skin preserved',flush=True)


if __name__=='__main__':main()
