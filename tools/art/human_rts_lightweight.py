"""Bake a reversible static RTS review derivative, preserving the authored master."""
import argparse
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from freeze_human_base import BODY_NAME, PROJECT_ROOT, configure_view, get_file_hash
from human_weapon_poses import MASTER, PRESETS, REVIEW_DIR, verify_pose, is_head_equipment
from human_rts_hands import HAND_PREFIX
from export_human_preview import merge_preview_parts

CONFIG = PROJECT_ROOT/'art/characters/human_base/rts_lightweight.json'
OUTPUT = PROJECT_ROOT/'Builds/ArtReview/RTSLightweight-v1'


def collect_character_objects():
    """Exclude review lighting/floor, hidden objects, and non-geometry."""
    return [obj for obj in bpy.context.scene.objects
            if obj.type in ('MESH', 'CURVE') and not obj.hide_render and not obj.hide_get()
            and 'floor' not in obj.name.lower() and 'ground' not in obj.name.lower()]


def get_part_center(obj):
    return obj.matrix_world @ (sum((Vector(corner) for corner in obj.bound_box), Vector())/8)


def select_readable_details(objects, config):
    """Spatially thin rivets rather than deleting the armor's metal fasteners."""
    retained, removed, rivet_centers = [], [], {}
    for obj in sorted(objects, key=lambda item: item.name):
        name = obj.name
        keep = True
        if 'rivet' in name:
            group = name.split('.')[0]
            centers = rivet_centers.setdefault(group, [])
            center = get_part_center(obj)
            spacing = config['rivet_spacing_m' if name.startswith('Armor') else 'helmet_rivet_spacing_m']
            keep = all((center-other).length >= spacing for other in centers)
            if keep:
                centers.append(center)
        for label, stride in (('tassel strand', config['tassel_stride']),
                              ('quilt seam', config['quilt_seam_stride']),
                              ('fine tailored seam', config['quilt_seam_stride'])):
            if label in name:
                suffix = name.rsplit('.', 1)[-1]
                keep = (int(suffix) if suffix.isdigit() else 0) % stride == 0
        (retained if keep else removed).append(obj)
    return retained, removed


def get_part_budget(name, config):
    budgets = config['part_budgets']
    if name.startswith(HAND_PREFIX):
        return budgets['hand']
    if name == BODY_NAME:
        return budgets['body']
    if 'rivet' in name:
        return budgets['rivet']
    if '.eye.' in name:
        return budgets['eye']
    if name.startswith('veteran_'):
        return budgets['weapon']
    if name.startswith(('Cuff', 'Collar', 'Equipment | folded', 'Equipment | double lobed')):
        return budgets['trim']
    if any(label in name for label in (' seam', 'edge', ' lip')):
        return budgets['small']
    if 'split crimson skirt' in name:
        return budgets['skirt']
    if name.startswith(('Armor', 'Equipment | smooth forged')):
        return budgets['armor']
    if name.startswith(('Tunic', 'Sleeve', 'Trousers', 'Shoes', 'Sash', 'Bindings')):
        return budgets['cloth']
    return budgets['small']


def compact_proportions(point, config, enlarge_head=False):
    """Mild broadening/shortening, with a slightly clearer head; not a chibi remake."""
    pivot = Vector((0, 0, config['head_pivot_height_m']))
    weight = max(0, min(1, (point.z-1.52)/.11)) if enlarge_head else 0
    point = point+(point-pivot)*(weight*(config['head_scale']-1))
    return Vector(tuple(point[index]*config['body_scale'][index] for index in range(3)))


def bake_part(obj, depsgraph, config):
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
    mesh.calc_loop_triangles()
    before = len(mesh.loop_triangles)
    duplicate = bpy.data.objects.new(obj.name+' | RTS', mesh)
    bpy.context.scene.collection.objects.link(duplicate)
    center = get_part_center(obj)
    for vertex in mesh.vertices:
        point = obj.matrix_world @ vertex.co
        if 'rivet' in obj.name:
            scale = config['rivet_scale' if obj.name.startswith('Armor') else 'helmet_rivet_scale']
            point = center+(point-center)*scale
        enlarge_head = is_head_equipment(obj) or (obj.name == BODY_NAME and abs(point.x) < .18 and point.y > -.16)
        vertex.co = compact_proportions(point, config, enlarge_head)
    duplicate.matrix_world = Matrix.Identity(4)
    budget = get_part_budget(obj.name, config)
    thickness = next((modifier for modifier in obj.modifiers if modifier.type == 'SOLIDIFY'), None)
    # Collapse one surface, then reconstruct thickness. Collapsing both close layers
    # together pinches their rims into spikes and tears on cuffs/leg bindings.
    if thickness:
        budget = int(budget*.40)
    if before > budget:
        bpy.context.view_layer.objects.active = duplicate
        modifier = duplicate.modifiers.new('RTS silhouette budget', 'DECIMATE')
        modifier.ratio = budget/before
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    if thickness:
        modifier = duplicate.modifiers.new('Rebuild clean garment thickness', 'SOLIDIFY')
        modifier.thickness = thickness.thickness
        modifier.offset = thickness.offset
        modifier.use_even_offset = thickness.use_even_offset
        modifier.material_offset = thickness.material_offset
        bpy.context.view_layer.objects.active = duplicate
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    for face in duplicate.data.polygons:
        face.use_smooth = True
    after = sum(len(face.vertices)-2 for face in duplicate.data.polygons)
    return duplicate, {'name': obj.name, 'before_triangles': before, 'after_triangles': after}


def validate_candidate(objects, report, config):
    """Enforce actual mesh budget and retained identifying equipment/rivets."""
    triangles = sum(len(face.vertices)-2 for obj in objects for face in obj.data.polygons)
    if triangles <= 0 or triangles > config['maximum_triangles']:
        raise ValueError(f'RTS review triangle budget exceeded: {triangles}')
    names = [entry['name'] for entry in report['parts']]
    for required in ('double lobed belt gourd', 'smooth forged iron helmet bowl',
                     'matchlock_carved_stock', 'RTS Hand | trigger', 'RTS Hand | support', 'brass rivet'):
        if not any(required in name for name in names):
            raise ValueError(f'Missing identity feature: {required}')
    if sum('rivet' in name for name in names) < 20:
        raise ValueError('Armor lost its readable rivet pattern')
    if any(obj.modifiers for obj in objects):
        raise ValueError('Static candidate still has live subdivision/geometry modifiers')
    return triangles


def render_candidate(output, preset):
    scene = bpy.context.scene
    scene.cycles.samples = 32
    for label, yaw, pitch, target, scale, resolution in preset['views']:
        configure_view(yaw, pitch, target, scale, resolution)
        scene.render.filepath = str(output/f'{label}.png')
        bpy.ops.render.render(write_still=True)


def verify_saved_candidate(name, config):
    """Reopen output, recount actual triangles and exercise missing-rivet guards."""
    output = OUTPUT/name
    report = json.loads((output/'verification.json').read_text())
    path = output/f'{name}_rts.blend'
    if report['blend_sha256'] != get_file_hash(path) or report['config_sha256'] != get_file_hash(CONFIG):
        raise ValueError(f'{name}: stale candidate/config')
    if report['source_sha256'] != get_file_hash(MASTER):
        raise ValueError(f'{name}: frozen master differs')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    objects = collect_character_objects()
    if validate_candidate(objects, report, config) != report['triangles']:
        raise ValueError(f'{name}: saved mesh count mismatch')
    broken = dict(report, parts=[entry for entry in report['parts'] if 'rivet' not in entry['name']])
    try:
        validate_candidate(objects, broken, config)
    except ValueError:
        pass
    else:
        raise AssertionError('Missing-rivet regression was not rejected')
    print('RTS_CANDIDATE_VERIFIED', name, report['triangles'], 'triangles; rivet guard passed', flush=True)


def build_candidate(name, preset, config, render):
    verify_pose(name, preset)
    originals = collect_character_objects()
    # Use one subdivision level before decimation; keep masks and pose deformation.
    for obj in originals:
        for modifier in obj.modifiers:
            if modifier.type == 'MULTIRES':
                modifier.levels = min(1, modifier.total_levels)
            if modifier.type == 'SOLIDIFY':
                modifier.show_viewport = False
    bpy.context.view_layer.update()
    kept, removed = select_readable_details(originals, config)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    parts, records = [], []
    for index, obj in enumerate(kept):
        part, record = bake_part(obj, depsgraph, config)
        parts.append(part)
        records.append(record)
        if index % 150 == 0:
            print('RTS_BAKE_PROGRESS', index, '/', len(kept), flush=True)
    report = {'pose': name, 'source_sha256': get_file_hash(MASTER),
              'pose_sha256': get_file_hash(REVIEW_DIR/name/f'{name}.blend'),
              'config_sha256': get_file_hash(CONFIG), 'parts': records,
              'removed_microdetails': [obj.name for obj in removed],
              'rivets_before': sum('rivet' in obj.name for obj in originals),
              'rivets_after': sum('rivet' in obj.name for obj in kept),
              'runtime_animation_ready': False}
    # Originals remain in the untouched input file; the output contains only baked geometry.
    for obj in originals:
        bpy.data.objects.remove(obj, do_unlink=True)
    parts = merge_preview_parts(parts)
    print('RTS_MESH_COUNTS', sorted(records, key=lambda item: item['after_triangles'], reverse=True)[:12], flush=True)
    report['triangles'] = validate_candidate(parts, report, config)
    report['mesh_objects'] = len(parts)
    report['materials'] = len({material.name for obj in parts for material in obj.data.materials if material})
    scene = bpy.context.scene
    scene['art_status'] = 'RTS static review candidate: not rigged for runtime crowds'
    output = OUTPUT/name
    output.mkdir(parents=True, exist_ok=True)
    configure_view(*preset['views'][0][1:])
    bpy.context.preferences.filepaths.save_version = 0
    blend_path = output/f'{name}_rts.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    report['blend_sha256'] = get_file_hash(blend_path)
    (output/'verification.json').write_text(json.dumps(report, indent=2)+'\n')
    print('RTS_CANDIDATE_SAVED', name, report['triangles'], 'triangles', report['rivets_after'], 'rivets', flush=True)
    verify_saved_candidate(name, config)
    if render:
        render_candidate(output, preset)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pose', choices=('all', 'aim', 'chest'), default='all')
    parser.add_argument('--no-render', action='store_true')
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    config = json.loads(CONFIG.read_text())
    presets = json.loads(PRESETS.read_text())['poses']
    source_hash = get_file_hash(MASTER)
    for name, preset in presets.items():
        if args.pose in ('all', name):
            if args.verify_only:
                verify_saved_candidate(name, config)
            else:
                build_candidate(name, preset, config, not args.no_render)
    if source_hash != get_file_hash(MASTER):
        raise ValueError('Frozen source changed during RTS derivation')


if __name__ == '__main__':
    main()
