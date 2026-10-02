"""Export the user's imported roster, preserving source rigs, UVs and baked colour.

Run on an autosave COPY, never on the live authoring scene. Reuse approved actions
where bone names match; draft motions affect poses only, not rest bones or weights.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from siege_motion import start_action, rotate_bone as rotate_source_bone, key_pose
from tripo_model_io import resolve_humanoid_bone

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/_Game/Art/CoastalImports"
STATIC = {"BZT barrier":"Barrier", "BZT wooden workshop":"Workshop",
          "BZT Main base1":"Headquarters", "stone wall":"Wall",
          "stone fortress 3d model":"GateTower", "farmhouse":"Pasture",
          "fire siege tower":"FireTower", "common zombie":"WalkerUnrigged"}
RIGGED = {"BZT archer Elite":"Archer", "Hammer zombie":"Headbutter",
          "Hunger Dog":"Hound", "wooden cage zombie":"CageBoss",
          "medieval soldier 3d model":"Soldier", "zombie 3d model":"Walker", "Explode zombie":"ExploderLatest"}


def rotate_bone(rig, name, angle):
    """Resolve original Tripo or Mixamo names without renaming the imported skeleton."""
    if name not in rig.pose.bones:
        name=resolve_humanoid_bone(rig,name).name
    rotate_source_bone(rig,name,angle)


def import_actions(path, names):
    with bpy.data.libraries.load(str(path)) as (available, loaded):
        loaded.actions = [name for name in names if name in available.actions]
    return {name: action for name, action in zip([n for n in names if n in available.actions], loaded.actions)}


def create_basic_actions(rig, identity):
    """Small original-rig locomotion/attack fallbacks; rigid kit is not re-skinned."""
    actions = {}
    humanoid = "Left_UpperLeg" in rig.pose.bones or 'mixamorig:LeftUpLeg' in rig.pose.bones
    for name, frames in (("Idle",40),("Run",24),("Attack",30)):
        action = start_action(rig, identity+"_"+name)
        for frame in range(frames+1):
            phase = frame/frames*math.tau
            if humanoid:
                for side, sign in (("Left",1),("Right",-1)):
                    if name == "Run":
                        rotate_bone(rig,side+"_UpperLeg",math.sin(phase)*sign*.38)
                        rotate_bone(rig,side+"_LowerLeg",max(0,-math.sin(phase)*sign)*.65)
                    elif name == "Attack":
                        rotate_bone(rig,side+"_UpperArm",-.6*math.sin(frame/frames*math.pi))
                        rotate_bone(rig,side+"_LowerArm",-.4*math.sin(frame/frames*math.pi))
                rotate_bone(rig,"Spine",.02*math.sin(phase) if name=="Idle" else .12*math.sin(phase))
                if identity=='Archer' and name=='Attack':
                    from tripo_crossbow import pose_arm
                    release=max(0,math.sin(phase))*.025
                    for side,position in (('Left',(.13,-.24,.72)),('Right',(-.10,-.035-release,.74))):
                        hand=rig.data.bones[side+'_Hand'].matrix_local.copy()
                        hand.translation=Vector(position)
                        pose_arm(rig,side,hand)
            else:
                # Measured imported quadruped chains. Do not confuse the head chain with legs.
                legs=[('tripo::0_Left_Limb_3','tripo::0_Left_Limb_4',1),
                      ('bone_17','bone_18',-1),('tripo::1_Left_Limb_1','tripo::1_Left_Limb_2',-1),
                      ('tripo::0_Right_Limb_1','tripo::0_Right_Limb_2',1)]
                for upper,lower,sign in legs:
                    swing=math.sin(phase)*sign
                    rotate_bone(rig,upper,(.38 if name=='Run' else .025)*swing)
                    rotate_bone(rig,lower,max(0,-swing)*(.48 if name=='Run' else .02))
                rotate_bone(rig,'tripo::Head_0',.3*math.sin(frame/frames*math.pi) if name=='Attack' else .025*math.sin(phase))
            key_pose(rig,frame+1,[b.name for b in rig.pose.bones])
        actions[name]=action
    return actions


def export_model(obj, identity, actions=None, extras=()):
    """Write one asset and original textures; normalize root translation only."""
    folder=OUTPUT/identity
    folder.mkdir(parents=True,exist_ok=True)
    obj.location=(0,0,0)
    obj.rotation_euler=(0,0,0)
    meshes=[obj] if obj.type=='MESH' else [m for m in bpy.context.scene.objects if m.type=='MESH' and m.parent==obj]
    meshes+=list(extras)
    materials=[]
    for mesh in meshes:
        for material in mesh.data.materials:
            if not material: continue
            color=material.diffuse_color[:]
            image=next((n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None) if material.use_nodes else None
            texture_name=''
            if image:
                texture_name=image.name.replace('/','_')+'.png'
                image.filepath_raw=str(folder/texture_name)
                image.file_format='PNG'
                image.save()
            materials.append({'name':material.name,'texture':texture_name,'color':color})
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    for mesh in meshes: mesh.select_set(True)
    for child in obj.children:
        if child.type=='EMPTY':child.select_set(True)
    bpy.context.view_layer.objects.active=obj
    if actions:
        obj.animation_data_clear()
        for name,action in actions.items():
            track=obj.animation_data_create().nla_tracks.new()
            track.name=name
            strip=track.strips.new(name,1,action)
            strip.name=name
    bpy.ops.export_scene.fbx(filepath=str(folder/(identity+'.fbx')),use_selection=True,
        add_leaf_bones=False,bake_anim=bool(actions),bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,
        path_mode='COPY',embed_textures=False,axis_forward='-Z',axis_up='Y')
    if actions: obj.animation_data_clear()
    return {'id':identity,'source':obj.name,'rig_bones':len(obj.data.bones) if obj.type=='ARMATURE' else 0,
            'clips':list(actions or {}),'materials':materials}


def main():
    OUTPUT.mkdir(parents=True,exist_ok=True)
    bpy.context.scene.render.fps=30
    records=[]
    for source,identity in STATIC.items():
        obj=bpy.data.objects.get(source)
        if obj is None: raise ValueError('Missing imported model: '+source)
        extras=[]
        if identity=='Pasture':
            from pasture_materials import add_straw_cover
            extras=[add_straw_cover(obj)]
        records.append(export_model(obj,identity,extras=extras))
    from modular_stone_wall import clip_mesh
    wall=bpy.data.objects['stone wall']
    middle=bpy.data.objects.new('Continuous wall middle',clip_mesh(wall.data,-.34,.34,'WallMiddle'))
    bpy.context.scene.collection.objects.link(middle)
    records.append(export_model(middle,'WallMiddle'))
    approved=import_actions(ROOT/'art/models/tripo-exploder/TripoExploder.blend',['Idle','Walk','Run','Attack'])
    with bpy.data.libraries.load(str(ROOT/'art/models/tripo-siege/SiegeAnimations.blend')) as (available, loaded):
        loaded.objects=['头槌巨尸.001','Greek Fire siege.001','tripo_node_bdfaf021.001']
    authored_ram,greek,greek_mesh=loaded.objects
    ram=authored_ram.animation_data.action
    greek_action=greek.animation_data.action
    bpy.context.scene.collection.objects.link(greek)
    bpy.context.scene.collection.objects.link(greek_mesh)
    records.append(export_model(greek,'GreekFire',{'Attack':greek_action}))
    for source,identity in RIGGED.items():
        obj=bpy.data.objects.get(source)
        if obj is None: raise ValueError('Missing imported rig: '+source)
        actions=create_basic_actions(obj,identity)
        if identity=='ExploderLatest':
            from tripo_exploder import create_actions
            actions=create_actions(obj)
        if identity in ('Walker','Headbutter','CageBoss') and 'Hips' in obj.pose.bones:
            actions['Run']=approved['Run']
            actions['Idle']=approved['Idle']
        if identity=='Headbutter': actions['Attack']=ram
        records.append(export_model(obj,identity,actions))
    # These accepted animation exports are reused byte-for-byte by the Unity importer.
    records.extend([{'id':'Crossbow','source':'TripoCrossbow/ByzantineCrossbow.fbx','reuse':True},
                    {'id':'Exploder','source':'TripoExploder/TripoExploder.fbx','reuse':True}])
    (OUTPUT/'manifest.json').write_text(json.dumps({'models':records},indent=2))
    # Keep the accepted heavy gait / mechanical pivots on every future full export too.
    from coastal_motion_revision import main as revise_motions
    revise_motions()
    print('COASTAL EXPORT PASS',len(records),'models; live source untouched',flush=True)


if __name__=='__main__': main()
