"""Render actual authored animation poses for visual QA, not generated concept substitutes."""
import sys
from pathlib import Path
import bpy

sys.path.insert(0,str(Path(__file__).parent))
from tripo_model_io import setup_render, point_at

ROOT=Path(__file__).resolve().parents[2]


def main():
    rig=next(obj for obj in bpy.context.scene.objects if obj.type=='ARMATURE')
    rig.location=(0,0,0)
    rig.animation_data_create()
    if bpy.context.scene.world is None:bpy.context.scene.world=bpy.data.worlds.new('Pose review world')
    setup_render();scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.resolution_x=640;scene.render.resolution_y=760
    folder=ROOT/'Builds/ArtReview/CoastalMotion';folder.mkdir(parents=True,exist_ok=True)
    for action,frame,name in [('Archer_Attack',1,'drawn'),('Archer_Attack',6,'release'),
                               ('Archer_Attack',26,'nock'),('Archer_Run',9,'step')]:
        rig.animation_data.action=bpy.data.actions[action];scene.frame_set(frame)
        scene.render.filepath=str(folder/(name+'.png'));bpy.ops.render.render(write_still=True)
    print('POSE REVIEW RENDERED',flush=True)


if __name__=='__main__':main()
