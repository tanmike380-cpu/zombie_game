"""Refine locomotion and archery on accepted original rigs; preserve imported geometry."""
import math
import sys
import json
from pathlib import Path
import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0,str(Path(__file__).parent))
from humanoid_gait import create_gait, add_motion_markers
from tripo_crossbow import weapon_pose, pose_arm, reset_pose, set_bone
from coastal_exports import ROOT, OUTPUT, export_model
from siege_motion import start_action


def export_crossbow():
    """Only replace Run in the approved source file; keep existing aiming/reload clips."""
    path=ROOT/'art/models/tripo-crossbow/ByzantineCrossbow.blend'
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
    if bpy.data.actions.get('Run'):bpy.data.actions['Run'].name='Archived_Lateral_Run'
    action,stride=create_gait(rig,'Run',lambda t:weapon_pose(rig,.18,bob=.002*math.cos(t*math.tau*2)))
    add_motion_markers(rig,stride)
    rig.animation_data.action=action;bpy.context.scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(path))
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    for child in rig.children:child.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/_Game/Art/TripoCrossbow/ByzantineCrossbow.fbx'),
        use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,bake_anim_simplify_factor=0,path_mode='COPY',embed_textures=True,
        axis_forward='-Z',axis_up='Y')
    print('CROSSBOW GAIT PASS: original skeleton; forward/back foot trajectory; cyclic loop; stride',stride,flush=True)


def archery_pose(rig, phase=None):
    """Release, follow-through, nock, draw-to-cheek and hold. Idle is ready, not a first-shot pop."""
    for name,angle in (('Hips',-.28),('UpperChest',-.8)):
        bone=rig.pose.bones[name]
        set_bone(rig,name,bone.head.copy(),Quaternion(Vector((0,0,1)),angle) @ bone.bone.matrix_local.to_quaternion())
    head=rig.pose.bones['Head']
    set_bone(rig,'Head',head.head.copy(),head.bone.matrix_local.to_quaternion())
    left=rig.data.bones['Left_Hand'].matrix_local.copy();left.translation=Vector((.025,-.30,.79))
    right=rig.data.bones['Right_Hand'].matrix_local.copy()
    keys=((0,(-.055,-.035,.815)),(.09,(-.065,.015,.825)),(.32,(-.13,.035,.60)),
          (.52,(-.03,-.165,.77)),(.90,(-.055,-.035,.815)),(1,(-.055,-.035,.815)))
    target=Vector(keys[0][1])
    if phase is not None:
        for (start,first),(end,last) in zip(keys,keys[1:]):
            if start<=phase<=end:
                t=(phase-start)/(end-start);t=t*t*(3-2*t);target=Vector(first).lerp(Vector(last),t);break
    right.translation=target
    errors=[pose_arm(rig,'Left',left),pose_arm(rig,'Right',right)]
    if max(errors)>.005:raise ValueError(f'Archer hand exceeds source reach at phase {phase}: {errors}')


def export_archer():
    """Use the actual archer mesh/rig, with rigid bow weighting and no invented replacement model."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    saved=ROOT/'art/models/coastal-polish/InfantryMotion.blend'
    source=saved if saved.exists() else ROOT/'Builds/ArtReview/GateTowerPasture/CoastalImportsSnapshot.blend'
    with bpy.data.libraries.load(str(source)) as (_,loaded):
        loaded.objects=['BZT archer Elite','tripo_node_7889d5ea']
    rig,mesh=loaded.objects
    for obj in loaded.objects:bpy.context.scene.collection.objects.link(obj)
    rig.location=(0,0,0);rig.rotation_euler=(0,0,0)
    from tripo_model_io import audit_model
    audit_path=ROOT/'Builds/ArtReview/archer-rig-audit.json';audit_path.parent.mkdir(parents=True,exist_ok=True)
    audit=audit_model(rig,mesh,audit_path)
    bow=next(part['indices'] for part in audit['components'] if part['count']==358)
    for group in mesh.vertex_groups:group.remove(bow)
    mesh.vertex_groups['Left_Hand'].add(bow,1,'REPLACE')
    actions={}
    for name in ('Idle','Attack'):
        action=start_action(rig,'Archer_'+name)
        for frame in range(49):
            reset_pose(rig);archery_pose(rig,None if name=='Idle' else frame/48)
            for bone in rig.pose.bones:
                bone.keyframe_insert('location',frame=frame+1);bone.keyframe_insert('rotation_quaternion',frame=frame+1)
        actions[name]=action
    actions['Run'],stride=create_gait(rig,'Archer_Run',lambda t:archery_pose(rig))
    add_motion_markers(rig,stride)
    muzzle=bpy.data.objects.new('Muzzle',None);bpy.context.scene.collection.objects.link(muzzle)
    muzzle.parent=rig;muzzle.parent_type='BONE';muzzle.parent_bone='Left_Hand'
    # Bone-parent origin is at its tail; establish the original grip marker through its matrix.
    reset_pose(rig);bpy.context.view_layer.update()
    muzzle.matrix_world=rig.matrix_world @ Matrix.Translation(Vector((.185,-.11,.51)))
    rig.animation_data.action=actions['Attack'];bpy.context.scene.frame_set(1);bpy.context.scene.render.fps=30
    source=ROOT/'art/models/coastal-polish/InfantryMotion.blend'
    bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(source))
    record=export_model(rig,'Archer',actions)
    manifest=json.loads((OUTPUT/'manifest.json').read_text())
    manifest['models']=[record if item['id']=='Archer' else item for item in manifest['models']]
    (OUTPUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
    print('ARCHERY PASS: original 67 bones, rigid original bow, release/nock/draw/hold cycle',flush=True)


if __name__=='__main__':
    export_crossbow()
    export_archer()
