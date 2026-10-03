"""Refresh the saved original-rig rolling clip and check bottom contact direction."""
import sys
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).parent))
from siege_locomotion import create_directional_movement, wheel_roll_angle


def main():
    source=Path(__file__).resolve().parents[2]/'art/models/tripo-siege/SiegeAnimations.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene=bpy.data.scenes['04_GreekFire_Movement'];bpy.context.window.scene=scene
    scene.frame_set(1)
    rig=next(obj for obj in scene.objects if obj.type=='ARMATURE')
    create_directional_movement(rig)
    # For chassis +Y travel the wheel's bottom must travel -Y relative to its axle.
    for distance in (.001,-.001):
        bottom=Vector((0,0,-.125))
        moved=Matrix.Rotation(wheel_roll_angle(distance,0,0),3,'X') @ bottom
        assert abs(moved.y+distance)<1e-6,'Wheel slips in the wrong direction'
    scene.frame_set(1)
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    print('VEHICLE ROLL PASS: forward/reverse contact direction, original mesh and rig retained',flush=True)


if __name__=='__main__':main()
