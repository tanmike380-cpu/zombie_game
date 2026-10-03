"""Author original-model hound/wall revisions from the saved Tripo snapshot, outside the live scene."""
import json
import hashlib
import sys
from pathlib import Path
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).parent))
from coastal_exports import ROOT, OUTPUT, export_model
from hound_motion import create_hound_actions, STRIDE
from modular_stone_wall import clip_mesh
from siege_preview import create_stage, set_preview_workspace
from tripo_model_io import point_at


def mesh_signature(mesh):
    return hashlib.sha256(str([(tuple(v.co), [(g.group, g.weight) for g in v.groups])
                              for v in mesh.data.vertices]).encode()).hexdigest()


def main():
    review = ROOT/'Builds/ArtReview/CoastalPolish'
    models = ROOT/'art/models/coastal-polish'
    review.mkdir(parents=True, exist_ok=True)
    models.mkdir(parents=True, exist_ok=True)
    hound = bpy.data.objects['Hunger Dog']
    dog_mesh = next(obj for obj in hound.children if obj.type == 'MESH')
    signature = mesh_signature(dog_mesh)
    wall = bpy.data.objects['stone wall']
    source_wall = wall.data.copy()
    middle = bpy.data.objects.new('Fortress wall middle', clip_mesh(source_wall, -.34, .34, 'Wall middle source'))
    bpy.context.scene.collection.objects.link(middle)
    # User correction: enlarge the ENTIRE original model, never extend its lower shaft.
    wall_width = max(v.co.x for v in wall.data.vertices)-min(v.co.x for v in wall.data.vertices)
    uniform_scale = 18/wall_width
    for obj in (wall, middle):
        obj.scale = (uniform_scale,)*3
        obj.location = (0, 0, 0)
    bpy.context.scene.render.fps = 30
    actions = create_hound_actions(hound)
    assert signature == mesh_signature(dog_mesh), 'Original dog mesh or weights changed'
    stature = max(v.co.z for v in dog_mesh.data.vertices)-min(v.co.z for v in dog_mesh.data.vertices)
    for name, point in [('ForwardOrigin', (0, 0, 0)), ('ForwardAim', (0, -1, 0)), ('StatureTop', (0, 0, stature)),
                        ('StrideEnd', (0, -STRIDE, 0))]:
        marker = bpy.data.objects.new(name, None)
        bpy.context.scene.collection.objects.link(marker)
        marker.parent = hound
        marker.location = point
    replacements = {'Hound': export_model(hound, 'Hound', actions),
                    'Wall': export_model(wall, 'Wall'), 'WallMiddle': export_model(middle, 'WallMiddle')}
    replacements['Hound']['stride_source'] = STRIDE
    manifest = json.loads((OUTPUT/'manifest.json').read_text())
    manifest['models'] = [replacements.get(record['id'], record) for record in manifest['models']]
    (OUTPUT/'manifest.json').write_text(json.dumps(manifest, indent=2))
    # Save an editable Blender source, retaining all original texture images and rig names.
    scene = bpy.data.scenes.new('01_Hound_Foot_Planted')
    scene.collection.objects.link(hound)
    scene.collection.objects.link(dog_mesh)
    for child in hound.children:
        if child.type == 'EMPTY':
            scene.collection.objects.link(child)
    bpy.context.window.scene = scene
    hound.animation_data_create().action = actions['Run']
    create_stage(scene, 'Hound')
    scene.render.fps = 30
    scene.frame_end = 33
    scene.camera.location = (1.5, -1.5, .85)
    point_at(scene.camera, (0, 0, .23))
    scene.camera.data.ortho_scale = 1.5
    heights = []
    for frame in range(1, 34):
        scene.frame_set(frame)
        graph = bpy.context.evaluated_depsgraph_get()
        mesh = dog_mesh.evaluated_get(graph)
        lowest = min((mesh.matrix_world @ v.co).z for v in mesh.data.vertices)
        heights.append(lowest)
        if frame in (1, 9, 17, 25):
            scene.render.filepath = str(review/f'hound-{frame:02d}.png')
            bpy.ops.render.render(write_still=True)
    assert max(abs(height) for height in heights) < .006, heights
    # Keep an inspectable wall in its own scene; preview it at game-space scale.
    wall_scene = bpy.data.scenes.new('02_Fortress_Wall')
    wall_scene.collection.objects.link(wall)
    wall_scene.collection.objects.link(middle)
    wall.location = (0, 0, 0)
    middle.hide_render = True
    bpy.context.window.scene = wall_scene
    create_stage(wall_scene, 'GreekFire')
    wall_scene.camera.location = (22, -28, 18)
    point_at(wall_scene.camera, (0, 0, 3))
    wall_scene.camera.data.ortho_scale = 24
    wall_scene.render.filepath = str(review/'fortress-wall.png')
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene = scene
    set_preview_workspace(scene)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(models/'CoastalPolish.blend'))
    report = {'hound_ground_max_error': max(abs(height) for height in heights),
              'hound_mesh_and_weights_unchanged': True, 'stride_source': STRIDE,
              'wall_height_tiles': (max(v.co.z for v in wall.data.vertices)-min(v.co.z for v in wall.data.vertices))*uniform_scale,
              'wall_uniform_scale': uniform_scale, 'headbutter_height_tiles': 6,
              'materials': 'original Tripo textures; no new lighting'}
    (review/'verification.json').write_text(json.dumps(report, indent=2))
    print('COASTAL ART POLISH PASS', report, flush=True)


if __name__ == '__main__':
    main()
